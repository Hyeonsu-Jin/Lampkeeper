// =============================================================
// 파일: ResourceViewStreamer.cs
// 역할: 자원 약 2만 개를 화면에 보인다. 플레이어 가까이(근거리)는 GameObject 풀로, 멀리(원거리)는 GPU 인스턴싱 한 번 그리기로 그린다.
//       자원 표(ClientWorldMirror.Resources)의 상태(타격 중·쓰러짐·부서짐)를 읽어 반영하고, 근거리 쓰러짐은 간단한 회전 연출을 한다.
//       조준한 자원을 찾아 주는 기능(TryAim)도 있다(공격 입력용).
// 레이어: adapters (Game.Adapters.Presentation/World)
// 관련: FR-009, FR-047, FR-049, NFR-011, NFR-012, NFR-016, DES-087 / SDD 3.10.4, 3.12.7
//
// 모양은 임시 기본 도형이다(M1-10). 위치 칸 묶음(ResourceCellGrid)으로 반경 안 자원만 골라, 이동하거나 표가 바뀔 때만 다시 고른다.
// 매 프레임 코드는 새로 할당하지 않는다(GetComponent·LINQ·new 없음).
// =============================================================
using System.Collections.Generic;
using Game.Application.Replication;
using Game.Application.WorldSync;
using Game.Domain.Gathering;
using Game.WorldGen.Model;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Adapters.Presentation.World
{
    /// <summary>자원 오브젝트 표시 관리자.</summary>
    public sealed class ResourceViewStreamer : MonoBehaviour
    {
        /// <summary>근거리 반경(m). 이 안은 GameObject(충돌체 포함)로 그린다.</summary>
        public const float DefaultNearRadiusMeters = 60f;

        /// <summary>원거리 반경(m). 근거리 밖 이 안까지 인스턴싱으로 그린다(카메라 먼 거리와 맞춘다).</summary>
        public const float DefaultFarRadiusMeters = 350f;

        /// <summary>마지막으로 고른 자리에서 이만큼 움직이면 다시 고른다(m).</summary>
        public const float RefreshDistanceMeters = 4f;

        /// <summary>다시 고를 일이 생겼는지 확인하는 간격(초).</summary>
        public const float CheckIntervalSeconds = 0.1f;

        /// <summary>쓰러지는 속도(도/초). 90도를 0.6초에 눕는다.</summary>
        private const float FallDegreesPerSecond = 150f;

        /// <summary>인스턴싱 한 번에 보낼 수 있는 최대 개수(Unity 한도).</summary>
        private const int InstancesPerDraw = 1023;

        private const int KindCount = 5; // 0 = 없음, 1~4 = 나무·돌·철광석·석탄
        private const float MillimetersPerMeter = 1000f;

        [SerializeField] private Material _treeMaterial;
        [SerializeField] private Material _stoneMaterial;
        [SerializeField] private Material _ironMaterial;
        [SerializeField] private Material _coalMaterial;
        [SerializeField] private float _nearRadius = DefaultNearRadiusMeters;
        [SerializeField] private float _farRadius = DefaultFarRadiusMeters;

        /// <summary>근거리 오브젝트 하나(풀에서 빌려 쓰고 돌려준다).</summary>
        private sealed class NearView
        {
            public int Index = -1;
            public GameObject Object;
            public Transform Transform;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public BoxCollider Box;
            public int KindValue;
            public Vector3 BasePosition;
            public float YawDegrees;
            public float SizeFactor;
            public float VisualScale;
            public float FallDegrees;
            public float TargetFallDegrees;
            public float FallYawDegrees;
            public bool Visible;
            public int Stamp;
        }

        private readonly Mesh[] _meshes = new Mesh[KindCount];
        private readonly Material[] _materials = new Material[KindCount];
        private readonly Matrix4x4[][] _farMatrices = new Matrix4x4[KindCount][];
        private readonly int[] _farCounts = new int[KindCount];
        private readonly Dictionary<int, NearView> _near = new Dictionary<int, NearView>();
        private readonly Dictionary<Collider, NearView> _byCollider = new Dictionary<Collider, NearView>();
        private readonly List<NearView> _nearList = new List<NearView>();
        private readonly List<NearView> _removeScratch = new List<NearView>();
        private readonly Stack<NearView> _pool = new Stack<NearView>();
        private readonly List<int> _candidates = new List<int>(4096);

        private ClientWorldMirror _mirror;
        private LocalWorld _local;
        private ResourceCellGrid _grid;
        private Transform _viewer;
        private Transform _nearRoot;
        private Vector3 _lastRefreshPosition;
        private int _lastVersion = -1;
        private float _nextCheck;
        private int _stamp;
        private bool _needsRefresh;

        /// <summary>지금 GameObject로 그리는 근거리 자원 수(보이는 것만).</summary>
        public int NearVisibleCount { get; private set; }

        /// <summary>지금 인스턴싱으로 그리는 원거리 자원 수.</summary>
        public int FarCount { get; private set; }

        /// <summary>풀에 만들어 둔 근거리 오브젝트 수(쓰는 것 + 쉬는 것).</summary>
        public int PooledObjectCount => _nearList.Count + _pool.Count;

        /// <summary>근거리 반경(m).</summary>
        public float NearRadius => _nearRadius;

        /// <summary>원거리 반경(m).</summary>
        public float FarRadius => _farRadius;

        private void Awake()
        {
            _materials[(int)ResourceKind.Tree] = _treeMaterial;
            _materials[(int)ResourceKind.Stone] = _stoneMaterial;
            _materials[(int)ResourceKind.IronOre] = _ironMaterial;
            _materials[(int)ResourceKind.Coal] = _coalMaterial;
            for (int kind = 1; kind < KindCount; kind++)
            {
                _meshes[kind] = GetPrimitiveMesh(ResourceViewMath.ShapeOf(kind).Primitive);
                _farMatrices[kind] = new Matrix4x4[256];
            }
            var rootObject = new GameObject("NearResources");
            _nearRoot = rootObject.transform;
            _nearRoot.SetParent(transform, false);
        }

        /// <summary>
        /// 표시할 월드 사본과 기준(플레이어) 위치를 정한다. 사본에 월드가 붙은 뒤에 불러야 한다.
        /// </summary>
        /// <param name="mirror">월드 사본(생성 표 + 자원 상태 표)</param>
        /// <param name="viewer">기준 위치(플레이어 카메라 또는 몸체)</param>
        public void Initialize(ClientWorldMirror mirror, Transform viewer)
        {
            if (mirror == null || mirror.Local == null)
            {
                Debug.LogError("[ResourceView] 월드 사본에 생성된 월드가 없어 자원을 그릴 수 없습니다.");
                return;
            }
            ClearAll();
            _mirror = mirror;
            _local = mirror.Local;
            _viewer = viewer;
            _grid = new ResourceCellGrid(_local);
            _lastVersion = -1;
            _needsRefresh = true;
        }

        /// <summary>
        /// 지금 기준 위치로 근거리·원거리 대상을 다시 고른다. 보통 Update가 알아서 부르고, 시험이 바로 확인할 때 쓴다.
        /// </summary>
        public void RefreshNow()
        {
            if (_grid == null || _viewer == null)
            {
                return;
            }
            Refresh();
        }

        /// <summary>
        /// 조준선(카메라 앞)에 걸린 근거리 자원을 찾는다. 공격 입력이 어떤 자원을 때릴지 정할 때 쓴다.
        /// </summary>
        /// <param name="origin">시선 시작점(카메라)</param>
        /// <param name="maxDistance">닿는 거리(m)</param>
        /// <param name="resourceIndex">찾은 자원 번호</param>
        public bool TryAim(Transform origin, float maxDistance, out int resourceIndex)
        {
            resourceIndex = -1;
            if (origin == null)
            {
                return false;
            }
            if (Physics.Raycast(origin.position, origin.forward, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider != null
                && _byCollider.TryGetValue(hit.collider, out NearView view)
                && view.Visible)
            {
                resourceIndex = view.Index;
                return true;
            }
            return false;
        }

        private void Update()
        {
            if (_grid == null || _viewer == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now >= _nextCheck)
            {
                _nextCheck = now + CheckIntervalSeconds;
                Vector3 p = _viewer.position;
                float moved = (p - _lastRefreshPosition).sqrMagnitude;
                if (_needsRefresh || moved >= RefreshDistanceMeters * RefreshDistanceMeters || _mirror.Resources.Version != _lastVersion)
                {
                    Refresh();
                }
            }

            AnimateNear(Time.deltaTime);
            DrawFar();
        }

        private void OnDestroy() => ClearAll();

        // ---------- 대상 고르기 ----------

        private void Refresh()
        {
            _needsRefresh = false;
            _lastVersion = _mirror.Resources.Version;
            Vector3 viewerPosition = _viewer.position;
            _lastRefreshPosition = viewerPosition;
            _stamp++;

            for (int k = 0; k < KindCount; k++)
            {
                _farCounts[k] = 0;
            }

            ResourceEntry[] entries = _local.Generated.Resources;
            _grid.CollectWithin(viewerPosition.x, viewerPosition.z, _farRadius, _candidates);
            float nearSq = _nearRadius * _nearRadius;
            int farTotal = 0;

            for (int c = 0; c < _candidates.Count; c++)
            {
                int index = _candidates[c];
                ResourceEntry entry = entries[index];
                int kindValue = entry.Type <= (ushort)ResourceKind.Coal ? entry.Type : 0;
                if (kindValue == 0)
                {
                    continue;
                }
                _mirror.Resources.TryGet((uint)index, out ResourceState state);
                ResourceVisual visual = ResourceVisualRules.Compute((ResourceKind)kindValue, state);
                var basePosition = new Vector3(entry.PosXmm / MillimetersPerMeter, entry.PosYmm / MillimetersPerMeter, entry.PosZmm / MillimetersPerMeter);
                float dx = basePosition.x - viewerPosition.x;
                float dz = basePosition.z - viewerPosition.z;

                if (dx * dx + dz * dz <= nearSq)
                {
                    UpdateNear(index, kindValue, entry, basePosition, visual);
                }
                else if (visual.Visible)
                {
                    AddFar(kindValue, entry, basePosition, visual);
                    farTotal++;
                }
            }

            // 이번에 근거리 후보에 들지 못한 오브젝트는 풀로 돌려보낸다.
            _removeScratch.Clear();
            int visibleNear = 0;
            for (int i = 0; i < _nearList.Count; i++)
            {
                NearView view = _nearList[i];
                if (view.Stamp != _stamp)
                {
                    _removeScratch.Add(view);
                }
                else if (view.Visible)
                {
                    visibleNear++;
                }
            }
            for (int i = 0; i < _removeScratch.Count; i++)
            {
                ReleaseNear(_removeScratch[i]);
            }
            NearVisibleCount = visibleNear;
            FarCount = farTotal;
        }

        private void UpdateNear(int index, int kindValue, ResourceEntry entry, Vector3 basePosition, ResourceVisual visual)
        {
            bool isNew = !_near.TryGetValue(index, out NearView view);
            if (isNew)
            {
                view = AcquireNear();
                view.Index = index;
                view.KindValue = kindValue;
                view.BasePosition = basePosition;
                view.YawDegrees = ResourceViewMath.YawDegrees(entry.Yaw);
                view.SizeFactor = ResourceViewMath.SizeFactor(entry.ScaleQ8);
                ApplyShape(view, kindValue);
                _near[index] = view;
                _nearList.Add(view);
            }
            view.Stamp = _stamp;

            bool wasVisible = view.Visible;
            view.Visible = visual.Visible;
            view.VisualScale = visual.ScaleFactor;
            view.FallYawDegrees = visual.FallYawDegrees;
            view.TargetFallDegrees = visual.FallDegrees;

            // 새로 나타났거나 다시 보이게 된 오브젝트는 바로 최종 자세로, 보이던 오브젝트의 상태 변화만 회전 연출을 한다.
            if (isNew || !wasVisible)
            {
                view.FallDegrees = visual.FallDegrees;
            }

            view.Object.SetActive(view.Visible);
            // 쓰러졌거나 부서진 자원은 더 때릴 수 없고 길을 막지 않게 충돌을 끈다.
            view.Box.enabled = view.Visible && view.TargetFallDegrees < 1f;
            ApplyPose(view);
        }

        private void AddFar(int kindValue, ResourceEntry entry, Vector3 basePosition, ResourceVisual visual)
        {
            ResourceShape shape = ResourceViewMath.ShapeOf(kindValue);
            float size = ResourceViewMath.SizeFactor(entry.ScaleQ8) * visual.ScaleFactor;
            ResourceViewMath.ComputePose(basePosition, ResourceViewMath.YawDegrees(entry.Yaw), visual.FallDegrees, visual.FallYawDegrees,
                shape.CenterHeight * size, out Vector3 position, out Quaternion rotation);

            Matrix4x4[] array = _farMatrices[kindValue];
            int count = _farCounts[kindValue];
            if (count >= array.Length)
            {
                System.Array.Resize(ref array, array.Length * 2); // 새로 고를 때만 늘어난다(그리는 프레임에는 없음)
                _farMatrices[kindValue] = array;
            }
            array[count] = Matrix4x4.TRS(position, rotation, shape.Scale * size);
            _farCounts[kindValue] = count + 1;
        }

        // ---------- 근거리 풀 ----------

        private NearView AcquireNear()
        {
            if (_pool.Count > 0)
            {
                return _pool.Pop();
            }
            var go = new GameObject("Resource");
            go.transform.SetParent(_nearRoot, false);
            var view = new NearView
            {
                Object = go,
                Transform = go.transform,
                Filter = go.AddComponent<MeshFilter>(),
                Renderer = go.AddComponent<MeshRenderer>(),
                Box = go.AddComponent<BoxCollider>(),
            };
            _byCollider[view.Box] = view;
            return view;
        }

        private void ReleaseNear(NearView view)
        {
            _near.Remove(view.Index);
            _nearList.Remove(view);
            view.Index = -1;
            view.Visible = false;
            view.Object.SetActive(false);
            _pool.Push(view);
        }

        private void ApplyShape(NearView view, int kindValue)
        {
            Mesh mesh = _meshes[kindValue];
            view.Filter.sharedMesh = mesh;
            view.Renderer.sharedMaterial = _materials[kindValue];
            view.Box.center = mesh.bounds.center;
            view.Box.size = mesh.bounds.size;
        }

        private void ApplyPose(NearView view)
        {
            ResourceShape shape = ResourceViewMath.ShapeOf(view.KindValue);
            float size = view.SizeFactor * view.VisualScale;
            ResourceViewMath.ComputePose(view.BasePosition, view.YawDegrees, view.FallDegrees, view.FallYawDegrees,
                shape.CenterHeight * size, out Vector3 position, out Quaternion rotation);
            view.Transform.SetPositionAndRotation(position, rotation);
            view.Transform.localScale = shape.Scale * size;
        }

        private void AnimateNear(float deltaTime)
        {
            float step = FallDegreesPerSecond * deltaTime;
            for (int i = 0; i < _nearList.Count; i++)
            {
                NearView view = _nearList[i];
                if (view.Visible && !Mathf.Approximately(view.FallDegrees, view.TargetFallDegrees))
                {
                    view.FallDegrees = Mathf.MoveTowards(view.FallDegrees, view.TargetFallDegrees, step);
                    ApplyPose(view);
                }
            }
        }

        // ---------- 원거리 인스턴싱 ----------

        private void DrawFar()
        {
            if (FarCount == 0)
            {
                return;
            }
            // 컬링용 영역: 기준 위치를 중심으로 원거리 반경을 덮는 상자(영역이 0이면 컬링이 꺼질 수 있어 명시한다).
            var bounds = new Bounds(_viewer.position, new Vector3(_farRadius * 2.2f, _farRadius * 2.2f, _farRadius * 2.2f));
            for (int kind = 1; kind < KindCount; kind++)
            {
                int total = _farCounts[kind];
                Material material = _materials[kind];
                if (total == 0 || material == null)
                {
                    continue;
                }
                var parameters = new RenderParams(material)
                {
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true,
                    worldBounds = bounds,
                };
                for (int start = 0; start < total; start += InstancesPerDraw)
                {
                    int count = Mathf.Min(InstancesPerDraw, total - start);
                    Graphics.RenderMeshInstanced(parameters, _meshes[kind], 0, _farMatrices[kind], count, start);
                }
            }
        }

        // ---------- 정리·보조 ----------

        private void ClearAll()
        {
            for (int i = 0; i < _nearList.Count; i++)
            {
                _nearList[i].Object.SetActive(false);
                _pool.Push(_nearList[i]);
            }
            _nearList.Clear();
            _near.Clear();
            for (int k = 0; k < KindCount; k++)
            {
                _farCounts[k] = 0;
            }
            NearVisibleCount = 0;
            FarCount = 0;
            _grid = null;
        }

        /// <summary>기본 도형의 공유 mesh를 얻는다(임시 도형 오브젝트를 만들었다 바로 지운다).</summary>
        private static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            if (UnityEngine.Application.isPlaying)
            {
                Destroy(temp);
            }
            else
            {
                DestroyImmediate(temp);
            }
            return mesh;
        }
    }
}

// =============================================================
// 파일: CreatureViews.cs
// 역할: 클라이언트의 생물 사본(ClientCreatureStore)을 읽어 임시 도형(캡슐)으로 그린다. 스냅샷 사이는 사본이 보간하고,
//       모드(배회/추적/공격/사망)에 따라 색과 모양을 바꾼다. 사본에서 사라진 생물의 도형은 지운다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-008, NFR-017, DES-088 / SDD 3.11.9, 3.12
//
// 애니메이터·피격 반응·지역 변형 외형은 이후 작업(M2)이다. 표시 전용이라 물리 충돌체를 두지 않는다(판정은 호스트).
// 도형은 캡슐 GameObject이고 SRP Batcher로 그린다. 모드별 재질 4개는 시작할 때 기준 재질을 복제해 만든다.
// 매 프레임 할당을 피하려고 도형 목록·정리 목록을 재사용한다.
// =============================================================
using System.Collections.Generic;
using System.Diagnostics;
using Game.Application.Replication;
using Game.Domain.Common;
using Game.Domain.Creatures;
using UnityEngine;

namespace Game.Adapters.Presentation.World
{
    /// <summary>생물 표시 관리자(DES-088).</summary>
    public sealed class CreatureViews : MonoBehaviour
    {
        private const float StandingScaleY = 0.9f;   // 캡슐 기본 높이 2 m x 0.9 = 1.8 m
        private const float WidthScale = 0.8f;
        private const float DeadScaleY = 0.25f;      // 사망은 납작하게 눕힌 모양으로 구분한다
        private const int ModeCount = 4;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private sealed class View
        {
            public Transform Transform;
            public Renderer Renderer;
            public byte Mode = byte.MaxValue;
            public int Stamp;
        }

        [Tooltip("모드별 색의 기준이 되는 재질(복제해서 색만 바꾼다). 새 재질을 인스턴싱으로 그린다면 GPU 인스턴싱을 켠다.")]
        [SerializeField] private Material _baseMaterial;

        private readonly Dictionary<ushort, View> _views = new Dictionary<ushort, View>();
        private readonly List<ushort> _scratch = new List<ushort>();
        private readonly Material[] _materials = new Material[ModeCount];
        private readonly Stopwatch _watch = new Stopwatch();
        private ClientCreatureStore _store;
        private int _stamp;
        private double _updateMillisecondsSmoothed;

        /// <summary>표시할 생물 사본을 정한다.</summary>
        public void Initialize(ClientCreatureStore store)
        {
            _store = store;
        }

        /// <summary>지금 표시 중인 생물 수(시험·진단).</summary>
        public int ViewCount => _views.Count;

        /// <summary>표시 갱신(Update) 한 번의 평균 비용(ms). 관찰값.</summary>
        public double UpdateMilliseconds => _updateMillisecondsSmoothed;

        private void Awake()
        {
            // 모드별 색: 0 배회(초록), 1 추적(주황), 2 공격(빨강), 3 사망(회색)
            Color[] colors =
            {
                new Color(0.35f, 0.75f, 0.35f),
                new Color(1.00f, 0.60f, 0.15f),
                new Color(0.90f, 0.15f, 0.15f),
                new Color(0.35f, 0.35f, 0.35f),
            };
            for (int i = 0; i < ModeCount; i++)
            {
                Material m = _baseMaterial != null ? new Material(_baseMaterial) : new Material(Shader.Find("HDRP/Lit"));
                m.name = "CreatureMode" + i;
                // HDRP는 _BaseColor, 내장 셰이더는 _Color다. 있는 쪽만 바꾼다.
                if (m.HasProperty(BaseColorId))
                {
                    m.SetColor(BaseColorId, colors[i]);
                }
                if (m.HasProperty(ColorId))
                {
                    m.SetColor(ColorId, colors[i]);
                }
                _materials[i] = m;
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < ModeCount; i++)
            {
                if (_materials[i] != null)
                {
                    Destroy(_materials[i]);
                }
            }
        }

        private void Update()
        {
            if (_store == null)
            {
                return;
            }
            _watch.Restart();
            _stamp++;
            double now = Time.realtimeSinceStartupAsDouble;

            foreach (KeyValuePair<ushort, ClientCreature> pair in _store.All)
            {
                ClientCreature creature = pair.Value;
                if (!_views.TryGetValue(pair.Key, out View view))
                {
                    view = CreateView(pair.Key);
                    _views[pair.Key] = view;
                }
                view.Stamp = _stamp;
                creature.Sample(now, out Vec3f position, out float yaw);
                bool dead = creature.Anim == (byte)CreatureMode.Dead;
                float scaleY = dead ? DeadScaleY : StandingScaleY;
                view.Transform.position = new Vector3(position.X, position.Y + scaleY, position.Z);
                view.Transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                if (view.Mode != creature.Anim)
                {
                    ApplyMode(view, creature.Anim, scaleY);
                }
            }

            // 사본에서 사라진 생물(관심 범위 밖, 사망 제거)의 도형을 지운다.
            if (_views.Count > _store.Count)
            {
                _scratch.Clear();
                foreach (KeyValuePair<ushort, View> pair in _views)
                {
                    if (pair.Value.Stamp != _stamp)
                    {
                        _scratch.Add(pair.Key);
                    }
                }
                for (int i = 0; i < _scratch.Count; i++)
                {
                    Destroy(_views[_scratch[i]].Transform.gameObject);
                    _views.Remove(_scratch[i]);
                }
            }

            _watch.Stop();
            double ms = _watch.Elapsed.TotalMilliseconds;
            _updateMillisecondsSmoothed += (ms - _updateMillisecondsSmoothed) * 0.05;
        }

        private void ApplyMode(View view, byte mode, float scaleY)
        {
            view.Mode = mode;
            int index = mode < ModeCount ? mode : 0;
            view.Renderer.sharedMaterial = _materials[index];
            view.Transform.localScale = new Vector3(WidthScale, scaleY, WidthScale);
        }

        private View CreateView(ushort id)
        {
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Creature-" + id;
            Collider collider = capsule.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider); // 표시 전용: 물리 충돌 없음
            }
            capsule.transform.SetParent(transform, false);
            return new View { Transform = capsule.transform, Renderer = capsule.GetComponent<Renderer>() };
        }
    }
}

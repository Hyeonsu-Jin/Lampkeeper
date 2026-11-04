// =============================================================
// 파일: WorldAndNetworkBuilders.cs
// 역할: M1-07·M1-08에 필요한 애셋을 코드로 만든다(에디터 전용): 플레이어 아바타 NetworkObject 프리팹(PlayerAvatar),
//       World 씬(바닥, 햇빛, 1인칭 조작 장치, 원격 플레이어 표시, 기준 상자들).
// 레이어: infrastructure (Game.Editor, 편집기 전용)
// 관련: DES-084, DES-086, DES-100, FR-023~025 / SDD 3.2.3, 3.8.2, 3.9.8
//
// Game.Editor는 FishNet·Net 어댑터를 참조하지 않으므로(SDD 3.4.2 표) 그 형식은 이름으로 찾아 붙인다(리플렉션).
// 이 스크립트가 만든 프리팹을 FishNet이 DefaultPrefabObjects에 등록한다(FishNet 자동 생성기).
// =============================================================
using System;
using Game.Adapters.Presentation.Player;
using Game.Adapters.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>이름으로 형식을 찾아 컴포넌트를 붙이는 도우미(에디터 전용).</summary>
    internal static class ReflectionComponents
    {
        public static Component Add(GameObject target, string typeName, string assemblyName)
        {
            Type type = Type.GetType(typeName + ", " + assemblyName);
            if (type == null)
            {
                throw new InvalidOperationException(typeName + " 형식을 찾지 못했습니다(" + assemblyName + " 확인).");
            }
            return target.AddComponent(type);
        }
    }

    /// <summary>플레이어 아바타 프리팹 생성기.</summary>
    public static class AvatarPrefabBuilder
    {
        public const string PrefabPath = "Assets/_Game/Prefabs/PlayerAvatar.prefab";

        /// <summary>프리팹이 없으면 만들고, 있으면 NetworkObject·PlayerAvatar 가 붙어 있는지 검사만 한다(덮어쓰지 않음).</summary>
        [MenuItem("Game/Setup/Ensure Avatar Prefab")]
        public static void Ensure()
        {
            if (!System.IO.File.Exists(PrefabPath))
            {
                CreateNew();
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var problems = new System.Collections.Generic.List<string>();
            if (prefab == null)
            {
                problems.Add("프리팹을 불러오지 못했습니다.");
            }
            else
            {
                var names = new System.Collections.Generic.HashSet<string>();
                foreach (Component component in prefab.GetComponents<Component>())
                {
                    if (component != null) { names.Add(component.GetType().FullName); }
                }
                if (!names.Contains("FishNet.Object.NetworkObject")) { problems.Add("NetworkObject 컴포넌트가 없습니다."); }
                if (!names.Contains("Game.Adapters.Net.Avatar.PlayerAvatar")) { problems.Add("PlayerAvatar 컴포넌트가 없습니다."); }
            }
            SetupChecks.ThrowIfAny("PlayerAvatar 프리팹", problems);
        }

        /// <summary>프리팹을 처음 한 번 만든다.</summary>
        private static void CreateNew()
        {
            DataAssetBuilder.EnsureFolder("Assets/_Game/Prefabs");
            var go = new GameObject("PlayerAvatar");
            try
            {
                // NetworkObject + PlayerAvatar(ServerRpc/ObserversRpc). 표시는 Presentation이 월드 사본으로 그린다(프리팹에 모양 없음).
                ReflectionComponents.Add(go, "FishNet.Object.NetworkObject", "FishNet.Runtime");
                ReflectionComponents.Add(go, "Game.Adapters.Net.Avatar.PlayerAvatar", "Game.Adapters.Net");
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] 아바타 프리팹을 만들었습니다: " + PrefabPath);
        }
    }

    /// <summary>World 씬 생성기(M1 개발용: 평평한 바닥 + 조작 장치).</summary>
    public static class WorldSceneBuilder
    {
        public const string WorldScenePath = "Assets/_Game/Scenes/World.unity";
        private const string HdrpLitShaderName = "HDRP/Lit";
        private const string GroundMaterialPath = "Assets/_Game/Materials/WorldGround.mat";
        private const string PropMaterialPath = "Assets/_Game/Materials/WorldProp.mat";
        private const string AvatarMaterialPath = "Assets/_Game/Materials/RemoteAvatar.mat";

        private static readonly Color GroundColor = new Color(0.28f, 0.32f, 0.27f);
        private static readonly Color PropColor = new Color(0.6f, 0.55f, 0.45f);
        private static readonly Color AvatarColor = new Color(0.35f, 0.6f, 0.85f);

        private const float EyeHeightMeters = 1.6f;
        private const float CapsuleHeightMeters = 1.8f;
        private const float CapsuleRadiusMeters = 0.35f;
        private const float GroundSizeMeters = 400f;

        /// <summary>World 씬이 없으면 만들고, 있으면 PlayerRig·RemoteAvatarViews 와 빌드 설정 등록을 검사만 한다(덮어쓰지 않음).</summary>
        [MenuItem("Game/Setup/Ensure World Scene")]
        public static void Ensure()
        {
            if (!System.IO.File.Exists(WorldScenePath))
            {
                CreateNew();
                return;
            }
            SetupChecks.InspectScene(WorldScenePath, "World 씬", (scene, problems) =>
            {
                GameObject rig = SetupChecks.FindRoot(scene, "PlayerRig");
                if (rig == null || rig.GetComponent<FirstPersonController>() == null)
                {
                    problems.Add("루트 오브젝트 'PlayerRig'(FirstPersonController)가 없습니다.");
                }
                GameObject views = SetupChecks.FindRoot(scene, "RemoteAvatarViews");
                if (views == null || views.GetComponent<RemoteAvatarViews>() == null)
                {
                    problems.Add("루트 오브젝트 'RemoteAvatarViews'(RemoteAvatarViews)가 없습니다.");
                }
                bool registered = false;
                foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
                {
                    if (entry.path == WorldScenePath) { registered = entry.enabled; }
                }
                if (!registered)
                {
                    problems.Add("빌드 설정(Scenes In Build)에 World 씬이 등록되어 있지 않습니다.");
                }
            });
        }

        /// <summary>World 씬을 처음 한 번 만든다.</summary>
        private static void CreateNew()
        {
            DataAssetBuilder.EnsureFolder("Assets/_Game/Scenes");
            DataAssetBuilder.EnsureFolder("Assets/_Game/Materials");
            Material ground = LoadOrCreateMaterial(GroundMaterialPath, GroundColor);
            Material prop = LoadOrCreateMaterial(PropMaterialPath, PropColor);
            Material avatar = LoadOrCreateMaterial(AvatarMaterialPath, AvatarColor);

            // 열려 있는 씬(Boot 등)을 건드리지 않도록 새 씬을 추가로 열어 작업한다.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var sun = new GameObject("Sun");
                var light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 3.14f;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Ground";
                floor.transform.position = new Vector3(0f, -0.5f, 0f);
                floor.transform.localScale = new Vector3(GroundSizeMeters, 1f, GroundSizeMeters);
                floor.GetComponent<Renderer>().sharedMaterial = ground;

                // 이동 확인용 기준 상자들(거리 감각).
                for (int i = 1; i <= 8; i++)
                {
                    GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    box.name = "Marker" + i;
                    box.transform.position = new Vector3(i * 6f, 1f, 10f);
                    box.transform.localScale = new Vector3(2f, 2f, 2f);
                    box.GetComponent<Renderer>().sharedMaterial = prop;
                }

                // 1인칭 조작 장치: CharacterController + 눈 높이 카메라.
                var rig = new GameObject("PlayerRig");
                rig.transform.position = new Vector3(0f, 0.1f, 0f);
                var controller = rig.AddComponent<CharacterController>();
                controller.height = CapsuleHeightMeters;
                controller.radius = CapsuleRadiusMeters;
                controller.center = new Vector3(0f, CapsuleHeightMeters * 0.5f, 0f);

                var cameraObject = new GameObject("Camera") { tag = "MainCamera" };
                cameraObject.transform.SetParent(rig.transform, false);
                cameraObject.transform.localPosition = new Vector3(0f, EyeHeightMeters, 0f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 600f;
                cameraObject.AddComponent<AudioListener>();

                var fpc = rig.AddComponent<FirstPersonController>();
                SetReference(fpc, "_camera", camera);

                var viewsObject = new GameObject("RemoteAvatarViews");
                var views = viewsObject.AddComponent<RemoteAvatarViews>();
                SetReference(views, "_material", avatar);

                EditorSceneManager.SaveScene(scene, WorldScenePath);
                RegisterInBuildSettings(WorldScenePath);
                Debug.Log("[Setup] World 씬을 만들었습니다: " + WorldScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Material LoadOrCreateMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }
            Shader shader = Shader.Find(HdrpLitShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException("HDRP/Lit 셰이더를 찾지 못했습니다(HDRP 패키지 확인).");
            }
            var material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetReference(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                throw new InvalidOperationException(target.GetType().Name + "에 직렬화 필드 '" + fieldName + "'가 없습니다.");
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>씬을 빌드 설정에 넣는다(Boot 다음). 이미 있으면 활성화만.</summary>
        private static void RegisterInBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == scenePath || !System.IO.File.Exists(s.path));
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

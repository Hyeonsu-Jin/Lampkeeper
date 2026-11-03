// =============================================================
// 파일: BenchSceneBuilder.cs
// 역할: Bench 장면(측정 장면)과 시험용 몹 재질을 코드로 만든다. 장면에는 카메라, 해, 바닥, BenchRunner가 있다.
//       Boot 씬을 건드리지 않도록 열려 있는 씬에 더하는(Additive) 방식으로 만들고 저장한 뒤 닫는다.
// 레이어: infrastructure (Game.Editor, 편집기 전용)
// 관련: FR-163, NFR-021, DES-113 / SDD 3.2.3 Unity 씬 구성(Bench), 3.8.2 Scenes/
//
// Bench 장면은 빌드 설정에 넣지 않는다. 개발 빌드를 만들 때 BuildCommands가 자동으로 포함한다(출시 빌드 제외).
// =============================================================
using Game.Bootstrap.DevTools;
using Game.Infra.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>Bench 장면 생성기.</summary>
    public static class BenchSceneBuilder
    {
        /// <summary>Bench 장면 경로.</summary>
        public const string BenchScenePath = "Assets/_Game/Scenes/Bench.unity";

        /// <summary>시험용 몹 재질 경로.</summary>
        public const string AgentMaterialPath = "Assets/_Game/Materials/BenchAgent.mat";

        private const string HdrpLitShaderName = "HDRP/Lit";
        private static readonly Color AgentColor = new Color(0.85f, 0.35f, 0.25f);
        private static readonly Color GroundColor = new Color(0.25f, 0.28f, 0.25f);

        /// <summary>Bench 장면을 (다시) 만든다.</summary>
        [MenuItem("Game/Setup/Create Bench Scene")]
        public static void CreateBenchScene()
        {
            DataAssetBuilder.EnsureFolder("Assets/_Game/Scenes");
            DataAssetBuilder.EnsureFolder("Assets/_Game/Materials");

            var dataConfig = AssetDatabase.LoadAssetAtPath<GameDataConfig>(DataAssetBuilder.RootPath);
            if (dataConfig == null)
            {
                throw new System.InvalidOperationException("GameDataConfig가 없습니다. Game/Data/Create Default Config Assets를 먼저 실행하세요.");
            }
            Material agentMaterial = LoadOrCreateMaterial(AgentMaterialPath, AgentColor);
            Material groundMaterial = LoadOrCreateMaterial("Assets/_Game/Materials/BenchGround.mat", GroundColor);

            // 열려 있는 씬(Boot 등)을 건드리지 않도록 새 씬을 추가로 열어 작업한다.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                var camera = cameraObject.AddComponent<Camera>();
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 400f;
                cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 22f, -48f), Quaternion.Euler(25f, 0f, 0f));

                var sunObject = new GameObject("Sun");
                var sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 3.14f;
                sunObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Ground";
                Object.DestroyImmediate(ground.GetComponent<Collider>());
                ground.transform.position = new Vector3(0f, -0.5f, 0f);
                ground.transform.localScale = new Vector3(100f, 1f, 100f);
                ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

                var runnerObject = new GameObject("BenchRunner");
                var runner = runnerObject.AddComponent<BenchRunner>();
                SetReference(runner, "_dataConfig", dataConfig);
                SetReference(runner, "_agentMaterial", agentMaterial);

                EditorSceneManager.SaveScene(scene, BenchScenePath);
                Debug.Log("[Setup] Bench 장면을 만들었습니다: " + BenchScenePath + " (개발 빌드에만 포함됩니다)");
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
                throw new System.InvalidOperationException("HDRP/Lit 셰이더를 찾지 못했습니다(HDRP 패키지 확인).");
            }
            var material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetReference(Object target, string fieldName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                throw new System.InvalidOperationException(target.GetType().Name + "에 직렬화 필드 '" + fieldName + "'가 없습니다.");
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

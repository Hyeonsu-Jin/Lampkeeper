// =============================================================
// 파일: BootSceneBuilder.cs
// 역할: Boot 씬(진입 씬)과 UI Toolkit PanelSettings 애셋을 코드로 만들고 빌드 설정에 넣는다.
//       GameBootstrap, SteamRequiredScreen(SCR-001)의 직렬화 필드도 여기서 연결한다.
// 레이어: infrastructure (Game.Editor, 편집기 전용)
// 관련: DES-100(CompositionRoot·SceneFlow), NFR-006, SCR-001 / SDD 3.2.3 Unity 씬 구성, 3.8.2 Scenes/
// =============================================================
using Game.Adapters.Presentation.UI.Screens;
using Game.Bootstrap;
using Game.Infra.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Editor
{
    /// <summary>Boot 씬 생성기.</summary>
    public static class BootSceneBuilder
    {
        /// <summary>Boot 씬 경로.</summary>
        public const string BootScenePath = "Assets/_Game/Scenes/Boot.unity";
        /// <summary>PanelSettings 경로.</summary>
        public const string PanelSettingsPath = "Assets/_Game/UI/PanelSettings.asset";

        private const string BaseUssPath = "Assets/_Game/UI/Common/Base.uss";
        private const string SteamRequiredUxmlPath = "Assets/_Game/UI/Screens/SteamRequired.uxml";
        private const string KoreanStringsPath = "Assets/_Game/Localization/strings.ko.txt";
        private const string EnglishStringsPath = "Assets/_Game/Localization/strings.en.txt";
        private const string DefaultThemeName = "UnityDefaultRuntimeTheme";

        // 기준 해상도. 글자·여백은 이 해상도 기준으로 설계되고 화면 크기에 맞춰 배율이 바뀐다.
        private static readonly Vector2Int ReferenceResolution = new Vector2Int(1920, 1080);

        /// <summary>Boot 씬을 (다시) 만든다.</summary>
        [MenuItem("Game/Setup/Create Boot Scene")]
        public static void CreateBootScene()
        {
            DataAssetBuilder.EnsureFolder("Assets/_Game/Scenes");

            // 주의: NewScene(Single)이 쓰이지 않는 애셋을 메모리에서 내리므로, 애셋은 반드시 씬을 만든 뒤에 불러온다.
            // (먼저 불러 두면 씬 저장 때 참조가 비어 버린다.)
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            var dataConfig = AssetDatabase.LoadAssetAtPath<GameDataConfig>(DataAssetBuilder.RootPath);
            var baseUss = AssetDatabase.LoadAssetAtPath<StyleSheet>(BaseUssPath);
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SteamRequiredUxmlPath);
            var ko = AssetDatabase.LoadAssetAtPath<TextAsset>(KoreanStringsPath);
            var en = AssetDatabase.LoadAssetAtPath<TextAsset>(EnglishStringsPath);
            if (dataConfig == null || baseUss == null || uxml == null || ko == null || en == null)
            {
                throw new System.InvalidOperationException("Boot 씬에 필요한 애셋이 없습니다. 설정 애셋(Create Default Config Assets)과 UI/Localization 파일을 확인하세요.");
            }

            // SCR-001: 평소에는 꺼 두고 부트가 필요할 때 켠다.
            var screenObject = new GameObject("SteamRequiredScreen");
            var document = screenObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = uxml;
            var screen = screenObject.AddComponent<SteamRequiredScreen>();
            SetReference(screen, "_baseStyle", baseUss);
            screenObject.SetActive(false);

            var bootObject = new GameObject("Bootstrap");
            var bootstrap = bootObject.AddComponent<GameBootstrap>();
            SetReference(bootstrap, "_dataConfig", dataConfig);
            SetReference(bootstrap, "_koreanStrings", ko);
            SetReference(bootstrap, "_englishStrings", en);
            SetReference(bootstrap, "_steamRequiredScreen", screen);

            EditorSceneManager.SaveScene(scene, BootScenePath);
            RegisterInBuildSettings(BootScenePath);
            Debug.Log("[Setup] Boot 씬을 만들었습니다: " + BootScenePath);
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel == null)
            {
                DataAssetBuilder.EnsureFolder("Assets/_Game/UI");
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            }

            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = ReferenceResolution;
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;

            // 기본 런타임 테마가 프로젝트에 보이면 연결한다. Unity 6.3에서는 애셋으로 보이지 않을 수 있으며,
            // 그 경우 비워 둬도 엔진 기본 테마가 쓰이고 화면 스타일은 Base.uss가 전부 정한다.
            string[] themeGuids = AssetDatabase.FindAssets(DefaultThemeName + " t:ThemeStyleSheet");
            if (themeGuids.Length > 0)
            {
                panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(themeGuids[0]));
            }
            else
            {
                Debug.Log("[Setup] 기본 런타임 테마 애셋이 보이지 않아 PanelSettings 테마를 비워 둡니다(엔진 기본값 사용).");
            }
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            return panel;
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

        /// <summary>씬을 빌드 설정 맨 앞에 넣는다(이미 있으면 활성화만).</summary>
        private static void RegisterInBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            // 같은 씬과, 파일이 사라진 옛 항목(템플릿 샘플 씬 등)은 빼고 다시 넣는다.
            scenes.RemoveAll(s => s.path == scenePath || !System.IO.File.Exists(s.path));
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

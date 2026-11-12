// =============================================================
// 파일: BootSceneBuilder.cs
// 역할: Boot 씬(진입 씬)을 「없을 때만」 처음 한 번 만들고, 있으면 필요한 루트 오브젝트·참조가 있는지 검사만 한다.
//       UI 는 UI/Prefabs 의 프리팹 인스턴스로만 놓는다. 이미 있는 씬을 통째로 다시 만들어 덮어쓰지 않는다.
// 레이어: infrastructure (Game.Editor, 편집기 전용)
// 관련: DES-100(CompositionRoot·SceneFlow), NFR-006, SCR-001 / SDD 3.2.3 Unity 씬 구성, 3.8.2 생성 파이프라인 8, D-27
//
// 빠진 것이 있으면 Console 오류와 예외로 알리고(Run All 실패) 아무것도 바꾸지 않는다. 빠진 것은 에이전트가 Unity MCP 도구로,
// 또는 사람이 하이어라키에서 채운다.
// =============================================================
using System;
using System.Collections.Generic;
using Game.Adapters.Presentation.UI.Common;
using Game.Adapters.Presentation.UI.Hud;
using Game.Adapters.Presentation.UI.Screens;
using Game.Bootstrap;
using Game.Infra.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>생성기 정책(없을 때만 만들기 + 있으면 검사만)에 쓰는 공통 도우미.</summary>
    internal static class SetupChecks
    {
        /// <summary>
        /// 씬 파일을 (이미 열려 있으면 그대로) 읽기 전용으로 살펴보고 문제 목록을 만든다. 문제가 있으면 예외를 던진다.
        /// </summary>
        public static void InspectScene(string scenePath, string label, Action<Scene, List<string>> check)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                openedHere = true;
            }
            var problems = new List<string>();
            try
            {
                check(scene, problems);
            }
            finally
            {
                if (openedHere)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            ThrowIfAny(label, problems);
        }

        /// <summary>문제가 있으면 Console 오류로 하나씩 알리고 예외를 던진다.</summary>
        public static void ThrowIfAny(string label, List<string> problems)
        {
            if (problems.Count == 0)
            {
                Debug.Log("[Setup] " + label + " 검사 통과(덮어쓰지 않음).");
                return;
            }
            foreach (string problem in problems)
            {
                Debug.LogError("[Setup] " + label + ": " + problem);
            }
            throw new InvalidOperationException(label + " 검사 실패: " + problems.Count + "건. 하이어라키에서 채우거나 Unity MCP 도구로 고치세요.");
        }

        /// <summary>씬 안에서 이름이 같은 루트 오브젝트를 찾는다.</summary>
        public static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }
            return null;
        }
    }

    /// <summary>Boot 씬 생성·검사.</summary>
    public static class BootSceneBuilder
    {
        /// <summary>Boot 씬 경로.</summary>
        public const string BootScenePath = "Assets/_Game/Scenes/Boot.unity";

        public const string UiRootPrefabPath = "Assets/_Game/UI/Prefabs/UiRoot.prefab";
        public const string SteamRequiredPrefabPath = "Assets/_Game/UI/Prefabs/Screens/SteamRequired.prefab";
        public const string DevLobbyPrefabPath = "Assets/_Game/UI/Prefabs/Screens/DevLobby.prefab";
        public const string GameMenuPrefabPath = "Assets/_Game/UI/Prefabs/Screens/GameMenu.prefab";
        public const string GameHudPrefabPath = "Assets/_Game/UI/Prefabs/Hud/GameHud.prefab";

        // 씬의 루트 오브젝트 이름
        public const string BootstrapName = "Bootstrap";
        public const string NetworkManagerName = "NetworkManager";
        public const string MenuCameraName = "MenuCamera";
        public const string UiRootName = "UiRoot";
        public const string SteamRequiredName = "SteamRequiredScreen";
        public const string DevLobbyName = "DevLobbyScreen";
        public const string GameHudName = "GameHud";
        public const string GameMenuName = "GameMenu";

        private const string KoreanStringsPath = "Assets/_Game/Localization/strings.ko.txt";
        private const string EnglishStringsPath = "Assets/_Game/Localization/strings.en.txt";

        /// <summary>GameBootstrap 이 값을 가져야 하는 직렬화 참조 필드.</summary>
        public static readonly string[] RequiredBootstrapFields =
        {
            "_dataConfig", "_koreanStrings", "_englishStrings", "_uiRoot", "_steamRequiredScreen", "_lobbyScreen", "_hud", "_gameMenu",
            "_networkManager", "_avatarPrefab", "_inputActions", "_menuCamera",
        };

        /// <summary>Boot 씬이 없으면 만들고, 있으면 검사만 한다. 빌드 설정의 첫 씬도 확인한다.</summary>
        [MenuItem("Game/Setup/Ensure Boot Scene")]
        public static void Ensure()
        {
            if (!System.IO.File.Exists(BootScenePath))
            {
                CreateNew(BootScenePath);
            }
            else
            {
                SetupChecks.InspectScene(BootScenePath, "Boot 씬", CheckBootScene);
            }
            EnsureFirstInBuildSettings(BootScenePath);
        }

        /// <summary>Boot 씬이 갖춰졌는지 검사한다(문제 목록에 더한다). 시험도 이 검사를 쓴다.</summary>
        public static void CheckBootScene(Scene scene, List<string> problems)
        {
            GameObject bootstrapObject = SetupChecks.FindRoot(scene, BootstrapName);
            GameBootstrap bootstrap = bootstrapObject != null ? bootstrapObject.GetComponent<GameBootstrap>() : null;
            if (bootstrap == null)
            {
                problems.Add("루트 오브젝트 '" + BootstrapName + "'(GameBootstrap)가 없습니다.");
            }
            else
            {
                var serialized = new SerializedObject(bootstrap);
                foreach (string field in RequiredBootstrapFields)
                {
                    SerializedProperty property = serialized.FindProperty(field);
                    if (property == null)
                    {
                        problems.Add("GameBootstrap 에 직렬화 필드 '" + field + "'가 없습니다.");
                    }
                    else if (property.objectReferenceValue == null)
                    {
                        problems.Add("GameBootstrap." + field + " 참조가 비어 있습니다.");
                    }
                }
            }

            GameObject network = SetupChecks.FindRoot(scene, NetworkManagerName);
            if (network == null)
            {
                problems.Add("루트 오브젝트 '" + NetworkManagerName + "'가 없습니다.");
            }
            else if (network.activeSelf)
            {
                problems.Add("NetworkManager 는 꺼진 상태여야 합니다(전송을 정한 뒤 GameBootstrap 이 켭니다).");
            }

            foreach (string name in new[] { MenuCameraName, UiRootName, SteamRequiredName, DevLobbyName, GameHudName, GameMenuName })
            {
                if (SetupChecks.FindRoot(scene, name) == null)
                {
                    problems.Add("루트 오브젝트 '" + name + "'가 없습니다.");
                }
            }

            int eventSystems = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                eventSystems += root.GetComponentsInChildren<EventSystem>(true).Length;
            }
            if (eventSystems != 1)
            {
                problems.Add("EventSystem 이 정확히 하나여야 합니다(현재 " + eventSystems + "개).");
            }
        }

        /// <summary>
        /// Boot 씬을 처음 한 번 만든다. UI 는 프리팹 인스턴스로만 놓으므로 UI 프리팹이 먼저 있어야 한다.
        /// </summary>
        /// <param name="path">저장할 씬 경로(시험에서는 임시 경로)</param>
        /// <exception cref="InvalidOperationException">필요한 프리팹·애셋이 없을 때</exception>
        public static void CreateNew(string path)
        {
            DataAssetBuilder.EnsureFolder("Assets/_Game/Scenes");

            // 주의: NewScene(Single)이 쓰이지 않는 애셋을 메모리에서 내리므로, 애셋은 반드시 씬을 만든 뒤에 불러온다.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var dataConfig = AssetDatabase.LoadAssetAtPath<GameDataConfig>(DataAssetBuilder.RootPath);
            var ko = AssetDatabase.LoadAssetAtPath<TextAsset>(KoreanStringsPath);
            var en = AssetDatabase.LoadAssetAtPath<TextAsset>(EnglishStringsPath);
            var inputAsset = AssetDatabase.LoadAssetAtPath<Object>(InputActionsBuilder.AssetPath);
            var avatarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPrefabBuilder.PrefabPath);
            if (dataConfig == null || ko == null || en == null || inputAsset == null || avatarPrefab == null)
            {
                throw new InvalidOperationException("Boot 씬에 필요한 애셋이 없습니다. 설정 애셋(Create Default Config Assets)과 Localization 파일, Input Actions, 아바타 프리팹을 확인하세요.");
            }

            GameObject uiRoot = InstantiateUiPrefab(UiRootPrefabPath, UiRootName);
            GameObject steam = InstantiateUiPrefab(SteamRequiredPrefabPath, SteamRequiredName);
            GameObject lobby = InstantiateUiPrefab(DevLobbyPrefabPath, DevLobbyName);
            GameObject hud = InstantiateUiPrefab(GameHudPrefabPath, GameHudName);
            GameObject menu = InstantiateUiPrefab(GameMenuPrefabPath, GameMenuName);

            // 메뉴 화면 뒤를 그리는 카메라(게임 중에는 꺼진다).
            var menuCameraObject = new GameObject(MenuCameraName);
            var menuCamera = menuCameraObject.AddComponent<Camera>();
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = Color.black;
            menuCamera.cullingMask = 0;
            menuCamera.depth = -10f;

            // FishNet NetworkManager. Game.Editor 는 FishNet 을 참조하지 않으므로 형식을 이름으로 찾는다.
            var networkObject = new GameObject(NetworkManagerName);
            Component networkManager = ReflectionComponents.Add(networkObject, "FishNet.Managing.NetworkManager", "FishNet.Runtime");
            networkObject.SetActive(false); // 전송을 정한 뒤 GameBootstrap 이 켠다(FishNet 은 깨어날 때 전송을 초기화)

            var bootObject = new GameObject(BootstrapName);
            var bootstrap = bootObject.AddComponent<GameBootstrap>();
            SetReference(bootstrap, "_dataConfig", dataConfig);
            SetReference(bootstrap, "_koreanStrings", ko);
            SetReference(bootstrap, "_englishStrings", en);
            SetReference(bootstrap, "_uiRoot", uiRoot.GetComponent<UiRoot>());
            SetReference(bootstrap, "_steamRequiredScreen", steam.GetComponent<SteamRequiredScreen>());
            SetReference(bootstrap, "_lobbyScreen", lobby.GetComponent<DevLobbyScreen>());
            SetReference(bootstrap, "_hud", hud.GetComponent<GameHud>());
            SetReference(bootstrap, "_gameMenu", menu.GetComponent<GameMenu>());
            SetReference(bootstrap, "_menuCamera", menuCamera);
            SetReference(bootstrap, "_networkManager", networkManager);
            SetReference(bootstrap, "_avatarPrefab", avatarPrefab.GetComponent(Type.GetType("FishNet.Object.NetworkObject, FishNet.Runtime")));
            SetReference(bootstrap, "_inputActions", inputAsset);

            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[Setup] Boot 씬을 처음 만들었습니다: " + path + " (이후에는 씬 파일이 원본입니다).");
        }

        private static GameObject InstantiateUiPrefab(string prefabPath, string instanceName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException("UI 프리팹이 없습니다: " + prefabPath + " (UI 프리팹은 생성기가 만들지 않습니다. 저장소에서 받거나 MCP 도구로 만드세요).");
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = instanceName;
            return instance;
        }

        private static void SetReference(Object target, string fieldName, Object value)
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

        /// <summary>씬을 빌드 설정 맨 앞에 둔다(이미 맨 앞이면 아무것도 바꾸지 않는다).</summary>
        private static void EnsureFirstInBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            if (current.Length > 0 && current[0].path == scenePath && current[0].enabled)
            {
                return;
            }
            var scenes = new List<EditorBuildSettingsScene>(current);
            // 같은 씬과, 파일이 사라진 옛 항목(템플릿 샘플 씬 등)은 빼고 다시 넣는다.
            scenes.RemoveAll(s => s.path == scenePath || !System.IO.File.Exists(s.path));
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

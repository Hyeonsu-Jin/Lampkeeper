// =============================================================
// 파일: UiTestSupport.cs
// 역할: uGUI 화면 시험이 함께 쓰는 도우미(프리팹 불러오기, 가짜 글자 공급자, 글자 표 읽기).
// 레이어: test (EditMode, Game.Tests.Infrastructure)
// 관련: NFR-034, NFR-044 / SDD 3.4.3 UI 프리팹·테마 규칙 검사
// =============================================================
using System.Collections.Generic;
using Game.Adapters.Presentation.UI.Common;
using Game.Application.Localization;
using Game.Application.Ports;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Infrastructure
{
    /// <summary>키를 그대로 "T(키)" 로 돌려주는 가짜 글자 공급자.</summary>
    public sealed class EchoText : ILocalizedText
    {
        public string Get(string key) => "T(" + key + ")";
        public string Get(string key, params object[] args) => Get(key);
    }

    /// <summary>UI 시험 도우미. 상태가 없는 정적 클래스.</summary>
    public static class UiTestSupport
    {
        public const string PrefabRoot = "Assets/_Game/UI/Prefabs";
        public const string SteamRequiredPrefab = PrefabRoot + "/Screens/SteamRequired.prefab";
        public const string DevLobbyPrefab = PrefabRoot + "/Screens/DevLobby.prefab";
        public const string GameMenuPrefab = PrefabRoot + "/Screens/GameMenu.prefab";
        public const string GameHudPrefab = PrefabRoot + "/Hud/GameHud.prefab";
        public const string UiRootPrefab = PrefabRoot + "/UiRoot.prefab";
        public const string KoPath = "Assets/_Game/Localization/strings.ko.txt";
        public const string EnPath = "Assets/_Game/Localization/strings.en.txt";

        /// <summary>프리팹 인스턴스를 현재 씬에 만든다(시험이 끝나면 DestroyImmediate 로 지운다).</summary>
        public static T Instantiate<T>(string prefabPath) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, "프리팹이 없습니다: " + prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var component = instance.GetComponent<T>();
            Assert.IsNotNull(component, prefabPath + " 루트에 " + typeof(T).Name + " 이(가) 없습니다.");
            return component;
        }

        /// <summary>시험용 글자 공급자를 UI 에 알린다. 끝나면 Reset 을 부른다.</summary>
        public static void UseEchoText()
        {
            UiContext.SetText(new EchoText());
        }

        /// <summary>UI 공유 상태를 시험 전 상태로 되돌린다.</summary>
        public static void Reset()
        {
            UiContext.SetText(null);
            UiContext.SetTextScale(UiTextScale.Normal);
        }

        /// <summary>글자 표(ko/en)를 읽는다.</summary>
        public static TextTable LoadTable(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            Assert.IsNotNull(asset, path);
            var errors = new List<string>();
            TextTable table = TextTable.Parse(asset.text, errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            return table;
        }

        /// <summary>테마 애셋.</summary>
        public static UiTheme LoadTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(Game.Editor.UiThemeBuilder.ThemePath);
            Assert.IsNotNull(theme, "UiTheme.asset 이 없습니다.");
            return theme;
        }
    }
}

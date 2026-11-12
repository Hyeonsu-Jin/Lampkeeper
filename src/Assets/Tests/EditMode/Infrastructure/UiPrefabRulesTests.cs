// =============================================================
// 파일: UiPrefabRulesTests.cs
// 역할: UI 프리팹·테마 규칙 검사(v0.13, USS 규칙 검사를 대신함). UI/Prefabs 아래 모든 프리팹과 Boot.unity 의 UI 인스턴스가
//       SDD 3.7.10 의 규칙(테마 토큰, 리치 텍스트 끔, 텍스트 키, Raycast Target, 선택 요소, 화면 루트, 씬 구성)을 지키는지 본다.
//       그리고 테마 애셋(토큰 20+6+3, 단계별 글자 크기, 대비)을 검사한다.
// 레이어: test (EditMode, Game.Tests.Infrastructure)
// 관련: FR-129, FR-130, NFR-002, NFR-004, NFR-034, NFR-044, NFR-050, NFR-056, NFR-057, DES-091, DES-095, DES-122 / SDD 3.4.3, 3.7.10
// =============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Adapters.Presentation.UI.Common;
using Game.Application.Localization;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Tests.Infrastructure
{
    public class UiPrefabRulesTests
    {
        private static readonly string[] ScreenPrefabFolders = { "/Screens/", "/Hud/" };

        private static IEnumerable<string> PrefabPaths()
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { UiTestSupport.PrefabRoot }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal);
        }

        /// <summary>프리팹을 읽어 검사하고 닫는다.</summary>
        private static void ForEachPrefab(Action<string, GameObject> check)
        {
            var any = false;
            foreach (string path in PrefabPaths())
            {
                any = true;
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    check(path, root);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Assert.IsTrue(any, "UI 프리팹이 하나도 없습니다.");
        }

        private static bool NearlyEqual(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f && Mathf.Abs(a.a - b.a) < 0.004f;
        }

        [Test]
        public void ExpectedPrefabs_Exist()
        {
            foreach (string path in new[]
                     {
                         UiTestSupport.UiRootPrefab, UiTestSupport.SteamRequiredPrefab, UiTestSupport.DevLobbyPrefab, UiTestSupport.GameMenuPrefab, UiTestSupport.GameHudPrefab,
                         UiTestSupport.PrefabRoot + "/Common/Button_Primary.prefab", UiTestSupport.PrefabRoot + "/Common/Button_Secondary.prefab",
                         UiTestSupport.PrefabRoot + "/Common/Button_Danger.prefab", UiTestSupport.PrefabRoot + "/Common/ListRow.prefab",
                         UiTestSupport.PrefabRoot + "/Common/Panel.prefab", UiTestSupport.PrefabRoot + "/Common/Badge_Running.prefab",
                     })
            {
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(path), path);
            }
        }

        [Test]
        public void Texts_HaveThemedText_WithTokenValues_AndNoAutoSizeOrRichText()
        {
            UiTheme theme = UiTestSupport.LoadTheme();
            ForEachPrefab((path, root) =>
            {
                foreach (TMP_Text tmp in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    string where = path + " / " + tmp.name;
                    var themed = tmp.GetComponent<ThemedText>();
                    Assert.IsNotNull(themed, where + ": ThemedText 가 없습니다.");
                    int size = theme.GetFontSize(themed.SizeToken, UiTextScale.Normal);
                    Assert.Greater(size, 0, where + ": 크기 토큰 '" + themed.SizeToken + "' 이(가) 테마에 없습니다.");
                    Assert.IsTrue(theme.TryGetColor(themed.ColorToken, out Color color), where + ": 색 토큰 '" + themed.ColorToken + "' 이(가) 테마에 없습니다.");
                    Assert.AreEqual(size, Mathf.RoundToInt(tmp.fontSize), where + ": fontSize 가 토큰 값(보통 단계)과 다릅니다(직접 바꾼 값?).");
                    Assert.IsTrue(NearlyEqual(color, tmp.color), where + ": 글자 색이 토큰 값과 다릅니다.");
                    Assert.IsFalse(tmp.enableAutoSizing, where + ": 자동 크기가 켜져 있습니다.");
                    Assert.IsFalse(tmp.richText, where + ": richText 가 켜져 있습니다.");
                    Assert.IsFalse(tmp.raycastTarget, where + ": 글자의 Raycast Target 이 켜져 있습니다.");
                    if (themed.IsDynamic)
                    {
                        Assert.IsFalse(tmp.parseCtrlCharacters, where + ": 동적 글자의 parseCtrlCharacters 가 켜져 있습니다.");
                    }
                    Assert.AreSame(theme, themed.Theme, where + ": 다른 테마를 가리킵니다.");
                }
            });
        }

        [Test]
        public void TextKeys_ExistInBothLanguages_OrTextIsDynamic()
        {
            TextTable ko = UiTestSupport.LoadTable(UiTestSupport.KoPath);
            TextTable en = UiTestSupport.LoadTable(UiTestSupport.EnPath);
            ForEachPrefab((path, root) =>
            {
                foreach (ThemedText themed in root.GetComponentsInChildren<ThemedText>(true))
                {
                    string where = path + " / " + themed.name;
                    if (string.IsNullOrEmpty(themed.TextKey))
                    {
                        Assert.IsTrue(themed.IsDynamic, where + ": 텍스트 키도 없고 동적 표시도 아닙니다.");
                    }
                    else
                    {
                        Assert.IsTrue(ko.TryGet(themed.TextKey, out _), where + ": 한국어 표에 키 없음 " + themed.TextKey);
                        Assert.IsTrue(en.TryGet(themed.TextKey, out _), where + ": 영어 표에 키 없음 " + themed.TextKey);
                    }
                }
            });
        }

        [Test]
        public void Graphics_HaveThemedGraphic_AndColorMatchesToken()
        {
            ForEachPrefab((path, root) =>
            {
                foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                {
                    if (graphic is TMP_Text || graphic is UiArcGraphic) { continue; } // 글자는 ThemedText, 호는 스스로 토큰을 읽는다
                    string where = path + " / " + graphic.name;
                    var themed = graphic.GetComponent<ThemedGraphic>();
                    Assert.IsNotNull(themed, where + ": ThemedGraphic 이 없습니다.");
                    Assert.IsTrue(NearlyEqual(themed.ResolveColor(), graphic.color), where + ": 실제 color 가 토큰 × alpha 와 다릅니다(직접 바꾼 값?).");
                    Assert.IsTrue(themed.Alpha >= 0.72f || themed.ColorToken == UiTokens.ColorBackdrop || string.IsNullOrEmpty(themed.ColorToken),
                        where + ": 글자가 올라가는 패널의 alpha 가 0.72 미만입니다.");
                    Assert.AreSame(UiTestSupport.LoadTheme(), themed.Theme, where + ": 다른 테마를 가리킵니다.");
                }
            });
        }

        [Test]
        public void RaycastTarget_OnlyOnSelectableTargetsAndBlockers()
        {
            ForEachPrefab((path, root) =>
            {
                foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                {
                    if (!graphic.raycastTarget) { continue; }
                    bool isTarget = graphic.GetComponentsInParent<Selectable>(true).Any(s => s.targetGraphic == graphic);
                    bool isBlocker = graphic.GetComponent<UiRaycastBlocker>() != null;
                    Assert.IsTrue(isTarget || isBlocker, path + " / " + graphic.name + ": Raycast Target 이 켜져 있으나 Selectable 대상도 UiRaycastBlocker 도 아닙니다.");
                }
            });
        }

        [Test]
        public void Selectables_AreUiButtons_WithThemedSelectable_NoColorTransition_AndNavigable()
        {
            ForEachPrefab((path, root) =>
            {
                foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
                {
                    string where = path + " / " + selectable.name;
                    Assert.AreEqual(Selectable.Transition.None, selectable.transition, where + ": transition 이 None 이 아닙니다.");
                    Assert.IsNotNull(selectable.GetComponent<ThemedSelectable>(), where + ": ThemedSelectable 이 없습니다.");
                    if (selectable is Button)
                    {
                        Assert.IsInstanceOf<UiButton>(selectable, where + ": 버튼이 UiButton 이 아닙니다.");
                    }
                    Assert.AreNotEqual(Navigation.Mode.None, selectable.navigation.mode, where + ": 내비게이션이 막혀 있습니다.");
                    Assert.IsTrue(selectable.interactable, where + ": interactable 이 꺼져 있습니다(사용 불가는 UiButton.SetUnavailable).");
                }
            });
        }

        [Test]
        public void ScreenRoots_HaveCanvasScalerGroupRaycasterAndLayerOrder()
        {
            ForEachPrefab((path, root) =>
            {
                bool isScreen = ScreenPrefabFolders.Any(f => path.Contains(f));
                if (!isScreen) { return; }
                var screen = root.GetComponent<UiScreen>();
                Assert.IsNotNull(screen, path + ": UiScreen 이 없습니다.");
                var canvas = root.GetComponent<Canvas>();
                Assert.IsNotNull(canvas, path + ": Canvas 가 없습니다.");
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode, path + ": Screen Space Overlay 가 아닙니다.");
                Assert.AreEqual((int)screen.Layer, canvas.sortingOrder, path + ": sortingOrder 가 UiScreen.Layer 띠 값과 다릅니다.");
                var scaler = root.GetComponent<CanvasScaler>();
                Assert.IsNotNull(scaler, path + ": CanvasScaler 가 없습니다.");
                Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
                Assert.AreEqual(new Vector2(1920, 1080), scaler.referenceResolution);
                Assert.AreEqual(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, scaler.screenMatchMode);
                Assert.AreEqual(1f, scaler.matchWidthOrHeight, 0.0001f, path + ": Match 가 1(높이 기준)이 아닙니다.");
                Assert.IsNotNull(root.GetComponent<CanvasGroup>(), path + ": CanvasGroup 이 없습니다.");
                Assert.IsNotNull(root.GetComponent<GraphicRaycaster>(), path + ": GraphicRaycaster 가 없습니다.");
                foreach (Canvas child in root.GetComponentsInChildren<Canvas>(true).Where(c => c != canvas))
                {
                    Assert.IsNull(child.GetComponent<CanvasScaler>(), path + " / " + child.name + ": 하위 캔버스에 CanvasScaler 가 있습니다.");
                    Assert.IsNull(child.GetComponentInChildren<LayoutGroup>(true), path + " / " + child.name + ": 동적 하위 캔버스 안에 Layout Group 이 있습니다.");
                }
            });
        }

        [Test]
        public void ThemeAsset_HasAllTokens_ScalesAndMinimumFontSize()
        {
            UiTheme theme = UiTestSupport.LoadTheme();
            CollectionAssert.IsEmpty(Game.Editor.UiThemeBuilder.FindMissingTokens(theme), "빠진 토큰");
            Assert.AreEqual(20, UiTokens.AllColors.Length);
            Assert.AreEqual(6, UiTokens.AllSizes.Length);
            Assert.AreEqual(3, UiTokens.AllFonts.Length);
            Assert.AreEqual(0.9f, theme.GetMultiplier(UiTextScale.Small), 0.0001f);
            Assert.AreEqual(1.0f, theme.GetMultiplier(UiTextScale.Normal), 0.0001f);
            Assert.AreEqual(1.25f, theme.GetMultiplier(UiTextScale.Large), 0.0001f);
            foreach (string token in UiTokens.AllSizes)
            {
                Assert.GreaterOrEqual(theme.GetFontSize(token, UiTextScale.Small), 18, token + " 의 「작게」 크기는 18px 이상이어야 합니다.");
            }
        }

        [Test]
        public void ThemeAsset_FontSizesPerStep_EqualV012BaseUssValues()
        {
            UiTheme theme = UiTestSupport.LoadTheme();
            // v0.12 Base.uss 의 단계별 값(전환 전후 화면 크기 동일)
            var expected = new Dictionary<string, int[]>
            {
                [UiTokens.FsCaption] = new[] { 18, 20, 25 },
                [UiTokens.FsBody] = new[] { 22, 24, 30 },
                [UiTokens.FsButton] = new[] { 22, 24, 30 },
                [UiTokens.FsHeading] = new[] { 27, 30, 38 },
                [UiTokens.FsHeadingLg] = new[] { 36, 40, 50 },
                [UiTokens.FsHud] = new[] { 20, 22, 28 },
            };
            foreach (KeyValuePair<string, int[]> entry in expected)
            {
                Assert.AreEqual(entry.Value[0], theme.GetFontSize(entry.Key, UiTextScale.Small), entry.Key + " 작게");
                Assert.AreEqual(entry.Value[1], theme.GetFontSize(entry.Key, UiTextScale.Normal), entry.Key + " 보통");
                Assert.AreEqual(entry.Value[2], theme.GetFontSize(entry.Key, UiTextScale.Large), entry.Key + " 크게");
            }
        }

        [Test]
        public void ThemeAsset_ColorValues_EqualStoryboardPalette()
        {
            UiTheme theme = UiTestSupport.LoadTheme();
            void Expect(string token, byte r, byte g, byte b)
            {
                Assert.IsTrue(theme.TryGetColor(token, out Color c), token);
                Assert.IsTrue(NearlyEqual(new Color32(r, g, b, 255), c), token + " 값이 STORYBOARD 2.1 과 다릅니다.");
            }
            Expect(UiTokens.ColorSurface, 0x14, 0x15, 0x1A);
            Expect(UiTokens.ColorText, 0xE8, 0xE4, 0xDC);
            Expect(UiTokens.ColorTextDim, 0xA9, 0xA5, 0x9C);
            Expect(UiTokens.ColorLight, 0xF2, 0xA5, 0x3A);
            Expect(UiTokens.ColorDanger, 0xE5, 0x59, 0x4F);
            Expect(UiTokens.ColorPure, 0x6F, 0xD1, 0xB0);
            Expect(UiTokens.ColorStamina, 0xA8, 0xD4, 0x5A);
            Expect(UiTokens.ColorTrack, 0x2C, 0x2E, 0x36);
            Assert.IsTrue(theme.TryGetColor(UiTokens.ColorBackdrop, out Color backdrop));
            Assert.AreEqual(0.88f, backdrop.a, 0.0001f);
        }

        [Test]
        public void ThemeAsset_ContrastPairs_MeetMinimumRatio()
        {
            UiTheme theme = UiTestSupport.LoadTheme();
            Assert.GreaterOrEqual(theme.ContrastPairs.Count, 10, "대비 검사 짝이 비어 있습니다.");
            foreach (UiTheme.ContrastPair pair in theme.ContrastPairs)
            {
                Assert.IsTrue(theme.TryGetColor(pair.TextToken, out Color text), pair.TextToken);
                Assert.IsTrue(theme.TryGetColor(pair.BackgroundToken, out Color background), pair.BackgroundToken);
                double ratio = UiTheme.ContrastRatio(text, background);
                Assert.GreaterOrEqual(ratio, pair.MinimumRatio, pair.TextToken + " ↔ " + pair.BackgroundToken + " 대비 " + ratio.ToString("F2") + " : 1");
            }
        }

        [Test]
        public void Theme_FontSizeRounding_HalfRoundsUp()
        {
            Assert.AreEqual(38, UiTheme.ComputeFontSize(30, 1.25f)); // 37.5 → 38
            Assert.AreEqual(28, UiTheme.ComputeFontSize(22, 1.25f)); // 27.5 → 28
            Assert.AreEqual(22, UiTheme.ComputeFontSize(24, 0.9f));  // 21.6 → 22
            Assert.AreEqual(27, UiTheme.ComputeFontSize(30, 0.9f));  // 27.0
        }

        [Test]
        public void Fonts_CoverGlyphsAndTextTableCharacters()
        {
            UiTheme theme = UiTestSupport.LoadTheme();
            TMP_FontAsset regular = theme.GetFont(UiTokens.FontRegular);
            Assert.IsNotNull(regular, "글꼴 토큰이 비어 있습니다.");
            // 전역 개발용 대체 글꼴은 빼고 글꼴 자체(와 그 정적 대체 표)만 본다.
            if (!regular.HasCharacter('가', false, true))
            {
                Assert.Ignore("Pretendard 글꼴 파일 대기 중: 정적 글꼴 사슬에 한글이 없어 글꼴 검사를 건너뜁니다(실행 중에는 개발용 OS 글꼴 대체가 붙음, SDD 7장 UI 전환 1단계).");
            }
            var missing = new HashSet<char>();
            foreach (string text in new[]
                     {
                         System.IO.File.ReadAllText(UiTestSupport.KoPath), System.IO.File.ReadAllText(UiTestSupport.EnPath),
                     })
            {
                foreach (char c in text)
                {
                    if (c > ' ' && !regular.HasCharacter(c, true)) { missing.Add(c); }
                }
            }
            CollectionAssert.IsEmpty(missing, "글꼴 사슬에 없는 글자(□로 보임): " + string.Join("", missing));
        }

        // ---------- 씬 구성 ----------
        private static void WithScene(string path, Action<Scene> check)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.isLoaded;
            if (openedHere)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }
            try
            {
                check(scene);
            }
            finally
            {
                if (openedHere)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void BootScene_HasOneEventSystemWithInputSystemModule_AndUnmodifiedPrefabInstances()
        {
            WithScene(Game.Editor.BootSceneBuilder.BootScenePath, scene =>
            {
                var eventSystems = new List<EventSystem>();
                var standalone = 0;
                var inputModules = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    eventSystems.AddRange(root.GetComponentsInChildren<EventSystem>(true));
                    standalone += root.GetComponentsInChildren<StandaloneInputModule>(true).Length;
                    // Game.Tests.Infrastructure 는 Input System 을 참조하지 않으므로 형식 이름으로 센다.
                    inputModules += root.GetComponentsInChildren<BaseInputModule>(true).Count(m => m.GetType().Name == "InputSystemUIInputModule");
                }
                Assert.AreEqual(1, eventSystems.Count, "EventSystem 은 하나여야 합니다.");
                Assert.AreEqual(0, standalone, "StandaloneInputModule 을 쓰면 안 됩니다.");
                Assert.AreEqual(1, inputModules, "InputSystemUIInputModule 이 하나여야 합니다.");

                // UI 프리팹 인스턴스에 추가된 오브젝트·컴포넌트가 없어야 한다(화면 배치는 프리팹에서 고친다).
                foreach (string name in new[] { "UiRoot", "SteamRequiredScreen", "DevLobbyScreen", "GameHud", "GameMenu" })
                {
                    GameObject instance = scene.GetRootGameObjects().FirstOrDefault(o => o.name == name);
                    Assert.IsNotNull(instance, name + " 인스턴스가 없습니다.");
                    Assert.IsTrue(PrefabUtility.IsAnyPrefabInstanceRoot(instance), name + " 은(는) 프리팹 인스턴스여야 합니다.");
                    Assert.AreEqual(0, PrefabUtility.GetAddedGameObjects(instance).Count, name + " 에 추가된 오브젝트가 있습니다.");
                    Assert.AreEqual(0, PrefabUtility.GetAddedComponents(instance).Count, name + " 에 추가된 컴포넌트가 있습니다.");
                }
            });
        }

        [Test]
        public void ActiveInputHandling_IsInputSystemOnly()
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            SerializedProperty handler = settings.FindProperty("activeInputHandler");
            Assert.IsNotNull(handler, "activeInputHandler 속성을 찾지 못했습니다.");
            Assert.AreEqual(1, handler.intValue, "Active Input Handling 이 Input System 만이 아닙니다.");
        }

        [TestCase("Assets/_Game/Scenes/World.unity")]
        [TestCase("Assets/_Game/Scenes/Bench.unity")]
        public void WorldAndBenchScenes_HaveNoCanvasOrEventSystem(string scenePath)
        {
            WithScene(scenePath, scene =>
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Assert.AreEqual(0, root.GetComponentsInChildren<Canvas>(true).Length, scenePath + ": Canvas 가 있습니다.");
                    Assert.AreEqual(0, root.GetComponentsInChildren<EventSystem>(true).Length, scenePath + ": EventSystem 이 있습니다.");
                }
            });
        }
    }
}

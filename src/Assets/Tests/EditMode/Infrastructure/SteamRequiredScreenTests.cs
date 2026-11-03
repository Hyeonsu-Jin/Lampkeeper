// =============================================================
// 파일: SteamRequiredScreenTests.cs
// 역할: SCR-001(Steam 실행 필요 안내) 화면 시험: UXML 요소, 상태별 표시, 버튼 이벤트,
//       한국어·영어 텍스트 키 누락, Boot 씬 연결.
// 레이어: test (EditMode, Game.Tests.Infrastructure)
// 관련: NFR-006, NFR-034, NFR-044 / STORYBOARD SCR-001 ①~⑤
// =============================================================
using System.Collections.Generic;
using Game.Adapters.Presentation.Localization;
using Game.Adapters.Presentation.UI.Common;
using Game.Adapters.Presentation.UI.Screens;
using Game.Application.Localization;
using Game.Application.Ports;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tests.Infrastructure
{
    public class SteamRequiredScreenTests
    {
        private const string UxmlPath = "Assets/_Game/UI/Screens/SteamRequired.uxml";
        private const string BaseUssPath = "Assets/_Game/UI/Common/Base.uss";
        private const string KoPath = "Assets/_Game/Localization/strings.ko.txt";
        private const string EnPath = "Assets/_Game/Localization/strings.en.txt";

        /// <summary>키를 그대로 "T(키)" 로 돌려주는 가짜 글자 공급자.</summary>
        private sealed class EchoText : ILocalizedText
        {
            public string Get(string key) => "T(" + key + ")";
            public string Get(string key, params object[] args) => Get(key);
        }

        private static VisualElement Instantiate()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            Assert.IsNotNull(uxml, "SteamRequired.uxml 없음");
            return uxml.Instantiate();
        }

        private static TextTable Load(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            Assert.IsNotNull(asset, path);
            var errors = new List<string>();
            TextTable table = TextTable.Parse(asset.text, errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            return table;
        }

        [Test]
        public void Uxml_HasAllElements_AndRetryComesBeforeQuit()
        {
            VisualElement root = Instantiate();
            Assert.IsNotNull(root.Q<Label>("title"));
            Assert.IsNotNull(root.Q<Label>("desc"));
            Assert.IsNotNull(root.Q<Label>("status"));
            Button retry = root.Q<Button>("retry");
            Button quit = root.Q<Button>("quit");
            Assert.IsNotNull(retry);
            Assert.IsNotNull(quit);

            // 포커스 순서 ③ -> ⑤ : 같은 부모 안에서 문서 순서가 이 순서여야 한다.
            Assert.AreEqual(retry.parent, quit.parent);
            Assert.Less(retry.parent.IndexOf(retry), retry.parent.IndexOf(quit));
        }

        [Test]
        public void Uxml_ReferencesScreenStyleSheet()
        {
            VisualElement root = Instantiate();
            Assert.GreaterOrEqual(root.styleSheets.count, 1, "화면 USS가 UXML에 연결되어야 한다.");
        }

        [Test]
        public void View_FillsTexts_FromKeys()
        {
            VisualElement root = Instantiate();
            var view = new SteamRequiredView(root, new EchoText());

            Assert.AreEqual("T(steam.required.title)", root.Q<Label>("title").text);
            Assert.AreEqual("T(steam.required.desc)", root.Q<Label>("desc").text);
            Assert.AreEqual("T(steam.required.retry)", root.Q<Button>("retry").text);
            Assert.AreEqual("T(steam.required.quit)", root.Q<Button>("quit").text);
            Assert.AreEqual(SteamRequiredState.Default, view.State);
        }

        [Test]
        public void View_DefaultState_HidesStatus_EnablesRetry()
        {
            VisualElement root = Instantiate();
            new SteamRequiredView(root, new EchoText());

            Assert.IsTrue(root.Q<Label>("status").ClassListContains(SteamRequiredView.HiddenClass));
            Assert.IsTrue(root.Q<Button>("retry").enabledSelf);
        }

        [Test]
        public void View_CheckingState_DisablesRetry_ShowsCheckingText()
        {
            VisualElement root = Instantiate();
            var view = new SteamRequiredView(root, new EchoText());
            view.SetState(SteamRequiredState.Checking);

            Assert.IsFalse(root.Q<Button>("retry").enabledSelf);
            Assert.AreEqual("T(steam.required.checking)", root.Q<Button>("retry").text);
        }

        [Test]
        public void View_FailedState_ShowsRetryFailedMessage_AndRestoresRetry()
        {
            VisualElement root = Instantiate();
            var view = new SteamRequiredView(root, new EchoText());
            view.SetState(SteamRequiredState.Checking);
            view.SetState(SteamRequiredState.Failed);

            Label status = root.Q<Label>("status");
            Assert.IsFalse(status.ClassListContains(SteamRequiredView.HiddenClass));
            Assert.AreEqual("T(steam.required.retry_failed)", status.text);
            Assert.IsTrue(root.Q<Button>("retry").enabledSelf);
            Assert.AreEqual("T(steam.required.retry)", root.Q<Button>("retry").text);
        }

        [Test]
        public void View_ButtonsRaiseEvents()
        {
            VisualElement root = Instantiate();
            var view = new SteamRequiredView(root, new EchoText());
            int retries = 0, quits = 0;
            view.RetryRequested += () => retries++;
            view.QuitRequested += () => quits++;

            view.RequestRetry();
            view.RequestQuit();
            view.SetState(SteamRequiredState.Checking);
            view.RequestRetry(); // 확인 중에는 중복 입력을 무시한다.
            Assert.AreEqual(1, retries);
            Assert.AreEqual(1, quits);
        }

        [Test]
        public void View_MissingElement_ThrowsClearError()
        {
            Assert.Throws<System.InvalidOperationException>(() => new SteamRequiredView(new VisualElement(), new EchoText()));
        }

        [Test]
        public void TextTables_ContainAllScreenKeys_InBothLanguages()
        {
            // [NFR-044] 누락 0건
            TextTable ko = Load(KoPath);
            TextTable en = Load(EnPath);
            foreach (string key in SteamRequiredTextKeys.All)
            {
                Assert.IsTrue(ko.TryGet(key, out string kv) && kv.Length > 0, "ko 누락: " + key);
                Assert.IsTrue(en.TryGet(key, out string ev) && ev.Length > 0, "en 누락: " + key);
            }
        }

        [Test]
        public void TextTables_HaveIdenticalKeySets()
        {
            var ko = new HashSet<string>(Load(KoPath).Keys);
            var en = new HashSet<string>(Load(EnPath).Keys);
            Assert.IsTrue(ko.SetEquals(en), "ko/en 키 집합이 다릅니다.");
        }

        [Test]
        public void LocalizedText_FallsBackToOtherLanguage_ThenToVisibleMarker()
        {
            var primary = TextTable.Parse("a\tA-primary");
            var fallback = TextTable.Parse("a\tA-fallback\nb\tB-fallback");
            var text = new TableLocalizedText(primary, fallback);

            Assert.AreEqual("A-primary", text.Get("a"));
            Assert.AreEqual("B-fallback", text.Get("b"));
            Assert.AreEqual("[!c]", text.Get("c"));
        }

        [Test]
        public void LocalizedText_FormatsArguments_AndSurvivesBadFormat()
        {
            var text = new TableLocalizedText(TextTable.Parse("k\tHello {0}\nbad\tBroken {"), null);
            Assert.AreEqual("Hello Kim", text.Get("k", "Kim"));
            Assert.AreEqual("Broken {", text.Get("bad", "x"));
        }

        [Test]
        public void UiTheme_AppliesBaseStyle_AndSwitchesTextScaleClass()
        {
            var baseUss = AssetDatabase.LoadAssetAtPath<StyleSheet>(BaseUssPath);
            Assert.IsNotNull(baseUss);
            var panelRoot = new VisualElement();

            UiTheme.Apply(panelRoot, baseUss, UiTextScale.Large);
            Assert.IsTrue(panelRoot.ClassListContains(UiTheme.TextLargeClass));
            Assert.IsTrue(panelRoot.styleSheets.Contains(baseUss));

            UiTheme.SetTextScale(panelRoot, UiTextScale.Small);
            Assert.IsTrue(panelRoot.ClassListContains(UiTheme.TextSmallClass));
            Assert.IsFalse(panelRoot.ClassListContains(UiTheme.TextLargeClass));

            // 같은 스타일을 두 번 붙이지 않는다.
            UiTheme.Apply(panelRoot, baseUss, UiTextScale.Normal);
            Assert.AreEqual(1, panelRoot.styleSheets.count);
        }

        [Test]
        public void ScreenUss_UsesOnlyTokens_NoDirectColorsOrPixelFontSizes()
        {
            // SDD 3.7.10: 화면 USS에서 색 값을 직접 쓰지 않고, 글자 크기는 변수로만 지정한다.
            string screenUss = System.IO.File.ReadAllText("Assets/_Game/UI/Screens/SteamRequired.uss");
            StringAssert.DoesNotContain("rgb(", screenUss);
            StringAssert.DoesNotContain("rgba(", screenUss);
            StringAssert.DoesNotContain("#", screenUss.Replace("/*", string.Empty));
            StringAssert.DoesNotContain("font-size", screenUss);
        }

        [Test]
        public void BootScene_IsWired_AndRegisteredFirstInBuildSettings()
        {
            Assert.AreEqual(Game.Editor.BootSceneBuilder.BootScenePath, EditorBuildSettings.scenes[0].path);
            Assert.IsTrue(EditorBuildSettings.scenes[0].enabled);

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(Game.Editor.BootSceneBuilder.BootScenePath, OpenSceneMode.Additive);
            try
            {
                var bootstrap = Object.FindFirstObjectByType<Game.Bootstrap.GameBootstrap>(FindObjectsInactive.Include);
                var screen = Object.FindFirstObjectByType<SteamRequiredScreen>(FindObjectsInactive.Include);
                Assert.IsNotNull(bootstrap, "Bootstrap 오브젝트 없음");
                Assert.IsNotNull(screen, "SteamRequiredScreen 오브젝트 없음");
                Assert.IsFalse(screen.gameObject.activeSelf, "SCR-001은 평소 꺼져 있어야 한다.");

                var so = new SerializedObject(bootstrap);
                foreach (string field in new[] { "_dataConfig", "_koreanStrings", "_englishStrings", "_steamRequiredScreen" })
                {
                    Assert.IsNotNull(so.FindProperty(field).objectReferenceValue, "Bootstrap." + field + " 연결 누락");
                }
                var screenSo = new SerializedObject(screen);
                Assert.IsNotNull(screenSo.FindProperty("_baseStyle").objectReferenceValue, "SteamRequiredScreen._baseStyle 연결 누락");

                var document = screen.GetComponent<UIDocument>();
                Assert.IsNotNull(document.visualTreeAsset);
                Assert.IsNotNull(document.panelSettings);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}

// =============================================================
// 파일: SteamRequiredScreenTests.cs
// 역할: SCR-001(Steam 실행 필요 안내) 화면 시험: 프리팹 요소, 상태별 표시, 버튼 이벤트(onClick.Invoke),
//       한국어·영어 텍스트 키 누락, 글자 공급자 대체 동작, Boot 씬 연결.
// 레이어: test (EditMode, Game.Tests.Infrastructure)
// 관련: NFR-006, NFR-034, NFR-044 / STORYBOARD SCR-001 ①~⑤ / SDD 3.4.3 (v0.13: uGUI 프리팹 기준)
// =============================================================
using System.Collections.Generic;
using Game.Adapters.Presentation.Localization;
using Game.Adapters.Presentation.UI.Common;
using Game.Adapters.Presentation.UI.Screens;
using Game.Application.Localization;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.Infrastructure
{
    public class SteamRequiredScreenTests
    {
        private SteamRequiredScreen _screen;

        [SetUp]
        public void SetUp()
        {
            UiTestSupport.UseEchoText();
            _screen = UiTestSupport.Instantiate<SteamRequiredScreen>(UiTestSupport.SteamRequiredPrefab);
        }

        [TearDown]
        public void TearDown()
        {
            if (_screen != null)
            {
                Object.DestroyImmediate(_screen.gameObject);
            }
            UiTestSupport.Reset();
        }

        [Test]
        public void Prefab_HasAllElements_AndRetryComesBeforeQuit()
        {
            Assert.IsNotNull(_screen.RetryButton, "③ [다시 확인]");
            Assert.IsNotNull(_screen.QuitButton, "⑤ [종료]");
            Assert.IsNotNull(_screen.StatusText, "② 아래 실패 문구");
            // 키보드·게임패드 이동 순서는 ③ -> ⑤ (하이어라키 순서와 같다).
            Assert.Less(_screen.RetryButton.transform.GetSiblingIndex(), _screen.QuitButton.transform.GetSiblingIndex());
            Assert.AreEqual(UiScreenLayer.System, _screen.Layer, "SCR-001 은 System 층");
        }

        [Test]
        public void Screen_FillsTexts_FromKeys()
        {
            _screen.Show();
            var texts = new List<string>();
            foreach (ThemedText text in _screen.GetComponentsInChildren<ThemedText>(true))
            {
                texts.Add(text.Tmp.text);
            }
            CollectionAssert.Contains(texts, "T(" + SteamRequiredTextKeys.Title + ")");
            CollectionAssert.Contains(texts, "T(" + SteamRequiredTextKeys.Description + ")");
            CollectionAssert.Contains(texts, "T(" + SteamRequiredTextKeys.Retry + ")");
            CollectionAssert.Contains(texts, "T(" + SteamRequiredTextKeys.Quit + ")");
        }

        [Test]
        public void DefaultState_HidesStatus_EnablesRetry()
        {
            _screen.Show();
            Assert.AreEqual(SteamRequiredState.Default, _screen.State);
            Assert.IsFalse(_screen.StatusText.gameObject.activeSelf);
            Assert.IsFalse(_screen.RetryButton.IsUnavailable);
            Assert.IsTrue(_screen.IsShown);
        }

        [Test]
        public void CheckingState_MakesRetryUnavailable_ShowsCheckingText_AndKeepsInteractable()
        {
            _screen.Show();
            _screen.ShowChecking();
            Assert.AreEqual(SteamRequiredState.Checking, _screen.State);
            Assert.IsTrue(_screen.RetryButton.IsUnavailable, "확인 중에는 중복 입력을 막는다");
            Assert.IsTrue(_screen.RetryButton.interactable, "사용 불가여도 interactable 은 끄지 않는다(포커스 유지)");
            Assert.AreEqual("T(" + SteamRequiredTextKeys.Checking + ")", _screen.RetryLabel.Tmp.text);
        }

        [Test]
        public void FailedState_ShowsRetryFailedMessage_AndRestoresRetry()
        {
            _screen.Show();
            _screen.ShowChecking();
            _screen.ShowRetryFailed();
            Assert.AreEqual(SteamRequiredState.Failed, _screen.State);
            Assert.IsTrue(_screen.StatusText.gameObject.activeSelf);
            Assert.AreEqual("T(" + SteamRequiredTextKeys.RetryFailed + ")", _screen.StatusText.Tmp.text);
            Assert.IsFalse(_screen.RetryButton.IsUnavailable);
            Assert.AreEqual("T(" + SteamRequiredTextKeys.Retry + ")", _screen.RetryLabel.Tmp.text);
        }

        [Test]
        public void Buttons_RaiseEvents_ThroughOnClick()
        {
            _screen.Show();
            int retries = 0, quits = 0;
            _screen.RetryRequested += () => retries++;
            _screen.QuitRequested += () => quits++;

            _screen.RetryButton.onClick.Invoke();
            _screen.QuitButton.onClick.Invoke();
            Assert.AreEqual(1, retries);
            Assert.AreEqual(1, quits);

            // 확인 중에는 재시도를 무시한다.
            _screen.ShowChecking();
            _screen.RetryButton.onClick.Invoke();
            Assert.AreEqual(1, retries);
        }

        [Test]
        public void Cancel_DoesNotCloseTheScreen()
        {
            _screen.Show();
            _screen.OnCancel();
            Assert.IsTrue(_screen.IsVisible, "SCR-001 은 Esc 로 닫히지 않는다");
        }

        [Test]
        public void Hide_TurnsOffCanvasRaycasterAndGroupTogether()
        {
            // ESC 메뉴 결함 재발 방지: 숨김은 세 가지를 함께 끈다.
            _screen.Show();
            var canvas = _screen.GetComponent<Canvas>();
            var raycaster = _screen.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            var group = _screen.GetComponent<CanvasGroup>();
            Assert.IsTrue(canvas.enabled && raycaster.enabled && group.blocksRaycasts && group.interactable);

            _screen.Hide();
            Assert.IsFalse(canvas.enabled);
            Assert.IsFalse(raycaster.enabled);
            Assert.IsFalse(group.blocksRaycasts);
            Assert.IsFalse(group.interactable);
        }

        [Test]
        public void TextTables_ContainAllScreenKeys_InBothLanguages()
        {
            // [NFR-044] 누락 0건
            TextTable ko = UiTestSupport.LoadTable(UiTestSupport.KoPath);
            TextTable en = UiTestSupport.LoadTable(UiTestSupport.EnPath);
            foreach (string key in SteamRequiredTextKeys.All)
            {
                Assert.IsTrue(ko.TryGet(key, out string kv) && kv.Length > 0, "ko 누락: " + key);
                Assert.IsTrue(en.TryGet(key, out string ev) && ev.Length > 0, "en 누락: " + key);
            }
        }

        [Test]
        public void TextTables_HaveIdenticalKeySets()
        {
            var ko = new HashSet<string>(UiTestSupport.LoadTable(UiTestSupport.KoPath).Keys);
            var en = new HashSet<string>(UiTestSupport.LoadTable(UiTestSupport.EnPath).Keys);
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
        public void TextScaleChange_ReappliesFontSizes_AcrossAllThemedTexts()
        {
            _screen.Show();
            ThemedText title = null;
            foreach (ThemedText text in _screen.GetComponentsInChildren<ThemedText>(true))
            {
                if (text.TextKey == SteamRequiredTextKeys.Title) { title = text; }
            }
            Assert.IsNotNull(title);
            UiTheme theme = title.Theme;
            Assert.AreEqual(theme.GetFontSize(title.SizeToken, UiTextScale.Normal), Mathf.RoundToInt(title.Tmp.fontSize));

            UiContext.SetTextScale(UiTextScale.Large);
            Assert.AreEqual(theme.GetFontSize(title.SizeToken, UiTextScale.Large), Mathf.RoundToInt(title.Tmp.fontSize));
            UiContext.SetTextScale(UiTextScale.Small);
            Assert.AreEqual(theme.GetFontSize(title.SizeToken, UiTextScale.Small), Mathf.RoundToInt(title.Tmp.fontSize));
        }

        [Test]
        public void BootScene_IsWired_AndRegisteredFirstInBuildSettings()
        {
            Assert.AreEqual(Game.Editor.BootSceneBuilder.BootScenePath, EditorBuildSettings.scenes[0].path);
            Assert.IsTrue(EditorBuildSettings.scenes[0].enabled);

            Scene scene = SceneManager.GetSceneByPath(Game.Editor.BootSceneBuilder.BootScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere)
            {
                scene = EditorSceneManager.OpenScene(Game.Editor.BootSceneBuilder.BootScenePath, OpenSceneMode.Additive);
            }
            try
            {
                var problems = new List<string>();
                Game.Editor.BootSceneBuilder.CheckBootScene(scene, problems);
                CollectionAssert.IsEmpty(problems, string.Join("\n", problems));
            }
            finally
            {
                if (openedHere)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}

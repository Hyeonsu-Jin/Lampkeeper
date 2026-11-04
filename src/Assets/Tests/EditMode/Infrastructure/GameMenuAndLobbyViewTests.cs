// =============================================================
// 파일: GameMenuAndLobbyViewTests.cs
// 역할: ESC 게임 메뉴(SCR-016) 버튼 동작·안내 문구, 로비 화면 층 숨김·상태 글자색, HUD 스태미나 표시 시험.
// 레이어: test (EditMode, Game.Tests.Infrastructure)
// 관련: FR-011, FR-025, SCR-012, SCR-015, SCR-016, NFR-001, I-07 / 사용자 수동 시험 결함(메뉴 버튼 무반응, 로비 배경이 HUD 클릭을 가로챔)
// 버튼은 onClick.Invoke() 로 누른다(v0.13: UI Toolkit 리플렉션 방식 폐지).
// =============================================================
using Game.Adapters.Presentation.UI.Common;
using Game.Adapters.Presentation.UI.Hud;
using Game.Adapters.Presentation.UI.Screens;
using Game.Application.Hud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Tests.Infrastructure
{
    public class GameMenuAndLobbyViewTests
    {
        [SetUp]
        public void SetUp()
        {
            UiTestSupport.UseEchoText();
        }

        [TearDown]
        public void TearDown()
        {
            UiTestSupport.Reset();
        }

        [Test]
        public void MenuButtons_RaiseEvents_AndCloseClosesMenu()
        {
            GameMenu menu = UiTestSupport.Instantiate<GameMenu>(UiTestSupport.GameMenuPrefab);
            try
            {
                menu.Build(new EchoText(), new UiState());
                int invites = 0, leaves = 0;
                menu.InviteRequested += () => invites++;
                menu.LeaveRequested += () => leaves++;

                menu.OpenMenu();
                Assert.IsTrue(menu.IsMenuOpen);
                Assert.IsTrue(menu.IsVisible);
                menu.InviteButton.onClick.Invoke();
                menu.LeaveButton.onClick.Invoke();
                Assert.AreEqual(1, invites, "친구 초대 버튼");
                Assert.AreEqual(1, leaves, "세션 나가기 버튼");

                menu.CloseButton.onClick.Invoke();
                Assert.IsFalse(menu.IsMenuOpen, "닫기(계속하기) 버튼");
                Assert.IsFalse(menu.IsVisible, "닫으면 화면도 숨는다");
            }
            finally
            {
                Object.DestroyImmediate(menu.gameObject);
            }
        }

        [Test]
        public void MenuCancel_ClosesMenu()
        {
            GameMenu menu = UiTestSupport.Instantiate<GameMenu>(UiTestSupport.GameMenuPrefab);
            try
            {
                menu.Build(new EchoText(), new UiState());
                menu.OpenMenu();
                menu.OnCancel(); // Esc, Ⓑ (UiInputRouter 가 전달)
                Assert.IsFalse(menu.IsMenuOpen);
            }
            finally
            {
                Object.DestroyImmediate(menu.gameObject);
            }
        }

        [Test]
        public void MenuNotice_ShowsReason_AndResetsOnReopen()
        {
            GameMenu menu = UiTestSupport.Instantiate<GameMenu>(UiTestSupport.GameMenuPrefab);
            try
            {
                menu.Build(new EchoText(), new UiState());
                menu.SetMenuNotice("안내");
                Assert.AreEqual("안내", menu.MenuNoticeText);
                menu.OpenMenu();
                Assert.AreEqual("T(" + HudTextKeys.MenuDesc + ")", menu.MenuNoticeText);
            }
            finally
            {
                Object.DestroyImmediate(menu.gameObject);
            }
        }

        [Test]
        public void MenuBadge_ShowsGameRunning_WithGlyphAndText()
        {
            GameMenu menu = UiTestSupport.Instantiate<GameMenu>(UiTestSupport.GameMenuPrefab);
            try
            {
                menu.Build(new EchoText(), new UiState());
                // 색(color-pure)만이 아니라 ● 기호와 글자를 함께 쓴다(모양 구분 규칙).
                Assert.AreEqual(UiGlyphs.Running + " T(" + HudTextKeys.GameRunning + ")", menu.Badge.Tmp.text);
            }
            finally
            {
                Object.DestroyImmediate(menu.gameObject);
            }
        }

        [Test]
        public void LobbyHideAll_TurnsOffCanvasRaycasterAndGroup_ShowLobbyRestores()
        {
            // 로비 배경(전체 화면)이 남아 있으면 위 층이 되어 HUD 메뉴 클릭을 가로챈다.
            DevLobbyScreen lobby = UiTestSupport.Instantiate<DevLobbyScreen>(UiTestSupport.DevLobbyPrefab);
            try
            {
                lobby.Build(new EchoText());
                var canvas = lobby.GetComponent<Canvas>();
                var raycaster = lobby.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                var group = lobby.GetComponent<CanvasGroup>();
                Assert.IsTrue(lobby.IsRootVisible);
                Assert.IsTrue(canvas.enabled && raycaster.enabled && group.blocksRaycasts);

                lobby.HideAll();
                Assert.IsFalse(lobby.IsRootVisible);
                Assert.IsFalse(canvas.enabled);
                Assert.IsFalse(raycaster.enabled, "레이캐스터만 남으면 보이지 않는 막이 클릭을 가로챈다");
                Assert.IsFalse(group.blocksRaycasts);
                Assert.IsFalse(group.interactable);

                lobby.ShowLobby();
                Assert.IsTrue(lobby.IsRootVisible);
                Assert.IsTrue(raycaster.enabled && group.blocksRaycasts);
            }
            finally
            {
                Object.DestroyImmediate(lobby.gameObject);
            }
        }

        [Test]
        public void LobbyButtons_RaiseEvents_AndUnavailableButtonsKeepFocusable()
        {
            DevLobbyScreen lobby = UiTestSupport.Instantiate<DevLobbyScreen>(UiTestSupport.DevLobbyPrefab);
            try
            {
                lobby.Build(new EchoText());
                int hosts = 0, refreshes = 0, invites = 0, leaves = 0;
                lobby.HostRequested += () => hosts++;
                lobby.RefreshRequested += () => refreshes++;
                lobby.InviteRequested += () => invites++;
                lobby.LeaveRequested += () => leaves++;

                lobby.HostButton.onClick.Invoke();
                lobby.RefreshButton.onClick.Invoke();
                Assert.AreEqual(1, hosts);
                Assert.AreEqual(1, refreshes);

                // 방을 열기 전: 초대·나가기는 사용 불가(포커스는 받는다)
                Assert.IsTrue(lobby.InviteButton.IsUnavailable);
                Assert.IsTrue(lobby.LeaveButton.IsUnavailable);
                Assert.IsTrue(lobby.InviteButton.interactable);

                // 사용 불가 버튼을 제출(Submit)해도 onClick 은 불리지 않고, 초대는 안내 문구를 위해 같은 이벤트로 알린다.
                ExecuteEvents.Execute(lobby.LeaveButton.gameObject, new BaseEventData(null), ExecuteEvents.submitHandler);
                Assert.AreEqual(0, leaves, "사용 불가 버튼은 onClick 을 부르지 않는다");
                ExecuteEvents.Execute(lobby.InviteButton.gameObject, new BaseEventData(null), ExecuteEvents.submitHandler);
                Assert.AreEqual(1, invites, "사용 불가 초대 버튼을 눌러도 안내가 나가도록 이벤트를 낸다");

                lobby.SetHosting(true, true);
                Assert.IsFalse(lobby.InviteButton.IsUnavailable);
                Assert.IsTrue(lobby.HostButton.IsUnavailable);
                lobby.LeaveButton.onClick.Invoke();
                Assert.AreEqual(1, leaves);
            }
            finally
            {
                Object.DestroyImmediate(lobby.gameObject);
            }
        }

        [Test]
        public void StatusText_UsesLightTextToken()
        {
            // STORYBOARD v1.6: 상태 줄 글자색은 color-text.
            DevLobbyScreen lobby = UiTestSupport.Instantiate<DevLobbyScreen>(UiTestSupport.DevLobbyPrefab);
            try
            {
                Assert.AreEqual(UiTokens.ColorText, lobby.StatusText.ColorToken);
            }
            finally
            {
                Object.DestroyImmediate(lobby.gameObject);
            }
        }

        [Test]
        public void LobbyPanels_SwitchBetweenLobbyConnectingAndFailure()
        {
            DevLobbyScreen lobby = UiTestSupport.Instantiate<DevLobbyScreen>(UiTestSupport.DevLobbyPrefab);
            try
            {
                lobby.Build(new EchoText());
                Assert.IsTrue(lobby.IsLobbyPanelActive);
                lobby.ShowConnecting();
                Assert.IsTrue(lobby.IsConnectPanelActive && !lobby.IsLobbyPanelActive);
                lobby.ShowFailure("join.fail.full");
                Assert.IsTrue(lobby.IsFailPanelActive && !lobby.IsConnectPanelActive);
                int acknowledged = 0;
                lobby.FailureAcknowledged += () => acknowledged++;
                lobby.FailOkButton.onClick.Invoke();
                Assert.AreEqual(1, acknowledged);
            }
            finally
            {
                Object.DestroyImmediate(lobby.gameObject);
            }
        }

        [Test]
        public void LobbyVersionLine_ShowsGivenDisplayText()
        {
            DevLobbyScreen lobby = UiTestSupport.Instantiate<DevLobbyScreen>(UiTestSupport.DevLobbyPrefab);
            try
            {
                lobby.Build(new EchoText());
                lobby.SetVersion("v0.1.0");
                Assert.AreEqual("v0.1.0", lobby.VersionText.Tmp.text);
            }
            finally
            {
                Object.DestroyImmediate(lobby.gameObject);
            }
        }

        [Test]
        public void Hud_StaminaArc_ShowsWhileConsumingAndWarnsWhenLow()
        {
            GameHud hud = UiTestSupport.Instantiate<GameHud>(UiTestSupport.GameHudPrefab);
            try
            {
                hud.Build(new EchoText(), 8);
                hud.Tick(1f, true, 10f, null); // 가득 참 + 오래 지남 → 사라짐
                Assert.AreEqual(0f, hud.ArcOpacity, 0.001f);
                Assert.IsFalse(hud.StaminaWarn.gameObject.activeSelf);

                hud.Tick(0.6f, false, 0.016f, null); // 소모 중 → 보임
                Assert.AreEqual(1f, hud.ArcOpacity, 0.001f);
                Assert.IsFalse(hud.Arc.IsLow);
                Assert.IsFalse(hud.StaminaWarn.gameObject.activeSelf);

                hud.Tick(0.2f, false, 0.016f, null); // 25% 이하 → 점선 + 경고 기호 + 글자
                Assert.IsTrue(hud.Arc.IsLow);
                Assert.IsTrue(hud.StaminaWarn.gameObject.activeSelf);
                StringAssert.Contains(UiGlyphs.Warning, hud.StaminaWarn.Tmp.text);
                StringAssert.Contains("T(" + HudTextKeys.StaminaLow + ")", hud.StaminaWarn.Tmp.text);
            }
            finally
            {
                Object.DestroyImmediate(hud.gameObject);
            }
        }

        [Test]
        public void TextTables_HaveInviteUnavailable_InBothLanguages()
        {
            foreach (string path in new[] { UiTestSupport.KoPath, UiTestSupport.EnPath })
            {
                StringAssert.Contains(HudTextKeys.InviteUnavailable + "\t", System.IO.File.ReadAllText(path), path);
            }
        }
    }
}

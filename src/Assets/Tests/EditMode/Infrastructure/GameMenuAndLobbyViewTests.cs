// =============================================================
// 파일: GameMenuAndLobbyViewTests.cs
// 역할: ESC 게임 메뉴(SCR-016) 버튼 동작과 로비 화면 층 숨김·상태 글자색 시험.
// 레이어: test (EditMode, Game.Tests.Infrastructure)
// 관련: FR-011, SCR-012, SCR-015, SCR-016 / 사용자 수동 시험 결함(메뉴 버튼 무반응)
// =============================================================
using Game.Adapters.Presentation.UI.Common;
using Game.Adapters.Presentation.UI.Hud;
using Game.Adapters.Presentation.UI.Screens;
using Game.Application.Hud;
using Game.Application.Ports;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Game.Tests.Infrastructure
{
    public class GameMenuAndLobbyViewTests
    {
        private sealed class EchoText : ILocalizedText
        {
            public string Get(string key) => "T(" + key + ")";
            public string Get(string key, params object[] args) => Get(key);
        }

        private static VisualElement Load(string path)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            Assert.IsNotNull(uxml, path);
            return uxml.Instantiate();
        }

        /// <summary>
        /// 버튼을 누른 것과 같은 결과를 낸다. 패널에 붙지 않은 요소는 이벤트를 받지 못하므로
        /// Clickable의 클릭 처리(내부 Invoke)를 직접 부른다. clicked 연결 여부를 확인하는 용도다.
        /// </summary>
        private static void Press(Button button)
        {
            using (var e = ClickEvent.GetPooled())
            {
                System.Reflection.MethodInfo invoke = typeof(Clickable).GetMethod("Invoke",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                    null, new[] { typeof(EventBase) }, null);
                Assert.IsNotNull(invoke, "Clickable.Invoke를 찾지 못했습니다(Unity 버전 변경?)");
                invoke.Invoke(button.clickable, new object[] { e });
            }
        }

        [Test]
        public void MenuButtons_RaiseEvents_AndCloseClosesMenu()
        {
            VisualElement root = Load("Assets/_Game/UI/Hud/Hud.uxml");
            var ui = new UiState();
            var view = new GameHudView(root, new EchoText(), ui, 8);
            int invites = 0, leaves = 0;
            view.InviteRequested += () => invites++;
            view.LeaveRequested += () => leaves++;

            view.OpenMenu();
            Assert.IsTrue(view.IsMenuOpen);
            Press(root.Q<Button>("menu-invite"));
            Press(root.Q<Button>("menu-leave"));
            Assert.AreEqual(1, invites, "친구 초대 버튼");
            Assert.AreEqual(1, leaves, "세션 나가기 버튼");

            Press(root.Q<Button>("menu-close"));
            Assert.IsFalse(view.IsMenuOpen, "닫기(계속하기) 버튼");
        }

        [Test]
        public void MenuNotice_ShowsReason_AndResetsOnReopen()
        {
            VisualElement root = Load("Assets/_Game/UI/Hud/Hud.uxml");
            var view = new GameHudView(root, new EchoText(), new UiState(), 8);

            view.SetMenuNotice("안내");
            Assert.AreEqual("안내", view.MenuNoticeText);
            view.OpenMenu();
            Assert.AreEqual("T(hud.menu.desc)", view.MenuNoticeText);
        }

        [Test]
        public void LobbyView_HideAll_HidesBackdropRoot_ShowLobbyRestoresIt()
        {
            // 로비 배경(전체 화면)이 남아 있으면 위 층이 되어 HUD 메뉴 클릭을 가로챈다.
            VisualElement root = Load("Assets/_Game/UI/Screens/DevLobby.uxml");
            var view = new DevLobbyView(root, new EchoText());
            Assert.IsTrue(view.IsRootVisible);

            view.HideAll();
            Assert.IsFalse(view.IsRootVisible);
            Assert.IsTrue(root.Q("root").ClassListContains("is-hidden"));

            view.ShowLobby();
            Assert.IsTrue(view.IsRootVisible);
        }

        [Test]
        public void StatusText_UsesLightTextToken()
        {
            string uss = System.IO.File.ReadAllText("Assets/_Game/UI/Common/Base.uss");
            int start = uss.IndexOf(".modal__status {", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            string block = uss.Substring(start, uss.IndexOf('}', start) - start);
            StringAssert.Contains("color: var(--color-text);", block);
        }

        [Test]
        public void TextTables_HaveInviteUnavailable_InBothLanguages()
        {
            foreach (string path in new[] { "Assets/_Game/Localization/strings.ko.txt", "Assets/_Game/Localization/strings.en.txt" })
            {
                StringAssert.Contains(HudTextKeys.InviteUnavailable + "\t", System.IO.File.ReadAllText(path), path);
            }
        }
    }
}

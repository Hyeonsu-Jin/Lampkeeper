// =============================================================
// 파일: UiLayerPlayTests.cs
// 역할: 화면 층 시험(PlayMode). 로비·게임·게임 + ESC 메뉴 상태에서 화면 가운데의 맨 위 레이캐스트 결과가 맞는지,
//       숨은 화면으로 키보드·패드 내비게이션이 넘어가지 않는지, 화면 스택이 포커스를 열고 되돌리는지 확인한다.
//       (v0.12 ESC 메뉴 결함 — 로비 전체 화면 배경이 HUD 위를 덮어 메뉴 클릭을 가로챔 — 의 회귀 시험)
// 레이어: test (PlayMode, Game.Tests.Integration)
// 관련: FR-011, NFR-002, NFR-004, I-07, DES-091, DES-122 / SDD 3.7.10 (5)·(6), 3.4.3, 7장 UI 전환 10단계
// =============================================================
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Game.Adapters.Presentation.UI.Common;
using Game.Adapters.Presentation.UI.Hud;
using Game.Adapters.Presentation.UI.Screens;
using Game.Application.Hud;
using Game.Application.Ports;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.Integration
{
    public class UiLayerPlayTests
    {
        private sealed class Echo : ILocalizedText
        {
            public string Get(string key) => "T(" + key + ")";
            public string Get(string key, params object[] args) => Get(key);
        }

        private const string Root = "Assets/_Game/UI/Prefabs/";
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private UiRoot _uiRoot;
        private DevLobbyScreen _lobby;
        private GameHud _hud;
        private GameMenu _menu;
        private UiState _uiState;

        private T Spawn<T>(string path) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + path);
            Assert.IsNotNull(prefab, path);
            GameObject instance = Object.Instantiate(prefab);
            _spawned.Add(instance);
            return instance.GetComponent<T>();
        }

        [SetUp]
        public void SetUp()
        {
            _uiRoot = Spawn<UiRoot>("UiRoot.prefab");
            _lobby = Spawn<DevLobbyScreen>("Screens/DevLobby.prefab");
            _hud = Spawn<GameHud>("Hud/GameHud.prefab");
            _menu = Spawn<GameMenu>("Screens/GameMenu.prefab");
            _uiRoot.Initialize(new Echo());
            _uiState = new UiState();
            _lobby.Build(new Echo());
            _hud.Build(new Echo(), 8);
            _menu.Build(new Echo(), _uiState);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) { Object.Destroy(go); }
            }
            _spawned.Clear();
            UiContext.SetText(null);
        }

        private static List<RaycastResult> RaycastCenter()
        {
            var data = new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);
            return results;
        }

        private static bool IsUnder(GameObject hit, Component screen) => hit.transform.IsChildOf(screen.transform);

        [UnityTest]
        public IEnumerator Lobby_TopRaycastHit_IsTheLobbyBackdropOrPanel()
        {
            yield return null;
            List<RaycastResult> results = RaycastCenter();
            Assert.IsNotEmpty(results, "로비가 보이는데 화면 가운데에 레이캐스트 결과가 없습니다.");
            Assert.IsTrue(IsUnder(results[0].gameObject, _lobby), "맨 위 결과가 로비 화면이 아닙니다: " + results[0].gameObject.name);
        }

        [UnityTest]
        public IEnumerator InGame_LobbyHidden_NothingBlocksTheCenter()
        {
            _lobby.HideAll();
            _hud.SetVisible(true);
            yield return null;
            // HUD 는 입력을 받지 않고 로비 배경은 함께 꺼졌으므로, 가운데는 아무 UI 도 클릭을 가로채지 않는다.
            CollectionAssert.IsEmpty(RaycastCenter(), "게임 중인데 보이지 않는 UI 가 가운데 클릭을 가로챕니다.");
        }

        [UnityTest]
        public IEnumerator InGame_WithMenuOpen_TopHitIsTheMenu_AndLobbyIsNeverHit()
        {
            // v0.12 ESC 메뉴 결함 회귀: 로비 배경이 메뉴 위를 덮으면 메뉴 버튼이 눌리지 않았다.
            _lobby.HideAll();
            _hud.SetVisible(true);
            _menu.OpenMenu();
            yield return null;

            List<RaycastResult> results = RaycastCenter();
            Assert.IsNotEmpty(results);
            Assert.IsTrue(IsUnder(results[0].gameObject, _menu), "맨 위 결과가 ESC 메뉴가 아닙니다: " + results[0].gameObject.name);
            foreach (RaycastResult hit in results)
            {
                Assert.IsFalse(IsUnder(hit.gameObject, _lobby), "숨긴 로비가 레이캐스트에 잡힙니다: " + hit.gameObject.name);
                Assert.IsFalse(IsUnder(hit.gameObject, _hud), "HUD 가 레이캐스트에 잡힙니다: " + hit.gameObject.name);
            }
        }

        [UnityTest]
        public IEnumerator MenuOpen_Navigation_NeverLeavesTheMenu()
        {
            _lobby.HideAll();
            _menu.OpenMenu();
            yield return null;

            foreach (Selectable from in new Selectable[] { _menu.InviteButton, _menu.LeaveButton, _menu.CloseButton })
            {
                foreach (Selectable next in new[] { from.FindSelectableOnLeft(), from.FindSelectableOnRight(), from.FindSelectableOnUp(), from.FindSelectableOnDown() })
                {
                    if (next != null)
                    {
                        Assert.IsTrue(IsUnder(next.gameObject, _menu), "내비게이션이 메뉴 밖으로 나갑니다: " + from.name + " -> " + next.name);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator ScreenStack_SelectsFirstOnOpen_AndRestoresFocusOnClose()
        {
            yield return null;
            Assert.AreSame(_lobby.HostButton.gameObject, EventSystem.current.currentSelectedGameObject, "로비의 처음 선택은 [방 열기]");

            _lobby.HideAll();
            _menu.OpenMenu();
            yield return null;
            Assert.AreSame(_menu.CloseButton.gameObject, EventSystem.current.currentSelectedGameObject, "메뉴의 처음 선택은 [닫기]");
            Assert.AreSame(_menu, _uiRoot.Stack.Top);

            _uiRoot.Router.DispatchCancel(); // 같은 프레임이 아니므로 전달된다 (Esc, Ⓑ)
            yield return null;
            Assert.IsFalse(_menu.IsMenuOpen, "닫기 입력 한 번으로 메뉴가 닫혀야 한다");
        }

        [UnityTest]
        public IEnumerator PointerClick_OnMenuButtons_RaisesEvents()
        {
            // UI 가 uGUI 가 되어 포인터 클릭이 EventSystem 경로로 버튼에 닿는지 확인한다(ExecuteEvents 로 클릭 전달).
            _lobby.HideAll();
            _menu.OpenMenu();
            yield return null;
            int leaves = 0;
            _menu.LeaveRequested += () => leaves++;
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(_menu.LeaveButton.gameObject, data, ExecuteEvents.pointerClickHandler);
            Assert.AreEqual(1, leaves);

            _menu.InviteButton.SetUnavailable(true);
            int invites = 0;
            _menu.InviteRequested += () => invites++;
            ExecuteEvents.Execute(_menu.InviteButton.gameObject, data, ExecuteEvents.pointerClickHandler);
            Assert.AreEqual(0, invites, "사용 불가 버튼은 onClick 을 부르지 않는다");
        }
    }
}
#endif

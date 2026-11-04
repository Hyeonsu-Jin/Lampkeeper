// =============================================================
// 파일: GameHud.cs
// 역할: 게임 HUD(SCR-015)의 M1 범위 — 스태미나 호(⑱), 접속자 명단(개발용), 연결 상태 한 줄, 게임 메뉴(Gameplay/Overlay 시험).
//       GameHudView는 요소 연결·표시 계산(일반 클래스, 시험 가능), GameHud는 씬에 올리는 MonoBehaviour.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-011(나가기), FR-025, NFR-001(값이 바뀌면 0.5초 안에 갱신), NFR-056, I-07, DES-091 / SDD 3.7.10, 3.10.7, STORYBOARD SCR-015
//
// 게임 메뉴를 열어도(Overlay) 게임은 멈추지 않는다: 「● 게임 진행 중」 배지를 보이고, 로컬 입력 경로만 UiState가 바꾼다.
// =============================================================
using System;
using System.Text;
using Game.Adapters.Presentation.UI.Common;
using Game.Application.Hud;
using Game.Application.Ports;
using Game.Application.Replication;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Adapters.Presentation.UI.Hud
{
    /// <summary>HUD 요소 연결과 표시 계산.</summary>
    public sealed class GameHudView
    {
        public const string MenuScreenId = "game-menu";
        public const string HiddenClass = "is-hidden";

        private readonly ILocalizedText _text;
        private readonly UiState _uiState;
        private readonly StaminaArcElement _arc = new StaminaArcElement();
        private readonly StaminaHudModel _staminaModel = new StaminaHudModel();
        private readonly VisualElement _arcContainer;
        private readonly Label _warn;
        private readonly Label _rosterTitle;
        private readonly Label _rosterList;
        private readonly Label _netStatus;
        private readonly VisualElement _menuBackdrop;
        private readonly Button _menuLeave;
        private readonly Button _menuClose;
        private readonly Button _menuInvite;
        private readonly Label _menuDesc;
        private int _rosterVersion = -1;
        private int _maxPlayers;

        /// <summary>메뉴의 [세션 나가기]를 눌렀을 때.</summary>
        public event Action LeaveRequested;

        /// <summary>메뉴의 [친구 초대]를 눌렀을 때(Steam 초대 창).</summary>
        public event Action InviteRequested;

        public GameHudView(VisualElement root, ILocalizedText text, UiState uiState, int maxPlayers)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _uiState = uiState ?? throw new ArgumentNullException(nameof(uiState));
            _maxPlayers = maxPlayers;

            _arcContainer = Require<VisualElement>(root, "stamina-arc");
            _warn = Require<Label>(root, "stamina-warn");
            _rosterTitle = Require<Label>(root, "roster-title");
            _rosterList = Require<Label>(root, "roster-list");
            _netStatus = Require<Label>(root, "net-status");
            _menuBackdrop = Require<VisualElement>(root, "menu-backdrop");
            _menuLeave = Require<Button>(root, "menu-leave");
            _menuClose = Require<Button>(root, "menu-close");
            _menuInvite = Require<Button>(root, "menu-invite");

            _arc.style.flexGrow = 1f;
            _arc.pickingMode = PickingMode.Ignore;
            _arcContainer.Add(_arc);
            _arcContainer.style.opacity = 0f;

            Require<Label>(root, "menu-title").text = _text.Get(HudTextKeys.MenuTitle);
            _menuDesc = Require<Label>(root, "menu-desc");
            _menuDesc.text = _text.Get(HudTextKeys.MenuDesc);
            Require<Label>(root, "menu-badge").text = UiGlyphs.Running + " " + _text.Get(HudTextKeys.GameRunning);
            _menuInvite.text = _text.Get(HudTextKeys.MenuInvite);
            _menuInvite.clicked += () => InviteRequested?.Invoke();
            _menuLeave.text = _text.Get(HudTextKeys.MenuLeave);
            _menuClose.text = _text.Get(HudTextKeys.MenuClose);
            _menuLeave.clicked += () => LeaveRequested?.Invoke();
            _menuClose.clicked += CloseMenu;
            _uiState.ModeChanged += _ => RefreshMenu();
            RefreshMenu();
        }

        /// <summary>게임 메뉴가 열려 있는지.</summary>
        public bool IsMenuOpen => _uiState.IsOpen(MenuScreenId);

        /// <summary>메뉴 안내 줄의 현재 글자(시험·진단용).</summary>
        public string MenuNoticeText => _menuDesc.text;

        /// <summary>버튼 동작을 못 할 때 메뉴 안내 줄에 이유를 보인다(아무 반응 없이 끝나지 않게).</summary>
        public void SetMenuNotice(string text)
        {
            _menuDesc.text = text ?? string.Empty;
        }

        public void OpenMenu()
        {
            _menuDesc.text = _text.Get(HudTextKeys.MenuDesc);
            _uiState.Open(MenuScreenId);
            RefreshMenu();
            _menuClose.Focus();
        }

        public void CloseMenu()
        {
            _uiState.Close(MenuScreenId);
            RefreshMenu();
        }

        /// <summary>최대 인원을 바꾼다(명단 제목 「접속자 n/8」).</summary>
        public void SetMaxPlayers(int maxPlayers)
        {
            _maxPlayers = maxPlayers;
            _rosterVersion = -1;
        }

        /// <summary>
        /// 매 프레임 불러 스태미나 호와 명단을 갱신한다. 값이 바뀐 프레임에 바로 반영해 0.5초 안 갱신 기준을 지킨다. [NFR-001]
        /// </summary>
        /// <param name="ratio">스태미나 비율(0~1)</param>
        /// <param name="isFull">가득 찼는지</param>
        /// <param name="deltaSeconds">경과 시간(비스케일 시간)</param>
        public void Tick(float ratio, bool isFull, float deltaSeconds, ClientWorldMirror mirror)
        {
            StaminaHudState state = _staminaModel.Update(ratio, isFull, deltaSeconds);
            _arcContainer.style.opacity = state.Opacity;
            _arc.SetState(state.Ratio, state.Low);

            // 25% 이하: 점선 호 + 경고 기호 + 글자(색에만 의존하지 않음)
            bool showWarn = state.Visible && state.Low;
            _warn.EnableInClassList(HiddenClass, !showWarn);
            if (showWarn)
            {
                _warn.text = UiGlyphs.Warning + " " + _text.Get(HudTextKeys.StaminaLow);
            }

            if (mirror != null && mirror.Roster.Version != _rosterVersion)
            {
                _rosterVersion = mirror.Roster.Version;
                RefreshRoster(mirror);
            }
        }

        /// <summary>연결 상태 한 줄(개발용).</summary>
        public void SetNetStatus(string text) => _netStatus.text = text ?? string.Empty;

        private void RefreshRoster(ClientWorldMirror mirror)
        {
            _rosterTitle.text = _text.Get(HudTextKeys.RosterTitle, mirror.Roster.Count, _maxPlayers);
            var builder = new StringBuilder();
            var slots = new System.Collections.Generic.List<uint>();
            foreach (System.Collections.Generic.KeyValuePair<uint, RosterEntry> pair in mirror.Roster.Entries)
            {
                slots.Add(pair.Key);
            }
            slots.Sort();
            foreach (uint slot in slots)
            {
                mirror.Roster.TryGet(slot, out RosterEntry entry);
                // 동료는 색이 아니라 ◆ 기호와 이름으로 구분한다. 호스트는 ★ 표시. (플레이어별 색 없음)
                builder.Append(entry.IsHost ? UiGlyphs.Host : UiGlyphs.Ally).Append(' ').Append(entry.Name);
                if (entry.IsHost)
                {
                    builder.Append(" (").Append(_text.Get(HudTextKeys.RosterHostTag)).Append(')');
                }
                builder.Append('\n');
            }
            _rosterList.text = builder.ToString().TrimEnd('\n');
        }

        private void RefreshMenu()
        {
            _menuBackdrop.EnableInClassList(HiddenClass, !IsMenuOpen);
        }

        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException("Hud.uxml에 '" + name + "' (" + typeof(T).Name + ") 요소가 없습니다.");
            }
            return element;
        }
    }

    /// <summary>HUD를 씬에 올리는 컴포넌트. UIDocument(Hud.uxml + PanelSettings)가 붙어 있어야 한다.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameHud : MonoBehaviour
    {
        [Tooltip("공통 USS(색 토큰, 글자 크기 변수). UI/Common/Base.uss")]
        [SerializeField] private StyleSheet _baseStyle;

        private GameHudView _view;

        public GameHudView View => _view;

        /// <summary>HUD를 만든다(처음 한 번).</summary>
        public GameHudView Build(ILocalizedText text, UiState uiState, int maxPlayers, UiTextScale scale = UiTextScale.Normal)
        {
            gameObject.SetActive(true);
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null || root.childCount == 0)
            {
                throw new InvalidOperationException("GameHud의 UIDocument에 Hud.uxml이 연결되지 않았습니다.");
            }
            UiTheme.Apply(root, _baseStyle, scale);
            _view = new GameHudView(root, text, uiState, maxPlayers);
            return _view;
        }

        /// <summary>HUD를 보이거나 숨긴다. 오브젝트를 끄면 요소 트리가 새로 만들어져 연결이 끊기므로 표시만 바꾼다.</summary>
        public void SetVisible(bool visible)
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            if (root != null)
            {
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}

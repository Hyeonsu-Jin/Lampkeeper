// =============================================================
// 파일: GameMenu.cs
// 역할: ESC 게임 메뉴(SCR-016). [친구 초대] [세션 나가기] [닫기(계속하기)]와 안내 줄, 「게임 진행 중」 배지.
//       v0.13 부터 HUD 에서 분리된 독립 화면 프리팹(Screens/GameMenu)이다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-011(나가기), I-07(화면을 열어도 게임은 멈추지 않음), NFR-004, DES-091, DES-122 / STORYBOARD SCR-016 / SDD 3.7.10 (5), 3.10.7
//
// 메뉴를 열어도(Overlay) 게임은 멈추지 않는다. 로컬 입력 경로만 UiState 가 바꾼다.
// 닫기(Esc, Ⓑ)는 UiInputRouter 가 OnCancel 로 전달한다.
// =============================================================
using System;
using Game.Adapters.Presentation.UI.Common;
using Game.Application.Hud;
using Game.Application.Ports;
using UnityEngine;

namespace Game.Adapters.Presentation.UI.Screens
{
    /// <summary>ESC 게임 메뉴 화면 컴포넌트.</summary>
    public sealed class GameMenu : UiScreen
    {
        /// <summary>UiState 에 등록하는 화면 ID.</summary>
        public const string MenuScreenId = "game-menu";

        [SerializeField] private ThemedText _badge;
        [SerializeField] private ThemedText _description;
        [SerializeField] private UiButton _invite;
        [SerializeField] private UiButton _leave;
        [SerializeField] private UiButton _close;

        private ILocalizedText _text;
        private UiState _uiState;

        /// <summary>메뉴의 [세션 나가기]를 눌렀을 때.</summary>
        public event Action LeaveRequested;

        /// <summary>메뉴의 [친구 초대]를 눌렀을 때(Steam 초대 창).</summary>
        public event Action InviteRequested;

        public UiButton InviteButton => _invite;
        public UiButton LeaveButton => _leave;
        public UiButton CloseButton => _close;
        public ThemedText Badge => _badge;

        /// <summary>메뉴가 열려 있는지.</summary>
        public bool IsMenuOpen => _uiState != null && _uiState.IsOpen(MenuScreenId);

        /// <summary>메뉴 안내 줄의 현재 글자(시험·진단용).</summary>
        public string MenuNoticeText => _description.Tmp.text;

        private bool _wired;

        protected override void Awake()
        {
            base.Awake();
            Wire();
        }

        /// <summary>버튼 동작을 한 번만 연결한다(편집 모드 시험에서는 Awake 가 불리지 않으므로 Build 에서도 부른다).</summary>
        private void Wire()
        {
            if (_wired)
            {
                return;
            }
            _wired = true;
            _invite.onClick.AddListener(() => InviteRequested?.Invoke());
            _leave.onClick.AddListener(() => LeaveRequested?.Invoke());
            _close.onClick.AddListener(CloseMenu);
        }

        /// <summary>메뉴를 시작 상태(닫힘)로 만든다. UiState 의 모드가 바뀔 때마다 표시를 맞춘다.</summary>
        public GameMenu Build(ILocalizedText text, UiState uiState)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _uiState = uiState ?? throw new ArgumentNullException(nameof(uiState));
            Wire();
            _badge.SetDynamicText(_text.Get(HudTextKeys.GameRunning));
            _description.SetDynamicText(_text.Get(HudTextKeys.MenuDesc));
            _uiState.ModeChanged += _ => RefreshVisibility();
            RefreshVisibility();
            return this;
        }

        /// <summary>버튼 동작을 못 할 때 안내 줄에 이유를 보인다(아무 반응 없이 끝나지 않게).</summary>
        public void SetMenuNotice(string text)
        {
            _description.SetDynamicText(text ?? string.Empty);
        }

        /// <summary>메뉴를 연다(처음 선택: 닫기).</summary>
        public void OpenMenu()
        {
            _description.SetDynamicText(_text.Get(HudTextKeys.MenuDesc));
            _uiState.Open(MenuScreenId);
            RefreshVisibility();
            Select(_close);
        }

        /// <summary>메뉴를 닫는다.</summary>
        public void CloseMenu()
        {
            _uiState.Close(MenuScreenId);
            RefreshVisibility();
        }

        /// <summary>닫기 입력(Esc, Ⓑ)은 메뉴 닫기다.</summary>
        public override void OnCancel()
        {
            CloseMenu();
        }

        private void RefreshVisibility()
        {
            if (IsVisible != IsMenuOpen)
            {
                SetVisible(IsMenuOpen);
            }
        }
    }
}

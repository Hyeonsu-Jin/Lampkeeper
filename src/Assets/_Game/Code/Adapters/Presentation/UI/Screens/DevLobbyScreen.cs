// =============================================================
// 파일: DevLobbyScreen.cs
// 역할: M1 개발용 로비·접속 화면. 로비 패널(방 열기, 방 목록, 친구 초대, 나가기), 참가 중 패널(SCR-012 ①~③),
//       참가 실패 패널(SCR-012 ④~⑦)을 한 화면 프리팹에서 전환한다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-001~004, FR-009, NFR-003, NFR-018, NFR-034, NFR-056, DES-091, DES-122 / STORYBOARD SCR-004(M1 최소), SCR-010(목록 형식), SCR-012 / SDD D-27
//
// M1 화면은 개발용이며 글자는 처음부터 텍스트 키로 쓴다(M2-01에서 String Table로 연결). 색·글자 크기는 테마 토큰만 쓴다.
// 상태 표시는 ✔▶○ 기호와 글자를 함께 쓴다(색에만 의존하지 않음).
// =============================================================
using System;
using System.Collections.Generic;
using Game.Adapters.Presentation.UI.Common;
using Game.Application.Hud;
using Game.Application.Ports;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapters.Presentation.UI.Screens
{
    /// <summary>로비 목록 표시 상태(SCR-010 상태별 표시).</summary>
    public enum LobbyListState : byte
    {
        Idle = 0,
        Loading = 1,
        Empty = 2,
        Error = 3,
        Loaded = 4,
    }

    /// <summary>개발용 로비·접속 화면.</summary>
    public sealed class DevLobbyScreen : UiScreen
    {
        [Header("패널")]
        [SerializeField] private GameObject _lobbyPanel;
        [SerializeField] private GameObject _connectPanel;
        [SerializeField] private GameObject _failPanel;

        [Header("로비 패널")]
        [SerializeField] private ThemedText _status;
        [SerializeField] private ThemedText _listState;
        [SerializeField] private RectTransform _listContent;
        [SerializeField] private UiButton _rowPrefab;
        [SerializeField] private UiButton _host;
        [SerializeField] private UiButton _refresh;
        [SerializeField] private UiButton _invite;
        [SerializeField] private UiButton _leave;
        [SerializeField] private ThemedText _version;

        [Header("참가 중 패널")]
        [SerializeField] private ThemedText[] _steps = new ThemedText[0];
        [SerializeField] private RectTransform _progressFill;
        [SerializeField] private UiButton _connectCancel;

        [Header("참가 실패 패널")]
        [SerializeField] private ThemedText _failTitle;
        [SerializeField] private ThemedText _failCause;
        [SerializeField] private ThemedText _failFix;
        [SerializeField] private UiButton _failOk;

        private ILocalizedText _text;

        public event Action HostRequested;
        public event Action RefreshRequested;
        public event Action InviteRequested;
        public event Action LeaveRequested;
        public event Action<LobbyInfo> JoinRequested;
        public event Action CancelRequested;
        public event Action FailureAcknowledged;

        public UiButton HostButton => _host;
        public UiButton RefreshButton => _refresh;
        public UiButton InviteButton => _invite;
        public UiButton LeaveButton => _leave;
        public UiButton ConnectCancelButton => _connectCancel;
        public UiButton FailOkButton => _failOk;
        public ThemedText StatusText => _status;
        public ThemedText VersionText => _version;
        public RectTransform ListContent => _listContent;

        /// <summary>현재 로비 패널이 보이는지(시험용).</summary>
        public bool IsLobbyPanelActive => _lobbyPanel.activeSelf;
        public bool IsConnectPanelActive => _connectPanel.activeSelf;
        public bool IsFailPanelActive => _failPanel.activeSelf;

        /// <summary>로비 화면 전체(어두운 배경 포함)가 보이는지.</summary>
        public bool IsRootVisible => IsVisible;

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
            _host.onClick.AddListener(() => HostRequested?.Invoke());
            _refresh.onClick.AddListener(() => RefreshRequested?.Invoke());
            _invite.onClick.AddListener(() => InviteRequested?.Invoke());
            // 초대 버튼이 사용 불가일 때 눌러도 아무 반응 없이 끝나지 않게, 같은 경로로 안내 문구를 보인다.
            _invite.UnavailablePressed += _ => InviteRequested?.Invoke();
            _leave.onClick.AddListener(() => LeaveRequested?.Invoke());
            _connectCancel.onClick.AddListener(() => CancelRequested?.Invoke());
            _failOk.onClick.AddListener(() => FailureAcknowledged?.Invoke());
        }

        /// <summary>화면을 시작 상태로 만든다(처음 한 번). 이후 표시·숨김은 같은 컴포넌트의 메서드로 한다.</summary>
        /// <param name="text">동적 글자를 만들 글자 공급자</param>
        public DevLobbyScreen Build(ILocalizedText text)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            Wire();
            SetStatus(_text.Get(LobbyTextKeys.StatusReady));
            SetHosting(false, false);
            SetListState(LobbyListState.Idle, null);
            ShowLobby();
            return this;
        }

        /// <summary>게임 버전 표시 문자열(예 v0.1.0, v0.1.0+dev.…)을 한 줄로 보인다. [NFR-058]</summary>
        public void SetVersion(string displayText) => _version.SetDynamicText(displayText);

        /// <summary>로비 패널을 보인다(기본 포커스: 방 열기).</summary>
        public void ShowLobby()
        {
            Show(_lobbyPanel);
            Select(_host);
        }

        /// <summary>참가 중 패널을 보인다(기본 포커스: 취소).</summary>
        public void ShowConnecting()
        {
            Show(_connectPanel);
            SetProgress(ConnectScreenModel.Compute(Game.Application.Session.ClientSessionState.Connecting));
            Select(_connectCancel);
        }

        /// <summary>
        /// 참가 실패 패널을 보인다. 제목·원인·해결 문장은 키 접두사에 .title/.cause/.fix를 붙여 조회한다. [NFR-003]
        /// </summary>
        public void ShowFailure(string keyPrefix)
        {
            Show(_failPanel);
            _failTitle.SetDynamicText(UiGlyphs.Warning + " " + _text.Get(keyPrefix + ".title"));
            _failCause.SetDynamicText(_text.Get(keyPrefix + ".cause"));
            _failFix.SetDynamicText(_text.Get(keyPrefix + ".fix"));
            Select(_failOk);
        }

        /// <summary>
        /// 화면 전체를 숨긴다(게임 중). 배경까지 함께 숨겨야 위에 있는 전체 화면 막이 HUD 클릭을 가로채지 않는다.
        /// Canvas, GraphicRaycaster, CanvasGroup 을 함께 끈다.
        /// </summary>
        public void HideAll()
        {
            SetVisible(false);
        }

        /// <summary>로비가 아직 시작 전에 닫기(Esc)를 눌러도 아무 일도 없다. 참가 중이면 취소, 실패 패널이면 확인과 같다.</summary>
        public override void OnCancel()
        {
            if (_connectPanel.activeSelf)
            {
                CancelRequested?.Invoke();
            }
            else if (_failPanel.activeSelf)
            {
                FailureAcknowledged?.Invoke();
            }
        }

        public void SetStatus(string text) => _status.SetDynamicText(text);

        /// <summary>방을 연 상태에 따라 버튼을 사용 가능/불가로 바꾼다(포커스는 유지, interactable 은 끄지 않음).</summary>
        public void SetHosting(bool hosting, bool canInvite)
        {
            _host.SetEnabled(!hosting);
            _invite.SetEnabled(hosting && canInvite);
            _leave.SetEnabled(hosting);
        }

        /// <summary>참가 중 패널의 단계와 진행 막대를 갱신한다.</summary>
        public void SetProgress(ConnectProgress progress)
        {
            _steps[0].SetDynamicText(StepText(progress.ConnectHost, LobbyTextKeys.StepHost));
            _steps[1].SetDynamicText(StepText(progress.ReceiveWorldInfo, LobbyTextKeys.StepInfo));
            _steps[2].SetDynamicText(StepText(progress.CreateWorld, LobbyTextKeys.StepWorld));
            _steps[3].SetDynamicText(StepText(progress.ApplyChanges, LobbyTextKeys.StepApply));
            // 채움 막대는 왼쪽에서 오른쪽으로 앵커 폭을 늘려 그린다.
            Vector2 max = _progressFill.anchorMax;
            max.x = Mathf.Clamp01(progress.Fraction);
            if (_progressFill.anchorMax != max)
            {
                _progressFill.anchorMax = max;
            }
        }

        /// <summary>방 목록을 표시한다(SCR-010 상태별: 로딩/빈 목록/오류/성공).</summary>
        public void SetListState(LobbyListState state, IReadOnlyList<LobbyInfo> lobbies)
        {
            for (int i = _listContent.childCount - 1; i >= 0; i--)
            {
                Transform child = _listContent.GetChild(i);
                child.SetParent(null, false);
                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject); // 편집 모드 시험
                }
            }
            switch (state)
            {
                case LobbyListState.Loading: _listState.SetDynamicText(_text.Get(LobbyTextKeys.ListLoading)); break;
                case LobbyListState.Empty: _listState.SetDynamicText(_text.Get(LobbyTextKeys.ListEmpty)); break;
                case LobbyListState.Error: _listState.SetDynamicText(UiGlyphs.Warning + " " + _text.Get(LobbyTextKeys.ListError)); break;
                default: _listState.SetDynamicText(string.Empty); break;
            }
            if (state != LobbyListState.Loaded || lobbies == null)
            {
                return;
            }
            foreach (LobbyInfo lobby in lobbies)
            {
                LobbyInfo captured = lobby;
                UiButton row = Instantiate(_rowPrefab, _listContent, false);
                row.gameObject.SetActive(true);
                row.name = "Row";
                string label = _text.Get(LobbyTextKeys.RowFormat, lobby.GetMeta(LobbyMetaKeys.World), lobby.GetMeta(LobbyMetaKeys.Host), lobby.MemberCount, lobby.MaxMembers)
                               + "  [" + _text.Get(LobbyTextKeys.Join) + "]";
                row.GetComponentInChildren<ThemedText>(true).SetDynamicText(label);
                row.onClick.AddListener(() => JoinRequested?.Invoke(captured));
            }
        }

        private string StepText(StepStatus status, string key)
        {
            string glyph = status == StepStatus.Done ? UiGlyphs.Ok : status == StepStatus.Active ? UiGlyphs.Active : UiGlyphs.Pending;
            return glyph + " " + _text.Get(key);
        }

        private void Show(GameObject panel)
        {
            SetVisible(true);
            _lobbyPanel.SetActive(panel == _lobbyPanel);
            _connectPanel.SetActive(panel == _connectPanel);
            _failPanel.SetActive(panel == _failPanel);
        }
    }
}

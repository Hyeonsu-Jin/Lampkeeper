// =============================================================
// 파일: DevLobbyScreen.cs
// 역할: M1 개발용 로비·접속 화면. 로비 패널(방 열기, 방 목록, 친구 초대, 나가기), 참가 중 패널(SCR-012 ①~③),
//       참가 실패 패널(SCR-012 ④~⑦)을 한 UIDocument에서 전환한다. DevLobbyView는 요소 연결·표시(시험 가능), DevLobbyScreen은 씬 컴포넌트.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-001~004, FR-009, NFR-003, NFR-018, NFR-034, NFR-056, DES-091 / STORYBOARD SCR-004(M1 최소), SCR-010(목록 형식), SCR-012
//
// M1 화면은 개발용이며 글자는 처음부터 텍스트 키로 쓴다(M2-01에서 String Table로 연결). 색·글자 크기는 Base.uss 토큰만 쓴다.
// 상태 표시는 ✔▶○ 기호와 글자를 함께 쓴다(색에만 의존하지 않음).
// =============================================================
using System;
using System.Collections.Generic;
using Game.Adapters.Presentation.UI.Common;
using Game.Application.Hud;
using Game.Application.Ports;
using UnityEngine;
using UnityEngine.UIElements;

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

    /// <summary>개발용 로비·접속 화면의 요소 연결과 표시.</summary>
    public sealed class DevLobbyView
    {
        public const string HiddenClass = "is-hidden";

        private readonly ILocalizedText _text;
        private readonly VisualElement _root;
        private readonly VisualElement _lobbyPanel;
        private readonly VisualElement _connectPanel;
        private readonly VisualElement _failPanel;
        private readonly Label _status;
        private readonly Label _listState;
        private readonly ScrollView _list;
        private readonly Button _host;
        private readonly Button _refresh;
        private readonly Button _invite;
        private readonly Button _leave;
        private readonly Label[] _steps;
        private readonly VisualElement _fill;
        private readonly Button _connectCancel;
        private readonly Label _failTitle;
        private readonly Label _failCause;
        private readonly Label _failFix;
        private readonly Button _failOk;

        public event Action HostRequested;
        public event Action RefreshRequested;
        public event Action InviteRequested;
        public event Action LeaveRequested;
        private readonly Label _version;

        /// <summary>게임 버전 표시 문자열(예 v0.1.0, v0.1.0+dev.…)을 한 줄로 보인다. [NFR-058]</summary>
        public void SetVersion(string displayText) => _version.text = displayText ?? string.Empty;

        public event Action<LobbyInfo> JoinRequested;
        public event Action CancelRequested;
        public event Action FailureAcknowledged;

        public DevLobbyView(VisualElement root, ILocalizedText text)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            _text = text ?? throw new ArgumentNullException(nameof(text));

            _root = Require<VisualElement>(root, "root");
            _lobbyPanel = Require<VisualElement>(root, "lobby-panel");
            _connectPanel = Require<VisualElement>(root, "connect-panel");
            _failPanel = Require<VisualElement>(root, "fail-panel");
            _status = Require<Label>(root, "lobby-status");
            _listState = Require<Label>(root, "list-state");
            _list = Require<ScrollView>(root, "lobby-list");
            _host = Require<Button>(root, "host");
            _refresh = Require<Button>(root, "refresh");
            _invite = Require<Button>(root, "invite");
            _leave = Require<Button>(root, "leave");
            _steps = new[] { Require<Label>(root, "step-host"), Require<Label>(root, "step-info"), Require<Label>(root, "step-world"), Require<Label>(root, "step-apply") };
            _fill = Require<VisualElement>(root, "connect-fill");
            _connectCancel = Require<Button>(root, "connect-cancel");
            _failTitle = Require<Label>(root, "fail-title");
            _failCause = Require<Label>(root, "fail-cause");
            _failFix = Require<Label>(root, "fail-fix");
            _failOk = Require<Button>(root, "fail-ok");
            _version = Require<Label>(root, "lobby-version");

            // 글자는 키로만 가져온다(NFR-034).
            Require<Label>(root, "lobby-title").text = _text.Get(LobbyTextKeys.Title);
            Require<Label>(root, "lobby-desc").text = _text.Get(LobbyTextKeys.Description);
            _host.text = _text.Get(LobbyTextKeys.Host);
            _refresh.text = _text.Get(LobbyTextKeys.Refresh);
            _invite.text = _text.Get(LobbyTextKeys.Invite);
            _leave.text = _text.Get(LobbyTextKeys.Leave);
            Require<Label>(root, "connect-title").text = _text.Get(LobbyTextKeys.ConnectTitle);
            _connectCancel.text = _text.Get(LobbyTextKeys.Cancel);
            _failOk.text = _text.Get(LobbyTextKeys.Ok);

            _host.clicked += () => HostRequested?.Invoke();
            _refresh.clicked += () => RefreshRequested?.Invoke();
            _invite.clicked += () => InviteRequested?.Invoke();
            _leave.clicked += () => LeaveRequested?.Invoke();
            _connectCancel.clicked += () => CancelRequested?.Invoke();
            _failOk.clicked += () => FailureAcknowledged?.Invoke();

            SetStatus(_text.Get(LobbyTextKeys.StatusReady));
            SetHosting(false, false);
            SetListState(LobbyListState.Idle, null);
            ShowLobby();
        }

        /// <summary>로비 패널을 보인다(기본 포커스: 방 열기).</summary>
        public void ShowLobby()
        {
            Show(_lobbyPanel);
            _host.Focus();
        }

        /// <summary>참가 중 패널을 보인다(기본 포커스: 취소).</summary>
        public void ShowConnecting()
        {
            Show(_connectPanel);
            SetProgress(ConnectScreenModel.Compute(Game.Application.Session.ClientSessionState.Connecting));
            _connectCancel.Focus();
        }

        /// <summary>
        /// 참가 실패 패널을 보인다. 제목·원인·해결 문장은 키 접두사에 .title/.cause/.fix를 붙여 조회한다. [NFR-003]
        /// </summary>
        public void ShowFailure(string keyPrefix)
        {
            Show(_failPanel);
            _failTitle.text = UiGlyphs.Warning + " " + _text.Get(keyPrefix + ".title");
            _failCause.text = _text.Get(keyPrefix + ".cause");
            _failFix.text = _text.Get(keyPrefix + ".fix");
            _failOk.Focus();
        }

        /// <summary>로비 화면 전체(어두운 배경 포함)가 보이는지.</summary>
        public bool IsRootVisible => !_root.ClassListContains(HiddenClass);

        /// <summary>모든 패널을 숨긴다(게임 중). 배경까지 숨겨야 위에 있는 전체 화면 층이 HUD 클릭을 가로채지 않는다.</summary>
        public void HideAll()
        {
            _root.AddToClassList(HiddenClass);
            _lobbyPanel.AddToClassList(HiddenClass);
            _connectPanel.AddToClassList(HiddenClass);
            _failPanel.AddToClassList(HiddenClass);
        }

        public void SetStatus(string text) => _status.text = text ?? string.Empty;

        /// <summary>방을 연 상태에 따라 버튼을 켜고 끈다.</summary>
        public void SetHosting(bool hosting, bool canInvite)
        {
            _host.SetEnabled(!hosting);
            _invite.SetEnabled(hosting && canInvite);
            _leave.SetEnabled(hosting);
        }

        /// <summary>참가 중 패널의 단계와 진행 막대를 갱신한다.</summary>
        public void SetProgress(ConnectProgress progress)
        {
            _steps[0].text = StepText(progress.ConnectHost, LobbyTextKeys.StepHost);
            _steps[1].text = StepText(progress.ReceiveWorldInfo, LobbyTextKeys.StepInfo);
            _steps[2].text = StepText(progress.CreateWorld, LobbyTextKeys.StepWorld);
            _steps[3].text = StepText(progress.ApplyChanges, LobbyTextKeys.StepApply);
            _fill.style.width = Length.Percent(Mathf.Clamp01(progress.Fraction) * 100f);
        }

        /// <summary>방 목록을 표시한다(SCR-010 상태별: 로딩/빈 목록/오류/성공).</summary>
        public void SetListState(LobbyListState state, IReadOnlyList<LobbyInfo> lobbies)
        {
            _list.Clear();
            switch (state)
            {
                case LobbyListState.Loading: _listState.text = _text.Get(LobbyTextKeys.ListLoading); break;
                case LobbyListState.Empty: _listState.text = _text.Get(LobbyTextKeys.ListEmpty); break;
                case LobbyListState.Error: _listState.text = UiGlyphs.Warning + " " + _text.Get(LobbyTextKeys.ListError); break;
                default: _listState.text = string.Empty; break;
            }
            if (state != LobbyListState.Loaded || lobbies == null)
            {
                return;
            }
            foreach (LobbyInfo lobby in lobbies)
            {
                LobbyInfo captured = lobby;
                var row = new Button { focusable = true };
                row.AddToClassList("list-row");
                row.text = _text.Get(LobbyTextKeys.RowFormat, lobby.GetMeta(LobbyMetaKeys.World), lobby.GetMeta(LobbyMetaKeys.Host), lobby.MemberCount, lobby.MaxMembers)
                           + "  [" + _text.Get(LobbyTextKeys.Join) + "]";
                row.clicked += () => JoinRequested?.Invoke(captured);
                _list.Add(row);
            }
        }

        private string StepText(StepStatus status, string key)
        {
            string glyph = status == StepStatus.Done ? UiGlyphs.Ok : status == StepStatus.Active ? UiGlyphs.Active : UiGlyphs.Pending;
            return glyph + " " + _text.Get(key);
        }

        private void Show(VisualElement panel)
        {
            _root.RemoveFromClassList(HiddenClass);
            _lobbyPanel.EnableInClassList(HiddenClass, panel != _lobbyPanel);
            _connectPanel.EnableInClassList(HiddenClass, panel != _connectPanel);
            _failPanel.EnableInClassList(HiddenClass, panel != _failPanel);
        }

        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException("DevLobby.uxml에 '" + name + "' (" + typeof(T).Name + ") 요소가 없습니다.");
            }
            return element;
        }
    }

    /// <summary>개발용 로비 화면 컴포넌트(UIDocument + DevLobby.uxml + PanelSettings가 붙어 있어야 한다).</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DevLobbyScreen : MonoBehaviour
    {
        [Tooltip("공통 USS(색 토큰, 글자 크기 변수). UI/Common/Base.uss")]
        [SerializeField] private StyleSheet _baseStyle;

        private DevLobbyView _view;

        public DevLobbyView View => _view;

        /// <summary>화면을 만든다(처음 한 번). 이후 표시·숨김은 View로 한다.</summary>
        public DevLobbyView Build(ILocalizedText text, UiTextScale scale = UiTextScale.Normal)
        {
            gameObject.SetActive(true);
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null || root.childCount == 0)
            {
                throw new InvalidOperationException("DevLobbyScreen의 UIDocument에 DevLobby.uxml이 연결되지 않았습니다.");
            }
            UiTheme.Apply(root, _baseStyle, scale);
            _view = new DevLobbyView(root, text);
            return _view;
        }
    }
}

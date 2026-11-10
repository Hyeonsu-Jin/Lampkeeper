// =============================================================
// 파일: GameFlow.cs
// 역할: M1 개발용 화면 흐름과 세션 생명 주기. 로비 화면 → (방 열기 / 목록 / 초대·친구 참가) → 참가 중 화면 → 게임(World 씬, HUD)
//       → 나가기 / 연결 끊김 → 로비 화면. UI 상태(Gameplay/Overlay)에 맞춰 입력 경로와 커서를 바꾼다.
// 레이어: infrastructure (Game.Bootstrap, 컴포지션 루트·씬 흐름)
// 관련: FR-001~004, FR-009, FR-011, NFR-018, NFR-039, I-07, DES-100, DES-091 / SDD 3.9.1, 3.9.2, 3.10.1, 3.10.7, STORYBOARD SCR-012, SCR-015
//
// 화면을 열어도(Overlay) 게임은 멈추지 않는다: Time.timeScale을 바꾸지 않고 서버 틱·복제는 계속 돈다.
// =============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using Game.Adapters.Presentation.Input;
using Game.Adapters.Presentation.Player;
using Game.Adapters.Presentation.UI.Common;
using Game.Adapters.Presentation.UI.Hud;
using Game.Adapters.Presentation.UI.Screens;
using Game.Adapters.Presentation.World;
using Game.Application.Boot;
using Game.Application.Config;
using Game.Application.Hud;
using Game.Application.Ports;
using Game.Application.Replication;
using Game.Application.Session;
using Game.Application.WorldSync;
using Game.Domain.Common;
using Game.Domain.Session;
using Game.Domain.Vitals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game.Bootstrap.Runtime
{
    /// <summary>화면 흐름 재료.</summary>
    public sealed class GameFlowDeps
    {
        public SessionRuntimeDeps Runtime;
        public ILocalizedText Text;
        public UiRoot UiRoot;
        public DevLobbyScreen LobbyScreen;
        public GameHud Hud;
        public GameMenu Menu;
        public InputActionAsset InputAsset;
        public MonoBehaviour CoroutineHost;
        public string WorldSceneName;
        public Camera MenuCamera;

        /// <summary>개발 빌드의 -autowalk: 입력 없이 자동으로 걷는다(화면 없는 다중 프로세스 시험).</summary>
        public bool AutoWalk;

        /// <summary>개발 빌드: 이 간격(초)마다 상태를 로그로 남긴다. 0이면 남기지 않는다.</summary>
        public float StatusLogSeconds;

        /// <summary>개발 빌드의 -resourcechanges N: 호스트가 월드를 만든 직후 자원 N개를 바뀐 상태로 만든다(0이면 하지 않음, NFR-016 시험 조건).</summary>
        public int DevResourceChanges;

        /// <summary>개발 빌드의 -testcreatures N: 호스트가 월드를 만든 직후 시험용 생물 N체를 만든다(0이면 만들지 않음, M1-11).</summary>
        public int DevTestCreatures;
    }

    /// <summary>개발용 화면 흐름.</summary>
    public sealed class GameFlow
    {
        private const string LogTag = "[Flow] ";

        /// <summary>개발 도구가 자원을 고르는 시드(같은 월드·개수면 늘 같은 결과).</summary>
        private const ulong DevResourceChangeSeed = 5000UL;

        private readonly GameFlowDeps _deps;
        private readonly UiState _uiState = new UiState();
        private DevLobbyScreen _lobby;
        private GameHud _hud;
        private GameMenu _menu;
        private GameInput _input;
        private SessionRuntime _runtime;
        private FirstPersonController _controller;
        private RemoteAvatarViews _remoteViews;
        private TerrainViewBuilder _terrain;
        private ResourceViewStreamer _resourceViews;
        private CreatureViews _creatureViews;
        private Game.Bootstrap.DevTools.CreatureInterestHud _interestHud;
        private ResourceHitInput _hitInput;
        private bool _hostStarting;
        private bool _inGame;
        private bool _worldLoading;
        private LobbyListState _listState = LobbyListState.Idle;
        private double _nextStatusLog;

        public GameFlow(GameFlowDeps deps)
        {
            _deps = deps ?? throw new ArgumentNullException(nameof(deps));
        }

        /// <summary>현재 세션 실행 구성(없으면 null). 시험·진단용.</summary>
        public SessionRuntime Runtime => _runtime;

        /// <summary>게임 중인지(조작 가능).</summary>
        public bool InGame => _inGame;

        /// <summary>화면을 만들고 로비를 보인다.</summary>
        public void Start()
        {
            _lobby = _deps.LobbyScreen.Build(_deps.Text);
            // [NFR-058] 출시 빌드 v{핵심}, 개발·에디터 v{전체}
            _lobby.SetVersion(BuildInfo.Version?.ToDisplayString());
            _hud = _deps.Hud.Build(_deps.Text, _deps.Runtime.Config.Balance.Session.MaxPlayers);
            _deps.Hud.SetVisible(false);
            _menu = _deps.Menu.Build(_deps.Text, _uiState);
            _input = new GameInput(_deps.InputAsset);
            // UI 입력 모듈과 닫기 라우터를 게임이 쓰는 같은 Input Actions 인스턴스에 연결한다(재설정한 바인딩이 메뉴에도 적용, FR-117).
            _deps.UiRoot.BindInput(_input.Asset);
            // 게임 밖(로비) 화면은 UI 동작 지도로 조작한다.
            _input.Apply(UiMode.Overlay);

            _lobby.HostRequested += OnHostRequested;
            _lobby.RefreshRequested += OnRefreshRequested;
            _lobby.InviteRequested += () =>
            {
                if (!TryOpenInvite())
                {
                    _lobby.SetStatus(_deps.Text.Get(HudTextKeys.InviteUnavailable));
                }
            };
            _lobby.JoinRequested += OnJoinListed;
            _lobby.CancelRequested += OnLeaveRequested;
            _lobby.FailureAcknowledged += () => _lobby.ShowLobby();
            _menu.LeaveRequested += OnLeaveRequested;
            _menu.InviteRequested += () =>
            {
                // 초대 창을 못 열면(Tugboat, 로비 없음, 오버레이 꺼짐) 아무 반응 없이 끝나지 않고 안내를 보인다.
                if (!TryOpenInvite())
                {
                    _menu.SetMenuNotice(_deps.Text.Get(HudTextKeys.InviteUnavailable));
                }
            };
            _uiState.ModeChanged += ApplyUiMode;

            // 친구 초대 수락·친구 목록 「게임 참가」
            if (_deps.Runtime.Lobby != null)
            {
                _deps.Runtime.Lobby.JoinRequested += OnLobbyJoinRequested;
            }
        }

        /// <summary>개발용 -host 옵션: 로비를 거치지 않고 바로 방을 연다(직접 전송).</summary>
        public void AutoHost() => OnHostRequested();

        /// <summary>개발용 -connect 옵션: 바로 접속한다(직접 전송).</summary>
        public void AutoConnect(NetworkEndpoint endpoint)
        {
            BeginClient();
            _runtime.StartClient(endpoint.Host, endpoint.Port, JoinPath.Friend);
        }

        /// <summary>매 프레임 부른다.</summary>
        public void Update()
        {
            _runtime?.Tick();
            if (_runtime != null && _lobby != null && _runtime.Session.State == ClientSessionState.Syncing)
            {
                _lobby.SetProgress(ConnectScreenModel.Compute(_runtime.Session.State, _runtime.Mirror.SnapshotChunksReceived, _runtime.Mirror.SnapshotChunksExpected));
            }

            if (!_inGame || _controller == null || _controller.Stamina == null)
            {
                return;
            }

            LogStatusIfDue();

            // [FR-044] 게임 입력 상태일 때만 공격 입력을 읽어 조준한 자원에 타격 요청을 보낸다(UI가 열려 있으면 보내지 않는다).
            if (_uiState.Mode == UiMode.Gameplay && _hitInput != null)
            {
                _hitInput.Tick(Time.unscaledDeltaTime);
            }

            _hud.Tick(_controller.Stamina.Ratio, _controller.Stamina.Ratio >= 1f, Time.unscaledDeltaTime, _runtime.Mirror);
            _hud.SetNetStatus(_deps.Text.Get(HudTextKeys.NetStatus, BuildStatus()));

            // Gameplay: Esc → 게임 메뉴(Overlay). Overlay 에서 Esc/Ⓑ 로 닫는 것은 UiInputRouter 가 메뉴의 OnCancel 로 전달한다.
            if (_uiState.Mode == UiMode.Gameplay && _input.Menu.WasPressedThisFrame())
            {
                _menu.OpenMenu();
            }
        }

        /// <summary>종료할 때 정리한다.</summary>
        public void Shutdown()
        {
            EndSession();
            _input?.Dispose();
        }

        // ---------- 방 열기 / 참가 ----------

        private void OnHostRequested()
        {
            if (_runtime != null || _hostStarting)
            {
                return;
            }
            // 월드 생성(Full 약 0.6초)은 한 프레임을 멈추므로, 먼저 문구를 그린 뒤 시작한다(로딩 표시).
            _hostStarting = true;
            _lobby.SetStatus(_deps.Text.Get(LobbyTextKeys.StatusGenerating));
            _lobby.SetHosting(true, false);
            _deps.CoroutineHost.StartCoroutine(StartHostRoutine());
        }

        private IEnumerator StartHostRoutine()
        {
            yield return null;
            yield return null; // 문구가 화면에 나온 다음 프레임에 무거운 생성을 한다
            if (!_hostStarting)
            {
                yield break; // 그 사이에 취소했다
            }
            _hostStarting = false;
            _lobby.SetStatus(_deps.Text.Get(LobbyTextKeys.StatusOpening));

            var info = new HostWorldInfo
            {
                WorldName = "M1-" + _deps.Runtime.Identity.LocalDisplayName,
                SeedText = _deps.Runtime.Config.Balance.WorldGenRules.BenchSeedText,
                Access = SessionAccess.FriendsOnly, // [M1] 참가 조건 「친구 전용」 고정
                GeneratorVersion = 0,
            };
            _deps.Runtime.DevTestCreatures = _deps.DevTestCreatures;
            _runtime = SessionRuntime.CreateHost(_deps.Runtime, info);
            WireSession(_runtime);
            _runtime.HostOpened += OnHostOpened;

            // [NFR-016 시험] 개발 옵션: 월드를 만든 직후, 참가자가 붙기 전에 자원 N개를 바뀐 상태로 만든다(스냅샷으로 모두에게 간다).
            if (_deps.DevResourceChanges > 0)
            {
                int applied = DevApplyResourceChanges(_deps.DevResourceChanges);
                Debug.Log(LogTag + "-resourcechanges: 자원 " + applied + "개를 바뀐 상태로 만들었습니다.");
            }
            _runtime.StartHost();
        }

        /// <summary>
        /// [개발 도구] 호스트에서만: 자원 count개를 쓰러짐·부서짐·타격 중 상태로 바꾼다. 바뀐 항목은 델타로 모든 참가자에게 간다.
        /// </summary>
        /// <returns>바꾼 개수. 호스트가 아니거나 월드가 없으면 -1</returns>
        public int DevApplyResourceChanges(int count)
        {
            if (_runtime == null || !_runtime.IsHost || _runtime.World == null || _runtime.World.State.Local == null)
            {
                return -1;
            }
            return ResourceChangeTool.ApplyChanges(_runtime.World.State.Resources, _runtime.World.State.Local, count, DevResourceChangeSeed);
        }

        /// <summary>
        /// [개발 도구] 호스트에서만: 시험용 생물 count체를 만든다. 참가자에게는 관심 범위 규칙으로 복제된다.
        /// </summary>
        /// <returns>만든 수. 호스트가 아니면 -1</returns>
        public int DevSpawnTestCreatures(int count) => _runtime != null ? _runtime.SpawnTestCreatures(count) : -1;

        private void OnHostOpened(LobbyError error)
        {
            if (error != LobbyError.None)
            {
                _lobby.SetStatus(_deps.Text.Get(LobbyTextKeys.StatusFailed));
                EndSession();
                _lobby.SetHosting(false, false);
                return;
            }
            _lobby.ShowConnecting();
        }

        private void OnRefreshRequested()
        {
            ILobbyService lobby = _deps.Runtime.Lobby;
            if (lobby == null || !lobby.IsAvailable)
            {
                _lobby.SetListState(LobbyListState.Error, null);
                return;
            }
            _listState = LobbyListState.Loading;
            _lobby.SetListState(LobbyListState.Loading, null);
            lobby.RequestList((error, list) =>
            {
                if (error != LobbyError.None)
                {
                    _lobby.SetListState(LobbyListState.Error, null);
                }
                else if (list.Count == 0)
                {
                    _lobby.SetListState(LobbyListState.Empty, null);
                }
                else
                {
                    _lobby.SetListState(LobbyListState.Loaded, list);
                }
            });
        }

        private void OnJoinListed(LobbyInfo lobby) => JoinLobby(lobby.Id);

        private void OnLobbyJoinRequested(LobbyId id)
        {
            if (_runtime == null)
            {
                JoinLobby(id);
            }
        }

        /// <summary>로비에 들어간 뒤 로비 주인(호스트)의 SteamID로 전송 연결을 시작한다.</summary>
        private void JoinLobby(LobbyId id)
        {
            ILobbyService lobby = _deps.Runtime.Lobby;
            if (_runtime != null || lobby == null)
            {
                return;
            }
            BeginClient();
            lobby.Join(id, result =>
            {
                if (!result.Success || result.Lobby == null)
                {
                    Debug.LogWarning(LogTag + "로비 참가 실패: " + result.Error);
                    EndSession();
                    _lobby.ShowFailure(result.Error == LobbyError.Full ? "join.fail.full" : "join.fail.noresponse");
                    return;
                }
                // [NFR-009] 이 게임의 로비가 아니면(App ID 480 공유) 바로 나간다.
                if (result.Lobby.GetMeta(LobbyMetaKeys.Game) != LobbyMetaKeys.GameIdValue)
                {
                    lobby.Leave();
                    EndSession();
                    _lobby.ShowFailure("join.fail.noresponse");
                    return;
                }
                _runtime.StartClient(result.Lobby.Owner.Value.ToString(), 0, JoinPath.Friend);
            });
        }

        private void BeginClient()
        {
            _runtime = SessionRuntime.CreateClient(_deps.Runtime);
            WireSession(_runtime);
            _lobby.ShowConnecting();
        }

        // ---------- 세션 상태 → 화면 ----------

        private void WireSession(SessionRuntime runtime)
        {
            runtime.Session.StateChanged += OnSessionState;
        }

        private void OnSessionState(ClientSessionState state)
        {
            ClientSession session = _runtime?.Session;
            if (session == null)
            {
                return;
            }
            switch (state)
            {
                case ClientSessionState.Connecting:
                case ClientSessionState.Generating:
                case ClientSessionState.Syncing:
                    _lobby.SetProgress(ConnectScreenModel.Compute(state));
                    break;

                case ClientSessionState.InGame:
                    _lobby.SetProgress(ConnectScreenModel.Compute(state));
                    EnterGame();
                    break;

                case ClientSessionState.Ended:
                {
                    // [NFR-003] 이유에 맞는 「무엇이 + 어떻게」 문장을 보인다.
                    string prefix = ConnectScreenModel.FailureKeyPrefix(session.Failure, session.RejectReason);
                    LeaveGameView();
                    EndSession();
                    _lobby.SetHosting(false, false);
                    _lobby.ShowFailure(prefix);
                    break;
                }
            }
        }

        /// <summary>Steam 초대 창을 연다. 열지 못하면 false.</summary>
        private bool TryOpenInvite()
        {
            ILobbyService lobby = _deps.Runtime.Lobby;
            return lobby != null && lobby.OpenInviteDialog();
        }

        private void OnLeaveRequested()
        {
            _hostStarting = false;
            // 호스트의 로비는 세션 종료와 함께 닫는다(참가자는 로비에서 퇴장).
            _deps.Runtime.Lobby?.Leave();
            LeaveGameView();
            EndSession();
            _lobby.SetHosting(false, false);
            _lobby.SetStatus(_deps.Text.Get(LobbyTextKeys.StatusReady));
            _lobby.ShowLobby();
        }

        /// <summary>세션 실행 구성을 정리한다.</summary>
        private void EndSession()
        {
            SessionRuntime runtime = _runtime;
            _runtime = null;
            runtime?.Dispose();
        }

        // ---------- 게임 진입 / 이탈 ----------

        private void EnterGame()
        {
            if (_inGame || _worldLoading)
            {
                return;
            }
            _worldLoading = true;
            _deps.CoroutineHost.StartCoroutine(LoadWorldRoutine());
        }

        private IEnumerator LoadWorldRoutine()
        {
            // 씬은 Additive로 불러 Boot 씬(조립 루트)을 그대로 둔다.
            AsyncOperation op = SceneManager.LoadSceneAsync(_deps.WorldSceneName, LoadSceneMode.Additive);
            while (op != null && !op.isDone)
            {
                yield return null;
            }
            _worldLoading = false;
            if (_runtime == null)
            {
                yield break; // 불러오는 중에 나갔다
            }

            _controller = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            _remoteViews = UnityEngine.Object.FindFirstObjectByType<RemoteAvatarViews>();
            if (_controller == null)
            {
                Debug.LogError(LogTag + "World 씬에 FirstPersonController가 없습니다.");
                OnLeaveRequested();
                yield break;
            }

            BalanceValues balance = _deps.Runtime.Config.Balance;
            var settings = new FirstPersonSettings
            {
                WalkSpeed = balance.Movement.WalkSpeedMps,
                RunSpeed = balance.Movement.RunSpeedMps,
                FieldOfViewDegrees = balance.Session.FovDefaultDegrees,
            };
            StaminaSettings s = balance.Stamina;
            _controller.Initialize(_input, _runtime.Pose, settings, new StaminaParams(s.Max, s.RunCostPerSecond, s.RegenDelaySeconds, s.RegenPerSecond));
            SetUpWorldView(balance);
            _controller.DevAutoWalk = _deps.AutoWalk;
            _remoteViews?.Initialize(_runtime.Mirror, _runtime.Session.LocalPlayer);
            _creatureViews = UnityEngine.Object.FindFirstObjectByType<CreatureViews>();
            if (_creatureViews != null)
            {
                _creatureViews.Initialize(_runtime.Mirror.Creatures);
            }
            else
            {
                Debug.LogWarning(LogTag + "World 씬에 CreatureViews가 없어 생물이 보이지 않습니다.");
            }
            if (BuildFlags.IsDevelopment)
            {
                // [개발 도구] F1 관심 범위 표시(기본 꺼짐). 호스트일 때만 서버 전체 생물 수를 함께 보인다.
                _interestHud = Game.Bootstrap.DevTools.CreatureInterestHud.Create(_runtime.Mirror.Creatures, _runtime.Pose,
                    CreatureInterestSettings.FromConfig(_deps.Runtime.Config.Network), _runtime.IsHost ? _runtime.World : null);
            }

            if (_deps.MenuCamera != null)
            {
                _deps.MenuCamera.enabled = false; // 게임 중에는 World 씬의 플레이어 카메라가 그린다
            }
            _lobby.HideAll();
            _deps.Hud.SetVisible(true);
            _inGame = true;
            _uiState.CloseAll();
            ApplyUiMode(UiMode.Gameplay);
        }

        /// <summary>
        /// 지형(Terrain 타일)과 자원 표시를 준비하고 공격 입력을 잇는다. 지형은 플레이어가 서기 전에 충돌체까지 만들어 둔다
        /// (스폰하자마자 땅 밑으로 떨어지지 않게).
        /// </summary>
        private void SetUpWorldView(BalanceValues balance)
        {
            LocalWorld local = _runtime.Mirror.Local;
            _terrain = UnityEngine.Object.FindFirstObjectByType<TerrainViewBuilder>();
            _resourceViews = UnityEngine.Object.FindFirstObjectByType<ResourceViewStreamer>();
            if (local == null || _terrain == null || _resourceViews == null)
            {
                Debug.LogError(LogTag + "월드 표시 준비 실패: 월드=" + (local != null) + " 지형=" + (_terrain != null) + " 자원 뷰=" + (_resourceViews != null));
                return;
            }
            _terrain.BuildFrom(local);
            Camera eye = _controller.GetComponentInChildren<Camera>();
            Transform viewer = eye != null ? eye.transform : _controller.transform;
            _resourceViews.Initialize(_runtime.Mirror, viewer);
            if (eye != null)
            {
                SessionRuntime runtime = _runtime;
                _hitInput = new ResourceHitInput(_input, _resourceViews, eye.transform, balance.Movement.InteractionDistanceMeters,
                    (index, slot) => runtime.Session.TrySendHitResource(index, slot));
            }
        }

        private void LeaveGameView()
        {
            if (!_inGame && !_worldLoading)
            {
                return;
            }
            _inGame = false;
            _uiState.CloseAll();
            _input.Apply(UiMode.Overlay); // 로비 화면은 UI 동작 지도로 조작한다
            if (_deps.MenuCamera != null)
            {
                _deps.MenuCamera.enabled = true;
            }
            _deps.Hud.SetVisible(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Scene world = SceneManager.GetSceneByName(_deps.WorldSceneName);
            if (world.isLoaded)
            {
                SceneManager.UnloadSceneAsync(world);
            }
            _hitInput = null;
            if (_terrain != null)
            {
                _terrain.Clear();
            }
            _terrain = null;
            _resourceViews = null;
            _controller = null;
            _remoteViews = null;
            _creatureViews = null;
            if (_interestHud != null)
            {
                UnityEngine.Object.Destroy(_interestHud.gameObject);
            }
            _interestHud = null;
        }

        /// <summary>UI 상태에 맞게 로컬 입력 경로와 커서를 바꾼다. 서버·복제·시각은 건드리지 않는다. [I-07]</summary>
        private void ApplyUiMode(UiMode mode)
        {
            if (!_inGame)
            {
                return;
            }
            _input.Apply(mode);
            if (mode == UiMode.Gameplay)
            {
                _deps.UiRoot.ClearSelection(); // 게임으로 돌아갈 때 UI 선택을 비운다
            }
            if (_controller != null)
            {
                _controller.InputEnabled = mode == UiMode.Gameplay;
            }
            Cursor.lockState = mode == UiMode.Gameplay ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = mode == UiMode.Overlay;
        }

        /// <summary>개발용: 접속자·원격 아바타·호스트 서버 쪽 위치를 주기적으로 로그에 남겨 다중 프로세스 시험을 확인한다.</summary>
        private void LogStatusIfDue()
        {
            if (_deps.StatusLogSeconds <= 0f || Time.realtimeSinceStartupAsDouble < _nextStatusLog || _runtime == null)
            {
                return;
            }
            _nextStatusLog = Time.realtimeSinceStartupAsDouble + _deps.StatusLogSeconds;
            Vector3 p = _controller.transform.position;
            string text = BuildStatus() + " roster=" + _runtime.Mirror.Roster.Count
                          + " pos=(" + p.x.ToString("F1") + "," + p.z.ToString("F1") + ") stamina=" + _controller.Stamina.Current.ToString("F0")
                          + (_controller.IsRunning ? " RUN" : " walk");
            if (_runtime.World != null)
            {
                var servers = new List<string>();
                foreach (Game.Domain.Players.PlayerCharacter c in _runtime.World.State.Players)
                {
                    servers.Add(c.Id.Value + "@(" + c.Position.X.ToString("F1") + "," + c.Position.Z.ToString("F1") + ") st=" + c.Stamina.Current.ToString("F0"));
                }
                text += " server[" + string.Join(" ", servers) + "]";
            }
            Debug.Log(LogTag + "상태 " + text);
        }

        private string BuildStatus()
        {
            ClientSession session = _runtime?.Session;
            string role = _runtime != null && _runtime.IsHost ? "HOST" : "CLIENT";
            int remote = _runtime?.Mirror.RemoteAvatars.Count ?? 0;
            return role + " slot=" + (session?.LocalSlot ?? -1) + " remote=" + remote + " " + (session?.LocalPlayer ?? PlayerId.None);
        }
    }
}

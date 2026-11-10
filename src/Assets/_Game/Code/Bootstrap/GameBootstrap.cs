// =============================================================
// 파일: GameBootstrap.cs
// 역할: Boot 씬의 진입점(컴포지션 루트). 개발 옵션을 읽고, 설정을 로드하고, 텍스트 표를 준비하고,
//       Steam 확인(CheckPlatformReady) 결과에 따라 SCR-001을 띄우거나 다음 단계로 보낸다.
// 레이어: infrastructure (Game.Bootstrap)
// 관련: NFR-006(Steam 실행 필수), NFR-033, NFR-034, FR-006, DES-100, DES-103 / SDD 3.7.4, STORYBOARD SCR-001
//
// M1-02 범위: 부트 판정까지. 메인 메뉴(M1-08/M2), 네트워크 세션(M1-07)은 아직 없어서
// "ContinueToMainMenu"이면 로그만 남기고 대기한다.
// =============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using Game.Adapters.Presentation.Localization;
using Game.Adapters.Presentation.UI.Common;
using Game.Adapters.Presentation.UI.Hud;
using Game.Adapters.Presentation.UI.Screens;
using Game.Application.Boot;
using Game.Application.Config;
using Game.Application.Perf;
using Game.Application.Localization;
using Game.Application.Ports;
using Game.Bootstrap.DevTools;
using Game.Bootstrap.Runtime;
using Game.Domain.Common;
using Game.Infra.Config;
using Game.Infra.Steam;
using Game.Infra.Transport;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Bootstrap
{
    /// <summary>Boot 씬 진입점.</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const string LogTag = "[Boot] ";

        /// <summary>개발 빌드에서 게임 상태를 로그로 남기는 간격(초).</summary>
        private const float DevStatusLogSeconds = 5f;

        /// <summary>Bench 장면 이름(개발 빌드의 빌드 설정에만 들어간다).</summary>
        public const string BenchSceneName = "Bench";

        [Header("설정(NFR-033)")]
        [SerializeField] private GameDataConfig _dataConfig;

        [Header("임시 텍스트 표(M2-01에서 String Table로 교체)")]
        [SerializeField] private TextAsset _koreanStrings;
        [SerializeField] private TextAsset _englishStrings;

        [Header("화면(uGUI 프리팹 인스턴스)")]
        [SerializeField] private UiRoot _uiRoot;
        [SerializeField] private SteamRequiredScreen _steamRequiredScreen;
        [SerializeField] private DevLobbyScreen _lobbyScreen;
        [SerializeField] private GameHud _hud;
        [SerializeField] private GameMenu _gameMenu;

        [Header("네트워크·입력(M1-07, M1-08)")]
        [Tooltip("FishNet NetworkManager. SpawnablePrefabs(DefaultPrefabObjects)가 지정되어 있어야 한다.")]
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private NetworkObject _avatarPrefab;
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private string _worldSceneName = "World";
        [Tooltip("메뉴(로비) 화면 뒤를 그리는 카메라. 게임 중에는 World 씬의 플레이어 카메라가 대신한다.")]
        [SerializeField] private Camera _menuCamera;

        private IPlatformServices _platform;
        private CheckPlatformReady _checkPlatformReady;
        private LaunchOptions _options;
        private ILocalizedText _text;
        private bool _bootSmoke;
        private GraphicsBootstrapper _graphics;
        private GameFlow _flow;
        private ILobbyService _lobbyService;
        private SteamLobbySmoke _lobbySmoke;
        private double _autoQuitAt;

        /// <summary>검증을 마친 설정. 로드에 실패하면 null.</summary>
        public IConfigProvider Config { get; private set; }

        /// <summary>가장 최근 부트 판정.</summary>
        public BootDecision Decision { get; private set; }

        private void Start()
        {
            IReadOnlyList<string> args = DevCommandLine.GetArgs();

            // [NFR-053] 결정론 비교 도구(-genhash, -genhash-compare)는 개발 빌드에서만 동작하고, 실행하면 부트를 멈추고 끝낸다.
            if (BuildFlags.IsDevelopment && GenHashCommand.TryRun(args, out int genHashExitCode))
            {
                QuitGame(genHashExitCode);
                return;
            }

            // [FR-158, FR-163] 파일 로그는 모든 빌드에서, 성능 측정·개발용 표시는 개발 빌드에서 켠다.
            PerfOptions perfOptions = DevDiagnostics.Install(args);

            // [NFR-021] -bench 가 있으면 부트 판정 없이 Bench 장면으로 간다(개발 빌드 전용).
            if (BuildFlags.IsDevelopment && perfOptions.Bench.HasValue)
            {
                Debug.Log(LogTag + "-bench " + perfOptions.Bench.Value + ": Bench 장면으로 이동합니다.");
                SceneManager.LoadScene(BenchSceneName);
                return;
            }

            _options = ParseOptions(args);
            if (BuildFlags.IsDevelopment && DevCommandLine.TryGetValue(args, DevCommandLine.AutoQuitFlag, out string quitText)
                && double.TryParse(quitText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double quitSeconds) && quitSeconds > 0d)
            {
                _autoQuitAt = Time.realtimeSinceStartupAsDouble + quitSeconds;
            }
            _bootSmoke = BuildFlags.IsDevelopment && DevCommandLine.HasFlag(args, DevCommandLine.BootSmokeFlag);

            // [NFR-058] 게임 버전을 로그 첫 줄들에 남긴다(전체 문자열). 해석하지 못하면 설정 오류로 끝낸다.
            if (BuildFlags.IsDevelopment && DevCommandLine.TryGetValue(args, DevCommandLine.GameVersionFlag, out string versionOverride))
            {
                bool applied = BuildInfo.TryOverrideForDevelopment(versionOverride);
                Debug.LogWarning(LogTag + "-gameversion " + versionOverride + (applied ? " 적용(개발 시험용)" : " 해석 실패, 무시"));
            }
            GameVersion gameVersion = BuildInfo.Version;
            if (gameVersion == null)
            {
                Debug.LogError(LogTag + "게임 버전을 해석하지 못했습니다: '" + UnityEngine.Application.version + "' (Project Settings → Player → Version이 MAJOR.MINOR.PATCH 형식인지 확인).");
                FinishSmokeIfRequested(DevCommandLine.ExitConfigError);
                return;
            }
            Debug.Log(LogTag + "게임 버전=" + gameVersion.Full);

            if (!TryLoadConfig())
            {
                FinishSmokeIfRequested(DevCommandLine.ExitConfigError);
                return;
            }

            // [FR-120, FR-121] 옵션 화면(M2-01)이 생기기 전에는 기준 프리셋 「높음」 + 업스케일링 기본값으로 시작한다.
            _graphics = GraphicsBootstrapper.TryCreate(_dataConfig.Graphics);
            _graphics?.ApplyStartupSettings(null, perfOptions.Upscale);

            _text = CreateLocalizedText();
            // 화면 글자를 채우기 전에 UI 에 글자 공급자를 알린다(SCR-001 도 이 값을 쓴다).
            if (_uiRoot != null)
            {
                _uiRoot.Initialize(_text);
            }
            _platform = new SteamPlatformService(BuildFlags.IsDevelopment);
            _checkPlatformReady = new CheckPlatformReady(_platform);

            EvaluateBoot();
        }

        private void Update()
        {
            // Steam 콜백은 메인 스레드에서 매 프레임 처리한다.
            _platform?.Tick();
            _flow?.Update();
            _lobbySmoke?.Tick();

            // 개발 빌드의 -autoquit N: N초 뒤 스스로 끝낸다(여러 프로세스 시험 정리용).
            if (_autoQuitAt > 0d && Time.realtimeSinceStartupAsDouble >= _autoQuitAt)
            {
                _autoQuitAt = 0d;
                Debug.Log(LogTag + "-autoquit: 종료합니다.");
                QuitGame(0);
            }
        }

        private void OnDestroy()
        {
            _flow?.Shutdown();
            (_lobbyService as IDisposable)?.Dispose();
            _platform?.Shutdown();
        }

        /// <summary>-resourcechanges N 값을 읽는다. 없거나 양의 정수가 아니면 0(하지 않음), 값 오류는 경고 로그로 남긴다.</summary>
        private static int ParseResourceChanges()
        {
            IReadOnlyList<string> args = DevCommandLine.GetArgs();
            if (!DevCommandLine.TryGetValue(args, DevCommandLine.ResourceChangesFlag, out string text))
            {
                return 0;
            }
            if (!int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int count) || count <= 0)
            {
                Debug.LogWarning(LogTag + "-resourcechanges 값이 양의 정수가 아닙니다: " + text);
                return 0;
            }
            return count;
        }

        /// <summary>
        /// [개발 도구] 에디터 메뉴가 부른다: 호스트로 게임 중일 때 자원 count개를 바뀐 상태로 만든다.
        /// </summary>
        /// <returns>바꾼 개수. 개발 빌드가 아니거나 호스트가 아니면 -1</returns>
        public int DevApplyResourceChanges(int count) => BuildFlags.IsDevelopment && _flow != null ? _flow.DevApplyResourceChanges(count) : -1;

        /// <summary>개발 옵션을 읽는다. 출시 빌드는 개발 옵션을 무시하고 경고만 남긴다.</summary>
        private static LaunchOptions ParseOptions(IReadOnlyList<string> args)
        {
            LaunchOptions options = LaunchOptionsParser.Parse(args);
            foreach (string error in options.Errors)
            {
                Debug.LogWarning(LogTag + "실행 옵션 오류: " + error);
            }
            if (!BuildFlags.IsDevelopment && options.HasAnyDevOption)
            {
                Debug.LogWarning(LogTag + "출시 빌드에서는 개발용 실행 옵션을 무시합니다.");
            }
            return options;
        }

        /// <summary>설정을 로드·검증한다. 실패하면 오류를 로그로 남기고 false.</summary>
        private bool TryLoadConfig()
        {
            if (_dataConfig == null)
            {
                Debug.LogError(LogTag + "GameDataConfig가 연결되지 않았습니다(Boot 씬 설정 확인).");
                return false;
            }
            try
            {
                ConfigProvider provider = ConfigLoader.Load(_dataConfig);
                Config = provider;
                foreach (ConfigIssue warning in provider.Warnings)
                {
                    Debug.LogWarning(LogTag + "설정 경고 " + warning);
                }
                Debug.Log(LogTag + "설정 로드 완료. ContentHash=" + provider.ContentHash + ", 콘텐츠 " + provider.Content.Count + "개");
                return true;
            }
            catch (ConfigLoadException ex)
            {
                // 설정이 틀리면 게임을 시작하지 않는다. 원인(경로와 이유)을 모두 남긴다.
                Debug.LogError(LogTag + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 텍스트 표를 만든다. Steam 확인 전이라 Steam 언어를 알 수 없으므로 운영체제 언어로 정한다.
        /// 첫 실행 언어(Steam 언어 기준, C-48)는 M2-01 옵션 작업에서 Steam 초기화 뒤에 다시 정한다.
        /// </summary>
        private ILocalizedText CreateLocalizedText()
        {
            GameLanguage language = LanguageRules.FromSystemLanguageName(UnityEngine.Application.systemLanguage.ToString());
            TextTable korean = ParseTable(_koreanStrings, "ko");
            TextTable english = ParseTable(_englishStrings, "en");
            return language == GameLanguage.Korean
                ? new TableLocalizedText(korean, english)
                : new TableLocalizedText(english, korean);
        }

        private static TextTable ParseTable(TextAsset asset, string languageCode)
        {
            if (asset == null)
            {
                Debug.LogError(LogTag + "텍스트 표(" + languageCode + ")가 연결되지 않았습니다.");
                return TextTable.Parse(string.Empty);
            }
            var errors = new List<string>();
            TextTable table = TextTable.Parse(asset.text, errors);
            foreach (string error in errors)
            {
                Debug.LogWarning(LogTag + "텍스트 표(" + languageCode + ") " + error);
            }
            return table;
        }

        /// <summary>부트 판정을 한 번 하고 결과에 따라 화면을 정한다.</summary>
        private void EvaluateBoot()
        {
            Decision = _checkPlatformReady.Execute(_options, BuildFlags.IsDevelopment);
            Debug.Log(LogTag + "판정: 경로=" + Decision.Route + ", 전송=" + Decision.Transport
                      + ", 개발용신원=" + Decision.UsesDevIdentity + ", 실패=" + Decision.Failure
                      + (string.IsNullOrEmpty(Decision.Detail) ? string.Empty : " (" + Decision.Detail + ")"));

            if (Decision.Route == BootRoute.ShowSteamRequired)
            {
                ShowSteamRequired();
                FinishSmokeIfRequested(DevCommandLine.ExitSteamRequired);
                return;
            }

            IPlatformIdentity identity = Decision.Identity;
            Debug.Log(LogTag + "준비 완료: 플레이어 " + identity.LocalPlayerId + " (" + identity.LocalDisplayName + ").");
            if (BuildFlags.IsDevelopment && !Decision.UsesDevIdentity && DevCommandLine.HasFlag(DevCommandLine.GetArgs(), DevCommandLine.SteamLobbyTestFlag))
            {
                RunSteamLobbySmoke();
                return;
            }
            if (!_bootSmoke)
            {
                StartGameFlow();
            }
            FinishSmokeIfRequested(DevCommandLine.ExitContinue);
        }

        /// <summary>-steamlobbytest: 실제 Steam 로비 점검을 돌리고 결과를 종료 코드로 낸다.</summary>
        private void RunSteamLobbySmoke()
        {
            _lobbyService = new SteamLobbyService();
            _lobbySmoke = new SteamLobbySmoke(_lobbyService, Decision.Identity, code => QuitGame(code));
            _lobbySmoke.Start();
        }

        /// <summary>
        /// 부트 판정이 끝나 플랫폼이 준비됐을 때: 전송을 고르고, 로비·접속·HUD 화면 흐름(GameFlow)을 시작한다. 한 번만 시작한다.
        /// </summary>
        private void StartGameFlow()
        {
            if (_flow != null)
            {
                return;
            }
            if (_networkManager == null || _uiRoot == null || _lobbyScreen == null || _hud == null || _gameMenu == null || _inputActions == null || _avatarPrefab == null)
            {
                Debug.LogError(LogTag + "네트워크·화면 연결이 비어 있어 게임 흐름을 시작하지 못했습니다(Boot 씬 설정 확인).");
                return;
            }

            // [FR-006] 전송 선택(출시 빌드는 Steam 고정)과 네트워크 튜닝(틱, 연결 끊김 판정 시간, 연결 수 상한).
            var selector = new TransportSelector(_networkManager, BuildFlags.IsDevelopment);
            SelectedTransport selected = selector.Apply(Decision.Transport);

            // Boot 씬의 NetworkManager는 꺼진 채로 두었다가, 전송을 정한 뒤에 깨운다(FishNet이 깨어날 때 그 전송을 초기화한다).
            _networkManager.gameObject.SetActive(true);
            selector.ApplyTuning(Config.Network, Config.Balance.Session.DisconnectTimeoutSeconds, selected);
            selector.ApplyConnectionLimit(Config.Balance.Session.MaxPlayers, selected);

            // Steam 로비는 Steam 전송이고 Steam을 실제로 초기화한 경우에만 쓴다(개발용 Tugboat 시험은 로비 없음).
            if (Decision.Transport == TransportKind.Steam && !Decision.UsesDevIdentity)
            {
                _lobbyService = new SteamLobbyService();
            }

            var deps = new GameFlowDeps
            {
                Runtime = new SessionRuntimeDeps
                {
                    Network = _networkManager,
                    Transport = selected.Kind,
                    Config = Config,
                    Identity = Decision.Identity,
                    Lobby = _lobbyService,
                    Clock = UnityClock.Instance,
                    Log = UnityLog.Instance,
                    Perf = DevDiagnostics.Probe,
                    AvatarPrefab = _avatarPrefab,
                    IsDevelopmentBuild = BuildFlags.IsDevelopment,
                },
                Text = _text,
                UiRoot = _uiRoot,
                LobbyScreen = _lobbyScreen,
                Hud = _hud,
                Menu = _gameMenu,
                InputAsset = _inputActions,
                CoroutineHost = this,
                WorldSceneName = _worldSceneName,
                MenuCamera = _menuCamera,
                AutoWalk = BuildFlags.IsDevelopment && DevCommandLine.HasFlag(DevCommandLine.GetArgs(), DevCommandLine.AutoWalkFlag),
                StatusLogSeconds = BuildFlags.IsDevelopment ? DevStatusLogSeconds : 0f,
                DevResourceChanges = BuildFlags.IsDevelopment ? ParseResourceChanges() : 0,
            };
            _flow = new GameFlow(deps);
            _flow.Start();

            // 개발용 옵션(개발 빌드만): -host 로 바로 방 열기, -connect 로 바로 접속.
            if (BuildFlags.IsDevelopment)
            {
                if (_options.Host)
                {
                    _flow.AutoHost();
                }
                else if (_options.Connect.HasValue)
                {
                    _flow.AutoConnect(_options.Connect.Value);
                }
            }
        }

        /// <summary>SCR-001을 띄운다. 이 화면 뒤의 메인 메뉴·옵션은 열 수 없다(오프라인 플레이 없음).</summary>
        private void ShowSteamRequired()
        {
            if (_steamRequiredScreen == null)
            {
                Debug.LogError(LogTag + "SteamRequiredScreen이 연결되지 않아 SCR-001을 표시할 수 없습니다.");
                return;
            }
            _steamRequiredScreen.RetryRequested -= OnRetryRequested;
            _steamRequiredScreen.QuitRequested -= OnQuitRequested;
            _steamRequiredScreen.RetryRequested += OnRetryRequested;
            _steamRequiredScreen.QuitRequested += OnQuitRequested;
            _steamRequiredScreen.Show();
        }

        /// <summary>③ [다시 확인]: 한 프레임 "확인 중"을 보인 뒤 다시 판정한다.</summary>
        private void OnRetryRequested()
        {
            StartCoroutine(RetryRoutine());
        }

        private IEnumerator RetryRoutine()
        {
            _steamRequiredScreen.ShowChecking();
            yield return null;

            Decision = _checkPlatformReady.Execute(_options, BuildFlags.IsDevelopment);
            Debug.Log(LogTag + "재확인: 경로=" + Decision.Route + ", 실패=" + Decision.Failure);
            if (Decision.Route == BootRoute.ContinueToMainMenu)
            {
                _steamRequiredScreen.Hide();
                Debug.Log(LogTag + "준비 완료: 플레이어 " + Decision.Identity.LocalPlayerId + " (" + Decision.Identity.LocalDisplayName + ").");
                StartGameFlow();
            }
            else
            {
                _steamRequiredScreen.ShowRetryFailed();
            }
        }

        /// <summary>⑤ [종료]: 즉시 종료.</summary>
        private void OnQuitRequested()
        {
            QuitGame(0);
        }

        /// <summary>-bootsmoke 일 때 판정 결과를 종료 코드로 내고 끝낸다(개발 빌드 전용).</summary>
        private void FinishSmokeIfRequested(int exitCode)
        {
            if (_bootSmoke)
            {
                Debug.Log(LogTag + "bootsmoke 종료 코드=" + exitCode);
                QuitGame(exitCode);
            }
        }

        private static void QuitGame(int exitCode)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit(exitCode);
#endif
        }
    }
}

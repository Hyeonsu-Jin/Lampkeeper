// =============================================================
// 파일: SessionRuntime.cs
// 역할: 세션 하나(호스트 또는 참가자)의 실행 구성을 조립하는 컴포지션 루트. 서버 월드·게이트웨이·복제·참가 절차(호스트)와
//       클라이언트 세션·복제 수신·월드 사본(참가자와 호스트 자신)을 만들어 서로 잇고, 끝나면 모두 정리한다.
// 레이어: infrastructure (Game.Bootstrap, 컴포지션 루트)
// 관련: FR-001~005, FR-007~009, FR-011, NFR-031, NFR-032, DES-100, DES-054, DES-072, DES-081~085, DES-121 / SDD 3.2.2, 3.9.1, 3.9.2, D-17, D-19
//
// 호스트 자신의 플레이어도 참가자와 같은 경로(서버에 연결 하나, JoinRequest부터)로 들어온다(D-19).
// 서버 월드는 로컬 플레이어를 모른다(NFR-032).
// =============================================================
using System;
using FishNet.Managing;
using FishNet.Object;
using Game.Adapters.Net.Avatar;
using Game.Adapters.Net.Gateway;
using Game.Adapters.Net.Join;
using Game.Adapters.Net.Replication;
using Game.Adapters.Net.Transport;
using Game.Application.Boot;
using Game.Application.Commands;
using Game.Application.Config;
using Game.Application.Gathering;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Application.Replication;
using Game.Application.Server;
using Game.Application.Session;
using Game.Application.WorldSync;
using Game.Domain.Common;
using Game.Domain.Session;
using Game.WorldGen.Common;

namespace Game.Bootstrap.Runtime
{
    /// <summary>세션 실행 구성 재료(부트에서 한 번 만든 것들).</summary>
    public sealed class SessionRuntimeDeps
    {
        public NetworkManager Network;
        public TransportKind Transport;
        public IConfigProvider Config;
        public IPlatformIdentity Identity;
        public ILobbyService Lobby;
        public IClock Clock;
        public ILog Log;
        public IPerfProbe Perf;
        public NetworkObject AvatarPrefab;
        public bool IsDevelopmentBuild;

        /// <summary>월드 생성 규모. 기본 Full(자원 약 2만 개). Small은 시험·벤치용이며 호스트와 참가자가 같아야 한다.</summary>
        public WorldScale WorldScale = WorldScale.Full;

        /// <summary>월드 캐시. 같은 PC의 호스트 서버와 호스트 클라이언트가 월드를 한 번만 만들게 한다. null이면 세션이 새로 만든다.</summary>
        public LocalWorldProvider Worlds;
    }

    /// <summary>세션 실행 구성. 세션마다 새로 만들고 <see cref="Dispose"/>로 정리한다.</summary>
    public sealed class SessionRuntime : IDisposable
    {
        private const string LogCategory = "session";

        /// <summary>참가자가 월드를 만드는 동안 호스트가 더 기다려 주는 시간(초). SDD 3.12.6 월드 생성 예산 25초 + 여유.</summary>
        private const double WorldGenerationAllowanceSeconds = 30d;

        private readonly SessionRuntimeDeps _deps;
        private readonly bool _isHost;
        private readonly LocalWorldProvider _worlds;
        private FishNetServer _server;
        private ServerCommandGateway _gateway;
        private AvatarSpawner _spawner;
        private bool _disposed;
        private bool _pendingHostConnect;

        private SessionRuntime(SessionRuntimeDeps deps, bool isHost)
        {
            _deps = deps;
            _isHost = isHost;

            Pose = new LocalPlayerPoseBuffer();
            Mirror = new ClientWorldMirror();
            ClientTransport = new FishNetClient(deps.Network, deps.Log);

            var info = new ClientHandshakeInfo
            {
                ProtocolVersion = ProtocolConstants.ProtocolVersion,
                GameVersion = BuildInfo.Version.Full,
                ContentHash = deps.Config.ContentHash.Value,
                // 개발 전송(Tugboat)에서만 쓰는 값. Steam 전송에서는 호스트가 무시한다(D-14).
                DevPlayerId = deps.Transport == TransportKind.Tugboat ? deps.Identity.LocalPlayerId.Value : 0UL,
                SupportedGeneratorVersions = new ushort[] { 0 },
            };
            Session = new ClientSession(ClientTransport, Mirror, Pose, info, deps.Clock, deps.Log, deps.Config.Network.JoinTimeoutSeconds);
            Receiver = new ClientReplicationReceiver(ClientTransport, Mirror, deps.Log);

            // [FR-009, FR-169] 허가를 받으면 시드로 월드를 로컬에서 만들고(백그라운드), 요약값이 같으면 사본에 연결한다.
            // 호스트 PC에서는 호스트 서버가 이미 만든 월드를 캐시에서 바로 받는다.
            _worlds = deps.Worlds ?? new LocalWorldProvider();
            WorldScale scale = deps.WorldScale;
            LocalWorldProvider worlds = _worlds;
            Session.PrepareInBackground = true;
            Session.PrepareWorld = accepted => worlds.GetOrGenerate(accepted.Seed, accepted.GeneratorVersion, scale).Hash;
            Session.WorldPrepared = (accepted, hash) => Mirror.AttachLocal(worlds.GetOrGenerate(accepted.Seed, accepted.GeneratorVersion, scale));
        }

        public ClientSession Session { get; }
        public ClientWorldMirror Mirror { get; }
        public LocalPlayerPoseBuffer Pose { get; }
        public FishNetClient ClientTransport { get; }
        public ClientReplicationReceiver Receiver { get; }

        /// <summary>호스트일 때만: 서버 월드.</summary>
        public ServerWorld World { get; private set; }

        /// <summary>호스트일 때만: 호스트 세션 서비스.</summary>
        public SessionService HostSession { get; private set; }

        /// <summary>호스트이면 true.</summary>
        public bool IsHost => _isHost;

        /// <summary>호스트 세션이 열렸음(로비 생성 결과).</summary>
        public event Action<LobbyError> HostOpened;

        /// <summary>참가자용 구성을 만든다.</summary>
        public static SessionRuntime CreateClient(SessionRuntimeDeps deps)
        {
            var runtime = new SessionRuntime(deps, false);
            AvatarServices.Current = new AvatarServices(null, runtime.Mirror, runtime.Pose, deps.Clock, deps.Log, deps.Config.Network.AvatarSendHz);
            return runtime;
        }

        /// <summary>호스트용 구성을 만든다(서버 쪽 + 호스트 자신의 클라이언트 쪽).</summary>
        public static SessionRuntime CreateHost(SessionRuntimeDeps deps, HostWorldInfo world)
        {
            var runtime = new SessionRuntime(deps, true);
            runtime.BuildServerSide(world);
            return runtime;
        }

        /// <summary>
        /// 호스트: 서버를 켜고 세션을 연 뒤(Steam이면 로비 생성) 호스트 자신의 클라이언트를 같은 경로로 접속시킨다.
        /// </summary>
        public void StartHost()
        {
            if (!_isHost)
            {
                throw new InvalidOperationException("호스트 구성이 아닙니다.");
            }
            if (!_server.Start())
            {
                HostOpened?.Invoke(LobbyError.Failed);
                return;
            }

            HostSession.Opened += OnHostOpened;
            HostSession.Open();
        }

        /// <summary>참가자: 호스트에 접속을 시작한다.</summary>
        public bool StartClient(string address, ushort port, JoinPath path) => Session.Connect(address, port, path);

        /// <summary>매 프레임 불러 접속 시간 제한을 본다.</summary>
        public void Tick()
        {
            // FishNet 서버는 시작 요청 뒤 몇 프레임 지나서 실제로 켜진다. 켜지기 전에 클라이언트를 시작하면 호스트 클라이언트가 아니라
            // 원격 접속을 시도해 실패하므로, 서버가 켜진 것을 확인한 뒤 호스트 자신의 클라이언트를 접속시킨다.
            if (_pendingHostConnect && _server.IsRunning)
            {
                _pendingHostConnect = false;
                Session.Connect(string.Empty, 0, JoinPath.Invite);
            }
            Session.Tick();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            Session.Leave();
            Receiver.Dispose();
            ClientTransport.Dispose();
            if (_isHost)
            {
                HostSession?.Close();
                _gateway?.Dispose();
                _server?.Stop();
            }
            if (_deps.Network.ClientManager.Started)
            {
                _deps.Network.ClientManager.StopConnection();
            }
            AvatarServices.Current = null;
        }

        // ---------- 내부 ----------

        private void BuildServerSide(HostWorldInfo world)
        {
            IConfigProvider config = _deps.Config;
            ServerSettings settings = ServerSettings.FromConfig(config.Balance, config.Network);

            var guard = new RequestGuard(settings.RequestsPerSecondLimit, new CommandLimits(ProtocolConstants.PositionHalfRangeMeters));
            var dispatcher = new CommandDispatcher(guard, _deps.Log);
            dispatcher.Register<PingCommand>(CommandKind.Ping, new PingCommandHandler());
            HitResourceCommandHandler.Install(dispatcher, guard, HarvestSettings.FromConfig(config.Balance));
            World = new ServerWorld(settings, dispatcher, _deps.Clock, _deps.Perf, _deps.Log);

            // [FR-009, FR-169] 호스트도 시드로 월드를 로컬에서 만들어 요약값을 정한다(참가자에게 기대 요약값으로 알려 준다).
            // 시드 글자에서 시드 값을 정한다. 몇 초 걸리므로 호스트 시작 전에 끝낸다(로딩 표시는 화면 쪽 몫).
            if (world.Seed == 0UL)
            {
                world.Seed = SeedCodec.TryParse(world.SeedText, out ulong parsed) == SeedParseError.None ? parsed : SeedCodec.SeedFromText("DEFAULT");
            }
            LocalWorld local = _worlds.GetOrGenerate(world.Seed, world.GeneratorVersion, _deps.WorldScale);
            world.ExpectedWorldHash = local.Hash;
            World.AttachWorld(local);
            _deps.Log.Info(LogCategory, "월드 생성 " + local.BuildMilliseconds + " ms, 자원 " + local.ResourceCount + "개, 요약값 " + local.Hash.ToString("x16"));

            world.GameVersion = BuildInfo.Version.Full;
            world.ContentHash = config.ContentHash.Value;
            world.HostName = _deps.Identity.LocalDisplayName;
            bool steam = _deps.Transport == TransportKind.Steam;
            var rules = new SessionRules(settings.MaxPlayers, ProtocolConstants.ProtocolVersion, BuildInfo.Version, world.ContentHash);
            HostSession = new SessionService(rules, World, steam ? _deps.Lobby : null, EmptyBanListStore.Instance, world, steam, _deps.Log);

            // 호스트 본인의 연결에는 전송이 원격 ID를 알려 주지 못하므로 로컬 플레이어 ID를 쓴다.
            _server = new FishNetServer(_deps.Network, _deps.Identity.LocalPlayerId.Value.ToString(), _deps.Log);
            var publisher = new ReplicationPublisher(_server, World, config.Network, _deps.Perf, _deps.Log);
            var join = new JoinHandshake(_server, HostSession, World, publisher, _deps.Clock, _deps.Log,
                config.Network.JoinTimeoutSeconds, id => _deps.Identity.ResolveName(id), _deps.Identity.LocalPlayerId);
            join.WorldGenerationAllowanceSeconds = WorldGenerationAllowanceSeconds;
            _gateway = new ServerCommandGateway(_server, join, World, _deps.Log);

            // 서버 틱(30 Hz)은 FishNet TimeManager가 알려 준다.
            _server.Ticked += World.Tick;

            // 참가를 마친 플레이어의 아바타를 만들고, 나가면 지운다.
            _spawner = new AvatarSpawner(_deps.Network, _deps.AvatarPrefab, _deps.Log);
            join.PlayerJoined += player =>
            {
                if (join.TryGetConnection(player, out int connectionId))
                {
                    _spawner.Spawn(player, connectionId);
                }
            };
            join.PlayerLeft += _spawner.Despawn;

            AvatarServices.Current = new AvatarServices(World, Mirror, Pose, _deps.Clock, _deps.Log, config.Network.AvatarSendHz);
        }

        private void OnHostOpened(LobbyError error)
        {
            HostSession.Opened -= OnHostOpened;
            if (error != LobbyError.None)
            {
                _deps.Log.Error(LogCategory, "세션을 열지 못했습니다: " + error);
                HostOpened?.Invoke(error);
                return;
            }
            HostOpened?.Invoke(LobbyError.None);

            // 호스트 자신의 클라이언트도 같은 경로로 접속한다(서버가 같은 프로세스라 FishNet이 호스트 클라이언트로 연결).
            // 서버가 실제로 켜진 뒤에 시작해야 하므로 Tick에서 이어 한다.
            _pendingHostConnect = true;
        }
    }
}

// =============================================================
// 파일: NetTestDoubles.cs
// 역할: Game.Tests.Net용 가짜 구현(시계, 서버 전송, 로비, 로그)과 서버 쪽 전체 구성 도우미(Rig).
//       FishNet 없이 게이트웨이·참가 절차·복제를 시험한다.
// 레이어: test (EditMode, Game.Tests.Net)
// 관련: NFR-031, DES-051, DES-081~083, DES-085 / SDD 3.7.3, D-23
// =============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Adapters.Net.Gateway;
using Game.Adapters.Net.Join;
using Game.Adapters.Net.Replication;
using Game.Application.Commands;
using Game.Application.Config;
using Game.Application.Gathering;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Application.Replication;
using Game.Application.Server;
using Game.Application.Session;
using Game.Domain.Common;
using Game.Domain.Session;

namespace Game.Tests.Net
{
    internal sealed class FakeClock : IClock
    {
        public double RealtimeSeconds { get; set; }
        public void Advance(double seconds) => RealtimeSeconds += seconds;
    }

    internal sealed class ListLog : ILog
    {
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public void Info(string category, string message) { }
        public void Warn(string category, string message) => Warnings.Add(category + ": " + message);
        public void Error(string category, string message) => Errors.Add(category + ": " + message);
    }

    internal sealed class FakeNetServer : INetServer
    {
        public readonly List<(int Conn, INetMessage Message, NetChannel Channel)> Sent = new List<(int, INetMessage, NetChannel)>();
        public readonly List<int> DisconnectCalls = new List<int>();
        public bool IsRunning { get; private set; }
        public event Action<int, string> Connected;
        public event Action<int> Disconnected;
        public event Action<int, INetMessage> Received;

        public bool Start() { IsRunning = true; return true; }
        public void Stop() { IsRunning = false; }

        public bool Send<T>(int connectionId, in T message, NetChannel channel) where T : struct, INetMessage
        {
            Sent.Add((connectionId, message, channel));
            return true;
        }

        public void Broadcast<T>(in T message, NetChannel channel, Func<int, bool> filter) where T : struct, INetMessage { }
        public void Disconnect(int connectionId) => DisconnectCalls.Add(connectionId);

        public void SimulateConnect(int id, string remote) => Connected?.Invoke(id, remote);
        public void SimulateDisconnect(int id) => Disconnected?.Invoke(id);
        public void SimulateReceive(int id, INetMessage message) => Received?.Invoke(id, message);

        /// <summary>한 연결에 보낸 메시지(보낸 순서).</summary>
        public List<INetMessage> To(int id) => Sent.Where(s => s.Conn == id).Select(s => s.Message).ToList();

        /// <summary>한 연결에 보낸 특정 종류 메시지 수.</summary>
        public int Count<T>(int id) where T : INetMessage => Sent.Count(s => s.Conn == id && s.Message is T);
    }

    internal sealed class FakeLobby : ILobbyService
    {
        public readonly HashSet<PlayerId> Members = new HashSet<PlayerId>();
        public bool IsAvailable => true;
        public LobbyId Current => default;
        public event Action<LobbyId> JoinRequested { add { } remove { } }
        public event Action<int> MembersChanged { add { } remove { } }
        public void Create(SessionAccess access, int maxMembers, IReadOnlyDictionary<string, string> meta, Action<LobbyResult> onDone) => onDone(new LobbyResult(LobbyError.None, null));
        public void SetAccess(SessionAccess access) { }
        public void SetMeta(string key, string value) { }
        public void RequestList(Action<LobbyError, IReadOnlyList<LobbyInfo>> onDone) { }
        public void Join(LobbyId lobby, Action<LobbyResult> onDone) { }
        public bool IsMember(PlayerId player) => Members.Contains(player);
        public void Leave() { }
        public bool OpenInviteDialog() => false;
    }

    /// <summary>자원 표와 같은 모양(키 2B + 상태 2B)의 시험용 코덱.</summary>
    internal sealed class ResourceLikeCodec : IStateCodec<ushort>
    {
        public int KeyBytes => 2;
        public int MaxStateBytes => 2;
        public void Write(ByteWriter writer, in ushort state) => writer.WriteU16(state);

        public bool TryRead(ByteReader reader, out ushort state)
        {
            state = reader.ReadU16();
            return !reader.Failed;
        }
    }

    /// <summary>서버 쪽 전체 구성(가짜 전송 위에). 테스트가 틱과 메시지를 직접 넣는다.</summary>
    internal sealed class Rig
    {
        public readonly FakeClock Clock = new FakeClock();
        public readonly ListLog Log = new ListLog();
        public readonly FakeNetServer Server = new FakeNetServer();
        public readonly FakeLobby Lobby = new FakeLobby();
        public readonly ServerWorld World;
        public readonly SessionService Session;
        public readonly ReplicationPublisher Publisher;
        public readonly JoinHandshake Join;
        public readonly ServerCommandGateway Gateway;
        public readonly NetworkTuning Tuning = new NetworkTuning();
        public uint TickNumber;

        public const string Build = "0.1.0+dev.20261104153012.a1b2c3d";
        public const ulong Hash = 0xABUL;
        public const float TickSeconds = 1f / 30f;

        public Rig(bool steam = false, Func<PlayerId, string> nameOf = null, ulong expectedWorldHash = 0UL)
        {
            var guard = new RequestGuard(30, new CommandLimits(ProtocolConstants.PositionHalfRangeMeters));
            var dispatcher = new CommandDispatcher(guard, Log);
            dispatcher.Register<PingCommand>(CommandKind.Ping, new PingCommandHandler());
            HitResourceCommandHandler.Install(dispatcher, guard, new HarvestSettings());
            World = new ServerWorld(new ServerSettings(), dispatcher, Clock, null, Log);

            var info = new HostWorldInfo { WorldName = "w", HostName = "h", SeedText = "BENCH-0001", Seed = 1, GeneratorVersion = 0, GameVersion = Build, ContentHash = Hash, ExpectedWorldHash = expectedWorldHash };
            var rules = new SessionRules(8, ProtocolConstants.ProtocolVersion, Game.Domain.Common.GameVersion.TryParse(Build, out var hostVersion) ? hostVersion : null, Hash);
            Session = new SessionService(rules, World, steam ? Lobby : null, null, info, steam, Log);
            Publisher = new ReplicationPublisher(Server, World, Tuning, null, Log);
            Join = new JoinHandshake(Server, Session, World, Publisher, Clock, Log, Tuning.JoinTimeoutSeconds, nameOf ?? (id => "P" + id.Value), new PlayerId(1));
            Gateway = new ServerCommandGateway(Server, Join, World, Log);
        }

        public void Tick(int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                World.Tick(++TickNumber, TickSeconds);
                Clock.Advance(TickSeconds);
            }
        }

        public static JoinRequest Request(ulong devId, JoinPath path = JoinPath.Friend, string build = Build, ulong hash = Hash) =>
            new JoinRequest(ProtocolConstants.ProtocolVersion, build, hash, path, devId, new ushort[] { 0 });

        /// <summary>개발 전송 연결 하나가 JoinRequest → WorldReady → 스냅샷 완료까지 가게 한다.</summary>
        public void JoinFully(int connectionId, ulong devId)
        {
            Server.SimulateConnect(connectionId, "127.0.0.1");
            Server.SimulateReceive(connectionId, Request(devId));
            Server.SimulateReceive(connectionId, new WorldReady(0UL));
            Tick(2);
        }
    }
}

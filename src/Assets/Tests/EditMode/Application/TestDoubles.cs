// =============================================================
// 파일: TestDoubles.cs
// 역할: 시험용 가짜 구현(시계, 서버·클라이언트 전송, 로비, 로그). FishNet 없이 접속·복제 로직을 시험한다.
// 레이어: test (EditMode, Game.Tests.Application)
// 관련: NFR-031, DES-051 / SDD 3.7.3, D-23
// (같은 모양의 가짜가 Game.Tests.Net에도 있다: 시험 어셈블리는 소스를 공유하지 않는다.)
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Domain.Common;
using Game.Domain.Session;

namespace Game.Tests.Application
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

    internal sealed class FakeNetClient : INetClient
    {
        public readonly List<INetMessage> Sent = new List<INetMessage>();
        public bool ConnectResult = true;
        public int DisconnectCalls;
        public NetClientState State { get; private set; }
        public event Action<NetClientState> StateChanged;
        public event Action<INetMessage> Received;

        public bool Connect(string address, ushort port)
        {
            if (ConnectResult)
            {
                SimulateState(NetClientState.Connecting);
            }
            return ConnectResult;
        }

        public void Disconnect()
        {
            DisconnectCalls++;
            SimulateState(NetClientState.Stopped);
        }

        public bool Send<T>(in T message, NetChannel channel) where T : struct, INetMessage
        {
            Sent.Add(message);
            return true;
        }

        public void SimulateState(NetClientState state)
        {
            if (State == state)
            {
                return;
            }
            State = state;
            StateChanged?.Invoke(state);
        }

        public void SimulateReceive(INetMessage message) => Received?.Invoke(message);
    }

    internal sealed class FakeNetServer : INetServer
    {
        public readonly List<(int Conn, INetMessage Message, NetChannel Channel)> Sent = new List<(int, INetMessage, NetChannel)>();
        public readonly List<int> Disconnected_ = new List<int>();
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

        public void Disconnect(int connectionId) => Disconnected_.Add(connectionId);

        public void SimulateConnect(int id, string remote) => Connected?.Invoke(id, remote);
        public void SimulateDisconnect(int id) => Disconnected?.Invoke(id);
        public void SimulateReceive(int id, INetMessage message) => Received?.Invoke(id, message);
    }

    internal sealed class FakeLobby : ILobbyService
    {
        public readonly HashSet<PlayerId> Members = new HashSet<PlayerId>();
        public readonly Dictionary<string, string> Meta = new Dictionary<string, string>();
        public int CreateCalls;
        public SessionAccess? CreatedAccess;
        public int CreatedMax;
        public LobbyError CreateError = LobbyError.None;
        public bool IsAvailable { get; set; } = true;
        public LobbyId Current { get; private set; }
        public event Action<LobbyId> JoinRequested;
        public event Action<int> MembersChanged;

        public void Create(SessionAccess access, int maxMembers, IReadOnlyDictionary<string, string> meta, Action<LobbyResult> onDone)
        {
            CreateCalls++;
            CreatedAccess = access;
            CreatedMax = maxMembers;
            foreach (KeyValuePair<string, string> pair in meta)
            {
                Meta[pair.Key] = pair.Value;
            }
            Current = new LobbyId(1234);
            onDone(new LobbyResult(CreateError, CreateError == LobbyError.None ? new LobbyInfo(Current, new PlayerId(1), 1, maxMembers, Meta) : null));
        }

        public void SetAccess(SessionAccess access) => CreatedAccess = access;
        public void SetMeta(string key, string value) => Meta[key] = value;
        public void RequestList(Action<LobbyError, IReadOnlyList<LobbyInfo>> onDone) => onDone(LobbyError.None, new List<LobbyInfo>());
        public void Join(LobbyId lobby, Action<LobbyResult> onDone) => onDone(new LobbyResult(LobbyError.None, null));
        public bool IsMember(PlayerId player) => Members.Contains(player);
        public void Leave() { Current = default; }
        public bool InviteResult;
        public int InviteCalls;
        public bool OpenInviteDialog() { InviteCalls++; return InviteResult; }
        public void RaiseJoinRequested(LobbyId id) => JoinRequested?.Invoke(id);
        public void RaiseMembersChanged(int count) => MembersChanged?.Invoke(count);
    }
}

// =============================================================
// 파일: FishNetPacketTransport.cs
// 역할: FishNet을 INetServer / INetClient 포트로 감싼다. 메시지는 바이트 묶음 하나(PacketBroadcast)로만 FishNet에 등록하고,
//       보낼 때 MessageCodec으로 인코딩(크기 상한 검사), 받을 때 TryDecode(실패는 버리고 로그)한다.
// 레이어: adapters (Game.Adapters.Net). 이 파일과 PlayerAvatar만 FishNet 형식을 쓴다(SDD 3.4.1 규칙 8).
// 관련: FR-005, NFR-031(전송 종류를 모름), NFR-040, NFR-013, NFR-014, DES-121 / SDD 3.7.2, D-23, R-31
//
// FishNet은 연결 관리·전송(FishySteamworks/Tugboat)·채널·호스트 모드·서버 틱(TimeManager)을 계속 맡는다.
// =============================================================
using System;
using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Application.Server;
using FishNetChannel = FishNet.Transporting.Channel;

namespace Game.Adapters.Net.Transport
{
    /// <summary>FishNet에 등록하는 유일한 메시지. 인코딩된 프로토콜 패킷 바이트를 그대로 담는다.</summary>
    public struct PacketBroadcast : IBroadcast
    {
        /// <summary>종류 바이트로 시작하는 인코딩된 메시지.</summary>
        public byte[] Data;
    }

    /// <summary>채널 변환·공통 인코딩.</summary>
    internal static class PacketTransportCommon
    {
        public static FishNetChannel ToFishNet(NetChannel channel) =>
            channel == NetChannel.Unreliable ? FishNetChannel.Unreliable : FishNetChannel.Reliable;

        /// <summary>메시지를 패킷으로 인코딩한다. 상한을 넘으면 false. 송신 버퍼는 호출자가 재사용한다.</summary>
        public static bool TryBuild<T>(in T message, ByteWriter writer, out PacketBroadcast packet) where T : struct, INetMessage
        {
            if (!MessageCodec.TryEncode(message, writer))
            {
                packet = default;
                return false;
            }
            packet = new PacketBroadcast { Data = writer.ToArray() };
            return true;
        }
    }

    /// <summary>서버 쪽 FishNet 구현(INetServer)과 서버 틱 신호(ITickSource).</summary>
    public sealed class FishNetServer : INetServer, ITickSource
    {
        private readonly NetworkManager _network;
        private readonly ILog _log;
        private readonly string _localRemoteId;
        private readonly ByteWriter _writer = new ByteWriter(ProtocolConstants.MaxPacketBytes);
        private readonly Dictionary<int, AbuseMeter> _decodeFailures = new Dictionary<int, AbuseMeter>();
        private bool _registered;

        /// <param name="network">NetworkManager</param>
        /// <param name="localRemoteId">호스트 본인의 연결(호스트 모드 로컬 클라이언트)에 쓸 원격 ID. Steam 전송이면 호스트 SteamID 문자열</param>
        public FishNetServer(NetworkManager network, string localRemoteId, ILog log)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _localRemoteId = localRemoteId ?? string.Empty;
            _log = log ?? NullLog.Instance;
        }

        public bool IsRunning => _network.ServerManager.Started;

        public event Action<int, string> Connected;
        public event Action<int> Disconnected;
        public event Action<int, INetMessage> Received;
        public event Action<uint, float> Ticked;

        /// <summary>디코드에 실패해 버린 패킷 수(진단).</summary>
        public int DecodeFailureCount { get; private set; }

        public bool Start()
        {
            if (!_registered)
            {
                // 패킷은 인증 절차 없이 받는다(참가 판정은 JoinHandshake가 한다).
                _network.ServerManager.RegisterBroadcast<PacketBroadcast>(OnPacket, false);
                _network.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
                _network.TimeManager.OnTick += OnTick;
                _registered = true;
            }
            return _network.ServerManager.StartConnection();
        }

        public void Stop()
        {
            if (_network.ServerManager.Started)
            {
                _network.ServerManager.StopConnection(true);
            }
            Unregister();
        }

        public bool Send<T>(int connectionId, in T message, NetChannel channel) where T : struct, INetMessage
        {
            if (!_network.ServerManager.Clients.TryGetValue(connectionId, out NetworkConnection connection) || !connection.IsActive)
            {
                return false;
            }
            if (!PacketTransportCommon.TryBuild(message, _writer, out PacketBroadcast packet))
            {
                _log.Warn("net", "인코딩 실패(크기 상한 초과 등) kind=" + message.Kind);
                return false;
            }
            _network.ServerManager.Broadcast(connection, packet, false, PacketTransportCommon.ToFishNet(channel));
            return true;
        }

        public void Broadcast<T>(in T message, NetChannel channel, Func<int, bool> filter) where T : struct, INetMessage
        {
            if (!PacketTransportCommon.TryBuild(message, _writer, out PacketBroadcast packet))
            {
                _log.Warn("net", "인코딩 실패(크기 상한 초과 등) kind=" + message.Kind);
                return;
            }
            FishNetChannel fishChannel = PacketTransportCommon.ToFishNet(channel);
            foreach (KeyValuePair<int, NetworkConnection> pair in _network.ServerManager.Clients)
            {
                if (pair.Value.IsActive && (filter == null || filter(pair.Key)))
                {
                    _network.ServerManager.Broadcast(pair.Value, packet, false, fishChannel);
                }
            }
        }

        /// <summary>[IMP-003, SEC-002] 참가 판정을 통과한 연결을 FishNet 인증기에서 인증한다(인증기가 없으면 이미 인증된 상태라 할 일 없음).</summary>
        public void Admit(int connectionId)
        {
            if (_network.ServerManager.GetAuthenticator() is FishNetJoinAuthenticator authenticator
                && _network.ServerManager.Clients.TryGetValue(connectionId, out NetworkConnection connection))
            {
                authenticator.Authorize(connection);
            }
        }

        public void Disconnect(int connectionId)
        {
            if (_network.ServerManager.Clients.TryGetValue(connectionId, out NetworkConnection connection))
            {
                connection.Disconnect(false);
            }
        }

        private void Unregister()
        {
            if (!_registered)
            {
                return;
            }
            _network.ServerManager.UnregisterBroadcast<PacketBroadcast>(OnPacket);
            _network.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            _network.TimeManager.OnTick -= OnTick;
            _registered = false;
        }

        private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                // 전송이 알려 주는 원격 ID(Steam: SteamID). 호스트 본인의 로컬 연결은 전송에 주소가 없어 알고 있는 값을 쓴다. [D-14]
                string remoteId = connection.IsLocalClient ? _localRemoteId : connection.GetAddress();
                Connected?.Invoke(args.ConnectionId, remoteId);
            }
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                _decodeFailures.Remove(args.ConnectionId);
                Disconnected?.Invoke(args.ConnectionId);
            }
        }

        private void OnPacket(NetworkConnection connection, PacketBroadcast packet, FishNetChannel channel)
        {
            if (!MessageCodec.TryDecode(packet.Data, out INetMessage message, out DecodeFailure failure))
            {
                // [NFR-040] 디코드 실패는 버리고 연결별로 집계해 로그에 남긴다. 예외를 던지지 않는다.
                DecodeFailureCount++;
                if (!_decodeFailures.TryGetValue(connection.ClientId, out AbuseMeter meter))
                {
                    meter = new AbuseMeter();
                    _decodeFailures[connection.ClientId] = meter;
                }
                // [IMP-004, SEC-003] 로그는 ThrottledLog가 묶고, 짧은 시간에 실패가 한도를 넘으면 그 연결을 끊는다.
                bool abusive = meter.Record(UnityEngine.Time.realtimeSinceStartupAsDouble);
                _log.Warn("net", "디코드 실패 conn=" + connection.ClientId + " 사유=" + failure + " 창 안 누적=" + meter.Count);
                if (abusive)
                {
                    _log.Warn("net_abuse", "잘못된 패킷이 한도를 넘어 연결을 끊습니다. conn=" + connection.ClientId);
                    connection.Disconnect(false);
                }
                return;
            }
            Received?.Invoke(connection.ClientId, message);
        }

        private void OnTick()
        {
            if (_network.ServerManager.Started)
            {
                Ticked?.Invoke(_network.TimeManager.LocalTick, (float)_network.TimeManager.TickDelta);
            }
        }
    }

    /// <summary>클라이언트 쪽 FishNet 구현(INetClient).</summary>
    public sealed class FishNetClient : INetClient
    {
        private readonly NetworkManager _network;
        private readonly ILog _log;
        private readonly ByteWriter _writer = new ByteWriter(ProtocolConstants.MaxPacketBytes);
        private bool _registered;

        public FishNetClient(NetworkManager network, ILog log)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _log = log ?? NullLog.Instance;
        }

        public NetClientState State { get; private set; }

        public event Action<NetClientState> StateChanged;
        public event Action<INetMessage> Received;

        /// <summary>디코드에 실패해 버린 패킷 수(진단).</summary>
        public int DecodeFailureCount { get; private set; }

        /// <summary>FishNet에 등록한 수신 처리를 풀고 연결을 끊는다(세션이 끝날 때).</summary>
        public void Dispose()
        {
            if (_registered)
            {
                _network.ClientManager.UnregisterBroadcast<PacketBroadcast>(OnPacket);
                _network.ClientManager.OnClientConnectionState -= OnClientConnectionState;
                _registered = false;
            }
        }

        public bool Connect(string address, ushort port)
        {
            if (!_registered)
            {
                _network.ClientManager.RegisterBroadcast<PacketBroadcast>(OnPacket);
                _network.ClientManager.OnClientConnectionState += OnClientConnectionState;
                _registered = true;
            }
            // 서버가 같은 프로세스에서 이미 켜져 있으면(호스트 모드) 주소와 상관없이 호스트 클라이언트로 붙는다.
            bool ok = _network.ServerManager.Started
                ? _network.ClientManager.StartConnection()
                : _network.ClientManager.StartConnection(address, port);
            if (ok)
            {
                SetState(NetClientState.Connecting);
            }
            return ok;
        }

        public void Disconnect()
        {
            if (_network.ClientManager.Started)
            {
                _network.ClientManager.StopConnection();
            }
            else
            {
                SetState(NetClientState.Stopped);
            }
        }

        public bool Send<T>(in T message, NetChannel channel) where T : struct, INetMessage
        {
            if (!_network.ClientManager.Started)
            {
                return false;
            }
            if (!PacketTransportCommon.TryBuild(message, _writer, out PacketBroadcast packet))
            {
                _log.Warn("net", "인코딩 실패(크기 상한 초과 등) kind=" + message.Kind);
                return false;
            }
            _network.ClientManager.Broadcast(packet, PacketTransportCommon.ToFishNet(channel));
            return true;
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Starting:
                    SetState(NetClientState.Connecting);
                    break;
                case LocalConnectionState.Started:
                    SetState(NetClientState.Connected);
                    break;
                case LocalConnectionState.Stopped:
                    SetState(NetClientState.Stopped);
                    break;
            }
        }

        private void OnPacket(PacketBroadcast packet, FishNetChannel channel)
        {
            if (!MessageCodec.TryDecode(packet.Data, out INetMessage message, out DecodeFailure failure))
            {
                DecodeFailureCount++;
                _log.Warn("net", "서버 패킷 디코드 실패 사유=" + failure);
                return;
            }
            Received?.Invoke(message);
        }

        private void SetState(NetClientState state)
        {
            if (State == state)
            {
                return;
            }
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}

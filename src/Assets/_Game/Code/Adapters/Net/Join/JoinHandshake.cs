// =============================================================
// 파일: JoinHandshake.cs
// 역할: 호스트 쪽 참가 절차. 연결 → JoinRequest 판정(버전·ContentHash·로비 멤버·원격 SteamID·인원) → JoinAccepted/Rejected
//       → WorldReady → 스냅샷 전송 시작 → JoinComplete. 시간 초과와 연결 끊김 정리.
// 레이어: adapters (Game.Adapters.Net). 포트(INetServer)와 Protocol 메시지에만 의존한다(FishNet 형식 없음).
// 관련: FR-003, FR-004, FR-009, FR-011, NFR-009, NFR-038, NFR-039, NFR-040, DES-085 / SDD 3.9.2, D-14
//
// 플레이어 ID는 전송이 알려 준 원격 ID(Steam)로 정한다. 요청에 적힌 값은 개발 전송에서만 쓴다.
// =============================================================
using System;
using System.Collections.Generic;
using Game.Adapters.Net.Replication;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Application.Server;
using Game.Application.Session;
using Game.Domain.Common;
using Game.Domain.Session;

namespace Game.Adapters.Net.Join
{
    /// <summary>호스트 쪽 참가 절차 처리기(DES-085).</summary>
    public sealed class JoinHandshake
    {
        private enum Phase : byte
        {
            Connected = 0,
            Accepted = 1,
            Syncing = 2,
            InGame = 3,
        }

        private sealed class Conn
        {
            public int Id;
            public string RemoteId;
            public Phase Phase;
            public double Since;
            public PlayerId Player;
            public double CloseAt = -1d;
        }

        /// <summary>거부 메시지가 전송에 실릴 시간을 주고 연결을 끊기까지의 지연(초).</summary>
        public const double CloseDelaySeconds = 0.5;

        private readonly INetServer _server;
        private readonly SessionService _session;
        private readonly ServerWorld _world;
        private readonly ReplicationPublisher _replication;
        private readonly IClock _clock;
        private readonly ILog _log;
        private readonly float _timeoutSeconds;
        private readonly Func<PlayerId, string> _nameOf;
        private readonly PlayerId _hostPlayer;
        private readonly Dictionary<int, Conn> _conns = new Dictionary<int, Conn>();
        private readonly List<Conn> _scratch = new List<Conn>();

        /// <param name="server">전송 포트</param>
        /// <param name="session">참가 판정 서비스</param>
        /// <param name="world">서버 월드(플레이어 추가·제거)</param>
        /// <param name="replication">복제 송신기(스냅샷 시작)</param>
        /// <param name="timeoutSeconds">연결 뒤 JoinComplete까지 제한 시간(초)</param>
        /// <param name="nameOf">플레이어 표시 이름 조회</param>
        /// <param name="hostPlayer">호스트 본인의 플레이어 ID(명단에 호스트 표시)</param>
        public JoinHandshake(INetServer server, SessionService session, ServerWorld world, ReplicationPublisher replication,
            IClock clock, ILog log, float timeoutSeconds, Func<PlayerId, string> nameOf, PlayerId hostPlayer)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _replication = replication ?? throw new ArgumentNullException(nameof(replication));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _log = log ?? NullLog.Instance;
            _timeoutSeconds = timeoutSeconds;
            _nameOf = nameOf ?? (id => id.ToString());
            _hostPlayer = hostPlayer;
        }

        /// <summary>
        /// 허가 뒤 참가자가 월드를 만드는 동안 더 기다려 주는 시간(초). 기본 0(시간 제한 그대로).
        /// 월드 생성이 있는 실제 세션에서는 조립 지점이 SDD 3.12.6의 생성 예산(25초) 이상으로 정한다.
        /// </summary>
        public double WorldGenerationAllowanceSeconds { get; set; }

        /// <summary>플레이어가 참가를 마치고 나갔음(연결 끊김).</summary>
        public event Action<PlayerId> PlayerLeft;

        /// <summary>플레이어가 JoinComplete까지 마쳤음.</summary>
        public event Action<PlayerId> PlayerJoined;

        /// <summary>참가 절차 중이거나 게임 중인 연결 수.</summary>
        public int ConnectionCount => _conns.Count;

        /// <summary>게임 중(JoinComplete를 마친) 연결의 플레이어를 찾는다.</summary>
        public bool TryGetInGamePlayer(int connectionId, out PlayerId player)
        {
            if (_conns.TryGetValue(connectionId, out Conn c) && c.Phase == Phase.InGame)
            {
                player = c.Player;
                return true;
            }
            player = PlayerId.None;
            return false;
        }

        /// <summary>플레이어의 연결 번호를 찾는다.</summary>
        public bool TryGetConnection(PlayerId player, out int connectionId)
        {
            foreach (Conn c in _conns.Values)
            {
                if (c.Player == player && c.Phase != Phase.Connected)
                {
                    connectionId = c.Id;
                    return true;
                }
            }
            connectionId = -1;
            return false;
        }

        /// <summary>새 연결. 아직 누구인지 모르는 상태로 JoinRequest를 기다린다.</summary>
        public void OnConnected(int connectionId, string remoteId)
        {
            _conns[connectionId] = new Conn { Id = connectionId, RemoteId = remoteId ?? string.Empty, Phase = Phase.Connected, Since = _clock.RealtimeSeconds };
        }

        /// <summary>연결이 끊김. 참가 절차 중이었다면 잡아 둔 자리도 돌려준다.</summary>
        public void OnDisconnected(int connectionId)
        {
            _replication.RemoveConnection(connectionId);
            if (!_conns.TryGetValue(connectionId, out Conn c))
            {
                return;
            }
            _conns.Remove(connectionId);
            if (c.Phase != Phase.Connected && _world.HasPlayer(c.Player))
            {
                // [FR-011] 퇴장: 서버 월드에서 플레이어를 내보낸다(상태 보존은 M3).
                _world.RemovePlayer(c.Player);
                _log.Info("join", "퇴장 player=" + c.Player);
                PlayerLeft?.Invoke(c.Player);
            }
        }

        /// <summary>참가 절차 메시지(JoinRequest, WorldReady)를 처리한다. 다른 메시지는 무시한다.</summary>
        public void HandleMessage(int connectionId, INetMessage message)
        {
            if (!_conns.TryGetValue(connectionId, out Conn c) || c.CloseAt >= 0d)
            {
                return;
            }
            switch (message)
            {
                case JoinRequest request:
                    HandleJoinRequest(c, request);
                    break;
                case WorldReady ready:
                    HandleWorldReady(c, ready);
                    break;
            }
        }

        /// <summary>틱마다 부른다. 시간 초과 연결을 거부하고, 닫기로 한 연결을 끊는다. [NFR-039]</summary>
        public void Tick()
        {
            double now = _clock.RealtimeSeconds;
            _scratch.Clear();
            _scratch.AddRange(_conns.Values);
            for (int i = 0; i < _scratch.Count; i++)
            {
                Conn c = _scratch[i];
                if (c.CloseAt >= 0d)
                {
                    if (now >= c.CloseAt)
                    {
                        _server.Disconnect(c.Id);
                    }
                    continue;
                }
                // [NFR-018] 허가를 받은 참가자는 자기 PC에서 월드를 만드는 동안 WorldReady를 보낼 수 없다. 그 시간만큼 더 기다린다.
                double limit = c.Phase == Phase.Accepted ? _timeoutSeconds + WorldGenerationAllowanceSeconds : _timeoutSeconds;
                if (c.Phase != Phase.InGame && now - c.Since > limit)
                {
                    _log.Warn("join", "참가 시간 초과 conn=" + c.Id + " phase=" + c.Phase);
                    Reject(c, JoinRejectReason.Timeout);
                }
            }
        }

        private void HandleJoinRequest(Conn c, in JoinRequest request)
        {
            // 같은 연결에서 JoinRequest를 되풀이하는 것은 무시한다.
            if (c.Phase != Phase.Connected)
            {
                return;
            }

            // [D-14] 플레이어 ID는 전송이 알려 준 원격 ID로 정한다.
            if (!PlayerIdentityPolicy.TryResolve(_session.IsSteamTransport, c.RemoteId, request.DevPlayerId, out PlayerId player))
            {
                _log.Warn("join", "원격 ID를 확인하지 못해 거부 conn=" + c.Id);
                Reject(c, JoinRejectReason.AccessDenied);
                return;
            }
            if (_world.HasPlayer(player))
            {
                _log.Warn("join", "이미 접속 중인 플레이어의 두 번째 연결을 거부 player=" + player);
                Reject(c, JoinRejectReason.BadRequest);
                return;
            }

            JoinRejectReason reason = _session.Evaluate(player, request);
            if (reason != JoinRejectReason.None)
            {
                _log.Info("join", "거부 player=" + player + " 사유=" + reason);
                Reject(c, reason);
                return;
            }

            int slot = _world.AddPlayer(player, _nameOf(player), player == _hostPlayer);
            if (slot < 0)
            {
                Reject(c, JoinRejectReason.Full);
                return;
            }
            c.Player = player;
            c.Phase = Phase.Accepted;
            c.Since = _clock.RealtimeSeconds;
            _server.SendDefault(c.Id, _session.BuildAccepted(player, slot, _world.CurrentTick));
            _log.Info("join", "허가 player=" + player + " 자리=" + slot);
        }

        private void HandleWorldReady(Conn c, in WorldReady ready)
        {
            if (c.Phase != Phase.Accepted)
            {
                return;
            }

            // [FR-169] 요약값이 호스트 기대값과 다르면 월드 생성 불일치로 기록하고 접속을 끝낸다(기대값 0은 월드 없음).
            ulong expected = _session.Info.ExpectedWorldHash;
            if (expected != 0UL && ready.ComputedWorldHash != expected)
            {
                _log.Error("join", "월드 생성 불일치 player=" + c.Player + " 기대=" + expected + " 받음=" + ready.ComputedWorldHash);
                Reject(c, JoinRejectReason.GenMismatch);
                return;
            }

            c.Phase = Phase.Syncing;
            c.Since = _clock.RealtimeSeconds;
            _replication.BeginSync(c.Id, OnSyncComplete);
        }

        private void OnSyncComplete(int connectionId)
        {
            if (!_conns.TryGetValue(connectionId, out Conn c) || c.Phase != Phase.Syncing)
            {
                return;
            }
            c.Phase = Phase.InGame;
            // [IMP-003, SEC-002] 참가 판정을 통과한 연결만 전송 계층에서 인증한다(스폰·관찰·SteamID 동기화는 이 뒤부터).
            _server.Admit(connectionId);
            _server.SendDefault(connectionId, new JoinComplete(_world.CurrentTick, _world.SpawnPositionOf(c.Player)));
            _log.Info("join", "참가 완료 player=" + c.Player);
            PlayerJoined?.Invoke(c.Player);
        }

        private void Reject(Conn c, JoinRejectReason reason)
        {
            _server.SendDefault(c.Id, new JoinRejected(reason));
            c.CloseAt = _clock.RealtimeSeconds + CloseDelaySeconds;
            _replication.RemoveConnection(c.Id);
            if (c.Phase != Phase.Connected && _world.HasPlayer(c.Player))
            {
                _world.RemovePlayer(c.Player);
            }
            c.Phase = Phase.Connected; // 이후 메시지는 CloseAt 때문에 무시된다
        }
    }
}

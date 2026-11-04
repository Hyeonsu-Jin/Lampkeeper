// =============================================================
// 파일: ClientSession.cs
// 역할: 참가자(클라이언트) 쪽 접속 절차 상태 기계. 연결 → JoinRequest → JoinAccepted/Rejected → (월드 준비) → WorldReady
//       → 스냅샷 수신 → JoinComplete → 게임 중. 나가기·연결 끊김 처리.
// 레이어: application (Game.Application/Session, 순수 C#)
// 관련: FR-003, FR-004, FR-009, FR-011, NFR-018(60초 안에 조작 가능), NFR-039, DES-072 / SDD 3.9.2, 3.10.1
//
// 월드 로컬 생성(요약값 계산)은 M1-10에서 WorldPrepareHandler로 연결한다. 지금은 호스트가 알려 준 값을 그대로 돌려준다.
// =============================================================
using System;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Domain.Common;
using Game.Domain.Session;

namespace Game.Application.Session
{
    /// <summary>접속 상태(SDD 3.10.1의 클라이언트 세션 상태 중 접속 구간).</summary>
    public enum ClientSessionState : byte
    {
        /// <summary>접속하지 않음.</summary>
        Idle = 0,
        /// <summary>호스트에 연결 중, 허가를 기다리는 중.</summary>
        Connecting = 1,
        /// <summary>허가를 받고 월드를 만드는 중.</summary>
        Generating = 2,
        /// <summary>월드 정보(스냅샷)를 받는 중.</summary>
        Syncing = 3,
        /// <summary>조작 가능.</summary>
        InGame = 4,
        /// <summary>접속에 실패했거나 끊김(이유는 <see cref="ClientSession.Failure"/>).</summary>
        Ended = 5,
    }

    /// <summary>접속이 끝난 이유.</summary>
    public enum ClientFailure : byte
    {
        None = 0,
        /// <summary>호스트에 연결하지 못함 또는 응답 없음(시간 초과).</summary>
        HostNoResponse = 1,
        /// <summary>호스트가 참가를 거부함(사유는 <see cref="ClientSession.RejectReason"/>).</summary>
        Rejected = 2,
        /// <summary>월드 생성 결과가 호스트와 다름.</summary>
        WorldMismatch = 3,
        /// <summary>게임 중 연결이 끊김(호스트 종료 등).</summary>
        ConnectionLost = 4,
        /// <summary>사용자가 나감.</summary>
        LeftByUser = 5,
    }

    /// <summary>참가 요청에 넣을 이 PC의 정보.</summary>
    public sealed class ClientHandshakeInfo
    {
        public ushort ProtocolVersion { get; set; } = ProtocolConstants.ProtocolVersion;
        /// <summary>이 PC의 게임 버전 전체 문자열. [NFR-058]</summary>
        public string GameVersion { get; set; } = string.Empty;
        public ulong ContentHash { get; set; }
        /// <summary>개발 전송에서만 쓰는 ID. Steam 전송에서는 0.</summary>
        public ulong DevPlayerId { get; set; }
        public ushort[] SupportedGeneratorVersions { get; set; } = { 0 };
    }

    /// <summary>
    /// 월드 준비 처리기. JoinAccepted의 시드로 월드를 만들고 요약값을 돌려준다(M1-10에서 V0 생성기와 연결).
    /// 지금은 기본 처리기가 호스트의 기대값을 그대로 돌려준다.
    /// </summary>
    public delegate ulong WorldPrepareHandler(JoinAccepted accepted);

    /// <summary>클라이언트 접속 절차.</summary>
    public sealed class ClientSession
    {
        private readonly INetClient _client;
        private readonly Replication.ClientWorldMirror _mirror;
        private readonly ILocalPlayerPose _localPose;
        private readonly ClientHandshakeInfo _info;
        private readonly IClock _clock;
        private readonly ILog _log;
        private readonly float _timeoutSeconds;
        private JoinPath _path;
        private double _stateEnteredAt;

        /// <param name="client">전송 포트</param>
        /// <param name="mirror">월드 사본(나갈 때 비움)</param>
        /// <param name="localPose">내 자세 포트(스폰 위치를 알림)</param>
        /// <param name="info">이 PC의 버전 정보</param>
        /// <param name="timeoutSeconds">접속·허가·월드 정보 수신 각 단계의 제한 시간(초)</param>
        public ClientSession(INetClient client, Replication.ClientWorldMirror mirror, ILocalPlayerPose localPose,
            ClientHandshakeInfo info, IClock clock, ILog log, float timeoutSeconds)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _mirror = mirror ?? throw new ArgumentNullException(nameof(mirror));
            _localPose = localPose ?? throw new ArgumentNullException(nameof(localPose));
            _info = info ?? throw new ArgumentNullException(nameof(info));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _log = log ?? NullLog.Instance;
            _timeoutSeconds = timeoutSeconds;
            PrepareWorld = a => a.ExpectedWorldHash;
            _client.StateChanged += OnClientStateChanged;
            _client.Received += OnReceived;
        }

        public ClientSessionState State { get; private set; } = ClientSessionState.Idle;
        public ClientFailure Failure { get; private set; }
        public JoinRejectReason RejectReason { get; private set; }

        /// <summary>호스트가 정해 준 내 플레이어 ID(허가 뒤).</summary>
        public PlayerId LocalPlayer { get; private set; }

        /// <summary>내 자리 번호(허가 뒤).</summary>
        public int LocalSlot { get; private set; } = -1;

        /// <summary>호스트가 알려 준 월드 정보(허가 뒤).</summary>
        public JoinAccepted World { get; private set; }

        /// <summary>월드 준비 처리기(M1-10에서 교체).</summary>
        public WorldPrepareHandler PrepareWorld { get; set; }

        /// <summary>상태가 바뀜.</summary>
        public event Action<ClientSessionState> StateChanged;

        /// <summary>
        /// 호스트에 접속을 시작한다.
        /// </summary>
        /// <param name="address">Steam이면 호스트 SteamID, 직접 전송이면 IP</param>
        /// <param name="port">직접 전송 포트(Steam은 무시)</param>
        /// <param name="path">들어온 길(초대/친구/공개)</param>
        /// <returns>전송이 접속 시도를 받아들였으면 true</returns>
        public bool Connect(string address, ushort port, JoinPath path)
        {
            if (State != ClientSessionState.Idle && State != ClientSessionState.Ended)
            {
                return false;
            }
            _path = path;
            Failure = ClientFailure.None;
            RejectReason = JoinRejectReason.None;
            LocalSlot = -1;
            _mirror.Reset();
            SetState(ClientSessionState.Connecting);
            if (!_client.Connect(address, port))
            {
                End(ClientFailure.HostNoResponse);
                return false;
            }
            return true;
        }

        /// <summary>나간다(사용자 동작). 연결을 끊고 사본을 비운다.</summary>
        public void Leave()
        {
            if (State == ClientSessionState.Idle)
            {
                return;
            }
            Failure = ClientFailure.LeftByUser;
            SetState(ClientSessionState.Idle); // 먼저 Idle로 바꿔야 끊김 알림이 실패로 처리되지 않는다
            _client.Disconnect();
            _mirror.Reset();
        }

        /// <summary>매 프레임 불러 단계별 시간 제한을 본다. [NFR-039]</summary>
        public void Tick()
        {
            bool waiting = State == ClientSessionState.Connecting || State == ClientSessionState.Syncing;
            if (waiting && _clock.RealtimeSeconds - _stateEnteredAt > _timeoutSeconds)
            {
                _log.Warn("join", "접속 시간 초과 state=" + State);
                End(ClientFailure.HostNoResponse);
            }
        }

        private void OnClientStateChanged(NetClientState state)
        {
            if (state == NetClientState.Connected && State == ClientSessionState.Connecting)
            {
                // 연결이 열리면 곧바로 참가 요청을 보낸다. 요청에는 자기 SteamID를 적지 않는다(D-14).
                var request = new JoinRequest(_info.ProtocolVersion, _info.GameVersion, _info.ContentHash, _path, _info.DevPlayerId, _info.SupportedGeneratorVersions);
                if (!_client.SendDefault(request))
                {
                    End(ClientFailure.HostNoResponse);
                }
                return;
            }

            if (state == NetClientState.Stopped)
            {
                switch (State)
                {
                    case ClientSessionState.Idle:
                    case ClientSessionState.Ended:
                        break;
                    case ClientSessionState.InGame:
                        // [NFR-039] 게임 중 연결이 끊김(호스트 종료 등).
                        End(ClientFailure.ConnectionLost);
                        break;
                    default:
                        End(ClientFailure.HostNoResponse);
                        break;
                }
            }
        }

        private void OnReceived(INetMessage message)
        {
            switch (message)
            {
                case JoinRejected rejected:
                    if (State == ClientSessionState.Connecting)
                    {
                        RejectReason = rejected.Reason;
                        End(ClientFailure.Rejected);
                    }
                    break;

                case JoinAccepted accepted:
                    if (State == ClientSessionState.Connecting)
                    {
                        HandleAccepted(accepted);
                    }
                    break;

                case JoinComplete complete:
                    if (State == ClientSessionState.Syncing)
                    {
                        // 호스트가 정한 스폰 위치를 로컬 이동 쪽에 알린다.
                        _localPose.ApplyCorrection(complete.Spawn);
                        SetState(ClientSessionState.InGame);
                    }
                    break;
            }
        }

        private void HandleAccepted(JoinAccepted accepted)
        {
            LocalPlayer = accepted.YourPlayerId;
            LocalSlot = accepted.YourSlot;
            World = accepted;
            SetState(ClientSessionState.Generating);

            ulong computed;
            try
            {
                computed = PrepareWorld(accepted);
            }
            catch (Exception ex)
            {
                // 월드 준비 중 오류. 삼키지 않고 로그에 남기고 접속을 끝낸다.
                _log.Error("join", "월드 준비 실패: " + ex);
                End(ClientFailure.WorldMismatch);
                return;
            }

            // [FR-169] 요약값이 호스트 기대값과 다르면 월드 생성 불일치로 접속을 끝낸다(기대값 0은 월드 없음 = 비교 생략).
            if (accepted.ExpectedWorldHash != 0UL && computed != accepted.ExpectedWorldHash)
            {
                _client.SendDefault(new WorldReady(computed)); // 호스트도 기록하도록 알린다
                End(ClientFailure.WorldMismatch);
                return;
            }
            SetState(ClientSessionState.Syncing);
            _client.SendDefault(new WorldReady(computed));
        }

        private void End(ClientFailure failure)
        {
            if (State == ClientSessionState.Ended)
            {
                return;
            }
            Failure = failure;
            SetState(ClientSessionState.Ended); // 먼저 Ended로 바꿔 끊김 알림이 다시 처리되지 않게 한다
            if (_client.State != NetClientState.Stopped)
            {
                _client.Disconnect();
            }
        }

        private void SetState(ClientSessionState state)
        {
            if (State == state)
            {
                return;
            }
            State = state;
            _stateEnteredAt = _clock.RealtimeSeconds;
            StateChanged?.Invoke(state);
        }
    }
}

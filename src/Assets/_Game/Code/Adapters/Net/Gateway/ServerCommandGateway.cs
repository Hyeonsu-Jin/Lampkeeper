// =============================================================
// 파일: ServerCommandGateway.cs
// 역할: 서버 쪽 메시지 입구. 전송 이벤트를 받아 연결 ↔ 플레이어를 정하고(원격 ID), 참가 절차는 JoinHandshake에,
//       명령은 디코드해 ServerWorld 대기열에 넣고, 처리 결과를 요청자에게 회신한다. 예외는 여기서 막는다.
// 레이어: adapters (Game.Adapters.Net). 포트(INetServer)와 Protocol 메시지에만 의존한다(FishNet 형식 없음).
// 관련: FR-007, NFR-030, NFR-040(예외가 연결 처리 루프 밖으로 새지 않음), NFR-041, DES-081 / SDD 3.7.2 메시지 공통 규칙, D-19
//
// 호스트 자신의 플레이어도 같은 경로를 탄다(호스트 클라이언트도 서버에는 연결 하나). 모든 검사는 디코드된 메시지로 하므로
// FishNet 없이 EditMode에서 시험한다(Game.Tests.Net).
// =============================================================
using System;
using Game.Adapters.Net.Join;
using Game.Application.Commands;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Application.Server;
using Game.Domain.Common;

namespace Game.Adapters.Net.Gateway
{
    /// <summary>서버 쪽 메시지 입구(DES-081).</summary>
    public sealed class ServerCommandGateway
    {
        private readonly INetServer _server;
        private readonly JoinHandshake _join;
        private readonly ServerWorld _world;
        private readonly ILog _log;

        /// <summary>잘못된 메시지(서버로 보낼 수 없는 종류, 디코드 실패한 명령)를 버린 횟수(진단).</summary>
        public int DroppedCount { get; private set; }

        /// <summary>
        /// 만들고 포트·월드 이벤트를 구독한다.
        /// </summary>
        public ServerCommandGateway(INetServer server, JoinHandshake join, ServerWorld world, ILog log)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _join = join ?? throw new ArgumentNullException(nameof(join));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _log = log ?? NullLog.Instance;

            _server.Connected += OnConnected;
            _server.Disconnected += OnDisconnected;
            _server.Received += OnReceived;
            _world.CommandCompleted += OnCommandCompleted;
            _world.TickEnded += OnTickEnded;
        }

        /// <summary>구독을 풀어 정리한다.</summary>
        public void Dispose()
        {
            _server.Connected -= OnConnected;
            _server.Disconnected -= OnDisconnected;
            _server.Received -= OnReceived;
            _world.CommandCompleted -= OnCommandCompleted;
            _world.TickEnded -= OnTickEnded;
        }

        private void OnConnected(int connectionId, string remoteId)
        {
            try
            {
                _join.OnConnected(connectionId, remoteId);
            }
            catch (Exception ex)
            {
                _log.Error("gateway", "연결 처리 중 예외: " + ex);
            }
        }

        private void OnDisconnected(int connectionId)
        {
            try
            {
                _join.OnDisconnected(connectionId);
            }
            catch (Exception ex)
            {
                _log.Error("gateway", "연결 끊김 처리 중 예외: " + ex);
            }
        }

        private void OnTickEnded(uint tick)
        {
            try
            {
                _join.Tick();
            }
            catch (Exception ex)
            {
                _log.Error("gateway", "참가 절차 틱 처리 중 예외: " + ex);
            }
        }

        private void OnReceived(int connectionId, INetMessage message)
        {
            // [NFR-040] 어떤 메시지가 어떤 예외를 일으켜도 연결 처리 루프 밖으로 새지 않게 한다.
            try
            {
                if (message == null || !MessageCodec.IsClientToServer(message.Kind))
                {
                    // 서버 → 클라이언트 종류를 클라이언트가 보냈다: 변조된 입력.
                    DroppedCount++;
                    _log.Warn("gateway", "서버로 보낼 수 없는 메시지 종류를 버림 conn=" + connectionId + " kind=" + message?.Kind);
                    return;
                }

                switch (message)
                {
                    case CommandMessage command:
                        HandleCommand(connectionId, command);
                        break;
                    default:
                        _join.HandleMessage(connectionId, message);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error("gateway", "메시지 처리 중 예외 conn=" + connectionId + " : " + ex);
            }
        }

        private void HandleCommand(int connectionId, in CommandMessage message)
        {
            // [FR-007] 참가를 마친 연결의 플레이어만 명령을 보낼 수 있다.
            if (!_join.TryGetInGamePlayer(connectionId, out PlayerId player))
            {
                DroppedCount++;
                return;
            }

            if (!CommandCodec.TryDecode(message, out ICommand command, out Rejection rejection))
            {
                DroppedCount++;
                // [IMP-005, NFR-041] 잘못된 패킷 회신도 명령과 같은 빈도 버킷을 쓴다. 한도를 넘으면 회신 없이 버린다.
                if (_world.AllowRejectionReply(player))
                {
                    _server.SendDefault(connectionId, new CommandResultMessage(message.RequestId, false, rejection));
                }
                return;
            }

            if (!_world.Enqueue(player, command))
            {
                DroppedCount++;
                // [IMP-005] 몫을 넘긴 명령의 거부 회신도 같은 빈도 버킷으로 제한한다(회신 폭주 방지).
                if (_world.AllowRejectionReply(player))
                {
                    _server.SendDefault(connectionId, new CommandResultMessage(message.RequestId, false, new Rejection(ReasonCode.RateLimited)));
                }
            }
        }

        private void OnCommandCompleted(CommandOutcome outcome)
        {
            // [NFR-041] 빈도 제한에 걸린 요청에는 회신하지 않는다(폭주 요청에 응답을 되돌려 부풀리지 않기 위해).
            if (!outcome.Result.Ok && outcome.Result.Rejection.Reason == ReasonCode.RateLimited)
            {
                return;
            }
            if (_join.TryGetConnection(outcome.Player, out int connectionId))
            {
                _server.SendDefault(connectionId, outcome.Result);
            }
        }
    }
}

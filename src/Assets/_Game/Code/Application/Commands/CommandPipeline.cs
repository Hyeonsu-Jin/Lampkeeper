// =============================================================
// 파일: CommandPipeline.cs
// 역할: 참가자 요청을 호스트가 검증·실행하는 명령 파이프라인. ICommand, 처리기 인터페이스, CommandDispatcher,
//       RequestGuard(빈도·값 범위), TokenBucket, CommandCodec(본문 ↔ 명령), M1 시험용 Ping 명령과 처리기.
// 레이어: application (Game.Application/Commands, 순수 C#)
// 관련: FR-007(호스트 검증), NFR-003(거부 사유), NFR-030, NFR-040(예외가 새지 않음), NFR-041(요청 빈도 제한), DES-050 / SDD 3.3.2, 3.11.13
//
// 흐름: 게이트웨이가 CommandMessage를 디코드 → ServerWorld.Enqueue → 틱에서 Dispatch(빈도 → 범위 → 처리기 검증 → 실행).
// 거부 사유는 ReasonCode로 돌려주고 화면 문장은 받는 쪽이 만든다.
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Protocol;
using Game.Domain.Common;

namespace Game.Application.Commands
{
    /// <summary>명령 종류. 숫자 값은 전송되므로 바꾸거나 재사용하지 않는다(SDD 3.7.2 목록 순서와 무관).</summary>
    public enum CommandKind : byte
    {
        None = 0,

        /// <summary>핑(월드 위치 표시). M1에서는 파이프라인 시험용 명령으로 쓴다. 실제 핑 UI는 M5.</summary>
        Ping = 1,

        /// <summary>자원 타격(M1 시험용: 호스트 검증 + 자원 델타). 본문 배치는 Gathering/HitResourceCommand.</summary>
        HitResource = 2,
    }

    /// <summary>값 범위 검사 기준. 월드 한 변의 절반(m) 같은 값.</summary>
    public readonly struct CommandLimits
    {
        public float WorldHalfExtentMeters { get; }

        public CommandLimits(float worldHalfExtentMeters)
        {
            WorldHalfExtentMeters = worldHalfExtentMeters;
        }
    }

    /// <summary>참가자가 보내는 명령. 위치·수량 같은 값 범위 검사를 스스로 안다.</summary>
    public interface ICommand
    {
        CommandKind Kind { get; }

        /// <summary>요청 번호. 결과를 돌려줄 때 같은 번호를 단다.</summary>
        uint RequestId { get; }

        /// <summary>값 범위가 맞는지(좌표가 월드 안, 수량 ≥ 1 등). 맞으면 null. [NFR-040]</summary>
        Rejection? CheckRanges(in CommandLimits limits);
    }

    /// <summary>처리기가 받는 호스트 쪽 상황.</summary>
    public readonly struct CommandContext
    {
        public Game.Domain.Common.PlayerId Player { get; }
        public Server.WorldState World { get; }
        public uint Tick { get; }

        public CommandContext(Game.Domain.Common.PlayerId player, Server.WorldState world, uint tick)
        {
            Player = player;
            World = world;
            Tick = tick;
        }
    }

    /// <summary>명령 하나를 검증하고 실행하는 처리기.</summary>
    public interface ICommandHandler<in T> where T : ICommand
    {
        /// <summary>상태를 바꾸기 전 검증. 문제가 있으면 거부 사유, 없으면 null.</summary>
        Rejection? Validate(CommandContext context, T command);

        /// <summary>검증을 통과한 명령을 실행한다(호스트 상태를 바꾸는 유일한 곳).</summary>
        void Execute(CommandContext context, T command);
    }

    /// <summary>토큰 버킷. 초당 rate개씩 채워지고 최대 burst개까지 모인다. 시각은 호출자가 넘긴다(시험 쉬움).</summary>
    public sealed class TokenBucket
    {
        private readonly double _rate;
        private readonly double _burst;
        private double _tokens;
        private double _last;

        public TokenBucket(double ratePerSecond, double burst, double now)
        {
            _rate = ratePerSecond;
            _burst = burst;
            _tokens = burst;
            _last = now;
        }

        /// <summary>토큰 하나를 쓴다. 없으면 false.</summary>
        public bool TryTake(double now)
        {
            _tokens = Math.Min(_burst, _tokens + Math.Max(0d, now - _last) * _rate);
            _last = now;
            if (_tokens >= 1d)
            {
                _tokens -= 1d;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 요청 빈도·값 범위 검사. [NFR-041] 연결(플레이어)마다 전체 버킷 + 명령 종류별 버킷을 둔다.
    /// 조작된 클라이언트가 초당 수천 번 보내도 처리량은 상한을 넘지 않는다.
    /// </summary>
    public sealed class RequestGuard
    {
        private sealed class PlayerBuckets
        {
            public TokenBucket All;
            public readonly Dictionary<CommandKind, TokenBucket> PerKind = new Dictionary<CommandKind, TokenBucket>();
        }

        private readonly double _ratePerSecond;
        private readonly CommandLimits _limits;
        private readonly Dictionary<CommandKind, double> _kindRates = new Dictionary<CommandKind, double>();
        private readonly Dictionary<PlayerId, PlayerBuckets> _players = new Dictionary<PlayerId, PlayerBuckets>();

        /// <param name="requestsPerSecondLimit">설정값 `초당 요청 상한`</param>
        /// <param name="limits">값 범위 기준</param>
        public RequestGuard(int requestsPerSecondLimit, CommandLimits limits)
        {
            _ratePerSecond = Math.Max(1, requestsPerSecondLimit);
            _limits = limits;
        }

        /// <summary>명령 종류별 초당 상한을 따로 정한다(예: 공격은 도구 공격 속도). 정하지 않으면 전체 상한을 쓴다.</summary>
        public void SetKindRate(CommandKind kind, double ratePerSecond) => _kindRates[kind] = ratePerSecond;

        /// <summary>이 플레이어가 지금 이 종류의 명령을 보내도 되는지. 토큰을 쓴다.</summary>
        public bool CheckRate(PlayerId player, CommandKind kind, double now)
        {
            if (!_players.TryGetValue(player, out PlayerBuckets buckets))
            {
                buckets = new PlayerBuckets { All = new TokenBucket(_ratePerSecond, _ratePerSecond, now) };
                _players[player] = buckets;
            }
            if (!buckets.All.TryTake(now))
            {
                return false;
            }
            if (!buckets.PerKind.TryGetValue(kind, out TokenBucket kindBucket))
            {
                double rate = _kindRates.TryGetValue(kind, out double configured) ? configured : _ratePerSecond;
                kindBucket = new TokenBucket(rate, Math.Max(1d, rate), now);
                buckets.PerKind[kind] = kindBucket;
            }
            return kindBucket.TryTake(now);
        }

        /// <summary>값 범위를 검사한다. 맞으면 null.</summary>
        public Rejection? CheckRanges(ICommand command) => command.CheckRanges(_limits);

        /// <summary>플레이어가 나가면 버킷을 지운다.</summary>
        public void RemovePlayer(PlayerId player) => _players.Remove(player);
    }

    /// <summary>
    /// 명령 처리 진입점. 처리 순서: 빈도 → 값 범위 → 처리기 검증 → 실행. 예외는 여기서 삼키지 않고 로그로 남긴 뒤
    /// 「내부 오류」 결과로 바꾼다(연결 처리 루프 밖으로 새지 않음, NFR-040).
    /// </summary>
    public sealed class CommandDispatcher
    {
        private sealed class Route
        {
            public Func<CommandContext, ICommand, Rejection?> Validate;
            public Action<CommandContext, ICommand> Execute;
        }

        private readonly RequestGuard _guard;
        private readonly Ports.ILog _log;
        private readonly Dictionary<CommandKind, Route> _routes = new Dictionary<CommandKind, Route>();

        public CommandDispatcher(RequestGuard guard, Ports.ILog log)
        {
            _guard = guard ?? throw new ArgumentNullException(nameof(guard));
            _log = log ?? Ports.NullLog.Instance;
        }

        /// <summary>처리기를 등록한다. 같은 종류가 이미 있으면 바꾼다.</summary>
        public void Register<T>(CommandKind kind, ICommandHandler<T> handler) where T : ICommand
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            _routes[kind] = new Route
            {
                Validate = (ctx, cmd) => handler.Validate(ctx, (T)cmd),
                Execute = (ctx, cmd) => handler.Execute(ctx, (T)cmd),
            };
        }

        /// <summary>플레이어가 나갈 때 빈도 상태를 지운다.</summary>
        public void RemovePlayer(PlayerId player) => _guard.RemovePlayer(player);

        /// <summary>
        /// 명령 하나를 처리하고 결과를 돌려준다. 이 함수는 예외를 던지지 않는다.
        /// </summary>
        /// <param name="context">호스트 상황(플레이어·월드·틱)</param>
        /// <param name="command">디코드된 명령</param>
        /// <param name="now">서버 시계(초)</param>
        public CommandResultMessage Dispatch(CommandContext context, ICommand command, double now)
        {
            uint requestId = command.RequestId;

            // [NFR-041] 빈도 제한. 조작된 클라이언트가 처리 예산을 못 먹게 가장 먼저 막는다.
            if (!_guard.CheckRate(context.Player, command.Kind, now))
            {
                return Fail(requestId, new Rejection(ReasonCode.RateLimited));
            }

            Rejection? range = _guard.CheckRanges(command);
            if (range.HasValue)
            {
                return Fail(requestId, range.Value);
            }

            if (!_routes.TryGetValue(command.Kind, out Route route))
            {
                return Fail(requestId, new Rejection(ReasonCode.UnknownCommand));
            }

            try
            {
                Rejection? rejection = route.Validate(context, command);
                if (rejection.HasValue)
                {
                    return Fail(requestId, rejection.Value);
                }
                route.Execute(context, command);
                return new CommandResultMessage(requestId, true, default);
            }
            catch (Exception ex)
            {
                // 처리기의 결함이 서버 전체를 멈추지 않게 한다. 원인은 호스트 로그에 남긴다.
                _log.Error("command", "처리기 예외 kind=" + command.Kind + " player=" + context.Player + " : " + ex);
                return Fail(requestId, new Rejection(ReasonCode.InternalError));
            }
        }

        private static CommandResultMessage Fail(uint requestId, Rejection rejection) =>
            new CommandResultMessage(requestId, false, rejection);
    }

    /// <summary>[M1 시험용] 핑 명령: 월드 위치 하나. 본문 12바이트(위치 밀리미터 3개).</summary>
    public readonly struct PingCommand : ICommand
    {
        public uint RequestId { get; }
        public Vec3f Target { get; }

        public PingCommand(uint requestId, Vec3f target)
        {
            RequestId = requestId;
            Target = target;
        }

        public CommandKind Kind => CommandKind.Ping;

        public Rejection? CheckRanges(in CommandLimits limits)
        {
            // 좌표가 유한한 수이고 월드 안이어야 한다. [NFR-040]
            // 월드 좌표는 0~(절반×2)다(원점 중심이 아님).
            float max = 2f * limits.WorldHalfExtentMeters + ProtocolConstants.WorldEdgeMarginMeters;
            if (!Target.IsFinite
                || Target.X < -ProtocolConstants.WorldEdgeMarginMeters || Target.X > max
                || Target.Z < -ProtocolConstants.WorldEdgeMarginMeters || Target.Z > max)
            {
                return new Rejection(ReasonCode.InvalidValue);
            }
            return null;
        }
    }

    /// <summary>핑 처리기: 마지막 핑 위치를 월드 상태에 기록한다(M1 시험용).</summary>
    public sealed class PingCommandHandler : ICommandHandler<PingCommand>
    {
        public Rejection? Validate(CommandContext context, PingCommand command)
        {
            // [FR-007] 호스트가 아는 접속자만 명령을 보낼 수 있다.
            return context.World.TryGetPlayer(context.Player, out _) ? (Rejection?)null : new Rejection(ReasonCode.TargetMissing);
        }

        public void Execute(CommandContext context, PingCommand command)
        {
            context.World.RecordPing(context.Player, command.Target);
        }
    }

    /// <summary>명령 ↔ CommandMessage 본문 변환. 알 수 없는 종류나 잘못된 본문은 거부 사유로 돌려준다(예외 없음).</summary>
    public static class CommandCodec
    {
        /// <summary>명령을 전송용 메시지로 만든다. 쓰기에 실패하면 false.</summary>
        public static bool TryEncode(ICommand command, out CommandMessage message)
        {
            message = default;
            switch (command)
            {
                case PingCommand ping:
                {
                    var writer = new ByteWriter(16);
                    writer.WriteI32(Quantize.MetersToMillimeters(ping.Target.X));
                    writer.WriteI32(Quantize.MetersToMillimeters(ping.Target.Y));
                    writer.WriteI32(Quantize.MetersToMillimeters(ping.Target.Z));
                    if (writer.Failed)
                    {
                        return false;
                    }
                    message = new CommandMessage((byte)CommandKind.Ping, ping.RequestId, writer.ToArray());
                    return true;
                }
                case Gathering.HitResourceCommand hit:
                {
                    var writer = new ByteWriter(8);
                    writer.WriteU16(hit.ResourceIndex);
                    writer.WriteU8(hit.HandSlot);
                    if (writer.Failed)
                    {
                        return false;
                    }
                    message = new CommandMessage((byte)CommandKind.HitResource, hit.RequestId, writer.ToArray());
                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary>전송된 메시지를 명령으로 바꾼다.</summary>
        public static bool TryDecode(in CommandMessage message, out ICommand command, out Rejection rejection)
        {
            command = null;
            rejection = default;
            switch ((CommandKind)message.CommandKindValue)
            {
                case CommandKind.Ping:
                {
                    var reader = new ByteReader(message.Payload);
                    float x = Quantize.MillimetersToMeters(reader.ReadI32());
                    float y = Quantize.MillimetersToMeters(reader.ReadI32());
                    float z = Quantize.MillimetersToMeters(reader.ReadI32());
                    if (!reader.IsCleanEnd)
                    {
                        rejection = new Rejection(ReasonCode.InvalidValue);
                        return false;
                    }
                    command = new PingCommand(message.RequestId, new Vec3f(x, y, z));
                    return true;
                }
                case CommandKind.HitResource:
                {
                    var reader = new ByteReader(message.Payload);
                    ushort index = reader.ReadU16();
                    byte handSlot = reader.ReadU8();
                    if (!reader.IsCleanEnd)
                    {
                        rejection = new Rejection(ReasonCode.InvalidValue);
                        return false;
                    }
                    command = new Gathering.HitResourceCommand(message.RequestId, index, handSlot);
                    return true;
                }
                default:
                    rejection = new Rejection(ReasonCode.UnknownCommand);
                    return false;
            }
        }
    }
}

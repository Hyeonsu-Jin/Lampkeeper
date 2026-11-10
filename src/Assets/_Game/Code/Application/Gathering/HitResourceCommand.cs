// =============================================================
// 파일: HitResourceCommand.cs
// 역할: [M1 시험용] 자원 타격 명령(HitResource)과 호스트 처리기, 채집 설정(HarvestSettings).
//       참가자가 「이 자원을 때렸다」고 요청하면 호스트가 거리·대상 상태를 검증하고 자원 표를 바꾼다. 바뀐 항목은 델타로 퍼진다.
// 레이어: application (Game.Application/Gathering, 순수 C#)
// 관련: FR-007(호스트 검증), FR-009, FR-044~047, FR-051(여러 명이 같은 나무를 때려도 한 번만 쓰러짐), NFR-040, NFR-041, DES-008, DES-057 / SDD 3.9.3
//
// M1 범위: 도구 종류·등급, 내구도, 드롭, 경험치는 다루지 않는다(M2). 타격 수와 단계 변화만 처리한다.
// 흐름: 게이트웨이가 디코드 → ServerWorld 대기열(도착 순서) → 틱에서 Dispatch(빈도 → 값 범위 → Validate → Execute).
// =============================================================
using System;
using Game.Application.Commands;
using Game.Application.Config;
using Game.Application.WorldSync;
using Game.Domain.Common;
using Game.Domain.Gathering;
using Game.Domain.Players;

namespace Game.Application.Gathering
{
    /// <summary>채집 설정값(설정 애셋에서 만든다).</summary>
    public sealed class HarvestSettings
    {
        /// <summary>
        /// 서버가 아는 위치와 클라이언트가 보는 위치의 차이를 흡수하는 여유 거리(m). 이동 검사의 여유 거리와 같은 성격이다.
        /// </summary>
        public const float DefaultDistanceSlackMeters = 0.75f;

        /// <summary>한 사람이 초당 보낼 수 있는 타격 요청 수(공격 속도 상한). 도구별 공격 속도는 M2에서 도구 값으로 바뀐다.</summary>
        public const float DefaultHitsPerSecond = 4f;

        public HarvestHitCounts HitCounts { get; set; } = new HarvestHitCounts(5, 4, 6, 5);
        public float InteractionDistanceMeters { get; set; } = 2.5f;
        public float DistanceSlackMeters { get; set; } = DefaultDistanceSlackMeters;
        public float HitsPerSecond { get; set; } = DefaultHitsPerSecond;

        /// <summary>설정 애셋 값으로 만든다.</summary>
        /// <exception cref="ArgumentNullException">balance가 null일 때</exception>
        public static HarvestSettings FromConfig(BalanceValues balance)
        {
            if (balance == null)
            {
                throw new ArgumentNullException(nameof(balance));
            }
            GatheringSettings g = balance.Gathering;
            return new HarvestSettings
            {
                // 광석은 기본 광석 횟수를 쓴다(희귀 광석 구분은 M2).
                HitCounts = new HarvestHitCounts(g.TreeBasicHits, g.StoneHits, g.OreBasicHits, g.CoalVeinHits),
                InteractionDistanceMeters = balance.Movement.InteractionDistanceMeters,
            };
        }
    }

    /// <summary>자원 타격 명령. 본문 3바이트(자원 번호 u16 + 손 칸 u8).</summary>
    public readonly struct HitResourceCommand : ICommand
    {
        /// <summary>손 칸 번호의 상한(퀵슬롯 수 안). 값 범위 검사 기준.</summary>
        public const byte MaxHandSlot = 9;

        public uint RequestId { get; }

        /// <summary>자원 번호(생성 표의 ResourceIndex).</summary>
        public ushort ResourceIndex { get; }

        /// <summary>손에 든 칸 번호. 도구 판정은 M2부터 쓴다.</summary>
        public byte HandSlot { get; }

        public HitResourceCommand(uint requestId, ushort resourceIndex, byte handSlot)
        {
            RequestId = requestId;
            ResourceIndex = resourceIndex;
            HandSlot = handSlot;
        }

        public CommandKind Kind => CommandKind.HitResource;

        public Rejection? CheckRanges(in CommandLimits limits)
        {
            // [NFR-040] 손 칸이 범위 안이어야 한다. 자원 번호의 범위는 월드가 있어야 알 수 있어 처리기가 본다.
            return HandSlot > MaxHandSlot ? new Rejection(ReasonCode.InvalidValue) : (Rejection?)null;
        }
    }

    /// <summary>자원 타격 처리기. 호스트 상태(자원 표)를 바꾸는 곳.</summary>
    public sealed class HitResourceCommandHandler : ICommandHandler<HitResourceCommand>
    {
        private readonly HarvestSettings _settings;

        public HitResourceCommandHandler(HarvestSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>타격 명령 종류의 처리기와 요청 빈도 상한을 한 번에 등록한다.</summary>
        public static void Install(CommandDispatcher dispatcher, RequestGuard guard, HarvestSettings settings)
        {
            if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
            if (guard == null) throw new ArgumentNullException(nameof(guard));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            dispatcher.Register<HitResourceCommand>(CommandKind.HitResource, new HitResourceCommandHandler(settings));
            guard.SetKindRate(CommandKind.HitResource, settings.HitsPerSecond);
        }

        public Rejection? Validate(CommandContext context, HitResourceCommand command)
        {
            // [FR-007] 호스트가 아는 접속자만, 호스트가 가진 월드의 자원만 때릴 수 있다.
            if (!context.World.TryGetPlayer(context.Player, out PlayerCharacter player))
            {
                return new Rejection(ReasonCode.TargetMissing);
            }
            LocalWorld local = context.World.Local;
            if (local == null || !local.TryGetResource(command.ResourceIndex, out ResourceKind kind, out Vec3f position))
            {
                return new Rejection(ReasonCode.TargetMissing);
            }

            // [FR-007] 서버가 아는 플레이어 위치와 생성 표의 자원 위치로 거리를 잰다(수평 거리. 지형 높이는 화면 쪽이 생긴 뒤 다룸).
            float allowed = _settings.InteractionDistanceMeters + _settings.DistanceSlackMeters;
            if (Vec3f.HorizontalDistance(player.Position, position) > allowed)
            {
                return new Rejection(ReasonCode.OutOfRange);
            }

            // [FR-051] 이미 쓰러진·부서진 대상은 다시 처리하지 않는다. 같은 나무를 두 명이 때려도 쓰러짐은 한 번만.
            context.World.Resources.TryGet(command.ResourceIndex, out ResourceState state);
            if (!HarvestRules.CanHit(state) || HarvestRules.RequiredHits(kind, _settings.HitCounts) <= 0)
            {
                return new Rejection(ReasonCode.NotAllowedNow);
            }
            return null;
        }

        public void Execute(CommandContext context, HitResourceCommand command)
        {
            context.World.TryGetPlayer(context.Player, out PlayerCharacter player);
            context.World.Local.TryGetResource(command.ResourceIndex, out ResourceKind kind, out Vec3f position);
            context.World.Resources.TryGet(command.ResourceIndex, out ResourceState state);

            byte fallDir = HarvestRules.QuantizeFallDirection(position.X - player.Position.X, position.Z - player.Position.Z);
            ResourceState next = HarvestRules.ApplyHit(state, kind, _settings.HitCounts, fallDir);
            // 표에는 바뀐 항목만 들어간다. 변경 기록에 남아 다음 틱 TableDelta로 모든 참가자에게 간다.
            context.World.SetResource(command.ResourceIndex, next);
        }
    }
}

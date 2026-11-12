// =============================================================
// 파일: CreatureBrain.cs
// 역할: 생물 판단 규칙(상태 전이, 배회 목적지 선택)과 공격 판정(거리·각도·쿨다운). 물리·애니메이션에 의존하지 않는다.
// 레이어: domain (Game.Domain/Creatures, 순수 C#)
// 관련: FR-079~081, DES-019 / SDD 3.10.6, 3.11.10
// =============================================================
using System;
using Game.Domain.Common;

namespace Game.Domain.Creatures
{
    /// <summary>판단에 쓰는 가장 가까운 플레이어 정보.</summary>
    public readonly struct PlayerSighting
    {
        /// <summary>없음(접속자 없음).</summary>
        public static readonly PlayerSighting None = new PlayerSighting(false, PlayerId.None, Vec3f.Zero, float.PositiveInfinity);

        public bool Exists { get; }
        public PlayerId Player { get; }
        public Vec3f Position { get; }

        /// <summary>생물과의 수평 거리(m).</summary>
        public float Distance { get; }

        public PlayerSighting(bool exists, PlayerId player, Vec3f position, float distance)
        {
            Exists = exists;
            Player = player;
            Position = position;
            Distance = distance;
        }
    }

    /// <summary>생물 판단·공격 규칙 모음(상태 없음).</summary>
    public static class CreatureBrain
    {
        /// <summary>배회 목적지에 이 거리 안으로 들어오면 도착으로 본다(m).</summary>
        public const float WanderArriveMeters = 1.5f;

        /// <summary>공격 중 대상이 사거리의 이 배수를 넘으면 다시 쫓는다(경계에서 모드가 떨리지 않게 하는 여유).</summary>
        public const float AttackLeaveFactor = 1.25f;

        /// <summary>
        /// 판단 한 번(10 Hz 이하로 불린다). 모드를 바꾸고 대상·배회 목적지를 갱신한다.
        /// </summary>
        /// <param name="creature">판단할 생물(Dead면 아무것도 하지 않음)</param>
        /// <param name="nearest">가장 가까운 플레이어</param>
        public static void Decide(Creature creature, in PlayerSighting nearest)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));
            CreatureDefinition def = creature.Definition;
            switch (creature.Mode)
            {
                case CreatureMode.Dead:
                    return;

                case CreatureMode.Idle:
                    // [FR-079] 탐지 거리 안에 플레이어가 있으면 추적을 시작한다.
                    if (nearest.Exists && nearest.Distance <= def.DetectRange)
                    {
                        Acquire(creature, nearest);
                        creature.Mode = nearest.Distance <= def.AttackRange ? CreatureMode.Attack : CreatureMode.Seek;
                        return;
                    }
                    PickWanderIfNeeded(creature);
                    return;

                case CreatureMode.Seek:
                    if (!nearest.Exists || nearest.Distance > def.LoseRange)
                    {
                        // 대상 상실 -> 배회로 돌아간다.
                        creature.HasTarget = false;
                        creature.Mode = CreatureMode.Idle;
                        PickWanderIfNeeded(creature);
                        return;
                    }
                    Acquire(creature, nearest);
                    if (nearest.Distance <= def.AttackRange)
                    {
                        creature.Mode = CreatureMode.Attack;
                    }
                    return;

                case CreatureMode.Attack:
                    if (!nearest.Exists || nearest.Distance > def.LoseRange)
                    {
                        creature.HasTarget = false;
                        creature.Mode = CreatureMode.Idle;
                        PickWanderIfNeeded(creature);
                        return;
                    }
                    Acquire(creature, nearest);
                    if (nearest.Distance > def.AttackRange * AttackLeaveFactor)
                    {
                        creature.Mode = CreatureMode.Seek; // 대상 이탈
                    }
                    else
                    {
                        creature.YawDegrees = FacingYawDegrees(creature.Position, nearest.Position); // 공격 중에는 대상을 바라본다
                    }
                    return;
            }
        }

        /// <summary>
        /// 공격 판정 한 틱. 쿨다운을 줄이고, 사거리·각도 안이며 쿨다운이 끝났으면 공격이 맞은 것으로 본다.
        /// 서버 틱마다 부르고, 판정은 거리·각도·쿨다운만으로 한다(물리·애니메이션에 의존하지 않음).
        /// </summary>
        /// <param name="creature">공격자</param>
        /// <param name="targetPosition">대상 플레이어의 현재 위치</param>
        /// <param name="deltaSeconds">틱 간격(초)</param>
        /// <returns>이번 틱에 공격이 맞았으면 true(쿨다운이 다시 시작됨)</returns>
        public static bool ResolveAttack(Creature creature, Vec3f targetPosition, float deltaSeconds)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));
            if (creature.AttackCooldownLeft > 0f)
            {
                creature.AttackCooldownLeft = Math.Max(0f, creature.AttackCooldownLeft - deltaSeconds);
            }
            if (creature.Mode != CreatureMode.Attack || !creature.HasTarget || creature.AttackCooldownLeft > 0f)
            {
                return false;
            }
            CreatureDefinition def = creature.Definition;
            if (Vec3f.HorizontalDistance(creature.Position, targetPosition) > def.AttackRange)
            {
                return false;
            }
            float toTarget = FacingYawDegrees(creature.Position, targetPosition);
            float diff = Math.Abs(NormalizeDegrees(toTarget - creature.YawDegrees));
            if (diff > def.AttackArcDegrees * 0.5f)
            {
                return false;
            }
            creature.AttackCooldownLeft = def.AttackCooldownSeconds;
            return true;
        }

        /// <summary>from에서 to를 바라보는 방향(도, 0 = +Z, 시계 방향이 +).</summary>
        public static float FacingYawDegrees(Vec3f from, Vec3f to)
        {
            float dx = to.X - from.X;
            float dz = to.Z - from.Z;
            if (dx == 0f && dz == 0f)
            {
                return 0f;
            }
            return (float)(Math.Atan2(dx, dz) * (180.0 / Math.PI));
        }

        private static float NormalizeDegrees(float d)
        {
            while (d > 180f) d -= 360f;
            while (d < -180f) d += 360f;
            return d;
        }

        private static void Acquire(Creature creature, in PlayerSighting nearest)
        {
            creature.HasTarget = true;
            creature.TargetPlayer = nearest.Player;
            creature.HasWander = false;
        }

        private static void PickWanderIfNeeded(Creature c)
        {
            if (c.HasWander)
            {
                float dx = c.WanderX - c.Position.X;
                float dz = c.WanderZ - c.Position.Z;
                if (dx * dx + dz * dz > WanderArriveMeters * WanderArriveMeters)
                {
                    return; // 아직 가는 중
                }
            }
            // 출발 위치 둘레의 무작위 한 점. 난수는 생물마다 따로라 시드가 같으면 같은 길을 걷는다.
            float angle = NextUnit(c) * (2f * (float)Math.PI);
            float radius = (float)Math.Sqrt(NextUnit(c)) * c.Definition.WanderRadius;
            c.WanderX = c.HomeX + (float)Math.Sin(angle) * radius;
            c.WanderZ = c.HomeZ + (float)Math.Cos(angle) * radius;
            c.HasWander = true;
        }

        /// <summary>xorshift32로 0 이상 1 미만 값.</summary>
        private static float NextUnit(Creature c)
        {
            uint x = c.RngState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            c.RngState = x;
            return (x >> 8) * (1f / 16777216f);
        }
    }
}

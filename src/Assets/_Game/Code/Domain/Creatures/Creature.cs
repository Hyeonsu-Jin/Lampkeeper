// =============================================================
// 파일: Creature.cs
// 역할: 생물 정의(CreatureDefinition), 생물 모드(CreatureMode), 생물 엔터티(Creature).
// 레이어: domain (Game.Domain/Creatures, 순수 C#)
// 관련: FR-077, FR-079~081, DES-019 / SDD 3.10.6
//
// M1-11 범위의 시험용 생물 1종만 다룬다. 정화 구역 대기(HoldAtBoundary)·광원 퇴치(Repelled)는 이후 마일스톤에서 모드를 더한다.
// =============================================================
using System;
using Game.Domain.Common;

namespace Game.Domain.Creatures
{
    /// <summary>생물 모드(상태 기계). SDD 3.10.6 중 M1-11에서 쓰는 부분.</summary>
    public enum CreatureMode : byte
    {
        /// <summary>배회. 대상이 없다.</summary>
        Idle = 0,
        /// <summary>대상 추적.</summary>
        Seek = 1,
        /// <summary>공격 사거리 안.</summary>
        Attack = 2,
        /// <summary>사망. 더는 움직이지도 판단하지도 않는다.</summary>
        Dead = 3,
    }

    /// <summary>생물 종류 정의(수치). 값은 만든 뒤 바꾸지 않는다.</summary>
    public sealed class CreatureDefinition
    {
        /// <summary>시험용 생물 종류 번호(CreatureSpawn.DefId).</summary>
        public const ushort TestDefId = 1;

        /// <summary>시험용 생물 1종(M1-11 부하 시험).</summary>
        public static CreatureDefinition TestCreature { get; } = new CreatureDefinition(
            TestDefId, maxHealth: 50f, detectRange: 40f, loseRange: 60f, attackRange: 1.8f, attackDamage: 10f,
            attackCooldownSeconds: 1.5f, attackArcDegrees: 90f, walkSpeed: 1.2f, runSpeed: 4f, wanderRadius: 12f);

        public ushort DefId { get; }
        public float MaxHealth { get; }
        /// <summary>이 거리 안에 플레이어가 들어오면 추적을 시작한다(m).</summary>
        public float DetectRange { get; }
        /// <summary>이 거리 밖으로 벗어나면 추적을 그만둔다(m). DetectRange보다 커야 경계에서 떨리지 않는다.</summary>
        public float LoseRange { get; }
        public float AttackRange { get; }
        public float AttackDamage { get; }
        public float AttackCooldownSeconds { get; }
        /// <summary>정면 기준 공격이 맞는 전체 각도(도).</summary>
        public float AttackArcDegrees { get; }
        public float WalkSpeed { get; }
        public float RunSpeed { get; }
        /// <summary>배회 반경(출발 위치 기준, m).</summary>
        public float WanderRadius { get; }

        public CreatureDefinition(ushort defId, float maxHealth, float detectRange, float loseRange, float attackRange, float attackDamage,
            float attackCooldownSeconds, float attackArcDegrees, float walkSpeed, float runSpeed, float wanderRadius)
        {
            if (maxHealth <= 0f) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (loseRange < detectRange) throw new ArgumentException("loseRange는 detectRange 이상이어야 합니다.", nameof(loseRange));
            DefId = defId;
            MaxHealth = maxHealth;
            DetectRange = detectRange;
            LoseRange = loseRange;
            AttackRange = attackRange;
            AttackDamage = attackDamage;
            AttackCooldownSeconds = attackCooldownSeconds;
            AttackArcDegrees = attackArcDegrees;
            WalkSpeed = walkSpeed;
            RunSpeed = runSpeed;
            WanderRadius = wanderRadius;
        }
    }

    /// <summary>생물 한 마리의 상태. 서버(호스트)만 바꾼다.</summary>
    public sealed class Creature
    {
        public ushort Id { get; }
        public CreatureDefinition Definition { get; }
        public CreatureMode Mode { get; internal set; }
        public Vec3f Position { get; set; }

        /// <summary>바라보는 방향(도, 0 = +Z, 시계 방향이 +).</summary>
        public float YawDegrees { get; set; }
        public float Health { get; private set; }

        /// <summary>추적·공격 대상이 있는지.</summary>
        public bool HasTarget { get; internal set; }
        public PlayerId TargetPlayer { get; internal set; }

        /// <summary>배회 목적지가 있는지, 있으면 그 위치(X, Z).</summary>
        public bool HasWander { get; set; }
        public float WanderX { get; internal set; }
        public float WanderZ { get; internal set; }

        /// <summary>출발 위치(배회 중심).</summary>
        public float HomeX { get; }
        public float HomeZ { get; }

        /// <summary>다음 공격까지 남은 시간(초).</summary>
        public float AttackCooldownLeft { get; internal set; }

        /// <summary>배회 목적지를 고르는 결정론적 난수 상태(0이면 안 됨).</summary>
        internal uint RngState;

        public bool IsDead => Mode == CreatureMode.Dead;

        /// <summary>체력 비율 0~100.</summary>
        public byte HealthPercent => (byte)Math.Max(0, Math.Min(100, (int)Math.Round(Health / Definition.MaxHealth * 100f)));

        public Creature(ushort id, CreatureDefinition definition, Vec3f position, uint rngSeed)
        {
            Id = id;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Position = position;
            HomeX = position.X;
            HomeZ = position.Z;
            Health = definition.MaxHealth;
            Mode = CreatureMode.Idle;
            RngState = rngSeed == 0 ? 0x9E3779B9u : rngSeed;
        }

        /// <summary>
        /// 피해를 준다. 체력이 0이 되면 사망한다. [FR-079] M1에서는 시험용 최소 처리(드롭·시체 없음).
        /// </summary>
        /// <returns>이번 피해로 죽었으면 true</returns>
        public bool TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return false;
            }
            Health = Math.Max(0f, Health - amount);
            if (Health > 0f)
            {
                return false;
            }
            Mode = CreatureMode.Dead;
            HasTarget = false;
            HasWander = false;
            return true;
        }
    }
}

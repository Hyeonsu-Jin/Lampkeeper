// =============================================================
// 파일: CreatureBrainTests.cs
// 역할: 생물 상태 기계(배회 -> 추적 -> 공격 -> 사망)와 공격 판정 규칙 단위 시험.
// 레이어: test (EditMode, Game.Tests.Domain)
// 관련: FR-079~081, DES-019 / SDD 3.10.6
// =============================================================
using Game.Domain.Common;
using Game.Domain.Creatures;
using NUnit.Framework;

namespace Game.Tests.Domain
{
    public class CreatureBrainTests
    {
        private static readonly CreatureDefinition Def = CreatureDefinition.TestCreature;
        private static readonly PlayerId Player = new PlayerId(7UL);

        private static Creature NewCreature(float x = 0f, float z = 0f, uint seed = 1u) =>
            new Creature(1, Def, new Vec3f(x, 0f, z), seed);

        private static PlayerSighting Sight(float distance, float x = 0f, float z = 0f) =>
            new PlayerSighting(true, Player, new Vec3f(x, 0f, z + distance), distance);

        [Test]
        public void Idle_OutsideDetectRange_StaysIdleAndPicksWanderPoint()
        {
            Creature c = NewCreature(100f, 100f);
            CreatureBrain.Decide(c, Sight(Def.DetectRange + 1f));
            Assert.AreEqual(CreatureMode.Idle, c.Mode);
            Assert.IsTrue(c.HasWander);
            float dx = c.WanderX - 100f;
            float dz = c.WanderZ - 100f;
            Assert.LessOrEqual(dx * dx + dz * dz, Def.WanderRadius * Def.WanderRadius + 0.01f);
        }

        [Test]
        public void Wander_SameSeedGivesSamePoints_DifferentSeedDiffers()
        {
            Creature a = NewCreature(0f, 0f, 5u);
            Creature b = NewCreature(0f, 0f, 5u);
            Creature d = NewCreature(0f, 0f, 6u);
            CreatureBrain.Decide(a, PlayerSighting.None);
            CreatureBrain.Decide(b, PlayerSighting.None);
            CreatureBrain.Decide(d, PlayerSighting.None);
            Assert.AreEqual(a.WanderX, b.WanderX);
            Assert.AreEqual(a.WanderZ, b.WanderZ);
            Assert.AreNotEqual(a.WanderX, d.WanderX);
        }

        [Test]
        public void Wander_KeepsPointUntilArrived()
        {
            Creature c = NewCreature(0f, 0f, 3u);
            CreatureBrain.Decide(c, PlayerSighting.None);
            float wx = c.WanderX;
            float wz = c.WanderZ;
            CreatureBrain.Decide(c, PlayerSighting.None); // 아직 도착 전: 같은 점
            Assert.AreEqual(wx, c.WanderX);
            Assert.AreEqual(wz, c.WanderZ);
            c.Position = new Vec3f(wx, 0f, wz); // 도착
            CreatureBrain.Decide(c, PlayerSighting.None);
            Assert.IsTrue(c.WanderX != wx || c.WanderZ != wz, "도착하면 새 목적지를 고른다.");
        }

        [Test]
        public void Idle_PlayerWithinDetectRange_StartsSeek()
        {
            Creature c = NewCreature();
            CreatureBrain.Decide(c, Sight(Def.DetectRange - 1f));
            Assert.AreEqual(CreatureMode.Seek, c.Mode);
            Assert.IsTrue(c.HasTarget);
            Assert.AreEqual(Player, c.TargetPlayer);
            Assert.IsFalse(c.HasWander);
        }

        [Test]
        public void Seek_InAttackRange_BecomesAttack_AndLeavesOnlyBeyondHysteresis()
        {
            Creature c = NewCreature();
            CreatureBrain.Decide(c, Sight(10f));
            CreatureBrain.Decide(c, Sight(Def.AttackRange - 0.1f));
            Assert.AreEqual(CreatureMode.Attack, c.Mode);

            CreatureBrain.Decide(c, Sight(Def.AttackRange * 1.1f)); // 여유 안: 그대로 공격
            Assert.AreEqual(CreatureMode.Attack, c.Mode);

            CreatureBrain.Decide(c, Sight(Def.AttackRange * CreatureBrain.AttackLeaveFactor + 0.1f));
            Assert.AreEqual(CreatureMode.Seek, c.Mode);
        }

        [Test]
        public void Seek_TargetBeyondLoseRange_ReturnsToIdle()
        {
            Creature c = NewCreature();
            CreatureBrain.Decide(c, Sight(20f));
            Assert.AreEqual(CreatureMode.Seek, c.Mode);
            CreatureBrain.Decide(c, Sight(Def.LoseRange + 1f));
            Assert.AreEqual(CreatureMode.Idle, c.Mode);
            Assert.IsFalse(c.HasTarget);
            CreatureBrain.Decide(c, PlayerSighting.None);
            Assert.AreEqual(CreatureMode.Idle, c.Mode);
        }

        [Test]
        public void Seek_BetweenDetectAndLoseRange_KeepsChasing()
        {
            Creature c = NewCreature();
            CreatureBrain.Decide(c, Sight(20f));
            CreatureBrain.Decide(c, Sight((Def.DetectRange + Def.LoseRange) * 0.5f)); // 탐지 밖이지만 상실 거리 안
            Assert.AreEqual(CreatureMode.Seek, c.Mode);
        }

        [Test]
        public void TakeDamage_ReducesHealth_ThenKillsAndStopsDeciding()
        {
            Creature c = NewCreature();
            Assert.IsFalse(c.TakeDamage(Def.MaxHealth - 1f));
            Assert.AreEqual(2, c.HealthPercent); // 50 중 1 남음 = 2%
            Assert.IsTrue(c.TakeDamage(5f));
            Assert.AreEqual(CreatureMode.Dead, c.Mode);
            Assert.AreEqual(0, c.HealthPercent);
            CreatureBrain.Decide(c, Sight(1f));
            Assert.AreEqual(CreatureMode.Dead, c.Mode, "죽은 생물은 판단하지 않는다.");
            Assert.IsFalse(c.TakeDamage(10f), "이미 죽은 생물은 다시 죽지 않는다.");
        }

        [Test]
        public void ResolveAttack_RequiresAttackModeRangeAngleAndCooldown()
        {
            Creature c = NewCreature();
            Vec3f target = new Vec3f(0f, 0f, 1.5f); // 정면(+Z) 1.5 m
            CreatureBrain.Decide(c, new PlayerSighting(true, Player, target, 1.5f));
            Assert.AreEqual(CreatureMode.Attack, c.Mode);
            Assert.AreEqual(0f, c.YawDegrees, 0.01f);

            Assert.IsTrue(CreatureBrain.ResolveAttack(c, target, 0.033f), "처음에는 쿨다운이 없어 바로 맞는다.");
            Assert.IsFalse(CreatureBrain.ResolveAttack(c, target, 0.033f), "쿨다운 중");
            for (int i = 0; i < 44; i++) CreatureBrain.ResolveAttack(c, target, 1f / 30f); // 1.47초
            Assert.IsTrue(CreatureBrain.ResolveAttack(c, target, 1f / 30f), "쿨다운이 끝나면 다시 맞는다.");

            // 사거리 밖
            CreatureBrain.ResolveAttack(c, new Vec3f(0f, 0f, 50f), 10f); // 쿨다운만 비운다(사거리 밖이라 맞지 않음)
            Assert.IsFalse(CreatureBrain.ResolveAttack(c, new Vec3f(0f, 0f, Def.AttackRange + 0.5f), 0.033f));

            // 각도 밖(등 뒤)
            c.YawDegrees = 180f;
            Assert.IsFalse(CreatureBrain.ResolveAttack(c, target, 0.033f));
        }

        [Test]
        public void ResolveAttack_NotAttackMode_NeverHits()
        {
            Creature c = NewCreature();
            Assert.IsFalse(CreatureBrain.ResolveAttack(c, new Vec3f(0f, 0f, 1f), 0.033f));
        }

        [Test]
        public void FacingYaw_ZeroIsPlusZ_NinetyIsPlusX()
        {
            Assert.AreEqual(0f, CreatureBrain.FacingYawDegrees(Vec3f.Zero, new Vec3f(0f, 0f, 5f)), 0.01f);
            Assert.AreEqual(90f, CreatureBrain.FacingYawDegrees(Vec3f.Zero, new Vec3f(5f, 0f, 0f)), 0.01f);
        }

        [Test]
        public void Definition_RejectsLoseRangeSmallerThanDetect()
        {
            Assert.Throws<System.ArgumentException>(() => new CreatureDefinition(9, 10f, 20f, 10f, 1f, 1f, 1f, 90f, 1f, 2f, 5f));
        }
    }
}

// =============================================================
// 파일: AiSchedulerTests.cs
// 역할: AI LOD 규칙, 판단 분산(10 Hz), 이동 일정, 공격 판정 연결, 시험용 생물 배치, ServerWorld 연결 단위 시험.
// 레이어: test (EditMode, Game.Tests.Application) - 길 찾기 백엔드는 가짜(직선 이동)를 쓴다.
// 관련: FR-079~082, NFR-011, NFR-017, DES-062 / SDD 3.11.10, 3.12.3
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Ai;
using Game.Application.Commands;
using Game.Application.Protocol;
using Game.Application.Server;
using Game.Domain.Common;
using Game.Domain.Creatures;
using NUnit.Framework;

namespace Game.Tests.Application
{
    /// <summary>목표를 향해 곧장 가는 가짜 백엔드(격자·흐름장 없음). 스케줄러 일정만 시험한다.</summary>
    internal sealed class StraightLineBackend : IAiNavigationBackend
    {
        public bool HasWalkGrid => true;
        public bool IsSteeringPending { get; private set; }
        public AiNavStats Stats { get; } = new AiNavStats();
        public int ScheduleCalls;
        public int FlowTargetCalls;
        public int LastFlowTargetCount;

        public void BuildWalkGrid(WalkGridDesc desc) { }
        public bool IsWalkable(float x, float z) => true;

        public void SetFlowTargets(float[] x, float[] z, int count)
        {
            FlowTargetCalls++;
            LastFlowTargetCount = count;
        }

        public void ScheduleSteering(AiAgentBuffer a)
        {
            ScheduleCalls++;
            for (int i = 0; i < a.Count; i++)
            {
                float dx = a.TargetX[i] - a.PosX[i];
                float dz = a.TargetZ[i] - a.PosZ[i];
                float dist = (float)Math.Sqrt(dx * dx + dz * dz);
                float step = a.Speed[i] * a.MoveSeconds[i];
                byte flags = 0;
                float nx = a.PosX[i];
                float nz = a.PosZ[i];
                if (step > 0f && dist > a.StopDistance[i] && dist > 1e-4f)
                {
                    float move = Math.Min(step, dist - a.StopDistance[i]);
                    nx += dx / dist * move;
                    nz += dz / dist * move;
                    flags = AiAgentBuffer.FlagMoved;
                    a.OutYaw[i] = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
                }
                a.OutX[i] = nx;
                a.OutY[i] = 0f;
                a.OutZ[i] = nz;
                a.OutFlags[i] = flags;
            }
            IsSteeringPending = true;
        }

        public void CompleteSteering(AiAgentBuffer a) => IsSteeringPending = false;
        public void Dispose() { }
    }

    public class AiSchedulerTests
    {
        private const float Tick = 1f / 30f;

        private static (AiScheduler ai, StraightLineBackend backend) NewAi(AiSettings settings = null)
        {
            var backend = new StraightLineBackend();
            return (new AiScheduler(settings ?? new AiSettings(), backend, new CreatureRegistry()), backend);
        }

        private static AiPlayerView[] OnePlayer(float x, float z) =>
            new[] { new AiPlayerView(new PlayerId(1UL), new Vec3f(x, 0f, z)) };

        // ----- AI LOD 규칙 -----

        [Test]
        public void Lod_ClassifiesByDistance()
        {
            var lod = new AiLodPolicy(new AiSettings());
            Assert.AreEqual(AiLod.Near, lod.Classify(39f * 39f));
            Assert.AreEqual(AiLod.Mid, lod.Classify(40f * 40f));
            Assert.AreEqual(AiLod.Mid, lod.Classify(119f * 119f));
            Assert.AreEqual(AiLod.Far, lod.Classify(120f * 120f));
            Assert.AreEqual(AiLod.Far, lod.Classify(299f * 299f));
            Assert.AreEqual(AiLod.Frozen, lod.Classify(300f * 300f));
            Assert.AreEqual(AiLod.Frozen, lod.Classify(float.PositiveInfinity));
        }

        [Test]
        public void Lod_Decisions_Near10Hz_Mid5Hz_Far2Hz_FrozenNever()
        {
            var lod = new AiLodPolicy(new AiSettings());
            int[] hits = new int[4];
            const int ticks = 300; // 10초
            for (uint t = 0; t < ticks; t++)
            {
                for (int id = 1; id <= 30; id++)
                {
                    for (int l = 0; l < 4; l++)
                    {
                        if (lod.ShouldDecide((AiLod)l, t, id)) hits[l]++;
                    }
                }
            }
            // 생물 30마리, 10초: Near 100회/마리, Mid 50회, Far 20회
            Assert.AreEqual(30 * 100, hits[(int)AiLod.Near]);
            Assert.AreEqual(30 * 50, hits[(int)AiLod.Mid]);
            Assert.AreEqual(30 * 20, hits[(int)AiLod.Far]);
            Assert.AreEqual(0, hits[(int)AiLod.Frozen]);
        }

        [Test]
        public void Lod_MoveSeconds_FarMovesEveryThirdTickWithTripleTime()
        {
            var lod = new AiLodPolicy(new AiSettings());
            Assert.AreEqual(Tick, lod.MoveSeconds(AiLod.Near, 5, 3, Tick), 1e-6f);
            Assert.AreEqual(Tick, lod.MoveSeconds(AiLod.Mid, 6, 3, Tick), 1e-6f);
            Assert.AreEqual(0f, lod.MoveSeconds(AiLod.Frozen, 6, 3, Tick));
            int moves = 0;
            float total = 0f;
            for (uint t = 0; t < 30; t++)
            {
                float m = lod.MoveSeconds(AiLod.Far, t, 4, Tick);
                if (m > 0f) moves++;
                total += m;
            }
            Assert.AreEqual(10, moves);
            Assert.AreEqual(1f, total, 1e-4f, "Far도 1초 동안 이동 시간 합계는 같다.");
        }

        // ----- 판단 분산 -----

        [Test]
        public void Decide_60Creatures_Near_EachTickOneThird_EveryCreatureOncePerThreeTicks()
        {
            (AiScheduler ai, StraightLineBackend _) = NewAi();
            for (int i = 0; i < 60; i++)
            {
                ai.Registry.Spawn(CreatureDefinition.TestCreature, new Vec3f(10f + i * 0.2f, 0f, 10f), (uint)(i + 1));
            }
            AiPlayerView[] players = OnePlayer(0f, 10f); // 모두 40 m 안(Near)
            for (uint tick = 30; tick < 33; tick++)
            {
                int before = CountSeeking(ai);
                ai.Decide(tick, Tick, players, 1);
                Assert.AreEqual(20, ai.LastDecisionCount, "틱당 20체 판단(예산표 3.12.3).");
                Assert.AreEqual(60, ai.GetLodCount(AiLod.Near));
                Assert.GreaterOrEqual(CountSeeking(ai), before);
            }
            // 3틱이 지나면 모든 생물이 한 번씩 판단해 추적 상태가 되어 있다.
            Assert.AreEqual(60, CountSeeking(ai));
        }

        private static int CountSeeking(AiScheduler ai)
        {
            int n = 0;
            foreach (Creature c in ai.Registry.Creatures)
            {
                if (c.Mode == CreatureMode.Seek) n++;
            }
            return n;
        }

        [Test]
        public void Decide_FrozenCreature_NeitherDecidesNorMoves()
        {
            (AiScheduler ai, StraightLineBackend _) = NewAi();
            Creature far = ai.Registry.Spawn(CreatureDefinition.TestCreature, new Vec3f(500f, 0f, 0f), 1u);
            Vec3f start = far.Position;
            AiPlayerView[] players = OnePlayer(0f, 0f);
            for (uint tick = 0; tick < 12; tick++)
            {
                ai.ApplySteering();
                ai.Decide(tick, Tick, players, 1);
                Assert.AreEqual(0, ai.LastDecisionCount);
            }
            Assert.AreEqual(1, ai.GetLodCount(AiLod.Frozen));
            Assert.AreEqual(start, far.Position);
            Assert.AreEqual(CreatureMode.Idle, far.Mode);
        }

        [Test]
        public void Decide_NoPlayers_AllCreaturesFrozen()
        {
            (AiScheduler ai, StraightLineBackend _) = NewAi();
            ai.Registry.Spawn(CreatureDefinition.TestCreature, new Vec3f(1f, 0f, 1f), 1u);
            ai.Decide(0, Tick, new AiPlayerView[1], 0);
            Assert.AreEqual(1, ai.GetLodCount(AiLod.Frozen));
            Assert.AreEqual(0, ai.LastMovingCount);
        }

        // ----- 이동 일정 -----

        [Test]
        public void Seek_MovesTowardPlayer_AndStopsInAttackRange_ThenAttacks()
        {
            (AiScheduler ai, StraightLineBackend backend) = NewAi();
            Creature c = ai.Registry.Spawn(CreatureDefinition.TestCreature, new Vec3f(20f, 0f, 0f), 11u);
            AiPlayerView[] players = OnePlayer(0f, 0f);
            bool attacked = false;
            for (uint tick = 0; tick < 300 && !attacked; tick++)
            {
                ai.ApplySteering();
                ai.Decide(tick, Tick, players, 1);
                attacked = ai.LastAttacks.Count > 0;
            }
            Assert.IsTrue(attacked, "플레이어에게 닿으면 공격 판정이 나야 한다.");
            Assert.AreEqual(CreatureMode.Attack, c.Mode);
            Assert.LessOrEqual(Vec3f.HorizontalDistance(c.Position, players[0].Position), c.Definition.AttackRange + 0.01f);
            Assert.AreEqual(c.Id, ai.LastAttacks[0].CreatureId);
            Assert.AreEqual(c.Definition.AttackDamage, ai.LastAttacks[0].Damage);
            Assert.Greater(backend.ScheduleCalls, 0);
        }

        [Test]
        public void Seek_RequestsFlowTargets_AtOneHzAndWhenFirstChasing()
        {
            (AiScheduler ai, StraightLineBackend backend) = NewAi();
            ai.Registry.Spawn(CreatureDefinition.TestCreature, new Vec3f(30f, 0f, 0f), 1u);
            AiPlayerView[] players = OnePlayer(0f, 0f);
            for (uint tick = 1; tick <= 95; tick++) // 틱 30·60·90에서 갱신
            {
                ai.ApplySteering();
                ai.Decide(tick, Tick, players, 1);
            }
            Assert.GreaterOrEqual(backend.FlowTargetCalls, 3);
            Assert.LessOrEqual(backend.FlowTargetCalls, 5, "초당 한 번 정도만 흐름장을 다시 계산한다.");
            Assert.AreEqual(1, backend.LastFlowTargetCount);
        }

        [Test]
        public void ScheduleAfterDecideFalse_HostSchedulesFromLateUpdate()
        {
            (AiScheduler ai, StraightLineBackend backend) = NewAi();
            ai.ScheduleAfterDecide = false;
            ai.Registry.Spawn(CreatureDefinition.TestCreature, new Vec3f(10f, 0f, 0f), 1u);
            ai.Decide(0, Tick, OnePlayer(0f, 0f), 1);
            Assert.AreEqual(0, backend.ScheduleCalls);
            Assert.IsTrue(ai.ScheduleSteering());
            Assert.AreEqual(1, backend.ScheduleCalls);
            Assert.IsFalse(ai.ScheduleSteering(), "이미 예약했으면 다시 예약하지 않는다.");
        }

        [Test]
        public void DeadCreature_IsSkipped()
        {
            (AiScheduler ai, StraightLineBackend _) = NewAi();
            Creature c = ai.Registry.Spawn(CreatureDefinition.TestCreature, new Vec3f(5f, 0f, 0f), 1u);
            c.TakeDamage(1000f);
            ai.Decide(0, Tick, OnePlayer(0f, 0f), 1);
            Assert.AreEqual(0, ai.GetLodCount(AiLod.Near));
            Assert.AreEqual(0, ai.LastDecisionCount);
        }

        // ----- 시험용 배치, 목록 -----

        [Test]
        public void TestLayout_SameSeedSamePositions_InsideRing()
        {
            var a = TestCreatureLayout.Generate(60, 42UL, 500f, 500f, null);
            var b = TestCreatureLayout.Generate(60, 42UL, 500f, 500f, null);
            var other = TestCreatureLayout.Generate(60, 43UL, 500f, 500f, null);
            Assert.AreEqual(60, a.Length);
            bool differs = false;
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i], b[i]);
                float d = (float)Math.Sqrt((a[i].X - 500f) * (a[i].X - 500f) + (a[i].Z - 500f) * (a[i].Z - 500f));
                Assert.GreaterOrEqual(d, TestCreatureLayout.MinRadiusMeters - 0.01f);
                Assert.LessOrEqual(d, TestCreatureLayout.MaxRadiusMeters + 0.01f);
                if (a[i] != other[i]) differs = true;
            }
            Assert.IsTrue(differs);
        }

        [Test]
        public void TestLayout_RetriesWhenNotWalkable()
        {
            int calls = 0;
            var spots = TestCreatureLayout.Generate(10, 7UL, 0f, 0f, (x, z) => ++calls % 2 == 0); // 첫 시도는 늘 막힘
            Assert.AreEqual(10, spots.Length);
            Assert.GreaterOrEqual(calls, 20);
        }

        [Test]
        public void Registry_AssignsIncreasingIds_RaisesEvents_RemovesById()
        {
            var reg = new CreatureRegistry();
            var spawned = new List<ushort>();
            var removed = new List<ushort>();
            reg.Spawned += c => spawned.Add(c.Id);
            reg.Despawned += removed.Add;
            Creature a = reg.Spawn(CreatureDefinition.TestCreature, Vec3f.Zero, 1u);
            Creature b = reg.Spawn(CreatureDefinition.TestCreature, Vec3f.Zero, 2u);
            Assert.AreEqual(1, a.Id);
            Assert.AreEqual(2, b.Id);
            Assert.IsTrue(reg.TryGet(2, out Creature found));
            Assert.AreSame(b, found);
            Assert.IsTrue(reg.Remove(1));
            Assert.IsFalse(reg.Remove(1));
            CollectionAssert.AreEqual(new ushort[] { 1, 2 }, spawned);
            CollectionAssert.AreEqual(new ushort[] { 1 }, removed);
            Assert.AreEqual(1, reg.Count);
        }

        // ----- ServerWorld 연결 -----

        private static ServerWorld NewServer()
        {
            var guard = new RequestGuard(30, new CommandLimits(ProtocolConstants.PositionHalfRangeMeters));
            var dispatcher = new CommandDispatcher(guard, Game.Application.Ports.NullLog.Instance);
            return new ServerWorld(new ServerSettings(), dispatcher, new FakeClock(), null, null);
        }

        [Test]
        public void ServerWorld_SpawnTestCreatures_RequiresAi_AndTicksDriveAi()
        {
            ServerWorld world = NewServer();
            Assert.Throws<InvalidOperationException>(() => world.SpawnTestCreatures(5, 1UL));

            var backend = new StraightLineBackend();
            var ai = new AiScheduler(new AiSettings(), backend, new CreatureRegistry());
            world.AttachAi(ai);
            Assert.Throws<InvalidOperationException>(() => world.AttachAi(ai), "두 번 연결할 수 없다.");

            Assert.AreEqual(60, world.SpawnTestCreatures(60, 99UL));
            Assert.AreEqual(60, ai.Registry.Count);

            world.AddPlayer(new PlayerId(1UL), "host", true); // 월드 없이는 (0,0) 근처 스폰, 생물은 중심 둘레 30~150 m
            for (uint tick = 1; tick <= 90; tick++)
            {
                world.Tick(tick, Tick);
            }
            Assert.AreEqual(60, ai.GetLodCount(AiLod.Near) + ai.GetLodCount(AiLod.Mid) + ai.GetLodCount(AiLod.Far) + ai.GetLodCount(AiLod.Frozen));
            Assert.Greater(backend.ScheduleCalls, 0);
        }
    }
}

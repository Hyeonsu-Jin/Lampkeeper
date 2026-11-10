// =============================================================
// 파일: BotBrainTests.cs
// 역할: 봇 행동 결정(BotBrain, BotPlanner)과 참가 시간 기록(JoinTimeline)의 단위 시험.
// 레이어: test (EditMode, Game.Tests.Application)
// 관련: DES-113, NFR-018, NFR-021 / SDD 3.7.4(-bot), 3.12.4, 3.12.6, M1-12
// =============================================================
using System;
using Game.Application.Perf;
using Game.Application.Session;
using NUnit.Framework;

namespace Game.Tests.Application
{
    public class BotBrainTests
    {
        private sealed class FakeWorld : IBotWorld
        {
            public float[] ResourceX = { 60f, 70f };
            public float[] ResourceZ = { 50f, 50f };
            public bool HasCreature = true;
            public float CreatureX = 120f;
            public float CreatureZ = 100f;

            public int FindNearestResources(float x, float z, int[] ids)
            {
                int n = Math.Min(ResourceX.Length, ids.Length);
                for (int i = 0; i < n; i++) ids[i] = i;
                return n;
            }

            public bool TryGetResourcePosition(int index, out float x, out float z)
            {
                x = ResourceX[index];
                z = ResourceZ[index];
                return true;
            }

            public bool TryFindNearestCreature(float x, float z, float maxMeters, out float creatureX, out float creatureZ)
            {
                creatureX = CreatureX;
                creatureZ = CreatureZ;
                return HasCreature;
            }
        }

        // ---------- 경로 ----------

        [Test]
        public void Waypoint_SameInputs_SameResult()
        {
            BotPlanner.Waypoint(42UL, 3, 5, 1536f, 1536f, out float x1, out float z1);
            BotPlanner.Waypoint(42UL, 3, 5, 1536f, 1536f, out float x2, out float z2);
            Assert.AreEqual(x1, x2);
            Assert.AreEqual(z1, z2);
        }

        [Test]
        public void Waypoint_StaysInRingAroundCenter_AndDiffersByBotAndIndex()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int bot = 1; bot <= 7; bot++)
            {
                for (int index = 0; index < 20; index++)
                {
                    BotPlanner.Waypoint(7UL, bot, index, 1000f, 2000f, out float x, out float z);
                    float distance = (float)Math.Sqrt((x - 1000f) * (x - 1000f) + (z - 2000f) * (z - 2000f));
                    Assert.GreaterOrEqual(distance, BotPlanner.WaypointMinRadiusMeters - 0.01f);
                    Assert.LessOrEqual(distance, BotPlanner.WaypointMaxRadiusMeters + 0.01f);
                    seen.Add(x.ToString("F2") + "," + z.ToString("F2"));
                }
            }
            Assert.Greater(seen.Count, 130, "봇·순번이 다르면 지점도 거의 모두 달라야 한다(140개 중)");
        }

        [Test]
        public void Waypoint_DifferentSeed_DifferentRoute()
        {
            BotPlanner.Waypoint(1UL, 2, 0, 0f, 0f, out float x1, out float z1);
            BotPlanner.Waypoint(2UL, 2, 0, 0f, 0f, out float x2, out float z2);
            Assert.IsTrue(x1 != x2 || z1 != z2);
        }

        [Test]
        public void YawDegrees_ZeroIsPlusZ_NinetyIsPlusX()
        {
            Assert.AreEqual(0f, BotPlanner.YawDegrees(0f, 5f), 0.001f);
            Assert.AreEqual(90f, BotPlanner.YawDegrees(5f, 0f), 0.001f);
            Assert.AreEqual(180f, Math.Abs(BotPlanner.YawDegrees(0f, -5f)), 0.001f);
        }

        [Test]
        public void InsertNearest_KeepsSortedAndDropsFarthest()
        {
            var d = new float[3];
            var ids = new int[3];
            int count = 0;
            count = BotPlanner.InsertNearest(d, ids, count, 9f, 1);
            count = BotPlanner.InsertNearest(d, ids, count, 4f, 2);
            count = BotPlanner.InsertNearest(d, ids, count, 6f, 3);
            Assert.AreEqual(3, count);
            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, ids);

            count = BotPlanner.InsertNearest(d, ids, count, 1f, 4);   // 가장 가까운 것이 앞에 들어가고 가장 먼 것(9)이 밀려난다
            count = BotPlanner.InsertNearest(d, ids, count, 50f, 5);  // 너무 멀어 무시
            Assert.AreEqual(3, count);
            CollectionAssert.AreEqual(new[] { 4, 2, 3 }, ids);
        }

        // ---------- 행동 순환 ----------

        [Test]
        public void Brain_StartsInTravel_AndMovesTowardWaypoint()
        {
            var brain = new BotBrain(1UL, 2, 100f, 100f, 2.5f, new FakeWorld());
            Assert.AreEqual(BotPhase.Travel, brain.Phase);
            BotAction a = brain.Tick(0.1f, 100f, 100f, true);
            Assert.IsTrue(a.Move);
            Assert.AreEqual(-1, a.HitResourceIndex);
        }

        [Test]
        public void Brain_RunsOnlyWhenFarAndAllowed()
        {
            var brain = new BotBrain(1UL, 2, 100f, 100f, 2.5f, new FakeWorld());
            Assert.IsTrue(brain.Tick(0.1f, 100f, 100f, true).Run, "경유 지점이 15 m보다 멀면 달린다");
            Assert.IsFalse(brain.Tick(0.1f, 100f, 100f, false).Run, "스태미나가 없으면 달리지 않는다");
        }

        [Test]
        public void Brain_Travel_ArrivalMovesToHarvest_ThenHitsAtInterval()
        {
            var world = new FakeWorld { ResourceX = new[] { 60f, 60f }, ResourceZ = new[] { 50f, 50f } };
            var brain = new BotBrain(1UL, 2, 100f, 100f, 2.5f, world);
            BotPlanner.Waypoint(1UL, 2, 0, 100f, 100f, out float wx, out float wz);
            brain.Tick(0.1f, wx, wz, true); // 도착 → 타격 단계
            Assert.AreEqual(BotPhase.Harvest, brain.Phase);

            // 자원(60,50)(70,50) 중 하나로 이동한 뒤 멈춰 서서 때린다. 자원 바로 옆에서 시작해 이동 단계를 건너뛴다.
            int hits = 0;
            float t = 0f;
            for (int i = 0; i < 40; i++)
            {
                BotAction a = brain.Tick(0.1f, 60f, 50f, true);
                t += 0.1f;
                if (a.HitResourceIndex >= 0) hits++;
            }
            // 4초 동안 0.3초 간격 → 약 14회. 닿는 거리 안에서는 이동하지 않는다.
            Assert.GreaterOrEqual(hits, 10);
            Assert.LessOrEqual(hits, 15);
            Assert.AreEqual(hits, brain.HitsRequested);
        }

        [Test]
        public void Brain_Harvest_FarFromResource_MovesInsteadOfHitting()
        {
            var brain = new BotBrain(1UL, 2, 100f, 100f, 2.5f, new FakeWorld());
            BotPlanner.Waypoint(1UL, 2, 0, 100f, 100f, out float wx, out float wz);
            brain.Tick(0.1f, wx, wz, true);
            BotAction a = brain.Tick(0.1f, 0f, 0f, true);
            Assert.IsTrue(a.Move);
            Assert.AreEqual(-1, a.HitResourceIndex);
        }

        [Test]
        public void Brain_Harvest_NoResource_GoesToChase()
        {
            var world = new FakeWorld { ResourceX = new float[0], ResourceZ = new float[0] };
            var brain = new BotBrain(1UL, 2, 100f, 100f, 2.5f, world);
            BotPlanner.Waypoint(1UL, 2, 0, 100f, 100f, out float wx, out float wz);
            brain.Tick(0.1f, wx, wz, true);
            brain.Tick(0.1f, wx, wz, true);
            Assert.AreEqual(BotPhase.Chase, brain.Phase);
        }

        [Test]
        public void Brain_Chase_MovesTowardCreature_AndNoCreatureAdvancesWaypoint()
        {
            var world = new FakeWorld();
            var brain = new BotBrain(1UL, 2, 100f, 100f, 2.5f, world);
            BotPlanner.Waypoint(1UL, 2, 0, 100f, 100f, out float wx, out float wz);
            brain.Tick(0.1f, wx, wz, true);                 // Harvest
            for (int i = 0; i < 120 && brain.Phase == BotPhase.Harvest; i++) brain.Tick(0.1f, 60f, 50f, true);
            Assert.AreEqual(BotPhase.Chase, brain.Phase, "타격 시간이 지나면 추적 단계");

            BotAction chase = brain.Tick(0.1f, 100f, 100f, true);
            Assert.IsTrue(chase.Move);
            Assert.AreEqual(BotPlanner.YawDegrees(20f, 0f), chase.YawDegrees, 0.01f, "생물(120,100) 쪽을 본다");

            world.HasCreature = false;
            brain.Tick(0.1f, 100f, 100f, true);
            Assert.AreEqual(BotPhase.Travel, brain.Phase);
            Assert.AreEqual(1, brain.WaypointIndex, "한 바퀴 돌면 다음 경유 지점");
        }

        [Test]
        public void Brain_TravelTimeout_MovesOn()
        {
            var brain = new BotBrain(1UL, 2, 100f, 100f, 2.5f, new FakeWorld());
            for (int i = 0; i < 500 && brain.Phase == BotPhase.Travel; i++) brain.Tick(0.1f, 5000f, 5000f, true); // 도착할 수 없는 곳
            Assert.AreNotEqual(BotPhase.Travel, brain.Phase);
        }

        [Test]
        public void Brain_NullWorld_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new BotBrain(1UL, 1, 0f, 0f, 2.5f, null));
        }

        // ---------- 참가 시간 ----------

        [Test]
        public void JoinTimeline_ComputesStageDurations()
        {
            var t = new JoinTimeline();
            t.Mark(ClientSessionState.Connecting, 10.0);
            t.Mark(ClientSessionState.Generating, 12.0);
            t.Mark(ClientSessionState.Syncing, 20.5);
            t.Mark(ClientSessionState.InGame, 23.0);
            Assert.IsTrue(t.IsComplete);
            Assert.AreEqual(2.0, t.ConnectSeconds, 1e-9);
            Assert.AreEqual(8.5, t.GenerateSeconds, 1e-9);
            Assert.AreEqual(2.5, t.SnapshotSeconds, 1e-9);
            Assert.AreEqual(-1.0, t.ViewsSeconds, "봇은 뷰 단계가 없다");
            Assert.AreEqual(13.0, t.TotalSeconds, 1e-9);
            Assert.IsTrue(t.WithinBudget);
        }

        [Test]
        public void JoinTimeline_ViewsReady_ExtendsTotal()
        {
            var t = new JoinTimeline();
            t.Mark(ClientSessionState.Connecting, 0.0);
            t.Mark(ClientSessionState.Generating, 1.0);
            t.Mark(ClientSessionState.Syncing, 30.0);
            t.Mark(ClientSessionState.InGame, 40.0);
            Assert.IsTrue(t.WithinBudget);
            t.MarkViewsReady(70.0);
            Assert.AreEqual(30.0, t.ViewsSeconds, 1e-9);
            Assert.AreEqual(70.0, t.TotalSeconds, 1e-9);
            Assert.IsFalse(t.WithinBudget, "뷰까지 60초를 넘기면 예산 초과");
        }

        [Test]
        public void JoinTimeline_Incomplete_HasNoTotal_AndFirstMarkWins()
        {
            var t = new JoinTimeline();
            t.Mark(ClientSessionState.Connecting, 5.0);
            t.Mark(ClientSessionState.Connecting, 9.0);
            Assert.IsFalse(t.IsComplete);
            Assert.AreEqual(-1.0, t.TotalSeconds);
            Assert.IsFalse(t.WithinBudget);
            t.Mark(ClientSessionState.Generating, 6.0);
            Assert.AreEqual(1.0, t.ConnectSeconds, 1e-9, "같은 상태가 다시 와도 처음 시각을 지킨다");
        }

        [Test]
        public void JoinTimeline_Json_HasStagesAndNullForMissing()
        {
            var t = new JoinTimeline();
            t.Mark(ClientSessionState.Connecting, 0.0);
            t.Mark(ClientSessionState.Generating, 1.0);
            t.Mark(ClientSessionState.Syncing, 2.0);
            t.Mark(ClientSessionState.InGame, 3.5);
            string json = t.ToJson("7\"x", true);
            StringAssert.Contains("\"player\": \"7\\\"x\"", json);
            StringAssert.Contains("\"bot\": true", json);
            StringAssert.Contains("\"generate_s\": 1", json);
            StringAssert.Contains("\"snapshot_s\": 1.5", json);
            StringAssert.Contains("\"views_s\": null", json);
            StringAssert.Contains("\"total_s\": 3.5", json);
            StringAssert.Contains("\"within_budget\": true", json);
        }
    }
}

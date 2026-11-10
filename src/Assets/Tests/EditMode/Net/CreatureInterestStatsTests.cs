// =============================================================
// 파일: CreatureInterestStatsTests.cs
// 역할: 개발용 관심 범위 통계(CreatureInterestStats)의 등급 수·거리 계산을 시험한다.
// 레이어: test (EditMode, Game.Tests.Net)
// 관련: FR-008 / SDD 3.11.9
// =============================================================
using Game.Application.Protocol;
using Game.Application.Replication;
using Game.Domain.Common;
using NUnit.Framework;

namespace Game.Tests.Net
{
    public sealed class CreatureInterestStatsTests
    {
        private static void Add(ClientCreatureStore store, ushort id, float x, float z)
        {
            var state = CreatureState.FromWorld(id, new Vec3f(x, 0f, z), 0f, 0, 100);
            store.ApplySpawn(new CreatureSpawn(1, state), 1, 0d);
        }

        [Test]
        public void Recompute_Empty_GivesZeroCountsAndMinusOneDistances()
        {
            var stats = new CreatureInterestStats(new CreatureInterestSettings());
            stats.Recompute(new ClientCreatureStore(), 0f, 0f);
            Assert.AreEqual(0, stats.Total);
            Assert.AreEqual(-1f, stats.NearestMeters);
            Assert.AreEqual(-1f, stats.FarthestMeters);
        }

        [Test]
        public void Recompute_CountsTiersAndDistances()
        {
            var store = new ClientCreatureStore();
            Add(store, 1, 1020f, 1000f); // 약 20 m: Near
            Add(store, 2, 1100f, 1000f); // 약 100 m: Mid
            Add(store, 3, 1250f, 1000f); // 약 250 m: Far
            Add(store, 4, 1305f, 1000f); // 약 305 m: 여유(10 m) 안이라 Far
            var stats = new CreatureInterestStats(new CreatureInterestSettings());
            stats.Recompute(store, 1000f, 1000f);
            Assert.AreEqual(4, stats.Total);
            Assert.AreEqual(1, stats.Near);
            Assert.AreEqual(1, stats.Mid);
            Assert.AreEqual(2, stats.Far);
            Assert.AreEqual(0, stats.Out);
            Assert.AreEqual(20f, stats.NearestMeters, 0.5f);
            Assert.AreEqual(305f, stats.FarthestMeters, 0.5f);
            Assert.AreEqual(310f, stats.LimitMeters, 0.001f);
        }

        [Test]
        public void Recompute_BeyondLimit_CountsOut()
        {
            var store = new ClientCreatureStore();
            Add(store, 1, 1400f, 1000f); // 400 m
            var stats = new CreatureInterestStats(new CreatureInterestSettings());
            stats.Recompute(store, 1000f, 1000f);
            Assert.AreEqual(1, stats.Out);
        }
    }
}

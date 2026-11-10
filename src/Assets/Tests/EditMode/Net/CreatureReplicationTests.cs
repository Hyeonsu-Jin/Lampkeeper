// =============================================================
// 파일: CreatureReplicationTests.cs
// 역할: 생물 복제(M1-11 2차)를 FishNet 없이 시험한다. 관심 범위 등급 판정, Spawn/Snapshot/Despawn 전환, 중도 참가자, 전송 주기,
//       클라이언트 사본 보간·순서 뒤집힘 방어, 송신량 관찰값.
// 레이어: test (EditMode, Game.Tests.Net) - 길 찾기 백엔드는 움직이지 않는 가짜를 쓴다.
// 관련: FR-008, NFR-013, NFR-017, DES-082, DES-088 / SDD 3.11.9, 3.12.5
// =============================================================
using System.Collections.Generic;
using System.Linq;
using Game.Adapters.Net.Replication;
using Game.Application.Ai;
using Game.Application.Commands;
using Game.Application.Config;
using Game.Application.Protocol;
using Game.Application.Replication;
using Game.Application.Server;
using Game.Domain.Common;
using Game.Domain.Creatures;
using NUnit.Framework;

namespace Game.Tests.Net
{
    /// <summary>움직이지 않는 가짜 백엔드(조향 결과 없음). 복제 규칙만 시험한다.</summary>
    internal sealed class StillAiBackend : IAiNavigationBackend
    {
        public bool HasWalkGrid => true;
        public bool IsSteeringPending => false;
        public AiNavStats Stats { get; } = new AiNavStats();
        public void BuildWalkGrid(WalkGridDesc desc) { }
        public bool IsWalkable(float x, float z) => true;
        public void SetFlowTargets(float[] x, float[] z, int count) { }
        public void ScheduleSteering(AiAgentBuffer agents) { }
        public void CompleteSteering(AiAgentBuffer agents) { }
        public void Dispose() { }
    }

    public class CreatureReplicationTests
    {
        private const float Tick = 1f / 30f;
        private static readonly PlayerId Alice = new PlayerId(1UL);
        private static readonly PlayerId Bob = new PlayerId(2UL);

        private sealed class Rig
        {
            public readonly FakeNetServer Server = new FakeNetServer();
            public readonly ServerWorld World;
            public readonly CreatureRegistry Registry = new CreatureRegistry();
            public readonly CreatureReplicator Replicator;
            public readonly Dictionary<PlayerId, int> Connections = new Dictionary<PlayerId, int>();
            public readonly HashSet<int> Ready = new HashSet<int>();
            public uint TickNumber;

            public Rig()
            {
                var guard = new RequestGuard(30, new CommandLimits(ProtocolConstants.PositionHalfRangeMeters));
                var dispatcher = new CommandDispatcher(guard, new ListLog());
                World = new ServerWorld(new ServerSettings(), dispatcher, new FakeClock(), null, null);
                World.AttachAi(new AiScheduler(new AiSettings(), new StillAiBackend(), Registry));
                Replicator = new CreatureReplicator(Server, World, new NetworkTuning(), TryConnection, id => Ready.Contains(id), new ListLog());
            }

            private bool TryConnection(PlayerId player, out int connectionId) => Connections.TryGetValue(player, out connectionId);

            public int Join(PlayerId player, int connection, float x, float z, bool ready = true)
            {
                World.AddPlayer(player, "p" + player.Value, false);
                Connections[player] = connection;
                if (ready)
                {
                    Ready.Add(connection);
                }
                MovePlayer(player, x, z);
                return connection;
            }

            public void MovePlayer(PlayerId player, float x, float z)
            {
                Assert.IsTrue(World.State.TryGetPlayer(player, out var p));
                p.Position = new Vec3f(x, 0f, z);
            }

            public Creature Spawn(float x, float z) => Registry.Spawn(CreatureDefinition.TestCreature, new Vec3f(x, 0f, z), 1u);

            public void Run(int ticks)
            {
                for (int i = 0; i < ticks; i++)
                {
                    World.Tick(++TickNumber, Tick);
                }
            }

            public int Count<T>(int conn) where T : INetMessage => Server.Count<T>(conn);

            public int StatesFor(int conn, ushort creatureId)
            {
                int n = 0;
                foreach (INetMessage m in Server.To(conn))
                {
                    if (m is CreatureSnapshot s)
                    {
                        n += s.Creatures.Count(c => c.Id == creatureId);
                    }
                }
                return n;
            }
        }

        // ----- 등급 판정 -----

        [Test]
        public void Interest_Classify_UsesTierBoundaries()
        {
            var s = new CreatureInterestSettings();
            Assert.AreEqual(InterestTier.Near, s.Classify(49f * 49f, false));
            Assert.AreEqual(InterestTier.Mid, s.Classify(50f * 50f, false));
            Assert.AreEqual(InterestTier.Mid, s.Classify(149f * 149f, false));
            Assert.AreEqual(InterestTier.Far, s.Classify(150f * 150f, false));
            Assert.AreEqual(InterestTier.Far, s.Classify(299f * 299f, false));
            Assert.AreEqual(InterestTier.Out, s.Classify(300f * 300f, false));
        }

        [Test]
        public void Interest_Hysteresis_KeepsKnownCreaturesSlightlyLonger()
        {
            var s = new CreatureInterestSettings();
            float d = 305f * 305f;
            Assert.AreEqual(InterestTier.Out, s.Classify(d, false), "모르는 생물은 300 m에서 끊는다.");
            Assert.AreEqual(InterestTier.Far, s.Classify(d, true), "이미 보낸 생물은 310 m까지 유지한다.");
            Assert.AreEqual(InterestTier.Out, s.Classify(311f * 311f, true));
        }

        [Test]
        public void Interest_FromConfig_DerivesIntervalsFromHz()
        {
            CreatureInterestSettings s = CreatureInterestSettings.FromConfig(new NetworkTuning());
            Assert.AreEqual(1, s.NearIntervalTicks);  // 30 Hz
            Assert.AreEqual(3, s.MidIntervalTicks);   // 10 Hz
            Assert.AreEqual(15, s.FarIntervalTicks);  // 2 Hz
            Assert.AreEqual(300f, s.FarMeters);
            Assert.AreEqual(0, s.IntervalOf(InterestTier.Out));
        }

        // ----- 복제 송신 -----

        [Test]
        public void Replicator_SendsSpawnOnce_ThenSnapshotsAtTierRate()
        {
            var rig = new Rig();
            int conn = rig.Join(Alice, 1, 0f, 0f);
            Creature near = rig.Spawn(20f, 0f);   // Near: 매 틱
            Creature mid = rig.Spawn(100f, 0f);   // Mid: 3틱마다
            Creature far = rig.Spawn(200f, 0f);   // Far: 15틱마다
            rig.Run(1);

            Assert.AreEqual(3, rig.Count<CreatureSpawn>(conn), "처음 본 생물은 한 번씩 Spawn.");
            Assert.IsTrue(rig.Replicator.Knows(conn, near.Id));

            rig.Server.Sent.Clear();
            rig.Run(30); // 1초
            Assert.AreEqual(0, rig.Count<CreatureSpawn>(conn), "이미 아는 생물은 다시 Spawn하지 않는다.");
            Assert.AreEqual(30, rig.StatesFor(conn, near.Id), "Near 30 Hz");
            Assert.AreEqual(10, rig.StatesFor(conn, mid.Id), "Mid 10 Hz");
            Assert.AreEqual(2, rig.StatesFor(conn, far.Id), "Far 2 Hz");
            Assert.IsTrue(rig.Server.Sent.Where(s => s.Message is CreatureSnapshot).All(s => s.Channel == NetChannel.Unreliable), "스냅샷은 비신뢰 채널.");
            Assert.IsTrue(rig.Server.Sent.Where(s => s.Message is CreatureSpawn).All(s => s.Channel == NetChannel.Reliable));
        }

        [Test]
        public void Replicator_OutOfRange_IsNeverSent_AndEntering_SendsSpawn_LeavingSendsDespawn()
        {
            var rig = new Rig();
            int conn = rig.Join(Alice, 1, 0f, 0f);
            Creature c = rig.Spawn(400f, 0f);
            rig.Run(3);
            Assert.AreEqual(0, rig.Server.To(conn).Count(m => m is CreatureSpawn || m is CreatureSnapshot), "범위 밖은 아무것도 안 보낸다.");

            rig.MovePlayer(Alice, 150f, 0f); // 거리 250 m: Far
            rig.Run(1);
            Assert.AreEqual(1, rig.Count<CreatureSpawn>(conn));

            rig.MovePlayer(Alice, 0f, 0f);   // 거리 400 m: 범위 밖
            rig.Run(1);
            Assert.AreEqual(1, rig.Count<CreatureDespawn>(conn));
            Assert.IsFalse(rig.Replicator.Knows(conn, c.Id));
            Assert.AreEqual(c.Id, rig.Server.To(conn).OfType<CreatureDespawn>().Single().Id);
        }

        [Test]
        public void Replicator_BoundaryJitter_DoesNotFlapSpawnDespawn()
        {
            var rig = new Rig();
            int conn = rig.Join(Alice, 1, 0f, 0f);
            rig.Spawn(298f, 0f);
            rig.Run(1);
            for (int i = 0; i < 20; i++)
            {
                rig.MovePlayer(Alice, i % 2 == 0 ? -4f : 0f, 0f); // 거리 302 ↔ 298 m
                rig.Run(1);
            }
            Assert.AreEqual(1, rig.Count<CreatureSpawn>(conn));
            Assert.AreEqual(0, rig.Count<CreatureDespawn>(conn), "히스테리시스 안에서는 Despawn하지 않는다.");
        }

        [Test]
        public void Replicator_RegistryRemoval_DespawnsEvenInsideRange()
        {
            var rig = new Rig();
            int conn = rig.Join(Alice, 1, 0f, 0f);
            Creature c = rig.Spawn(10f, 0f);
            rig.Run(2);
            Assert.IsTrue(rig.Registry.Remove(c.Id));
            rig.Run(1);
            Assert.AreEqual(1, rig.Count<CreatureDespawn>(conn));
            Assert.AreEqual(0, rig.Replicator.KnownCount(conn));
        }

        [Test]
        public void Replicator_LateJoiner_ReceivesInRangeCreatures_AndOnlyAfterReady()
        {
            var rig = new Rig();
            rig.Join(Alice, 1, 0f, 0f);
            rig.Spawn(10f, 0f);
            rig.Spawn(500f, 0f);
            rig.Run(10);

            // Bob은 스냅샷 동기화가 끝나기 전에는 생물을 받지 않는다.
            rig.Join(Bob, 2, 495f, 0f, ready: false);
            rig.Run(3);
            Assert.AreEqual(0, rig.Server.To(2).Count);

            rig.Ready.Add(2);
            rig.Run(1);
            Assert.AreEqual(1, rig.Count<CreatureSpawn>(2), "Bob 근처의 생물(500 m) 한 마리만 받는다.");
            Assert.AreEqual(1, rig.Count<CreatureSpawn>(1), "Alice는 자기 근처 한 마리만 알고 있다(추가 Spawn 없음).");
        }

        [Test]
        public void Replicator_PerParticipant_IndependentInterest()
        {
            var rig = new Rig();
            rig.Join(Alice, 1, 0f, 0f);
            rig.Join(Bob, 2, 1000f, 0f);
            Creature a = rig.Spawn(10f, 0f);
            Creature b = rig.Spawn(1010f, 0f);
            rig.Run(2);
            Assert.IsTrue(rig.Replicator.Knows(1, a.Id));
            Assert.IsFalse(rig.Replicator.Knows(1, b.Id));
            Assert.IsTrue(rig.Replicator.Knows(2, b.Id));
            Assert.IsFalse(rig.Replicator.Knows(2, a.Id));
        }

        [Test]
        public void Replicator_PlayerLeaves_ForgetsConnection_SoRejoinGetsSpawnAgain()
        {
            var rig = new Rig();
            int conn = rig.Join(Alice, 1, 0f, 0f);
            rig.Spawn(10f, 0f);
            rig.Run(2);
            rig.Connections.Remove(Alice);
            rig.Run(1);
            Assert.AreEqual(0, rig.Replicator.KnownCount(conn));
            rig.Connections[Alice] = 1;
            rig.Run(1);
            Assert.AreEqual(2, rig.Count<CreatureSpawn>(conn));
        }

        [Test]
        public void Replicator_MoreThanBatchLimit_SplitsSnapshots()
        {
            var rig = new Rig();
            int conn = rig.Join(Alice, 1, 0f, 0f);
            int total = ProtocolConstants.MaxCreaturesPerSnapshot + 40;
            for (int i = 0; i < total; i++)
            {
                rig.Spawn(1f + i * 0.1f, 0f);
            }
            rig.Run(1);
            rig.Server.Sent.Clear();
            rig.Run(1);
            var snapshots = rig.Server.To(conn).OfType<CreatureSnapshot>().ToList();
            Assert.AreEqual(2, snapshots.Count);
            Assert.AreEqual(total, snapshots.Sum(s => s.Creatures.Length));
            Assert.IsTrue(snapshots.All(s => s.Creatures.Length <= ProtocolConstants.MaxCreaturesPerSnapshot));
        }

        [Test]
        public void Replicator_SixtyCreatures_OneParticipant_BytesPerSecondIsWithinBudget()
        {
            // 관찰값: 참가자 1명이 받는 생물 복제량. SDD 3.12.5 추정 7.9 KB/s(등급 평균 12 Hz, 60체).
            var rig = new Rig();
            int conn = rig.Join(Alice, 1, 0f, 0f);
            var layout = TestCreatureLayout.Generate(60, 99UL, 0f, 0f, null);
            foreach (var spot in layout)
            {
                rig.Spawn(spot.X, spot.Z);
            }
            rig.Run(1); // Spawn
            long before = rig.Replicator.BytesSent;
            rig.Run(300); // 10초
            double bytesPerSecond = (rig.Replicator.BytesSent - before) / 10.0;
            UnityEngine.Debug.Log("[M1-11 관찰] 생물 60체 복제 송신량(본문 추정, 참가자 1명) = " + bytesPerSecond.ToString("F0") + " B/s, 스냅샷 항목 "
                + rig.Replicator.StatesSent + ", Spawn " + rig.Replicator.SpawnsSent);
            Assert.Less(bytesPerSecond, 12000.0, "SDD 추정 7.9 KB/s 근처(최악 가정 12 KB/s 이하).");
            Assert.Greater(bytesPerSecond, 0.0);
        }

        // ----- 클라이언트 사본 -----

        private static CreatureState StateAt(ushort id, float x, float z, byte anim = 0) =>
            CreatureState.FromWorld(id, new Vec3f(x, 0f, z), 0f, anim, 100);

        [Test]
        public void ClientStore_SpawnSnapshotDespawn_Roundtrip()
        {
            var store = new ClientCreatureStore();
            store.ApplySpawn(new CreatureSpawn(1, StateAt(5, 100f, 200f)), 0u, 0d);
            Assert.AreEqual(1, store.Count);
            Assert.IsTrue(store.TryGet(5, out ClientCreature c));
            Assert.AreEqual(100f, c.LatestPosition.X, 0.03f);

            store.ApplySnapshot(new CreatureSnapshot(10u, new[] { StateAt(5, 110f, 200f, 1), StateAt(99, 0f, 0f) }), 1d);
            Assert.AreEqual(110f, c.LatestPosition.X, 0.03f);
            Assert.AreEqual(1, c.Anim);
            Assert.AreEqual(1, store.StatesReceived, "Spawn을 못 받은 번호 99는 버린다.");

            store.ApplyDespawn(5);
            Assert.AreEqual(0, store.Count);
            store.ApplyDespawn(5); // 없는 번호는 무시
        }

        [Test]
        public void ClientStore_OldTick_IsIgnored()
        {
            var store = new ClientCreatureStore();
            store.ApplySpawn(new CreatureSpawn(1, StateAt(5, 0f, 0f)), 0u, 0d);
            store.ApplySnapshot(new CreatureSnapshot(20u, new[] { StateAt(5, 20f, 0f) }), 1d);
            store.ApplySnapshot(new CreatureSnapshot(10u, new[] { StateAt(5, 10f, 0f) }), 1.1d); // 뒤집혀 늦게 도착
            store.TryGet(5, out ClientCreature c);
            Assert.AreEqual(20f, c.LatestPosition.X, 0.03f);
        }

        [Test]
        public void ClientCreature_Interpolates_AndStartsFromShownPosition()
        {
            var store = new ClientCreatureStore();
            store.ApplySpawn(new CreatureSpawn(1, StateAt(5, 100f, 0f)), 0u, 0d);
            store.TryGet(5, out ClientCreature c);
            // 0.1초 간격(기본 추정)으로 100 → 110 m
            store.ApplySnapshot(new CreatureSnapshot(3u, new[] { StateAt(5, 110f, 0f) }), 0.1d);
            c.Sample(0.1d, out Vec3f start, out _);
            Assert.AreEqual(100f, start.X, 0.05f, "받은 순간에는 보이던 위치에서 시작(점프 없음).");
            c.Sample(0.15d, out Vec3f mid, out _);
            Assert.Greater(mid.X, 100.5f);
            Assert.Less(mid.X, 110f);
            c.Sample(5d, out Vec3f end, out _);
            Assert.AreEqual(110f, end.X, 0.05f, "충분히 지나면 목표에 멈춘다(외삽 없음).");
        }

        [Test]
        public void ClientCreature_Yaw_TakesShortWayAround360()
        {
            var store = new ClientCreatureStore();
            store.ApplySpawn(new CreatureSpawn(1, CreatureState.FromWorld(5, Vec3f.Zero, 350f, 0, 100)), 0u, 0d);
            store.TryGet(5, out ClientCreature c);
            store.ApplySnapshot(new CreatureSnapshot(3u, new[] { CreatureState.FromWorld(5, Vec3f.Zero, 10f, 0, 100) }), 0.1d);
            c.Sample(0.1d + 0.05d, out _, out float yaw);
            Assert.IsTrue(yaw > 350f || yaw < 12f, "350 → 10도는 짧은 쪽(0도 경유)으로 돈다. yaw=" + yaw);
        }

        [Test]
        public void ClientMirror_Reset_ClearsCreatures()
        {
            var mirror = new ClientWorldMirror();
            mirror.Creatures.ApplySpawn(new CreatureSpawn(1, StateAt(5, 0f, 0f)), 0u, 0d);
            mirror.Reset();
            Assert.AreEqual(0, mirror.Creatures.Count);
        }
    }
}

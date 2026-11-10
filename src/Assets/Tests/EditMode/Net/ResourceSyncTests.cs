// =============================================================
// 파일: ResourceSyncTests.cs
// 역할: 자원 2만 개 동기화(M1-10)를 FishNet 없이 시험한다. 5,000개가 바뀐 월드에 중도 참가 → 스냅샷 + 델타 = 호스트 상태,
//       타격 명령 검증(거리·대상 상태·값 범위), 스냅샷 전 델타 보관, 바이트 크기·시간 관찰값 기록.
// 레이어: test (EditMode, Game.Tests.Net)
// 관련: FR-007, FR-009, FR-051, NFR-016, NFR-018, DES-008, DES-057, DES-072 / SDD 3.9.2, 3.9.3, 3.11.8
// =============================================================
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Adapters.Net.Replication;
using Game.Application.Commands;
using Game.Application.Gathering;
using Game.Application.Protocol;
using Game.Application.Replication;
using Game.Application.WorldSync;
using Game.Domain.Common;
using Game.Domain.Gathering;
using NUnit.Framework;

namespace Game.Tests.Net
{
    public class ResourceSyncTests
    {
        private const ulong Seed = 0xC0FFEEUL;
        private const ulong DevId = 11UL;
        private const int Conn = 1;

        private static LocalWorld _full;
        private static LocalWorld _small;

        private static LocalWorld Full => _full ?? (_full = LocalWorld.Generate(Seed, LocalWorld.GeneratorV0, WorldScale.Full));
        private static LocalWorld Small => _small ?? (_small = LocalWorld.Generate(Seed, LocalWorld.GeneratorV0, WorldScale.Small));

        private static void Deliver(FakeClientBridge client, IEnumerable<INetMessage> messages)
        {
            foreach (INetMessage message in messages)
            {
                client.Deliver(message);
            }
        }

        private static void AssertTablesEqual(ReplicatedTable<ResourceState> expected, ReplicatedTable<ResourceState> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count, "바뀐 자원 수");
            foreach (KeyValuePair<uint, ResourceState> e in expected.Entries)
            {
                Assert.IsTrue(actual.TryGet(e.Key, out ResourceState got), "항목 없음 " + e.Key);
                Assert.AreEqual(e.Value, got, "상태 다름 " + e.Key);
            }
        }

        /// <summary>종류가 kind이고 아직 손대지 않은 자원 번호 하나.</summary>
        private static int FindPristine(LocalWorld world, ReplicatedTable<ResourceState> table, ResourceKind kind)
        {
            for (int i = 0; i < world.ResourceCount; i++)
            {
                world.TryGetResource(i, out ResourceKind k, out _);
                if (k == kind && !table.TryGet((uint)i, out _))
                {
                    return i;
                }
            }
            Assert.Fail("해당 종류의 손대지 않은 자원이 없다: " + kind);
            return -1;
        }

        private static void PlacePlayerAt(Rig rig, ulong devId, LocalWorld world, int resource, float offsetMeters)
        {
            world.TryGetResource(resource, out _, out Vec3f p);
            Assert.IsTrue(rig.World.State.TryGetPlayer(new PlayerId(devId), out var player));
            player.Position = new Vec3f(p.X + offsetMeters, p.Y, p.Z);
        }

        private static CommandResultMessage Hit(Rig rig, uint requestId, int resource, byte slot = 0)
        {
            Assert.IsTrue(CommandCodec.TryEncode(new HitResourceCommand(requestId, (ushort)resource, slot), out CommandMessage message));
            int before = rig.Server.Sent.Count;
            rig.Server.SimulateReceive(Conn, message);
            rig.Tick(8); // 약 0.27초: 빈도 상한(초당 4회) 안에서 연속 타격이 되도록 시계를 흘린다
            return rig.Server.Sent.Skip(before).Select(s => s.Message).OfType<CommandResultMessage>().Last(r => r.RequestId == requestId);
        }

        [Test]
        public void FullWorld_HasAboutTwentyThousandResources()
        {
            Assert.That(Full.ResourceCount, Is.InRange(15000, 25000), "NFR-016: 자원 약 2만 개");
            Assert.Less(Full.ResourceCount, ushort.MaxValue, "자원 번호는 2바이트 키에 담겨야 한다");
        }

        [Test]
        public void ChangeTool_IsDeterministic_AndChangesExactlyRequestedCount()
        {
            var a = new ReplicatedTable<ResourceState>(TableIds.Resources, ResourceStateCodec.Instance);
            var b = new ReplicatedTable<ResourceState>(TableIds.Resources, ResourceStateCodec.Instance);
            Assert.AreEqual(5000, ResourceChangeTool.ApplyChanges(a, Full, ResourceChangeTool.DefaultChangedCount, 7UL));
            ResourceChangeTool.ApplyChanges(b, Full, ResourceChangeTool.DefaultChangedCount, 7UL);
            Assert.AreEqual(5000, a.Count);
            AssertTablesEqual(a, b);
            Assert.IsTrue(a.Entries.Any(e => e.Value.Phase == ResourcePhase.Felled));
            Assert.IsTrue(a.Entries.Any(e => e.Value.Phase == ResourcePhase.Broken));
            Assert.IsTrue(a.Entries.Any(e => e.Value.Phase == ResourcePhase.Damaged));
        }

        [Test]
        public void LateJoin_With5000Changes_SnapshotEqualsHost_ThenHitDeltaIsApplied()
        {
            var rig = new Rig();
            rig.World.AttachWorld(Full);
            ResourceChangeTool.ApplyChanges(rig.World.State.Resources, Full, ResourceChangeTool.DefaultChangedCount, 99UL);
            rig.Tick(); // 변경 기록을 비운다(연결이 없어 보내지 않음)

            // 중도 참가: 자원 스냅샷이 속도 제한 안에서 여러 틱에 걸쳐 온다.
            rig.Server.SimulateConnect(Conn, "127.0.0.1");
            rig.Server.SimulateReceive(Conn, Rig.Request(DevId));
            rig.Server.SimulateReceive(Conn, new WorldReady(0UL));
            int ticks = 0;
            while (rig.Server.Count<JoinComplete>(Conn) == 0 && ticks++ < 3000)
            {
                rig.Tick();
            }
            Assert.AreEqual(1, rig.Server.Count<JoinComplete>(Conn), "스냅샷이 끝나고 JoinComplete가 왔다");

            var mirror = new ClientWorldMirror();
            var client = new FakeClientBridge();
            var receiver = new ClientReplicationReceiver(client, mirror, null);
            List<INetMessage> sent = rig.Server.To(Conn);
            var watch = Stopwatch.StartNew();
            Deliver(client, sent);
            watch.Stop();

            AssertTablesEqual(rig.World.State.Resources, mirror.Resources);
            long chunkBytes = sent.OfType<SnapshotChunk>().Where(c => c.TableId == TableIds.Resources).Sum(c => (long)c.Payload.Length);
            int chunks = sent.OfType<SnapshotChunk>().Count(c => c.TableId == TableIds.Resources);
            // 항목 4바이트 × 5,000 + 조각마다 개수 머리 4바이트.
            Assert.AreEqual(5000 * ResourceStateCodec.EntryBytes + chunks * 4, chunkBytes);
            UnityEngine.Debug.Log("[M1-10] 자원 스냅샷 " + chunkBytes + " B, 조각 " + chunks + "개, 서버 틱 " + ticks + "개(" + (ticks / 30.0).ToString("F2")
                + "초 분량), 클라이언트 적용 " + watch.ElapsedMilliseconds + " ms, 월드 생성 " + Full.BuildMilliseconds + " ms");

            // 이후 타격 델타: 서 있는 나무를 한 번 때리면 그 항목 하나만 델타로 온다.
            int tree = FindPristine(Full, rig.World.State.Resources, ResourceKind.Tree);
            PlacePlayerAt(rig, DevId, Full, tree, 1.0f);
            int deltasBefore = rig.Server.Count<TableDelta>(Conn);
            int sentBefore = rig.Server.Sent.Count;
            CommandResultMessage result = Hit(rig, 1u, tree);
            Assert.IsTrue(result.Ok, result.Rejection.ToString());
            List<TableDelta> deltas = rig.Server.Sent.Skip(sentBefore).Select(s => s.Message).OfType<TableDelta>().Where(d => d.TableId == TableIds.Resources).ToList();
            Assert.AreEqual(1, deltas.Count);
            Assert.AreEqual(2 + ResourceStateCodec.EntryBytes + 2, deltas[0].Payload.Length, "델타 본문 = 바뀐 개수 2 + 항목 4 + 지운 개수 2");
            Assert.Greater(rig.Server.Count<TableDelta>(Conn), deltasBefore);
            Deliver(client, deltas.Cast<INetMessage>());
            AssertTablesEqual(rig.World.State.Resources, mirror.Resources);
            Assert.IsTrue(mirror.Resources.TryGet((uint)tree, out ResourceState hitState));
            Assert.AreEqual(ResourcePhase.Damaged, hitState.Phase);
            Assert.AreEqual(1, hitState.Hits);
        }

        [Test]
        public void Hit_FiveTimes_FellsTree_OnceAndSixthIsRejected()
        {
            var rig = new Rig();
            rig.World.AttachWorld(Small);
            rig.JoinFully(Conn, DevId);
            int tree = FindPristine(Small, rig.World.State.Resources, ResourceKind.Tree);
            PlacePlayerAt(rig, DevId, Small, tree, -1.5f);

            for (uint i = 1; i <= 5; i++)
            {
                Assert.IsTrue(Hit(rig, i, tree).Ok, "타격 " + i);
            }
            rig.World.State.Resources.TryGet((uint)tree, out ResourceState state);
            Assert.AreEqual(ResourcePhase.Felled, state.Phase);
            Assert.AreEqual(5, state.Hits);

            CommandResultMessage sixth = Hit(rig, 6u, tree);
            Assert.IsFalse(sixth.Ok, "[FR-051] 이미 쓰러진 나무는 다시 처리하지 않는다");
            Assert.AreEqual(ReasonCode.NotAllowedNow, sixth.Rejection.Reason);
            rig.World.State.Resources.TryGet((uint)tree, out ResourceState after);
            Assert.AreEqual(state, after);
        }

        [Test]
        public void Hit_FromFarAway_IsRejected_AndChangesNothing()
        {
            var rig = new Rig();
            rig.World.AttachWorld(Small);
            rig.JoinFully(Conn, DevId);
            int tree = FindPristine(Small, rig.World.State.Resources, ResourceKind.Tree);
            PlacePlayerAt(rig, DevId, Small, tree, 40f);

            CommandResultMessage result = Hit(rig, 1u, tree);
            Assert.IsFalse(result.Ok);
            Assert.AreEqual(ReasonCode.OutOfRange, result.Rejection.Reason);
            Assert.AreEqual(0, rig.World.State.Resources.Count);
        }

        [Test]
        public void Hit_WithBadValues_IsRejected()
        {
            var rig = new Rig();
            rig.World.AttachWorld(Small);
            rig.JoinFully(Conn, DevId);

            CommandResultMessage missing = Hit(rig, 1u, ushort.MaxValue);
            Assert.AreEqual(ReasonCode.TargetMissing, missing.Rejection.Reason, "없는 자원 번호");

            int tree = FindPristine(Small, rig.World.State.Resources, ResourceKind.Tree);
            PlacePlayerAt(rig, DevId, Small, tree, 1f);
            CommandResultMessage badSlot = Hit(rig, 2u, tree, (byte)(HitResourceCommand.MaxHandSlot + 1));
            Assert.AreEqual(ReasonCode.InvalidValue, badSlot.Rejection.Reason, "손 칸 범위 밖");
            Assert.AreEqual(0, rig.World.State.Resources.Count);
        }

        [Test]
        public void TwoPlayersHittingSameTree_FellItOnlyOnce()
        {
            var rig = new Rig();
            rig.World.AttachWorld(Small);
            rig.JoinFully(Conn, DevId);
            rig.JoinFully(2, 12UL);
            int tree = FindPristine(Small, rig.World.State.Resources, ResourceKind.Tree);
            PlacePlayerAt(rig, DevId, Small, tree, 1f);
            PlacePlayerAt(rig, 12UL, Small, tree, -1f);

            // 두 사람이 같은 틱에 번갈아 4번씩(총 8번) 보낸다. 호스트가 도착 순서대로 하나씩 처리한다.
            int felledDeltas = 0;
            int before = rig.Server.Sent.Count;
            for (uint i = 0; i < 4; i++)
            {
                Assert.IsTrue(CommandCodec.TryEncode(new HitResourceCommand(100 + i, (ushort)tree, 0), out CommandMessage m1));
                Assert.IsTrue(CommandCodec.TryEncode(new HitResourceCommand(200 + i, (ushort)tree, 0), out CommandMessage m2));
                rig.Server.SimulateReceive(Conn, m1);
                rig.Server.SimulateReceive(2, m2);
                rig.Tick(8);
            }
            foreach (TableDelta d in rig.Server.Sent.Skip(before).Where(s => s.Conn == Conn).Select(s => s.Message).OfType<TableDelta>().Where(d => d.TableId == TableIds.Resources))
            {
                var table = new ReplicatedTable<ResourceState>(TableIds.Resources, ResourceStateCodec.Instance);
                table.ApplyPayload(d.Payload);
                if (table.Entries.Any(e => e.Value.Phase == ResourcePhase.Felled))
                {
                    felledDeltas++;
                }
            }
            Assert.AreEqual(1, felledDeltas, "쓰러짐 델타는 한 번만");
            rig.World.State.Resources.TryGet((uint)tree, out ResourceState state);
            Assert.AreEqual(ResourcePhase.Felled, state.Phase);
            Assert.AreEqual(5, state.Hits, "여덟 번 보냈어도 다섯 번까지만 인정");
        }

        [Test]
        public void Receiver_HoldsDeltasUntilJoinComplete_ThenAppliesInOrder()
        {
            var mirror = new ClientWorldMirror();
            var client = new FakeClientBridge();
            var receiver = new ClientReplicationReceiver(client, mirror, null);
            var table = new ReplicatedTable<ResourceState>(TableIds.Resources, ResourceStateCodec.Instance);
            table.Set(7, new ResourceState(5, ResourcePhase.Felled, 3));
            byte[] payload = table.CaptureSnapshot(ProtocolConstants.MaxChunkPayloadBytes)[0];

            client.Deliver(new JoinAccepted("s", 1UL, 0, 0UL, 1u, 8, Game.Domain.Session.SessionAccess.FriendsOnly, new PlayerId(5), 0));
            client.Deliver(new TableDelta(2u, TableIds.Resources, payload)); // 스냅샷 전에 델타가 먼저 도착
            Assert.AreEqual(1, receiver.HeldDeltaCount);
            Assert.AreEqual(0, mirror.Resources.Count, "보관 중에는 적용하지 않는다");

            client.Deliver(new JoinComplete(2u, Vec3f.Zero));
            Assert.AreEqual(0, receiver.HeldDeltaCount);
            Assert.IsTrue(mirror.Resources.TryGet(7, out ResourceState got));
            Assert.AreEqual(new ResourceState(5, ResourcePhase.Felled, 3), got);
        }
    }
}

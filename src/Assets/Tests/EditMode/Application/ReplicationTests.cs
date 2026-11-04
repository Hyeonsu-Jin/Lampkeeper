// =============================================================
// 파일: ReplicationTests.cs
// 역할: 복제 표(ReplicatedTable), 접속자 명단 코덱, 클라이언트 월드 사본(ClientWorldMirror), 아바타 보간(RemoteAvatarTrack) 시험.
// 레이어: test (EditMode, Game.Tests.Application)
// 관련: FR-008, FR-009, NFR-013, NFR-016, DES-053, DES-072 / SDD 3.11.8
// =============================================================
using System.Collections.Generic;
using System.Linq;
using Game.Application.Protocol;
using Game.Application.Replication;
using Game.Domain.Common;
using NUnit.Framework;

namespace Game.Tests.Application
{
    /// <summary>자원 표와 같은 모양(키 2B + 상태 2B = 4B)의 시험용 상태 코덱.</summary>
    internal sealed class ResourceLikeCodec : IStateCodec<ushort>
    {
        public int KeyBytes => 2;
        public int MaxStateBytes => 2;
        public void Write(ByteWriter writer, in ushort state) => writer.WriteU16(state);

        public bool TryRead(ByteReader reader, out ushort state)
        {
            state = reader.ReadU16();
            return !reader.Failed;
        }
    }

    public class ReplicatedTableTests
    {
        private static ReplicatedTable<ushort> NewTable() => new ReplicatedTable<ushort>(2, new ResourceLikeCodec());

        [Test]
        public void DeltaPayload_FixedBytes_EntryIsFourBytes()
        {
            // [3.11.8] 자원 항목 = 인덱스 2B + 상태 2B = 4B. 본문 = [개수 2B] + 항목 + [지운 개수 2B]
            ReplicatedTable<ushort> table = NewTable();
            table.Set(5, 0x0102);
            IReadOnlyList<byte[]> payloads = table.DrainJournal(ProtocolConstants.MaxChunkPayloadBytes);
            Assert.AreEqual(1, payloads.Count);
            CollectionAssert.AreEqual(new byte[] { 1, 0, 5, 0, 2, 1, 0, 0 }, payloads[0]);

            table.Set(1, 1);
            table.Set(2, 2);
            table.Set(3, 3);
            Assert.AreEqual(2 + 3 * 4 + 2, table.DrainJournal(ProtocolConstants.MaxChunkPayloadBytes)[0].Length);
        }

        [Test]
        public void DrainJournal_ClearsChanges_AndKeepsInsertionOrder()
        {
            ReplicatedTable<ushort> table = NewTable();
            table.Set(9, 1);
            table.Set(3, 2);
            table.Set(9, 3); // 같은 키를 다시 바꿔도 한 번만, 처음 순서 유지
            Assert.IsTrue(table.HasChanges);
            byte[] payload = table.DrainJournal(1024)[0];
            Assert.IsFalse(table.HasChanges);
            Assert.AreEqual(0, table.DrainJournal(1024).Count, "변경이 없으면 빈 목록");

            ReplicatedTable<ushort> mirror = NewTable();
            Assert.IsTrue(mirror.ApplyPayload(payload));
            Assert.IsTrue(mirror.TryGet(9, out ushort a));
            Assert.AreEqual(3, a);
            Assert.AreEqual(9, ((ushort)payload[2]) | (payload[3] << 8), "첫 항목이 먼저 바뀐 키(9)");
        }

        [Test]
        public void SnapshotPlusDeltas_EqualHostState()
        {
            // 새 참가자: 스냅샷을 받은 뒤 이어서 오는 델타를 적용하면 호스트 상태와 같다. [FR-009]
            ReplicatedTable<ushort> host = NewTable();
            for (ushort i = 0; i < 500; i++)
            {
                host.Set(i, (ushort)(i * 3));
            }
            host.DrainJournal(1024); // 기존 델타는 이미 나간 것으로 본다

            ReplicatedTable<ushort> client = NewTable();
            foreach (byte[] chunk in host.CaptureSnapshot(256))
            {
                Assert.IsTrue(client.ApplyPayload(chunk));
            }

            host.Set(10, 9999);
            host.Remove(20);
            host.Set(600, 7);
            foreach (byte[] delta in host.DrainJournal(256))
            {
                Assert.IsTrue(client.ApplyPayload(delta));
            }

            CollectionAssert.AreEquivalent(host.Entries.ToList(), client.Entries.ToList());
        }

        [Test]
        public void Snapshot_IsSplitIntoChunksUnderMaxBytes()
        {
            ReplicatedTable<ushort> table = NewTable();
            for (ushort i = 0; i < 20000; i++)
            {
                table.Set(i, i);
            }
            IReadOnlyList<byte[]> chunks = table.CaptureSnapshot(ProtocolConstants.MaxChunkPayloadBytes);
            Assert.Greater(chunks.Count, 1, "2만 개는 16 KB 한 조각에 담기지 않는다");
            Assert.IsTrue(chunks.All(c => c.Length <= ProtocolConstants.MaxChunkPayloadBytes));

            var client = NewTable();
            foreach (byte[] c in chunks)
            {
                Assert.IsTrue(client.ApplyPayload(c));
            }
            Assert.AreEqual(20000, client.Count);
        }

        [Test]
        public void ApplyingTheSameDeltaTwice_GivesSameResult()
        {
            // 덮어쓰기라서 스냅샷과 델타가 겹쳐도 결과가 같다.
            ReplicatedTable<ushort> host = NewTable();
            host.Set(1, 11);
            byte[] delta = host.DrainJournal(1024)[0];
            ReplicatedTable<ushort> client = NewTable();
            client.ApplyPayload(delta);
            client.ApplyPayload(delta);
            Assert.AreEqual(1, client.Count);
        }

        [Test]
        public void Remove_IsReplicated_AndSetAfterRemoveCancelsIt()
        {
            ReplicatedTable<ushort> host = NewTable();
            host.Set(1, 1);
            host.Set(2, 2);
            ReplicatedTable<ushort> client = NewTable();
            foreach (byte[] p in host.DrainJournal(1024)) { client.ApplyPayload(p); }

            host.Remove(1);
            foreach (byte[] p in host.DrainJournal(1024)) { client.ApplyPayload(p); }
            Assert.IsFalse(client.TryGet(1, out _));

            host.Remove(2);
            host.Set(2, 22);
            foreach (byte[] p in host.DrainJournal(1024)) { client.ApplyPayload(p); }
            Assert.IsTrue(client.TryGet(2, out ushort v));
            Assert.AreEqual(22, v);
        }

        [Test]
        public void ApplyPayload_RejectsTruncatedAndTrailing()
        {
            ReplicatedTable<ushort> host = NewTable();
            host.Set(1, 1);
            byte[] payload = host.DrainJournal(1024)[0];
            ReplicatedTable<ushort> client = NewTable();
            Assert.IsFalse(client.ApplyPayload(payload.Take(payload.Length - 1).ToArray()));
            Assert.IsFalse(client.ApplyPayload(payload.Concat(new byte[] { 0 }).ToArray()));
            Assert.IsFalse(client.ApplyPayload(null));
        }

        [Test]
        public void TwoByteKey_RejectsLargerKeys()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => NewTable().Set(70000, 1));
        }

        [Test]
        public void Version_ChangesOnEveryChange()
        {
            ReplicatedTable<ushort> table = NewTable();
            int v0 = table.Version;
            table.Set(1, 1);
            Assert.Greater(table.Version, v0);
        }
    }

    public class RosterAndMirrorTests
    {
        [Test]
        public void RosterEntry_RoundTrip_AndLongNameIsTruncatedByUtf8Bytes()
        {
            var table = new ReplicatedTable<RosterEntry>(TableIds.Roster, RosterEntryCodec.Instance);
            string longName = new string('한', 60); // 180바이트
            table.Set(0, new RosterEntry(new PlayerId(76561198000000001UL), "하루", true));
            table.Set(1, new RosterEntry(new PlayerId(2), longName, false));

            var mirror = new ClientWorldMirror();
            foreach (byte[] chunk in table.CaptureSnapshot(ProtocolConstants.MaxChunkPayloadBytes))
            {
                Assert.IsTrue(mirror.Roster.ApplyPayload(chunk));
            }
            Assert.IsTrue(mirror.Roster.TryGet(0, out RosterEntry first));
            Assert.AreEqual("하루", first.Name);
            Assert.IsTrue(first.IsHost);
            Assert.IsTrue(mirror.Roster.TryGet(1, out RosterEntry second));
            Assert.LessOrEqual(new System.Text.UTF8Encoding().GetByteCount(second.Name), RosterEntryCodec.MaxNameBytes);
            Assert.IsTrue(longName.StartsWith(second.Name));
        }

        [Test]
        public void Mirror_AppliesSnapshotChunksAndDeltas_AndRejectsUnknownTable()
        {
            var host = new ReplicatedTable<RosterEntry>(TableIds.Roster, RosterEntryCodec.Instance);
            host.Set(0, new RosterEntry(new PlayerId(1), "a", true));
            byte[] snapshot = host.CaptureSnapshot(1024)[0];
            var mirror = new ClientWorldMirror();
            Assert.IsTrue(mirror.ApplySnapshotChunk(new SnapshotChunk(1, TableIds.Roster, 0, 1, snapshot)));
            Assert.AreEqual(1, mirror.Roster.Count);
            Assert.AreEqual(1, mirror.SnapshotChunksReceived);
            Assert.AreEqual(1, mirror.SnapshotChunksExpected);

            host.DrainJournal(1024);
            host.Set(1, new RosterEntry(new PlayerId(2), "b", false));
            Assert.IsTrue(mirror.ApplyDelta(new TableDelta(2, TableIds.Roster, host.DrainJournal(1024)[0])));
            Assert.AreEqual(2, mirror.Roster.Count);

            Assert.IsFalse(mirror.ApplyDelta(new TableDelta(3, 99, new byte[] { 0, 0, 0, 0 })));
            Assert.AreEqual(1, mirror.ApplyFailures);

            mirror.Reset();
            Assert.AreEqual(0, mirror.Roster.Count);
        }

        [Test]
        public void Utf8Text_Truncate_DoesNotSplitCharacters()
        {
            string text = "가나다라";
            Assert.AreEqual("가나", Utf8Text.Truncate(text, 7)); // 한글 3바이트: 6바이트까지
            Assert.AreEqual(text, Utf8Text.Truncate(text, 12));
            Assert.AreEqual(string.Empty, Utf8Text.Truncate(null, 5));
        }
    }

    public class RemoteAvatarTrackTests
    {
        private static AvatarPose Pose(float x, uint ms, float yaw = 0f) => new AvatarPose(new Vec3f(x, 0, 0), yaw, 0f, false, ms);

        [Test]
        public void Sample_InterpolatesBetweenTwoPoses()
        {
            var track = new RemoteAvatarTrack();
            track.Add(Pose(0f, 0), 10.0);
            track.Add(Pose(10f, 1000), 11.0);
            Assert.IsTrue(track.TrySample(10.5, out Vec3f pos, out _, out _, out _));
            Assert.AreEqual(5f, pos.X, 0.01f);
        }

        [Test]
        public void Sample_ClampsOutsideRange_AndFailsWithoutSamples()
        {
            var track = new RemoteAvatarTrack();
            Assert.IsFalse(track.TrySample(1.0, out _, out _, out _, out _));
            track.Add(Pose(2f, 0), 5.0);
            track.Add(Pose(4f, 500), 5.5);
            track.TrySample(0.0, out Vec3f before, out _, out _, out _);
            track.TrySample(99.0, out Vec3f after, out _, out _, out _);
            Assert.AreEqual(2f, before.X, 0.001f);
            Assert.AreEqual(4f, after.X, 0.001f);
        }

        [Test]
        public void Sample_YawInterpolatesTheShortWayAround360()
        {
            var track = new RemoteAvatarTrack();
            track.Add(Pose(0f, 0, 350f), 0.0);
            track.Add(Pose(0f, 1000, 10f), 1.0);
            track.TrySample(0.5, out _, out float yaw, out _, out _);
            Assert.IsTrue(yaw > 355f || yaw < 5f, "350도와 10도 사이는 0도 근처여야 한다 (실제 " + yaw + ")");
        }

        [Test]
        public void OutOfOrderPose_IsDropped()
        {
            var track = new RemoteAvatarTrack();
            track.Add(Pose(5f, 1000), 1.0);
            track.Add(Pose(1f, 500), 1.1); // 더 옛 자세
            track.TryGetLatest(out AvatarPose latest);
            Assert.AreEqual(5f, latest.Position.X, 0.001f);
        }
    }
}

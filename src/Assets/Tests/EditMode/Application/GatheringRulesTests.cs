// =============================================================
// 파일: GatheringRulesTests.cs
// 역할: 자원 상태(ResourceState) 비트 묶음, 채집 규칙(HarvestRules), 자원 표 바이트 크기(항목 4B), 타격 명령 코덱, 월드 생성 캐시를 시험한다.
// 레이어: test (EditMode, Game.Tests.Application)
// 관련: FR-009, FR-051, NFR-016, NFR-040, DES-008, DES-053 / SDD 3.11.8
// =============================================================
using System;
using Game.Application.Commands;
using Game.Application.Gathering;
using Game.Application.Protocol;
using Game.Application.Replication;
using Game.Application.WorldSync;
using Game.Domain.Common;
using Game.Domain.Gathering;
using NUnit.Framework;

namespace Game.Tests.Application
{
    public class GatheringRulesTests
    {
        private static readonly HarvestHitCounts Counts = new HarvestHitCounts(5, 4, 6, 5);

        [Test]
        public void ResourceState_PackRoundTrip_UsesSixteenBits()
        {
            var state = new ResourceState(200, ResourcePhase.Felled, 13);
            ushort packed = state.Pack();
            Assert.AreEqual((200 << 8) | (2 << 4) | 13, packed);
            Assert.IsTrue(ResourceState.TryUnpack(packed, out ResourceState back));
            Assert.AreEqual(state, back);
        }

        [Test]
        public void ResourceState_UnpackRejectsUnknownPhase()
        {
            Assert.IsFalse(ResourceState.TryUnpack((ushort)(0xF << 4), out _));
            Assert.IsTrue(ResourceState.Pristine.IsPristine);
        }

        [Test]
        public void Tree_BecomesFelled_OnFifthHit_WithFallDirection()
        {
            ResourceState s = ResourceState.Pristine;
            for (int i = 1; i <= 4; i++)
            {
                s = HarvestRules.ApplyHit(s, ResourceKind.Tree, Counts, 9);
                Assert.AreEqual(ResourcePhase.Damaged, s.Phase);
                Assert.AreEqual(i, s.Hits);
            }
            s = HarvestRules.ApplyHit(s, ResourceKind.Tree, Counts, 9);
            Assert.AreEqual(ResourcePhase.Felled, s.Phase);
            Assert.AreEqual(9, s.FallDir);
            Assert.IsFalse(HarvestRules.CanHit(s));
            Assert.AreEqual(s, HarvestRules.ApplyHit(s, ResourceKind.Tree, Counts, 1), "끝난 대상은 그대로");
        }

        [Test]
        public void Stone_BecomesBroken_OnFourthHit()
        {
            ResourceState s = ResourceState.Pristine;
            for (int i = 0; i < 4; i++)
            {
                s = HarvestRules.ApplyHit(s, ResourceKind.Stone, Counts, 5);
            }
            Assert.AreEqual(ResourcePhase.Broken, s.Phase);
            Assert.AreEqual(0, s.FallDir);
        }

        [TestCase(0f, 1f, 0)]
        [TestCase(1f, 0f, 4)]
        [TestCase(0f, -1f, 8)]
        [TestCase(-1f, 0f, 12)]
        public void FallDirection_QuantizesTo16Steps(float dx, float dz, int expected)
        {
            Assert.AreEqual(expected, HarvestRules.QuantizeFallDirection(dx, dz));
        }

        [Test]
        public void Table_EntryIsFourBytes_AndPayloadSizeIsFixed()
        {
            var table = new ReplicatedTable<ResourceState>(TableIds.Resources, ResourceStateCodec.Instance);
            for (uint i = 0; i < 100; i++)
            {
                table.Set(i * 3, new ResourceState(5, ResourcePhase.Felled, (byte)(i % 16)));
            }
            byte[] payload = table.CaptureSnapshot(ProtocolConstants.MaxChunkPayloadBytes)[0];
            Assert.AreEqual(2 + 100 * ResourceStateCodec.EntryBytes + 2, payload.Length);
            Assert.AreEqual(4, ResourceStateCodec.EntryBytes);

            var other = new ReplicatedTable<ResourceState>(TableIds.Resources, ResourceStateCodec.Instance);
            Assert.IsTrue(other.ApplyPayload(payload));
            Assert.AreEqual(100, other.Count);
        }

        [Test]
        public void Table_RejectsCorruptedPhase()
        {
            var table = new ReplicatedTable<ResourceState>(TableIds.Resources, ResourceStateCodec.Instance);
            // [개수 1][키 0x0001][상태 0x00F0: 단계 15 = 범위 밖][지운 개수 0]
            byte[] bad = { 0, 1, 0, 1, 0, 0xF0, 0, 0 };
            Assert.IsFalse(table.ApplyPayload(bad));
        }

        [Test]
        public void SnapshotSplitsIntoChunksUnderLimit_For5000Entries()
        {
            var table = new ReplicatedTable<ResourceState>(TableIds.Resources, ResourceStateCodec.Instance);
            for (uint i = 0; i < 5000; i++)
            {
                table.Set(i, new ResourceState(5, ResourcePhase.Felled, 1));
            }
            var chunks = table.CaptureSnapshot(ProtocolConstants.MaxChunkPayloadBytes);
            long total = 0;
            foreach (byte[] c in chunks)
            {
                Assert.LessOrEqual(c.Length, ProtocolConstants.MaxChunkPayloadBytes);
                total += c.Length;
            }
            Assert.AreEqual(5000 * 4 + chunks.Count * 4, total);
        }

        [Test]
        public void HitResourceCommand_RoundTrips_ThreeByteBody()
        {
            Assert.IsTrue(CommandCodec.TryEncode(new HitResourceCommand(42u, 12345, 3), out CommandMessage message));
            Assert.AreEqual(3, message.Payload.Length);
            Assert.IsTrue(CommandCodec.TryDecode(message, out ICommand command, out _));
            var hit = (HitResourceCommand)command;
            Assert.AreEqual(42u, hit.RequestId);
            Assert.AreEqual(12345, hit.ResourceIndex);
            Assert.AreEqual(3, hit.HandSlot);
        }

        [Test]
        public void HitResourceCommand_RejectsWrongBodyLength()
        {
            var bad = new CommandMessage((byte)CommandKind.HitResource, 1u, new byte[] { 1, 2 });
            Assert.IsFalse(CommandCodec.TryDecode(bad, out _, out Rejection rejection));
            Assert.AreEqual(ReasonCode.InvalidValue, rejection.Reason);
        }

        [Test]
        public void LocalWorldProvider_ReusesSameWorld_AndRejectsUnknownGeneratorVersion()
        {
            var provider = new LocalWorldProvider();
            LocalWorld a = provider.GetOrGenerate(5UL, LocalWorld.GeneratorV0, WorldScale.Small);
            LocalWorld b = provider.GetOrGenerate(5UL, LocalWorld.GeneratorV0, WorldScale.Small);
            Assert.AreSame(a, b);
            Assert.AreNotEqual(0UL, a.Hash);
            Assert.Throws<NotSupportedException>(() => LocalWorld.Generate(5UL, 9, WorldScale.Small));
        }
    }
}

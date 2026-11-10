// =============================================================
// 파일: JoinAndGatewayTests.cs
// 역할: 참가 절차(JoinHandshake), 서버 게이트웨이(ServerCommandGateway), 복제 송신기(ReplicationPublisher),
//       클라이언트 복제 수신기(ClientReplicationReceiver)를 가짜 INetServer/INetClient로 시험한다(FishNet 없음).
// 레이어: test (EditMode, Game.Tests.Net)
// 관련: FR-003, FR-004, FR-007~009, FR-011, NFR-009, NFR-030, NFR-038~041, DES-081~083, DES-085 / SDD 3.9.2, 3.11.13
// =============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Adapters.Net.Join;
using Game.Adapters.Net.Replication;
using Game.Application.Commands;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Application.Replication;
using Game.Domain.Common;
using Game.Domain.Session;
using NUnit.Framework;

namespace Game.Tests.Net
{
    public class JoinHandshakeTests
    {
        [Test]
        public void NormalJoin_FullSequence_AcceptedThenSnapshotThenComplete()
        {
            var rig = new Rig();
            rig.Server.SimulateConnect(1, "127.0.0.1");
            rig.Server.SimulateReceive(1, Rig.Request(11));

            var accepted = (JoinAccepted)rig.Server.To(1).Single();
            Assert.AreEqual(0, accepted.YourSlot);
            Assert.AreEqual(new PlayerId(11), accepted.YourPlayerId);
            Assert.AreEqual("BENCH-0001", accepted.SeedText);

            rig.Server.SimulateReceive(1, new WorldReady(0UL));
            Assert.AreEqual(0, rig.Server.Count<JoinComplete>(1), "스냅샷은 틱 끝에 보낸다");
            rig.Tick(2);

            List<INetMessage> sent = rig.Server.To(1);
            int snapshotIndex = sent.FindIndex(m => m is SnapshotChunk);
            int completeIndex = sent.FindIndex(m => m is JoinComplete);
            Assert.GreaterOrEqual(snapshotIndex, 0, "접속자 명단 스냅샷 조각");
            Assert.Greater(completeIndex, snapshotIndex, "스냅샷 조각이 JoinComplete보다 먼저");
            Assert.IsTrue(rig.Join.TryGetInGamePlayer(1, out PlayerId player));
            Assert.AreEqual(new PlayerId(11), player);
        }

        [Test]
        public void EightPlayersJoin_NinthIsRejectedFull_AndDisconnectedAfterDelay()
        {
            var rig = new Rig();
            for (int i = 1; i <= 8; i++)
            {
                rig.JoinFully(i, (ulong)(100 + i));
            }
            Assert.AreEqual(8, rig.World.State.PlayerCount);

            rig.Server.SimulateConnect(9, "x");
            rig.Server.SimulateReceive(9, Rig.Request(109));
            var rejected = (JoinRejected)rig.Server.To(9).Single();
            Assert.AreEqual(JoinRejectReason.Full, rejected.Reason);
            Assert.AreEqual(8, rig.World.State.PlayerCount);

            Assert.IsEmpty(rig.Server.DisconnectCalls, "거부 메시지가 전송에 실릴 시간을 준다");
            rig.Clock.Advance(JoinHandshake.CloseDelaySeconds + 0.1);
            rig.Tick();
            CollectionAssert.Contains(rig.Server.DisconnectCalls, 9);
        }

        [Test]
        public void VersionMismatch_IsRejected_AndPlayerNotAdded()
        {
            var rig = new Rig();
            rig.Server.SimulateConnect(1, "x");
            rig.Server.SimulateReceive(1, Rig.Request(5, build: "9.9.9"));
            Assert.AreEqual(JoinRejectReason.VersionMismatch, ((JoinRejected)rig.Server.To(1).Single()).Reason);
            Assert.AreEqual(0, rig.World.State.PlayerCount);

            rig.Server.SimulateConnect(2, "x");
            rig.Server.SimulateReceive(2, Rig.Request(6, hash: 1UL));
            Assert.AreEqual(JoinRejectReason.VersionMismatch, ((JoinRejected)rig.Server.To(2).Single()).Reason, "ContentHash 불일치");
        }

        [Test]
        public void ReleaseAndDevBuilds_WithSameCoreVersion_AreRejected()
        {
            // [NFR-058, DES-112] 핵심 번호가 같아도 채널(rel/dev)·빌드 시각이 다르면 다른 빌드이므로 참가를 거부한다.
            var rig = new Rig();
            rig.Server.SimulateConnect(1, "x");
            rig.Server.SimulateReceive(1, Rig.Request(5, build: "0.1.0+rel.20261110090000.9f8e7d6"));
            Assert.AreEqual(JoinRejectReason.VersionMismatch, ((JoinRejected)rig.Server.To(1).Single()).Reason);
            Assert.AreEqual(0, rig.World.State.PlayerCount);
        }

        [Test]
        public void Steam_UsesRemoteId_NotTheIdInTheRequest()
        {
            // [D-14] 요청에 적은 devPlayerId(남의 ID를 가장)는 Steam 전송에서 무시하고 전송이 알려 준 원격 ID를 쓴다.
            var rig = new Rig(steam: true);
            var real = new PlayerId(76561198000000001UL);
            rig.Lobby.Members.Add(real);
            rig.Server.SimulateConnect(1, "76561198000000001");
            rig.Server.SimulateReceive(1, Rig.Request(devId: 76561198999999999UL));
            var accepted = (JoinAccepted)rig.Server.To(1).Single();
            Assert.AreEqual(real, accepted.YourPlayerId);
            Assert.IsTrue(rig.World.HasPlayer(real));
        }

        [Test]
        public void Steam_NonLobbyMember_IsAccessDenied()
        {
            var rig = new Rig(steam: true);
            rig.Server.SimulateConnect(1, "76561198000000002");
            rig.Server.SimulateReceive(1, Rig.Request(0));
            Assert.AreEqual(JoinRejectReason.AccessDenied, ((JoinRejected)rig.Server.To(1).Single()).Reason);
        }

        [Test]
        public void Steam_UnparsableRemoteId_IsRejected()
        {
            var rig = new Rig(steam: true);
            rig.Server.SimulateConnect(1, "");
            rig.Server.SimulateReceive(1, Rig.Request(5));
            Assert.AreEqual(JoinRejectReason.AccessDenied, ((JoinRejected)rig.Server.To(1).Single()).Reason);
        }

        [Test]
        public void DevTransport_WithoutDevId_IsRejected()
        {
            var rig = new Rig();
            rig.Server.SimulateConnect(1, "127.0.0.1");
            rig.Server.SimulateReceive(1, Rig.Request(0));
            Assert.AreEqual(JoinRejectReason.AccessDenied, ((JoinRejected)rig.Server.To(1).Single()).Reason);
        }

        [Test]
        public void NoJoinRequest_TimesOut()
        {
            var rig = new Rig();
            rig.Server.SimulateConnect(1, "x");
            rig.Clock.Advance(rig.Tuning.JoinTimeoutSeconds + 1);
            rig.Tick();
            Assert.AreEqual(JoinRejectReason.Timeout, ((JoinRejected)rig.Server.To(1).Single()).Reason);
        }

        [Test]
        public void AcceptedButNoWorldReady_TimesOut_AndFreesTheSlot()
        {
            var rig = new Rig();
            rig.Server.SimulateConnect(1, "x");
            rig.Server.SimulateReceive(1, Rig.Request(5));
            Assert.AreEqual(1, rig.World.State.PlayerCount);
            rig.Clock.Advance(rig.Tuning.JoinTimeoutSeconds + 1);
            rig.Tick();
            Assert.AreEqual(0, rig.World.State.PlayerCount, "참가 중 시간 초과: 잡아 둔 자리를 돌려준다");
        }

        [Test]
        public void DuplicateJoinRequest_OnSameConnection_IsIgnored()
        {
            var rig = new Rig();
            rig.Server.SimulateConnect(1, "x");
            rig.Server.SimulateReceive(1, Rig.Request(5));
            rig.Server.SimulateReceive(1, Rig.Request(5));
            Assert.AreEqual(1, rig.Server.Count<JoinAccepted>(1));
            Assert.AreEqual(1, rig.World.State.PlayerCount);
        }

        [Test]
        public void SamePlayerSecondConnection_IsRejected()
        {
            var rig = new Rig();
            rig.JoinFully(1, 5);
            rig.Server.SimulateConnect(2, "x");
            rig.Server.SimulateReceive(2, Rig.Request(5));
            Assert.AreEqual(JoinRejectReason.BadRequest, ((JoinRejected)rig.Server.To(2).Single()).Reason);
            Assert.AreEqual(1, rig.World.State.PlayerCount);
        }

        [Test]
        public void WorldHashMismatch_IsRecordedAndRejected()
        {
            var rig = new Rig(expectedWorldHash: 0x1234UL);
            rig.Server.SimulateConnect(1, "x");
            rig.Server.SimulateReceive(1, Rig.Request(5));
            rig.Server.SimulateReceive(1, new WorldReady(0x9999UL));
            Assert.AreEqual(JoinRejectReason.GenMismatch, ((JoinRejected)rig.Server.To(1).Last()).Reason);
            Assert.IsNotEmpty(rig.Log.Errors, "호스트도 기록한다(FR-169)");
            Assert.AreEqual(0, rig.World.State.PlayerCount);
        }

        [Test]
        public void WorldReadyBeforeRequest_AndUnexpectedKinds_AreIgnored()
        {
            var rig = new Rig();
            rig.Server.SimulateConnect(1, "x");
            rig.Server.SimulateReceive(1, new WorldReady(0UL));
            Assert.IsEmpty(rig.Server.To(1));
            Assert.AreEqual(0, rig.World.State.PlayerCount);
        }

        [Test]
        public void Disconnect_RemovesPlayer_FreesSlot_AndRaisesPlayerLeft()
        {
            var rig = new Rig();
            var left = new List<PlayerId>();
            rig.Join.PlayerLeft += left.Add;
            rig.JoinFully(1, 11);
            rig.JoinFully(2, 12);
            rig.Server.SimulateDisconnect(1);
            Assert.AreEqual(1, rig.World.State.PlayerCount);
            CollectionAssert.AreEqual(new[] { new PlayerId(11) }, left);
            Assert.IsFalse(rig.Join.TryGetInGamePlayer(1, out _));

            rig.JoinFully(3, 13);
            Assert.IsTrue(rig.World.TryGetSlot(new PlayerId(13), out int slot));
            Assert.AreEqual(0, slot, "나간 자리를 다시 쓴다");
        }

        [Test]
        public void DisconnectDuringSync_StopsSendingAndFreesSlot()
        {
            var rig = new Rig();
            rig.Server.SimulateConnect(1, "x");
            rig.Server.SimulateReceive(1, Rig.Request(5));
            rig.Server.SimulateReceive(1, new WorldReady(0UL));
            rig.Server.SimulateDisconnect(1);
            rig.Tick(3);
            Assert.AreEqual(0, rig.World.State.PlayerCount);
            Assert.AreEqual(0, rig.Server.Count<JoinComplete>(1));
        }

        [Test]
        public void ExceptionInHandling_DoesNotEscapeTheGateway()
        {
            // [NFR-040] 이름 조회가 예외를 던져도 연결 처리 루프 밖으로 새지 않고 로그로 남는다.
            var rig = new Rig(nameOf: _ => throw new InvalidOperationException("의도된 시험 예외"));
            rig.Server.SimulateConnect(1, "x");
            Assert.DoesNotThrow(() => rig.Server.SimulateReceive(1, Rig.Request(5)));
            Assert.IsNotEmpty(rig.Log.Errors);
        }
    }

    public class GatewayTests
    {
        [Test]
        public void CommandBeforeJoin_IsDropped()
        {
            var rig = new Rig();
            rig.Server.SimulateConnect(1, "x");
            Assert.IsTrue(CommandCodec.TryEncode(new PingCommand(1u, Vec3f.Zero), out CommandMessage message));
            rig.Server.SimulateReceive(1, message);
            rig.Tick();
            Assert.AreEqual(1, rig.Gateway.DroppedCount);
            Assert.IsEmpty(rig.Server.To(1));
        }

        [Test]
        public void ValidCommand_IsProcessedOnTick_AndResultSentToRequesterOnly()
        {
            var rig = new Rig();
            rig.JoinFully(1, 11);
            rig.JoinFully(2, 12);
            Assert.IsTrue(CommandCodec.TryEncode(new PingCommand(42u, new Vec3f(5, 0, 5)), out CommandMessage message));
            rig.Server.SimulateReceive(1, message);
            Assert.AreEqual(0, rig.Server.Count<CommandResultMessage>(1), "틱에서 처리한다");
            rig.Tick();

            var result = rig.Server.To(1).OfType<CommandResultMessage>().Single();
            Assert.IsTrue(result.Ok);
            Assert.AreEqual(42u, result.RequestId);
            Assert.AreEqual(0, rig.Server.Count<CommandResultMessage>(2));
            Assert.IsTrue(rig.World.State.TryGetLastPing(new PlayerId(11), out Vec3f ping));
            Assert.AreEqual(5f, ping.X, 0.01f);
        }

        [Test]
        public void ServerToClientKinds_SentByAClient_AreDropped()
        {
            // 변조된 클라이언트가 서버 → 클라이언트 종류(예: JoinAccepted)를 보내도 무시한다.
            var rig = new Rig();
            rig.JoinFully(1, 11);
            int before = rig.Gateway.DroppedCount;
            rig.Server.SimulateReceive(1, new TableDelta(1u, TableIds.Roster, new byte[] { 0, 0, 0, 0 }));
            rig.Server.SimulateReceive(1, new JoinComplete(1u, Vec3f.Zero));
            Assert.AreEqual(before + 2, rig.Gateway.DroppedCount);
        }

        [Test]
        public void MalformedCommandPayload_GetsRejectionReply_AndDoesNotChangeState()
        {
            var rig = new Rig();
            rig.JoinFully(1, 11);
            rig.Server.SimulateReceive(1, new CommandMessage((byte)CommandKind.Ping, 3u, new byte[2]));
            var result = rig.Server.To(1).OfType<CommandResultMessage>().Single();
            Assert.IsFalse(result.Ok);
            Assert.AreEqual(ReasonCode.InvalidValue, result.Rejection.Reason);
            Assert.IsFalse(rig.World.State.TryGetLastPing(new PlayerId(11), out _));
        }

        [Test]
        public void UnknownCommandKind_GetsRejectionReply()
        {
            var rig = new Rig();
            rig.JoinFully(1, 11);
            rig.Server.SimulateReceive(1, new CommandMessage(250, 8u, new byte[0]));
            Assert.AreEqual(ReasonCode.UnknownCommand, rig.Server.To(1).OfType<CommandResultMessage>().Single().Rejection.Reason);
        }

        [Test]
        public void OutOfRangeValue_IsRejected_ByRangeCheck()
        {
            var rig = new Rig();
            rig.JoinFully(1, 11);
            Assert.IsTrue(CommandCodec.TryEncode(new PingCommand(1u, new Vec3f(50000f, 0, 0)), out CommandMessage message));
            rig.Server.SimulateReceive(1, message);
            rig.Tick();
            Assert.AreEqual(ReasonCode.InvalidValue, rig.Server.To(1).OfType<CommandResultMessage>().Single().Rejection.Reason);
        }

        [Test]
        public void Flood_IsRateLimited_AndFloodRepliesAreNotAmplified()
        {
            // [NFR-041] 초당 상한(30)을 넘는 요청은 실행하지 않고, 빈도 제한 거부에는 회신하지 않는다.
            var rig = new Rig();
            rig.JoinFully(1, 11);
            Assert.IsTrue(CommandCodec.TryEncode(new PingCommand(1u, Vec3f.Zero), out CommandMessage message));
            for (int i = 0; i < 500; i++)
            {
                rig.Server.SimulateReceive(1, new CommandMessage(message.CommandKindValue, (uint)i, message.Payload));
            }
            rig.Tick();
            var results = rig.Server.To(1).OfType<CommandResultMessage>().ToList();
            Assert.LessOrEqual(results.Count(r => r.Ok), 30);
            Assert.Less(results.Count, 300);
        }

        [Test]
        public void WorldState_CanOnlyBeChangedThroughServerWorld()
        {
            // 참가자 쪽 어셈블리(Net 어댑터)에서는 WorldState의 변경 메서드가 보이지 않는다(internal). [NFR-030]
            var methods = typeof(Game.Application.Server.WorldState).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
            string[] names = methods.Select(m => m.Name).ToArray();
            CollectionAssert.DoesNotContain(names, "AddPlayer");
            CollectionAssert.DoesNotContain(names, "RemovePlayer");
            CollectionAssert.DoesNotContain(names, "RecordPing");
        }
    }

    public class ReplicationPublisherTests
    {
        [Test]
        public void ReadyConnection_ReceivesDeltaOnTickEnd()
        {
            var rig = new Rig();
            rig.JoinFully(1, 11);
            int before = rig.Server.Count<TableDelta>(1);
            rig.JoinFully(2, 12); // 두 번째 플레이어가 들어오면 명단이 바뀐다
            Assert.Greater(rig.Server.Count<TableDelta>(1), before, "준비된 연결은 명단 변경 델타를 받는다");
        }

        [Test]
        public void LateJoiner_SnapshotPlusDeltas_EqualsHostRoster()
        {
            var rig = new Rig();
            rig.JoinFully(1, 11);
            rig.JoinFully(2, 12);

            // 세 번째 참가자: 스냅샷을 받는 동안 네 번째가 들어와 델타가 생긴다.
            rig.Server.SimulateConnect(3, "x");
            rig.Server.SimulateReceive(3, Rig.Request(13));
            rig.Server.SimulateReceive(3, new WorldReady(0UL));
            rig.JoinFully(4, 14);
            rig.Tick(3);

            var mirror = new ClientWorldMirror();
            var client = new Game.Tests.Net.FakeClientBridge();
            var receiver = new ClientReplicationReceiver(client, mirror, null);
            foreach (INetMessage message in rig.Server.To(3))
            {
                client.Deliver(message);
            }

            Assert.AreEqual(rig.World.State.Roster.Count, mirror.Roster.Count);
            CollectionAssert.AreEquivalent(
                rig.World.State.Roster.Entries.Select(e => (e.Key, e.Value.Player)).ToList(),
                mirror.Roster.Entries.Select(e => (e.Key, e.Value.Player)).ToList(),
                "스냅샷 + 이어서 온 델타 = 호스트 상태");
            Assert.AreEqual(4, mirror.Roster.Count);
        }

        [Test]
        public void SyncingConnection_DoesNotReceiveDeltasBeforeJoinComplete()
        {
            var rig = new Rig();
            // 큰 표를 더해 스냅샷이 여러 틱에 걸치게 한다.
            var big = new ReplicatedTable<ushort>(TableIds.Resources, new ResourceLikeCodec());
            for (ushort i = 0; i < 30000; i++)
            {
                big.Set(i, i);
            }
            big.DrainJournal(ProtocolConstants.MaxChunkPayloadBytes);
            rig.World.AddTable(big);

            rig.Server.SimulateConnect(1, "x");
            rig.Server.SimulateReceive(1, Rig.Request(5));
            rig.Server.SimulateReceive(1, new WorldReady(0UL));
            rig.Tick();

            big.Set(5, 999); // 스냅샷 전송 중에 생긴 변경
            rig.Tick();
            Assert.AreEqual(0, rig.Server.Count<TableDelta>(1), "스냅샷을 받는 중인 연결에는 델타를 바로 보내지 않고 보관한다");
            Assert.AreEqual(0, rig.Server.Count<JoinComplete>(1));

            int ticks = 0;
            while (rig.Server.Count<JoinComplete>(1) == 0 && ticks++ < 500)
            {
                rig.Tick();
            }
            List<INetMessage> sent = rig.Server.To(1);
            int complete = sent.FindIndex(m => m is JoinComplete);
            int lastChunk = sent.FindLastIndex(m => m is SnapshotChunk);
            int firstDelta = sent.FindIndex(m => m is TableDelta);
            Assert.Greater(complete, lastChunk, "스냅샷 조각이 모두 JoinComplete보다 먼저");
            Assert.Greater(firstDelta, complete, "보관한 델타는 JoinComplete 뒤에");
            Assert.Greater(ticks, 3, "스냅샷은 속도 제한(약 1.5 Mbps)으로 여러 틱에 걸친다");
        }

        [Test]
        public void SnapshotRate_StaysUnderLimit_PerSecond()
        {
            var rig = new Rig();
            var big = new ReplicatedTable<ushort>(TableIds.Resources, new ResourceLikeCodec());
            for (ushort i = 0; i < 40000; i++)
            {
                big.Set(i, i);
            }
            big.DrainJournal(ProtocolConstants.MaxChunkPayloadBytes);
            rig.World.AddTable(big);
            rig.Server.SimulateConnect(1, "x");
            rig.Server.SimulateReceive(1, Rig.Request(5));
            rig.Server.SimulateReceive(1, new WorldReady(0UL));

            rig.Tick(30); // 1초
            long bytes = rig.Server.To(1).OfType<SnapshotChunk>().Sum(c => (long)c.Payload.Length);
            // 한 조각(16 KB)이 한 틱 몫보다 크므로 쌓인 몫이 차야 보낸다. 1초 동안 보낸 양은 상한 + 한 조각 안.
            Assert.LessOrEqual(bytes, rig.Tuning.SnapshotBytesPerSecond + ProtocolConstants.MaxChunkPayloadBytes);
            Assert.Greater(bytes, 0);
        }

        [Test]
        public void ChunksAreUnderSixteenKilobytes()
        {
            var rig = new Rig();
            var big = new ReplicatedTable<ushort>(TableIds.Resources, new ResourceLikeCodec());
            for (ushort i = 0; i < 20000; i++)
            {
                big.Set(i, i);
            }
            big.DrainJournal(ProtocolConstants.MaxChunkPayloadBytes);
            rig.World.AddTable(big);
            rig.Server.SimulateConnect(1, "x");
            rig.Server.SimulateReceive(1, Rig.Request(5));
            rig.Server.SimulateReceive(1, new WorldReady(0UL));
            rig.Tick(400);
            var chunks = rig.Server.To(1).OfType<SnapshotChunk>().ToList();
            Assert.IsTrue(chunks.All(c => c.Payload.Length <= ProtocolConstants.MaxChunkPayloadBytes));
            Assert.AreEqual(1, rig.Server.Count<JoinComplete>(1));
        }
    }

    /// <summary>INetClient 가짜: 서버가 보낸 메시지를 시험이 직접 전달한다.</summary>
    internal sealed class FakeClientBridge : INetClient
    {
        public NetClientState State { get; private set; }
        public event Action<NetClientState> StateChanged { add { } remove { } }
        public event Action<INetMessage> Received;
        public bool Connect(string address, ushort port) => true;
        public void Disconnect() { }
        public bool Send<T>(in T message, NetChannel channel) where T : struct, INetMessage => true;
        public void Deliver(INetMessage message) => Received?.Invoke(message);
    }
}

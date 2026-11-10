// =============================================================
// 파일: ProtocolCodecTests.cs
// 역할: 네트워크 메시지 코덱(MessageCodec, ByteWriter/ByteReader) 시험.
//       (1) 쓰고 다시 읽으면 같은 값(왕복) (2) 고정 입력의 고정 바이트 기대값 (3) 잘린 입력·상한 초과·모르는 종류·남는 바이트 거부.
// 레이어: test (EditMode, Game.Tests.Application)
// 관련: NFR-013, NFR-014, NFR-040, DES-080 / SDD 3.7.2, D-23, R-30
//
// 고정 바이트 시험은 쓰기와 읽기가 같은 잘못을 함께 저질러도 잡히도록 손으로 계산한 바이트와 비교한다.
// =============================================================
using System;
using Game.Application.Protocol;
using Game.Domain.Common;
using Game.Domain.Session;
using NUnit.Framework;

namespace Game.Tests.Application
{
    public class ProtocolCodecTests
    {
        private static byte[] Encode<T>(T message) where T : struct, INetMessage
        {
            var writer = new ByteWriter(ProtocolConstants.MaxPacketBytes);
            Assert.IsTrue(MessageCodec.TryEncode(message, writer), "인코딩에 성공해야 한다: " + message.Kind);
            return writer.ToArray();
        }

        private static INetMessage Decode(byte[] bytes)
        {
            Assert.IsTrue(MessageCodec.TryDecode(bytes, out INetMessage message, out DecodeFailure failure), "디코드 실패: " + failure);
            return message;
        }

        // ---------- (1) 왕복 ----------

        [Test]
        public void RoundTrip_JoinRequest()
        {
            var sent = new JoinRequest(ProtocolConstants.ProtocolVersion, "0.1.0+dev.20261104153012.a1b2c3d", 0x1122334455667788UL, JoinPath.Invite, 42UL, new ushort[] { 0, 1 });
            var got = (JoinRequest)Decode(Encode(sent));
            Assert.AreEqual(sent.ProtocolVersion, got.ProtocolVersion);
            Assert.AreEqual(sent.GameVersion, got.GameVersion);
            Assert.AreEqual(sent.ContentHash, got.ContentHash);
            Assert.AreEqual(sent.Path, got.Path);
            Assert.AreEqual(sent.DevPlayerId, got.DevPlayerId);
            CollectionAssert.AreEqual(sent.SupportedGeneratorVersions, got.SupportedGeneratorVersions);
        }

        [Test]
        public void RoundTrip_JoinAccepted_WithKoreanSeedText()
        {
            var sent = new JoinAccepted("꺼진 하늘-시드", 99UL, 0, 0xDEADBEEFUL, 1234u, 8, SessionAccess.FriendsOnly, new PlayerId(76561198000000001UL), 3);
            var got = (JoinAccepted)Decode(Encode(sent));
            Assert.AreEqual(sent.SeedText, got.SeedText);
            Assert.AreEqual(sent.Seed, got.Seed);
            Assert.AreEqual(sent.ExpectedWorldHash, got.ExpectedWorldHash);
            Assert.AreEqual(sent.ServerTick, got.ServerTick);
            Assert.AreEqual(sent.MaxPlayers, got.MaxPlayers);
            Assert.AreEqual(sent.Access, got.Access);
            Assert.AreEqual(sent.YourPlayerId, got.YourPlayerId);
            Assert.AreEqual(sent.YourSlot, got.YourSlot);
        }

        [Test]
        public void RoundTrip_SimpleMessages()
        {
            Assert.AreEqual(JoinRejectReason.Full, ((JoinRejected)Decode(Encode(new JoinRejected(JoinRejectReason.Full)))).Reason);
            Assert.AreEqual(0xFFUL, ((WorldReady)Decode(Encode(new WorldReady(0xFFUL)))).ComputedWorldHash);

            var complete = (JoinComplete)Decode(Encode(new JoinComplete(77u, new Vec3f(1.5f, 0.1f, -2f))));
            Assert.AreEqual(77u, complete.SnapshotTick);
            Assert.AreEqual(1.5f, complete.Spawn.X, 0.001f);
            Assert.AreEqual(0.1f, complete.Spawn.Y, 0.001f);
            Assert.AreEqual(-2f, complete.Spawn.Z, 0.001f);

            var cmd = (CommandMessage)Decode(Encode(new CommandMessage(1, 9u, new byte[] { 1, 2, 3 })));
            Assert.AreEqual(1, cmd.CommandKindValue);
            Assert.AreEqual(9u, cmd.RequestId);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, cmd.Payload);

            var result = (CommandResultMessage)Decode(Encode(new CommandResultMessage(5u, false, new Rejection(ReasonCode.OutOfRange, 7, -9))));
            Assert.AreEqual(5u, result.RequestId);
            Assert.IsFalse(result.Ok);
            Assert.AreEqual(new Rejection(ReasonCode.OutOfRange, 7, -9), result.Rejection);
        }

        [Test]
        public void RoundTrip_SnapshotChunk_AndTableDelta_AtMaxPayload()
        {
            var big = new byte[ProtocolConstants.MaxChunkPayloadBytes];
            for (int i = 0; i < big.Length; i++)
            {
                big[i] = (byte)(i * 31);
            }
            var chunk = (SnapshotChunk)Decode(Encode(new SnapshotChunk(10u, 2, 1, 3, big)));
            Assert.AreEqual(10u, chunk.SnapshotTick);
            Assert.AreEqual(2, chunk.TableId);
            Assert.AreEqual(1, chunk.ChunkIndex);
            Assert.AreEqual(3, chunk.ChunkCount);
            CollectionAssert.AreEqual(big, chunk.Payload);

            var delta = (TableDelta)Decode(Encode(new TableDelta(11u, 2, big)));
            CollectionAssert.AreEqual(big, delta.Payload);
        }

        [Test]
        public void RoundTrip_CreatureMessages()
        {
            CreatureState state = CreatureState.FromWorld(5, new Vec3f(10.5f, 2f, -300f), 90f, 3, 80);
            var spawn = (CreatureSpawn)Decode(Encode(new CreatureSpawn(9, state)));
            Assert.AreEqual(9, spawn.DefId);
            Assert.AreEqual(state, spawn.State);
            Assert.AreEqual(5, ((CreatureDespawn)Decode(Encode(new CreatureDespawn(5)))).Id);

            var snapshot = (CreatureSnapshot)Decode(Encode(new CreatureSnapshot(3u, new[] { state, state })));
            Assert.AreEqual(3u, snapshot.Tick);
            Assert.AreEqual(2, snapshot.Creatures.Length);
            Assert.AreEqual(state, snapshot.Creatures[1]);
        }

        [Test]
        public void CreatureQuantization_ErrorIsUnderThreeCm_AcrossWholeWorld()
        {
            // [3.11.9] 위치는 2^16 단계(약 4.7 cm)라 오차는 반 단계(약 2.4 cm) 이하여야 한다.
            // 월드는 0~3072 m(원점 한쪽)이고 가장자리 여유 16 m까지 담는다. 경계·중앙·끝·여유 끝 값을 모두 왕복한다.
            foreach (float x in new[] { -16f, 0f, 0.01f, 12.34f, 300.123f, 1535.9f, 1536f, 1536.1f, 2999.99f, 3071.9f, 3072f, 3088f })
            {
                CreatureState s = CreatureState.FromWorld(1, new Vec3f(x, 0f, 0f), 0f, 0, 0);
                Assert.AreEqual(x, s.Position.X, 0.03f, "x=" + x);
                CreatureState z = CreatureState.FromWorld(1, new Vec3f(0f, 0f, x), 0f, 0, 0);
                Assert.AreEqual(x, z.Position.Z, 0.03f, "z=" + x);
            }
        }

        [Test]
        public void CreatureQuantization_HeightRangeCoversTerrain_AndClampsOutside()
        {
            // 높이(Y)도 같은 범위를 쓴다. 지형 높이(수백 m)가 들어가야 한다.
            foreach (float y in new[] { 0f, 120.5f, 312.34f, 600f })
            {
                Assert.AreEqual(y, CreatureState.FromWorld(1, new Vec3f(0f, y, 0f), 0f, 0, 0).Position.Y, 0.03f, "y=" + y);
            }
            // 범위 밖은 자른다(예외 없음).
            Assert.AreEqual(-16f, CreatureState.FromWorld(1, new Vec3f(-500f, 0f, 0f), 0f, 0, 0).Position.X, 0.001f);
            Assert.AreEqual(3088f, CreatureState.FromWorld(1, new Vec3f(9000f, 0f, 0f), 0f, 0, 0).Position.X, 0.001f);
            Assert.AreEqual(-16f, CreatureState.FromWorld(1, new Vec3f(float.NaN, 0f, 0f), 0f, 0, 0).Position.X, 0.001f);
        }

        [Test]
        public void RoundTrip_AvatarPose()
        {
            var sent = new AvatarPose(new Vec3f(-12.345f, 1.2f, 300.5f), 270f, 30f, true, 123456u);
            var writer = new ByteWriter(AvatarPose.ByteSize);
            Assert.IsTrue(MessageCodec.TryEncodeAvatarPose(sent, writer));
            Assert.IsTrue(MessageCodec.TryDecodeAvatarPose(writer.ToArray(), out AvatarPose got));
            Assert.AreEqual(sent.Position.X, got.Position.X, 0.001f);
            Assert.AreEqual(sent.Position.Y, got.Position.Y, 0.001f);
            Assert.AreEqual(sent.Position.Z, got.Position.Z, 0.001f);
            Assert.AreEqual(sent.YawDegrees, got.YawDegrees, 0.01f);
            Assert.AreEqual(sent.PitchDegrees, got.PitchDegrees, 0.5f);
            Assert.IsTrue(got.Running);
            Assert.AreEqual(sent.TimeMs, got.TimeMs);
        }

        // ---------- (2) 고정 바이트 ----------

        [Test]
        public void FixedBytes_JoinRejected()
        {
            CollectionAssert.AreEqual(new byte[] { 17, 1 }, Encode(new JoinRejected(JoinRejectReason.Full)));
        }

        [Test]
        public void JoinRequest_GameVersionOver64Bytes_IsRejected()
        {
            // [NFR-058, R-30] 64바이트까지는 왕복, 65바이트는 쓰기도 읽기도 실패한다.
            string max = new string('a', ProtocolConstants.MaxGameVersionBytes);
            var ok = (JoinRequest)Decode(Encode(new JoinRequest(2, max, 1, JoinPath.Invite, 0, new ushort[] { 0 })));
            Assert.AreEqual(max, ok.GameVersion);

            var writer = new ByteWriter(ProtocolConstants.MaxPacketBytes);
            Assert.IsFalse(MessageCodec.TryEncode(new JoinRequest(2, new string('a', 65), 1, JoinPath.Invite, 0, new ushort[] { 0 }), writer));

            // 변조된 패킷: 길이 접두를 65로 적어 보낸다.
            byte[] forged = new byte[1 + 2 + 1 + 65 + 8 + 1 + 8 + 1];
            forged[0] = (byte)MessageKind.JoinRequest;
            forged[1] = 2;
            forged[3] = 65;
            Assert.IsFalse(MessageCodec.TryDecode(forged, out _, out _));
        }

        [Test]
        public void JoinRequest_OtherProtocolVersion_SkipsRestAndYieldsProtocolOnly()
        {
            // [SDD 3.7.12 (5)] 옛 프로토콜(1) 요청: 빌드 번호 u32가 오는 배치. 나머지는 해석하지 않고 protocolVer만 담아 돌려준다.
            byte[] legacy =
            {
                (byte)MessageKind.JoinRequest, 1, 0, 7, 0, 0, 0,
                0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0,
            };
            var got = (JoinRequest)Decode(legacy);
            Assert.AreEqual((ushort)1, got.ProtocolVersion);
            Assert.AreEqual(string.Empty, got.GameVersion);
            Assert.AreNotEqual(ProtocolConstants.ProtocolVersion, got.ProtocolVersion);

            // 프로토콜 번호만 있어도(본문 없음) 같은 경로로 거부 대상이 된다.
            var onlyProto = (JoinRequest)Decode(new byte[] { (byte)MessageKind.JoinRequest, 9, 0 });
            Assert.AreEqual((ushort)9, onlyProto.ProtocolVersion);
        }

        [Test]
        public void ProtocolVersion_IsTwo()
        {
            Assert.AreEqual((ushort)2, ProtocolConstants.ProtocolVersion);
        }

        [Test]
        public void FixedBytes_WorldReady()
        {
            CollectionAssert.AreEqual(new byte[] { 2, 8, 7, 6, 5, 4, 3, 2, 1 }, Encode(new WorldReady(0x0102030405060708UL)));
        }

        [Test]
        public void FixedBytes_JoinRequest()
        {
            var message = new JoinRequest(2, "0.1.0", 0x1122334455667788UL, JoinPath.Friend, 0UL, new ushort[] { 0 });
            byte[] expected =
            {
                1,                              // 종류 JoinRequest
                2, 0,                           // 프로토콜 2
                5, 0x30, 0x2E, 0x31, 0x2E, 0x30, // 게임 버전: 길이 5 + "0.1.0"
                0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, // ContentHash
                1,                              // 참가 경로 Friend
                0, 0, 0, 0, 0, 0, 0, 0,         // 개발용 ID 0
                1,                              // 생성기 버전 개수
                0, 0,                           // 생성기 버전 0
            };
            CollectionAssert.AreEqual(expected, Encode(message));
        }

        [Test]
        public void FixedBytes_CommandResult()
        {
            var message = new CommandResultMessage(0x01020304u, true, new Rejection(ReasonCode.InvalidValue, 5, -1));
            byte[] expected = { 19, 4, 3, 2, 1, 1, 4, 5, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF };
            CollectionAssert.AreEqual(expected, Encode(message));
        }

        [Test]
        public void FixedBytes_AvatarPose_Is20Bytes()
        {
            var pose = new AvatarPose(new Vec3f(1.5f, 2f, -3f), 90f, 0f, true, 100u);
            var writer = new ByteWriter(AvatarPose.ByteSize);
            Assert.IsTrue(MessageCodec.TryEncodeAvatarPose(pose, writer));
            byte[] expected =
            {
                0xDC, 0x05, 0x00, 0x00, // X 1500 mm
                0xD0, 0x07, 0x00, 0x00, // Y 2000 mm
                0x48, 0xF4, 0xFF, 0xFF, // Z -3000 mm
                0x00, 0x40,             // 방향 90도 = 16384
                0x80,                   // 상하 0도 = 128
                0x01,                   // 달리기 플래그
                0x64, 0x00, 0x00, 0x00, // 시각 100 ms
            };
            CollectionAssert.AreEqual(expected, writer.ToArray());
            Assert.AreEqual(AvatarPose.ByteSize, writer.Length);
        }

        [Test]
        public void FixedBytes_CreatureEntry_Is11Bytes()
        {
            var writer = new ByteWriter(32);
            new CreatureState(0x0102, 0x0304, 0x0506, 0x0708, 9, 10, 11).Write(writer);
            byte[] expected = { 2, 1, 4, 3, 6, 5, 8, 7, 9, 10, 11 };
            CollectionAssert.AreEqual(expected, writer.ToArray());
            Assert.AreEqual(CreatureState.ByteSize, writer.Length);
        }

        // ---------- (3) 거부 ----------

        [Test]
        public void Truncated_AnyPrefix_IsRejectedWithoutException()
        {
            byte[][] samples =
            {
                Encode(new JoinRequest(2, "0.1.0+editor", 5UL, JoinPath.Invite, 3UL, new ushort[] { 0, 1 })),
                Encode(new JoinAccepted("seed", 1UL, 0, 2UL, 3u, 8, SessionAccess.Public, new PlayerId(5), 1)),
                Encode(new JoinComplete(1u, new Vec3f(1, 2, 3))),
                Encode(new CommandMessage(1, 2u, new byte[] { 9, 9 })),
                Encode(new CommandResultMessage(1u, true, default)),
                Encode(new SnapshotChunk(1u, 1, 0, 1, new byte[] { 1, 2, 3 })),
                Encode(new TableDelta(1u, 1, new byte[] { 1, 2, 3 })),
                Encode(new CreatureSnapshot(1u, new[] { CreatureState.FromWorld(1, Vec3f.Zero, 0f, 0, 0) })),
            };
            foreach (byte[] full in samples)
            {
                // 한 바이트도 모자라거나 더 많으면(어느 쪽도) 거부해야 한다. 길이 1(종류만)부터 전체-1까지 모두 시험.
                for (int length = 1; length < full.Length; length++)
                {
                    Assert.IsFalse(MessageCodec.TryDecode(full, 0, length, out _, out DecodeFailure f), "잘린 입력이 받아들여짐 kind=" + full[0] + " 길이=" + length);
                    Assert.AreNotEqual(DecodeFailure.None, f);
                }
            }
        }

        [Test]
        public void TrailingBytes_AreRejected()
        {
            byte[] full = Encode(new WorldReady(1UL));
            var longer = new byte[full.Length + 1];
            Array.Copy(full, longer, full.Length);
            Assert.IsFalse(MessageCodec.TryDecode(longer, out _, out DecodeFailure failure));
            Assert.AreEqual(DecodeFailure.Malformed, failure, "WorldReady는 상한(16바이트) 안이지만 남는 바이트는 거부해야 한다");
        }

        [Test]
        public void ExtraBytesInsideLimit_AreMalformed()
        {
            byte[] full = Encode(new JoinRejected(JoinRejectReason.Full));
            var longer = new byte[full.Length + 1];
            Array.Copy(full, longer, full.Length);
            Assert.IsFalse(MessageCodec.TryDecode(longer, out _, out DecodeFailure failure));
            Assert.AreEqual(DecodeFailure.Malformed, failure);
        }

        [Test]
        public void UnknownKind_AndEmpty_AreRejected()
        {
            Assert.IsFalse(MessageCodec.TryDecode(new byte[] { 0 }, out _, out DecodeFailure f0));
            Assert.AreEqual(DecodeFailure.UnknownKind, f0);
            Assert.IsFalse(MessageCodec.TryDecode(new byte[] { 99, 1, 2 }, out _, out DecodeFailure f1));
            Assert.AreEqual(DecodeFailure.UnknownKind, f1);
            Assert.IsFalse(MessageCodec.TryDecode(new byte[0], out _, out DecodeFailure f2));
            Assert.AreEqual(DecodeFailure.Empty, f2);
            Assert.IsFalse(MessageCodec.TryDecode(null, out _, out DecodeFailure f3));
            Assert.AreEqual(DecodeFailure.Empty, f3);
        }

        [Test]
        public void OversizedPacket_IsRejected_BeforeParsing()
        {
            // 작은 메시지 종류가 큰 입력을 받지 않는다. [NFR-040]
            var big = new byte[1000];
            big[0] = (byte)MessageKind.JoinRejected;
            Assert.IsFalse(MessageCodec.TryDecode(big, out _, out DecodeFailure failure));
            Assert.AreEqual(DecodeFailure.TooLarge, failure);
        }

        [Test]
        public void Encode_FailsInsteadOfThrowing_WhenOverLimit()
        {
            var writer = new ByteWriter(ProtocolConstants.MaxPacketBytes);
            var longSeed = new string('가', 40); // UTF-8 120바이트 > 64
            Assert.IsFalse(MessageCodec.TryEncode(new JoinAccepted(longSeed, 0, 0, 0, 0, 8, SessionAccess.Public, new PlayerId(1), 0), writer));
            Assert.IsFalse(MessageCodec.TryEncode(new CommandMessage(1, 1, new byte[ProtocolConstants.MaxCommandPayloadBytes + 1]), writer));
            Assert.IsFalse(MessageCodec.TryEncode(new SnapshotChunk(1, 1, 0, 1, new byte[ProtocolConstants.MaxChunkPayloadBytes + 1]), writer));
            Assert.IsFalse(MessageCodec.TryEncode(new JoinRequest(2, "0.1.0", 1, JoinPath.Invite, 0, new ushort[ProtocolConstants.MaxGeneratorVersions + 1]), writer));
            Assert.IsFalse(MessageCodec.TryEncode(new CreatureSnapshot(1, new CreatureState[ProtocolConstants.MaxCreaturesPerSnapshot + 1]), writer));
        }

        [Test]
        public void Decode_RejectsOutOfRangeEnumValues()
        {
            // 참가 경로·참가 조건·거부 사유는 범위를 벗어난 값을 받지 않는다.
            byte[] request = Encode(new JoinRequest(2, "0.1.0", 1, JoinPath.Invite, 0, new ushort[] { 0 }));
            request[1 + 2 + (1 + 5) + 8] = 9; // 참가 경로 자리
            Assert.IsFalse(MessageCodec.TryDecode(request, out _, out _));
            Assert.IsFalse(MessageCodec.TryDecode(new byte[] { (byte)MessageKind.JoinRejected, 0 }, out _, out _), "사유 없음(0)은 거부 메시지가 될 수 없다");
            Assert.IsFalse(MessageCodec.TryDecode(new byte[] { (byte)MessageKind.JoinRejected, 200 }, out _, out _));
        }

        [Test]
        public void Decode_RejectsInvalidUtf8_InSeedText()
        {
            byte[] accepted = Encode(new JoinAccepted("ab", 1, 0, 0, 0, 8, SessionAccess.Public, new PlayerId(1), 0));
            accepted[2] = 0xFF; // 시드 문자열 첫 글자를 잘못된 UTF-8로 바꾼다
            Assert.IsFalse(MessageCodec.TryDecode(accepted, out _, out DecodeFailure failure));
            Assert.AreEqual(DecodeFailure.Malformed, failure);
        }

        [Test]
        public void Decode_RejectsSnapshotChunkWithBadIndex()
        {
            var writer = new ByteWriter(64);
            writer.WriteU8((byte)MessageKind.SnapshotChunk);
            writer.WriteU32(1);
            writer.WriteU16(1);
            writer.WriteU16(3); // chunkIndex
            writer.WriteU16(3); // chunkCount (index >= count)
            writer.WriteU16(0);
            Assert.IsFalse(MessageCodec.TryDecode(writer.ToArray(), out _, out _));
        }

        [Test]
        public void AvatarPose_Decode_RejectsWrongLengthAndFlags()
        {
            Assert.IsFalse(MessageCodec.TryDecodeAvatarPose(new byte[19], out _));
            Assert.IsFalse(MessageCodec.TryDecodeAvatarPose(new byte[21], out _));
            Assert.IsFalse(MessageCodec.TryDecodeAvatarPose(null, out _));
            var bad = new byte[AvatarPose.ByteSize];
            bad[15] = 0x80; // 알 수 없는 플래그 비트
            Assert.IsFalse(MessageCodec.TryDecodeAvatarPose(bad, out _));
        }

        [Test]
        public void ChannelsAndDirections_FollowSdd()
        {
            Assert.AreEqual(NetChannel.Unreliable, MessageCodec.DefaultChannel(MessageKind.CreatureSnapshot));
            Assert.AreEqual(NetChannel.Reliable, MessageCodec.DefaultChannel(MessageKind.TableDelta));
            Assert.IsTrue(MessageCodec.IsClientToServer(MessageKind.JoinRequest));
            Assert.IsTrue(MessageCodec.IsClientToServer(MessageKind.Command));
            Assert.IsFalse(MessageCodec.IsClientToServer(MessageKind.JoinAccepted));
            Assert.IsFalse(MessageCodec.IsClientToServer(MessageKind.TableDelta));
        }

        // ---------- ByteWriter / ByteReader ----------

        [Test]
        public void ByteWriter_OverCapacity_FailsWithoutException()
        {
            var writer = new ByteWriter(3);
            writer.WriteU32(1u);
            Assert.IsTrue(writer.Failed);
            writer.Reset();
            Assert.IsFalse(writer.Failed);
            writer.WriteU16(1);
            Assert.AreEqual(2, writer.Length);
        }

        [Test]
        public void ByteReader_ReadingPastEnd_FailsWithoutException()
        {
            var reader = new ByteReader(new byte[] { 1, 2 });
            reader.ReadU32();
            Assert.IsTrue(reader.Failed);
            Assert.AreEqual(0, reader.ReadU8());
        }

        [Test]
        public void ByteReader_ReadBool_RejectsValuesOverOne()
        {
            var reader = new ByteReader(new byte[] { 2 });
            reader.ReadBool();
            Assert.IsTrue(reader.Failed);
        }
    }
}

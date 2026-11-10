// =============================================================
// 파일: MessageCodec.cs
// 역할: 메시지 ↔ 바이트 변환의 단일 입구. 쓰기는 크기 상한을 검사하고, 읽기(TryDecode)는
//       모르는 종류·잘린 입력·상한 초과·남는 바이트를 모두 예외 없이 거부한다.
// 레이어: application (Game.Application/Protocol, 순수 C#)
// 관련: NFR-040(잘못된 입력에도 멈추지 않음), NFR-013, NFR-014, DES-080 / SDD 3.7.2, D-23
// =============================================================
using System;

namespace Game.Application.Protocol
{
    /// <summary>디코드 거부 사유(로그·연결별 실패 집계용).</summary>
    public enum DecodeFailure : byte
    {
        None = 0,
        Empty = 1,
        TooLarge = 2,
        UnknownKind = 3,
        Malformed = 4,
    }

    /// <summary>메시지 코덱. 상태가 없는 정적 클래스(스레드 안전하지 않은 ByteWriter는 호출자가 가진다).</summary>
    public static class MessageCodec
    {
        /// <summary>종류별 패킷 크기 상한(바이트, 종류 바이트 포함). 작은 메시지가 큰 입력을 받지 않게 막는다. [NFR-040]</summary>
        public static int MaxBytes(MessageKind kind)
        {
            switch (kind)
            {
                // 종류 1 + protocolVer 2 + gameVersion(길이 1 + 최대 64) + contentHash 8 + path 1 + devPlayerId 8 + 생성기 버전(개수 1 + u16 × 최대 8)
                case MessageKind.JoinRequest: return 1 + 2 + 1 + ProtocolConstants.MaxGameVersionBytes + 8 + 1 + 8 + 1 + 2 * ProtocolConstants.MaxGeneratorVersions;
                case MessageKind.WorldReady: return 16;
                case MessageKind.Command: return 1 + 1 + 4 + 2 + ProtocolConstants.MaxCommandPayloadBytes;
                case MessageKind.JoinAccepted: return 1 + 1 + ProtocolConstants.MaxSeedTextBytes + 64;
                // 종류 1 + 사유 1 + 호스트 버전(길이 1 + 최대 64)
                case MessageKind.JoinRejected: return 1 + 1 + 1 + ProtocolConstants.MaxGameVersionBytes;
                case MessageKind.JoinComplete: return 24;
                case MessageKind.CommandResult: return 24;
                case MessageKind.SnapshotChunk:
                case MessageKind.TableDelta: return ProtocolConstants.MaxPacketBytes;
                case MessageKind.CreatureSpawn: return 24;
                case MessageKind.CreatureDespawn: return 4;
                case MessageKind.CreatureSnapshot: return 1 + 4 + 2 + ProtocolConstants.MaxCreaturesPerSnapshot * CreatureState.ByteSize;
                default: return 0;
            }
        }

        /// <summary>이 종류를 클라이언트가 서버로 보낼 수 있는지. 서버는 이 종류만 받는다.</summary>
        public static bool IsClientToServer(MessageKind kind) =>
            kind == MessageKind.JoinRequest || kind == MessageKind.WorldReady || kind == MessageKind.Command;

        /// <summary>메시지 종류의 기본 채널. 생물 스냅샷만 비신뢰(손실돼도 다음 것이 덮어씀).</summary>
        public static NetChannel DefaultChannel(MessageKind kind) =>
            kind == MessageKind.CreatureSnapshot ? NetChannel.Unreliable : NetChannel.Reliable;

        /// <summary>
        /// 메시지를 writer에 쓴다(종류 바이트 + 본문). 쓰기 전에 writer를 비운다.
        /// </summary>
        /// <returns>성공하면 true. 종류별 상한이나 용량을 넘으면 false(예외 없음)</returns>
        public static bool TryEncode<T>(in T message, ByteWriter writer) where T : struct, INetMessage
        {
            writer.Reset();
            int max = MaxBytes(message.Kind);
            if (max == 0)
            {
                return false;
            }
            writer.Limit = max;
            writer.WriteU8((byte)message.Kind);
            message.Write(writer);
            return !writer.Failed;
        }

        /// <summary>
        /// 바이트를 메시지로 바꾼다. 실패해도 예외를 던지지 않는다.
        /// </summary>
        /// <param name="data">받은 바이트</param>
        /// <param name="offset">시작 위치</param>
        /// <param name="count">길이</param>
        /// <param name="message">성공하면 박싱된 메시지</param>
        /// <param name="failure">실패 사유(성공이면 None)</param>
        public static bool TryDecode(byte[] data, int offset, int count, out INetMessage message, out DecodeFailure failure)
        {
            message = null;
            failure = DecodeFailure.None;
            if (data == null || offset < 0 || count <= 0 || offset + count > data.Length)
            {
                failure = DecodeFailure.Empty;
                return false;
            }

            var kind = (MessageKind)data[offset];
            int max = MaxBytes(kind);
            if (max == 0)
            {
                failure = DecodeFailure.UnknownKind;
                return false;
            }
            if (count > max)
            {
                failure = DecodeFailure.TooLarge;
                return false;
            }

            var reader = new ByteReader(data, offset + 1, count - 1);
            bool ok;
            switch (kind)
            {
                case MessageKind.JoinRequest: ok = Read(JoinRequest.TryRead(reader, out JoinRequest m1), m1, out message); break;
                case MessageKind.WorldReady: ok = Read(WorldReady.TryRead(reader, out WorldReady m2), m2, out message); break;
                case MessageKind.Command: ok = Read(CommandMessage.TryRead(reader, out CommandMessage m3), m3, out message); break;
                case MessageKind.JoinAccepted: ok = Read(JoinAccepted.TryRead(reader, out JoinAccepted m4), m4, out message); break;
                case MessageKind.JoinRejected: ok = Read(JoinRejected.TryRead(reader, out JoinRejected m5), m5, out message); break;
                case MessageKind.JoinComplete: ok = Read(JoinComplete.TryRead(reader, out JoinComplete m6), m6, out message); break;
                case MessageKind.CommandResult: ok = Read(CommandResultMessage.TryRead(reader, out CommandResultMessage m7), m7, out message); break;
                case MessageKind.SnapshotChunk: ok = Read(SnapshotChunk.TryRead(reader, out SnapshotChunk m8), m8, out message); break;
                case MessageKind.TableDelta: ok = Read(TableDelta.TryRead(reader, out TableDelta m9), m9, out message); break;
                case MessageKind.CreatureSpawn: ok = Read(CreatureSpawn.TryRead(reader, out CreatureSpawn m10), m10, out message); break;
                case MessageKind.CreatureDespawn: ok = Read(CreatureDespawn.TryRead(reader, out CreatureDespawn m11), m11, out message); break;
                case MessageKind.CreatureSnapshot: ok = Read(CreatureSnapshot.TryRead(reader, out CreatureSnapshot m12), m12, out message); break;
                default:
                    failure = DecodeFailure.UnknownKind;
                    return false;
            }

            // 남는 바이트가 있으면 잘못된 메시지로 본다(길이가 어긋난 변조 입력 방지).
            if (!ok || !reader.IsCleanEnd)
            {
                message = null;
                failure = DecodeFailure.Malformed;
                return false;
            }
            return true;
        }

        public static bool TryDecode(byte[] data, out INetMessage message, out DecodeFailure failure) =>
            TryDecode(data, 0, data?.Length ?? 0, out message, out failure);

        private static bool Read<T>(bool parsed, T value, out INetMessage message) where T : struct, INetMessage
        {
            message = parsed ? value : null;
            return parsed;
        }

        // ---------- 아바타 자세(RPC 본문, 종류 바이트 없음) ----------

        /// <summary>아바타 자세 20바이트를 writer에 쓴다(writer를 먼저 비운다).</summary>
        public static bool TryEncodeAvatarPose(in AvatarPose pose, ByteWriter writer)
        {
            writer.Reset();
            writer.Limit = AvatarPose.ByteSize;
            pose.Write(writer);
            return !writer.Failed && writer.Length == AvatarPose.ByteSize;
        }

        /// <summary>아바타 자세를 읽는다. 길이가 20바이트가 아니거나 값이 이상하면 false.</summary>
        public static bool TryDecodeAvatarPose(byte[] data, out AvatarPose pose)
        {
            pose = default;
            if (data == null || data.Length != AvatarPose.ByteSize)
            {
                return false;
            }
            var reader = new ByteReader(data);
            return AvatarPose.TryRead(reader, out pose) && reader.IsCleanEnd;
        }
    }
}

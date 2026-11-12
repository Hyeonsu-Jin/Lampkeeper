// =============================================================
// 파일: Messages.cs
// 역할: 네트워크 메시지 종류(MessageKind), 채널(NetChannel), 프로토콜 상수, 메시지 구조체 전부(M1 범위).
//       각 구조체는 Write(바이트 쓰기)와 TryRead(바이트 읽기)를 가진다. 바이트 배치는 여기서 직접 정한다.
// 레이어: application (Game.Application/Protocol, 순수 C#, FishNet 형식 없음)
// 관련: FR-003, FR-005, FR-007~009, NFR-013, NFR-014, NFR-040, DES-080 / SDD 3.7.2, D-23
//
// 새 메시지는 MessageKind 값을 뒤에 붙인다(기존 값 변경 금지). 프로토콜이 바뀌면 ProtocolConstants.ProtocolVersion을 올린다.
// =============================================================
using System;
using Game.Domain.Common;
using Game.Domain.Session;

namespace Game.Application.Protocol
{
    /// <summary>메시지 첫 바이트. 숫자 값은 전송되므로 바꾸거나 재사용하지 않는다.</summary>
    public enum MessageKind : byte
    {
        None = 0,

        // 클라이언트 → 서버
        JoinRequest = 1,
        WorldReady = 2,
        Command = 3,

        // 서버 → 클라이언트
        JoinAccepted = 16,
        JoinRejected = 17,
        JoinComplete = 18,
        CommandResult = 19,
        SnapshotChunk = 20,
        TableDelta = 21,
        CreatureSpawn = 22,
        CreatureDespawn = 23,
        CreatureSnapshot = 24,
    }

    /// <summary>전송 채널. 신뢰(순서 보장) / 비신뢰.</summary>
    public enum NetChannel : byte
    {
        Reliable = 0,
        Unreliable = 1,
    }

    /// <summary>프로토콜 상수. 값을 바꾸면 ProtocolVersion을 올린다.</summary>
    public static class ProtocolConstants
    {
        /// <summary>프로토콜 버전(JoinRequest, 로비 kh_proto). 메시지 배치를 바꿀 때 올린다.</summary>
        public const ushort ProtocolVersion = 3;

        /// <summary>게임 버전 문자열 최대 UTF-8 바이트(GameVersion.MaxBytes와 같다). 읽기 전에 상한을 확인한다. [R-30, NFR-058]</summary>
        public const int MaxGameVersionBytes = 64;

        /// <summary>한 패킷의 최대 크기(바이트). 스냅샷 조각 16 KB + 머리말 여유. [3.7.2]</summary>
        public const int MaxPacketBytes = 16 * 1024 + 64;

        /// <summary>스냅샷 조각 본문 상한(바이트). [3.7.2: ≤16 KB]</summary>
        public const int MaxChunkPayloadBytes = 16 * 1024;

        /// <summary>명령 본문 상한(바이트). 명령은 작으므로 작게 막는다. [NFR-040]</summary>
        public const int MaxCommandPayloadBytes = 256;

        /// <summary>시드 문자열 최대 UTF-8 바이트.</summary>
        public const int MaxSeedTextBytes = 64;

        /// <summary>JoinRequest가 담을 수 있는 생성기 버전 수 상한.</summary>
        public const int MaxGeneratorVersions = 8;

        /// <summary>생물 스냅샷 한 번에 담는 마리 수 상한.</summary>
        public const int MaxCreaturesPerSnapshot = 256;

        /// <summary>월드 한 변의 절반(m). 월드는 0~2배 값(3072 m). 위치 압축은 여유 16 m를 더해 -16~3088 m를 65536 단계로 나눈다. [3.11.9]</summary>
        public const float PositionHalfRangeMeters = 1536f;

        /// <summary>월드 좌표 검사 여유(m). 가장자리에 붙어 선 플레이어를 거부하지 않게 한다.</summary>
        public const float WorldEdgeMarginMeters = 16f;

        /// <summary>
        /// 수평 위치가 월드 안인지. 월드 좌표는 (0,0)~(3072,3072)다(지형·자원 생성 기준). 원점 중심(±절반)으로 검사하면
        /// 월드 중앙(1536)에서 스폰한 플레이어의 이동이 전부 거부되므로 0~전체 크기로 본다.
        /// </summary>
        public static bool IsInsideWorld(float x, float z)
        {
            float max = 2f * PositionHalfRangeMeters + WorldEdgeMarginMeters;
            return x >= -WorldEdgeMarginMeters && x <= max && z >= -WorldEdgeMarginMeters && z <= max;
        }
    }

    /// <summary>모든 메시지가 구현하는 형식. 구조체라 박싱 없이 <see cref="MessageCodec.TryEncode{T}"/>로 쓴다.</summary>
    public interface INetMessage
    {
        /// <summary>메시지 종류.</summary>
        MessageKind Kind { get; }

        /// <summary>종류 바이트 뒤의 본문을 쓴다.</summary>
        void Write(ByteWriter writer);
    }

    /// <summary>위치·방향 양자화. 쓰기와 읽기가 같은 식을 쓰도록 한 곳에 둔다.</summary>
    public static class Quantize
    {
        private const float Mm = 1000f;
        private const float U16Max = 65535f;

        // 월드 좌표는 0~3072 m이다. 가장자리 여유(IsInsideWorld와 같은 16 m)까지 담도록 -16~3088 m를 65536 단계로 나눈다(약 4.7 cm).
        // 높이(Y)도 같은 범위를 쓴다(지형 높이는 수백 m 이내).
        private const float MinMeters = -ProtocolConstants.WorldEdgeMarginMeters;
        private const float SpanMeters = 2f * ProtocolConstants.PositionHalfRangeMeters + 2f * ProtocolConstants.WorldEdgeMarginMeters;

        /// <summary>미터 → 밀리미터(정수). 범위를 넘으면 자른다.</summary>
        public static int MetersToMillimeters(float meters)
        {
            float mm = (float)Math.Round(meters * Mm);
            if (float.IsNaN(mm))
            {
                return 0;
            }
            return (int)Math.Max(int.MinValue / 2f, Math.Min(int.MaxValue / 2f, mm));
        }

        public static float MillimetersToMeters(int mm) => mm / Mm;

        /// <summary>월드 좌표(m) → 0~65535 단계. 범위 밖은 자른다. [3.11.9]</summary>
        public static ushort PositionToU16(float meters)
        {
            float t = (meters - MinMeters) / SpanMeters;
            if (float.IsNaN(t))
            {
                t = 0f;
            }
            return (ushort)Math.Round(Math.Max(0f, Math.Min(1f, t)) * U16Max);
        }

        public static float U16ToPosition(ushort value) => value / U16Max * SpanMeters + MinMeters;

        /// <summary>각도(도, 어떤 값이든) → 0~65535 단계(0~360도).</summary>
        public static ushort AngleToU16(float degrees)
        {
            float wrapped = degrees % 360f;
            if (float.IsNaN(wrapped))
            {
                wrapped = 0f;
            }
            if (wrapped < 0f)
            {
                wrapped += 360f;
            }
            return (ushort)(Math.Round(wrapped / 360f * 65536f) % 65536f);
        }

        public static float U16ToAngle(ushort value) => value / 65536f * 360f;

        /// <summary>각도(도) → 0~255 단계(0~360도).</summary>
        public static byte AngleToU8(float degrees) => (byte)(AngleToU16(degrees) >> 8);

        public static float U8ToAngle(byte value) => value / 256f * 360f;

        /// <summary>상하 시선(-90~90도) → 0~255 단계.</summary>
        public static byte PitchToU8(float degrees)
        {
            float t = (Math.Max(-90f, Math.Min(90f, float.IsNaN(degrees) ? 0f : degrees)) + 90f) / 180f;
            return (byte)Math.Round(t * 255f);
        }

        public static float U8ToPitch(byte value) => value / 255f * 180f - 90f;
    }

    /// <summary>[C→S] 참가 요청. 자기 SteamID를 적지 않는다(전송이 알려 주는 원격 ID를 씀, D-14).</summary>
    public readonly struct JoinRequest : INetMessage
    {
        public ushort ProtocolVersion { get; }
        /// <summary>게임 버전 전체 문자열(메타데이터 포함). 호스트가 같은 빌드인지 판정한다. [NFR-058]</summary>
        public string GameVersion { get; }
        public ulong ContentHash { get; }
        public JoinPath Path { get; }
        /// <summary>개발 전송(Tugboat)에서만 쓰는 가짜 ID. Steam 전송에서는 무시된다.</summary>
        public ulong DevPlayerId { get; }
        /// <summary>이 PC가 지원하는 월드 생성기 버전 목록(최대 8개).</summary>
        public ushort[] SupportedGeneratorVersions { get; }

        public JoinRequest(ushort protocolVersion, string gameVersion, ulong contentHash, JoinPath path, ulong devPlayerId, ushort[] supportedGeneratorVersions)
        {
            ProtocolVersion = protocolVersion;
            GameVersion = gameVersion ?? string.Empty;
            ContentHash = contentHash;
            Path = path;
            DevPlayerId = devPlayerId;
            SupportedGeneratorVersions = supportedGeneratorVersions ?? Array.Empty<ushort>();
        }

        public MessageKind Kind => MessageKind.JoinRequest;

        public void Write(ByteWriter w)
        {
            w.WriteU16(ProtocolVersion);
            w.WriteString(GameVersion, ProtocolConstants.MaxGameVersionBytes);
            w.WriteU64(ContentHash);
            w.WriteU8((byte)Path);
            w.WriteU64(DevPlayerId);
            ushort[] versions = SupportedGeneratorVersions;
            if (versions.Length > ProtocolConstants.MaxGeneratorVersions)
            {
                w.WriteU8(0);
                w.Fail(); // 상한 초과: 쓰기 실패로 표시
                return;
            }
            w.WriteU8((byte)versions.Length);
            for (int i = 0; i < versions.Length; i++)
            {
                w.WriteU16(versions[i]);
            }
        }

        public static bool TryRead(ByteReader r, out JoinRequest message)
        {
            ushort proto = r.ReadU16();
            // [SDD 3.7.12 (5)] protocolVer는 모든 프로토콜 버전에서 첫 필드다. 다르면 나머지 배치를 알 수 없으므로
            // 해석하지 않고 버리고(호스트가 프로토콜 불일치로 거부), 옛 빌드 참가자가 시간 초과 대신 「버전 불일치」를 보게 한다.
            if (!r.Failed && proto != ProtocolConstants.ProtocolVersion)
            {
                r.SkipRemaining();
                message = new JoinRequest(proto, string.Empty, 0UL, JoinPath.Invite, 0UL, null);
                return true;
            }
            string version = r.ReadString(ProtocolConstants.MaxGameVersionBytes);
            ulong hash = r.ReadU64();
            byte path = r.ReadU8();
            ulong dev = r.ReadU64();
            int count = r.ReadU8();
            if (r.Failed || count > ProtocolConstants.MaxGeneratorVersions || path > (byte)JoinPath.Rejoin)
            {
                message = default;
                return false;
            }
            var versions = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                versions[i] = r.ReadU16();
            }
            message = new JoinRequest(proto, version, hash, (JoinPath)path, dev, versions);
            return !r.Failed;
        }
    }

    /// <summary>[S→C] 참가 허용. 클라이언트가 월드를 로컬 생성하는 데 필요한 값과 호스트 기준 요약값.</summary>
    public readonly struct JoinAccepted : INetMessage
    {
        public string SeedText { get; }
        public ulong Seed { get; }
        public ushort GeneratorVersion { get; }
        /// <summary>호스트가 계산한 월드 요약값. 0이면 아직 월드가 없다는 뜻(M1-07 기본).</summary>
        public ulong ExpectedWorldHash { get; }
        public uint ServerTick { get; }
        public byte MaxPlayers { get; }
        public SessionAccess Access { get; }
        /// <summary>호스트가 정한 이 참가자의 플레이어 ID(Steam이면 본인 SteamID와 같다).</summary>
        public PlayerId YourPlayerId { get; }
        /// <summary>이 참가자의 자리 번호(0 ~ 최대 인원-1). 로스터 표의 키.</summary>
        public byte YourSlot { get; }

        public JoinAccepted(string seedText, ulong seed, ushort generatorVersion, ulong expectedWorldHash, uint serverTick,
            byte maxPlayers, SessionAccess access, PlayerId yourPlayerId, byte yourSlot)
        {
            SeedText = seedText ?? string.Empty;
            Seed = seed;
            GeneratorVersion = generatorVersion;
            ExpectedWorldHash = expectedWorldHash;
            ServerTick = serverTick;
            MaxPlayers = maxPlayers;
            Access = access;
            YourPlayerId = yourPlayerId;
            YourSlot = yourSlot;
        }

        public MessageKind Kind => MessageKind.JoinAccepted;

        public void Write(ByteWriter w)
        {
            w.WriteString(SeedText, ProtocolConstants.MaxSeedTextBytes);
            w.WriteU64(Seed);
            w.WriteU16(GeneratorVersion);
            w.WriteU64(ExpectedWorldHash);
            w.WriteU32(ServerTick);
            w.WriteU8(MaxPlayers);
            w.WriteU8((byte)Access);
            w.WriteU64(YourPlayerId.Value);
            w.WriteU8(YourSlot);
        }

        public static bool TryRead(ByteReader r, out JoinAccepted message)
        {
            string seedText = r.ReadString(ProtocolConstants.MaxSeedTextBytes);
            ulong seed = r.ReadU64();
            ushort gen = r.ReadU16();
            ulong hash = r.ReadU64();
            uint tick = r.ReadU32();
            byte max = r.ReadU8();
            byte access = r.ReadU8();
            ulong player = r.ReadU64();
            byte slot = r.ReadU8();
            if (r.Failed || access > (byte)SessionAccess.Public)
            {
                message = default;
                return false;
            }
            message = new JoinAccepted(seedText, seed, gen, hash, tick, max, (SessionAccess)access, new PlayerId(player), slot);
            return true;
        }
    }

    /// <summary>[S→C] 참가 거부. 버전 불일치(VersionMismatch)일 때 호스트 게임 버전을 함께 실어 참가자가 두 버전을 보게 한다. [FR-003, IMP-002]</summary>
    public readonly struct JoinRejected : INetMessage
    {
        public JoinRejectReason Reason { get; }

        /// <summary>호스트의 게임 버전 전체 문자열(최대 64바이트). 버전 불일치가 아니면 빈 문자열.</summary>
        public string HostVersion { get; }

        public JoinRejected(JoinRejectReason reason, string hostVersion = null)
        {
            Reason = reason;
            HostVersion = hostVersion ?? string.Empty;
        }

        public MessageKind Kind => MessageKind.JoinRejected;

        public void Write(ByteWriter w)
        {
            w.WriteU8((byte)Reason);
            w.WriteString(HostVersion, ProtocolConstants.MaxGameVersionBytes);
        }

        public static bool TryRead(ByteReader r, out JoinRejected message)
        {
            byte value = r.ReadU8();
            // 옛 빌드 호스트(프로토콜 2)는 사유 한 바이트만 보낸다. 남는 바이트가 없으면 호스트 버전 없이 받아들인다.
            string hostVersion = r.Remaining > 0 ? r.ReadString(ProtocolConstants.MaxGameVersionBytes) : string.Empty;
            if (r.Failed || value == 0 || value > (byte)JoinRejectReason.BadRequest)
            {
                message = default;
                return false;
            }
            message = new JoinRejected((JoinRejectReason)value, hostVersion);
            return true;
        }
    }

    /// <summary>[C→S] 클라이언트가 월드를 만들고 계산한 요약값.</summary>
    public readonly struct WorldReady : INetMessage
    {
        public ulong ComputedWorldHash { get; }

        public WorldReady(ulong computedWorldHash)
        {
            ComputedWorldHash = computedWorldHash;
        }

        public MessageKind Kind => MessageKind.WorldReady;

        public void Write(ByteWriter w) => w.WriteU64(ComputedWorldHash);

        public static bool TryRead(ByteReader r, out WorldReady message)
        {
            ulong hash = r.ReadU64();
            message = new WorldReady(hash);
            return !r.Failed;
        }
    }

    /// <summary>[S→C] 스냅샷 전달이 끝났고 조작해도 좋다는 알림. 스폰 위치 포함.</summary>
    public readonly struct JoinComplete : INetMessage
    {
        public uint SnapshotTick { get; }
        public Vec3f Spawn { get; }

        public JoinComplete(uint snapshotTick, Vec3f spawn)
        {
            SnapshotTick = snapshotTick;
            Spawn = spawn;
        }

        public MessageKind Kind => MessageKind.JoinComplete;

        public void Write(ByteWriter w)
        {
            w.WriteU32(SnapshotTick);
            w.WriteI32(Quantize.MetersToMillimeters(Spawn.X));
            w.WriteI32(Quantize.MetersToMillimeters(Spawn.Y));
            w.WriteI32(Quantize.MetersToMillimeters(Spawn.Z));
        }

        public static bool TryRead(ByteReader r, out JoinComplete message)
        {
            uint tick = r.ReadU32();
            float x = Quantize.MillimetersToMeters(r.ReadI32());
            float y = Quantize.MillimetersToMeters(r.ReadI32());
            float z = Quantize.MillimetersToMeters(r.ReadI32());
            message = new JoinComplete(tick, new Vec3f(x, y, z));
            return !r.Failed;
        }
    }

    /// <summary>[C→S] 명령. 본문(Payload)의 배치는 명령 종류마다 CommandCodec이 정한다.</summary>
    public readonly struct CommandMessage : INetMessage
    {
        public byte CommandKindValue { get; }
        public uint RequestId { get; }
        public byte[] Payload { get; }

        public CommandMessage(byte commandKindValue, uint requestId, byte[] payload)
        {
            CommandKindValue = commandKindValue;
            RequestId = requestId;
            Payload = payload ?? Array.Empty<byte>();
        }

        public MessageKind Kind => MessageKind.Command;

        public void Write(ByteWriter w)
        {
            w.WriteU8(CommandKindValue);
            w.WriteU32(RequestId);
            if (Payload.Length > ProtocolConstants.MaxCommandPayloadBytes)
            {
                w.Fail(); // 상한 초과: 쓰기 실패로 표시
                return;
            }
            w.WriteBytes(Payload, 0, Payload.Length);
        }

        public static bool TryRead(ByteReader r, out CommandMessage message)
        {
            byte kind = r.ReadU8();
            uint id = r.ReadU32();
            byte[] payload = r.ReadBytes(ProtocolConstants.MaxCommandPayloadBytes);
            message = new CommandMessage(kind, id, payload);
            return !r.Failed;
        }
    }

    /// <summary>[S→C] 명령 결과.</summary>
    public readonly struct CommandResultMessage : INetMessage
    {
        public uint RequestId { get; }
        public bool Ok { get; }
        public Rejection Rejection { get; }

        public CommandResultMessage(uint requestId, bool ok, Rejection rejection)
        {
            RequestId = requestId;
            Ok = ok;
            Rejection = rejection;
        }

        public MessageKind Kind => MessageKind.CommandResult;

        public void Write(ByteWriter w)
        {
            w.WriteU32(RequestId);
            w.WriteBool(Ok);
            w.WriteU8((byte)Rejection.Reason);
            w.WriteI32(Rejection.ArgA);
            w.WriteI32(Rejection.ArgB);
        }

        public static bool TryRead(ByteReader r, out CommandResultMessage message)
        {
            uint id = r.ReadU32();
            bool ok = r.ReadBool();
            byte reason = r.ReadU8();
            int a = r.ReadI32();
            int b = r.ReadI32();
            message = new CommandResultMessage(id, ok, new Rejection((ReasonCode)reason, a, b));
            return !r.Failed;
        }
    }

    /// <summary>[S→C] 스냅샷 조각. 표 하나의 전체 상태를 16 KB 이하 조각으로 나눈 것.</summary>
    public readonly struct SnapshotChunk : INetMessage
    {
        public uint SnapshotTick { get; }
        public ushort TableId { get; }
        public ushort ChunkIndex { get; }
        public ushort ChunkCount { get; }
        public byte[] Payload { get; }

        public SnapshotChunk(uint snapshotTick, ushort tableId, ushort chunkIndex, ushort chunkCount, byte[] payload)
        {
            SnapshotTick = snapshotTick;
            TableId = tableId;
            ChunkIndex = chunkIndex;
            ChunkCount = chunkCount;
            Payload = payload ?? Array.Empty<byte>();
        }

        public MessageKind Kind => MessageKind.SnapshotChunk;

        public void Write(ByteWriter w)
        {
            w.WriteU32(SnapshotTick);
            w.WriteU16(TableId);
            w.WriteU16(ChunkIndex);
            w.WriteU16(ChunkCount);
            if (Payload.Length > ProtocolConstants.MaxChunkPayloadBytes)
            {
                w.Fail(); // 상한 초과: 쓰기 실패로 표시
                return;
            }
            w.WriteBytes(Payload, 0, Payload.Length);
        }

        public static bool TryRead(ByteReader r, out SnapshotChunk message)
        {
            uint tick = r.ReadU32();
            ushort table = r.ReadU16();
            ushort index = r.ReadU16();
            ushort count = r.ReadU16();
            byte[] payload = r.ReadBytes(ProtocolConstants.MaxChunkPayloadBytes);
            if (r.Failed || count == 0 || index >= count)
            {
                message = default;
                return false;
            }
            message = new SnapshotChunk(tick, table, index, count, payload);
            return true;
        }
    }

    /// <summary>[S→C] 표 변경 목록. 본문 배치는 ReplicatedTable이 정한다(항목 = 키 + 상태).</summary>
    public readonly struct TableDelta : INetMessage
    {
        public uint Tick { get; }
        public ushort TableId { get; }
        public byte[] Payload { get; }

        public TableDelta(uint tick, ushort tableId, byte[] payload)
        {
            Tick = tick;
            TableId = tableId;
            Payload = payload ?? Array.Empty<byte>();
        }

        public MessageKind Kind => MessageKind.TableDelta;

        public void Write(ByteWriter w)
        {
            w.WriteU32(Tick);
            w.WriteU16(TableId);
            if (Payload.Length > ProtocolConstants.MaxChunkPayloadBytes)
            {
                w.Fail(); // 상한 초과: 쓰기 실패로 표시
                return;
            }
            w.WriteBytes(Payload, 0, Payload.Length);
        }

        public static bool TryRead(ByteReader r, out TableDelta message)
        {
            uint tick = r.ReadU32();
            ushort table = r.ReadU16();
            byte[] payload = r.ReadBytes(ProtocolConstants.MaxChunkPayloadBytes);
            message = new TableDelta(tick, table, payload);
            return !r.Failed;
        }
    }

    /// <summary>생물 한 마리의 양자화된 상태(11바이트): id u16, 위치 3×u16, 방향 u8, 애니메이션 u8, 체력% u8. [3.11.9]</summary>
    public readonly struct CreatureState : IEquatable<CreatureState>
    {
        public const int ByteSize = 11;

        public ushort Id { get; }
        public ushort X { get; }
        public ushort Y { get; }
        public ushort Z { get; }
        public byte Yaw { get; }
        public byte Anim { get; }
        public byte HealthPercent { get; }

        public CreatureState(ushort id, ushort x, ushort y, ushort z, byte yaw, byte anim, byte healthPercent)
        {
            Id = id;
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
            Anim = anim;
            HealthPercent = healthPercent;
        }

        /// <summary>실제 좌표·각도로 양자화된 상태를 만든다.</summary>
        public static CreatureState FromWorld(ushort id, Vec3f position, float yawDegrees, byte anim, byte healthPercent) =>
            new CreatureState(id, Quantize.PositionToU16(position.X), Quantize.PositionToU16(position.Y), Quantize.PositionToU16(position.Z),
                Quantize.AngleToU8(yawDegrees), anim, healthPercent);

        public Vec3f Position => new Vec3f(Quantize.U16ToPosition(X), Quantize.U16ToPosition(Y), Quantize.U16ToPosition(Z));

        public void Write(ByteWriter w)
        {
            w.WriteU16(Id);
            w.WriteU16(X);
            w.WriteU16(Y);
            w.WriteU16(Z);
            w.WriteU8(Yaw);
            w.WriteU8(Anim);
            w.WriteU8(HealthPercent);
        }

        public static CreatureState Read(ByteReader r) =>
            new CreatureState(r.ReadU16(), r.ReadU16(), r.ReadU16(), r.ReadU16(), r.ReadU8(), r.ReadU8(), r.ReadU8());

        public bool Equals(CreatureState o) => Id == o.Id && X == o.X && Y == o.Y && Z == o.Z && Yaw == o.Yaw && Anim == o.Anim && HealthPercent == o.HealthPercent;
        public override bool Equals(object obj) => obj is CreatureState o && Equals(o);
        public override int GetHashCode() => Id * 397 ^ X;
    }

    /// <summary>[S→C] 생물이 관심 범위에 들어옴(정의 번호 포함).</summary>
    public readonly struct CreatureSpawn : INetMessage
    {
        public ushort DefId { get; }
        public CreatureState State { get; }

        public CreatureSpawn(ushort defId, CreatureState state)
        {
            DefId = defId;
            State = state;
        }

        public MessageKind Kind => MessageKind.CreatureSpawn;

        public void Write(ByteWriter w)
        {
            w.WriteU16(DefId);
            State.Write(w);
        }

        public static bool TryRead(ByteReader r, out CreatureSpawn message)
        {
            ushort def = r.ReadU16();
            CreatureState state = CreatureState.Read(r);
            message = new CreatureSpawn(def, state);
            return !r.Failed;
        }
    }

    /// <summary>[S→C] 생물이 관심 범위를 벗어남.</summary>
    public readonly struct CreatureDespawn : INetMessage
    {
        public ushort Id { get; }

        public CreatureDespawn(ushort id)
        {
            Id = id;
        }

        public MessageKind Kind => MessageKind.CreatureDespawn;

        public void Write(ByteWriter w) => w.WriteU16(Id);

        public static bool TryRead(ByteReader r, out CreatureDespawn message)
        {
            message = new CreatureDespawn(r.ReadU16());
            return !r.Failed;
        }
    }

    /// <summary>[S→C, 비신뢰] 생물 상태 묶음. 한 메시지에 여러 마리(머리말 비용 절약). [3.11.9]</summary>
    public readonly struct CreatureSnapshot : INetMessage
    {
        public uint Tick { get; }
        public CreatureState[] Creatures { get; }

        public CreatureSnapshot(uint tick, CreatureState[] creatures)
        {
            Tick = tick;
            Creatures = creatures ?? Array.Empty<CreatureState>();
        }

        public MessageKind Kind => MessageKind.CreatureSnapshot;

        public void Write(ByteWriter w)
        {
            w.WriteU32(Tick);
            if (Creatures.Length > ProtocolConstants.MaxCreaturesPerSnapshot)
            {
                w.Fail(); // 상한 초과: 쓰기 실패로 표시
                return;
            }
            w.WriteU16((ushort)Creatures.Length);
            for (int i = 0; i < Creatures.Length; i++)
            {
                Creatures[i].Write(w);
            }
        }

        public static bool TryRead(ByteReader r, out CreatureSnapshot message)
        {
            uint tick = r.ReadU32();
            int count = r.ReadU16();
            if (r.Failed || count > ProtocolConstants.MaxCreaturesPerSnapshot || r.Remaining < count * CreatureState.ByteSize)
            {
                message = default;
                return false;
            }
            var list = new CreatureState[count];
            for (int i = 0; i < count; i++)
            {
                list[i] = CreatureState.Read(r);
            }
            message = new CreatureSnapshot(tick, list);
            return !r.Failed;
        }
    }

    /// <summary>
    /// 아바타 자세(위치, 시선, 이동 플래그, 클라이언트 시각). 아바타 RPC의 바이트 내용(20바이트 고정). [3.9.8]
    /// 위치는 밀리미터 정수, 시선은 양자화한다. 달리기 여부는 호스트가 서버 쪽 스태미나 추정에 쓴다.
    /// </summary>
    public readonly struct AvatarPose : IEquatable<AvatarPose>
    {
        /// <summary>인코딩된 크기(바이트).</summary>
        public const int ByteSize = 20;
        private const byte RunningFlag = 1;

        public Vec3f Position { get; }
        public float YawDegrees { get; }
        public float PitchDegrees { get; }
        public bool Running { get; }
        /// <summary>보낸 쪽 시각(밀리초, 보낸 쪽 시계). 호스트는 차이만 쓴다. 약 49일마다 한 바퀴 돈다.</summary>
        public uint TimeMs { get; }

        public AvatarPose(Vec3f position, float yawDegrees, float pitchDegrees, bool running, uint timeMs)
        {
            Position = position;
            YawDegrees = yawDegrees;
            PitchDegrees = pitchDegrees;
            Running = running;
            TimeMs = timeMs;
        }

        public void Write(ByteWriter w)
        {
            w.WriteI32(Quantize.MetersToMillimeters(Position.X));
            w.WriteI32(Quantize.MetersToMillimeters(Position.Y));
            w.WriteI32(Quantize.MetersToMillimeters(Position.Z));
            w.WriteU16(Quantize.AngleToU16(YawDegrees));
            w.WriteU8(Quantize.PitchToU8(PitchDegrees));
            w.WriteU8(Running ? RunningFlag : (byte)0);
            w.WriteU32(TimeMs);
        }

        public static bool TryRead(ByteReader r, out AvatarPose pose)
        {
            float x = Quantize.MillimetersToMeters(r.ReadI32());
            float y = Quantize.MillimetersToMeters(r.ReadI32());
            float z = Quantize.MillimetersToMeters(r.ReadI32());
            ushort yaw = r.ReadU16();
            byte pitch = r.ReadU8();
            byte flags = r.ReadU8();
            uint time = r.ReadU32();
            if (r.Failed || (flags & ~RunningFlag) != 0)
            {
                pose = default;
                return false;
            }
            pose = new AvatarPose(new Vec3f(x, y, z), Quantize.U16ToAngle(yaw), Quantize.U8ToPitch(pitch), (flags & RunningFlag) != 0, time);
            return true;
        }

        public bool Equals(AvatarPose o) => Position.Equals(o.Position) && YawDegrees == o.YawDegrees && PitchDegrees == o.PitchDegrees && Running == o.Running && TimeMs == o.TimeMs;
        public override bool Equals(object obj) => obj is AvatarPose o && Equals(o);
        public override int GetHashCode() => Position.GetHashCode() * 397 ^ (int)TimeMs;
    }
}

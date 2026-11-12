// =============================================================
// 파일: ResourceStateCodec.cs
// 역할: 자원 표 항목의 바이트 규칙. 키 2바이트(ResourceIndex) + 상태 2바이트(타격 수 8·단계 4·방향 4비트) = 항목당 4바이트.
// 레이어: application (Game.Application/Gathering, 순수 C#)
// 관련: FR-009, NFR-013, NFR-016, DES-053 / SDD 3.11.8
// =============================================================
using Game.Application.Protocol;
using Game.Application.Replication;
using Game.Domain.Gathering;

namespace Game.Application.Gathering
{
    /// <summary>자원 상태 ↔ 바이트. 잘못된 단계 값은 읽기 실패로 돌려준다(변조·손상 방어, NFR-040).</summary>
    public sealed class ResourceStateCodec : IStateCodec<ResourceState>
    {
        /// <summary>항목 하나의 전송 바이트(키 + 상태). 고정 크기 시험이 확인한다.</summary>
        public const int EntryBytes = 4;

        public static ResourceStateCodec Instance { get; } = new ResourceStateCodec();

        public int KeyBytes => 2;
        public int MaxStateBytes => 2;

        public void Write(ByteWriter writer, in ResourceState state) => writer.WriteU16(state.Pack());

        public bool TryRead(ByteReader reader, out ResourceState state)
        {
            ushort packed = reader.ReadU16();
            if (reader.Failed)
            {
                state = default;
                return false;
            }
            return ResourceState.TryUnpack(packed, out state);
        }
    }
}

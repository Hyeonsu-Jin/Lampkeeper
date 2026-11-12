// =============================================================
// 파일: GatheringTypes.cs
// 역할: 자원 오브젝트(나무·돌·광석)의 종류, 단계, 상태 값(ResourceState)과 채집 규칙(HarvestRules)을 정의한다.
// 레이어: domain (Game.Domain/Gathering, 순수 C#)
// 관련: FR-009(바뀐 상태만 동기화), FR-044~047, FR-051(동시 타격은 한 번만), NFR-016, DES-008 / SDD 3.10.4, 3.11.8
//
// 상태 값은 16비트 하나로 묶어 네트워크로 보낸다(SDD 3.11.8): 타격 수 8비트 + 단계 4비트 + 넘어지는 방향 4비트.
// 아무도 건드리지 않은 자원(Pristine)은 표에 넣지 않는다. 표에 있는 항목만 「바뀐 상태」다.
// =============================================================
using System;

namespace Game.Domain.Gathering
{
    /// <summary>자원 종류. 숫자는 생성기(V0)의 자원 종류 번호와 같다(네트워크로 보내지 않고 생성 표에서 읽는다).</summary>
    public enum ResourceKind : byte
    {
        None = 0,
        Tree = 1,
        Stone = 2,
        IronOre = 3,
        Coal = 4,
    }

    /// <summary>자원 오브젝트 단계(SDD 3.10.4). 숫자는 전송되므로 바꾸거나 재사용하지 않는다.</summary>
    public enum ResourcePhase : byte
    {
        /// <summary>손대지 않음(생성 그대로).</summary>
        Pristine = 0,
        /// <summary>타격을 받았지만 아직 서 있음.</summary>
        Damaged = 1,
        /// <summary>나무가 쓰러짐.</summary>
        Felled = 2,
        /// <summary>돌·광석이 부서짐.</summary>
        Broken = 3,
        /// <summary>손 채집으로 거둠.</summary>
        Harvested = 4,
    }

    /// <summary>자원 하나의 바뀐 상태. 16비트(타격 수 8 + 단계 4 + 방향 4)로 묶인다.</summary>
    public readonly struct ResourceState : IEquatable<ResourceState>
    {
        /// <summary>넘어지는 방향 단계 수(360도를 16등분).</summary>
        public const int FallDirections = 16;

        /// <summary>단계 값의 최댓값(그보다 큰 값은 잘못된 데이터).</summary>
        public const byte MaxPhaseValue = (byte)ResourcePhase.Harvested;

        /// <summary>지금까지 받은 타격 수.</summary>
        public byte Hits { get; }

        /// <summary>단계.</summary>
        public ResourcePhase Phase { get; }

        /// <summary>쓰러진 방향(0~15). 쓰러지지 않았다면 0.</summary>
        public byte FallDir { get; }

        public ResourceState(byte hits, ResourcePhase phase, byte fallDir)
        {
            Hits = hits;
            Phase = phase;
            FallDir = (byte)(fallDir & 0xF);
        }

        /// <summary>손대지 않은 상태.</summary>
        public static ResourceState Pristine => default;

        /// <summary>손대지 않은 상태인지(표에 넣을 필요가 없는 상태).</summary>
        public bool IsPristine => Hits == 0 && Phase == ResourcePhase.Pristine && FallDir == 0;

        /// <summary>더 때릴 수 없는 끝 상태인지(쓰러짐·부서짐·거둠).</summary>
        public bool IsFinished => Phase == ResourcePhase.Felled || Phase == ResourcePhase.Broken || Phase == ResourcePhase.Harvested;

        /// <summary>16비트로 묶는다.</summary>
        public ushort Pack() => (ushort)((Hits << 8) | (((byte)Phase & 0xF) << 4) | (FallDir & 0xF));

        /// <summary>16비트에서 읽는다. 단계 값이 범위 밖이면 false(변조·손상 데이터).</summary>
        public static bool TryUnpack(ushort packed, out ResourceState state)
        {
            byte phase = (byte)((packed >> 4) & 0xF);
            if (phase > MaxPhaseValue)
            {
                state = default;
                return false;
            }
            state = new ResourceState((byte)(packed >> 8), (ResourcePhase)phase, (byte)(packed & 0xF));
            return true;
        }

        public bool Equals(ResourceState other) => Pack() == other.Pack();
        public override bool Equals(object obj) => obj is ResourceState other && Equals(other);
        public override int GetHashCode() => Pack();
        public override string ToString() => Phase + "(hits=" + Hits + ", dir=" + FallDir + ")";
    }

    /// <summary>종류별 필요 타격 수(설정 애셋 값에서 만든다).</summary>
    public readonly struct HarvestHitCounts
    {
        public int Tree { get; }
        public int Stone { get; }
        public int IronOre { get; }
        public int Coal { get; }

        public HarvestHitCounts(int tree, int stone, int ironOre, int coal)
        {
            Tree = tree;
            Stone = stone;
            IronOre = ironOre;
            Coal = coal;
        }
    }

    /// <summary>채집 규칙. 순수 함수만 있다(상태를 들고 있지 않음).</summary>
    public static class HarvestRules
    {
        /// <summary>종류에 필요한 타격 수. 알 수 없는 종류는 0(타격할 수 없음).</summary>
        public static int RequiredHits(ResourceKind kind, in HarvestHitCounts counts)
        {
            switch (kind)
            {
                case ResourceKind.Tree: return counts.Tree;
                case ResourceKind.Stone: return counts.Stone;
                case ResourceKind.IronOre: return counts.IronOre;
                case ResourceKind.Coal: return counts.Coal;
                default: return 0;
            }
        }

        /// <summary>지금 더 때릴 수 있는 상태인지. [FR-051] 이미 쓰러진·부서진 대상은 다시 처리하지 않는다.</summary>
        public static bool CanHit(in ResourceState state) => !state.IsFinished;

        /// <summary>
        /// 타격 한 번을 적용한 새 상태를 돌려준다. 필요 횟수에 닿으면 나무는 쓰러지고 돌·광석은 부서진다.
        /// 더 때릴 수 없는 상태면 그대로 돌려준다.
        /// </summary>
        /// <param name="state">지금 상태</param>
        /// <param name="kind">자원 종류</param>
        /// <param name="counts">종류별 필요 타격 수</param>
        /// <param name="fallDir">쓰러질 때 방향(0~15). 마지막 타격자 기준</param>
        public static ResourceState ApplyHit(in ResourceState state, ResourceKind kind, in HarvestHitCounts counts, byte fallDir)
        {
            int required = RequiredHits(kind, counts);
            if (required <= 0 || !CanHit(state))
            {
                return state;
            }
            int hits = Math.Min(state.Hits + 1, byte.MaxValue);
            if (hits >= required)
            {
                return kind == ResourceKind.Tree
                    ? new ResourceState((byte)hits, ResourcePhase.Felled, fallDir)
                    : new ResourceState((byte)hits, ResourcePhase.Broken, 0);
            }
            return new ResourceState((byte)hits, ResourcePhase.Damaged, 0);
        }

        /// <summary>
        /// 타격한 사람에서 대상으로 향하는 방향(수평 차이)을 16등분 값으로 바꾼다(0 = +Z, 시계 방향으로 증가).
        /// 나무는 맞은 사람 반대쪽으로 넘어가므로 「사람 → 나무」 방향을 쓴다.
        /// </summary>
        public static byte QuantizeFallDirection(float dx, float dz)
        {
            double angle = Math.Atan2(dx, dz);
            if (angle < 0d)
            {
                angle += 2d * Math.PI;
            }
            int step = (int)Math.Round(angle / (2d * Math.PI / ResourceState.FallDirections));
            return (byte)(step % ResourceState.FallDirections);
        }
    }
}

// =============================================================
// 파일: AiSettings.cs
// 역할: 생물 AI 설정값(판단 분산, AI LOD 거리, 흐름장 크기)과 AI LOD 규칙(AiLodPolicy).
// 레이어: application (Game.Application/Ai, 순수 C#)
// 관련: FR-079~082, NFR-011, NFR-017, DES-062, DES-105 / SDD 3.11.10, 3.11.11, 3.12.3
//
// 매직 넘버를 한곳에 모았다. 균형 값 설정 애셋(BalanceValues)으로 옮기는 일은 AI가 본격화되는 M4에서 한다.
// =============================================================
using System;

namespace Game.Application.Ai
{
    /// <summary>보행 격자·흐름장·조향 설정(길 찾기 백엔드용).</summary>
    public sealed class AiNavSettings
    {
        /// <summary>보행 격자 한 칸의 한 변(m). SDD 3.11.11: 2 m.</summary>
        public float CellSizeMeters { get; set; } = 2f;

        /// <summary>걸을 수 있는 최대 경사(높이 차 / 거리). 0.8은 약 39도.</summary>
        public float MaxSlope { get; set; } = 0.8f;

        /// <summary>흐름장 반경(칸). 64칸 = 128 m(밤 습격 기준, SDD 3.11.11).</summary>
        public int FlowRadiusCells { get; set; } = 64;

        /// <summary>동시에 두는 흐름장 수(대상 플레이어마다 1개, 최대 8개).</summary>
        public int MaxFlowFields { get; set; } = 8;

        /// <summary>서로 밀어내는 거리(m). 이 안에 다른 생물이 있으면 분리 조향이 작동한다.</summary>
        public float SeparationRadiusMeters { get; set; } = 1.5f;

        /// <summary>분리 조향이 이동 속도에 더해지는 비율.</summary>
        public float SeparationWeight { get; set; } = 0.8f;

        /// <summary>공간 해시 칸 크기(m). 분리 반경 이상이어야 3x3 칸 검색으로 충분하다.</summary>
        public float HashCellMeters { get; set; } = 4f;

        /// <summary>공간 해시 버킷 수(2의 거듭제곱).</summary>
        public int HashBuckets { get; set; } = 1024;

        /// <summary>목적지 앞에서 속도를 줄이기 시작하는 구간 길이(m).</summary>
        public float ArriveSlowMeters { get; set; } = 2f;
    }

    /// <summary>AI 일정 설정.</summary>
    public sealed class AiSettings
    {
        /// <summary>판단 분산 나눔수. 3이면 틱(30 Hz)마다 생물의 1/3만 판단해 판단이 10 Hz가 된다.</summary>
        public int DecisionDivisor { get; set; } = 3;

        /// <summary>Near 거리(m, 미만).</summary>
        public float NearMeters { get; set; } = 40f;

        /// <summary>Mid 거리(m, 미만).</summary>
        public float MidMeters { get; set; } = 120f;

        /// <summary>Far 거리(m, 미만). 이 이상은 정지(Frozen).</summary>
        public float FarMeters { get; set; } = 300f;

        /// <summary>Mid는 판단 차례의 몇 번에 한 번 판단하는지(2 = 5 Hz).</summary>
        public int MidDecisionEvery { get; set; } = 2;

        /// <summary>Far는 판단 차례의 몇 번에 한 번 판단하는지(5 = 약 2 Hz).</summary>
        public int FarDecisionEvery { get; set; } = 5;

        /// <summary>Far는 몇 틱에 한 번 이동하는지(이동 거리는 그만큼 모아서 적분한다).</summary>
        public int FarMoveEveryTicks { get; set; } = 3;

        /// <summary>흐름장을 다시 계산하는 틱 간격(30틱 = 1 Hz).</summary>
        public int FlowRebuildTicks { get; set; } = 30;

        /// <summary>추적 중 멈춰 서는 거리 = 공격 사거리 x 이 값(사거리 안에서 멈추도록).</summary>
        public float SeekStopFactor { get; set; } = 0.7f;

        /// <summary>배회 목적지에서 멈추는 거리(m).</summary>
        public float WanderStopMeters { get; set; } = 0.5f;

        /// <summary>시험용 생물 배치 때 보행 격자를 만드는 범위(월드 중심 기준 한 변의 절반, m).</summary>
        public float TestGridHalfExtentMeters { get; set; } = 300f;

        /// <summary>보행 격자·조향 설정.</summary>
        public AiNavSettings Nav { get; set; } = new AiNavSettings();
    }

    /// <summary>AI LOD 단계(가장 가까운 플레이어와의 거리 기준).</summary>
    public enum AiLod : byte
    {
        /// <summary>40 m 미만: 판단 10 Hz, 이동 매 틱.</summary>
        Near = 0,
        /// <summary>120 m 미만: 판단 5 Hz, 이동 매 틱.</summary>
        Mid = 1,
        /// <summary>300 m 미만: 판단 2 Hz, 이동 3틱마다.</summary>
        Far = 2,
        /// <summary>300 m 이상: 정지.</summary>
        Frozen = 3,
    }

    /// <summary>AI LOD 규칙. 상태 없이 거리와 틱 번호만으로 판단 여부·이동 시간을 정한다.</summary>
    public sealed class AiLodPolicy
    {
        private readonly AiSettings _s;
        private readonly float _nearSq;
        private readonly float _midSq;
        private readonly float _farSq;

        public AiLodPolicy(AiSettings settings)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            if (settings.DecisionDivisor < 1) throw new ArgumentOutOfRangeException(nameof(settings));
            _nearSq = settings.NearMeters * settings.NearMeters;
            _midSq = settings.MidMeters * settings.MidMeters;
            _farSq = settings.FarMeters * settings.FarMeters;
        }

        /// <summary>거리 제곱(m^2)으로 LOD 단계를 정한다. 플레이어가 없으면 float.PositiveInfinity를 넘긴다.</summary>
        public AiLod Classify(float distanceSquared)
        {
            if (distanceSquared < _nearSq) return AiLod.Near;
            if (distanceSquared < _midSq) return AiLod.Mid;
            if (distanceSquared < _farSq) return AiLod.Far;
            return AiLod.Frozen;
        }

        /// <summary>
        /// 이 틱에 이 생물이 판단할 차례인지. 생물 번호를 나눔수로 나눈 나머지가 틱과 맞는 생물만 차례가 되고(틱마다 1/3),
        /// LOD가 멀수록 그 차례 중 일부만 판단한다. 같은 나머지 안에서도 번호로 어긋나게 해 한 틱에 몰리지 않게 한다.
        /// </summary>
        public bool ShouldDecide(AiLod lod, uint tick, int creatureId)
        {
            if (lod == AiLod.Frozen) return false;
            int div = _s.DecisionDivisor;
            if (creatureId % div != (int)(tick % (uint)div)) return false;
            int step = (int)(tick / (uint)div) + creatureId / div;
            switch (lod)
            {
                case AiLod.Mid: return step % Math.Max(1, _s.MidDecisionEvery) == 0;
                case AiLod.Far: return step % Math.Max(1, _s.FarDecisionEvery) == 0;
                default: return true;
            }
        }

        /// <summary>
        /// 이 틱에 이동 적분에 쓸 시간(초). 0이면 이번 틱은 이동하지 않는다.
        /// Far는 N틱에 한 번 이동하되 N틱 치를 한꺼번에 움직인다.
        /// </summary>
        public float MoveSeconds(AiLod lod, uint tick, int creatureId, float tickSeconds)
        {
            switch (lod)
            {
                case AiLod.Near:
                case AiLod.Mid:
                    return tickSeconds;
                case AiLod.Far:
                    int every = Math.Max(1, _s.FarMoveEveryTicks);
                    return (tick + (uint)creatureId) % (uint)every == 0 ? tickSeconds * every : 0f;
                default:
                    return 0f;
            }
        }
    }
}

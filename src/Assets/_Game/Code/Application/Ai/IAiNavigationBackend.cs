// =============================================================
// 파일: IAiNavigationBackend.cs
// 역할: AI 길 찾기·조향 백엔드 포트(IAiNavigationBackend)와 그 입출력 값(WalkGridDesc, AiAgentBuffer, AiNavStats).
// 레이어: application (포트). 구현: Infra.AiJobs.AiJobsBackend(Burst)
// 관련: FR-067, NFR-011, NFR-017, DES-062, DES-105 / SDD 3.11.10, 3.11.11, D-07
//
// 입출력은 모두 관리 배열이다. 백엔드가 안에서 NativeArray로 복사해 Burst 잡을 돌린다(생물 수십~수백 체라 복사 비용은 무시할 만하다).
// =============================================================
using System;

namespace Game.Application.Ai
{
    /// <summary>보행 격자 입력. 칸 중심 높이와 막힌 칸(물·건축물·정화 구역)을 담는다. 백엔드가 경사로 걷기 가능 여부를 정한다.</summary>
    public sealed class WalkGridDesc
    {
        /// <summary>격자 왼쪽 아래 모서리의 월드 X(m).</summary>
        public float OriginX { get; }
        public float OriginZ { get; }
        public float CellSize { get; }
        public int CellsX { get; }
        public int CellsZ { get; }

        /// <summary>칸 중심 높이(m). 길이 CellsX x CellsZ, 행 우선(z * CellsX + x).</summary>
        public float[] Heights { get; }

        /// <summary>막힌 칸(0이 아니면 통행 금지). 길이는 Heights와 같다.</summary>
        public byte[] Blocked { get; }

        public WalkGridDesc(float originX, float originZ, float cellSize, int cellsX, int cellsZ, float[] heights, byte[] blocked)
        {
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (cellsX <= 0 || cellsZ <= 0) throw new ArgumentOutOfRangeException(nameof(cellsX));
            if (heights == null || heights.Length != cellsX * cellsZ) throw new ArgumentException("높이 배열 길이가 격자와 다릅니다.", nameof(heights));
            if (blocked == null || blocked.Length != heights.Length) throw new ArgumentException("막힌 칸 배열 길이가 격자와 다릅니다.", nameof(blocked));
            OriginX = originX;
            OriginZ = originZ;
            CellSize = cellSize;
            CellsX = cellsX;
            CellsZ = cellsZ;
            Heights = heights;
            Blocked = blocked;
        }
    }

    /// <summary>조향 입력·출력 묶음(구조체 배열). 생물 한 마리가 같은 번호의 칸 하나를 쓴다.</summary>
    public sealed class AiAgentBuffer
    {
        /// <summary>출력 플래그: 이번에 위치가 바뀌었다.</summary>
        public const byte FlagMoved = 1;

        /// <summary>출력 플래그: 가려 했으나 막혔다(배회 목적지를 다시 고르는 신호).</summary>
        public const byte FlagBlocked = 2;

        /// <summary>쓰는 칸 수.</summary>
        public int Count;

        // 입력
        public float[] PosX = new float[0];
        public float[] PosZ = new float[0];
        /// <summary>이동 속도(m/s). 0이면 서 있는다.</summary>
        public float[] Speed = new float[0];
        /// <summary>이동 적분 시간(초). 0이면 이번 틱은 이동하지 않는다(AI LOD).</summary>
        public float[] MoveSeconds = new float[0];
        public float[] TargetX = new float[0];
        public float[] TargetZ = new float[0];
        /// <summary>쓸 흐름장 번호. -1이면 목표를 향해 곧장 간다(배회·흐름장 밖).</summary>
        public int[] Field = new int[0];
        /// <summary>목표와 이 거리 안이면 멈춘다(m).</summary>
        public float[] StopDistance = new float[0];

        // 출력
        public float[] OutX = new float[0];
        public float[] OutY = new float[0];
        public float[] OutZ = new float[0];
        /// <summary>이동 방향(도, 0 = +Z). FlagMoved일 때만 의미가 있다.</summary>
        public float[] OutYaw = new float[0];
        public byte[] OutFlags = new byte[0];

        /// <summary>배열 길이를 count 이상으로 키운다(줄이지 않음).</summary>
        public void EnsureCapacity(int count)
        {
            if (PosX.Length >= count) return;
            int n = Math.Max(count, PosX.Length * 2);
            PosX = new float[n]; PosZ = new float[n]; Speed = new float[n]; MoveSeconds = new float[n];
            TargetX = new float[n]; TargetZ = new float[n]; Field = new int[n]; StopDistance = new float[n];
            OutX = new float[n]; OutY = new float[n]; OutZ = new float[n]; OutYaw = new float[n]; OutFlags = new byte[n];
        }
    }

    /// <summary>백엔드 측정값(시험·성능 관찰용).</summary>
    public sealed class AiNavStats
    {
        /// <summary>마지막 보행 격자 구성에 걸린 시간(ms).</summary>
        public double GridBuildMs { get; set; }

        /// <summary>마지막 조향 주기: 예약부터 완료까지 걸린 시간(ms). 즉시 완료하는 시험에서는 잡 실행 시간과 비슷하다.</summary>
        public double LastCycleMs { get; set; }

        /// <summary>마지막 완료 때 메인 스레드가 잡을 기다린 시간(ms). 0에 가까우면 워커에서 이미 끝나 있었다.</summary>
        public double LastCompleteWaitMs { get; set; }

        /// <summary>마지막 주기에 흐름장 계산이 포함되었는지.</summary>
        public bool LastCycleIncludedFlow { get; set; }

        /// <summary>[IMP-018] 마지막으로 잰 흐름장 재계산의 워커 시간(ms, 예약부터 완료까지). 아직 잰 적이 없으면 0.</summary>
        public double LastFlowRecomputeMs { get; set; }

        /// <summary>[IMP-018] 흐름장 재계산을 잰 횟수. 값이 바뀌었을 때만 새 측정값이 생긴 것이다.</summary>
        public int FlowRecomputeSerial { get; set; }

        /// <summary>현재 흐름장 수.</summary>
        public int FlowFieldCount { get; set; }

        /// <summary>마지막 주기의 생물 수.</summary>
        public int LastAgentCount { get; set; }
    }

    /// <summary>AI 길 찾기·조향 백엔드. 메인 스레드에서만 부른다.</summary>
    public interface IAiNavigationBackend : IDisposable
    {
        /// <summary>보행 격자가 만들어졌는지.</summary>
        bool HasWalkGrid { get; }

        /// <summary>조향 잡이 예약되어 아직 완료 전인지.</summary>
        bool IsSteeringPending { get; }

        /// <summary>측정값.</summary>
        AiNavStats Stats { get; }

        /// <summary>보행 격자를 만든다(월드 로드 시 1회). 이전 흐름장은 버린다.</summary>
        void BuildWalkGrid(WalkGridDesc desc);

        /// <summary>월드 위치 한 점이 걸을 수 있는 칸인지. 격자 밖이거나 격자가 없으면 false.</summary>
        bool IsWalkable(float x, float z);

        /// <summary>흐름장 목표(플레이어 위치들)를 정한다. 다음 조향 예약 때 다시 계산한다(최대 MaxFlowFields개, 넘으면 무시).</summary>
        void SetFlowTargets(float[] x, float[] z, int count);

        /// <summary>조향 잡을 예약한다(흐름장 갱신 → 공간 해시 → 조향). 이미 예약되어 있으면 먼저 완료시킨다.</summary>
        void ScheduleSteering(AiAgentBuffer agents);

        /// <summary>예약한 잡을 끝내고 결과를 agents의 출력 배열에 쓴다. 예약이 없으면 아무것도 하지 않는다.</summary>
        void CompleteSteering(AiAgentBuffer agents);
    }
}

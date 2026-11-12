// =============================================================
// 파일: GridJobs.cs
// 역할: 보행 격자 값(GridInfo)과 그 계산 잡(WalkGridJob), 흐름장 잡(FlowFieldJob, 8-이웃 Dijkstra).
// 레이어: infrastructure (Game.Infra.AiJobs, Burst)
// 관련: FR-067, NFR-011, DES-105 / SDD 3.11.11, D-07
//
// NavMesh 대신 2 m 보행 격자 + 정수 비용 흐름장을 쓴다. 잡 안에서는 managed 객체를 쓰지 않는다.
// =============================================================
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Infra.AiJobs
{
    /// <summary>보행 격자 모양(월드 위치 <-> 칸 번호). 값 형식이라 잡에 그대로 넘긴다.</summary>
    internal struct GridInfo
    {
        public float OriginX;
        public float OriginZ;
        public float CellSize;
        public float InvCell;
        public int CellsX;
        public int CellsZ;

        public int CellX(float x) => (int)math.floor((x - OriginX) * InvCell);

        public int CellZ(float z) => (int)math.floor((z - OriginZ) * InvCell);

        public bool InBounds(int cx, int cz) => cx >= 0 && cz >= 0 && cx < CellsX && cz < CellsZ;

        public int Index(int cx, int cz) => cz * CellsX + cx;

        /// <summary>칸 중심 높이를 이중 선형 보간한다(물리 레이캐스트 없이 높이 격자에서 읽는다). 범위 밖은 가장자리 값.</summary>
        public float SampleHeight(NativeArray<float> heights, float x, float z)
        {
            float fx = (x - OriginX) * InvCell - 0.5f;
            float fz = (z - OriginZ) * InvCell - 0.5f;
            int x0 = (int)math.floor(fx);
            int z0 = (int)math.floor(fz);
            float tx = fx - x0;
            float tz = fz - z0;
            int xa = math.clamp(x0, 0, CellsX - 1);
            int xb = math.clamp(x0 + 1, 0, CellsX - 1);
            int za = math.clamp(z0, 0, CellsZ - 1);
            int zb = math.clamp(z0 + 1, 0, CellsZ - 1);
            float h00 = heights[za * CellsX + xa];
            float h10 = heights[za * CellsX + xb];
            float h01 = heights[zb * CellsX + xa];
            float h11 = heights[zb * CellsX + xb];
            return math.lerp(math.lerp(h00, h10, tx), math.lerp(h01, h11, tx), tz);
        }

        /// <summary>월드 위치가 걸을 수 있는 칸인지.</summary>
        public bool WalkableAt(NativeArray<byte> walk, float x, float z)
        {
            int cx = CellX(x);
            int cz = CellZ(z);
            return InBounds(cx, cz) && walk[cz * CellsX + cx] != 0;
        }

        /// <summary>칸 번호가 걸을 수 있는 칸인지(격자 밖은 막힘).</summary>
        public bool WalkableCell(NativeArray<byte> walk, int cx, int cz) => InBounds(cx, cz) && walk[cz * CellsX + cx] != 0;
    }

    /// <summary>8방향 번호 도우미. 0~3은 상하좌우, 4~7은 대각선.</summary>
    internal static class Dir8
    {
        /// <summary>방향 없음(흐름장 칸에 갈 곳이 없음).</summary>
        public const byte None = 255;

        public static void Get(int d, out int dx, out int dz)
        {
            switch (d)
            {
                case 0: dx = 1; dz = 0; break;
                case 1: dx = -1; dz = 0; break;
                case 2: dx = 0; dz = 1; break;
                case 3: dx = 0; dz = -1; break;
                case 4: dx = 1; dz = 1; break;
                case 5: dx = 1; dz = -1; break;
                case 6: dx = -1; dz = 1; break;
                default: dx = -1; dz = -1; break;
            }
        }

        /// <summary>직선 이동 비용 10, 대각선 14(정수 비용).</summary>
        public static int Step(int d) => d >= 4 ? 14 : 10;
    }

    /// <summary>한 흐름장이 덮는 창(격자 칸 좌표)과 유효 여부.</summary>
    internal struct FieldInfo
    {
        public int WinX0;
        public int WinZ0;
        public int Valid;
    }

    /// <summary>칸 중심 높이에서 걸을 수 있는 칸을 정한다(경사 한도 이하이고 막히지 않은 칸).</summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct WalkGridJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Heights;
        [ReadOnly] public NativeArray<byte> Blocked;
        [WriteOnly] public NativeArray<byte> Walk;
        public int CellsX;
        public int CellsZ;
        public float CellSize;
        public float MaxSlope;

        public void Execute(int i)
        {
            int x = i % CellsX;
            int z = i / CellsX;
            float h = Heights[i];
            float steepest = 0f;
            if (x > 0) steepest = math.max(steepest, math.abs(h - Heights[i - 1]));
            if (x < CellsX - 1) steepest = math.max(steepest, math.abs(h - Heights[i + 1]));
            if (z > 0) steepest = math.max(steepest, math.abs(h - Heights[i - CellsX]));
            if (z < CellsZ - 1) steepest = math.max(steepest, math.abs(h - Heights[i + CellsX]));
            bool ok = Blocked[i] == 0 && steepest / CellSize <= MaxSlope;
            Walk[i] = ok ? (byte)1 : (byte)0;
        }
    }

    /// <summary>
    /// 흐름장 한 개: 목표 칸에서 비용 0으로 시작해 8-이웃 Dijkstra로 창 안의 칸마다 목표까지의 비용을 구하고,
    /// 칸마다 비용이 가장 낮은 이웃 방향을 저장한다. 여러 흐름장 잡이 같은 배열의 서로 다른 구간에 쓰므로
    /// 안전 검사를 끈다(구간이 겹치지 않음).
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct FlowFieldJob : IJob
    {
        private const int Infinity = int.MaxValue;

        [ReadOnly] public NativeArray<byte> Walk;
        public GridInfo Grid;
        public int WinX0;
        public int WinZ0;
        public int WinSize;
        public int TargetX;
        public int TargetZ;
        public int Offset;
        public int HeapOffset;
        public int HeapCapacity;
        [NativeDisableContainerSafetyRestriction] public NativeArray<int> Cost;
        [NativeDisableContainerSafetyRestriction] public NativeArray<byte> Dirs;
        [NativeDisableContainerSafetyRestriction] public NativeArray<long> Heap;

        public void Execute()
        {
            int w = WinSize;
            int n = w * w;
            for (int i = 0; i < n; i++)
            {
                Cost[Offset + i] = Infinity;
                Dirs[Offset + i] = Dir8.None;
            }
            if (!Grid.InBounds(TargetX, TargetZ))
            {
                return;
            }

            int heapCount = 0;
            int targetCell = (TargetZ - WinZ0) * w + (TargetX - WinX0);
            Cost[Offset + targetCell] = 0;
            Push(ref heapCount, ((long)0 << 32) | (uint)targetCell);

            while (heapCount > 0)
            {
                long top = Pop(ref heapCount);
                int c = (int)(top >> 32);
                int cell = (int)(top & 0xFFFFFFFFL);
                if (c > Cost[Offset + cell])
                {
                    continue; // 더 싼 길로 이미 갱신됨
                }
                int lx = cell % w;
                int lz = cell / w;
                int gx = WinX0 + lx;
                int gz = WinZ0 + lz;
                for (int d = 0; d < 8; d++)
                {
                    Dir8.Get(d, out int dx, out int dz);
                    int nlx = lx + dx;
                    int nlz = lz + dz;
                    if (nlx < 0 || nlz < 0 || nlx >= w || nlz >= w)
                    {
                        continue;
                    }
                    if (!CanStep(gx, gz, d, dx, dz))
                    {
                        continue;
                    }
                    int nc = c + Dir8.Step(d);
                    int ncell = nlz * w + nlx;
                    if (nc < Cost[Offset + ncell])
                    {
                        Cost[Offset + ncell] = nc;
                        Push(ref heapCount, ((long)nc << 32) | (uint)ncell);
                    }
                }
            }

            // 칸마다 (이웃 비용 + 걸음 비용)이 가장 낮은 방향을 저장한다.
            for (int cell = 0; cell < n; cell++)
            {
                int cc = Cost[Offset + cell];
                if (cc == Infinity || cc == 0)
                {
                    continue;
                }
                int lx = cell % w;
                int lz = cell / w;
                int gx = WinX0 + lx;
                int gz = WinZ0 + lz;
                int best = Infinity;
                byte bestDir = Dir8.None;
                for (int d = 0; d < 8; d++)
                {
                    Dir8.Get(d, out int dx, out int dz);
                    int nlx = lx + dx;
                    int nlz = lz + dz;
                    if (nlx < 0 || nlz < 0 || nlx >= w || nlz >= w)
                    {
                        continue;
                    }
                    int nc = Cost[Offset + nlz * w + nlx];
                    if (nc == Infinity)
                    {
                        continue;
                    }
                    // 대상 칸 자신은 걸을 수 없어도 목적지로 갈 수 있다(비용 0). 그 밖에는 걸을 수 있어야 한다.
                    if (nc != 0 && !CanStep(gx, gz, d, dx, dz))
                    {
                        continue;
                    }
                    int total = nc + Dir8.Step(d);
                    if (total < best)
                    {
                        best = total;
                        bestDir = (byte)d;
                    }
                }
                Dirs[Offset + cell] = bestDir;
            }
        }

        /// <summary>(gx, gz)에서 방향 d로 한 칸 가도 되는지. 도착 칸이 걸을 수 있어야 하고, 대각선은 양옆 칸도 걸을 수 있어야 한다(모서리 끼임 방지).</summary>
        private bool CanStep(int gx, int gz, int d, int dx, int dz)
        {
            if (!Grid.WalkableCell(Walk, gx + dx, gz + dz))
            {
                return false;
            }
            if (d >= 4)
            {
                return Grid.WalkableCell(Walk, gx + dx, gz) && Grid.WalkableCell(Walk, gx, gz + dz);
            }
            return true;
        }

        private void Push(ref int count, long key)
        {
            if (count >= HeapCapacity)
            {
                return; // 가득 차면 버린다(복잡한 지형에서 창 밖 일부 칸이 비게 될 뿐 멈추지 않는다)
            }
            int i = count++;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                long pv = Heap[HeapOffset + parent];
                if (pv <= key)
                {
                    break;
                }
                Heap[HeapOffset + i] = pv;
                i = parent;
            }
            Heap[HeapOffset + i] = key;
        }

        private long Pop(ref int count)
        {
            long top = Heap[HeapOffset];
            long last = Heap[HeapOffset + --count];
            int i = 0;
            while (true)
            {
                int child = i * 2 + 1;
                if (child >= count)
                {
                    break;
                }
                if (child + 1 < count && Heap[HeapOffset + child + 1] < Heap[HeapOffset + child])
                {
                    child++;
                }
                long cv = Heap[HeapOffset + child];
                if (cv >= last)
                {
                    break;
                }
                Heap[HeapOffset + i] = cv;
                i = child;
            }
            if (count > 0)
            {
                Heap[HeapOffset + i] = last;
            }
            return top;
        }
    }
}

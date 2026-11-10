// =============================================================
// 파일: SpatialHashAndSteering.cs
// 역할: 공간 해시(AiSpatialHash, 이웃 검색용 격자 해시)와 조향 잡(SteeringJob, 흐름장 + 분리 + 도착).
// 레이어: infrastructure (Game.Infra.AiJobs, Burst)
// 관련: FR-067, NFR-011, NFR-017, DES-105 / SDD 3.11.11, 3.12.3
//
// 조향 잡은 생물마다 자기 칸(out 배열의 자기 번호)에만 쓴다. 높이는 물리 레이캐스트 없이 높이 격자에서 읽는다.
// =============================================================
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Infra.AiJobs
{
    /// <summary>공간 해시 계산 도우미(잡과 managed 검색이 같은 식을 쓴다).</summary>
    internal static class SpatialHashMath
    {
        public static int Bucket(int cx, int cz, int mask)
        {
            unchecked
            {
                return (int)((uint)(cx * 73856093 ^ cz * 19349663) & (uint)mask);
            }
        }

        /// <summary>3x3 칸 중 번호 c의 버킷이 앞서 본 칸(0..c-1)의 버킷과 겹치는지(해시 충돌 중복 방지).</summary>
        public static bool IsDuplicateBucket(int hx, int hz, int c, int mask)
        {
            int b = Bucket(hx + c % 3 - 1, hz + c / 3 - 1, mask);
            for (int e = 0; e < c; e++)
            {
                if (Bucket(hx + e % 3 - 1, hz + e / 3 - 1, mask) == b)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>위치 목록을 칸 해시로 정렬해 두고 반경 안 이웃을 찾는다. 버킷이 충돌해도 거리를 다시 확인하므로 결과가 맞다.</summary>
    public sealed class AiSpatialHash : IDisposable
    {
        private readonly float _invCell;
        private readonly int _buckets;
        private readonly int _mask;
        private int _capacity;
        private int _count;

        internal NativeArray<float> Px;
        internal NativeArray<float> Pz;
        internal NativeArray<int> Start;
        internal NativeArray<int> Sorted;
        private NativeArray<int> _cursor;
        private NativeArray<int> _bucketOf;

        /// <param name="cellMeters">칸 크기(m). 검색 반경 이상이어야 3x3 칸으로 충분하다</param>
        /// <param name="buckets">버킷 수(2의 거듭제곱)</param>
        public AiSpatialHash(float cellMeters, int buckets)
        {
            if (cellMeters <= 0f) throw new ArgumentOutOfRangeException(nameof(cellMeters));
            if (buckets <= 0 || (buckets & (buckets - 1)) != 0) throw new ArgumentException("버킷 수는 2의 거듭제곱이어야 합니다.", nameof(buckets));
            _invCell = 1f / cellMeters;
            _buckets = buckets;
            _mask = buckets - 1;
            CellMeters = cellMeters;
            Start = new NativeArray<int>(buckets + 1, Allocator.Persistent);
            _cursor = new NativeArray<int>(buckets, Allocator.Persistent);
            EnsureCapacity(64);
        }

        public float CellMeters { get; }

        internal float InvCell => _invCell;
        internal int Mask => _mask;

        /// <summary>마지막으로 만든 위치 수.</summary>
        public int Count => _count;

        internal void EnsureCapacity(int n)
        {
            if (n <= _capacity)
            {
                return;
            }
            int size = Math.Max(n, Math.Max(64, _capacity * 2));
            if (Px.IsCreated)
            {
                Px.Dispose(); Pz.Dispose(); Sorted.Dispose(); _bucketOf.Dispose();
            }
            Px = new NativeArray<float>(size, Allocator.Persistent);
            Pz = new NativeArray<float>(size, Allocator.Persistent);
            Sorted = new NativeArray<int>(size, Allocator.Persistent);
            _bucketOf = new NativeArray<int>(size, Allocator.Persistent);
            _capacity = size;
        }

        /// <summary>Px/Pz에 채운 count개 위치로 해시를 만드는 잡을 예약한다.</summary>
        internal JobHandle Schedule(int count, JobHandle dependency)
        {
            _count = count;
            var job = new BuildJob
            {
                Px = Px, Pz = Pz, Start = Start, Sorted = Sorted, Cursor = _cursor, BucketOf = _bucketOf,
                Count = count, Buckets = _buckets, Mask = _mask, InvCell = _invCell,
            };
            return job.Schedule(dependency);
        }

        /// <summary>위치 목록으로 해시를 바로 만든다(시험·도구용, 잡을 끝까지 기다림).</summary>
        public void Build(float[] x, float[] z, int count)
        {
            if (x == null) throw new ArgumentNullException(nameof(x));
            if (z == null) throw new ArgumentNullException(nameof(z));
            EnsureCapacity(count);
            NativeArray<float>.Copy(x, Px, count);
            NativeArray<float>.Copy(z, Pz, count);
            Schedule(count, default).Complete();
        }

        /// <summary>
        /// (x, z)에서 반경 안 위치의 번호를 results에 더한다(자기 자신을 빼려면 excludeIndex를 넘긴다).
        /// </summary>
        /// <returns>찾은 수</returns>
        public int Query(float x, float z, float radius, int excludeIndex, List<int> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (radius > CellMeters)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "반경은 해시 칸 크기 이하여야 합니다.");
            }
            int found = 0;
            int hx = (int)math.floor(x * _invCell);
            int hz = (int)math.floor(z * _invCell);
            float rSq = radius * radius;
            for (int c = 0; c < 9; c++)
            {
                if (SpatialHashMath.IsDuplicateBucket(hx, hz, c, _mask))
                {
                    continue;
                }
                int b = SpatialHashMath.Bucket(hx + c % 3 - 1, hz + c / 3 - 1, _mask);
                for (int k = Start[b]; k < Start[b + 1]; k++)
                {
                    int j = Sorted[k];
                    if (j == excludeIndex)
                    {
                        continue;
                    }
                    float dx = x - Px[j];
                    float dz = z - Pz[j];
                    if (dx * dx + dz * dz <= rSq)
                    {
                        results.Add(j);
                        found++;
                    }
                }
            }
            return found;
        }

        public void Dispose()
        {
            if (Px.IsCreated)
            {
                Px.Dispose(); Pz.Dispose(); Sorted.Dispose(); _bucketOf.Dispose();
            }
            if (Start.IsCreated) Start.Dispose();
            if (_cursor.IsCreated) _cursor.Dispose();
            _capacity = 0;
        }

        /// <summary>계수 정렬로 버킷별 시작 위치와 정렬된 번호 목록을 만든다.</summary>
        [BurstCompile(CompileSynchronously = true)]
        private struct BuildJob : IJob
        {
            [ReadOnly] public NativeArray<float> Px;
            [ReadOnly] public NativeArray<float> Pz;
            public NativeArray<int> Start;
            public NativeArray<int> Sorted;
            public NativeArray<int> Cursor;
            public NativeArray<int> BucketOf;
            public int Count;
            public int Buckets;
            public int Mask;
            public float InvCell;

            public void Execute()
            {
                for (int b = 0; b <= Buckets; b++) Start[b] = 0;
                for (int i = 0; i < Count; i++)
                {
                    int b = SpatialHashMath.Bucket((int)math.floor(Px[i] * InvCell), (int)math.floor(Pz[i] * InvCell), Mask);
                    BucketOf[i] = b;
                    Start[b + 1]++;
                }
                for (int b = 0; b < Buckets; b++)
                {
                    Start[b + 1] += Start[b];
                    Cursor[b] = Start[b];
                }
                for (int i = 0; i < Count; i++)
                {
                    int b = BucketOf[i];
                    Sorted[Cursor[b]++] = i;
                }
            }
        }
    }

    /// <summary>
    /// 조향: 흐름장 방향(없으면 목표를 향해 곧장) x 속도 + 분리(공간 해시 이웃) + 도착 감속.
    /// 다음 위치가 막힌 칸이면 축별로 미끄러져 보고, 그래도 안 되면 제자리에 두고 막힘 표시를 한다.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct SteeringJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Px;
        [ReadOnly] public NativeArray<float> Pz;
        [ReadOnly] public NativeArray<float> Speed;
        [ReadOnly] public NativeArray<float> MoveSeconds;
        [ReadOnly] public NativeArray<float> TargetX;
        [ReadOnly] public NativeArray<float> TargetZ;
        [ReadOnly] public NativeArray<int> Field;
        [ReadOnly] public NativeArray<float> Stop;
        [ReadOnly] public NativeArray<byte> Walk;
        [ReadOnly] public NativeArray<float> Heights;
        [ReadOnly] public NativeArray<FieldInfo> Fields;
        [ReadOnly] public NativeArray<byte> Dirs;
        [ReadOnly] public NativeArray<int> HashStart;
        [ReadOnly] public NativeArray<int> HashSorted;
        public GridInfo Grid;
        public int FlowWindow;
        public int MaxFields;
        public int HashMask;
        public float HashInvCell;
        public float SeparationRadius;
        public float SeparationWeight;
        public float ArriveSlow;

        [WriteOnly] public NativeArray<float> OutX;
        [WriteOnly] public NativeArray<float> OutY;
        [WriteOnly] public NativeArray<float> OutZ;
        [WriteOnly] public NativeArray<float> OutYaw;
        [WriteOnly] public NativeArray<byte> OutFlags;

        public void Execute(int i)
        {
            float px = Px[i];
            float pz = Pz[i];
            float nx = px;
            float nz = pz;
            float yaw = 0f;
            byte flags = 0;
            float speed = Speed[i];
            float dtm = MoveSeconds[i];

            if (speed > 0f && dtm > 0f)
            {
                float tx = TargetX[i] - px;
                float tz = TargetZ[i] - pz;
                float dist = math.sqrt(tx * tx + tz * tz);

                // 기본 방향: 흐름장이 있으면 흐름장, 없거나 창 밖이면 목표를 향해 곧장.
                float dirX = 0f;
                float dirZ = 0f;
                bool haveFlow = false;
                int f = Field[i];
                if (f >= 0 && f < MaxFields && Fields[f].Valid != 0)
                {
                    int lx = Grid.CellX(px) - Fields[f].WinX0;
                    int lz = Grid.CellZ(pz) - Fields[f].WinZ0;
                    if (lx >= 0 && lz >= 0 && lx < FlowWindow && lz < FlowWindow)
                    {
                        byte d = Dirs[f * FlowWindow * FlowWindow + lz * FlowWindow + lx];
                        if (d != Dir8.None)
                        {
                            Dir8.Get(d, out int dx, out int dz);
                            float inv = d >= 4 ? 0.70710678f : 1f;
                            dirX = dx * inv;
                            dirZ = dz * inv;
                            haveFlow = true;
                        }
                    }
                }
                if (!haveFlow && dist > 1e-4f)
                {
                    dirX = tx / dist;
                    dirZ = tz / dist;
                }

                // 도착 감속: 멈춤 거리 안이면 0, 그 바깥 ArriveSlow 구간에서 서서히 속도를 올린다.
                float scale = math.saturate((dist - Stop[i]) / math.max(ArriveSlow, 1e-3f));
                float vx = dirX * speed * scale;
                float vz = dirZ * speed * scale;

                // 분리: 가까운 이웃에서 멀어지는 방향으로 민다(공간 해시 3x3 칸).
                float sepX = 0f;
                float sepZ = 0f;
                int hx = (int)math.floor(px * HashInvCell);
                int hz = (int)math.floor(pz * HashInvCell);
                for (int c = 0; c < 9; c++)
                {
                    if (SpatialHashMath.IsDuplicateBucket(hx, hz, c, HashMask))
                    {
                        continue;
                    }
                    int b = SpatialHashMath.Bucket(hx + c % 3 - 1, hz + c / 3 - 1, HashMask);
                    for (int k = HashStart[b]; k < HashStart[b + 1]; k++)
                    {
                        int j = HashSorted[k];
                        if (j == i)
                        {
                            continue;
                        }
                        float ox = px - Px[j];
                        float oz = pz - Pz[j];
                        float dsq = ox * ox + oz * oz;
                        if (dsq >= SeparationRadius * SeparationRadius)
                        {
                            continue;
                        }
                        if (dsq < 1e-6f)
                        {
                            sepX += i < j ? 1f : -1f; // 완전히 겹친 둘은 번호로 갈라서 한쪽씩 밀어낸다
                            continue;
                        }
                        float d = math.sqrt(dsq);
                        float w = (SeparationRadius - d) / SeparationRadius;
                        sepX += ox / d * w;
                        sepZ += oz / d * w;
                    }
                }
                vx += sepX * speed * SeparationWeight;
                vz += sepZ * speed * SeparationWeight;
                float len = math.sqrt(vx * vx + vz * vz);
                if (len > speed)
                {
                    vx = vx / len * speed;
                    vz = vz / len * speed;
                    len = speed;
                }

                if (len > 1e-4f)
                {
                    float cx = px + vx * dtm;
                    float cz = pz + vz * dtm;
                    if (Grid.WalkableAt(Walk, cx, cz))
                    {
                        nx = cx;
                        nz = cz;
                    }
                    else if (Grid.WalkableAt(Walk, cx, pz))
                    {
                        nx = cx;
                    }
                    else if (Grid.WalkableAt(Walk, px, cz))
                    {
                        nz = cz;
                    }
                    if (nx != px || nz != pz)
                    {
                        flags |= 1;
                        yaw = math.degrees(math.atan2(nx - px, nz - pz));
                    }
                    else
                    {
                        flags |= 2;
                    }
                }
            }

            OutX[i] = nx;
            OutZ[i] = nz;
            OutY[i] = Grid.SampleHeight(Heights, nx, nz);
            OutYaw[i] = yaw;
            OutFlags[i] = flags;
        }
    }
}

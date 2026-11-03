// =============================================================
// 파일: GeneratedWorld.cs
// 역할: 월드 생성 결과 데이터(DES-047). 정수 높이 격자, 물 마스크, 자원 생성 표(ResourceId·ResourceIndex),
//       일반 등광탑 후보 자리, 높이·경사 질의를 담는다.
// 레이어: domain(생성 규칙, 순수 C#)
// 관련: FR-168, FR-169, NFR-016, NFR-052, DES-047 / SDD 3.11.4, 3.5.5
//
// 단위: 위치는 mm 정수, 높이는 mm 정수, 방향(Yaw)은 16비트 회전 단위(1/65536 바퀴), 크기는 Q8(256 = 1.0).
// 모든 값이 정수라서 어떤 PC에서도 같은 값이다. Unity에 넘길 때(표현 계층)만 부동소수점으로 바꾼다.
// =============================================================
using System;

namespace Game.WorldGen.Model
{
    /// <summary>자원 오브젝트 하나의 생성 표 항목.</summary>
    public readonly struct ResourceEntry
    {
        /// <summary>고정 ID. 지역 → 셀Z → 셀X → 슬롯 순서로 정렬되는 값.</summary>
        public ulong Id { get; }

        /// <summary>자원 종류 번호.</summary>
        public ushort Type { get; }

        /// <summary>X 위치(mm).</summary>
        public int PosXmm { get; }

        /// <summary>높이 Y 위치(mm). 정수 높이에서 계산해 표에 저장한다.</summary>
        public int PosYmm { get; }

        /// <summary>Z 위치(mm).</summary>
        public int PosZmm { get; }

        /// <summary>방향(1/65536 바퀴).</summary>
        public ushort Yaw { get; }

        /// <summary>크기(Q8, 256 = 1.0).</summary>
        public ushort ScaleQ8 { get; }

        public ResourceEntry(ulong id, ushort type, int posXmm, int posYmm, int posZmm, ushort yaw, ushort scaleQ8)
        {
            Id = id;
            Type = type;
            PosXmm = posXmm;
            PosYmm = posYmm;
            PosZmm = posZmm;
            Yaw = yaw;
            ScaleQ8 = scaleQ8;
        }
    }

    /// <summary>일반 등광탑 후보 자리.</summary>
    public readonly struct TowerSite
    {
        /// <summary>고정 ID.</summary>
        public ulong Id { get; }

        /// <summary>X 위치(mm).</summary>
        public int PosXmm { get; }

        /// <summary>Z 위치(mm).</summary>
        public int PosZmm { get; }

        /// <summary>종류(0 = 일반).</summary>
        public byte Kind { get; }

        public TowerSite(ulong id, int posXmm, int posZmm, byte kind)
        {
            Id = id;
            PosXmm = posXmm;
            PosZmm = posZmm;
            Kind = kind;
        }
    }

    /// <summary>
    /// 생성된 월드. 만들어진 뒤에는 바꾸지 않는다(읽기 전용으로 다룬다).
    /// 높이 배열은 행 우선(index = z * Width + x)이고 1 m 간격이다.
    /// </summary>
    public sealed class GeneratedWorld
    {
        /// <summary>높이 격자 간격(mm).</summary>
        public const int HeightStepMm = 1000;

        /// <summary>가로(X) 격자 수.</summary>
        public int Width { get; }

        /// <summary>세로(Z) 격자 수.</summary>
        public int Depth { get; }

        /// <summary>타일 한 변의 격자 수(요약값을 타일별로 계산하는 단위).</summary>
        public int TileSize { get; }

        /// <summary>지역 수(V0는 1).</summary>
        public int RegionCount { get; }

        /// <summary>높이(mm). 행 우선.</summary>
        public int[] Height { get; }

        /// <summary>물 마스크(1 = 물). 물이 없는 생성기는 빈 배열.</summary>
        public byte[] WaterMask { get; }

        /// <summary>자원 생성 표. Id 오름차순이며 배열 위치가 네트워크용 ResourceIndex다.</summary>
        public ResourceEntry[] Resources { get; }

        /// <summary>일반 등광탑 후보 자리. Id 오름차순.</summary>
        public TowerSite[] TowerSites { get; }

        /// <summary>생성기 버전 이름(예: "V0").</summary>
        public string GeneratorVersion { get; }

        /// <summary>생성에 쓴 시드(재시도 시드가 아닌 원래 시드).</summary>
        public ulong Seed { get; }

        /// <summary>몇 번째 시도에서 만들어졌는지(0부터).</summary>
        public int Attempt { get; }

        public GeneratedWorld(
            string generatorVersion,
            ulong seed,
            int attempt,
            int width,
            int depth,
            int tileSize,
            int regionCount,
            int[] height,
            byte[] waterMask,
            ResourceEntry[] resources,
            TowerSite[] towerSites)
        {
            if (width <= 0 || depth <= 0 || tileSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "월드 크기가 올바르지 않습니다.");
            }
            if (height == null || height.Length != width * depth)
            {
                throw new ArgumentException("높이 배열 크기가 가로 x 세로와 다릅니다.", nameof(height));
            }
            GeneratorVersion = generatorVersion ?? string.Empty;
            Seed = seed;
            Attempt = attempt;
            Width = width;
            Depth = depth;
            TileSize = tileSize;
            RegionCount = regionCount;
            Height = height;
            WaterMask = waterMask ?? Array.Empty<byte>();
            Resources = resources ?? Array.Empty<ResourceEntry>();
            TowerSites = towerSites ?? Array.Empty<TowerSite>();
        }

        /// <summary>월드 한 변의 길이(mm) 가로.</summary>
        public int WidthMm => Width * HeightStepMm;

        /// <summary>월드 세로 길이(mm).</summary>
        public int DepthMm => Depth * HeightStepMm;

        /// <summary>X 방향 타일 수.</summary>
        public int TilesX => (Width + TileSize - 1) / TileSize;

        /// <summary>Z 방향 타일 수.</summary>
        public int TilesZ => (Depth + TileSize - 1) / TileSize;

        /// <summary>격자 좌표의 높이(mm). 범위를 벗어나면 가장자리 값을 쓴다.</summary>
        public int HeightAt(int x, int z)
        {
            x = x < 0 ? 0 : (x >= Width ? Width - 1 : x);
            z = z < 0 ? 0 : (z >= Depth ? Depth - 1 : z);
            return Height[z * Width + x];
        }

        /// <summary>
        /// mm 위치의 높이(mm). 주변 네 격자를 정수로 선형 보간한다(부동소수점 없음).
        /// </summary>
        /// <param name="xMm">X 위치(mm)</param>
        /// <param name="zMm">Z 위치(mm)</param>
        public int HeightMmAt(int xMm, int zMm)
        {
            int x0 = FloorDiv(xMm, HeightStepMm);
            int z0 = FloorDiv(zMm, HeightStepMm);
            int fx = xMm - x0 * HeightStepMm;
            int fz = zMm - z0 * HeightStepMm;

            long h00 = HeightAt(x0, z0);
            long h10 = HeightAt(x0 + 1, z0);
            long h01 = HeightAt(x0, z0 + 1);
            long h11 = HeightAt(x0 + 1, z0 + 1);

            long top = h00 * (HeightStepMm - fx) + h10 * fx;
            long bottom = h01 * (HeightStepMm - fx) + h11 * fx;
            long value = top * (HeightStepMm - fz) + bottom * fz;
            return (int)(value / ((long)HeightStepMm * HeightStepMm));
        }

        /// <summary>
        /// 격자 (x, z) 둘레의 기울기(높이 차 mm / 격자 간격 mm, Q16.16). 가장 가파른 이웃 방향 값이다.
        /// 4방향 이웃과의 높이 차 최댓값을 격자 간격으로 나눈다.
        /// </summary>
        public int SlopeQ16(int x, int z)
        {
            int center = HeightAt(x, z);
            int maxDiff = Math.Max(
                Math.Max(Math.Abs(HeightAt(x + 1, z) - center), Math.Abs(HeightAt(x - 1, z) - center)),
                Math.Max(Math.Abs(HeightAt(x, z + 1) - center), Math.Abs(HeightAt(x, z - 1) - center)));
            return (int)(((long)maxDiff << 16) / HeightStepMm);
        }

        private static int FloorDiv(int value, int divisor)
        {
            int q = value / divisor;
            return (value % divisor != 0 && value < 0) ? q - 1 : q;
        }
    }
}

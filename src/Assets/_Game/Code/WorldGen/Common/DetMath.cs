// =============================================================
// 파일: DetMath.cs
// 역할: 월드 생성의 결정론 수학. 고정소수점(Q16.16), 카운터 기반 난수, 정수 값 노이즈.
//       같은 입력이면 어떤 CPU·런타임(Mono, IL2CPP)에서도 같은 정수 결과가 나온다.
// 레이어: domain(생성 규칙, 순수 C#)
// 관련: NFR-053, FR-169, DES-040 / SDD 3.11.1, 3.11.3
//
// 결정론 규칙 (SDD 3.11.1)
// - float, double, Math.Sin 등 부동소수점 금지. 정수와 고정소수점(Q16.16)만 쓴다.
// - 상태를 가진 난수(System.Random, UnityEngine.Random) 금지. 카운터 기반 Rand(키, a, b, c)만 쓴다.
// - 이 파일은 "추가만 허용"한다. 기존 함수의 계산을 바꾸면 모든 월드의 골든 해시가 바뀐다.
//   바꿔야 하면 새 이름의 함수를 만들고 새 생성기 버전에서 쓴다.
// =============================================================
using System;

namespace Game.WorldGen.Common
{
    /// <summary>결정론 정수 수학 모음. 상태가 없는 정적 클래스.</summary>
    public static class DetMath
    {
        /// <summary>Q16.16 고정소수점의 소수부 비트 수.</summary>
        public const int FracBits = 16;

        /// <summary>Q16.16에서 1.0을 뜻하는 정수.</summary>
        public const int One = 1 << FracBits;

        /// <summary>소수부만 뽑는 마스크.</summary>
        public const int FracMask = One - 1;

        /// <summary>지역을 가리지 않는(전역) 스트림에 쓰는 지역 번호.</summary>
        public const int RegionAny = 0xFFFF;

        /// <summary>노이즈 격자 값의 범위를 나타내는 오프셋(값은 -16384..16383).</summary>
        public const int LatticeOffset = 16384;

        private const ulong CoordMultiplierA = 0x9E3779B97F4A7C15UL;
        private const ulong CoordMultiplierB = 0xC2B2AE3D27D4EB4FUL;
        private const ulong CoordMultiplierC = 0x165667B19E3779F9UL;
        private const int LatticeShift = 49;
        private const int RegionShift = 48;
        private const int StageShift = 32;
        private const ulong StageMask = 0xFFFFUL;

        // ---------- 고정소수점 ----------

        /// <summary>정수를 Q16.16으로 바꾼다.</summary>
        public static int ToQ16(int whole) => whole << FracBits;

        /// <summary>
        /// Q16.16 곱셈. 중간값은 long으로 계산하고 산술 시프트(부호 유지)로 줄인다.
        /// </summary>
        public static int MulQ16(int a, int b) => (int)(((long)a * b) >> FracBits);

        // ---------- 난수 ----------

        /// <summary>64비트 값을 섞는다(SplitMix64 마무리 함수).</summary>
        public static ulong Mix64(ulong x) => SplitMix64.Mix(x);

        /// <summary>
        /// 지역·단계·재시도마다 독립인 난수 스트림 키를 만든다.
        /// </summary>
        /// <param name="worldSeed">월드 시드(u64)</param>
        /// <param name="regionId">지역 번호(전역이면 <see cref="RegionAny"/>)</param>
        /// <param name="stageId">생성 단계 번호(16비트까지)</param>
        /// <param name="attempt">재생성 시도 번호</param>
        public static ulong StreamKey(ulong worldSeed, int regionId, int stageId, int attempt)
        {
            unchecked
            {
                ulong packed = ((ulong)(uint)regionId << RegionShift)
                               | (((ulong)(uint)stageId & StageMask) << StageShift)
                               | (uint)attempt;
                return Mix64(worldSeed ^ Mix64(packed));
            }
        }

        /// <summary>
        /// 재생성 시도(attempt)에 쓰는 시드. 0번째는 원래 시드 그대로, 이후는 시도 번호를 섞어 파생한다.
        /// </summary>
        public static ulong RetrySeed(ulong seed, int attempt)
        {
            if (attempt == 0)
            {
                return seed;
            }
            unchecked
            {
                return Mix64(seed ^ ((ulong)(uint)attempt * SplitMix64.GoldenGamma));
            }
        }

        /// <summary>
        /// 카운터 기반 난수. 정수 좌표(a, b, c)에서 상태 없이 64비트 값을 얻는다.
        /// 음수 좌표는 부호를 확장한 64비트 값으로 섞는다.
        /// </summary>
        public static ulong Rand(ulong key, int a, int b, int c)
        {
            unchecked
            {
                ulong mixed = (ulong)(long)a * CoordMultiplierA
                              + (ulong)(long)b * CoordMultiplierB
                              + (ulong)(long)c * CoordMultiplierC;
                return Mix64(key ^ Mix64(mixed));
            }
        }

        /// <summary>
        /// [0, n) 범위 정수 난수. 나눗셈과 실수 없이 상위 32비트에 n을 곱해 줄인다.
        /// </summary>
        /// <param name="n">상한(0이면 0을 돌려줌)</param>
        public static uint RandBelow(ulong key, int a, int b, int c, uint n)
        {
            unchecked
            {
                return (uint)(((Rand(key, a, b, c) >> 32) * n) >> 32);
            }
        }

        // ---------- 정수 값 노이즈 ----------

        /// <summary>격자점의 노이즈 값. 범위 -16384..16383 (Q15 기준 -0.5..0.5).</summary>
        public static int LatticeQ15(ulong key, int ix, int iz)
        {
            unchecked
            {
                return (int)(Rand(key, ix, iz, 0) >> LatticeShift) - LatticeOffset;
            }
        }

        /// <summary>부드러운 보간 곡선 3t^2 - 2t^3. 입력·출력 모두 Q16.16(0..65535 → 0..65536).</summary>
        public static int SmoothQ16(int tQ16)
        {
            long t = tQ16;
            long t2 = (t * t) >> FracBits;
            long t3 = (t2 * t) >> FracBits;
            return (int)(3 * t2 - 2 * t3);
        }

        /// <summary>
        /// 2차원 값 노이즈. 좌표는 Q16.16이고 정수부가 격자 번호다.
        /// </summary>
        /// <returns>-16384..16383 범위의 값(Q15)</returns>
        public static int ValueNoise(ulong key, int xQ16, int zQ16)
        {
            int ix = xQ16 >> FracBits;
            int iz = zQ16 >> FracBits;
            int sx = SmoothQ16(xQ16 & FracMask);
            int sz = SmoothQ16(zQ16 & FracMask);

            int a = LatticeQ15(key, ix, iz);
            int b = LatticeQ15(key, ix + 1, iz);
            int c = LatticeQ15(key, ix, iz + 1);
            int d = LatticeQ15(key, ix + 1, iz + 1);

            int ab = a + (int)(((long)(b - a) * sx) >> FracBits);
            int cd = c + (int)(((long)(d - c) * sx) >> FracBits);
            return ab + (int)(((long)(cd - ab) * sz) >> FracBits);
        }

        /// <summary>
        /// 여러 옥타브를 겹친 프랙탈 노이즈. 옥타브가 오를 때마다 주파수는 2배, 진폭은 gain배가 된다.
        /// </summary>
        /// <param name="key">스트림 키</param>
        /// <param name="xQ16">X 좌표(Q16.16)</param>
        /// <param name="zQ16">Z 좌표(Q16.16)</param>
        /// <param name="octaves">옥타브 수(1 이상)</param>
        /// <param name="gainQ16">옥타브마다 곱하는 진폭 비율(Q16.16, 보통 0.5 = 32768)</param>
        public static int Fbm(ulong key, int xQ16, int zQ16, int octaves, int gainQ16)
        {
            int sum = 0;
            int amp = One;
            for (int o = 0; o < octaves; o++)
            {
                ulong octaveKey = Rand(key, o, 0, 0);
                int noise = ValueNoise(octaveKey, xQ16 << o, zQ16 << o);
                sum += (int)(((long)noise * amp) >> FracBits);
                amp = (int)(((long)amp * gainQ16) >> FracBits);
            }
            return sum;
        }

        // ---------- 보조 ----------

        /// <summary>값을 [min, max] 안으로 자른다.</summary>
        public static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);

        /// <summary>(a * b) / c 를 long 중간값으로 계산한다. 나눗셈은 0 방향으로 버림(C# 정수 나눗셈 규칙).</summary>
        public static int MulDiv(int a, int b, int c) => (int)((long)a * b / c);
    }
}

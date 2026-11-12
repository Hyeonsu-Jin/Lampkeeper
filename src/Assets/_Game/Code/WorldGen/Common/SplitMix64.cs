// =============================================================
// 파일: SplitMix64.cs
// 역할: SplitMix64 난수 함수. 마무리 함수(Mix)는 카운터 기반 난수의 바탕이고,
//       Next는 상태를 하나씩 올리는 원래의 순차 생성기 형태(시험·시드 파생용)다.
// 레이어: domain(생성 규칙, 순수 C#)
// 관련: NFR-053, FR-169, DES-040 / SDD 3.11.1, 3.11.3
//
// 결정론 규칙: 모든 연산은 unchecked ulong 곱셈·시프트·XOR뿐이다(부동소수점 없음).
// 이 파일의 결과 값이 바뀌면 모든 월드가 바뀌므로 상수를 고치지 않는다(골든 값 시험이 지킨다).
// =============================================================
namespace Game.WorldGen.Common
{
    /// <summary>SplitMix64 정수 난수 함수.</summary>
    public static class SplitMix64
    {
        /// <summary>상태에 더하는 황금비 상수.</summary>
        public const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

        private const ulong MixMultiplier1 = 0xBF58476D1CE4E5B9UL;
        private const ulong MixMultiplier2 = 0x94D049BB133111EBUL;
        private const int MixShift1 = 30;
        private const int MixShift2 = 27;
        private const int MixShift3 = 31;

        /// <summary>
        /// 64비트 값을 섞는 마무리 함수(SDD 3.11.3의 Mix64).
        /// </summary>
        /// <param name="x">섞을 값</param>
        /// <returns>섞인 64비트 값</returns>
        public static ulong Mix(ulong x)
        {
            unchecked
            {
                x ^= x >> MixShift1;
                x *= MixMultiplier1;
                x ^= x >> MixShift2;
                x *= MixMultiplier2;
                x ^= x >> MixShift3;
                return x;
            }
        }

        /// <summary>
        /// 순차 생성기의 다음 값. 상태를 황금비 상수만큼 올린 뒤 섞는다.
        /// </summary>
        /// <param name="state">생성기 상태(호출할 때마다 바뀜)</param>
        /// <returns>다음 64비트 값</returns>
        public static ulong Next(ref ulong state)
        {
            unchecked
            {
                state += GoldenGamma;
                return Mix(state);
            }
        }
    }
}

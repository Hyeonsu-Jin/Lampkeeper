// =============================================================
// 파일: SeedCodec.cs
// 역할: 시드 문자열 <-> u64 변환(DES-041). 사용자가 입력한 글자를 UTF-8 바이트 그대로 해시해 시드를 만들고,
//       「무작위」를 고르면 혼동되는 글자를 뺀 32자 알파벳으로 10글자 시드 문자열을 만든다.
// 레이어: domain(생성 규칙, 순수 C#)
// 관련: FR-167, NFR-053, DES-041 / SDD 3.11.2
//
// 규칙
// - 앞뒤 ASCII 공백만 제거한다. 대소문자는 구분한다. 문화권 의존 API(ToUpper, Normalize)는 쓰지 않는다.
// - 빈 글자이거나 UTF-8 64바이트를 넘으면 오류(InvalidSeed).
// - 하이픈은 "표시용"이다. 화면에는 K7QM-2RTX-9B처럼 보여 주지만 월드 파일과 해시에는 하이픈 없는 글자를 쓴다.
//   사용자가 하이픈까지 직접 입력하면 그 글자 그대로 시드가 된다(다른 시드).
// =============================================================
using System;
using System.Security.Cryptography;
using System.Text;

namespace Game.WorldGen.Common
{
    /// <summary>시드 문자열 해석 결과 종류.</summary>
    public enum SeedParseError
    {
        /// <summary>오류 없음.</summary>
        None = 0,

        /// <summary>공백 제거 뒤 비어 있음.</summary>
        Empty = 1,

        /// <summary>UTF-8로 64바이트를 넘음.</summary>
        TooLong = 2,
    }

    /// <summary>시드 처리. 상태가 없는 정적 클래스.</summary>
    public static class SeedCodec
    {
        /// <summary>시드 문자열 해시에 쓰는 XXH64 소금 값(변경 금지: 모든 시드 값이 바뀜).</summary>
        public const ulong SeedSalt = 0x4B48534545445F56UL;

        /// <summary>시드 문자열의 최대 UTF-8 바이트 수.</summary>
        public const int MaxUtf8Bytes = 64;

        /// <summary>무작위 시드 문자열의 글자 수.</summary>
        public const int RandomSeedLength = 10;

        /// <summary>무작위 시드 알파벳(0/O, 1/I를 뺀 32자).</summary>
        public const string Alphabet32 = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        /// <summary>표시용 하이픈을 넣는 간격: 4-4-2.</summary>
        private const int DisplayGroup = 4;

        /// <summary>
        /// 입력 글자를 u64 시드로 바꾼다.
        /// </summary>
        /// <param name="text">사용자가 입력한 시드 글자</param>
        /// <param name="seed">성공하면 시드 값</param>
        /// <returns>오류 종류(성공이면 None)</returns>
        public static SeedParseError TryParse(string text, out ulong seed)
        {
            seed = 0;
            string trimmed = TrimAsciiWhitespace(text ?? string.Empty);
            if (trimmed.Length == 0)
            {
                return SeedParseError.Empty;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(trimmed);
            if (bytes.Length > MaxUtf8Bytes)
            {
                return SeedParseError.TooLong;
            }
            seed = Xxh64.Hash(bytes, SeedSalt);
            return SeedParseError.None;
        }

        /// <summary>
        /// 입력 글자를 u64 시드로 바꾼다. 형식이 틀리면 예외.
        /// </summary>
        /// <exception cref="ArgumentException">빈 글자이거나 64바이트를 넘을 때</exception>
        public static ulong SeedFromText(string text)
        {
            SeedParseError error = TryParse(text, out ulong seed);
            if (error != SeedParseError.None)
            {
                throw new ArgumentException("시드 글자가 올바르지 않습니다: " + error, nameof(text));
            }
            return seed;
        }

        /// <summary>
        /// 암호학적 난수로 무작위 시드 문자열(10글자)을 만든다. 생성 시점에만 쓰므로 결정론은 필요 없다.
        /// </summary>
        public static string RandomSeedText()
        {
            byte[] random = new byte[RandomSeedLength];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(random);
            }
            return RandomSeedTextFrom(random);
        }

        /// <summary>
        /// 주어진 무작위 바이트로 시드 문자열을 만든다(시험용으로 바이트를 넣을 수 있게 분리).
        /// 알파벳이 32자라 바이트의 하위 5비트를 쓰면 치우침이 없다.
        /// </summary>
        /// <param name="random">최소 10바이트</param>
        public static string RandomSeedTextFrom(byte[] random)
        {
            if (random == null || random.Length < RandomSeedLength)
            {
                throw new ArgumentException("무작위 바이트가 " + RandomSeedLength + "개 이상 필요합니다.", nameof(random));
            }
            var builder = new StringBuilder(RandomSeedLength);
            for (int i = 0; i < RandomSeedLength; i++)
            {
                builder.Append(Alphabet32[random[i] & 31]);
            }
            return builder.ToString();
        }

        /// <summary>화면에 보여 줄 모양(4-4-2 하이픈). 월드 파일과 해시에는 하이픈 없는 글자를 쓴다.</summary>
        public static string FormatForDisplay(string seedText)
        {
            if (string.IsNullOrEmpty(seedText) || seedText.Length != RandomSeedLength)
            {
                return seedText ?? string.Empty;
            }
            return seedText.Substring(0, DisplayGroup) + "-" + seedText.Substring(DisplayGroup, DisplayGroup) + "-" + seedText.Substring(2 * DisplayGroup);
        }

        /// <summary>앞뒤의 ASCII 공백(스페이스, 탭, 줄바꿈)만 제거한다. 전각 공백 등은 남긴다.</summary>
        public static string TrimAsciiWhitespace(string text)
        {
            int start = 0;
            int end = text.Length;
            while (start < end && IsAsciiWhitespace(text[start]))
            {
                start++;
            }
            while (end > start && IsAsciiWhitespace(text[end - 1]))
            {
                end--;
            }
            return text.Substring(start, end - start);
        }

        private static bool IsAsciiWhitespace(char c) => c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\v' || c == '\f';
    }
}

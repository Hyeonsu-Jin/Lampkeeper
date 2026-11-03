// =============================================================
// 파일: DetMathTests.cs
// 역할: 결정론 수학(SplitMix64, XXH64, DetMath, SeedCodec)의 단위 시험.
//       기대값은 공개된 시험 값(XXH64, SplitMix64)과, 같은 알고리즘을 별도 언어(Python)로 따로 구현해 얻은 값이다.
// 레이어: test (EditMode, Game.Tests.WorldGen)
// 관련: NFR-053, FR-167, FR-169, DES-040, DES-041 / SDD 3.11
// =============================================================
using System;
using System.Text;
using Game.WorldGen.Common;
using NUnit.Framework;

namespace Game.Tests.WorldGen
{
    public class DetMathTests
    {
        // ---------- SplitMix64 ----------

        [Test]
        public void SplitMix64_Next_MatchesPublishedVector()
        {
            // 공개 시험 값: 시드 1234567에서 시작한 처음 5개 값
            ulong state = 1234567UL;
            Assert.AreEqual(6457827717110365317UL, SplitMix64.Next(ref state));
            Assert.AreEqual(3203168211198807973UL, SplitMix64.Next(ref state));
            Assert.AreEqual(9817491932198370423UL, SplitMix64.Next(ref state));
            Assert.AreEqual(4593380528125082431UL, SplitMix64.Next(ref state));
            Assert.AreEqual(16408922859458223821UL, SplitMix64.Next(ref state));
        }

        [Test]
        public void Mix64_KnownValues()
        {
            Assert.AreEqual(0UL, DetMath.Mix64(0UL));
            Assert.AreEqual(0x5692161d100b05e5UL, DetMath.Mix64(1UL));
        }

        // ---------- XXH64 ----------

        [Test]
        public void Xxh64_MatchesPublishedVectors()
        {
            Assert.AreEqual(0xEF46DB3751D8E999UL, Xxh64.Hash(Array.Empty<byte>(), 0));
            Assert.AreEqual(0xD24EC4F1A98C6E5BUL, Xxh64.HashUtf8("a", 0));
            Assert.AreEqual(0x44BC2CF5AD770999UL, Xxh64.HashUtf8("abc", 0));
            // 32바이트가 넘는 긴 입력(묶음 처리 경로)
            Assert.AreEqual(0xFBCEA83C8A378BF1UL, Xxh64.HashUtf8("Nobody inspects the spammish repetition", 0));
        }

        [Test]
        public void Xxh64_Stream_AnyChunking_EqualsOneShot()
        {
            var data = new byte[1000];
            ulong state = 99;
            for (int i = 0; i < data.Length; i++) data[i] = (byte)SplitMix64.Next(ref state);

            ulong expected = Xxh64.Hash(data, 12345UL);
            int[] chunkSizes = { 1, 3, 7, 31, 32, 33, 64, 100, 999 };
            foreach (int chunk in chunkSizes)
            {
                var stream = new Xxh64Stream(12345UL);
                for (int offset = 0; offset < data.Length; offset += chunk)
                {
                    stream.Write(new ReadOnlySpan<byte>(data, offset, Math.Min(chunk, data.Length - offset)));
                }
                Assert.AreEqual(expected, stream.Digest(), "조각 크기 " + chunk);
            }
        }

        [Test]
        public void Xxh64_Stream_Digest_DoesNotChangeState()
        {
            var stream = new Xxh64Stream(1UL);
            stream.WriteUInt64(42UL);
            ulong first = stream.Digest();
            Assert.AreEqual(first, stream.Digest());
            stream.WriteUInt64(43UL);
            Assert.AreNotEqual(first, stream.Digest());
        }

        [Test]
        public void Xxh64_Int32Array_IsLittleEndianBytes()
        {
            int[] values = { 1, -2, 0x01020304, int.MinValue };
            var expectedBytes = new byte[16];
            for (int i = 0; i < values.Length; i++)
            {
                uint v = unchecked((uint)values[i]);
                expectedBytes[i * 4] = (byte)v;
                expectedBytes[i * 4 + 1] = (byte)(v >> 8);
                expectedBytes[i * 4 + 2] = (byte)(v >> 16);
                expectedBytes[i * 4 + 3] = (byte)(v >> 24);
            }
            var stream = new Xxh64Stream(7UL);
            stream.WriteInt32Array(values, 0, values.Length);
            Assert.AreEqual(Xxh64.Hash(expectedBytes, 7UL), stream.Digest());
        }

        // ---------- 카운터 기반 난수 ----------

        private const ulong Key = 0x123456789ABCDEF0UL;

        [Test]
        public void Rand_MatchesReferenceImplementation_IncludingNegativeCoordinates()
        {
            Assert.AreEqual(0x1c6310075d8e7d31UL, DetMath.Rand(Key, 1, 2, 3));
            Assert.AreEqual(0xa9aed23dc59aa64eUL, DetMath.Rand(Key, -1, -2, -3));
            Assert.AreEqual(898U, DetMath.RandBelow(Key, 5, 6, 7, 1000U));
        }

        [Test]
        public void StreamKey_MatchesReferenceImplementation()
        {
            Assert.AreEqual(0x9d80af8951343e9aUL, DetMath.StreamKey(0xDEADBEEFCAFEF00DUL, DetMath.RegionAny, 4, 2));
            Assert.AreEqual(0xfe1a024661722f06UL, DetMath.StreamKey(77UL, 0, 1, 0));
        }

        [Test]
        public void StreamKey_DiffersByRegionStageAndAttempt()
        {
            ulong baseKey = DetMath.StreamKey(5UL, 1, 1, 0);
            Assert.AreNotEqual(baseKey, DetMath.StreamKey(5UL, 2, 1, 0));
            Assert.AreNotEqual(baseKey, DetMath.StreamKey(5UL, 1, 2, 0));
            Assert.AreNotEqual(baseKey, DetMath.StreamKey(5UL, 1, 1, 1));
        }

        [Test]
        public void RetrySeed_AttemptZeroKeepsSeed_OthersDiffer()
        {
            Assert.AreEqual(1234UL, DetMath.RetrySeed(1234UL, 0));
            Assert.AreNotEqual(1234UL, DetMath.RetrySeed(1234UL, 1));
            Assert.AreNotEqual(DetMath.RetrySeed(1234UL, 1), DetMath.RetrySeed(1234UL, 2));
        }

        [Test]
        public void RandBelow_StaysInRange_AndCoversRangeRoughlyEvenly()
        {
            const uint n = 10;
            var counts = new int[n];
            for (int i = 0; i < 20000; i++)
            {
                uint v = DetMath.RandBelow(Key, i, -i, 3, n);
                Assert.Less(v, n);
                counts[v]++;
            }
            foreach (int count in counts)
            {
                Assert.That(count, Is.InRange(1700, 2300), "한 칸에 몰렸습니다.");
            }
            Assert.AreEqual(0U, DetMath.RandBelow(Key, 1, 1, 1, 0U));
        }

        // ---------- 고정소수점 ----------

        [Test]
        public void MulQ16_UsesArithmeticShift_ForNegativeValues()
        {
            Assert.AreEqual(DetMath.One, DetMath.MulQ16(DetMath.One, DetMath.One));
            Assert.AreEqual(DetMath.One / 2, DetMath.MulQ16(DetMath.One, DetMath.One / 2));
            // -3 * 0.5 = -1.5 -> 산술 시프트는 바닥(-2)으로 내린다. 0 방향 버림(-1)이 아니다.
            Assert.AreEqual(-2, DetMath.MulQ16(-3, 1 << 15));
            Assert.AreEqual(1, DetMath.MulQ16(3, 1 << 15));
        }

        [Test]
        public void SmoothQ16_ReferenceValues()
        {
            Assert.AreEqual(0, DetMath.SmoothQ16(0));
            Assert.AreEqual(32768, DetMath.SmoothQ16(32768));
            Assert.AreEqual(65536, DetMath.SmoothQ16(65535));
            Assert.AreEqual(4111, DetMath.SmoothQ16(10000));
        }

        // ---------- 값 노이즈 ----------

        [Test]
        public void LatticeQ15_MatchesReference_AndStaysInRange()
        {
            Assert.AreEqual(2119, DetMath.LatticeQ15(Key, 3, -4));
            Assert.AreEqual(-3152, DetMath.LatticeQ15(Key, -1, 0));
            for (int i = -200; i < 200; i++)
            {
                Assert.That(DetMath.LatticeQ15(Key, i, i * 7), Is.InRange(-16384, 16383));
            }
        }

        [Test]
        public void ValueNoise_MatchesReference()
        {
            Assert.AreEqual(9648, DetMath.ValueNoise(Key, 5 * 65536 + 12345, -3 * 65536 + 54321));
            Assert.AreEqual(-4588, DetMath.ValueNoise(Key, -100000, 200000));
        }

        [Test]
        public void ValueNoise_AtLatticePoint_EqualsLatticeValue()
        {
            Assert.AreEqual(DetMath.LatticeQ15(Key, 7, -9), DetMath.ValueNoise(Key, 7 << 16, -9 << 16));
        }

        [Test]
        public void Fbm_MatchesReference()
        {
            Assert.AreEqual(-7163, DetMath.Fbm(Key, 123456, -654321, 5, 32768));
            Assert.AreEqual(7051, DetMath.Fbm(Key, 3072 << 9, 3071 << 9, 4, 32768));
        }

        // ---------- SeedCodec ----------

        [Test]
        public void SeedCodec_MatchesReferenceHash_AndTrimsAsciiWhitespaceOnly()
        {
            Assert.AreEqual(0x9377e0b0209ebf95UL, SeedCodec.SeedFromText("golden-seed-1"));
            Assert.AreEqual(0x33aa00bab40e819dUL, SeedCodec.SeedFromText("KkeojinHaneul"));
            Assert.AreEqual(0xdf2659202b2ca48fUL, SeedCodec.SeedFromText("K7QM2RTX9B"));
            Assert.AreEqual(0xa35ce5894b715c62UL, SeedCodec.SeedFromText("abc"), "공백 제거 뒤 abc");
            Assert.AreEqual(SeedCodec.SeedFromText("abc"), SeedCodec.SeedFromText("  \t abc \r\n"));
        }

        [Test]
        public void SeedCodec_IsCaseSensitive_AndKeepsInnerHyphens()
        {
            Assert.AreNotEqual(SeedCodec.SeedFromText("abc"), SeedCodec.SeedFromText("ABC"));
            Assert.AreNotEqual(SeedCodec.SeedFromText("K7QM2RTX9B"), SeedCodec.SeedFromText("K7QM-2RTX-9B"));
        }

        [Test]
        public void SeedCodec_UsesUtf8Bytes_NotCharacterCount()
        {
            // 한글 한 글자는 UTF-8 3바이트: 21자 = 63바이트(허용), 22자 = 66바이트(초과)
            string ok = new string('가', 21);
            string tooLong = new string('가', 22);
            Assert.AreEqual(SeedParseError.None, SeedCodec.TryParse(ok, out ulong seed));
            Assert.AreEqual(Xxh64.Hash(Encoding.UTF8.GetBytes(ok), SeedCodec.SeedSalt), seed);
            Assert.AreEqual(SeedParseError.TooLong, SeedCodec.TryParse(tooLong, out _));
        }

        [Test]
        public void SeedCodec_Rejects_EmptyAndTooLong_AcceptsExactly64Bytes()
        {
            Assert.AreEqual(SeedParseError.Empty, SeedCodec.TryParse("", out _));
            Assert.AreEqual(SeedParseError.Empty, SeedCodec.TryParse("   \t", out _));
            Assert.AreEqual(SeedParseError.Empty, SeedCodec.TryParse(null, out _));
            Assert.AreEqual(SeedParseError.None, SeedCodec.TryParse(new string('a', 64), out _));
            Assert.AreEqual(SeedParseError.TooLong, SeedCodec.TryParse(new string('a', 65), out _));
            Assert.Throws<ArgumentException>(() => SeedCodec.SeedFromText(""));
        }

        [Test]
        public void SeedCodec_RandomSeedText_UsesSafeAlphabet_AndFormatsForDisplay()
        {
            for (int i = 0; i < 50; i++)
            {
                string text = SeedCodec.RandomSeedText();
                Assert.AreEqual(SeedCodec.RandomSeedLength, text.Length);
                foreach (char c in text)
                {
                    Assert.GreaterOrEqual(SeedCodec.Alphabet32.IndexOf(c), 0, "허용하지 않는 글자: " + c);
                }
                Assert.AreEqual(SeedParseError.None, SeedCodec.TryParse(text, out _));
            }
            Assert.AreEqual(32, SeedCodec.Alphabet32.Length);
            Assert.AreEqual(-1, SeedCodec.Alphabet32.IndexOf('O'));
            Assert.AreEqual(-1, SeedCodec.Alphabet32.IndexOf('0'));
            Assert.AreEqual(-1, SeedCodec.Alphabet32.IndexOf('I'));
            Assert.AreEqual(-1, SeedCodec.Alphabet32.IndexOf('1'));

            string fixedText = SeedCodec.RandomSeedTextFrom(new byte[] { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 });
            Assert.AreEqual(10, fixedText.Length);
            Assert.AreEqual(fixedText.Substring(0, 4) + "-" + fixedText.Substring(4, 4) + "-" + fixedText.Substring(8), SeedCodec.FormatForDisplay(fixedText));
        }
    }
}

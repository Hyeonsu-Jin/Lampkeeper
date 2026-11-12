// =============================================================
// 파일: Xxh64.cs
// 역할: XXH64 해시를 직접 구현한다(스트리밍 + 한 번에 계산). 라이브러리 버전이 바뀌어도 값이 변하지 않게
//       자체 구현으로 고정한다. 시드 문자열 해시(SeedCodec)와 월드 요약값(WorldHasher)이 쓴다.
// 레이어: domain(생성 규칙, 순수 C#)
// 관련: NFR-053, FR-167, FR-169, DES-040, DES-041, DES-046 / SDD 3.11.2, 3.11.5
//
// 결정론 규칙
// - 정수 연산만 쓴다. 숫자는 항상 리틀 엔디언 바이트로 풀어서 넣는다(CPU 바이트 순서와 무관).
// - 알고리즘은 공개된 XXH64 사양 그대로라서 외부 구현과 값이 같아야 한다(시험에서 공개 시험 값으로 확인).
// =============================================================
using System;
using System.Buffers.Binary;
using System.Text;

namespace Game.WorldGen.Common
{
    /// <summary>XXH64 스트리밍 해시 계산기. 값을 순서대로 쓰고 마지막에 Digest로 읽는다.</summary>
    public sealed class Xxh64Stream
    {
        private const ulong Prime1 = 11400714785074694791UL;
        private const ulong Prime2 = 14029467366897019727UL;
        private const ulong Prime3 = 1609587929392839161UL;
        private const ulong Prime4 = 9650029242287828579UL;
        private const ulong Prime5 = 2870177450012600261UL;

        /// <summary>한 번에 처리하는 묶음 크기(바이트).</summary>
        private const int StripeSize = 32;

        /// <summary>정수 배열을 바이트로 풀 때 쓰는 임시 칸의 정수 개수.</summary>
        private const int IntChunk = 512;

        private readonly ulong _seed;
        private readonly byte[] _buffer = new byte[StripeSize];
        private readonly byte[] _scratch = new byte[IntChunk * sizeof(int)];
        private int _bufferLength;
        private ulong _totalLength;
        private ulong _v1;
        private ulong _v2;
        private ulong _v3;
        private ulong _v4;

        /// <summary>새 계산기를 만든다.</summary>
        /// <param name="seed">XXH64 시드(소금 값)</param>
        public Xxh64Stream(ulong seed)
        {
            _seed = seed;
            unchecked
            {
                _v1 = seed + Prime1 + Prime2;
                _v2 = seed + Prime2;
                _v3 = seed;
                _v4 = seed - Prime1;
            }
        }

        /// <summary>바이트 묶음을 넣는다.</summary>
        public void Write(ReadOnlySpan<byte> data)
        {
            _totalLength += (ulong)data.Length;
            int offset = 0;

            // 이전에 남은 바이트가 있으면 먼저 32바이트를 채운다.
            if (_bufferLength > 0)
            {
                int need = StripeSize - _bufferLength;
                int take = Math.Min(need, data.Length);
                data.Slice(0, take).CopyTo(new Span<byte>(_buffer, _bufferLength, take));
                _bufferLength += take;
                offset = take;
                if (_bufferLength < StripeSize)
                {
                    return;
                }
                ConsumeStripe(new ReadOnlySpan<byte>(_buffer, 0, StripeSize));
                _bufferLength = 0;
            }

            // 32바이트씩 곧바로 처리한다.
            while (data.Length - offset >= StripeSize)
            {
                ConsumeStripe(data.Slice(offset, StripeSize));
                offset += StripeSize;
            }

            // 남은 바이트는 다음 호출을 위해 보관한다.
            int rest = data.Length - offset;
            if (rest > 0)
            {
                data.Slice(offset, rest).CopyTo(new Span<byte>(_buffer, 0, rest));
                _bufferLength = rest;
            }
        }

        /// <summary>글자를 UTF-8로 바꿔 넣는다. 앞에 바이트 길이(uint32)를 붙여 경계를 구분한다.</summary>
        public void WriteString(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            WriteUInt32((uint)bytes.Length);
            Write(bytes);
        }

        /// <summary>1바이트 값을 넣는다.</summary>
        public void WriteByte(byte value)
        {
            Span<byte> tmp = stackalloc byte[1];
            tmp[0] = value;
            Write(tmp);
        }

        /// <summary>uint16을 리틀 엔디언으로 넣는다.</summary>
        public void WriteUInt16(ushort value)
        {
            Span<byte> tmp = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(tmp, value);
            Write(tmp);
        }

        /// <summary>uint32를 리틀 엔디언으로 넣는다.</summary>
        public void WriteUInt32(uint value)
        {
            Span<byte> tmp = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(tmp, value);
            Write(tmp);
        }

        /// <summary>int32를 리틀 엔디언으로 넣는다.</summary>
        public void WriteInt32(int value)
        {
            Span<byte> tmp = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(tmp, value);
            Write(tmp);
        }

        /// <summary>uint64를 리틀 엔디언으로 넣는다.</summary>
        public void WriteUInt64(ulong value)
        {
            Span<byte> tmp = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(tmp, value);
            Write(tmp);
        }

        /// <summary>int32 배열의 일부를 리틀 엔디언 바이트열로 풀어 넣는다(CPU 바이트 순서와 무관).</summary>
        /// <param name="values">값 배열</param>
        /// <param name="start">시작 위치</param>
        /// <param name="count">개수</param>
        public void WriteInt32Array(int[] values, int start, int count)
        {
            int index = start;
            int end = start + count;
            while (index < end)
            {
                int n = Math.Min(IntChunk, end - index);
                for (int i = 0; i < n; i++)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(_scratch, i * sizeof(int), sizeof(int)), values[index + i]);
                }
                Write(new ReadOnlySpan<byte>(_scratch, 0, n * sizeof(int)));
                index += n;
            }
        }

        /// <summary>
        /// 지금까지 넣은 값의 해시를 돌려준다. 계산기 상태는 바꾸지 않으므로 이어서 더 쓸 수 있다.
        /// </summary>
        public ulong Digest()
        {
            unchecked
            {
                ulong h;
                if (_totalLength >= StripeSize)
                {
                    h = RotateLeft(_v1, 1) + RotateLeft(_v2, 7) + RotateLeft(_v3, 12) + RotateLeft(_v4, 18);
                    h = MergeRound(h, _v1);
                    h = MergeRound(h, _v2);
                    h = MergeRound(h, _v3);
                    h = MergeRound(h, _v4);
                }
                else
                {
                    h = _seed + Prime5;
                }
                h += _totalLength;

                int p = 0;
                while (p + 8 <= _bufferLength)
                {
                    ulong k1 = Round(0, BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(_buffer, p, 8)));
                    h ^= k1;
                    h = RotateLeft(h, 27) * Prime1 + Prime4;
                    p += 8;
                }
                if (p + 4 <= _bufferLength)
                {
                    h ^= BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(_buffer, p, 4)) * Prime1;
                    h = RotateLeft(h, 23) * Prime2 + Prime3;
                    p += 4;
                }
                while (p < _bufferLength)
                {
                    h ^= _buffer[p] * Prime5;
                    h = RotateLeft(h, 11) * Prime1;
                    p++;
                }

                // 마지막 섞기(avalanche)
                h ^= h >> 33;
                h *= Prime2;
                h ^= h >> 29;
                h *= Prime3;
                h ^= h >> 32;
                return h;
            }
        }

        private void ConsumeStripe(ReadOnlySpan<byte> stripe)
        {
            _v1 = Round(_v1, BinaryPrimitives.ReadUInt64LittleEndian(stripe.Slice(0, 8)));
            _v2 = Round(_v2, BinaryPrimitives.ReadUInt64LittleEndian(stripe.Slice(8, 8)));
            _v3 = Round(_v3, BinaryPrimitives.ReadUInt64LittleEndian(stripe.Slice(16, 8)));
            _v4 = Round(_v4, BinaryPrimitives.ReadUInt64LittleEndian(stripe.Slice(24, 8)));
        }

        private static ulong Round(ulong acc, ulong input)
        {
            unchecked
            {
                acc += input * Prime2;
                acc = RotateLeft(acc, 31);
                acc *= Prime1;
                return acc;
            }
        }

        private static ulong MergeRound(ulong acc, ulong value)
        {
            unchecked
            {
                value = Round(0, value);
                acc ^= value;
                return acc * Prime1 + Prime4;
            }
        }

        private static ulong RotateLeft(ulong value, int bits) => (value << bits) | (value >> (64 - bits));
    }

    /// <summary>XXH64 한 번에 계산하기.</summary>
    public static class Xxh64
    {
        /// <summary>바이트열의 XXH64 해시.</summary>
        /// <param name="data">입력 바이트</param>
        /// <param name="seed">XXH64 시드</param>
        public static ulong Hash(ReadOnlySpan<byte> data, ulong seed)
        {
            var stream = new Xxh64Stream(seed);
            stream.Write(data);
            return stream.Digest();
        }

        /// <summary>글자를 UTF-8 바이트 그대로 해시한다(문화권·정규화 영향 없음).</summary>
        public static ulong HashUtf8(string text, ulong seed) => Hash(Encoding.UTF8.GetBytes(text ?? string.Empty), seed);
    }
}

// =============================================================
// 파일: ByteIo.cs
// 역할: 네트워크 메시지 바이트 배치용 ByteWriter / ByteReader. 리틀 엔디언, 문자열은 길이 접두 UTF-8.
// 레이어: application (Game.Application/Protocol, 순수 C#)
// 관련: NFR-013, NFR-014, NFR-040, DES-080 / SDD 3.7.2 「형식과 인코딩」, D-23
//
// 규칙
//  - 쓰기: 용량을 넘으면 예외 대신 Failed 표시를 하고 이후 쓰기는 무시한다(호출자가 Failed를 확인).
//  - 읽기: 모자라거나 잘못된 입력이면 예외 대신 Failed 표시를 하고 0/빈 값을 돌려준다.
//  - 바이트 배치는 여기서 직접 정하므로 라이브러리 버전과 무관하다(고정 바이트 시험으로 고정).
// =============================================================
using System;
using System.Text;

namespace Game.Application.Protocol
{
    /// <summary>버퍼에 값을 차례로 쓰는 도구. 버퍼는 재사용할 수 있다(<see cref="Reset"/>).</summary>
    public sealed class ByteWriter
    {
        private readonly byte[] _buffer;
        private int _position;

        /// <param name="capacity">최대 쓰기 크기(바이트). 넘으면 Failed</param>
        /// <exception cref="ArgumentOutOfRangeException">capacity가 1보다 작을 때</exception>
        public ByteWriter(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "용량은 1 이상이어야 한다.");
            }
            _buffer = new byte[capacity];
        }

        /// <summary>쓴 바이트 수.</summary>
        public int Length => _position;

        /// <summary>용량 초과 등으로 쓰기에 실패했는지.</summary>
        public bool Failed { get; private set; }

        /// <summary>쓰기 상한(바이트). 0이면 용량 전체.</summary>
        public int Limit { get; set; }

        /// <summary>내부 버퍼(앞 <see cref="Length"/> 바이트만 유효). 전송 직전 복사용.</summary>
        public byte[] Buffer => _buffer;

        /// <summary>쓰기 실패로 표시한다(상한 초과 등 호출자가 알아낸 오류).</summary>
        public void Fail() => Failed = true;

        /// <summary>처음부터 다시 쓸 수 있게 비운다.</summary>
        public void Reset()
        {
            _position = 0;
            Failed = false;
            Limit = 0;
        }

        /// <summary>쓴 내용을 새 배열로 복사한다.</summary>
        public byte[] ToArray()
        {
            var copy = new byte[_position];
            Array.Copy(_buffer, copy, _position);
            return copy;
        }

        private bool Reserve(int count)
        {
            int max = Limit > 0 ? Math.Min(Limit, _buffer.Length) : _buffer.Length;
            if (Failed || _position + count > max)
            {
                Failed = true;
                return false;
            }
            return true;
        }

        public void WriteU8(byte value)
        {
            if (Reserve(1))
            {
                _buffer[_position++] = value;
            }
        }

        public void WriteBool(bool value) => WriteU8(value ? (byte)1 : (byte)0);

        public void WriteU16(ushort value)
        {
            if (Reserve(2))
            {
                _buffer[_position++] = (byte)value;
                _buffer[_position++] = (byte)(value >> 8);
            }
        }

        public void WriteI16(short value) => WriteU16(unchecked((ushort)value));

        public void WriteU32(uint value)
        {
            if (Reserve(4))
            {
                for (int i = 0; i < 4; i++)
                {
                    _buffer[_position++] = (byte)(value >> (8 * i));
                }
            }
        }

        public void WriteI32(int value) => WriteU32(unchecked((uint)value));

        public void WriteU64(ulong value)
        {
            if (Reserve(8))
            {
                for (int i = 0; i < 8; i++)
                {
                    _buffer[_position++] = (byte)(value >> (8 * i));
                }
            }
        }

        /// <summary>길이(u16) + 바이트. 65535바이트를 넘으면 Failed.</summary>
        public void WriteBytes(byte[] data, int offset, int count)
        {
            if (data == null || count < 0 || count > ushort.MaxValue || offset < 0 || offset + count > data.Length)
            {
                Failed = true;
                return;
            }
            WriteU16((ushort)count);
            if (Reserve(count))
            {
                Array.Copy(data, offset, _buffer, _position, count);
                _position += count;
            }
        }

        /// <summary>길이(u8) + UTF-8. 인코딩 결과가 maxBytes(≤255)를 넘으면 Failed.</summary>
        public void WriteString(string value, int maxBytes)
        {
            value = value ?? string.Empty;
            if (maxBytes < 0 || maxBytes > byte.MaxValue)
            {
                Failed = true;
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > maxBytes)
            {
                Failed = true;
                return;
            }
            WriteU8((byte)bytes.Length);
            if (Reserve(bytes.Length))
            {
                Array.Copy(bytes, 0, _buffer, _position, bytes.Length);
                _position += bytes.Length;
            }
        }
    }

    /// <summary>바이트 배열에서 값을 차례로 읽는 도구. 잘못된 입력은 예외 없이 Failed 표시.</summary>
    public sealed class ByteReader
    {
        private readonly byte[] _data;
        private readonly int _end;
        private int _position;

        /// <param name="data">읽을 배열</param>
        /// <param name="offset">시작 위치</param>
        /// <param name="count">읽을 바이트 수</param>
        public ByteReader(byte[] data, int offset, int count)
        {
            if (data == null || offset < 0 || count < 0 || offset + count > data.Length)
            {
                _data = Array.Empty<byte>();
                _end = 0;
                _position = 0;
                Failed = true;
                return;
            }
            _data = data;
            _position = offset;
            _end = offset + count;
        }

        public ByteReader(byte[] data) : this(data, 0, data?.Length ?? 0) { }

        /// <summary>읽기에 실패했는지(모자란 입력 등).</summary>
        public bool Failed { get; private set; }

        /// <summary>남은 바이트 수.</summary>
        public int Remaining => _end - _position;

        /// <summary>모두 읽었고 실패도 없는지(남는 바이트가 있으면 잘못된 메시지로 본다).</summary>
        public bool IsCleanEnd => !Failed && _position == _end;

        private bool Need(int count)
        {
            if (Failed || count < 0 || _position + count > _end)
            {
                Failed = true;
                return false;
            }
            return true;
        }

        /// <summary>남은 바이트를 읽지 않고 버린다(다른 프로토콜 버전의 본문 처럼 해석할 수 없는 부분).</summary>
        public void SkipRemaining()
        {
            if (!Failed)
            {
                _position = _end;
            }
        }

        public byte ReadU8() => Need(1) ? _data[_position++] : (byte)0;

        public bool ReadBool()
        {
            byte value = ReadU8();
            if (value > 1)
            {
                Failed = true;
            }
            return value == 1;
        }

        public ushort ReadU16()
        {
            if (!Need(2))
            {
                return 0;
            }
            ushort value = (ushort)(_data[_position] | (_data[_position + 1] << 8));
            _position += 2;
            return value;
        }

        public short ReadI16() => unchecked((short)ReadU16());

        public uint ReadU32()
        {
            if (!Need(4))
            {
                return 0;
            }
            uint value = 0;
            for (int i = 0; i < 4; i++)
            {
                value |= (uint)_data[_position + i] << (8 * i);
            }
            _position += 4;
            return value;
        }

        public int ReadI32() => unchecked((int)ReadU32());

        public ulong ReadU64()
        {
            if (!Need(8))
            {
                return 0;
            }
            ulong value = 0;
            for (int i = 0; i < 8; i++)
            {
                value |= (ulong)_data[_position + i] << (8 * i);
            }
            _position += 8;
            return value;
        }

        /// <summary>길이(u16) + 바이트를 새 배열로 읽는다. maxBytes를 넘으면 Failed.</summary>
        public byte[] ReadBytes(int maxBytes)
        {
            int count = ReadU16();
            if (Failed || count > maxBytes || !Need(count))
            {
                Failed = true;
                return Array.Empty<byte>();
            }
            var result = new byte[count];
            Array.Copy(_data, _position, result, 0, count);
            _position += count;
            return result;
        }

        /// <summary>길이(u8) + UTF-8 문자열. maxBytes를 넘거나 UTF-8이 아니면 Failed.</summary>
        public string ReadString(int maxBytes)
        {
            int count = ReadU8();
            if (Failed || count > maxBytes || !Need(count))
            {
                Failed = true;
                return string.Empty;
            }
            try
            {
                string text = new UTF8Encoding(false, true).GetString(_data, _position, count);
                _position += count;
                return text;
            }
            catch (ArgumentException)
            {
                // 잘못된 UTF-8(변조된 입력). 예외를 밖으로 내보내지 않고 실패로 표시한다.
                Failed = true;
                return string.Empty;
            }
        }
    }
}

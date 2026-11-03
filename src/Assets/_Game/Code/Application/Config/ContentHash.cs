// =============================================================
// 파일: ContentHash.cs
// 역할: 설정값 + 콘텐츠 정의 전체의 64비트 요약값(값 객체).
// 레이어: application (순수 C#)
// 관련: NFR-033, DES-104, DES-085(JoinHandshake) / SDD 3.7.7
// =============================================================
using System;
using System.Globalization;

namespace Game.Application.Config
{
    /// <summary>
    /// 접속할 때 호스트와 비교하는 요약값. 다르면 「게임 버전 불일치」로 참가를 거부한다.
    /// </summary>
    public readonly struct ContentHash : IEquatable<ContentHash>
    {
        /// <summary>64비트 요약값.</summary>
        public ulong Value { get; }

        /// <param name="value">요약값</param>
        public ContentHash(ulong value)
        {
            Value = value;
        }

        public bool Equals(ContentHash other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ContentHash other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();

        /// <summary>16자리 16진수 문자열(로그·보고용).</summary>
        public override string ToString() => Value.ToString("X16", CultureInfo.InvariantCulture);

        public static bool operator ==(ContentHash left, ContentHash right) => left.Equals(right);
        public static bool operator !=(ContentHash left, ContentHash right) => !left.Equals(right);
    }
}

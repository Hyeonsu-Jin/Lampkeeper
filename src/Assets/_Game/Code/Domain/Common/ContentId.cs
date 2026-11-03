// =============================================================
// 파일: ContentId.cs
// 역할: 콘텐츠 정의(아이템, 자원 종류 등) 하나를 가리키는 숫자 ID. 네트워크로 보내기 좋은 작은 값.
// 레이어: domain (Game.Domain, 순수 C#)
// 관련: NFR-033, NFR-034, DES-104 / SDD 3.7.7("로더가 키를 정렬해 ContentId를 붙인다")
// =============================================================
using System;

namespace Game.Domain.Common
{
    /// <summary>
    /// 콘텐츠 정의의 숫자 ID. 문자열 키(예: <c>item.wood_ash</c>)를 정렬한 순서로 1부터 붙는다.
    /// 같은 데이터면 어느 PC에서든 같은 번호가 나온다(접속 때 ContentHash로 확인).
    /// 0은 "없음"이다.
    /// </summary>
    public readonly struct ContentId : IEquatable<ContentId>, IComparable<ContentId>
    {
        /// <summary>"없음".</summary>
        public static readonly ContentId None = new ContentId(0);

        /// <summary>숫자 값(1부터).</summary>
        public int Value { get; }

        /// <summary>값이 0보다 크면 유효하다.</summary>
        public bool IsValid => Value > 0;

        /// <param name="value">0 이상의 값</param>
        /// <exception cref="ArgumentOutOfRangeException">음수일 때</exception>
        public ContentId(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "ContentId는 음수가 될 수 없다.");
            }
            Value = value;
        }

        public bool Equals(ContentId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ContentId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(ContentId other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString();

        public static bool operator ==(ContentId left, ContentId right) => left.Equals(right);
        public static bool operator !=(ContentId left, ContentId right) => !left.Equals(right);
    }
}

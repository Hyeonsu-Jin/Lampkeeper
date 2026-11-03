// =============================================================
// 파일: PlayerId.cs
// 역할: 플레이어를 가리키는 값 객체. Steam 계정의 SteamID(64비트)를 그대로 담는다.
// 레이어: domain (Game.Domain, 순수 C#)
// 관련: FR-015(진행은 SteamID 기준), NFR-006, DES-014 / SDD 3.8.2 Domain/Common
// =============================================================
using System;

namespace Game.Domain.Common
{
    /// <summary>
    /// 플레이어 식별자. 값 0은 "없음"으로 취급한다.
    /// [FR-015] 모든 플레이어 진행 데이터는 이 ID를 열쇠로 저장한다.
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>, IComparable<PlayerId>
    {
        /// <summary>"없음"을 뜻하는 값(Steam에서도 0은 유효하지 않은 ID).</summary>
        public static readonly PlayerId None = new PlayerId(0UL);

        /// <summary>원본 64비트 값.</summary>
        public ulong Value { get; }

        /// <summary>값이 0이 아니면 유효한 ID다.</summary>
        public bool IsValid => Value != 0UL;

        /// <param name="value">SteamID 또는 개발용 ID 값</param>
        public PlayerId(ulong value)
        {
            Value = value;
        }

        public bool Equals(PlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(PlayerId other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString();

        public static bool operator ==(PlayerId left, PlayerId right) => left.Equals(right);
        public static bool operator !=(PlayerId left, PlayerId right) => !left.Equals(right);
    }
}

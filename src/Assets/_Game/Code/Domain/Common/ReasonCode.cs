// =============================================================
// 파일: ReasonCode.cs
// 역할: 호스트가 요청을 거부한 이유(거부 사유 코드)와 부가 값을 담는 값 객체, 위치용 Vec3f.
// 레이어: domain (Game.Domain, 순수 C#)
// 관련: NFR-003(거부 사유 표시), FR-007(호스트 검증), DES-001 / SDD 3.3.3, 3.7.5
//
// 화면 문장은 이 코드를 텍스트 키로 바꿔 받는 쪽 언어로 만든다. 도메인은 글자를 모른다.
// =============================================================
using System;

namespace Game.Domain.Common
{
    /// <summary>
    /// 거부 사유 코드. 숫자 값은 네트워크로 전송되므로 한 번 정하면 바꾸거나 재사용하지 않는다.
    /// M1에서 쓰는 것만 정의하고, 나머지(3.7.5 표)는 해당 마일스톤에서 추가한다.
    /// </summary>
    public enum ReasonCode : byte
    {
        /// <summary>사유 없음(성공).</summary>
        None = 0,

        /// <summary>대상이 상호작용 거리 밖이다. [FR-007]</summary>
        OutOfRange = 1,

        /// <summary>대상이 존재하지 않는다. [FR-007]</summary>
        TargetMissing = 2,

        /// <summary>요청이 너무 잦다. 화면에 표시하지 않고 로그만 남긴다. [NFR-041]</summary>
        RateLimited = 3,

        /// <summary>값이 허용 범위 밖이다(좌표가 월드 밖 등). [NFR-040]</summary>
        InvalidValue = 4,

        /// <summary>알 수 없는 명령 종류. [NFR-040]</summary>
        UnknownCommand = 5,

        /// <summary>호스트 내부 오류로 처리하지 못했다(원인은 호스트 로그).</summary>
        InternalError = 6,

        /// <summary>지금은 할 수 없는 상태다.</summary>
        NotAllowedNow = 7,
    }

    /// <summary>거부 사유 + 부가 값(예: 부족한 재료 ID와 수량). 값이 필요 없으면 0.</summary>
    public readonly struct Rejection : IEquatable<Rejection>
    {
        /// <summary>사유 코드.</summary>
        public ReasonCode Reason { get; }

        /// <summary>부가 값 1(사유마다 뜻이 다름).</summary>
        public int ArgA { get; }

        /// <summary>부가 값 2.</summary>
        public int ArgB { get; }

        public Rejection(ReasonCode reason, int argA = 0, int argB = 0)
        {
            Reason = reason;
            ArgA = argA;
            ArgB = argB;
        }

        public bool Equals(Rejection other) => Reason == other.Reason && ArgA == other.ArgA && ArgB == other.ArgB;
        public override bool Equals(object obj) => obj is Rejection other && Equals(other);
        public override int GetHashCode() => ((int)Reason * 397 ^ ArgA) * 397 ^ ArgB;
        public override string ToString() => Reason + (ArgA != 0 || ArgB != 0 ? "(" + ArgA + "," + ArgB + ")" : string.Empty);
    }

    /// <summary>float 3개 묶음. 도메인이 UnityEngine을 모르므로 위치·속도를 이 값으로 다룬다.</summary>
    public readonly struct Vec3f : IEquatable<Vec3f>
    {
        public static readonly Vec3f Zero = new Vec3f(0f, 0f, 0f);

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public Vec3f(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>X·Z 평면(수평) 거리. 높이는 무시한다.</summary>
        public static float HorizontalDistance(Vec3f a, Vec3f b)
        {
            float dx = a.X - b.X;
            float dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>세 값이 모두 유한한 수인지(NaN·무한대가 아닌지).</summary>
        public bool IsFinite =>
            !(float.IsNaN(X) || float.IsInfinity(X) || float.IsNaN(Y) || float.IsInfinity(Y) || float.IsNaN(Z) || float.IsInfinity(Z));

        public bool Equals(Vec3f other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is Vec3f other && Equals(other);
        public override int GetHashCode() => (X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();
        public override string ToString() => "(" + X + ", " + Y + ", " + Z + ")";
    }
}

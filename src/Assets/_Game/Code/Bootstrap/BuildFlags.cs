// =============================================================
// 파일: BuildFlags.cs
// 역할: "지금 개발용 코드를 켜도 되는 빌드인가"를 한 곳에서 판단한다.
// 레이어: infrastructure (Game.Bootstrap, 컴포지션 루트)
// 관련: FR-006, NFR-006, NFR-009, DES-112 / SDD M1-01 (5) 개발/출시 정의 기호
//
// 개발 빌드 = Unity 에디터 또는 Development Build(DEVELOPMENT_BUILD 기호). 출시 빌드는 둘 다 아니다.
// 개발용 옵션(-transport, -devPlayerId, ...)과 Steam 없이 시작하는 예외는 개발 빌드에서만 켜진다.
// =============================================================
namespace Game.Bootstrap
{
    /// <summary>빌드 종류 판별. 컴파일 시점 상수.</summary>
    public static class BuildFlags
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>개발 빌드(에디터 포함)이면 true.</summary>
        public const bool IsDevelopment = true;
#else
        /// <summary>개발 빌드(에디터 포함)이면 true.</summary>
        public const bool IsDevelopment = false;
#endif
    }
}

// =============================================================
// 파일: WorldGenAssembly.cs
// 역할: Game.WorldGen 어셈블리 자리 표시. 실제 코드(DetMath, 생성기 V0)는 M1-04에서 추가한다.
// 레이어: domain(생성 규칙, 순수 C#)
// 관련: DES-040 / SDD 3.4.2, 3.8.2
// =============================================================
namespace Game.WorldGen
{
    /// <summary>어셈블리를 컴파일 대상으로 만들고, 아키텍처 시험이 이름으로 찾을 수 있게 하는 표식.</summary>
    public static class WorldGenAssembly
    {
        /// <summary>어셈블리 이름.</summary>
        public const string Name = "Game.WorldGen";
    }
}

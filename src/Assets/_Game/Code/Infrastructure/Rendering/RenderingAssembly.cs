// =============================================================
// 파일: RenderingAssembly.cs
// 역할: Game.Infra.Rendering 어셈블리 자리 표시. 실제 코드는 해당 마일스톤 작업에서 추가한다.
// 레이어: 인프라(HDRP 의존 코드 전부)
// 관련: DES-114 / SDD 3.4.2, 3.8.2
// =============================================================
namespace Game.Infra.Rendering
{
    /// <summary>어셈블리를 컴파일 대상으로 만들고, 아키텍처 시험이 이름으로 찾을 수 있게 하는 표식.</summary>
    public static class RenderingAssembly
    {
        /// <summary>어셈블리 이름.</summary>
        public const string Name = "Game.Infra.Rendering";
    }
}

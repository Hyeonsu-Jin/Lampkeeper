// =============================================================
// 파일: PersistenceAssembly.cs
// 역할: Game.Adapters.Persistence 어셈블리 자리 표시. 실제 코드는 해당 마일스톤 작업에서 추가한다.
// 레이어: 어댑터(월드 파일 코덱·저장소, 순수 C#)
// 관련: DES-098 / SDD 3.4.2, 3.8.2
// =============================================================
namespace Game.Adapters.Persistence
{
    /// <summary>어셈블리를 컴파일 대상으로 만들고, 아키텍처 시험이 이름으로 찾을 수 있게 하는 표식.</summary>
    public static class PersistenceAssembly
    {
        /// <summary>어셈블리 이름.</summary>
        public const string Name = "Game.Adapters.Persistence";
    }
}

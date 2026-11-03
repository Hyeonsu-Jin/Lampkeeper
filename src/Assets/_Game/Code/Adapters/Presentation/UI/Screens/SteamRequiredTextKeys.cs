// =============================================================
// 파일: SteamRequiredTextKeys.cs
// 역할: SCR-001(Steam 실행 필요 안내)에 쓰는 텍스트 키 상수.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: NFR-006, NFR-034 / STORYBOARD SCR-001 (스크린리더 라벨: steam.required.title/desc/retry/quit)
// =============================================================
namespace Game.Adapters.Presentation.UI.Screens
{
    /// <summary>SCR-001 텍스트 키. 한국어·영어 글자는 Assets/_Game/Localization/ 표에 있다.</summary>
    public static class SteamRequiredTextKeys
    {
        /// <summary>① 제목</summary>
        public const string Title = "steam.required.title";
        /// <summary>② 설명</summary>
        public const string Description = "steam.required.desc";
        /// <summary>③ [다시 확인]</summary>
        public const string Retry = "steam.required.retry";
        /// <summary>⑤ [종료]</summary>
        public const string Quit = "steam.required.quit";
        /// <summary>재확인 중 표시(③ 로딩)</summary>
        public const string Checking = "steam.required.checking";
        /// <summary>재확인 실패 문구(② 아래)</summary>
        public const string RetryFailed = "steam.required.retry_failed";

        /// <summary>이 화면이 쓰는 모든 키(누락 검사용).</summary>
        public static readonly string[] All = { Title, Description, Retry, Quit, Checking, RetryFailed };
    }
}

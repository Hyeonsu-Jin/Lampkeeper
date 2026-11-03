// =============================================================
// 파일: IPlatformServices.cs
// 역할: 플랫폼(Steam) 시작·종료·매 프레임 처리를 위한 포트와 초기화 결과 타입.
// 레이어: application (포트). 구현: Infra.Steam(SteamPlatformService)
// 관련: NFR-006(Steam 실행 필수), DES-102, DES-100 / SDD 3.7.1, 3.7.3
// =============================================================
using System;

namespace Game.Application.Ports
{
    /// <summary>플랫폼 초기화가 실패한 이유. 화면 문구 선택과 로그에 쓴다.</summary>
    public enum PlatformInitFailure
    {
        /// <summary>실패 없음(성공).</summary>
        None = 0,

        /// <summary>Steam 클라이언트가 실행 중이 아니다.</summary>
        SteamNotRunning = 1,

        /// <summary>Steam은 있지만 로그인되지 않았거나 초기화 호출이 실패했다.</summary>
        InitFailed = 2,

        /// <summary>Steamworks 네이티브 라이브러리를 찾을 수 없거나 이 플랫폼에서 지원하지 않는다.</summary>
        LibraryUnavailable = 3,

        /// <summary>Steam이 게임을 다시 실행하도록 요청했다(출시 빌드에서 Steam 밖으로 실행한 경우).</summary>
        RestartRequested = 4,
    }

    /// <summary>플랫폼 초기화 결과.</summary>
    public readonly struct PlatformInitResult
    {
        /// <summary>성공 여부.</summary>
        public bool Success { get; }

        /// <summary>실패 이유(성공이면 None).</summary>
        public PlatformInitFailure Failure { get; }

        /// <summary>로그용 상세 설명(화면에 그대로 보이지 않는다).</summary>
        public string Detail { get; }

        private PlatformInitResult(bool success, PlatformInitFailure failure, string detail)
        {
            Success = success;
            Failure = failure;
            Detail = detail ?? string.Empty;
        }

        /// <summary>성공 결과를 만든다.</summary>
        public static PlatformInitResult Ok() => new PlatformInitResult(true, PlatformInitFailure.None, string.Empty);

        /// <summary>실패 결과를 만든다.</summary>
        /// <param name="failure">실패 이유</param>
        /// <param name="detail">로그용 설명</param>
        public static PlatformInitResult Fail(PlatformInitFailure failure, string detail) =>
            new PlatformInitResult(false, failure, detail);
    }

    /// <summary>
    /// 플랫폼 서비스 생명 주기. 부트에서 <see cref="Initialize"/>를 호출하고,
    /// 성공한 뒤에는 매 프레임 <see cref="Tick"/>, 종료할 때 <see cref="Shutdown"/>을 부른다.
    /// </summary>
    public interface IPlatformServices
    {
        /// <summary>초기화에 성공했는지.</summary>
        bool IsInitialized { get; }

        /// <summary>초기화에 성공한 뒤의 로컬 플레이어 정보. 실패 상태에서는 null.</summary>
        IPlatformIdentity Identity { get; }

        /// <summary>
        /// 플랫폼을 초기화한다. 실패한 뒤 다시 불러도 된다(Steam을 늦게 켠 경우의 재확인).
        /// 예외를 밖으로 던지지 않고 결과로 알린다.
        /// </summary>
        PlatformInitResult Initialize();

        /// <summary>매 프레임 불러서 플랫폼 콜백을 처리한다. 초기화 전에는 아무 일도 하지 않는다.</summary>
        void Tick();

        /// <summary>플랫폼을 끝낸다. 여러 번 불러도 안전하다.</summary>
        void Shutdown();
    }
}

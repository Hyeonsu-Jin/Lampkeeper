// =============================================================
// 파일: CheckPlatformReady.cs
// 역할: 부트에서 "Steam을 쓸 수 있는가"를 확인하고 다음 길(메인 메뉴 / SCR-001 안내 화면)을 정하는 유스케이스.
//       함께 TransportPolicy(전송 선택 규칙)와 DevPlayerIdentity(개발용 플레이어)를 둔다.
// 레이어: application (순수 C#)
// 관련: NFR-006(Steam 실행 필수), FR-006, NFR-031, DES-100, DES-102, DES-103 / SDD 3.7.4, STORYBOARD SCR-001
//
// 규칙 요약
//  - 오프라인 플레이는 없다. Steam 초기화에 실패하면 메인 메뉴로 못 들어가고 SCR-001에서 멈춘다.
//  - 예외는 하나뿐: 개발 빌드에서 -transport tugboat 와 -devPlayerId 를 함께 준 경우(Steam 없이 같은 PC 시험).
//  - 출시 빌드는 개발 옵션을 모두 무시하고 Steam으로 고정한다.
// =============================================================
using System;
using Game.Application.Ports;
using Game.Domain.Common;

namespace Game.Application.Boot
{
    /// <summary>부트 다음 길.</summary>
    public enum BootRoute
    {
        /// <summary>준비 완료. 다음 단계(첫 실행 확인, 메인 메뉴)로 간다.</summary>
        ContinueToMainMenu = 0,

        /// <summary>Steam을 쓸 수 없다. SCR-001 안내 화면에서 멈춘다.</summary>
        ShowSteamRequired = 1,
    }

    /// <summary>부트 판정 결과.</summary>
    public sealed class BootDecision
    {
        /// <summary>다음 길.</summary>
        public BootRoute Route { get; }

        /// <summary>최종 전송 방식(출시 빌드는 항상 Steam).</summary>
        public TransportKind Transport { get; }

        /// <summary>로컬 플레이어 정보. ShowSteamRequired이면 null.</summary>
        public IPlatformIdentity Identity { get; }

        /// <summary>Steam 없이 개발용 ID로 들어왔으면 true.</summary>
        public bool UsesDevIdentity { get; }

        /// <summary>안내 화면을 보일 때의 실패 이유(성공이면 None).</summary>
        public PlatformInitFailure Failure { get; }

        /// <summary>로그용 상세 설명.</summary>
        public string Detail { get; }

        public BootDecision(BootRoute route, TransportKind transport, IPlatformIdentity identity, bool usesDevIdentity, PlatformInitFailure failure, string detail)
        {
            Route = route;
            Transport = transport;
            Identity = identity;
            UsesDevIdentity = usesDevIdentity;
            Failure = failure;
            Detail = detail ?? string.Empty;
        }
    }

    /// <summary>전송 선택 규칙 [FR-006].</summary>
    public static class TransportPolicy
    {
        /// <summary>
        /// 요청한 전송을 빌드 종류에 맞게 확정한다.
        /// 출시 빌드는 요청과 관계없이 Steam, 개발 빌드는 요청대로(없으면 Steam).
        /// </summary>
        public static TransportKind Resolve(TransportKind? requested, bool isDevelopmentBuild)
        {
            if (!isDevelopmentBuild)
            {
                return TransportKind.Steam;
            }
            return requested ?? TransportKind.Steam;
        }

        /// <summary>
        /// Steam 없이 개발용 ID로 시작해도 되는지. 개발 빌드이고 Tugboat + devPlayerId가 모두 있을 때만 true.
        /// </summary>
        public static bool AllowsSteamlessStart(LaunchOptions options, bool isDevelopmentBuild)
        {
            return isDevelopmentBuild
                   && options != null
                   && options.RequestedTransport == TransportKind.Tugboat
                   && options.DevPlayerId.HasValue;
        }
    }

    /// <summary>
    /// 개발용 플레이어 신원. -devPlayerId 값을 ID로 쓰고, 이름은 "Dev-번호"로 한다.
    /// 같은 PC에서 같은 Steam 계정으로 여러 빌드를 띄워 직접 전송을 시험할 때 쓴다(개발 빌드 전용).
    /// </summary>
    public sealed class DevPlayerIdentity : IPlatformIdentity
    {
        public PlayerId LocalPlayerId { get; }
        public string LocalDisplayName { get; }

        /// <param name="devPlayerId">0이 아닌 개발용 ID</param>
        /// <exception cref="ArgumentOutOfRangeException">0일 때</exception>
        public DevPlayerIdentity(ulong devPlayerId)
        {
            if (devPlayerId == 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(devPlayerId), "개발용 ID는 0이 될 수 없다.");
            }
            LocalPlayerId = new PlayerId(devPlayerId);
            LocalDisplayName = "Dev-" + devPlayerId;
        }

        public string ResolveName(PlayerId playerId) => "Dev-" + playerId.Value;
    }

    /// <summary>
    /// 유스케이스: 플랫폼 준비 확인. 부트 때와 SCR-001의 [다시 확인]에서 같은 방식으로 부른다.
    /// </summary>
    public sealed class CheckPlatformReady
    {
        private readonly IPlatformServices _platform;

        /// <param name="platform">플랫폼 서비스(Steam)</param>
        /// <exception cref="ArgumentNullException">platform이 null일 때</exception>
        public CheckPlatformReady(IPlatformServices platform)
        {
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
        }

        /// <summary>
        /// 다음 길을 정한다.
        /// </summary>
        /// <param name="options">명령줄 옵션(null이면 없는 것으로 본다)</param>
        /// <param name="isDevelopmentBuild">개발 빌드(또는 에디터)인지</param>
        public BootDecision Execute(LaunchOptions options, bool isDevelopmentBuild)
        {
            options = options ?? LaunchOptions.None;

            // 출시 빌드는 개발 옵션을 무시하고 Steam으로 고정한다 [FR-006].
            TransportKind transport = TransportPolicy.Resolve(options.RequestedTransport, isDevelopmentBuild);

            // [NFR-006] 예외 경로: 개발 빌드의 Tugboat + devPlayerId 는 Steam 초기화를 건너뛴다.
            if (TransportPolicy.AllowsSteamlessStart(options, isDevelopmentBuild))
            {
                var devIdentity = new DevPlayerIdentity(options.DevPlayerId.Value);
                return new BootDecision(BootRoute.ContinueToMainMenu, transport, devIdentity, true, PlatformInitFailure.None, "개발용 신원(Steam 건너뜀)");
            }

            // [NFR-006] 그 밖에는 Steam이 반드시 있어야 한다. 실패하면 오프라인 대안 없이 안내 화면으로 간다.
            PlatformInitResult result = _platform.Initialize();
            if (!result.Success)
            {
                return new BootDecision(BootRoute.ShowSteamRequired, transport, null, false, result.Failure, result.Detail);
            }

            return new BootDecision(BootRoute.ContinueToMainMenu, transport, _platform.Identity, false, PlatformInitFailure.None, result.Detail);
        }
    }
}

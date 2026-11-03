// =============================================================
// 파일: IPlatformIdentity.cs
// 역할: "지금 이 PC의 플레이어가 누구인가"를 알려 주는 포트(인터페이스).
// 레이어: application (포트). 구현: Infra.Steam(SteamIdentity), Application.Boot(DevPlayerIdentity)
// 관련: NFR-006, FR-015, DES-051, DES-102 / SDD 3.7.3
// =============================================================
using Game.Domain.Common;

namespace Game.Application.Ports
{
    /// <summary>
    /// 로컬 플레이어의 식별 정보. 전송 방식(Steam/개발용)에 관계없이 같은 모양으로 쓴다.
    /// </summary>
    public interface IPlatformIdentity
    {
        /// <summary>이 PC 플레이어의 ID(Steam이면 SteamID).</summary>
        PlayerId LocalPlayerId { get; }

        /// <summary>화면에 보일 이름(Steam이면 프로필 이름).</summary>
        string LocalDisplayName { get; }

        /// <summary>
        /// 다른 플레이어의 표시 이름을 찾는다.
        /// </summary>
        /// <param name="playerId">찾을 플레이어</param>
        /// <returns>이름. 알 수 없으면 ID 문자열</returns>
        string ResolveName(PlayerId playerId);
    }
}

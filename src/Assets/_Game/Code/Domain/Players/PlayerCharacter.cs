// =============================================================
// 파일: PlayerCharacter.cs
// 역할: 플레이어 진행 집합체(M1 범위: ID, 이름, 위치, 시선, 스태미나).
// 레이어: domain (Game.Domain, 순수 C#)
// 관련: FR-015, FR-023~025, NFR-030, DES-002 / SDD 3.3.1
//
// 체력·허기·인벤토리 등은 해당 마일스톤(M2~)에서 이 클래스에 추가한다.
// 이동 검사 규칙(AvatarMoveValidator)은 애플리케이션 레이어에 있다(DES-052, SDD 3.11.12).
// =============================================================
using Game.Domain.Common;
using Game.Domain.Vitals;

namespace Game.Domain.Players
{
    /// <summary>플레이어 한 명의 서버 쪽 상태. 호스트(서버 역할)만 바꾼다.</summary>
    public sealed class PlayerCharacter
    {
        /// <param name="id">플레이어 ID(SteamID)</param>
        /// <param name="displayName">표시 이름</param>
        /// <param name="position">스폰 위치</param>
        /// <param name="stamina">스태미나 규칙 설정값</param>
        public PlayerCharacter(PlayerId id, string displayName, Vec3f position, StaminaParams stamina)
        {
            Id = id;
            DisplayName = displayName ?? string.Empty;
            Position = position;
            Stamina = new StaminaState(stamina);
        }

        /// <summary>플레이어 ID(SteamID). [FR-015]</summary>
        public PlayerId Id { get; }

        /// <summary>표시 이름.</summary>
        public string DisplayName { get; }

        /// <summary>호스트가 마지막으로 받아들인 위치. 거리 검증(FR-007)은 이 값을 쓴다.</summary>
        public Vec3f Position { get; set; }

        /// <summary>시선 좌우 각도(도).</summary>
        public float YawDegrees { get; set; }

        /// <summary>시선 상하 각도(도).</summary>
        public float PitchDegrees { get; set; }

        /// <summary>호스트가 추정하는 스태미나(이동 검사용).</summary>
        public StaminaState Stamina { get; }
    }
}

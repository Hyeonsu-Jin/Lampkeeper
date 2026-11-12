// =============================================================
// 파일: GameDataConfig.cs
// 역할: 설정 뿌리 ScriptableObject. 부트가 이 애셋 하나만 참조하면 모든 설정을 읽는다. 위치: Assets/_Game/Data/GameDataConfig.asset
// 레이어: infrastructure (Game.Infra.Config)
// 관련: NFR-033(설정값 데이터 분리), NFR-034, DES-104 / SDD 3.7.7, 3.8.2 Data/, 3.8.3 이름 규칙(…Def / …Config)
// =============================================================
using System.Collections.Generic;
using UnityEngine;

namespace Game.Infra.Config
{
    /// <summary>
    /// 설정 뿌리 애셋. 콘텐츠 목록은 편집기 메뉴(Game/Data/Refresh Config Index)가 채운다.
    /// </summary>
    [CreateAssetMenu(fileName = "GameDataConfig", menuName = "Game/Config/Game Data Config")]
    public sealed class GameDataConfig : ScriptableObject
    {
        public BalanceConfig Balance;
        public NetworkTuningConfig Network;

        /// <summary>그래픽 프리셋 4종(FR-120). 비어 있으면 부트가 경고하고 프리셋 적용을 건너뛴다.</summary>
        public GraphicsPresetConfig Graphics;
        public List<ContentDef> Contents = new List<ContentDef>();
    }
}

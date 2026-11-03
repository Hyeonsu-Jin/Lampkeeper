// =============================================================
// 파일: BalanceConfig.cs
// 역할: 균형 설정값 ScriptableObject. 초기값은 SRS 부록 C.1 후보값. 위치: Assets/_Game/Data/Balance/
// 레이어: infrastructure (Game.Infra.Config)
// 관련: NFR-033(설정값 데이터 분리), NFR-034, DES-104 / SDD 3.7.7, 3.8.2 Data/, 3.8.3 이름 규칙(…Def / …Config)
// =============================================================
using Game.Application.Config;
using UnityEngine;

namespace Game.Infra.Config
{
    /// <summary>균형 설정값 애셋. 값 구조는 순수 C# 클래스(BalanceValues)에 있다.</summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "Game/Config/Balance Config")]
    public sealed class BalanceConfig : ScriptableObject
    {
        /// <summary>설정값 묶음.</summary>
        public BalanceValues Values = BalanceValues.CreateCandidate();
    }
}

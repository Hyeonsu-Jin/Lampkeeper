// =============================================================
// 파일: NetworkTuningConfig.cs
// 역할: 네트워크 튜닝 ScriptableObject. 위치: Assets/_Game/Data/Network/
// 레이어: infrastructure (Game.Infra.Config)
// 관련: NFR-033(설정값 데이터 분리), NFR-034, DES-104 / SDD 3.7.7, 3.8.2 Data/, 3.8.3 이름 규칙(…Def / …Config)
// =============================================================
using Game.Application.Config;
using UnityEngine;

namespace Game.Infra.Config
{
    /// <summary>네트워크 튜닝 애셋(틱·AI 주기·관심 범위 등).</summary>
    [CreateAssetMenu(fileName = "NetworkTuningConfig", menuName = "Game/Config/Network Tuning Config")]
    public sealed class NetworkTuningConfig : ScriptableObject
    {
        /// <summary>튜닝 값 묶음.</summary>
        public NetworkTuning Values = new NetworkTuning();
    }
}

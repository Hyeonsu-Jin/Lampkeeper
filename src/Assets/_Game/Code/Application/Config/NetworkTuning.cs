// =============================================================
// 파일: NetworkTuning.cs
// 역할: 서버 틱·AI 주기·복제 주기·관심 범위 같은 네트워크 튜닝 값(SDD 3.12.1).
// 레이어: application (순수 C#). 호스트 PC 로컬 튜닝이라 ContentHash에 넣지 않는다.
// 관련: FR-008(관심 범위), NFR-013~016, NFR-011, DES-101, DES-104 / SDD 3.12.1
// =============================================================
using System;

namespace Game.Application.Config
{
    /// <summary>
    /// 네트워크·시뮬레이션 주기 튜닝. 초기값은 SDD 3.12.1 표의 기본값과 SRS 부록 C.1 후보값이다.
    /// </summary>
    [Serializable]
    public sealed class NetworkTuning
    {
        /// <summary>서버 틱(Hz). FishNet TimeManager.TickRate에 적용</summary>
        public int ServerTickRateHz = 30;
        /// <summary>AI 판단 주기(Hz). 틱 3분할로 분산</summary>
        public int AiDecisionHz = 10;
        /// <summary>AI 이동(조향) 주기(Hz)</summary>
        public int AiSteeringHz = 30;
        /// <summary>AI LOD가 낮을 때 이동 주기(Hz)</summary>
        public int AiSteeringLodHz = 10;
        /// <summary>밤 습격 중 흐름장 재계산 주기(Hz)</summary>
        public int FlowFieldRaidHz = 1;
        /// <summary>플레이어 아바타 전송 주기(Hz)</summary>
        public int AvatarSendHz = 30;
        /// <summary>생물 스냅샷 주기: 가까운 등급(Hz)</summary>
        public int CreatureSnapshotNearHz = 30;
        /// <summary>생물 스냅샷 주기: 중간 등급(Hz)</summary>
        public int CreatureSnapshotMidHz = 10;
        /// <summary>생물 스냅샷 주기: 먼 등급(Hz)</summary>
        public int CreatureSnapshotFarHz = 2;
        /// <summary>시각 동기화 주기(초). 변경 시에도 즉시 보낸다</summary>
        public float ClockSyncSeconds = 5f;
        /// <summary>관심 범위(m). 이 거리 안의 변화만 보낸다 [FR-008]</summary>
        public float InterestRangeMeters = 300f;
        /// <summary>참가 절차 시간 제한(초): 연결 뒤 JoinRequest, 요청 뒤 JoinComplete까지 이 안에 끝나지 않으면 끊는다 [NFR-039]</summary>
        public float JoinTimeoutSeconds = 15f;
        /// <summary>한 틱에 처리하는 명령 수 상한(서버 처리 예산 보호) [NFR-041]</summary>
        public int MaxCommandsPerTick = 64;
        /// <summary>처리 대기 명령 수 상한(넘으면 버림) [NFR-041]</summary>
        public int MaxQueuedCommands = 256;
        /// <summary>새 참가자 한 명에게 보내는 스냅샷 속도 상한(바이트/초). 약 1.5 Mbps [3.9.2]</summary>
        public int SnapshotBytesPerSecond = 187500;

        /// <summary>값을 복사한 새 객체.</summary>
        public NetworkTuning Clone() => (NetworkTuning)MemberwiseClone();
    }
}

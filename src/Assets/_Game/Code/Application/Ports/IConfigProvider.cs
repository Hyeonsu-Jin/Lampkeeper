// =============================================================
// 파일: IConfigProvider.cs
// 역할: 설정값·콘텐츠 정의를 읽기 전용으로 내주는 포트.
// 레이어: application (포트). 구현: Application.Config.ConfigProvider (로더는 Infra.Config)
// 관련: NFR-033(설정값 데이터 분리), NFR-034, DES-051, DES-104 / SDD 3.7.3, 3.7.7
// =============================================================
using Game.Application.Config;

namespace Game.Application.Ports
{
    /// <summary>
    /// 로드·검증이 끝난 설정을 읽는 창구. 돌려주는 객체는 읽기 전용으로 쓴다(수정 금지).
    /// 도메인 규칙과 유스케이스는 숫자를 코드에 쓰지 않고 이 창구에서 받는다.
    /// </summary>
    public interface IConfigProvider
    {
        /// <summary>게임 균형 설정값(SRS 부록 C.1).</summary>
        BalanceValues Balance { get; }

        /// <summary>네트워크 주기·튜닝 값(SDD 3.12.1). 호스트 PC 로컬 값이라 ContentHash에 넣지 않는다.</summary>
        NetworkTuning Network { get; }

        /// <summary>콘텐츠 정의 목록(키 ↔ ContentId).</summary>
        ContentCatalog Content { get; }

        /// <summary>
        /// 균형 설정값 + 콘텐츠 정의의 요약값. 접속할 때 호스트와 비교해 다르면 참가를 거부한다.
        /// </summary>
        ContentHash ContentHash { get; }
    }
}

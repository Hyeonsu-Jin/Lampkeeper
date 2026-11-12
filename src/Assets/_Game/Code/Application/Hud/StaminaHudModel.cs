// =============================================================
// 파일: StaminaHudModel.cs
// 역할: 스태미나 호(십자선 둘레 얇은 호)를 보일지, 어떻게 보일지 정하는 표시용 값 계산(HUD 뷰모델의 스태미나 부분).
// 레이어: application (Game.Application/Hud, 순수 C#)
// 관련: NFR-001(값이 바뀌면 0.5초 안에 갱신), FR-025, DES-074 / SDD 3.7.11, STORYBOARD SCR-015 ⑱
//
// STORYBOARD ⑱: 소모·회복 중에만 보이고, 가득 차면 1초에 걸쳐 사라진다. 25% 이하는 점선 패턴 + 경고 글자(색에만 의존하지 않음).
// 화면은 이 값을 읽어 그리기만 한다.
// =============================================================
using System;

namespace Game.Application.Hud
{
    /// <summary>스태미나 호 표시 값.</summary>
    public readonly struct StaminaHudState
    {
        /// <summary>채워진 비율(0~1).</summary>
        public float Ratio { get; }
        /// <summary>불투명도(0~1). 0이면 그리지 않는다.</summary>
        public float Opacity { get; }
        /// <summary>25% 이하: 점선 패턴 + 경고 기호로 표시한다.</summary>
        public bool Low { get; }

        public StaminaHudState(float ratio, float opacity, bool low)
        {
            Ratio = ratio;
            Opacity = opacity;
            Low = low;
        }

        public bool Visible => Opacity > 0f;
    }

    /// <summary>스태미나 호 표시 계산기. 시간은 호출자가 넘긴 경과 시간으로만 흐른다(시험 쉬움).</summary>
    public sealed class StaminaHudModel
    {
        /// <summary>가득 찬 뒤 사라지는 데 걸리는 시간(초). STORYBOARD ⑱ 「1초에 걸쳐」</summary>
        public const float FadeSeconds = 1f;

        /// <summary>낮은 값 기준 비율(25%). STORYBOARD 2.5 게이지 오류 상태.</summary>
        public const float LowThreshold = 0.25f;

        private float _opacity;

        /// <summary>
        /// 값을 갱신하고 이번 프레임 표시 상태를 돌려준다.
        /// </summary>
        /// <param name="ratio">현재 스태미나 비율(0~1)</param>
        /// <param name="isFull">가득 찼는지</param>
        /// <param name="deltaSeconds">경과 시간(비스케일 시간)</param>
        public StaminaHudState Update(float ratio, bool isFull, float deltaSeconds)
        {
            ratio = Math.Max(0f, Math.Min(1f, ratio));
            if (!isFull)
            {
                _opacity = 1f; // 소모·회복 중에는 바로 보인다
            }
            else
            {
                _opacity = Math.Max(0f, _opacity - Math.Max(0f, deltaSeconds) / FadeSeconds);
            }
            return new StaminaHudState(ratio, _opacity, ratio <= LowThreshold);
        }
    }
}

// =============================================================
// 파일: StaminaArcElement.cs
// 역할: 십자선 둘레의 스태미나 호를 그리는 UI Toolkit 요소(STORYBOARD SCR-015 ⑱). 채워진 비율만큼 호를 그리고,
//       25% 이하이면 점선 패턴으로 바꾼다(색에만 의존하지 않음, 모양 구분 규칙).
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-025, NFR-001, NFR-056, DES-091, DES-120 / SDD 3.7.10, 3.7.11
//
// 색은 USS 토큰(--color-stamina, --color-track, --color-warning)에서 읽는다. 코드에 색 값을 쓰지 않는다.
// =============================================================
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Adapters.Presentation.UI.Hud
{
    /// <summary>스태미나 호 요소.</summary>
    public sealed class StaminaArcElement : VisualElement
    {
        private static readonly CustomStyleProperty<Color> FillColorProperty = new CustomStyleProperty<Color>("--color-stamina");
        private static readonly CustomStyleProperty<Color> TrackColorProperty = new CustomStyleProperty<Color>("--color-track");
        private static readonly CustomStyleProperty<Color> LowColorProperty = new CustomStyleProperty<Color>("--color-warning");

        /// <summary>호 두께(px, 1080p 기준). 얇은 호.</summary>
        private const float LineWidth = 6f;

        /// <summary>점선 호를 몇 조각으로 나눌지.</summary>
        private const int DashCount = 24;

        /// <summary>시작 각도(위쪽 12시 방향, 도).</summary>
        private const float StartAngleDegrees = -90f;

        private const float FullCircleDegrees = 360f;

        private Color _fill = Color.clear;
        private Color _track = Color.clear;
        private Color _low = Color.clear;
        private float _ratio = 1f;
        private bool _low_;

        public StaminaArcElement()
        {
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
        }

        /// <summary>표시 값을 바꾼다. 바뀔 때만 다시 그린다.</summary>
        public void SetState(float ratio, bool low)
        {
            ratio = Mathf.Clamp01(ratio);
            if (Mathf.Approximately(ratio, _ratio) && low == _low_)
            {
                return;
            }
            _ratio = ratio;
            _low_ = low;
            MarkDirtyRepaint();
        }

        private void OnStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (customStyle.TryGetValue(FillColorProperty, out Color fill)) { _fill = fill; }
            if (customStyle.TryGetValue(TrackColorProperty, out Color track)) { _track = track; }
            if (customStyle.TryGetValue(LowColorProperty, out Color low)) { _low = low; }
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }
            Painter2D painter = context.painter2D;
            var center = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f - LineWidth;
            painter.lineWidth = LineWidth;
            painter.lineCap = LineCap.Butt;

            // 빈 부분(트랙): 전체 원
            painter.strokeColor = _track;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, FullCircleDegrees);
            painter.Stroke();

            if (_ratio <= 0f)
            {
                return;
            }

            if (_low_)
            {
                // 25% 이하: 점선 호(조각 사이에 틈). 색만이 아니라 모양으로도 낮음을 알린다.
                painter.strokeColor = _low;
                float sweep = FullCircleDegrees * _ratio;
                float step = FullCircleDegrees / DashCount;
                for (float a = 0f; a < sweep; a += step)
                {
                    float end = Mathf.Min(a + step * 0.5f, sweep);
                    painter.BeginPath();
                    painter.Arc(center, radius, StartAngleDegrees + a, StartAngleDegrees + end);
                    painter.Stroke();
                }
                return;
            }

            painter.strokeColor = _fill;
            painter.BeginPath();
            painter.Arc(center, radius, StartAngleDegrees, StartAngleDegrees + FullCircleDegrees * _ratio);
            painter.Stroke();
        }
    }
}

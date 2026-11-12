// =============================================================
// 파일: UiArcGraphic.cs
// 역할: 스태미나 호(SCR-015 ⑱) 같은 호 모양을 직접 그리는 Graphic. 스프라이트가 필요 없다.
//       빈 부분(트랙) 전체 원 + 채워진 비율만큼의 호를 그리고, 낮음(25% 이하)이면 점선 호로 바꾼다(모양 구분 규칙).
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-025, NFR-001, NFR-056, FR-130, DES-091, DES-122 / SDD 3.7.10 (3)·(9), STORYBOARD SCR-015 ⑱
//
// 색은 테마 토큰에서 읽는다(코드에 색 값을 쓰지 않는다). 값(비율·낮음)이 바뀔 때만 정점을 다시 만든다.
// =============================================================
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>호 그래픽.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiArcGraphic : MaskableGraphic
    {
        /// <summary>시작 각도: 위쪽(12시) 방향에서 시계 방향으로 채운다.</summary>
        private const float FullCircleDegrees = 360f;

        /// <summary>트랙 원을 이루는 조각 수.</summary>
        private const int TrackSegments = 72;

        [SerializeField] private UiTheme _theme;
        [Tooltip("호 두께(1080p 기준 픽셀)")]
        [SerializeField] private float _thickness = 6f;
        [Tooltip("낮음일 때 점선 호를 몇 조각으로 나눌지")]
        [SerializeField] private int _dashCount = 24;
        [UiToken(UiTokenKind.Color)]
        [SerializeField] private string _fillToken = UiTokens.ColorStamina;
        [UiToken(UiTokenKind.Color)]
        [SerializeField] private string _trackToken = UiTokens.ColorTrack;
        [UiToken(UiTokenKind.Color)]
        [SerializeField] private string _lowToken = UiTokens.ColorWarning;

        private float _ratio = 1f;
        private bool _low;
        private Color _fillColor;
        private Color _trackColor;
        private Color _lowColor;

        public float Ratio => _ratio;
        public bool IsLow => _low;
        public UiTheme Theme => _theme;
        public string FillToken => _fillToken;
        public string TrackToken => _trackToken;
        public string LowToken => _lowToken;

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            ResolveColors();
        }

        /// <summary>표시 값을 바꾼다. 바뀔 때만 다시 그린다.</summary>
        /// <param name="ratio">채워진 비율(0~1)</param>
        /// <param name="low">낮음(점선으로 표시)</param>
        public void SetState(float ratio, bool low)
        {
            ratio = Mathf.Clamp01(ratio);
            if (Mathf.Approximately(ratio, _ratio) && low == _low)
            {
                return;
            }
            _ratio = ratio;
            _low = low;
            SetVerticesDirty();
        }

        private void ResolveColors()
        {
            if (_theme == null)
            {
                return;
            }
            _theme.TryGetColor(_fillToken, out _fillColor);
            _theme.TryGetColor(_trackToken, out _trackColor);
            _theme.TryGetColor(_lowToken, out _lowColor);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            float inner = outer - _thickness;
            if (outer <= 0f || inner <= 0f)
            {
                return;
            }
            Vector2 center = rect.center;

            // 빈 부분(트랙): 전체 원
            AddArc(vh, center, inner, outer, 0f, FullCircleDegrees, TrackSegments, _trackColor);
            if (_ratio <= 0f)
            {
                return;
            }

            if (_low)
            {
                // 낮음: 점선 호(조각 사이에 틈). 색만이 아니라 모양으로도 낮음을 알린다.
                float sweep = FullCircleDegrees * _ratio;
                float step = FullCircleDegrees / Mathf.Max(1, _dashCount);
                for (float a = 0f; a < sweep; a += step)
                {
                    float end = Mathf.Min(a + step * 0.5f, sweep);
                    AddArc(vh, center, inner, outer, a, end, 2, _lowColor);
                }
                return;
            }

            int segments = Mathf.Max(2, Mathf.CeilToInt(TrackSegments * _ratio));
            AddArc(vh, center, inner, outer, 0f, FullCircleDegrees * _ratio, segments, _fillColor);
        }

        /// <summary>12시 방향에서 시계 방향으로 [from, to] 도 구간의 띠를 정점 도우미에 더한다.</summary>
        private static void AddArc(VertexHelper vh, Vector2 center, float inner, float outer, float fromDegrees, float toDegrees, int segments, Color32 color)
        {
            int start = vh.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float t = segments == 0 ? 0f : (float)i / segments;
                float radians = Mathf.Deg2Rad * Mathf.Lerp(fromDegrees, toDegrees, t);
                var direction = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
                vh.AddVert(center + direction * inner, color, Vector4.zero);
                vh.AddVert(center + direction * outer, color, Vector4.zero);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 2;
                vh.AddTriangle(a, a + 1, a + 3);
                vh.AddTriangle(a, a + 3, a + 2);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            ResolveColors();
        }
#endif
    }
}

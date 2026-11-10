// =============================================================
// 파일: UiSafeArea16x9.cs
// 역할: 가운데 16:9 영역을 만드는 작은 부품.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: NFR-056, DES-122 / SDD 3.7.10 (3)·(4), STORYBOARD 2.3 (가운데 16:9 영역)
// =============================================================
using UnityEngine;

namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>
    /// 부모(캔버스) 안에서 높이 기준 16:9 폭만큼의 가운데 영역을 차지한다. HUD·창은 이 안에, 배경·막은 이 밖(캔버스 전체)에 둔다.
    /// 21:9 는 좌우에 배경만 보이고, 16:10 은 높이 기준이라 폭이 줄어든다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiSafeArea16x9 : MonoBehaviour
    {
        /// <summary>가로 대 세로 비율(16:9).</summary>
        public const float AspectWidthOverHeight = 16f / 9f;

        private RectTransform _rect;
        private bool _refreshing;

        private void OnEnable()
        {
            Refresh();
        }

        private void OnRectTransformDimensionsChange()
        {
            Refresh();
        }

        /// <summary>부모 크기에 맞춰 좌우 안쪽 여백을 계산해 적용한다.</summary>
        public void Refresh()
        {
            // offset을 바꾸면 OnRectTransformDimensionsChange가 같은 호출 안에서 다시 불려 Refresh가 되풀이된다.
            // 화면 없는 실행(Null 그래픽 장치)에서는 값이 수렴하지 않아 스택 오버플로로 크래시했으므로 재진입을 막는다.
            if (_refreshing)
            {
                return;
            }
            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }
            var parent = _rect.parent as RectTransform;
            if (parent == null)
            {
                return;
            }
            _refreshing = true;
            try
            {
                float height = parent.rect.height;
                float width = parent.rect.width;
                // 화면 없는 실행(Null 그래픽 장치)에서는 캔버스 크기가 0이거나 NaN일 수 있다.
                // NaN은 매번 '다르다'고 판정돼 offset 설정 → 치수 변경 → Refresh가 끝없이 되풀이(스택 오버플로, 크래시)되므로 건너뛴다.
                if (!(width > 0f) || !(height > 0f) || float.IsInfinity(width) || float.IsInfinity(height))
                {
                    return;
                }
                float inset = Mathf.Max(0f, (width - height * AspectWidthOverHeight) * 0.5f);

                // 부모 전체에 늘어나는 앵커 + 좌우 여백
                Vector2 min = new Vector2(inset, 0f);
                Vector2 max = new Vector2(-inset, 0f);
                if (_rect.anchorMin != Vector2.zero) { _rect.anchorMin = Vector2.zero; }
                if (_rect.anchorMax != Vector2.one) { _rect.anchorMax = Vector2.one; }
                if (_rect.offsetMin != min) { _rect.offsetMin = min; }
                if (_rect.offsetMax != max) { _rect.offsetMax = max; }
            }
            finally
            {
                _refreshing = false;
            }
        }
    }
}

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
            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }
            var parent = _rect.parent as RectTransform;
            if (parent == null)
            {
                return;
            }
            float height = parent.rect.height;
            float width = parent.rect.width;
            float inset = Mathf.Max(0f, (width - height * AspectWidthOverHeight) * 0.5f);

            // 부모 전체에 늘어나는 앵커 + 좌우 여백
            Vector2 min = new Vector2(inset, 0f);
            Vector2 max = new Vector2(-inset, 0f);
            if (_rect.anchorMin != Vector2.zero) { _rect.anchorMin = Vector2.zero; }
            if (_rect.anchorMax != Vector2.one) { _rect.anchorMax = Vector2.one; }
            if (_rect.offsetMin != min) { _rect.offsetMin = min; }
            if (_rect.offsetMax != max) { _rect.offsetMax = max; }
        }
    }
}

// =============================================================
// 파일: ThemedSelectable.cs
// 역할: Selectable(버튼·목록 행 등)의 상태별 모양을 토큰으로 그린다. Selectable 의 색 전환(transition)은 쓰지 않는다(None).
//       포커스 = 3px 굵은 외곽선 + 왼쪽 > 표시 (색에만 의존하지 않음).
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: NFR-004, NFR-056, FR-129, FR-130, DES-122 / SDD 3.7.10 (3), STORYBOARD 2.5 (기본·호버·포커스·눌림·사용 불가)
//
// 구조(프리팹): 뿌리 Image = 테두리(상태에 따라 색이 바뀜), 자식 Fill = 테두리 안쪽 3px 안으로 들어간 배경, 자식 Label,
//   자식 FocusMarker(>). 포커스일 때 테두리 색이 color-focus 가 되고 > 가 보인다.
// =============================================================
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>버튼·행의 모양 변형.</summary>
    public enum UiButtonVariant
    {
        Primary = 0,
        Secondary = 1,
        Danger = 2,
        ListRow = 3,
    }

    /// <summary>상태별 모양을 그리는 컴포넌트.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public sealed class ThemedSelectable : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private UiButtonVariant _variant = UiButtonVariant.Secondary;
        [SerializeField] private ThemedGraphic _border;
        [SerializeField] private ThemedGraphic _fill;
        [SerializeField] private ThemedText _label;
        [SerializeField] private GameObject _focusMarker;

        private Selectable _selectable;
        private UiButton _button;
        private bool _hover;
        private bool _pressed;
        private bool _selected;

        public UiButtonVariant Variant => _variant;
        public ThemedGraphic Border => _border;
        public ThemedGraphic Fill => _fill;
        public ThemedText Label => _label;
        public GameObject FocusMarker => _focusMarker;

        private void OnEnable()
        {
            _selectable = GetComponent<Selectable>();
            _button = GetComponent<UiButton>();
            if (_selectable.transition != Selectable.Transition.None)
            {
                _selectable.transition = Selectable.Transition.None;
            }
            if (_button != null)
            {
                _button.StateChanged += Redraw;
            }
            _hover = false;
            _pressed = false;
            _selected = UnityEngine.Application.isPlaying && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            Redraw();
        }

        private void OnDisable()
        {
            if (_button != null)
            {
                _button.StateChanged -= Redraw;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled)
                {
                    Redraw();
                }
            };
        }
#endif

        public void OnPointerEnter(PointerEventData eventData) { _hover = true; Redraw(); }
        public void OnPointerExit(PointerEventData eventData) { _hover = false; _pressed = false; Redraw(); }
        public void OnPointerDown(PointerEventData eventData) { _pressed = true; Redraw(); }
        public void OnPointerUp(PointerEventData eventData) { _pressed = false; Redraw(); }
        public void OnSelect(BaseEventData eventData) { _selected = true; Redraw(); }
        public void OnDeselect(BaseEventData eventData) { _selected = false; Redraw(); }

        /// <summary>현재 상태(호버·포커스·눌림·사용 불가)에 맞게 토큰을 적용한다.</summary>
        public void Redraw()
        {
            bool unavailable = _button != null && _button.IsUnavailable;
            bool primary = _variant == UiButtonVariant.Primary;

            // 바탕과 글자
            string fill = primary && !unavailable ? UiTokens.ColorPrimary : UiTokens.ColorSurfaceRaised;
            string label;
            if (unavailable)
            {
                label = UiTokens.ColorDisabled;
            }
            else
            {
                switch (_variant)
                {
                    case UiButtonVariant.Primary: label = UiTokens.ColorPrimaryText; break;
                    case UiButtonVariant.Danger: label = UiTokens.ColorDanger; break;
                    default: label = UiTokens.ColorText; break;
                }
            }

            // 테두리: 포커스 > 눌림 > 호버 > 기본 순서. 사용 불가는 흐린 테두리(포커스는 여전히 보인다).
            string border;
            if (_selected)
            {
                border = UiTokens.ColorFocus;
            }
            else if (unavailable)
            {
                border = UiTokens.ColorDisabled;
            }
            else if (_pressed)
            {
                border = UiTokens.ColorLight;
            }
            else if (_hover)
            {
                border = UiTokens.ColorTextDim;
            }
            else if (primary)
            {
                border = UiTokens.ColorPrimary;
            }
            else if (_variant == UiButtonVariant.Danger)
            {
                border = UiTokens.ColorDanger;
            }
            else
            {
                border = _variant == UiButtonVariant.ListRow ? UiTokens.ColorSurfaceRaised : UiTokens.ColorBorder;
            }

            if (_fill != null) { _fill.SetColorToken(fill); }
            if (_border != null) { _border.SetColorToken(border); }
            if (_label != null) { _label.SetColorToken(label); }
            if (_focusMarker != null && _focusMarker.activeSelf != _selected)
            {
                _focusMarker.SetActive(_selected);
            }
            // 주 버튼은 바탕이 포커스 색(빛)과 같으므로 > 가 묻히지 않게 어두운 글자색을 쓴다.
            if (_focusMarker != null)
            {
                ThemedText marker = _focusMarker.GetComponent<ThemedText>();
                if (marker != null)
                {
                    marker.SetColorToken(primary && !unavailable ? UiTokens.ColorPrimaryText : UiTokens.ColorFocus);
                }
            }
        }
    }
}

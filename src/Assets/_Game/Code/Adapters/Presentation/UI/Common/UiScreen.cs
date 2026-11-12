// =============================================================
// 파일: UiScreen.cs
// 역할: 화면 프리팹 루트에 붙는 화면 기본 컴포넌트. 화면 층(Canvas 정렬 순서), 처음 선택할 요소, 보이기·숨기기를 맡는다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-166, NFR-002, NFR-004, DES-091, DES-122, I-07 / SDD 3.7.10 (4)·(5)·(6), STORYBOARD 2.6·2.7
//
// 숨김 = Canvas, GraphicRaycaster, CanvasGroup 을 함께 끈다(오브젝트는 SetActive 로 끄지 않아 참조와 레이아웃이 유지된다).
// Canvas 만 끄면 Selectable 이 살아 있어 키보드·패드 내비게이션이 숨은 화면으로 넘어가고, 레이캐스터만 남으면
// 보이지 않는 전체 화면 막이 아래 화면 클릭을 가로챈다(v0.12 ESC 메뉴 결함의 재발 방지).
// =============================================================
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>화면 층. 값이 Canvas.sortingOrder 가 된다(SDD 3.7.10 (5)).</summary>
    public enum UiScreenLayer
    {
        WorldMarkers = 0,
        Hud = 100,
        Window = 200,
        Alert = 300,
        Modal = 400,
        System = 500,
    }

    /// <summary>UI 화면 하나(Canvas 하나).</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public class UiScreen : MonoBehaviour
    {
        [SerializeField] private UiScreenLayer _layer = UiScreenLayer.Window;
        [Tooltip("화면이 열릴 때 처음 선택할 요소(키보드·패드 포커스).")]
        [SerializeField] private Selectable _firstSelected;
        [Tooltip("켜면 시작할 때 숨긴 상태가 된다.")]
        [SerializeField] private bool _startHidden;
        [Tooltip("끄면 입력을 받지 않는 화면(HUD 등)이다. 보여도 레이캐스터와 CanvasGroup 입력을 켜지 않는다.")]
        [SerializeField] private bool _interactive = true;

        private Canvas _canvas;
        private GraphicRaycaster _raycaster;
        private CanvasGroup _group;
        private bool _cached;
        private bool _visible = true;

        /// <summary>화면이 보이거나 숨겨졌다(화면, 보이는지). UiScreenStack 이 듣는다.</summary>
        public static event Action<UiScreen, bool> VisibilityChanged;

        public UiScreenLayer Layer => _layer;
        public Selectable FirstSelected => _firstSelected;
        public bool IsInteractive => _interactive;

        /// <summary>화면이 보이는지.</summary>
        public bool IsVisible => _visible;

        protected virtual void Awake()
        {
            Cache();
            _visible = _canvas.enabled;
            if (_startHidden)
            {
                SetVisible(false);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 층 값이 Canvas 정렬 순서와 같도록 맞춘다(프리팹에서 고친 값이 그대로 저장된다).
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    Canvas canvas = GetComponent<Canvas>();
                    if (canvas != null && canvas.sortingOrder != (int)_layer)
                    {
                        canvas.sortingOrder = (int)_layer;
                    }
                }
            };
        }
#endif

        private void Cache()
        {
            if (_cached)
            {
                return;
            }
            _canvas = GetComponent<Canvas>();
            _raycaster = GetComponent<GraphicRaycaster>();
            _group = GetComponent<CanvasGroup>();
            _cached = true;
        }

        /// <summary>
        /// 화면을 보이거나 숨긴다. Canvas, GraphicRaycaster, CanvasGroup 을 함께 켜고 끈다.
        /// </summary>
        /// <param name="visible">보일지 여부</param>
        public void SetVisible(bool visible)
        {
            Cache();
            bool changed = _visible != visible;
            _visible = visible;

            _canvas.enabled = visible;
            bool input = visible && _interactive;
            if (_raycaster != null)
            {
                _raycaster.enabled = input;
            }
            if (_group != null)
            {
                _group.interactable = input;
                _group.blocksRaycasts = input;
            }

            if (changed)
            {
                OnVisibilityChanged(visible);
                VisibilityChanged?.Invoke(this, visible);
            }
        }

        /// <summary>보임/숨김이 바뀐 뒤 파생 화면이 할 일.</summary>
        protected virtual void OnVisibilityChanged(bool visible)
        {
        }

        /// <summary>닫기 입력(Esc, Ⓑ)이 맨 위 화면에 전달됐다. 기본은 닫기.</summary>
        public virtual void OnCancel()
        {
            SetVisible(false);
        }

        /// <summary>탭 이동 입력(Q/E, LB/RB)이 전달됐다. -1 이전, +1 다음. 기본은 아무것도 안 한다.</summary>
        public virtual void OnTab(int direction)
        {
        }

        /// <summary>처음 선택할 요소를 선택한다.</summary>
        public void SelectFirst()
        {
            Select(_firstSelected);
        }

        /// <summary>요소 하나를 선택(포커스)한다. EventSystem 이 없거나 요소가 꺼져 있으면 아무것도 안 한다.</summary>
        public static void Select(Selectable target)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || target == null || !target.gameObject.activeInHierarchy)
            {
                return;
            }
            eventSystem.SetSelectedGameObject(target.gameObject);
        }
    }
}

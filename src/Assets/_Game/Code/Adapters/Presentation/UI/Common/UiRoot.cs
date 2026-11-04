// =============================================================
// 파일: UiRoot.cs
// 역할: UI 전체의 뿌리(UiRoot 프리팹). 글자 공급자·글자 크기 단계를 UI 에 알리고, EventSystem 의 입력 모듈과
//       UiInputRouter 를 게임의 Input Actions 인스턴스에 연결한다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-117, FR-166, NFR-002, NFR-004, NFR-034, NFR-056, DES-092, DES-100, DES-122 / SDD 3.7.10 (6)·(7)
//
// 입력 모듈은 게임이 쓰는 것과 같은 Input Actions 인스턴스를 가리킨다(복제하지 않음 — 재설정한 바인딩(FR-117)이 메뉴 조작에도 적용).
// Cancel 칸은 비워 둔다(닫기는 UiInputRouter 한 곳에서만 처리).
// =============================================================
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using Game.Application.Ports;

namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>UI 뿌리 컴포넌트.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UiScreenStack))]
    [RequireComponent(typeof(UiInputRouter))]
    public sealed class UiRoot : MonoBehaviour
    {
        private const string PointPath = "UI/Point";
        private const string ClickPath = "UI/Click";
        private const string ScrollPath = "UI/ScrollWheel";
        private const string NavigatePath = "UI/Navigate";
        private const string SubmitPath = "UI/Submit";

        [SerializeField] private UiTheme _theme;
        [SerializeField] private EventSystem _eventSystem;
        [SerializeField] private InputSystemUIInputModule _inputModule;

        private UiScreenStack _stack;
        private UiInputRouter _router;

        public UiTheme Theme => _theme;
        public EventSystem EventSystem => _eventSystem;
        public InputSystemUIInputModule InputModule => _inputModule;
        public UiScreenStack Stack => _stack;
        public UiInputRouter Router => _router;

        private void Awake()
        {
            _stack = GetComponent<UiScreenStack>();
            _router = GetComponent<UiInputRouter>();
            if (_eventSystem == null)
            {
                _eventSystem = GetComponentInChildren<EventSystem>(true);
            }
            if (_inputModule == null)
            {
                _inputModule = GetComponentInChildren<InputSystemUIInputModule>(true);
            }
        }

        /// <summary>글자 공급자와 글자 크기 단계를 UI 전체에 적용한다. 켜진 모든 ThemedText 가 다시 적용된다.</summary>
        public void Initialize(ILocalizedText text, UiTextScale scale = UiTextScale.Normal)
        {
            UiContext.SetTextScale(scale);
            UiContext.SetText(text);
        }

        /// <summary>
        /// 입력 모듈과 라우터를 게임의 Input Actions 인스턴스에 연결한다.
        /// </summary>
        /// <param name="actions">GameInput 이 복제해 쓰는 Input Actions 인스턴스</param>
        /// <exception cref="ArgumentNullException">actions 가 null 일 때</exception>
        public void BindInput(InputActionAsset actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }
            if (_inputModule != null)
            {
                _inputModule.actionsAsset = actions;
                _inputModule.point = Reference(actions, PointPath);
                _inputModule.leftClick = Reference(actions, ClickPath);
                _inputModule.middleClick = null;
                _inputModule.rightClick = null;
                _inputModule.scrollWheel = Reference(actions, ScrollPath);
                _inputModule.move = Reference(actions, NavigatePath);
                _inputModule.submit = Reference(actions, SubmitPath);
                _inputModule.cancel = null; // 닫기는 UiInputRouter 한 곳에서만 처리
                _inputModule.deselectOnBackgroundClick = false; // 패드로 바꿨을 때 선택이 남게
            }
            _router.Bind(actions);
        }

        /// <summary>선택 요소를 비운다(게임 화면으로 돌아갈 때).</summary>
        public void ClearSelection()
        {
            if (_eventSystem != null)
            {
                _eventSystem.SetSelectedGameObject(null);
            }
        }

        private static InputActionReference Reference(InputActionAsset asset, string path)
        {
            InputAction action = asset.FindAction(path, false);
            return action == null ? null : InputActionReference.Create(action);
        }
    }
}

// =============================================================
// 파일: UiInputRouter.cs
// 역할: UI 동작 지도의 Cancel(Esc, Ⓑ)과 TabPrev/TabNext(Q/E, LB/RB)를 구독해 화면 스택의 맨 위 화면에 전달한다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-166, NFR-002, NFR-004, DES-092, DES-122 / SDD 3.7.10 (6), STORYBOARD 2.6 「닫기까지 한 번의 입력」
//
// 입력 모듈의 Cancel 칸은 비워 두고 닫기를 여기 한 곳에서만 처리한다(두 번 닫힘 방지).
// =============================================================
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>UI 입력 라우터(UiRoot 프리팹에 붙는다).</summary>
    [DisallowMultipleComponent]
    public sealed class UiInputRouter : MonoBehaviour
    {
        private const string UiMapName = "UI";
        private const string CancelName = "Cancel";
        private const string TabPrevName = "TabPrev";
        private const string TabNextName = "TabNext";

        [SerializeField] private UiScreenStack _stack;

        private InputAction _cancel;
        private InputAction _tabPrev;
        private InputAction _tabNext;

        private void Awake()
        {
            if (_stack == null)
            {
                _stack = GetComponent<UiScreenStack>();
            }
        }

        /// <summary>입력 애셋의 UI 지도에 연결한다. 이미 연결돼 있으면 먼저 해제한다.</summary>
        /// <exception cref="ArgumentNullException">asset 이 null 일 때</exception>
        public void Bind(InputActionAsset asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }
            Unbind();
            InputActionMap map = asset.FindActionMap(UiMapName, true);
            _cancel = map[CancelName];
            _tabPrev = map[TabPrevName];
            _tabNext = map[TabNextName];
            _cancel.performed += OnCancelPerformed;
            _tabPrev.performed += OnTabPrevPerformed;
            _tabNext.performed += OnTabNextPerformed;
        }

        /// <summary>연결을 해제한다.</summary>
        public void Unbind()
        {
            if (_cancel != null) { _cancel.performed -= OnCancelPerformed; _cancel = null; }
            if (_tabPrev != null) { _tabPrev.performed -= OnTabPrevPerformed; _tabPrev = null; }
            if (_tabNext != null) { _tabNext.performed -= OnTabNextPerformed; _tabNext = null; }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void OnCancelPerformed(InputAction.CallbackContext context) => DispatchCancel();
        private void OnTabPrevPerformed(InputAction.CallbackContext context) => DispatchTab(-1);
        private void OnTabNextPerformed(InputAction.CallbackContext context) => DispatchTab(1);

        /// <summary>닫기 입력을 맨 위 화면에 전달한다. 화면이 방금 열린 같은 프레임이면 무시한다.</summary>
        public void DispatchCancel()
        {
            if (_stack == null || _stack.Top == null || Time.frameCount == _stack.LastChangeFrame)
            {
                return;
            }
            _stack.Top.OnCancel();
        }

        /// <summary>탭 이동 입력을 맨 위 화면에 전달한다.</summary>
        public void DispatchTab(int direction)
        {
            if (_stack != null && _stack.Top != null)
            {
                _stack.Top.OnTab(direction);
            }
        }
    }
}

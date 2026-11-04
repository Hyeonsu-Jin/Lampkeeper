// =============================================================
// 파일: UiScreenStack.cs
// 역할: 보이는 Window·Modal·System 층 화면을 연 순서로 쌓는다. 맨 위 화면만 닫기·탭 이동을 받고,
//       화면이 열리면 그 화면의 처음 선택 요소에 포커스를 주며, 닫으면 아래 화면의 마지막 선택 요소로 포커스를 되돌린다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-166, NFR-002, NFR-004, DES-122 / SDD 3.7.10 (5)·(6), STORYBOARD 2.6·2.7
// =============================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>화면 스택(UiRoot 프리팹에 붙는다).</summary>
    [DisallowMultipleComponent]
    public sealed class UiScreenStack : MonoBehaviour
    {
        private readonly List<UiScreen> _screens = new List<UiScreen>();
        private readonly Dictionary<UiScreen, GameObject> _lastSelected = new Dictionary<UiScreen, GameObject>();

        /// <summary>맨 위 화면(없으면 null).</summary>
        public UiScreen Top => _screens.Count > 0 ? _screens[_screens.Count - 1] : null;

        /// <summary>쌓인 화면 수.</summary>
        public int Count => _screens.Count;

        /// <summary>스택이 마지막으로 바뀐 프레임. 열자마자 같은 프레임의 닫기 입력을 무시하는 데 쓴다.</summary>
        public int LastChangeFrame { get; private set; } = -1;

        private void OnEnable()
        {
            UiScreen.VisibilityChanged += OnVisibilityChanged;
        }

        private void OnDisable()
        {
            UiScreen.VisibilityChanged -= OnVisibilityChanged;
        }

        /// <summary>입력을 받는 층인지(HUD·월드 표시·경고 층은 쌓지 않는다).</summary>
        private static bool IsTracked(UiScreen screen)
        {
            return screen.IsInteractive
                   && (screen.Layer == UiScreenLayer.Window || screen.Layer == UiScreenLayer.Modal || screen.Layer == UiScreenLayer.System);
        }

        private void OnVisibilityChanged(UiScreen screen, bool visible)
        {
            if (!IsTracked(screen))
            {
                return;
            }

            if (visible)
            {
                RememberSelectionOfTop();
                _screens.Remove(screen);
                _screens.Add(screen);
                LastChangeFrame = Time.frameCount;
                screen.SelectFirst();
                return;
            }

            bool wasTop = Top == screen;
            _screens.Remove(screen);
            _lastSelected.Remove(screen);
            LastChangeFrame = Time.frameCount;
            if (wasTop)
            {
                RestoreFocus(Top);
            }
        }

        /// <summary>새 화면이 위에 올라가기 전에 지금 맨 위 화면의 선택 요소를 기억한다.</summary>
        private void RememberSelectionOfTop()
        {
            UiScreen top = Top;
            EventSystem eventSystem = EventSystem.current;
            if (top != null && eventSystem != null && eventSystem.currentSelectedGameObject != null)
            {
                _lastSelected[top] = eventSystem.currentSelectedGameObject;
            }
        }

        private void RestoreFocus(UiScreen screen)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }
            if (screen == null)
            {
                eventSystem.SetSelectedGameObject(null);
                return;
            }
            if (_lastSelected.TryGetValue(screen, out GameObject remembered) && remembered != null && remembered.activeInHierarchy)
            {
                var selectable = remembered.GetComponent<Selectable>();
                if (selectable != null)
                {
                    UiScreen.Select(selectable);
                    return;
                }
            }
            screen.SelectFirst();
        }
    }
}

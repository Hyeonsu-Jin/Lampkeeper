// =============================================================
// 파일: SteamRequiredScreen.cs
// 역할: SCR-001을 씬에 올리는 MonoBehaviour. UIDocument의 요소 트리에 공통 스타일을 붙이고 SteamRequiredView를 만든다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: NFR-006, DES-091, STORYBOARD SCR-001 / SDD 3.7.10, 3.10.7
//
// 이 컴포넌트가 붙은 GameObject에는 UIDocument(SteamRequired.uxml + PanelSettings)가 있어야 한다.
// 평소에는 비활성으로 두고, 부트가 Steam 확인에 실패하면 Show로 켠다.
// =============================================================
using System;
using Game.Adapters.Presentation.UI.Common;
using Game.Application.Ports;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Adapters.Presentation.UI.Screens
{
    /// <summary>SCR-001 화면 컴포넌트.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SteamRequiredScreen : MonoBehaviour
    {
        [Tooltip("공통 USS(색 토큰, 글자 크기 변수). UI/Common/Base.uss")]
        [SerializeField] private StyleSheet _baseStyle;

        private SteamRequiredView _view;

        /// <summary>③ [다시 확인]을 눌렀을 때.</summary>
        public event Action RetryRequested;

        /// <summary>⑤ [종료]를 눌렀을 때.</summary>
        public event Action QuitRequested;

        /// <summary>화면이 떠 있는지.</summary>
        public bool IsShown => gameObject.activeSelf && _view != null;

        /// <summary>현재 표시 상태. 화면이 아직 없으면 Default.</summary>
        public SteamRequiredState State => _view != null ? _view.State : SteamRequiredState.Default;

        /// <summary>
        /// 화면을 켠다.
        /// </summary>
        /// <param name="text">글자 공급자</param>
        /// <param name="scale">글자 크기 단계(M2-01 옵션 전에는 보통)</param>
        /// <exception cref="InvalidOperationException">UIDocument에 UXML이 없을 때</exception>
        public void Show(ILocalizedText text, UiTextScale scale = UiTextScale.Normal)
        {
            gameObject.SetActive(true);

            var document = GetComponent<UIDocument>();
            VisualElement root = document.rootVisualElement;
            if (root == null || root.childCount == 0)
            {
                throw new InvalidOperationException("SteamRequiredScreen의 UIDocument에 SteamRequired.uxml이 연결되지 않았습니다.");
            }

            if (_baseStyle == null)
            {
                Debug.LogWarning("[SCR-001] Base.uss가 연결되지 않아 공통 스타일 없이 표시합니다.");
            }
            UiTheme.Apply(root, _baseStyle, scale);

            if (_view == null)
            {
                _view = new SteamRequiredView(root, text);
                _view.RetryRequested += () => RetryRequested?.Invoke();
                _view.QuitRequested += () => QuitRequested?.Invoke();
            }
            _view.SetState(SteamRequiredState.Default);
            _view.FocusDefault();
        }

        /// <summary>③ 눌린 뒤 결과를 기다리는 상태로 바꾼다.</summary>
        public void ShowChecking()
        {
            _view?.SetState(SteamRequiredState.Checking);
        }

        /// <summary>재확인에 실패했음을 ② 아래에 보인다.</summary>
        public void ShowRetryFailed()
        {
            if (_view == null)
            {
                return;
            }
            _view.SetState(SteamRequiredState.Failed);
            _view.FocusDefault();
        }

        /// <summary>화면을 닫는다(재확인에 성공했을 때).</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            _view = null;
        }
    }
}

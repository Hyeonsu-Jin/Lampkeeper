// =============================================================
// 파일: KeycapView.cs
// 역할: 키캡 자리. 현재 입력 장치(키보드·마우스 / 게임패드)의 할당을 글자로 보인다. M1 은 키 이름 글자만(아이콘 세트는 M2-01).
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-166, NFR-002, NFR-004, DES-092, DES-122 / SDD 3.7.10 (3), STORYBOARD 2.4·2.7
//
// 리치 텍스트를 끄므로 문장 안에 키캡을 넣을 때는 글자와 키캡을 가로 레이아웃으로 나란히 둔다.
// =============================================================
using Game.Adapters.Presentation.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>키캡 표시.</summary>
    [DisallowMultipleComponent]
    public sealed class KeycapView : MonoBehaviour
    {
        /// <summary>할당이 없을 때(패드 보류 행동 등) 보이는 글자.</summary>
        public const string UnassignedText = "-";

        private const string KeyboardMouseGroup = "KeyboardMouse";
        private const string GamepadGroup = "Gamepad";

        [SerializeField] private ThemedText _label;
        [Tooltip("표시할 Input Action 이름(예: Gameplay/Menu)")]
        [SerializeField] private string _actionPath = string.Empty;

        public string ActionPath => _actionPath;

        /// <summary>장치 종류에 맞는 할당을 보인다. 장치가 바뀌는 이벤트에서 1초 안에 다시 부른다. [FR-166]</summary>
        /// <param name="asset">게임의 Input Actions 인스턴스</param>
        /// <param name="device">현재 입력 장치 종류</param>
        public void Refresh(InputActionAsset asset, InputDeviceKind device)
        {
            string text = UnassignedText;
            if (asset != null && !string.IsNullOrEmpty(_actionPath))
            {
                InputAction action = asset.FindAction(_actionPath, false);
                if (action != null)
                {
                    string group = device == InputDeviceKind.Gamepad ? GamepadGroup : KeyboardMouseGroup;
                    string display = action.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions, group);
                    if (!string.IsNullOrEmpty(display))
                    {
                        text = display;
                    }
                }
            }
            if (_label != null)
            {
                _label.SetDynamicText(text);
            }
        }
    }
}

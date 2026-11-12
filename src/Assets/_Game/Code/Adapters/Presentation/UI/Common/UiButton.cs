// =============================================================
// 파일: UiButton.cs
// 역할: 게임의 모든 버튼. 사용 불가여도 interactable 을 끄지 않아 포커스를 받고, 누르면 onClick 대신 사유 이벤트를 낸다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: NFR-004, FR-166, DES-122 / SDD 3.7.10 (3), STORYBOARD 2.5 (사용 불가 상태)
//
// 사용 불가 버튼의 모양(점선 테두리·흐린 글자)은 ThemedSelectable 이 그린다.
// =============================================================
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>게임 버튼.</summary>
    public sealed class UiButton : Button
    {
        [SerializeField] private bool _unavailable;
        [SerializeField] private string _unavailableReasonKey = string.Empty;

        /// <summary>사용 불가 상태에서 눌렀다(사유 텍스트 키). 도움말 줄에 사유를 보이는 데 쓴다.</summary>
        public event Action<string> UnavailablePressed;

        /// <summary>사용 불가 여부나 사유가 바뀌었다(ThemedSelectable 이 다시 그린다).</summary>
        public event Action StateChanged;

        /// <summary>지금 사용 불가인지. true 여도 interactable 은 켜져 있다.</summary>
        public bool IsUnavailable => _unavailable;

        /// <summary>사용 불가 사유 텍스트 키(없으면 빈 문자열).</summary>
        public string UnavailableReasonKey => _unavailableReasonKey;

        /// <summary>사용 가능 여부를 바꾼다.</summary>
        /// <param name="unavailable">true 면 사용 불가</param>
        /// <param name="reasonKey">사용 불가 사유 텍스트 키(선택)</param>
        public void SetUnavailable(bool unavailable, string reasonKey = null)
        {
            string key = reasonKey ?? string.Empty;
            if (_unavailable == unavailable && _unavailableReasonKey == key)
            {
                return;
            }
            _unavailable = unavailable;
            _unavailableReasonKey = key;
            StateChanged?.Invoke();
        }

        /// <summary>사용 가능 여부를 켜고 끈다(UI Toolkit 의 SetEnabled 와 같은 호출 모양).</summary>
        public void SetEnabled(bool enabledForUse)
        {
            SetUnavailable(!enabledForUse);
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (_unavailable)
            {
                if (eventData.button == PointerEventData.InputButton.Left)
                {
                    UnavailablePressed?.Invoke(_unavailableReasonKey);
                }
                return;
            }
            base.OnPointerClick(eventData);
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            if (_unavailable)
            {
                UnavailablePressed?.Invoke(_unavailableReasonKey);
                return;
            }
            base.OnSubmit(eventData);
        }
    }
}

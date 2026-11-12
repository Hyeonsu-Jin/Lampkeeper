// =============================================================
// 파일: UiRaycastBlocker.cs
// 역할: 「이 그래픽은 일부러 레이캐스트를 받는다」는 표시 부품.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: NFR-056, DES-122 / SDD 3.7.10 (3)·(4)
// =============================================================
using UnityEngine;

namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>
    /// 「이 그래픽은 일부러 레이캐스트를 받는다」는 표시. 모달 뒤 어둡게 막처럼 아래 화면 입력을 막아야 하는 그래픽에 붙인다.
    /// 규칙 검사는 이 표시와 Selectable 의 targetGraphic 이 아닌데 raycastTarget 이 켜진 Graphic 을 실패로 본다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiRaycastBlocker : MonoBehaviour
    {
    }
}

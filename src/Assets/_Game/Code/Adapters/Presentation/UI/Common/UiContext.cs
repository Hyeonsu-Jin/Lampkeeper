// =============================================================
// 파일: UiContext.cs
// 역할: UI 전체가 함께 쓰는 현재 상태(글자 공급자, 글자 크기 단계)와 변경 알림.
//       UiRoot가 값을 바꾸면 켜져 있는 모든 ThemedText가 이 알림을 받아 다시 적용한다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: NFR-034, NFR-044, NFR-056, DES-093, DES-095 / SDD 3.7.10 (2)·(7)
//
// 싱글턴 대신 쓰는 아주 작은 공유 상태다. 값을 쓰는 곳은 UiRoot 하나뿐이다.
// =============================================================
using System;
using Game.Application.Ports;
using UnityEngine;

namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>UI 공유 상태.</summary>
    public static class UiContext
    {
        /// <summary>현재 언어의 글자 공급자. 아직 정해지지 않았으면 null(에디터).</summary>
        public static ILocalizedText Text { get; private set; }

        /// <summary>현재 글자 크기 단계.</summary>
        public static UiTextScale TextScale { get; private set; } = UiTextScale.Normal;

        /// <summary>글자 공급자나 글자 크기 단계가 바뀌었다(언어 변경 포함).</summary>
        public static event Action Changed;

        /// <summary>글자 공급자를 바꾼다(언어를 바꿀 때도 부른다).</summary>
        public static void SetText(ILocalizedText text)
        {
            Text = text;
            Changed?.Invoke();
        }

        /// <summary>글자 크기 단계를 바꾼다. 게임 진행에는 영향이 없다(I-07).</summary>
        public static void SetTextScale(UiTextScale scale)
        {
            if (TextScale == scale)
            {
                return;
            }
            TextScale = scale;
            Changed?.Invoke();
        }

        // 에디터에서 도메인 다시 불러오기를 끄면 정적 값이 남으므로 재생 시작 때 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            Text = null;
            TextScale = UiTextScale.Normal;
            Changed = null;
        }
    }
}

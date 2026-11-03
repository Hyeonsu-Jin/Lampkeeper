// =============================================================
// 파일: UiTheme.cs
// 역할: 패널 루트에 공통 USS(Base.uss)를 붙이고 글자 크기 단계 클래스(text-small/normal/large)를 적용한다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: SDD 3.7.10 (I-04 접근성 기반을 M1 UI부터, I-12 다크 단일 테마), NFR-056
// =============================================================
using System;
using UnityEngine.UIElements;

namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>글자 크기 단계. 루트 USS 클래스가 글자 크기 변수를 바꿔 화면 전체에 적용된다.</summary>
    public enum UiTextScale
    {
        Small = 0,
        Normal = 1,
        Large = 2,
    }

    /// <summary>UI 공통 기반 적용 도우미. 상태가 없는 정적 클래스.</summary>
    public static class UiTheme
    {
        /// <summary>Base.uss의 작은 글자 클래스 이름.</summary>
        public const string TextSmallClass = "text-small";
        /// <summary>Base.uss의 보통 글자 클래스 이름.</summary>
        public const string TextNormalClass = "text-normal";
        /// <summary>Base.uss의 큰 글자 클래스 이름.</summary>
        public const string TextLargeClass = "text-large";

        /// <summary>
        /// 패널 루트에 공통 스타일을 적용한다. 색 토큰(:root 변수)은 패널 루트에 붙어야 모든 화면이 쓸 수 있다.
        /// </summary>
        /// <param name="panelRoot">UIDocument.rootVisualElement</param>
        /// <param name="baseStyle">Base.uss (null이면 스타일 없이 진행하므로 호출자가 로그로 알려야 한다)</param>
        /// <param name="scale">글자 크기 단계</param>
        /// <exception cref="ArgumentNullException">panelRoot가 null일 때</exception>
        public static void Apply(VisualElement panelRoot, StyleSheet baseStyle, UiTextScale scale)
        {
            if (panelRoot == null)
            {
                throw new ArgumentNullException(nameof(panelRoot));
            }
            if (baseStyle != null && !panelRoot.styleSheets.Contains(baseStyle))
            {
                panelRoot.styleSheets.Add(baseStyle);
            }
            SetTextScale(panelRoot, scale);
        }

        /// <summary>글자 크기 단계 클래스를 바꾼다. 게임 진행은 건드리지 않는다(I-07).</summary>
        public static void SetTextScale(VisualElement panelRoot, UiTextScale scale)
        {
            panelRoot.RemoveFromClassList(TextSmallClass);
            panelRoot.RemoveFromClassList(TextNormalClass);
            panelRoot.RemoveFromClassList(TextLargeClass);
            switch (scale)
            {
                case UiTextScale.Small: panelRoot.AddToClassList(TextSmallClass); break;
                case UiTextScale.Large: panelRoot.AddToClassList(TextLargeClass); break;
                default: panelRoot.AddToClassList(TextNormalClass); break;
            }
        }
    }
}

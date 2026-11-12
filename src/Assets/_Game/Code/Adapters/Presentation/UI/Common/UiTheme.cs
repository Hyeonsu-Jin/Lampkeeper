// =============================================================
// 파일: UiTheme.cs
// 역할: UI 테마 애셋(ScriptableObject). 색 토큰, 글자 크기 토큰과 단계 배율, 글꼴 토큰, 대비 검사 짝을 한 벌로 가진다.
//       화면 프리팹의 글자·그래픽은 토큰 이름으로만 값을 참조하고, 실제 값은 이 애셋에만 있다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: NFR-056, NFR-057, FR-129, FR-130, DES-091, DES-095, DES-122 / SDD 3.7.10 (1)~(3), D-27, STORYBOARD 2.1, 2.2
//
// 다크 단일 테마(I-12): 토큰 세트 교체·테마 선택 구조는 없다. 값은 애셋(UI/Theme/UiTheme.asset)을 인스펙터에서 고친다.
// =============================================================
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>글자 크기 단계. 단계 배율(작게 0.9 / 보통 1.0 / 크게 1.25)을 고른다.</summary>
    public enum UiTextScale
    {
        Small = 0,
        Normal = 1,
        Large = 2,
    }

    /// <summary>글꼴 굵기. TMP의 가짜 굵게(FontStyles.Bold)는 쓰지 않고 굵기별 글꼴 애셋을 쓴다.</summary>
    public enum UiFontWeight
    {
        Regular = 0,
        SemiBold = 1,
        Bold = 2,
    }

    /// <summary>토큰 이름 상수. 코드에서 토큰을 바꿀 때 문자열 대신 쓴다.</summary>
    public static class UiTokens
    {
        // 색 (v0.12 USS의 --color-* 에서 -- 만 뺀 이름)
        public const string ColorSurface = "color-surface";
        public const string ColorText = "color-text";
        public const string ColorTextDim = "color-text-dim";
        public const string ColorLight = "color-light";
        public const string ColorFocus = "color-focus";
        public const string ColorPrimary = "color-primary";
        public const string ColorWarning = "color-warning";
        public const string ColorDanger = "color-danger";
        public const string ColorEnemy = "color-enemy";
        public const string ColorPure = "color-pure";
        public const string ColorAlly = "color-ally";
        public const string ColorStamina = "color-stamina";
        public const string ColorSand = "color-sand";
        public const string ColorTrack = "color-track";
        public const string ColorDisabled = "color-disabled";
        public const string ColorBg = "color-bg";
        public const string ColorBackdrop = "color-backdrop";
        public const string ColorSurfaceRaised = "color-surface-raised";
        public const string ColorBorder = "color-border";
        public const string ColorPrimaryText = "color-primary-text";

        // 글자 크기
        public const string FsCaption = "fs-caption";
        public const string FsBody = "fs-body";
        public const string FsButton = "fs-button";
        public const string FsHeading = "fs-heading";
        public const string FsHeadingLg = "fs-heading-lg";
        public const string FsHud = "fs-hud";

        // 글꼴
        public const string FontRegular = "font-regular";
        public const string FontSemiBold = "font-semibold";
        public const string FontBold = "font-bold";

        /// <summary>색 토큰 20개(테마 애셋에 모두 있어야 한다).</summary>
        public static readonly string[] AllColors =
        {
            ColorSurface, ColorText, ColorTextDim, ColorLight, ColorFocus, ColorPrimary, ColorWarning, ColorDanger, ColorEnemy, ColorPure,
            ColorAlly, ColorStamina, ColorSand, ColorTrack, ColorDisabled, ColorBg, ColorBackdrop, ColorSurfaceRaised, ColorBorder, ColorPrimaryText,
        };

        /// <summary>글자 크기 토큰 6개.</summary>
        public static readonly string[] AllSizes = { FsCaption, FsBody, FsButton, FsHeading, FsHeadingLg, FsHud };

        /// <summary>글꼴 토큰 3개.</summary>
        public static readonly string[] AllFonts = { FontRegular, FontSemiBold, FontBold };

        /// <summary>굵기에 맞는 글꼴 토큰 이름.</summary>
        public static string FontTokenFor(UiFontWeight weight)
        {
            switch (weight)
            {
                case UiFontWeight.SemiBold: return FontSemiBold;
                case UiFontWeight.Bold: return FontBold;
                default: return FontRegular;
            }
        }
    }

    /// <summary>토큰 종류(인스펙터 드롭다운이 목록을 고를 때 쓴다).</summary>
    public enum UiTokenKind
    {
        Color = 0,
        Size = 1,
    }

    /// <summary>문자열 필드가 테마 토큰 이름임을 표시한다. 에디터에서 드롭다운으로 그려진다.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class UiTokenAttribute : PropertyAttribute
    {
        public UiTokenKind Kind { get; }

        public UiTokenAttribute(UiTokenKind kind)
        {
            Kind = kind;
        }
    }

    /// <summary>UI 테마 애셋.</summary>
    [CreateAssetMenu(fileName = "UiTheme", menuName = "Game/UI Theme")]
    public sealed class UiTheme : ScriptableObject
    {
        /// <summary>색 토큰 하나.</summary>
        [Serializable]
        public struct ColorToken
        {
            public string Name;
            public Color Color;
        }

        /// <summary>글자 크기 토큰 하나(보통 단계 기준값, 1080p 픽셀).</summary>
        [Serializable]
        public struct SizeToken
        {
            public string Name;
            public int BasePixels;
        }

        /// <summary>글꼴 토큰 하나.</summary>
        [Serializable]
        public struct FontToken
        {
            public string Name;
            public TMP_FontAsset Font;
        }

        /// <summary>대비 검사 짝: 글자 토큰 ↔ 바탕 토큰 ↔ 최소 대비(4.5 또는 3.0).</summary>
        [Serializable]
        public struct ContrastPair
        {
            public string TextToken;
            public string BackgroundToken;
            public float MinimumRatio;
        }

        /// <summary>글자 크기 반올림 오차를 없애려고 더하는 아주 작은 값.</summary>
        private const double RoundingEpsilon = 1e-6;

        [SerializeField] private List<ColorToken> _colors = new List<ColorToken>();
        [SerializeField] private List<SizeToken> _sizes = new List<SizeToken>();
        [Tooltip("단계 배율: 작게, 보통, 크게 순서")]
        [SerializeField] private float[] _scaleMultipliers = { 0.9f, 1f, 1.25f };
        [SerializeField] private List<FontToken> _fonts = new List<FontToken>();
        [SerializeField] private List<ContrastPair> _contrastPairs = new List<ContrastPair>();

        private Dictionary<string, Color> _colorMap;
        private Dictionary<string, int> _sizeMap;
        private Dictionary<string, TMP_FontAsset> _fontMap;

        public IReadOnlyList<ColorToken> Colors => _colors;
        public IReadOnlyList<SizeToken> Sizes => _sizes;
        public IReadOnlyList<FontToken> Fonts => _fonts;
        public IReadOnlyList<ContrastPair> ContrastPairs => _contrastPairs;
        public IReadOnlyList<float> ScaleMultipliers => _scaleMultipliers;

        /// <summary>인스펙터에서 값을 고치면 이름 → 값 사전을 다시 만든다.</summary>
        private void OnValidate()
        {
            _colorMap = null;
            _sizeMap = null;
            _fontMap = null;
        }

        private void OnEnable()
        {
            OnValidate();
        }

        /// <summary>색 토큰의 값을 찾는다.</summary>
        public bool TryGetColor(string token, out Color color)
        {
            if (_colorMap == null)
            {
                _colorMap = new Dictionary<string, Color>(_colors.Count);
                foreach (ColorToken entry in _colors)
                {
                    _colorMap[entry.Name] = entry.Color;
                }
            }
            return _colorMap.TryGetValue(token ?? string.Empty, out color);
        }

        /// <summary>
        /// 글자 크기 토큰의 현재 단계 크기(픽셀)를 돌려준다. 토큰이 없으면 0을 돌려주고 호출자가 오류를 알린다.
        /// </summary>
        public int GetFontSize(string token, UiTextScale scale)
        {
            if (_sizeMap == null)
            {
                _sizeMap = new Dictionary<string, int>(_sizes.Count);
                foreach (SizeToken entry in _sizes)
                {
                    _sizeMap[entry.Name] = entry.BasePixels;
                }
            }
            if (!_sizeMap.TryGetValue(token ?? string.Empty, out int basePixels))
            {
                return 0;
            }
            return ComputeFontSize(basePixels, GetMultiplier(scale));
        }

        /// <summary>단계 배율을 돌려준다(배열이 짧으면 1).</summary>
        public float GetMultiplier(UiTextScale scale)
        {
            int index = (int)scale;
            return _scaleMultipliers != null && index >= 0 && index < _scaleMultipliers.Length ? _scaleMultipliers[index] : 1f;
        }

        /// <summary>글꼴 토큰의 글꼴 애셋을 찾는다(없으면 null).</summary>
        public TMP_FontAsset GetFont(string token)
        {
            if (_fontMap == null)
            {
                _fontMap = new Dictionary<string, TMP_FontAsset>(_fonts.Count);
                foreach (FontToken entry in _fonts)
                {
                    _fontMap[entry.Name] = entry.Font;
                }
            }
            _fontMap.TryGetValue(token ?? string.Empty, out TMP_FontAsset font);
            return font;
        }

        /// <summary>
        /// 실제 글자 크기 = round(기준값 × 배율). 소수 .5는 올림. v0.12 Base.uss의 단계별 값과 같다.
        /// </summary>
        public static int ComputeFontSize(int basePixels, float multiplier)
        {
            return (int)Math.Round(basePixels * (double)multiplier + RoundingEpsilon, MidpointRounding.AwayFromZero);
        }

        /// <summary>WCAG 상대 휘도(0~1). 알파는 무시한다.</summary>
        public static double RelativeLuminance(Color color)
        {
            return 0.2126 * Linearize(color.r) + 0.7152 * Linearize(color.g) + 0.0722 * Linearize(color.b);
        }

        /// <summary>두 색의 WCAG 대비(1~21).</summary>
        public static double ContrastRatio(Color a, Color b)
        {
            double la = RelativeLuminance(a);
            double lb = RelativeLuminance(b);
            double lighter = Math.Max(la, lb);
            double darker = Math.Min(la, lb);
            return (lighter + 0.05) / (darker + 0.05);
        }

        private static double Linearize(float channel)
        {
            double c = channel;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        /// <summary>에디터용: 토큰 값을 통째로 채운다(UiThemeBuilder가 처음 만들 때 쓴다).</summary>
        public void SetAll(List<ColorToken> colors, List<SizeToken> sizes, float[] multipliers, List<FontToken> fonts, List<ContrastPair> pairs)
        {
            _colors = colors;
            _sizes = sizes;
            _scaleMultipliers = multipliers;
            _fonts = fonts;
            _contrastPairs = pairs;
            OnValidate();
        }
    }
}

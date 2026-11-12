// =============================================================
// 파일: UiThemeBuilder.cs
// 역할: UI 테마 애셋(UI/Theme/UiTheme.asset)을 「없을 때만」 처음 한 번 만들고, 있으면 토큰이 빠졌는지 검사만 한다.
//       그 밖에 인스펙터 토큰 드롭다운과 메뉴 Game/UI/Apply Theme To Prefabs 를 제공한다.
// 레이어: infrastructure (Game.Editor, 편집기 전용)
// 관련: NFR-056, FR-129, FR-130, DES-091, DES-122 / SDD 3.7.10 (1)(2), 3.8.2 생성 파이프라인 5, D-27
//
// 만든 뒤에는 애셋이 원본이다. 값은 인스펙터에서 고치고, 이 코드로 다시 덮어쓰지 않는다.
// 색 값은 SDD 3.7.10 (1) 표(= v0.12 Base.uss 의 :root 값), 글자 크기는 (2) 표의 보통 단계 기준값이다.
// =============================================================
using System;
using System.Collections.Generic;
using System.IO;
using Game.Adapters.Presentation.UI.Common;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>테마 애셋 생성·검사.</summary>
    public static class UiThemeBuilder
    {
        /// <summary>테마 애셋 경로.</summary>
        public const string ThemePath = "Assets/_Game/UI/Theme/UiTheme.asset";

        /// <summary>없으면 만들고, 있으면 토큰 누락만 검사한다. 빠진 것이 있으면 예외로 Run All 을 실패시킨다.</summary>
        [MenuItem("Game/Setup/Ensure UI Theme")]
        public static void Ensure()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null)
            {
                Create();
                return;
            }
            List<string> missing = FindMissingTokens(theme);
            if (missing.Count > 0)
            {
                throw new InvalidOperationException("UiTheme.asset 에 빠진 토큰이 있습니다(인스펙터에서 채우세요): " + string.Join(", ", missing));
            }
            Debug.Log("[Setup] UiTheme.asset 검사 통과(덮어쓰지 않음).");
        }

        /// <summary>테마에 빠진 토큰 이름 목록. 색 20 + 글자 크기 6 + 글꼴 3 + 단계 배율 3.</summary>
        public static List<string> FindMissingTokens(UiTheme theme)
        {
            var missing = new List<string>();
            foreach (string name in UiTokens.AllColors)
            {
                if (!theme.TryGetColor(name, out _)) { missing.Add(name); }
            }
            foreach (string name in UiTokens.AllSizes)
            {
                if (theme.GetFontSize(name, UiTextScale.Normal) <= 0) { missing.Add(name); }
            }
            bool hasFontToken;
            foreach (string name in UiTokens.AllFonts)
            {
                hasFontToken = false;
                foreach (UiTheme.FontToken token in theme.Fonts)
                {
                    if (token.Name == name) { hasFontToken = true; break; }
                }
                if (!hasFontToken) { missing.Add(name); }
            }
            if (theme.ScaleMultipliers.Count < 3) { missing.Add("scale-multipliers"); }
            return missing;
        }

        private static void Create()
        {
            DataAssetBuilder.EnsureFolder("Assets/_Game/UI");
            DataAssetBuilder.EnsureFolder("Assets/_Game/UI/Theme");

            var theme = ScriptableObject.CreateInstance<UiTheme>();

            // 색: SDD 3.7.10 (1) 표
            var colors = new List<UiTheme.ColorToken>
            {
                Color(UiTokens.ColorSurface, 0x14, 0x15, 0x1A),
                Color(UiTokens.ColorText, 0xE8, 0xE4, 0xDC),
                Color(UiTokens.ColorTextDim, 0xA9, 0xA5, 0x9C),
                Color(UiTokens.ColorLight, 0xF2, 0xA5, 0x3A),
                Color(UiTokens.ColorFocus, 0xF2, 0xA5, 0x3A),
                Color(UiTokens.ColorPrimary, 0xF2, 0xA5, 0x3A),
                Color(UiTokens.ColorWarning, 0xF2, 0xA5, 0x3A),
                Color(UiTokens.ColorDanger, 0xE5, 0x59, 0x4F),
                Color(UiTokens.ColorEnemy, 0xE5, 0x59, 0x4F),
                Color(UiTokens.ColorPure, 0x6F, 0xD1, 0xB0),
                Color(UiTokens.ColorAlly, 0x6D, 0xB8, 0xF0),
                Color(UiTokens.ColorStamina, 0xA8, 0xD4, 0x5A),
                Color(UiTokens.ColorSand, 0xD8, 0xCB, 0xB0),
                Color(UiTokens.ColorTrack, 0x2C, 0x2E, 0x36),
                Color(UiTokens.ColorDisabled, 0x6B, 0x6A, 0x66),
                Color(UiTokens.ColorBg, 0x0E, 0x10, 0x14),
                new UiTheme.ColorToken { Name = UiTokens.ColorBackdrop, Color = new Color(0f, 0f, 0f, 0.88f) },
                Color(UiTokens.ColorSurfaceRaised, 0x2C, 0x2E, 0x36),
                Color(UiTokens.ColorBorder, 0x58, 0x5C, 0x68),
                Color(UiTokens.ColorPrimaryText, 0x14, 0x15, 0x1A),
            };

            // 글자 크기: (2) 표의 보통 단계 기준값
            var sizes = new List<UiTheme.SizeToken>
            {
                new UiTheme.SizeToken { Name = UiTokens.FsCaption, BasePixels = 20 },
                new UiTheme.SizeToken { Name = UiTokens.FsBody, BasePixels = 24 },
                new UiTheme.SizeToken { Name = UiTokens.FsButton, BasePixels = 24 },
                new UiTheme.SizeToken { Name = UiTokens.FsHeading, BasePixels = 30 },
                new UiTheme.SizeToken { Name = UiTokens.FsHeadingLg, BasePixels = 40 },
                new UiTheme.SizeToken { Name = UiTokens.FsHud, BasePixels = 22 },
            };

            // 글꼴: Pretendard 가 들어오기 전에는 TMP 기본 글꼴을 세 토큰에 같이 쓴다. Pretendard 를 받으면 이 세 칸만 바꾼다.
            TMP_FontAsset defaultFont = TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;
            if (defaultFont == null)
            {
                Debug.LogWarning("[Setup] TMP 기본 글꼴을 찾지 못해 글꼴 토큰을 비워 둡니다(TMP Essential Resources 를 먼저 가져오세요).");
            }
            var fonts = new List<UiTheme.FontToken>
            {
                new UiTheme.FontToken { Name = UiTokens.FontRegular, Font = defaultFont },
                new UiTheme.FontToken { Name = UiTokens.FontSemiBold, Font = defaultFont },
                new UiTheme.FontToken { Name = UiTokens.FontBold, Font = defaultFont },
            };

            // 대비 검사 짝: SDD 3.7.10 (1) 「대비 검사 짝」
            var pairs = new List<UiTheme.ContrastPair>();
            string[] texts =
            {
                UiTokens.ColorText, UiTokens.ColorTextDim, UiTokens.ColorLight, UiTokens.ColorDanger,
                UiTokens.ColorPure, UiTokens.ColorAlly, UiTokens.ColorStamina, UiTokens.ColorSand,
            };
            foreach (string text in texts)
            {
                pairs.Add(Pair(text, UiTokens.ColorSurface, 4.5f));
                pairs.Add(Pair(text, UiTokens.ColorBg, 4.5f));
            }
            pairs.Add(Pair(UiTokens.ColorText, UiTokens.ColorSurfaceRaised, 4.5f));
            pairs.Add(Pair(UiTokens.ColorPrimaryText, UiTokens.ColorPrimary, 4.5f));
            pairs.Add(Pair(UiTokens.ColorDisabled, UiTokens.ColorSurface, 3.0f)); // 비활성 예외

            theme.SetAll(colors, sizes, new[] { 0.9f, 1f, 1.25f }, fonts, pairs);
            AssetDatabase.CreateAsset(theme, ThemePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] UiTheme.asset 을 만들었습니다: " + ThemePath + " (이후에는 애셋이 원본입니다).");
        }

        private static UiTheme.ColorToken Color(string name, byte r, byte g, byte b)
        {
            return new UiTheme.ColorToken { Name = name, Color = new Color32(r, g, b, 255) };
        }

        private static UiTheme.ContrastPair Pair(string text, string background, float ratio)
        {
            return new UiTheme.ContrastPair { TextToken = text, BackgroundToken = background, MinimumRatio = ratio };
        }
    }

    /// <summary>토큰 이름 필드를 테마 애셋의 이름 목록 드롭다운으로 그린다.</summary>
    [CustomPropertyDrawer(typeof(UiTokenAttribute))]
    public sealed class UiTokenDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(UiThemeBuilder.ThemePath);
            if (theme == null)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var attribute = (UiTokenAttribute)this.attribute;
            var names = new List<string>();
            if (attribute.Kind == UiTokenKind.Color)
            {
                names.Add(string.Empty);
                foreach (UiTheme.ColorToken token in theme.Colors) { names.Add(token.Name); }
            }
            else
            {
                foreach (UiTheme.SizeToken token in theme.Sizes) { names.Add(token.Name); }
            }

            int current = Mathf.Max(0, names.IndexOf(property.stringValue));
            var labels = names.ConvertAll(n => string.IsNullOrEmpty(n) ? "(없음)" : n);
            int selected = EditorGUI.Popup(position, label.text, current, labels.ToArray());
            if (selected != current || !names.Contains(property.stringValue))
            {
                property.stringValue = names[selected];
            }
        }
    }

    /// <summary>테마 값을 바꾼 뒤 모든 UI 프리팹의 Themed* 가 계산 결과를 다시 써 넣게 하는 메뉴.</summary>
    public static class UiThemeMenu
    {
        private const string PrefabFolder = "Assets/_Game/UI/Prefabs";

        /// <summary>구조는 건드리지 않고 색·크기 값만 다시 쓴다.</summary>
        [MenuItem("Game/UI/Apply Theme To Prefabs")]
        public static void ApplyThemeToPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                Debug.Log("[UI] UI 프리팹 폴더가 없습니다.");
                return;
            }
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (ThemedGraphic graphic in root.GetComponentsInChildren<ThemedGraphic>(true)) { graphic.Apply(); }
                    foreach (ThemedText text in root.GetComponentsInChildren<ThemedText>(true)) { text.Apply(); }
                    foreach (ThemedSelectable selectable in root.GetComponentsInChildren<ThemedSelectable>(true)) { selectable.Redraw(); }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[UI] 테마를 " + changed + "개 프리팹에 다시 적용했습니다.");
        }
    }
}

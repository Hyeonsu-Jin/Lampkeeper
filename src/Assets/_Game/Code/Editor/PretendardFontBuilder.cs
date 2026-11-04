// =============================================================
// 파일: PretendardFontBuilder.cs
// 역할: Pretendard 3굵기의 TMP 글꼴 애셋(정적 SDF 아틀라스 + 동적 대체)을 "없을 때만" 만들고 연결한다.
// 레이어: editor (Game.Editor)
// 관련: NFR-044, R-37 / SDD 3.7.10 (8), STORYBOARD 2.2
//
// 글자 목록은 Assets/_Game/UI/Fonts/pretendard-static-chars.txt (ASCII + KS X 1001 한글 2,350자 + 게임 문장 글자).
// 이미 있는 글꼴 애셋은 덮어쓰지 않는다. 원본 TTF 는 수정하지 않는다(OFL 예약 이름).
// =============================================================
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Game.Editor
{
    /// <summary>Pretendard TMP 글꼴 애셋 생성기(없을 때만).</summary>
    public static class PretendardFontBuilder
    {
        private const string FontFolder = "Assets/_Game/UI/Fonts";
        private const string CharListPath = FontFolder + "/pretendard-static-chars.txt";
        private const int SamplingPointSize = 48;
        private const int AtlasPadding = 5;
        private const int StaticAtlasSize = 4096;
        private const int DynamicAtlasSize = 1024;
        private static readonly string[] Weights = { "Regular", "SemiBold", "Bold" };

        /// <summary>굵기별 정적 SDF 애셋 경로.</summary>
        public static string StaticPath(string weight) { return FontFolder + "/Pretendard-" + weight + " SDF.asset"; }

        /// <summary>굵기별 동적 대체 애셋 경로.</summary>
        public static string DynamicPath(string weight) { return FontFolder + "/Pretendard-" + weight + " Dynamic SDF.asset"; }

        [MenuItem("Tools/Game/UI/Build Pretendard Font Assets (if missing)")]
        public static void BuildIfMissing()
        {
            string chars = File.ReadAllText(CharListPath);
            var unicodes = new List<uint>();
            foreach (char c in chars)
            {
                if (c > ' ') { unicodes.Add(c); }
            }
            foreach (string weight in Weights)
            {
                Font ttf = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "/Pretendard-" + weight + ".ttf");
                if (ttf == null) { Debug.LogError("[UI] Pretendard-" + weight + ".ttf 를 찾지 못했습니다."); continue; }

                // 동적 대체 글꼴 먼저(정적의 대체 목록이 가리킴)
                TMP_FontAsset dynamicAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DynamicPath(weight));
                if (dynamicAsset == null)
                {
                    dynamicAsset = TMP_FontAsset.CreateFontAsset(ttf, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
                        DynamicAtlasSize, DynamicAtlasSize, AtlasPopulationMode.Dynamic, true);
                    Save(dynamicAsset, DynamicPath(weight), "Pretendard-" + weight + " Dynamic SDF");
                }

                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StaticPath(weight)) == null)
                {
                    TMP_FontAsset st = TMP_FontAsset.CreateFontAsset(ttf, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
                        StaticAtlasSize, StaticAtlasSize, AtlasPopulationMode.Dynamic, false);
                    st.TryAddCharacters(unicodes.ToArray(), out uint[] missing);
                    st.atlasPopulationMode = AtlasPopulationMode.Static;
                    st.fallbackFontAssetTable = new List<TMP_FontAsset> { dynamicAsset };
                    Save(st, StaticPath(weight), "Pretendard-" + weight + " SDF");
                    Debug.Log("[UI] " + weight + " 정적 아틀라스: 글자 " + st.characterTable.Count + "개, 아틀라스 " + StaticAtlasSize
                        + "x" + StaticAtlasSize + ", 못 넣은 글자 " + (missing == null ? 0 : missing.Length));
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static void Save(TMP_FontAsset asset, string path, string name)
        {
            asset.name = name;
            AssetDatabase.CreateAsset(asset, path);
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            Texture2D[] atlases = asset.atlasTextures;
            for (int i = 0; i < atlases.Length; i++)
            {
                atlases[i].name = name + " Atlas" + (i == 0 ? "" : " " + i);
                AssetDatabase.AddObjectToAsset(atlases[i], asset);
            }
            EditorUtility.SetDirty(asset);
        }

        /// <summary>테마 글꼴 3칸과 TMP Settings 기본·대체 글꼴을 연결한다(한 번만 쓰는 연결 작업, 값만 바꿈).</summary>
        [MenuItem("Tools/Game/UI/Link Pretendard To Theme And TMP Settings")]
        public static void Link()
        {
            var theme = AssetDatabase.LoadAssetAtPath<Game.Adapters.Presentation.UI.Common.UiTheme>("Assets/_Game/UI/Theme/UiTheme.asset");
            var so = new SerializedObject(theme);
            SerializedProperty fonts = so.FindProperty("_fonts");
            string[] tokenNames = { "font-regular", "font-semibold", "font-bold" };
            for (int i = 0; i < fonts.arraySize; i++)
            {
                SerializedProperty e = fonts.GetArrayElementAtIndex(i);
                int w = System.Array.IndexOf(tokenNames, e.FindPropertyRelative("Name").stringValue);
                if (w >= 0)
                {
                    e.FindPropertyRelative("Font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StaticPath(Weights[w]));
                }
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(theme);

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
            var ss = new SerializedObject(settings);
            ss.FindProperty("m_defaultFontAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StaticPath("Regular"));
            SerializedProperty fb = ss.FindProperty("m_fallbackFontAssets");
            fb.arraySize = 1;
            fb.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DynamicPath("Regular"));
            ss.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}

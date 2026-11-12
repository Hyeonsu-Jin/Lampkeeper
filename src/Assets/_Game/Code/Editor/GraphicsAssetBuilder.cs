// =============================================================
// 파일: GraphicsAssetBuilder.cs
// 역할: 그래픽 품질 단계 4개(낮음/보통/높음/최고)를 만든다: HDRP 애셋 4개(기존 3개를 복사해 4개로), QualitySettings 4단계에
//       연결, 업스케일링에 필요한 동적 해상도 설정 켜기, 기본 렌더 파이프라인 지정. 그래픽 프리셋 애셋도 함께 만든다.
//       여러 번 실행해도 같은 결과가 나온다(멱등).
// 레이어: infrastructure (Game.Editor, 편집기 전용)
// 관련: FR-120, FR-121, NFR-028, DES-109, DES-112 / SDD 3.12.7, M1-06
//
// HDRP 형식은 쓰지 않는다(Game.Editor는 HDRP 어셈블리를 참조할 수 없음, 규칙 7). 애셋은 SerializedObject의 속성 경로로 바꾼다.
// 속성 경로를 찾지 못하면 경고를 남기고 건너뛴다(HDRP 버전이 바뀌었을 때 알아차리기 위해).
// =============================================================
using System.Collections.Generic;
using Game.Application.Graphics;
using Game.Infra.Config;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>품질 단계·HDRP 애셋 생성기.</summary>
    public static class GraphicsAssetBuilder
    {
        private const string SettingsFolder = "Assets/_Game/Settings";
        private const string QualitySettingsPath = "ProjectSettings/QualitySettings.asset";

        /// <summary>업스케일러 우선순위 목록의 이름(HDRP의 AdvancedUpscalers 열거형 이름).</summary>
        private static readonly string[] UpscalerNames = { "DLSS", "FSR2" };

        private const string PropRoot = "m_RenderPipelineSettings.";

        /// <summary>단계 이름과 복사 원본 HDRP 애셋. 낮음은 가벼운 것, 보통은 중간, 높음·최고는 고화질 원본에서 시작한다.</summary>
        private static readonly (GraphicsPresetId Id, string LevelName, string Source)[] Levels =
        {
            (GraphicsPresetId.Low, "Low", SettingsFolder + "/HDRP Performant.asset"),
            (GraphicsPresetId.Medium, "Medium", SettingsFolder + "/HDRP Balanced.asset"),
            (GraphicsPresetId.High, "High", SettingsFolder + "/HDRP High Fidelity.asset"),
            (GraphicsPresetId.Ultra, "Ultra", SettingsFolder + "/HDRP High Fidelity.asset"),
        };

        /// <summary>그래픽 프리셋 애셋과 품질 단계 4개를 만들고 연결한다.</summary>
        [MenuItem("Game/Graphics/Create Quality Levels And Presets")]
        public static void CreateAll()
        {
            DataAssetBuilder.CreateDefaults(); // 프리셋 애셋(Data/Graphics)이 없으면 기본값으로 만든다.
            var presets = AssetDatabase.LoadAssetAtPath<GraphicsPresetConfig>(DataAssetBuilder.GraphicsPath);
            var presetById = new Dictionary<GraphicsPresetId, GraphicsPresetData>();
            foreach (GraphicsPresetData p in presets.Presets)
            {
                presetById[p.Id] = p;
            }

            var pipelineAssets = new List<Object>();
            foreach (var level in Levels)
            {
                string path = SettingsFolder + "/HDRP " + level.LevelName + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Object>(path) == null)
                {
                    if (!AssetDatabase.CopyAsset(level.Source, path))
                    {
                        Debug.LogError("[Graphics] HDRP 애셋 복사 실패: " + level.Source + " -> " + path);
                        return;
                    }
                }
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                ConfigureHdrpAsset(asset, presetById[level.Id]);
                pipelineAssets.Add(asset);
            }
            AssetDatabase.SaveAssets();

            ConfigureQualitySettings(pipelineAssets);

            // 품질 단계에 연결되지 않은 경우를 위한 기본 렌더 파이프라인 = 기준 프리셋 「높음」
            GraphicsSettings.defaultRenderPipeline = (RenderPipelineAsset)pipelineAssets[(int)GraphicsPresetId.High];
            AssetDatabase.SaveAssets();
            Debug.Log("[Graphics] 품질 단계 4개와 그래픽 프리셋 애셋을 만들었습니다(낮음/보통/높음/최고).");
        }

        /// <summary>HDRP 애셋 하나에 프리셋 값과 업스케일링 설정을 쓴다.</summary>
        private static void ConfigureHdrpAsset(Object asset, GraphicsPresetData preset)
        {
            var so = new SerializedObject(asset);
            SetBool(so, PropRoot + "supportSSAO", preset.SsaoEnabled);
            SetBool(so, PropRoot + "supportSSR", preset.SsrEnabled);
            SetBool(so, PropRoot + "supportVolumetrics", preset.VolumetricFogQuality > 0);

            // LOD 편향: HDRP는 카메라를 그릴 때마다 HDRP 애셋의 lodBias 표(낮음/보통/높음 3칸)로 QualitySettings.lodBias를 덮어쓴다.
            // 그래서 런타임에 QualitySettings.lodBias를 바꿔도 소용이 없고, 프리셋 값을 세 칸 모두에 구워 넣는다.
            SetFloatArray(so, PropRoot + "lodBias.m_Values", preset.LodBias);

            // 업스케일링(DLSS/FSR2)은 동적 해상도 기능 위에서 동작한다. 카메라별 허용·품질은 런타임이 정한다.
            string dr = PropRoot + "dynamicResolutionSettings.";
            SetBool(so, dr + "enabled", true);
            SetBool(so, dr + "DLSSUseOptimalSettings", true);
            SetBool(so, dr + "FSR2UseOptimalSettings", true);
            SetStringArray(so, dr + "advancedUpscalerNames", UpscalerNames);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        /// <summary>QualitySettings를 4단계로 맞추고 각 단계에 HDRP 애셋을 연결한다.</summary>
        private static void ConfigureQualitySettings(List<Object> pipelineAssets)
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(QualitySettingsPath);
            if (all == null || all.Length == 0)
            {
                Debug.LogError("[Graphics] " + QualitySettingsPath + " 를 읽지 못했습니다.");
                return;
            }
            var so = new SerializedObject(all[0]);
            SerializedProperty levels = so.FindProperty("m_QualitySettings");
            if (levels == null || levels.arraySize == 0)
            {
                Debug.LogError("[Graphics] 품질 단계 목록(m_QualitySettings)을 찾지 못했습니다.");
                return;
            }

            // 첫 단계를 바탕으로 4개가 될 때까지 복제하고, 많으면 줄인다.
            while (levels.arraySize < Levels.Length)
            {
                levels.InsertArrayElementAtIndex(levels.arraySize - 1);
            }
            while (levels.arraySize > Levels.Length)
            {
                levels.DeleteArrayElementAtIndex(levels.arraySize - 1);
            }

            for (int i = 0; i < Levels.Length; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("name").stringValue = Levels[i].LevelName;
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipelineAssets[i];
            }

            // 기본 품질 단계 = 기준 프리셋 「높음」
            SerializedProperty current = so.FindProperty("m_CurrentQuality");
            if (current != null) current.intValue = (int)GraphicsPresetId.High;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- 속성 쓰기 도우미 ----------

        private static void SetBool(SerializedObject so, string path, bool value)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null)
            {
                Debug.LogWarning("[Graphics] HDRP 속성을 찾지 못해 건너뜁니다: " + path);
                return;
            }
            p.boolValue = value;
        }

        private static void SetFloatArray(SerializedObject so, string path, float value)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null || !p.isArray)
            {
                Debug.LogWarning("[Graphics] HDRP 속성을 찾지 못해 건너뜁니다: " + path);
                return;
            }
            for (int i = 0; i < p.arraySize; i++)
            {
                p.GetArrayElementAtIndex(i).floatValue = value;
            }
        }

        private static void SetStringArray(SerializedObject so, string path, string[] values)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null || !p.isArray)
            {
                Debug.LogWarning("[Graphics] HDRP 속성을 찾지 못해 건너뜁니다: " + path);
                return;
            }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                p.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }
    }
}

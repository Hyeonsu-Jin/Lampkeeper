// =============================================================
// 파일: GraphicsDataTests.cs
// 역할: M1-06 그래픽 기반 산출물의 에디터 시험. 프리셋 애셋, 품질 단계 4개, HDRP 애셋의 업스케일링 설정,
//       업스케일러 품질 숫자와 엔진 열거형의 일치, Bench 장면이 빌드 설정에 들어가지 않는지 확인한다.
// 레이어: test (EditMode, Game.Tests.Infrastructure)
// 관련: FR-120, FR-121, NFR-028, DES-109, DES-114 / SDD 3.12.7, M1-06
// =============================================================
using System;
using System.Linq;
using Game.Application.Graphics;
using Game.Editor;
using Game.Infra.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Tests.Infrastructure
{
    public class GraphicsDataTests
    {
        private static GraphicsPresetConfig LoadPresets()
        {
            var config = AssetDatabase.LoadAssetAtPath<GraphicsPresetConfig>(DataAssetBuilder.GraphicsPath);
            Assert.IsNotNull(config, "그래픽 프리셋 애셋이 없습니다: " + DataAssetBuilder.GraphicsPath);
            return config;
        }

        [Test]
        public void PresetAsset_IsValid_AndBenchmarkIsHigh()
        {
            GraphicsPresetCatalog catalog = LoadPresets().ToCatalog();
            Assert.AreEqual(GraphicsPresetId.High, catalog.Benchmark.Id);
        }

        [Test]
        public void PresetAsset_ToCatalog_CopiesData_SoRuntimeCannotChangeAsset()
        {
            GraphicsPresetConfig config = LoadPresets();
            GraphicsPresetCatalog catalog = config.ToCatalog();
            float original = config.Presets[0].ShadowDistanceMeters;
            catalog.Presets[0].ShadowDistanceMeters = original + 1000f;
            Assert.AreEqual(original, config.Presets[0].ShadowDistanceMeters);
        }

        [Test]
        public void PresetAsset_Invalid_Throws()
        {
            var config = ScriptableObject.CreateInstance<GraphicsPresetConfig>();
            try
            {
                config.Presets.RemoveAt(3);
                Assert.Throws<GraphicsPresetException>(() => config.ToCatalog());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void GameDataConfig_LinksGraphicsPresets()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameDataConfig>(DataAssetBuilder.RootPath);
            Assert.IsNotNull(root.Graphics, "GameDataConfig.Graphics가 연결되어 있어야 합니다.");
        }

        [Test]
        public void QualitySettings_HasFourLevels_EachWithOwnHdrpAsset()
        {
            Assert.AreEqual(4, QualitySettings.names.Length, "품질 단계 4개");
            CollectionAssert.AreEqual(new[] { "Low", "Medium", "High", "Ultra" }, QualitySettings.names);

            var assets = Enumerable.Range(0, 4).Select(QualitySettings.GetRenderPipelineAssetAt).ToList();
            CollectionAssert.AllItemsAreNotNull(assets);
            CollectionAssert.AllItemsAreUnique(assets, "단계마다 서로 다른 HDRP 애셋");
            Assert.AreEqual(assets[(int)GraphicsPresetId.High], GraphicsSettings.defaultRenderPipeline, "기본 파이프라인 = 「높음」");
        }

        [Test]
        public void PresetQualityLevelIndexes_PointAtExistingLevels()
        {
            foreach (GraphicsPresetData p in LoadPresets().ToCatalog().Presets)
            {
                Assert.That(p.QualityLevelIndex, Is.InRange(0, QualitySettings.names.Length - 1), p.Id.ToString());
            }
        }

        [Test]
        public void HdrpAssets_EnableDynamicResolution_AndUpscalerList_AndFollowPresetValues()
        {
            GraphicsPresetCatalog catalog = LoadPresets().ToCatalog();
            foreach (GraphicsPresetData preset in catalog.Presets)
            {
                UnityEngine.Object asset = QualitySettings.GetRenderPipelineAssetAt(preset.QualityLevelIndex);
                var so = new SerializedObject(asset);
                const string root = "m_RenderPipelineSettings.";
                Assert.IsTrue(so.FindProperty(root + "dynamicResolutionSettings.enabled").boolValue, preset.Id + ": 동적 해상도");
                SerializedProperty names = so.FindProperty(root + "dynamicResolutionSettings.advancedUpscalerNames");
                var list = Enumerable.Range(0, names.arraySize).Select(i => names.GetArrayElementAtIndex(i).stringValue).ToList();
                CollectionAssert.Contains(list, "DLSS", preset.Id.ToString());
                CollectionAssert.Contains(list, "FSR2", preset.Id.ToString());
                SerializedProperty lod = so.FindProperty(root + "lodBias.m_Values");
                for (int i = 0; i < lod.arraySize; i++)
                {
                    Assert.AreEqual(preset.LodBias, lod.GetArrayElementAtIndex(i).floatValue, 1e-4f, preset.Id + ": LOD 편향 표 " + i);
                }
                Assert.AreEqual(preset.SsaoEnabled, so.FindProperty(root + "supportSSAO").boolValue, preset.Id + ": SSAO");
                Assert.AreEqual(preset.SsrEnabled, so.FindProperty(root + "supportSSR").boolValue, preset.Id + ": SSR");
                Assert.AreEqual(preset.VolumetricFogQuality > 0, so.FindProperty(root + "supportVolumetrics").boolValue, preset.Id + ": 볼류메트릭 안개");
            }
        }

        [Test]
        public void UpscalerQualityMap_MatchesEngineEnums_WhenModulesInstalled()
        {
            // DLSS·FSR2 내장 모듈이 켜져 있어야 열거형이 존재한다(manifest의 com.unity.modules.nvidia/amd).
            Type dlss = Type.GetType("UnityEngine.NVIDIA.DLSSQuality, UnityEngine.NVIDIAModule");
            Type fsr2 = Type.GetType("UnityEngine.AMD.FSR2Quality, UnityEngine.AMDModule");
            Assert.IsNotNull(dlss, "nvidia 모듈이 없습니다.");
            Assert.IsNotNull(fsr2, "amd 모듈이 없습니다.");

            Assert.AreEqual(UpscalerQualityMap.DlssValue(UpscalerQuality.Quality), Convert.ToUInt32(Enum.Parse(dlss, "MaximumQuality")));
            Assert.AreEqual(UpscalerQualityMap.DlssValue(UpscalerQuality.Balanced), Convert.ToUInt32(Enum.Parse(dlss, "Balanced")));
            Assert.AreEqual(UpscalerQualityMap.DlssValue(UpscalerQuality.Performance), Convert.ToUInt32(Enum.Parse(dlss, "MaximumPerformance")));
            Assert.AreEqual(UpscalerQualityMap.DlssValue(UpscalerQuality.UltraPerformance), Convert.ToUInt32(Enum.Parse(dlss, "UltraPerformance")));

            Assert.AreEqual(UpscalerQualityMap.Fsr2Value(UpscalerQuality.Quality), Convert.ToUInt32(Enum.Parse(fsr2, "Quality")));
            Assert.AreEqual(UpscalerQualityMap.Fsr2Value(UpscalerQuality.Balanced), Convert.ToUInt32(Enum.Parse(fsr2, "Balanced")));
            Assert.AreEqual(UpscalerQualityMap.Fsr2Value(UpscalerQuality.Performance), Convert.ToUInt32(Enum.Parse(fsr2, "Performance")));
            Assert.AreEqual(UpscalerQualityMap.Fsr2Value(UpscalerQuality.UltraPerformance), Convert.ToUInt32(Enum.Parse(fsr2, "UltraPerformance")));
        }

        [Test]
        public void BenchScene_ExistsButIsNotInBuildSettings()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(BenchSceneBuilder.BenchScenePath), "Bench 장면");
            Assert.IsFalse(EditorBuildSettings.scenes.Any(s => s.path == BenchSceneBuilder.BenchScenePath),
                "Bench는 개발 빌드를 만들 때만 BuildCommands가 넣습니다(출시 빌드 제외).");
        }
    }
}

// =============================================================
// 파일: GraphicsPresetTests.cs
// 역할: 그래픽 프리셋 데이터 검증, 기준 프리셋 「높음」 규칙, GPU별 업스케일러 선택, 품질 단계 숫자 변환의 단위 시험.
// 레이어: test (EditMode, Game.Tests.Application)
// 관련: FR-120, FR-121, NFR-033, DES-109 / SDD 3.12.7, C-52, M1-06
// =============================================================
using System.Collections.Generic;
using System.Linq;
using Game.Application.Graphics;
using NUnit.Framework;

namespace Game.Tests.Application
{
    public class GraphicsPresetTests
    {
        private static List<GraphicsPresetData> Defaults() => GraphicsPresetCatalog.CreateDefaults().ToList();

        [Test]
        public void Defaults_AreValid_FourPresets_BenchmarkIsHigh()
        {
            var catalog = new GraphicsPresetCatalog(Defaults());
            Assert.AreEqual(4, catalog.Presets.Count);
            Assert.AreEqual(GraphicsPresetId.High, catalog.Benchmark.Id, "기준 프리셋은 「높음」(C-52)");
            Assert.IsTrue(catalog.Benchmark.UpscalerOnByDefault);
            Assert.AreEqual(UpscalerQuality.Quality, catalog.Benchmark.UpscalerQualityDefault);
            Assert.AreEqual(1, catalog.Presets.Count(p => p.IsBenchmarkPreset), "기준 프리셋은 정확히 1개");
        }

        [Test]
        public void Defaults_GetsEachPresetByIdInOrder()
        {
            var catalog = new GraphicsPresetCatalog(Defaults().OrderByDescending(p => (int)p.Id)); // 순서가 뒤섞여도 번호로 정렬된다
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual((GraphicsPresetId)i, catalog.Get((GraphicsPresetId)i).Id);
                Assert.AreEqual(i, catalog.Get((GraphicsPresetId)i).QualityLevelIndex);
            }
        }

        [Test]
        public void Defaults_GetHeavierWithHigherPresets()
        {
            var catalog = new GraphicsPresetCatalog(Defaults());
            for (int i = 1; i < 4; i++)
            {
                GraphicsPresetData lower = catalog.Get((GraphicsPresetId)(i - 1));
                GraphicsPresetData higher = catalog.Get((GraphicsPresetId)i);
                Assert.GreaterOrEqual(higher.ShadowDistanceMeters, lower.ShadowDistanceMeters);
                Assert.GreaterOrEqual(higher.ShadowQualityTier, lower.ShadowQualityTier);
                Assert.GreaterOrEqual(higher.VolumetricFogQuality, lower.VolumetricFogQuality);
                Assert.GreaterOrEqual(higher.TerrainDetailDensity, lower.TerrainDetailDensity);
                Assert.GreaterOrEqual(higher.MaxShadowCastingLights, lower.MaxShadowCastingLights);
            }
        }

        [Test]
        public void Validate_RejectsWrongCount()
        {
            List<GraphicsPresetData> three = Defaults().Take(3).ToList();
            Assert.IsNotEmpty(GraphicsPresetCatalog.Validate(three, out _));
            Assert.Throws<GraphicsPresetException>(() => new GraphicsPresetCatalog(three));
            Assert.IsNotEmpty(GraphicsPresetCatalog.Validate(null, out _));
        }

        [Test]
        public void Validate_RejectsDuplicateIdAndDuplicateQualityLevel()
        {
            List<GraphicsPresetData> list = Defaults();
            list[1].Id = GraphicsPresetId.Low; // 번호 중복
            Assert.IsTrue(GraphicsPresetCatalog.Validate(list, out _).Any(m => m.Contains("중복")));

            list = Defaults();
            list[1].QualityLevelIndex = 0; // 품질 단계 번호 겹침
            Assert.IsTrue(GraphicsPresetCatalog.Validate(list, out _).Any(m => m.Contains("겹")));
        }

        [Test]
        public void Validate_RequiresExactlyOneBenchmark_AndItMustBeHighWithUpscalerQuality()
        {
            List<GraphicsPresetData> none = Defaults();
            none.ForEach(p => p.IsBenchmarkPreset = false);
            Assert.IsTrue(GraphicsPresetCatalog.Validate(none, out _).Any(m => m.Contains("기준 프리셋은 정확히 1개")));

            List<GraphicsPresetData> two = Defaults();
            two[3].IsBenchmarkPreset = true;
            Assert.IsNotEmpty(GraphicsPresetCatalog.Validate(two, out _));

            List<GraphicsPresetData> wrongOne = Defaults();
            wrongOne[2].IsBenchmarkPreset = false;
            wrongOne[3].IsBenchmarkPreset = true; // 최고가 기준 -> C-52 위반
            Assert.IsTrue(GraphicsPresetCatalog.Validate(wrongOne, out _).Any(m => m.Contains("C-52")));

            List<GraphicsPresetData> noUpscaler = Defaults();
            noUpscaler[2].UpscalerOnByDefault = false;
            Assert.IsTrue(GraphicsPresetCatalog.Validate(noUpscaler, out _).Any(m => m.Contains("C-52")));

            List<GraphicsPresetData> wrongQuality = Defaults();
            wrongQuality[2].UpscalerQualityDefault = UpscalerQuality.Performance;
            Assert.IsTrue(GraphicsPresetCatalog.Validate(wrongQuality, out _).Any(m => m.Contains("품질 모드")));
        }

        [Test]
        public void Validate_RejectsOutOfRangeValues()
        {
            Assert.IsNotEmpty(WithChange(p => p.ShadowDistanceMeters = 0f));
            Assert.IsNotEmpty(WithChange(p => p.ShadowQualityTier = 4));
            Assert.IsNotEmpty(WithChange(p => p.VolumetricFogQuality = -1));
            Assert.IsNotEmpty(WithChange(p => p.TerrainDetailDensity = 1.5f));
            Assert.IsNotEmpty(WithChange(p => p.LodBias = 0f));
            Assert.IsNotEmpty(WithChange(p => p.MaxShadowCastingLights = -1));
            Assert.IsNotEmpty(WithChange(p => p.DisplayKey = ""));
            Assert.IsNotEmpty(WithChange(p => p.QualityLevelIndex = -1));
        }

        private static List<string> WithChange(System.Action<GraphicsPresetData> change)
        {
            List<GraphicsPresetData> list = Defaults();
            change(list[0]);
            return GraphicsPresetCatalog.Validate(list, out _);
        }

        [Test]
        public void ErrorsAreReportedTogether_NotJustTheFirst()
        {
            List<GraphicsPresetData> list = Defaults();
            list[0].ShadowDistanceMeters = 0f;
            list[1].LodBias = 0f;
            var ex = Assert.Throws<GraphicsPresetException>(() => new GraphicsPresetCatalog(list));
            Assert.GreaterOrEqual(ex.Issues.Count, 2);
        }

        [Test]
        public void Clone_CopiesValues()
        {
            GraphicsPresetData original = Defaults()[2];
            GraphicsPresetData copy = original.Clone();
            Assert.AreNotSame(original, copy);
            copy.ShadowDistanceMeters = 1f;
            Assert.AreNotEqual(1f, original.ShadowDistanceMeters);
            Assert.AreEqual(original.DisplayKey, copy.DisplayKey);
        }

        // ---------- 업스케일러 선택 ----------

        [TestCase(0x10DE, true, true, UpscalerMode.Dlss)]
        [TestCase(0x10DE, true, false, UpscalerMode.Dlss)]
        [TestCase(0x10DE, false, true, UpscalerMode.Fsr2)]   // NVIDIA인데 DLSS 불가(구형 GPU) -> FSR2
        [TestCase(0x1002, true, true, UpscalerMode.Fsr2)]    // AMD -> FSR2 (DLSS 가능 표시여도 NVIDIA 아님)
        [TestCase(0x8086, false, true, UpscalerMode.Fsr2)]   // Intel -> FSR2
        [TestCase(0x1002, false, false, UpscalerMode.Off)]
        [TestCase(0x10DE, false, false, UpscalerMode.Off)]
        public void UpscalerSelector_PicksDlssForNvidia_Fsr2Otherwise(int vendor, bool dlss, bool fsr2, UpscalerMode expected)
        {
            Assert.AreEqual(expected, UpscalerSelector.Choose(vendor, dlss, fsr2));
        }

        [Test]
        public void QualityMap_MatchesEngineEnumValues()
        {
            // DLSSQuality: MaximumPerformance=0, Balanced=1, MaximumQuality=2, UltraPerformance=3
            Assert.AreEqual(2u, UpscalerQualityMap.DlssValue(UpscalerQuality.Quality));
            Assert.AreEqual(1u, UpscalerQualityMap.DlssValue(UpscalerQuality.Balanced));
            Assert.AreEqual(0u, UpscalerQualityMap.DlssValue(UpscalerQuality.Performance));
            Assert.AreEqual(3u, UpscalerQualityMap.DlssValue(UpscalerQuality.UltraPerformance));
            // FSR2Quality: Quality=0, Balanced=1, Performance=2, UltraPerformance=3
            Assert.AreEqual(0u, UpscalerQualityMap.Fsr2Value(UpscalerQuality.Quality));
            Assert.AreEqual(1u, UpscalerQualityMap.Fsr2Value(UpscalerQuality.Balanced));
            Assert.AreEqual(2u, UpscalerQualityMap.Fsr2Value(UpscalerQuality.Performance));
            Assert.AreEqual(3u, UpscalerQualityMap.Fsr2Value(UpscalerQuality.UltraPerformance));
        }

        [Test]
        public void UpscalerStatus_IsActiveOnlyWhenModeIsNotOff()
        {
            Assert.IsFalse(new UpscalerStatus(UpscalerMode.Off, UpscalerQuality.Quality, "x", 0).IsActive);
            Assert.IsTrue(new UpscalerStatus(UpscalerMode.Fsr2, UpscalerQuality.Quality, null, 0x1002).IsActive);
            Assert.AreEqual(string.Empty, new UpscalerStatus(UpscalerMode.Fsr2, UpscalerQuality.Quality, null, 0).UnavailableReason);
        }
    }
}

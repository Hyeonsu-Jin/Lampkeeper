// =============================================================
// 파일: GraphicsAndTelemetryPlayTests.cs
// 역할: 플레이 모드에서 HDRP 렌더 적용기(프리셋 전환, 업스케일러 적용)와 성능 측정 컴포넌트(프레임 루프 연결, CSV)가
//       실제로 동작하는지 확인한다. 시험이 끝나면 품질 설정을 원래대로 되돌린다.
// 레이어: test (PlayMode, Game.Tests.Integration)
// 관련: FR-120, FR-121, FR-163, NFR-021, NFR-028, DES-108, DES-109, DES-114 / SDD M1-05, M1-06
//
// 업스케일러는 이 PC의 GPU·그래픽 API에서 쓸 수 있으면 켜지고, 쓸 수 없으면 꺼진 상태와 이유가 남는지를 본다.
// (어느 쪽이든 정상 동작이다. 플레이어 빌드에서의 최종 확인은 M1-12.)
// =============================================================
using System.Collections;
using System.IO;
using System.Linq;
using Game.Application.Graphics;
using Game.Application.Perf;
using Game.Infra.Platform.Telemetry;
using Game.Infra.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Integration
{
    public class GraphicsAndTelemetryPlayTests
    {
        private int _savedLevel;
        private float _savedLodBias;
        private float _savedTerrainDensity;

        [SetUp]
        public void SaveQualityState()
        {
            _savedLevel = QualitySettings.GetQualityLevel();
            _savedLodBias = QualitySettings.lodBias;
            _savedTerrainDensity = QualitySettings.terrainDetailDensityScale;
        }

        [TearDown]
        public void RestoreQualityState()
        {
            QualitySettings.SetQualityLevel(_savedLevel, true);
            QualitySettings.lodBias = _savedLodBias;
            QualitySettings.terrainDetailDensityScale = _savedTerrainDensity;
        }

        private static int CountPresetVolumes() =>
            Resources.FindObjectsOfTypeAll<GameObject>().Count(g => g.name == "GraphicsPresetVolume" && g.scene.IsValid());

        [UnityTest]
        public IEnumerator Backend_AppliesEveryPreset_QualityLevelAndRuntimeValues()
        {
            int volumesBefore = CountPresetVolumes();
            var backend = new HdrpRenderBackend();
            GraphicsPresetData[] presets = GraphicsPresetCatalog.CreateDefaults();
            Assert.IsNull(backend.CurrentPreset);

            foreach (GraphicsPresetData preset in presets)
            {
                backend.ApplyPreset(preset);
                yield return null; // 한 프레임 렌더해 HDRP가 새 애셋·Volume으로 그려 보게 한다.
                Assert.AreEqual(preset.QualityLevelIndex, QualitySettings.GetQualityLevel(), preset.Id + ": 품질 단계");
                Assert.AreEqual(preset.TerrainDetailDensity, QualitySettings.terrainDetailDensityScale, 1e-4f, preset.Id + ": 지형 디테일");
                Assert.AreSame(preset, backend.CurrentPreset);
                Assert.AreEqual(preset.MaxShadowCastingLights, backend.MaxShadowCastingLights);
            }

            // 프리셋 적용용 Volume은 하나만 만들어 재사용한다.
            Assert.AreEqual(volumesBefore + 1, CountPresetVolumes());
            backend.Dispose();
            yield return null;
            Assert.AreEqual(volumesBefore, CountPresetVolumes(), "Dispose하면 Volume이 사라져야 합니다.");
        }

        [UnityTest]
        public IEnumerator Backend_Upscaler_OnAndOff_ReportsStatus_AndConfiguresCamera()
        {
            var cameraObject = new GameObject("TestCamera");
            var camera = cameraObject.AddComponent<Camera>();
            var backend = new HdrpRenderBackend();
            backend.ApplyPreset(GraphicsPresetCatalog.CreateDefaults()[(int)GraphicsPresetId.High]);
            yield return null;

            UpscalerStatus on = backend.ApplyUpscaler(new UpscalerRequest(true, UpscalerQuality.Quality));
            yield return null;
            Debug.Log("[시험] 업스케일러 요청=켬/품질 → 실제=" + on.Mode + ", GPU 0x" + on.GpuVendorId.ToString("X4") + " " + SystemInfo.graphicsDeviceName
                                  + ", API " + SystemInfo.graphicsDeviceType + ", 이유='" + on.UnavailableReason + "'");
            Assert.AreEqual(UpscalerQuality.Quality, on.Quality);
            if (on.Mode == UpscalerMode.Off)
            {
                Assert.IsNotEmpty(on.UnavailableReason, "켤 수 없으면 이유가 남아야 합니다.");
            }
            else
            {
                Assert.IsEmpty(on.UnavailableReason);
                // NVIDIA는 DLSS, 그 밖은 FSR2 (SDD 3.12.7)
                Assert.AreEqual(on.GpuVendorId == UpscalerSelector.NvidiaVendorId ? UpscalerMode.Dlss : UpscalerMode.Fsr2, on.Mode);
            }

            UpscalerStatus off = backend.ApplyUpscaler(new UpscalerRequest(false, UpscalerQuality.Quality));
            yield return null;
            Assert.AreEqual(UpscalerMode.Off, off.Mode);
            Assert.IsEmpty(off.UnavailableReason);
            Assert.AreEqual(UpscalerMode.Off, backend.Upscaler.Mode);

            // 나중에 만든 카메라에도 마지막 요청이 적용된다(예외 없이).
            var late = new GameObject("LateCamera").AddComponent<Camera>();
            backend.ApplyToCamera(late);
            Object.Destroy(late.gameObject);
            Object.Destroy(camera.gameObject);
        }

        [UnityTest]
        public IEnumerator Backend_ApplyPreset_WithBadQualityLevel_KeepsLevelAndWarns()
        {
            var backend = new HdrpRenderBackend();
            GraphicsPresetData bad = GraphicsPresetCatalog.CreateDefaults()[0];
            bad.QualityLevelIndex = 99;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("품질 단계 번호가 범위"));
            backend.ApplyPreset(bad);
            yield return null;
            Assert.AreEqual(_savedLevel, QualitySettings.GetQualityLevel());
        }

        [UnityTest]
        public IEnumerator TelemetryBehaviour_RunsInFrameLoop_WritesCsvAndHud()
        {
            string folder = Path.Combine(Path.GetTempPath(), "kh_perf_" + System.Guid.NewGuid().ToString("N"));
            string csv = Path.Combine(folder, "perf.csv");
            PerfTelemetryBehaviour behaviour = PerfTelemetryBehaviour.Create(new PerfTelemetrySettings { CsvPath = csv, ShowHud = true });
            try
            {
                Assert.IsTrue(behaviour.HudVisible);
                float end = Time.realtimeSinceStartup + 2.3f;
                while (Time.realtimeSinceStartup < end)
                {
                    behaviour.Telemetry.Probe.Begin(PerfMarkers.ClientViews);
                    behaviour.Telemetry.Probe.End(PerfMarkers.ClientViews);
                    yield return null;
                }
                Assert.IsNotNull(behaviour.Telemetry.Latest, "프레임 루프에서 1초 통계가 나와야 합니다.");
                Assert.Greater(behaviour.Telemetry.Latest.Frames, 0);
                Assert.Greater(behaviour.Telemetry.Latest.AverageFps, 0);
                StringAssert.Contains("FPS", behaviour.Telemetry.GetHudText());
            }
            finally
            {
                Object.Destroy(behaviour.gameObject);
            }
            yield return null;
            Assert.GreaterOrEqual(File.ReadAllLines(csv).Length, 3, "머리글 + 2줄 이상");
            Directory.Delete(folder, true);
        }
    }
}

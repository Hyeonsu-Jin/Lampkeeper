// =============================================================
// 파일: GraphicsBootstrapper.cs
// 역할: 그래픽 프리셋 애셋을 읽어 검증하고, HDRP 렌더 적용기를 만들어 시작 프리셋(기준 프리셋 「높음」)과 업스케일링을 적용한다.
//       옵션 화면이 생기기 전(M2-01)까지는 항상 기준 프리셋으로 시작한다.
// 레이어: infrastructure (Game.Bootstrap, 컴포지션 루트)
// 관련: FR-120, FR-121, NFR-005, NFR-028, DES-109, DES-114 / SDD 3.12.7, M1-06
// =============================================================
using Game.Adapters.Presentation.Rendering;
using Game.Application.Graphics;
using Game.Infra.Config;
using Game.Infra.Rendering;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>그래픽 시작 설정 조립.</summary>
    public sealed class GraphicsBootstrapper
    {
        private const string LogTag = "[Graphics] ";

        /// <summary>HDRP 렌더 적용기.</summary>
        public HdrpRenderBackend Backend { get; }

        /// <summary>검증된 프리셋 모음.</summary>
        public GraphicsPresetCatalog Catalog { get; }

        /// <summary>표현 계층이 쓰는 인터페이스 형태.</summary>
        public IRenderBackend RenderBackend => Backend;

        private GraphicsBootstrapper(GraphicsPresetCatalog catalog, HdrpRenderBackend backend)
        {
            Catalog = catalog;
            Backend = backend;
        }

        /// <summary>
        /// 프리셋 애셋을 읽어 조립한다.
        /// </summary>
        /// <param name="config">그래픽 프리셋 애셋(GameDataConfig.Graphics)</param>
        /// <returns>조립 결과. 애셋이 없거나 올바르지 않으면 로그를 남기고 null</returns>
        public static GraphicsBootstrapper TryCreate(GraphicsPresetConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning(LogTag + "그래픽 프리셋 애셋이 연결되지 않아 프리셋 적용을 건너뜁니다(Game/Graphics/Create Quality Levels And Presets 실행).");
                return null;
            }
            try
            {
                return new GraphicsBootstrapper(config.ToCatalog(), new HdrpRenderBackend());
            }
            catch (GraphicsPresetException ex)
            {
                // 데이터가 틀리면 잘못된 화질로 조용히 시작하지 않고 원인을 모두 남긴다.
                Debug.LogError(LogTag + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 프리셋을 적용하고 그 프리셋의 기본 업스케일링을 켠다(업스케일링 지정이 있으면 그것을 따른다).
        /// </summary>
        /// <param name="preset">적용할 프리셋. null이면 기준 프리셋</param>
        /// <param name="upscaleOverride">업스케일링 켬/끔 지정(측정용). PresetDefault면 프리셋 기본값</param>
        public void ApplyStartupSettings(GraphicsPresetData preset, Game.Application.Perf.UpscaleSwitch upscaleOverride)
        {
            GraphicsPresetData target = preset ?? Catalog.Benchmark;
            Backend.ApplyPreset(target);

            bool enabled = upscaleOverride == Game.Application.Perf.UpscaleSwitch.PresetDefault
                ? target.UpscalerOnByDefault
                : upscaleOverride == Game.Application.Perf.UpscaleSwitch.On;
            UpscalerStatus status = Backend.ApplyUpscaler(new UpscalerRequest(enabled, target.UpscalerQualityDefault));

            Debug.Log(LogTag + "프리셋 " + target.Id + "(품질 단계 " + target.QualityLevelIndex + ") 적용. 업스케일러: 요청="
                      + (enabled ? "켬" : "끔") + ", 실제=" + status.Mode + ", 품질=" + status.Quality
                      + ", GPU=" + SystemInfo.graphicsDeviceName + " (0x" + status.GpuVendorId.ToString("X4") + ")"
                      + (status.UnavailableReason.Length > 0 ? ". " + status.UnavailableReason : string.Empty));
        }
    }
}

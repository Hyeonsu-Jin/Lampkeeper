// =============================================================
// 파일: HdrpRenderBackend.cs
// 역할: IRenderBackend의 HDRP 구현(골격). 그래픽 프리셋 적용(GraphicsService)과 업스케일러 적용(UpscalerService)을
//       묶는다. 나중에 URP로 옮기게 되면 이 어셈블리를 통째로 고치면 되도록 HDRP 의존 코드가 여기에만 있다.
// 레이어: infrastructure (Game.Infra.Rendering) - HDRP 의존 코드
// 관련: FR-120, FR-121, NFR-028, DES-109, DES-114 / SDD 3.4.4, M1-06
//
// M1-06 범위: 프리셋·업스케일러. 낮·밤 Volume 혼합, 태양·광원, 색각 보정은 M2-01·M2-04 작업에서 이 클래스에 더한다.
// =============================================================
using Game.Adapters.Presentation.Rendering;
using Game.Application.Graphics;
using UnityEngine;

namespace Game.Infra.Rendering
{
    /// <summary>HDRP용 렌더 적용기. 메인 스레드에서만 쓴다.</summary>
    public sealed class HdrpRenderBackend : IRenderBackend, System.IDisposable
    {
        private readonly GraphicsService _graphics = new GraphicsService();
        private readonly UpscalerService _upscaler = new UpscalerService();

        /// <inheritdoc />
        public GraphicsPresetData CurrentPreset => _graphics.Current;

        /// <inheritdoc />
        public UpscalerStatus Upscaler => _upscaler.Status;

        /// <summary>그림자를 드리우는 국소 광원 상한. 조명 상태 계산(DES-090)이 읽는다.</summary>
        public int MaxShadowCastingLights => _graphics.MaxShadowCastingLights;

        /// <inheritdoc />
        public void ApplyPreset(GraphicsPresetData preset) => _graphics.Apply(preset);

        /// <inheritdoc />
        public UpscalerStatus ApplyUpscaler(UpscalerRequest request) => _upscaler.Apply(request);

        /// <summary>프리셋 적용용 Volume을 없앤다. 백엔드를 더 쓰지 않을 때 부른다.</summary>
        public void Dispose() => _graphics.Dispose();

        /// <summary>나중에 만들어진 카메라에도 마지막 업스케일링 요청을 적용한다.</summary>
        public void ApplyToCamera(Camera camera) => _upscaler.ApplyToCamera(camera);
    }
}

// =============================================================
// 파일: IRenderBackend.cs
// 역할: 표현 계층이 렌더링 파이프라인(HDRP)을 직접 모르고 화면 품질을 바꾸는 창구. 인터페이스만 여기에 있고,
//       HDRP를 부르는 구현(HdrpRenderBackend)은 Game.Infra.Rendering에 있다.
// 레이어: adapter (Game.Adapters.Presentation)
// 관련: FR-120, FR-121, NFR-028, DES-114, DES-109 / SDD 3.4.4
//
// 주고받는 값은 파이프라인과 무관한 값(프리셋 데이터, 업스케일링 요청)뿐이다. 이 인터페이스에는 HDRP/URP 형식이 없다.
// 월드 분위기(낮·밤 Volume), 광원, 후처리(색각 보정)는 해당 작업(M2-04, M2-01 등)에서 이 인터페이스에 메서드로 더한다.
// =============================================================
using Game.Application.Graphics;

namespace Game.Adapters.Presentation.Rendering
{
    /// <summary>렌더링 적용 창구.</summary>
    public interface IRenderBackend
    {
        /// <summary>마지막으로 적용한 프리셋. 아직 적용 전이면 null.</summary>
        GraphicsPresetData CurrentPreset { get; }

        /// <summary>업스케일러의 현재 상태.</summary>
        UpscalerStatus Upscaler { get; }

        /// <summary>
        /// 프리셋 하나를 적용한다: 품질 단계 전환, 그림자 거리, SSAO·SSR, LOD 편향, 지형 디테일 밀도 등.
        /// 프리셋의 업스케일링 기본값은 이 호출에서 적용하지 않는다(<see cref="ApplyUpscaler"/>로 따로).
        /// </summary>
        /// <param name="preset">프리셋 데이터</param>
        void ApplyPreset(GraphicsPresetData preset);

        /// <summary>
        /// 업스케일링을 적용한다. 방식(DLSS/FSR2)은 GPU 종류로 고르고, 쓸 수 없으면 꺼진 상태로 두고
        /// 이유를 <see cref="Upscaler"/>.UnavailableReason에 남긴다.
        /// </summary>
        /// <returns>적용 뒤의 상태</returns>
        UpscalerStatus ApplyUpscaler(UpscalerRequest request);
    }
}

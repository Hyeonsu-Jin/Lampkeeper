// =============================================================
// 파일: UpscalerService.cs
// 역할: 업스케일러(DLSS / FSR2) 적용. GPU 종류로 방식을 고르고(NVIDIA는 DLSS, 그 밖은 FSR2), 품질 단계를
//       게임 카메라마다 HDAdditionalCameraData에 써 넣는다. 쓸 수 없는 환경이면 꺼진 상태로 두고 이유를 남긴다.
// 레이어: infrastructure (Game.Infra.Rendering) - HDRP 의존 코드
// 관련: FR-121(업스케일링), NFR-005, NFR-010~NFR-012, NFR-028, DES-109, DES-114 / SDD 3.12.7
//
// HDRP 쪽 전제(Editor 도구 GraphicsAssetBuilder가 애셋에 설정): 동적 해상도 사용, 업스케일러 목록에 DLSS·FSR2,
// 내장 모듈 nvidia·amd 설치. 카메라에서는 allowDynamicResolution과 방식별 허용 플래그만 바꾼다.
// 판정(R-19): 에디터 플레이 모드 동작은 M1-06에서, 플레이어 빌드 확인은 M1-12 측정 빌드에서 한다.
// =============================================================
using Game.Application.Graphics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Infra.Rendering
{
    /// <summary>업스케일러 적용기.</summary>
    internal sealed class UpscalerService
    {
        private UpscalerRequest _lastRequest;
        private UpscalerMode _lastMode;
        private bool _hasRequest;

        /// <summary>마지막 적용 결과.</summary>
        public UpscalerStatus Status { get; private set; } = new UpscalerStatus(UpscalerMode.Off, UpscalerQuality.Quality, string.Empty, 0);

        /// <summary>모든 게임 카메라에 적용한다.</summary>
        public UpscalerStatus Apply(UpscalerRequest request)
        {
            int vendor = SystemInfo.graphicsDeviceVendorID;
            UpscalerMode mode = UpscalerMode.Off;
            string reason = string.Empty;

            if (request.Enabled)
            {
                bool dlss = HDDynamicResolutionPlatformCapabilities.DLSSDetected;
                bool fsr2 = HDDynamicResolutionPlatformCapabilities.FSR2Detected;
                mode = UpscalerSelector.Choose(vendor, dlss, fsr2);
                if (mode == UpscalerMode.Off)
                {
                    reason = Describe(vendor, dlss, fsr2);
                }
            }

            _lastRequest = request;
            _lastMode = mode;
            _hasRequest = true;

            foreach (Camera camera in Camera.allCameras)
            {
                ConfigureCamera(camera, mode, request.Quality);
            }

            Status = new UpscalerStatus(mode, request.Quality, reason, vendor);
            return Status;
        }

        /// <summary>나중에 만들어진 카메라에도 마지막 요청을 적용한다.</summary>
        public void ApplyToCamera(Camera camera)
        {
            if (_hasRequest)
            {
                ConfigureCamera(camera, _lastMode, _lastRequest.Quality);
            }
        }

        private static void ConfigureCamera(Camera camera, UpscalerMode mode, UpscalerQuality quality)
        {
            // 게임 화면을 그리는 카메라만 다룬다(씬 뷰·미리보기 카메라 제외).
            if (camera == null || camera.cameraType != CameraType.Game)
            {
                return;
            }
            HDAdditionalCameraData data = camera.GetComponent<HDAdditionalCameraData>();
            if (data == null)
            {
                data = camera.gameObject.AddComponent<HDAdditionalCameraData>();
            }

            data.allowDynamicResolution = mode != UpscalerMode.Off;

            data.allowDeepLearningSuperSampling = mode == UpscalerMode.Dlss;
            data.deepLearningSuperSamplingUseCustomQualitySettings = true;
            data.deepLearningSuperSamplingQuality = UpscalerQualityMap.DlssValue(quality);

            data.allowFidelityFX2SuperResolution = mode == UpscalerMode.Fsr2;
            data.fidelityFX2SuperResolutionUseCustomQualitySettings = true;
            data.fidelityFX2SuperResolutionQuality = UpscalerQualityMap.Fsr2Value(quality);
        }

        private static string Describe(int vendor, bool dlss, bool fsr2)
        {
            string api = SystemInfo.graphicsDeviceType.ToString();
            return "업스케일러를 쓸 수 없습니다: GPU 제조사 0x" + vendor.ToString("X4") + ", 그래픽 API " + api
                   + ", DLSS 사용 가능=" + dlss + ", FSR2 사용 가능=" + fsr2
                   + " (DLSS·FSR2는 DirectX 12/Vulkan과 내장 모듈 nvidia·amd가 필요)";
        }
    }
}

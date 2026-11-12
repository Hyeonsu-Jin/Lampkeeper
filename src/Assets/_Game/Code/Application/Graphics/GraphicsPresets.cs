// =============================================================
// 파일: GraphicsPresets.cs
// 역할: 그래픽 프리셋 4단계(낮음/보통/높음/최고)의 데이터 형식과 검증, 업스케일러 종류·품질 모드, GPU별 업스케일러 선택 규칙.
//       렌더 파이프라인(HDRP)을 전혀 모르는 순수 C#이다. HDRP에 적용하는 코드는 Game.Infra.Rendering에 있다.
// 레이어: application (순수 C#)
// 관련: FR-120(그래픽 품질 프리셋), FR-121(화면 세부 설정, 업스케일링), NFR-005, NFR-010~NFR-012, NFR-028, NFR-033,
//       DES-109 / SDD 3.12.7, 3.4.4, C-52(기준 프리셋 「높음」)
//
// 값은 코드가 아니라 Data/Graphics 애셋(GraphicsPresetConfig)에 있고, 여기 있는 CreateDefaults는 처음 애셋을 만들 때의 초기값이다.
// =============================================================
using System;
using System.Collections.Generic;

namespace Game.Application.Graphics
{
    /// <summary>프리셋 번호. 낮은 번호가 가벼운 프리셋이다. 세부 설정을 바꾸면 화면에는 「사용자 지정」으로 표시한다(FR-121).</summary>
    public enum GraphicsPresetId
    {
        /// <summary>낮음.</summary>
        Low = 0,
        /// <summary>보통.</summary>
        Medium = 1,
        /// <summary>높음. 성능 측정 기준 프리셋(C-52).</summary>
        High = 2,
        /// <summary>최고.</summary>
        Ultra = 3,
    }

    /// <summary>업스케일링 방식.</summary>
    public enum UpscalerMode
    {
        /// <summary>사용 안 함(원래 해상도로 그림).</summary>
        Off = 0,
        /// <summary>NVIDIA DLSS.</summary>
        Dlss = 1,
        /// <summary>AMD FidelityFX Super Resolution 2.</summary>
        Fsr2 = 2,
    }

    /// <summary>업스케일링 품질 단계. 판정·기본 경로는 「품질 모드」(Quality)다(FR-121 최소 품질 모드 포함).</summary>
    public enum UpscalerQuality
    {
        /// <summary>품질 모드(기본).</summary>
        Quality = 0,
        /// <summary>균형.</summary>
        Balanced = 1,
        /// <summary>성능.</summary>
        Performance = 2,
        /// <summary>초고성능.</summary>
        UltraPerformance = 3,
    }

    /// <summary>
    /// 프리셋 하나의 세부 값(SDD 3.12.7): 그림자 거리·단계, 볼류메트릭 안개 품질, SSAO·SSR, 지형 디테일 밀도,
    /// LOD 편향, 광원 그림자 수. Unity 직렬화가 읽을 수 있게 공개 필드로 둔다.
    /// </summary>
    [Serializable]
    public sealed class GraphicsPresetData
    {
        /// <summary>프리셋 번호.</summary>
        public GraphicsPresetId Id;

        /// <summary>화면 텍스트 키(다국어 표에서 이름을 찾는 키).</summary>
        public string DisplayKey = string.Empty;

        /// <summary>QualitySettings 품질 단계 번호(HDRP 애셋이 단계별로 연결됨).</summary>
        public int QualityLevelIndex;

        /// <summary>그림자가 그려지는 최대 거리(m).</summary>
        public float ShadowDistanceMeters = 100f;

        /// <summary>그림자 품질 단계(0 낮음 ~ 3 최고). HDRP 애셋의 그림자 해상도·필터에 대응.</summary>
        public int ShadowQualityTier = 2;

        /// <summary>볼류메트릭 안개 품질(0 끔 ~ 3 높음).</summary>
        public int VolumetricFogQuality = 2;

        /// <summary>화면 공간 앰비언트 오클루전(SSAO) 사용.</summary>
        public bool SsaoEnabled = true;

        /// <summary>화면 공간 반사(SSR) 사용.</summary>
        public bool SsrEnabled = true;

        /// <summary>지형 디테일(풀 등) 밀도 배율(0~1).</summary>
        public float TerrainDetailDensity = 1f;

        /// <summary>LOD 편향(클수록 먼 거리에서 낮은 LOD로 바뀜).</summary>
        public float LodBias = 1f;

        /// <summary>그림자를 드리우는 국소 광원의 최대 수(가까운 순). SDD 3.12.7: 기본 4.</summary>
        public int MaxShadowCastingLights = 4;

        /// <summary>이 프리셋을 고를 때 업스케일링을 기본으로 켤지.</summary>
        public bool UpscalerOnByDefault;

        /// <summary>이 프리셋의 기본 업스케일링 품질 단계.</summary>
        public UpscalerQuality UpscalerQualityDefault = UpscalerQuality.Quality;

        /// <summary>성능 측정 기준 프리셋 표시. 전체에서 정확히 1개(C-52: 「높음」).</summary>
        public bool IsBenchmarkPreset;

        /// <summary>값을 복사한 새 객체.</summary>
        public GraphicsPresetData Clone() => (GraphicsPresetData)MemberwiseClone();
    }

    /// <summary>프리셋 모음과 검증. 로드한 뒤 바뀌지 않는다.</summary>
    public sealed class GraphicsPresetCatalog
    {
        /// <summary>프리셋 개수(낮음·보통·높음·최고).</summary>
        public const int PresetCount = 4;

        /// <summary>허용하는 최대 그림자 단계.</summary>
        public const int MaxShadowTier = 3;

        /// <summary>허용하는 최대 안개 품질.</summary>
        public const int MaxFogQuality = 3;

        private readonly GraphicsPresetData[] _presets;

        /// <summary>프리셋 목록(번호 순서).</summary>
        public IReadOnlyList<GraphicsPresetData> Presets => _presets;

        /// <summary>성능 측정 기준 프리셋(「높음」).</summary>
        public GraphicsPresetData Benchmark { get; }

        /// <summary>검증된 프리셋 목록으로 만든다.</summary>
        /// <exception cref="GraphicsPresetException">검증에 실패할 때</exception>
        public GraphicsPresetCatalog(IEnumerable<GraphicsPresetData> presets)
        {
            var issues = Validate(presets, out GraphicsPresetData[] ordered);
            if (issues.Count > 0)
            {
                throw new GraphicsPresetException(issues);
            }
            _presets = ordered;
            foreach (GraphicsPresetData preset in ordered)
            {
                if (preset.IsBenchmarkPreset)
                {
                    Benchmark = preset;
                }
            }
        }

        /// <summary>번호로 프리셋을 찾는다.</summary>
        public GraphicsPresetData Get(GraphicsPresetId id) => _presets[(int)id];

        /// <summary>
        /// 데이터를 검사하고 문제 목록을 돌려준다(빈 목록이면 정상). 검사 항목: 정확히 4개, 번호 중복 없음,
        /// 품질 단계 번호 중복·음수 없음, 값 범위, 기준 프리셋 정확히 1개이며 「높음」.
        /// </summary>
        public static List<string> Validate(IEnumerable<GraphicsPresetData> presets, out GraphicsPresetData[] ordered)
        {
            var issues = new List<string>();
            ordered = null;
            var list = new List<GraphicsPresetData>(presets ?? Array.Empty<GraphicsPresetData>());
            if (list.Count != PresetCount)
            {
                issues.Add("프리셋은 정확히 " + PresetCount + "개여야 합니다(현재 " + list.Count + "개).");
                return issues;
            }

            var byId = new GraphicsPresetData[PresetCount];
            var usedLevels = new HashSet<int>();
            int benchmarkCount = 0;
            foreach (GraphicsPresetData p in list)
            {
                if (p == null)
                {
                    issues.Add("비어 있는 프리셋 항목이 있습니다.");
                    continue;
                }
                int index = (int)p.Id;
                if (index < 0 || index >= PresetCount)
                {
                    issues.Add("알 수 없는 프리셋 번호: " + index);
                    continue;
                }
                if (byId[index] != null)
                {
                    issues.Add("프리셋 번호가 중복됩니다: " + p.Id);
                    continue;
                }
                byId[index] = p;

                string name = p.Id.ToString();
                if (string.IsNullOrEmpty(p.DisplayKey)) issues.Add(name + ": DisplayKey가 비어 있습니다.");
                if (p.QualityLevelIndex < 0) issues.Add(name + ": 품질 단계 번호가 음수입니다.");
                else if (!usedLevels.Add(p.QualityLevelIndex)) issues.Add(name + ": 품질 단계 번호가 다른 프리셋과 겹칩니다: " + p.QualityLevelIndex);
                if (p.ShadowDistanceMeters <= 0f) issues.Add(name + ": 그림자 거리는 0보다 커야 합니다.");
                if (p.ShadowQualityTier < 0 || p.ShadowQualityTier > MaxShadowTier) issues.Add(name + ": 그림자 단계 범위 오류(0~" + MaxShadowTier + ").");
                if (p.VolumetricFogQuality < 0 || p.VolumetricFogQuality > MaxFogQuality) issues.Add(name + ": 안개 품질 범위 오류(0~" + MaxFogQuality + ").");
                if (p.TerrainDetailDensity < 0f || p.TerrainDetailDensity > 1f) issues.Add(name + ": 지형 디테일 밀도는 0~1이어야 합니다.");
                if (p.LodBias <= 0f) issues.Add(name + ": LOD 편향은 0보다 커야 합니다.");
                if (p.MaxShadowCastingLights < 0) issues.Add(name + ": 그림자 광원 수가 음수입니다.");
                if (p.IsBenchmarkPreset)
                {
                    benchmarkCount++;
                    // [C-52] 기준 프리셋은 「높음」이다.
                    if (p.Id != GraphicsPresetId.High) issues.Add(name + ": 기준 프리셋은 「높음」이어야 합니다(C-52).");
                    if (!p.UpscalerOnByDefault) issues.Add(name + ": 기준 프리셋은 업스케일링 기본값이 켬이어야 합니다(C-52).");
                    if (p.UpscalerQualityDefault != UpscalerQuality.Quality) issues.Add(name + ": 기준 프리셋의 업스케일링은 품질 모드여야 합니다(C-52).");
                }
            }
            if (benchmarkCount != 1) issues.Add("기준 프리셋은 정확히 1개여야 합니다(현재 " + benchmarkCount + "개).");

            for (int i = 0; i < PresetCount; i++)
            {
                if (byId[i] == null) issues.Add("프리셋이 빠졌습니다: " + (GraphicsPresetId)i);
            }
            if (issues.Count == 0) ordered = byId;
            return issues;
        }

        /// <summary>
        /// 처음 애셋을 만들 때 쓰는 초기값 4개. 기준 프리셋은 「높음」(업스케일링 켬, 품질 모드).
        /// 실제 값은 측정(M1-12)으로 조정하며, 그때는 코드가 아니라 애셋을 고친다(NFR-033).
        /// </summary>
        public static GraphicsPresetData[] CreateDefaults()
        {
            return new[]
            {
                new GraphicsPresetData
                {
                    Id = GraphicsPresetId.Low, DisplayKey = "graphics.preset.low", QualityLevelIndex = 0,
                    ShadowDistanceMeters = 60f, ShadowQualityTier = 0, VolumetricFogQuality = 0, SsaoEnabled = false, SsrEnabled = false,
                    TerrainDetailDensity = 0.4f, LodBias = 0.7f, MaxShadowCastingLights = 1,
                    UpscalerOnByDefault = true, UpscalerQualityDefault = UpscalerQuality.Performance,
                },
                new GraphicsPresetData
                {
                    Id = GraphicsPresetId.Medium, DisplayKey = "graphics.preset.medium", QualityLevelIndex = 1,
                    ShadowDistanceMeters = 100f, ShadowQualityTier = 1, VolumetricFogQuality = 1, SsaoEnabled = true, SsrEnabled = false,
                    TerrainDetailDensity = 0.65f, LodBias = 0.85f, MaxShadowCastingLights = 2,
                    UpscalerOnByDefault = true, UpscalerQualityDefault = UpscalerQuality.Balanced,
                },
                new GraphicsPresetData
                {
                    Id = GraphicsPresetId.High, DisplayKey = "graphics.preset.high", QualityLevelIndex = 2,
                    ShadowDistanceMeters = 150f, ShadowQualityTier = 2, VolumetricFogQuality = 2, SsaoEnabled = true, SsrEnabled = true,
                    TerrainDetailDensity = 0.85f, LodBias = 1.0f, MaxShadowCastingLights = 4,
                    UpscalerOnByDefault = true, UpscalerQualityDefault = UpscalerQuality.Quality,
                    IsBenchmarkPreset = true,
                },
                new GraphicsPresetData
                {
                    Id = GraphicsPresetId.Ultra, DisplayKey = "graphics.preset.ultra", QualityLevelIndex = 3,
                    ShadowDistanceMeters = 220f, ShadowQualityTier = 3, VolumetricFogQuality = 3, SsaoEnabled = true, SsrEnabled = true,
                    TerrainDetailDensity = 1.0f, LodBias = 1.3f, MaxShadowCastingLights = 6,
                    UpscalerOnByDefault = false, UpscalerQualityDefault = UpscalerQuality.Quality,
                },
            };
        }
    }

    /// <summary>프리셋 데이터가 올바르지 않을 때 던지는 오류.</summary>
    public sealed class GraphicsPresetException : Exception
    {
        /// <summary>발견된 문제 목록.</summary>
        public IReadOnlyList<string> Issues { get; }

        public GraphicsPresetException(IReadOnlyList<string> issues)
            : base("그래픽 프리셋 데이터 오류:\n - " + string.Join("\n - ", issues))
        {
            Issues = issues;
        }
    }

    /// <summary>GPU별 업스케일러 선택 규칙 (SDD 3.12.7): NVIDIA는 DLSS, 그 밖은 FSR.</summary>
    public static class UpscalerSelector
    {
        /// <summary>NVIDIA의 PCI 제조사 번호.</summary>
        public const int NvidiaVendorId = 0x10DE;

        /// <summary>
        /// 쓸 업스케일러를 고른다. 기본 경로: NVIDIA이고 DLSS를 쓸 수 있으면 DLSS, 아니면 FSR2를 쓸 수 있을 때 FSR2.
        /// 둘 다 안 되면 Off.
        /// </summary>
        /// <param name="gpuVendorId">GPU 제조사 번호(SystemInfo.graphicsDeviceVendorID)</param>
        /// <param name="dlssAvailable">이 환경에서 DLSS가 실제로 쓸 수 있는지(드라이버·그래픽 API·모듈 포함)</param>
        /// <param name="fsr2Available">이 환경에서 FSR2를 쓸 수 있는지</param>
        public static UpscalerMode Choose(int gpuVendorId, bool dlssAvailable, bool fsr2Available)
        {
            if (gpuVendorId == NvidiaVendorId && dlssAvailable)
            {
                return UpscalerMode.Dlss;
            }
            return fsr2Available ? UpscalerMode.Fsr2 : UpscalerMode.Off;
        }
    }
}

namespace Game.Application.Graphics
{
    /// <summary>업스케일링 요청: 사용 여부와 품질 단계. 방식(DLSS/FSR2)은 GPU 종류로 정해진다.</summary>
    public readonly struct UpscalerRequest
    {
        /// <summary>업스케일링을 쓸지.</summary>
        public bool Enabled { get; }

        /// <summary>품질 단계.</summary>
        public UpscalerQuality Quality { get; }

        public UpscalerRequest(bool enabled, UpscalerQuality quality)
        {
            Enabled = enabled;
            Quality = quality;
        }
    }

    /// <summary>업스케일링이 실제로 어떻게 적용됐는지.</summary>
    public readonly struct UpscalerStatus
    {
        /// <summary>실제로 켜진 방식(Off면 꺼져 있음).</summary>
        public UpscalerMode Mode { get; }

        /// <summary>적용된 품질 단계.</summary>
        public UpscalerQuality Quality { get; }

        /// <summary>요청은 켬이었는데 쓸 수 없어 꺼진 이유(텍스트 키가 아니라 개발용 설명). 정상이면 빈 글자.</summary>
        public string UnavailableReason { get; }

        /// <summary>GPU 제조사 번호(측정 보고서용).</summary>
        public int GpuVendorId { get; }

        public UpscalerStatus(UpscalerMode mode, UpscalerQuality quality, string unavailableReason, int gpuVendorId)
        {
            Mode = mode;
            Quality = quality;
            UnavailableReason = unavailableReason ?? string.Empty;
            GpuVendorId = gpuVendorId;
        }

        /// <summary>업스케일링이 실제로 켜져 있는지.</summary>
        public bool IsActive => Mode != UpscalerMode.Off;
    }
}

namespace Game.Application.Graphics
{
    /// <summary>
    /// 업스케일링 품질 단계를 각 방식의 숫자 값으로 바꾼다(HDRP의 카메라 설정이 uint를 받음).
    /// 값은 UnityEngine.NVIDIA.DLSSQuality, UnityEngine.AMD.FSR2Quality 열거형의 값과 같다(Rendering 시험이 대조).
    /// </summary>
    public static class UpscalerQualityMap
    {
        /// <summary>DLSSQuality: MaximumPerformance=0, Balanced=1, MaximumQuality=2, UltraPerformance=3.</summary>
        public static uint DlssValue(UpscalerQuality quality)
        {
            switch (quality)
            {
                case UpscalerQuality.Quality: return 2;
                case UpscalerQuality.Balanced: return 1;
                case UpscalerQuality.Performance: return 0;
                default: return 3;
            }
        }

        /// <summary>FSR2Quality: Quality=0, Balanced=1, Performance=2, UltraPerformance=3.</summary>
        public static uint Fsr2Value(UpscalerQuality quality)
        {
            switch (quality)
            {
                case UpscalerQuality.Quality: return 0;
                case UpscalerQuality.Balanced: return 1;
                case UpscalerQuality.Performance: return 2;
                default: return 3;
            }
        }
    }
}

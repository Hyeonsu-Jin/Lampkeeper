// =============================================================
// 파일: GraphicsService.cs
// 역할: 그래픽 프리셋을 HDRP에 적용한다. 품질 단계(QualitySettings)를 바꾸면 단계에 연결된 HDRP 애셋이 바뀌고,
//       그 위에 프리셋의 세부 값(지형 디테일 밀도, 그림자 거리·SSAO·SSR·안개 품질)을 덮어쓴다. LOD 편향은 HDRP 애셋에 들어 있다.
// 레이어: infrastructure (Game.Infra.Rendering) - HDRP 의존 코드
// 관련: FR-120(프리셋 적용), FR-121, NFR-005, NFR-010~NFR-012, NFR-028, DES-109, DES-114 / SDD 3.12.7
//
// 그림자 거리·SSAO·SSR·안개는 HDRP에서 Volume으로 정해지므로, 우선순위가 높은 전역 Volume 하나("GraphicsPresetVolume")를
// 만들어 프리셋 값을 넣는다. 장면의 다른 Volume(낮·밤, 지역별 분위기 - M2-04)은 이 Volume이 건드리지 않는 항목만 바꾼다.
// 그림자 광원 수(MaxShadowCastingLights)는 조명 상태 계산(DES-090)이 읽어 가도록 속성으로 노출한다.
// =============================================================
using Game.Application.Graphics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Infra.Rendering
{
    /// <summary>프리셋 적용기.</summary>
    internal sealed class GraphicsService : System.IDisposable
    {
        private const string VolumeObjectName = "GraphicsPresetVolume";

        /// <summary>프리셋 Volume의 우선순위(장면 Volume보다 높게).</summary>
        private const float VolumePriority = 1000f;

        private Volume _volume;
        private VolumeProfile _profile;

        /// <summary>마지막으로 적용한 프리셋.</summary>
        public GraphicsPresetData Current { get; private set; }

        /// <summary>그림자를 드리우는 국소 광원의 상한(조명 쪽이 읽음).</summary>
        public int MaxShadowCastingLights => Current != null ? Current.MaxShadowCastingLights : 0;

        /// <summary>프리셋을 적용한다.</summary>
        public void Apply(GraphicsPresetData preset)
        {
            if (preset == null)
            {
                throw new System.ArgumentNullException(nameof(preset));
            }

            // 1) 품질 단계 전환 -> 연결된 HDRP 애셋이 바뀐다.
            int levelCount = QualitySettings.names.Length;
            if (preset.QualityLevelIndex >= 0 && preset.QualityLevelIndex < levelCount)
            {
                QualitySettings.SetQualityLevel(preset.QualityLevelIndex, true);
            }
            else
            {
                Debug.LogWarning("[Graphics] 품질 단계 번호가 범위를 벗어나 전환하지 않았습니다: " + preset.QualityLevelIndex + " (단계 " + levelCount + "개). GraphicsAssetBuilder로 단계를 만드세요.");
            }

            // 2) 엔진 품질 값. (LOD 편향은 HDRP가 애셋 표로 덮어쓰므로 여기서 바꾸지 않는다: GraphicsAssetBuilder가 애셋에 반영)
            QualitySettings.terrainDetailDensityScale = preset.TerrainDetailDensity;

            // 3) HDRP Volume 값
            ApplyVolume(preset);

            Current = preset;
        }

        private void ApplyVolume(GraphicsPresetData preset)
        {
            EnsureVolume();

            if (!_profile.TryGet(out HDShadowSettings shadows))
            {
                shadows = _profile.Add<HDShadowSettings>();
            }
            shadows.maxShadowDistance.Override(preset.ShadowDistanceMeters);

            if (!_profile.TryGet(out ScreenSpaceAmbientOcclusion ssao))
            {
                ssao = _profile.Add<ScreenSpaceAmbientOcclusion>();
            }
            // 끄면 강도를 0으로 눌러 장면 Volume의 값을 덮는다. 켠 경우는 장면 Volume이 정한 값을 그대로 둔다.
            ssao.intensity.overrideState = !preset.SsaoEnabled;
            ssao.intensity.value = 0f;

            if (!_profile.TryGet(out ScreenSpaceReflection ssr))
            {
                ssr = _profile.Add<ScreenSpaceReflection>();
            }
            ssr.enabled.overrideState = !preset.SsrEnabled;
            ssr.enabled.value = false;

            if (!_profile.TryGet(out Fog fog))
            {
                fog = _profile.Add<Fog>();
            }
            // 볼류메트릭 안개 품질 0 = 끔, 1~3 = 낮음·보통·높음 (HDRP의 품질 단계 Low/Medium/High에 대응)
            fog.enableVolumetricFog.overrideState = preset.VolumetricFogQuality == 0;
            fog.enableVolumetricFog.value = false;
            if (preset.VolumetricFogQuality > 0)
            {
                fog.quality.Override(preset.VolumetricFogQuality - 1);
            }
            else
            {
                fog.quality.overrideState = false;
            }
        }

        /// <summary>만든 Volume과 프로필을 없앤다.</summary>
        public void Dispose()
        {
            if (_volume != null) Object.Destroy(_volume.gameObject);
            if (_profile != null) Object.Destroy(_profile);
            _volume = null;
            _profile = null;
        }

        private void EnsureVolume()
        {
            if (_volume != null)
            {
                return;
            }
            var go = new GameObject(VolumeObjectName) { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.hideFlags = HideFlags.HideAndDontSave;
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = VolumePriority;
            _volume.weight = 1f;
            _volume.sharedProfile = _profile;
        }
    }
}

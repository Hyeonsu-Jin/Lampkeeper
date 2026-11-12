// =============================================================
// 파일: PerfTelemetryBehaviour.cs
// 역할: 성능 측정을 Unity 프레임 루프에 연결하는 컴포넌트. 프레임마다 시간(프레임·메인 스레드·GPU·GC 할당)을 읽어
//       PerfTelemetry.Tick에 넘기고, 개발 빌드에서 개발용 성능 표시(F3으로 켜고 끔)를 그린다.
// 레이어: infrastructure (Game.Infra.Platform)
// 관련: FR-163(개발용 성능 표시), NFR-011, NFR-021, DES-108 / SDD 3.12.4
//
// - 출시 빌드: 표시(OnGUI)는 컴파일에서 빠진다. 켜는 길이 없다(FR-163 수용 기준).
// - GPU 시간은 FrameTimingManager가 쓸 수 있을 때만 값이 들어온다(Player Settings의 Frame Timing Stats).
// - GC 할당은 Profiler 기록기("GC Allocated In Frame")를 읽는다. 개발 빌드·에디터에서만 값이 있고 그 밖에는 0이다.
// =============================================================
using System;
using Game.Application.Perf;
using Unity.Profiling;
using UnityEngine;

namespace Game.Infra.Platform.Telemetry
{
    /// <summary>성능 측정 설정.</summary>
    public sealed class PerfTelemetrySettings
    {
        /// <summary>CSV 파일 경로. null이면 기록하지 않음.</summary>
        public string CsvPath { get; set; }

        /// <summary>시작할 때 표시를 켤지.</summary>
        public bool ShowHud { get; set; }

        /// <summary>예산. null이면 기본 예산.</summary>
        public PerfBudget Budget { get; set; }
    }

    /// <summary>프레임 루프 연결 컴포넌트.</summary>
    public sealed class PerfTelemetryBehaviour : MonoBehaviour
    {
        private const string GcCounterName = "GC Allocated In Frame";
        private const float HudFontRatio = 0.018f;
        private const int HudMargin = 8;

        private PerfTelemetry _telemetry;
        private ProfilerRecorder _gcRecorder;
        private FrameTiming[] _timings = new FrameTiming[1];
        private double _elapsed;
        private bool _hudVisible;
        private GUIStyle _hudStyle;

        /// <summary>측정 중심 객체.</summary>
        public PerfTelemetry Telemetry => _telemetry;

        /// <summary>개발용 성능 표시를 켜고 끈다.</summary>
        public bool HudVisible
        {
            get => _hudVisible;
            set => _hudVisible = value;
        }

        /// <summary>
        /// 측정 컴포넌트를 담은 오브젝트를 만들고 장면이 바뀌어도 남겨 둔다.
        /// </summary>
        public static PerfTelemetryBehaviour Create(PerfTelemetrySettings settings)
        {
            var go = new GameObject("PerfTelemetry") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            var behaviour = go.AddComponent<PerfTelemetryBehaviour>();
            behaviour.Initialize(settings ?? new PerfTelemetrySettings());
            return behaviour;
        }

        private void Initialize(PerfTelemetrySettings settings)
        {
            _telemetry = new PerfTelemetry(settings.Budget, settings.CsvPath, message => Debug.LogWarning(message));
            _hudVisible = settings.ShowHud;
            _gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, GcCounterName);
            if (!string.IsNullOrEmpty(settings.CsvPath))
            {
                Debug.Log("[Perf] 성능 CSV 기록: " + settings.CsvPath);
            }
        }

        private void LateUpdate()
        {
            if (_telemetry == null)
            {
                return;
            }
            double frameMs = Time.unscaledDeltaTime * 1000.0;
            _elapsed += Time.unscaledDeltaTime;

            double mainMs = frameMs;
            double gpuMs = 0;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
            {
                if (_timings[0].cpuMainThreadFrameTime > 0) mainMs = _timings[0].cpuMainThreadFrameTime;
                gpuMs = _timings[0].gpuFrameTime;
            }
            long gcBytes = _gcRecorder.Valid ? _gcRecorder.LastValue : 0;

            _telemetry.Tick(_elapsed, frameMs, mainMs, gpuMs, gcBytes);
        }

        private void OnDestroy()
        {
            if (_gcRecorder.Valid) _gcRecorder.Dispose();
            _telemetry?.Dispose();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            // F3: 표시 켜고 끄기. IMGUI 이벤트로 읽어 입력 시스템 설정과 무관하게 동작한다.
            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.F3)
            {
                _hudVisible = !_hudVisible;
                e.Use();
            }
            if (!_hudVisible || _telemetry == null)
            {
                return;
            }
            if (_hudStyle == null)
            {
                _hudStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = false };
            }
            _hudStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * HudFontRatio));
            string text = _telemetry.GetHudText();
            Vector2 size = _hudStyle.CalcSize(new GUIContent(text));
            GUI.Box(new Rect(HudMargin, HudMargin, size.x + HudMargin, size.y + HudMargin), text, _hudStyle);
        }
#endif
    }
}

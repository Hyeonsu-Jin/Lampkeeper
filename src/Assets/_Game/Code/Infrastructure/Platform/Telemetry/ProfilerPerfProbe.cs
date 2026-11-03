// =============================================================
// 파일: ProfilerPerfProbe.cs
// 역할: IPerfProbe 구현. 구간 시간을 Stopwatch 시각으로 직접 재서 모으고, 동시에 Unity ProfilerMarker로도 감싸
//       Profiler 창에서 같은 이름으로 볼 수 있게 한다. 프레임이 끝날 때 FlushFrame으로 집계기에 넘긴다.
// 레이어: infrastructure (Game.Infra.Platform)
// 관련: FR-163, NFR-011, NFR-021, DES-108 / SDD 3.12.4 1~2항
//
// SDD와 다른 점: SDD는 "ProfilerRecorder로 읽기"라고 적었지만, ProfilerMarker는 Development 빌드가 아니면 호출이
// 사라져 측정 빌드(출시 설정)에서 값을 읽을 수 없다. 그래서 자체 시각 측정을 기본으로 하고 ProfilerMarker는 보조로 둔다.
// 메인 스레드 전용이다(스레드 안전하지 않음).
// =============================================================
using System.Collections.Generic;
using System.Diagnostics;
using Game.Application.Perf;
using Game.Application.Ports;
using Unity.Profiling;
using UnityDebug = UnityEngine.Debug;

namespace Game.Infra.Platform.Telemetry
{
    /// <summary>구간 시간 측정기(메인 스레드 전용).</summary>
    public sealed class ProfilerPerfProbe : IPerfProbe
    {
        private const double MillisecondsPerSecond = 1000.0;

        private struct Slot
        {
            public ProfilerMarker Marker;
            public long StartTimestamp;
            public int Depth;
            public double SumMs;
            public double MaxCallMs;
        }

        private readonly Slot[] _slots;
        private readonly Dictionary<string, int> _indexByName;
        private readonly HashSet<string> _warnedUnknown = new HashSet<string>();
        private readonly PerfAggregator _aggregator;
        private readonly double _ticksToMs = MillisecondsPerSecond / Stopwatch.Frequency;

        /// <summary>측정기를 만든다.</summary>
        /// <param name="aggregator">프레임마다 값을 넘길 집계기</param>
        public ProfilerPerfProbe(PerfAggregator aggregator)
        {
            _aggregator = aggregator;
            _slots = new Slot[PerfMarkers.All.Count];
            _indexByName = new Dictionary<string, int>(PerfMarkers.All.Count);
            for (int i = 0; i < PerfMarkers.All.Count; i++)
            {
                _slots[i].Marker = new ProfilerMarker(ProfilerCategory.Scripts, PerfMarkers.All[i]);
                _indexByName[PerfMarkers.All[i]] = i;
            }
        }

        /// <inheritdoc />
        public void Begin(string marker)
        {
            if (!TryGetSlot(marker, out int index))
            {
                return;
            }
            ref Slot slot = ref _slots[index];
            if (slot.Depth++ == 0)
            {
                slot.Marker.Begin();
                slot.StartTimestamp = Stopwatch.GetTimestamp();
            }
        }

        /// <inheritdoc />
        public void End(string marker)
        {
            if (!TryGetSlot(marker, out int index))
            {
                return;
            }
            ref Slot slot = ref _slots[index];
            if (slot.Depth == 0)
            {
                return; // 짝이 없는 End는 무시한다(Begin 전에 켜진 경우 등)
            }
            if (--slot.Depth == 0)
            {
                double ms = (Stopwatch.GetTimestamp() - slot.StartTimestamp) * _ticksToMs;
                slot.Marker.End();
                slot.SumMs += ms;
                if (ms > slot.MaxCallMs) slot.MaxCallMs = ms;
            }
        }

        /// <inheritdoc />
        public void Count(string name, double value) => _aggregator.SetCounter(name, value);

        /// <summary>
        /// 프레임이 끝났을 때 부른다. 표식마다 이번 프레임 합계를 집계기에 넘기고 0으로 되돌린다.
        /// (호출되지 않은 표식도 0으로 넘겨 프레임당 평균에 반영한다)
        /// </summary>
        public void FlushFrame()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _aggregator.RecordMarker(i, _slots[i].SumMs, _slots[i].MaxCallMs);
                _slots[i].SumMs = 0;
                _slots[i].MaxCallMs = 0;
            }
        }

        private bool TryGetSlot(string marker, out int index)
        {
            if (marker != null && _indexByName.TryGetValue(marker, out index))
            {
                return true;
            }
            index = -1;
            // 이름 오타는 한 번만 알린다(매 프레임 로그가 쌓이지 않게).
            if (marker != null && _warnedUnknown.Add(marker))
            {
                UnityDebug.LogWarning("[Perf] 등록되지 않은 측정 구간 이름이라 무시합니다: " + marker + " (PerfMarkers에 추가하세요)");
            }
            return false;
        }
    }
}

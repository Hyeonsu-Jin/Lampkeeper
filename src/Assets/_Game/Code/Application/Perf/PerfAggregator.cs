// =============================================================
// 파일: PerfAggregator.cs
// 역할: 프레임마다 들어오는 측정값(프레임 시간, 표식별 합계, GC 할당 등)을 모아 1초마다 통계(PerfSnapshot)를 낸다.
//       시간 흐름은 호출한 쪽이 알려 주므로 시계에 의존하지 않고 시험하기 쉽다.
// 레이어: application (순수 C#)
// 관련: FR-163, NFR-010~NFR-021, DES-108 / SDD 3.12.4 2항
//
// 사용 순서(프레임마다): RecordMarker를 표식 수만큼 -> EndFrame. EndFrame이 true를 돌려주면 Snapshot을 읽는다.
// 표식이 그 프레임에 호출되지 않았으면 합계 0으로 알린다(프레임당 평균에 0이 들어가는 것이 맞다).
// =============================================================
using System;

namespace Game.Application.Perf
{
    /// <summary>1초 통계 집계기.</summary>
    public sealed class PerfAggregator
    {
        /// <summary>한 번에 통계를 내는 구간 길이(초). SDD 3.12.4: 1초마다.</summary>
        public const double WindowSeconds = 1.0;

        private const int InitialCapacity = 256;

        private readonly int _markerCount = PerfMarkers.All.Count;
        private readonly int _counterCount = PerfCounters.All.Count;
        private readonly double[][] _markerFrameMs;
        private readonly double[] _markerMaxCallMs;
        private readonly double[] _counters;
        private double[] _frameMs = new double[InitialCapacity];
        private double[] _mainMs = new double[InitialCapacity];
        private double[] _gpuMs = new double[InitialCapacity];
        private double[] _scratch = new double[InitialCapacity];
        private int _frames;
        private long _gcBytes;
        private double _windowStartSeconds;
        private bool _started;

        public PerfAggregator()
        {
            _markerFrameMs = new double[_markerCount][];
            for (int i = 0; i < _markerCount; i++)
            {
                _markerFrameMs[i] = new double[InitialCapacity];
            }
            _markerMaxCallMs = new double[_markerCount];
            _counters = new double[_counterCount];
        }

        /// <summary>가장 최근에 완성한 1초 통계. 아직 없으면 null.</summary>
        public PerfSnapshot Snapshot { get; private set; }

        /// <summary>이름 붙은 지표 값을 갱신한다. 모르는 이름은 무시한다.</summary>
        public void SetCounter(string name, double value)
        {
            for (int i = 0; i < _counterCount; i++)
            {
                if (string.Equals(PerfCounters.All[i], name, StringComparison.Ordinal))
                {
                    _counters[i] = value;
                    return;
                }
            }
        }

        /// <summary>이번 프레임에 표식이 쓴 시간을 넣는다.</summary>
        /// <param name="markerIndex">PerfMarkers.All 안의 위치</param>
        /// <param name="frameSumMs">이번 프레임에 그 표식에 쓴 시간의 합(ms)</param>
        /// <param name="maxCallMs">이번 프레임의 한 번 호출 최댓값(ms)</param>
        public void RecordMarker(int markerIndex, double frameSumMs, double maxCallMs)
        {
            EnsureCapacity(_frames + 1);
            _markerFrameMs[markerIndex][_frames] = frameSumMs;
            if (maxCallMs > _markerMaxCallMs[markerIndex])
            {
                _markerMaxCallMs[markerIndex] = maxCallMs;
            }
        }

        /// <summary>
        /// 프레임을 마친다.
        /// </summary>
        /// <param name="nowSeconds">측정 시작 후 경과 시간(초). 프레임이 끝난 시각</param>
        /// <param name="frameMs">이 프레임의 길이(ms)</param>
        /// <param name="mainThreadMs">메인 스레드 시간(ms). 모르면 frameMs</param>
        /// <param name="gpuMs">GPU 시간(ms). 모르면 0</param>
        /// <param name="gcAllocatedBytes">이 프레임에 메인 스레드가 할당한 바이트</param>
        /// <returns>1초가 차서 새 <see cref="Snapshot"/>이 만들어졌으면 true</returns>
        public bool EndFrame(double nowSeconds, double frameMs, double mainThreadMs, double gpuMs, long gcAllocatedBytes)
        {
            if (!_started)
            {
                _started = true;
                _windowStartSeconds = nowSeconds - frameMs / 1000.0;
            }
            EnsureCapacity(_frames + 1);
            _frameMs[_frames] = frameMs;
            _mainMs[_frames] = mainThreadMs;
            _gpuMs[_frames] = gpuMs;
            _gcBytes += gcAllocatedBytes;
            _frames++;

            if (nowSeconds - _windowStartSeconds < WindowSeconds)
            {
                return false;
            }
            Snapshot = BuildSnapshot(nowSeconds);
            ResetWindow(nowSeconds);
            return true;
        }

        private PerfSnapshot BuildSnapshot(double nowSeconds)
        {
            var snapshot = new PerfSnapshot
            {
                ElapsedSeconds = nowSeconds,
                Frames = _frames,
                GcAllocatedBytes = _gcBytes,
                Markers = new PerfStat[_markerCount],
                Counters = (double[])_counters.Clone(),
            };

            double totalMs = 0;
            for (int i = 0; i < _frames; i++) totalMs += _frameMs[i];
            snapshot.FrameMsAverage = _frames > 0 ? totalMs / _frames : 0;
            snapshot.AverageFps = totalMs > 0 ? _frames * 1000.0 / totalMs : 0;
            snapshot.MainThreadMsAverage = PerfMath.Average(_mainMs, _frames);
            snapshot.GpuMsAverage = PerfMath.Average(_gpuMs, _frames);

            Array.Copy(_frameMs, _scratch, _frames);
            Array.Sort(_scratch, 0, _frames);
            snapshot.FrameMsP99 = PerfMath.Percentile(_scratch, _frames, 99);
            snapshot.FrameMsMax = _frames > 0 ? _scratch[_frames - 1] : 0;
            snapshot.OnePercentLowFps = PerfMath.OnePercentLowFps(_scratch, _frames);

            for (int m = 0; m < _markerCount; m++)
            {
                double[] series = _markerFrameMs[m];
                double average = PerfMath.Average(series, _frames);
                Array.Copy(series, _scratch, _frames);
                Array.Sort(_scratch, 0, _frames);
                snapshot.Markers[m] = new PerfStat(
                    average,
                    PerfMath.Percentile(_scratch, _frames, 95),
                    PerfMath.Percentile(_scratch, _frames, 99),
                    _frames > 0 ? _scratch[_frames - 1] : 0,
                    _markerMaxCallMs[m]);
            }
            return snapshot;
        }

        private void ResetWindow(double nowSeconds)
        {
            // 알리지 않은 표식이 지난 구간의 값을 물려받지 않도록, 쓴 칸을 0으로 되돌린다.
            for (int m = 0; m < _markerCount; m++)
            {
                Array.Clear(_markerFrameMs[m], 0, _frames);
            }
            _frames = 0;
            _gcBytes = 0;
            _windowStartSeconds = nowSeconds;
            Array.Clear(_markerMaxCallMs, 0, _markerCount);
        }

        private void EnsureCapacity(int needed)
        {
            if (needed <= _frameMs.Length)
            {
                return;
            }
            int size = _frameMs.Length * 2;
            while (size < needed) size *= 2;
            Array.Resize(ref _frameMs, size);
            Array.Resize(ref _mainMs, size);
            Array.Resize(ref _gpuMs, size);
            Array.Resize(ref _scratch, size);
            for (int i = 0; i < _markerCount; i++)
            {
                Array.Resize(ref _markerFrameMs[i], size);
            }
        }
    }
}

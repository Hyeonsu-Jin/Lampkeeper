// =============================================================
// 파일: PerfTelemetry.cs
// 역할: 성능 측정의 중심. 측정기(IPerfProbe 구현) + 1초 집계 + 예산 감시 + CSV 기록을 묶는다.
//       Unity 프레임 루프와 분리해 두어(Tick 호출만 받음) EditMode 시험에서 시계를 흉내 내어 확인할 수 있다.
// 레이어: infrastructure (Game.Infra.Platform)
// 관련: FR-163, NFR-010~NFR-021, DES-108 / SDD 3.12.3, 3.12.4
//
// 서버 처리 예산 감시(3.12.4): Server.Total이 예산(평균 3.0 ms, p99 5.0 ms, 틱 6.0 ms)을 넘으면 1초마다 경고 로그를 남긴다.
// 같은 항목 경고는 ViolationLogCooldownSeconds 동안 한 번만 남겨 로그가 넘치지 않게 한다.
// =============================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Application.Perf;
using Game.Application.Ports;

namespace Game.Infra.Platform.Telemetry
{
    /// <summary>성능 측정 중심 객체.</summary>
    public sealed class PerfTelemetry : IDisposable
    {
        /// <summary>같은 예산 경고를 다시 남기기까지의 최소 간격(초).</summary>
        public const double ViolationLogCooldownSeconds = 10.0;

        private readonly PerfAggregator _aggregator = new PerfAggregator();
        private readonly ProfilerPerfProbe _probe;
        private readonly PerfBudget _budget;
        private readonly Action<string> _warn;
        private readonly Dictionary<string, double> _lastViolationLog = new Dictionary<string, double>(StringComparer.Ordinal);
        private StreamWriter _csv;
        private readonly object _hudGate = new object();
        private string _hudText = string.Empty;

        /// <summary>측정기. 서버·클라이언트 코드에 이것(IPerfProbe)을 넘긴다.</summary>
        public IPerfProbe Probe => _probe;

        /// <summary>가장 최근 1초 통계. 아직 없으면 null.</summary>
        public PerfSnapshot Latest => _aggregator.Snapshot;

        /// <summary>가장 최근 1초의 예산 초과 항목.</summary>
        public IReadOnlyList<PerfBudgetViolation> LatestViolations { get; private set; } = Array.Empty<PerfBudgetViolation>();

        /// <summary>CSV 파일 경로. 기록하지 않으면 null.</summary>
        public string CsvPath { get; }

        /// <summary>1초 통계가 새로 나올 때마다 호출된다.</summary>
        public event Action<PerfSnapshot> SnapshotReady;

        /// <summary>
        /// 만든다.
        /// </summary>
        /// <param name="budget">감시할 예산</param>
        /// <param name="csvPath">CSV를 쓸 파일 경로. null이면 기록하지 않음</param>
        /// <param name="warn">예산 초과 경고를 남길 함수(Unity 로그 등)</param>
        public PerfTelemetry(PerfBudget budget, string csvPath, Action<string> warn)
        {
            _budget = budget ?? PerfBudget.Default;
            _warn = warn ?? (_ => { });
            _probe = new ProfilerPerfProbe(_aggregator);
            CsvPath = csvPath;
            if (!string.IsNullOrEmpty(csvPath))
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(csvPath));
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                // 측정 중에도 다른 프로그램(엑셀 등)이 파일을 읽을 수 있게 읽기 공유로 연다.
                _csv = new StreamWriter(new FileStream(csvPath, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
                _csv.WriteLine(PerfCsv.Header());
                _csv.Flush();
            }
        }

        /// <summary>
        /// 프레임이 끝날 때마다 부른다.
        /// </summary>
        /// <param name="elapsedSeconds">측정 시작 후 경과 시간(초)</param>
        /// <param name="frameMs">프레임 길이(ms)</param>
        /// <param name="mainThreadMs">메인 스레드 시간(ms). 모르면 frameMs</param>
        /// <param name="gpuMs">GPU 시간(ms). 모르면 0</param>
        /// <param name="gcAllocatedBytes">이 프레임의 GC 할당 바이트</param>
        public void Tick(double elapsedSeconds, double frameMs, double mainThreadMs, double gpuMs, long gcAllocatedBytes)
        {
            _probe.FlushFrame();
            if (!_aggregator.EndFrame(elapsedSeconds, frameMs, mainThreadMs, gpuMs, gcAllocatedBytes))
            {
                return;
            }

            PerfSnapshot snapshot = _aggregator.Snapshot;
            if (_csv != null)
            {
                _csv.WriteLine(PerfCsv.Row(snapshot));
                _csv.Flush();
            }

            List<PerfBudgetViolation> violations = PerfBudgetChecker.Evaluate(snapshot, _budget);
            LatestViolations = violations;
            LogViolations(violations, snapshot.ElapsedSeconds);

            string text = BuildHudText(snapshot, violations);
            lock (_hudGate) { _hudText = text; }
            SnapshotReady?.Invoke(snapshot);
        }

        /// <summary>개발용 성능 표시에 보여 줄 글자(마지막 1초 기준). 아직 통계가 없으면 안내 글자.</summary>
        public string GetHudText()
        {
            lock (_hudGate) { return _hudText.Length == 0 ? "측정 중..." : _hudText; }
        }

        public void Dispose()
        {
            if (_csv != null)
            {
                _csv.Dispose();
                _csv = null;
            }
        }

        private void LogViolations(List<PerfBudgetViolation> violations, double nowSeconds)
        {
            foreach (PerfBudgetViolation v in violations)
            {
                if (_lastViolationLog.TryGetValue(v.Code, out double last) && nowSeconds - last < ViolationLogCooldownSeconds)
                {
                    continue;
                }
                _lastViolationLog[v.Code] = nowSeconds;
                _warn("[Perf] 예산 초과: " + v);
            }
        }

        /// <summary>FR-163 표시 항목: FPS(평균·1% Low), 프레임 시간, 송수신 대역폭, RTT, 활동 적 수, 동기화 자원 수.</summary>
        private static string BuildHudText(PerfSnapshot s, List<PerfBudgetViolation> violations)
        {
            var b = new StringBuilder();
            b.Append("FPS ").Append(s.AverageFps.ToString("0.0")).Append("  (1% Low ").Append(s.OnePercentLowFps.ToString("0.0")).Append(")\n");
            b.Append("프레임 ").Append(s.FrameMsAverage.ToString("0.00")).Append(" ms  p99 ").Append(s.FrameMsP99.ToString("0.00")).Append("  최대 ").Append(s.FrameMsMax.ToString("0.00")).Append("\n");
            b.Append("메인 ").Append(s.MainThreadMsAverage.ToString("0.00")).Append(" ms  GPU ").Append(s.GpuMsAverage.ToString("0.00")).Append(" ms\n");
            PerfStat server = s.GetMarker(PerfMarkers.ServerTotal);
            if (server.MaxCall > 0)
            {
                b.Append("서버 처리 평균 ").Append(server.Average.ToString("0.00")).Append("  p99 ").Append(server.P99.ToString("0.00")).Append("  틱 최대 ").Append(server.MaxCall.ToString("0.00")).Append(" ms\n");
            }
            b.Append("송신 ").Append((s.GetCounter(PerfCounters.NetSendBytesPerSec) * 8 / 1000.0).ToString("0.0")).Append(" kbps  수신 ").Append((s.GetCounter(PerfCounters.NetRecvBytesPerSec) * 8 / 1000.0).ToString("0.0")).Append(" kbps  RTT ").Append(s.GetCounter(PerfCounters.NetRttMs).ToString("0")).Append(" ms\n");
            b.Append("활동 적 ").Append(s.GetCounter(PerfCounters.ActiveCreatures).ToString("0")).Append("  동기화 자원 ").Append(s.GetCounter(PerfCounters.SyncedResources).ToString("0")).Append("\n");
            b.Append("GC 할당 ").Append((s.GcAllocatedBytes / 1024.0).ToString("0.0")).Append(" KB/s");
            if (violations.Count > 0)
            {
                b.Append("\n! 예산 초과: ");
                for (int i = 0; i < violations.Count; i++)
                {
                    if (i > 0) b.Append(", ");
                    b.Append(violations[i].Code);
                }
            }
            return b.ToString();
        }
    }
}

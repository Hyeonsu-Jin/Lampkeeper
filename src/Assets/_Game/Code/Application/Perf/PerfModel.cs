// =============================================================
// 파일: PerfModel.cs
// 역할: 성능 측정의 순수 규칙. 표식 이름, 지표 이름, 처리 예산(SDD 3.12.3), 1초 통계 값(평균·p95·p99·최댓값),
//       예산 판정, CSV 형식. Unity 없이 시험할 수 있게 순수 C#으로 둔다.
// 레이어: application (순수 C#)
// 관련: FR-163, NFR-010~NFR-021, NFR-048, DES-108 / SDD 3.12.3, 3.12.4
// =============================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Application.Perf
{
    /// <summary>측정 구간 이름 (SDD 3.12.4 1항).</summary>
    public static class PerfMarkers
    {
        public const string ServerTotal = "Server.Total";
        public const string ServerCommands = "Server.Commands";
        public const string ServerAiDecide = "Server.AiDecide";
        public const string ServerAiApply = "Server.AiApply";
        public const string ServerSystems = "Server.Systems";
        public const string ServerReplication = "Server.Replication";
        public const string ClientMirror = "Client.Mirror";
        public const string ClientViews = "Client.Views";
        public const string ClientUi = "Client.UI";

        /// <summary>CSV 열과 통계 계산 순서가 되는 전체 목록(고정 순서).</summary>
        public static readonly IReadOnlyList<string> All = new[]
        {
            ServerTotal, ServerCommands, ServerAiDecide, ServerAiApply, ServerSystems, ServerReplication,
            ClientMirror, ClientViews, ClientUi,
        };
    }

    /// <summary>이름 붙은 지표(Count로 알리는 값). FR-163 표시 항목.</summary>
    public static class PerfCounters
    {
        public const string NetSendBytesPerSec = "net.send_bytes_per_s";
        public const string NetRecvBytesPerSec = "net.recv_bytes_per_s";
        public const string NetRttMs = "net.rtt_ms";
        public const string ActiveCreatures = "creatures.active";
        public const string SyncedResources = "resources.synced";

        /// <summary>CSV 열 순서(고정).</summary>
        public static readonly IReadOnlyList<string> All = new[]
        {
            NetSendBytesPerSec, NetRecvBytesPerSec, NetRttMs, ActiveCreatures, SyncedResources,
        };
    }

    /// <summary>한 구간의 1초 통계. 프레임마다 합친 시간(ms)을 기준으로 한다.</summary>
    public readonly struct PerfStat
    {
        public double Average { get; }
        public double P95 { get; }
        public double P99 { get; }
        public double Max { get; }

        /// <summary>구간 한 번 호출의 최댓값(ms). 서버 틱 1회 상한(SDD 3.12.3)을 판정한다.</summary>
        public double MaxCall { get; }

        public PerfStat(double average, double p95, double p99, double max, double maxCall)
        {
            Average = average;
            P95 = p95;
            P99 = p99;
            Max = max;
            MaxCall = maxCall;
        }
    }

    /// <summary>1초 단위 측정 결과 한 줄.</summary>
    public sealed class PerfSnapshot
    {
        /// <summary>측정 시작 후 경과 초.</summary>
        public double ElapsedSeconds { get; set; }

        /// <summary>이 구간에 지난 프레임 수.</summary>
        public int Frames { get; set; }

        /// <summary>평균 FPS.</summary>
        public double AverageFps { get; set; }

        /// <summary>1% Low FPS (가장 느린 1% 프레임의 평균 프레임 시간을 FPS로 바꾼 값).</summary>
        public double OnePercentLowFps { get; set; }

        /// <summary>프레임 시간 평균(ms).</summary>
        public double FrameMsAverage { get; set; }

        /// <summary>프레임 시간 p99(ms).</summary>
        public double FrameMsP99 { get; set; }

        /// <summary>프레임 시간 최댓값(ms).</summary>
        public double FrameMsMax { get; set; }

        /// <summary>메인 스레드 시간 평균(ms). 알 수 없으면 프레임 시간과 같게 넣는다.</summary>
        public double MainThreadMsAverage { get; set; }

        /// <summary>GPU 프레임 시간 평균(ms). 알 수 없으면 0.</summary>
        public double GpuMsAverage { get; set; }

        /// <summary>이 구간에 메인 스레드가 할당한 바이트 합.</summary>
        public long GcAllocatedBytes { get; set; }

        /// <summary>표식별 통계(PerfMarkers.All 순서). 이 구간에 호출이 없던 표식도 0으로 들어 있다.</summary>
        public PerfStat[] Markers { get; set; } = Array.Empty<PerfStat>();

        /// <summary>지표 값(PerfCounters.All 순서). 한 번도 알리지 않았으면 0.</summary>
        public double[] Counters { get; set; } = Array.Empty<double>();

        /// <summary>표식 이름으로 통계를 찾는다. 없으면 0 통계.</summary>
        public PerfStat GetMarker(string marker)
        {
            for (int i = 0; i < PerfMarkers.All.Count; i++)
            {
                if (string.Equals(PerfMarkers.All[i], marker, StringComparison.Ordinal) && i < Markers.Length)
                {
                    return Markers[i];
                }
            }
            return default;
        }

        /// <summary>지표 이름으로 값을 찾는다. 없으면 0.</summary>
        public double GetCounter(string name)
        {
            for (int i = 0; i < PerfCounters.All.Count; i++)
            {
                if (string.Equals(PerfCounters.All[i], name, StringComparison.Ordinal) && i < Counters.Length)
                {
                    return Counters[i];
                }
            }
            return 0;
        }
    }

    /// <summary>
    /// 처리 예산 (SDD 3.12.3, 3.12.4 4항). 기준: 기준 프리셋 「높음」 + 업스케일링 품질 모드, P8 조건.
    /// 값을 바꾸려면 SDD 예산표를 먼저 고친다. (설정값으로 옮길 때는 Data/Network 튜닝 애셋이 후보)
    /// </summary>
    public sealed class PerfBudget
    {
        /// <summary>기본 예산.</summary>
        public static PerfBudget Default { get; } = new PerfBudget(3.0, 5.0, 6.0, 13.0, 60.0, 45.0);

        /// <summary>Server.Total 프레임당 평균 상한(ms). [NFR-011]</summary>
        public double ServerTotalAverageMs { get; }

        /// <summary>Server.Total 프레임 p99 상한(ms).</summary>
        public double ServerTotalP99Ms { get; }

        /// <summary>Server.Total 틱 1회 상한(ms): 평균의 2배.</summary>
        public double ServerTickMaxMs { get; }

        /// <summary>메인 스레드 평균 상한(ms).</summary>
        public double MainThreadAverageMs { get; }

        /// <summary>평균 FPS 하한. [NFR-011, NFR-012]</summary>
        public double MinAverageFps { get; }

        /// <summary>1% Low FPS 하한.</summary>
        public double MinOnePercentLowFps { get; }

        public PerfBudget(double serverTotalAverageMs, double serverTotalP99Ms, double serverTickMaxMs, double mainThreadAverageMs, double minAverageFps, double minOnePercentLowFps)
        {
            ServerTotalAverageMs = serverTotalAverageMs;
            ServerTotalP99Ms = serverTotalP99Ms;
            ServerTickMaxMs = serverTickMaxMs;
            MainThreadAverageMs = mainThreadAverageMs;
            MinAverageFps = minAverageFps;
            MinOnePercentLowFps = minOnePercentLowFps;
        }
    }

    /// <summary>예산을 넘은 항목 하나.</summary>
    public readonly struct PerfBudgetViolation
    {
        /// <summary>항목 코드(예: Server.Total.Avg).</summary>
        public string Code { get; }
        public double Actual { get; }
        public double Limit { get; }

        public PerfBudgetViolation(string code, double actual, double limit)
        {
            Code = code;
            Actual = actual;
            Limit = limit;
        }

        public override string ToString() => Code + " " + Actual.ToString("0.00", CultureInfo.InvariantCulture) + " (한도 " + Limit.ToString("0.00", CultureInfo.InvariantCulture) + ")";
    }

    /// <summary>예산 판정기.</summary>
    public static class PerfBudgetChecker
    {
        /// <summary>
        /// 1초 결과가 예산을 넘었는지 본다. 서버 항목은 Server.Total이 한 번이라도 불렸을 때(호스트일 때)만 판정한다.
        /// </summary>
        /// <param name="snapshot">1초 결과</param>
        /// <param name="budget">예산</param>
        /// <returns>넘은 항목 목록(없으면 빈 목록)</returns>
        public static List<PerfBudgetViolation> Evaluate(PerfSnapshot snapshot, PerfBudget budget)
        {
            var result = new List<PerfBudgetViolation>();
            if (snapshot == null || snapshot.Frames == 0)
            {
                return result;
            }

            PerfStat server = snapshot.GetMarker(PerfMarkers.ServerTotal);
            bool isHost = server.MaxCall > 0;
            if (isHost)
            {
                if (server.Average > budget.ServerTotalAverageMs) result.Add(new PerfBudgetViolation("Server.Total.Avg", server.Average, budget.ServerTotalAverageMs));
                if (server.P99 > budget.ServerTotalP99Ms) result.Add(new PerfBudgetViolation("Server.Total.P99", server.P99, budget.ServerTotalP99Ms));
                if (server.MaxCall > budget.ServerTickMaxMs) result.Add(new PerfBudgetViolation("Server.Total.Tick", server.MaxCall, budget.ServerTickMaxMs));
            }
            if (snapshot.MainThreadMsAverage > budget.MainThreadAverageMs) result.Add(new PerfBudgetViolation("MainThread.Avg", snapshot.MainThreadMsAverage, budget.MainThreadAverageMs));
            if (snapshot.AverageFps < budget.MinAverageFps) result.Add(new PerfBudgetViolation("Fps.Avg", snapshot.AverageFps, budget.MinAverageFps));
            if (snapshot.OnePercentLowFps < budget.MinOnePercentLowFps) result.Add(new PerfBudgetViolation("Fps.1PctLow", snapshot.OnePercentLowFps, budget.MinOnePercentLowFps));
            return result;
        }
    }

    /// <summary>값 목록의 평균·백분위 계산.</summary>
    public static class PerfMath
    {
        /// <summary>
        /// 오름차순으로 정렬된 값의 백분위(가장 가까운 순위 방식). 값이 없으면 0.
        /// </summary>
        /// <param name="sortedAscending">오름차순 값</param>
        /// <param name="count">쓸 개수</param>
        /// <param name="percentile">0~100</param>
        public static double Percentile(double[] sortedAscending, int count, double percentile)
        {
            if (count <= 0)
            {
                return 0;
            }
            int rank = (int)Math.Ceiling(percentile / 100.0 * count);
            if (rank < 1) rank = 1;
            if (rank > count) rank = count;
            return sortedAscending[rank - 1];
        }

        /// <summary>앞 count개의 평균. 없으면 0.</summary>
        public static double Average(double[] values, int count)
        {
            if (count <= 0)
            {
                return 0;
            }
            double sum = 0;
            for (int i = 0; i < count; i++) sum += values[i];
            return sum / count;
        }

        /// <summary>
        /// 가장 느린 1%(최소 1프레임)의 평균 프레임 시간(ms)을 FPS로 바꾼 값.
        /// </summary>
        /// <param name="sortedFrameMsAscending">오름차순 프레임 시간(ms)</param>
        /// <param name="count">프레임 수</param>
        public static double OnePercentLowFps(double[] sortedFrameMsAscending, int count)
        {
            if (count <= 0)
            {
                return 0;
            }
            int worst = Math.Max(1, (int)Math.Ceiling(count * 0.01));
            double sum = 0;
            for (int i = count - worst; i < count; i++) sum += sortedFrameMsAscending[i];
            double avgMs = sum / worst;
            return avgMs > 0 ? 1000.0 / avgMs : 0;
        }
    }

    /// <summary>CSV 형식. 숫자는 항상 InvariantCulture(소수점 .)로 쓴다.</summary>
    public static class PerfCsv
    {
        /// <summary>첫 줄(열 이름). 표식마다 avg/p95/p99/max/maxcall 5열, 지표 열은 PerfCounters.All 순서.</summary>
        public static string Header()
        {
            var b = new StringBuilder();
            b.Append("time_s,frames,fps_avg,fps_1pct_low,frame_ms_avg,frame_ms_p99,frame_ms_max,main_ms_avg,gpu_ms_avg,gc_alloc_bytes");
            foreach (string marker in PerfMarkers.All)
            {
                foreach (string suffix in new[] { "avg", "p95", "p99", "max", "maxcall" })
                {
                    b.Append(',').Append(marker).Append('.').Append(suffix);
                }
            }
            foreach (string counter in PerfCounters.All)
            {
                b.Append(',').Append(counter);
            }
            return b.ToString();
        }

        /// <summary>데이터 한 줄.</summary>
        public static string Row(PerfSnapshot s)
        {
            var b = new StringBuilder();
            b.Append(F(s.ElapsedSeconds)).Append(',').Append(s.Frames.ToString(CultureInfo.InvariantCulture))
             .Append(',').Append(F(s.AverageFps)).Append(',').Append(F(s.OnePercentLowFps))
             .Append(',').Append(F(s.FrameMsAverage)).Append(',').Append(F(s.FrameMsP99)).Append(',').Append(F(s.FrameMsMax))
             .Append(',').Append(F(s.MainThreadMsAverage)).Append(',').Append(F(s.GpuMsAverage))
             .Append(',').Append(s.GcAllocatedBytes.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < PerfMarkers.All.Count; i++)
            {
                PerfStat m = i < s.Markers.Length ? s.Markers[i] : default;
                b.Append(',').Append(F(m.Average)).Append(',').Append(F(m.P95)).Append(',').Append(F(m.P99)).Append(',').Append(F(m.Max)).Append(',').Append(F(m.MaxCall));
            }
            for (int i = 0; i < PerfCounters.All.Count; i++)
            {
                b.Append(',').Append(F(i < s.Counters.Length ? s.Counters[i] : 0));
            }
            return b.ToString();
        }

        /// <summary>CSV 열 수.</summary>
        public static int ColumnCount => 10 + PerfMarkers.All.Count * 5 + PerfCounters.All.Count;

        private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}

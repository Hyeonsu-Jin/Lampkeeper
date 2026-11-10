// =============================================================
// 파일: PerfTests.cs
// 역할: 성능 측정 순수 규칙(통계, 집계기, 예산 판정, CSV, 명령줄 옵션)의 단위 시험.
// 레이어: test (EditMode, Game.Tests.Application)
// 관련: FR-163, NFR-010~NFR-021, DES-108 / SDD 3.12.3, 3.12.4, M1-05
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Perf;
using Game.Application.Ports;
using NUnit.Framework;

namespace Game.Tests.Application
{
    public class PerfTests
    {
        // ---------- 통계 ----------

        [Test]
        public void Percentile_UsesNearestRank()
        {
            double[] sorted = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            Assert.AreEqual(10, PerfMath.Percentile(sorted, 10, 100));
            Assert.AreEqual(10, PerfMath.Percentile(sorted, 10, 99));
            Assert.AreEqual(10, PerfMath.Percentile(sorted, 10, 95));
            Assert.AreEqual(5, PerfMath.Percentile(sorted, 10, 50));
            Assert.AreEqual(1, PerfMath.Percentile(sorted, 10, 1));
            Assert.AreEqual(0, PerfMath.Percentile(sorted, 0, 99));
        }

        [Test]
        public void OnePercentLow_AveragesWorstOnePercent()
        {
            // 100프레임: 99개는 10 ms, 1개는 50 ms -> 가장 느린 1프레임 = 50 ms -> 20 FPS
            var frames = new double[100];
            for (int i = 0; i < 99; i++) frames[i] = 10;
            frames[99] = 50;
            Assert.AreEqual(20.0, PerfMath.OnePercentLowFps(frames, 100), 1e-9);

            // 프레임이 100개 미만이어도 최소 1개를 본다.
            Assert.AreEqual(1000.0 / 30.0, PerfMath.OnePercentLowFps(new double[] { 10, 20, 30 }, 3), 1e-9);
            Assert.AreEqual(0, PerfMath.OnePercentLowFps(new double[0], 0));
        }

        // ---------- 집계기 ----------

        private static void FeedFrame(PerfAggregator agg, double now, double frameMs, double serverTotalMs, double maxCallMs, double main = -1)
        {
            for (int m = 0; m < PerfMarkers.All.Count; m++)
            {
                bool isServerTotal = PerfMarkers.All[m] == PerfMarkers.ServerTotal;
                agg.RecordMarker(m, isServerTotal ? serverTotalMs : 0, isServerTotal ? maxCallMs : 0);
            }
            agg.EndFrame(now, frameMs, main < 0 ? frameMs : main, 0, 100);
        }

        [Test]
        public void Aggregator_ProducesSnapshotEveryOneSecond()
        {
            var agg = new PerfAggregator();
            double now = 0;
            int snapshots = 0;
            for (int i = 0; i < 305; i++) // 3초 + 여유(부동소수점 누적 오차로 경계가 한 프레임 밀릴 수 있음)
            {
                now += 0.01; // 100 FPS
                for (int m = 0; m < PerfMarkers.All.Count; m++) agg.RecordMarker(m, 0, 0);
                if (agg.EndFrame(now, 10.0, 8.0, 5.0, 50)) snapshots++;
            }
            Assert.AreEqual(3, snapshots, "3초 동안 1초마다 1번");
            PerfSnapshot s = agg.Snapshot;
            Assert.AreEqual(100, s.Frames, 1);
            Assert.AreEqual(100.0, s.AverageFps, 0.5);
            Assert.AreEqual(10.0, s.FrameMsAverage, 1e-9);
            Assert.AreEqual(8.0, s.MainThreadMsAverage, 1e-9);
            Assert.AreEqual(5.0, s.GpuMsAverage, 1e-9);
            Assert.AreEqual(50L * s.Frames, s.GcAllocatedBytes);
        }

        [Test]
        public void Aggregator_MarkerStats_AveragePercentilesAndMaxCall()
        {
            var agg = new PerfAggregator();
            double now = 0;
            // 100프레임. 서버 합계: 98프레임은 2 ms, 2프레임은 8 ms(틱이 몰림). 한 번 호출 최대 8 ms.
            for (int i = 0; i < 100; i++)
            {
                now += 0.01;
                bool spike = i >= 98;
                FeedFrame(agg, now, 10, spike ? 8 : 2, spike ? 8 : 2);
            }
            PerfStat server = agg.Snapshot.GetMarker(PerfMarkers.ServerTotal);
            Assert.AreEqual((98 * 2 + 2 * 8) / 100.0, server.Average, 1e-9);
            Assert.AreEqual(2, server.P95, 1e-9);
            Assert.AreEqual(8, server.P99, 1e-9);
            Assert.AreEqual(8, server.Max, 1e-9);
            Assert.AreEqual(8, server.MaxCall, 1e-9);
            Assert.AreEqual(0, agg.Snapshot.GetMarker(PerfMarkers.ClientUi).Average);
        }

        [Test]
        public void Aggregator_NextWindow_DoesNotInheritOldMarkerValues()
        {
            var agg = new PerfAggregator();
            double now = 0;
            for (int i = 0; i < 100; i++) { now += 0.01; FeedFrame(agg, now, 10, 5, 5); }
            Assert.AreEqual(5, agg.Snapshot.GetMarker(PerfMarkers.ServerTotal).Average, 1e-9);

            // 다음 1초: 표식을 전혀 알리지 않는다 -> 0이어야 한다.
            for (int i = 0; i < 101; i++)
            {
                now += 0.01;
                if (agg.EndFrame(now, 10, 10, 0, 0)) break;
            }
            Assert.AreEqual(0, agg.Snapshot.GetMarker(PerfMarkers.ServerTotal).Average, 1e-9);
            Assert.AreEqual(0, agg.Snapshot.GetMarker(PerfMarkers.ServerTotal).MaxCall, 1e-9);
        }

        [Test]
        public void Aggregator_Counters_KeepLatestValue_AndIgnoreUnknownNames()
        {
            var agg = new PerfAggregator();
            agg.SetCounter(PerfCounters.NetRttMs, 30);
            agg.SetCounter(PerfCounters.NetRttMs, 42);
            agg.SetCounter("does.not.exist", 1);
            double now = 0;
            for (int i = 0; i < 101; i++)
            {
                now += 0.01;
                for (int m = 0; m < PerfMarkers.All.Count; m++) agg.RecordMarker(m, 0, 0);
                agg.EndFrame(now, 10, 10, 0, 0);
            }
            Assert.AreEqual(42, agg.Snapshot.GetCounter(PerfCounters.NetRttMs));
            Assert.AreEqual(0, agg.Snapshot.GetCounter(PerfCounters.ActiveCreatures));
            Assert.AreEqual(0, agg.Snapshot.GetCounter("does.not.exist"));
        }

        [Test]
        public void Aggregator_HandlesVeryHighFrameRate_WithoutOverflow()
        {
            var agg = new PerfAggregator();
            double now = 0;
            int snapshots = 0;
            for (int i = 0; i < 5100; i++) // 5,000 FPS: 초기 용량(256)을 넘는다
            {
                now += 0.0002;
                for (int m = 0; m < PerfMarkers.All.Count; m++) agg.RecordMarker(m, 0.01, 0.01);
                if (agg.EndFrame(now, 0.2, 0.2, 0, 0)) snapshots++;
            }
            Assert.GreaterOrEqual(snapshots, 1);
            Assert.AreEqual(5000, agg.Snapshot.AverageFps, 5);
        }

        // ---------- 예산 판정 ----------

        private static PerfSnapshot MakeSnapshot(double serverAvg, double serverP99, double serverMaxCall, double mainMs, double fps, double low)
        {
            var s = new PerfSnapshot
            {
                Frames = 60,
                AverageFps = fps,
                OnePercentLowFps = low,
                MainThreadMsAverage = mainMs,
                Markers = new PerfStat[PerfMarkers.All.Count],
                Counters = new double[PerfCounters.All.Count],
            };
            s.Markers[PerfMarkers.All.IndexOf(PerfMarkers.ServerTotal)] = new PerfStat(serverAvg, serverP99, serverP99, serverP99, serverMaxCall);
            return s;
        }

        [Test]
        public void Budget_Default_MatchesSddValues()
        {
            PerfBudget b = PerfBudget.Default;
            Assert.AreEqual(3.0, b.ServerTotalAverageMs);
            Assert.AreEqual(5.0, b.ServerTotalP99Ms);
            Assert.AreEqual(6.0, b.ServerTickMaxMs);
            Assert.AreEqual(13.0, b.MainThreadAverageMs);
            Assert.AreEqual(60.0, b.MinAverageFps);
            Assert.AreEqual(45.0, b.MinOnePercentLowFps);
        }

        [Test]
        public void BudgetChecker_PassesWithinBudget_AndReportsEachViolation()
        {
            Assert.IsEmpty(PerfBudgetChecker.Evaluate(MakeSnapshot(2.5, 4.5, 5.5, 11, 75, 55), PerfBudget.Default));

            List<PerfBudgetViolation> bad = PerfBudgetChecker.Evaluate(MakeSnapshot(3.5, 6.0, 7.0, 14, 50, 30), PerfBudget.Default);
            var codes = new List<string>();
            foreach (PerfBudgetViolation v in bad) codes.Add(v.Code);
            CollectionAssert.AreEquivalent(
                new[] { "Server.Total.Avg", "Server.Total.P99", "Server.Total.Tick", "MainThread.Avg", "Fps.Avg", "Fps.1PctLow" }, codes);
        }

        [Test]
        public void BudgetChecker_SkipsServerItems_WhenNotHost()
        {
            // Server.Total이 한 번도 불리지 않았으면(참가자) 서버 항목은 판정하지 않는다.
            List<PerfBudgetViolation> result = PerfBudgetChecker.Evaluate(MakeSnapshot(0, 0, 0, 10, 70, 50), PerfBudget.Default);
            Assert.IsEmpty(result);
            Assert.IsEmpty(PerfBudgetChecker.Evaluate(null, PerfBudget.Default));
        }

        // ---------- CSV ----------

        [Test]
        public void Csv_HeaderAndRow_HaveSameColumnCount_AndUseInvariantNumbers()
        {
            PerfSnapshot s = MakeSnapshot(2.5, 4.5, 5.5, 11.123456, 75.5, 55.25);
            s.ElapsedSeconds = 12.0;
            s.GcAllocatedBytes = 2048;
            string[] header = PerfCsv.Header().Split(',');
            string[] row = PerfCsv.Row(s).Split(',');
            Assert.AreEqual(PerfCsv.ColumnCount, header.Length);
            Assert.AreEqual(header.Length, row.Length);
            Assert.AreEqual("time_s", header[0]);
            Assert.Contains("Server.Total.avg", header);
            Assert.Contains("Server.Total.maxcall", header);
            Assert.Contains(PerfCounters.NetRttMs, header);
            Assert.AreEqual("11.123", row[Array.IndexOf(header, "main_ms_avg")], "소수점은 항상 점, 소수 3자리");
            Assert.AreEqual("2048", row[Array.IndexOf(header, "gc_alloc_bytes")]);
            Assert.AreEqual("5.5", row[Array.IndexOf(header, "Server.Total.maxcall")]);
        }

        // ---------- 측정기 포트 ----------

        [Test]
        public void NullProbe_AndScope_DoNothing()
        {
            using (NullPerfProbe.Instance.Scope(PerfMarkers.ServerTotal)) { }
            NullPerfProbe.Instance.Count("x", 1);
            using (((IPerfProbe)null).Scope(PerfMarkers.ServerTotal)) { }
            Assert.Pass();
        }

        private sealed class RecordingProbe : IPerfProbe
        {
            public readonly List<string> Calls = new List<string>();
            public void Begin(string marker) => Calls.Add("B:" + marker);
            public void End(string marker) => Calls.Add("E:" + marker);
            public void Count(string name, double value) => Calls.Add("C:" + name);
        }

        [Test]
        public void Scope_CallsBeginThenEnd_EvenWhenExceptionThrown()
        {
            var probe = new RecordingProbe();
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (probe.Scope(PerfMarkers.ServerCommands))
                {
                    throw new InvalidOperationException();
                }
            });
            CollectionAssert.AreEqual(new[] { "B:Server.Commands", "E:Server.Commands" }, probe.Calls);
        }

        // ---------- 명령줄 ----------

        [Test]
        public void Options_ParseAll()
        {
            PerfOptions o = PerfOptions.Parse(new[] { "game.exe", "-perflog", "D:/perf", "-perfhud", "-bench", "p8", "-upscale", "OFF", "-benchseconds", "42" });
            Assert.IsTrue(o.PerfLog);
            Assert.AreEqual("D:/perf", o.PerfLogFolder);
            Assert.IsTrue(o.Hud);
            Assert.AreEqual(BenchScenario.P8, o.Bench);
            Assert.AreEqual(UpscaleSwitch.Off, o.Upscale);
            Assert.AreEqual(42, o.BenchSeconds);
            Assert.IsEmpty(o.Errors);
        }

        [Test]
        public void Options_Defaults_AndBenchDurationPerScenario()
        {
            PerfOptions none = PerfOptions.Parse(new[] { "-batchmode" });
            Assert.IsFalse(none.PerfLog);
            Assert.IsNull(none.PerfLogFolder);
            Assert.IsFalse(none.Bench.HasValue);
            Assert.AreEqual(UpscaleSwitch.PresetDefault, none.Upscale);

            Assert.AreEqual(PerfOptions.DefaultP1Seconds, PerfOptions.Parse(new[] { "-bench", "P1" }).BenchSeconds);
            Assert.AreEqual(PerfOptions.DefaultP8Seconds, PerfOptions.Parse(new[] { "-bench", "P8" }).BenchSeconds);
            // -perflog 뒤에 다른 옵션이 바로 오면 폴더로 읽지 않는다.
            PerfOptions o = PerfOptions.Parse(new[] { "-perflog", "-perfhud" });
            Assert.IsTrue(o.PerfLog);
            Assert.IsNull(o.PerfLogFolder);
            Assert.IsTrue(o.Hud);
        }

        [Test]
        public void Options_VSync_OffOnAndDefault()
        {
            // [IMP-018] 옵션이 없으면 기존 동작(건드리지 않음)
            Assert.AreEqual(VSyncSwitch.Unchanged, PerfOptions.Parse(new[] { "-bench", "P8" }).VSync);
            Assert.AreEqual(VSyncSwitch.Unchanged, PerfOptions.None.VSync);
            Assert.AreEqual(VSyncSwitch.Off, PerfOptions.Parse(new[] { "-vsync", "OFF" }).VSync);
            Assert.AreEqual(VSyncSwitch.On, PerfOptions.Parse(new[] { "-vsync", "on" }).VSync);
            Assert.IsNotEmpty(PerfOptions.Parse(new[] { "-vsync", "half" }).Errors);
            Assert.IsNotEmpty(PerfOptions.Parse(new[] { "-vsync" }).Errors);
        }

        [Test]
        public void FlowRecomputeMarker_IsLast_AndPositionOfOtherColumnsUnchanged()
        {
            // [IMP-018] 기존 열 위치를 바꾸지 않으려고 새 표식은 맨 끝에 둔다.
            Assert.AreEqual(PerfMarkers.ServerFlowRecompute, PerfMarkers.All[PerfMarkers.All.Count - 1]);
            Assert.AreEqual(0, System.Linq.Enumerable.ToList(PerfMarkers.All).IndexOf(PerfMarkers.ServerTotal));
            StringAssert.Contains("Server.FlowRecompute.max", PerfCsv.Header());
        }

        [Test]
        public void NullProbe_Record_DoesNothing()
        {
            Assert.DoesNotThrow(() => NullPerfProbe.Instance.Record(PerfMarkers.ServerFlowRecompute, 1.5));
        }

        [Test]
        public void Options_ReportBadValues()
        {
            Assert.IsNotEmpty(PerfOptions.Parse(new[] { "-bench", "P3" }).Errors);
            Assert.IsNotEmpty(PerfOptions.Parse(new[] { "-bench" }).Errors);
            Assert.IsNotEmpty(PerfOptions.Parse(new[] { "-upscale", "maybe" }).Errors);
            Assert.IsNotEmpty(PerfOptions.Parse(new[] { "-bench", "P1", "-benchseconds", "-5" }).Errors);
            Assert.IsNotEmpty(PerfOptions.Parse(new[] { "-bench", "P1", "-benchseconds", "abc" }).Errors);
        }
    }

    internal static class ReadOnlyListExtensions
    {
        public static int IndexOf(this IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value) return i;
            }
            return -1;
        }
    }
}

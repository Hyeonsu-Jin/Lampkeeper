// =============================================================
// 파일: DiagnosticsTests.cs
// 역할: 성능 측정(ProfilerPerfProbe, PerfTelemetry, CSV 파일)과 파일 로그(FileLogSink)의 시험.
//       프레임 루프 대신 Tick을 직접 불러 시간 흐름을 흉내 낸다.
// 레이어: test (EditMode, Game.Tests.Infrastructure)
// 관련: FR-158, FR-163, NFR-021, NFR-042, DES-107, DES-108 / SDD M1-05
// =============================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Game.Application.Perf;
using Game.Infra.Platform.Logging;
using Game.Infra.Platform.Telemetry;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Infrastructure
{
    public class DiagnosticsTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "kh_diag_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_folder, true); } catch (IOException) { }
        }

        // ---------- ProfilerPerfProbe / PerfTelemetry ----------

        /// <summary>한 프레임을 흉내 낸다: serverMs만큼 Server.Total을 재고 Tick을 부른다.</summary>
        private static void SimulateFrame(PerfTelemetry t, ref double now, double serverMs, double frameMs = 10)
        {
            if (serverMs > 0)
            {
                t.Probe.Begin(PerfMarkers.ServerTotal);
                Thread.Sleep((int)serverMs);
                t.Probe.End(PerfMarkers.ServerTotal);
            }
            now += frameMs / 1000.0;
            t.Tick(now, frameMs, frameMs, 0, 0);
        }

        [Test]
        public void Probe_MeasuresRealElapsedTime_PerMarker()
        {
            using (var t = new PerfTelemetry(PerfBudget.Default, null, null))
            {
                double now = 0;
                for (int i = 0; i < 120; i++)
                {
                    SimulateFrame(t, ref now, i % 2 == 0 ? 5 : 0); // 짝수 프레임만 약 5 ms
                }
                Assert.IsNotNull(t.Latest, "1초가 지나면 통계가 나와야 합니다.");
                PerfStat server = t.Latest.GetMarker(PerfMarkers.ServerTotal);
                Assert.That(server.MaxCall, Is.InRange(4.5, 40), "한 번 호출 최대(ms)");
                Assert.That(server.Average, Is.InRange(1.5, 20), "프레임당 평균(절반 프레임에서 약 5 ms)");
                Assert.AreEqual(0, t.Latest.GetMarker(PerfMarkers.ClientUi).Average);
            }
        }

        [Test]
        public void Probe_NestedAndUnpairedCalls_AreSafe()
        {
            using (var t = new PerfTelemetry(null, null, null))
            {
                t.Probe.End(PerfMarkers.ServerTotal);                // 짝 없는 End
                t.Probe.Begin(PerfMarkers.ServerTotal);
                t.Probe.Begin(PerfMarkers.ServerTotal);              // 중첩: 바깥 구간만 잰다
                t.Probe.End(PerfMarkers.ServerTotal);
                t.Probe.End(PerfMarkers.ServerTotal);
                t.Probe.Count("unknown.counter", 5);                 // 모르는 지표는 무시
                Assert.Pass();
            }
        }

        [Test]
        public void Probe_UnknownMarkerName_WarnsOnce_AndIsIgnored()
        {
            using (var t = new PerfTelemetry(null, null, null))
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Typo.Marker"));
                t.Probe.Begin("Typo.Marker");
                t.Probe.End("Typo.Marker");
                t.Probe.Begin("Typo.Marker"); // 두 번째부터는 경고 없음
                t.Probe.End("Typo.Marker");
            }
        }

        [Test]
        public void Telemetry_BudgetViolation_IsWarned_WithCooldown()
        {
            var warnings = new List<string>();
            using (var t = new PerfTelemetry(new PerfBudget(1.0, 1.0, 1.0, 13, 0, 0), null, warnings.Add))
            {
                double now = 0;
                for (int i = 0; i < 260; i++)
                {
                    SimulateFrame(t, ref now, 3); // Server.Total 3 ms > 예산 1 ms
                }
                Assert.IsNotEmpty(t.LatestViolations);
                Assert.IsTrue(warnings.Exists(w => w.Contains("Server.Total.Avg")), "예산 초과 경고");
                int count = warnings.FindAll(w => w.Contains("Server.Total.Avg")).Count;
                Assert.AreEqual(1, count, "같은 경고는 쿨다운(" + PerfTelemetry.ViolationLogCooldownSeconds + "초) 동안 한 번만");
            }
        }

        [Test]
        public void Telemetry_WritesCsv_HeaderAndOneRowPerSecond()
        {
            string path = Path.Combine(_folder, "sub", "perf.csv");
            using (var t = new PerfTelemetry(PerfBudget.Default, path, null))
            {
                t.Probe.Count(PerfCounters.NetRttMs, 33);
                double now = 0;
                for (int i = 0; i < 250; i++) SimulateFrame(t, ref now, 0, 10);
            }
            string[] lines = File.ReadAllLines(path);
            Assert.GreaterOrEqual(lines.Length, 3, "머리글 + 2초 이상");
            Assert.AreEqual(PerfCsv.Header(), lines[0]);
            string[] header = lines[0].Split(',');
            for (int i = 1; i < lines.Length; i++)
            {
                string[] cells = lines[i].Split(',');
                Assert.AreEqual(header.Length, cells.Length);
                Assert.AreEqual("33", cells[Array.IndexOf(header, PerfCounters.NetRttMs)]);
            }
        }

        [Test]
        public void Telemetry_HudText_ShowsRequiredFr163Items()
        {
            using (var t = new PerfTelemetry(null, null, null))
            {
                StringAssert.Contains("측정 중", t.GetHudText());
                double now = 0;
                for (int i = 0; i < 120; i++) SimulateFrame(t, ref now, 1);
                string hud = t.GetHudText();
                StringAssert.Contains("FPS", hud);
                StringAssert.Contains("1% Low", hud);
                StringAssert.Contains("프레임", hud);
                StringAssert.Contains("송신", hud);
                StringAssert.Contains("수신", hud);
                StringAssert.Contains("RTT", hud);
                StringAssert.Contains("활동 적", hud);
                StringAssert.Contains("동기화 자원", hud);
            }
        }

        [Test]
        public void Telemetry_CsvPathUnwritable_ThrowsInsteadOfSilentlyIgnoring()
        {
            string blocker = Path.Combine(_folder, "file.txt");
            File.WriteAllText(blocker, "x");
            Assert.Catch<Exception>(() => new PerfTelemetry(null, Path.Combine(blocker, "perf.csv"), null));
        }

        // ---------- FileLogSink ----------

        [Test]
        public void FileLogSink_WritesSanitizedLines_AndHeader()
        {
            var now = new DateTime(2026, 11, 3, 1, 2, 3, DateTimeKind.Utc);
            string logPath;
            using (FileLogSink sink = FileLogSink.CreateUnhooked(_folder, now))
            {
                logPath = sink.FilePath;
                sink.OnLogMessage("접속 76561198000000001 token=abc123 from 8.8.8.8", "stack", LogType.Log);
                sink.OnLogMessage("실패 C:\\Users\\kim\\game", "at Foo.Bar() in C:\\Users\\kim\\x.cs:1", LogType.Error);
            }
            Assert.AreEqual("game_20261103_010203.log", Path.GetFileName(logPath));
            string text = File.ReadAllText(logPath);
            StringAssert.Contains("로그 시작", text);
            StringAssert.Contains("[Log] 접속 [steamid] token=[redacted] from 8.8.x.x", text);
            StringAssert.Contains("[Error] 실패 C:\\Users\\<user>\\game", text);
            StringAssert.Contains("at Foo.Bar() in C:\\Users\\<user>\\x.cs:1", text);
            StringAssert.DoesNotContain("76561198000000001", text);
            StringAssert.DoesNotContain("abc123", text);
            StringAssert.DoesNotContain("kim", text);
        }

        [Test]
        public void FileLogSink_NormalLogs_DoNotIncludeStackTrace()
        {
            string logPath;
            using (FileLogSink sink = FileLogSink.CreateUnhooked(_folder, DateTime.UtcNow))
            {
                logPath = sink.FilePath;
                sink.OnLogMessage("평범", "STACK_SHOULD_NOT_APPEAR", LogType.Log);
            }
            StringAssert.DoesNotContain("STACK_SHOULD_NOT_APPEAR", File.ReadAllText(logPath));
        }

        [Test]
        public void FileLogSink_AfterDispose_IgnoresFurtherWrites()
        {
            FileLogSink sink = FileLogSink.CreateUnhooked(_folder, DateTime.UtcNow);
            string path = sink.FilePath;
            sink.Dispose();
            sink.OnLogMessage("늦은 로그", "", LogType.Log);
            sink.Flush();
            sink.Dispose(); // 두 번 불러도 안전
            StringAssert.DoesNotContain("늦은 로그", File.ReadAllText(path));
        }

        [Test]
        public void FileLogSink_ConcurrentWrites_DoNotCorruptLines()
        {
            string logPath;
            using (FileLogSink sink = FileLogSink.CreateUnhooked(_folder, DateTime.UtcNow))
            {
                logPath = sink.FilePath;
                var threads = new List<Thread>();
                for (int t = 0; t < 4; t++)
                {
                    int id = t;
                    var thread = new Thread(() =>
                    {
                        for (int i = 0; i < 200; i++) sink.OnLogMessage("T" + id + "-" + i, "", LogType.Log);
                    });
                    threads.Add(thread);
                    thread.Start();
                }
                threads.ForEach(th => th.Join());
            }
            int count = 0;
            foreach (string line in File.ReadAllLines(logPath))
            {
                if (line.Contains("[Log] T")) count++;
            }
            Assert.AreEqual(800, count);
        }
    }
}

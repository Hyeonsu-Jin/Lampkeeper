// 파일: AbuseGuardTests.cs
// 역할: 연결 남용 방어 도구(PoseRateGate, AbuseMeter)와 반복 로그 묶음(ThrottledLog), 부하 시험. [IMP-004, SEC-003, REV-007]
using System.Collections.Generic;
using Game.Application.Logging;
using Game.Application.Ports;
using Game.Application.Server;
using NUnit.Framework;

namespace Game.Tests.Application
{
    public class AbuseGuardTests
    {
        private sealed class CountLog : ILog
        {
            public readonly List<string> Lines = new List<string>();
            public void Info(string category, string message) => Lines.Add("I:" + category + ":" + message);
            public void Warn(string category, string message) => Lines.Add("W:" + category + ":" + message);
            public void Error(string category, string message) => Lines.Add("E:" + category + ":" + message);
        }

        [Test]
        public void PoseRateGate_AllowsSendRateTimesOnePointFive_DropsFlood()
        {
            var gate = new PoseRateGate(30, 0d);
            int allowed = 0;
            for (int i = 0; i < 5000; i++) { if (gate.Allow(i / 5000d)) { allowed++; } } // 1초에 5000개
            Assert.LessOrEqual(allowed, 45 + PoseRateGate.BurstCapacity);
            Assert.GreaterOrEqual(allowed, 45);
        }

        [Test]
        public void PoseRateGate_HonestClient_AtSendRate_IsNeverDropped()
        {
            var gate = new PoseRateGate(30, 0d);
            for (int i = 0; i < 300; i++) { Assert.IsTrue(gate.Allow(i / 30d)); }
        }

        [Test]
        public void AbuseMeter_OverLimitInWindow_ReportsDisconnect_ButSlowFailuresDoNot()
        {
            var fast = new AbuseMeter(50, 10d);
            bool cut = false;
            for (int i = 0; i < 100 && !cut; i++) { cut = fast.Record(i * 0.01); }
            Assert.IsTrue(cut);

            var slow = new AbuseMeter(50, 10d);
            for (int i = 0; i < 500; i++) { Assert.IsFalse(slow.Record(i * 1.0), "초당 1건은 한도 안"); }
        }

        [Test]
        public void TC067_10초에_50회까지는_유지하고_51회째에_연결_종료를_알린다()
        {
            var meter = new AbuseMeter(AbuseMeter.DefaultLimit, AbuseMeter.DefaultWindowSeconds);
            for (int i = 1; i <= 50; i++) { Assert.IsFalse(meter.Record(i * 0.1), "50회째까지는 종료하지 않는다: " + i); }
            Assert.IsTrue(meter.Record(5.1), "51회째에 종료");
        }

        [Test]
        public void ThrottledLog_Flood_WritesOnePerWindow_WithSuppressedCount()
        {
            var inner = new CountLog();
            var clock = new FakeClock();
            var log = new ThrottledLog(inner, clock, 5d);
            for (int i = 0; i < 10000; i++) { log.Warn("move_rejected", "x"); }
            Assert.AreEqual(1, inner.Lines.Count);
            clock.Advance(5.1);
            log.Warn("move_rejected", "y");
            Assert.AreEqual(2, inner.Lines.Count);
            StringAssert.Contains("9999건 생략", inner.Lines[1]);
            log.Warn("other", "z");
            Assert.AreEqual(3, inner.Lines.Count, "다른 분류는 따로 센다");
        }

        [Test]
        public void ThrottledLog_Error_IsNeverThrottled()
        {
            var inner = new CountLog();
            var log = new ThrottledLog(inner, new FakeClock());
            for (int i = 0; i < 5; i++) { log.Error("e", "x"); }
            Assert.AreEqual(5, inner.Lines.Count);
        }

        [Test]
        public void Load_PoseFlood_ThroughGateAndLog_StaysBounded()
        {
            // [NFR-041 부하 시험] 초당 5000개 자세를 쏟아도 통과·로그가 상한 안이고 연결 종료로 끝난다.
            var inner = new CountLog();
            var clock = new FakeClock();
            var log = new ThrottledLog(inner, clock);
            var gate = new PoseRateGate(30, 0d);
            var meter = new AbuseMeter(50, 10d);
            int passed = 0;
            bool cut = false;
            for (int i = 0; i < 50000 && !cut; i++)
            {
                clock.RealtimeSeconds = i / 5000d;
                if (gate.Allow(clock.RealtimeSeconds)) { passed++; continue; }
                log.Warn("pose_dropped", "player=1");
                cut = meter.Record(clock.RealtimeSeconds);
            }
            Assert.IsTrue(cut);
            Assert.Less(passed, 100);
            Assert.Less(inner.Lines.Count, 5);
        }
    }
}

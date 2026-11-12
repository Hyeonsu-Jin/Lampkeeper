// 파일: FileLogSinkLimitTests.cs
// 역할: 파일 로그 크기 상한과 오래된 로그 정리 시험. [IMP-004, SEC-003, NFR-045]
using System;
using System.IO;
using System.Linq;
using Game.Infra.Platform.Logging;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Infrastructure
{
    public class FileLogSinkLimitTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "logsink_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) { Directory.Delete(_dir, true); }
        }

        [Test]
        public void FileSize_IsCapped_UnderFlood()
        {
            using (FileLogSink sink = FileLogSink.CreateUnhooked(_dir, new DateTime(2026, 11, 10, 1, 0, 0, DateTimeKind.Utc), 10_000, 10))
            {
                for (int i = 0; i < 100_000; i++) { sink.OnLogMessage("경고 반복 " + i, "", LogType.Warning); }
                sink.Flush();
                long size = new FileInfo(sink.FilePath).Length;
                Assert.Less(size, 10_000 + 2_000, "상한 근처에서 멈춤(한글 UTF-8 여유 포함)... 줄 수 기준 상한");
            }
        }

        [Test]
        public void OldLogs_AreDeleted_KeepingNewest()
        {
            for (int day = 1; day <= 15; day++)
            {
                File.WriteAllText(Path.Combine(_dir, "game_202611" + day.ToString("00") + "_000000.log"), "x");
            }
            int deleted = FileLogSink.CleanupOldLogs(_dir, 10);
            Assert.AreEqual(5, deleted);
            string[] left = Directory.GetFiles(_dir, "game_*.log").Select(Path.GetFileName).OrderBy(n => n).ToArray();
            Assert.AreEqual(10, left.Length);
            Assert.AreEqual("game_20261106_000000.log", left[0], "가장 오래된 5개가 지워짐");
        }

        [Test]
        public void NewSink_KeepsAtMostKeepFiles_IncludingItself()
        {
            for (int day = 1; day <= 12; day++)
            {
                File.WriteAllText(Path.Combine(_dir, "game_202611" + day.ToString("00") + "_000000.log"), "x");
            }
            using (FileLogSink.CreateUnhooked(_dir, new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc), 1_000_000, 10))
            {
                Assert.AreEqual(10, Directory.GetFiles(_dir, "game_*.log").Length);
            }
        }
    }
}

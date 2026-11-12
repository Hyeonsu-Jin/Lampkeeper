// =============================================================
// 파일: LogSanitizerTests.cs
// 역할: 로그 민감 정보 마스킹(Steam ID, 비밀 값, 사용자 폴더 이름, 외부 IP)의 단위 시험.
// 레이어: test (EditMode, Game.Tests.Application)
// 관련: FR-158, NFR-042, NFR-045, DES-107 / SDD M1-05
// =============================================================
using System;
using Game.Application.Logging;
using NUnit.Framework;

namespace Game.Tests.Application
{
    public class LogSanitizerTests
    {
        [Test]
        public void Empty_AndNull_AreSafe()
        {
            Assert.AreEqual(string.Empty, LogSanitizer.Sanitize(null));
            Assert.AreEqual(string.Empty, LogSanitizer.Sanitize(string.Empty));
            Assert.AreEqual("평범한 로그 12:34:56", LogSanitizer.Sanitize("평범한 로그 12:34:56"));
        }

        [Test]
        public void SteamId_IsMasked_OnlyWhenSeventeenDigitsWithPrefix()
        {
            Assert.AreEqual("플레이어 [steamid] 접속", LogSanitizer.Sanitize("플레이어 76561198000000001 접속"));
            Assert.AreEqual("a=[steamid],b=[steamid]", LogSanitizer.Sanitize("a=76561198000000001,b=76561199999999999"));
            // 16자리, 18자리, 접두사가 다른 17자리는 그대로
            Assert.AreEqual("7656119800000001", LogSanitizer.Sanitize("7656119800000001"));
            Assert.AreEqual("765611980000000012", LogSanitizer.Sanitize("765611980000000012"));
            Assert.AreEqual("12345678901234567", LogSanitizer.Sanitize("12345678901234567"));
        }

        [TestCase("password=hunter2", "password=[redacted]")]
        [TestCase("Password: hunter2 end", "Password: [redacted] end")]
        [TestCase("login ok token=abc123xyz;next", "login ok token=[redacted];next")]
        [TestCase("ticket = ABCDEF012345", "ticket = [redacted]")]
        [TestCase("{\"apikey\":\"K-12345\"}", "{\"apikey\":\"[redacted]\"}")]
        [TestCase("secret=a&other=1", "secret=[redacted]&other=1")]
        public void SecretValues_AreMasked(string input, string expected)
        {
            Assert.AreEqual(expected, LogSanitizer.Sanitize(input));
        }

        [Test]
        public void SecretKeyWithoutValue_IsLeftAlone()
        {
            Assert.AreEqual("token expired", LogSanitizer.Sanitize("token expired"));
            Assert.AreEqual("password=", LogSanitizer.Sanitize("password="));
        }

        [TestCase("C:\\Users\\kim\\AppData\\LocalLow\\Co\\Game\\Logs", "C:\\Users\\<user>\\AppData\\LocalLow\\Co\\Game\\Logs")]
        [TestCase("c:\\users\\Bob Smith\\x", "c:\\users\\<user> Smith\\x")]
        [TestCase("/Users/alice/Library/Logs", "/Users/<user>/Library/Logs")]
        [TestCase("/home/dev/.config/x", "/home/<user>/.config/x")]
        [TestCase("E:\\Claude_Project\\Game\\src", "E:\\Claude_Project\\Game\\src")]
        public void UserFolderName_IsMasked(string input, string expected)
        {
            Assert.AreEqual(expected, LogSanitizer.Sanitize(input));
        }

        [TestCase("연결 192.168.0.15:7770", "연결 192.168.x.x:7770")]
        [TestCase("peer 203.0.113.42 left", "peer 203.0.x.x left")]
        [TestCase("local 127.0.0.1:7770", "local 127.0.0.1:7770")]
        [TestCase("any 0.0.0.0", "any 0.0.0.0")]
        [TestCase("버전 6000.3.27.1", "버전 6000.3.27.1")]
        [TestCase("not ip 1.2.3", "not ip 1.2.3")]
        [TestCase("범위 밖 300.1.1.1", "범위 밖 300.1.1.1")]
        public void ExternalIpv4_IsMasked_LoopbackAndVersionsAreKept(string input, string expected)
        {
            Assert.AreEqual(expected, LogSanitizer.Sanitize(input));
        }

        [Test]
        public void MultipleKindsInOneLine_AreAllMasked()
        {
            string line = "joined 76561198000000001 from 10.20.30.40 token=abc at C:\\Users\\kim\\x";
            string result = LogSanitizer.Sanitize(line);
            StringAssert.DoesNotContain("76561198000000001", result);
            StringAssert.DoesNotContain("10.20.30.40", result);
            StringAssert.DoesNotContain("abc", result);
            StringAssert.DoesNotContain("kim", result);
            StringAssert.Contains(LogSanitizer.SteamIdMask, result);
        }

        [Test]
        public void Formatter_PutsTimeLevelAndSanitizedMessage()
        {
            string line = LogLineFormatter.Format(new DateTime(2026, 11, 3, 9, 5, 7, 12, DateTimeKind.Utc), "Warning", "ticket=XYZ 접속");
            Assert.AreEqual("2026-11-03 09:05:07.012 [Warning] ticket=[redacted] 접속", line);
        }
    }
}

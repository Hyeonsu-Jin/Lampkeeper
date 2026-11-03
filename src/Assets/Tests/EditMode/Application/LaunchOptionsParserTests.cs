// =============================================================
// 파일: LaunchOptionsParserTests.cs
// 역할: 개발용 실행 옵션(-transport, -host, -connect, -devPlayerId) 해석 시험.
// 레이어: test (EditMode, Game.Tests.Application)
// 관련: FR-006, DES-103 / SDD 3.7.4
// =============================================================
using Game.Application.Boot;
using NUnit.Framework;

namespace Game.Tests.Application
{
    public class LaunchOptionsParserTests
    {
        private static LaunchOptions Parse(params string[] args) => LaunchOptionsParser.Parse(args);

        [Test]
        public void NoArgs_ReturnsNoOptions()
        {
            LaunchOptions o = Parse();
            Assert.IsFalse(o.HasAnyDevOption);
            Assert.IsEmpty(o.Errors);
        }

        [Test]
        public void Null_IsSafe()
        {
            Assert.IsFalse(LaunchOptionsParser.Parse(null).HasAnyDevOption);
        }

        [TestCase("tugboat", TransportKind.Tugboat)]
        [TestCase("TUGBOAT", TransportKind.Tugboat)]
        [TestCase("steam", TransportKind.Steam)]
        public void Transport_IsParsed_CaseInsensitive(string value, TransportKind expected)
        {
            LaunchOptions o = Parse("game.exe", "-transport", value);
            Assert.AreEqual(expected, o.RequestedTransport);
            Assert.IsEmpty(o.Errors);
        }

        [Test]
        public void Transport_UnknownValue_RecordsError()
        {
            LaunchOptions o = Parse("-transport", "udp");
            Assert.IsNull(o.RequestedTransport);
            Assert.AreEqual(1, o.Errors.Count);
        }

        [Test]
        public void Transport_MissingValue_RecordsError()
        {
            LaunchOptions o = Parse("-transport");
            Assert.IsNull(o.RequestedTransport);
            Assert.AreEqual(1, o.Errors.Count);
        }

        [Test]
        public void Transport_NextArgIsAnotherFlag_RecordsError()
        {
            LaunchOptions o = Parse("-transport", "-host");
            // -transport 뒤에 값이 없으므로 오류. -host는 정상 해석되어 Tugboat로 간주된다.
            Assert.IsNotEmpty(o.Errors);
            Assert.IsTrue(o.Host);
            Assert.AreEqual(TransportKind.Tugboat, o.RequestedTransport);
        }

        [Test]
        public void Host_ImpliesTugboat_WhenTransportNotGiven()
        {
            LaunchOptions o = Parse("-host");
            Assert.IsTrue(o.Host);
            Assert.AreEqual(TransportKind.Tugboat, o.RequestedTransport);
        }

        [Test]
        public void Connect_ParsesEndpoint_AndImpliesTugboat()
        {
            LaunchOptions o = Parse("-connect", "127.0.0.1:7770");
            Assert.AreEqual(new NetworkEndpoint("127.0.0.1", 7770), o.Connect);
            Assert.AreEqual(TransportKind.Tugboat, o.RequestedTransport);
        }

        [TestCase("127.0.0.1")]
        [TestCase("127.0.0.1:")]
        [TestCase(":7770")]
        [TestCase("host:abc")]
        [TestCase("host:0")]
        [TestCase("host:70000")]
        public void Connect_InvalidEndpoint_RecordsError(string value)
        {
            LaunchOptions o = Parse("-connect", value);
            Assert.IsFalse(o.Connect.HasValue);
            Assert.AreEqual(1, o.Errors.Count);
        }

        [Test]
        public void HostWithSteam_RecordsError()
        {
            LaunchOptions o = Parse("-transport", "steam", "-host");
            Assert.IsNotEmpty(o.Errors);
        }

        [Test]
        public void HostAndConnectTogether_RecordsError()
        {
            LaunchOptions o = Parse("-host", "-connect", "localhost:7770");
            Assert.IsNotEmpty(o.Errors);
        }

        [Test]
        public void DevPlayerId_IsParsed()
        {
            LaunchOptions o = Parse("-devPlayerId", "2");
            Assert.AreEqual(2UL, o.DevPlayerId);
        }

        [TestCase("0")]
        [TestCase("-3")]
        [TestCase("abc")]
        [TestCase("1.5")]
        public void DevPlayerId_Invalid_RecordsError(string value)
        {
            LaunchOptions o = Parse("-devPlayerId", value);
            Assert.IsFalse(o.DevPlayerId.HasValue);
            Assert.IsNotEmpty(o.Errors);
        }

        [Test]
        public void UnknownOptions_AreIgnored()
        {
            LaunchOptions o = Parse("-batchmode", "-nographics", "-logFile", "x.log", "-transport", "tugboat");
            Assert.AreEqual(TransportKind.Tugboat, o.RequestedTransport);
            Assert.IsEmpty(o.Errors);
        }

        [Test]
        public void TypicalTwoClientSetup_Parses()
        {
            LaunchOptions o = Parse("-transport", "tugboat", "-connect", "127.0.0.1:7770", "-devPlayerId", "2");
            Assert.AreEqual(TransportKind.Tugboat, o.RequestedTransport);
            Assert.AreEqual(2UL, o.DevPlayerId);
            Assert.IsTrue(o.Connect.HasValue);
            Assert.IsEmpty(o.Errors);
        }
    }
}

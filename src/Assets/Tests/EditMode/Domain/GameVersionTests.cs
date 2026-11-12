// =============================================================
// 파일: GameVersionTests.cs
// 역할: 게임 버전 값 객체(GameVersion)의 해석, 같은 빌드 판정, 표시 형식, 메타데이터 조립 단위 시험.
// 레이어: test (EditMode, Game.Tests.Domain)
// 관련: NFR-058, FR-003, DES-037 / SDD 3.7.12
// =============================================================
using System;
using System.Text;
using Game.Domain.Common;
using NUnit.Framework;

namespace Game.Tests.Domain
{
    public class GameVersionTests
    {
        private static GameVersion Parse(string text)
        {
            Assert.IsTrue(GameVersion.TryParse(text, out GameVersion version), "해석 실패: " + text);
            return version;
        }

        [TestCase("0.1.0")]
        [TestCase("1.0.0")]
        [TestCase("65535.65535.65535")]
        [TestCase("0.3.0-alpha.1")]
        [TestCase("1.0.0-rc.1")]
        [TestCase("1.0.0-0")]
        [TestCase("1.0.0-x-y.z")]
        [TestCase("0.1.0+editor")]
        [TestCase("0.1.0+dev.20261104153012.a1b2c3d")]
        [TestCase("0.1.0+dev.20261104153012.a1b2c3d.dirty")]
        [TestCase("1.0.0-rc.1+rel.20270301120000.0c1d2e3")]
        [TestCase("1.0.0+001")] // 메타데이터 식별자는 앞자리 0 허용
        public void ValidStrings_Parse_AndFullIsOriginal(string text)
        {
            GameVersion version = Parse(text);
            Assert.AreEqual(text, version.Full);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("1")]
        [TestCase("1.0")]
        [TestCase("1.0.0.0")]
        [TestCase("01.0.0")]
        [TestCase("1.00.0")]
        [TestCase("1.0.00")]
        [TestCase("1.0.0-")]
        [TestCase("1.0.0-01")]
        [TestCase("1.0.0-a..b")]
        [TestCase("1.0.0+")]
        [TestCase("1.0.0+a..b")]
        [TestCase("1.0.0+a+b")]
        [TestCase("1.0.0+.a")]
        [TestCase("65536.0.0")]
        [TestCase("0.65536.0")]
        [TestCase("0.0.65536")]
        [TestCase("-1.0.0")]
        [TestCase("v1.0.0")]
        [TestCase(" 1.0.0")]
        [TestCase("1.0.0 ")]
        [TestCase("1.0.0-한글")]
        [TestCase("1.0.0+한글")]
        [TestCase("1.a.0")]
        public void InvalidStrings_AreRejected_WithoutException(string text)
        {
            Assert.IsFalse(GameVersion.TryParse(text, out GameVersion version));
            Assert.IsNull(version);
        }

        [Test]
        public void Length_64IsAllowed_65IsRejected()
        {
            string core = "1.0.0+";
            string ok = core + new string('a', 64 - core.Length);
            string tooLong = core + new string('a', 65 - core.Length);
            Assert.AreEqual(64, Encoding.UTF8.GetByteCount(ok));
            Assert.IsTrue(GameVersion.TryParse(ok, out _));
            Assert.AreEqual(65, Encoding.UTF8.GetByteCount(tooLong));
            Assert.IsFalse(GameVersion.TryParse(tooLong, out _));
        }

        [Test]
        public void Parts_AreSplitCorrectly()
        {
            GameVersion v = Parse("1.2.3-rc.1+rel.20270301120000.0c1d2e3");
            Assert.AreEqual(1, v.Major);
            Assert.AreEqual(2, v.Minor);
            Assert.AreEqual(3, v.Patch);
            Assert.AreEqual("rc.1", v.PreRelease);
            Assert.AreEqual("rel.20270301120000.0c1d2e3", v.BuildMetadata);
            Assert.AreEqual("1.2.3-rc.1", v.Core);
            Assert.AreEqual("rel", v.Channel);

            GameVersion plain = Parse("0.1.0");
            Assert.AreEqual(string.Empty, plain.PreRelease);
            Assert.AreEqual(string.Empty, plain.BuildMetadata);
            Assert.AreEqual("0.1.0", plain.Core);
            Assert.AreEqual(string.Empty, plain.Channel);
        }

        [TestCase("0.1.0+dev.1.abc", "dev")]
        [TestCase("0.1.0+rel.1.abc", "rel")]
        [TestCase("0.1.0+editor", "editor")]
        [TestCase("0.1.0+other.1", "")]
        [TestCase("0.1.0", "")]
        public void Channel_IsFirstMetadataIdentifier(string text, string expected)
        {
            Assert.AreEqual(expected, Parse(text).Channel);
        }

        [Test]
        public void IsSameBuild_ComparesWholeStringIncludingMetadata()
        {
            string a = "0.1.0+dev.20261104153012.a1b2c3d";
            Assert.IsTrue(Parse(a).IsSameBuild(Parse(a)));
            Assert.IsFalse(Parse(a).IsSameBuild(Parse("0.1.0+dev.20261104153012.a1b2c3d.dirty")), "dirty만 달라도 다른 빌드");
            Assert.IsFalse(Parse(a).IsSameBuild(Parse("0.1.0+dev.20261104153013.a1b2c3d")), "시각이 다르면 다른 빌드");
            Assert.IsFalse(Parse(a).IsSameBuild(Parse("0.1.0+dev.20261104153012.a1b2c3e")), "커밋이 다르면 다른 빌드");
            Assert.IsFalse(Parse(a).IsSameBuild(Parse("0.1.0+rel.20261104153012.a1b2c3d")), "채널이 다르면 다른 빌드");
            Assert.IsFalse(Parse("0.1.0").IsSameBuild(Parse("0.1.0+editor")));
            Assert.IsFalse(Parse("0.1.0-rc.1").IsSameBuild(Parse("0.1.0")));
            Assert.IsFalse(Parse("0.1.0").IsSameBuild(null));
        }

        [Test]
        public void IsSameBuild_IsCaseSensitive()
        {
            Assert.IsFalse(Parse("1.0.0-RC.1").IsSameBuild(Parse("1.0.0-rc.1")));
        }

        [TestCase("0.1.0+rel.20261110090000.9f8e7d6", "v0.1.0")]
        [TestCase("1.0.0-rc.1+rel.20270301120000.0c1d2e3", "v1.0.0-rc.1")]
        [TestCase("0.1.0+dev.20261104153012.a1b2c3d", "v0.1.0+dev.20261104153012.a1b2c3d")]
        [TestCase("0.1.0+editor", "v0.1.0+editor")]
        [TestCase("0.1.0", "v0.1.0")]
        public void ToDisplayString_ReleaseShowsCore_OthersShowFull(string text, string expected)
        {
            Assert.AreEqual(expected, Parse(text).ToDisplayString());
        }

        [Test]
        public void ComposeBuildMetadata_FollowsFormat()
        {
            var utc = new DateTime(2026, 11, 4, 15, 30, 12, DateTimeKind.Utc);
            Assert.AreEqual("dev.20261104153012.a1b2c3d", GameVersion.ComposeBuildMetadata("dev", utc, "a1b2c3d", false));
            Assert.AreEqual("dev.20261104153012.a1b2c3d.dirty", GameVersion.ComposeBuildMetadata("dev", utc, "a1b2c3d", true));
            Assert.AreEqual("rel.20261104153012.a1b2c3d", GameVersion.ComposeBuildMetadata("rel", utc, "a1b2c3d", false));
            Assert.AreEqual("rel.20261104153012.nogit", GameVersion.ComposeBuildMetadata("rel", utc, "nogit", false));
            Assert.AreEqual("dev.20261104153012.nogit", GameVersion.ComposeBuildMetadata("dev", utc, null, false));
            Assert.AreEqual("dev.20261104153012.nogit", GameVersion.ComposeBuildMetadata("dev", utc, "  ", false), "허용 글자가 없으면 nogit");
            Assert.AreEqual("dev.20261104153012.a1b2c3d", GameVersion.ComposeBuildMetadata("dev", utc, "a1b2c3d4e5f6", false), "앞 7자만");
        }

        [Test]
        public void ComposedMetadata_AppendedToCore_IsAlwaysParseable()
        {
            var utc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            foreach (string core in new[] { "0.1.0", "1.0.0-rc.1", "65535.65535.65535" })
            {
                foreach (bool dirty in new[] { false, true })
                {
                    string full = core + "+" + GameVersion.ComposeBuildMetadata("dev", utc, "abc1234", dirty);
                    GameVersion v = Parse(full);
                    Assert.AreEqual(core, v.Core);
                    Assert.AreEqual("dev", v.Channel);
                    Assert.LessOrEqual(Encoding.UTF8.GetByteCount(full), GameVersion.MaxBytes);
                }
            }
        }
    }
}

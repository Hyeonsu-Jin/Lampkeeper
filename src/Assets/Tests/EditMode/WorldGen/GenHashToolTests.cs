// =============================================================
// 파일: GenHashToolTests.cs
// 역할: -genhash / -genhash-compare 도구(Game.Application.Dev)의 시험. 옵션 해석, 시드 목록, JSON 왕복,
//       보고서 비교(불일치 시드·타일 찾기).
// 레이어: test (EditMode, Game.Tests.WorldGen)
// 관련: NFR-053, DES-113 / SDD 3.7.4, M1-04
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Dev;
using NUnit.Framework;

namespace Game.Tests.WorldGen
{
    public class GenHashToolTests
    {
        [Test]
        public void Options_ParseGenerate()
        {
            GenHashOptions o = GenHashOptions.Parse(new[] { "game.exe", "-batchmode", "-genhash", "seeds.txt", "-out", "r.json", "-genver", "V0", "-genprofile", "SMALL" });
            Assert.AreEqual(GenHashMode.Generate, o.Mode);
            Assert.AreEqual("seeds.txt", o.SeedsPath);
            Assert.AreEqual("r.json", o.OutPath);
            Assert.AreEqual("V0", o.GeneratorVersion);
            Assert.AreEqual("small", o.ProfileName);
            CollectionAssert.IsEmpty(o.Errors);
        }

        [Test]
        public void Options_ParseCompare_CollectsPathsUntilNextFlag()
        {
            GenHashOptions o = GenHashOptions.Parse(new[] { "-genhash-compare", "a.json", "b.json", "c.json", "-batchmode" });
            Assert.AreEqual(GenHashMode.Compare, o.Mode);
            CollectionAssert.AreEqual(new[] { "a.json", "b.json", "c.json" }, o.ComparePaths);
            CollectionAssert.IsEmpty(o.Errors);
        }

        [Test]
        public void Options_ReportMissingValuesAndBadVersion()
        {
            Assert.AreEqual(GenHashMode.None, GenHashOptions.Parse(new[] { "-batchmode" }).Mode);
            Assert.IsNotEmpty(GenHashOptions.Parse(new[] { "-genhash", "seeds.txt" }).Errors, "-out 없음");
            Assert.IsNotEmpty(GenHashOptions.Parse(new[] { "-genhash", "s.txt", "-out", "o.json", "-genver", "V9" }).Errors);
            Assert.IsNotEmpty(GenHashOptions.Parse(new[] { "-genhash-compare", "only-one.json" }).Errors);
            Assert.IsNotEmpty(GenHashOptions.Parse(new[] { "-genhash", "s.txt", "-out", "o.json", "-genprofile", "huge" }).Errors);
        }

        [Test]
        public void SeedList_SkipsBlankLinesAndComments()
        {
            List<string> seeds = SeedListParser.Parse("# 주석\r\nalpha\r\n\r\n  beta  \n#x\ngamma");
            CollectionAssert.AreEqual(new[] { "alpha", "  beta  ", "gamma" }, seeds);
        }

        [Test]
        public void Report_Json_RoundTrips()
        {
            GenHashReport report = GenHashRunner.Run(new[] { "round-trip", "한글 시드" }, "small");
            report.Runtime = "Mono";
            report.Unity = "6000.3.27f2";
            report.Cpu = "TestCPU \"quoted\" x12";
            report.Machine = "PC\\1";

            GenHashReport read = GenHashReport.FromJson(report.ToJson());
            Assert.AreEqual("Mono", read.Runtime);
            Assert.AreEqual("TestCPU \"quoted\" x12", read.Cpu);
            Assert.AreEqual("PC\\1", read.Machine);
            Assert.AreEqual(2, read.Entries.Count);
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(report.Entries[i].SeedText, read.Entries[i].SeedText);
                Assert.AreEqual(report.Entries[i].Seed, read.Entries[i].Seed);
                Assert.AreEqual(report.Entries[i].Hash, read.Entries[i].Hash);
                CollectionAssert.AreEqual(report.Entries[i].TileHashes, read.Entries[i].TileHashes);
                CollectionAssert.AreEqual(report.Entries[i].RegionHashes, read.Entries[i].RegionHashes);
            }
        }

        [Test]
        public void Compare_SameReports_Match()
        {
            GenHashReport a = GenHashRunner.Run(new[] { "cmp-1", "cmp-2" }, "small");
            GenHashReport b = GenHashRunner.Run(new[] { "cmp-1", "cmp-2" }, "small", 1);
            GenHashComparison result = GenHashComparer.Compare(new[] { a, b });
            Assert.IsTrue(result.AllMatch, result.Describe(new[] { "a", "b" }));
            Assert.AreEqual(2, result.SeedsCompared);
        }

        [Test]
        public void Compare_DetectsMismatch_AndPointsAtTile()
        {
            GenHashReport a = GenHashRunner.Run(new[] { "cmp-1" }, "small");
            GenHashReport b = GenHashReport.FromJson(a.ToJson());
            b.Entries[0].Hash ^= 1UL;
            b.Entries[0].TileHashes[5] ^= 1UL;
            b.Entries[0].ResourcesHash ^= 1UL;

            GenHashComparison result = GenHashComparer.Compare(new[] { a, b });
            Assert.IsFalse(result.AllMatch);
            Assert.AreEqual(1, result.Mismatches.Count);
            CollectionAssert.AreEqual(new[] { 5 }, result.Mismatches[0].DifferentTiles);
            Assert.IsTrue(result.Mismatches[0].ResourcesDiffer);
            Assert.IsFalse(result.Mismatches[0].TowerSitesDiffer);
            StringAssert.Contains("cmp-1", result.Describe(new[] { "a", "b" }));
        }

        [Test]
        public void Compare_MissingSeedAndProfileMismatch_AreReported()
        {
            GenHashReport a = GenHashRunner.Run(new[] { "x1" }, "small");
            GenHashReport other = GenHashRunner.Run(new[] { "x2" }, "small");
            Assert.IsFalse(GenHashComparer.Compare(new[] { a, other }).AllMatch);

            GenHashReport b = GenHashReport.FromJson(a.ToJson());
            b.Profile = "full";
            Assert.IsFalse(GenHashComparer.Compare(new[] { a, b }).AllMatch);
        }

        [Test]
        public void Runner_RejectsInvalidSeedText()
        {
            Assert.Throws<ArgumentException>(() => GenHashRunner.Run(new[] { new string('a', 100) }, "small"));
        }

        [Test]
        public void MiniJson_ParsesBasicShapes_AndRejectsGarbage()
        {
            var root = (Dictionary<string, object>)MiniJson.Parse("{\"a\": [1, -2, 3.5, true, false, null, \"x\\n\\u0041\"], \"b\": {}}");
            List<object> a = MiniJson.GetArray(root, "a");
            Assert.AreEqual(1L, a[0]);
            Assert.AreEqual(-2L, a[1]);
            Assert.AreEqual(3.5, a[2]);
            Assert.AreEqual(true, a[3]);
            Assert.IsNull(a[5]);
            Assert.AreEqual("x\nA", a[6]);
            Assert.IsNotNull(MiniJson.GetObject(root, "b"));
            Assert.Throws<MiniJsonException>(() => MiniJson.Parse("{\"a\": }"));
            Assert.Throws<MiniJsonException>(() => MiniJson.Parse("[1, 2"));
            Assert.Throws<MiniJsonException>(() => MiniJson.Parse("{} extra"));
        }
    }
}

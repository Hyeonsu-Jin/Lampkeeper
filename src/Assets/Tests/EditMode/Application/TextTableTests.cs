// =============================================================
// 파일: TextTableTests.cs
// 역할: 임시 텍스트 표(TextTable)와 언어 선택 규칙(LanguageRules) 시험.
// 레이어: test (EditMode, Game.Tests.Application)
// 관련: NFR-034, NFR-044, C-48 / SDD 3.7.9
// =============================================================
using System.Collections.Generic;
using Game.Application.Localization;
using NUnit.Framework;

namespace Game.Tests.Application
{
    public class TextTableTests
    {
        [Test]
        public void Parse_ReadsKeyAndText_IgnoresCommentsAndBlankLines()
        {
            TextTable t = TextTable.Parse("# 주석\n\na.b\t안녕\r\nc.d\tHello\n");
            Assert.AreEqual(2, t.Count);
            Assert.IsTrue(t.TryGet("a.b", out string v));
            Assert.AreEqual("안녕", v);
            Assert.IsTrue(t.TryGet("c.d", out v));
            Assert.AreEqual("Hello", v);
        }

        [Test]
        public void Parse_Unescapes_NewlineTabBackslash()
        {
            TextTable t = TextTable.Parse("k\tline1\\nline2\\tTab\\\\End");
            Assert.IsTrue(t.TryGet("k", out string v));
            Assert.AreEqual("line1\nline2\tTab\\End", v);
        }

        [Test]
        public void Parse_DuplicateKey_ReportsError_KeepsFirst()
        {
            var errors = new List<string>();
            TextTable t = TextTable.Parse("k\tone\nk\ttwo", errors);
            Assert.AreEqual(1, errors.Count);
            t.TryGet("k", out string v);
            Assert.AreEqual("one", v);
        }

        [Test]
        public void Parse_LineWithoutTab_ReportsError()
        {
            var errors = new List<string>();
            TextTable.Parse("no tab here", errors);
            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void Parse_NullOrEmpty_GivesEmptyTable()
        {
            Assert.AreEqual(0, TextTable.Parse(null).Count);
            Assert.AreEqual(0, TextTable.Parse("").Count);
            Assert.IsFalse(TextTable.Parse("").TryGet(null, out _));
        }

        [TestCase("koreana", GameLanguage.Korean)]
        [TestCase("KOREANA", GameLanguage.Korean)]
        [TestCase("english", GameLanguage.English)]
        [TestCase("japanese", GameLanguage.English)]
        [TestCase(null, GameLanguage.English)]
        public void FirstRunLanguage_FromSteamLanguage(string steam, GameLanguage expected)
        {
            // [C-48] Steam 언어가 한국어면 한국어, 그 밖은 영어
            Assert.AreEqual(expected, LanguageRules.FromSteamLanguage(steam));
        }

        [TestCase("Korean", GameLanguage.Korean)]
        [TestCase("English", GameLanguage.English)]
        [TestCase("German", GameLanguage.English)]
        public void SystemLanguageFallback(string system, GameLanguage expected)
        {
            Assert.AreEqual(expected, LanguageRules.FromSystemLanguageName(system));
        }
    }
}

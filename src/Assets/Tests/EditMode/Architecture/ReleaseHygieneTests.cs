// 파일: ReleaseHygieneTests.cs
// 역할: 보고서가 지적한 저장소·설정 위생 문제(REV-014, SEC-005)를 재현하는 시험.
// 레이어: Tests (Game.Tests.Architecture)
// 관련: TC-055, TC-056, DEF-005, DEF-006, NFR-042, NFR-045
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Game.Tests.Architecture
{
    [TestFixture]
    public class ReleaseHygieneTests
    {
        [Test]
        public void TC055_Assets_아래에_원본이_없는_meta_파일이_없다()
        {
            string assets = UnityEngine.Application.dataPath;
            var orphans = Directory.EnumerateFiles(assets, "*.meta", SearchOption.AllDirectories)
                .Where(m => { string t = m.Substring(0, m.Length - 5); return !File.Exists(t) && !Directory.Exists(t); })
                .Select(m => m.Substring(assets.Length))
                .ToList();
            Assert.IsEmpty(orphans, "원본 없는 .meta: " + string.Join(", ", orphans.Take(5)));
        }

        [Test]
        [Category("KnownDefect")]
        public void TC056_Player_log가_꺼져_있다()
        {
            string path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "ProjectSettings", "ProjectSettings.asset"));
            string line = File.ReadAllLines(path).First(l => l.TrimStart().StartsWith("usePlayerLog:"));
            Assert.AreEqual("usePlayerLog: 0", line.Trim(), "Player.log에 SteamID·사용자 경로가 가려지지 않고 남음(SEC-005)");
        }
    }
}

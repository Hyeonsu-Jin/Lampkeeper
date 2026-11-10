// 파일: PvpAbsenceTests.cs
// 역할: FR-010(PvP 없음) 구조 확인. 참가자가 보낼 수 있는 명령 종류에 플레이어를 대상으로 하는 피해 명령이 없는지 확인한다.
// 레이어: Tests (Game.Tests.Application)
// 관련: TC-010, FR-010
using System;
using System.Linq;
using Game.Application.Commands;
using NUnit.Framework;

namespace Game.Tests.Application
{
    [TestFixture]
    public class PvpAbsenceTests
    {
        [Test]
        public void TC010_M1_명령_종류에는_플레이어를_공격하는_명령이_없다()
        {
            var names = Enum.GetNames(typeof(CommandKind)).Where(n => n != "None").OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "HitResource", "Ping" }, names, "명령 종류가 늘면 플레이어 대상 피해가 없는지 다시 확인한다(FR-010)");
        }
    }
}

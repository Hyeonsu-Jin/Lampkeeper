// =============================================================
// 파일: BuildPolicyTests.cs
// 역할: 출시 빌드 규칙(깨끗한 커밋만, 배포 폴더에서 디버그 산출물 제외) 판정 시험. 플레이어 빌드는 돌리지 않는다.
// 레이어: test (EditMode, Game.Tests.Infrastructure)
// 관련: IMP-012, SEC-009
// =============================================================
using System;
using System.IO;
using Game.Editor;
using NUnit.Framework;

namespace Game.Tests.Infrastructure
{
    [TestFixture]
    public class BuildPolicyTests
    {
        [Test]
        public void Release_WithDirtyTree_IsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => BuildPolicy.CheckReleaseAllowed(false, true, "a1b2c3d"));
        }

        [Test]
        public void Release_WithoutGit_IsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => BuildPolicy.CheckReleaseAllowed(false, false, "nogit"));
        }

        [Test]
        public void Release_WithCleanCommit_IsAllowed()
        {
            Assert.DoesNotThrow(() => BuildPolicy.CheckReleaseAllowed(false, false, "a1b2c3d"));
        }

        [Test]
        public void Development_IsAllowedEvenWhenDirty()
        {
            Assert.DoesNotThrow(() => BuildPolicy.CheckReleaseAllowed(true, true, "nogit"));
        }

        private static readonly string NL = ((char)10).ToString();
        private const string Bold = "\"src/Assets/_Game/UI/Fonts/Pretendard-Bold Dynamic SDF.asset\"";
        private const string Regular = "\"src/Assets/_Game/UI/Fonts/Pretendard-Regular Dynamic SDF.asset\"";

        [Test]
        public void IsDirty_OnlyTheTwoFonts_IsNotDirty()
        {
            // [R-46] 호출자가 앞뒤 공백을 자른 출력(첫 줄 앞 공백 없음)도 같게 읽는다.
            Assert.IsFalse(BuildPolicy.IsDirty("M " + Bold + NL + " M " + Regular));
            Assert.IsFalse(BuildPolicy.IsDirty(string.Empty));
        }

        [Test]
        public void IsDirty_FontsPlusOtherFile_IsDirty()
        {
            Assert.IsTrue(BuildPolicy.IsDirty("M " + Bold + NL + " M " + Regular + NL + " M src/Assets/_Game/Code/Editor/BuildPolicy.cs"));
            Assert.IsTrue(BuildPolicy.IsDirty(" M src/Assets/_Game/UI/Fonts/Other Dynamic SDF.asset"), "목록에 없는 글꼴은 허용하지 않는다");
        }

        [TestCase("KkeojinHaneul_BurstDebugInformation_DoNotShip", true)]
        [TestCase("KkeojinHaneul_BackUpThisFolder_ButDontShipItWithYourGame", true)]
        [TestCase("KkeojinHaneul_Data", false)]
        [TestCase("MonoBleedingEdge", false)]
        public void IsNonShippable_ByFolderName(string name, bool expected)
        {
            Assert.AreEqual(expected, BuildPolicy.IsNonShippable(name));
        }

        [Test]
        public void MoveNonShippable_MovesOnlyDebugFolders_OutOfOutput()
        {
            string root = Path.Combine(Path.GetTempPath(), "BuildPolicyTests_" + Guid.NewGuid().ToString("N"));
            string output = Path.Combine(root, "out");
            string symbols = Path.Combine(root, "sym");
            try
            {
                Directory.CreateDirectory(Path.Combine(output, "G_BurstDebugInformation_DoNotShip"));
                Directory.CreateDirectory(Path.Combine(output, "G_BackUp_ButDontShipItWithYourGame"));
                Directory.CreateDirectory(Path.Combine(output, "G_Data"));
                Assert.AreEqual(2, BuildPolicy.MoveNonShippable(output, symbols));
                Assert.IsTrue(Directory.Exists(Path.Combine(output, "G_Data")));
                Assert.IsFalse(Directory.Exists(Path.Combine(output, "G_BurstDebugInformation_DoNotShip")));
                Assert.IsTrue(Directory.Exists(Path.Combine(symbols, "G_BackUp_ButDontShipItWithYourGame")));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }
    }
}

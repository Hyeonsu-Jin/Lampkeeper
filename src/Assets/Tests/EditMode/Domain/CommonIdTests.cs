// =============================================================
// 파일: CommonIdTests.cs
// 역할: Domain/Common의 PlayerId, ContentId 값 객체 단위 시험.
// 레이어: test (EditMode, Game.Tests.Domain)
// 관련: FR-015, NFR-033 / SDD 3.8.2 Domain/Common
// =============================================================
using System;
using Game.Domain.Common;
using NUnit.Framework;

namespace Game.Tests.Domain
{
    public class CommonIdTests
    {
        [Test]
        public void PlayerId_Zero_IsNotValid_AndNoneIsZero()
        {
            Assert.IsFalse(new PlayerId(0UL).IsValid);
            Assert.AreEqual(PlayerId.None, new PlayerId(0UL));
        }

        [Test]
        public void PlayerId_SameValue_IsEqual_AndHasSameHash()
        {
            var a = new PlayerId(76561198000000001UL);
            var b = new PlayerId(76561198000000001UL);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsTrue(a.IsValid);
            Assert.AreEqual("76561198000000001", a.ToString());
        }

        [Test]
        public void PlayerId_Compare_OrdersByValue()
        {
            Assert.Less(new PlayerId(1).CompareTo(new PlayerId(2)), 0);
            Assert.IsTrue(new PlayerId(1) != new PlayerId(2));
        }

        [Test]
        public void ContentId_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContentId(-1));
        }

        [Test]
        public void ContentId_Zero_IsNone_AndPositiveIsValid()
        {
            Assert.IsFalse(ContentId.None.IsValid);
            Assert.IsTrue(new ContentId(1).IsValid);
            Assert.AreEqual(new ContentId(7), new ContentId(7));
            Assert.Less(new ContentId(1).CompareTo(new ContentId(2)), 0);
        }
    }
}

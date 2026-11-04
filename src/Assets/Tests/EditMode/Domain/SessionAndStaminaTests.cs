// =============================================================
// 파일: SessionAndStaminaTests.cs
// 역할: 참가 판정 규칙(SessionRules)과 스태미나 규칙(StaminaState)의 단위 시험.
// 레이어: test (EditMode, Game.Tests.Domain)
// 관련: FR-003, FR-004, FR-024, FR-025, NFR-038, DES-003, DES-031 / SDD 3.9.2
// =============================================================
using Game.Domain.Common;
using Game.Domain.Session;
using Game.Domain.Vitals;
using NUnit.Framework;

namespace Game.Tests.Domain
{
    public class SessionRulesTests
    {
        private const ushort Proto = 1;
        private const string Build = "0.1.0+dev.20261104153012.a1b2c3d";
        private const ulong Hash = 0xABCDUL;

        private static GameVersion Version(string text)
        {
            Assert.IsTrue(GameVersion.TryParse(text, out GameVersion version), text);
            return version;
        }

        private static SessionRules Rules(int max = 8) => new SessionRules(max, Proto, Version(Build), Hash);

        private static JoinFacts Facts(
            bool banned = false, bool member = true, bool needsMember = true, int connected = 0, int reserved = 0,
            SessionAccess access = SessionAccess.FriendsOnly, JoinPath path = JoinPath.Friend,
            ushort proto = Proto, string build = Build, ulong hash = Hash, bool genOk = true) =>
            new JoinFacts(new PlayerId(1), banned, member, needsMember, connected, reserved, access, path, proto, build, hash, genOk);

        [Test]
        public void Normal_Join_IsAccepted()
        {
            Assert.AreEqual(JoinRejectReason.None, Rules().Evaluate(Facts()));
        }

        [Test]
        public void EighthPlayer_IsAccepted_NinthIsFull()
        {
            // [NFR-038] 최대 8인: 7명이 있을 때 8번째는 허용, 8명이 있을 때 9번째는 거부
            Assert.AreEqual(JoinRejectReason.None, Rules().Evaluate(Facts(connected: 7)));
            Assert.AreEqual(JoinRejectReason.Full, Rules().Evaluate(Facts(connected: 8)));
        }

        [Test]
        public void Reservations_CountTowardCapacity()
        {
            Assert.AreEqual(JoinRejectReason.Full, Rules().Evaluate(Facts(connected: 6, reserved: 2)));
        }

        [TestCase((ushort)2, Build, Hash)]
        [TestCase(Proto, "0.1.1+dev.20261104153012.a1b2c3d", Hash)]
        [TestCase(Proto, "0.1.0+dev.20261104153013.a1b2c3d", Hash)]
        [TestCase(Proto, "0.1.0+dev.20261104153012.a1b2c3d.dirty", Hash)]
        [TestCase(Proto, "0.1.0+rel.20261104153012.a1b2c3d", Hash)]
        [TestCase(Proto, "0.1.0", Hash)]
        [TestCase(Proto, "0.1.0+editor", Hash)]
        [TestCase(Proto, "", Hash)]
        [TestCase(Proto, "garbage", Hash)]
        [TestCase(Proto, "01.0.0", Hash)]
        [TestCase(Proto, Build, 0x1234UL)]
        public void VersionOrContentMismatch_IsRejected(ushort proto, string build, ulong hash)
        {
            Assert.AreEqual(JoinRejectReason.VersionMismatch, Rules().Evaluate(Facts(proto: proto, build: build, hash: hash)));
        }

        [Test]
        public void UnsupportedGeneratorVersion_IsVersionMismatch()
        {
            Assert.AreEqual(JoinRejectReason.VersionMismatch, Rules().Evaluate(Facts(genOk: false)));
        }

        [Test]
        public void Banned_IsRejected_BeforeOtherChecks()
        {
            // 차단된 사람에게는 버전이 달라도 「차단됨」만 알려 준다.
            Assert.AreEqual(JoinRejectReason.Banned, Rules().Evaluate(Facts(banned: true, build: "9.9.9", connected: 8)));
        }

        [Test]
        public void SteamTransport_NonLobbyMember_IsAccessDenied()
        {
            Assert.AreEqual(JoinRejectReason.AccessDenied, Rules().Evaluate(Facts(member: false, needsMember: true)));
        }

        [Test]
        public void DevTransport_DoesNotRequireLobbyMembership()
        {
            Assert.AreEqual(JoinRejectReason.None, Rules().Evaluate(Facts(member: false, needsMember: false)));
        }

        [Test]
        public void InviteOnly_AllowsInviteAndRejoin_ButNotFriendPath()
        {
            Assert.AreEqual(JoinRejectReason.None, Rules().Evaluate(Facts(access: SessionAccess.InviteOnly, path: JoinPath.Invite)));
            Assert.AreEqual(JoinRejectReason.None, Rules().Evaluate(Facts(access: SessionAccess.InviteOnly, path: JoinPath.Rejoin)));
            Assert.AreEqual(JoinRejectReason.AccessDenied, Rules().Evaluate(Facts(access: SessionAccess.InviteOnly, path: JoinPath.Friend)));
        }

        [Test]
        public void FriendsOnly_RejectsPublicListPath()
        {
            Assert.AreEqual(JoinRejectReason.AccessDenied, Rules().Evaluate(Facts(access: SessionAccess.FriendsOnly, path: JoinPath.PublicList)));
        }

        [Test]
        public void Constructor_RejectsNonPositiveCapacity()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new SessionRules(0, Proto, Version(Build), Hash));
        }
    }

    public class StaminaStateTests
    {
        // 후보값: 최대 100, 달리기 소모 10/초, 회복 지연 1초, 회복 20/초
        private static StaminaState Create() => new StaminaState(new StaminaParams(100f, 10f, 1f, 20f));

        [Test]
        public void Running_DrainsAtConfiguredRate()
        {
            StaminaState s = Create();
            s.Tick(2f, true);
            Assert.AreEqual(80f, s.Current, 0.001f);
        }

        [Test]
        public void Zero_CannotRun_AndDoesNotGoNegative()
        {
            StaminaState s = Create();
            s.Tick(20f, true);
            Assert.AreEqual(0f, s.Current);
            Assert.IsFalse(s.CanRun, "[FR-024] 0이면 달리기를 시작하거나 유지할 수 없다");
        }

        [Test]
        public void Recovery_StartsOnlyAfterDelay()
        {
            StaminaState s = Create();
            s.Tick(5f, true); // 50
            s.Tick(0.9f, false);
            Assert.AreEqual(50f, s.Current, 0.001f, "지연(1초) 안에는 회복하지 않는다");
            s.Tick(0.2f, false); // 누적 1.1초: 지연이 끝나는 순간부터 회복
            Assert.Greater(s.Current, 50f);
        }

        [Test]
        public void Spending_AgainRestartsTheDelay()
        {
            StaminaState s = Create();
            s.Tick(5f, true);
            s.Tick(1.5f, false);
            float afterRecover = s.Current;
            s.Tick(0.1f, true); // 다시 소모하면 지연을 처음부터 센다
            s.Tick(0.9f, false);
            Assert.Less(s.Current, afterRecover, "다시 소모한 뒤 0.9초 동안은 회복하지 않는다");
        }

        [Test]
        public void Recovery_StopsAtMax()
        {
            StaminaState s = Create();
            s.Tick(1f, true);
            s.Tick(30f, false);
            Assert.AreEqual(100f, s.Current);
            Assert.AreEqual(1f, s.Ratio, 0.0001f);
        }

        [Test]
        public void NonPositiveDelta_DoesNothing()
        {
            StaminaState s = Create();
            s.Tick(0f, true);
            s.Tick(-1f, true);
            Assert.AreEqual(100f, s.Current);
        }
    }
}

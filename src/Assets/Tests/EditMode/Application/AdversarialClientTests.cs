// 파일: AdversarialClientTests.cs
// 역할: 검토·보안 보고서(REV-001·003·004, SEC-001·004)가 지적한 적대적 클라이언트 시나리오를 재현하는 시험.
// 레이어: Tests (Game.Tests.Application)
// 관련: TC-050~TC-054, DEF-001~DEF-004, NFR-030, NFR-041, FR-003
// 주의: [Category("KnownDefect")] 시험은 현재 실패하는 것이 정상이다(결함 재현). 결함이 고쳐지면 통과해야 한다. [Ignore] 하지 않는다.
//       DEF-001~003은 IMP-001·005로 고쳐져 표시를 뺐다. TC054(DEF-004, IMP-002)만 남아 있다.
using System.Linq;
using Game.Application.Commands;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Application.Server;
using Game.Domain.Common;
using Game.Domain.Vitals;
using NUnit.Framework;

namespace Game.Tests.Application
{
    [TestFixture]
    public class AdversarialClientTests
    {
        private static readonly AvatarMoveSettings Settings = new AvatarMoveSettings(4f, 7f, 1.25f, 0.5f, 0.5f, 0.5f, 40f);
        private static readonly StaminaParams Stamina = new StaminaParams(100f, 10f, 1f, 20f);

        private static AvatarPose Pose(float x, uint ms, bool running = false) =>
            new AvatarPose(new Vec3f(x, 0f, 0f), 0f, 0f, running, ms);

        [Test]
        public void TC050_달리기_속도의_2배로_정직한_시각을_보내면_거부된다()
        {
            // NFR-030 수용 기준: 달리기 속도(7 m/s)의 2배(14 m/s)로 이동한 위치를 보내면 되돌린다.
            var validator = new AvatarMoveValidator(Settings);
            var state = new AvatarMoveState(Stamina);
            validator.Check(state, Pose(0f, 0, true), 0d);
            int rejected = 0;
            for (int i = 1; i <= 60; i++) // 30 Hz, 2초
            {
                uint ms = (uint)(i * 1000 / 30);
                float x = 14f * ms / 1000f;
                if (validator.Check(state, Pose(x, ms, true), ms / 1000d) == AvatarMoveVerdict.Rejected) { rejected++; }
            }
            Assert.Greater(rejected, 0, "14 m/s 이동이 한 번도 거부되지 않음(자세마다 0.5 m 여유가 더해짐, SEC-001 항목 1)");
        }

        [Test]
        public void TC051_시각을_500ms씩_부풀린_자세_폭주는_거부된다()
        {
            // 호스트가 본 실제 시간은 1초 안(30개 자세)인데 클라이언트 시각만 15초 흐른 것처럼 보낸다. 걷기 상한 안에서 자세마다 2.9 m씩 = 호스트 시간 기준 87 m/s(걷기 한도 5 m/s).
            var validator = new AvatarMoveValidator(Settings);
            var state = new AvatarMoveState(Stamina);
            validator.Check(state, Pose(0f, 0, true), 0d);
            int accepted = 0;
            for (int i = 1; i <= 30; i++)
            {
                // 호스트가 받은 시각은 33 ms 간격(30 Hz)이고 자세 안의 시각만 500 ms씩 부풀렸다.
                if (validator.Check(state, Pose(2.9f * i, (uint)(500 * i), false), i * 0.033d) == AvatarMoveVerdict.Accepted) { accepted++; }
            }
            Assert.Less(accepted, 30, "30개 모두 수락됨: 호스트 수신 시각 기준 검사·자세 빈도 제한 없음(REV-001, SEC-001 항목 2·3)");
        }

        [Test]
        public void TC052_첫_자세는_스폰_위치에서_멀면_거부된다()
        {
            var dispatcher = new CommandDispatcher(new RequestGuard(30, new CommandLimits(1500f)), NullLog.Instance);
            var world = new ServerWorld(new ServerSettings(), dispatcher, new FakeClock(), null, NullLog.Instance);
            var player = new PlayerId(7);
            world.AddPlayer(player, "p", false);
            var far = new AvatarPose(new Vec3f(1000f, 0f, 1000f), 0f, 0f, false, 0u);
            Assert.AreEqual(AvatarMoveVerdict.Rejected, world.CheckAvatarPose(player, far, out _),
                "참가 직후 첫 자세로 월드 어디든 순간 이동됨(REV-003, SEC-001 항목 5)");
        }

        [Test]
        public void TC053_한_참가자가_대기열을_채워도_다른_참가자_명령은_받아들여진다()
        {
            var settings = new ServerSettings { MaxQueuedCommands = 10, MaxCommandsPerTick = 3, RequestsPerSecondLimit = 5 };
            var dispatcher = new CommandDispatcher(new RequestGuard(5, new CommandLimits(1500f)), NullLog.Instance);
            dispatcher.Register<PingCommand>(CommandKind.Ping, new PingCommandHandler());
            var world = new ServerWorld(settings, dispatcher, new FakeClock(), null, NullLog.Instance);
            var flooder = new PlayerId(1);
            var honest = new PlayerId(2);
            world.AddPlayer(flooder, "a", false);
            world.AddPlayer(honest, "b", false);
            for (int i = 0; i < 50; i++) { world.Enqueue(flooder, new PingCommand((uint)i, Vec3f.Zero)); }
            Assert.IsTrue(world.Enqueue(honest, new PingCommand(1u, Vec3f.Zero)),
                "공유 대기열이 가득 차 정상 참가자의 명령이 버려짐(REV-004, SEC-004)");
        }

        [Test]
        public void IMP005_디코드_실패_회신도_명령과_같은_빈도_버킷을_쓴다()
        {
            var dispatcher = new CommandDispatcher(new RequestGuard(5, new CommandLimits(1500f)), NullLog.Instance);
            var world = new ServerWorld(new ServerSettings { RequestsPerSecondLimit = 5 }, dispatcher, new FakeClock(), null, NullLog.Instance);
            var player = new PlayerId(1);
            world.AddPlayer(player, "a", false);
            int allowed = 0;
            for (int i = 0; i < 100; i++) { if (world.AllowRejectionReply(player)) { allowed++; } }
            Assert.AreEqual(5, allowed, "같은 순간에 회신은 초당 상한(5)까지만");
        }

        [Test]
        public void IMP005_대기열_몫을_다_쓴_참가자는_더_넣지_못하고_나간_뒤_몫은_정리된다()
        {
            var settings = new ServerSettings { MaxPlayers = 2, MaxQueuedCommands = 10, MaxCommandsPerTick = 64, RequestsPerSecondLimit = 100 };
            var dispatcher = new CommandDispatcher(new RequestGuard(100, new CommandLimits(1500f)), NullLog.Instance);
            dispatcher.Register<PingCommand>(CommandKind.Ping, new PingCommandHandler());
            var world = new ServerWorld(settings, dispatcher, new FakeClock(), null, NullLog.Instance);
            var a = new PlayerId(1);
            world.AddPlayer(a, "a", false);
            int accepted = 0;
            for (int i = 0; i < 20; i++) { if (world.Enqueue(a, new PingCommand((uint)i, Vec3f.Zero))) { accepted++; } }
            Assert.AreEqual(5, accepted, "몫 = 10 / 2");
            world.Tick(1u, 0.033f); // 처리되면 몫이 돌아온다
            Assert.IsTrue(world.Enqueue(a, new PingCommand(99u, Vec3f.Zero)));
        }

        [Test]
        [Category("KnownDefect")]
        public void TC054_버전_불일치_거부에는_호스트_버전이_실리고_문구에_자리가_있다()
        {
            // FR-003 수용 기준: 「게임 버전 불일치」와 두 버전(참가자·호스트)을 함께 표시.
            bool messageCarriesString = typeof(JoinRejected).GetProperties().Any(p => p.PropertyType == typeof(string));
            string path = UnityEngine.Application.dataPath + "/_Game/Localization/strings.ko.txt";
            string cause = System.IO.File.ReadAllLines(path)
                .First(l => l.StartsWith("join.fail.version.cause\t"));
            Assert.IsTrue(messageCarriesString && cause.Contains("{0}"),
                "JoinRejected에 버전 문자열이 없고 문구에 버전 자리({0})도 없음(REV-002)");
        }
    }
}

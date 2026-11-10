// =============================================================
// 파일: ServerAndSessionTests.cs
// 역할: 명령 파이프라인(RequestGuard·CommandDispatcher·CommandCodec), 아바타 이동 검사(AvatarMoveValidator), 서버 월드 틱,
//       호스트 세션 서비스(SessionService·PlayerIdentityPolicy·LobbyMetaBuilder), 클라이언트 접속 절차(ClientSession) 시험.
// 레이어: test (EditMode, Game.Tests.Application)
// 관련: FR-003, FR-004, FR-007, FR-023~025, NFR-030, NFR-038~041, DES-050, DES-052, DES-054, DES-072 / SDD 3.9.2, 3.11.12, 3.11.13
// =============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Application.Commands;
using Game.Application.Config;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Application.Replication;
using Game.Application.Server;
using Game.Application.Session;
using Game.Domain.Common;
using Game.Domain.Session;
using Game.Domain.Vitals;
using NUnit.Framework;

namespace Game.Tests.Application
{
    public class RequestGuardTests
    {
        [Test]
        public void TokenBucket_AllowsBurstThenRefills()
        {
            var bucket = new TokenBucket(10, 10, 0d);
            int ok = 0;
            for (int i = 0; i < 20; i++)
            {
                if (bucket.TryTake(0d)) { ok++; }
            }
            Assert.AreEqual(10, ok, "버스트만큼만 통과");
            Assert.IsFalse(bucket.TryTake(0.05));
            Assert.IsTrue(bucket.TryTake(0.2), "0.2초 뒤 2개 채워짐");
        }

        [Test]
        public void Flood_ProcessesNoMoreThanTheLimitPerSecond()
        {
            // [NFR-041] 초당 수천 번 보내도 처리량은 상한(30) 안.
            var guard = new RequestGuard(30, new CommandLimits(1500f));
            var player = new PlayerId(1);
            int allowed = 0;
            for (int i = 0; i < 5000; i++)
            {
                if (guard.CheckRate(player, CommandKind.Ping, i / 5000d)) { allowed++; }
            }
            Assert.LessOrEqual(allowed, 30 + 30, "1초 동안 버스트 + 보충 안에서만 통과");
            Assert.GreaterOrEqual(allowed, 30);
        }

        [Test]
        public void PerKindRate_AppliesInAdditionToGlobal()
        {
            var guard = new RequestGuard(100, new CommandLimits(1500f));
            guard.SetKindRate(CommandKind.Ping, 2);
            int allowed = Enumerable.Range(0, 50).Count(_ => guard.CheckRate(new PlayerId(1), CommandKind.Ping, 0d));
            Assert.AreEqual(2, allowed);
        }

        [Test]
        public void Buckets_AreSeparatePerPlayer()
        {
            var guard = new RequestGuard(1, new CommandLimits(1500f));
            Assert.IsTrue(guard.CheckRate(new PlayerId(1), CommandKind.Ping, 0d));
            Assert.IsFalse(guard.CheckRate(new PlayerId(1), CommandKind.Ping, 0d));
            Assert.IsTrue(guard.CheckRate(new PlayerId(2), CommandKind.Ping, 0d));
        }
    }

    public class CommandPipelineTests
    {
        private sealed class ThrowingHandler : ICommandHandler<PingCommand>
        {
            public Rejection? Validate(CommandContext context, PingCommand command) => throw new InvalidOperationException("의도된 시험 예외");
            public void Execute(CommandContext context, PingCommand command) { }
        }

        private static (ServerWorld world, CommandDispatcher dispatcher, FakeClock clock, ListLog log) Create(int ratePerSecond = 30, bool throwing = false)
        {
            var clock = new FakeClock();
            var log = new ListLog();
            var guard = new RequestGuard(ratePerSecond, new CommandLimits(ProtocolConstants.PositionHalfRangeMeters));
            var dispatcher = new CommandDispatcher(guard, log);
            if (throwing)
            {
                dispatcher.Register<PingCommand>(CommandKind.Ping, new ThrowingHandler());
            }
            else
            {
                dispatcher.Register<PingCommand>(CommandKind.Ping, new PingCommandHandler());
            }
            var world = new ServerWorld(new ServerSettings(), dispatcher, clock, null, log);
            return (world, dispatcher, clock, log);
        }

        [Test]
        public void ValidPing_IsExecutedOnTick_AndResultSentToRequester()
        {
            var (world, _, _, _) = Create();
            var player = new PlayerId(10);
            world.AddPlayer(player, "p", false);
            var outcomes = new List<CommandOutcome>();
            world.CommandCompleted += outcomes.Add;

            Assert.IsTrue(world.Enqueue(player, new PingCommand(5u, new Vec3f(10, 0, 20))));
            Assert.AreEqual(0, outcomes.Count, "틱 전에는 처리하지 않는다");
            world.Tick(1u, 1f / 30f);

            Assert.AreEqual(1, outcomes.Count);
            Assert.IsTrue(outcomes[0].Result.Ok);
            Assert.AreEqual(5u, outcomes[0].Result.RequestId);
            Assert.IsTrue(world.State.TryGetLastPing(player, out Vec3f ping));
            Assert.AreEqual(10f, ping.X);
        }

        [Test]
        public void PingOutsideWorld_IsRejectedWithInvalidValue()
        {
            var (world, _, _, _) = Create();
            var player = new PlayerId(10);
            world.AddPlayer(player, "p", false);
            var outcomes = new List<CommandOutcome>();
            world.CommandCompleted += outcomes.Add;
            world.Enqueue(player, new PingCommand(1u, new Vec3f(99999f, 0, 0)));
            world.Enqueue(player, new PingCommand(2u, new Vec3f(float.NaN, 0, 0)));
            world.Tick(1u, 0.033f);
            Assert.AreEqual(2, outcomes.Count);
            Assert.IsTrue(outcomes.All(o => !o.Result.Ok && o.Result.Rejection.Reason == ReasonCode.InvalidValue));
            Assert.IsFalse(world.State.TryGetLastPing(player, out _), "거부된 명령은 상태를 바꾸지 않는다");
        }

        [Test]
        public void HandlerException_IsLogged_AndDoesNotEscape()
        {
            var (world, _, _, log) = Create(throwing: true);
            var player = new PlayerId(10);
            world.AddPlayer(player, "p", false);
            var outcomes = new List<CommandOutcome>();
            world.CommandCompleted += outcomes.Add;
            world.Enqueue(player, new PingCommand(1u, Vec3f.Zero));
            Assert.DoesNotThrow(() => world.Tick(1u, 0.033f));
            Assert.AreEqual(ReasonCode.InternalError, outcomes[0].Result.Rejection.Reason);
            Assert.IsNotEmpty(log.Errors, "원인은 호스트 로그에 남는다(예외를 삼키지 않음)");
        }

        [Test]
        public void ClientCannotBypassRate_ByFlooding()
        {
            var (world, _, clock, _) = Create(ratePerSecond: 5);
            var player = new PlayerId(10);
            world.AddPlayer(player, "p", false);
            int ok = 0;
            world.CommandCompleted += o => { if (o.Result.Ok) { ok++; } };
            for (int i = 0; i < 200; i++)
            {
                world.Enqueue(player, new PingCommand((uint)i, Vec3f.Zero));
            }
            world.Tick(1u, 0.033f);
            Assert.AreEqual(5, ok, "초당 상한(5)만 실행된다");
            Assert.AreEqual(0, clock.RealtimeSeconds);
        }

        [Test]
        public void QueueIsBounded_AndPerTickBudgetLimitsWork()
        {
            var settings = new ServerSettings { MaxPlayers = 1, MaxQueuedCommands = 10, MaxCommandsPerTick = 3, RequestsPerSecondLimit = 1000 };
            var clock = new FakeClock();
            var dispatcher = new CommandDispatcher(new RequestGuard(1000, new CommandLimits(1500f)), NullLog.Instance);
            dispatcher.Register<PingCommand>(CommandKind.Ping, new PingCommandHandler());
            var world = new ServerWorld(settings, dispatcher, clock, null, NullLog.Instance);
            var player = new PlayerId(1);
            world.AddPlayer(player, "p", false);
            int accepted = 0;
            for (int i = 0; i < 50; i++)
            {
                if (world.Enqueue(player, new PingCommand((uint)i, Vec3f.Zero))) { accepted++; }
            }
            Assert.AreEqual(10, accepted, "대기열 상한(10)을 넘는 명령은 버린다");
            int done = 0;
            world.CommandCompleted += _ => done++;
            world.Tick(1u, 0.033f);
            Assert.AreEqual(3, done, "한 틱 처리 상한(3)");
        }

        [Test]
        public void CommandFromUnknownPlayer_IsNotQueued()
        {
            var (world, _, _, _) = Create();
            Assert.IsFalse(world.Enqueue(new PlayerId(404), new PingCommand(1u, Vec3f.Zero)));
        }

        [Test]
        public void UnknownCommandKind_IsRejected()
        {
            Assert.IsFalse(CommandCodec.TryDecode(new CommandMessage(200, 1u, new byte[0]), out _, out Rejection rejection));
            Assert.AreEqual(ReasonCode.UnknownCommand, rejection.Reason);
        }

        [Test]
        public void PingCodec_RoundTrip_AndBadPayloadRejected()
        {
            Assert.IsTrue(CommandCodec.TryEncode(new PingCommand(7u, new Vec3f(1.5f, 2f, -3f)), out CommandMessage message));
            Assert.IsTrue(CommandCodec.TryDecode(message, out ICommand command, out _));
            var ping = (PingCommand)command;
            Assert.AreEqual(7u, ping.RequestId);
            Assert.AreEqual(-3f, ping.Target.Z, 0.001f);
            Assert.AreEqual(12, message.Payload.Length);

            var broken = new CommandMessage((byte)CommandKind.Ping, 1u, new byte[5]);
            Assert.IsFalse(CommandCodec.TryDecode(broken, out _, out Rejection rejection));
            Assert.AreEqual(ReasonCode.InvalidValue, rejection.Reason);
        }

        [Test]
        public void Tick_RecordsServerMarkers_InOrder()
        {
            var calls = new List<string>();
            var probe = new RecordingProbe(calls);
            var clock = new FakeClock();
            var dispatcher = new CommandDispatcher(new RequestGuard(30, new CommandLimits(1500f)), NullLog.Instance);
            var world = new ServerWorld(new ServerSettings(), dispatcher, clock, probe, NullLog.Instance);
            world.Tick(1u, 0.033f);
            Assert.AreEqual("B:Server.Total", calls.First());
            Assert.AreEqual("E:Server.Total", calls.Last());
            CollectionAssert.IsSubsetOf(new[] { "B:Server.Commands", "B:Server.Systems", "B:Server.Replication" }, calls);
        }

        private sealed class RecordingProbe : IPerfProbe
        {
            private readonly List<string> _calls;
            public RecordingProbe(List<string> calls) { _calls = calls; }
            public void Begin(string marker) => _calls.Add("B:" + marker);
            public void End(string marker) => _calls.Add("E:" + marker);
            public void Count(string name, double value) { }
        }
    }

    /// <summary>시험용: 자세 안의 시각(ms)을 호스트 수신 시각으로 쓰는 호출(정직한 클라이언트를 흉내 낸다).</summary>
    internal static class AvatarMoveValidatorTestExtensions
    {
        public static AvatarMoveVerdict Check(this AvatarMoveValidator validator, AvatarMoveState state, AvatarPose pose) =>
            validator.Check(state, pose, pose.TimeMs / 1000d);
    }

    public class AvatarMoveValidatorTests
    {
        // 걷기 4, 달리기 7, 허용 배율 1.25, 여유 0.5 m, 소진 여유 0.5초, 최대 간격 0.5초, 수직 40 m/s
        private static readonly AvatarMoveSettings Settings = new AvatarMoveSettings(4f, 7f, 1.25f, 0.5f, 0.5f, 0.5f, 40f);
        private static readonly StaminaParams Stamina = new StaminaParams(100f, 10f, 1f, 20f);

        private static AvatarPose Pose(float x, uint ms, bool running = false, float y = 0f, float z = 0f) =>
            new AvatarPose(new Vec3f(x, y, z), 0f, 0f, running, ms);

        private static (AvatarMoveValidator, AvatarMoveState) Create() => (new AvatarMoveValidator(Settings), new AvatarMoveState(Stamina));

        [Test]
        public void FirstPose_NearSpawn_IsAcceptedAsBaseline()
        {
            var (validator, state) = Create();
            state.SetSpawn(new Vec3f(100f, 0f, 100f));
            Assert.AreEqual(AvatarMoveVerdict.Accepted, validator.Check(state, Pose(105f, 1000, z: 100f)));
            Assert.IsTrue(state.HasLast);
        }

        [Test]
        public void FirstPose_FarFromSpawn_OrWrongHeight_IsRejected()
        {
            // [IMP-001, REV-003] 첫 자세로 순간 이동하지 못한다.
            var (validator, state) = Create();
            state.SetSpawn(new Vec3f(100f, 0f, 100f));
            Assert.AreEqual(AvatarMoveVerdict.Rejected, validator.Check(state, Pose(500f, 1000, z: 100f)));
            Assert.AreEqual(AvatarMoveVerdict.Rejected, validator.Check(state, Pose(100f, 1100, y: 200f, z: 100f)));
            Assert.IsFalse(state.HasLast);
        }

        [Test]
        public void ClientClaimedTime_IsNotTrusted_HostTimeDecidesDistance()
        {
            // [IMP-001] 자세 안 시각을 10초 앞당겨도 호스트가 받은 시각 차이(0.1초)만 인정한다.
            var (validator, state) = Create();
            validator.Check(state, Pose(0f, 0), 0d);
            Assert.AreEqual(AvatarMoveVerdict.Rejected, validator.Check(state, Pose(3f, 10000), 0.1d));
        }

        [Test]
        public void SlackIsGivenOnce_NotPerPose()
        {
            // [IMP-001] 걷기 한도(4 x 1.25 = 5 m/s)로 2초: 합계 10 m + 여유 0.5 m까지만 가능. 33 ms마다 0.18 m씩 = 5.45 m/s는 쌓여 결국 거부된다.
            var (validator, state) = Create();
            validator.Check(state, Pose(0f, 0), 0d);
            int rejected = 0;
            for (int i = 1; i <= 60; i++)
            {
                uint ms = (uint)(i * 33);
                if (validator.Check(state, Pose(0.18f * i, ms), ms / 1000d) == AvatarMoveVerdict.Rejected) { rejected++; }
            }
            Assert.Greater(rejected, 0);
        }

        [Test]
        public void PoseFlood_IsDroppedByRateLimit()
        {
            // [IMP-001, SEC-001 항목 3] 같은 호스트 시각에 자세를 몰아 보내면 버킷(5개)을 넘는 자세는 버린다.
            var (validator, state) = Create();
            int stale = 0;
            for (int i = 0; i < 50; i++)
            {
                if (validator.Check(state, Pose(0f, (uint)(i + 1)), 1d) == AvatarMoveVerdict.Stale) { stale++; }
            }
            Assert.AreEqual(45, stale);
        }

        [Test]
        public void WalkingSpeed_IsAccepted_FiveTimesFasterIsRejected()
        {
            var (validator, state) = Create();
            validator.Check(state, Pose(0f, 0));
            // 0.5초에 2 m = 걷기 속도(4 m/s)
            Assert.AreEqual(AvatarMoveVerdict.Accepted, validator.Check(state, Pose(2f, 500)));
            // 0.1초에 20 m
            Assert.AreEqual(AvatarMoveVerdict.Rejected, validator.Check(state, Pose(22f, 600)));
            Assert.AreEqual(2f, state.LastAccepted.Position.X, 0.001f, "거부된 자세는 마지막 수락 위치를 바꾸지 않는다");
        }

        [Test]
        public void RunSpeed_NeedsRunningFlag()
        {
            var (validator, state) = Create();
            validator.Check(state, Pose(0f, 0));
            // 0.5초에 3.5 m(7 m/s): 달리기 플래그 없이는 걷기 한도(4*1.25*0.5+0.5=3.0)를 넘는다.
            Assert.AreEqual(AvatarMoveVerdict.Rejected, validator.Check(state, Pose(3.5f, 500, running: false)));
            Assert.AreEqual(AvatarMoveVerdict.Accepted, validator.Check(state, Pose(3.5f, 500, running: true)));
        }

        [Test]
        public void ZeroStamina_LimitsToWalkingSpeed_AfterGrace()
        {
            var (validator, state) = Create();
            validator.Check(state, Pose(0f, 0));
            float x = 0f;
            uint ms = 0;
            // 달리기 속도로 계속 달려 서버 추정 스태미나를 0으로 만든다(100/10 = 10초). 0.5초 간격 자세.
            for (int i = 0; i < 20; i++)
            {
                x += 3.5f;
                ms += 500;
                validator.Check(state, Pose(x, ms, running: true));
            }
            Assert.AreEqual(0f, state.Stamina.Current, 0.01f, "추정 스태미나가 소진됨");
            Assert.IsFalse(state.Stamina.CanRun);

            // 소진 여유(0.5초)가 지난 뒤에는 달리기 플래그를 달아도 걷기 속도가 상한이다. [FR-024]
            x += 3.5f;
            ms += 500;
            validator.Check(state, Pose(x, ms, running: true)); // 여유 구간 소모
            x += 3.5f;
            ms += 500;
            Assert.AreEqual(AvatarMoveVerdict.Rejected, validator.Check(state, Pose(x, ms, running: true)));
        }

        [Test]
        public void StaminaGrace_AllowsRunSpeedRightAfterEstimateReachesZero()
        {
            var (validator, state) = Create();
            validator.Check(state, Pose(0f, 0));
            float x = 0f;
            uint ms = 0;
            while (state.Stamina.Current > 0f)
            {
                x += 0.7f; // 0.1초에 0.7 m = 7 m/s
                ms += 100;
                validator.Check(state, Pose(x, ms, running: true));
            }
            // 추정 0이 된 직후 0.5초 안에는 클라이언트와 추정 차이를 흡수하려고 달리기 속도를 계속 허용한다.
            x += 0.7f;
            ms += 100;
            Assert.AreEqual(AvatarMoveVerdict.Accepted, validator.Check(state, Pose(x, ms, running: true)));
        }

        [Test]
        public void StalePose_IsIgnored_NotCorrected()
        {
            var (validator, state) = Create();
            validator.Check(state, Pose(0f, 1000));
            Assert.AreEqual(AvatarMoveVerdict.Stale, validator.Check(state, Pose(1f, 1000)));
            Assert.AreEqual(AvatarMoveVerdict.Stale, validator.Check(state, Pose(1f, 900)));
        }

        [Test]
        public void OutsideWorld_AndNaN_AreRejected()
        {
            var (validator, state) = Create();
            validator.Check(state, Pose(0f, 0));
            Assert.AreEqual(AvatarMoveVerdict.Rejected, validator.Check(state, Pose(5000f, 1000)));
            Assert.AreEqual(AvatarMoveVerdict.Rejected, validator.Check(state, Pose(float.NaN, 1000)));
        }

        [Test]
        public void WorldCenterSpawn_FirstAndMovingPoses_AreAccepted()
        {
            // 회귀: 월드 좌표는 0~3072인데 ±1536으로 검사해 중앙(1536) 스폰의 이동이 전부 거부되던 결함.
            var (validator, state) = Create();
            state.SetSpawn(new Vec3f(1536f, 311f, 1536f));
            Assert.AreEqual(AvatarMoveVerdict.Accepted, validator.Check(state, Pose(1536f, 0, y: 311f, z: 1536f)));
            Assert.AreEqual(AvatarMoveVerdict.Accepted, validator.Check(state, Pose(1536.2f, 33, y: 311f, z: 1535.8f)));
            Assert.AreEqual(AvatarMoveVerdict.Accepted, validator.Check(state, Pose(1536.4f, 66, y: 311f, z: 1535.6f)));
            // 월드 한쪽 끝(3072)까지는 허용, 훨씬 밖은 거부.
            Assert.IsTrue(Game.Application.Protocol.ProtocolConstants.IsInsideWorld(3072f, 3072f));
            Assert.IsFalse(Game.Application.Protocol.ProtocolConstants.IsInsideWorld(3200f, 100f));
        }

        [Test]
        public void LongGap_IsCappedAtMaxDelta_SoTeleportAfterPauseIsRejected()
        {
            var (validator, state) = Create();
            validator.Check(state, Pose(0f, 0));
            // 60초 만에 200 m: 간격은 0.5초로 잘려 허용 거리는 약 3.1 m.
            Assert.AreEqual(AvatarMoveVerdict.Rejected, validator.Check(state, Pose(200f, 60000)));
        }

        [Test]
        public void VerticalSpeed_IsLimited()
        {
            var (validator, state) = Create();
            validator.Check(state, Pose(0f, 0));
            Assert.AreEqual(AvatarMoveVerdict.Rejected, validator.Check(state, Pose(0f, 100, y: 500f)));
            Assert.AreEqual(AvatarMoveVerdict.Accepted, validator.Check(state, Pose(0f, 200, y: -2f)));
        }

        [Test]
        public void TimeWrapAround_IsHandled()
        {
            var (validator, state) = Create();
            validator.Check(state, Pose(0f, uint.MaxValue - 100), 0d);
            Assert.AreEqual(AvatarMoveVerdict.Accepted, validator.Check(state, Pose(1f, 400), 0.5d), "49일 시각이 한 바퀴 돌아도 차이로 계산");
        }

        [Test]
        public void ServerWorld_ServerSideStamina_DrainsWhileRunningPosesAreAccepted()
        {
            // [SDD 3.9.8] 호스트는 수락한 자세의 달리기 플래그로 스태미나를 추정한다(캐릭터의 스태미나 하나로 관리).
            var dispatcher = new CommandDispatcher(new RequestGuard(30, new CommandLimits(1500f)), NullLog.Instance);
            var clock = new FakeClock();
            var world = new ServerWorld(new ServerSettings(), dispatcher, clock, null, NullLog.Instance);
            var player = new PlayerId(5);
            world.AddPlayer(player, "p", false);
            world.CheckAvatarPose(player, Pose(0f, 0), out _);
            for (int i = 1; i <= 10; i++)
            {
                clock.Advance(0.5); // 호스트 시계 기준으로 0.5초씩
                world.CheckAvatarPose(player, Pose(3.5f * i, (uint)(500 * i), running: true), out _);
            }
            world.State.TryGetPlayer(player, out var character);
            Assert.Less(character.Stamina.Current, 100f);
            Assert.AreEqual(50f, character.Stamina.Current, 0.01f, "10번 x 0.5초 달리기 = 5초 x 10/초 = 50 소모");
        }

        [Test]
        public void ServerWorld_UpdatesPlayerPosition_OnlyForAcceptedPose()
        {
            var clock = new FakeClock();
            var dispatcher = new CommandDispatcher(new RequestGuard(30, new CommandLimits(1500f)), NullLog.Instance);
            var log = new ListLog();
            var world = new ServerWorld(new ServerSettings(), dispatcher, clock, null, log);
            var player = new PlayerId(5);
            world.AddPlayer(player, "p", false);

            Assert.AreEqual(AvatarMoveVerdict.Accepted, world.CheckAvatarPose(player, Pose(0f, 0), out _));
            clock.Advance(1.0);
            Assert.AreEqual(AvatarMoveVerdict.Accepted, world.CheckAvatarPose(player, Pose(3f, 1000), out _));
            world.State.TryGetPlayer(player, out var character);
            Assert.AreEqual(3f, character.Position.X, 0.001f);

            clock.Advance(0.1);
            Assert.AreEqual(AvatarMoveVerdict.Rejected, world.CheckAvatarPose(player, Pose(300f, 1100), out Vec3f correction));
            Assert.AreEqual(3f, correction.X, 0.001f, "보정 위치 = 마지막 수락 위치");
            Assert.AreEqual(3f, character.Position.X, 0.001f);
            Assert.IsTrue(log.Warnings.Any(w => w.StartsWith("move_rejected")), "move_rejected 로그(보안 점검 근거)");
        }
    }

    public class ServerWorldPlayersTests
    {
        private static ServerWorld NewWorld(int max = 8)
        {
            var dispatcher = new CommandDispatcher(new RequestGuard(30, new CommandLimits(1500f)), NullLog.Instance);
            return new ServerWorld(new ServerSettings { MaxPlayers = max }, dispatcher, new FakeClock(), null, NullLog.Instance);
        }

        [Test]
        public void AddPlayer_AssignsLowestFreeSlot_AndReusesAfterLeaving()
        {
            ServerWorld world = NewWorld();
            Assert.AreEqual(0, world.AddPlayer(new PlayerId(1), "a", true));
            Assert.AreEqual(1, world.AddPlayer(new PlayerId(2), "b", false));
            Assert.AreEqual(2, world.AddPlayer(new PlayerId(3), "c", false));
            world.RemovePlayer(new PlayerId(2));
            Assert.AreEqual(1, world.AddPlayer(new PlayerId(4), "d", false), "빈 자리를 다시 쓴다");
            Assert.AreEqual(1, world.AddPlayer(new PlayerId(4), "d", false), "같은 플레이어를 두 번 넣어도 같은 자리");
        }

        [Test]
        public void EightPlayersFit_NinthGetsNoSlot()
        {
            ServerWorld world = NewWorld();
            for (ulong i = 1; i <= 8; i++)
            {
                Assert.GreaterOrEqual(world.AddPlayer(new PlayerId(i), "p" + i, i == 1), 0);
            }
            Assert.AreEqual(-1, world.AddPlayer(new PlayerId(9), "p9", false));
            Assert.AreEqual(8, world.State.PlayerCount);
        }

        [Test]
        public void Roster_FollowsPlayers_AndPublishesChanges()
        {
            ServerWorld world = NewWorld();
            world.AddPlayer(new PlayerId(1), "호스트", true);
            world.AddPlayer(new PlayerId(2), "친구", false);
            Assert.AreEqual(2, world.State.Roster.Count);
            Assert.IsTrue(world.State.Roster.HasChanges);
            world.State.Roster.DrainJournal(1024);
            world.RemovePlayer(new PlayerId(2));
            Assert.AreEqual(1, world.State.Roster.Count);
            Assert.IsTrue(world.State.Roster.HasChanges);
        }

        [Test]
        public void Spawn_PositionsDifferBySlot()
        {
            Assert.AreNotEqual(ServerSettings.SpawnFor(0), ServerSettings.SpawnFor(1));
        }

        [Test]
        public void ServerSettings_FromConfig_UsesAssetValues()
        {
            var balance = BalanceValues.CreateCandidate();
            balance.Session.MaxPlayers = 6;
            balance.Stamina.RegenDelaySeconds = 1f;
            var tuning = new NetworkTuning { MaxCommandsPerTick = 7 };
            ServerSettings settings = ServerSettings.FromConfig(balance, tuning);
            Assert.AreEqual(6, settings.MaxPlayers);
            Assert.AreEqual(7, settings.MaxCommandsPerTick);
            Assert.AreEqual(balance.Movement.RunSpeedMps, settings.Move.RunSpeed);
            Assert.AreEqual(1f, settings.Stamina.RegenDelaySeconds, "[SRS v0.10] 회복 지연 1초가 후보값");
        }
    }

    public class SessionServiceTests
    {
        private const string HostVersion = "0.1.0+dev.20261104153012.a1b2c3d";

        private static (SessionService service, ServerWorld world, FakeLobby lobby) Create(bool steam, FakeLobby lobby = null, IBanListStore bans = null, int max = 8)
        {
            var dispatcher = new CommandDispatcher(new RequestGuard(30, new CommandLimits(1500f)), NullLog.Instance);
            var world = new ServerWorld(new ServerSettings { MaxPlayers = max }, dispatcher, new FakeClock(), null, NullLog.Instance);
            lobby = lobby ?? new FakeLobby();
            var info = new HostWorldInfo { WorldName = "재의 숲", HostName = "하루", SeedText = "BENCH-0001", Seed = 5, GeneratorVersion = 0, GameVersion = HostVersion, ContentHash = 0xABUL };
            Game.Domain.Common.GameVersion.TryParse(HostVersion, out var hostVersion);
            var rules = new SessionRules(max, ProtocolConstants.ProtocolVersion, hostVersion, 0xABUL);
            var service = new SessionService(rules, world, steam ? lobby : null, bans, info, steam, NullLog.Instance);
            return (service, world, lobby);
        }

        private static JoinRequest Request(JoinPath path = JoinPath.Friend, string build = HostVersion, ulong hash = 0xABUL, ushort[] gens = null) =>
            new JoinRequest(ProtocolConstants.ProtocolVersion, build, hash, path, 0, gens ?? new ushort[] { 0 });

        [Test]
        public void Open_Steam_CreatesFriendsOnlyLobby_WithMetadata()
        {
            var (service, _, lobby) = Create(true);
            LobbyError? result = null;
            service.Opened += e => result = e;
            service.Open();
            Assert.AreEqual(LobbyError.None, result);
            Assert.IsTrue(service.IsOpen);
            Assert.AreEqual(SessionAccess.FriendsOnly, lobby.CreatedAccess, "[M1] 참가 조건 「친구 전용」 고정");
            Assert.AreEqual(8, lobby.CreatedMax);
            Assert.AreEqual(LobbyMetaKeys.GameIdValue, lobby.Meta[LobbyMetaKeys.Game]);
            Assert.AreEqual("friends", lobby.Meta[LobbyMetaKeys.Access]);
            Assert.AreEqual("재의 숲", lobby.Meta[LobbyMetaKeys.World]);
            Assert.AreEqual("0", lobby.Meta[LobbyMetaKeys.Password]);
        }

        [Test]
        public void Open_DevTransport_SkipsLobby()
        {
            var (service, _, lobby) = Create(false);
            service.Open();
            Assert.IsTrue(service.IsOpen);
            Assert.AreEqual(0, lobby.CreateCalls);
        }

        [Test]
        public void Open_LobbyUnavailable_ReportsError()
        {
            var lobby = new FakeLobby { IsAvailable = false };
            var (service, _, _) = Create(true, lobby);
            LobbyError? result = null;
            service.Opened += e => result = e;
            service.Open();
            Assert.AreEqual(LobbyError.NotAvailable, result);
            Assert.IsFalse(service.IsOpen);
        }

        [Test]
        public void PlayerCountChange_UpdatesLobbyCountMeta()
        {
            var (service, world, lobby) = Create(true);
            service.Open();
            world.AddPlayer(new PlayerId(1), "a", true);
            Assert.AreEqual("1", lobby.Meta[LobbyMetaKeys.Count]);
            world.AddPlayer(new PlayerId(2), "b", false);
            Assert.AreEqual("2", lobby.Meta[LobbyMetaKeys.Count]);
            world.RemovePlayer(new PlayerId(1));
            Assert.AreEqual("1", lobby.Meta[LobbyMetaKeys.Count]);
        }

        [Test]
        public void Evaluate_Steam_RequiresLobbyMembership()
        {
            var lobby = new FakeLobby();
            var (service, _, _) = Create(true, lobby);
            var player = new PlayerId(76561198000000009UL);
            Assert.AreEqual(JoinRejectReason.AccessDenied, service.Evaluate(player, Request()));
            lobby.Members.Add(player);
            Assert.AreEqual(JoinRejectReason.None, service.Evaluate(player, Request()));
        }

        [Test]
        public void Evaluate_Rejects_VersionHashAndBannedAndUnsupportedGenerator()
        {
            var bans = new BanList();
            var lobby = new FakeLobby();
            var (service, _, _) = Create(true, lobby, bans);
            var player = new PlayerId(5);
            lobby.Members.Add(player);
            Assert.AreEqual(JoinRejectReason.VersionMismatch, service.Evaluate(player, Request(build: "0.1.0+dev.20261104153099.a1b2c3d")));
            Assert.AreEqual(JoinRejectReason.VersionMismatch, service.Evaluate(player, Request(hash: 1)));
            Assert.AreEqual(JoinRejectReason.VersionMismatch, service.Evaluate(player, Request(gens: new ushort[] { 3 })));
            bans.Banned.Add(player);
            Assert.AreEqual(JoinRejectReason.Banned, service.Evaluate(player, Request()));
        }

        [Test]
        public void Evaluate_NinthPlayer_IsFull()
        {
            var (service, world, _) = Create(false);
            for (ulong i = 1; i <= 8; i++)
            {
                world.AddPlayer(new PlayerId(i), "p", false);
            }
            Assert.AreEqual(JoinRejectReason.Full, service.Evaluate(new PlayerId(9), Request()));
        }

        [Test]
        public void BuildAccepted_CarriesSlotAndPlayer()
        {
            var (service, _, _) = Create(false);
            JoinAccepted accepted = service.BuildAccepted(new PlayerId(7), 3, 99u);
            Assert.AreEqual(3, accepted.YourSlot);
            Assert.AreEqual(new PlayerId(7), accepted.YourPlayerId);
            Assert.AreEqual("BENCH-0001", accepted.SeedText);
            Assert.AreEqual(8, accepted.MaxPlayers);
        }

        private sealed class BanList : IBanListStore
        {
            public readonly HashSet<PlayerId> Banned = new HashSet<PlayerId>();
            public bool Contains(PlayerId player) => Banned.Contains(player);
        }
    }

    public class PlayerIdentityPolicyTests
    {
        [Test]
        public void Steam_UsesRemoteId_IgnoringDevId()
        {
            // [D-14] 요청에 적힌 값(개발용 ID)은 Steam 전송에서 무시한다. 남의 진행을 가로채는 것을 막는다.
            Assert.IsTrue(PlayerIdentityPolicy.TryResolve(true, "76561198000000001", 12345UL, out PlayerId id));
            Assert.AreEqual(76561198000000001UL, id.Value);
        }

        [TestCase("")]
        [TestCase("abc")]
        [TestCase("0")]
        [TestCase("-5")]
        [TestCase("99999999999999999999999")]
        public void Steam_InvalidRemoteId_IsRejected(string remote)
        {
            Assert.IsFalse(PlayerIdentityPolicy.TryResolve(true, remote, 1UL, out _));
        }

        [Test]
        public void DevTransport_UsesDevPlayerId_AndRequiresIt()
        {
            Assert.IsTrue(PlayerIdentityPolicy.TryResolve(false, "127.0.0.1", 77UL, out PlayerId id));
            Assert.AreEqual(77UL, id.Value);
            Assert.IsFalse(PlayerIdentityPolicy.TryResolve(false, "127.0.0.1", 0UL, out _));
        }
    }

    public class LobbyMetaBuilderTests
    {
        [Test]
        public void Build_HasAllKeysFromSdd37()
        {
            Dictionary<string, string> meta = LobbyMetaBuilder.Build("월드", "호스트", 3, 1, "0.1.0+dev.20261104153012.a1b2c3d", SessionAccess.Public, true);
            foreach (string key in new[] { LobbyMetaKeys.Game, LobbyMetaKeys.Proto, LobbyMetaKeys.Build, LobbyMetaKeys.World, LobbyMetaKeys.Host,
                                           LobbyMetaKeys.Count, LobbyMetaKeys.Reserved, LobbyMetaKeys.Password, LobbyMetaKeys.Access })
            {
                Assert.IsTrue(meta.ContainsKey(key), key);
            }
            Assert.AreEqual("public", meta[LobbyMetaKeys.Access]);
            Assert.AreEqual("1", meta[LobbyMetaKeys.Password]);
            Assert.AreEqual("3", meta[LobbyMetaKeys.Count]);
            Assert.AreEqual("kh_game", LobbyMetaKeys.Game);
            Assert.AreEqual("0.1.0+dev.20261104153012.a1b2c3d", meta[LobbyMetaKeys.Build], "kh_build 값 = 게임 버전 전체 문자열 [NFR-058]");
        }

        [Test]
        public void AccessValue_MapsAllThree()
        {
            Assert.AreEqual("invite", LobbyMetaBuilder.AccessValue(SessionAccess.InviteOnly));
            Assert.AreEqual("friends", LobbyMetaBuilder.AccessValue(SessionAccess.FriendsOnly));
            Assert.AreEqual("public", LobbyMetaBuilder.AccessValue(SessionAccess.Public));
        }
    }

    public class ClientSessionTests
    {
        private static (ClientSession session, FakeNetClient client, ClientWorldMirror mirror, LocalPlayerPoseBuffer pose, FakeClock clock) Create()
        {
            var client = new FakeNetClient();
            var mirror = new ClientWorldMirror();
            var pose = new LocalPlayerPoseBuffer();
            var clock = new FakeClock();
            var info = new ClientHandshakeInfo { GameVersion = "0.1.0+dev.20261104153012.a1b2c3d", ContentHash = 0xABUL, DevPlayerId = 3UL };
            return (new ClientSession(client, mirror, pose, info, clock, NullLog.Instance, 15f), client, mirror, pose, clock);
        }

        private static JoinAccepted Accepted(ulong hash = 0UL) =>
            new JoinAccepted("seed", 1UL, 0, hash, 5u, 8, SessionAccess.FriendsOnly, new PlayerId(3), 2);

        [Test]
        public void Connect_SendsJoinRequest_WithoutOwnSteamId()
        {
            var (session, client, _, _, _) = Create();
            Assert.IsTrue(session.Connect("127.0.0.1", 7770, JoinPath.Friend));
            Assert.AreEqual(ClientSessionState.Connecting, session.State);
            client.SimulateState(NetClientState.Connected);
            var request = (JoinRequest)client.Sent.Single();
            Assert.AreEqual("0.1.0+dev.20261104153012.a1b2c3d", request.GameVersion);
            Assert.AreEqual(0xABUL, request.ContentHash);
            Assert.AreEqual(JoinPath.Friend, request.Path);
            CollectionAssert.AreEqual(new ushort[] { 0 }, request.SupportedGeneratorVersions);
        }

        [Test]
        public void FullFlow_AcceptedThenComplete_ReachesInGame_AndSetsSpawn()
        {
            var (session, client, _, pose, _) = Create();
            var states = new List<ClientSessionState>();
            session.StateChanged += states.Add;
            session.Connect("h", 0, JoinPath.Invite);
            client.SimulateState(NetClientState.Connected);
            client.SimulateReceive(Accepted());
            Assert.AreEqual(ClientSessionState.Syncing, session.State);
            Assert.IsInstanceOf<WorldReady>(client.Sent.Last());
            Assert.AreEqual(2, session.LocalSlot);

            client.SimulateReceive(new JoinComplete(5u, new Vec3f(3f, 0.1f, 0f)));
            Assert.AreEqual(ClientSessionState.InGame, session.State);
            Assert.IsTrue(pose.TryTakeCorrection(out Vec3f spawn));
            Assert.AreEqual(3f, spawn.X, 0.001f);
            CollectionAssert.AreEqual(new[] { ClientSessionState.Connecting, ClientSessionState.Generating, ClientSessionState.Syncing, ClientSessionState.InGame }, states);
        }

        [Test]
        public void VersionMismatchRejection_KeepsHostVersion()
        {
            // [FR-003, IMP-002] 화면이 두 버전을 보이도록 호스트 버전을 세션이 보관한다.
            var (session, client, _, _, _) = Create();
            session.Connect("h", 0, JoinPath.Friend);
            client.SimulateState(NetClientState.Connected);
            client.SimulateReceive(new JoinRejected(JoinRejectReason.VersionMismatch, "0.1.0+rel.20261110090000.9f8e7d6"));
            Assert.AreEqual(JoinRejectReason.VersionMismatch, session.RejectReason);
            Assert.AreEqual("0.1.0+rel.20261110090000.9f8e7d6", session.RejectHostVersion);
        }

        [Test]
        public void Rejected_EndsWithReason_AndDisconnects()
        {
            var (session, client, _, _, _) = Create();
            session.Connect("h", 0, JoinPath.Friend);
            client.SimulateState(NetClientState.Connected);
            client.SimulateReceive(new JoinRejected(JoinRejectReason.Full));
            Assert.AreEqual(ClientSessionState.Ended, session.State);
            Assert.AreEqual(ClientFailure.Rejected, session.Failure);
            Assert.AreEqual(JoinRejectReason.Full, session.RejectReason);
            Assert.AreEqual(NetClientState.Stopped, client.State);
        }

        [Test]
        public void WorldHashMismatch_EndsWithWorldMismatch_AndTellsHost()
        {
            var (session, client, _, _, _) = Create();
            session.PrepareWorld = _ => 0x1111UL;
            session.Connect("h", 0, JoinPath.Friend);
            client.SimulateState(NetClientState.Connected);
            client.SimulateReceive(Accepted(hash: 0x2222UL));
            Assert.AreEqual(ClientFailure.WorldMismatch, session.Failure);
            Assert.AreEqual(0x1111UL, ((WorldReady)client.Sent.Last()).ComputedWorldHash, "호스트도 기록하도록 계산한 값을 알린다");
        }

        [Test]
        public void PrepareWorldException_EndsSession_NotSwallowed()
        {
            var (session, client, _, _, _) = Create();
            session.PrepareWorld = _ => throw new InvalidOperationException("생성 실패");
            session.Connect("h", 0, JoinPath.Friend);
            client.SimulateState(NetClientState.Connected);
            client.SimulateReceive(Accepted());
            Assert.AreEqual(ClientSessionState.Ended, session.State);
            Assert.AreEqual(ClientFailure.WorldMismatch, session.Failure);
        }

        [Test]
        public void NoResponse_TimesOut()
        {
            var (session, client, _, _, clock) = Create();
            session.Connect("h", 0, JoinPath.Friend);
            clock.Advance(14);
            session.Tick();
            Assert.AreEqual(ClientSessionState.Connecting, session.State);
            clock.Advance(2);
            session.Tick();
            Assert.AreEqual(ClientSessionState.Ended, session.State);
            Assert.AreEqual(ClientFailure.HostNoResponse, session.Failure);
        }

        [Test]
        public void ConnectionLostInGame_ReportsConnectionLost()
        {
            var (session, client, _, _, _) = Create();
            session.Connect("h", 0, JoinPath.Friend);
            client.SimulateState(NetClientState.Connected);
            client.SimulateReceive(Accepted());
            client.SimulateReceive(new JoinComplete(1u, Vec3f.Zero));
            client.SimulateState(NetClientState.Stopped);
            Assert.AreEqual(ClientSessionState.Ended, session.State);
            Assert.AreEqual(ClientFailure.ConnectionLost, session.Failure);
        }

        [Test]
        public void Leave_GoesIdle_ClearsMirror_NotAFailure()
        {
            var (session, client, mirror, _, _) = Create();
            session.Connect("h", 0, JoinPath.Friend);
            client.SimulateState(NetClientState.Connected);
            client.SimulateReceive(Accepted());
            client.SimulateReceive(new JoinComplete(1u, Vec3f.Zero));
            mirror.Roster.Set(0, new RosterEntry(new PlayerId(1), "x", true));
            session.Leave();
            Assert.AreEqual(ClientSessionState.Idle, session.State);
            Assert.AreEqual(ClientFailure.LeftByUser, session.Failure);
            Assert.AreEqual(0, mirror.Roster.Count);
        }

        [Test]
        public void UnexpectedMessages_AreIgnored()
        {
            var (session, client, _, _, _) = Create();
            client.SimulateReceive(new JoinComplete(1u, Vec3f.Zero)); // 접속하지 않은 상태
            Assert.AreEqual(ClientSessionState.Idle, session.State);
            session.Connect("h", 0, JoinPath.Friend);
            client.SimulateReceive(new JoinComplete(1u, Vec3f.Zero)); // 허가 전에 온 완료 알림
            Assert.AreEqual(ClientSessionState.Connecting, session.State);
        }

        [Test]
        public void ConnectFailure_EndsImmediately()
        {
            var (session, client, _, _, _) = Create();
            client.ConnectResult = false;
            Assert.IsFalse(session.Connect("h", 0, JoinPath.Friend));
            Assert.AreEqual(ClientSessionState.Ended, session.State);
        }

        [Test]
        public void CanReconnectAfterEnded()
        {
            var (session, client, _, _, _) = Create();
            session.Connect("h", 0, JoinPath.Friend);
            client.SimulateState(NetClientState.Connected);
            client.SimulateReceive(new JoinRejected(JoinRejectReason.Full));
            Assert.IsTrue(session.Connect("h", 0, JoinPath.Friend));
            Assert.AreEqual(ClientSessionState.Connecting, session.State);
            Assert.AreEqual(ClientFailure.None, session.Failure);
        }
    }

    public class LocalPlayerPoseBufferTests
    {
        [Test]
        public void PoseAndCorrection_PassThroughOnce()
        {
            var buffer = new LocalPlayerPoseBuffer();
            Assert.IsFalse(buffer.TryRead(out _));
            buffer.Write(new AvatarPose(new Vec3f(1, 2, 3), 0, 0, false, 5));
            Assert.IsTrue(buffer.TryRead(out AvatarPose pose));
            Assert.AreEqual(2f, pose.Position.Y);

            Assert.IsFalse(buffer.TryTakeCorrection(out _));
            buffer.ApplyCorrection(new Vec3f(9, 9, 9));
            Assert.IsTrue(buffer.TryTakeCorrection(out Vec3f pos));
            Assert.AreEqual(9f, pos.X);
            Assert.IsFalse(buffer.TryTakeCorrection(out _), "보정은 한 번만 가져간다");
        }
    }
}

// =============================================================
// 파일: HostClientLoopbackPlayTests.cs
// 역할: 실제 FishNet(Tugboat 직접 전송) 위에서 호스트 + 호스트 자신의 클라이언트(같은 프로세스 루프백)가 같은 경로로
//       참가(JoinRequest → JoinAccepted → WorldReady → 스냅샷 → JoinComplete)하고, 아바타 RPC(자세 → 호스트 검사 → 중계/보정)와
//       명령(Ping) 왕복이 되는지 확인한다. 메시지 직렬화 할당(R-31)도 측정해 로그로 남긴다.
// 레이어: test (PlayMode, Game.Tests.Integration)
// 관련: FR-003, FR-005~009, NFR-013, NFR-030, NFR-031, NFR-040, DES-081~085, DES-121 / SDD 3.9.2, 3.9.8, D-19, D-23, R-31
//
// 이 PC는 Steam 계정이 하나라 다인 접속은 직접 전송(Tugboat) 다중 프로세스 시험으로 확인한다(별도 절차). 여기서는 같은 프로세스 루프백만.
// 에디터에서만 도는 시험이다(아바타 프리팹을 애셋 경로로 읽는다).
// =============================================================
#if UNITY_EDITOR
using System;
using System.Collections;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using Game.Adapters.Net.Transport;
using Game.Application.Boot;
using Game.Application.Commands;
using Game.Application.Config;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Bootstrap.Runtime;
using Game.Domain.Common;
using Game.Domain.Session;
using Game.Infra.Transport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Application.Session;
using Game.Application.Server;
using Game.Application.Gathering;
using Game.Application.WorldSync;
using Game.Domain.Gathering;

namespace Game.Tests.Integration
{
    public class HostClientLoopbackPlayTests
    {
        private const ulong DevPlayerId = 101UL;
        private const string AvatarPrefabPath = "Assets/_Game/Prefabs/PlayerAvatar.prefab";
        private const float WaitSeconds = 10f;

        // 월드(약 2만 개 자원)를 시험마다 다시 만들지 않도록 같은 시드면 재사용한다.
        private static readonly LocalWorldProvider SharedWorlds = new LocalWorldProvider();

        private GameObject _networkObject;
        private NetworkManager _network;
        private SessionRuntime _runtime;
        private SessionRuntimeDeps _deps;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 아바타 프리팹을 스폰 가능 목록에 넣는다(비어 있으면 FishNet이 런타임 오류를 낸다).
            var avatar = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPrefabPath);
            Assert.IsNotNull(avatar, "PlayerAvatar 프리팹이 없습니다(Game/Setup/Create Avatar Prefab).");
            var prefabs = ScriptableObject.CreateInstance<SinglePrefabObjects>();
            prefabs.AddObject(avatar.GetComponent<NetworkObject>());

            _networkObject = new GameObject("TestNetworkManager");
            _networkObject.SetActive(false);
            // 에디터에서 컴포넌트를 붙일 때 OnValidate가 「SpawnablePrefabs is null」 오류를 한 번 남긴다. 이 구간만 무시한다.
            LogAssert.ignoreFailingMessages = true;
            _network = _networkObject.AddComponent<NetworkManager>();
            _network.SpawnablePrefabs = prefabs;
            LogAssert.ignoreFailingMessages = false;
            _networkObject.SetActive(true);
            yield return null;

            var selector = new TransportSelector(_network, true);
            SelectedTransport selected = selector.Apply(TransportKind.Tugboat);
            var config = ConfigAssembler.Assemble(BalanceValues.CreateCandidate(), new NetworkTuning(), null);
            selector.ApplyTuning(config.Network, config.Balance.Session.DisconnectTimeoutSeconds, selected);
            selector.ApplyConnectionLimit(config.Balance.Session.MaxPlayers, selected);

            _deps = new SessionRuntimeDeps
            {
                Network = _network,
                Transport = TransportKind.Tugboat,
                Config = config,
                Identity = new DevPlayerIdentity(DevPlayerId),
                Lobby = null,
                Clock = UnityClock.Instance,
                Log = UnityLog.Instance,
                Perf = NullPerfProbe.Instance,
                AvatarPrefab = avatar.GetComponent<NetworkObject>(),
                IsDevelopmentBuild = true,
                WorldScale = WorldScale.Full,
                Worlds = SharedWorlds,
            };
        }

        [TearDown]
        public void TearDown()
        {
            _runtime?.Dispose();
            if (_networkObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_networkObject);
            }
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string what)
        {
            float end = Time.realtimeSinceStartup + WaitSeconds;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, end, "시간 초과: " + what);
                yield return null;
            }
        }

        private IEnumerator StartHostAndWaitInGame()
        {
            var world = new HostWorldInfo { WorldName = "loopback", SeedText = "BENCH-0001", Access = SessionAccess.FriendsOnly };
            _runtime = SessionRuntime.CreateHost(_deps, world);
            _runtime.StartHost();
            yield return WaitUntil(() => { _runtime.Tick(); return _runtime.Session.State == ClientSessionState.InGame; }, "호스트 클라이언트가 게임 중 상태가 됨");
        }

        [UnityTest]
        public IEnumerator HostClient_JoinsThroughTheSamePath_AndSeesItselfInTheRoster()
        {
            yield return StartHostAndWaitInGame();

            Assert.AreEqual(new PlayerId(DevPlayerId), _runtime.Session.LocalPlayer);
            Assert.AreEqual(0, _runtime.Session.LocalSlot);
            Assert.AreEqual(1, _runtime.World.State.PlayerCount);

            // 스냅샷 → 월드 사본: 호스트 화면도 사본으로 그린다(D-19).
            yield return WaitUntil(() => _runtime.Mirror.Roster.Count == 1, "명단 스냅샷 수신");
            Assert.IsTrue(_runtime.Mirror.Roster.TryGet(0, out var entry));
            Assert.AreEqual(new PlayerId(DevPlayerId), entry.Player);
            Assert.IsTrue(entry.IsHost);

            // 참가 완료 때 아바타가 스폰된다.
            yield return WaitUntil(() => _network.ServerManager.Objects.Spawned.Count == 1, "아바타 스폰");

            // 호스트가 정한 스폰 위치가 로컬 이동 쪽으로 전달된다.
            Assert.IsTrue(_runtime.Pose.TryTakeCorrection(out Vec3f spawn));
            Assert.AreEqual(_runtime.World.SpawnPositionForSlot(0).X, spawn.X, 0.01f); // 월드가 붙은 호스트의 스폰은 월드 중앙 기준
        }

        [UnityTest]
        public IEnumerator AvatarPose_IsValidatedByHost_AcceptedUpdatesServerPosition_RejectedIsCorrected()
        {
            yield return StartHostAndWaitInGame();
            yield return WaitUntil(() => _network.ServerManager.Objects.Spawned.Count == 1, "아바타 스폰");
            _runtime.Pose.TryTakeCorrection(out _); // 스폰 보정은 비운다

            // 정상 이동: 호스트가 받아들여 서버 쪽 위치가 따라온다.
            uint ms = 1000;
            _runtime.Pose.Write(new AvatarPose(new Vec3f(1f, 0.1f, 1f), 10f, 0f, false, ms));
            yield return WaitUntil(() => _runtime.World.State.TryGetPlayer(new PlayerId(DevPlayerId), out var p) && Math.Abs(p.Position.X - 1f) < 0.01f, "정상 자세가 서버 위치에 반영됨");

            // 순간이동: 속도 한도를 넘으므로 거부하고 마지막 수락 위치로 되돌리라고 알려 준다.
            ms += 100;
            _runtime.Pose.Write(new AvatarPose(new Vec3f(400f, 0.1f, 400f), 10f, 0f, false, ms));
            yield return WaitUntil(() => _runtime.Pose.TryTakeCorrection(out _), "이동 거부 보정 도착");
            _runtime.World.State.TryGetPlayer(new PlayerId(DevPlayerId), out var player);
            Assert.AreEqual(1f, player.Position.X, 0.01f, "거부된 위치는 서버 위치를 바꾸지 않는다");
        }

        [UnityTest]
        public IEnumerator Command_RoundTrips_ThroughGateway_AndIsProcessedByServerWorld()
        {
            yield return StartHostAndWaitInGame();
            CommandResultMessage? result = null;
            _runtime.ClientTransport.Received += message =>
            {
                if (message is CommandResultMessage r)
                {
                    result = r;
                }
            };

            Assert.IsTrue(CommandCodec.TryEncode(new PingCommand(77u, new Vec3f(12f, 0f, 34f)), out CommandMessage command));
            Assert.IsTrue(_runtime.ClientTransport.SendDefault(command));
            yield return WaitUntil(() => result.HasValue, "명령 결과 수신");

            Assert.IsTrue(result.Value.Ok);
            Assert.AreEqual(77u, result.Value.RequestId);
            Assert.IsTrue(_runtime.World.State.TryGetLastPing(new PlayerId(DevPlayerId), out Vec3f ping));
            Assert.AreEqual(12f, ping.X, 0.01f);
        }

        [UnityTest]
        public IEnumerator MalformedPacket_FromClient_DoesNotBreakTheServer()
        {
            yield return StartHostAndWaitInGame();

            // 서버가 받을 수 없는 종류(서버 → 클라이언트 메시지)를 보내도 서버는 계속 돈다.
            Assert.IsTrue(_runtime.ClientTransport.SendDefault(new JoinComplete(1u, Vec3f.Zero)));
            Assert.IsTrue(_runtime.ClientTransport.SendDefault(new CommandMessage(250, 1u, new byte[0])));
            uint tickBefore = _runtime.World.CurrentTick;
            yield return WaitUntil(() => _runtime.World.CurrentTick > tickBefore + 5, "서버 틱이 계속 진행됨");
            Assert.AreEqual(ClientSessionState.InGame, _runtime.Session.State);
        }

        [UnityTest]
        public IEnumerator Serialization_Allocation_IsMeasuredAndReported_R31()
        {
            yield return StartHostAndWaitInGame();
            yield return null;

            Assert.IsTrue(CommandCodec.TryEncode(new PingCommand(1u, Vec3f.Zero), out CommandMessage command));
            const int Iterations = 200;
            const int Trials = 7;
            _runtime.ClientTransport.SendDefault(command); // 첫 호출의 초기화 할당은 제외
            // 에디터에서는 GC 모드를 바꿀 수 없으므로, 짧은 구간(수집이 끼어들 가능성이 낮음)의 Mono 힙 사용량 증가분을 여러 번 재어 중앙값을 쓴다.
            var deltas = new System.Collections.Generic.List<long>();
            for (int t = 0; t < Trials; t++)
            {
                long before = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                for (int i = 0; i < Iterations; i++)
                {
                    _runtime.ClientTransport.SendDefault(command);
                }
                long delta = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - before;
                if (delta >= 0)
                {
                    deltas.Add(delta);
                }
                yield return null;
            }
            deltas.Sort();
            long perSend = deltas.Count == 0 ? 0 : deltas[deltas.Count / 2] / Iterations;

            // [R-31] PacketBroadcast 한 종류로 바이트를 담는 한 겹이 늘어난 비용. 결과는 보고서에 적는다.
            Debug.Log("[R-31] FishNet PacketBroadcast 송신 1건당 관리 힙 할당 약 " + perSend + " B (Ping 명령, " + command.Payload.Length + " B 본문, 시도 " + deltas.Count + "회 중앙값)");
            Assert.Less(perSend, 4096, "송신 1건당 할당이 비정상적으로 크다");
        }

        [UnityTest]
        public IEnumerator LateJoin_With5000ChangedResources_MirrorEqualsHost_ThenHitDeltaArrives()
        {
            // [FR-009, NFR-016, NFR-018] 2만 개 중 5,000개가 이미 바뀐 월드에 (호스트 자신의 클라이언트가) 중도 참가한다.
            // 참가 경로는 참가자와 같다: 시드 → 로컬 생성 → 요약값 비교 → 바뀐 상태만 스냅샷 → 이후 델타.
            var world = new HostWorldInfo { WorldName = "resources", SeedText = "BENCH-0001", Access = SessionAccess.FriendsOnly };
            _runtime = SessionRuntime.CreateHost(_deps, world);
            LocalWorld local = _runtime.World.State.Local;
            Assert.IsNotNull(local);
            Assert.AreNotEqual(0UL, world.ExpectedWorldHash, "호스트가 월드를 만들어 기대 요약값을 정했다");
            Assert.AreEqual(5000, ResourceChangeTool.ApplyChanges(_runtime.World.State.Resources, local, ResourceChangeTool.DefaultChangedCount, 4242UL));

            float start = Time.realtimeSinceStartup;
            _runtime.StartHost();
            yield return WaitUntil(() => { _runtime.Tick(); return _runtime.Session.State == ClientSessionState.InGame; }, "중도 참가 완료");
            float joinSeconds = Time.realtimeSinceStartup - start;

            Assert.AreSame(local, _runtime.Mirror.Local, "참가자 사본이 로컬에서 만든 월드를 가졌다");
            Assert.AreEqual(5000, _runtime.Mirror.Resources.Count);
            foreach (var entry in _runtime.World.State.Resources.Entries)
            {
                Assert.IsTrue(_runtime.Mirror.Resources.TryGet(entry.Key, out ResourceState mirrored));
                Assert.AreEqual(entry.Value, mirrored);
            }
            UnityEngine.Debug.Log("[M1-10] 루프백 중도 참가(5,000개 변경) " + (joinSeconds * 1000f).ToString("F0") + " ms (서버 시작·접속·월드 준비·스냅샷 " + _runtime.Mirror.SnapshotChunksReceived + "조각 포함)");

            // 이후 타격: 서 있는 나무를 때리면 델타 하나가 사본에 반영된다.
            int tree = -1;
            Vec3f treePos = Vec3f.Zero;
            for (int i = 0; i < local.ResourceCount && tree < 0; i++)
            {
                local.TryGetResource(i, out ResourceKind kind, out Vec3f pos);
                if (kind == ResourceKind.Tree && !_runtime.World.State.Resources.TryGet((uint)i, out _))
                {
                    tree = i;
                    treePos = pos;
                }
            }
            Assert.GreaterOrEqual(tree, 0);
            Assert.IsTrue(_runtime.World.State.TryGetPlayer(new PlayerId(DevPlayerId), out var player));
            player.Position = new Vec3f(treePos.X + 1f, treePos.Y, treePos.Z);

            CommandResultMessage? result = null;
            _runtime.ClientTransport.Received += message =>
            {
                if (message is CommandResultMessage r)
                {
                    result = r;
                }
            };
            Assert.IsTrue(CommandCodec.TryEncode(new HitResourceCommand(900u, (ushort)tree, 0), out CommandMessage command));
            Assert.IsTrue(_runtime.ClientTransport.SendDefault(command));
            yield return WaitUntil(() => result.HasValue, "타격 결과 수신");
            Assert.IsTrue(result.Value.Ok, result.Value.Rejection.ToString());
            yield return WaitUntil(() => _runtime.Mirror.Resources.TryGet((uint)tree, out _), "타격 델타가 사본에 반영됨");
            _runtime.Mirror.Resources.TryGet((uint)tree, out ResourceState hit);
            Assert.AreEqual(ResourcePhase.Damaged, hit.Phase);
            Assert.AreEqual(5001, _runtime.Mirror.Resources.Count);
        }

        [UnityTest]
        public IEnumerator LeavingSession_FreesSlot_AndStopsServer()
        {
            yield return StartHostAndWaitInGame();
            _runtime.Session.Leave();
            yield return WaitUntil(() => _runtime.World.State.PlayerCount == 0, "퇴장 처리(자리 반환)");
            Assert.AreEqual(ClientSessionState.Idle, _runtime.Session.State);
        }
    }
}
#endif

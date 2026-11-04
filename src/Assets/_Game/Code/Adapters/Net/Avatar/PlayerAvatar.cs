// =============================================================
// 파일: PlayerAvatar.cs
// 역할: 플레이어 아바타 NetworkObject(DES-084). 소유자가 자세를 ServerRpc(비신뢰)로 보내면 호스트가 이동 검사를 하고,
//       통과한 자세만 ObserversRpc로 다른 참가자에게 중계한다. 거부하면 TargetRpc로 보정 위치를 돌려준다.
//       AvatarSpawner는 참가 완료 때 아바타를 만들고 나가면 지운다.
// 레이어: adapters (Game.Adapters.Net). FishNet 형식을 쓰는 곳은 이 파일과 FishNetPacketTransport뿐이다.
// 관련: FR-023, FR-024, NFR-030, NFR-040, DES-084 / SDD 3.9.8, 3.11.12, D-04, D-24
//
// 검사 로직은 Game.Application의 AvatarMoveValidator에 있고(EditMode 시험), 여기서는 디코드·RPC·호출만 한다.
// 자세 바이트는 Protocol의 AvatarPose 인코딩(20바이트 고정)이다. 표시는 Presentation이 ClientWorldMirror를 읽어 한다.
// =============================================================
using System;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Application.Replication;
using Game.Application.Server;
using Game.Domain.Common;

namespace Game.Adapters.Net.Avatar
{
    /// <summary>
    /// 아바타가 쓰는 서비스 묶음. FishNet이 NetworkBehaviour를 직접 만들기 때문에 조립 루트(Bootstrap)가
    /// 네트워크를 켜기 전에 <see cref="AvatarServices.Current"/>를 채워 둔다.
    /// </summary>
    public sealed class AvatarServices
    {
        /// <summary>현재 서비스. 조립 루트가 채운다.</summary>
        public static AvatarServices Current { get; set; }

        /// <summary>서버 월드. 호스트에서만 있다(참가자 전용 실행이면 null).</summary>
        public ServerWorld World { get; }
        public ClientWorldMirror Mirror { get; }
        public ILocalPlayerPose LocalPose { get; }
        public IClock Clock { get; }
        public ILog Log { get; }
        /// <summary>자세를 보내는 주기(Hz). NetworkTuning.AvatarSendHz.</summary>
        public int SendHz { get; }

        public AvatarServices(ServerWorld world, ClientWorldMirror mirror, ILocalPlayerPose localPose, IClock clock, ILog log, int sendHz)
        {
            World = world;
            Mirror = mirror ?? throw new ArgumentNullException(nameof(mirror));
            LocalPose = localPose ?? throw new ArgumentNullException(nameof(localPose));
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Log = log ?? NullLog.Instance;
            SendHz = Math.Max(1, sendHz);
        }
    }

    /// <summary>플레이어 아바타(NetworkObject).</summary>
    public sealed class PlayerAvatar : NetworkBehaviour
    {
        private readonly SyncVar<ulong> _ownerPlayerId = new SyncVar<ulong>();
        private readonly ByteWriter _writer = new ByteWriter(AvatarPose.ByteSize);
        private PlayerId _serverPlayer;
        private double _nextSendTime;

        /// <summary>이 아바타 주인의 플레이어 ID. 서버가 정해 모든 클라이언트에 동기화한다.</summary>
        public PlayerId OwnerPlayer => new PlayerId(_ownerPlayerId.Value);

        /// <summary>서버에서만: 소유자의 플레이어 ID를 정한다(클라이언트가 보낸 값을 쓰지 않는다).</summary>
        public void ServerInitialize(PlayerId player)
        {
            _serverPlayer = player;
            _ownerPlayerId.Value = player.Value;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner)
            {
                // 소유자는 서버 틱마다 자세를 보낸다(주기는 AvatarSendHz, 기본 30 Hz).
                TimeManager.OnTick += OnOwnerTick;
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (TimeManager != null)
            {
                TimeManager.OnTick -= OnOwnerTick;
            }
        }

        private void OnOwnerTick()
        {
            AvatarServices services = AvatarServices.Current;
            if (services == null)
            {
                return;
            }
            double now = services.Clock.RealtimeSeconds;
            if (now < _nextSendTime)
            {
                return;
            }
            _nextSendTime = now + 1.0 / services.SendHz;

            // [D-04] 위치는 클라이언트가 계산한다. 표현 쪽(FirstPersonController)이 포트에 쓴 가장 최근 자세를 보낸다.
            if (!services.LocalPose.TryRead(out AvatarPose pose))
            {
                return;
            }
            if (MessageCodec.TryEncodeAvatarPose(pose, _writer))
            {
                ServerSubmitPose(_writer.ToArray());
            }
        }

        [ServerRpc(RequireOwnership = true, RunLocally = false)]
        private void ServerSubmitPose(byte[] poseBytes, Channel channel = Channel.Unreliable)
        {
            AvatarServices services = AvatarServices.Current;
            if (services == null || services.World == null || !_serverPlayer.IsValid)
            {
                return;
            }

            // [NFR-040] 디코드 실패는 버린다(예외 없음).
            if (!MessageCodec.TryDecodeAvatarPose(poseBytes, out AvatarPose pose))
            {
                services.Log.Warn("pose_decode_failed", "player=" + _serverPlayer);
                return;
            }

            AvatarMoveVerdict verdict = services.World.CheckAvatarPose(_serverPlayer, pose, out Vec3f correction);
            switch (verdict)
            {
                case AvatarMoveVerdict.Accepted:
                    // 검사를 통과한 자세만 다른 참가자에게 퍼뜨린다. [NFR-030]
                    ObserversPose(poseBytes);
                    break;
                case AvatarMoveVerdict.Rejected:
                    TargetCorrect(Owner, correction.X, correction.Y, correction.Z);
                    break;
            }
        }

        [ObserversRpc(ExcludeOwner = true, BufferLast = false)]
        private void ObserversPose(byte[] poseBytes, Channel channel = Channel.Unreliable)
        {
            AvatarServices services = AvatarServices.Current;
            if (services == null || !MessageCodec.TryDecodeAvatarPose(poseBytes, out AvatarPose pose))
            {
                return;
            }
            // 받은 자세는 월드 사본의 보간 버퍼에 넣는다. 화면(Presentation)은 사본을 읽는다.
            services.Mirror.AddRemotePose(OwnerPlayer, pose, services.Clock.RealtimeSeconds);
        }

        [TargetRpc]
        private void TargetCorrect(NetworkConnection connection, float x, float y, float z, Channel channel = Channel.Reliable)
        {
            // 호스트가 이동을 거부했다: 마지막으로 받아들인 위치로 되돌리라고 로컬 이동 쪽에 알린다.
            AvatarServices.Current?.LocalPose.ApplyCorrection(new Vec3f(x, y, z));
        }
    }

    /// <summary>서버에서 참가 완료한 플레이어의 아바타를 만들고 나가면 지운다.</summary>
    public sealed class AvatarSpawner
    {
        private readonly FishNet.Managing.NetworkManager _network;
        private readonly NetworkObject _prefab;
        private readonly ILog _log;
        private readonly System.Collections.Generic.Dictionary<PlayerId, NetworkObject> _spawned =
            new System.Collections.Generic.Dictionary<PlayerId, NetworkObject>();

        /// <param name="network">NetworkManager</param>
        /// <param name="prefab">PlayerAvatar가 붙은 NetworkObject 프리팹(SpawnablePrefabs에 있어야 한다)</param>
        public AvatarSpawner(FishNet.Managing.NetworkManager network, NetworkObject prefab, ILog log)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _prefab = prefab ?? throw new ArgumentNullException(nameof(prefab));
            _log = log ?? NullLog.Instance;
        }

        /// <summary>참가를 마친 플레이어의 아바타를 만든다(소유자 = 그 연결).</summary>
        public void Spawn(PlayerId player, int connectionId)
        {
            if (_spawned.ContainsKey(player))
            {
                return;
            }
            if (!_network.ServerManager.Clients.TryGetValue(connectionId, out NetworkConnection owner))
            {
                _log.Warn("avatar", "소유자 연결을 찾지 못해 아바타를 만들지 못함 conn=" + connectionId);
                return;
            }
            NetworkObject instance = UnityEngine.Object.Instantiate(_prefab);
            instance.GetComponent<PlayerAvatar>().ServerInitialize(player);
            _network.ServerManager.Spawn(instance, owner);
            _spawned[player] = instance;
        }

        /// <summary>나간 플레이어의 아바타를 지운다.</summary>
        public void Despawn(PlayerId player)
        {
            if (_spawned.TryGetValue(player, out NetworkObject instance))
            {
                _spawned.Remove(player);
                if (instance != null && instance.IsSpawned)
                {
                    _network.ServerManager.Despawn(instance);
                }
            }
        }
    }
}

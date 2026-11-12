// =============================================================
// 파일: CreatureReplicator.cs
// 역할: 서버 틱이 끝날 때 참가자마다 관심 범위 안의 생물을 CreatureSpawn / CreatureSnapshot / CreatureDespawn으로 보낸다.
//       거리 등급(Near/Mid/Far)별로 전송 주기가 다르고, 범위 밖으로 나가면 Despawn, 들어오면 Spawn을 보낸다.
// 레이어: adapters (Game.Adapters.Net). 포트(INetServer)·서버 월드·Protocol 메시지에만 의존한다(FishNet 형식 없음).
// 관련: FR-008, NFR-013, NFR-017, DES-082 / SDD 3.11.9, 3.12.5
//
// 복제 대상은 스냅샷 동기화(JoinComplete까지)가 끝난 연결뿐이다. 중도 참가자도 준비가 끝난 첫 틱에 범위 안 생물을 Spawn으로 받는다.
// 스냅샷은 비신뢰 채널이라 일부가 사라져도 다음 스냅샷이 덮는다. Spawn/Despawn은 신뢰 채널이다.
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Ai;
using Game.Application.Config;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Application.Replication;
using Game.Application.Server;
using Game.Domain.Common;
using Game.Domain.Creatures;

namespace Game.Adapters.Net.Replication
{
    /// <summary>플레이어의 연결 번호를 찾는다(참가를 마친 플레이어만 true).</summary>
    public delegate bool PlayerConnectionLookup(PlayerId player, out int connectionId);

    /// <summary>호스트 쪽 생물 복제 송신기(DES-082).</summary>
    public sealed class CreatureReplicator
    {
        private sealed class Peer
        {
            public readonly HashSet<ushort> Known = new HashSet<ushort>();
            public bool Seen;
        }

        private readonly INetServer _server;
        private readonly ServerWorld _world;
        private readonly CreatureInterestSettings _interest;
        private readonly PlayerConnectionLookup _lookup;
        private readonly Func<int, bool> _isReady;
        private readonly ILog _log;
        private readonly Dictionary<int, Peer> _peers = new Dictionary<int, Peer>();
        private readonly List<int> _drop = new List<int>();
        private readonly List<ushort> _removed = new List<ushort>();
        private readonly List<ushort> _gone = new List<ushort>();
        private readonly CreatureState[] _batch = new CreatureState[ProtocolConstants.MaxCreaturesPerSnapshot];
        private CreatureRegistry _registry;

        /// <param name="server">전송 포트</param>
        /// <param name="world">생물 레지스트리(Ai)를 가진 서버 월드</param>
        /// <param name="tuning">관심 범위와 등급별 전송 Hz</param>
        /// <param name="lookup">플레이어 → 연결 번호</param>
        /// <param name="isReady">연결이 스냅샷 동기화를 마쳤는지</param>
        public CreatureReplicator(INetServer server, ServerWorld world, NetworkTuning tuning, PlayerConnectionLookup lookup, Func<int, bool> isReady, ILog log)
            : this(server, world, CreatureInterestSettings.FromConfig(tuning), lookup, isReady, log)
        {
        }

        public CreatureReplicator(INetServer server, ServerWorld world, CreatureInterestSettings interest, PlayerConnectionLookup lookup, Func<int, bool> isReady, ILog log)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _interest = interest ?? throw new ArgumentNullException(nameof(interest));
            _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
            _isReady = isReady ?? throw new ArgumentNullException(nameof(isReady));
            _log = log ?? NullLog.Instance;
            _world.TickEnded += OnServerTickEnd;
        }

        /// <summary>보낸 Spawn 수(누적, 진단).</summary>
        public long SpawnsSent { get; private set; }

        /// <summary>보낸 Despawn 수(누적, 진단).</summary>
        public long DespawnsSent { get; private set; }

        /// <summary>보낸 스냅샷 항목 수(누적, 진단).</summary>
        public long StatesSent { get; private set; }

        /// <summary>
        /// 보낸 본문 바이트 추정(누적). 종류 바이트까지 세고 전송 계층 머리말은 뺀다.
        /// 스냅샷 7 B + 11 B × 항목, Spawn 14 B, Despawn 3 B.
        /// </summary>
        public long BytesSent { get; private set; }

        /// <summary>이 연결이 지금 알고 있는 생물 수(시험·진단). 연결이 없으면 0.</summary>
        public int KnownCount(int connectionId) => _peers.TryGetValue(connectionId, out Peer p) ? p.Known.Count : 0;

        /// <summary>이 연결이 이 생물을 알고 있는지(시험·진단).</summary>
        public bool Knows(int connectionId, ushort creatureId) => _peers.TryGetValue(connectionId, out Peer p) && p.Known.Contains(creatureId);

        /// <summary>구독을 푼다.</summary>
        public void Dispose()
        {
            _world.TickEnded -= OnServerTickEnd;
            if (_registry != null)
            {
                _registry.Despawned -= OnCreatureRemoved;
                _registry = null;
            }
        }

        private void OnCreatureRemoved(ushort id) => _removed.Add(id);

        private void OnServerTickEnd(uint tick)
        {
            AiScheduler ai = _world.Ai;
            if (ai == null)
            {
                return;
            }
            if (_registry != ai.Registry)
            {
                if (_registry != null)
                {
                    _registry.Despawned -= OnCreatureRemoved;
                }
                _registry = ai.Registry;
                _registry.Despawned += OnCreatureRemoved;
            }

            foreach (Peer p in _peers.Values)
            {
                p.Seen = false;
            }

            foreach (Game.Domain.Players.PlayerCharacter player in _world.State.Players)
            {
                if (!_lookup(player.Id, out int connectionId) || !_isReady(connectionId))
                {
                    continue;
                }
                if (!_peers.TryGetValue(connectionId, out Peer peer))
                {
                    peer = new Peer();
                    _peers[connectionId] = peer;
                }
                peer.Seen = true;
                ReplicateTo(connectionId, peer, player.Position, tick);
            }
            _removed.Clear();

            // 나간 연결의 기억을 지운다(같은 번호로 다시 들어오면 Spawn부터 다시 받는다).
            _drop.Clear();
            foreach (KeyValuePair<int, Peer> pair in _peers)
            {
                if (!pair.Value.Seen)
                {
                    _drop.Add(pair.Key);
                }
            }
            for (int i = 0; i < _drop.Count; i++)
            {
                _peers.Remove(_drop[i]);
            }
        }

        private void ReplicateTo(int connectionId, Peer peer, Vec3f at, uint tick)
        {
            // 레지스트리에서 사라진 생물은 거리와 무관하게 Despawn한다. [FR-008]
            for (int i = 0; i < _removed.Count; i++)
            {
                if (peer.Known.Remove(_removed[i]))
                {
                    SendDespawn(connectionId, _removed[i]);
                }
            }

            IReadOnlyList<Creature> creatures = _registry.Creatures;
            int count = 0;
            for (int i = 0; i < creatures.Count; i++)
            {
                Creature c = creatures[i];
                bool known = peer.Known.Contains(c.Id);
                float dx = c.Position.X - at.X;
                float dy = c.Position.Y - at.Y;
                float dz = c.Position.Z - at.Z;
                InterestTier tier = _interest.Classify(dx * dx + dy * dy + dz * dz, known);

                if (tier == InterestTier.Out)
                {
                    if (known)
                    {
                        peer.Known.Remove(c.Id);
                        SendDespawn(connectionId, c.Id);
                    }
                    continue;
                }

                if (!known)
                {
                    peer.Known.Add(c.Id);
                    var spawn = new CreatureSpawn(c.Definition.DefId, StateOf(c));
                    if (_server.Send(connectionId, spawn, NetChannel.Reliable))
                    {
                        SpawnsSent++;
                        BytesSent += 1 + 2 + CreatureState.ByteSize;
                    }
                    else
                    {
                        _log.Warn("replication", "CreatureSpawn 전송 실패 conn=" + connectionId);
                    }
                    continue; // Spawn에 현재 상태가 들어 있으므로 이번 틱 스냅샷은 건너뛴다
                }

                // [3.11.9] 번호로 위상을 나눠 틱마다 고른 부하가 되게 한다.
                int interval = _interest.IntervalOf(tier);
                if (interval > 0 && (tick + c.Id) % (uint)interval == 0)
                {
                    _batch[count++] = StateOf(c);
                    if (count == _batch.Length)
                    {
                        Flush(connectionId, tick, count);
                        count = 0;
                    }
                }
            }
            if (count > 0)
            {
                Flush(connectionId, tick, count);
            }
        }

        private static CreatureState StateOf(Creature c) =>
            CreatureState.FromWorld(c.Id, c.Position, c.YawDegrees, (byte)c.Mode, c.HealthPercent);

        private void SendDespawn(int connectionId, ushort id)
        {
            if (_server.Send(connectionId, new CreatureDespawn(id), NetChannel.Reliable))
            {
                DespawnsSent++;
                BytesSent += 1 + 2;
            }
        }

        private void Flush(int connectionId, uint tick, int count)
        {
            // 메시지가 배열 그대로를 들고 있으므로 보낼 길이만큼 새로 만든다(전송이 즉시 인코딩하지만 시험 대역은 보관한다).
            var states = new CreatureState[count];
            Array.Copy(_batch, states, count);
            if (_server.Send(connectionId, new CreatureSnapshot(tick, states), NetChannel.Unreliable))
            {
                StatesSent += count;
                BytesSent += 1 + 4 + 2 + count * CreatureState.ByteSize;
            }
        }
    }
}

// =============================================================
// 파일: ReplicationPublisher.cs
// 역할: 서버 틱이 끝날 때 복제 표의 변경(델타)을 준비된 연결에 보내고, 새로 들어온 연결에는 스냅샷 조각을 속도 제한으로 보낸 뒤
//       보관해 둔 델타를 이어서 보낸다.
// 레이어: adapters (Game.Adapters.Net). 포트(INetServer)와 Protocol 메시지에만 의존한다(FishNet 형식 없음).
// 관련: FR-008, FR-009, NFR-013~016, NFR-018, DES-082 / SDD 3.9.2, 3.11.8, D-05
//
// 순서 보장: 스냅샷 조각(표 순서) → JoinComplete(완료 콜백) → 보관해 둔 델타 → 이후 매 틱 델타.
// 델타는 덮어쓰기라서 스냅샷과 겹쳐도 결과가 같다.
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Config;
using Game.Application.Perf;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Application.Replication;
using Game.Application.Server;

namespace Game.Adapters.Net.Replication
{
    /// <summary>호스트 쪽 복제 송신기(DES-082).</summary>
    public sealed class ReplicationPublisher
    {
        private sealed class Connection
        {
            public bool Ready;
            public readonly Queue<SnapshotChunk> Chunks = new Queue<SnapshotChunk>();
            public readonly List<TableDelta> Pending = new List<TableDelta>();
            public double Budget;
            public Action<int> OnSyncComplete;
        }

        private readonly INetServer _server;
        private readonly ServerWorld _world;
        private readonly IPerfProbe _perf;
        private readonly ILog _log;
        private readonly int _bytesPerSecond;
        private readonly float _tickSeconds;
        private readonly Dictionary<int, Connection> _connections = new Dictionary<int, Connection>();
        private readonly List<int> _scratch = new List<int>();

        /// <param name="server">전송 포트</param>
        /// <param name="world">복제할 표를 가진 서버 월드</param>
        /// <param name="tuning">스냅샷 속도 상한(SnapshotBytesPerSecond)과 틱 주기를 읽는다</param>
        public ReplicationPublisher(INetServer server, ServerWorld world, NetworkTuning tuning, IPerfProbe perf, ILog log)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }
            _perf = perf ?? NullPerfProbe.Instance;
            _log = log ?? NullLog.Instance;
            _bytesPerSecond = Math.Max(1, tuning.SnapshotBytesPerSecond);
            _tickSeconds = 1f / Math.Max(1, tuning.ServerTickRateHz);
            _world.TickEnded += OnServerTickEnd;
        }

        /// <summary>스냅샷을 아직 다 받지 못한 연결 수(시험·진단).</summary>
        public int SyncingCount
        {
            get
            {
                int count = 0;
                foreach (Connection c in _connections.Values)
                {
                    if (!c.Ready)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public bool IsReady(int connectionId) => _connections.TryGetValue(connectionId, out Connection c) && c.Ready;

        /// <summary>
        /// 연결의 스냅샷 전송을 시작한다. 이 시점의 모든 표 전체 상태를 조각으로 떠 두고, 이후 델타는 보관한다.
        /// </summary>
        /// <param name="connectionId">연결 번호</param>
        /// <param name="onSyncComplete">스냅샷을 다 보낸 직후(보관 델타를 보내기 전) 부르는 콜백. JoinComplete를 보낸다</param>
        public void BeginSync(int connectionId, Action<int> onSyncComplete)
        {
            var connection = new Connection { OnSyncComplete = onSyncComplete };
            uint tick = _world.CurrentTick;
            foreach (IReplicatedTable table in _world.Tables)
            {
                IReadOnlyList<byte[]> payloads = table.CaptureSnapshot(ProtocolConstants.MaxChunkPayloadBytes);
                for (int i = 0; i < payloads.Count; i++)
                {
                    connection.Chunks.Enqueue(new SnapshotChunk(tick, table.TableId, (ushort)i, (ushort)payloads.Count, payloads[i]));
                }
            }
            _connections[connectionId] = connection;
        }

        /// <summary>연결이 끊기면 상태를 지운다.</summary>
        public void RemoveConnection(int connectionId) => _connections.Remove(connectionId);

        private void OnServerTickEnd(uint tick)
        {
            // 1) 표마다 변경 기록을 비워 델타 메시지로 만든다.
            foreach (IReplicatedTable table in _world.Tables)
            {
                if (!table.HasChanges)
                {
                    continue;
                }
                IReadOnlyList<byte[]> payloads = table.DrainJournal(ProtocolConstants.MaxChunkPayloadBytes);
                for (int i = 0; i < payloads.Count; i++)
                {
                    var delta = new TableDelta(tick, table.TableId, payloads[i]);
                    foreach (KeyValuePair<int, Connection> pair in _connections)
                    {
                        if (pair.Value.Ready)
                        {
                            _server.Send(pair.Key, delta, NetChannel.Reliable);
                        }
                        else
                        {
                            pair.Value.Pending.Add(delta); // 스냅샷을 받는 중인 연결은 보관
                        }
                    }
                }
            }

            // 2) 스냅샷 전송을 속도 제한 안에서 진행한다.
            _scratch.Clear();
            foreach (KeyValuePair<int, Connection> pair in _connections)
            {
                if (!pair.Value.Ready)
                {
                    _scratch.Add(pair.Key);
                }
            }
            for (int i = 0; i < _scratch.Count; i++)
            {
                AdvanceSnapshot(_scratch[i], _connections[_scratch[i]]);
            }
        }

        private void AdvanceSnapshot(int connectionId, Connection c)
        {
            // [3.9.2] 스냅샷은 약 1.5 Mbps로 제한한다. 한 조각이 한 틱 몫보다 커도 쌓인 몫이 차면 보낸다.
            double cap = Math.Max(_bytesPerSecond, ProtocolConstants.MaxChunkPayloadBytes);
            c.Budget = Math.Min(cap, c.Budget + _bytesPerSecond * _tickSeconds);

            while (c.Chunks.Count > 0)
            {
                SnapshotChunk next = c.Chunks.Peek();
                if (c.Budget < next.Payload.Length)
                {
                    return;
                }
                c.Budget -= next.Payload.Length;
                c.Chunks.Dequeue();
                if (!_server.Send(connectionId, next, NetChannel.Reliable))
                {
                    _log.Warn("replication", "스냅샷 조각 전송 실패 conn=" + connectionId);
                }
            }

            // 다 보냈다: JoinComplete → 보관한 델타 → 준비 완료.
            c.OnSyncComplete?.Invoke(connectionId);
            for (int i = 0; i < c.Pending.Count; i++)
            {
                _server.Send(connectionId, c.Pending[i], NetChannel.Reliable);
            }
            c.Pending.Clear();
            c.Ready = true;
        }
    }
}

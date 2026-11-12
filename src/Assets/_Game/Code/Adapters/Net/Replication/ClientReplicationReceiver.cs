// =============================================================
// 파일: ClientReplicationReceiver.cs
// 역할: 클라이언트 쪽 복제 수신. 스냅샷 조각과 표 델타를 ClientWorldMirror에 적용한다.
//       스냅샷을 다 받기 전(JoinComplete 전)에 도착한 델타는 보관했다가 JoinComplete 때 순서대로 적용한다.
// 레이어: adapters (Game.Adapters.Net). 포트(INetClient)와 Protocol 메시지에만 의존한다(FishNet 형식 없음).
// 관련: FR-008, FR-009, NFR-015, NFR-040, DES-083 / SDD 3.9.2, 3.11.8
//
// 호스트 서버가 순서를 보장해(스냅샷 → JoinComplete → 보관 델타 → 이후 델타) 보내므로 정상이면 보관할 델타가 없다.
// 이 보관은 순서가 어긋난 경우에도 옛 스냅샷이 새 델타를 덮어쓰지 않게 하는 안전장치다(델타는 덮어쓰기라 겹쳐도 결과가 같다).
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Application.Replication;

namespace Game.Adapters.Net.Replication
{
    /// <summary>클라이언트 복제 수신기(DES-083).</summary>
    public sealed class ClientReplicationReceiver
    {
        /// <summary>스냅샷이 끝나기 전에 보관할 수 있는 델타 수 상한. 넘으면 버리고 기록한다(메모리 폭주 방어).</summary>
        public const int MaxHeldDeltas = 20000;

        private readonly INetClient _client;
        private readonly ClientWorldMirror _mirror;
        private readonly ILog _log;
        private readonly IClock _clock;
        private readonly List<TableDelta> _held = new List<TableDelta>();
        private bool _snapshotDone = true; // 참가 절차 밖(JoinAccepted를 받기 전)에는 받은 대로 적용

        /// <param name="clock">생물 보간에 쓰는 받은 시각. null이면 시각이 0으로 고정되므로 시험에서만 생략한다</param>
        public ClientReplicationReceiver(INetClient client, ClientWorldMirror mirror, ILog log, IClock clock = null)
        {
            _clock = clock;
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _mirror = mirror ?? throw new ArgumentNullException(nameof(mirror));
            _log = log ?? NullLog.Instance;
            _client.Received += OnReceived;
        }

        /// <summary>스냅샷이 끝나기를 기다리며 보관 중인 델타 수(시험·진단).</summary>
        public int HeldDeltaCount => _held.Count;

        /// <summary>구독을 푼다.</summary>
        public void Dispose() => _client.Received -= OnReceived;

        private void OnReceived(INetMessage message)
        {
            try
            {
                switch (message)
                {
                    case JoinAccepted _:
                        // 새 참가 절차의 시작: 스냅샷이 올 때까지 델타는 보관한다.
                        _snapshotDone = false;
                        _held.Clear();
                        break;

                    case SnapshotChunk chunk:
                        if (!_mirror.ApplySnapshotChunk(chunk))
                        {
                            _log.Warn("replication", "스냅샷 조각 적용 실패 table=" + chunk.TableId);
                        }
                        break;

                    case JoinComplete _:
                        // 스냅샷 끝: 보관한 델타를 받은 순서대로 적용하고 이후는 바로 적용한다.
                        _snapshotDone = true;
                        for (int i = 0; i < _held.Count; i++)
                        {
                            ApplyDelta(_held[i]);
                        }
                        _held.Clear();
                        break;

                    case CreatureSpawn spawn:
                        // [FR-008] 관심 범위에 들어온 생물. 첫 스냅샷은 틱이 더 커서 그대로 적용된다.
                        _mirror.Creatures.ApplySpawn(spawn, 0u, Now());
                        break;

                    case CreatureDespawn despawn:
                        _mirror.Creatures.ApplyDespawn(despawn.Id);
                        break;

                    case CreatureSnapshot creatures:
                        _mirror.Creatures.ApplySnapshot(creatures, Now());
                        break;

                    case TableDelta delta:
                        if (_snapshotDone)
                        {
                            ApplyDelta(delta);
                        }
                        else if (_held.Count < MaxHeldDeltas)
                        {
                            _held.Add(delta);
                        }
                        else
                        {
                            _log.Warn("replication", "스냅샷 전 델타 보관 한도를 넘어 버림 table=" + delta.TableId);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error("replication", "복제 수신 중 예외: " + ex);
            }
        }

        private double Now() => _clock != null ? _clock.RealtimeSeconds : 0d;

        private void ApplyDelta(in TableDelta delta)
        {
            if (!_mirror.ApplyDelta(delta))
            {
                _log.Warn("replication", "델타 적용 실패 table=" + delta.TableId);
            }
        }
    }
}

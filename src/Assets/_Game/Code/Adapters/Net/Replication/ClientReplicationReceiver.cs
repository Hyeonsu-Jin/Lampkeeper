// =============================================================
// 파일: ClientReplicationReceiver.cs
// 역할: 클라이언트 쪽 복제 수신. 스냅샷 조각과 표 델타를 ClientWorldMirror에 적용한다.
// 레이어: adapters (Game.Adapters.Net). 포트(INetClient)와 Protocol 메시지에만 의존한다(FishNet 형식 없음).
// 관련: FR-008, FR-009, NFR-015, NFR-040, DES-083 / SDD 3.9.2, 3.11.8
//
// 호스트 서버가 순서를 보장해(스냅샷 → JoinComplete → 보관 델타 → 이후 델타) 보내므로 도착 순서대로 적용하면 된다.
// =============================================================
using System;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Application.Replication;

namespace Game.Adapters.Net.Replication
{
    /// <summary>클라이언트 복제 수신기(DES-083).</summary>
    public sealed class ClientReplicationReceiver
    {
        private readonly INetClient _client;
        private readonly ClientWorldMirror _mirror;
        private readonly ILog _log;

        public ClientReplicationReceiver(INetClient client, ClientWorldMirror mirror, ILog log)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _mirror = mirror ?? throw new ArgumentNullException(nameof(mirror));
            _log = log ?? NullLog.Instance;
            _client.Received += OnReceived;
        }

        /// <summary>구독을 푼다.</summary>
        public void Dispose() => _client.Received -= OnReceived;

        private void OnReceived(INetMessage message)
        {
            try
            {
                switch (message)
                {
                    case SnapshotChunk chunk:
                        if (!_mirror.ApplySnapshotChunk(chunk))
                        {
                            _log.Warn("replication", "스냅샷 조각 적용 실패 table=" + chunk.TableId);
                        }
                        break;
                    case TableDelta delta:
                        if (!_mirror.ApplyDelta(delta))
                        {
                            _log.Warn("replication", "델타 적용 실패 table=" + delta.TableId);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error("replication", "복제 수신 중 예외: " + ex);
            }
        }
    }
}

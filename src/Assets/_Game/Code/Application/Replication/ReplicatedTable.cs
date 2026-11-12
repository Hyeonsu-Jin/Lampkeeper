// =============================================================
// 파일: ReplicatedTable.cs
// 역할: 「ID → 상태」 표 하나를 복제하는 모델. 호스트는 변경 기록(저널)을 비우며 델타를 만들고, 새 참가자용 스냅샷을 조각으로 뜬다.
//       클라이언트는 같은 표에 델타·스냅샷 조각을 적용한다.
// 레이어: application (Game.Application/Replication, 순수 C#)
// 관련: FR-008(모든 플레이어에게 같게), FR-009(중도 참가), NFR-013~016, DES-053 / SDD 3.3.2, 3.11.8, D-05
//
// 바이트 배치(본문): [u16 바뀐 개수] [키 + 상태]… [u16 지운 개수] [키]…   (키 폭은 표마다 2 또는 4바이트)
// 자원 표는 키 2B + 상태 2B = 항목당 4B(3.11.8). 스냅샷 조각과 델타가 같은 배치를 써서 같은 적용 함수로 처리한다.
// 적용은 「덮어쓰기(upsert)」라서 같은 변경이 두 번 와도 결과가 같다(스냅샷과 델타가 겹치는 구간 대비).
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Protocol;

namespace Game.Application.Replication
{
    /// <summary>표 상태 하나를 바이트로 쓰고 읽는 규칙. 키 폭도 여기서 정한다.</summary>
    public interface IStateCodec<TState>
    {
        /// <summary>키 폭(바이트). 2 또는 4.</summary>
        int KeyBytes { get; }

        /// <summary>상태 하나의 최대 바이트 수(조각 분할 계산용).</summary>
        int MaxStateBytes { get; }

        void Write(ByteWriter writer, in TState state);

        /// <summary>읽기에 실패하면 false(예외 없음).</summary>
        bool TryRead(ByteReader reader, out TState state);
    }

    /// <summary>표 종류에 상관없이 복제기가 다루는 공통 모양.</summary>
    public interface IReplicatedTable
    {
        /// <summary>표 번호. 호스트·클라이언트가 같아야 한다(<see cref="TableIds"/>).</summary>
        ushort TableId { get; }

        /// <summary>항목 수.</summary>
        int Count { get; }

        /// <summary>아직 보내지 않은 변경이 있는지.</summary>
        bool HasChanges { get; }

        /// <summary>바뀔 때마다 늘어나는 번호(화면이 다시 그릴지 판단).</summary>
        int Version { get; }

        /// <summary>변경 기록을 비우고 maxBytes 이하 본문 목록으로 돌려준다. 변경이 없으면 빈 목록.</summary>
        IReadOnlyList<byte[]> DrainJournal(int maxBytes);

        /// <summary>지금 전체 상태를 maxBytes 이하 본문 조각 목록으로 돌려준다(변경 기록은 건드리지 않음).</summary>
        IReadOnlyList<byte[]> CaptureSnapshot(int maxBytes);

        /// <summary>본문을 적용한다(델타·스냅샷 조각 공용). 잘못된 본문이면 false이고 이미 적용된 부분은 되돌리지 않는다.</summary>
        bool ApplyPayload(byte[] payload);

        /// <summary>모두 지운다(세션을 떠날 때).</summary>
        void Clear();
    }

    /// <summary>표 번호 상수. 한 번 정하면 바꾸지 않는다.</summary>
    public static class TableIds
    {
        /// <summary>접속자 명단(자리 번호 → 플레이어). M1-07.</summary>
        public const ushort Roster = 1;

        /// <summary>자원 오브젝트(ResourceIndex → 상태). M1-10.</summary>
        public const ushort Resources = 2;
    }

    /// <summary>복제되는 표. 키는 uint이고 표마다 전송 폭(2/4B)이 다르다.</summary>
    public sealed class ReplicatedTable<TState> : IReplicatedTable
    {
        private static readonly IReadOnlyList<byte[]> NoPayloads = Array.Empty<byte[]>();

        private readonly IStateCodec<TState> _codec;
        private readonly Dictionary<uint, TState> _entries = new Dictionary<uint, TState>();
        private readonly List<uint> _dirtyOrder = new List<uint>();
        private readonly HashSet<uint> _dirtySet = new HashSet<uint>();
        private readonly List<uint> _removed = new List<uint>();

        /// <param name="tableId">표 번호</param>
        /// <param name="codec">상태 바이트 규칙</param>
        /// <exception cref="ArgumentNullException">codec이 null일 때</exception>
        /// <exception cref="ArgumentOutOfRangeException">키 폭이 2·4가 아닐 때</exception>
        public ReplicatedTable(ushort tableId, IStateCodec<TState> codec)
        {
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            if (codec.KeyBytes != 2 && codec.KeyBytes != 4)
            {
                throw new ArgumentOutOfRangeException(nameof(codec), "키 폭은 2 또는 4바이트여야 한다.");
            }
            TableId = tableId;
        }

        public ushort TableId { get; }
        public int Count => _entries.Count;
        public bool HasChanges => _dirtyOrder.Count > 0 || _removed.Count > 0;
        public int Version { get; private set; }

        /// <summary>항목 읽기.</summary>
        public bool TryGet(uint id, out TState state) => _entries.TryGetValue(id, out state);

        /// <summary>전체 항목(순서 보장 없음). 화면·시험이 읽는다.</summary>
        public IEnumerable<KeyValuePair<uint, TState>> Entries => _entries;

        /// <summary>항목을 넣거나 바꾼다. 변경 기록에 남는다(삽입 순서 유지).</summary>
        /// <exception cref="ArgumentOutOfRangeException">키가 키 폭에 담기지 않을 때</exception>
        public void Set(uint id, TState state)
        {
            CheckKey(id);
            _entries[id] = state;
            if (_dirtySet.Add(id))
            {
                _dirtyOrder.Add(id);
            }
            _removed.Remove(id);
            Version++;
        }

        /// <summary>항목을 지운다. 있었으면 변경 기록에 지움으로 남는다.</summary>
        public bool Remove(uint id)
        {
            CheckKey(id);
            if (!_entries.Remove(id))
            {
                return false;
            }
            if (_dirtySet.Remove(id))
            {
                _dirtyOrder.Remove(id);
            }
            _removed.Add(id);
            Version++;
            return true;
        }

        public void Clear()
        {
            _entries.Clear();
            _dirtyOrder.Clear();
            _dirtySet.Clear();
            _removed.Clear();
            Version++;
        }

        public IReadOnlyList<byte[]> DrainJournal(int maxBytes)
        {
            if (!HasChanges)
            {
                return NoPayloads;
            }
            var changed = new List<uint>(_dirtyOrder);
            var removed = new List<uint>(_removed);
            _dirtyOrder.Clear();
            _dirtySet.Clear();
            _removed.Clear();

            var result = new List<byte[]>();
            EncodeSets(changed, maxBytes, result);
            EncodeRemovals(removed, maxBytes, result);
            return result;
        }

        public IReadOnlyList<byte[]> CaptureSnapshot(int maxBytes)
        {
            if (_entries.Count == 0)
            {
                return NoPayloads;
            }
            // 키 순서로 정렬해 같은 상태는 같은 바이트가 되게 한다(시험 재현성).
            var keys = new List<uint>(_entries.Keys);
            keys.Sort();
            var result = new List<byte[]>();
            EncodeSets(keys, maxBytes, result);
            return result;
        }

        public bool ApplyPayload(byte[] payload)
        {
            var reader = new ByteReader(payload);
            int changedCount = reader.ReadU16();
            for (int i = 0; i < changedCount; i++)
            {
                uint key = ReadKey(reader);
                if (!_codec.TryRead(reader, out TState state) || reader.Failed)
                {
                    return false;
                }
                _entries[key] = state;
            }
            int removedCount = reader.ReadU16();
            for (int i = 0; i < removedCount; i++)
            {
                uint key = ReadKey(reader);
                if (reader.Failed)
                {
                    return false;
                }
                _entries.Remove(key);
            }
            Version++;
            return reader.IsCleanEnd;
        }

        // ---------- 내부 ----------

        private void CheckKey(uint id)
        {
            if (_codec.KeyBytes == 2 && id > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "이 표의 키는 2바이트라 65535를 넘을 수 없다.");
            }
        }

        private uint ReadKey(ByteReader reader) => _codec.KeyBytes == 2 ? reader.ReadU16() : reader.ReadU32();

        private void WriteKey(ByteWriter writer, uint key)
        {
            if (_codec.KeyBytes == 2)
            {
                writer.WriteU16((ushort)key);
            }
            else
            {
                writer.WriteU32(key);
            }
        }

        /// <summary>바뀐 항목들을 maxBytes 이하 본문으로 나눠 result에 더한다.</summary>
        private void EncodeSets(List<uint> keys, int maxBytes, List<byte[]> result)
        {
            int entryMax = _codec.KeyBytes + _codec.MaxStateBytes;
            int perPayload = Math.Max(1, Math.Min((maxBytes - 4) / entryMax, ushort.MaxValue));
            var body = new ByteWriter(Math.Max(maxBytes, entryMax + 8));
            int index = 0;
            while (index < keys.Count)
            {
                int take = Math.Min(perPayload, keys.Count - index);
                body.Reset();
                body.WriteU16((ushort)take);
                for (int i = 0; i < take; i++)
                {
                    uint key = keys[index + i];
                    WriteKey(body, key);
                    _codec.Write(body, _entries[key]);
                }
                body.WriteU16(0); // 지운 개수 0
                result.Add(body.ToArray());
                index += take;
            }
        }

        private void EncodeRemovals(List<uint> keys, int maxBytes, List<byte[]> result)
        {
            int perPayload = Math.Max(1, Math.Min((maxBytes - 4) / _codec.KeyBytes, ushort.MaxValue));
            var body = new ByteWriter(Math.Max(maxBytes, _codec.KeyBytes + 8));
            int index = 0;
            while (index < keys.Count)
            {
                int take = Math.Min(perPayload, keys.Count - index);
                body.Reset();
                body.WriteU16(0); // 바뀐 개수 0
                body.WriteU16((ushort)take);
                for (int i = 0; i < take; i++)
                {
                    WriteKey(body, keys[index + i]);
                }
                result.Add(body.ToArray());
                index += take;
            }
        }
    }
}

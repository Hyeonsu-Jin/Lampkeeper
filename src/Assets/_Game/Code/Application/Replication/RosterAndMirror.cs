// =============================================================
// 파일: RosterAndMirror.cs
// 역할: 접속자 명단 항목(RosterEntry)과 그 바이트 규칙, 클라이언트가 읽는 월드 사본(ClientWorldMirror),
//       다른 참가자 아바타 자세 보간 버퍼(RemoteAvatarTrack).
// 레이어: application (Game.Application/Replication, 순수 C#)
// 관련: FR-008, FR-009, FR-023(다른 플레이어 화면에도 그 위치로 보임), FR-160, DES-053, DES-072 / SDD 3.3.2, 3.9.8, 3.11.8
//
// 클라이언트의 화면(Presentation)은 이 사본만 읽는다. 호스트의 화면도 같은 경로로 그린다(D-19).
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Gathering;
using Game.Application.Protocol;
using Game.Application.WorldSync;
using Game.Domain.Common;
using Game.Domain.Gathering;

namespace Game.Application.Replication
{
    /// <summary>접속자 한 명의 공개 정보. 자리 번호(0 ~ 최대 인원-1)가 표의 키다.</summary>
    public readonly struct RosterEntry : IEquatable<RosterEntry>
    {
        public PlayerId Player { get; }
        public string Name { get; }
        /// <summary>호스트 본인인지(명단 표시용).</summary>
        public bool IsHost { get; }

        public RosterEntry(PlayerId player, string name, bool isHost)
        {
            Player = player;
            Name = name ?? string.Empty;
            IsHost = isHost;
        }

        public bool Equals(RosterEntry other) => Player == other.Player && Name == other.Name && IsHost == other.IsHost;
        public override bool Equals(object obj) => obj is RosterEntry other && Equals(other);
        public override int GetHashCode() => Player.GetHashCode() * 397 ^ Name.GetHashCode();
    }

    /// <summary>명단 항목의 바이트 규칙: 키 2B(자리 번호) + 상태(ID 8B, 호스트 1B, 이름 길이 1B + UTF-8).</summary>
    public sealed class RosterEntryCodec : IStateCodec<RosterEntry>
    {
        /// <summary>이름의 UTF-8 최대 바이트 수(Steam 이름 32자를 담는 값).</summary>
        public const int MaxNameBytes = 96;

        public static RosterEntryCodec Instance { get; } = new RosterEntryCodec();

        public int KeyBytes => 2;
        public int MaxStateBytes => 8 + 1 + 1 + MaxNameBytes;

        public void Write(ByteWriter writer, in RosterEntry state)
        {
            writer.WriteU64(state.Player.Value);
            writer.WriteBool(state.IsHost);
            writer.WriteString(Utf8Text.Truncate(state.Name, MaxNameBytes), MaxNameBytes);
        }

        public bool TryRead(ByteReader reader, out RosterEntry state)
        {
            ulong id = reader.ReadU64();
            bool host = reader.ReadBool();
            string name = reader.ReadString(MaxNameBytes);
            state = new RosterEntry(new PlayerId(id), name, host);
            return !reader.Failed;
        }
    }

    /// <summary>UTF-8 길이 맞춤 도우미.</summary>
    public static class Utf8Text
    {
        /// <summary>UTF-8로 maxBytes를 넘지 않도록 글자 단위로 자른다(글자 중간에서 자르지 않는다).</summary>
        public static string Truncate(string text, int maxBytes)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            var encoding = new System.Text.UTF8Encoding(false);
            if (encoding.GetByteCount(text) <= maxBytes)
            {
                return text;
            }
            int used = 0;
            int end = 0;
            while (end < text.Length)
            {
                int length = char.IsHighSurrogate(text[end]) && end + 1 < text.Length ? 2 : 1;
                int bytes = encoding.GetByteCount(text.Substring(end, length));
                if (used + bytes > maxBytes)
                {
                    break;
                }
                used += bytes;
                end += length;
            }
            return text.Substring(0, end);
        }
    }

    /// <summary>
    /// 다른 참가자 한 명의 자세 보간 버퍼. 받은 자세를 시각순으로 몇 개 들고, 지연된 시각의 위치를 선형 보간으로 돌려준다.
    /// 보낸 쪽 시계(밀리초)와 받은 시각의 차이 중 가장 작은 값을 기준으로 삼아 지터를 줄인다.
    /// </summary>
    public sealed class RemoteAvatarTrack
    {
        private const int MaxSamples = 16;

        private struct Sample
        {
            public double Time;
            public AvatarPose Pose;
        }

        private readonly List<Sample> _samples = new List<Sample>(MaxSamples);
        private double _offset = double.MaxValue;
        private uint _firstSenderMs;
        private uint _lastSenderMs;
        private bool _hasLast;

        /// <summary>가장 최근에 받은 자세. 아직 없으면 false.</summary>
        public bool TryGetLatest(out AvatarPose pose)
        {
            if (_samples.Count == 0)
            {
                pose = default;
                return false;
            }
            pose = _samples[_samples.Count - 1].Pose;
            return true;
        }

        /// <summary>받은 자세를 넣는다.</summary>
        /// <param name="pose">호스트가 검사를 통과시켜 중계한 자세</param>
        /// <param name="receiveSeconds">받은 시각(로컬 실시간 초)</param>
        public void Add(in AvatarPose pose, double receiveSeconds)
        {
            // 보낸 쪽 시각은 약 49일마다 한 바퀴 돌므로, 이전 값과의 부호 있는 차이로 늘려 간다.
            if (!_hasLast)
            {
                _firstSenderMs = pose.TimeMs;
                _hasLast = true;
            }
            _lastSenderMs = pose.TimeMs;
            double sender = unchecked((int)(pose.TimeMs - _firstSenderMs)) / 1000.0;
            double offset = receiveSeconds - sender;
            if (offset < _offset)
            {
                _offset = offset;
            }
            double time = sender + _offset;
            if (_samples.Count > 0 && time <= _samples[_samples.Count - 1].Time)
            {
                return; // 순서가 뒤집힌 옛 자세는 버린다
            }
            if (_samples.Count == MaxSamples)
            {
                _samples.RemoveAt(0);
            }
            _samples.Add(new Sample { Time = time, Pose = pose });
        }

        /// <summary>
        /// renderSeconds 시각의 위치와 시선을 구한다. 앞뒤 두 자세를 선형 보간하고, 범위 밖이면 가장 가까운 자세를 쓴다.
        /// </summary>
        /// <returns>자세가 하나라도 있으면 true</returns>
        public bool TrySample(double renderSeconds, out Vec3f position, out float yawDegrees, out float pitchDegrees, out bool running)
        {
            position = Vec3f.Zero;
            yawDegrees = 0f;
            pitchDegrees = 0f;
            running = false;
            if (_samples.Count == 0)
            {
                return false;
            }

            Sample first = _samples[0];
            Sample last = _samples[_samples.Count - 1];
            if (renderSeconds <= first.Time || _samples.Count == 1)
            {
                Take(first.Pose, out position, out yawDegrees, out pitchDegrees, out running);
                return true;
            }
            if (renderSeconds >= last.Time)
            {
                Take(last.Pose, out position, out yawDegrees, out pitchDegrees, out running);
                return true;
            }

            for (int i = 1; i < _samples.Count; i++)
            {
                Sample b = _samples[i];
                if (renderSeconds > b.Time)
                {
                    continue;
                }
                Sample a = _samples[i - 1];
                float t = (float)((renderSeconds - a.Time) / (b.Time - a.Time));
                position = new Vec3f(
                    a.Pose.Position.X + (b.Pose.Position.X - a.Pose.Position.X) * t,
                    a.Pose.Position.Y + (b.Pose.Position.Y - a.Pose.Position.Y) * t,
                    a.Pose.Position.Z + (b.Pose.Position.Z - a.Pose.Position.Z) * t);
                yawDegrees = LerpAngle(a.Pose.YawDegrees, b.Pose.YawDegrees, t);
                pitchDegrees = a.Pose.PitchDegrees + (b.Pose.PitchDegrees - a.Pose.PitchDegrees) * t;
                running = b.Pose.Running;
                return true;
            }
            Take(last.Pose, out position, out yawDegrees, out pitchDegrees, out running);
            return true;
        }

        private static void Take(AvatarPose pose, out Vec3f position, out float yaw, out float pitch, out bool running)
        {
            position = pose.Position;
            yaw = pose.YawDegrees;
            pitch = pose.PitchDegrees;
            running = pose.Running;
        }

        /// <summary>360도 경계를 넘는 각도 보간(짧은 쪽으로).</summary>
        private static float LerpAngle(float a, float b, float t)
        {
            float delta = ((b - a) % 360f + 540f) % 360f - 180f;
            return (a + delta * t + 360f) % 360f;
        }
    }

    /// <summary>
    /// 클라이언트가 가진 월드 사본. 복제된 표, 접속자 명단, 다른 참가자 자세를 담는다.
    /// 화면(표현 어댑터)은 이것만 읽고, 쓰는 쪽은 Net 어댑터(복제 수신)뿐이다. [NFR-030]
    /// </summary>
    public sealed class ClientWorldMirror
    {
        private readonly Dictionary<ushort, IReplicatedTable> _tables = new Dictionary<ushort, IReplicatedTable>();
        private readonly Dictionary<PlayerId, RemoteAvatarTrack> _avatars = new Dictionary<PlayerId, RemoteAvatarTrack>();

        public ClientWorldMirror()
        {
            Roster = new ReplicatedTable<RosterEntry>(TableIds.Roster, RosterEntryCodec.Instance);
            RegisterTable(Roster);
            Resources = new ReplicatedTable<ResourceState>(TableIds.Resources, ResourceStateCodec.Instance);
            RegisterTable(Resources);
        }

        /// <summary>
        /// 자원 상태 표(ResourceIndex → 바뀐 상태). 호스트와 같은 월드를 로컬에서 만든 뒤 스냅샷·델타로 이 표만 맞춘다. [FR-009]
        /// 자원 화면(M1-10 2차)은 <see cref="Local"/>의 생성 표와 이 표를 함께 읽는다.
        /// </summary>
        public ReplicatedTable<ResourceState> Resources { get; }

        /// <summary>이 PC에서 만든 월드. 접속 절차에서 월드 생성이 끝나면 채워지고, 세션을 떠나면 비워진다.</summary>
        public LocalWorld Local { get; private set; }

        /// <summary>월드 생성이 끝났음을 알린다(접속 절차가 화면 스레드에서 부른다).</summary>
        public void AttachLocal(LocalWorld local) => Local = local;

        /// <summary>접속자 명단 표(자리 번호 → 항목).</summary>
        public ReplicatedTable<RosterEntry> Roster { get; }

        /// <summary>받은 스냅샷 조각 수(진행 표시용).</summary>
        public int SnapshotChunksReceived { get; private set; }

        /// <summary>스냅샷 조각 총 수(첫 조각에서 알 수 있는 표별 총합).</summary>
        public int SnapshotChunksExpected { get; private set; }

        /// <summary>적용 실패 횟수(진단).</summary>
        public int ApplyFailures { get; private set; }

        /// <summary>표를 등록한다. 같은 번호가 이미 있으면 바꾼다.</summary>
        public void RegisterTable(IReplicatedTable table)
        {
            _tables[table.TableId] = table ?? throw new ArgumentNullException(nameof(table));
        }

        public bool TryGetTable(ushort tableId, out IReplicatedTable table) => _tables.TryGetValue(tableId, out table);

        /// <summary>스냅샷 조각을 적용한다. 모르는 표 번호나 잘못된 본문이면 false.</summary>
        public bool ApplySnapshotChunk(in SnapshotChunk chunk)
        {
            if (!_tables.TryGetValue(chunk.TableId, out IReplicatedTable table))
            {
                ApplyFailures++;
                return false;
            }
            if (chunk.ChunkIndex == 0)
            {
                SnapshotChunksExpected += chunk.ChunkCount;
            }
            SnapshotChunksReceived++;
            bool ok = table.ApplyPayload(chunk.Payload);
            if (!ok)
            {
                ApplyFailures++;
            }
            return ok;
        }

        /// <summary>표 델타를 적용한다.</summary>
        public bool ApplyDelta(in TableDelta delta)
        {
            if (!_tables.TryGetValue(delta.TableId, out IReplicatedTable table))
            {
                ApplyFailures++;
                return false;
            }
            bool ok = table.ApplyPayload(delta.Payload);
            if (!ok)
            {
                ApplyFailures++;
            }
            return ok;
        }

        /// <summary>다른 참가자의 자세를 보간 버퍼에 넣는다.</summary>
        public void AddRemotePose(PlayerId player, in AvatarPose pose, double receiveSeconds)
        {
            if (!_avatars.TryGetValue(player, out RemoteAvatarTrack track))
            {
                track = new RemoteAvatarTrack();
                _avatars[player] = track;
            }
            track.Add(pose, receiveSeconds);
        }

        /// <summary>다른 참가자 자세 버퍼를 읽는다(표현 어댑터).</summary>
        public IReadOnlyDictionary<PlayerId, RemoteAvatarTrack> RemoteAvatars => _avatars;

        /// <summary>플레이어가 명단에서 빠졌을 때 자세 버퍼를 지운다.</summary>
        public void RemoveRemoteAvatar(PlayerId player) => _avatars.Remove(player);

        /// <summary>세션을 떠날 때 모두 비운다.</summary>
        public void Reset()
        {
            foreach (IReplicatedTable table in _tables.Values)
            {
                table.Clear();
            }
            _avatars.Clear();
            Local = null;
            SnapshotChunksReceived = 0;
            SnapshotChunksExpected = 0;
            ApplyFailures = 0;
        }
    }
}

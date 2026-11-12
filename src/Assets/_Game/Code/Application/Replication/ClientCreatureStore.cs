// =============================================================
// 파일: ClientCreatureStore.cs
// 역할: 클라이언트가 가진 생물 사본. Spawn/Despawn/Snapshot을 받아 생물마다 위치를 들고, 스냅샷 사이를 보간해 돌려준다.
// 레이어: application (Game.Application.Replication). 순수 C#. 화면(CreatureViews)은 읽기만 한다.
// 관련: FR-008, NFR-017, DES-082, DES-088 / SDD 3.11.9
//
// 보간: 새 스냅샷이 오면 「지금 보이는 위치」에서 「새 위치」로 직전 수신 간격만큼 움직인다. 근거리(30 Hz)는 약 33 ms,
//       원거리(2 Hz)는 약 0.5 s 늦게 보이지만 끊김이 없고, 시작점이 지금 보이는 위치라 점프하지 않는다.
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Protocol;
using Game.Domain.Common;

namespace Game.Application.Replication
{
    /// <summary>클라이언트가 가진 생물 한 마리.</summary>
    public sealed class ClientCreature
    {
        /// <summary>수신 간격 추정의 하한(초). 30 Hz 한 틱.</summary>
        public const double MinGapSeconds = 1d / 30d;

        /// <summary>수신 간격 추정의 상한(초). 스냅샷이 끊겨도 영원히 느리게 가지 않게 한다.</summary>
        public const double MaxGapSeconds = 1d;

        private const double GapSmoothing = 0.3;

        private Vec3f _from;
        private float _fromYaw;
        private Vec3f _to;
        private float _toYaw;
        private double _toTime;
        private double _gap = 0.1;

        public ushort Id { get; }
        public ushort DefId { get; }

        /// <summary>애니메이션/모드 바이트(서버의 CreatureMode 값).</summary>
        public byte Anim { get; private set; }

        /// <summary>체력 비율 0~100.</summary>
        public byte HealthPercent { get; private set; }

        /// <summary>받은 가장 최근 상태의 틱.</summary>
        public uint LastTick { get; private set; }

        /// <summary>가장 최근에 받은(보간 목표) 위치.</summary>
        public Vec3f LatestPosition => _to;

        /// <summary>가장 최근에 받은 방향(도).</summary>
        public float LatestYaw => _toYaw;

        public ClientCreature(ushort id, ushort defId, in CreatureState state, uint tick, double nowSeconds)
        {
            Id = id;
            DefId = defId;
            _to = state.Position;
            _toYaw = Quantize.U8ToAngle(state.Yaw);
            _from = _to;
            _fromYaw = _toYaw;
            _toTime = nowSeconds;
            Anim = state.Anim;
            HealthPercent = state.HealthPercent;
            LastTick = tick;
        }

        /// <summary>새 상태를 받았다. 현재 보이는 위치를 시작점으로 삼아 새 위치로 이어 간다.</summary>
        public void Apply(in CreatureState state, uint tick, double nowSeconds)
        {
            Sample(nowSeconds, out Vec3f shownPosition, out float shownYaw);
            double observed = nowSeconds - _toTime;
            if (observed > 0d)
            {
                double clamped = Math.Max(MinGapSeconds, Math.Min(MaxGapSeconds, observed));
                _gap += (clamped - _gap) * GapSmoothing;
            }
            _from = shownPosition;
            _fromYaw = shownYaw;
            _to = state.Position;
            _toYaw = Quantize.U8ToAngle(state.Yaw);
            _toTime = nowSeconds;
            Anim = state.Anim;
            HealthPercent = state.HealthPercent;
            LastTick = tick;
        }

        /// <summary>지금 시각에 보일 위치와 방향.</summary>
        public void Sample(double nowSeconds, out Vec3f position, out float yawDegrees)
        {
            double t = (nowSeconds - _toTime) / _gap;
            float f = (float)Math.Max(0d, Math.Min(1d, t));
            position = new Vec3f(
                _from.X + (_to.X - _from.X) * f,
                _from.Y + (_to.Y - _from.Y) * f,
                _from.Z + (_to.Z - _from.Z) * f);
            float delta = ((_toYaw - _fromYaw) % 360f + 540f) % 360f - 180f;
            yawDegrees = (_fromYaw + delta * f + 360f) % 360f;
        }
    }

    /// <summary>생물 사본 모음(id → 생물).</summary>
    public sealed class ClientCreatureStore
    {
        private readonly Dictionary<ushort, ClientCreature> _creatures = new Dictionary<ushort, ClientCreature>();

        /// <summary>지금 가진 생물 수.</summary>
        public int Count => _creatures.Count;

        /// <summary>읽기용 목록(화면이 돈다).</summary>
        public IReadOnlyDictionary<ushort, ClientCreature> All => _creatures;

        /// <summary>받은 Spawn 수(진단).</summary>
        public int SpawnsReceived { get; private set; }

        /// <summary>받은 Despawn 수(진단).</summary>
        public int DespawnsReceived { get; private set; }

        /// <summary>적용한 스냅샷 항목 수(진단).</summary>
        public int StatesReceived { get; private set; }

        public bool TryGet(ushort id, out ClientCreature creature) => _creatures.TryGetValue(id, out creature);

        /// <summary>생물이 관심 범위에 들어왔다. 같은 번호가 있으면 새로 바꾼다.</summary>
        public void ApplySpawn(in CreatureSpawn message, uint tick, double nowSeconds)
        {
            SpawnsReceived++;
            _creatures[message.State.Id] = new ClientCreature(message.State.Id, message.DefId, message.State, tick, nowSeconds);
        }

        /// <summary>생물이 범위를 벗어났다. 없는 번호는 무시한다.</summary>
        public void ApplyDespawn(ushort id)
        {
            DespawnsReceived++;
            _creatures.Remove(id);
        }

        /// <summary>스냅샷 묶음을 적용한다. 모르는 번호(Spawn이 아직 안 옴)와 옛 틱(순서가 뒤집힌 패킷)은 버린다.</summary>
        public void ApplySnapshot(in CreatureSnapshot message, double nowSeconds)
        {
            CreatureState[] list = message.Creatures;
            for (int i = 0; i < list.Length; i++)
            {
                if (!_creatures.TryGetValue(list[i].Id, out ClientCreature creature))
                {
                    continue;
                }
                // 부호 있는 차이로 비교해 틱 번호가 한 바퀴 돌아도 맞게 한다.
                if (unchecked((int)(message.Tick - creature.LastTick)) < 0)
                {
                    continue;
                }
                StatesReceived++;
                creature.Apply(list[i], message.Tick, nowSeconds);
            }
        }

        /// <summary>모두 비운다(세션을 떠날 때).</summary>
        public void Clear()
        {
            _creatures.Clear();
            SpawnsReceived = 0;
            DespawnsReceived = 0;
            StatesReceived = 0;
        }
    }
}

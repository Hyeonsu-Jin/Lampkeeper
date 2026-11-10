// =============================================================
// 파일: AiScheduler.cs
// 역할: 서버 틱 안의 생물 AI 일정. 판단 분산(10 Hz), AI LOD, 공격 판정, 조향 잡 예약·결과 반영.
// 레이어: application (Game.Application/Ai, 순수 C#)
// 관련: FR-079~082, NFR-011, NFR-017, DES-062 / SDD 3.11.10, 3.11.11, 3.12.3
//
// 한 틱의 순서(SDD 3.11.10):
//   ApplySteering()  지난번에 예약한 조향 잡을 끝내고 위치를 반영한다.
//   (명령 처리는 ServerWorld가 한다)
//   Decide()         판단 차례인 생물만 판단 -> 공격 판정 -> 다음 조향 입력을 채우고 예약한다.
// 조향 잡은 호스트가 렌더 프레임의 LateUpdate에서 ScheduleSteering()으로 예약하게 할 수도 있다
// (ScheduleAfterDecide = false). 시험·렌더가 없는 환경에서는 Decide 끝에서 바로 예약한다.
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.WorldSync;
using Game.Domain.Common;
using Game.Domain.Creatures;

namespace Game.Application.Ai
{
    /// <summary>AI가 보는 플레이어 한 명(번호와 위치).</summary>
    public readonly struct AiPlayerView
    {
        public PlayerId Id { get; }
        public Vec3f Position { get; }

        public AiPlayerView(PlayerId id, Vec3f position)
        {
            Id = id;
            Position = position;
        }
    }

    /// <summary>생물의 공격이 맞았다는 판정 결과. 피해 적용은 받는 쪽이 한다(M1에서는 기록만).</summary>
    public readonly struct CreatureAttack
    {
        public ushort CreatureId { get; }
        public PlayerId Target { get; }
        public float Damage { get; }
        public uint Tick { get; }

        public CreatureAttack(ushort creatureId, PlayerId target, float damage, uint tick)
        {
            CreatureId = creatureId;
            Target = target;
            Damage = damage;
            Tick = tick;
        }
    }

    /// <summary>생물 AI 일정.</summary>
    public sealed class AiScheduler
    {
        private readonly AiSettings _settings;
        private readonly IAiNavigationBackend _backend;
        private readonly AiLodPolicy _lod;
        private readonly AiAgentBuffer _buffer = new AiAgentBuffer();
        private readonly List<CreatureAttack> _attacks = new List<CreatureAttack>();
        private readonly int[] _lodCounts = new int[4];

        // 생물 순서와 같은 임시 표(틱마다 다시 채움). 할당을 늘리지 않으려고 필드로 둔다.
        private Creature[] _agentCreature = new Creature[0];
        private AiLod[] _creatureLod = new AiLod[0];

        private PlayerId[] _fieldPlayers = new PlayerId[0];
        private float[] _targetX = new float[0];
        private float[] _targetZ = new float[0];
        private int _fieldCount;
        private bool _prepared;

        public AiScheduler(AiSettings settings, IAiNavigationBackend backend, CreatureRegistry registry)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _lod = new AiLodPolicy(settings);
        }

        /// <summary>생물 목록.</summary>
        public CreatureRegistry Registry { get; }

        /// <summary>길 찾기 백엔드.</summary>
        public IAiNavigationBackend Backend => _backend;

        /// <summary>true면 Decide 끝에서 바로 조향 잡을 예약한다. 호스트가 LateUpdate에서 예약하려면 false로 둔다.</summary>
        public bool ScheduleAfterDecide { get; set; } = true;

        /// <summary>이번 틱에 맞은 공격 판정 목록(다음 Decide에서 비워진다).</summary>
        public IReadOnlyList<CreatureAttack> LastAttacks => _attacks;

        /// <summary>이번 틱에 판단한 생물 수.</summary>
        public int LastDecisionCount { get; private set; }

        /// <summary>이번 틱에 이동 입력이 들어간(이동 시간 > 0) 생물 수.</summary>
        public int LastMovingCount { get; private set; }

        /// <summary>이번 틱의 LOD 단계별 생물 수(죽은 생물 제외).</summary>
        public int GetLodCount(AiLod lod) => _lodCounts[(int)lod];

        /// <summary>현재 흐름장 수.</summary>
        public int FlowFieldCount => _fieldCount;

        /// <summary>월드 높이에서 보행 격자를 만들어 백엔드에 넘긴다(월드 로드 시 1회).</summary>
        /// <param name="world">호스트가 만든 월드</param>
        /// <param name="centerX">범위 중심 X(m)</param>
        /// <param name="centerZ">범위 중심 Z(m)</param>
        public void PrepareWalkGrid(LocalWorld world, float centerX, float centerZ)
        {
            AiNavSettings nav = _settings.Nav;
            WalkGridDesc desc = WalkGridBuilder.FromWorld(world, centerX, centerZ, _settings.TestGridHalfExtentMeters, nav.CellSizeMeters);
            _backend.BuildWalkGrid(desc);
        }

        /// <summary>지난번에 예약한 조향 잡을 끝내고 위치를 반영한다. 예약이 없으면 아무 일도 하지 않는다.</summary>
        public void ApplySteering()
        {
            if (!_backend.IsSteeringPending)
            {
                return;
            }
            _backend.CompleteSteering(_buffer);
            int n = _buffer.Count;
            for (int k = 0; k < n; k++)
            {
                Creature c = _agentCreature[k];
                if (c == null || c.IsDead)
                {
                    continue;
                }
                byte flags = _buffer.OutFlags[k];
                Vec3f p = c.Position;
                bool moved = (flags & AiAgentBuffer.FlagMoved) != 0;
                c.Position = new Vec3f(moved ? _buffer.OutX[k] : p.X, _buffer.OutY[k], moved ? _buffer.OutZ[k] : p.Z);
                if (moved && c.Mode != CreatureMode.Attack)
                {
                    c.YawDegrees = _buffer.OutYaw[k]; // 공격 중에는 대상을 바라본 채 둔다
                }
                if ((flags & AiAgentBuffer.FlagBlocked) != 0 && c.Mode == CreatureMode.Idle)
                {
                    c.HasWander = false; // 막힌 배회 목적지는 버리고 다음 판단에서 다시 고른다
                }
            }
        }

        /// <summary>
        /// 이 틱의 판단과 공격 판정을 하고, 다음 조향 입력을 채운다.
        /// </summary>
        /// <param name="tick">서버 틱 번호</param>
        /// <param name="deltaSeconds">틱 간격(초)</param>
        /// <param name="players">플레이어 목록(앞 playerCount개만 유효)</param>
        /// <param name="playerCount">플레이어 수</param>
        public void Decide(uint tick, float deltaSeconds, AiPlayerView[] players, int playerCount)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            _attacks.Clear();
            IReadOnlyList<Creature> list = Registry.Creatures;
            int total = list.Count;
            EnsureScratch(total);
            for (int i = 0; i < _lodCounts.Length; i++) _lodCounts[i] = 0;

            // 1) 각 생물의 가장 가까운 플레이어와 LOD, 그리고 판단 차례인 생물의 판단.
            int decisions = 0;
            int agents = 0;
            bool anyChasing = false;
            for (int i = 0; i < total; i++)
            {
                Creature c = list[i];
                if (c.IsDead)
                {
                    continue;
                }
                int nearest = -1;
                float bestSq = float.PositiveInfinity;
                Vec3f pos = c.Position;
                for (int p = 0; p < playerCount; p++)
                {
                    float dx = players[p].Position.X - pos.X;
                    float dz = players[p].Position.Z - pos.Z;
                    float sq = dx * dx + dz * dz;
                    if (sq < bestSq)
                    {
                        bestSq = sq;
                        nearest = p;
                    }
                }
                AiLod lod = _lod.Classify(bestSq);
                _lodCounts[(int)lod]++;
                _agentCreature[agents] = c;
                _creatureLod[agents] = lod;
                agents++;

                if (_lod.ShouldDecide(lod, tick, c.Id))
                {
                    PlayerSighting sight = nearest < 0
                        ? PlayerSighting.None
                        : new PlayerSighting(true, players[nearest].Id, players[nearest].Position, (float)Math.Sqrt(bestSq));
                    CreatureBrain.Decide(c, sight);
                    decisions++;
                }
                if (c.Mode == CreatureMode.Seek || c.Mode == CreatureMode.Attack)
                {
                    anyChasing = true;
                }
            }
            LastDecisionCount = decisions;

            // 2) 공격 판정(거리·각도·쿨다운). 판단 차례와 상관없이 매 틱 한다.
            for (int k = 0; k < agents; k++)
            {
                Creature c = _agentCreature[k];
                Vec3f targetPos = Vec3f.Zero;
                if (c.HasTarget)
                {
                    TryFindPlayer(players, playerCount, c.TargetPlayer, out targetPos);
                }
                if (CreatureBrain.ResolveAttack(c, targetPos, deltaSeconds))
                {
                    _attacks.Add(new CreatureAttack(c.Id, c.TargetPlayer, c.Definition.AttackDamage, tick));
                }
            }

            // 3) 흐름장 목표 갱신: 정해진 간격마다, 또는 쫓는 생물이 처음 생겼는데 흐름장이 없을 때.
            if (playerCount > 0 && anyChasing && (tick % (uint)Math.Max(1, _settings.FlowRebuildTicks) == 0 || _fieldCount == 0))
            {
                RefreshFlowTargets(players, playerCount);
            }

            // 4) 조향 입력 채우기.
            _buffer.EnsureCapacity(agents);
            _buffer.Count = agents;
            int moving = 0;
            for (int k = 0; k < agents; k++)
            {
                Creature c = _agentCreature[k];
                CreatureDefinition def = c.Definition;
                _buffer.PosX[k] = c.Position.X;
                _buffer.PosZ[k] = c.Position.Z;
                _buffer.Field[k] = -1;
                _buffer.Speed[k] = 0f;
                _buffer.StopDistance[k] = 0f;
                _buffer.TargetX[k] = c.Position.X;
                _buffer.TargetZ[k] = c.Position.Z;
                switch (c.Mode)
                {
                    case CreatureMode.Idle:
                        if (c.HasWander)
                        {
                            _buffer.Speed[k] = def.WalkSpeed;
                            _buffer.TargetX[k] = c.WanderX;
                            _buffer.TargetZ[k] = c.WanderZ;
                            _buffer.StopDistance[k] = _settings.WanderStopMeters;
                        }
                        break;
                    case CreatureMode.Seek:
                        if (c.HasTarget && TryFindPlayer(players, playerCount, c.TargetPlayer, out Vec3f tp))
                        {
                            _buffer.Speed[k] = def.RunSpeed;
                            _buffer.TargetX[k] = tp.X;
                            _buffer.TargetZ[k] = tp.Z;
                            _buffer.StopDistance[k] = def.AttackRange * _settings.SeekStopFactor;
                            _buffer.Field[k] = FieldOf(c.TargetPlayer);
                        }
                        break;
                }
                float move = _buffer.Speed[k] > 0f ? _lod.MoveSeconds(_creatureLod[k], tick, c.Id, deltaSeconds) : 0f;
                // 서 있거나 이번 틱에 쉬는 생물도 입력에는 넣는다(백엔드가 제자리 높이만 읽는다).
                _buffer.MoveSeconds[k] = move;
                if (move > 0f) moving++;
            }
            LastMovingCount = moving;

            if (ScheduleAfterDecide)
            {
                ScheduleSteering();
            }
        }

        /// <summary>
        /// 채워 둔 조향 입력으로 잡을 예약한다. 이미 예약되어 있거나 채운 입력이 없으면 false.
        /// ScheduleAfterDecide가 false일 때 호스트가 LateUpdate에서 부른다.
        /// </summary>
        public bool ScheduleSteering()
        {
            if (_backend.IsSteeringPending || !_backend.HasWalkGrid || _buffer.Count == 0)
            {
                return false;
            }
            _backend.ScheduleSteering(_buffer);
            return true;
        }

        private void RefreshFlowTargets(AiPlayerView[] players, int playerCount)
        {
            int n = Math.Min(playerCount, _settings.Nav.MaxFlowFields);
            if (_fieldPlayers.Length < n)
            {
                _fieldPlayers = new PlayerId[n];
                _targetX = new float[n];
                _targetZ = new float[n];
            }
            for (int i = 0; i < n; i++)
            {
                _fieldPlayers[i] = players[i].Id;
                _targetX[i] = players[i].Position.X;
                _targetZ[i] = players[i].Position.Z;
            }
            _fieldCount = n;
            _backend.SetFlowTargets(_targetX, _targetZ, n);
            _prepared = true;
        }

        private int FieldOf(PlayerId player)
        {
            if (!_prepared)
            {
                return -1;
            }
            for (int i = 0; i < _fieldCount; i++)
            {
                if (_fieldPlayers[i].Equals(player))
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool TryFindPlayer(AiPlayerView[] players, int count, PlayerId id, out Vec3f position)
        {
            for (int i = 0; i < count; i++)
            {
                if (players[i].Id.Equals(id))
                {
                    position = players[i].Position;
                    return true;
                }
            }
            position = Vec3f.Zero;
            return false;
        }

        private void EnsureScratch(int n)
        {
            if (_agentCreature.Length >= n)
            {
                return;
            }
            int size = Math.Max(n, _agentCreature.Length * 2);
            _agentCreature = new Creature[size];
            _creatureLod = new AiLod[size];
        }
    }
}

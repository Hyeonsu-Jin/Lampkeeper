// =============================================================
// 파일: BotBrain.cs
// 역할: 봇 클라이언트(-bot)의 행동 결정. 화면·입력 없이 「정해진 경로로 걷고 달리기 → 가까운 자원 타격 → 생물 쪽으로 이동」을
//       반복하는 규칙을 순수 C#으로 둔다. 실제 이동·자세 전송·타격 명령 전송은 Bootstrap의 BotClient가 한다.
// 레이어: application (Game.Application/Perf, 순수 C#)
// 관련: DES-113(봇 클라이언트), NFR-010~018, NFR-021 / SDD 3.7.4(-bot), 3.12.4 3항(M1 대체 측정: 호스트 1 + 봇 7)
//
// 같은 (시드, 봇 번호)이면 늘 같은 경로가 나온다(측정 재현성). 경로는 월드 중심(스폰) 둘레에서만 돈다:
// 시험용 생물이 중심 30~150 m 고리에 있어 모든 봇이 같은 구역에서 복제·타격 부하를 만든다(8인 동시 접속의 최악 조건).
// =============================================================
using System;

namespace Game.Application.Perf
{
    /// <summary>봇이 지금 하는 일.</summary>
    public enum BotPhase : byte
    {
        /// <summary>정해진 지점으로 걷기·달리기.</summary>
        Travel = 0,

        /// <summary>가까운 자원으로 가서 일정 간격으로 타격.</summary>
        Harvest = 1,

        /// <summary>가장 가까운 생물 쪽으로 이동(추적을 유발).</summary>
        Chase = 2,
    }

    /// <summary>봇이 보는 월드(자원·생물 위치). Bootstrap이 월드 사본으로 구현하고, 시험은 가짜로 구현한다.</summary>
    public interface IBotWorld
    {
        /// <summary>(x, z)에서 가까운 자원 번호를 가까운 순서로 ids에 채운다.</summary>
        /// <returns>채운 개수</returns>
        int FindNearestResources(float x, float z, int[] ids);

        /// <summary>자원 위치(수평). 모르면 false.</summary>
        bool TryGetResourcePosition(int index, out float x, out float z);

        /// <summary>maxMeters 안에서 가장 가까운 생물 위치. 없으면 false.</summary>
        bool TryFindNearestCreature(float x, float z, float maxMeters, out float creatureX, out float creatureZ);
    }

    /// <summary>한 프레임에 봇이 하려는 동작.</summary>
    public readonly struct BotAction
    {
        /// <summary>앞으로 이동하려는지.</summary>
        public bool Move { get; }

        /// <summary>달리려는지(스태미나가 있을 때만 실제로 달린다).</summary>
        public bool Run { get; }

        /// <summary>바라볼 방향(도, 0 = +Z 방향).</summary>
        public float YawDegrees { get; }

        /// <summary>타격 요청을 보낼 자원 번호. 보내지 않으면 -1.</summary>
        public int HitResourceIndex { get; }

        public BotAction(bool move, bool run, float yawDegrees, int hitResourceIndex)
        {
            Move = move;
            Run = run;
            YawDegrees = yawDegrees;
            HitResourceIndex = hitResourceIndex;
        }
    }

    /// <summary>봇 경로 계산용 순수 함수.</summary>
    public static class BotPlanner
    {
        /// <summary>경유 지점 최소 반경(m): 스폰 바로 옆만 돌지 않게.</summary>
        public const float WaypointMinRadiusMeters = 30f;

        /// <summary>경유 지점 최대 반경(m): 시험용 생물 고리(30~150 m) 안쪽·바깥쪽을 모두 지나가게.</summary>
        public const float WaypointMaxRadiusMeters = 180f;

        private const ulong MixMultiplierA = 0xBF58476D1CE4E5B9UL;
        private const ulong MixMultiplierB = 0x94D049BB133111EBUL;
        private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
        private const float Unit24Bits = 16777216f;

        /// <summary>64비트 섞기(SplitMix64 마무리 단계). 같은 입력이면 같은 출력.</summary>
        public static ulong Mix(ulong value)
        {
            unchecked
            {
                value += GoldenGamma;
                value = (value ^ (value >> 30)) * MixMultiplierA;
                value = (value ^ (value >> 27)) * MixMultiplierB;
                return value ^ (value >> 31);
            }
        }

        /// <summary>
        /// 봇의 index번째 경유 지점. 월드 중심 둘레 [최소, 최대] 반경 안에서 시드·봇 번호·순번으로 정해진다.
        /// </summary>
        /// <param name="seed">월드 시드</param>
        /// <param name="botNumber">봇 번호(개발용 플레이어 ID)</param>
        /// <param name="index">경유 지점 순번(0부터)</param>
        /// <param name="centerX">중심 x(m)</param>
        /// <param name="centerZ">중심 z(m)</param>
        public static void Waypoint(ulong seed, int botNumber, int index, float centerX, float centerZ, out float x, out float z)
        {
            ulong h;
            unchecked
            {
                h = Mix(seed ^ Mix(((ulong)(uint)botNumber << 32) | (uint)index));
            }
            float angle = (h & 0xFFFFFF) / Unit24Bits * 2f * (float)Math.PI;
            float t = ((h >> 24) & 0xFFFFFF) / Unit24Bits;
            float radius = WaypointMinRadiusMeters + (WaypointMaxRadiusMeters - WaypointMinRadiusMeters) * t;
            x = centerX + (float)Math.Sin(angle) * radius;
            z = centerZ + (float)Math.Cos(angle) * radius;
        }

        /// <summary>(dx, dz) 방향의 시선 각도(도). 0도 = +Z, 90도 = +X (이동 입력의 forward와 같은 규칙).</summary>
        public static float YawDegrees(float dx, float dz) => (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);

        /// <summary>
        /// 거리 순으로 정렬된 후보 목록(앞 count개)에 후보 하나를 끼워 넣는다. 목록이 가득 차면 가장 먼 것이 밀려난다.
        /// </summary>
        /// <returns>새 개수</returns>
        public static int InsertNearest(float[] distances, int[] ids, int count, float distance, int id)
        {
            int capacity = Math.Min(distances.Length, ids.Length);
            if (count >= capacity && distance >= distances[capacity - 1])
            {
                return count;
            }
            int at = Math.Min(count, capacity - 1);
            while (at > 0 && distances[at - 1] > distance)
            {
                if (at < capacity)
                {
                    distances[at] = distances[at - 1];
                    ids[at] = ids[at - 1];
                }
                at--;
            }
            distances[at] = distance;
            ids[at] = id;
            return Math.Min(count + 1, capacity);
        }
    }

    /// <summary>
    /// 봇의 행동 결정기. 프레임마다 <see cref="Tick"/>을 부르면 이번 프레임에 할 동작을 돌려준다.
    /// 이동·충돌·스태미나는 호출한 쪽이 처리한다(여기는 「어디로 갈지·무엇을 때릴지」만 정한다).
    /// </summary>
    public sealed class BotBrain
    {
        /// <summary>경유 지점에 닿았다고 보는 거리(m).</summary>
        public const float ArriveMeters = 3f;

        /// <summary>이동 단계 최대 시간(초). 지형에 막혀도 다음 단계로 넘어간다.</summary>
        public const float TravelTimeoutSeconds = 40f;

        /// <summary>이 거리보다 멀면 달린다(m).</summary>
        public const float RunDistanceMeters = 15f;

        /// <summary>타격 단계 길이(초).</summary>
        public const float HarvestSeconds = 10f;

        /// <summary>추적 단계 길이(초).</summary>
        public const float ChaseSeconds = 10f;

        /// <summary>생물을 찾는 최대 거리(m). 생물 복제 관심 범위(SDD 3.9.4)보다 넉넉히 잡는다.</summary>
        public const float ChaseRangeMeters = 150f;

        /// <summary>생물 가까이에서 멈추는 거리(m). 계속 겹치지 않게.</summary>
        public const float ChaseStopMeters = 4f;

        /// <summary>타격 요청 간격(초). 사람 입력(ResourceHitInput.RepeatIntervalSeconds)과 같다. 호스트의 초당 상한보다 느리다.</summary>
        public const float HitIntervalSeconds = 0.3f;

        /// <summary>타격하려고 자원 가까이 서는 거리 = 닿는 거리의 이 비율(호스트 허용 거리 = 닿는 거리 + 여유 안쪽).</summary>
        public const float HitStandRatio = 0.7f;

        /// <summary>후보 자원 수(가까운 순). 순번마다 다른 자원을 고르게 한다.</summary>
        public const int ResourceCandidates = 8;

        private readonly ulong _seed;
        private readonly int _botNumber;
        private readonly float _centerX;
        private readonly float _centerZ;
        private readonly float _standMeters;
        private readonly IBotWorld _world;
        private readonly int[] _candidates = new int[ResourceCandidates];
        private float _targetX;
        private float _targetZ;
        private int _targetResource = -1;
        private bool _hasTarget;
        private float _phaseSeconds;
        private float _hitCooldown;
        private float _yaw;
        private int _harvestRound;

        /// <param name="seed">월드 시드</param>
        /// <param name="botNumber">봇 번호(개발용 플레이어 ID)</param>
        /// <param name="centerX">경로 중심 x(m, 월드 중심)</param>
        /// <param name="centerZ">경로 중심 z(m)</param>
        /// <param name="reachMeters">닿는 거리(m, 호스트의 상호작용 거리)</param>
        /// <param name="world">월드 조회</param>
        /// <exception cref="ArgumentNullException">world가 null일 때</exception>
        public BotBrain(ulong seed, int botNumber, float centerX, float centerZ, float reachMeters, IBotWorld world)
        {
            _seed = seed;
            _botNumber = botNumber;
            _centerX = centerX;
            _centerZ = centerZ;
            _standMeters = reachMeters * HitStandRatio;
            _world = world ?? throw new ArgumentNullException(nameof(world));
            Enter(BotPhase.Travel);
        }

        /// <summary>지금 하는 일.</summary>
        public BotPhase Phase { get; private set; }

        /// <summary>지금까지 지나간 경유 지점 수(= 현재 순번).</summary>
        public int WaypointIndex { get; private set; }

        /// <summary>지금까지 보낸 타격 요청 수.</summary>
        public int HitsRequested { get; private set; }

        /// <summary>
        /// 한 프레임 처리.
        /// </summary>
        /// <param name="deltaSeconds">지난 시간(초)</param>
        /// <param name="x">봇의 현재 x(m)</param>
        /// <param name="z">봇의 현재 z(m)</param>
        /// <param name="canRun">스태미나가 있어 달릴 수 있는지</param>
        public BotAction Tick(float deltaSeconds, float x, float z, bool canRun)
        {
            _phaseSeconds += deltaSeconds;
            switch (Phase)
            {
                case BotPhase.Travel: return TickTravel(x, z, canRun);
                case BotPhase.Harvest: return TickHarvest(deltaSeconds, x, z, canRun);
                default: return TickChase(x, z, canRun);
            }
        }

        private BotAction TickTravel(float x, float z, bool canRun)
        {
            float dx = _targetX - x;
            float dz = _targetZ - z;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);
            if (distance <= ArriveMeters || _phaseSeconds > TravelTimeoutSeconds)
            {
                Enter(BotPhase.Harvest);
                return Idle();
            }
            return MoveToward(dx, dz, distance, canRun);
        }

        private BotAction TickHarvest(float deltaSeconds, float x, float z, bool canRun)
        {
            if (!_hasTarget && !ChooseResource(x, z))
            {
                Enter(BotPhase.Chase);
                return Idle();
            }
            if (_phaseSeconds > HarvestSeconds)
            {
                Enter(BotPhase.Chase);
                return Idle();
            }

            float dx = _targetX - x;
            float dz = _targetZ - z;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);
            if (distance > _standMeters)
            {
                return MoveToward(dx, dz, distance, canRun);
            }

            // 닿는 거리 안: 멈춰 서서 일정 간격으로 때린다. [FR-007] 호스트가 거리·상태를 다시 검사한다.
            _yaw = BotPlanner.YawDegrees(dx, dz);
            _hitCooldown -= deltaSeconds;
            int hit = -1;
            if (_hitCooldown <= 0f)
            {
                _hitCooldown = HitIntervalSeconds;
                hit = _targetResource;
                HitsRequested++;
            }
            return new BotAction(false, false, _yaw, hit);
        }

        private BotAction TickChase(float x, float z, bool canRun)
        {
            if (_phaseSeconds > ChaseSeconds || !_world.TryFindNearestCreature(x, z, ChaseRangeMeters, out float cx, out float cz))
            {
                // 생물이 없거나 시간이 다 됐다. 다음 경유 지점으로.
                WaypointIndex++;
                Enter(BotPhase.Travel);
                return Idle();
            }
            float dx = cx - x;
            float dz = cz - z;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);
            if (distance <= ChaseStopMeters)
            {
                _yaw = BotPlanner.YawDegrees(dx, dz);
                return new BotAction(false, false, _yaw, -1);
            }
            return new BotAction(true, canRun, BotPlanner.YawDegrees(dx, dz), -1); // 추적은 거리와 상관없이 달린다
        }

        private bool ChooseResource(float x, float z)
        {
            int found = _world.FindNearestResources(x, z, _candidates);
            if (found <= 0)
            {
                return false;
            }
            // 순번과 봇 번호로 후보를 돌려 가며 골라, 봇마다·회차마다 다른 자원을 때린다.
            int pick = (int)(((uint)_harvestRound + (uint)_botNumber) % (uint)found);
            _targetResource = _candidates[pick];
            if (!_world.TryGetResourcePosition(_targetResource, out _targetX, out _targetZ))
            {
                return false;
            }
            _hasTarget = true;
            return true;
        }

        private BotAction MoveToward(float dx, float dz, float distance, bool canRun)
        {
            _yaw = BotPlanner.YawDegrees(dx, dz);
            return new BotAction(true, canRun && distance > RunDistanceMeters, _yaw, -1);
        }

        private BotAction Idle() => new BotAction(false, false, _yaw, -1);

        private void Enter(BotPhase phase)
        {
            Phase = phase;
            _phaseSeconds = 0f;
            _hasTarget = false;
            _targetResource = -1;
            _hitCooldown = 0f;
            if (phase == BotPhase.Travel)
            {
                BotPlanner.Waypoint(_seed, _botNumber, WaypointIndex, _centerX, _centerZ, out _targetX, out _targetZ);
            }
            else if (phase == BotPhase.Harvest)
            {
                _harvestRound++;
            }
        }
    }
}

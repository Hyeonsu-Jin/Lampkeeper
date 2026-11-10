// =============================================================
// 파일: ServerWorld.cs
// 역할: 서버 시뮬레이션 루트(ServerWorld, 30 Hz 틱), 월드 상태(WorldState), 아바타 이동 검사(AvatarMoveValidator),
//       서버 설정 묶음(ServerSettings).
// 레이어: application (Game.Application/Server, 순수 C#)
// 관련: FR-007, FR-008, FR-023~025, NFR-030, NFR-032(서버 역할이 로컬 플레이어에 의존하지 않음), NFR-040, NFR-041,
//       DES-052 / SDD 3.3.2, 3.9.8, 3.11.10, 3.11.12, D-04, D-19, D-24
//
// 호스트 자신의 플레이어도 참가자와 같은 경로(게이트웨이 → 이 클래스)로 들어온다. 이 클래스는 「로컬 플레이어」를 모른다.
// WorldState를 바꾸는 코드는 이 어셈블리 안(internal)에만 있다. 참가자 쪽 코드는 직접 바꿀 수 없다. [NFR-030]
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Ai;
using Game.Application.Commands;
using Game.Application.Config;
using Game.Application.Gathering;
using Game.Application.Perf;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Application.Replication;
using Game.Application.WorldSync;
using Game.Domain.Common;
using Game.Domain.Creatures;
using Game.Domain.Gathering;
using Game.Domain.Players;
using Game.Domain.Vitals;

namespace Game.Application.Server
{
    /// <summary>서버 설정값 묶음. 설정 애셋(BalanceValues, NetworkTuning)에서 만든다.</summary>
    public sealed class ServerSettings
    {
        /// <summary>스폰 위치 높이(m). 지형이 생기면(M1-10) 지형 높이로 바뀐다.</summary>
        public const float SpawnHeightMeters = 0.1f;

        /// <summary>자리마다 스폰 위치를 벌리는 간격(m). 겹쳐 태어나지 않게.</summary>
        public const float SpawnSpacingMeters = 1.5f;

        public int MaxPlayers { get; set; } = 8;
        public int RequestsPerSecondLimit { get; set; } = 30;
        public int MaxCommandsPerTick { get; set; } = 64;
        public int MaxQueuedCommands { get; set; } = 256;
        public StaminaParams Stamina { get; set; } = new StaminaParams(100f, 10f, 1f, 20f);
        public AvatarMoveSettings Move { get; set; } = new AvatarMoveSettings(4f, 7f, 1.25f, 0.5f, 0.5f, 0.5f, 40f);

        /// <summary>설정 애셋 값으로 만든다.</summary>
        public static ServerSettings FromConfig(BalanceValues balance, NetworkTuning tuning)
        {
            if (balance == null)
            {
                throw new ArgumentNullException(nameof(balance));
            }
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }
            StaminaSettings s = balance.Stamina;
            MovementSettings m = balance.Movement;
            return new ServerSettings
            {
                MaxPlayers = balance.Session.MaxPlayers,
                RequestsPerSecondLimit = balance.Session.RequestsPerSecondLimit,
                MaxCommandsPerTick = tuning.MaxCommandsPerTick,
                MaxQueuedCommands = tuning.MaxQueuedCommands,
                Stamina = new StaminaParams(s.Max, s.RunCostPerSecond, s.RegenDelaySeconds, s.RegenPerSecond),
                Move = new AvatarMoveSettings(m.WalkSpeedMps, m.RunSpeedMps, m.MoveTolerance, m.MoveSlackMeters, m.MoveStaminaGraceSeconds, m.MaxPoseDeltaSeconds, m.MaxVerticalSpeedMps),
            };
        }

        /// <summary>자리 번호에 맞는 스폰 위치.</summary>
        public static Vec3f SpawnFor(int slot) => new Vec3f(slot * SpawnSpacingMeters, SpawnHeightMeters, 0f);
    }

    /// <summary>호스트 이동 검사 설정값.</summary>
    public readonly struct AvatarMoveSettings
    {
        public float WalkSpeed { get; }
        public float RunSpeed { get; }
        public float Tolerance { get; }
        public float SlackMeters { get; }
        public float StaminaGraceSeconds { get; }
        public float MaxDeltaSeconds { get; }
        public float MaxVerticalSpeed { get; }

        public AvatarMoveSettings(float walkSpeed, float runSpeed, float tolerance, float slackMeters, float staminaGraceSeconds, float maxDeltaSeconds, float maxVerticalSpeed)
        {
            WalkSpeed = walkSpeed;
            RunSpeed = runSpeed;
            Tolerance = tolerance;
            SlackMeters = slackMeters;
            StaminaGraceSeconds = staminaGraceSeconds;
            MaxDeltaSeconds = maxDeltaSeconds;
            MaxVerticalSpeed = maxVerticalSpeed;
        }
    }

    /// <summary>아바타 이동 검사 결과 종류.</summary>
    public enum AvatarMoveVerdict : byte
    {
        /// <summary>받아들임(중계 대상).</summary>
        Accepted = 0,
        /// <summary>속도·거리 한도 초과 또는 월드 밖(되돌림 보정 대상).</summary>
        Rejected = 1,
        /// <summary>옛 자세·중복(시각이 앞서지 않음). 조용히 버린다.</summary>
        Stale = 2,
    }

    /// <summary>플레이어(연결)별 이동 검사 상태. 마지막으로 받아들인 자세와 서버 쪽 스태미나 추정.</summary>
    public sealed class AvatarMoveState
    {
        public AvatarMoveState(StaminaParams stamina)
        {
            Stamina = new StaminaState(stamina);
        }

        /// <summary>플레이어 캐릭터가 가진 스태미나 상태를 그대로 쓴다(서버 쪽 추정값을 한 곳에서 관리).</summary>
        public AvatarMoveState(StaminaState stamina)
        {
            Stamina = stamina ?? throw new ArgumentNullException(nameof(stamina));
        }

        public bool HasLast { get; internal set; }
        public AvatarPose LastAccepted { get; internal set; }
        public StaminaState Stamina { get; }

        /// <summary>추정 스태미나가 0인 채로 흐른 시간(초). 0보다 크면 소진 상태.</summary>
        public float SecondsAtZero { get; internal set; }
    }

    /// <summary>
    /// 아바타 이동 검사. [D-04, D-24, SDD 3.11.12] 위치는 클라이언트가 계산하고 호스트는 속도·범위만 검사한다.
    /// 허용 거리 = 속도 × 허용 배율 × 시간 + 여유 거리. 서버 쪽 스태미나 추정이 0이면 걷기 속도가 상한이다(0.5초 여유 포함).
    /// 속도 한도 안의 위치 조작은 막지 못하며 Check 단계 보안 점검 대상이다(R-07).
    /// </summary>
    public sealed class AvatarMoveValidator
    {
        private readonly AvatarMoveSettings _settings;

        public AvatarMoveValidator(AvatarMoveSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// 새 자세를 검사한다. 받아들이면 상태(마지막 자세·스태미나 추정)를 갱신한다.
        /// </summary>
        /// <param name="state">이 플레이어의 검사 상태</param>
        /// <param name="pose">클라이언트가 보낸 자세</param>
        /// <returns>결과. 거부면 호스트가 마지막으로 받아들인 위치는 state.LastAccepted.Position</returns>
        public AvatarMoveVerdict Check(AvatarMoveState state, in AvatarPose pose)
        {
            // 월드 밖이거나 값이 이상하면 거부한다(NaN·무한대 포함).
            if (!pose.Position.IsFinite || !ProtocolConstants.IsInsideWorld(pose.Position.X, pose.Position.Z))
            {
                return AvatarMoveVerdict.Rejected;
            }

            // 첫 자세는 스폰 직후 기준점으로 받아들인다(시간 간격이 없음).
            if (!state.HasLast)
            {
                state.LastAccepted = pose;
                state.HasLast = true;
                return AvatarMoveVerdict.Accepted;
            }

            // 보낸 쪽 시각 차이(밀리초). 한 바퀴 돌아도 부호 있는 차이로 맞는다.
            int deltaMs = unchecked((int)(pose.TimeMs - state.LastAccepted.TimeMs));
            if (deltaMs <= 0)
            {
                return AvatarMoveVerdict.Stale;
            }
            float dt = Math.Min(deltaMs / 1000f, _settings.MaxDeltaSeconds);

            // [FR-024] 스태미나가 있어야(또는 막 소진된 직후 여유 안이어야) 달리기 속도를 인정한다.
            bool runAllowed = state.Stamina.CanRun || state.SecondsAtZero <= _settings.StaminaGraceSeconds;
            bool running = pose.Running && runAllowed;
            float speed = running ? _settings.RunSpeed : _settings.WalkSpeed;

            float horizontal = Vec3f.HorizontalDistance(state.LastAccepted.Position, pose.Position);
            float vertical = Math.Abs(pose.Position.Y - state.LastAccepted.Position.Y);
            float allowedHorizontal = speed * _settings.Tolerance * dt + _settings.SlackMeters;
            float allowedVertical = _settings.MaxVerticalSpeed * dt + _settings.SlackMeters;
            if (horizontal > allowedHorizontal || vertical > allowedVertical)
            {
                return AvatarMoveVerdict.Rejected;
            }

            // 받아들인 자세로 스태미나를 추정한다(달리기 플래그 기준). 0 근처에서는 소진 시간을 센다.
            state.Stamina.Tick(dt, pose.Running && state.Stamina.CanRun);
            state.SecondsAtZero = state.Stamina.Current <= 0f ? state.SecondsAtZero + dt : 0f;
            state.LastAccepted = pose;
            return AvatarMoveVerdict.Accepted;
        }
    }

    /// <summary>월드 상태. 읽기는 누구나, 바꾸기는 이 어셈블리 안에서만(NFR-030).</summary>
    public sealed class WorldState
    {
        private readonly Dictionary<PlayerId, PlayerCharacter> _players = new Dictionary<PlayerId, PlayerCharacter>();
        private readonly Dictionary<PlayerId, Vec3f> _lastPings = new Dictionary<PlayerId, Vec3f>();

        public WorldState()
        {
            Roster = new ReplicatedTable<RosterEntry>(TableIds.Roster, RosterEntryCodec.Instance);
            Resources = new ReplicatedTable<ResourceState>(TableIds.Resources, ResourceStateCodec.Instance);
        }

        /// <summary>접속자 명단 표(복제됨).</summary>
        public ReplicatedTable<RosterEntry> Roster { get; }

        /// <summary>
        /// 자원 상태 표(ResourceIndex → 바뀐 상태, 복제됨). [FR-009] 손대지 않은 자원은 넣지 않는다(바뀐 항목만).
        /// </summary>
        public ReplicatedTable<ResourceState> Resources { get; }

        /// <summary>호스트가 이 PC에서 만든 월드(자원 위치·종류를 읽는 표). 아직 없으면 null.</summary>
        public LocalWorld Local { get; private set; }

        /// <summary>접속 중인 플레이어 수.</summary>
        public int PlayerCount => _players.Count;

        public IEnumerable<PlayerCharacter> Players => _players.Values;

        public bool TryGetPlayer(PlayerId id, out PlayerCharacter player) => _players.TryGetValue(id, out player);

        /// <summary>마지막 핑 위치(시험·표시용).</summary>
        public bool TryGetLastPing(PlayerId id, out Vec3f position) => _lastPings.TryGetValue(id, out position);

        internal void AddPlayer(PlayerCharacter player) => _players[player.Id] = player;

        internal bool RemovePlayer(PlayerId id)
        {
            _lastPings.Remove(id);
            return _players.Remove(id);
        }

        internal void RecordPing(PlayerId id, Vec3f position) => _lastPings[id] = position;

        internal void AttachLocal(LocalWorld local) => Local = local;

        /// <summary>접속 중인 플레이어의 번호·위치를 buffer에 복사한다(AI 입력용, 할당 없음).</summary>
        /// <returns>복사한 수(buffer 길이를 넘으면 거기까지)</returns>
        internal int CopyPlayerViews(AiPlayerView[] buffer)
        {
            int n = 0;
            foreach (KeyValuePair<PlayerId, PlayerCharacter> pair in _players)
            {
                if (n >= buffer.Length)
                {
                    break;
                }
                buffer[n++] = new AiPlayerView(pair.Key, pair.Value.Position);
            }
            return n;
        }

        /// <summary>자원 상태를 바꾼다. 손대지 않은 상태로 돌아가면 표에서 지운다(바뀐 항목만 들고 있는다).</summary>
        internal void SetResource(ushort index, ResourceState state)
        {
            if (state.IsPristine)
            {
                Resources.Remove(index);
                return;
            }
            Resources.Set(index, state);
        }
    }

    /// <summary>한 틱 안에서 명령 처리가 끝났음을 알리는 결과(누가, 무슨 결과).</summary>
    public readonly struct CommandOutcome
    {
        public PlayerId Player { get; }
        public CommandResultMessage Result { get; }

        public CommandOutcome(PlayerId player, CommandResultMessage result)
        {
            Player = player;
            Result = result;
        }
    }

    /// <summary>서버 시뮬레이션 루트. 틱 순서: 명령 → 월드 시스템(M2~) → 복제.</summary>
    public sealed class ServerWorld
    {
        private readonly struct QueuedCommand
        {
            public readonly PlayerId Player;
            public readonly ICommand Command;

            public QueuedCommand(PlayerId player, ICommand command)
            {
                Player = player;
                Command = command;
            }
        }

        private readonly ServerSettings _settings;
        private readonly CommandDispatcher _dispatcher;
        private readonly AvatarMoveValidator _moveValidator;
        private readonly IPerfProbe _perf;
        private readonly IClock _clock;
        private readonly ILog _log;
        private readonly Queue<QueuedCommand> _queue = new Queue<QueuedCommand>();
        private readonly Dictionary<PlayerId, AvatarMoveState> _moveStates = new Dictionary<PlayerId, AvatarMoveState>();
        private readonly Dictionary<PlayerId, int> _slots = new Dictionary<PlayerId, int>();
        private readonly bool[] _slotUsed;
        private readonly List<IReplicatedTable> _tables = new List<IReplicatedTable>();
        private readonly AiPlayerView[] _aiPlayers;

        public ServerWorld(ServerSettings settings, CommandDispatcher dispatcher, IClock clock, IPerfProbe perf, ILog log)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _perf = perf ?? NullPerfProbe.Instance;
            _log = log ?? NullLog.Instance;
            _moveValidator = new AvatarMoveValidator(settings.Move);
            _slotUsed = new bool[Math.Max(1, settings.MaxPlayers)];
            _aiPlayers = new AiPlayerView[Math.Max(1, settings.MaxPlayers)];
            State = new WorldState();
            _tables.Add(State.Roster);
            _tables.Add(State.Resources);
        }

        public WorldState State { get; }

        /// <summary>생물 AI 일정(호스트 전용). 연결하지 않았으면 null. [DES-062]</summary>
        public AiScheduler Ai { get; private set; }

        /// <summary>
        /// 생물 AI를 연결한다. 틱마다 조향 결과 반영(명령 처리 앞)과 판단·공격 판정(명령 처리 뒤)이 돈다. 한 번만 부를 수 있다.
        /// </summary>
        public void AttachAi(AiScheduler scheduler)
        {
            if (Ai != null)
            {
                throw new InvalidOperationException("생물 AI는 이미 연결되어 있습니다.");
            }
            Ai = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        /// <summary>
        /// 시험용 생물을 시드 고정 배치로 만든다(M1-11 부하 시험, 호스트 전용). 월드가 붙어 있으면 보행 격자를 만들고 지면 위에 놓는다.
        /// 위치는 월드 중심(플레이어 스폰) 둘레 30~150 m 고리다.
        /// </summary>
        /// <param name="count">생물 수(시험은 60)</param>
        /// <param name="seed">배치 시드(같으면 같은 위치)</param>
        /// <returns>만든 수</returns>
        /// <exception cref="InvalidOperationException">AttachAi 전에 불렀을 때</exception>
        public int SpawnTestCreatures(int count, ulong seed)
        {
            if (Ai == null)
            {
                throw new InvalidOperationException("AttachAi를 먼저 부르세요.");
            }
            LocalWorld local = State.Local;
            float cx = local != null ? local.WidthMeters * 0.5f : 0f;
            float cz = local != null ? local.DepthMeters * 0.5f : 0f;
            if (local != null && !Ai.Backend.HasWalkGrid)
            {
                Ai.PrepareWalkGrid(local, cx, cz);
            }
            Func<float, float, bool> walkable = Ai.Backend.HasWalkGrid ? (Func<float, float, bool>)Ai.Backend.IsWalkable : null;
            (float X, float Z)[] spots = TestCreatureLayout.Generate(count, seed, cx, cz, walkable);
            for (int i = 0; i < spots.Length; i++)
            {
                float y = local != null ? local.SurfaceHeightMeters(spots[i].X, spots[i].Z) : 0f;
                // 생물마다 다른 배회 난수 시드(번호 기반이라 같은 시드면 같은 배회).
                uint rng = unchecked((uint)(seed ^ (seed >> 32)) + (uint)(i + 1) * 2654435761u);
                Ai.Registry.Spawn(CreatureDefinition.TestCreature, new Vec3f(spots[i].X, y, spots[i].Z), rng);
            }
            return spots.Length;
        }

        /// <summary>복제 대상 표 목록(복제 송신기가 읽는다).</summary>
        public IReadOnlyList<IReplicatedTable> Tables => _tables;

        /// <summary>가장 최근 틱 번호.</summary>
        public uint CurrentTick { get; private set; }

        /// <summary>명령 처리 결과(게이트웨이가 요청자에게 회신).</summary>
        public event Action<CommandOutcome> CommandCompleted;

        /// <summary>틱 끝(복제 직전). 틱 번호. 복제 송신기가 구독한다.</summary>
        public event Action<uint> TickEnded;

        /// <summary>플레이어가 들어왔음(자리 번호). 세션이 로비 인원 갱신 등에 쓴다.</summary>
        public event Action<PlayerId, int> PlayerAdded;

        /// <summary>플레이어가 나갔음.</summary>
        public event Action<PlayerId> PlayerRemoved;

        /// <summary>호스트가 만든 월드를 연결한다. 타격 검증이 자원 위치·종류를 여기서 읽는다.</summary>
        public void AttachWorld(LocalWorld local) => State.AttachLocal(local ?? throw new ArgumentNullException(nameof(local)));

        /// <summary>복제할 표를 더한다(건축물 등 이후 마일스톤의 표).</summary>
        public void AddTable(IReplicatedTable table) => _tables.Add(table ?? throw new ArgumentNullException(nameof(table)));

        /// <summary>
        /// 플레이어를 들인다. 가장 낮은 빈 자리를 주고, 명단 표에 올리고, 스폰 위치에 만든다.
        /// </summary>
        /// <returns>자리 번호. 자리가 없으면 -1</returns>
        public int AddPlayer(PlayerId id, string displayName, bool isHost)
        {
            if (_slots.TryGetValue(id, out int existing))
            {
                return existing;
            }
            int slot = Array.IndexOf(_slotUsed, false);
            if (slot < 0)
            {
                return -1;
            }
            _slotUsed[slot] = true;
            _slots[id] = slot;

            Vec3f spawn = SpawnPositionForSlot(slot);
            var character = new PlayerCharacter(id, displayName, spawn, _settings.Stamina);
            State.AddPlayer(character);
            _moveStates[id] = new AvatarMoveState(character.Stamina); // 서버 쪽 스태미나 추정은 캐릭터의 값 하나만 쓴다
            State.Roster.Set((uint)slot, new RosterEntry(id, displayName, isHost));
            PlayerAdded?.Invoke(id, slot);
            return slot;
        }

        /// <summary>플레이어를 내보낸다(퇴장 시 상태 보존은 M3, FR-011).</summary>
        public void RemovePlayer(PlayerId id)
        {
            if (!_slots.TryGetValue(id, out int slot))
            {
                return;
            }
            _slots.Remove(id);
            _slotUsed[slot] = false;
            _moveStates.Remove(id);
            State.RemovePlayer(id);
            State.Roster.Remove((uint)slot);
            _dispatcher.RemovePlayer(id);
            PlayerRemoved?.Invoke(id);
        }

        public bool HasPlayer(PlayerId id) => _slots.ContainsKey(id);

        /// <summary>플레이어의 자리 번호. 없으면 false.</summary>
        public bool TryGetSlot(PlayerId id, out int slot) => _slots.TryGetValue(id, out slot);

        /// <summary>스폰 위치(자리 기준).</summary>
        public Vec3f SpawnPositionOf(PlayerId id) => _slots.TryGetValue(id, out int slot) ? SpawnPositionForSlot(slot) : Vec3f.Zero;

        /// <summary>
        /// 자리 번호의 스폰 위치. 월드가 붙어 있으면 월드 한가운데에서 자리 간격만큼 떨어진 곳, 높이는 지면 + 여유 높이다
        /// (지형 위에서 땅에 박히거나 허공에 뜨지 않게, 월드 모서리에서 시작하지 않게). 월드가 없으면(시험) 평평한 기본 위치.
        /// </summary>
        public Vec3f SpawnPositionForSlot(int slot)
        {
            Vec3f flat = ServerSettings.SpawnFor(slot);
            LocalWorld local = State.Local;
            if (local == null)
            {
                return flat;
            }
            float x = local.WidthMeters * 0.5f + flat.X;
            float z = local.DepthMeters * 0.5f + flat.Z;
            return new Vec3f(x, local.SurfaceHeightMeters(x, z) + ServerSettings.SpawnHeightMeters, z);
        }

        /// <summary>
        /// 명령을 대기열에 넣는다. 대기열이 가득 차면 버리고 false(조작된 클라이언트의 폭주 방어). [NFR-041]
        /// </summary>
        public bool Enqueue(PlayerId player, ICommand command)
        {
            if (command == null || !_slots.ContainsKey(player))
            {
                return false;
            }
            if (_queue.Count >= _settings.MaxQueuedCommands)
            {
                _log.Warn("command", "대기열이 가득 차 명령을 버렸습니다. player=" + player);
                return false;
            }
            _queue.Enqueue(new QueuedCommand(player, command));
            return true;
        }

        /// <summary>
        /// 서버 틱 한 번. FishNet TimeManager 틱(30 Hz)에서 부른다.
        /// </summary>
        /// <param name="tick">틱 번호</param>
        /// <param name="deltaSeconds">틱 간격(초)</param>
        public void Tick(uint tick, float deltaSeconds)
        {
            CurrentTick = tick;
            _perf.Begin(PerfMarkers.ServerTotal);
            try
            {
                // [DES-062] 지난번 예약한 조향 잡 결과를 먼저 반영한다(SDD 3.11.10).
                if (Ai != null)
                {
                    _perf.Begin(PerfMarkers.ServerAiApply);
                    Ai.ApplySteering();
                    _perf.End(PerfMarkers.ServerAiApply);
                }

                _perf.Begin(PerfMarkers.ServerCommands);
                ProcessCommands(tick);
                _perf.End(PerfMarkers.ServerCommands);

                // 판단(10 Hz 분산) -> 공격 판정 -> 다음 조향 입력·예약.
                if (Ai != null)
                {
                    _perf.Begin(PerfMarkers.ServerAiDecide);
                    int players = State.CopyPlayerViews(_aiPlayers);
                    Ai.Decide(tick, deltaSeconds, _aiPlayers, players);
                    _perf.End(PerfMarkers.ServerAiDecide);
                    // [NFR-011] 활동 생물 수를 CSV 지표 이름으로 알린다(이전에는 이름이 달라 기록되지 않았음)
                    _perf.Count(PerfCounters.ActiveCreatures, Ai.Registry.Count);
                }

                // 월드 시스템(시각, 상태 값, 스폰 등)은 M2 이후에 이 자리에서 돈다.
                _perf.Begin(PerfMarkers.ServerSystems);
                _perf.End(PerfMarkers.ServerSystems);

                _perf.Begin(PerfMarkers.ServerReplication);
                try
                {
                    TickEnded?.Invoke(tick);
                }
                catch (Exception ex)
                {
                    // 복제 구독자의 결함이 서버 틱을 멈추지 않게 한다.
                    _log.Error("server", "TickEnded 처리 중 예외: " + ex);
                }
                _perf.End(PerfMarkers.ServerReplication);
            }
            finally
            {
                _perf.End(PerfMarkers.ServerTotal);
            }
        }

        /// <summary>
        /// 아바타 자세를 검사하고, 받아들이면 서버 쪽 플레이어 위치를 갱신한다(거리 검증 FR-007의 기준).
        /// </summary>
        /// <param name="player">자세를 보낸 플레이어</param>
        /// <param name="pose">보낸 자세</param>
        /// <param name="correction">거부했을 때 되돌릴 위치(호스트가 마지막으로 받아들인 위치)</param>
        public AvatarMoveVerdict CheckAvatarPose(PlayerId player, in AvatarPose pose, out Vec3f correction)
        {
            correction = Vec3f.Zero;
            if (!_moveStates.TryGetValue(player, out AvatarMoveState state) || !State.TryGetPlayer(player, out PlayerCharacter character))
            {
                return AvatarMoveVerdict.Stale;
            }

            AvatarMoveVerdict verdict = _moveValidator.Check(state, pose);
            switch (verdict)
            {
                case AvatarMoveVerdict.Accepted:
                    character.Position = pose.Position;
                    character.YawDegrees = pose.YawDegrees;
                    character.PitchDegrees = pose.PitchDegrees;
                    break;
                case AvatarMoveVerdict.Rejected:
                    // [NFR-030] 검사에 걸린 이동은 근거 자료로 로그에 남긴다(R-07, Check 단계 보안 점검).
                    correction = state.HasLast ? state.LastAccepted.Position : SpawnPositionForSlot(_slots[player]);
                    _log.Warn("move_rejected", "player=" + player + " from=" + correction + " to=" + pose.Position);
                    break;
            }
            return verdict;
        }

        private void ProcessCommands(uint tick)
        {
            int budget = _settings.MaxCommandsPerTick;
            double now = _clock.RealtimeSeconds;
            while (budget-- > 0 && _queue.Count > 0)
            {
                QueuedCommand item = _queue.Dequeue();
                if (!State.TryGetPlayer(item.Player, out _))
                {
                    continue; // 대기 중에 나간 플레이어
                }
                var context = new CommandContext(item.Player, State, tick);
                CommandResultMessage result = _dispatcher.Dispatch(context, item.Command, now);
                CommandCompleted?.Invoke(new CommandOutcome(item.Player, result));
            }
        }
    }
}

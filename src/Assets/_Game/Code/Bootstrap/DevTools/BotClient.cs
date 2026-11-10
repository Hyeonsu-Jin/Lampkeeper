// =============================================================
// 파일: BotClient.cs
// 역할: 봇 클라이언트(-bot)의 실행부. 입력·카메라·지형·자원 뷰 없이, BotBrain이 정한 동작대로 로컬 위치를 움직여
//       자세를 호스트에 보내고(걷기·달리기), 근처 자원에 타격 명령을 보낸다. 화면 없는 프로세스(-batchmode -nographics)에서 CPU를 아끼려고
//       World 씬을 불러오지 않는다.
// 레이어: infrastructure (Game.Bootstrap/DevTools, 개발 빌드 전용)
// 관련: DES-113(봇 클라이언트), FR-007, NFR-010~018, NFR-021 / SDD 3.7.4(-bot), 3.12.4 3항
//
// 이동 규칙은 FirstPersonController와 같다(걷기·달리기 속도, 스태미나 소모 → 0이면 걷기). 그래서 호스트의 이동 검사를 같은 조건으로 통과한다.
// 높이는 지면 높이 + 스폰 때 받은 오프셋으로 따라가고, 한 프레임 오르내림은 상한을 둔다(호스트 수직 속도 검사 MaxVerticalSpeed보다 작게).
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Config;
using Game.Application.Perf;
using Game.Application.Protocol;
using Game.Application.WorldSync;
using Game.Bootstrap.Runtime;
using Game.Domain.Common;
using Game.Domain.Vitals;
using UnityEngine;

namespace Game.Bootstrap.DevTools
{
    /// <summary>봇 클라이언트. 게임 흐름이 InGame에서 만들고 매 프레임 <see cref="Tick"/>을 부른다.</summary>
    public sealed class BotClient : IBotWorld
    {
        private const string LogTag = "[Bot] ";

        /// <summary>한 프레임으로 치는 시간 상한(초). 프레임이 튀어도 호스트 이동 검사(최대 간격)를 넘지 않게.</summary>
        private const float MaxDeltaSeconds = 0.1f;

        /// <summary>높이를 지면에 맞추는 최대 속도(m/s). 호스트 수직 속도 상한(40 m/s)의 절반.</summary>
        private const float MaxVerticalMetersPerSecond = 20f;

        /// <summary>월드 가장자리에서 떨어지는 거리(m).</summary>
        private const float WorldEdgeMarginMeters = 2f;

        /// <summary>생물 위치를 다시 찾는 간격(초). 사전 순회 비용과 할당을 줄인다.</summary>
        private const float CreatureSearchIntervalSeconds = 0.5f;

        /// <summary>상태 로그 간격(초).</summary>
        private const float StatusLogSeconds = 10f;

        private const float MillisecondsPerSecond = 1000f;

        private readonly SessionRuntime _runtime;
        private readonly LocalWorld _local;
        private readonly BotBrain _brain;
        private readonly StaminaState _stamina;
        private readonly float _walkSpeed;
        private readonly float _runSpeed;
        private readonly HashSet<int> _targeted = new HashSet<int>();
        private float _x;
        private float _z;
        private float _y;
        private float _heightOffset;
        private bool _hasPosition;
        private float _creatureSearchLeft;
        private bool _creatureFound;
        private float _creatureX;
        private float _creatureZ;
        private float _nextStatusLog;
        private int _hitsAccepted;

        /// <param name="runtime">세션 실행 구성(자세 버퍼·월드 사본·접속 세션)</param>
        /// <param name="balance">균형 값(속도·스태미나·닿는 거리)</param>
        /// <param name="botNumber">봇 번호(개발용 플레이어 ID). 경로를 정하는 입력</param>
        /// <exception cref="InvalidOperationException">월드 사본이 아직 없을 때</exception>
        public BotClient(SessionRuntime runtime, BalanceValues balance, int botNumber)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _local = runtime.Mirror.Local ?? throw new InvalidOperationException("월드 사본이 없어 봇을 시작할 수 없습니다.");
            _walkSpeed = balance.Movement.WalkSpeedMps;
            _runSpeed = balance.Movement.RunSpeedMps;
            StaminaSettings s = balance.Stamina;
            _stamina = new StaminaState(new StaminaParams(s.Max, s.RunCostPerSecond, s.RegenDelaySeconds, s.RegenPerSecond));
            _brain = new BotBrain(runtime.Session.World.Seed, botNumber, _local.WidthMeters * 0.5f, _local.DepthMeters * 0.5f,
                balance.Movement.InteractionDistanceMeters, this);
            Debug.Log(LogTag + "시작: 봇 번호=" + botNumber + " 시드=" + runtime.Session.World.Seed + " 자원=" + _local.ResourceCount);
        }

        /// <summary>지금 하는 일(로그·시험용).</summary>
        public BotPhase Phase => _brain.Phase;

        /// <summary>보낸 타격 요청 수.</summary>
        public int HitsRequested => _brain.HitsRequested;

        /// <summary>매 프레임 부른다.</summary>
        public void Tick(float deltaTime)
        {
            float dt = Mathf.Min(deltaTime, MaxDeltaSeconds);
            if (!TakeCorrection())
            {
                return; // 호스트가 정해 줄 스폰 위치를 기다린다
            }

            BotAction action = _brain.Tick(dt, _x, _z, _stamina.CanRun);

            // [FR-024] 달리기는 스태미나가 있을 때만. 0이면 걷기 속도로 이동한다.
            bool running = action.Run && action.Move && _stamina.CanRun;
            if (action.Move)
            {
                float speed = (running ? _runSpeed : _walkSpeed) * dt;
                float yawRad = action.YawDegrees * Mathf.Deg2Rad;
                _x = Mathf.Clamp(_x + Mathf.Sin(yawRad) * speed, WorldEdgeMarginMeters, _local.WidthMeters - WorldEdgeMarginMeters);
                _z = Mathf.Clamp(_z + Mathf.Cos(yawRad) * speed, WorldEdgeMarginMeters, _local.DepthMeters - WorldEdgeMarginMeters);
            }
            float targetY = _local.SurfaceHeightMeters(_x, _z) + _heightOffset;
            _y = Mathf.MoveTowards(_y, targetY, MaxVerticalMetersPerSecond * dt);
            _stamina.Tick(dt, running);

            uint timeMs = unchecked((uint)(long)(Time.realtimeSinceStartupAsDouble * MillisecondsPerSecond));
            _runtime.Pose.Write(new AvatarPose(new Vec3f(_x, _y, _z), action.YawDegrees, 0f, running, timeMs));

            if (action.HitResourceIndex >= 0 && action.HitResourceIndex <= ushort.MaxValue)
            {
                if (_runtime.Session.TrySendHitResource((ushort)action.HitResourceIndex, 0))
                {
                    _hitsAccepted++;
                }
            }
            LogStatusIfDue();
        }

        // ---------- IBotWorld ----------

        /// <inheritdoc/>
        public int FindNearestResources(float x, float z, int[] ids)
        {
            var distances = new float[ids.Length]; // 타격 단계에 들어갈 때만(10초에 한 번) 불리므로 할당을 허용한다
            int count = 0;
            int total = _local.ResourceCount;
            for (int i = 0; i < total; i++)
            {
                if (!_local.TryGetResource(i, out Game.Domain.Gathering.ResourceKind kind, out Vec3f p) || kind == Game.Domain.Gathering.ResourceKind.None || _targeted.Contains(i))
                {
                    continue;
                }
                float dx = p.X - x;
                float dz = p.Z - z;
                count = BotPlanner.InsertNearest(distances, ids, count, dx * dx + dz * dz, i);
            }
            return count;
        }

        /// <inheritdoc/>
        public bool TryGetResourcePosition(int index, out float x, out float z)
        {
            if (_local.TryGetResource(index, out _, out Vec3f p))
            {
                _targeted.Add(index); // 같은 자원을 반복해서 고르지 않는다(이미 쓰러졌을 수 있음)
                x = p.X;
                z = p.Z;
                return true;
            }
            x = 0f;
            z = 0f;
            return false;
        }

        /// <inheritdoc/>
        public bool TryFindNearestCreature(float x, float z, float maxMeters, out float creatureX, out float creatureZ)
        {
            _creatureSearchLeft -= Time.deltaTime;
            if (_creatureSearchLeft <= 0f)
            {
                _creatureSearchLeft = CreatureSearchIntervalSeconds;
                _creatureFound = false;
                float best = maxMeters * maxMeters;
                foreach (KeyValuePair<ushort, Game.Application.Replication.ClientCreature> pair in _runtime.Mirror.Creatures.All)
                {
                    Vec3f p = pair.Value.LatestPosition;
                    float dx = p.X - x;
                    float dz = p.Z - z;
                    float d = dx * dx + dz * dz;
                    if (d < best)
                    {
                        best = d;
                        _creatureX = p.X;
                        _creatureZ = p.Z;
                        _creatureFound = true;
                    }
                }
            }
            creatureX = _creatureX;
            creatureZ = _creatureZ;
            return _creatureFound;
        }

        // ---------- 내부 ----------

        /// <summary>호스트가 알려 준 위치(스폰·이동 거부 보정)를 가져온다. 아직 한 번도 못 받았으면 false.</summary>
        private bool TakeCorrection()
        {
            if (_runtime.Pose.TryTakeCorrection(out Vec3f corrected))
            {
                _x = corrected.X;
                _z = corrected.Z;
                _y = corrected.Y;
                // 스폰 높이는 지면보다 조금 위(0.1 m). 그 차이를 유지하며 지면을 따라간다.
                _heightOffset = corrected.Y - _local.SurfaceHeightMeters(_x, _z);
                _hasPosition = true;
            }
            return _hasPosition;
        }

        private void LogStatusIfDue()
        {
            if (Time.realtimeSinceStartup < _nextStatusLog)
            {
                return;
            }
            _nextStatusLog = Time.realtimeSinceStartup + StatusLogSeconds;
            Debug.Log(LogTag + "상태 단계=" + _brain.Phase + " 순번=" + _brain.WaypointIndex + " 위치=(" + _x.ToString("F0") + "," + _z.ToString("F0")
                      + ") 스태미나=" + _stamina.Current.ToString("F0") + " 타격요청=" + _brain.HitsRequested + " 전송=" + _hitsAccepted);
        }
    }
}

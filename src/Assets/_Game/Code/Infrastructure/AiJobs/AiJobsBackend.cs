// =============================================================
// 파일: AiJobsBackend.cs
// 역할: IAiNavigationBackend의 Burst 구현. 보행 격자 구성, 흐름장 예약, 공간 해시, 조향 잡 예약·완료.
// 레이어: infrastructure (Game.Infra.AiJobs)
// 관련: FR-067, NFR-011, NFR-017, DES-105 / SDD 3.11.10, 3.11.11, 3.12.3, D-07
//
// 호출 순서: BuildWalkGrid(1회) -> SetFlowTargets(1 Hz) -> ScheduleSteering -> (렌더링 동안 워커 스레드) -> CompleteSteering.
// 흐름장 갱신은 SetFlowTargets 뒤 첫 ScheduleSteering의 잡 사슬 맨 앞에 들어가므로 메인 스레드를 막지 않는다.
// 메인 스레드에서만 부른다. 사용 뒤에는 Dispose로 네이티브 메모리를 돌려준다.
// =============================================================
using System;
using System.Diagnostics;
using Game.Application.Ai;
using Unity.Collections;
using Unity.Jobs;

namespace Game.Infra.AiJobs
{
    /// <summary>AI 길 찾기·조향 백엔드(Burst 잡).</summary>
    public sealed class AiJobsBackend : IAiNavigationBackend
    {
        private const int HeapSlotsPerCell = 4;

        private readonly AiNavSettings _nav;
        private readonly int _window;
        private readonly AiSpatialHash _hash;

        private GridInfo _grid;
        private NativeArray<float> _heights;
        private NativeArray<byte> _walk;
        private byte[] _walkManaged;

        // 흐름장 저장소: 흐름장 f는 [f * window^2, (f + 1) * window^2) 구간을 쓴다.
        private NativeArray<int> _cost;
        private NativeArray<byte> _dirs;
        private NativeArray<long> _heap;
        private NativeArray<FieldInfo> _fields;
        private float[] _targetX;
        private float[] _targetZ;
        private int _targetCount;
        private bool _flowDirty;
        private bool _forceRebuildAll;

        // 생물 입력·출력(네이티브 사본)
        private int _agentCapacity;
        private NativeArray<float> _speed, _moveSeconds, _targetXn, _targetZn, _stop;
        private NativeArray<int> _fieldIndex;
        private NativeArray<float> _outX, _outY, _outZ, _outYaw;
        private NativeArray<byte> _outFlags;

        private JobHandle _handle;
        private int _pendingCount;
        private bool _cycleHadFlow;
        private readonly Stopwatch _cycleWatch = new Stopwatch();
        private bool _disposed;

        public AiJobsBackend(AiNavSettings nav)
        {
            _nav = nav ?? throw new ArgumentNullException(nameof(nav));
            if (nav.FlowRadiusCells < 1 || nav.MaxFlowFields < 1) throw new ArgumentOutOfRangeException(nameof(nav));
            _window = nav.FlowRadiusCells * 2 + 1;
            _hash = new AiSpatialHash(nav.HashCellMeters, nav.HashBuckets);
            _targetX = new float[nav.MaxFlowFields];
            _targetZ = new float[nav.MaxFlowFields];
            Stats = new AiNavStats();
        }

        public bool HasWalkGrid => _walk.IsCreated;

        public bool IsSteeringPending { get; private set; }

        public AiNavStats Stats { get; }

        /// <summary>공간 해시(분리 조향이 쓰는 것과 같은 것, 시험·진단용).</summary>
        public AiSpatialHash SpatialHash => _hash;

        public void BuildWalkGrid(WalkGridDesc desc)
        {
            if (desc == null) throw new ArgumentNullException(nameof(desc));
            ThrowIfDisposed();
            var watch = Stopwatch.StartNew();
            CompletePendingDiscard();
            DisposeGrid();

            int n = desc.CellsX * desc.CellsZ;
            _grid = new GridInfo
            {
                OriginX = desc.OriginX, OriginZ = desc.OriginZ, CellSize = desc.CellSize, InvCell = 1f / desc.CellSize,
                CellsX = desc.CellsX, CellsZ = desc.CellsZ,
            };
            _heights = new NativeArray<float>(desc.Heights, Allocator.Persistent);
            _walk = new NativeArray<byte>(n, Allocator.Persistent);
            var blocked = new NativeArray<byte>(desc.Blocked, Allocator.TempJob);
            try
            {
                var job = new WalkGridJob
                {
                    Heights = _heights, Blocked = blocked, Walk = _walk,
                    CellsX = desc.CellsX, CellsZ = desc.CellsZ, CellSize = desc.CellSize, MaxSlope = _nav.MaxSlope,
                };
                job.Schedule(n, 4096).Complete();
            }
            finally
            {
                blocked.Dispose();
            }
            _walkManaged = new byte[n];
            _walk.CopyTo(_walkManaged);

            int fieldCells = _window * _window * _nav.MaxFlowFields;
            _cost = new NativeArray<int>(fieldCells, Allocator.Persistent);
            _dirs = new NativeArray<byte>(fieldCells, Allocator.Persistent);
            _heap = new NativeArray<long>(fieldCells * HeapSlotsPerCell, Allocator.Persistent);
            _fields = new NativeArray<FieldInfo>(_nav.MaxFlowFields, Allocator.Persistent);
            _targetCount = 0;
            _flowDirty = false;
            Stats.FlowFieldCount = 0;
            watch.Stop();
            Stats.GridBuildMs = watch.Elapsed.TotalMilliseconds;
        }

        /// <summary>모든 흐름장을 다음 예약 때 다시 계산하게 한다(건축·정화 구역 변경으로 걸을 수 있는 칸이 바뀐 뒤, M2~).</summary>
        public void InvalidateFlowFields()
        {
            _forceRebuildAll = true;
            _flowDirty = true;
        }

        public bool IsWalkable(float x, float z)
        {
            if (!HasWalkGrid)
            {
                return false;
            }
            int cx = _grid.CellX(x);
            int cz = _grid.CellZ(z);
            return _grid.InBounds(cx, cz) && _walkManaged[_grid.Index(cx, cz)] != 0;
        }

        public void SetFlowTargets(float[] x, float[] z, int count)
        {
            if (x == null) throw new ArgumentNullException(nameof(x));
            if (z == null) throw new ArgumentNullException(nameof(z));
            int n = Math.Min(count, _nav.MaxFlowFields);
            for (int i = 0; i < n; i++)
            {
                _targetX[i] = x[i];
                _targetZ[i] = z[i];
            }
            _targetCount = n;
            _flowDirty = true;
        }

        public void ScheduleSteering(AiAgentBuffer agents)
        {
            if (agents == null) throw new ArgumentNullException(nameof(agents));
            ThrowIfDisposed();
            if (!HasWalkGrid)
            {
                throw new InvalidOperationException("보행 격자가 없습니다. BuildWalkGrid를 먼저 부르세요.");
            }
            CompletePendingDiscard();

            int n = agents.Count;
            EnsureAgentCapacity(n);
            _hash.EnsureCapacity(n);
            NativeArray<float>.Copy(agents.PosX, _hash.Px, n);
            NativeArray<float>.Copy(agents.PosZ, _hash.Pz, n);
            NativeArray<float>.Copy(agents.Speed, _speed, n);
            NativeArray<float>.Copy(agents.MoveSeconds, _moveSeconds, n);
            NativeArray<float>.Copy(agents.TargetX, _targetXn, n);
            NativeArray<float>.Copy(agents.TargetZ, _targetZn, n);
            NativeArray<float>.Copy(agents.StopDistance, _stop, n);
            NativeArray<int>.Copy(agents.Field, _fieldIndex, n);

            // 1) 흐름장(대상마다 1개, 서로 독립이라 워커 스레드에서 병렬).
            JobHandle flow = default;
            _cycleHadFlow = _flowDirty;
            if (_flowDirty)
            {
                int cells = _window * _window;
                for (int f = 0; f < _nav.MaxFlowFields; f++)
                {
                    var info = new FieldInfo();
                    if (f < _targetCount)
                    {
                        int tcx = _grid.CellX(_targetX[f]);
                        int tcz = _grid.CellZ(_targetZ[f]);
                        info.WinX0 = tcx - _nav.FlowRadiusCells;
                        info.WinZ0 = tcz - _nav.FlowRadiusCells;
                        FieldInfo prev = _fields[f];
                        if (!_forceRebuildAll && prev.Valid != 0 && prev.WinX0 == info.WinX0 && prev.WinZ0 == info.WinZ0)
                        {
                            continue; // 대상이 같은 칸에 있으면 기존 흐름장이 그대로 맞다(제자리 플레이어는 다시 계산하지 않음)
                        }
                        info.Valid = _grid.InBounds(tcx, tcz) ? 1 : 0;
                        if (info.Valid != 0)
                        {
                            var job = new FlowFieldJob
                            {
                                Walk = _walk, Grid = _grid, WinX0 = info.WinX0, WinZ0 = info.WinZ0, WinSize = _window,
                                TargetX = tcx, TargetZ = tcz, Offset = f * cells, HeapOffset = f * cells * HeapSlotsPerCell,
                                HeapCapacity = cells * HeapSlotsPerCell, Cost = _cost, Dirs = _dirs, Heap = _heap,
                            };
                            flow = JobHandle.CombineDependencies(flow, job.Schedule());
                        }
                    }
                    _fields[f] = info;
                }
                _flowDirty = false;
                _forceRebuildAll = false;
                Stats.FlowFieldCount = _targetCount;
            }

            // 2) 공간 해시(조향과 독립) 3) 조향
            JobHandle hash = _hash.Schedule(n, default);
            var steer = new SteeringJob
            {
                Px = _hash.Px, Pz = _hash.Pz, Speed = _speed, MoveSeconds = _moveSeconds, TargetX = _targetXn, TargetZ = _targetZn,
                Field = _fieldIndex, Stop = _stop, Walk = _walk, Heights = _heights, Fields = _fields, Dirs = _dirs,
                HashStart = _hash.Start, HashSorted = _hash.Sorted, Grid = _grid, FlowWindow = _window,
                MaxFields = _nav.MaxFlowFields, HashMask = _hash.Mask, HashInvCell = _hash.InvCell,
                SeparationRadius = _nav.SeparationRadiusMeters, SeparationWeight = _nav.SeparationWeight, ArriveSlow = _nav.ArriveSlowMeters,
                OutX = _outX, OutY = _outY, OutZ = _outZ, OutYaw = _outYaw, OutFlags = _outFlags,
            };
            _handle = steer.Schedule(n, 16, JobHandle.CombineDependencies(flow, hash));
            JobHandle.ScheduleBatchedJobs();
            _pendingCount = n;
            IsSteeringPending = true;
            _cycleWatch.Restart();
        }

        public void CompleteSteering(AiAgentBuffer agents)
        {
            if (agents == null) throw new ArgumentNullException(nameof(agents));
            if (!IsSteeringPending)
            {
                return;
            }
            var wait = Stopwatch.StartNew();
            _handle.Complete();
            wait.Stop();
            _cycleWatch.Stop();
            int n = _pendingCount;
            agents.EnsureCapacity(n);
            NativeArray<float>.Copy(_outX, agents.OutX, n);
            NativeArray<float>.Copy(_outY, agents.OutY, n);
            NativeArray<float>.Copy(_outZ, agents.OutZ, n);
            NativeArray<float>.Copy(_outYaw, agents.OutYaw, n);
            NativeArray<byte>.Copy(_outFlags, agents.OutFlags, n);
            IsSteeringPending = false;
            Stats.LastCompleteWaitMs = wait.Elapsed.TotalMilliseconds;
            Stats.LastCycleMs = _cycleWatch.Elapsed.TotalMilliseconds;
            Stats.LastCycleIncludedFlow = _cycleHadFlow;
            Stats.LastAgentCount = n;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (IsSteeringPending)
            {
                _handle.Complete();
                IsSteeringPending = false;
            }
            DisposeGrid();
            DisposeAgents();
            _hash.Dispose();
        }

        /// <summary>예약된 잡이 있으면 결과를 버리고 끝낸다(입력을 바꾸기 전에 안전하게 비우는 용도).</summary>
        private void CompletePendingDiscard()
        {
            if (IsSteeringPending)
            {
                _handle.Complete();
                IsSteeringPending = false;
            }
        }

        private void EnsureAgentCapacity(int n)
        {
            if (n <= _agentCapacity)
            {
                return;
            }
            DisposeAgents();
            int size = Math.Max(n, Math.Max(64, _agentCapacity * 2));
            _speed = new NativeArray<float>(size, Allocator.Persistent);
            _moveSeconds = new NativeArray<float>(size, Allocator.Persistent);
            _targetXn = new NativeArray<float>(size, Allocator.Persistent);
            _targetZn = new NativeArray<float>(size, Allocator.Persistent);
            _stop = new NativeArray<float>(size, Allocator.Persistent);
            _fieldIndex = new NativeArray<int>(size, Allocator.Persistent);
            _outX = new NativeArray<float>(size, Allocator.Persistent);
            _outY = new NativeArray<float>(size, Allocator.Persistent);
            _outZ = new NativeArray<float>(size, Allocator.Persistent);
            _outYaw = new NativeArray<float>(size, Allocator.Persistent);
            _outFlags = new NativeArray<byte>(size, Allocator.Persistent);
            _agentCapacity = size;
        }

        private void DisposeAgents()
        {
            if (_agentCapacity == 0)
            {
                return;
            }
            _speed.Dispose(); _moveSeconds.Dispose(); _targetXn.Dispose(); _targetZn.Dispose(); _stop.Dispose(); _fieldIndex.Dispose();
            _outX.Dispose(); _outY.Dispose(); _outZ.Dispose(); _outYaw.Dispose(); _outFlags.Dispose();
            _agentCapacity = 0;
        }

        private void DisposeGrid()
        {
            if (_walk.IsCreated) _walk.Dispose();
            if (_heights.IsCreated) _heights.Dispose();
            if (_cost.IsCreated) _cost.Dispose();
            if (_dirs.IsCreated) _dirs.Dispose();
            if (_heap.IsCreated) _heap.Dispose();
            if (_fields.IsCreated) _fields.Dispose();
            _walkManaged = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AiJobsBackend));
            }
        }
    }
}

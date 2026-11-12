// =============================================================
// 파일: AiJobsBackendTests.cs
// 역할: Burst AI 백엔드(보행 격자, 흐름장, 조향, 공간 해시) 시험과 60체 + 플레이어 8명 서버 처리 시간 관찰.
// 레이어: test (EditMode, Game.Tests.Infrastructure)
// 관련: FR-067, NFR-011, NFR-017, DES-062, DES-105 / SDD 3.11.11, 3.12.3
//
// 시간 관찰은 정식 측정이 아니다(정식 측정은 M1-12). 예산표 3.12.3 값과 로그로 비교만 한다. (틱 로그는 Console에 한 줄로 남는다)
// =============================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Application.Ai;
using Game.Application.Commands;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Application.Server;
using Game.Application.WorldSync;
using Game.Domain.Common;
using Game.Domain.Creatures;
using Game.Domain.Players;
using Game.Infra.AiJobs;
using NUnit.Framework;

namespace Game.Tests.Infrastructure
{
    public class AiJobsBackendTests
    {
        private const float CellMeters = 2f;
        private const int Cells = 60; // 120 m x 120 m

        private sealed class TestClock : IClock
        {
            public double RealtimeSeconds => 0d;
        }

        /// <summary>평평한(또는 높이 함수) 격자를 만든다. blocked(cx, cz)가 true인 칸은 막힌다.</summary>
        private static WalkGridDesc MakeGrid(Func<int, int, bool> blocked = null, Func<float, float, float> height = null)
        {
            var h = new float[Cells * Cells];
            var b = new byte[Cells * Cells];
            for (int z = 0; z < Cells; z++)
            {
                for (int x = 0; x < Cells; x++)
                {
                    float wx = (x + 0.5f) * CellMeters;
                    float wz = (z + 0.5f) * CellMeters;
                    h[z * Cells + x] = height != null ? height(wx, wz) : 0f;
                    b[z * Cells + x] = blocked != null && blocked(x, z) ? (byte)1 : (byte)0;
                }
            }
            return new WalkGridDesc(0f, 0f, CellMeters, Cells, Cells, h, b);
        }

        private static AiJobsBackend NewBackend(WalkGridDesc grid)
        {
            var backend = new AiJobsBackend(new AiNavSettings());
            backend.BuildWalkGrid(grid);
            return backend;
        }

        /// <summary>생물 한 마리를 한 걸음씩 걷게 한다. 흐름장을 쓰려면 field = 0.</summary>
        private static (float X, float Z, float MaxZ, bool EverBlocked) Walk(AiJobsBackend backend, float sx, float sz, float tx, float tz,
            int field, int steps, float speed = 4f, float stop = 0.5f)
        {
            var buf = new AiAgentBuffer();
            buf.EnsureCapacity(1);
            buf.Count = 1;
            float x = sx;
            float z = sz;
            float maxZ = sz;
            bool everBlocked = false;
            for (int s = 0; s < steps; s++)
            {
                buf.PosX[0] = x; buf.PosZ[0] = z; buf.Speed[0] = speed; buf.MoveSeconds[0] = 0.1f;
                buf.TargetX[0] = tx; buf.TargetZ[0] = tz; buf.Field[0] = field; buf.StopDistance[0] = stop;
                backend.ScheduleSteering(buf);
                backend.CompleteSteering(buf);
                x = buf.OutX[0];
                z = buf.OutZ[0];
                maxZ = Math.Max(maxZ, z);
                if (!backend.IsWalkable(x, z)) everBlocked = true;
            }
            return (x, z, maxZ, everBlocked);
        }

        // ----- 보행 격자 -----

        [Test]
        public void WalkGrid_SteepAndBlockedCellsAreNotWalkable_FlatAreWalkable()
        {
            // 오른쪽 절반은 칸마다 3 m씩 솟는 절벽(경사 1.5 > 0.8). 세로 한 줄은 건물로 막힘.
            WalkGridDesc grid = MakeGrid(blocked: (x, z) => x == 5, height: (wx, wz) => wx >= 60f ? (wx - 60f) * 1.5f + 3f : 0f);
            using (AiJobsBackend backend = NewBackend(grid))
            {
                Assert.IsTrue(backend.HasWalkGrid);
                Assert.IsTrue(backend.IsWalkable(30f, 30f));
                Assert.IsFalse(backend.IsWalkable(11f, 30f), "막힌 칸(건물)");
                Assert.IsFalse(backend.IsWalkable(100f, 30f), "가파른 경사");
                Assert.IsFalse(backend.IsWalkable(-1f, 30f), "격자 밖");
                Assert.IsFalse(backend.IsWalkable(30f, 500f), "격자 밖");
            }
        }

        [Test]
        public void Steering_NoGrid_Throws_ButIsWalkableIsFalse()
        {
            using (var backend = new AiJobsBackend(new AiNavSettings()))
            {
                Assert.IsFalse(backend.HasWalkGrid);
                Assert.IsFalse(backend.IsWalkable(1f, 1f));
                Assert.Throws<InvalidOperationException>(() => backend.ScheduleSteering(new AiAgentBuffer()));
            }
        }

        // ----- 흐름장 -----

        [Test]
        public void FlowField_GoesAroundWallToTarget_NeverEntersBlockedCells()
        {
            // 세로 벽(x 60~62 m)이 z 90 m까지 막고, 위쪽(z 90~120 m)만 열려 있다.
            WalkGridDesc grid = MakeGrid(blocked: (x, z) => x == 30 && z < 45);
            using (AiJobsBackend backend = NewBackend(grid))
            {
                backend.SetFlowTargets(new[] { 100f }, new[] { 20f }, 1);
                var r = Walk(backend, 40f, 20f, 100f, 20f, field: 0, steps: 900);
                float dist = (float)Math.Sqrt((r.X - 100f) * (r.X - 100f) + (r.Z - 20f) * (r.Z - 20f));
                Assert.Less(dist, 1.5f, "벽을 돌아 목표 근처에 닿는다. 마지막 위치=" + r.X + "," + r.Z);
                Assert.Greater(r.MaxZ, 88f, "벽의 열린 쪽(위)으로 돌아간다.");
                Assert.IsFalse(r.EverBlocked, "막힌 칸에 들어가지 않는다.");
                Assert.AreEqual(1, backend.Stats.FlowFieldCount);
            }
        }

        [Test]
        public void WithoutFlowField_StraightLineGetsStuckAtWall()
        {
            WalkGridDesc grid = MakeGrid(blocked: (x, z) => x == 30 && z < 45);
            using (AiJobsBackend backend = NewBackend(grid))
            {
                var r = Walk(backend, 40f, 20f, 100f, 20f, field: -1, steps: 600);
                Assert.Less(r.X, 60.5f, "흐름장이 없으면 벽 앞에서 막힌다(대조군).");
                Assert.IsFalse(r.EverBlocked);
            }
        }

        [Test]
        public void FlowField_TargetOutsideWalkableWorld_FallsBackToStraight_NoCrash()
        {
            using (AiJobsBackend backend = NewBackend(MakeGrid()))
            {
                backend.SetFlowTargets(new[] { 5000f }, new[] { 5000f }, 1); // 격자 밖 목표
                var r = Walk(backend, 20f, 20f, 100f, 20f, field: 0, steps: 50);
                Assert.Greater(r.X, 20f, "흐름장이 비어도 목표 방향으로 간다.");
            }
        }

        [Test]
        public void FlowField_TargetsBeyondMax_AreIgnored()
        {
            using (AiJobsBackend backend = NewBackend(MakeGrid()))
            {
                var xs = new float[12];
                var zs = new float[12];
                for (int i = 0; i < 12; i++) { xs[i] = 20f + i; zs[i] = 20f; }
                backend.SetFlowTargets(xs, zs, 12);
                Walk(backend, 20f, 20f, 100f, 20f, 0, 2);
                Assert.AreEqual(new AiNavSettings().MaxFlowFields, backend.Stats.FlowFieldCount);
            }
        }

        // ----- 조향 -----

        [Test]
        public void Steering_SeparationPushesCloseAgentsApart()
        {
            using (AiJobsBackend backend = NewBackend(MakeGrid()))
            {
                var buf = new AiAgentBuffer();
                buf.EnsureCapacity(2);
                buf.Count = 2;
                float[] x = { 50f, 50f };
                float[] z = { 20f, 20.4f };
                for (int s = 0; s < 20; s++)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        buf.PosX[i] = x[i]; buf.PosZ[i] = z[i]; buf.Speed[i] = 3f; buf.MoveSeconds[i] = 0.1f;
                        buf.TargetX[i] = 110f; buf.TargetZ[i] = 20.2f; buf.Field[i] = -1; buf.StopDistance[i] = 0.5f;
                    }
                    backend.ScheduleSteering(buf);
                    backend.CompleteSteering(buf);
                    for (int i = 0; i < 2; i++) { x[i] = buf.OutX[i]; z[i] = buf.OutZ[i]; }
                }
                float gap = (float)Math.Sqrt((x[0] - x[1]) * (x[0] - x[1]) + (z[0] - z[1]) * (z[0] - z[1]));
                Assert.Greater(gap, 1.0f, "처음 0.4 m였던 둘이 분리 조향으로 벌어진다.");
            }
        }

        [Test]
        public void Steering_OutputHeightFollowsHeightGrid_AndStandingAgentKeepsPosition()
        {
            WalkGridDesc grid = MakeGrid(height: (wx, wz) => 0.5f * wx); // 경사 0.5(걸을 수 있음)
            using (AiJobsBackend backend = NewBackend(grid))
            {
                var buf = new AiAgentBuffer();
                buf.EnsureCapacity(2);
                buf.Count = 2;
                buf.PosX[0] = 40f; buf.PosZ[0] = 30f; buf.Speed[0] = 4f; buf.MoveSeconds[0] = 0.5f; buf.TargetX[0] = 80f; buf.TargetZ[0] = 30f; buf.Field[0] = -1;
                buf.PosX[1] = 40f; buf.PosZ[1] = 60f; buf.Speed[1] = 0f; buf.MoveSeconds[1] = 0.5f; buf.TargetX[1] = 80f; buf.TargetZ[1] = 60f; buf.Field[1] = -1;
                backend.ScheduleSteering(buf);
                Assert.IsTrue(backend.IsSteeringPending);
                backend.CompleteSteering(buf);
                Assert.IsFalse(backend.IsSteeringPending);
                Assert.AreEqual(AiAgentBuffer.FlagMoved, buf.OutFlags[0] & AiAgentBuffer.FlagMoved);
                Assert.AreEqual(42f, buf.OutX[0], 0.05f, "속도 4 m/s x 0.5초 = 2 m");
                Assert.AreEqual(90f, buf.OutYaw[0], 0.5f, "+X 방향 = 90도");
                Assert.AreEqual(0.5f * buf.OutX[0], buf.OutY[0], 0.05f);
                Assert.AreEqual(0, buf.OutFlags[1]);
                Assert.AreEqual(40f, buf.OutX[1]);
                Assert.AreEqual(20f, buf.OutY[1], 0.05f);
            }
        }

        [Test]
        public void Steering_BlockedAhead_SetsBlockedFlag_AndKeepsPosition()
        {
            using (AiJobsBackend backend = NewBackend(MakeGrid(blocked: (x, z) => x >= 26 && x <= 28)))
            {
                var buf = new AiAgentBuffer();
                buf.EnsureCapacity(1);
                buf.Count = 1;
                buf.PosX[0] = 51f; buf.PosZ[0] = 30f; buf.Speed[0] = 4f; buf.MoveSeconds[0] = 1f; // 51 -> 55 (막힌 구간 52~58 m)
                buf.TargetX[0] = 100f; buf.TargetZ[0] = 30f; buf.Field[0] = -1;
                backend.ScheduleSteering(buf);
                backend.CompleteSteering(buf);
                Assert.AreEqual(AiAgentBuffer.FlagBlocked, buf.OutFlags[0] & AiAgentBuffer.FlagBlocked);
                Assert.AreEqual(51f, buf.OutX[0]);
            }
        }

        // ----- 공간 해시 -----

        [Test]
        public void SpatialHash_QueryMatchesBruteForce_EvenWithManyBucketCollisions()
        {
            using (var hash = new AiSpatialHash(4f, 16)) // 버킷 16개로 충돌을 많이 만든다
            {
                var rng = new System.Random(1234);
                const int n = 300;
                var x = new float[n];
                var z = new float[n];
                for (int i = 0; i < n; i++)
                {
                    x[i] = (float)rng.NextDouble() * 80f;
                    z[i] = (float)rng.NextDouble() * 80f;
                }
                hash.Build(x, z, n);
                Assert.AreEqual(n, hash.Count);
                var found = new List<int>();
                for (int q = 0; q < 50; q++)
                {
                    found.Clear();
                    hash.Query(x[q], z[q], 3.5f, q, found);
                    var expected = new List<int>();
                    for (int j = 0; j < n; j++)
                    {
                        if (j == q) continue;
                        float dx = x[q] - x[j];
                        float dz = z[q] - z[j];
                        if (dx * dx + dz * dz <= 3.5f * 3.5f) expected.Add(j);
                    }
                    found.Sort();
                    CollectionAssert.AreEqual(expected, found, "질의 " + q);
                }
            }
        }

        [Test]
        public void SpatialHash_RejectsBadArguments()
        {
            Assert.Throws<ArgumentException>(() => new AiSpatialHash(4f, 100)); // 2의 거듭제곱이 아님
            using (var hash = new AiSpatialHash(4f, 16))
            {
                hash.Build(new[] { 1f }, new[] { 1f }, 1);
                Assert.Throws<ArgumentOutOfRangeException>(() => hash.Query(1f, 1f, 5f, -1, new List<int>()));
            }
        }

        // ----- 서버 연결 + 처리 시간 관찰 -----

        [Test]
        public void Server_60Creatures_8Players_ObserveTickTime()
        {
            LocalWorld world = LocalWorld.Generate(20260101UL, LocalWorld.GeneratorV0, WorldScale.Small);
            var guard = new RequestGuard(30, new CommandLimits(ProtocolConstants.PositionHalfRangeMeters));
            var server = new ServerWorld(new ServerSettings(), new CommandDispatcher(guard, NullLog.Instance), new TestClock(), null, null);
            server.AttachWorld(world);
            var nav = new AiSettings();
            using (var backend = new AiJobsBackend(nav.Nav))
            {
                var ai = new AiScheduler(nav, backend, new CreatureRegistry());
                server.AttachAi(ai);
                int spawned = server.SpawnTestCreatures(60, 20260101UL);
                Assert.AreEqual(60, spawned);
                Assert.IsTrue(backend.HasWalkGrid);

                // 플레이어 8명을 월드 중심 둘레 60 m 고리에 흩어 놓는다(흐름장 8개가 모두 쓰이도록).
                float cx = world.WidthMeters * 0.5f;
                float cz = world.DepthMeters * 0.5f;
                for (int i = 0; i < 8; i++)
                {
                    var id = new PlayerId((ulong)(i + 1));
                    server.AddPlayer(id, "p" + i, i == 0);
                    Assert.IsTrue(server.State.TryGetPlayer(id, out PlayerCharacter pc));
                    float a = i * (float)Math.PI / 4f;
                    float px = cx + (float)Math.Sin(a) * 60f;
                    float pz = cz + (float)Math.Cos(a) * 60f;
                    pc.Position = new Vec3f(px, world.SurfaceHeightMeters(px, pz), pz);
                }

                const float dt = 1f / 30f;
                const int warmup = 10;
                const int ticks = 300; // 10초
                var ms = new List<double>(ticks);
                double firstTick;
                var sw = new Stopwatch();
                sw.Start();
                server.Tick(1, dt);
                sw.Stop();
                firstTick = sw.Elapsed.TotalMilliseconds;
                double flowCycleMs = 0d;
                var flowCycles = new List<double>();
                double steerCycleMs = 0d;
                int seekCount = 0;
                for (uint t = 2; t <= ticks; t++)
                {
                    sw.Restart();
                    server.Tick(t, dt);
                    sw.Stop();
                    if (t > warmup) ms.Add(sw.Elapsed.TotalMilliseconds);
                    if (backend.Stats.LastCycleIncludedFlow) { flowCycleMs = Math.Max(flowCycleMs, backend.Stats.LastCycleMs); flowCycles.Add(backend.Stats.LastCycleMs); }
                    else steerCycleMs = Math.Max(steerCycleMs, backend.Stats.LastCycleMs);
                }
                foreach (Creature c in ai.Registry.Creatures)
                {
                    if (c.Mode == CreatureMode.Seek || c.Mode == CreatureMode.Attack) seekCount++;
                }
                ms.Sort();
                double sum = 0d;
                foreach (double v in ms) sum += v;
                double avg = sum / ms.Count;
                double p99 = ms[(int)(ms.Count * 0.99)];
                double max = ms[ms.Count - 1];
                UnityEngine.Debug.Log(
                    "[AI 시간 관찰 M1-11] 생물 60 + 플레이어 8, 틱 " + ticks + "회(워밍업 " + warmup + " 제외)\n" +
                    "서버 틱 평균 " + avg.ToString("F3") + " ms / p99 " + p99.ToString("F3") + " ms / 최대 " + max.ToString("F3") +
                    " ms (예산표: Server.Total 틱 1회 <= 6.0 ms, AI 판단+반영+공격 판정 0.8 ms/프레임)\n" +
                    "첫 틱(Burst 준비 포함) " + firstTick.ToString("F2") + " ms, 보행 격자 구성 " + backend.Stats.GridBuildMs.ToString("F1") + " ms(" +
                    "한 번만, 월드 샘플링 제외)\n" +
                    "조향 주기(예약~완료, 즉시 완료) 최대: 흐름장 포함 " + flowCycleMs.ToString("F3") + " ms(첫 회 " + flowCycles[0].ToString("F3") + ", 마지막 " + flowCycles[flowCycles.Count - 1].ToString("F3") + ", " + flowCycles.Count + "회) / 흐름장 없음 " + steerCycleMs.ToString("F3") +
                    " ms (예산표 워커 <= 2.5 ms)\n" +
                    "추적·공격 중 생물 " + seekCount + "/60, 흐름장 " + backend.Stats.FlowFieldCount + "개");

                Assert.Greater(seekCount, 0, "플레이어 가까이 배치된 생물은 추적을 시작한다.");
                Assert.AreEqual(8, backend.Stats.FlowFieldCount);
            }
        }
    }
}

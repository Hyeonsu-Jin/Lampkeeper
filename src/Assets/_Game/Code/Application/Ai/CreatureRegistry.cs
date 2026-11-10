// =============================================================
// 파일: CreatureRegistry.cs
// 역할: 서버가 가진 생물 목록(번호 부여, 생성·제거 알림), 시험용 생물 배치(TestCreatureLayout), 보행 격자 구성(WalkGridBuilder).
// 레이어: application (Game.Application/Ai, 순수 C#)
// 관련: FR-077, FR-079, NFR-017(시험용 AI 60체), DES-019, DES-062, DES-082(복제는 M1-11 2차) / SDD 3.10.6, 3.11.8
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.WorldSync;
using Game.Domain.Common;
using Game.Domain.Creatures;

namespace Game.Application.Ai
{
    /// <summary>생물 목록. 번호는 1부터 올라가고(0은 비움) 한 번 쓴 번호는 다시 쓰지 않는다(ushort가 한 바퀴 돌 때까지).</summary>
    public sealed class CreatureRegistry
    {
        private readonly List<Creature> _creatures = new List<Creature>();
        private ushort _nextId = 1;

        /// <summary>생물 목록(생성 순서). 죽은 생물도 제거 전까지 남는다. 번호가 곧 복제 키다.</summary>
        public IReadOnlyList<Creature> Creatures => _creatures;

        public int Count => _creatures.Count;

        /// <summary>생물이 생겼을 때(복제 송신기가 CreatureSpawn을 만든다).</summary>
        public event Action<Creature> Spawned;

        /// <summary>생물이 제거되었을 때(번호).</summary>
        public event Action<ushort> Despawned;

        /// <summary>생물을 만든다.</summary>
        /// <exception cref="InvalidOperationException">번호가 모두 쓰였을 때</exception>
        public Creature Spawn(CreatureDefinition definition, Vec3f position, uint rngSeed)
        {
            if (_nextId == ushort.MaxValue)
            {
                throw new InvalidOperationException("생물 번호를 모두 썼습니다.");
            }
            var creature = new Creature(_nextId++, definition, position, rngSeed);
            _creatures.Add(creature);
            Spawned?.Invoke(creature);
            return creature;
        }

        /// <summary>번호로 찾는다.</summary>
        public bool TryGet(ushort id, out Creature creature)
        {
            for (int i = 0; i < _creatures.Count; i++)
            {
                if (_creatures[i].Id == id)
                {
                    creature = _creatures[i];
                    return true;
                }
            }
            creature = null;
            return false;
        }

        /// <summary>생물을 제거한다.</summary>
        public bool Remove(ushort id)
        {
            for (int i = 0; i < _creatures.Count; i++)
            {
                if (_creatures[i].Id == id)
                {
                    _creatures.RemoveAt(i);
                    Despawned?.Invoke(id);
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>시험용 생물 배치(시드 고정). 같은 시드·중심이면 항상 같은 위치가 나온다.</summary>
    public static class TestCreatureLayout
    {
        /// <summary>배치 반경 안쪽 한계(m). 플레이어 스폰 지점에 겹치지 않게 한다.</summary>
        public const float MinRadiusMeters = 30f;

        /// <summary>배치 반경 바깥쪽 한계(m).</summary>
        public const float MaxRadiusMeters = 150f;

        private const int MaxRetriesPerCreature = 8;

        /// <summary>
        /// 중심 둘레 고리 모양 영역에 count개의 위치를 만든다.
        /// </summary>
        /// <param name="count">생물 수</param>
        /// <param name="seed">시드(고정)</param>
        /// <param name="centerX">중심 X(m)</param>
        /// <param name="centerZ">중심 Z(m)</param>
        /// <param name="isWalkable">걸을 수 있는 곳인지 확인(없으면 확인 생략). 막힌 곳이면 다른 위치를 다시 뽑는다</param>
        /// <returns>위치(X, Z) 목록. 길이 count</returns>
        public static (float X, float Z)[] Generate(int count, ulong seed, float centerX, float centerZ, Func<float, float, bool> isWalkable)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var result = new (float X, float Z)[count];
            ulong state = seed;
            for (int i = 0; i < count; i++)
            {
                float x = centerX;
                float z = centerZ;
                for (int attempt = 0; attempt < MaxRetriesPerCreature; attempt++)
                {
                    float a = NextUnit(ref state) * (2f * (float)Math.PI);
                    float r = MinRadiusMeters + NextUnit(ref state) * (MaxRadiusMeters - MinRadiusMeters);
                    x = centerX + (float)Math.Sin(a) * r;
                    z = centerZ + (float)Math.Cos(a) * r;
                    if (isWalkable == null || isWalkable(x, z))
                    {
                        break;
                    }
                }
                result[i] = (x, z);
            }
            return result;
        }

        /// <summary>SplitMix64 한 걸음 -> 0 이상 1 미만.</summary>
        private static float NextUnit(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (z >> 40) * (1f / 16777216f);
        }
    }

    /// <summary>LocalWorld의 높이에서 보행 격자 입력을 만든다.</summary>
    public static class WalkGridBuilder
    {
        /// <summary>
        /// 월드의 한 사각형 범위를 칸 중심 높이로 샘플링한다. 범위는 월드 안으로 잘라 칸 경계에 맞춘다.
        /// </summary>
        /// <param name="world">호스트가 만든 월드</param>
        /// <param name="centerX">범위 중심 X(m)</param>
        /// <param name="centerZ">범위 중심 Z(m)</param>
        /// <param name="halfExtentMeters">중심에서 한 변의 절반(m)</param>
        /// <param name="cellSize">칸 크기(m)</param>
        public static WalkGridDesc FromWorld(LocalWorld world, float centerX, float centerZ, float halfExtentMeters, float cellSize)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            float minX = Math.Max(0f, centerX - halfExtentMeters);
            float minZ = Math.Max(0f, centerZ - halfExtentMeters);
            float maxX = Math.Min(world.WidthMeters, centerX + halfExtentMeters);
            float maxZ = Math.Min(world.DepthMeters, centerZ + halfExtentMeters);
            int cx = Math.Max(1, (int)Math.Ceiling((maxX - minX) / cellSize));
            int cz = Math.Max(1, (int)Math.Ceiling((maxZ - minZ) / cellSize));
            var heights = new float[cx * cz];
            var blocked = new byte[cx * cz];
            for (int z = 0; z < cz; z++)
            {
                float wz = minZ + (z + 0.5f) * cellSize;
                for (int x = 0; x < cx; x++)
                {
                    heights[z * cx + x] = world.SurfaceHeightMeters(minX + (x + 0.5f) * cellSize, wz);
                }
            }
            return new WalkGridDesc(minX, minZ, cellSize, cx, cz, heights, blocked);
        }
    }
}

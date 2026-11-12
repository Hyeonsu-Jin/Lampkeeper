// =============================================================
// 파일: ResourceCellGrid.cs
// 역할: 자원 약 2만 개를 위치 칸(셀)별로 묶어 두고, 「이 지점에서 반지름 R 안의 자원」을 칸 단위로 빠르게 찾아 준다.
//       화면(ResourceViewStreamer)이 근거리 오브젝트·원거리 인스턴싱 대상을 고를 때 2만 개를 매번 훑지 않게 하려는 것이다.
// 레이어: application (Game.Application/WorldSync, 순수 C#)
// 관련: FR-009, NFR-016, NFR-011, DES-087 / SDD 3.10.4, 3.12.7
//
// 월드 생성 표(LocalWorld)는 만들어진 뒤 바뀌지 않으므로 칸 묶음은 한 번만 만든다. 결과는 자원 번호(ResourceIndex)다.
// =============================================================
using System;
using System.Collections.Generic;
using Game.WorldGen.Model;

namespace Game.Application.WorldSync
{
    /// <summary>자원 위치 칸 묶음. 읽기 전용이라 만든 뒤에는 여러 곳에서 읽어도 된다.</summary>
    public sealed class ResourceCellGrid
    {
        /// <summary>기본 칸 크기(m). 근거리 반경(수십 m)보다 작아 칸을 훑는 낭비가 적다.</summary>
        public const float DefaultCellSizeMeters = 32f;

        private const float MillimetersPerMeter = 1000f;

        private readonly ResourceEntry[] _resources;
        private readonly float _cellSize;
        private readonly int[] _cellStart; // 칸 i의 항목은 _items[_cellStart[i] .. _cellStart[i+1])
        private readonly int[] _items;

        /// <summary>X 방향 칸 수.</summary>
        public int CellsX { get; }

        /// <summary>Z 방향 칸 수.</summary>
        public int CellsZ { get; }

        /// <summary>칸 크기(m).</summary>
        public float CellSizeMeters => _cellSize;

        /// <summary>묶은 자원 수.</summary>
        public int ResourceCount => _resources.Length;

        /// <exception cref="ArgumentNullException">world가 null일 때</exception>
        /// <exception cref="ArgumentOutOfRangeException">칸 크기가 0 이하일 때</exception>
        public ResourceCellGrid(LocalWorld world, float cellSizeMeters = DefaultCellSizeMeters)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (cellSizeMeters <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSizeMeters));

            _resources = world.Generated.Resources;
            _cellSize = cellSizeMeters;
            CellsX = Math.Max(1, (int)Math.Ceiling(world.WidthMeters / cellSizeMeters));
            CellsZ = Math.Max(1, (int)Math.Ceiling(world.DepthMeters / cellSizeMeters));

            // 세어 담기(counting sort): 칸별 개수 → 시작 위치 → 채우기. 칸 안에서는 자원 번호 오름차순이 유지된다.
            int cells = CellsX * CellsZ;
            _cellStart = new int[cells + 1];
            var cellOf = new int[_resources.Length];
            for (int i = 0; i < _resources.Length; i++)
            {
                int cell = CellIndexOf(_resources[i].PosXmm / MillimetersPerMeter, _resources[i].PosZmm / MillimetersPerMeter);
                cellOf[i] = cell;
                _cellStart[cell + 1]++;
            }
            for (int c = 0; c < cells; c++)
            {
                _cellStart[c + 1] += _cellStart[c];
            }
            _items = new int[_resources.Length];
            var fill = new int[cells];
            for (int i = 0; i < _resources.Length; i++)
            {
                int cell = cellOf[i];
                _items[_cellStart[cell] + fill[cell]++] = i;
            }
        }

        /// <summary>위치(m)가 속한 칸 번호. 월드 밖이면 가장자리 칸으로 맞춘다.</summary>
        public int CellIndexOf(float xMeters, float zMeters)
        {
            int cx = Clamp((int)Math.Floor(xMeters / _cellSize), CellsX);
            int cz = Clamp((int)Math.Floor(zMeters / _cellSize), CellsZ);
            return cz * CellsX + cx;
        }

        /// <summary>한 칸에 들어 있는 자원 수.</summary>
        public int CountInCell(int cell) => _cellStart[cell + 1] - _cellStart[cell];

        /// <summary>
        /// 중심 (x, z)에서 수평 거리 반지름 이하인 자원 번호를 result에 모은다(result는 먼저 비운다).
        /// 칸 단위로 훑고, 칸 안에서는 거리를 실제로 재서 반지름 밖은 뺀다.
        /// </summary>
        /// <returns>모은 개수</returns>
        public int CollectWithin(float centerX, float centerZ, float radiusMeters, List<int> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            if (radiusMeters < 0f)
            {
                return 0;
            }
            int minX = Clamp((int)Math.Floor((centerX - radiusMeters) / _cellSize), CellsX);
            int maxX = Clamp((int)Math.Floor((centerX + radiusMeters) / _cellSize), CellsX);
            int minZ = Clamp((int)Math.Floor((centerZ - radiusMeters) / _cellSize), CellsZ);
            int maxZ = Clamp((int)Math.Floor((centerZ + radiusMeters) / _cellSize), CellsZ);
            float radiusSq = radiusMeters * radiusMeters;

            for (int cz = minZ; cz <= maxZ; cz++)
            {
                for (int cx = minX; cx <= maxX; cx++)
                {
                    int cell = cz * CellsX + cx;
                    for (int k = _cellStart[cell]; k < _cellStart[cell + 1]; k++)
                    {
                        int index = _items[k];
                        float dx = _resources[index].PosXmm / MillimetersPerMeter - centerX;
                        float dz = _resources[index].PosZmm / MillimetersPerMeter - centerZ;
                        if (dx * dx + dz * dz <= radiusSq)
                        {
                            result.Add(index);
                        }
                    }
                }
            }
            return result.Count;
        }

        private static int Clamp(int value, int count) => value < 0 ? 0 : (value >= count ? count - 1 : value);
    }
}

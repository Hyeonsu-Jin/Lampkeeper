// =============================================================
// 파일: ResourceViewRulesTests.cs
// 역할: 자원 표시용 순수 규칙 시험 - 위치 칸 묶음(셀 계산), 상태 → 표시 변환, 지형 위 스폰 높이.
// 관련: FR-009, NFR-016, DES-087 / SDD 7장 M1-10 (2차)
// =============================================================
using System.Collections.Generic;
using Game.Application.WorldSync;
using Game.Domain.Gathering;
using NUnit.Framework;

namespace Game.Tests.Application
{
    public sealed class ResourceViewRulesTests
    {
        private const ulong Seed = 12345UL;
        private static LocalWorld _world;

        private static LocalWorld World => _world ?? (_world = LocalWorld.Generate(Seed, LocalWorld.GeneratorV0, WorldScale.Small));

        [Test]
        public void CellGrid_AllResourcesAreInExactlyOneCell()
        {
            var grid = new ResourceCellGrid(World);
            int total = 0;
            for (int c = 0; c < grid.CellsX * grid.CellsZ; c++)
            {
                total += grid.CountInCell(c);
            }
            Assert.AreEqual(World.ResourceCount, total);
        }

        [Test]
        public void CellGrid_CollectWithin_MatchesBruteForce()
        {
            var grid = new ResourceCellGrid(World);
            var found = new List<int>();
            float cx = World.WidthMeters * 0.5f;
            float cz = World.DepthMeters * 0.5f;
            const float radius = 90f;
            grid.CollectWithin(cx, cz, radius, found);

            var expected = new HashSet<int>();
            for (int i = 0; i < World.ResourceCount; i++)
            {
                World.TryGetResource(i, out _, out var p);
                float dx = p.X - cx;
                float dz = p.Z - cz;
                if (dx * dx + dz * dz <= radius * radius) expected.Add(i);
            }
            Assert.Greater(expected.Count, 0);
            Assert.AreEqual(expected.Count, found.Count);
            foreach (int i in found) Assert.IsTrue(expected.Contains(i));
        }

        [Test]
        public void CellGrid_CellIndex_ClampsOutsideWorld()
        {
            var grid = new ResourceCellGrid(World);
            Assert.AreEqual(0, grid.CellIndexOf(-50f, -50f));
            Assert.AreEqual(grid.CellsX * grid.CellsZ - 1, grid.CellIndexOf(1e6f, 1e6f));
        }

        [Test]
        public void Visual_PristineAndDamaged_AreStanding()
        {
            ResourceVisual a = ResourceVisualRules.Compute(ResourceKind.Tree, ResourceState.Pristine);
            Assert.IsTrue(a.Visible);
            Assert.AreEqual(0f, a.FallDegrees);
            ResourceVisual b = ResourceVisualRules.Compute(ResourceKind.Stone, new ResourceState(2, ResourcePhase.Damaged, 0));
            Assert.IsTrue(b.Visible);
            Assert.Less(b.ScaleFactor, 1f);
        }

        [Test]
        public void Visual_FelledTree_LiesInGivenDirection()
        {
            ResourceVisual v = ResourceVisualRules.Compute(ResourceKind.Tree, new ResourceState(5, ResourcePhase.Felled, 4));
            Assert.IsTrue(v.Visible);
            Assert.AreEqual(ResourceVisualRules.FelledDegrees, v.FallDegrees);
            Assert.AreEqual(90f, v.FallYawDegrees, 0.001f); // 방향 4 = 16등분 중 4번째 = 90도
        }

        [Test]
        public void Visual_BrokenHarvestedOrFelledStone_AreHidden()
        {
            Assert.IsFalse(ResourceVisualRules.Compute(ResourceKind.Stone, new ResourceState(4, ResourcePhase.Broken, 0)).Visible);
            Assert.IsFalse(ResourceVisualRules.Compute(ResourceKind.Tree, new ResourceState(0, ResourcePhase.Harvested, 0)).Visible);
            Assert.IsFalse(ResourceVisualRules.Compute(ResourceKind.Stone, new ResourceState(4, ResourcePhase.Felled, 1)).Visible);
        }

        [Test]
        public void Surface_HeightIsFinite_AndMatchesGrid()
        {
            float x = World.WidthMeters * 0.5f;
            float z = World.DepthMeters * 0.5f;
            float h = World.SurfaceHeightMeters(x, z);
            Assert.AreEqual(World.Generated.HeightAt((int)x, (int)z) / 1000f, h, 0.001f);
        }
    }
}

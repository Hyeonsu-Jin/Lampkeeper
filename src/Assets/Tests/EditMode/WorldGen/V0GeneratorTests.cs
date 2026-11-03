// =============================================================
// 파일: V0GeneratorTests.cs
// 역할: 시험용 생성기 V0와 요약값 계산기의 결정론·규칙 시험, 골든 해시 시험.
//       같은 PC 반복 생성 일치, 병렬 수와 무관한 결과, 시드·시도에 따라 달라짐, 불변 규칙(자원 정렬·간격 등)을 본다.
// 레이어: test (EditMode, Game.Tests.WorldGen)
// 관련: NFR-053(선행), FR-169(선행), FR-168, NFR-052, DES-043, DES-046, DES-047 / SDD 3.11, M1-04
// =============================================================
using System;
using System.Diagnostics;
using Game.WorldGen.Common;
using Game.WorldGen.Hashing;
using Game.WorldGen.Model;
using Game.WorldGen.V0;
using NUnit.Framework;

namespace Game.Tests.WorldGen
{
    public class V0GeneratorTests
    {
        private static ulong Seed(string text) => SeedCodec.SeedFromText(text);

        // ---------- 같은 PC 반복 ----------

        [Test]
        public void SameSeed_GeneratedTwice_GivesIdenticalWorldAndHash()
        {
            GeneratedWorld a = V0WorldGenerator.Generate(Seed("repeat-test"), V0Profile.Small);
            GeneratedWorld b = V0WorldGenerator.Generate(Seed("repeat-test"), V0Profile.Small);

            CollectionAssert.AreEqual(a.Height, b.Height);
            Assert.AreEqual(a.Resources.Length, b.Resources.Length);
            Assert.AreEqual(WorldHasher.Compute(a).Total, WorldHasher.Compute(b).Total);
        }

        [Test]
        public void ParallelDegree_DoesNotChangeResult()
        {
            ulong seed = Seed("parallel-test");
            ulong single = WorldHasher.Compute(V0WorldGenerator.Generate(seed, V0Profile.Small, 0, 1)).Total;
            ulong two = WorldHasher.Compute(V0WorldGenerator.Generate(seed, V0Profile.Small, 0, 2)).Total;
            ulong many = WorldHasher.Compute(V0WorldGenerator.Generate(seed, V0Profile.Small, 0, 16)).Total;
            ulong auto = WorldHasher.Compute(V0WorldGenerator.Generate(seed, V0Profile.Small, 0, -1)).Total;
            Assert.AreEqual(single, two);
            Assert.AreEqual(single, many);
            Assert.AreEqual(single, auto);
        }

        [Test]
        public void DifferentSeedsAndAttempts_GiveDifferentWorlds()
        {
            ulong h1 = WorldHasher.Compute(V0WorldGenerator.Generate(Seed("seed-A"), V0Profile.Small)).Total;
            ulong h2 = WorldHasher.Compute(V0WorldGenerator.Generate(Seed("seed-B"), V0Profile.Small)).Total;
            ulong h1Retry = WorldHasher.Compute(V0WorldGenerator.Generate(Seed("seed-A"), V0Profile.Small, 1)).Total;
            Assert.AreNotEqual(h1, h2);
            Assert.AreNotEqual(h1, h1Retry);
        }

        // ---------- 규칙 ----------

        [Test]
        public void Erosion_ConservesTotalHeight()
        {
            // 침식은 이웃끼리 같은 양을 주고받으므로 높이 합계가 변하지 않는다.
            var noErosion = new V0Profile(1024, 256, 16, 32, 0, 1500, 1, 35553, 150, 8);
            var withErosion = new V0Profile(1024, 256, 16, 32, 3, 1500, 1, 35553, 150, 8);
            ulong seed = Seed("erosion-test");
            long before = SumHeights(V0WorldGenerator.Generate(seed, noErosion));
            long after = SumHeights(V0WorldGenerator.Generate(seed, withErosion));
            Assert.AreEqual(before, after);
        }

        [Test]
        public void Erosion_ReducesRoughness()
        {
            var noErosion = new V0Profile(1024, 256, 16, 32, 0, 100, 1, 35553, 150, 8);
            var withErosion = new V0Profile(1024, 256, 16, 32, 3, 100, 1, 35553, 150, 8);
            ulong seed = Seed("erosion-test");
            Assert.Less(MaxStep(V0WorldGenerator.Generate(seed, withErosion)), MaxStep(V0WorldGenerator.Generate(seed, noErosion)));
        }

        [Test]
        public void Resources_AreSortedByUniqueId_InsideWorld_WithValidTypes()
        {
            GeneratedWorld world = V0WorldGenerator.Generate(Seed("resource-rules"), V0Profile.Small);
            Assert.Greater(world.Resources.Length, 0);

            ulong previous = 0;
            for (int i = 0; i < world.Resources.Length; i++)
            {
                ResourceEntry r = world.Resources[i];
                if (i > 0) Assert.Greater(r.Id, previous, "자원 표는 Id 오름차순이고 Id가 겹치지 않아야 합니다.");
                previous = r.Id;

                Assert.That(r.PosXmm, Is.InRange(0, world.WidthMm - 1));
                Assert.That(r.PosZmm, Is.InRange(0, world.DepthMm - 1));
                Assert.That(r.Type, Is.InRange((int)V0ResourceTypes.Tree, (int)V0ResourceTypes.Coal));
                Assert.That(r.ScaleQ8, Is.InRange(256, 383));
                Assert.AreEqual(world.HeightMmAt(r.PosXmm, r.PosZmm), r.PosYmm, "높이 Y는 정수 높이에서 계산한 값이어야 합니다.");
            }
        }

        [Test]
        public void ResourceCellsAndSlots_AreEncodedInId()
        {
            GeneratedWorld world = V0WorldGenerator.Generate(Seed("id-test"), V0Profile.Small);
            foreach (ResourceEntry r in world.Resources)
            {
                int cellX = (int)((r.Id >> 16) & 0xFFFF);
                int cellZ = (int)((r.Id >> 32) & 0xFFFF);
                Assert.AreEqual(cellX, r.PosXmm / 16000, "Id의 셀X가 위치와 맞지 않습니다.");
                Assert.AreEqual(cellZ, r.PosZmm / 16000, "Id의 셀Z가 위치와 맞지 않습니다.");
            }
        }

        [Test]
        public void TowerSites_KeepMinSpacing_AndAreSortedById()
        {
            V0Profile profile = V0Profile.Small;
            GeneratedWorld world = V0WorldGenerator.Generate(Seed("site-rules"), profile);
            Assert.Greater(world.TowerSites.Length, 1);
            Assert.LessOrEqual(world.TowerSites.Length, profile.SiteMaxCount);

            long minSq = (long)profile.SiteMinSpacingM * 1000 * profile.SiteMinSpacingM * 1000;
            for (int i = 0; i < world.TowerSites.Length; i++)
            {
                if (i > 0) Assert.Greater(world.TowerSites[i].Id, world.TowerSites[i - 1].Id);
                for (int j = i + 1; j < world.TowerSites.Length; j++)
                {
                    long dx = world.TowerSites[i].PosXmm - world.TowerSites[j].PosXmm;
                    long dz = world.TowerSites[i].PosZmm - world.TowerSites[j].PosZmm;
                    Assert.GreaterOrEqual(dx * dx + dz * dz, minSq, "후보 자리 간격이 최소 간격보다 가깝습니다.");
                }
            }
        }

        [Test]
        public void HeightQueries_AreIntegerAndConsistent()
        {
            GeneratedWorld world = V0WorldGenerator.Generate(Seed("query-test"), V0Profile.Small);
            // 격자점 위에서는 격자 값 그대로
            Assert.AreEqual(world.Height[5 * world.Width + 7], world.HeightMmAt(7000, 5000));
            // 두 격자 사이는 두 값 사이
            int a = world.HeightAt(10, 10);
            int b = world.HeightAt(11, 10);
            int mid = world.HeightMmAt(10500, 10000);
            Assert.That(mid, Is.InRange(Math.Min(a, b), Math.Max(a, b)));
            // 경사는 0 이상
            Assert.GreaterOrEqual(world.SlopeQ16(20, 20), 0);
            // 범위 밖은 가장자리 값
            Assert.AreEqual(world.HeightAt(0, 0), world.HeightAt(-5, -5));
        }

        // ---------- 요약값 ----------

        [Test]
        public void Hash_ChangesWhenOneHeightChanges_AndOnlyThatTileHashChanges()
        {
            GeneratedWorld world = V0WorldGenerator.Generate(Seed("hash-sensitivity"), V0Profile.Small);
            WorldHash before = WorldHasher.Compute(world);

            const int x = 300; // 타일 X = 1 (타일 크기 256)
            const int z = 10;  // 타일 Z = 0
            world.Height[z * world.Width + x] += 1;
            WorldHash after = WorldHasher.Compute(world);

            Assert.AreNotEqual(before.Total, after.Total);
            int changed = 0;
            for (int i = 0; i < before.TileHashes.Count; i++)
            {
                if (before.TileHashes[i] != after.TileHashes[i])
                {
                    changed++;
                    Assert.AreEqual(1, i, "바뀐 타일 번호");
                }
            }
            Assert.AreEqual(1, changed);
            Assert.AreEqual(before.ResourcesHash, after.ResourcesHash);
        }

        [Test]
        public void Hash_HasTileAndRegionPartialHashes()
        {
            GeneratedWorld world = V0WorldGenerator.Generate(Seed("partial"), V0Profile.Small);
            WorldHash hash = WorldHasher.Compute(world);
            Assert.AreEqual(16, hash.TileHashes.Count);
            Assert.AreEqual(1, hash.RegionHashes.Count);
            Assert.AreNotEqual(0UL, hash.ResourcesHash);
            Assert.AreNotEqual(0UL, hash.TowerSitesHash);
        }

        // ---------- 규모 ----------

        [Test]
        public void FullProfile_ProducesAboutTwentyThousandResources_InReasonableTime()
        {
            var watch = Stopwatch.StartNew();
            GeneratedWorld world = V0WorldGenerator.Generate(Seed(GoldenHashes.SeedTexts[0]), V0Profile.Full);
            WorldHash hash = WorldHasher.Compute(world);
            watch.Stop();

            Assert.AreEqual(3072, world.Width);
            Assert.That(world.Resources.Length, Is.InRange(19000, 21000), "자원 약 2만 개");
            Assert.AreEqual(36, hash.TileHashes.Count);
            Assert.Greater(world.TowerSites.Length, 5);
            // NFR-052(25초) 대비 여유 확인. 에디터(Mono)는 느리므로 넉넉한 상한만 둔다.
            Assert.Less(watch.Elapsed.TotalSeconds, 25.0, "Full 생성+요약 시간(초): " + watch.Elapsed.TotalSeconds);
            TestContext.WriteLine("Full 생성+요약 " + watch.ElapsedMilliseconds + " ms, 자원 " + world.Resources.Length + ", 후보 자리 " + world.TowerSites.Length);
        }

        // ---------- 골든 해시 ----------

        [Test]
        public void GoldenHash_Small_MatchesPinnedValues()
        {
            for (int i = 0; i < GoldenHashes.SeedTexts.Length; i++)
            {
                ulong actual = WorldHasher.Compute(V0WorldGenerator.Generate(Seed(GoldenHashes.SeedTexts[i]), V0Profile.Small)).Total;
                Assert.AreEqual(GoldenHashes.Small[i], actual,
                    "Small 골든 해시 불일치: " + GoldenHashes.SeedTexts[i] + " 실제=0x" + actual.ToString("x16"));
            }
        }

        [Test]
        public void GoldenHash_Full_MatchesPinnedValues()
        {
            for (int i = 0; i < GoldenHashes.SeedTexts.Length; i++)
            {
                ulong actual = WorldHasher.Compute(V0WorldGenerator.Generate(Seed(GoldenHashes.SeedTexts[i]), V0Profile.Full)).Total;
                Assert.AreEqual(GoldenHashes.Full[i], actual,
                    "Full 골든 해시 불일치: " + GoldenHashes.SeedTexts[i] + " 실제=0x" + actual.ToString("x16"));
            }
        }

        [Test]
        public void GoldenHash_Full_RepeatedGenerationOnSamePc_IsIdentical()
        {
            // 같은 PC에서 반복(3회, 서로 다른 병렬 수)해도 골든 값과 같다.
            ulong seed = Seed(GoldenHashes.SeedTexts[1]);
            foreach (int degree in new[] { -1, 1, 4 })
            {
                ulong actual = WorldHasher.Compute(V0WorldGenerator.Generate(seed, V0Profile.Full, 0, degree)).Total;
                Assert.AreEqual(GoldenHashes.Full[1], actual, "병렬 수 " + degree);
            }
        }

        // ---------- 도우미 ----------

        private static long SumHeights(GeneratedWorld world)
        {
            long sum = 0;
            foreach (int h in world.Height) sum += h;
            return sum;
        }

        private static int MaxStep(GeneratedWorld world)
        {
            int max = 0;
            for (int z = 0; z < world.Depth; z++)
            {
                for (int x = 0; x + 1 < world.Width; x++)
                {
                    max = Math.Max(max, Math.Abs(world.Height[z * world.Width + x + 1] - world.Height[z * world.Width + x]));
                }
            }
            return max;
        }
    }
}

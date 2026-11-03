// =============================================================
// 파일: WorldHasher.cs
// 역할: 생성 결과 요약값(해시)을 계산한다(DES-046). 참가자와 호스트의 월드가 같은지 비교하는 기준이고,
//       다르면 어디가 다른지 좁힐 수 있게 지역별·타일별 부분 요약값도 같이 계산한다.
// 레이어: domain(생성 규칙, 순수 C#)
// 관련: FR-169, NFR-053, NFR-054, DES-046 / SDD 3.11.5
//
// 요약값에 넣는 순서(고정)
//   "KHWG" 머리말, 생성기 버전, 시드, 시도 번호
//   -> 높이(타일 Z행 -> 타일 X열 -> 타일 안 행 순서, int32 리틀 엔디언)
//   -> 물 마스크 바이트
//   -> 자원(Id 오름차순: Id, 종류, X, Y, Z, 방향, 크기)
//   -> 등광탑 후보 자리(Id 오름차순: Id, X, Z, 종류)
// 이 순서나 소금 값을 바꾸면 모든 요약값이 바뀐다. 바꾸려면 새 생성기 버전을 만든다.
// =============================================================
using System;
using System.Collections.Generic;
using Game.WorldGen.Common;
using Game.WorldGen.Model;

namespace Game.WorldGen.Hashing
{
    /// <summary>요약값 계산 결과.</summary>
    public sealed class WorldHash
    {
        /// <summary>전체 요약값.</summary>
        public ulong Total { get; }

        /// <summary>타일별 요약값(타일 Z행 우선 순서). 불일치 위치를 좁히는 데 쓴다.</summary>
        public IReadOnlyList<ulong> TileHashes { get; }

        /// <summary>지역별 요약값(지역 번호 순서): 그 지역 타일 요약값들과 그 지역 자원.</summary>
        public IReadOnlyList<ulong> RegionHashes { get; }

        /// <summary>자원 표만의 요약값.</summary>
        public ulong ResourcesHash { get; }

        /// <summary>등광탑 후보 자리 표만의 요약값.</summary>
        public ulong TowerSitesHash { get; }

        public WorldHash(ulong total, IReadOnlyList<ulong> tileHashes, IReadOnlyList<ulong> regionHashes, ulong resourcesHash, ulong towerSitesHash)
        {
            Total = total;
            TileHashes = tileHashes;
            RegionHashes = regionHashes;
            ResourcesHash = resourcesHash;
            TowerSitesHash = towerSitesHash;
        }
    }

    /// <summary>월드 요약값 계산기.</summary>
    public static class WorldHasher
    {
        /// <summary>요약값 XXH64 소금 값(변경 금지).</summary>
        public const ulong HashSalt = 0x4B48574F524C4448UL;

        /// <summary>요약값 머리말.</summary>
        public const string Magic = "KHWG";

        /// <summary>자원 ID에서 지역 번호가 들어 있는 비트 위치(ID = 지역 &lt;&lt; 48 | 셀Z &lt;&lt; 32 | 셀X &lt;&lt; 16 | 슬롯).</summary>
        public const int ResourceIdRegionShift = 48;

        /// <summary>
        /// 월드의 요약값을 계산한다.
        /// </summary>
        /// <param name="world">생성된 월드</param>
        /// <returns>전체·지역별·타일별 요약값</returns>
        public static WorldHash Compute(GeneratedWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            // 타일별 요약값 (타일 하나의 높이만, 같은 소금 값)
            int tileCount = world.TilesX * world.TilesZ;
            var tileHashes = new ulong[tileCount];
            for (int tz = 0; tz < world.TilesZ; tz++)
            {
                for (int tx = 0; tx < world.TilesX; tx++)
                {
                    var tileStream = new Xxh64Stream(HashSalt);
                    WriteTileHeights(tileStream, world, tx, tz);
                    tileHashes[tz * world.TilesX + tx] = tileStream.Digest();
                }
            }

            // 자원·후보 자리 표 요약값
            var resourceStream = new Xxh64Stream(HashSalt);
            WriteResources(resourceStream, world.Resources, -1);
            ulong resourcesHash = resourceStream.Digest();

            var siteStream = new Xxh64Stream(HashSalt);
            WriteTowerSites(siteStream, world.TowerSites);
            ulong sitesHash = siteStream.Digest();

            // 전체 요약값
            var total = new Xxh64Stream(HashSalt);
            total.WriteString(Magic);
            total.WriteString(world.GeneratorVersion);
            total.WriteUInt64(world.Seed);
            total.WriteInt32(world.Attempt);
            for (int tz = 0; tz < world.TilesZ; tz++)
            {
                for (int tx = 0; tx < world.TilesX; tx++)
                {
                    WriteTileHeights(total, world, tx, tz);
                }
            }
            total.WriteUInt32((uint)world.WaterMask.Length);
            total.Write(world.WaterMask);
            WriteResources(total, world.Resources, -1);
            WriteTowerSites(total, world.TowerSites);

            // 지역별 요약값: V0는 지역이 하나라 모든 타일이 0번 지역이다. 지역이 늘면 타일의 지역 판정을 이 곳에 넣는다.
            var regionHashes = new ulong[Math.Max(1, world.RegionCount)];
            for (int region = 0; region < regionHashes.Length; region++)
            {
                var regionStream = new Xxh64Stream(HashSalt);
                if (region == 0)
                {
                    for (int i = 0; i < tileHashes.Length; i++)
                    {
                        regionStream.WriteUInt64(tileHashes[i]);
                    }
                }
                WriteResources(regionStream, world.Resources, region);
                regionHashes[region] = regionStream.Digest();
            }

            return new WorldHash(total.Digest(), tileHashes, regionHashes, resourcesHash, sitesHash);
        }

        private static void WriteTileHeights(Xxh64Stream stream, GeneratedWorld world, int tx, int tz)
        {
            int xStart = tx * world.TileSize;
            int zStart = tz * world.TileSize;
            int xEnd = Math.Min(xStart + world.TileSize, world.Width);
            int zEnd = Math.Min(zStart + world.TileSize, world.Depth);
            for (int z = zStart; z < zEnd; z++)
            {
                stream.WriteInt32Array(world.Height, z * world.Width + xStart, xEnd - xStart);
            }
        }

        /// <param name="regionFilter">-1이면 전부, 아니면 그 지역 번호의 자원만</param>
        private static void WriteResources(Xxh64Stream stream, ResourceEntry[] resources, int regionFilter)
        {
            for (int i = 0; i < resources.Length; i++)
            {
                ResourceEntry r = resources[i];
                if (regionFilter >= 0 && (int)(r.Id >> ResourceIdRegionShift) != regionFilter)
                {
                    continue;
                }
                stream.WriteUInt64(r.Id);
                stream.WriteUInt16(r.Type);
                stream.WriteInt32(r.PosXmm);
                stream.WriteInt32(r.PosYmm);
                stream.WriteInt32(r.PosZmm);
                stream.WriteUInt16(r.Yaw);
                stream.WriteUInt16(r.ScaleQ8);
            }
        }

        private static void WriteTowerSites(Xxh64Stream stream, TowerSite[] sites)
        {
            for (int i = 0; i < sites.Length; i++)
            {
                TowerSite s = sites[i];
                stream.WriteUInt64(s.Id);
                stream.WriteInt32(s.PosXmm);
                stream.WriteInt32(s.PosZmm);
                stream.WriteByte(s.Kind);
            }
        }
    }
}

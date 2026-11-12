// =============================================================
// 파일: V0WorldGenerator.cs
// 역할: M1 결정론 선행 검증용 시험 생성기 V0. 정수 높이(노이즈 + 침식), 자원 약 2만 개, 등광탑 후보 자리를 만든다.
//       실제 게임 생성기(V1)가 쓸 결정론 규칙(정수·고정소수점, 카운터 난수, 이중 버퍼 병렬, 정렬된 합치기)을
//       그대로 따라서, 같은 PC 반복·Mono/IL2CPP·다른 CPU 결과가 같은지 미리 확인하는 용도다.
// 레이어: domain(생성 규칙, 순수 C#)
// 관련: FR-168, FR-169, NFR-052, NFR-053, DES-043 / SDD 3.11.1, 3.11.4
//
// 결정론 규칙 적용
// - 병렬 작업은 자기 칸에만 쓴다(타일 -> 자기 타일 칸, 침식 -> 자기 행). 이웃을 읽는 침식은 이중 버퍼.
// - 자원 합치기·정렬은 단일 스레드에서 고정 순서(셀Z -> 셀X -> 슬롯).
// - 컬렉션은 배열과 정렬된 목록만 쓴다(Dictionary/HashSet 순회 금지). 정렬 비교는 동률 없이 Id로 끝낸다.
// - 난수는 DetMath.Rand(키, 좌표)만. 부동소수점·System.Random·시간 사용 없음.
// V0는 출시 전에 삭제할 수 있다(SDD 3.8.2).
// =============================================================
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.WorldGen.Common;
using Game.WorldGen.Model;

namespace Game.WorldGen.V0
{
    /// <summary>V0 자원 종류 번호.</summary>
    public static class V0ResourceTypes
    {
        public const ushort None = 0;
        public const ushort Tree = 1;
        public const ushort Stone = 2;
        public const ushort IronOre = 3;
        public const ushort Coal = 4;
    }

    /// <summary>V0 시험용 월드 생성기.</summary>
    public static class V0WorldGenerator
    {
        /// <summary>생성기 버전 이름. 요약값에 들어간다.</summary>
        public const string Version = "V0";

        // 스트림 단계 번호 (StreamKey의 stageId)
        private const int StageMacroHeight = 1;
        private const int StageDetailHeight = 2;
        private const int StageResources = 3;
        private const int StageSites = 4;

        // 높이 노이즈 (좌표 Q16.16: 미터에 2^k를 곱하면 파장이 65536/2^k 이 아니라 2^(16-k) m)
        private const int BaseHeightMm = 300_000;
        private const int MacroShift = 6;               // 파장 1024 m
        private const int MacroOctaves = 3;
        private const int MacroAmplitudeMm = 200_000;
        private const int DetailShift = 9;              // 파장 128 m
        private const int DetailOctaves = 4;
        private const int DetailAmplitudeMm = 15_000;
        private const int NoiseGainQ16 = 32768;         // 0.5
        private const int NoiseUnitShift = 15;          // 노이즈 값 단위: Q15
        private const int ErosionDivisor = 8;

        // 자원
        private const int MmPerMeter = 1000;
        private const int ResourceIdCellZShift = 32;
        private const int ResourceIdCellXShift = 16;
        private const int RegionZero = 0;
        private const int RollRange = 65536;
        private const int PercentBase = 100;
        private const int TreeUntilPercent = 55;
        private const int StoneUntilPercent = 80;
        private const int IronUntilPercent = 92;
        private const int ScaleBaseQ8 = 256;
        private const uint ScaleVariationQ8 = 128;
        private const int SlotStride = 8;

        // 등광탑 후보
        private const int SiteScoreBase = 1_000_000;
        private const uint SiteScoreJitter = 1000;
        private const int SiteSamplePoints = 5;

        /// <summary>
        /// 월드를 만든다.
        /// </summary>
        /// <param name="seed">월드 시드(u64)</param>
        /// <param name="profile">생성 프로필</param>
        /// <param name="attempt">재생성 시도 번호(0부터)</param>
        /// <param name="maxDegreeOfParallelism">병렬 작업 수 상한. -1이면 기본값, 1이면 단일 스레드(결과는 같아야 한다)</param>
        public static GeneratedWorld Generate(ulong seed, V0Profile profile, int attempt = 0, int maxDegreeOfParallelism = -1)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }
            var options = new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism };
            ulong s = DetMath.RetrySeed(seed, attempt);
            int size = profile.WorldSizeM;

            // 1) 높이: 타일마다 자기 칸에만 쓴다.
            int[] height = GenerateHeights(s, attempt, profile, options);

            // 2) 침식: 이중 버퍼. 한 번 돌 때마다 현재/다음 버퍼를 맞바꾼다.
            for (int i = 0; i < profile.ErosionIterations; i++)
            {
                int[] next = new int[height.Length];
                ErodeOnce(height, next, size, profile.TalusMm, options);
                height = next;
            }

            var heightOnly = new GeneratedWorld(Version, seed, attempt, size, size, profile.TileSizeM, 1, height, null, null, null);

            // 3) 자원: 셀마다 후보를 만들고 단일 스레드에서 고정 순서로 합친다.
            ResourceEntry[] resources = PlaceResources(s, attempt, profile, heightOnly, options);

            // 4) 등광탑 후보 자리
            TowerSite[] sites = PickTowerSites(s, attempt, profile, heightOnly, options);

            return new GeneratedWorld(Version, seed, attempt, size, size, profile.TileSizeM, 1, height, null, resources, sites);
        }

        // ------------------------------------------------------------
        // 높이
        // ------------------------------------------------------------
        private static int[] GenerateHeights(ulong s, int attempt, V0Profile profile, ParallelOptions options)
        {
            int size = profile.WorldSizeM;
            int tile = profile.TileSizeM;
            int tilesPerSide = size / tile;
            var height = new int[size * size];
            ulong macroKey = DetMath.StreamKey(s, RegionZero, StageMacroHeight, attempt);
            ulong detailKey = DetMath.StreamKey(s, RegionZero, StageDetailHeight, attempt);

            Parallel.For(0, tilesPerSide * tilesPerSide, options, tileIndex =>
            {
                int tx = tileIndex % tilesPerSide;
                int tz = tileIndex / tilesPerSide;
                for (int z = tz * tile; z < (tz + 1) * tile; z++)
                {
                    int row = z * size;
                    for (int x = tx * tile; x < (tx + 1) * tile; x++)
                    {
                        int macro = DetMath.Fbm(macroKey, x << MacroShift, z << MacroShift, MacroOctaves, NoiseGainQ16);
                        int detail = DetMath.Fbm(detailKey, x << DetailShift, z << DetailShift, DetailOctaves, NoiseGainQ16);
                        long h = BaseHeightMm
                                 + (((long)macro * MacroAmplitudeMm) >> NoiseUnitShift)
                                 + (((long)detail * DetailAmplitudeMm) >> NoiseUnitShift);
                        height[row + x] = (int)h;
                    }
                }
            });
            return height;
        }

        /// <summary>열 침식 한 번. 이웃과의 높이 차가 talus보다 크면 차이의 1/8을 옮긴다(질량 보존, 정수).</summary>
        private static void ErodeOnce(int[] current, int[] next, int size, int talusMm, ParallelOptions options)
        {
            // 행마다 자기 행(next)에만 쓰고 current는 읽기만 한다.
            Parallel.For(0, size, options, z =>
            {
                for (int x = 0; x < size; x++)
                {
                    int index = z * size + x;
                    int center = current[index];
                    int value = center;
                    if (x > 0) value += Transfer(current[index - 1] - center, talusMm);
                    if (x < size - 1) value += Transfer(current[index + 1] - center, talusMm);
                    if (z > 0) value += Transfer(current[index - size] - center, talusMm);
                    if (z < size - 1) value += Transfer(current[index + size] - center, talusMm);
                    next[index] = value;
                }
            });
        }

        /// <summary>이웃과의 높이 차(이웃 - 나)에서 이 칸이 받는 양. 부호가 대칭이라 합계가 변하지 않는다.</summary>
        private static int Transfer(int diff, int talusMm)
        {
            if (diff > talusMm) return (diff - talusMm) / ErosionDivisor;
            if (diff < -talusMm) return (diff + talusMm) / ErosionDivisor;
            return 0;
        }

        // ------------------------------------------------------------
        // 자원
        // ------------------------------------------------------------
        private struct Candidate
        {
            public bool Valid;
            public ulong Id;
            public ushort Type;
            public int XMm;
            public int ZMm;
            public ushort Yaw;
            public ushort ScaleQ8;
        }

        private static ResourceEntry[] PlaceResources(ulong s, int attempt, V0Profile profile, GeneratedWorld world, ParallelOptions options)
        {
            int cells = profile.WorldSizeM / profile.CellSizeM;
            int slots = profile.SlotsPerCell;
            ulong key = DetMath.StreamKey(s, RegionZero, StageResources, attempt);
            var candidates = new Candidate[cells * cells * slots];

            // 셀마다 자기 칸(후보 배열의 자기 구간)에만 쓴다.
            Parallel.For(0, cells, options, cz =>
            {
                for (int cx = 0; cx < cells; cx++)
                {
                    for (int slot = 0; slot < slots; slot++)
                    {
                        candidates[(cz * cells + cx) * slots + slot] = MakeCandidate(key, profile, cx, cz, slot);
                    }
                }
            });

            // 고정 순서(셀Z -> 셀X -> 슬롯)로 합친다. 이 순서가 곧 Id 오름차순이다.
            var list = new List<ResourceEntry>(candidates.Length);
            for (int i = 0; i < candidates.Length; i++)
            {
                Candidate c = candidates[i];
                if (!c.Valid)
                {
                    continue;
                }
                int y = world.HeightMmAt(c.XMm, c.ZMm);
                list.Add(new ResourceEntry(c.Id, c.Type, c.XMm, y, c.ZMm, c.Yaw, c.ScaleQ8));
            }
            ResourceEntry[] result = list.ToArray();
            Array.Sort(result, (a, b) => a.Id.CompareTo(b.Id)); // Id는 겹치지 않으므로 동률 없음
            return result;
        }

        private static Candidate MakeCandidate(ulong key, V0Profile profile, int cx, int cz, int slot)
        {
            int cellMm = profile.CellSizeM * MmPerMeter;
            int baseIndex = slot * SlotStride;
            uint jitterX = DetMath.RandBelow(key, cx, cz, baseIndex + 0, (uint)cellMm);
            uint jitterZ = DetMath.RandBelow(key, cx, cz, baseIndex + 1, (uint)cellMm);
            uint roll = DetMath.RandBelow(key, cx, cz, baseIndex + 2, RollRange);
            uint yaw = DetMath.RandBelow(key, cx, cz, baseIndex + 3, 65536);
            uint scaleVariation = DetMath.RandBelow(key, cx, cz, baseIndex + 4, ScaleVariationQ8);

            var candidate = new Candidate
            {
                Id = ((ulong)RegionZero << 48) | ((ulong)(uint)cz << ResourceIdCellZShift) | ((ulong)(uint)cx << ResourceIdCellXShift) | (uint)slot,
                XMm = cx * cellMm + (int)jitterX,
                ZMm = cz * cellMm + (int)jitterZ,
                Yaw = (ushort)yaw,
                ScaleQ8 = (ushort)(ScaleBaseQ8 + (int)scaleVariation),
            };

            if (roll >= (uint)profile.SpawnChanceQ16)
            {
                candidate.Valid = false;
                return candidate;
            }

            // 확률 구간 안에서의 위치(0..99)로 종류를 정한다.
            int percent = (int)(roll * (uint)PercentBase / (uint)profile.SpawnChanceQ16);
            if (percent < TreeUntilPercent) candidate.Type = V0ResourceTypes.Tree;
            else if (percent < StoneUntilPercent) candidate.Type = V0ResourceTypes.Stone;
            else if (percent < IronUntilPercent) candidate.Type = V0ResourceTypes.IronOre;
            else candidate.Type = V0ResourceTypes.Coal;
            candidate.Valid = true;
            return candidate;
        }

        // ------------------------------------------------------------
        // 등광탑 후보 자리
        // ------------------------------------------------------------
        private struct SiteCandidate
        {
            public ulong Id;
            public int XMm;
            public int ZMm;
            public int Score;
        }

        private static TowerSite[] PickTowerSites(ulong s, int attempt, V0Profile profile, GeneratedWorld world, ParallelOptions options)
        {
            int cells = profile.WorldSizeM / profile.SiteCellSizeM;
            int cellMm = profile.SiteCellSizeM * MmPerMeter;
            ulong key = DetMath.StreamKey(s, DetMath.RegionAny, StageSites, attempt);
            var candidates = new SiteCandidate[cells * cells];

            // 후보 점수: 셀 안 5점의 높이 차가 작을수록(평평할수록) 높고, 약간의 무작위 값을 더한다.
            Parallel.For(0, cells, options, cz =>
            {
                for (int cx = 0; cx < cells; cx++)
                {
                    int originX = cx * cellMm;
                    int originZ = cz * cellMm;
                    int centerX = originX + cellMm / 2;
                    int centerZ = originZ + cellMm / 2;
                    int min = int.MaxValue;
                    int max = int.MinValue;
                    for (int p = 0; p < SiteSamplePoints; p++)
                    {
                        int px = p == 1 ? originX : (p == 2 ? originX + cellMm - 1 : centerX);
                        int pz = p == 3 ? originZ : (p == 4 ? originZ + cellMm - 1 : centerZ);
                        int h = world.HeightMmAt(px, pz);
                        if (h < min) min = h;
                        if (h > max) max = h;
                    }
                    int score = SiteScoreBase - (max - min) + (int)DetMath.RandBelow(key, cx, cz, 0, SiteScoreJitter);
                    candidates[cz * cells + cx] = new SiteCandidate
                    {
                        Id = ((ulong)(uint)cz << ResourceIdCellXShift) | (uint)cx,
                        XMm = centerX,
                        ZMm = centerZ,
                        Score = score,
                    };
                }
            });

            // 점수 높은 순, 같으면 Id 작은 순으로 정렬(동률 없음 -> 정렬 알고리즘과 무관하게 같은 결과).
            Array.Sort(candidates, (a, b) =>
            {
                int byScore = b.Score.CompareTo(a.Score);
                return byScore != 0 ? byScore : a.Id.CompareTo(b.Id);
            });

            // 최소 간격 탐욕 선택(단일 스레드). 거리 비교는 제곱 정수로 한다.
            long minSpacingMm = (long)profile.SiteMinSpacingM * MmPerMeter;
            long minSpacingSq = minSpacingMm * minSpacingMm;
            var chosen = new List<SiteCandidate>();
            for (int i = 0; i < candidates.Length && chosen.Count < profile.SiteMaxCount; i++)
            {
                SiteCandidate c = candidates[i];
                bool farEnough = true;
                for (int j = 0; j < chosen.Count; j++)
                {
                    long dx = c.XMm - chosen[j].XMm;
                    long dz = c.ZMm - chosen[j].ZMm;
                    if (dx * dx + dz * dz < minSpacingSq)
                    {
                        farEnough = false;
                        break;
                    }
                }
                if (farEnough)
                {
                    chosen.Add(c);
                }
            }

            chosen.Sort((a, b) => a.Id.CompareTo(b.Id));
            var sites = new TowerSite[chosen.Count];
            for (int i = 0; i < sites.Length; i++)
            {
                sites[i] = new TowerSite(chosen[i].Id, chosen[i].XMm, chosen[i].ZMm, 0);
            }
            return sites;
        }
    }
}

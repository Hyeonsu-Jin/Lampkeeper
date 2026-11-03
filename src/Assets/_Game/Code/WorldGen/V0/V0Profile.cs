// =============================================================
// 파일: V0Profile.cs
// 역할: 시험용 생성기 V0의 설정값 묶음(생성 프로필). 월드 크기, 타일·셀 크기, 침식 반복 수, 자원 비율 등.
// 레이어: domain(생성 규칙, 순수 C#)
// 관련: FR-168, NFR-052, NFR-053, DES-043 / SDD 3.11.1(설정값 변경), 3.11.4
//
// 생성에 영향을 주는 값은 모두 여기에 두고 생성기 버전에 묶는다. 값을 바꾸면 결과가 달라지므로
// Full 프로필의 값을 바꿀 때는 골든 해시도 같이 갱신한다. V0는 출시 전에 삭제할 수 있는 시험용이다.
// =============================================================
using System;

namespace Game.WorldGen.V0
{
    /// <summary>V0 생성 프로필. 만든 뒤 바꾸지 않는다.</summary>
    public sealed class V0Profile
    {
        /// <summary>월드 한 변(m). 3 km x 3 km (SRS 2.1).</summary>
        public int WorldSizeM { get; }

        /// <summary>타일 한 변(m). 병렬 생성과 요약값의 단위. Unity Terrain 타일(512 m)과 같다.</summary>
        public int TileSizeM { get; }

        /// <summary>자원 배치 셀 한 변(m).</summary>
        public int CellSizeM { get; }

        /// <summary>등광탑 후보 자리 평가 셀 한 변(m).</summary>
        public int SiteCellSizeM { get; }

        /// <summary>침식(열 침식) 반복 수. 이웃 높이를 읽는 이중 버퍼 반복이라 병렬 결정론을 시험한다.</summary>
        public int ErosionIterations { get; }

        /// <summary>침식에서 이 값(mm)보다 큰 인접 높이 차를 깎는다.</summary>
        public int TalusMm { get; }

        /// <summary>셀마다 만드는 자원 슬롯 수.</summary>
        public int SlotsPerCell { get; }

        /// <summary>슬롯이 자원이 될 확률(0..65535, Q16). 자원 약 2만 개를 목표로 정한 값.</summary>
        public int SpawnChanceQ16 { get; }

        /// <summary>등광탑 후보 자리끼리의 최소 간격(m).</summary>
        public int SiteMinSpacingM { get; }

        /// <summary>등광탑 후보 자리 최대 수.</summary>
        public int SiteMaxCount { get; }

        public V0Profile(
            int worldSizeM,
            int tileSizeM,
            int cellSizeM,
            int siteCellSizeM,
            int erosionIterations,
            int talusMm,
            int slotsPerCell,
            int spawnChanceQ16,
            int siteMinSpacingM,
            int siteMaxCount)
        {
            if (worldSizeM <= 0 || tileSizeM <= 0 || cellSizeM <= 0 || siteCellSizeM <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(worldSizeM), "크기 값은 0보다 커야 합니다.");
            }
            if (worldSizeM % tileSizeM != 0 || worldSizeM % cellSizeM != 0 || worldSizeM % siteCellSizeM != 0)
            {
                throw new ArgumentException("월드 크기는 타일·셀 크기로 나누어떨어져야 합니다.");
            }
            if (spawnChanceQ16 < 0 || spawnChanceQ16 > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(spawnChanceQ16));
            }
            WorldSizeM = worldSizeM;
            TileSizeM = tileSizeM;
            CellSizeM = cellSizeM;
            SiteCellSizeM = siteCellSizeM;
            ErosionIterations = erosionIterations;
            TalusMm = talusMm;
            SlotsPerCell = slotsPerCell;
            SpawnChanceQ16 = spawnChanceQ16;
            SiteMinSpacingM = siteMinSpacingM;
            SiteMaxCount = siteMaxCount;
        }

        /// <summary>
        /// 실제 규모: 3072 m, 타일 512 m(6x6), 셀 16 m(192x192 = 36,864칸), 자원 약 20,000개 목표
        /// (확률 35,553 / 65,536 = 54.25%).
        /// </summary>
        public static V0Profile Full { get; } = new V0Profile(
            worldSizeM: 3072,
            tileSizeM: 512,
            cellSizeM: 16,
            siteCellSizeM: 32,
            erosionIterations: 2,
            talusMm: 1500,
            slotsPerCell: 1,
            spawnChanceQ16: 35553,
            siteMinSpacingM: 300,
            siteMaxCount: 24);

        /// <summary>
        /// 시험용 작은 규모: 1024 m, 타일 256 m(4x4), 셀 16 m(64x64 = 4,096칸). 같은 규칙, 더 빨리 끝난다.
        /// </summary>
        public static V0Profile Small { get; } = new V0Profile(
            worldSizeM: 1024,
            tileSizeM: 256,
            cellSizeM: 16,
            siteCellSizeM: 32,
            erosionIterations: 2,
            talusMm: 1500,
            slotsPerCell: 1,
            spawnChanceQ16: 35553,
            siteMinSpacingM: 150,
            siteMaxCount: 8);
    }
}

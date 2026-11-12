// =============================================================
// 파일: BalanceValues.cs
// 역할: SRS 부록 C.1 "설정값 기본값 후보"를 담는 설정값 묶음(읽기 전용으로 쓴다).
//       값을 바꾸고 싶으면 코드가 아니라 Data/Balance 애셋을 고친다(NFR-033).
// 레이어: application (순수 C#). Unity 직렬화가 읽을 수 있게 [Serializable] 공개 필드로 둔다.
// 관련: NFR-033(설정값 데이터 분리, C-33 해소), DES-104 / SDD 3.7.7
//
// 규칙
// - 초기값은 전부 SRS 부록 C.1의 "후보값"이다. 플레이 테스트로 조정한다.
// - 이 파일에 없는 C.1 항목은 "미정"이거나 뒤 마일스톤에서 필드를 더한다:
//   등광탑 체력(웨이브 구성 확정 후), 행동 경험치·레벨업 필요 경험치(C-06) -> 값이 정해질 때 필드 추가.
// - 필드를 더하거나 이름을 바꾸면 ContentHash가 바뀐다(접속 때 호스트와 같은지 비교).
// - 섹션 클래스는 값 형식과 문자열만 가진다(MemberwiseClone으로 복사하기 위해).
// =============================================================
using System;

namespace Game.Application.Config
{
    /// <summary>이동·상호작용 [FR-007, FR-023, FR-024, NFR-002]</summary>
    [Serializable]
    public sealed class MovementSettings
    {
        /// <summary>걷기 속도(m/s)</summary>
        public float WalkSpeedMps = 4f;
        /// <summary>달리기 속도(m/s)</summary>
        public float RunSpeedMps = 7f;
        /// <summary>상호작용 거리(m)</summary>
        public float InteractionDistanceMeters = 2.5f;
        /// <summary>호스트 이동 검사의 속도 허용 배율(네트워크 지터 흡수) [NFR-030, SDD 3.11.12]</summary>
        public float MoveTolerance = 1.25f;
        /// <summary>호스트 이동 검사의 여유 거리(m)</summary>
        public float MoveSlackMeters = 0.5f;
        /// <summary>서버 추정 스태미나가 0이 된 뒤 달리기 속도를 계속 허용하는 시간(초). 추정 오차로 정상 플레이어가 되돌려지지 않게 하는 여유</summary>
        public float MoveStaminaGraceSeconds = 0.5f;
        /// <summary>호스트가 한 번에 인정하는 자세 시간 간격 상한(초)</summary>
        public float MaxPoseDeltaSeconds = 0.5f;
        /// <summary>수직 이동(낙하·점프) 최대 속도(m/s)</summary>
        public float MaxVerticalSpeedMps = 40f;
        /// <summary>호스트가 받는 자세 빈도 상한(Hz). 송신 주기(Network.AvatarSendHz)의 1.5배 [IMP-001, R-47, SDD 3.11.12]</summary>
        public float MaxPoseHz = 45f;
        /// <summary>자세 빈도 버킷의 순간 허용 개수(잠깐 끊겼다 몰려 들어오는 자세 흡수) [R-47]</summary>
        public int PoseBurstCapacity = 5;
        /// <summary>첫 자세가 스폰 지점에서 벗어나도 되는 수평 반경(m) [REV-003, R-47]</summary>
        public float SpawnRadiusMeters = 15f;
        /// <summary>첫 자세가 스폰 지점에서 벗어나도 되는 높이 차(m) [R-47]</summary>
        public float SpawnHeightToleranceMeters = 15f;

        /// <summary>값을 복사한 새 객체.</summary>
        public MovementSettings Clone() => (MovementSettings)MemberwiseClone();
    }

    /// <summary>스태미나 [FR-024, FR-025]. 후보값이면 달리기 약 10초.</summary>
    [Serializable]
    public sealed class StaminaSettings
    {
        /// <summary>스태미나 최대</summary>
        public float Max = 100f;
        /// <summary>달리기 소모량(초당)</summary>
        public float RunCostPerSecond = 10f;
        /// <summary>회복이 시작되기까지의 지연(초)</summary>
        public float RegenDelaySeconds = 1f;
        /// <summary>회복량(초당)</summary>
        public float RegenPerSecond = 20f;

        public StaminaSettings Clone() => (StaminaSettings)MemberwiseClone();
    }

    /// <summary>생존 상태 값 [FR-026~FR-032, FR-104, FR-105, FR-142, FR-143, FR-161]</summary>
    [Serializable]
    public sealed class VitalsSettings
    {
        public float HealthMax = 100f;
        public float HungerMax = 100f;
        /// <summary>허기가 최대에서 0까지 줄어드는 실시간(분)</summary>
        public float HungerDepleteRealMinutes = 60f;
        public float ThirstMax = 100f;
        /// <summary>갈증이 최대에서 0까지 줄어드는 실시간(분)</summary>
        public float ThirstDepleteRealMinutes = 40f;
        public float FatigueMax = 100f;
        /// <summary>피로도가 최대에서 0까지 줄어드는 게임 내 일수. M2~M3 시험에서는 0으로 둘 수 있다(SRS 3.2).</summary>
        public float FatigueDepleteGameDays = 2f;
        /// <summary>수면 회복: 이 실시간(초)에 피로도 최대치만큼 회복</summary>
        public float SleepRecoverRealSeconds = 60f;
        /// <summary>쓰러짐 유지 시간(초)</summary>
        public float DownedDurationSeconds = 60f;
        /// <summary>소생 시간(초)</summary>
        public float ReviveSeconds = 5f;
        /// <summary>소생 후 체력(최대 체력 대비 비율)</summary>
        public float ReviveHealthRatio = 0.25f;
        /// <summary>독기 피해(초당 체력)</summary>
        public float PoisonDamagePerSecond = 1f;
        /// <summary>혹한 피해(초당 체력)</summary>
        public float ColdDamagePerSecond = 0.5f;
        /// <summary>혹한 중 스태미나 회복 감소 비율</summary>
        public float ColdStaminaRegenReduction = 0.5f;

        public VitalsSettings Clone() => (VitalsSettings)MemberwiseClone();
    }

    /// <summary>하루 길이·시야 [FR-033, FR-034, FR-106]</summary>
    [Serializable]
    public sealed class WorldTimeSettings
    {
        /// <summary>하루 길이(실시간 분)</summary>
        public float DayLengthRealMinutes = 30f;
        /// <summary>하루 중 낮 비율(0~1). 0.65면 낮 약 20분, 밤 약 10분</summary>
        public float DaytimeRatio = 0.65f;
        /// <summary>밤 가시 거리(m)</summary>
        public float NightVisibilityMeters = 5f;
        /// <summary>어둠 가시 거리(m)</summary>
        public float DarknessVisibilityMeters = 3f;

        public WorldTimeSettings Clone() => (WorldTimeSettings)MemberwiseClone();
    }

    /// <summary>광원 [FR-036, FR-037]</summary>
    [Serializable]
    public sealed class LightSettings
    {
        public float TorchRadiusMeters = 8f;
        public float LanternRadiusMeters = 12f;
        public float DeviceLanternRadiusMeters = 15f;
        /// <summary>광원 퇴치 지속 시간(초). 효과 규칙은 C-19</summary>
        public float RepelSeconds = 5f;

        public LightSettings Clone() => (LightSettings)MemberwiseClone();
    }

    /// <summary>인벤토리·보관함 [FR-038, FR-133]</summary>
    [Serializable]
    public sealed class InventorySettings
    {
        public int SlotCount = 32;
        public int MaxStackResource = 50;
        public int MaxStackTool = 1;
        public int MaxStackFood = 20;
        public int ContainerSlotCount = 24;

        public InventorySettings Clone() => (InventorySettings)MemberwiseClone();
    }

    /// <summary>채집 타격 횟수 [FR-047, FR-049, FR-179]. 도구 등급 보정은 C-09.</summary>
    [Serializable]
    public sealed class GatheringSettings
    {
        public int TreeBasicHits = 5;
        public int TreeAdvancedMinHits = 8;
        public int TreeAdvancedMaxHits = 12;
        public int StoneHits = 4;
        public int CoalVeinHits = 5;
        public int OreBasicHits = 6;
        public int OreRareHits = 10;

        public GatheringSettings Clone() => (GatheringSettings)MemberwiseClone();
    }

    /// <summary>용광로 [FR-175~FR-178]. 숯 비율은 C-63.</summary>
    [Serializable]
    public sealed class FurnaceSettings
    {
        /// <summary>광석 1개 가공에 드는 연료 점수</summary>
        public int OreProcessFuelCost = 4;
        public int WoodFuelPoints = 2;
        public int CoalFuelPoints = 16;
        public int CharcoalFuelPoints = 16;
        /// <summary>가공 1회당 실시간(초)</summary>
        public float ProcessRealSeconds = 20f;
        /// <summary>숯 1회분에 드는 나무 개수</summary>
        public int CharcoalWoodCost = 4;
        /// <summary>숯 1회분 결과 개수</summary>
        public int CharcoalYield = 1;

        public FurnaceSettings Clone() => (FurnaceSettings)MemberwiseClone();
    }

    /// <summary>내구도·수리·철거 [FR-135~FR-138]</summary>
    [Serializable]
    public sealed class DurabilitySettings
    {
        /// <summary>1등급 도구의 내구도 최대</summary>
        public int BaseToolDurability = 100;
        /// <summary>등급마다 늘어나는 내구도</summary>
        public int DurabilityPerTier = 50;
        public int LossPerUse = 1;
        /// <summary>수리 재료: 제작 재료 대비 비율(최소 1개)</summary>
        public float RepairMaterialRatio = 0.25f;
        /// <summary>철거 회수 비율</summary>
        public float DemolishRefundRatio = 0.75f;

        public DurabilitySettings Clone() => (DurabilitySettings)MemberwiseClone();
    }

    /// <summary>저장·로그 [FR-018, FR-019, NFR-045]</summary>
    [Serializable]
    public sealed class SaveSettings
    {
        /// <summary>자동 저장 간격(분). 최대 손실 시간과 같다</summary>
        public float AutosaveIntervalMinutes = 10f;
        public int BackupCount = 5;
        /// <summary>로그 보관: 최근 N회 실행분</summary>
        public int LogKeepRuns = 5;

        public SaveSettings Clone() => (SaveSettings)MemberwiseClone();
    }

    /// <summary>등광탑·방어전 [FR-062~FR-065, FR-069, FR-071, FR-154, NFR-017]</summary>
    [Serializable]
    public sealed class DefenseSettings
    {
        public int WavesTutorial = 3;
        public int WavesNormal = 5;
        public int WavesCore = 7;
        public int WavesFinal = 10;
        public float WaveIntervalSeconds = 60f;
        public float PrepSeconds = 60f;
        /// <summary>인원 1명이 늘 때마다 더해지는 웨이브 배율 (8명이면 4.5)</summary>
        public float WaveBonusPerExtraPlayer = 0.5f;
        /// <summary>인원 1명이 늘 때마다 더해지는 보스 체력 배율 (8명이면 6.25)</summary>
        public float BossHealthBonusPerExtraPlayer = 0.75f;
        public float PurifyRadiusNormalMeters = 100f;
        public float PurifyRadiusCoreMeters = 200f;
        public float PurifyRadiusTutorialMeters = 60f;
        public float StartPurifyRadiusMeters = 30f;
        /// <summary>실패 시 재료 손실 비율</summary>
        public float FailMaterialLossRatio = 0.30f;
        /// <summary>동시 적 상한. M1 측정 뒤 확정</summary>
        public int MaxConcurrentEnemies = 60;

        public DefenseSettings Clone() => (DefenseSettings)MemberwiseClone();
    }

    /// <summary>생물·동물 [FR-075, FR-077, FR-079, FR-082]</summary>
    [Serializable]
    public sealed class CreatureSettings
    {
        public float AnimalSpawnIntervalMinutes = 5f;
        public int AnimalMaxPerPurifiedZone = 6;
        public float DetectionDistanceMeters = 20f;
        public float NightRaidStartDelaySeconds = 60f;

        public CreatureSettings Clone() => (CreatureSettings)MemberwiseClone();
    }

    /// <summary>농사·요리·버프 [FR-084, FR-085, FR-091, FR-096, FR-107]</summary>
    [Serializable]
    public sealed class FarmingSettings
    {
        /// <summary>물 유지 시간(게임 내 일)</summary>
        public float WaterKeepGameDays = 1f;
        public float GrowMinGameDays = 1f;
        public float GrowMaxGameDays = 3f;
        /// <summary>기구 인식 거리(m)</summary>
        public float StationRecognitionMeters = 3f;
        /// <summary>위협 대응 요리 버프 지속(분)</summary>
        public float BuffDurationMinutes = 10f;

        public FarmingSettings Clone() => (FarmingSettings)MemberwiseClone();
    }

    /// <summary>세션·채팅·요청 제한 [FR-115, FR-116, FR-126~FR-128, FR-146, FR-159, NFR-038~041, NFR-050]</summary>
    [Serializable]
    public sealed class SessionSettings
    {
        /// <summary>최대 인원</summary>
        public int MaxPlayers = 8;
        /// <summary>연결 끊김 판정 시간(초)</summary>
        public float DisconnectTimeoutSeconds = 10f;
        /// <summary>초당 요청 상한(NFR-041)</summary>
        public int RequestsPerSecondLimit = 30;
        /// <summary>재접속 자리 유지 시간(초)</summary>
        public float ReconnectHoldSeconds = 120f;
        /// <summary>호스트 종료 예고 시간(초)</summary>
        public float ShutdownNoticeSeconds = 30f;
        public int PasswordMaxLength = 32;
        /// <summary>비밀번호 시도 상한(1분당 횟수)</summary>
        public int PasswordAttemptsPerMinute = 5;
        public int WorldNameMaxLength = 32;
        public int ChatMaxLength = 200;
        /// <summary>채팅 빈도 상한(초당 개수)</summary>
        public int ChatPerSecond = 2;
        public int NoticeLogCount = 100;
        public float PingDurationSeconds = 15f;
        public int PingMaxCount = 3;
        public float NameTagDistanceMeters = 50f;
        public float FovMinDegrees = 70f;
        public float FovMaxDegrees = 110f;
        public float FovDefaultDegrees = 90f;

        public SessionSettings Clone() => (SessionSettings)MemberwiseClone();
    }

    /// <summary>
    /// 월드 생성 검증 기준 [FR-168, FR-170, NFR-055]. 생성 결과에 영향을 주는 값은
    /// 뒤에 GenProfile(생성기 버전에 묶임)로 옮겨질 수 있다(M1-04에서 확정).
    /// </summary>
    [Serializable]
    public sealed class WorldGenRuleSettings
    {
        /// <summary>성능 측정 재현용 기준 시드</summary>
        public string BenchSeedText = "BENCH-0001";
        /// <summary>지역 경계 최대 단차(m)</summary>
        public float MaxBoundaryStepMeters = 2f;
        /// <summary>단차를 재는 경계 구간 폭(m)</summary>
        public float BoundaryBlendMeters = 1f;
        public float MaxWalkableSlopeDegrees = 35f;
        public int MinTowerSlotsForest = 6;
        public int MinTowerSlotsOther = 8;
        /// <summary>진행 재료 최소 수량 배율(필요량의 몇 배)</summary>
        public float ResourceSafetyMultiplier = 1.5f;
        public int ValidationSeedCount = 1000;
        public int ReleaseValidationSeedCount = 10000;

        public WorldGenRuleSettings Clone() => (WorldGenRuleSettings)MemberwiseClone();
    }

    /// <summary>난이도 프리셋 배율 [FR-156]</summary>
    [Serializable]
    public sealed class DifficultySettings
    {
        public float EasyMultiplier = 0.7f;
        public float NormalMultiplier = 1.0f;
        public float HardMultiplier = 1.4f;

        public DifficultySettings Clone() => (DifficultySettings)MemberwiseClone();
    }

    /// <summary>숙련도 [FR-100]. 행동 경험치·레벨업 필요 경험치는 미정(C-06)이라 값이 정해질 때 추가한다.</summary>
    [Serializable]
    public sealed class ProgressionSettings
    {
        /// <summary>레벨당 스탯 포인트</summary>
        public int StatPointsPerLevel = 1;

        public ProgressionSettings Clone() => (ProgressionSettings)MemberwiseClone();
    }

    /// <summary>
    /// 게임 균형 설정값 전체. 섹션별 클래스로 나눠 두었고, 초기값이 곧 SRS 부록 C.1 후보값이다.
    /// </summary>
    [Serializable]
    public sealed class BalanceValues
    {
        public MovementSettings Movement = new MovementSettings();
        public StaminaSettings Stamina = new StaminaSettings();
        public VitalsSettings Vitals = new VitalsSettings();
        public WorldTimeSettings WorldTime = new WorldTimeSettings();
        public LightSettings Light = new LightSettings();
        public InventorySettings Inventory = new InventorySettings();
        public GatheringSettings Gathering = new GatheringSettings();
        public FurnaceSettings Furnace = new FurnaceSettings();
        public DurabilitySettings Durability = new DurabilitySettings();
        public SaveSettings Save = new SaveSettings();
        public DefenseSettings Defense = new DefenseSettings();
        public CreatureSettings Creatures = new CreatureSettings();
        public FarmingSettings Farming = new FarmingSettings();
        public SessionSettings Session = new SessionSettings();
        public WorldGenRuleSettings WorldGenRules = new WorldGenRuleSettings();
        public DifficultySettings Difficulty = new DifficultySettings();
        public ProgressionSettings Progression = new ProgressionSettings();

        /// <summary>SRS 부록 C.1 후보값으로 채운 새 묶음.</summary>
        public static BalanceValues CreateCandidate() => new BalanceValues();

        /// <summary>
        /// 모든 섹션을 복사한 새 묶음. 로더가 편집 중인 애셋과 실행 중 설정을 분리할 때 쓴다.
        /// 섹션이 비어 있으면(애셋 손상) 후보값으로 채운다.
        /// </summary>
        public BalanceValues Clone()
        {
            return new BalanceValues
            {
                Movement = (Movement ?? new MovementSettings()).Clone(),
                Stamina = (Stamina ?? new StaminaSettings()).Clone(),
                Vitals = (Vitals ?? new VitalsSettings()).Clone(),
                WorldTime = (WorldTime ?? new WorldTimeSettings()).Clone(),
                Light = (Light ?? new LightSettings()).Clone(),
                Inventory = (Inventory ?? new InventorySettings()).Clone(),
                Gathering = (Gathering ?? new GatheringSettings()).Clone(),
                Furnace = (Furnace ?? new FurnaceSettings()).Clone(),
                Durability = (Durability ?? new DurabilitySettings()).Clone(),
                Save = (Save ?? new SaveSettings()).Clone(),
                Defense = (Defense ?? new DefenseSettings()).Clone(),
                Creatures = (Creatures ?? new CreatureSettings()).Clone(),
                Farming = (Farming ?? new FarmingSettings()).Clone(),
                Session = (Session ?? new SessionSettings()).Clone(),
                WorldGenRules = (WorldGenRules ?? new WorldGenRuleSettings()).Clone(),
                Difficulty = (Difficulty ?? new DifficultySettings()).Clone(),
                Progression = (Progression ?? new ProgressionSettings()).Clone(),
            };
        }
    }
}

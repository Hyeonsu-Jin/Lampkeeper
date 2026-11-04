// =============================================================
// 파일: ConfigValidator.cs
// 역할: 균형 설정값(BalanceValues)과 네트워크 튜닝(NetworkTuning)의 필수 값·범위·관계를 검사한다.
// 레이어: application (순수 C#)
// 관련: NFR-033, DES-104 / SDD 3.7.7 ("검증: 필수 값, 범위, 참조 무결성")
//
// 검사는 두 겹이다.
//  1) 모든 숫자 필드: 유한한 수(NaN/무한대 아님)이고 0 이상이어야 한다(반사로 전부 검사).
//  2) 필드별 규칙: 0보다 커야 하는 값, 비율은 0~1, 최소 <= 최대 같은 관계.
// =============================================================
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Game.Application.Config
{
    /// <summary>설정값 검증기. 상태가 없는 정적 클래스.</summary>
    public static class ConfigValidator
    {
        /// <summary>
        /// 균형 설정값을 검사하고 문제를 issues에 추가한다.
        /// </summary>
        /// <param name="balance">검사할 값(null이면 오류 1건)</param>
        /// <param name="issues">문제를 모을 목록</param>
        public static void ValidateBalance(BalanceValues balance, List<ConfigIssue> issues)
        {
            if (balance == null)
            {
                issues.Add(ConfigIssue.Error("Balance", "균형 설정값이 없습니다."));
                return;
            }

            // 1) 섹션이 비어 있으면(애셋 손상) 아래 검사가 의미 없으므로 바로 알린다.
            foreach (FieldInfo section in typeof(BalanceValues).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object sectionValue = section.GetValue(balance);
                if (sectionValue == null)
                {
                    issues.Add(ConfigIssue.Error("Balance." + section.Name, "섹션이 비어 있습니다."));
                    continue;
                }
                CheckNumericFields(sectionValue, "Balance." + section.Name, issues);
            }

            if (issues.Exists(i => i.Severity == ConfigIssueSeverity.Error && i.Path.StartsWith("Balance.", StringComparison.Ordinal)))
            {
                // 형식 오류가 있으면 필드 규칙은 건너뛴다(null 참조 방지).
                return;
            }

            ValidateRules(balance, issues);
        }

        /// <summary>
        /// 네트워크 튜닝을 검사한다.
        /// </summary>
        public static void ValidateNetwork(NetworkTuning network, List<ConfigIssue> issues)
        {
            if (network == null)
            {
                issues.Add(ConfigIssue.Error("Network", "네트워크 튜닝 값이 없습니다."));
                return;
            }

            CheckNumericFields(network, "Network", issues);
            Require(issues, network.ServerTickRateHz >= 1 && network.ServerTickRateHz <= 240,
                "Network.ServerTickRateHz", "1~240 사이여야 합니다.");
            Require(issues, network.AiDecisionHz >= 1, "Network.AiDecisionHz", "1 이상이어야 합니다.");
            Require(issues, network.AiSteeringHz >= 1, "Network.AiSteeringHz", "1 이상이어야 합니다.");
            Require(issues, network.AiSteeringLodHz >= 1 && network.AiSteeringLodHz <= network.AiSteeringHz,
                "Network.AiSteeringLodHz", "1 이상이고 AiSteeringHz 이하여야 합니다.");
            Require(issues, network.FlowFieldRaidHz >= 1, "Network.FlowFieldRaidHz", "1 이상이어야 합니다.");
            Require(issues, network.AvatarSendHz >= 1, "Network.AvatarSendHz", "1 이상이어야 합니다.");
            Require(issues, network.CreatureSnapshotFarHz >= 1
                            && network.CreatureSnapshotFarHz <= network.CreatureSnapshotMidHz
                            && network.CreatureSnapshotMidHz <= network.CreatureSnapshotNearHz,
                "Network.CreatureSnapshot*Hz", "먼 <= 중간 <= 가까운 순서이고 1 이상이어야 합니다.");
            Require(issues, network.ClockSyncSeconds > 0f, "Network.ClockSyncSeconds", "0보다 커야 합니다.");
            Require(issues, network.InterestRangeMeters > 0f, "Network.InterestRangeMeters", "0보다 커야 합니다.");
            Require(issues, network.JoinTimeoutSeconds > 0f, "Network.JoinTimeoutSeconds", "0보다 커야 합니다.");
            Require(issues, network.MaxCommandsPerTick >= 1, "Network.MaxCommandsPerTick", "1 이상이어야 합니다.");
            Require(issues, network.MaxQueuedCommands >= network.MaxCommandsPerTick, "Network.MaxQueuedCommands", "MaxCommandsPerTick 이상이어야 합니다.");
            Require(issues, network.SnapshotBytesPerSecond > 0, "Network.SnapshotBytesPerSecond", "0보다 커야 합니다.");
        }

        // ---------------------------------------------------------
        // 내부: 필드별 규칙
        // ---------------------------------------------------------
        private static void ValidateRules(BalanceValues b, List<ConfigIssue> issues)
        {
            // [FR-023, FR-024] 이동
            Require(issues, b.Movement.WalkSpeedMps > 0f, "Balance.Movement.WalkSpeedMps", "0보다 커야 합니다.");
            Require(issues, b.Movement.RunSpeedMps >= b.Movement.WalkSpeedMps, "Balance.Movement.RunSpeedMps", "걷기 속도 이상이어야 합니다.");
            Require(issues, b.Movement.InteractionDistanceMeters > 0f, "Balance.Movement.InteractionDistanceMeters", "0보다 커야 합니다.");
            Require(issues, b.Movement.MoveTolerance >= 1f, "Balance.Movement.MoveTolerance", "1 이상이어야 합니다.");
            Require(issues, b.Movement.MoveSlackMeters >= 0f, "Balance.Movement.MoveSlackMeters", "0 이상이어야 합니다.");
            Require(issues, b.Movement.MoveStaminaGraceSeconds >= 0f, "Balance.Movement.MoveStaminaGraceSeconds", "0 이상이어야 합니다.");
            Require(issues, b.Movement.MaxPoseDeltaSeconds > 0f, "Balance.Movement.MaxPoseDeltaSeconds", "0보다 커야 합니다.");
            Require(issues, b.Movement.MaxVerticalSpeedMps > 0f, "Balance.Movement.MaxVerticalSpeedMps", "0보다 커야 합니다.");

            // [FR-024, FR-025] 스태미나
            Require(issues, b.Stamina.Max > 0f, "Balance.Stamina.Max", "0보다 커야 합니다.");
            Require(issues, b.Stamina.RegenPerSecond > 0f, "Balance.Stamina.RegenPerSecond", "0보다 커야 합니다.");

            // [FR-026~FR-032] 생존 상태
            Require(issues, b.Vitals.HealthMax > 0f, "Balance.Vitals.HealthMax", "0보다 커야 합니다.");
            Require(issues, b.Vitals.HungerMax > 0f, "Balance.Vitals.HungerMax", "0보다 커야 합니다.");
            Require(issues, b.Vitals.ThirstMax > 0f, "Balance.Vitals.ThirstMax", "0보다 커야 합니다.");
            Require(issues, b.Vitals.FatigueMax > 0f, "Balance.Vitals.FatigueMax", "0보다 커야 합니다.");
            Require(issues, b.Vitals.HungerDepleteRealMinutes > 0f, "Balance.Vitals.HungerDepleteRealMinutes", "0보다 커야 합니다.");
            Require(issues, b.Vitals.ThirstDepleteRealMinutes > 0f, "Balance.Vitals.ThirstDepleteRealMinutes", "0보다 커야 합니다.");
            Require(issues, b.Vitals.SleepRecoverRealSeconds > 0f, "Balance.Vitals.SleepRecoverRealSeconds", "0보다 커야 합니다.");
            Require(issues, b.Vitals.ReviveHealthRatio > 0f && b.Vitals.ReviveHealthRatio <= 1f,
                "Balance.Vitals.ReviveHealthRatio", "0 초과 1 이하여야 합니다.");
            Require(issues, b.Vitals.ColdStaminaRegenReduction <= 1f, "Balance.Vitals.ColdStaminaRegenReduction", "1 이하여야 합니다.");

            // [FR-033, FR-034] 하루 길이
            Require(issues, b.WorldTime.DayLengthRealMinutes > 0f, "Balance.WorldTime.DayLengthRealMinutes", "0보다 커야 합니다.");
            Require(issues, b.WorldTime.DaytimeRatio > 0f && b.WorldTime.DaytimeRatio < 1f,
                "Balance.WorldTime.DaytimeRatio", "0과 1 사이(양 끝 제외)여야 합니다.");
            Require(issues, b.WorldTime.DarknessVisibilityMeters > 0f
                            && b.WorldTime.DarknessVisibilityMeters <= b.WorldTime.NightVisibilityMeters,
                "Balance.WorldTime.DarknessVisibilityMeters", "0보다 크고 밤 가시 거리 이하여야 합니다.");

            // [FR-036] 광원
            Require(issues, b.Light.TorchRadiusMeters > 0f, "Balance.Light.TorchRadiusMeters", "0보다 커야 합니다.");
            Require(issues, b.Light.LanternRadiusMeters > 0f, "Balance.Light.LanternRadiusMeters", "0보다 커야 합니다.");
            Require(issues, b.Light.DeviceLanternRadiusMeters > 0f, "Balance.Light.DeviceLanternRadiusMeters", "0보다 커야 합니다.");

            // [FR-038] 인벤토리
            Require(issues, b.Inventory.SlotCount >= 1, "Balance.Inventory.SlotCount", "1 이상이어야 합니다.");
            Require(issues, b.Inventory.ContainerSlotCount >= 1, "Balance.Inventory.ContainerSlotCount", "1 이상이어야 합니다.");
            Require(issues, b.Inventory.MaxStackResource >= 1 && b.Inventory.MaxStackTool >= 1 && b.Inventory.MaxStackFood >= 1,
                "Balance.Inventory.MaxStack*", "묶음 최대는 1 이상이어야 합니다.");

            // [FR-047, FR-049] 채집
            Require(issues, b.Gathering.TreeBasicHits >= 1 && b.Gathering.StoneHits >= 1 && b.Gathering.CoalVeinHits >= 1
                            && b.Gathering.OreBasicHits >= 1 && b.Gathering.OreRareHits >= 1 && b.Gathering.TreeAdvancedMinHits >= 1,
                "Balance.Gathering.*Hits", "타격 횟수는 1 이상이어야 합니다.");
            Require(issues, b.Gathering.TreeAdvancedMinHits <= b.Gathering.TreeAdvancedMaxHits,
                "Balance.Gathering.TreeAdvancedMinHits", "상위 나무 최소 횟수는 최대 이하여야 합니다.");

            // [FR-175~FR-178] 용광로
            Require(issues, b.Furnace.OreProcessFuelCost >= 1, "Balance.Furnace.OreProcessFuelCost", "1 이상이어야 합니다.");
            Require(issues, b.Furnace.ProcessRealSeconds > 0f, "Balance.Furnace.ProcessRealSeconds", "0보다 커야 합니다.");
            Require(issues, b.Furnace.CharcoalWoodCost >= 1 && b.Furnace.CharcoalYield >= 1,
                "Balance.Furnace.Charcoal*", "숯 재료와 결과는 1 이상이어야 합니다.");

            // [FR-135~FR-138] 내구도
            Require(issues, b.Durability.BaseToolDurability >= 1, "Balance.Durability.BaseToolDurability", "1 이상이어야 합니다.");
            Require(issues, b.Durability.LossPerUse >= 1, "Balance.Durability.LossPerUse", "1 이상이어야 합니다.");
            Require(issues, b.Durability.RepairMaterialRatio <= 1f, "Balance.Durability.RepairMaterialRatio", "1 이하여야 합니다.");
            Require(issues, b.Durability.DemolishRefundRatio <= 1f, "Balance.Durability.DemolishRefundRatio", "1 이하여야 합니다.");

            // [FR-018, FR-019] 저장
            Require(issues, b.Save.AutosaveIntervalMinutes > 0f, "Balance.Save.AutosaveIntervalMinutes", "0보다 커야 합니다.");
            Require(issues, b.Save.BackupCount >= 1, "Balance.Save.BackupCount", "1 이상이어야 합니다.");

            // [FR-062~FR-071, NFR-017] 방어전
            Require(issues, b.Defense.WavesTutorial >= 1 && b.Defense.WavesNormal >= 1 && b.Defense.WavesCore >= 1 && b.Defense.WavesFinal >= 1,
                "Balance.Defense.Waves*", "웨이브 수는 1 이상이어야 합니다.");
            Require(issues, b.Defense.WaveIntervalSeconds > 0f, "Balance.Defense.WaveIntervalSeconds", "0보다 커야 합니다.");
            Require(issues, b.Defense.FailMaterialLossRatio <= 1f, "Balance.Defense.FailMaterialLossRatio", "1 이하여야 합니다.");
            Require(issues, b.Defense.MaxConcurrentEnemies >= 1, "Balance.Defense.MaxConcurrentEnemies", "1 이상이어야 합니다.");
            Require(issues, b.Defense.PurifyRadiusNormalMeters > 0f && b.Defense.PurifyRadiusCoreMeters > 0f
                            && b.Defense.PurifyRadiusTutorialMeters > 0f && b.Defense.StartPurifyRadiusMeters > 0f,
                "Balance.Defense.*Radius*", "정화 반경은 0보다 커야 합니다.");

            // [FR-075, FR-077, FR-079, FR-082] 생물
            Require(issues, b.Creatures.AnimalSpawnIntervalMinutes > 0f, "Balance.Creatures.AnimalSpawnIntervalMinutes", "0보다 커야 합니다.");
            Require(issues, b.Creatures.AnimalMaxPerPurifiedZone >= 1, "Balance.Creatures.AnimalMaxPerPurifiedZone", "1 이상이어야 합니다.");
            Require(issues, b.Creatures.DetectionDistanceMeters > 0f, "Balance.Creatures.DetectionDistanceMeters", "0보다 커야 합니다.");

            // [FR-084, FR-085, FR-091] 농사
            Require(issues, b.Farming.GrowMinGameDays > 0f && b.Farming.GrowMinGameDays <= b.Farming.GrowMaxGameDays,
                "Balance.Farming.GrowMinGameDays", "0보다 크고 최대 이하여야 합니다.");
            Require(issues, b.Farming.WaterKeepGameDays > 0f, "Balance.Farming.WaterKeepGameDays", "0보다 커야 합니다.");
            Require(issues, b.Farming.StationRecognitionMeters > 0f, "Balance.Farming.StationRecognitionMeters", "0보다 커야 합니다.");
            Require(issues, b.Farming.BuffDurationMinutes > 0f, "Balance.Farming.BuffDurationMinutes", "0보다 커야 합니다.");

            // [FR-001, NFR-038~041] 세션
            Require(issues, b.Session.MaxPlayers >= 1 && b.Session.MaxPlayers <= 8, "Balance.Session.MaxPlayers", "1~8 사이여야 합니다(최대 8인).");
            Require(issues, b.Session.DisconnectTimeoutSeconds > 0f, "Balance.Session.DisconnectTimeoutSeconds", "0보다 커야 합니다.");
            Require(issues, b.Session.RequestsPerSecondLimit >= 1, "Balance.Session.RequestsPerSecondLimit", "1 이상이어야 합니다.");
            Require(issues, b.Session.ChatMaxLength >= 1 && b.Session.ChatPerSecond >= 1, "Balance.Session.Chat*", "1 이상이어야 합니다.");
            Require(issues, b.Session.PasswordMaxLength >= 1 && b.Session.PasswordAttemptsPerMinute >= 1,
                "Balance.Session.Password*", "1 이상이어야 합니다.");
            Require(issues, b.Session.WorldNameMaxLength >= 1, "Balance.Session.WorldNameMaxLength", "1 이상이어야 합니다.");
            Require(issues, b.Session.FovMinDegrees > 0f && b.Session.FovMinDegrees <= b.Session.FovDefaultDegrees
                            && b.Session.FovDefaultDegrees <= b.Session.FovMaxDegrees && b.Session.FovMaxDegrees < 180f,
                "Balance.Session.Fov*", "0 < 최소 <= 기본 <= 최대 < 180 이어야 합니다.");

            // [FR-168, FR-170, NFR-055] 월드 생성 규칙
            Require(issues, !string.IsNullOrWhiteSpace(b.WorldGenRules.BenchSeedText), "Balance.WorldGenRules.BenchSeedText", "비어 있으면 안 됩니다.");
            Require(issues, b.WorldGenRules.MaxWalkableSlopeDegrees > 0f && b.WorldGenRules.MaxWalkableSlopeDegrees < 90f,
                "Balance.WorldGenRules.MaxWalkableSlopeDegrees", "0과 90 사이여야 합니다.");
            Require(issues, b.WorldGenRules.ResourceSafetyMultiplier >= 1f, "Balance.WorldGenRules.ResourceSafetyMultiplier", "1 이상이어야 합니다.");
            Require(issues, b.WorldGenRules.ValidationSeedCount >= 1 && b.WorldGenRules.ReleaseValidationSeedCount >= b.WorldGenRules.ValidationSeedCount,
                "Balance.WorldGenRules.*ValidationSeedCount", "1 이상이고 출시 전 개수가 일반 개수 이상이어야 합니다.");
            Require(issues, b.WorldGenRules.MaxBoundaryStepMeters > 0f && b.WorldGenRules.BoundaryBlendMeters > 0f,
                "Balance.WorldGenRules.Boundary*", "0보다 커야 합니다.");

            // [FR-156] 난이도
            Require(issues, b.Difficulty.EasyMultiplier > 0f && b.Difficulty.NormalMultiplier > 0f && b.Difficulty.HardMultiplier > 0f,
                "Balance.Difficulty.*Multiplier", "0보다 커야 합니다.");
        }

        /// <summary>조건이 거짓이면 오류를 추가한다.</summary>
        private static void Require(List<ConfigIssue> issues, bool ok, string path, string message)
        {
            if (!ok)
            {
                issues.Add(ConfigIssue.Error(path, message));
            }
        }

        /// <summary>객체의 모든 숫자 필드가 유한하고 0 이상인지 검사한다(반사). 문자열은 건너뛴다.</summary>
        private static void CheckNumericFields(object section, string path, List<ConfigIssue> issues)
        {
            foreach (FieldInfo field in section.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = field.GetValue(section);
                string fieldPath = path + "." + field.Name;
                switch (value)
                {
                    case float f:
                        if (float.IsNaN(f) || float.IsInfinity(f))
                        {
                            issues.Add(ConfigIssue.Error(fieldPath, "유한한 수여야 합니다."));
                        }
                        else if (f < 0f)
                        {
                            issues.Add(ConfigIssue.Error(fieldPath, "0 이상이어야 합니다."));
                        }
                        break;
                    case int i:
                        if (i < 0)
                        {
                            issues.Add(ConfigIssue.Error(fieldPath, "0 이상이어야 합니다."));
                        }
                        break;
                }
            }
        }
    }
}

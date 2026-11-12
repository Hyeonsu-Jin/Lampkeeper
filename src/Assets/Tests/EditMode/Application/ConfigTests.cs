// =============================================================
// 파일: ConfigTests.cs
// 역할: 설정 데이터 기반(M1-03) 시험: 후보값, 검증, 콘텐츠 목록(키 정렬·ID·참조), ContentHash, 설정값 변경 반영(NFR-033).
// 레이어: test (EditMode, Game.Tests.Application) - 순수 C#만 사용
// 관련: NFR-033, NFR-034, DES-104, SRS 부록 C.1 / SDD 3.7.7
// =============================================================
using System.Collections.Generic;
using System.Linq;
using Game.Application.Config;
using Game.Domain.Common;
using NUnit.Framework;

namespace Game.Tests.Application
{
    public class ConfigTests
    {
        // ---------- 도우미 ----------
        private static ContentDefinitionData Item(string key, params string[] refs) =>
            new ContentDefinitionData(key, ContentKind.Item, "text." + key.Replace('.', '_'), null, refs);

        private static ConfigProvider Assemble(BalanceValues balance = null, IEnumerable<ContentDefinitionData> defs = null) =>
            ConfigAssembler.Assemble(balance ?? BalanceValues.CreateCandidate(), new NetworkTuning(), defs);

        private static List<ConfigIssue> ValidateBalance(BalanceValues b)
        {
            var issues = new List<ConfigIssue>();
            ConfigValidator.ValidateBalance(b, issues);
            return issues;
        }

        // ---------- 후보값 (SRS 부록 C.1, C-33) ----------
        [Test]
        public void Candidate_MatchesSrsAppendixC1_Samples()
        {
            BalanceValues b = BalanceValues.CreateCandidate();
            Assert.AreEqual(2.5f, b.Movement.InteractionDistanceMeters);
            Assert.AreEqual(4f, b.Movement.WalkSpeedMps);
            Assert.AreEqual(7f, b.Movement.RunSpeedMps);
            Assert.AreEqual(100f, b.Stamina.Max);
            Assert.AreEqual(10f, b.Stamina.RunCostPerSecond);
            Assert.AreEqual(1f, b.Stamina.RegenDelaySeconds);
            Assert.AreEqual(20f, b.Stamina.RegenPerSecond);
            Assert.AreEqual(30f, b.WorldTime.DayLengthRealMinutes);
            Assert.AreEqual(0.65f, b.WorldTime.DaytimeRatio);
            Assert.AreEqual(32, b.Inventory.SlotCount);
            Assert.AreEqual(50, b.Inventory.MaxStackResource);
            Assert.AreEqual(10f, b.Save.AutosaveIntervalMinutes);
            Assert.AreEqual(5, b.Save.BackupCount);
            Assert.AreEqual(60, b.Defense.MaxConcurrentEnemies);
            Assert.AreEqual(10f, b.Session.DisconnectTimeoutSeconds);
            Assert.AreEqual(30, b.Session.RequestsPerSecondLimit);
            Assert.AreEqual(8, b.Session.MaxPlayers);
            Assert.AreEqual("BENCH-0001", b.WorldGenRules.BenchSeedText);
            Assert.AreEqual(35f, b.WorldGenRules.MaxWalkableSlopeDegrees);
        }

        [Test]
        public void NetworkTuning_Defaults_MatchSdd3121()
        {
            var n = new NetworkTuning();
            Assert.AreEqual(30, n.ServerTickRateHz);
            Assert.AreEqual(10, n.AiDecisionHz);
            Assert.AreEqual(300f, n.InterestRangeMeters);
            Assert.AreEqual(5f, n.ClockSyncSeconds);
        }

        [Test]
        public void Candidate_PassesValidation()
        {
            Assert.IsEmpty(ValidateBalance(BalanceValues.CreateCandidate()));
            var issues = new List<ConfigIssue>();
            ConfigValidator.ValidateNetwork(new NetworkTuning(), issues);
            Assert.IsEmpty(issues);
        }

        // ---------- 검증 ----------
        [Test]
        public void Validate_NullBalance_IsError()
        {
            Assert.IsTrue(ValidateBalance(null).Any(i => i.Severity == ConfigIssueSeverity.Error));
        }

        [Test]
        public void Validate_NegativeValue_IsError_WithPath()
        {
            BalanceValues b = BalanceValues.CreateCandidate();
            b.Stamina.RunCostPerSecond = -1f;
            List<ConfigIssue> issues = ValidateBalance(b);
            Assert.IsTrue(issues.Any(i => i.Path == "Balance.Stamina.RunCostPerSecond"));
        }

        [Test]
        public void Validate_NaN_IsError()
        {
            BalanceValues b = BalanceValues.CreateCandidate();
            b.Movement.WalkSpeedMps = float.NaN;
            Assert.IsTrue(ValidateBalance(b).Any(i => i.Path == "Balance.Movement.WalkSpeedMps"));
        }

        [Test]
        public void Validate_RunSlowerThanWalk_IsError()
        {
            BalanceValues b = BalanceValues.CreateCandidate();
            b.Movement.RunSpeedMps = 3f;
            Assert.IsTrue(ValidateBalance(b).Any(i => i.Path == "Balance.Movement.RunSpeedMps"));
        }

        [Test]
        public void Validate_MoveCheckConstants_MustBePositive_AndPoseHzNotBelowSendHz()
        {
            // [R-47] 이동 검사 상수: 0 이하 거부, 수신 상한이 송신 주기보다 낮으면 거부.
            BalanceValues b = BalanceValues.CreateCandidate();
            b.Movement.MaxPoseHz = 0f;
            Assert.IsTrue(ValidateBalance(b).Any(i => i.Path == "Balance.Movement.MaxPoseHz"));
            b = BalanceValues.CreateCandidate();
            b.Movement.PoseBurstCapacity = 0;
            Assert.IsTrue(ValidateBalance(b).Any(i => i.Path == "Balance.Movement.PoseBurstCapacity"));
            b = BalanceValues.CreateCandidate();
            b.Movement.SpawnRadiusMeters = 0f;
            Assert.IsTrue(ValidateBalance(b).Any(i => i.Path == "Balance.Movement.SpawnRadiusMeters"));
            b = BalanceValues.CreateCandidate();
            b.Movement.SpawnHeightToleranceMeters = -1f;
            Assert.IsTrue(ValidateBalance(b).Any(i => i.Path == "Balance.Movement.SpawnHeightToleranceMeters"));

            var issues = new List<ConfigIssue>();
            var net = new NetworkTuning { AvatarSendHz = 60 };
            ConfigValidator.ValidateMovementAgainstNetwork(BalanceValues.CreateCandidate(), net, issues);
            Assert.IsTrue(issues.Any(i => i.Path == "Balance.Movement.MaxPoseHz"), "45 < 60");
            issues.Clear();
            ConfigValidator.ValidateMovementAgainstNetwork(BalanceValues.CreateCandidate(), new NetworkTuning(), issues);
            Assert.IsEmpty(issues);
        }

        [Test]
        public void Validate_DaytimeRatio_MustBeBetweenZeroAndOneExclusive()
        {
            BalanceValues b = BalanceValues.CreateCandidate();
            b.WorldTime.DaytimeRatio = 1f;
            Assert.IsTrue(ValidateBalance(b).Any(i => i.Path == "Balance.WorldTime.DaytimeRatio"));
            b.WorldTime.DaytimeRatio = 0f;
            Assert.IsTrue(ValidateBalance(b).Any(i => i.Path == "Balance.WorldTime.DaytimeRatio"));
        }

        [Test]
        public void Validate_MaxPlayers_OverEight_IsError()
        {
            BalanceValues b = BalanceValues.CreateCandidate();
            b.Session.MaxPlayers = 9;
            Assert.IsTrue(ValidateBalance(b).Any(i => i.Path == "Balance.Session.MaxPlayers"));
        }

        [Test]
        public void Validate_TreeMinHitsAboveMax_IsError()
        {
            BalanceValues b = BalanceValues.CreateCandidate();
            b.Gathering.TreeAdvancedMinHits = 13;
            Assert.IsTrue(ValidateBalance(b).Any(i => i.Path == "Balance.Gathering.TreeAdvancedMinHits"));
        }

        [Test]
        public void Validate_MissingSection_IsError_NotCrash()
        {
            BalanceValues b = BalanceValues.CreateCandidate();
            b.Furnace = null;
            Assert.IsTrue(ValidateBalance(b).Any(i => i.Path == "Balance.Furnace"));
        }

        [Test]
        public void Validate_FatigueDepleteZero_IsAllowed_ForM2M3Testing()
        {
            // SRS 3.2: M2~M3 시험에서는 피로도 감소 속도를 0으로 둔다.
            BalanceValues b = BalanceValues.CreateCandidate();
            b.Vitals.FatigueDepleteGameDays = 0f;
            Assert.IsEmpty(ValidateBalance(b));
        }

        [Test]
        public void ValidateNetwork_BadTick_IsError()
        {
            var n = new NetworkTuning { ServerTickRateHz = 0 };
            var issues = new List<ConfigIssue>();
            ConfigValidator.ValidateNetwork(n, issues);
            Assert.IsTrue(issues.Any(i => i.Path == "Network.ServerTickRateHz"));
        }

        // ---------- 콘텐츠 목록 ----------
        [Test]
        public void Catalog_AssignsIdsByOrdinalKeyOrder_IndependentOfInputOrder()
        {
            ConfigProvider a = Assemble(null, new[] { Item("item.wood"), Item("item.stone"), Item("item.ash") });
            ConfigProvider b = Assemble(null, new[] { Item("item.ash"), Item("item.wood"), Item("item.stone") });

            Assert.AreEqual(new ContentId(1), IdOf(a, "item.ash"));
            Assert.AreEqual(new ContentId(2), IdOf(a, "item.stone"));
            Assert.AreEqual(new ContentId(3), IdOf(a, "item.wood"));
            Assert.AreEqual(IdOf(a, "item.wood"), IdOf(b, "item.wood"));
            Assert.AreEqual(a.ContentHash, b.ContentHash);
        }

        private static ContentId IdOf(ConfigProvider p, string key)
        {
            Assert.IsTrue(p.Content.TryGetId(key, out ContentId id), key);
            return id;
        }

        [Test]
        public void Catalog_LookupById_RoundTrips()
        {
            ConfigProvider p = Assemble(null, new[] { Item("item.wood"), Item("item.ash") });
            Assert.IsTrue(p.Content.TryGetEntry(new ContentId(2), out ContentEntry entry));
            Assert.AreEqual("item.wood", entry.Key);
            Assert.IsFalse(p.Content.TryGetEntry(ContentId.None, out _));
            Assert.IsFalse(p.Content.TryGetEntry(new ContentId(3), out _));
            Assert.IsFalse(p.Content.TryGetId("item.nothing", out _));
        }

        [Test]
        public void Catalog_DuplicateKey_Throws()
        {
            ConfigLoadException ex = Assert.Throws<ConfigLoadException>(() => Assemble(null, new[] { Item("item.wood"), Item("item.wood") }));
            Assert.IsTrue(ex.Issues.Any(i => i.Message.Contains("중복")));
        }

        [TestCase("")]
        [TestCase("wood")]
        [TestCase("Item.wood")]
        [TestCase("item.Wood")]
        [TestCase("item.")]
        [TestCase("item. wood")]
        public void Catalog_BadKeyFormat_Throws(string key)
        {
            Assert.Throws<ConfigLoadException>(() => Assemble(null, new[] { Item(key) }));
        }

        [Test]
        public void Catalog_KeyPrefixMustMatchKind_Throws()
        {
            var wrong = new ContentDefinitionData("recipe.axe", ContentKind.Item, "text.axe");
            Assert.Throws<ConfigLoadException>(() => Assemble(null, new[] { wrong }));
        }

        [Test]
        public void Catalog_EmptyNameKey_Throws()
        {
            // [NFR-034] 화면 이름은 텍스트 키만 가진다.
            var noName = new ContentDefinitionData("item.wood", ContentKind.Item, "");
            Assert.Throws<ConfigLoadException>(() => Assemble(null, new[] { noName }));
        }

        [Test]
        public void Catalog_DuplicatePropertyName_Throws()
        {
            var props = new[] { new ContentProperty("hits", "5"), new ContentProperty("hits", "6") };
            var def = new ContentDefinitionData("item.wood", ContentKind.Item, "text.wood", props);
            Assert.Throws<ConfigLoadException>(() => Assemble(null, new[] { def }));
        }

        [Test]
        public void Catalog_UnknownReference_Throws()
        {
            ConfigLoadException ex = Assert.Throws<ConfigLoadException>(() => Assemble(null, new[] { Item("item.axe", "item.handle") }));
            Assert.IsTrue(ex.Issues.Any(i => i.Message.Contains("item.handle")));
        }

        [Test]
        public void Catalog_ValidReference_Passes()
        {
            ConfigProvider p = Assemble(null, new[] { Item("item.axe", "item.handle"), Item("item.handle") });
            Assert.AreEqual(2, p.Content.Count);
        }

        [Test]
        public void Assemble_EmptyContent_IsAllowed_WithWarning()
        {
            ConfigProvider p = Assemble();
            Assert.AreEqual(0, p.Content.Count);
            Assert.IsTrue(p.Warnings.Any(w => w.Severity == ConfigIssueSeverity.Warning));
        }

        [Test]
        public void Assemble_InvalidBalance_Throws_AndCollectsAllErrors()
        {
            BalanceValues b = BalanceValues.CreateCandidate();
            b.Stamina.Max = 0f;
            b.Light.TorchRadiusMeters = 0f;
            ConfigLoadException ex = Assert.Throws<ConfigLoadException>(() => Assemble(b));
            Assert.GreaterOrEqual(ex.Issues.Count(i => i.Severity == ConfigIssueSeverity.Error), 2);
        }

        // ---------- ContentHash ----------
        [Test]
        public void Hash_IsStable_ForSameData()
        {
            ConfigProvider a = Assemble(null, new[] { Item("item.wood") });
            ConfigProvider b = Assemble(null, new[] { Item("item.wood") });
            Assert.AreEqual(a.ContentHash, b.ContentHash);
            Assert.AreEqual(16, a.ContentHash.ToString().Length);
        }

        [Test]
        public void Hash_Changes_WhenBalanceChanges()
        {
            BalanceValues changed = BalanceValues.CreateCandidate();
            changed.Movement.WalkSpeedMps = 4.5f;
            Assert.AreNotEqual(Assemble().ContentHash, Assemble(changed).ContentHash);
        }

        [Test]
        public void Hash_Changes_WhenStringBalanceChanges()
        {
            BalanceValues changed = BalanceValues.CreateCandidate();
            changed.WorldGenRules.BenchSeedText = "BENCH-0002";
            Assert.AreNotEqual(Assemble().ContentHash, Assemble(changed).ContentHash);
        }

        [Test]
        public void Hash_Changes_WhenContentPropertyChanges()
        {
            var one = new ContentDefinitionData("item.wood", ContentKind.Item, "text.wood", new[] { new ContentProperty("stack", "50") });
            var two = new ContentDefinitionData("item.wood", ContentKind.Item, "text.wood", new[] { new ContentProperty("stack", "40") });
            Assert.AreNotEqual(Assemble(null, new[] { one }).ContentHash, Assemble(null, new[] { two }).ContentHash);
        }

        [Test]
        public void Hash_IgnoresPropertyOrder_ButNotKeys()
        {
            var ab = new ContentDefinitionData("item.x", ContentKind.Item, "t.x", new[] { new ContentProperty("a", "1"), new ContentProperty("b", "2") });
            var ba = new ContentDefinitionData("item.x", ContentKind.Item, "t.x", new[] { new ContentProperty("b", "2"), new ContentProperty("a", "1") });
            Assert.AreEqual(Assemble(null, new[] { ab }).ContentHash, Assemble(null, new[] { ba }).ContentHash);
            Assert.AreNotEqual(Assemble(null, new[] { Item("item.x") }).ContentHash, Assemble(null, new[] { Item("item.y") }).ContentHash);
        }

        [Test]
        public void Hash_DoesNotDependOnNetworkTuning()
        {
            // 네트워크 튜닝은 호스트 로컬 값이라 접속 비교에 넣지 않는다.
            ConfigProvider a = ConfigAssembler.Assemble(BalanceValues.CreateCandidate(), new NetworkTuning { ServerTickRateHz = 30 }, null);
            ConfigProvider b = ConfigAssembler.Assemble(BalanceValues.CreateCandidate(), new NetworkTuning { ServerTickRateHz = 60 }, null);
            Assert.AreEqual(a.ContentHash, b.ContentHash);
        }

        [Test]
        public void CanonicalText_CoversEveryBalanceField()
        {
            // 새 필드를 추가하고 해시에 빠뜨리는 사고를 막는다: 모든 공개 필드가 정규화 글자에 한 줄씩 있어야 한다.
            string text = ContentHasher.BuildCanonicalText(BalanceValues.CreateCandidate(), ContentCatalog.Empty);
            int expected = 0;
            foreach (var section in typeof(BalanceValues).GetFields())
            {
                expected += section.FieldType.GetFields().Length;
            }
            int lines = text.Split('\n').Count(l => l.StartsWith("balance.", System.StringComparison.Ordinal));
            Assert.AreEqual(expected, lines);
        }

        // ---------- 설정값 변경이 동작에 반영 (NFR-033 표본 10개 이상) ----------
        [Test]
        public void ConfigChange_IsReflectedInProvider_ForTenSampleValues()
        {
            BalanceValues b = BalanceValues.CreateCandidate();
            b.Movement.WalkSpeedMps = 5f;
            b.Movement.InteractionDistanceMeters = 3f;
            b.Stamina.Max = 120f;
            b.Vitals.HealthMax = 150f;
            b.WorldTime.DayLengthRealMinutes = 20f;
            b.Inventory.SlotCount = 40;
            b.Gathering.TreeBasicHits = 3;
            b.Save.AutosaveIntervalMinutes = 5f;
            b.Defense.WaveIntervalSeconds = 45f;
            b.Session.DisconnectTimeoutSeconds = 15f;
            b.Light.TorchRadiusMeters = 9f;

            IConfigProviderProbe probe = new IConfigProviderProbe(Assemble(b));
            Assert.AreEqual(11, probe.Count(
                5f, 3f, 120f, 150f, 20f, 40, 3, 5f, 45f, 15f, 9f));
        }

        /// <summary>제공자에서 값을 읽어 기대값과 맞는 개수를 센다(가짜 IConfigProvider 역할).</summary>
        private sealed class IConfigProviderProbe
        {
            private readonly Game.Application.Ports.IConfigProvider _p;
            public IConfigProviderProbe(Game.Application.Ports.IConfigProvider p) { _p = p; }

            public int Count(float walk, float interact, float stamina, float health, float day, int slots, int tree, float autosave, float wave, float timeout, float torch)
            {
                int n = 0;
                if (_p.Balance.Movement.WalkSpeedMps == walk) n++;
                if (_p.Balance.Movement.InteractionDistanceMeters == interact) n++;
                if (_p.Balance.Stamina.Max == stamina) n++;
                if (_p.Balance.Vitals.HealthMax == health) n++;
                if (_p.Balance.WorldTime.DayLengthRealMinutes == day) n++;
                if (_p.Balance.Inventory.SlotCount == slots) n++;
                if (_p.Balance.Gathering.TreeBasicHits == tree) n++;
                if (_p.Balance.Save.AutosaveIntervalMinutes == autosave) n++;
                if (_p.Balance.Defense.WaveIntervalSeconds == wave) n++;
                if (_p.Balance.Session.DisconnectTimeoutSeconds == timeout) n++;
                if (_p.Balance.Light.TorchRadiusMeters == torch) n++;
                return n;
            }
        }

        [Test]
        public void Provider_ReturnsCopies_EditingSourceAfterAssembleDoesNotChangeIt()
        {
            BalanceValues source = BalanceValues.CreateCandidate();
            ConfigProvider p = Assemble(source);
            source.Movement.WalkSpeedMps = 99f;
            Assert.AreEqual(4f, p.Balance.Movement.WalkSpeedMps);
        }
    }
}

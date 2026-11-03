// =============================================================
// 파일: ConfigLoaderTests.cs
// 역할: 설정 애셋 로더(ConfigLoader)와 실제 Data 애셋 시험.
// 레이어: test (EditMode, Game.Tests.Infrastructure)
// 관련: NFR-033, NFR-034, DES-104 / SDD 3.7.7
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Config;
using Game.Infra.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Infrastructure
{
    public class ConfigLoaderTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        private T Make<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _created.Add(instance);
            return instance;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object o in _created)
            {
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
            _created.Clear();
        }

        private GameDataConfig MakeRoot()
        {
            GameDataConfig root = Make<GameDataConfig>();
            root.Balance = Make<BalanceConfig>();
            root.Network = Make<NetworkTuningConfig>();
            return root;
        }

        private ContentDef MakeDef(string key, ContentKind kind, string nameKey)
        {
            ContentDef def = Make<ContentDef>();
            def.Key = key;
            def.Kind = kind;
            def.NameKey = nameKey;
            return def;
        }

        [Test]
        public void Load_NullRoot_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ConfigLoader.Load(null));
        }

        [Test]
        public void Load_DefaultAssets_GivesCandidateValues()
        {
            ConfigProvider p = ConfigLoader.Load(MakeRoot());
            Assert.AreEqual(4f, p.Balance.Movement.WalkSpeedMps);
            Assert.AreEqual(30, p.Network.ServerTickRateHz);
        }

        [Test]
        public void Load_MissingBalance_ThrowsWithClearPath()
        {
            GameDataConfig root = MakeRoot();
            root.Balance = null;
            ConfigLoadException ex = Assert.Throws<ConfigLoadException>(() => ConfigLoader.Load(root));
            StringAssert.Contains("GameDataConfig.Balance", ex.Message);
        }

        [Test]
        public void Load_MissingNetwork_Throws()
        {
            GameDataConfig root = MakeRoot();
            root.Network = null;
            Assert.Throws<ConfigLoadException>(() => ConfigLoader.Load(root));
        }

        [Test]
        public void Load_EmptyContentSlot_Throws()
        {
            GameDataConfig root = MakeRoot();
            root.Contents.Add(null);
            Assert.Throws<ConfigLoadException>(() => ConfigLoader.Load(root));
        }

        [Test]
        public void Load_ContentDefs_AreSortedAndHashed()
        {
            GameDataConfig root = MakeRoot();
            root.Contents.Add(MakeDef("item.wood", ContentKind.Item, "item.wood.name"));
            root.Contents.Add(MakeDef("item.ash", ContentKind.Item, "item.ash.name"));

            ConfigProvider p = ConfigLoader.Load(root);
            Assert.AreEqual(2, p.Content.Count);
            Assert.IsTrue(p.Content.TryGetId("item.ash", out var ash));
            Assert.AreEqual(1, ash.Value);
        }

        [Test]
        public void Load_ContentProperties_ReachTheHash()
        {
            GameDataConfig a = MakeRoot();
            ContentDef defA = MakeDef("item.wood", ContentKind.Item, "item.wood.name");
            defA.Properties.Add(new ContentPropertyEntry { Name = "stack", Value = "50" });
            a.Contents.Add(defA);

            GameDataConfig b = MakeRoot();
            ContentDef defB = MakeDef("item.wood", ContentKind.Item, "item.wood.name");
            defB.Properties.Add(new ContentPropertyEntry { Name = "stack", Value = "49" });
            b.Contents.Add(defB);

            Assert.AreNotEqual(ConfigLoader.Load(a).ContentHash, ConfigLoader.Load(b).ContentHash);
        }

        [Test]
        public void Load_BalanceEditedInAsset_ChangesProviderAndHash()
        {
            // NFR-033: 코드를 고치지 않고 애셋 값만 바꿔도 동작 값이 바뀐다.
            GameDataConfig root = MakeRoot();
            ConfigProvider before = ConfigLoader.Load(root);
            root.Balance.Values.Stamina.RunCostPerSecond = 20f;
            ConfigProvider after = ConfigLoader.Load(root);

            Assert.AreEqual(20f, after.Balance.Stamina.RunCostPerSecond);
            Assert.AreNotEqual(before.ContentHash, after.ContentHash);
        }

        [Test]
        public void ShippedAssets_AreValid_AndLinked()
        {
            // 저장소에 들어 있는 실제 애셋(Assets/_Game/Data)이 검증을 통과해야 한다.
            var root = AssetDatabase.LoadAssetAtPath<GameDataConfig>(Game.Editor.DataAssetBuilder.RootPath);
            Assert.IsNotNull(root, "GameDataConfig 애셋이 없습니다. Game/Setup/Run All을 실행하세요.");
            ConfigProvider p = ConfigLoader.Load(root);
            // 에디터(Mono)와 IL2CPP 빌드의 ContentHash가 같은지 눈으로 비교할 수 있게 값을 남긴다.
            Debug.Log("[Test] ShippedAssets ContentHash=" + p.ContentHash);
            Assert.AreEqual(BalanceValues.CreateCandidate().Movement.RunSpeedMps, p.Balance.Movement.RunSpeedMps);
        }
    }
}

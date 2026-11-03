// =============================================================
// 파일: TransportSelectorTests.cs
// 역할: TransportSelector가 FishNet NetworkManager에 알맞은 전송(Tugboat / FishySteamworks)을 연결하고
//       네트워크 튜닝을 적용하는지 시험한다. NetworkManager는 PlayMode에서만 정상 초기화된다.
// 레이어: test (PlayMode, Game.Tests.Integration)
// 관련: FR-006(전송 방식 전환), NFR-031, NFR-049, DES-101 / SDD 3.7.4
// =============================================================
using System.Collections;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Transporting.Tugboat;
using Game.Application.Boot;
using Game.Application.Config;
using Game.Infra.Transport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FishySteamworksTransport = FishySteamworks.FishySteamworks;

namespace Game.Tests.Integration
{
    public class TransportSelectorTests
    {
        private GameObject _host;
        private NetworkManager _networkManager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // NetworkManager는 Awake에서 SpawnablePrefabs를 요구하므로, 꺼 둔 상태에서 값을 넣고 켠다.
            _host = new GameObject("TestNetworkManager");
            _host.SetActive(false);
            // 에디터에서 컴포넌트를 붙일 때 OnValidate가 "SpawnablePrefabs is null" 오류를 한 번 남긴다. 이 구간만 무시한다.
            LogAssert.ignoreFailingMessages = true;
            _networkManager = _host.AddComponent<NetworkManager>();
            _networkManager.SpawnablePrefabs = ScriptableObject.CreateInstance<SinglePrefabObjects>();
            LogAssert.ignoreFailingMessages = false;
            _host.SetActive(true);
            yield return null; // Awake/Start가 돌 때까지 한 프레임 기다린다.
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.DestroyImmediate(_host); // 즉시 지운다: 다음 시험의 NetworkManager가 "이미 있음"으로 스스로 삭제되지 않게
            }
        }

        [Test]
        public void DevBuild_Tugboat_AttachesTugboat()
        {
            var selector = new TransportSelector(_networkManager, true);
            SelectedTransport selected = selector.Apply(TransportKind.Tugboat);

            Assert.AreEqual(TransportKind.Tugboat, selected.Kind);
            Assert.IsInstanceOf<Tugboat>(selected.Component);
            Assert.AreSame(selected.Component, _networkManager.TransportManager.Transport);
        }

        [Test]
        public void DevBuild_NoRequest_DefaultsToSteam()
        {
            var selector = new TransportSelector(_networkManager, true);
            SelectedTransport selected = selector.Apply(null);

            Assert.AreEqual(TransportKind.Steam, selected.Kind);
            Assert.IsInstanceOf<FishySteamworksTransport>(selected.Component);
        }

        [Test]
        public void ReleaseBuild_IgnoresTugboatRequest()
        {
            // [FR-006] 출시 빌드는 Steam 고정
            var selector = new TransportSelector(_networkManager, false);
            SelectedTransport selected = selector.Apply(TransportKind.Tugboat);

            Assert.AreEqual(TransportKind.Steam, selected.Kind);
            Assert.IsInstanceOf<FishySteamworksTransport>(selected.Component);
        }

        [Test]
        public void Apply_Twice_ReusesComponent()
        {
            var selector = new TransportSelector(_networkManager, true);
            SelectedTransport first = selector.Apply(TransportKind.Tugboat);
            SelectedTransport second = selector.Apply(TransportKind.Tugboat);

            Assert.AreSame(first.Component, second.Component);
            Assert.AreEqual(1, _host.GetComponents<Tugboat>().Length);
        }

        [Test]
        public void ApplyTuning_SetsServerTickRate()
        {
            var selector = new TransportSelector(_networkManager, true);
            SelectedTransport selected = selector.Apply(TransportKind.Tugboat);
            selector.ApplyTuning(new NetworkTuning { ServerTickRateHz = 20 }, 10f, selected);

            Assert.AreEqual(20, _networkManager.TimeManager.TickRate);
        }
    }
}

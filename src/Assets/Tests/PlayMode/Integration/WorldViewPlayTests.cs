// =============================================================
// 파일: WorldViewPlayTests.cs
// 역할: 지형 타일 생성(TerrainViewBuilder)과 자원 표시(ResourceViewStreamer)의 동작 시험 - 타일 수, 근거리 풀 재사용, 상태 → 표시 반영, 조준.
// 관련: FR-009, NFR-016, DES-087, DES-089 / SDD 7장 M1-10 (2차)
// =============================================================
using System.Collections;
using Game.Adapters.Presentation.World;
using Game.Application.Replication;
using Game.Application.WorldSync;
using Game.Domain.Gathering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Integration
{
    public sealed class WorldViewPlayTests
    {
        private LocalWorld _local;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _local = LocalWorld.Generate(777UL, LocalWorld.GeneratorV0, WorldScale.Small);
            _root = new GameObject("WorldViewTest");
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_root);
        }

        [Test]
        public void Terrain_BuildsOneTilePerWorldTile_WithCollider()
        {
            var builder = _root.AddComponent<TerrainViewBuilder>();
            builder.BuildFrom(_local);
            Assert.AreEqual(_local.TileCount, builder.TileCount);
            Assert.IsNotNull(_root.GetComponentInChildren<TerrainCollider>());

            // 지형 높이가 생성 격자와 맞는다(스폰 높이가 지면과 같다는 뜻).
            float x = _local.WidthMeters * 0.5f;
            float z = _local.DepthMeters * 0.5f;
            float expected = _local.SurfaceHeightMeters(x, z);
            Terrain tile = _root.GetComponentInChildren<Terrain>();
            Assert.IsNotNull(tile);
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(new Vector3(x, expected + 50f, z), Vector3.down, out RaycastHit hit, 200f));
            Assert.AreEqual(expected, hit.point.y, 0.5f);
        }

        [UnityTest]
        public IEnumerator Streamer_NearObjectsArePooledAndReused_AndStateIsReflected()
        {
            var mirror = new ClientWorldMirror();
            mirror.AttachLocal(_local);
            var streamerObject = new GameObject("Streamer");
            streamerObject.transform.SetParent(_root.transform);
            var streamer = streamerObject.AddComponent<ResourceViewStreamer>();
            var viewer = new GameObject("Viewer").transform;
            viewer.SetParent(_root.transform);

            // 첫 자원 근처에 선다.
            _local.TryGetResource(0, out _, out var p0);
            viewer.position = new Vector3(p0.X, p0.Y + 1.6f, p0.Z);
            streamer.Initialize(mirror, viewer);
            yield return null;
            streamer.RefreshNow();
            Assert.Greater(streamer.NearVisibleCount, 0, "근거리 자원이 그려져야 한다");
            // 두 곳(A, B)을 오가며 풀이 재사용되는지 본다: 한 바퀴 돈 뒤에는 다시 돌아도 오브젝트 수가 늘지 않는다.
            Vector3 posA = new Vector3(p0.X, p0.Y + 1.6f, p0.Z);
            Vector3 posB = new Vector3(p0.X + 300f, p0.Y, p0.Z);
            viewer.position = posB;
            streamer.RefreshNow();
            viewer.position = posA;
            streamer.RefreshNow();
            int pooledAfterRound = streamer.PooledObjectCount;
            viewer.position = posB;
            streamer.RefreshNow();
            viewer.position = posA;
            streamer.RefreshNow();
            Assert.AreEqual(pooledAfterRound, streamer.PooledObjectCount);

            // 전부 부서진 상태로 만들면 근거리·원거리 모두 그려지는 수가 줄어든다.
            int before = streamer.NearVisibleCount + streamer.FarCount;
            ResourceChangeTool.ApplyChanges(mirror.Resources, _local, _local.ResourceCount, 1UL);
            streamer.RefreshNow();
            int after = streamer.NearVisibleCount + streamer.FarCount;
            Assert.Less(after, before);
        }
    }
}

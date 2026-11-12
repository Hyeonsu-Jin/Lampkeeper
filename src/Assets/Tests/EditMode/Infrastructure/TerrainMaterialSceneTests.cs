// =============================================================
// 파일: TerrainMaterialSceneTests.cs
// 역할: World 씬의 TerrainViewBuilder에 HDRP TerrainLit 재질이 연결돼 있는지 확인(빌드에서 바닥이 안 보이던 M1-10 결함의 회귀 시험)
// 레이어: tests (EditMode)
// 관련: FR-168, M1-10
// =============================================================
using Game.Adapters.Presentation.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.Infrastructure
{
    public sealed class TerrainMaterialSceneTests
    {
        private const string WorldScenePath = "Assets/_Game/Scenes/World.unity";
        private const string TerrainLitShaderName = "HDRP/TerrainLit";

        [Test]
        public void WorldScene_TerrainViewBuilder_HasTerrainLitMaterial()
        {
            Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Additive);
            try
            {
                var builder = Object.FindFirstObjectByType<TerrainViewBuilder>();
                Assert.IsNotNull(builder, "World 씬에 TerrainViewBuilder가 없다");
                Assert.IsNotNull(builder.TerrainMaterial, "지형 재질이 연결되지 않았다");
                Assert.AreEqual(TerrainLitShaderName, builder.TerrainMaterial.shader.name);
                // 인스턴싱이 꺼진 재질은 빌드에서 지형이 안 그려진다(INSTANCING_ON 변형 제거)
                Assert.IsTrue(builder.TerrainMaterial.enableInstancing, "지형 재질의 GPU 인스턴싱이 꺼져 있다");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}

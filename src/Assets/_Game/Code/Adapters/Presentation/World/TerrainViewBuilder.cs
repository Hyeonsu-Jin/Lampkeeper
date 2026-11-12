// =============================================================
// 파일: TerrainViewBuilder.cs
// 역할: 월드 생성 결과의 정수 높이 격자(V0)를 Unity Terrain 타일로 만들어 보인다. 타일마다 TerrainData·TerrainCollider가 있다.
// 레이어: adapters (Game.Adapters.Presentation/World)
// 관련: FR-168, NFR-052, NFR-011, DES-089 / SDD 3.2.3, 3.11.4, 3.12.7
//
// M1-10 범위: 높이만 만든다. 재질은 임시 단색 한 장(레이어 1개)이고, 물 표면·스플랫·디테일은 이후 작업이다.
// 격자 간격이 1 m이고 타일이 한 변 2^n m이므로, 타일의 높이 지도(2^n + 1 칸)가 격자와 1:1로 맞고 이웃 타일의 가장자리 값이 같다(틈 없음).
// =============================================================
using System;
using System.Collections.Generic;
using Game.WorldGen.Model;
using UnityEngine;

namespace Game.Adapters.Presentation.World
{
    /// <summary>Terrain 타일 생성기. 월드 하나당 한 번 <see cref="Build"/>를 부르고, 나갈 때 <see cref="Clear"/>(또는 파괴)로 정리한다.</summary>
    public sealed class TerrainViewBuilder : MonoBehaviour
    {
        /// <summary>임시 지면 색(풀밭 느낌의 단색). 정식 재질은 이후 작업에서 교체한다.</summary>
        private static readonly Color PlaceholderGroundColor = new Color(0.30f, 0.42f, 0.24f);

        private const float MillimetersPerMeter = 1000f;

        /// <summary>임시 지면 무늬가 반복되는 크기(m). 단색이라 값은 중요하지 않다.</summary>
        private const float LayerTileSizeMeters = 16f;

        /// <summary>겹쳐 칠하는 지도(알파맵) 해상도. 레이어가 하나뿐이라 가장 작게 둔다(Unity 최소 16).</summary>
        private const int AlphamapResolution = 16;

        /// <summary>높이 범위 여유(m). 0 높이 범위일 때 Terrain 크기가 0이 되지 않게 한다.</summary>
        private const float MinHeightRangeMeters = 1f;

        /// <summary>Terrain 화면 정밀도 오차(픽셀). 값이 클수록 먼 곳 삼각형이 줄어든다.</summary>
        private const float HeightmapPixelError = 8f;

        [SerializeField] private TerrainLayer _groundLayer;

        /// <summary>
        /// Terrain에 쓸 재질(HDRP/TerrainLit). 씬에 연결돼 있어야 빌드에 셰이더가 포함된다.
        /// 실행 중에 만든 Terrain은 빌드가 셰이더를 찾지 못해 바닥이 안 보일 수 있다(M1-10 결함).
        /// </summary>
        [SerializeField] private Material _terrainMaterial;

        /// <summary>연결된 지형 재질(시험·점검용).</summary>
        public Material TerrainMaterial => _terrainMaterial;

        private readonly List<Terrain> _tiles = new List<Terrain>();
        private readonly List<UnityEngine.Object> _ownedAssets = new List<UnityEngine.Object>();

        /// <summary>만들어진 타일 수.</summary>
        public int TileCount => _tiles.Count;

        /// <summary>월드 사본의 생성 결과로 타일을 만든다(<see cref="Build"/>와 같다).</summary>
        public void BuildFrom(Game.Application.WorldSync.LocalWorld local)
        {
            if (local == null) throw new ArgumentNullException(nameof(local));
            Build(local.Generated);
        }

        /// <summary>
        /// 높이 격자로 타일을 만든다. 이미 만든 타일이 있으면 먼저 지운다.
        /// </summary>
        /// <param name="world">생성된 월드(읽기만 한다)</param>
        /// <exception cref="ArgumentNullException">world가 null일 때</exception>
        /// <exception cref="InvalidOperationException">타일 한 변이 2의 거듭제곱 m가 아닐 때(Unity 높이 지도 규칙)</exception>
        public void Build(GeneratedWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Clear();

            if (_terrainMaterial == null)
            {
                Debug.LogWarning("[TerrainViewBuilder] 지형 재질이 비어 있습니다. 빌드에서 바닥이 안 보일 수 있습니다.");
            }

            if (_terrainMaterial != null && !_terrainMaterial.enableInstancing)
            {
                Debug.LogWarning("[TerrainViewBuilder] 지형 재질의 GPU 인스턴싱이 꺼져 있어 drawInstanced를 끕니다.");
            }

            int tile = world.TileSize;
            if (tile <= 0 || (tile & (tile - 1)) != 0)
            {
                throw new InvalidOperationException("타일 크기가 2의 거듭제곱이 아닙니다: " + tile);
            }
            int resolution = tile + 1;

            // 높이를 0~1로 맞추려고 전체 최소·최대를 먼저 구한다(Terrain은 높이를 0~size.y 안의 비율로 저장한다).
            int minMm = int.MaxValue;
            int maxMm = int.MinValue;
            for (int i = 0; i < world.Height.Length; i++)
            {
                int h = world.Height[i];
                if (h < minMm) minMm = h;
                if (h > maxMm) maxMm = h;
            }
            float baseMeters = minMm / MillimetersPerMeter;
            float rangeMeters = Mathf.Max(MinHeightRangeMeters, (maxMm - minMm) / MillimetersPerMeter);
            float tileMeters = tile * (GeneratedWorld.HeightStepMm / MillimetersPerMeter);

            TerrainLayer layer = _groundLayer != null ? _groundLayer : CreatePlaceholderLayer();
            var alphamap = new float[AlphamapResolution, AlphamapResolution, 1];
            for (int z = 0; z < AlphamapResolution; z++)
            {
                for (int x = 0; x < AlphamapResolution; x++)
                {
                    alphamap[z, x, 0] = 1f;
                }
            }

            var heights = new float[resolution, resolution]; // 타일마다 다시 채워 쓴다(할당을 한 번만)
            for (int tz = 0; tz < world.TilesZ; tz++)
            {
                for (int tx = 0; tx < world.TilesX; tx++)
                {
                    for (int j = 0; j < resolution; j++)
                    {
                        for (int i = 0; i < resolution; i++)
                        {
                            // 가장자리 칸은 이웃 타일의 첫 칸과 같은 격자 값(HeightAt은 월드 밖이면 가장자리 값)
                            int mm = world.HeightAt(tx * tile + i, tz * tile + j);
                            heights[j, i] = (mm - minMm) / (rangeMeters * MillimetersPerMeter);
                        }
                    }

                    var data = new TerrainData
                    {
                        heightmapResolution = resolution,
                        alphamapResolution = AlphamapResolution,
                        baseMapResolution = AlphamapResolution * 2,
                    };
                    data.size = new Vector3(tileMeters, rangeMeters, tileMeters);
                    data.SetHeights(0, 0, heights);
                    data.terrainLayers = new[] { layer };
                    data.SetAlphamaps(0, 0, alphamap);
                    _ownedAssets.Add(data);

                    GameObject go = Terrain.CreateTerrainGameObject(data);
                    go.name = "TerrainTile_" + tx + "_" + tz;
                    go.transform.SetParent(transform, false);
                    go.transform.position = new Vector3(tx * tileMeters, baseMeters, tz * tileMeters);
                    var terrain = go.GetComponent<Terrain>();
                    if (_terrainMaterial != null)
                    {
                        terrain.materialTemplate = _terrainMaterial;
                    }
                    terrain.heightmapPixelError = HeightmapPixelError;
                    // 재질에 GPU 인스턴싱이 꺼져 있으면 빌드에 인스턴싱 변형이 없어 지형이 안 그려지므로 끈다
                    terrain.drawInstanced = _terrainMaterial == null || _terrainMaterial.enableInstancing;
                    terrain.drawTreesAndFoliage = false; // 풀·나무는 Terrain이 아니라 자원 뷰가 그린다
                    _tiles.Add(terrain);
                }
            }
        }

        /// <summary>만든 타일과 이 클래스가 만든 데이터를 모두 지운다.</summary>
        public void Clear()
        {
            for (int i = 0; i < _tiles.Count; i++)
            {
                if (_tiles[i] != null)
                {
                    DestroyObject(_tiles[i].gameObject);
                }
            }
            _tiles.Clear();
            for (int i = 0; i < _ownedAssets.Count; i++)
            {
                if (_ownedAssets[i] != null)
                {
                    DestroyObject(_ownedAssets[i]);
                }
            }
            _ownedAssets.Clear();
        }

        private void OnDestroy() => Clear();

        /// <summary>재생 중에는 Destroy, 에디터 시험(EditMode)에서는 DestroyImmediate로 지운다.</summary>
        private static void DestroyObject(UnityEngine.Object target)
        {
            if (UnityEngine.Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        /// <summary>임시 단색 지면 레이어를 코드로 만든다(애셋 없이도 지형이 보이게).</summary>
        private TerrainLayer CreatePlaceholderLayer()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "PlaceholderGround", wrapMode = TextureWrapMode.Repeat };
            texture.SetPixels(new[] { PlaceholderGroundColor, PlaceholderGroundColor, PlaceholderGroundColor, PlaceholderGroundColor });
            texture.Apply(false, true);
            var layer = new TerrainLayer();
            layer.name = "PlaceholderGroundLayer";
            layer.diffuseTexture = texture;
            layer.tileSize = new Vector2(LayerTileSizeMeters, LayerTileSizeMeters);
            _ownedAssets.Add(texture);
            _ownedAssets.Add(layer);
            return layer;
        }
    }
}

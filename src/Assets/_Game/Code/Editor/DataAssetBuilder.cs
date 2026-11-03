// =============================================================
// 파일: DataAssetBuilder.cs
// 역할: 설정 애셋(BalanceConfig, NetworkTuningConfig, GameDataConfig)을 만들고, Data/Content 아래 ContentDef를
//       GameDataConfig.Contents에 모아 넣는다(Refresh Config Index).
// 레이어: infrastructure (Game.Editor, 편집기 전용)
// 관련: NFR-033, NFR-034, DES-104 / SDD 3.7.7, 3.8.2 Data/
// =============================================================
using System.Collections.Generic;
using System.IO;
using Game.Infra.Config;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>설정 애셋 생성·색인 도구.</summary>
    public static class DataAssetBuilder
    {
        /// <summary>데이터 루트 폴더.</summary>
        public const string DataRoot = "Assets/_Game/Data";
        /// <summary>균형 설정값 애셋 경로.</summary>
        public const string BalancePath = DataRoot + "/Balance/BalanceConfig.asset";
        /// <summary>네트워크 튜닝 애셋 경로.</summary>
        public const string NetworkPath = DataRoot + "/Network/NetworkTuningConfig.asset";
        /// <summary>그래픽 프리셋 애셋 경로.</summary>
        public const string GraphicsPath = DataRoot + "/Graphics/GraphicsPresetConfig.asset";
        /// <summary>설정 뿌리 애셋 경로.</summary>
        public const string RootPath = DataRoot + "/GameDataConfig.asset";
        /// <summary>콘텐츠 정의 폴더.</summary>
        public const string ContentFolder = DataRoot + "/Content";

        /// <summary>기본 설정 애셋(없는 것만)을 만들고 서로 연결한 뒤 색인을 갱신한다.</summary>
        [MenuItem("Game/Data/Create Default Config Assets")]
        public static void CreateDefaults()
        {
            BalanceConfig balance = LoadOrCreate<BalanceConfig>(BalancePath);
            NetworkTuningConfig network = LoadOrCreate<NetworkTuningConfig>(NetworkPath);
            GraphicsPresetConfig graphics = LoadOrCreate<GraphicsPresetConfig>(GraphicsPath);
            GameDataConfig root = LoadOrCreate<GameDataConfig>(RootPath);
            root.Balance = balance;
            root.Network = network;
            root.Graphics = graphics;
            EditorUtility.SetDirty(graphics);
            EditorUtility.SetDirty(root);
            RefreshIndex();
        }

        /// <summary>Data/Content 아래의 모든 ContentDef를 경로 순서로 GameDataConfig.Contents에 넣는다.</summary>
        [MenuItem("Game/Data/Refresh Config Index")]
        public static void RefreshIndex()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameDataConfig>(RootPath);
            if (root == null)
            {
                Debug.LogWarning("[Setup] GameDataConfig가 없어 색인을 건너뜁니다. 먼저 Create Default Config Assets를 실행하세요.");
                return;
            }

            var found = new List<ContentDef>();
            if (AssetDatabase.IsValidFolder(ContentFolder))
            {
                string[] guids = AssetDatabase.FindAssets("t:ContentDef", new[] { ContentFolder });
                var paths = new List<string>();
                foreach (string guid in guids)
                {
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                }
                paths.Sort(System.StringComparer.Ordinal);
                foreach (string path in paths)
                {
                    found.Add(AssetDatabase.LoadAssetAtPath<ContentDef>(path));
                }
            }

            root.Contents = found;
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] 설정 색인 갱신: 콘텐츠 정의 " + found.Count + "개");
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        /// <summary>폴더가 없으면 위에서부터 차례로 만든다.</summary>
        internal static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}

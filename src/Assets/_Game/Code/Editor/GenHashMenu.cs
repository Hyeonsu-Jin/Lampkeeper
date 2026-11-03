// =============================================================
// 파일: GenHashMenu.cs
// 역할: 에디터(Mono)에서 골든 시드 목록으로 -genhash 보고서를 만드는 메뉴. IL2CPP 플레이어가 만든 보고서와
//       -genhash-compare로 비교하기 위한 Mono 쪽 기준 파일을 만든다.
// 레이어: infrastructure (Game.Editor, 편집기 전용)
// 관련: NFR-053, DES-113 / SDD M1-04
//
// 출력: src/Builds/GenHash/mono_full.json, mono_small.json (git 제외 폴더)
// =============================================================
using System.IO;
using Game.Bootstrap.DevTools;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>결정론 비교 보고서 생성 메뉴.</summary>
    public static class GenHashMenu
    {
        private const string SeedsAssetPath = "Assets/Tests/EditMode/WorldGen/golden_seeds.txt";
        private const string OutputFolder = "Builds/GenHash";

        /// <summary>Full·Small 두 보고서를 Mono 런타임으로 만든다.</summary>
        [MenuItem("Game/Dev/GenHash/Write Mono Reports (Golden Seeds)")]
        public static void WriteMonoReports()
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string seeds = Path.Combine(projectRoot, SeedsAssetPath);
            string folder = Path.Combine(projectRoot, OutputFolder);
            foreach (string profile in new[] { "full", "small" })
            {
                string output = Path.Combine(folder, "mono_" + profile + ".json");
                bool ran = GenHashCommand.TryRun(new[] { "-genhash", seeds, "-out", output, "-genprofile", profile }, out int exitCode);
                Debug.Log("[GenHash] 에디터 보고서 " + profile + ": 실행=" + ran + ", 종료 코드=" + exitCode + ", 파일=" + output);
            }
        }
    }
}

// =============================================================
// 파일: BuildCommands.cs
// 역할: Windows x64 플레이어 빌드(개발/출시)와 프로젝트 전체 설정 한 번에 적용(RunAll).
// 레이어: infrastructure (Game.Editor, 편집기 전용)
// 관련: NFR-043, NFR-009, DES-112 / SDD M1-01 (5) 빌드 번호, App ID 설정 파일
//
// 배치 모드 예
//   Unity.exe -batchmode -quit -projectPath src -executeMethod Game.Editor.BuildCommands.BuildWindowsDevelopment -buildNumber 12
// 개발 빌드: Development Build(DEVELOPMENT_BUILD) + 실행 파일 옆에 steam_appid.txt(480) 복사.
// 출시 빌드: steam_appid.txt를 넣지 않는다(정식 App ID가 정해지면 SteamAppIds.Release 사용).
// =============================================================
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>빌드 명령.</summary>
    public static class BuildCommands
    {
        private const string BuildNumberArg = "-buildNumber";
        private const string BuildNumberEnv = "GAME_BUILD_NUMBER";
        private const string SteamAppIdFile = "steam_appid.txt";

        /// <summary>프로젝트 설정 + 데이터 애셋 + Boot 씬을 한 번에 만든다.</summary>
        [MenuItem("Game/Setup/Run All")]
        public static void RunAll()
        {
            ProjectConfigurator.Apply();
            DataAssetBuilder.CreateDefaults();
            GraphicsAssetBuilder.CreateAll();
            BootSceneBuilder.CreateBootScene();
            BenchSceneBuilder.CreateBenchScene();
        }

        /// <summary>Windows x64 개발 빌드(IL2CPP).</summary>
        [MenuItem("Game/Build/Windows Development")]
        public static void BuildWindowsDevelopment() => Build(true);

        /// <summary>Windows x64 출시 빌드(IL2CPP).</summary>
        [MenuItem("Game/Build/Windows Release")]
        public static void BuildWindowsRelease() => Build(false);

        private static void Build(bool development)
        {
            int exitCode = 1;
            try
            {
                string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
                string folder = Path.Combine(projectRoot, "Builds", development ? "Development" : "Release");
                string exePath = Path.Combine(folder, ProjectConfigurator.ProductName + ".exe");

                PlayerSettings.bundleVersion = ProjectConfigurator.VersionPrefix + "." + ReadBuildNumber();

                var options = new BuildPlayerOptions
                {
                    scenes = EnabledScenePaths(development),
                    locationPathName = exePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                };
                if (options.scenes.Length == 0)
                {
                    throw new InvalidOperationException("빌드 설정에 활성 씬이 없습니다. Game/Setup/Run All을 먼저 실행하세요.");
                }

                BuildReport report = BuildPipeline.BuildPlayer(options);
                Debug.Log("[Build] 결과=" + report.summary.result + ", 크기=" + report.summary.totalSize + "바이트, 출력=" + exePath);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException("플레이어 빌드 실패: " + report.summary.result);
                }

                // 사용자 결정 2026-11-03: Unity Pipeline 런타임 서버가 산출물에 들어가면 빌드를 실패로 처리한다.
                string dataFolder = Path.Combine(folder, ProjectConfigurator.ProductName + "_Data");
                var forbidden = BuildArtifactInspector.FindForbiddenPipelineAssemblies(dataFolder);
                if (forbidden.Count > 0)
                {
                    throw new InvalidOperationException("빌드 산출물에 Unity Pipeline 런타임 어셈블리가 들어갔습니다: " + string.Join(", ", forbidden));
                }
                Debug.Log("[Build] Unity Pipeline 런타임 어셈블리 없음(검사: Managed 폴더, global-metadata.dat)");

                // 개발 빌드만 App ID 480 파일을 실행 파일 옆에 둔다(NFR-009).
                string appIdTarget = Path.Combine(folder, SteamAppIdFile);
                if (development)
                {
                    File.Copy(Path.Combine(projectRoot, SteamAppIdFile), appIdTarget, true);
                }
                else if (File.Exists(appIdTarget))
                {
                    File.Delete(appIdTarget);
                }
                exitCode = 0;
            }
            catch (Exception ex)
            {
                Debug.LogError("[Build] " + ex);
            }

            if (UnityEngine.Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static string[] EnabledScenePaths(bool development)
        {
            var paths = new System.Collections.Generic.List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    paths.Add(scene.path);
                }
            }
            // Bench 장면(측정 장면, M1-05)은 개발 빌드에만 넣는다. 출시 빌드에는 들어가지 않는다.
            if (development && File.Exists(BenchSceneBuilder.BenchScenePath) && !paths.Contains(BenchSceneBuilder.BenchScenePath))
            {
                paths.Add(BenchSceneBuilder.BenchScenePath);
            }
            return paths.ToArray();
        }

        /// <summary>빌드 번호: 명령줄 -buildNumber, 없으면 환경 변수 GAME_BUILD_NUMBER, 둘 다 없으면 0.</summary>
        private static int ReadBuildNumber()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == BuildNumberArg && int.TryParse(args[i + 1], out int fromArg) && fromArg >= 0)
                {
                    return fromArg;
                }
            }
            string env = Environment.GetEnvironmentVariable(BuildNumberEnv);
            return int.TryParse(env, out int fromEnv) && fromEnv >= 0 ? fromEnv : 0;
        }
    }
}

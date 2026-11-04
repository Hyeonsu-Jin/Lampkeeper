// =============================================================
// 파일: BuildCommands.cs
// 역할: Windows x64 플레이어 빌드(개발/출시)와 프로젝트 전체 설정 한 번에 적용(RunAll).
// 레이어: infrastructure (Game.Editor, 편집기 전용)
// 관련: NFR-043, NFR-009, NFR-058, DES-112, DES-037 / SDD 3.7.12 (게임 버전 규칙), App ID 설정 파일
//
// 실행: 열린 에디터에서 메뉴 Game/Build/Windows Development|Release (또는 `unity command build run`).
//       batchmode Unity를 따로 띄우지 않는다.
// 게임 버전: bundleVersion(사람이 정한 MAJOR.MINOR.PATCH[-사전릴리스])에 빌드하는 동안만
//           `+<rel|dev>.<UTC 시각>.<커밋 7자>[.dirty]`를 붙이고, 끝나면(실패해도) 원래 값으로 되돌린다.
//           `-buildNumber`, `GAME_BUILD_NUMBER`는 폐지.
// 개발 빌드: Development Build(DEVELOPMENT_BUILD) + 실행 파일 옆에 steam_appid.txt(480) 복사.
// 출시 빌드: steam_appid.txt를 넣지 않는다(정식 App ID가 정해지면 SteamAppIds.Release 사용).
// =============================================================
using System;
using System.Diagnostics;
using System.IO;
using Game.Domain.Common;
using UnityEditor;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace Game.Editor
{
    /// <summary>빌드 명령.</summary>
    public static class BuildCommands
    {
        private const string SteamAppIdFile = "steam_appid.txt";

        /// <summary>git 명령 하나의 시간 제한(밀리초). [SDD 3.7.12 (2)]</summary>
        private const int GitTimeoutMilliseconds = 5000;

        /// <summary>프로젝트 설정 + 데이터 애셋 + Boot 씬을 한 번에 만든다.</summary>
        [MenuItem("Game/Setup/Run All")]
        public static void RunAll()
        {
            ProjectConfigurator.Apply();
            DataAssetBuilder.CreateDefaults();
            GraphicsAssetBuilder.CreateAll();
            InputActionsBuilder.Create();
            AvatarPrefabBuilder.Create();
            WorldSceneBuilder.Create();
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
            string originalVersion = null; // 빌드 전 bundleVersion(핵심). 아직 바꾸지 않았으면 null
            try
            {
                string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
                string folder = Path.Combine(projectRoot, "Builds", development ? "Development" : "Release");
                string exePath = Path.Combine(folder, ProjectConfigurator.ProductName + ".exe");

                // [NFR-058] 게임 버전: 핵심 검사 → git 정보 → (출시) 태그 검사 → 메타데이터를 붙여 빌드, 끝나면 복구(finally).
                GameVersion core = ReadCoreVersion(out originalVersion);
                string gitRoot = Directory.GetParent(projectRoot).FullName;
                ReadGitInfo(gitRoot, out string commit, out bool dirty);
                if (!development)
                {
                    CheckReleaseTag(gitRoot, core, commit);
                    if (dirty)
                    {
                        Debug.LogWarning("[Build] 작업 트리에 커밋하지 않은 수정이 있습니다. 외부에 나눠 주는 출시 빌드는 깨끗한 트리에서 만드세요(출시 점검표 M10-06).");
                    }
                }
                string channel = development ? GameVersion.ChannelDevelopment : GameVersion.ChannelRelease;
                string fullVersion = core.Core + "+" + GameVersion.ComposeBuildMetadata(channel, DateTime.UtcNow, commit, dirty);
                if (!GameVersion.TryParse(fullVersion, out _))
                {
                    throw new InvalidOperationException("게임 버전 형식 오류: " + fullVersion);
                }
                PlayerSettings.bundleVersion = fullVersion;
                Debug.Log("[Build] 게임 버전=" + fullVersion);

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
            finally
            {
                // 빌드가 끝나면(실패·중단 포함) bundleVersion을 핵심으로 되돌려 ProjectSettings.asset이 바뀌지 않게 한다.
                if (originalVersion != null)
                {
                    PlayerSettings.bundleVersion = originalVersion;
                    AssetDatabase.SaveAssets(); // ProjectSettings.asset에도 원래 값이 바로 기록되게 한다
                }
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

        /// <summary>
        /// bundleVersion을 읽어 핵심을 검사한다. `+` 뒤가 남아 있으면(이전 빌드가 되돌리기 전에 중단된 경우) 떼고 경고한다.
        /// </summary>
        /// <param name="original">되돌릴 값(`+` 뒤를 뗀 핵심 문자열)</param>
        /// <returns>해석한 핵심 버전</returns>
        /// <exception cref="InvalidOperationException">핵심이 SemVer 형식이 아닐 때</exception>
        private static GameVersion ReadCoreVersion(out string original)
        {
            string text = PlayerSettings.bundleVersion ?? string.Empty;
            int plus = text.IndexOf('+');
            if (plus >= 0)
            {
                Debug.LogWarning("[Build] bundleVersion '" + text + "'에 '+' 뒤가 남아 있어 뗍니다(이전 빌드 중단 흔적).");
                text = text.Substring(0, plus);
                PlayerSettings.bundleVersion = text;
            }
            if (!GameVersion.TryParse(text, out GameVersion core))
            {
                throw new InvalidOperationException("게임 버전 형식 오류: '" + text + "' (Project Settings → Player → Version을 MAJOR.MINOR.PATCH[-사전릴리스]로 고치세요)");
            }
            original = text;
            return core;
        }

        /// <summary>
        /// git 커밋 7자와 수정 여부를 읽는다. 추적 파일만 본다(--untracked-files=no). git이 없거나 실패하면 `nogit`으로 두고 경고한다.
        /// </summary>
        private static void ReadGitInfo(string gitRoot, out string commit, out bool dirty)
        {
            commit = GameVersion.NoGitCommit;
            dirty = false;
            if (!TryRunGit(gitRoot, "rev-parse --short=7 HEAD", out string head) || head.Length == 0)
            {
                Debug.LogWarning("[Build] git 커밋을 읽지 못했습니다. 커밋 자리는 '" + GameVersion.NoGitCommit + "'로 둡니다.");
                return;
            }
            commit = head;
            if (TryRunGit(gitRoot, "status --porcelain --untracked-files=no", out string status))
            {
                dirty = status.Length > 0;
            }
            else
            {
                Debug.LogWarning("[Build] git status를 읽지 못했습니다. 수정 여부는 '깨끗함'으로 둡니다.");
            }
        }

        /// <summary>
        /// 출시 빌드만: 태그 v(핵심)이 있고 그 커밋이 HEAD와 다르면 실패한다.
        /// 이미 배포한 번호를 다른 내용에 다시 쓰는 것을 막는다. 태그가 없으면 통과.
        /// </summary>
        private static void CheckReleaseTag(string gitRoot, GameVersion core, string shortCommit)
        {
            if (shortCommit == GameVersion.NoGitCommit)
            {
                return; // git을 못 읽었으면 검사할 수 없다(경고는 ReadGitInfo가 이미 남김)
            }
            string tag = "v" + core.Core;
            if (!TryRunGit(gitRoot, "rev-parse -q --verify refs/tags/" + tag + "^{commit}", out string tagCommit) || tagCommit.Length == 0)
            {
                return; // 태그 없음
            }
            if (!TryRunGit(gitRoot, "rev-parse HEAD", out string headCommit) || !string.Equals(tagCommit, headCommit, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("태그 " + tag + "가 현재 커밋과 다른 커밋을 가리킵니다. 배포한 번호는 다른 내용에 다시 쓸 수 없으니 버전을 올리세요.");
            }
        }

        /// <summary>git을 실행해 표준 출력(앞뒤 공백 제거)을 얻는다. 시간 제한 5초, 종료 코드 0이 아니면 false.</summary>
        private static bool TryRunGit(string workingDirectory, string arguments, out string output)
        {
            output = string.Empty;
            try
            {
                var info = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (Process process = Process.Start(info))
                {
                    if (process == null)
                    {
                        return false;
                    }
                    // 출력을 비동기로 읽어, 파이프가 차서 멈추는 일을 막는다.
                    System.Threading.Tasks.Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                    process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(GitTimeoutMilliseconds))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (InvalidOperationException)
                        {
                            // 그 사이 끝난 경우
                        }
                        return false;
                    }
                    process.WaitForExit(); // 비동기 읽기 마무리
                    if (process.ExitCode != 0)
                    {
                        return false;
                    }
                    output = stdout.Result.Trim();
                    return true;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false; // git 실행 파일이 없음
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}

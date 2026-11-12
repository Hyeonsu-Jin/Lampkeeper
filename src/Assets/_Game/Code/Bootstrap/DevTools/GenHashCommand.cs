// =============================================================
// 파일: GenHashCommand.cs
// 역할: 명령줄 -genhash / -genhash-compare 를 실제로 실행한다(파일 읽기·쓰기, 환경 정보, 종료 코드).
//       계산과 비교 자체는 Game.Application.Dev(순수 C#)에 있고, 여기는 Unity와 파일 시스템에 닿는 얇은 부분이다.
// 레이어: infrastructure (Game.Bootstrap/DevTools, 개발 빌드 전용)
// 관련: NFR-053, FR-169, DES-113 / SDD 3.7.4, M1-04
//
// 종료 코드: 0 = 성공(비교는 모두 일치), 1 = 사용법·파일·생성 오류, 3 = 비교에서 불일치.
// 출시 빌드에서는 호출하지 않는다(GameBootstrap이 BuildFlags.IsDevelopment로 막는다).
// =============================================================
using System;
using System.Collections.Generic;
using System.IO;
using Game.Application.Dev;
using UnityEngine;

namespace Game.Bootstrap.DevTools
{
    /// <summary>-genhash 계열 명령 실행기.</summary>
    public static class GenHashCommand
    {
        private const string LogTag = "[GenHash] ";

        /// <summary>종료 코드: 성공.</summary>
        public const int ExitOk = 0;
        /// <summary>종료 코드: 오류.</summary>
        public const int ExitError = 1;
        /// <summary>종료 코드: 비교 불일치.</summary>
        public const int ExitMismatch = 3;

        /// <summary>
        /// 인자에 -genhash 계열 옵션이 있으면 실행한다.
        /// </summary>
        /// <param name="args">명령줄 인자</param>
        /// <param name="exitCode">실행했다면 종료 코드</param>
        /// <returns>명령을 실행했으면 true(호출한 쪽은 부트를 멈추고 종료한다)</returns>
        public static bool TryRun(IReadOnlyList<string> args, out int exitCode)
        {
            exitCode = ExitOk;
            GenHashOptions options = GenHashOptions.Parse(args);
            if (options.Mode == GenHashMode.None)
            {
                return false;
            }
            if (options.Errors.Count > 0)
            {
                foreach (string error in options.Errors) Debug.LogError(LogTag + error);
                exitCode = ExitError;
                return true;
            }

            try
            {
                exitCode = options.Mode == GenHashMode.Generate ? RunGenerate(options) : RunCompare(options);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is MiniJsonException)
            {
                // 입출력·형식 오류는 원인을 남기고 종료 코드로 알린다. 그 밖의 예외는 그대로 올려 로그에 남게 한다.
                Debug.LogError(LogTag + ex.Message);
                exitCode = ExitError;
            }
            return true;
        }

        /// <summary>현재 실행 환경 설명을 보고서에 채운다.</summary>
        public static void FillEnvironment(GenHashReport report)
        {
#if ENABLE_IL2CPP
            report.Runtime = "IL2CPP";
#else
            report.Runtime = "Mono";
#endif
            report.Unity = UnityEngine.Application.unityVersion + (UnityEngine.Application.isEditor ? " (editor)" : " (player)");
            report.Cpu = SystemInfo.processorType + " x" + SystemInfo.processorCount;
            report.Machine = SystemInfo.deviceName;
        }

        private static int RunGenerate(GenHashOptions options)
        {
            List<string> seeds = SeedListParser.Parse(File.ReadAllText(options.SeedsPath));
            if (seeds.Count == 0)
            {
                Debug.LogError(LogTag + "시드 목록이 비어 있습니다: " + options.SeedsPath);
                return ExitError;
            }
            GenHashReport report = GenHashRunner.Run(seeds, options.ProfileName);
            FillEnvironment(report);

            string directory = Path.GetDirectoryName(Path.GetFullPath(options.OutPath));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(options.OutPath, report.ToJson());

            foreach (GenHashEntry e in report.Entries)
            {
                Debug.Log(LogTag + e.SeedText + " -> " + GenHashReport.Hex(e.Hash) + " (자원 " + e.ResourceCount + ", 후보 자리 " + e.TowerSiteCount + ", " + e.ElapsedMs + " ms)");
            }
            Debug.Log(LogTag + "보고서 저장: " + options.OutPath + " [" + report.Runtime + ", " + report.Profile + "]");
            return ExitOk;
        }

        private static int RunCompare(GenHashOptions options)
        {
            var reports = new List<GenHashReport>();
            var names = new List<string>();
            foreach (string path in options.ComparePaths)
            {
                GenHashReport report = GenHashReport.FromJson(File.ReadAllText(path));
                reports.Add(report);
                names.Add(Path.GetFileName(path) + "[" + report.Runtime + "]");
            }
            GenHashComparison comparison = GenHashComparer.Compare(reports);
            string summary = comparison.Describe(names);
            if (comparison.AllMatch)
            {
                Debug.Log(LogTag + summary);
                return ExitOk;
            }
            Debug.LogError(LogTag + summary);
            return ExitMismatch;
        }
    }
}

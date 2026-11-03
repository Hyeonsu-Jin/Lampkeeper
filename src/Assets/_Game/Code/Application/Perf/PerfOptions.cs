// =============================================================
// 파일: PerfOptions.cs
// 역할: 성능 측정 관련 개발용 명령줄 옵션(-perflog, -perfhud, -bench, -upscale, -benchseconds)을 해석한다.
// 레이어: application (순수 C#)
// 관련: FR-163, NFR-021, DES-108, DES-113 / SDD 3.7.4
//
//   -perflog [폴더]        성능 CSV 기록. 폴더를 생략하면 기본 위치(저장 폴더/Perf)
//   -perfhud               개발용 성능 표시를 켠 채 시작(실행 중에는 F3으로 켜고 끔)
//   -bench P1|P8           Bench 장면 실행(측정 장면 재현)
//   -upscale on|off        Bench 측정에서 업스케일링 켬(판정 경로)·끔(참고)
//   -benchseconds <초>     Bench 길이. 생략하면 P1 600초, P8 300초(SRS 3.3)
// 개발 빌드에서만 의미가 있다(출시 빌드는 호출한 쪽이 무시).
// =============================================================
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Application.Perf
{
    /// <summary>측정 장면 종류.</summary>
    public enum BenchScenario
    {
        /// <summary>1인 측정(P1).</summary>
        P1 = 1,

        /// <summary>8인 측정(P8). 호스트 1 + 봇 7 구성은 M1-12에서 맞춘다.</summary>
        P8 = 8,
    }

    /// <summary>업스케일링 지정(측정용).</summary>
    public enum UpscaleSwitch
    {
        /// <summary>프리셋 기본값을 따른다.</summary>
        PresetDefault = 0,
        On = 1,
        Off = 2,
    }

    /// <summary>성능·Bench 명령줄 옵션.</summary>
    public sealed class PerfOptions
    {
        public const string PerfLogFlag = "-perflog";
        public const string PerfHudFlag = "-perfhud";
        public const string BenchFlag = "-bench";
        public const string UpscaleFlag = "-upscale";
        public const string BenchSecondsFlag = "-benchseconds";

        /// <summary>P1 기본 길이(초): 10분 순회.</summary>
        public const int DefaultP1Seconds = 600;

        /// <summary>P8 기본 길이(초): 5분.</summary>
        public const int DefaultP8Seconds = 300;

        /// <summary>CSV 기록 여부.</summary>
        public bool PerfLog { get; }

        /// <summary>CSV 폴더. null이면 기본 위치.</summary>
        public string PerfLogFolder { get; }

        /// <summary>성능 표시를 켠 채 시작할지.</summary>
        public bool Hud { get; }

        /// <summary>Bench 장면. null이면 Bench를 실행하지 않는다.</summary>
        public BenchScenario? Bench { get; }

        /// <summary>업스케일링 지정.</summary>
        public UpscaleSwitch Upscale { get; }

        /// <summary>Bench 길이(초). 지정하지 않았으면 장면 기본값.</summary>
        public int BenchSeconds { get; }

        /// <summary>해석하지 못한 값 설명.</summary>
        public IReadOnlyList<string> Errors { get; }

        private PerfOptions(bool perfLog, string perfLogFolder, bool hud, BenchScenario? bench, UpscaleSwitch upscale, int benchSeconds, IReadOnlyList<string> errors)
        {
            PerfLog = perfLog;
            PerfLogFolder = perfLogFolder;
            Hud = hud;
            Bench = bench;
            Upscale = upscale;
            BenchSeconds = benchSeconds;
            Errors = errors;
        }

        /// <summary>옵션이 없는 기본값.</summary>
        public static PerfOptions None { get; } = Parse(Array.Empty<string>());

        /// <summary>명령줄 인자를 해석한다. 관련 없는 인자는 무시한다.</summary>
        public static PerfOptions Parse(IReadOnlyList<string> args)
        {
            var errors = new List<string>();
            bool perfLog = false;
            string folder = null;
            bool hud = false;
            BenchScenario? bench = null;
            UpscaleSwitch upscale = UpscaleSwitch.PresetDefault;
            int seconds = 0;

            for (int i = 0; args != null && i < args.Count; i++)
            {
                string arg = args[i] ?? string.Empty;
                if (Is(arg, PerfLogFlag))
                {
                    perfLog = true;
                    if (i + 1 < args.Count && !string.IsNullOrEmpty(args[i + 1]) && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    {
                        folder = args[++i];
                    }
                }
                else if (Is(arg, PerfHudFlag))
                {
                    hud = true;
                }
                else if (Is(arg, BenchFlag))
                {
                    string value = ReadValue(args, ref i, BenchFlag, errors);
                    if (Is(value, "P1")) bench = BenchScenario.P1;
                    else if (Is(value, "P8")) bench = BenchScenario.P8;
                    else if (value != null) errors.Add("-bench 값은 P1 또는 P8 이어야 합니다: " + value);
                }
                else if (Is(arg, UpscaleFlag))
                {
                    string value = ReadValue(args, ref i, UpscaleFlag, errors);
                    if (Is(value, "on")) upscale = UpscaleSwitch.On;
                    else if (Is(value, "off")) upscale = UpscaleSwitch.Off;
                    else if (value != null) errors.Add("-upscale 값은 on 또는 off 이어야 합니다: " + value);
                }
                else if (Is(arg, BenchSecondsFlag))
                {
                    string value = ReadValue(args, ref i, BenchSecondsFlag, errors);
                    if (value != null && (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out seconds) || seconds <= 0))
                    {
                        errors.Add("-benchseconds 값은 1 이상의 정수여야 합니다: " + value);
                        seconds = 0;
                    }
                }
            }

            if (bench.HasValue && seconds == 0)
            {
                seconds = bench.Value == BenchScenario.P1 ? DefaultP1Seconds : DefaultP8Seconds;
            }
            return new PerfOptions(perfLog, folder, hud, bench, upscale, seconds, errors);
        }

        private static bool Is(string actual, string expected) => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

        private static string ReadValue(IReadOnlyList<string> args, ref int index, string flag, List<string> errors)
        {
            if (index + 1 >= args.Count || string.IsNullOrEmpty(args[index + 1]) || args[index + 1].StartsWith("-", StringComparison.Ordinal))
            {
                errors.Add(flag + " 뒤에 값이 필요합니다.");
                return null;
            }
            return args[++index];
        }
    }
}

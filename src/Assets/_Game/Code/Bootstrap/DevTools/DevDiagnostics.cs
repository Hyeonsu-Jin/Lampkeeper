// =============================================================
// 파일: DevDiagnostics.cs
// 역할: 진단 기능(파일 로그, 성능 측정)을 부트에서 켜는 조립 코드. 파일 로그는 모든 빌드에서,
//       성능 측정·개발용 성능 표시·CSV 기록은 개발 빌드에서만 켠다.
// 레이어: infrastructure (Game.Bootstrap, 컴포지션 루트)
// 관련: FR-158, FR-163, NFR-021, NFR-042, DES-107, DES-108 / SDD 3.7.4, 3.12.4, M1-05
//
// 측정기(IPerfProbe)는 여기서 만든 것을 서버·클라이언트 코드에 생성자로 넘겨 준다(전역 접근은 이 조립 지점뿐).
// =============================================================
using System;
using System.Collections.Generic;
using System.IO;
using Game.Application.Perf;
using Game.Application.Ports;
using Game.Infra.Platform.Logging;
using Game.Infra.Platform.Telemetry;
using UnityEngine;

namespace Game.Bootstrap.DevTools
{
    /// <summary>진단 기능 조립.</summary>
    public static class DevDiagnostics
    {
        private const string LogTag = "[Diag] ";
        private const string PerfFolderName = "Perf";

        /// <summary>켜진 성능 측정 컴포넌트. 출시 빌드이거나 아직 설치 전이면 null.</summary>
        public static PerfTelemetryBehaviour Telemetry { get; private set; }

        /// <summary>서버·클라이언트 코드에 넘겨 줄 측정기. 측정이 꺼져 있으면 아무것도 하지 않는 구현.</summary>
        public static IPerfProbe Probe => Telemetry != null ? Telemetry.Telemetry.Probe : NullPerfProbe.Instance;

        /// <summary>
        /// 진단 기능을 켠다. 여러 번 불러도 한 번만 설치된다.
        /// </summary>
        /// <param name="args">명령줄 인자</param>
        public static PerfOptions Install(IReadOnlyList<string> args)
        {
            PerfOptions options = PerfOptions.Parse(args);

            try
            {
                FileLogSink sink = FileLogSink.Install();
                Debug.Log(LogTag + "파일 로그: " + sink.FilePath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // 로그 폴더를 못 열어도 게임은 시작한다. 콘솔 로그만 남는다.
                Debug.LogWarning(LogTag + "파일 로그를 켜지 못했습니다: " + ex.Message);
            }

            if (!BuildFlags.IsDevelopment || Telemetry != null)
            {
                return options;
            }

            foreach (string error in options.Errors)
            {
                Debug.LogWarning(LogTag + "성능 옵션 오류: " + error);
            }
            var settings = new PerfTelemetrySettings
            {
                ShowHud = options.Hud,
                CsvPath = options.PerfLog ? ResolveCsvPath(options.PerfLogFolder) : null,
            };
            try
            {
                Telemetry = PerfTelemetryBehaviour.Create(settings);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // CSV 파일을 못 만들면 CSV 없이 측정만 계속한다.
                Debug.LogWarning(LogTag + "성능 CSV를 만들지 못해 기록 없이 측정합니다: " + ex.Message);
                settings.CsvPath = null;
                Telemetry = PerfTelemetryBehaviour.Create(settings);
            }
            return options;
        }

        /// <summary>
        /// 이미 켜진 성능 측정을 CSV 기록과 함께 다시 시작한다(Bench처럼 항상 파일이 필요한 측정용).
        /// 표시 상태는 그대로 이어받는다. 측정이 꺼져 있으면 아무것도 하지 않는다.
        /// </summary>
        public static void EnableCsv(string csvPath)
        {
            if (Telemetry == null)
            {
                return;
            }
            bool hud = Telemetry.HudVisible;
            UnityEngine.Object.Destroy(Telemetry.gameObject);
            Telemetry = PerfTelemetryBehaviour.Create(new PerfTelemetrySettings { CsvPath = csvPath, ShowHud = hud });
        }

        /// <summary>CSV 파일 전체 경로. 폴더를 주지 않으면 저장 폴더/Perf.</summary>
        public static string ResolveCsvPath(string folder, DateTime? now = null)
        {
            string directory = string.IsNullOrEmpty(folder) ? Path.Combine(UnityEngine.Application.persistentDataPath, PerfFolderName) : folder;
            string name = "perf_" + (now ?? DateTime.Now).ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".csv";
            return Path.Combine(directory, name);
        }
    }
}

// =============================================================
// 파일: DevCommandLine.cs
// 역할: 개발용 명령줄 읽기. 순수 해석(LaunchOptionsParser)은 Application에 있고, 여기서는 실제 인자를 가져오고
//       Unity에 의존하는 보조 옵션(-bootsmoke)을 다룬다.
// 레이어: infrastructure (Game.Bootstrap/DevTools)
// 관련: FR-006, DES-103 / SDD 3.7.4
// =============================================================
using System;
using System.Collections.Generic;

namespace Game.Bootstrap.DevTools
{
    /// <summary>개발용 명령줄 도우미. 상태가 없는 정적 클래스.</summary>
    public static class DevCommandLine
    {
        /// <summary>
        /// 부트 판정을 한 번 하고 종료하는 점검 옵션. 종료 코드: 0 = 메인 메뉴로 진행 가능, 2 = SCR-001(Steam 필요), 1 = 설정 오류.
        /// 개발 빌드에서만 동작한다(자동 시험·빌드 확인용).
        /// </summary>
        public const string BootSmokeFlag = "-bootsmoke";

        /// <summary>종료 코드: 메인 메뉴로 진행 가능.</summary>
        public const int ExitContinue = 0;
        /// <summary>종료 코드: 설정(ConfigLoader) 오류.</summary>
        public const int ExitConfigError = 1;
        /// <summary>종료 코드: SCR-001(Steam 필요) 화면으로 감.</summary>
        public const int ExitSteamRequired = 2;

        /// <summary>현재 프로세스의 명령줄 인자.</summary>
        public static IReadOnlyList<string> GetArgs() => Environment.GetCommandLineArgs();

        /// <summary>플래그가 있는지(대소문자 무시).</summary>
        public static bool HasFlag(IReadOnlyList<string> args, string flag)
        {
            for (int i = 0; i < args.Count; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}

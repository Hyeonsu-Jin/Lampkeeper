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

        /// <summary>실제 Steam 로비(생성·메타데이터·검색·참가)를 계정 하나로 점검하고 종료하는 옵션(개발 빌드 전용). 종료 코드 0 = 통과.</summary>
        public const string SteamLobbyTestFlag = "-steamlobbytest";

        /// <summary>입력 없이 자동으로 걷는 옵션(개발 빌드 전용, 화면 없는 다중 프로세스 시험용).</summary>
        public const string AutoWalkFlag = "-autowalk";

        /// <summary>봇 클라이언트(개발 빌드 전용, M1-12): 입력·화면 없이 정해진 경로로 걷고 자원을 때리고 생물을 쫓는다. -connect와 함께 쓴다(DES-113).</summary>
        public const string BotFlag = "-bot";

        /// <summary>N초 뒤 스스로 종료하는 옵션(개발 빌드 전용): -autoquit N</summary>
        public const string AutoQuitFlag = "-autoquit";

        /// <summary>게임 버전 문자열을 강제로 바꾸는 옵션(개발 빌드 전용): -gameversion 0.1.0+dev.x. 버전이 다른 참가자의 거부 시험용(SDD R-35).</summary>
        public const string GameVersionFlag = "-gameversion";

        /// <summary>호스트가 월드를 만든 직후 자원 N개를 바뀐 상태로 만드는 옵션(개발 빌드 전용): -resourcechanges N</summary>
        public const string ResourceChangesFlag = "-resourcechanges";

        /// <summary>호스트가 월드를 만든 직후 시험용 생물 N체를 만드는 옵션(개발 빌드 전용): -testcreatures N (M1-11 부하 시험)</summary>
        public const string TestCreaturesFlag = "-testcreatures";

        /// <summary>종료 코드: 메인 메뉴로 진행 가능.</summary>
        public const int ExitContinue = 0;
        /// <summary>종료 코드: 설정(ConfigLoader) 오류.</summary>
        public const int ExitConfigError = 1;
        /// <summary>종료 코드: SCR-001(Steam 필요) 화면으로 감.</summary>
        public const int ExitSteamRequired = 2;

        /// <summary>현재 프로세스의 명령줄 인자.</summary>
        public static IReadOnlyList<string> GetArgs() => Environment.GetCommandLineArgs();

        /// <summary>플래그 다음 인자를 값으로 읽는다(대소문자 무시). 없으면 false.</summary>
        public static bool TryGetValue(IReadOnlyList<string> args, string flag, out string value)
        {
            for (int i = 0; i < args.Count - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    value = args[i + 1];
                    return true;
                }
            }
            value = null;
            return false;
        }

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

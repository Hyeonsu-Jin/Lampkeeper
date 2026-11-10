// =============================================================
// 파일: BuildPolicy.cs
// 역할: 플레이어 빌드의 출시 규칙(깨끗한 커밋만, 배포 폴더에서 디버그 산출물 제외, 개발 빌드 표시)을 판정한다.
//       에디터·빌드를 실제로 돌리지 않고 EditMode 시험으로 검증할 수 있게 판정 로직만 따로 둔다.
// 레이어: infrastructure (Game.Editor, 편집기 전용)
// 관련: IMP-012, REV 7장 권고 3, SEC-009, NFR-058
// =============================================================
using System;
using System.Collections.Generic;
using System.IO;

namespace Game.Editor
{
    /// <summary>빌드 출시 규칙 판정. 상태가 없는 정적 클래스.</summary>
    public static class BuildPolicy
    {
        /// <summary>Unity가 출력 폴더에 만드는 디버그 산출물 폴더 이름 끝(배포 금지). 예: Game_BurstDebugInformation_DoNotShip</summary>
        public const string DoNotShipSuffix = "_DoNotShip";

        /// <summary>Unity가 출력 폴더에 만드는 백업용 폴더 이름 끝(배포 금지). 예: Game_BackUpThisFolder_ButDontShipItWithYourGame</summary>
        public const string DontShipSuffix = "_ButDontShipItWithYourGame";

        /// <summary>개발 빌드 폴더에 두는 내부용 표시 파일 이름.</summary>
        public const string InternalMarkerFile = "INTERNAL_DEVELOPMENT_BUILD.txt";

        /// <summary>내부용 표시 파일 내용.</summary>
        public const string InternalMarkerText = "내부 시험용 개발 빌드입니다. 외부에 배포하지 마세요. (Internal development build - do not distribute)";

        /// <summary>제외한 산출물을 옮겨 두는 폴더 이름(Builds 아래).</summary>
        public const string SymbolsFolderName = "Symbols";

        /// <summary>
        /// 출시 빌드는 깨끗한 커밋에서만 만든다. 개발 빌드는 검사하지 않는다.
        /// </summary>
        /// <param name="development">개발 빌드 여부</param>
        /// <param name="dirty">추적 파일에 커밋하지 않은 수정이 있는지</param>
        /// <param name="commit">커밋 7자(git을 못 읽으면 nogit)</param>
        /// <exception cref="InvalidOperationException">출시 빌드인데 작업 트리가 깨끗하다고 증명되지 않을 때</exception>
        public static void CheckReleaseAllowed(bool development, bool dirty, string commit)
        {
            if (development)
            {
                return;
            }
            // [IMP-012] 무엇이 들어갔는지 커밋으로 되짚을 수 없는 출시 빌드는 만들지 않는다.
            if (dirty)
            {
                throw new InvalidOperationException("작업 트리에 커밋하지 않은 수정이 있어 출시 빌드를 거부합니다. 커밋한 뒤 다시 만드세요(개발 빌드는 가능).");
            }
            if (string.IsNullOrEmpty(commit) || commit == Game.Domain.Common.GameVersion.NoGitCommit)
            {
                throw new InvalidOperationException("git 커밋을 읽지 못해 출시 빌드를 거부합니다(깨끗한 커밋인지 확인할 수 없음).");
            }
        }

        /// <summary>폴더 이름이 배포하면 안 되는 디버그·백업 산출물이면 true.</summary>
        public static bool IsNonShippable(string folderName)
        {
            return !string.IsNullOrEmpty(folderName)
                && (folderName.EndsWith(DoNotShipSuffix, StringComparison.Ordinal)
                    || folderName.EndsWith(DontShipSuffix, StringComparison.Ordinal));
        }

        /// <summary>출력 폴더 바로 아래에서 배포하면 안 되는 폴더를 찾는다.</summary>
        public static List<string> FindNonShippable(string outputFolder)
        {
            var found = new List<string>();
            if (!Directory.Exists(outputFolder))
            {
                return found;
            }
            foreach (string dir in Directory.GetDirectories(outputFolder))
            {
                if (IsNonShippable(Path.GetFileName(dir)))
                {
                    found.Add(dir);
                }
            }
            return found;
        }

        /// <summary>
        /// 배포하면 안 되는 폴더를 출력 폴더 밖(symbolsRoot)으로 옮긴다. 지우지 않는 이유: 충돌 보고를 해석할 때 심볼이 필요하다.
        /// </summary>
        /// <returns>옮긴 폴더 수</returns>
        public static int MoveNonShippable(string outputFolder, string symbolsRoot)
        {
            int moved = 0;
            foreach (string dir in FindNonShippable(outputFolder))
            {
                Directory.CreateDirectory(symbolsRoot);
                string target = Path.Combine(symbolsRoot, Path.GetFileName(dir));
                if (Directory.Exists(target))
                {
                    Directory.Delete(target, true);
                }
                Directory.Move(dir, target);
                moved++;
            }
            return moved;
        }
    }
}

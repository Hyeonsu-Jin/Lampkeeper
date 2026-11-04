// =============================================================
// 파일: GameVersion.cs
// 역할: 게임 버전 값 객체(시맨틱 버저닝 2.0.0). 해석, 같은 빌드 판정, 표시 형식, 빌드 메타데이터 조립.
// 레이어: domain (Game.Domain, 순수 C#)
// 관련: NFR-058, FR-003, FR-145, DES-037, DES-031 / SDD 3.7.12 (3)
//
// 우선순위(어느 쪽이 더 새 버전인가) 비교는 만들지 않는다. 지금 쓰는 곳이 없다.
// 세션 참가는 번호의 크기와 무관하게 「같은 빌드」끼리만 허용한다(IsSameBuild).
// =============================================================
using System;
using System.Globalization;
using System.Text;

namespace Game.Domain.Common
{
    /// <summary>게임 버전. `MAJOR.MINOR.PATCH[-사전릴리스][+빌드메타데이터]`. 만든 뒤에는 바뀌지 않는다.</summary>
    public sealed class GameVersion
    {
        /// <summary>각 숫자 자리의 상한. 월드 파일 헤더가 u16으로 담는다(SDD 3.5.2).</summary>
        public const int MaxNumber = 65535;

        /// <summary>전체 문자열의 최대 바이트 수. 네트워크 필드 상한과 같다(ProtocolConstants.MaxGameVersionBytes).</summary>
        public const int MaxBytes = 64;

        /// <summary>메타데이터 채널: 출시 빌드.</summary>
        public const string ChannelRelease = "rel";

        /// <summary>메타데이터 채널: 개발 빌드.</summary>
        public const string ChannelDevelopment = "dev";

        /// <summary>메타데이터 채널: 에디터 재생(런타임이 붙임).</summary>
        public const string ChannelEditor = "editor";

        /// <summary>git 정보를 읽지 못했을 때 커밋 자리에 넣는 값.</summary>
        public const string NoGitCommit = "nogit";

        /// <summary>작업 트리에 커밋하지 않은 수정이 있다는 표시.</summary>
        public const string DirtyMark = "dirty";

        private const string TimeFormat = "yyyyMMddHHmmss";

        private GameVersion(int major, int minor, int patch, string preRelease, string buildMetadata)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            PreRelease = preRelease;
            BuildMetadata = buildMetadata;
            Core = Major.ToString(CultureInfo.InvariantCulture) + "." + Minor.ToString(CultureInfo.InvariantCulture) + "."
                   + Patch.ToString(CultureInfo.InvariantCulture)
                   + (preRelease.Length > 0 ? "-" + preRelease : string.Empty);
            Full = buildMetadata.Length > 0 ? Core + "+" + buildMetadata : Core;

            // 채널 = 메타데이터 첫 식별자(rel/dev/editor). 그 밖의 값이나 메타데이터 없음은 빈 값.
            string first = buildMetadata;
            int dot = buildMetadata.IndexOf('.');
            if (dot >= 0)
            {
                first = buildMetadata.Substring(0, dot);
            }
            Channel = first == ChannelRelease || first == ChannelDevelopment || first == ChannelEditor ? first : string.Empty;
        }

        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }

        /// <summary>사전 릴리스 식별자(`-` 뒤). 없으면 빈 문자열.</summary>
        public string PreRelease { get; }

        /// <summary>빌드 메타데이터(`+` 뒤). 없으면 빈 문자열.</summary>
        public string BuildMetadata { get; }

        /// <summary>`MAJOR.MINOR.PATCH[-PreRelease]`.</summary>
        public string Core { get; }

        /// <summary>`Core[+BuildMetadata]`. 해석한 원래 문자열과 같다.</summary>
        public string Full { get; }

        /// <summary>메타데이터 첫 식별자가 rel/dev/editor이면 그 값, 아니면 빈 문자열.</summary>
        public string Channel { get; }

        /// <summary>
        /// 버전 문자열을 엄격하게 해석한다. 예외를 던지지 않는다.
        /// 실패 조건: null·빈 문자열, 64바이트 초과, 허용 글자 밖, 숫자 앞자리 0, 빈 식별자, 자리 65535 초과.
        /// </summary>
        /// <param name="text">예: `0.1.0`, `1.0.0-rc.1`, `0.1.0+dev.20261104153012.a1b2c3d`</param>
        /// <param name="version">성공하면 해석한 값, 실패하면 null</param>
        public static bool TryParse(string text, out GameVersion version)
        {
            version = null;
            if (string.IsNullOrEmpty(text) || text.Length > MaxBytes)
            {
                return false;
            }

            // 허용 글자만 쓰면 모두 ASCII라 글자 수가 곧 바이트 수다. 공백·한글 등은 여기서 걸러진다.
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '-' || c == '.' || c == '+';
                if (!ok)
                {
                    return false;
                }
            }

            // 첫 `+` 뒤가 메타데이터, 그 앞에서 첫 `-` 뒤가 사전 릴리스.
            string main = text;
            string metadata = string.Empty;
            int plus = text.IndexOf('+');
            if (plus >= 0)
            {
                main = text.Substring(0, plus);
                metadata = text.Substring(plus + 1);
                if (!AreValidIdentifiers(metadata, false))
                {
                    return false;
                }
            }

            string core = main;
            string pre = string.Empty;
            int dash = main.IndexOf('-');
            if (dash >= 0)
            {
                core = main.Substring(0, dash);
                pre = main.Substring(dash + 1);
                if (!AreValidIdentifiers(pre, true))
                {
                    return false;
                }
            }

            string[] parts = core.Split('.');
            if (parts.Length != 3
                || !TryParseNumber(parts[0], out int major)
                || !TryParseNumber(parts[1], out int minor)
                || !TryParseNumber(parts[2], out int patch))
            {
                return false;
            }

            version = new GameVersion(major, minor, patch, pre, metadata);
            return true;
        }

        /// <summary>
        /// 같은 빌드인지 판정한다: `Full` 전체 문자열의 서수(ordinal) 비교.
        /// SemVer의 「메타데이터는 우선순위에서 무시」는 새 버전 판정 규칙이고, 이 게임의 참가 판정은 「같은 빌드인가」라 메타데이터까지 본다(D-26).
        /// </summary>
        public bool IsSameBuild(GameVersion other) => other != null && string.Equals(Full, other.Full, StringComparison.Ordinal);

        /// <summary>화면 표시 문자열. 출시 빌드는 `v{Core}`, 그 밖(개발·에디터·메타데이터 없음)은 `v{Full}`.</summary>
        public string ToDisplayString() => Channel == ChannelRelease ? "v" + Core : "v" + Full;

        public override string ToString() => Full;

        /// <summary>
        /// 빌드 메타데이터를 조립한다: `채널.UTC시각(yyyyMMddHHmmss).커밋7자[.dirty]`.
        /// 커밋이 비었거나 허용 글자가 하나도 없으면 `nogit`을 쓴다. 7자를 넘는 커밋은 앞 7자만 쓴다.
        /// </summary>
        /// <param name="channel">`rel` 또는 `dev`</param>
        /// <param name="utc">빌드 시각(UTC)</param>
        /// <param name="commit">git 커밋 해시(앞 7자만 쓴다)</param>
        /// <param name="dirty">커밋하지 않은 수정이 있는지</param>
        public static string ComposeBuildMetadata(string channel, DateTime utc, string commit, bool dirty)
        {
            var sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(channel) ? ChannelDevelopment : channel);
            sb.Append('.');
            sb.Append(utc.ToString(TimeFormat, CultureInfo.InvariantCulture));
            sb.Append('.');
            sb.Append(CleanCommit(commit));
            if (dirty)
            {
                sb.Append('.').Append(DirtyMark);
            }
            return sb.ToString();
        }

        private static string CleanCommit(string commit)
        {
            if (string.IsNullOrEmpty(commit))
            {
                return NoGitCommit;
            }
            var sb = new StringBuilder();
            for (int i = 0; i < commit.Length && sb.Length < 7; i++)
            {
                char c = commit[i];
                if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '-')
                {
                    sb.Append(c);
                }
            }
            return sb.Length == 0 ? NoGitCommit : sb.ToString();
        }

        /// <summary>숫자 식별자: 비어 있지 않고, 숫자만, 앞자리 0 금지(`0` 하나는 허용), 65535 이하.</summary>
        private static bool TryParseNumber(string text, out int value)
        {
            value = 0;
            if (text.Length == 0 || text.Length > 5 || (text.Length > 1 && text[0] == '0'))
            {
                return false;
            }
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] < '0' || text[i] > '9')
                {
                    return false;
                }
                value = value * 10 + (text[i] - '0');
            }
            return value <= MaxNumber;
        }

        /// <summary>
        /// 점으로 나눈 식별자 목록 검사. 빈 식별자 금지, 글자는 [0-9A-Za-z-].
        /// 사전 릴리스에서만 숫자뿐인 식별자의 앞자리 0을 금지한다(`01` 불가, 메타데이터는 허용).
        /// </summary>
        private static bool AreValidIdentifiers(string text, bool rejectLeadingZeroNumeric)
        {
            if (text.Length == 0)
            {
                return false;
            }
            string[] ids = text.Split('.');
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                if (id.Length == 0)
                {
                    return false;
                }
                bool numeric = true;
                for (int k = 0; k < id.Length; k++)
                {
                    char c = id[k];
                    if (c == '+')
                    {
                        return false;
                    }
                    if (c < '0' || c > '9')
                    {
                        numeric = false;
                    }
                }
                if (rejectLeadingZeroNumeric && numeric && id.Length > 1 && id[0] == '0')
                {
                    return false;
                }
            }
            return true;
        }
    }
}

// =============================================================
// 파일: LogSanitizer.cs
// 역할: 로그 한 줄에서 민감 정보(Steam ID, 비밀번호·토큰·티켓 값, 사용자 폴더 이름, 외부 IP 주소)를 가린 글자로 바꾼다.
//       로그 파일과 충돌 보고에 남기기 전에 반드시 거친다.
// 레이어: application (순수 C#)
// 관련: FR-158, NFR-042, NFR-045, DES-107 / SDD 3.7.3 (Logging·CrashReporter), M1-05
//
// 가리는 규칙
//  1) Steam ID64(17자리, 7656119로 시작) -> [steamid]
//  2) password / passwd / token / ticket / secret / apikey / api_key 뒤의 값 (= 또는 : 로 이어짐) -> [redacted]
//  3) C:\Users\이름\ 및 /Users/이름/, /home/이름/ 의 이름 -> <user>
//  4) IPv4 주소: 앞 두 자리만 남기고 a.b.x.x (127.x.x.x 루프백과 0.0.0.0은 그대로)
// 정규식 없이 직접 훑어서 플랫폼(IL2CPP 코드 스트리핑) 영향을 받지 않게 했다.
// =============================================================
using System;
using System.Text;

namespace Game.Application.Logging
{
    /// <summary>로그 민감 정보 마스킹. 상태가 없는 정적 클래스.</summary>
    public static class LogSanitizer
    {
        /// <summary>Steam ID64를 대체하는 글자.</summary>
        public const string SteamIdMask = "[steamid]";

        /// <summary>비밀 값을 대체하는 글자.</summary>
        public const string SecretMask = "[redacted]";

        /// <summary>사용자 폴더 이름을 대체하는 글자.</summary>
        public const string UserMask = "<user>";

        private const string SteamIdPrefix = "7656119";
        private const int SteamIdLength = 17;

        private static readonly string[] SecretKeys = { "password", "passwd", "token", "ticket", "secret", "apikey", "api_key" };
        private static readonly string[] UserFolderMarkers = { "\\Users\\", "/Users/", "/home/" };

        /// <summary>글자에서 민감 정보를 가린다. null은 빈 글자로 취급한다.</summary>
        public static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            string result = MaskSecrets(text);
            result = MaskUserFolders(result);
            result = MaskSteamIds(result);
            result = MaskIpAddresses(result);
            return result;
        }

        // ---------- 1) Steam ID ----------
        private static string MaskSteamIds(string text)
        {
            StringBuilder builder = null;
            int copied = 0;
            for (int i = 0; i + SteamIdLength <= text.Length; i++)
            {
                if (!IsDigit(text[i]) || (i > 0 && IsDigit(text[i - 1])) || string.CompareOrdinal(text, i, SteamIdPrefix, 0, SteamIdPrefix.Length) != 0)
                {
                    continue;
                }
                int end = i;
                while (end < text.Length && IsDigit(text[end])) end++;
                if (end - i != SteamIdLength)
                {
                    i = end;
                    continue;
                }
                builder = builder ?? new StringBuilder(text.Length);
                builder.Append(text, copied, i - copied).Append(SteamIdMask);
                copied = end;
                i = end - 1;
            }
            if (builder == null)
            {
                return text;
            }
            return builder.Append(text, copied, text.Length - copied).ToString();
        }

        // ---------- 2) 비밀 값 ----------
        private static string MaskSecrets(string text)
        {
            StringBuilder builder = null;
            int copied = 0;
            int i = 0;
            while (i < text.Length)
            {
                int keyLength = MatchSecretKey(text, i);
                if (keyLength == 0)
                {
                    i++;
                    continue;
                }
                int p = i + keyLength;
                if (p < text.Length && text[p] == '"') p++; // JSON처럼 키가 따옴표로 닫힌 경우
                while (p < text.Length && text[p] == ' ') p++;
                if (p >= text.Length || (text[p] != '=' && text[p] != ':'))
                {
                    i += keyLength;
                    continue;
                }
                p++;
                while (p < text.Length && text[p] == ' ') p++;
                if (p < text.Length && text[p] == '"') p++; // 값이 따옴표로 시작하면 따옴표 안쪽만 가린다
                int valueStart = p;
                while (p < text.Length && !IsValueTerminator(text[p])) p++;
                if (p == valueStart)
                {
                    i = p;
                    continue;
                }
                builder = builder ?? new StringBuilder(text.Length);
                builder.Append(text, copied, valueStart - copied).Append(SecretMask);
                copied = p;
                i = p;
            }
            if (builder == null)
            {
                return text;
            }
            return builder.Append(text, copied, text.Length - copied).ToString();
        }

        private static int MatchSecretKey(string text, int index)
        {
            foreach (string key in SecretKeys)
            {
                if (index + key.Length <= text.Length && string.Compare(text, index, key, 0, key.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return key.Length;
                }
            }
            return 0;
        }

        private static bool IsValueTerminator(char c) => c == ' ' || c == ',' || c == ';' || c == '&' || c == '"' || c == '\'' || c == '\r' || c == '\n' || c == '\t';

        // ---------- 3) 사용자 폴더 ----------
        private static string MaskUserFolders(string text)
        {
            string result = text;
            foreach (string marker in UserFolderMarkers)
            {
                int search = 0;
                StringBuilder builder = null;
                int copied = 0;
                while (true)
                {
                    int found = result.IndexOf(marker, search, StringComparison.OrdinalIgnoreCase);
                    if (found < 0) break;
                    int nameStart = found + marker.Length;
                    int nameEnd = nameStart;
                    while (nameEnd < result.Length && result[nameEnd] != '\\' && result[nameEnd] != '/' && result[nameEnd] != ' ' && result[nameEnd] != '"' && result[nameEnd] != '\'' && result[nameEnd] != '\r' && result[nameEnd] != '\n') nameEnd++;
                    builder = builder ?? new StringBuilder(result.Length);
                    builder.Append(result, copied, nameStart - copied).Append(UserMask);
                    copied = nameEnd;
                    search = nameEnd;
                }
                if (builder != null)
                {
                    result = builder.Append(result, copied, result.Length - copied).ToString();
                }
            }
            return result;
        }

        // ---------- 4) IPv4 ----------
        private static string MaskIpAddresses(string text)
        {
            StringBuilder builder = null;
            int copied = 0;
            int i = 0;
            while (i < text.Length)
            {
                if (!IsDigit(text[i]) || (i > 0 && (IsDigit(text[i - 1]) || text[i - 1] == '.')))
                {
                    i++;
                    continue;
                }
                if (TryReadIpv4(text, i, out int end, out int firstOctet, out int secondEnd))
                {
                    if (firstOctet != 127 && firstOctet != 0)
                    {
                        builder = builder ?? new StringBuilder(text.Length);
                        builder.Append(text, copied, secondEnd - copied).Append(".x.x");
                        copied = end;
                    }
                    i = end;
                }
                else
                {
                    while (i < text.Length && (IsDigit(text[i]) || text[i] == '.')) i++;
                }
            }
            if (builder == null)
            {
                return text;
            }
            return builder.Append(text, copied, text.Length - copied).ToString();
        }

        /// <summary>index에서 a.b.c.d 모양을 읽는다. secondEnd는 둘째 숫자가 끝나는 위치.</summary>
        private static bool TryReadIpv4(string text, int index, out int end, out int firstOctet, out int secondEnd)
        {
            end = index;
            firstOctet = 0;
            secondEnd = index;
            int p = index;
            for (int part = 0; part < 4; part++)
            {
                int start = p;
                int value = 0;
                while (p < text.Length && IsDigit(text[p]) && p - start < 3)
                {
                    value = value * 10 + (text[p] - '0');
                    p++;
                }
                if (p == start || value > 255)
                {
                    return false;
                }
                if (part == 0) firstOctet = value;
                if (part == 1) secondEnd = p;
                if (part < 3)
                {
                    if (p >= text.Length || text[p] != '.') return false;
                    p++;
                }
            }
            if (p < text.Length && (IsDigit(text[p]) || text[p] == '.' && p + 1 < text.Length && IsDigit(text[p + 1])))
            {
                return false; // 숫자가 더 이어지면 IP가 아니라 버전 번호 등
            }
            end = p;
            return true;
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';
    }

    /// <summary>로그 한 줄 만들기. 시각·수준·본문을 정해진 모양으로 붙인다.</summary>
    public static class LogLineFormatter
    {
        /// <summary>
        /// "2026-11-03 18:20:01.123 [Error] 본문" 모양의 한 줄을 만든다. 본문은 민감 정보를 가린 뒤 붙인다.
        /// </summary>
        /// <param name="utcTime">기록 시각(UTC)</param>
        /// <param name="level">수준 이름(Log, Warning, Error 등)</param>
        /// <param name="message">본문</param>
        public static string Format(DateTime utcTime, string level, string message)
        {
            return utcTime.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture)
                   + " [" + level + "] " + LogSanitizer.Sanitize(message);
        }
    }
}

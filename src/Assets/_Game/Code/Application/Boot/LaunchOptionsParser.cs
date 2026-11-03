// =============================================================
// 파일: LaunchOptionsParser.cs
// 역할: 명령줄 인자 배열을 LaunchOptions로 바꾼다. 알 수 없는 옵션은 무시한다(다른 도구 옵션과 공존).
// 레이어: application (순수 C#)
// 관련: FR-006, DES-103 / SDD 3.7.4
//
// 지원 옵션
//   -transport tugboat|steam   전송 선택
//   -host                      직접 전송으로 호스트 (-transport 없으면 tugboat로 간주)
//   -connect <ip:port>         직접 전송으로 참가 (-transport 없으면 tugboat로 간주)
//   -devPlayerId <숫자>         개발용 플레이어 ID (같은 PC·같은 Steam 계정으로 여러 빌드를 띄울 때)
// =============================================================
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Application.Boot
{
    /// <summary>명령줄 해석기. 상태가 없는 정적 클래스.</summary>
    public static class LaunchOptionsParser
    {
        /// <summary>-transport 옵션 이름.</summary>
        public const string TransportFlag = "-transport";
        /// <summary>-host 옵션 이름.</summary>
        public const string HostFlag = "-host";
        /// <summary>-connect 옵션 이름.</summary>
        public const string ConnectFlag = "-connect";
        /// <summary>-devPlayerId 옵션 이름.</summary>
        public const string DevPlayerIdFlag = "-devPlayerId";

        /// <summary>
        /// 인자 배열을 해석한다.
        /// </summary>
        /// <param name="args">명령줄 인자(실행 파일 경로 포함 여부는 상관없음)</param>
        /// <returns>해석 결과. 값 오류는 Errors에 담고 예외는 던지지 않는다</returns>
        public static LaunchOptions Parse(IReadOnlyList<string> args)
        {
            var errors = new List<string>();
            TransportKind? transport = null;
            bool host = false;
            NetworkEndpoint? connect = null;
            ulong? devPlayerId = null;

            if (args != null)
            {
                for (int i = 0; i < args.Count; i++)
                {
                    string arg = args[i] ?? string.Empty;
                    if (Is(arg, TransportFlag))
                    {
                        if (TryReadValue(args, ref i, TransportFlag, errors, out string value))
                        {
                            if (Is(value, "tugboat")) transport = TransportKind.Tugboat;
                            else if (Is(value, "steam")) transport = TransportKind.Steam;
                            else errors.Add("-transport 값은 tugboat 또는 steam 이어야 합니다: " + value);
                        }
                    }
                    else if (Is(arg, HostFlag))
                    {
                        host = true;
                    }
                    else if (Is(arg, ConnectFlag))
                    {
                        if (TryReadValue(args, ref i, ConnectFlag, errors, out string value))
                        {
                            if (TryParseEndpoint(value, out NetworkEndpoint endpoint)) connect = endpoint;
                            else errors.Add("-connect 값은 ip:port 형식이어야 합니다: " + value);
                        }
                    }
                    else if (Is(arg, DevPlayerIdFlag))
                    {
                        if (TryReadValue(args, ref i, DevPlayerIdFlag, errors, out string value))
                        {
                            if (ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id) && id != 0UL) devPlayerId = id;
                            else errors.Add("-devPlayerId 값은 0이 아닌 양의 정수여야 합니다: " + value);
                        }
                    }
                }
            }

            // -host / -connect 는 직접 전송을 뜻하므로, 전송을 따로 안 정했으면 Tugboat로 본다.
            if ((host || connect.HasValue) && !transport.HasValue)
            {
                transport = TransportKind.Tugboat;
            }
            if ((host || connect.HasValue) && transport == TransportKind.Steam)
            {
                errors.Add("-host/-connect 는 -transport tugboat 와 함께 써야 합니다.");
            }
            if (host && connect.HasValue)
            {
                errors.Add("-host 와 -connect 는 함께 쓸 수 없습니다.");
            }

            return new LaunchOptions(transport, host, connect, devPlayerId, errors);
        }

        /// <summary>"ip:port" 글자를 주소로 바꾼다. IPv6는 지원하지 않는다(개발용).</summary>
        public static bool TryParseEndpoint(string text, out NetworkEndpoint endpoint)
        {
            endpoint = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            int colon = text.LastIndexOf(':');
            if (colon <= 0 || colon == text.Length - 1)
            {
                return false;
            }
            string host = text.Substring(0, colon);
            string portText = text.Substring(colon + 1);
            if (!ushort.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out ushort port) || port == 0)
            {
                return false;
            }
            endpoint = new NetworkEndpoint(host, port);
            return true;
        }

        private static bool Is(string actual, string expected) =>
            string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

        /// <summary>다음 인자를 값으로 읽는다. 없거나 또 다른 옵션이면 오류.</summary>
        private static bool TryReadValue(IReadOnlyList<string> args, ref int index, string flag, List<string> errors, out string value)
        {
            if (index + 1 >= args.Count || string.IsNullOrEmpty(args[index + 1]) || args[index + 1].StartsWith("-", StringComparison.Ordinal))
            {
                errors.Add(flag + " 뒤에 값이 필요합니다.");
                value = null;
                return false;
            }
            index++;
            value = args[index];
            return true;
        }
    }
}

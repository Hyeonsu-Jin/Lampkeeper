// =============================================================
// 파일: TextTable.cs
// 역할: "키<TAB>글자" 줄 형식의 텍스트 표를 읽어 키 -> 글자로 찾게 한다. 한 언어에 표 하나.
// 레이어: application (순수 C#)
// 관련: NFR-034, NFR-044, SDD R-20 / SDD 3.7.9 (M1은 임시 표, M2-01에서 Unity Localization String Table로 교체)
//
// 형식 규칙
//  - 한 줄에 한 항목: 키, 탭 문자, 글자
//  - '#'으로 시작하는 줄과 빈 줄은 무시
//  - 글자 안의 \n 은 줄바꿈, \t 는 탭, \\ 는 역슬래시로 읽는다
// =============================================================
using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Application.Localization
{
    /// <summary>한 언어의 텍스트 표.</summary>
    public sealed class TextTable
    {
        private readonly Dictionary<string, string> _entries;

        /// <summary>항목 수.</summary>
        public int Count => _entries.Count;

        /// <summary>키 목록(누락 검사용).</summary>
        public IEnumerable<string> Keys => _entries.Keys;

        private TextTable(Dictionary<string, string> entries)
        {
            _entries = entries;
        }

        /// <summary>
        /// 글자 내용을 표로 읽는다.
        /// </summary>
        /// <param name="content">파일 내용(null이면 빈 표)</param>
        /// <param name="errors">형식 오류를 모을 목록(null 가능)</param>
        public static TextTable Parse(string content, List<string> errors = null)
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(content))
            {
                return new TextTable(entries);
            }

            string[] lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                int tab = line.IndexOf('\t');
                if (tab <= 0)
                {
                    errors?.Add("줄 " + (i + 1) + ": 키와 글자를 탭으로 나눠야 합니다.");
                    continue;
                }

                string key = line.Substring(0, tab).Trim();
                string value = Unescape(line.Substring(tab + 1));
                if (!entries.TryAdd(key, value))
                {
                    errors?.Add("줄 " + (i + 1) + ": 키가 중복됩니다: " + key);
                }
            }
            return new TextTable(entries);
        }

        /// <summary>키에 해당하는 글자를 찾는다.</summary>
        public bool TryGet(string key, out string value)
        {
            if (key == null)
            {
                value = null;
                return false;
            }
            return _entries.TryGetValue(key, out value);
        }

        private static string Unescape(string raw)
        {
            if (raw.IndexOf('\\') < 0)
            {
                return raw;
            }
            var sb = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '\\' && i + 1 < raw.Length)
                {
                    char next = raw[++i];
                    switch (next)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case '\\': sb.Append('\\'); break;
                        default: sb.Append('\\').Append(next); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}

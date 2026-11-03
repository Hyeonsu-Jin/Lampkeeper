// =============================================================
// 파일: MiniJson.cs
// 역할: 개발용 보고서(-genhash 결과, 성능 기록 등)를 읽고 쓰는 아주 작은 JSON 도구.
//       순수 C# 어셈블리에서 외부 JSON 라이브러리 없이 쓰려고 만들었다. 객체·배열·문자열·정수·실수·불리언·null만 다룬다.
// 레이어: application (순수 C#)
// 관련: NFR-053, DES-113 / SDD 3.7.4
//
// 읽기 결과 형식: 객체 = Dictionary&lt;string, object&gt;, 배열 = List&lt;object&gt;, 문자열 = string,
//               숫자 = long(정수) 또는 double(소수점/지수 포함), 불리언 = bool, null = null.
// 주의: 읽은 Dictionary의 순회 순서에 의존하는 로직(월드 생성 등)에 쓰지 않는다. 키로만 꺼낸다.
// =============================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Application.Dev
{
    /// <summary>JSON 해석 오류.</summary>
    public sealed class MiniJsonException : Exception
    {
        public MiniJsonException(string message) : base(message) { }
    }

    /// <summary>작은 JSON 읽기·쓰기 도구.</summary>
    public static class MiniJson
    {
        // ---------- 쓰기 ----------

        /// <summary>글자를 JSON 문자열 리터럴(따옴표 포함)로 바꾼다.</summary>
        public static string Quote(string text)
        {
            var builder = new StringBuilder();
            builder.Append('"');
            foreach (char c in text ?? string.Empty)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }
            builder.Append('"');
            return builder.ToString();
        }

        // ---------- 읽기 ----------

        /// <summary>JSON 글자를 읽어 객체 트리로 바꾼다.</summary>
        /// <exception cref="MiniJsonException">형식이 틀릴 때</exception>
        public static object Parse(string json)
        {
            if (json == null)
            {
                throw new MiniJsonException("JSON 글자가 null입니다.");
            }
            var reader = new Reader(json);
            object value = reader.ReadValue();
            reader.SkipWhitespace();
            if (!reader.AtEnd)
            {
                throw new MiniJsonException("JSON 끝에 남은 글자가 있습니다(위치 " + reader.Position + ").");
            }
            return value;
        }

        private sealed class Reader
        {
            private readonly string _text;
            private int _pos;

            public Reader(string text) { _text = text; }

            public bool AtEnd => _pos >= _text.Length;
            public int Position => _pos;

            public void SkipWhitespace()
            {
                while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos])) _pos++;
            }

            public object ReadValue()
            {
                SkipWhitespace();
                if (AtEnd) throw new MiniJsonException("값이 필요한 곳에서 JSON이 끝났습니다.");
                char c = _text[_pos];
                if (c == '{') return ReadObject();
                if (c == '[') return ReadArray();
                if (c == '"') return ReadString();
                if (c == 't') return ReadLiteral("true", true);
                if (c == 'f') return ReadLiteral("false", false);
                if (c == 'n') return ReadLiteral("null", null);
                return ReadNumber();
            }

            private Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                _pos++; // {
                SkipWhitespace();
                if (Peek('}')) { _pos++; return result; }
                while (true)
                {
                    SkipWhitespace();
                    if (!Peek('"')) throw new MiniJsonException("객체 키는 문자열이어야 합니다(위치 " + _pos + ").");
                    string key = ReadString();
                    SkipWhitespace();
                    Expect(':');
                    result[key] = ReadValue();
                    SkipWhitespace();
                    if (Peek(',')) { _pos++; continue; }
                    Expect('}');
                    return result;
                }
            }

            private List<object> ReadArray()
            {
                var result = new List<object>();
                _pos++; // [
                SkipWhitespace();
                if (Peek(']')) { _pos++; return result; }
                while (true)
                {
                    result.Add(ReadValue());
                    SkipWhitespace();
                    if (Peek(',')) { _pos++; continue; }
                    Expect(']');
                    return result;
                }
            }

            private string ReadString()
            {
                _pos++; // 여는 따옴표
                var builder = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw new MiniJsonException("문자열이 닫히지 않았습니다.");
                    char c = _text[_pos++];
                    if (c == '"') return builder.ToString();
                    if (c != '\\') { builder.Append(c); continue; }
                    if (AtEnd) throw new MiniJsonException("문자열 이스케이프가 끝났습니다.");
                    char e = _text[_pos++];
                    switch (e)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 > _text.Length) throw new MiniJsonException("\\u 이스케이프가 짧습니다.");
                            builder.Append((char)int.Parse(_text.Substring(_pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _pos += 4;
                            break;
                        default:
                            throw new MiniJsonException("알 수 없는 이스케이프: \\" + e);
                    }
                }
            }

            private object ReadLiteral(string word, object value)
            {
                if (string.CompareOrdinal(_text, _pos, word, 0, word.Length) != 0)
                {
                    throw new MiniJsonException("알 수 없는 값입니다(위치 " + _pos + ").");
                }
                _pos += word.Length;
                return value;
            }

            private object ReadNumber()
            {
                int start = _pos;
                bool isFloat = false;
                while (_pos < _text.Length)
                {
                    char c = _text[_pos];
                    if (c == '.' || c == 'e' || c == 'E') isFloat = true;
                    else if (!(c == '-' || c == '+' || (c >= '0' && c <= '9'))) break;
                    _pos++;
                }
                string token = _text.Substring(start, _pos - start);
                if (token.Length == 0) throw new MiniJsonException("숫자가 필요합니다(위치 " + start + ").");
                if (!isFloat && long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole)) return whole;
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double real)) return real;
                throw new MiniJsonException("숫자 형식이 틀립니다: " + token);
            }

            private bool Peek(char c) => _pos < _text.Length && _text[_pos] == c;

            private void Expect(char c)
            {
                if (!Peek(c)) throw new MiniJsonException("'" + c + "' 가 필요합니다(위치 " + _pos + ").");
                _pos++;
            }
        }

        // ---------- 꺼내기 도우미 ----------

        /// <summary>객체에서 문자열 값을 꺼낸다. 없으면 기본값.</summary>
        public static string GetString(Dictionary<string, object> obj, string key, string fallback = "")
        {
            return obj != null && obj.TryGetValue(key, out object value) && value is string s ? s : fallback;
        }

        /// <summary>객체에서 정수 값을 꺼낸다. 없으면 기본값.</summary>
        public static long GetLong(Dictionary<string, object> obj, string key, long fallback = 0)
        {
            return obj != null && obj.TryGetValue(key, out object value) && value is long l ? l : fallback;
        }

        /// <summary>객체에서 객체 값을 꺼낸다. 없으면 null.</summary>
        public static Dictionary<string, object> GetObject(Dictionary<string, object> obj, string key)
        {
            return obj != null && obj.TryGetValue(key, out object value) ? value as Dictionary<string, object> : null;
        }

        /// <summary>객체에서 배열 값을 꺼낸다. 없으면 null.</summary>
        public static List<object> GetArray(Dictionary<string, object> obj, string key)
        {
            return obj != null && obj.TryGetValue(key, out object value) ? value as List<object> : null;
        }
    }
}

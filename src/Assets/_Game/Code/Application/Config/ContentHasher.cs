// =============================================================
// 파일: ContentHasher.cs
// 역할: 균형 설정값과 콘텐츠 정의를 "정규화된 글자"로 바꾼 뒤 FNV-1a 64비트로 요약한다.
// 레이어: application (순수 C#)
// 관련: NFR-033, DES-104, DES-085(접속 확인) / SDD 3.7.7 (ContentHash(u64), 접속 때 호스트와 비교)
//
// 같은 데이터면 어느 PC, 어느 런타임(에디터 Mono / IL2CPP)에서도 같은 값이 나와야 한다.
//  - 필드는 이름 서수 순서로, 정의는 키 서수 순서로 쓴다.
//  - 실수는 글자 변환(런타임마다 달라질 수 있음) 대신 비트 패턴(16진수)으로 쓴다.
// =============================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Game.Application.Config
{
    /// <summary>설정·콘텐츠 요약값 계산기. 상태가 없는 정적 클래스.</summary>
    public static class ContentHasher
    {
        // 정규화 형식이 바뀌면 이 값을 올려, 예전 해시와 섞이지 않게 한다.
        private const string FormatTag = "KHCFG1";

        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        /// <summary>
        /// 요약값을 계산한다. 네트워크 튜닝은 포함하지 않는다(호스트 로컬 값).
        /// </summary>
        /// <param name="balance">균형 설정값</param>
        /// <param name="catalog">콘텐츠 목록</param>
        public static ContentHash Compute(BalanceValues balance, ContentCatalog catalog)
        {
            string text = BuildCanonicalText(balance, catalog);
            return new ContentHash(Fnv1a64(Encoding.UTF8.GetBytes(text)));
        }

        /// <summary>해시 입력이 되는 정규화 글자를 만든다(시험·진단용으로 공개).</summary>
        public static string BuildCanonicalText(BalanceValues balance, ContentCatalog catalog)
        {
            var sb = new StringBuilder(4096);
            sb.Append(FormatTag).Append('\n');
            AppendObject(sb, "balance", balance);
            foreach (ContentEntry e in catalog.Entries)
            {
                sb.Append("content|").Append(e.Id.Value.ToString(CultureInfo.InvariantCulture))
                  .Append('|').Append((int)e.Kind)
                  .Append('|').Append(e.Key)
                  .Append('|').Append(e.NameKey);
                foreach (ContentProperty p in e.Properties)
                {
                    sb.Append("|p:").Append(p.Name).Append('=').Append(p.Value);
                }
                foreach (string r in e.References)
                {
                    sb.Append("|r:").Append(r);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private static void AppendObject(StringBuilder sb, string path, object obj)
        {
            FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            foreach (FieldInfo field in fields)
            {
                object value = field.GetValue(obj);
                string fieldPath = path + "." + field.Name;
                switch (value)
                {
                    case float f:
                        sb.Append(fieldPath).Append("=f").Append(BitConverter.SingleToInt32Bits(f).ToString("X8", CultureInfo.InvariantCulture)).Append('\n');
                        break;
                    case double d:
                        sb.Append(fieldPath).Append("=d").Append(BitConverter.DoubleToInt64Bits(d).ToString("X16", CultureInfo.InvariantCulture)).Append('\n');
                        break;
                    case int i:
                        sb.Append(fieldPath).Append("=i").Append(i.ToString(CultureInfo.InvariantCulture)).Append('\n');
                        break;
                    case long l:
                        sb.Append(fieldPath).Append("=l").Append(l.ToString(CultureInfo.InvariantCulture)).Append('\n');
                        break;
                    case bool b:
                        sb.Append(fieldPath).Append(b ? "=b1" : "=b0").Append('\n');
                        break;
                    case string s:
                        sb.Append(fieldPath).Append("=s").Append(s).Append('\n');
                        break;
                    case null:
                        sb.Append(fieldPath).Append("=null\n");
                        break;
                    default:
                        if (value.GetType().IsClass)
                        {
                            AppendObject(sb, fieldPath, value);
                        }
                        else
                        {
                            throw new NotSupportedException("해시에 쓸 수 없는 필드 형식입니다: " + fieldPath + " (" + value.GetType().Name + ")");
                        }
                        break;
                }
            }
        }

        private static ulong Fnv1a64(byte[] data)
        {
            ulong hash = FnvOffsetBasis;
            for (int i = 0; i < data.Length; i++)
            {
                hash ^= data[i];
                hash *= FnvPrime;
            }
            return hash;
        }
    }
}

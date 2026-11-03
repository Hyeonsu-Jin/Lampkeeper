// =============================================================
// 파일: ContentCatalogBuilder.cs
// 역할: 정의 데이터(ContentDefinitionData) 목록을 검증하고, 키를 정렬해 ContentId를 붙인 ContentCatalog를 만든다.
// 레이어: application (순수 C#)
// 관련: NFR-033, NFR-034, DES-104 / SDD 3.7.7
//
// 검증 항목
//  - 키: 비어 있지 않고, "종류접두사.이름" 형식(소문자·숫자·밑줄), 종류에 맞는 접두사, 중복 없음
//  - 이름 텍스트 키(NameKey): 비어 있지 않음 (화면 글자는 String Table에 있다 - NFR-034)
//  - 속성 이름 중복 없음
//  - 참조 무결성: References의 키가 모두 존재
// =============================================================
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Domain.Common;

namespace Game.Application.Config
{
    /// <summary>콘텐츠 목록 조립기. 상태가 없는 정적 클래스.</summary>
    public static class ContentCatalogBuilder
    {
        // 키 형식: 소문자로 시작하는 접두사 + '.' + 소문자/숫자/밑줄 이름(점으로 단계 구분 가능)
        private static readonly Regex KeyPattern =
            new Regex(@"^[a-z][a-z0-9]*(\.[a-z0-9][a-z0-9_]*)+$", RegexOptions.CultureInvariant);

        // 텍스트 키 형식: 소문자·숫자·밑줄·점
        private static readonly Regex TextKeyPattern =
            new Regex(@"^[a-z][a-z0-9_]*(\.[a-z0-9_]+)*$", RegexOptions.CultureInvariant);

        /// <summary>종류별로 키가 시작해야 하는 접두사.</summary>
        public static string PrefixOf(ContentKind kind)
        {
            switch (kind)
            {
                case ContentKind.Item: return "item";
                case ContentKind.Resource: return "resource";
                case ContentKind.Recipe: return "recipe";
                case ContentKind.Building: return "building";
                case ContentKind.Creature: return "creature";
                case ContentKind.Wave: return "wave";
                case ContentKind.Crop: return "crop";
                case ContentKind.Cooking: return "cooking";
                case ContentKind.Buff: return "buff";
                case ContentKind.Skill: return "skill";
                case ContentKind.Objective: return "objective";
                case ContentKind.Achievement: return "achievement";
                default: return "unknown";
            }
        }

        /// <summary>
        /// 정의 목록을 검증하고 카탈로그를 만든다.
        /// </summary>
        /// <param name="definitions">입력 정의(순서 무관, null 가능)</param>
        /// <param name="issues">문제를 모을 목록</param>
        /// <returns>오류가 하나라도 있으면 null, 없으면 카탈로그</returns>
        public static ContentCatalog Build(IEnumerable<ContentDefinitionData> definitions, List<ConfigIssue> issues)
        {
            int errorsBefore = CountErrors(issues);
            var list = new List<ContentDefinitionData>();
            if (definitions != null)
            {
                foreach (ContentDefinitionData d in definitions)
                {
                    if (d == null)
                    {
                        issues.Add(ConfigIssue.Error("Content", "비어 있는 정의가 있습니다."));
                        continue;
                    }
                    list.Add(d);
                }
            }

            // 키를 서수 비교로 정렬한다. ContentId가 입력 순서·PC 언어 설정과 무관하게 같아지도록
            // 문화권 비교(CompareTo)를 쓰지 않는다.
            list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ContentDefinitionData d in list)
            {
                string path = "Content[" + d.Key + "]";
                if (string.IsNullOrEmpty(d.Key) || !KeyPattern.IsMatch(d.Key))
                {
                    issues.Add(ConfigIssue.Error(path, "키는 '종류.이름' 형식(소문자·숫자·밑줄)이어야 합니다."));
                }
                else if (!d.Key.StartsWith(PrefixOf(d.Kind) + ".", StringComparison.Ordinal))
                {
                    issues.Add(ConfigIssue.Error(path, "키는 '" + PrefixOf(d.Kind) + ".'로 시작해야 합니다(종류: " + d.Kind + ")."));
                }

                if (!seen.Add(d.Key))
                {
                    issues.Add(ConfigIssue.Error(path, "키가 중복됩니다."));
                }

                if (string.IsNullOrEmpty(d.NameKey) || !TextKeyPattern.IsMatch(d.NameKey))
                {
                    issues.Add(ConfigIssue.Error(path, "이름 텍스트 키가 비었거나 형식이 틀립니다(화면 글자는 텍스트 키만 둡니다)."));
                }

                var propertyNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (ContentProperty p in d.Properties)
                {
                    if (string.IsNullOrEmpty(p.Name))
                    {
                        issues.Add(ConfigIssue.Error(path, "이름이 없는 속성이 있습니다."));
                    }
                    else if (!propertyNames.Add(p.Name))
                    {
                        issues.Add(ConfigIssue.Error(path, "속성 이름이 중복됩니다: " + p.Name));
                    }
                }
            }

            // 참조 무결성: 모든 정의가 등록된 뒤에 확인한다.
            foreach (ContentDefinitionData d in list)
            {
                foreach (string reference in d.References)
                {
                    if (!seen.Contains(reference ?? string.Empty))
                    {
                        issues.Add(ConfigIssue.Error("Content[" + d.Key + "]", "없는 정의를 참조합니다: " + reference));
                    }
                }
            }

            if (CountErrors(issues) > errorsBefore)
            {
                return null;
            }

            var entries = new List<ContentEntry>(list.Count);
            int nextId = 1;
            foreach (ContentDefinitionData d in list)
            {
                var props = new List<ContentProperty>(d.Properties);
                props.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                var refs = new List<string>(d.References);
                refs.Sort(StringComparer.Ordinal);
                entries.Add(new ContentEntry(new ContentId(nextId++), d.Key, d.Kind, d.NameKey, props, refs));
            }
            return new ContentCatalog(entries);
        }

        private static int CountErrors(List<ConfigIssue> issues)
        {
            int count = 0;
            foreach (ConfigIssue issue in issues)
            {
                if (issue.Severity == ConfigIssueSeverity.Error)
                {
                    count++;
                }
            }
            return count;
        }
    }
}

// =============================================================
// 파일: ContentCatalog.cs
// 역할: 콘텐츠 정의의 입력 형태(ContentDefinitionData)와, 검증을 마친 읽기 전용 목록(ContentCatalog).
// 레이어: application (순수 C#)
// 관련: NFR-033, NFR-034, DES-104 / SDD 3.7.7 (정의마다 문자열 키, 로더가 키를 정렬해 ContentId 부여)
// =============================================================
using System;
using System.Collections.Generic;
using Game.Domain.Common;

namespace Game.Application.Config
{
    /// <summary>콘텐츠 정의의 종류. SDD 3.8.2 Data/Content 하위 폴더와 같다.</summary>
    public enum ContentKind
    {
        Item = 0,
        Resource = 1,
        Recipe = 2,
        Building = 3,
        Creature = 4,
        Wave = 5,
        Crop = 6,
        Cooking = 7,
        Buff = 8,
        Skill = 9,
        Objective = 10,
        Achievement = 11,
    }

    /// <summary>정의 하나가 가진 이름-값 속성 하나. 값은 문자열로 직렬화해 둔 것(해시 계산용).</summary>
    public readonly struct ContentProperty
    {
        public string Name { get; }
        public string Value { get; }

        public ContentProperty(string name, string value)
        {
            Name = name ?? string.Empty;
            Value = value ?? string.Empty;
        }
    }

    /// <summary>
    /// 로더가 ScriptableObject에서 읽어 넘기는 정의 한 개. 아직 검증 전이다.
    /// </summary>
    public sealed class ContentDefinitionData
    {
        /// <summary>문자열 키 (예: item.wood_ash). 종류에 맞는 접두사로 시작해야 한다.</summary>
        public string Key { get; }

        /// <summary>종류.</summary>
        public ContentKind Kind { get; }

        /// <summary>화면 이름의 텍스트 키 (실제 글자는 String Table에 있다, NFR-034).</summary>
        public string NameKey { get; }

        /// <summary>속성 목록(순서 무관).</summary>
        public IReadOnlyList<ContentProperty> Properties { get; }

        /// <summary>이 정의가 참조하는 다른 정의의 키 목록.</summary>
        public IReadOnlyList<string> References { get; }

        public ContentDefinitionData(
            string key,
            ContentKind kind,
            string nameKey,
            IReadOnlyList<ContentProperty> properties = null,
            IReadOnlyList<string> references = null)
        {
            Key = key ?? string.Empty;
            Kind = kind;
            NameKey = nameKey ?? string.Empty;
            Properties = properties ?? Array.Empty<ContentProperty>();
            References = references ?? Array.Empty<string>();
        }
    }

    /// <summary>검증과 정렬을 마친 정의 한 개(읽기 전용).</summary>
    public sealed class ContentEntry
    {
        public ContentId Id { get; }
        public string Key { get; }
        public ContentKind Kind { get; }
        public string NameKey { get; }

        /// <summary>이름 기준으로 정렬된 속성.</summary>
        public IReadOnlyList<ContentProperty> Properties { get; }

        /// <summary>키 기준으로 정렬된 참조.</summary>
        public IReadOnlyList<string> References { get; }

        internal ContentEntry(
            ContentId id,
            string key,
            ContentKind kind,
            string nameKey,
            IReadOnlyList<ContentProperty> properties,
            IReadOnlyList<string> references)
        {
            Id = id;
            Key = key;
            Kind = kind;
            NameKey = nameKey;
            Properties = properties;
            References = references;
        }
    }

    /// <summary>
    /// 콘텐츠 정의 전체. 키 순서(서수 비교)대로 ContentId 1, 2, 3...이 붙어 있다.
    /// </summary>
    public sealed class ContentCatalog
    {
        private readonly List<ContentEntry> _entries;
        private readonly Dictionary<string, ContentEntry> _byKey;

        /// <summary>빈 목록.</summary>
        public static ContentCatalog Empty { get; } = new ContentCatalog(new List<ContentEntry>());

        /// <summary>키 순서대로 정렬된 정의.</summary>
        public IReadOnlyList<ContentEntry> Entries => _entries;

        /// <summary>정의 개수.</summary>
        public int Count => _entries.Count;

        internal ContentCatalog(List<ContentEntry> sortedEntries)
        {
            _entries = sortedEntries;
            _byKey = new Dictionary<string, ContentEntry>(sortedEntries.Count, StringComparer.Ordinal);
            foreach (ContentEntry entry in sortedEntries)
            {
                _byKey.Add(entry.Key, entry);
            }
        }

        /// <summary>키가 있으면 true.</summary>
        public bool Contains(string key) => key != null && _byKey.ContainsKey(key);

        /// <summary>키로 ContentId를 찾는다.</summary>
        public bool TryGetId(string key, out ContentId id)
        {
            if (key != null && _byKey.TryGetValue(key, out ContentEntry entry))
            {
                id = entry.Id;
                return true;
            }
            id = ContentId.None;
            return false;
        }

        /// <summary>ContentId로 정의를 찾는다.</summary>
        public bool TryGetEntry(ContentId id, out ContentEntry entry)
        {
            // ContentId는 1부터 키 순서대로 붙어 있으므로 목록 위치로 바로 찾는다.
            int index = id.Value - 1;
            if (index >= 0 && index < _entries.Count)
            {
                entry = _entries[index];
                return true;
            }
            entry = null;
            return false;
        }
    }
}

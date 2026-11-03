// =============================================================
// 파일: ContentDef.cs
// 역할: 콘텐츠 정의 ScriptableObject 한 개(아이템, 자원 종류, 제작법 등). 위치: Assets/_Game/Data/Content/<종류>/
// 레이어: infrastructure (Game.Infra.Config)
// 관련: NFR-033(설정값 데이터 분리), NFR-034, DES-104 / SDD 3.7.7, 3.8.2 Data/, 3.8.3 이름 규칙(…Def / …Config)
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Config;
using UnityEngine;

namespace Game.Infra.Config
{
    /// <summary>콘텐츠 정의의 속성 하나(이름-값 문자열).</summary>
    [Serializable]
    public sealed class ContentPropertyEntry
    {
        public string Name;
        public string Value;
    }

    /// <summary>
    /// 콘텐츠 정의 애셋. 종류별 전용 필드(드롭 표 등)는 해당 마일스톤에서 이 클래스를 상속해 추가한다.
    /// </summary>
    [CreateAssetMenu(fileName = "ContentDef", menuName = "Game/Config/Content Def")]
    public class ContentDef : ScriptableObject
    {
        /// <summary>문자열 키(예: item.wood_ash). 종류 접두사로 시작해야 한다.</summary>
        public string Key;

        /// <summary>종류.</summary>
        public ContentKind Kind;

        /// <summary>화면 이름의 텍스트 키. 실제 글자는 String Table에 둔다(NFR-034).</summary>
        public string NameKey;

        /// <summary>속성(이름-값).</summary>
        public List<ContentPropertyEntry> Properties = new List<ContentPropertyEntry>();

        /// <summary>참조하는 다른 정의의 키.</summary>
        public List<string> References = new List<string>();

        /// <summary>
        /// 순수 C# 입력 데이터로 바꾼다. 하위 클래스가 필드를 더하면 이 메서드를 재정의해 속성으로 풀어 준다.
        /// </summary>
        public virtual ContentDefinitionData ToData()
        {
            var props = new List<ContentProperty>(Properties.Count);
            foreach (ContentPropertyEntry p in Properties)
            {
                props.Add(new ContentProperty(p?.Name, p?.Value));
            }
            return new ContentDefinitionData(Key, Kind, NameKey, props, References.ToArray());
        }
    }
}

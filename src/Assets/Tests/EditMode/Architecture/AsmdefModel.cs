// =============================================================
// 파일: AsmdefModel.cs
// 역할: 아키텍처 시험이 쓰는 asmdef 읽기 도우미. 디스크의 .asmdef JSON을 읽어 이름, 참조(GUID는 이름으로 풀어서),
//       플랫폼, noEngineReferences를 돌려준다.
// 레이어: test (EditMode, Game.Tests.Architecture)
// 관련: DES-112 / SDD 3.4.3 의존 규칙의 강제 방법
// =============================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Architecture
{
    /// <summary>.asmdef 파일 하나의 내용.</summary>
    public sealed class AsmdefInfo
    {
        public string Name;
        public string Path;
        public List<string> References = new List<string>();
        public List<string> IncludePlatforms = new List<string>();
        public List<string> DefineConstraints = new List<string>();
        public bool NoEngineReferences;
    }

    /// <summary>asmdef 읽기. 상태가 없는 정적 클래스.</summary>
    public static class AsmdefModel
    {
        private const string GuidPrefix = "GUID:";

        [Serializable]
        private sealed class Json
        {
            public string name = string.Empty;
            public string[] references = Array.Empty<string>();
            public string[] includePlatforms = Array.Empty<string>();
            public string[] defineConstraints = Array.Empty<string>();
            public bool noEngineReferences;
        }

        /// <summary>프로젝트의 Assets 폴더 경로.</summary>
        public static string AssetsFolder => Application.dataPath;

        /// <summary>Assets 아래의 모든 asmdef.</summary>
        public static List<AsmdefInfo> LoadAssetsAsmdefs()
        {
            return Directory.GetFiles(AssetsFolder, "*.asmdef", SearchOption.AllDirectories)
                .Select(Read)
                .ToList();
        }

        /// <summary>폴더 아래의 모든 asmdef(패키지 검사용).</summary>
        public static List<AsmdefInfo> LoadFrom(string folder)
        {
            return Directory.GetFiles(folder, "*.asmdef", SearchOption.AllDirectories)
                .Select(Read)
                .ToList();
        }

        /// <summary>asmdef 파일 하나를 읽는다. GUID 참조는 이름으로 바꾼다.</summary>
        public static AsmdefInfo Read(string path)
        {
            Json json = JsonUtility.FromJson<Json>(File.ReadAllText(path));
            var info = new AsmdefInfo
            {
                Name = json.name,
                Path = path.Replace('\\', '/'),
                NoEngineReferences = json.noEngineReferences,
            };
            info.IncludePlatforms.AddRange(json.includePlatforms ?? Array.Empty<string>());
            info.DefineConstraints.AddRange(json.defineConstraints ?? Array.Empty<string>());
            foreach (string reference in json.references ?? Array.Empty<string>())
            {
                info.References.Add(ResolveReference(reference));
            }
            return info;
        }

        private static string ResolveReference(string reference)
        {
            if (!reference.StartsWith(GuidPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return reference;
            }
            string guid = reference.Substring(GuidPrefix.Length);
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                return reference; // 풀지 못하면 GUID 그대로 둔다(규칙 위반 판정에는 영향 없음).
            }
            string absolute = System.IO.Path.GetFullPath(assetPath);
            return File.Exists(absolute) ? Read(absolute).Name : reference;
        }
    }
}

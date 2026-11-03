// =============================================================
// 파일: BuildArtifactInspector.cs
// 역할: 빌드 산출물에 Unity Pipeline 런타임 어셈블리(Unity.Pipeline, Unity.Pipeline.IlInterpreter)가 들어갔는지 검사한다.
//       속성 정의 어셈블리 Unity.Pipeline.Attributes만 허용한다(사용자 결정 2026-11-03).
// 레이어: infrastructure (Game.Editor, 편집기 전용)
// 관련: DES-112, SDD 3.4.3 (Pipeline 패키지 격리), M1-01 (3)
//
// 검사 방법
//  - Managed 폴더(Mono 빌드)의 DLL 파일 이름
//  - IL2CPP 빌드의 il2cpp_data/Metadata/global-metadata.dat 안의 어셈블리 이름 글자("….dll")
// =============================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Game.Editor
{
    /// <summary>빌드 산출물 검사기. 상태가 없는 정적 클래스.</summary>
    public static class BuildArtifactInspector
    {
        private const string PipelinePrefix = "Unity.Pipeline";
        private const string AllowedAssembly = "Unity.Pipeline.Attributes.dll";
        private const string DllSuffix = ".dll";

        /// <summary>
        /// 데이터 폴더(예: KkeojinHaneul_Data)에서 허용되지 않는 Pipeline 어셈블리 이름을 찾는다.
        /// </summary>
        /// <param name="dataFolder">빌드 산출물의 *_Data 폴더</param>
        /// <returns>발견한 어셈블리 이름(없으면 빈 목록)</returns>
        public static List<string> FindForbiddenPipelineAssemblies(string dataFolder)
        {
            var found = new SortedSet<string>(StringComparer.Ordinal);

            string managed = Path.Combine(dataFolder, "Managed");
            if (Directory.Exists(managed))
            {
                foreach (string dll in Directory.GetFiles(managed, "*.dll"))
                {
                    AddIfForbidden(found, Path.GetFileName(dll));
                }
            }

            string metadata = Path.Combine(dataFolder, "il2cpp_data", "Metadata", "global-metadata.dat");
            if (File.Exists(metadata))
            {
                foreach (string name in ExtractDllNames(File.ReadAllBytes(metadata)))
                {
                    AddIfForbidden(found, name);
                }
            }
            return new List<string>(found);
        }

        /// <summary>바이트에서 "Unity.Pipeline…….dll" 모양의 이름 글자를 모두 뽑는다(시험용으로 공개).</summary>
        public static List<string> ExtractDllNames(byte[] data)
        {
            var names = new List<string>();
            byte[] needle = Encoding.ASCII.GetBytes(PipelinePrefix);
            int index = 0;
            while ((index = IndexOf(data, needle, index)) >= 0)
            {
                int end = index;
                while (end < data.Length && IsNameChar(data[end]))
                {
                    end++;
                }
                string token = Encoding.ASCII.GetString(data, index, end - index);
                if (token.EndsWith(DllSuffix, StringComparison.Ordinal))
                {
                    names.Add(token);
                }
                index = Math.Max(end, index + needle.Length);
            }
            return names;
        }

        private static void AddIfForbidden(ISet<string> found, string name)
        {
            if (name.StartsWith(PipelinePrefix, StringComparison.Ordinal)
                && !string.Equals(name, AllowedAssembly, StringComparison.Ordinal))
            {
                found.Add(name);
            }
        }

        private static bool IsNameChar(byte b) =>
            (b >= (byte)'a' && b <= (byte)'z') || (b >= (byte)'A' && b <= (byte)'Z')
            || (b >= (byte)'0' && b <= (byte)'9') || b == (byte)'.' || b == (byte)'_' || b == (byte)'-';

        private static int IndexOf(byte[] haystack, byte[] needle, int start)
        {
            for (int i = start; i <= haystack.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j])
                {
                    j++;
                }
                if (j == needle.Length)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}

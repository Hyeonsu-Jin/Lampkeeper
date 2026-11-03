// =============================================================
// 파일: ArchitectureRulesTests.cs
// 역할: 아키텍처 시험. asmdef 참조가 SDD 3.4.2 표와 같은지, 의존 방향·렌더링 격리·네트워크 라이브러리 위치·
//       Assembly-CSharp 금지·Unity Pipeline 패키지 격리·프로젝트 설정(Force Text, IL2CPP, 버전 고정)을 검사한다.
// 레이어: test (EditMode, Game.Tests.Architecture)
// 관련: DES-112, NFR-027~029, NFR-043, NFR-049 / SDD 3.4.2, 3.4.3, 3.4.4, M1-01
//
// 이 시험이 실패하면 규칙을 어긴 참조를 고치는 것이 맞다. 규칙 자체를 바꾸려면 SDD 3.4.2 표부터 바꾼다.
// =============================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;
using Assembly = System.Reflection.Assembly;

namespace Game.Tests.Architecture
{
    public class ArchitectureRulesTests
    {
        // ---------- 이름 상수 ----------
        private const string Domain = "Game.Domain";
        private const string WorldGen = "Game.WorldGen";
        private const string App = "Game.Application";
        private const string Persistence = "Game.Adapters.Persistence";
        private const string Net = "Game.Adapters.Net";
        private const string Presentation = "Game.Adapters.Presentation";
        private const string Steam = "Game.Infra.Steam";
        private const string Transport = "Game.Infra.Transport";
        private const string AiJobs = "Game.Infra.AiJobs";
        private const string Config = "Game.Infra.Config";
        private const string Platform = "Game.Infra.Platform";
        private const string Rendering = "Game.Infra.Rendering";
        private const string Bootstrap = "Game.Bootstrap";
        private const string EditorTools = "Game.Editor";

        private const string FishNet = "FishNet.Runtime";
        private const string FishySteamworks = "FishySteamworks";
        private const string SteamworksNet = "com.rlabrecque.steamworks.net";
        private const string HdrpRuntime = "Unity.RenderPipelines.HighDefinition.Runtime";
        private const string CoreRpRuntime = "Unity.RenderPipelines.Core.Runtime";
        private const string RenderPipelinePrefix = "Unity.RenderPipelines";
        private const string PipelinePackageName = "com.unity.pipeline";
        private const string PipelineAssemblyPrefix = "Unity.Pipeline";

        // 사용자 결정 2026-11-03: 런타임 기능이 없는 속성 정의 어셈블리만 플레이어에 들어가도 좋다.
        private const string PipelineAllowedRuntimeAssembly = "Unity.Pipeline.Attributes";
        private const string RuntimePipelineSymbolPrefix = "ENABLE_RUNTIME_PIPELINE";

        /// <summary>SDD 3.4.2 표: 어셈블리별 허용(=선언) 참조와 noEngineReferences.</summary>
        private static readonly Dictionary<string, (string[] Refs, bool NoEngine)> Expected = new Dictionary<string, (string[], bool)>
        {
            [Domain] = (new string[0], true),
            [WorldGen] = (new[] { Domain }, true),
            [App] = (new[] { Domain, WorldGen }, true),
            [Persistence] = (new[] { App, Domain }, true),
            [Net] = (new[] { App, Domain, WorldGen, FishNet }, false),
            [Presentation] = (new[] { App, Domain, WorldGen, "Unity.InputSystem", "Unity.Localization" }, false),
            [Steam] = (new[] { App, Domain, SteamworksNet }, false),
            [Transport] = (new[] { Net, App, FishNet, FishySteamworks }, false),
            [AiJobs] = (new[] { App, Domain, "Unity.Burst", "Unity.Collections", "Unity.Mathematics" }, false),
            [Config] = (new[] { App, Domain, WorldGen }, false),
            [Platform] = (new[] { App, Domain }, false),
            [Rendering] = (new[] { Presentation, App, Domain, HdrpRuntime, CoreRpRuntime }, false),
        };

        /// <summary>Game.Bootstrap은 "위 전부"(HDRP 제외, 규칙 7) + 네트워크 라이브러리를 참조할 수 있다.</summary>
        private static readonly string[] BootstrapAllowed =
        {
            Domain, WorldGen, App, Persistence, Net, Presentation, Steam, Transport, AiJobs, Config, Platform, Rendering,
            FishNet, SteamworksNet, FishySteamworks, "Unity.InputSystem", "Unity.Localization",
        };

        /// <summary>레이어 순서. 낮은 번호가 안쪽이다. 참조는 더 안쪽(낮은 번호)으로만 향한다.</summary>
        private static int Rank(string assembly)
        {
            switch (assembly)
            {
                case Domain:
                case WorldGen: return 1;
                case App: return 2;
                case Persistence:
                case Net:
                case Presentation: return 3;
                case Steam:
                case Transport:
                case AiJobs:
                case Config:
                case Platform:
                case Rendering: return 4;
                case Bootstrap: return 5;
                default: return -1;
            }
        }

        private static List<AsmdefInfo> _all;
        private static List<AsmdefInfo> All => _all ?? (_all = AsmdefModel.LoadAssetsAsmdefs());

        private static AsmdefInfo Get(string name)
        {
            AsmdefInfo info = All.FirstOrDefault(a => a.Name == name);
            Assert.IsNotNull(info, "asmdef가 없습니다: " + name);
            return info;
        }

        /// <summary>실제 게임 런타임 어셈블리 13개(시험·편집기 제외).</summary>
        private static IEnumerable<AsmdefInfo> RuntimeGameAsmdefs() =>
            All.Where(a => Expected.ContainsKey(a.Name) || a.Name == Bootstrap);

        // =========================================================
        // 1) asmdef 목록과 참조가 SDD 3.4.2 표와 같다
        // =========================================================
        [Test]
        public void GameAssemblies_AreExactlyTheThirteenInSdd_PlusEditorAndTests()
        {
            var runtime = All.Where(a => a.Name.StartsWith("Game.", StringComparison.Ordinal)
                                         && !a.Name.StartsWith("Game.Tests.", StringComparison.Ordinal)
                                         && a.Name != EditorTools)
                .Select(a => a.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            var expected = Expected.Keys.Concat(new[] { Bootstrap }).OrderBy(n => n, StringComparer.Ordinal).ToList();

            Assert.AreEqual(13, expected.Count);
            CollectionAssert.AreEqual(expected, runtime, "SDD 3.4.2의 asmdef 13개와 달라졌습니다.");
        }

        [Test]
        public void EveryGameAsmdef_HasUniqueName_MatchingItsFileName()
        {
            foreach (AsmdefInfo a in All.Where(x => x.Name.StartsWith("Game.", StringComparison.Ordinal)))
            {
                Assert.AreEqual(Path.GetFileNameWithoutExtension(a.Path), a.Name, a.Path);
            }
            Assert.AreEqual(All.Count, All.Select(a => a.Name).Distinct().Count(), "asmdef 이름이 중복됩니다.");
        }

        [Test]
        public void DeclaredReferences_MatchSddTable_Exactly()
        {
            foreach (KeyValuePair<string, (string[] Refs, bool NoEngine)> entry in Expected)
            {
                AsmdefInfo actual = Get(entry.Key);
                CollectionAssert.AreEquivalent(entry.Value.Refs, actual.References,
                    entry.Key + "의 참조가 SDD 3.4.2 표와 다릅니다.");
            }
        }

        [Test]
        public void Bootstrap_ReferencesOnlyAllowedAssemblies()
        {
            foreach (string reference in Get(Bootstrap).References)
            {
                CollectionAssert.Contains(BootstrapAllowed, reference, "Game.Bootstrap이 허용되지 않은 어셈블리를 참조합니다: " + reference);
            }
        }

        [Test]
        public void NoEngineReferences_FlagsMatchSddTable()
        {
            foreach (KeyValuePair<string, (string[] Refs, bool NoEngine)> entry in Expected)
            {
                Assert.AreEqual(entry.Value.NoEngine, Get(entry.Key).NoEngineReferences, entry.Key + "의 noEngineReferences");
            }
            Assert.IsFalse(Get(Bootstrap).NoEngineReferences);
        }

        [Test]
        public void PureAssemblies_AreFourExactly()
        {
            // 규칙 2: Domain, WorldGen, Application, Adapters.Persistence만 순수 C#
            var pure = RuntimeGameAsmdefs().Where(a => a.NoEngineReferences).Select(a => a.Name).OrderBy(n => n, StringComparer.Ordinal);
            CollectionAssert.AreEqual(new[] { Persistence, App, Domain, WorldGen }.OrderBy(n => n, StringComparer.Ordinal), pure);
        }

        // =========================================================
        // 2) 의존 방향: 안쪽으로만, 어댑터끼리·인프라끼리 금지
        // =========================================================
        [Test]
        public void Dependencies_PointInwardOnly()
        {
            foreach (AsmdefInfo asm in RuntimeGameAsmdefs())
            {
                int selfRank = Rank(asm.Name);
                foreach (string reference in asm.References.Where(r => Rank(r) > 0))
                {
                    bool ok = Rank(reference) < selfRank || (asm.Name == WorldGen && reference == Domain);
                    Assert.IsTrue(ok, asm.Name + " -> " + reference + " : 바깥 레이어를 참조하거나 같은 층을 참조합니다.");
                }
            }
        }

        [Test]
        public void Adapters_DoNotReferenceEachOther()
        {
            string[] adapters = { Persistence, Net, Presentation };
            foreach (string adapter in adapters)
            {
                foreach (string other in adapters.Where(o => o != adapter))
                {
                    CollectionAssert.DoesNotContain(Get(adapter).References, other, adapter + " -> " + other);
                }
            }
        }

        [Test]
        public void InfrastructureAssemblies_DoNotReferenceEachOther()
        {
            string[] infra = { Steam, Transport, AiJobs, Config, Platform, Rendering };
            foreach (string asm in infra)
            {
                foreach (string other in infra.Where(o => o != asm))
                {
                    CollectionAssert.DoesNotContain(Get(asm).References, other, asm + " -> " + other);
                }
            }
        }

        // =========================================================
        // 3) 컴파일된 어셈블리가 실제로 순수 C#인지 (반사)
        // =========================================================
        [TestCase(Domain)]
        [TestCase(WorldGen)]
        [TestCase(App)]
        [TestCase(Persistence)]
        public void PureAssemblies_CompiledReferences_AreOnlyBclAndAllowedGameAssemblies(string name)
        {
            Assembly assembly = FindLoaded(name);
            string[] allowedGame = Get(name).References.ToArray();

            foreach (AssemblyName referenced in assembly.GetReferencedAssemblies())
            {
                string n = referenced.Name;
                bool bcl = n == "mscorlib" || n == "netstandard" || n.StartsWith("System", StringComparison.Ordinal);
                Assert.IsTrue(bcl || allowedGame.Contains(n),
                    name + "이(가) 허용되지 않은 어셈블리를 참조합니다: " + n + " (UnityEngine, FishNet, Steamworks 참조 금지)");
            }
        }

        [Test]
        public void DomainAndApplication_DoNotUseForbiddenNamespaces()
        {
            string[] forbidden = { "UnityEngine", "UnityEditor", "FishNet", "Steamworks", "FishySteamworks" };
            foreach (string name in new[] { Domain, WorldGen, App, Persistence })
            {
                foreach (AssemblyName referenced in FindLoaded(name).GetReferencedAssemblies())
                {
                    foreach (string prefix in forbidden)
                    {
                        Assert.IsFalse(referenced.Name.StartsWith(prefix, StringComparison.Ordinal), name + " -> " + referenced.Name);
                    }
                }
            }
        }

        private static Assembly FindLoaded(string name)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
            Assert.IsNotNull(assembly, "어셈블리가 로드되어 있지 않습니다(컴파일 오류?): " + name);
            return assembly;
        }

        // =========================================================
        // 4) 렌더링 격리: HDRP 참조는 Game.Infra.Rendering만 (SDD 3.4.4, 규칙 7)
        // =========================================================
        [Test]
        public void Rendering_OnlyInfraRenderingReferencesRenderPipelines_Declared()
        {
            foreach (AsmdefInfo asm in All.Where(a => a.Name.StartsWith("Game.", StringComparison.Ordinal) && a.Name != Rendering))
            {
                CollectionAssert.IsEmpty(asm.References.Where(r => r.StartsWith(RenderPipelinePrefix, StringComparison.Ordinal)),
                    asm.Name + "이(가) 렌더 파이프라인 어셈블리를 참조합니다. HDRP 의존 코드는 Game.Infra.Rendering에만 둡니다.");
            }
            Assert.IsTrue(Get(Rendering).References.Contains(HdrpRuntime), "Game.Infra.Rendering은 HDRP를 참조해야 합니다.");
        }

        [Test]
        public void Rendering_OnlyInfraRenderingReferencesRenderPipelines_Compiled()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()
                         .Where(a => a.GetName().Name.StartsWith("Game.", StringComparison.Ordinal) && a.GetName().Name != Rendering))
            {
                foreach (AssemblyName referenced in assembly.GetReferencedAssemblies())
                {
                    Assert.IsFalse(referenced.Name.StartsWith(RenderPipelinePrefix, StringComparison.Ordinal),
                        assembly.GetName().Name + " -> " + referenced.Name);
                }
            }
        }

        // =========================================================
        // 5) 네트워크 라이브러리 위치 (규칙 3)
        // =========================================================
        [Test]
        public void FishNet_OnlyInNetTransportBootstrap()
        {
            string[] allowed = { Net, Transport, Bootstrap };
            foreach (AsmdefInfo asm in RuntimeGameAsmdefs().Where(a => a.References.Contains(FishNet)))
            {
                CollectionAssert.Contains(allowed, asm.Name, asm.Name + "이(가) FishNet을 참조합니다.");
            }
        }

        [Test]
        public void SteamworksNet_OnlyInSteamAndBootstrap()
        {
            string[] allowed = { Steam, Bootstrap };
            foreach (AsmdefInfo asm in RuntimeGameAsmdefs().Where(a => a.References.Contains(SteamworksNet)))
            {
                CollectionAssert.Contains(allowed, asm.Name, asm.Name + "이(가) Steamworks.NET을 참조합니다.");
            }
        }

        [Test]
        public void FishySteamworks_OnlyInTransportAndBootstrap()
        {
            string[] allowed = { Transport, Bootstrap };
            foreach (AsmdefInfo asm in RuntimeGameAsmdefs().Where(a => a.References.Contains(FishySteamworks)))
            {
                CollectionAssert.Contains(allowed, asm.Name, asm.Name + "이(가) FishySteamworks를 참조합니다.");
            }
        }

        // =========================================================
        // 6) 모든 게임 코드는 asmdef 안에 (Assembly-CSharp 금지, 규칙 6)
        // =========================================================
        [Test]
        public void NoScriptsOutsideAssemblyDefinitions()
        {
            string assets = AsmdefModel.AssetsFolder;
            var orphans = new List<string>();
            foreach (string script in Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories))
            {
                if (!HasAsmdefAncestor(Path.GetDirectoryName(script), assets))
                {
                    orphans.Add(script.Replace('\\', '/'));
                }
            }
            CollectionAssert.IsEmpty(orphans, "asmdef 밖의 스크립트(Assembly-CSharp)가 있습니다:\n" + string.Join("\n", orphans));
        }

        private static bool HasAsmdefAncestor(string directory, string stopAt)
        {
            string current = Path.GetFullPath(directory);
            string root = Path.GetFullPath(stopAt);
            while (current.Length >= root.Length)
            {
                if (Directory.GetFiles(current, "*.asmdef").Length > 0 || Directory.GetFiles(current, "*.asmref").Length > 0)
                {
                    return true;
                }
                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || parent == current)
                {
                    break;
                }
                current = parent;
            }
            return false;
        }

        // =========================================================
        // 7) Unity Pipeline 패키지(에디터 조작 MCP용) 격리 (SDD 3.4.3, v0.7)
        // =========================================================
        [Test]
        public void UnityPipeline_WhenInstalled_OnlyAttributesAssemblyMayBeNonEditor()
        {
            string manifest = File.ReadAllText(Path.Combine(Directory.GetParent(AsmdefModel.AssetsFolder).FullName, "Packages", "manifest.json"));
            if (!manifest.Contains("\"" + PipelinePackageName + "\""))
            {
                Assert.Pass("Unity Pipeline 패키지가 설치되어 있지 않습니다.");
            }

            UnityEditor.PackageManager.PackageInfo info = UnityEditor.PackageManager.PackageInfo.FindForPackageName(PipelinePackageName);
            Assert.IsNotNull(info, "manifest에는 있으나 패키지를 해석하지 못했습니다.");

            // Unity.Pipeline.Attributes(속성 정의, 런타임 기능 없음)만 예외. 나머지는 includePlatforms: Editor이거나,
            // 플레이어에 들어가려면 ENABLE_RUNTIME_PIPELINE* 기호가 필요한 어셈블리다(그 기호는 아래 시험이 금지).
            var offenders = AsmdefModel.LoadFrom(info.resolvedPath)
                .Where(a => a.Name != PipelineAllowedRuntimeAssembly)
                .Where(a => !(a.IncludePlatforms.Count == 1 && a.IncludePlatforms[0] == "Editor"))
                .Where(a => !a.DefineConstraints.Any(c => c.Contains(RuntimePipelineSymbolPrefix)))
                .Select(a => a.Name)
                .ToList();
            CollectionAssert.IsEmpty(offenders,
                "에디터 전용도 아니고 런타임 기호로 보호되지도 않는 Unity Pipeline 어셈블리가 있습니다(패키지 버전을 올린 뒤 재검토 필요): " + string.Join(", ", offenders));
        }

        [Test]
        public void GameAssemblies_NeverReferenceUnityPipeline()
        {
            foreach (AsmdefInfo asm in All.Where(a => a.Name.StartsWith("Game.", StringComparison.Ordinal)))
            {
                CollectionAssert.IsEmpty(asm.References.Where(r => r.StartsWith(PipelineAssemblyPrefix, StringComparison.Ordinal)), asm.Name);
            }
        }

        [Test]
        public void PlayerAssemblies_ContainNoPipelineEditorOrTestAssemblies()
        {
            string[] player = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies).Select(a => a.name).ToArray();
            CollectionAssert.IsEmpty(
                player.Where(n => n.StartsWith(PipelineAssemblyPrefix, StringComparison.Ordinal) && n != PipelineAllowedRuntimeAssembly),
                "플레이어 빌드에 Unity Pipeline 런타임 어셈블리(Unity.Pipeline, IlInterpreter)가 들어갑니다.");
            CollectionAssert.DoesNotContain(player, EditorTools, "편집기 도구가 플레이어 빌드에 들어갑니다.");
            Assert.IsFalse(player.Any(n => n.StartsWith("Game.Tests.", StringComparison.Ordinal)), "시험 어셈블리가 플레이어 빌드에 들어갑니다.");
            foreach (string runtimeAssembly in Expected.Keys.Concat(new[] { Bootstrap }))
            {
                CollectionAssert.Contains(player, runtimeAssembly, runtimeAssembly + "이(가) 플레이어 어셈블리에 없습니다.");
            }
        }

        [Test]
        public void ScriptingDefines_AllPlatforms_DoNotEnableRuntimePipeline()
        {
            // 모든 빌드 대상 그룹의 Scripting Define Symbols를 검사한다.
            foreach (BuildTargetGroup group in Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (group == BuildTargetGroup.Unknown || IsObsolete(group))
                {
                    continue;
                }
                string defines;
                try
                {
                    defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group));
                }
                catch (Exception)
                {
                    continue; // 이 Unity에서 지원하지 않는 그룹
                }
                StringAssert.DoesNotContain(RuntimePipelineSymbolPrefix, defines ?? string.Empty,
                    group + " 플랫폼에 런타임 Pipeline을 켜는 기호가 있습니다.");
            }
        }

        [Test]
        public void ProjectFiles_DoNotMentionRuntimePipelineSymbols()
        {
            // csc.rsp, mcs.rsp, *.rsp, Directory.Build.* 와 ProjectSettings(기호가 저장되는 곳)를 훑는다.
            string projectRoot = Directory.GetParent(AsmdefModel.AssetsFolder).FullName;
            var files = new List<string>();
            files.AddRange(Directory.GetFiles(AsmdefModel.AssetsFolder, "*.rsp", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(projectRoot, "*.rsp", SearchOption.TopDirectoryOnly));
            files.AddRange(Directory.GetFiles(projectRoot, "Directory.Build.*", SearchOption.TopDirectoryOnly));
            files.Add(Path.Combine(projectRoot, "ProjectSettings", "ProjectSettings.asset"));

            foreach (string file in files.Where(File.Exists))
            {
                StringAssert.DoesNotContain(RuntimePipelineSymbolPrefix, File.ReadAllText(file), file);
            }
        }

        private static bool IsObsolete(BuildTargetGroup group)
        {
            FieldInfo field = typeof(BuildTargetGroup).GetField(group.ToString());
            return field != null && field.GetCustomAttributes(typeof(ObsoleteAttribute), false).Length > 0;
        }

        // =========================================================
        // 8) 프로젝트 설정과 버전 고정 (NFR-027~029, NFR-049)
        // =========================================================
        [Test]
        public void ProjectSettings_ForceText_Il2cppStandalone_LinearColor()
        {
            Assert.AreEqual(SerializationMode.ForceText, EditorSettings.serializationMode, "직렬화는 Force Text여야 합니다(NFR-027).");
            Assert.AreEqual(ScriptingImplementation.IL2CPP, PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone), "출시·측정 빌드는 IL2CPP(D-03).");
            Assert.AreEqual(ColorSpace.Linear, PlayerSettings.colorSpace, "HDRP는 선형 색 공간이 필요합니다.");
        }

        [Test]
        public void UnityEditorVersion_IsPinnedTo_6000_3_27f2()
        {
            Assert.AreEqual("6000.3.27f2", Application.unityVersion, "D-03: 엔진 버전은 6000.3.27f2로 고정입니다.");
        }

        [Test]
        public void Manifest_PinsExactVersions_NoRangesOrLatest()
        {
            string manifest = File.ReadAllText(Path.Combine(Directory.GetParent(AsmdefModel.AssetsFolder).FullName, "Packages", "manifest.json"));
            StringAssert.Contains("com.unity.render-pipelines.high-definition\": \"17.3.", manifest, "HDRP는 6000.3에 묶인 17.3.x여야 합니다(D-03).");
            StringAssert.Contains("#2025.165.0\"", manifest, "Steamworks.NET은 태그 2025.165.0으로 고정입니다(NFR-049).");
            StringAssert.DoesNotContain("\"latest\"", manifest);
            StringAssert.DoesNotContain("\"^", manifest);
            StringAssert.DoesNotContain("\"~", manifest);
        }

        [Test]
        public void ThirdPartyNetworkLibraries_AreAtPinnedVersions()
        {
            string assets = AsmdefModel.AssetsFolder;
            StringAssert.Contains("\"version\": \"4.7.4\"", File.ReadAllText(Path.Combine(assets, "ThirdParty", "FishNet", "package.json")), "FishNet 4.7.4R");
            StringAssert.Contains("\"version\": \"4.1.1\"", File.ReadAllText(Path.Combine(assets, "ThirdParty", "FishySteamworks", "package.json")), "FishySteamworks 4.1.1");
        }

        [Test]
        public void SteamAppIdFile_ContainsDevelopmentAppId480()
        {
            string projectRoot = Directory.GetParent(AsmdefModel.AssetsFolder).FullName;
            Assert.AreEqual("480", File.ReadAllText(Path.Combine(projectRoot, "steam_appid.txt")).Trim(), "NFR-009: 개발 App ID 480");
        }
    }
}

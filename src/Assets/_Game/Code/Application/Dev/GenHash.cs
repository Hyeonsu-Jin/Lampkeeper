// =============================================================
// 파일: GenHash.cs
// 역할: 결정론 비교 도구의 순수 부분. 시드 목록을 읽고, 월드를 생성해 요약값 보고서를 만들고,
//       여러 보고서(다른 PC·다른 런타임)를 비교해 어긋난 시드와 타일을 알려 준다.
//       명령줄 -genhash / -genhash-compare 해석도 여기서 한다. 파일 읽기·쓰기와 종료 코드는 Bootstrap이 맡는다.
// 레이어: application (순수 C#)
// 관련: NFR-053, FR-169, DES-113 / SDD 3.7.4, M1-04
//
// 사용 예 (개발 빌드)
//   Player.exe -batchmode -nographics -genhash seeds.txt -out mono.json [-genver V0] [-genprofile full|small]
//   Player.exe -batchmode -nographics -genhash-compare mono.json il2cpp.json [다른PC.json ...]
// =============================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Game.WorldGen.Common;
using Game.WorldGen.Hashing;
using Game.WorldGen.Model;
using Game.WorldGen.V0;

namespace Game.Application.Dev
{
    /// <summary>-genhash 계열 명령의 종류.</summary>
    public enum GenHashMode
    {
        /// <summary>해당 옵션 없음.</summary>
        None = 0,

        /// <summary>시드 목록을 생성해 보고서를 쓴다.</summary>
        Generate = 1,

        /// <summary>보고서 파일들을 비교한다.</summary>
        Compare = 2,
    }

    /// <summary>명령줄에서 읽은 -genhash 옵션.</summary>
    public sealed class GenHashOptions
    {
        public const string GenerateFlag = "-genhash";
        public const string CompareFlag = "-genhash-compare";
        public const string OutFlag = "-out";
        public const string VersionFlag = "-genver";
        public const string ProfileFlag = "-genprofile";

        /// <summary>실행 종류.</summary>
        public GenHashMode Mode { get; }

        /// <summary>시드 목록 파일 경로(Generate).</summary>
        public string SeedsPath { get; }

        /// <summary>보고서 출력 경로(Generate).</summary>
        public string OutPath { get; }

        /// <summary>비교할 보고서 경로들(Compare).</summary>
        public IReadOnlyList<string> ComparePaths { get; }

        /// <summary>생성기 버전 이름. 기본 V0.</summary>
        public string GeneratorVersion { get; }

        /// <summary>프로필 이름(full 또는 small). 기본 full.</summary>
        public string ProfileName { get; }

        /// <summary>해석 오류 설명. 비어 있지 않으면 실행하지 않는다.</summary>
        public IReadOnlyList<string> Errors { get; }

        private GenHashOptions(GenHashMode mode, string seedsPath, string outPath, IReadOnlyList<string> comparePaths, string version, string profile, IReadOnlyList<string> errors)
        {
            Mode = mode;
            SeedsPath = seedsPath;
            OutPath = outPath;
            ComparePaths = comparePaths;
            GeneratorVersion = version;
            ProfileName = profile;
            Errors = errors;
        }

        /// <summary>명령줄 인자를 해석한다. 관련 없는 인자는 무시한다.</summary>
        public static GenHashOptions Parse(IReadOnlyList<string> args)
        {
            var errors = new List<string>();
            var comparePaths = new List<string>();
            GenHashMode mode = GenHashMode.None;
            string seeds = null;
            string outPath = null;
            string version = V0WorldGenerator.Version;
            string profile = "full";

            for (int i = 0; args != null && i < args.Count; i++)
            {
                string arg = args[i] ?? string.Empty;
                if (Is(arg, CompareFlag))
                {
                    mode = GenHashMode.Compare;
                    while (i + 1 < args.Count && !string.IsNullOrEmpty(args[i + 1]) && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    {
                        comparePaths.Add(args[++i]);
                    }
                }
                else if (Is(arg, GenerateFlag))
                {
                    if (mode != GenHashMode.Compare) mode = GenHashMode.Generate;
                    seeds = ReadValue(args, ref i, GenerateFlag, errors);
                }
                else if (Is(arg, OutFlag)) outPath = ReadValue(args, ref i, OutFlag, errors);
                else if (Is(arg, VersionFlag)) version = ReadValue(args, ref i, VersionFlag, errors) ?? version;
                else if (Is(arg, ProfileFlag)) profile = (ReadValue(args, ref i, ProfileFlag, errors) ?? profile).ToLowerInvariant();
            }

            if (mode == GenHashMode.Generate)
            {
                if (seeds == null) errors.Add("-genhash 뒤에 시드 목록 파일 경로가 필요합니다.");
                if (outPath == null) errors.Add("-genhash 는 -out <보고서.json> 이 필요합니다.");
                if (!string.Equals(version, V0WorldGenerator.Version, StringComparison.Ordinal)) errors.Add("지원하는 생성기 버전은 V0 뿐입니다: " + version);
                if (profile != "full" && profile != "small") errors.Add("-genprofile 값은 full 또는 small 이어야 합니다: " + profile);
            }
            else if (mode == GenHashMode.Compare && comparePaths.Count < 2)
            {
                errors.Add("-genhash-compare 는 보고서 파일을 2개 이상 받아야 합니다.");
            }
            return new GenHashOptions(mode, seeds, outPath, comparePaths, version, profile, errors);
        }

        private static bool Is(string actual, string expected) => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

        private static string ReadValue(IReadOnlyList<string> args, ref int index, string flag, List<string> errors)
        {
            if (index + 1 >= args.Count || string.IsNullOrEmpty(args[index + 1]) || args[index + 1].StartsWith("-", StringComparison.Ordinal))
            {
                errors.Add(flag + " 뒤에 값이 필요합니다.");
                return null;
            }
            return args[++index];
        }
    }

    /// <summary>시드 목록 파일(한 줄에 시드 하나, # 로 시작하는 줄은 주석) 해석.</summary>
    public static class SeedListParser
    {
        /// <summary>줄 단위로 시드 글자를 모은다. 앞뒤 공백은 시드 코덱 규칙대로 처리되도록 그대로 둔다.</summary>
        public static List<string> Parse(string content)
        {
            var list = new List<string>();
            foreach (string raw in (content ?? string.Empty).Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (SeedCodec.TrimAsciiWhitespace(line).Length == 0 || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }
                list.Add(line);
            }
            return list;
        }
    }

    /// <summary>시드 하나의 생성 결과 요약.</summary>
    public sealed class GenHashEntry
    {
        public string SeedText { get; set; }
        public ulong Seed { get; set; }
        public ulong Hash { get; set; }
        public int Attempt { get; set; }
        public int ResourceCount { get; set; }
        public int TowerSiteCount { get; set; }
        public long ElapsedMs { get; set; }
        public ulong ResourcesHash { get; set; }
        public ulong TowerSitesHash { get; set; }
        public List<ulong> RegionHashes { get; set; } = new List<ulong>();
        public List<ulong> TileHashes { get; set; } = new List<ulong>();
    }

    /// <summary>-genhash 보고서.</summary>
    public sealed class GenHashReport
    {
        /// <summary>보고서 형식 버전.</summary>
        public const int FormatVersion = 1;

        public string GeneratorVersion { get; set; } = V0WorldGenerator.Version;
        public string Profile { get; set; } = "full";

        /// <summary>실행 환경 설명(런타임 종류, Unity 버전, CPU 등). 비교 결과에 보여 주기만 한다.</summary>
        public string Runtime { get; set; } = string.Empty;
        public string Unity { get; set; } = string.Empty;
        public string Cpu { get; set; } = string.Empty;
        public string Machine { get; set; } = string.Empty;

        public List<GenHashEntry> Entries { get; } = new List<GenHashEntry>();

        /// <summary>JSON 글자로 쓴다. 해시 값은 16자리 16진수 글자다(JSON 숫자는 64비트를 정확히 담지 못한다).</summary>
        public string ToJson()
        {
            var b = new StringBuilder();
            b.Append("{\n");
            b.Append("  \"format\": ").Append(FormatVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            b.Append("  \"generator\": ").Append(MiniJson.Quote(GeneratorVersion)).Append(",\n");
            b.Append("  \"profile\": ").Append(MiniJson.Quote(Profile)).Append(",\n");
            b.Append("  \"runtime\": ").Append(MiniJson.Quote(Runtime)).Append(",\n");
            b.Append("  \"unity\": ").Append(MiniJson.Quote(Unity)).Append(",\n");
            b.Append("  \"cpu\": ").Append(MiniJson.Quote(Cpu)).Append(",\n");
            b.Append("  \"machine\": ").Append(MiniJson.Quote(Machine)).Append(",\n");
            b.Append("  \"entries\": [\n");
            for (int i = 0; i < Entries.Count; i++)
            {
                GenHashEntry e = Entries[i];
                b.Append("    {\n");
                b.Append("      \"seedText\": ").Append(MiniJson.Quote(e.SeedText)).Append(",\n");
                b.Append("      \"seed\": \"").Append(Hex(e.Seed)).Append("\",\n");
                b.Append("      \"hash\": \"").Append(Hex(e.Hash)).Append("\",\n");
                b.Append("      \"attempt\": ").Append(e.Attempt.ToString(CultureInfo.InvariantCulture)).Append(",\n");
                b.Append("      \"resourceCount\": ").Append(e.ResourceCount.ToString(CultureInfo.InvariantCulture)).Append(",\n");
                b.Append("      \"towerSiteCount\": ").Append(e.TowerSiteCount.ToString(CultureInfo.InvariantCulture)).Append(",\n");
                b.Append("      \"elapsedMs\": ").Append(e.ElapsedMs.ToString(CultureInfo.InvariantCulture)).Append(",\n");
                b.Append("      \"resourcesHash\": \"").Append(Hex(e.ResourcesHash)).Append("\",\n");
                b.Append("      \"towerSitesHash\": \"").Append(Hex(e.TowerSitesHash)).Append("\",\n");
                b.Append("      \"regionHashes\": ").Append(HexArray(e.RegionHashes)).Append(",\n");
                b.Append("      \"tileHashes\": ").Append(HexArray(e.TileHashes)).Append("\n");
                b.Append("    }").Append(i + 1 < Entries.Count ? ",\n" : "\n");
            }
            b.Append("  ]\n}\n");
            return b.ToString();
        }

        /// <summary>JSON 글자에서 보고서를 읽는다.</summary>
        /// <exception cref="MiniJsonException">형식이 틀릴 때</exception>
        public static GenHashReport FromJson(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            if (root == null) throw new MiniJsonException("보고서 최상위가 객체가 아닙니다.");

            var report = new GenHashReport
            {
                GeneratorVersion = MiniJson.GetString(root, "generator"),
                Profile = MiniJson.GetString(root, "profile"),
                Runtime = MiniJson.GetString(root, "runtime"),
                Unity = MiniJson.GetString(root, "unity"),
                Cpu = MiniJson.GetString(root, "cpu"),
                Machine = MiniJson.GetString(root, "machine"),
            };
            List<object> entries = MiniJson.GetArray(root, "entries") ?? throw new MiniJsonException("entries 배열이 없습니다.");
            foreach (object item in entries)
            {
                var o = item as Dictionary<string, object> ?? throw new MiniJsonException("entries 항목이 객체가 아닙니다.");
                report.Entries.Add(new GenHashEntry
                {
                    SeedText = MiniJson.GetString(o, "seedText"),
                    Seed = ParseHex(MiniJson.GetString(o, "seed")),
                    Hash = ParseHex(MiniJson.GetString(o, "hash")),
                    Attempt = (int)MiniJson.GetLong(o, "attempt"),
                    ResourceCount = (int)MiniJson.GetLong(o, "resourceCount"),
                    TowerSiteCount = (int)MiniJson.GetLong(o, "towerSiteCount"),
                    ElapsedMs = MiniJson.GetLong(o, "elapsedMs"),
                    ResourcesHash = ParseHex(MiniJson.GetString(o, "resourcesHash")),
                    TowerSitesHash = ParseHex(MiniJson.GetString(o, "towerSitesHash")),
                    RegionHashes = ReadHexArray(MiniJson.GetArray(o, "regionHashes")),
                    TileHashes = ReadHexArray(MiniJson.GetArray(o, "tileHashes")),
                });
            }
            return report;
        }

        /// <summary>64비트 값을 16자리 소문자 16진수로.</summary>
        public static string Hex(ulong value) => value.ToString("x16", CultureInfo.InvariantCulture);

        private static ulong ParseHex(string text)
        {
            if (!ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value))
            {
                throw new MiniJsonException("16진수 해시 형식이 틀립니다: " + text);
            }
            return value;
        }

        private static string HexArray(List<ulong> values)
        {
            var b = new StringBuilder("[");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) b.Append(", ");
                b.Append('"').Append(Hex(values[i])).Append('"');
            }
            return b.Append(']').ToString();
        }

        private static List<ulong> ReadHexArray(List<object> array)
        {
            var list = new List<ulong>();
            if (array != null)
            {
                foreach (object item in array) list.Add(ParseHex(item as string));
            }
            return list;
        }
    }

    /// <summary>시드 목록을 생성해 보고서를 만드는 실행기.</summary>
    public static class GenHashRunner
    {
        /// <summary>
        /// 시드마다 월드를 만들고 요약값을 계산한다.
        /// </summary>
        /// <param name="seedTexts">시드 글자 목록</param>
        /// <param name="profileName">full 또는 small</param>
        /// <param name="maxDegreeOfParallelism">병렬 작업 수(-1 기본)</param>
        /// <returns>환경 설명(Runtime 등)은 비워 둔다. 호출한 쪽이 채운다.</returns>
        /// <exception cref="ArgumentException">시드 글자가 올바르지 않을 때</exception>
        public static GenHashReport Run(IEnumerable<string> seedTexts, string profileName, int maxDegreeOfParallelism = -1)
        {
            V0Profile profile = string.Equals(profileName, "small", StringComparison.OrdinalIgnoreCase) ? V0Profile.Small : V0Profile.Full;
            var report = new GenHashReport { GeneratorVersion = V0WorldGenerator.Version, Profile = profile == V0Profile.Small ? "small" : "full" };
            foreach (string text in seedTexts)
            {
                ulong seed = SeedCodec.SeedFromText(text);
                var watch = Stopwatch.StartNew();
                GeneratedWorld world = V0WorldGenerator.Generate(seed, profile, 0, maxDegreeOfParallelism);
                WorldHash hash = WorldHasher.Compute(world);
                watch.Stop();

                report.Entries.Add(new GenHashEntry
                {
                    SeedText = text,
                    Seed = seed,
                    Hash = hash.Total,
                    Attempt = world.Attempt,
                    ResourceCount = world.Resources.Length,
                    TowerSiteCount = world.TowerSites.Length,
                    ElapsedMs = watch.ElapsedMilliseconds,
                    ResourcesHash = hash.ResourcesHash,
                    TowerSitesHash = hash.TowerSitesHash,
                    RegionHashes = new List<ulong>(hash.RegionHashes),
                    TileHashes = new List<ulong>(hash.TileHashes),
                });
            }
            return report;
        }
    }

    /// <summary>한 시드의 불일치 내용.</summary>
    public sealed class GenHashMismatch
    {
        public string SeedText { get; set; }

        /// <summary>기준(첫 번째) 보고서와 다른 보고서의 번호(0부터).</summary>
        public int ReportIndex { get; set; }

        public ulong ExpectedHash { get; set; }
        public ulong ActualHash { get; set; }

        /// <summary>다른 타일 번호들(Z행 우선).</summary>
        public List<int> DifferentTiles { get; } = new List<int>();

        public bool ResourcesDiffer { get; set; }
        public bool TowerSitesDiffer { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    /// <summary>비교 결과.</summary>
    public sealed class GenHashComparison
    {
        public int SeedsCompared { get; set; }
        public List<GenHashMismatch> Mismatches { get; } = new List<GenHashMismatch>();
        public bool AllMatch => Mismatches.Count == 0;

        /// <summary>사람이 읽는 요약 글자.</summary>
        public string Describe(IReadOnlyList<string> reportNames)
        {
            var b = new StringBuilder();
            b.Append("비교한 시드 ").Append(SeedsCompared).Append("개, 보고서 ").Append(reportNames.Count).Append("개: ");
            if (AllMatch)
            {
                return b.Append("모두 일치").ToString();
            }
            b.Append("불일치 ").Append(Mismatches.Count).Append("건");
            foreach (GenHashMismatch m in Mismatches)
            {
                b.Append("\n - 시드 '").Append(m.SeedText).Append("' : ").Append(reportNames[0]).Append("=").Append(GenHashReport.Hex(m.ExpectedHash))
                 .Append(" / ").Append(reportNames[m.ReportIndex]).Append("=").Append(GenHashReport.Hex(m.ActualHash));
                if (m.Note.Length > 0) b.Append(" (").Append(m.Note).Append(")");
                if (m.DifferentTiles.Count > 0) b.Append(", 다른 타일 ").Append(m.DifferentTiles.Count).Append("개 (처음 ").Append(m.DifferentTiles[0]).Append("번)");
                if (m.ResourcesDiffer) b.Append(", 자원 표 다름");
                if (m.TowerSitesDiffer) b.Append(", 등광탑 후보 자리 다름");
            }
            return b.ToString();
        }
    }

    /// <summary>보고서 비교기.</summary>
    public static class GenHashComparer
    {
        /// <summary>
        /// 첫 번째 보고서를 기준으로 나머지를 비교한다. 같은 시드 글자끼리 요약값이 같아야 한다.
        /// </summary>
        public static GenHashComparison Compare(IReadOnlyList<GenHashReport> reports)
        {
            var result = new GenHashComparison();
            if (reports == null || reports.Count < 2)
            {
                return result;
            }
            GenHashReport baseline = reports[0];
            foreach (GenHashEntry expected in baseline.Entries)
            {
                result.SeedsCompared++;
                for (int r = 1; r < reports.Count; r++)
                {
                    GenHashEntry actual = Find(reports[r], expected.SeedText);
                    var mismatch = new GenHashMismatch { SeedText = expected.SeedText, ReportIndex = r, ExpectedHash = expected.Hash };
                    if (actual == null)
                    {
                        mismatch.Note = "이 보고서에 해당 시드 없음";
                        result.Mismatches.Add(mismatch);
                        continue;
                    }
                    mismatch.ActualHash = actual.Hash;
                    if (reports[r].Profile != baseline.Profile || reports[r].GeneratorVersion != baseline.GeneratorVersion)
                    {
                        mismatch.Note = "프로필/생성기 버전이 다름(" + reports[r].Profile + "/" + reports[r].GeneratorVersion + ")";
                        result.Mismatches.Add(mismatch);
                        continue;
                    }
                    if (actual.Hash == expected.Hash)
                    {
                        continue;
                    }
                    int tiles = Math.Min(expected.TileHashes.Count, actual.TileHashes.Count);
                    for (int t = 0; t < tiles; t++)
                    {
                        if (expected.TileHashes[t] != actual.TileHashes[t]) mismatch.DifferentTiles.Add(t);
                    }
                    mismatch.ResourcesDiffer = expected.ResourcesHash != actual.ResourcesHash;
                    mismatch.TowerSitesDiffer = expected.TowerSitesHash != actual.TowerSitesHash;
                    result.Mismatches.Add(mismatch);
                }
            }
            return result;
        }

        private static GenHashEntry Find(GenHashReport report, string seedText)
        {
            foreach (GenHashEntry e in report.Entries)
            {
                if (string.Equals(e.SeedText, seedText, StringComparison.Ordinal)) return e;
            }
            return null;
        }
    }
}

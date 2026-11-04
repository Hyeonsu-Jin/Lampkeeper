// =============================================================
// 파일: UiAndInputRulesTests.cs
// 역할: (1) USS 규칙 검사: 화면 USS·UXML·UI 코드에 색 값·고정 글자 크기를 직접 쓰지 않았는지, Base.uss의 접근성 기반(색 토큰, 글자 크기 3단계,
//       모양 구분 짝, 다크 단일 테마)이 갖춰졌는지. (2) Input Actions 애셋이 STORYBOARD 2.4 기본 할당표와 같은지.
//       (3) 빌드 번호 해석. (4) 텍스트 표(ko/en) 키 일치.
// 레이어: test (EditMode, Game.Tests.Infrastructure)
// 관련: FR-117, FR-129, FR-130, FR-166, NFR-034, NFR-044, NFR-056, NFR-057, DES-091, DES-092, DES-095 / SDD 3.7.9, 3.7.10 / STORYBOARD 2.1, 2.2, 2.4, 2.7
// =============================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Application.Dev;
using Game.Bootstrap.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Infrastructure
{
    public class UssRulesTests
    {
        private static string UiFolder => Path.Combine(UnityEngine.Application.dataPath, "_Game", "UI");
        private static string BaseUssPath => Path.Combine(UiFolder, "Common", "Base.uss");

        private static IEnumerable<string> ScreenUssFiles() =>
            Directory.GetFiles(UiFolder, "*.uss", SearchOption.AllDirectories)
                .Where(p => !string.Equals(Path.GetFullPath(p), Path.GetFullPath(BaseUssPath), StringComparison.OrdinalIgnoreCase));

        /// <summary>USS 속성 선언(속성 이름, 값)을 모두 뽑는다. 주석은 지운다.</summary>
        private static IEnumerable<(string Property, string Value, string File)> Declarations(string path)
        {
            string text = Regex.Replace(File.ReadAllText(path), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            foreach (Match m in Regex.Matches(text, @"([a-zA-Z\-]+)\s*:\s*([^;{}]+);"))
            {
                yield return (m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim(), Path.GetFileName(path));
            }
        }

        private static readonly Regex ColorLiteral = new Regex(@"(^|[\s,(])(#[0-9a-fA-F]{3,8}\b|rgb\(|rgba\(|hsl\(|hsla\()", RegexOptions.IgnoreCase);
        private static readonly string[] ColorProperties =
        {
            "color", "background-color", "border-color", "border-top-color", "border-right-color", "border-bottom-color", "border-left-color",
            "-unity-background-image-tint-color", "-unity-text-outline-color", "text-shadow",
        };
        private static readonly string[] NamedColors = { "white", "black", "red", "green", "blue", "yellow", "gray", "grey", "orange", "cyan", "magenta", "transparent" };

        [Test]
        public void ScreenUss_UseNoColorLiterals_OnlyTokens()
        {
            // [NFR-056] 화면에서 색 값 직접 사용 금지: 색은 var(--color-*) 토큰만.
            foreach (string file in ScreenUssFiles())
            {
                foreach ((string property, string value, string name) in Declarations(file))
                {
                    Assert.IsFalse(ColorLiteral.IsMatch(value), name + ": '" + property + ": " + value + "' 색 값을 직접 썼습니다.");
                    if (ColorProperties.Contains(property))
                    {
                        Assert.IsTrue(value.StartsWith("var(--color-", StringComparison.Ordinal) || value == "initial" || value == "inherit",
                            name + ": '" + property + ": " + value + "' 색 속성은 var(--color-*) 토큰이어야 합니다.");
                        Assert.IsFalse(NamedColors.Contains(value), name + ": 색 이름 직접 사용 금지");
                    }
                }
            }
        }

        [Test]
        public void ScreenUss_FontSizesComeFromVariables_NoFixedPixels()
        {
            // [FR-129, NFR-056] 글자 크기는 배율 변수(--fs-*)로만 지정한다. 고정 px 금지.
            foreach (string file in ScreenUssFiles())
            {
                foreach ((string property, string value, string name) in Declarations(file))
                {
                    if (property == "font-size")
                    {
                        Assert.IsTrue(value.StartsWith("var(--fs-", StringComparison.Ordinal), name + ": 'font-size: " + value + "' 는 var(--fs-*) 여야 합니다.");
                    }
                }
            }
        }

        [Test]
        public void Uxml_HasNoInlineColorOrFontSizeStyles()
        {
            foreach (string file in Directory.GetFiles(UiFolder, "*.uxml", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                foreach (Match m in Regex.Matches(text, "style=\"([^\"]*)\""))
                {
                    string style = m.Groups[1].Value;
                    StringAssert.DoesNotContain("color", style, Path.GetFileName(file) + ": 인라인 색");
                    StringAssert.DoesNotContain("font-size", style, Path.GetFileName(file) + ": 인라인 글자 크기");
                }
                Assert.IsFalse(ColorLiteral.IsMatch(Regex.Replace(text, "project://database[^\"]*", string.Empty)), Path.GetFileName(file) + ": 색 값 직접 사용");
            }
        }

        [Test]
        public void PresentationUiCode_HasNoColorLiterals()
        {
            string folder = Path.Combine(UnityEngine.Application.dataPath, "_Game", "Code", "Adapters", "Presentation", "UI");
            var pattern = new Regex(@"new Color(32)?\(|Color\.(white|black|red|green|blue|yellow|gray|grey|cyan|magenta)\b|StyleColor\(|style\.(color|backgroundColor)\s*=");
            foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                Assert.IsFalse(pattern.IsMatch(File.ReadAllText(file)), Path.GetFileName(file) + ": UI 코드에 색 값을 직접 쓰지 않는다(토큰만, USS 사용자 지정 속성으로 읽기).");
            }
        }

        [Test]
        public void BaseUss_DefinesTokens_ForTheStoryboardPalette()
        {
            string text = File.ReadAllText(BaseUssPath);
            string[] required =
            {
                "--color-bg", "--color-surface", "--color-text", "--color-text-dim", "--color-ally", "--color-enemy", "--color-warning", "--color-danger",
                "--color-light", "--color-pure", "--color-stamina", "--color-track", "--color-disabled", "--color-focus", "--color-primary",
            };
            foreach (string token in required)
            {
                StringAssert.Contains(token + ":", text, "색 토큰 누락: " + token);
            }
        }

        [Test]
        public void BaseUss_ColorLiterals_OnlyInsideRootTokenBlock()
        {
            string text = Regex.Replace(File.ReadAllText(BaseUssPath), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            Match root = Regex.Match(text, @":root\s*\{[^}]*\}");
            Assert.IsTrue(root.Success, ":root 토큰 블록이 있어야 합니다.");
            string outside = text.Remove(root.Index, root.Length);
            Assert.IsFalse(ColorLiteral.IsMatch(outside), "Base.uss도 :root 밖에서는 색 값을 직접 쓰지 않는다.");
        }

        [Test]
        public void BaseUss_HasThreeTextSizeSteps_WithEveryVariable_AndSmallestIsAtLeast18px()
        {
            string text = Regex.Replace(File.ReadAllText(BaseUssPath), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            string[] vars = { "--fs-caption", "--fs-body", "--fs-button", "--fs-heading", "--fs-hud" };
            foreach (string cls in new[] { ".text-small", ".text-normal", ".text-large" })
            {
                Match block = Regex.Match(text, Regex.Escape(cls) + @"\s*\{([^}]*)\}");
                Assert.IsTrue(block.Success, cls + " 클래스 누락");
                foreach (string variable in vars)
                {
                    StringAssert.Contains(variable + ":", block.Groups[1].Value, cls + "에 " + variable + " 누락");
                }
            }
            // STORYBOARD 2.2: 가장 작은 글자가 「작게」에서도 18px 이상
            Match small = Regex.Match(text, @"\.text-small\s*\{([^}]*)\}");
            foreach (Match m in Regex.Matches(small.Groups[1].Value, @"--fs-[a-z]+:\s*(\d+)px"))
            {
                Assert.GreaterOrEqual(int.Parse(m.Groups[1].Value), 18, "작게 단계의 글자는 18px 이상");
            }
            // 크게는 보통보다 커야 하고 작게는 작아야 한다.
            int Body(string cls) => int.Parse(Regex.Match(Regex.Match(text, Regex.Escape(cls) + @"\s*\{([^}]*)\}").Groups[1].Value, @"--fs-body:\s*(\d+)px").Groups[1].Value);
            Assert.Less(Body(".text-small"), Body(".text-normal"));
            Assert.Less(Body(".text-normal"), Body(".text-large"));
        }

        [Test]
        public void BaseUss_IsDarkSingleTheme_NoThemeSwitching()
        {
            // [I-12] 테마 전환 구조(라이트 토큰, 테마 선택 클래스)를 만들지 않는다.
            string text = File.ReadAllText(BaseUssPath).ToLowerInvariant();
            StringAssert.DoesNotContain(".theme-", text);
            StringAssert.DoesNotContain("light-theme", text);
            StringAssert.DoesNotContain("theme-light", text);
        }

        [Test]
        public void BaseUss_EveryColorStatusClass_HasAShapeCompanion()
        {
            // [FR-130] 색으로 구분하는 상태(.status--X)는 모양·기호 짝(.status-icon--X)이 있어야 한다.
            string text = File.ReadAllText(BaseUssPath);
            var statuses = Regex.Matches(text, @"\.status--([a-z]+)").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
            Assert.IsNotEmpty(statuses);
            foreach (string status in statuses)
            {
                StringAssert.Contains(".status-icon--" + status, text, ".status--" + status + " 의 모양 짝 누락");
            }
        }

        [Test]
        public void ButtonsHaveMinimumHeight56_AndFocusIsShownByThickBorder()
        {
            string text = File.ReadAllText(BaseUssPath);
            StringAssert.Contains("min-height: 56px", text, "STORYBOARD 2.3: 목록 행·버튼 최소 높이 56px");
            Assert.IsTrue(Regex.IsMatch(text, @"\.btn:focus\s*\{[^}]*border-width:\s*4px"), "포커스는 굵은 테두리로(색에만 의존하지 않음)");
        }
    }

    public class InputActionsAssetTests
    {
        private static string AssetFile => Path.Combine(UnityEngine.Application.dataPath, "_Game", "Settings", "GameInput.inputactions");

        private sealed class Binding
        {
            public string Map, Action, Path, Group;
        }

        private static List<Binding> ReadBindings(out List<string> schemes, out List<string> maps)
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(AssetFile));
            schemes = MiniJson.GetArray(root, "controlSchemes").Select(o => MiniJson.GetString((Dictionary<string, object>)o, "name")).ToList();
            maps = new List<string>();
            var result = new List<Binding>();
            foreach (object mapObj in MiniJson.GetArray(root, "maps"))
            {
                var map = (Dictionary<string, object>)mapObj;
                string mapName = MiniJson.GetString(map, "name");
                maps.Add(mapName);
                foreach (object bObj in MiniJson.GetArray(map, "bindings"))
                {
                    var b = (Dictionary<string, object>)bObj;
                    if (b.TryGetValue("isComposite", out object composite) && composite is bool c && c)
                    {
                        continue; // 합성 묶음 자체는 건너뛰고 각 부분(w/a/s/d)을 센다
                    }
                    result.Add(new Binding { Map = mapName, Action = MiniJson.GetString(b, "action"), Path = MiniJson.GetString(b, "path"), Group = MiniJson.GetString(b, "groups") });
                }
            }
            return result;
        }

        private static HashSet<string> Set(IEnumerable<Binding> bindings, string map, string group) =>
            new HashSet<string>(bindings.Where(b => b.Map == map && b.Group == group).Select(b => b.Action + " = " + b.Path));

        [Test]
        public void Asset_ExistsAndIsImportedAsInputActionAsset()
        {
            Assert.IsTrue(File.Exists(AssetFile), "GameInput.inputactions가 없습니다(Game/Setup/Create Input Actions).");
            UnityEngine.Object asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/_Game/Settings/GameInput.inputactions");
            Assert.IsNotNull(asset);
            Assert.AreEqual("InputActionAsset", asset.GetType().Name, "Input System이 .inputactions로 가져와야 한다(에디터 입력 설정 창에서 열림)");
        }

        [Test]
        public void ControlSchemes_AreKeyboardMouseAndGamepad_OnlyTwo()
        {
            ReadBindings(out List<string> schemes, out List<string> maps);
            CollectionAssert.AreEquivalent(new[] { "KeyboardMouse", "Gamepad" }, schemes);
            CollectionAssert.AreEquivalent(new[] { "Gameplay", "UI" }, maps);
        }

        [Test]
        public void Gameplay_KeyboardMouse_MatchesStoryboardTable()
        {
            // STORYBOARD 2.4 (v1.5) 키보드·마우스 열. 시험이 가진 독립 사본과 비교한다.
            var expected = new HashSet<string>
            {
                "Move = <Keyboard>/w", "Move = <Keyboard>/s", "Move = <Keyboard>/a", "Move = <Keyboard>/d",
                "Look = <Mouse>/delta",
                "Run = <Keyboard>/leftShift",
                "Interact = <Keyboard>/e",
                "Attack = <Mouse>/leftButton",
                "QuickSlot1 = <Keyboard>/1", "QuickSlot2 = <Keyboard>/2", "QuickSlot3 = <Keyboard>/3", "QuickSlot4 = <Keyboard>/4",
                "QuickSlot5 = <Keyboard>/5", "QuickSlot6 = <Keyboard>/6", "QuickSlot7 = <Keyboard>/7", "QuickSlot8 = <Keyboard>/8",
                "QuickSlotScroll = <Mouse>/scroll/y",
                "Inventory = <Keyboard>/tab", "Recipe = <Keyboard>/c",
                "Build = <Keyboard>/b", "Map = <Keyboard>/m", "Skills = <Keyboard>/k",
                "Wrist = <Keyboard>/q",
                "Ping = <Mouse>/middleButton", "Chat = <Keyboard>/enter", "Menu = <Keyboard>/escape",
            };
            Assert.AreEqual(Order(expected), Order(Set(ReadBindings(out _, out _), "Gameplay", "KeyboardMouse")));
        }

        [Test]
        public void Gameplay_Gamepad_MatchesStoryboardTable_WithHeldButtonsLeftEmpty()
        {
            // STORYBOARD 2.4 (v1.5) Xbox 패드 열. 퀵슬롯·핑·채팅은 보류(C-67)라 바인딩이 없어야 한다.
            var expected = new HashSet<string>
            {
                "Move = <Gamepad>/leftStick", "Look = <Gamepad>/rightStick", "Run = <Gamepad>/leftStickPress",
                "Interact = <Gamepad>/buttonSouth", "Attack = <Gamepad>/rightTrigger",
                "Inventory = <Gamepad>/select", "Recipe = <Gamepad>/buttonNorth",
                "Build = <Gamepad>/dpad/left", "Map = <Gamepad>/dpad/down", "Skills = <Gamepad>/dpad/right",
                "Wrist = <Gamepad>/dpad/up", "Menu = <Gamepad>/start",
            };
            List<Binding> all = ReadBindings(out _, out _);
            Assert.AreEqual(Order(expected), Order(Set(all, "Gameplay", "Gamepad")));
            foreach (string held in new[] { "QuickSlot", "Ping", "Chat" })
            {
                Assert.IsFalse(all.Any(b => b.Map == "Gameplay" && b.Group == "Gamepad" && b.Action.StartsWith(held, StringComparison.Ordinal)), "게임패드 " + held + " 는 보류(C-67): 바인딩이 없어야 한다");
            }
            Assert.IsFalse(all.Any(b => b.Path == "<Gamepad>/leftShoulder" && b.Map == "Gameplay"), "이전 안(LB/RB 퀵슬롯)을 구현 기준으로 쓰지 않는다");
        }

        [Test]
        public void Ui_Map_MatchesMenuCommonInput()
        {
            var expectedKeyboard = new HashSet<string>
            {
                "Navigate = <Keyboard>/upArrow", "Navigate = <Keyboard>/downArrow", "Navigate = <Keyboard>/leftArrow", "Navigate = <Keyboard>/rightArrow",
                "Submit = <Keyboard>/enter", "Cancel = <Keyboard>/escape", "TabPrev = <Keyboard>/q", "TabNext = <Keyboard>/e",
            };
            var expectedPad = new HashSet<string>
            {
                "Navigate = <Gamepad>/dpad", "Navigate = <Gamepad>/leftStick", "Submit = <Gamepad>/buttonSouth", "Cancel = <Gamepad>/buttonEast",
                "TabPrev = <Gamepad>/leftShoulder", "TabNext = <Gamepad>/rightShoulder",
            };
            List<Binding> all = ReadBindings(out _, out _);
            Assert.AreEqual(Order(expectedKeyboard), Order(Set(all, "UI", "KeyboardMouse")));
            Assert.AreEqual(Order(expectedPad), Order(Set(all, "UI", "Gamepad")));
        }

        [Test]
        public void NoDuplicatePath_WithinGameplayPerControlScheme()
        {
            // 같은 제어 체계 안에서 한 입력이 두 행동에 걸리지 않는다(충돌 없음).
            foreach (var group in ReadBindings(out _, out _).Where(b => b.Map == "Gameplay").GroupBy(b => b.Group))
            {
                var duplicates = group.GroupBy(b => b.Path).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                CollectionAssert.IsEmpty(duplicates, group.Key + " 안에서 같은 입력이 여러 행동에 할당됨");
            }
        }

        [Test]
        public void Builder_IsDeterministic_AndFileMatchesBuilderOutput()
        {
            string generated = Game.Editor.InputActionsBuilder.BuildJson();
            Assert.AreEqual(generated, Game.Editor.InputActionsBuilder.BuildJson(), "고정 ID라 다시 만들어도 같다");
            Assert.AreEqual(generated.Replace("\r\n", "\n"), File.ReadAllText(AssetFile).Replace("\r\n", "\n"), "파일이 생성기 출력과 달라졌다(손으로 고쳤다면 생성기에 반영)");
        }

        private static string Order(IEnumerable<string> items) => string.Join("\n", items.OrderBy(i => i, StringComparer.Ordinal));
    }

    public class BuildInfoAndTextTests
    {
        [Test]
        public void BuildInfo_Resolve_EditorAppendsEditorChannel_PlayerKeepsBuildMetadata()
        {
            // [NFR-058] 에디터 재생은 `핵심+editor`, 플레이어는 빌드 때 붙은 전체 문자열 그대로, 해석 실패는 null.
            Assert.AreEqual("0.1.0+editor", BuildInfo.Resolve("0.1.0", true).Full);
            Assert.AreEqual("0.1.0+dev.20261104153012.a1b2c3d", BuildInfo.Resolve("0.1.0+dev.20261104153012.a1b2c3d", false).Full);
            Assert.AreEqual("0.1.0+dev.20261104153012.a1b2c3d", BuildInfo.Resolve("0.1.0+dev.20261104153012.a1b2c3d", true).Full, "이미 메타데이터가 있으면 덧붙이지 않는다");
            Assert.IsNull(BuildInfo.Resolve("garbage", false));
            Assert.IsNull(BuildInfo.Resolve("", true));
        }

        [Test]
        public void TextTables_KoreanAndEnglish_HaveTheSameKeys()
        {
            // [NFR-044] 두 언어의 키 누락 0건.
            string folder = Path.Combine(UnityEngine.Application.dataPath, "_Game", "Localization");
            HashSet<string> Keys(string file) => new HashSet<string>(File.ReadAllLines(Path.Combine(folder, file))
                .Where(l => l.Length > 0 && l[0] != '#' && l.Contains('\t')).Select(l => l.Substring(0, l.IndexOf('\t'))));
            HashSet<string> ko = Keys("strings.ko.txt");
            HashSet<string> en = Keys("strings.en.txt");
            CollectionAssert.IsEmpty(ko.Except(en), "영어 표에 없는 키");
            CollectionAssert.IsEmpty(en.Except(ko), "한국어 표에 없는 키");
        }

        [Test]
        public void EveryTextKeyUsedByM1Screens_ExistsInKorean()
        {
            string folder = Path.Combine(UnityEngine.Application.dataPath, "_Game", "Localization");
            string korean = File.ReadAllText(Path.Combine(folder, "strings.ko.txt"));
            string keyFile = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "_Game", "Code", "Adapters", "Presentation", "UI", "Common", "UiGlyphsAndKeys.cs"));
            foreach (Match m in Regex.Matches(keyFile, "const string \\w+ = \"([a-z_.]+)\";"))
            {
                string key = m.Groups[1].Value;
                if (key.StartsWith("lobby.", StringComparison.Ordinal) || key.StartsWith("connect.", StringComparison.Ordinal) || key.StartsWith("hud.", StringComparison.Ordinal))
                {
                    StringAssert.Contains("\n" + key + "\t", "\n" + korean, "한국어 표에 키 누락: " + key);
                }
            }
        }
    }
}

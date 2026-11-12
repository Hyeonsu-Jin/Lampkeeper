// =============================================================
// 파일: UiAndInputRulesTests.cs
// 역할: (1) Input Actions 애셋이 STORYBOARD 2.4 기본 할당표와 같은지(v0.13: UI 지도의 마우스 입력 포함).
//       (2) 빌드 번호 해석. (3) 텍스트 표(ko/en) 키 일치.
//       (v0.13: USS 규칙 검사는 uGUI 전환으로 UiPrefabRulesTests 로 대체됐다.)
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
                // v0.13 (uGUI): InputSystemUIInputModule 이 쓰는 마우스 입력
                "Point = <Mouse>/position", "Click = <Mouse>/leftButton", "ScrollWheel = <Mouse>/scroll",
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

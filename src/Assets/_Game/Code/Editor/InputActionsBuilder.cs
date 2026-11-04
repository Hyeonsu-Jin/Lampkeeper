// =============================================================
// 파일: InputActionsBuilder.cs
// 역할: Input Actions 애셋(`GameInput.inputactions`)을 STORYBOARD 2.4 기본 할당표대로 만든다(에디터 전용).
//       제어 체계 2개(KeyboardMouse, Gamepad), 동작 지도 2개(Gameplay, UI). 결과물은 에디터 입력 설정 창에서 열리는 .inputactions 파일이다.
// 레이어: infrastructure (Game.Editor, 편집기 전용)
// 관련: FR-117, FR-166, NFR-002, NFR-004, DES-092 / SDD 3.7.9, M1-08 / STORYBOARD 2.4 (v1.5)
//
// 규칙
//  - 게임패드의 퀵슬롯·핑·채팅은 보류(C-67)라 바인딩을 비워 둔다(사용자가 재설정 화면에서 직접 할당).
//  - 항목 ID는 이름에서 계산한 고정 값이라 다시 만들어도 파일이 같다(불필요한 변경 이력 방지).
//  - JSON을 직접 써서 Game.Editor가 InputSystem 어셈블리를 참조하지 않게 한다(SDD 3.4.2 표 유지).
// =============================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Input Actions 애셋 생성기.</summary>
    public static class InputActionsBuilder
    {
        /// <summary>애셋 경로.</summary>
        public const string AssetPath = "Assets/_Game/Settings/GameInput.inputactions";

        public const string KeyboardMouseScheme = "KeyboardMouse";
        public const string GamepadScheme = "Gamepad";
        public const string GameplayMap = "Gameplay";
        public const string UiMap = "UI";

        private sealed class BindingSpec
        {
            public string Action;
            public string Path;
            public string Group;
            public string Name = string.Empty;
            public bool IsComposite;
            public bool IsPartOfComposite;
        }

        private sealed class ActionSpec
        {
            public string Name;
            public string Type;
            public string ControlType;
        }

        /// <summary>애셋을 (다시) 만든다.</summary>
        [MenuItem("Game/Setup/Create Input Actions")]
        public static void Create()
        {
            string json = BuildJson();
            string full = Path.GetFullPath(AssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, json, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[Setup] Input Actions 애셋을 만들었습니다: " + AssetPath);
        }

        /// <summary>애셋 JSON 본문을 만든다(시험이 파일을 읽어 비교하므로 파일이 곧 기준).</summary>
        public static string BuildJson()
        {
            var gameplayActions = new List<ActionSpec>();
            var gameplayBindings = new List<BindingSpec>();
            BuildGameplay(gameplayActions, gameplayBindings);

            var uiActions = new List<ActionSpec>();
            var uiBindings = new List<BindingSpec>();
            BuildUi(uiActions, uiBindings);

            var sb = new StringBuilder();
            sb.Append("{\n    \"name\": \"GameInput\",\n    \"maps\": [\n");
            WriteMap(sb, GameplayMap, gameplayActions, gameplayBindings);
            sb.Append(",\n");
            WriteMap(sb, UiMap, uiActions, uiBindings);
            sb.Append("\n    ],\n    \"controlSchemes\": [\n");
            WriteScheme(sb, KeyboardMouseScheme, "<Keyboard>", "<Mouse>");
            sb.Append(",\n");
            WriteScheme(sb, GamepadScheme, "<Gamepad>", null);
            sb.Append("\n    ]\n}\n");
            return sb.ToString();
        }

        // ---------- STORYBOARD 2.4 기본 할당표 ----------

        private static void BuildGameplay(List<ActionSpec> actions, List<BindingSpec> bindings)
        {
            // 이동 / 달리기: WASD / Shift, 왼쪽 스틱 / 왼쪽 스틱 누름
            actions.Add(new ActionSpec { Name = "Move", Type = "Value", ControlType = "Vector2" });
            AddComposite2D(bindings, "Move", KeyboardMouseScheme, "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d");
            Add(bindings, "Move", "<Gamepad>/leftStick", GamepadScheme);

            // 시점: 마우스 / 오른쪽 스틱
            actions.Add(new ActionSpec { Name = "Look", Type = "Value", ControlType = "Vector2" });
            Add(bindings, "Look", "<Mouse>/delta", KeyboardMouseScheme);
            Add(bindings, "Look", "<Gamepad>/rightStick", GamepadScheme);

            AddButton(actions, bindings, "Run", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            AddButton(actions, bindings, "Interact", "<Keyboard>/e", "<Gamepad>/buttonSouth");
            AddButton(actions, bindings, "Attack", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");

            // 퀵슬롯: 1~8, 휠 / 패드는 보류(C-67)
            for (int i = 1; i <= 8; i++)
            {
                AddButton(actions, bindings, "QuickSlot" + i, "<Keyboard>/" + i, null);
            }
            actions.Add(new ActionSpec { Name = "QuickSlotScroll", Type = "Value", ControlType = "Axis" });
            Add(bindings, "QuickSlotScroll", "<Mouse>/scroll/y", KeyboardMouseScheme);

            AddButton(actions, bindings, "Inventory", "<Keyboard>/tab", "<Gamepad>/select");        // 보기 버튼
            AddButton(actions, bindings, "Recipe", "<Keyboard>/c", "<Gamepad>/buttonNorth");        // Y
            AddButton(actions, bindings, "Build", "<Keyboard>/b", "<Gamepad>/dpad/left");
            AddButton(actions, bindings, "Map", "<Keyboard>/m", "<Gamepad>/dpad/down");
            AddButton(actions, bindings, "Skills", "<Keyboard>/k", "<Gamepad>/dpad/right");
            AddButton(actions, bindings, "Wrist", "<Keyboard>/q", "<Gamepad>/dpad/up");             // 손목 계기
            AddButton(actions, bindings, "Ping", "<Mouse>/middleButton", null);                     // 패드 보류
            AddButton(actions, bindings, "Chat", "<Keyboard>/enter", null);                         // 패드 보류
            AddButton(actions, bindings, "Menu", "<Keyboard>/escape", "<Gamepad>/start");           // 메뉴 버튼
        }

        private static void BuildUi(List<ActionSpec> actions, List<BindingSpec> bindings)
        {
            // 메뉴 공통 입력: 방향키·D-pad·왼쪽 스틱 = 포커스 이동, 확인 Enter·A, 뒤로 Esc·B, 탭 이동 Q/E · LB/RB
            actions.Add(new ActionSpec { Name = "Navigate", Type = "Value", ControlType = "Vector2" });
            AddComposite2D(bindings, "Navigate", KeyboardMouseScheme, "<Keyboard>/upArrow", "<Keyboard>/downArrow", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow");
            Add(bindings, "Navigate", "<Gamepad>/dpad", GamepadScheme);
            Add(bindings, "Navigate", "<Gamepad>/leftStick", GamepadScheme);

            AddButton(actions, bindings, "Submit", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
            AddButton(actions, bindings, "Cancel", "<Keyboard>/escape", "<Gamepad>/buttonEast");
            AddButton(actions, bindings, "TabPrev", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            AddButton(actions, bindings, "TabNext", "<Keyboard>/e", "<Gamepad>/rightShoulder");

            // v0.13 (uGUI, D-27): InputSystemUIInputModule이 쓰는 마우스 입력. 패드에는 대응이 없다.
            actions.Add(new ActionSpec { Name = "Point", Type = "PassThrough", ControlType = "Vector2" });
            Add(bindings, "Point", "<Mouse>/position", KeyboardMouseScheme);
            // 포인터 관련 동작은 입력 모듈이 여러 포인터를 구분하도록 PassThrough 형식이어야 한다.
            actions.Add(new ActionSpec { Name = "Click", Type = "PassThrough", ControlType = "Button" });
            Add(bindings, "Click", "<Mouse>/leftButton", KeyboardMouseScheme);
            actions.Add(new ActionSpec { Name = "ScrollWheel", Type = "PassThrough", ControlType = "Vector2" });
            Add(bindings, "ScrollWheel", "<Mouse>/scroll", KeyboardMouseScheme);
        }

        private static void AddButton(List<ActionSpec> actions, List<BindingSpec> bindings, string name, string keyboardMouse, string gamepad)
        {
            actions.Add(new ActionSpec { Name = name, Type = "Button", ControlType = "Button" });
            if (keyboardMouse != null)
            {
                Add(bindings, name, keyboardMouse, KeyboardMouseScheme);
            }
            if (gamepad != null)
            {
                Add(bindings, name, gamepad, GamepadScheme);
            }
        }

        private static void Add(List<BindingSpec> bindings, string action, string path, string group)
        {
            bindings.Add(new BindingSpec { Action = action, Path = path, Group = group });
        }

        private static void AddComposite2D(List<BindingSpec> bindings, string action, string group, string up, string down, string left, string right)
        {
            bindings.Add(new BindingSpec { Action = action, Path = "2DVector", Group = string.Empty, Name = "2D Vector", IsComposite = true });
            bindings.Add(new BindingSpec { Action = action, Path = up, Group = group, Name = "up", IsPartOfComposite = true });
            bindings.Add(new BindingSpec { Action = action, Path = down, Group = group, Name = "down", IsPartOfComposite = true });
            bindings.Add(new BindingSpec { Action = action, Path = left, Group = group, Name = "left", IsPartOfComposite = true });
            bindings.Add(new BindingSpec { Action = action, Path = right, Group = group, Name = "right", IsPartOfComposite = true });
        }

        // ---------- JSON 쓰기 ----------

        private static void WriteMap(StringBuilder sb, string mapName, List<ActionSpec> actions, List<BindingSpec> bindings)
        {
            sb.Append("        {\n            \"name\": \"").Append(mapName).Append("\",\n            \"id\": \"").Append(Id("map:" + mapName)).Append("\",\n");
            sb.Append("            \"actions\": [\n");
            for (int i = 0; i < actions.Count; i++)
            {
                ActionSpec a = actions[i];
                sb.Append("                {\n");
                sb.Append("                    \"name\": \"").Append(a.Name).Append("\",\n");
                sb.Append("                    \"type\": \"").Append(a.Type).Append("\",\n");
                sb.Append("                    \"id\": \"").Append(Id(mapName + "/" + a.Name)).Append("\",\n");
                sb.Append("                    \"expectedControlType\": \"").Append(a.ControlType).Append("\",\n");
                sb.Append("                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": ")
                  .Append(a.Type == "Value" ? "true" : "false").Append("\n                }");
                sb.Append(i < actions.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("            ],\n            \"bindings\": [\n");
            for (int i = 0; i < bindings.Count; i++)
            {
                BindingSpec b = bindings[i];
                sb.Append("                {\n");
                sb.Append("                    \"name\": \"").Append(b.Name).Append("\",\n");
                sb.Append("                    \"id\": \"").Append(Id(mapName + "/" + b.Action + "/" + i + "/" + b.Path)).Append("\",\n");
                sb.Append("                    \"path\": \"").Append(b.Path).Append("\",\n");
                sb.Append("                    \"interactions\": \"\",\n                    \"processors\": \"\",\n");
                sb.Append("                    \"groups\": \"").Append(b.Group).Append("\",\n");
                sb.Append("                    \"action\": \"").Append(b.Action).Append("\",\n");
                sb.Append("                    \"isComposite\": ").Append(b.IsComposite ? "true" : "false").Append(",\n");
                sb.Append("                    \"isPartOfComposite\": ").Append(b.IsPartOfComposite ? "true" : "false").Append("\n                }");
                sb.Append(i < bindings.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("            ]\n        }");
        }

        private static void WriteScheme(StringBuilder sb, string name, string device1, string device2)
        {
            sb.Append("        {\n            \"name\": \"").Append(name).Append("\",\n            \"bindingGroup\": \"").Append(name).Append("\",\n            \"devices\": [\n");
            WriteDevice(sb, device1);
            if (device2 != null)
            {
                sb.Append(",\n");
                WriteDevice(sb, device2);
            }
            sb.Append("\n            ]\n        }");
        }

        private static void WriteDevice(StringBuilder sb, string path)
        {
            sb.Append("                {\n                    \"devicePath\": \"").Append(path).Append("\",\n                    \"isOptional\": false,\n                    \"isOR\": false\n                }");
        }

        /// <summary>이름에서 고정 GUID 문자열을 만든다.</summary>
        private static string Id(string key)
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
                return new Guid(hash).ToString();
            }
        }
    }
}

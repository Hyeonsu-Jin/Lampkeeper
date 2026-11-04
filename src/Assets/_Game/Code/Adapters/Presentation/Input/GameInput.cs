// =============================================================
// 파일: GameInput.cs
// 역할: Input Actions 애셋(GameInput.inputactions)을 감싸 동작 지도(Gameplay/UI)를 UI 상태에 맞게 켜고 끄고,
//       마지막으로 쓴 입력 장치(키보드·마우스 / 게임패드)를 알려 안내 표시를 바꿀 수 있게 한다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-117, FR-166, NFR-002, NFR-004, I-07, DES-092 / SDD 3.7.9, 3.10.7
//
// UI 상태(Gameplay/Overlay)는 로컬 입력 경로만 바꾼다: Overlay이면 Gameplay 지도를 끄고 UI 지도를 켠다.
// 서버·복제·시각에는 영향이 없다(이 클래스는 그쪽을 모른다).
// =============================================================
using System;
using Game.Application.Hud;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Adapters.Presentation.Input
{
    /// <summary>입력 장치 종류(안내 표시용). M1은 Xbox 계열 아이콘만(C-28).</summary>
    public enum InputDeviceKind : byte
    {
        KeyboardMouse = 0,
        Gamepad = 1,
    }

    /// <summary>마지막으로 입력한 장치를 추적한다. 바뀌면 안내 아이콘을 1초 안에 교체할 수 있도록 이벤트로 알린다. [FR-166]</summary>
    public sealed class InputDeviceContext : IDisposable
    {
        public InputDeviceKind Current { get; private set; } = InputDeviceKind.KeyboardMouse;

        /// <summary>장치 종류가 바뀜.</summary>
        public event Action<InputDeviceKind> Changed;

        public InputDeviceContext()
        {
            InputSystem.onActionChange += OnActionChange;
        }

        public void Dispose() => InputSystem.onActionChange -= OnActionChange;

        private void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed || !(obj is InputAction action) || action.activeControl == null)
            {
                return;
            }
            InputDevice device = action.activeControl.device;
            InputDeviceKind kind = device is Gamepad ? InputDeviceKind.Gamepad : InputDeviceKind.KeyboardMouse;
            if (kind != Current)
            {
                Current = kind;
                Changed?.Invoke(kind);
            }
        }
    }

    /// <summary>게임 입력 묶음.</summary>
    public sealed class GameInput : IDisposable
    {
        public const string GameplayMapName = "Gameplay";
        public const string UiMapName = "UI";

        private readonly InputActionMap _gameplay;
        private readonly InputActionMap _ui;

        /// <param name="source">GameInput.inputactions 애셋(원본은 고치지 않도록 복제해서 쓴다)</param>
        /// <exception cref="ArgumentNullException">source가 null일 때</exception>
        /// <exception cref="InvalidOperationException">동작 지도가 없을 때</exception>
        public GameInput(InputActionAsset source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            Asset = UnityEngine.Object.Instantiate(source);
            _gameplay = Asset.FindActionMap(GameplayMapName, true);
            _ui = Asset.FindActionMap(UiMapName, true);
            Device = new InputDeviceContext();
        }

        /// <summary>복제한 애셋(재설정 저장은 M2).</summary>
        public InputActionAsset Asset { get; }

        public InputDeviceContext Device { get; }

        public InputAction Move => _gameplay["Move"];
        public InputAction Look => _gameplay["Look"];
        public InputAction Run => _gameplay["Run"];
        public InputAction Interact => _gameplay["Interact"];
        public InputAction Attack => _gameplay["Attack"];
        public InputAction Menu => _gameplay["Menu"];
        public InputAction UiCancel => _ui["Cancel"];
        public InputAction UiNavigate => _ui["Navigate"];

        /// <summary>
        /// UI 상태에 맞게 입력 경로를 바꾼다. Gameplay: 이동·시점·공격 입력, Overlay: UI 입력. 게임 진행은 그대로다.
        /// </summary>
        public void Apply(UiMode mode)
        {
            if (mode == UiMode.Gameplay)
            {
                _ui.Disable();
                _gameplay.Enable();
            }
            else
            {
                _gameplay.Disable();
                _ui.Enable();
            }
        }

        /// <summary>모든 입력을 끈다(게임 밖 화면).</summary>
        public void DisableAll()
        {
            _gameplay.Disable();
            _ui.Disable();
        }

        public void Dispose()
        {
            Device.Dispose();
            DisableAll();
            if (Asset != null)
            {
                UnityEngine.Object.Destroy(Asset);
            }
        }
    }
}

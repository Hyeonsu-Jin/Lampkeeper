// =============================================================
// 파일: FirstPersonController.cs
// 역할: 1인칭 이동·달리기·시점(DES-086). 이동은 클라이언트가 계산하고(체감), 결과를 ILocalPlayerPose 포트에 쓴다.
//       호스트가 이동을 거부하면 포트로 알려 온 보정 위치로 되돌린다. 스태미나는 Domain의 StaminaState 규칙을 그대로 쓴다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-023(걷기), FR-024(달리기), FR-025(스태미나), NFR-001, NFR-030(위치는 호스트 검사), D-04, DES-086 / SDD 3.9.8
//
// UI 상태가 Overlay이면 이동·시점 입력을 받지 않는다(InputEnabled=false). 중력과 스태미나 회복은 계속 흐른다(게임은 멈추지 않음, I-07).
// =============================================================
using System;
using Game.Adapters.Presentation.Input;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Domain.Common;
using Game.Domain.Vitals;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Adapters.Presentation.Player
{
    /// <summary>1인칭 조작 설정값(설정 애셋에서 조립 루트가 채운다).</summary>
    public sealed class FirstPersonSettings
    {
        public float WalkSpeed = 4f;
        public float RunSpeed = 7f;
        public float FieldOfViewDegrees = 90f;
        /// <summary>마우스 감도(도/픽셀). 옵션 화면(M2)에서 바뀐다.</summary>
        public float MouseDegreesPerPixel = 0.1f;
        /// <summary>패드 오른쪽 스틱 회전 속도(도/초).</summary>
        public float GamepadDegreesPerSecond = 180f;
        public float Gravity = -20f;
        /// <summary>시점 상하 한계(도).</summary>
        public float PitchLimitDegrees = 85f;
        /// <summary>달리기 입력으로 인정하는 최소 이동 입력 크기.</summary>
        public float MoveDeadzone = 0.1f;
    }

    /// <summary>1인칭 컨트롤러.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        private const float MillisecondsPerSecond = 1000f;
        private const float DevAutoWalkCycleSeconds = 8f;
        private const float DevAutoWalkRunSeconds = 3f;
        private const float DevAutoWalkTurnDegreesPerSecond = 25f;

        [SerializeField] private Camera _camera;

        private CharacterController _controller;
        private GameInput _input;
        private ILocalPlayerPose _pose;
        private FirstPersonSettings _settings;
        private float _yaw;
        private float _pitch;
        private float _verticalVelocity;

        /// <summary>이 컨트롤러가 쓰는 스태미나(HUD 호가 읽는다).</summary>
        public StaminaState Stamina { get; private set; }

        /// <summary>지금 달리는 중인지(입력 + 이동 + 스태미나 있음).</summary>
        public bool IsRunning { get; private set; }

        /// <summary>false이면 이동·시점 입력을 받지 않는다(Overlay).</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>
        /// 개발용 자동 걷기(개발 빌드의 -autowalk): 입력 없이 앞으로 걷고 천천히 돌며, 주기적으로 달린다.
        /// 화면 없이 도는 여러 프로세스로 이동 동기화·호스트 검사·스태미나를 시험하려고 둔다.
        /// </summary>
        public bool DevAutoWalk { get; set; }

        /// <summary>현재 시선 좌우 각도(도).</summary>
        public float YawDegrees => _yaw;

        /// <summary>조작 준비. 조립 루트가 한 번 부른다.</summary>
        public void Initialize(GameInput input, ILocalPlayerPose pose, FirstPersonSettings settings, StaminaParams stamina)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _pose = pose ?? throw new ArgumentNullException(nameof(pose));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Stamina = new StaminaState(stamina);
            _controller = GetComponent<CharacterController>();
            if (_camera == null)
            {
                _camera = GetComponentInChildren<Camera>();
            }
            if (_camera != null)
            {
                _camera.fieldOfView = settings.FieldOfViewDegrees;
            }
        }

        /// <summary>위치를 옮긴다(스폰, 호스트 보정). CharacterController를 잠깐 꺼야 위치가 바뀐다.</summary>
        public void Teleport(Vec3f position)
        {
            bool wasEnabled = _controller != null && _controller.enabled;
            if (_controller != null)
            {
                _controller.enabled = false;
            }
            transform.position = new Vector3(position.X, position.Y, position.Z);
            _verticalVelocity = 0f;
            if (_controller != null)
            {
                _controller.enabled = wasEnabled;
            }
        }

        private void Update()
        {
            if (_input == null)
            {
                return;
            }
            float dt = Time.deltaTime;

            // [NFR-030] 호스트가 정한 위치(스폰·이동 거부 보정)가 오면 따른다.
            if (_pose.TryTakeCorrection(out Vec3f corrected))
            {
                Teleport(corrected);
            }

            Vector2 move = Vector2.zero;
            bool runHeld = false;
            if (DevAutoWalk)
            {
                // 3초 달리고 5초 걷는 순환. 시선은 초당 DevAutoWalkTurnDegreesPerSecond도 돈다.
                move = Vector2.up;
                runHeld = Time.time % DevAutoWalkCycleSeconds < DevAutoWalkRunSeconds;
                _yaw = (_yaw + DevAutoWalkTurnDegreesPerSecond * dt) % 360f;
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            }
            else if (InputEnabled)
            {
                move = Vector2.ClampMagnitude(_input.Move.ReadValue<Vector2>(), 1f);
                runHeld = _input.Run.IsPressed();
                ApplyLook(dt);
            }

            // [FR-024] 달리기: 달리기 키 + 이동 입력 + 스태미나가 있을 때만. 0이면 걷기 속도로 이동한다.
            bool moving = move.magnitude > _settings.MoveDeadzone;
            bool running = runHeld && moving && Stamina.CanRun;
            float speed = running ? _settings.RunSpeed : _settings.WalkSpeed;

            Vector3 planar = (transform.right * move.x + transform.forward * move.y) * speed;
            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f; // 바닥에 붙여 두기
            }
            _verticalVelocity += _settings.Gravity * dt;
            _controller.Move((planar + Vector3.up * _verticalVelocity) * dt);

            // [FR-025] 소모·회복은 Domain 규칙이 한다. UI가 열려 있어도(Overlay) 시간은 흐른다.
            Stamina.Tick(dt, running);
            IsRunning = running;

            PublishPose(running);
        }

        private void ApplyLook(float dt)
        {
            InputAction look = _input.Look;
            Vector2 delta = look.ReadValue<Vector2>();
            bool fromGamepad = look.activeControl != null && look.activeControl.device is Gamepad;
            float yawDelta = fromGamepad ? delta.x * _settings.GamepadDegreesPerSecond * dt : delta.x * _settings.MouseDegreesPerPixel;
            float pitchDelta = fromGamepad ? delta.y * _settings.GamepadDegreesPerSecond * dt : delta.y * _settings.MouseDegreesPerPixel;
            _yaw = (_yaw + yawDelta) % 360f;
            _pitch = Mathf.Clamp(_pitch - pitchDelta, -_settings.PitchLimitDegrees, _settings.PitchLimitDegrees);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_camera != null)
            {
                _camera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void PublishPose(bool running)
        {
            Vector3 p = transform.position;
            uint timeMs = unchecked((uint)(long)(Time.realtimeSinceStartupAsDouble * MillisecondsPerSecond));
            _pose.Write(new AvatarPose(new Vec3f(p.x, p.y, p.z), _yaw, _pitch, running, timeMs));
        }
    }
}

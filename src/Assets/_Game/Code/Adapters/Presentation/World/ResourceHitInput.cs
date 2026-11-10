// =============================================================
// 파일: ResourceHitInput.cs
// 역할: 공격 입력(마우스 왼쪽 / 패드 공격 버튼)을 읽어, 조준선에 걸린 자원이 있으면 「타격 요청」을 보낸다.
//       요청을 어디로 보낼지는 호출하는 쪽(조립 루트)이 델리게이트로 넘긴다(어댑터끼리·네트워크를 직접 참조하지 않는다).
// 레이어: adapters (Game.Adapters.Presentation/World)
// 관련: FR-007, FR-009, FR-044~047, DES-086, DES-087 / SDD 3.9.3
//
// 결과는 호스트가 검증한 뒤 자원 표 델타로 돌아와 화면(ResourceViewStreamer)이 바뀐다. 이 클래스는 낙관적 표현(소리·애니메이션)을 하지 않는다(M2).
// =============================================================
using System;
using Game.Adapters.Presentation.Input;
using UnityEngine;

namespace Game.Adapters.Presentation.World
{
    /// <summary>공격 입력 → 자원 타격 요청. 매 프레임 <see cref="Tick"/>을 부른다(게임 입력 상태일 때만).</summary>
    public sealed class ResourceHitInput
    {
        /// <summary>누르고 있을 때 다음 요청까지 기다리는 시간(초). 호스트의 초당 타격 상한(4회)보다 느리게 보낸다.</summary>
        public const float RepeatIntervalSeconds = 0.3f;

        /// <summary>M1은 손 칸을 쓰지 않는다(도구 판정은 M2). 0번 칸 고정.</summary>
        public const byte HandSlotNone = 0;

        private readonly GameInput _input;
        private readonly ResourceViewStreamer _streamer;
        private readonly Transform _eye;
        private readonly float _reachMeters;
        private readonly Func<ushort, byte, bool> _send;
        private float _cooldown;

        /// <summary>마지막으로 보낸 자원 번호(진단용). 보낸 적이 없으면 -1.</summary>
        public int LastSentIndex { get; private set; } = -1;

        /// <param name="input">게임 입력 묶음(공격 동작)</param>
        /// <param name="streamer">조준 대상을 찾는 자원 뷰</param>
        /// <param name="eye">시선 기준(카메라)</param>
        /// <param name="reachMeters">닿는 거리(m). 호스트의 상호작용 거리와 같게 둔다</param>
        /// <param name="send">요청 전송(자원 번호, 손 칸) → 보냈으면 true</param>
        /// <exception cref="ArgumentNullException">인자가 null일 때</exception>
        public ResourceHitInput(GameInput input, ResourceViewStreamer streamer, Transform eye, float reachMeters, Func<ushort, byte, bool> send)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _streamer = streamer != null ? streamer : throw new ArgumentNullException(nameof(streamer));
            _eye = eye != null ? eye : throw new ArgumentNullException(nameof(eye));
            _reachMeters = reachMeters;
            _send = send ?? throw new ArgumentNullException(nameof(send));
        }

        /// <summary>한 프레임 처리. 공격을 누르면 바로, 누르고 있으면 일정 간격으로 요청한다.</summary>
        /// <param name="deltaTime">지난 시간(초)</param>
        public void Tick(float deltaTime)
        {
            if (_cooldown > 0f)
            {
                _cooldown -= deltaTime;
            }
            if (_cooldown > 0f || !_input.Attack.IsPressed())
            {
                return;
            }
            if (!_streamer.TryAim(_eye, _reachMeters, out int index) || index < 0 || index > ushort.MaxValue)
            {
                return;
            }
            if (_send((ushort)index, HandSlotNone))
            {
                LastSentIndex = index;
                _cooldown = RepeatIntervalSeconds;
            }
        }
    }
}

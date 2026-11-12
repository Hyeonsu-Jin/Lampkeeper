// =============================================================
// 파일: StaminaRules.cs
// 역할: 스태미나 소모·회복 규칙(달리기 소모, 소모를 멈춘 뒤 지연 후 회복).
// 레이어: domain (Game.Domain, 순수 C#)
// 관련: FR-024(달리기), FR-025(스태미나), NFR-001(스태미나 표시), DES-003 / SDD 3.3.1
//
// 클라이언트(체감)와 호스트(검사)가 같은 규칙을 쓴다.
// [SRS v0.10 FR-024/025 확정] 0이 돼도 불이익은 없고 달리기만 걷기 속도가 된다(IZeroVitalPenaltyRule을 거치지 않음).
// 회복 지연 1초: 마지막으로 소모한 뒤 1초가 지나야 회복이 시작되고, 다시 소모하면 1초를 처음부터 센다.
// =============================================================
using System;

namespace Game.Domain.Vitals
{
    /// <summary>스태미나 규칙 설정값(설정값 `스태미나 최대` 등을 호출자가 채워 준다).</summary>
    public readonly struct StaminaParams
    {
        public float Max { get; }
        public float RunCostPerSecond { get; }
        public float RegenDelaySeconds { get; }
        public float RegenPerSecond { get; }

        public StaminaParams(float max, float runCostPerSecond, float regenDelaySeconds, float regenPerSecond)
        {
            Max = max;
            RunCostPerSecond = runCostPerSecond;
            RegenDelaySeconds = regenDelaySeconds;
            RegenPerSecond = regenPerSecond;
        }
    }

    /// <summary>한 플레이어의 스태미나 상태. 시간 진행은 <see cref="Tick"/> 하나로만 바뀐다.</summary>
    public sealed class StaminaState
    {
        private readonly StaminaParams _params;
        private float _secondsSinceUse;

        /// <param name="parameters">규칙 설정값</param>
        /// <exception cref="ArgumentOutOfRangeException">최대값이 0 이하일 때</exception>
        public StaminaState(StaminaParams parameters)
        {
            if (parameters.Max <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(parameters), "스태미나 최대는 0보다 커야 한다.");
            }
            _params = parameters;
            Current = parameters.Max;
            _secondsSinceUse = parameters.RegenDelaySeconds;
        }

        /// <summary>현재 스태미나(0 ~ 최대).</summary>
        public float Current { get; private set; }

        /// <summary>최대 스태미나.</summary>
        public float Max => _params.Max;

        /// <summary>현재 비율(0 ~ 1).</summary>
        public float Ratio => Current / _params.Max;

        /// <summary>지금 달리기를 시작하거나 유지할 수 있는지. [FR-024] 0이면 달릴 수 없다.</summary>
        public bool CanRun => Current > 0f;

        /// <summary>회복 중인지(지연이 지나 늘어나는 중). HUD 호 표시 판단에 쓴다.</summary>
        public bool IsRecovering => Current < _params.Max && _secondsSinceUse >= _params.RegenDelaySeconds;

        /// <summary>
        /// 시간을 진행한다.
        /// </summary>
        /// <param name="deltaSeconds">경과 시간(초). 0 이하이면 아무 일도 하지 않는다</param>
        /// <param name="isRunning">이 시간 동안 달렸는지</param>
        public void Tick(float deltaSeconds, bool isRunning)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            if (isRunning && Current > 0f)
            {
                // [FR-024] 달리는 동안 초당 소모량만큼 줄인다.
                Current = Math.Max(0f, Current - _params.RunCostPerSecond * deltaSeconds);
                _secondsSinceUse = 0f;
                return;
            }

            // [FR-025] 소모를 멈춘 뒤 회복 지연이 지나야 회복이 시작된다.
            _secondsSinceUse += deltaSeconds;
            if (_secondsSinceUse >= _params.RegenDelaySeconds && Current < _params.Max)
            {
                Current = Math.Min(_params.Max, Current + _params.RegenPerSecond * deltaSeconds);
            }
        }

        /// <summary>값을 직접 정한다(시험용, 보정). 0 ~ 최대로 자른다.</summary>
        public void Set(float value)
        {
            Current = Math.Min(_params.Max, Math.Max(0f, value));
        }
    }
}

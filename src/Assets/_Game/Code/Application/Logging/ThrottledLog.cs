// =============================================================
// 파일: ThrottledLog.cs
// 역할: 같은 종류(수준 + 분류)의 경고·정보 로그가 폭주하지 않게 묶는 ILog 감싸개.
//       창(기본 5초)마다 첫 줄만 통과시키고, 나머지는 세었다가 다음 통과 줄에 「N건 생략」으로 붙인다.
//       오류(Error)는 드물고 중요하므로 묶지 않는다.
// 레이어: application (Game.Application/Logging, 순수 C#)
// 관련: IMP-004, SEC-003, REV-007, NFR-040, NFR-045
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Ports;

namespace Game.Application.Logging
{
    /// <summary>반복 로그를 묶는 ILog.</summary>
    public sealed class ThrottledLog : ILog
    {
        /// <summary>기본 묶음 창(초).</summary>
        public const double DefaultWindowSeconds = 5d;

        private sealed class Slot
        {
            public double NextAllowed;
            public int Suppressed;
        }

        private readonly ILog _inner;
        private readonly IClock _clock;
        private readonly double _windowSeconds;
        private readonly Dictionary<string, Slot> _slots = new Dictionary<string, Slot>();

        public ThrottledLog(ILog inner, IClock clock, double windowSeconds = DefaultWindowSeconds)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _windowSeconds = windowSeconds;
        }

        public void Info(string category, string message) => Write(false, category, message);

        public void Warn(string category, string message) => Write(true, category, message);

        /// <summary>오류는 묶지 않는다.</summary>
        public void Error(string category, string message) => _inner.Error(category, message);

        private void Write(bool warn, string category, string message)
        {
            string key = (warn ? "W:" : "I:") + category;
            double now = _clock.RealtimeSeconds;
            if (!_slots.TryGetValue(key, out Slot slot))
            {
                slot = new Slot();
                _slots[key] = slot;
            }
            if (now < slot.NextAllowed)
            {
                slot.Suppressed++;
                return;
            }
            slot.NextAllowed = now + _windowSeconds;
            if (slot.Suppressed > 0)
            {
                message += " (직전 " + _windowSeconds + "초 동안 같은 종류 " + slot.Suppressed + "건 생략)";
                slot.Suppressed = 0;
            }
            if (warn)
            {
                _inner.Warn(category, message);
            }
            else
            {
                _inner.Info(category, message);
            }
        }
    }
}

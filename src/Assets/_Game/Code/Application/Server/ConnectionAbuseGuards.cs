// =============================================================
// 파일: ConnectionAbuseGuards.cs
// 역할: 연결 하나가 호스트를 괴롭히지 못하게 하는 작은 도구 둘.
//       PoseRateGate  - 자세 RPC 수신 빈도 상한(토큰 버킷, 송신 주기 x 1.5).
//       AbuseMeter    - 일정 시간 안의 실패(디코드 실패·거부) 횟수가 기준을 넘으면 연결 종료를 알린다.
// 레이어: application (Game.Application/Server, 순수 C#)
// 관련: IMP-004, SEC-003, REV-007, NFR-040, NFR-041
// 시각은 호출자가 넘긴다(시험이 쉽다). 연결마다 하나씩 만든다.
// =============================================================
using System;
using Game.Application.Commands;

namespace Game.Application.Server
{
    /// <summary>연결별 자세 수신 빈도 상한. [SEC-003] 넘치는 자세는 버린다.</summary>
    public sealed class PoseRateGate
    {
        /// <summary>송신 주기에 곱하는 허용 배율(지터 흡수).</summary>
        public const double RateMultiplier = 1.5;

        /// <summary>순간 허용 개수(잠깐 끊겼다 몰려 들어오는 자세).</summary>
        public const int BurstCapacity = 5;

        private readonly TokenBucket _bucket;

        /// <param name="sendHz">클라이언트 자세 송신 주기(Hz)</param>
        /// <param name="now">만든 시각(초)</param>
        public PoseRateGate(int sendHz, double now)
        {
            _bucket = new TokenBucket(Math.Max(1, sendHz) * RateMultiplier, BurstCapacity, now);
        }

        /// <summary>이 자세를 받아도 되는지. 토큰을 쓴다.</summary>
        public bool Allow(double now) => _bucket.TryTake(now);
    }

    /// <summary>
    /// 실패 횟수 측정기. 창(window) 안에서 limit번을 넘으면 true를 돌려주고 호출자는 연결을 끊는다. [SEC-003, REV-007]
    /// 창은 고정 구간이다: 구간이 지나면 횟수를 0으로 되돌린다.
    /// </summary>
    public sealed class AbuseMeter
    {
        /// <summary>기본 창(초).</summary>
        public const double DefaultWindowSeconds = 10d;

        /// <summary>기본 한도(창 안의 실패 횟수). SECURITY_REPORT 예: 10초에 50회.</summary>
        public const int DefaultLimit = 50;

        private readonly double _windowSeconds;
        private readonly int _limit;
        private double _windowStart = double.NaN;
        private int _count;

        public AbuseMeter(int limit = DefaultLimit, double windowSeconds = DefaultWindowSeconds)
        {
            _limit = Math.Max(1, limit);
            _windowSeconds = windowSeconds;
        }

        /// <summary>지금 창 안의 실패 횟수.</summary>
        public int Count => _count;

        /// <summary>실패 하나를 기록한다.</summary>
        /// <returns>한도를 넘었으면 true(연결을 끊어야 함)</returns>
        public bool Record(double now)
        {
            if (double.IsNaN(_windowStart) || now - _windowStart >= _windowSeconds)
            {
                _windowStart = now;
                _count = 0;
            }
            _count++;
            return _count > _limit;
        }
    }
}

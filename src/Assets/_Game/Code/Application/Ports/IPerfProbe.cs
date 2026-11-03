// =============================================================
// 파일: IPerfProbe.cs
// 역할: 처리 시간을 재는 포트. 서버·클라이언트 코드는 Unity의 ProfilerMarker를 직접 모르고 이 인터페이스로만 잰다.
// 레이어: application (포트). 구현: Infra.Platform.ProfilerPerfProbe
// 관련: FR-163, NFR-010~NFR-021, NFR-048, DES-108 / SDD 3.7.3, 3.12.4
// =============================================================
using System;
using Game.Application.Perf;

namespace Game.Application.Ports
{
    /// <summary>
    /// 처리 구간 시간 측정기. 메인 스레드에서만 부른다(워커 스레드 호출은 지원하지 않음).
    /// 구간 이름은 <see cref="PerfMarkers"/> 상수를 쓴다.
    /// </summary>
    public interface IPerfProbe
    {
        /// <summary>구간 시작.</summary>
        /// <param name="marker">구간 이름(PerfMarkers)</param>
        void Begin(string marker);

        /// <summary>구간 끝. 같은 이름의 Begin과 짝이 맞아야 한다.</summary>
        void End(string marker);

        /// <summary>이름 붙은 지표 값을 알린다(대역폭, RTT, 활동 생물 수 등). 가장 최근 값이 기록에 남는다.</summary>
        void Count(string name, double value);
    }

    /// <summary>측정하지 않는 기본 구현. 측정기가 없을 때(출시 빌드, 시험) 쓴다.</summary>
    public sealed class NullPerfProbe : IPerfProbe
    {
        /// <summary>공용 인스턴스.</summary>
        public static NullPerfProbe Instance { get; } = new NullPerfProbe();

        public void Begin(string marker) { }
        public void End(string marker) { }
        public void Count(string name, double value) { }
    }

    /// <summary><c>using</c>으로 구간을 재는 도우미. 힙 할당이 없는 값 형식이다.</summary>
    public readonly struct PerfScope : IDisposable
    {
        private readonly IPerfProbe _probe;
        private readonly string _marker;

        public PerfScope(IPerfProbe probe, string marker)
        {
            _probe = probe;
            _marker = marker;
            _probe?.Begin(marker);
        }

        public void Dispose() => _probe?.End(_marker);
    }

    /// <summary>IPerfProbe 확장.</summary>
    public static class PerfProbeExtensions
    {
        /// <summary>구간 측정 범위를 만든다. 사용 예: <c>using (probe.Scope(PerfMarkers.ServerTotal)) { ... }</c></summary>
        public static PerfScope Scope(this IPerfProbe probe, string marker) => new PerfScope(probe, marker);
    }
}

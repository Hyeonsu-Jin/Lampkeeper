// =============================================================
// 파일: RuntimePerfSampler.cs
// 역할: 1초마다 프로세스 메모리(작업 집합·관리 힙·그래픽 메모리), 전송 계층 송수신 B/s·연결별 최댓값·RTT, 바뀐 자원 수를 읽어
//       성능 측정기(IPerfProbe 지표)에 알린다. 이 값들이 -perflog CSV의 net.*, resources.synced, mem.* 열이 된다.
// 레이어: infrastructure (Game.Bootstrap/DevTools, 개발 빌드 전용)
// 관련: FR-163, NFR-013, NFR-014, NFR-016, NFR-048, NFR-021 / SDD 3.12.4 2항, 3.12.5, 3.12.8
//
// 송수신량은 Tugboat(LiteNetLib)의 통계를 읽는다(전송 머리말 포함, 실제로 선에 실리는 양). FishNet 자체 통계는 내부 형식이라 쓰지 않는다.
// NetManager는 Tugboat 내부(internal) 필드라 리플렉션으로 한 번만 찾아 둔다. 개발 빌드 측정 전용이므로 허용하고, 못 찾으면 0으로 두고 로그를 남긴다.
// Steam 전송에서는 이 통계가 없어 net.* 열이 0이다(M1 측정은 Tugboat).
// =============================================================
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using LiteNetLib;
using Game.Application.Perf;
using Game.Application.Ports;
using Game.Bootstrap.Runtime;
using Unity.Profiling;
using Debug = UnityEngine.Debug;

namespace Game.Bootstrap.DevTools
{
    /// <summary>측정 값 읽기. 게임 흐름이 매 프레임 <see cref="Tick"/>을 부르면 1초마다 한 번만 일한다.</summary>
    public sealed class RuntimePerfSampler : IDisposable
    {
        private const string LogTag = "[PerfSampler] ";
        private const double SampleIntervalSeconds = 1.0;
        private const double BytesPerMegabyte = 1024.0 * 1024.0;
        private const string GfxCounterName = "Gfx Used Memory";

        private static readonly FieldInfo NetManagerField = typeof(CommonSocket).GetField("NetManager", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly IPerfProbe _probe;
        private readonly NetworkManager _network;
        private readonly Func<SessionRuntime> _runtime;
        private readonly Dictionary<int, long[]> _lastPeerBytes = new Dictionary<int, long[]>();
        private ProfilerRecorder _gfxRecorder;
        private double _nextSampleAt;
        private double _lastSampleAt = -1;
        private long _lastSent;
        private long _lastReceived;
        private bool _reflectionWarned;

        /// <param name="probe">지표를 알릴 측정기</param>
        /// <param name="network">FishNet 네트워크 관리자(전송을 찾는다)</param>
        /// <param name="runtime">현재 세션 실행 구성을 돌려주는 함수(없으면 null)</param>
        public RuntimePerfSampler(IPerfProbe probe, NetworkManager network, Func<SessionRuntime> runtime)
        {
            _probe = probe ?? throw new ArgumentNullException(nameof(probe));
            _network = network;
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _gfxRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, GfxCounterName);
        }

        /// <summary>매 프레임 부른다.</summary>
        /// <param name="nowSeconds">실행 시작 후 경과 시간(초)</param>
        public void Tick(double nowSeconds)
        {
            if (nowSeconds < _nextSampleAt)
            {
                return;
            }
            _nextSampleAt = nowSeconds + SampleIntervalSeconds;
            double elapsed = _lastSampleAt < 0 ? 0 : nowSeconds - _lastSampleAt;
            _lastSampleAt = nowSeconds;

            SampleMemory();
            SampleNet(elapsed);
            SampleResources();
        }

        public void Dispose()
        {
            if (_gfxRecorder.Valid)
            {
                _gfxRecorder.Dispose();
            }
        }

        private void SampleMemory()
        {
            // IL2CPP는 System.Diagnostics.Process를 지원하지 않아 Windows API(psapi)로 읽는다.
            if (TryGetWorkingSetBytes(out double workingSetBytes))
            {
                _probe.Count(PerfCounters.MemWorkingSetMb, workingSetBytes / BytesPerMegabyte);
            }
            _probe.Count(PerfCounters.MemManagedHeapMb, GC.GetTotalMemory(false) / BytesPerMegabyte);
            // 그래픽 메모리는 값을 못 읽으면 0(= 측정 불가). 실제 VRAM은 측정 스크립트가 GPU 성능 카운터로 잰다.
            _probe.Count(PerfCounters.MemGfxMb, _gfxRecorder.Valid ? _gfxRecorder.LastValue / BytesPerMegabyte : 0.0);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessMemoryCounters
        {
            public uint Cb;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("psapi.dll")]
        private static extern bool GetProcessMemoryInfo(IntPtr process, out ProcessMemoryCounters counters, uint size);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
#endif

        /// <summary>이 프로세스의 작업 집합(바이트). Windows가 아니거나 읽지 못하면 false.</summary>
        private static bool TryGetWorkingSetBytes(out double bytes)
        {
            bytes = 0;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var counters = new ProcessMemoryCounters { Cb = (uint)Marshal.SizeOf(typeof(ProcessMemoryCounters)) };
            if (GetProcessMemoryInfo(GetCurrentProcess(), out counters, counters.Cb))
            {
                bytes = (double)(ulong)counters.WorkingSetSize;
                return true;
            }
#endif
            return false;
        }

        private void SampleNet(double elapsedSeconds)
        {
            SessionRuntime runtime = _runtime();
            if (runtime == null || _network == null || elapsedSeconds <= 0)
            {
                return;
            }
            var tugboat = _network.TransportManager.Transport as Tugboat;
            if (tugboat == null)
            {
                return; // Steam 전송 등: 통계가 없다
            }
            CommonSocket socket = runtime.IsHost ? (CommonSocket)tugboat.ServerSocket : tugboat.ClientSocket;
            NetManager manager = NetManagerField != null ? NetManagerField.GetValue(socket) as NetManager : null;
            if (manager == null)
            {
                if (NetManagerField == null && !_reflectionWarned)
                {
                    _reflectionWarned = true;
                    Debug.LogWarning(LogTag + "Tugboat 내부 필드(NetManager)를 찾지 못해 송수신 통계를 기록하지 못합니다.");
                }
                return;
            }
            manager.EnableStatistics = true; // 켠 순간부터 센다(첫 1초는 0에서 시작)

            long sent = manager.Statistics.BytesSent;
            long received = manager.Statistics.BytesReceived;
            _probe.Count(PerfCounters.NetSendBytesPerSec, Math.Max(0, sent - _lastSent) / elapsedSeconds);
            _probe.Count(PerfCounters.NetRecvBytesPerSec, Math.Max(0, received - _lastReceived) / elapsedSeconds);
            _lastSent = sent;
            _lastReceived = received;

            // 연결별: 접속자 수, 연결별 송수신 최댓값, RTT(여러 연결이면 평균).
            List<NetPeer> peers = manager.ConnectedPeerList;
            double maxSend = 0;
            double maxRecv = 0;
            double rttSum = 0;
            for (int i = 0; i < peers.Count; i++)
            {
                NetPeer peer = peers[i];
                long peerSent = peer.Statistics.BytesSent;
                long peerReceived = peer.Statistics.BytesReceived;
                if (_lastPeerBytes.TryGetValue(peer.Id, out long[] last))
                {
                    maxSend = Math.Max(maxSend, Math.Max(0, peerSent - last[0]) / elapsedSeconds);
                    maxRecv = Math.Max(maxRecv, Math.Max(0, peerReceived - last[1]) / elapsedSeconds);
                }
                else
                {
                    _lastPeerBytes[peer.Id] = last = new long[2];
                }
                last[0] = peerSent;
                last[1] = peerReceived;
                rttSum += peer.RoundTripTime;
            }
            _probe.Count(PerfCounters.NetPeers, peers.Count);
            _probe.Count(PerfCounters.NetPeerSendMaxBytesPerSec, maxSend);
            _probe.Count(PerfCounters.NetPeerRecvMaxBytesPerSec, maxRecv);
            _probe.Count(PerfCounters.NetRttMs, peers.Count > 0 ? rttSum / peers.Count : 0);
        }

        private void SampleResources()
        {
            SessionRuntime runtime = _runtime();
            if (runtime != null && runtime.IsHost && runtime.World != null)
            {
                // 표에는 바뀐 항목만 들어 있다(-resourcechanges 5000 → 5000).
                _probe.Count(PerfCounters.SyncedResources, runtime.World.State.Resources.Count);
            }
        }
    }
}

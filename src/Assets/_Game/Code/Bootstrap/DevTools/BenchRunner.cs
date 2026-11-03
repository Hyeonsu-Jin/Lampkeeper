// =============================================================
// 파일: BenchRunner.cs
// 역할: Bench 장면의 진행자. 측정 장면(P1/P8)을 재현한다: 기준 프리셋 「높음」과 업스케일링 설정을 적용하고,
//       시험용 몹 N체와 서버 틱(30 Hz) 흉내를 돌리며 성능 측정(CSV)을 남기고, 정해진 시간 뒤 요약 파일을 쓰고 끝낸다.
// 레이어: infrastructure (Game.Bootstrap/DevTools, 개발 빌드 전용)
// 관련: FR-163, NFR-010~NFR-012, NFR-021, DES-113, DES-108 / SDD 3.7.4(-bench), 3.12.3~3.12.4, M1-05
//
// 지금은 "측정 틀"이다: 서버 틱의 각 구간(Commands, AiDecide, AiApply, Systems, Replication)에 실제 시스템 대신
// 비슷한 양의 정수 계산·복사를 넣어 표식·집계·CSV·예산 감시가 끝까지 도는지 확인한다.
// 실제 AI는 M1-11, 복제는 M1-09, 봇 7개와 8인 구성은 M1-12에서 이 틀에 끼워 넣는다.
// 그래서 이 장면의 서버 처리 시간은 판정에 쓰지 않고, 보고서에도 "틀 검증용"으로 표시한다.
// =============================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Application.Graphics;
using Game.Application.Dev;
using Game.Application.Perf;
using Game.Application.Ports;
using Game.Infra.Config;
using UnityEngine;

namespace Game.Bootstrap.DevTools
{
    /// <summary>Bench 장면 진행자.</summary>
    public sealed class BenchRunner : MonoBehaviour
    {
        private const string LogTag = "[Bench] ";

        // 서버 틱 흉내 (SDD 3.12.1: 서버 틱 30 Hz, 틱당 20체 판단)
        private const float ServerTickSeconds = 1f / 30f;
        private const int AiDecidePerTick = 20;
        private const int TestAgentsP1 = 30;
        private const int TestAgentsP8 = 60;
        private const int ReplicationBytesPerAgent = 11;

        /// <summary>명령줄 없이 에디터에서 장면을 바로 실행할 때의 길이(초).</summary>
        private const int EditorDefaultSeconds = 30;

        private const float ArenaHalfSizeMeters = 40f;
        private const float AgentSpeedMps = 3f;
        private const float AgentScaleMeters = 1.2f;

        /// <summary>시험에서 길이(초)를 바꾸기 위한 값. 0이면 쓰지 않는다. 명령줄 -bench 가 없을 때만 적용.</summary>
        public static int EditorDurationSecondsOverride { get; set; }

        /// <summary>끝났을 때 플레이 모드/프로세스를 끝낼지. 시험에서만 끈다.</summary>
        public static bool QuitWhenFinished { get; set; } = true;

        /// <summary>마지막 요약 파일 경로(시험이 읽음).</summary>
        public static string LastSummaryPath { get; private set; }

        [Header("설정(그래픽 프리셋을 읽는 데 씀)")]
        [SerializeField] private GameDataConfig _dataConfig;

        [Header("시험용 몹")]
        [SerializeField] private Material _agentMaterial;

        private PerfOptions _options;
        private BenchScenario _scenario;
        private int _durationSeconds;
        private GraphicsBootstrapper _graphics;
        private IPerfProbe _probe;
        private Transform[] _agents;
        private Vector3[] _velocities;
        private byte[] _replicationBuffer;
        private float _tickAccumulator;
        private float _elapsed;
        private uint _rngState = 12345u;
        private bool _finished;
        private readonly List<PerfSnapshot> _snapshots = new List<PerfSnapshot>();
        private string _csvPath;
        private UpscalerStatus _upscalerStatus;

        private void Start()
        {
            IReadOnlyList<string> args = DevCommandLine.GetArgs();
            _options = PerfOptions.Parse(args);
            _scenario = _options.Bench ?? BenchScenario.P1;
            _durationSeconds = _options.Bench.HasValue ? _options.BenchSeconds
                : (EditorDurationSecondsOverride > 0 ? EditorDurationSecondsOverride : EditorDefaultSeconds);

            ApplyGraphics();
            StartTelemetry();
            SpawnAgents(_scenario == BenchScenario.P8 ? TestAgentsP8 : TestAgentsP1);

            Debug.Log(LogTag + "시작: " + _scenario + ", " + _durationSeconds + "초, 몹 " + _agents.Length + "체, CSV=" + _csvPath);
        }

        private void ApplyGraphics()
        {
            _graphics = GraphicsBootstrapper.TryCreate(_dataConfig != null ? _dataConfig.Graphics : null);
            if (_graphics == null)
            {
                return;
            }
            _graphics.ApplyStartupSettings(null, _options.Upscale);
            _upscalerStatus = _graphics.Backend.Upscaler;
        }

        private void StartTelemetry()
        {
            DevDiagnostics.Install(DevCommandLine.GetArgs());
            if (DevDiagnostics.Telemetry == null)
            {
                Debug.LogWarning(LogTag + "성능 측정기를 켜지 못했습니다(출시 빌드에서는 Bench를 쓸 수 없습니다).");
                _probe = NullPerfProbe.Instance;
                return;
            }
            string upscaleTag = _options.Upscale == UpscaleSwitch.Off ? "off" : "on";
            _csvPath = DevDiagnostics.ResolveCsvPath(_options.PerfLogFolder, DateTime.Now).Replace("perf_", "bench_" + _scenario + "_up" + upscaleTag + "_");
            DevDiagnostics.EnableCsv(_csvPath);
            _probe = DevDiagnostics.Probe;
            DevDiagnostics.Telemetry.HudVisible = _options.Hud;
            DevDiagnostics.Telemetry.Telemetry.SnapshotReady += OnSnapshot;
        }

        private void OnSnapshot(PerfSnapshot snapshot) => _snapshots.Add(snapshot);

        private void SpawnAgents(int count)
        {
            _agents = new Transform[count];
            _velocities = new Vector3[count];
            _replicationBuffer = new byte[count * ReplicationBytesPerAgent];
            for (int i = 0; i < count; i++)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "TestAgent" + i;
                Destroy(cube.GetComponent<Collider>()); // 생물은 물리를 쓰지 않는다(SDD 3.12.3)
                if (_agentMaterial != null) cube.GetComponent<Renderer>().sharedMaterial = _agentMaterial;
                cube.transform.SetParent(transform, false);
                cube.transform.localScale = Vector3.one * AgentScaleMeters;
                cube.transform.position = new Vector3(NextRange(-ArenaHalfSizeMeters, ArenaHalfSizeMeters), AgentScaleMeters * 0.5f, NextRange(-ArenaHalfSizeMeters, ArenaHalfSizeMeters));
                _agents[i] = cube.transform;
                _velocities[i] = RandomHeading();
            }
        }

        private void Update()
        {
            if (_finished)
            {
                return;
            }
            _elapsed += Time.unscaledDeltaTime;
            _tickAccumulator += Time.unscaledDeltaTime;

            // 서버 틱 흉내: 30 Hz로 한 번씩 처리 구간을 돈다. 밀린 틱은 한 프레임에 최대 1회만 돌려 폭주를 막는다.
            if (_tickAccumulator >= ServerTickSeconds)
            {
                _tickAccumulator -= ServerTickSeconds;
                if (_tickAccumulator > ServerTickSeconds) _tickAccumulator = 0f;
                RunServerTick();
            }

            using (_probe.Scope(PerfMarkers.ClientViews))
            {
                MoveAgentViews(Time.deltaTime);
            }

            if (_elapsed >= _durationSeconds)
            {
                Finish();
            }
        }

        // ---------- 서버 틱 흉내 (틀 검증용: 실제 시스템으로 교체 예정) ----------

        private int _decideCursor;

        private void RunServerTick()
        {
            using (_probe.Scope(PerfMarkers.ServerTotal))
            {
                using (_probe.Scope(PerfMarkers.ServerCommands))
                {
                    // 자리표시: 명령 7명분 검증 (M1-07~M1-10에서 실제 명령 처리기로 교체)
                    SpinIntegerWork(40);
                }
                using (_probe.Scope(PerfMarkers.ServerAiDecide))
                {
                    // 틱당 20체만 판단(방향 다시 고르기)
                    for (int n = 0; n < AiDecidePerTick && _agents.Length > 0; n++)
                    {
                        _decideCursor = (_decideCursor + 1) % _agents.Length;
                        _velocities[_decideCursor] = RandomHeading();
                    }
                }
                using (_probe.Scope(PerfMarkers.ServerAiApply))
                {
                    SpinIntegerWork(_agents.Length * 3);
                }
                using (_probe.Scope(PerfMarkers.ServerSystems))
                {
                    SpinIntegerWork(60);
                }
                using (_probe.Scope(PerfMarkers.ServerReplication))
                {
                    // 생물 위치를 바이트 버퍼에 복사(직렬화 비용 흉내)
                    for (int i = 0; i < _agents.Length; i++)
                    {
                        Vector3 p = _agents[i].position;
                        int offset = i * ReplicationBytesPerAgent;
                        _replicationBuffer[offset] = (byte)(p.x * 4f);
                        _replicationBuffer[offset + 1] = (byte)(p.z * 4f);
                        _replicationBuffer[offset + 2] = (byte)i;
                    }
                }
            }
            _probe.Count(PerfCounters.ActiveCreatures, _agents.Length);
        }

        private int _spinSink;

        /// <summary>정수 계산으로 시간을 쓴다(컴파일러가 지우지 못하게 결과를 필드에 남긴다).</summary>
        private void SpinIntegerWork(int iterations)
        {
            int acc = _spinSink;
            for (int i = 0; i < iterations; i++)
            {
                acc = acc * 31 + i;
            }
            _spinSink = acc;
        }

        private void MoveAgentViews(float deltaTime)
        {
            for (int i = 0; i < _agents.Length; i++)
            {
                Vector3 p = _agents[i].position + _velocities[i] * deltaTime;
                // 경기장 밖으로 나가면 안쪽으로 되돌린다.
                if (Mathf.Abs(p.x) > ArenaHalfSizeMeters) _velocities[i].x = -_velocities[i].x;
                if (Mathf.Abs(p.z) > ArenaHalfSizeMeters) _velocities[i].z = -_velocities[i].z;
                _agents[i].position = p;
            }
        }

        // ---------- 마무리 ----------

        private void Finish()
        {
            _finished = true;
            string summaryPath = WriteSummary();
            Debug.Log(LogTag + "끝. 요약: " + summaryPath + ", CSV: " + _csvPath);
            LastSummaryPath = summaryPath;
            if (QuitWhenFinished) QuitPlayer(); // 에디터에서는 플레이 모드를 끝내고, 플레이어에서는 종료 코드 0으로 끝낸다.
        }

        private static void QuitPlayer()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit(0);
#endif
        }

        private string WriteSummary()
        {
            string directory = string.IsNullOrEmpty(_csvPath) ? Path.Combine(UnityEngine.Application.persistentDataPath, "Perf") : Path.GetDirectoryName(_csvPath);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, (string.IsNullOrEmpty(_csvPath) ? "bench_summary" : Path.GetFileNameWithoutExtension(_csvPath)) + "_summary.json");

            double fpsAvg = 0, fpsLow = double.MaxValue, serverAvg = 0, serverP99Worst = 0, serverTickMax = 0, mainAvg = 0, gpuAvg = 0;
            foreach (PerfSnapshot s in _snapshots)
            {
                PerfStat server = s.GetMarker(PerfMarkers.ServerTotal);
                fpsAvg += s.AverageFps;
                fpsLow = Math.Min(fpsLow, s.OnePercentLowFps);
                serverAvg += server.Average;
                serverP99Worst = Math.Max(serverP99Worst, server.P99);
                serverTickMax = Math.Max(serverTickMax, server.MaxCall);
                mainAvg += s.MainThreadMsAverage;
                gpuAvg += s.GpuMsAverage;
            }
            int n = Math.Max(1, _snapshots.Count);
            if (_snapshots.Count == 0) fpsLow = 0;

            var b = new StringBuilder();
            b.Append("{\n");
            b.Append("  \"scenario\": ").Append(MiniJson.Quote(_scenario.ToString())).Append(",\n");
            b.Append("  \"purpose\": \"frame-verification\",\n");
            b.Append("  \"note\": \"시험용 몹과 서버 틱 흉내로 측정 틀만 확인한 값입니다. 판정에 쓰지 않습니다.\",\n");
            b.Append("  \"seconds\": ").Append(_durationSeconds).Append(",\n");
            b.Append("  \"snapshots\": ").Append(_snapshots.Count).Append(",\n");
            b.Append("  \"preset\": ").Append(MiniJson.Quote(_graphics?.Backend.CurrentPreset?.Id.ToString() ?? "none")).Append(",\n");
            b.Append("  \"upscalerRequested\": ").Append(MiniJson.Quote(_options.Upscale.ToString())).Append(",\n");
            b.Append("  \"upscalerActive\": ").Append(MiniJson.Quote(_upscalerStatus.Mode.ToString())).Append(",\n");
            b.Append("  \"upscalerQuality\": ").Append(MiniJson.Quote(_upscalerStatus.Quality.ToString())).Append(",\n");
            b.Append("  \"gpu\": ").Append(MiniJson.Quote(SystemInfo.graphicsDeviceName)).Append(",\n");
            b.Append("  \"graphicsApi\": ").Append(MiniJson.Quote(SystemInfo.graphicsDeviceType.ToString())).Append(",\n");
            b.Append("  \"fpsAverage\": ").Append((fpsAvg / n).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(",\n");
            b.Append("  \"fps1PctLowWorst\": ").Append(fpsLow.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(",\n");
            b.Append("  \"mainThreadMsAverage\": ").Append((mainAvg / n).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(",\n");
            b.Append("  \"gpuMsAverage\": ").Append((gpuAvg / n).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(",\n");
            b.Append("  \"serverTotalMsAverage\": ").Append((serverAvg / n).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(",\n");
            b.Append("  \"serverTotalMsP99Worst\": ").Append(serverP99Worst.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(",\n");
            b.Append("  \"serverTickMsMax\": ").Append(serverTickMax.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(",\n");
            b.Append("  \"csv\": ").Append(MiniJson.Quote(_csvPath ?? string.Empty)).Append("\n");
            b.Append("}\n");
            File.WriteAllText(path, b.ToString());
            return path;
        }

        private void OnDestroy()
        {
            _graphics?.Backend.Dispose();
            if (DevDiagnostics.Telemetry != null)
            {
                DevDiagnostics.Telemetry.Telemetry.SnapshotReady -= OnSnapshot;
            }
        }

        // ---------- 난수(시험용 몹 움직임) ----------

        private float NextRange(float min, float max)
        {
            _rngState = _rngState * 1664525u + 1013904223u;
            return min + (_rngState >> 8) / (float)(1 << 24) * (max - min);
        }

        private Vector3 RandomHeading()
        {
            float angle = NextRange(0f, Mathf.PI * 2f);
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * AgentSpeedMps;
        }
    }
}

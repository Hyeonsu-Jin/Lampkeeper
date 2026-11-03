// =============================================================
// 파일: FileLogSink.cs
// 역할: Unity 로그(Debug.Log 등)를 파일로 남기는 기본 로그 기록기. 한 번 실행에 파일 하나이고,
//       모든 줄은 LogSanitizer를 거쳐 Steam ID·비밀 값·사용자 폴더 이름·외부 IP를 가린 뒤 기록한다.
// 레이어: infrastructure (Game.Infra.Platform)
// 관련: FR-158, NFR-042, NFR-045, DES-107 / SDD 3.7.3 (Logging·CrashReporter), M1-05
//
// M1-05 범위: 기본 파일 기록과 민감 정보 제외까지. 보관 개수·회전, 세션 잠금 파일, 충돌 보고 압축 파일은 M2-10.
// 외부 전송 경로는 없다(C-43): 네트워크 호출 코드를 두지 않는다.
// 스레드: logMessageReceivedThreaded는 어느 스레드에서든 불리므로 쓰기는 잠금으로 보호한다.
// =============================================================
using System;
using System.IO;
using System.Text;
using System.Threading;
using Game.Application.Logging;
using UnityEngine;

namespace Game.Infra.Platform.Logging
{
    /// <summary>파일 로그 기록기.</summary>
    public sealed class FileLogSink : IDisposable
    {
        /// <summary>쌓인 줄을 파일에 내보내는 간격(밀리초).</summary>
        private const int FlushIntervalMs = 1000;

        /// <summary>기본 로그 폴더 이름(저장 폴더 아래).</summary>
        public const string DefaultFolderName = "Logs";

        private readonly object _gate = new object();
        private readonly StreamWriter _writer;
        private readonly Timer _flushTimer;
        private bool _disposed;

        /// <summary>로그 파일 전체 경로.</summary>
        public string FilePath { get; }

        /// <summary>실행 중인 기록기(없으면 null). 한 프로세스에 하나만 둔다.</summary>
        public static FileLogSink Current { get; private set; }

        /// <summary>
        /// 만들고 Unity 로그 수신에 연결한다. 이미 설치돼 있으면 기존 것을 돌려준다.
        /// </summary>
        /// <param name="folder">로그 폴더. null이면 저장 폴더/Logs</param>
        /// <param name="now">현재 시각(UTC). 파일 이름에 쓴다. 시험에서 바꿀 수 있게 받음</param>
        public static FileLogSink Install(string folder = null, DateTime? now = null)
        {
            if (Current != null)
            {
                return Current;
            }
            string target = string.IsNullOrEmpty(folder) ? Path.Combine(UnityEngine.Application.persistentDataPath, DefaultFolderName) : folder;
            Current = new FileLogSink(target, now ?? DateTime.UtcNow);
            Current.Hook();
            return Current;
        }

        /// <summary>시험용: 연결 없이 파일만 만드는 생성자(Hook을 부르지 않는다).</summary>
        public static FileLogSink CreateUnhooked(string folder, DateTime now) => new FileLogSink(folder, now);

        private FileLogSink(string folder, DateTime now)
        {
            Directory.CreateDirectory(folder);
            string name = "game_" + now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".log";
            FilePath = Path.Combine(folder, name);
            _writer = new StreamWriter(new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false));
            WriteHeader(now);
            _flushTimer = new Timer(_ => Flush(), null, FlushIntervalMs, FlushIntervalMs);
        }

        private void Hook()
        {
            UnityEngine.Application.logMessageReceivedThreaded += OnLogMessage;
            UnityEngine.Application.quitting += Dispose;
        }

        private void WriteHeader(DateTime now)
        {
            // 시스템 정보는 게임·엔진 버전과 운영체제 종류만 적는다(PC 이름·사용자 이름 제외).
            WriteLine(LogLineFormatter.Format(now, "Info", "로그 시작. 게임 " + UnityEngine.Application.version
                                                         + ", Unity " + UnityEngine.Application.unityVersion
                                                         + ", " + SystemInfo.operatingSystemFamily
                                                         + (UnityEngine.Application.isEditor ? ", 에디터" : ", 플레이어")));
        }

        /// <summary>Unity 로그 한 건을 기록한다. 오류·예외는 호출 스택도 붙이고 바로 내보낸다.</summary>
        public void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            string text = condition;
            bool important = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            if (important && !string.IsNullOrEmpty(stackTrace))
            {
                text = condition + "\n" + stackTrace.TrimEnd();
            }
            WriteLine(LogLineFormatter.Format(DateTime.UtcNow, type.ToString(), text));
            if (important)
            {
                Flush();
            }
        }

        /// <summary>이미 가려진 한 줄을 파일에 쓴다.</summary>
        private void WriteLine(string line)
        {
            lock (_gate)
            {
                if (_disposed) return;
                _writer.WriteLine(line);
            }
        }

        /// <summary>쌓인 내용을 파일에 내보낸다.</summary>
        public void Flush()
        {
            lock (_gate)
            {
                if (_disposed) return;
                try
                {
                    _writer.Flush();
                }
                catch (IOException)
                {
                    // 디스크가 가득 찬 경우 등. 로그 때문에 게임이 멈추면 안 되므로 다음 주기에 다시 시도한다.
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _flushTimer.Dispose();
                try { _writer.Flush(); } catch (IOException) { }
                _writer.Dispose();
            }
            UnityEngine.Application.logMessageReceivedThreaded -= OnLogMessage;
            UnityEngine.Application.quitting -= Dispose;
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }
    }
}

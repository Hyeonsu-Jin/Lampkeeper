// =============================================================
// 파일: ConfigIssues.cs
// 역할: 설정 검증에서 찾은 문제(ConfigIssue)와, 문제 때문에 로드를 멈출 때 던지는 오류(ConfigLoadException).
// 레이어: application (순수 C#)
// 관련: NFR-033, DES-104 / SDD 3.7.7 ("검증: 필수 값, 범위, 참조 무결성")
// =============================================================
using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Application.Config
{
    /// <summary>문제의 심각도.</summary>
    public enum ConfigIssueSeverity
    {
        /// <summary>경고: 로드는 계속한다.</summary>
        Warning = 0,

        /// <summary>오류: 로드를 멈춘다.</summary>
        Error = 1,
    }

    /// <summary>설정 검증에서 찾은 문제 한 건.</summary>
    public readonly struct ConfigIssue
    {
        public ConfigIssueSeverity Severity { get; }

        /// <summary>문제 위치(예: "Balance.Movement.WalkSpeedMps", "Content[item.wood]").</summary>
        public string Path { get; }

        /// <summary>무엇이 문제인지.</summary>
        public string Message { get; }

        public ConfigIssue(ConfigIssueSeverity severity, string path, string message)
        {
            Severity = severity;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public static ConfigIssue Error(string path, string message) =>
            new ConfigIssue(ConfigIssueSeverity.Error, path, message);

        public static ConfigIssue Warning(string path, string message) =>
            new ConfigIssue(ConfigIssueSeverity.Warning, path, message);

        public override string ToString() => $"[{Severity}] {Path}: {Message}";
    }

    /// <summary>설정에 오류가 있어 게임을 시작할 수 없을 때 던진다.</summary>
    public sealed class ConfigLoadException : Exception
    {
        /// <summary>찾은 문제 전체(경고 포함).</summary>
        public IReadOnlyList<ConfigIssue> Issues { get; }

        public ConfigLoadException(IReadOnlyList<ConfigIssue> issues)
            : base(BuildMessage(issues))
        {
            Issues = issues;
        }

        private static string BuildMessage(IReadOnlyList<ConfigIssue> issues)
        {
            var builder = new StringBuilder("설정 검증 실패:");
            foreach (ConfigIssue issue in issues)
            {
                if (issue.Severity == ConfigIssueSeverity.Error)
                {
                    builder.Append("\n  - ").Append(issue.Path).Append(": ").Append(issue.Message);
                }
            }
            return builder.ToString();
        }
    }
}

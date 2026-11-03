// =============================================================
// 파일: LaunchOptions.cs
// 역할: 개발용 실행 옵션(-transport, -host, -connect, -devPlayerId)의 값 객체와 전송 종류.
// 레이어: application (순수 C#)
// 관련: FR-006(전송 방식 전환), NFR-031, DES-103 / SDD 3.7.4
// =============================================================
using System;
using System.Collections.Generic;

namespace Game.Application.Boot
{
    /// <summary>전송 방식. Steam(출시·일반)과 Tugboat(개발용 직접 전송).</summary>
    public enum TransportKind
    {
        /// <summary>Steam 릴레이(FishySteamworks). 출시 빌드는 이것으로 고정.</summary>
        Steam = 0,

        /// <summary>FishNet 기본 UDP 전송(Tugboat). 개발 빌드 전용.</summary>
        Tugboat = 1,
    }

    /// <summary>직접 전송 접속 주소(ip:port).</summary>
    public readonly struct NetworkEndpoint : IEquatable<NetworkEndpoint>
    {
        public string Host { get; }
        public ushort Port { get; }

        public NetworkEndpoint(string host, ushort port)
        {
            Host = host ?? string.Empty;
            Port = port;
        }

        public bool Equals(NetworkEndpoint other) => string.Equals(Host, other.Host, StringComparison.Ordinal) && Port == other.Port;
        public override bool Equals(object obj) => obj is NetworkEndpoint other && Equals(other);
        public override int GetHashCode() => (Host?.GetHashCode() ?? 0) * 397 ^ Port;
        public override string ToString() => Host + ":" + Port;
    }

    /// <summary>
    /// 명령줄에서 읽은 개발용 실행 옵션. 출시 빌드에서는 <see cref="CheckPlatformReady"/>가 무시한다.
    /// </summary>
    public sealed class LaunchOptions
    {
        /// <summary>옵션이 하나도 없는 기본값.</summary>
        public static LaunchOptions None { get; } = new LaunchOptions(null, false, null, null, Array.Empty<string>());

        /// <summary>-transport 로 요청한 전송. 없으면 null.</summary>
        public TransportKind? RequestedTransport { get; }

        /// <summary>-host 가 있으면 true (직접 전송으로 호스트).</summary>
        public bool Host { get; }

        /// <summary>-connect 로 준 접속 주소. 없으면 null.</summary>
        public NetworkEndpoint? Connect { get; }

        /// <summary>-devPlayerId 로 준 개발용 플레이어 ID. 없으면 null.</summary>
        public ulong? DevPlayerId { get; }

        /// <summary>해석하지 못한 옵션·값 오류 설명(실행은 계속하고 로그에 남긴다).</summary>
        public IReadOnlyList<string> Errors { get; }

        public LaunchOptions(
            TransportKind? requestedTransport,
            bool host,
            NetworkEndpoint? connect,
            ulong? devPlayerId,
            IReadOnlyList<string> errors)
        {
            RequestedTransport = requestedTransport;
            Host = host;
            Connect = connect;
            DevPlayerId = devPlayerId;
            Errors = errors ?? Array.Empty<string>();
        }

        /// <summary>개발용 옵션이 하나라도 지정됐는지.</summary>
        public bool HasAnyDevOption =>
            RequestedTransport.HasValue || Host || Connect.HasValue || DevPlayerId.HasValue;
    }
}

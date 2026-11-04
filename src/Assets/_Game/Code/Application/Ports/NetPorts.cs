// =============================================================
// 파일: NetPorts.cs
// 역할: 네트워크 전송 포트(INetServer, INetClient), 틱 신호 포트(ITickSource), 내 자세 전달 포트(ILocalPlayerPose),
//       공통 보조 포트(ILog, IClock, IBanListStore)와 기본 구현.
// 레이어: application (포트). 구현: Adapters.Net(FishNetServer/FishNetClient), Presentation/Bootstrap, Infra.Platform
// 관련: NFR-031(전송 종류를 모름), NFR-040, FR-005, DES-051, DES-121 / SDD 3.7.3
//
// 시그니처에 FishNet 형식이 없다. 시험은 가짜 구현으로 한다(Game.Tests.Net).
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Protocol;
using Game.Domain.Common;

namespace Game.Application.Ports
{
    /// <summary>서버 쪽 전송. 호스트 모드에서는 호스트 자신의 클라이언트도 연결 하나로 들어온다(D-19).</summary>
    public interface INetServer
    {
        /// <summary>서버가 연결을 받는 중인지.</summary>
        bool IsRunning { get; }

        /// <summary>새 연결. (연결 번호, 전송이 알려 주는 원격 ID: Steam이면 SteamID 문자열, 개발 전송이면 주소)</summary>
        event Action<int, string> Connected;

        /// <summary>연결이 끊김(연결 번호).</summary>
        event Action<int> Disconnected;

        /// <summary>디코드된 메시지가 도착함(연결 번호, 메시지). 디코드 실패는 구현이 버리고 로그를 남긴다.</summary>
        event Action<int, INetMessage> Received;

        /// <summary>연결 받기를 시작한다.</summary>
        /// <returns>시작 요청에 성공하면 true</returns>
        bool Start();

        /// <summary>연결 받기를 멈추고 모든 연결을 끊는다.</summary>
        void Stop();

        /// <summary>메시지 하나를 한 연결에 보낸다. 크기 상한을 넘으면 보내지 않고 false.</summary>
        bool Send<T>(int connectionId, in T message, NetChannel channel) where T : struct, INetMessage;

        /// <summary>연결 번호 목록 중 filter가 true인 연결 모두에 보낸다(filter가 null이면 전체).</summary>
        void Broadcast<T>(in T message, NetChannel channel, Func<int, bool> filter) where T : struct, INetMessage;

        /// <summary>한 연결을 끊는다.</summary>
        void Disconnect(int connectionId);
    }

    /// <summary>클라이언트 연결 상태.</summary>
    public enum NetClientState : byte
    {
        Stopped = 0,
        Connecting = 1,
        Connected = 2,
    }

    /// <summary>클라이언트 쪽 전송.</summary>
    public interface INetClient
    {
        /// <summary>현재 연결 상태.</summary>
        NetClientState State { get; }

        /// <summary>연결 상태가 바뀜.</summary>
        event Action<NetClientState> StateChanged;

        /// <summary>디코드된 메시지가 도착함.</summary>
        event Action<INetMessage> Received;

        /// <summary>
        /// 서버에 연결을 시도한다. Steam 전송이면 address는 호스트의 SteamID(문자열)이고 port는 무시된다.
        /// </summary>
        bool Connect(string address, ushort port);

        /// <summary>연결을 끊는다.</summary>
        void Disconnect();

        /// <summary>메시지를 서버로 보낸다.</summary>
        bool Send<T>(in T message, NetChannel channel) where T : struct, INetMessage;
    }

    /// <summary>서버 틱 신호(30 Hz). FishNet TimeManager가 구현의 뒤에 있다.</summary>
    public interface ITickSource
    {
        /// <summary>틱마다 불린다(틱 번호, 틱 간격 초).</summary>
        event Action<uint, float> Ticked;
    }

    /// <summary>
    /// 내 플레이어 자세 전달. 표현 어댑터(FirstPersonController)가 쓰고 Net 어댑터(PlayerAvatar)가 읽는다.
    /// 어댑터끼리 참조할 수 없어 애플리케이션 포트로 잇는다(SDD 3.4.1 규칙 4).
    /// </summary>
    public interface ILocalPlayerPose
    {
        /// <summary>표현 쪽: 이번 프레임 로컬 이동 결과를 쓴다(클라이언트 계산).</summary>
        void Write(in AvatarPose pose);

        /// <summary>Net 쪽: 가장 최근 자세를 읽는다. 아직 없으면 false.</summary>
        bool TryRead(out AvatarPose pose);

        /// <summary>Net 쪽: 호스트가 정한 위치(스폰, 이동 거부 보정)를 알린다.</summary>
        void ApplyCorrection(Vec3f position);

        /// <summary>표현 쪽: 알려 온 보정 위치를 한 번 가져간다. 없으면 false.</summary>
        bool TryTakeCorrection(out Vec3f position);
    }

    /// <summary><see cref="ILocalPlayerPose"/>의 단순 보관 구현. 조립 루트가 하나를 만들어 두 어댑터에 넘긴다.</summary>
    public sealed class LocalPlayerPoseBuffer : ILocalPlayerPose
    {
        private AvatarPose _pose;
        private bool _hasPose;
        private Vec3f _correction;
        private bool _hasCorrection;

        public void Write(in AvatarPose pose)
        {
            _pose = pose;
            _hasPose = true;
        }

        public bool TryRead(out AvatarPose pose)
        {
            pose = _pose;
            return _hasPose;
        }

        public void ApplyCorrection(Vec3f position)
        {
            _correction = position;
            _hasCorrection = true;
        }

        public bool TryTakeCorrection(out Vec3f position)
        {
            position = _correction;
            bool had = _hasCorrection;
            _hasCorrection = false;
            return had;
        }
    }

    /// <summary>로그 포트. 채팅·비밀번호를 넘기지 않는 것이 규칙이다.</summary>
    public interface ILog
    {
        void Info(string category, string message);
        void Warn(string category, string message);
        void Error(string category, string message);
    }

    /// <summary>아무것도 남기지 않는 로그(시험, 기본값).</summary>
    public sealed class NullLog : ILog
    {
        public static NullLog Instance { get; } = new NullLog();
        public void Info(string category, string message) { }
        public void Warn(string category, string message) { }
        public void Error(string category, string message) { }
    }

    /// <summary>시계 포트. 시험에서는 가짜 시계를 쓴다.</summary>
    public interface IClock
    {
        /// <summary>프로그램이 켜진 뒤의 실시간(초).</summary>
        double RealtimeSeconds { get; }
    }

    /// <summary>호스트의 차단 목록 조회. [FR-148, C-46] 채우기·저장은 M2(bans.json)에서 확장한다.</summary>
    public interface IBanListStore
    {
        bool Contains(PlayerId player);
    }

    /// <summary>차단 목록이 없는 기본 구현.</summary>
    public sealed class EmptyBanListStore : IBanListStore
    {
        public static EmptyBanListStore Instance { get; } = new EmptyBanListStore();
        public bool Contains(PlayerId player) => false;
    }

    /// <summary>메시지 송신 확장.</summary>
    public static class NetSendExtensions
    {
        /// <summary>메시지 종류의 기본 채널로 보낸다.</summary>
        public static bool SendDefault<T>(this INetServer server, int connectionId, in T message) where T : struct, INetMessage =>
            server.Send(connectionId, message, MessageCodec.DefaultChannel(message.Kind));

        /// <summary>메시지 종류의 기본 채널로 보낸다.</summary>
        public static bool SendDefault<T>(this INetClient client, in T message) where T : struct, INetMessage =>
            client.Send(message, MessageCodec.DefaultChannel(message.Kind));
    }
}

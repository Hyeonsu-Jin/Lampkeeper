// =============================================================
// 파일: ILobbyService.cs
// 역할: Steam 로비(세션 방) 포트. 로비 생성·검색·참가·초대와 메타데이터 키 상수, 메타데이터 조립 도우미.
// 레이어: application (포트). 구현: Infra.Steam(SteamLobbyService)
// 관련: FR-001~003, FR-145, NFR-009, DES-051, DES-054, DES-102 / SDD 3.7.1, 3.7.3
//
// Steam 호출은 비동기(콜백)이므로 결과는 콜백으로 받는다. 구현은 메인 스레드 Tick 안에서 콜백을 부른다.
// =============================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Domain.Common;
using Game.Domain.Session;

namespace Game.Application.Ports
{
    /// <summary>로비 번호(Steam이면 로비 SteamID 64비트).</summary>
    public readonly struct LobbyId : IEquatable<LobbyId>
    {
        public ulong Value { get; }
        public bool IsValid => Value != 0UL;
        public LobbyId(ulong value) { Value = value; }
        public bool Equals(LobbyId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is LobbyId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>로비 메타데이터 키(SDD 3.7.1). 비밀번호 원문은 넣지 않는다(FR-146).</summary>
    public static class LobbyMetaKeys
    {
        /// <summary>게임 식별 문자열. App ID 480(Spacewar)에서 다른 게임 로비를 거르는 필터. [NFR-009]</summary>
        public const string Game = "kh_game";
        /// <summary>프로토콜 버전.</summary>
        public const string Proto = "kh_proto";
        /// <summary>게임 버전 전체 문자열(GameVersion.Full, 예 0.1.0+dev.20261104153012.a1b2c3d). 값은 SemVer 문자열이며 키 이름은 유지. [NFR-058]</summary>
        public const string Build = "kh_build";
        /// <summary>월드 이름.</summary>
        public const string World = "kh_world";
        /// <summary>호스트 표시 이름.</summary>
        public const string Host = "kh_host";
        /// <summary>접속 인원.</summary>
        public const string Count = "kh_count";
        /// <summary>재접속 예약 자리 수(M2).</summary>
        public const string Reserved = "kh_resv";
        /// <summary>비밀번호 여부 0/1.</summary>
        public const string Password = "kh_pw";
        /// <summary>참가 조건 invite/friends/public.</summary>
        public const string Access = "kh_access";

        /// <summary>kh_game 값. 이 값이 같은 로비만 이 게임의 세션이다.</summary>
        public const string GameIdValue = "KKEOJIN_HANEUL_SURVIVE";
    }

    /// <summary>로비 목록의 한 항목.</summary>
    public sealed class LobbyInfo
    {
        public LobbyId Id { get; }
        /// <summary>로비 주인(호스트)의 플레이어 ID. 클라이언트는 이 ID로 Steam 전송에 연결한다.</summary>
        public PlayerId Owner { get; }
        public int MemberCount { get; }
        public int MaxMembers { get; }
        public IReadOnlyDictionary<string, string> Meta { get; }

        public LobbyInfo(LobbyId id, PlayerId owner, int memberCount, int maxMembers, IReadOnlyDictionary<string, string> meta)
        {
            Id = id;
            Owner = owner;
            MemberCount = memberCount;
            MaxMembers = maxMembers;
            Meta = meta ?? new Dictionary<string, string>();
        }

        /// <summary>메타데이터 값을 읽는다. 없으면 빈 문자열.</summary>
        public string GetMeta(string key) => Meta.TryGetValue(key, out string value) ? value : string.Empty;
    }

    /// <summary>로비 작업 실패 이유.</summary>
    public enum LobbyError : byte
    {
        None = 0,
        NotAvailable = 1,
        Failed = 2,
        Timeout = 3,
        NoAccess = 4,
        Full = 5,
        DoesNotExist = 6,
    }

    /// <summary>로비 작업 결과(생성·참가).</summary>
    public readonly struct LobbyResult
    {
        public LobbyError Error { get; }
        public LobbyInfo Lobby { get; }
        public bool Success => Error == LobbyError.None;

        public LobbyResult(LobbyError error, LobbyInfo lobby)
        {
            Error = error;
            Lobby = lobby;
        }
    }

    /// <summary>Steam 로비 포트.</summary>
    public interface ILobbyService
    {
        /// <summary>로비를 쓸 수 있는지(Steam 초기화 완료).</summary>
        bool IsAvailable { get; }

        /// <summary>지금 들어가 있는 로비. 없으면 무효 값.</summary>
        LobbyId Current { get; }

        /// <summary>초대 수락·친구 목록 「게임 참가」로 들어오라는 요청이 왔음.</summary>
        event Action<LobbyId> JoinRequested;

        /// <summary>로비 멤버가 들어오거나 나감(현재 멤버 수).</summary>
        event Action<int> MembersChanged;

        /// <summary>로비를 만든다. 참가 조건이 Steam 로비 유형(초대/친구/공개)으로 바뀐다.</summary>
        void Create(SessionAccess access, int maxMembers, IReadOnlyDictionary<string, string> meta, Action<LobbyResult> onDone);

        /// <summary>내 로비의 참가 조건을 바꾼다.</summary>
        void SetAccess(SessionAccess access);

        /// <summary>내 로비의 메타데이터 한 항목을 바꾼다.</summary>
        void SetMeta(string key, string value);

        /// <summary>kh_game 필터로 로비를 검색한다.</summary>
        void RequestList(Action<LobbyError, IReadOnlyList<LobbyInfo>> onDone);

        /// <summary>로비에 들어간다.</summary>
        void Join(LobbyId lobby, Action<LobbyResult> onDone);

        /// <summary>플레이어가 지금 로비 멤버인지.</summary>
        bool IsMember(PlayerId player);

        /// <summary>로비를 나간다.</summary>
        void Leave();

        /// <summary>Steam 친구 초대 창을 연다.</summary>
        /// <returns>열었으면 true. 로비가 없거나 오버레이를 쓸 수 없으면 false(호출자가 안내 문구를 보인다)</returns>
        bool OpenInviteDialog();
    }

    /// <summary>로비 메타데이터 조립(순수 규칙).</summary>
    public static class LobbyMetaBuilder
    {
        /// <summary>참가 조건을 메타데이터 값(invite/friends/public)으로 바꾼다. [3.7.1]</summary>
        public static string AccessValue(SessionAccess access)
        {
            switch (access)
            {
                case SessionAccess.InviteOnly: return "invite";
                case SessionAccess.Public: return "public";
                default: return "friends";
            }
        }

        /// <summary>
        /// 로비를 만들 때 넣을 메타데이터 전체를 만든다.
        /// </summary>
        public static Dictionary<string, string> Build(string worldName, string hostName, int connectedCount, int reservedCount,
            string gameVersion, SessionAccess access, bool hasPassword)
        {
            return new Dictionary<string, string>
            {
                { LobbyMetaKeys.Game, LobbyMetaKeys.GameIdValue },
                { LobbyMetaKeys.Proto, Protocol.ProtocolConstants.ProtocolVersion.ToString(CultureInfo.InvariantCulture) },
                { LobbyMetaKeys.Build, gameVersion ?? string.Empty },
                { LobbyMetaKeys.World, worldName ?? string.Empty },
                { LobbyMetaKeys.Host, hostName ?? string.Empty },
                { LobbyMetaKeys.Count, connectedCount.ToString(CultureInfo.InvariantCulture) },
                { LobbyMetaKeys.Reserved, reservedCount.ToString(CultureInfo.InvariantCulture) },
                { LobbyMetaKeys.Password, hasPassword ? "1" : "0" },
                { LobbyMetaKeys.Access, AccessValue(access) },
            };
        }
    }
}

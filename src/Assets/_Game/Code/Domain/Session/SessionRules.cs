// =============================================================
// 파일: SessionRules.cs
// 역할: 세션 참가 판정 규칙(인원 제한, 버전 일치, 차단, 참가 경로와 참가 조건).
// 레이어: domain (Game.Domain, 순수 C#)
// 관련: FR-003, FR-004, FR-144, FR-148(차단), NFR-038, DES-031 / SDD 3.9.2, 3.7.2
//
// 비밀번호(FR-146)와 재접속 예약(FR-115)은 M2 범위라 이번에는 판정하지 않는다.
// 예약 자리 수(ReservedCount)는 M2를 위해 인원 계산에만 반영한다.
// =============================================================
using System;
using Game.Domain.Common;

namespace Game.Domain.Session
{
    /// <summary>참가 조건. 월드 설정에 저장되고 새 월드 기본값은 친구 전용이다. [FR-144]</summary>
    public enum SessionAccess : byte
    {
        /// <summary>초대받은 사람만.</summary>
        InviteOnly = 0,

        /// <summary>Steam 친구 또는 초대받은 사람.</summary>
        FriendsOnly = 1,

        /// <summary>누구나(공개 목록에 표시).</summary>
        Public = 2,
    }

    /// <summary>참가자가 들어온 길. 전송된 값이라 신뢰하지 않고, 참가 조건 판정 보조로만 쓴다.</summary>
    public enum JoinPath : byte
    {
        Invite = 0,
        Friend = 1,
        PublicList = 2,
        Rejoin = 3,
    }

    /// <summary>참가 거부 사유. 숫자 값은 네트워크로 전송되므로 바꾸지 않는다. 화면 텍스트 키 `join.*` (SDD 3.7.5).</summary>
    public enum JoinRejectReason : byte
    {
        /// <summary>거부 아님(허용).</summary>
        None = 0,
        /// <summary>인원 초과(join.full).</summary>
        Full = 1,
        /// <summary>게임 버전 또는 콘텐츠 요약값 불일치(join.version).</summary>
        VersionMismatch = 2,
        /// <summary>비밀번호 불일치(join.password). M2.</summary>
        PasswordMismatch = 3,
        /// <summary>차단됨(join.banned).</summary>
        Banned = 4,
        /// <summary>참가 조건 불충족(join.access). 로비 멤버가 아닌 직접 연결도 여기에 해당.</summary>
        AccessDenied = 5,
        /// <summary>시간 초과(join.timeout).</summary>
        Timeout = 6,
        /// <summary>월드 생성 불일치(join.gen_mismatch).</summary>
        GenMismatch = 7,
        /// <summary>시도 횟수 초과(join.attempt_limit). M2.</summary>
        AttemptLimit = 8,
        /// <summary>요청 형식이 잘못됨 또는 이미 참가 중.</summary>
        BadRequest = 9,
    }

    /// <summary>호스트가 알고 있는 세션 사실과 참가 요청 내용. 판정 입력.</summary>
    public readonly struct JoinFacts
    {
        public PlayerId Player { get; }
        public bool IsBanned { get; }
        public bool IsLobbyMember { get; }
        public bool RequiresLobbyMembership { get; }
        public int ConnectedCount { get; }
        public int ReservedCount { get; }
        public SessionAccess Access { get; }
        public JoinPath Path { get; }
        public ushort ProtocolVersion { get; }
        /// <summary>참가자가 보낸 게임 버전 전체 문자열(메타데이터 포함). 신뢰하지 않는 값이라 문자열로 두고 판정 때 해석한다. [NFR-058]</summary>
        public string GameVersion { get; }
        public ulong ContentHash { get; }
        public bool GeneratorVersionSupported { get; }

        public JoinFacts(PlayerId player, bool isBanned, bool isLobbyMember, bool requiresLobbyMembership,
            int connectedCount, int reservedCount, SessionAccess access, JoinPath path,
            ushort protocolVersion, string gameVersion, ulong contentHash, bool generatorVersionSupported)
        {
            Player = player;
            IsBanned = isBanned;
            IsLobbyMember = isLobbyMember;
            RequiresLobbyMembership = requiresLobbyMembership;
            ConnectedCount = connectedCount;
            ReservedCount = reservedCount;
            Access = access;
            Path = path;
            ProtocolVersion = protocolVersion;
            GameVersion = gameVersion;
            ContentHash = contentHash;
            GeneratorVersionSupported = generatorVersionSupported;
        }
    }

    /// <summary>참가 판정 규칙. 상태가 없고 설정은 생성자로 받는다.</summary>
    public sealed class SessionRules
    {
        private readonly int _maxPlayers;
        private readonly ushort _protocolVersion;
        private readonly GameVersion _gameVersion;
        private readonly ulong _contentHash;

        /// <param name="maxPlayers">최대 인원(접속 + 예약). 설정값 `최대 인원`, 기본 8 [NFR-038]</param>
        /// <param name="protocolVersion">호스트의 프로토콜 버전</param>
        /// <param name="gameVersion">호스트의 게임 버전(메타데이터 포함)</param>
        /// <param name="contentHash">호스트의 콘텐츠 요약값</param>
        /// <exception cref="ArgumentOutOfRangeException">maxPlayers가 1보다 작을 때</exception>
        public SessionRules(int maxPlayers, ushort protocolVersion, GameVersion gameVersion, ulong contentHash)
        {
            if (maxPlayers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPlayers), "최대 인원은 1 이상이어야 한다.");
            }
            _maxPlayers = maxPlayers;
            _protocolVersion = protocolVersion;
            _gameVersion = gameVersion ?? throw new ArgumentNullException(nameof(gameVersion));
            _contentHash = contentHash;
        }

        /// <summary>최대 인원.</summary>
        public int MaxPlayers => _maxPlayers;

        /// <summary>
        /// 참가 요청을 판정한다. 먼저 걸리는 규칙의 사유를 돌려준다.
        /// 순서: 차단 → 버전·콘텐츠 → 참가 조건 → 인원. (차단된 사람에게 다른 정보를 주지 않기 위해 차단을 가장 먼저 본다)
        /// </summary>
        /// <returns>허용이면 None</returns>
        public JoinRejectReason Evaluate(in JoinFacts facts)
        {
            // [FR-148] 차단된 계정은 어떤 경우에도 막는다.
            if (facts.IsBanned)
            {
                return JoinRejectReason.Banned;
            }

            // [FR-003, NFR-058] 프로토콜·게임 버전·콘텐츠 요약값이 다르면 PC마다 데이터가 달라 동기화가 깨지므로 거부한다.
            // 검사 순서: 프로토콜 → 게임 버전 전체 문자열 → 콘텐츠 → 생성기. 번호의 크기와 무관하게 「같은 빌드」만 허용한다.
            if (facts.ProtocolVersion != _protocolVersion)
            {
                return JoinRejectReason.VersionMismatch;
            }
            // 해석에 실패한 값도 불일치다. 비교는 메타데이터까지 포함한 전체 문자열(서수)이다.
            if (!Common.GameVersion.TryParse(facts.GameVersion, out GameVersion theirs) || !_gameVersion.IsSameBuild(theirs))
            {
                return JoinRejectReason.VersionMismatch;
            }
            if (facts.ContentHash != _contentHash)
            {
                return JoinRejectReason.VersionMismatch;
            }
            if (!facts.GeneratorVersionSupported)
            {
                return JoinRejectReason.VersionMismatch;
            }

            // [D-14] Steam 전송에서는 로비 멤버가 아닌 직접 연결을 막는다(초대 전용 우회 방지).
            if (facts.RequiresLobbyMembership && !facts.IsLobbyMember)
            {
                return JoinRejectReason.AccessDenied;
            }

            // 참가 조건과 참가 경로. 초대 전용은 초대·재접속 경로만 허용한다.
            if (facts.Access == SessionAccess.InviteOnly && facts.Path != JoinPath.Invite && facts.Path != JoinPath.Rejoin)
            {
                return JoinRejectReason.AccessDenied;
            }
            // 친구 전용은 공개 목록 경로를 허용하지 않는다(공개 목록에 나오지 않는 방).
            if (facts.Access == SessionAccess.FriendsOnly && facts.Path == JoinPath.PublicList)
            {
                return JoinRejectReason.AccessDenied;
            }

            // [NFR-038] 접속 인원 + 재접속 예약이 최대 인원에 차면 거부한다.
            if (facts.ConnectedCount + facts.ReservedCount >= _maxPlayers)
            {
                return JoinRejectReason.Full;
            }
            return JoinRejectReason.None;
        }
    }
}

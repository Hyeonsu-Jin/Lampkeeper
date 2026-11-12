// =============================================================
// 파일: SteamLobbyService.cs
// 역할: Steamworks.NET으로 로비(세션 방)를 만들고 검색하고 참가하고 초대한다. ILobbyService 구현.
// 레이어: infrastructure (Game.Infra.Steam). 포트 구현: ILobbyService
// 관련: FR-001~003, FR-005, FR-145, NFR-009, DES-102 / SDD 3.7.1, 3.7.3
//
// 참가 조건 → Steam 로비 유형: 초대 전용 = Private, 친구 전용 = FriendsOnly, 공개 = Public.
// 주의(Steam 동작): FriendsOnly 로비는 로비 「검색」 목록에는 나오지 않고 친구 목록의 「게임 참가」와 초대로 들어온다.
//   검색 목록(공개 세션 목록)은 M2에서 참가 조건 「공개」를 열 때 쓴다. 검색 코드는 M1부터 만들어 둔다.
// 모든 Steam 호출은 메인 스레드에서 한다(콜백은 SteamAPI.RunCallbacks 안에서 불린다).
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Ports;
using Game.Domain.Common;
using Game.Domain.Session;
using Steamworks;

namespace Game.Infra.Steam
{
    /// <summary>Steam 로비 서비스. Steam 초기화에 성공한 뒤에만 만든다.</summary>
    public sealed class SteamLobbyService : ILobbyService, IDisposable
    {
        /// <summary>목록 검색 결과 최대 개수(M2 공개 목록에서 조정).</summary>
        private const int MaxListResults = 50;

        /// <summary>메타데이터 키·값 읽기 버퍼 크기(Steam 한도 255 + 끝 문자).</summary>
        private const int MetaBufferSize = 256;

        /// <summary>참가 성공 응답 코드(EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess).</summary>
        private const uint EnterSuccess = 1;

        private readonly CallResult<LobbyCreated_t> _createdResult;
        private readonly CallResult<LobbyMatchList_t> _listResult;
        private readonly CallResult<LobbyEnter_t> _enterResult;
        private readonly Callback<GameLobbyJoinRequested_t> _joinRequested;
        private readonly Callback<LobbyChatUpdate_t> _chatUpdate;

        private Action<LobbyResult> _onCreated;
        private Action<LobbyError, IReadOnlyList<LobbyInfo>> _onList;
        private Action<LobbyResult> _onJoined;
        private SessionAccess _access;
        private int _maxMembers;

        public SteamLobbyService()
        {
            _createdResult = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            _listResult = CallResult<LobbyMatchList_t>.Create(OnLobbyList);
            _enterResult = CallResult<LobbyEnter_t>.Create(OnLobbyEnter);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            _chatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        }

        public bool IsAvailable => SteamAPI.IsSteamRunning();

        public LobbyId Current { get; private set; }

        public event Action<LobbyId> JoinRequested;
        public event Action<int> MembersChanged;

        public void Create(SessionAccess access, int maxMembers, IReadOnlyDictionary<string, string> meta, Action<LobbyResult> onDone)
        {
            if (onDone == null)
            {
                throw new ArgumentNullException(nameof(onDone));
            }
            _access = access;
            _maxMembers = maxMembers;
            _pendingMeta = meta;
            _onCreated = onDone;
            SteamAPICall_t call = SteamMatchmaking.CreateLobby(ToSteamType(access), maxMembers);
            _createdResult.Set(call);
        }

        private IReadOnlyDictionary<string, string> _pendingMeta;

        public void SetAccess(SessionAccess access)
        {
            _access = access;
            if (Current.IsValid)
            {
                SteamMatchmaking.SetLobbyType(new CSteamID(Current.Value), ToSteamType(access));
            }
        }

        public void SetMeta(string key, string value)
        {
            if (Current.IsValid && IsOwner())
            {
                SteamMatchmaking.SetLobbyData(new CSteamID(Current.Value), key, value ?? string.Empty);
            }
        }

        public void RequestList(Action<LobbyError, IReadOnlyList<LobbyInfo>> onDone)
        {
            if (onDone == null)
            {
                throw new ArgumentNullException(nameof(onDone));
            }
            _onList = onDone;

            // [NFR-009] App ID 480(Spacewar)은 다른 게임 로비가 섞이므로 kh_game 값이 같은 로비만 받는다.
            SteamMatchmaking.AddRequestLobbyListStringFilter(LobbyMetaKeys.Game, LobbyMetaKeys.GameIdValue, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(MaxListResults);
            _listResult.Set(SteamMatchmaking.RequestLobbyList());
        }

        public void Join(LobbyId lobby, Action<LobbyResult> onDone)
        {
            if (onDone == null)
            {
                throw new ArgumentNullException(nameof(onDone));
            }
            _onJoined = onDone;
            _enterResult.Set(SteamMatchmaking.JoinLobby(new CSteamID(lobby.Value)));
        }

        public bool IsMember(PlayerId player)
        {
            if (!Current.IsValid)
            {
                return false;
            }
            var lobby = new CSteamID(Current.Value);
            int count = SteamMatchmaking.GetNumLobbyMembers(lobby);
            for (int i = 0; i < count; i++)
            {
                if (SteamMatchmaking.GetLobbyMemberByIndex(lobby, i).m_SteamID == player.Value)
                {
                    return true;
                }
            }
            return false;
        }

        public void Leave()
        {
            if (Current.IsValid)
            {
                SteamMatchmaking.LeaveLobby(new CSteamID(Current.Value));
                Current = default;
            }
        }

        public bool OpenInviteDialog()
        {
            // 로비가 없거나 Steam 오버레이를 쓸 수 없으면(에디터 등) 열지 못한다. 호출자가 안내 문구를 보인다.
            if (!Current.IsValid || !SteamUtils.IsOverlayEnabled())
            {
                return false;
            }
            SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(Current.Value));
            return true;
        }

        public void Dispose()
        {
            _createdResult.Dispose();
            _listResult.Dispose();
            _enterResult.Dispose();
            _joinRequested.Dispose();
            _chatUpdate.Dispose();
        }

        // ---------- 콜백 ----------

        private void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
        {
            Action<LobbyResult> done = _onCreated;
            _onCreated = null;
            if (ioFailure || result.m_eResult != EResult.k_EResultOK)
            {
                done?.Invoke(new LobbyResult(ioFailure ? LobbyError.Failed : MapError(result.m_eResult), null));
                return;
            }
            var id = new CSteamID(result.m_ulSteamIDLobby);
            Current = new LobbyId(result.m_ulSteamIDLobby);
            if (_pendingMeta != null)
            {
                foreach (KeyValuePair<string, string> pair in _pendingMeta)
                {
                    SteamMatchmaking.SetLobbyData(id, pair.Key, pair.Value ?? string.Empty);
                }
            }
            _pendingMeta = null;
            done?.Invoke(new LobbyResult(LobbyError.None, ReadInfo(id)));
        }

        private void OnLobbyList(LobbyMatchList_t result, bool ioFailure)
        {
            Action<LobbyError, IReadOnlyList<LobbyInfo>> done = _onList;
            _onList = null;
            if (ioFailure)
            {
                done?.Invoke(LobbyError.Failed, new List<LobbyInfo>());
                return;
            }
            var list = new List<LobbyInfo>((int)result.m_nLobbiesMatching);
            for (int i = 0; i < result.m_nLobbiesMatching; i++)
            {
                list.Add(ReadInfo(SteamMatchmaking.GetLobbyByIndex(i)));
            }
            done?.Invoke(LobbyError.None, list);
        }

        private void OnLobbyEnter(LobbyEnter_t result, bool ioFailure)
        {
            Action<LobbyResult> done = _onJoined;
            _onJoined = null;
            if (ioFailure || result.m_EChatRoomEnterResponse != EnterSuccess)
            {
                done?.Invoke(new LobbyResult(ioFailure ? LobbyError.Failed : MapEnterError(result.m_EChatRoomEnterResponse), null));
                return;
            }
            var id = new CSteamID(result.m_ulSteamIDLobby);
            Current = new LobbyId(result.m_ulSteamIDLobby);
            done?.Invoke(new LobbyResult(LobbyError.None, ReadInfo(id)));
        }

        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t request)
        {
            // 초대 수락 또는 친구 목록 「게임 참가」. 화면이 이 이벤트를 받아 접속을 시작한다.
            JoinRequested?.Invoke(new LobbyId(request.m_steamIDLobby.m_SteamID));
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t update)
        {
            if (Current.IsValid && update.m_ulSteamIDLobby == Current.Value)
            {
                MembersChanged?.Invoke(SteamMatchmaking.GetNumLobbyMembers(new CSteamID(Current.Value)));
            }
        }

        // ---------- 도우미 ----------

        private bool IsOwner() =>
            SteamMatchmaking.GetLobbyOwner(new CSteamID(Current.Value)) == SteamUser.GetSteamID();

        private static LobbyInfo ReadInfo(CSteamID lobby)
        {
            int dataCount = SteamMatchmaking.GetLobbyDataCount(lobby);
            var meta = new Dictionary<string, string>(dataCount);
            for (int i = 0; i < dataCount; i++)
            {
                if (SteamMatchmaking.GetLobbyDataByIndex(lobby, i, out string key, MetaBufferSize, out string value, MetaBufferSize))
                {
                    meta[key] = value;
                }
            }
            return new LobbyInfo(
                new LobbyId(lobby.m_SteamID),
                new PlayerId(SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID),
                SteamMatchmaking.GetNumLobbyMembers(lobby),
                SteamMatchmaking.GetLobbyMemberLimit(lobby),
                meta);
        }

        /// <summary>참가 조건을 Steam 로비 유형으로 바꾼다. (3.7.1)</summary>
        public static ELobbyType ToSteamType(SessionAccess access)
        {
            switch (access)
            {
                case SessionAccess.InviteOnly: return ELobbyType.k_ELobbyTypePrivate;
                case SessionAccess.Public: return ELobbyType.k_ELobbyTypePublic;
                default: return ELobbyType.k_ELobbyTypeFriendsOnly;
            }
        }

        private static LobbyError MapError(EResult result)
        {
            switch (result)
            {
                case EResult.k_EResultTimeout: return LobbyError.Timeout;
                case EResult.k_EResultAccessDenied: return LobbyError.NoAccess;
                default: return LobbyError.Failed;
            }
        }

        private static LobbyError MapEnterError(uint response)
        {
            switch ((EChatRoomEnterResponse)response)
            {
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseFull: return LobbyError.Full;
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist: return LobbyError.DoesNotExist;
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed:
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseBanned:
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseLimited:
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseCommunityBan:
                    return LobbyError.NoAccess;
                default: return LobbyError.Failed;
            }
        }
    }
}

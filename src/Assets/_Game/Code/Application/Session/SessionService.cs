// =============================================================
// 파일: SessionService.cs
// 역할: 호스트 쪽 세션 유스케이스. 세션 열기(로비 만들기), 참가 요청 판정(SessionRules 호출), 인원 변동에 따른 로비 메타데이터 갱신,
//       전송이 알려 준 원격 ID로 플레이어 ID 정하기(PlayerIdentityPolicy).
// 레이어: application (Game.Application/Session, 순수 C#)
// 관련: FR-001~004, FR-011(퇴장), NFR-009, NFR-038, NFR-039, DES-054, DES-031 / SDD 3.9.1, 3.9.2, 3.7.1, D-14
//
// M1 범위: 참가 조건은 「친구 전용」 고정. 비밀번호·재접속 예약·강퇴·차단 편집은 M2.
// =============================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Application.Ports;
using Game.Application.Protocol;
using Game.Application.Server;
using Game.Domain.Common;
using Game.Domain.Session;

namespace Game.Application.Session
{
    /// <summary>플레이어 ID 정하기 규칙. [D-14] 자기 SteamID를 메시지로 적어 보내게 하지 않는다.</summary>
    public static class PlayerIdentityPolicy
    {
        /// <summary>
        /// 연결의 플레이어 ID를 정한다.
        /// Steam 전송이면 전송이 알려 준 원격 ID(SteamID 문자열)를 쓰고, 개발 전송(Tugboat)이면 요청의 개발용 ID를 쓴다.
        /// </summary>
        /// <param name="isSteamTransport">Steam 전송인지</param>
        /// <param name="remoteId">전송이 알려 준 원격 ID 문자열</param>
        /// <param name="devPlayerId">요청에 적힌 개발용 ID</param>
        /// <param name="player">결정된 ID</param>
        /// <returns>유효한 ID이면 true</returns>
        public static bool TryResolve(bool isSteamTransport, string remoteId, ulong devPlayerId, out PlayerId player)
        {
            player = PlayerId.None;
            if (isSteamTransport)
            {
                if (!ulong.TryParse(remoteId, NumberStyles.None, CultureInfo.InvariantCulture, out ulong steamId) || steamId == 0UL)
                {
                    return false;
                }
                player = new PlayerId(steamId);
                return true;
            }
            if (devPlayerId == 0UL)
            {
                return false;
            }
            player = new PlayerId(devPlayerId);
            return true;
        }
    }

    /// <summary>호스트가 연 월드 정보. 참가자에게 JoinAccepted로 알려 주는 값과 로비 메타데이터의 재료.</summary>
    public sealed class HostWorldInfo
    {
        public string WorldName { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public string SeedText { get; set; } = string.Empty;
        public ulong Seed { get; set; }
        public ushort GeneratorVersion { get; set; }
        /// <summary>호스트가 계산한 월드 요약값. 0이면 아직 월드가 없다(M1-07). M1-10부터 실제 값.</summary>
        public ulong ExpectedWorldHash { get; set; }
        public SessionAccess Access { get; set; } = SessionAccess.FriendsOnly;
        /// <summary>호스트의 게임 버전 전체 문자열. 로비 kh_build 값이 된다. [NFR-058]</summary>
        public string GameVersion { get; set; } = string.Empty;
        public ulong ContentHash { get; set; }
    }

    /// <summary>호스트 세션 서비스.</summary>
    public sealed class SessionService
    {
        private readonly SessionRules _rules;
        private readonly ServerWorld _world;
        private readonly ILobbyService _lobby;
        private readonly IBanListStore _bans;
        private readonly ILog _log;
        private readonly HostWorldInfo _info;
        private readonly bool _isSteamTransport;
        private int _pendingAccepted;

        /// <param name="rules">참가 판정 규칙</param>
        /// <param name="world">서버 월드(인원 수·자리)</param>
        /// <param name="lobby">Steam 로비(개발 전송이면 null)</param>
        /// <param name="bans">차단 목록(없으면 빈 목록)</param>
        /// <param name="info">월드 정보</param>
        /// <param name="isSteamTransport">Steam 전송인지(로비 멤버 확인을 요구할지)</param>
        public SessionService(SessionRules rules, ServerWorld world, ILobbyService lobby, IBanListStore bans, HostWorldInfo info, bool isSteamTransport, ILog log)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _lobby = lobby;
            _bans = bans ?? EmptyBanListStore.Instance;
            _info = info ?? throw new ArgumentNullException(nameof(info));
            _isSteamTransport = isSteamTransport;
            _log = log ?? NullLog.Instance;
            _world.PlayerAdded += (_, __) => PublishCount();
            _world.PlayerRemoved += _ => PublishCount();
        }

        /// <summary>호스트가 연 월드 정보.</summary>
        public HostWorldInfo Info => _info;

        /// <summary>Steam 전송인지.</summary>
        public bool IsSteamTransport => _isSteamTransport;

        /// <summary>현재 접속 인원 + 참가 절차 중인 인원.</summary>
        public int ConnectedCount => _world.State.PlayerCount;

        /// <summary>로비 만들기가 끝나 세션이 열렸는지(개발 전송은 로비 없이 바로 true).</summary>
        public bool IsOpen { get; private set; }

        /// <summary>세션 열림 결과(로비 오류 없음이면 None).</summary>
        public event Action<LobbyError> Opened;

        /// <summary>
        /// 세션을 연다. Steam 전송이면 로비를 만들고(참가 조건 → 로비 유형), 개발 전송이면 로비 없이 바로 연다.
        /// </summary>
        public void Open()
        {
            if (_lobby == null || !_isSteamTransport)
            {
                IsOpen = true;
                Opened?.Invoke(LobbyError.None);
                return;
            }
            if (!_lobby.IsAvailable)
            {
                Opened?.Invoke(LobbyError.NotAvailable);
                return;
            }

            // [FR-001] M1은 참가 조건 「친구 전용」 고정. 비밀번호는 없다(M2).
            Dictionary<string, string> meta = LobbyMetaBuilder.Build(_info.WorldName, _info.HostName, ConnectedCount, 0,
                _info.GameVersion, _info.Access, false);
            _lobby.Create(_info.Access, _rules.MaxPlayers, meta, result =>
            {
                IsOpen = result.Success;
                if (!result.Success)
                {
                    _log.Error("session", "로비 생성 실패: " + result.Error);
                }
                Opened?.Invoke(result.Error);
            });
        }

        /// <summary>세션을 닫는다(로비 나가기).</summary>
        public void Close()
        {
            IsOpen = false;
            _lobby?.Leave();
        }

        /// <summary>
        /// 참가 요청을 판정한다. 허용이면 None.
        /// </summary>
        /// <param name="player">전송이 알려 준 원격 ID로 정한 플레이어</param>
        /// <param name="request">참가 요청</param>
        public JoinRejectReason Evaluate(PlayerId player, in JoinRequest request)
        {
            bool supported = Array.IndexOf(request.SupportedGeneratorVersions, _info.GeneratorVersion) >= 0;
            var facts = new JoinFacts(
                player,
                _bans.Contains(player),
                _lobby != null && _lobby.IsMember(player),
                _isSteamTransport,
                ConnectedCount,
                0,
                _info.Access,
                request.Path,
                request.ProtocolVersion,
                request.GameVersion,
                request.ContentHash,
                supported);
            return _rules.Evaluate(facts);
        }

        /// <summary>참가 허용 메시지를 만든다.</summary>
        public JoinAccepted BuildAccepted(PlayerId player, int slot, uint serverTick) =>
            new JoinAccepted(_info.SeedText, _info.Seed, _info.GeneratorVersion, _info.ExpectedWorldHash, serverTick,
                (byte)_rules.MaxPlayers, _info.Access, player, (byte)slot);

        /// <summary>참가 절차 중인 인원 수 조정(자리는 AddPlayer에서 이미 잡혀 있어 현재는 기록용).</summary>
        public void NotePending(int delta) => _pendingAccepted = Math.Max(0, _pendingAccepted + delta);

        private void PublishCount()
        {
            // [3.7.1] 인원이 바뀔 때마다 로비 메타데이터 kh_count를 갱신한다.
            _lobby?.SetMeta(LobbyMetaKeys.Count, ConnectedCount.ToString(CultureInfo.InvariantCulture));
        }
    }
}

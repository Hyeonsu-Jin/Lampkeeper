// =============================================================
// 파일: SteamLobbySmoke.cs
// 역할: 실제 Steam(App ID 480)에서 로비 기능이 동작하는지 계정 하나로 확인하는 개발용 점검(-steamlobbytest, 개발 빌드 전용).
//       로비 생성(친구 전용) → 메타데이터 읽기 → 멤버 확인 → 검색(친구 전용은 목록에 안 나옴) → 공개 로비 생성·검색(kh_game 필터) →
//       참가 호출 → 나가기. 결과는 로그와 종료 코드(0 = 통과)로 남긴다. 초대 창(GUI)은 열지 않는다.
// 레이어: infrastructure (Game.Bootstrap/DevTools)
// 관련: FR-001, FR-002(초대는 수동 확인), FR-003, FR-145, NFR-009, DES-102, DES-103 / SDD 3.7.1, M1-07
// =============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Application.Ports;
using Game.Bootstrap.Runtime;
using Game.Domain.Session;
using UnityEngine;

namespace Game.Bootstrap.DevTools
{
    /// <summary>Steam 로비 점검 시나리오. 콜백이 올 때마다 다음 단계로 넘어간다.</summary>
    public sealed class SteamLobbySmoke
    {
        private const string Tag = "[SteamSmoke] ";
        private const double StepTimeoutSeconds = 20d;
        private const string TestWorldName = "smoke-world";
        private const string TestHostName = "smoke-host";

        private readonly ILobbyService _lobby;
        private readonly IPlatformIdentity _identity;
        private readonly Action<int> _onDone;
        private readonly List<string> _failures = new List<string>();
        private double _stepStarted;
        private bool _finished;
        private string _step = "init";
        private Action _delayed;
        private double _delayedAt;

        /// <summary>Steam이 새 로비를 검색 색인에 올리는 데 걸리는 시간을 기다리며 다시 검색하는 횟수와 간격.</summary>
        private const int SearchAttempts = 6;
        private const double SearchRetrySeconds = 3d;

        public SteamLobbySmoke(ILobbyService lobby, IPlatformIdentity identity, Action<int> onDone)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _identity = identity ?? throw new ArgumentNullException(nameof(identity));
            _onDone = onDone ?? throw new ArgumentNullException(nameof(onDone));
        }

        /// <summary>시작한다.</summary>
        public void Start()
        {
            Debug.Log(Tag + "시작. 플레이어 " + _identity.LocalPlayerId + " (" + _identity.LocalDisplayName + "), 로비 사용 가능=" + _lobby.IsAvailable);
            CreateFriendsLobby();
        }

        /// <summary>매 프레임 불러 단계 시간 초과를 본다.</summary>
        public void Tick()
        {
            if (_delayed != null && Time.realtimeSinceStartupAsDouble >= _delayedAt)
            {
                Action next = _delayed;
                _delayed = null;
                next();
            }
            if (!_finished && Time.realtimeSinceStartupAsDouble - _stepStarted > StepTimeoutSeconds)
            {
                Fail("단계 시간 초과: " + _step);
                Finish();
            }
        }

        private void Begin(string step)
        {
            _step = step;
            _stepStarted = Time.realtimeSinceStartupAsDouble;
            Debug.Log(Tag + "단계: " + step);
        }

        private void CreateFriendsLobby()
        {
            Begin("친구 전용 로비 생성");
            Dictionary<string, string> meta = LobbyMetaBuilder.Build(TestWorldName, TestHostName, 1, 0, BuildInfo.Version.Full, SessionAccess.FriendsOnly, false);
            _lobby.Create(SessionAccess.FriendsOnly, 8, meta, result =>
            {
                if (!result.Success)
                {
                    Fail("친구 전용 로비 생성 실패: " + result.Error);
                    Finish();
                    return;
                }
                LobbyInfo info = result.Lobby;
                Debug.Log(Tag + "생성됨 id=" + info.Id + " 주인=" + info.Owner + " 멤버=" + info.MemberCount + "/" + info.MaxMembers + " 메타 " + info.Meta.Count + "개");
                Check(info.GetMeta(LobbyMetaKeys.Game) == LobbyMetaKeys.GameIdValue, "kh_game 메타 읽기");
                Check(info.GetMeta(LobbyMetaKeys.Access) == "friends", "kh_access = friends");
                Check(info.GetMeta(LobbyMetaKeys.World) == TestWorldName, "kh_world 메타");
                Check(info.MaxMembers == 8, "최대 인원 8");
                Check(info.Owner == _identity.LocalPlayerId, "로비 주인 = 내 SteamID");
                Check(_lobby.IsMember(_identity.LocalPlayerId), "로비 멤버 확인(IsMember)");

                _lobby.SetMeta(LobbyMetaKeys.Count, "3");
                SearchExpectingNone();
            });
        }

        private void SearchExpectingNone()
        {
            Begin("검색(친구 전용 로비는 목록에 나오지 않는 것이 Steam 정상 동작)");
            _lobby.RequestList((error, list) =>
            {
                Check(error == LobbyError.None, "검색 호출 성공");
                int mine = list.Count(l => l.GetMeta(LobbyMetaKeys.World) == TestWorldName);
                Debug.Log(Tag + "검색 결과 " + list.Count + "개 중 내 친구 전용 로비 " + mine + "개(0이 정상)");
                _lobby.Leave();
                CreatePublicLobby();
            });
        }

        private void CreatePublicLobby()
        {
            Begin("공개 로비 생성(검색 확인용)");
            Dictionary<string, string> meta = LobbyMetaBuilder.Build(TestWorldName, TestHostName, 1, 0, BuildInfo.Version.Full, SessionAccess.Public, false);
            _lobby.Create(SessionAccess.Public, 8, meta, result =>
            {
                if (!result.Success)
                {
                    Fail("공개 로비 생성 실패: " + result.Error);
                    Finish();
                    return;
                }
                LobbyId created = result.Lobby.Id;
                Debug.Log(Tag + "공개 로비 생성됨 id=" + created);
                SearchPublic(created, 1);
            });
        }

        private void SearchPublic(LobbyId created, int attempt)
        {
            Begin("공개 로비 검색(kh_game 필터) " + attempt + "/" + SearchAttempts);
            _lobby.RequestList((error, list) =>
            {
                Check(error == LobbyError.None, "공개 로비 검색 호출 성공");
                LobbyInfo found = list.FirstOrDefault(l => l.Id.Value == created.Value);
                if (found == null && attempt < SearchAttempts)
                {
                    // 방금 만든 로비는 Steam 검색 색인에 오르기까지 몇 초 걸릴 수 있어 간격을 두고 다시 검색한다.
                    Debug.Log(Tag + "아직 검색 결과에 없음(결과 " + list.Count + "개). 다시 검색합니다.");
                    _delayedAt = Time.realtimeSinceStartupAsDouble + SearchRetrySeconds;
                    _delayed = () => SearchPublic(created, attempt + 1);
                    return;
                }
                Check(found != null, "kh_game 필터 검색에서 내 공개 로비를 찾음(시도 " + attempt + "회, 결과 " + list.Count + "개)");
                if (found != null)
                {
                    Check(found.GetMeta(LobbyMetaKeys.Game) == LobbyMetaKeys.GameIdValue, "검색 결과의 kh_game");
                    Check(found.GetMeta(LobbyMetaKeys.Host) == TestHostName, "검색 결과의 kh_host");
                    Check(found.GetMeta(LobbyMetaKeys.Access) == "public", "검색 결과의 kh_access");
                    Check(found.Owner == _identity.LocalPlayerId, "검색 결과의 로비 주인");
                }
                foreach (LobbyInfo other in list.Where(l => l.GetMeta(LobbyMetaKeys.Game) != LobbyMetaKeys.GameIdValue))
                {
                    Fail("kh_game 필터를 통과한 다른 게임 로비가 있음 id=" + other.Id);
                }
                JoinOwn(created);
            });
        }

        private void JoinOwn(LobbyId id)
        {
            Begin("로비 참가 호출(내 로비)");
            _lobby.Join(id, result =>
            {
                Check(result.Success, "참가 호출 성공(" + result.Error + ")");
                _lobby.Leave();
                Finish();
            });
        }

        private void Check(bool condition, string what)
        {
            if (condition)
            {
                Debug.Log(Tag + "통과: " + what);
            }
            else
            {
                Fail(what);
            }
        }

        private void Fail(string what)
        {
            _failures.Add(what);
            Debug.LogError(Tag + "실패: " + what);
        }

        private void Finish()
        {
            if (_finished)
            {
                return;
            }
            _finished = true;
            Debug.Log(Tag + (_failures.Count == 0 ? "전체 통과" : "실패 " + _failures.Count + "건"));
            _onDone(_failures.Count == 0 ? 0 : 1);
        }
    }
}

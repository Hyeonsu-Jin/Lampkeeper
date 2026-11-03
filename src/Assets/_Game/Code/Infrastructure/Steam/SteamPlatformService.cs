// =============================================================
// 파일: SteamPlatformService.cs
// 역할: Steamworks.NET으로 Steam을 초기화하고, 로컬 플레이어(SteamID, 이름)와 콜백 처리를 제공한다.
// 레이어: infrastructure (Game.Infra.Steam). 포트 구현: IPlatformServices
// 관련: NFR-006(Steam 실행 필수), NFR-009(App ID), DES-102 / SDD 3.7.1
//
// App ID
//  - 개발: 프로젝트 루트(src/)의 steam_appid.txt(내용 480, Spacewar). 에디터는 이 파일을, 개발 빌드는 실행 파일 옆 복사본을 읽는다.
//  - 출시: 파일을 두지 않는다. 정식 App ID가 정해지면 SteamAppIds.Release에 넣고 RestartAppIfNecessary로 Steam 경유 실행을 강제한다.
// =============================================================
using System;
using Game.Application.Ports;
using Game.Domain.Common;
using Steamworks;

namespace Game.Infra.Steam
{
    /// <summary>Steam App ID 상수.</summary>
    public static class SteamAppIds
    {
        /// <summary>개발용 테스트 App ID(Spacewar). 여러 개발자가 공유하므로 다른 게임 로비가 섞인다(NFR-009: kh_game 필터로 거른다).</summary>
        public const uint Development = 480;

        /// <summary>출시용 App ID. 0은 "아직 발급 전". 발급되면 이 값만 바꾼다.</summary>
        public const uint Release = 0;
    }

    /// <summary>Steam에 연결된 로컬 플레이어 정보. 초기화에 성공한 뒤에만 만든다.</summary>
    public sealed class SteamIdentity : IPlatformIdentity
    {
        public PlayerId LocalPlayerId { get; }
        public string LocalDisplayName { get; }

        public SteamIdentity(PlayerId localPlayerId, string localDisplayName)
        {
            LocalPlayerId = localPlayerId;
            LocalDisplayName = localDisplayName ?? string.Empty;
        }

        public string ResolveName(PlayerId playerId)
        {
            // 로컬 플레이어는 이미 알고 있는 이름을 쓰고, 다른 사람은 Steam에 묻는다.
            if (playerId == LocalPlayerId)
            {
                return LocalDisplayName;
            }
            string name = SteamFriends.GetFriendPersonaName(new CSteamID(playerId.Value));
            return string.IsNullOrEmpty(name) ? playerId.ToString() : name;
        }
    }

    /// <summary>
    /// Steam 초기화·종료·콜백 처리. Initialize는 실패 뒤 다시 불러도 된다.
    /// </summary>
    public sealed class SteamPlatformService : IPlatformServices
    {
        private readonly bool _isDevelopmentBuild;

        public bool IsInitialized { get; private set; }
        public IPlatformIdentity Identity { get; private set; }

        /// <param name="isDevelopmentBuild">개발 빌드(또는 에디터)이면 true. 출시 빌드에서만 RestartAppIfNecessary를 쓴다</param>
        public SteamPlatformService(bool isDevelopmentBuild)
        {
            _isDevelopmentBuild = isDevelopmentBuild;
        }

        public PlatformInitResult Initialize()
        {
            if (IsInitialized)
            {
                return PlatformInitResult.Ok();
            }

            try
            {
                // 출시 빌드: Steam 밖에서 실행했다면 Steam으로 다시 실행하게 하고 이 프로세스는 끝낸다.
                if (!_isDevelopmentBuild && SteamAppIds.Release != 0 && SteamAPI.RestartAppIfNecessary(new AppId_t(SteamAppIds.Release)))
                {
                    return PlatformInitResult.Fail(PlatformInitFailure.RestartRequested, "Steam 경유 재실행 요청");
                }

                // [NFR-006] Steam 클라이언트가 떠 있지 않으면 바로 SCR-001로 보낸다.
                if (!SteamAPI.IsSteamRunning())
                {
                    return PlatformInitResult.Fail(PlatformInitFailure.SteamNotRunning, "SteamAPI.IsSteamRunning() == false");
                }

                ESteamAPIInitResult initResult = SteamAPI.InitEx(out string errorMessage);
                if (initResult != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    return PlatformInitResult.Fail(PlatformInitFailure.InitFailed, initResult + ": " + errorMessage);
                }
            }
            catch (DllNotFoundException ex)
            {
                // steam_api64.dll을 못 찾음. 예외를 삼키지 않고 이유를 결과에 담아 화면과 로그가 쓴다.
                return PlatformInitResult.Fail(PlatformInitFailure.LibraryUnavailable, "DllNotFoundException: " + ex.Message);
            }
            catch (EntryPointNotFoundException ex)
            {
                return PlatformInitResult.Fail(PlatformInitFailure.LibraryUnavailable, "EntryPointNotFoundException: " + ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                // Steamworks.NET이 지원하지 않는 플랫폼 등
                return PlatformInitResult.Fail(PlatformInitFailure.LibraryUnavailable, "InvalidOperationException: " + ex.Message);
            }

            var steamId = new PlayerId(SteamUser.GetSteamID().m_SteamID);
            Identity = new SteamIdentity(steamId, SteamFriends.GetPersonaName());
            IsInitialized = true;
            return PlatformInitResult.Ok();
        }

        public void Tick()
        {
            if (IsInitialized)
            {
                SteamAPI.RunCallbacks();
            }
        }

        public void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }
            SteamAPI.Shutdown();
            IsInitialized = false;
            Identity = null;
        }
    }
}

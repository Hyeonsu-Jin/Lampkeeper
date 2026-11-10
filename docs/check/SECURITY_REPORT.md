# 보안 점검 보고서 (SECURITY_REPORT)

- 대상: 사이클 1 (M1 기술 검증), 브랜치 `cycle/1-mvp` (a5db2df)
- 작성: security-auditor / 날짜: 2026-11-10
- 참조: SRS NFR-003·NFR-030·NFR-040·NFR-041·NFR-042·NFR-045·NFR-049·NFR-050, SDD 3.7.4·3.7.5·3.7.12·3.11.12·3.11.13·D-14·R-07
- 방법: 정적 코드 분석, 출시 빌드 산출물(`src/Builds/Release/`) 문자열·설정 표본 검사, 저장소(`git ls-files`)·git 이력 검색, 서드파티 소스(FishNet·FishySteamworks) 동작 확인. 실제 공격 코드는 실행하지 않았습니다.

## 변경 이력
| 버전 | 날짜 | 내용 |
|---|---|---|
| 1.0 | 2026-11-10 | 사이클 1 Check 첫 작성 |

---

## 1. 점검 범위와 이유

온라인 기능은 **Steam만**입니다(Steam 로비 + FishySteamworks P2P 릴레이, 개발용 Tugboat). 자체 서버·결제·광고·분석이 없고, **호스트가 서버 역할**을 하는 호스트 권위 구조라서 "서버 통신" 범주로 보고 아래 항목을 점검했습니다.

| 항목 | 점검 | 이유 |
|---|---|---|
| 1 세이브 데이터 변조 | 제외 (다음 사이클) | 저장·Steam Cloud는 M3 이후라 구현되지 않음 |
| 2 빌드에 포함된 비밀 값 | 점검 | 저장소·빌드에 키, 개인 경로, SteamID가 들어갔는지 |
| 3 메모리·클라이언트 변조 | 점검 | 호스트 권위가 막는 범위와, 남은 클라이언트 신뢰(이동) |
| 4 결제 검증 | 해당 없음 | 결제 없음 |
| 5 클라이언트를 믿지 않기 | **중점** | 호스트가 서버 역할. 네트워크 입력 검증, 참가자 신원, 빈도 제한, DoS |
| 6 개인정보·외부 SDK | 점검 | Steam SDK, 로그의 SteamID·경로, 측정 원본 |
| 7 배포 빌드 설정 | **중점** | 개발 옵션·Tugboat·디버그 HUD가 출시 빌드에서 꺼지는지 |
| OWASP Top 10 | 관련 항목만 | 웹 서버가 없어 XSS/CSRF·SQL 인젝션은 해당 없음. 입력 검증(A03), 인증(A07), 접근 제어(A01), 보안 설정(A05), 취약한 의존성(A06), 로깅(A09)을 호스트 코드 기준으로 봄 |

## 2. 결과 요약

| 심각도 | 건수 | ID |
|---|---|---|
| 🔴 치명 | 0 | — |
| 🟠 높음 | 3 | SEC-001, SEC-002, SEC-003 |
| 🟡 중간 | 2 | SEC-004, SEC-005 |
| 🟢 낮음 | 5 | SEC-006 ~ SEC-010 |

**출시 빌드의 개발 기능 제외: 적합(조건부).** `BuildFlags.IsDevelopment`가 컴파일 시점 상수라서 개발 옵션, Tugboat, `-devPlayerId`, 봇, 디버그 HUD(F1/F3)가 모두 꺼지는 것을 확인했습니다(5장). 다만 개발용 클래스와 옵션 문자열이 실행되지 않는 코드로 출시 바이너리에 남아 있고, `-upscale` 옵션 하나는 출시 빌드에서도 동작합니다(SEC-008).

---

## 3. 발견 사항

### SEC-001 🟠 이동 검사를 피해 빠르게 이동하거나 날 수 있음 (NFR-030 수용 기준 미달)
- 위치: `src/Assets/_Game/Code/Application/Server/ServerWorld.cs:160-164`(첫 자세 무조건 수락), `:168`(클라이언트 시각으로 dt 계산), `:182-183`(자세마다 여유 거리 추가), `Adapters/Net/Avatar/PlayerAvatar.cs:117`(자세 RPC 빈도 제한 없음)
- 문제
  1. **자세마다 더하는 여유 거리(`MoveSlackMeters` 0.5 m)**: 정상 송신 빈도 30 Hz에서 자세 하나에 허용되는 거리는 `7×1.25/30 + 0.5 ≈ 0.79 m`, 초당 약 **23.7 m/s(달리기 7 m/s의 3.4배)**입니다. 시각을 정직하게 보내도 **달리기 2배(14 m/s)로 움직인 자세를 받아들입니다.** NFR-030 수용 기준("2배로 이동하면 되돌린다")을 채우지 못합니다.
  2. **dt를 클라이언트가 보낸 시각(`TimeMs`)으로 계산**: 자세마다 `TimeMs`를 500 ms씩 올리면 dt가 상한 `MaxPoseDeltaSeconds`(0.5 s)로 잡혀 자세 하나에 4.875 m까지 허용됩니다. 30 Hz 기준 **약 146 m/s**입니다.
  3. **자세 RPC에 빈도 제한이 없음**: 1 ms 간격 시각으로 자세를 많이 보내면 자세마다 0.5 m 여유가 더해져 속도 상한이 사실상 없습니다.
  4. **수직 검사는 `40 m/s × dt + 0.5 m`뿐**: 중력·지면 검사가 없어 공중에 떠 있거나 날 수 있습니다.
  5. **첫 자세는 월드 안이면 어디든 수락**: 참가 직후 월드 어느 곳으로든 순간 이동할 수 있습니다.
- 공격 시나리오: 조작한 참가자 클라이언트(메모리 편집·DLL 주입)가 `TimeMs`를 부풀리고 송신 빈도를 올립니다. 그러면 고속 이동, 비행, 참가 직후 순간 이동이 가능합니다. 호스트의 거리 검증(FR-007, 타격 2.5 m)은 이렇게 옮긴 서버 위치를 기준으로 하므로, 월드 어디에서든 채집할 수 있게 됩니다(SEC-006과 연계).
- 영향 범위: PvP가 없는 친구 협동이라 다른 플레이어에게 직접 피해는 없습니다. 다만 사용자가 받아들인 위험("속도 한도 **안**의 조작", R-07)보다 범위가 훨씬 넓습니다.
- 수정 방법
  - dt는 **호스트가 받은 시각**(`IClock.RealtimeSeconds`)으로 계산합니다. 클라이언트 시각은 순서 판별(Stale)에만 쓰거나, `min(클라이언트 dt, 호스트 수신 간격 + 지터 여유)`로 제한합니다.
  - 여유 거리를 자세마다 더하지 말고 **거리 예산(토큰 버킷)**으로 바꿉니다. 초당 `speed × tolerance`만큼 채우고, 상한은 `slack`으로 둡니다.
  - 자세 RPC를 플레이어별로 `AvatarSendHz × 1.5` 정도로 빈도 제한합니다(SEC-003과 함께).
  - 첫 자세는 `SpawnPositionOf(player)`에서 `slack` 이내만 받아들입니다.
  - 수직은 상승 속도를 따로 제한하는 정도면 됩니다(지면 높이 `SurfaceHeightMeters` + 점프 높이 상한 등). 비용이 작은 범위에서 적용합니다.
  - 시험: NFR-030 수용 기준을 그대로 EditMode 시험으로 추가합니다. 2배 속도의 정직한 시각, 부풀린 시각, 1 ms 간격 폭주를 넣고 Rejected가 나오는지 확인합니다.
- 관련: NFR-030, FR-007, FR-024, SDD 3.11.12, R-07

### SEC-002 🟠 로비 멤버가 아닌 Steam 사용자도 참가 판정 전에 접속해 게임 데이터를 받음
- 위치
  - `src/Assets/ThirdParty/FishySteamworks/Core/ServerSocket.cs:283-299`: 들어오는 P2P 연결을 인원 상한 안에서 모두 수락합니다.
  - Boot 씬 NetworkManager: FishNet `Authenticator`가 없어 연결 즉시 인증 완료로 처리됩니다(`ServerManager.cs:514`).
  - `src/Assets/_Game/Prefabs/PlayerAvatar.prefab`: `NetworkObserver`·관찰 조건이 없습니다. FishNet은 조건이 없으면 인증된 모든 연결을 관찰자로 넣습니다(`ThirdParty/FishNet/Runtime/Observing/NetworkObserver.cs:358`).
  - `Adapters/Net/Avatar/PlayerAvatar.cs`: `SyncVar<ulong> _ownerPlayerId`(SteamID), `ObserversPose`.
  - `Infrastructure/Transport/TransportSelector.cs`: `ApplyConnectionLimit`는 최대 인원 + 2 = 10입니다.
- 문제: D-14의 로비 멤버 확인은 우리 `JoinRequest` 단계에서만 합니다. 그 아래 FishNet 계층은 연결되자마자 해당 연결을 인증된 관찰자로 취급합니다. 그래서 JoinRequest를 보내지 않은 연결도 `JoinTimeoutSeconds` 동안 다음을 받을 수 있습니다(코드 분석 결과, **실측 확인 필요**).
  - 모든 PlayerAvatar 스폰과 **참가자 전원의 SteamID**(SyncVar)
  - 실시간 위치(ObserversRpc)
  - FishNet `ShareIds` 기본값(true)에 따른 클라이언트 ID 목록
- 공격 시나리오
  - (a) 호스트의 SteamID64(공개 프로필에서 확인 가능)를 아는 친구가 아닌 사용자가, 조작한 클라이언트로 호스트에 P2P 연결합니다. 그러면 함께 플레이하는 사람들의 SteamID와 위치를 엿볼 수 있습니다.
  - (b) 계정 여러 개로 연결을 열어 두고 시간 초과 직전에 다시 연결하면 연결 자리 10개를 차지해 친구들이 참가하지 못합니다.
  - (c) 참가 전 연결에서도 PacketBroadcast는 받으므로 SEC-003의 로그 폭주를 일으킬 수 있습니다.
- 수정 방법
  - FishNet `Authenticator`를 구현해 `JoinHandshake`와 연결합니다. JoinRequest 판정(로비 멤버·버전)을 통과한 연결만 `OnAuthenticationResult(conn, true)`로 인증하고, 그 전에는 FishNet 관찰·RPC가 열리지 않게 합니다. PacketBroadcast는 지금처럼 `requireAuthentication=false`로 둡니다.
  - 또는 `JoinHandshake.Phase == InGame`인 연결만 통과시키는 사용자 정의 `ObserverCondition`을 기본 조건으로 등록합니다.
  - 연결이 들어오는 시점(전송 계층)에서 원격 SteamID가 로비 멤버가 아니면 바로 끊습니다(FishySteamworks 연결 콜백 이후 `OnConnected`에서 `IsMember` 확인 후 `Disconnect`).
  - ServerManager `ShareIds`를 끕니다(필요하지 않다면).
  - 시험: 개발 빌드에서 JoinRequest를 보내지 않는 연결이 PlayerAvatar 스폰을 받지 않는지 PlayMode로 확인합니다.
- 관련: D-14, NFR-009, NFR-030, NFR-042(SteamID 노출), OWASP A01·A07

### SEC-003 🟠 빈도 제한 없는 자세 RPC·잘못된 패킷이 로그 폭주와 호스트 부하를 일으킴 (DoS)
- 위치
  - `Adapters/Net/Avatar/PlayerAvatar.cs:117-141`: 자세 RPC를 받을 때마다 디코드·검사합니다. 거부되면 `Warn` 로그와 신뢰 채널 `TargetCorrect`, 수락되면 다른 참가자 7명에게 중계합니다.
  - `Adapters/Net/Transport/FishNetPacketTransport.cs:168-175`: 디코드 실패 패킷마다 `Warn` 로그를 남깁니다. 실패 횟수를 세기만 하고 연결은 끊지 않습니다.
  - `Application/Server/ServerWorld.cs:567`: `move_rejected` 로그를 거부할 때마다 남깁니다.
  - `Bootstrap/Runtime/RuntimeSupport.cs:73-79`(`UnityLog` → `Debug.LogWarning`), `ProjectSettings/ProjectSettings.asset:58`(`m_StackTraceTypes` = ScriptOnly, 로그마다 스택 추적 수집).
  - `Infrastructure/Platform/Logging/FileLogSink.cs`: 파일 크기 상한과 보관 개수 정리가 없습니다.
- 문제: 패킷 1개가 `Debug.LogWarning` 1회(스택 추적 수집 포함), 파일 쓰기 1회, Player.log 쓰기 1회로 이어집니다. 자세 RPC와 잘못된 PacketBroadcast에는 연결 단위 빈도 제한도, 끊기 기준도 없습니다.
- 공격 시나리오: 조작한 참가자(또는 SEC-002의 비멤버 연결)가 초당 수천 개의 잘못된 자세나 패킷을 보냅니다. 호스트는 로그를 처리하느라 프레임이 떨어지고, 로그 파일이 GB 단위로 커지며, 다른 참가자에게 중계 트래픽이 늘어납니다. NFR-040의 "멈추지 않음"은 지키지만 실제로는 서비스 거부 상태가 됩니다.
- 수정 방법
  - 연결별로 자세 RPC에 토큰 버킷을 둡니다(예: `AvatarSendHz × 1.5`/s, 넘으면 버림).
  - 디코드 실패·거부가 일정 횟수(예: 10초에 50회)를 넘으면 그 연결을 끊습니다. `_decodeFailures` 집계를 실제로 활용합니다.
  - 같은 종류의 로그는 연결별로 묶어서 남깁니다(예: "5초간 N건 생략").
  - 대량으로 반복되는 경고는 스택 추적 없이(`Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None)`) 남깁니다.
  - FileLogSink에 파일 크기 상한을 두고, NFR-045의 `로그 보관 개수`에 맞춰 오래된 로그를 지웁니다(현재 미구현).
- 관련: NFR-040, NFR-041, NFR-045, OWASP A09(로깅) / DoS

### SEC-004 🟡 공유 명령 대기열이 빈도 제한보다 먼저 차서 다른 참가자의 명령이 버려짐
- 위치: `Adapters/Net/Gateway/ServerCommandGateway.cs:133-144`, `Application/Server/ServerWorld.cs:471-483`(전체 대기열 256), `Application/Commands/CommandPipeline.cs:214`(빈도 제한은 틱에서 Dispatch할 때만 적용)
- 문제: 플레이어별 빈도 제한(RequestGuard)이 **대기열에 넣은 뒤** 틱 처리 단계에서 적용됩니다. 대기열은 모든 플레이어가 함께 쓰는 256칸입니다. 디코드에 실패한 명령에는 빈도 제한 없이 매번 `CommandResultMessage`로 회신합니다.
- 공격 시나리오: 조작한 참가자 한 명이 틱마다 수백 개의 명령을 보내 대기열을 계속 가득 채웁니다. 그러면 다른 참가자의 타격 명령은 `RateLimited`로 버려집니다(협동 방해). 잘못된 명령을 보내면 호스트 회신 트래픽도 1:1로 늘어납니다.
- 수정 방법: 게이트웨이에서 `Enqueue` 전에 플레이어별 빈도 검사를 먼저 합니다. 또는 플레이어별 대기열 칸 수(예: 256 ÷ 최대 인원)를 둡니다. 디코드 실패 회신에도 같은 버킷을 적용하고, 넘으면 회신하지 않습니다.
- 관련: NFR-041, SDD 3.11.13

### SEC-005 🟡 Unity Player.log에 SteamID·사용자 경로가 가려지지 않고 남음
- 위치: `ProjectSettings/ProjectSettings.asset:97`(`usePlayerLog: 1`), `Bootstrap/Runtime/RuntimeSupport.cs:77-79`, `Bootstrap/GameBootstrap.cs:315·448`(SteamID + Steam 표시 이름), `Bootstrap/DevTools/DevDiagnostics.cs:44`(로그 파일 전체 경로), `Adapters/Net/Join/JoinHandshake.cs`(`허가 player=` 등 참가자 SteamID), FishySteamworks `ServerSocket.cs:287`(`Incoming connection {SteamID}`)
- 문제: `LogSanitizer`는 우리 `FileLogSink`(`Logs/game_*.log`)에만 적용됩니다. Unity가 따로 쓰는 `%USERPROFILE%\AppData\LocalLow\Lampkeeper\KkeojinHaneul\Player.log`에는 원문이 그대로 남습니다.
  - 이 PC의 Player.log를 확인한 결과 가려지지 않은 SteamID 형식 값 10건과 사용자 폴더 경로 1건이 있었습니다(값은 이 보고서에 옮기지 않음).
  - 같은 실행의 `game_*.log`에서는 0건이었습니다(LogSanitizer 정상 동작).
  - 호스트의 Player.log에는 **참가자 전원의 SteamID**가 남습니다.
- 공격 시나리오: 사용자가 오류를 신고하려고 Player.log를 포럼이나 디스코드에 올리면 본인과 함께 플레이한 친구들의 SteamID, Windows 사용자 이름이 공개됩니다.
- 수정 방법
  - 출시 빌드에서 `usePlayerLog`를 끕니다(우리 FileLogSink가 이미 같은 역할을 함).
  - 또는 `Debug.Log` 호출 전에 메시지를 Sanitize합니다(`UnityLog`에서 `LogSanitizer.Sanitize` 적용). 그래도 서드파티 로그는 가려지지 않으므로 앞의 방법을 권장합니다.
  - FR-158(오류 보고) 안내 문구에는 `Logs/` 폴더만 첨부하도록 적습니다.
- 관련: NFR-042, NFR-045, FR-158, DES-107

### SEC-006 🟢 타격 거리 검증이 수평 거리만 봄 / 재접속하면 빈도 버킷이 초기화됨
- 위치: `Application/Gathering/HitResourceCommand.cs:120`, `Application/Commands/CommandPipeline.cs:161`(`RemovePlayer` 시 버킷 삭제)
- 문제: 높이 차이를 검사하지 않아 SEC-001의 비행과 함께 쓰면 자원 바로 위 공중에서도 타격할 수 있습니다. 나갔다 다시 들어오면 타격 버킷이 가득 찬 상태(초당 4회 버스트)로 다시 시작합니다. 재접속 비용이 커서 영향은 작습니다.
- 수정 방법: 3차원 거리나 높이 차 상한(예: 3 m)을 추가합니다. 빈도 버킷은 SteamID 기준으로 나간 뒤 몇 초간 유지합니다.
- 관련: FR-007, NFR-041

### SEC-007 🟢 참가 판정의 일부를 클라이언트 신고 값과 Steam 로비 유형에 맡김
- 위치: `Domain/Session/SessionRules.cs:131-181`, `Application/Session/SessionService.cs:159-176`, `Infrastructure/Steam/SteamLobbyService.cs:260-265`
- 문제
  - (a) 게임 버전, ContentHash, 생성기 버전, `JoinPath`, `ComputedWorldHash`는 클라이언트가 스스로 보고하는 값입니다. **호환성 확인일 뿐 변조 방지 수단이 아닙니다.** 조작한 클라이언트는 기대값을 그대로 보내 통과할 수 있습니다(값은 로비 메타데이터와 빌드에서 알 수 있음).
  - (b) "친구 전용"은 Steam 로비 유형(FriendsOnly)에 의존하고, 호스트가 친구 관계를 다시 확인하지 않습니다. 로비 ID를 알면 친구가 아닌 사람도 `JoinLobby`할 수 있는지는 Steam 동작을 **확인해야 합니다**.
- 수정 방법
  - (a) SDD 3.7.12에 "호환성 확인이며 변조 방지 아님"을 명시합니다. 변조 방지는 호스트 권위 검증에 맡깁니다.
  - (b) FriendsOnly 세션에서는 `SteamFriends.HasFriend(remote, k_EFriendFlagImmediate)`나 초대 기록을 호스트가 추가로 확인합니다. M2의 강퇴·차단(FR-147·148)과 함께 다룹니다.
- 관련: D-14, FR-001, FR-003, NFR-009

### SEC-008 🟢 개발 전용 코드·옵션 문자열이 출시 바이너리에 포함됨 (실행 경로는 막혀 있음)
- 위치: `Bootstrap/DevTools/*`(BotClient, BenchRunner, SteamLobbySmoke, GenHashCommand), `Application/Boot/CheckPlatformReady.cs`(`DevPlayerIdentity`), `Application/Boot/LaunchOptionsParser.cs`, `Bootstrap/GameBootstrap.cs:141`(`perfOptions.Upscale`)
- 확인 결과: 출시 빌드 `global-metadata.dat`에 `devPlayerId`, `-transport`, `Tugboat`, `-bot`, `-resourcechanges`, `DevPlayerIdentity`, `BotClient` 등의 문자열과 형식이 있습니다. 모든 실행 지점이 `const` `BuildFlags.IsDevelopment`로 막혀 있어 실제로는 실행되지 않습니다. 다만 `-upscale` 옵션은 출시 빌드에서도 적용됩니다(그래픽 옵션이라 위험은 없음).
- 위험: 바이너리를 패치하려는 사람에게 개발 기능의 위치를 알려 줍니다. 패치해도 영향은 패치한 사람의 클라이언트에만 미칩니다. 호스트는 출시 빌드에서 Steam 전송과 원격 SteamID만 쓰므로 `devPlayerId` 위장은 통하지 않습니다.
- 수정 방법: `Bootstrap/DevTools/`를 별도 asmdef(`defineConstraints: UNITY_EDITOR || DEVELOPMENT_BUILD`)로 분리하거나 `#if`로 감쌉니다. `-upscale`을 출시 빌드에서 허용할지 SDD 3.7.4에 명시합니다.
- 관련: FR-006, NFR-006, SDD 3.7.4, DES-112

### SEC-009 🟢 빌드 폴더의 배포 금지 파일·개발 빌드 설정
- 위치: `src/Builds/Release/KkeojinHaneul_BackUpThisFolder_ButDontShipItWithYourGame/`, `src/Builds/Release/KkeojinHaneul_BurstDebugInformation_DoNotShip/`, `src/Builds/Development/KkeojinHaneul_Data/boot.config`(`player-connection-mode=Listen`, 로컬 LAN IP 2건)
- 문제
  - 출시 폴더를 그대로 압축해 배포하면 IL2CPP 변환 C++ 소스와 Burst 디버그 정보가 함께 나가 리버스 엔지니어링이 쉬워집니다.
  - 개발 빌드를 외부 시험자에게 주면 LAN IP가 노출되고 프로파일러 연결 대기가 열립니다.
- 수정 방법: 배포 스크립트(devops-engineer, Steam depot 설정)에서 `*_DoNotShip`, `*_ButDontShipItWithYourGame` 폴더를 제외합니다. 개발 빌드는 내부에서만 씁니다.
- 관련: 배포 빌드 설정(항목 7)

### SEC-010 🟢 `.claude/` 폴더 일부만 git 제외
- 위치: `.gitignore:129`(`/.claude/agents/`만 제외), 현재 `?? .claude/`(`scheduled_tasks.lock`)
- 문제: `git add -A`를 실수로 쓰면 `.claude/` 아래 로컬 파일이 커밋됩니다. 이후 생길 수 있는 `settings.local.json`에는 PC 전용 경로나 권한 설정이 들어 있을 수 있습니다.
- 수정 방법: `.gitignore`에 `/.claude/`를 통째로 추가합니다(총괄이 처리할 일).
- 관련: 형상 관리(CLAUDE.md "git으로 관리하지 않는 것")

---

## 4. 양호 확인 항목 (취약점 없음)

| 항목 | 근거 |
|---|---|
| 패킷 디코드 | `MessageCodec.TryDecode`: 종류별 크기 상한, 모르는 종류 거부, 남는 바이트 거부. `ByteReader`는 길이 접두어를 상한 확인 후 할당하고, 잘못된 UTF-8은 예외 없이 실패 처리(`ByteIo.cs:274-309`). `CreatureSnapshot` 개수×크기 사전 확인 |
| NaN·무한대 | 위치는 밀리미터 정수(i32)로 전송해 NaN이 들어올 수 없음. 그래도 `IsFinite`·`IsInsideWorld`로 다시 확인(`ServerWorld.cs:154`, `CommandPipeline.cs:271`) |
| 방향 검사 | 서버→클라이언트 종류를 클라이언트가 보내면 버림(`ServerCommandGateway.cs:100`). 명령은 InGame 연결만 받음(`:127`) |
| 예외 격리 | 게이트웨이·디스패처·틱 구독자 모두 try/catch로 감싸 호스트가 멈추지 않음(NFR-040) |
| 참가자 신원 | Steam 전송에서는 전송 계층의 원격 SteamID만 쓰고, 요청의 `DevPlayerId`는 무시(`SessionService.cs:36-43`). 출시 클라이언트는 `DevPlayerId=0`을 보냄(`SessionRuntime.cs:95`). 로비 멤버 확인은 `SteamMatchmaking` 멤버 목록으로 함. 같은 SteamID의 두 번째 연결은 거부 |
| 타격 명령 | 호스트 검증(대상 존재, 거리, 상태), 초당 4회 버킷(`HitResourceCommand.cs:102`). 상태 변경은 호스트만 함 |
| RPC 소유권 | `ServerSubmitPose`는 `RequireOwnership=true`이고, 서버가 정한 `_serverPlayer` 기준으로 처리(클라이언트가 ID를 정하지 못함) |
| 리치 텍스트 | 모든 TMP 글자에 `richText=false`를 강제해 Steam 표시 이름의 서식 태그가 동작하지 않음(`ThemedText.cs:93`) |
| 스크립팅 백엔드 | Standalone IL2CPP(`ProjectSettings.asset:847`). 난독화는 없음(협동 게임이라 필수는 아님) |
| 저장소 비밀 값 | 추적 파일·git 이력에 API 키, 키스토어, .env, 실제 SteamID, 사용자 경로 없음. 시험 코드의 SteamID는 합성 값(실제 로그 값과 겹치지 않음). `steam_appid.txt`(480, 개발용 공용 ID)는 저장소에만 있고 출시 빌드에는 없음 |
| 측정 원본 | `docs/test/perf/raw/`: CSV·JSON에는 플레이어 번호(1~8), 시간, CPU/GPU 모델명만 있음. SteamID·경로·PC 이름 없음 |
| 파일 로그 | `game_*.log`는 LogSanitizer를 거침(SteamID·비밀 값·사용자 폴더·외부 IP). 실제 파일에서 가려지지 않은 SteamID 0건 |
| 에디터 연결 패키지 | `com.unity.pipeline`(에디터·실행 중 게임 원격 명령) 런타임 어셈블리는 `defineConstraints`로 출시·개발 빌드 모두에서 빠짐(두 빌드 `ScriptingAssemblies.json`에 `Unity.Pipeline.Attributes`만 있음) |

## 5. 출시 빌드 개발 기능 제외 확인

| 기능 | 출시 빌드 | 근거 |
|---|---|---|
| `-transport tugboat` / 직접 UDP | **꺼짐** | `TransportPolicy.Resolve`·`TransportSelector.Apply`가 출시 빌드에서 Steam으로 고정(`CheckPlatformReady.cs`, `TransportSelector.cs`) |
| Steam 없이 시작(`-devPlayerId`) | **꺼짐** | `AllowsSteamlessStart`가 `isDevelopmentBuild`를 요구함 |
| `-host`, `-connect`, `-bot`, `-autowalk`, `-resourcechanges`, `-testcreatures`, `-gameversion`, `-bench`, `-autoquit`, `-bootsmoke`, `-steamlobbytest`, GenHash | **꺼짐** | `GameBootstrap.cs:93-119·316·390-412`, 모두 `const` 조건 |
| `-perflog`, 성능 CSV, 참가 시간 보고서 | **꺼짐** | `DevDiagnostics.cs:52`, `GameBootstrap.cs:397-398` |
| F1 생물 HUD, F3 성능 HUD | **꺼짐** | `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`(`CreatureInterestHud.cs`, `PerfTelemetryBehaviour.cs:107`) |
| 출시 빌드 boot.config | 정상 | `player-connection-*`, 디버거 연결 대기 없음 |
| `-upscale` | 동작 | SEC-008 참고(위험 없음, 명시 필요) |
| 개발 코드 존재 | 포함(실행 불가) | SEC-008 |

## 6. 의존성 목록

| 패키지 | 버전 | 출처 | 알려진 취약점 |
|---|---|---|---|
| FishNet | 4.7.4 (`FISHNET_VERSION`) | `src/Assets/ThirdParty/FishNet` | **확인 필요** (이번 점검에서 웹 검색 불가) |
| FishySteamworks | 4.1.1 | `src/Assets/ThirdParty/FishySteamworks` | **확인 필요** |
| Steamworks.NET | git 태그 `2025.165.0` | `Packages/manifest.json` | **확인 필요**. 고정 태그라 공급망 변동 위험은 낮음 |
| HDRP 17.3.0, Input System 1.20.1, Localization 1.5.8, Burst 1.8.31, Collections 2.7.1, Mathematics 1.3.3, uGUI 2.0.0, Test Framework 1.6.0 | 고정 | Unity 레지스트리 | 확인 필요(Unity 공식 패키지, 네트워크 공격면 없음) |
| com.unity.pipeline | 0.9.1-exp.1 (실험판) | Unity 레지스트리 | 에디터 원격 명령 패키지. 빌드에서 빠지는 것을 확인함(4장). 실험판이므로 출시 전에 manifest에서 빼는 것을 권장 |
| `com.unity.modules.unitywebrequest*` | 내장 | — | 코드에서 쓰지 않음. 공격면은 없고, 빼면 빌드 크기만 줄어듦(선택) |

- 참고: FishNet·FishySteamworks는 소스로 넣었으므로 버전을 올릴 때 NFR-049 절차(변경 이력 + 회귀 시험)를 따릅니다. 출시 전에 GitHub Issues·보안 공지를 직접 확인하기를 권장합니다.

## 7. 다음 사이클 점검 대상 (M2·M3 이후 기능)
- **세이브 변조(M3)**: `world.sav` 본문 해시는 손상 감지일 뿐 변조 방지가 아닙니다. 호스트 본인의 조작은 허용이지만, 변조·손상 파일을 읽을 때 멈추지 않는지(NFR-037), 경로 조작·거대 길이 필드를 확인합니다.
- **Steam Cloud(M3)**: 동기화 충돌·덮어쓰기 처리.
- **채팅·월드 이름·비밀번호(M2)**: NFR-050(길이, 제어 문자, 빈도), 비밀번호 시도 제한(FR-146), 로그·저장 제외(NFR-042).
- **강퇴·차단(M2)**: 호스트 전용 확인, 차단 목록 저장 위치·형식.
- **리치 프레즌스(M2)**: 표시 문자열에 개인정보가 없는지.
- **정식 App ID 전환**: 480 공용 ID에서 로비 혼선·필터(NFR-009). 출시 빌드의 `SteamAPI.RestartAppIfNecessary` 사용 여부.

## 8. ISO/IEC 25010 보안성 부특성별 요약

| 부특성 | 평가 | 근거 | 관련 SEC |
|---|---|---|---|
| 기밀성 (Confidentiality) | **보통** | 저장소·빌드에 비밀 값 없음, 파일 로그는 가려짐. 다만 Player.log에 SteamID·사용자 경로 원문이 남고, 비멤버 연결에 참가자 SteamID·위치가 노출될 수 있음 | SEC-002, SEC-005, SEC-009 |
| 무결성 (Integrity) | **보통** | 패킷 디코드·값 범위·타격 검증은 견고하고 호스트 권위가 지켜짐. 이동 검사는 시각 위조·자세 폭주로 우회되어 NFR-030 수용 기준 미달. 공유 대기열은 협동 방해에 약함 | SEC-001, SEC-004, SEC-006 |
| 부인 방지 (Non-repudiation) | **보통** | `move_rejected`·참가 허가/거부 로그에 플레이어 ID와 시각이 남음. 로그가 위조 가능한 로컬 파일이라 증거력은 제한적(협동 게임 수준에서는 충분) | SEC-003(로그 폭주로 기록 손실 가능) |
| 책임 추적성 (Accountability) | **보통** | 거부·디코드 실패를 연결별로 집계하고 로그로 남김. 로그 크기 상한·보관 정리(NFR-045)가 없어 폭주 시 추적이 어려움 | SEC-003 |
| 인증성 (Authenticity) | **보통** | 참가자 신원은 전송 계층 SteamID + 로비 멤버(D-14)로 정해 위장 불가. FishNet 계층은 이 판정 전에 연결을 인증 처리하고, 친구 관계는 Steam 로비 유형에만 의존함 | SEC-002, SEC-007 |

- 종합: 🔴 0 / 🟠 3 / 🟡 2 / 🟢 5. 우선 처리 권장 순서: **SEC-002 → SEC-001 → SEC-003** (세 건 모두 Net 어댑터·`AvatarMoveValidator` 안에서 고칠 수 있고 설계 방향 변경은 필요 없음), 그다음 SEC-005(설정 한 줄), SEC-004.

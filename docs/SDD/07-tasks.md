> SDD(소프트웨어 설계 명세서)의 일부 · 목차: [00-index.md](00-index.md)

## 7. 구현 작업 목록

- developer는 마일스톤 하나씩 위에서 아래 순서로 구현합니다. 각 마일스톤 안에서 SRS 2.6.1 공통 원칙 6가지(동기화, 호스트 검증, 저장 항목, 경험치 이벤트, 한국어·영어 텍스트, 게임패드 조작)를 함께 끝냅니다.
- 「결정 필요」는 그 작업을 시작하기 전에 사용자에게 받아야 할 SRS 부록 C 항목입니다. 결정이 늦으면 잠정 구현(3.7.6)으로 진행하고 기록합니다.
- 마일스톤 마지막 작업은 항상 「완료 빌드 + 그 마일스톤 FR 시험 + 아키텍처 시험」입니다(NFR-043).
- **실행 방식 (v0.8, D-22)**: 컴파일·EditMode·PlayMode 시험·Console 확인은 열린 에디터에 Unity CLI(`unity recompile`, `unity command test run ...`, `unity command console get`)로 합니다. batchmode 에디터 실행(`unity build`, `unity test`, `Unity.exe -batchmode`)은 쓰지 않습니다. v0.9: **플레이어 빌드(IL2CPP)도 열린 에디터에서** `unity command build run`으로 만들고(`BuildCommands` 경로, 산출물 검사 포함), 빌드한 플레이어를 `-batchmode -nographics`로 따로 실행하는 것은 허용합니다. 사용자에게 에디터를 닫아 달라고 요청하는 것은 **성능 측정 중**(에디터가 같은 CPU·GPU를 써서 결과가 흐려짐, 3.12.4)뿐입니다. M1은 해당 작업(M1-04, M1-12, M1-13)의 설명에 빌드·실행 순서를 적었고, M2 이후에도 같은 규칙을 적용합니다(각 마일스톤 완료 빌드, IL2CPP 결정론 교차가 들어간 M3-03·M4-09, 출시 작업 M3-15·M10-06, 측정 작업 M6-10은 측정 중 에디터 닫음). 빌드는 시간이 걸리므로 한 번에 몰아서 합니다.
- **UI 작업 공통 규칙 (v0.13, D-27)**: M2 이후 모든 화면·HUD 작업은 (1) uGUI 프리팹(`Assets/_Game/UI/Prefabs/`)을 공통 부품 프리팹으로 조합해 MCP 도구로 만들고 프리팹을 원본으로 둠, (2) 글자·그래픽·선택 요소에 `Themed*` 컴포넌트와 토큰(3.7.10), (3) 화면 루트에 `UiScreen`(층·정렬 순서·`FirstSelected`), (4) 씬에는 프리팹 인스턴스와 참조만, (5) `UiPrefabRulesTests` 통과와 버튼 `onClick.Invoke()` 시험, (6) 생성기로 기존 씬·프리팹을 덮어쓰지 않음(3.8.2)을 지킵니다.
- **버전 고정 (v0.8)**: 패키지 버전은 D-03 표 그대로 둡니다. 특히 `com.unity.modules.physics2d`(FishNet 컴파일 조건)와 `com.unity.modules.nvidia`·`com.unity.modules.amd`(DLSS·FSR2 컴파일 조건, v0.9)를 지우지 않고, `com.unity.pipeline`을 올리면 3.4.3 격리 검사를 다시 통과해야 합니다.

### M1 기반 — 기술 검증과 그에 필요한 최소 기능만 (C-51 확정, 사용자 결정 2026-11-02)

M1 범위: 기술 검증(M1-01~M1-12)과 이를 위한 최소 기능(로비 생성·초대·참가, 이동, 측정 도구). 세션 관리 세부(참가 조건 선택, 공개 목록, 비밀번호, 강퇴, 차단), 채팅, 옵션·다국어·입력 재설정 기반, 광과민성 경고, 이름표, 리치 프레즌스, 충돌 보고는 **M2로 옮겼습니다**(M2-01, M2-07~M2-10). M1의 최소 화면은 개발용이며, 글자는 처음부터 텍스트 키로 써 두고 M2-01에서 String Table에 연결합니다. **M1의 모든 UI는 처음부터 공통 기반(글자 크기 단계, 다크 단일 테마 색 토큰, 모양 구분 규칙, 3.7.10)으로 만들고**, 화면을 열어도 게임 진행은 멈추지 않게 합니다(3.10.7). v0.13: 공통 기반의 구현은 USS에서 **uGUI 테마(`UiTheme` + `Themed*`, DES-122)** 로 바뀌었고, 이미 만든 M1 화면은 아래 「M1 묶음 3 마무리 — UI uGUI 전환」에서 옮깁니다. **M2 이후의 모든 UI 작업은 uGUI 프리팹(`UI/Prefabs/`) + 테마 컴포넌트로 만들고, 프리팹이 원본**입니다(3.8.2).

| 순서 | 작업 | 관련 DES | 관련 FR/NFR | 선행 작업 |
|---|---|---|---|---|
| M1-01 | 프로젝트 골격: (1) `src/`에 **Unity 6000.3.27f2** + HDRP 프로젝트 생성(`C:\Program Files\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe`), 스크립팅 백엔드 IL2CPP(출시·측정)·Mono(에디터), HDRP 패키지 버전을 `manifest.json`에서 확인해 기록(v0.8: 17.3.0, D-03). (2) asmdef 13개와 참조 규칙(HDRP 참조는 `Game.Infra.Rendering`만), 아키텍처 시험(렌더링 격리 검사 포함). v0.8: 에디터 전용 `Game.Editor`와 시험 어셈블리 5개 포함(3.4.2). (3) **Unity Pipeline 패키지 설치**: `unity pipeline install`(Unity CLI 1.0.0-beta.15) → 에디터 조작 MCP(`unity mcp --project-path src`) 연결 확인. v0.8 결과: `com.unity.pipeline` 0.9.1-exp.1은 에디터 전용이 아니었고, 사용자 결정(2026-11-03)으로 **허용 + 안전장치 3가지**(3.4.3: `ENABLE_RUNTIME_PIPELINE*` 기호 금지, `Game.*` 참조 금지, 출시 빌드 산출물 검사 `BuildArtifactInspector`)를 적용. 출시 IL2CPP 빌드에서 Pipeline 런타임 어셈블리 0건 확인. (4) **Unity Smart Merge (v0.8 변경)**: `.gitattributes`의 `merge=unityyamlmerge` 드라이버 방식은 UnityYAMLMerge가 git이 넘기는 확장자 없는 임시 파일을 처리하지 못해 실패함(실제 `Boot.unity`로 확인). 저장소 로컬 git 설정을 **`git mergetool` 방식**으로 바꿈: `merge.tool=unityyamlmerge`, `mergetool.unityyamlmerge.cmd='<에디터 폴더>/Data/Tools/UnityYAMLMerge.exe' merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"`, `mergetool.unityyamlmerge.trustExitCode=false`, `mergetool.keepBackup=false`. 병합 드라이버(`merge.unityyamlmerge.driver`)는 등록하지 않으며, 씬·프리팹 충돌이 나면 `git mergetool`로 실행. 절차 문서 `docs/dev/SMART_MERGE_SETUP.md`. 첫 실제 충돌 때 동작 확인(R-28). Unity 직렬화 「Force Text」·.meta 표시 설정. (5) 개발/출시 정의 기호, App ID 설정 파일, 빌드 번호(v0.12: 게임 버전 규칙으로 바뀜 — M1 묶음 3 마무리, 3.7.12), `.gitignore`·Git LFS (git init은 사용자 결정대로 Do 시작 전) | DES-100, DES-112, DES-114 | NFR-027~029, NFR-043 | — |
| M1-02 | 네트워크 라이브러리 도입·버전 고정(FishNet 4.7.4R, FishySteamworks 4.1.1, Steamworks.NET 2025.165.0 git URL 고정) 조합 컴파일 확인(v0.8: 확인됨, 내장 모듈 `com.unity.modules.physics2d` 필수, D-03), TransportSelector, DevTools(`-transport`, `-host`, `-connect`, `-devPlayerId`), Steam 초기화·App ID 480, 부트의 Steam 확인 → 없으면 안내 화면 SCR-001(오프라인 플레이 없음, 개발 빌드 `-transport tugboat -devPlayerId`만 예외) | DES-101, DES-102, DES-103, DES-100 | FR-005, FR-006, NFR-006, NFR-008, NFR-009, NFR-031, NFR-049 | M1-01 |
| M1-03 | 설정 데이터 기반: ConfigLoader·검증, Balance·Network 튜닝 애셋, ContentRegistry·ContentHash, IConfigProvider. 부록 C.1 설정값 후보로 시작(C-33 해소) | DES-104, DES-051 | NFR-033, NFR-034 | M1-01 |
| M1-04 | **결정론 선행 검증**: DetMath, SeedCodec, 시험용 생성기 V0(높이 + 자원 2만 개 + 후보 자리), WorldHasher, 골든 해시 시험, `-genhash`·`-genhash-compare`. **이 PC에서** 에디터(Mono)·IL2CPP 빌드 결과 교차 비교, 같은 PC 반복 생성 일치, 골든 해시 고정까지 보고. v0.9 실행 순서(D-22, 실제로 한 방법): 골든 해시 시험(Mono)은 열린 에디터에서 EditMode 시험으로 → **에디터를 닫지 않고** 열린 에디터에서 `unity command build run`으로 IL2CPP 개발 빌드(184초) → 빌드한 플레이어를 `-batchmode -nographics -genhash`로 따로 실행(에디터와 별개 프로세스라 허용) → 결과 비교. 다른 CPU(Intel·AMD) 교차 검증은 M6-10으로 이동(다른 PC를 빌릴 수 있으면 `-genhash` 결과 파일 비교로 선택 실시) | DES-040, DES-041, DES-043, DES-046, DES-047, DES-113 | NFR-053(선행), FR-169(선행) | M1-01 |
| M1-05 | 측정 기반: 서버·클라이언트 ProfilerMarker, PerfTelemetry(서버 처리 예산 감시, 개발용 성능 표시, CSV), Bench 씬, 기본 파일 로그(민감 정보 제외. 보관 개수·충돌 보고는 M2-10). v0.8(D-22): 표식·기록기·CSV 형식은 열린 에디터에서 EditMode·PlayMode 시험으로 확인하고, 측정 빌드(IL2CPP)는 만들지 않음. 측정 빌드는 M1-12에서 한 번에 만듦(빌드 횟수를 줄임). v0.9: 구간 측정은 Stopwatch 기본·ProfilerMarker 보조, `enableFrameTimingStats = true`, 성능 표시는 IMGUI(F3, 개발 빌드 전용)(3.12.4) | DES-108, DES-113, DES-107 | FR-163, NFR-021, NFR-042 | M1-01 |
| M1-06 | 그래픽 기반: `IRenderBackend`·HdrpRenderBackend 골격, HDRP 품질 단계 4개, 프리셋 데이터, 업스케일러(DLSS/FSR 품질 모드) 적용, 기준 프리셋 「높음」 지정(C-52 확정). 옵션 화면은 M2-01. v0.8(D-22): 품질 단계·업스케일러 동작은 먼저 열린 에디터(플레이 모드)에서 확인하고, 플레이어 빌드에서의 최종 확인(R-19)은 M1-12 측정 빌드 때 함께 함(이 작업만을 위해 따로 빌드하지 않음). v0.9: 내장 모듈 `com.unity.modules.nvidia`·`amd` 추가(D-03), LOD 편향은 `GraphicsAssetBuilder`가 HDRP 애셋에 구워 넣음(프리셋 값 변경 시 메뉴 재실행), `GraphicsPresetConfig`는 `Game.Infra.Config`·Rendering은 `GraphicsPresetCatalog`만 받음(3.12.7) | DES-109, DES-114 | FR-120(데이터), FR-121(업스케일링), NFR-005, NFR-028 | M1-01 |
| M1-07 | **8인 Steam 접속(최소)**: 로비 생성(M1은 참가 조건 「친구 전용」 고정)·검색(`kh_game` 필터)·참가·초대, 최대 인원 8, JoinHandshake(버전·ContentHash·로비 멤버·원격 SteamID), 나가기·연결 끊김 처리. **v0.10 (D-23)**: 참가 절차가 처음으로 메시지를 쓰므로 여기서 먼저 만듦 — (1) `Game.Application/Protocol/` 기반: `MessageKind`, `ByteWriter`/`ByteReader`, 크기 상한, `MessageCodec`, 참가 메시지(JoinRequest, JoinAccepted, JoinRejected, WorldReady, JoinComplete)와 왕복·고정 바이트 시험(`Game.Tests.Application`). (2) `NetChannel`, `INetServer`/`INetClient` 시그니처를 Protocol 메시지 기준으로. (3) DES-121 `FishNetServer`/`FishNetClient` + `PacketBroadcast`. (4) JoinHandshake를 포트·메시지에만 의존하는 일반 클래스로. (5) 시험 asmdef `Game.Tests.Net`을 3.4.2 표대로 만들고(아키텍처 시험 `ExpectedTestReferences`도 같은 값) 가짜 `INetServer`로 JoinHandshake 시험 | DES-054, DES-085, DES-102, DES-031, DES-072, DES-080, DES-051, DES-121, DES-112 | FR-001~005, FR-011(퇴장), NFR-009, NFR-031, NFR-038, NFR-039, NFR-040 | M1-02, M1-03 |
| M1-08 | 1인칭 이동·달리기·스태미나(**v0.10 SRS v0.10**: 0이 돼도 불이익 없음 — 달리기만 걷기 속도, `스태미나 회복 지연` 1초 확정. 확정 규칙이라 `IZeroVitalPenaltyRule`을 거치지 않음). Input Actions 기본 바인딩 = STORYBOARD 2.4 기본 할당표 중 **키보드·마우스 전체 + 게임패드는 겹치지 않는 기본 행동(이동·시점·달리기·상호작용·공격 등)만**(v0.10, 사용자 결정 2026-11-03: 패드의 채팅·핑·퀵슬롯 등 겹치는 버튼 할당은 추후 다시 정의, 보류 — 패드 바인딩을 비워 둠). 안내 아이콘은 Xbox 계열만(C-28). PlayerAvatar(클라이언트 계산 + 호스트 검사, **v0.10 D-24**: NetworkTransform 대신 소유자 ServerRpc(비신뢰, `AvatarPose` 바이트) → `AvatarMoveValidator`(이동 검사, 서버 쪽 스태미나 추정, `Game.Application`) → ObserversRpc, 거부 시 TargetRpc 보정. 내 자세는 `ILocalPlayerPose`, 다른 참가자 자세는 `ClientWorldMirror`로 표시. `AvatarPose` 코덱 왕복 시험과 `AvatarMoveValidator` 시험은 `Game.Tests.Application`), **UI 공통 기반 `Base.uss`(글자 크기 배율·다크 테마 색 토큰·모양 구분 규칙)와 USS 규칙 검사 시험**, 그 위에 HUD 스태미나·로비/접속 화면, UI 상태(Gameplay/Overlay)는 입력 경로만 전환. **v0.13: 이 작업의 UI 부분(`Base.uss`, UXML 화면, USS 규칙 검사)은 「M1 묶음 3 마무리 — UI uGUI 전환」으로 uGUI 테마·프리팹·프리팹 규칙 검사로 바뀜**(완료된 작업의 결과물 교체, D-27) | DES-086, DES-084, DES-002, DES-003, DES-052, DES-051, DES-072, DES-080, DES-091, DES-092, DES-095 | FR-023~025, NFR-001(스태미나), NFR-030, NFR-056, NFR-057 | M1-07 |
| M1-09 | 명령·복제 기반: CommandDispatcher, RequestGuard, ServerCommandGateway, ServerWorld 틱, ReplicatedTable, ReplicationPublisher/Receiver, ClientWorldMirror. **v0.10 (D-23)**: M1-07의 Protocol 기반에 Command, CommandResult, SnapshotChunk, TableDelta(자원 항목 = 인덱스 2 B + 상태 2 B, 3.11.8), CreatureSpawn/Despawn/Snapshot(양자화) 메시지를 더하고 각각 왕복·고정 바이트 시험(`Game.Tests.Application`). 게이트웨이·복제 송수신은 포트와 Protocol 메시지에만 의존하고(FishNet 형식 없음), 가짜 `INetServer`로 크기·빈도·값 범위 검사와 스냅샷 + 델타 = 호스트 상태를 시험(`Game.Tests.Net`). FishNet 경유 확인은 PlayMode 루프백 시험(`Game.Tests.Integration`) | DES-050, DES-052, DES-053, DES-080~083, DES-121, DES-072 | FR-007(구조), FR-008, NFR-013, NFR-030, NFR-040, NFR-041 | M1-07 |
| M1-10 | **자원 2만 개 동기화**: V0 월드 로컬 생성, 시험용 타격 명령(호스트 검증), 자원 델타, 중도 참가(시드 → 생성 → 요약값 → 스냅샷 → 델타), ResourceViewStreamer, 「5,000개 변경」 상태 만드는 개발 도구 | DES-008, DES-057, DES-087, DES-089, DES-072 | FR-009, NFR-016, NFR-018 | M1-04, M1-09 |
| M1-11 | **시험용 AI 60체 부하**: AiJobsBackend(보행 격자·흐름장·조향·공간 해시), AiScheduler(10 Hz 분산, LOD), 생물 복제(관심 범위 등급), CreatureViews(보간) | DES-105, DES-062, DES-082, DES-088, DES-019 | NFR-011, NFR-017 | M1-09 |
| M1-12 | **M1 기술 검증 측정·보고 (이 PC 1대 + 봇 7개)**: P8 대체 측정(호스트 + `-bot` 7개, 직접 전송, 기준 프리셋 「높음」 + 업스케일링 품질 모드)으로 호스트 서버 처리 예산·대역폭·중도 참가 시간·메모리(프로세스 10 GB / VRAM 7.5 GB)를 판정, 호스트 FPS·1% Low는 봇 CPU 점유와 함께 참고값으로 기록, P1(1인) 측정, Steam 릴레이 접속 기능 확인 → `docs/test/perf/M1_tech_verification.md`(측정 조건 한계 명시). 실제 8대 P8 판정과 다른 CPU 결정론은 M6-10. 미달 시 3.12.9 순서로 대응안 제시 후 사용자 결정. v0.9(D-22): 열린 에디터에서 `unity command build run`으로 IL2CPP 측정 빌드(개발 빌드 `BuildWindowsDevelopment`, 산출물 검사 포함) → 측정 시작 전 **에디터 닫음**, 측정 중에도 **닫아 둠**(열린 에디터가 같은 CPU·GPU·메모리를 써서 측정 결과가 흐려지므로) → 측정 끝난 뒤 다시 열기 | DES-108, DES-113 | NFR-010~018, NFR-021, NFR-048 | M1-05, M1-06, M1-08, M1-10, M1-11 |
| M1-13 | M1 완료 빌드, M1 FR 시험, 아키텍처 시험. v0.9(D-22): FR·아키텍처 시험은 열린 에디터에서 Unity CLI로 → 같은 열린 에디터에서 `unity command build run`으로 출시 IL2CPP 빌드(`BuildWindowsRelease`)와 산출물 검사(Pipeline 런타임 어셈블리 0건, 3.4.3. Console의 `[Build]` 기록으로 검사가 돌았는지 확인). 에디터를 닫지 않음(성능 측정이 아님). v0.12: 출시 빌드의 게임 버전 `0.1.0+rel.…` 로그, 빌드 뒤 `bundleVersion` 원상 복구, 출시 ↔ 개발 빌드 접속 거부 확인(3.7.12) | DES-112, DES-113 | NFR-043 | M1-01~M1-12 |

- **v0.11 M1-07~M1-09 결과(2026-11-04)**: 구현 완료. EditMode 476/476, PlayMode 16/16 통과. Tugboat 8인 접속과 9번째 참가자 인원 초과 거부, Steam 호스트 경로(`-transport steam -host`) 확인. 실제와 다르게 적혀 있던 곳을 3.2.3, 3.4.1~3.4.3, 3.7.2, 3.7.4, 3.7.10, 3.8.2, 3.9.9, 3.10.1, 3.11.12~13, 3.12.1, 3.12.4~5, DES-084·100·101·103·112·121, D-25에 맞춤. 남은 사용자 결정: R-33(친구 전용 로비와 공개 목록), R-34(빌드 번호 규칙) — **v0.12에서 둘 다 해소**(사용자 결정 2026-11-04). M1-12 착수 때 볼 것: R-31(송신 할당 약 81 B/건), R-32(한 PC 8프로세스 1% Low 28 FPS 해석). 생성 메뉴 `Game/Setup/Run All`은 Do 묶음마다 다시 실행해 확인(D-25)
- **v0.12 M1 묶음 3 마무리 — 게임 버전 규칙 (D-26, 3.7.12, NFR-058, DES-037·DES-031·DES-080·DES-112)**: M1-07·M1-01 (5)에 대한 보완 작업으로, M1-10에 들어가기 전에 끝냅니다. 순서: (1) `GameVersion` 값 객체와 `Game.Tests.Domain` 시험 → (2) `SessionRules`·`JoinFacts`의 버전을 문자열로, `JoinRequest.gameVersion`(≤ 64 B)·`ProtocolVersion` 2·`protocolVer` 먼저 읽기 규칙, `HostWorldInfo`·`ClientHandshakeInfo`·`LobbyMetaBuilder`(`kh_build` = `Full`) → 코덱 고정 바이트·세션 시험 갱신 → (3) `BuildInfo.Version`(에디터 `+editor`, 해석 실패 = 설정 오류), 부트 로그 첫 줄, DevLobby 화면 버전 한 줄(`ToDisplayString`) → (4) `BuildCommands`(메타데이터 붙임·`finally` 되돌림·출시 태그 검사·`ReadBuildNumber`/`-buildNumber`/`GAME_BUILD_NUMBER` 폐지)·`ProjectConfigurator`(비어 있을 때만 `0.1.0`, `VersionPrefix` 폐지) → (5) 확인: `unity recompile`, EditMode·PlayMode 시험, 열린 에디터에서 개발 빌드 1회(`unity command build run`) 뒤 `[Build] 게임 버전=0.1.0+dev.…` 로그와 `bundleVersion`이 `0.1.0`으로 돌아왔는지(`git status`에 `ProjectSettings.asset` 변경 없음), 빌드한 플레이어 `-bootsmoke` 로그의 버전 줄, 같은 빌드 폴더 두 프로세스 Tugboat 접속 통과·에디터 재생 ↔ 플레이어 접속 「게임 버전 불일치」 거부. 출시 빌드 확인은 M1-13에서 함께 함(빌드 횟수를 줄임)
- **v0.13 M1 묶음 3 마무리 — UI uGUI 전환 (D-27, 3.7.10, 3.8.2, DES-091·092·095·122·112·100, FR-129·FR-130·FR-166, NFR-002·004·050·056·057)**: M1-08 UI 결과물의 교체 작업으로, **M1-10에 들어가기 전에 끝냅니다**(작업 수 변화 없음). **범위: 기능·화면 동작은 그대로**(SCR-001 Steam 필요, 개발용 로비·접속 중, HUD 스태미나 호·경고·명단·연결 상태, ESC 메뉴의 친구 초대·세션 나가기·닫기와 안내 문구·「● 게임 진행 중」 배지, 글자 크기·색 값 동일). 새 화면·새 기능은 넣지 않습니다. 순서:
  1. **패키지·자원**: `manifest.json`에 `"com.unity.ugui": "2.0.0"` 추가(D-03) → TMP Essential Resources 가져오기(`Assets/TextMesh Pro/`) → TMP Settings: 기본 글꼴, 「Enable Raycast Target」 끔. 사용자 작업: Pretendard(Regular·SemiBold·Bold) TTF와 OFL 라이선스 파일을 `Assets/_Game/UI/Fonts/`에 넣기(공식 배포처, 출시 전 라이선스 최종 확인) — 받기 전에는 TMP 기본 글꼴로 진행하고 글꼴 검사만 「대기」로 둠.
  2. **asmdef·아키텍처 시험**: 3.4.2 표대로 `UnityEngine.UI`·`Unity.TextMeshPro`를 `Game.Adapters.Presentation`·`Game.Editor`·`Game.Tests.Infrastructure`·`Game.Tests.Integration`에 추가, `ArchitectureRulesTests`의 `Expected`·`ExpectedTestReferences` 갱신, UI 패키지 격리·`UnityEngine.UIElements` 금지·`.uxml/.uss/PanelSettings` 없음 검사 추가(3.4.3).
  3. **테마 기반(DES-122)**: `UiTheme`(ScriptableObject)·`UiTokens`·`ThemedText`·`ThemedGraphic`·`ThemedSelectable`·`UiButton`·`UiScreen`·`UiRoot`(`UiScreenStack`, `UiInputRouter`, 글자 단계·언어 다시 적용)·`UiSafeArea16x9`·`UiRaycastBlocker`·`KeycapView`(M1은 Xbox·키보드 표시만)·`UiArcGraphic`, 에디터 토큰 드롭다운·`Apply Theme To Prefabs`, `UiThemeBuilder.Ensure`로 `UI/Theme/UiTheme.asset` 생성(값 = v0.12 `Base.uss`, 3.7.10 표). 순수 계산(글자 크기 반올림, 대비 계산)은 EditMode 시험.
  4. **입력**: `InputActionsBuilder`의 UI 지도에 `Point`·`Click`·`ScrollWheel` 추가 후 다시 실행.
  5. **프리팹 만들기(MCP 도구로 개별 오브젝트 조합)**: `UiRoot.prefab`(EventSystem + `InputSystemUIInputModule`, 3.7.10 (6)), 공통 부품(`Common/` 버튼 3변형·패널·모달·목록 행·배지), 화면 `Screens/SteamRequired`·`Screens/DevLobby`·`Screens/GameMenu`, `Hud/GameHud`(정적 루트 + `HudDynamic` 하위 캔버스). 배치·수치는 현재 UXML·USS 값과 STORYBOARD SCR-001·004·012·015·016을 따르고, 층·정렬 순서·숨김 규칙은 3.7.10 (4)·(5).
  6. **뷰 코드 교체**: `SteamRequiredView/Screen`, `DevLobbyView/Screen`, `GameHudView/GameHud`, `GameMenu`(Hud에서 분리)를 uGUI 참조로 다시 작성하되 **공개 API·이벤트는 유지**(`LeaveRequested`, `InviteRequested`, `OpenMenu`, `CloseMenu`, `SetMenuNotice`, `SetNetStatus` 등). `GameFlow`·`GameBootstrap`은 직렬화 참조 형식 교체와 `UiRoot` 초기화(테마·글자 단계·`ILocalizedText`·Input Actions 전달)만. 옛 정적 클래스 `UiTheme`(USS 적용 도우미)와 `StaminaArcElement` 삭제.
  7. **Boot 씬 고치기(다시 만들지 않음)**: 열린 에디터에서 MCP 도구로 `UIDocument` 오브젝트 3개(`SteamRequiredScreen`, `DevLobbyScreen`, `GameHud`)를 지우고 같은 이름의 프리팹 인스턴스와 `UiRoot`·`GameMenu` 인스턴스를 놓은 뒤 `GameBootstrap` 참조를 연결·저장. `Bootstrap`·`NetworkManager`(꺼짐)·`MenuCamera`는 그대로 둠.
  8. **생성기 정책 변경(3.8.2 표)**: `BootSceneBuilder`·`WorldSceneBuilder`·`AvatarPrefabBuilder`·`BenchSceneBuilder`를 「없을 때만 만들기 + 있으면 검사만」으로 바꾸고 `PanelSettings` 생성 코드 삭제, `UiThemeBuilder.Ensure`를 `Run All` 5단계로.
  9. **삭제**: `UI/Common/Base.uss`, `UI/Screens/*.uxml·*.uss`, `UI/Hud/Hud.uxml·Hud.uss`, `UI/PanelSettings.asset`, `Assets/UI Toolkit/` 폴더(모두 `.meta`와 함께).
  10. **시험**: `UiAndInputRulesTests`의 USS 부분 → `UiPrefabRulesTests`(3.4.3 표 전체, 입력 규칙 부분은 유지), `SteamRequiredScreenTests`·`GameMenuAndLobbyViewTests` → 프리팹을 불러 `onClick.Invoke()`로 같은 동작 확인(ESC 메뉴 친구 초대 불가 안내 문구 포함), 테마 시험(토큰 20 + 6 + 3개, 단계별 글자 크기 = v0.12 값, 대비), PlayMode `UiLayerPlayTests`(로비·게임·게임 + ESC 메뉴에서 맨 위 레이캐스트 결과, 내비게이션이 화면 밖으로 나가지 않음 — ESC 결함 회귀 시험).
  11. **확인**: `unity recompile`, EditMode·PlayMode 전체 통과, `Game/Setup/Run All` 실행 뒤 `git status`에 `Boot.unity`·UI 프리팹 변경 없음(덮어쓰지 않음 확인), `.meta` 누락 0, 열린 에디터에서 개발 빌드 1회(`unity command build run`) 뒤 플레이어에서 한글·기호가 □ 없이 보이는지 `-bootsmoke` 화면 캡처로 확인(TMP 셰이더·글꼴이 빌드에 들어갔는지). 사용자 작업 목록(글꼴 파일 넣기, 에디터에서 다듬을 배치)과 **수동 플레이 시험 요청**(키보드·마우스: 로비 방 열기, 상태 문구 글자색, ESC 메뉴 열기·닫기(Esc)·친구 초대 안내·세션 나가기, 키보드 방향키로 버튼 이동과 포커스 표시, 화면 비율 16:9·21:9 창 모드. Xbox 패드는 사용자 결정대로 나중에 한꺼번에)을 보고.

### M2 생존 상태 + M1에서 옮긴 부가 기능

| 순서 | 작업 | 관련 DES | 관련 FR/NFR | 선행 작업 |
|---|---|---|---|---|
| M2-01 | (M1에서 이동) 다국어·입력·옵션 기반: Localization(ko/en, 누락 키 검사, M1 화면 텍스트 키 연결 — v0.13: `ILocalizedText` 뒤를 String Table로 바꾸고 `ThemedText` 텍스트 키는 그대로, 프리팹 변경 없음), Input System 2개 체계·장치 전환·안내 아이콘(v0.13: `KeycapView` 아이콘 세트 데이터), SettingsService·LocalSettingsStore, 감도·반전·시야각·언어·그래픽 옵션 화면(v0.13: uGUI 프리팹 `Screens/Options`, 글자 크기 단계는 `UiRoot`에 적용), 첫 실행 언어 = Steam 언어(C-48), PlayStation 계열 안내 아이콘 추가(C-28) | DES-093, DES-092, DES-073, DES-110, DES-109 | FR-118, FR-120, FR-121, FR-157, FR-166, NFR-004, NFR-044, NFR-047 | M1-13 |
| M2-02 | 체력·허기·갈증·피로도, SurvivalSystems 틱, 0 불이익(IZeroVitalPenaltyRule — 허기·갈증·피로도만. 스태미나는 M1-08에서 확정 규칙으로 끝남, v0.10), 본인 상태 복제, HUD (결정 필요: C-04 중 허기·갈증·피로도·혹한 부분(스태미나 부분은 해소), C-37 남은 부분. 피로도 감소 속도 0으로 시험) | DES-003, DES-060, DES-033, DES-091 | FR-026~029, NFR-001 | M1-13 |
| M2-03 | 쓰러짐·소생·사망·부활(IDeathRule, IDownedDamageRule) (결정 필요: C-05 남은 부분, C-45) | DES-004, DES-060, DES-033 | FR-030, FR-031, FR-142, FR-143 | M2-02 |
| M2-04 | 낮/밤: WorldClock·시각 동기화, 장막 아래 조명·밤 가시 거리, 시작 정화 구역 햇빛 선적용(HDRP 적용은 DES-114) | DES-014, DES-090, DES-114, DES-015 | FR-033, FR-034, FR-035(시작 구역) | M1-13 |
| M2-05 | 입력 재설정(장치별 저장, 충돌 경고, 기본값 복원), 오디오 설정 | DES-092, DES-094, DES-073 | FR-117, FR-119 | M2-01 |
| M2-06 | 동료 상태 목록 | DES-091, DES-084 | FR-160 | M2-03 |
| M2-07 | (M1에서 이동) 세션 관리: 참가 조건 선택(초대 전용/친구 전용/공개, 세션 열기 때 선택, 세션 중 변경 → 로비 유형·프레즌스 갱신. 월드 파일 저장은 M3-12), 공개 세션 목록(참가 조건 「공개」 세션만 — Steam이 친구 전용 로비를 검색 결과에 넣지 않는 것은 정상 동작이며 친구 전용 세션은 친구 목록·초대로 참가(R-33 해소, 사용자 확인 2026-11-04, SRS FR-145 그대로). 접속 인원·예약 자리 수 `7/8 · 예약 1` 표시. M2에서는 `kh_resv` 필드와 표시만, 값은 M3-12 전까지 0. 버전 일치 표시는 `kh_proto`·`kh_build`를 내 버전과 비교(3.7.12 (5)·(6))), 비밀번호(저장 안 함, 시도 제한), 강퇴, 차단(호스트의 모든 월드 공통, `bans.json`·`IBanListStore`, C-46 해소) | DES-031, DES-054, DES-102, DES-030 | FR-003, FR-144~148 | M1-13 |
| M2-08 | (M1에서 이동) 텍스트 채팅·알림 기록, 텍스트 검증 | DES-069, DES-034, DES-091 | FR-127, FR-128, NFR-042, NFR-050 | M2-01 |
| M2-09 | (M1에서 이동) 광과민성 경고(v0.13: 시스템 층 프리팹), 동료 이름표(v0.13: 월드 표시 층 `Hud/WorldMarkers`, 화면 좌표 투영), 리치 프레즌스(메뉴·인원) | DES-095, DES-097, DES-070, DES-102 | FR-132, FR-159, FR-165 | M2-01, M2-07 |
| M2-10 | (M1에서 이동) 로그 보관 개수·회전, 충돌 보고(로컬 압축 파일 생성·폴더 열기까지만, 외부 전송 없음, C-43 해소) | DES-107 | FR-158, NFR-045, NFR-042 | M1-13 |
| M2-11 | (v0.4) HUD 뷰모델과 일반 HUD: HudViewModel, 공통 HUD 부품(v0.13: uGUI 부품 프리팹 `Hud/Parts/` — 상태 막대·위협·버프 시간·목표 자리·동료 항목), ScreenHud, HUD 표시 방식 옵션과 재시작 없는 전환(M2-02·M2-06의 HUD 표시를 이 구조로 정리) | DES-074, DES-091, DES-073, DES-110 | FR-171, NFR-001, FR-160 | M2-01, M2-02, M2-06 |
| M2-12 | (v0.4, v0.5 축소) 장치형 HUD: **v0.13: 첫 작업으로 UI 캡처 카메라 시작품(비용 `Client.UI`·TMP 표시 확인, R-39 — 문제 시 3.7.11 대체안을 사용자에게 보여 결정)**, 계기 화면 렌더 텍스처(uGUI 프리팹 `Hud/WristPanel` → 캡처 카메라, HDRP 설정은 DES-114), 표시 면 `IWristSurface` 인터페이스와 **(B) 화면 공간 기울인 평면 구현만**(v0.13: `UiWarpedRawImage`, 올림/내림은 위치·각도 보간), 경고 층 프리팹 `Hud/HudAlerts`(정렬 300), `ToggleWrist` Action(기본 할당 결정 대기)과 `WristRaiseMode`(기본 토글, 옵션 누르는 동안), 올린 동안 모든 행동 가능·다른 창이 열리면 자동 내림, 십자선 스태미나 호, 머리 위 등불 이름표(체력 4단계 + 꺼짐, 설정값), 피격 호·위협 테두리(UI 레이어, 계기 위)와 월드 보조 색조 골격(M2는 저체력·쓰러짐, 지역 위협은 M9a~c에서 연결)과 섬광 제한 적용, `손목 자동 내림 시간` 설정값, 손목 글자 가독성 확인(글자 크기 「크게」 포함). **제외**: (A)용 손 애니메이션·1인칭 손 그리기 패스·손목 소품 아트(M6-11 결정 뒤) | DES-120, DES-092, DES-114, DES-095, DES-097 | FR-172, FR-171, FR-159, NFR-001, FR-131 | M2-11, M2-09 |
| M2-13 | M2 완료 빌드·시험 | DES-113 | NFR-043 | M2-01~M2-12 |

### M3 채집·인벤토리·저장

| 순서 | 작업 | 관련 DES | 관련 FR/NFR | 선행 작업 |
|---|---|---|---|---|
| M3-01 | 고정 큰 배치 데이터(지역 순서·경계·시작 지점·근원 자리), 생성기 0.x(잿빛 숲 내부 + 다른 지역 자리표시 지형), 경계 보정 (결정 필요: C-13, C-56 위치) | DES-042, DES-043 | FR-102(틀), FR-168(잿빛 숲) | M1-04 |
| M3-02 | GeneratorRegistry·생성 프로필·골든 해시, GenerationValidator(잿빛 숲 조건), IGenRetryPolicy, `-genvalidate` (결정 필요: C-55, C-57) | DES-044, DES-045, DES-113 | FR-169, FR-170, NFR-054, NFR-055 | M3-01 |
| M3-03 | TerrainViewBuilder(36타일·물), 생성 진행 표시, 생성 시간 측정, 결정론 시험(이 PC에서 Mono·IL2CPP 교차 + 시드 100 반복 일치, 골든 해시 갱신. 다른 PC 3대 교차는 M6-10, 그 전에는 `-genhash` 결과 수집으로 선택 실시) | DES-089, DES-111, DES-113 | FR-168, NFR-052, NFR-053 | M3-01 |
| M3-04 | 아이템 기반: 콘텐츠 정의(아이템·자원 종류·드롭 표), 인벤토리, 줍기·버리기·이동·나누기·가득 참·손에 들기 (결정 필요: C-10) | DES-005, DES-006, DES-007, DES-009, DES-057 | FR-038~042 | M1-09 |
| M3-05 | 채집: E 채집, 도구 타격, 쓰러짐·파괴 애니메이션, 드롭, 동시 채집 (결정 필요: C-08, C-09) | DES-008, DES-009, DES-057, DES-087, DES-033 | FR-044, FR-045, FR-047~051 | M3-04 |
| M3-06 | 섭취 | DES-057, DES-060 | FR-032 | M3-04 |
| M3-07 | 경험치 이벤트 기록 시작(벌목·채광·채집), ActionEventRecorder, 이벤트 시험 (권장: C-06 중 행동 목록만 미리 확인) | DES-025, DES-068 | NFR-046 | M3-05 |
| M3-08 | 월드 파일: 코덱(헤더·청크·본문 해시·형식 버전·변환 틀), 저장소(원자적 저장·백업 회전), 저장 금지 항목 검사, JSON 덤프 도구 | DES-098, DES-099 | NFR-022~026, NFR-036, NFR-037, NFR-042 | M1-01 |
| M3-09 | 월드 생명 주기: 새 월드(이름·시드 입력/무작위·난이도·참가 조건 기본 친구 전용), 불러오기(재생성 + 바뀐 상태), 자동·종료 저장, 백업 복원, 저장 끊김 측정 (결정 필요: C-36) | DES-055, DES-030, DES-041, DES-029, DES-111 | FR-012~014, FR-018~020, FR-167, NFR-019, NFR-020 | M3-02, M3-08 |
| M3-10 | 플레이어 진행: SteamID 저장·복원·새로 생성, 퇴장 시 보존, 월드 파일 이전 시험 | DES-056, DES-002 | FR-011, FR-015~017, FR-022 | M3-09 |
| M3-11 | 월드 관리(이름 변경·복제·삭제·폴더 열기), Steam Cloud 월드별 동기화 (결정 필요: C-34) | DES-055, DES-099, DES-106 | FR-021, FR-149~152, NFR-007 | M3-09 |
| M3-12 | 세션 보강: 참가 조건 월드별 저장·다음 세션 기본값, 재접속(자리 예약·직접 연결, 예약 생성·해제 때 로비 `kh_resv` 갱신), 호스트 종료 예고 | DES-054, DES-031, DES-085, DES-030 | FR-115, FR-116, FR-144 | M3-09, M2-07 |
| M3-13 | 월드 난이도 구조(상태 배율) (결정 필요: C-47) | DES-029, DES-033 | FR-156(구조) | M3-09 |
| M3-14 | 나침반, 사망 위치 표시 | DES-097, DES-032 | FR-125, FR-141 | M2-03 |
| M3-15 | M3 완료: 생성 시간·결정론 측정 보고(NFR-021), 생성 지형 화면 사용자 확인, 빌드·시험 | DES-113 | NFR-021, NFR-043, NFR-053 | M3-01~M3-14 |

### M4 제작·도구·기본 건축

| 순서 | 작업 | 관련 DES | 관련 FR/NFR | 선행 작업 |
|---|---|---|---|---|
| M4-01 | 제작·재료 부족, 손 제작 / 제작대 구분(확정 규칙 `CraftingStationRules`, 제작법 데이터 `requiresStation`), 제작대(손 제작 후 배치), 레시피북 `Hotkey`(열람 + 손 제작, 「제작대 필요」 표시)·`Station`(제작대 팝업), `Craft` 명령의 제작대 거리·존재 검증, 제작대 동시 사용(`IStationUseRule`, 후보 가능), 경험치(제작) (결정 필요: C-60) | DES-010, DES-058, DES-012, DES-091, DES-025 | FR-052, FR-053, FR-173, FR-174 | M3-15, M4-04(제작대 배치) |
| M4-02 | 도구 등급·최소 도구 등급, 도구별 공격 모션·효과 | DES-008, DES-021, DES-058, DES-086 | FR-046, FR-054, FR-055 | M4-01 |
| M4-03 | 내구도·망가짐·수리 (결정 필요: C-40, C-41) | DES-011, DES-058 | FR-135~137 | M4-02 |
| M4-04 | 건축 배치·판정·미리보기, 경험치(건축), PvP 없음 시험 (결정 필요: C-12) | DES-012, DES-059, DES-096, DES-021 | FR-057, FR-058, FR-010 | M4-01 |
| M4-05 | 철거·건축물 수리 | DES-012, DES-059 | FR-138, FR-139 | M4-04 |
| M4-06 | 보관함(열람 구독, 동시 사용) (결정 필요: C-50) | DES-005, DES-059 | FR-133, FR-134 | M4-04 |
| M4-07 | 침대·수면·부활 지점 | DES-013, DES-059 | FR-140, FR-161 | M4-04 |
| M4-08 | 횃불 광원 | DES-090, DES-006 | FR-036 | M4-01 |
| M4-09 | (v0.6) 석탄 광맥: 생성 프로필 자원 표에 추가(분포 후보 모든 생성 지역, M4 시점에는 잿빛 숲만 생성됨, C-64), 생성기 0.x 갱신·골든 해시 갱신·결정론 재시험(이 PC Mono·IL2CPP 교차, 다른 PC는 M6-10), 검증 조건(진행 재료)에 석탄 추가, 채광 규칙·드롭 표 (결정 필요: C-64) | DES-043, DES-008, DES-045, DES-113 | FR-179, NFR-053, NFR-055 | M3-15 |
| M4-10 | (v0.6) 용광로: 배치형 건축물(제작 위치 후보 제작대, C-61), 연료·재료·결과물 칸(태우지 않은 연료는 꺼낼 수 있음), 여러 플레이어 공유(보관함과 같은 동시 조작 규칙), 열람 구독 `FurnaceView`, 건축물 테이블 `lit`, 철거 시 칸 내용 드롭, 용광로 화면 | DES-036, DES-075, DES-012, DES-091 | FR-175 | M4-04, M4-06 |
| M4-11 | (v0.6) 연료·가공 진행: 정수 연료 점수(나무 2·석탄 16·광석 1개 4점, 4점 미만이면 1개씩 태움, 남은 점수 저장·꺼낼 수 없음), 「남은 연료: 광석 n개분」 0.5개분 표시용 값, 완료 시각 최소 힙, 떠나도 진행·결과물 가득/연료 없음 정지, 「시작 시각 + 가공 시간」 전송·클라이언트 진행 막대, 저장 `FURN` 청크(진행 경과), 월드 닫힘·밤 건너뛰기 때 진행 안 함(`IFurnaceTimeRule`), 경험치(가공, `ISmeltXpRecipientRule` 잠정 = 재료 투입자), 용광로 다수 부하 측정(`용광로 최대 수`) (결정 필요: C-65, C-66) | DES-036, DES-075, DES-098, DES-025, DES-108 | FR-176, FR-177, NFR-046 | M4-10, M4-09 |
| M4-12 | (v0.6) 숯 만들기: 나무 → 숯 작업 종류, 숯 연료 점수(후보 16, C-62)·비율(후보 나무 4개 + 연료 4점 → 숯 1개, C-63)·시간 설정값 (결정 필요: C-62, C-63) | DES-036, DES-075 | FR-178 | M4-11 |
| M4-13 | M4 완료 빌드·시험(경험치 이벤트 포함) | DES-113 | NFR-043, NFR-046 | M4-01~M4-12 |

### M5 적·동물·전투

| 순서 | 작업 | 관련 DES | 관련 FR/NFR | 선행 작업 |
|---|---|---|---|---|
| M5-01 | 전투 피해 판정·처치, 경험치(처치) (결정 필요: C-24) | DES-021, DES-064, DES-033 | FR-076 | M4-02, M1-11 |
| M5-02 | 재에 물든 짐승, 재그림자 기본형(실제 판단 규칙) | DES-019, DES-062, DES-088 | FR-079, FR-080, FR-081(기본형) | M5-01 |
| M5-03 | 평범한 동물·사냥 드롭, 경험치(사냥) (결정 필요: C-38) | DES-019, DES-020, DES-063, DES-009 | FR-077, FR-078 | M5-02 |
| M5-04 | 스폰 감독: 동시 적 상한·웨이브 우선 대기열, 밤 습격, 정화 구역 생성 금지 (결정 필요: C-18) | DES-020, DES-063, DES-033 | FR-082, FR-066(선적용), NFR-017 | M5-02 |
| M5-05 | 광원 퇴치 (결정 필요: C-19) | DES-021, DES-033 | FR-037 | M5-02 |
| M5-06 | 핑 | DES-032, DES-069, DES-097 | FR-126 | M2-08 |
| M5-07 | 밤 건너뛰기 (결정 필요: C-42) | DES-013, DES-059 | FR-162 | M4-07 |
| M5-08 | 색각 보정, 흔들림·섬광 줄이기(후처리 적용은 DES-114) | DES-095, DES-114 | FR-130, FR-131 | M2-01 |
| M5-09 | M5 완료 빌드·시험 | DES-113 | NFR-043, NFR-046 | M5-01~M5-08 |

### M6 등광탑·정화 (수직 슬라이스)

| 순서 | 작업 | 관련 DES | 관련 FR/NFR | 선행 작업 |
|---|---|---|---|---|
| M6-01 | 정화 구역 완성: 추가만 가능, 보행 격자 반영(경계 진입 금지), 경계 대기, 정화 마스크 조명 (결정 필요: C-20) | DES-015, DES-105, DES-090, DES-033 | FR-035, FR-065~068, FR-071 | M5-09 |
| M6-02 | 등광탑 건설·활성화 확인·준비 단계·준비 완료·취소, 첫 등광탑 설계도 (결정 필요: C-13, C-16, C-17) | DES-017, DES-061, DES-010, DES-033 | FR-056, FR-059, FR-060, FR-153~155 | M4-04 |
| M6-03 | 웨이브·보스·인원 배율(적 수·보스 체력에만, 확정)·튜토리얼 방어전·난이도(적·손실 배율) (결정 필요: C-14, C-15) | DES-018, DES-061, DES-063, DES-029 | FR-061~064, FR-072, FR-073(잿빛 숲), FR-156 | M6-02, M5-04 |
| M6-04 | 방어 성공·실패·재도전 | DES-017, DES-061 | FR-065, FR-069, FR-070 | M6-03 |
| M6-05 | 정화 결과·해금·목표 안내 | DES-027, DES-061, DES-071 | FR-074, FR-075, FR-122 | M6-04 |
| M6-06 | 생존 튜토리얼, 스토리 도입 (결정 필요: C-25) | DES-035, DES-028, DES-071 | FR-110, FR-123 | M6-05 |
| M6-07 | 지도(탐험 기록) (결정 필요: C-53) | DES-097, DES-032, DES-071 | FR-124 | M6-01 |
| M6-08 | 자막·글자 크기, 방어전·보스 섬광 재검증 | DES-095, DES-094 | FR-129, FR-131 | M2-01 |
| M6-09 | 업적 구조, 리치 프레즌스(지역·방어전) (결정 필요: C-30, C-44) | DES-070, DES-102 | FR-164(구조), FR-165 | M6-04 |
| M6-10 | **수직 슬라이스**: 8인 플레이 시험, **실제 권장 사양 PC 8대 P8 정식 측정**(Steam 릴레이, 기준 프리셋 「높음」 + 업스케일링 품질 모드, 평균 60·1% Low 45 — M1 대체 측정 결과와 비교), **다른 CPU 결정론 교차 검증**(Intel·AMD 포함 PC 3대 이상, 시드 100, 생성기 0.x 현재 버전 + M1~M5 골든 해시 시드, NFR-053), 보고서. 불일치가 나오면 원인(연산·순서·런타임)을 찾아 고친 뒤 M3~M6 생성 결과 재확인(R-26) | DES-113, DES-108, DES-046 | NFR-051, NFR-021, NFR-053, NFR-011~014 | M6-01~M6-09 |
| M6-11 | **렌더 파이프라인 재평가 체크포인트** (사용자 결정 2026-11-02): M6-10 측정 결과(특히 GPU 시간), 실사 품질, Unity 렌더 파이프라인 정책·HDRP 지원 상황, 6.7 LTS 상태, 격리 상태(아키텍처 시험 결과, `Game.Infra.Rendering` 코드량, HDRP 전용 애셋 수)를 정리해 「HDRP 유지 / URP 전환 / Unity 버전 상승」 선택지와 영향(작업량·일정)을 보고 → 사용자 결정. **함께 결정(v0.5 추가)**: 손목 계기를 (B) 화면 공간 평면에서 (A) 3D 손목 소품으로 올릴지 — 수직 슬라이스 플레이 감상, 가독성(R-22), (A) 추가 작업량(손 애니메이션·1인칭 손 그리기 패스·소품 아트)과 URP 전환 시 영향(R-23)을 함께 보고. (A)로 정하면 그 작업을 다음 마일스톤에 추가 | DES-114, DES-109, DES-108, DES-112, DES-120 | NFR-028, NFR-011, NFR-012, FR-172 | M6-10 |

### M7 농사·요리

| 순서 | 작업 | 관련 DES | 관련 FR/NFR | 선행 작업 |
|---|---|---|---|---|
| M7-01 | 심기·물 주기·생장·햇빛 조건·그늘 작물·수확, 경험치(심기·수확) (결정 필요: C-22) | DES-022, DES-065, DES-033 | FR-083~088 | M6-10 |
| M7-02 | 요리 솥·보조 기구·인식 거리·조리 방식·요리법·목록·요리, 경험치(요리) (결정 필요: C-07, C-23) | DES-023, DES-065, DES-012, DES-091 | FR-089~095 | M4-04 |
| M7-03 | 요리 버프·만료·저장 후 유지 | DES-024, DES-066 | FR-096, FR-097 | M7-02 |
| M7-04 | M7 완료: 경험치 이벤트 누락 0건 검사, 빌드·시험 | DES-068, DES-113 | NFR-046, NFR-043 | M7-01~M7-03 |

### M8 숙련도

| 순서 | 작업 | 관련 DES | 관련 FR/NFR | 선행 작업 |
|---|---|---|---|---|
| M8-01 | 숙련도 모델·경험치·레벨업·스탯 포인트 (결정 필요: C-06) | DES-026, DES-067 | FR-098~100 | M7-04 |
| M8-02 | 스킬 트리·노드 획득 검증·효과 적용 | DES-026, DES-067, DES-021, DES-091 | FR-101 | M8-01 |
| M8-03 | M8 완료 빌드·시험 | DES-113 | NFR-043 | M8-01, M8-02 |

### M9a 잿물 늪·독기

| 순서 | 작업 | 관련 DES | 관련 FR/NFR | 선행 작업 |
|---|---|---|---|---|
| M9a-01 | 잿물 늪 생성 규칙·검증 기준(생성기 0.x 갱신, 골든 해시 갱신, 다중 PC 결정론 재시험) | DES-043, DES-045, DES-113 | FR-102, FR-168, FR-170, NFR-053 | M8-03 |
| M9a-02 | 장비 착용 칸 | DES-006, DES-066 | FR-043 | M8-03 |
| M9a-03 | 독기, 방독 마스크, 해독 절임 (결정 필요: C-21) | DES-016, DES-066, DES-024 | FR-104, FR-107, FR-108 | M9a-02, M7-03 |
| M9a-04 | 늪 재그림자(독), 늪 보스, 핵심 등광탑 고유 보스 | DES-019, DES-018, DES-088 | FR-073, FR-081 | M9a-01 |
| M9a-05 | 늪 단서, 제작법 의존 검사(n=2) (결정 필요: C-26) | DES-028, DES-071, DES-027 | FR-111, FR-103 | M9a-04 |
| M9a-06 | M9a 완료: 메모리 측정 후 지역별 애셋 읽기(Addressables) 도입 여부 결정, 빌드·시험 | DES-108, DES-113 | NFR-048, NFR-043 | M9a-01~M9a-05 |

### M9b 얼어붙은 고원·혹한

| 순서 | 작업 | 관련 DES | 관련 FR/NFR | 선행 작업 |
|---|---|---|---|---|
| M9b-01 | 고원 생성 규칙·검증 기준, 골든 해시·결정론 재시험 | DES-043, DES-045, DES-113 | FR-102, FR-168, FR-170 | M9a-06 |
| M9b-02 | 혹한(추가 불이익 C-04), 방한복, 따뜻한 수프 | DES-016, DES-066, DES-024 | FR-105, FR-107, FR-108 | M9b-01 |
| M9b-03 | 고원 재그림자(얼음), 고원 보스·핵심 보스 | DES-019, DES-018, DES-088 | FR-073, FR-081 | M9b-01 |
| M9b-04 | 고원 단서, 제작법 의존 검사(n=3) | DES-028, DES-071, DES-027 | FR-111, FR-103 | M9b-03 |
| M9b-05 | M9b 완료 빌드·시험 | DES-113 | NFR-043 | M9b-01~M9b-04 |

### M9c 검은 협곡·어둠

| 순서 | 작업 | 관련 DES | 관련 FR/NFR | 선행 작업 |
|---|---|---|---|---|
| M9c-01 | 협곡 생성 규칙·검증 기준, 골든 해시·결정론 재시험 (결정 필요: C-56 경계 접합 방식) | DES-043, DES-045, DES-113 | FR-102, FR-168, FR-170 | M9b-05 |
| M9c-02 | 완전한 어둠, 등광 랜턴, 야광 버섯 요리 (결정 필요: C-19 어둠 속 횃불) | DES-016, DES-066, DES-090, DES-024 | FR-106, FR-107, FR-108 | M9c-01 |
| M9c-03 | 협곡 재그림자(어둠 속 기습), 협곡 보스·핵심 보스 | DES-019, DES-018, DES-088 | FR-073, FR-081 | M9c-01 |
| M9c-04 | 협곡 단서, 제작법 의존 검사(n=4) | DES-028, DES-071, DES-027 | FR-111, FR-103 | M9c-03 |
| M9c-05 | M9c 완료 빌드·시험 | DES-113 | NFR-043 | M9c-01~M9c-04 |

### M10 최종 지역·결말

| 순서 | 작업 | 관련 DES | 관련 FR/NFR | 선행 작업 |
|---|---|---|---|---|
| M10-01 | 장막의 근원 고정 맵 배치·생성 지역과 경계 접합, 검증 조건(근원 연결) | DES-042, DES-043, DES-045 | FR-102, FR-168, FR-170 | M9c-05 |
| M10-02 | 세 위협 중첩 | DES-016, DES-066 | FR-109 | M9c-05 |
| M10-03 | 최종 등광탑·최종 보스(인원 배율) | DES-017, DES-018 | FR-112 | M10-01 |
| M10-04 | 결말 연출·하늘 열림 저장·결말 후 계속 플레이 | DES-028, DES-071, DES-033 | FR-113, FR-114 | M10-03 |
| M10-05 | 업적 목록 완성, 제작법 의존 검사(n=5) | DES-070, DES-027 | FR-164, FR-103 | M10-04 |
| M10-06 | 출시 전: 전체 성능 재측정, 생성기 V1 동결(공개 배포 시점), 출시 점검표(App ID, 로비 메타데이터, Cloud. v0.12: 게임 버전 `1.0.0`(또는 사용자가 정한 번호)으로 올림, 깨끗한 작업 트리에서 출시 빌드, 태그 `v<핵심>`과 빌드 커밋 일치, 3.7.12) | DES-113, DES-044, DES-112 | NFR-021, NFR-035, NFR-054 | M10-01~M10-05 |

### 마일스톤별 작업 수

| M1 | M2 | M3 | M4 | M5 | M6 | M7 | M8 | M9a | M9b | M9c | M10 | 합계 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 13 | 13 | 15 | 13 | 9 | 11 | 4 | 3 | 6 | 5 | 5 | 6 | 103 |

v0.2 변화: M1 18 → 13(부가 기능 5개 작업을 M2로 이동, 완료 작업 번호 변경), M2 6 → 11, M6 10 → 11(재평가 체크포인트 추가), 합계 96 → 97.
v0.4 변화: M2 11 → 13(HUD 뷰모델·일반 HUD M2-11, 장치형 HUD M2-12 추가, 완료 작업 M2-13으로 번호 변경), 합계 97 → 99.
v0.6 변화: M4 9 → 13(M4-01에 제작대·레시피북 진입 추가, 석탄 광맥 M4-09·용광로 M4-10·연료·가공 M4-11·숯 M4-12 추가, 완료 작업 M4-13으로 번호 변경), 합계 99 → 103. M4-01은 제작대 배치를 위해 M4-04(건축 배치) 뒤로 순서가 바뀜.

---

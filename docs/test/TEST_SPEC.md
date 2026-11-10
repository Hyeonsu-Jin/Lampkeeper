# 시험 명세서 (ISO/IEC/IEEE 29119-3) — 사이클 1 (M1)

- 문서 버전: 1.0 / 날짜: 2026-11-10
- TC 표기: 기존 자동 시험은 클래스(개수)로 묶어 TC에 대응시킴. 새로 쓴 시험은 메서드 이름에 TC ID가 있음(`TC050_...`). 기법 약어: 동등=동등 분할, 경계=경계값, 상태=상태 전이, 시나리오, 오류=오류 추측, 구조=구조 기반.
- 표의 「사전 조건·입력·절차」는 대응 시험 코드와 4장 수동 절차에 있다(표에는 요약만).

## 1. 시험 조건 요약
FR·NFR별 기대 동작을 TC로 묶었다. 호스트 권위(NFR-030)와 요청 제한(NFR-041)은 보고서(SEC-001·004, REV-001·003·004)에서 나온 적대적 시나리오를 별도 TC로 두었다(TC-050~054, 056).

## 2. M1 FR·NFR ↔ TC 대응표 (REVIEW_REPORT 추적성 지적 해소)
방식 A=자동, M=수동, P=측정(`perf/M1_tech_verification.md`). 우선순위 H/M/L.

| TC | FR/NFR·SCR | 레벨 | 방식 | 기법 | 대응 시험 (클래스, 개수) | 기대 결과 | 우선순위 |
|---|---|---|---|---|---|---|---|
| TC-001 | FR-001 세션 열기 | 단위·통합 | A | 동등, 상태 | Application.LobbyMetaBuilderTests(2), SessionServiceTests(8), Domain.SessionRulesTests(21), Infrastructure.GameMenuAndLobbyViewTests(11) | 방 열기 → 로비 메타·참가 조건(친구 전용)·상태 전이 정상 | H |
| TC-002 | FR-002 친구 초대 | 인수 | M | 시나리오 | MP-01(2PC) + TC-059(오버레이 창, 사용자 확인) | 초대 창 표시, 친구가 수락해 참가 | H |
| TC-003 | FR-003 세션 참가 | 통합·시스템 | A+M | 결정 테이블, 경계 | Net.JoinHandshakeTests(17), Domain.SessionRulesTests, GameVersionTests(53), Application.ClientSessionTests(11), ConnectScreenModelTests(9), PlayMode HostClientLoopback(8) + TC-054 + MP-01·03 | 거부 사유 하나 결정, 버전 불일치 때 두 버전 표시 | H |
| TC-004 | FR-004 최대 인원 | 단위 | A | 경계 | Application.ServerWorldPlayersTests(5, 8명/9번째), SessionRulesTests | 9번째 참가 거부(인원 초과) | H |
| TC-005 | FR-005 Steam 릴레이 | 인수 | M | 시나리오 | MP-02 | 서로 다른 네트워크 두 PC가 릴레이로 접속 | H |
| TC-006 | FR-006 / NFR-008 전송 전환 | 통합 | A | 동등 | PlayMode TransportSelectorTests(5), Application.CheckPlatformReadyTests(18), LaunchOptionsParserTests(25) + MP-03 | 출시는 Steam 고정, 개발은 Tugboat 선택, 출시↔개발 접속 거부 | H |
| TC-007 | FR-007 요청 호스트 검증 | 단위·통합 | A | 동등, 경계 | CommandPipelineTests(9), GatheringRulesTests(14), Net.GatewayTests(8), PlayMode Command_RoundTrips | 거리·상태 검증, 잘못된 요청 거부 | H |
| TC-008 | FR-008 월드 동기화 | 통합 | A | 시나리오 | Application.ReplicatedTableTests(9), Net.ReplicationPublisherTests(5), ResourceSyncTests(8), CreatureReplicationTests(17) | 호스트 변경이 참가자 사본에 반영 | H |
| TC-009 | FR-009 중도 참가 동기화 | 시스템 | A | 시나리오 | PlayMode LateJoin_With5000ChangedResources, ClientSessionTests | 중도 참가자 사본 = 호스트, 이후 델타 도착 | H |
| TC-010 | FR-010 PvP 없음 | 단위 | A | 구조 | Application.PvpAbsenceTests(1, 새로 추가) | 명령 종류에 플레이어 피해 명령 없음 | M |
| TC-011 | FR-011 세션 나가기 | 시스템 | A | 상태 | PlayMode LeavingSession_FreesSlot, ServerWorldPlayersTests, UiStateTests(5) | 슬롯 반환, 서버 정지, 화면 복귀 | M |
| TC-012 | FR-023 1인칭 이동 | 단위·인수 | A+M | 경계 | AvatarMoveValidatorTests(13), RemoteAvatarTrackTests(4), LocalPlayerPoseBufferTests(1), Infrastructure.InputActionsAssetTests(7) + TC-060 | 이동·보간·입력 맵 정상 | H |
| TC-013 | FR-024 달리기 | 단위·인수 | A+M | 상태 | AvatarMoveValidatorTests, Domain.StaminaStateTests(6) + TC-060 | 달리기 속도 인정, 스태미나 0이면 걷기 한도 | H |
| TC-014 | FR-025 스태미나 | 단위 | A | 경계, 상태 | StaminaStateTests, Application.StaminaHudModelTests(4) | 소모·회복·0 불이익 없음 | H |
| TC-015 | FR-120 그래픽 프리셋 | 단위·통합 | A | 동등 | GraphicsPresetTests(18), Infrastructure.GraphicsDataTests(9), PlayMode GraphicsAndTelemetry(4) | 프리셋마다 품질 단계·값 적용, 잘못된 단계 방어 | M |
| TC-016 | FR-121 화면 세부 설정 | 통합·인수 | A+M | 동등 | GraphicsPresetTests, PlayMode Backend_Upscaler_OnAndOff + MP-07 | 업스케일러 켜기·끄기, 설정 화면 표시 | M |
| TC-017 | FR-163 개발용 성능 표시 | 통합 | A | 시나리오 | Application.PerfTests(16), Infrastructure.DiagnosticsTests(11), PlayMode TelemetryBehaviour_RunsInFrameLoop | CSV·HUD 기록, 출시 빌드에서 비활성 | M |
| TC-018 | NFR-001 상태 표시(HUD) | 단위·인수 | A+M | 동등 | StaminaHudModelTests, HudModelTests + MP-07 | 스태미나 게이지 표시 | M |
| TC-019 | NFR-004 입력 장치 | 단위·통합 | A | 구조 | Infrastructure.InputActionsAssetTests, UiAndInputRulesTests(10), PlayMode UiLayerPlay(6) + TC-061 | 키보드·마우스 맵, Esc 메뉴, 포커스 이동 | M |
| TC-020 | NFR-005 권장 사양 | 인수 | M | 시나리오 | MP-06 | 권장 사양 PC에서 기준 FPS (M1은 개발 PC 한 대만) | L |
| TC-021 | NFR-006 Steamworks 연동 | 통합 | A+M | 동등 | CheckPlatformReadyTests, Infrastructure.SteamRequiredScreenTests(14) + MP-01 | Steam 없음 판정·안내, 실제 Steam 로비 | H |
| TC-022 | NFR-007 Steam Cloud | 해당 없음 | M | — | M3 구현. M1에서 확인 불가 | 차단 처리 | L |
| TC-023 | NFR-008 전송 방식 | 통합 | A | 동등 | TransportSelectorTests (TC-006과 공유) | 전송별 컴포넌트·틱률 | M |
| TC-024 | NFR-009 App ID 구성 | 통합 | A+M | 동등 | LobbyMetaBuilderTests + MP-01 | 로비 메타에 앱 구분 값, 480 공용 ID에서 혼선 없음 | M |
| TC-025 | NFR-010 1인 프레임 | 성능 | P | 시나리오 | P1 측정(보고서 2장) | 평균 60 이상(참고) | M |
| TC-026 | NFR-011 8인 호스트 프레임 | 성능 | P | 시나리오 | P8 측정, PlayMode BenchScene | 서버 틱·메인 스레드 예산 이내(참고) | H |
| TC-027 | NFR-012 참가자 프레임 | 인수 | M | 시나리오 | MP-04 | 참가자 FPS 측정(봇은 화면 없음) | M |
| TC-028 | NFR-013 참가자 대역폭 | 성능 | P | 시나리오 | P8 측정(174.8 kbps) | <= 512 kbps | H |
| TC-029 | NFR-014 호스트 업로드 | 성능 | P | 시나리오 | P8 측정(1,028 kbps) | <= 4,000 kbps | H |
| TC-030 | NFR-015 상태 반영 지연 | 인수 | M | 시나리오 | MP-05 | 지연 기준 이내 | M |
| TC-031 | NFR-016 대량 자원 동기화 | 시스템·성능 | A+P | 시나리오 | PlayMode LateJoin_With5000..., ResourceSyncTests, P8 측정 | 5,000개 변경 동기화, 프레임 유지 | H |
| TC-032 | NFR-017 동시 적 수 | 성능 | A+P | 경계 | CreatureReplicationTests, Domain.CreatureBrainTests(12), AiSchedulerTests(14), P8 측정(60체) | 60체 처리·복제 (상한 수치 판정은 M4 이후) | M |
| TC-033 | NFR-018 중도 참가 시간 | 성능 | P | 시나리오 | P8 측정(0.744 s) | <= 60 s | M |
| TC-034 | NFR-021 성능 측정 시점·조건 | 통합 | A+P | 시나리오 | PlayMode BenchScene_RunsToEnd, PerfTests, 보고서 4장(조건 기록, 시드 미기록) | 측정 조건 기록, CSV·요약 생성 | M |
| TC-035 | NFR-027 엔진 | 구조 | A | 구조 | Architecture.ArchitectureRulesTests(35) | 엔진 버전·asmdef 고정 | L |
| TC-036 | NFR-028 렌더 파이프라인 | 구조 | A | 구조 | Infrastructure.TerrainMaterialSceneTests(1), GraphicsDataTests | HDRP 설정·재질 | L |
| TC-037 | NFR-029 플랫폼 | 구조 | A | 구조 | Infrastructure.BuildArtifactInspectorTests(3), 출시 빌드 성공 | Windows IL2CPP 빌드, Pipeline 어셈블리 0건 | M |
| TC-038 | NFR-030 호스트 권위 | 단위·시스템 | A | 경계, 오류 | AvatarMoveValidatorTests(13), ArchitectureRulesTests, PlayMode AvatarPose_IsValidatedByHost + TC-050·051·052 | 2배 속도 위치는 되돌림, 상태 변경은 호스트만 | H |
| TC-039 | NFR-031 전송 계층 교체 | 구조 | A | 구조 | ArchitectureRulesTests(Domain·Application 엔진·전송 참조 0) | 게임 로직이 전송에 비의존 | M |
| TC-040 | NFR-038 참가자 끊김 시 세션 유지 | 시스템 | A | 상태 | PlayMode LeavingSession_FreesSlot, ServerWorldPlayersTests | 한 명이 나가도 세션 유지, 슬롯 재사용 | H |
| TC-041 | NFR-039 호스트 끊김 시 참가자 처리 | 단위·인수 | A+M | 상태 | ClientSessionTests(11), UiStateTests + MP-08 | 참가자가 메인 메뉴로, 실제 끊김 확인 | M |
| TC-042 | NFR-040 요청 값 검증 | 단위·시스템 | A | 오류, 경계 | ProtocolCodecTests(31), PlayMode MalformedPacket_FromClient, GatewayTests, CommandPipelineTests | 잘못된 값·패킷에도 호스트 정상 | H |
| TC-043 | NFR-041 요청 빈도 제한 | 단위 | A | 경계 | RequestGuardTests(4), CommandPipelineTests.ClientCannotBypassRate + TC-053 | 10배 요청에도 상한 이하만 적용, 다른 참가자 영향 없음 | H |
| TC-044 | NFR-048 메모리 | 성능 | P+M | 시나리오 | P8 측정(개발 빌드 1,366 MB) + MP-06 | 출시 빌드 <= 10 GB / VRAM <= 7.5 GB | M |
| TC-045 | NFR-049 네트워크 라이브러리 | 검사 | A | 구조 | SECURITY_REPORT 6장 버전 목록, ArchitectureRulesTests | FishNet 4.7.4 등 고정 기록 | L |
| TC-046 | NFR-053 생성 결정론 | 단위 | A | 동등 | WorldGen.DetMathTests(22), V0GeneratorTests(15), GenHashToolTests(10), 골든 해시 | 같은 PC 반복 생성 같은 해시 | H |
| TC-047 | NFR-058 게임 버전(SemVer) | 단위 | A | 동등, 경계 | Domain.GameVersionTests(53), Infrastructure.BuildInfoAndTextTests(3) | 규칙대로 비교·표시 | H |
| TC-048 | SCR 화면 계층·UI 규칙 | 통합 | A | 구조 | PlayMode UiLayerPlayTests(6), Infrastructure.UiPrefabRulesTests(17), Application.TextTableTests(13) | 로비·메뉴 레이어, 프리팹 규칙, 문구 표 | M |
| TC-049 | 월드 뷰 (지형·자원 풀링) | 시스템 | A | 시나리오 | PlayMode WorldViewPlayTests(2), Application.ResourceViewRulesTests(7) | 타일당 지형, 자원 뷰 풀 재사용 | M |
| TC-050 | NFR-030, FR-024, SEC-001 | 단위 | A(결함 재현) | 오류 | `AdversarialClientTests.TC050` | 달리기 2배(14 m/s) 정직한 시각 자세가 1회 이상 거부 | H |
| TC-051 | NFR-030, REV-001 | 단위 | A(결함 재현) | 오류 | `TC051` | 시각을 500 ms씩 올린 자세 30개(1초 분량)가 모두 수락되면 안 됨 | H |
| TC-052 | NFR-030, REV-003 | 단위 | A(결함 재현) | 오류 | `TC052` | 참가 직후 스폰에서 먼 첫 자세 거부 | M |
| TC-053 | NFR-041, REV-004, SEC-004 | 단위 | A(결함 재현) | 오류 | `TC053` | 한 참가자가 대기열을 채워도 다른 참가자 명령 수락 | M |
| TC-054 | FR-003, REV-002 | 단위 | A(결함 재현) | 시나리오 | `TC054` | 버전 불일치 거부 메시지·문구에 버전 자리 있음 | H |
| TC-055 | REV-014 | 구조 | A | 구조 | `ReleaseHygieneTests.TC055` | Assets 아래 원본 없는 `.meta` 0개 (로컬 디스크 기준) | L |
| TC-056 | SEC-005, NFR-042 | 구조 | A(결함 재현) | 구조 | `ReleaseHygieneTests.TC056` | `usePlayerLog: 0` | M |
| TC-057 | M1-09 측정용 생물·AI(FR 외 보조) | 단위·통합 | A | 상태 | Application.BotBrainTests(17), AiSchedulerTests, Infrastructure.AiJobsBackendTests(12) | 봇·AI 작업 스케줄, 네이티브 메모리 해제 | M |
| TC-058 | NFR-042·045 로그·설정 | 단위 | A | 동등 | ConfigTests(38), ConfigLoaderTests(9), LogSanitizerTests(23) | 설정 읽기, 로그에서 SteamID·경로 가림 | M |
| TC-059 | FR-002, SCR 로비 | 인수 | M(완료) | 시나리오 | 사용자 확인: Steam 오버레이 초대 창 | 초대 창 표시 | H |
| TC-060 | FR-023~025 | 인수 | M(완료) | 시나리오 | 사용자 확인: 이동·달리기·스태미나 | 정상 | H |
| TC-061 | NFR-004, SCR 메뉴 | 인수 | M(완료) | 시나리오 | 사용자 확인: 키보드 Esc 메뉴·방향키 포커스 | 정상 | M |
| TC-062 | FR-007 | 인수 | M(완료) | 시나리오 | 사용자 확인: 나무·돌 타격 | 정상 | H |
| TC-063 | NFR-016 | 인수 | M(완료) | 시나리오 | 사용자 확인: 5,000개 변경(FPS 순간 24 관찰) | 정상, 순간 하락은 R-44 | M |
| TC-064 | FR-009 | 인수 | M(완료) | 시나리오 | 사용자 확인: 빌드 2개 중도 참가 | 정상 | H |
| TC-065 | NFR-017 | 인수 | M(완료) | 시나리오 | 사용자 확인: 생물 60체 표시·추적·참가자 표시 | 정상 | M |
| TC-066 | FR-163 | 인수 | M(완료) | 시나리오 | 사용자 확인: F1 관심 범위 표시 | 정상 | L |

TC-059~066은 이번 Check에서 다시 실행하지 않았고, PROJECT_STATUS.md 결정 기록의 사용자 확인 내용을 근거로 한다.

## 3. 새 시험 코드 (이번에 추가)
| 파일 | TC | 비고 |
|---|---|---|
| `src/Assets/Tests/EditMode/Application/AdversarialClientTests.cs` | TC-050~054 | `[Category("KnownDefect")]`. 실패가 정상(결함 재현). `[Ignore]` 안 씀 |
| `src/Assets/Tests/EditMode/Architecture/ReleaseHygieneTests.cs` | TC-055, TC-056 | TC-056만 KnownDefect |
| `src/Assets/Tests/EditMode/Application/PvpAbsenceTests.cs` | TC-010 | 일반 시험 |

기존 `FirstPose_IsAcceptedAsBaseline`(`ServerAndSessionTests.cs`)은 REV-003의 동작을 정상으로 고정하고 있어, DEF-002를 고칠 때 함께 바꿔야 한다.

## 4. 수동 플레이 시험 절차 (29119-5 키워드 형식, 사용자 수행)
공통: 출시 빌드 `src/Builds/Release/`, 개발 빌드 `src/Builds/Development/`. Steam 로그인 필요.

- **MP-01 친구 계정 2PC 참가 (TC-002, 003, 021, 024)**: `실행 PC1 출시빌드` → `확인화면 SCR 로비` → `탭 방 열기` → `탭 친구 초대` → `실행 PC2(친구 계정) 출시빌드` → `수락 초대` → `대기 10초` → `확인 PC2 월드 진입` → `확인 PC1 명단에 참가자 2명` → `확인 서로 캐릭터 보임` → `확인 Logs/ 폴더 오류 0`.
- **MP-02 Steam 릴레이 (TC-005)**: 서로 다른 공유기의 두 PC로 MP-01 반복 → `확인 연결 성공` → `기록 접속까지 시간` → `확인 이동 동기화 끊김 없음`.
- **MP-03 출시↔개발 접속 거부 (TC-006, 003)**: `실행 PC1 출시빌드 방 열기` → `실행 PC2 개발빌드 참가` → `확인 거부 화면` → `확인 문구에 사유 표시(버전 두 개는 DEF-004로 아직 안 나옴)` → 반대 조합도 반복.
- **MP-04 참가자 프레임 (TC-027)**: MP-01 상태에서 `열기 F3`(개발 빌드) 또는 외부 FPS 표시 → `걷기 60초` → `기록 참가자 평균 FPS·1% Low`.
- **MP-05 반영 지연 (TC-030)**: 호스트가 `타격 나무` → 참가자 화면에서 변화까지 `기록 지연(ms)`(동영상 프레임 수로 추정).
- **MP-06 출시 빌드 메모리·권장 사양 (TC-020, 044)**: 출시 빌드로 방 열기 → `대기 5분` → `기록 작업 관리자 메모리·GPU 전용 메모리` → 기준 10 GB / 7.5 GB.
- **MP-07 HUD·그래픽 설정 화면 (TC-016, 018)**: `열기 설정 화면` → `확인 프리셋 4종 변경 즉시 적용` → `확인 업스케일러 켜기/끄기` → `달리기 확인 스태미나 게이지 표시` → 1920x1080, 2560x1080(21:9), 1280x720에서 `확인 UI 잘림 없음`.
- **MP-08 호스트 끊김 실제 (TC-041)**: MP-01 상태에서 호스트 프로세스 `강제 종료` → `확인 참가자 메인 메뉴로 복귀` → `확인 오류 문구`.
- **MP-09 비멤버 연결 실측 (SEC-002 확인)**: 현재 실행 옵션만으로는 `JoinRequest` 없이 연결만 여는 방법이 없다. developer가 개발 전용 옵션(연결만 하고 요청 안 보냄)을 만든 뒤, 개발 빌드 2개에서 `확인 호스트 로그에 PlayerAvatar 스폰이 수신되는지` 본다. 사용자 수행 전에 준비 필요.

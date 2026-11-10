# 검토 보고서 (IEEE 1012 · CMMI · SPICE · 코드 리뷰) — 사이클 1 (M1 기술 검증)

- 문서 버전: 1.0
- 작성: code-reviewer (Check 모드)
- 평가 날짜: 2026-11-10
- 브랜치·기준 커밋: `cycle/1-mvp` @ `a5db2df` (커밋 91개, `main` 대비 74개)

---

## 1. 개요

| 항목 | 내용 |
|---|---|
| 사이클 | 1 (PDCA Check 단계), 구현 범위 M1-01~M1-13 (기술 검증) |
| 평가 대상 문서 | `docs/SRS.md` v0.12, `docs/SDD.md` v0.14, `docs/STORYBOARD.md` v1.8, `docs/PROJECT_STATUS.md`, `docs/test/perf/M1_tech_verification.md` |
| 평가 대상 코드 | `src/Assets/_Game/Code/` (163개 파일, 약 26,800줄) 중 M1 핵심 부분 표본: `Application/Server/ServerWorld.cs`, `Application/Protocol/Messages.cs`, `Application/Session/`, `Domain/Session/SessionRules.cs`, `Adapters/Net/`(Avatar·Join·Gateway·Replication·Transport), `Bootstrap/Runtime/`(GameFlow·SessionRuntime), `Infrastructure/AiJobs/AiJobsBackend.cs`, `Infrastructure/Steam/SteamLobbyService.cs`, `Adapters/Presentation/`(Player·World·UI/Common·Hud), asmdef 14개, 시험 48개 파일(목록·표본) |
| 범위 밖 | M2 이후 기능(생존 수치, 인벤토리, 저장, 건축 등) 미구현은 결함으로 보지 않음 |
| 평가 방법 | 문서 절·ID 검색과 부분 읽기, `git log`·커밋 꼬리말 통계, asmdef 참조 비교, 소스 표본 정독, `.meta` 짝 검사 |

**한계 (자체 평가)**: 이 평가는 프로젝트 문서·코드·git 이력을 근거로 한 **자체 평가**입니다. 공식 CMMI 평가(SCAMPI)나 공인 ISO/IEC 33000(SPICE) 심사를 대신하지 않습니다. 코드는 표본 검토이므로 검토하지 않은 파일에 문제가 없다는 뜻은 아닙니다. 보안은 핵심만 짚었고, 자세한 점검은 `docs/check/SECURITY_REPORT.md`(security-auditor) 몫입니다. 실행 시험은 하지 않았습니다(test-engineer 몫).

---

## 2. 요약: 주요 문제 Top 5

| 순위 | 문제 | 근거 | 관련 |
|---|---|---|---|
| 1 | 🔴 **호스트 이동 검사를 시각 조작으로 우회할 수 있음.** 허용 이동 거리를 클라이언트가 보낸 `TimeMs` 차이로 계산하고, 자세 RPC 빈도 제한이 없습니다. 조작된 클라이언트는 자세마다 최대 약 4.9 m를 인정받으므로 30 Hz에서 약 146 m/s까지 움직일 수 있습니다. 이는 SDD R-07이 받아들인 「속도 한도 안의 위치 조작」을 넘어섭니다 | `ServerWorld.cs:168-173`, `PlayerAvatar.cs:117-140` | REV-001, NFR-030, FR-007 |
| 2 | 🔴 **FR-003 「게임 버전 불일치」 표시에 두 버전이 나오지 않음.** SRS v0.11 수용 기준(`0.1.0`·`0.2.0` 표시)이 STORYBOARD와 코드에 반영되지 않았습니다. 요구사항 변경이 아래 산출물로 전파되지 않은 사례입니다 | `Messages.cs:312-336`, `strings.ko.txt:46-48`, `STORYBOARD.md:739` | REV-002, FR-003, NFR-058 |
| 3 | 🟡 **명령 대기열을 모든 참가자가 함께 쓰고, 빈도 제한은 대기열을 지난 뒤에 검사함.** 참가자 한 명이 대기열(256)을 채우면 다른 참가자의 명령이 `RateLimited`로 버려집니다. SDD 3.11.13의 「버킷을 통과한 명령만 대기열」과도 다릅니다 | `ServerCommandGateway.cs:140`, `ServerWorld.cs:477`, `CommandPipeline.cs:214` | REV-004, NFR-041 |
| 4 | 🟡 **형상 관리 규칙 이탈.** (1) Plan과 M1 묶음 1(feat·test 포함 17커밋)이 `main`에 직접 커밋됨, (2) Do 결함 5건에 DEF ID가 없고 `Closes:` 꼬리말이 0건, (3) 출시 IL2CPP 빌드가 `.dirty` 트리에서 만들어짐 | `git log main`, PROJECT_STATUS 측정 표·결정 기록 138행 | CMMI CM, SPICE 형상 관리 |
| 5 | 🟡 **추적성 끝단이 비어 있음.** FR→DES→SCR→코드(파일 머리 주석) 연결은 좋지만 TC ID와 FR↔시험 대응표 파일이 없습니다. M1 FR·NFR 대응표는 developer 보고 요약(PROJECT_STATUS 138행)에만 있습니다 | `docs/test/`에 perf만 있음 | IEEE 1012 추적성 |

---

## 3. IEEE 1012 요구사항 평가 (SRS v0.12 독립 평가, M1 배분 FR·NFR 중심)

표본: SRS 2.6.1 M1 행(FR-001~011, FR-023~025, FR-120, FR-121, FR-163, NFR-001, NFR-004~018, NFR-021, NFR-027~031, NFR-038~041, NFR-048, NFR-049, NFR-053, NFR-058), 부록 A·D.

| 평가 항목 | 판정 | 근거 | 권고 |
|---|---|---|---|
| 정확성 | 적합 | 사용자 결정이 본문에 반영됨: 스태미나 0 불이익 없음(FR-024·FR-025, v0.10), SemVer(NFR-058, v0.11), M1 참가 조건 「친구 전용」 고정(FR-001 마일스톤 줄), P8 판정은 M6(3.3). 부록 D 변경 이력이 결정 날짜와 맞음 | — |
| 일관성 | 부분 적합 | (1) 2.6.1 M1 행은 「NFR-010~NFR-018(P8-봇 예비 측정)」인데 NFR-021은 M1 측정 대상을 NFR-011·013·014·016·048로 적음. NFR-012·015·017은 어느 쪽 기준으로도 M1 처리가 분명하지 않음. (2) FR-003(v0.11)의 「두 버전 표시」와 STORYBOARD SCR-012 실패 문구(`STORYBOARD.md:739`)가 다름. (3) SDD v0.14 머리말이 「참조 SRS v0.11」(SRS는 v0.12. 새 FR은 없어 영향 작음) | 2.6.1 M1 행을 NFR-021 목록과 맞추고, M1에서 뺀 NFR(012·015)은 「M6」으로 명시. STORYBOARD SCR-012 문구 갱신 |
| 완전성 | 부분 적합 | M1 범위의 정상·예외 흐름(인원 초과, 버전 불일치, 시간 초과, 연결 끊김)은 정의됨. 빠진 것: 연속 스트림(아바타 자세)의 **시각 기준과 빈도 상한**. NFR-041은 「요청」 빈도만 다루고, NFR-030은 「속도·범위 검사」만 적어 시각을 누가 정하는지 비어 있음 → REV-001의 원인. 부록 C 확인 필요 39건(마일스톤별 결정 시점 있음) | NFR-030에 「허용 이동량은 호스트 수신 시각 기준」, NFR-041에 「자세 스트림 상한(예: 송신 주기 ×1.5)」 추가 검토 |
| 정밀성 | 적합 (M1 범위) | 수치는 이름 있는 설정값(NFR-033)으로 두고, P1/P8/P8-봇 측정 조건을 정의함(3.3). 다만 NFR-021 수용 기준(측정 조건 기록)에 비해 M1 보고서는 시드 값을 적지 못함(`M1_tech_verification.md` 1장 「로그에서 값 확인 못 함」) | 측정 스크립트가 시드를 CSV·요약에 기록하게 함(test-engineer·developer) |
| 가독성 | 적합 | Given-When-Then, 한 항목에 요구 하나. 다만 문서가 약 30만 바이트이고 FR 설명에 마일스톤별 예외가 섞여 M1 범위만 읽기 어려움 | 마일스톤별 수용 기준을 하위 줄로 나눠 적기(아래 시험 가능성) |
| 시험 가능성 | 부분 적합 | FR-003 수용 기준이 M2 사유(비밀번호·차단·참가 조건)를, FR-009가 M4 건축물 5개를, FR-011이 M3 아이템 복원을 포함해 **M1 시점에 수용 기준 전체를 판정할 수 없음**. FR-025는 「공격은 M1에 없으므로…」처럼 분리해 둔 좋은 예. FR-005는 서로 다른 공유기 두 대가 필요해 M1 미확인(PROJECT_STATUS 138행) | 마일스톤이 여러 개인 FR은 FR-025처럼 「M1 판정 범위」를 수용 기준에 명시 |
| 추적성 | 부분 적합 | FR→DES: SDD 6장. FR→SCR: STORYBOARD 6장. DES/FR→코드: 모든 `.cs` 머리 주석에 `관련:` ID 있음(163/163 머리 주석 확인). 커밋 꼬리말 `Refs:` 70/91. **끊긴 곳**: (1) TC ID 없음, FR↔시험 대응표가 파일로 없음. (2) DEF ID 없음(결함 5건은 측정 표에 설명만). (3) FR-003 v0.11 변경이 SCR-012·코드로 전파되지 않음 | Check에서 test-engineer가 TC ID와 FR↔TC 표 작성. 결함에 DEF ID를 붙이고 `Closes:` 사용 |
| 인터페이스 분석 | 적합 | Steamworks(SDD 3.7.1), 프로토콜(3.7.2, 첫 필드 `protocolVer` 위치 고정), 거부 사유 코드(3.7.5), 포트(3.7.3)가 정의되고 코드가 따름(`Messages.cs:214-239`의 길이 상한·개수 상한 검사). 빈 곳: `JoinRejected`에 버전 정보가 없어 FR-003을 만족할 수 없음 | `JoinRejected(VersionMismatch)`에 호스트 버전 문자열(64바이트 상한) 추가를 SDD 3.7.2에 반영 |
| 중요도 분석 | 적합 | 부록 A.1 무결성 수준 2, 저장·호스트 검증을 높은 중요도로 지정 | — |
| 위해 분석 | 적합 | 광과민성·멀미(FR-118, FR-131, FR-132, M2). M1 범위에서는 해당 없음 | — |
| 보안 분석 | 부분 적합 | 호스트 권위(NFR-030), 요청 검증(NFR-040), 빈도 제한(NFR-041), 원격 SteamID 기준 신원(D-14)을 정의함. 「속도 한도 안 위치 조작」을 위험으로 받아들인 근거(R-07)도 있음. 다만 시각 기준이 빠져 실제로는 속도 한도 자체를 우회할 수 있음(REV-001). 참가자 한 명이 다른 참가자를 방해하는 가용성 측면(REV-004, REV-007)도 요구로 정의되지 않음 | security-auditor 결과와 함께 NFR-030·NFR-041 보강 |
| 리스크 분석 | 적합 | PROJECT_STATUS 위험 목록(약 75건), SDD 8장 R-01~R-45. M1 측정 보고서 5장이 위험을 측정값으로 다시 평가함 | 위험 목록에 「해소」 행이 쌓여 있으므로 해소 항목을 아래 절로 분리 권장 |
| 구현-요구 불일치 | 부분 적합 | 불일치: FR-003 두 버전 표시(REV-002), NFR-030 속도 검사 우회(REV-001). 미확인(수동 남음): FR-002·FR-003 2PC, FR-005 Steam 릴레이, NFR-012, NFR-015, NFR-039 실제, NFR-048 출시 빌드 | 미확인 항목은 test-engineer 수동 시험 요청으로 넘김 |

---

## 4. CMMI 평가

| 실천 영역 | 판정 | 근거 (증거) |
|---|---|---|
| 요구사항 개발 및 관리 (RDM) | 부분 충족 | 충족: SRS IEEE 830 구조, ID 체계, 부록 A 자체 평가, 부록 C 확인 필요 39건, 부록 D 변경 이력(v0.1~v0.12), 측정 표의 버전별 FR·NFR 수. 미흡: 승인된 변경(FR-003 v0.11)이 STORYBOARD·코드로 전파되지 않음, FR↔TC 양방향 추적 없음 |
| 계획 수립 (PLAN) | 충족 | SRS 2.6.1 마일스톤 배분, SDD 7장 M1-01~M1-13 작업·선행 관계, 묶음 단위 진행(PROJECT_STATUS 측정 표 묶음 1~4) |
| 모니터링 및 통제 (MC) | 충족 | PROJECT_STATUS 단계별 상태·결정 기록(118행)·위험·측정 표, 묶음마다 결과 기록 커밋(`docs: Record M1-xx …` 다수), Do 승인 기록(a5db2df) |
| 기술 솔루션 (TS) | 충족 | SDD v0.14(IEEE 1016 관점별), 설계 결정 D-01~D-27, asmdef 표(3.4.2)와 실제가 일치(14개 asmdef 참조·`noEngineReferences` 확인), 아키텍처 시험이 표와 실제를 자동 비교 |
| 제품 통합 (PI) | 부분 충족 | 충족: `Editor/BuildCommands.cs`·`BuildArtifactInspector`(Pipeline 어셈블리 0건 검사), PlayMode 통합 시험 26건, 한 PC 호스트 + 봇 7개 통합 측정, 출시 IL2CPP 빌드 성공. 미흡: 출시 빌드가 `.dirty` 트리(재현 불가), 자동 빌드 파이프라인(CI) 없음, 2PC·Steam 릴레이 통합 미확인 |
| 검증 및 확인 (VV) | 부분 충족 | EditMode 663 + PlayMode 26 통과, 측정 보고서와 원본 데이터(`docs/test/perf/raw/`, git 관리). 29119-3 시험 문서·TC ID는 Check에서 작성 예정. 확인(Validation)은 수동 시험 일부만(PROJECT_STATUS 138행 「수동 남음」 11건) |
| 동료 검토 (PR) | 부분 충족 | architect가 SDD 작성 중 SRS 문제 5건 보고(SRS v0.4 이력), 총괄 재확인 기록. 코드 동료 검토는 이 보고서가 처음(Do 중 코드 리뷰 기록 없음) |
| 형상 관리 (CM) | 부분 충족 | 충족: git + LFS, Conventional Commits, 사이클 브랜치 원격 push, `Refs:` 꼬리말 70/91, `.meta` 짝 대부분 정상, 문서별 변경 이력, 측정 원본 데이터 git 관리. 미흡: (1) `main`에 직접 커밋 17개(병합 커밋 0, Plan·M1 묶음 1 포함), (2) DEF ID·`Closes:` 0건, (3) 출시 빌드 `.dirty`, (4) 제목 50자 초과 15/91, (5) `.meta`만 남은 항목 3개(REV-014) |
| 프로세스 품질 보증 (PQA) | 부분 충족 | 규칙 자동 검사(아키텍처 시험 약 35건, UI 프리팹 규칙 시험, 소스 글자 검사)가 강점. 독립 QA 활동은 이번 Check가 처음. 규칙 이탈(브랜치·DEF ID)을 잡아낸 기록 없음 |
| 위험 및 기회 관리 (RSK) | 충족 | PROJECT_STATUS 위험 목록(출처·영향·대응), SDD 8장 R-01~R-45, M1 보고서 5장 위험 재평가(R-44 「빌드에서 재현 안 됨」 등) |
| 의사결정 분석 및 해결 (DAR) | 충족 | SDD D-xx(대안 비교·근거), PROJECT_STATUS 결정 기록(날짜·근거), STORYBOARD 7장 디자인 결정 기록, HDRP 유지보수 모드 대응(NFR-051 재평가 시점) |

**성숙도 추정**: **수준 2 (관리됨)**, 수준 3(정의됨)의 특성 일부 있음.
- 수준 2 근거: 요구사항·계획·모니터링·형상·위험 관리가 프로젝트 안에서 실제로 수행되고 기록됨.
- 수준 3에 못 미치는 이유: CLAUDE.md와 에이전트 정의가 표준 프로세스 역할을 하지만, 그 프로세스를 지키지 못한 곳(브랜치 전략, DEF ID, 요구사항 변경 전파)을 걸러 내는 장치가 없고, VV·PR·PQA가 부분 충족임.
- 수준 4~5: **평가 불가**. 측정 데이터(시험 통과율, 결함 수, 성능 CSV, 토큰 보고서)는 있지만 프로세스 성능 기준선·통계적 관리 기록은 없음.

---

## 5. SPICE 평가 (ISO/IEC 33020 등급 기준)

등급: N 0~15%, P 15% 초과~50%, L 50% 초과~85%, F 85% 초과~100%. 능력 수준은 「해당 수준 PA가 모두 L 또는 F이고, 아래 수준 PA가 모두 F」일 때 달성으로 봄.

| 프로세스 | PA1.1 | PA2.1 | PA2.2 | PA3.1 | PA3.2 | PA4.1 | PA4.2 | PA5.1 | PA5.2 | 능력 수준 | 근거 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 요구사항 분석 | F | F | L | L | L | P | N | N | N | **2** | SRS v0.12, 부록 A·C·D, 단계 승인. PA2.2 L: 변경 이력은 있으나 승인된 변경(FR-003)이 다음 산출물로 전파되지 않음. PA4.1 P: FR·NFR 수 추이만 측정 |
| 설계 | F | F | F | L | L | P | N | N | N | **3** | SDD v0.14 관점별 설계, D-xx, 변경 이력, asmdef 표 ↔ 실제 자동 비교. 경미한 불일치 1건(3.11.13 대기열 순서, REV-004). 표준 절차는 CLAUDE.md·에이전트 정의 |
| 구현 | F | F | L | L | L | P | N | N | N | **2** | M1-01~13 구현, 묶음 단위 커밋·승인. PA2.2 L: 결함 DEF ID 없음, 출시 빌드 `.dirty`. 한국어 머리 주석 163/163 |
| 통합 | L | L | L | P | P | P | N | N | N | **1** | IL2CPP 빌드·산출물 검사·호스트+봇 통합 측정. PA1.1 L: 2PC·Steam 릴레이 통합 미수행. CI 없음 |
| 테스트 | L | L | P | P | P | L | N | N | N | **1** | 자동 시험 689건, 측정 원본 데이터 git 관리. PA1.1 L: TC·시험 계획 문서는 Check에서 작성 예정, 수동 확인 11건 남음. PA2.2 P: 시험 문서 산출물 없음 |
| 형상 관리 | F | L | L | L | P | P | N | N | N | **2** | git·LFS·Conventional Commits·꼬리말. PA2.1·2.2 L: `main` 직접 커밋, `Closes:` 0건, 제목 길이 초과 16%, `.meta`만 남은 항목 3개 |
| 품질 보증 | L | P | P | P | P | N | N | N | N | **1** | 아키텍처·UI 규칙 자동 검사는 강점. 독립 QA 계획·기록은 이번이 처음 |
| 프로젝트 관리 | F | F | F | L | L | P | N | N | N | **3** | PROJECT_STATUS(상태·결정 118행·위험·측정), 단계별 승인, PDCA·12207 대응을 CLAUDE.md로 정의. PA4.1 P: 측정 항목은 있으나 기준선·관리 한계 없음. 형식의 「현재 브랜치·최근 버전 태그」 줄 빠짐 |

---

## 6. 코드 리뷰 결과

### 6.1 지적 사항

| REV ID | 심각도 | 파일:줄번호 | 문제 | 수정 방향 | 관련 품질 특성 (ISO/IEC 25010) |
|---|---|---|---|---|---|
| REV-001 | 🔴 반드시 수정 | `Application/Server/ServerWorld.cs:168-173`, `Adapters/Net/Avatar/PlayerAvatar.cs:117-140` | 허용 이동량 `dt`를 **클라이언트가 보낸 `pose.TimeMs` 차이**로 계산함(상한 0.5초). `ServerSubmitPose`에는 수신 빈도 제한이 없음. 조작된 클라이언트가 `TimeMs`를 +500 ms씩 올린 자세를 연달아 보내면 자세마다 `7×1.25×0.5+0.5 ≈ 4.9 m`를 인정받아 속도 검사가 무력화됨(30 Hz면 약 146 m/s, 더 자주 보내면 그 이상). SDD R-07이 받아들인 범위(「속도 한도 안」 조작)를 넘고, NFR-030의 「호스트 속도 검사」와 FR-007 거리 검증의 기준 위치가 함께 무너짐. 이 공격을 다루는 시험이 없음(`ServerAndSessionTests.cs`의 이동 시험 6건은 정상 경로만) | `dt`를 `min(클라이언트 차이, 호스트 수신 시각 차이 + 지터 여유)`로 제한하거나, 호스트 시각 기준 「이동 허용량 버킷」을 둠. 연결별 자세 수신 빈도 상한(예: `AvatarSendHz × 1.5`)을 두고 넘으면 버림. 시각 부풀리기·고빈도 전송 시험 추가. SDD 3.11.12와 NFR-030 보강. security-auditor 결과와 교차 확인 | 보안성(무결성), 기능 적합성(정확성) |
| REV-002 | 🔴 반드시 수정 | `Application/Protocol/Messages.cs:312-336`, `src/Assets/_Game/Localization/strings.ko.txt:46-48`, `docs/STORYBOARD.md:739` | FR-003(M1) 수용 기준은 버전 불일치 때 「게임 버전 불일치」와 **참가자·호스트 두 버전**을 함께 보이라고 함(SRS v0.11). `JoinRejected`에는 사유 코드만 있고, 문구에도 버전 자리가 없으며, 클라이언트가 로비 `kh_build`를 읽어 표시하는 경로도 없음. STORYBOARD SCR-012 문구도 옛 내용 그대로 | `JoinRejected`의 `VersionMismatch`에 호스트 `GameVersion.Full`(64바이트 상한, `TryParse` 통과 값만 표시)을 싣거나, Steam 경로에서는 로비 `kh_build`를 씀. 표시는 SDD 3.7.12 (6) 형식(`ToDisplayString`). SDD 3.7.2·STORYBOARD SCR-012 갱신(architect·ui-ux-designer), 왕복·표시 시험 추가 | 기능 적합성(완전성), 사용성(오류 대처) |
| REV-003 | 🟡 수정 권장 | `Application/Server/ServerWorld.cs:160-165`, `:421` | 참가 직후 `AvatarMoveState.HasLast`가 false라 **첫 자세는 월드 안 어디든 그대로 받아들임**. 조작된 클라이언트가 스폰 위치를 무시하고 원하는 곳으로 순간 이동할 수 있음. 시험 `FirstPose_IsAcceptedAsBaseline`이 이 동작을 정상으로 고정하고 있음 | `AddPlayer`(또는 JoinComplete 시점)에 기준 위치를 스폰 위치로 넣고, 첫 자세는 스폰 위치에서 여유 거리 안일 때만 받아들임(시각 기준만 첫 자세로 정함) | 보안성(무결성) |
| REV-004 | 🟡 수정 권장 | `Adapters/Net/Gateway/ServerCommandGateway.cs:140`, `Application/Server/ServerWorld.cs:477`, `Application/Commands/CommandPipeline.cs:214` | 명령 대기열(256)은 모든 플레이어가 함께 쓰는데, 빈도 제한(토큰 버킷)은 **꺼낸 뒤** `Dispatch`에서 검사함. 참가자 한 명이 명령을 쏟아 대기열을 채우면 다른 참가자의 정상 명령이 `RateLimited`로 버려짐. SDD 3.11.13 「버킷을 통과한 명령은 대기열에 들어가고」와 실제 순서가 다름 | 게이트웨이에서 `Enqueue` 전에 `RequestGuard.CheckRate`를 부르거나, 플레이어별 대기열 몫(예: `MaxQueuedCommands / MaxPlayers`)을 둠. 처리 순서를 SDD와 맞추고 「한 명의 폭주가 다른 사람을 막지 않음」 시험 추가 | 보안성(가용성), 신뢰성(결함 허용) |
| REV-005 | 🟡 수정 권장 | `Bootstrap/Runtime/GameFlow.cs:214`, `:713-718`, `Adapters/Presentation/World/RemoteAvatarViews.cs:51`, `:52`, `:73`, `Adapters/Presentation/World/CreatureViews.cs:111` | 매 프레임 메모리 할당: (1) `BuildStatus()` 문자열 합치기와 `Text.Get(key, params object[])`(배열·박싱·서식)가 **출시 빌드에서도** 매 프레임 실행됨(`ThemedText`가 같은 글자면 다시 그리지 않는 것과 별개로 문자열은 새로 만들어짐). (2) `new HashSet<PlayerId>()`를 매 프레임 만듦. (3) `IReadOnlyDictionary`·`IEnumerable` 속성을 `foreach`로 돌아 열거자가 박싱됨 | (1) 명단·슬롯 버전이 바뀔 때만 상태 줄을 만들거나 개발 빌드에서만 표시. (2) `HashSet`을 필드로 두고 `Clear()`. (3) 구체 `Dictionary`를 돌려주는 내부 접근자나 `struct` 열거자 사용. Profiler GC Alloc으로 0 B/프레임 확인 | 성능 효율성(자원 효율성) |
| REV-006 | 🟡 수정 권장 | `Adapters/Presentation/World/RemoteAvatarViews.cs:69` | 표현 어댑터가 `ClientWorldMirror.RemoveRemoteAvatar`로 **애플리케이션 상태를 바꿈**. SDD 3.4.1 규칙 4는 「Net 어댑터가 사본에 쓰고 표현은 읽음」. 또 뷰가 만들어지지 않은 채 나간 플레이어의 보간 트랙은 지워지지 않고 남음 | 명단에서 빠질 때 트랙을 지우는 일을 `ClientWorldMirror`(명단 갱신 처리)나 Net 수신기로 옮기고, 뷰는 읽기만 함 | 유지보수성(모듈성), 신뢰성 |
| REV-007 | 🟡 수정 권장 | `Adapters/Net/Transport/FishNetPacketTransport.cs:174`, `Application/Server/ServerWorld.cs:567`, `Adapters/Net/Avatar/PlayerAvatar.cs:128` | 신뢰할 수 없는 입력마다 경고 로그 한 줄(문자열 합치기 + 파일 쓰기). 디코드 실패·`move_rejected`·`pose_decode_failed`에 빈도 제한이 없어, 조작된 클라이언트가 호스트 로그와 프레임 시간을 소모시킬 수 있음. 누적 디코드 실패가 많아도 연결을 끊지 않음 | 연결별로 초당 한 번 요약해 기록(누적 수만 증가), 디코드 실패가 기준을 넘으면 연결 종료. NFR-040·NFR-041에 근거 추가 검토 | 보안성(가용성), 성능 효율성 |
| REV-008 | 🟡 수정 권장 | `Bootstrap/Runtime/GameFlow.cs` (721줄) | 조립 루트(Bootstrap)에 화면 흐름 상태 기계, 세션 생명 주기, 월드 뷰 준비, 봇·참가 시간 기록·상태 로그 같은 개발 도구가 한 클래스로 모여 있음. M2에서 메인 메뉴·옵션·채팅이 들어오면 더 커질 위험 | 월드 뷰 준비(`SetUpWorldView`), 개발 도구(봇·상태 로그·참가 시간 보고), 세션 시작·종료를 별도 클래스로 나누고, 흐름 판단은 Application의 UI 상태 모델로 옮기는 방향을 SDD에 정리 | 유지보수성(분석성·수정 용이성) |
| REV-009 | 🟢 참고 (확인 필요) | `Adapters/Net/Replication/ReplicationPublisher.cs:162`, `:173`, `Adapters/Net/Replication/ClientReplicationReceiver.cs:95-101`, `:116-120` | 스냅샷 조각 전송 실패를 로그만 남기고 그 연결을 「준비 완료」로 바꾸고 `JoinComplete`를 보냄. 클라이언트도 델타 적용 실패·보관 한도 초과를 로그만 남김. 어느 경우든 상태가 어긋난 채 게임이 이어질 수 있음(실제 발생 조건이 연결 끊김 직전뿐인지 확인 필요) | 실패하면 동기화를 중단하고 사유와 함께 연결을 끝내거나 다시 동기화. 실패 수를 성능 CSV 지표로 남김 | 신뢰성(성숙성), 기능 적합성(정확성) |
| REV-010 | 🟢 참고 | `Bootstrap/Runtime/GameFlow.cs:614-618` | `SetUpWorldView`가 지형·자원 뷰를 찾지 못하면 오류 로그만 남기고 `_inGame = true`로 게임에 들어감(지형 없이 떨어짐). 컨트롤러가 없을 때(554행 아래)는 로비로 돌아가는 것과 다름 | 실패를 돌려받아 컨트롤러 없음과 같은 경로(`OnLeaveRequested`)로 처리 | 신뢰성(결함 허용) |
| REV-011 | 🟢 참고 | `Bootstrap/Runtime/GameFlow.cs:573` | `UnityEngine.Object`(`RemoteAvatarViews`)에 `?.` 사용. 바로 위에서 찾은 값이라 지금은 위험이 낮지만 프로젝트 규칙 위반 | `if (_remoteViews != null)` 사용, 없으면 `CreatureViews`처럼 경고 | 유지보수성 |
| REV-012 | 🟢 참고 | `Bootstrap/Runtime/SessionRuntime.cs:276`, `Adapters/Net/Replication/ReplicationPublisher.cs:58`, `SessionRuntime.cs:186-204` | `_server.Ticked += World.Tick`, `_world.TickEnded += OnServerTickEnd` 구독을 풀지 않고, `Dispose`에서 `ReplicationPublisher`를 정리하지 않음. 지금은 모두 함께 버려져 누수는 없음 | `ReplicationPublisher.Dispose()`를 두고 `SessionRuntime.Dispose`에서 구독 해제 | 유지보수성, 신뢰성 |
| REV-013 | 🟢 참고 | `Adapters/Net/Avatar/PlayerAvatar.cs:112` | 자세 전송마다 `_writer.ToArray()`로 `byte[]`를 새로 만듦(30 Hz, 연결 수만큼) | FishNet `ArraySegment<byte>` 매개변수나 재사용 버퍼 검토. R-43 개선 때 함께 측정 | 성능 효율성 |
| REV-014 | 🟢 참고 | `src/Assets/_Game/UI/Icons.meta`, `src/Assets/ThirdParty/FishNet/CodeGenerating/cecil-0.11.4/Mono.Cecil.sln.meta`, `.../Tugboat/LiteNetLib/LiteNetLib.csproj.meta` | git에 `.meta`만 있고 원본이 없음(빈 폴더, `.gitignore`가 `*.sln`·`*.csproj`를 제외). 다른 PC에서 Unity가 `.meta`를 지우며 변경이 생김 | 빈 폴더 `.meta`는 지우거나 폴더에 `.gitkeep`을 둠. 서드파티 두 개는 `.meta` 삭제 커밋 | 이식성(설치성), 유지보수성 |
| REV-015 | 🟢 참고 | `Adapters/Net/Avatar/PlayerAvatar.cs:32` | `AvatarServices.Current`가 정적으로 바꿀 수 있는 전역 서비스 찾기. FishNet이 `NetworkBehaviour`를 만들어 생성자 주입이 어려운 사정은 이해되나, 시험 사이에 값이 남거나 호스트·클라이언트가 한 프로세스에 둘일 때 섞일 수 있음 | SDD에 예외로 명시하고, 시험 `TearDown`에서 초기화하는지 확인. 필요하면 `NetworkManager`별 등록부로 바꿈 | 유지보수성(시험 용이성) |

**심각도별 개수**: 🔴 2 · 🟡 6 · 🟢 7 (총 15)

### 6.2 알려진 위험과 겹치는 관찰 (새 지적 아님)

| 관찰 | 위치 | 이미 등록된 위험 |
|---|---|---|
| `CreatureSnapshot` 배열을 틱마다 연결별로 새로 할당 | `Adapters/Net/Replication/CreatureReplicator.cs:238` | SDD R-43, PROJECT_STATUS |
| 흐름장 재계산이 워커 예산 초과, 틱 최악 5.57 ms(`AiApply`) | `Infrastructure/AiJobs/AiJobsBackend.cs` | SDD R-41 |
| 보행 격자가 월드 중심 600 m만 | `ServerWorld.SpawnTestCreatures` → `PrepareWalkGrid` | SDD R-42 |
| 자원 5,000개 일괄 변경 때 순간 FPS 하락 | `ResourceViewStreamer` | SDD R-44 |
| 호스트 월드 동기 생성으로 시작 때 약 0.6초 멈춤 | `SessionRuntime.cs` `BuildServerSide`(`GetOrGenerate`) | SDD R-45 |
| 참가자 월드 생성 작업 취소 불가 | `Application/Session/ClientSession.cs:288` | PROJECT_STATUS 위험 목록 |
| 「속도 한도 안의」 위치 조작은 막지 못함 | `AvatarMoveValidator` | SDD R-07 (단, 한도 자체를 넘는 우회는 REV-001로 따로 지적) |

### 6.3 잘된 점 (근거)

- **의존 규칙**: asmdef 14개 참조가 SDD 3.4.2 표와 일치. `Game.Domain`·`Game.WorldGen`·`Game.Application`·`Game.Adapters.Persistence`는 `noEngineReferences: true`, 이 폴더들에 `using UnityEngine` 0건. 실제 구현체 생성은 `Bootstrap`(`SessionRuntime.BuildServerSide`)에 모여 있음.
- **신뢰할 수 없는 입력 처리**: `JoinRequest` 디코더가 `protocolVer`를 먼저 읽고 문자열·배열 길이 상한을 확인함(`Messages.cs:214-239`). 게이트웨이가 예외를 연결 처리 루프 밖으로 내보내지 않음(`ServerCommandGateway.cs` 각 처리기). 플레이어 ID를 요청 값이 아니라 전송의 원격 ID로 정함(D-14, `JoinHandshake.cs` `PlayerIdentityPolicy`).
- **참가 판정**: 차단 → 버전·콘텐츠·생성기 → 로비 멤버 → 참가 조건 → 인원 순서가 SDD와 같고, 사유가 하나로 정해짐(`SessionRules.cs:131-180`).
- **네이티브 메모리**: `AiJobsBackend`가 잡을 `Complete()`한 뒤 `NativeArray`를 해제하고, 서버 틱이 멈춘 뒤 해제하도록 순서를 지킴(`SessionRuntime.cs:197`).
- **Unity 규칙**: `[RequireComponent(typeof(CharacterController))]`, 부트의 `[SerializeField]` 연결 누락을 시작할 때 모두 검사(`GameBootstrap.cs:245`, `:345`, `:420`). Steam `Callback`·`CallResult` 해제(`SteamLobbyService.cs:157-163`). 개발 옵션은 `BuildFlags.IsDevelopment`로 막음.
- **한국어 주석**: 163개 파일 모두 머리 주석(파일·역할·레이어·관련 ID)이 있음. 표본에서 공개 API XML 주석과 `[FR-xxx]` 인라인 주석이 코드와 맞음.

---

## 7. 프로세스 개선 권고

| # | 권고 | 대상 영역 | 담당 제안 |
|---|---|---|---|
| 1 | 결함에 **DEF ID**를 붙이고 수정 커밋에 `Closes: DEF-xxx`를 씀. Do 중 결함 5건도 DEF-001~005로 소급 등록 | CM, VV, 측정 | 총괄, test-engineer |
| 2 | **요구사항 변경 영향 표**: SRS 변경 이력 행마다 「영향 받는 DES·SCR·코드·TC」 칸을 두고, 반영 여부를 다음 단계 시작 때 확인(FR-003 같은 전파 누락 방지) | RDM, PQA | requirements-analyst, 총괄 |
| 3 | 출시·측정 빌드는 **커밋된 깨끗한 트리**(가능하면 태그)에서만 만듦. 빌드 스크립트가 `.dirty`이면 출시 빌드를 거부 | CM, PI | developer, devops-engineer |
| 4 | Check에서 **TC ID와 FR↔TC 대응표**를 `docs/test/`에 파일로 만듦(지금은 developer 보고 요약에만 있음) | VV, RDM | test-engineer |
| 5 | **적대적 클라이언트 시험 묶음**: 시각 부풀리기, 자세·명령 폭주, 첫 자세 순간 이동, 디코드 실패 폭주(REV-001·003·004·007) | VV, 보안 | test-engineer, security-auditor |
| 6 | 브랜치 전략 이탈(`main` 직접 커밋 17개)을 결정 기록에 사유와 함께 남기고, 사이클 2부터는 Plan부터 사이클 브랜치에서 진행 | CM | 총괄 |
| 7 | 커밋 제목 50자 검사(`commit-msg` 훅 또는 커밋 전 확인 목록). 현재 초과 15/91 | CM | 총괄 |
| 8 | EditMode 시험을 push마다 자동 실행하는 CI(GameCI) 검토 | PI, VV | devops-engineer |
| 9 | 측정 보고서가 시드 값을 기록하게 하고(NFR-021), SRS 2.6.1 M1 행의 NFR 범위를 NFR-021과 맞춤(NFR-012·015는 M6) | RDM, 측정 | developer, requirements-analyst |
| 10 | PROJECT_STATUS 머리에 「현재 브랜치」「최근 버전 태그」 줄을 정해진 형식대로 추가하고, 위험 목록의 「해소」 항목을 별도 절로 옮김 | MC | 총괄 |
| 11 | 수준 4를 목표로 한다면 묶음별 결함 밀도, 리뷰 지적 수, 시험 통과율의 **기준선**을 정해 사이클 2부터 비교 | 측정 (CMMI 4, SPICE PA4.1) | 총괄 |

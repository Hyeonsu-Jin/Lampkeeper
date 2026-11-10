# 프로젝트 진행 상황
- 현재 사이클: 1
- 현재 단계: Act 회귀 시험 완료 (승인 IMP 12건 모두 검증 완료, EditMode 705/705·PlayMode 26/26 통과, 미해결 결함 0)
- 다음에 할 일: **[2026-11-11 Act (6) 회귀 시험 완료]** test-engineer 회귀 판정 12/12 검증 완료(IMP-013은 SRS 머리말 버전 DEF-012 수정 뒤 재확인), TC-067 추가(TC 68), Do 결함 소급 DEF-006~011 등록, 시험 대응표 SRS v0.13 기준 갱신. 커버리지 계측 중 실패 1건은 재현되지 않아 이름 미확인(계측이 도메인 재로드 때 꺼지는 문제, 다음 측정 전 방식 변경). **다음은 사이클 병합(`cycle/1-mvp` → `main`)·태그 v0.1.0 제안**, 이어서 사이클 2 시작 여부 확인. 남은 수동 시험: IMP-014 MP-01~08, MP-09(비멤버 연결 실측). `tools/git/`의 untracked LFS 훅 4개(post-checkout·post-commit·post-merge·pre-push) 커밋 여부 사용자 결정 대기. 주의: 이 PC는 `core.hooksPath`가 비어 있어 IMP-011 commit-msg 훅이 아직 꺼져 있음. TMP 폰트 `Pretendard-*Dynamic SDF.asset` 2개는 커밋 제외(R-46), 출시 빌드 전 처리 결정 필요. 다음 사이클 후보: NFR-030 지형 높이 Y 검사, Server.Total p99를 틱 기준으로(현재 프레임 기준), CI·성능 패키지·커버리지 하한(보류).

## 프로젝트 개요
- Unity 3D 생존 게임 (SRP 기반 실사 그래픽)
- 사용자가 제시한 기능: 플레이어(이동·달리기, 도구별 공격, 체력·허기·갈증·스태미나·피로도, 채집, 도구 제작, 건축, 농사), 나무(도끼 벌목·쓰러짐 애니메이션·종류별 드롭), 돌/광석(곡괭이 채광·파괴 애니메이션·광석별 드롭), 작물(심기·물 주기·수확·작물별 드롭)
- 사용자 요청: 핵심 재미 요소 추천, 상세 기능은 대화로 구체화

## 단계별 상태
| 사이클 | 단계 | 상태 (대기/진행 중/완료/승인됨) | 날짜 | 산출물 |
|---|---|---|---|---|
| 1 | Plan | 승인됨 | 2026-11-03 | docs/SRS.md v0.8 (승인), docs/SDD.md v0.6 (승인), docs/DESIGN_PROPOSAL.md, docs/wireframes/ (hud, defense, cooking), docs/STORYBOARD.md v1.3 |
| 1 | Do | 승인됨 (M1-01~M1-13) | 2026-11-10 | src/ (M1 기술 검증: Steam·FishNet 접속, 1인칭 이동, 자원 2만 개 동기화·중도 참가, 시험 AI 60체·생물 복제, 봇·측정 스크립트, 출시 IL2CPP 빌드), docs/test/perf/M1_tech_verification.md, tools/perf/RunM1Perf.ps1, SRS v0.12·SDD v0.14·STORYBOARD v1.8 |
| 1 | Check | 완료 | 2026-11-10 | docs/check/REVIEW_REPORT.md(REV 15), docs/check/SECURITY_REPORT.md(SEC 10), docs/test/TEST_PLAN·TEST_SPEC·TEST_LOG·TEST_COMPLETION_REPORT.md(TC 66, DEF 5), docs/IMPROVEMENTREPORT.md(IMP 18) |
| 1 | Act | 완료 (IMP 12건 처리·회귀 검증 완료, 병합 대기) | 2026-11-11 | src/ 개선, docs/IMPROVEMENTREPORT.md, SRS v0.13·SDD v0.17·STORYBOARD v1.9, docs/test/perf/M1_tech_verification.md 8장, docs/test/ 회귀 결과(TC 68, DEF 12) |

## 결정 기록
| 날짜 | 결정 내용 (채택한 SUG, 확정한 디자인, Act 대상 IMP 등) | 근거 |
|---|---|---|
| 2026-11-02 | Unity 3D 생존 게임으로 프로젝트 시작, SRP 기반 실사 그래픽 목표 | 사용자 요청 |
| 2026-11-02 | 렌더 파이프라인: HDRP | 실사 그래픽 목표, 커스텀 SRP 작성 비용 회피 |
| 2026-11-02 | 시점: 1인칭 | 사용자 결정 |
| 2026-11-02 | 엔진·대상: Unity 6, 중상급 게임용 PC | 사용자 결정 |
| 2026-11-02 | 동물과 적 모두 포함 | 사용자 결정 |
| 2026-11-02 | 멀티플레이 기준으로 개발 | 사용자 결정 |
| 2026-11-02 | 개발 항목을 마일스톤으로 나눠 마일스톤 단위로 달성 | 사용자 결정 |
| 2026-11-02 | git init은 Do 단계(코딩) 시작 전에 수행 | 사용자 결정 |
| 2026-11-02 | 핵심 재미 요소: B(탐험 범위 확장·단계형 지역), D(요리 버프 조합) 채택. A(계절 순환), C(밤 습격)는 미채택, 대체 후보 추천 요청 | 사용자 결정 |
| 2026-11-02 | 멀티플레이: 최대 8인 협동, PvP 없음, 호스트 방식, Steam 연동(Steamworks) | 사용자 결정 |
| 2026-11-02 | 월드 저장: 월드와 플레이어 진행(인벤토리·상태·위치)을 호스트가 저장, 모든 플레이어 SteamID 기준, 자동 저장+백업, Steam Cloud 선택, 월드 파일 단독 이전 가능 | Valheim·Enshrouded·Palworld·Sons of the Forest 사례 비교 후 사용자 채택 |
| 2026-11-02 | 핵심 재미 요소 G(숙련도) 채택: 행동마다 경험치 → 레벨업 시 스탯 포인트 → 스킬 노드 선택 | 사용자 결정 |
| 2026-11-02 | E(지역 수호자·협동 원정)는 Valheim과 너무 비슷해 보류, 차별화된 대안 요청 | 사용자 결정 |
| 2026-11-02 | 네트워크: FishNet + FishySteamworks + Steamworks.NET 권장안 제시 (출시 사례·안전성·효율성 검토), M1에 8인 동기화 기술 검증 포함 | 총괄 조사 결과 |
| 2026-11-02 | 핵심 진행 방식: 잠식 정화와 영역 확장 채택. 정화탑 건설·활성화 시 적 웨이브, 마지막 웨이브에 보스 등장. 정화 전 지역은 밤마다 적 습격, 정화된 지역은 밤에도 적이 스폰되지 않음 | 사용자 결정 (E 대체, Valheim 차별화) |
| 2026-11-02 | 환경 위협: 혹한·독기·완전한 어둠 채택 (고산 저산소 제외). 전용 장비 제작 또는 요리 버프로 대응 | 사용자 결정 |
| 2026-11-02 | 요리(D) 변경: 조합 발견 방식이 아니라 정해진 요리법만 가능. 요리 솥 주변에 도마·항아리 등 보조 기구를 배치하면 만들 수 있는 요리 종류가 늘어남 | 사용자 결정 |
| 2026-11-02 | 살아 있는 생태계 제외 | 사용자 결정 |
| 2026-11-02 | 세계의 기록 해독 보류: 스토리·세계관을 먼저 정한 뒤 재검토 | 사용자 결정 |
| 2026-11-02 | 요리 기구 방식: 단계 상승이 아닌 기구별 조리 방식 해금 (도마=손질, 항아리=발효·절임, 훈연대=훈제, 절구=가루·약재). 요리법마다 필요한 기구 조합이 정해짐 | 사용자 채택 (Valheim 솥 업그레이드와 차별화) |
| 2026-11-02 | 시작 지점에 작은 정화 구역 제공, 첫 정화탑은 튜토리얼 수준의 쉬운 웨이브 | 사용자 채택 |
| 2026-11-02 | 정화탑 방어 실패 시 탑 파괴와 재료 일부 손실, 재건설 후 재도전 가능 | 사용자 채택 |
| 2026-11-02 | 정화된 지역은 다시 잠식되지 않음 | 사용자 채택 |
| 2026-11-02 | 정화 구역 밖의 적은 정화 구역 안으로 들어오지 않음 | 사용자 채택 |
| 2026-11-02 | 웨이브 규모와 보스 체력은 접속 인원(1~8명)에 맞춰 조정 | 사용자 채택 |
| 2026-11-02 | 환경 위협 대응: 장비는 지속적이지만 제작 비용 높음, 요리 버프는 저렴하지만 시간 제한 | 사용자 채택 |
| 2026-11-02 | 진행 순서: 세계관·스토리 확정 후 SRS·SDD 등 문서 작성 | 사용자 결정 |
| 2026-11-02 | 장막의 정체: "실패한 태양 붙잡기" (옛 문명이 태양의 힘을 붙잡으려 만든 장치의 폭주). 스토리 흐름·지역 5곳·적·보스 구성 확정 | 사용자 결정 |
| 2026-11-02 | C-01 네트워크: FishNet + FishySteamworks + Steamworks.NET 확정 | 사용자 결정 |
| 2026-11-02 | C-02 기준 사양: 권장 RTX 3060급, 6코어 CPU, RAM 16GB, SSD, 1080p | 사용자 결정 |
| 2026-11-02 | C-35 월드: 고정 맵, 약 3km x 3km, 자원 오브젝트 약 2만 개 | 사용자 결정 (아래 혼합형으로 변경됨) |
| 2026-11-02 | C-35 변경: 혼합형 월드 생성. 지역 순서·큰 배치(숲 중앙 → 늪 → 고원 → 협곡)와 장막의 근원, 시작 지점은 고정. 나머지 네 지역 내부(지형, 강·호수, 자원, 일반 등광탑 후보 자리)는 시드로 생성. 새 월드 생성 시 시드 입력/무작위, 참가자는 시드로 직접 생성 후 바뀐 상태만 동기화 | 사용자 결정 (스토리 순서 유지 + 재플레이성 + 네트워크 부담 감소) |
| 2026-11-02 | C-03 성능 목표 제안값 수용 (1인 평균 60 FPS, 8인 호스트 평균 50 FPS, 참가자 수신 512kbps 이하, 호스트 송신 4Mbps 이하, 중도 참가 60초 이내) | 사용자 결정 |
| 2026-11-02 | C-31 마일스톤 조정 6건 모두 수용 (섭취 M3, 기본 건축·횃불 M4~M5, 경험치 이벤트 M3부터, M6 수직 슬라이스 8인 테스트, M9 a/b/c 분할, 저장 형식 버전 필드) | 사용자 결정 |
| 2026-11-02 | 성능 목표 상향: 8인 세션에서 호스트·참가자 모두 평균 60 FPS 이상 (권장 사양 기준). 기존 "8인 호스트 평균 50 FPS" 대체 | 사용자 결정 |
| 2026-11-02 | 렌더 파이프라인: HDRP 유지 + 위험 대응 (Unity 6 LTS 고정, 렌더링 코드 어댑터 격리, M6 수직 슬라이스에서 재평가) | 사용자 결정 (HDRP 유지보수 모드 전환 소식 검토 후) |
| 2026-11-02 | 생성기 버전: 개발 중(0.x)은 보존하지 않고 첫 공개 빌드 생성기를 V1로 동결 | 사용자 결정 |
| 2026-11-02 | 경험치 이벤트 소급 저장 안 함 | 사용자 결정 |
| 2026-11-02 | 인원 배율은 적 수와 보스 체력에만 적용, 일반 적 체력에는 적용 안 함 (FR-156 예시 수정) | 사용자 결정 |
| 2026-11-02 | 세션 비밀번호는 저장하지 않고 세션마다 입력 | 사용자 결정 |
| 2026-11-02 | 동기화 일관성(FR-008)은 관심 범위(300m) 안에서 보장 | 사용자 결정 |
| 2026-11-02 | 클라이언트 이동 위치 조작은 속도·범위 검사로 대응, Check 단계 보안 점검 대상 | 사용자 결정 |
| 2026-11-02 | C-52 성능 측정 기준: 프리셋 "높음", 업스케일링 허용(DLSS·FSR 품질 모드), 8인 1% Low 45 FPS | 사용자 결정 |
| 2026-11-02 | C-51 M1 부가 기능을 M2로 이동, M1은 기술 검증에 집중 | 사용자 결정 |
| 2026-11-02 | SRS v0.3·SDD v0.1을 위 결정 반영 후 승인 (반영 완료 시 승인 처리) | 사용자 결정 |
| 2026-11-02 | 디자인 확정: D-01 A 불씨, D-02 A 상시 표시형, D-03 A 계층형, D-04 A 최소형, D-05 A 격자 한 화면, D-06 B 독립 레시피북, D-07 A 건축 도구 모드형, D-08 A 상단 방어전 바, D-09 A 방식 탭+목록, D-10 B 조준 패널+근처 상태 아이콘, D-11 B 열형 트리, D-12 B 반투명 창 지도, D-13 A 중앙 패널형, D-14 B 분리형, D-15 B 한 단계 카드 | 사용자 결정 (D-03·D-06·D-09·D-12는 추천안과 다르게 선택) |
| 2026-11-02 | 디자인 변경 (와이어프레임 확인 후): D-09 요리 솥 → B 솥 도식+목록, D-08 등광탑 방어전 → A 유지, D-02 HUD → C 장치형(손목 계기)을 기본으로 하고 "손목 계기 올림" 시 A의 목표 한 줄도 표시. 접근성을 위해 옵션에서 A 상시 표시형(일반 HUD)으로 전환 가능 | 사용자 결정 |
| 2026-11-02 | 손목 계기: 화면 UI(원근 기울인 평면)로 시작, M6 수직 슬라이스에서 3D 손목 소품으로 올릴지 결정 / C-58 올림 방식은 토글 기본 + 누르는 동안 옵션 / C-59 올린 동안 모든 행동 가능 / 등불 이름표 체력은 살아 있는 4단계(76%↑, 51~75%, 26~50%, 25%↓) + 꺼짐(쓰러짐) | 사용자 결정 (C-59는 총괄 추천과 다르게 선택) |
| 2026-11-02 | Plan 완료: 위 결정 반영 후 사용자가 한 번 더 검토하고 승인 | 사용자 결정 |
| 2026-11-03 | M4 제작 추가: 도구·무기·대응 장비는 제작대에서 제작(제작대 상호작용 시 제작 UI 팝업), 제작대·횃불 등 기본품은 손 제작. 레시피북 키는 열람과 손 제작만(제작대 필요 항목은 "제작대 필요" 표시). C-11 해소 | 사용자 결정 |
| 2026-11-03 | Plan 단계 완료 승인 (SRS v0.8, SDD v0.6, STORYBOARD v1.3). git init(main 브랜치)과 git lfs install 진행 | 사용자 결정 |
| 2026-11-03 | 형상 관리 범위: docs/는 커밋, CLAUDE.md와 .claude/agents/는 git 제외 (.gitignore에서 /docs/ 줄 삭제) | 사용자 결정 |
| 2026-11-03 | 원격 저장소: origin = https://github.com/Hyeonsu-Jin/Lampkeeper (사용자가 추가). Plan 산출물을 Conventional Commits 규칙으로 6개 커밋 (4dcd38b chore, e6e12f8 srs, eecb0ff sdd, 899db6b design, f2d5649 storyboard, 4404fe1 status. 2026-11-03 사용자 요청으로 Co-Authored-By 꼬리말을 지우고 강제 push해 해시가 바뀜) | 사용자 결정 |
| 2026-11-03 | Unity 에디터 조작 MCP 도구: Unity 공식 CLI 채택 (커뮤니티 MCP for Unity 대신). winget으로 Unity.CLI 1.0.0-beta.15 설치, 프로젝트 루트 .mcp.json에 `unity-editor-mcp`(unity mcp --project-path src) 등록. .mcp.json은 PC 전용 절대 경로가 있어 git 제외(.gitignore) | 사용자 결정 |
| 2026-11-03 | M1 착수 전 결정: 기본 키 할당표 채택(WASD/Shift, E 상호작용, 마우스 왼쪽 공격, 1~8·휠 퀵슬롯, Tab 인벤토리, C 레시피북, B 건축, M 지도, K 스킬, Q 손목 계기, 가운데 버튼 핑, Enter 채팅, Esc 메뉴 / Xbox 패드 대응 배치) · C-28 Xbox 계열 아이콘 먼저, PlayStation은 M2 · Steam 실행 필수(오프라인 1인 플레이 없음) · C-48 첫 실행 언어는 Steam 언어(한국어면 한국어, 그 밖 영어) · C-46 차단은 호스트의 모든 월드 · C-43 오류 보고는 로컬 파일만 · C-33 설정값 후보로 시작해 테스트로 조정 · C-49 메모리 상한 프로세스 10GB / VRAM 7.5GB | 사용자 결정 (모두 총괄 추천안) |
| 2026-11-03 | M1 개발 환경: Unity 6000.3 LTS 최신 패치 새로 설치 · 측정은 이 PC + 봇 7개로 먼저, 다른 CPU(Intel/AMD) 결정론 검증과 8대 실측은 M6 수직 슬라이스에서 · Unity CLI 충돌 보고·사용 신호 끔(UNITY_NO_CRASH_REPORT, UNITY_NO_CLI_INVOKED_TELEMETRY) | 사용자 결정 |
| 2026-11-03 | M1 묶음 1 후속: Unity Pipeline 패키지 허용 + 안전장치(ENABLE_RUNTIME_PIPELINE* 기호 사용 금지, Game.* 참조 금지, 출시 빌드에 런타임 서버 미포함을 자동 시험) · Smart Merge는 실제 Unity 파일로 한 번 검증 후 유지/변경 결정 · Unity 생성 파일(.vsconfig, DefaultPrefabObjects.asset, UnityDefaultRuntimeTheme.tss)은 커밋 · 결정 반영 후 커밋·push | 사용자 결정 (모두 총괄 추천안) |
| 2026-11-03 | 후속 결과: com.unity.pipeline 0.9.1-exp.1 재설치 + 안전장치(Attributes만 예외, ENABLE_RUNTIME_PIPELINE* 기호 금지 시험, 출시 빌드 산출물 검사 BuildArtifactInspector, 출시 IL2CPP 빌드에서 Pipeline 어셈블리 0건 확인) · Smart Merge 드라이버는 실제 Boot.unity로도 실패(확장자 없는 임시 파일 원인 확정) → 저장소 로컬 드라이버 제거, git mergetool 방식(unityyamlmerge)으로 등록, 첫 실제 충돌 때 확인 | 사용자 결정에 따른 조치 |
| 2026-11-03 | Unity 프로젝트 위치는 src/ 유지 (SDD D-01). .gitignore의 루트 기준 Unity 규칙 21줄을 /src/ 기준으로 수정, 샘플 경로로 제외·커밋·LFS 동작 확인 | 사용자 결정 (A안) |
| 2026-11-03 | 용광로: 채집한 광석은 용광로에서 가공, 연료와 광석을 넣어야 함. 연료 나무 2개 = 광석 1개, 석탄 1개 = 광석 4개. 석탄은 석탄 광맥 채광과 숯(나무를 구워 만듦) 둘 다. 가공은 시간이 걸리고 플레이어가 자리를 떠나도 연료가 있는 동안 계속(호스트 계산) | 사용자 결정 |
| 2026-11-02 | 디자인 확인: I-01 환경 위협은 상태 칩+버프 남은 시간(누적 게이지 없음), I-03 공개 방 목록에 예약 자리 표시(예: 7/8, 예약 1), I-04 접근성 기반(글자 배율·색 토큰·모양 구분)을 M1 UI부터 적용, I-07 메뉴를 열어도 게임은 멈추지 않음, I-12 다크 단일 테마 | 사용자 결정 |
| 2026-11-02 | SUG 채택 27건: 001, 002, 003, 004, 005, 006, 007, 008, 009, 011, 012, 013, 014, 015, 016, 017, 019, 020, 021, 022, 023, 025, 026, 028, 029, 030, 031 | 사용자 결정 (추천안 + 013 내구도, 029 업적, 030 리치 프레즌스, 031 게임패드 추가 채택) |
| 2026-11-02 | SUG 보류 6건: 018, 024, 027, 032, 033, 034 | 사용자 결정 |
| 2026-11-02 | SUG-010 음성 채팅: 영구 제외 (추후에도 넣지 않음) | 사용자 결정 |
| 2026-11-02 | 세션 참가 조건: [공개 / 친구 전용 / 초대 전용] 세 가지. 호스트가 새 월드를 만들 때와 기존 월드로 세션을 열 때 설정, 월드별로 저장, 세션 중 변경 가능. 새 월드 기본값 "친구 전용"(사용자 확정 2026-11-02. SRS의 "사용자 검토 대기" 표시는 SRS 승인 때 정리) | 사용자 결정 (SRS FR-144 세부화). 게임 안 행동 권한(SUG-018)은 보류 유지 |
| 2026-11-02 | 세계관: B "꺼진 하늘" 채택. 잠식 = 하늘을 덮은 잿빛 장막, 정화탑 = 장막을 뚫고 빛을 쏘는 등광탑, 혹한·어둠·독기는 햇빛 소실과 재에서 비롯됨, 빛이 닿는 곳에는 적이 생기지 않음 | 사용자 결정 (HDRP 조명 연출과 규칙 일관성) |
| 2026-11-03 | 개발 실행 방식: Unity 에디터를 열어 둔 채로 진행. 에이전트는 batchmode 대신 Unity CLI로 열린 에디터를 조작 (컴파일 `unity recompile`, EditMode `unity command test run --mode EditMode --result-only`, PlayMode `--async_tests true` 후 `unity command test status`로 확인, Console `unity command console get`). 시범 실행: 컴파일 오류 0, EditMode 157/157, PlayMode 5/5. batchmode를 띄우는 `unity build`/`unity test`/`-batchmode` 실행은 잠금 충돌로 금지, 출시 빌드 검사가 필요하면 사용자에게 에디터를 닫아 달라고 요청 | 사용자 결정 (에디터를 닫았다 여는 번거로움 제거, MCP·CLI 연결 확인 완료) |
| 2026-11-03 | SDD v0.8 갱신 (architect): D-03 실제 고정 버전, 3.4.2 asmdef 표(Unity.Jobs 참조 삭제, Game.Editor·시험 asmdef 추가), 3.4.3 Pipeline 안전장치, M1-01 (4) mergetool 방식, D-22 에디터 열어 둔 채 개발 방식 신설, 위험 R-28(mergetool 미검증)·R-29(실험 패키지·베타 CLI 의존) 신설. 상태 「승인」 유지 | 묶음 1 결과 사실 반영 |
| 2026-11-03 | 에디터 앞으로 가져오기(`unity command editor focus`, `unity recompile --focus`)를 필요할 때만 허용: PlayMode 시험·재생 모드 확인, 뷰 캡처, 그래픽·성능 측정 직전, 또는 에디터가 뒤에 있거나 최소화되어 명령이 30초 넘게 멈출 때. 단순 컴파일·EditMode 시험에는 쓰지 않음. 에이전트는 가져온 횟수와 이유를 보고. 뒤에 있어도 돌도록 `editor set_auto_tick` 100ms(유지) 설정 | 사용자 결정 (최소화·비활성 상태에서 작업이 멈추는 문제) |
| 2026-11-03 | M1 묶음 2 결과(developer): M1-04 결정론 V0 + 골든 해시(golden-seed-1 Full f4797ecf0e9e888a 등 3시드×2크기), 열린 에디터에서 IL2CPP 개발 빌드(184초) 후 Mono와 해시·타일 요약 전부 일치, 병렬 수 1~16 반복 일치 · M1-05 PerfTelemetry(Stopwatch 기본, ProfilerMarker 보조)·CSV·F3 성능 표시·Bench 씬·파일 로그 가림 · M1-06 HDRP 품질 4단계·프리셋(기준 「높음」)·DLSS/FSR2 품질 모드(이 PC는 DLSS 확인), manifest에 com.unity.modules.nvidia·amd 추가 · 총괄 재확인: 컴파일 0, EditMode 282/282, PlayMode 10/10, .meta 누락 0 | developer 보고 + 총괄 검증 |
| 2026-11-03 | CLAUDE.md 갱신(사용자 승인, 총괄 수정): 「에디터와 명령줄 실행」을 에디터 열어 둔 채 Unity CLI로 조작하는 방식으로 교체(명령 표, batchmode 금지, auto_tick, 필요할 때만 focus), Do 1단계·Check 2단계의 「에디터 닫기」 안내를 「열려 있고 연결됐는지 확인」으로, Smart Merge 절을 git mergetool 방식으로 교체(merge 드라이버 등록 금지 명시). 명령이 실제 설정과 같은 값을 만드는지 임시 저장소로 확인 | 사용자 결정 (SDD v0.8 D-22·M1-01 (4)와 일치시킴) |
| 2026-11-03 | CLAUDE.md 2차 갱신(사용자 승인, 총괄 수정): Unity 프로젝트 위치를 저장소 루트 → `src/`로, git 관리 범위에 `docs/` 포함·제외 목록에 `.mcp.json` 추가, Plan 커밋 시점과 `docs` 타입 설명·Plan 커밋 예시를 docs 커밋 기준으로, 산출물 표의 코드·시험 경로를 `src/Assets/_Game/`·`src/Assets/Tests/`로 | 사용자 결정 (실제 저장소 구조와 일치시킴) |
| 2026-11-03 | 네트워크 메시지 정의·바이트 인코딩을 Game.Application/Protocol(순수 C#)에 둠. FishNet은 연결 관리·전송·채널·바이트 전달·호스트 모드·플레이어 아바타에 계속 사용, 메시지는 바이트를 담는 PacketBroadcast 한 종류로 등록. 게이트웨이·JoinHandshake·복제는 Net 어댑터에서 포트에만 의존. 시험 asmdef Game.Tests.Net 신설 | 사용자 결정 (developer 제안 채택: FishNet 없이 시험, 대역폭 직접 제어) |
| 2026-11-03 | 게임패드 채팅·핑·퀵슬롯 등 겹치는 입력 버튼 할당은 추후 다시 정의, 보류 (키보드·마우스 할당은 유지) | 사용자 결정 |
| 2026-11-03 | 스태미나 0이 돼도 불이익 없음(달리기만 걷기 속도) · 스태미나 회복 지연 1초 확정(소모 행동을 멈춘 뒤 1초 뒤 회복 시작). C-04는 스태미나 부분만 해소, 허기·갈증·피로도·혹한은 미결 | 사용자 결정 |
| 2026-11-03 | 문서 갱신: SRS v0.10(FR-024·025 스태미나 확정, FR-117·NFR-004·FR-166 패드 보류, C-04 일부 해소, C-67 신설 「게임패드 채팅·핑·퀵슬롯 할당 보류, 늦어도 M2 채팅 착수 전」) · SDD v0.10(DES-080을 애플리케이션으로, DES-121 FishNetPacketTransport 신설 → 설계 요소 107개, 아바타는 ServerRpc→호스트 검사→ObserversRpc(D-24), Game.Tests.Net(참조 Application·Domain·Adapters.Net), 자원 델타 항목 4 B로 통일, 설정값 MoveStaminaGrace 0.5초, R-30·R-31 신설) · STORYBOARD v1.5(2.4 패드 퀵슬롯·핑·채팅 보류, SCR-014·015·030 반영, 스태미나 C-04 문구 정리) | 사용자 결정 반영 (requirements-analyst, architect, ui-ux-designer) |
| 2026-11-03 | 입력 설정은 SDD 원래 방식대로 Input System `.inputactions` 파일 에셋으로 (developer가 제안한 C# 표 + 런타임 생성 안은 채택하지 않음). 에디터 입력 설정 창에서 보고 고칠 수 있게. 보류된 패드 바인딩(C-67)은 파일에서 비워 둠 | 사용자 결정 |
| 2026-11-04 | (취소됨, 같은 날 사용자 요청) 사용자 취침 중 무인 진행: 묶음 3이 끝나면 총괄이 결과 확인(컴파일·시험·.meta)·진행 기록 갱신·보고서 작성 후 에디터 씬 저장, Unity 에디터 종료, PC 종료(shutdown /s /t 120). 커밋·push는 하지 않고 사용자 승인 대기로 남김 | 사용자 요청 → 취소: 사용자가 직접 지켜보고 끄기로 함 |
| 2026-11-04 | M1 묶음 3 결과(developer 2회, 1회차는 사용량 한도로 중지 후 새 developer가 마무리): Protocol 코덱·메시지 12종·AvatarPose 20 B, 포트, 세션·명령·복제·ServerWorld 30 Hz, FishNetPacketTransport·JoinHandshake·게이트웨이, SteamLobbyService, GameInput.inputactions(패드 C-67 보류 비움), 1인칭 이동·스태미나(불이익 없음·지연 1초)·HUD·개발용 로비, World 씬·PlayerAvatar 프리팹. 총괄 재확인: 컴파일 0, Console 오류 0, .meta 누락 0. HDRenderPipelineGlobalSettings.asset 변경은 HDRP 17.3 폐기 필드 자동 정리(값 변화 없음). 이번부터 에디터 조작은 Unity MCP 도구 우선 | developer 보고 + 총괄 검증 |
| 2026-11-04 | SDD v0.11 (architect): 3.2.3 Boot·World 씬 구성, 3.4.2 asmdef 실제값(Game.Tests.Integration에 Domain·Adapters.Net), 3.7.4 실행 옵션, 3.7.10 USS 토큰 대응표(--color-*, --fs-*), 3.8.2 에디터 생성 파이프라인 8단계, 3.9.9 부트·네트워크 시작 순서 신설, 3.11.12 이동 검사 설정값, D-25(에디터 생성기의 FishNet 리플렉션), R-31 갱신·R-32~R-34 신설. 설계 요소 107개 유지 | 묶음 3 구현 사실 반영 |
| 2026-11-04 | 사용자 수동 시험(키보드·마우스): 달리기 정상, 스태미나 부족 시 알림 후 달리기 불가 정상. **결함: ESC 메뉴의 친구 초대·세션 나가기 버튼 무반응** → developer 수정. 로비 상태 문구(「준비됨」) 글자색을 흰색으로. Xbox 패드 시험은 나중에 한꺼번에(당분간 키보드·마우스로만 시험). ESC 메뉴 수정 후 다시 시험 | 사용자 시험·결정 |
| 2026-11-04 | 버전 규칙 = 시맨틱 버저닝(SemVer 2.0.0): MAJOR.MINOR.PATCH, 출시 전 0.y.z(0.1.0 시작), 공개 출시 1.0.0, 배포 버전 불변, 사전 릴리스 -alpha.1·-rc.1, 빌드 정보 +… . 사이클 태그 규칙(v0.1.0 MINOR, fix v0.1.1 PATCH)과 일치. 현재 0.1.0 | 사용자 결정 (R-34 해소) |
| 2026-11-04 | 친구 전용 방이 방 검색에 안 나오는 것은 정상 동작(문제 아님). M2 공개 세션 목록은 「공개」 방만 표시(FR-145 그대로), SRS에 설명 추가 | 사용자 결정 (R-33 해소) |
| 2026-11-04 | 문서 갱신: SRS v0.11(NFR-058 게임 버전 SemVer 신설 → NFR 58개, FR-003 버전 불일치 거부, FR-145 친구 전용 방 미표시는 정상, FR-158, C-68 「메인 메뉴 버전 상시 표시」 미결) · SDD v0.12(3.7.12 게임 버전 규칙, DES-037 GameVersion 신설 → 108개, D-26, bundleVersion 단일 출처·빌드 중에만 +메타데이터, 참가 검사는 전체 문자열 일치, JoinRequest.gameVersion 문자열·protocolVer 2, R-33·R-34 해소, R-35 신설) · STORYBOARD v1.6(상태 문구 --color-text #E8E4DC, 색 토큰 대응표·토큰 4개 추가, SCR-016 친구 초대·세션 나가기 반응) · developer: ESC 메뉴 무반응 원인(로비 전체 화면 배경이 HUD 위를 덮음) 수정, 친구 초대 불가 시 안내 문구, 상태 문구 밝은 글자색. 총괄 재확인 EditMode 481/481 | 사용자 결정 반영 + 결함 수정 |
| 2026-11-04 | SemVer 구현(developer): Domain GameVersion 값 객체, 참가 검사에 게임 버전 전체 문자열 비교, JoinRequest.gameVersion·ProtocolVersion 2, BuildCommands가 빌드 중에만 +dev.<UTC>.<커밋>[.dirty] 붙였다 복구, ReadBuildNumber 폐지, DevLobby에 버전 표시. 개발 빌드 확인: 플레이어 버전 0.1.0+dev.20261104054926.417edbb.dirty, Windows에서 + 문제 없음(R-35 6번 해소), 같은 빌드 접속 통과·다른 메타데이터 VersionMismatch 거부. 개발 전용 옵션 -gameversion 추가(SDD 미반영 → 다음 SDD 갱신 때 반영). 총괄 재확인: EditMode 541/541, PlayMode 16/16, .meta 누락 0, bundleVersion 0.1.0 | developer 보고 + 총괄 검증 |
| 2026-11-04 | 사용자 재시험(키보드·마우스, 에디터): 방 열기 정상, ESC 메뉴 닫기·세션 나가기 정상, 나간 뒤 로비 화면 정상, 친구 초대는 안내 문구 「지금은 친구 초대 창을 열 수 없습니다 (Steam 오버레이가 꺼져 있거나 방이 Steam 방이 아닙니다)」 표시(에디터에서 예상된 동작), 로비 글자·버전 표시 정상 → ESC 메뉴 결함 해소 확인. 남은 수동 시험: Steam으로 실행한 빌드에서 실제 초대 창, 친구 계정 2PC 참가, Xbox 패드(나중에 한꺼번에) | 사용자 시험 |
| 2026-11-04 | **UI 방식을 UI Toolkit → uGUI(Canvas + TextMeshPro)로 변경. 앞으로 만들 모든 UI도 uGUI 기반.** 이유(사용자): UI 오브젝트를 하이어라키에서 직접 만들고 관리하는 쪽이 유지보수에 낫다. 원칙: UI는 하이어라키·프리팹의 게임 오브젝트가 원본이고, 스크립트가 다시 만들어 덮어쓰지 않음. 기존 화면(SCR-001 Steam 필요, 개발용 로비, 게임 HUD·ESC 메뉴)을 uGUI로 옮김. SDD D-16 대체 | 사용자 결정 |
| 2026-11-04 | 문서 갱신: SDD v0.13(D-27 uGUI + TextMeshPro로 D-16 대체, DES-122 UiThemeKit 신설 → 109개, 3.7.10 재작성: UiTheme 애셋 color-* 20·fs-* 6, ThemedText·UiButton·UiInputRouter 등, Overlay 캔버스 1920×1080 높이 기준, 숨길 때 Canvas·Raycaster·CanvasGroup 함께 끔, 생성기는 씬·UI 프리팹을 없을 때만 만들고 덮어쓰지 않음, R-18 해소, R-36~R-40 신설) · STORYBOARD v1.7(구현 용어 uGUI 기준, 디자인 변화 없음) | 사용자 결정 반영 |
| 2026-11-04 | uGUI 전환 결과(developer, 사용량 한도로 1회 중지 후 재개): com.unity.ugui 2.0.0·TMP Essential Resources, UiTheme 애셋(color 20·fs 6·글꼴 3), ThemedText·UiButton·UiScreenStack·UiInputRouter 등, 프리팹 UiRoot·Common·Screens(SteamRequired·DevLobby·GameMenu)·Hud/GameHud, Boot 씬은 MCP로 개별 교체(프리팹 인스턴스 5개), UI Toolkit 자산 삭제, 생성기는 없을 때만 생성(Run All 전후 md5 동일), 한글은 개발 전용 OS 글꼴(맑은 고딕) 메모리 대체. 총괄 재확인에서 PlayMode UiLayerPlayTests 6개 실패 발견(결함: 메모리 대체 글꼴의 재질·아틀라스 파괴 후 죽은 참조) → debugger가 UiDevFontFallback 수정. 최종: EditMode 555(554 통과, 1 건너뜀=Pretendard 대기), PlayMode 22/22, Console 오류는 알려진 SpawnablePrefabs 소음만 | developer·debugger 보고 + 총괄 검증 |
| 2026-11-04 | CLAUDE.md 갱신(사용자 승인, 총괄 수정): 「씬과 프리팹은 developer가 에디터 스크립트로 생성」 → 씬·프리팹 파일 자체가 원본, 스크립트는 없을 때만 생성하고 덮어쓰지 않음, 수정은 Unity MCP 도구로 오브젝트 단위, 사용자 수정을 덮어쓰지 않음, 스크립트 재생성은 코드가 원본인 설정(ProjectSettings·HDRP 품질 애셋·.inputactions)만, 씬·프리팹 작업은 묶음마다 커밋. 에디터 조작은 Unity MCP 도구 우선·CLI 대체로 명시 | 사용자 결정 (SDD v0.13과 일치) |
| 2026-11-04 | 결정 보류: Pretendard 글꼴 도입 방법, 중국어·일본어 대체 글꼴 (사용자가 토큰 소모 문제를 먼저 다루기로 함) | 사용자 결정 |
| 2026-11-04 | 토큰 절약 진행 방식(사용자 요청, 주간 한도 89% 소진): (1) 결정은 PROJECT_STATUS에만 쌓고 문서(SRS·SDD·STORYBOARD) 반영은 구현 묶음 끝에 한 번, (2) 단순 사실 반영은 가벼운 모델(sonnet), (3) developer 작업은 SDD 작업 1~2개 단위로 작게, (4) 보고·응답은 짧게, 읽을 문서 절을 정확히 지정, (5) 메인 세션 기록이 커지면 새 세션으로. 이 세션(대화 기록 54만 토큰)을 마치고 새 세션으로 이동. 미결 제안: CLAUDE.md에 「작은 사실 반영은 총괄이 직접 수정」 예외 추가 여부 | 사용자 결정 |
| 2026-11-04 | 에이전트 정의 갱신(사용자 요청, 총괄 수정): developer.md·architect.md를 현재 규칙에 맞춤 — batchmode 제거, Unity MCP 우선·CLI 대체, 씬·프리팹은 파일이 원본, uGUI+TMP, 실제 폴더(src/Assets/_Game/Code)·asmdef, 「총괄이 지정한 절만 읽기」 규칙, architect 수정 모드는 바뀌는 절만 Edit, 보고 길이 제한 | 토큰 절약, 규칙 불일치로 인한 재시도 방지 |
| 2026-11-04 | **UI 기호는 일단 모두 빼고 글자로만 상태 표시**(사용자 결정). 이유: Pretendard에 ✔·✕·컬러 이모지(✅❌⭐❗, ⚠의 U+FE0F)가 없어 □로 보임. 현재 코드의 UiGlyphs 7개(⚠✔▶○◆★●, 참가 화면·HUD·메뉴)를 글자로 대체(developer). **문서 반영 대기**: STORYBOARD 기호 표기(✔ 약 30곳, ✕·▲·○·⚠ 등)와 SDD 3.7.10 (8) 기호 대체 글꼴 단계 — M1 묶음 끝에 한 번에 반영. 같은 날 Pretendard v1.3.9 TTF 3굵기 적용(정적 SDF 4096² ×3, 동적 대체 ×3, TMP 기본 글꼴, 빌드 시 OFL 복사) | 사용자 결정, 글꼴 검사 |
| 2026-11-04 | developer 토큰 절약(사용자 승인): 정의에 「토큰 절약(응답 횟수·출력 크기)」 5항목 추가, 도구를 32개로 지정(기본 6 + ToolSearch + Unity MCP 25, 나머지 Unity 기능은 CLI). 측정(OK만 답하는 호출의 첫 문맥): **69,005 → 25,005 토큰(-64%)**. Unity 도구 이름 목록 169개 → 25개, 도구 설명은 필요할 때 불러 씀. 정의 파일 변경은 앱 재시작 뒤 적용됨 | 기본 문맥이 매 응답 다시 읽혀 developer 토큰의 60~86% 차지 |
| 2026-11-10 | uGUI 전환 수동 확인 완료(키보드 Esc로 ESC 메뉴 열기·닫기, 방향키 포커스 표시, Boot 하이어라키의 UI 프리팹 인스턴스): 문제 없음 | 사용자 확인 |
| 2026-11-10 | 중국어·일본어 대체 글꼴은 지금 넣지 않음. 이름 표시 화면을 만들 때 다시 결정(위험 목록 유지) | 사용자 결정 (총괄 추천안) |
| 2026-11-10 | 동적 대체 글꼴 4개(Pretendard Regular·SemiBold·Bold Dynamic SDF, LiberationSans SDF - Fallback)는 플레이 중 쌓인 임시 글자를 비운 상태로 커밋(12332ec), 이후 플레이로 다시 바뀌면 커밋 전에 되돌림. 빈 src/Assets/Resources 폴더 삭제 | 사용자 결정 |
| 2026-11-10 | M1 묶음 4는 작업 하나씩 진행, M1-10부터 developer에게 단독으로 맡김 | 사용자 결정 (토큰 절약) |
| 2026-11-10 | M1-10 1차 결과(developer, Unity MCP 사용, focus 1회): Domain/Gathering(ResourceState 16비트·HarvestRules), Application/Gathering(타격 명령 본문 3 B·호스트 처리기), Application/WorldSync(LocalWorld·요약값, ResourceChangeTool 「5,000개 변경」), ServerWorld 자원 표 복제, 참가자 백그라운드 생성·JoinComplete 전 델타 보관. 관찰: Full 월드 생성+요약값 605 ms, 5,000개 변경 스냅샷 20,008 B, 타격 델타 8 B, 루프백 중도 참가 313 ms(정식 측정은 M1-12). **SDD 반영 대기**(묶음 끝): 델타는 준비된 모든 연결에 전송, 거리 검증은 수평만(지형 붙으면 재검토), 타격 빈도 상수 4회/초, 거부 사유 기존 코드 재사용, 폴더 Gathering·WorldSync(3.8), 허가 뒤 생성 대기 +30초. 총괄 재확인: 컴파일 0, EditMode 577/577, PlayMode 23/23, Console 오류는 FishNet 시험 소음(SpawnablePrefabs is null)뿐, .meta 누락 0 | developer 보고 + 총괄 검증 |
| 2026-11-10 | M1-10 2차 결과(developer, Unity MCP 사용, focus 2회): Application/WorldSync(ResourceCellGrid 32 m 칸, ResourceVisualRules), Adapters/Presentation/World(TerrainViewBuilder Full 36타일·충돌체·임시 단색, ResourceViewStreamer 60 m GameObject 풀 + 350 m RenderMeshInstanced·임시 도형·쓰러짐 회전, ResourceHitInput 공격 0.3초 간격), Editor 메뉴 Game/Dev/Resources/Apply 5000 Changes, 개발 옵션 `-resourcechanges N`, 문구 키 lobby.status.generating, 인스턴싱 재질 4개, World 씬에 TerrainView·ResourceViews 추가·Ground 삭제(MCP·에디터 API). **SDD 반영 대기**(묶음 끝): 패키지 com.unity.modules.terrainphysics 추가(D-03), 스폰 = 월드 중앙 + 지면 높이 0.1 m, 쓰러진·부서진 자원은 충돌체 끔, 물·스플랫·디테일·낙관적 표현·CommandResult 처리는 이후. 미측정: Full 규모 표시 개수·FPS·드로우콜·지형 생성 시간. 총괄 재확인: 컴파일 0, EditMode 584/584, PlayMode 25/25, 새 Console 오류 0(FishNet 시험 소음만), .meta 누락 0, 씬 변경은 TerrainView·ResourceViews 추가와 Ground 삭제뿐 | developer 보고 + 총괄 검증 |
| 2026-11-10 | **결함(사용자 수동 시험): 플레이어가 앞으로 가도 떨림만 있고 움직이지 않음** → debugger 원인: 이동 검사(`AvatarMoveValidator`)와 핑 좌표 검사가 월드를 원점 중심 ±1536 m로 봤으나 실제 월드 좌표는 0~3072 m라, 월드 중앙(1536, 1536) 스폰 뒤 이동이 「월드 밖」으로 거부·되돌림(파일 로그 move_rejected 237회). 사용자 재시험: 이동 정상(결함 해소 확인). 수정: `ProtocolConstants.IsInsideWorld`(0~3072 + 가장자리 여유 16 m)로 두 검사 교체, 회귀 시험 1개. 총괄 재확인: 컴파일 0, EditMode 585/585, PlayMode 25/25. **M1-11에 넘김**: `Quantize.PositionToU16`·`CreatureState`도 ±1536 기준이라 생물 복제에서 같은 문제. **SDD 반영 대기**: 3.11.9·3.11.12 월드 좌표 범위 0~3072. 남은 의심: 「오디오 리스너 2개」 로그 반복(World 카메라와 메뉴 카메라 동시 활성 추정, 미조사) | 사용자 시험 + debugger |
| 2026-11-10 | developer 토큰 사용 보고서 작성(개인 참고용 `docs/dev/TOKEN_USAGE_REPORT.md`, `.gitignore`로 git 제외): 절약 적용 전후 요청 1회 평균 -63%, 기본 문맥 약 63k → 27k, SDD 작업 1개당 총량 -12%(M1-10 요청 121회). 보고서 6장 반영(사용자 요청, 총괄 수정): developer·debugger·test-engineer 정의에 같은 파일 재읽기 금지, 큰 파일은 Grep 후 범위 읽기, 2,000자 넘는 출력 자르기, 전체 시험은 CLI 요약(MCP test_run은 filter 시험만), developer 보고에 넘길 공개 API 기록. test-engineer·debugger의 옛 batchmode 실행·「에디터 닫기」·시험 asmdef 경로를 열린 에디터 방식과 실제 asmdef로 정리. 총괄: 시험 결과 요약만 받기, 연결된 작업은 문맥이 작으면 같은 developer에 이어 맡기기. 정의 변경은 앱 재시작 뒤 적용 | 사용자 요청 |
| 2026-11-10 | M1-10 사용자 수동 시험(에디터): 나무·돌 타격 시 쓰러짐·파괴 정상. 메뉴 Game/Dev/Resources/Apply 5000 Changes 실행 때 FPS가 순간 24까지 떨어짐(관찰, M1-12에서 측정·대응). 중도 참가 시험용으로 열린 에디터에서 개발 빌드 생성(Builds/Development, 결과 Succeeded, Pipeline 런타임 어셈블리 없음) → 빌드 2개로 호스트(-host -resourcechanges 5000)·참가자(-connect 127.0.0.1:7770) 시험 안내 | 사용자 시험 |
| 2026-11-10 | M1-10 중도 참가 수동 시험(개발 빌드 2개, Tugboat, 호스트 -resourcechanges 5000 + 직접 부순 나무·돌): 참가자 「월드를 만드는 중」 뒤 1~2초에 입장, 참가 전 부순 나무·돌이 참가자 화면에도 반영, 참가 뒤 호스트↔참가자 타격 서로 반영, 서로의 캐릭터 정상 → FR-009 중도 참가 동작 확인. **결함: 빌드에서 바닥(지형)이 보이지 않음**(에디터는 정상) → developer 수정: TerrainViewBuilder에 `_terrainMaterial`(materialTemplate) 추가, 애셋 TerrainGround.mat(HDRP/TerrainLit)·.png·.terrainlayer, World 씬 TerrainView에 MCP로 연결, 회귀 시험 TerrainMaterialSceneTests. 원인 추정(실행 중 만든 Terrain에 재질 미지정 → 빌드에서 TerrainLit 셰이더 제외, Player.log에 직접 증거 없음). 총괄 재확인: 컴파일 0, EditMode 586/586, PlayMode 25/25, 개발 빌드 재생성(12:45). 참고: 씬 저장 때 RenderSettings 스카이박스 참조가 기본값 → 없음으로 바뀜(HDRP는 Volume 하늘을 써서 영향 없음). **사용자 재시험: 여전히 안 보임(빌드에서만, 하늘만 보임)** → 진짜 원인: 지형 `drawInstanced = true`인데 재질 GPU 인스턴싱 꺼짐 + GraphicsSettings 인스턴싱 변형 제거(Strip Unused)로 빌드에 INSTANCING_ON 지형 변형이 없음. 수정(developer 이어서): TerrainGround.mat enableInstancing 켬, TerrainViewBuilder가 재질 인스턴싱에 맞춰 drawInstanced 결정(꺼져 있으면 경고), 회귀 시험에 enableInstancing 검사. 빌드 화면 캡처로 지면 보임 확인(총괄도 확인). 총괄 재확인: EditMode 586/586, PlayMode 25/25, 빌드 13:01. 남은 일: 지면 색이 의도(0.30, 0.42, 0.24)보다 어두움(조명 영향 추정, 이후 아트 작업) | 사용자 시험 |
| 2026-11-10 | M1-11 1차 결과(developer, focus 1회): Domain/Creatures(Creature·CreatureBrain 배회→추적→근접 공격), Application/Ai(AiSettings·AiLodPolicy, IAiNavigationBackend 포트, CreatureRegistry·WalkGridBuilder, AiScheduler 10 Hz 분산·LOD), Infra.AiJobs(WalkGridJob·FlowFieldJob·AiSpatialHash·SteeringJob, AiJobsBackend), ServerWorld.AttachAi·SpawnTestCreatures. 관찰(에디터, 60체 + 8명, 300틱): 서버 틱 평균 0.16~0.29 ms, p99 4.3~8.1 ms(흐름장 재계산 틱), 흐름장 8개 재계산 4~10 ms(워커 예산 2.5 ms 초과, 빌드에서 M1-12 재측정). **SDD 반영 대기**(묶음 끝): 3.4.2 시험 참조 Game.Tests.Infrastructure → Game.Infra.AiJobs, 보행 격자는 월드 중심 600 m만(전체는 M4 전), IAiNavigationBackend 실제 시그니처(BuildWalkGrid·SetFlowTargets·ScheduleSteering·CompleteSteering), 물·정화 구역 미반영, 피해는 CreatureAttack 목록만. 총괄 재확인: 컴파일 0, EditMode 624/624, PlayMode 25/25, .meta 누락 0 | developer 보고 + 총괄 검증 |
| 2026-11-10 | M1-11 2차 결과(developer, focus 1회): 생물 위치 압축을 -16~3088 m(4.7 cm 단계, Y 포함)로 수정, 관심 범위 복제(CreatureInterest 등급·CreatureReplicator·ClientCreatureStore 보간), CreatureViews(캡슐, 모드별 색), Bootstrap에 AiJobsBackend·AiScheduler 연결, 개발 옵션 `-testcreatures N`·메뉴 Game/Dev/AI/Spawn 60 Test Creatures, World 씬에 CreatureViews 루트(MCP). 관찰: 참가자 1명 생물 복제 약 8.8 KB/s(SDD 추정 7.9 KB/s), CreatureViews 60체 0.048 ms, 루프백 60체 위치 최대 차 0.26 m. **SDD 반영 대기**(묶음 끝): 보간 방식(직전 수신 간격만큼 이동, 서버 시계 동기 불필요), Despawn 히스테리시스 +10 m, Near/Mid 50/150 m 상수, AI 설정 기본값 코드(설정 파일 미분리). 총괄 재확인: 컴파일 0, EditMode 642/642, PlayMode 26/26, .meta 누락 0, 남은 백그라운드 명령 0, 개발 빌드 재생성(13:55) | developer 보고 + 총괄 검증 |
| 2026-11-10 | **토큰 계측·보고서 갱신 중단**: 이번 세션 총괄 토큰의 약 20%(처리량 약 9M, 비용 환산 약 1.16M)가 토큰 계측·보고서·규칙 반영에 들었고, 매 호출 계측 비용(1회 약 1M)이 작업당 절감분(M1-10 → M1-11 약 -4%)과 비슷함. 앞으로 사용자가 요청할 때만 계측. 이미 반영한 에이전트 절약 규칙은 유지 | 사용자 결정 |
| 2026-11-10 | **자연 에셋(HDRP Terrain 에셋 + 자원 모델) 도입 일정**: M1~M2는 임시 도형 유지(M1-12 성능 기준 보존, 에셋 후보·HDRP 17.3/Unity 6000.3 호환·라이선스 확인만). **M3**에 지역별 「자연 프로필」 데이터(바닥 TerrainLayer, 풀·꽃 디테일, 자원 종류별 모델)와 지역 1(잿빛 숲) 에셋 투입(벌목·채광 애니메이션에 실제 모델 필요). M4~M5 지역 1 보강. **M6** 지역 1 모습 완성·실제 에셋 성능 측정(HDRP 재평가 뒤 HDRP 전용 에셋 확대). M9a/b/c·M10에 지역별 에셋과 생성 규칙. 원칙: 지형 높이·자원 배치는 생성기(결정론, SDD 3.11.4 지역 지도·지역별 자원 표), 바닥·풀·꽃은 화면 전용 Terrain 레이어·디테일(동기화 없음, 밀도는 그래픽 프리셋), **나무·돌·광석은 Terrain 나무가 아니라 ResourceViewStreamer 모델 교체**(상태 동기화·쓰러짐 연출), 에셋 투입마다 빌드 확인(인스턴싱·셰이더 포함). **SRS·SDD 반영 대기**(M1 묶음 끝 또는 M3 계획 때) | 사용자 결정 (총괄 추천안) |
| 2026-11-10 | M1-11 사용자 수동 시험(Host.bat·Client.bat, 개발 빌드 2개, `-testcreatures 60`): 생물 배회, 접근 시 주황→빨강으로 바뀌며 추적 정상, 참가자 화면에도 생물 정상 표시. 관심 범위(300 m) 밖 사라짐·재등장은 거리 감이 없고 지형 높낮이로 눈으로 판정 불가 → 자동 시험(CreatureReplicationTests 등급·Spawn/Despawn 전환)으로 확인됨, 실제 송수신은 M1-12에서 측정. 개인 시험용 Host.bat·Client.bat은 저장소 루트에 두고 .gitignore로 제외 | 사용자 시험·결정 |
| 2026-11-10 | 개발용 관심 범위 표시 추가(사용자 요청, developer): F1로 켜고 끔(기본 꺼짐), 오른쪽 아래, 개발 빌드·에디터 전용(IMGUI, F3 성능 표시와 같은 방식). 내 위치, 받은 생물 수/범위 300 m, 가까움·중간·멂 수, 최근접·최원 거리와 한계 310 m, 호스트는 서버 전체 생물 수. 파일 CreatureInterestStats(순수 계산)·CreatureInterestHud·GameFlow 연결, 시험 3개. 총괄 재확인: EditMode 645/645, PlayMode 26/26, 개발 빌드 15:24. 사용자 화면 확인 완료. **SRS·SDD 반영 대기**: 개발용 키 F1(3.7.4 개발 옵션·STORYBOARD 키 할당표의 개발 전용 키) | 사용자 요청 |
| 2026-11-10 | M1-12 1차 결과(developer, focus 1회, 측정 빌드 16:19): 봇 `-bot`(BotBrain 경로→자원 타격→생물 추적, BotClient는 World 씬·지형·자원 뷰 없이 동작), JoinTimeline(중도 참가 단계별 시간 JSON), RuntimePerfSampler(1초마다 메모리·대역폭·RTT·바뀐 자원 수, IL2CPP용 psapi), CSV 열 6개 추가, 측정 스크립트 `tools/perf/RunM1Perf.ps1`(호스트 + 봇 6 + 60초 뒤 중도 참가 봇 1, P1 모드, summary.md·json), 기존 버그 수정(활동 생물 수가 CSV에 안 남던 카운터 이름). 30초 점검(에디터 켜짐, 판정용 아님): Server.Total 평균 0.061 ms·p99 0.277 ms, 메인 스레드 2.68 ms, 호스트 송신 624 kbps, 참가자 수신 95 kbps, 중도 참가 11.1초(연결 10.3초가 대부분), 메모리 1.5 GB, VRAM 1.3 GB. **결함: 개발 빌드를 `-batchmode`/`-nographics`로 띄우면 시작 직후 크래시**(ntdll, 종료 코드 -1073741819, 창 모드는 정상, 11-04에는 -nographics 동작) → 스크립트가 창 모드 봇(320x180)으로 자동 전환. 참고: 봇 창 실행으로 플레이어 설정(레지스트리 PlayerPrefs)의 마지막 창 크기가 320x1080 창 모드로 남음(실행 인자 없이 exe를 켤 때만 영향). 총괄 재확인: EditMode 662/662, PlayMode 26/26, 남은 프로세스 0, 빌드 폴더 플러그인 원상태 | developer 보고 + 총괄 검증 |
| 2026-11-10 | **화면 없는 실행 크래시 수정**(사용자 결정: 측정 전에 먼저 수정 → debugger): 원인은 `UiSafeArea16x9.Refresh`의 끝없는 재귀(offset 설정이 같은 호출 안에서 OnRectTransformDimensionsChange를 다시 불러 Refresh 반복, Null 그래픽 장치에서 캔버스 크기가 수렴하지 않아 스택 넘침 → ntdll 접근 위반). 근거: WER 덤프 스택 잔여 주소를 GameAssembly.pdb로 풀어 확인, frame timing 끄기는 무관 확인. 수정: 재진입 방지 플래그 + 캔버스 폭·높이 0 이하/NaN/무한이면 건너뜀(UiSafeArea16x9.cs 한 파일). 빌드 4회. 화면 없는 30초 점검: 크래시 0, 접속 8명, 호스트 FPS 60.0/1% Low 59.7(참고), 중도 참가 0.73초(연결 0.20초, 창 모드 때 10.3초는 봇 창 부팅 시간이 섞였던 것으로 보임), 참가자 수신 115 kbps. 남은 의심: Null 장치에서 값이 수렴하지 않은 정확한 이유 미확인. 총괄 재확인: EditMode 662/662, PlayMode 26/26, ProjectSettings 차이 없음 | 사용자 결정 + debugger |
| 2026-11-10 | **M1-12 측정 결과: 조건부 통과**(에디터 닫고 총괄이 RunM1Perf.ps1 실행, 보고서 `docs/test/perf/M1_tech_verification.md`는 developer). P8(호스트 + 화면 없는 봇 7, 300초): Server.Total 평균 0.060 ms·p99 0.377 ms(틱 최악 5.57 ms, AiApply 구간), 메인 스레드 2.20 ms, 호스트 송신 1.03 Mbps, 참가자 수신 최대 175 kbps, 중도 참가 0.74초, 메모리 1.37 GB, VRAM 1.4 GB, 호스트 FPS 60 고정(vSync 추정, 참고). P1: Server.Total 0.029 ms, 송신 66 kbps. 3.12.9 미달 대응 불필요, 관찰 항목. 「조건부」 이유: PC 1대·봇 CPU 공유·임시 그래픽·Tugboat 직접 전송, Steam 릴레이 미확인. 권고: 흐름장 전용 측정 표식, 생물 스냅샷 GC 재측정, 실제 에셋 뒤 재측정(M6-10). 스크립트 에디터 감지 오탐(MCP용 unity.exe) 수정 | 총괄 측정 + developer 보고서 |
| 2026-11-10 | 사용자 수동 시험: 개발 빌드를 Steam 전송(실행 옵션 없음)으로 실행해 방 열기 → 친구 초대에서 **Steam 오버레이 초대 창이 뜨는 것 확인**. 친구 계정 실제 초대·참가와 Steam 릴레이 접속은 사용자가 나중에 Steam으로 플레이하며 확인 | 사용자 시험 |
| 2026-11-10 | **묶음 끝 문서 반영**(반영 대기 9건 해소, 문서 에이전트 3개 동시·sonnet): SRS v0.12(2.6.1 자연 에셋 도입 일정과 원칙, 새 ID 없음, FR 179·NFR 58 유지), SDD v0.14(월드 좌표 0~3072·압축 범위, 스폰, 자원 델타·타격 검사, 중도 참가, 지형 재질·인스턴싱 빌드 주의, terrainphysics, World 씬 구성, IAiNavigationBackend 실제 시그니처·보행 격자 600 m, 생물 복제 보간·Despawn 여유, asmdef 참조, 폴더 트리(총괄이 실제 파일과 대조해 4줄 정정), 개발 옵션 `-resourcechanges`·`-testcreatures`·`-bot`·`-gameversion`·F1·F3, 화면 없는 실행 안전 규칙, 기호 제거·CJK 보류, M1-12 결과, R-41~R-45 신설, 자연 에셋 원칙(DES-089); 새 DES 없음, 상태 승인 유지), STORYBOARD v1.8(UI 기호 약 150곳 글자 표기, 개발 전용 키 F1·F3 표). **사용자 결정**: STORYBOARD에 남은 기호(핑·쓰러짐·부활·위협 모양·체력·손목 계기 게이지·햇빛·잠김·체크박스·선택 화살표·슬라이더 손잡이)는 글꼴 문자가 아니라 **앞으로 만들 아이콘 그림 자리**로 명시, 구현 때 TMP 글자로 찍지 않고 아이콘 전에는 글자 라벨로 대신 | 사용자 결정 + 문서 에이전트 |
| 2026-11-10 | **M1-13 결과 → M1 완료**(developer, focus 1회): 아키텍처 시험 약 35건 통과(새 폴더·asmdef 규칙 위반 없음). M1 FR·NFR 대응표: 자동 시험 약 33건, 수동 확인됨 9건, 추가 시험 1건(출시↔개발 채널 접속 거부 `ReleaseAndDevBuilds_WithSameCoreVersion_AreRejected`). **수동 남음**: 2PC 참가(FR-003), Steam 릴레이(FR-005), Xbox 패드, 참가자 프레임(NFR-012), 반영 지연(NFR-015), 출시 빌드 메모리(NFR-048), 사양(NFR-005), Steam Cloud(NFR-007), 호스트 끊김 실제(NFR-039), 화면 표시(HUD·그래픽 설정), 출시↔개발 실제 접속 거부. 출시 IL2CPP 빌드 Succeeded(약 1.87 GB), Pipeline 런타임 어셈블리 0건, 버전 `0.1.0+rel.20261110090052.5d3f33e.dirty`(시험 1개 커밋 전이라 dirty), bundleVersion 원상 복구, 출시 exe에서 개발 옵션 무시 경고·Steam 필요 판정 확인(F1·F3 화면 미표시는 소스 조건부로 확인). 총괄 재확인: EditMode 663/663, PlayMode 26/26, ProjectSettings 차이 없음, 남은 프로세스 0. 위험: 출시 빌드 용량 1.8 GB(Steam 업로드 전 검토) | developer 보고 + 총괄 검증 |
| 2026-11-10 | **Do 단계 완료 승인**, 커밋 후 cycle/1-mvp push(ce571b7..a50c508, LFS 117 MB) | 사용자 결정 |
| 2026-11-10 | **Check 결과**: 코드 리뷰 REV 15건(심각 2: 이동 검사 우회, FR-003 버전 두 개 미표시 / 보통 6 / 가벼움 7), CMMI 수준 2 추정, SPICE(요구 2·설계 3·구현 2·통합 1·시험 1·형상 2·QA 1·관리 3), SRS IEEE 1012 일부 부분 적합(M1 기준에 M2~M4 섞임, TC 대응표 부재). 보안 SEC 10건(높음 3: 이동 검사 우회, 비멤버 Steam 연결이 판정 전 데이터 수신, 자세·잘못된 패킷 로그 폭주 / 중간 2 / 낮음 5), 출시 빌드 개발 기능 제외 조건부 적합, 비밀 값 없음. 시험(29119): TC 66(통과 44, 실패 9, 부분 7, 차단 6), EditMode 671(통과 665, 실패 6 = 결함 재현 `KnownDefect`), PlayMode 26/26, M1 FR 17개 모두 TC 대응. DEF-001 이동 검사 우회(높음), DEF-002 첫 자세 순간 이동, DEF-003 공유 명령 대기열 고갈, DEF-004 버전 거부 두 버전 미표시(높음), DEF-005 Player.log 원문 기록. 25010: 기능 적합성 보통, 성능 효율성 양호, 호환성 보통, 사용성 보통, 신뢰성 양호, 보안성 보통(무결성 미흡), 유지보수성 양호, 이식성 보통. 종료 기준 조건부(수동 시험 미완, 높음 결함 2). IMP 18건 제안(P1: 001 이동 검사, 002 버전 표시, 003 비멤버 연결 차단, 004 빈도 제한·로그 폭주, 014 수동 시험 / P2: 005 대기열, 006 Player.log, 011 형상 관리, 012 dirty 빌드 거부, 013 SRS·STORYBOARD 정합 / P3: 007~010, 015~018). 총괄 재확인: EditMode 671(665 통과, 실패 6은 결함 재현) | Check 에이전트 3개 + 총괄 검증 |
| 2026-11-10 | **Act 대상 선택**: 승인 IMP-001 이동 검사 보강, 002 버전 두 개 표시, 003 비멤버 Steam 연결 차단, 004 빈도 제한·로그 폭주 방지, 005 명령 대기열 플레이어별 몫, 006 Player.log 끄기, 011 형상 관리 보강, 012 dirty 출시 빌드 거부, 013 SRS·STORYBOARD 정합, 016 커버리지·CI, 017 의존성 취약점 확인, 018 측정 보고서 보완(12건). 보류(다음 사이클): IMP-007~010, 015. IMP-014 수동 시험은 사용자 수행 대기 | 사용자 결정 |
| 2026-11-10 | **총괄 세션은 작업 묶음마다 새로 시작**(Do: SDD 작업 하나마다, Plan: 문서 하나마다, Check·Act: 단계마다). 마칠 때 「다음에 할 일」에 인계를 적고 새 세션에서 「작업 이어서 진행해줘」로 시작 | Check 토큰 분석 세션 결과: 11-10 하루 총괄 비용 환산 14.0M, 문맥 86k→617k. 묶음마다 새로 시작하면 약 -35~40%(추정). 사용자 결정 |
| 2026-11-10 | **code-reviewer 모델 opus → sonnet**, security-auditor는 opus 유지. 다음 Check 뒤 사이클 1 REVIEW_REPORT와 비교해 품질이 떨어지면 되돌림 | API 단가 Opus = Sonnet의 2배, Check 비용 약 -17%(추정). 사용자 결정 |
| 2026-11-10 | code-reviewer·security-auditor 보고서 앞에 **「개선 후보 표」**, test-engineer는 그 표와 필요한 절만 읽기. 두 검토 에이전트에 「같은 파일 재읽기 금지」. 반영: `.claude/agents/code-reviewer.md`·`security-auditor.md`·`test-engineer.md`, 메모리 `token-saving-workflow`, `docs/dev/TOKEN_USAGE_REPORT.md` 8.7절(모두 git 제외). 에이전트 정의는 앱 재시작 뒤 적용 | 각각 Check 전체의 약 2% 효과, 품질 위험 거의 없음. 사용자 결정 |
| 2026-11-10 | **IMP-011 형상 관리 사유 기록**: `main` 직접 커밋 17개(4dcd38b~8eed7fa, 모두 2026-11-03)는 저장소 준비 중 사이클 브랜치(`cycle/1-mvp`, 첫 커밋 24e771b)를 만들기 전에 Plan 문서·M1 묶음 1을 커밋한 것. 되돌리거나 이력을 고치지 않고 그대로 둠(강제 push 금지 규칙). 사이클 2부터는 사이클 시작 때 브랜치를 먼저 만들고 Plan부터 그 브랜치에 커밋 | REV 4장 CM, 7장 권고 1·6. 이력 재작성은 원격 push 이력을 깨므로 사유 기록으로 대신 |
| 2026-11-10 | **R-47 이동 검사 상수 이전을 이번 Act에서 처리**: `MaxPoseHz`·`PoseBurstCapacity`·`SpawnRadiusMeters`·`SpawnHeightToleranceMeters`를 `AvatarMoveSettings` 코드 상수에서 `BalanceValues.Movement`로 옮기고 `ConfigValidator`에 `MaxPoseHz ≥ AvatarSendHz` 같은 검사 추가 (SDD v0.15 R-47) | 사용자 결정 (architect 권고는 이전, 총괄 추천은 다음 사이클이었음) |
| 2026-11-10 | **R-46 폰트 dirty: 검사에서 제외**. `Pretendard-Bold/Regular Dynamic SDF.asset` 2개만 출시 빌드 dirty 검사에서 뺌. 이 글꼴은 `m_ClearDynamicDataOnBuild: 1`이라 빌드 산출물에는 동적 글자판이 들어가지 않음(저장본 6KB 빈 글자판, 에디터 사용 뒤 약 2MB). 이 두 파일의 작업 트리 변경은 커밋하지 않음 | 사용자 결정 (architect 권고 Static 전환 대신, 빌드 때 지워지는 데이터라 안전) |
| 2026-11-10 | **보류**: GameCI EditMode CI 도입(라이선스 종류 미정), 성능 시험 패키지·커버리지 하한(기준선 줄 62.1%만 기록, docs/test/COVERAGE_BASELINE.md) | 사용자 미결정. 총괄 추천은 CI는 출시 준비·협업 시점, 나머지는 사이클 2(M2) |
| 2026-11-10 | **IMP-011 커밋 규칙 보강**: Act 커밋부터 `Closes: IMP-xxx, DEF-xxx` 사용(b889f97·de07509·4931bef). 제목 50자 검사 훅 `tools/git/commit-msg` 추가(Conventional Commits 형식·50자·대문자 시작·마침표 금지 검사, 병합·revert 제외). 개발 PC마다 `git config core.hooksPath tools/git` 한 번 실행. 기존 50자 초과 제목 17/99건은 이력 재작성 없이 둠. Do 결함 소급 DEF 등록은 test-engineer 회귀 시험 때 TEST_LOG에 함께 | REV 7장 권고 7. 훅은 저장소에 커밋해 PC 간 공유 |
| 2026-11-10 | **IMP-018 처리·재측정**: 측정 도구 보완 후 에디터 닫고 P8 300초 3회(기본·vSync 끔·업스케일 끔). 판정 항목 모두 통과, vSync 끈 호스트 FPS 평균 454·1% Low 176(60 고정은 vSync 상한), 업스케일 끔 GPU 4.73 ms(켬 4.82), 흐름장 재계산 평균 1.7~2.3·최대 3.1~4.6 ms, 틱 최악 2.3~3.3 ms. 1회차는 유튜브 재생 중이었으나 판정 항목 영향이 작아 유효 채택, 2·3회차는 오염돼 영상 끄고 재측정(무효 폴더 225837·230435). **Server.Total 판정을 틱당 평균 ≤ 3.0 ms로 변경**(사용자 결정, SDD v0.17 33행·3.12.3·3.12.4) | 프레임당 평균은 FPS에 따라 변함(vSync 끔 0.009 ms), 틱당 0.12~0.14 ms로 여유 큼 |
| 2026-11-11 | **Act 회귀 결과 수용**: 회귀 판정 재작업 1건(IMP-013, SRS 머리말 버전 DEF-012)을 고친 뒤 커밋. 승인 IMP 12/12 검증 완료, 미해결 결함 0 | 사용자 결정 |

## 위험 목록
| 위험 | 출처 | 영향 | 대응 |
|---|---|---|---|
| 커스텀 SRP를 직접 작성할 경우 실사 그래픽 구현 비용이 매우 큼 | 총괄 | 일정 지연, 품질 저하 | HDRP 채택으로 해소 (2026-11-02) |
| 기능 범위가 넓음 (생존·건축·농사·요리·전투) | 총괄 | 1사이클 범위 초과 | 마일스톤 단위로 나눠 진행 |
| 멀티플레이는 나중에 붙이기 어려움 (상태 동기화, 권한, 인벤토리 검증) | 총괄 | 후반 대규모 재작업 | 첫 마일스톤부터 네트워크 구조 포함 |
| HDRP 실사 그래픽 + 멀티플레이 동시 부하 | 총괄 | 프레임·대역폭 목표 미달 | NFR로 성능 목표를 수치화 |
| 동물·적 AI의 네트워크 동기화 | 총괄 | 구현·테스트 비용 증가 | 서버 권한 AI로 설계 |
| git 저장소 아님 | 총괄 | 형상 관리 불가 | 해소: 2026-11-03 git init + git lfs install |
| Unity CLI가 베타(1.0.0-beta.15)이고 MCP로 씬 편집·컴파일 오류 확인이 부족하다는 사용자 보고가 있음 | 총괄(조사) | 에디터 자동 조작 범위 제한 | 셸 명령(unity, unity eval)과 함께 사용, M1에서 실제 동작 범위 확인 |
| MCP 연결 전제: src/ Unity 프로젝트 생성 + Unity Pipeline 패키지 설치 + Editor 실행 필요. 설치된 Editor는 6000.0.84f1로 SDD 권장(6000.3 LTS)과 다름 | 총괄 | 프로젝트 생성 전에는 MCP가 Editor에 연결되지 않음 | M1-01에서 6000.3 LTS 설치·프로젝트 생성·unity pipeline install |
| .gitattributes의 Unity Smart Merge(merge=unityyamlmerge)는 git config로 merge 드라이버를 등록해야 동작 | 총괄 | 씬·프리팹 충돌 시 일반 병합으로 처리됨 | M1에서 Unity 설치 경로 확인 후 드라이버 등록 (devops-engineer 또는 developer) |
| Steamworks는 플랫폼(로비·초대·릴레이)이고 동기화 라이브러리는 별도로 필요 | 총괄 | 구조 결정 누락 | 네트워크 라이브러리(FishNet 등) 선택 필요 |
| Steam 전송 방식은 한 PC에서 두 빌드로 자기 자신에게 접속 불가 | 총괄(조사) | 로컬 테스트 어려움 | 개발용 일반 전송과 Steam 전송을 바꿔 끼울 수 있게 설계 |
| 출시용 Steam App ID 필요 | 총괄 | 출시 지연 | 개발 중에는 Steam 테스트용 App ID(480) 사용, 출시 전 Steamworks 파트너 등록 |
| 호스트 캐릭터를 고정 ID로 저장하면 전용 서버 이전 시 캐릭터 유실 (타 게임 사례) | 총괄(조사) | 월드 이전 불가 | 모든 플레이어를 SteamID로 저장 |
| FishNet·FishySteamworks는 소수 개발자가 관리 (의존성 위험) | 총괄(조사) | 업데이트 중단 시 대응 어려움 | 전송 계층을 추상화하고 버전을 고정, 필요하면 포크 |
| FishNet 출시작 중 대형 오픈월드·8인 생존 사례는 확인 안 됨 | 총괄(조사) | 대규모 월드 동기화 성능 미검증 | M1에서 8인 접속·대량 자원 오브젝트 동기화 기술 검증 |
| 호스트 권한 구조라 호스트는 조작 가능 | 총괄 | 협동 전용이라 영향 낮음 | 참가자 요청은 서버(호스트)에서 반드시 검증 |
| 탐험 단계 진행(B)과 음식 버프(D)가 Valheim과 겹쳐 보일 수 있음 | 총괄 | 차별성 약화 | 잠식 정화 진행 방식 채택으로 일부 해소 |
| 솥 주변 보조 기구로 요리 단계를 올리는 방식은 Valheim 솥 업그레이드와 비슷함 | 총괄 | 차별성 약화 | 단계 상승 대신 기구별 조리 방식(손질·발효·훈연 등) 해금으로 차별화 검토 |
| 정화탑 웨이브·보스·밤 습격으로 적 AI와 동기화 부하 증가 | 총괄 | 성능·개발 일정 | 인원 수에 따른 웨이브 규모 조정, 동시 적 수 상한을 NFR로 정함 |
| 스토리·세계관 미정 | 총괄 | 잠식의 정체·지역 구성이 나중에 바뀔 수 있음 | 세계관 "꺼진 하늘" 확정으로 해소 (2026-11-02) |
| App ID 480은 여러 개발자가 공유 → 다른 게임 로비가 섞이고 Steam Cloud 검증에 한계 | requirements-analyst | 개발 중 로비·저장 테스트 신뢰도 저하 | 로비 필터(NFR-009), 정식 App ID 조기 확보 검토(C-30, C-34) |
| 월드 크기·지역 배치·자원 오브젝트 총수 미정 (C-35) | requirements-analyst | 성능 목표·동기화 설계 불확정 | 혼합형 월드 생성으로 결정 (2026-11-02) |
| Unity가 2026 렌더 파이프라인 전략에서 HDRP를 유지보수 모드로 전환 (새 기능 없음, 안정성·중요 수정만. 신규 프로젝트에는 URP 권장) | architect (총괄이 Unity 공식 페이지로 확인) | 장기 개발 중 HDRP 기능 정체, 에셋·커뮤니티 축소 | 사용자 결정 필요: HDRP 유지(LTS 고정, 렌더링 코드 어댑터 격리) 또는 URP 전환 |
| 결정론 생성 실패 시 동기화 전체 붕괴 | architect | 치명적 | 정수·Q16.16 고정소수점만 사용, float·Burst 금지, 골든 해시·교차 PC 시험 |
| 클라이언트 권위 이동: 속도 한도 안의 위치 조작은 막지 못함 | architect | 협동 전용이라 영향 낮음 | Check 단계 보안 점검 대상 |
| HUD 기본이 장치형(손목 계기, 3D 소품·애니메이션·월드 공간 UI) + 일반 HUD 옵션 두 벌 | 총괄 (DESIGN_PROPOSAL D-02 단점) | M2 일정 증가, 월드 공간 UI 구현 위험(SDD R-18), 8인 동료 체력은 등불 밝기 단계로만 구분 | 일반 HUD(A) 부품을 장치형이 재사용, 접근성 옵션으로 일반 HUD 제공 |
| Unity Pipeline 패키지(com.unity.pipeline 0.9.1-exp.1)가 에디터 전용이 아님 | developer | 런타임 서버가 빌드에 들어갈 위험 | 해소: 허용 + 안전장치(기호 금지 시험, 출시 빌드 산출물 검사). MCP 실제 연결 사용자 확인 완료(2026-11-03) |
| Smart Merge 드라이버가 확장자 없는 임시 파일 때문에 실패(실제 Boot.unity로 확인) | developer | 씬·프리팹 자동 병합 불가 | git mergetool 방식으로 변경(미시험), 첫 실제 충돌 때 확인. SDD M1-01 (4) 수정 필요 |
| FishNet 4.7.4R은 com.unity.modules.physics2d 내장 모듈이 있어야 컴파일됨 | developer | 모듈 제거 시 빌드 실패 | manifest에 유지, SDD D-03에 기록 |
| SCR-001(Steam 미실행) 실제 화면 미확인 (Steam 실행 중이라 단위 시험만) | developer | UI 표시 문제 늦게 발견 | 해소: 사용자가 에디터 GUI에서 Steam 끄고 확인, 정상(2026-11-03) |
| 다른 CPU(Intel·AMD) 결정론 확인이 M6로 늦어짐 → 불일치 시 M3~M6 생성 규칙·시험 월드·골든 해시 재작업 (SDD R-26) | architect, requirements-analyst | 중간 | 결정론 규칙을 코드 리뷰 체크 항목으로, 생성 규칙 변경 시 Mono/IL2CPP 교차 비교, 다른 PC를 빌릴 수 있으면 M3-15·M4-13에 -genhash 비교 |
| M1 봇 7개가 같은 PC에서 돌아 호스트 FPS가 실제보다 나쁘게 나오고 Steam 릴레이 지연이 빠짐 (SDD R-27) | architect | 중간 | M1은 서버 처리 예산·대역폭·중도 참가·메모리로 판정, 호스트 FPS는 참고값, 최종 판정은 M6-10 실측 |
| 기본 키 할당 충돌: 패드 채팅 버튼 미지정(보기 버튼이 인벤토리와 겹침), 핑 LT+RB와 퀵슬롯 RB 조합 처리, LB/RB 문맥별 사용 | ui-ux-designer | 낮음 (채팅 M2, 핑 M5) | M2 착수 전 결정 |
| M4 일정 지연 (작업 13개, 미정 항목 C-60~C-66, SDD R-24) | architect | 중간~높음 | 보관함 규칙 재사용, 미정 항목은 잠정값으로 진행, 늦으면 숯을 M5로 미루는 안 제시 |
| 석탄 광맥 추가로 생성 결과 변경 → M3 시험 월드·골든 해시·측정 기준 시드 재정렬 (SDD R-25) | architect | 낮음~중간 | M4-09에서 생성 시간·재생성 비율 재측정 |
| M2 일정 지연 (작업 13개, SDD R-21) | architect | 중간 (2026-11-03 손목 3D 소품 제외로 중간~높음에서 낮춤) | 일반 HUD 먼저, 손목 소품은 임시 모델로 시작, 늦으면 등불 이름표·가장자리 효과를 M5로 미루는 안 제시 |
| 손목 계기 글자 가독성 (기울임·글자 크기, SDD R-22) | architect | 낮음~중간 (화면 UI 방식은 업스케일링 영향 없음) | 텍스처 해상도·최소 글자 크기 설정값, M2-12에서 업스케일링 켠 채 확인, 안 되면 일반 HUD 사용 |
| 손 그리기 패스·가장자리 효과가 HDRP 전용이라 URP 전환 비용 증가 (SDD R-23) | architect | 낮음 | DES-114에 격리, M6-11 재평가 자료에 포함 |
| 업스케일링이 판정 기본 경로가 되어 GPU 종류(DLSS/FSR)에 따라 측정 비교가 어려움, HDRP 17.3의 품질 모드 지원 조건 확인 필요 (SDD R-19) | architect | 성능 판정 신뢰도 | M1-06에서 확인, PC별 GPU·업스케일러 기록, RTX 3060 기준은 DLSS로 판정 |
| M2 범위 증가 (M1에서 15개 이동) | requirements-analyst | M2 일정 지연 | M2 착수 전 작업량 재점검 |
| 외부 테스트 빌드를 M9a~M10 전에 내보내면 지역 추가마다 새 생성기 버전 필요 (SDD R-12) | architect | 생성기 유지 비용 | 외부 테스트 시점 결정 시 함께 고려 |
| 권한 설정(SUG-018) 보류로 보관함 털기·남의 건축물 철거 같은 그리핑은 강퇴·차단으로만 대응 | requirements-analyst | 공개 세션에서 협동 경험 훼손 | 공개 범위 기본값 "친구"(C-29), 다음 버전에서 SUG-018 재검토 |
| 8인 60 FPS가 문서상 가장 큰 기술 위험 (호스트가 렌더링·AI 60체·2만 오브젝트·7명 검증을 16.7ms 안에) | requirements-analyst | M1 측정 미달 시 범위 조정 필요 | M1 측정 후 업스케일링 허용·동시 적 상한 하향·기준 프리셋 하향 중 사용자 결정 (C-52) |
| 절차적 지형의 실사 품질 저하 | 총괄 | HDRP 실사 목표 미달 | 침식·식생 분포 규칙 공들여 설계, 손으로 만든 지형 조각 혼용 검토 |
| 시드 결정론 실패 (호스트와 참가자 지형 불일치) | 총괄 | 동기화 오류, 위치 어긋남 | PC마다 결과가 같은 생성 코드, 생성 결과 해시 비교 |
| 생성 결과가 플레이 불가능한 시드 | 총괄 | 진행 막힘 | 생성 검증 규칙(도달 가능성, 등광탑 자리, 필수 재료 수량)과 자동 시드 테스트 |
| 8인 60 FPS 목표: 호스트는 렌더링과 서버 처리(적 AI 최대 60, 자원 2만 개 동기화)를 동시에 해야 함 | 총괄 | RTX 3060급 HDRP 실사 1080p에서 목표 미달 가능 | 업스케일링(DLSS/FSR) 활용, 적 AI·동기화 부하 최적화, M1 기술 검증에서 조기 측정 |
| 확인 필요 39건 (부록 C) | requirements-analyst | 일부 FR 판정 불가 | SDD 전 필수(C-01, C-02, C-35)부터 결정, 나머지는 해당 마일스톤 전 결정 |
| PlayMode 시험(TransportSelectorTests) 실행 때마다 Console에 FishNet "SpawnablePrefabs is null" 오류가 최대 5건 찍힘 (AddComponent 순간 OnValidate가 출력, 시험 판정은 LogAssert로 무시해 통과) | 사용자 보고 2026-11-03, 총괄 분석 | 낮음: 결함 아님, 진짜 오류와 헷갈릴 수 있음 | 사용자 결정: 현 상태 유지(2026-11-03). M1-07에서 NetworkManager 프리팹을 만들 때 SpawnablePrefabs(DefaultPrefabObjects) 지정 필수, 시험도 그 프리팹 사용으로 전환 검토 |
| FSR2 경로(AMD·Intel GPU)는 실기기 없이 선택 규칙 단위 시험만 | developer (M1-06) | 다른 GPU에서 업스케일러 실패 늦게 발견 | 쓸 수 없으면 끄고 이유 기록하도록 구현. 다른 GPU 확인은 M6-10 |
| Bench 씬의 서버 틱은 가짜 작업이라 서버 처리 예산 판정 불가 | developer (M1-05) | M1-12 전까지 서버 예산 판단 못 함 | M1-09·M1-11·M1-12에서 실제 작업으로 교체 |
| HDRP가 lodBias를 애셋 값으로 덮어써서 프리셋 LOD 값은 GraphicsAssetBuilder가 애셋에 구워 넣음 | developer (M1-06) | 프리셋 값을 바꾸고 메뉴를 다시 실행하지 않으면 반영 안 됨 | M2-01 옵션 화면 작업 때 다시 확인 |
| SDD 3.4.2 표와 시험 asmdef 참조가 다름 (Game.Tests.WorldGen 신설, Infrastructure·Integration 참조 추가) | developer (M1 묶음 2) | 문서와 실제 불일치 | 해소: SDD v0.9 (2026-11-03) |
| `unity command build run`은 Unity 기본 빌드라 BuildCommands의 산출물 검사·steam_appid.txt 복사·Bench 포함이 빠질 수 있음. 출시 빌드(BuildWindowsRelease)는 열린 에디터에서 아직 미시험 | architect (SDD v0.9) | 출시 빌드에 Pipeline 런타임이 섞이거나 Steam 초기화 실패를 늦게 발견 | 빌드는 BuildCommands 메뉴(invoke_menu/script run)로, Console [Build] 기록 확인. M1-13에서 출시 빌드 재확인 |
| 프로토콜 변환 코드를 직접 작성하면서 생기는 결함(쓰기·읽기 불일치, 경계값, 잘못된 입력에서 디코더 예외) (SDD R-30) | architect (SDD v0.10) | 동기화 오류·접속 끊김·악성 패킷 취약 | 왕복·고정 바이트·거부 시험, 예외 없는 TryDecode, Check 보안 점검 대상 |
| 바이트 래퍼·RPC 방식 비용(머리말·복사·GC 할당), 서버 쪽 스태미나 추정 불일치로 정상 달리기 되돌림 (SDD R-31) | architect (SDD v0.10) | 대역폭·프레임 저하, 조작감 저하 | M1-07에서 FishNet Broadcast 바이트 필드 할당 확인, MoveStaminaGrace 0.5초, M1 로그로 조정 |
| 스태미나 0일 때 공격 허용 여부 미정 (SRS·SDD 모두 명시 없음, 「불이익 없음」이면 허용으로 읽힘) | 총괄 (문서 대조) | 공격 구현 마일스톤에서 해석 갈림 | 공격 구현 전 사용자 확인 |
| 한 PC 8프로세스 측정에서 화면 없는 클라이언트 1% Low 28 FPS·예산 위반 로그 | developer (M1 묶음 3) | M1 성능 판정 왜곡 | M1-12 대체 측정에서 봇 CPU 점유와 함께 별도 해석 |
| 에디터 HUD(월드 진입 후 스태미나 호) 미확인: MCP 클릭 시뮬레이션이 UI Toolkit 버튼에 전달 안 됨. 로비 「준비됨」 글자 대비 낮아 보임 | developer (M1 묶음 3) | UI 결함 늦게 발견 | 사용자 수동 확인, 필요 시 Base.uss 색 토큰 조정 |
| Steam 「친구 전용」 로비는 로비 검색 목록에 나오지 않음(Steam 정상 동작) | developer (M1-07) | 없음 | 해소: 사용자 확인 2026-11-04, 정상 동작이며 공개 목록은 「공개」 방만 |
| 게임 버전 엄격 일치로 개발 빌드는 다시 빌드할 때마다 서로 접속 불가, 에디터 ↔ 플레이어 접속도 거부 · 사이클 시작 때 MINOR 올리기 누락 가능 · Windows 플레이어에서 bundleVersion의 + 처리 미확인 (SDD R-35) | architect (SDD v0.12) | 개발 중 다중 접속 시험 불편, 버전 번호 오류 | 다중 프로세스 시험은 같은 빌드 폴더 복사 사용. **총괄 규칙: 새 사이클의 첫 Do 작업에서 MINOR를 올림(사이클 2는 0.2.0)**. + 처리는 마무리 개발 빌드에서 확인 |
| uGUI 전환 비용·회귀, TMP 한글 글꼴(아틀라스 크기, 동적 추가 순간 정지, Steam 이름의 중국어·일본어·이모지 □ 표시), 「오브젝트가 원본」 원칙의 작업 손실·병합 충돌, 손목 계기 캡처 카메라 비용, 캔버스 재빌드 성능 (SDD R-36~R-40) | architect (SDD v0.13) | M1·M2 일정, 표시 품질, 성능 | 기존 시험 유지 + UiPrefabRulesTests·UiLayerPlayTests, Pretendard 도입(사용자 작업), CJK 대체 글꼴은 사용자 결정, 정적·동적 캔버스 분리 |
| 개발 전용 한글 대체 글꼴(OS 맑은 고딕, 메모리 전용)이 플레이어 빌드에서도 동작하는지 미확인, 다른 OS에서는 □ · 키보드 Esc로 ESC 메뉴 열기는 MCP 키 흉내가 안 먹어 미확인 · 처음 만드는 경로(새 PC에서 Boot 씬 생성) 미실행 | developer·debugger (uGUI 전환) | 한글 미표시, 입력 경로 결함 늦게 발견 | Pretendard 도입(사용자 작업), 사용자 수동 확인, M1-13 개발 빌드에서 확인 |
| 호스트 시작 때 월드 생성이 메인 스레드를 약 0.6초 막음(이 PC), 느린 PC는 더 김 | developer (M1-10 1차) | 시작 시 잠깐 멈춤 | M1-10 2차에서 로딩 표시, M1-12에서 측정 |
| 참가자 월드 생성 작업은 취소할 수 없음(도중에 나가면 결과만 버림) | developer (M1-10 1차) | 나간 뒤에도 잠시 CPU 사용 | 낮음, M2 세션 관리 때 재검토 |
| 지형 36타일 동기 생성 시간 Full 규모 미확인, 월드 중앙 스폰 지점이 물·급경사일 수 있음 | developer (M1-10 2차) | 시작 시 멈춤, 스폰 불편 | 사용자 수동 시험으로 확인, 길면 여러 프레임 분할 |
| 생물 위치 압축(`Quantize.PositionToU16`, `CreatureState`)이 원점 중심 ±1536 m 기준이라 0~3072 월드에서 절반이 잘림 | debugger (M1-10 결함 수정) | M1-11 생물 위치 오류 | M1-11에서 developer가 함께 수정 |
| 자원 5,000개를 한 번에 바꿀 때 FPS 순간 24(에디터, 호스트) | 사용자 수동 시험 (M1-10) | 대량 변경 순간 끊김 | M1-12에서 측정, 필요하면 델타 적용·자원 뷰 재선택을 여러 프레임에 나눔 |
| 흐름장 재계산 4~10 ms(에디터)로 워커 예산 2.5 ms 초과 | developer (M1-11 1차) | 호스트 프레임 부담 | M1-12 빌드 측정, 넘으면 바뀐 흐름장만 계산·창 반경 축소. 2026-11-10 IMP-018 전용 표식으로 빌드 측정: 평균 1.7~2.3·최대 3.1~4.6 ms(예산 가끔 초과), 틱 최악 2.3~3.3 ms → 관찰 유지 |
| 보행 격자가 월드 중심 600 m만 있어 생물이 밖으로 나가면 막힘 | developer (M1-11 1차) | 넓은 지역 AI 불가 | M4 전에 월드 전체 격자 |
| CreatureSnapshot 배열을 틱마다 새로 할당(참가자당 30 Hz) | developer (M1-11 2차) | 8인·수백 체에서 GC 끊김 | M1-12에서 측정, 필요하면 풀링 |
| `-testcreatures`·메뉴를 여러 번 쓰면 생물이 누적됨 | developer (M1-11 2차) | 시험 조건 흐려짐 | 세션당 한 번만 사용(안내) |
| 화면 없는 봇(-batchmode/-nographics) 시작 크래시 | developer (M1-12 1차) | 측정 조건 왜곡 | 해소: UiSafeArea16x9 재귀 수정(2026-11-10). Null 장치에서 수렴하지 않은 이유는 미확인 |
| 중도 참가 「연결」 구간 10.3초(창 모드 점검 실행) | developer (M1-12 1차) | NFR-018 여유 감소 | 화면 없는 점검에서 0.20초로 해소 추정, 2차 측정에서 재확인 |
| 커버리지 계측이 도메인 재로드 때 꺼지고 `StopRecording`이 NullReference, 계측 중 실패 1건 이름 미확인 | test-engineer (Act 회귀) | 커버리지 수치 갱신 불가(기준선 62.1% 유지) | 다음 측정 전에 계측 방식 변경, 결과를 파일로 저장해 Failed 항목 확인 |
| 이 PC의 `core.hooksPath`가 비어 commit-msg 훅 꺼짐, `tools/git/`에 LFS 훅 4개 untracked | test-engineer (Act 회귀) | 커밋 규칙 자동 검사 없음, hooksPath를 바꾸면 LFS 훅 누락 위험 | 사용자 결정 대기: LFS 훅 커밋 후 `git config core.hooksPath tools/git` |
| 자동 시험으로 확인 못 한 항목(SEC-002 실측 MP-09, 2PC·릴레이 MP-01·02, 출시 빌드 거부 실동작) | test-engineer (Act 회귀) | 보안·접속 확인 공백 | IMP-014 수동 시험, 다음 출시 빌드 때 확인, 사이클 2 PlayMode 후보 |

## 측정
| 사이클 | FR 수 | NFR 수 | TC 수 | 통과율 | 결함 수 | IMP 처리율 |
|---|---|---|---|---|---|---|
| 1 (SRS v0.1) | 114 | 43 | - | - | - | - |
| 1 (SRS v0.2) | 170 | 55 | - | - | - | - |
| 1 (SRS v0.3 / SDD v0.1) | 170 | 55 | - | - | - | - |
| 1 (Plan 완료: SRS v0.5 / SDD v0.3 / STORYBOARD v1.0) | 170 | 57 | - | - | - | - |
| 1 (SRS v0.6 / SDD v0.4 / STORYBOARD v1.1) | 172 | 57 | - | - | - | - |
| 1 (SRS v0.8 / SDD v0.6 / STORYBOARD v1.3) | 179 | 57 | - | - | - | - |
| 1 (Do M1 묶음 1) | 179 | 57 | EditMode 157 + PlayMode 5 (개발자 단위·아키텍처 시험) | 100% (162/162) | 0 | - |
| 1 (Do M1 묶음 2) | 179 | 57 | EditMode 282 + PlayMode 10 (개발자 단위·아키텍처 시험) | 100% (292/292) | 0 | - |
| 1 (Do M1 묶음 3) | 179 | 57 | EditMode 476 + PlayMode 16 (개발자 단위·통합·아키텍처 시험) | 100% (492/492) | 0 | - |
| 1 (Do M1 묶음 3 마무리: ESC 결함·SemVer) | 179 | 58 | EditMode 541 + PlayMode 16 | 100% (557/557) | 1 (ESC 메뉴 버튼 무반응, 수정됨) | - |
| 1 (Do M1 묶음 3 마무리: uGUI 전환) | 179 | 58 | EditMode 555(1 건너뜀) + PlayMode 22 | 100% (576/576, 건너뜀 제외) | 1 (PlayMode 대체 글꼴 죽은 참조, 수정됨) | - |
| 1 (Do M1-10 1차) | 179 | 58 | EditMode 577 + PlayMode 23 | 100% (600/600) | 0 | - |
| 1 (Do M1-10 2차) | 179 | 58 | EditMode 584 + PlayMode 25 | 100% (609/609) | 0 | - |
| 1 (Do M1-10 결함 수정: 월드 좌표 범위) | 179 | 58 | EditMode 585 + PlayMode 25 | 100% (610/610) | 1 (이동 거부로 플레이어 멈춤, 수정됨) | - |
| 1 (Do M1-10 결함 수정: 빌드 지형) | 179 | 58 | EditMode 586 + PlayMode 25 | 100% (611/611) | 1 (빌드에서 지형 안 보임, 수정됨) | - |
| 1 (Do M1-11 1차) | 179 | 58 | EditMode 624 + PlayMode 25 | 100% (649/649) | 0 | - |
| 1 (Do M1-11 2차) | 179 | 58 | EditMode 642 + PlayMode 26 | 100% (668/668) | 0 | - |
| 1 (Do M1-12 1차) | 179 | 58 | EditMode 662 + PlayMode 26 | 100% (688/688) | 1 (화면 없는 실행 시작 크래시, 수정됨) | - |
| 1 (Do M1-12 측정) | 179 | 58 | EditMode 662 + PlayMode 26, 성능 P8·P1 판정 항목 전부 통과 | 100% | 0 (신규) | - |
| 1 (Do M1-13, M1 완료) | 179 | 58 | EditMode 663 + PlayMode 26 (+ 아키텍처 약 35건 포함) | 100% (689/689) | 누적 5 (모두 수정: ESC 메뉴, 이동 거부, 빌드 지형, 화면 없는 실행 크래시, PlayMode 글꼴 참조) | - |
| 1 (Check) | 179 | 58 | TC 66 (EditMode 671 + PlayMode 26, 수동 MP-01~09) | TC 통과 44/66, 자동 시험 691/697 (실패 6 = 결함 재현) | 5 (DEF-001~005, 미해결) + REV 15 + SEC 10 | 0% (IMP 18 제안) |
| 1 (Act 회귀) | 179 | 58 | TC 68 (EditMode 705 + PlayMode 26, 수동 MP-01~09) | 자동 시험 100% (731/731), TC 통과 53/68 (부분 8·차단 6 수동·대기 1) | 12 (DEF-001~012 모두 해결, 006~011 소급 등록) | 승인 12/12 (100%), 전체 12/18 (보류 5, 수동 대기 1) |

설계 측정 (SDD v0.1): 설계 요소 101개, FR·NFR 추적 225/225 (100%), 구현 작업 96개 (M1 18, M2 6, M3 15, M4 9, M5 9, M6 10, M7 4, M8 3, M9a 6, M9b 5, M9c 5, M10 6)
설계 측정 (SDD v0.6, 승인): 설계 요소 106개, FR·NFR 추적 236/236 (100%), 구현 작업 103개 (M4 13개로 증가). 화면 38개 (STORYBOARD v1.3)
설계 측정 (SDD v0.4, 승인): 설계 요소 104개, FR·NFR 추적 229/229 (100%), 구현 작업 99개 (M2 13개로 증가)
디자인 측정 (STORYBOARD v1.0): 화면 37개 (SCR-001~037), UI 성격 FR 모두 화면 연결, 잠정 가정 18건, 결정 대기 항목은 마일스톤별로 7.4에 정리
설계 측정 (SDD v0.3, 승인): 설계 요소 102개, FR·NFR 추적 227/227 (100%), 구현 작업 97개
설계 측정 (SDD v0.2, 승인): 설계 요소 102개, FR·NFR 추적 225/225 (100%), 구현 작업 97개 (M1 13, M2 11, M3 15, M4 9, M5 9, M6 11, M7 4, M8 3, M9a 6, M9b 5, M9c 5, M10 6), asmdef 13개

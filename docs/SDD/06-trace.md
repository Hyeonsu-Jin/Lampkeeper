> SDD(소프트웨어 설계 명세서)의 일부 · 목차: [00-index.md](00-index.md)
> **자동 생성 파일입니다. 직접 고치지 마세요.** 연결은 [04-elements.md](04-elements.md)의
> 「관련 FR/NFR」 열, 비고는 [06-trace-notes.md](06-trace-notes.md)에서 가져옵니다.
> 다시 만들기: `python tools/docs/trace.py build`

## 6. 요구사항 추적표


### 6.1 기능 요구사항

| FR | DES ID | 비고 |
|---|---|---|
| FR-001 | DES-054, DES-055, DES-102 | 세션 열기 시 참가 조건 선택(v0.3) |
| FR-002 | DES-054, DES-102 | 초대 대화상자·콜백 |
| FR-003 | DES-031, DES-037, DES-054, DES-072, DES-085, DES-102 | 참가 경로 ↔ 참가 조건. v0.12: 버전 검사 = 프로토콜·게임 버전 전체 문자열·ContentHash·생성기 버전(3.7.12 (4)) |
| FR-004 | DES-031, DES-054, DES-085 | 접속 + 예약 < 8 |
| FR-005 | DES-101, DES-121 | FishySteamworks(SDR). v0.10: 바이트 전달은 DES-121 |
| FR-006 | DES-101, DES-103 | `-transport`, 출시 빌드 노출 안 함 |
| FR-007 | DES-001, DES-050, DES-081 | 4개 검증 항목 |
| FR-008 | DES-053, DES-072, DES-082, DES-083 | 생물은 관심 범위(기본 300 m) 안에서 같게(확정 2026-11-02) |
| FR-009 | DES-053, DES-072, DES-082, DES-085 | 3.9.2 |
| FR-010 | DES-021, DES-064 | 피해 대상에서 플레이어 제외 |
| FR-011 | DES-054, DES-056 |  |
| FR-012 | DES-030, DES-042, DES-043, DES-055 | 참가 조건 포함(v0.3) |
| FR-013 | DES-055, DES-098, DES-099 |  |
| FR-014 | DES-055, DES-098 |  |
| FR-015 | DES-001, DES-002, DES-056, DES-098 | 호스트 특수 ID 없음 |
| FR-016 | DES-002, DES-056 |  |
| FR-017 | DES-002, DES-033, DES-056 | 시작 아이템 C-05 |
| FR-018 | DES-055, DES-099, DES-111 |  |
| FR-019 | DES-055, DES-099 |  |
| FR-020 | DES-055, DES-099 |  |
| FR-021 | DES-030, DES-106 |  |
| FR-022 | DES-055, DES-056, DES-098, DES-099 | 단일 파일 |
| FR-023 | DES-002, DES-084, DES-086 |  |
| FR-024 | DES-002, DES-003, DES-052, DES-060, DES-084, DES-086 | v0.10: 스태미나 0이면 걷기 속도. 호스트는 `AvatarMoveValidator`로 속도 확인(3.11.12) |
| FR-025 | DES-002, DES-003, DES-052, DES-060, DES-086 | v0.10 (SRS v0.10): 0이 돼도 불이익 없음(달리기만 걷기 속도), 회복 지연 1초 확정 → C-04 스태미나 부분 해소, 교체 지점(DES-033) 아님. 허기·갈증·피로도·혹한 불이익은 C-04 미결로 FR-027~029·FR-105에 남음 |
| FR-026 | DES-003, DES-004, DES-060 |  |
| FR-027 | DES-003, DES-033, DES-060 |  |
| FR-028 | DES-003, DES-033, DES-060 |  |
| FR-029 | DES-003, DES-033, DES-060 |  |
| FR-030 | DES-004, DES-033, DES-060 |  |
| FR-031 | DES-004, DES-033, DES-060 |  |
| FR-032 | DES-057, DES-060 |  |
| FR-033 | DES-014, DES-090, DES-114 |  |
| FR-034 | DES-090, DES-114 |  |
| FR-035 | DES-015, DES-090, DES-114 |  |
| FR-036 | DES-006, DES-090, DES-114 |  |
| FR-037 | DES-021, DES-033, DES-064 | C-19 |
| FR-038 | DES-005, DES-057 |  |
| FR-039 | DES-005, DES-009, DES-057 |  |
| FR-040 | DES-005, DES-057 |  |
| FR-041 | DES-009, DES-033, DES-057 | C-10 |
| FR-042 | DES-006, DES-057, DES-084 |  |
| FR-043 | DES-006, DES-066 |  |
| FR-044 | DES-008, DES-033, DES-057 | C-08 |
| FR-045 | DES-008, DES-033, DES-057 | C-09 |
| FR-046 | DES-008, DES-057 |  |
| FR-047 | DES-008, DES-057, DES-087 |  |
| FR-048 | DES-009, DES-057 |  |
| FR-049 | DES-008, DES-057, DES-087 |  |
| FR-050 | DES-009, DES-033, DES-057 | C-09 |
| FR-051 | DES-008, DES-050, DES-057 | 도착 순서 처리 |
| FR-052 | DES-010, DES-033, DES-058, DES-091 | C-11 해소(v0.6). 제작 화면 = 독립 레시피북(3.7.10) |
| FR-053 | DES-010, DES-058 |  |
| FR-054 | DES-008, DES-058 |  |
| FR-055 | DES-021, DES-058, DES-064 |  |
| FR-056 | DES-010, DES-027 |  |
| FR-057 | DES-012, DES-059, DES-096 |  |
| FR-058 | DES-012, DES-059, DES-096 |  |
| FR-059 | DES-017, DES-033, DES-061 | C-13 |
| FR-060 | DES-017, DES-061 |  |
| FR-061 | DES-017, DES-061 |  |
| FR-062 | DES-017, DES-018, DES-033, DES-061, DES-063 | C-14 |
| FR-063 | DES-017, DES-018, DES-061 |  |
| FR-064 | DES-018, DES-033, DES-061 | C-15. 인원 배율은 적 수·보스 체력에만(확정) |
| FR-065 | DES-015, DES-017, DES-061 |  |
| FR-066 | DES-015, DES-020, DES-061, DES-063 |  |
| FR-067 | DES-015, DES-033, DES-061, DES-105 | 정화 구역 = 막힌 칸 |
| FR-068 | DES-015, DES-061 | 추가만 가능 |
| FR-069 | DES-017, DES-033, DES-061 | C-14 |
| FR-070 | DES-017, DES-061 |  |
| FR-071 | DES-015, DES-042, DES-061 |  |
| FR-072 | DES-017, DES-018, DES-061 |  |
| FR-073 | DES-018, DES-061 |  |
| FR-074 | DES-027, DES-061 |  |
| FR-075 | DES-020, DES-027, DES-061, DES-063 |  |
| FR-076 | DES-021, DES-064, DES-088 |  |
| FR-077 | DES-019, DES-020, DES-063, DES-088 |  |
| FR-078 | DES-009, DES-064, DES-088 |  |
| FR-079 | DES-019, DES-062, DES-088 |  |
| FR-080 | DES-019, DES-062, DES-088 |  |
| FR-081 | DES-019, DES-062, DES-088 |  |
| FR-082 | DES-020, DES-033, DES-062, DES-063 | C-18 |
| FR-083 | DES-022, DES-033, DES-065 | C-22 |
| FR-084 | DES-022, DES-033, DES-065 |  |
| FR-085 | DES-022, DES-033, DES-065 |  |
| FR-086 | DES-015, DES-022, DES-033, DES-065 |  |
| FR-087 | DES-022, DES-033, DES-065 |  |
| FR-088 | DES-009, DES-022, DES-065 |  |
| FR-089 | DES-012, DES-023, DES-065 |  |
| FR-090 | DES-012, DES-023, DES-065 |  |
| FR-091 | DES-023, DES-065 |  |
| FR-092 | DES-023, DES-065 |  |
| FR-093 | DES-023, DES-065 |  |
| FR-094 | DES-023, DES-033, DES-065, DES-091 | C-23. 화면 배치 「솥 도식 + 목록」으로 바뀌었으나 데이터는 같음(v0.4) |
| FR-095 | DES-023, DES-033, DES-065 |  |
| FR-096 | DES-024, DES-033, DES-065, DES-066 |  |
| FR-097 | DES-024, DES-065, DES-066 |  |
| FR-098 | DES-025, DES-026, DES-067 |  |
| FR-099 | DES-026, DES-067 |  |
| FR-100 | DES-026, DES-067 |  |
| FR-101 | DES-026, DES-067 |  |
| FR-102 | DES-016, DES-042, DES-043 |  |
| FR-103 | DES-027, DES-104 | 제작법 데이터 검사 |
| FR-104 | DES-016, DES-033, DES-066, DES-120 | C-21 |
| FR-105 | DES-016, DES-066, DES-120 |  |
| FR-106 | DES-016, DES-066, DES-090, DES-114, DES-120 |  |
| FR-107 | DES-006, DES-066 | 내구도 없음 |
| FR-108 | DES-024, DES-066 |  |
| FR-109 | DES-016, DES-066 |  |
| FR-110 | DES-028, DES-071 |  |
| FR-111 | DES-028, DES-071 |  |
| FR-112 | DES-018 |  |
| FR-113 | DES-028, DES-033, DES-071 | C-26 |
| FR-114 | DES-028, DES-071 |  |
| FR-115 | DES-031, DES-054, DES-085, DES-102 | 로비 없이 직접 연결 + 예약 확인. 예약 수를 로비 `kh_resv`로 공개, 접속 + 예약 = 8이면 가득 참(SRS v0.5) |
| FR-116 | DES-054 |  |
| FR-144 | DES-030, DES-031, DES-054, DES-102 | 명칭·월드별 저장·기본값 친구 전용(v0.3) |
| FR-145 | DES-037, DES-054, DES-102 | 로비 필터, 예약 자리 수 표시(`kh_resv`, SRS v0.5). v0.12: 참가 조건 「공개」 세션만 표시(친구 전용 로비가 검색에 안 나오는 것은 Steam 정상 동작, R-33 해소), 버전 일치 = `kh_proto`·`kh_build` 비교(3.7.12 (5)) |
| FR-146 | DES-031, DES-054, DES-085 | 비밀번호는 저장 안 함 |
| FR-147 | DES-031, DES-054 |  |
| FR-148 | DES-031, DES-033, DES-054, DES-110 | C-46 해소: 호스트의 모든 월드 공통, `bans.json` |
| FR-165 | DES-070, DES-102 |  |
| FR-117 | DES-073, DES-092 |  |
| FR-118 | DES-073, DES-086 |  |
| FR-119 | DES-073, DES-094 |  |
| FR-120 | DES-073, DES-109 |  |
| FR-121 | DES-073, DES-109 |  |
| FR-157 | DES-073, DES-093, DES-102 | 첫 실행 언어 = Steam 언어(C-48) |
| FR-166 | DES-092, DES-122 | v0.13: `KeycapView`(3.7.10 (3)·(6)) |
| FR-129 | DES-073, DES-094, DES-095, DES-122 | v0.13: 글자 크기 토큰 × 단계 배율(3.7.10 (2)) |
| FR-130 | DES-073, DES-095, DES-114, DES-122 | v0.13: UI 색 토큰·모양 구분 규칙은 `UiTheme`·`Themed*`(3.7.10 (1)·(3)) |
| FR-131 | DES-073, DES-095, DES-114 |  |
| FR-132 | DES-073, DES-095 |  |
| FR-122 | DES-027, DES-071, DES-074 |  |
| FR-123 | DES-035, DES-071 |  |
| FR-124 | DES-032, DES-071, DES-097 |  |
| FR-125 | DES-032, DES-097 |  |
| FR-126 | DES-032, DES-069, DES-097 |  |
| FR-127 | DES-034, DES-069 |  |
| FR-128 | DES-069, DES-091 |  |
| FR-159 | DES-074, DES-084, DES-097, DES-120 | 장치형 HUD: 머리 위 등불 이름표(체력 단계) |
| FR-160 | DES-074, DES-084, DES-091 |  |
| FR-133 | DES-005, DES-059 |  |
| FR-134 | DES-005, DES-059 |  |
| FR-135 | DES-011, DES-058 |  |
| FR-136 | DES-011, DES-058 |  |
| FR-137 | DES-011, DES-058 |  |
| FR-138 | DES-012, DES-059 |  |
| FR-139 | DES-012, DES-059 | C-12, C-54 |
| FR-161 | DES-013, DES-059 |  |
| FR-162 | DES-013, DES-033, DES-059 | C-42 |
| FR-140 | DES-013, DES-059 |  |
| FR-141 | DES-032, DES-097 |  |
| FR-142 | DES-004, DES-060 |  |
| FR-143 | DES-004, DES-060 |  |
| FR-149 | DES-030, DES-055 |  |
| FR-150 | DES-055, DES-099 |  |
| FR-151 | DES-055, DES-099, DES-106 |  |
| FR-152 | DES-055, DES-099 |  |
| FR-156 | DES-018, DES-029, DES-033 | C-47 |
| FR-153 | DES-017, DES-061 |  |
| FR-154 | DES-017, DES-061 |  |
| FR-155 | DES-017, DES-061 |  |
| FR-158 | DES-107 | 로컬 파일만(C-43) |
| FR-163 | DES-108 |  |
| FR-164 | DES-070, DES-102 |  |
| FR-171 | DES-073, DES-074, DES-091, DES-110, DES-120 | HUD 표시 방식 옵션(장치형 기본 / 일반 HUD), 재시작 없이 전환(3.7.11) |
| FR-172 | DES-073, DES-092, DES-114, DES-120 | 손목 계기 올림/내림, `ToggleWrist`, `WristRaiseMode` 기본 토글(C-58), 올린 동안 모든 행동 가능(C-59), 다른 창이 열리면 자동 내림, 게임 진행 유지. 표시 면 (B) 기본 |
| FR-167 | DES-030, DES-041 |  |
| FR-168 | DES-042, DES-043, DES-047, DES-089 |  |
| FR-169 | DES-040, DES-044, DES-046, DES-047, DES-072, DES-085 |  |
| FR-170 | DES-033, DES-045 | C-55 |
| FR-173 | DES-010, DES-012, DES-033, DES-058, DES-059 | 제작대(손 제작으로 만들어 배치), 확정 규칙 `CraftingStationRules`(v0.6) |
| FR-174 | DES-010, DES-058, DES-091 | `OpenRecipeBook(Station)` 팝업, `Hotkey`는 열람 + 손 제작만, 「제작대 필요」 표시(3.7.10) |
| FR-175 | DES-012, DES-036, DES-075, DES-091 | 용광로 칸·공유(보관함 동시 조작 규칙), 구독 방식 `FurnaceView` |
| FR-176 | DES-036, DES-075, DES-104 | 정수 연료 점수(나무 2, 석탄 16, 광석 1개 = 4점), 4점 미만이면 1개씩 태워 보충, 남은 점수는 꺼낼 수 없고 저장, 설정값 |
| FR-177 | DES-025, DES-033, DES-036, DES-052, DES-075, DES-098 | 완료 시각 힙, 떠나도 진행, 결과물 가득·연료 없음 정지, `FURN` 저장(진행 경과), 월드 닫힘·밤 건너뛰기 때 진행 안 함(`IFurnaceTimeRule`, C-65 잠정) |
| FR-178 | DES-036, DES-075 | 숯 작업 종류, 숯 연료 점수 16(C-62)·비율 나무 4개 + 연료 4점(C-63)·시간 설정값 |
| FR-179 | DES-008, DES-043, DES-045 | 생성 프로필 자원 표에 석탄 광맥(모든 생성 지역 후보, C-64), 생성 검증 진행 재료에 포함, 채광 규칙 재사용 |

### 6.2 비기능 요구사항

| NFR | DES ID | 비고 |
|---|---|---|
| NFR-001 | DES-003, DES-066, DES-074, DES-091, DES-120 | 환경 위협은 현재 상태 + 대응 버프 남은 시간만 표시, 누적 게이지 없음(SRS v0.5). 0.5초 갱신은 「표시되고 있는 동안」 기준(SRS v0.6, 3.7.11) |
| NFR-002 | DES-091, DES-092, DES-096, DES-122 | v0.13: uGUI 내비게이션·`UiInputRouter`(3.7.10 (6)) |
| NFR-003 | DES-001, DES-050, DES-091 | 3.7.5 사유 코드 |
| NFR-044 | DES-093 |  |
| NFR-004 | DES-092, DES-102, DES-122 | 화면 키보드. v0.13: 사용 불가 버튼도 포커스(`UiButton`) |
| NFR-005 | DES-108, DES-109 |  |
| NFR-006 | DES-100, DES-102 | Steam 실행 필수, 없으면 SCR-001(v0.7) |
| NFR-007 | DES-106 |  |
| NFR-008 | DES-101, DES-103 |  |
| NFR-009 | DES-102, DES-112 | `kh_game` 필터 |
| NFR-010 | DES-108, DES-109 |  |
| NFR-011 | DES-052, DES-062, DES-087, DES-105, DES-108, DES-109 | 3.12.3 |
| NFR-012 | DES-087, DES-108, DES-109 |  |
| NFR-013 | DES-053, DES-080, DES-082, DES-108, DES-121 | 3.12.5. v0.10: 메시지 바이트 길이를 코덱 시험으로 고정 |
| NFR-014 | DES-053, DES-080, DES-082, DES-108, DES-121 |  |
| NFR-015 | DES-052, DES-053, DES-082, DES-083, DES-108 | 틱 30 Hz |
| NFR-016 | DES-047, DES-053, DES-082, DES-087, DES-108 |  |
| NFR-017 | DES-020, DES-062, DES-063, DES-105, DES-108 |  |
| NFR-018 | DES-072, DES-108, DES-111 | 3.12.6 |
| NFR-019 | DES-055, DES-108, DES-111 |  |
| NFR-020 | DES-055, DES-099, DES-108, DES-111 |  |
| NFR-021 | DES-108, DES-113 | v0.7: M1은 이 PC 1대 + 봇 7개 대체 측정, 실제 8대 P8은 M6-10 |
| NFR-052 | DES-043, DES-047, DES-089, DES-111 |  |
| NFR-048 | DES-108 | 3.12.8 |
| NFR-051 | DES-113 |  |
| NFR-022 | DES-036, DES-055, DES-098 | v0.6: 용광로 칸 내용·남은 연료 점수·진행 경과(`FURN`) |
| NFR-023 | DES-002, DES-098 |  |
| NFR-024 | DES-055, DES-098, DES-099 |  |
| NFR-025 | DES-055, DES-098 |  |
| NFR-054 | DES-044, DES-046, DES-055 |  |
| NFR-026 | DES-055, DES-099 |  |
| NFR-027 | DES-112 |  |
| NFR-028 | DES-109, DES-112, DES-114 | HDRP 의존 코드 격리(3.4.4) |
| NFR-029 | DES-112 |  |
| NFR-030 | DES-001, DES-050, DES-052, DES-084 | 3.4.3 검사. v0.10: 아바타 자세도 호스트 검사를 통과한 것만 중계(3.9.8) |
| NFR-031 | DES-051, DES-080, DES-100, DES-101, DES-121 | v0.10: 전송 라이브러리 의존은 DES-121·DES-084에만 |
| NFR-049 | DES-101, DES-112 |  |
| NFR-032 | DES-051, DES-052, DES-100 | D-19 |
| NFR-033 | DES-007, DES-033, DES-104 |  |
| NFR-034 | DES-007, DES-093, DES-104 |  |
| NFR-056 | DES-091, DES-095, DES-122 | UI 접근성 기반(글자 크기 배율, 색 토큰, 색만으로 구분하지 않는 규칙)을 M1 첫 UI부터(3.7.10, SRS v0.5). v0.13: uGUI 테마 애셋 + 적용 컴포넌트 |
| NFR-057 | DES-091, DES-122 | 다크 단일 테마, 라이트 테마·테마 전환 구조 없음(3.7.10, SRS v0.5). v0.13: `UiTheme` 한 벌, 대비 검사 짝 |
| NFR-058 | DES-031, DES-037, DES-080, DES-100, DES-112 | (v0.12, SRS v0.11) 게임 버전 SemVer, 단일 출처 `bundleVersion`, 빌드 메타데이터, 참가 시 버전 확인, 표시 형식(3.7.12, D-26) |
| NFR-035 | DES-102, DES-112 |  |
| NFR-036 | DES-055, DES-099 |  |
| NFR-037 | DES-055, DES-098 |  |
| NFR-053 | DES-040, DES-046, DES-113 | v0.7: 다른 CPU 교차 검증은 M6-10(M6 전에는 `-genhash` 결과 수집으로 선택 실시) |
| NFR-038 | DES-054, DES-101 |  |
| NFR-039 | DES-054, DES-072 |  |
| NFR-040 | DES-050, DES-080, DES-081, DES-121 | v0.10: 디코드는 예외 없는 `TryDecode`, 실패는 DES-121이 버리고 로그 |
| NFR-041 | DES-050, DES-081 |  |
| NFR-042 | DES-069, DES-098, DES-107 |  |
| NFR-050 | DES-034, DES-069, DES-091, DES-122 | v0.13: 모든 TMP 글자·입력칸 `richText = false`(규칙 검사 3.4.3) |
| NFR-045 | DES-107 |  |
| NFR-046 | DES-025, DES-068, DES-075 | 3.7.8. v0.6: 용광로 가공 완료 포함(대상 C-66) |
| NFR-055 | DES-045, DES-113 |  |
| NFR-047 | DES-073, DES-110 |  |
| NFR-043 | DES-112, DES-113 |  |

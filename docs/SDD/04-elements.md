> SDD(소프트웨어 설계 명세서)의 일부 · 목차: [00-index.md](00-index.md)

## 4. 설계 요소 목록

종류: 모듈 / 엔터티 / 값 객체 / 서비스(규칙) / 인터페이스 / 유스케이스 / 컴포넌트(MonoBehaviour) / 데이터 / 도구

### 4.1 도메인 (Game.Domain, DES-001~035)

| DES ID | 이름 | 종류 | 레이어 | 설명 | 관련 FR/NFR |
|---|---|---|---|---|---|
| DES-001 | 도메인 공통 기반 | 값 객체 모음 | 도메인 | PlayerId(SteamID), ContentId, EntityId, Result·Rejection·ReasonCode, IDomainEvent | FR-007, FR-015, NFR-003, NFR-030 |
| DES-002 | PlayerCharacter | 엔터티 | 도메인 | 플레이어 진행 집합체: 상태 값, 위치, 생명 상태, 인벤토리·장비·버프·숙련도·튜토리얼·부활 지점·사망 위치 | FR-015~017, FR-023~025, NFR-023 |
| DES-003 | VitalsRules | 서비스(규칙) | 도메인 | 스태미나 소모(달리기·공격)·회복, 체력·허기·갈증·피로도 증감과 상한, 0일 때 불이익(허기·갈증·피로도는 교체 지점 `IZeroVitalPenaltyRule`, C-04 미결). v0.10 (SRS v0.10, 사용자 결정 2026-11-03) 스태미나 확정 규칙: 소모 행동을 멈춘 뒤 `스태미나 회복 지연`(**1초 확정**)이 지나야 `스태미나 회복량`으로 회복, **0이 돼도 불이익 없음**(달리기만 걷기 속도로, 그 외 불이익 없음). 같은 규칙을 클라이언트 이동(DES-086)과 호스트 스태미나 추정(DES-052 `AvatarMoveValidator`)이 함께 씀 | FR-024~029, NFR-001 |
| DES-004 | LifeStateRules | 서비스(규칙) | 도메인 | 쓰러짐 → 소생/사망, 사망 → 부활 위치 결정, 소생 중단 조건 | FR-026, FR-030, FR-031, FR-142, FR-143 |
| DES-005 | Inventory·ItemStack·Container | 엔터티 | 도메인 | 칸, 묶음 수, 이동·나누기, 가득 참, 보관함 칸(공용) | FR-038~040, FR-133, FR-134 |
| DES-006 | Equipment | 엔터티 | 도메인 | 손에 든 것, 착용 칸, 착용 효과 목록 | FR-042, FR-043, FR-107, FR-036 |
| DES-007 | ContentCatalog | 인터페이스(읽기 전용) | 도메인 | ItemDef·ResourceTypeDef 등 정의 조회 | NFR-033, NFR-034 |
| DES-008 | ResourceNode·HarvestRules | 엔터티 + 규칙 | 도메인 | 타격 수, 단계, 도구 종류·등급 판정, 쓰러짐·파괴, 손 채집, 동시 타격 합산. 석탄 광맥도 같은 채광 규칙(v0.6) | FR-044~047, FR-049, FR-051, FR-054, FR-179 |
| DES-009 | DropTable·DropItem | 서비스 + 엔터티 | 도메인 | 드롭 표 굴림(호스트 난수), 드롭 아이템, 줍기 1명 보장 | FR-039, FR-041, FR-048, FR-050, FR-078, FR-088 |
| DES-010 | CraftingRules | 서비스(규칙) | 도메인 | 제작법 판정, 재료 부족 사유, 첫 등광탑 설계도 기본 해금. v0.6: 확정 규칙 `CraftingStationRules`(손 제작 / 제작대 필요, 사유 `StationRequired`) | FR-052, FR-053, FR-056, FR-173, FR-174 |
| DES-011 | DurabilityRules | 서비스(규칙) | 도메인 | 내구도 소모, 망가짐, 수리(대응 장비 제외) | FR-135~137 |
| DES-012 | Structure·PlacementRules | 엔터티 + 규칙 | 도메인 | 건축물, 겹침·지형·거리·정화 구역 판정, 철거 회수(버림), 수리, 솥·보조 기구 배치. v0.6: 기능 건축물 목록(SRS v0.8 FR-057) = 제작대, 용광로, 보관함, 침대, 요리 솥·보조 기구, 등광탑. 용광로 철거 시 칸 내용 드롭(남은 연료 점수는 사라짐) | FR-057, FR-058, FR-089, FR-090, FR-138, FR-139, FR-173, FR-175 |
| DES-013 | SleepAndRespawnRules | 서비스(규칙) | 도메인 | 수면 회복, 피격 시 기상, 부활 지점 지정·해제, 밤 건너뛰기 4조건 | FR-140, FR-161, FR-162 |
| DES-014 | WorldClock | 엔터티 | 도메인 | 하루 길이, 낮 비율, 일수, 밤 여부 | FR-033 |
| DES-015 | PurifiedZoneSet | 엔터티 | 도메인 | 정화 구역 목록(추가만 가능), 위치 질의(격자 가속), 시작 정화 구역 | FR-065~068, FR-071, FR-035, FR-086 |
| DES-016 | RegionMap·ThreatRules | 서비스(규칙) | 도메인 | 위치 → 지역, 지역 위협(독기·혹한·어둠), 대응 수단 판정, 근원 중첩 | FR-102, FR-104~106, FR-109 |
| DES-017 | LightTower·DefenseSession | 엔터티(상태 기계) | 도메인 | 비활성 → 준비 → 웨이브 → 성공/실패, 준비 상태, 취소 권한, 등광탑 체력, 실패 손실 | FR-059~063, FR-065, FR-069, FR-070, FR-072, FR-153~155 |
| DES-018 | WaveComposer | 서비스(규칙) | 도메인 | 웨이브 구성, 보스 선택(종류·지역), 인원·난이도 배율 | FR-062~064, FR-072, FR-073, FR-112, FR-156 |
| DES-019 | Creature·CreatureBrain | 엔터티 + 규칙 | 도메인 | 생물 정의, 모드(상태 기계), 판단 규칙(대상 선택·공격 개시), 지역 변형 | FR-077, FR-079~081 |
| DES-020 | SpawnRules | 서비스(규칙) | 도메인 | 동시 적 상한, 웨이브 우선 대기열, 정화 구역 생성 금지, 밤 습격·동물 생성 조건 | FR-066, FR-075, FR-077, FR-082, NFR-017 |
| DES-021 | CombatRules | 서비스(규칙) | 도메인 | 피해 계산(도구·버프·숙련도 보정), PvP 없음, 광원 퇴치(교체 지점) | FR-010, FR-037, FR-055, FR-076 |
| DES-022 | Crop·FarmingRules | 엔터티 + 규칙 | 도메인 | 심기, 물 유지, 생장 누적(물·햇빛 조건), 수확·재생장 | FR-083~088 |
| DES-023 | CookingRules | 서비스(규칙) | 도메인 | 기구 인식 거리, 조리 방식 해금, 요리법별 기구 조합, 가능 목록 | FR-089~095 |
| DES-024 | BuffSet | 엔터티 | 도메인 | 활성 버프, 남은 시간, 만료, 중첩(교체 지점), 위협 저항 효과 | FR-096, FR-097, FR-108 |
| DES-025 | ActionCompleted (경험치 이벤트) | 도메인 이벤트 | 도메인 | 행동 종류·SteamID·대상 종류. 호스트 확정 행동에서만 발행. v0.6: 가공(`Smelt`) 잠정 추가 | NFR-046, FR-098, FR-177 |
| DES-026 | Proficiency | 엔터티 | 도메인 | 경험치, 레벨, 스탯 포인트, 획득 노드, 선행 조건(모델은 C-06) | FR-098~101 |
| DES-027 | ProgressionState | 엔터티 | 도메인 | 해금(정화 성공 조건만), 월드 목표 순서, 정화 후 지도·자원 활성화 표시 | FR-056, FR-074, FR-075, FR-103, FR-122 |
| DES-028 | StoryState | 엔터티 | 도메인 | 도입 표시 여부(플레이어별), 공개 단서, 결말 여부, 하늘 열림 | FR-110, FR-111, FR-113, FR-114 |
| DES-029 | DifficultyMultipliers | 값 객체 | 도메인 | 상태 감소·적 체력·적 피해·실패 손실 배율, 프리셋(쉬움·보통·어려움), 손실 비율 100% 상한 | FR-156 |
| DES-030 | WorldSettings | 엔터티 | 도메인 | 이름, 시드 문자열·값, 생성기 버전, 난이도, **참가 조건(초대 전용/친구 전용/공개, 기본 친구 전용, SRS v0.3)**, Cloud 켬/끔 | FR-012, FR-021, FR-144, FR-149, FR-167 |
| DES-031 | SessionRules | 서비스(규칙) | 도메인 | 인원(접속 + 예약) 제한, 재접속 예약, 참가 경로 ↔ 참가 조건, 비밀번호 시도 제한, 차단 목록(호스트의 모든 월드 공통, `IBanListStore`로 읽음, C-46), 강퇴 권한. v0.12: 참가 호환 검사(프로토콜·게임 버전 전체 문자열·ContentHash·생성기 버전, 3.7.12 (4)) — 게임 버전은 `GameVersion.IsSameBuild`(DES-037) | FR-003, FR-004, FR-115, FR-144, FR-146~148, NFR-058 |
| DES-032 | MarkerRules | 서비스 + 엔터티 | 도메인 | 핑(개수 상한·유지 시간·대상 종류), 사망 위치, 탐험 기록(공유 여부 교체 지점) | FR-124~126, FR-141 |
| DES-033 | 규칙 교체 지점 | 인터페이스 모음 | 도메인 | 미정 규칙(C 항목)별 인터페이스 + 잠정 구현, 선택값으로 교체(3.7.6). v0.10: FR-025(스태미나)는 C-04 스태미나 부분 해소로 관련 FR에서 뺌 | NFR-033, FR-027~031, FR-037, FR-041, FR-044, FR-045, FR-050, FR-052, FR-059, FR-062, FR-064, FR-067, FR-069, FR-082, FR-083~087, FR-094~096, FR-104, FR-113, FR-148, FR-156, FR-162, FR-170, FR-173, FR-177, FR-017 |
| DES-034 | TextRules | 서비스(규칙) | 도메인 | 길이 상한, 제어 문자 제거, 빈 메시지 거부, 채팅 빈도 | FR-127, NFR-050 |
| DES-035 | TutorialProgress | 엔터티 | 도메인 | 생존 튜토리얼 단계(이동 → 채집 → 제작 → 벌목·채광 → 건축 → 섭취), 건너뛰기 | FR-123 |
| DES-036 | FurnaceState·FurnaceRules | 엔터티 + 규칙 | 도메인 | 용광로 칸(연료·재료·결과물), 연료 점수(정수, `FuelPoints`), 작업 시작·완료·정지 규칙(결과물 가득·연료 없음), 숯 만들기. 비율·시간은 설정값. v0.6 신설 | FR-175~178, NFR-022 |
| DES-037 | GameVersion | 값 객체 | 도메인 | SemVer 2.0.0 게임 버전(`Major`·`Minor`·`Patch` 0~65535, `PreRelease`, `BuildMetadata`, `Core`, `Full`, `Channel`), 엄격한 `TryParse`(64바이트 이하, 예외 없음), 같은 빌드 판정 `IsSameBuild`(전체 문자열 서수 비교), 표시 `ToDisplayString`(출시 `v{Core}`, 그 밖 `v{Full}`), 빌드 메타데이터 조립 `ComposeBuildMetadata`. 우선순위 비교는 없음. `Domain/Common/`. v0.12 신설(3.7.12 (3)) | NFR-058, FR-003, FR-145 |

### 4.2 월드 생성 (Game.WorldGen, DES-040~047)

| DES ID | 이름 | 종류 | 레이어 | 설명 | 관련 FR/NFR |
|---|---|---|---|---|---|
| DES-040 | DetMath | 모듈 | 도메인(생성) | 고정소수점, Mix64·XXH64 자체 구현, 카운터 기반 난수, 정수 노이즈 | NFR-053, FR-169 |
| DES-041 | SeedCodec | 서비스 | 도메인(생성) | 시드 문자열 → u64, 무작위 시드 문자열 | FR-167 |
| DES-042 | WorldLayout | 데이터 모델 | 도메인(생성) | 고정 큰 배치(지역 순서·경계·높이 기반), 시작 지점, 마지막 등불, 장막의 근원 도장, 고정 자원 | FR-012, FR-071, FR-102, FR-168 |
| DES-043 | RegionGenerator (버전별) | 모듈 | 도메인(생성) | 지형·침식·물·경계 보정·자원 배치·등광탑 후보 자리 생성. v0.6: 생성 프로필 자원 표에 석탄 광맥 | FR-012, FR-102, FR-168, FR-179, NFR-052 |
| DES-044 | GeneratorRegistry | 서비스 | 도메인(생성) | 생성기 버전 → 구현, 최신 버전, 미지원 판정, 동결 규칙 | FR-169, NFR-054 |
| DES-045 | GenerationValidator·IGenRetryPolicy | 서비스 + 인터페이스 | 도메인(생성) | FR-170 4조건 검사, 결정론적 재생성 정책(C-55 교체 지점) | FR-170, NFR-055, FR-179 |
| DES-046 | WorldHasher | 서비스 | 도메인(생성) | 전체·지역별·타일별 요약값 | FR-169, NFR-053, NFR-054 |
| DES-047 | GeneratedWorld | 데이터 | 도메인(생성) | 정수 높이, 물·지역 마스크, 자원 생성 표(ResourceId·Index), 후보 자리, 질의(높이·경사) | FR-168, FR-169, NFR-016, NFR-052 |

### 4.3 애플리케이션 (Game.Application, DES-050~075, DES-080)

| DES ID | 이름 | 종류 | 레이어 | 설명 | 관련 FR/NFR |
|---|---|---|---|---|---|
| DES-050 | 명령 파이프라인 | 모듈 | 애플리케이션 | ICommand, 처리기(검증 + 실행), CommandDispatcher, RequestGuard(값 범위·토큰 버킷) | FR-007, NFR-003, NFR-040, NFR-041, FR-051, NFR-030 |
| DES-051 | 포트 모음 | 인터페이스 | 애플리케이션 | 3.7.3의 포트. v0.10: `INetServer`/`INetClient`는 Protocol 메시지·`NetChannel`만 다룸(FishNet 형식 없음), `ILocalPlayerPose`와 단순 보관 구현 `LocalPlayerPoseBuffer` 추가 | NFR-031, NFR-032 |
| DES-052 | ServerWorld·TickScheduler | 모듈 | 애플리케이션 | 서버 시뮬레이션 루트, 틱 순서, 서버 처리 측정 표식. 로컬 플레이어 비의존. v0.10: 아바타 이동 검사기 `AvatarMoveValidator`(연결별 마지막 수락 자세, 속도·범위 검사, 서버 쪽 스태미나 추정, 3.11.12) | NFR-011, NFR-030, NFR-032, FR-024, FR-025, FR-177, NFR-015 |
| DES-053 | 복제 모델 | 모듈 | 애플리케이션 | ReplicatedTable, 변경 기록, 스냅샷, 관심 범위 정책(등급 표) | FR-008, FR-009, NFR-013~016 |
| DES-054 | SessionService | 유스케이스 | 애플리케이션 | 세션 열기(참가 조건 선택·월드에 저장), 참가 판정, 나가기, 강퇴, 차단·해제, 참가 조건 변경, 재접속 예약(생성·해제 시 로비 메타데이터 `kh_count`·`kh_resv` 갱신), 종료 예고, 연결 끊김 처리 | FR-001~004, FR-011, FR-115, FR-116, FR-144~148, NFR-038, NFR-039 |
| DES-055 | WorldLifecycle | 유스케이스 | 애플리케이션 | 새 월드(참가 조건 포함), 불러오기(재생성 + 바뀐 상태), 저장·자동 저장, 백업 복원, 이름 변경·복제·삭제·폴더, 형식 변환 | FR-012~014, FR-018~020, FR-022, FR-149~152, NFR-019, NFR-020, NFR-022, NFR-024~026, NFR-036, NFR-037, NFR-054, FR-001 |
| DES-056 | PlayerProgressService | 유스케이스 | 애플리케이션 | SteamID로 복원·새로 생성, 퇴장 시 보존 | FR-011, FR-015~017, FR-022 |
| DES-057 | GatheringAndItems | 유스케이스 | 애플리케이션 | 타격·손 채집·줍기·버리기·이동·나누기·손에 들기·섭취 명령 | FR-032, FR-038~042, FR-044~051 |
| DES-058 | CraftingAndRepair | 유스케이스 | 애플리케이션 | 제작(독립 레시피북에서 요청, 제작대 진입은 C-11 결정 시 추가), 도구 등급·공격 데이터 조회, 수리 | FR-052~055, FR-135~137, FR-173, FR-174 |
| DES-059 | BuildingAndStorage | 유스케이스 | 애플리케이션 | 배치, 철거, 건축물 수리, 보관함 열기·넣기·꺼내기(구독), 수면, 부활 지점, 밤 건너뛰기 | FR-057, FR-058, FR-133, FR-134, FR-138~140, FR-161, FR-162, FR-173 |
| DES-060 | SurvivalSystems | 유스케이스(시스템) | 애플리케이션 | 상태 값 틱, 쓰러짐·소생·사망·부활, 섭취 효과 | FR-024~032, FR-142, FR-143 |
| DES-061 | DefenseService | 유스케이스 | 애플리케이션 | 등광탑 건설·활성화 확인·준비·준비 완료·취소·웨이브 진행·성공·실패·재도전, 정화 결과 | FR-059~075, FR-153~155 |
| DES-062 | AiScheduler | 모듈 | 애플리케이션 | 판단 분산(10 Hz), AI LOD, 공격 판정, IAiNavigationBackend 호출 | FR-079~082, NFR-011, NFR-017 |
| DES-063 | SpawnDirector | 모듈 | 애플리케이션 | 웨이브·밤 습격·동물 생성, 상한·대기열, 정화 구역 금지 | FR-062, FR-066, FR-075, FR-077, FR-082, NFR-017 |
| DES-064 | CombatService | 유스케이스 | 애플리케이션 | 플레이어 공격 요청 검증(사거리 + 지연 허용), 적 공격 적용, 처치·사냥 드롭 | FR-010, FR-037, FR-055, FR-076, FR-078 |
| DES-065 | FarmingAndCooking | 유스케이스 | 애플리케이션 | 심기·물 주기·수확, 작물 틱, 요리 목록·요리 실행 | FR-083~097 |
| DES-066 | ThreatAndBuffSystems | 유스케이스(시스템) | 애플리케이션 | 위치별 위협 적용, 장비·버프 대응, 버프 만료 | FR-043, FR-096, FR-097, FR-104~109, NFR-001 |
| DES-067 | ProficiencyService | 유스케이스 | 애플리케이션 | 경험치 이벤트 → 경험치·레벨업·포인트, 노드 획득 명령 | FR-098~101 |
| DES-068 | ActionEventRecorder | 모듈 | 애플리케이션 | M3~M7 경험치 이벤트 집계·기록·시험 조회 | NFR-046 |
| DES-069 | CommunicationService | 유스케이스 | 애플리케이션 | 채팅(검증·중계, 저장·로그 안 함), 시스템 알림 기록, 핑 | FR-126~128, NFR-042, NFR-050 |
| DES-070 | PlatformFeatures | 유스케이스 | 애플리케이션 | 업적 판정(호스트) → 대상에게 해제 요청, 리치 프레즌스 상태 | FR-164, FR-165 |
| DES-071 | GuidanceService | 유스케이스 | 애플리케이션 | 목표 안내, 생존 튜토리얼, 스토리 도입·단서·결말·결말 후, 지도 탐험 기록 | FR-110, FR-111, FR-113, FR-114, FR-122~124 |
| DES-072 | ClientSession·ClientWorldMirror | 모듈 | 애플리케이션 | 참가 흐름(로컬 생성·요약값 비교·스냅샷 적용·델타 보관), 화면이 읽는 월드 사본. v0.10: 다른 참가자 아바타 자세의 보간 버퍼(Net 어댑터가 넣고 표현 어댑터가 읽음, 3.9.8) | FR-003, FR-008, FR-009, FR-169, NFR-018, NFR-039 |
| DES-073 | SettingsService | 유스케이스 | 애플리케이션 | 옵션 읽기·적용·저장(로컬), HUD 표시 방식(v0.4) | FR-117~121, FR-129~132, FR-157, FR-171, NFR-047, FR-172 |
| DES-074 | HudViewModel | 모듈 | 애플리케이션 | 두 HUD 방식이 공유하는 표시용 값: 상태 값(비율·수치), 위협 상태, 버프 남은 시간, 목표 한 줄, 동료(이름·체력 단계·쓰러짐), 스태미나 호 표시 여부. 미러에서 계산, 표시 방식과 무관(3.7.11). v0.4 신설 | NFR-001, FR-122, FR-159, FR-160, FR-171 |
| DES-075 | FurnaceService | 유스케이스 + 시스템 | 애플리케이션 | 용광로 열기·닫기(구독), 칸 넣기·꺼내기(보관함과 같은 동시 조작 규칙), 완료 시각 최소 힙으로 가공 진행(서버 틱, 관심 범위 무관), `FurnaceView` 상태 변화 때만 발행, 저장·불러오기 때 남은 시간 변환, 가공 완료 경험치 이벤트(잠정). v0.6 신설 | FR-175~178, NFR-046 |
| DES-080 | NetMessages (Protocol) | 모듈 | 애플리케이션 (v0.10: 어댑터(Net) → 애플리케이션, ID 유지) | `Game.Application/Protocol/`: `MessageKind`(u8), 3.7.2 표의 메시지 구조체(JoinRequest, JoinAccepted, JoinRejected, WorldReady, JoinComplete, Command, CommandResult, SnapshotChunk, TableDelta 등)와 `AvatarPose`, `ByteWriter`/`ByteReader`, 양자화, 메시지별 크기 상한 검사, `MessageCodec`(Encode / 예외 없는 TryDecode). 순수 C#(`noEngineReferences`), FishNet 형식 없음. 메시지마다 왕복 시험 + 고정 바이트 시험(`Game.Tests.Application`) | NFR-013, NFR-014, NFR-040, NFR-031, NFR-058 |

### 4.4 어댑터 (DES-081~099, DES-120~122)

DES-080은 v0.10에서 애플리케이션으로 옮겼습니다(4.3). v0.10부터 DES-081·082·083·085는 FishNet 형식 없이 포트(`INetServer`/`INetClient`)와 Protocol 메시지에만 의존하는 일반 C# 클래스이며(MonoBehaviour·NetworkBehaviour 아님), 조립 루트가 만들어 연결합니다.

| DES ID | 이름 | 종류 | 레이어 | 설명 | 관련 FR/NFR |
|---|---|---|---|---|---|
| DES-081 | ServerCommandGateway | 모듈 | 어댑터(Net) | 연결 → PlayerId(전송 원격 ID, `Connected(connId, remoteId)`로 받음), 디코드된 메시지 받기, 예외 격리, 빈도·값 범위 검사, 디스패처 호출, 결과 회신. v0.10: 포트·Protocol 메시지에만 의존, 가짜 `INetServer`로 시험(`Game.Tests.Net`) | FR-007, NFR-040, NFR-041 |
| DES-082 | ReplicationPublisher | 모듈 | 어댑터(Net) | 테이블 델타·스냅샷 청크 송신, 생물 관심 범위 등급 송신, 연결별 큐·속도 제한. v0.10: 포트·Protocol 메시지에만 의존 | FR-008, FR-009, NFR-013~016 |
| DES-083 | ClientReplicationReceiver | 모듈 | 어댑터(Net) | 수신 → 미러 적용, 생물 보간 버퍼. v0.10: 포트·Protocol 메시지에만 의존 | FR-008, NFR-015 |
| DES-084 | PlayerAvatar (NetworkObject) | 컴포넌트 | 어댑터(Net) | v0.11: 위치 `Adapters/Net/Avatar/PlayerAvatar.cs`, 프리팹 `Prefabs/PlayerAvatar.prefab`(NetworkObject + PlayerAvatar, 모양 없음, `DefaultPrefabObjects` 등록). FishNet 형식을 써도 되는 두 폴더 중 하나(3.4.1 규칙 8). 클라이언트 계산 이동 + 호스트 검사. v0.10: NetworkTransform 대신 소유자 ServerRpc(비신뢰, `AvatarPose` 바이트) → 호스트에서 `AvatarMoveValidator`(DES-052) 호출 → 통과하면 ObserversRpc(비신뢰, 소유자 제외)로 중계, 거부하면 TargetRpc 보정. 내 자세는 `ILocalPlayerPose`에서 읽고, 받은 다른 참가자 자세는 `ClientWorldMirror`에 넣음(표현 어댑터 참조 없음). 손에 든 것·생명 상태·체력 비율은 호스트가 정해 공유(3.9.8) | FR-023, FR-024, FR-042, FR-159, FR-160, NFR-030 |
| DES-085 | JoinHandshake | 모듈 | 어댑터(Net) | JoinRequest/Accepted/Rejected, WorldReady, 로비 멤버 확인, 비밀번호 확인. v0.10: 포트·Protocol 메시지에만 의존, 가짜 `INetServer`로 시험 | FR-003, FR-004, FR-009, FR-115, FR-146, FR-169 |
| DES-086 | FirstPersonController | 컴포넌트 | 어댑터(표현) | 1인칭 이동·달리기(CharacterController), 카메라, 감도·반전·시야각. 스태미나는 `VitalsRules`(DES-003)로 계산(0이면 걷기 속도). v0.10: 매 프레임 자세를 `ILocalPlayerPose`에 쓰고, 호스트 보정을 받으면 위치를 되돌림 | FR-023, FR-024, FR-025, FR-118 |
| DES-087 | ResourceViewStreamer | 컴포넌트 | 어댑터(표현) | 셀 단위 근거리 GameObject 풀 + 원거리 인스턴싱, 쓰러짐·파괴 애니메이션 | FR-047, FR-049, NFR-011, NFR-012, NFR-016 |
| DES-088 | CreatureViews | 컴포넌트 | 어댑터(표현) | 생물 보간 표시, 애니메이터(화면 밖 정지), 피격 반응, 지역 변형 외형 | FR-076~081 |
| DES-089 | TerrainViewBuilder | 컴포넌트 | 어댑터(표현) | 정수 높이 → Unity Terrain 36타일, 물 표면, 스플랫·디테일 | FR-168, NFR-052 |
| DES-090 | LightingAtmosphere | 컴포넌트 | 어댑터(표현) | 낮·밤, 장막 아래 잿빛 조명, 정화 구역, 완전한 어둠, 광원(횃불·랜턴)의 **상태 계산**. 적용은 `IRenderBackend`(DES-114) 호출 | FR-033~036, FR-106 |
| DES-091 | UI (v0.13: uGUI + TextMeshPro, v0.12까지 UI Toolkit) | 컴포넌트 모음 + 프리팹 | 어댑터(표현) | 화면·HUD 프리젠터(배치는 STORYBOARD), 사유 메시지, 리치 텍스트 끔. 공통 기반(글자 크기 단계·색 토큰·모양 구분 규칙, M1부터 — v0.13: DES-122 테마 위에), 다크 단일 테마, 화면을 열어도 게임 진행 유지(로컬 입력 경로만 전환), 독립 레시피북(`OpenRecipeBook(source)`, v0.6: `Hotkey` = 열람 + 손 제작, `Station` = 제작대 팝업) (3.7.10, 3.10.7), 용광로 화면(v0.6, 진행 막대는 시작 시각 + 가공 시간으로 로컬 계산). v0.4: 일반 HUD(`ScreenHud`), HUD 공통 부품, `HudModeSwitcher`(재시작 없이 전환) (3.7.11). **v0.13**: 화면마다 프리팹(`UI/Prefabs/Screens/`, `Hud/`)이 원본이고 루트에 `UiScreen` + `…Screen` 컴포넌트, 표시 로직은 일반 C# `…View` 클래스(패시브 뷰). M1 화면: `SteamRequired`(SCR-001), `DevLobby`(SCR-004 최소판·SCR-012), `GameHud`(SCR-015 M1 범위), `GameMenu`(SCR-016, Hud에서 분리). 화면 층·숨김 규칙 3.7.10 (5) | NFR-001~003, NFR-056, NFR-057, FR-052, FR-094, FR-128, FR-171, FR-160, FR-174, FR-175, NFR-050 |
| DES-092 | InputLayer | 컴포넌트 | 어댑터(표현) | Input Actions, 장치별 재설정·저장, 장치 전환 감지, 안내 아이콘. v0.13: UI 입력은 EventSystem + `InputSystemUIInputModule`(게임과 같은 Input Actions 인스턴스), 닫기·탭 이동은 `UiInputRouter`(DES-122), 키캡 `KeycapView`(장치 전환 시 1초 안 교체), UI 지도에 `Point`·`Click`·`ScrollWheel` 추가(3.7.10 (6)) | NFR-002, NFR-004, FR-117, FR-166, FR-172 |
| DES-093 | LocalizationLayer | 컴포넌트 | 어댑터(표현) | String Table 조회, 텍스트 키·인자 조립, 언어 전환(재시작 없음), 누락 키 검사. 첫 실행 언어 = Steam 언어(한국어면 한국어, 그 밖 영어, C-48) | NFR-034, NFR-044, FR-157 |
| DES-094 | AudioLayer | 컴포넌트 | 어댑터(표현) | Audio Mixer 5그룹 볼륨, 중요 소리 자막 이벤트 | FR-119, FR-129 |
| DES-095 | Accessibility | 컴포넌트 | 어댑터(표현) | 글자 크기(v0.13: 단계를 정해 `UiRoot`에 넘기면 DES-122가 모든 글자에 적용. v0.12까지 USS 배율 변수), 색각 보정 모드, 흔들림 배율, 섬광 제한 판단, 광과민성 경고. 후처리 적용은 DES-114 | FR-129~132, NFR-056 |
| DES-096 | InteractionAndPlacement | 컴포넌트 | 어댑터(표현) | 조준 대상 판정·안내, 건축 미리보기(배치 가능 표시) | NFR-002, FR-057, FR-058 |
| DES-097 | HudWorldMarkers | 컴포넌트 | 어댑터(표현) | 나침반, 지도, 이름표(화면 좌표 투영. 장치형 HUD에서는 등불 소품 위치에 붙임), 핑·사망 위치·부활 지점 표시. v0.13: 월드 표시 층 캔버스(정렬 0, `UI/Prefabs/Hud/WorldMarkers.prefab`)에 표식 오브젝트 풀을 두고 화면 좌표로 옮김(월드 공간 캔버스는 쓰지 않음 — 후처리·업스케일링·깊이 영향, R-18 해소) | FR-124~126, FR-141, FR-159 |
| DES-098 | WorldFileCodec | 모듈 | 어댑터(저장) | 헤더·청크 인코딩, 본문 해시, 청크별 형식 변환, 저장 금지 항목 검사. v0.6: `FURN` 청크 | NFR-022~025, NFR-037, NFR-042, FR-177, FR-013, FR-014, FR-015, FR-022 |
| DES-099 | WorldSaveStore | 모듈 | 어댑터(저장) | 월드 폴더, 원자적 저장, 백업 회전·복원, 이름 변경·복제·삭제 | FR-018~020, FR-022, FR-150~152, NFR-024, NFR-026, NFR-036, FR-013, NFR-020 |
| DES-120 | DiegeticHud | 컴포넌트 | 어댑터(표현) | 장치형 HUD(기본): 왼쪽 손목 옛 등광 계기와 올림/내림, 계기 화면(v0.13: uGUI 프리팹 `WristPanel` → UI 캡처 카메라 → 렌더 텍스처. v0.12까지 UI Toolkit 패널), 표시 면 `IWristSurface`(기본 구현 (B) 화면 공간 기울인 평면 = v0.13: `UiWarpedRawImage` 사다리꼴 메시, v0.12까지 UI Toolkit 사용자 지정 메시. 캡처 문제 시 대체안 3.7.11·R-39. (A) 3D 손목 소품은 교체 지점만, M6-11에서 결정), 올림 방식 `WristRaiseMode`(기본 토글, 옵션 누르는 동안), 올린 동안 모든 행동 가능, 다른 창이 열리면 자동 내림, 십자선 둘레 스태미나 호, 머리 위 등불 이름표 소품(체력 4단계 + 꺼짐), 피격 호·위협 테두리(UI 레이어, 계기 위), `손목 자동 내림 시간` 설정값(기본값 결정 대기). 월드 보조 색조만 `IRenderBackend`(DES-114). 어댑터 대역(080~099)이 차서 120번 사용. v0.4 신설 | NFR-001, FR-159, FR-171, FR-172, FR-104~106 |
| DES-121 | FishNetPacketTransport | 모듈 + 컴포넌트 | 어댑터(Net) | v0.10 신설. `FishNetServer`(`INetServer` 구현)·`FishNetClient`(`INetClient` 구현)와 바이트 래퍼 Broadcast 1종 `PacketBroadcast`. FishNet `ServerManager`/`ClientManager`에 `PacketBroadcast`만 등록, 연결 시작·종료 이벤트와 원격 ID(FishySteamworks SteamID / Tugboat 개발 ID)를 포트 이벤트로 넘김, 보낼 때 `MessageCodec.Encode` → 채널(신뢰/비신뢰)로 전송, 받을 때 `TryDecode` 실패는 버리고 로그, 송수신 버퍼 재사용. 게이트웨이·복제·참가 절차에서 FishNet 형식을 쓰는 곳은 여기뿐(DES-084 아바타 RPC 제외). v0.11: 위치 `Adapters/Net/Transport/FishNetPacketTransport.cs`(폴더 규칙 3.4.1 규칙 8). 호스트 본인 연결은 로컬 플레이어 ID를 원격 ID로 씀. 에디터 측정에서 송신 1건당 관리 힙 약 81 B 할당이 남음(R-31) | NFR-031, NFR-040, NFR-013, NFR-014, FR-005 |
| DES-122 | UiThemeKit | 데이터 + 컴포넌트 모음 | 어댑터(표현) | v0.13 신설(D-27). uGUI 공통 기반: 테마 애셋 `UiTheme`(ScriptableObject, `UI/Theme/UiTheme.asset` 한 벌 — 색 토큰 20개, 글자 크기 토큰 6개, 단계 배율 0.9/1.0/1.25, 글꼴 토큰 3개, 대비 검사 짝), 토큰 상수 `UiTokens`, 적용 컴포넌트 `ThemedText`(크기·색·굵기·텍스트 키, `richText`·자동 크기·레이캐스트 끔 강제)·`ThemedGraphic`·`ThemedSelectable`(STORYBOARD 2.5 상태, 포커스 3px 외곽선 + ▶), `UiButton`(사용 불가여도 포커스를 받고 사유 이벤트), `UiScreen`(층별 정렬 순서, 숨김 = Canvas·GraphicRaycaster·CanvasGroup 함께 끔), `UiRoot`(글자 단계·언어 다시 적용, `UiScreenStack`, `UiInputRouter` — Esc·Ⓑ 닫기, Q/E·LB/RB 탭 이동), `UiSafeArea16x9`, `UiRaycastBlocker`, `KeycapView`, `UiArcGraphic`, `UiWarpedRawImage`. 에디터 보조(토큰 드롭다운, Apply Theme To Prefabs)는 `Game.Editor`. 규칙 검사 `UiPrefabRulesTests`(3.4.3) | FR-129, FR-130, FR-166, NFR-002, NFR-004, NFR-050, NFR-056, NFR-057 |

### 4.5 인프라 (DES-100~113)

| DES ID | 이름 | 종류 | 레이어 | 설명 | 관련 FR/NFR |
|---|---|---|---|---|---|
| DES-100 | CompositionRoot·SceneFlow | 모듈 | 인프라 | 구현 조립, 씬 전환, 호스트/참가자 구성. 부트에서 Steam 확인 → 없으면 안내 화면 SCR-001(오프라인 플레이 없음, 개발 빌드 Tugboat 예외). v0.11(실제): `GameBootstrap`(Boot 씬 진입점) → 전송 선택 뒤 **꺼 둔 `NetworkManager`를 켬**(3.9.9) → `GameFlow`(로비·접속·HUD 화면 흐름, World 씬 Additive 로드)·`SessionRuntime`(호스트/참가자 세션 조립, `ILocalPlayerPose` 버퍼를 두 어댑터에 넘김) | NFR-006, NFR-031, NFR-032, NFR-058 |
| DES-101 | TransportSelector·NetworkConfig | 모듈 | 인프라 | FishySteamworks/Tugboat 선택, 틱·시간 초과(연결 끊김 판정 시간)·MTU 설정. v0.11(실제): `Apply`는 선택한 전송 구성 요소를 붙이고(FishySteamworks는 P2P 필드를 리플렉션으로 켬), 꺼진 `NetworkManager`에 `TransportManager`가 없으면 **직접 붙인 뒤** 전송을 지정 — FishNet이 깨어날 때(Awake) 그 전송을 초기화하므로 반드시 `NetworkManager`를 켜기 전에 호출(3.9.9). 켠 뒤 `ApplyTuning`(서버 틱, 연결 끊김 판정 시간), `ApplyConnectionLimit`(최대 인원 + 여유 2, 초과분은 세션 규칙이 인원 초과로 거부). MTU는 Tugboat 내부 상수·Steam이 정하므로 바꾸지 않음 | FR-005, FR-006, NFR-008, NFR-031, NFR-038, NFR-049 |
| DES-102 | SteamPlatformService | 모듈 | 인프라 | 초기화·App ID, 로비(참가 조건 → 로비 유형, 메타데이터 키 3.7.1 — 예약 자리 수 `kh_resv` 포함), 초대, 이름, 업적, 리치 프레즌스, 화면 키보드, Steam 언어 조회(C-48) | FR-001~003, FR-145, FR-164, FR-165, NFR-004, NFR-006, NFR-009, FR-115, FR-144, FR-157, NFR-035 |
| DES-103 | DevTools | 도구 | 인프라 | 개발용 ID, 실행 옵션, 봇 클라이언트, 출시 빌드에서 제외. v0.11(실제): 순수 해석 `LaunchOptionsParser`(Application, `-transport`·`-host`·`-connect`·`-devPlayerId`)와 Unity 쪽 보조 `DevCommandLine`(Bootstrap/DevTools, `-bootsmoke`·`-steamlobbytest`·`-autowalk`·`-autoquit N`), `SteamLobbySmoke`. `-host`/`-connect`는 전송을 안 주면 Tugboat, `-transport steam`과 함께면 옵션 오류를 기록하되 진행(3.7.4). 봇 클라이언트 `-bot`은 M1-12에서 만듦 | FR-006, NFR-008 |
| DES-104 | ConfigData·ContentRegistry | 데이터 + 모듈 | 인프라 | ScriptableObject 정의, 로더·검증, ContentId 부여, ContentHash | NFR-033, NFR-034, FR-103, FR-176 |
| DES-105 | AiJobsBackend | 모듈 | 인프라 | 보행 격자, 흐름장, 조향, 공간 해시(Burst) | FR-067, NFR-011, NFR-017 |
| DES-106 | SteamCloudSync | 모듈 | 인프라 | 월드별 원격 저장소 업로드·목록·내려받기·삭제 | FR-021, FR-151, NFR-007 |
| DES-107 | Logging·CrashReporter | 모듈 | 인프라 | 파일 로그(회전, 민감 정보 제외), 세션 잠금 파일, 보고 압축 파일. C-43 해소: 로컬 압축 파일 생성과 폴더 열기까지만, 외부 전송 경로 없음(네트워크 호출 코드를 두지 않음) | FR-158, NFR-042, NFR-045 |
| DES-108 | PerfTelemetry | 모듈 | 인프라 | 표식·기록기, 서버 처리 예산 감시, 개발용 성능 표시, CSV. v0.9: Stopwatch 자체 측정이 기본·ProfilerMarker 보조, GPU 프레임 시간은 `enableFrameTimingStats`, 성능 표시는 IMGUI(F3, 개발 빌드 전용)(3.12.4) | FR-163, NFR-010~021, NFR-048, NFR-005 |
| DES-109 | GraphicsService | 모듈 | 인프라(`Game.Infra.Rendering`) | HDRP 품질 단계·프리셋·세부 설정, 업스케일러(DLSS/FSR 품질 모드) 적용, 기준 프리셋 「높음」. v0.9: 프리셋은 순수 `GraphicsPresetCatalog`로 받음(애셋 `GraphicsPresetConfig`는 `Game.Infra.Config`), LOD 편향은 `GraphicsAssetBuilder`가 HDRP 애셋에 구워 넣음(3.12.7) | FR-120, FR-121, NFR-005, NFR-010~012, NFR-028 |
| DES-110 | LocalSettingsStore | 모듈 | 인프라 | settings.json 읽기·쓰기(형식 버전). v0.7: 호스트 차단 목록 `bans.json`(`IBanListStore` 구현, 모든 월드 공통, C-46) | NFR-047, FR-148, FR-171 |
| DES-111 | BackgroundRunner | 모듈 | 인프라 | 생성·저장 직렬화·압축을 메인 스레드 밖에서, 결과는 메인 스레드로 | NFR-018~020, NFR-052, FR-018 |
| DES-112 | BuildConfig·ArchitectureRules | 데이터 + 도구 | 인프라 | 개발/출시 정의 기호, App ID 설정 파일, 게임 버전(v0.12: SemVer, 3.7.12), 패키지 버전 고정, asmdef 규칙 시험(3.4.2 표 비교, Pipeline 패키지 격리 3.4.3), Unity Smart Merge 설정(v0.8: `git mergetool` 방식, M1-01 (4)). v0.8: 에디터 전용 어셈블리 `Game.Editor`의 빌드 명령(`BuildCommands`)·출시 빌드 산출물 검사(`BuildArtifactInspector`, Pipeline 런타임 어셈블리 0건 확인)·프로젝트 설정 적용을 이 요소에 포함. v0.11(실제, **빌드 번호 규칙은 사용자 결정 대기**, R-34): `BuildCommands`가 빌드할 때마다 `ReadBuildNumber()`(명령줄 `-buildNumber`, 없으면 환경 변수 `GAME_BUILD_NUMBER`, 둘 다 없으면 0)로 `PlayerSettings.bundleVersion`을 `VersionPrefix("0.1").빌드 번호`로 **덮어씀**(예: 손으로 0.1.2로 바꿔 두어도 번호 없이 빌드하면 0.1.0). 런타임 `BuildInfo.Number`는 이 문자열을 `주×10000 + 부×100 + 끝자리`로 바꿔 `JoinRequest.buildVer`·로비 `kh_build`·세션 규칙의 버전 비교에 씀. **v0.12(사용자 결정 2026-11-04, R-34 해소, D-26)**: 위 덮어쓰기와 숫자 인코딩을 폐지. 원본은 사람이 정하는 `bundleVersion`(SemVer 핵심 `MAJOR.MINOR.PATCH[-pre]`)이고, `BuildCommands`는 빌드하는 동안만 메타데이터 `+<rel|dev>.<UTC yyyyMMddHHmmss>.<커밋 7자>[.dirty]`를 붙였다가 `finally`에서 되돌림, 출시 빌드는 태그 `v<핵심>`이 다른 커밋이면 실패, `-buildNumber`·`GAME_BUILD_NUMBER` 폐지. `ProjectConfigurator`는 비어 있을 때만 `0.1.0`. 런타임 `BuildInfo.Version`(`GameVersion`, DES-037)이 `JoinRequest.gameVersion`·`kh_build`·화면 표시에 쓰임(3.7.12). **v0.13**: 씬·프리팹 생성기는 「없을 때만 처음 만들기 + 있으면 검사만」(3.8.2), 아키텍처 시험에 UI 패키지 격리·UI Toolkit 사용 금지 검사 추가(3.4.3) | NFR-009, NFR-027~029, NFR-035, NFR-043, NFR-049, NFR-058 |
| DES-113 | TestAndBenchHarness | 도구 | 인프라 | 골든 해시, `-genhash`(다른 PC 비교)·`-genhash-compare`(결과 파일 비교, v0.7), `-genvalidate`(NFR-055), Bench 씬(P1/P8), 봇 | NFR-021, NFR-051, NFR-053, NFR-055, NFR-043 |
| DES-114 | RenderBackend (HDRP) | 모듈(어댑터 인터페이스 구현) | 인프라(`Game.Infra.Rendering`) | `IRenderBackend` 구현. Volume 혼합, 태양·광원·정화 마스크 쿠키 적용, 후처리(색각 보정·섬광 완화), 셰이더 전역 변수. v0.4: 위협 시 월드 보조 색조(Volume. 테두리·피격 호 본체는 UI 레이어, v0.5). 1인칭 손·손목 소품 그리기 패스는 메서드 자리만(손목 표시 (A) 선택 시 구현, v0.5). HDRP 의존 코드가 모이는 유일한 곳(3.4.4). v0.2 신설 | FR-033~036, FR-106, FR-130, FR-131, FR-172, NFR-028 |

설계 요소 합계: **109개** (도메인 37, 월드 생성 8, 애플리케이션 27, 어댑터 22, 인프라 15). v0.4에서 DES-074, DES-120, v0.6에서 DES-036, DES-075 추가. v0.10: DES-080을 어댑터 → 애플리케이션으로 옮기고(ID 유지) DES-121 추가. v0.11: 새 요소 없음(에디터 생성 스크립트는 DES-112, 부트 순서는 DES-100·101 설명에 포함). v0.12: DES-037 GameVersion 추가. v0.13: DES-122 UiThemeKit 추가.

---

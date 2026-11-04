> SDD(소프트웨어 설계 명세서)의 일부 · 목차: [00-index.md](00-index.md)

### 3.9 상호작용 관점

#### 3.9.1 새 월드 만들기와 세션 열기 (참가 조건 포함, SRS v0.3)

```mermaid
sequenceDiagram
    actor H as 호스트 플레이어
    participant UI as 메인 메뉴 UI
    participant WL as WorldLifecycle (DES-055)
    participant GEN as Generator (DES-043~046)
    participant ST as WorldSaveStore (DES-099)
    participant SS as SessionService (DES-054)
    participant LB as LobbyService (DES-102)
    participant NET as Transport/FishNet (DES-101)
    H->>UI: 월드 이름, 시드(입력/무작위), 난이도, 참가 조건(기본 친구 전용)
    UI->>WL: CreateWorld(name, seedText, difficulty, access)
    WL->>GEN: Generate(SeedCodec(seedText), 최신 생성기 버전)
    GEN-->>WL: GeneratedWorld + 요약값 (검증 통과, 재시도 횟수)
    WL->>ST: Save(새 월드 파일: WSET에 시드·생성기 버전·참가 조건 기록)
    WL-->>UI: 생성 완료
    UI->>H: 세션 열기 화면 (참가 조건 기본 선택 = 월드 설정 값, 비밀번호 선택 입력)
    H->>UI: 세션 열기 (참가 조건 확인/변경)
    UI->>SS: OpenSession(worldId, access, password?)
    SS->>WL: LoadWorld(worldId) (이미 메모리에 있으면 생략)
    SS->>NET: StartHost(Steam 또는 개발용 Tugboat)
    SS->>LB: Create(access → Private/FriendsOnly/Public, 메타데이터)
    LB-->>SS: lobbyId
    SS->>WL: 참가 조건을 바꿨으면 WorldSettings.Access 갱신(다음 저장에 반영)
    SS-->>UI: 호스트 입장 (호스트 플레이어도 JoinRequest 경로로 스폰)
```

- 기존 월드를 열 때도 같은 화면에서 참가 조건 기본값은 그 월드에 저장된 값입니다. 세션 중 변경(FR-144)은 `ChangeAccess` 호스트 명령 → 로비 유형·리치 프레즌스 갱신 → 월드 설정 갱신 순입니다.
- 게임 안 행동 권한(SUG-018)은 보류이므로 참가 조건은 「누가 들어올 수 있는가」만 정합니다.

#### 3.9.2 중도 참가 (시드 → 로컬 생성 → 요약값 비교 → 바뀐 상태 적용)

```mermaid
sequenceDiagram
    actor P as 참가자
    participant CS as ClientSession (DES-072)
    participant LB as Steam 로비
    participant GW as 호스트 Gateway/Join (DES-081, 085)
    participant SR as SessionRules (DES-031)
    participant SW as ServerWorld (DES-052)
    participant RP as ReplicationPublisher (DES-082)
    participant GEN as 로컬 Generator
    P->>CS: 초대 수락 / 친구 참가 / 공개 목록 선택
    CS->>LB: JoinLobby(lobbyId)
    LB-->>CS: 로비 데이터(호스트 SteamID, 게임 버전, 참가 조건)
    CS->>GW: 전송 연결(FishySteamworks → 호스트 SteamID)
    GW->>GW: 원격 SteamID 확인, 로비 멤버(또는 재접속 예약)인지 확인
    CS->>GW: JoinRequest(프로토콜·게임 버전 문자열·콘텐츠 요약값·지원 생성기 버전·참가 경로·비밀번호)
    GW->>SR: 차단? 인원(접속+예약) < 8? 참가 조건? 비밀번호(시도 제한)? 버전? 생성기 버전 지원?
    alt 거부
        GW-->>CS: JoinRejected(사유)
        CS-->>P: 사유 표시 후 메인 메뉴
    else 허용
        GW-->>CS: JoinAccepted(시드, 생성기 버전, 기대 요약값, 월드 설정, 틱, 시각)
        CS->>GEN: 백그라운드 생성 (진행 표시 갱신)
        GEN-->>CS: GeneratedWorld + 요약값
        alt 요약값 다름
            CS-->>P: 「월드 생성 불일치」 표시·기록, 접속 종료
            CS->>GW: WorldReady(요약값) → 호스트도 기록
        else 같음
            CS->>GW: WorldReady(요약값)
            GW->>SW: 스냅샷 틱 T 확정, 테이블 스냅샷 복사
            SW->>RP: 스냅샷 + T 이후 델타를 이 연결 큐에 등록
            RP-->>CS: SnapshotChunk × n (속도 제한 약 1.5 Mbps)
            CS->>CS: 미러에 적용(자원 → 건축물 → 드롭 → 작물 → 등광탑 → 구역 → 생물)
            SW->>SW: 플레이어 진행 복원(SteamID) 또는 새로 생성
            RP-->>CS: JoinComplete(스폰 정보) + 보관해 둔 델타(T 이후)
            CS-->>P: 조작 가능 (NFR-018 60초 이내)
        end
    end
```

- v0.10: 위 메시지(`JoinRequest`~`JoinComplete`, `SnapshotChunk`, `TableDelta`)는 모두 `Protocol/` 메시지입니다. GW(DES-081, 085)와 RP(DES-082)는 `INetServer`로만 주고받고, 실제 바이트 전송은 `FishNetServer`/`FishNetClient`(DES-121)가 `PacketBroadcast`로 합니다. 그래서 참가 판정·스냅샷·델타 보관 흐름은 가짜 `INetServer`로 FishNet 없이 시험합니다(`Game.Tests.Net`).

#### 3.9.3 자원 타격 → 쓰러짐 → 드롭 → 경험치 이벤트

```mermaid
sequenceDiagram
    actor P as 참가자
    participant PV as Presentation(DES-086, 087, 096)
    participant GW as Gateway (DES-081)
    participant CD as CommandDispatcher (DES-050)
    participant HR as HarvestRules (DES-008)
    participant TB as ResourceTable / DropTable
    participant EV as 도메인 이벤트
    participant RP as ReplicationPublisher
    P->>PV: 도끼 공격 입력
    PV->>PV: 공격 애니메이션·소리 즉시 재생(낙관적 표현), 조준 대상 ResourceId
    PV->>GW: Command HitResource(resourceIndex, handSlot)
    GW->>GW: 크기·빈도(공격 속도 이하)·값 범위 검사
    GW->>CD: Dispatch(PlayerId, cmd)
    CD->>HR: 거리(서버의 플레이어 위치 ↔ 생성 표 위치) ≤ 상호작용 거리? 도구 종류·등급? 망가짐? 대상 단계?
    alt 거부
        CD-->>GW: Rejection(사유)
        GW-->>P: CommandResult(실패, 사유)
    else 통과
        HR->>TB: 타격 수 +1, 내구도 감소
        opt 타격 수 = 나무 타격 횟수
            HR->>TB: 단계 = 쓰러짐, 넘어지는 방향(마지막 타격자 방향 양자화)
            HR->>TB: 드롭 표 1회 굴림 → DropItem 엔터티 생성
            HR->>EV: ActionCompleted(벌목, PlayerId, 나무 종류)
        end
        RP-->>P: 다음 틱 TableDelta(자원 상태, 드롭 생성) — 모든 참가자에게
        PV->>PV: 쓰러짐 애니메이션(전원 같은 방향)
    end
```

- 여러 명이 같은 나무를 때려도 호스트가 명령을 도착 순서대로 하나씩 처리하므로 쓰러짐·드롭은 한 번만 일어납니다(FR-051).

#### 3.9.4 자동 저장 (원자적 교체)

```mermaid
sequenceDiagram
    participant SW as ServerWorld
    participant WL as WorldLifecycle (DES-055)
    participant BG as BackgroundRunner (DES-111)
    participant CO as WorldFileCodec (DES-098)
    participant ST as WorldSaveStore (DES-099)
    participant CL as CloudSync (DES-106)
    SW->>WL: 자동 저장 시각 도달 / 정상 종료
    WL->>SW: CaptureForSave() — 메인 스레드에서 값 복사(목표 5 ms 이하)
    WL->>BG: Run(Encode + Write)
    BG->>CO: Encode(snapshot) → bytes (본문 해시 포함)
    BG->>ST: tmp 파일 쓰기 → 디스크 플러시 → 다시 읽어 해시 확인
    ST->>ST: File.Replace(tmp → world.sav, 이전 파일 → backups/…)
    ST->>ST: 백업 N개 초과분 삭제(오래된 순)
    opt Cloud 켬
        BG->>CL: Upload(world.sav)
    end
    BG-->>WL: 결과(성공/실패) → 로그, 실패 시 호스트에게 알림
```

#### 3.9.5 등광탑 활성화 → 준비 단계 → 방어전 → 정화

```mermaid
sequenceDiagram
    actor A as 활성화한 플레이어
    participant UI as 확인 창
    participant DF as DefenseService (DES-061)
    participant WC as WaveComposer (DES-018)
    participant SD as SpawnDirector (DES-063)
    participant AI as AiScheduler (DES-062)
    participant Z as PurifiedZoneSet (DES-015)
    A->>UI: 비활성 등광탑 상호작용
    UI-->>A: 경고, 웨이브 수, 실패 시 손실 비율
    A->>DF: TowerActivate (확인)
    DF->>DF: 단계 = 준비, 카운트다운 시작, 전원에게 알림·나침반 표시
    loop 준비 단계
        Note over DF: Ready 명령 수집. 전원 준비 → 즉시 시작. 활성화자/호스트 TowerCancel → 비활성 복귀(재료 변화 없음)
    end
    DF->>WC: 웨이브 구성(등광탑 종류, 지역, 시작 시 접속 인원, 난이도)
    loop 웨이브 1..N
        WC-->>SD: 웨이브 적 목록 (마지막 웨이브에 보스 1체)
        SD->>AI: 동시 적 상한 안에서 생성(웨이브 우선, 넘치면 대기열)
        AI-->>DF: 처치·등광탑 피해 이벤트
        alt 등광탑 체력 0
            DF->>DF: 실패: 탑 제거, 재료 (1-손실 비율) 드롭(버림), 남은 적은 C-14 규칙
        end
    end
    DF->>Z: Add(중심, 정화 반경) — 추가만 가능
    DF-->>A: 「정화 성공」, 해금·목표·업적·단서 이벤트 발행
```

#### 3.9.6 재접속 (FR-115)

```mermaid
sequenceDiagram
    participant C as 참가자 B
    participant H as 호스트 SessionService
    Note over H: B 연결 끊김 → B 진행 저장 대상으로 옮김, 자리 예약(재접속 자리 유지 시간), 로비 kh_resv +1
    Note over H: 재접속 성공 또는 유지 시간 만료 → 예약 해제, 로비 kh_resv -1
    C->>C: 메인 메뉴 「다시 참가」(마지막 호스트 SteamID 기억)
    C->>H: 전송 연결(로비 없이 호스트 SteamID로 직접)
    H->>H: 예약 목록에 B가 있는가? (없으면 일반 참가 규칙 → 로비 경유 필요)
    C->>H: JoinRequest(joinPath = Rejoin) — 참가 조건·비밀번호 확인 생략
    H-->>C: 이후 3.9.2와 같음(생성 → 요약값 → 스냅샷 → 진행 복원)
```

#### 3.9.7 호스트 종료 예고 (FR-116)

```mermaid
sequenceDiagram
    actor H as 호스트
    participant SS as SessionService
    participant ALL as 모든 참가자
    H->>SS: 세션 종료 선택
    SS-->>ALL: ShutdownNotice(남은 초) 매초
    alt 호스트 취소
        SS-->>ALL: ShutdownNotice(취소)
    else 카운트다운 끝
        SS->>SS: 저장(3.9.4) 완료 대기
        SS-->>ALL: 세션 종료 → 메인 메뉴
        SS->>SS: 로비 나가기, 전송 정지
    end
```

#### 3.9.8 플레이어 아바타 이동 (v0.10, DES-084, 사용자 결정 2026-11-03)

```mermaid
sequenceDiagram
    participant FPC as FirstPersonController (DES-086, 참가자 A)
    participant LP as ILocalPlayerPose (DES-051)
    participant AVA as PlayerAvatar A 소유자 쪽 (DES-084)
    participant AVH as PlayerAvatar A 호스트 쪽 (DES-084)
    participant VAL as AvatarMoveValidator (DES-052)
    participant SW as ServerWorld (DES-052)
    participant AVB as PlayerAvatar A 참가자 B 쪽
    participant MIR as ClientWorldMirror B (DES-072)
    participant VIEW as 원격 플레이어 표시 (Presentation)
    FPC->>LP: Write(AvatarPose) — 매 프레임 로컬 이동 결과(클라이언트 계산)
    loop 서버 틱(30 Hz)
        AVA->>LP: TryRead(pose)
        AVA->>AVH: ServerRpc(비신뢰, AvatarPose 바이트 — Protocol 인코딩)
        AVH->>VAL: Check(이전 수락 자세, 새 자세, 서버 쪽 스태미나 추정)
        alt 통과
            VAL-->>AVH: 수락
            AVH->>SW: 서버 플레이어 위치·달리기 상태 갱신(거리 검증 FR-007의 기준)
            AVH->>AVB: ObserversRpc(비신뢰, 수락한 자세 바이트) — 소유자 제외
            AVB->>MIR: 플레이어 A 자세를 보간 버퍼에 넣음
            VIEW->>MIR: 보간한 자세 읽어 표시
        else 거부(속도·범위 초과)
            VAL-->>AVH: 거부 + 마지막 수락 위치
            AVH->>AVA: TargetRpc(신뢰, 보정 위치), 로그 move_rejected
            AVA->>LP: ApplyCorrection(pos) → FPC가 위치를 되돌림
        end
    end
```

- NetworkTransform을 쓰지 않는 이유: NetworkTransform(클라이언트 권한)은 소유자의 위치를 다른 참가자에게 바로 퍼뜨려 호스트가 중간에 검사·보정할 지점이 없습니다. RPC 경로는 호스트가 검사를 통과한 자세만 중계합니다(D-24).
- 검사 로직 `AvatarMoveValidator`(3.11.12)는 `Game.Application`의 순수 C#이라 EditMode에서 시험합니다(`Game.Tests.Application`). PlayerAvatar는 RPC 수신·송신과 호출만 합니다.
- 서버 쪽 스태미나 추정: 호스트는 수락한 자세의 달리기 플래그로 `VitalsRules`(DES-003)를 같이 돌려 스태미나를 추정하고, 추정값이 0이면 허용 속도를 걷기 속도 기준으로 낮춥니다(3.11.12). 추정이 클라이언트 값과 조금 달라도 허용 오차(`MoveTolerance`, `MoveSlackMeters`, `MoveStaminaGraceSeconds`) 안에서 흡수합니다.
- 손에 든 것·생명 상태·체력 비율 같은 아바타 표시 상태(FR-042, FR-159, FR-160)는 호스트 권위 값이므로 클라이언트 자세 RPC에 싣지 않고, 호스트가 정해 ObserversRpc(신뢰, 바뀔 때만) 또는 복제 메시지로 보냅니다.
- 아바타 RPC의 인자는 FishNet 형식이 아닌 Protocol 바이트(`AvatarPose` 인코딩)이므로, 자세 형식·양자화도 다른 메시지와 같은 코덱·왕복 시험을 따릅니다.

#### 3.9.9 부트와 네트워크 시작 순서 (v0.11, M1-07 실제, DES-100·101)

FishNet `NetworkManager`는 **깨어날 때(Awake) 그때 연결된 전송을 초기화**하고, 깨어난 뒤 전송을 바꾸면 전송 이벤트가 이어지지 않습니다. 그래서 Boot 씬의 `NetworkManager`는 **꺼진 상태로 저장**하고(`BootSceneBuilder`), 전송을 정한 뒤에 켭니다.

```mermaid
sequenceDiagram
    participant GB as GameBootstrap (DES-100)
    participant OPT as LaunchOptionsParser·DevCommandLine (DES-103)
    participant CFG as ConfigLoader (DES-104)
    participant CPR as CheckPlatformReady + SteamPlatformService (DES-102)
    participant TS as TransportSelector (DES-101)
    participant NM as NetworkManager (Boot 씬, 꺼진 상태)
    participant GF as GameFlow·SessionRuntime (DES-100)
    GB->>OPT: 명령줄 해석(-genhash·-bench면 해당 도구로 분기, -autoquit 예약)
    GB->>CFG: 설정 로드·검증 (실패 → 오류 로그, 시작 안 함)
    GB->>CPR: Steam 확인 → 경로·전송·신원 결정
    alt Steam 없음
        CPR-->>GB: ShowSteamRequired → SCR-001 (다시 확인 시 이 단계부터 반복)
    else -steamlobbytest (개발, 실제 Steam)
        GB->>GB: SteamLobbySmoke 실행 → 종료 코드로 끝냄
    else 계속
        GB->>TS: Apply(요청 전송) — 출시 빌드는 Steam 고정
        TS->>NM: 전송 구성 요소(Tugboat / FishySteamworks + P2P 켬) 붙임
        TS->>NM: TransportManager가 없으면 직접 AddComponent, Transport 지정
        GB->>NM: gameObject.SetActive(true) — Awake에서 지정된 전송으로 초기화
        GB->>TS: ApplyTuning(서버 틱, 연결 끊김 판정 시간), ApplyConnectionLimit(최대 인원 + 여유 2)
        GB->>GF: 시작(Steam 전송이고 실제 Steam일 때만 로비 서비스 연결)
        opt 개발 빌드 -host / -connect
            GB->>GF: AutoHost() / AutoConnect(ip:port)
        end
        GF->>GF: 접속 완료 → World 씬 Additive 로드, HUD·입력 전환
    end
```

- `TransportSelector`는 전송 선택 규칙(순수 C# `TransportPolicy`)의 결과를 FishNet 구성 요소에 적용하는 일만 합니다. 꺼진 `NetworkManager`는 아직 보조 관리자(`TransportManager`)를 만들지 않았으므로 선택기가 먼저 붙여 두고, FishNet은 깨어날 때 같은 오브젝트의 기존 구성 요소를 재사용합니다.
- **연결 수 여유(NFR-038)**: 전송은 `최대 인원 + 2` 연결까지 받고, 세션 규칙이 초과분에 `JoinRejected(인원 초과)`를 보내 사유를 알린 뒤 끊습니다. M1-07에서 Tugboat 8인 접속과 9번째 참가자의 인원 초과 거부를 확인했습니다.
- 호스트 본인의 연결은 전송이 원격 ID를 주지 못하므로 로컬 플레이어 ID를 씁니다(`SessionRuntime`). 참가자 원격 ID 규칙은 3.7.2와 같습니다.

### 3.10 상태 동적 관점

#### 3.10.1 클라이언트 세션 상태

```mermaid
stateDiagram-v2
    [*] --> Boot
    Boot --> SteamMissing: Steam 미실행·미로그인
    SteamMissing --> Boot: 다시 시도(안내 화면 SCR-001)
    SteamMissing --> [*]: 종료
    Boot --> MainMenu
    MainMenu --> Hosting: 세션 열기
    MainMenu --> Connecting: 참가/다시 참가
    Connecting --> Generating: JoinAccepted
    Connecting --> MainMenu: JoinRejected / 시간 초과
    Generating --> Syncing: 요약값 일치
    Generating --> MainMenu: 요약값 불일치
    Syncing --> InGame: JoinComplete
    Hosting --> InGame
    InGame --> ShutdownCountdown: 호스트 종료 예고 수신
    ShutdownCountdown --> InGame: 취소
    ShutdownCountdown --> MainMenu: 종료
    InGame --> MainMenu: 나가기 / 강퇴 / 호스트 끊김(연결 끊김 판정 시간)
```
- **Steam 실행 필수(v0.7, SRS v0.9)**: 1인 플레이도 Steam이 실행·로그인된 상태에서만 합니다(1인 세션도 호스트 세션과 같은 경로). Steam이 없으면 부트에서 안내 화면(SCR-001)만 보이고 메인 메뉴로 가지 않습니다. 개발 빌드의 Tugboat 같은 PC 시험만 예외입니다(3.7.4).
- **참가 시간 제한(v0.11)**: `Connecting → MainMenu`의 「시간 초과」는 설정값 `NetworkTuning.JoinTimeoutSeconds`(기본 15초)입니다. 호스트는 연결 뒤 이 시간 안에 `JoinRequest`가 없거나, 요청 뒤 이 시간 안에 `JoinComplete`까지 끝나지 않으면 연결을 끊고(`JoinHandshake`), 참가자도 같은 값으로 기다림을 끝냅니다(`ClientSession`)(NFR-039).

#### 3.10.2 플레이어 생명 상태

```mermaid
stateDiagram-v2
    [*] --> Alive
    Alive --> Sleeping: 침대 상호작용
    Sleeping --> Alive: 기상 / 피격
    Alive --> Downed: 체력 0
    Sleeping --> Downed: 체력 0
    Downed --> Alive: 소생 완료(소생 후 체력)
    Downed --> Dead: 쓰러짐 유지 시간 초과 / 포기
    Dead --> Alive: 부활(부활 지점 또는 시작 정화 구역, C-05 규칙)
```

#### 3.10.3 등광탑과 방어전

```mermaid
stateDiagram-v2
    [*] --> Inactive: 건설 완료
    Inactive --> Preparing: 활성화 확인
    Preparing --> Inactive: 취소(활성화자/호스트)
    Preparing --> Wave: 카운트다운 끝 / 전원 준비
    Wave --> Wave: 다음 웨이브(전멸 또는 웨이브 간격, C-14)
    Wave --> Succeeded: 마지막 웨이브 전멸 + 체력 > 0
    Wave --> Failed: 체력 0 (+ C-14 추가 실패 조건)
    Succeeded --> [*]: 정화 구역 추가
    Failed --> [*]: 탑 제거, 재료 일부 드롭
    Inactive --> [*]: 철거
```

#### 3.10.4 자원 오브젝트

```mermaid
stateDiagram-v2
    [*] --> Pristine: 생성(델타 없음)
    Pristine --> Damaged: 타격
    Damaged --> Damaged: 타격(타격 수 < 필요 횟수)
    Damaged --> Felled: 나무 마지막 타격
    Damaged --> Broken: 광석 마지막 타격
    Pristine --> Harvested: 손 채집(E)
    Felled --> Pristine: 재생성(C-08, 결정 시)
    Broken --> Pristine: 재생성(C-08)
    Harvested --> Pristine: 재생성(C-08)
```

#### 3.10.5 작물

```mermaid
stateDiagram-v2
    [*] --> Growing: 심기
    Growing --> Paused: 물 없음 / 햇빛 조건 불충족
    Paused --> Growing: 조건 회복
    Growing --> Harvestable: 생장 누적 ≥ 생장 시간
    Harvestable --> [*]: 수확(제거)
    Harvestable --> Growing: 수확(다시 자라는 작물)
```

#### 3.10.6 생물(적) AI

```mermaid
stateDiagram-v2
    [*] --> Idle
    Idle --> Seek: 대상 발견(탐지 거리) / 웨이브 배정
    Seek --> Attack: 공격 사거리 안
    Attack --> Seek: 대상 이탈
    Seek --> HoldAtBoundary: 다음 칸이 정화 구역
    HoldAtBoundary --> Seek: 대상이 경계 밖으로 나옴
    HoldAtBoundary --> Idle: 대기 시간 초과(C-20)
    Seek --> Repelled: 광원 반경(C-19)
    Repelled --> Seek: 광원 퇴치 지속 시간 끝
    Seek --> Idle: 대상 상실
    Attack --> Dead: 체력 0
    Seek --> Dead: 체력 0
    Dead --> [*]
```

#### 3.10.7 UI 상태와 게임 진행의 분리 (I-07, SRS v0.5 3.2 공통 규칙)

**어떤 화면을 열어도 게임은 멈추지 않습니다. 1인 세션도 같습니다**(1인 일시정지 SUG-027은 보류).

```mermaid
stateDiagram-v2
    [*] --> Gameplay
    Gameplay --> Overlay: 인벤토리·레시피북·지도·보관함·옵션·채팅 등 화면 열기
    Overlay --> Overlay: 다른 화면으로 전환
    Overlay --> Gameplay: 닫기
    note right of Overlay
        로컬 입력 경로만 바뀜
        서버 틱·AI·네트워크·월드 시각은 계속 진행
    end note
```

- UI 상태(`Gameplay`/`Overlay`)는 **로컬 입력 경로만** 바꿉니다. 화면이 열려 있는 동안 이동·시점·공격 입력은 막고 UI 입력을 받습니다.
- `ServerWorld` 틱(DES-052), AI(DES-062), 복제(DES-082), 월드 시각, 상태 값 감소, 버프 시간은 UI 상태를 모릅니다. 애플리케이션 레이어에 「일시정지」 개념이나 API를 두지 않습니다.
- `Time.timeScale`을 바꾸지 않습니다. 메뉴 연출이 필요하면 UI 자체 시간(비스케일 시간)을 씁니다.
- 화면이 열려 있어도 플레이어 캐릭터는 월드에 남아 피해를 받고, 쓰러짐 등 상태 변화가 생기면 HUD 경고가 화면 위에 표시됩니다(배치는 STORYBOARD).
- 화면을 연 상태에서 보내는 명령(예: 레시피북에서 제작, 보관함에서 꺼내기)은 다른 명령과 같은 검증 경로를 거칩니다.
- (v0.13, uGUI) 화면 열기·닫기는 `UiScreenStack`(DES-122)이 하고, 게임 안 창(Window 층)이 하나라도 열려 있으면 `UiState`가 Overlay입니다. Overlay ↔ Gameplay 전환 때 Input Actions 지도(Gameplay ↔ UI)와 커서 잠금만 바꿉니다. 화면 숨김·층 규칙은 3.7.10 (5), 메뉴 연출(열림 애니메이션 등)은 비스케일 시간(`Time.unscaledDeltaTime`)을 씁니다.
- **손목 계기 올림(FR-172, v0.4)** 도 같은 원칙입니다. 손목을 올린 상태는 `Gameplay` 안의 하위 상태로, 모든 행동이 가능하고(C-59 해소) 게임 진행은 멈추지 않습니다. `Overlay`(다른 창)로 들어가면 손목은 자동으로 내려갑니다.

#### 3.10.8 용광로 (v0.6, FR-175~178)

```mermaid
stateDiagram-v2
    [*] --> Idle: 배치
    Idle --> Working: 재료 있음 + 연료(점수 또는 연료 칸) 충분 + 결과물 칸 여유
    Working --> Working: 1개 완료 → 다음 재료 시작
    Working --> StoppedOutputFull: 결과물 칸 가득
    Working --> StoppedNoFuel: 다음 작업에 필요한 연료 부족
    Working --> Idle: 재료 소진
    StoppedOutputFull --> Working: 결과물을 꺼냄
    StoppedNoFuel --> Working: 연료를 넣음
    Idle --> [*]: 철거(칸 내용 드롭)
```
- 상태 전이는 호스트 서버 틱에서만 일어납니다. 플레이어가 떠나도(관심 범위 300 m 밖, 접속 종료 포함) 연료가 있는 동안 계속합니다. 월드가 닫혀 있으면 서버 플레이 시간이 흐르지 않아 진행하지 않고, 밤 건너뛰기로도 앞당겨지지 않습니다(`IFurnaceTimeRule`, C-65 잠정).

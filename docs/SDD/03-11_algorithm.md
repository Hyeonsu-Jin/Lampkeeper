> SDD(소프트웨어 설계 명세서)의 일부 · 목차: [00-index.md](00-index.md)

### 3.11 알고리즘 관점

#### 3.11.1 결정론 원칙 (DES-040~047에 공통 적용)

| 위험 원인 | 대응 규칙 |
|---|---|
| 부동소수점 차이(CPU, 컴파일러 최적화, FMA, Mono/IL2CPP/Burst 차이) | 생성 코드는 **정수와 고정소수점만** 씀. `float`, `double`, `Math.Sin` 등 금지. 높이는 정수 mm, 좌표는 Q16.16, 각도는 16비트 회전 단위(1/65536 바퀴). 부동소수점 변환은 결과를 Unity에 넘길 때(표현)만 하고, 요약값 계산에는 쓰지 않음 |
| 병렬 처리 순서 | 병렬 작업은 **자기 칸에만 쓰기**(입력 읽기 전용, 출력 칸 고정). 합계·목록 합치기는 단일 스레드에서 고정 순서(지역 → 셀Z → 셀X → 슬롯)로. 침식처럼 이웃을 읽는 반복은 더블 버퍼 |
| 난수 호출 순서 | 상태를 가진 난수(`System.Random`, `UnityEngine.Random`) 금지. **카운터 기반 난수** `Rand(스트림 키, a, b, c)`만 사용. 스트림 키는 (월드 시드, 지역, 단계, 재시도 번호)에서 파생 → 지역·단계마다 독립 |
| 컬렉션 순서 | `Dictionary`·`HashSet` 순회 결과를 생성 결과에 쓰지 않음. 정렬된 배열만 사용. 정렬은 키가 같은 경우가 없게(동률은 ID로) |
| 문화권·플랫폼 의존 API | `string.ToUpper`, `Normalize`, `GetHashCode`, `DateTime` 금지. 시드 문자열은 UTF-8 바이트 그대로 해시 |
| Unity·라이브러리 버전 | 생성 코드는 UnityEngine·Burst·Unity.Mathematics를 쓰지 않는 순수 C# 어셈블리(`Game.WorldGen`). 엔진을 올리면 골든 해시 시험(DES-113)으로 확인 |
| 설정값 변경 | 생성에 영향을 주는 설정값(경계 최대 단차, 통행 가능 최대 경사, 검증 기준 등)은 **생성 프로필**에 두고 생성기 버전에 묶음. 바꾸려면 새 생성기 버전을 만듦 |
| Unity Terrain 변환 | 정수 높이 → `TerrainData.SetHeights`(float 0~1) 변환은 표현용. 자원 오브젝트의 높이(Y)도 정수 높이에서 계산해 생성 표에 저장하므로 PC 간 차이 없음 |

#### 3.11.2 시드 처리 (DES-041, FR-167)

```
// 입력: 사용자가 입력한 문자열 또는 「무작위」
func SeedFromText(text):
    t = TrimAsciiWhitespace(text)                 // 앞뒤 ASCII 공백만 제거. 대소문자 구분
    if t.Length == 0 or Utf8Length(t) > 64: return Error(InvalidSeed)
    return XXH64(Utf8Bytes(t), seed: SEED_SALT)   // XXH64는 자체 구현 고정(라이브러리 교체로 값이 바뀌지 않게)

func RandomSeedText():
    // 생성 시점에만 쓰므로 결정론 불필요. 혼동되는 글자(0/O, 1/I) 제외 32자 알파벳에서 10글자
    return 10 x Alphabet32[CryptoRandom() % 32]    // 예: "K7QM-2RTX-9B" 형식으로 표시(하이픈은 표시용, 해시에 미포함)
// 월드 파일에는 seedText와 seed(u64)를 함께 기록. 화면에는 seedText를 보여 주고 복사하게 함
```

#### 3.11.3 카운터 기반 난수와 정수 노이즈 (DES-040)

```
func Mix64(x):                                    // SplitMix64 마무리 함수, 모든 연산은 unchecked uint64
    x ^= x >> 30; x *= 0xBF58476D1CE4E5B9
    x ^= x >> 27; x *= 0x94D049BB133111EB
    x ^= x >> 31; return x

func StreamKey(worldSeed, regionId, stageId, attempt):
    return Mix64(worldSeed ^ Mix64((regionId << 48) | (stageId << 32) | attempt))

func Rand(key, a, b, c):                          // a, b, c = 정수 좌표·번호
    return Mix64(key ^ Mix64(a * 0x9E3779B97F4A7C15 + b * 0xC2B2AE3D27D4EB4F + c * 0x165667B19E3779F9))

func RandBelow(key, a, b, c, n):                  // [0, n) 정수, 나눗셈·실수 없음
    return (uint)(((Rand(key, a, b, c) >> 32) * (ulong)n) >> 32)

// 고정소수점 Q16.16 (int). 곱셈은 long 중간값 후 >> 16 (산술 시프트)
func LatticeQ15(key, ix, iz): return (int)(Rand(key, ix, iz, 0) >> 49) - 16384   // [-16384, 16383]
func Smooth(tQ16): t2 = (tQ16 * tQ16) >> 16; t3 = (t2 * tQ16) >> 16; return 3*t2 - 2*t3
func ValueNoise(key, xQ16, zQ16):
    ix = xQ16 >> 16; iz = zQ16 >> 16
    sx = Smooth(xQ16 & 0xFFFF); sz = Smooth(zQ16 & 0xFFFF)
    a = LatticeQ15(key, ix, iz);     b = LatticeQ15(key, ix+1, iz)
    c = LatticeQ15(key, ix, iz+1);   d = LatticeQ15(key, ix+1, iz+1)
    ab = a + (((long)(b - a) * sx) >> 16); cd = c + (((long)(d - c) * sx) >> 16)
    return ab + (((long)(cd - ab) * sz) >> 16)
func Fbm(key, xQ16, zQ16, octaves, gainQ16):
    sum = 0; amp = 1 << 16
    for o in 0 .. octaves-1:
        sum += (ValueNoise(Rand(key, o, 0, 0), xQ16 << o, zQ16 << o) * amp) >> 16
        amp = (amp * gainQ16) >> 16
    return sum
// 노이즈 종류(값/기울기/도메인 왜곡)는 구현 때 고르되 위 규칙(정수, 카운터 난수, 산술 시프트)을 지킨다
```

#### 3.11.4 월드 생성 파이프라인 (DES-043, FR-168)

```
func Generate(seed, genVersion, layout, profile, retryPolicy) -> GenResult:
    for attempt in 0 .. retryPolicy.MaxAttempts - 1:
        s = retryPolicy.SeedFor(seed, attempt)                     // 기본: attempt 0 = seed, 이후 Mix64(seed ^ attempt*K)
        w = new GeneratedWorld(layout)
        // 1) 높이: 큰 배치 높이 + 지역 내부 노이즈. 경계는 고정 폭 구간에서 정수 가중치로 섞음
        ParallelFor(tile in layout.Tiles):                          // 각 타일은 자기 칸만 기록
            for (x, z) in tile:
                r = layout.RegionAt(x, z); wts = layout.BlendWeightsQ16(x, z)   // 고정 데이터, 정수
                h = layout.MacroHeightMm(x, z)
                for (region, wt) in wts: h += (RegionHeightMm(StreamKey(s, region, H, attempt), region, x, z) * wt) >> 16
                w.Height[x, z] = h
        // 2) 침식: 열 침식 K회, 더블 버퍼(이웃 읽기 → 다음 버퍼 쓰기)
        for i in 0 .. profile.ErosionIterations-1:
            ParallelFor(row): next[row] = ThermalStep(cur, row, profile.TalusMm)
            swap(cur, next)
        // 3) 물: 거친 격자(8 m)에서 단일 스레드, 정해진 순서
        lakes  = FillDepressions(coarse(w.Height))                 // 우선순위 큐, 동률은 (z, x) 작은 쪽 먼저
        rivers = TraceRivers(StreamKey(s, *, RIVER, attempt), coarse, lakes, profile)   // 수원 후보를 고정 순서로 정렬해 차례로 하강
        ParallelFor(tile): CarveWater(w, rivers, lakes, tile)
        // 4) 경계 단차 보정: 지역 경계 1 m 구간에서 높이 차 > 경계 최대 단차면 정수 보간으로 완화
        ParallelFor(boundaryTile): SmoothBoundary(w, profile.MaxBoundaryStepMm)
        // 5) 고정 콘텐츠: 장막의 근원 높이 덮어쓰기, 시작 정화 구역 평탄화 (손으로 만든 데이터)
        StampFixed(w, layout)
        // 6) 자원 오브젝트: 셀(16 m)마다 고정 슬롯 수만큼 후보 → 규칙 필터
        ParallelFor(cell): cand[cell] = PlaceInCell(StreamKey(s, cell.region, RES, attempt), cell, w, profile)
        w.Resources = []
        for cell in SortedCells(region, cz, cx):                   // 고정 순서로 합침
            for c in cand[cell] (slot 오름차순):
                if c.valid: w.Resources.Add(c with Id = Pack(cell.region, cell.cx, cell.cz, c.slot))
        w.Resources.AddRange(layout.FixedResources)                 // 근원·시작 지점의 손 배치 자원(지역 7 대역 ID)
        SortById(w.Resources); AssignDenseIndex(w.Resources)        // 네트워크용 ResourceIndex
        // 7) 일반 등광탑 후보 자리: 지역별 후보 점수 계산(병렬) → 점수·ID 순 정렬 → 최소 간격 탐욕 선택(단일 스레드)
        w.TowerSites = PickTowerSites(w, StreamKey(s, *, SITE, attempt), profile)
        // 8) 검증
        report = Validate(w, profile.Criteria)                     // 3.11.6
        if report.Ok:
            w.Hash = ComputeHash(w, genVersion, seed, attempt)      // 3.11.5
            return GenResult(w, attempt, report)
        log(report.Reasons)
    return retryPolicy.OnExhausted(seed)                            // 기본(잠정): 실패 → UI가 다른 시드 요청 (C-55)

func PlaceInCell(key, cell, w, profile):
    out = []
    for slot in 0 .. profile.SlotsPerCell-1:                       // 슬롯 번호가 ID에 들어감 → 안정적
        jx = RandBelow(key, cell.cx, cell.cz, slot*4+0, CELL_MM); jz = RandBelow(key, cell.cx, cell.cz, slot*4+1, CELL_MM)
        p = cell.originMm + (jx, jz)
        type = profile.Biome(cell.region).PickType(RandBelow(key, cell.cx, cell.cz, slot*4+2, 65536), w.SlopeQ16(p), w.WaterDist(p), w.HeightMm(p))
        valid = type != None and not w.IsWater(p) and not layout.IsReserved(p) and SpacingOk(out, p, type)
        yaw = (ushort)RandBelow(key, cell.cx, cell.cz, slot*4+3, 65536)
        out.Add(Candidate(slot, type, p, w.HeightMm(p), yaw, scaleQ8 = profile.ScaleFor(type, slot), valid))
    return out
```

**석탄 광맥(v0.6, FR-179)**: 생성 프로필의 지역별 자원 종류 표(`Biome(region).PickType`)에 석탄 광맥을 추가합니다(분포 후보: 모든 생성 지역, C-64). 생성 알고리즘은 바뀌지 않지만 프로필 값이 바뀌므로 생성 결과가 달라집니다. 개발 중 생성기 0.x라 버전 보존은 필요 없고(3.11.7), 골든 해시 갱신과 결정론 재시험(이 PC Mono·IL2CPP 교차, 다른 CPU는 M6-10)을 합니다. 석탄은 생성 검증 대상(진행 재료, SRS v0.8)이므로 검증 조건(3)의 `지역별 진행 재료 최소 수량`에 석탄을 넣습니다. 나무로도 가공할 수 있어 석탄이 적어도 진행이 막히지는 않지만, 검증 기준을 밑돌면 다른 진행 재료와 같이 재생성 대상입니다.

생성 시간 목표(NFR-052, 25초 이하)를 맞추는 방법: 타일·셀 단위 `Parallel.For`(6코어 12스레드 사용), 높이 1 m 간격, 침식 반복 수와 거친 격자 크기는 생성 프로필 값. 생성은 백그라운드 스레드에서 돌고 메인 스레드는 진행 표시만 갱신합니다(응답 없음 방지).

#### 3.11.5 생성 결과 요약값 (DES-046, FR-169, NFR-053)

```
func ComputeHash(w, genVersion, seed, attempt):
    h = XXH64Stream(seed: HASH_SALT)
    h.Write("KHWG"); h.Write(genVersion); h.Write(seed); h.Write(attempt)
    regionHashes = []
    for tile in Tiles (고정 순서): h.Write(w.Height[tile] as int32 little-endian 배열)
    h.Write(w.WaterMask bytes)
    for r in w.Resources (Id 오름차순): h.Write(r.Id, r.Type, r.PosXmm, r.PosYmm, r.PosZmm, r.Yaw, r.ScaleQ8)
    for t in w.TowerSites (Id 오름차순): h.Write(t.Id, t.PosXmm, t.PosZmm, t.Kind)
    // 디버깅용: 지역별·타일별 부분 요약값도 같이 계산해 불일치 위치를 좁힘
    return (h.Digest(), perRegion, perTile)
```
- 참가자는 `JoinAccepted`의 기대 요약값과 자기 결과를 비교하고, 다르면 부분 요약값과 함께 로그에 남깁니다(FR-169).

#### 3.11.6 생성 결과 검증과 재생성 (DES-045, FR-170)

```
func Validate(w, c) -> Report:
    g = CoarseGraph(w, cellMm = c.ValidationCellMm /*8 m*/, maxSlopeQ16 = TanQ16(c.MaxSlopeDeg /*35°*/), waterBlocks = true)
    reasons = []
    // (1) 지역 순서대로 걸어서 갈 수 있음: 지역 1..k만 허용한 BFS로 지역 k+1 경계에 닿는지
    for k in 1 .. 4:
        if not BfsReaches(g, layout.StartCell, allowed = regions ≤ k, goal = BoundaryCells(k, k+1)): reasons.Add(UNREACHABLE(k → k+1))
    // (4) 시작 지점 → 장막의 근원 입구
    if not BfsReaches(g, layout.StartCell, allowed = all, goal = layout.VeilSourceEntry): reasons.Add(NO_PATH_TO_SOURCE)
    // (2) 등광탑 후보 자리 수
    for r in regions 1..4: if count(w.TowerSites in r) < c.MinTowerSites[r]: reasons.Add(FEW_SITES(r))
    // (3) 진행 재료 수량: 자원 종류별 드롭 기대 최소량(드롭 표 최소값) 합계
    for r in regions 1..4: for (mat, need) in c.MinMaterials[r]:
        if Σ MinYield(res, mat) for res in w.Resources in r < need: reasons.Add(FEW_MATERIAL(r, mat))
    return Report(reasons.Count == 0, reasons)
// BFS는 4-이웃, 큐는 FIFO, 이웃 방문 순서 고정 → 결과(도달 여부)만 쓰므로 순서와 무관
// 재생성 정책은 IGenRetryPolicy (C-55 교체 지점): 기본 DerivedSeedRetry(최대 16회), 대안 RejectSeed(첫 실패 즉시 거부)
```

#### 3.11.7 생성기 버전 관리 (DES-044, NFR-054)

```
registry = { 0: V0PrototypeGenerator (개발 전용), 1: V1Generator, 2: V2Generator, ... }
latest = max(registry.keys where released or devBuild)

func GeneratorFor(version):
    if version not in registry: return Error(GeneratorUnsupported)   // 참가 거부 「게임 버전 불일치」(FR-169)
    return registry[version]
// 새 월드: latest. 불러오기·참가: 월드 파일/JoinAccepted의 version
```
동결 규칙
1. 생성기 버전 N의 코드(`WorldGen/VN/`)와 생성 프로필(`Data/WorldGen/Profiles/VN/`)은 공개 배포 후 수정하지 않습니다. 버그 수정도 새 버전 N+1로 합니다.
2. `WorldGen/Common/`의 결정론 수학은 **추가만** 합니다. 기존 함수의 동작을 바꾸면 모든 버전의 골든 해시가 깨지므로 시험에서 바로 드러납니다.
3. 버전마다 골든 해시(시드 32개의 요약값)를 `Tests/EditMode/WorldGen/Golden/VN.json`에 두고 매 빌드 비교합니다.
4. **(확정, 사용자 결정 2026-11-02)** 개발 중 생성기는 `0.x` 개발 버전이며 보존하지 않습니다. 0.x로 만든 월드는 새 빌드에서 「지원하지 않는 개발 버전 월드」로 안내하고 열지 않습니다. **첫 공개 빌드(외부 테스트 빌드 포함)에 들어가는 생성기를 V1로 동결**하고, 그때부터 위 1~3 규칙을 적용합니다. 외부 테스트 빌드를 내보내기 전 점검표에 「생성기 버전 동결 확인」을 넣습니다.
5. 지원을 끝낼 버전이 생기면 업데이트 전에 옮기는 방법을 정합니다(NFR-054). 이 설계는 옮기는 도구를 미리 만들지 않습니다.

#### 3.11.8 엔터티 테이블 복제 (DES-053, DES-082)

```
class ReplicatedTable<TState>:
    dense: TState[]                 // 자원처럼 ID가 미리 정해진 경우 / 또는 Dictionary<EntityId, TState> (실행 중 발급)
    dirty: OrderedSet<Id>           // 이번 틱에 바뀐 ID (삽입 순서 유지)
    removed: List<Id>
    func Set(id, state): dense[id] = state; dirty.Add(id)
    func DrainJournal(): changes = [(id, dense[id]) for id in dirty] + removed; dirty.Clear(); removed.Clear(); return changes
    func CaptureSnapshot(): return copy of (바뀐 상태 항목 전체)   // 자원은 Pristine이 아닌 것만

func OnServerTickEnd(tick):          // 30Hz
    for table in reliableTables:     // 자원, 드롭, 건축물, 작물, 등광탑, 정화 구역, 진행
        ch = table.DrainJournal()
        if ch.Any: for conn in readyConns: conn.Reliable.Enqueue(TableDelta(tick, table.Id, Encode(ch)))
        for conn in syncingConns: conn.Pending.Enqueue(ch)          // 스냅샷 받는 중인 연결은 보관
    CreatureReplication(tick)        // 3.11.9
    for conn in conns: conn.Flush()  // 메시지 묶어 보내기(FishNet이 MTU 단위로 분할)
```
- 자원 상태 인코딩: `ResourceIndex(u16) + hits(u8) + phase(4비트) + fallDir(4비트)` = 4바이트.
- 드롭 아이템·작물·건축물은 생성/변경/삭제 이벤트 단위로 보냅니다. 건축물은 위치(float 3개)·방향(u16)·종류(u16)·체력%(u8).
- **제작대·용광로(v0.6)**: 둘 다 건축물 테이블의 항목입니다(별도 테이블 없음). 제작대는 상태가 없습니다. 용광로는 건축물 항목에 `lit`(불 켜짐, 1비트)만 더해 모든 참가자에게 보내고(불빛·연기 표시), 칸 내용·연료 점수·작업 진행은 **열어 본 사람에게만** `FurnaceView`로 보냅니다(보관함 구독 방식). 진행률은 매 틱 보내지 않고 「작업 시작 서버 시각 + 가공 시간」만 보내 클라이언트가 계산합니다. 관심 범위 밖이어도 호스트는 계속 계산합니다(3.11.18). 중도 참가 스냅샷에는 `lit`만 들어갑니다.

#### 3.11.9 생물 복제와 관심 범위 (DES-082)

```
tiers = config.InterestTiers   // 기본: Near < 50 m: 매 틱(30Hz) / Mid < 150 m: 3틱마다(10Hz) / Far < 300 m: 15틱마다(2Hz) / Out ≥ 300 m: 보내지 않음(디스폰)
func CreatureReplication(tick):
    for conn in readyConns:
        p = conn.PlayerPosition
        batch = []
        for a in creatures:
            t = TierOf(Distance(p, a.Pos))
            if t == Out: if conn.Known(a): conn.Reliable.Enqueue(CreatureDespawn(a.Id)); conn.Forget(a); continue
            if not conn.Known(a): conn.Reliable.Enqueue(CreatureSpawn(a)); conn.Remember(a)
            if (tick + a.Id) % tiers[t].IntervalTicks == 0: batch.Add(Quantize(a))   // ID로 위상 분산 → 틱마다 고른 부하
        if batch.Any: conn.Unreliable.Enqueue(CreatureSnapshot(tick, batch))
// 클라이언트: 생물마다 스냅샷 버퍼, 렌더 시각 = 서버 시각 - 보간 지연(기본 100 ms + 등급 간격)으로 보간
// 양자화: 위치 = 월드 좌표를 2^16 단계(약 4.7 cm), 방향 8비트, 애니메이션 상태 8비트, 체력% 8비트
```
- 플레이어 아바타(최대 8개)는 FishNet NetworkObject이고(v0.10: NetworkTransform 대신 ServerRpc → 호스트 검사 → ObserversRpc, 3.9.8) **항상 전원 관찰**합니다(나침반·동료 목록이 거리와 무관하게 필요, FR-125, FR-160).
- 자원·건축물 델타는 양이 작아 관심 범위 없이 전원에게 보냅니다(3.12.5 대역폭 추정).
- **동기화 범위(확정, 사용자 결정 2026-11-02)**: FR-008의 「모든 플레이어에게 같게」는 생물에 대해 「각 참가자의 관심 범위(기본 300 m, 설정값) 안에서 같게」로 적용합니다. 범위 밖 생물은 그 참가자에게 보내지 않습니다.

#### 3.11.10 서버 틱과 AI 일정 (DES-052, DES-062)

```
// 렌더 프레임과 분리: FishNet TimeManager OnTick (기본 30Hz, 설정값)
func OnTick(tick, dt):
    perf.Begin("Server.Total")
    aiBackend.CompleteSteering()                    // 지난 프레임 LateUpdate에 예약한 잡 완료 → 위치 반영
    ApplySteeringResults()                          // 생물 테이블 갱신, 정화 구역 진입 차단 확인
    commands.ProcessQueued(maxPerTick)              // 도착 순서대로, 명령별 처리 시간 측정
    group = tick % config.DecisionDivisor           // 기본 3 → 판단 10Hz
    for a in creatures where (a.Id % DecisionDivisor == group) and a.Lod.ShouldDecide(tick):
        a.Decide(ctx)                               // 도메인 규칙(순수 C#): 대상 선택, 공격 개시, 상태 전이
    ResolveAttacks(tick)                            // 거리·각도·쿨다운으로 판정(물리·애니메이션에 의존하지 않음)
    systems.Tick(dt)                                // 시각, 상태 값 감소, 버프, 작물, 위협, 스폰, 웨이브, 저장 일정
    OnServerTickEnd(tick)                           // 복제(3.11.8)
    perf.End("Server.Total")

func LateUpdate():                                  // 렌더 프레임마다
    if steeringPending: aiBackend.ScheduleSteering(creatures, tickDt)   // 잡을 렌더링 동안 워커 스레드에서 돌림
// AI LOD (가장 가까운 플레이어와의 거리): Near < 40 m 판단 10Hz·이동 매 틱 / Mid < 120 m 판단 5Hz·이동 매 틱 / Far 판단 2Hz·이동 3틱마다 / 플레이어 300 m 밖: 정지(웨이브 적 제외)
```

#### 3.11.11 흐름장 길 찾기 (DES-105)

```
// 보행 격자: 2 m 칸. 지형 경사·물·건축물·정화 구역(적 통행 금지) 반영. 호스트만 보유
func BuildWalkGrid(region):                                         // 월드 로드 시 1회 + 건축·정화 변경 시 해당 칸만
    ParallelFor(cell): walk[cell] = slope ≤ maxSlope and not water and not structureBlocked and not purified
// 흐름장: 목표(등광탑 둘레 칸들 / 대상 플레이어 칸) 기준 Dijkstra(정수 비용, 8-이웃) → 칸마다 다음 방향
job BuildFlowField(walk, targets, radiusCells) [Burst]:
    cost = INF; heap = targets with 0
    while heap: c = pop; for n in Neighbors8(c) within radius: if walk[n] and cost[c]+step < cost[n]: cost[n] = ...; push
    dir[c] = argmin_n cost[n]
// 사용: 웨이브 = 활성 등광탑마다 흐름장 1개(반경 200 m, 건축 변경 시 다시 계산)
//       밤 습격 = 대상 플레이어마다 흐름장 1개(반경 128 m, 1Hz 재계산, 최대 8개)
//       동물·배회 = 흐름장 없이 지역 조향(무작위 목표점 + 보행 격자 확인)
job Steering(agents, flowFields, spatialHash, dt) [Burst, IJobParallelFor]:
    desired = flow.dir[agent.cell] * speed + Separation(spatialHash, agent) + Arrive(target)
    next = agent.pos + desired * dt
    if Purified(next) and agent.IsHostile: next = agent.pos; agent.flags |= HoldAtBoundary   // FR-067: 경계를 넘는 위치는 만들지 않음
    next.y = SampleHeight(heightGrid, next)                  // 물리 레이캐스트 없이 높이 격자에서
    out[agent.index] = next                                   // 자기 칸에만 기록
```
- NavMesh를 쓰지 않는 이유: 3 km 생성 지형에 실행 중 NavMesh를 구우면 시간·메모리가 크고, 60체가 각자 경로를 구하면 메인 스레드 비용이 큽니다(5장 D-07).

#### 3.11.12 참가자 이동 검증 (DES-084, NFR-030)

```
// 이동은 클라이언트 계산 + 호스트 검사. v0.10: NetworkTransform 대신 소유자 ServerRpc(AvatarPose 바이트, 비신뢰) → 호스트 검사 → ObserversRpc (3.9.8)
// PlayerAvatar(DES-084, Net 어댑터)는 디코드와 RPC만, 판단은 AvatarMoveValidator(Game.Application, 순수 C#)
func PlayerAvatar.OnPoseServerRpc(bytes):                      // 호스트에서
    if not MessageCodec.TryDecodeAvatarPose(bytes, out pose): log.Warn("pose_decode_failed", Owner); return
    r = validator.Check(state[Owner], pose, now)
    if r.Accepted: serverPlayer.Position = pose.Pos; ObserversRpc(ExcludeOwner, Encode(pose))   // 거리 검증(FR-007)은 이 서버 위치를 씀
    else: TargetRpc(Owner, Correction(r.LastAcceptedPos)); log.Warn("move_rejected", Owner)

// v0.11: 실제 구현(Application/Server/ServerWorld.cs의 AvatarMoveValidator)에 맞춤. m = BalanceValues.Movement
func AvatarMoveValidator.Check(s, pose):                      // 연결별 상태 s (LastAccepted, Stamina, SecondsAtZero)
    if not pose.Pos.IsFinite or |pose.Pos.X| > half or |pose.Pos.Z| > half: return Rejected   // 월드 밖·NaN·무한대
    if not s.HasLast: s.LastAccepted = pose; return Accepted                 // 스폰 직후 첫 자세는 기준점
    deltaMs = (int)(pose.TimeMs - s.LastAccepted.TimeMs)                      // 보낸 쪽 시각, 한 바퀴 돌아도 부호 있는 차이
    if deltaMs <= 0: return Stale                                            // 늦게 온·중복 자세는 버림(보정 안 함)
    dt = min(deltaMs / 1000, m.MaxPoseDeltaSeconds(0.5))
    runAllowed = s.Stamina.CanRun or s.SecondsAtZero <= m.MoveStaminaGraceSeconds(0.5)
    speed = (pose.Running and runAllowed) ? m.RunSpeedMps : m.WalkSpeedMps   // 스태미나 0 → 걷기 속도가 상한(SRS v0.10)
    allowedH = speed * m.MoveTolerance(1.25) * dt + m.MoveSlackMeters(0.5)
    allowedV = m.MaxVerticalSpeedMps(40) * dt + m.MoveSlackMeters(0.5)       // 낙하·점프
    if Horizontal(pose.Pos - s.LastAccepted.Pos) > allowedH or |pose.Pos.Y - s.LastAccepted.Pos.Y| > allowedV: return Rejected
    s.Stamina.Tick(dt, running: pose.Running and s.Stamina.CanRun)          // 받아들인 자세로 서버 쪽 스태미나 추정(회복 지연 1초 포함)
    s.SecondsAtZero = (s.Stamina.Current <= 0) ? s.SecondsAtZero + dt : 0
    s.LastAccepted = pose; return Accepted
```
- v0.10: 스태미나 추정은 클라이언트 값과 프레임 단위로 어긋날 수 있으므로, 추정값이 0이 된 뒤 처음 0.5초(설정값 `MoveStaminaGraceSeconds`, v0.10의 `MoveStaminaGrace`)는 달리기 속도를 계속 허용합니다. 오탐으로 정상 플레이어가 되돌려지지 않게 하는 여유이며 M1-08·M1-12 로그(`move_rejected`)로 조정합니다.
- v0.11: 결과는 세 가지입니다 — `Accepted`(중계), `Rejected`(마지막 수락 위치로 보정), `Stale`(시각이 앞서지 않은 자세. 비신뢰 채널 순서 뒤바뀜이라 보정하지 않고 버림). 시각 간격 상한(`MaxPoseDeltaSeconds`)은 오래 끊겼다 온 자세 하나로 큰 이동을 인정하지 않게 합니다.

이동 검사 설정값 (v0.11, `BalanceValues.Movement`, 애셋 `Data/Balance/BalanceConfig.asset`. 모두 `ConfigValidator`가 로드 때 검사하고 어기면 시작하지 않음)

| 설정값 | 기본값 | 검사 규칙 | 뜻 |
|---|---|---|---|
| `WalkSpeedMps` / `RunSpeedMps` | 4 / 7 m/s | > 0 / 걷기 이상 | 걷기·달리기 속도(FR-023, FR-024) |
| `MoveTolerance` | 1.25 | ≥ 1 | 속도 허용 배율(네트워크 지터 흡수) |
| `MoveSlackMeters` | 0.5 m | ≥ 0 | 매 검사에 더하는 여유 거리(수평·수직 공통) |
| `MoveStaminaGraceSeconds` | 0.5 s | ≥ 0 | 서버 추정 스태미나 0 뒤 달리기 속도를 더 인정하는 시간 |
| `MaxPoseDeltaSeconds` | 0.5 s | > 0 | 한 번에 인정하는 자세 시간 간격 상한 |
| `MaxVerticalSpeedMps` | 40 m/s | > 0 | 수직 이동(낙하·점프) 최대 속도 |
- **확정(사용자 결정 2026-11-02)**: 이동은 클라이언트가 계산하고 호스트는 속도·범위만 검사합니다. 속도 한도 안의 위치 조작은 막지 못하므로 **Check 단계 보안 점검(security-auditor) 대상**으로 둡니다(8장 R-07). `move_rejected` 로그는 점검 근거 자료로 씁니다.

#### 3.11.13 요청 빈도 제한 (DES-050, NFR-041, NFR-050)

```
class TokenBucket(rate, burst): tokens = burst; last = now
    func TryTake(): tokens = min(burst, tokens + (now - last) * rate); last = now; if tokens >= 1: tokens -= 1; return true; return false
// 연결마다: 전체 버킷(초당 요청 상한) + 명령 종류별 버킷
// 공격·타격: rate = 들고 있는 도구의 공격 속도(초당) — 조작 클라이언트가 10배로 보내도 적용 수 ≤ 공격 속도
// 채팅: 채팅 빈도 상한
// v0.11(M1-09 실제): 버킷을 통과한 명령은 ServerWorld 대기열에 들어가고, 틱마다 최대 MaxCommandsPerTick(64)개만 처리,
//   대기열이 MaxQueuedCommands(256)개에 이르면 새 명령은 버리고 로그(서버 처리 예산 보호, NFR-041)
```
- v0.11: 두 상한은 `NetworkTuning` 설정값이고 `ConfigValidator`가 `MaxCommandsPerTick ≥ 1`, `MaxQueuedCommands ≥ MaxCommandsPerTick`을 검사합니다(3.12.1 표).

#### 3.11.14 원자적 저장 (DES-099, NFR-036)

```
func SaveAtomic(folder, bytes):
    tmp = folder/"world.sav.tmp"; cur = folder/"world.sav"
    WriteAll(tmp, bytes); FlushToDisk(tmp)
    if not Codec.VerifyBodyHash(ReadAll(tmp)): Delete(tmp); return Error(SaveVerifyFailed)
    if Exists(cur):
        bak = folder/"backups"/("world_" + UtcNow.ToString("yyyyMMdd_HHmmss", Invariant) + ".sav")
        File.Replace(tmp, cur, bak)          // 같은 볼륨에서 교체 + 이전 파일을 백업으로 이동
    else: File.Move(tmp, cur)
    PruneBackups(folder/"backups", keep = 백업 개수)    // 파일 이름의 시각 순, 오래된 것부터 삭제
// 불러오기: 헤더 magic·버전 확인 → 본문 해시 확인 → 실패 시 「파일 손상」 + 가장 최근 백업 복원 제안(NFR-037)
// 남은 .tmp는 다음 실행 때 삭제
```

#### 3.11.15 웨이브 규모와 배율 (DES-018, FR-064, FR-156)

```
func ComposeWave(tower, waveIndex, playerCountAtStart, difficulty, rules):
    def = tower.IsTutorial ? config.TutorialDefense : config.Defense[tower.Kind][tower.Region]
    w = def.Waves[waveIndex]
    countMul = config.WaveCountMul[playerCountAtStart]            // 인원별 웨이브 배율
    enemies = [ (type, floor(n * countMul)) for (type, n) in w.Composition ]
    if waveIndex == def.Waves.Count - 1: enemies.Add(boss = def.Boss)
    hp(e) = e.BaseHp * difficulty.EnemyHealthMul * (e.IsBoss ? config.BossHpMul[playerCountAtStart] : 1.0)
    dmg(e) = e.BaseDmg * difficulty.EnemyDamageMul
    return enemies
// 확정 규칙(사용자 결정 2026-11-02): 인원 배율은 웨이브 적 수와 보스 체력에만 적용. 일반 적 체력에는 적용하지 않음(교체 지점 아님)
// 방어전 중 인원 변화(C-15)는 IPlayerCountChangeRule: 기본 = 시작 시 인원 고정
```

#### 3.11.16 요리 가능 목록 (DES-023, FR-091~095)

```
func AvailableRecipes(pot, structures, inventory):
    utensils = { s.Kind for s in structures where s.IsUtensil and Distance(s.Pos, pot.Pos) ≤ 기구 인식 거리 }
    for r in recipes (해금된 것, C-23):
        missingUtensils = r.RequiredUtensils - utensils
        missingItems = [ (i, n - inventory.Count(i)) for (i, n) in r.Ingredients if inventory.Count(i) < n ]
        yield (r, state = missingUtensils.Any ? UtensilMissing : missingItems.Any ? MissingMaterials : Cookable, missingUtensils, missingItems)
// 실행(Cook 명령)은 호스트에서 같은 함수로 다시 판정한 뒤 재료 차감(FR-007). 정해진 요리법 외 조합을 넣는 명령은 존재하지 않음(FR-095)
```

#### 3.11.17 밤 건너뛰기 판정 (DES-013, FR-162)

```
func CanSkipNight(world):
    if not world.Clock.IsNight: return No(NotNight)
    present = world.ConnectedPlayers                              // 접속 인원 전원(예약 자리 제외)
    sleeping = present.Where(p => p.Life == Sleeping)
    if sleeping.Count < present.Count: return No(SleepingCount(sleeping.Count, present.Count))
    if sleeping.Any(p => not world.Zones.IsPurified(p.Bed.Pos)): return No(BedOutsidePurified)
    if world.Defense.AnyPreparingOrActive: return No(DefenseInProgress)
    return Yes   // 건너뛴 시간 처리(작물·버프·상태)는 ISkipTimeRule (C-42)
```

#### 3.11.18 용광로 연료와 가공 (DES-036, DES-075, FR-175~178)

```
// 설정값(BalanceConfig, 3.7.7, SRS v0.8 FR-176): fuelPoints[item] (나무 2, 석탄 16, 숯 16 후보 C-62), smeltCostPoints = 4 (광석 1개),
//   smeltSeconds[ore], charcoalRecipe(후보: 나무 4개 + 연료 4점 → 숯 1개, C-63), 숯 시간, 용광로 최대 수
// 정수만 사용: 나무 2개(4점) = 광석 1개(4점), 석탄 1개(16점) = 광석 4개, 나무 1개 = 0.5개분(2점)
// 태우고 남은 점수는 용광로에 남아 다음 가공에 쓰이며 꺼낼 수 없고 저장됨. 태우지 않은 연료 칸 아이템은 꺼낼 수 있음

func TryStartJob(f, now):                          // 칸이 바뀔 때, 작업이 끝날 때만 호출(매 틱 아님)
    job = f.Input.Any ? JobFor(f.Input.Item) : None // 광석 → 제련, 나무(숯 만들기 칸 또는 레시피) → 숯 — 구분 방법은 STORYBOARD/SRS를 따름
    if job == None: return Stop(f, Idle)
    if not f.Output.CanAccept(job.Result): return Stop(f, OutputFull)
    while f.FuelPoints < job.CostPoints and f.FuelSlot.Any:      // 남은 점수가 4점 미만일 때만 연료 아이템을 1개씩 태워 보충
        f.FuelPoints += fuelPoints[f.FuelSlot.Item]; f.FuelSlot.Remove(1)
    if f.FuelPoints < job.CostPoints: return Stop(f, NoFuel)
    f.FuelPoints -= job.CostPoints                  // 가공 1회 시작 때 4점 차감(중간 정지·철거 시 환불 없음 — 잠정)
    f.Input.Remove(job.InputCount); f.LastInputBy = f.InputOwnerOf(job)   // 경험치 대상(C-66 잠정: 재료 투입자)
    f.Job = (job, startAt = now, endAt = now + job.Seconds)
    scheduler.Push(f.Id, f.Job.endAt)               // 최소 힙(완료 시각 순)
    f.Lit = true; Notify(f)                         // 구독자에게 FurnaceView, 건축물 테이블의 lit 갱신

func OnServerTick(now):                             // FurnaceService, 30 Hz
    while scheduler.PeekTime() <= now:              // 이번 틱에 끝나는 것만 꺼냄 → 비용 = 완료 개수
        f = scheduler.Pop()
        f.Output.Add(f.Job.Result); events.Publish(ActionCompleted(Smelt, xpRule.RecipientOf(f), f.Job.Result))
        f.Job = None
        TryStartJob(f, f.PrevEndAt)                 // 끝난 시각 기준으로 이어서 시작 → 틱 단위 오차가 쌓이지 않음

// now = 서버 플레이 시간(월드가 열려 있는 동안만 증가, 저장됨. 밤 건너뛰기와 무관 — C-65). 저장 시 진행 경과 = now - startAt를 FURN 청크에 기록(NFR-022)
// 칸 조작(FurnacePut/Take)은 보관함과 같은 규칙: 호스트가 도착 순서대로 처리, 복제·소실 없음(FR-134 규칙 재사용)
// 클라이언트 진행 막대 = (서버 시각 추정 - startAt) / (endAt - startAt). 서버 시각 추정은 FishNet TimeManager 틱 기준
```

# 코드 커버리지 기준선 (IMP-016)

- 측정일: 2026-11-10, 사이클 1 Act
- 도구: `com.unity.testtools.codecoverage` 1.3.0 (Unity 6000.3.27f2 열린 에디터)
- 대상: Game.* 제품 어셈블리 13개 (Editor, Tests 제외). 실제 결과에 잡힌 것은 12개이고 Game.Adapters.Persistence는 코드가 `PersistenceAssembly.cs` 마커뿐이라 빠졌다.
- 시험: EditMode 698건 (커버리지 측정 중 1건 실패, 아래 한계 참고. 측정하지 않은 일반 실행은 698/698 통과)

## 1. 결과 (줄 커버리지)

전체: 줄 62.1% (7,573 / 12,188), 메서드 72.5% (1,584 / 2,182). 분기는 측정 불가(0 / 0).

| 어셈블리 | 덮은 줄 | 측정 대상 줄 | 줄 비율 |
|---|---:|---:|---:|
| Game.Domain | 537 | 568 | 94.5% |
| Game.Application | 4,668 | 4,985 | 93.6% |
| Game.WorldGen | 587 | 686 | 85.5% |
| Game.Infra.Config | 54 | 54 | 100% |
| Game.Infra.Platform | 223 | 312 | 71.4% |
| Game.Adapters.Net | 478 | 899 | 53.1% |
| Game.Infra.AiJobs | 299 | 634 | 47.1% |
| Game.Adapters.Presentation | 718 | 2,010 | 35.7% |
| Game.Bootstrap | 9 | 1,623 | 0.5% |
| Game.Infra.Rendering | 0 | 132 | 0% |
| Game.Infra.Steam | 0 | 235 | 0% |
| Game.Infra.Transport | 0 | 50 | 0% |

해석: 순수 로직 계층(Domain, Application, WorldGen)은 85% 이상이다. 0%에 가까운 곳은 MonoBehaviour·FishNet·Steamworks·렌더링 연결 코드로, EditMode로는 닿지 않고 PlayMode·수동 시험(MP-01~08)이 담당한다. Bootstrap의 0.5%가 전체 수치를 가장 크게 낮춘다(1,623줄).

## 2. 한계

- 분기 비율: Unity 코드 커버리지는 시퀀스 포인트(줄) 기준 OpenCover 형식이라 분기를 내지 못한다. 분기 기준선은 만들 수 없고, 줄·메서드 비율만 쓴다.
- 측정 중 1건 실패: 계측으로 느려진 실행에서 1건이 실패했다(시험 이름은 결과 출력이 잘려 확인하지 못함, 계측을 끈 뒤 즉시 재실행은 698/698 통과). **회귀 시험(2026-11-10)에서 같은 방식으로 EditMode 704건을 두 번 다시 돌렸으나 모두 704/704 통과해 실패가 재현되지 않았고 이름은 끝내 확인하지 못했다.** 다만 이 재실행은 도메인 재로드 뒤 `Coverage.enabled`가 False로 돌아가고 `StopRecording()`이 NullReference로 실패해 실제 계측이 켜졌는지 확인되지 않았다(결과 파일 미갱신). 따라서 「재현 안 됨」은 계측이 확실히 켜진 상태의 결과가 아니다. 다음 측정 때는 결과 출력을 파일로 저장하고 `Failed` 항목을 찾는다. 줄 커버리지 수치는 갱신하지 못해 위 62.1%(시험 698건 기준)가 그대로 기준선이다. 설정은 원래대로 되돌렸다(`Coverage.enabled` False, 코드 최적화 Release).
- PlayMode 커버리지는 이번에 측정하지 않았다.
- 계측 실행은 EditMode 전체가 일반 실행보다 크게 느리다(약 5분 이상).

## 3. 재측정 절차 (열린 에디터, batchmode 없음)

1. `ProjectSettings/Packages/com.unity.testtools.codecoverage/Settings.json`: IncludeAssemblies에 Game.* 제품 어셈블리, Path는 `Logs/Coverage`, GenerateAdditionalReports 켬(Summary.md·Cobertura·lcov 생성). 이번 측정에서 이미 저장했다.
2. 에디터에서 코드 최적화를 Debug로 바꾸고(`CompilationPipeline.codeOptimization`), `UnityEngine.TestTools.Coverage.enabled = true`를 켠 뒤 도메인 리로드를 거친다.
3. `CodeCoverage.StartRecording()` 호출, EditMode 시험 실행, `CodeCoverage.StopRecording()` 호출. (Unity CLI의 test run 만으로는 자동 기록이 시작되지 않아 StartRecording/StopRecording API로 감쌌다.)
4. 결과: `src/Logs/Coverage/Report/Summary.md`, `Summary.json`, `Cobertura.xml`, `lcov.info` (Logs는 git 제외).
5. 끝나면 Coverage.enabled를 끄고 코드 최적화를 Release로 되돌린다(켜 두면 에디터가 느려진다). 현재 에디터는 되돌려 둔 상태다.

수동 대안: 메뉴 Window > Analysis > Code Coverage에서 Enable Code Coverage 체크, Included Assemblies 지정, Start Recording, Test Runner 실행, Stop Recording.

## 4. EditMode CI(GameCI) 도입 검토

| 항목 | 검토 |
|---|---|
| 라이선스 | GameCI는 Unity 라이선스를 Secrets로 활성화해야 한다. Personal은 수동 활성화 파일(.alf)로 .ulf를 얻어 `UNITY_LICENSE`에 넣는 방식이 가능하나 정기 갱신이 번거롭고 약관상 회사 규모 조건을 따라야 한다. Pro/Plus는 `UNITY_EMAIL`·`UNITY_PASSWORD`·`UNITY_SERIAL`로 안정적이다. 사용자의 라이선스 종류 확인 필요 |
| 비공개 저장소 분 사용량 | GitHub Free 비공개 저장소는 월 2,000분이다(Linux 기준). Unity 이미지 내려받기와 Library 첫 가져오기에 10~20분, EditMode 698건은 캐시 후 수 분이다. PR마다 돌려도 월 수십 회까지는 한도 안이다. Windows·macOS 러너는 분 배율이 2배·10배라 Linux 러너를 쓴다 |
| FishNet | `Assets/ThirdParty/FishNet`에 소스째 들어 있어 별도 설치 없이 임포트된다. 첫 임포트 시 CodeGen 어셈블리가 돌므로 캐시가 없으면 오래 걸린다. 문제 없음 |
| Steamworks.NET | 패키지가 git URL(`github.com/rlabrecque/Steamworks.NET`)이라 CI에서 git 접근이 필요하다(공개 저장소라 인증 불필요). 컴파일은 Steam 클라이언트가 없어도 되며 EditMode 시험은 Steam 호출을 가짜로 분리해 두었다. 실제 SteamAPI 초기화를 하는 시험은 CI에서 제외해야 한다 |
| 그 밖의 | Git LFS 체크아웃(`lfs: true`)과 Library 캐시 필요. 글꼴 .asset 2건이 작업 트리에서 수정 상태인 점은 CI와 무관 |

권고: Linux 러너의 GameCI EditMode 시험 워크플로는 기술적으로 가능하고 비용 부담이 작으므로 도입을 권고하되, 먼저 사용자의 Unity 라이선스 종류와 GitHub Secrets 등록 가능 여부를 정한다. 워크플로 파일은 만들지 않았다.

## 5. 성능 시험 패키지 (`com.unity.test-framework.performance`)

- 현재 성능 점검은 `docs/test/perf/`의 수동 측정과 시험 안의 관찰 로그(ms 출력)뿐이라 기준값 대비 회귀를 자동 판정하지 못한다.
- 패키지는 Measure.Method·Frames로 중앙값, 표준편차를 기록해 임계 초과를 실패로 만들 수 있어, NFR 프레임·틱 예산 회귀 방지에 필요하다.
- 에디터 측정은 기기에 따라 값이 흔들리므로 기준선은 같은 PC에서만 비교한다. 설치는 아직 하지 않았고 사용자 결정이 필요하다.

## 변경 이력
| 버전 | 날짜 | 내용 |
|---|---|---|
| 1.0 | 2026-11-10 | IMP-016 처리, 기준선 작성 (devops-engineer) |

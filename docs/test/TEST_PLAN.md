# 시험 계획서 (ISO/IEC/IEEE 29119-3) — 사이클 1 (M1 기술 검증)

- 문서 버전: 1.0 / 작성: test-engineer (Check 모드) / 날짜: 2026-11-10
- 대상: `cycle/1-mvp` @ `a5db2df` (시험 코드 8건 추가는 아직 커밋 전), Unity 6000.3.27f2, 대상 플랫폼 Windows PC(IL2CPP)
- 참조: SRS v0.12 2.6.1 M1 행, SDD v0.14, STORYBOARD v1.8, `docs/check/REVIEW_REPORT.md`, `docs/check/SECURITY_REPORT.md`, `docs/test/perf/M1_tech_verification.md`

## 변경 이력
| 버전 | 날짜 | 내용 |
|---|---|---|
| 1.0 | 2026-11-10 | 사이클 1 Check 첫 작성 |

## 1. 시험 대상과 범위
- 포함: SRS 2.6.1 M1 배분 FR 17개(FR-001~011, 023~025, 120, 121, 163)와 NFR(NFR-001, 004~018, 021, 027~031, 038~041, 048, 049, 053, 058). 대응은 TEST_SPEC 2장.
- 제외와 이유
  - M2 이후 기능(생존 수치, 인벤토리, 저장, 건축 등): M1 구현 범위 밖.
  - 세이브 변조·Steam Cloud(NFR-007 실제 동작): 저장은 M3 이후(SECURITY_REPORT 1장과 같음).
  - NFR-010·011·012의 정식 판정: SRS 3.3과 SDD에 따라 M6-10(실제 PC 8대). 이번에는 참고 측정만.
  - 접근성(색약·자막), 터치 입력: M1에 해당 FR 없음, 대상 플랫폼 PC.
- 코드 커버리지: 측정하지 않음. `com.unity.testtools.codecoverage`가 manifest에 없고, 에디터 batchmode 측정이 금지라 이번 사이클은 **측정 불가**. IMP-016으로 제안.

## 2. 위험 기반 우선순위
| 순위 | 위험 | 근거 | 시험 |
|---|---|---|---|
| 1 | 호스트 권위 우회(이동 검사, 비멤버 연결) | SEC-001·002, REV-001 | TC-038, TC-050~052, MP-09 |
| 2 | 참가 흐름(버전·인원·2PC·릴레이) | FR-001~005 필수, REV-002 | TC-003~005, TC-054, MP-01~03 |
| 3 | 8인 동기화 성능·대역폭 | SDD R-41~R-45, NFR-011·013·014 | TC-025~033 (측정 보고서) |
| 4 | 가용성(폭주·잘못된 패킷) | SEC-003·004, REV-004·007 | TC-042, TC-043, TC-053 |
| 5 | 기반 규칙(의존 방향, 결정론, 버전 규칙) | NFR-027~031, 053, 058 | TC-035~037, TC-046~047 |

## 3. 시험 레벨과 방식 (Unity)
| 29119 레벨 | 방식 | 대상 | 실행 |
|---|---|---|---|
| 단위 | EditMode (`Game.Tests.Domain/Application/WorldGen`) | 도메인 규칙, 명령 파이프라인, 이동 검사, 프로토콜 | 열린 에디터, Unity CLI |
| 통합 | EditMode (`Game.Tests.Net/Infrastructure`) + PlayMode `Game.Tests.Integration` | 게이트웨이, 복제, 전송 선택, 설정·UI 프리팹, 호스트↔클라이언트 루프백 | 위와 같음 |
| 시스템 | PlayMode (`[UnityTest]`) | 루프백 접속·중도 참가·5,000개 동기화, 화면 계층, 벤치 씬 | `--async_tests true` 후 상태 확인 |
| 인수 | 수동 플레이 (빌드 2개, 2PC) | 2PC 참가, Steam 릴레이, 출시↔개발 접속 거부, 패드, 메모리, 화면 표시 | TEST_SPEC 4장 절차, 사용자 수행 |

실행 순서: EditMode → PlayMode → 수동. 결함 재현 시험(`[Category("KnownDefect")]`)은 실패로 남겨 두고, 이를 이유로 PlayMode를 막지 않는다(제품 결함이지 환경 문제가 아님).

## 4. 시험 유형과 품질 특성
기능(FR별 TC), 성능(M1 측정 보고서, PlayMode `BenchScenePlayTests`), 호환성(Windows IL2CPP 출시·개발 빌드, 출시↔개발 접속 거부), 사용성(수동 MP-07), 신뢰성(잘못된 패킷, 중도 참가, 나가기), 보안(적대적 클라이언트 TC-050~056), 유지보수성(아키텍처 시험 35건), 이식성(빌드 산출물 검사).

## 5. 시험 환경
| 항목 | 값 |
|---|---|
| Unity / 에디터 | 6000.3.27f2, 열린 에디터(pid 31620), Unity CLI(`unity command test run ...`) |
| 패키지 | `com.unity.test-framework` 1.6.0 (이번에 추가한 패키지 없음). `com.unity.test-framework.performance`, `com.unity.testtools.codecoverage`는 없음 |
| PC | i7-12700K, RAM 32 GB, RTX 3070 Ti (M1 측정 보고서 1장) |
| 빌드 | 출시 IL2CPP `src/Builds/Release/`(버전에 `.dirty`), 개발 `src/Builds/Development/` |
| 시험 어셈블리 | `Game.Tests.Domain·Application·WorldGen·Net·Infrastructure·Architecture`(EditMode), `Game.Tests.Integration`(PlayMode). 새 asmdef 없음 |
| 에디터 focus | PlayMode 실행 직전 1회 |

## 6. 진입 기준 / 종료 기준
| 구분 | 기준 | 충족 |
|---|---|---|
| 진입 | 컴파일 오류 0, 에디터 연결(`unity status` ready), 기존 시험 663/663·26/26 | 충족 |
| 종료 1 | 필수 FR 시험 100% 실행 | **미충족**: 수동 차단 6건(2PC, 릴레이 등) |
| 종료 2 | 치명 결함 0건 | 충족 (치명 0) |
| 종료 3 | 높음 결함 0건 또는 처리 계획 | 높음 2건(DEF-001, DEF-004) → IMP로 계획 |
| 종료 4 | 성능 기준 충족(참고값) | 충족 (M1 측정 보고서) |
| 종료 5 | 코드 커버리지 목표 | 측정 불가 |
| 판정 | **조건부** (Act에서 DEF 해소, 수동 시험 수행 후 재판정) | |

## 7. 환경 준비 상태
시험 어셈블리 7개 준비 완료. 새 시험 파일 3개 추가(`Application/AdversarialClientTests.cs`, `Application/PvpAbsenceTests.cs`, `Architecture/ReleaseHygieneTests.cs`). 결과 원본: `docs/test/results/`.

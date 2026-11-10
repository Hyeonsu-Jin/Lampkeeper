# 개선 사항 보고서 (ISO/IEC 25010) — 사이클 1 (M1 기술 검증)

## 변경 이력
| 버전 | 날짜 | 내용 |
|---|---|---|
| 1.0 | 2026-11-10 | 사이클 1 Check 첫 작성 (test-engineer) |
| 1.1 | 2026-11-10 | 회귀 시험(test-engineer): 처리 12건 판정(검증 완료 11, 재작업 필요 1=IMP-013), EditMode 705/705·PlayMode 26/26, Do 결함 소급 DEF-006~011과 신규 DEF-012 등록, 보안성·기능 적합성·유지보수성 등급 재평가, 시험 대응표 SRS v0.13 기준 갱신 |
| 1.2 | 2026-11-11 | 회귀 재확인(test-engineer): IMP-013 검증 완료(SRS 머리말 0.13 확인), DEF-012 해결됨, 처리율 12/12, 미해결 결함 0건 |
| (총괄) | 2026-11-10 | 사용자 Act 대상 선택 반영: 승인 IMP-001~006·011~013·016~018(12건), 보류 IMP-007~010·015(다음 사이클), IMP-014는 사용자 수동 시험 대기 |

## 1. 개요
| 항목 | 내용 |
|---|---|
| 사이클 | 1 (Check), 구현 범위 M1-01~M1-13 |
| 대상 | `cycle/1-mvp` @ `a5db2df` (+ 미커밋 시험 코드 8건) |
| Unity / 플랫폼 | 6000.3.27f2 / Windows PC (IL2CPP) |
| 참조 | SRS v0.12, SDD v0.14, STORYBOARD v1.8, `docs/check/REVIEW_REPORT.md`, `docs/check/SECURITY_REPORT.md`, `docs/test/TEST_*.md`, `docs/test/perf/M1_tech_verification.md` |
| 평가 기준 | ISO/IEC 25010 8개 품질 특성. 등급: 우수 / 양호 / 보통 / 미흡 |

## 2. 종합 요약
**전체 판정(회귀 시험 뒤): 조건부(수동 시험만 남음).** 치명 결함 0건, 미해결 결함 0건(DEF-012 SRS 머리말 버전, 낮음, 2026-11-11 해결). Check 때 높음 결함 2건(DEF-001·004)을 포함한 5건과 Do 때 고친 6건(소급)은 모두 해결됨. 자동 시험 EditMode 705/705, PlayMode 26/26. 수동 시험 차단 6건(MP-01~09 대기)이 남아 필수 FR 시험 100% 실행 기준은 아직 미충족. 성능과 구조는 기준을 크게 밑돌아 양호하다. (아래 등급 표의 기능 적합성·보안성은 회귀 결과로 갱신함)

| 품질 특성 | 등급 | 한 줄 근거 |
|---|---|---|
| 기능 적합성 | 양호 (회귀 뒤, Check 때 보통) | M1 FR 17개 시험 대응 100%, FR-003 수용 기준 해소(TC-054 통과), 자동 시험 전부 통과. FR-002·005 2PC 미확인(MP-01·02 대기)
| 성능 효율성 | 양호 | P8 서버 틱 평균 0.06 ms, 호스트 1.03 Mbps, 참가자 175 kbps, 중도 참가 0.74 s 모두 기준 이내. 출시 빌드 메모리·참가자 FPS는 측정 대기 |
| 호환성 | 보통 | Windows IL2CPP 빌드 성공. 해상도·화면비, Steam 릴레이, 출시↔개발 실제 접속 미확인 |
| 사용성 | 보통 | 키보드 조작·메뉴 사용자 확인됨. 버전 불일치 오류 문구에 정보 부족, HUD·설정 화면 수동 대기 |
| 신뢰성 | 양호 | 잘못된 패킷·예외 격리·중도 참가·나가기 시험 통과. 동기화 실패 처리(REV-009), 호스트 끊김 실제 미확인 |
| 보안성 | 양호 (회귀 뒤, Check 때 보통·무결성 미흡) | NFR-030·041 회귀 통과(TC-050~053, 067), Player.log 끔(TC-056), 로그 상한. 비멤버 연결 차단은 방어 구현·자동 범위만 확인, 실측 MP-09 대기 |
| 유지보수성 | 양호 | asmdef 규칙 자동 검사, 머리 주석 163/163, 자동 시험 731건(EditMode 705 + PlayMode 26), 코드 커버리지 기준선 줄 62.1%(Domain 94.5%, Application 93.6%, Bootstrap 0.5%). GameFlow 721줄·IMP-009 보류
| 이식성 | 보통 | Windows 대상 빌드 가능. 출시 빌드 `.dirty`·약 1.8 GB, 배포 금지 폴더 분리 필요, 대상 외 플랫폼 해당 없음 |

### 먼저 처리할 개선 사항 Top 5
1. IMP-001 이동 검사 보강 (DEF-001·002, 높음)
2. IMP-002 버전 불일치 때 두 버전 표시 (DEF-004, 높음)
3. IMP-003 비멤버 연결 차단 (SEC-002, 높음, 실측 먼저)
4. IMP-004 자세·잘못된 패킷 빈도 제한과 로그 폭주 방지 (SEC-003, 높음)
5. IMP-014 수동 시험(2PC·릴레이·출시 메모리) 수행과 결과 반영 (필수 FR 미확인)

## 3. 테스트 결과 요약
| 방식 | 전체 | 통과 | 실패 | 차단 |
|---|---|---|---|---|
| EditMode | 705 (회귀 뒤, Check 때 671) | 705 (Check 때 665) | 0 (Check 때 6, 결함 재현 시험이 전부 해결) | 0 |
| PlayMode | 26 | 26 | 0 | 0 |
| 수동 | 사전 확인 8건 통과 | 8 | 0 | 절차 9개 대기 |

TC 68개(회귀 뒤): 통과 53, 실패 0, 부분 8, 차단 6, 대기 1. (Check 때 TC 66개: 통과 44, 실패 9, 부분 7, 차단 6.) 코드 커버리지는 기준선 줄 62.1%(`COVERAGE_BASELINE.md`), 회귀 재측정은 계측이 켜지지 않아 갱신하지 못함. 상세는 `docs/test/TEST_COMPLETION_REPORT.md`.

## 4. 결함 목록
| DEF | 제목 | 심각도 | 관련 |
|---|---|---|---|
| DEF-001 | 이동 검사 우회(2배 속도·부풀린 시각·무제한 자세) | 높음 | TC-050·051, NFR-030, REV-001, SEC-001 |
| DEF-002 | 첫 자세로 월드 어디든 순간 이동 | 보통 | TC-052, REV-003 |
| DEF-003 | 공유 대기열 고갈로 다른 참가자 명령 버려짐 | 보통 | TC-053, NFR-041, REV-004, SEC-004 |
| DEF-004 | 버전 불일치 때 두 버전 미표시 | 높음 | TC-054, FR-003, REV-002 |
| DEF-005 | Player.log에 SteamID·경로 원문 | 보통 | TC-056, SEC-005 |
| DEF-006 | 이동·핑 좌표 검사가 월드 중앙 스폰 이동을 거부(소급) | 높음 | NFR-030, 커밋 ae66631 |
| DEF-007 | 플레이어 빌드에서 지형이 안 그려짐(소급) | 높음 | DES-089, 커밋 719653c·cc97e52 |
| DEF-008 | 생물 위치 압축 범위가 월드 절반만 덮음(소급) | 높음 | DES-080·082, 커밋 13ebf31 |
| DEF-009 | 화면 없는 실행에서 안전 영역 갱신 무한 반복 크래시(소급) | 높음 | NFR-021, 커밋 c97e571 |
| DEF-010 | 활동 생물 수가 측정 CSV에 빠짐(소급) | 낮음 | NFR-011·021, 커밋 b31a7be |
| DEF-011 | 측정 스크립트가 MCP unity.exe를 에디터로 셈(소급) | 낮음 | NFR-021, 커밋 4b739ca |
| DEF-012 | SRS 머리말 문서 버전이 0.12로 남아 변경 이력 0.13과 어긋남 | 낮음 | IMP-013, SRS 머리말 (해결됨, 2026-11-11 회귀 재확인, Act 회귀 결과 커밋) |

상태: DEF-001~011 **해결됨**(001~005는 회귀 시험으로 확인, 006~011은 소급 등록으로 해결 커밋 확인), DEF-012 **해결됨**(2026-11-11 재확인). 결함 합계 12건(높음 6, 보통 3, 낮음 3, 치명 0). 소급 DEF의 상세는 `docs/test/TEST_LOG.md` 4.1.

## 5. 품질 특성별 분석

### 5.1 기능 적합성 — 양호 (회귀 뒤 상향, Check 때 보통)
| 부특성 | 평가 | 근거 |
|---|---|---|
| 기능 완전성 | 보통 | M1 FR 17개 시험 대응 100%. 2PC 참가·릴레이(FR-002·005)와 설정 화면(FR-121) 미확인. 마일스톤이 여러 개인 FR의 수용 기준을 M1 시점에 전부 판정할 수 없음(REV 3장) |
| 기능 정확성 | 양호 | 회귀 시험: FR-003 두 버전·「?」 표시(TC-054)와 NFR-030 2배 속도 되돌림(TC-050)이 통과. 자동 시험 731건 전부 통과 |
| 기능 적절성 | 양호 | 호스트 권위, 중도 참가, 자원·생물 복제가 M1 목적(기술 검증)에 맞게 동작 |

문제(Check): SRS 변경(FR-003 v0.11)이 STORYBOARD·코드로 전파되지 않음 → IMP-002 검증 완료, IMP-013은 SRS 머리말 버전 한 줄(DEF-012)을 고쳐 2026-11-11 재확인으로 검증 완료.

### 5.2 성능 효율성 — 양호
| 부특성 | 평가 | 근거 |
|---|---|---|
| 시간 반응성 | 양호 | 서버 틱 평균 0.060 ms, p99 0.377 ms, 최대 5.57 ms(상한 6.0), 메인 2.20 ms, GPU 5.0 ms, 중도 참가 0.744 s. FPS 60은 vSync 상한이라 여유 판단 불가 |
| 자원 효율성 | 양호 | 메모리 1,366 MB / VRAM 1,418 MB(개발 빌드, 기준 10 GB / 7.5 GB). GC 521.6 KB/s(P8), 매 프레임 할당 지점 확인(REV-005). 출시 빌드 값은 측정 대기 |
| 용량 | 양호 | 8인, 60체, 자원 5,000개 변경에서 기준 이내. 수백 체·M4 규모는 미측정 |

한계: 같은 PC 직접 전송, 임시 도형 그래픽, 참가자 FPS 미측정(보고서 4장). 정식 판정 M6. 문제: IMP-007, IMP-018.

### 5.3 호환성 — 보통
| 부특성 | 평가 | 근거 |
|---|---|---|
| 공존성 | 양호 | 호스트 + 봇 7개가 한 PC에서 문제 없음 |
| 상호운용성 | 보통 | Steamworks.NET·FishNet·FishySteamworks 연동은 오버레이 초대 창까지만 확인. 릴레이·출시↔개발 실제 접속 거부 미확인(MP-02·03) |

해상도·화면비(21:9 등) UI 확인은 측정 대기(MP-07). 문제: IMP-014.

### 5.4 사용성 — 보통
| 부특성 | 평가 | 근거 |
|---|---|---|
| 적절성 인식성 | 양호 | 로비·메뉴 흐름 사용자 확인(TC-059·061) |
| 학습성·운용성 | 양호 | 키보드 Esc 메뉴·방향키 포커스, 이동·달리기·스태미나 사용자 확인 |
| 사용자 오류 방지·오류 대처 | 보통 | 버전 불일치 문구에 두 버전·조치 대상이 없음(DEF-004). 거부 화면 자체는 있음 |
| UI 심미성 | 측정 대기 | 임시 그래픽·아이콘 자리 글자 라벨(결정 기록). 수동 화면 확인 대기 |
| 접근성 | 해당 없음 | M1 범위에 접근성 FR 없음 |

### 5.5 신뢰성 — 양호
| 부특성 | 평가 | 근거 |
|---|---|---|
| 성숙성 | 양호 | 자동 시험 697건 통과, 잘못된 패킷 후에도 서버 정상(TC-042) |
| 가용성 | 보통 | 참가자 한 명이 대기열·로그로 다른 참가자를 방해 가능(DEF-003, SEC-003) |
| 결함 허용성 | 양호 | 게이트웨이·디스패처·틱 구독자 예외 격리. 지형 뷰 실패 시 게임에 진입하는 REV-010 |
| 회복성 | 보통 | 스냅샷 전송 실패·델타 적용 실패를 로그만 남기고 진행(REV-009). 호스트 끊김 실제 확인 대기(MP-08) |

세이브 도중 종료·변조 시험은 저장이 M3 이후라 해당 없음.

### 5.6 보안성 — 양호 (회귀 뒤 상향, Check 때 보통·무결성 미흡)
| 부특성 | 평가 | 근거 |
|---|---|---|
| 기밀성 | 보통 | 저장소·빌드에 비밀 값 없음, 파일 로그는 가려짐, Player.log 끔(TC-056 통과). 비멤버 연결 차단은 인증기 구현·자동 범위 확인, 실제 연결 실측은 MP-09 대기 |
| 무결성 | 양호 (Check 때 미흡) | 회귀: 2배 속도 거부(TC-050), 시각 부풀리기 거부(TC-051), 먼 첫 자세 거부(TC-052)가 통과. 지형 높이 Y 검사는 미반영(다음 사이클 후보). 패킷 디코드·타격 검증은 견고 |
| 부인 방지·책임 추적성 | 양호 | 거부·허가 로그 있음, 로그 20 MB 상한·10개 보관·5초 묶음 시험 통과(SEC-003 해소) |
| 인증성 | 보통 | SteamID + 로비 멤버(D-14)는 양호, FishNet 계층 인증 없음(SEC-002), 친구 관계 재확인 없음(SEC-007) |

### 5.7 유지보수성 — 양호
| 부특성 | 평가 | 근거 |
|---|---|---|
| 모듈성 | 양호 | asmdef 14개가 SDD 표와 일치, 도메인·애플리케이션은 엔진 비참조. 예외: 표현 어댑터가 애플리케이션 상태를 변경(REV-006) |
| 재사용성 | 양호 | 전송 계층 교체 가능(TC-039) |
| 분석성 | 양호 | 머리 주석·ID 연결 163/163. GameFlow 721줄(REV-008) |
| 수정 용이성 | 보통 | 정적 전역(`AvatarServices.Current`, REV-015), 구독 미해제(REV-012) |
| 시험성 | 양호 | 자동 731건(EditMode 705 + PlayMode 26), 가짜 구현 분리. 코드 커버리지 기준선 줄 62.1%(IMP-016, 검증 완료), 결함 동작을 고정하던 시험은 갱신됨. 공백: 이동 거부 비종료 규칙·FishNet 인증기 동작은 자동 시험 없음, 커버리지 재측정 절차 불안정 |

### 5.8 이식성 — 보통
| 부특성 | 평가 | 근거 |
|---|---|---|
| 적응성 | 보통 | Windows 대상. 해상도·화면비 확인 대기 |
| 설치 용이성 | 보통 | 출시 빌드 약 1.8 GB, `.dirty` 트리에서 빌드(재현 불가), `*_DoNotShip`·`*_ButDontShipItWithYourGame` 폴더 분리 필요(SEC-009). `.meta`만 남은 항목 3개(REV-014, 로컬에서는 재현 안 됨) |
| 대체성 | 양호 | 개발 전용 코드는 상수로 막혀 있음(SEC-008) |

## 6. 검토·보안 결과 요약
- REVIEW_REPORT: 🔴 2(REV-001·002) · 🟡 6 · 🟢 7. IEEE 1012: 추적성·완전성 부분 적합. CMMI 성숙도 수준 2 추정(형상 관리·VV·PQA 부분 충족), SPICE 통합·테스트·품질 보증 능력 수준 1. 프로세스 권고 11개 중 TC ID·대응표(권고 4)는 이번 시험 문서로 해소.
- SECURITY_REPORT: 치명 0 / 높음 3(SEC-001·002·003) / 중간 2(SEC-004·005) / 낮음 5. 출시 빌드 개발 기능 제외는 적합(조건부). 이 중 SEC-001·004·005(REV-001·003·004)를 시험으로 재현했다(DEF-001~003, 005). SEC-002·003은 재현 못 함(실측·부하 시험 필요).

## 7. 개선 사항 목록
크기: XS(반나절 이하) / S(1일) / M(2~3일) / L(그 이상). 우선순위 P1(먼저) ~ P3.

| IMP ID | 품질 특성 | 내용 | 근거(DEF/REV/SEC/TC) | 심각도 | 우선순위 | 권장 조치 | 영향 범위 | 크기 | 상태 | 조치 내역 |
|---|---|---|---|---|---|---|---|---|---|---|
| IMP-001 | 보안성(무결성) | 이동 검사 보강: 호스트 수신 시각 기준 dt, 자세마다가 아닌 거리 예산, 자세 빈도 상한, 첫 자세는 스폰 근처만, 수직·높이 차 검사 | DEF-001, DEF-002, REV-001, REV-003, SEC-001, SEC-006, TC-050·051·052 | 높음 | P1 | `AvatarMoveValidator`·`PlayerAvatar` 수정, SDD 3.11.12·NFR-030 보강, `FirstPose_IsAcceptedAsBaseline` 갱신, KnownDefect 시험이 통과해야 함 | Application/Server, Adapters/Net/Avatar, SDD, 시험 | M | 검증 완료 | 조치: AvatarMoveValidator를 호스트 수신 시각 기준 누적 예산·자세 빈도 버킷(45 Hz)·스폰 근처 첫 자세 검사로 변경, ServerWorld가 스폰 지점·호스트 시계 전달 (ServerWorld.cs, 시험 갱신) **확인(회귀 2026-11-10)**: TC-050·051·052·038 통과, `PoseRateGate`·첫 자세 근접/원거리 시험 통과 |
| IMP-002 | 기능 적합성·사용성 | `JoinRejected(VersionMismatch)`에 호스트 버전 실어 두 버전 표시 | DEF-004, REV-002, TC-054, FR-003 | 높음 | P1 | 프로토콜 필드 추가(64바이트 상한), 문구·STORYBOARD SCR-012·SDD 3.7.2 갱신, 왕복·표시 시험, 프로토콜 버전 확인 | Application/Protocol, 문구 표, UI, SDD·STORYBOARD | M | 검증 완료 | (2026-11-10) JoinRejected에 hostVersion(문자열, 64B 상한) 추가, 프로토콜 2→3, 옛 호스트 형식은 호스트 버전 빈 값으로 허용. 호스트 JoinHandshake.Reject가 버전 불일치 때 호스트 버전 전송, ClientSession.RejectHostVersion 보관, GameFlow→DevLobbyScreen.ShowFailure(prefix, 내 버전, 호스트 버전), strings ko/en cause에 {0}·{1}. TC-054 통과(KnownDefect 제거), 왕복·상한·세션 시험 추가 **확인(회귀 2026-11-10)**: TC-054, ProtocolCodecTests.JoinRejected_*(3) 통과. 2PC 실제 표시는 IMP-014(MP-03) |
| IMP-003 | 보안성(인증성·기밀성) | 비멤버 Steam 연결이 참가 판정 전에 스폰·위치·SteamID 수신 | SEC-002 | 높음 | P1 | 먼저 실측(MP-09, 개발용 연결만 열기 옵션 필요), 확인되면 FishNet Authenticator 연동 또는 ObserverCondition, `ShareIds` 끄기 | Adapters/Net, Boot 씬 NetworkManager | M | 검증 완료(자동 범위) | 조치(방어 구현, 실측 MP-09 대기): FishNetJoinAuthenticator 추가(참가 판정 통과 후 INetServer.Admit으로만 인증), Boot 씬 NetworkManager에 인증기·ServerManager 추가 및 ShareIds 끔, 개발용 DevOpenConnections는 에디터·개발 빌드에서만 적용 **확인(회귀 2026-11-10)**: Net.JoinAndGatewayTests(비멤버 거부, 참가 요청 없음·세계 준비 시간 초과) 통과. 실제 FishNet 인증기로 연결 후 스폰 0건은 EditMode로 닿지 않아 MP-09(IMP-014)에서 확인 |
| IMP-004 | 보안성(가용성) | 자세 RPC·잘못된 패킷 빈도 제한, 실패 누적 시 연결 종료, 반복 로그 묶기·스택 추적 끄기, FileLogSink 크기 상한·보관 정리 | SEC-003, REV-007, NFR-040·041·045 | 높음 | P1 | 연결별 토큰 버킷(`AvatarSendHz × 1.5`), `_decodeFailures` 활용, 로그 요약, 부하 시험 추가(시험 먼저) | Adapters/Net, RuntimeSupport, FileLogSink | M | 검증 완료 | 조치: PoseRateGate(연결별 AvatarSendHz x 1.5)·AbuseMeter(10초 50회 초과 시 연결 종료)를 PlayerAvatar·FishNetServer에 적용, ThrottledLog(5초 묶음)와 경고 스택 추적 끄기, FileLogSink 20MB 상한·10개 보관, 부하 시험 추가 **확인(회귀 2026-11-10)**: AbuseGuardTests(PoseRateGate 2, ThrottledLog 2, TC-067 51회 종료), FileLogSinkLimitTests(3) 통과. 이동 거부 51회 비종료 규칙은 자동 시험 공백(다음 사이클 PlayMode 후보) |
| IMP-005 | 보안성(가용성) | 명령 대기열을 플레이어별 몫 또는 대기열 진입 전 빈도 검사 | DEF-003, REV-004, SEC-004, TC-053 | 보통 | P2 | 게이트웨이에서 `CheckRate` 선행, 디코드 실패 회신도 같은 버킷, SDD 3.11.13과 순서 일치 | Net/Gateway, ServerWorld, CommandPipeline | S | 검증 완료 | 조치: ServerWorld 대기열 플레이어별 몫(상한/최대 인원), 디코드 실패·몫 초과 회신을 RequestGuard 전체 버킷(CheckRateAll)으로 제한 (CommandPipeline.cs, ServerWorld.cs, ServerCommandGateway.cs) **확인(회귀 2026-11-10)**: TC-053, TC-043, Flood_IsRateLimited_AndFloodRepliesAreNotAmplified 통과 |
| IMP-006 | 보안성(기밀성) | Player.log 끄기(출시), 또는 Debug 호출 전 Sanitize | DEF-005, SEC-005, TC-056 | 보통 | P2 | 출시 빌드 `usePlayerLog` 0(설정 한 줄, 개발 빌드 필요 여부 판단), FR-158 안내문에 Logs/ 폴더만 첨부 | ProjectSettings, 빌드 스크립트 | XS | 검증 완료 | (2026-11-10) ProjectConfigurator가 PlayerSettings.usePlayerLog=false 적용(ProjectSettings.asset usePlayerLog: 0). 설정이 빌드 종류별이 아니라 하나뿐이고 가려진 로그는 Logs/에 따로 남으므로 개발 빌드도 끔. TC-056 통과(KnownDefect 제거). FR-158 안내문(Logs/ 폴더만 첨부)은 SRS 반영 필요 **확인(회귀 2026-11-10)**: TC-056 통과, ProjectSettings `usePlayerLog: 0` 확인 |
| IMP-007 | 성능 효율성 | 매 프레임 할당 제거(상태 줄 문자열, HashSet, 박싱 열거, 자세마다 byte[]), 생물 스냅샷 배열 풀링 검토 | REV-005, REV-013, M1 보고서 3·5장 | 보통 | P3 | Profiler로 GC Alloc 0 B/프레임 확인, 개발 빌드에서만 상태 줄 | Bootstrap/GameFlow, Presentation/World, Net/Avatar | S | 보류(다음 사이클) | |
| IMP-008 | 유지보수성 | 표현 어댑터의 애플리케이션 상태 변경 제거, 구독 해제·`ReplicationPublisher.Dispose`, 정적 전역 서비스 정리 | REV-006, REV-012, REV-015 | 낮음 | P3 | 보간 트랙 삭제를 미러·수신기로 이동, 시험 TearDown 확인 | Presentation/World, SessionRuntime, ReplicationPublisher | S | 보류(다음 사이클) | |
| IMP-009 | 유지보수성 | GameFlow(721줄) 분리, `?.` 규칙 위반·지형 뷰 실패 처리 보강 | REV-008, REV-010, REV-011 | 보통 | P3 | M2 시작 전에 월드 뷰 준비·개발 도구·세션 수명 주기 분리 | Bootstrap/Runtime | M | 보류(다음 사이클) | |
| IMP-010 | 신뢰성 | 스냅샷 전송·델타 적용 실패 시 동기화 중단·재동기화, 실패 수 지표 | REV-009 | 낮음 | P3 | 조건 확인 후 연결 종료 또는 재동기화, 실패 시험 | Net/Replication | S | 보류(다음 사이클) | |
| IMP-011 | 프로세스(형상 관리) | `main` 직접 커밋 17개 사유 기록, DEF ID·`Closes:` 사용, 커밋 제목 50자(초과 15/91), 소급 DEF 등록 | REV 4장 CM, REV 7장 권고 1·6·7 | 보통 | P2 | 결정 기록에 사유 남김, 이번 DEF-001~005를 Act 커밋 `Closes:`에 사용, commit-msg 점검, 사이클 2부터 Plan을 사이클 브랜치에서 | 총괄·형상 관리 | S | 검증 완료(훅 활성화 사용자 결정 대기) | **확인(회귀 2026-11-10)**: 산출물 확인: `tools/git/commit-msg` 존재, 64자 제목 거부(rc=1)·정상 제목 통과(rc=0) 직접 실행 확인, Act 커밋 7건에 `Closes:` 있음, 결정 기록 2026-11-10, Do 결함 소급 DEF-006~011 등록(TEST_LOG 4.1). 이 PC `core.hooksPath` 비어 있어 훅이 아직 꺼져 있음(위험) |
| IMP-012 | 프로세스·이식성 | 출시 빌드는 깨끗한 커밋(태그)에서만, `.dirty`면 거부. 배포 폴더에서 `*_DoNotShip`·`*_ButDontShipItWithYourGame` 제외, 개발 빌드는 내부용 | REV 7장 권고 3, SEC-009 | 보통 | P2 | 빌드 스크립트에 거부 조건, 배포 설정 제외 목록, 빌드 용량 1.8 GB 검토 | Editor/BuildCommands, devops | S | 검증 완료(실빌드 확인은 다음 출시 빌드) | (2026-11-10) Editor/BuildPolicy 추가: 출시 빌드는 dirty·git 읽기 실패면 거부(BuildCommands가 호출), *_DoNotShip·*_ButDontShipItWithYourGame는 Builds/Symbols/로 이동, 개발 빌드 폴더에 INTERNAL_DEVELOPMENT_BUILD.txt. BuildPolicyTests 추가. 전체 빌드는 실행하지 않음(실빌드 확인은 다음 출시 빌드 때) **확인(회귀 2026-11-10)**: BuildPolicyTests 통과. 실제 출시 빌드 거부 동작은 이번에 실행하지 않음 |
| IMP-013 | 요구사항·문서 | SRS 2.6.1 M1 NFR 범위를 NFR-021과 맞춤(NFR-012·015는 M6 명시), 다중 마일스톤 FR의 「M1 판정 범위」 명시, NFR-030·041 보강(시각 기준·자세 상한), 변경 영향 표, STORYBOARD SCR-012 문구 | REV 3장, REV 7장 권고 2·9, REV-002 | 보통 | P2 | requirements-analyst·ui-ux-designer 갱신, 시험 대응표 갱신 | SRS, SDD 머리말(참조 SRS 버전), STORYBOARD | S | 검증 완료 | **확인(회귀 2026-11-10, 재확인 2026-11-11)**: SRS 변경 이력은 v0.13이고 SDD 머리말은 SRS v0.13 참조, STORYBOARD SCR-012 두 버전 표시(v1.9), SRS 2.6.1·「M1 판정 범위」·NFR-030·041·045·FR-003·158 반영 확인. 다만 SRS 머리말 「문서 버전」이 0.12로 남아 있어(DEF-012) 재작업으로 판정했으나, 2026-11-11 머리말 3행이 0.13으로 고쳐진 것을 확인해 검증 완료 |
| IMP-014 | 기능 적합성·호환성 | 수동 시험 MP-01~08 수행(2PC·릴레이·출시↔개발 거부·참가자 FPS·지연·출시 메모리·설정 화면·호스트 끊김) | TC-002·005·020·022·027·030, TC-016·018·041·044 | 높음 | P1 | 사용자 수행, 결과를 TEST_LOG 반영. TC-022(NFR-007)는 M3에서 | 시험 문서 | M | 사용자 수행 대기 | |
| IMP-015 | 이식성·형상 | 저장소 위생: `.gitignore`에 `/.claude/`, 빈 폴더·서드파티 `.meta` 정리, 개발 전용 코드(Bootstrap/DevTools) 분리, 에디터 실행 뒤 변경되는 Pretendard SDF 애셋 원인 확인 | REV-014, SEC-008, SEC-010, TC-055 | 낮음 | P3 | `.meta` 삭제 커밋, DevTools를 별도 asmdef로(`UNITY_EDITOR \|\| DEVELOPMENT_BUILD`), TMP 동적 애셋 정책 결정 | 저장소, Bootstrap | S | 보류(다음 사이클) | |
| IMP-016 | 유지보수성(시험성) | 코드 커버리지 패키지 설치·측정, EditMode CI(GameCI) 검토, 성능 시험 패키지 | TEST_PLAN 1장, REV 7장 권고 8 | 낮음 | P3 | `com.unity.testtools.codecoverage` 추가, 에디터 안에서 측정, 수준 4 목표 시 기준선 설정 | Packages, CI | M | 검증 완료 | 코드 커버리지 패키지 1.3.0 설치, 에디터 안에서 EditMode 측정(줄 62.1%, 분기 측정 불가), 기준선은 docs/test/COVERAGE_BASELINE.md. CI 검토와 성능 시험 패키지 필요성은 같은 문서에 기록, 도입은 사용자 결정 대기 **확인(회귀 2026-11-10)**: `com.unity.testtools.codecoverage` 1.3.0이 manifest에 있고 기준선 문서 있음(줄 62.1%, 메서드 72.5%). 계측 중 실패 1건은 회귀에서 재현되지 않아 이름 미확인(COVERAGE_BASELINE 2장, TEST_LOG 6.3). 재측정 절차는 불안정(Coverage.enabled가 재로드 뒤 꺼짐) |
| IMP-017 | 보안성 | 의존성 취약점 확인(FishNet 4.7.4, FishySteamworks 4.1.1, Steamworks.NET), 출시 전 `com.unity.pipeline` 제거 검토, 참가 호환성 확인이 변조 방지가 아님을 SDD에 명시 | SEC §6, SEC-007 | 낮음 | P3 | 공지·이슈 확인, manifest 정리 | Packages, SDD | XS | 검증 완료 | **확인(회귀 2026-11-10)**: SDD D-03 의존성 확인 결과(보안 공지 0건)와 3.4.3 Pipeline 출시 포함·제거 검토 시점, SRS 「버전 확인은 변조 방지가 아님」 확인 |
| IMP-018 | 성능 효율성(측정) | 측정 보고서 보완: 시드 기록, vSync 끈 FPS, 흐름장 전용 표식, 업스케일 끈 측정 | REV 3장 정밀성, M1 보고서 3·6장, R-41 | 낮음 | P3 | 측정 스크립트가 시드를 요약에 기록, 표식 추가, M2 전에 재측정 | Bootstrap/DevTools, 측정 문서 | S | 검증 완료 | 2026-11-10 developer: PerfOptions `-vsync on/off`, 시드·화면 로그 줄(GameFlow, DevDiagnostics), `Server.FlowRecompute` 표식(IPerfProbe.Record, AiJobsBackend 표식 잡, ServerWorld), RunM1Perf.ps1 `-VSyncOff`·`-UpscaleOff`·시드/주사율/재계산 요약·run_info.csv. EditMode 704, PlayMode 26 통과. 재측정은 사용자 실행 대기. 재측정 완료(2026-11-10, 보고서 8장) **확인(회귀 2026-11-10)**: `docs/test/perf/M1_tech_verification.md` 8장 재측정 3회: Server.Total 틱당 평균 0.116·0.135·0.121 ms(기준 3.0 ms), 판정 항목 모두 통과, 시드 기록·`Server.FlowRecompute` 표식·도구 옵션 확인. 참가자 FPS는 여전히 미측정 |

## 8. 다음 사이클 권고
1. Act에서 P1(IMP-001~004, 014)을 먼저 처리하고, DEF-001·004는 KnownDefect 시험이 통과하는 것으로 검증한다. 처리 뒤 `KnownDefect` 분류를 제거한다.
2. 보안 시험(적대적 클라이언트 묶음)을 사이클마다 유지한다. 새 명령(M2 이후) 추가 때마다 TC-010 형태의 구조 시험을 같이 갱신한다.
3. M2 시작 전에 IMP-009(GameFlow 분리), IMP-013(SRS 정합)을 끝내 새 UI·채팅 기능이 커진 조립 루트에 쌓이지 않게 한다.
4. 사이클 2는 Plan부터 사이클 브랜치에서 진행하고(IMP-011), 모든 DEF는 ID를 붙여 커밋 `Closes:`로 닫는다.
5. M3 저장 구현 때 세이브 변조·도중 종료 시험을 새로 설계한다(이번 사이클 제외 항목).

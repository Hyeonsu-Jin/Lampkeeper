# 해소된 위험 (보관용)

| 위험 | 출처 | 영향 | 대응 |
|---|---|---|---|
| git 저장소 아님 | 총괄 | 형상 관리 불가 | 해소: 2026-11-03 git init + git lfs install |
| Unity Pipeline 패키지(com.unity.pipeline 0.9.1-exp.1)가 에디터 전용이 아님 | developer | 런타임 서버가 빌드에 들어갈 위험 | 해소: 허용 + 안전장치(기호 금지 시험, 출시 빌드 산출물 검사). MCP 실제 연결 사용자 확인 완료(2026-11-03) |
| SCR-001(Steam 미실행) 실제 화면 미확인 (Steam 실행 중이라 단위 시험만) | developer | UI 표시 문제 늦게 발견 | 해소: 사용자가 에디터 GUI에서 Steam 끄고 확인, 정상(2026-11-03) |
| SDD 3.4.2 표와 시험 asmdef 참조가 다름 (Game.Tests.WorldGen 신설, Infrastructure·Integration 참조 추가) | developer (M1 묶음 2) | 문서와 실제 불일치 | 해소: SDD v0.9 (2026-11-03) |
| Steam 「친구 전용」 로비는 로비 검색 목록에 나오지 않음(Steam 정상 동작) | developer (M1-07) | 없음 | 해소: 사용자 확인 2026-11-04, 정상 동작이며 공개 목록은 「공개」 방만 |

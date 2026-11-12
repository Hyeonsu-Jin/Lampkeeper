// =============================================================
// 파일: JoinTimeline.cs
// 역할: 중도 참가 시간(접속 시작 → 조작 가능)을 단계별로 기록·계산한다. 단계는 SDD 3.12.6 예산표와 같은 구분이다:
//       연결·허가 / 월드 생성·검증·요약값 / 스냅샷 수신·적용 / (사람 클라이언트만) 지형·자원 뷰 준비.
// 레이어: application (Game.Application/Perf, 순수 C#)
// 관련: NFR-018(중도 참가 60초), NFR-021, DES-108 / SDD 3.12.6, 3.12.4
//
// 시계는 호출한 쪽이 넘긴다(시험에서 가짜 시간을 쓸 수 있다). 값이 없는 단계는 -1.
// =============================================================
using System.Globalization;
using System.Text;
using Game.Application.Session;

namespace Game.Application.Perf
{
    /// <summary>참가 시간 기록. 접속 시도마다 새로 만든다.</summary>
    public sealed class JoinTimeline
    {
        /// <summary>중도 참가 전체 상한(초). NFR-018.</summary>
        public const double BudgetTotalSeconds = 60d;

        /// <summary>단계별 예산(초). SDD 3.12.6: 연결 5, 월드 생성 25, 스냅샷 수신·적용·스폰 3+2, 뷰 8.</summary>
        public const double BudgetConnectSeconds = 5d;
        public const double BudgetGenerateSeconds = 25d;
        public const double BudgetSnapshotSeconds = 5d;
        public const double BudgetViewsSeconds = 8d;

        private const double NotRecorded = -1d;

        private double _connecting = NotRecorded;
        private double _generating = NotRecorded;
        private double _syncing = NotRecorded;
        private double _inGame = NotRecorded;
        private double _viewsReady = NotRecorded;

        /// <summary>접속 상태가 바뀔 때 부른다. 같은 상태가 다시 와도 처음 시각을 지킨다.</summary>
        /// <param name="state">새 상태</param>
        /// <param name="nowSeconds">현재 시각(초, 같은 시계)</param>
        public void Mark(ClientSessionState state, double nowSeconds)
        {
            switch (state)
            {
                case ClientSessionState.Connecting:
                    if (_connecting < 0d) _connecting = nowSeconds;
                    break;
                case ClientSessionState.Generating:
                    if (_generating < 0d) _generating = nowSeconds;
                    break;
                case ClientSessionState.Syncing:
                    if (_syncing < 0d) _syncing = nowSeconds;
                    break;
                case ClientSessionState.InGame:
                    if (_inGame < 0d) _inGame = nowSeconds;
                    break;
            }
        }

        /// <summary>지형·자원 뷰 준비가 끝났을 때(사람 클라이언트만) 부른다.</summary>
        public void MarkViewsReady(double nowSeconds)
        {
            if (_viewsReady < 0d) _viewsReady = nowSeconds;
        }

        /// <summary>조작 가능 상태(InGame)까지 왔는지.</summary>
        public bool IsComplete => _inGame >= 0d;

        /// <summary>연결·참가 요청·허가 구간(초). 없으면 -1.</summary>
        public double ConnectSeconds => Span(_connecting, _generating);

        /// <summary>월드 생성·검증·요약값 구간(초). 없으면 -1.</summary>
        public double GenerateSeconds => Span(_generating, _syncing);

        /// <summary>스냅샷 수신·적용 구간(초). 없으면 -1.</summary>
        public double SnapshotSeconds => Span(_syncing, _inGame);

        /// <summary>지형·자원 뷰 준비 구간(초). 봇은 뷰를 만들지 않아 -1.</summary>
        public double ViewsSeconds => Span(_inGame, _viewsReady);

        /// <summary>접속 시작부터 끝까지(초). 뷰까지 쟀으면 뷰 준비 끝, 아니면 InGame까지. 끝나지 않았으면 -1.</summary>
        public double TotalSeconds => Span(_connecting, _viewsReady >= 0d ? _viewsReady : _inGame);

        /// <summary>전체 시간이 예산 안인지. 끝나지 않았으면 false.</summary>
        public bool WithinBudget => TotalSeconds >= 0d && TotalSeconds <= BudgetTotalSeconds;

        /// <summary>로그 한 줄.</summary>
        public string ToLogLine()
        {
            var b = new StringBuilder();
            b.Append("연결 ").Append(F(ConnectSeconds)).Append("s / 월드 생성 ").Append(F(GenerateSeconds))
             .Append("s / 스냅샷 ").Append(F(SnapshotSeconds)).Append("s / 뷰 ").Append(F(ViewsSeconds))
             .Append("s / 합계 ").Append(F(TotalSeconds)).Append("s (예산 ").Append(BudgetTotalSeconds.ToString("0", CultureInfo.InvariantCulture)).Append("s)");
            return b.ToString();
        }

        /// <summary>
        /// 요약 JSON(측정 스크립트가 읽는다). 값이 없는 단계는 null.
        /// </summary>
        /// <param name="playerLabel">플레이어 표시(개발용 ID 등)</param>
        /// <param name="isBot">봇이면 true(뷰 단계는 없음)</param>
        public string ToJson(string playerLabel, bool isBot)
        {
            var b = new StringBuilder();
            b.Append("{\n");
            b.Append("  \"player\": \"").Append(Escape(playerLabel)).Append("\",\n");
            b.Append("  \"bot\": ").Append(isBot ? "true" : "false").Append(",\n");
            b.Append("  \"complete\": ").Append(IsComplete ? "true" : "false").Append(",\n");
            b.Append("  \"connect_s\": ").Append(J(ConnectSeconds)).Append(",\n");
            b.Append("  \"generate_s\": ").Append(J(GenerateSeconds)).Append(",\n");
            b.Append("  \"snapshot_s\": ").Append(J(SnapshotSeconds)).Append(",\n");
            b.Append("  \"views_s\": ").Append(J(ViewsSeconds)).Append(",\n");
            b.Append("  \"total_s\": ").Append(J(TotalSeconds)).Append(",\n");
            b.Append("  \"budget_total_s\": ").Append(BudgetTotalSeconds.ToString("0", CultureInfo.InvariantCulture)).Append(",\n");
            b.Append("  \"within_budget\": ").Append(WithinBudget ? "true" : "false").Append('\n');
            b.Append('}');
            return b.ToString();
        }

        private static double Span(double from, double to) => from < 0d || to < 0d ? NotRecorded : (to >= from ? to - from : 0d);

        private static string F(double value) => value < 0d ? "-" : value.ToString("0.00", CultureInfo.InvariantCulture);

        private static string J(double value) => value < 0d ? "null" : value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Escape(string text) => (text ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

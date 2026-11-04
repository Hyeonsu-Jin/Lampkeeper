// =============================================================
// 파일: GameHud.cs
// 역할: 게임 HUD(SCR-015)의 M1 범위 — 십자선(①), 스태미나 호(⑱), 스태미나 부족 알림, 접속자 명단(개발용), 연결 상태 한 줄(개발용).
//       uGUI 프리팹(Hud/GameHud: 정적 루트 + HudDynamic 하위 캔버스)에 붙는 화면 컴포넌트.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-025, NFR-001(값이 바뀌면 0.5초 안에 갱신), NFR-056, I-07, DES-091, DES-122 / SDD 3.7.10 (4)·(9), 3.7.11, STORYBOARD SCR-015
//
// 정적·동적 분리: 십자선·명단 틀은 정적 루트 캔버스, 자주 바뀌는 스태미나 호·경고·연결 상태 줄은 HudDynamic 하위 캔버스에 있다.
// 하위 캔버스에는 Layout Group 을 두지 않는다(매 프레임 레이아웃 재계산 방지). 값이 같으면 글자·정점을 다시 쓰지 않는다.
// ESC 게임 메뉴는 GameMenu 로 분리됐다.
// =============================================================
using System;
using System.Collections.Generic;
using System.Text;
using Game.Adapters.Presentation.UI.Common;
using Game.Application.Hud;
using Game.Application.Ports;
using Game.Application.Replication;
using UnityEngine;

namespace Game.Adapters.Presentation.UI.Hud
{
    /// <summary>게임 HUD 화면 컴포넌트.</summary>
    public sealed class GameHud : UiScreen
    {
        [Header("동적(HudDynamic 하위 캔버스)")]
        [SerializeField] private UiArcGraphic _arc;
        [SerializeField] private CanvasGroup _arcGroup;
        [SerializeField] private ThemedText _staminaWarn;
        [SerializeField] private ThemedText _netStatus;

        [Header("정적")]
        [SerializeField] private ThemedText _rosterTitle;
        [SerializeField] private ThemedText _rosterList;

        private readonly StaminaHudModel _staminaModel = new StaminaHudModel();
        private readonly StringBuilder _builder = new StringBuilder();
        private readonly List<uint> _slots = new List<uint>();
        private ILocalizedText _text;
        private string _warnText = string.Empty;
        private int _rosterVersion = -1;
        private int _maxPlayers;
        private bool _warnShown;

        public UiArcGraphic Arc => _arc;
        public ThemedText StaminaWarn => _staminaWarn;
        public ThemedText NetStatusText => _netStatus;
        public ThemedText RosterTitle => _rosterTitle;
        public ThemedText RosterList => _rosterList;

        /// <summary>호가 보이는지(불투명도 0보다 큼, 시험용).</summary>
        public float ArcOpacity => _arcGroup.alpha;

        /// <summary>HUD 를 시작 상태로 만든다(처음 한 번).</summary>
        /// <param name="text">동적 글자를 만들 글자 공급자</param>
        /// <param name="maxPlayers">명단 제목에 보일 최대 인원</param>
        public GameHud Build(ILocalizedText text, int maxPlayers)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _maxPlayers = maxPlayers;
            // 경고 문구는 값이 바뀔 때만 만든다(매 프레임 문자열 연결 방지).
            _warnText = UiGlyphs.Warning + " " + _text.Get(HudTextKeys.StaminaLow);
            _staminaWarn.SetDynamicText(_warnText);
            _staminaWarn.gameObject.SetActive(false);
            _warnShown = false;
            _arcGroup.alpha = 0f;
            return this;
        }

        /// <summary>HUD 는 입력을 받지 않으므로 Esc 로 닫히지 않는다.</summary>
        public override void OnCancel()
        {
        }

        /// <summary>최대 인원을 바꾼다(명단 제목 「접속자 n/8」).</summary>
        public void SetMaxPlayers(int maxPlayers)
        {
            _maxPlayers = maxPlayers;
            _rosterVersion = -1;
        }

        /// <summary>
        /// 매 프레임 불러 스태미나 호와 명단을 갱신한다. 값이 바뀐 프레임에 바로 반영해 0.5초 안 갱신 기준을 지킨다. [NFR-001]
        /// </summary>
        /// <param name="ratio">스태미나 비율(0~1)</param>
        /// <param name="isFull">가득 찼는지</param>
        /// <param name="deltaSeconds">경과 시간(비스케일 시간)</param>
        /// <param name="mirror">명단을 읽을 클라이언트 월드 거울(없으면 명단은 그대로)</param>
        public void Tick(float ratio, bool isFull, float deltaSeconds, ClientWorldMirror mirror)
        {
            StaminaHudState state = _staminaModel.Update(ratio, isFull, deltaSeconds);
            if (!Mathf.Approximately(_arcGroup.alpha, state.Opacity))
            {
                _arcGroup.alpha = state.Opacity;
            }
            _arc.SetState(state.Ratio, state.Low);

            // 25% 이하: 점선 호 + 경고 기호 + 글자(색에만 의존하지 않음)
            bool showWarn = state.Visible && state.Low;
            if (showWarn != _warnShown)
            {
                _warnShown = showWarn;
                _staminaWarn.gameObject.SetActive(showWarn);
            }

            if (mirror != null && mirror.Roster.Version != _rosterVersion)
            {
                _rosterVersion = mirror.Roster.Version;
                RefreshRoster(mirror);
            }
        }

        /// <summary>연결 상태 한 줄(개발용). 같은 글자면 다시 쓰지 않는다.</summary>
        public void SetNetStatus(string text) => _netStatus.SetDynamicText(text);

        private void RefreshRoster(ClientWorldMirror mirror)
        {
            _rosterTitle.SetDynamicText(_text.Get(HudTextKeys.RosterTitle, mirror.Roster.Count, _maxPlayers));
            _builder.Clear();
            _slots.Clear();
            foreach (KeyValuePair<uint, RosterEntry> pair in mirror.Roster.Entries)
            {
                _slots.Add(pair.Key);
            }
            _slots.Sort();
            foreach (uint slot in _slots)
            {
                mirror.Roster.TryGet(slot, out RosterEntry entry);
                // 동료는 색이 아니라 ◆ 기호와 이름으로 구분한다. 호스트는 ★ 표시. (플레이어별 색 없음)
                _builder.Append(entry.IsHost ? UiGlyphs.Host : UiGlyphs.Ally).Append(' ').Append(entry.Name);
                if (entry.IsHost)
                {
                    _builder.Append(" (").Append(_text.Get(HudTextKeys.RosterHostTag)).Append(')');
                }
                _builder.Append('\n');
            }
            _rosterList.SetDynamicText(_builder.ToString().TrimEnd('\n'));
        }
    }
}

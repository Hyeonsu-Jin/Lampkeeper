// =============================================================
// 파일: UiGlyphsAndKeys.cs
// 역할: M1 화면의 텍스트 키 상수(UI 기호는 쓰지 않는다. 글꼴에 없는 글자가 □로 보이기 때문).
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-130(색만으로 구분 금지), NFR-034, NFR-044, NFR-056 / SDD 3.7.10, STORYBOARD 2.7, 2.8
//
// 색으로 구분하는 상태는 색 + 글자를 함께 쓴다(기호 없음).
// 글자는 키로만 다루고, 실제 문장은 strings.ko.txt / strings.en.txt에 있다(M2-01에서 String Table로 이동).
// =============================================================
namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>개발용 로비·접속 화면(SCR-004 M1 최소판, SCR-012)의 텍스트 키.</summary>
    public static class LobbyTextKeys
    {
        public const string Title = "lobby.title";
        public const string Description = "lobby.desc";
        public const string Host = "lobby.host";
        public const string Refresh = "lobby.refresh";
        public const string Invite = "lobby.invite";
        public const string Leave = "lobby.leave";
        public const string StatusReady = "lobby.status.ready";
        public const string StatusOpening = "lobby.status.opening";
        public const string StatusGenerating = "lobby.status.generating";
        public const string StatusOpen = "lobby.status.open";
        public const string StatusFailed = "lobby.status.failed";
        public const string StatusDevHost = "lobby.status.dev_host";
        public const string ListLoading = "lobby.list.loading";
        public const string ListEmpty = "lobby.list.empty";
        public const string ListError = "lobby.list.error";
        public const string Join = "lobby.join";
        public const string RowFormat = "lobby.row";

        public const string ConnectTitle = "connect.title";
        public const string StepHost = "connect.step.host";
        public const string StepInfo = "connect.step.info";
        public const string StepWorld = "connect.step.world";
        public const string StepApply = "connect.step.apply";
        public const string StateDone = "connect.state.done";
        public const string StateActive = "connect.state.active";
        public const string StatePending = "connect.state.pending";
        public const string Cancel = "connect.cancel";
        public const string Ok = "connect.ok";
    }

    /// <summary>게임 HUD(SCR-015 M1 범위)의 텍스트 키.</summary>
    public static class HudTextKeys
    {
        public const string RosterTitle = "hud.roster.title";
        public const string RosterHostTag = "hud.roster.host";
        public const string StaminaLow = "hud.stamina.low";
        public const string NetStatus = "hud.net.status";
        public const string MenuTitle = "hud.menu.title";
        public const string MenuDesc = "hud.menu.desc";
        public const string MenuLeave = "hud.menu.leave";
        public const string MenuClose = "hud.menu.close";
        public const string MenuInvite = "hud.menu.invite";
        public const string GameRunning = "hud.menu.running";
        public const string InviteUnavailable = "hud.menu.invite_unavailable";
    }
}

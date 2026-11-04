// =============================================================
// 파일: SteamRequiredScreen.cs
// 역할: SCR-001(Steam 실행 필요 안내) 화면. uGUI 프리팹(Screens/SteamRequired)의 글자·버튼을 연결하고 상태별 표시를 바꾼다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: NFR-006(Steam 실행 필수), NFR-034, DES-091, DES-122 / STORYBOARD SCR-001 ①~⑤ / SDD 3.7.10, D-27
//
// 요소 번호 (STORYBOARD SCR-001)
//   ① 제목(_title)   ② 설명(_description) + 실패 문구(_status)
//   ③ [다시 확인](_retry, 주 버튼, 기본 포커스)   ⑤ [종료](_quit)   ④ 비어 있음(번호 재사용 안 함)
// 상태: 기본 / 재확인 중(③ 사용 불가 + 로딩 문구) / 오류(② 아래 실패 문구)
// 이 화면이 떠 있는 동안 메인 메뉴·옵션은 열 수 없다(다른 화면으로 가는 길을 만들지 않는다). Esc 로 닫히지 않는다.
// =============================================================
using System;
using Game.Adapters.Presentation.UI.Common;
using UnityEngine;

namespace Game.Adapters.Presentation.UI.Screens
{
    /// <summary>SCR-001 표시 상태.</summary>
    public enum SteamRequiredState
    {
        /// <summary>처음 표시된 기본 상태.</summary>
        Default = 0,

        /// <summary>③ 눌러서 Steam 상태를 다시 조회하는 중.</summary>
        Checking = 1,

        /// <summary>재확인에 실패해 ② 아래에 실패 문구를 보이는 상태.</summary>
        Failed = 2,
    }

    /// <summary>SCR-001 화면 컴포넌트(화면 프리팹 루트에 붙는다).</summary>
    public sealed class SteamRequiredScreen : UiScreen
    {
        [SerializeField] private ThemedText _title;
        [SerializeField] private ThemedText _description;
        [SerializeField] private ThemedText _status;
        [SerializeField] private UiButton _retry;
        [SerializeField] private ThemedText _retryLabel;
        [SerializeField] private UiButton _quit;

        /// <summary>③ [다시 확인]을 눌렀을 때.</summary>
        public event Action RetryRequested;

        /// <summary>⑤ [종료]를 눌렀을 때.</summary>
        public event Action QuitRequested;

        /// <summary>현재 표시 상태.</summary>
        public SteamRequiredState State { get; private set; }

        /// <summary>화면이 떠 있는지.</summary>
        public bool IsShown => IsVisible;

        public UiButton RetryButton => _retry;
        public UiButton QuitButton => _quit;
        public ThemedText StatusText => _status;
        public ThemedText RetryLabel => _retryLabel;

        private bool _wired;

        protected override void Awake()
        {
            base.Awake();
            Wire();
        }

        /// <summary>버튼 동작을 한 번만 연결한다(편집 모드 시험에서는 Awake 가 불리지 않으므로 Show 에서도 부른다).</summary>
        private void Wire()
        {
            if (_wired)
            {
                return;
            }
            _wired = true;
            _retry.onClick.AddListener(RequestRetry);
            _quit.onClick.AddListener(RequestQuit);
            ApplyState(SteamRequiredState.Default);
        }

        /// <summary>화면을 켠다. 글자는 UiRoot 가 정한 글자 공급자를 따른다.</summary>
        public void Show()
        {
            Wire();
            ApplyState(SteamRequiredState.Default);
            SetVisible(true);
        }

        /// <summary>③ 눌린 뒤 결과를 기다리는 상태로 바꾼다.</summary>
        public void ShowChecking()
        {
            ApplyState(SteamRequiredState.Checking);
        }

        /// <summary>재확인에 실패했음을 ② 아래에 보인다.</summary>
        public void ShowRetryFailed()
        {
            ApplyState(SteamRequiredState.Failed);
            SelectFirst();
        }

        /// <summary>화면을 닫는다(재확인에 성공했을 때).</summary>
        public void Hide()
        {
            SetVisible(false);
        }

        /// <summary>③ [다시 확인] 동작. 확인 중에는 중복 입력을 무시한다. 버튼 클릭과 시험이 같은 경로를 쓴다.</summary>
        public void RequestRetry()
        {
            if (State == SteamRequiredState.Checking)
            {
                return;
            }
            RetryRequested?.Invoke();
        }

        /// <summary>⑤ [종료] 동작.</summary>
        public void RequestQuit()
        {
            QuitRequested?.Invoke();
        }

        /// <summary>SCR-001 은 닫을 수 없다(오프라인 플레이 없음). Esc 를 무시한다.</summary>
        public override void OnCancel()
        {
        }

        /// <summary>상태에 맞게 문구와 버튼을 바꾼다.</summary>
        private void ApplyState(SteamRequiredState state)
        {
            State = state;
            switch (state)
            {
                case SteamRequiredState.Checking:
                    // 재확인 중에는 중복 입력을 막는다. 문구로도 알려 색이나 점에만 의존하지 않는다.
                    _retry.SetUnavailable(true);
                    _retryLabel.SetTextKey(SteamRequiredTextKeys.Checking);
                    SetStatusVisible(false);
                    break;
                case SteamRequiredState.Failed:
                    _retry.SetUnavailable(false);
                    _retryLabel.SetTextKey(SteamRequiredTextKeys.Retry);
                    SetStatusVisible(true);
                    break;
                default:
                    _retry.SetUnavailable(false);
                    _retryLabel.SetTextKey(SteamRequiredTextKeys.Retry);
                    SetStatusVisible(false);
                    break;
            }
        }

        private void SetStatusVisible(bool visible)
        {
            // 숨길 때는 오브젝트를 꺼서 레이아웃에서 빠지게 한다(빈 줄이 남지 않음).
            _status.gameObject.SetActive(visible);
        }
    }
}

// =============================================================
// 파일: SteamRequiredView.cs
// 역할: SCR-001(Steam 실행 필요 안내)의 화면 동작. UXML로 만든 요소 트리에 글자를 채우고 버튼 동작을 잇는다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: NFR-006(Steam 실행 필수), NFR-034 / STORYBOARD SCR-001 ①~⑤, DES-091
//
// 요소 번호 (STORYBOARD SCR-001)
//   ① 제목 (name="title")      ② 설명 (name="desc") + 실패 문구 (name="status")
//   ③ [다시 확인] (name="retry", 주 버튼, 기본 포커스)      ⑤ [종료] (name="quit")
//   ④ 비어 있음(번호 재사용 안 함)
// 상태: 기본 / 재확인 중(③ 비활성 + 로딩 문구) / 오류(② 아래 실패 문구)
// 이 화면이 떠 있는 동안 메인 메뉴·옵션은 열 수 없다(이 클래스는 다른 화면으로 가는 길을 만들지 않는다).
// =============================================================
using System;
using Game.Application.Ports;
using UnityEngine.UIElements;

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

    /// <summary>SCR-001의 요소 연결과 상태 표시. UI Toolkit 요소만 다루는 일반 클래스라 EditMode에서 시험할 수 있다.</summary>
    public sealed class SteamRequiredView
    {
        /// <summary>UXML 요소 이름: ① 제목</summary>
        public const string TitleName = "title";
        /// <summary>UXML 요소 이름: ② 설명</summary>
        public const string DescriptionName = "desc";
        /// <summary>UXML 요소 이름: 재확인 실패 문구</summary>
        public const string StatusName = "status";
        /// <summary>UXML 요소 이름: ③ [다시 확인]</summary>
        public const string RetryName = "retry";
        /// <summary>UXML 요소 이름: ⑤ [종료]</summary>
        public const string QuitName = "quit";

        /// <summary>상태 문구를 숨길 때 쓰는 USS 클래스.</summary>
        public const string HiddenClass = "is-hidden";

        private readonly ILocalizedText _text;
        private readonly Label _title;
        private readonly Label _description;
        private readonly Label _status;
        private readonly Button _retry;
        private readonly Button _quit;

        /// <summary>③ 버튼을 눌렀을 때.</summary>
        public event Action RetryRequested;

        /// <summary>⑤ 버튼을 눌렀을 때.</summary>
        public event Action QuitRequested;

        /// <summary>현재 상태.</summary>
        public SteamRequiredState State { get; private set; }

        /// <param name="root">UXML을 복제해 만든 요소 트리</param>
        /// <param name="text">글자 공급자</param>
        /// <exception cref="ArgumentNullException">root 또는 text가 null일 때</exception>
        /// <exception cref="InvalidOperationException">필수 요소를 UXML에서 찾지 못했을 때</exception>
        public SteamRequiredView(VisualElement root, ILocalizedText text)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            _text = text ?? throw new ArgumentNullException(nameof(text));

            _title = Require<Label>(root, TitleName);
            _description = Require<Label>(root, DescriptionName);
            _status = Require<Label>(root, StatusName);
            _retry = Require<Button>(root, RetryName);
            _quit = Require<Button>(root, QuitName);

            // 글자는 키로만 가져온다(NFR-034).
            _title.text = _text.Get(SteamRequiredTextKeys.Title);
            _description.text = _text.Get(SteamRequiredTextKeys.Description);
            _retry.text = _text.Get(SteamRequiredTextKeys.Retry);
            _quit.text = _text.Get(SteamRequiredTextKeys.Quit);

            // 키보드·게임패드로 이동할 수 있게 둘 다 포커스를 받는다. 순서는 ③ -> ⑤(UXML 순서와 같다).
            _retry.focusable = true;
            _quit.focusable = true;
            _retry.clicked += RequestRetry;
            _quit.clicked += RequestQuit;

            SetState(SteamRequiredState.Default);
        }

        /// <summary>
        /// ③ [다시 확인] 동작. 확인 중에는 중복 입력을 무시한다. 버튼 클릭과 시험이 같은 경로를 쓴다.
        /// </summary>
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

        /// <summary>③에 기본 포커스를 준다(STORYBOARD 접근성: 기본 포커스 ③).</summary>
        public void FocusDefault()
        {
            _retry.Focus();
        }

        /// <summary>상태에 맞게 문구와 버튼을 바꾼다.</summary>
        public void SetState(SteamRequiredState state)
        {
            State = state;
            switch (state)
            {
                case SteamRequiredState.Checking:
                    // 재확인 중에는 중복 입력을 막는다. 문구로도 알려 색이나 점에만 의존하지 않는다.
                    _retry.SetEnabled(false);
                    _retry.text = _text.Get(SteamRequiredTextKeys.Checking);
                    SetStatusVisible(false, string.Empty);
                    break;
                case SteamRequiredState.Failed:
                    _retry.SetEnabled(true);
                    _retry.text = _text.Get(SteamRequiredTextKeys.Retry);
                    SetStatusVisible(true, _text.Get(SteamRequiredTextKeys.RetryFailed));
                    break;
                default:
                    _retry.SetEnabled(true);
                    _retry.text = _text.Get(SteamRequiredTextKeys.Retry);
                    SetStatusVisible(false, string.Empty);
                    break;
            }
        }

        private void SetStatusVisible(bool visible, string message)
        {
            _status.text = message;
            _status.EnableInClassList(HiddenClass, !visible);
        }

        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException("SteamRequired.uxml에 '" + name + "' (" + typeof(T).Name + ") 요소가 없습니다.");
            }
            return element;
        }
    }
}

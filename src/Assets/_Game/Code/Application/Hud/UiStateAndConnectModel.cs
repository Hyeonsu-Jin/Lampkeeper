// =============================================================
// 파일: UiStateAndConnectModel.cs
// 역할: (1) UI 상태(Gameplay/Overlay): 어떤 화면을 열어도 게임은 멈추지 않고 로컬 입력 경로만 바뀐다는 규칙의 상태 기계.
//       (2) 참가 중 화면(SCR-012)의 단계 목록·진행 막대·실패 문구 키를 접속 상태에서 계산하는 표시 모델.
// 레이어: application (Game.Application/Hud, 순수 C#)
// 관련: I-07(화면을 열어도 게임은 멈추지 않음), FR-003, FR-004, FR-009, NFR-003, NFR-018, DES-091, DES-072 / SDD 3.10.7, STORYBOARD SCR-012
//
// 이 파일에는 「일시정지」 개념이 없다. 서버 틱·AI·네트워크·월드 시각은 UI 상태를 모른다(3.10.7).
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Session;
using Game.Domain.Session;

namespace Game.Application.Hud
{
    /// <summary>UI 상태. Overlay이면 이동·시점·공격 입력을 막고 UI 입력을 받는다(로컬 입력 경로만).</summary>
    public enum UiMode : byte
    {
        Gameplay = 0,
        Overlay = 1,
    }

    /// <summary>
    /// UI 상태 기계. 열려 있는 화면(창) 수로 Gameplay/Overlay를 정한다. 화면 사이 전환은 Overlay 안에서 일어난다.
    /// 게임 진행(서버 틱 등)에는 아무 영향도 주지 않는다.
    /// </summary>
    public sealed class UiState
    {
        private readonly List<string> _open = new List<string>();

        /// <summary>현재 상태.</summary>
        public UiMode Mode => _open.Count > 0 ? UiMode.Overlay : UiMode.Gameplay;

        /// <summary>열려 있는 화면 수.</summary>
        public int OpenCount => _open.Count;

        /// <summary>상태가 바뀜(입력 경로 전환용).</summary>
        public event Action<UiMode> ModeChanged;

        /// <summary>화면(창)을 연다. 같은 화면을 두 번 열어도 한 번만 센다.</summary>
        public void Open(string screenId)
        {
            if (string.IsNullOrEmpty(screenId))
            {
                throw new ArgumentException("화면 ID가 비었습니다.", nameof(screenId));
            }
            UiMode before = Mode;
            if (!_open.Contains(screenId))
            {
                _open.Add(screenId);
            }
            Notify(before);
        }

        /// <summary>화면을 닫는다. 열려 있지 않으면 아무 일도 없다.</summary>
        public void Close(string screenId)
        {
            UiMode before = Mode;
            _open.Remove(screenId);
            Notify(before);
        }

        /// <summary>모든 화면을 닫는다(세션을 떠날 때).</summary>
        public void CloseAll()
        {
            UiMode before = Mode;
            _open.Clear();
            Notify(before);
        }

        public bool IsOpen(string screenId) => _open.Contains(screenId);

        private void Notify(UiMode before)
        {
            if (Mode != before)
            {
                ModeChanged?.Invoke(Mode);
            }
        }
    }

    /// <summary>참가 단계 표시 상태(SCR-012 ①: 완료 / 진행 / 대기).</summary>
    public enum StepStatus : byte
    {
        Pending = 0,
        Active = 1,
        Done = 2,
    }

    /// <summary>참가 중 화면 표시 값(단계 4개 + 진행 비율).</summary>
    public readonly struct ConnectProgress
    {
        public const int StepCount = 4;

        public StepStatus ConnectHost { get; }
        public StepStatus ReceiveWorldInfo { get; }
        public StepStatus CreateWorld { get; }
        public StepStatus ApplyChanges { get; }
        /// <summary>진행 막대 비율(0~1).</summary>
        public float Fraction { get; }

        public ConnectProgress(StepStatus connectHost, StepStatus receiveWorldInfo, StepStatus createWorld, StepStatus applyChanges, float fraction)
        {
            ConnectHost = connectHost;
            ReceiveWorldInfo = receiveWorldInfo;
            CreateWorld = createWorld;
            ApplyChanges = applyChanges;
            Fraction = fraction;
        }
    }

    /// <summary>접속 상태 → 참가 중 화면(SCR-012) 값 계산.</summary>
    public static class ConnectScreenModel
    {
        /// <summary>
        /// 접속 상태에서 단계 표시를 계산한다. 스냅샷 조각을 받는 중이면 마지막 단계 안에서 막대를 채운다.
        /// </summary>
        /// <param name="state">접속 상태</param>
        /// <param name="snapshotReceived">받은 스냅샷 조각 수</param>
        /// <param name="snapshotExpected">스냅샷 조각 총 수(모르면 0)</param>
        public static ConnectProgress Compute(ClientSessionState state, int snapshotReceived = 0, int snapshotExpected = 0)
        {
            const float quarter = 0.25f;
            switch (state)
            {
                case ClientSessionState.Connecting:
                    return new ConnectProgress(StepStatus.Active, StepStatus.Pending, StepStatus.Pending, StepStatus.Pending, 0f);
                case ClientSessionState.Generating:
                    return new ConnectProgress(StepStatus.Done, StepStatus.Done, StepStatus.Active, StepStatus.Pending, quarter * 2f);
                case ClientSessionState.Syncing:
                {
                    float inner = snapshotExpected > 0 ? Math.Min(1f, (float)snapshotReceived / snapshotExpected) : 0f;
                    return new ConnectProgress(StepStatus.Done, StepStatus.Done, StepStatus.Done, StepStatus.Active, quarter * 3f + quarter * inner);
                }
                case ClientSessionState.InGame:
                    return new ConnectProgress(StepStatus.Done, StepStatus.Done, StepStatus.Done, StepStatus.Done, 1f);
                default:
                    return new ConnectProgress(StepStatus.Pending, StepStatus.Pending, StepStatus.Pending, StepStatus.Pending, 0f);
            }
        }

        /// <summary>
        /// 접속이 끝난 이유에 맞는 실패 문구 키의 앞부분을 돌려준다. 화면은 뒤에 `.title`, `.cause`, `.fix`를 붙여 조회한다. [NFR-003]
        /// </summary>
        public static string FailureKeyPrefix(ClientFailure failure, JoinRejectReason reason)
        {
            switch (failure)
            {
                case ClientFailure.Rejected:
                    switch (reason)
                    {
                        case JoinRejectReason.Full: return "join.fail.full";
                        case JoinRejectReason.VersionMismatch: return "join.fail.version";
                        case JoinRejectReason.PasswordMismatch: return "join.fail.password";
                        case JoinRejectReason.AttemptLimit: return "join.fail.attempts";
                        case JoinRejectReason.Banned: return "join.fail.banned";
                        case JoinRejectReason.AccessDenied: return "join.fail.access";
                        case JoinRejectReason.GenMismatch: return "join.fail.gen_mismatch";
                        case JoinRejectReason.Timeout: return "join.fail.noresponse";
                        default: return "join.fail.generic";
                    }
                case ClientFailure.WorldMismatch: return "join.fail.gen_mismatch";
                case ClientFailure.ConnectionLost: return "join.fail.lost";
                default: return "join.fail.noresponse";
            }
        }
    }
}

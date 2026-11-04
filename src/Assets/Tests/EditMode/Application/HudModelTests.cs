// =============================================================
// 파일: HudModelTests.cs
// 역할: 스태미나 호 표시 모델(StaminaHudModel), UI 상태 기계(UiState), 참가 중 화면 모델(ConnectScreenModel) 시험.
//       「화면을 열어도 게임은 멈추지 않는다」 규칙(어플리케이션에 일시정지 개념 없음) 확인 포함.
// 레이어: test (EditMode, Game.Tests.Application)
// 관련: FR-025, NFR-001, NFR-003, I-07, DES-072, DES-074, DES-091 / SDD 3.7.11, 3.10.7, STORYBOARD SCR-012, SCR-015 ⑱
// =============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Application.Hud;
using Game.Application.Server;
using Game.Application.Session;
using Game.Domain.Session;
using NUnit.Framework;

namespace Game.Tests.Application
{
    public class StaminaHudModelTests
    {
        [Test]
        public void FullAndIdle_FadesOutOverOneSecond()
        {
            var model = new StaminaHudModel();
            Assert.IsTrue(model.Update(0.5f, false, 0.016f).Visible, "소모·회복 중에는 보인다");
            Assert.AreEqual(1f, model.Update(1f, false, 0.016f).Opacity);

            StaminaHudState half = model.Update(1f, true, 0.5f);
            Assert.AreEqual(0.5f, half.Opacity, 0.01f, "가득 차면 1초에 걸쳐 사라진다");
            Assert.IsFalse(model.Update(1f, true, 0.6f).Visible);
        }

        [Test]
        public void LowThreshold_IsTwentyFivePercentAndBelow()
        {
            var model = new StaminaHudModel();
            Assert.IsTrue(model.Update(0.25f, false, 0.016f).Low, "25% 이하: 점선 + 경고");
            Assert.IsFalse(model.Update(0.26f, false, 0.016f).Low);
        }

        [Test]
        public void RatioIsClamped()
        {
            var model = new StaminaHudModel();
            Assert.AreEqual(1f, model.Update(5f, false, 0.01f).Ratio);
            Assert.AreEqual(0f, model.Update(-1f, false, 0.01f).Ratio);
        }

        [Test]
        public void Reappears_ImmediatelyWhenSpendingStartsAgain()
        {
            var model = new StaminaHudModel();
            model.Update(1f, true, 2f);
            Assert.IsFalse(model.Update(1f, true, 0.1f).Visible);
            Assert.AreEqual(1f, model.Update(0.9f, false, 0.016f).Opacity);
        }
    }

    public class UiStateTests
    {
        [Test]
        public void OpeningAnyScreen_SwitchesToOverlay_ClosingAllReturnsToGameplay()
        {
            var state = new UiState();
            var modes = new List<UiMode>();
            state.ModeChanged += modes.Add;
            Assert.AreEqual(UiMode.Gameplay, state.Mode);

            state.Open("inventory");
            state.Open("map"); // 화면 사이 전환은 Overlay 안에서
            Assert.AreEqual(UiMode.Overlay, state.Mode);
            state.Close("inventory");
            Assert.AreEqual(UiMode.Overlay, state.Mode);
            state.Close("map");
            Assert.AreEqual(UiMode.Gameplay, state.Mode);
            CollectionAssert.AreEqual(new[] { UiMode.Overlay, UiMode.Gameplay }, modes, "모드가 바뀔 때만 알린다");
        }

        [Test]
        public void SameScreenOpenedTwice_CountsOnce_AndCloseAllResets()
        {
            var state = new UiState();
            state.Open("menu");
            state.Open("menu");
            Assert.AreEqual(1, state.OpenCount);
            state.CloseAll();
            Assert.AreEqual(UiMode.Gameplay, state.Mode);
            Assert.DoesNotThrow(() => state.Close("never-opened"));
        }

        [Test]
        public void EmptyScreenId_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => new UiState().Open(""));
        }

        [Test]
        public void ApplicationLayer_HasNoPauseConcept()
        {
            // [3.10.7] 서버 틱·AI·네트워크·월드 시각은 UI 상태를 모르고, 애플리케이션 레이어에 「일시정지」 API를 두지 않는다.
            Type[] types = typeof(ServerWorld).Assembly.GetTypes();
            foreach (Type type in types)
            {
                StringAssert.DoesNotContain("Pause", type.Name, type.FullName);
                foreach (System.Reflection.MethodInfo method in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    StringAssert.DoesNotContain("Pause", method.Name, type.FullName + "." + method.Name);
                }
            }
        }

        [Test]
        public void ServerWorldTick_DoesNotDependOnUiState()
        {
            // 서버 월드는 UiState 형식을 참조하지 않는다(생성자·필드 어디에도).
            foreach (System.Reflection.ConstructorInfo ctor in typeof(ServerWorld).GetConstructors())
            {
                Assert.IsFalse(ctor.GetParameters().Any(p => p.ParameterType == typeof(UiState)));
            }
            Assert.IsFalse(typeof(ServerWorld).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Any(f => f.FieldType == typeof(UiState)));
        }
    }

    public class ConnectScreenModelTests
    {
        [Test]
        public void StepsFollowSessionState()
        {
            ConnectProgress connecting = ConnectScreenModel.Compute(ClientSessionState.Connecting);
            Assert.AreEqual(StepStatus.Active, connecting.ConnectHost);
            Assert.AreEqual(StepStatus.Pending, connecting.ApplyChanges);
            Assert.AreEqual(0f, connecting.Fraction);

            ConnectProgress generating = ConnectScreenModel.Compute(ClientSessionState.Generating);
            Assert.AreEqual(StepStatus.Done, generating.ReceiveWorldInfo);
            Assert.AreEqual(StepStatus.Active, generating.CreateWorld);

            ConnectProgress syncing = ConnectScreenModel.Compute(ClientSessionState.Syncing, 1, 4);
            Assert.AreEqual(StepStatus.Active, syncing.ApplyChanges);
            Assert.AreEqual(0.75f + 0.25f * 0.25f, syncing.Fraction, 0.001f);

            ConnectProgress done = ConnectScreenModel.Compute(ClientSessionState.InGame);
            Assert.AreEqual(1f, done.Fraction);
            Assert.AreEqual(StepStatus.Done, done.ApplyChanges);
        }

        [Test]
        public void ProgressFraction_NeverDecreasesAcrossStates()
        {
            float last = -1f;
            foreach (ClientSessionState state in new[] { ClientSessionState.Connecting, ClientSessionState.Generating, ClientSessionState.Syncing, ClientSessionState.InGame })
            {
                float fraction = ConnectScreenModel.Compute(state).Fraction;
                Assert.GreaterOrEqual(fraction, last);
                last = fraction;
            }
        }

        [TestCase(JoinRejectReason.Full, "join.fail.full")]
        [TestCase(JoinRejectReason.VersionMismatch, "join.fail.version")]
        [TestCase(JoinRejectReason.Banned, "join.fail.banned")]
        [TestCase(JoinRejectReason.AccessDenied, "join.fail.access")]
        [TestCase(JoinRejectReason.GenMismatch, "join.fail.gen_mismatch")]
        [TestCase(JoinRejectReason.Timeout, "join.fail.noresponse")]
        public void RejectionReasons_MapToSddTextKeys(JoinRejectReason reason, string expected)
        {
            Assert.AreEqual(expected, ConnectScreenModel.FailureKeyPrefix(ClientFailure.Rejected, reason));
        }

        [Test]
        public void OtherFailures_MapToKeys()
        {
            Assert.AreEqual("join.fail.noresponse", ConnectScreenModel.FailureKeyPrefix(ClientFailure.HostNoResponse, JoinRejectReason.None));
            Assert.AreEqual("join.fail.gen_mismatch", ConnectScreenModel.FailureKeyPrefix(ClientFailure.WorldMismatch, JoinRejectReason.None));
            Assert.AreEqual("join.fail.lost", ConnectScreenModel.FailureKeyPrefix(ClientFailure.ConnectionLost, JoinRejectReason.None));
        }
    }
}

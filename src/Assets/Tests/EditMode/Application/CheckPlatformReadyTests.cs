// =============================================================
// 파일: CheckPlatformReadyTests.cs
// 역할: 부트의 Steam 확인(CheckPlatformReady), 전송 선택 규칙(TransportPolicy), 개발용 신원 시험.
// 레이어: test (EditMode, Game.Tests.Application)
// 관련: NFR-006(Steam 실행 필수, 오프라인 없음), FR-006, DES-100, DES-103 / STORYBOARD SCR-001
// =============================================================
using System;
using Game.Application.Boot;
using Game.Application.Ports;
using Game.Domain.Common;
using NUnit.Framework;

namespace Game.Tests.Application
{
    public class CheckPlatformReadyTests
    {
        /// <summary>시험용 가짜 플랫폼. 초기화 결과를 시험이 정한다.</summary>
        private sealed class FakePlatform : IPlatformServices
        {
            public PlatformInitResult NextResult = PlatformInitResult.Ok();
            public int InitializeCalls;
            public bool IsInitialized { get; private set; }
            public IPlatformIdentity Identity { get; private set; }

            public PlatformInitResult Initialize()
            {
                InitializeCalls++;
                if (NextResult.Success)
                {
                    IsInitialized = true;
                    Identity = new DevPlayerIdentity(76561198000000001UL);
                }
                return NextResult;
            }

            public void Tick() { }
            public void Shutdown() { IsInitialized = false; }
        }

        private static LaunchOptions Options(params string[] args) => LaunchOptionsParser.Parse(args);

        [Test]
        public void SteamAvailable_ContinuesToMainMenu_WithSteamTransport()
        {
            var platform = new FakePlatform();
            BootDecision d = new CheckPlatformReady(platform).Execute(LaunchOptions.None, false);

            Assert.AreEqual(BootRoute.ContinueToMainMenu, d.Route);
            Assert.AreEqual(TransportKind.Steam, d.Transport);
            Assert.IsFalse(d.UsesDevIdentity);
            Assert.IsNotNull(d.Identity);
        }

        [TestCase(PlatformInitFailure.SteamNotRunning)]
        [TestCase(PlatformInitFailure.InitFailed)]
        [TestCase(PlatformInitFailure.LibraryUnavailable)]
        [TestCase(PlatformInitFailure.RestartRequested)]
        public void SteamUnavailable_ShowsSteamRequired_NoIdentity(PlatformInitFailure failure)
        {
            // [NFR-006] 오프라인 대안 없이 SCR-001에서 멈춘다.
            var platform = new FakePlatform { NextResult = PlatformInitResult.Fail(failure, "x") };
            BootDecision d = new CheckPlatformReady(platform).Execute(LaunchOptions.None, false);

            Assert.AreEqual(BootRoute.ShowSteamRequired, d.Route);
            Assert.AreEqual(failure, d.Failure);
            Assert.IsNull(d.Identity);
        }

        [Test]
        public void Retry_AfterSteamStarts_Continues()
        {
            var platform = new FakePlatform { NextResult = PlatformInitResult.Fail(PlatformInitFailure.SteamNotRunning, "off") };
            var useCase = new CheckPlatformReady(platform);

            Assert.AreEqual(BootRoute.ShowSteamRequired, useCase.Execute(LaunchOptions.None, false).Route);

            platform.NextResult = PlatformInitResult.Ok(); // 사용자가 Steam을 켠 뒤 [다시 확인]
            Assert.AreEqual(BootRoute.ContinueToMainMenu, useCase.Execute(LaunchOptions.None, false).Route);
            Assert.AreEqual(2, platform.InitializeCalls);
        }

        [Test]
        public void DevBuild_TugboatWithDevPlayerId_SkipsSteam()
        {
            var platform = new FakePlatform { NextResult = PlatformInitResult.Fail(PlatformInitFailure.SteamNotRunning, "off") };
            BootDecision d = new CheckPlatformReady(platform)
                .Execute(Options("-transport", "tugboat", "-devPlayerId", "5"), true);

            Assert.AreEqual(BootRoute.ContinueToMainMenu, d.Route);
            Assert.IsTrue(d.UsesDevIdentity);
            Assert.AreEqual(new PlayerId(5), d.Identity.LocalPlayerId);
            Assert.AreEqual(TransportKind.Tugboat, d.Transport);
            Assert.AreEqual(0, platform.InitializeCalls, "Steam 초기화를 건너뛰어야 한다.");
        }

        [Test]
        public void DevBuild_TugboatWithoutDevPlayerId_StillNeedsSteam()
        {
            var platform = new FakePlatform { NextResult = PlatformInitResult.Fail(PlatformInitFailure.SteamNotRunning, "off") };
            BootDecision d = new CheckPlatformReady(platform).Execute(Options("-transport", "tugboat"), true);
            Assert.AreEqual(BootRoute.ShowSteamRequired, d.Route);
        }

        [Test]
        public void DevBuild_DevPlayerIdWithoutTugboat_StillNeedsSteam()
        {
            var platform = new FakePlatform { NextResult = PlatformInitResult.Fail(PlatformInitFailure.SteamNotRunning, "off") };
            BootDecision d = new CheckPlatformReady(platform).Execute(Options("-devPlayerId", "5"), true);
            Assert.AreEqual(BootRoute.ShowSteamRequired, d.Route);
        }

        [Test]
        public void ReleaseBuild_IgnoresDevOptions_AndForcesSteam()
        {
            // [FR-006] 출시 빌드는 Steam 고정. 개발용 예외도 통하지 않는다.
            var platform = new FakePlatform { NextResult = PlatformInitResult.Fail(PlatformInitFailure.SteamNotRunning, "off") };
            BootDecision d = new CheckPlatformReady(platform)
                .Execute(Options("-transport", "tugboat", "-devPlayerId", "5"), false);

            Assert.AreEqual(BootRoute.ShowSteamRequired, d.Route);
            Assert.AreEqual(TransportKind.Steam, d.Transport);
            Assert.AreEqual(1, platform.InitializeCalls);
        }

        [Test]
        public void NullOptions_AreTreatedAsNone()
        {
            var platform = new FakePlatform();
            Assert.AreEqual(BootRoute.ContinueToMainMenu, new CheckPlatformReady(platform).Execute(null, false).Route);
        }

        [Test]
        public void Constructor_NullPlatform_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CheckPlatformReady(null));
        }

        [TestCase(null, false, TransportKind.Steam)]
        [TestCase(TransportKind.Tugboat, false, TransportKind.Steam)]
        [TestCase(TransportKind.Steam, true, TransportKind.Steam)]
        [TestCase(TransportKind.Tugboat, true, TransportKind.Tugboat)]
        [TestCase(null, true, TransportKind.Steam)]
        public void TransportPolicy_Resolve(TransportKind? requested, bool dev, TransportKind expected)
        {
            Assert.AreEqual(expected, TransportPolicy.Resolve(requested, dev));
        }

        [Test]
        public void DevPlayerIdentity_ZeroThrows_AndNamesAreStable()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DevPlayerIdentity(0));
            var id = new DevPlayerIdentity(3);
            Assert.AreEqual("Dev-3", id.LocalDisplayName);
            Assert.AreEqual("Dev-9", id.ResolveName(new PlayerId(9)));
        }
    }
}

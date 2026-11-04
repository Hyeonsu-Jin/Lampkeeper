// =============================================================
// 파일: RuntimeSupport.cs
// 역할: 조립 루트용 작은 구현들 — 게임 버전(BuildInfo), Unity 로그를 쓰는 ILog(UnityLog), Unity 시계를 쓰는 IClock(UnityClock).
// 레이어: infrastructure (Game.Bootstrap)
// 관련: NFR-009, NFR-058(게임 버전), NFR-042(로그에 민감 정보 제외), DES-100, DES-107 / SDD 3.7.3
// =============================================================
using System;
using Game.Application.Logging;
using Game.Application.Ports;
using Game.Domain.Common;
using UnityEngine;

namespace Game.Bootstrap.Runtime
{
    /// <summary>
    /// 이 실행의 게임 버전(SemVer). 접속 때 호스트와 같은 빌드인지 비교하고(참가 판정, 로비 kh_build), 로그·화면에 표시한다.
    /// 플레이어에서는 빌드 때 BuildCommands가 붙인 `Application.version`(핵심 + 메타데이터), 에디터 재생에서는 `핵심+editor`.
    /// </summary>
    public static class BuildInfo
    {
        private static GameVersion _version;
        private static bool _resolved;

        /// <summary>해석한 게임 버전. 해석하지 못하면 null(부트 판정이 「설정 오류」로 끝낸다).</summary>
        public static GameVersion Version
        {
            get
            {
                if (!_resolved)
                {
                    _version = Resolve(UnityEngine.Application.version, UnityEngine.Application.isEditor);
                    _resolved = true;
                }
                return _version;
            }
        }

        /// <summary>
        /// 개발 빌드 전용: 게임 버전을 강제로 바꾼다(버전이 다른 참가자의 거부를 두 프로세스로 시험하기 위함, SDD R-35).
        /// 호출하는 쪽이 개발 빌드인지 확인한다.
        /// </summary>
        /// <returns>해석에 성공해 적용했으면 true. 실패하면 기존 값을 그대로 둔다.</returns>
        public static bool TryOverrideForDevelopment(string text)
        {
            GameVersion parsed = Resolve(text, false);
            if (parsed == null)
            {
                return false;
            }
            _version = parsed;
            _resolved = true;
            return true;
        }

        /// <summary>
        /// 버전 문자열을 해석한다. 에디터에서는 `+editor`를 붙인다(이미 메타데이터가 있으면 그대로).
        /// </summary>
        /// <param name="applicationVersion">`Application.version` 값</param>
        /// <param name="isEditor">에디터 재생인지</param>
        /// <returns>해석한 버전, 실패하면 null</returns>
        public static GameVersion Resolve(string applicationVersion, bool isEditor)
        {
            string text = applicationVersion ?? string.Empty;
            if (isEditor && text.IndexOf('+') < 0)
            {
                text += "+" + GameVersion.ChannelEditor;
            }
            return GameVersion.TryParse(text, out GameVersion version) ? version : null;
        }
    }

    /// <summary>Unity 콘솔(그리고 그 뒤의 파일 로그)에 남기는 ILog. 채팅·비밀번호는 호출자가 넘기지 않는 것이 규칙이다.</summary>
    public sealed class UnityLog : ILog
    {
        public static UnityLog Instance { get; } = new UnityLog();

        public void Info(string category, string message) => Debug.Log("[" + category + "] " + message);
        public void Warn(string category, string message) => Debug.LogWarning("[" + category + "] " + message);
        public void Error(string category, string message) => Debug.LogError("[" + category + "] " + message);
    }

    /// <summary>Unity 실시간 시계.</summary>
    public sealed class UnityClock : IClock
    {
        public static UnityClock Instance { get; } = new UnityClock();
        public double RealtimeSeconds => Time.realtimeSinceStartupAsDouble;
    }
}

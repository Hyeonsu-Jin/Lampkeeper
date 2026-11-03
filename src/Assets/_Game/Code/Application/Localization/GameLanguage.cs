// =============================================================
// 파일: GameLanguage.cs
// 역할: 지원 언어(한국어, 영어)와 첫 실행 언어를 정하는 규칙.
// 레이어: application (순수 C#)
// 관련: NFR-034, C-48(첫 실행 언어: Steam 언어가 한국어면 한국어, 그 밖은 영어) / SDD 3.7.9 다국어
// =============================================================
using System;

namespace Game.Application.Localization
{
    /// <summary>게임이 지원하는 언어.</summary>
    public enum GameLanguage
    {
        English = 0,
        Korean = 1,
    }

    /// <summary>언어 선택 규칙. 상태가 없는 정적 클래스.</summary>
    public static class LanguageRules
    {
        /// <summary>Steam 언어 이름 중 한국어를 가리키는 값(SteamApps.GetCurrentGameLanguage()).</summary>
        public const string SteamKoreanName = "koreana";

        /// <summary>운영체제 언어 이름 중 한국어(Unity SystemLanguage.Korean.ToString()).</summary>
        public const string SystemKoreanName = "Korean";

        /// <summary>
        /// [C-48] Steam 언어 이름으로 첫 실행 언어를 고른다. 한국어면 한국어, 그 밖은 영어.
        /// </summary>
        /// <param name="steamLanguage">Steam이 알려 준 언어 이름(null 가능)</param>
        public static GameLanguage FromSteamLanguage(string steamLanguage)
        {
            return string.Equals(steamLanguage, SteamKoreanName, StringComparison.OrdinalIgnoreCase)
                ? GameLanguage.Korean
                : GameLanguage.English;
        }

        /// <summary>
        /// Steam을 아직 쓸 수 없을 때(SCR-001 등) 운영체제 언어 이름으로 임시 언어를 고른다.
        /// </summary>
        /// <param name="systemLanguageName">Unity SystemLanguage.ToString() 값</param>
        public static GameLanguage FromSystemLanguageName(string systemLanguageName)
        {
            return string.Equals(systemLanguageName, SystemKoreanName, StringComparison.OrdinalIgnoreCase)
                ? GameLanguage.Korean
                : GameLanguage.English;
        }
    }
}

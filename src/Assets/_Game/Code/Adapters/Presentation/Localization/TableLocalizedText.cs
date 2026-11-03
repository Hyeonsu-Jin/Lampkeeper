// =============================================================
// 파일: TableLocalizedText.cs
// 역할: TextTable(현재 언어) + TextTable(대체 언어)로 ILocalizedText를 구현한다.
//       M1의 임시 구현. M2-01에서 Unity Localization String Table 구현으로 교체한다(키는 그대로).
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: NFR-034, NFR-044, SDD R-20 / SDD 3.7.9
// =============================================================
using System;
using Game.Application.Localization;
using Game.Application.Ports;

namespace Game.Adapters.Presentation.Localization
{
    /// <summary>표 두 개(현재 언어, 대체 언어)로 글자를 찾는다. 둘 다 없으면 "[!키]"를 돌려준다.</summary>
    public sealed class TableLocalizedText : ILocalizedText
    {
        private readonly TextTable _current;
        private readonly TextTable _fallback;

        /// <param name="current">현재 언어 표</param>
        /// <param name="fallback">현재 언어에 키가 없을 때 쓸 대체 표(null 가능)</param>
        /// <exception cref="ArgumentNullException">current가 null일 때</exception>
        public TableLocalizedText(TextTable current, TextTable fallback)
        {
            _current = current ?? throw new ArgumentNullException(nameof(current));
            _fallback = fallback;
        }

        public string Get(string key)
        {
            if (_current.TryGet(key, out string value))
            {
                return value;
            }
            if (_fallback != null && _fallback.TryGet(key, out value))
            {
                return value;
            }
            // 누락은 화면에서 바로 눈에 띄도록 키를 드러낸다(NFR-044: 누락 0건이 목표).
            return "[!" + key + "]";
        }

        public string Get(string key, params object[] args)
        {
            string format = Get(key);
            if (args == null || args.Length == 0)
            {
                return format;
            }
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                // 번역 문장의 자리 표시({0})가 잘못되어도 게임이 멈추지 않게 원문을 그대로 보인다.
                return format;
            }
        }
    }
}

// =============================================================
// 파일: ILocalizedText.cs
// 역할: 텍스트 키로 화면 글자를 가져오는 포트. 화면 글자는 처음부터 키로만 쓴다.
// 레이어: application (포트). 구현: Adapters.Presentation(M1은 임시 표, M2-01에서 String Table 연결)
// 관련: NFR-034(이름 분리), NFR-044(키 누락 0건), SDD R-20 / SDD 3.7.9 다국어
// =============================================================
namespace Game.Application.Ports
{
    /// <summary>텍스트 키 → 현재 언어의 글자.</summary>
    public interface ILocalizedText
    {
        /// <summary>
        /// 키에 해당하는 글자를 돌려준다.
        /// </summary>
        /// <param name="key">텍스트 키 (예: steam.required.title)</param>
        /// <returns>현재 언어의 글자. 키가 없으면 "[!key]" 형태(눈에 띄게)</returns>
        string Get(string key);

        /// <summary>
        /// 키에 해당하는 글자를 돌려주되 {0}, {1} 자리에 인자를 채운다.
        /// </summary>
        /// <param name="key">텍스트 키</param>
        /// <param name="args">채울 값</param>
        string Get(string key, params object[] args);
    }
}

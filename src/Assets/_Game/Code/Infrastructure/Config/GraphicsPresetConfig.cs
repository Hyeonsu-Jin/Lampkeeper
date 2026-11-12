// =============================================================
// 파일: GraphicsPresetConfig.cs
// 역할: 그래픽 프리셋 4종(낮음/보통/높음/최고)의 세부 값과 기준 프리셋 지정을 담는 ScriptableObject.
//       위치: Assets/_Game/Data/Graphics/GraphicsPresetConfig.asset
// 레이어: infrastructure (Game.Infra.Config). HDRP를 모르는 순수 데이터라 이 어셈블리에 둔다.
// 관련: FR-120, NFR-033(설정값 데이터 분리), DES-109, DES-104 / SDD 3.12.7, 3.8.2 Data/Graphics
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Graphics;
using UnityEngine;

namespace Game.Infra.Config
{
    /// <summary>그래픽 프리셋 애셋.</summary>
    [CreateAssetMenu(fileName = "GraphicsPresetConfig", menuName = "Game/Config/Graphics Preset Config")]
    public sealed class GraphicsPresetConfig : ScriptableObject
    {
        /// <summary>프리셋 목록(번호 순서로 4개).</summary>
        public List<GraphicsPresetData> Presets = new List<GraphicsPresetData>(GraphicsPresetCatalog.CreateDefaults());

        /// <summary>
        /// 검증된 프리셋 모음을 만든다.
        /// </summary>
        /// <exception cref="GraphicsPresetException">데이터가 올바르지 않을 때</exception>
        public GraphicsPresetCatalog ToCatalog()
        {
            // 애셋 값을 복사해 넘겨, 실행 중 코드가 애셋을 바꾸지 못하게 한다.
            var copy = new List<GraphicsPresetData>(Presets.Count);
            foreach (GraphicsPresetData preset in Presets)
            {
                copy.Add(preset?.Clone());
            }
            return new GraphicsPresetCatalog(copy);
        }
    }
}

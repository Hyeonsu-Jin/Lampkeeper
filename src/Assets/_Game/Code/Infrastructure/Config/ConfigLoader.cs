// =============================================================
// 파일: ConfigLoader.cs
// 역할: GameDataConfig 애셋을 읽어 순수 C# 입력으로 바꾸고, 검증·조립을 Application의 ConfigAssembler에 맡긴다.
// 레이어: infrastructure (Game.Infra.Config)
// 관련: NFR-033, NFR-034, DES-104, DES-051 / SDD 3.7.7
// =============================================================
using System;
using System.Collections.Generic;
using Game.Application.Config;

namespace Game.Infra.Config
{
    /// <summary>설정 애셋 로더. 상태가 없는 정적 클래스.</summary>
    public static class ConfigLoader
    {
        /// <summary>
        /// 설정 뿌리 애셋을 읽어 검증된 ConfigProvider를 돌려준다.
        /// </summary>
        /// <param name="root">설정 뿌리 애셋</param>
        /// <returns>읽기 전용으로 쓸 설정(IConfigProvider)</returns>
        /// <exception cref="ArgumentNullException">root가 null일 때</exception>
        /// <exception cref="ConfigLoadException">필수 값 누락·범위 위반·키 중복·참조 오류가 있을 때</exception>
        public static ConfigProvider Load(GameDataConfig root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            // 필수 애셋 연결 확인: 연결이 빠지면 값 검증 전에 분명한 메시지를 준다.
            var preIssues = new List<ConfigIssue>();
            if (root.Balance == null)
            {
                preIssues.Add(ConfigIssue.Error("GameDataConfig.Balance", "BalanceConfig 애셋이 연결되지 않았습니다."));
            }
            if (root.Network == null)
            {
                preIssues.Add(ConfigIssue.Error("GameDataConfig.Network", "NetworkTuningConfig 애셋이 연결되지 않았습니다."));
            }
            if (preIssues.Count > 0)
            {
                throw new ConfigLoadException(preIssues);
            }

            // 콘텐츠 정의 애셋 -> 순수 입력. 빈 칸(삭제된 애셋)은 오류로 알린다.
            var definitions = new List<ContentDefinitionData>(root.Contents.Count);
            var contentIssues = new List<ConfigIssue>();
            for (int i = 0; i < root.Contents.Count; i++)
            {
                ContentDef def = root.Contents[i];
                if (def == null)
                {
                    contentIssues.Add(ConfigIssue.Error("GameDataConfig.Contents[" + i + "]", "비어 있는 칸입니다(삭제된 애셋)."));
                    continue;
                }
                definitions.Add(def.ToData());
            }
            if (contentIssues.Count > 0)
            {
                throw new ConfigLoadException(contentIssues);
            }

            return ConfigAssembler.Assemble(root.Balance.Values, root.Network.Values, definitions);
        }
    }
}

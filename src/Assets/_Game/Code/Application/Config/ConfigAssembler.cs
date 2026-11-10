// =============================================================
// 파일: ConfigAssembler.cs
// 역할: 균형 설정값·네트워크 튜닝·콘텐츠 정의를 검증하고 IConfigProvider(ConfigProvider)로 묶는다.
//       Infra.Config의 ConfigLoader가 ScriptableObject를 읽어 이 클래스에 넘긴다.
// 레이어: application (순수 C#)
// 관련: NFR-033, NFR-034, DES-104, DES-051 / SDD 3.7.7
// =============================================================
using System.Collections.Generic;
using Game.Application.Ports;

namespace Game.Application.Config
{
    /// <summary>검증을 마친 설정. 편집 중인 원본과 분리된 복사본을 들고 있다(읽기 전용으로 쓴다).</summary>
    public sealed class ConfigProvider : IConfigProvider
    {
        public BalanceValues Balance { get; }
        public NetworkTuning Network { get; }
        public ContentCatalog Content { get; }
        public ContentHash ContentHash { get; }

        /// <summary>로드 중 나온 경고(오류는 예외로 끝나므로 여기에 없다).</summary>
        public IReadOnlyList<ConfigIssue> Warnings { get; }

        internal ConfigProvider(
            BalanceValues balance,
            NetworkTuning network,
            ContentCatalog content,
            ContentHash contentHash,
            IReadOnlyList<ConfigIssue> warnings)
        {
            Balance = balance;
            Network = network;
            Content = content;
            ContentHash = contentHash;
            Warnings = warnings;
        }
    }

    /// <summary>설정 조립기.</summary>
    public static class ConfigAssembler
    {
        /// <summary>
        /// 입력을 검증하고 ConfigProvider를 만든다.
        /// </summary>
        /// <param name="balance">균형 설정값(원본은 복사되어 안전하다)</param>
        /// <param name="network">네트워크 튜닝(원본은 복사된다)</param>
        /// <param name="definitions">콘텐츠 정의 목록. 비어 있어도 된다(경고만)</param>
        /// <returns>읽기 전용으로 쓸 설정</returns>
        /// <exception cref="ConfigLoadException">오류가 하나라도 있을 때</exception>
        public static ConfigProvider Assemble(
            BalanceValues balance,
            NetworkTuning network,
            IEnumerable<ContentDefinitionData> definitions)
        {
            var issues = new List<ConfigIssue>();
            ConfigValidator.ValidateBalance(balance, issues);
            ConfigValidator.ValidateNetwork(network, issues);
            ConfigValidator.ValidateMovementAgainstNetwork(balance, network, issues);
            ContentCatalog catalog = ContentCatalogBuilder.Build(definitions, issues);

            if (issues.Exists(i => i.Severity == ConfigIssueSeverity.Error))
            {
                throw new ConfigLoadException(issues);
            }

            if (catalog.Count == 0)
            {
                // M1에는 실제 콘텐츠 정의가 아직 없다(M3부터 추가). 오류가 아니라 알림만 준다.
                issues.Add(ConfigIssue.Warning("Content", "콘텐츠 정의가 0개입니다."));
            }

            BalanceValues balanceCopy = balance.Clone();
            NetworkTuning networkCopy = network.Clone();
            ContentHash hash = ContentHasher.Compute(balanceCopy, catalog);
            return new ConfigProvider(balanceCopy, networkCopy, catalog, hash, issues);
        }
    }
}

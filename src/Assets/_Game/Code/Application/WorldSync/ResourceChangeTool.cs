// =============================================================
// 파일: ResourceChangeTool.cs
// 역할: [개발 도구] 자원 오브젝트 N개(기본 5,000개)가 이미 바뀐 월드 상태를 만든다. NFR-016의 시험 조건(2만 개 중 5,000개 변경)을 재현한다.
// 레이어: application (Game.Application/WorldSync, 순수 C#)
// 관련: FR-009, NFR-016, NFR-018, DES-087 / SDD 7장 M1-10, 3.12.5, 3.12.6
//
// 같은 (월드, 개수, 시드)면 항상 같은 결과다(시험·측정 재현성). 메뉴·실행 옵션 연결은 개발 도구 쪽(Bootstrap/Editor)이 한다.
// =============================================================
using System;
using Game.Application.Gathering;
using Game.Application.Replication;
using Game.Domain.Common;
using Game.Domain.Gathering;
using Game.WorldGen.Common;

namespace Game.Application.WorldSync
{
    /// <summary>자원 N개를 쓰러짐·부서짐·타격 중 상태로 만드는 개발 도구의 핵심 함수.</summary>
    public static class ResourceChangeTool
    {
        /// <summary>NFR-016 시험 조건의 변경 개수.</summary>
        public const int DefaultChangedCount = 5000;

        /// <summary>다섯 번째마다 하나는 아직 서 있는 타격 중(Damaged) 상태로 만든다(단계가 섞이도록).</summary>
        private const int DamagedEvery = 5;

        /// <summary>
        /// 서로 다른 자원 count개를 골라 바뀐 상태로 만든다. 나무는 쓰러짐(방향은 난수), 돌·광석은 부서짐, 일부는 타격 중.
        /// </summary>
        /// <param name="table">서버 자원 표(변경 기록에 남아 델타·스냅샷에 들어간다)</param>
        /// <param name="world">호스트가 만든 월드(자원 종류를 읽는다)</param>
        /// <param name="count">바꿀 개수. 자원 수보다 크면 자원 수로 줄인다</param>
        /// <param name="seed">고르는 순서를 정하는 시드(같으면 같은 결과)</param>
        /// <returns>실제로 바꾼 개수</returns>
        public static int ApplyChanges(ReplicatedTable<ResourceState> table, LocalWorld world, int count, ulong seed)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (world == null) throw new ArgumentNullException(nameof(world));

            int total = world.ResourceCount;
            int take = Math.Max(0, Math.Min(count, total));
            var counts = new HarvestSettings().HitCounts;

            // 부분 Fisher-Yates: 앞에서 take개만 무작위로 뽑는다(중복 없음).
            var order = new int[total];
            for (int i = 0; i < total; i++)
            {
                order[i] = i;
            }
            ulong rng = seed;
            for (int i = 0; i < take; i++)
            {
                int j = i + (int)(SplitMix64.Next(ref rng) % (ulong)(total - i));
                (order[i], order[j]) = (order[j], order[i]);
            }

            int applied = 0;
            for (int i = 0; i < take; i++)
            {
                int index = order[i];
                world.TryGetResource(index, out ResourceKind kind, out _);
                int required = HarvestRules.RequiredHits(kind, counts);
                if (required <= 0)
                {
                    continue; // 종류를 알 수 없는 자원은 건너뛴다
                }

                ResourceState state;
                if (i % DamagedEvery == DamagedEvery - 1 && required > 1)
                {
                    state = new ResourceState((byte)(1 + SplitMix64.Next(ref rng) % (ulong)(required - 1)), ResourcePhase.Damaged, 0);
                }
                else if (kind == ResourceKind.Tree)
                {
                    state = new ResourceState((byte)required, ResourcePhase.Felled, (byte)(SplitMix64.Next(ref rng) % ResourceState.FallDirections));
                }
                else
                {
                    state = new ResourceState((byte)required, ResourcePhase.Broken, 0);
                }
                table.Set((uint)index, state);
                applied++;
            }
            return applied;
        }
    }
}

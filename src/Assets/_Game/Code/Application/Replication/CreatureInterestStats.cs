// =============================================================
// 파일: CreatureInterestStats.cs
// 역할: [개발 도구] 내 위치 기준으로 클라이언트가 받은 생물의 등급별 수와 가장 가까운/먼 거리를 센다.
//       관심 범위 밖 생물이 사라지는지 눈으로 확인하는 화면 표시용.
// 레이어: application (Game.Application.Replication). 순수 C#.
// 관련: FR-008, NFR-013 / SDD 3.11.9
// =============================================================
using System;
using Game.Domain.Common;

namespace Game.Application.Replication
{
    /// <summary>생물 관심 범위 통계. 한 번 만들어 재사용하며 계산 중 할당하지 않는다.</summary>
    public sealed class CreatureInterestStats
    {
        private readonly CreatureInterestSettings _settings;

        public CreatureInterestStats(CreatureInterestSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>받은 생물 수.</summary>
        public int Total { get; private set; }

        /// <summary>Near 등급 수.</summary>
        public int Near { get; private set; }

        /// <summary>Mid 등급 수.</summary>
        public int Mid { get; private set; }

        /// <summary>Far 등급 수(Despawn 여유 안에 남은 것 포함).</summary>
        public int Far { get; private set; }

        /// <summary>관심 범위(+여유)를 넘은 수. 0이어야 정상.</summary>
        public int Out { get; private set; }

        /// <summary>가장 가까운 생물 거리(m). 생물이 없으면 -1.</summary>
        public float NearestMeters { get; private set; }

        /// <summary>가장 먼 생물 거리(m). 생물이 없으면 -1.</summary>
        public float FarthestMeters { get; private set; }

        /// <summary>관심 범위(m).</summary>
        public float RangeMeters => _settings.FarMeters;

        /// <summary>Despawn 여유까지 포함한 한계(m).</summary>
        public float LimitMeters => _settings.FarMeters + _settings.HysteresisMeters;

        /// <summary>
        /// 다시 센다. 거리는 수평(x, z)만 쓴다. 서버의 등급 계산과 같은 기준이다.
        /// </summary>
        public void Recompute(ClientCreatureStore store, float x, float z)
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }
            Total = 0;
            Near = 0;
            Mid = 0;
            Far = 0;
            Out = 0;
            float minSq = float.MaxValue;
            float maxSq = -1f;
            foreach (var pair in store.All)
            {
                Vec3f p = pair.Value.LatestPosition;
                float dx = p.X - x;
                float dz = p.Z - z;
                float d2 = dx * dx + dz * dz;
                Total++;
                if (d2 < minSq) minSq = d2;
                if (d2 > maxSq) maxSq = d2;
                // 받은 생물은 이미 알려진 것이므로 alreadyKnown=true(히스테리시스 포함)
                switch (_settings.Classify(d2, true))
                {
                    case InterestTier.Near: Near++; break;
                    case InterestTier.Mid: Mid++; break;
                    case InterestTier.Far: Far++; break;
                    default: Out++; break;
                }
            }
            NearestMeters = Total == 0 ? -1f : (float)Math.Sqrt(minSq);
            FarthestMeters = Total == 0 ? -1f : (float)Math.Sqrt(maxSq);
        }
    }
}

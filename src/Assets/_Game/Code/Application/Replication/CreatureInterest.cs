// =============================================================
// 파일: CreatureInterest.cs
// 역할: 생물 복제의 관심 범위 등급(거리별 전송 주기) 규칙. 참가자 위치와 생물 위치의 거리로 Near/Mid/Far/Out을 정한다.
// 레이어: application (Game.Application.Replication). 순수 C#.
// 관련: FR-008, NFR-013, DES-082 / SDD 3.11.9
//
// 기본: Near < 50 m 매 틱(30 Hz) / Mid < 150 m 3틱마다(10 Hz) / Far < 관심 범위(300 m) 15틱마다(2 Hz) / 그 밖은 보내지 않음.
// 이미 보내 준 생물은 범위를 약간(히스테리시스) 더 벗어나야 Despawn한다. 경계에서 서성이는 생물이 Spawn/Despawn을 반복해 대역폭을 쓰는 것을 막는다.
// =============================================================
using System;
using Game.Application.Config;

namespace Game.Application.Replication
{
    /// <summary>관심 범위 등급.</summary>
    public enum InterestTier : byte
    {
        Near = 0,
        Mid = 1,
        Far = 2,
        /// <summary>범위 밖. 보내지 않는다.</summary>
        Out = 3,
    }

    /// <summary>관심 범위 설정과 등급 판정.</summary>
    public sealed class CreatureInterestSettings
    {
        /// <summary>Despawn 전에 관심 범위를 더 벗어나야 하는 거리(m).</summary>
        public const float DefaultHysteresisMeters = 10f;

        /// <summary>Near 거리(m, 미만).</summary>
        public float NearMeters { get; set; } = 50f;

        /// <summary>Mid 거리(m, 미만).</summary>
        public float MidMeters { get; set; } = 150f;

        /// <summary>Far 거리 = 관심 범위(m, 미만).</summary>
        public float FarMeters { get; set; } = 300f;

        /// <summary>Despawn 히스테리시스(m).</summary>
        public float HysteresisMeters { get; set; } = DefaultHysteresisMeters;

        /// <summary>Near 전송 주기(틱).</summary>
        public int NearIntervalTicks { get; set; } = 1;

        /// <summary>Mid 전송 주기(틱).</summary>
        public int MidIntervalTicks { get; set; } = 3;

        /// <summary>Far 전송 주기(틱).</summary>
        public int FarIntervalTicks { get; set; } = 15;

        /// <summary>설정값(Hz, 관심 범위)에서 만든다. 주기는 서버 틱 속도를 Hz로 나눈 값(최소 1틱)이다.</summary>
        public static CreatureInterestSettings FromConfig(NetworkTuning tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }
            int tick = Math.Max(1, tuning.ServerTickRateHz);
            return new CreatureInterestSettings
            {
                FarMeters = tuning.InterestRangeMeters,
                NearIntervalTicks = IntervalOf(tick, tuning.CreatureSnapshotNearHz),
                MidIntervalTicks = IntervalOf(tick, tuning.CreatureSnapshotMidHz),
                FarIntervalTicks = IntervalOf(tick, tuning.CreatureSnapshotFarHz),
            };
        }

        private static int IntervalOf(int tickRate, int hz) => Math.Max(1, tickRate / Math.Max(1, hz));

        /// <summary>거리(제곱)로 등급을 정한다.</summary>
        /// <param name="distanceSquared">참가자-생물 거리 제곱(m²)</param>
        /// <param name="alreadyKnown">이미 Spawn을 보낸 생물이면 true(Far 경계를 히스테리시스만큼 넓힌다)</param>
        public InterestTier Classify(float distanceSquared, bool alreadyKnown)
        {
            if (distanceSquared < NearMeters * NearMeters)
            {
                return InterestTier.Near;
            }
            if (distanceSquared < MidMeters * MidMeters)
            {
                return InterestTier.Mid;
            }
            float far = alreadyKnown ? FarMeters + HysteresisMeters : FarMeters;
            return distanceSquared < far * far ? InterestTier.Far : InterestTier.Out;
        }

        /// <summary>등급별 전송 주기(틱). Out은 0.</summary>
        public int IntervalOf(InterestTier tier)
        {
            switch (tier)
            {
                case InterestTier.Near: return NearIntervalTicks;
                case InterestTier.Mid: return MidIntervalTicks;
                case InterestTier.Far: return FarIntervalTicks;
                default: return 0;
            }
        }
    }
}

// =============================================================
// 파일: ResourceVisualRules.cs
// 역할: 자원 상태(ResourceState) → 화면에 어떻게 보일지(보임 여부, 쓰러진 각도, 크기 배율, 쓰러지는 방향)를 정하는 순수 규칙.
// 레이어: application (Game.Application/WorldSync, 순수 C#)
// 관련: FR-009, FR-047(쓰러짐은 모두에게 같은 방향), NFR-016, DES-087 / SDD 3.10.4, 3.9.3
//
// 화면 코드(ResourceViewStreamer)는 이 값을 받아 그리기만 한다. 규칙을 순수하게 둬서 Unity 없이 시험한다.
// =============================================================
using Game.Domain.Gathering;

namespace Game.Application.WorldSync
{
    /// <summary>자원 하나가 지금 어떻게 보여야 하는지.</summary>
    public readonly struct ResourceVisual
    {
        /// <summary>그릴지 여부. 부서지거나 거둔 자원은 그리지 않는다.</summary>
        public bool Visible { get; }

        /// <summary>쓰러진 각도(도, 0 = 서 있음, 90 = 완전히 누움).</summary>
        public float FallDegrees { get; }

        /// <summary>쓰러지는 방향(수평 각도, 도. 0 = +Z, 시계 방향). 서 있으면 0.</summary>
        public float FallYawDegrees { get; }

        /// <summary>크기 배율(타격을 받으면 조금 작아 보이게).</summary>
        public float ScaleFactor { get; }

        public ResourceVisual(bool visible, float fallDegrees, float fallYawDegrees, float scaleFactor)
        {
            Visible = visible;
            FallDegrees = fallDegrees;
            FallYawDegrees = fallYawDegrees;
            ScaleFactor = scaleFactor;
        }
    }

    /// <summary>상태 → 표시 변환 규칙.</summary>
    public static class ResourceVisualRules
    {
        /// <summary>완전히 쓰러진 나무의 각도(도).</summary>
        public const float FelledDegrees = 90f;

        /// <summary>타격을 받아 서 있는(Damaged) 자원의 크기 배율. 맞은 것이 눈에 보이게 한다.</summary>
        public const float DamagedScaleFactor = 0.92f;

        private const float DegreesPerFallStep = 360f / ResourceState.FallDirections;

        /// <summary>종류와 상태로 표시 값을 계산한다.</summary>
        public static ResourceVisual Compute(ResourceKind kind, in ResourceState state)
        {
            switch (state.Phase)
            {
                case ResourcePhase.Pristine:
                    return new ResourceVisual(true, 0f, 0f, 1f);
                case ResourcePhase.Damaged:
                    return new ResourceVisual(true, 0f, 0f, DamagedScaleFactor);
                case ResourcePhase.Felled:
                    // 나무만 쓰러져 남는다. 그 밖의 종류가 쓰러짐 상태로 오면(잘못된 데이터) 그리지 않는다.
                    return kind == ResourceKind.Tree
                        ? new ResourceVisual(true, FelledDegrees, FallYawOf(state.FallDir), 1f)
                        : new ResourceVisual(false, 0f, 0f, 1f);
                default:
                    // Broken, Harvested: 사라진다.
                    return new ResourceVisual(false, 0f, 0f, 1f);
            }
        }

        /// <summary>방향 단계(0~15)를 수평 각도(도)로 바꾼다.</summary>
        public static float FallYawOf(byte fallDir) => (fallDir % ResourceState.FallDirections) * DegreesPerFallStep;
    }
}

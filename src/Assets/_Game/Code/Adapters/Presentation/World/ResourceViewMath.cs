// =============================================================
// 파일: ResourceViewMath.cs
// 역할: 자원 오브젝트의 화면 자세(위치·회전·크기)를 계산하는 순수 계산 모음. 근거리 오브젝트와 원거리 인스턴싱이 같은 계산을 쓴다.
// 레이어: adapters (Game.Adapters.Presentation/World)
// 관련: FR-009, FR-047, NFR-016, DES-087 / SDD 3.9.3, 3.10.4
//
// 모양 기준점은 자원의 바닥 위치다. 나무는 바닥을 축으로 넘어지고, 모든 참가자에게 같은 방향으로 보인다(방향은 상태에 들어 있음).
// =============================================================
using UnityEngine;

namespace Game.Adapters.Presentation.World
{
    /// <summary>임시 기본 도형 모양 하나(M1-10). 정식 모델이 생기면 교체한다.</summary>
    public readonly struct ResourceShape
    {
        /// <summary>기본 도형 종류.</summary>
        public PrimitiveType Primitive { get; }

        /// <summary>도형 크기 배율(기본 도형 mesh 기준).</summary>
        public Vector3 Scale { get; }

        /// <summary>바닥에서 도형 중심까지 높이(m, 크기 배율 1일 때).</summary>
        public float CenterHeight { get; }

        public ResourceShape(PrimitiveType primitive, Vector3 scale, float centerHeight)
        {
            Primitive = primitive;
            Scale = scale;
            CenterHeight = centerHeight;
        }
    }

    /// <summary>자원 모양 표와 자세 계산.</summary>
    public static class ResourceViewMath
    {
        /// <summary>쓰러져 누운 나무가 땅에 반쯤 묻히지 않게 들어 올리는 높이(m).</summary>
        private const float LyingLiftMeters = 0.35f;

        private const float Q8One = 256f;
        private const float YawUnitsPerTurn = 65536f;
        private const float DegreesPerTurn = 360f;

        /// <summary>종류별 임시 모양. 나무=원기둥(높이 7 m), 돌=납작한 구, 철광석·석탄=상자. 번호는 ResourceKind 값과 같다.</summary>
        public static ResourceShape ShapeOf(int kindValue)
        {
            switch (kindValue)
            {
                case 1: return new ResourceShape(PrimitiveType.Cylinder, new Vector3(0.8f, 3.5f, 0.8f), 3.5f);
                case 2: return new ResourceShape(PrimitiveType.Sphere, new Vector3(1.6f, 1.1f, 1.6f), 0.55f);
                case 3: return new ResourceShape(PrimitiveType.Cube, new Vector3(1.4f, 1.4f, 1.4f), 0.7f);
                case 4: return new ResourceShape(PrimitiveType.Cube, new Vector3(1.2f, 1.0f, 1.2f), 0.5f);
                default: return new ResourceShape(PrimitiveType.Cube, Vector3.one, 0.5f);
            }
        }

        /// <summary>생성 표의 크기(Q8, 256 = 1.0)를 배율로 바꾼다. 0이면 1로 본다.</summary>
        public static float SizeFactor(ushort scaleQ8) => scaleQ8 == 0 ? 1f : scaleQ8 / Q8One;

        /// <summary>생성 표의 방향(1/65536 바퀴)을 도로 바꾼다.</summary>
        public static float YawDegrees(ushort yaw) => yaw * DegreesPerTurn / YawUnitsPerTurn;

        /// <summary>
        /// 자세를 계산한다.
        /// </summary>
        /// <param name="basePosition">바닥 위치(m)</param>
        /// <param name="yawDegrees">서 있을 때 바라보는 방향(도)</param>
        /// <param name="fallDegrees">쓰러진 각도(0 = 서 있음, 90 = 누움)</param>
        /// <param name="fallYawDegrees">쓰러지는 수평 방향(도, 0 = +Z, 시계 방향)</param>
        /// <param name="centerHeight">크기 배율을 곱한 중심 높이(m)</param>
        /// <param name="position">도형 중심의 월드 위치</param>
        /// <param name="rotation">도형 회전</param>
        public static void ComputePose(Vector3 basePosition, float yawDegrees, float fallDegrees, float fallYawDegrees,
            float centerHeight, out Vector3 position, out Quaternion rotation)
        {
            Quaternion tilt = Quaternion.identity;
            float lift = 0f;
            if (fallDegrees > 0.01f)
            {
                // 「위」 방향을 쓰러지는 방향 쪽으로 fallDegrees만큼 눕힌 방향으로 돌린다(좌표계 손 방향에 상관없이 맞다).
                float yawRad = fallYawDegrees * Mathf.Deg2Rad;
                float tiltRad = fallDegrees * Mathf.Deg2Rad;
                var fallDirection = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
                Vector3 tiltedUp = Mathf.Cos(tiltRad) * Vector3.up + Mathf.Sin(tiltRad) * fallDirection;
                tilt = Quaternion.FromToRotation(Vector3.up, tiltedUp);
                lift = Mathf.Sin(tiltRad) * LyingLiftMeters;
            }
            rotation = tilt * Quaternion.Euler(0f, yawDegrees, 0f);
            position = basePosition + rotation * new Vector3(0f, centerHeight, 0f) + new Vector3(0f, lift, 0f);
        }
    }
}

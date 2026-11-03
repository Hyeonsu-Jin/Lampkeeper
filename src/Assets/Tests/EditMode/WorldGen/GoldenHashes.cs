// =============================================================
// 파일: GoldenHashes.cs
// 역할: 골든 해시 값 모음. V0 생성기 결과 요약값을 고정해 둔 기준표다.
//       Mono(에디터)와 IL2CPP(플레이어)에서 -genhash로 만든 값이 이 표와 같아야 한다.
// 레이어: test (EditMode, Game.Tests.WorldGen)
// 관련: NFR-053, FR-169, DES-113 / SDD M1-04
//
// 값을 바꾸는 경우: 생성 규칙(DetMath, V0 생성기, WorldHasher 순서)을 일부러 바꿨을 때뿐이다.
// 그때는 변경 이유를 커밋 메시지에 적고, 이 표와 -genhash 비교 결과(Mono/IL2CPP)를 같이 갱신한다.
// 이 값이 우연히 바뀌면 결정론이 깨진 것이므로 값이 아니라 코드를 고친다.
// =============================================================
namespace Game.Tests.WorldGen
{
    /// <summary>골든 해시 기준표.</summary>
    public static class GoldenHashes
    {
        /// <summary>골든 시드 글자들(시드 목록 파일 Assets/Tests/EditMode/WorldGen/golden_seeds.txt와 같다).</summary>
        public static readonly string[] SeedTexts = { "golden-seed-1", "KkeojinHaneul", "K7QM2RTX9B" };

        /// <summary>실제 규모(Full, 3072 m) 요약값. SeedTexts와 같은 순서.</summary>
        public static readonly ulong[] Full =
        {
            0xf4797ecf0e9e888aUL,
            0x504bf4e1d7a60525UL,
            0x7710417e5a3cd6eaUL,
        };

        /// <summary>작은 규모(Small, 1024 m) 요약값. SeedTexts와 같은 순서.</summary>
        public static readonly ulong[] Small =
        {
            0xc5d397884b04b735UL,
            0x25d439e67bd2432dUL,
            0x89cd4ff8e817b1b7UL,
        };
    }
}

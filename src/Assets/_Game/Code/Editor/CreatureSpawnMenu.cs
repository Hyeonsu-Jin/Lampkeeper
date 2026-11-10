// =============================================================
// 파일: CreatureSpawnMenu.cs
// 역할: [개발 도구] 에디터에서 재생 중 호스트가 시험용 생물 60체를 만드는 메뉴(M1-11 부하 시험 조건 재현).
// 레이어: infrastructure (Game.Editor, 편집기 전용)
// 관련: NFR-011, NFR-017, DES-062, DES-082, DES-088 / SDD 7장 M1-11
//
// 재생 중 호스트(-host 또는 로비에서 방 열기)로 게임에 들어간 뒤 실행한다. 호스트가 아니면 아무것도 바꾸지 않고 안내만 남긴다.
// 실행 옵션 -testcreatures N 은 개발 빌드에서 같은 일을 호스트 시작 때 한다. 메뉴를 여러 번 누르면 그만큼 더 만들어진다.
// =============================================================
using Game.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>시험용 생물 개발 메뉴.</summary>
    public static class CreatureSpawnMenu
    {
        private const string MenuPath = "Game/Dev/AI/Spawn 60 Test Creatures (Host, Play Mode)";
        private const int DefaultCount = 60;

        /// <summary>재생 중 호스트에서 시험용 생물 60체를 만든다.</summary>
        [MenuItem(MenuPath)]
        public static void Spawn60()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[TestCreatures] 재생 중에 실행하세요(호스트로 게임에 들어간 뒤).");
                return;
            }
            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogWarning("[TestCreatures] 부트 오브젝트를 찾지 못했습니다.");
                return;
            }
            int made = bootstrap.DevSpawnTestCreatures(DefaultCount);
            if (made < 0)
            {
                Debug.LogWarning("[TestCreatures] 호스트로 게임 중이 아니라 만들지 못했습니다.");
                return;
            }
            Debug.Log("[TestCreatures] 시험용 생물 " + made + "체를 만들었습니다.");
        }

        /// <summary>재생 중일 때만 메뉴를 켠다.</summary>
        [MenuItem(MenuPath, true)]
        private static bool Validate() => EditorApplication.isPlaying;
    }
}

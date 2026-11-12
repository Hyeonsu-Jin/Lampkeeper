// =============================================================
// 파일: ResourceChangeMenu.cs
// 역할: [개발 도구] 에디터에서 재생 중 호스트가 「자원 5,000개를 바뀐 상태로」 만드는 메뉴(NFR-016 시험 조건 재현).
// 레이어: infrastructure (Game.Editor, 편집기 전용)
// 관련: FR-009, NFR-016, DES-087 / SDD 7장 M1-10
//
// 재생 중 호스트(-host 또는 로비에서 방 열기)로 게임에 들어간 뒤 실행한다. 호스트가 아니면 아무것도 바꾸지 않고 안내만 남긴다.
// 같은 개수·같은 월드면 늘 같은 자원이 바뀐다(시험 재현성). 실행 옵션 -resourcechanges N 은 개발 빌드에서 같은 일을 시작 때 한다.
// =============================================================
using Game.Application.WorldSync;
using Game.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>자원 변경 개발 메뉴.</summary>
    public static class ResourceChangeMenu
    {
        private const string MenuPath = "Game/Dev/Resources/Apply 5000 Changes (Host, Play Mode)";

        /// <summary>재생 중 호스트에서 자원 5,000개를 바꾼다.</summary>
        [MenuItem(MenuPath)]
        public static void Apply5000()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ResourceChange] 재생 중에 실행하세요(호스트로 게임에 들어간 뒤).");
                return;
            }
            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogWarning("[ResourceChange] 부트 오브젝트를 찾지 못했습니다.");
                return;
            }
            int applied = bootstrap.DevApplyResourceChanges(ResourceChangeTool.DefaultChangedCount);
            if (applied < 0)
            {
                Debug.LogWarning("[ResourceChange] 호스트로 게임 중이 아니라 바꾸지 못했습니다.");
                return;
            }
            Debug.Log("[ResourceChange] 자원 " + applied + "개를 바뀐 상태로 만들었습니다.");
        }

        /// <summary>재생 중일 때만 메뉴를 켠다.</summary>
        [MenuItem(MenuPath, true)]
        private static bool Validate() => EditorApplication.isPlaying;
    }
}

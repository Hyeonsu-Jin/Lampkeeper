// =============================================================
// 파일: ProjectConfigurator.cs
// 역할: 프로젝트 설정(직렬화 Force Text, .meta 표시, 스크립팅 백엔드 IL2CPP, 제품 정보)을 코드로 고정한다.
//       여러 번 실행해도 같은 결과가 나온다(멱등).
// 레이어: infrastructure (Game.Editor, 편집기 전용)
// 관련: NFR-027~029, NFR-043, DES-112 / SDD M1-01 (1)(4)(5)
//
// 실행: 메뉴 Game/Setup/Apply Project Settings, 또는 배치 모드
//   Unity.exe -batchmode -quit -projectPath src -executeMethod Game.Editor.ProjectConfigurator.Apply
// =============================================================
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.VersionControl;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>프로젝트 설정 적용기.</summary>
    public static class ProjectConfigurator
    {
        /// <summary>회사 이름(임시). 저장 폴더 이름에 쓰인다.</summary>
        public const string CompanyName = "Lampkeeper";

        /// <summary>제품 이름(임시 명칭, NFR-034). 실행 파일 이름과 저장 경로에 쓰인다.</summary>
        public const string ProductName = "KkeojinHaneul";

        /// <summary>대표 버전의 앞 두 자리. 마지막 자리는 빌드 번호(BuildCommands)가 채운다.</summary>
        public const string VersionPrefix = "0.1";

        /// <summary>.meta 파일을 보이는 파일로 두는 모드 이름(Unity 버전 관리 설정 값).</summary>
        public const string VisibleMetaFilesMode = "Visible Meta Files";

        /// <summary>프로젝트 설정을 적용한다.</summary>
        [MenuItem("Game/Setup/Apply Project Settings")]
        public static void Apply()
        {
            // [NFR-027] 씬·프리팹을 텍스트로 저장해 diff·병합(Unity Smart Merge)이 가능하게 한다.
            EditorSettings.serializationMode = SerializationMode.ForceText;
            // [NFR-027] .meta 파일을 보이는 파일로 두고 저장소에 함께 올린다.
            VersionControlSettings.mode = VisibleMetaFilesMode;

            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || !PlayerSettings.bundleVersion.StartsWith(VersionPrefix + ".", System.StringComparison.Ordinal))
            {
                PlayerSettings.bundleVersion = VersionPrefix + ".0";
            }

            // [D-03] 출시·측정 빌드는 IL2CPP, 에디터는 항상 Mono. 결정론 시험은 둘 다 통과해야 한다(D-08).
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);

            // HDRP는 선형 색 공간이 필요하다.
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // 호스트 창이 뒤로 가도 서버가 멈추면 안 된다(8인 세션).
            PlayerSettings.runInBackground = true;

            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] 프로젝트 설정을 적용했습니다(Force Text, IL2CPP, 선형 색 공간).");
        }
    }
}

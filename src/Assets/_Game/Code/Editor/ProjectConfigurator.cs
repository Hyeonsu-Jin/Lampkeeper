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

        /// <summary>`bundleVersion`이 비어 있을 때 넣는 처음 버전(SemVer 핵심, 출시 전 0.y.z). 그 뒤의 값은 사람이 정한다. [NFR-058]</summary>
        public const string InitialVersion = "0.1.0";

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
            // [NFR-058] 게임 버전의 원본은 bundleVersion 하나이고 사람이 정한다. 비어 있을 때만 처음 값을 넣고, 그 밖에는 바꾸지 않는다.
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion))
            {
                PlayerSettings.bundleVersion = InitialVersion;
            }
            else if (!Game.Domain.Common.GameVersion.TryParse(PlayerSettings.bundleVersion, out Game.Domain.Common.GameVersion parsed) || parsed.BuildMetadata.Length > 0)
            {
                Debug.LogError("[Setup] bundleVersion '" + PlayerSettings.bundleVersion + "' 형식이 올바르지 않습니다(MAJOR.MINOR.PATCH[-사전릴리스], '+' 없음).");
            }

            // [D-03] 출시·측정 빌드는 IL2CPP, 에디터는 항상 Mono. 결정론 시험은 둘 다 통과해야 한다(D-08).
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);

            // HDRP는 선형 색 공간이 필요하다.
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // [NFR-021] GPU·메인 스레드 프레임 시간(FrameTimingManager)을 읽으려면 켜야 한다(개발용 성능 표시, DES-108).
            PlayerSettings.enableFrameTimingStats = true;

            // 호스트 창이 뒤로 가도 서버가 멈추면 안 된다(8인 세션).
            PlayerSettings.runInBackground = true;

            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] 프로젝트 설정을 적용했습니다(Force Text, IL2CPP, 선형 색 공간).");
        }
    }
}

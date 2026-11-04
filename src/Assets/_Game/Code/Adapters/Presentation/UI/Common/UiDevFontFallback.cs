// =============================================================
// 파일: UiDevFontFallback.cs
// 역할: 개발 전용. Pretendard 글꼴 파일이 프로젝트에 들어오기 전까지, 실행 중에 Windows 의 한글 글꼴(맑은 고딕)과
//       기호 글꼴(Segoe UI Symbol)로 동적 TMP 글꼴을 만들어 전역 대체 글꼴에 붙인다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: NFR-044, R-37 / SDD 3.7.10 (8)
//
// 글꼴 파일을 프로젝트에 복사하거나 빌드에 넣지 않는다(재배포 라이선스). 운영체제에 이미 설치된 글꼴을 이름으로 불러
// 메모리에서만 쓴다. 테마의 기본 글꼴이 이미 한글을 가지고 있으면(Pretendard 가 들어오면) 아무것도 하지 않는다.
// =============================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>개발용 OS 글꼴 대체 설치기.</summary>
    public static class UiDevFontFallback
    {
        private const char HangulProbe = '가';
        private const string KoreanFamily = "Malgun Gothic";
        private const string SymbolFamily = "Segoe UI Symbol";
        private const string RegularStyle = "Regular";

        private static bool _installed;
        private static readonly List<TMP_FontAsset> Added = new List<TMP_FontAsset>();

        /// <summary>대체 글꼴 설치를 이미 시도했는지(시험·진단용).</summary>
        public static bool IsInstalled => _installed;

        /// <summary>필요하면 OS 글꼴 대체를 설치한다. 이미 살아 있는 대체 글꼴이 있으면 다시 만들지 않고, 파괴됐으면 정리 후 다시 만든다.</summary>
        /// <param name="primary">테마의 기본 글꼴(한글이 이미 있으면 설치하지 않는다)</param>
        public static void EnsureInstalled(TMP_FontAsset primary)
        {
            // 도메인 리로드·재생 모드 전환·EditMode 시험 뒤에 메모리 전용 글꼴이나 그 재질이 파괴될 수 있으므로 매번 먼저 정리한다.
            PruneDeadFonts();

            if (_installed && Added.Count > 0)
            {
                return;
            }
            if (_installed && Added.Count == 0 && primary != null && primary.HasCharacter(HangulProbe, true))
            {
                return;
            }
            _installed = true;

            if (primary != null && primary.HasCharacter(HangulProbe, true))
            {
                return;
            }

            AddFallback(KoreanFamily);
            AddFallback(SymbolFamily);
        }

        /// <summary>글꼴 또는 그 재질·아틀라스가 파괴됐는지 본다.</summary>
        private static bool IsDead(TMP_FontAsset font)
        {
            if (font == null)
            {
                return true;
            }
            try
            {
                if (font.material == null)
                {
                    return true;
                }
                Texture2D[] atlases = font.atlasTextures;
                if (atlases == null || atlases.Length == 0 || atlases[0] == null)
                {
                    return true;
                }
            }
            catch (MissingReferenceException)
            {
                return true;
            }
            return false;
        }

        /// <summary>전역 대체 목록과 내 목록에서 파괴된(또는 null) 글꼴을 빼고, 하나라도 죽었으면 남은 내 글꼴도 걷어 새로 만들게 한다.</summary>
        private static void PruneDeadFonts()
        {
            List<TMP_FontAsset> global = TMP_Settings.fallbackFontAssets;
            bool anyDead = false;
            for (int i = Added.Count - 1; i >= 0; i--)
            {
                if (IsDead(Added[i]))
                {
                    anyDead = true;
                    break;
                }
            }
            if (anyDead)
            {
                Uninstall(); // 살아 있는 짝도 함께 걷고 처음부터 다시 만든다.
            }
            if (global != null)
            {
                for (int i = global.Count - 1; i >= 0; i--)
                {
                    TMP_FontAsset f = global[i];
                    // 개발용 이름표가 붙은 잔여물이거나 파괴된 항목은 제거한다(이전 실행에서 남은 참조).
                    if (f == null || (f.name != null && f.name.EndsWith("(dev fallback)") && !Added.Contains(f)) || (!Added.Contains(f) && IsDead(f)))
                    {
                        global.RemoveAt(i);
                    }
                }
            }
        }

        private static void AddFallback(string family)
        {
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(family, RegularStyle);
            if (font == null)
            {
                Debug.LogWarning("[UI] 개발용 대체 글꼴 '" + family + "' 을(를) 운영체제에서 찾지 못했습니다. 해당 글자는 □로 보일 수 있습니다.");
                return;
            }
            font.name = family + " (dev fallback)";
            // 글꼴·재질·아틀라스 모두 씬 전환 때 정리되지 않고 파일로도 저장되지 않게 한다.
            font.hideFlags = HideFlags.HideAndDontSave;
            if (font.material != null)
            {
                font.material.hideFlags = HideFlags.HideAndDontSave;
            }
            if (font.atlasTextures != null)
            {
                foreach (Texture2D atlas in font.atlasTextures)
                {
                    if (atlas != null)
                    {
                        atlas.hideFlags = HideFlags.HideAndDontSave;
                    }
                }
            }
            if (TMP_Settings.fallbackFontAssets != null)
            {
                TMP_Settings.fallbackFontAssets.Add(font);
                Added.Add(font);
            }
            Debug.Log("[UI] 개발 전용 대체 글꼴을 붙였습니다: " + family + " (Pretendard 가 들어오면 사라집니다).");
        }

        /// <summary>붙였던 대체 글꼴을 전역 목록에서 빼고 지운다(TMP Settings 애셋에 메모리 전용 참조가 남지 않게).</summary>
        public static void Uninstall()
        {
            foreach (TMP_FontAsset font in Added)
            {
                if (TMP_Settings.fallbackFontAssets != null)
                {
                    TMP_Settings.fallbackFontAssets.Remove(font);
                }
                if (font != null)
                {
                    Texture2D[] atlases = null;
                    Material mat = null;
                    try { atlases = font.atlasTextures; mat = font.material; } catch (MissingReferenceException) { }
                    if (atlases != null)
                    {
                        foreach (Texture2D atlas in atlases)
                        {
                            if (atlas != null) { Object.DestroyImmediate(atlas); }
                        }
                    }
                    if (mat != null) { Object.DestroyImmediate(mat); }
                    Object.DestroyImmediate(font);
                }
            }
            Added.Clear();
            _installed = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            // 도메인 리로드를 끈 상태에서도 이전 실행의 잔여를 전역 목록에서 걷어 낸다.
            Uninstall();
        }

#if UNITY_EDITOR
        // 에디터에서는 편집 모드에서도 한글이 보이도록 같은 대체를 쓰되, 스크립트를 다시 불러오거나 에디터를 닫기 전에 반드시 걷어 낸다.
        [UnityEditor.InitializeOnLoadMethod]
        private static void HookEditorCleanup()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Uninstall;
            UnityEditor.EditorApplication.quitting += Uninstall;
        }
#endif
    }
}

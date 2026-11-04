// =============================================================
// 파일: ThemedGraphic.cs
// 역할: Image·RawImage·사용자 지정 Graphic 에 붙어 색 토큰을 적용한다. 색 값은 테마 애셋에만 있다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: NFR-056, FR-129, DES-122 / SDD 3.7.10 (1)·(3)
//
// 실제 Graphic.color 는 이 컴포넌트가 토큰에서 계산해 넣은 결과다(토큰 × alpha). 인스펙터에서 직접 바꾸면 규칙 검사가 실패한다.
// =============================================================
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>테마 색 적용 그래픽.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public sealed class ThemedGraphic : MonoBehaviour
    {
        [SerializeField] private UiTheme _theme;
        [Tooltip("비워 두면 흰색(스프라이트 원래 색).")]
        [UiToken(UiTokenKind.Color)]
        [SerializeField] private string _colorToken = string.Empty;
        [Range(0f, 1f)]
        [Tooltip("불투명도(색 값이 아님). 토큰 색의 알파에 곱해진다.")]
        [SerializeField] private float _alpha = 1f;

        private Graphic _graphic;

        public UiTheme Theme => _theme;
        public string ColorToken => _colorToken;
        public float Alpha => _alpha;

        /// <summary>같은 오브젝트의 Graphic.</summary>
        public Graphic Graphic
        {
            get
            {
                if (_graphic == null)
                {
                    _graphic = GetComponent<Graphic>();
                }
                return _graphic;
            }
        }

        private void OnEnable()
        {
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    Apply();
                }
            };
        }
#endif

        /// <summary>토큰 색 × 알파를 Graphic 에 쓴다.</summary>
        public void Apply()
        {
            Graphic graphic = Graphic;
            if (graphic == null)
            {
                return;
            }
            Color target = ResolveColor();
            if (graphic.color != target)
            {
                graphic.color = target;
            }
        }

        /// <summary>토큰과 알파로 계산한 색을 돌려준다(규칙 검사도 이 값과 비교한다).</summary>
        public Color ResolveColor()
        {
            Color color = Color.white;
            if (!string.IsNullOrEmpty(_colorToken))
            {
                if (_theme != null && _theme.TryGetColor(_colorToken, out Color token))
                {
                    color = token;
                }
                else
                {
                    Debug.LogError("[UI] 색 토큰을 테마에서 찾지 못했습니다: '" + _colorToken + "' (" + name + ")", this);
                }
            }
            color.a *= _alpha;
            return color;
        }

        /// <summary>색 토큰을 바꾼다. 상태에 따라 색이 바뀌는 그래픽에 쓴다.</summary>
        public void SetColorToken(string token)
        {
            if (_colorToken == token)
            {
                return;
            }
            _colorToken = token ?? string.Empty;
            Apply();
        }
    }
}

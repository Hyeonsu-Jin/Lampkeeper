// =============================================================
// 파일: ThemedText.cs
// 역할: TextMeshProUGUI에 붙어 테마 토큰(크기·색·굵기)과 텍스트 키를 적용한다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: NFR-034, NFR-056, FR-129, DES-122 / SDD 3.7.10 (3)·(7)·(9)
//
// 규칙(프리팹 규칙 검사가 확인): 모든 TMP 글자에 붙는다. richText 꺼짐, 자동 크기 꺼짐, 레이캐스트 꺼짐을 강제한다.
// 글자 내용은 텍스트 키가 있으면 키로, 없으면 IsDynamic 으로 표시하고 코드가 SetDynamicText 로 넣는다.
// =============================================================
using TMPro;
using UnityEngine;

namespace Game.Adapters.Presentation.UI.Common
{
    /// <summary>테마 적용 글자.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class ThemedText : MonoBehaviour
    {
        [SerializeField] private UiTheme _theme;
        [UiToken(UiTokenKind.Size)]
        [SerializeField] private string _sizeToken = UiTokens.FsBody;
        [UiToken(UiTokenKind.Color)]
        [SerializeField] private string _colorToken = UiTokens.ColorText;
        [SerializeField] private UiFontWeight _weight = UiFontWeight.Regular;
        [Tooltip("텍스트 키(strings.ko.txt). 비워 두면 아래 IsDynamic 을 켜야 한다.")]
        [SerializeField] private string _textKey = string.Empty;
        [Tooltip("글자 내용을 코드가 넣는다(키가 없을 때 체크).")]
        [SerializeField] private bool _isDynamic;

        private TextMeshProUGUI _tmp;

        public UiTheme Theme => _theme;
        public string SizeToken => _sizeToken;
        public string ColorToken => _colorToken;
        public UiFontWeight Weight => _weight;
        public string TextKey => _textKey;
        public bool IsDynamic => _isDynamic;

        /// <summary>같은 오브젝트의 TMP 글자.</summary>
        public TextMeshProUGUI Tmp
        {
            get
            {
                if (_tmp == null)
                {
                    _tmp = GetComponent<TextMeshProUGUI>();
                }
                return _tmp;
            }
        }

        private void OnEnable()
        {
            UiContext.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            UiContext.Changed -= Apply;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 인스펙터에서 값을 고치는 즉시 결과를 보여 준다(OnValidate 안에서는 오브젝트를 바꾸지 않도록 한 프레임 미룬다).
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    Apply();
                }
            };
        }
#endif

        /// <summary>테마 값(크기·색·글꼴)과 글자를 적용한다. 값이 같으면 아무것도 쓰지 않는다.</summary>
        public void Apply()
        {
            if (_theme == null)
            {
                return;
            }
            TextMeshProUGUI tmp = Tmp;
            if (tmp == null)
            {
                return;
            }

            // 강제 규칙: 리치 텍스트·자동 크기·레이캐스트 끔
            if (tmp.richText) { tmp.richText = false; }
            if (tmp.enableAutoSizing) { tmp.enableAutoSizing = false; }
            if (tmp.raycastTarget) { tmp.raycastTarget = false; }
            // 코드가 넣는 동적 글자(Steam 이름 등)의 제어 문자 해석을 끈다.
            if (_isDynamic && tmp.parseCtrlCharacters) { tmp.parseCtrlCharacters = false; }

            int size = _theme.GetFontSize(_sizeToken, UiContext.TextScale);
            if (size <= 0)
            {
                Debug.LogError("[UI] 글자 크기 토큰을 테마에서 찾지 못했습니다: '" + _sizeToken + "' (" + name + ")", this);
            }
            else if (!Mathf.Approximately(tmp.fontSize, size))
            {
                tmp.fontSize = size;
            }

            if (_theme.TryGetColor(_colorToken, out Color color))
            {
                if (tmp.color != color)
                {
                    tmp.color = color;
                }
            }
            else
            {
                Debug.LogError("[UI] 색 토큰을 테마에서 찾지 못했습니다: '" + _colorToken + "' (" + name + ")", this);
            }

            TMP_FontAsset font = _theme.GetFont(UiTokens.FontTokenFor(_weight));
            if (font != null && tmp.font != font)
            {
                tmp.font = font;
            }

            // 개발 전용: Pretendard 가 아직 없으면 OS 한글 글꼴을 동적 대체 글꼴로 붙인다(파일 복사 없음, 메모리 전용).
            UiDevFontFallback.EnsureInstalled(font);

            if (!string.IsNullOrEmpty(_textKey) && UiContext.Text != null)
            {
                SetStringIfChanged(UiContext.Text.Get(_textKey));
            }
        }

        /// <summary>코드가 정하는 글자를 넣는다. 같은 값이면 무시한다(할당 없음).</summary>
        public void SetDynamicText(string text)
        {
            SetStringIfChanged(text ?? string.Empty);
        }

        /// <summary>색 토큰을 바꾼다. 상태에 따라 색이 바뀌는 글자에 쓴다.</summary>
        public void SetColorToken(string token)
        {
            if (_colorToken == token)
            {
                return;
            }
            _colorToken = token;
            Apply();
        }

        /// <summary>텍스트 키를 바꾸고 바로 적용한다.</summary>
        public void SetTextKey(string key)
        {
            _textKey = key ?? string.Empty;
            _isDynamic = string.IsNullOrEmpty(_textKey);
            Apply();
        }

        private void SetStringIfChanged(string text)
        {
            TextMeshProUGUI tmp = Tmp;
            if (tmp != null && tmp.text != text)
            {
                tmp.text = text;
            }
        }
    }
}

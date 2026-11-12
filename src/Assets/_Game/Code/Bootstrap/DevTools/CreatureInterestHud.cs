// =============================================================
// 파일: CreatureInterestHud.cs
// 역할: [개발 도구] 관심 범위(300 m) 밖 생물이 사라지는지 보는 작은 표시. F1로 켜고 끈다(기본 꺼짐).
//       내 위치, 받은 생물 수, 등급별 수, 가장 가까운/먼 거리, (호스트) 서버 전체 생물 수를 오른쪽 아래에 보인다.
// 레이어: infrastructure (Game.Bootstrap.DevTools). F3 성능 표시와 같은 방식(IMGUI, 개발 빌드·에디터에서만 컴파일).
// 관련: FR-008, NFR-013 / SDD 3.11.9
// =============================================================
using System.Text;
using Game.Application.Ports;
using Game.Application.Replication;
using Game.Application.Server;
using UnityEngine;

namespace Game.Bootstrap.DevTools
{
    /// <summary>관심 범위 개발용 표시. GameFlow가 게임에 들어갈 때 만들고 나갈 때 없앤다.</summary>
    public sealed class CreatureInterestHud : MonoBehaviour
    {
        private const float RefreshSeconds = 0.25f;
        private const float FontRatio = 0.018f;
        private const int MinFont = 12;
        private const float Margin = 8f;

        private ClientCreatureStore _store;
        private ILocalPlayerPose _pose;
        private CreatureInterestStats _stats;
        private ServerWorld _serverWorld;
        private bool _visible;
        private float _nextRefresh;
        private readonly StringBuilder _builder = new StringBuilder(256);
        private string _text = string.Empty;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GUIStyle _style;
#endif

        /// <summary>만들어 붙인다. serverWorld는 호스트일 때만 넘긴다(아니면 null).</summary>
        public static CreatureInterestHud Create(ClientCreatureStore store, ILocalPlayerPose pose, CreatureInterestSettings settings, ServerWorld serverWorld)
        {
            var go = new GameObject("CreatureInterestHud");
            var hud = go.AddComponent<CreatureInterestHud>();
            hud._store = store;
            hud._pose = pose;
            hud._stats = new CreatureInterestStats(settings);
            hud._serverWorld = serverWorld;
            return hud;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            // F1: 켜고 끄기. IMGUI 이벤트로 읽어 입력 시스템 설정과 무관하게 동작한다.
            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.F1)
            {
                _visible = !_visible;
                _nextRefresh = 0f;
                e.Use();
            }
            if (!_visible || _store == null)
            {
                return;
            }
            if (e.type != EventType.Repaint && e.type != EventType.Layout)
            {
                return;
            }
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshSeconds;
                Refresh();
            }
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = false };
            }
            _style.fontSize = Mathf.Max(MinFont, Mathf.RoundToInt(Screen.height * FontRatio));
            Vector2 size = _style.CalcSize(new GUIContent(_text));
            float w = size.x + Margin;
            float h = size.y + Margin;
            GUI.Box(new Rect(Screen.width - w - Margin, Screen.height - h - Margin, w, h), _text, _style);
        }
#endif

        private void Refresh()
        {
            float x = 0f;
            float z = 0f;
            if (_pose != null && _pose.TryRead(out var pose))
            {
                x = pose.Position.X;
                z = pose.Position.Z;
            }
            _stats.Recompute(_store, x, z);
            _builder.Length = 0;
            _builder.Append("[F1] 위치 ").Append(Mathf.RoundToInt(x)).Append(", ").Append(Mathf.RoundToInt(z)).Append('\n');
            _builder.Append("받은 생물 ").Append(_stats.Total).Append(" / 범위 ").Append(Mathf.RoundToInt(_stats.RangeMeters)).Append(" m\n");
            _builder.Append("가까움 ").Append(_stats.Near).Append("  중간 ").Append(_stats.Mid).Append("  멂 ").Append(_stats.Far);
            if (_stats.Out > 0)
            {
                _builder.Append("  범위밖 ").Append(_stats.Out);
            }
            _builder.Append('\n');
            _builder.Append("최근접 ").Append(Mathf.RoundToInt(_stats.NearestMeters)).Append(" m  최원 ")
                .Append(Mathf.RoundToInt(_stats.FarthestMeters)).Append(" m (한계 ").Append(Mathf.RoundToInt(_stats.LimitMeters)).Append(")");
            if (_serverWorld != null && _serverWorld.Ai != null)
            {
                _builder.Append("\n서버 전체 ").Append(_serverWorld.Ai.Registry.Creatures.Count);
            }
            _text = _builder.ToString();
        }
    }
}

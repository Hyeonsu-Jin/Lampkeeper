// =============================================================
// 파일: RemoteAvatarViews.cs
// 역할: 다른 참가자의 아바타를 그린다. ClientWorldMirror의 자세 보간 버퍼를 읽어 캡슐 표시를 옮긴다(M1 개발용 모양).
//       명단(Roster)에서 빠진 플레이어의 표시는 지운다.
// 레이어: adapters (Game.Adapters.Presentation)
// 관련: FR-008, FR-023(다른 플레이어 화면에도 그 위치로 보임), FR-160, DES-072, DES-084 / SDD 3.9.8, D-19
//
// Net 어댑터를 참조하지 않는다(어댑터끼리 참조 금지). Net 어댑터가 사본에 넣은 자세만 읽는다.
// =============================================================
using System.Collections.Generic;
using Game.Application.Replication;
using Game.Domain.Common;
using UnityEngine;

namespace Game.Adapters.Presentation.World
{
    /// <summary>원격 플레이어 표시 관리자.</summary>
    public sealed class RemoteAvatarViews : MonoBehaviour
    {
        /// <summary>보간 지연(초). 서버 전송 간격(30 Hz)의 세 배 정도를 두어 패킷 간격 흔들림을 흡수한다.</summary>
        public const double InterpolationDelaySeconds = 0.1;

        /// <summary>캡슐 높이 중심 보정(발 위치 기준 캡슐 중심 높이, m).</summary>
        private const float CapsuleCenterHeightMeters = 0.9f;

        [SerializeField] private Material _material;

        private readonly Dictionary<PlayerId, Transform> _views = new Dictionary<PlayerId, Transform>();
        private readonly List<PlayerId> _scratch = new List<PlayerId>();
        private ClientWorldMirror _mirror;
        private PlayerId _local;

        /// <summary>표시할 월드 사본과 내 플레이어 ID를 정한다.</summary>
        public void Initialize(ClientWorldMirror mirror, PlayerId localPlayer)
        {
            _mirror = mirror;
            _local = localPlayer;
        }

        /// <summary>지금 표시 중인 원격 플레이어 수(시험·진단).</summary>
        public int ViewCount => _views.Count;

        private void Update()
        {
            if (_mirror == null)
            {
                return;
            }

            // 명단에 있는 사람만 표시한다.
            var inRoster = new HashSet<PlayerId>();
            foreach (KeyValuePair<uint, RosterEntry> pair in _mirror.Roster.Entries)
            {
                inRoster.Add(pair.Value.Player);
            }

            _scratch.Clear();
            foreach (PlayerId id in _views.Keys)
            {
                if (!inRoster.Contains(id))
                {
                    _scratch.Add(id);
                }
            }
            foreach (PlayerId id in _scratch)
            {
                Destroy(_views[id].gameObject);
                _views.Remove(id);
                _mirror.RemoveRemoteAvatar(id);
            }

            double renderTime = Time.realtimeSinceStartupAsDouble - InterpolationDelaySeconds;
            foreach (KeyValuePair<PlayerId, RemoteAvatarTrack> pair in _mirror.RemoteAvatars)
            {
                if (pair.Key == _local || !inRoster.Contains(pair.Key))
                {
                    continue;
                }
                if (!pair.Value.TrySample(renderTime, out Vec3f position, out float yaw, out _, out _))
                {
                    continue;
                }
                if (!_views.TryGetValue(pair.Key, out Transform view))
                {
                    view = CreateView(pair.Key);
                    _views[pair.Key] = view;
                }
                view.position = new Vector3(position.X, position.Y + CapsuleCenterHeightMeters, position.Z);
                view.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        private Transform CreateView(PlayerId player)
        {
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "RemoteAvatar-" + player;
            Collider collider = capsule.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider); // 표시 전용: 물리 충돌 없음(적·상호작용 판정은 호스트)
            }
            if (_material != null)
            {
                capsule.GetComponent<Renderer>().sharedMaterial = _material;
            }
            capsule.transform.SetParent(transform, false);
            return capsule.transform;
        }
    }
}

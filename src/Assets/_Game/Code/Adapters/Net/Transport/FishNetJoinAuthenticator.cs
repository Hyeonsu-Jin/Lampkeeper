// =============================================================
// 파일: FishNetJoinAuthenticator.cs
// 역할: FishNet 인증기. 연결이 들어와도 바로 인증하지 않고, 참가 판정(JoinHandshake)을 통과한 연결만 Authorize로 인증한다.
//       인증 전에는 FishNet이 스폰·관찰자 등록·클라이언트 ID 공유를 하지 않는다.
// 레이어: adapters (Game.Adapters.Net, FishNet 형식을 쓰는 파일)
// 관련: IMP-003, SEC-002, D-14, NFR-030
//
// 선택 이유: ObserverCondition은 스폰 객체마다 달아야 하고 ID 공유·기타 연결 데이터를 못 막는다. 인증기는 연결 단위로 한 곳에서 막는다.
// 개발용 옵션 DevOpenConnections: 켜면 예전처럼 연결 즉시 인증한다(실측 MP-09에서 방어 전후 비교용). 출시 빌드에서는 무시된다.
// =============================================================
using System;
using FishNet.Authenticating;
using FishNet.Connection;
using UnityEngine;

namespace Game.Adapters.Net.Transport
{
    /// <summary>참가 판정을 통과한 연결만 인증하는 FishNet 인증기. Boot 씬의 NetworkManager 오브젝트에 붙는다.</summary>
    public sealed class FishNetJoinAuthenticator : Authenticator
    {
        [Tooltip("개발 빌드·에디터에서만: 켜면 연결 즉시 인증(방어 끄기). 출시 빌드에서는 무시된다.")]
        [SerializeField] private bool _devOpenConnections;

        public override event Action<NetworkConnection, bool> OnAuthenticationResult;

        /// <summary>개발용 열림 옵션이 실제로 적용되는지(출시 빌드에서는 항상 false).</summary>
        public bool DevOpenActive
        {
            get
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                return _devOpenConnections;
#else
                return false;
#endif
            }
        }

        public override void OnRemoteConnection(NetworkConnection connection)
        {
            base.OnRemoteConnection(connection);
            if (DevOpenActive)
            {
                OnAuthenticationResult?.Invoke(connection, true);
            }
            // 그 밖에는 기다린다. 참가 판정이 끝나면 Authorize, 시간 초과면 JoinHandshake가 연결을 끊는다.
        }

        /// <summary>참가 판정을 통과한 연결을 인증한다. 이미 인증됐으면 아무것도 하지 않는다.</summary>
        public void Authorize(NetworkConnection connection)
        {
            if (connection == null || connection.IsAuthenticated)
            {
                return;
            }
            OnAuthenticationResult?.Invoke(connection, true);
        }
    }
}

// =============================================================
// 파일: TransportSelector.cs
// 역할: FishNet NetworkManager에 쓸 전송(Steam=FishySteamworks, 개발용=Tugboat)을 고르고,
//       네트워크 튜닝(서버 틱, 연결 끊김 판정 시간)을 적용한다.
// 레이어: infrastructure (Game.Infra.Transport)
// 관련: FR-005, FR-006(전송 방식 전환), NFR-008, NFR-031, NFR-049, DES-101 / SDD 3.7.4, 3.12.1
//
// 선택 규칙은 순수 C#(TransportPolicy)에 있고, 이 파일은 그 결과를 FishNet 구성 요소에 적용하는 일만 한다.
// 게임 로직은 전송 종류를 모른다(NFR-031): 전송은 INetServer/INetClient 뒤에 숨는다(M1-07).
// =============================================================
using System;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using FishNet.Transporting.Tugboat;
using Game.Application.Boot;
using Game.Application.Config;
using FishySteamworksTransport = FishySteamworks.FishySteamworks;
using FishNetTransport = FishNet.Transporting.Transport;

namespace Game.Infra.Transport
{
    /// <summary>선택된 전송과 그 FishNet 구성 요소.</summary>
    public readonly struct SelectedTransport
    {
        /// <summary>확정된 전송 종류.</summary>
        public TransportKind Kind { get; }

        /// <summary>NetworkManager에 연결된 전송 구성 요소.</summary>
        public FishNetTransport Component { get; }

        public SelectedTransport(TransportKind kind, FishNetTransport component)
        {
            Kind = kind;
            Component = component;
        }
    }

    /// <summary>
    /// NetworkManager의 전송 구성 요소를 고른다. 요청한 전송의 구성 요소가 없으면 같은 GameObject에 붙인다.
    /// </summary>
    public sealed class TransportSelector
    {
        /// <summary>전송이 받는 연결 수에 더하는 여유(거부 메시지를 보내 주기 위한 자리).</summary>
        public const int TransportConnectionMargin = 2;

        private const string PeerToPeerFieldName = "_peerToPeer";

        private readonly NetworkManager _networkManager;
        private readonly bool _isDevelopmentBuild;

        /// <param name="networkManager">설정할 NetworkManager</param>
        /// <param name="isDevelopmentBuild">개발 빌드(또는 에디터)인지. 출시 빌드는 항상 Steam</param>
        /// <exception cref="ArgumentNullException">networkManager가 null일 때</exception>
        public TransportSelector(NetworkManager networkManager, bool isDevelopmentBuild)
        {
            _networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
            _isDevelopmentBuild = isDevelopmentBuild;
        }

        /// <summary>
        /// 전송을 확정하고 NetworkManager에 연결한다.
        /// </summary>
        /// <param name="requested">요청한 전송(없으면 null, Steam으로 본다)</param>
        /// <returns>실제로 선택된 전송과 구성 요소</returns>
        public SelectedTransport Apply(TransportKind? requested)
        {
            // [FR-006] 출시 빌드는 요청을 무시하고 Steam으로 고정한다.
            TransportKind kind = TransportPolicy.Resolve(requested, _isDevelopmentBuild);

            FishNetTransport component;
            switch (kind)
            {
                case TransportKind.Tugboat:
                    component = GetOrAdd<Tugboat>();
                    break;
                case TransportKind.Steam:
                    component = GetOrAdd<FishySteamworksTransport>();
                    EnablePeerToPeer(component);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(requested), kind, "알 수 없는 전송 종류");
            }

            // NetworkManager가 아직 꺼져 있으면(Awake 전) TransportManager 속성이 비어 있으므로 같은 오브젝트의 컴포넌트를 직접 쓴다.
            // 전송은 NetworkManager가 깨어나기 전에 정해 둬야 FishNet이 초기화 때 그 전송을 연결한다(깨어난 뒤에 바꾸면 이벤트가 이어지지 않는다).
            TransportManager transportManager = _networkManager.TransportManager != null
                ? _networkManager.TransportManager
                : _networkManager.GetComponent<TransportManager>();
            if (transportManager == null)
            {
                // 꺼진 NetworkManager는 아직 보조 관리자 컴포넌트를 만들지 않았다. FishNet은 깨어날 때 같은 오브젝트의 기존 컴포넌트를 재사용하므로
                // 전송을 미리 걸어 둘 TransportManager를 먼저 붙인다.
                transportManager = _networkManager.gameObject.AddComponent<TransportManager>();
            }
            transportManager.Transport = component;
            return new SelectedTransport(kind, component);
        }

        /// <summary>
        /// 네트워크 튜닝을 적용한다(DES-101: 서버 틱, 연결 끊김 판정 시간).
        /// MTU는 Tugboat 내부 상수(1350)이고 FishySteamworks는 Steam이 정하므로 여기서 바꾸지 않는다.
        /// </summary>
        /// <param name="tuning">네트워크 튜닝 값</param>
        /// <param name="disconnectTimeoutSeconds">연결 끊김 판정 시간(초) [NFR-038, NFR-039]</param>
        /// <param name="active">Apply가 돌려준 선택 결과</param>
        /// <exception cref="ArgumentNullException">tuning이 null일 때</exception>
        public void ApplyTuning(NetworkTuning tuning, float disconnectTimeoutSeconds, SelectedTransport active)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            _networkManager.TimeManager.SetTickRate((ushort)tuning.ServerTickRateHz);
            active.Component.SetTimeout(disconnectTimeoutSeconds, true);
            active.Component.SetTimeout(disconnectTimeoutSeconds, false);
        }

        /// <summary>
        /// 전송의 연결 수 상한을 정한다. 세션 규칙(최대 인원)이 먼저 거부 사유를 알려 줄 수 있도록 여유를 둔다.
        /// [NFR-038] 최대 인원 + 여유 연결까지는 전송이 받고, 초과분은 JoinRejected(인원 초과)로 돌려보낸다.
        /// </summary>
        /// <param name="maxPlayers">세션 최대 인원</param>
        /// <param name="active">Apply가 돌려준 선택 결과</param>
        public void ApplyConnectionLimit(int maxPlayers, SelectedTransport active)
        {
            active.Component.SetMaximumClients(maxPlayers + TransportConnectionMargin);
        }

        private T GetOrAdd<T>() where T : FishNetTransport
        {
            T component = _networkManager.GetComponent<T>();
            return component != null ? component : _networkManager.gameObject.AddComponent<T>();
        }

        /// <summary>
        /// FishySteamworks를 Steam ID 주소(P2P, 릴레이) 방식으로 쓴다. 이 값은 FishySteamworks가 공개 설정 함수를 주지 않는 직렬화 필드라
        /// 리플렉션으로 켠다(서드파티 소스를 고치지 않기 위함). 필드가 없어지면(버전 변경) 예외로 알려 조용히 실패하지 않게 한다.
        /// </summary>
        private static void EnablePeerToPeer(FishNetTransport component)
        {
            System.Reflection.FieldInfo field = typeof(FishySteamworksTransport).GetField(PeerToPeerFieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null)
            {
                throw new InvalidOperationException("FishySteamworks에 '" + PeerToPeerFieldName + "' 필드가 없습니다(버전 변경 확인).");
            }
            field.SetValue(component, true);
        }
    }
}

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
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(requested), kind, "알 수 없는 전송 종류");
            }

            _networkManager.TransportManager.Transport = component;
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

        private T GetOrAdd<T>() where T : FishNetTransport
        {
            T component = _networkManager.GetComponent<T>();
            return component != null ? component : _networkManager.gameObject.AddComponent<T>();
        }
    }
}

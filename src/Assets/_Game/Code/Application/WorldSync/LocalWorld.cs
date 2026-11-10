// =============================================================
// 파일: LocalWorld.cs
// 역할: 이 PC에서 시드로 만든 월드(생성 결과 + 요약값)를 담는 값, 생성 규모(WorldScale), 같은 시드를 다시 만들지 않게 하는 캐시(LocalWorldProvider).
// 레이어: application (Game.Application/WorldSync, 순수 C#)
// 관련: FR-009, FR-169(모든 PC가 같은 월드를 만든다), NFR-016(자원 약 2만 개), NFR-018, NFR-052, DES-072 / SDD 3.9.2, 3.11.5, 3.12.6
//
// 호스트도 참가자도 같은 코드로 월드를 만든다. 호스트 PC에서는 호스트 서버와 호스트 자신의 클라이언트가 같은 월드를 쓰므로
// 한 번만 만들고 캐시에서 꺼내 쓴다. 상위 레이어(Net 어댑터, 시험)가 WorldGen 어셈블리를 직접 참조하지 않도록 필요한 값만 이 클래스로 보여 준다.
// =============================================================
using System;
using System.Diagnostics;
using Game.Domain.Common;
using Game.Domain.Gathering;
using Game.WorldGen.Hashing;
using Game.WorldGen.Model;
using Game.WorldGen.V0;

namespace Game.Application.WorldSync
{
    /// <summary>월드 생성 규모. 실제 게임은 Full(3,072 m, 자원 약 2만 개). Small은 시험·벤치 전용이다.</summary>
    public enum WorldScale : byte
    {
        Full = 0,
        Small = 1,
    }

    /// <summary>이 PC에서 만든 월드와 요약값. 만들어진 뒤에는 바꾸지 않는다.</summary>
    public sealed class LocalWorld
    {
        /// <summary>지원하는 생성기 버전 번호(JoinAccepted의 GeneratorVersion). 0 = V0.</summary>
        public const ushort GeneratorV0 = 0;

        private const float MillimetersPerMeter = 1000f;

        /// <summary>생성 결과(WorldGen 형식). 화면 어댑터(지형·자원 뷰)가 읽는다.</summary>
        public GeneratedWorld Generated { get; }

        /// <summary>월드 요약값(FR-169). 호스트와 참가자가 같아야 한다.</summary>
        public ulong Hash { get; }

        /// <summary>시드(u64).</summary>
        public ulong Seed { get; }

        /// <summary>생성기 버전 번호.</summary>
        public ushort GeneratorVersion { get; }

        /// <summary>규모.</summary>
        public WorldScale Scale { get; }

        /// <summary>생성 + 요약값 계산에 걸린 시간(밀리초, 진단·측정용).</summary>
        public long BuildMilliseconds { get; }

        /// <summary>자원 수. 배열 위치가 곧 ResourceIndex다.</summary>
        public int ResourceCount => Generated.Resources.Length;

        private LocalWorld(GeneratedWorld generated, ulong hash, ulong seed, ushort generatorVersion, WorldScale scale, long buildMilliseconds)
        {
            Generated = generated;
            Hash = hash;
            Seed = seed;
            GeneratorVersion = generatorVersion;
            Scale = scale;
            BuildMilliseconds = buildMilliseconds;
        }

        /// <summary>
        /// 시드로 월드를 만들고 요약값을 계산한다. 몇 초 걸리므로 화면 스레드에서 부르지 않는다(참가자는 백그라운드에서).
        /// </summary>
        /// <exception cref="NotSupportedException">지원하지 않는 생성기 버전</exception>
        public static LocalWorld Generate(ulong seed, ushort generatorVersion, WorldScale scale)
        {
            if (generatorVersion != GeneratorV0)
            {
                throw new NotSupportedException("지원하지 않는 생성기 버전입니다: " + generatorVersion);
            }
            var watch = Stopwatch.StartNew();
            V0Profile profile = scale == WorldScale.Small ? V0Profile.Small : V0Profile.Full;
            GeneratedWorld world = V0WorldGenerator.Generate(seed, profile);
            ulong hash = WorldHasher.Compute(world).Total;
            watch.Stop();
            return new LocalWorld(world, hash, seed, generatorVersion, scale, watch.ElapsedMilliseconds);
        }

        /// <summary>자원의 종류와 위치(m)를 읽는다. 번호가 범위 밖이면 false.</summary>
        public bool TryGetResource(int index, out ResourceKind kind, out Vec3f position)
        {
            ResourceEntry[] resources = Generated.Resources;
            if (index < 0 || index >= resources.Length)
            {
                kind = ResourceKind.None;
                position = Vec3f.Zero;
                return false;
            }
            ResourceEntry entry = resources[index];
            kind = entry.Type <= (ushort)ResourceKind.Coal ? (ResourceKind)entry.Type : ResourceKind.None;
            position = new Vec3f(entry.PosXmm / MillimetersPerMeter, entry.PosYmm / MillimetersPerMeter, entry.PosZmm / MillimetersPerMeter);
            return true;
        }
    }

    /// <summary>같은 (시드, 버전, 규모)의 월드를 한 번만 만들게 하는 캐시(마지막 하나만 기억). 여러 스레드에서 불러도 안전하다.</summary>
    public sealed class LocalWorldProvider
    {
        private readonly object _gate = new object();
        private LocalWorld _last;

        /// <summary>캐시에 있으면 꺼내고, 없으면 만든다.</summary>
        public LocalWorld GetOrGenerate(ulong seed, ushort generatorVersion, WorldScale scale)
        {
            lock (_gate)
            {
                if (_last != null && _last.Seed == seed && _last.GeneratorVersion == generatorVersion && _last.Scale == scale)
                {
                    return _last;
                }
                _last = LocalWorld.Generate(seed, generatorVersion, scale);
                return _last;
            }
        }
    }
}

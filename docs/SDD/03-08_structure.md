> SDD(소프트웨어 설계 명세서)의 일부 · 목차: [00-index.md](00-index.md)

### 3.8 구조 관점

#### 3.8.1 저장소 배치 결정
**Unity 프로젝트를 저장소의 하위 폴더 `src/`에 둡니다.** 저장소 루트는 git 루트이며 `docs/`, `.claude/`, `CLAUDE.md`가 `src/`와 나란히 있습니다. Unity Hub에서는 `src/` 폴더를 프로젝트로 엽니다.
- CLAUDE.md 기본값 `src/`와 같아 다른 에이전트(developer, code-reviewer, test-engineer)가 별도 안내 없이 코드 위치를 찾습니다.
- Unity가 루트에 만드는 `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.sln`, `*.csproj`가 문서·에이전트 설정과 섞이지 않습니다.
- Unity는 프로젝트 폴더 안의 `Assets/`, `Packages/`만 가져오므로 `docs/`가 Unity 가져오기(.meta 생성) 대상이 되지 않습니다.
- 대안(저장소 루트 = Unity 프로젝트)과 비교는 5장 D-01.

#### 3.8.2 폴더 구조

```
/ (git 루트)
├─ CLAUDE.md
├─ .claude/
├─ docs/                                  SRS, SDD, 디자인, 테스트 문서, 측정 보고서(docs/test/perf/)
├─ .gitignore                             src/Library, src/Temp, src/Logs, src/UserSettings, src/Builds, *.csproj, *.sln
├─ .gitattributes                         Git LFS: *.fbx *.png *.tga *.psd *.wav *.exr *.asset(대용량 지형 데이터) 등. YAML 애셋의 `merge=unityyamlmerge` 지정은 남아 있으나 드라이버를 git 설정에 등록하지 않아 git 기본 병합으로 동작하고, 충돌 시 `git mergetool`로 UnityYAMLMerge 실행(M1-01 (4))
└─ src/                                   ← Unity 프로젝트 루트 (Unity 6000.3.27f2)
   ├─ Packages/manifest.json              패키지·버전 고정 (NFR-049, D-03)
   ├─ ProjectSettings/
   └─ Assets/
      ├─ _Game/
      │  ├─ Code/
      │  │  ├─ Domain/                    Game.Domain.asmdef
      │  │  │  ├─ Common/                 PlayerId, ContentId, Result, ReasonCode, DomainEvent, GameVersion(v0.12, DES-037)
      │  │  │  ├─ Players/  Vitals/  Life/  Items/  Resources/  Crafting/  Building/
      │  │  │  ├─ World/                  WorldClock, PurifiedZoneSet, RegionMap, WorldSettings, Difficulty
      │  │  │  ├─ Defense/  Creatures/  Combat/  Farming/  Cooking/  Buffs/  Threats/
      │  │  │  ├─ Progression/            ActionCompleted, Proficiency, Unlocks, Objectives, Story, Tutorial
      │  │  │  ├─ Session/                SessionRules, SessionAccess, BanList
      │  │  │  ├─ Text/                   TextRules
      │  │  │  └─ Rules/                  규칙 교체 지점 인터페이스와 잠정 구현 (DES-033)
      │  │  ├─ WorldGen/                  Game.WorldGen.asmdef
      │  │  │  ├─ Common/                 DetMath(고정소수점, 해시, 난수, 노이즈) — 추가만 허용, 기존 함수 수정 금지
      │  │  │  ├─ Layout/                 WorldLayout 모델(고정 큰 배치)
      │  │  │  ├─ Validation/             GenerationValidator, 재생성 정책
      │  │  │  ├─ Hashing/                WorldHasher
      │  │  │  ├─ V0/                     M1 시험용 생성기 (출시 전 삭제 가능)
      │  │  │  └─ V1/ (V2/ ...)           버전별 생성기. 공개 배포 후 동결
      │  │  ├─ Application/               Game.Application.asmdef
      │  │  │  ├─ Ports/  Commands/  Server/  Replication/  Session/  WorldLifecycle/
      │  │  │  ├─ Protocol/               (v0.10) MessageKind, 메시지 구조체, AvatarPose, ByteWriter/ByteReader, 양자화, 크기 상한, MessageCodec (DES-080)
      │  │  │  ├─ Gameplay/               채집·제작·건축·생존·방어전·전투·농사·요리·위협·숙련도
      │  │  │  ├─ Ai/                     AiScheduler, SpawnDirector
      │  │  │  ├─ Client/                 ClientSession, ClientWorldMirror
      │  │  │  └─ Settings/
      │  │  ├─ Adapters/
      │  │  │  ├─ Net/                    Game.Adapters.Net.asmdef (Transport/ = FishNetServer·FishNetClient·PacketBroadcast(DES-121, `FishNetPacketTransport.cs`), Gateway/, Replication/, Avatar/(DES-084, `PlayerAvatar.cs`), Join/). v0.10: Messages/ 없음(메시지는 Application/Protocol/). v0.11: **FishNet 형식은 Transport/·Avatar/에서만**(3.4.1 규칙 8, 아키텍처 시험이 폴더 이름으로 검사)
      │  │  │  ├─ Presentation/           Game.Adapters.Presentation.asmdef
      │  │  │  │  ├─ Player/  World/(Terrain, Resources, Creatures, Lighting)  Interaction/
      │  │  │  │  ├─ UI/                  Common/(v0.13: UiTheme·UiTokens·Themed*·UiButton·UiScreen·UiRoot·UiScreenStack·UiInputRouter·UiSafeArea16x9·UiRaycastBlocker·KeycapView·UiArcGraphic·UiWarpedRawImage, UiGlyphs·텍스트 키 상수), Screens/(화면마다 …Screen 컴포넌트 + …View 클래스), Hud/(Common/ 공통 부품, Screen/ 일반 HUD, Diegetic/ 손목 계기·스태미나 호·등불 이름표), Presenters/
      │  │  │  │  ├─ Input/  Localization/  Audio/  Accessibility/
      │  │  │  └─ Persistence/            Game.Adapters.Persistence.asmdef (Codec/, Chunks/, Migrations/, Store/)
      │  │  ├─ Infrastructure/
      │  │  │  ├─ Steam/                  Game.Infra.Steam.asmdef
      │  │  │  ├─ Transport/              Game.Infra.Transport.asmdef
      │  │  │  ├─ AiJobs/                 Game.Infra.AiJobs.asmdef
      │  │  │  ├─ Config/                 Game.Infra.Config.asmdef (ScriptableObject 클래스, 로더)
      │  │  │  ├─ Platform/               Game.Infra.Platform.asmdef (Logging/, Crash/, Telemetry/, Settings/, Tasks/)
      │  │  │  └─ Rendering/              Game.Infra.Rendering.asmdef (HdrpRenderBackend, Graphics/, Upscaling/, PostProcess/) — HDRP 의존 코드는 여기만
      │  │  ├─ Bootstrap/                 Game.Bootstrap.asmdef (GameBootstrap = 컴포지션 루트, Runtime/(GameFlow, SessionRuntime, RuntimeSupport), DevTools/)
      │  │  └─ Editor/                    Game.Editor.asmdef (에디터 전용: BuildCommands, BuildArtifactInspector, ProjectConfigurator, 씬·애셋 생성 스크립트 — v0.11: InputActionsBuilder, WorldAndNetworkBuilders(AvatarPrefabBuilder, WorldSceneBuilder) 추가. v0.13: 생성기는 「없을 때만 만들기 + 검사」로 바뀜(아래 표), UiThemeBuilder(테마 애셋이 없을 때만), UI/(토큰 드롭다운 속성 그리기, Apply Theme To Prefabs 메뉴))
      │  ├─ Data/                         ScriptableObject 애셋 (코드 아님)
      │  │  ├─ Balance/                   부록 C.1 설정값 묶음
      │  │  ├─ Content/                   Items/, Resources/, Recipes/, Buildings/, Creatures/, Waves/, Crops/, Cooking/, Buffs/, Skills/, Objectives/, Achievements/
      │  │  ├─ Rules/                     규칙 교체 지점 선택값
      │  │  ├─ WorldGen/Profiles/V1/      생성 프로필(생성기 버전에 묶여 동결)
      │  │  ├─ Graphics/                  그래픽 프리셋 4종 + 기준 프리셋 지정
      │  │  └─ Network/                   틱·관심 범위·AI 주기 튜닝
      │  ├─ Localization/                 String Table(ko, en). M1~: strings.ko.txt·strings.en.txt(텍스트 키 표, M2-01에서 String Table로). v0.13: 글꼴은 UI/Fonts/로
      │  ├─ Art/  Audio/  VFX/
      │  ├─ Prefabs/                      v0.11: PlayerAvatar.prefab (NetworkObject + PlayerAvatar, 모양 없음 — 표시는 표현 어댑터가 월드 사본으로 그림)
      │  ├─ Materials/                    v0.11: WorldGround, WorldProp, RemoteAvatar, BenchGround, BenchAgent (HDRP/Lit, 생성 스크립트가 만듦)
      │  ├─ Scenes/                       Boot, World(v0.11 M1 개발용 장면), Bench. 계획: MainMenu(M2)
      │  ├─ UI/                           (v0.13, uGUI) 화면 배치는 ui-ux-designer 결과(STORYBOARD)를 따르고 **프리팹이 원본**
      │  │  ├─ Theme/                     UiTheme.asset (색·글자 크기·글꼴 토큰 한 벌, 3.7.10)
      │  │  ├─ Fonts/                     Pretendard TTF(3굵기)·TMP 글꼴 애셋(정적·동적)·기호 대체 글꼴, OFL 라이선스 원문
      │  │  ├─ Icons/                     단색 아이콘 스프라이트, 입력 장치 아이콘 세트(Xbox, M2: PlayStation)
      │  │  └─ Prefabs/
      │  │     ├─ UiRoot.prefab           UiRoot(테마·글자 단계·화면 스택·입력 라우터) + 자식 EventSystem(InputSystemUIInputModule)
      │  │     ├─ Common/                 공통 부품: Button_Primary·Button_Secondary·Button_Danger(UiButton + ThemedSelectable, 변형 프리팹), ListRow, Panel, Modal, Badge_Running, Gauge, Keycap, HelpLine
      │  │     ├─ Screens/                SteamRequired(SCR-001), DevLobby(SCR-004 M1 최소판 + SCR-012 접속 중), GameMenu(SCR-016 ESC). M2부터: MainMenu, Options 등 화면마다 하나
      │  │     └─ Hud/                    GameHud(SCR-015: 정적 루트 + HudDynamic 하위 캔버스), Parts/(HUD 공통 부품, M2-11), WristPanel(M2-12), HudAlerts(경고 층, M2-12), WorldMarkers(M2-09)
      │  │  (v0.13 삭제: Common/Base.uss, Screens/*.uxml·uss, Hud/Hud.uxml·uss, PanelSettings.asset)
      │  └─ Settings/                     HDRP 애셋(품질 단계별), Volume 프로필, Input Actions(v0.11: GameInput.inputactions)
      ├─ DefaultPrefabObjects.asset       (v0.11) FishNet이 자동으로 만드는 생성 가능 프리팹 목록. PlayerAvatar.prefab이 등록됨. NetworkManager가 이 애셋을 SpawnablePrefabs로 씀. FishNet 기본 위치(`Assets/` 바로 아래)라 _Game/ 밖에 있음 — 옮기지 않음
      ├─ TextMesh Pro/                    (v0.13) TMP Essential Resources(셰이더, TMP Settings — `Resources/` 안에 있어야 함). TMP 기본 위치라 _Game/ 밖에 둠. Examples & Extras는 가져오지 않음
      ├─ (삭제) UI Toolkit/UnityThemes/   v0.11에 PanelSettings가 자동으로 만든 기본 런타임 테마. v0.13 전환 때 .meta와 함께 삭제
      ├─ ThirdParty/
      │  ├─ FishNet/                      FishNet 4.7.4R (자체 asmdef: FishNet.Runtime, GameKit.Dependencies, Synapse, Unity.FishNet.CodeGen, MonoFN.Cecil)
      │  └─ FishySteamworks/              FishySteamworks 4.1.1 (FishySteamworks.asmdef)
      └─ Tests/
         ├─ EditMode/                     Domain/(Game.Tests.Domain), Application/(Game.Tests.Application), Infrastructure/(Game.Tests.Infrastructure), Architecture/(Game.Tests.Architecture), WorldGen/(Game.Tests.WorldGen, 골든 해시, M1-04에서 만듦), Net/(Game.Tests.Net, 가짜 INetServer로 게이트웨이·참가 절차·복제, v0.10, M1-07에서 만듦). 계획: Persistence/(Game.Tests.Persistence, M3-08)
         └─ PlayMode/                     Integration/(Game.Tests.Integration, 루프백 호스트+클라이언트)
```

**씬·프리팹 관리 원칙 (v0.13, 사용자 결정 2026-11-04, D-27)**: 결과물을 두 종류로 나눕니다.

| 종류 | 원본 | 대상 | 고치는 방법 |
|---|---|---|---|
| **오브젝트 원본** | 하이어라키·프리팹의 게임 오브젝트(씬·프리팹 파일) | 모든 UI 프리팹, `Boot.unity`, `World.unity`, `Bench.unity`, `PlayerAvatar.prefab`, `UiTheme.asset`, 머티리얼, `Data/` 설정 애셋(값) | 사람은 하이어라키·인스펙터·프리팹 모드에서 직접 고칩니다. **에이전트는 Unity MCP 도구**(gameobject·component·prefab 도구, 열린 에디터 조작)로 **개별 오브젝트·컴포넌트를 추가·수정**하고 씬·프리팹을 저장합니다. 씬·프리팹 YAML을 손으로(텍스트로) 고치지 않습니다. 화면 배치·색·글자 크기 토큰은 **프리팹에서** 고치고, 씬에는 프리팹 인스턴스와 참조 연결만 둡니다(인스턴스에 오브젝트·컴포넌트를 추가하지 않음, 3.4.3 규칙 검사) |
| **코드·데이터 원본** | 생성기 코드 또는 설정 데이터 | `ProjectSettings`(ProjectConfigurator), HDRP 품질 단계 애셋의 프리셋 값(GraphicsAssetBuilder, 프리셋 데이터가 원본), `GameInput.inputactions`(InputActionsBuilder, 시험이 생성 결과와 파일을 비교) | 생성기·데이터를 고치고 메뉴를 다시 실행합니다. 이 결과물은 에디터에서 직접 고치지 않습니다(다음 실행 때 덮어씀) |

- **오브젝트 원본 결과물의 생성기는 「없을 때만 처음 한 번 만들기 + 있으면 검사만」** 입니다. 파일이 이미 있으면 **절대 새로 만들어 덮어쓰지 않고**(`NewScene` + `SaveScene`로 같은 경로에 저장하는 v0.11 방식 폐지), 필요한 루트 오브젝트·컴포넌트·참조가 있는지 **검사만** 해서 빠진 것을 Console 오류로 알리고 `Run All`을 실패시킵니다(아무것도 바꾸지 않음). 빠진 것은 에이전트가 MCP 도구로, 또는 사람이 하이어라키에서 채웁니다. 「처음 한 번 만들기」는 새 PC에서 파일이 없을 때와 새 마일스톤의 새 씬·프리팹 첫 생성에만 쓰입니다.
- 화면을 처음 만드는 방법: 새 UI 화면 프리팹은 developer가 MCP 도구로 공통 부품 프리팹을 조합해 만들거나(권장), 필요하면 그 화면 전용 「없을 때만 만드는」 생성 메뉴를 한 번 쓰고, 이후에는 프리팹이 원본입니다.
- 검사 결과 확인: `Run All`을 실행한 뒤 `git status`에 오브젝트 원본 결과물(`.unity`, UI `.prefab`)의 변경이 없어야 합니다(생성기가 덮어쓰지 않는다는 확인).

전체를 한 번에 실행하는 메뉴 `Game/Setup/Run All`(`BuildCommands.RunAll`)은 앞 단계의 결과물을 뒤 단계가 참조하므로 아래 순서로 호출합니다. 하나씩 실행하는 메뉴도 각각 있습니다(v0.13: 오브젝트 원본은 `Game/Setup/Ensure …` — 없을 때만 만들고 있으면 검사).

| 순서 | 생성기 | 결과물 | v0.13 정책 | 비고 |
|---|---|---|---|---|
| 1 | `ProjectConfigurator.Apply` | ProjectSettings(Force Text·Visible Meta Files, 회사·제품 이름, `bundleVersion`은 **비어 있을 때만** `0.1.0`(v0.12: 그 밖에는 바꾸지 않음, 3.7.12 (2)), IL2CPP·.NET Standard, 선형 색 공간, `enableFrameTimingStats`, `runInBackground`, v0.13: Active Input Handling = Input System 확인) | 코드 원본(값 적용) | |
| 2 | `DataAssetBuilder.CreateDefaults` | `Data/GameDataConfig.asset`, `Data/Balance/BalanceConfig.asset`, `Data/Network/NetworkTuningConfig.asset`, `Data/Graphics/GraphicsPresetConfig.asset` | 없을 때만(기존과 같음) | 없는 애셋만 만들고(기존 값은 유지) 서로 연결, 콘텐츠 색인 갱신 |
| 3 | `GraphicsAssetBuilder.CreateAll` | 품질 단계·HDRP 애셋(`Settings/HDRP *.asset`) | 코드·데이터 원본(값 동기화) | v0.9 |
| 4 | `InputActionsBuilder.Create` | `Settings/GameInput.inputactions` (제어 체계 `KeyboardMouse`·`Gamepad`, 동작 지도 `Gameplay`·`UI`, STORYBOARD 2.4 기본 할당, 패드 겹침 항목은 비움 — 3.7.9. v0.13: UI 지도에 `Point`·`Click`·`ScrollWheel` 추가) | 코드 원본(덮어씀) | v0.11 |
| 5 | `UiThemeBuilder.Ensure` (v0.13) | `UI/Theme/UiTheme.asset`(3.7.10 표의 토큰·값) | **없을 때만** 만들고, 있으면 토큰이 빠졌는지 검사 | 만든 뒤에는 애셋이 원본(값은 인스펙터에서 고침) |
| 6 | `AvatarPrefabBuilder.Ensure` (v0.13 이름 변경) | `Prefabs/PlayerAvatar.prefab` → FishNet 자동 생성기가 `Assets/DefaultPrefabObjects.asset`에 등록 | **없을 때만**, 있으면 `NetworkObject`·`PlayerAvatar` 검사 | v0.11. FishNet 형식은 리플렉션으로 붙임(D-25) |
| 7 | `WorldSceneBuilder.Ensure` (v0.13 이름 변경) | `Scenes/World.unity`, `Materials/World*.mat`·`RemoteAvatar.mat`, 빌드 설정에 World 추가 | **없을 때만**, 있으면 `PlayerRig`·`RemoteAvatarViews`와 빌드 설정 등록 검사. 머티리얼도 없을 때만 | v0.11. 열린 씬을 건드리지 않게 새 씬을 Additive로 열어 저장 후 닫음(처음 만들 때만) |
| 8 | `BootSceneBuilder.Ensure` (v0.13 이름 변경) | `Scenes/Boot.unity`(꺼진 `NetworkManager`, `Bootstrap`, `MenuCamera`, UI 프리팹 인스턴스 `UiRoot`·`SteamRequiredScreen`·`DevLobbyScreen`·`GameHud`·`GameMenu`), 빌드 설정 첫 씬 | **없을 때만** 만들고(UI는 `PrefabUtility.InstantiatePrefab`으로 인스턴스 배치 + `GameBootstrap` 참조 연결), 있으면 **검사만**: 위 루트 오브젝트, `NetworkManager` 꺼짐, `GameBootstrap` 직렬화 참조가 모두 비어 있지 않음, EventSystem 하나, 빌드 설정 첫 씬 | 4·6의 결과물(Input Actions, 아바타 프리팹의 `NetworkObject`)을 처음 만들 때 연결. v0.11의 `UI/PanelSettings.asset` 생성은 삭제 |
| 9 | `BenchSceneBuilder.Ensure` (v0.13 이름 변경) | `Scenes/Bench.unity`(개발 빌드에만 포함) | **없을 때만**, 있으면 `BenchRunner` 검사 | v0.9. 측정 조건(생물 수 등)은 씬이 아니라 설정 데이터에서 읽으므로 씬을 다시 만들 필요가 없음 |

- UI 프리팹(`UI/Prefabs/**`)은 `Run All`이 만들지 않습니다(항상 원본이 저장소에 있음). 프리팹 규칙 검사는 시험(`UiPrefabRulesTests`)이 합니다.
- 결과물(`.unity`, `.prefab`, `.inputactions`, `.asset`)은 `.meta`와 함께 커밋합니다. 코드 원본 생성기를 바꾸면 해당 메뉴를 다시 실행하고 결과물 변경도 같은 커밋에 넣습니다. 오브젝트 원본을 MCP 도구로 고쳤으면 에이전트는 바꾼 오브젝트·컴포넌트 목록을 보고하고, 사용자는 그동안 에디터를 조작하지 않습니다(CLAUDE.md, 저장하지 않은 사용자 변경과 섞이지 않게).

#### 3.8.3 이름 규칙
- 네임스페이스는 어셈블리 이름과 같게 시작합니다(`Game.Domain.Items`).
- 순수 C# 어셈블리 안의 클래스는 `MonoBehaviour`, `ScriptableObject`를 상속하지 않습니다.
- ScriptableObject 클래스 이름은 `…Def`(콘텐츠 하나) / `…Config`(설정값 묶음) / `…Profile`(생성 프로필)로 끝냅니다.
- 생성기 버전 폴더(`V1/`)의 클래스는 `V1` 접두사 또는 네임스페이스로 구분해 다른 버전과 이름이 겹치지 않게 합니다.

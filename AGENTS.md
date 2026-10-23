# AGENTS.md

Welcome to the **Simpiens** project! This document outlines the Unity simulation's architectural layout, guiding automated agents on where to find, modify, or extend code.

> **Source of truth:** [`docs/ROADMAP.md`](docs/ROADMAP.md) is the highest source of truth for epic/milestone **status**, scope, sequencing, engineering invariants and the Definition of Done. This file explains *how to work in the code*; it deliberately does **not** track status. If this file and the roadmap disagree, the roadmap wins — fix this file.

If you are an agent then please check the .agents/ directory for rules or any current *directives* the developer has issued for your current session. 

## Current Project Structure
The simulation is engine-free and lives in its own assembly (Epic CORE):

| Folder | Assembly | Holds |
| :--- | :--- | :--- |
| `Assets/Simpiens.Core/` | `Simpiens.Core` (`noEngineReferences: true`: Unity refuses any engine reference) | `Cognition/`, `Simulation/` (incl. `SimulationManager.cs`), `Session/` (sessions, layouts, `SessionHost`), `Intervention/`, `Narrative/`, `Diagnostics/` |
| `Assets/Scripts/` | `Simpiens` (Unity) | the composition root (`Core/`), views (`Entities/`), the dashboard (`UI/`), `SwarmSpawner`, `AutonomousAgent`, `GridVisualizer`, the persistence smoke |
| `Assets/Simpiens.Core.Testing/` | `Simpiens.Testing.Core` (test-only, no engine) | engine-free validators, fixtures, the Dev Map session, `WorldStateHash` |
| `Assets/Scripts/Testing/` | `Simpiens.Testing` (test-only, Unity) | the anomaly detector and recorder, the batch controller, `EdgeCaseInjector`, view and UI validators |
| `dotnet/` | `Simpiens.slnx` (`net10.0`) | the same core and harness sources compiled for .NET by glob (`Simpiens.Core`, `Simpiens.Testing.Core`), `Simpiens.Hosting` (registrations for `Microsoft.Extensions.DependencyInjection`, `LocalSimulation`, the `EconomySoak` and the DoD observer `OutpostWatch`), `Simpiens.Host` (the `simpiens` development console: `run`, `sweep`, `soak`, `golden`; it uses the test harness and never ships), and `Simpiens.Core.Tests` (xUnit v3: the engine-free validators, the golden hashes, determinism and the outpost's DoD scenarios headless; `dotnet test dotnet/Simpiens.slnx`). The KNI game client (Epic CLIENT): `Simpiens.Presentation` (what is drawn, as data, with no XNA reference: generated sprites, map tiles, the projections (`IViewProjection`: `TopDownProjection`, `IsometricProjection`; a `View` is a projection through a camera, and everything that maps world to screen takes one; `View.FloorToScreen` is its affine transform, under which what lies on the floor (`DrawItem.OnFloor`: the fog's cloud) is drawn) and view modes (`ViewMode`: a projection with its art set, `WorldArt`: `TopDownArt` is Unity's art, `IsometricArt` stands the generated placeholders of `IsometricSprites` on their feet; isometric is the game's view, V or `--view top` switches to top-down; walls in front of rooms are cut away, `WallStyle`, C cycles), screen space for hard cutscenes (`ScreenProjection`: a 640×360 virtual screen, letterboxed), and `Cutscenes/`: cutscenes written in code (`CutsceneBuilder`, `CutsceneScript`), played by `CutscenePlayer` (hard: `CutsceneStage` draws them; dynamic: `SceneDirector` starts them from what the frames saw, a crawler entering a zone or a chronicle interaction, once per tick), the draw list and its sort key (draw roles; the projection picks their layers), the views' per-tick frames (`WorldViewFrame`, captured on `SessionHost.TickCompleted`: the views read frames, never the live simulation, and draw crawlers between ticks), crawler appearance, fog (it hides what an uncharted sector holds, crawlers included: producer decision D1 = C), the map's labels laid out in screen space from the baked fonts (`MapLabelLayout`, `BitmapFontMetrics`), the camera rig and its controls, selection and picking (clicks pick what was drawn), PNG files, and `Dashboard/`: the dashboard as data, whose actions leave as typed commands), `Simpiens.Client` (`SimpiensGame`, the XNA shell: window, device, content (`ClientGraphics`: the sheets, renderer and fonts every screen shares), the Gum layer and a stack of screens (`ScreenStack`; `PlayScreen` is the world view with the dashboard and dynamic scenes, `DynamicSceneView`; `CutsceneScreen` is a hard cutscene, which holds the world); `Restart` starts a new session in the same window (the tests reuse windows: an offscreen game can't be disposed); `ClientSession`, what is played, with no XNA type (the simulation and its driver, the frames, the selection, the dashboard model, the save slot, and the host's commands: speed, pause, save, load and the session switch are `HostCommand`s the views queue and the session applies before its ticks); the input, read once per update into an `InputFrame` (keys become `InputAction`s through `KeyBindings`) that feeds both the top screen and Gum's cursor (`Ui/ClientCursor`); `FrameTimings`, each frame's phases (F3 in Debug builds); `WorldView`, how the world is drawn (projection, camera rig and controls, the scene, the map's text, picking: its state moves on in `Update`, and a draw only presents it); the renderer; and `Ui/`: the dashboard in Gum, views over the dashboard models that decide nothing; `XnaPlatform` is the one class where KNI and MonoGame may differ), `Simpiens.Desktop` (the desktop exe; Debug builds add the test harness's Dev Map to the session switch), `Simpiens.FontBaker` (a developer tool, never shipped: bakes `FontSet`'s faces of Inter into BMFont atlases under Gum's cache names in `Simpiens.Client/Content/FontCache`, which are committed; text is drawn 1:1 from them, never scaled), `Simpiens.Android` (compile-only until M4; outside `Simpiens.slnx`, because it needs the Android workload) and `Simpiens.Client.Tests` (xUnit v3: presentation tests, golden hashes from inside the client, render tests against images in its `Baselines/`, and picture pins (`PicturePinTests`) that hash what is drawn finer than the render tolerance, which hides a crawler-sized change) |

**Unity's views are frozen** (Epic CLIENT, see the roadmap): new view, UI and rendering work goes into the KNI client in `dotnet/`, not into `Assets/Scripts/`. In the client, views read the simulation and never write it, and only `XnaPlatform` may use an API that KNI and MonoGame don't share (`-p:XnaFramework=MonoGame` compiles the client against MonoGame to check).

Namespaces didn't change when files moved (`Simpiens.Cognition`, `Simpiens.Simulation`, `Simpiens.Core.Session`, …). The directory notes below use the folder names inside those trees. Code shared with .NET stays at C# 10 and netstandard2.1 APIs (`dotnet build` checks both). Unity's Mono and .NET's CoreCLR round some scalar float expressions differently: they agree on the golden seeds, not on every seed, so compare a .NET run with .NET runs and a Unity run with Unity runs (epic CORE §6 F1).

### Checking a change
A change to the core is checked in both runtimes: `dotnet test` takes seconds; the Unity suite takes minutes.

| Check | Command | Needs |
| :--- | :--- | :--- |
| .NET tests (validators, golden hashes, the outpost's DoD scenarios, the economy soak) | `dotnet test dotnet/Simpiens.slnx` | .NET 10 SDK |
| Console host | `dotnet run --project dotnet/Simpiens.Host -- run`, `sweep --seeds 1..20`, `soak`, `golden check` (`-- help` lists the options) | .NET 10 SDK |
| The game client's tests (part of `dotnet test`): presentation, golden hashes inside the client, renders | `dotnet test dotnet/Simpiens.Client.Tests` (renders within a driver's rounding of the references) | System SDL2 and Mesa (the tests run SDL's offscreen driver on llvmpipe, no display) |
| The client's tests in the pinned image, as CI runs them: renders must match the references byte for byte | `scripts/test-client-in-container.sh`; record changed renders only here, with `--record` (look at `container-output/render-output/` first, commit them on their own) | podman (rootless), or `CONTAINER_ENGINE=docker` |
| The game (desktop) | `dotnet run --project dotnet/Simpiens.Desktop -- --seed 424242` (`-- --help` lists the options) | A display |
| The client against MonoGame (compile only) | `dotnet build dotnet/Simpiens.Desktop -p:XnaFramework=MonoGame` | .NET 10 SDK |
| Re-bake the fonts after changing `FontSet` (commit the result on purpose; a test compares the committed atlases with a fresh bake) | `dotnet run --project dotnet/Simpiens.FontBaker` | .NET 10 SDK (KernSmith with FreeType, Linux x64 or Windows) |
| The Android client (compile only) | `scripts/build-android.sh` | A user-local toolchain: `scripts/setup-android-toolchain.sh` installs a .NET SDK with the android workload, a JDK and the Android SDK under `~/.cache/simpiens-toolchains`, with no sudo |
| The client's frame time by phase (a measurement; `FrameTimeGateTests` is the gate, with a generous budget): the map, the panels, a window | `dotnet dotnet/Simpiens.Client.Tests/bin/Release/net10.0/Simpiens.Client.Tests.dll -method "*MeasureFrameTime*" -explicit only -showLiveOutput` | As the client's tests |
| Unity PlayMode suite | `scripts/run-playmode-tests.sh` | Unity, with the editor closed |
| Golden hashes in Unity, one process per seed | `scripts/golden-hashes.sh check` (`record` only for a deliberate behaviour change, in its own commit) | Unity |
| Economy soak in Unity | `scripts/run-economy-soak.sh` | Unity |
| Save and load in a Linux player | `scripts/run-persistence-smoke.sh` | Unity |

CI (`.github/workflows/dotnet.yml`) runs the .NET tests (the client's in the pinned image of `scripts/client-tests.Containerfile`), the golden check, the MonoGame compile and the Android compile on pushes to `main` and `main-kni` (the acting main until M4) and on pull requests; the Unity suite runs locally.

### Directories

- **`Cognition/`**
  Houses the asynchronous cognitive reasoning engine for agents.
  - `Contracts/`: Interfaces and data structures for Agent Intents (`AgentIntent`, `WanderIntent`, `TradeIntent`, `ClaimBountyIntent`, …), Intent Execution (`IIntentHandler`, `IIntentExecutionContext`, `IIntentHandlerRegistry`), and Context (`AgentContext`, `AgentNeeds`, `IntentResult`).
  - `Evaluators/`: Tier-1 reflexive/utility evaluators (`SurvivalUtilityEvaluator` — hunger reflex only, returns `null` otherwise; `MentalBreakEvaluator`, `AltruismEvaluator`, `FrustrationEvaluator`) and the Tier-3 ambient fallback `AmbientWanderEvaluator` (passed to `AgentCognitionService`).
  - `Planning/`: GOAP domain — zero-allocation bitmask `WorldState`/`StateFact`, `GoapPlanner` (A*), `ActionGraph`, `Actions/`, `Goals/`, `WorldStateBuilder`, and modular intent handlers under `Handlers/` (`IntentHandlerRegistry`, `WanderIntentHandler`, `PanicIntentHandler`, `HarvestResourceIntentHandler`, `ShareResourceIntentHandler`, `IdleIntentHandler`, `TradeIntentHandler`, `ClaimBountyIntentHandler`, `DeliverBountyIntentHandler` (Gather + Scout, escrow-only payout; a scout's report publishes its survey), `ScoutIntentHandler`, `SleepAtBunkIntentHandler`), plus shared `ResourceTargeting` / `BountyQueries` / `BunkTargeting` helpers, the utility `Considerations` (personality-weighted priorities, reservation/minimum prices, bounty score) and `Domain/OutpostGoapDomain` (the production action graph and goal list).
  - `Memory/`, `Social/`: Fog-of-war spatial memory, double-buffered snapshots (`IAgentMemory`, `AgentMemorySnapshot`), event ledger, gossip, 4-axis affinity (`AgentMemory`, `AffinityRecord`).
  - `Groups/`: Generic group primitive, directives, compliance, succession (`SuccessionEngine`, `ComplianceUtility`).
  - `Inventory/`: `AgentInventory` (fixed-capacity item slots + gold, double-buffered `AgentInventorySnapshot`).
  - `Pathfinding/`: Zero-allocation 2D Grid A* navigation engine with binary min-heap, epoch tracking, diagonal corner-cutting prevention, and raycast smoothing (`AsyncPathfinder`, `GridAStarSearcher`, `PathRequest`, `PathResponse`).
  - **Key Files**: `AgentState.cs` (pure C# state, needs & drives), `AgentCognitionService.cs` (cognition: main-thread preparation, off-thread evaluation returning a `CognitionResult`), `CognitionScheduler.cs` (decisions dispatched on tick T land at T + 1 in agent-id order, on the main thread), `AgentConsequenceService.cs` (intent consequences & rewards), `ToolExecutionRegistry.cs` (MCP tool execution bridge), `AgentRuntime.cs` (a crawler's tick), `AutonomousAgent.cs` (its view).
  - A crawler is plain data (Epic CORE C5): its `AgentState`, its `WorldEntity` in the world and its `AgentRuntime` (the per-tick drives, pre-emption, decision dispatch and perception while busy). `AutonomousAgent` is only its view (bound with `Bind(runtime)`, editor telemetry); it never ticks or writes state. Agents can be instantiated, ticked and simulated headlessly without any `GameObject`.

- **`Core/`**
  Handles bootstrapping and Unity-specific initialization, and hosts the main-thread simulation executor.
  - **Key Files**: `AppBootstrapper.cs` (VContainer composition root; namespace `Simpiens.Core.Bootstrapping`; engine-free classes with several constructors are registered by factory, since the core carries no container attributes), `SimulationClockDriver.cs` (advances the engine-free clock with Unity's frame time), `UnitySimLogSinks.cs`, `SimulationStarter.cs`, `SimulationManager.cs` (main-thread tick: orchestrator implementing `IIntentExecutionContext`, delegating intent execution to `IIntentHandlerRegistry`, draining typed `InfluenceEvent`s, trade/bounty/contention rules, hearth dynamics).
  - `Session/`: sessions as code. An `ISessionDefinition` is a map plus `Populate(SessionBuilder)` (agents, relationships, groups, stalls, seeded bounties). Its `Economy` (`EconomyTuning`) is applied on every respawn, before `Populate`. `OutpostSession` is the live game. `SessionHost` (engine-free) builds any session (loads the map through `IWorldMap`, spawns its keyed nodes, bunks and hearths, calls `Populate`, records the ledger baseline, registers seeded bounties), ticks every crawler after the simulation's tick and then raises `TickCompleted` (where views capture what they draw), and captures and restores it; `SwarmSpawner` is its Unity side: it draws what the host spawns (views follow the host's and the registry's events) and keeps the session API (`ISessionSwitcher`, `ISessionPersistence`) for the dashboard and scenarios (`spawner.Host` for the engine-free session). Every respawn rewinds the clock to tick 0: express seeded expiries relative to the session start. A respawn also starts a new session epoch: an intent stamped with an older `AgentIntent.SessionEpoch` is dropped, because entity ids repeat per seed. Boot always picks the default (outpost) session by id. `SessionHost` also captures and loads whole sessions (`Capture`, `TryRestore`: see `Simulation/Persistence/`), and `SaveGameService` is the player's quick-save slot.
  - `Session/Layouts/`: map definitions as code. `Floor1Layout` is the live outpost floor (sectors, doors, POIs, nodes, spawn points, bunks).
  - `BootstrapExtensions.cs`: hook through which optional assemblies (the test harness) add DI registrations; production never references `Simpiens.Testing`.
  - `Editor/`: Editor-only batch/setup utilities.

- **`Routines/`**
  `IRoutine.cs` — routine abstraction used by `AgentMotor`.

- **`Entities/`**
  Contains strictly `MonoBehaviour` views that hold state or rendering instructions.
  - **Rules**: Zero logic! Only basic view management, motor controls, and configurations.
  - **Key Files**: `AgentMotor.cs`, `NodeController.cs`, `AgentView.cs`, `NodeFactory.cs`, `MapView.cs` (draws a map's floors, rock and walls on tilemaps plus its labels; walls are tiles only, never entities), `ShroudView.cs` (fog over uncharted sectors, read from `ISectorRegistry.ExploredMask`), `CameraRig.cs` / `CameraController.cs` (camera maths and its Input System layer), `WorldSelection.cs` (the player's selected crawler and sector, shared with the dashboard), `RenderOrder.cs` (the one sorting-order table: use it for anything drawn).
  - A `NodeController` is the view of one `WorldEntity` (`Bind(entity)`): it draws the entity's logical position and never writes back. It requires a `Rigidbody2D` and a collider (for clicking): set your own (static, trigger if walkable), or Unity adds a dynamic body.

- **`Intervention/`**
  Player intervention system acting as an influence queue.
  - **Key Files**: `PlayerInterventionService.cs`, `InfluenceEvent.cs` (zero-allocation typed command union: `PostBountyCommand`, `CancelBountyCommand`, `UpdateStallPricesCommand`).

- **`Simulation/`**
  The simulation environment, registries, and spatial representations.
  - `Primitives/`: engine-free value types in the root namespace `Simpiens` (found from every `Simpiens.*` namespace without a `using`, and winning over Unity's own `UnityEngine.EntityId`): `EntityId`, the id of every simulation entity, bit-compatible with the `UnityEngine.GUID` it replaced (same words, ordering, hash code and hex text); and the maths: `Vec2`, `GridPoint`, `RectF`, `Rgba`, `SimMath`, which copy Unity's `Vector2`, `Vector2Int`, `Rect` and `Mathf` formulas exactly (including `Vec2 ==` being approximate and rounding half to even). Simulation code never uses an engine type; views convert at the boundary with `ToUnity()` / `ToSim()` (`Entities/SimUnityConversions`).
  - `Spatial/`: Handles memory-managed grids (`SpatialHashGrid`, `SharedWorldSnapshot`, `EntitySnapshot`, `StaticNavGrid` with a static base layer for map walls).
  - `World/`: static geography as plain data. `MapDefinition` (+ `MapDefinitionBuilder`) is the single source of truth for walls, sectors, resource nodes, POIs and labels; `WallRasterizer` writes walls into the nav grid; `IWorldMap.Load` sizes the partition from the map (never the camera) and loads `ISectorRegistry` (authoritative sectors + exploration). The snapshot carries the map and the charted-sector mask: **fog of war**, nothing inside an uncharted sector is perceived until a scout's report charts it. Never hard-code coordinates or sector ids elsewhere: read them from the layout.
  - `Economy/`: Unmanaged economy structs (`ItemInstance`, `TradeStallData`, `BountyContract`), authoritative gold tracking (`IEconomyLedger`, `EconomyLedger`), the session's economy numbers (`EconomyTuning`, with `SupplyTuning` nested), the trade fairness rule (`TradeFairness`), and the periodic economy (`ISupplyService`, `SupplyService`). The periodic economy covers dormant-node regrowth and the caravan visit: salvage quota faucet, then upkeep sink, then ration restock. `SimulationManager` ticks it via `ISupplyHost`. The model is in [`docs/economy-model.md`](docs/economy-model.md). Economy numbers live in `EconomyTuning` only: read them from the session's tuning (`IIntentExecutionContext.Economy`, or `Considerations.Economy` in cognition), never as literals.
  - `Persistence/`: session save and load.
    - `SessionSave` is a whole session at one tick: a header, then one block per owner (crawlers, economy, world, groups, chronicle). Each owner writes and reads its own block (`CaptureTo` / `RestoreFrom`, `AgentState.CaptureRecord` / `RestoreRecord`); `SessionHost.Capture` / `TryRestore` assemble them. Keyed records are sorted by id, so a capture doesn't depend on dictionary order.
    - **New authoritative state goes into its owner's block,** with a check in the round-trip validators (`SessionCaptureValidator`, `SessionRestoreValidator`). A round trip only proves what a capture contains: state left out of the save is lost on load without any test failing.
    - Not saved: work in progress (intents, plans, and the bunk claims and contention tied to them: crawlers decide afresh after a load), derived state (snapshots, grids, the agent and world registries), and what the session definition provides (the map and the economy tuning).
    - A load rebuilds the map exactly as a respawn does (same ids for the map's entities), then restores in place of `Populate`. It refuses, before changing anything, a save with another format version, session, seed or map (`SaveFingerprint`).
    - Saves are captured and loaded on the main thread between ticks, never from cognition. Nothing a save reads is written off-thread.
    - `SaveFile` is the file format: a plain header (magic, format version, length, checksum), then the save, so a wrong, cut-off or damaged file is refused with a reason. `SaveGameService` (`Core/Session/`) is the quick-save slot, `Saves/quicksave.simsave` under the persistent data path; tests give it their own folder.
    - `SaveSerializer` is the only code that calls MemoryPack. MemoryPack is vendored by `scripts/update-memorypack.sh`, and its source generator runs as a Roslyn analyzer.
    - Unmanaged records are saved as raw memory copies, so changing a saved struct's layout changes the save format. When that happens, bump `SessionSaveHeader.CurrentFormatVersion` (`EconomySerializationValidator` pins the economy records' sizes).
    - `scripts/run-persistence-smoke.sh` builds a Linux player with a persistence smoke test compiled in and runs it headless: the player saves the running outpost to a file and loads it back.
  - **Key Files**: `WorldEntity.cs` (an entity as plain data: id, kind, position, radius, speed, resource kind, its `AgentState` for crawlers), `WorldRegistry.cs` (entities in registration order, which is the snapshot's order; `EntityRegistered`/`EntityUnregistered` let views show and hide), `GroupRegistry.cs`, `SimulationClock.cs`, `ISimulationManager.cs`. (`SimulationManager.cs` itself lives in `Core/`.)

- **`Diagnostics/`**
  Logging façade over ZLogger (`SimLog`, `SimLogSettings`, `SafeSinkLogProcessor`, `TickStampedFileLogProcessor`). One `ILogger` per channel (`SimLog.Planning`, `SimLog.Economy`, …). Engine-free: the host plugs its console in (`SimLog.ConsoleSinkFactory`; Unity's is installed by `Core/UnitySimLogSinks` in `AppBootstrapper.Awake`, which then configures it); it is a silent `NullLogger` until configured (headless validators). Rules: `docs/ROADMAP.md` §1A.3.

- **`Narrative/`**
  Chronicle pipeline: `NarrativeChronicleService`, `TemplateNarrativeFormatter`, `ILlmNarrativeEnricher` (LLM hook; passthrough by default).

- **`UI/`**
  UI Toolkit presenters (dumb views over simulation state): `FacilityDashboardPresenter`, `Pooling/VisualElementPool`, `Zones/FacilityZoneService` (UI adapter over `ISectorRegistry`; owns no sector data).

- **`Testing/`** (separate `Simpiens.Testing` assembly, `UNITY_INCLUDE_TESTS` only: never ships in player builds)
  Domain validators and test harness services. Validators do **not** run at boot; each has a PlayMode test wrapper in `SwarmPlayModeTests`. `TestHarnessInstaller` registers the harness services (anomaly detector, recorder view, `EdgeCaseInjector`, batch controller) through `BootstrapExtensions`. Production internals are visible to it via `Assets/Scripts/AssemblyInfo.cs`.
  - `Environment/`: Chaos injectors and batch runners (`EdgeCaseInjector.cs`, `BatchPlaymodeController.cs`), `HeadlessEntities` (validators build crawlers and props as records, never GameObjects), plus environment/determinism validators, `WorldStateHash` (a hash of the authoritative world, with per-part descriptions that name what differs) and `GoldenWorldHash` (the golden baselines in `Baselines/golden-world-hash.txt`).
  - `Sessions/`: the test-only **Dev Map**: `CanaryPenSession` (the 10 scripted demonstration pens) on `DevMapLayout`, registered by `TestHarnessInstaller` and reachable in the Editor through the dashboard's session switch. Pen code never goes back into production.
  - `World/`: map validators (`MapDefinitionValidator` incl. Floor1 and Dev Map checks, `WorldMapValidator`, `SectorChartingValidator` for the fog of war), `TestWorlds` fixtures and `CommonsYardFixture` (an open yard for validators that hand-place agents with no walls between sectors).
  - `Memory/`: Knowledge matrix and social gossip validation (`AgentMemoryValidator.cs`).
  - `Planning/`: GOAP domain, A* graph search, and plan preemption validation (`GoapDomainValidator.cs`, `GoapPlannerValidator.cs`, `PlanExecutionValidator.cs`).
  - `Drives/`, `Navigation/`, `Sociopolitical/`, `Economy/`, `Persistence/`, `UI/`, `Diagnostics/`: Domain validators per feature area.

- **`Assets/Tests/PlayMode/`**
  Unity Test Framework PlayMode scenarios (`SwarmPlayModeTests`, `Scenarios/`). This is where **live-simulation** acceptance scenarios belong (see Definition of Done). Each scenario names the session it runs on (`ISimulationScenario.SessionId`): DoD and anomaly scenarios use `OutpostSession` (Floor 1); `PenCanaryScenario` and `ResourceContentionScenario` use the Dev Map. Scenarios that drive the dashboard (`SessionSwitchScenario`, `MapHudScenario`, `PlayerScoutScenario`, `QuickSaveButtonsScenario`) create it themselves, because loading the test scene destroys the boot one; they assert view state, never pixels (batch mode has no display). `OutpostLifecycleScenario` is the outpost's whole day (Epic MAP DoD) and logs a per-crawler report from `AgentState.Activity`; `DeterminismScenario` compares same-seed runs with `WorldStateHash`; `GoldenWorldHashScenario` compares a run with the committed golden baselines (Epic CORE): a change that must not alter behaviour must reproduce them, and a deliberate behaviour change re-records them in its own commit with `scripts/golden-hashes.sh record` (`check` runs the three baseline seeds, one Unity process each). `SaveLoadScenario` is the save/load DoD: the outpost saved mid-bounty, loaded into a fresh session, then played on; `SessionCaptureScenario` and `SessionRestoreScenario` check a live capture and round trip byte for byte. `PopulationScalingProbe` is an `[Explicit]` measurement. Run via `scripts/run-playmode-tests.sh`. The long economy soak is `[Explicit]` (`Scenario_EconomySoak_PaceBands`); run it with `scripts/run-economy-soak.sh`. It plays a fair, a greedy and a generous player (`SIMPIENS_SOAK_PROFILE`, prices set by a typed command at tick 0) and asserts each one's pace band ([`docs/economy-model.md`](docs/economy-model.md) §7). Cognition decisions land on a fixed tick (`CognitionScheduler`), so `DeterminismScenario` holds alone or in the suite. Simulation seed: `SIMPIENS_SEED` / `-simseed`.

---

## Version Control (commit discipline)
Work is saved through regular, reviewable commits. Uncommitted progress spanning several sessions is a defect.
1. **Commit at the end of every completed session or task.** Also commit after each self-contained step of a multi-phase plan (a phase of `.agents/*_plan.md`, a fix plus its validator, a docs reconciliation). Never end a session with completed work only in the working tree.
2. **Commit a working state.** The `Simpiens` and `Simpiens.Tests.PlayMode` assemblies must compile. State the test status honestly in the commit body (e.g. "PlayMode: 10/14 pass; 4 known demo-pen anomaly failures"). Never claim tests passed that were not run.
3. **Keep commits logical and scoped.** Separate dependency/tooling changes, feature code and docs where practical. Use the existing Conventional Commits style: `feat(scope):`, `fix(scope):`, `docs(scope):`, `chore(scope):`, `test(scope):`.
4. **Always commit `.meta` files with their assets.** Never commit generated artifacts: `Library/`, `Temp/`, `Logs/`, `obj/`, `Screenshots/`, `ProfilerCaptures/`, `*.csproj`, `*.sln`/`*.slnx`. `.gitignore` covers these. If a new kind of generated output appears, extend `.gitignore` in the same commit.
5. **Branching:** follow the user's instruction. When none is given, do not commit large multi-session work directly to `main`; use a descriptive branch (`feat/<topic>`) and say so.
6. **Never** force-push, rewrite published history, or commit secrets/credentials.

## Documentation Protocol (read before changing status or scope)
1. **Roadmap first.** When an epic's status or scope changes, update `docs/ROADMAP.md` *first*, then the epic's `Status` header in `docs/epics/`, then `.agents/curentfocus.txt`.
2. **Use the status vocabulary** defined in the roadmap §0: `QUEUED`, `ACTIVE`, `PAUSED`, `MECHANISM COMPLETE`, `COMPLETED`.
3. **Never restate status in this file or in code comments.** Link to the roadmap.
4. **Acceptance checklists list only delivered work.** Anything dropped or postponed goes under an explicit **Deferred** heading naming the epic that owns it.
5. `.agents/architecture_history.md` is an append-only decision log. Add entries; don't rewrite history.
6. **Plans in `.agents/*_plan.md` are working documents.** Keep only the plans for work in progress. When a plan's work is complete and its docs are reconciled:
   - move its open findings to the epic doc;
   - move data still needed for a decision (soak numbers, measurements) to `docs/`;
   - summarise its decisions and deviations in `architecture_history.md`;
   - then delete the plan, and link nothing to it. Git keeps it (`git log --diff-filter=D -- .agents/`).

## Definition of Done
**"Done" means the feature *emerges in the live simulation*, not only that a validator passes.**

- Headless validators prove a mechanism in isolation, usually by **injecting preconditions** (hand-placed inventory, teleported agents, forced intents, pre-claimed bounties). That is a *mechanism test*, not evidence the game works.
- Before an epic/slice may be marked `COMPLETED`, add at least one **PlayMode scenario** (`Assets/Tests/PlayMode/`) that runs on the real session setup, injects **none** of the outcome's preconditions, and asserts a concrete outcome within a bounded tick count (e.g. *"within N ticks a posted Gather bounty is claimed, fulfilled and paid out"*).
- With only validators passing, the status is `MECHANISM COMPLETE`.
- Full criteria: `docs/ROADMAP.md` §1B.

## Multiplayer-Readiness (binding for new simulation work)
Multiplayer is a **required post-MVP feature**. The MVP is single-player, but do not make choices that prevent an authoritative client-server model later. In short (full list: `docs/ROADMAP.md` §1A.1):
- Authoritative state advances on **fixed logical ticks** (`ISimulationClock`); express durations/cooldowns in **ticks**, not `Time.deltaTime`/`Time.time`.
- Actions enter the simulation only as **typed, serializable commands/intents** applied in a **deterministic order** (not thread-pool completion order, not `Dictionary` enumeration order).
- Simulation randomness uses a **seeded** per-simulation RNG — no `UnityEngine.Random` or unseeded `System.Random` in simulation decisions.
- Authoritative state is **plain data** that can exist without a scene; views read it and never own it.
- **Single writer:** background threads read immutable snapshots/copies; all mutation happens on the simulation thread.

## Known Deviations (do not copy these patterns)
The code currently violates some of the rules above and below in places (singleton `GoapPlanner` behind a lock). The full table is `docs/ROADMAP.md` §1A.2. When touching these areas, don't extend the deviation; follow the rules, and flag the conflict rather than silently entrenching it.

---

## Logging (binding; full rules in `docs/ROADMAP.md` §1A.3)
- Use `SimLog.<Channel>.ZLog<Level>($"...")` and never `Debug.Log` in application code. Validators log through `TestLog` and fail by throwing, so any test framework can run them.
- Build nothing outside the interpolation holes: a disabled call must allocate nothing. Gate per-agent decision traces with `SimLog.Traces(channel, level, agentId)` and label entities with `SimLog.Agent(id)`.
- Levels:
  - **Error:** broken invariants.
  - **Warning:** rejected or degraded operations.
  - **Information:** player-meaningful events.
  - **Debug:** per-agent decisions.
  - **Trace:** per-candidate detail.
- Enable verbose output with `SIMPIENS_LOG="Planning=Debug;Console=Warn" SIMPIENS_LOG_FILE=Logs/sim.log SIMPIENS_LOG_FOCUS=Crawler`, or the `-simlog*` command-line args.
- `OutpostSupplyLoopScenario` writes a crawler decision trace to `Logs/outpost-supply-loop.log`.

## Architecture Constraints Reminder
- **Composition over Inheritance**: Utilize C# Interfaces and VContainer DI.
- **No MonoBehaviours for Logic**: Game objects are dumb views.
- **Zero Allocations in Hot Paths**: Use Structs, object pooling, and standard `for` loops in tick-critical areas.
- **Dual-Loop System**: Use `SimulationManager` for the fixed logical tick, and `AgentCognitionService` for background cognition. Off-thread evaluation writes nothing on `AgentState`: it returns its decision, and the `CognitionScheduler` applies it on the main thread.
- **Defensive Null-Safety for Test Isolation**: Core domain services (`SimulationManager`, `AutonomousAgent`, `AgentCognitionService`) must be null-safe regarding their injected interfaces (`ISpatialPartition`, `IWorldRegistry`, `ISimulationClock`). Domain validators in `Assets/Scripts/Testing/` instantiate these services directly in isolation with `null` dependencies. Never dereference an injected dependency without a null guard or fallback (e.g., `_spatialPartition == null` returns unblocked/empty, `_clock == null` defaults to tick 0).


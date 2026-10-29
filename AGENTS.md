# AGENTS.md

Welcome to the **Simpiens** project! This document outlines the simulation's architectural layout, guiding automated agents on where to find, modify, or extend code.

> **Source of truth:** [`docs/ROADMAP.md`](docs/ROADMAP.md) is the highest source of truth for epic/milestone **status**, scope, sequencing, engineering invariants and the Definition of Done. This file explains *how to work in the code*; it deliberately does **not** track status. If this file and the roadmap disagree, the roadmap wins — fix this file.

If you are an agent then please check the .agents/ directory for rules or any current *directives* the developer has issued for your current session.

## Current Project Structure
Simpiens is a .NET 10 solution, `dotnet/Simpiens.slnx`. (It began as a Unity project; Unity was removed in Epic CLIENT M2.9, and its history is in git.)

| Project | Holds |
| :--- | :--- |
| `Simpiens.Core` | The engine-free simulation: `Cognition/`, `Simulation/` (incl. `SimulationManager.cs`), `Session/` (sessions, layouts, `SessionHost`), `Intervention/`, `Narrative/`, `Diagnostics/`. |
| `Simpiens.Testing.Core` | Test-only, never shipped: the engine-free validators, fixtures, the Dev Map session, `WorldStateHash` and `GoldenWorldHash`. |
| `Simpiens.Hosting` | Registrations for `Microsoft.Extensions.DependencyInjection`, `LocalSimulation` (the simulation in this process), and the harness the tests and the console share: the economy soak (`EconomySoak`), the Definition-of-Done observer `OutpostWatch`, the `AnomalyDetector` (jitter, stacking, wall penetration, boundary breaches, state thrashing, judged per tick) and the `EdgeCaseInjector`. |
| `Simpiens.Host` | The `simpiens` development console: `run`, `sweep`, `soak`, `golden`. It uses the test harness and never ships. |
| `Simpiens.Core.Tests` | xUnit v3: the engine-free validators, the golden hashes, determinism, and the live scenarios headless (the outpost's day on 23 gate seeds, the supply loop, save and load, drifters, hazards, the Dev Map pens, walls dropped into doors, bunks, price response, live saves). |
| `Simpiens.Presentation` | What the game client draws, as data, with no XNA reference (see below). |
| `Simpiens.Client` | The KNI game client (see below). |
| `Simpiens.Desktop` | The desktop exe; Debug builds add the test harness's Dev Map to the session switch. |
| `Simpiens.Client.Tests` | xUnit v3: presentation tests, golden hashes from inside the client, render tests against images in its `Baselines/`, picture pins (`PicturePinTests`) that hash what is drawn finer than the render tolerance, which hides a crawler-sized change, and what the player sees (`ThrashingTests`). |
| `Simpiens.FontBaker` | A developer tool, never shipped: bakes `FontSet`'s faces of Inter into BMFont atlases under Gum's cache names in `Simpiens.Client/Content/FontCache`, which are committed; text is drawn 1:1 from them, never scaled. |
| `Simpiens.Android` | Compile-only until M4; outside `Simpiens.slnx`, because it needs the Android workload. |

**The game client** (Epic CLIENT):
- **`Simpiens.Presentation`**, what is drawn, as data:
  - generated sprites (`ProceduralSprites`, `IsometricSprites`, named pixels) and map tiles;
  - `Art/`: the sprite sheets (`SpriteSheets`: what each `DrawSheet` holds; a sheet alone gives out sprite indices) and the art as data: an `ArtCatalog` per view mode (`TopDownArtTable`, `IsometricArtTable`) maps art keys (`ArtKeys`: `tile/wall`, `prop/hearth`, `crawler/<state>`, `node/<item sprite key>`) to frames, sizes, pivots and pulses, resolved to `ArtId`s once; `EntityVisuals` says which art each entity of a frame is drawn with. New art for a known kind of thing is a table row;
  - the projections (`IViewProjection`: `TopDownProjection`, `IsometricProjection`). A `View` is a projection through a camera, and everything that maps world to screen takes one; `View.FloorToScreen` is its affine transform, under which what lies on the floor (`DrawItem.OnFloor`: the fog's cloud) is drawn;
  - view modes (`ViewMode`: a projection with its art catalogue). Top-down's is the original art; isometric's stands the generated placeholders of `IsometricSprites` on their feet. Isometric is the game's view; V or `--view top` switches to top-down. Walls in front of rooms are cut away (`WallStyle`; C cycles);
  - screen space for hard cutscenes (`ScreenProjection`: a 640×360 virtual screen, letterboxed), and `Cutscenes/`: cutscenes written in code (`CutsceneBuilder`, `CutsceneScript`), played by `CutscenePlayer`. Hard ones are drawn by `CutsceneStage`; dynamic ones are started by `SceneDirector` from what the frames saw (a crawler entering a zone, or a chronicle interaction), once per tick;
  - the draw list and its sort key (draw roles; the projection picks their layers);
  - the views' per-tick frames (`WorldViewFrame`, captured on `SessionHost.TickCompleted`). The views read frames, never the live simulation, and draw crawlers between ticks;
  - crawler appearance (`CrawlerAppearance`, which holds each state at least 3 ticks); the fog, which hides what an uncharted sector holds, crawlers included (producer decision D1 = C);
  - the map's labels, laid out in screen space from the baked fonts (`MapLabelLayout`, `BitmapFontMetrics`); the camera rig and its controls; selection and picking (clicks pick what was drawn); PNG files;
  - `Dashboard/`: the dashboard as data, whose actions leave as typed commands.
- **`Simpiens.Client`**, the XNA shell:
  - `SimpiensGame`: window, device, content (`ClientGraphics`: the sheets, renderer and fonts every screen shares), the Gum layer, and a stack of screens (`ScreenStack`). `PlayScreen` is the world view with the dashboard and dynamic scenes (`DynamicSceneView`); `CutsceneScreen` is a hard cutscene, which holds the world. `Restart` starts a new session in the same window (the tests reuse windows: an offscreen game can't be disposed);
  - `ClientSession`, what is played, with no XNA type: the simulation and its driver, the frames, the selection, the dashboard model, the save slot, and the host's commands (speed, pause, save, load and the session switch are `HostCommand`s the views queue and the session applies before its ticks);
  - the input, read once per update into an `InputFrame` (keys become `InputAction`s through `KeyBindings`) that feeds both the top screen and Gum's cursor (`Ui/ClientCursor`);
  - `FrameTimings`, each frame's phases (F3 in Debug builds);
  - `WorldView`, how the world is drawn (projection, camera rig and controls, the scene, the map's text, picking): its state moves on in `Update`, and a draw only presents it;
  - the renderer, and `Ui/`: the dashboard in Gum, views over the dashboard models that decide nothing.
- **Rules:** views read the simulation and never write it (a test enforces it), and only `XnaPlatform` may use an API that KNI and MonoGame don't share (`-p:XnaFramework=MonoGame` compiles the client against MonoGame to check).

Namespaces kept the names of the Unity era (`Simpiens.Cognition`, `Simpiens.Simulation`, `Simpiens.Core.Session`, `Simpiens.Entities` for the visual-state enum, …). The directory notes below use the folder names inside `Simpiens.Core` and `Simpiens.Testing.Core`. Every project targets `net10.0` with its default C#, C# 14: .NET 10 is the newest .NET KNI supports (its Android platform targets `net10.0-android`), and the client references the core, so the solution moves to a newer .NET together, when KNI does.

### Checking a change

| Check | Command | Needs |
| :--- | :--- | :--- |
| All tests (validators, golden hashes, the live scenarios, the soak, the client's presentation, golden-hash and render tests) | `dotnet test dotnet/Simpiens.slnx` | .NET 10 SDK; for the client's tests, system SDL2 and Mesa (they run SDL's offscreen driver on llvmpipe, no display) |
| Console host | `dotnet run --project dotnet/Simpiens.Host -- run`, `sweep --seeds 1..20`, `soak`, `golden check` (`-- help` lists the options) | .NET 10 SDK |
| Golden hashes | `dotnet run --project dotnet/Simpiens.Host -- golden check`; re-record only for a deliberate behaviour change, in its own commit: `golden record --out Baselines/golden-world-hash.txt --seeds 424242,20261009,20261010` | .NET 10 SDK |
| Art pins: every sprite's size and pixels, by name (`Baselines/art-pins.txt` in `Simpiens.Client.Tests`) | Part of `dotnet test`, which also writes the art review sheet, every sprite named: `render-output/art-sheet.png` under the client tests' output (`container-output/render-output/` from the pinned image). After a deliberate art change, look at the sheet, then, in its own commit: `SIMPIENS_RECORD_ART=1 dotnet test dotnet/Simpiens.Client.Tests --filter ArtPinTests`, and the renders re-recorded in the pinned image | .NET 10 SDK (exact on any machine: the pixels are generated on the CPU) |
| The client's tests in the pinned image, as CI runs them: renders must match the references byte for byte | `scripts/test-client-in-container.sh`; record changed renders only here, with `--record` (look at `container-output/render-output/` first, commit them on their own) | podman (rootless), or `CONTAINER_ENGINE=docker` |
| The game (desktop) | `dotnet run --project dotnet/Simpiens.Desktop -- --seed 424242` (`-- --help` lists the options) | A display |
| The client against MonoGame (compile only) | `dotnet build dotnet/Simpiens.Desktop -p:XnaFramework=MonoGame` | .NET 10 SDK |
| Re-bake the fonts after changing `FontSet` (commit the result on purpose; a test compares the committed atlases with a fresh bake) | `dotnet run --project dotnet/Simpiens.FontBaker` | .NET 10 SDK (KernSmith with FreeType, Linux x64 or Windows) |
| The Android client (compile only) | `scripts/build-android.sh` | A user-local toolchain: `scripts/setup-android-toolchain.sh` installs a .NET SDK with the android workload, a JDK and the Android SDK under `~/.cache/simpiens-toolchains`, with no sudo |
| Measurements (explicit tests): the client's frame time by phase (`FrameTimeGateTests` is the gate, with a generous budget), and the simulation's throughput by population | `dotnet dotnet/Simpiens.Client.Tests/bin/Release/net10.0/Simpiens.Client.Tests.dll -method "*MeasureFrameTime*" -explicit only -showLiveOutput`; `dotnet dotnet/Simpiens.Core.Tests/bin/Release/net10.0/Simpiens.Core.Tests.dll -method "*PopulationScaling*" -explicit only -showLiveOutput`; whether a moving crawler shimmers, by view mode: `-method "*ShimmerProbe*"` on the client's tests | As the tests |

CI (`.github/workflows/dotnet.yml`) runs the tests (the client's in the pinned image of `scripts/client-tests.Containerfile`), the golden check, the MonoGame compile and the Android compile on pushes to `main` and on pull requests.

### Directories (`Simpiens.Core`)

- **`Cognition/`**
  Houses the asynchronous cognitive reasoning engine for agents.
  - `Contracts/`: Interfaces and data structures for Agent Intents (`AgentIntent`, `WanderIntent`, `TradeIntent`, `ClaimBountyIntent`, …), Intent Execution (`IIntentHandler`, `IIntentExecutionContext`, `IIntentHandlerRegistry`), and Context (`AgentContext`, `AgentNeeds`, `IntentResult`).
  - `Evaluators/`: Tier-1 reflexive/utility evaluators (`SurvivalUtilityEvaluator` — hunger reflex only, returns `null` otherwise; `MentalBreakEvaluator`, with the danger reflexes; `AltruismEvaluator`, `FrustrationEvaluator`) and the Tier-3 ambient fallback `AmbientWanderEvaluator` (passed to `AgentCognitionService`).
  - `Planning/`: GOAP domain — zero-allocation bitmask `WorldState`/`StateFact`, `GoapPlanner` (A*), `ActionGraph`, `Actions/`, `Goals/`, `WorldStateBuilder`, and modular intent handlers under `Handlers/` (`IntentHandlerRegistry`, `WanderIntentHandler`, `PanicIntentHandler`, `HarvestResourceIntentHandler`, `ShareResourceIntentHandler`, `IdleIntentHandler`, `TradeIntentHandler`, `ClaimBountyIntentHandler`, `DeliverBountyIntentHandler` (Gather + Scout, escrow-only payout; a scout's report publishes its survey), `ScoutIntentHandler`, `SleepAtBunkIntentHandler`, `RetreatIntentHandler`, `TreatWoundsIntentHandler`), plus shared `ResourceTargeting` / `BountyQueries` / `BunkTargeting` helpers, the utility `Considerations` (personality-weighted priorities, reservation/minimum prices, bounty score) and `Domain/OutpostGoapDomain` (the production action graph and goal list).
  - `Memory/`, `Social/`: Fog-of-war spatial memory, double-buffered snapshots (`IAgentMemory`, `AgentMemorySnapshot`), event ledger, gossip, 4-axis affinity (`AgentMemory`, `AffinityRecord`).
  - `Groups/`: Generic group primitive, directives, compliance, succession (`SuccessionEngine`, `ComplianceUtility`).
  - `Inventory/`: `AgentInventory` (fixed-capacity item slots + gold, double-buffered `AgentInventorySnapshot`), and the worn gear beside the pack (`Gear`, Epic 10 G3: hazard gear, whose durability counts the ticks of hazard exposure it has left).
  - `Pathfinding/`: Zero-allocation 2D Grid A* navigation engine with binary min-heap, epoch tracking, diagonal corner-cutting prevention, and raycast smoothing (`AsyncPathfinder`, `GridAStarSearcher`, `PathRequest`, `PathResponse`).
  - **Key Files**: `AgentState.cs` (pure C# state, needs & drives, health: Epic 10 G2), `Vitality.cs` (health's rules: hazard damage, downed at 0, healing in bunks and from herbs, who ventures into danger; worn gear cuts the damage and wears out in `AgentRuntime`, Epic 10 G3), `AgentCognitionService.cs` (cognition: main-thread preparation, off-thread evaluation returning a `CognitionResult`), `CognitionScheduler.cs` (decisions dispatched on tick T land at T + 1 in agent-id order, on the main thread), `AgentConsequenceService.cs` (intent consequences & rewards), `ToolExecutionRegistry.cs` (MCP tool execution bridge), `AgentRuntime.cs` (a crawler's tick).
  - A crawler is plain data (Epic CORE C5): its `AgentState`, its `WorldEntity` in the world and its `AgentRuntime` (the per-tick drives, pre-emption, decision dispatch and perception while busy). Views only read it.

- **`Session/`**
  Sessions as code.
  - An `ISessionDefinition` is a map plus `Populate(SessionBuilder)` (agents, relationships, groups, stalls, seeded bounties). Its `Economy` (`EconomyTuning`) is applied on every respawn, before `Populate`. `OutpostSession` is the live game.
  - `SessionHost` builds any session (loads the map through `IWorldMap`, spawns its keyed nodes, bunks and hearths, calls `Populate`, records the ledger baseline, registers seeded bounties), ticks every crawler after the simulation's tick and then raises `TickCompleted` (where views capture what they draw), and captures and restores it.
  - Every respawn rewinds the clock to tick 0: express seeded expiries relative to the session start. A respawn also starts a new session epoch: an intent stamped with an older `AgentIntent.SessionEpoch` is dropped, because entity ids repeat per seed. The default (outpost) session is picked by id.
  - The host also admits **drifters** (Epic 10 G1, `SessionHost.Drifters.cs`): strangers on a seeded schedule (`EconomyTuning.Drifters`, `DrifterTuning`) who come in at the map's spawn points with a purse (the `DrifterPurses` faucet), live as crawlers (`AgentState.DepartsAtTick`, 0 for a resident), and leave by deciding to (`LeaveOutpostGoal`, `SimulationManager.DepartOutpost`: their gold leaves as `DrifterDepartures`); the host removes them at the end of the tick and raises `CrawlerDeparted`. Checks that judge "every crawler" judge the residents.
  - `SessionHost.Capture` / `TryRestore` capture and load whole sessions (see `Simulation/Persistence/`); `SaveGameService` is the player's quick-save slot.
  - `Layouts/`: map definitions as code. `Floor1Layout` is the live outpost floor (sectors, hazards, doors, POIs, nodes, spawn points, bunks).

- **`Intervention/`**
  Player intervention system acting as an influence queue.
  - **Key Files**: `PlayerInterventionService.cs`, `InfluenceEvent.cs` (zero-allocation typed command union: `PostBountyCommand`, `CancelBountyCommand`, `UpdateStallPricesCommand`, `CalmCrawlerCommand`).

- **`Simulation/`**
  The simulation environment, registries, and spatial representations.
  - `Primitives/`: engine-free value types in the root namespace `Simpiens`: `EntityId`, the id of every simulation entity; and the maths: `Vec2`, `GridPoint`, `RectF`, `Rgba`, `SimMath`. They copy Unity's `Vector2`, `Vector2Int`, `Rect` and `Mathf` formulas exactly (including `Vec2 ==` being approximate and rounding half to even), and float steps are cast where Unity's Mono rounded differently from CoreCLR: the golden baselines depend on both. Keep them.
  - `Spatial/`: Handles memory-managed grids (`SpatialHashGrid`, `SharedWorldSnapshot`, `EntitySnapshot`, `StaticNavGrid` with a static base layer for map walls).
  - `World/`: static geography as plain data. `MapDefinition` (+ `MapDefinitionBuilder`) is the single source of truth for walls, sectors, resource nodes, POIs and labels; `WallRasterizer` writes walls into the nav grid; `IWorldMap.Load` sizes the partition from the map and loads `ISectorRegistry` (authoritative sectors + exploration). The snapshot carries the map and the charted-sector mask: **fog of war**, nothing inside an uncharted sector is perceived until a scout's report charts it. A sector may be a **hazard** (`SectorDefinition.Hazard`, Epic 10 G2): it takes health from whoever stands in it. Never hard-code coordinates or sector ids elsewhere: read them from the layout.
  - `Items/`: the item system (item refactor I1–I8, 2026-10-26).
    - **What an item is:** `ItemCatalog`, static and immutable, holds one `ItemDefinition` per `ItemType`: its groups as `[Flags] ItemCategory`, its `ItemRarity` (a score that is its default loot weight), names, stack size, sprite key, consume effects (`ConsumeEffect` on a `StatKind`, applied by `Cognition/ItemUse`), and equipment (`EquipmentBlock`: slot, `StatModifier`s, durability and `WearRule`).
    - **Equipment and stats:** a crawler's stats with equipment come from `CrawlerStats`. `AgentInventory` has three equipment slots beside the pack (protection, weapon, tool).
    - **Loot:** loot tables (`Loot.cs`: `LootTable`, `LootTables`, `LootRoller`) are deterministic on `SimRandom` and not yet wired into play.
    - **Code asks for groups, not items:** `ItemCatalog.Is`, `InCategory`, `InventoryGroups`. `ItemNamesTests` fails on production code that names an item outside content.
    - **How a session trades an item is its economy:** `EconomyTuning`'s `ItemTerms` (base value; the stall's role, opening price and stock) and `SupplyTuning`'s `CaravanTerms` (exports or supplies). A stall's goods are a table (`TradeStallData`'s `StallGoods`), and a price command carries (item, price) pairs.
    - **The client's words for items** are `Simpiens.Presentation/ItemTexts` (an item without an entry gets words from its catalog name).
    - **A save** holding an item the catalog doesn't define is refused (`SaveItems`).
  - `Economy/`: Unmanaged economy structs (`ItemInstance`, `TradeStallData`, `BountyContract`), authoritative gold tracking (`IEconomyLedger`, `EconomyLedger`), the session's economy numbers (`EconomyTuning`, with `SupplyTuning`, `DrifterTuning` and `GearTuning` nested), the trade fairness rule (`TradeFairness`), and the periodic economy (`ISupplyService`, `SupplyService`). The periodic economy covers dormant-node regrowth and the caravan visit: salvage quota faucet, then upkeep sink, then ration restock. `SimulationManager` ticks it via `ISupplyHost`. The model is in [`docs/economy-model.md`](docs/economy-model.md). Economy numbers live in `EconomyTuning` only: read them from the session's tuning (`IIntentExecutionContext.Economy`, or `Considerations.Economy` in cognition), never as literals.
  - `Persistence/`: session save and load.
    - `SessionSave` is a whole session at one tick: a header, then one block per owner (crawlers, economy, world, groups, chronicle). Each owner writes and reads its own block (`CaptureTo` / `RestoreFrom`, `AgentState.CaptureRecord` / `RestoreRecord`); `SessionHost.Capture` / `TryRestore` assemble them. Keyed records are sorted by id, so a capture doesn't depend on dictionary order.
    - **New authoritative state goes into its owner's block,** with a check in the round-trip validators (`SessionCaptureValidator`, `SessionRestoreValidator`). A round trip only proves what a capture contains: state left out of the save is lost on load without any test failing.
    - Not saved: work in progress (intents, plans, and the bunk claims and contention tied to them: crawlers decide afresh after a load), derived state (snapshots, grids, the agent and world registries), and what the session definition provides (the map and the economy tuning).
    - A load rebuilds the map exactly as a respawn does (same ids for the map's entities), then restores in place of `Populate`. It refuses, before changing anything, a save with another format version, session, seed or map (`SaveFingerprint`).
    - Saves are captured and loaded on the main thread between ticks, never from cognition. Nothing a save reads is written off-thread.
    - `SaveFile` is the file format: a plain header (magic, format version, length, checksum), then the save, so a wrong, cut-off or damaged file is refused with a reason. `SaveGameService` (`Session/`) is the quick-save slot, `Saves/quicksave.simsave` under the per-user application data folder; tests give it their own folder.
    - `SaveSerializer` is the only code that calls MemoryPack.
    - Unmanaged records are saved as raw memory copies, so changing a saved struct's layout changes the save format. When that happens, bump `SessionSaveHeader.CurrentFormatVersion` (`EconomySerializationValidator` pins the economy records' sizes).
  - **Key Files**: `SimulationManager.cs` (the main-thread tick: an orchestrator implementing `IIntentExecutionContext`, delegating intent execution to `IIntentHandlerRegistry`, draining typed `InfluenceEvent`s, trade/bounty/contention rules, hearth dynamics), `WorldEntity.cs` (an entity as plain data: id, kind, position, radius, speed, resource kind, its `AgentState` for crawlers), `WorldRegistry.cs` (entities in registration order, which is the snapshot's order; `EntityRegistered`/`EntityUnregistered` let views show and hide), `GroupRegistry.cs`, `SimulationClock.cs`, `ISimulationManager.cs`.

- **`Diagnostics/`**
  Logging façade over ZLogger (`SimLog`, `SimLogSettings`, `SafeSinkLogProcessor`, `TickStampedFileLogProcessor`). One `ILogger` per channel (`SimLog.Planning`, `SimLog.Economy`, …). The host plugs its console in (`SimLog.ConsoleSinkFactory`); it is a silent `NullLogger` until configured (validators). Rules: `docs/ROADMAP.md` §1A.3.

- **`Narrative/`**
  Chronicle pipeline: `NarrativeChronicleService`, `TemplateNarrativeFormatter`, `ILlmNarrativeEnricher` (LLM hook; passthrough by default).

- **`Simpiens.Testing.Core`** (test-only, never shipped)
  Domain validators and fixtures. Validators do **not** run at boot; `ValidatorTests` runs each. They log through `TestLog` and fail by throwing.
  - `Environment/`: `HeadlessEntities` (validators build crawlers and props as records), environment and determinism validators, `WorldStateHash` (a hash of the authoritative world, with per-part descriptions that name what differs) and `GoldenWorldHash` (the golden baselines in `Baselines/golden-world-hash.txt`).
  - `Sessions/`: the test-only **Dev Map**: `CanaryPenSession` (the 10 scripted demonstration pens) on `DevMapLayout`, reachable in Debug builds through the dashboard's session switch. Pen code never goes back into production.
  - `World/`: map validators (`MapDefinitionValidator` incl. Floor1 and Dev Map checks, `WorldMapValidator`, `SectorChartingValidator` for the fog of war), `TestWorlds` fixtures and `CommonsYardFixture` (an open yard for validators that hand-place agents with no walls between sectors).
  - `Memory/`, `Planning/`, `Drives/`, `Navigation/`, `Sociopolitical/`, `Economy/`, `Persistence/`, `UI/`, `Diagnostics/`: Domain validators per feature area.

- **Live scenarios** (`Simpiens.Core.Tests`)
  The Definition of Done's evidence, headless on the real session with nothing injected (`OutpostScenarioTests` shows how): `OutpostWatch` observes every tick (ledger conservation, starvation, the fog, the bounties, trust, the anomaly detector, intent handlers that threw). `TheOutpostsDayHoldsOnEverySeed` plays the 15-minute day on the 23 gate seeds; `GoldenWorldHashTests` compares a run with the committed baselines: a change that must not alter behaviour must reproduce them, and a deliberate behaviour change re-records them in its own commit. Simulation seed: `SIMPIENS_SEED` / `--seed`.

### Adding or removing an item
- **Add:**
  1. An `ItemType` value. Never reuse one: a removed item's id stays reserved.
  2. A definition in `ItemCatalog`: groups, rarity, and its consume or equipment block if it has one.
  3. Its terms in the session's economy, if the stall or the caravan trades it (`EconomyTuning`'s item terms, `SupplyTuning`'s caravan terms).
  4. Its art, found by its art key (`ItemDefinition.ArtKey`, the name in kebab case): a node row if a map node yields it.
  5. The map nodes that yield it.
  6. Optionally, bespoke words in `ItemTexts`.
  - Nothing else: the stall, the caravan, the crawlers and the merchant terminal follow (`ItemSystemProofTests` adds one this way).
  - If the stall's goods would pass `TradeStallData.GoodsCapacity` (8), raise it: that changes the save format.
- **Take out of the economy:** drop its stall and caravan terms (glowstone was taken out this way in `ItemSystemProofTests`).
- **Remove from the game:** delete its definition and mark its `ItemType` value retired; saves holding it are refused on load.

---

## Version Control (commit discipline)
Work is saved through regular, reviewable commits. Uncommitted progress spanning several sessions is a defect.
1. **Commit at the end of every completed session or task.** Also commit after each self-contained step of a multi-phase plan (a phase of `.agents/*_plan.md`, a fix plus its validator, a docs reconciliation). Never end a session with completed work only in the working tree.
2. **Commit a working state.** The solution builds, with no warnings: warnings are errors (`dotnet/Directory.Build.props`), so fix a new one rather than suppress it. State the test status honestly in the commit body (e.g. "dotnet test 372/372; golden 3/3"). Never claim tests passed that were not run.
3. **Keep commits logical and scoped.** Separate dependency/tooling changes, feature code and docs where practical. Use the existing Conventional Commits style: `feat(scope):`, `fix(scope):`, `docs(scope):`, `chore(scope):`, `test(scope):`.
4. **Never commit generated artifacts:** `bin/`, `obj/`, `Logs/`, `container-output/`. `.gitignore` covers these. If a new kind of generated output appears, extend `.gitignore` in the same commit.
5. **Branching:** follow the user's instruction. When none is given, do not commit large multi-session work directly to `main`; use a descriptive branch (`feat/<topic>`) and say so. Target pull requests at `main`, not at another feature branch.
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
- Before an epic/slice may be marked `COMPLETED`, add at least one **live scenario** in `dotnet/Simpiens.Core.Tests` that runs on the real session setup, injects **none** of the outcome's preconditions, and asserts a concrete outcome within a bounded tick count (e.g. *"within N ticks a posted Gather bounty is claimed, fulfilled and paid out"*). A scripted player that acts only through typed player commands is input, not injection.
- With only validators passing, the status is `MECHANISM COMPLETE`.
- Full criteria: `docs/ROADMAP.md` §1B.

## Multiplayer-Readiness (binding for new simulation work)
Multiplayer is a **required post-MVP feature**. The MVP is single-player, but do not make choices that prevent an authoritative client-server model later. In short (full list: `docs/ROADMAP.md` §1A.1):
- Authoritative state advances on **fixed logical ticks** (`ISimulationClock`); express durations/cooldowns in **ticks**, never frame time.
- Actions enter the simulation only as **typed, serializable commands/intents** applied in a **deterministic order** (not thread-pool completion order, not `Dictionary` enumeration order).
- Simulation randomness uses a **seeded** per-simulation RNG (`SimRandom`) — no unseeded `System.Random` in simulation decisions.
- Authoritative state is **plain data**; views read it and never own it.
- **Single writer:** background threads read immutable snapshots/copies; all mutation happens on the simulation thread.

## Known Deviations (do not copy these patterns)
The code currently violates some of the rules above and below in places (singleton `GoapPlanner` behind a lock). The full table is `docs/ROADMAP.md` §1A.2. When touching these areas, don't extend the deviation; follow the rules, and flag the conflict rather than silently entrenching it.

---

## Logging (binding; full rules in `docs/ROADMAP.md` §1A.3)
- Use `SimLog.<Channel>.ZLog<Level>($"...")` and never `Console.WriteLine` in application code. Validators log through `TestLog` and fail by throwing, so any test framework can run them.
- Build nothing outside the interpolation holes: a disabled call must allocate nothing. Gate per-agent decision traces with `SimLog.Traces(channel, level, agentId)` and label entities with `SimLog.Agent(id)`.
- Levels:
  - **Error:** broken invariants.
  - **Warning:** rejected or degraded operations.
  - **Information:** player-meaningful events.
  - **Debug:** per-agent decisions.
  - **Trace:** per-candidate detail.
- Enable verbose output with `SIMPIENS_LOG="Planning=Debug;Console=Warn" SIMPIENS_LOG_FILE=Logs/sim.log SIMPIENS_LOG_FOCUS=Crawler`.

## Architecture Constraints Reminder
- **Composition over Inheritance**: C# interfaces and constructor injection; the core is container-agnostic, and `Microsoft.Extensions.DependencyInjection` composes it in the hosts (`AddSimpiensCore`).
- **Views decide nothing**: the client's views and dashboard models read frames and send typed commands.
- **Zero Allocations in Hot Paths**: Use Structs, object pooling, and standard `for` loops in tick-critical areas.
- **Dual-Loop System**: Use `SimulationManager` for the fixed logical tick, and `AgentCognitionService` for background cognition. Off-thread evaluation writes nothing on `AgentState`: it returns its decision, and the `CognitionScheduler` applies it on the main thread.
- **Defensive Null-Safety for Test Isolation**: Core domain services (`SimulationManager`, `AgentRuntime`, `AgentCognitionService`) must be null-safe regarding their injected interfaces (`ISpatialPartition`, `IWorldRegistry`, `ISimulationClock`). Validators instantiate these services directly in isolation with `null` dependencies. Never dereference an injected dependency without a null guard or fallback (e.g., `_spatialPartition == null` returns unblocked/empty, `_clock == null` defaults to tick 0).

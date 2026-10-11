# AGENTS.md

Welcome to the **Simpiens** project! This document outlines the Unity simulation's architectural layout, guiding automated agents on where to find, modify, or extend code.

> **Source of truth:** [`docs/ROADMAP.md`](docs/ROADMAP.md) is the highest source of truth for epic/milestone **status**, scope, sequencing, engineering invariants and the Definition of Done. This file explains *how to work in the code*; it deliberately does **not** track status. If this file and the roadmap disagree, the roadmap wins — fix this file.

## Current Project Structure
The `Assets/Scripts/` directory contains the core simulation logic, strongly emphasizing a code-first, decoupled approach where heavy cognitive logic runs asynchronously off the main Unity loop.

### Directories

- **`Cognition/`**
  Houses the asynchronous cognitive reasoning engine for agents.
  - `Contracts/`: Interfaces and data structures for Agent Intents (`AgentIntent`, `WanderIntent`, `TradeIntent`, `ClaimBountyIntent`, …), Intent Execution (`IIntentHandler`, `IIntentExecutionContext`, `IIntentHandlerRegistry`), and Context (`AgentContext`, `AgentNeeds`, `IntentResult`).
  - `Evaluators/`: Tier-1 reflexive/utility evaluators (`SurvivalUtilityEvaluator` — hunger reflex only, returns `null` otherwise; `MentalBreakEvaluator`, `AltruismEvaluator`, `FrustrationEvaluator`) and the Tier-3 ambient fallback `AmbientWanderEvaluator` (passed to `AgentCognitionService`).
  - `Planning/`: GOAP domain — zero-allocation bitmask `WorldState`/`StateFact`, `GoapPlanner` (A*), `ActionGraph`, `Actions/`, `Goals/`, `WorldStateBuilder`, and modular intent handlers under `Handlers/` (`IntentHandlerRegistry`, `WanderIntentHandler`, `PanicIntentHandler`, `HarvestResourceIntentHandler`, `ShareResourceIntentHandler`, `IdleIntentHandler`, `TradeIntentHandler`, `ClaimBountyIntentHandler`, `DeliverBountyIntentHandler` (Gather + Scout, escrow-only payout), `ScoutIntentHandler`), plus shared `ResourceTargeting` / `BountyQueries` helpers, the utility `Considerations` (personality-weighted priorities, reservation/minimum prices, bounty score) and `Domain/OutpostGoapDomain` (the production action graph and goal list).
  - `Memory/`, `Social/`: Fog-of-war spatial memory, double-buffered snapshots (`IAgentMemory`, `AgentMemorySnapshot`), event ledger, gossip, 4-axis affinity (`AgentMemory`, `AffinityRecord`).
  - `Groups/`: Generic group primitive, directives, compliance, succession (`SuccessionEngine`, `ComplianceUtility`).
  - `Inventory/`: `AgentInventory` (fixed-capacity item slots + gold, double-buffered `AgentInventorySnapshot`).
  - `Pathfinding/`: Zero-allocation 2D Grid A* navigation engine with binary min-heap, epoch tracking, diagonal corner-cutting prevention, and raycast smoothing (`AsyncPathfinder`, `GridAStarSearcher`, `PathRequest`, `PathResponse`).
  - **Key Files**: `AgentState.cs` (pure C# state, needs & drives), `AgentCognitionService.cs` (async cognition pipeline), `AgentConsequenceService.cs` (intent consequences & rewards), `ToolExecutionRegistry.cs` (MCP tool execution bridge), `AutonomousAgent.cs` (view/façade).
  - `AutonomousAgent` is a thin `MonoBehaviour` view delegating state to `AgentState` and cognition to `IAgentCognitionService`. Agents can be instantiated, ticked, and simulated headlessly without any `GameObject`.

- **`Core/`**
  Handles bootstrapping and Unity-specific initialization, and hosts the main-thread simulation executor.
  - **Key Files**: `AppBootstrapper.cs` (VContainer composition root; namespace `Simpiens.Core.Bootstrapping`), `SimulationStarter.cs`, `SimulationManager.cs` (main-thread tick: orchestrator implementing `IIntentExecutionContext`, delegating intent execution to `IIntentHandlerRegistry`, draining typed `InfluenceEvent`s, trade/bounty/contention rules, hearth dynamics).
  - `Session/`: `SwarmSpawner` builds the live session: loads a map through `IWorldMap`, spawns its keyed nodes/campfires/stall, then agents (crawler personalities, seeded entity ids).
  - `Session/Layouts/`: map definitions as code. `DevMapLayout` (the legacy pens + Outpost Commons, the live map until Epic MAP Phase 3) and `Floor1Layout` (the outpost floor).
  - `BootstrapExtensions.cs`: hook through which optional assemblies (the test harness) add DI registrations; production never references `Simpiens.Testing`.
  - `Editor/`: Editor-only batch/setup utilities.

- **`Routines/`**
  `IRoutine.cs` — routine abstraction used by `AgentMotor`.

- **`Entities/`**
  Contains strictly `MonoBehaviour` views that hold state or rendering instructions.
  - **Rules**: Zero logic! Only basic view management, motor controls, and configurations.
  - **Key Files**: `AgentMotor.cs`, `NodeController.cs`, `AgentView.cs`, `NodeFactory.cs`, `MapView.cs` (draws a map's walls and labels, frames the camera; walls are sprites only, never entities).

- **`Intervention/`**
  Player intervention system acting as an influence queue.
  - **Key Files**: `PlayerInterventionService.cs`, `InfluenceEvent.cs` (zero-allocation typed command union: `PostBountyCommand`, `CancelBountyCommand`, `UpdateStallPricesCommand`).

- **`Simulation/`**
  The simulation environment, registries, and spatial representations.
  - `Spatial/`: Handles memory-managed grids (`SpatialHashGrid`, `SharedWorldSnapshot`, `EntitySnapshot`, `StaticNavGrid` with a static base layer for map walls).
  - `World/`: static geography as plain data. `MapDefinition` (+ `MapDefinitionBuilder`) is the single source of truth for walls, sectors, resource nodes, POIs and labels; `WallRasterizer` writes walls into the nav grid; `IWorldMap.Load` sizes the partition from the map (never the camera) and loads `ISectorRegistry` (authoritative sectors + exploration). Never hard-code coordinates or sector ids elsewhere: read them from the layout.
  - `Economy/`: Unmanaged economy structs (`ItemInstance`, `TradeStallData`, `BountyContract`), authoritative gold tracking (`IEconomyLedger`, `EconomyLedger`), supply faucets (`ISupplyService`, `SupplyService`: dormant-node regrowth + stall supply caravan, ticked by `SimulationManager` via `ISupplyHost`).
  - **Key Files**: `WorldRegistry.cs`, `GroupRegistry.cs`, `SimulationClock.cs`, `ISimulationManager.cs`. (`SimulationManager.cs` itself lives in `Core/`.)

- **`Diagnostics/`**
  Logging façade over ZLogger (`SimLog`, `SimLogSettings`, `SafeSinkLogProcessor`, `TickStampedFileLogProcessor`). One `ILogger` per channel (`SimLog.Planning`, `SimLog.Economy`, …). Configured once in `AppBootstrapper.Awake`; it is a silent `NullLogger` until then (headless validators). Rules: `docs/ROADMAP.md` §1A.3.

- **`Narrative/`**
  Chronicle pipeline: `NarrativeChronicleService`, `TemplateNarrativeFormatter`, `ILlmNarrativeEnricher` (LLM hook; passthrough by default).

- **`UI/`**
  UI Toolkit presenters (dumb views over simulation state): `FacilityDashboardPresenter`, `Pooling/VisualElementPool`, `Zones/FacilityZoneService` (UI adapter over `ISectorRegistry`; owns no sector data).

- **`Testing/`** (separate `Simpiens.Testing` assembly, `UNITY_INCLUDE_TESTS` only: never ships in player builds)
  Domain validators and test harness services. Validators do **not** run at boot; each has a PlayMode test wrapper in `SwarmPlayModeTests`. `TestHarnessInstaller` registers the harness services (anomaly detector, recorder view, `EdgeCaseInjector`, batch controller) through `BootstrapExtensions`. Production internals are visible to it via `Assets/Scripts/AssemblyInfo.cs`.
  - `Environment/`: Chaos injectors and batch runners (`EdgeCaseInjector.cs`, `BatchPlaymodeController.cs`), plus environment/determinism validators.
  - `World/`: map validators (`MapDefinitionValidator` incl. Floor1 connectivity/doors, `WorldMapValidator`, temporary `DevMapParityValidator`) and `TestWorlds` fixtures.
  - `Memory/`: Knowledge matrix and social gossip validation (`AgentMemoryValidator.cs`).
  - `Planning/`: GOAP domain, A* graph search, and plan preemption validation (`GoapDomainValidator.cs`, `GoapPlannerValidator.cs`, `PlanExecutionValidator.cs`).
  - `Drives/`, `Navigation/`, `Sociopolitical/`, `Economy/`, `UI/`, `Diagnostics/`: Domain validators per feature area.

- **`Assets/Tests/PlayMode/`**
  Unity Test Framework PlayMode scenarios (`SwarmPlayModeTests`, `Scenarios/`). This is where **live-simulation** acceptance scenarios belong (see Definition of Done). Run via `scripts/run-playmode-tests.sh`. The long economy soak is `[Explicit]`; run it with `scripts/run-economy-soak.sh`. Simulation seed: `SIMPIENS_SEED` / `-simseed`.

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
The code currently violates some of the rules above and below in places (singleton `GoapPlanner` behind a lock, off-thread plan/intent bookkeeping in cognition, cognition result arrival tick depends on thread timing). The full table is `docs/ROADMAP.md` §1A.2. When touching these areas, don't extend the deviation; follow the rules, and flag the conflict rather than silently entrenching it.

---

## Logging (binding; full rules in `docs/ROADMAP.md` §1A.3)
- Use `SimLog.<Channel>.ZLog<Level>($"...")` and never `Debug.Log` in application code. Validators under `Testing/` may keep `Debug.Log`.
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
- **Dual-Loop System**: Use `SimulationManager` for the fixed logical tick, and `AgentCognitionService` for asynchronous background cognition.
- **Defensive Null-Safety for Test Isolation**: Core domain services (`SimulationManager`, `AutonomousAgent`, `AgentCognitionService`) must be null-safe regarding their injected interfaces (`ISpatialPartition`, `IWorldRegistry`, `ISimulationClock`). Domain validators in `Assets/Scripts/Testing/` instantiate these services directly in isolation with `null` dependencies. Never dereference an injected dependency without a null guard or fallback (e.g., `_spatialPartition == null` returns unblocked/empty, `_clock == null` defaults to tick 0).


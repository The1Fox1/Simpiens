# Simpiens Master Project Roadmap (Multi-Track Architecture)

This document is the **highest source of truth** for project status, scope, sequencing, engineering invariants and the Definition of Done across the Simpiens simulation framework. *Last reconciled: 2026-10-24.*

## 0. Source of Truth & Documentation Protocol

When two documents disagree, the higher row in this table wins. Fix the lower document; do not "average" them.

| Rank | Document | Owns | Rules |
| :---: | :--- | :--- | :--- |
| 1 | `docs/ROADMAP.md` (this file) | Epic/milestone **status**, scope numbers, sequencing, engineering invariants (§1A), Definition of Done (§1B) | Update **first** whenever status changes. |
| 2 | `AGENTS.md` | How agents navigate and modify the code (layout, constraints, workflow) | Must not restate or contradict status. Links here for anything status-related. |
| 3 | `docs/epics/*.md` | Detailed epic specifications | The `Status` header must match this file. Acceptance checklists list only what was actually delivered; anything dropped is listed explicitly as **Deferred**. |
| 4 | `docs/Game Design Proposal.md` | Product vision and player-facing design | Scope numbers (sector counts, milestone contents) defer to this file. |
| 5 | `.agents/curentfocus.txt` | Informational mirror of current focus | Never the origin of a status. Update after this file. |
| 5 | `.agents/architecture_history.md` | Append-only log of architectural decisions and rationale | Historical record; do not rewrite past entries. |

**Status vocabulary** (use exactly these values everywhere):

| Status | Meaning |
| :--- | :--- |
| `QUEUED` | Specified or proposed, no work started. |
| `ACTIVE` | Work in progress. |
| `PAUSED` | Partially started and intentionally halted. The epic doc must record what exists and what does not. |
| `MECHANISM COMPLETE` | Domain validators pass, but the **live-simulation** criterion of the Definition of Done (§1B) has not been demonstrated yet. |
| `COMPLETED` | All Definition of Done criteria in §1B are met. |

---

## 1. The Multi-Track Branching Model

As development scales across multiple concurrent streams (UI design, testing harness revamps, and deep cognitive simulation), the project operates under a **Multi-Track Branching Architecture**.

This allows core simulation logic, player interface layers, and verification harnesses to proceed in parallel without blocking each other or causing numbering collisions:

```mermaid
flowchart LR
    subgraph Independent: Spatial & World Topology
        MAP["Epic MAP\nOutpost Floor &\nLiving Simulation Loop\n(COMPLETED)"]
        CORE["Epic CORE\nEngine-Free Core &\nHeadless .NET\n(COMPLETED)"]
        CLIENT["Epic CLIENT\nKNI Client &\nEngine Rework\n(PAUSED; Unity removed)"]
    end

    subgraph Track A: Core Simulation & Cognition (Authoritative Engine)
        A1["Epic 1-6\nFoundations & GOAP"] --> A2["Epic Alpha\nNeeds & Watchdog"]
        A2 --> A3["Epic 7\nSocial Affinity"]
        A3 --> A4["Epic Beta\nInterdependence"]
        A4 --> A5["Epic 8\nGroups & Directives\n(COMPLETED)"]
        A5 --> A6["Epic 9\nOutpost Economy\n& Public Bounties\n(COMPLETED)"]
        A6 --> A7["Epic 10\nGuild, Hazards & Descent\n(Part 1 ACTIVE)"]
        A7 --> A8["Epic 11\nMCP Narrative\n(QUEUED)"]
    end

    subgraph Track B: UI / UX & Player Interface (Dumb Views)
        B1["Epic UI-1\nFacility Dashboard\n(COMPLETED)"] --> BAlpha["Epic UI-Alpha\nNarrative Chronicle\n(COMPLETED)"]
        BAlpha --> B2["Epic UI-2\nMerchant Terminal\n& Bounty Board\n(COMPLETED)"]
        B2 --> B3["Epic UI-3\nGuild Roster &\nExpeditions\n(in Epic 10 Part 1)"]
    end

    subgraph Track C: Harness & Diagnostics (Verification Track)
        C1["Epic Test-1\nVisual Anomaly Detection\n& PlayMode Scenarios\n(COMPLETED)"] --> C2["Epic Test-2\nEconomy & Bounty\nStress Harness\n(COMPLETED)"]
    end

    MAP ==>|Substrate for Spatial Quests & Economy| A6
    MAP ==>|Real Sector Bounds & Shrouds| B2
    MAP ==>|Continuous Layout for Scenario Tests| C1
    A5 -.->|Provides Group & Social Primitives| B2
    A6 <==>|Production Phase 1 Convergence| B2
    A6 -.->|Validation Target| C2
    BAlpha -.->|Prepares Narrative Contracts for LLM| A8
    CORE -.->|Engine-free simulation, headless tests| A7
    CORE ==>|Engine-free core and .NET host| CLIENT
    CLIENT -.->|Replaces Unity's views and dashboard| B3
```

### Track Rules
1. **Track A (Core Simulation)** maintains the authoritative, original numerical sequence (`Epic 1` through `Epic 11`). Its cognition runs off-thread (read-only against immutable snapshots) and lands on a fixed tick through the `CognitionScheduler`; everything else is ticked via `SimulationManager`. Zero MonoBehaviour logic. All Track A work is bound by the invariants in §1A, including multiplayer-readiness.
2. **Track B (UI / UX)** uses the `Epic UI-X` designation. Epics in Track B consume models and snapshots from Track A without mutating core engine state directly (interacting strictly via `PlayerInterventionService`).
3. **Track C (Diagnostics & Harness)** uses the `Epic Test-X` designation, focusing on headless domain validators, batch-mode playmode automation, and zero-allocation memory profiling.
4. **Independent Epics** use domain-specific designations (e.g., `Epic MAP`) and exist outside of any specific track. They represent cross-cutting architectural or environmental foundations that serve all tracks simultaneously.

---

## 1A. Engineering Invariants

### 1A.1 Multiplayer-Readiness (Producer decision, 2026-10-08)

**Multiplayer is a required feature after the MVP.** The MVP ships single-player, but no architectural choice made now may prevent a later authoritative client-server model. The following constraints are **binding for all new work** on the authoritative simulation:

1. **Fixed logical ticks only.** Authoritative state advances exclusively on `ISimulationClock` ticks. Gameplay durations, cooldowns and rates are expressed in ticks, never frame time (`Time.deltaTime` / `Time.time`). Views interpolate for smoothness.
2. **Commands in, state out.** Player and agent actions enter the simulation only as typed, serializable command/intent data (`InfluenceEvent`, `AgentIntent`), applied at tick boundaries.
3. **Deterministic ordering.** Commands, asynchronous cognition results and any state-mutating iteration are applied in a stable order (e.g. by tick, then agent id) — never "whenever the thread pool finishes" and never by `Dictionary` enumeration order.
4. **Seeded randomness.** Simulation outcomes draw only from a seeded, per-simulation RNG. No `UnityEngine.Random` and no unseeded `System.Random` in simulation decisions.
5. **State is plain data.** Authoritative state is serializable plain C# that can exist without a scene or `GameObject`. Views read state and never own it. (This is also required for save/load, replays and floor relocation.)
6. **Single writer.** Background threads read only immutable snapshots or copies. All mutation happens on the simulation thread.
7. **Views never mutate.** UI and rendering code interacts with the simulation only through the command queue.

### 1A.2 Known Deviations (as of 2026-10-09; #17–#19 added 2026-10-17; #17 and #19 resolved 2026-10-25)

These violate §1A.1 or the project architecture rules today. They are recorded so that new code does not copy them. They are not scheduled work by themselves; scheduling is a producer decision. **Scheduled 2026-10-17:** Epic CORE fixed #6, #15 and #18 (chunk C1) and #16 (chunk C5a), all 2026-10-17; #17 and #19 were recorded only, and fixed 2026-10-25 in a cleanup before Epic 10 G3 (producer).

| # | Deviation | Violates | Where |
| :-: | :--- | :--- | :--- |
| 1 | ~~`SimulationManager.Tick()` runs once per rendered frame, and walking, harvest/share/idle durations, need decay and watchdog timers use `Time.deltaTime`, while bounty/memory/gossip/hearth timers use logical ticks.~~ **RESOLVED (2026-10-08):** Fixed-tick architecture implemented; simulation state advances exclusively on `ISimulationClock.OnSimulationTick` at fixed 10 Hz (`dt = 0.1s`), with visual interpolation in views. | 1 | [`SimulationManager.cs`](../dotnet/Simpiens.Core/Simulation/SimulationManager.cs), `AutonomousAgent.cs`, [`SimulationClock.cs`](../dotnet/Simpiens.Core/Simulation/SimulationClock.cs) |
| 2 | ~~`AutonomousAgent` is a `MonoBehaviour` that owns needs, inventory, memory, group membership and the active plan, and applies action consequences. Pawn position is `transform.position`.~~ **RESOLVED (2026-10-08):** Pure C# `AgentState` manages needs, inventory, memory, goals, and tick logic headlessly in `IAgentRegistry`. Intent consequences are applied authoritatively by `IAgentConsequenceService`, async cognition is coordinated by `IAgentCognitionService`, and `AutonomousAgent` acts as a thin view/façade. | 5, "No Logic in MonoBehaviours" | [`AgentState.cs`](../dotnet/Simpiens.Core/Cognition/AgentState.cs), [`AgentRegistry.cs`](../dotnet/Simpiens.Core/Simulation/AgentRegistry.cs), `AutonomousAgent.cs` |
| 3 | ~~Background planners read live `AgentMemory` / `AgentInventory` (non-thread-safe dictionaries) while the main thread mutates them (gossip, trade affinity). The snapshot is `Retain()`ed only after `SwitchToThreadPool()`.~~ **RESOLVED (2026-10-09):** Single-Writer Threading Architecture enforced. `SharedWorldSnapshot.Retain()` invoked on main thread before thread pool dispatch; `IAgentMemory` and `IAgentInventory` provide preallocated double-buffered zero-allocation snapshots (`AgentMemorySnapshot`, `AgentInventorySnapshot`) for off-thread cognition; `GetAffinity` refactored to be pure side-effect-free calculation (no dictionary writes on read); and panic memory pruning moved authoritatively to main thread in `AgentConsequenceService`. | 6 | [`AgentMemory.cs`](../dotnet/Simpiens.Core/Cognition/Memory/AgentMemory.cs), [`AgentCognitionService.cs`](../dotnet/Simpiens.Core/Cognition/AgentCognitionService.cs), [`AgentInventory.cs`](../dotnet/Simpiens.Core/Cognition/Inventory/AgentInventory.cs) |
| 4 | ~~`InfluenceEvent` carries a string command name and a comma-separated string payload parsed with `Split`.~~ **RESOLVED (2026-10-09):** `InfluenceEvent` converted to a typed zero-allocation command union (`PostBountyCommand`, `CancelBountyCommand`, `UpdateStallPricesCommand`) with static factory methods; UI presenters dispatch typed commands directly; legacy string constructor preserved for backward compatibility. | 2 | [`InfluenceEvent.cs`](../dotnet/Simpiens.Core/Intervention/InfluenceEvent.cs), [`SimulationManager.cs`](../dotnet/Simpiens.Core/Simulation/SimulationManager.cs) |
| 5 | ~~Unseeded RNG in simulation paths (`[ThreadStatic] System.Random` in evaluators).~~ **RESOLVED (2026-10-10, closure Phase 2):** `SimRandom` gives stateless, seeded streams keyed by (seed, entity, tick, salt). The seed comes from `SimulationSettings` (`SIMPIENS_SEED` / `-simseed`), and `EntityIdAllocator` makes entity ids reproducible per seed. Remaining `UnityEngine.Random` calls are view-only (`NodeController` bounce direction, `NodeFactory` sprite colour). `OutpostMapBuilder` (not live) and `NodeFactory` still call `GUID.Generate()`. | 4 | [`SimRandom.cs`](../dotnet/Simpiens.Core/Simulation/SimRandom.cs), [`EntityIdAllocator.cs`](../dotnet/Simpiens.Core/Simulation/EntityIdAllocator.cs) |
| 6 | ~~Completion order of background cognition determines the order intents are enqueued; which tick a result arrives on depends on thread timing.~~ **RESOLVED (2026-10-17, Epic CORE C1):** a `CognitionScheduler` applies every decision dispatched on tick T at the start of tick T + 1, behind a barrier, in agent-id order, so neither the tick nor the order depends on thread timing. `DeterminismScenario` now passes alone (3 of 3 runs identical; before, a cold run diverged at t100 in 5 of 5) with the same milestones as before (first cycle 235, scout 382, gather 605, third sleeper 865), and the economy soak is unchanged (fair 546–771g at 30 minutes). History: 2026-10-10 same-tick arrivals were applied in agent-id order; 2026-10-13 and 2026-10-15 the full-suite divergences are recorded in `.agents/architecture_history.md`. | 3 | [`CognitionScheduler.cs`](../dotnet/Simpiens.Core/Cognition/CognitionScheduler.cs), [`SimulationManager.cs`](../dotnet/Simpiens.Core/Simulation/SimulationManager.cs) |
| 7 | ~~`AsyncPathfinder` is a straight-line stress-test placeholder, not Grid A*.~~ **RESOLVED (2026-10-09):** Upgraded `AsyncPathfinder` into a zero-allocation 8-directional Grid A* engine with preallocated binary min-heap, $O(1)$ epoch visited tracking, diagonal corner-cutting prevention, raycast string-pulling path smoothing, and concurrent searcher worker pooling. Separated static map obstacles (`StaticNavGrid`) from per-frame dynamic entity snapshots. | — (Epic 9 carry-over) | [`AsyncPathfinder.cs`](../dotnet/Simpiens.Core/Cognition/Pathfinding/AsyncPathfinder.cs), [`GridAStarSearcher.cs`](../dotnet/Simpiens.Core/Cognition/Pathfinding/GridAStarSearcher.cs), [`StaticNavGrid.cs`](../dotnet/Simpiens.Core/Simulation/Spatial/StaticNavGrid.cs) |
| 8 | `GoapPlanner` is a DI singleton guarded by a `lock`, so all agents' planning is serialized. | Dual-loop scaling intent | [`GoapPlanner.cs`](../dotnet/Simpiens.Core/Cognition/Planning/GoapPlanner.cs) |
| 9 | ~~The production GOAP domain (action graph, goals) and session population live under `Testing/` in `SwarmSpawner`; validators are registered as boot-time `IStartable`s and ship in the single `Simpiens.asmdef`.~~ **RESOLVED (2026-10-11, closure Phase 5):** domain in `Cognition/Planning/Domain/OutpostGoapDomain`, session in `Core/Session/SwarmSpawner`. Test code is in a separate `Simpiens.Testing` assembly (`UNITY_INCLUDE_TESTS` only), wired in through `BootstrapExtensions`. Validators run as PlayMode tests, not at boot. | Test isolation | [`OutpostGoapDomain.cs`](../dotnet/Simpiens.Core/Cognition/Planning/Domain/OutpostGoapDomain.cs), `AppBootstrapper.cs` |
| 10 | ~~`CognitiveEngine`, `IContextBuilder` and `IToolExecutionRegistry` are registered/declared but unused; the real cognition loop is `AutonomousAgent.EvaluateCognitionAsync`.~~ **RESOLVED (2026-10-09; trade gold routed through the ledger 2026-10-11):** `IToolExecutionRegistry` implemented via `ToolExecutionRegistry` mapping cognitive tool execution to typed intents, backed by `IIntentHandlerRegistry` with 8 modular intent handlers (`WanderIntentHandler`, `PanicIntentHandler`, `HarvestResourceIntentHandler`, etc.). `SimulationManager` implements `IIntentExecutionContext` and delegates execution to modular handlers. Authoritative `IEconomyLedger` introduced to govern all gold flows and fix escrow minting leaks. The `CognitiveEngine`/`ContextValidator` stub was deleted 2026-10-11; `IContextBuilder` is kept as the Epic 11 seam. | — | [`ToolExecutionRegistry.cs`](../dotnet/Simpiens.Core/Cognition/ToolExecutionRegistry.cs), [`IntentHandlerRegistry.cs`](../dotnet/Simpiens.Core/Cognition/Planning/Handlers/IntentHandlerRegistry.cs), [`SimulationManager.cs`](../dotnet/Simpiens.Core/Simulation/SimulationManager.cs), [`EconomyLedger.cs`](../dotnet/Simpiens.Core/Simulation/Economy/EconomyLedger.cs) |
| 11 | ~~Tier-1 `SurvivalUtilityEvaluator` returned a non-null intent (Idle/Wander/Harvest) on every path, so Tier-2 GOAP never ran in the live session; every GOAP goal from Epics 6–9 only worked under validator injection.~~ **RESOLVED in code (2026-10-09, review §3.1):** the evaluator is now a pure hunger reflex (Hunger ≥ 60 + reachable known food, else `null`); idle/wander moved to the Tier-3 `AmbientWanderEvaluator` fallback; `HarvestResourceAction`, `TravelToResourceAction` and `SearchResourceAction` emit real intents. Regression covered by `SupplyLoopValidator` Test 7. Live verification pending the first `OutpostSupplyLoopScenario` run. | §1B (status accuracy) | [`SurvivalUtilityEvaluator.cs`](../dotnet/Simpiens.Core/Cognition/Evaluators/SurvivalUtilityEvaluator.cs), [`AgentCognitionService.cs`](../dotnet/Simpiens.Core/Cognition/AgentCognitionService.cs) |
| 12 | ~~No live population could run the Milestone 1 loop: all session agents were scripted into walled demo pens, the trade stall sat inside Pen 3, and the seeded Scout bounty targeted walled Pen 7.~~ **RESOLVED in code (2026-10-09, review §3.1):** the open yard north of the pens is now the *Outpost Commons* (stall, Ore Ridge, Herb Hollow, food bushes) with 6 free crawler agents (`SpawnCount` 24 → 30). | §1B | `SwarmSpawner.cs` |
| 13 | ~~Seeded bounties bypassed escrow (`RegisterBounty` locked nothing), so delivery minted gold via a direct `AddGold`.~~ **RESOLVED in code (2026-10-09, review §3.1):** `RegisterBounty` routes through the same escrow lock as `PostBountyCommand` and rejects un-escrowable bounties; delivery pays only by releasing escrow. Session gold enters through a single `InitialSeed` ledger faucet. | Ledger conservation | [`SimulationManager.cs`](../dotnet/Simpiens.Core/Simulation/SimulationManager.cs), [`DeliverBountyIntentHandler.cs`](../dotnet/Simpiens.Core/Cognition/Planning/Handlers/DeliverBountyIntentHandler.cs) |
| 14 | ~~`SimulationManager` executes active intents by enumerating the `_activeIntents` `Dictionary` (and stalls, bounties and anchors the same way).~~ **RESOLVED (2026-10-10):** active intents run in agent-id order (sorted id list), the stall/bounty/expiry/anchor buffers are sorted, and harvest contention picks the lowest-id competitor. Mutation-checked by `DeterminismValidator` Test 3. Unaudited: `foreach` over agent memory maps in GOAP actions (e.g. `TravelToAnchorAction` nearest-anchor ties). | 3 | [`SimulationManager.cs`](../dotnet/Simpiens.Core/Simulation/SimulationManager.cs) |
| 15 | ~~The background cognition task writes per-agent state off the main thread: plan state (`AbortActivePlan`, `ActivePlan.CopyFrom`, `ActiveGoal`), `CurrentIntent`/`HasActiveIntent` and `VisualState`; `AgentState.Tick` reads `ActivePlan.CurrentAction` while cognition rewrites the plan (architecture scan H2).~~ **RESOLVED (2026-10-17, Epic CORE C1):** the evaluation returns its decision as data (`CognitionResult`: the intent, the plan to abort or adopt, the goal) and writes only its own scratch plan; the main thread applies it at the due tick. Visual state is set during the main-thread preparation. | 6 | [`AgentCognitionService.cs`](../dotnet/Simpiens.Core/Cognition/AgentCognitionService.cs) |
| 16 | ~~`AutonomousAgent.ManualTick` copies `transform.position` into `AgentState.Position` every tick, so the view feeds the simulation's position (latent: crawler views never interpolated, so the copy equalled the logical position).~~ **RESOLVED (2026-10-17, Epic CORE C5a):** the simulation works with plain-data `WorldEntity` records (a crawler's position is its `AgentState.Position`), the per-crawler tick is the engine-free `AgentRuntime`, and `NodeController` and `AutonomousAgent` are views that only read state. Golden world hashes identical. | 5 | [`WorldEntity.cs`](../dotnet/Simpiens.Core/Simulation/WorldEntity.cs), [`AgentRuntime.cs`](../dotnet/Simpiens.Core/Cognition/AgentRuntime.cs) |
| 17 | ~~No `try`/`catch` on the tick path. `SimulationManager.SimulateTick` completes intents inside its loop but removes them after it, so one handler exception leaves finished intents in place: they run again next tick and return their `PathResponse` to the pool twice. (Architecture scan H1, 2026-10-15; recorded 2026-10-17.)~~ **RESOLVED (2026-10-25):** a handler or completion that throws aborts its own intent only; the agents after it still run, finished intents are removed in a `finally`, and a `PathResponse` returned twice goes back to its pool once. Every fault is logged at Error and counted (`ISimulationManager.IntentFaults`), and `OutpostWatch` fails the day on any, so a live scenario can't pass over one (`SimulationManagerModularizationValidator` Test 6, `GridAStarValidator` Test 10). | Robustness | [`SimulationManager.cs`](../dotnet/Simpiens.Core/Simulation/SimulationManager.cs) |
| 18 | ~~`AgentCognitionService` sets `IsEvaluating` before its `try`/`finally`, so an exception in the main-thread preparation freezes that crawler for good (architecture scan M3).~~ **RESOLVED (2026-10-17, Epic CORE C1):** the preparation is guarded and clears the flag on failure; a failed or cancelled evaluation is released at its due tick (`CognitionSchedulerValidator`). | Robustness | [`AgentCognitionService.cs`](../dotnet/Simpiens.Core/Cognition/AgentCognitionService.cs) |
| 19 | ~~A posted bounty gets the command's id, or `_bounties.Count + 1` when it has none, and nothing checks for an existing id: a bounty whose id is taken overwrites the old one. A save keeps finished bounties, so the count survives a load. (Architecture scan M4, 2026-10-15; recorded 2026-10-17.)~~ **RESOLVED (2026-10-25):** generated ids come from a counter that skips every id on the board, and a full board refuses a post; id 0 and a live bounty's id are refused, so no escrow or claimant is lost (a finished bounty's id may be reused explicitly, as scripted ids do). The counter isn't saved: a load derives it from the saved bounties, and a bounty posted after a load gets the id the uninterrupted session gives (`BountyFulfillmentValidator` Tests 9–10, `SessionRestoreValidator` Test 8). | Robustness | [`SimulationManager.cs`](../dotnet/Simpiens.Core/Simulation/SimulationManager.cs) |

### 1A.3 Logging (Producer decision, 2026-10-09)

All application logging goes through `Simpiens.Diagnostics.SimLog` (ZLogger, §2.1). Binding for new code:

1. **No `UnityEngine.Debug.Log*` in application code.** Validators log test output through `Simpiens.Testing.TestLog` (framework-free since Epic CORE C6). (Since Unity's removal on 2026-10-24 the rule reads: no console writes in application code.)
2. **Interpolated ZLogger calls only:** `SimLog.<Channel>.ZLog<Level>($"...")`. Never pre-build a message string, and never call `string.Format`, `ToString()` or concatenation outside the hole. A disabled call must evaluate nothing and allocate nothing.
3. **Pick the channel and level deliberately.**
   - Channels: `Bootstrap`, `Simulation`, `Cognition`, `Planning`, `Economy`, `Supply`, `Navigation`, `Social`, `Groups`, `Drives`, `Intervention`.
   - Levels:

     | Level | Use for |
     | :--- | :--- |
     | Error | Broken invariants |
     | Warning | Rejected or degraded operations |
     | Information | Player-meaningful events, a few per minute |
     | Debug | Per-agent decisions and outcomes |
     | Trace | Per-candidate and per-path detail |

4. **Per-agent decision traces are gated by `SimLog.Traces(channel, level, agentId)`**, so the agent focus filter (`-simlog-focus Crawler`) works. Identify entities with `SimLog.Agent(id)`.
5. **Logging is read-only.** It never mutates simulation state or changes control flow.

Configuration:
- Environment: `SIMPIENS_LOG="Default=Info;Planning=Debug;Console=Warn"`, `SIMPIENS_LOG_FILE`, `SIMPIENS_LOG_FILE_LEVEL`, `SIMPIENS_LOG_FOCUS`.
- Command line: `-simlog`, `-simlog-file`, `-simlog-file-level`, `-simlog-focus`.
- Code: `SimLog.Configure`.

Every line is prefixed `[t<tick>] <LVL> <Channel> |`.

---

## 1B. Definition of Done

**"Done" means the feature *emerges in the live simulation*, not only that a validator passes.**

Domain validators prove a mechanism is correct *in isolation*, and they usually do so by injecting the preconditions (hand-placed inventory, teleported agents, forced intents, pre-claimed bounties). That proves the code works, not that the game does. An epic or slice may be marked `COMPLETED` only when **all** of the following hold:

1. **Mechanism verified.** Headless domain validators pass (0 errors, 0 steady-state allocations where the hot path rule applies).
2. **Live emergence demonstrated.** At least one live scenario shows the feature occurring *naturally* in the real session setup. Since 2026-10-23 (producer, Epic 10 Q8) new work proves this headless in `dotnet test` (`dotnet/Simpiens.Core.Tests`, on the real session); earlier epics did it with a Unity PlayMode scenario, and each of those has a .NET twin since Unity was removed (Epic CLIENT M2.8–M2.9, 2026-10-24). A scripted player that acts only through the typed commands a real player sends is input, not injection. It must **not** inject the outcome's preconditions. It asserts a concrete outcome within a bounded number of ticks (e.g. *"within N ticks, at least one posted Gather bounty is claimed, fulfilled and paid out"*).
3. **Docs reconciled.** This roadmap's status is updated first, the epic's `Status` header matches it, and its acceptance checklist contains only delivered items. Anything dropped or postponed is listed under an explicit **Deferred** heading with the epic that now owns it.

Rules of thumb:
- If only (1) is met, the status is `MECHANISM COMPLETE`, never `COMPLETED`.
- A validator that injects state is a *mechanism test*. It must not be cited as evidence that a gameplay loop works.
- Epics that predate this rule (Epics 1–8, Alpha, Beta, UI-1, UI-Alpha, UI-2, Test-1) are recorded as `COMPLETED` on the basis of the criteria in force at the time. Their live-scenario coverage has **not** been audited.

---

## 2. Approved 3rd-Party Libraries & Ecosystem Strategy

Simpiens is an **agent-first, decoupled simulation**. In this development paradigm, handrolling domain logic is fast and produces zero-allocation, scene-independent pure C# services. However, 3rd-party libraries are selectively embraced where they offer battle-tested low-level memory performance, source-generated serialization, or engine-level optimization without violating our headless testability or off-thread threading model.

### 2.1 The Approved Library Stack

| Library / Package | Scope & Role | Justification & Architectural Boundary |
| :--- | :--- | :--- |
| ~~**`VContainer`**~~ | **Retired 2026-10-24** with Unity (Epic CLIENT M2.9) | It was Unity's composition root only; the core never depended on it (Epic CORE C6). |
| ~~**`UniTask`**~~ | **Retired 2026-10-17** (Epic CORE C6) | Cognition, saves, the narrative hook and the persistence smoke use `Task`/`ValueTask`; the package is removed from `Packages/manifest.json`. |
| **`MemoryPack`** (1.21.4) | **Zero-Alloc Binary Serialization** (installed 2026-10-15) | Source-generated binary serializer; unmanaged records (`ItemInstance`, `BountyContract`, `TradeStallData`, `GroupData`, `ChronicleEvent`, …) are saved as raw memory copies.<br>• **Callers:** only `Simulation/Persistence/SaveSerializer`.<br>• **Used by:** the whole-session save and load (`SessionSave`, `SaveFile`, the quick-save slot); later replays and the floor relocation (Milestone 4).<br>• From NuGet; its source generator runs as a Roslyn analyzer and never ships. |
| **`ZLogger`** (2.5.10) | **Zero-Alloc Structured Logging** (2026-10-09) | Interpolated-string handlers that evaluate and allocate nothing when a level is disabled, wrapped by `Simpiens.Diagnostics.SimLog` (§1A.3). From NuGet. |
| ~~**`Unity.Collections` + Burst**~~ | **Dropped 2026-10-17** (producer, Epic CORE) | Never installed; the core is engine-free. .NET's own SIMD types replace them if a measured need arises. |
| **`Microsoft.Extensions.DependencyInjection`** / **`.Hosting`** (10.0.13) | Composition for .NET hosts (2026-10-17) | `dotnet/Simpiens.Hosting` and the `simpiens` console host. The engine-free core stays container-agnostic: constructor injection only. |
| **xUnit v3** (3.2.2) | .NET tests (2026-10-17) | `Simpiens.Core.Tests` and `Simpiens.Client.Tests` under `dotnet test`. |
| **KNI** (`nkast.Xna.Framework.*`, `nkast.Kni.Platform.*` 4.3.9001, MS-PL) | The game client's framework (producer, 2026-10-18, Epic CLIENT) | A MonoGame-derived implementation of the XNA 4 API, chosen over Godot, MonoGame, FNA and BRUTAL in the engine spike ([`docs/engine-proposal-kni.md`](engine-proposal-kni.md)). Only `XnaPlatform` may use an API that KNI and MonoGame don't share. |
| **MonoGame** (`MonoGame.Framework.DesktopGL` 3.8.6) | KNI's fallback, compile-only | CI compiles the client against it (`-p:XnaFramework=MonoGame`), so a switch stays a package swap. |
| **Gum** (`Gum.KNI`, `Gum.MonoGame` 2026.10.1.1) | The client's UI (Epic CLIENT M2) | Gum's built-in visuals in the dashboard's palette; pinned, bumped only on purpose in its own commit. |
| **KernSmith** (0.21.0, with FreeType) | Developer tool only | `Simpiens.FontBaker` bakes `FontSet`'s faces of Inter into committed BMFont atlases; never shipped. |

> **History of this table:** the producer approved VContainer, UniTask and `Unity.Collections` + Burst for the Unity project, ZLogger on 2026-10-09 and MemoryPack (installed 2026-10-15). On 2026-10-17 (the migration write-up's checklist, Epic CORE) the producer retired UniTask and `Unity.Collections` + Burst, kept VContainer as Unity's composition root only, and approved `Microsoft.Extensions.DependencyInjection` and xUnit v3 for .NET. On 2026-10-18 (Epic CLIENT) KNI was chosen for the client, with MonoGame as its compile-only fallback, and Gum for its UI. On 2026-10-24 Unity was removed, and VContainer and Unity's vendored copies of MemoryPack and ZLogger with it; the solution moved to C# 14 on .NET 10, the newest .NET KNI supports (producer: "the latest version KNI supports"; its Android platform targets `net10.0-android`).

### 2.2 Rejected 3rd-Party Frameworks & Handroll Policies

1. **2D Pathfinding & Navigation Engine**:
   - **Rejected**: *A* Pathfinding Project (Granberg)* and *Unity AI Navigation*. Both heavily couple to `MonoBehaviour` components (`Seeker`, `AIPath`, `NavMeshAgent`) or main-thread NavMesh carving, which breaks headless domain testing and stalls high-speed ticks (5x–10x).
   - **Handroll Policy** (delivered 2026-10-09, §1A.2 #7; kept for the record): Upgrade [`AsyncPathfinder.cs`](../dotnet/Simpiens.Core/Cognition/Pathfinding/AsyncPathfinder.cs) into a zero-allocation 2D Grid A* / Jump Point Search (JPS) engine by porting the array-backed binary min-heap and struct node pool architecture already proven in [`GoapPlanner.cs`](../dotnet/Simpiens.Core/Cognition/Planning/GoapPlanner.cs).
2. **Model Context Protocol (MCP) Integration**:
   - **Rejected**: *Official .NET MCP C# SDK (`modelcontextprotocol/csharp-sdk`)*. Relies on ASP.NET Core Hosting and modern .NET runtime assemblies that collide with Unity's Mono/IL2CPP `netstandard2.1` target.
   - **Rejection lifted (producer, 2026-10-17):** Epic 11 may use the official SDK (`ModelContextProtocol`, targets `net10.0`): the simulation runs on .NET, and since Unity's removal (2026-10-24) only on .NET.
   - **Handroll Policy** (obsolete since Unity's removal; kept for the record): Implement a lean, pure-C# `Task`-based JSON-RPC 2.0 / SSE client (~250 LOC) under [`ILlmNarrativeEnricher`](../dotnet/Simpiens.Core/Narrative/ILlmNarrativeEnricher.cs) with an automatic circuit breaker falling back to [`TemplateNarrativeFormatter`](../dotnet/Simpiens.Core/Narrative/TemplateNarrativeFormatter.cs).
3. **UI Toolkit Reactive Data-Binding** (**superseded 2026-10-18,** and removed with Unity on 2026-10-24; the dashboard is Gum in the KNI client, refreshed at 10 Hz of view time with reused rows, Epic CLIENT M2):
   - **Policy** (Unity's frozen UI): Reserve Unity 6 built-in runtime data binding (`INotifyPropertyChanged`) exclusively for low-frequency modal dialogs and player input forms. For high-frequency dynamic simulation feeds (logical ticks run at 10 Hz at 1x and up to 100 Hz at 10x clock speed), standardize on throttled (10 Hz) element pooling via a reusable `VisualElementPool<TData, TElement>` utility modeled after `FacilityDashboardPresenter.cs`.

---

## 3. Production Milestones Overview

To bridge low-level technical epics with high-level game design goals, development is staged into **Production Milestones**:

| Milestone | Target Scope | Aligned Epics | Target Release State |
| :--- | :--- | :--- | :--- |
| **Milestone 1** | **The Outpost Loop (MVP)** | Track A: Epic 9<br>Track B: Epic UI-2<br>Track C: Epic Test-2 | Single connected outpost floor (Epic MAP; sector count is data, not a target: producer 2026-10-11). Player manages a roadside trading post: sets buy/sell prices for salvaged materials and provisions, posts public bounties (Scout, Gather), and builds dyadic trust with visiting crawlers. Integrates MemoryPack serialization for unmanaged inventory/contracts and zero-alloc Grid A* navigation (Grid A* delivered 2026-10-09; MemoryPack delivered 2026-10-15 as a whole-session save and load with one quick-save slot). |
| **Milestone 2** | **Guild Roster & Expeditions** | Track A: [Epic 10 Part 1](epics/epic-10.md), with Epic UI-3 folded in (its UI is the KNI client's) | High-affinity adventurers convert into the player's permanent Guild (up to four). The player outfits parties and issues expedition directives to high-hazard sectors; drifters open the economy. (The earlier `Unity.Collections` + Burst item is dropped: the core is engine-free, and speed-ups follow measurements.) |
| **Milestone 3** | **Facility Workshops & Crafting** | Track A: Epic 10 (Part 2)<br>Track B: Epic UI-4 | Fixed outpost expansion stations (Forge, Alchemy Still). Hired non-combat artisans refine raw salvage into weapons, armor, and potions. |
| **Milestone 4** | **Gatekeeper Descent & Floor Relocation** | Track A: Epic 10 (Part 3) & Epic 11<br>Track B: Epic UI-5 | Boss-guarded descent gate breach, roguelike forward operating base relocation loop (via MemoryPack state snapshotting), and LLM-powered MCP tavern chronicles. |

### Milestone 1 Status

**`COMPLETED` (2026-10-15).** The producer's check of save and load at 1× passed (recorded 2026-10-24).

- **The live loop emerges** on the Floor 1 outpost with nothing injected: the player sets prices and posts bounties, crawlers harvest salvage and sell it at the stall, and trust toward the player grows. Evidence: `OutpostSupplyLoopScenario` (2026-10-10) and `OutpostLifecycleScenario` (the whole day; trust up for ⅔ of crawlers at the default prices, 2026-10-15).
- **Its epics are `COMPLETED`:** Epic 9 (economy and bounties, with §3.1 supply, §3.2 personality and §3.3 the economy model, [docs/economy-model.md](economy-model.md)), Epic UI-2 (merchant terminal, bounty board), Epic Test-2 (economy harness, soak with pace bands) and Epic MAP (the outpost floor, fog of war, the player's view). Zero-allocation Grid A* was delivered 2026-10-09.
- **MemoryPack serialization was delivered 2026-10-15** as a whole-session save and load (the producer's reading, D1 = B):
  - one quick-save slot, with Save and Load in the dashboard header (D2 = A);
  - a load puts back every crawler (needs, purse, pack, memories, relationships), the stall, the bounties and their escrow, the fog, regrowth and the chronicle; crawlers decide afresh on the next tick (D3 = A);
  - a save from another format, session, seed or map is refused with a reason (D4 = A);
  - Epic 9's deferred validators pass: `EconomySerializationValidator` and `EconomyZeroAllocValidator` (D5 = A);
  - live Definition of Done: `SaveLoadScenario` saves the outpost while a crawler holds the Gather bounty, loads it into a fresh session (byte-identical, equal world hash) and plays 3,000 more ticks: zero anomalies, the ledger balanced, nobody starving, every crawler eating, a harvest-to-sell cycle, and the claimed bounty delivered. It passed in 3 of 3 consecutive full-suite runs (2026-10-15).
- History: the closure sequence below (§3A) and `.agents/architecture_history.md`.

*Closure plan: approved 2026-10-09 and complete. The soak and scaling work planned before Epic 10 (§3A #8) was not done first: the producer started Epic 10 Part 1 on 2026-10-23, which measures population cost at G1 and G5 and builds faster planning only on a measured need.*

---

## 3A. Architecture Review Remediation Tracker

Progress against the consultant review [`docs/reviews/architecture_review.md`](reviews/architecture_review.md) (2026-10-08), §5 *Prioritised Recommendations*. Verified against the code on 2026-10-09. The review itself is a dated snapshot; **this table is the live status**.

| Review # | Recommendation (section) | Status | Evidence / what remains |
| :-: | :--- | :--- | :--- |
| 1 | Fixed-tick simulation; durations in ticks; views interpolate (§2.1) | ✅ Done | §1A.2 #1. `SimulationManager` runs on `OnSimulationTick`; frame time remains only in views (`AgentMotor`, `AgentView`, `CampfireView`) and UI refresh. |
| 2 | Close the Milestone 1 supply side (§3.1) | ✅ Done; live scenario passes | §1A.2 #11–13, *Milestone 1 Status*. `OutpostSupplyLoopScenario` passes with zero anomalies (2026-10-10). |
| 3 | Single-writer threading; fix `Retain` ordering (§2.3) | ✅ Done | §1A.2 #3. |
| 4 | Grid A* + static walkability, gating MAP (§2.6) | ✅ Done | §1A.2 #7, plus static reachability regions. Navigation clearance follow-ups (grid reset, wall-checked watchdog, body clearance) done 2026-10-10. |
| 5 | Agent personality + utility "considerations" for target selection (§3.2, §2.7) | ✅ Done (2026-10-11) | `AgentPersonality` (5 traits, seeded; pen agents neutral), `Considerations` (reservation/minimum prices, bounty score, trait factors on goal priorities; neutral = unchanged). Live: `PriceResponseScenario` shows gradual demand (e.g. 6/5/5/1/1/0 buyers at 10–60g). Risk tolerance decides who ventures into hazard sectors since Epic 10 G2 (2026-10-23). |
| 6 | Intent-handler registry, typed `InfluenceEvent`, gold ledger (§2.4) | ✅ Done | §1A.2 #4, #10, #13. `SimulationManager` down from ~1,500 to ~1,190 lines. The legacy string `InfluenceEvent` constructor still exists. |
| 7 | Plain-C# `AgentState` + `AgentRegistry`; `AutonomousAgent` as view (§2.2) | ✅ Done | §1A.2 #2. Save/load delivered 2026-10-15: a session round-trips byte for byte through `SessionSave`. The last latent breach (the view fed the position, §1A.2 #16) was closed 2026-10-17 by Epic CORE C5a. |
| 8 | Profiled soak test → spatial-hash queries, per-thread planners, cognition scheduler (§2.5) | ⬜ Not started | `GoapPlanner` is still a locked singleton (§1A.2 #8). `CognitionScheduler` (Epic CORE C1) lands decisions on fixed ticks but doesn't budget them. The economy soak exists (Epic Test-2) and runs headless in seconds (`simpiens soak`, Epic CORE C10), as does a seed sweep of the outpost's day (`simpiens sweep`; the 23 gate seeds pass in `dotnet test` since Epic 10 G0), but there is no profiled scaling soak (`PopulationScalingProbe` is a one-off measurement). No measured agent budget. |
| 9 | Gameplay scope, separate test asmdef, remove stub engine (§2.8) | ✅ Done (2026-10-11) | §1A.2 #9. All 40 PlayMode tests pass with the new assembly layout. Not verified: a player build (relies on the standard `UNITY_INCLUDE_TESTS` constraint). A dedicated `GameplayLifetimeScope` / ScriptableObject-driven session definition was not done (`AppBootstrapper` stays the single scope). |
| 10a | One-page economy model: faucets, sinks, scarcity lever (§3.3) | ✅ Done (2026-10-15); producer play session passed | [docs/economy-model.md](economy-model.md). Decided under the producer's provisional authority, D1–D7 as recommended, then reviewed and merged by the producer (2026-10-15, PR #3): a caravan salvage quota (faucet), outpost upkeep (sink), fairness judged by each crawler's own valuation, crawlers' willingness to trade as the scarcity lever, food paced by data plus one behaviour fix, drifters deferred to Milestone 2. Every number is one `EconomyTuning` per session. The soak asserts a fair, a greedy and a generous player's pace band. Evidence: [Epic 9 §8.5](epics/epic-9.md). |
| 10b | "Why?" agent inspector (§3.4) | 🟨 Groundwork | `DecisionSummary` (source tier + top 3 goal priorities) recorded on every intent and copied to `AgentState.LastDecision` on the main thread (2026-10-11). The inspector UI is not started (Track B). |
| 10c | Live-scenario Definition of Done (§4.2) | ✅ Done | §1B. |
| 10d | Docs single source of truth (§4.3) | ✅ Done | §0. |

Other review items still open:
- **GOAP fact budget (§2.7):** 39 of 64 facts used (`StateFact` 0–38, 2026-10-24).
- **Core refactor (2026-10-25):** a code-smell report checked against the code; a plan, not an epic, in `.agents/core_refactor_plan.md`. Its cleanup (R0: §1A.2 #17 and #19, warnings as errors) is done; R1–R6 (guards, `SimulationManager`'s split, planners per worker: #8) come back after Epic 10 Part 1, R7, the item system, was done on 2026-10-26, before Epic 10 G4, at the producer's word: a catalog with groups and rarity, consumable effects, equipment stats, and loot tables as a foundation, with no behaviour change (PR #15; how to add or remove an item: `AGENTS.md`).
- **Determinism (§2.1 note):** #5 and #14 resolved 2026-10-10; **#6 resolved 2026-10-17 (Epic CORE C1): decisions land on a fixed tick, and `DeterminismScenario` passes alone as well.** Before that, #6 was partially resolved; the arrival tick of async cognition results is still timing-dependent (measured 2026-10-13: no divergence to t3,000 on this machine). **Measured 2026-10-15:** inside the full suite, `DeterminismScenario` diverged once in 3 runs (t1800). Run alone, it diverges at t100 in 5 of 5 runs, on both `c244b1c` and the §3.3 step 6 tree: a cold first run delays cognition results by a tick. The scenario's assertion is reliable only inside the full suite, after warm-up. Seen again 2026-10-15 (save/load): divergences in 3 full-suite runs, 2 of them on a busy machine; every rerun on a quiet machine was identical (see §1A.2 #6).
- ~~**Single writer:** §1A.2 #15 (cognition writes plan/intent bookkeeping off-thread).~~ Resolved 2026-10-17 (Epic CORE C1).

### Milestone 1 closure sequence (approved 2026-10-09)

Implementation plan: `.agents/milestone1_closure_plan.md` (completed and removed; `git show f679a0c:.agents/milestone1_closure_plan.md`).

1. ~~**Green PlayMode.**~~ **Done 2026-10-10:** 14/14, `OutpostSupplyLoopScenario` meets §1B for the supply loop.
2. ~~**Determinism**~~ **Done 2026-10-10:** seeded `SimRandom`, reproducible entity ids, ordered intent application and iteration. PlayMode 16/16.
3. ~~**Economy observation soak**~~ **Done 2026-10-11.** `EconomySoakScenario` and `scripts/run-economy-soak.sh` ran 6 runs: integrity holds. Findings are in [Epic 9 §8](epics/epic-9.md) (with the Floor 1 re-run). Without the provisional export the economy freezes within about 10 sim-minutes; with it, the treasury grows about 145g per sim-minute. The economy model (§3.3) stays **deferred**.
4. ~~**Personality + considerations (§3.2).**~~ **Done 2026-10-11.** Pen demo agents stay neutral. PlayMode 19/19 plus the explicit soak.
5. ~~**Test-scope separation**~~ **Done 2026-10-11** (#9, review §2.8). PlayMode 40/40.
6. ~~**Docs + promotion.**~~ **Docs reconciled 2026-10-11.** ~~Epic 9 promotion is **not** done: it waits on the deferred §3.3 economy model (producer).~~ **Epic 9 promoted 2026-10-15** with the §3.3 economy model (producer play session passed the same day).

Later Milestone 1 scope:
- ~~Epic MAP~~ (`COMPLETED` 2026-10-13);
- ~~the §3.3 economy decision on the real map~~ (done 2026-10-15, [docs/economy-model.md](economy-model.md));
- ~~then `MemoryPack`~~ (done 2026-10-15: a whole-session save and load; the completion plan is removed, `git log --diff-filter=D -- .agents/milestone1_completion_plan.md`).

Before Epic 10 the soak and scaling work (#8) was planned; the producer started Epic 10 without it (2026-10-23), see Milestone 1 Status.

---

## Track A: Core Simulation & Cognition Engine (Authoritative Sequence)

*Status: Authoritative engine logic. Background cognition runs against immutable snapshots and its decisions land on a fixed tick; world mutation is applied by `SimulationManager`. See §1A for binding invariants and §1A.2 for known deviations.*

| Epic | Name | Status | Key Deliverables |
| :--- | :--- | :--- | :--- |
| **Epic 1** | Core Simulation & Physics Foundation | **COMPLETED** | 2D kinematic physics, screen bounds generator, basic `NodeController` view. |
| **Epic 2** | The Code-First Architecture | **COMPLETED** | VContainer DI integration, `AppBootstrapper`, `ITickable` simulation manager. |
| **Epic 3** | Cognitive Pipeline & Async Pathfinding | **COMPLETED** | `SpatialHashGrid`, lock-free `SharedWorldSnapshot`, zero-alloc `AsyncPathfinder`. |
| **Epic 4** | State Mutation & Active Idling | **COMPLETED** | `WanderIntent`, thread-safe harvesting state mutation, thread-static RNG. |
| **Epic 5** | Memory & Knowledge Matrix | **COMPLETED** | Fog of War memory maps, temporal decay, ring-buffer `MemoryLedger`, peer gossip. |
| **Epic 6** | Multi-Step Reasoning (GOAP) | **COMPLETED** | Zero-alloc bitmask `WorldState`, A* `GoapPlanner`, reflexive preemption (`AbortIntent`). |
| **Epic Alpha**| Needs, Mental Breaks & Motor Watchdog | **COMPLETED** | `AgentNeeds` (Hunger/Energy/Social), 3-tier unstuck watchdog, panic & collapse breaks. |
| **Epic 7** | Social Affinity & Relational Dynamics | **COMPLETED** | 4-Axis Dyadic Matrix (`Warmth`, `Trust`, `Fear`, `Respect`), selective gossip filtering. |
| **Epic Beta** | Concrete World Mechanics & Interdependence | **COMPLETED** | Dominance contention/yielding, altruistic food sharing, campfire gathering anchors. |
| **Epic 8** | [Socio-Political Structures & Groups](epics/epic-8.md) | **COMPLETED** | 4-layer taxonomy, parameter-driven `Group` primitive (`GroupData`, `GroupDirective`, `GroupMembership`), compliance/defiance dynamics, and organic hearth succession. |
| **Epic 9** | [The Outpost Economy & Public Bounties](epics/epic-9.md) | **COMPLETED** (2026-10-15; producer play session passed) | **Delivered (Slices 1–3 + Modernization):** unmanaged item/inventory structs, gold currency, trade stall entity, public bounty board with escrow/refund, `PlayerInterventionService` drained in `Tick()`, trade-to-affinity pipeline, GOAP economic actions/goals, zero-alloc Grid A* in `AsyncPathfinder`, 3 headless validators. ~~**Deferred (not delivered):** `MemoryPack` state serialization, `EconomyZeroAllocValidator`, `EconomySerializationValidator`.~~ **Delivered 2026-10-15 with Milestone 1's save and load.** **§3.1 supply side (2026-10-09, implemented, live run pending):** typed resources, salvage harvesting, Scout fulfilment, escrow-only bounty payout, supply caravan & regrowth, Outpost Commons arena, `SupplyLoopValidator`, `OutpostSupplyLoopScenario`. §3.2 personality landed 2026-10-11 (`PriceResponseScenario` passes). **§3.3 economy model (2026-10-15):** `EconomyTuning`, the fairness rule (trust from trade), caravan salvage quota, outpost upkeep and arrears, food pacing, and the soak's pace bands; `OutpostLifecycleScenario` asserts trust toward the player ([docs/economy-model.md](economy-model.md)). **Still deferred:** drifters (Milestone 2). |
| **Epic 10** | [Part 1: The Guild and Its Expeditions](epics/epic-10.md); Parts 2 and 3: workshops, the descent | **ACTIVE** (Part 1, 2026-10-23; G0, G1 and G2 done 2026-10-23; the producer's look at G2 passed and the fair soak band is 1,000–2,000g (both 2026-10-25); G3, gear, done 2026-10-25; G4, the guild, is next) | **Part 1 (Milestone 2):** G0 a day that holds on every seed (Epic CORE F2), G1 drifters, G2 health and environmental hazards, G3 gear, G4 the guild (recruitment, wages, quitting), G5 expeditions (parties of up to four), G6 the gate. No combat and no death in Part 1. **Part 2 (Milestone 3):** workshops and crafting. **Part 3 (Milestone 4):** the descent gate and floor relocation. |
| **Epic 11** | Agentic Narrative Engine (MCP Integration) | **QUEUED** | External Model Context Protocol integration behind `ILlmNarrativeEnricher` (the official .NET MCP SDK, §2.2), dynamic tavern rumors. |

---

## Track B: UI / UX & Player Interface (Branching Track)

*Status: The epics below were built in Unity's UI Toolkit, frozen from Epic CLIENT M0 (2026-10-18) and removed with Unity (2026-10-24). Their panels were rebuilt in the KNI client (Epic CLIENT M2), where UI work goes.*

### Epic UI-1: The Flat "Facility Dashboard" Menu UI (COMPLETED)
* **Goal**: Deliver a static, web-like menu interface using **Unity UI Toolkit (UXML + USS)** where players inspect facility sectors, view agent statuses, and observe social dynamics without wrestling with 2D camera controls.
* **Architecture**: Dual-View. The 2D spatial simulation continues running as the authoritative headless model. The dashboard groups agents by sector/anchor. Both views coexist and toggle via `Tab`.
* **Key Deliverables**:
  - `FacilityZone` & `FacilityZoneService` mapping world positions to semantic facility sectors.
  - Fullscreen 3-column UXML/USS dashboard with stable object pooling (`0 bytes GC` on hot paths).
  - Unity 6 `PanelSettings` asset and `UnityDefaultRuntimeTheme` automated setup.
  - `ViewToggleController` for instant `Tab` key hotkey view switching.

### Epic UI-Alpha: [The Humanized Narrative Chronicle & Story Feed](epics/epic-ui-alpha.md) (COMPLETED)
* **Goal**: Transform raw debug telemetry into humanized, engaging narrative feeds (`[1 minute ago] Burt shared information with Bob in the Common Hearth`).
* **Architecture**: Pipe-and-Filter Chronicle Service (`Simpiens.Narrative`) with Two-Stage Execution:
  - **Stage 1 (Immediate / Deterministic)**: Instant zero-alloc template synthesis generates readable prose with relative human timestamps (`[Just now]`, `[1m ago]`).
  - **Stage 2 (Async LLM Enrichment Hook)**: Provides the extensible foundation for Track A's **Epic 11 (Agentic Narrative Engine)** to asynchronously upgrade dramatic events into rich flavor text.
* **Key Deliverables**:
  - `CircularEventBuffer` (bounded, thread-safe, zero-alloc event storage per sector and globally).
  - `IAgentIdentityService` (friendly character name generator).
  - `TemplateNarrativeFormatter` (procedural natural language sentence generator).
  - `RelativeTimeFormatter` (`[Just now]`, `[45s ago]`, `[1m ago]`).
  - `FacilityDashboardPresenter` refactored to bind directly to sector-scoped chronicle feeds.

### Epic UI-2: [The Merchant Terminal & Bounty Board](epics/epic-ui-2.md) (COMPLETED)
* **Goal**: Expand `PlayerInterventionService` and UI Toolkit to deliver the core management loop for the trading outpost.
* **Architecture**: Connects the flat UI directly to `PlayerInterventionService` using command-pattern `InfluenceEvent`s, leaving simulation state mutation to `SimulationManager.Tick()`. Standardizes on a reusable `VisualElementPool<TData, TElement>` utility to keep 10 Hz UI refresh decoupled from simulation ticks of up to 100 Hz at 10x (0 bytes GC), using Unity 6 DataBinding selectively for static modal dialogs.
* **Key Deliverables**:
  - **Trade Stall Inspector**: Buy/Sell price sliders for provisions (Rations) and raw salvage (Iron Ore, Medicinal Herbs).
  - **Public Bounty Board Modal**: Post Scout and Gather bounties with adjustable gold payout rewards and target sector selection.
  - **Outpost Treasury & Stockpile HUD**: Real-time display of player gold reserves, trade stall inventory, and active bounty counters.
  - **Sector Shroud Indicators**: Visual indication of unexplored sectors vs. scouted sectors in the sector roster.
  - **Zero-Allocation Element Pooling**: Generic `VisualElementPool<TData, TElement>` delivering 0 bytes GC in 10 Hz UI loops.
  - **Automated Domain Suite**: `BountyTerminalValidator` validating price updates, bounty posting/escrow, cancellation refunds, shroud tracking, and pooling.

### Epic UI-3: Guild Roster & Expedition Dispatch (folded into [Epic 10 Part 1](epics/epic-10.md), 2026-10-23)
*Its roster, party builder and expedition tracker are built in the KNI client as Epic 10's G4 and G5. The goal below is kept as the original intent.*
* **Goal**: Provide management controls for recruited guild adventurers and organized expeditions.
* **Key Features**:
  - Roster card inspector displaying crawler combat readiness, equipment loadout, and dyadic affinity toward the player.
  - 4-man expedition party builder assigning persistent `GroupDirective` contracts.
  - Expedition status tracker showing real-time party cohesion and sector progress.

---

## Track C: Tooling, Diagnostics & Testing Harness (Branching Track)

*Status: Dedicated to maintaining automated verification and headless quality assurance.*

| Epic | Name | Status | Key Deliverables |
| :--- | :--- | :--- | :--- |
| **Epic Test-1** | Visual Anomaly Detection & Automated Scenarios | **COMPLETED** | `VisualAnomalyDetector`, `SwarmPlayModeTests`, `BatchPlaymodeController`, rate-limited screenshot recorder. |
| **Epic Test-2** | Outpost Economy & Bounty Contract Harness | **COMPLETED** (2026-10-15) | Delivered:<br>• headless `BountyFulfillmentValidator`, `TradeTransactionValidator`, `EconomyCountersValidator`, `EconomyTuningValidator`;<br>• `OutpostSupplyLoopScenario`, the live-loop PlayMode scenario required by §1B (passing);<br>• the economy soak `EconomySoakScenario` and `scripts/run-economy-soak.sh` (2026-10-11). Since §3.3 (2026-10-15) the soak plays a fair, a greedy and a generous player and asserts each one's pace band (`Scenario_EconomySoak_PaceBands`, 9/9). |

---

## Independent Epics: Cross-Cutting & World Architecture

*Status: Foundational spatial and environmental architectures that operate orthogonally to the specific feature tracks, providing the physical substrate for all agent lifecycles.*

### Epic MAP: [The Outpost Floor & Living Simulation Loop](epics/epic-map.md)
- **Status**: COMPLETED (2026-10-13; Revision 2 approved by the producer 2026-10-11; the producer's visual check at 1× passed 2026-10-15).
- **Delivered** in five phases, 2026-10-11 to 2026-10-13 (evidence, fixes and decisions in the epic doc):
  1. world as data: `MapDefinition`, `ISectorRegistry`, world bounds from the map, not the camera;
  2. the session split: the pens became the test-only Dev Map, guarded by `PenCanaryScenario`;
  3. the outpost floor goes live: Floor 1, 12 crawlers from the Road Gate, fog of war (D1: nothing in an uncharted sector is known until a scout reports back), bunks;
  4. the player's view: the map, the fog (D1 = A, crawlers visible; the KNI client uses D1 = C since 2026-10-21), the camera, picking, the HUD over the map (D2);
  5. the Definition of Done: `OutpostLifecycleScenario` (9,000 ticks, nothing injected) and `DeterminismScenario`.
- **Producer intent (2026-10-11):** one connected outpost floor where crawlers physically work, sell, eat, socialize and sleep. **Sector count is data, not a target.**
- **Deferred:** the descent gate (Epic 10 Part 3), workshops (Milestone 3), cognition scaling (§3A #8). Drifters, deferred here, came with Epic 10 G1 (2026-10-23); the map's save and load with Milestone 1's whole-session save (2026-10-15). Open findings and their owners: [epic §6](epics/epic-map.md).

### Epic CORE: [Engine-Free Core and Headless .NET](epics/epic-core.md)
- **Status**: COMPLETED (2026-10-17). Merged to `main` as PR #5 on 2026-10-17.
- **What:** Phases 0 and 1 of the Unity-to-Godot migration write-up (Claude Docs, 2026-10-15), approved as standalone work. The simulation runs without an engine: the same source compiled in a Unity assembly that might not reference the engine (`noEngineReferences`) and for `net10.0` (`dotnet/`), where a host steps ticks with no frames, and both produced the same golden world hashes. Since Unity's removal (2026-10-24) the core lives in `dotnet/Simpiens.Core`. The write-up's later phases became the engine spike, which chose KNI over Godot (2026-10-18): [Epic CLIENT](epics/epic-client.md).
- **Producer decisions (2026-10-17):** Phases 0–1 under the agent's provisional authority; no web builds; `Microsoft.Extensions.DependencyInjection` and xUnit v3 for .NET; the §2 library retirements and the MCP SDK lift.
- **Delivered:** Phase 0, decouple (C1 cognition on fixed ticks, resolving §1A.2 #6, #15 and #18; C2 golden world-hash baselines; C3 `EntityId`; C4 engine-free maths; C5 entities as data, #16; C6 infrastructure out of the core; C7 the `Simpiens.Core` assembly) and Phase 1, headless .NET (C8 the `dotnet/` solution; C9 xUnit v3 tests and the DoD scenarios headless; C10 the `simpiens` console host; C11 CI). Chunk by chunk, with measurements: the epic's checklist.
- **Findings** ([epic §6](epics/epic-core.md)): F1, Unity's Mono and CoreCLR rounded some float chains differently, narrowed by Epic 10 G1 and G2, **closed 2026-10-24** with Unity's removal: one runtime remains. F2, the outpost's day failing on 7 of 23 seeds, was resolved by Epic 10 G0 (2026-10-23).

### Epic CLIENT: [KNI Client and Engine Rework](epics/epic-client.md)
- **Status**: PAUSED (2026-10-24) for core gameplay ([Epic 10 Part 1](epics/epic-10.md)), whose new screens are built in this client, after M2.8–M2.9: **Unity was removed** (producer: "remove Unity first"). It was PAUSED on 2026-10-23 and ACTIVE again for the removal on 2026-10-24.
  - **Exists**, merged into `main` as PR #6 on 2026-10-23: M0 (first pixels), M1 (top-down parity), M2.0–M2.7 (the dashboard in Gum, parity with Unity's game), the text cleanup (baked Inter, drawn 1:1), the client seams (projections, draw roles, per-tick view frames), M3 (isometric as the game's view; hard and dynamic cutscenes) and the architecture review's findings (host commands, one input frame, the pinned test image).
  - **Producer sign-offs (2026-10-24):** the side-by-side with Unity, the look at M3 (isometric, wall styles, cutscenes), the motion between ticks, and the header's pause and speed all passed. The producer: "we can officially move away from Unity." **The gate of M2.8–M2.9 is met.**
  - **Done 2026-10-24:** M2.8 (every Unity test has a .NET twin or a recorded reason) and M2.9 (Unity removed: the core and its harness live in `dotnet/`). **Not done:** M4 Android (deferred 2026-10-22), M5 iOS, M6 screen sizes, M7 the art pipeline, and the developers' map designer.
- **What:** a game client on KNI over the engine-free core, replacing Unity's views and dashboard. Isometric is the main view, top-down is for design and debugging, cutscenes are in screen space. Windows and Linux desktop for the MVP; Android and iOS after it. The core never references KNI. Proposal and comparison: [`docs/engine-proposal-kni.md`](engine-proposal-kni.md).
- **Producer decisions:**
  - 2026-10-18: KNI, with MonoGame compiling in CI as its fallback; Godot dropped. Phones in portrait only, tablets not a priority, consoles last, no macOS or web. Art up to 64×64 px; 30 fps. **Unity's views are frozen from M0, and Unity retires at M2** (done 2026-10-24). The map designer is for developers only.
  - 2026-10-18 (M2): Gum's built-in visuals in Unity's palette, Unity's layout first, and a Definition of Done in `dotnet/` once Unity is gone (now §1B).
  - 2026-10-21: the fog D1 = C in the client (it hides crawlers too); M3's two kinds of cutscene (hard ones pause the game, dynamic ones are part of play), no rotation, dynamic scenes start on a zone entry or a crawler-to-crawler interaction and never move crawlers, and the player keeps control.
  - 2026-10-22: M4 deferred. 2026-10-23: the client went into `main` before M4 (`main-kni` retired), then the epic paused.
- **Milestones and progress, chunk by chunk with measurements:** the epic doc (§3) and `.agents/architecture_history.md`.

---

## Strategic Operating Rhythm

When working in parallel agent sessions:
1. **Engine Work**: Reference **Track A** (Milestone 1 is `COMPLETED`; **Epic 10 Part 1 is `ACTIVE`** (Milestone 2, [epics/epic-10.md](epics/epic-10.md)), G0 (the day on every seed, Epic CORE's F2), G1 (drifters) and G2 (health and hazards) done 2026-10-23, the producer's look at G2 passed 2026-10-25; G3 (gear) done 2026-10-25; G4 (the guild) next; **Epic CORE** is `COMPLETED` and merged). The simulation lives in `dotnet/Simpiens.Core` (`Simpiens.Cognition`, `Simpiens.Simulation`, …), its validators in `dotnet/Simpiens.Testing.Core`.
2. **UI Work**: Reference **Track B** (Epic UI-2 `COMPLETED`; no UI epic is `ACTIVE`; Epic UI-3 is folded into Epic 10 Part 1). New views and UI go into the KNI client (`dotnet/Simpiens.Presentation` for what is drawn, as data; `dotnet/Simpiens.Client` for Gum and XNA).
3. **Testing Work**: Reference **Track C** (Epic Test-2 `COMPLETED` 2026-10-15; no Track C epic is `ACTIVE`). New live scenarios go headless into `dotnet/Simpiens.Core.Tests` (§1B); engine-free validators live in `Simpiens.Testing.Core`; the shared harness (`OutpostWatch`, `AnomalyDetector`, `EconomySoak`) in `Simpiens.Hosting`.
4. **World & Environment Work**: Reference **Independent Epics** (**Epic MAP** is `COMPLETED`; **Epic CORE** is `COMPLETED`; **Epic CLIENT** is `PAUSED`: the KNI client in `dotnet/`, Unity removed). Code lives in `Simpiens.Simulation.World` (map definition, sectors, hazards) and `Session/` (sessions, layouts) in `dotnet/Simpiens.Core`; views in the KNI client.
5. **Every session**: follow §0 (document hierarchy and update order), §1A (invariants) and §1B (Definition of Done).

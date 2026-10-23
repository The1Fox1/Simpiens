# Simpiens Master Project Roadmap (Multi-Track Architecture)

This document is the **highest source of truth** for project status, scope, sequencing, engineering invariants and the Definition of Done across the Simpiens simulation framework. *Last reconciled: 2026-10-23.*

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
        CLIENT["Epic CLIENT\nKNI Client &\nEngine Rework\n(PAUSED)"]
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

### 1A.2 Known Deviations (as of 2026-10-09; #17–#19 added 2026-10-17)

These violate §1A.1 or the project architecture rules today. They are recorded so that new code does not copy them. They are not scheduled work by themselves; scheduling is a producer decision. **Scheduled 2026-10-17:** Epic CORE fixed #6, #15 and #18 (chunk C1) and #16 (chunk C5a), all 2026-10-17; #17 and #19 are recorded only.

| # | Deviation | Violates | Where |
| :-: | :--- | :--- | :--- |
| 1 | ~~`SimulationManager.Tick()` runs once per rendered frame, and walking, harvest/share/idle durations, need decay and watchdog timers use `Time.deltaTime`, while bounty/memory/gossip/hearth timers use logical ticks.~~ **RESOLVED (2026-10-08):** Fixed-tick architecture implemented; simulation state advances exclusively on `ISimulationClock.OnSimulationTick` at fixed 10 Hz (`dt = 0.1s`), with visual interpolation in views. | 1 | [`SimulationManager.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/SimulationManager.cs), [`AutonomousAgent.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AutonomousAgent.cs), [`SimulationClock.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/SimulationClock.cs) |
| 2 | ~~`AutonomousAgent` is a `MonoBehaviour` that owns needs, inventory, memory, group membership and the active plan, and applies action consequences. Pawn position is `transform.position`.~~ **RESOLVED (2026-10-08):** Pure C# `AgentState` manages needs, inventory, memory, goals, and tick logic headlessly in `IAgentRegistry`. Intent consequences are applied authoritatively by `IAgentConsequenceService`, async cognition is coordinated by `IAgentCognitionService`, and `AutonomousAgent` acts as a thin view/façade. | 5, "No Logic in MonoBehaviours" | [`AgentState.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AgentState.cs), [`AgentRegistry.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/AgentRegistry.cs), [`AutonomousAgent.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AutonomousAgent.cs) |
| 3 | ~~Background planners read live `AgentMemory` / `AgentInventory` (non-thread-safe dictionaries) while the main thread mutates them (gossip, trade affinity). The snapshot is `Retain()`ed only after `SwitchToThreadPool()`.~~ **RESOLVED (2026-10-09):** Single-Writer Threading Architecture enforced. `SharedWorldSnapshot.Retain()` invoked on main thread before thread pool dispatch; `IAgentMemory` and `IAgentInventory` provide preallocated double-buffered zero-allocation snapshots (`AgentMemorySnapshot`, `AgentInventorySnapshot`) for off-thread cognition; `GetAffinity` refactored to be pure side-effect-free calculation (no dictionary writes on read); and panic memory pruning moved authoritatively to main thread in `AgentConsequenceService`. | 6 | [`AgentMemory.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Memory/AgentMemory.cs), [`AgentCognitionService.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AgentCognitionService.cs), [`AgentInventory.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Inventory/AgentInventory.cs) |
| 4 | ~~`InfluenceEvent` carries a string command name and a comma-separated string payload parsed with `Split`.~~ **RESOLVED (2026-10-09):** `InfluenceEvent` converted to a typed zero-allocation command union (`PostBountyCommand`, `CancelBountyCommand`, `UpdateStallPricesCommand`) with static factory methods; UI presenters dispatch typed commands directly; legacy string constructor preserved for backward compatibility. | 2 | [`InfluenceEvent.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Intervention/InfluenceEvent.cs), [`SimulationManager.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/SimulationManager.cs) |
| 5 | ~~Unseeded RNG in simulation paths (`[ThreadStatic] System.Random` in evaluators).~~ **RESOLVED (2026-10-10, closure Phase 2):** `SimRandom` gives stateless, seeded streams keyed by (seed, entity, tick, salt). The seed comes from `SimulationSettings` (`SIMPIENS_SEED` / `-simseed`), and `EntityIdAllocator` makes entity ids reproducible per seed. Remaining `UnityEngine.Random` calls are view-only (`NodeController` bounce direction, `NodeFactory` sprite colour). `OutpostMapBuilder` (not live) and `NodeFactory` still call `GUID.Generate()`. | 4 | [`SimRandom.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/SimRandom.cs), [`EntityIdAllocator.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/EntityIdAllocator.cs) |
| 6 | ~~Completion order of background cognition determines the order intents are enqueued; which tick a result arrives on depends on thread timing.~~ **RESOLVED (2026-10-17, Epic CORE C1):** a `CognitionScheduler` applies every decision dispatched on tick T at the start of tick T + 1, behind a barrier, in agent-id order, so neither the tick nor the order depends on thread timing. `DeterminismScenario` now passes alone (3 of 3 runs identical; before, a cold run diverged at t100 in 5 of 5) with the same milestones as before (first cycle 235, scout 382, gather 605, third sleeper 865), and the economy soak is unchanged (fair 546–771g at 30 minutes). History: 2026-10-10 same-tick arrivals were applied in agent-id order; 2026-10-13 and 2026-10-15 the full-suite divergences are recorded in `.agents/architecture_history.md`. | 3 | [`CognitionScheduler.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/CognitionScheduler.cs), [`SimulationManager.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/SimulationManager.cs) |
| 7 | ~~`AsyncPathfinder` is a straight-line stress-test placeholder, not Grid A*.~~ **RESOLVED (2026-10-09):** Upgraded `AsyncPathfinder` into a zero-allocation 8-directional Grid A* engine with preallocated binary min-heap, $O(1)$ epoch visited tracking, diagonal corner-cutting prevention, raycast string-pulling path smoothing, and concurrent searcher worker pooling. Separated static map obstacles (`StaticNavGrid`) from per-frame dynamic entity snapshots. | — (Epic 9 carry-over) | [`AsyncPathfinder.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Pathfinding/AsyncPathfinder.cs), [`GridAStarSearcher.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Pathfinding/GridAStarSearcher.cs), [`StaticNavGrid.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/Spatial/StaticNavGrid.cs) |
| 8 | `GoapPlanner` is a DI singleton guarded by a `lock`, so all agents' planning is serialized. | Dual-loop scaling intent | [`GoapPlanner.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/GoapPlanner.cs) |
| 9 | ~~The production GOAP domain (action graph, goals) and session population live under `Testing/` in `SwarmSpawner`; validators are registered as boot-time `IStartable`s and ship in the single `Simpiens.asmdef`.~~ **RESOLVED (2026-10-11, closure Phase 5):** domain in `Cognition/Planning/Domain/OutpostGoapDomain`, session in `Core/Session/SwarmSpawner`. Test code is in a separate `Simpiens.Testing` assembly (`UNITY_INCLUDE_TESTS` only), wired in through `BootstrapExtensions`. Validators run as PlayMode tests, not at boot. | Test isolation | [`OutpostGoapDomain.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/Domain/OutpostGoapDomain.cs), [`AppBootstrapper.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/AppBootstrapper.cs) |
| 10 | ~~`CognitiveEngine`, `IContextBuilder` and `IToolExecutionRegistry` are registered/declared but unused; the real cognition loop is `AutonomousAgent.EvaluateCognitionAsync`.~~ **RESOLVED (2026-10-09; trade gold routed through the ledger 2026-10-11):** `IToolExecutionRegistry` implemented via `ToolExecutionRegistry` mapping cognitive tool execution to typed intents, backed by `IIntentHandlerRegistry` with 8 modular intent handlers (`WanderIntentHandler`, `PanicIntentHandler`, `HarvestResourceIntentHandler`, etc.). `SimulationManager` implements `IIntentExecutionContext` and delegates execution to modular handlers. Authoritative `IEconomyLedger` introduced to govern all gold flows and fix escrow minting leaks. The `CognitiveEngine`/`ContextValidator` stub was deleted 2026-10-11; `IContextBuilder` is kept as the Epic 11 seam. | — | [`ToolExecutionRegistry.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/ToolExecutionRegistry.cs), [`IntentHandlerRegistry.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/Handlers/IntentHandlerRegistry.cs), [`SimulationManager.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/SimulationManager.cs), [`EconomyLedger.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/Economy/EconomyLedger.cs) |
| 11 | ~~Tier-1 `SurvivalUtilityEvaluator` returned a non-null intent (Idle/Wander/Harvest) on every path, so Tier-2 GOAP never ran in the live session; every GOAP goal from Epics 6–9 only worked under validator injection.~~ **RESOLVED in code (2026-10-09, review §3.1):** the evaluator is now a pure hunger reflex (Hunger ≥ 60 + reachable known food, else `null`); idle/wander moved to the Tier-3 `AmbientWanderEvaluator` fallback; `HarvestResourceAction`, `TravelToResourceAction` and `SearchResourceAction` emit real intents. Regression covered by `SupplyLoopValidator` Test 7. Live verification pending the first `OutpostSupplyLoopScenario` run. | §1B (status accuracy) | [`SurvivalUtilityEvaluator.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Evaluators/SurvivalUtilityEvaluator.cs), [`AgentCognitionService.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AgentCognitionService.cs) |
| 12 | ~~No live population could run the Milestone 1 loop: all session agents were scripted into walled demo pens, the trade stall sat inside Pen 3, and the seeded Scout bounty targeted walled Pen 7.~~ **RESOLVED in code (2026-10-09, review §3.1):** the open yard north of the pens is now the *Outpost Commons* (stall, Ore Ridge, Herb Hollow, food bushes) with 6 free crawler agents (`SpawnCount` 24 → 30). | §1B | [`SwarmSpawner.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Testing/Environment/SwarmSpawner.cs) |
| 13 | ~~Seeded bounties bypassed escrow (`RegisterBounty` locked nothing), so delivery minted gold via a direct `AddGold`.~~ **RESOLVED in code (2026-10-09, review §3.1):** `RegisterBounty` routes through the same escrow lock as `PostBountyCommand` and rejects un-escrowable bounties; delivery pays only by releasing escrow. Session gold enters through a single `InitialSeed` ledger faucet. | Ledger conservation | [`SimulationManager.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/SimulationManager.cs), [`DeliverBountyIntentHandler.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/Handlers/DeliverBountyIntentHandler.cs) |
| 14 | ~~`SimulationManager` executes active intents by enumerating the `_activeIntents` `Dictionary` (and stalls, bounties and anchors the same way).~~ **RESOLVED (2026-10-10):** active intents run in agent-id order (sorted id list), the stall/bounty/expiry/anchor buffers are sorted, and harvest contention picks the lowest-id competitor. Mutation-checked by `DeterminismValidator` Test 3. Unaudited: `foreach` over agent memory maps in GOAP actions (e.g. `TravelToAnchorAction` nearest-anchor ties). | 3 | [`SimulationManager.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/SimulationManager.cs) |
| 15 | ~~The background cognition task writes per-agent state off the main thread: plan state (`AbortActivePlan`, `ActivePlan.CopyFrom`, `ActiveGoal`), `CurrentIntent`/`HasActiveIntent` and `VisualState`; `AgentState.Tick` reads `ActivePlan.CurrentAction` while cognition rewrites the plan (architecture scan H2).~~ **RESOLVED (2026-10-17, Epic CORE C1):** the evaluation returns its decision as data (`CognitionResult`: the intent, the plan to abort or adopt, the goal) and writes only its own scratch plan; the main thread applies it at the due tick. Visual state is set during the main-thread preparation. | 6 | [`AgentCognitionService.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AgentCognitionService.cs) |
| 16 | ~~`AutonomousAgent.ManualTick` copies `transform.position` into `AgentState.Position` every tick, so the view feeds the simulation's position (latent: crawler views never interpolated, so the copy equalled the logical position).~~ **RESOLVED (2026-10-17, Epic CORE C5a):** the simulation works with plain-data `WorldEntity` records (a crawler's position is its `AgentState.Position`), the per-crawler tick is the engine-free `AgentRuntime`, and `NodeController` and `AutonomousAgent` are views that only read state. Golden world hashes identical. | 5 | [`WorldEntity.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/WorldEntity.cs), [`AgentRuntime.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AgentRuntime.cs) |
| 17 | No `try`/`catch` on the tick path. `SimulationManager.SimulateTick` completes intents inside its loop but removes them after it, so one handler exception leaves finished intents in place: they run again next tick and return their `PathResponse` to the pool twice. (Architecture scan H1, 2026-10-15; recorded 2026-10-17.) | Robustness | [`SimulationManager.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/SimulationManager.cs) |
| 18 | ~~`AgentCognitionService` sets `IsEvaluating` before its `try`/`finally`, so an exception in the main-thread preparation freezes that crawler for good (architecture scan M3).~~ **RESOLVED (2026-10-17, Epic CORE C1):** the preparation is guarded and clears the flag on failure; a failed or cancelled evaluation is released at its due tick (`CognitionSchedulerValidator`). | Robustness | [`AgentCognitionService.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AgentCognitionService.cs) |
| 19 | A posted bounty gets the command's id, or `_bounties.Count + 1` when it has none, and nothing checks for an existing id: a bounty whose id is taken overwrites the old one. A save keeps finished bounties, so the count survives a load. (Architecture scan M4, 2026-10-15; recorded 2026-10-17.) | Robustness | [`SimulationManager.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/SimulationManager.cs) |

### 1A.3 Logging (Producer decision, 2026-10-09)

All application logging goes through `Simpiens.Diagnostics.SimLog` (ZLogger, §2.1). Binding for new code:

1. **No `UnityEngine.Debug.Log*` in application code.** Validators log test output through `Simpiens.Testing.TestLog` (framework-free since Epic CORE C6; Unity routes it to its console).
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
2. **Live emergence demonstrated.** At least one live scenario shows the feature occurring *naturally* in the real session setup. Since 2026-10-23 (producer, Epic 10 Q8) new work proves this headless in `dotnet test` (`dotnet/Simpiens.Core.Tests`, on the real session); earlier epics did it with a PlayMode scenario (Track C harness: `Assets/Tests/PlayMode/`), and Unity's PlayMode suite stays a regression check on the core while Unity exists. A scripted player that acts only through the typed commands a real player sends is input, not injection. It must **not** inject the outcome's preconditions. It asserts a concrete outcome within a bounded number of ticks (e.g. *"within N ticks, at least one posted Gather bounty is claimed, fulfilled and paid out"*).
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
| **`VContainer`** (`jp.hadashikick.vcontainer`) | Core Dependency Injection | Pure C# DI container managing `ITickable` and `IStartable` lifecycles outside of `MonoBehaviour` scene hierarchies. |
| **`UniTask`** (`com.cysharp.unitask`) | Zero-Alloc Async Primitives | Off-thread deliberative background workers (`UniTask.SwitchToThreadPool()`), non-blocking IO, and deterministic timing. |
| **`MemoryPack`** (`MemoryPack` 1.21.4 via NuGet DLLs) | **Zero-Alloc Binary Serialization** *(NEW APPROVED; installed 2026-10-15)* | High-performance C# source-generated binary serializer. Native memory-copy (`Unsafe.CopyBlock`) for unmanaged blittable structs (`ItemInstance`, `BountyContract`, `GroupData`, `WorldState`, `ChronicleEvent`). Powers state persistence, save/load, tick replays, and the Milestone 4 floor descent relocation loop.<br>• **Install:** vendored into `Assets/Plugins/MemoryPack/` by `scripts/update-memorypack.sh`.<br>• **Shared dependencies:** MemoryPack is resolved together with ZLogger, so `System.Collections.Immutable` and `System.Runtime.CompilerServices.Unsafe` ship once, from ZLogger's folder.<br>• **Source generator:** runs as a Roslyn analyzer and never ships in a player.<br>• **`MemoryPack.Unity`:** not installed, because no Unity reference types are saved.<br>• **Callers:** only `Simulation/Persistence/SaveSerializer` calls MemoryPack.<br>• **Used by:** the whole-session save and load (Milestone 1, 2026-10-15): `SessionSave`, `SaveFile`, the quick-save slot. |
| **`ZLogger`** (`ZLogger` 2.5.10 via NuGet DLLs + `com.cysharp.zlogger` ZLogger.Unity) | **Zero-Alloc Structured Logging** *(NEW APPROVED, 2026-10-09)* | Microsoft.Extensions.Logging levels with interpolated-string handlers that evaluate and allocate nothing when a level is disabled. Wrapped by `Simpiens.Diagnostics.SimLog` (see §1A.3). The NuGet closure is vendored in `Assets/Plugins/ZLogger/` by `scripts/update-zlogger.sh` (facades Unity already provides are excluded). `Assets/Scripts/csc.rsp` raises the `Simpiens` assembly to C# 10. Keep the ZLogger.Unity git tag in `Packages/manifest.json` in sync with the vendored version. |
| **`Unity.Collections` + Burst** (`com.unity.collections`, `com.unity.burst`) | **Main-Thread Spatial Broadphase** *(NEW APPROVED - HYBRID)* | Unmanaged off-heap containers (`NativeArray`, `NativeParallelMultiHashMap`) and SIMD-accelerated Burst jobs. Applied strictly on the main thread for physics broadphase, campfire proximity, and sector bounds in `SimulationManager.Tick()`. *(Note: Off-thread cognitive workers retain pure C# flat struct arrays in `SharedWorldSnapshot` to prevent `AtomicSafetyHandle` thread-pool exceptions).* |

> **Producer decision, 2026-10-18 (Epic CLIENT):** the game client is built on **KNI** (`nkast.Xna.Framework.*` and `nkast.Kni.Platform.*` 4.3.9001, MS-PL), a MonoGame-derived implementation of the XNA 4 API, chosen over Godot, MonoGame, FNA and BRUTAL in the engine spike. **MonoGame** (`MonoGame.Framework.DesktopGL`) is approved only as KNI's fallback: CI compiles the client against it so that a switch stays a package swap. Gum (`Gum.KNI`) is planned for the dashboard (Epic CLIENT M2). The comparison and its measurements: [`docs/engine-proposal-kni.md`](engine-proposal-kni.md).

> **Install status (verified against `Packages/manifest.json` 2026-10-09; MemoryPack 2026-10-15; UniTask removed 2026-10-17):** `VContainer`, `ZLogger` and `MemoryPack` are installed; `UniTask` was removed by Epic CORE C6. `Unity.Collections` / Burst are **approved but not installed**; no code uses them yet.

> **Producer decisions, 2026-10-17 (the migration write-up's checklist; Epic CORE):**
> - **Retire the `VContainer`, `UniTask` and `Unity.Collections` + Burst rows.** Engine-free code drops VContainer and UniTask during Epic CORE (Phase 0). VContainer stays Unity's composition root until a cutover, if the Godot spike says go. `Unity.Collections` + Burst won't be installed; .NET's own SIMD types replace them after a cutover.
> - **`Microsoft.Extensions.DependencyInjection`** is the container for .NET hosts (`dotnet/Simpiens.Hosting`). The engine-free core stays container-agnostic: constructor injection only.
> - **xUnit v3** runs the .NET tests under `dotnet test`. The Unity Test Framework (NUnit) keeps running the Unity suite until a cutover.
> - The table above stays as written until each retirement lands; Epic CORE updates it.
> - **Progress (2026-10-17, Epic CORE C6):** no code uses UniTask any more (cognition, saves, the narrative hook and the persistence smoke use `Task`/`ValueTask`), and the `com.cysharp.unitask` package is removed from `Packages/manifest.json`. Engine-free code carries no VContainer attribute or lifecycle interface; VContainer is only Unity's composition root (`AppBootstrapper`) and the Unity-side services it builds.
> - **Epic CORE completed (2026-10-17):** the core builds for .NET 10, where `Microsoft.Extensions.DependencyInjection` composes it (`dotnet/Simpiens.Hosting`) and xUnit v3 tests it (`dotnet/Simpiens.Core.Tests`). VContainer and the Unity Test Framework stay on the Unity side until a cutover.

### 2.2 Rejected 3rd-Party Frameworks & Handroll Policies

1. **2D Pathfinding & Navigation Engine**:
   - **Rejected**: *A* Pathfinding Project (Granberg)* and *Unity AI Navigation*. Both heavily couple to `MonoBehaviour` components (`Seeker`, `AIPath`, `NavMeshAgent`) or main-thread NavMesh carving, which breaks headless domain testing and stalls high-speed ticks (5x–10x).
   - **Handroll Policy**: Upgrade [`AsyncPathfinder.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Pathfinding/AsyncPathfinder.cs) into a zero-allocation 2D Grid A* / Jump Point Search (JPS) engine by porting the array-backed binary min-heap and struct node pool architecture already proven in [`GoapPlanner.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/GoapPlanner.cs).
2. **Model Context Protocol (MCP) Integration**:
   - **Rejected**: *Official .NET MCP C# SDK (`modelcontextprotocol/csharp-sdk`)*. Relies on ASP.NET Core Hosting and modern .NET runtime assemblies that collide with Unity's Mono/IL2CPP `netstandard2.1` target.
   - **Rejection lifted (producer, 2026-10-17):** Epic 11 may use the official SDK (`ModelContextProtocol`, targets `net10.0`) once the simulation runs on .NET. Inside Unity the handroll policy below still applies.
   - **Handroll Policy**: Implement a lean, pure-C# UniTask JSON-RPC 2.0 / SSE client (~250 LOC) under [`ILlmNarrativeEnricher`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Narrative/ILlmNarrativeEnricher.cs) with an automatic circuit breaker falling back to [`TemplateNarrativeFormatter`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Narrative/TemplateNarrativeFormatter.cs).
3. **UI Toolkit Reactive Data-Binding**:
   - **Policy**: Reserve Unity 6 built-in runtime data binding (`INotifyPropertyChanged`) exclusively for low-frequency modal dialogs and player input forms. For high-frequency dynamic simulation feeds (logical ticks run at 10 Hz at 1x and up to 100 Hz at 10x clock speed), standardize on throttled (10 Hz) element pooling via a reusable `VisualElementPool<TData, TElement>` utility modeled after [`FacilityDashboardPresenter.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/UI/FacilityDashboardPresenter.cs).

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

**`COMPLETED` (2026-10-15), pending the producer's check of save and load at 1×.**

- **The live loop emerges** on the Floor 1 outpost with nothing injected: the player sets prices and posts bounties, crawlers harvest salvage and sell it at the stall, and trust toward the player grows. Evidence: `OutpostSupplyLoopScenario` (2026-10-10) and `OutpostLifecycleScenario` (the whole day; trust up for ⅔ of crawlers at the default prices, 2026-10-15).
- **Its epics are `COMPLETED`:** Epic 9 (economy and bounties, with §3.1 supply, §3.2 personality and §3.3 the economy model, [docs/economy-model.md](economy-model.md)), Epic UI-2 (merchant terminal, bounty board), Epic Test-2 (economy harness, soak with pace bands) and Epic MAP (the outpost floor, fog of war, the player's view). Zero-allocation Grid A* was delivered 2026-10-09.
- **MemoryPack serialization was delivered 2026-10-15** as a whole-session save and load (the producer's reading, D1 = B):
  - one quick-save slot, with Save and Load in the dashboard header (D2 = A);
  - a load puts back every crawler (needs, purse, pack, memories, relationships), the stall, the bounties and their escrow, the fog, regrowth and the chronicle; crawlers decide afresh on the next tick (D3 = A);
  - a save from another format, session, seed or map is refused with a reason (D4 = A);
  - Epic 9's deferred validators pass: `EconomySerializationValidator` and `EconomyZeroAllocValidator` (D5 = A);
  - live Definition of Done: `SaveLoadScenario` saves the outpost while a crawler holds the Gather bounty, loads it into a fresh session (byte-identical, equal world hash) and plays 3,000 more ticks: zero anomalies, the ledger balanced, nobody starving, every crawler eating, a harvest-to-sell cycle, and the claimed bounty delivered. It passed in 3 of 3 consecutive full-suite runs (2026-10-15).
- **Pending:** the producer's check of save and load at 1× in the editor.
- History: the closure sequence below (§3A) and `.agents/architecture_history.md`.

*Closure plan: approved 2026-10-09 and complete. Next: the soak test and scaling work (§3A #8) before Epic 10, which remains `QUEUED`.*

---

## 3A. Architecture Review Remediation Tracker

Progress against the consultant review [`docs/reviews/architecture_review.md`](reviews/architecture_review.md) (2026-10-08), §5 *Prioritised Recommendations*. Verified against the code on 2026-10-09. The review itself is a dated snapshot; **this table is the live status**.

| Review # | Recommendation (section) | Status | Evidence / what remains |
| :-: | :--- | :--- | :--- |
| 1 | Fixed-tick simulation; durations in ticks; views interpolate (§2.1) | ✅ Done | §1A.2 #1. `SimulationManager` runs on `OnSimulationTick`; frame time remains only in views (`AgentMotor`, `AgentView`, `CampfireView`) and UI refresh. |
| 2 | Close the Milestone 1 supply side (§3.1) | ✅ Done; live scenario passes | §1A.2 #11–13, *Milestone 1 Status*. `OutpostSupplyLoopScenario` passes with zero anomalies (2026-10-10). |
| 3 | Single-writer threading; fix `Retain` ordering (§2.3) | ✅ Done | §1A.2 #3. |
| 4 | Grid A* + static walkability, gating MAP (§2.6) | ✅ Done | §1A.2 #7, plus static reachability regions. Navigation clearance follow-ups (grid reset, wall-checked watchdog, body clearance) done 2026-10-10. |
| 5 | Agent personality + utility "considerations" for target selection (§3.2, §2.7) | ✅ Done (2026-10-11) | `AgentPersonality` (5 traits, seeded; pen agents neutral), `Considerations` (reservation/minimum prices, bounty score, trait factors on goal priorities; neutral = unchanged). Live: `PriceResponseScenario` shows gradual demand (e.g. 6/5/5/1/1/0 buyers at 10–60g). Risk tolerance is reserved for Epic 10. |
| 6 | Intent-handler registry, typed `InfluenceEvent`, gold ledger (§2.4) | ✅ Done | §1A.2 #4, #10, #13. `SimulationManager` down from ~1,500 to ~1,190 lines. The legacy string `InfluenceEvent` constructor still exists. |
| 7 | Plain-C# `AgentState` + `AgentRegistry`; `AutonomousAgent` as view (§2.2) | ✅ Done | §1A.2 #2. Save/load delivered 2026-10-15: a session round-trips byte for byte through `SessionSave`. The last latent breach (the view fed the position, §1A.2 #16) was closed 2026-10-17 by Epic CORE C5a. |
| 8 | Profiled soak test → spatial-hash queries, per-thread planners, cognition scheduler (§2.5) | ⬜ Not started | `GoapPlanner` is still a locked singleton (§1A.2 #8). `CognitionScheduler` (Epic CORE C1) lands decisions on fixed ticks but doesn't budget them. The economy soak exists (Epic Test-2) and runs headless in seconds (`simpiens soak`, Epic CORE C10), as does a seed sweep of the outpost's day (`simpiens sweep`: 16 of 23 seeds pass, Epic CORE §6 F2), but there is no profiled scaling soak (`PopulationScalingProbe` is a one-off measurement). No measured agent budget. |
| 9 | Gameplay scope, separate test asmdef, remove stub engine (§2.8) | ✅ Done (2026-10-11) | §1A.2 #9. All 40 PlayMode tests pass with the new assembly layout. Not verified: a player build (relies on the standard `UNITY_INCLUDE_TESTS` constraint). A dedicated `GameplayLifetimeScope` / ScriptableObject-driven session definition was not done (`AppBootstrapper` stays the single scope). |
| 10a | One-page economy model: faucets, sinks, scarcity lever (§3.3) | ✅ Done (2026-10-15); producer play session passed | [docs/economy-model.md](economy-model.md). Decided under the producer's provisional authority, D1–D7 as recommended, then reviewed and merged by the producer (2026-10-15, PR #3): a caravan salvage quota (faucet), outpost upkeep (sink), fairness judged by each crawler's own valuation, crawlers' willingness to trade as the scarcity lever, food paced by data plus one behaviour fix, drifters deferred to Milestone 2. Every number is one `EconomyTuning` per session. The soak asserts a fair, a greedy and a generous player's pace band. Evidence: [Epic 9 §8.5](epics/epic-9.md). |
| 10b | "Why?" agent inspector (§3.4) | 🟨 Groundwork | `DecisionSummary` (source tier + top 3 goal priorities) recorded on every intent and copied to `AgentState.LastDecision` on the main thread (2026-10-11). The inspector UI is not started (Track B). |
| 10c | Live-scenario Definition of Done (§4.2) | ✅ Done | §1B. |
| 10d | Docs single source of truth (§4.3) | ✅ Done | §0. |

Other review items still open:
- **GOAP fact budget (§2.7):** 36 of 64 facts used (`StateFact` 0–35).
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

Before Epic 10: soak test and scaling work (#8).

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
| **Epic 10** | [Part 1: The Guild and Its Expeditions](epics/epic-10.md); Parts 2 and 3: workshops, the descent | **ACTIVE** (Part 1, 2026-10-23; G0, G1 and G2 done 2026-10-23; G2 awaits the producer's look) | **Part 1 (Milestone 2):** G0 a day that holds on every seed (Epic CORE F2), G1 drifters, G2 health and environmental hazards, G3 gear, G4 the guild (recruitment, wages, quitting), G5 expeditions (parties of up to four), G6 the gate. No combat and no death in Part 1. **Part 2 (Milestone 3):** workshops and crafting. **Part 3 (Milestone 4):** the descent gate and floor relocation. |
| **Epic 11** | Agentic Narrative Engine (MCP Integration) | **QUEUED** | External Model Context Protocol integration via lean pure-C# UniTask JSON-RPC/SSE client (`ILlmNarrativeEnricher`), dynamic tavern rumors. |

---

## Track B: UI / UX & Player Interface (Branching Track)

*Status: Dedicated to delivering clean, responsive UI Toolkit interfaces that monitor and influence the simulation.*

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
- **Status**: COMPLETED (2026-10-13; Revision 2 approved by the producer, 2026-10-11).
  - **Phase 1, world as data: done 2026-10-11** (PlayMode 42/42 ×3; live supply loop unchanged with 0 anomalies).
  - **Phase 2, session split: done 2026-10-11** (PlayMode 43/43; live supply loop passes on the pen-free yard with 0 anomalies; `PenCanaryScenario` and `SessionSwitchScenario` pass; producer visual check of the Dev Map button passed 2026-10-11). The canary found and fixed a real regression: Pen 4's altruism had never worked since Grid A* (a hearth on a cell corner sealed the 2-cell pen).
  - **Phase 3, the outpost floor goes live: done 2026-10-12** (PlayMode 47/47 ×3; producer visual check passed 2026-10-12, with crawlers lying down in their bunks added on request):
    - Floor 1 is the live map with 12 crawlers walking in from the Road Gate; the Commons yard is retired.
    - **Fog of war** (producer decision D1): nobody knows what is inside an uncharted sector until a scout visits it and successfully reports back; the report publishes the chart to every crawler.
    - **Bunks** (the rest anchor): tired crawlers walk to the Bunkhouse and sleep, one per bunk.
    - Fixes found by the new live scenarios: crawlers kept selling ore owed to a claimed bounty; agents were blind while walking; starvation panic never left the room; the whole population reached starvation panic together at the start (crawlers now arrive fed); a starving crawler at the stall with gold panicked instead of buying; the scout charted only part of its room. Phase 2 findings #2 (duplicate leadership events) and #3 (paths through hearths) fixed.
    - The economy soak re-ran on Floor 1: the numbers for the §3.3 decision are in the plan's §9.
  - **Phase 4, the player's view: done 2026-10-12** (PlayMode 52/52 ×3; producer visual check passed 2026-10-12, including the mouse and keyboard controls):
    - The map is drawn on a tilemap with a floor per room (14 GameObjects instead of about 270), checked cell for cell against the walls the pathfinder uses.
    - **Fog of war on the map** (producer decision D1 = A): an uncharted room's contents are hidden, crawlers walking in stay visible; a scout's report fades the fog out. (In the KNI client, D1 = C replaces it from 2026-10-21: crawlers are hidden too. See Epic CLIENT.)
    - Camera: pan, zoom toward the cursor, reframe; click a crawler to follow it, click a room to select it in the dashboard.
    - **The header and the Bounty and Merchant windows work over the map** (producer decision D2); Tab still opens the full dashboard.
    - Bounties target rooms by name; the simulation refuses a Scout bounty on a charted room or any bounty on an unknown sector, before escrow.
    - Fixes found on the way: bunks had a dynamic physics body and were shoved around by crawlers; the Bounties and Merchant buttons needed two presses the first time.
  - **Phase 5, Definition of Done: done 2026-10-13** (PlayMode 54/54 in 3 consecutive runs; producer visual check at 1× passed 2026-10-15; plan approved 2026-10-12 with D1 = A, D2 = A; removed, `git show 4e83ecd:.agents/epic_map_phase5_plan.md`):
    - `OutpostLifecycleScenario`: the real outpost for 9,000 ticks, nothing injected. Every crawler closes a harvest → sell cycle; crawlers eat, sleep in bunks and gather at the hearth; both seeded bounties are fulfilled; zero anomalies of every type; the ledger balances; nobody starves.
    - **Trust toward the player is reported, not asserted** (producer decision 2026-10-13): on the default prices it falls for almost every crawler, and every economic decision belongs to §3.3. **Asserted again since 2026-10-15 (§3.3).**
    - **`BunkRestScenario` reports starvation instead of asserting it** (producer decision 2026-10-13): in 3 of 6 close-out runs a broke crawler starved while the Garden regrew. That is food pacing, owned by §3.3. The lifecycle scenario still asserts that nobody starves. **Asserted again since 2026-10-15 (§3.3 food pacing and the starving crawler's return to the Garden).**
    - `DeterminismScenario`: same-seed runs identical at every 100-tick sample to t3,000 (§1A.2 #6).
    - The jitter check measures sim time (D1 = A); jitter is 0 in every scenario. A per-crawler activity tally (`AgentActivity`) feeds the report.
    - Fixes found on the way: a hungry crawler eats the ration it carries; an intent decided before a respawn no longer moves the new agent with the same id.
- **Producer intent (2026-10-11):** the live game shows one connected outpost floor where crawlers physically work, sell, eat, socialize and sleep. **Sector count is unimportant** and is data, not design; the earlier 9- versus 12-sector target is superseded. The play pens leave the live map and become test-only canaries.
- **Producer decisions (2026-10-11):**
  - Revision 2 approved.
  - Drifter arrival and departure through the Road Gate is **deferred** to the §3.3 economy decision; crawlers stay resident.
  - A **"Dev Map" button** switches the running game from the main map to the legacy pen arena (Phase 2).
- **Phases:**
  1. World as data: `MapDefinition`, `WorldBuilder`, `ISectorRegistry`, world bounds independent of the camera.
  2. Session split: pens become the Dev Map, plus the Dev Map toggle and `PenCanaryScenario`.
  3. Populate the outpost floor: rest anchor, 12 crawlers, fog of war, scenario migration, economy soak re-run.
  4. Player view: rendering, shroud, camera, sector-aware UI (D1 = A: crawlers visible in the fog; D2: the HUD works over the map).
  5. DoD: `OutpostLifecycleScenario`.
- **Superseded:** Revision 1's `OutpostMapBuilder` (never wired into the live session) is replaced in Phase 1. See the epic's review (§1).
- **Deferred:** drifter churn (Milestone 2, §3.3 D7), descent gate (Epic 10), workshops (Milestone 3), cognition scaling (§3A #8). Map save/load was delivered with Milestone 1's whole-session save (2026-10-15). Open findings and their owners: [epic §6](epics/epic-map.md).

### Epic CORE: [Engine-Free Core and Headless .NET](epics/epic-core.md)
- **Status**: COMPLETED (2026-10-17). Merged to `main` as PR #5 on 2026-10-17.
- **What:** Phases 0 and 1 of the Unity-to-Godot migration write-up (Claude Docs, 2026-10-15). The simulation runs without an engine: the same source compiles in a Unity assembly that may not reference the engine and for `net10.0`, where a host steps ticks with no frames, and both produce the same world hashes. It pays off on any engine; Phases 2–4 (the Godot spike, view parity, cutover) wait on the spike's go/no-go and are not part of this epic.
- **Producer decisions (2026-10-17):** Phases 0–1 approved as standalone work, under the agent's provisional authority; web builds are not needed; commit to Godot only after the spike; `Microsoft.Extensions.DependencyInjection` and xUnit v3 for .NET; the §2 retirements and the MCP SDK lift above.
- **Chunks** (branch `feat/godot-migration`; the working plan was removed at close-out):
  - Phase 0, Decouple: C1 cognition on fixed ticks (§1A.2 #6, #15); C2 golden world-hash baselines; C3 `EntityId`; C4 engine-free maths; C5 entities as data (§1A.2 #16); C6 infrastructure out of the core; C7 the `Simpiens.Core` assembly (`noEngineReferences`). Gate: same world hash, PlayMode green.
  - Phase 1, Headless .NET: C8 the `dotnet/` solution; C9 xUnit v3 tests and headless DoD scenarios; C10 the console host; C11 CI and close-out. Gate: Unity and .NET hashes match.
- **Progress:**
  - C0 (setup) done 2026-10-17.
  - C1 (cognition on fixed ticks) done 2026-10-17: §1A.2 #6, #15 and #18 resolved; PlayMode 66/66, `DeterminismScenario` identical alone 3 of 3, economy soak within every pace band.
  - C2 (golden world-hash baselines) done 2026-10-17: 3 seeds × 31 samples in `Baselines/golden-world-hash.txt`, reproduced in fresh processes; every later chunk must reproduce them. PlayMode 67/67.
  - C3 (`EntityId` replaces `UnityEngine.GUID`, bit-compatible) done 2026-10-17: golden hashes identical, save format unchanged; PlayMode 68/68.
  - C4 (engine-free maths: `Vec2`, `GridPoint`, `RectF`, `Rgba`, `SimMath`, copying Unity's formulas) done 2026-10-17: golden hashes identical for all 3 seeds; PlayMode 68/68.
  - C5a (entities as data: `WorldEntity`, the engine-free `AgentRuntime`, views that only read; §1A.2 #16 resolved) done 2026-10-17: golden hashes identical, the validators build headless records; PlayMode 68/68.
  - C5b (`SessionHost`: the engine-free session; `SwarmSpawner` only draws it) done 2026-10-17: golden hashes identical for all 3 seeds; PlayMode 68/68.
  - C6 (no UnityEngine, VContainer, UniTask or ZLogger.Unity in engine-free code; validators framework-free) done 2026-10-17: golden hashes identical; PlayMode 68/68.
  - **Phase 0 (Decouple) is complete.**
  - C7 (`Assets/Simpiens.Core/`, asmdef `noEngineReferences`; engine-free validators in `Simpiens.Testing.Core`) done 2026-10-17. Phase 0 gate: PlayMode 68/68 in 3 consecutive runs, economy soak within its bands, golden hashes identical for 3 seeds. The persistence smoke passes too: the Linux player boots and round-trips the outpost (t25, 18,603 bytes, the same as Milestone 1).
  - C8 (`dotnet/`: the core for `netstandard2.1;net10.0`, the engine-free harness, MS.DI hosting and `HeadlessSimulation`) done 2026-10-17: a headless .NET 10 run reproduces Unity's golden hashes for all 3 seeds, 3,000 ticks in about 0.4 s.
  - C9 (xUnit v3: `dotnet/Simpiens.Core.Tests`) done 2026-10-17. Phase 1 gate: `dotnet test` 53/53 in about 4 s: the 44 engine-free validators (zero-allocation ones included), the golden hashes equal to Unity's baseline for all 3 seeds, same-seed determinism, and the outpost's three DoD scenarios headless with nothing injected (lifecycle 9,000 ticks, supply loop, save/load). Unity still green (PlayMode 68/68). Found: Unity's Mono and CoreCLR round some float expressions differently, so the runtimes agree on the golden seeds but drift apart on others (seed 1 from t5); each runtime reproduces itself exactly (epic §6 F1).
  - C10 (`simpiens`, the console host on the Generic Host: `run`, `sweep`, `soak`, `golden`) done 2026-10-17: the economy soak's 9 runs stay in their pace bands headless in about 4 s (Unity: about 26 minutes), and `dotnet test` runs them too (62/62). Found: the outpost's day passes on 16 of 23 seeds; 5 have a starving crawler, 2 a crawler with no cycle; Unity agrees on seed 11 (epic §6 F2, a balance question for the producer).
  - C11 (CI and close-out) done 2026-10-17: `.github/workflows/dotnet.yml` builds and tests the .NET solution on Ubuntu with no Unity (not pushed); docs reconciled. Final check: PlayMode 68/68 (70, 2 explicit skipped); golden check in Unity 3/3 seeds; `dotnet test` 62/62.
  - **Phase 1 (Headless .NET) is complete; Epic CORE is complete.** Definition of Done: the validators pass in Unity and under `dotnet test` (zero-allocation ones included); the outpost's DoD scenarios pass in Unity and headless with nothing injected; the golden hashes are equal in both runtimes. Open findings: epic §6 (F1 Mono and CoreCLR round some float chains differently; F2 the outpost's day fails on 7 of 23 seeds, resolved 2026-10-23 by Epic 10 G0). Next on this path: the engine spike (Phase 2) ran as desk research on 2026-10-17 and 2026-10-18 and chose KNI over Godot; the new client is Epic CLIENT below.


### Epic CLIENT: [KNI Client and Engine Rework](epics/epic-client.md)
- **Status**: PAUSED (2026-10-23): the producer set Android and the other engine work aside for core gameplay ([Epic 10 Part 1](epics/epic-10.md)). **What exists**, merged into `main` as PR #6 on 2026-10-23: M0 and M1; M2.0–M2.7 (parity with Unity's game); the text cleanup, the client seams, M3 (isometric and cutscenes) and the architecture review's findings. **What does not:** the producer's side-by-side and Unity's removal (M2.8, M2.9), M4 (Android, deferred) and M5–M7. The producer's look at M3 and at the header's pause and speed is still open. Epic 10's new screens are built in this client.
- **What:** a new game client on KNI over the engine-free core, replacing Unity's views and dashboard. It draws the simulation in top-down (developers' map designer and debugging), isometric (the main view) and screen space (cutscenes), on Windows and Linux first and on Android and iOS after the MVP. The simulation core doesn't change and never references KNI. Proposal, comparison and producer answers: [`docs/engine-proposal-kni.md`](engine-proposal-kni.md) (live version in Claude Docs).
- **Producer decisions (2026-10-18):**
  - Framework KNI; MonoGame kept compiling in CI as its fallback; Godot dropped.
  - Isometric will most likely replace top-down as the main view; height is visual only; top-down is for design only; cutscenes are still backdrops with dynamic characters and text boxes (Fire Emblem style).
  - The MVP is Windows and Linux desktop. Android and iOS follow it (portrait phones only; tablets not a priority; an Apple developer account and a Mac exist). Consoles last, only if mobile goes well. No macOS, no web.
  - Art starts basic and grows to at most 64×64 px sprites. UI style and art designer to be decided. Target 30 fps.
  - **Unity is retired at M2.** From M0, Unity's views and dashboard are frozen: new view and UI work goes into the client. Track B's UI Toolkit code stays as it is until then.
  - The map designer is for developers only. No sponsorship of KNI for now.
  - Same working rules as Epic CORE. The producer granted the agent provisional authority over M0 and M1 (2026-10-18), with an implementation plan before each.
- **Milestones** (estimates in agent days; proposal §Roadmap):
  - M0 first pixels (1–2): a desktop window where the host ticks the outpost and Floor 1 and its crawlers are drawn top-down. Gate: golden hashes 3 of 3 from inside the client; a render test in Linux CI; the MonoGame build compiles.
  - M1 top-down parity on desktop (3–5): fog, props, labels, animation, camera, mouse picking; a compile-only Android build in CI. Gate: render tests pass; screenshots reviewed beside Unity's.
  - M2 dashboard in Gum (5–8), then Unity retired in its own chunk. M3 isometric (3–5). M4 Android and M5 iOS (3–5 each, after the MVP). M6 screen sizes (1–3). M7 art pipeline (2–3). The developers' map designer follows M2.
- **Progress:**
  - C0 (setup: this entry, the epic doc, the proposal copy, the M0 plan) done 2026-10-18.
  - M0.1 (the client's projects: `Simpiens.Presentation`, `Simpiens.Client`, `Simpiens.Desktop`, `Simpiens.Client.Tests`; the `XnaFramework` switch, KNI or MonoGame compile-only) done 2026-10-18.
  - M0.2 (the engine-free presentation: the generated art ported pixel for pixel from Unity, Floor 1 as tiles, the stable draw order, the top-down camera, PNG files) done 2026-10-18.
  - M0.3 (the client ticks the simulation) done 2026-10-18: the game, run frame by frame offscreen, reproduces the golden world hashes for all 3 seeds; uneven frame pacing gives the same world.
  - M0.4 (first pixels: Floor 1 and its crawlers drawn top-down; render tests against committed reference images; the desktop exe runs the live outpost) done 2026-10-18.
  - M0.5 (CI: the client's tests under Xvfb with Mesa, and the MonoGame compile; AGENTS.md) done 2026-10-18. The workflow is unverified until the branch is pushed.
  - **M0 (first pixels) is complete:** golden hashes 3 of 3 from inside the client, render tests passing on Mesa's software renderer (locally; CI's first run comes with a push), the MonoGame build compiling. Measured: `dotnet test` 83/83 (client 21, core 62). Next: M1, top-down parity, with its own plan.
  - M1 plan written 2026-10-18: props and crawler appearance, text and map labels, fog of war, camera, picking and selection, a compile-only Android build, then the gate and close-out. It was deleted at M1's close-out; its decisions are in `.agents/architecture_history.md`.
  - M1.1 (props, and crawler appearance as Unity's `AgentView` drew it), M1.2 (a pixel font and the room names), M1.3 (fog of war, as `ShroudView`), M1.4 (the camera and its controls, as `CameraRig` and `CameraController`) and M1.5 (picking and selection, as `WorldPicker` and `WorldSelection`) done 2026-10-18.
  - M1.6 (a compile-only Android build, `dotnet/Simpiens.Android`, portrait; built locally with a user-local toolchain and in a CI job) done 2026-10-18.
  - M1.7 (the gate and close-out) done 2026-10-18:
    - The review set renders the outpost at t0, t300 and t1500, a zoomed and panned view, and a selection. The agent looked at each against the ported Unity logic.
    - A full frame (one tick, 1,925 draw items, 800×600) takes 1.2 ms on Mesa's software renderer and 0.7 ms on a desktop GPU, against the 33 ms budget.
    - The desktop exe logs through `SimLog`.
  - **M1 (top-down parity on desktop) is complete:** render tests pass for fog, props, labels, animation, the camera and selection; the Android client compiles. Measured: `dotnet test` 111/111 (client 49, core 62); golden check 3/3; the MonoGame and Android builds compile.
    - Still open: the producer's side-by-side look beside Unity, which is person-gated, and CI's first run, which comes with a push.
    - Next: M2 (the dashboard in Gum, then Unity retired), when the producer starts it.
  - M2 plan written 2026-10-18 (`.agents/client_m2_plan.md`), on the same branch, because parity comes before the pull request (producer). It covers Gum in the client, an engine-free dashboard model, the calm command as a typed command, then each panel: the header, the merchant, the bounty board, the Tab dashboard, saving and the session switch. Then the producer's side-by-side, Unity-only coverage moved to .NET, and Unity removed. Execution waits for the producer's go-ahead.
  - **Producer decisions (2026-10-18, M2):** go ahead with M2. Two pull requests: the first at parity (after the side-by-side sign-off following M2.7), the second removing Unity (M2.8 and M2.9). The plan's defaults are approved: Gum's built-in visuals in Unity's palette, Unity's layout first, and, once Unity is gone, a Definition of Done scenario that lives in `dotnet/` (headless or through the client) on the real session, with nothing injected.
  - M2.0 (Gum 2026.10.1.1 in the client, in Unity's palette; scripted pointer for tests), M2.1 (the dashboard as data, engine-free), M2.2 (the dossier's calm as a typed command in the core; checked in Unity too: PlayMode 69/69, golden 3/3), M2.3 (the header over the map), M2.4 (the merchant terminal), M2.5 (the bounty board), M2.6 (the Tab dashboard) and M2.7 (quick save and load, the Dev Map switch in development builds, the camera's follow) done 2026-10-18.
  - **Parity with Unity's game (M2.0–M2.7):** every panel works in the client.
    - Ported to the client, through the UI: Unity's view and UI scenarios (MapHud, PlayerScout, QuickSaveButtons, SessionSwitch, ShroudView's camera part) and UI validators (FacilityZoneService, BountyTerminal).
    - `PlayerScoutScenario` passes on the real outpost with nothing injected: charted at tick 291, bound 520.
    - Measured: `dotnet test` 147/147 (client 84, core 63); golden check 3/3; the MonoGame, Debug desktop and Android builds compile.
    - Frame time with the panels refreshed every frame: about 4 ms against the 33 ms budget.
    - Next: the producer's side-by-side with Unity, then the first pull request; then M2.8 and M2.9.
  - **Producer decision (2026-10-18):** the branch `main-kni`, cut at `681d3b5`, acts as the main branch until M4 proves the Android build. Before M3, a cleanup pass begins with the text.
  - Text cleanup plan written 2026-10-18 (deleted at its close-out; its decisions are in `.agents/architecture_history.md`), on branch `feat/client-text` into `main-kni`:
    - **Causes:** the UI's text is a scaled 18 px bitmap, so Gum stops snapping glyphs to pixels. The map's 5×7 pixel font breaks off the whole-pixel scale and has no descenders.
    - **Fix:** fonts are baked offline (KernSmith and FreeType, Gum's own cache format) at exact sizes and drawn 1:1. Inter is the default typeface. Map text moves to screen space.
    - Chunks T1–T4.
  - **Text cleanup done (2026-10-18):** T1 (the baker and the committed atlases, with a font golden), T2 (the UI on baked Inter, 1:1) and T3 (the map's text in screen space).
    - Every text in the client is one of 15 baked Inter faces, drawn 1:1 on whole pixels. The UI's scaled bitmap and the map's pixel font are gone.
    - Measured: `dotnet test` 173/173 (client 110, core 63); golden 3/3; the MonoGame, Debug desktop and Android builds compile; frame time 1.4–3.6 ms.
  - **Producer request (2026-10-20):** before M3, carry out the three high findings of the client's architecture review (Claude Docs, "KNI Client Architecture Review"): a projection seam (A1), a draw order the projection chooses (A2) and a per-tick view frame (A3). The plan is the Claude Doc "Plan: Projection, Draw Order and Frame Snapshot (A1–A3)"; its working copy was deleted at close-out. Branch `feat/client-seams`, into `main-kni`.
  - **Producer decision D1 = C (2026-10-21), replacing D1 = A in the client, in both projections:** the fog hides everything in an uncharted sector, crawlers included. A scout vanishes into the fog until the room is charted. Unity's frozen views keep A.
  - **Client seams done (2026-10-21):**
    - A1: `IViewProjection` with `TopDownProjection` (and `IsometricProjection`, tested but not wired); `View` for the renderer, labels and controls; the camera rig and picking through the projection; clicks pick what is drawn.
    - A2: draw roles, layers chosen by the projection, a role tie-break in the sort key; the fog rule D1 = C in the scene builder.
    - A3: `WorldViewFrame`, captured on `SessionHost.TickCompleted` (the one core change), the only thing the views read; crawlers and the camera's follow drawn between ticks.
    - Measured: `dotnet test` 199/199 (client 136, core 63); golden 3/3; Unity PlayMode 69/69 and golden 3/3 on the core change; the MonoGame build compiles. Frame time 2.14 ms against 2.09 ms before (llvmpipe, a tick every frame).
    - Open: the producer's look at crawlers moving between ticks at 1× and 10×, and the pull request into `main-kni`. Next: M3, isometric.
    - The producer's look is open (person-gated); the pull request goes into `main-kni`.
  - M3 plan written 2026-10-21 (deleted at its close-out; its decisions are in `.agents/architecture_history.md`), on branch `feat/client-m3` into `main-kni`:
    - Phase 0: the review items deferred into M3 (one world epoch, the scene built in `Update`, `SimpiensGame` split into a session, a world view and screens, and a frame path that allocates nothing), with no visual change.
    - Phase 1: isometric drawn from generated placeholders, with art sets per view mode, the fog as floor quads, a wall style for walls in the way, and the view toggle.
    - Phase 2: the gate (depth order and picking on the real outpost, labels, references, frame time).
    - Phase 3: cutscenes in two kinds on one script model (hard cutscenes in screen space; dynamic cutscenes over the running world), with Gum text boxes and a demonstration of each.
    - About 7–10 agent days. Execution waits for the producer's go-ahead.
  - **Producer decisions (2026-10-21, M3):**
    - Accepted as proposed:
      - P1: placeholders at 32×16;
      - P2: cutaway walls, with the three styles shown at the look;
      - P4: isometric by default, with V switching to top-down in every build;
      - P6: three pull requests into `main-kni`.
    - **P3: two kinds of cutscene.** Hard, traditional cutscenes pause the game. Dynamic cutscenes are part of the gameplay itself.
    - **P5: no rotation.** One fixed view.
    - **P7: a dynamic scene starts when a crawler enters a zone or starts an interaction with another crawler.** The scene never moves crawlers.
    - **P8: the player keeps control during a dynamic scene.** They can click away to watch another crawler, or do other gameplay.
    - Go-ahead given 2026-10-21: execution started on `feat/client-m3`.
  - **M3 Phase 0 done (2026-10-21), the first of three pull requests:**
    - one world epoch for every view cache (review A5);
    - view state advanced in `Update`, drawing pure (A6);
    - the game split into `ClientSession`, `WorldView` and a stack of screens (A4);
    - a world frame that allocates nothing and culls what is off screen (R1–R3).

    Nothing on screen changed: the picture pins and render references hold. The pins now count only what lands on screen. Measured: `dotnet test` 211/211 (client 148, core 63); golden 3/3; the MonoGame, Debug desktop and Android builds compile; frame time 2.27 ms (llvmpipe) and 1.59 ms (GPU).
  - **M3 Phases 1–2 done (2026-10-21), the second pull request: isometric is the game's view.**
    - Art sets per view mode, generated isometric placeholders on 32×16 tiles, things standing on their feet.
    - The fog lies on the floor under the view's transform.
    - The cutaway for walls in the way (C cycles full and low).
    - `--view` and V switch to top-down; framing is crisp first.
    - **The gate:** on the real outpost, wherever a crawler or prop overlaps a wall on screen the nearer is on top. A crawler walking past a wall goes behind it exactly where its depth crosses. A click on a crawler's head picks it, and a crawler hidden behind a wall can still be clicked. Room names never overlap.
    - Nine isometric references and two pins, each looked at.
    - Measured: `dotnet test` 258/258 (client 195, core 63); golden 3/3; the MonoGame, Debug desktop and Android builds compile; frame time 1.59 ms (isometric) and 2.33 ms (top-down) on llvmpipe.
    - Open: the producer's look at the isometric view and the wall styles. Next: Phase 3, cutscenes.
  - **M3 Phase 3 done (2026-10-21), the third pull request: cutscenes in two kinds.**
    - One script model written in code, and one player: lines appear letter by letter, an advance finishes a line first, Esc skips.
    - **Hard cutscenes** cover the game and hold the world. They are drawn in screen space, with a backdrop, characters in slots and a Gum text box.
    - **Dynamic cutscenes** play over the running world:
      - they start when a crawler enters a zone or starts an interaction with another (from the chronicle), checked once per tick;
      - lines appear in a bubble over the speaker, or in a box at the bottom while it is hidden or off screen;
      - the camera is directed until the player takes it back;
      - one plays at a time, and they never move crawlers.
    - **Demonstrations** in Debug builds: F9 for the hard one; the first gossip and the first crawler into the deep seam for the dynamic ones.
    - The golden hashes hold with dynamic scenes playing. In real play with nothing injected, both dynamic demonstrations start within 1,000 ticks.
    - A test-harness defect is fixed: every offscreen game stayed alive (about 81 MB each) and the client suite ran out of memory. The tests now reuse windows through `SimpiensGame.Restart`; the suite peaks at about 750 MB.
  - **M3 done (2026-10-21)** on `feat/client-m3`, in three pull requests' worth: Phase 0 (`feat/client-m3-phase0`), Phases 1–2 (`feat/client-m3-isometric`), Phase 3 (the branch itself).
    - Measured: `dotnet test` 297/297 (client 234, core 63); golden 3/3; the MonoGame, Debug desktop and Android builds compile; frame time 1.62 ms in isometric and 2.38 ms in top-down on llvmpipe, 1.40 and 1.64 ms on the GPU.
    - **Open, person-gated:** the producer's look at the isometric view, the wall styles, the V toggle and the camera, and both kinds of cutscene; the pull requests into `main-kni`.
    - Next: M2.8–M2.9 (Unity's removal, after the side-by-side) or M4, as the producer decides.
  - **Producer decision (2026-10-22):** M4 (Android) is deferred until later. Its plan is the Claude Doc "Plan: M4 Android (Epic CLIENT)". `main-kni` stays the acting main meanwhile.
  - **Architecture improvements (producer request, 2026-10-22):** the open findings of the client's architecture review (Claude Docs, "KNI Client Architecture Review"), planned in a working plan (deleted at its close-out; its decisions are in `.agents/architecture_history.md`), on branch `feat/client-architecture` into `main-kni`. In order:
    - measurement and guards (D1, T3, T4);
    - host commands with speed and pause (D3, D2);
    - one input frame (U1);
    - the dashboard's loose ends (U3, U4, U5);
    - smaller items, and the client tests in a pinned container (T2).

    U2 and R6 stay with M4; R4, R5 and R7 with M7.
  - **Architecture improvements done (2026-10-22)** on `feat/client-architecture`, 16 commits from `9b8c456` to `ff3d81f`, not pushed:
    - **Measure and guard:** frame timings by phase (F3 in Debug), a frame-time gate, a test that views write nothing.
    - **Host commands:** speed, pause, save, load and the session switch are typed commands applied before the ticks; pause and speed controls in the header (P, 1–4); a stalled frame simulates at most 400 ms.
    - **One input frame:** keys map to actions through a bindings table, and Gum's cursor is fed from the game's frame. The tests no longer reflect into Gum.
    - **The dashboard:** every colour is a `UiStyle` token, the game's services are required, display names replace type names, crawlers are counted once, affinities are sorted.
    - **Smaller items:** `HeadlessSimulation` became `LocalSimulation`.
    - **The pinned image:** the client's tests run in a pinned Ubuntu image, locally (podman) and in CI (docker), where renders must match the references exactly.
    - Measured: `dotnet test` 324/324 (client 261, core 63); the client's 261/261 exactly in the pinned image; golden 3/3; the MonoGame and Android builds compile; frame time 1.5 ms (isometric) and 2.4 ms (top-down) on llvmpipe.
    - Open: the producer's look at the header's pause and speed, and the dashboard's new wording. CI's container job passed on its first run (pull request #6, 2026-10-23): the client's 261/261 with exact renders, after `0537155` made its script executable.
  - **Producer decision (2026-10-23):** the client goes into `main` before M4, which stays deferred. One pull request from `feat/client-architecture` carries everything on `main-kni` (M2.0–M2.7, the text cleanup, the seams, M3) and the architecture work. CI runs on pushes to `main` and on pull requests; `main-kni` retires once the pull request is merged.
  - **Merged (2026-10-23):** PR #6, after CI passed both jobs. **Producer decision (2026-10-23):** Epic CLIENT pauses; core gameplay comes next ([Epic 10 Part 1](epics/epic-10.md)).

---

## Strategic Operating Rhythm

When working in parallel agent sessions:
1. **Engine Work**: Reference **Track A** (Milestone 1 is `COMPLETED`, pending the producer's save/load check; **Epic 10 Part 1 is `ACTIVE`** (Milestone 2, [epics/epic-10.md](epics/epic-10.md)), G0 (the day on every seed, Epic CORE's F2), G1 (drifters) and G2 (health and hazards) done 2026-10-23; the producer looks at G2 before G3 (gear); **Epic CORE** is `COMPLETED` and merged). All code lives in `Simpiens.Cognition`, `Simpiens.Simulation`, and headless validators.
2. **UI Work**: Reference **Track B** (Epic UI-2 `COMPLETED`; no UI epic is `ACTIVE`; Epic UI-3 is folded into Epic 10 Part 1). All code lives in `Simpiens.UI`, UI Toolkit UXML/USS assets, and presenters. **Frozen from Epic CLIENT M0 (2026-10-18):** new views and UI go into the KNI client (`dotnet/Simpiens.Client`), which rebuilds the dashboard in Gum at M2.
3. **Testing Work**: Reference **Track C** (Epic Test-2 `COMPLETED` 2026-10-15; no Track C epic is `ACTIVE`). All code lives in `Simpiens.Testing` and `Assets/Tests/`.
4. **World & Environment Work**: Reference **Independent Epics** (**Epic MAP** is `COMPLETED`; **Epic CORE** is `COMPLETED`: the simulation is engine-free in `Assets/Simpiens.Core/` and builds for .NET in `dotnet/`, see AGENTS.md; **Epic CLIENT** is `PAUSED`: the KNI client in `dotnet/`, merged into `main`). Code lives in `Simpiens.Simulation.World` (map definition, sectors), `Core/Session` (sessions, layouts) and views in `Simpiens.Entities` / `Simpiens.UI`.
5. **Every session**: follow §0 (document hierarchy and update order), §1A (invariants) and §1B (Definition of Done).

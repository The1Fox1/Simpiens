# Simpiens Master Project Roadmap (Multi-Track Architecture)

This document is the **highest source of truth** for project status, scope, sequencing, engineering invariants and the Definition of Done across the Simpiens simulation framework. *Last reconciled: 2026-10-11.*

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
        MAP["Epic MAP\nUtilitarian Outpost Map\n& Lifecycle Topology\n(PAUSED)"]
    end

    subgraph Track A: Core Simulation & Cognition (Authoritative Engine)
        A1["Epic 1-6\nFoundations & GOAP"] --> A2["Epic Alpha\nNeeds & Watchdog"]
        A2 --> A3["Epic 7\nSocial Affinity"]
        A3 --> A4["Epic Beta\nInterdependence"]
        A4 --> A5["Epic 8\nGroups & Directives\n(COMPLETED)"]
        A5 --> A6["Epic 9\nOutpost Economy\n& Public Bounties\n(MECHANISM COMPLETE)"]
        A6 --> A7["Epic 10\nHazards & Descent\n(QUEUED)"]
        A7 --> A8["Epic 11\nMCP Narrative\n(QUEUED)"]
    end

    subgraph Track B: UI / UX & Player Interface (Dumb Views)
        B1["Epic UI-1\nFacility Dashboard\n(COMPLETED)"] --> BAlpha["Epic UI-Alpha\nNarrative Chronicle\n(COMPLETED)"]
        BAlpha --> B2["Epic UI-2\nMerchant Terminal\n& Bounty Board\n(COMPLETED)"]
        B2 --> B3["Epic UI-3\nGuild Roster &\nExpeditions\n(QUEUED)"]
    end

    subgraph Track C: Harness & Diagnostics (Verification Track)
        C1["Epic Test-1\nVisual Anomaly Detection\n& PlayMode Scenarios\n(COMPLETED)"] --> C2["Epic Test-2\nEconomy & Bounty\nStress Harness\n(ACTIVE)"]
    end

    MAP ==>|Substrate for Spatial Quests & Economy| A6
    MAP ==>|Real Sector Bounds & Shrouds| B2
    MAP ==>|Continuous Layout for Scenario Tests| C1
    A5 -.->|Provides Group & Social Primitives| B2
    A6 <==>|Production Phase 1 Convergence| B2
    A6 -.->|Validation Target| C2
    BAlpha -.->|Prepares Narrative Contracts for LLM| A8
```

### Track Rules
1. **Track A (Core Simulation)** maintains the authoritative, original numerical sequence (`Epic 1` through `Epic 11`). Its engine logic runs off-thread in `UniTask` (read-only against immutable snapshots) or is ticked via `SimulationManager`. Zero MonoBehaviour logic. All Track A work is bound by the invariants in §1A, including multiplayer-readiness.
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

### 1A.2 Known Deviations (as of 2026-10-09)

These violate §1A.1 or the project architecture rules today. They are recorded so that new code does not copy them. They are not scheduled work by themselves; scheduling is a producer decision.

| # | Deviation | Violates | Where |
| :-: | :--- | :--- | :--- |
| 1 | ~~`SimulationManager.Tick()` runs once per rendered frame, and walking, harvest/share/idle durations, need decay and watchdog timers use `Time.deltaTime`, while bounty/memory/gossip/hearth timers use logical ticks.~~ **RESOLVED (2026-10-08):** Fixed-tick architecture implemented; simulation state advances exclusively on `ISimulationClock.OnSimulationTick` at fixed 10 Hz (`dt = 0.1s`), with visual interpolation in views. | 1 | [`SimulationManager.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/SimulationManager.cs), [`AutonomousAgent.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AutonomousAgent.cs), [`SimulationClock.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/SimulationClock.cs) |
| 2 | ~~`AutonomousAgent` is a `MonoBehaviour` that owns needs, inventory, memory, group membership and the active plan, and applies action consequences. Pawn position is `transform.position`.~~ **RESOLVED (2026-10-08):** Pure C# `AgentState` manages needs, inventory, memory, goals, and tick logic headlessly in `IAgentRegistry`. Intent consequences are applied authoritatively by `IAgentConsequenceService`, async cognition is coordinated by `IAgentCognitionService`, and `AutonomousAgent` acts as a thin view/façade. | 5, "No Logic in MonoBehaviours" | [`AgentState.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AgentState.cs), [`AgentRegistry.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/AgentRegistry.cs), [`AutonomousAgent.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AutonomousAgent.cs) |
| 3 | ~~Background planners read live `AgentMemory` / `AgentInventory` (non-thread-safe dictionaries) while the main thread mutates them (gossip, trade affinity). The snapshot is `Retain()`ed only after `SwitchToThreadPool()`.~~ **RESOLVED (2026-10-09):** Single-Writer Threading Architecture enforced. `SharedWorldSnapshot.Retain()` invoked on main thread before thread pool dispatch; `IAgentMemory` and `IAgentInventory` provide preallocated double-buffered zero-allocation snapshots (`AgentMemorySnapshot`, `AgentInventorySnapshot`) for off-thread cognition; `GetAffinity` refactored to be pure side-effect-free calculation (no dictionary writes on read); and panic memory pruning moved authoritatively to main thread in `AgentConsequenceService`. | 6 | [`AgentMemory.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Memory/AgentMemory.cs), [`AgentCognitionService.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AgentCognitionService.cs), [`AgentInventory.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Inventory/AgentInventory.cs) |
| 4 | ~~`InfluenceEvent` carries a string command name and a comma-separated string payload parsed with `Split`.~~ **RESOLVED (2026-10-09):** `InfluenceEvent` converted to a typed zero-allocation command union (`PostBountyCommand`, `CancelBountyCommand`, `UpdateStallPricesCommand`) with static factory methods; UI presenters dispatch typed commands directly; legacy string constructor preserved for backward compatibility. | 2 | [`InfluenceEvent.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Intervention/InfluenceEvent.cs), [`SimulationManager.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/SimulationManager.cs) |
| 5 | ~~Unseeded RNG in simulation paths (`[ThreadStatic] System.Random` in evaluators).~~ **RESOLVED (2026-10-10, closure Phase 2):** `SimRandom` gives stateless, seeded streams keyed by (seed, entity, tick, salt). The seed comes from `SimulationSettings` (`SIMPIENS_SEED` / `-simseed`), and `EntityIdAllocator` makes entity ids reproducible per seed. Remaining `UnityEngine.Random` calls are view-only (`NodeController` bounce direction, `NodeFactory` sprite colour). `OutpostMapBuilder` (not live) and `NodeFactory` still call `GUID.Generate()`. | 4 | [`SimRandom.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/SimRandom.cs), [`EntityIdAllocator.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/EntityIdAllocator.cs) |
| 6 | Completion order of background cognition determines the order intents are enqueued. **PARTIALLY RESOLVED (2026-10-10):** intents arriving in the same tick are applied in agent-id order. **Still open:** *which tick* a result arrives on depends on thread timing; a fixed-latency or barrier scheme belongs to the multiplayer work. | 3 | [`SimulationManager.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/SimulationManager.cs) |
| 7 | ~~`AsyncPathfinder` is a straight-line stress-test placeholder, not Grid A*.~~ **RESOLVED (2026-10-09):** Upgraded `AsyncPathfinder` into a zero-allocation 8-directional Grid A* engine with preallocated binary min-heap, $O(1)$ epoch visited tracking, diagonal corner-cutting prevention, raycast string-pulling path smoothing, and concurrent searcher worker pooling. Separated static map obstacles (`StaticNavGrid`) from per-frame dynamic entity snapshots. | — (Epic 9 carry-over) | [`AsyncPathfinder.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Pathfinding/AsyncPathfinder.cs), [`GridAStarSearcher.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Pathfinding/GridAStarSearcher.cs), [`StaticNavGrid.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/Spatial/StaticNavGrid.cs) |
| 8 | `GoapPlanner` is a DI singleton guarded by a `lock`, so all agents' planning is serialized. | Dual-loop scaling intent | [`GoapPlanner.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/GoapPlanner.cs) |
| 9 | ~~The production GOAP domain (action graph, goals) and session population live under `Testing/` in `SwarmSpawner`; validators are registered as boot-time `IStartable`s and ship in the single `Simpiens.asmdef`.~~ **RESOLVED (2026-10-11, closure Phase 5):** domain in `Cognition/Planning/Domain/OutpostGoapDomain`, session in `Core/Session/SwarmSpawner`. Test code is in a separate `Simpiens.Testing` assembly (`UNITY_INCLUDE_TESTS` only), wired in through `BootstrapExtensions`. Validators run as PlayMode tests, not at boot. | Test isolation | [`OutpostGoapDomain.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/Domain/OutpostGoapDomain.cs), [`AppBootstrapper.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/AppBootstrapper.cs) |
| 10 | ~~`CognitiveEngine`, `IContextBuilder` and `IToolExecutionRegistry` are registered/declared but unused; the real cognition loop is `AutonomousAgent.EvaluateCognitionAsync`.~~ **RESOLVED (2026-10-09; trade gold routed through the ledger 2026-10-11):** `IToolExecutionRegistry` implemented via `ToolExecutionRegistry` mapping cognitive tool execution to typed intents, backed by `IIntentHandlerRegistry` with 8 modular intent handlers (`WanderIntentHandler`, `PanicIntentHandler`, `HarvestResourceIntentHandler`, etc.). `SimulationManager` implements `IIntentExecutionContext` and delegates execution to modular handlers. Authoritative `IEconomyLedger` introduced to govern all gold flows and fix escrow minting leaks. The `CognitiveEngine`/`ContextValidator` stub was deleted 2026-10-11; `IContextBuilder` is kept as the Epic 11 seam. | — | [`ToolExecutionRegistry.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/ToolExecutionRegistry.cs), [`IntentHandlerRegistry.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/Handlers/IntentHandlerRegistry.cs), [`SimulationManager.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/SimulationManager.cs), [`EconomyLedger.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/Economy/EconomyLedger.cs) |
| 11 | ~~Tier-1 `SurvivalUtilityEvaluator` returned a non-null intent (Idle/Wander/Harvest) on every path, so Tier-2 GOAP never ran in the live session; every GOAP goal from Epics 6–9 only worked under validator injection.~~ **RESOLVED in code (2026-10-09, review §3.1):** the evaluator is now a pure hunger reflex (Hunger ≥ 60 + reachable known food, else `null`); idle/wander moved to the Tier-3 `AmbientWanderEvaluator` fallback; `HarvestResourceAction`, `TravelToResourceAction` and `SearchResourceAction` emit real intents. Regression covered by `SupplyLoopValidator` Test 7. Live verification pending the first `OutpostSupplyLoopScenario` run. | §1B (status accuracy) | [`SurvivalUtilityEvaluator.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Evaluators/SurvivalUtilityEvaluator.cs), [`AgentCognitionService.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AgentCognitionService.cs) |
| 12 | ~~No live population could run the Milestone 1 loop: all session agents were scripted into walled demo pens, the trade stall sat inside Pen 3, and the seeded Scout bounty targeted walled Pen 7.~~ **RESOLVED in code (2026-10-09, review §3.1):** the open yard north of the pens is now the *Outpost Commons* (stall, Ore Ridge, Herb Hollow, food bushes) with 6 free crawler agents (`SpawnCount` 24 → 30). | §1B | [`SwarmSpawner.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Testing/Environment/SwarmSpawner.cs) |
| 13 | ~~Seeded bounties bypassed escrow (`RegisterBounty` locked nothing), so delivery minted gold via a direct `AddGold`.~~ **RESOLVED in code (2026-10-09, review §3.1):** `RegisterBounty` routes through the same escrow lock as `PostBountyCommand` and rejects un-escrowable bounties; delivery pays only by releasing escrow. Session gold enters through a single `InitialSeed` ledger faucet. | Ledger conservation | [`SimulationManager.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/SimulationManager.cs), [`DeliverBountyIntentHandler.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/Handlers/DeliverBountyIntentHandler.cs) |
| 14 | ~~`SimulationManager` executes active intents by enumerating the `_activeIntents` `Dictionary` (and stalls, bounties and anchors the same way).~~ **RESOLVED (2026-10-10):** active intents run in agent-id order (sorted id list), the stall/bounty/expiry/anchor buffers are sorted, and harvest contention picks the lowest-id competitor. Mutation-checked by `DeterminismValidator` Test 3. Unaudited: `foreach` over agent memory maps in GOAP actions (e.g. `TravelToAnchorAction` nearest-anchor ties). | 3 | [`SimulationManager.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/SimulationManager.cs) |
| 15 | The background cognition task writes per-agent state off the main thread: plan state (`AbortActivePlan`, `ActivePlan.CopyFrom`, `ActiveGoal`), `CurrentIntent`/`HasActiveIntent` and `VisualState`. In practice this is serialized per agent by the `IsEvaluating`/`HasActiveIntent` guards, but it is not single-writer. Narrows #3's resolution: memory and inventory are snapshot-isolated; plan and intent bookkeeping are not. (Recorded 2026-10-10.) | 6 | [`AgentCognitionService.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AgentCognitionService.cs) |

### 1A.3 Logging (Producer decision, 2026-10-09)

All application logging goes through `Simpiens.Diagnostics.SimLog` (ZLogger, §2.1). Binding for new code:

1. **No `UnityEngine.Debug.Log*` in application code.** Validators under `Testing/` may keep `Debug.Log` for test output.
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
2. **Live emergence demonstrated.** At least one PlayMode scenario (Track C harness: `Assets/Tests/PlayMode/`) shows the feature occurring *naturally* in the real session setup. It must **not** inject the outcome's preconditions. It asserts a concrete outcome within a bounded number of ticks (e.g. *"within N ticks, at least one posted Gather bounty is claimed, fulfilled and paid out"*).
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
| **`MemoryPack`** (`com.cysharp.memorypack`) | **Zero-Alloc Binary Serialization** *(NEW APPROVED)* | High-performance C# source-generated binary serializer. Native memory-copy (`Unsafe.CopyBlock`) for unmanaged blittable structs (`ItemInstance`, `BountyContract`, `GroupData`, `WorldState`, `ChronicleEvent`). Powers state persistence, save/load, tick replays, and the Milestone 4 floor descent relocation loop. |
| **`ZLogger`** (`ZLogger` 2.5.10 via NuGet DLLs + `com.cysharp.zlogger` ZLogger.Unity) | **Zero-Alloc Structured Logging** *(NEW APPROVED, 2026-10-09)* | Microsoft.Extensions.Logging levels with interpolated-string handlers that evaluate and allocate nothing when a level is disabled. Wrapped by `Simpiens.Diagnostics.SimLog` (see §1A.3). The NuGet closure is vendored in `Assets/Plugins/ZLogger/` by `scripts/update-zlogger.sh` (facades Unity already provides are excluded). `Assets/Scripts/csc.rsp` raises the `Simpiens` assembly to C# 10. Keep the ZLogger.Unity git tag in `Packages/manifest.json` in sync with the vendored version. |
| **`Unity.Collections` + Burst** (`com.unity.collections`, `com.unity.burst`) | **Main-Thread Spatial Broadphase** *(NEW APPROVED - HYBRID)* | Unmanaged off-heap containers (`NativeArray`, `NativeParallelMultiHashMap`) and SIMD-accelerated Burst jobs. Applied strictly on the main thread for physics broadphase, campfire proximity, and sector bounds in `SimulationManager.Tick()`. *(Note: Off-thread cognitive workers retain pure C# flat struct arrays in `SharedWorldSnapshot` to prevent `AtomicSafetyHandle` thread-pool exceptions).* |

> **Install status (verified against `Packages/manifest.json`, 2026-10-09):** `VContainer`, `UniTask` and `ZLogger` are installed. `MemoryPack` and `Unity.Collections` / Burst are **approved but not installed**; no code uses them yet.

### 2.2 Rejected 3rd-Party Frameworks & Handroll Policies

1. **2D Pathfinding & Navigation Engine**:
   - **Rejected**: *A* Pathfinding Project (Granberg)* and *Unity AI Navigation*. Both heavily couple to `MonoBehaviour` components (`Seeker`, `AIPath`, `NavMeshAgent`) or main-thread NavMesh carving, which breaks headless domain testing and stalls high-speed ticks (5x–10x).
   - **Handroll Policy**: Upgrade [`AsyncPathfinder.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Pathfinding/AsyncPathfinder.cs) into a zero-allocation 2D Grid A* / Jump Point Search (JPS) engine by porting the array-backed binary min-heap and struct node pool architecture already proven in [`GoapPlanner.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/GoapPlanner.cs).
2. **Model Context Protocol (MCP) Integration**:
   - **Rejected**: *Official .NET MCP C# SDK (`modelcontextprotocol/csharp-sdk`)*. Relies on ASP.NET Core Hosting and modern .NET runtime assemblies that collide with Unity's Mono/IL2CPP `netstandard2.1` target.
   - **Handroll Policy**: Implement a lean, pure-C# UniTask JSON-RPC 2.0 / SSE client (~250 LOC) under [`ILlmNarrativeEnricher`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Narrative/ILlmNarrativeEnricher.cs) with an automatic circuit breaker falling back to [`TemplateNarrativeFormatter`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Narrative/TemplateNarrativeFormatter.cs).
3. **UI Toolkit Reactive Data-Binding**:
   - **Policy**: Reserve Unity 6 built-in runtime data binding (`INotifyPropertyChanged`) exclusively for low-frequency modal dialogs and player input forms. For high-frequency dynamic simulation feeds (logical ticks run at 10 Hz at 1x and up to 100 Hz at 10x clock speed), standardize on throttled (10 Hz) element pooling via a reusable `VisualElementPool<TData, TElement>` utility modeled after [`FacilityDashboardPresenter.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/UI/FacilityDashboardPresenter.cs).

---

## 3. Production Milestones Overview

To bridge low-level technical epics with high-level game design goals, development is staged into **Production Milestones**:

| Milestone | Target Scope | Aligned Epics | Target Release State |
| :--- | :--- | :--- | :--- |
| **Milestone 1** | **The Outpost Loop (MVP)** | Track A: Epic 9<br>Track B: Epic UI-2<br>Track C: Epic Test-2 | Single-floor outpost with **12 interconnected sectors** (target design; Epic MAP currently laid out an initial 9 sectors as incomplete WIP before being paused). Player manages a roadside trading post: sets buy/sell prices for salvaged materials and provisions, posts public bounties (Scout, Gather), and builds dyadic trust with visiting crawlers. Integrates MemoryPack serialization for unmanaged inventory/contracts and zero-alloc Grid A* navigation (Grid A* delivered 2026-10-09, MemoryPack not delivered yet). |
| **Milestone 2** | **Guild Roster & Expeditions** | Track A: Epic 10 (Part 1)<br>Track B: Epic UI-3 | High-affinity adventurers convert into the player's permanent Guild. Player organizes 4-man parties and issues expedition directives to high-hazard sectors. `Unity.Collections` + Burst broadphase for multi-agent party coordination. |
| **Milestone 3** | **Facility Workshops & Crafting** | Track A: Epic 10 (Part 2)<br>Track B: Epic UI-4 | Fixed outpost expansion stations (Forge, Alchemy Still). Hired non-combat artisans refine raw salvage into weapons, armor, and potions. |
| **Milestone 4** | **Gatekeeper Descent & Floor Relocation** | Track A: Epic 10 (Part 3) & Epic 11<br>Track B: Epic UI-5 | Boss-guarded descent gate breach, roguelike forward operating base relocation loop (via MemoryPack state snapshotting), and LLM-powered MCP tavern chronicles. |

### Milestone 1 Status (verified against the code, 2026-10-09): NOT YET CLOSED

Engine mechanisms (Epic 9) and the player UI (Epic UI-2) are delivered and validator-tested. **Update 2026-10-11:** §3.2 (personality) has landed. Epic 9 promotion now waits only on the deferred §3.3 economy model (producer decision). The **live loop** — *player posts a bounty → agents harvest salvage → agents sell at the stall → trust toward the player grows* — has **not** yet been demonstrated in the live simulation (§1B). The supply side (review §3.1) was implemented on 2026-10-09. Its Definition-of-Done scenario (`OutpostSupplyLoopScenario`) **passes** (2026-10-10, closure plan Phase 1): no injection, zero anomalies. Latest PlayMode runs (2026-10-10): **14/14 pass, 3 consecutive runs**. The supply-loop part of §1B is therefore demonstrated; Epic 9 stays `MECHANISM COMPLETE` by producer decision until §3.2/§3.3 land. Gaps:

- ~~**No salvage supply.**~~ **Implemented (2026-10-09, §3.1):** resources are typed (`ResourceData.YieldItem`, `EntitySnapshot`/`SpatialMemoryRecord.ResourceKind`); harvesting ore/herb nodes yields items; `SearchSalvage → GatherSalvage → TravelToStall → DeliverBounty/SellSalvage` chains are plannable; `EarnIncomeGoal` drives no-bounty salvage sales. Root cause also fixed: GOAP never ran live (§1A.2 #11).
- ~~**Scout bounties have no fulfilment behaviour.**~~ **Implemented (2026-10-09, §3.1):** typed claims (`ClaimGatherBountyAction` / `ClaimScoutBountyAction`), `ScoutSectorAction` + `ScoutIntentHandler` (enter the sector), `ReportScoutBountyAction` (report at a stall, sector marked explored).
- ~~**Trade stall ration stock only decreases.**~~ **Implemented (2026-10-09, §3.1):** `SupplyService` supply caravan (every 600 ticks, cap 50, 2g/unit `SupplyCaravan` sink) and dormant-node regrowth (regrowth on for all nodes, pens included).
- **Live loop emerges (2026-10-09, PlayMode, no injection):** crawlers claim and fulfil the seeded Scout bounty (about t260, Herb Hollow explored) and Gather bounty (about t720, player trust +15). They sell salvage, and the caravan restocks and nodes regrow with ledger conservation intact. This needed the trace-driven GOAP fixes (need-driven aid, static reachability regions, planner relevance pruning, affordability gating).
- ~~**Still blocking closure (§1B):** `OutpostSupplyLoopScenario` fails on its final zero-anomaly assertion (pen stacking, wall penetration, thrashing).~~ **Resolved (2026-10-10, [closure plan](../.agents/milestone1_closure_plan.md) Phase 1):** body-aware navigation (inflated wall rasterization, snapped A* endpoints, exact segment clearance, wall- and crowd-checked displacement, live-position pawn separation, nav-grid reset, ±20 play-area border), a visual-flash fix with a 2-tick display dwell in `AgentView`, and a planner fix (salvage/scout actions clear `AtTradeStall`).
- **Provisional economy stopgap (2026-10-10):** once agents navigated properly they drained the stall treasury and caravan restocks stopped. A `ProvisionalCaravanExport` faucet (caravan buys salvage above a 10-unit reserve at placeholder prices) keeps the loop running. It is **not** an economy decision: §3.3 is deferred until the game is functional (producer decision 2026-10-09). Side effect: the treasury now grows (≈830g by t3600).
- ~~Navigation is straight-line~~ **RESOLVED (2026-10-09):** Zero-allocation 8-directional Grid A* delivered in `AsyncPathfinder` with `StaticNavGrid`, `GridAStarSearcher`, and worker pooling (Deviation #7).
- **`MemoryPack` serialization is not delivered** (package not installed).
- **Epic MAP is `PAUSED`**, so the live session still runs in the isolated play-pen test arena rather than the outpost map (initial 9 sectors built as WIP towards the 12-sector target).

*Closure plan: proposed in §3A (pending producer approval). Epic 10 remains `QUEUED`.*

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
| 7 | Plain-C# `AgentState` + `AgentRegistry`; `AutonomousAgent` as view (§2.2) | ✅ Done | §1A.2 #2. Save/load still needs `MemoryPack` (not installed). |
| 8 | Profiled soak test → spatial-hash queries, per-thread planners, cognition scheduler (§2.5) | ⬜ Not started | `GoapPlanner` is still a locked singleton (§1A.2 #8). No `CognitionScheduler`. No soak runner (Epic Test-2). No measured agent budget. |
| 9 | Gameplay scope, separate test asmdef, remove stub engine (§2.8) | ✅ Done (2026-10-11) | §1A.2 #9. All 40 PlayMode tests pass with the new assembly layout. Not verified: a player build (relies on the standard `UNITY_INCLUDE_TESTS` constraint). A dedicated `GameplayLifetimeScope` / ScriptableObject-driven session definition was not done (`AppBootstrapper` stays the single scope). |
| 10a | One-page economy model: faucets, sinks, scarcity lever (§3.3) | ⏸ Deferred | Producer decision 2026-10-09: decide once the game is functional. Mechanisms exist (`InitialSeed` faucet, `SupplyCaravan` sink, node regrowth, ledger conservation, provisional `ProvisionalCaravanExport` faucet). **Blocks Epic 9 promotion.** |
| 10b | "Why?" agent inspector (§3.4) | 🟨 Groundwork | `DecisionSummary` (source tier + top 3 goal priorities) recorded on every intent and copied to `AgentState.LastDecision` on the main thread (2026-10-11). The inspector UI is not started (Track B). |
| 10c | Live-scenario Definition of Done (§4.2) | ✅ Done | §1B. |
| 10d | Docs single source of truth (§4.3) | ✅ Done | §0. |

Other review items still open:
- **GOAP fact budget (§2.7):** 36 of 64 facts used (`StateFact` 0–35).
- **Determinism (§2.1 note):** #5 and #14 resolved 2026-10-10. #6 is partially resolved; the arrival tick of async cognition results is still timing-dependent.
- **Single writer:** §1A.2 #15 (cognition writes plan/intent bookkeeping off-thread).

### Milestone 1 closure sequence (approved 2026-10-09)

Implementation plan: [`.agents/milestone1_closure_plan.md`](../.agents/milestone1_closure_plan.md).

1. ~~**Green PlayMode.**~~ **Done 2026-10-10:** 14/14, `OutpostSupplyLoopScenario` meets §1B for the supply loop.
2. ~~**Determinism**~~ **Done 2026-10-10:** seeded `SimRandom`, reproducible entity ids, ordered intent application and iteration. PlayMode 16/16.
3. ~~**Economy observation soak**~~ **Done 2026-10-11.** `EconomySoakScenario` and `scripts/run-economy-soak.sh` ran 6 runs: integrity holds. Findings are in the [plan](../.agents/milestone1_closure_plan.md) Phase 3 *Observations*. Without the provisional export the economy freezes within about 10 sim-minutes; with it, the treasury grows about 145g per sim-minute. The economy model (§3.3) stays **deferred**.
4. ~~**Personality + considerations (§3.2).**~~ **Done 2026-10-11.** Pen demo agents stay neutral. PlayMode 19/19 plus the explicit soak.
5. ~~**Test-scope separation**~~ **Done 2026-10-11** (#9, review §2.8). PlayMode 40/40.
6. ~~**Docs + promotion.**~~ **Docs reconciled 2026-10-11.** Epic 9 promotion is **not** done: it waits on the deferred §3.3 economy model (producer).

Later Milestone 1 scope: resume Epic MAP (live outpost map), deliver `MemoryPack`. Before Epic 10: soak test and scaling work (#8).

---

## Track A: Core Simulation & Cognition Engine (Authoritative Sequence)

*Status: Authoritative engine logic. Background cognition runs in `UniTask` against immutable snapshots; world mutation is applied by `SimulationManager`. See §1A for binding invariants and §1A.2 for known deviations.*

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
| **Epic 9** | [The Outpost Economy & Public Bounties](epics/epic-9.md) | **MECHANISM COMPLETE** | **Delivered (Slices 1–3 + Modernization):** unmanaged item/inventory structs, gold currency, trade stall entity, public bounty board with escrow/refund, `PlayerInterventionService` drained in `Tick()`, trade-to-affinity pipeline, GOAP economic actions/goals, zero-alloc Grid A* in `AsyncPathfinder`, 3 headless validators. **Deferred (not delivered):** `MemoryPack` state serialization, `EconomyZeroAllocValidator`, `EconomySerializationValidator`. **§3.1 supply side (2026-10-09, implemented, live run pending):** typed resources, salvage harvesting, Scout fulfilment, escrow-only bounty payout, supply caravan & regrowth, Outpost Commons arena, `SupplyLoopValidator`, `OutpostSupplyLoopScenario`. §3.2 personality landed 2026-10-11 (`PriceResponseScenario` passes). Stays `MECHANISM COMPLETE` until the deferred §3.3 economy model lands (producer decision 2026-10-09). **Open:** see *Milestone 1 Status*. |
| **Epic 10** | Sector Hazards & Dungeon Descent | **QUEUED** | Environmental sector hazards, gear-gated survival checks, sector descent gate clearance, floor relocation migration loop, `Unity.Collections` + Burst hazard spatial checks. |
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

### Epic UI-3: Guild Roster & Expedition Dispatch (QUEUED — Focus: Production Phase 2)
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
| **Epic Test-2** | Outpost Economy & Bounty Contract Harness | **ACTIVE** | Headless `BountyFulfillmentValidator`, `TradeTransactionValidator`, `EconomyCountersValidator` *(delivered)*; `OutpostSupplyLoopScenario` live-loop PlayMode scenario required by §1B *(delivered, passing)*; economy observation soak `EconomySoakScenario` + `scripts/run-economy-soak.sh` *(delivered 2026-10-11; observation only, no economic bands until §3.3)*. Remaining: soak assertions once the economy decisions are made. |

---

## Independent Epics: Cross-Cutting & World Architecture

*Status: Foundational spatial and environmental architectures that operate orthogonally to the specific feature tracks, providing the physical substrate for all agent lifecycles.*

### Epic MAP: [The Utilitarian Outpost Map & Agent Lifecycle Topology](epics/epic-map.md)
- **Status**: PAUSED (Independent Foundation) — partially started, then halted to prioritise the architecture review and documentation reconciliation
- **Architectural Scope**: Environmental Architecture, Spatial Sector Partitioning, Simulation Topology
- **Progress at pause (verified against the code, 2026-10-08)**:
  - *Exists:* `OutpostMapBuilder` / `IOutpostMapBuilder` (wall and doorway geometry, 10 canonical doors) with `OutpostMapBuilderValidator`, registered in DI.
  - *Not done:* `BuildMap()` is not invoked by the live session (`SwarmSpawner` still builds the isolated play-pen arena); `SimulationStarter` still initialises the 44×36 fallback spatial bounds; `FacilityZoneService` still defines the legacy play-pen zones (Phase 2); entity/resource population and lifecycle scenarios (Phases 3–4) not started.
- **Outpost Sector Scope**: The target design specifies **12 interconnected sectors** for the full outpost. The 9-sector layout in this epic represents the initial core topology constructed as incomplete work-in-progress (WIP) before being paused; it does not supersede the 12-sector design target.
- **Preceding Epic**: None (Replaces isolated test playpens from Epics 7 & 8)
- **Downstream Beneficiaries**: Track A (Epic 9 Outpost Economy, Epic 10 Descent), Track B (Epic UI-1 Facility Dashboard, Epic UI-2 Merchant Terminal), Track C (PlayMode Scenarios)
- **Goal**: Replace the isolated 10-pen test arena with a unified, utilitarian 9-sector outpost map featuring clean rectangular geometry and 2.5m open doorways. Accurately supports the complete agent survival and economic lifecycle:
  1. **Sector 1 (Central Outpost Hub)**: Central market nexus housing the Merchant Trade Stall, public Bounty Board, and Common Hearth Campfire ($r=3.5\text{m}$).
  2. **Sector 2 (Bunkhouse / Rest Quarters)**: Quiet residential retreat for low-stimuli sleep and energy recovery.
  3. **Sector 3 (Foraging Orchard)**: Hydroponic berry grove with renewable food bushes for foraging and altruistic feeding.
  4. **Sector 4 (Directive Yard)**: Open drill square with rally standard for group directives, training, and succession dominance contests.
  5. **Sector 5 (Deep Quarry)**: Industrial mine with Iron Ore resource nodes for gather bounties and salvage liquidation.
  6. **Sector 6 (Herbal Conservatory)**: Overgrown botanical sector with Medicinal Herb nodes for harvesting and scouting.
  7. **Sector 7 (Forward Descent Gate)**: Fortified airlock threshold for dungeon descent preparation and expedition staging (Epic 10).
  8. **Sectors 8 & 9 (West & East Transit Perimeters)**: Auxiliary bypass corridors connecting outer sectors and staging future artisan workshops (Milestone 3).
- **Key Deliverables**:
  - `OutpostMapBuilder`: Procedural, deterministic construction of grid-aligned walls, open doorways, and sector anchors.
  - `FacilityZoneService` expansion: Registration of 9 continuous non-overlapping sectors with shroud tracking.
  - Dual-ring connectivity: Cardinal hub doors and perimeter bypass corridors preventing pathfinding bottlenecks.
  - Headless test alignment: `FacilityZoneServiceValidator` and `SwarmPlayModeTests` updated for the unified outpost topology.

---

## Strategic Operating Rhythm

When working in parallel agent sessions:
1. **Engine Work**: Reference **Track A** (Epic 9 is `MECHANISM COMPLETE`; Epic 10 is `QUEUED`; next engine focus is a producer decision, see the proposed sequence in §3A). All code lives in `Simpiens.Cognition`, `Simpiens.Simulation`, and headless validators.
2. **UI Work**: Reference **Track B** (Epic UI-2 `COMPLETED`; no UI epic is `ACTIVE`; Epic UI-3 is `QUEUED` for Milestone 2). All code lives in `Simpiens.UI`, UI Toolkit UXML/USS assets, and presenters.
3. **Testing Work**: Reference **Track C** (current: **Epic Test-2**, `ACTIVE`). All code lives in `Simpiens.Testing` and `Assets/Tests/`.
4. **World & Environment Work**: Reference **Independent Epics** (**Epic MAP** is `PAUSED`; resume requires a producer decision). Code lives in `Simpiens.Environment`, `Simpiens.UI.Zones`, and spawner infrastructure.
5. **Every session**: follow §0 (document hierarchy and update order), §1A (invariants) and §1B (Definition of Done).

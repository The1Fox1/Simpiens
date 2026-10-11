# Architectural History & Decisions Log

> Append-only decision log. Status and scope live in `docs/ROADMAP.md` (highest source of truth); entries here record *what was decided and why*. Later entries below supersede earlier ones where they conflict.

This document tracks the progression of the Simpiens architecture, mapping completed epics, key technical decisions, and the rationale behind the code-first, decoupled simulation approach.

## Epic 1: Core Simulation & Physics Foundation
**Focus:** Establishing a modular, scalable foundation for 2D simulation.
**Key Decisions:**
- Move beyond relying heavily on Unity's visual editor by adopting a "code-first" structure.
- Implementation of basic entity physics and screen-boundary management using `Kinematic` and `Dynamic` rigidbodies appropriately.
- Introduction of `NodeController` as a strict "dumb view" to handle basic rendering and movement updates without executing heavy simulation logic in `Update()`.

## Epic 2: The Code-First Architecture
**Focus:** Full decoupling of simulation state from Unity's Scene/Hierarchy structure.
**Key Decisions:**
- **Dependency Injection:** Integrated VContainer to manage dependencies (e.g., `AppBootstrapper.cs`). This ensures all services are instantiated, injected, and configured at runtime before any scene dependencies kick in.
- **VContainer ITickable / IStartable:** Moved standard Unity lifecycle logic (`Start`, `Update`) into pure C# classes managed by the DI container. This reduces `MonoBehaviour` overhead and allows explicit control over the execution order.
- **Data-Driven Configuration:** `NodeController` and agents are initialized via structs (e.g., `NodeConfiguration`) passed in from the backend rather than tweaked individually via the Inspector.

## Epic 3: The Cognitive Pipeline & Async Pathfinding
**Focus:** Bridging deterministic simulation logic with asynchronous, heavy "thought" processes.
**Key Decisions:**
- **Dual-Loop Architecture:** Separated the real-time Unity main thread (handled by `SimulationManager`) from the background cognitive reasoning (handled by `CognitiveEngine` / `UniTask` threads).
- **Spatial Hash Grid:** Built a highly performant, pooled `SpatialHashGrid` that creates a `SharedWorldSnapshot` every tick. This snapshot can be safely read by background threads without triggering locks or Unity API restrictions.
- **Zero-Allocation Pathfinding:** Implemented `AsyncPathfinder` which calculates A* paths over the spatial grid on the thread pool, utilizing pooled `PathResponse` objects to eliminate garbage collection stutters.
- **Intent System:** Agents communicate with the main thread strictly through data objects derived from `AgentIntent`.

## Epic 4: State Mutation & Active Idling
**Focus:** Closing the loop between background cognition and main-thread state changes.
**Key Decisions:**
- **Execution Loop:** `SimulationManager` actively dequeues `AgentIntent` instances, tracks their state over multiple frames, and physically moves the pawns logic-first. 
- **Active Idling:** Implemented `WanderIntent` with hysteresis in `SurvivalUtilityEvaluator`. Sated agents request random paths to keep the simulation visually dynamic.
- **Thread-Safe State Mutation:** When an agent successfully completes an intent (like harvesting), the main thread mutates the state (e.g., decrementing `ResourceData` yields and increasing `Hunger`). 
- **Thread-Safe Randomization:** Discovered and fixed `UnityException` failures caused by `UnityEngine.Random` on background threads by utilizing `[System.ThreadStatic]` instances of `System.Random` alongside thread-safe `Mathf` trigonometric functions.

## Epic 5: The Memory & Knowledge Matrix
**Focus:** Removing omniscience and grounding agents in localized, decaying, and socially shareable memory.
**Key Decisions:**
- **Local Fog-of-War Memory:** Replaced direct queries against the global `SharedWorldSnapshot` with a local `AgentMemory` containing a `SpatialMemoryMap` and ring-buffer `MemoryLedger`.
- **Line-of-Sight & Temporal Decay:** Implemented dual-layer memory pruning in `UpdateMemory`. Unobserved entities fade over time based on type-specific decay thresholds (`PawnDecayTicks = 400`, `ResourceDecayTicks = 1500`), while entities seen to be missing are pruned immediately.
- **Intent Feedback & Deadlock Relief:** Integrated `IntentResult` (`Success`, `TargetMissing`, `PathBlocked`, `Aborted`) to drive agent drive mutations. Added target blacklisting and `FrustrationEvaluator` / `PanicIntent` for deadlock avoidance.
- **Timed Idling & Emergent Gossip:** Added a `Duration` property to `IdleIntent`. When agents idle in proximity (within 2.5 units), `SimulationManager` coordinates a zero-allocation mutual gossip exchange (`TryGossip`), transferring spatial records, updating intel to fresher timestamps, logging ledger events, and relieving frustration.

## Epic 6: Multi-Step Reasoning (GOAP)
**Focus:** Introducing Goal-Oriented Action Planning to transition agents from single-step reactive utility AI to multi-step chain reasoning.
**Key Decisions:**
- **Phase 1 (Zero-Allocation Action Graph & State Bitmasks):**
  - Designed `WorldState` as a 16-byte stack struct (`ulong Values, Mask`) rather than heap-allocated dictionaries. Precondition evaluation and effect applications operate via bitwise operations in single-digit clock cycles with 0 GC allocations.
  - Implemented `GoapAction` and `GoapGoal` base abstractions with procedural context validation and dynamic utility scoring.
  - Implemented `ActionGraph` with zero-allocation forward (`GetApplicableActions`) and backward (`GetActionsSatisfying`) querying for A* dependency chaining.
- **Phase 2 (A* Graph Search Engine & Zero-Allocation Planner):**
  - Designed `GoapPlanner` using forward A* search guided by an admissible branchless bitwise Hamming weight heuristic (`CountUnsatisfiedFacts` * minimum action cost).
  - Engineered zero-allocation search infrastructure: struct node pool (`GoapPlanNode[]`), array-backed binary min-heap open list (`int[] _openHeap`), and flat open-addressing closed hash table (`ClosedEntry[] _closedTable`).
- **Phase 3 (Planner Integration & Reflexive Preemption):**
  - Designed and implemented a Two-Tier Cognitive Architecture: Tier 1 handles emergency biological drives (e.g., Panic when Frustration > 80f) while Tier 2 handles deliberative long-term multi-step planning (`GoapCognitiveEvaluator`).
  - Added `WorldStateBuilder` for zero-allocation mapping from `AgentContext` and `AgentMemory` to bitmask `WorldState` in ~1-2 microseconds with strictly 0 GC allocations.
  - Implemented sequential plan execution in `AutonomousAgent`: stepping through `GoapPlan.CurrentAction.CreateIntentAsync()`, advancing steps upon `IntentResult.Success`, and invalidating plans upon environment mismatches (`TargetMissing`, `PathBlocked`).
  - Implemented reflexive preemption: when Tier 1 panic or emergency triggers, in-flight physical movement in `SimulationManager` is safely cancelled via `ISimulationManager.AbortIntent(agentId)`, freeing the active slot and resetting the deliberative plan without state corruption.
  - Verified end-to-end integration via `Epic6Phase3Validator` with zero compile warnings and 0 runtime allocations.

## Epic Alpha: Biological Needs, Mental Breaks & Autonomous Motor Watchdog [COMPLETED]
- Decoupled physical navigational watchdog stalls from biological drives in `SimulationManager` using a 3-tier progressive unstuck reflex (orthogonal nudge, relaxed clearance, target disengage).
- Implemented `AgentNeeds` (Hunger, Energy, Social) with metabolic decay and dynamic GOAP goal priorities.
- Added critical mental breaks: Starvation Panic (exploratory sprint, memory wipe) and Exhaustion Collapse in `MentalBreakEvaluator`.
- Integrated visual action-states into `AgentView` with zero-allocation procedural micro-animations.

## Epic 7: The Social Affinity Matrix & Relational Dynamics [COMPLETED]
- Implemented a 4-Axis Dyadic Matrix (`Warmth`, `Trust`, `Fear`, `Respect`) with temporal decay in `AgentMemory`.
- Integrated selective gossip filtering based on affinity and added GOAP world state relational facts.
- Pivoted macro-tribal hierarchies to Epic 8 to prevent the "simulation void trap" and grounded interpersonal dynamics in concrete world mechanics (Epic Beta).

## Epic Beta: Concrete World Mechanics & Physical Interdependence [COMPLETED]
- **Phase 1 (Resource Contention & Yielding):** Implemented pairwise social dominance deltas. Intimidated/deferent pawns yield contested resources without frustration, while rivals face simultaneous competition with memory logging.
- **Phase 2 (Altruistic Food Sharing):** Sated agents evaluate `ShareResourceIntent` based on Warmth and Trust to feed critically starving or collapsed companions.
- **Phase 3 (Gathering Anchors & Propinquity):** Introduced `EntityType.Anchor` (Campfires/Hearths). Sated pawns autonomously gather around anchors, accelerating gossip cooldowns 3x and passively accumulating $+2$ Warmth and $+1$ Trust.

## Architectural Decision: Multi-Track Branching Roadmap (Track A, B, C)
As development expanded across multiple concurrent sessions (UI redesign, test harness revamps, and deep cognitive simulation), the single-track linear queue was upgraded to a **Multi-Track Branching Architecture**:
- **Track A: Core Simulation & Cognition Engine** (Original sequence: Epics 1–11). Keeps the authoritative simulation decoupled and view-agnostic.
- **Track B: UI / UX & Player Interface** (Branching track: `Epic UI-1` Facility Dashboard onwards). Implements high-level player interfaces (e.g. UI Toolkit) as views over Track A's headless model.
- **Track C: Harness, Tooling & Diagnostics** (Branching track: `Epic Test-1` Visual Anomaly Detection onwards). Maintains headless playmode testing and validation suites.
*(See `docs/ROADMAP.md` and `curentfocus.txt` for active tracking)*

## Epic 8: Socio-Political Structures & The Generic Group Primitive
**Focus:** Establishing collective structures, dynamic group membership, hierarchy directives, and compliance/defiance dynamics.
**Key Decisions:**
- **Phase 1 (Identity Tags & Generic Group Primitive):**
  - Designed `GroupData`, `GroupMembership`, `GroupDirective`, and `IdentityTag` as unmanaged structs with bitwise flags and fixed byte identifiers.
  - Implemented `GroupRegistry` conforming to `IGroupRegistry` for lifecycle management, role promotions, and membership lookups.
  - Extended `SharedWorldSnapshot` with zero-allocation group buffers (`SetGroups`, `TryGetGroup`) synchronized by `SimulationManager` each tick.
  - Created `GroupPrimitiveValidator` confirming 0 GC allocations and deterministic multi-group tracking.
- **Phase 2 (Collective Directives & Compliance/Defiance Dynamics):**
  - Added GOAP planning facts: `HasActiveDirective = 21`, `AtDirectiveLocation = 22`, `DirectiveFulfilled = 23`, `IsGroupLeader = 24`.
  - Added episodic memory events: `DirectiveComplied = 23`, `SubordinateDefied = 24`, `DesertedGroup = 25`.
  - Engineered `ComplianceUtility` computing $U_{\text{comply}} = 0.35 \cdot \text{Respect} + 0.25 \cdot \text{Fear} + 0.10 \cdot \text{Warmth} + 0.25 \cdot \text{Allegiance} - 0.30 \cdot \text{Hunger} - 0.20 \cdot \text{Frustration}$, yielding categorical actions (`Comply`, `PassiveDefiance`, `ActiveDesertion`).
  - Implemented GOAP actions `TravelToDirectiveAction`, `ExecuteDirectiveAction`, and `ComplyDirectiveGoal` with starvation preemption override vs. fear intimidation.
  - Created procedural 16x16 pixel-art sprites in `AgentSpriteLibrary` for `Rallying = 9` (golden standard banner) and `Defiant = 10` (red clenched fist banner).
- **Phase 3 (Organic Emergence, Leadership Succession & Schisms):**
  - Added decoupled interface `ISocialAgent` implemented by `AutonomousAgent` enabling 100% pure headless domain testing without Unity `GameObject` native dependencies.
  - Implemented `SuccessionEngine` handling:
    - Organic bottom-up group emergence around gathering hearths (consensus election via net $(\text{Respect} - \text{Fear})$ standing).
    - Incapacitation and legitimacy watchdog (detecting collapse, starvation panic, dead/missing leaders, and negative average follower respect).
    - Tripartite succession: Peaceful ($\text{Cohesion} \ge 50$, Officer and respect priority), Dominance Challenge ($25 \le \text{Cohesion} < 50$, pairwise dominance delta, loser allegiance penalty / desertion), and Factional Schism ($\text{Cohesion} < 25$, bilateral warmth/trust partition into 2 splinter groups).
  - Extended `IGroupRegistry` and `GroupRegistry` with `UpdateCohesion`, `SetCohesion`, and `TransferLeadership`.
  - Added episodic memory events: `GroupFormed = 19`, `LeadershipAssumed = 20`, `DominanceChallenged = 21`, `GroupSchism = 22`.
  - Verified via `SuccessionValidator` with all 8 domain tests passing cleanly at strictly 0 GC allocations.

## Epic 9: The Outpost Economy & Public Bounties
**Focus:** Establishing indirect player intervention, decentralized market exchanges, zero-allocation inventory/currency primitives, and the trade-to-affinity pipeline.
**Key Decisions:**
- **Slice 1 (Unmanaged Inventory, Currency & Item Primitives):**
  - Designed `ItemInstance` as a 16-byte unmanaged struct (`LayoutKind.Sequential`, blittable) holding `ItemId`, `Type` (enum), `Quality`, `Quantity`, `BaseValue`, and `Durability`. Designed for direct `MemoryPack` zero-copy serialization.
  - Implemented `AgentInventory` with a pre-allocated fixed-capacity array (4 slots, stack limit 99) and bounded integer `Gold` balances with underflow protection (`TryDeductGold`).
  - Preserved backward compatibility by converting `AutonomousAgent.HasFoodInInventory` into a computed property backed by `AgentInventory` queries on `ItemType.Ration`.
  - Added economic bits 25–29 to `StateFact` bitmask and mapped inventory contents in `WorldStateBuilder`.
  - Validated via `InventoryValidator` (7 headless assertions).
- **Slice 2 (The Trade Stall & Commercial Exchange):**
  - **Unmanaged Trade Stall Primitives:** Created `TradeStallData` (44-byte unmanaged struct) with fixed prices, stock quantities, treasury gold, and immutable `With*` modifier patterns.
  - **Zero-Allocation Snapshot Buffering:** Added `TradeStallData[8] StallBuffer` to `SharedWorldSnapshot` synced directly from `SimulationManager.Tick()` each frame, allowing background GOAP planning threads to inspect stalls without locks or heap allocations.
  - **Atomic Transaction Engine:** Implemented `TradeIntent` execution in `SimulationManager.ExecuteIntent()` handling distance checks (2.0m radius), inventory capacity checks, stock validation, and atomic two-way item/gold transfers with zero net currency leaks.
  - **Trade-to-Affinity Pipeline:** Connected commercial exchanges to `AgentMemory.AffinityRecord` towards `WorldRegistry.PlayerEntityId`:
    - Fair trades grant $+10\text{ Trust}$, $+5\text{ Warmth}$, and $+5\text{ Respect}$.
    - Price gouging ($> 150\%$ base cost) inflicts $-15\text{ Trust}$ and $-10\text{ Warmth}$.
  - **GOAP Commercial Actions & Goals:** Implemented `TravelToStallAction`, `BuyProvisionsAction`, `SellSalvageAction`, `PurchaseProvisionsGoal`, and `LiquidateSalvageGoal` hooked into `ActionGraph` and `WorldStateBuilder`.
  - **Narrative Chronicle Integration:** Added `ChronicleEventType.TradeTransaction = 14` with relative timestamps and humanized feed templates.
  - **Headless Validation:** Verified through `TradeTransactionValidator` (8 comprehensive domain tests passing with 0 runtime errors and 0 allocations).
- **Slice 3 (The Public Bounty Board & Player Intervention Pipeline):**
  - **Indirect Intervention Pipeline:** Injected `IPlayerInterventionService` into `SimulationManager`. Queued `InfluenceEvent` commands (`PostBounty`, `CancelBounty`, `UpdateStallPrices`) are drained in `SimulationManager.Tick()` prior to spatial snapshot generation, adhering strictly to defensive null-safety for isolated testing.
  - **Unmanaged Contract Primitives:** Designed `BountyContract` as a 64-byte blittable struct (`[StructLayout(LayoutKind.Sequential)]`, 1 CPU cache line) containing target sector, item type, quantity, reward, claimant GUID, location, and lifetime ticks. Added `BountyBuffer[16]` on `SharedWorldSnapshot` with zero GC allocations.
  - **Escrow & Refund Lifecycle:** When a player posts a bounty, gold is escrowed from the outpost trade stall treasury. Upon contract expiration or player cancellation, escrowed gold is automatically refunded to the stall.
  - **Atomic Contract Fulfillment:** Implemented `ClaimBountyIntent` (single-claim lock) and `DeliverBountyIntent` in `SimulationManager.ExecuteIntent()`. Successful delivery verifies stall reach, removes salvage from agent inventory, deposits gold, increments stall stockpile, completes contract, and boosts dyadic affinity toward `PlayerEntityId` ($+15\text{ Trust}, +10\text{ Warmth}, +10\text{ Respect}$).
  - **GOAP Freelance Loop:** Implemented `ClaimBountyAction`, `DeliverBountyAction`, and `FulfillBountyGoal`. Integrated `StateFact.HasActiveBounty` into Section 8 of `WorldStateBuilder`.
  - **Narrative Story Integration:** Added `ChronicleEventType` values `BountyPosted = 15`, `BountyClaimed = 16`, `BountyFulfilled = 17`, and `BountyExpired = 18` with relative templates.
  - **Headless Validation:** Verified through `BountyFulfillmentValidator` (8 comprehensive domain tests passing with 0 runtime errors and 0 allocations).

## Epic 9 Reconciliation (2026-10-08)
- The Epic 9 section above describes the *delivered* mechanisms (Slices 1-3). Statements that structs were "designed for direct MemoryPack zero-copy serialization" describe memory layout intent only: MemoryPack is not installed and nothing serializes these types yet.
- Deferred out of Epic 9 and unscheduled: MemoryPack serialization, zero-allocation Grid A* in `AsyncPathfinder`, `EconomyZeroAllocValidator`, `EconomySerializationValidator`.
- Epic 9 status is `MECHANISM COMPLETE`: validators pass, but no live-simulation scenario demonstrates the bounty/trade loop (the validators inject salvage into inventories).

## Architectural Decision: Multiplayer-Readiness (Producer decision, 2026-10-08)
- **Decision:** Multiplayer is a required feature after the MVP. The MVP ships single-player, but the architecture must not prevent adding an authoritative client-server runtime later.
- **Consequence:** New simulation work is bound by the constraints in `docs/ROADMAP.md` §1A.1 (fixed logical ticks, typed commands applied in deterministic order, seeded RNG, plain-data state, single-writer threading, views never mutate). Existing code that violates them is catalogued in §1A.2 so it is not copied.

## Architectural Decision: Definition of Done and Document Hierarchy (2026-10-08)
- **Definition of Done:** an epic is `COMPLETED` only when a PlayMode scenario demonstrates the feature emerging in the live simulation without injected preconditions; validator-only completion is `MECHANISM COMPLETE`. Rationale: Epic 9 passed all its validators while the live loop (salvage production) did not exist. See `docs/ROADMAP.md` §1B.
- **Document hierarchy:** `docs/ROADMAP.md` > `AGENTS.md` > `docs/epics/*.md` > `docs/Game Design Proposal.md` > `.agents/curentfocus.txt`. Status is updated roadmap-first. Rationale: five documents disagreed on epic status and sector count, and automated agents treat these files as ground truth.
- **Epic MAP paused:** partially started (map builder + validator exist, not wired into the live session); 9 sectors represents incomplete WIP towards the 12-sector design target.

## Architectural Decision: Section 2.1 Fixed-Tick Simulation Architecture (2026-10-08)
- **Problem:** Simulation state mutation previously occurred on render frame time (`Time.deltaTime`, `Time.time`) across `SimulationManager` (which implemented VContainer's `ITickable`), `AutonomousAgent`, intent execution, watchdog timers, and biological drives. This introduced frame-rate-dependent gameplay drift, broken pause semantics where timers advanced during pause, and non-deterministic behavior incompatible with multiplayer-readiness.
- **Decision:**
  - Game state mutation advances strictly on discrete logical ticks emitted by `ISimulationClock` (10 Hz, `FixedTickDeltaTime = 0.1f`).
  - Decoupled `SimulationManager` and `SwarmSpawner` from `ITickable`, binding them directly to `_clock.OnSimulationTick`.
  - Converted all drive decay, intent movement, periodic hearth checks, and watchdog decrements to evaluate against `FixedTickDeltaTime` or `currentTick`.
  - Added visual interpolation support via `NodeController` lerping `transform.position` between `PreviousLogicalPosition` and `LogicalPosition` driven by `clock.InterpolationAlpha`.
  - Validated via `SimulationClockValidator` (5 tests covering tick rates, speed scaling, pause freeze, periodic exactness, interpolation alpha, and 0-byte GC allocations across 1,000 ticks).

## Architectural Decision: Section 2.2 Decoupled Agent State & Cognition Architecture (2026-10-08)
- **Problem:** Agent state (needs, inventory, memory, active GOAP plan, physical coordinates, and visual emotion timers) was owned by `AutonomousAgent`, a Unity `MonoBehaviour`. Cognition and intent consequence logic were embedded directly within this component. This violated the "No Logic in MonoBehaviours" rule, made headless execution without scene GameObjects impossible, duplicated intent consequence execution between `AutonomousAgent` and `SimulationManager`, and prevented headless multi-agent simulation and authoritative client-server multiplayer.
- **Decision:**
  - **Authoritative Pure C# Model (`AgentState`):** Extracted all simulation state (`AgentNeeds`, `AgentInventory`, `AgentMemory`, GOAP goals/plans, emotional state, logical `Position`) into `AgentState`, implementing `ISocialAgent`. State advances deterministically via `AgentState.Tick(float dt, int currentTick)` with strictly 0 GC allocations in hot paths.
  - **In-Memory Agent Directory (`IAgentRegistry` / `AgentRegistry`):** Added a thread-safe registry providing $O(1)$ lookup by GUID and allocation-free iteration across active agents for the simulation loop.
  - **Decoupled Consequence Service (`IAgentConsequenceService` / `AgentConsequenceService`):** Centralized action completion and reward/penalty mutations (hunger recovery, gold/item transfers, mental break resets) into an authoritative service, eliminating split/duplicate mutations.
  - **Asynchronous Deliberative Engine (`IAgentCognitionService` / `AgentCognitionService`):** Decoupled the dual-loop cognition pipeline from MonoBehaviours, orchestrating Tier-1 evaluators, Tier-2 GOAP planning, and intent submission off-thread via UniTask.
  - **Backward-Compatible Façade (`AutonomousAgent`):** Refactored `AutonomousAgent` into a thin `MonoBehaviour` view delegating state, needs, inventory, memory, and cognition to `AgentState` and `IAgentCognitionService`, preserving 100% Inspector and test suite compatibility.
  - **Comprehensive Validation (`AgentStateValidator`):** Implemented a 5-scenario headless test suite verifying headless state lifecycle, context generation, null-dependency isolation, consequence ownership, façade synchronization, and 0 GC heap allocations across 1,000 ticks.

## Architectural Decision: Section 2.3 Single-Writer Threading Architecture & Snapshot Memory Isolation (2026-10-09)
- **Problem:** 
  - While `SharedWorldSnapshot` was designed to be thread-safe, `AgentCognitionService` previously switched to the thread pool (`await UniTask.SwitchToThreadPool()`) *before* acquiring or retaining the snapshot, opening a race condition where the snapshot buffer could be recycled by `SimulationManager` before background workers read it.
  - Furthermore, `AgentContext` directly referenced live `AgentMemory` and `AgentInventory` instances. Background evaluators and planners iterated live non-thread-safe dictionaries (`SpatialMemoryMap`, `_blacklistedEntities`, `_affinityMap`) while the main simulation thread mutated them (e.g. gossip exchanges, trade transactions, memory decay, and social affinity mutations), producing intermittent `InvalidOperationException` ("Collection was modified; enumeration operation may not execute") and torn struct reads under high load.
  - Querying `AgentMemory.GetAffinity(peerId, currentTick)` had an active write side-effect (`_affinityMap[peerId] = regressed;`), causing dictionary mutations on read threads.
  - Evaluator-driven panic memory wipes (`FrustrationEvaluator`) mutated memory state directly on background worker threads, violating the single-writer invariant.
- **Decision:**
  - **Enforce Single-Writer Rule:** All simulation state mutations (inventory, memory, affinity, drives) occur strictly on the authoritative main simulation thread. Background worker threads have read-only access to immutable snapshots.
  - **Main-Thread Snapshot Retention:** In `AgentCognitionService`, `snapshot.Retain()` is invoked on the main thread *prior* to `UniTask.SwitchToThreadPool()`, and safely released in a `finally` block upon task completion.
  - **Preallocated Double-Buffered Snapshots (`IAgentMemory` / `IAgentInventory`):**
    - Introduced `IAgentMemory` and `AgentMemorySnapshot` alongside `IAgentInventory` and `AgentInventorySnapshot`.
    - `AgentMemory` and `AgentInventory` maintain pre-allocated double buffers (`_snapshots[2]`). Taking a snapshot (`GetSnapshot()`) performs a zero-allocation copy (`PopulateFrom`) into the inactive buffer on the main thread before background dispatch.
  - **Pure Side-Effect-Free Affinity Reads:** Refactored `GetAffinity(peerId, currentTick)` across `AgentMemory` and `AgentMemorySnapshot` to compute temporal regression on-the-fly without writing back to internal dictionaries during read operations.
  - **Authoritative Main-Thread Memory Pruning:** Migrated panic memory wipes to `AgentConsequenceService`, authoritatively executing `agent.Memory?.PruneStaleResourceMemories(currentTick)` on the main thread when handling `PanicIntent`.
  - **Rigorous Domain Validation (`SingleWriterMemoryValidator`):** Implemented a 5-test headless validation suite proving:
    1. Snapshot lifetime retention across ticks until explicitly released.
    2. Zero concurrent access exceptions across 4 concurrent worker threads under aggressive main-thread gossip, pruning, and affinity mutations.
    3. Pure side-effect-free affinity queries with zero dictionary mutations on read.
    4. Inventory snapshot isolation from main-thread transaction mutations.
    5. Strictly 0 GC heap allocations across 1,000 snapshot cycles.

## Architectural Decision: Section 2.4 Modular Intent Handlers, Typed Commands & Authoritative Economy Ledger (2026-10-09)
- **Problem:**
  - `SimulationManager.cs` had become a monolithic 1,500+ line class containing an 8-branch, 700+ line switch statement in `ExecuteIntent` handling all intent types (`Wander`, `Panic`, `HarvestResource`, `ShareResource`, `Idle`, `Trade`, `ClaimBounty`, `DeliverBounty`).
  - Player commands arrived through `InfluenceEvent` as string command identifiers and comma-separated string payloads parsed via `.Split()`, incurring heap allocations on tick and preventing schema versioning.
  - In `ParseAndPostBounty`, if an outpost trade stall lacked sufficient treasury gold to cover a bounty reward, the contract was posted anyway. When cancelled or expired, the unescrowed reward was refunded to the stall, causing an economic exploit that minted gold from nothing.
  - Economy transactions lacked an authoritative single-source ledger and conservation invariants ($\Delta \text{Circulation} = \text{Faucets} - \text{Sinks}$).
  - `IToolExecutionRegistry` was declared but unimplemented.
- **Decision:**
  - **Zero-Allocation Typed Commands:** Converted `InfluenceEvent` into a typed union struct wrapping unmanaged blittable payloads (`PostBountyCommand`, `CancelBountyCommand`, `UpdateStallPricesCommand`) with zero-allocation factory methods (`InfluenceEvent.CreatePostBounty`, etc.). Retained a legacy string constructor for backwards compatibility with existing test suites. Updated UI presenters (`FacilityDashboardPresenter`) to dispatch typed commands directly.
  - **Authoritative Economy Ledger (`IEconomyLedger` / `EconomyLedger`):** Introduced a pure C# service tracking all gold accounts (stalls, agent wallets, bounty escrows, named faucets/sinks).
    - `TryLockEscrow` verifies stall solvency upfront and locks funds; bounty posting is strictly rejected if treasury gold is insufficient.
    - `TryRefundEscrow` only refunds gold actually held in escrow, completely eliminating the free-gold minting bug.
    - Enforced conservation invariant checks: $\text{Current Circulating Gold} + \text{Escrowed Gold} == \text{Initial Gold} + \text{Faucets} - \text{Sinks}$.
  - **Modular Intent Execution Architecture:** Decomposed `SimulationManager.ExecuteIntent` into 8 decoupled single-responsibility `IIntentHandler<T>` classes:
    - `WanderIntentHandler`: Path navigation and movement visual state.
    - `PanicIntentHandler`: Panic fleeing and minimum duration enforcement.
    - `HarvestResourceIntentHandler`: Node reach, social yielding with submission scores, rivalry tracking, harvest timers, node yields, and node destruction.
    - `ShareResourceIntentHandler`: Proximity reach, food transfer duration, hunger replenishment, altruism memory recording, and narrative chronicling.
    - `IdleIntentHandler`: Idle timers, emotion visual states, and gossip checks.
    - `TradeIntentHandler`: Atomic ration purchases (with price gouging penalties vs. fair trade trust bonuses) and raw salvage sales.
    - `ClaimBountyIntentHandler`: Atomic single-claim contract locking and chronicle recording.
    - `DeliverBountyIntentHandler`: Delivery validation, escrow release via `IEconomyLedger`, salvage removal, stock replenishment, claimant reward payout, and narrative recording.
  - **Registry & Execution Context (`IIntentHandlerRegistry`, `IIntentExecutionContext`):**
    - Created `IIntentHandlerRegistry` implemented by `IntentHandlerRegistry` with `CreateDefault()` factory method.
    - `SimulationManager` implements `IIntentExecutionContext`, providing domain access to handlers while delegating all intent execution to `_intentRegistry.ExecuteIntent(...)`.
    - Maintained defensive null-safety with benign fallbacks for isolated test runners.
  - **MCP Tool Execution Bridge (`IToolExecutionRegistry` / `ToolExecutionRegistry`):** Implemented `IToolExecutionRegistry` mapping cognitive tool execution requests into typed intents dispatched through the engine.
  - **Comprehensive Domain Validation (`SimulationManagerModularizationValidator`):** Implemented a 5-test headless validation suite proving:
    1. Zero-allocation typed command posting, cancellation, and stall price updates.
    2. Escrow failure rejection when a trade stall lacks sufficient treasury funds.
    3. Authoritative ledger conservation invariant enforcement ($60 + 90 + 0 == 150 - 0$).
    4. Modular intent execution across all 8 handler types.
    5. Zero GC heap allocations across 1,000 intent execution cycles.

## Architectural Decision: Section 2.6 High-Performance Zero-Allocation Grid A* Navigation Engine (2026-10-09)
- **Problem:**
  - `AsyncPathfinder.cs` was a straight-line displacement placeholder (Roadmap Deviation #7) that stepped directly toward the target without obstacle avoidance, preventing unpausing of Epic MAP (dungeon sectors and walls).
  - Static walls and dynamic entities were stored together in `SharedWorldSnapshot.Grid` (`EntitySnapshot[16]` per cell). Placing static map walls consumed dynamic entity slots, risking buffer overflows in crowded sectors.
  - Pathfinding needed to run concurrently across multiple off-thread cognitive worker tasks without thread contention, lock serialization, or heap allocations during high-frequency simulation.
- **Decision:**
  - **Static Nav Grid Layer (`StaticNavGrid`):**
    - Introduced a dedicated static obstacle grid (`StaticNavGrid`) separating static map boundaries/walls from per-frame dynamic entity snapshots.
    - `SpatialHashGrid` registers `EntityType.Wall` into `_staticGrid`, freeing the 16-entity per-cell buffer in `_currentTickSnapshot.Grid` strictly for dynamic pawns, resource nodes, and trade stalls.
    - Thread-safe coordinate transformation methods (`WorldToCell`, `CellToWorld`, `IsCellWalkable`, `IsWorldWalkable`) shared with snapshots.
  - **Zero-Allocation 8-Directional Grid A\* (`GridAStarSearcher`):**
    - Implemented a dedicated 8-directional Grid A* engine leveraging the binary min-heap and struct node pool architecture proven in `GoapPlanner.cs`.
    - Employs $O(1)$ epoch-based visited tracking (`_currentIteration`), completely eliminating array clears and GC allocations between queries.
    - Uses standard octile distance heuristic ($D = 1.0f$, $D_2 = \sqrt{2} \approx 1.414f$) for natural diagonal exploration.
    - Enforces diagonal corner-cutting prevention (`!IsWalkable(x, parentY) || !IsWalkable(parentX, y)`), guaranteeing pawns never squeeze diagonally between orthogonal wall corners.
    - Nearest-walkable fallback: When a target is inside an unwalkable obstacle, A* safely falls back to the closest accessible open cell.
  - **Raycast String-Pulling Path Smoothing:**
    - Integrated post-processing line-of-sight checks (`SharedWorldSnapshot.HasLineOfSight`) to string-pull jagged staircase paths into clean, direct waypoints.
    - Hardened `HasLineOfSight` with diagonal corner-crossing clearance checks so smoothed line-of-sight vectors never clip through wall corners.
  - **Lock-Free Multi-Threaded Searcher Pool (`AsyncPathfinder`):**
    - Upgraded `AsyncPathfinder` with a thread-safe `ConcurrentQueue<GridAStarSearcher>` worker pool.
    - Cognitive workers dequeue a searcher, execute queries against immutable `SharedWorldSnapshot`s off-thread without lock contention, and return searchers in `finally` blocks.
  - **Comprehensive Domain Validation (`GridAStarValidator` & UTF PlayMode Tests):**
    - Implemented a 7-test headless validation suite (`GridAStarValidator`) verifying:
      1. Direct unobstructed paths collapsing to 2-point direct vectors.
      2. Obstacle detours routing cleanly around vertical barriers.
      3. Doorway navigation finding narrow room portals.
      4. Corner-cutting prevention disallowing diagonal transitions through orthogonal walls.
      5. Enclosed target fallback safely terminating at the nearest reachable boundary without infinite loops.
      6. Raycast path smoothing collapsing straight corridors into minimal waypoints.
      7. Multi-threaded worker pool executing 1,000 queries across 4 concurrent workers with 0 exceptions and strictly 0 GC heap bytes allocated in steady state.
    - Added `Scenario_GridAStar_AllDomainValidationTestsPass` to UTF PlayMode test suite (`SwarmPlayModeTests.cs`).



## Architectural Decision: Section 3.1 Close the Milestone 1 Supply Side (2026-10-09)
- **Problem:**
  - The Milestone 1 loop (post bounty → harvest salvage → deliver/sell at stall → trust grows) could not run live. Verifying review §3.1 against the code turned up two gaps the review missed:
    - **F1 (root cause):** `SurvivalUtilityEvaluator` returned a non-null intent on every path, so `AgentCognitionService` never reached Tier-2 GOAP in the live session. Every GOAP goal from Epics 6–9 only ran under validator injection.
    - **F7:** all 24 session agents were scripted into walled pens, and the stall sat inside Pen 3.
  - Also: untyped resources (F2), harvest always produced food (F3), a stubbed `HarvestResourceAction` (F4), unfulfillable Scout bounties (F5), no restock and permanent node destruction (F6), and seeded bounties minting gold because they bypassed escrow (F8).
- **Decision:**
  - **Tiering:** Tier 1 is reflex-only (hunger ≥ 60 with reachable known food). Idle/wander moves unchanged into the Tier-3 `AmbientWanderEvaluator`, injected into `AgentCognitionService` as an optional fallback. A `Failed` intent result now aborts the active plan, so failed transactions replan instead of retrying forever.
  - **Typed resources:** `ResourceData.YieldItem/MaxYield/RegrowTicks/IsDormant`. `ResourceKind` flows through `NodeController` → `EntitySnapshot` → `SpatialMemoryRecord` and survives gossip. `ItemType.None` is treated as food, so legacy records keep their meaning.
  - **GOAP domain:** six new facts (35 of 64 bits used). Typed claim actions are only valid while a bounty of their type is open. `SearchSalvage` heads for the advertised bounty location, otherwise a deterministic exploration waypoint derived from a hash of agent id and 50-tick bucket. `GatherSalvage` harvests the remaining bounty quantity in place. `ScoutSector`/`ReportScoutBounty` handle Scout bounties. `EarnIncomeGoal` (with a `HasSoldSalvage` fact) drives no-bounty salvage sales, a small addition beyond the plan: `LiquidateSalvageGoal` is only valid while already carrying salvage, so on its own it could not start the chain. While a Gather bounty is claimed, `KnowsSalvageLocation` is narrowed to that bounty's item.
  - **Shared helpers:** `ResourceTargeting` and `BountyQueries` provide allocation-free selection with GUID tie-breaks, so outcomes never depend on dictionary order.
  - **Single writer:** handlers never touch GameObjects for depletion. They call `IIntentExecutionContext.DepleteResource`, and `SimulationManager` decides between dormant and permanently removed and owns the view toggle. Scout reports are written to `AgentState` on the main thread.
  - **Economy:** `RegisterBounty` escrows or rejects every live bounty. Delivery pays only from released escrow. `IEconomyLedger.Reset()` was added, and session gold enters through one `InitialSeed` faucet so conservation holds from tick 0.
  - **Supply:** `SupplyService` (plain C#, tick-based, zero-alloc steady state) keeps dormant nodes sorted by (ReadyTick, GUID) and runs the caravan (600 ticks, cap 50, 2g/unit `SupplyCaravan` sink, skipped if unaffordable). Producer chose regrowth **on for all nodes**, demo pens included.
  - **Arena:** the open yard north of the pens became the Outpost Commons (zones 11–13) with 6 free crawlers. The production domain is still in `SwarmSpawner` (Deviation #9); it was extended only with the new registrations.
  - **Related fix:** `PurchaseProvisionsGoal` now checks the nearest stall's price and stock. It previously accepted 5g against a 15g price, which became a fail/replan loop once GOAP ran. The salvage-selling goals now require stall treasury.
- **Verification status:** `Simpiens` and `Simpiens.Tests.PlayMode` compile. `SupplyLoopValidator` (8 tests) and `OutpostSupplyLoopScenario` (no-injection DoD scenario) were added but **not yet run**. Epic 9 stays `MECHANISM COMPLETE` (producer decision).

## Architectural Decision: ZLogger Logging Integration (2026-10-09)
- **Problem:** Logging was ad-hoc `Debug.Log`: no levels below Log, no per-subsystem filtering, string-built messages allocating on hot paths, and spam that buried signal. One PlayMode run produced 1,225 gossip lines and more than 300 succession lines. Diagnosing why crawlers never finished the Gather bounty would have required throwaway logging.
- **Decision:**
  - **Library:** the producer approved ZLogger 2.5.10 (Cysharp, built on Microsoft.Extensions.Logging). Its interpolated-string handlers check `IsEnabled` before evaluating holes, so a disabled log evaluates and allocates nothing.
  - **Install:** the NuGet dependency closure is **vendored** into `Assets/Plugins/ZLogger/` by `scripts/update-zlogger.sh`, which runs `dotnet restore` and excludes facades Unity provides (`System.Buffers`, `System.Memory`, `System.Numerics.Vectors`, `System.Threading.Tasks.Extensions`). Vendoring was chosen over NuGetForUnity because restoring in headless batchmode isn't reliable and vendoring is reproducible. ZLogger.Unity (the Unity console sink) comes from a git package pinned to tag `2.5.10`. `Assets/Scripts/csc.rsp` sets `-langVersion:10` and omits `-nullable` to avoid warning floods.
  - **`Simpiens.Diagnostics.SimLog`:**
    - 11 channel loggers. `NullLogger` until configured, so headless validators stay silent and free.
    - Configuration from env vars, then `-simlog*` args, then code; sinks have their own floors (console default Info, optional file sink).
    - A tick-stamped prefix, `[t<tick>] <LVL> <Channel> |`.
    - An agent-name registry and an agent focus filter for per-agent decision traces.
  - **File sink:** ZLogger's built-in file provider formats on a background writer, which would stamp lines with a later tick. `TickStampedFileLogProcessor` formats synchronously on the logging thread instead.
  - **Sink safety:** both sinks are wrapped in `SafeSinkLogProcessor`, which applies the sink's own level floor and catches formatting/writing failures. The first Unity run showed an unsupported custom numeric format (`{x:+0;-0}`, rejected by ZLogger's UTF-8 formatter) throwing *into* the trade handler. Logging must never alter simulation control flow, so failures are reported as a console warning and the line is dropped.
  - **Package wiring:** `Simpiens.asmdef` must list `ZLogger.Unity` explicitly, because auto-referencing does not apply to assemblies with explicit references. Benign Editor message: `com.unity.collections` ships `System.IO.Hashing` 9.0 in its test folder, so the Editor ignores the vendored 8.0 copy, which player builds still need.
  - **Instrumentation:**
    - **Cognition:** which tier produced each intent, goal candidates and priorities (Trace), plan chosen or not found with the world state, step execution, abort reasons, pre-emption by reflexes, and exceptions previously lost to fire-and-forget tasks.
    - **Consequences:** the result of every intent.
    - **Economy:** every trade, claim, delivery and scout rejection with a stated reason; ledger escrow, faucet and sink activity; conservation violations at Error.
    - **Supply:** caravan restocks and skips, node regrowth.
    - **Navigation:** motor watchdog tiers, which flag that nudges and back-offs don't check walls (relevant to the obstacle-penetration anomalies); unreachable paths at Trace.
    - **Groups and drives:** succession at Info only when the leader actually changes; collapses, tantrums and panics.
    - **Planner:** GOAP node-pool exhaustion, which was previously a silent plan failure.
    - **Legacy calls:** all 23 app-code `Debug.Log` calls converted.
  - **Fix found while instrumenting:** `MentalBreakEvaluator.HasKnownFood` counted salvage nodes as food, so a starving agent who knew only ore would never trigger a starvation-panic search.
- **Verification:** `LoggingValidator` has 5 tests, all passing in Unity PlayMode on 2026-10-09: disabled logs allocate 0 bytes and skip hole evaluation; channel/sink levels; agent focus; file format; formatting failures contained. `OutpostSupplyLoopScenario` writes a crawler-focused trace to `Logs/outpost-supply-loop.log`.

## Architectural Decision: GOAP Live-Loop Fixes from the SimLog Trace (2026-10-09)
- **Problem:** The first crawler decision trace (`Logs/outpost-supply-loop.log`) showed why the §3.1 loop didn't emerge:
  - **(A)** `AidCompanionGoal` was valid whenever the agent was sated, and `ShareFoodAction` was a target-less stub. Crawlers looped AidCompanion → ShareFood: TargetMissing about 16,000 times, crowding out every other goal.
  - **(B)** Goals and actions only checked that stalls and bounties *existed*. Walled pen agents claimed both Commons bounties at t181–182 and failed about 2,200 purchases "out of stall range".
  - **(C)** The planner expanded every applicable action. Impossible or unreachable goals exhausted the 256-node pool about 2,000 times.
  - Follow-up: the same fail/replan pattern for purchases without gold and sales the treasury couldn't pay.
- **Decision:**
  - **(A)** `AltruismTargeting` holds the distressed-peer rule, shared by the Tier-1 `AltruismEvaluator` and the GOAP goal/action. `AidCompanionGoal` and `ShareFoodAction` are valid only while a perceived companion is starving or collapsed. `ShareFood` paths to that peer. The share handler no longer hands food over at a distance when a path is missing.
  - **(B) Static reachability.**
    - `StaticNavGrid` keeps 4-connected region labels, matching A*'s no-corner-cutting rule. They are rebuilt on the main thread only when walkability actually changes (version counter) and published by reference swap, so readers always see an immutable labelling. `AreConnected` treats unknown as connected.
    - `Reachability.IsReachable` gates `GetStallAccess` (None/Unreachable/Reachable), all stall actions and goals, and `BountyQueries.TryGetOpenBounty`: target location and redemption stall must both be reachable. `FulfillBountyGoal` now uses `BountyQueries`.
  - **(C) Planner relevance pruning.** `GoapPlanner` computes a backward relevance closure over fact masks, evaluates `IsValid`/`GetCost` once per relevant action, and rejects a goal before searching if an unsatisfied fact has no usable producer. Pruning is sound: irrelevant actions only touch facts the goal never depends on. `LastNodeCount` is exposed for diagnostics.
  - **Affordability.** `BuyProvisionsAction` is plannable only when a ration is affordable at a reachable stall. `SellSalvageAction` sells the held item the treasury can pay for. `LiquidateSalvageGoal` requires an affordable held item. Speculative salvage gathering skips types the treasury can't pay for.
- **Result (PlayMode, 2026-10-09):** with no injection, crawlers claim and fulfil both seeded bounties: Scout at t362, Herb Hollow explored; Gather at t607, player trust +15. Salvage sells, the caravan restocks, and nodes regrow. Zero node-pool warnings. `GoapLiveLoopFixesValidator` passes 5 of 5.
- **Open (not part of this change):**
  - Demo-pen visual anomalies remain: pawn stacking at the Pen 4 hearth and Pens 9/10, and obstacle penetration (watchdog nudges and back-offs are not checked against walls).
  - `StaticNavGrid` is never cleared on respawn, so injected chaos walls persist across scenarios.
  - Treasury drain is §3.3.

## Architectural Decision: Navigation Clearance & Visual Stability (Milestone 1 closure, Phase 1, 2026-10-10)
- **Problem:** PlayMode was 10/14. Wall penetrations, pawn stacking and visual thrashing failed `OutpostSupplyLoopScenario` and all three anomaly scenarios.
- **Root causes (from traces):**
  - A* pinned its final waypoint to the exact target even inside blocked cells.
  - Watchdog nudges, back-offs, the contention yield step and panic movement wrote positions with no wall or pawn check.
  - The static grid was never cleared on respawn.
  - Pawn separation compared against one-tick-stale snapshot positions.
  - Reflex pre-emption reset the visual state to Idle mid-evaluation.
- **Decision:**
  - **Invariant:** a pawn centre never enters a statically blocked cell. Walls are rasterized inflated by `NavConstants.PawnBodyRadius`, so that invariant alone guarantees no geometric overlap.
  - Smoothing and movement use an exact grid traversal (`IsSegmentStaticClear`).
  - **Single displacement entry point:** `SimulationManager.TryDisplacePawn` is wall-checked and crowd-aware. Panic movement ignores occupied waypoints, but never walls or pawns.
  - **Pawn separation** uses snapshot cells to find candidates and live registry positions to judge them. Stepping apart is always allowed. An occupied final waypoint within 1m counts as arrival.
  - **Play-area border:** cells outside ±20 are permanently blocked, and the border survives `StaticNavGrid.Clear()`.
  - **Visual stability:** reflex pre-emption doesn't touch visuals. `AgentView` holds displayed states for at least 2 ticks. The anomaly detector measures the rendered state, because thrashing is a visual defect.
  - **Planner:** `GatherSalvage`, `SearchSalvage` and `ScoutSector` set `AtTradeStall = false`.
  - **Provisional economy:** a `ProvisionalCaravanExport` faucet. It is explicitly not the §3.3 economy model, which the producer has deferred.
- **Verification:** `NavigationClearanceValidator` (6 tests), `SupplyLoopValidator` Tests 4b and 6. PlayMode 14/14 in 3 consecutive runs, with zero validator assertion failures at boot.
- **Recorded deviation:** §1A.2 #15. Cognition writes plan and intent bookkeeping off-thread.

## Architectural Decision: Seeded Determinism (Milestone 1 closure, Phase 2, 2026-10-10)
- **Problem:** §1A.1 requires seeded randomness and deterministic ordering, but simulation decisions used unseeded thread-static `System.Random`. Entity ids came from `GUID.Generate()`, and intents were applied in thread-pool completion order and executed in `Dictionary` order (§1A.2 #5, #6, #14).
- **Decision:**
  - **`SimRandom`** is a stateless SplitMix64 stream derived from (seed, entity, tick, salt), rather than a stateful shared RNG. Cognition runs off-thread in arbitrary order, and a stream that depends only on its inputs needs no locking or `[ThreadStatic]`.
  - **One salt per decision** (`SimRandomSalt`), so independent decisions by the same agent in the same tick don't share draws.
  - **`SimulationSettings`** carries the seed (`SIMPIENS_SEED` / `-simseed`, default 20261009) and is logged at boot.
  - **`EntityIdAllocator`** issues seeded ids and resets on respawn. Ids are inputs to tie-breaks, hashes and RNG keys, so they are part of the deterministic state.
  - **Ordering in `SimulationManager`:** each tick's arrivals are applied in agent-id order with a stable insertion sort, which is allocation-free and keeps per-agent arrival order. Active intents run from a sorted id list. Small per-tick buffers are sorted.
- **Not addressed:** the tick on which an async cognition result arrives (§1A.2 #6 remainder), and `foreach` over memory maps inside GOAP actions.
- **Verification:** `DeterminismValidator`, 4 tests including a mutation check of the ordering test. PlayMode 16/16.

## Architectural Decision: Personality & Utility Considerations (Milestone 1 closure, Phase 4, 2026-10-11)
- **Problem:** every agent ran identical utility curves, so the price lever was effectively binary (review §3.2), and bounty choice was "lowest id" (review §2.7).
- **Decision:**
  - **Representation:** five traits stored as signed deviations, so `default` is neutral.
  - **Considerations layer:** a static, allocation-free `Considerations` folds traits into goal priorities, a ration reservation price, a minimum sale price and a bounty score. Every trait enters as `1 + trait × strength`, which preserves the invariant that **a neutral personality changes nothing**. Existing validators and demos therefore stay valid, and variety comes only from non-neutral agents.
  - **Who gets personalities:** crawlers get seeded personalities; scripted pen agents stay neutral (producer).
  - **GOAP fact budget:** untouched. Personality acts through priorities and selection, not facts.
  - **"Why?" groundwork:** `DecisionSummary` is carried on the intent and copied to `AgentState` on the main thread.
- **Verification:** `PersonalityValidator` (5 tests). Live `PriceResponseScenario`: buyers per price step 6/5/5/1/1/0 for 10–60g.
- **Open:** trait strengths are provisional and should be tuned alongside the deferred §3.3 economy decisions. Risk tolerance is unused until Epic 10.

## Architectural Decision: Test Assembly Separation (Milestone 1 closure, Phase 5, 2026-10-11)
- **Problem:**
  - The production GOAP domain and session lived in `Testing/SwarmSpawner`.
  - About 33 validators ran as `IStartable`s on every boot; 21 of them only there, where failures were just logged.
  - Test code shipped in the single `Simpiens` assembly.
- **Decision:**
  - `OutpostGoapDomain` holds the production action graph and goals. `SwarmSpawner` moves to `Core/Session` as the live session.
  - Separate `Simpiens.Testing` assembly constrained to `UNITY_INCLUDE_TESTS`.
  - Production exposes `BootstrapExtensions` (installer list). The test assembly registers its harness services from a `RuntimeInitializeOnLoadMethod`, so the dependency points one way only.
  - Validators are PlayMode tests only.
  - `InternalsVisibleTo` for the test assemblies.
  - Unused cognition stub deleted.
- **Verification:** PlayMode 40/40. A player build was not run.


## Architectural Decision: Epic MAP Revision 2, Map as Data and Pens as Canaries (2026-10-11)
- **Problem:** Revision 1 drew rooms to house legacy mechanics. It built walls as about 300 physics GameObjects with random ids, kept geography in three hard-coded places, put sectors in the UI layer, and derived world bounds from the camera. It was never wired in.
- **Decision (producer-approved):**
  - One plain-data `MapDefinition` per layout drives the nav grid, sectors, POIs, spawns and scenarios.
  - Walls are written into `StaticNavGrid` as a base layer and drawn by a view.
  - Sectors move to a simulation-owned `ISectorRegistry`.
  - World bounds come from the definition.
  - The pen arena becomes a "Dev Map" session reachable by a toggle button from the test harness; the live game boots into the outpost floor.
  - Sector count is data (producer: unimportant).
  - Drifter churn is deferred to the §3.3 economy decision.
- **Consequences:** `OutpostMapBuilder` is retired. Removing wall entities shifts seeded entity ids, so seeded outcomes are re-verified rather than compared to old runs.

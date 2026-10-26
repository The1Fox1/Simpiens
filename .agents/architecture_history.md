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

## Architectural Decision: Epic MAP Phase 1, the World as Data (2026-10-11)
- **Decision:**
  - Static geography lives in `Simulation/World`.
    - An immutable `MapDefinition` holds walls as rects, sectors plus a fallback, keyed nodes, POIs and labels.
    - `IWorldMap.Load` is the only world-building entry point.
  - Walls go into a `StaticNavGrid` **base layer** that survives the dynamic-wall `Clear()`. Dynamic walls (`EdgeCaseInjector`) keep the registry rasterization path.
  - **Rasterization constant:** wall runs have half-extent 0.55 and are inflated by `PawnBodyRadius` (0.25). This is cell-for-cell identical to the legacy per-cell wall entities, proven over the whole Dev Map grid.
  - **Grid origin stays centred.** The partition covers the bounds symmetrically, with an even cell count so cell edges stay on whole units. A non-centred origin was rejected: it touches every cell computation.
  - **Partition re-sizing (`ISpatialPartition.Configure`):** a resize builds a new grid. Snapshots with a different `StaticGrid` are dropped on release and skipped on dequeue. Background readers keep their consistent old world. This is groundwork for the Phase 2 map switch.
  - **Sectors:** `ISectorRegistry` (simulation) owns them and their exploration state, reset per load. `FacilityZoneService` is a UI adapter.
  - **Spawn ordering:** world bounds come from the map, never the camera (the old grid size depended on screen aspect). The spawner loads the map before spawning (fixes the latent spawn-before-partition-init ordering).
- **Consequences:**
  - About 156 fewer GameObjects with physics.
  - Walls no longer consume entity ids, so all seeded ids shift (accepted; outcomes are re-verified, not padded).
  - The anomaly detector checks map wall geometry.
- **Verification:**
  - Map, world, parity and zone validators pass outside Unity.
  - PlayMode was pending at the time of writing.
- **Verification (follow-up, 2026-10-11):**
  - PlayMode 42/42 in 3 consecutive runs.
  - The live supply loop passes with 0 anomalies, with identical milestone ticks in every run.
  - The seeded entity-id shift caused no regressions.

## Architectural Decision: Epic MAP Phase 2, Session Split (2026-10-11)
- **Decision:**
  - A session is an `ISessionDefinition`: a `MapDefinition` plus `Populate(SessionBuilder)` for agents, relationships, groups, stalls and seeded bounties. `SwarmSpawner` builds any session: it loads the map, spawns the map's nodes and hearths, calls `Populate`, records the ledger baseline, then registers the staged seed bounties (baseline before escrow, as before).
  - Sessions are injected as `IReadOnlyList<ISessionDefinition>`. **The default is chosen by id (`OutpostSession`), never by registration order**, so boot can never pick a test session.
  - The live session is `OutpostSession` on `OutpostCommonsLayout` (an interim yard; Floor 1 replaces it in Phase 3). The pens are `CanaryPenSession` + `DevMapLayout` in `Simpiens.Testing`, registered by `TestHarnessInstaller`.
  - **Respawn rewinds the clock to tick 0 and clears the chronicle.** Seeded expiries (bounties t6000, directives t5000) were absolute and expired after ~10 minutes of uptime; seeded randomness is keyed on the tick, so outcomes depended on uptime and PlayMode test order; chronicle events outlived the GUIDs the reset allocator reissues.
  - **The session switch is generic production UI**, shown only when more than one session is registered (only the test harness adds one). This differs from the epic's wording (the harness installs the button); the effect is the same and test code does not reach into production UXML.
  - **Hearths and stalls block only the grid cell holding their centre.** They are listed in every cell their radius overlaps; treating all of those as blocked let a hearth on a cell corner seal a 2-cell room (Pen 4). Movement already keeps pawns off the prop (`IsPositionBlocked`).
  - Every PlayMode scenario names its session (`ISimulationScenario.SessionId`).
- **Consequences:**
  - Production code holds no pen coordinates, keys, sector ids or agent indices.
  - Seeded ids and milestone ticks shifted twice (clock rewind, then the split); outcomes were re-verified, not compared.
  - The pens finally have assertions (`PenCanaryScenario`); the first run exposed the Pen 4 regression.
- **Verification:** PlayMode 43/43; the split was green 3 runs in a row; `OutpostSupplyLoopScenario` passes with 0 anomalies on the pen-free yard; `SessionSwitchScenario` presses the real button and gets back a world structurally identical to a fresh boot.

## Architectural Decision: Epic MAP Phase 3, the Outpost Floor Goes Live (2026-10-12)
- **Decision:**
  - The live session is `OutpostSession` on `Floor1Layout`: 12 crawlers spawn at the Road Gate and discover the outpost by walking in (no seeded knowledge). The Commons yard is retired from production and kept only as the test fixture `CommonsYardFixture`.
  - **Fog of war** (producer decision D1): nobody knows what is inside an uncharted sector until a scout visits it and successfully reports back.
    - The charted state reaches cognition through the snapshot (`ExploredSectorMask`, written by the main thread each tick). Cognition also reads the immutable `MapDefinition` from the snapshot.
    - Landmarks (resources, hearths, stalls, bunks) inside uncharted sectors are not perceived. A scout claimant keeps what it sees in its own target sector as private survey notes. A successful report charts the sector and publishes the survey to every agent's spatial memory, in entity id order, on the main thread. An abandoned survey charts nothing.
    - A scout's visit means reaching the sector's survey point (within 1.25), not crossing the threshold.
  - **The rest anchor:** bunks are entities with one sleeper each. Occupancy is claimed and validated on the main thread; a claim is dropped every tick unless its holder is still sleeping there. Preferring bunks is a price in `Considerations`, not a rule. Resting in place and collapse remain the fallbacks.
  - **Map-aware searching:** exploration and starvation panic pick the charted sector the agent stood in least recently (`AgentMemory.LastSectorVisit`, copied into the memory snapshot), not a random point a few units away.
  - **Perception while busy:** agents also perceive every 5 ticks while carrying out an intent, on the main thread and never during a background evaluation. Before this they were blind for the whole of a walk.
  - **Bounty reservation:** units a claimed Gather bounty is owed are never sold at the stall.
  - **Crawlers arrive fed** (hunger 20, session data). Prices, stocks and starting gold are unchanged; economy design stays deferred.
  - **An affordable stall ration is known food** for the starvation reflex (`ResourceTargeting.HasStallFood`, cached on `AgentState` at each evaluation), so a starving crawler with the gold buys instead of panicking.
- **Why:** each of these surfaced when a live scenario ran on a walled floor. The open yard had hidden them: short trips, everything in sight.
- **Consequences:**
  - The Dev Map pens start charted (no scouts there).
  - `NavConstants.ArrivalTolerance` (1.0) lets walks end up to a unit short, so precise interactions (bunks) make their own last step.
  - Injected walls block the 3×3 cells around them; tests must drop them clear of pawns.
  - Population cost grows faster than linearly: 24 crawlers run at about 54× real time unthrottled, against 146× for 12. That is input for §3A #8.
- **Verification:**
  - PlayMode 47/47 in 3 consecutive runs, with identical milestones each time.
  - `BunkRestScenario`: 10 distinct sleepers, 0 starving.
  - `OutpostSupplyLoopScenario`: no fog leak, the Gather bounty fulfilled after the charting, 0 anomalies.
  - The economy soak re-ran on Floor 1 (Phase 3 plan §9).

## Architectural Decision: Epic MAP Phase 4, the Player's View (2026-10-12)
- **Decision:**
  - **The map is drawn on a Tilemap** (`MapView`): Floor, Rock and Walls tilemaps built from the `MapDefinition`, aligned with the map's wall cells (whole-number centres on Floor 1, half-number rows on the Dev Map). Each room's floor is a muted tint of its label colour. One `RenderOrder` table orders every layer.
  - **The player's fog is a view of the charted state** (`ShroudView`, producer decision D1 = A). It reads `ISectorRegistry.ExploredMask`, the same state cognition sees, and owns none, so a respawn or a future save/load gives the right fog for free. Props sort below the fog and crawlers above it: the player knows no more about a room than the crawlers do, but can watch a scout walk in.
  - **The camera is plain maths plus a thin input layer:** `CameraRig` (clamp, zoom about the cursor, follow, reframe; headless-testable) and `CameraController` (Input System, unscaled time, ignores pointer input over UI Toolkit elements). Follow moves with the target and closes the remaining gap, so it keeps up at 10×.
  - **Selection is shared view state** (`WorldSelection`, a DI singleton): the map and the dashboard read and write it; nothing enters the simulation. Clicks pick the nearest crawler where it is drawn (ties to the lower id), with no physics raycast.
  - **The HUD is a layer over everything** (producer decision D2): the header and the Bounty and Merchant windows sit in a pass-through `hud-root`, and Tab shows or hides only the three dashboard panels.
  - **Bounty targets are validated by the simulation** (`BountyRules`, in `TryRegisterBounty`, before escrow): an unknown sector, or a Scout bounty on a charted one, is refused. The bounty board uses the same rule to explain the refusal. A Gather bounty may wait on an uncharted sector.
- **Why:** the map gave the fog of war away, the fixed camera made crawlers a few pixels tall, posting anything meant switching to a full-screen dashboard that hid the map, and the pen-era sector slider accepted any id. Validation in the authoritative layer is what a multiplayer server will need; a picker alone is not.
- **Consequences:**
  - The map view is 14 GameObjects for Floor 1 instead of about 270.
  - `IWorldMap` exposes its sector registry; `MapView.Show` takes the registry and the selection.
  - A UXML `display: none` reads as Flex through `style.display` until code sets it; the presenter's `IsShown` falls back to the resolved style. That bug made the Bounties and Merchant buttons need two presses the first time.
  - `NodeController` requires a body and a collider: every entity that uses it must set its own (bunks had silently got a dynamic body that crawlers shoved).
- **Verification:** PlayMode 52/52 in 3 consecutive runs with identical milestones. `MapViewValidator` matches the tiles to the definition and the nav grid; `ShroudViewScenario`, `MapHudScenario` and `PlayerScoutScenario` run on the live session. Supply-loop and bunk milestones are unchanged from Phase 3.

## Process Decision: Completed Plans Leave `.agents/` (2026-10-12)
- **Decision (producer request):** plans in `.agents/*_plan.md` are working documents, kept only while their work is in progress (AGENTS.md, Documentation Protocol #6). Removed: `epic_map_phase1_plan.md` to `epic_map_phase4_plan.md`, `milestone1_closure_plan.md`, `implementation_plan.md` (§3.1), `goap_fixes_plan.md` and `logging_implementation_plan.md`.
- **Where their live content went:**
  - the economy soak data and the deferred §3.3 decisions: `docs/epics/epic-9.md` §8;
  - the open findings from Epic MAP Phases 2–3: `docs/epics/epic-map.md` §6.
- **Earlier entries** that cite a plan section (for example "Phase 3 plan §9") resolve through git: `git show f679a0c:.agents/<plan>.md`. The decisions themselves are already recorded in this log.
- **Why:** finished plans duplicate this log and the epic docs, go stale, and bury the one plan that matters.

## Architectural Decision: Epic MAP Phase 5, the Definition of Done (2026-10-13)
- **Decision:**
  - **What each crawler did is recorded by the simulation** (`AgentState.Activity`, an `AgentActivity` struct): harvests, sales, Gather deliveries, ration purchases, meals, bunk sleeps, hearth gatherings, harvest → sell cycles and the tick each first happened. Each is counted where the act is applied, on the main thread. Diagnostic only: nothing decides on it. A later crawler card (UI-3) can read it.
  - **The jitter check measures sim time** (producer decision D1 = A): it runs once per simulation tick from logical positions, with a window of 5 ticks (0.5 s at play speed) and the same thresholds. It judged 5 sim-seconds at the tests' 10× before, ten times stricter than anything the player sees at 1×, the game's only speed. Jitter fell to 0 in every scenario.
  - **Eating is a typed intent** (`EatCarriedFoodIntent` + handler): the meal applies when the intent succeeds, whoever chose it (the hunger reflex or a plan step), and the starvation reflex counts a carried ration as known food.
  - **Intents carry their session** (`AgentIntent.SessionEpoch`, stamped on the main thread before cognition awaits). A respawn starts a new session and `ProcessQueuedIntents` drops older intents; 0 means unstamped and is accepted.
  - **Determinism is asserted exactly** (producer decision D2 = A, strongest branch): `DeterminismScenario` requires same-seed runs to match `WorldStateHash` at every 100-tick sample to t3,000.
  - **Every economic decision is folded into §3.3** (producer decision 2026-10-13), including trust toward the player. `OutpostLifecycleScenario` reports trust and does not assert it.
- **Why:**
  - Nothing recorded meals or per-crawler sales, so "every crawler works, eats and sleeps" could not be asserted.
  - Traced starvation: a crawler bought a ration and then panicked for about 300 ticks with it in its pocket.
  - The determinism check found that the run after a respawn differed from t100: `Destroy` is deferred to the end of the frame, a cognition task finishing in between still enqueued its intent, and the allocator reissues the same ids per seed, so the new agent executed the dead agent's decision. This also happened at boot and on every Dev Map toggle.
  - Trust fell on the default prices (rations at 15g, gouging above 7.5g; 10 of 12 crawlers ended at −80 to −100). Fixing it means choosing prices or a fairness rule, which is economy design the producer deferred.
- **Deviations from the plan** (`git show 4e83ecd:.agents/epic_map_phase5_plan.md`):
  - The tally lives on `AgentState`, not in a `SimulationManager` dictionary, so each handler records into the agent it already holds. `SimulationManager.BunkSleeps` is kept for the existing scenarios.
  - Criterion 7 (trust rose for at least one crawler) is reported only, by producer decision, instead of asserted.
  - `BunkRestScenario`'s "no crawler starves" is reported only, by producer decision. In the close-out runs it starved one crawler in 3 of 6 close-out runs: a crawler with 10g (rations 15g) whose known Garden plots were all empty and blacklisted panicked away from the Garden while they regrew. This is food pacing, owned by §3.3; `OutpostLifecycleScenario` keeps the assertion and passed 3 of 3.
  - Step 4 found two causes, not navigation: the uneaten ration and the stale intent after a respawn. No navigation change was needed for zero anomalies.
- **Consequences:**
  - Epic MAP is `COMPLETED`. The player-trust half of the Milestone 1 loop is not demonstrated yet; it waits on §3.3 ([Epic 9 §8.4](../docs/epics/epic-9.md)).
  - `BunkRestScenario` still follows different trajectories between runs (first sleep t885 vs t839), while `DeterminismScenario` and `OutpostLifecycleScenario` were identical in all three: cognition timing (§1A.2 #6) still shows up in some scenarios.
  - `DeterminismScenario` rests on cognition results arriving on the same tick in both runs (ROADMAP §1A.2 #6). It may flake on a slower or busier machine; the fix is fixed-latency cognition with the multiplayer work.
  - Gather was fulfilled sooner once crawlers ate what they carried (t568 → t511).
- **Verification:** PlayMode 54/54 in 3 consecutive runs (56 with the 2 explicit runs, skipped), identical milestones (sale t233, scout t356, gather t511, third sleeper t983). `OutpostLifecycleScenario`: 12 of 12 crawlers close 2–18 cycles, 0 anomalies, 0 starving (worst stretch about 230 ticks). `DeterminismScenario`: 3 of 3 runs identical at every sample, and across two Unity processes.

## Architectural Decision: Review §3.3, the Outpost Economy Model (2026-10-14 to 2026-10-15)
- **Context:** the producer went AFK and gave the agent provisional authority to finish §3.3 on `feat/economy-model`, including the design decisions (D1–D7). Every decision is reviewable and the branch is not merged. Pending producer checks: a play session at 1×, and the Epic MAP visual check.
- **Decisions** (D1–D7 as the plan recommended; full model in `docs/economy-model.md`):
  - **One `EconomyTuning` per session** (`ISessionDefinition.Economy`): immutable, applied by `SwarmSpawner` through `ISimulationManager.SetEconomy`, which reconfigures the supply service and publishes the tuning on the world snapshot. Cognition reads it from there (`Considerations.Economy`), with no locks. Garden yields stay map data (`Floor1Layout`), and the hunger rate stays a drive.
  - **D3, fairness by the crawler's own valuation** (`TradeFairness`).
    - A purchase is judged against the buyer's calm valuation, its reservation price at hunger 50: generous at ≤ 0.6×, fair at ≤ 1×, gouged above.
    - A sale is judged against the seller's minimum price: generous at ≥ 3×, fair at ≥ 1.5×, stingy below.
    - Refused trades change nothing.
  - **D1, the caravan buys salvage up to a quota per visit** (`CaravanSalvageSales`; 2 ore at 24g and 2 herbs at 30g). The stall holds at most 20 of each type. The provisional export and its switch are deleted.
  - **D2, outpost upkeep** (`OutpostUpkeep`, 80g per visit, on the caravan's rhythm). A stall that can't pay in full pays what it has and is in arrears (`TradeStallData.InArrears`): it buys no salvage but sells rations until a visit pays in full.
  - **D6, food:**
    - crawlers start with 15g;
    - the Garden regrows in 300 ticks and yields 4 per plot;
    - caravan restocks are partial;
    - a starving crawler that has stood in a charted food sector returns to the nearest plot and waits for regrowth (`ResourceTargeting.TryPickFoodPlotWaypoint`).
  - **D4:** crawlers' willingness to trade is the scarcity lever. **D7:** drifters deferred to Milestone 2.
  - **D5:** the soak plays a fair, a greedy and a generous player (one `UpdateStallPricesCommand` at tick 0) and asserts a band for each.
- **Why:**
  - Before: trust fell for 10 of 12 crawlers at the default prices, because the 7.5g gouging line ignored the crawler's own valuation. The treasury grew without limit, since the provisional export was unbounded. A broke crawler could starve while panicking away from a regrowing Garden.
  - The baseline by player profile (Epic 9 §8.5) showed the old rule rewarding the greedy player and punishing the fair one.
  - The economy is closed: crawlers spend what they earn, so the stall's net income per visit is roughly salvage faucet − upkeep − wholesale. Prices matter mainly through trade volume.
- **Deviations from the plan** (`.agents/economy_model_plan.md`, removed; `git log --diff-filter=D -- .agents/`):
  - Fairness uses the calm valuation and adds a stingy band for sales. The generous bands were narrowed (0.6× and 3×, not 0.75× and 2×) so that default prices read as fair rather than generous.
  - The stall got a salvage capacity, and the caravan keeps no reserve.
  - The quota and upkeep tuning moved ahead of the food step: crawler income, and so food, depends on the quota.
  - D5 bands changed with the evidence. Greedy earns less than fair and is distrusted, rather than earning faster early: a closed economy can't reward greed. A bankrupt generous outpost may go hungry, so generous starvation isn't asserted.
  - Partial restocks were added, and food got one behaviour fix, on evidence. With the data changes alone, the lifecycle still starved a broke crawler.
- **Consequences:**
  - Epic 9 and Epic Test-2 are `COMPLETED`, pending the producer's play session. Milestone 1 now waits only on `MemoryPack`.
  - `OutpostLifecycleScenario` asserts trust again (⅔ of crawlers), and `BunkRestScenario` asserts "no crawler starves" again.
  - The soak test is now `Scenario_EconomySoak_PaceBands`; `SimulationSettings.ProvisionalExportEnabled` and `SIMPIENS_PROVISIONAL_EXPORT` are gone.
  - `DeterminismScenario` is timing-sensitive (ROADMAP §1A.2 #6, measured 2026-10-15): it diverges at t100 whenever it runs alone (cold start), on the pre-§3.3 code as well, and diverged once in 3 full-suite runs.
- **Verification:**
  - Each step was committed with its measured test status.
  - Final code: PlayMode 55/55 (57 total, 2 explicit skipped) in 3 consecutive runs, with identical milestones (first cycle 235, scout 382, gather 605, third sleeper 865). The lifecycle had trust up for 10 of 12 and 0 starving.
  - Soak 9/9 with bands, all within band:
    - fair 546–771g, trust up 9–10, 0 starving;
    - greedy trust up 0, mean −3 to −17;
    - generous broke by minute 10, peak trust up 12, 0 starving.

## Producer Review: §3.3 Accepted (2026-10-15)
- **Context:** §3.3 was decided under provisional authority (previous entry), with two producer checks pending.
- **Outcome:**
  - the producer reviewed the branch and merged it as PR #3 (merge commit `201b96b`);
  - the play session in the Unity editor passed;
  - the Epic MAP visual check at 1× passed. It covered the small steps at the stall and the bunks that Phase 5 left for the producer's eye (Epic MAP §6 #1).
- **Consequences:**
  - D1–D7 stand as the producer's model.
  - No producer check is pending on §3.3 or Epic MAP.
  - Milestone 1 waits only on `MemoryPack`.

## Milestone 1 Completed: Whole-Session Save and Load (2026-10-15)
- **Context:** Milestone 1's last clause was "Integrates MemoryPack serialization for unmanaged inventory/contracts". The producer approved the completion plan on 2026-10-15 with D1–D6 as recommended (`.agents/milestone1_completion_plan.md`, removed; `git log --diff-filter=D -- .agents/milestone1_completion_plan.md`).
- **Decisions (producer, 2026-10-15):**
  - **D1 = B:** read the clause as a whole-session save and load; the economy's data is its first part.
  - **D2 = A:** one quick-save slot, with Save and Load in the dashboard header.
  - **D3 = A:** work in progress isn't saved. Intents, plans, and the bunk claims and contention tied to them start empty; crawlers decide afresh on the next tick. Bounty claims are durable state and are saved.
  - **D4 = A:** a save with another format version, session or map is refused with a reason; no migration. The seed is checked as well, since entity ids and randomness derive from it.
  - **D5 = A:** `EconomyZeroAllocValidator` is in scope.
  - **D6:** (a) the dossier's *Feed ration* button is removed; (b) stale docs refreshed (ROADMAP's Milestone 1 status and closure-plan line, the design proposal's 12-sector MVP line).
- **Design:**
  - MemoryPack 1.21.4 is vendored next to ZLogger from one dependency closure; only `SaveSerializer` calls it. Unmanaged records are raw memory copies, so a saved struct's layout is the format (`SessionSaveHeader.CurrentFormatVersion`; the economy records' sizes are pinned).
  - `SessionSave` holds a header and one block per owner; each owner captures and restores its own part. Keyed records are sorted by id, so a capture doesn't depend on dictionary order, and a restored session captures to the same bytes.
  - A load rebuilds the map exactly as a respawn does (the map's entities get the same ids), then restores in place of `Populate`; the clock and the id counter resume at the save.
  - `SaveFile` puts a plain header (format, length, checksum) in front, so bad files are refused before MemoryPack reads them. `SaveGameService` is the slot; it writes off the main thread through a temporary file.
- **Deviations from the plan:**
  - `SaveLoadScenario` saves when a crawler holds the seeded Gather bounty (about t400), not at t3,000: the seeded bounties finish by about t605, so a later save would resume no work in progress. Saving mid-bounty proves a claim survives a load.
  - The plan's F7 note on positions was corrected: the copy of `transform.position` into `AgentState.Position` is a latent breach (ROADMAP §1A.2 #16; the architecture scan's M5), because crawler views never get the clock and so never interpolate.
  - `ResetState` now drops queued player commands, for respawns too: nothing carries across a session boundary.
  - The service gained a synchronous `QuickSave` beside `QuickSaveAsync`: a plain test can't wait on an async save that resumes on the frame loop, and a quit-time save needs one.
- **Consequences:**
  - Milestone 1 is `COMPLETED`, pending the producer's check of save and load at 1×.
  - New authoritative state must be added to its owner's save block (AGENTS.md, `Persistence/`); a round trip can't catch state left out of the save.
  - A loaded game doesn't replay the original's future exactly: cognition timing (§1A.2 #6), rebuilt dictionaries, and the world registry's order (a regrown node re-registers at the end in the original, at its spawn place after a load). Tests assert identical state at the load, not identical futures.
  - Findings moved out of the plan: the latent position breach (ROADMAP §1A.2 #16); `GroupRegistry.CreateGroup` keeps a new leader on their old roster (Epic 8 §11); the *Calm Frustration* button has the same breach as *Feed ration* (Epic MAP §6 #10, a producer decision); `DeterminismScenario` diverged once more in a full suite (ROADMAP §1A.2 #6).
- **Verification:**
  - Each step was committed with its measured test status: step 0 `2bf65fe`, step 1 `27e4967`, step 2 `0fc3d4f`, step 3 `82615b9`, step 4 `640b90a`, step 5 `59478a6`, step 6 `720095b`.
  - `SaveLoadScenario` passed in 3 of 3 consecutive full-suite runs (PlayMode 64/64 each), with identical results every time: saved at t394 with the Gather bounty claimed, delivered at t647 after the load, 32 cycles, 12/12 crawlers ate, 0 ledger violations, 0 anomalies. Final suite: 65/65 (67 total, 2 explicit skipped).
  - The live outpost round-trips byte for byte at t600 (39,444 bytes); the Linux player saved and reloaded it at t25 (18,603 bytes, `scripts/run-persistence-smoke.sh`).
  - 1,000 ticks of trading and bounties and 1,000 rounds of off-thread trade and bounty evaluation allocate 0 bytes.
  - Pending: the producer's check of save and load at 1×.

## Epic CORE Started: Engine-Free Core and Headless .NET (2026-10-17)
- **Context:** the 2026-10-15 migration write-up ("Simpiens: Migrating from Unity to Godot on .NET 10", Claude Docs) proposed five phases. Phases 0 (decouple inside Unity) and 1 (headless .NET 10) change no engine and pay off on any engine.
- **Decisions (producer, 2026-10-17, the write-up's checklist):**
  - Phases 0–1 approved as standalone work; the agent sees them through under provisional authority. Godot is decided only after the Phase 2 spike, which is outside this epic.
  - No web build. `Microsoft.Extensions.DependencyInjection` for .NET hosts; xUnit v3 for .NET tests.
  - ROADMAP §2: retire the VContainer, UniTask and `Unity.Collections` + Burst rows; lift the MCP SDK rejection for Epic 11 once the simulation runs on .NET.
- **Design choices (agent, under that authority; reasons in `docs/epics/epic-core.md` §2):**
  - Cognition results land on a fixed tick (dispatched at T, applied at T + 1 behind a barrier, in agent-id order) and are applied as data on the main thread. This closes §1A.2 #6 and #15.
  - Golden world-hash baselines in the repo gate every mechanical chunk; behaviour changes re-record them in their own commit.
  - `EntityId` stays bit-compatible with `UnityEngine.GUID` (128 bits) so the swap changes no behaviour; a 64-bit id is deferred to the multiplayer wire format.
  - Engine-free maths (`Vec2`, `GridPoint`, `RectF`, `Rgba`, `SimMath`) copy Unity's formulas exactly, rather than `System.Numerics`, whose scalar division and exact equality would change results.
  - The core becomes `Assets/Simpiens.Core/` (asmdef `noEngineReferences: true`); the .NET project compiles the same files at C# 10 for `netstandard2.1;net10.0`.
- **Recorded, not scheduled:** the 2026-10-15 architecture scan's H1 (no `try`/`catch` on the tick path) and M4 (bounty ids) became §1A.2 #17 and #19; M3 became #18 and H2 joined #15 (both fixed by chunk C1); M5 was already #16.
- **Plan:** `.agents/godot_phase01_plan.md`, chunks C0–C11, on branch `feat/godot-migration`.

## Cognition on Fixed Ticks (Epic CORE C1, 2026-10-17)
- **Context:** the tick a cognition decision landed on depended on thread timing (ROADMAP §1A.2 #6), and the evaluation wrote plan and intent state off the main thread (#15), including a real race with `AgentState.Tick` (architecture scan H2). `DeterminismScenario` held only warm, inside the full suite.
- **Decision:** a `CognitionScheduler` owned by `SimulationManager`. The main thread prepares a due agent as before (now inside a guarded block, #18); the evaluation runs on the thread pool and returns a `CognitionResult` (intent, plan to abort or adopt, goal); at the start of tick T + 1 the main thread waits for every evaluation dispatched at T and applies them in agent-id order, before intents are registered. A respawn or load waits for and drops decisions in flight. The cognition path moved from UniTask to `ValueTask`, and the pathfinder plans on the calling worker instead of hopping threads.
- **Why one tick:** on a quiet machine results already landed at T + 1, so the outpost's behaviour is unchanged: the same `DeterminismScenario` milestones (first cycle 235, scout 382, gather 605, third sleeper 865) and the same soak numbers as §3.3 (fair 546–771g at 30 minutes).
- **Verification:** `CognitionSchedulerValidator` (decision lands at T + 1 with a 0 ms or 120 ms evaluation; agent-id order whatever the completion order; a failed preparation or evaluation never freezes an agent; a respawn drops decisions in flight). `DeterminismScenario` alone identical in 3 of 3 runs (it diverged at t100 in 5 of 5 cold runs before). PlayMode 66/66 (68, 2 explicit skipped); economy soak 9/9 within its pace bands.
- **Consequence:** a run is now reproducible cold, warm and across processes, which the golden world-hash baselines (C2) rely on.

## Entities as Data (Epic CORE C5a, 2026-10-17)
- **Context:** the simulation and its 12 intent handlers moved agents through `NodeController` (a MonoBehaviour), and `AutonomousAgent.ManualTick` (a MonoBehaviour) ran each crawler's tick and copied the rendered transform into `AgentState.Position` (ROADMAP §1A.2 #16).
- **Decision:**
  - `WorldEntity`, a plain-data record (id, kind, position, previous position, radius, speed, resource kind, and the `AgentState` of a crawler), is what the world registry, `SimulationManager`, the handlers, the execution context and the spatial grid work with. A crawler's position *is* its `AgentState.Position`.
  - The registry raises `EntityRegistered`/`EntityUnregistered`; views show and hide with them (a dormant resource node used to be hidden by the simulation calling `SetActive`).
  - `AgentRuntime` (engine-free) is a crawler's tick; `AutonomousAgent` and `NodeController` are views bound to their records and never write back. `NodeFactory` (dead code) is deleted.
- **Why the crawler tick moved now, not in C5b as planned:** once positions live in `AgentState`, the old per-tick copy from the transform would have reverted every move with last frame's position.
- **Kept on purpose:** a crawler view's destruction still stops its crawler (`OnDestroy` disposes the runtime), as before; C5b's session host makes the session own both.
- **Test harness:** the 18 validators that built GameObjects to set up a test build records (`HeadlessEntities`, `TestCrawler`): a prerequisite for running them under .NET. Defaults reproduce what the GameObjects gave the simulation (radius 0.5, the default circle collider; speed 2). The anomaly detector checks injected walls by simulation geometry instead of a physics overlap, and finds a crawler's view through `NodeController.ViewOf(id)`.
- **Verification:** golden world hashes identical; `DeterminismScenario` milestones unchanged; PlayMode 68/68.

## Epic CORE Completed: Engine-Free Core and Headless .NET (C2–C11, 2026-10-17)
- **Context:** Phases 0 (decouple inside Unity) and 1 (headless .NET 10) of the migration write-up, delivered chunk by chunk on `feat/godot-migration` under the agent's provisional authority (Epic CORE Started, above). C1 and C5a have their own entries.
- **Decisions (agent):**
  - **C2, golden hashes:** `Baselines/golden-world-hash.txt` holds 31 `WorldStateHash` samples (t0 to t3,000) of the outpost for 3 seeds, with a readable line per crawler for diagnosis. Every later chunk had to reproduce it; a deliberate behaviour change re-records it in its own commit (none was needed).
  - **C3, `EntityId`:** bit-compatible with `UnityEngine.GUID` (layout, order, hash code, text), so ids, `SimRandom` streams and saves didn't change. It lives in the root namespace `Simpiens` because Unity 6.4 has its own `UnityEngine.EntityId`. A 64-bit id is deferred to the multiplayer wire format.
  - **C4, maths:** `Vec2`, `GridPoint`, `RectF`, `Rgba` and `SimMath` copy Unity's formulas (approximate `==`, `Normalized` zero below 1e-5, round half to even), not `System.Numerics`, whose scalar division and exact equality change results.
  - **C5b, the session host:** `SessionHost` (engine-free) builds, populates, ticks, captures and restores the session; `SwarmSpawner` only draws it. Body radii became constants (crawler 0.35, resource 0.4, hearth 0.35, bunk 0.5).
  - **C6, infrastructure:** the clock advances by the host's elapsed time; the host plugs `SimLog`'s console sink in; validators log through `TestLog` and throw, so NUnit and xUnit both run them.
  - **C7, the assembly:** `Assets/Simpiens.Core/` with `noEngineReferences: true`; the engine-free harness is `Simpiens.Testing.Core`. Namespaces unchanged.
  - **C8, .NET:** `dotnet/` compiles the same files by glob, `netstandard2.1` at C# 10 (Unity's level) and `net10.0` at the latest C# (MemoryPack's generator needs C# 11 there).
  - **C9, tests:** xUnit v3, tests in sequence. The DoD scenarios run headless without the visual anomaly detector, which needs the views. The .NET core is always built optimized, so the zero-allocation validators check shipping code.
  - **C10, the console host:** `simpiens` on the Generic Host. It references the test harness (golden hashes), so it is a development tool that never ships. `EconomySoak` and `OutpostWatch` live in `Simpiens.Hosting`, shared by the host and the tests.
  - **C11, CI:** a GitHub Actions workflow for the .NET solution; added, not pushed.
- **Deviations from the plan:**
  - C5 became C5a and C5b (no C5c); the crawler tick moved in C5a (see its entry).
  - The plan put the Phase 1 gate at the end of C10; the tests that make it up landed in C9, and it was met there. C10 added the soak and the seed sweep.
  - C9 changed core code that the port was meant to leave alone: `BeginEvaluation`'s thread-pool lambda allocated its closure on every call, early returns included (176 bytes a tick for an idle crawler, invisible to Mono's allocation counter). Behaviour-neutral: golden hashes identical.
  - The C7 gate's persistence smoke first failed to start (the scripts had lost their executable bit, `8e2898a`); rerun, it passed (t25, 18,603 bytes, as in Milestone 1).
- **Findings** (epic CORE §6): **F1**, Unity's Mono keeps scalar float intermediates at double precision while CoreCLR rounds each step, so the runtimes agree on the golden seeds but not on every seed (proven on `TryPickSectorWaypoint`); recorded, not fixed. **F2**, the outpost's day passes on 16 of 23 seeds (starvation on 5, no cycle on 2; Unity agrees on seed 11); a pacing decision for the producer. ROADMAP §1A.2 #17 and #19 (scan H1, M4) stay recorded, untouched.
- **Verification:** one commit per chunk with its measured status: C0 `7bac9ac`, C1 `cc996f2`, C2 `78c10b4`, C3 `ed62d1b`, C4 `2144530`, C5a `e06e283`, C5b `236412d`, C6 `3a9f97b`, C7 `79391ba` (+ `8e2898a`), C8 `f7cc053`, C9 `a1307fd`, C10 `72c16a2`, C11 the close-out commit. Final: PlayMode 68/68 (70, 2 explicit skipped); Unity golden check 3/3 seeds; `dotnet test` 62/62; the economy soak 9/9 within its bands in both runtimes.
- **Consequences:**
  - Unity is still the game. Engine-free code must stay at C# 10 and netstandard2.1 APIs, and a core change is checked in both runtimes (AGENTS.md, *Checking a change*).
  - Compare .NET runs with .NET runs (F1). After a cutover, CoreCLR becomes the reference and its own baseline is recorded.
  - The Godot spike (Phase 2) waits on the producer's go/no-go. The soak and scaling work (§3A #8) can now run headless, in seconds.
  - The working plan `.agents/godot_phase01_plan.md` was deleted at close-out; its findings are in the epic's §6 and its soak data in `docs/economy-model.md` §7.

## Epic CLIENT M0: First Pixels on KNI (2026-10-18)
- **Context:** the producer chose KNI for the game client (engine proposal, `docs/engine-proposal-kni.md`) and gave the agent provisional authority over M0 and M1, with an implementation plan before each. M0's plan was `.agents/client_m0_plan.md`, deleted at this close-out.
- **Decisions (agent):**
  - **Two projects, not one:** `Simpiens.Presentation` holds everything that turns state into something drawable, with no XNA reference, so it is tested without a GPU. `Simpiens.Client` holds the XNA game and renderer. (The proposal had a single `Simpiens.Client`.)
  - **The framework switch:** the MSBuild property `XnaFramework` (`Kni` by default, `MonoGame` compile-only), with separate `bin/` and `obj/` folders for MonoGame so the two restores never collide. `XnaPlatform` is the one class allowed to differ.
  - **Headless rendering in tests:**
    - SDL's offscreen driver, the system SDL2 (the test project excludes KNI's bundled copy, which can't create a GL context offscreen), and Mesa's software renderer pinned.
    - The settings are set with libc `setenv`, because .NET's managed environment never reaches `getenv` on Unix.
    - Games run in-process with `RunOneFrame` and are never disposed, because disposing segfaults offscreen.
  - **The gate inside the client:** the golden world hashes are sampled while the real `SimpiensGame` runs frame by frame (100 ticks a frame), so the client's own tick path is what's checked. A second test feeds uneven real-time frames.
  - **Art and pixels:** Unity's `AgentSpriteLibrary` was ported by a script that only swapped types (a normalised diff proves the patterns and palettes are identical). Animation frames come from the tick, not wall-clock time. The camera snaps to whole pixels.
  - **Our own PNG writer and reader** (zlib from .NET): render references are plain files an agent can look at. The tolerance is 0.1% of pixels by more than 2 per channel.
- **Verification:** M0.1 `ba1f17c`, M0.2 `27c48be`, M0.3 `79fa223`, M0.4 `cf88e89`, M0.5 the close-out commit, each with build, `dotnet test`, golden check and the MonoGame compile passing. Final: `dotnet test` 83/83 (client 21, core 62); golden check 3/3; Unity untouched.
- **Consequences:**
  - Unity's views are frozen; new view work goes into `dotnet/`.
  - CI's client step (Xvfb, Ubuntu's SDL 2.30) runs for the first time on the first push.
  - The desktop exe hasn't been opened in a window by the agent, only run offscreen.

## Epic CLIENT M1: Top-Down Parity on KNI (2026-10-18)
- **Context:** the second half of the agent's provisional authority over Epic CLIENT. The goal was for the KNI client to show and play what Unity's 2D view did. M1's plan was `.agents/client_m1_plan.md`, deleted at this close-out. Unity's frozen views were the source of truth: `AgentView`, `CampfireView`, `MapView`, `ShroudView`, `CameraRig`, `CameraController`, `WorldPicker` and `WorldSelection`.
- **Decisions (agent):**
  - **Ports, rule for rule, into `Simpiens.Presentation`:** crawler appearance (state overrides, the 3-tick dwell, micro-animation), the fog overlays and their fade, the camera rig and its controls, and picking and selection. Each is plain C# tested without a GPU; `CameraRigValidator`'s checks were ported. `Simpiens.Client` only reads devices into snapshots and draws.
  - **A view clock:** real seconds in the game, as Unity's unscaled time; tick × 0.1 s in tests, so a render at a given tick is always the same.
  - **Draw sheets:** sprites, the font and the fog cloud. The renderer starts a batch whenever the sheet changes, each with its own sampler (point, or linear for the fog). The sort key still decides the order.
  - **A code-generated 5×7 pixel font** for room names, "UNCHARTED" and crawler names. It needs no content pipeline and is replaced by Gum's fonts in M2. Because it is taller than Unity's text, "UNCHARTED" sits 1.125 units under the room name instead of 0.9.
  - **Whole-pixel framing,** an addition to Unity's rig. Given the window's height, reframing and the zoom-out limit show the map at a whole number of screen pixels per art pixel. Without a pixel height, the rig is Unity's exactly.
  - **The selection ring** is generated as Unity's was. It is the 34th sprite. The 33 ported sprites keep their own pin, and the whole set has a second pin.
  - **A respawn or a new map clears the selection,** because the selected crawler no longer exists.
  - **Android, compile-only:** `dotnet/Simpiens.Android` sits outside `Simpiens.slnx`.
    - It is built locally with a user-local toolchain under `~/.cache/simpiens-toolchains` (scripts, no sudo), and in a CI job.
    - It is portrait, as the producer decided.
    - Its application id `dev.simpiens.game` is a placeholder until the producer picks one.
  - **Shared console log sink:** the `SimLog` console sink moved from `simpiens` into `Simpiens.Hosting`, and the desktop exe installs it (warnings and up unless `SIMPIENS_LOG` is set).
- **Deviations from the plan:**
  - The plan's logging item (the desktop exe's console sink) landed at M1.7, not with the camera.
  - M1.7 added a direct test of KNI's mouse and keyboard reads, because an offscreen game never has focus and so never reads them (epic F3).
  - The gate's "screenshots reviewed beside Unity's" was split. The agent reviewed every render against the ported logic; the producer's look beside Unity's own screen is open and person-gated.
  - Not in M1, as planned: position interpolation (Unity had none), sound, touch (M4), and the dashboard (M2).
- **Verification:** one commit per chunk with its measured status:
  - plan `f83a948`, M1.1 `0dbfaf9`, M1.2 `981e5aa`, M1.3 `fa789fc`, M1.4 `510d31a`, M1.5 `989f2cd`, M1.6 `adcde54`, M1.7 the close-out commit;
  - every render reference was looked at before it was recorded.

  Final:
  - `dotnet test` 111/111 (client 49, core 62); golden check 3/3;
  - the MonoGame and Android builds compile; Unity untouched;
  - frame time 1.2 ms on llvmpipe and 0.7 ms on a desktop GPU (epic F2).
- **Consequences:**
  - The client draws everything Unity's 2D view did. M2 (the dashboard in Gum, then Unity retired) is the producer's call.
  - CI's client and Android jobs run for the first time on the first push.
  - Explicit registration of KNI's platform factories (epic F4) waits for M4's trimming work.

## Epic CLIENT M2, part 1: the dashboard in Gum, parity with Unity (2026-10-18)
- **Context:** the producer said go for M2 with two pull requests: parity first, then Unity's removal. The plan's defaults were approved. The plan `.agents/client_m2_plan.md` stays until M2 closes; this entry covers M2.0–M2.7, the first pull request.
- **Decisions (agent):**
  - **Gum's built-in visuals, not the Meadow theme** (D1, a deviation from the proposal).
    - Meadow needs HiDef and a pinned Apos.Shapes 0.8.1; 0.8.6 breaks Gum.Shapes at start-up.
    - Meadow also adds FreeType natives, and its look is further from Unity's.
    - The built-in visuals take Unity's palette through `Styling.ActiveStyle.Colors`, and its 11–14 px text through a `FontScale` on the three text styles.
  - **The dashboard as data in `Simpiens.Presentation/Dashboard`,** with Gum views in `Simpiens.Client/Ui` that decide nothing. The player's actions leave only as typed commands.
  - **Unity's dossier calm became a core command,** `CalmCrawlerCommand`. The client never writes simulation state. It was checked in both runtimes.
  - **Gum keeps static state,** so the game that started it last owns it. Tests run many games in one process.
  - **Tests drive Gum's own cursor from a script,** through its internal per-frame method, and make the offscreen game active through KNI's private flag. Both are test-only and fail loudly if moved.
  - **With the tick-based view clock** (tests), the camera steps in view time and the dashboard refreshes every frame. The game uses real time and refreshes at 10 Hz.
  - **Parity details kept:** the game starts on the panels; one window at a time; Esc closes a window before it stops the camera.
  - **Changed for fit or clarity:**
    - The metric pills follow the status in the header, and the save message sits at the bottom left, so 1280 px holds everything.
    - The player shows as "You (the outpost)" in affinities.
    - The Dev Map's labels shrink to fit their row.
  - **The Dev Map only in Debug desktop builds,** through `ClientOptions.ConfigureServices`.
- **Deviations from the plan:**
  - M2.1 named the command `InfluenceType.CalmCrawler`; the core's enum is `InfluenceCommandType`.
  - The negative frame time (epic F5) and the paused-tick quirk (F6) were found and handled along the way.
  - An executable-bit fix for M1.6's scripts landed in its own commit (`3082ab8`).
- **Verification:** one commit per chunk with its measured status:
  - decisions `3223168`, M2.0 `9abd6c1`, M2.1 `7f99875`, M2.2 `81b2604`, M2.3 `4dc8aa1`, M2.4 `569547c`, M2.5 `3e13024`, M2.6 `8e045df`, M2.7 `dd5e1fc`, and the close-out commit;
  - every new or changed render reference was looked at before it was recorded.

  Final:
  - `dotnet test` 147/147 (client 84, core 63); golden 3/3;
  - the MonoGame, Debug desktop and Android builds compile;
  - Unity: PlayMode 69/69 and golden 3/3 on M2.2's core change.
- **Consequences:**
  - The client plays the whole game Unity plays. The first pull request waits for the producer's side-by-side.
  - M2.8 (Unity-only coverage moved to .NET) and M2.9 (Unity removed) form the second pull request.
  - Narrow windows wait for M6.

## Epic CLIENT text cleanup: baked fonts, drawn 1:1 (2026-10-18)
- **Context:** the producer found the client's text rough (blurry, letters not lining up) and asked for a cleanup before M3. They made `main-kni` the acting main until M4 and approved Inter with smooth map labels. The plan was `.agents/client_text_plan.md`, deleted at this close-out.
- **Findings:**
  - The UI scaled Gum's 18 px Arial bitmap, and Gum never snaps scaled text to pixels (epic F8).
  - The map's code-made 5×7 pixel font broke off the whole-pixel scale and had no descenders.
- **Decisions (agent, producer-approved where noted):**
  - **One text pipeline.** TrueType fonts are baked offline into BMFont atlases at exact pixel sizes, committed, and drawn 1:1. There is no `FontScale` and no rasterizer at run time.
  - **Inter (OFL)** in Regular and Bold, 15 faces (producer).
  - **The baker** (`Simpiens.FontBaker`) uses KernSmith with FreeType through Gum's own `BmfcSave` and `GumFontGenerator`, so the names and metrics are Gum's. A test compares the committed atlases with a fresh bake byte for byte, as with the golden hashes.
  - **The UI:** `UiText` styles in Unity's sizes. Font properties are set together under suspended layout. Gum's default font is Inter, and its property errors are logged.
  - **The map:** text moved to screen space (producer: smooth labels). Labels hang on world points, sized by the zoom in baked steps, a size per row so that none overlaps, with a 1 px shadow. `PixelFont`, `WorldText` and `FitLabels` are removed.
  - The client reads fonts through `TitleContainer`, so the same code reads Android's assets.
- **Verification:**
  - one commit per chunk: plan `f493515`, T1 `580740f`, T2 `04e8a09`, T3 `e6483e4`, and the close-out commit;
  - every re-recorded reference was looked at, the text magnified.

  Final:
  - `dotnet test` 173/173 (client 110, core 63); golden 3/3;
  - the MonoGame, Debug desktop and Android builds compile (the APK holds the 30 font files);
  - frame time 1.4–3.6 ms.
- **Consequences:**
  - Any new text (isometric labels, cutscene text boxes, the map designer) takes a face from `FontSet`, adding a size by re-baking.
  - High-DPI and phone screens (M6) bake larger sets from the same `FontSet`.

## Epic CLIENT seams before M3: projection, draw order, view frame (review A1–A3, 2026-10-20/21)
- **Context:** the producer had the KNI client reviewed (Claude Docs, "KNI Client Architecture Review", 2026-10-19). Its three high findings stood between the client and M3's isometric view: top-down was hard-wired (A1), the layer table couldn't interleave walls and crawlers by depth and left the fog without a slot (A2), and the views read live simulation objects in `Draw` (A3). The producer asked for a plan, then for its execution. The plan was the Claude Doc "Plan: Projection, Draw Order and Frame Snapshot (A1–A3)", with a working copy in `.agents/`, deleted at this close-out.
- **Decisions (agent, unless noted):**
  - **A projection is a linear map of the floor onto a view plane** (`ToView`, `ToFloor`, `ViewBounds`, `Depth`, `LayerOf`, `ArtPixelsPerUnit`); `View` (projection + `ViewCamera`) does pixels, snapping and framing once for every projection. The plan had sketched screen maths on each projection. Because top-down's view plane is the world, the camera, rig and renders stayed bit-identical through the refactor. The art scale is a power of two, so sizes in art pixels convert exactly.
  - **Draw items stand on a floor anchor** with a height, a pivot and a size in art pixels; `ScreenQuad` places them for the renderer and the picker alike.
  - **Picking hit-tests the last drawn list:** the topmost pickable quad under the click, else the nearest centre within Unity's 0.6-unit reach. Behaviour change: in a crowd, the crawler drawn on top wins.
  - **Draw roles; the projection picks layers.** The sort key is layer 8 | depth 32 | role 4 | sequence 20. Walls take the projection's depth in both projections (top-down's wall order changed, not its picture).
  - **Fog: producer decision D1 = C (2026-10-21), both projections,** replacing A in the client. Built as a rule in the scene builder (`FogOfWar.Visibility`): what an uncharted sector holds isn't drawn and fades in with the cloud, so it holds in isometric without a cloud layer over the walls. The selected crawler's ring and name hide with it (agent; the selection and the camera's follow keep it).
  - **`WorldViewFrame`**, plain data captured on a new `SessionHost.TickCompleted` (raised after every crawler ticked, independent of subscription order; the seam a server would send from). Two frames, a new epoch on every respawn, switch or load. The views take no live entity, crawler state or registry (an architecture test).
  - **Interpolation:** crawlers drawn between the two frames at the clock's fraction, alpha 1 when the view runs on ticks or updates step whole ticks; no glide across epochs, for newcomers or over 2 units.
  - **`PicturePinTests`** hash the drawn picture finer than the render tolerance, which a hidden 11×11 px crawler passes (epic F9).
- **Verification:** 15 commits, `a34eb78` to `0eb0489`, each with its measured status; the 3.2 and 3.3 commits were tested as snapshots, and 3.3 again alone in a scratch worktree. Final: `dotnet test` 199/199 (client 136, core 63); golden 3/3; Unity PlayMode 69/69 and golden 3/3 on the core change; the MonoGame build compiles; frame time 2.14 ms against 2.09 ms (llvmpipe).
- **Consequences:**
  - M3 sets `SimpiensGame.Projection` to an `IsometricProjection` (tile size and orientation from the art), stands sprites on their feet, and draws the cloud per floor cell.
  - The simulation can later leave the render thread behind three frame buffers; the dashboard needs its own snapshot first.
  - The producer's look at the interpolated motion is open.

## Epic CLIENT M3: isometric as the game's view, and cutscenes in two kinds (2026-10-21)
- **Context:** the producer asked for the M3 plan and then the go-ahead (2026-10-21), answering only where they disagreed with a default:
  - P3: two kinds of cutscene: hard ones pause the game, dynamic ones are part of play.
  - P5: no rotation; one fixed view.
  - P7: a dynamic scene starts when a crawler enters a zone or starts an interaction with another, and never moves crawlers.
  - P8: the player keeps control during one.
  - P1, P2, P4 and P6 stood: 32×16 placeholders, cutaway walls, isometric by default with V to top-down, three pull requests.

  The working plan was `.agents/client_m3_plan.md`, deleted at this close-out.
- **Phase 0, the architecture review's items deferred into M3 (no visual change):**
  - One world epoch, unique in the process, for every view cache (A5).
  - View state advances once per update, and drawing is pure (A6).
  - The game splits into `ClientSession` (no XNA type), `WorldView` and a stack of screens, with `SimpiensGame` as the shell (A4).
  - Culling before sorting, and a frame path held at zero allocations by a test (R1's cheap half, R2, R3).
- **Phase 1, isometric drawing:**
  - **Art sets per view mode** (`WorldArt`, `ViewMode`), so the scene never asks which projection it has.
  - **Generated placeholders** decided per pixel against the projection's diamond.
  - **Each art set has its own sheet** (F12): a shared, larger atlas moved which texels point sampling picked.
  - **Things on the floor** (the fog's cloud) are drawn in floor units under the view's affine transform. The plan's "cloud per floor cell" was replaced by one continuous quad per sector; top-down stayed pixel-identical.
  - **Walls in the way:** the cutaway is a scene rule over the eight neighbours further from the viewer (the diagonals remove lone posts at T-junctions).
  - **Framing is crisp first** (F14).
- **Phase 2, the gate:**
  - Depth order wherever things overlap walls on the real outpost, and a crawler walking past a wall, picking and labels, all checked.
  - Nine isometric references and two pins, each looked at.
  - Frame time measured (F11).
- **Phase 3, cutscenes:**
  - **One model and one player** for both kinds, written in code.
  - **Hard cutscenes** are a covering screen that holds the world by stopping the simulation driver (no clock writes). They draw through `ScreenProjection`, a third projection, so the renderer is reused, with a Gum text box revealed by `MaxLettersToShow`.
  - **Dynamic scenes:**
    - **Triggers** are evaluated on frame capture (once per tick, on the simulation thread) from frames that now carry crawlers' sectors and the tick's chronicle events, so they start on the same tick in every run.
    - **Presentation:** a bubble over the head, placed in the view this frame draws; a bottom box as the fallback; a directed camera the player takes back.
    - **They write nothing:** the golden hashes hold with them playing.
  - **Demonstration content** is Debug-only, because the story belongs to the narrative work.
- **Deviations from the plan:**
  - `ClientGraphics` (the shell's shared rendering resources) was added so the world view and cutscenes share one renderer.
  - `SimpiensGame.Restart` and window reuse in the tests (F15) were added to fix a test-harness memory defect. Every offscreen game stayed alive, about 81 MB each, and the client suite was killed out of memory.
  - Chunks 2.1 and 2.2 shared one commit (one test file).
  - Two commit messages were corrected before any push: a frame-time run count, and a test count the gate hadn't measured.
- **Consequences:**
  - Isometric is the main view; top-down remains for developers (V).
  - M7's real art replaces `IsometricSprites` and `CutsceneArt` through the art sets and sheets.
  - The narrative work fills cutscene content and the hard ones' triggers.
  - M4 measures the static floor pass on a phone.
  - The tests can grow without memory growing per test.
- **Verification:** one commit per chunk with the gate measured.
  - Final: `dotnet test` 297/297 (client 234, core 63); golden 3/3; the MonoGame, Debug desktop and Android builds compile; Unity untouched.
  - Frame time 1.62 ms (isometric) and 2.38 ms (top-down) on llvmpipe.
  - The producer's look is open.

## 2026-10-22 — Epic CLIENT: the architecture review's open findings (M4 deferred)

- **Request:** the producer deferred M4 (Android) and asked for the open findings of the Claude Doc "KNI Client Architecture Review" first, on a new branch. Plan `.agents/client_architecture_plan.md` (deleted at this close-out), branch `feat/client-architecture` from `main-kni` at `9ff5be3`, 16 commits `9b8c456`..`ff3d81f`, not pushed.
- **Decisions:**
  - **Host commands are the client's, not the core's** (D3). `HostCommand` lives in `Simpiens.Presentation`; `ClientSession` applies them at the start of its update, before the ticks. Speed, pause, save, load and the switch are the host's decisions in a multiplayer game, and a network host would apply the same commands. The core didn't change.
  - **Pause holds the driver as well as the clock**, so whole-tick updates count no ticks they don't simulate (epic F6 in the client). The core's paused-clock behaviour is unchanged; tests that set the clock directly behave as before.
  - **A frame simulates at most 400 ms** (D2): a cap on real time, so it scales with the speed (4 ticks at 1x, 40 at 10x). The dropped time is counted and logged once per stall.
  - **Gum's cursor is the client's** (U1). `ClientCursor` ports Gum's mouse rules from the commit the package was built from (MIT), installed with `FormsUtilities.SetCursor` as Gum supports. The test harness no longer reflects into Gum. It still sets KNI's private focus flag, because Gum gates its whole update on `Game.IsActive` (epic F17).
  - **Keys are data:** `KeyBindings` maps keys to `InputAction`s; `KeyInput` stays as the frame's actions, so tests and the controls didn't change shape.
  - **Pause and speed are two header buttons** (PAUSE/PLAY and 1x/2x/5x/10x) plus P and 1–4. Space stays with cutscenes and dynamic scenes. The header's right group is 4 px apart, to keep about 38 px of slack at 1280 px.
  - **The dashboard requires its services** in the game (`DashboardContext.From`); the nullable constructor remains for unit tests, per the core's null-safety rule.
  - **Display names are a table** (`PlanText`), with a test that every planner type is named.
  - **One sector rule:** occupancy and residents use the map's `GetSectorAt`.
  - **The pinned image needs no registry:** Ubuntu by digest, apt by `--snapshot`, the SDK by version. The references are recorded in it, and inside it renders must match exactly (`SIMPIENS_RENDERS_EXACT`); other machines keep the tolerance.
- **Deviations from the plan:**
  - The frame-time gate's mean budget is 10 ms, not the review's 8, for CI runners' variance.
  - S6 was already fixed.
  - `HeadlessSimulation` became `LocalSimulation`; dated records keep the old name.
  - T2 landed as two commits: the image with the references unchanged, then the exact references.
  - Step 4.3 was committed twice. A power cut left its commit's objects empty (22 of them). `.git` was backed up, the branch pointed back at the last sound commit (`89732e1`), the empty objects and the index moved aside, and the index rebuilt from HEAD. Then `git fsck --full` was clean, and the same working tree was committed again after a non-incremental build and every check.
- **Deferred:**
  - U2 (density) and R6 (frame cap): M4.
  - R4, R5, R7 and S10: M7.
  - S5 (Unity in comments): Unity's removal.
  - The simulation on its own thread (D1's second half): not needed yet, since the tick is about 0.1 ms of a 1.4 ms frame (epic F16).
- **Verification:**
  - `dotnet test` 324/324 (client 261, core 63).
  - The client's 261/261 exactly in the pinned image, twice; a single one-level pixel change fails there.
  - Golden 3/3; the MonoGame and Android builds compile; Unity untouched.
  - Frame time unchanged: 1.5 ms (isometric) and 2.4 ms (top-down) on llvmpipe.

## 2026-10-23 — Epic CLIENT: into `main` before M4

- **Decision (producer):** the KNI client merges into `main` now, without waiting for M4 (Android), which stays deferred. This replaces the 2026-10-18 decision that `main-kni` acts as the main branch until M4 proves the Android build.
- **How:** one pull request from `feat/client-architecture` into `main`. The branch holds everything on `main-kni` (`9ff5be3`: M2.0–M2.7, the text cleanup, the seams, M3) and the architecture review's findings, so the 17 architecture commits are reviewed with the rest instead of in a pull request into `main-kni` first.
- **Consequences:** `main` is the main branch again. CI runs on pushes to `main` and on pull requests; the `main-kni` push trigger is removed, and `main-kni` retires once the pull request is merged. The Android client keeps compiling in CI.
- **Verification (pull request #6):** CI's first run stopped at "Permission denied", because git stored the container script as 100644 (`core.fileMode=false` hid it locally, as for the Android scripts in `3082ab8`); `0537155` fixed the mode. The next run passed both jobs: core 63/63, the client's 261/261 in the pinned image with exact renders, golden 3/3, the MonoGame and Android compiles.

## 2026-10-23 — Epic 10, Part 1: The Guild and Its Expeditions (Milestone 2) opens; Epic CLIENT pauses

- **Decision (producer):** after PR #6 merged, Android and the other engine work are set aside (Epic CLIENT `PAUSED`), and core gameplay comes next. The next epic was drafted as the Claude Doc "Epic 10, Part 1: The Guild and Its Expeditions" and approved with every default: `docs/epics/epic-10.md` §0 lists them (Q1–Q10).
- **Scope:** the roadmap's Milestone 2, with Epic UI-3 folded in because its UI is the KNI client's. Seven milestones: G0 a day that holds on every seed (Epic CORE F2), G1 drifters, G2 health and environmental hazards, G3 gear, G4 the guild, G5 expeditions, G6 the gate. No combat and no death; a guild of up to four.
- **Definition of Done amended (Q8):** new work proves live emergence headless in `dotnet test`, on the real session with nothing injected; a scripted player acting only through typed player commands is input, not injection. Unity's PlayMode suite stays a regression check on the core while Unity exists. ROADMAP §1B and AGENTS.md updated.
- **Dropped:** Epic 10's "`Unity.Collections` + Burst" deliverable; the core is engine-free, and speed-ups follow measurements (§3A #8 only on a measured need).
- **Shipping (Q10):** one pull request into `main` per milestone, from a `feat/` branch; each milestone has a working plan in `.agents/`, deleted at its close-out.

## 2026-10-23 — Epic 10 G0: a day that holds on every seed (finding F2)

- **Diagnosis:** no food shortage. Every failing seed had a crawler that could not move or kept retrying something that could not work:
  - an occupied destination froze a crawler across the map (the motor waited while its next waypoint was occupied, and a smoothed path's next waypoint is often the destination);
  - a sale the stall refused was retried every tick (room for either salvage passed for a crawler holding the other);
  - a panicking crawler blocked by a pawn panicked in place;
  - a hungry crawler searched the Road Gate and starved on the walk back.
- **Decisions:**
  - The waypoint-occupied check applies only within `ArrivalTolerance`; further out `IsCrowdedMove` keeps pawns apart.
  - Selling is planned only for a held item the stall can pay for, has room for, at or above the crawler's minimum (`AffordableHeldSalvage`, as `LiquidateSalvageGoal` did).
  - Fixing the motor let walkers reach occupied bunks, where they lay down on sleepers just up: the last step onto a bunk now waits for it to clear.
  - Panic sidesteps a blocking pawn (45°, then 90°) and lasts at most 6 s: the first version, without the cap, sprinted for 4,000 ticks around one waypoint.
  - The food search waits at the nearest plot the crawler has seen, as the starvation reflex already did.
  - **The headless day checks pawn stacking** as Unity's anomaly detector does (0.35 m for 1 s), so a navigation change that stacks pawns fails `dotnet test`, not only Unity's suite. Before the bunk fix it found 15 stackings on some seeds.
  - **The day is gated on 23 seeds** in `dotnet test` (`OutpostWatch.GateSeeds`: the golden seeds and 1–20), about 6 s.
  - **The greedy soak band is judged at its lowest trust mean**, as generous is at its peak. With hungry crawlers eating at the Garden instead of buying gouged rations late in the run, trust drifts back to neutral by minute 30 once greed has emptied the stall (seed 2: mean 0.0). The band's intent, that greed costs trust, is unchanged; reported to the producer, whose band it is (D5).
- **Deviations:** the working plan was written mid-milestone, after the diagnosis, rather than before it (G0 started as a measurement).
- **Findings:** F2 resolved. F3: on 4 of seeds 21–120 trust rises for 6–7 of 12 (crawlers who trade early, then socialize); a balance question for G1 and G4.
- **Verification:** gate seeds 23/23 with no stacking (16/23 before); seeds 21–120: 96/100; `dotnet test` 347/347; the client's 261/261 exactly in the pinned image; golden re-recorded in Unity, 3/3 headless; Unity PlayMode 69/69 (2 explicit skipped); the economy soak 9/9 headless and in Unity.

## 2026-10-23 — Epic 10 G1: drifters

- **Decision (producer Q7, defaults):** the 12 residents stay; strangers come in with their own gold and leave after a stay. Plan `.agents/epic10_g1_plan.md` (deleted at this close-out), branch `feat/epic10-g1` stacked on G0's.
- **Design:**
  - **Leaving is a decision, not a teleport.** Past its stay a drifter's `LeaveOutpostGoal` (priority 95, above every need short of starvation) walks it to the nearest spawn point; `SimulationManager.DepartOutpost` sends its gold out as `DrifterDepartures`, reopens a claimed bounty (escrow kept) and frees its bunk; the host removes it at the end of the tick, before views capture the frame, and raises `CrawlerDeparted`.
  - **The schedule is the host's and is saved** (`SessionSave.Population`); a drifter's `DepartsAtTick` is saved in its record (0 means resident); save format 2.
  - **Checks that judge "every crawler" judge the residents:** the day, the soak's trust bands. Gold conservation counts every present crawler.
  - **Tuning by the approved Definition of Done** ("the fair player's soak stays in its band"): purses of 40–80g every two minutes nearly doubled the fair treasury (1,189–1,273g against 375–875g); 20–26g every 3.5 minutes keeps it at 768–831g, with the keen player still richer than closed over the soak seeds. Per seed that comparison is noisy (a different run of a chaotic day), so the test sums it and checks per seed that drifters leave gold behind.
- **Fixed on the way** (each was a crawler stuck or a borderline in the residents' day once drifters were about): walkers keep right around a crawler in the way (head-on jams in doorways); salvage the stall won't take no longer counts as salvage to sell, so a crawler gathers what it still buys (and gathering prefers that kind); food beyond the 30-unit search distance is still food (a crawler at the Road Gate "arrived" in place every tick); the walk to food stops at 0.7, inside the 0.8 harvest reach, instead of on its edge.
- **Cross-runtime (Epic CORE F1, narrowed):** the golden seeds parted twice (424242 at t2600, 20261010 at t2700) at borderline distance tests. `Vec2`'s scalar chains now cast to float after every step, which makes Mono round as CoreCLR does and changes nothing on CoreCLR; the drifters' random draws do the same. All three golden seeds match in both runtimes again.
- **Unity:** `SwarmSpawner` destroys a departed drifter's view (an adaptation to a crawler leaving, not new view work).
- **Verification:** gate seeds 23/23 with drifters, no stacking; seeds 21–120: 98/100 (F3); `dotnet test` 352/352; the client's 263/263 exactly in the pinned image; golden 3/3 in both runtimes; Unity PlayMode 69/69 (2 explicit skipped); the soak 9/9 headless and in Unity.

## 2026-10-23 — Epic 10 G2: health and hazards

- **Decision (producer Q2, Q3, Q9, defaults):** no combat and no death: the danger is the floor's, and a crawler at 0 health is downed; one rare salvage, found only in hazard sectors and bought by the caravan at a premium. Plan `.agents/epic10_g2_plan.md` (deleted at this close-out), branch `feat/epic10-g2` stacked on G1's.
- **Design:**
  - **Hazards are map data** (`SectorDefinition.Hazard`: a kind and damage a second). Floor 1's two blocks of rock became the Sump and the Glow Caves, so every existing room keeps its place; both start charted and are dead ends, so pathfinding needs nothing new. Floor 1 has no rock tiles any more.
  - **Health is the crawler's** (`AgentState.Health`, `IsDowned`, saved, format 3); the rules are in `Cognition/Vitality`. Damage is the runtime's (it knows the map); healing is the state's tick. The reflexes sit in `MentalBreakEvaluator.DangerReflexAsync`, before the planner.
  - **Who ventures** is personality and condition (`Vitality.WouldVenture`): risk tolerance compared in whole percent, because `30 / 100f >= 0.3f` holds in CoreCLR and not in Mono's double evaluation.
  - **Glowstone** is a fourth stall price and stock (`TradeStallData` appended, 52 bytes) and a caravan quota at a premium (`CaravanSalvageSales`).
- **Tuning and faults found on the way:**
  - Glowstone was worth farming at first: one unit per node, 10-minute regrowth and a caravan quota of 2.
  - Cautious crawlers wandered into hazards through exploration and ambient wander, which now apply the venture rule too.
  - Fetching a herb starved crawlers; they now fetch one only when hunger is under 50.
  - A downed crawler stayed in the caves, because it knew no free bunk. The refuge is the floor plan's nearest bunk, which every crawler knows.
  - The starvation rule pre-empted a retreat every tick; tending (retreat, treating wounds, sleeping, resting out of danger) is now exempt.
  - Bold crawlers looped in and out without selling, until damage was set at 3 and 4 a second and the retreat at 40.
- **Deviation:** the epic's G2 Definition of Done asked that the soak keep its bands; glowstone is new money by design, so the fair band was re-set to 1,000–2,200g (fair 1,545–1,594g) rather than tuning the danger out of being worth it. **To confirm at the producer's look.**
- **Client beyond the plan:** the red tint alone read weakly (the Sump looked like the Ore Quarry), so each hazard's danger is written under the room's name ("FOUL AIR"), hidden while the fog covers the sector; the merchant window widened to 660 for the longer stock line.
- **Cross-runtime (Epic CORE F1, narrowed again):** the golden seeds parted at t100 and t3000. A per-tick trace of every crawler's position bits found the first divergence at one crawler's first step, and a dump of its raw path showed two equal-cost A* routes. Mono kept `PathNode.FCost`, the octile heuristic and the step costs at double precision. The searcher, the snapshot's line checks and `IsPositionBlocked` now round each float step, as do `AgentState`'s drives; nothing changes on CoreCLR. The soak's 9 runs then matched to the gold in both runtimes.
- **Unity:** no view changes; its frozen views draw the new sectors from the map and glowstone with the fallback sprite.
- **Verification:**
  - gate seeds 23/23, no stacking; seeds 1–120 with the golden seeds 120/123 (F3: 31, 43; seed 73 one crawler's hunger peak);
  - `dotnet test` 355/355; the client's 265/265 exactly in the pinned image;
  - golden 3/3 in both runtimes;
  - Unity PlayMode 69/69 (2 explicit skipped);
  - the soak 9/9 headless and in Unity, with identical numbers.

## 2026-10-24 — Docs cleanup; the producer's sign-offs; the move away from Unity

- **Request (producer):** clean up stale documents under `docs/` and `.agents/`, reconcile discrepancies, and ask where unclear.
- **Producer answers (2026-10-24):**
  - Milestone 1's save/load check at 1× passed.
  - Epic CLIENT's side-by-side with Unity passed, as did the looks at M3, the motion between ticks, and the header's pause and speed. "We are at a point where we can officially move away from Unity": M2.8–M2.9 are unblocked, to be scheduled.
  - Still open: the look at Epic 10 G2 and the re-set fair soak band.
- **Found: G1 and G2 never reached `main`.** PRs #8 and #9 were stacked on the previous milestone's branch, merged into it, and those branches were deleted after #7 merged; `main` stops at G0. Producer decision: G1, G2 and this cleanup go into `main` in one pull request from `docs/cleanup` (cut from `feat/epic10-g2`). Stacked milestone PRs are not used again; from G3 each targets `main`.
- **Done:**
  - The roadmap: the Epic MAP, CORE and CLIENT logs condensed to status, decisions and open items (the detail is in the epic docs and this log); §2.1's library table brought up to date (UniTask retired, `Unity.Collections` + Burst dropped, VContainer Unity-only; MS.DI, xUnit v3, KNI, MonoGame, Gum and KernSmith added); §2.2's handroll and UI Toolkit policies marked delivered or superseded; Milestone 1's pending check closed; §3A's stale numbers (risk tolerance used, the 23-seed gate, 39 of 64 GOAP facts); the Epic 10 and 11 rows; the operating rhythm.
  - Links: every `file:///` absolute link and every link to a file moved by Epic CORE repointed to its current location (the roadmap, epics 7, 9 and UI-2, the consultant review); none broken.
  - `docs/epics/epic-client.md`: the sign-offs; M2.8–M2.9's scope moved in from `.agents/client_m2_plan.md`, which was deleted (its decisions are in the M2 entry above); the closed person-gated risks.
  - `docs/epics/epic-10.md`: how G1 and G2 landed; the fact count. `docs/epics/epic-core.md`: F1's owner is Unity's removal.
  - `docs/economy-model.md`: four prices, glowstone in the loop, the salvage cap (248g a visit), current measurements, the 23-seed gate.
  - `docs/Game Design Proposal.md`: a title, the producer's platform and presentation decisions, the economy question marked answered.
  - `.agents/curentfocus.txt`: rewritten as a short mirror (266 → about 70 lines).
  - `.agents/rules/`: the two original bootstrap prompts (`autonomous-architecture.md`, `unityrules.md`), loaded always-on by some IDE agents and contradicting `AGENTS.md`, replaced by one pointer to `AGENTS.md` and the roadmap that keeps the producer's communication preferences.
  - `.agents/local/`: Epic CLIENT's directive and design decisions archived as `archive/epic-client-m0-m3_2026-10-23.zip` (local, never committed).
- **Left as they are:** the completed epics' specs (history; their Unity-era wording describes the code as built), `docs/engine-proposal-kni.md` (a dated decision record), the consultant review's text (a dated snapshot).

## 2026-10-24 — Epic CLIENT M2.8–M2.9: Unity removed

- **Decision (producer, 2026-10-24):** "remove Unity first", after signing off the side-by-side and the client's looks. Epic CLIENT was ACTIVE for M2.8–M2.9 and is PAUSED again; Epic 10 G3 is next. Plan `.agents/client_unity_removal_plan.md` (deleted at this close-out), branch `feat/remove-unity`, cut from `docs/cleanup` (PR #11, which carries G1 and G2), with its pull request into `main` (no stacking).
- **M2.8, every Unity test twinned:**
  - **The anomaly detector moved to plain data** (`Simpiens.Hosting.AnomalyDetector`), judged per tick on the authoritative positions and states with Unity's thresholds in simulation time. Placed in `Simpiens.Hosting`, next to `OutpostWatch`, so no new code went where Unity compiled while Unity still existed.
  - **Thrashing is judged on what the player sees.** On the simulation's own state the rule fires on single-tick decision bookkeeping (2–13 a day on the gate seeds, 438 in 1,000 ticks of the pens); `CrawlerAppearance` holds a state 3 ticks, as Unity's `AgentView` did, and Unity judged the displayed state. So the detector takes a state source; the client's `ThrashingTests` judge through `CrawlerAppearance` (0 on screen), and headless runs report raw thrashing without asserting it (epic F18). The day now asserts zero jitter, stacking, penetration and breaches.
  - **Scenarios ported** with Unity's bounds in ticks: the pens (outcomes at the ticks Unity saw), resource contention, walls dropped into doors (with an engine-free `EdgeCaseInjector`), bunk rest, price response (residents only: drifters come and go), the live capture and restore, and the population probe (explicit; no latency to sample, decisions land on the next tick). `SwarmNavigationScenario` is subsumed by the day's checks; `EntityIdCompatibilityValidator` was dropped.
- **M2.9, the removal:** a pure move with `git mv` (history kept) into `dotnet/Simpiens.Core` and `dotnet/Simpiens.Testing.Core`, then deletions (`Assets/`, `ProjectSettings/`, `Packages/`, Unity's solution, `.vscode/`, six Unity scripts). Deviations from the scope: none in the sources; the C# 10 / netstandard2.1 cap stays (a follow-up). `.gitignore` keeps ignoring Unity's leftover folders until developers delete them by hand. `simpiens golden record` became the way to re-record the baselines (it writes the committed file line for line). Comments that pointed at Unity's tools or assemblies were fixed; notes on where code was ported from, and the reasons for the float casts, stay.
- **Docs:** `AGENTS.md` rewritten for a .NET-only repo; the roadmap's DoD, logging rule, library table, MCP policy and operating rhythm; Epic CORE's F1 closed; Epic 10's rules (Unity was a check for G0–G2); the economy model's soak references.
- **Verification:** `dotnet test` 372/372 (core 105, client 267); golden 3/3; the client's 267/267 exactly in the pinned image; the MonoGame and Android builds compile; the population probe runs explicitly (11–24k ticks a second, ledger balanced).

## 2026-10-24 — C# 14 on .NET 10, the newest .NET KNI supports

- **Decision (producer, 2026-10-24):** move to the latest .NET and C# as part of Unity's removal, "the latest version KNI supports", unless the core were independent and could differ. It isn't: the client references the core through Presentation and Hosting, so the core can't target a newer .NET than the client.
- **The ceiling:** KNI 4.3.9001 (the latest on NuGet) ships its desktop libraries for `net8.0` and its Android platform only for `net10.0-android36.0`; Gum's stable packages target `net8.0`; .NET 11 is at release candidate (GA expected November 2026). So: .NET 10, C# 14.
- **Done (`fb86e80`):** the core dropped its `netstandard2.1` build; the C# 10 pin and the net10.0 "latest" override are gone, so every project uses its framework's default, C# 14. Nothing in the sources worked around netstandard2.1, so no code changed. Nullable reference types and implicit usings stay off: turning them on is a code-wide change of its own.
- **Verification:** `dotnet test` 372/372; golden 3/3; the client's 267/267 exactly in the pinned image; the MonoGame and Android builds compile.

## 2026-10-25 — A code-smell report checked; the cleanup before Epic 10 G3

- **Source:** the producer had a smaller agent write a code-smell report (not in the repo) and make the small fixes on `feat/code-cleanliness`: `52f582f` (§1A.2 #17's exception boundary, #19's bounty ids, `PathResponse`'s double-return guard, succession ties broken by entity id, a dead field) and `e58073a` (Gum's types to `Gum.GueDeriving`: the build's 316 warnings to 0, renders unchanged byte for byte). Both were re-run before building on them.
- **Decision (producer, 2026-10-25):** no epic. The plan `.agents/core_refactor_plan.md` holds the report's triage and the refactor (R0–R6); R0, the cleanup, is done now, and the rest comes back after Epic 10 Part 1.
- **The report, checked:** `SimulationManager` as a god object (1,876 lines), the locked GOAP planner (#8), the A* cell cap and the 64-fact limit hold, and are in the plan. Rejected or narrowed:
  - `Vec2 ==` stays approximate (the golden baselines depend on it).
  - `MentalBreakEvaluator` edits only the agent's private snapshot buffer; the authoritative forgetting is on the main thread when the panic ends.
  - Gossip through the spatial grid would change who gossips with whom (the first partner found wins), so it is a behaviour change, deferred to roadmap §3A #8 with the other scaling items.
- **R0, what the cleanup added to the smaller agent's fixes:**
  - **Faults are counted.** A caught fault was only logged, so a test would pass over a throwing handler. `ISimulationManager.IntentFaults` and `FirstIntentFault` count them for the simulation's lifetime; `OutpostWatch` fails the day on any, and the scenarios that don't judge the whole day assert zero.
  - **Bounty ids.** The generator's skip loop could never end on a full board; it now gives up after one pass and the post is refused.
  - **Deviation from the plan:** R0 proposed refusing a finished bounty's id as well. `EconomyZeroAllocValidator` reposts fixed ids every round to measure a steady state, and the bug was losing a *live* bounty's escrow, so only live ids are refused, as the smaller agent had it.
  - **Counter not saved:** `_nextBountyId` is derived on load from the saved bounties, and a check pins it.
  - **Tests moved:** the smaller agent's `CleanlinessTests` became validator tests by domain (navigation, economy, sociopolitical, environment, persistence).
  - **Warnings are errors** in `Directory.Build.props`, except NuGet's audit advisories.
- **Git identity:** every local commit since the start used the global config's `brandon.fox@gmail.com`; the producer's identity is `Brandon Fox <brandonslyfox@gmail.com>` (their GitHub account). The global config is fixed, and the branch's three unpushed commits were re-authored; published history keeps the old address (producer's choice: no force-push).

- **Verification:** `dotnet test` 372/372 (core 105: the smaller agent's 5 tests now run inside the validator tests; client 267); golden 3/3, unchanged; the client's 267/267 exactly in the pinned image; the MonoGame and Android builds compile; 0 warnings everywhere.

## 2026-10-25 — Epic 10: G2 signed off, the fair band set, G3 planned

- **Decisions (producer, 2026-10-25):**
  - The look at G2 passed: "hazards UI is fine for now".
  - The fair soak band is 1,000–2,000g, not G2's provisional 1,000–2,200g. `EconomySoak.FairTreasuryMax` is now 2,000; the fair runs measure 1,545–1,594g (the soak passes 10/10).
- **G3 planned** (`.agents/epic10_g3_plan.md`, branch `feat/epic10-g3`). Three design defaults go to the producer by exception:
  - **D1, one protective item.** A harvest takes 1.5 s against 15–30 s of exposure per trip, so a pickaxe can't cut a trip's damage by more than about 5%, and a yield bonus is an economy change better tuned with the workshops. The pickaxe moves to Part 2. This deviates from the epic's G3 row, which names the pickaxe and the weapon.
  - **D2, the hazard-defence item renamed.** Epic 9's `BasicWeapon`, "equipped for hazard defense", becomes `HazardGear`, because Part 1 has no combat.
  - **D3, gear worn in a slot of its own.** It never crowds salvage out of the 4-slot pack.
- **Gear's place in the economy:** gear wear is the crawler sink Epic 9 D2 deferred. The caravan's wholesale gear is a new named sink (`GearWholesale`), and the counterweight to glowstone's money until wages (G4).

## 2026-10-25 — Epic 10 G3: hazard gear

- **Decisions:** the producer said "proceed" on the plan's defaults (D1–D3, by exception).
  - **D1:** one protective item. The pickaxe moves to Part 2 and a weapon to the descent: a faster harvest cuts a hazard trip's damage by 5% at most.
  - **D2:** `BasicWeapon` becomes `HazardGear`.
  - **D3:** gear is worn in a slot of its own beside the pack.
- **Built** (`906310e`..`9ff548c`):
  - worn gear halves hazard damage and wears out after 500 ticks in danger (durability counts ticks: whole numbers, and the `ushort` already saved with an item);
  - the stall sells gear; after the rations, the caravan restocks it under a new sink, `GearWholesale`;
  - bold crawlers who know where glowstone is buy it (`OutfitForDangerGoal`, `BuyHazardGearAction`; no new planner facts);
  - save format 4;
  - a gear row in the merchant terminal; gear in the dossier.
- **Deviations from the plan:**
  - **The default gear price is 30g, not 40g.** At 40g, the base value, nobody bought in a day on three seeds: a crawler with any greed values gear below its base value. 30g keeps the ration's ratio (15g against a 20g neutral value: 0.75). Soak profiles: generous 20, keen 35, greedy 60.
  - **Durability:** the plan's "100, losing 2 a second" became 500 ticks of life (the same 50 s).
  - **The merchant window grew to 740.** The longer stock line ran into the treasury; a test now checks it with a four-digit treasury.
  - **G1's keen comparison stopped holding** once gear tipped a fragile margin. Since glowstone it is within the noise: 15 seeds, 19,355g against 19,249g. `AKeenPlayerDoesBetterWithDrifters` became `DriftersLeaveGoldBehindForAKeenPlayer`, which asserts what is causal and reports the rest (Epic 10 F4).
  - **The world hash** now covers glowstone, health, worn gear and the stall's glowstone and gear stock. G2 and G3 had added them without the hash seeing them.
- **Found:** on seeds 1–120 the day fails on 7 seeds on `main` and on 8 with G3 (seed 81's trust, F3). Four of those are early jitter (Epic 10 F5). They fail only since Unity's removal made jitter fail the day; no gate seed is affected.
- **Verification:**
  - `dotnet test` 376/376 (core 107, client 269); golden 3/3 re-recorded; the client's 269/269 exactly in the pinned image;
  - the gate seeds 23/23; the soak 9/9 (fair 1,499–1,564g);
  - the MonoGame and Android builds compile; 0 warnings.

## 2026-10-26 — The item system reviewed; an item catalog planned (refactor R7)

- **The question (producer, before merging Epic 10 G3):** is the item system extensible, and can items be grouped by tag or category?
- **The answer:** not yet.
  - **Generic:** the item record, the pack and resource nodes.
  - **Not generic:** the stall (`TradeStallData`), the caravan (`SupplyTuning`), the price command, the stall's opening terms, the crawlers' salvage choices and the client name each item.
  - **Categories** are hand-written checks.
  - **Cost:** glowstone (G2) and hazard gear (G3) each touched about 20 files; `TradeStallData` alone took 16–17 edits and a save-format bump per item.
- **Decision (producer, 2026-10-26):** add R7, an item catalog, to `.agents/core_refactor_plan.md`.
  - **Design:** item definitions with bit-flag categories; per-item tables in the stall and the caravan; cognition looping over a category in a stated order.
  - **Timing:** before Epic 10 Part 2, whose crafting multiplies the items, and alongside R4 (the stall's extraction into `Market`).

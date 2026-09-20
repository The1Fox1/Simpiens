# Architectural History & Decisions Log

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

## Future Progression
*(See `curentfocus.txt` for upcoming epics)*
- **Epic 6: Multi-Step Reasoning** (Phase 2: A* Graph Search; Phase 3: Planner Integration & Reflexive Preemption)
- **Epic 7: Sociopolitical & Relationship System** (Affinity matrices, Tribal metadata)
- **Epic 8: Inventory & World Construction** (Item ownership, permanent world mutations)
- **Epic 9: Martial Engagement** (Tactical evaluation, transient projectile data)
- **Epic 10: Agentic Narrative Engine** (MCP JSON payload serialization, LLM evaluators)

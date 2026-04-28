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

## Future Progression
*(See `curentfocus.txt` for upcoming epics)*
- **Epic 5: The Memory & Knowledge Matrix** (Local knowledge grids, Event Ledgers)
- **Epic 6: Multi-Step Reasoning** (GOAP / HTN planners)
- **Epic 7: Sociopolitical & Relationship System** (Affinity matrices, Tribal metadata)
- **Epic 8: Inventory & World Construction** (Item ownership, permanent world mutations)
- **Epic 9: Martial Engagement** (Tactical evaluation, transient projectile data)
- **Epic 10: Agentic Narrative Engine** (MCP JSON payload serialization, LLM evaluators)

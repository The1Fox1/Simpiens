# Epic CORE: Engine-Free Core and Headless .NET

- **Status**: ACTIVE (started 2026-10-17). Status is owned by [ROADMAP.md](../ROADMAP.md). Working plan: `.agents/godot_phase01_plan.md`.
- **Track**: Independent (cross-cutting architecture)
- **Source**: Phases 0 and 1 of the migration write-up "Simpiens: Migrating from Unity to Godot on .NET 10" (Claude Docs, 2026-10-15). Phases 2–4 (Godot spike, view parity, cutover) are outside this epic; the spike decides whether they happen.
- **Downstream beneficiaries**: multiplayer (state as plain data, deterministic ticks: §1A.1), Epic 10 and the scaling work (§3A #8: profiling and soaks without the editor), Epic 11 (the official MCP SDK needs .NET), and any engine choice.
- **Layers**: every engine-free part of the simulation (`Simpiens.Cognition`, `Simpiens.Simulation`, `Simpiens.Intervention`, `Simpiens.Narrative`, `Simpiens.Diagnostics`, `Simpiens.Core.Session`), the views that read it (`Simpiens.Entities`, `Simpiens.UI`), the test harness, and a new `dotnet/` solution.

---

## 0. Producer intent and decisions

The producer approved Phases 0 and 1 of the migration as standalone work on 2026-10-17, because they pay off on any engine, and asked the agent to see them through under provisional authority.

Decisions taken on the write-up's checklist:
- **Platforms:** web is not needed.
- **Sequencing:** Phases 0–1 go ahead as standalone work. MemoryPack shipped with Milestone 1 and can be replaced if needed.
- **Go/no-go:** commit to Godot only after the Phase 2 spike.
- **DI container:** `Microsoft.Extensions.DependencyInjection` for .NET hosts.
- **Test framework:** xUnit (v3) for the .NET tests.
- **UI authoring:** follow industry standards (applies to Phase 3).
- **ROADMAP §2:** retire the VContainer, UniTask and `Unity.Collections` + Burst rows; lift the MCP SDK rejection for Epic 11.

---

## 1. Goal

The simulation runs without an engine. The same source files compile in Unity, where a no-engine assembly forbids engine references, and for `net10.0`, where a host steps ticks with no frames. Both produce the same world, proven by the same world hashes.

**In scope:**
- Phase 0, Decouple: cognition on fixed ticks; golden world-hash baselines; engine-free ids and maths; entities as plain data; no DI, async or logging framework of Unity's in engine-free code; the `Simpiens.Core` assembly.
- Phase 1, Headless .NET: the `dotnet/` solution; xUnit v3 tests for the validators and the Definition-of-Done scenarios; a console host for runs and soaks; CI without Unity.

**Out of scope:** any Godot code, view or UI changes beyond reading the new data, gameplay or economy changes, and fixes not on the path (§1A.2 #8, scan H1 and M4).

---

## 2. Design

### 2.1 Target layout

| Unit | What | References |
| :--- | :--- | :--- |
| `Simpiens.Core` (Unity asmdef, `noEngineReferences: true`; `dotnet/Simpiens.Core.csproj`) | Simulation, cognition, sessions and layouts, economy, persistence, narrative, logging | ZLogger, MemoryPack, `Microsoft.Extensions.Logging` |
| `Simpiens` (Unity) | Composition root (VContainer, until cutover), views, dashboard, camera, input | `Simpiens.Core`, Unity packages |
| Engine-free test harness (Unity, test-only; and the .NET test project) | Validators, the headless harness, `WorldStateHash` | `Simpiens.Core` |
| `Simpiens.Testing` (Unity, test-only) | Anomaly detector, recorder, batch controller, view-driven scenarios | `Simpiens`, the engine-free harness |
| `dotnet/Simpiens.Hosting`, `Simpiens.Host`, `Simpiens.Core.Tests` (`net10.0`) | DI registration and the headless simulation; the console host; xUnit v3 tests | `Simpiens.Core` |

The shared code stays at C# 10 and netstandard2.1 APIs while Unity compiles it. Namespaces don't change.

### 2.2 Cognition on fixed ticks
On tick T, each agent due for a decision is prepared on the main thread as today (memory, group compliance, context, snapshot retained). Its evaluation runs off-thread and returns a result as data: the intent, the plan to adopt or abort, the goal, the decision summary, the visual state. At the start of tick T + 1 the main thread waits for every evaluation dispatched at T and applies the results in agent-id order. Evaluations still run in parallel within a tick, the tick a result lands on no longer depends on thread timing (§1A.2 #6), and nothing off-thread writes agent state (#15).

### 2.3 Golden world-hash baselines
`WorldStateHash` samples of the outpost for 3 seeds every 100 ticks to t3,000 live in a committed file. A PlayMode scenario, and in Phase 1 a .NET test, compares a fresh run against it. Every mechanical chunk must reproduce it; a deliberate behaviour change re-records it in its own commit.

### 2.4 Ids and maths
- `EntityId` replaces `UnityEngine.GUID`. It is bit-compatible (layout, ordering, hash code, hex string), so the swap changes no behaviour and saves stay readable.
- `Vec2`, `GridPoint`, `RectF`, `Rgba` and `SimMath` replace Unity's maths in engine-free code. Their arithmetic copies Unity's formulas exactly, so hashes don't move; views convert at the boundary.

### 2.5 Entities as data
Entity records (id, kind, position, radius, speed, resource kind) keyed by `EntityId` replace `NodeController` as the simulation's handle. The per-agent tick moves out of `AutonomousAgent` into the core, driven by an explicit tick pipeline. Views read state and never write it, which closes §1A.2 #16. The session spawner splits into an engine-free world builder and a Unity view binder.

### 2.6 Headless .NET
`dotnet/Simpiens.Core.csproj` compiles the core's files by glob for `netstandard2.1;net10.0`. `AddSimpiensCore()` registers the core for `Microsoft.Extensions.DependencyInjection`; a headless simulation composes a session and steps ticks. xUnit v3 runs the validators and the DoD scenarios. `Simpiens.Host` runs sessions, soaks and seed sweeps from the command line.

---

## 3. Work chunks

| Chunk | Phase | Work |
| :-: | :-: | :--- |
| C0 | 0 | Setup and docs |
| C1 | 0 | Cognition on fixed ticks (§2.2) |
| C2 | 0 | Golden world-hash baselines (§2.3) |
| C3 | 0 | `EntityId` (§2.4) |
| C4 | 0 | Engine-free maths (§2.4) |
| C5 | 0 | Entities as data (§2.5): C5a records and the crawler tick; C5b the session host |
| C6 | 0 | Infrastructure out of the core |
| C7 | 0 | The `Simpiens.Core` assembly; Phase 0 gate |
| C8 | 1 | `dotnet/` solution and the headless simulation |
| C9 | 1 | xUnit v3 tests; Phase 1 gate |
| C10 | 1 | `Simpiens.Host` |
| C11 | 1 | CI and close-out |

**Definition of Done (ROADMAP §1B) for this epic:**
1. **Mechanism:** the validators pass in Unity and under `dotnet test`, including the zero-allocation ones.
2. **Live emergence:** the existing DoD scenarios (`OutpostSupplyLoopScenario`, `OutpostLifecycleScenario`, `SaveLoadScenario`) pass in Unity on the real outpost, and their headless .NET counterparts pass on the same session with nothing injected. The golden world hashes are equal in both runtimes.
3. **Docs reconciled.**

---

## 4. Acceptance checklist (delivered work only; filled in as chunks land)

- [x] **C1, cognition on fixed ticks (2026-10-17).** `CognitionScheduler` lands every decision dispatched on tick T at the start of T + 1, in agent-id order, behind a barrier; the evaluation returns a `CognitionResult` and writes only its scratch plan; the main-thread preparation is guarded. The cognition path uses `ValueTask` (no UniTask), and the pathfinder plans on the calling worker. Closes ROADMAP §1A.2 #6, #15, #18. Evidence: `CognitionSchedulerValidator` (6 tests); `DeterminismScenario` identical alone in 3 of 3 runs (it diverged at t100 in 5 of 5 cold runs before), with the pre-C1 milestones; PlayMode 66/66; economy soak within every pace band, fair 546–771g at 30 minutes as in §3.3.
- [x] **C2, golden world-hash baselines (2026-10-17).** `Baselines/golden-world-hash.txt` holds 31 `WorldStateHash` samples (t0 to t3,000) of the outpost for seeds 20261009, 20261010 and 424242: the total and part hashes, and each agent's hash with a readable line for diagnosis. `GoldenWorldHashScenario` checks the default seed in every full suite; `scripts/golden-hashes.sh check|record` runs each seed in its own Unity process. Evidence: all 3 seeds reproduce their baselines in fresh processes and inside the suite; a corrupted baseline fails at the corrupted tick and names the agent; PlayMode 67/67.
- [x] **C3, `EntityId` (2026-10-17).** `Simpiens.EntityId` replaces `UnityEngine.GUID` in all 125 files that used it: the same four words, ordering, hash code (which keys `SimRandom`), hex text and memory layout, so the swap changes no behaviour and the save format is unchanged. It lives in the root namespace because Unity 6.4 has its own `UnityEngine.EntityId`. Evidence: `EntityIdCompatibilityValidator` (layout bytes; 5,000 values compared with `GUID` for hash, text, emptiness, ordering, equality and both parsers; the player id); golden hashes identical; save validators pass; PlayMode 68/68.
- [x] **C4, engine-free maths (2026-10-17).** `Vec2`, `GridPoint`, `RectF`, `Rgba` and `SimMath` (root namespace) replace Unity's `Vector2`, `Vector2Int`, `Rect`, `Color` and `Mathf` in the 198 engine-free files, formula for formula from Unity's decompiled source (approximate `Vec2 ==`, `Normalized` zero below 1e-5, round half to even, the same hash codes and layouts). `NodeController`'s simulation API is `Vec2`; views, the dashboard and the test harness convert at the boundary (`SimUnityConversions`). Evidence: golden hashes identical for all 3 seeds in fresh processes; PlayMode 68/68.
- [x] **C5a, entities as data (2026-10-17).** `WorldEntity` (plain data; a crawler's position is its `AgentState.Position`) replaces `NodeController` in the world registry, `SimulationManager`, the 12 intent handlers, the execution context, the spatial grid and the succession engine. The per-crawler tick moved from the `AutonomousAgent` MonoBehaviour to the engine-free `AgentRuntime`, without the copy of the rendered transform into the simulation (ROADMAP §1A.2 #16). `NodeController` and `AutonomousAgent` are views bound to their records; views show and hide with the registry's events. The 18 validators that built GameObjects build headless records (`HeadlessEntities`); the anomaly detector checks injected walls by simulation geometry. Evidence: golden hashes identical; `DeterminismScenario` milestones unchanged; PlayMode 68/68.
- [x] **C5b, the session host (2026-10-17).** `SessionHost` (engine-free) is the running session: it builds the world from the map, populates it, ticks every crawler after the simulation's tick, and captures and restores it. `SwarmSpawner` is its Unity side: it draws what the host spawns and keeps the session API. Body radii are constants (crawler 0.35, resource 0.4, hearth 0.35, bunk 0.5, the last measured in play mode as the collider default it replaced). `WorldStateHash` reads crawlers from the host. Evidence: golden hashes identical for all 3 seeds; PlayMode 68/68.
- [x] **C6, infrastructure out of the core (2026-10-17).** Engine-free code no longer uses UnityEngine, VContainer, UniTask or ZLogger.Unity: the clock takes the host's elapsed time (`Advance`; Unity's `SimulationClockDriver` passes the frame time), the pathfinder's pools are filled by the composition root, multi-constructor services are registered by factory, the narrative hook and the save slot use `Task`/`ValueTask` (the slot's folder is a constructor argument), and `SimLog`'s console sink is plugged in by the host (`UnitySimLogSinks`). Validators log through the framework-free `TestLog`, fail by throwing, and no longer implement `IStartable`; only 12 harness files still need the engine (views, UI, the anomaly detector, the GUID comparison). Evidence: golden hashes identical; PlayMode 68/68.

### Deferred (owned elsewhere)
- 64-bit `EntityId` (smaller saves and network messages): changes behaviour and the save format; belongs with the multiplayer wire format.

---

## 5. Risks

| Risk | Mitigation |
| :--- | :--- |
| Fixed-tick cognition shifts scenario outcomes | It is a deliberate behaviour change: full suite and economy soak at its gate; assertions change only with evidence |
| Mono and CoreCLR floats differ | Engine-free maths with explicit scalar formulas; per-part hash descriptions find the first difference |
| The entity refactor changes iteration order | Spawn and registration order are kept; golden hashes after each sub-chunk |
| Scope creep (planner #8, UI) | Port behaviour as it is; other fixes land before or after with their own baseline |

---

## 6. Open findings

*(none yet)*

---

## History
- 2026-10-17: epic created from the migration write-up's Phases 0–1; producer approval and decisions recorded (§0).
- 2026-10-17: C1 done (cognition on fixed ticks).
- 2026-10-17: C2 done (golden world-hash baselines).
- 2026-10-17: C3 done (`EntityId`).
- 2026-10-17: C4 done (engine-free maths).
- 2026-10-17: C5a done (entities as data).
- 2026-10-17: C5b done (the session host).
- 2026-10-17: C6 done (infrastructure out of the core).

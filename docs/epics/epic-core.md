# Epic CORE: Engine-Free Core and Headless .NET

- **Status**: COMPLETED (2026-10-17). Merged to `main` as PR #5 on 2026-10-17. Status is owned by [ROADMAP.md](../ROADMAP.md). Decisions and deviations: `.agents/architecture_history.md`.
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
- [x] **C7, the `Simpiens.Core` assembly (2026-10-17).** Engine-free code is in `Assets/Simpiens.Core/` (asmdef `noEngineReferences: true`), engine-free validators in `Assets/Simpiens.Core.Testing/`; namespaces unchanged. The narrative formatter reads sector names from `ISectorRegistry` (the core no longer references the dashboard). Phase 0 gate: PlayMode 68/68 in 3 consecutive runs, economy soak within its bands, golden hashes identical for 3 seeds; the persistence smoke's Linux player boots and round-trips the outpost (t25, 18,603 bytes, as in Milestone 1).
- [x] **C8, the `dotnet/` solution (2026-10-17).** `Simpiens.Core` builds the same files for `netstandard2.1;net10.0` (C# 10 on netstandard2.1; the latest C# on net10.0, where the MemoryPack generator needs it), `Simpiens.Testing.Core` the engine-free harness, `Simpiens.Hosting` the `Microsoft.Extensions.DependencyInjection` registrations and `HeadlessSimulation`. A headless run reproduces Unity's golden hashes for all 3 seeds (3,000 ticks in about 0.4 s).
- [x] **C9, xUnit v3 tests; Phase 1 gate (2026-10-17).** `dotnet/Simpiens.Core.Tests` (xUnit v3, tests in sequence, `TestLog` to the test output) runs the 44 engine-free validators as tests, the golden-hash theory over every seed of Unity's baseline file, same-seed determinism, and the three Definition-of-Done scenarios headless on the real outpost with nothing injected: the outpost's day (9,000 ticks: every crawler closes a cycle, eats and sleeps; both seeded bounties, the Deep Seam charted before anyone knows its landmarks; trust rises; the ledger balances; nobody starves), the supply loop (sale t235, scout t382, gather t605, restock and regrowth, all by their deadlines) and save/load (saved mid-bounty at t394, loaded into a fresh session byte for byte with the same world hash, then 3,000 ticks played on). The visual anomaly detector stays Unity-only (it needs the views). The zero-allocation validators run under CoreCLR, which measures what Mono did not: an idle crawler's cognition allocated a closure every tick (176 bytes; the thread-pool hop now has its own method), and two validators measured heap size instead of allocations (now `GC.GetAllocatedBytesForCurrentThread`). The .NET core is always built optimized, so these validators check the shipping code. Evidence: `dotnet test` 53/53 in about 4 s, Debug and Release; PlayMode 68/68 after the changes; golden identical.
- [x] **C10, the console host (2026-10-17).** `dotnet/Simpiens.Host` (the `simpiens` command, on the Generic Host; Ctrl+C stops between ticks; simulation logs on standard error) plays a session (`run`: a line per sim-minute, the world hash at the end), judges the outpost's day on many seeds (`sweep`), runs the economy soak (`soak`: the three player profiles, any seeds, optional CSV) and checks or records golden hashes (`golden`); exit code 1 when a run fails. `EconomySoak` (the soak's profiles, samples and pace bands, as `EconomySoakScenario` asserts them) and `OutpostWatch` (the DoD observer, now with the day's checks) live in `Simpiens.Hosting`, shared by the host and the tests; the soak is a `dotnet test` theory. A development tool: it references the test harness for the golden hashes. Evidence: `simpiens soak` 9/9 within the bands in about 4 s (fair 664–768g, greedy trust up 0, generous broke by minute 10); `simpiens golden check` 3/3 seeds; `dotnet test` 62/62.
- [x] **C11, CI and close-out (2026-10-17).** `.github/workflows/dotnet.yml` restores, builds and tests the .NET solution on Ubuntu, then checks the golden hashes with the console host; it needs no Unity licence and runs on pushes to `main`, pull requests and by hand. Not pushed: it first runs when the producer pushes the branch. Its steps pass locally. Docs reconciled (ROADMAP, this epic, AGENTS.md, `curentfocus`, `architecture_history`, `docs/economy-model.md` §7); the working plan was removed. Evidence: PlayMode 68/68 (70, 2 explicit skipped); Unity golden check 3/3 seeds in fresh processes; `dotnet test` 62/62.

**Definition of Done, met:** (1) the validators pass in Unity (PlayMode) and under `dotnet test`, the zero-allocation ones included; (2) `OutpostSupplyLoopScenario`, `OutpostLifecycleScenario` and `SaveLoadScenario` pass in Unity, and their headless counterparts pass on the same session with nothing injected; the golden hashes are equal in both runtimes (3 seeds × 31 samples); (3) docs reconciled.

### Deferred (owned elsewhere)
- 64-bit `EntityId` (smaller saves and network messages): changes behaviour and the save format; belongs with the multiplayer wire format.

---

## 5. Risks

| Risk | Mitigation |
| :--- | :--- |
| Fixed-tick cognition shifts scenario outcomes | It is a deliberate behaviour change: full suite and economy soak at its gate; assertions change only with evidence |
| Mono and CoreCLR floats differ | Engine-free maths with explicit scalar formulas; per-part hash descriptions find the first difference. It happened (§6 F1): the golden seeds agree, others drift; each runtime is checked against itself beyond the golden file |
| The entity refactor changes iteration order | Spawn and registration order are kept; golden hashes after each sub-chunk |
| Scope creep (planner #8, UI) | Port behaviour as it is; other fixes land before or after with their own baseline |

---

## 6. Open findings

- **F1. Unity's Mono and CoreCLR round some float expressions differently (C9, 2026-10-17).** The 3 golden seeds are identical in both runtimes to t3,000, but seed 1 differs from t5 and seed 2 from t1,700, and the 30-minute economy soaks end on different numbers (fair, seed 20261009: 546g in Unity, 664g headless; every run of both is inside its pace band). Each runtime reproduces itself exactly (two Unity processes, two .NET processes). Cause, proven on the first difference: Unity's Mono keeps intermediates of a scalar float expression at double precision, CoreCLR rounds each step to float. `ResourceTargeting.TryPickSectorWaypoint` computes `k / 1000f * 2f - 1f`; for sector 1 and k = 429 the waypoint's y is `bf116873` in Unity, which only the double evaluation gives, and `bf116874` in .NET, which only the float evaluation gives. Vector maths agrees because each `Vec2` operator rounds when it stores its result; only chains of scalar operations differ. `MONO_ENV_OPTIONS=-O=float32` has no effect in the editor. **Consequences:** the golden file is a cross-runtime oracle for its 3 seeds only; compare other .NET runs with .NET runs (CoreCLR is the runtime Godot uses, so it becomes the reference after cutover). Not fixed: making every scalar chain round once is open-ended and changes Unity's behaviour, against this epic's scope rule. **Related, not measured:** `SimMath.Sin`, `Cos` and `Atan2` call the platform's C library, which can differ between operating systems even on CoreCLR. **Owner:** the multiplayer wire-up (server and clients must run the same runtime and maths) and the Godot spike (record a CoreCLR baseline when Unity stops being the reference).
- **F2. The outpost's day is not robust across seeds (C10, 2026-10-17).** `simpiens sweep` on 23 seeds (the 3 golden seeds and 1–20) with `OutpostLifecycleScenario`'s checks: 16 pass. Seeds 4, 10, 11, 12 and 18 have a crawler at hunger 95 or more for 300+ ticks (seed 11: three crawlers, 302–383 ticks); on seeds 8 and 16 one crawler never closes a harvest-to-sell cycle. Everything else holds on every seed: both bounties (scout t281–t382, gather t438–t1,493), the ledger, the fog of war, trust (9–12 of 12 up). Unity agrees where checked: its lifecycle scenario fails on seed 11 (SwarmAgent_4 302 ticks and SwarmAgent_10 383, as headless) and passes on seed 4 (which fails headless; the runtimes drift apart, F1). Only the default seed is asserted in either suite, so this predates Epic CORE as far as the suites can tell. Not changed here: food pacing is an economy decision (§3.3). **Owner:** Track A's soak work (ROADMAP §3A #8: a seed-sweep soak), with a producer decision on the pacing fix.

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
- 2026-10-17: C7 done (the core assembly); Phase 0 complete.
- 2026-10-17: C8 done (the dotnet solution).
- 2026-10-17: C9 done (xUnit v3 and the headless DoD scenarios; Phase 1 gate passed); finding F1 recorded.
- 2026-10-17: C10 done (the console host); finding F2 recorded.
- 2026-10-17: C11 done (CI, close-out). Epic CORE COMPLETED on its branch, pending the producer's review.
- 2026-10-17: merged to `main` as PR #5.
- 2026-10-18: the engine spike (Phase 2, desk research) chose KNI over Godot; the client is [Epic CLIENT](epic-client.md).

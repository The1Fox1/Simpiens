# Plan: Godot migration Phases 0 and 1 (Epic CORE)

- **Epic:** [Epic CORE: Engine-Free Core and Headless .NET](../docs/epics/epic-core.md). Status is owned by [ROADMAP.md](../docs/ROADMAP.md).
- **Source:** the migration write-up "Simpiens: Migrating from Unity to Godot on .NET 10" (Claude Docs, 2026-10-15), sections *Migration plan* and *Decisions needed before starting*. Its Phases 2–4 (Godot spike, view parity, cutover) are not in this plan.
- **Approved:** 2026-10-17. The producer approved Phases 0–1 as standalone work and gave the agent provisional authority over the technical decisions; the agent's decisions are reviewed on the branch.
- **Branch:** `feat/godot-migration`, from `main` at `dc47319`. One commit (or a few) per chunk, each with its measured test status.

## 1. Outcome

1. **Phase 0, Decouple (inside Unity).** The simulation runs without the engine: entities are plain data, cognition lands on fixed ticks, ids and maths are engine-free types, and the engine-free code compiles in an assembly that Unity forbids from referencing the engine.
2. **Phase 1, Headless .NET.** The same source files build for `net10.0`; a host steps ticks with no frames; xUnit v3 runs the validators and the Definition-of-Done scenarios under `dotnet test`; the world hashes match Unity's; CI needs no Unity.

## 2. Gates

| Gate | Checked by | Passes when |
| :--- | :--- | :--- |
| Every chunk | compile check; full PlayMode suite; from C2, `GoldenWorldHashScenario` | Both assemblies compile; every test passes (65/65 plus 2 explicit skipped at `dc47319`, plus new tests); the golden hashes are reproduced, unless the chunk is a deliberate behaviour change that re-records them in its own commit |
| Phase 0 (end of C7) | 3 consecutive full suites; economy soak; `Simpiens.Core` compiles with `noEngineReferences` | All green 3 times; soak 9/9 within the pace bands; golden equal |
| Phase 1 (end of C10) | `dotnet test`; the golden-hash test in .NET | Every test passes; the .NET hashes equal the Unity baseline file; the Unity suite is still green |

`DeterminismScenario` is only reliable inside the full suite today (ROADMAP §1A.2 #6). From C1 it must also pass alone, 3 runs out of 3.

## 3. Chunks

Each chunk gets a local implementation plan before it starts (what changes, files, tests, gate, risks) and is updated with what happened.

### Phase 0: Decouple

| Chunk | Work | Closes |
| :-: | :--- | :--- |
| **C0** | Setup: branch, epic doc, roadmap entries (Epic CORE `ACTIVE`; scan findings H1–M4 into §1A.2; §2 library decisions), this plan. | — |
| **C1** | **Cognition on fixed ticks.** A `CognitionScheduler`: due agents are prepared on the main thread at tick T as today; their evaluations run off-thread and return a `CognitionResult` instead of writing `AgentState`; at tick T + 1 the main thread waits for them and applies the results in agent-id order. The cognition path moves from UniTask to `ValueTask`/`Task`. | §1A.2 #6, #15; scan H2, M3 |
| **C2** | **Golden world-hash baselines.** 3 seeds × 30 samples (t100 to t3,000) of `WorldStateHash` on the outpost, in a committed text file; `GoldenWorldHashScenario` compares a fresh run, and `SIMPIENS_RECORD_GOLDEN=1` re-records. | — |
| **C3** | **`EntityId`.** An engine-free struct bit-compatible with `UnityEngine.GUID` (layout, ordering, hash code, hex string) replaces it everywhere; a validator proves the compatibility against `GUID`. | — |
| **C4** | **Engine-free maths.** `Vec2`, `GridPoint`, `RectF`, `Rgba` and `SimMath` reproduce Unity's formulas; engine-free code uses them; views convert at the boundary. | — |
| **C5** | **Entities as data.** Entity records keyed by `EntityId` replace `NodeController` as the simulation's handle (world registry, `SimulationManager`, intent handlers, execution context). The per-agent tick moves from `AutonomousAgent` into the core, driven by an explicit tick pipeline. `SwarmSpawner` splits into an engine-free world builder and a Unity view binder. `NodeController`, `AutonomousAgent` and `AgentView` only read state. Likely sub-chunks C5a–C5c. | §1A.2 #16 (scan M5) |
| **C6** | **Infrastructure out of the core.** No VContainer attributes or lifecycles, no UniTask, no ZLogger.Unity, `Application`, `Time` or `Debug` in engine-free code: the host hands in elapsed time, build info and log sinks. Validators throw on failure instead of logging errors, so any test framework can wrap them. | — |
| **C7** | **The `Simpiens.Core` assembly.** Engine-free code moves to `Assets/Simpiens.Core/` (asmdef `noEngineReferences: true`); engine-free validators and the headless harness move to a test-only engine-free assembly. Phase 0 gate. | — |

### Phase 1: Headless .NET

| Chunk | Work |
| :-: | :--- |
| **C8** | **`dotnet/` solution.** `Simpiens.Core.csproj` compiles the core's files by glob for `netstandard2.1;net10.0` at C# 10, with ZLogger, MemoryPack and `Microsoft.Extensions.Logging` from NuGet. `Simpiens.Hosting` (`net10.0`): `AddSimpiensCore()` for `Microsoft.Extensions.DependencyInjection`, and a headless simulation that steps ticks with no frames. |
| **C9** | **xUnit v3 tests.** Every engine-free validator as a test; headless DoD scenarios on the real outpost session with nothing injected (supply loop, lifecycle, save/load, determinism); the golden-hash test against Unity's baseline file. Phase 1 gate. |
| **C10** | **`Simpiens.Host`.** A console app on the Generic Host: run a session, soak with the three player profiles, sweep seeds, print hashes. The soak's pace bands hold headless. |
| **C11** | **CI and close-out.** A GitHub Actions workflow runs `dotnet test` (the agent does not push it). Docs reconciled: roadmap first, then the epic, `AGENTS.md` (layout, commands), `curentfocus`, `architecture_history`. This plan is deleted per the plan protocol. |

## 4. Rules for this work
- **Port behaviour as it is.** A fix that changes behaviour lands in its own commit, with its own baseline. On the path: #6, #15, #16, scan H2 and M3. Not on the path, recorded only: scan H1 and M4, the locked planner (#8).
- **Shared code stays at C# 10 and netstandard2.1 APIs** while Unity compiles it.
- **Namespaces don't change** when files move.
- **Zero allocations in hot paths** still hold in both runtimes; the zero-allocation validators run in both.

## 5. Risks

| Risk | Mitigation |
| :--- | :--- |
| The scheduler changes timing, so scenario outcomes shift | C1 is a deliberate behaviour change: full suite and soak at its gate; bounded assertions are adjusted only with evidence, recorded here |
| Mono and CoreCLR floats differ, so Gate 1 fails | Engine-free maths with explicit scalar formulas (no `System.Numerics`); find the first divergent sample with the per-part hash descriptions; if a runtime difference remains, record it and gate on each runtime's own baseline |
| The entity refactor (C5) breaks iteration order | Keep spawn and registration order; compare against the golden hashes after each sub-chunk |
| `.csproj`/`.slnx` are git-ignored for Unity | Add negations for `dotnet/` in `.gitignore` in C8 |
| A large mechanical diff hides a semantic change | Compiler-driven swaps (no lowercase members on the new types); golden hashes after every chunk |

## 6. Progress

| Chunk | Status | Commit | Tests |
| :-: | :--- | :--- | :--- |
| C0 | Done 2026-10-17 | `7bac9ac` | Docs only; not run. Baseline before any change: PlayMode 65/65 (67, 2 explicit skipped) |
| C1 | Done 2026-10-17 | `cc996f2` | PlayMode 66/66 (68, 2 explicit skipped); `DeterminismScenario` alone identical 3/3, milestones unchanged (first cycle 235, scout 382, gather 605, third sleeper 865); soak 9/9 in band |
| C2 | Done 2026-10-17 | `78c10b4` | PlayMode 67/67 (69, 2 explicit skipped); golden check 3/3 seeds in fresh processes; a corrupted baseline fails at its tick |
| C3 | Done 2026-10-17 | `ed62d1b` | PlayMode 68/68 (70, 2 explicit skipped); golden identical; `EntityIdCompatibilityValidator` passes |
| C4 | Done 2026-10-17 | `2144530` | PlayMode 68/68 (70, 2 explicit skipped); golden check 3/3 seeds identical |
| C5a | Done 2026-10-17 | `e06e283` | PlayMode 68/68 (70, 2 explicit skipped) on the rerun; the first run failed 1 (a validator set a property by reflection on the old type), fixed; golden identical in both |
| C5b | Done 2026-10-17 | `236412d` | PlayMode 68/68 (70, 2 explicit skipped); golden check 3/3 seeds identical |
| C6 | Done 2026-10-17 | `3a9f97b` | PlayMode 68/68 (70, 2 explicit skipped), golden identical; the final asmdef cleanup (UniTask references) and the smoke test conversion after that run were checked by compiling only |
| C7 | Done 2026-10-17 | `79391ba` | Phase 0 gate: PlayMode 68/68 ×3, soak exit 0, golden 3/3 seeds; persistence smoke not run (script not executable) |
| C8 | Done 2026-10-17 | (this commit) | `dotnet build` 0 errors; headless golden probe identical for 3 seeds (20261009 416 ms, 20261010 366 ms, 424242 475 ms) |

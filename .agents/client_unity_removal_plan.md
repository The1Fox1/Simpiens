# Epic CLIENT M2.8–M2.9: Unity's coverage moves to .NET, then Unity is removed (working plan)

Working document (AGENTS.md, documentation protocol 6): deleted when M2.9 closes, after its findings move to
`docs/epics/epic-client.md` and its decisions to `.agents/architecture_history.md`. Scope: epic-client §4 (moved there
from the M2 plan on 2026-10-24).

- **Go:** the producer, 2026-10-24: "remove Unity first", after signing off the side-by-side and the client's looks
  ("we can officially move away from Unity"). Epic 10 G3 waits until this is done.
- **Branch:** `feat/remove-unity`, cut from `docs/cleanup` (PR #11, which carries G1 and G2 into `main`). Its pull
  request targets `main` directly (no stacking); until #11 merges it also shows #11's commits.
- **Baseline** (`docs/cleanup` at `a6b4728`): `dotnet test` 355/355 (core 90, client 265); golden 3/3 in both runtimes;
  Unity PlayMode 69/69 (2 explicit skipped) at G2.

## M2.8: every Unity test gets a .NET twin or a recorded reason

New harness code goes into `dotnet/` only (`Simpiens.Hosting`, next to `OutpostWatch` and the soak), so nothing
changes for Unity's compiler while Unity still exists.

| Unity test | .NET twin | Chunk |
| :--- | :--- | :--- |
| `VisualAnomalyDetector` + its validator | an engine-free detector judged per tick (jitter, stacking, penetration, breaches, state thrashing on the simulation's state), with the validator's cases as unit tests; `OutpostWatch` reports every anomaly type, and the day's checks assert zero of each as Unity's lifecycle, supply-loop and save/load scenarios did | 1 |
| `SwarmNavigationScenario`, `ResourceContentionScenario` (Dev Map), `DynamicChaosScenario` + `EdgeCaseInjector` | headless scenarios on the detector; the injector registers a wall entity (no physics) | 2 |
| `PenCanaryScenario`, `BunkRestScenario`, `PriceResponseScenario` | headless scenarios, same assertions and bounds in ticks | 3 |
| `SessionCaptureScenario`, `SessionRestoreScenario` (live) | headless scenarios (the validators already run in .NET) | 3 |
| `PopulationScalingProbe` (explicit) | an explicit test | 3 |
| `OutpostLifecycle`, `OutpostSupplyLoop`, `SaveLoad`, `Determinism`, `GoldenWorldHash`, `EconomySoak` | already twinned (`OutpostScenarioTests`, `GoldenWorldHashTests`, `EconomySoakTests`) | — |
| `SessionSwitch`, `PlayerScout`, `MapHud`, `QuickSaveButtons`, `ShroudView` (camera part) | already in the client's tests (M2.3–M2.7) | — |
| `BountyTerminalValidator`, `FacilityZoneServiceValidator`, `CameraRigValidator`, `MapViewValidator`, `AgentSpriteValidator` | already in the client's tests (dashboard model, camera, tiles, the pinned art) | — |
| `EntityIdCompatibilityValidator` | dropped: it proves bit-compatibility with `UnityEngine.GUID`, which means nothing without Unity | — |
| The 60 engine-free validators | already run by `ValidatorTests` | — |

Each chunk: `dotnet test` green, one commit. Chunk 3 closes M2.8 with the inventory recorded in the epic.

## M2.9: Unity removed (its own commit, a pure move plus deletions)

4. **The move:** `git mv Assets/Simpiens.Core/** → dotnet/Simpiens.Core/`, `Assets/Simpiens.Core.Testing/** →
   dotnet/Simpiens.Testing.Core/`, without `.meta` and `.asmdef` files; the projects compile their own folders and stop
   globbing `Assets/`. Deleted: `Assets/`, `ProjectSettings/`, `Packages/`, the root `Simpiens.slnx` (Unity's), `.vscode/`
   (Unity's debugger), the Unity scripts (`run-playmode-tests.sh`, `golden-hashes.sh`, `run-economy-soak.sh`,
   `run-persistence-smoke.sh`, `update-memorypack.sh`, `update-zlogger.sh`), Unity's `.gitignore` and `.gitattributes`
   entries. The C# 10 and netstandard2.1 cap stays (a pure move; lifting it is a follow-up). The golden baselines stay:
   .NET reproduces them, and `simpiens golden record` re-records them from now on.
   Gate: `dotnet build`, `dotnet test` (same count as before the move), the golden check, the client in the pinned
   image, the MonoGame and Android builds.
5. **Words and docs:** test and comment wording that names Unity as the reference (`HeadlessRunReproducesUnitysGoldenHashes`,
   "as Unity's ... did" where the code no longer exists to compare); `AGENTS.md` rewritten for a .NET-only repo; the
   roadmap's rules that name Unity (§1A, §1B, §2), Track B and Epic CORE's F1 (ends); the epic, curentfocus and the
   decision log; this plan deleted.

## Risks

| Risk | Plan |
| :--- | :--- |
| The headless detector flags state thrashing Unity never saw (Unity judged the displayed state, which holds each state 3 ticks) | Measure first; if the raw state thrashes, judge it through the same 3-tick dwell (`CrawlerAppearance.MinDisplayTicks`) and record why |
| A Unity scenario's bound was set at Unity's frame pacing | Bounds are in ticks already, or converted at the scenario's multiplier; a port that fails is investigated, not loosened |
| Moving the sources breaks a path (golden baseline, font cache, container script) | The gate runs everything, including the pinned image, before the commit |
| Library/ and other Unity output stay on the producer's disk | Untracked; listed for the producer to delete by hand |

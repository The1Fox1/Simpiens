# Core refactor plan: SimulationManager's split, guards and the cleanup's loose ends

- **Status (producer, 2026-10-25; updated 2026-11-01 after Epic 10 Part 1):** an implementation plan, not an epic. **R0 is done** (`feat/code-cleanliness`, PR #14); **R7, the item system, is done** (`feat/items-i1-catalog`, PR #15); **Epic 10 Part 1 (G0–G6) is `COMPLETED`** (2026-11-01). **R1–R5 are next**, before Epic 10 Part 2 starts. The roadmap records this in §3A ("Core refactor"); keep this plan until R1–R6 are done or dropped, then close it out by the docs protocol.
- **Source:** a smaller agent's report, *Simpiens Solution Architecture & Code Smells Deep Dive* (2026-10-24, not in the repo), and its two commits on `feat/code-cleanliness`: `52f582f` (core fixes) and `e58073a` (Gum migration, 0 warnings). This plan re-checks the report against the code (re-audited 2026-11-01 after Epic 10 G6); §2 records which findings hold.
- **Complexity** ([rubric](rules/complexity-grading.md)): **Very Complex** as one plan, because R5 bundles three extractions. Run R5 as three separate PRs (**R5.1**, **R5.2**, **R5.3**). R1 is Possibly Complex (R1.1–R1.3 are Basic; R1.4 is Possibly Complex); R2 is Basic; R3, R4, R5.1, R5.2 and R5.3 are Possibly Complex. R6 is optional and Very Complex because it changes threading; it is too small to split, so the developer supervises it if run.
- **Rule for every phase:** behaviour-preserving unless the phase says otherwise. The golden hashes are the proof: a phase that changes them is not a refactor; stop and report.

---

## 1. The cleanup branch, verified

Re-run on `e58073a` (2026-10-25):

| Check | Result |
| :--- | :--- |
| `dotnet build dotnet/Simpiens.slnx -c Release --no-incremental` | 0 warnings, 0 errors (was 316 warnings) |
| `dotnet test dotnet/Simpiens.slnx` | 377/377 (core 110, client 267) |
| `golden check` | 3/3 |
| `scripts/test-client-in-container.sh` | 267/267, renders byte for byte |

What `52f582f` does, and what it leaves open:

- **#17, the tick's exception boundary.** Each intent's execution and completion is caught. A failed execution is aborted, and finished intents are always removed (`finally`). **Gap:** a caught fault is only logged at Error level. Nothing counts faults, so a test runs straight past one, and an Epic 10 handler that throws would ship green. Fixed in R0.
- **#19, bounty ids.** Generated ids come from `_nextBountyId`, which skips ids already taken. A registration with id 0, or with an id held by a live bounty, is refused.
  - **Gap 1:** an explicit id that matches a *finished* bounty still overwrites its record.
  - **Gap 2:** the counter isn't saved. On load it is derived as the highest saved id + 1. That is correct only because nothing ever removes a bounty from `_bounties` (the save keeps finished ones). No comment or check records that invariant.
- **`PathResponse` double return.** A rented flag, cleared with `Interlocked.Exchange`, makes a second return a no-op. Sound.
- **Succession tie-break.** `EntityId` order now breaks ties, so the comparator is total and the unstable sort no longer matters. Sound. Golden hashes are unchanged: no tie happens on the gate seeds.
- **`_gossipEventBuffer`.** Removed. Sound.
- **Tests:** `CleanlinessTests` (5 tests) sits in `Simpiens.Core.Tests` and is named after the cleanup, not a domain. By convention, mechanism tests are validators in `Simpiens.Testing.Core/<Domain>/`, run by `ValidatorTests`. Moved in R0.
- **Docs:** not reconciled. Roadmap §1A.2 still lists #17 and #19 as open, and `architecture_history.md` has no entry. Done in R0.
- **Authorship:** every local commit used the global git config's `brandon.fox@gmail.com`, mine included. Fixed 2026-10-25: the config is now `Brandon Fox <brandonslyfox@gmail.com>`, and this branch's unpushed commits were re-authored (hashes above are the re-authored ones). Published history is unchanged (no force-push).

`e58073a` (Gum types to `Gum.GueDeriving`, `RectangleRuntime`, `Typeface`, `AbsoluteWidth`) holds up: renders are byte for byte in the pinned image.

---

## 2. The report, checked against the code (re-audited 2026-11-01 after Epic 10 G6)

| Report § | Finding | Verdict | Action |
| :-: | :--- | :--- | :--- |
| 2.1 | `SimulationManager` is a god object | **Confirmed.** 2,022 lines after Epic 10 G6 (was 1,876 at G2); the next-largest core file is 616. Blocks: `IGuildHost` / `ISupplyHost` bridge 110–153, resources and surveys 163–250 & 357–367, bunks and departures 252–406, stalls and bounties 416–426 & 613–965, constructor and intent queue 429–600, persistence and reset 983–1182, tick 1184–1374, social (gossip, hearth, succession) 1376–1629, movement 1631–1981. | R2–R5.2 |
| 2.2 | §1A.2 #17, #19 | Fixed by `52f582f`, with the gaps in §1 | R0 (done) |
| 2.2 | §1A.2 #8, the locked `GoapPlanner` singleton | **Confirmed** (`SimpiensServiceCollectionExtensions.cs:57`, `GoapPlanner.cs:97`). Plans depend only on their inputs, so planners per worker shouldn't change behaviour. G5 measured +28% tick time during an expedition (13,835 ticks/s), well within budget. | R6, optional |
| 3.1 | `SuccessionEngine`'s plain `static` buffers | **Real, latent.** It runs on the simulation thread only, and `Simpiens.Core.Tests` disables parallelism (`TestSetup.cs`). It breaks only with two simulations ticking concurrently in one process, e.g. a future server. | R5.2 |
| 3.2 | Succession ties hash-ordered | Fixed by `52f582f` | — |
| 3.3 | `MentalBreakEvaluator` "mutates authoritative memory off-thread" and "never syncs back" | **Overstated.** In play it edits the agent's own `AgentMemorySnapshot` buffer inside `if (!hasKnownFood)`, right before returning an `IdleIntent` or `PanicIntent` (after which nothing reads that snapshot again before the next evaluation clears it). Authoritative forgetting happens on the main thread when `PanicIntent` completes (`AgentConsequenceService.cs:57`). The real smell is that `IAgentMemory` exposes mutable `Dictionary<K, V>` properties to evaluators. | R5.3 |
| 1A.1 #4 | `new AgentState()` uses an unseeded id | **Real, test-only.** The 5 callers are all validators (`EconomyCountersValidator.cs:129`, `AgentStateValidator.cs:41,70,92,133`); production passes seeded ids. | R1.3 |
| 4.1 | Cognition dispatch allocates (`CognitionJob`, `Task.Run` closure, the `OnComplete` lambda, intent classes) | **Real, per decision, not per tick.** Measured throughput is 11–24k ticks/s at 12–24 crawlers. `Task.Run` allocates whatever we pool. | Deferred to roadmap §3A #8 |
| 4.2 | Gossip scans the registry; hearth pairs are O(P²); `RemoveSorted` shifts | **Real, negligible at this scale.** Gossip pairs with the *first* partner in registry order, then stops (`break`). A spatial-grid query returns neighbours in another order, so it would change who gossips with whom and re-record the golden hashes. It is a behaviour change, not a refactor. | Deferred to §3A #8 |
| 5.1 | `PathResponse` double return | Fixed by `52f582f` | — |
| 5.2 | A* `MaxCells` 16,384 silently skips cells | **Real, latent.** Floor 1's grid after G5 is 48 × 42 = 2,016 cells (12% of the cap), and the Dev Map is smaller. A bigger floor (Epic 10 Part 2 W0.4 and Part 3) would lose paths with no error. | R1.2: refuse loudly at load |
| 5.3 | `WorldState` packs facts into a `ulong`; fact 64 wraps onto fact 0 | **Real, not near.** 40 of 64 facts are used (`StateFact` 0–39 after G5's `AtExpeditionWork`; roadmap §3A tracks the budget). Nothing guards the limit. | R1.1: a guard. Widen when a fact past 60 is planned. |
| 6.1 | `PruneStaleResourceMemories` wipes every resource memory | **Intended behaviour, wrong name.** A panicking crawler forgets its resource sightings and explores anew. `currentTick` is unused. | R5.3: rename it |
| 6.2 | Dead fields (`_gossipEventBuffer`, `ScriptedInput`) | Fixed by `52f582f` and `e58073a` | — |
| 6.3 | `SessionHost`'s four parallel crawler lists | **Real, low risk.** Only three sites change them (`SpawnCrawler`, `RemoveCrawlerAt`, `BuildStaticWorld`'s clear). | R1.4 |
| 6.4 | `Vec2 ==` approximate vs exact `Equals` | **Rejected.** Copied from Unity on purpose, and the golden baselines depend on it (`AGENTS.md`: keep them). | — |
| 7.1 | 316 warnings | Fixed by `e58073a` (verified 0) | R0: keep it at 0 (warnings as errors) |
| 7.2 | Nullable reference types off | **A choice, not a defect.** Enabling them across 500 files is its own project. | Producer decision (§5) |

---

## 3. Phases

Each phase is one PR into `main`, branched from `main` after the previous one merges (never stacked). Each must pass:
- `dotnet test dotnet/Simpiens.slnx`;
- `golden check` 3/3, **unchanged**;
- `scripts/test-client-in-container.sh` (byte for byte);
- `dotnet build dotnet/Simpiens.Desktop -p:XnaFramework=MonoGame`;
- `scripts/build-android.sh`;
- a build with 0 warnings.

### R0. Finish the cleanup (`feat/code-cleanliness`, ~0.5 day) — done 2026-10-25
1. **Faults are counted.** `SimulationManager` counts the intents it caught (count, plus the first fault's tick, agent, intent type and message). `OutpostWatch` fails the day on any fault, so every live scenario and the 23-seed gate assert zero.
2. **A validator for #17.** A handler that throws on one agent: later agents in the same tick still run, the finished intents are removed once, their `PathResponse` returns once, and the fault is counted.
3. **Bounty ids.**
   - ~~An explicit id that matches *any* bounty, finished ones included, is refused.~~ Not done: `EconomyZeroAllocValidator` reposts fixed ids every round to measure a steady state, and the bug was losing a *live* bounty's escrow, so only live ids are refused (as `52f582f` had it).
   - The id generator's skip loop gives up after one pass: a full board refuses the post instead of hanging.
   - A comment at `_nextBountyId` records why deriving it on load is exact.
   - The round-trip validator checks that the first generated id after a load equals the uninterrupted session's.
4. **Tests where the conventions put them:**
   - `CleanlinessTests` moves into validators: `PathResponse` to `Navigation/`, bounty ids to `Economy/`, succession ties to `Sociopolitical/`.
   - `ValidatorTests` runs them.
5. **0 warnings stays 0.** Set `TreatWarningsAsErrors` in `Directory.Build.props`. If the Android build's toolchain warns, scope the setting to the solution's projects.
6. **Docs:**
   - Roadmap §1A.2: #17 and #19 RESOLVED, with the commits.
   - An entry in `architecture_history`.
   - `AGENTS.md`: warnings are errors.

### R1. Guards (`feat/refactor-r1-guards`, ~0.5 day)
**Complexity:** Possibly Complex, because of R1.4; R1.1–R1.3 are Basic.
- [x] **R1.1 GOAP facts (`StateFact` < 64).** [Basic]
  - Add `public const int MaxFacts = 64;` on `WorldState` (`Cognition/Planning/WorldState.cs`) and `System.Diagnostics.Debug.Assert((uint)fact < MaxFacts, ...)` in `With`, `Without`, `Get` and `IsSet` (zero cost in Release).
  - Add a validator check in `Simpiens.Testing.Core/Planning/GoapDomainValidator.cs` asserting every `StateFact` enum value is `< WorldState.MaxFacts`.
  - Update `docs/ROADMAP.md` §3A fact budget to `40 of 64 facts used (StateFact 0–39, 2026-11-01)`.
- [x] **R1.2 The A* grid (`MaxCells` guard at map load).** [Basic]
  - Expose `public const int MaxCells = 16384;` on `GridAStarSearcher` (`Cognition/Pathfinding/GridAStarSearcher.cs`); keep its internal bounds checks (`totalGridCells > MaxCells`, `nCellIdx >= MaxCells`) as a defensive backstop.
  - In `WorldMap.Load(MapDefinition map)` (`Simulation/World/WorldMap.cs`), compute `var cells = map.PartitionCells(CellSize); int totalCells = cells.X * cells.Y;` before configuring the partition (running even when `_partition == null` so headless loads validate too) and throw `InvalidOperationException` when `totalCells > GridAStarSearcher.MaxCells`, naming the map, `cells.X * cells.Y` and `GridAStarSearcher.MaxCells`.
  - Add a validator test in `Simpiens.Testing.Core/World/WorldMapValidator.cs` that builds an oversized `MapDefinition` (e.g. 130 × 130 = 16,900 cells) and asserts `WorldMap.Load` throws `InvalidOperationException` naming both numbers.
- [x] **R1.3 Seeded `AgentState` ids.** [Basic]
  - Remove the parameterless `public AgentState() : this(EntityId.Generate())` constructor in `Cognition/AgentState.cs` so production code can never construct an `AgentState` without an explicit `EntityId`. `EntityId.Generate` stays for tests and tools.
  - Pass `EntityId.Generate()` at the 5 validator call sites: `EconomyCountersValidator.cs:129` and `AgentStateValidator.cs:41, 70, 92, 133`.
- [x] **R1.4 One crawler list in `SessionHost`.** [Possibly Complex: the record's order is the spawn order, which ticks, snapshots, saves and the golden hashes depend on; drifters add and remove crawlers mid-session]
  - Replace `_crawlers`, `_states`, `_names` and `_labels` in `Session/SessionHost.cs` (and `Session/SessionHost.Drifters.cs`) with one `List<CrawlerEntry> _crawlers = new List<CrawlerEntry>(1000)` of `private readonly record struct CrawlerEntry(AgentRuntime Runtime, AgentState State, string Name, string Label)`.
  - Keep `public IReadOnlyList<AgentRuntime> Crawlers`, `public IReadOnlyList<AgentState> CrawlerStates` and `public IReadOnlyList<string> CrawlerNames` with their exact types via preallocated, zero-allocation `IReadOnlyList<T>` view wrappers over `_crawlers` initialized once in `SessionHost`'s constructor, so external callers (`SessionBuilder`, `WorldStateHash`, `GuildPlayer`, `OutpostWatch`) don't change.
  - Update the three mutation sites (`SpawnCrawler`, `RemoveCrawlerAt`, `BuildStaticWorld`) and internal loops (`Respawn` ledger baseline, `GiveIdentity`, `Capture`, `TickCrawlers`, `TickPopulation`, `IsClearOfCrawlers`) to index `_crawlers`.

### R2. `SimulationManager` into partial files (`feat/refactor-r2-partials`, ~0.5 day, moves only)
**Complexity:** Basic. Moves only: no logic or signature changes, and the golden hashes and the tests prove it.
- [x] **R2.1 Split `SimulationManager.cs` (2,022 lines) into 7 partial files** [Basic] under `Simulation/`, following the precedent of `SessionHost.Drifters.cs`:
  - `SimulationManager.cs` (~390 lines): `ActiveIntentState`, `AnchorData`, core fields, constructor, `Dispose`, `Pause`/`Resume`, `SessionEpoch`, `Cognition`, the intent queue (`EnqueueIntent`, `ProcessQueuedIntents`, `AbortIntent`, `ExecuteIntent`, `CompleteIntent`, `RecordIntentFault`, `SortStable`, `EntityIdComparer`, `InsertSorted`, `RemoveSorted`), `ProcessInfluenceEvent`, `ExecuteCalmCrawlerCommand`, `SetEconomy`, `Tick`/`SimulateTick`, and the explicit `IGuildHost` / `ISupplyHost.PayGuildWages` bridge members added in Epic 10 G4–G5.
  - `SimulationManager.Market.cs` (~355 lines): `_stalls`, `_activeStallsBuffer`, `_bounties`, `_nextBountyId`, `_activeBountiesBuffer`, `_expiredBountiesBuffer`, `ActiveStalls`, `ActiveBounties`, `TryGetStall`, `UpdateStall`, `UpdateBounty`, `RegisterTradeStall`, `TryGetTradeStall`, `UpdateActiveStallsBuffer`, `RegisterBounty`, `TryRegisterBounty`, `ResolveSectorTarget`, `TryGetBounty`, `UpdateActiveBountiesBuffer`, `ExecutePostBountyCommand`, `TryTakeNextFreeBountyId`, `ExecuteCancelBountyCommand`, `ExecuteUpdateStallPricesCommand`, `PostBountyInternal`, `CancelBountyInternal`.
  - `SimulationManager.World.cs` (~125 lines): `_resources`, `_dormantNodes`, `_surveyBuffer`, `_harvested`, `RegisterResource`, `TryGetResource`, `RemoveResource`, `DepleteResource`, `ReactivateResource`, `MarkSectorExplored`, `PublishSurvey`, `FindMemory`, `RecordHarvest`, `HarvestedUnits`.
  - `SimulationManager.Lodging.cs` (~155 lines): `BunkClaimTolerance`, `BunkSettleDistance`, `_bunks`, `_bunkOccupants`, `_occupiedBunks`, `_bunkSleeps`, `_bunkScratch`, `_departingClaims`, `_departingBunks`, `RegisterBunk`, `BunkOccupants`, `BunkSleeps`, `TryClaimBunk`, `StepOntoBunk`, `DepartOutpost`, `ReleaseBunk`, `RecordBunkSleep`, `ValidateBunkOccupancy`, `RebuildOccupiedBunks`.
  - `SimulationManager.Social.cs` (~265 lines): `GossipRadius`, `_anchors`, `_hearthPawnsBuffer`, `_hearthAgentsBuffer`, `_activeAnchorsBuffer`, `RegisterAnchor`, `CheckGossipOpportunity`, `EvaluateHearthProximity`, `EvaluateLeadershipSuccession`.
  - `SimulationManager.Movement.cs` (~360 lines): `SidestepTurns`, `FlowTurns`, `CrowdSearchMargin`, `ExecutePanicMovement`, `TryStep`, `ExecutePathMovement`, `IsTurnBack` (added in G5.2), `ApplyProgressiveUnstuckReflex`, `TryDisplacePawn`, `IsCrowdedMove`, `IsPositionBlocked`.
  - `SimulationManager.Persistence.cs` (~205 lines): `CaptureTo`, `RestoreFrom`, `RestoreResources`, `HideResourceNode`, `ResetState`.

**Why first:** each later extraction then diffs one small partial file instead of a 2,022-line class, and agents stop loading 2,022 lines to touch one domain.

### R3. Extract locomotion (`feat/refactor-r3-locomotion`, ~0.5 day)
**Complexity:** Possibly Complex by rubric (touches a zero-allocation hot path with exact float casts), with a complete mechanical blueprint below so a **Basic** agent can execute R3.1 and R3.2 with zero design calls.
- **Branch note:** R3 depends on R2's `SimulationManager.Movement.cs` partial split (`0fa3555`). Confirm `dotnet/Simpiens.Core/Simulation/SimulationManager.Movement.cs` is present on the working branch before starting (either branched from `main` after R1–R2 merge, or on the branch the developer designates).

- [x] **R3.1 Create `dotnet/Simpiens.Core/Simulation/Movement/Locomotion.cs`.** [Possibly Complex: hot-path math and float casts; mechanical recipe below for Basic-agent execution]
  - **Namespace & type:** `namespace Simpiens.Simulation.Movement`, `internal sealed class Locomotion` (`Simpiens.Core/AssemblyInfo.cs` already grants `InternalsVisibleTo` to `Simpiens.Testing.Core`, `Simpiens.Hosting` and `Simpiens.Core.Tests`, while keeping `Locomotion` hidden from client views).
  - **Exact `using` directives:**
    ```csharp
    using Simpiens.Cognition;
    using Simpiens.Cognition.Contracts;
    using Simpiens.Cognition.Pathfinding;
    using Simpiens.Diagnostics;
    using Simpiens.Simulation.Spatial;
    using ZLogger;
    ```
  - **Fields & constructor (owns no mutable state):**
    ```csharp
    private static readonly Vec2[] SidestepTurns =
    {
        new Vec2(0.70710677f, 0.70710677f), new Vec2(0.70710677f, -0.70710677f), new Vec2(0f, 1f), new Vec2(0f, -1f)
    };

    private static readonly Vec2[] FlowTurns =
    {
        new Vec2(0.70710677f, -0.70710677f), new Vec2(0.70710677f, 0.70710677f)
    };

    private const float CrowdSearchMargin = 0.5f;

    private readonly ISpatialPartition _spatialPartition;
    private readonly IWorldRegistry _worldRegistry;
    private readonly ISimulationClock _clock;

    public Locomotion(ISpatialPartition spatialPartition = null, IWorldRegistry worldRegistry = null, ISimulationClock clock = null)
    {
        _spatialPartition = spatialPartition;
        _worldRegistry = worldRegistry;
        _clock = clock;
    }
    ```
  - **Methods moved verbatim from `SimulationManager.Movement.cs` (preserving all existing comments and XML docstrings):**
    - `public IntentResult ExecutePanicMovement(WorldEntity node, PathResponse path, ref int currentIndex)`
    - `private bool TryStep(WorldEntity node, Vec2 targetPos, float step, Vec2[] turns)`
    - `public IntentResult ExecutePathMovement(WorldEntity node, PathResponse path, ActiveIntentState state)`
    - `private bool IsTurnBack(Vec2 position, Vec2 waypoint, Vec2 next)`
    - `public IntentResult ApplyProgressiveUnstuckReflex(WorldEntity node, Vec2 targetPos, ActiveIntentState state)`
    - `public bool TryDisplacePawn(WorldEntity node, Vec2 candidate, bool respectPawns = true)`
    - `public bool IsCrowdedMove(WorldEntity node, Vec2 candidate)`
    - `public bool IsPositionBlocked(Vec2 targetPos, EntityId ignoreAgentId, float pawnClearanceRadius = 0.4f)`
  - **Hard invariants (do NOT "clean up" or simplify any expression):**
    1. **Constant qualification:** Replace the 3 unqualified `FixedTickDeltaTime` references (`ExecutePanicMovement` step, `ExecutePathMovement` `state.TimeSinceLastProgress += ...`, and `ExecutePathMovement` step) with `SimulationManager.FixedTickDeltaTime`.
    2. **Exact float/vector casts (golden hashes depend on them):**
       - `float step = (float)(speed * SimulationManager.FixedTickDeltaTime);` (both movement methods);
       - `if (node?.Agent != null && node.Agent.IsDowned) speed = (float)(speed * Vitality.DownedSpeedFactor);`;
       - `(Vec2)node.Position` casts in `TryStep` and `ApplyProgressiveUnstuckReflex`, and `Vec2 turned = dir * turn.X + new Vec2(-dir.Y, dir.X) * turn.Y;` in `TryStep`;
       - `int cellX = SimMath.FloorToInt((float)((float)(targetPos.X + (float)(snapshot.Width * snapshot.CellSize * 0.5f)) / snapshot.CellSize));` (and `cellY`) in `IsPositionBlocked`.
    3. **Null-safety rules:** `_spatialPartition == null` or `snapshot == null` means unblocked (`IsPositionBlocked` and `IsCrowdedMove` return `false`; `TryDisplacePawn` skips static/crowd checks and still calls `node.SetPosition(candidate)` when `node != null`); `node == null` returns `false` in `TryDisplacePawn` and `IsCrowdedMove`; `IsCrowdedMove` falls back to `entity.Position` when `_worldRegistry == null` or returns `null`; Tier 3 blacklisting uses `uint currentTick = _clock != null ? (uint)_clock.CurrentTick : 0;`.

- [x] **R3.2 Forward from `SimulationManager.Movement.cs` and validate.** [Basic]
  - **DI trap — do NOT add `Locomotion` to `SimulationManager`'s constructor parameters:** `SimpiensServiceCollectionExtensions.AddSimpiensCore` registers `services.AddSingleton<SimulationManager>()` without a factory lambda. Instead, in `dotnet/Simpiens.Core/Simulation/SimulationManager.cs`:
    - Add `using Simpiens.Simulation.Movement;` and field `private readonly Locomotion _locomotion;`.
    - Inside `SimulationManager(...)`, right after `_clock = clock;`, initialize `_locomotion = new Locomotion(_spatialPartition, _worldRegistry, _clock);`.
  - **Replace `dotnet/Simpiens.Core/Simulation/SimulationManager.Movement.cs` with the 6 thin forwarding methods** (`TryStep`, `IsTurnBack`, `SidestepTurns`, `FlowTurns` and `CrowdSearchMargin` have zero external callers and stay only in `Locomotion.cs`):
    ```csharp
    using Simpiens.Cognition.Contracts;
    using Simpiens.Cognition.Pathfinding;

    namespace Simpiens.Simulation
    {
        public partial class SimulationManager
        {
            public IntentResult ExecutePanicMovement(WorldEntity node, PathResponse path, ref int currentIndex)
                => _locomotion.ExecutePanicMovement(node, path, ref currentIndex);

            public IntentResult ExecutePathMovement(WorldEntity node, PathResponse path, ActiveIntentState state)
                => _locomotion.ExecutePathMovement(node, path, state);

            internal IntentResult ApplyProgressiveUnstuckReflex(WorldEntity node, Vec2 targetPos, ActiveIntentState state)
                => _locomotion.ApplyProgressiveUnstuckReflex(node, targetPos, state);

            public bool TryDisplacePawn(WorldEntity node, Vec2 candidate, bool respectPawns = true)
                => _locomotion.TryDisplacePawn(node, candidate, respectPawns);

            internal bool IsCrowdedMove(WorldEntity node, Vec2 candidate)
                => _locomotion.IsCrowdedMove(node, candidate);

            internal bool IsPositionBlocked(Vec2 targetPos, EntityId ignoreAgentId, float pawnClearanceRadius = 0.4f)
                => _locomotion.IsPositionBlocked(targetPos, ignoreAgentId, pawnClearanceRadius);
        }
    }
    ```
    This satisfies `IIntentExecutionContext`, `StepOntoBunk` (`SimulationManager.Lodging.cs`), `DriveSystemValidator`, `MotorWatchdogValidator` and `NavigationClearanceValidator` with zero caller edits.
  - **Add standalone validator test in `dotnet/Simpiens.Testing.Core/Navigation/MotorWatchdogValidator.cs`:**
    - Add `using Simpiens.Simulation.Movement;`, add `ValidateStandaloneLocomotion()` to `Start()`, and update the summary log from `ALL 5 TESTS` to `ALL 6 TESTS`.
    - Construct `WorldEntity` directly via `new WorldEntity(id, EntityType.Pawn, pos, 0.35f, agent: new AgentState(id))` (do NOT use `HeadlessEntities.Crawler`, which requires a `SimulationManager`) and verify on `Locomotion` alone:
      1. `new Locomotion()` (null partition/registry/clock): `TryDisplacePawn` moves the pawn and returns `true`; `IsPositionBlocked` and `IsCrowdedMove` return `false`.
      2. Wired `new Locomotion(grid, registry, clock)` (`SpatialHashGrid` `Initialize(20, 20, 1f)` with a wall cell set via `grid.StaticGrid.WorldToCell(new Vec2(0.5f, 0.5f), out int cx, out int cy)` + `grid.StaticGrid.SetWall(cx, cy)`, `occupant` registered at `(3f, 0f)`, `mover` registered at `(1f, 0f)`, `grid.UpdateFromRegistry(registry)`, `clock.AdvanceTicks(50)`):
         - `locomotion.TryDisplacePawn(mover, new Vec2(0.5f, 0.5f))` returns `false` (wall rejected);
         - with `mover` placed at `(2.7f, 0f)`, `IsCrowdedMove(mover, new Vec2(2.8f, 0f))` is `true` and `IsCrowdedMove(mover, new Vec2(2.5f, 0f))` is `false` (stepping apart allowed);
         - resetting `mover` to `(1f, 0f)` and stepping `ExecutePathMovement` toward a 1-waypoint `PathResponse` at `(3f, 0f)` (calling `grid.UpdateFromRegistry(registry)` each tick) finishes `IntentResult.Success` with `Vec2.Distance(mover.Position, occupant.Position) >= 0.35f`;
         - Tier 3 `ApplyProgressiveUnstuckReflex` (`StallCount = 4`, `Intent = new HarvestResourceIntent(mover.Id, resourceId, default)`) returns `IntentResult.PathBlocked` and blacklists `resourceId` until tick `350` (`mover.Agent.Memory.IsBlacklisted(resourceId, 349)` is `true`, `IsBlacklisted(resourceId, 350)` is `false`).
  - **Verify all §3 gates** (`dotnet test dotnet/Simpiens.slnx`, `golden check` 3/3 unchanged, `scripts/test-client-in-container.sh`, MonoGame compile, Android compile, 0 warnings) and commit as `refactor(core): extract Locomotion from SimulationManager (R3)`.

### R4. Extract the market (`feat/refactor-r4-market`, ~0.5 day)
**Complexity:** Possibly Complex by rubric (escrow through the ledger, the `EconomySave` block moved with its byte layout kept, and deterministic expiry/buffer order), with a complete mechanical blueprint below so a **Basic** agent can execute R4.1 and R4.2 with zero design calls.
- **Branch note:** R4 depends on R2's partial split (`SimulationManager.Market.cs`, `SimulationManager.Lodging.cs`, `SimulationManager.Persistence.cs`). Confirm those files exist on the working branch before starting.

- [x] **R4.1 Create `dotnet/Simpiens.Core/Simulation/Economy/Market.cs`.** [Possibly Complex: escrow, save block and deterministic buffer ordering; mechanical recipe below for Basic-agent execution]
  - **Why stalls and bounties go together:** escrow is locked from and refunded to a stall's treasury through `IEconomyLedger`.
  - **Step 1 — Expose `SimulationManager.SortStable` (`SimulationManager.cs:271`):** change `private static void SortStable<T>(List<T> list, System.Comparison<T> compare)` to `internal static void SortStable<T>(...)` so `Market` (and later `SocialDynamics` in R5.2) calls `SimulationManager.SortStable(...)` directly without duplicating it.
  - **Step 2 — Create `dotnet/Simpiens.Core/Simulation/Economy/Market.cs`:**
    - **Namespace & type:** `namespace Simpiens.Simulation.Economy`, `internal sealed class Market`.
    - **Exact `using` directives:**
      ```csharp
      using System;
      using System.Collections.Generic;
      using Simpiens.Diagnostics;
      using Simpiens.Intervention;
      using Simpiens.Narrative;
      using Simpiens.Simulation.Items;
      using Simpiens.Simulation.Persistence;
      using Simpiens.Simulation.World;
      using ZLogger;
      ```
    - **Hard architectural guard (`ItemNamesTests`):** `ItemNamesTests.ProductionCodeNamesItemsOnlyWhereAllowed` regex-scans the raw text of every `.cs` file in `Simpiens.Core` (comments included!) for `\bItemType\.<Name>\b` other than `ItemType.None`. **Never write `ItemType.IronOre`, `ItemType.Ration`, etc. anywhere in `Market.cs`, not even in a comment.** Keep the Gather default-item expression and its comment verbatim:
      ```csharp
      // A Gather bounty without an item asks for the first resource (iron ore)
      itemType == ItemType.None && resolvedType == BountyType.GatherResource ? ItemCatalog.InCategory(ItemCategory.Resource)[0] : itemType,
      ```
    - **Fields & constructor:**
      ```csharp
      private readonly IEconomyLedger _economyLedger;
      private readonly ISectorRegistry _sectors;
      private readonly INarrativeChronicleService _chronicleService;

      private readonly Dictionary<EntityId, TradeStallData> _stalls = new Dictionary<EntityId, TradeStallData>();
      private readonly List<TradeStallData> _activeStallsBuffer = new List<TradeStallData>(8);
      private readonly Dictionary<ushort, BountyContract> _bounties = new Dictionary<ushort, BountyContract>();
      // The next generated bounty id. Not saved: a load derives it as the highest saved id + 1, which is exact because
      // nothing removes a bounty from _bounties (finished ones stay, and are saved) and every id taken raises it (until
      // the 65,535 ids run out and generation wraps to fill gaps)
      private ushort _nextBountyId = 1;
      private readonly List<BountyContract> _activeBountiesBuffer = new List<BountyContract>(16);
      private readonly List<ushort> _expiredBountiesBuffer = new List<ushort>(16);
      private readonly List<ushort> _departingClaims = new List<ushort>(2);

      public Market(
          IEconomyLedger economyLedger = null,
          ISectorRegistry sectors = null,
          INarrativeChronicleService chronicleService = null)
      {
          _economyLedger = economyLedger;
          _sectors = sectors;
          _chronicleService = chronicleService;
      }
      ```
    - **Exact members moved into `Market` (preserving all existing comments, log lines and chronicle events):**
      - Properties: `public IReadOnlyList<TradeStallData> ActiveStalls => _activeStallsBuffer;` and `public IReadOnlyList<BountyContract> ActiveBounties => _activeBountiesBuffer;`.
      - Stall operations (from `SimulationManager.Market.cs`):
        - `public void RegisterTradeStall(TradeStallData stallData)`
        - `public bool TryGetStall(EntityId stallId, out TradeStallData stall)`
        - `public void UpdateStall(TradeStallData stall)`
        - `public void UpdateStallPrices(in UpdateStallPricesCommand cmd)` (body of `ExecuteUpdateStallPricesCommand`)
        - `private void UpdateActiveStallsBuffer()` (calls `SimulationManager.SortStable(_activeStallsBuffer, (a, b) => a.StallEntityId.CompareTo(b.StallEntityId));`)
      - Bounty operations (from `SimulationManager.Market.cs`):
        - `public void RegisterBounty(BountyContract bounty)`
        - `public bool TryRegisterBounty(BountyContract bounty)`
        - `public bool TryGetBounty(ushort bountyId, out BountyContract bounty)`
        - `public void UpdateBounty(BountyContract bounty)`
        - `public void PostBounty(in PostBountyCommand cmd, uint currentTick)` (body of `ExecutePostBountyCommand`, calling `private bool TryTakeNextFreeBountyId(out ushort id)` and `private void PostBountyInternal(...)`; qualify `SimulationManager.PlayerEntityId` in the `ChronicleEvent`)
        - `public void CancelBounty(ushort bountyId, uint currentTick)` (body of `CancelBountyInternal`; qualify `SimulationManager.PlayerEntityId` in the `ChronicleEvent`)
        - `private Vec2 ResolveSectorTarget(byte sectorId)`
        - `private void UpdateActiveBountiesBuffer()` (calls `SimulationManager.SortStable(_activeBountiesBuffer, (a, b) => a.BountyId.CompareTo(b.BountyId));`)
      - Bounty expiry (moved verbatim from `SimulationManager.cs` `SimulateTick` lines 435–485):
        - `public void ExpireBounties(uint currentTick)`: early-returns when `_bounties.Count == 0` or `_expiredBountiesBuffer.Count == 0`; sorts `_expiredBountiesBuffer` via `SimulationManager.SortStable(_expiredBountiesBuffer, (a, b) => a.CompareTo(b));`; refunds escrow (with the `else` fallback when `_economyLedger == null`), sets `BountyStatus.Expired`, logs, records `ChronicleEventType.BountyExpired` with `SimulationManager.PlayerEntityId`; and calls `UpdateActiveBountiesBuffer(); UpdateActiveStallsBuffer();` once after the loop.
      - Departing drifter claim release (moved from `SimulationManager.Lodging.cs` `DepartOutpost` lines 78–87):
        - `public int ReleaseClaimsForAgent(EntityId agentId)`: clears `_departingClaims`, collects claimed bounty ids in `_activeBountiesBuffer` where `bounty.Status == BountyStatus.Claimed && bounty.ClaimantId == agentId`, reopens each via `UpdateBounty(claimed.WithClaimant(default, BountyStatus.Open))`, and **returns `_departingClaims.Count`** so `DepartOutpost`'s log line keeps the exact released count.
      - Persistence & reset (moved from `SimulationManager.Persistence.cs`):
        - `public EconomySave Capture()`: copies `_stalls` to `TradeStallData[]` and `Array.Sort(stalls, (a, b) => a.StallEntityId.CompareTo(b.StallEntityId))`; copies `_bounties` to `BountyContract[]` and `Array.Sort(bounties, (a, b) => a.BountyId.CompareTo(b.BountyId))`; captures `var ledger = new LedgerSave(); _economyLedger?.CaptureTo(ledger);`; returns `new EconomySave { Stalls = stalls, Bounties = bounties, Ledger = ledger }`.
        - `public void RestoreFrom(EconomySave economy)`: clears `_stalls`, populates from `economy?.Stalls`, calls `UpdateActiveStallsBuffer()`; clears `_bounties`, resets `_nextBountyId = 1`, populates from `economy?.Bounties` advancing `if (b.BountyId >= _nextBountyId) _nextBountyId = (ushort)(b.BountyId + 1);`, calls `UpdateActiveBountiesBuffer()`; and calls `if (economy?.Ledger != null) _economyLedger?.RestoreFrom(economy.Ledger);`.
        - `public void Reset()`: clears `_stalls`, `_activeStallsBuffer`, `_bounties`, sets `_nextBountyId = 1`, clears `_activeBountiesBuffer`, `_expiredBountiesBuffer` and `_departingClaims`.
    - **Zero-allocation invariant (`EconomyZeroAllocValidator` measures 1,000 steady-state ticks expecting 0 bytes):** keep every `Comparison<T>` lambda passed to `SimulationManager.SortStable` stateless (capturing no local variables and no `this`) so the C# compiler caches each delegate in a static field.

- [x] **R4.2 Forward from `SimulationManager` and validate.** [Basic]
  - **Trap 1 — Constructor ordering & DI (`SimulationManager.cs`):**
    - Do NOT add `Market` to `SimulationManager`'s constructor parameter list (`AddSingleton<SimulationManager>()` relies on DI).
    - Add `private readonly Market _market;` on `SimulationManager`.
    - Inside `SimulationManager(...)`, construct `_market = new Market(_economyLedger, _sectors, _chronicleService);` **AFTER** `_economyLedger = economyLedger ?? new EconomyLedger();`, passing the **`_economyLedger` field** (NEVER the raw `economyLedger` parameter, which is `null` in many callers — `_market` and `sim.EconomyLedger` must share the exact same fallback `EconomyLedger` instance!).
  - **Trap 2 — Dual stall getters in `dotnet/Simpiens.Core/Simulation/SimulationManager.Market.cs`:**
    - `ISimulationManager` requires `TryGetTradeStall` while `IIntentExecutionContext` requires `TryGetStall`. Forward **both** to `_market.TryGetStall(...)`. Replace the entire body of `SimulationManager.Market.cs` with:
      ```csharp
      using System.Collections.Generic;
      using Simpiens.Intervention;
      using Simpiens.Simulation.Economy;

      namespace Simpiens.Simulation
      {
          public partial class SimulationManager
          {
              public IReadOnlyList<TradeStallData> ActiveStalls => _market.ActiveStalls;
              public IReadOnlyList<BountyContract> ActiveBounties => _market.ActiveBounties;

              public bool TryGetStall(EntityId stallId, out TradeStallData stall) => _market.TryGetStall(stallId, out stall);

              public bool TryGetTradeStall(EntityId stallId, out TradeStallData stallData) => _market.TryGetStall(stallId, out stallData);

              public void UpdateStall(TradeStallData stall) => _market.UpdateStall(stall);

              public void UpdateBounty(BountyContract bounty) => _market.UpdateBounty(bounty);

              public void RegisterTradeStall(TradeStallData stallData) => _market.RegisterTradeStall(stallData);

              public void RegisterBounty(BountyContract bounty) => _market.RegisterBounty(bounty);

              public bool TryRegisterBounty(BountyContract bounty) => _market.TryRegisterBounty(bounty);

              public bool TryGetBounty(ushort bountyId, out BountyContract bounty) => _market.TryGetBounty(bountyId, out bounty);

              private void ExecutePostBountyCommand(in PostBountyCommand cmd, uint currentTick) => _market.PostBounty(in cmd, currentTick);

              private void ExecuteCancelBountyCommand(in CancelBountyCommand cmd, uint currentTick) => _market.CancelBounty(cmd.BountyId, currentTick);

              private void ExecuteUpdateStallPricesCommand(in UpdateStallPricesCommand cmd) => _market.UpdateStallPrices(in cmd);
          }
      }
      ```
  - **Update `SimulateTick` in `dotnet/Simpiens.Core/Simulation/SimulationManager.cs`:**
    - Replace the 50-line bounty expiration block (`if (_bounties.Count > 0) { ... }`) with `_market.ExpireBounties(currentTick);`.
    - At the end of `SimulateTick`, update `_spatialPartition.UpdateFromRegistry(_worldRegistry, _groupRegistry, _activeStallsBuffer, _activeBountiesBuffer);` to pass `_market.ActiveStalls, _market.ActiveBounties`.
  - **Trap 3 — Remove dead `_departingClaims` field in `dotnet/Simpiens.Core/Simulation/SimulationManager.Lodging.cs` (`TreatWarningsAsErrors`):**
    - Delete `private readonly List<ushort> _departingClaims = new List<ushort>(2);` (and unused `using Simpiens.Simulation.Economy;`) from `SimulationManager.Lodging.cs` so `CS0414`/unused-field warnings never fire.
    - In `DepartOutpost`, replace the `_departingClaims` loops with `int releasedClaims = _market.ReleaseClaimsForAgent(node.Id);` and update the log line at the end of `DepartOutpost` to `({releasedClaims} claim(s) released)`.
  - **Trap 4 — Local `int n` in `dotnet/Simpiens.Core/Simulation/SimulationManager.Persistence.cs`:**
    - In `CaptureTo`: replace the stalls/bounties/ledger lines (lines 17–29) with `save.Economy = _market.Capture();`, and change `n = 0;` right before the `resources` loop to **`int n = 0;`** (because `int n = 0;` was previously declared inside the removed stalls block!). Remove unused `using Simpiens.Simulation.Economy;` at line 1.
    - In `RestoreFrom`: replace lines 79–102 with `_market.RestoreFrom(save.Economy);`.
    - In `ResetState`: keep `_economyLedger?.Reset();` (the ledger also tracks non-market faucets/sinks) and replace `_stalls.Clear(); _activeStallsBuffer.Clear(); _bounties.Clear(); _nextBountyId = 1; _activeBountiesBuffer.Clear();` with `_market.Reset();`.
  - **Add standalone validator test in `dotnet/Simpiens.Testing.Core/Economy/BountyFulfillmentValidator.cs`:**
    - Add `ValidateStandaloneMarket()` to `Start()` and update the summary log from `ALL 11 DOMAIN TESTS` to `ALL 12 DOMAIN TESTS`.
    - Exercise `new Market(ledger, sectors: null, new Simpiens.Narrative.NarrativeChronicleService())` directly with **no `SimulationManager`**:
      1. Register two stalls out of `EntityId` order (`stallB = new EntityId(2, 0, 0, 0)` with `100g`, then `stallA = new EntityId(1, 0, 0, 0)` with `200g`); assert `market.ActiveStalls.Count == 2` and `market.ActiveStalls[0].StallEntityId == stallA`. Apply `market.UpdateStallPrices(new UpdateStallPricesCommand(stallA).With(ItemType.Ration, 19))` and assert `RationSellPrice == 19`.
      2. Call `market.PostBounty(new PostBountyCommand(0, BountyType.GatherResource, 0, ItemType.IronOre, 1, 50, durationTicks: 20, default, default), currentTick: 10)` -> auto-assigns id `1`, defaults `CreatorStallId` to `stallA`, deducts `50g` into `ledger.TotalEscrowGold == 50` (`stallA` treasury `150g`). Post a second bounty with explicit `bountyId: 3`, `rewardGold: 40`, `durationTicks: 100`, `creatorStallId: stallA` at `currentTick: 10` (`ledger.TotalEscrowGold == 90`, treasury `110g`), and claim `#3` for `drifterId = new EntityId(9, 0, 0, 0)` via `market.UpdateBounty(...)`.
      3. Call `int released = market.ReleaseClaimsForAgent(drifterId);` -> assert `released == 1` and `market.TryGetBounty(3, out var reopened) && reopened.Status == BountyStatus.Open && reopened.ClaimantId == default`.
      4. Call `market.ExpireBounties(35);` -> bounty `#1` expires, `50g` refunds to `stallA` (`treasury == 160`, `ledger.TotalEscrowGold == 40`), `market.ActiveBounties.Count == 1` (only `#3`), and `market.TryGetBounty(1, out var expired) && expired.Status == BountyStatus.Expired`.
      5. Round-trip `var saved = market.Capture();` into a fresh `var restoredLedger = new EconomyLedger(); var restored = new Market(restoredLedger); restored.RestoreFrom(saved);` -> assert `restored.ActiveStalls.Count == 2`, `restored.ActiveBounties.Count == 1`, `restored.TryGetBounty(1, out var savedExp) && savedExp.Status == BountyStatus.Expired`, `restoredLedger.TotalEscrowGold == 40`, and a new `restored.PostBounty` with `bountyId: 0` receives id `4` (`highest + 1`).
      6. Call `restored.Reset();` -> assert `restored.ActiveStalls.Count == 0`, `restored.ActiveBounties.Count == 0`, and `!restored.TryGetBounty(1, out _)`.
  - **Verify all §3 gates** (`dotnet test dotnet/Simpiens.slnx`, `golden check` 3/3 unchanged, `scripts/test-client-in-container.sh`, MonoGame compile, Android compile, 0 warnings) and commit as `refactor(core): extract Market from SimulationManager (R4)`.

### R5.1. Extract lodging (`feat/refactor-r5-1-lodging`, ~0.3 day)
**Complexity:** Possibly Complex: bunk claims, `StepOntoBunk` movement through `Locomotion`, and preemption stand-up in `ValidateOccupancy`.
- [ ] **R5.1.1 Create `Simulation/World/Lodging.cs`.** [Possibly Complex: occupancy validation mutates preempted sleepers' `VisualState`]
  - Owns `BunkClaimTolerance` (`0.25f`), `BunkSettleDistance` (`0.02f`), `_bunks`, `_bunkOccupants`, `_occupiedBunks`, `_bunkSleeps`, `_bunkScratch` and `_departingBunks`.
  - Moves `RegisterBunk`, `BunkOccupants`, `BunkSleeps`, `OccupiedBunks`, `TryClaimBunk`, `StepOntoBunk` (calling `Locomotion.IsCrowdedMove` and `Locomotion.TryDisplacePawn`), `ReleaseBunk`, `ReleaseAllForAgent` (called by `DepartOutpost`), `RecordBunkSleep`, `ValidateOccupancy(IReadOnlyDictionary<EntityId, ActiveIntentState> activeIntents, IAgentRegistry agentRegistry)`, `CaptureSleeps`/`RestoreSleeps` (`IdCountRecord[]` sorted by `Id`), and `Reset()`.
  - `DepartOutpost(WorldEntity node)` stays on `SimulationManager` as the coordinator across `_economyLedger` (`DrifterDepartures`), `_market.ReleaseClaimsForAgent`, `_lodging.ReleaseAllForAgent`, `_groupRegistry.LeaveAllGroups`, `Guild.Forget` and `_chronicleService`.
- [ ] **R5.1.2 Forward from `SimulationManager.Lodging.cs` and validate.** [Basic]
  - `SimulationManager` forwards `ISimulationManager` and `IIntentExecutionContext` bunk members to `_lodging`, and calls `_lodging.ValidateOccupancy` before updating `_spatialPartition` at the end of `SimulateTick`.
  - Add a direct `Lodging` check in `RestAnchorValidator`.

### R5.2. Extract social dynamics and instance-ify `SuccessionEngine` (`feat/refactor-r5-2-social`, ~0.4 day)
**Complexity:** Possibly Complex: hearth pair loops, emergent group formation, succession, and converting `SuccessionEngine`'s static scratch buffers to instance state.
- [ ] **R5.2.1 Make `SuccessionEngine` an instance class owning its buffers.** [Possibly Complex]
  - In `Cognition/Groups/SuccessionEngine.cs`, change `public static class SuccessionEngine` to `public sealed class SuccessionEngine` and make `_candidateBuffer`, `_scoreBuffer`, `_followersToMove` and `_unalignedCluster` instance `readonly` fields so two simulations in one process never share scratch buffers.
  - Pure stateless helpers (`DetermineSuccessionMode`, `IsLeaderIncapacitatedOrDiscredited`) may stay `static`; `TryFormEmergentGroup` and `ResolveSuccession` become instance methods.
  - Update `SuccessionValidator` to instantiate `new SuccessionEngine()`.
- [ ] **R5.2.2 Create `Simulation/Social/SocialDynamics.cs` and forward from `SimulationManager`.** [Possibly Complex]
  - Owns `GossipRadius` (`2.5f`), `_anchors`, `_hearthPawnsBuffer`, `_hearthAgentsBuffer`, `_activeAnchorsBuffer` and a `SuccessionEngine` instance; takes `IWorldRegistry`, `IGroupRegistry`, `ISectorRegistry`, `INarrativeChronicleService` and `ISimulationClock`.
  - Moves `RegisterAnchor`, `CheckGossipOpportunity`, `EvaluateHearthProximity`, `EvaluateLeadershipSuccession` and `Reset()` out of `SimulationManager.Social.cs`.
  - After R5.2, `SimulationManager` is down to the tick pipeline, the intent queue, `DepartOutpost`, persistence orchestration and the façade (~500 lines).

### R5.3. Tidy cognition memory snapshot and panic forgetting (`feat/refactor-r5-3-cognition-memory`, ~0.3 day)
**Complexity:** Possibly Complex: the rename is Basic; exposing read-only memory maps to off-thread evaluators must preserve zero-allocation `foreach` enumeration and identical golden hashes.
- [ ] **R5.3.1 Rename `AgentMemory.PruneStaleResourceMemories` to `ForgetResourceSightings`.** [Basic]
  - Rename `AgentMemory.PruneStaleResourceMemories(uint currentTick)` to `ForgetResourceSightings()` (dropping the unused `currentTick` parameter) in `Cognition/Memory/AgentMemory.cs`, `Cognition/AgentConsequenceService.cs:57` and `Simpiens.Testing.Core/Environment/SingleWriterMemoryValidator.cs:136`.
- [ ] **R5.3.2 Zero-allocation read-only dictionary views on `IAgentMemory` and `AgentMemorySnapshot`.** [Possibly Complex: must not box `Dictionary.Enumerator` on hot paths, and golden hashes must reproduce 3/3]
  - Add a `readonly struct ReadOnlyMemoryMap<TKey, TValue>` in `Cognition/Memory/` wrapping `Dictionary<TKey, TValue>` with `Count`, `ContainsKey`, `TryGetValue`, indexer getter (`this[TKey]`) and `public Dictionary<TKey, TValue>.Enumerator GetEnumerator() => _map.GetEnumerator();` (duck-typed struct enumerator: 0 bytes allocated in `foreach`, unlike `IReadOnlyDictionary<K, V>` which boxes `IEnumerator<KeyValuePair<K, V>>`).
  - Change `IAgentMemory.SpatialMemoryMap`, `IAgentMemory.AffinityMap`, `AgentMemorySnapshot.SpatialMemoryMap` and `AgentMemorySnapshot.AffinityMap` to `ReadOnlyMemoryMap<...>`. Keep `public Dictionary<...> SpatialMemoryMap` and `AffinityMap` on concrete `AgentMemory` (implementing `IAgentMemory` via explicit interface properties) so main-thread callers and test fixtures that seed `agent.Memory.SpatialMemoryMap[id] = ...` compile unchanged while off-thread evaluators holding `AgentContext.Memory` (`IAgentMemory`) cannot mutate the maps.
  - Remove the dead off-thread `MentalBreakEvaluator.PruneStaleResourceMemories(context)` mutation (in `MentalBreakEvaluator.EvaluateAsync`, every branch inside `if (!hasKnownFood)` returns an `IdleIntent` or `PanicIntent` without reading `SpatialMemoryMap` again, and `AgentConsequenceService` calls `agent.Memory?.ForgetResourceSightings()` on the main thread when `PanicIntent` completes). Update `MentalBreakValidator.ValidateStarvationPanicActivation` to assert resource forgetting after `AgentConsequenceService.ApplyConsequences` (the authoritative main-thread path).
  - Verify `golden check` 3/3 is identical. (If any test or seed ever depended on the in-evaluation snapshot edit, stop and report before changing behaviour.)

### R6. Planners per worker (optional; ~0.5 day; §1A.2 #8)
**Complexity:** Very Complex, supervised by the developer: planning on several threads. It is too small to split. `GridAStarSearcher`'s pool is the precedent, and the probe has to be read.
- **The change:** a pool of `GoapPlanner`s, as with `GridAStarSearcher`, in place of the locked singleton.
- **Measure first:** `PopulationScalingProbe` before and after, at 12, 16 and 24 crawlers. The headless step waits for cognition at each tick barrier, so the lock costs time only when evaluations overlap.
- **The decision:** if the probe shows no gain, it still closes #8. Whether that's worth it now is the producer's call.

### R7. The item system — done 2026-10-26 (its own plan, deleted at close-out)
- **History:**
  - added on 2026-10-26 as an item catalog with categories;
  - widened the same day into its own plan, `.agents/item_system_plan.md`, phases I1–I8: groups, rarity, consumable effects, equipment stats and loot tables;
  - run at once, before Epic 10 G4, at the producer's word ("proceed with refactor plan"), on `feat/items-i1-catalog`, PR #15.
- **The outcome:**
  - code asks for groups, not items (`ItemNamesTests`);
  - an item is added or taken out of the economy as data (`ItemSystemProofTests`);
  - behaviour unchanged: golden 3/3, the soak identical to `main`, renders byte for byte.
- **Decisions and deviations:** `architecture_history.md`, 2026-10-26. How to add or remove an item: `AGENTS.md`.
- **Deferred:**

| Item | Owner |
| :--- | :--- |
| Loot in play: node drops (`ResourceNodeDefinition.LootTableId` is unset everywhere), expedition hauls, conditions (sector, danger, depth, a crawler's stats) | The milestone that first needs random drops (Epic 10 G5's haul, if the producer wants it; otherwise Part 2) |
| Weapon stats in use (Attack, Defense), combat | The descent, Epic 10 Part 3 |
| Tool stats in use (HarvestSpeed on the pickaxe), quality grades (`ItemInstance.Quality`), crafting recipes, repair | Epic 10 Part 2 |
| Rarity in the client (colours, the merchant) | With the first rare drop the player sees |
| Item data files (JSON) and content tools | The map designer (Epic CLIENT) |
| "Salvage" as "a resource the stall buys": `IsSalvage` still means "a Resource"; the stall's role is checked where it trades | When an unsold resource appears (none today) |
| The test conveniences for today's item names (`Simpiens.Testing.Core/Economy/LegacyItemAccessors.cs`, `MerchantTestAccessors`): validators could move to the generic API | Whenever a validator is next rewritten |

- **Quirks kept on purpose, each a behaviour change to make deliberately, with its golden re-record:**
  1. A ration takes 40 off hunger from the pack, but 50 when eaten at a Garden plot (a node rule).
  2. `AgentState.HasFoodInInventory` set true packs a ration.
  3. A node yielding `ItemType.None` counts as food.
  4. `ItemInstance.ItemId` is stamped 1 everywhere.

Estimate: R0–R5 about 3.5 agent days, plus 0.5 for R6 (R7 done).

---

## 4. Deferred

| Item | Owner |
| :--- | :--- |
| Gossip through the spatial grid, the hearth's pair loop, `RemoveSorted` | Roadmap §3A #8, the profiled scaling soak. Gossip partner order is a behaviour change, so it needs a golden re-record in its own commit. |
| Cognition dispatch allocations (job pooling, the `OnComplete` closure, pooled intents) | §3A #8, with an allocation measurement first |
| `WorldState` wider than 64 bits (`UInt128` or two words) | The epic that plans the 61st fact. R1's guard fails first. |
| Nullable reference types | The producer (§5) |

---

## 5. The producer's answers (2026-10-25)

1. **When:** R0 now, before Epic 10 G3; R1–R6 after Epic 10 Part 1 ("we will circle back around to this after Epic 10"). R7 added by the producer on 2026-10-26 ("add R7 to the refactor plan"), to be done before Part 2's crafting multiplies the items.
2. **Where:** no epic; this implementation plan.
3. **Nullable:** not now (default). If wanted later, enable it one project at a time, smallest first (`Presentation`, `Hosting`), and `Core` last, keeping 0 warnings.
4. **Bounty ids:** the default (refuse a finished bounty's id too) was reversed in R0.3, for the reason given there.

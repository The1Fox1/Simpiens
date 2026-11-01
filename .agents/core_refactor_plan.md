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
**Complexity:** Possibly Complex: a hot path (zero allocations, exact float casts), the façade's forwarding, and the null-safety rule; the golden hashes must stay unchanged.
- [ ] **R3.1 Create `Simulation/Movement/Locomotion.cs`.** [Possibly Complex: hot-path math and float casts]
  - Holds `ISpatialPartition`, `IWorldRegistry` and `ISimulationClock`, plus `SidestepTurns`, `FlowTurns` and `CrowdSearchMargin`; owns no mutable state.
  - Moves `ExecutePathMovement`, `ExecutePanicMovement`, `TryStep`, `IsTurnBack` (G5.2), `ApplyProgressiveUnstuckReflex`, `TryDisplacePawn`, `IsCrowdedMove` and `IsPositionBlocked` out of `SimulationManager.Movement.cs`.
  - Preserves every `(float)` intermediate cast (Epic CORE F1), downed limp speed (`Vitality.DownedSpeedFactor`, G2), wall-sight and turn-back waypoint skips (G5.2), and the null-safety rule (a null `_spatialPartition` or `snapshot` means "unblocked", as today).
- [ ] **R3.2 Forward from `SimulationManager.Movement.cs` and validate.** [Basic]
  - `IIntentExecutionContext` and `SimulationManager`'s internal callers (`StepOntoBunk`, `SimulationManagerModularizationValidator`, `NavigationClearanceValidator`, `MotorWatchdogValidator`) forward to `_locomotion`, so handlers and existing tests don't change.
  - Add a direct `Locomotion` unit check in `MotorWatchdogValidator` (or `NavigationClearanceValidator`) exercising `Locomotion` without a `SimulationManager`.

### R4. Extract the market (`feat/refactor-r4-market`, ~0.5 day)
**Complexity:** Possibly Complex: escrow through the ledger, the save block moved with its byte layout kept, and the expiry and buffer order kept.
- [ ] **R4.1 Create `Simulation/Economy/Market.cs`.** [Possibly Complex: escrow, save block and deterministic buffer ordering]
  - Owns `_stalls`, `_activeStallsBuffer`, `_bounties`, `_nextBountyId`, `_activeBountiesBuffer` and `_expiredBountiesBuffer`; takes `IEconomyLedger`, `ISectorRegistry` and `INarrativeChronicleService`.
  - Moves stall registration, queries and price updates (`RegisterTradeStall`, `TryGetStall`, `UpdateStall`, `UpdateStallPrices`), bounty registration, queries, posting, cancellation and expiry (`RegisterBounty`, `TryRegisterBounty`, `TryGetBounty`, `UpdateBounty`, `PostBounty`, `CancelBounty`, `ExpireBounties`, `ReleaseClaimsForAgent`), and their `EconomySave` capture/restore and `Reset()`.
  - Why stalls and bounties go together: escrow is locked from and refunded to a stall's treasury through `IEconomyLedger`.
  - Ordering invariants kept exact: `SortStable` by `StallEntityId` / `BountyId` on active buffers, `SortStable` on `_expiredBountiesBuffer` before refunding in `ExpireBounties`, `Array.Sort` on `EconomySave.Stalls` and `EconomySave.Bounties`, and `_nextBountyId` derived on restore as `highest + 1`.
- [ ] **R4.2 Forward from `SimulationManager` and validate.** [Basic]
  - `SimulationManager.Market.cs`, `SimulateTick` (bounty expiration step), `DepartOutpost` (releasing a departing drifter's claimed bounties), `CaptureTo`, `RestoreFrom` and `ResetState` delegate to `_market`.
  - Add a direct `Market` test in `BountyFulfillmentValidator` that exercises stall escrow, posting, expiry and save/restore on `Market` without a `SimulationManager`.

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

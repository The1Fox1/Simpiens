# Core refactor plan: SimulationManager's split, guards and the cleanup's loose ends

- **Status (producer, 2026-10-25):** an implementation plan, not an epic. **R0 is done** (`feat/code-cleanliness`); **R1–R6 come back after Epic 10 Part 1.** The roadmap records this in §3A ("Core refactor"); keep this plan until R1–R6 are done or dropped, then close it out by the docs protocol.
- **Source:** a smaller agent's report, *Simpiens Solution Architecture & Code Smells Deep Dive* (2026-10-24, not in the repo), and its two commits on `feat/code-cleanliness`: `52f582f` (core fixes) and `e58073a` (Gum migration, 0 warnings). This plan re-checks the report against the code; §2 records which findings hold.
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

## 2. The report, checked against the code

| Report § | Finding | Verdict | Action |
| :-: | :--- | :--- | :--- |
| 2.1 | `SimulationManager` is a god object | **Confirmed.** 1,876 lines; the next-largest core file is 616. Blocks: bunks 215–357, bounties and stalls 580–866, persistence 867–1074, tick 1093–1257, social (gossip, hearth, succession) 1269–1520, movement 1521–1836. | R2–R5 |
| 2.2 | §1A.2 #17, #19 | Fixed by `52f582f`, with the gaps in §1 | R0 |
| 2.2 | §1A.2 #8, the locked `GoapPlanner` singleton | **Confirmed** (`SimpiensServiceCollectionExtensions.cs:57`, `GoapPlanner.cs:97`). Plans depend only on their inputs, so planners per worker shouldn't change behaviour. | R6, optional |
| 3.1 | `SuccessionEngine`'s plain `static` buffers | **Real, latent.** It runs on the simulation thread only, and `Simpiens.Core.Tests` disables parallelism (`TestSetup.cs`). It breaks only with two simulations ticking concurrently in one process, e.g. a future server. | R5 |
| 3.2 | Succession ties hash-ordered | Fixed by `52f582f` | — |
| 3.3 | `MentalBreakEvaluator` "mutates authoritative memory off-thread" and "never syncs back" | **Overstated.** It edits the agent's own snapshot buffer, which no one else reads while that agent's single evaluation is in flight. The authoritative forgetting happens on the main thread when the panic completes (`AgentConsequenceService.cs:56`). The real smell is that the snapshot hands evaluators a mutable dictionary. | R5 |
| 1A.1 #4 | `new AgentState()` uses an unseeded id | **Real, test-only.** The 5 callers are all validators; production passes seeded ids. | R1 |
| 4.1 | Cognition dispatch allocates (`CognitionJob`, `Task.Run` closure, the `OnComplete` lambda, intent classes) | **Real, per decision, not per tick.** Measured throughput is 11–24k ticks/s at 12–24 crawlers. `Task.Run` allocates whatever we pool. | Deferred to roadmap §3A #8 |
| 4.2 | Gossip scans the registry; hearth pairs are O(P²); `RemoveSorted` shifts | **Real, negligible at this scale.** Gossip pairs with the *first* partner in registry order, then stops (`break`). A spatial-grid query returns neighbours in another order, so it would change who gossips with whom and re-record the golden hashes. It is a behaviour change, not a refactor. | Deferred to §3A #8 |
| 5.1 | `PathResponse` double return | Fixed by `52f582f` | — |
| 5.2 | A* `MaxCells` 16,384 silently skips cells | **Real, latent.** Floor 1's grid is 52 × 40 = 2,080 cells (13% of the cap), and the Dev Map is smaller. A bigger floor (Epic 10 Part 3) would lose paths with no error. | R1: refuse loudly at load |
| 5.3 | `WorldState` packs facts into a `ulong`; fact 64 wraps onto fact 0 | **Real, not near.** 39 of 64 facts are used (roadmap §3A already tracks the budget). Nothing guards the limit. | R1: a guard. Widen when a fact past 60 is planned. |
| 6.1 | `PruneStaleResourceMemories` wipes every resource memory | **Intended behaviour, wrong name.** A panicking crawler forgets its resource sightings and explores anew. `currentTick` is unused. | R5: rename it |
| 6.2 | Dead fields (`_gossipEventBuffer`, `ScriptedInput`) | Fixed by `52f582f` and `e58073a` | — |
| 6.3 | `SessionHost`'s four parallel crawler lists | **Real, low risk.** Only three sites change them (spawn, `RemoveCrawlerAt`, the respawn's clear). | R1 |
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

### R1. Guards (~0.5 day; after Epic 10 Part 1)
1. **GOAP facts.**
   - A validator: every `StateFact` value is below 64.
   - A `Debug.Assert` in `WorldState`'s bit helpers (free in Release).
   - The roadmap's fact budget is updated.
2. **The A* grid.**
   - `WorldMap.Load` refuses a map whose grid exceeds the searcher's cells, with a message that names both numbers.
   - The searcher keeps its silent check only as a backstop.
   - A validator with an oversized map.
3. **Ids.** `AgentState()` no longer calls `EntityId.Generate()`; the 5 validator callers pass ids. `EntityId.Generate` stays for tests and tools, and its comment already says so.
4. **One crawler list.**
   - `SessionHost`'s four parallel lists become one list of a `Crawler` record (runtime, state, name, label).
   - `CrawlerStates` keeps its type: a read-only view over the states, so callers don't change.

### R2. `SimulationManager` into partial files (~0.5 day, moves only)
Pure moves, no edited lines, after the precedent of `SessionHost.Drifters.cs`:
- `SimulationManager.cs`: fields, constructor, tick, the intent queue;
- `.Market.cs`: stalls, bounties, escrow;
- `.World.cs`: resources, sectors, surveys;
- `.Lodging.cs`: bunks, departures;
- `.Social.cs`: gossip, the hearth, succession;
- `.Movement.cs`: movement, the unstuck reflex, displacement, blocking;
- `.Persistence.cs`: capture, restore, reset.

**Why first:** each later extraction then diffs one file. Agents also stop loading 1,876 lines to touch one block (context size is a stated priority).

### R3. Extract locomotion (~0.5 day)
- **What moves:** `ExecutePathMovement`, `ExecutePanicMovement`, `TryStep`, `ApplyProgressiveUnstuckReflex`, `TryDisplacePawn`, `IsCrowdedMove` and `IsPositionBlocked`, into `Simulation/Movement/Locomotion`.
- **Why it's the cleanest cut:** the block reads only the spatial partition, the registry and the clock, and owns no `SimulationManager` state.
- **The interface stays:** `IIntentExecutionContext` keeps its members and forwards to `Locomotion`, so handlers don't change.
- **Null-safety rule:** a null partition means "unblocked", as today.

### R4. Extract the market (~0.5 day)
- **What moves:** stalls, the bounty board (`_bounties`, `_nextBountyId`, the active and expired buffers), posting, cancelling, expiry, escrow through the ledger, and their save block, into `Simulation/Economy/Market`.
- **Why stalls and bounties go together:** they're one unit, because escrow is drawn from a stall's treasury.
- **Ordering:** the expiry and buffer order is kept exactly as it is.
- **What it gains:** market validators can build a `Market` without a `SimulationManager`.

### R5. Extract lodging and social dynamics; tidy cognition (~1 day)
1. **`Lodging`:** bunks, claims, sleeps, occupancy, departing claims.
2. **`SocialDynamics`:** gossip, hearth proximity, group formation, the succession tick.
   - `SuccessionEngine` becomes an instance that owns its buffers, held by `SocialDynamics`, so two simulations in one process don't share them.
3. **Cognition:**
   - `PruneStaleResourceMemories` is renamed `ForgetResourceSightings` (no tick argument).
   - The memory snapshot gives evaluators a read-only view. `MentalBreakEvaluator` filters instead of deleting, only if the golden hashes reproduce exactly. If they don't, keep the in-place edit on the private buffer and record why.
4. **Expected result:** `SimulationManager` is down to the tick, the intent queue and the façade, around 500 lines.

### R6. Planners per worker (optional; ~0.5 day; §1A.2 #8)
- **The change:** a pool of `GoapPlanner`s, as with `GridAStarSearcher`, in place of the locked singleton.
- **Measure first:** `PopulationScalingProbe` before and after, at 12, 16 and 24 crawlers. The headless step waits for cognition at each tick barrier, so the lock costs time only when evaluations overlap.
- **The decision:** if the probe shows no gain, it still closes #8. Whether that's worth it now is the producer's call.

Estimate: R0–R5 about 3.5 agent days, plus 0.5 for R6.

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

1. **When:** R0 now, before Epic 10 G3; R1–R6 after Epic 10 Part 1 ("we will circle back around to this after Epic 10").
2. **Where:** no epic; this implementation plan.
3. **Nullable:** not now (default). If wanted later, enable it one project at a time, smallest first (`Presentation`, `Hosting`), and `Core` last, keeping 0 warnings.
4. **Bounty ids:** the default (refuse a finished bounty's id too) was reversed in R0.3, for the reason given there.

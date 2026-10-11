# Plan — Milestone 1 Closure (ROADMAP §3A recommended sequence)

> **Status: approved 2026-10-09** (producer). Phase order approved. Economy design decisions are **deferred** until the game is functional (see Phase 3). Pen demo agents stay neutral. Work proceeds on `feat/milestone1-supply-loop-zlogger`.
>
> **Source:** [ROADMAP §3A](../docs/ROADMAP.md#3a-architecture-review-remediation-tracker) proposed sequence, consultant review [`architecture_review.md`](../docs/reviews/architecture_review.md) §2.5, §2.7, §2.8, §3.2, §3.3.
>
> **Branch:** `feat/milestone1-supply-loop-zlogger` (producer decision). Commit after every phase (AGENTS.md *Version Control*).
>
> **Standing directive (producer, 2026-10-11):** complete the remaining phases autonomously on this branch, with authority to make **minor design decisions** only (tuning constants, structure, naming, test design). Each one is recorded under its phase. Commit every working state with an honest test status; never push, merge or rewrite history.

**Out of scope for autonomous work** (left for the producer):
- Economy design (§3.3).
- Promoting Epic 9 while §3.3 is deferred.
- Dropping scope.
- New product features.

## Goal

Epic 9 → `COMPLETED`, which (per producer decision 2026-10-09) needs:
1. PlayMode fully green: `OutpostSupplyLoopScenario` meets §1B, and the 3 anomaly scenarios no longer regress.
2. A written, implemented economy model (review §3.3).
3. Agent personality + utility considerations (review §3.2 / §2.7).

Parallel, low-cost work rides alongside: determinism fixes (§1A.2 #5, #6, #14) and test-scope separation (§1A.2 #9, review §2.8).

Out of scope here (later Milestone 1 steps in §3A): resuming Epic MAP, MemoryPack, scaling/soak (§2.5).

## Phase overview

| Phase | Work | Size | Depends on | Unblocks |
| :-: | :--- | :-: | :--- | :--- |
| 1 | Green PlayMode: navigation body clearance, wall-checked watchdog, nav-grid reset, stacking, thrashing | M | — | §1B gate for the supply loop |
| 2 | Determinism: seeded RNG, ordered intent apply, ordered iteration | S | — (do before 3 so tuning runs are reproducible) | Reproducible PlayMode, multiplayer invariants |
| 3 | Economy: **observation-only** soak now; design decisions + model **deferred** until the game is functional | S now / S–M later | 2 (recommended) | Epic 9 promotion (when the deferred part lands) |
| 4 | Personality + considerations | M | 1, 2 (tuned against the live-loop scenarios, not an economy band) | Epic 9 promotion |
| 5 | Test-scope separation | S–M | — (any time; avoid running concurrently with 1 in `SwarmSpawner`) | Clean boot, shippable build |
| 6 | Docs + promotion | S | 1–4 | Epic 9 `COMPLETED` |

---

## Phase 1 — Green PlayMode

> **Status: DONE 2026-10-10.** PlayMode 14/14 across 3 consecutive runs. Commits `c10a57f`, `e74c2d2`, `c12871f`, `cdf6514`, `498bb6c`.
>
> **Differences from the plan below:**
> - **Body clearance:** done by inflating wall rasterization by the pawn radius plus snapped A* endpoints and an exact segment traversal, instead of a per-step circle test (equivalent guarantee, cheaper).
> - **Arrival slots (1E):** replaced by live-position pawn separation and arrival tolerance. Every displacement is now crowd-aware, panic included. The remaining stacking came from panic runs and unchecked watchdog nudges, not from a shared destination.
> - **Thrashing (1F):** the trace showed `Thinking > Idle > Walking`, caused by reflex pre-emption resetting the visual to Idle. Fixed at the source, plus a 2-tick display dwell in `AgentView`. The detector now measures the rendered state.
> - **Found along the way:**
>   - Pawns walked to the edge of the oversized grid, so a ±20 play-area border was added.
>   - A planner bug: salvage/scout actions didn't clear `AtTradeStall`.
>   - A validator broken since §3.1 (`BountyFulfillmentValidator` Test 3).
>   - New known deviation §1A.2 #15 (off-thread plan bookkeeping).
> - **Provisional economy stopgap:** fixed navigation exposed the treasury drain inside the scenario horizon, so `ProvisionalCaravanExport` was added (Phase 3's "only if needed" clause).

### Evidence (latest run, `Logs/playmode-results.xml`, 10/14 pass)

| Scenario | Failure | Representative anomaly |
| :--- | :--- | :--- |
| `OutpostSupplyLoopScenario` | 79 pawn stackings (plus penetrations) | stacking at (2.12, 0.15), 0.21 m for 1.5 s; penetrations at `Wall_3.0_-5.5`, `Wall_10.0_-6.5`, `Wall_6.0_0.5` |
| `DynamicChaosScenario` | 6 penetrations | same two agents repeatedly at `Wall_-8.0_-12.5` and `Wall_6.0_±0.5` (pen walls, **not** the injected wall) |
| `SwarmNavigationScenario` | 2 penetrations | `Wall_6.0_-0.5` / `Wall_6.0_0.5` |
| `ResourceContentionScenario` | 2 thrashing incidents | 4 visual-state switches in 0.12 s, ending `Idle` |

Tick numbers keep rising across scenarios (t6000+), so the 4 scenarios share one session; state leaks between them (see 1C).

### Root causes (verified in code)

1. **Movement checks a point, not a body.** `SimulationManager.ExecutePathMovement` ([SimulationManager.cs:1007](../Assets/Scripts/Core/SimulationManager.cs)) checks only that the *waypoint's* cell is walkable (`IsPositionBlocked`), then `MoveTowards` with no check on the pawn's actual position or radius. Walls are 1 m cells (`SimulationStarter` initialises the grid with `cellSize = 1.0`). A pawn whose centre sits in a free cell next to a wall overlaps it. The detector flags overlap at `Radius * 0.7` ([VisualAnomalyDetector.cs:360](../Assets/Scripts/Testing/Diagnostics/VisualAnomalyDetector.cs)).
2. **Path smoothing grazes corners.** `GridAStarSearcher`'s raycast string-pulling tests a zero-width line, so smoothed segments cut past wall corners (the `Wall_6.0_±0.5` pair is a doorway post).
3. **Watchdog reflexes teleport without checks.** Tier 1 nudges (±0.2 / −0.4 m perpendicular) and the Tier 3 0.5 m back-off call `SetLogicalPosition` directly ([SimulationManager.cs:1075-1125](../Assets/Scripts/Core/SimulationManager.cs)). The SimLog messages already say "unchecked against walls".
4. **Static walls are never cleared.** `SpatialHashGrid.UpdateFromRegistry` calls `StaticNavGrid.SetWall` for every wall each tick, but nothing calls `StaticNavGrid.Clear()`. `SwarmSpawner.Respawn` → `SimulationManager.ResetState` leaves old walls (including `EdgeCaseInjector` chaos walls) in the grid for the next scenario.
5. **Stacking:** pawn clearance applies only to the waypoint, so two pawns heading for the same goal point (hearth slot, stall, bush) both arrive. The detector flags < 0.35 m for ≥ 1 s unless the pair is gossiping/sharing.
6. **Thrashing:** an agent cycles intent → immediate fail/success → Idle → re-think within 0.12 s. Likely a zero-length path or an intent that completes on its first tick. **Confirm from a trace before fixing**; don't guess.

### Changes

**1A. Body-aware movement (`SimulationManager`, `SharedWorldSnapshot`)**
- Add `SharedWorldSnapshot.IsCircleWalkable(Vector2 centre, float radius)`: scan the cells the circle overlaps against `StaticGrid` (allocation-free, ≤ 9 cells at r < 1).
- In `ExecutePathMovement`, compute the candidate step position. If `!IsCircleWalkable(candidate, node.Radius * 0.7f)`, don't move (the watchdog takes over), exactly as a blocked waypoint is handled now.
- Use one shared constant `NavConstants.BodyClearanceFactor = 0.7f` (with `NodeController.Radius`) so the detector and the motor can't drift apart.

**1B. Clearance-aware smoothing (`GridAStarSearcher`)**
- Make the line-of-sight test sweep the agent radius: test the two parallel offset lines at ±r, or treat any cell within r of the segment as blocking. Keep it zero-alloc.
- Add a small static-clearance cost to A* for cells adjacent to walls, so unsmoothed paths keep off walls where there's room. Doorways (2.5 m) must stay passable; cover this in `GridAStarValidator`.

**1C. Nav-grid lifecycle (`ISpatialPartition`, `SimulationManager.ResetState`)**
- Add `ISpatialPartition.ResetStatic()` → `StaticNavGrid.Clear()` (bumps the walkability version so reachability regions rebuild).
- Call it from `SimulationManager.ResetState()`. Walls re-register on the next `UpdateFromRegistry`.
- Unregistered walls need clearing too: if an injected wall is removed mid-run, either rebuild the static grid from registered walls whenever the wall set changes (simplest: a wall-set version counter in `WorldRegistry`), or have `EdgeCaseInjector` call a `RemoveWall` API. **Prefer the rebuild.** It is O(walls), happens rarely, and has no bookkeeping that can drift.

**1D. Wall-checked watchdog (`ApplyProgressiveUnstuckReflex`)**
- Before each nudge or back-off, test `IsCircleWalkable(candidate, r)`. If blocked, try the mirrored direction, then skip the displacement and fall through to the next tier.
- Never displace into a cell in a different static region (`StaticNavGrid.AreConnected`). That stops teleporting through thin walls.
- Update the SimLog messages (drop "unchecked against walls"). Extend `MotorWatchdogValidator` with a "nudge toward a wall is rejected" case.

**1E. Pawn stacking**
- **Arrival slots:** when several agents share a destination (stall interaction point, hearth, bush), the handler that builds the path asks the execution context for a free spot. Add `IIntentExecutionContext.ResolveArrivalPoint(target, agentId, radius)`: a deterministic ring of offsets checked against current pawn positions and `IsCircleWalkable`.
- **Per-step pawn check:** in `ExecutePathMovement`, also reject a step that would bring the pawn within `2 × r × 0.5` (≈ 0.35 m) of another non-interacting pawn. The watchdog resolves the face-off.
- Leave social interactions (gossip/share) exempt, matching the detector.

**1F. Thrashing**
- Run `ResourceContentionScenario` with `SIMPIENS_LOG="Cognition=Debug;Navigation=Debug" SIMPIENS_LOG_FOCUS=<agent>`. Identify the intent that completes within one tick.
- Likely fix: treat zero-waypoint paths as `PathBlocked` rather than `Success`, and add a minimum dwell (in ticks) before the visual state is allowed to change. The dwell belongs in `AgentView`, not the simulation.

### Verification
- `MotorWatchdogValidator`, `GridAStarValidator`, `GoapLiveLoopFixesValidator`: all pass.
- New `NavigationClearanceValidator`: a pawn is never placed overlapping a wall, across move, nudge and back-off; static grid clears on reset; arrival slots don't overlap.
- PlayMode: **14/14**. Record per-scenario anomaly counts in the commit body.
- **Do not loosen the detector thresholds** to get green. If a threshold is genuinely wrong, that's a separate, justified change.

Commits: `fix(navigation): body-aware movement and clearance smoothing` · `fix(navigation): wall-checked watchdog and nav-grid reset` · `fix(simulation): arrival slots to prevent pawn stacking` · `fix(cognition): ...thrashing` (after diagnosis).

---

## Phase 2 — Determinism (§1A.2 #5, #6, #14)

> **Status: DONE 2026-10-10** (`1dc831b`). PlayMode 16/16.
>
> **Differences from the plan below:**
> - **Added `EntityIdAllocator`.** Entity ids came from `GUID.Generate()`, and they feed tie-breaks, exploration hashes and `SimRandom` keys, so a seeded RNG alone could not reproduce a run.
> - **Harvest contention** now picks the lowest-id competitor.
> - **`DeterminismValidator` Test 3** was mutation-checked: restoring insertion-order application makes it fail.
> - **Validator wrappers:** PlayMode wrappers added for `DeterminismValidator` and `NavigationClearanceValidator`. Boot-time `IStartable` failures are only logged.

### Changes

**2A. Seeded, stateless RNG.**
- New `Simulation/SimRandom.cs`: a `readonly struct` built from `(sessionSeed, agentId, tick, salt)` via SplitMix64. It has no shared state, so it's safe on worker threads without `[ThreadStatic]`. Methods: `NextFloat()`, `Range(min, max)`.
- Session seed lives in a new `SimulationSettings` (DI singleton). The seed comes from a command-line arg/env var `SIMPIENS_SEED`, with a fixed default. It's logged at Info on boot.
- **Replace:** `AmbientWanderEvaluator`, `MentalBreakEvaluator` (`[ThreadStatic] System.Random`). `AgentContext` already carries `AgentId` and `CurrentTick`; add the seed to `AgentContext`, or have evaluators take `SimulationSettings` in their constructor.
- **Leave as view-only** (add a one-line comment saying so): `NodeController` `Random.Range` (lines 143, 200) applies only to Dynamic-body bouncing nodes; `NodeFactory` uses `Random.ColorHSV` only for sprite colours.

**2B. Ordered intent application (#6).**
- `SimulationManager.ProcessQueuedIntents`: drain the `ConcurrentQueue` into a reusable `List<AgentIntent>`, sort by `AgentId` (`GUID.CompareTo`, as `ResourceTargeting` does), then apply. Zero-alloc with a preallocated list and a static comparer.
- This fixes order *within* a tick. The tick an intent *arrives* still depends on thread timing. Making that deterministic (apply cognition results at a fixed tick offset, or a barrier) belongs to the multiplayer epic. Record it as remaining in §1A.2 #6 rather than claiming it's resolved.

**2C. Ordered iteration (#14).**
- Keep an insertion-independent sorted key list next to `_activeIntents`, `_stalls`, `_bounties` and `_anchors` (a `SortedList`, or a `List` re-sorted on add/remove, which is rare), and iterate that instead of the `Dictionary`.
- `_bounties` keys are `ushort`; iterate in ascending id.

### Verification
- New `DeterminismValidator`: two headless runs of N ticks with the same seed and identical injected intents give identical agent positions/needs/gold hashes. A different seed gives a different hash.
- Phase 1 PlayMode stays green.

Commit: `fix(simulation): seeded SimRandom and deterministic intent ordering`. Update §1A.2 #5/#14 to RESOLVED and #6 to partially resolved.

---

## Phase 3 — Economy observation soak (§3.3 design deferred)

> **Status: DONE 2026-10-11.** The producer deferred economy *design* decisions until the game is functional (2026-10-09). This phase only **measures**: no new faucets, sinks, prices or pace targets.
>
> - Commits: `c5c2240`, `67cc1d2`, `44183e5`.
> - Six soak runs (3 seeds × export on/off) passed all integrity assertions. Observations are at the end of this phase.
> - **Found and fixed:** `TradeIntentHandler` moved trade gold directly instead of through the ledger (`44183e5`). Conservation held, but trades were invisible to the ledger, which contradicted §1A.2 #10.

### Goal

Give the producer the data to make the deferred economy decisions:
- A repeatable, long, uninstrumented run of the real session that records how gold, stock and needs move over time.
- The same run with and without the provisional export, so the stopgap's effect is visible.

**Assertions are integrity only:**
- Ledger conservation at every sample.
- The run reaches its tick horizon.
- The CSV is written.

There are no economic bands. Phase 3 delivers part of Epic Test-2's "zero-allocation high-frequency market soak runner".

**Non-goals:**
- `docs/economy-model.md`, new faucets/sinks, retuning `SupplyTuning`, and food-pressure changes. These all wait for the producer.
- Making the run bit-reproducible. Arrival ticks of async cognition still vary (§1A.2 #6), so same-seed runs are close but not identical. The CSV records the seed anyway.

### Current flows (verified 2026-10-10)

| Flow | Direction | Numbers | Ledger today |
| :--- | :--- | :--- | :--- |
| `InitialSeed` | faucet → stall 250g + agents 10g each | once, at spawn | named faucet (total only) |
| `ProvisionalCaravanExport` | faucet → stall | ore 24g, herb 30g above a 10-unit reserve, every 600 ticks | named faucet (total only) |
| `SupplyCaravan` restock | stall → sink | 2g/ration up to cap 50, every 600 ticks; skipped if unaffordable | named sink (total only) |
| Agent buys ration | agent → stall | 15g | conserved transfer, **not counted** |
| Agent sells ore / herb | stall → agent | 20g / 25g | conserved transfer, **not counted** |
| Bounty escrow → payout / refund | stall → escrow → agent / stall | per bounty | `TotalEscrowGold` only |
| Free food (bush regrowth) | — | hunger met without gold | `SupplyService.RegrowthCount` |

`IEconomyLedger` keeps only `TotalFaucetedGold` and `TotalSinkedGold`, with no per-name breakdown and no transfer volumes. That is not enough to explain a treasury curve, so step 3A adds read-only counters.

### Questions the soak must answer

These feed the deferred decisions listed at the end of this phase.
1. **Treasury trajectory**, with and without the provisional export: does it drain, plateau or grow, and how fast?
2. **Where gold sits:** stall, crawlers or escrow. Does it pool in crawler wallets?
3. **Flow volumes per sim-minute:** rations bought, salvage sold, bounty payouts, caravan sink, export faucet.
4. **Free food vs. bought food:** do crawlers ever *need* to buy rations, or do bushes cover hunger (the missing agent sink)?
5. **Supply health:** skipped restocks, stall stock levels, node regrowth rate.

### Changes

**3A. Read-only economy counters** (`Simulation/Economy/`)
- **`EconomyFlowTotals`:** a new `readonly struct` snapshot of cumulative values since `Reset()`:
  - Stall purchases: count, gold (agent → stall).
  - Stall sales: count, gold (stall → agent).
  - Escrow locked, released and refunded gold.
- **`IEconomyLedger`:**
  - `EconomyFlowTotals Flows { get; }`, updated inside the existing `TryExecuteStallPurchase/Sale`, `TryLockEscrow`, `TryReleaseEscrowToClaimant` and `TryRefundEscrow` on success. These are main-thread only (single writer).
  - `uint GetFaucetTotal(string name)` / `uint GetSinkTotal(string name)`, backed by small `Dictionary<string, uint>`s keyed by the constant names already passed to `RecordFaucet/RecordSink`. Lookups with constant strings don't allocate; a new name allocates once.
  - `Reset()` clears all of them.
- **`ISupplyService`:** `SkippedRestockCount`, incremented where the "Supply caravan skipped" warning is logged today.
- **Nothing here changes behaviour.** The counters are written where gold already moves and are read by the soak.

**3B. Provisional export toggle** (for the A/B comparison)
- `SimulationSettings.ProvisionalExportEnabled`, from `SIMPIENS_PROVISIONAL_EXPORT` (`0` disables) or `-simprovisional-export 0`. Default on, the current behaviour.
- `AppBootstrapper` registers `SupplyService` with `SupplyTuning.Default` or `SupplyTuning.DefaultWithoutExport`. The latter is a new factory with export prices 0, the same as the 3-argument constructor.
- Logged at boot next to the seed.

**3C. `EconomySoakScenario`** (`Assets/Tests/PlayMode/Scenarios/`)
- **Runtime:** real session via `SwarmSpawner.Respawn`, 10× (the speed the supply loop is validated at), **18,000 ticks = 30 sim-minutes**, about 3 wall-clock minutes. Wall-clock timeout 600s. No injection and no player commands. It only observes, like `OutpostSupplyLoopScenario`.
- **Sampling:** every 600 ticks (one caravan cycle, one sim-minute), plus a final sample. Rows are buffered in a preallocated array and written once at the end, so file I/O stays out of the observed run.
- **Agent groups:** crawlers and pen agents are summarized separately. Agents at index ≥ `SwarmSpawner.ScriptedPenAgentCount` are crawlers; pen agents are walled off from the stall and would only dilute the numbers.
- **Integrity assertions:**
  - Ledger conservation holds at every sample.
  - `observedTicks ≥ 18000`.
  - The CSV exists and has the expected number of rows.
- **Not asserted:** anomaly counts (the supply-loop scenario already guards those), plus every economic value.
- **Output:**
  - `Logs/economy-soak-seed<seed>-export<on|off>.csv`.
  - One Info summary line: start → end treasury, total faucets/sinks, transfer volumes, skipped restocks.
- **Opt-in:** `[UnityTest, Explicit, Category("Soak")]` in `SwarmPlayModeTests`, so the regular suite stays around 2 minutes and doesn't pick up a 3-minute run.

**3D. Runner script** (`scripts/run-economy-soak.sh`)
- Runs the soak test filter for each `(seed, export)` pair. Default: seeds `20261009 1 2`, export on and off, so 6 runs (about 20 minutes).
- Accepts `--seeds` and `--export on|off|both`.
- Uses the same Unity binary and lockfile checks as `run-playmode-tests.sh`.
- Exits non-zero if any integrity assertion fails, and prints the CSV paths.

**3E. Findings note**
- After the runs, append an **Observations** subsection to this phase. It answers the five questions with numbers from the CSVs, and states facts only, without recommendations.
- Optionally, publish a chart artifact of treasury, crawler gold and flows over time, so the producer can read it at a glance.

### CSV schema (one row per sample)

| Column group | Columns |
| :--- | :--- |
| Run | `seed`, `export_enabled`, `tick`, `sim_minute` |
| Stall | `treasury`, `ration_stock`, `ore_stock`, `herb_stock`, `ration_price`, `ore_buy_price`, `herb_buy_price` |
| Ledger totals | `fauceted_total`, `sunk_total`, `escrow`, `faucet_initial_seed`, `faucet_provisional_export`, `sink_supply_caravan` |
| Transfers (cumulative) | `purchases`, `purchase_gold`, `sales`, `sale_gold`, `escrow_locked`, `escrow_released`, `escrow_refunded` |
| Crawlers (6) | `crawler_gold_total`, `crawler_gold_min`, `crawler_gold_max`, `crawler_hunger_mean`, `crawlers_hungry` (hunger ≥ 60), `crawlers_starving` (≥ 85) |
| Pens (24) | `pen_gold_total`, `pen_hunger_mean` |
| Supply | `restocks`, `skipped_restocks`, `exports`, `regrowths`, `dormant_nodes` |
| Bounties | `active_bounties`, `completed_bounties` |
| Integrity | `conservation_ok`, `discrepancy` |

Per-minute rates come from differencing the cumulative columns. The scenario doesn't compute them.

### Verification
- New `EconomyCountersValidator` (headless, 4 tests):
  - A purchase, a sale and an escrow lock/release/refund each move exactly their counters.
  - The per-name faucet/sink breakdown sums to `TotalFaucetedGold` / `TotalSinkedGold`.
  - `Reset` clears everything.
  - Zero allocation across 1,000 counter updates with constant names.
- `SupplyLoopValidator`: a skipped restock increments `SkippedRestockCount`.
- Regular PlayMode suite still passes (16/16 plus the new validator wrapper). The soak is explicit, so it isn't counted.
- `scripts/run-economy-soak.sh` completes all 6 runs with integrity assertions passing. Report the results in the commit/docs, failures included.

### Commits
- `feat(economy): read-only flow counters for ledger and supply`
- `feat(simulation): provisional export toggle in SimulationSettings`
- `test(economy): economy observation soak scenario and runner`
- `docs(economy): soak observations`

### Risks

| Risk | Mitigation |
| :--- | :--- |
| A 30 sim-minute run exposes a late failure (crash, conservation break, frozen loop) | That is a finding: record it and fix it if it's a bug. Integrity failures fail the soak. |
| Counters drift from the gold they describe | Counters are updated only inside the existing ledger transfer methods, and the validator cross-checks them against totals. |
| 10× wall-clock throughput drops on long runs | 600s timeout plus the `observedTicks` assertion. A shortfall is reported, not hidden. |
| Same-seed runs differ (arrival-tick nondeterminism) | Run 3 seeds and compare trends, not exact values. Seed recorded per CSV. |

### Observations (2026-10-11; facts only, no recommendations)

Six runs: 18,000 ticks (30 sim-minutes) at 10×, seeds 20261009 / 1 / 2, provisional export on and off. CSVs are in `Logs/economy-soak-seed<seed>-export<on|off>.csv`.

| Export | Seed | Treasury end (start 210g) | Crawler gold (6 crawlers, start 60g) | Rations bought | Salvage sold | Export faucet | Caravan sink | Skipped restocks | Trading stops at | Max crawlers starving | Mean crawler hunger |
| :-: | :-: | --: | --: | --: | --: | --: | --: | --: | :-: | :-: | --: |
| on | 20261009 | 4,709g | 235g | 278 (4,170g) | 186 (4,305g) | 5,190g | 556g | 0 | never | 0 | 37.0 |
| on | 1 | 4,488g | 250g | 264 (3,960g) | 177 (4,110g) | 4,956g | 528g | 0 | never | 2 | 40.3 |
| on | 2 | 4,260g | 230g | 250 (3,750g) | 166 (3,880g) | 4,680g | 500g | 0 | never | 3 | 40.1 |
| off | 20261009 | 17g (min 2g) | 235g | 79 (1,185g) | 59 (1,320g) | — | 58g | 25 of 29 | min 10 | 4 | 49.3 |
| off | 1 | 1g | 215g | 97 (1,455g) | 69 (1,570g) | — | 94g | 23 of 29 | min 12 | 3 | 50.0 |
| off | 2 | 3g | 245g | 81 (1,215g) | 60 (1,360g) | — | 62g | 25 of 29 | min 11 | 3 | 54.5 |

1. **Treasury trajectory.**
   - *Export off:* the treasury drains to 1–17g within 10–12 sim-minutes. After that, every trade stops: the stall can't pay for salvage and can't afford restocks, and ration stock hits 0. The economy stays frozen for the rest of the run.
   - *Export on:* the treasury grows almost linearly, about +140–150g per sim-minute, to roughly 4,300–4,700g. Growth ≈ export faucet − caravan sink (net flow from crawlers ≈ 0, see 2).
2. **Where gold sits.** Crawler wallets plateau at about 215–250g in total (≈ 35–42g each), with or without the export. Crawlers spend almost exactly what they earn: ration spend is 96–97% of sale income with export on, and 87–93% with it off. Gold doesn't pool in crawler wallets; it pools in the stall treasury (export on) or nowhere (export off, economy frozen). Pen agents hold a constant 240g, since they're walled off from the stall.
3. **Flow volumes.** With export on: about 8–9 ration purchases per sim-minute (15g each) and about 6 salvage sales per sim-minute (20–25g each). Bounty payouts total 40g per run: only the two seeded bounties exist, and no player posts any.
4. **Free vs. bought food.** Crawlers buy rations heavily whenever they can (250–278 purchases per run), even though bushes regrow (≈ 230–250 regrowths per run). With the export off and the stall unable to restock, crawler hunger rises (mean 49–55 vs 37–40) and 3–4 crawlers reach starvation. Free food doesn't fully replace the stall in the current setup.
5. **Supply health.** With export on, the caravan never skips (29/29 restocks) and the export runs every visit (30/30). With export off, 23–25 of 29 restocks are skipped. Salvage then piles up unsold in the stall (39–48 units each of ore and herb), because nothing removes it.

**Measurement notes:**
- Samples are taken every 600 ticks, which coincides with caravan visits. Stall stock columns therefore show post-visit values: ration stock 50, salvage at the 10-unit reserve. Use transfer counters, not stock columns, for rates.
- Same-seed runs aren't bit-identical, because the arrival tick of async cognition still varies (§1A.2 #6). The trends agree across seeds.

### Producer decisions (deferred; the soak informs them)

1. **Salvage outlet (faucet):** keep, retune or replace the provisional caravan export.
2. **Agent sinks:**
   - (a) Free bushes yield less or regrow slower, so rations matter.
   - (b) Daily lodging/upkeep fee.
   - (c) Gear wear (defer to Epic 10).
3. **Scarcity lever:** material scarcity (node regrowth) vs. agent willingness to spend (Phase 4 personality).
4. **Target pace:** e.g. "a competent player's treasury roughly doubles in 30 sim-minutes; a careless one hits zero". Once set, it becomes a soak assertion.

Once decided: write `docs/economy-model.md`, implement it, and turn the soak's agreed bands into assertions. That is the deferred half of §3.3.

---

## Phase 4 — Personality + considerations (review §3.2, §2.7)

### Design
- **`AgentPersonality`** (`Cognition/Contracts/`): blittable 5-byte struct (`Greed`, `RiskTolerance`, `Sociability`, `Loyalty`, `Diligence`, each `byte` 0–255, accessed as 0..1 floats). Stored on `AgentState`, copied into `AgentContext` (it's immutable, so no single-writer issue).
- **Generation:** deterministic from `SimRandom(seed, agentId, 0, salt: Personality)` (Phase 2). Pen demo agents get **fixed neutral personalities** so the Epics 7/Beta/8 demos stay as tuned.
- **Considerations layer** (`Cognition/Planning/Considerations/`): static, allocation-free scoring helpers that return 0..1.
  - `Reward(gold, personality.Greed)`, `Distance(path or straight-line, Diligence)`, `Risk(...)` (placeholder = 1 until Epic 10 hazards), `Affinity(memory, targetId)`, `Price(stall price vs. agent reference price, Greed)`.
  - A `Score = Π considerations` combiner (Dave Mark-style compensated product) with a fixed-capacity span.

### Changes
- **Target selection:** `BountyQueries.TryGetOpenBounty` currently picks the *reachable, lowest id* bounty. Replace that with the highest consideration score (reward × distance × personality), keeping the GUID/id tie-break. `ResourceTargeting` nearest-node/stall choices get the same treatment where it matters (stall choice once there's more than one stall).
- **Goal priorities:** `FulfillBountyGoal`, `EarnIncomeGoal`, `PurchaseProvisionsGoal`, `LiquidateSalvageGoal`, `SocializeGoal` and `GatherAtAnchorGoal` multiply in their personality consideration. This removes the binary price threshold: agents have personal reservation prices (`Greed` shifts the acceptable ration price and sale price).
- **"Why?" groundwork (review §3.4, not the UI):** record the top 3 goal scores and the winning considerations into `AgentState.LastDecision` (fixed-size struct) on the main thread when the intent is applied. The inspector UI itself stays out of scope; this just makes it cheap later.
- **Fact budget:** add no new `StateFact`s. Personality acts through priorities and selection, not facts (36/64 used).

### Verification
- New `PersonalityValidator`: same seed gives the same personalities; a greedy agent picks the higher-reward farther bounty and a low-diligence agent picks the nearer one; reservation price varies across agents.
- **Live (§1B):** new PlayMode `PriceResponseScenario`. Raise the ration price in steps (via `UpdateStallPricesCommand`, the player's real lever, not injected outcomes) and assert purchase volume falls **gradually** across agents rather than all at once. This is the review's §3.2 "price slider is an on/off switch" check.
- Economy soak: ledger conservation holds (no pace band until the economy decisions are made).

Commits: `feat(cognition): agent personality` · `feat(planning): utility considerations for target selection` · `test(playmode): price response scenario`.

---

## Phase 5 — Test-scope separation (review §2.8, §1A.2 #9, #10 remainder)

Order matters, because `AppBootstrapper` (production assembly) currently references validators, and the production domain lives in `Testing/SwarmSpawner`.

1. **Extract the production domain.** Move the GOAP action graph and goal list out of `SwarmSpawner` into `Cognition/Planning/Domain/OutpostGoapDomain.cs` (a static builder, or a DI-registered `IGoapDomain`). Move session population and world seeding (stall, bounties, Commons nodes) into `Core/Session/SessionBootstrap.cs`. `SwarmSpawner` keeps only the demo-pen scripting and calls into these.
2. **Delete the `CognitiveEngine` stub**, its registration and `ContextValidator`. Their only consumers are each other.
3. **Validator scope.** Move the ~30 validator registrations from `AppBootstrapper` into a `ValidationLifetimeScope` (child scope) in the testing assembly. It's instantiated by the PlayMode harness and by an Editor menu item / `-runValidators` batch arg. It never runs on a normal boot.
4. **asmdef split.** `Assets/Scripts/Testing/Simpiens.Testing.asmdef` references `Simpiens` and is Editor + test builds only (define constraint `UNITY_INCLUDE_TESTS` or Editor-only platform). `Simpiens.Tests.PlayMode` references `Simpiens.Testing`. Add `[assembly: InternalsVisibleTo("Simpiens.Testing")]` for the internals validators use (e.g. `ApplyProgressiveUnstuckReflex`).

### Verification
- Both assemblies compile. A normal Play boot logs **no** validator output. PlayMode 14/14. A player build contains no `Simpiens.Testing` types.
- Update AGENTS.md *Directories* (domain location, `Testing/` no longer hosts production) and remove the "⚠️ SwarmSpawner hosts production" note. Mark §1A.2 #9 and #10 resolved.

Commits: `refactor(planning): extract production GOAP domain from SwarmSpawner` · `refactor(core): session bootstrap and validation scope` · `chore(build): Simpiens.Testing assembly`.

---

## Phase 6 — Docs & promotion

Following the ROADMAP §0 order:
1. **ROADMAP.** Update the §3A rows (#5, #9, #10a; determinism). Update *Milestone 1 Status*. Set Epic 9 → `COMPLETED` only if §1B holds: validators pass, `OutpostSupplyLoopScenario` + `PriceResponseScenario` pass with no injection, and the checklist is reconciled.
2. **`epic-9.md`.** Update the Status header and acceptance checklist. MemoryPack and the two economy validators stay under **Deferred** (owner: Milestone 1 / MemoryPack slice).
3. **`.agents/curentfocus.txt`**, then append decisions to `.agents/architecture_history.md`: nav clearance model, determinism scheme, economy model, personality/considerations.

---

## Risks

| Risk | Mitigation |
| :--- | :--- |
| Phase 1 clearance changes make doorways or pen gaps impassable | Doorway pass-through case in `GridAStarValidator`; clearance cost is soft, only the body check is hard |
| Phase 4 changes demo-pen behaviour | Pen agents get fixed neutral personalities; anomaly scenarios are the regression gate |
| Economy tuning becomes a hand-tweaking loop | Tune only against `EconomySoakScenario` output with fixed seeds (Phase 2 makes runs comparable) |
| Phase 5 touches `SwarmSpawner` at the same time as Phase 1 | Sequence Phase 5 after Phase 1 merges, or keep Phase 1 out of `SwarmSpawner` |
| Arrival time of cognition results stays nondeterministic | Documented as remaining in §1A.2 #6; owned by the future multiplayer epic |

## Open questions for the producer

1. ~~Phase order~~: approved 2026-10-09.
2. Economy decisions 1–4 in Phase 3: **deferred** until the game is functional.
3. ~~Pen personalities~~: **neutral** (decided 2026-10-09).
4. ~~Branch~~: `feat/milestone1-supply-loop-zlogger` (decided 2026-10-09).

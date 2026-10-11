# Plan — GOAP Live-Loop Fixes from the SimLog Trace (2026-10-09)

> **Status: executed 2026-10-09.**
>
> - `GoapLiveLoopFixesValidator` passes 5 of 5. The full suite passes 10 of 14; the 4 failures are demo-pen anomaly checks.
> - Live loop: both seeded bounties were claimed and fulfilled by crawlers. Node-pool warnings dropped from about 2,000 to 0, and AidCompanion loops from about 16,000 to 0.
> - **Addition beyond plan:** affordability gating for BuyProvisions and SellSalvage, LiquidateSalvage and speculative gathering. This took purchase failures from 576 to 0.

The crawler decision trace (`Logs/outpost-supply-loop.log`, run of 2026-10-09 20:02) identified three defects that stop the §3.1 loop from emerging. Each fix below is general (no scenario-specific hacks) and keeps the §1A invariants: zero-alloc hot paths, single writer, deterministic selection, and null-safety for headless validators.

## Evidence
| # | Symptom in trace | Root cause |
|---|---|---|
| A | Crawlers chose `AidCompanion` about 16,000 times; every plan aborted at `ShareFood: TargetMissing` | `AidCompanionGoal.IsValid` is just `Hunger < 50`; nobody has to need help. `ShareFoodAction` is a stub that emits a `ShareResourceIntent` with no target, fails instantly, and the agent replans into the same goal. This crowds out every other goal and causes the Thinking↔Walking flicker. |
| B | Pen agents `Pen9.Agent19` and `Pen10.Agent20` claimed both Commons bounties at t181–182. About 2,200 `BuyItem … out of stall interaction range` rejections, all from walled pen agents. | Goals and actions only check that a stall or bounty *exists* in the snapshot, not that the agent can *reach* it. `TravelToStall` with an unroutable path completes instantly, so `BuyProvisions` then fails out of range, over and over. |
| C | About 2,000 `GOAP node pool exhausted (256 nodes)` warnings, mostly while planning FulfillBounty | The planner expands **every** applicable action (Rest, TravelToPeer, Socialize chains…) even when it can't help the goal. For unreachable or impossible goals it explores the whole state space. `IsValid` is also re-evaluated on every expansion. |

## Fix A — Real, need-driven companion aid
- **[NEW] `Cognition/Planning/AltruismTargeting.cs`:** the distressed-peer selection extracted from `AltruismEvaluator`, with the same rules (perceived within 6m, line of sight, peer hunger ≥ 85 or collapsed, altruism score ≥ 25). Ties break by GUID.
- **`AltruismEvaluator`:** uses the helper. Behaviour is unchanged, so the Pen 4 demo stays the same.
- **`AidCompanionGoal`:**
  - Valid only when the agent is sated (< 50) **and** a distressed peer is selectable.
  - Priority is that peer's altruism score.
- **`ShareFoodAction`:**
  - Valid only while a distressed peer is selectable.
  - Emits a `ShareResourceIntent` to that peer with a real path (optional pathfinder).
  - Falls back to a TargetMissing-style intent if the peer has vanished by execution time.
- **`SwarmSpawner`:** passes the pathfinder to `ShareFoodAction`.

## Fix B — Static reachability (connected regions)
- **`StaticNavGrid`:**
  - **Labels:** 4-connected region labels over walkable cells. 4-connectivity matches A*'s no-corner-cutting rule.
  - **Recompute:** only when a cell's walkability actually changes (version counter), done by `EnsureRegions()` on the main thread from `SpatialHashGrid.UpdateFromRegistry` after walls are rasterized.
  - **Publishing:** labels go into a new array, published by reference swap. Background threads only ever read a complete, immutable array, and allocation happens only when walls change.
  - **Query:** `TryGetRegion(worldPos)` snaps to the nearest walkable cell within 2 cells. `AreConnected(a, b)` returns **true when unknown** (no labels, out of bounds), so headless contexts are never blocked.
- **[NEW] `Cognition/Planning/Reachability.cs`:** `IsReachable(in AgentContext, Vector2 target)` via `context.Snapshot.StaticGrid`; null-safe.
- **Stall access:** `ResourceTargeting.GetStallAccess(ctx, out stall)` returns `None` (no stalls), `Unreachable`, or `Reachable` (nearest reachable stall).
  - **Uses the nearest reachable stall:** `TryGetNearestStall` (used by every stall action), `BuyProvisionsAction`, `SellSalvageAction`, `DeliverBountyAction`, `ReportScoutBountyAction`.
  - **`TravelToStallAction`:** paths to the nearest reachable stall. `IsValid` is false when every stall is unreachable.
  - **`PurchaseProvisionsGoal`, `EarnIncomeGoal`, `LiquidateSalvageGoal`:** invalid when `Unreachable`. `None` keeps the legacy headless behaviour.
- **Open bounties:** `BountyQueries.TryGetOpenBounty` only returns bounties whose `TargetLocation` (if set) **and** creator stall are reachable. This flows into the claim actions, `SearchSalvage` and `ScoutSector` validity.
- **`FulfillBountyGoal`:** uses `BountyQueries` (own claim, or reachable open bounty) instead of its own unfiltered loop.

## Fix C — Planner relevance pruning
- **`GoapPlanner.Plan`:** before searching, compute a per-call relevance closure over the action graph:
  - **Relevance:** start from the goal's facts. An action is relevant if its effects touch a relevant fact; its preconditions then become relevant. Iterate to a fixpoint. Irrelevant actions can never help reach the goal, so pruning them is sound.
  - **Validity:** evaluate `IsValid(context)` once per relevant action per call. This is equivalent today, because the context is fixed during a plan.
  - **Early out:** if an unsatisfied goal fact has no valid, relevant producer, return false immediately. Impossible goals stop being exhaustive searches.
  - **Expansion:** iterate graph actions directly, skipping irrelevant or invalid ones. No `GetApplicableActions` list churn.
  - **Allocation:** flag arrays are preallocated and grow only if the graph grows.
- Pool size stays 256. The existing warning remains as a regression signal.

## Verification
- **New `GoapLiveLoopFixesValidator` (PlayMode `[Test]`):**
  1. AidCompanion is invalid with no distressed peer; valid, and `ShareFood` targets the peer, when one is starving in view.
  2. `StaticNavGrid` regions: a walled pen is disconnected from the outside, an open yard is connected, the result is unknown-safe, and labels are recomputed only when walls change.
  3. A walled-in agent can't claim, plan for, or use a stall/bounty outside its region; an agent outside can.
  4. Relevance pruning: an impossible goal returns false without exhausting the pool (node count far below 256); a large graph still finds the same optimal plan as before.
  5. Zero GC for `AreConnected` and pruned planning in steady state.
- Scratch Roslyn build; full `scripts/run-playmode-tests.sh`; inspect `Logs/outpost-supply-loop.log`:
  - no `AidCompanion` loop;
  - no claims by pen agents;
  - no pool-exhaustion warnings;
  - crawler FulfillBounty plans.

## Docs
`architecture_history.md` entry. If `OutpostSupplyLoopScenario` passes, the ROADMAP Milestone 1 status and §1A.2 lines are updated (Epic 9 still stays `MECHANISM COMPLETE` by producer decision).

## Out of scope (noted, not fixed)
- **Stall treasury drains fast:** 250g to 15g in about 550 ticks from salvage sales. This is faucet/sink tuning, owned by §3.3.
- **Ghost walls:** `StaticNavGrid` is never cleared on `Respawn`, so walls injected by `DynamicChaosScenario` persist into later scenarios. Flagged for the harness track.

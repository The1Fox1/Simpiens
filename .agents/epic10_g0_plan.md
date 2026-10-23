# Epic 10 G0: a day that holds on every seed (working plan)

Working document (AGENTS.md, Documentation Protocol 6): deleted when G0 closes. Epic: `docs/epics/epic-10.md` §2.2, §3.
Branch `feat/epic10-g0`; one pull request into `main` (Q10).

## Goal
The 15-minute outpost day passes its checks on all 23 gate seeds (the three golden seeds and 1 to 20): nobody
starves, every crawler closes a harvest-to-sell cycle, both bounties are delivered, the ledger balances. Epic CORE
finding F2 recorded 7 failing seeds (re-measured 2026-10-23 on `main`: 4, 8, 10, 11, 12, 16, 18).

## Diagnosis (2026-10-23)
No food shortage: every failure was a crawler that could not move or kept retrying something that could not work.
1. **Frozen by an occupied destination.** `ExecutePathMovement` stood still while its next waypoint was occupied.
   Smoothed paths often have two waypoints, so the next one is the destination: a crawler idling at the stall froze a
   crawler across the map, or let it creep 0.27 units per watchdog cycle (seeds 4, 10, 12).
2. **A sale the stall refuses, retried every tick.** `SellSalvageAction` asked whether the stall had room for either
   salvage; a crawler holding ore retried a refused sale while the stall was full of ore (seed 19 after fix 1).
3. **Lying down on a crawler still in the bunk.** `StepOntoBunk` ignored pawns; once walkers reached occupied bunks
   (fix 1), they stacked there.
4. **Panic in place.** A panicking crawler blocked by a pawn skipped every waypoint, "finished" and panicked again on the
   spot (seeds 18, 58); a sidestep fixes that but needs a time limit (one panic lasted 4,000 ticks).
5. **Searching away from food.** A hungry crawler with empty plots around it explored the least-visited sector (the Road
   Gate) and starved on the walk back (seed 73).

## Steps
- [x] 1. Fixes 1–5 in the core; `OutpostWatch` gains a stacking check that mirrors Unity's anomaly detector and the
      start tick of the longest hungry stretch.
- [x] 2. The greedy soak band is judged at its lowest trust mean (fixes 4–5 removed the late gouged purchases that kept
      the mean below 0 at minute 30); both soaks and the economy model.
- [x] 3. `dotnet test` gate: `OutpostScenarioTests.TheOutpostsDayHoldsOnEverySeed`, 23 seeds, with stacking in
      `DayFailures`.
- [ ] 4. Golden hashes re-recorded in Unity (`scripts/golden-hashes.sh record`), matched headless.
- [ ] 5. Unity PlayMode suite.
- [ ] 6. Client references re-recorded in the pinned image (`scripts/test-client-in-container.sh --record`), looked at,
      committed on their own.
- [ ] 7. Docs: F2 resolved (epic CORE, epic 10), the new trust finding, roadmap, focus, history; this plan deleted.

## Measured
- Gate seeds: 23/23 pass, stacking 0 (was 16/23).
- Seeds 21–120: 96/100 pass; the 4 failures are trust rising for 6–7 of 12 (need 8): crawlers that trade early and then
  spend the day at the hearth. A balance finding for G1 and G4, not a stuck crawler.
- Economy soak 9/9.

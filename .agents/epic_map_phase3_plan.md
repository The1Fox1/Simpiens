# Epic MAP Phase 3: Populate the Outpost Floor and Its Physical Lifecycle (Implementation Plan)

- **Epic:** [Epic MAP Revision 2](../docs/epics/epic-map.md), Phase 3. Status is owned by `docs/ROADMAP.md`.
- **Branch:** `feat/epic-map-revamp` (continues from Phase 2, `a646989`; not yet merged).
- **Written:** 2026-10-11. **Awaiting producer approval.** Phase 2's visual check passed 2026-10-11.
- **Goal:** the live game runs on `Floor1Layout`. Crawlers walk in from the Road Gate, work the quarry, grove and garden, sell at the stall, gather at the hearth and sleep in the bunkhouse. Scouting the frontier has a real payoff. Every economy and anomaly scenario runs on that floor, and the soak produces the numbers for the §3.3 economy decision.
- **What the producer sees after Phase 3:**
  - Pressing Play shows the whole outpost floor: 8 rooms and their walls.
  - 12 crawlers walk in from the Road Gate, through the Bunkhouse, into the Hub.
  - They harvest in the Ore Quarry, Herb Grove and Garden, sell at the stall, and sit around the hearth.
  - Tired crawlers walk to the Bunkhouse and sleep in a bunk. When the bunks are full, they rest where they stand.
  - One crawler takes the seeded Scout bounty into the **Deep Seam**. Once it is charted, crawlers mine its richer veins and the seeded Gather bounty pays out.
  - The **Overgrowth** stays uncharted until the player posts a Scout bounty for it.
  - The Dev Map toggle still switches to the pens and back.

---

## 1. Findings from the code (why each step exists)

| # | Finding | Where | Consequence for Phase 3 |
| :-: | :--- | :--- | :--- |
| H1 | `Floor1Layout` is validated but never spawned. The spawner instantiates resource nodes and `Hearth` POIs only; `Bunk` and `SpawnPoint` POIs are ignored. The layout has 6 spawn points and 4 bunks. | `SwarmSpawner.cs:167-180`, `Floor1Layout.cs` | The spawner spawns bunks. The layout grows to 12 spawn points and 8 bunks (data edit). |
| H2 | **Rest is invisible.** `RestAction` rests wherever the agent stands: +15 energy/s while resting and +30 on completion, through a 2 s `IdleIntent`, at a static cost of 1.0. `GoapAction.GetCost(context)` is virtual, so a dynamic price is available. | `RestAction.cs`, `AgentState.cs:194-210`, `AgentConsequenceService.cs:76-79` | Bunk preference is a price in `Considerations`, not a hard rule. Resting in the field stays as the fallback. |
| H3 | **There is only one anchor type.** `EntityType.Anchor` means a hearth. `TravelToAnchor` walks to the nearest anchor, and `GatherAtAnchorGoal` is valid once any anchor is known. | `TravelToAnchorAction.cs`, `GatherAtAnchorGoal.cs`, `WorldStateBuilder.cs:123` | If bunks were anchors, hearth gatherings would turn into trips to the bunks. Bunks need their own entity type and facts. |
| H4 | **Exploration is a random walk.** `PickExplorationPoint` picks a point 5–9 units away in a hashed random direction. Perception (vision 6) respects line of sight through walls. | `ResourceTargeting.cs:172`, `AgentMemory.UpdateMemory` | This was fine in the open yard. On a walled floor most waypoints land in walls, rock or the same room, so finding a 4-cell door is luck. Exploration has to know about sectors. |
| H5 | **Shroud has no effect on the simulation.** Only the UI reads `ISectorRegistry.IsExplored`. Agents perceive, target and harvest inside "shrouded" sectors freely. On the Commons, the seeded Gather bounty in the shrouded Ore Ridge completed at t155, before anyone scouted. A Scout bounty's only effect is a UI flag. Exploration state is mutable main-thread data, and cognition runs off-thread. | `SectorRegistry.cs`, `DeliverBountyIntentHandler.cs:85` | Decision **D1** (§7). If shroud is to mean anything, explored state reaches cognition through the snapshot (single-writer rule). |
| H6 | Gather fulfilment checks only item type and quantity, not where the item came from. | `BountyQueries.HasBountyItems` | Accepted (§7 #6). A "Deep Seam ore" bounty can be paid with quarry ore. |
| H7 | **The Floor 1 hearth blocks a door.** The hearth at (0, −3) has a gathering ring 2 units out, so it sits 1 unit from the Hub–Bunkhouse door (x −3..3, y −6). It is also on the straight line from that door to the stall at (0, 2.5). With Phase 2 finding #3 (path smoothing ignores anchors), paths run through the fire, and a hearth crowd would jam the busiest door. | `Floor1Layout.cs:106` | Move the hearth off the door lanes, fix the smoothing, and add a validator check that the hearth ring clears every door lane. |
| H8 | `OutpostSupplyLoopScenario`'s deadlines were set on the yard, where trips are about 10 units. On Floor 1, quarry to stall is about 18 units through a door, and crawlers start at the Road Gate. | `OutpostSupplyLoopScenario.cs:23-26` | Re-observe and rebound: slowest × 1.5, capped. Same policy as Phase 2. |
| H9 | `OutpostCommonsLayout` is used by `OutpostSession`, `SupplyLoopValidator`, `WorldMapValidator`, `TestWorlds` and `MapDefinitionValidator`. `SessionSwitchScenario` checks Commons sectors 12/13. | grep | The Commons is retired. Each user moves to Floor 1 or to a `TestWorlds` fixture. |
| H10 | Duplicate `LeadershipAssumed` events (Phase 2 §8 #2) reach the live game once groups form at the outpost hearth. They add noise to the dashboard's chronicle and push real events out of its 1,024-event buffer. | `SuccessionEngine`, `SimulationManager` | Fixed in Phase 3 (step 2). |
| H11 | `SwarmNavigationScenario` and `DynamicChaosScenario` run on the Dev Map. `ResourceContentionScenario` exercises the contention in pens 1–3 by design. | `Tests/PlayMode/Scenarios/` | Navigation and chaos move to Floor 1, where the crowding is real (12 crawlers through 4-cell doors). Contention stays on the Dev Map (§7 #4). |

---

## 2. Design

### 2.1 Floor 1 goes live
- **Layout edits** (`Floor1Layout`, data only):
  - **12 spawn points** in two rows of 6 across the Road Gate.
  - **8 bunks** in two rows of 4 across the Bunkhouse.
  - **The hearth moves out of the door lanes.** Candidate: (−4.5, −3), with the Hub–Bunkhouse door shifted east to x 4 so the hub's south-west quarter carries no through-traffic. The ASCII path probe picks the final position, and the new validator check (below) pins it.
  - Named **door centres** (`Floor1Layout.Doors`), shared by the validator and `DynamicChaosScenario`.
  - `CameraFrame` covers the whole floor. Pan and zoom are Phase 4.
- **`OutpostSession` on Floor 1:**
  - 12 crawlers with seeded personalities, one per spawn point, labelled `Crawler0..11`. The count is a constructor parameter (default 12); above 12, extra crawlers take the same spawn points offset by a row. Used by the probe (§2.5).
  - The stall at `hub.stall`, with **today's terms** (§7 #5).
  - Seeded bounties: **#2 Scout Deep Seam** (15g) and **#1 Gather iron ×1 in Deep Seam** (25g). The Gather bounty becomes claimable once Deep Seam is charted (§2.3). Both expire at session tick 6000 (§7 #8).
  - Crawlers start with empty memory. They discover the bunks and the hearth by walking in from the gate through the Bunkhouse (§7 #7).
- **The spawner spawns bunks** for `PoiKind.Bunk` (entity and placeholder view, §2.4), like it does hearths.
- **The Commons is retired.** `OutpostCommonsLayout` is deleted. Validators that need an open yard get a small fixture in `TestWorlds`; the others move to Floor 1.

### 2.2 Sector-directed exploration (H4)
- `PickExplorationPoint` chooses a **sector**, then a point in it:
  - candidates are the map's sectors other than the one the agent stands in (subject to the shroud rule, §2.3);
  - the pick is hashed on agent and tick/50, as now, so it stays deterministic;
  - the destination is the sector's anchor (or centre) with a small hashed jitter;
  - the pathfinder routes it through the doors. An unroutable pick falls back to today's short random waypoint.
- `MapDefinition` is immutable plain data, so cognition reads it off-thread through the snapshot (a reference set when the map loads).
- Null-safe: with no map, as in headless validators, today's behaviour is unchanged.
- `SearchSalvage` keeps heading for an advertised bounty location first.

### 2.3 Shroud charts the frontier (decision D1, recommended)
- The snapshot carries an **explored-sector mask**: a `ulong` with bit *i* set when sector *i* is explored. The main thread writes it when it builds the snapshot; cognition only reads it. A validator check keeps sector ids below 64.
- **Rules:**
  1. Exploration never picks an unexplored sector.
  2. Resource and salvage targeting ignores remembered nodes inside unexplored sectors.
  3. A Gather bounty whose target sector is unexplored is not fulfillable, so nobody claims it.
  4. **The Scout claimant is exempt.** It walks into its target sector and remembers what it sees there. Once it delivers, the sector is charted for everyone, and rules 1–3 lift.
- **Effect:** the yield-6 frontier veins sit idle until someone scouts them. Scouting then visibly pays off: crawlers start making the longer trip for richer nodes, and the seeded Gather bounty becomes claimable. This is the chain the epic §2.1.3 intends.
- **If the producer declines D1:** shroud stays UI-only. The Deep Seam Gather bounty is claimable from tick 0, crawlers wander into the frontier by chance, and the Scout bounty only changes the dashboard.

### 2.4 Bunks and the rest anchor (epic §2.3)
- **Entity:** `EntityType.Bunk` (appended to the enum). A bunk is walkable, not an obstacle: the sleeper stands on it. It gets a placeholder sprite until Phase 4.
- **Facts:** `KnowsBunkLocation` and `AtBunk` (within reach of a bunk the agent may use).
- **Actions:**
  - `TravelToBunk`: to the nearest known bunk that the snapshot shows as free.
  - `SleepAtBunk`: requires `AtBunk`; its effect is `IsResting`. It emits a typed `SleepAtBunkIntent(bunkId)`.
  - `RestAction` is unchanged and stays in the graph.
- **Occupancy is authoritative on the main thread.** The handler claims the bunk or fails, and a failure briefly blacklists that bunk in the agent's memory. The occupant is published in the snapshot. The bunk is freed when the sleep ends, or when the agent is preempted or destroyed.
- **Recovery:** sleeping in a bunk restores energy twice as fast as field rest, plus a larger completion bonus. Starting point: +30/s for 3 s, then +40 (field rest is +15/s for 2 s, then +30). The step 7 observation run tunes these numbers, and the plan records the final values.
- **Price (`Considerations`):**
  - `TravelToBunk` costs base + distance/k.
  - `RestAction.GetCost` rises while a free bunk is known and energy is above a "too tired to walk" floor (about 15), so a walk to the bunk beats resting in the field.
  - Below that floor, field rest is cheapest. Exhaustion collapse (energy ≤ 5) remains the forced fallback.
- **Mechanism validator:** `RestAnchorValidator` checks:
  - a tired agent who knows a free bunk plans `TravelToBunk → SleepAtBunk`;
  - one with no bunk, or below the floor, plans `Rest`;
  - two agents can't hold one bunk;
  - the bunk is freed on preemption.

### 2.5 Population and the scaling probe
- **12 crawlers** live (6 today).
- **`PopulationScalingProbe`** is an `[Explicit]` PlayMode test. It runs `OutpostSession(crawlerCount: N)` for N = 12, 16, 24, for 3,000 ticks each, at the maximum multiplier. It records:
  - ticks per wall-second;
  - cognition latency in ticks (request to intent arrival): mean and p95;
  - the share of plans that waited on the `GoapPlanner` lock, if measurable cheaply.
- It reports numbers and asserts only integrity: ledger conserved, horizon reached.
- **Escalation rule:** if 24 crawlers sustain under 100 ticks/s (10× real time), or p95 latency exceeds 10 ticks, the numbers go to ROADMAP §3A #8 as the case for cognition scaling. Scaling work itself is out of scope.

### 2.6 Scenario migration (no hard-coded coordinates in `Tests/`)

| Scenario | Session | Change |
| :--- | :--- | :--- |
| `OutpostSupplyLoopScenario` | Floor 1 | Asserts the chain: Scout Deep Seam fulfilled → Deep Seam charted → the Gather bounty fulfilled *after* the scout, with player trust up; first sale; regrowth; stall restock; 0 anomalies. Deadlines are rebounded from observation (H8). |
| `PriceResponseScenario` | Floor 1 | 12 crawlers. `StepTicks` is re-observed, because longer trips mean fewer shopping visits per step. |
| `EconomySoakScenario` | Floor 1 | New CSV columns for the economy report (§2.7). |
| `SessionSwitchScenario` | both | Counts come from `OutpostSession`. Its shroud checks use the Floor 1 frontier sectors. |
| `SwarmNavigationScenario` | **Floor 1** (was Dev Map) | 12 crawlers crossing 4-cell doors; 0 anomalies. |
| `DynamicChaosScenario` | **Floor 1** (was Dev Map) | Injects a temporary wall into the Hub–Bunkhouse door (from `Floor1Layout.Doors`) and asserts traffic reroutes or waits without stacking, then that it recovers once the wall is removed. |
| `ResourceContentionScenario` | Dev Map | Unchanged (§7 #4). |
| `PenCanaryScenario` | Dev Map | Its rally points move into `DevMapLayout` constants. That was the last hard-coded coordinate in `Tests/`. |
| **New `BunkRestScenario`** | Floor 1 | No injection. Within its bound, at least 3 distinct crawlers complete a bunk sleep; no bunk ever holds two sleepers; no crawler starves. The bound comes from observation × 1.5, capped at 4,000 ticks. |

### 2.7 Soak re-run: the §3.3 economy input
- `scripts/run-economy-soak.sh` on Floor 1 with 12 crawlers: 3 seeds × provisional export on/off, as before.
- **New CSV columns:**
  - rations bought versus garden meals;
  - bunk sleeps versus field rests;
  - exhaustion collapses;
  - starvation events;
  - mean trip length (work → sale).
- **Report** (appended to this plan as §9, and summarised for the producer):
  - the stall treasury trajectory;
  - ration stock-outs;
  - gold per crawler;
  - sales per sim-minute;
  - all of the above against the Phase 2 yard numbers.
- No economy tuning beyond the data-only limits in §7 #5.

---

## 3. Steps and commits

Each step must compile and keep PlayMode green (`scripts/run-playmode-tests.sh`, Editor closed). Each commit body states the real test status.

| Step | Work | Gate | Commit |
| :-: | :--- | :--- | :--- |
| 1 | **Smoothed paths respect hearths and stalls** (Phase 2 §8 #3). Path smoothing's line of sight treats the centre cell of a hearth or stall as blocked, matching `IsWalkable`. | New `GridAStarValidator` test (fails without the fix); full suite; canary still passes | `fix(navigation): path smoothing no longer cuts through hearths and stalls` |
| 2 | **One `LeadershipAssumed` per real change** (H10, Phase 2 §8 #2). Succession records the event only when the leader actually changes. | Group-dynamics validator test; canary still passes (Pens 8 and 9 still see their leader events) | `fix(social): chronicle leadership only when it changes` |
| 3 | **Floor 1 data for live use** (§2.1). 12 spawn points, 8 bunks, the hearth off the door lanes, `Doors`, camera frame. New `MapDefinitionValidator` checks: the hearth ring clears every door lane; spawn points and bunks are walkable and sit in their sectors; sector ids < 64. Not live yet. | `MapDefinitionValidator`; ASCII probe of gate → hub → each sector paths, none crossing the hearth ring | `feat(world): Floor 1 layout ready for the live session` |
| 4 | **Sector-directed exploration** (§2.2). | New `ExplorationValidator` (destinations lie in other sectors and are routable through doors; deterministic; null map = old behaviour); full suite with the Commons still live, and the supply loop still passes | `feat(cognition): exploration heads for sectors, not random points` |
| 5 | **Floor 1 goes live** (§2.1, §2.6). `OutpostSession` on Floor 1 with 12 crawlers; bunk entities spawned (no bunk behaviour yet); the Commons retired; validators re-pointed; scenarios migrated. Observation run first, then the supply-loop and price-response bounds. | PlayMode full suite **×3**; `OutpostSupplyLoopScenario` passes with 0 anomalies (new milestones recorded); grep: no `OutpostCommonsLayout` anywhere | `feat(session): the live game runs on the outpost floor` |
| 6 | **Shroud charts the frontier** (§2.3; only if D1 is approved). Explored mask in the snapshot; rules 1–4. | New `SectorChartingValidator`; the supply loop asserts scout-before-gather ×3 | `feat(cognition): uncharted sectors stay off-limits until scouted` |
| 7 | **Bunks and the rest anchor** (§2.4). Observation run to tune recovery and price. | `RestAnchorValidator`; `BunkRestScenario` ×3; full suite | `feat(cognition): tired crawlers sleep in the bunkhouse` |
| 8 | **Anomaly scenarios on Floor 1** (§2.6). | Navigation and chaos pass ×3 with 0 anomalies. A long-run jitter check (Phase 2 §8 #4) on Floor 1 is reported either way. | `test(navigation): anomaly scenarios run on the outpost floor` |
| 9 | **`PopulationScalingProbe`** (§2.5). | Numbers recorded in §9 of this plan; escalation rule applied | `test(perf): population scaling probe at 12, 16 and 24 crawlers` |
| 10 | **Soak on Floor 1** (§2.7). | 6/6 integrity; report written | `docs(map): Floor 1 economy soak report` |
| 11 | **Docs and the producer's visual check.** ROADMAP, the epic checklist, `curentfocus.txt`, `architecture_history.md` (Floor 1 live, shroud semantics, bunks, Commons retired), AGENTS.md (`Layouts/`, scenario placement). **Producer check in the Editor:** crawlers walk in from the gate, work, sell, gather at the hearth, sleep in bunks; after the scout, they mine Deep Seam. | Docs agree with each other; producer confirms | `docs: Epic MAP Phase 3 complete` |

---

## 4. Tests added or changed

- **New:**
  - `ExplorationValidator`;
  - `SectorChartingValidator` (if D1);
  - `RestAnchorValidator`;
  - `BunkRestScenario`;
  - `PopulationScalingProbe` (`[Explicit]`).
- **New checks in existing validators:**
  - `GridAStarValidator`: smoothing around a hearth;
  - the group-dynamics validator: no duplicate leadership event;
  - `MapDefinitionValidator`: Floor 1 live-use checks.
- **Changed:**
  - `OutpostSupplyLoopScenario`, `PriceResponseScenario`, `EconomySoakScenario` and `SessionSwitchScenario` move to Floor 1;
  - `SwarmNavigationScenario` and `DynamicChaosScenario` move from the Dev Map to Floor 1;
  - `PenCanaryScenario`'s rally points come from the layout;
  - `SupplyLoopValidator`, `WorldMapValidator` and `TestWorlds` drop the Commons.
- **Expected count:** 43 + 4 = **47** PlayMode tests (46 if D1 is declined), plus the two explicit runs (soak, probe).

---

## 5. Risks

| Risk | Mitigation |
| :--- | :--- |
| **Longer trips starve crawlers** (gate → hub → quarry → stall, with 12 mouths on one stall and 4 garden plots) | `BunkRestScenario` and the soak assert no starvation. If they fail, tune data only (garden plots and yield, stall ration stock) and report every change (§7 #5). |
| **Door jams at the gate at t0**: 12 crawlers start together behind two 4-cell doors | Two spawn rows; navigation and chaos run on Floor 1 with the anomaly detector. If stacking shows, stagger the first planning tick per crawler (a hashed 0–20-tick delay). |
| **Discovery on a walled floor is still slow** after step 4 | Step 4's gate runs on the Commons, and step 5's observation run shows the Floor 1 discovery times. Gossip at the hearth already spreads spatial memory. Seeding residents' memory is a last resort (it would weaken the DoD story), so it needs the producer's approval. |
| Milestones move again (new map, 12 crawlers, new ids) | Expected; same policy as Phases 1–2: re-observe ×3, never loosen a check past slowest × 1.5. A missing outcome is a finding, fixed at its cause. |
| The shroud rule leaves crawlers idle when the near sectors run dry and nobody scouts | Regrowth keeps the near sectors producing. The seeded Scout bounty charts Deep Seam early. Overgrowth waiting for the player is intended. |
| Bunk occupancy leaks (a sleeper is preempted or destroyed and the bunk stays taken) | Freed in the handler's end path and on agent removal; `RestAnchorValidator` covers preemption; `BunkRestScenario` asserts single occupancy every tick |
| 12+ crawlers expose the locked `GoapPlanner` | Measured in step 9 and escalated with numbers (§2.5), not fixed here |
| Moving the anomaly scenarios loses the pens' 24-agent stress | Contention stays on the Dev Map; the probe's 24-crawler run on Floor 1 replaces the crowd stress with a realistic one |

---

## 6. Out of scope (later phases)
- **Phase 4:** tilemap and wall rendering, the shroud overlay, pan, zoom and follow, the sector-aware bounty picker, final bunk and hearth art. The existing Bounty modal can already post a Scout bounty for Overgrowth; Phase 4 makes its picker list the real uncharted sectors.
- **Phase 5:** `OutpostLifecycleScenario` and the determinism check.
- **Elsewhere:** cognition scaling (§3A #8), drifter arrival and departure (§3.3 / Epic 9), and item provenance on Gather bounties.
- **Phase 2 §8 #1** (Pen 7 leadership hand-off): Dev Map only; still open, not touched here.

## 7. Decisions

**D1 (changes game feel; producer to confirm). Recommended: shroud gates knowledge (§2.3).** Uncharted sectors are off-limits to everyone except the scout, until a Scout bounty charts them. Without D1, scouting changes only the dashboard, and the frontier is reached by chance.

**Minor decisions (producer may override):**
2. **The Commons is retired**, not kept as a second production map. One live map, one source of truth.
3. **8 bunks for 12 crawlers.** The bunkhouse never fits everyone at once, so the scarcity is visible and field rest stays exercised.
4. **`ResourceContentionScenario` stays on the Dev Map.** Pens 1–3 are purpose-built contention. Navigation and chaos move to Floor 1.
5. **Stall terms are unchanged at 12 crawlers.** If crawlers starve, only data is tuned (garden plots and yield, ration stock), and every change is reported. Economy design stays deferred, as the producer asked.
6. **Gather fulfilment doesn't check provenance** (H6).
7. **Crawlers start with empty memory** and discover the outpost by walking in. Nothing is seeded, which keeps the Phase 5 "no injection" story clean.
8. **Seeded bounties keep their 6,000-tick expiry**, unless observation shows the scout → gather chain can't fit; then the expiry is raised as data and reported.

## 8. Carried findings
- Phase 2 §8 #2 (duplicate leadership events): **fixed** in step 2.
- Phase 2 §8 #3 (paths through hearths): **fixed** in step 1.
- Phase 2 §8 #4 (Pen 6 jitter): rechecked on Floor 1 in step 8.
- Phase 2 §8 #1: open (§6).

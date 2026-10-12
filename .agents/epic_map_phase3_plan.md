# Epic MAP Phase 3: Populate the Outpost Floor and Its Physical Lifecycle (Implementation Plan)

- **Epic:** [Epic MAP Revision 2](../docs/epics/epic-map.md), Phase 3. Status is owned by `docs/ROADMAP.md`.
- **Branch:** `feat/epic-map-revamp` (continues from Phase 2, `a646989`; not yet merged).
- **Written:** 2026-10-11. **Approved** by the producer 2026-10-11, with D1 = fog of war (§2.3). Phase 2's visual check passed 2026-10-11.
- **Implementation status (2026-10-12): steps 1–11 done** (`10c321f`..). PlayMode **47/47** in 3 consecutive runs, identical milestones each time (48 total with the explicit soak skipped; the population probe is explicit too).
  - `OutpostSupplyLoopScenario` on Floor 1: first sale t234, scout reports t357 (all 3 Deep Seam veins charted for all 12 crawlers), gather t569 (after the charting), regrowth t852, restock t600; 0 fog leaks, 0 anomalies.
  - `BunkRestScenario`: first bunk sleep t811, third sleeper t1375, 10 distinct sleepers, 0 starving (longest stretch at 95+ hunger 275 ticks), 6 collapses.
  - `PopulationScalingProbe`: 12 / 16 / 24 crawlers at 1465 / 977 / 535 ticks per second unthrottled (146× / 98× / 54× real time). No escalation (§2.5).
  - Economy soak on Floor 1: §9.
  - **Pending:** the producer's visual check in the Editor (§3 step 11).
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

### 2.3 Shroud is fog of war (decision D1, producer 2026-10-11)
> "Shroud is a fog of war. An adventurer should not know what is inside an uncharted room until a scout visits then successfully reports back."

- The snapshot carries an **explored-sector mask**: a `ulong` with bit *i* set when sector *i* is charted. The main thread writes it when it builds the snapshot; cognition only reads it. A validator check keeps sector ids below 64.
- **Rules:**
  1. **Perception:** an agent records nothing static (resource nodes, anchors, bunks, stalls) that lies inside an uncharted sector, even when it is in line of sight through a door. Pawns stay visible: a crawler can see a colleague walk into the fog.
  2. **The scout is the exception.** The claimant of a Scout bounty records what it sees inside *its* target sector. That knowledge is private:
     - gossip never passes on memories of nodes inside an uncharted sector;
     - the scout's own targeting ignores them until the sector is charted, so it doesn't stop to mine on the way out.
  3. **Charting happens only on a successful report.** When the scout delivers at the stall:
     - the sector is marked charted;
     - the **chart is published**: every crawler's spatial memory receives the scout's remembered nodes in that sector. This is a main-thread write in the delivery handler, ordered by entity id.
     - Rules 1, 2, 4 and 5 lift for that sector.
  4. Exploration never picks an uncharted sector.
  5. A Gather bounty whose target sector is uncharted is not fulfillable, so nobody claims it.
- **If the scout never reports** (it abandons the claim, the bounty expires, or the agent is removed), the sector stays uncharted, and what the scout saw stays in its memory only, unused.
- **Effect:** nobody knows about the yield-6 frontier veins until a scout comes back. Then the whole outpost learns of them at once, crawlers start making the longer trip, and the seeded Gather bounty becomes claimable. This is the chain the epic §2.1.3 intends.
- **Dev Map:** pen sectors 6–10 start uncharted in `DevMapLayout`. Rule 1 would blind the pen agents, and nobody scouts there, so those pens become `startsExplored: true`. Tests that use a Dev Map sector as their "shrouded" example (`BountyTerminalValidator` via `TestWorlds.DevMapZones`) switch to a fixture with an uncharted sector. The canary must still pass unchanged.

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
| 6 | **Fog of war** (§2.3). Explored mask in the snapshot; rules 1–5; chart publication on report; Dev Map pens explored. | New `SectorChartingValidator`: no perception through the fog; the scout's knowledge is private (no gossip, no targeting); a report publishes the chart; an abandoned scout charts nothing; uncharted Gather bounties can't be claimed. The supply loop asserts that **no crawler knows a Deep Seam node before the scout's report** and that the gather comes after it, ×3. The canary still passes. | `feat(cognition): fog of war over uncharted sectors until a scout reports back` |
| 7 | **Bunks and the rest anchor** (§2.4). Observation run to tune recovery and price. | `RestAnchorValidator`; `BunkRestScenario` ×3; full suite | `feat(cognition): tired crawlers sleep in the bunkhouse` |
| 8 | **Anomaly scenarios on Floor 1** (§2.6). | Navigation and chaos pass ×3 with 0 anomalies. A long-run jitter check (Phase 2 §8 #4) on Floor 1 is reported either way. | `test(navigation): anomaly scenarios run on the outpost floor` |
| 9 | **`PopulationScalingProbe`** (§2.5). | Numbers recorded in §9 of this plan; escalation rule applied | `test(perf): population scaling probe at 12, 16 and 24 crawlers` |
| 10 | **Soak on Floor 1** (§2.7). | 6/6 integrity; report written | `docs(map): Floor 1 economy soak report` |
| 11 | **Docs and the producer's visual check.** ROADMAP, the epic checklist, `curentfocus.txt`, `architecture_history.md` (Floor 1 live, shroud semantics, bunks, Commons retired), AGENTS.md (`Layouts/`, scenario placement). **Producer check in the Editor:** crawlers walk in from the gate, work, sell, gather at the hearth, sleep in bunks; after the scout, they mine Deep Seam. | Docs agree with each other; producer confirms | `docs: Epic MAP Phase 3 complete` |

---

## 4. Tests added or changed

- **New:**
  - `ExplorationValidator`;
  - `SectorChartingValidator`;
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
- **Expected count:** 43 + 4 = **47** PlayMode tests, plus the two explicit runs (soak, probe).

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

**D1 (decided by the producer 2026-10-11): shroud is fog of war (§2.3).** Nobody knows what is inside an uncharted room until a scout visits it and successfully reports back. The successful report publishes the chart to every crawler.

**Minor decisions (producer may override):**
2. **The Commons is retired**, not kept as a second production map. One live map, one source of truth.
3. **8 bunks for 12 crawlers.** The bunkhouse never fits everyone at once, so the scarcity is visible and field rest stays exercised.
4. **`ResourceContentionScenario` stays on the Dev Map.** Pens 1–3 are purpose-built contention. Navigation and chaos move to Floor 1.
5. **Stall terms are unchanged at 12 crawlers.** If crawlers starve, only data is tuned (garden plots and yield, ration stock), and every change is reported. Economy design stays deferred, as the producer asked.
6. **Gather fulfilment doesn't check provenance** (H6).
7. **Crawlers start with empty memory** and discover the outpost by walking in. Nothing is seeded, which keeps the Phase 5 "no injection" story clean.
8. **Seeded bounties keep their 6,000-tick expiry**, unless observation shows the scout → gather chain can't fit; then the expiry is raised as data and reported.

## 6A. Deviations from this plan during implementation
- **Extra fixes, each found by a live scenario on Floor 1** (one commit each):
  - `8203394` **bounty reservation:** the Gather bounty claimant kept selling the ore it owed the bounty (`LiquidateSalvage` outranks `FulfillBounty`); delivery took 3,373 ticks instead of 560.
  - `201750b` **finding things on a walled floor:**
    - agents now also perceive while walking (they were blind between intents);
    - starvation panic heads for another charted sector instead of 2.5–4.5 units in a random direction;
    - searches take the sector visited least recently. Before this, a crawler panicked for 2,000+ ticks without finding the Garden.
  - `ba17023` **crawlers arrive fed** (hunger 20 instead of 50): all 12 used to hit starvation panic together about 23 s in, before earning the gold for a ration.
  - `c315ee8` **the scout surveys from the middle of the room** (survey radius 2.0 → 1.25): it used to chart 2 of the Deep Seam's 3 veins.
  - `0ceb890` **an affordable stall ration counts as known food:** a starving crawler stood at the stall with 20 gold and panicked instead of buying, because only remembered food plots counted.
- **Fog of war design (D1):** the scout's private knowledge is a separate store of survey notes, not filtered memory, so gossip and targeting need no fog checks. A scout's visit means reaching the sector's survey point (it used to succeed at the threshold and saw only the doorway).
- **Bunks** were spawned in step 7, not step 5. Relaxing at the hearth while tired is priced like resting in place, because a tired crawler at the fire otherwise never went to bed. The sleeper makes the last step onto the bunk itself: walks end up to 1 unit short (`NavConstants.ArrivalTolerance`). A bunk that could not be taken is blacklisted for 10 s; without that, one crawler retried 405 times.
- **`BunkRestScenario` "starving"** means 95+ hunger for 300 ticks in a row. Hunger rises 1.5/s, so brief peaks at the cap (100) are routine and not starvation; the scenario logs the peak.
- **`DynamicChaosScenario`** drops its walls in the Hub–Bunkhouse door and the Ore Quarry–Hub door, at least 2 units from every pawn. The gate door is never that clear. The injector's own clearance check (physics colliders) does not see pawns.
- **Supply-loop bounds** (slowest of 3 × 1.5): sale 400, scout 550, gather 900. `BunkRestScenario` third sleeper: 2100.
- **Seeded bounty expiry** stayed 6000 (§7 #8): the chain finishes by t569.
- **Floor 1 doors moved** more than the candidate in §2.1: the quarry and grove doors went up beside the stall, the Hub–Bunkhouse and gate doors moved east (x 1..7), and the hearth sits at (−4.5, −3.5).

## 8. Carried findings
- Phase 2 §8 #2 (duplicate leadership events): **fixed** in step 2.
- Phase 2 §8 #3 (paths through hearths): **fixed** in step 1.
- Phase 2 §8 #4 (Pen 6 jitter): rechecked on Floor 1 in step 8. See open finding 1 below.
- Phase 2 §8 #1: open (§6).

**Open findings from Phase 3** (none fails a test):
1. **Jitter at the stall queue and the bunks.** Over 4,000 ticks the detector reports 6–8 `OscillationJitter` events across 6 crawlers, with 0 stacking. Nothing asserts on jitter on Floor 1 yet; Phase 5's zero-anomaly bar will.
2. **The starvation margin is thin.** The longest stretch at 95+ hunger in `BunkRestScenario` is 275 ticks, against the 300-tick bar. Hunger rises 1.5/s, and a crawler without gold must reach the Garden. This is economy pacing (§9), not navigation.
3. **About 1 in 3 bunk claims miss** (bunk taken by arrival). The crawler picks another bunk. Churn only.
4. **Cognition cost grows faster than linearly** with population (2× crawlers, 2.7× slower). Input for §3A #8.
5. **A succession re-confirmation still applies the peaceful-succession cohesion bonus** and memory events for the leader (step 2 fixed only the chronicle). That is behaviour, left for a decision.
6. Gather fulfilment does not check provenance (H6, accepted).

## 9. Economy soak on Floor 1: input for the §3.3 decision (2026-10-12)
`scripts/run-economy-soak.sh`: 30 sim-minutes (18,000 ticks) at 10×, with 3 seeds (20261009, 1, 2) × the provisional caravan export on and off. All 6 runs pass their integrity checks: the ledger is conserved and the horizon is reached. No economy values are asserted, and none were tuned: prices, stocks and starting gold are exactly as in Phase 2. Phase 2 measured the Commons yard with 6 crawlers; Phase 3 measures Floor 1 with 12. The CSVs are in `Logs/economy-soak-seed<seed>-export<on|off>.csv`.

| | Phase 2 yard, export off | **Floor 1, export off** | Phase 2 yard, export on | **Floor 1, export on** |
| :--- | :-: | :-: | :-: | :-: |
| Stall treasury, start → 30 min | 210 → 2–19g | **210 → 6–17g** (under 20g by minute 3) | 210 → 4,747–5,105g | **210 → 7,616–8,178g** |
| Rations sold out | minute 9–11 | **minute 6–9**, never restocked | never | **never** |
| Caravan restocks | 2–3 of 30 | **2–4 of 30** | 29 of 30 | **30 of 30** |
| Rations bought (purchases) | 78–85 | **72–99** | 280–303 | **458–494** |
| Salvage sold (sales) | 57–62 | **56–71** | 186–201 | **317–342** |
| Rations harvested from the Garden | not measured | **234–283** | not measured | **133–152** |
| Ore / herbs harvested | not measured | **32–42 / 27–34** | not measured | **165–189 / 153–156** |
| Crawler gold total | 60 → 230–250 | **120 → 255–320** | 60 → 215–265 | **120 → 360–430** |
| Mean hunger at 30 min | 36–55 | **55–71** | 35–42 | **41–52** |
| Bunk sleeps | (no bunks) | **52–73** | (no bunks) | **91–117** |
| Exhaustion collapses | not measured | **24–31** | not measured | **9–13** |
| Starving episodes (95+ hunger for 30 s) | not measured | **7–26** | not measured | **0** |

What the numbers say (observations, not decisions):
1. **Without the provisional export the economy still fails,** and faster on Floor 1. The stall can't afford salvage after about 3 minutes, so it stops buying and stops restocking. Rations sell out by minute 6–9. Crawlers fall back on the Garden (234–283 rations harvested, against 133–152 with the export), stop working salvage (ore and herbs fall from about 160 to about 35), go hungrier, collapse two to three times as often, and starve 7–26 times in 30 minutes.
2. **With the export, the treasury grows about 60% faster than on the yard** (about 7,600–8,200g against about 4,700–5,100g). Twice the crawlers means about 60% more purchases and sales. The export is a faucet with no matching sink, so this growth is the stopgap's side effect, not a working economy.
3. **Crawler wealth barely moves either way:** crawlers end with about 20–35g each after 30 minutes (from 10g). Gold flows through the stall and the export, not into the crawlers' pockets.
4. **Hunger dominates the crawlers' day.** Hunger rises 1.5/s (a meal about every 40 s), so food is most of what they do. With the export, 0 starvation; without it, food runs out.
5. **The Phase 2 conclusion stands, now on the real map:** §3.3 has to close the stall's money loop. A sink for the export gold, or a source of income for the stall that isn't a free faucet, is the decision the producer deferred. Floor 1 adds a time constraint: distance and 12 mouths make the stall's failure show within 3–6 minutes.

# Epic MAP: The Outpost Floor & Living Simulation Loop

- **Status**: COMPLETED (2026-10-13; Revision 2, approved by the producer 2026-10-11). Status is owned by [ROADMAP.md](../ROADMAP.md). Phases 1–5 done 2026-10-11 to 2026-10-13. The phase plans were removed from `.agents/`; read Phases 1–4 with `git show f679a0c:.agents/epic_map_phase<N>_plan.md` and Phase 5 with `git show 4e83ecd:.agents/epic_map_phase5_plan.md`.
- **Track**: Independent (cross-cutting world architecture)
- **Replaces**: the isolated 10-pen demo arena and the Outpost Commons yard as the **live** session. The pens are kept as test-only canaries.
- **Downstream beneficiaries**: Epic 9 (the economy loop runs on a real map), Epic 10 (hazards and descent need sectors), Epic UI-2/UI-3 (sector-aware UI), Track C (scenarios run on the shipped session).
- **Layers**: `Simpiens.Simulation.World` (new: map definition, sector registry), `Simpiens.Core.Session` (session definitions), `Simpiens.Entities` / `Simpiens.UI` (views only).

---

## 0. Producer intent (2026-10-11)

> "Sector numbers are unimportant to me. I simply want to see a full simulation loop physically, not play pens. Ideally play pens would be moved out of my immediate line of sight. They are legacy tests that are more like canaries."

This revision is built around that intent:
1. **The live game boots into one connected outpost floor.** Crawlers physically work, sell, eat, socialize and sleep there.
2. **The sector count is data, not design.** We start with a small layout and add sectors by editing data.
3. **The pens move into a test-only session.** They no longer appear in the live game, and they keep earning their place by asserting outcomes in PlayMode.

**Producer decisions (2026-10-11):**
- Revision 2 approved.
- Drifter arrival and departure is deferred (see Deferred).
- A **"Dev Map" button** switches from the main map to the pen arena (§2.4).
- **Shroud is fog of war** (2026-10-11, Phase 3 decision D1): "An adventurer should not know what is inside an uncharted room until a scout visits then successfully reports back."
- **The player's fog** (2026-10-12, Phase 4 decision D1 = A): inside an uncharted room the player sees the walls and the name; its contents are hidden under fog; crawlers walking in stay visible.
- **Controls over the map** (2026-10-12, Phase 4 decision D2): "Yes control over map would be nice." The header and the Bounty and Merchant windows work over the 2D view.

---

## 1. Review of Revision 1 (lead developer, 2026-10-11)

### What Revision 1 got right
- It identified the core problem: the pens break the agent lifecycle, and the stall can only be reached from one pen.
- It used grid-aligned rectangular geometry with wide doorways. That still suits the Grid A\* engine and its body-clearance rules.
- Its Phase 4 aimed at a lifecycle verified in PlayMode.

### Problems

| # | Problem | Why it matters |
| :-: | :--- | :--- |
| R1 | **Sectors were chosen to house legacy mechanics, not to serve the game loop.** The Directive Yard and Rally Standard exist to give Epic 8 a room; the Descent Gate is Epic 10 scope; the two "Transit Perimeters" have no function. | Rooms are cheap to draw and expensive to keep alive: each needs entities, behaviour and tests. Groups and directives should emerge wherever agents are, not in a drill square. |
| R2 | **Every sector is one door from the hub.** Shroud and scouting mean nothing when everything is adjacent and visible. | Scout bounties are half of the Milestone 1 bounty loop. Travel distance is what gives Gather bounties a price. |
| R3 | **The map is built from GameObjects, not data.** `OutpostMapBuilder` spawns about 300 wall `GameObject`s, each with a `Rigidbody2D`, two colliders and a `NodeController`. Walkability is re-rasterized from registered wall nodes inside `SpatialHashGrid`. | This violates the §1A.1 rule that authoritative state is plain data that exists without a scene. It cannot run headless and cannot be sent to a future client. It costs one GameObject per wall cell. |
| R4 | **Wall ids are not deterministic.** `OutpostMapBuilder` calls `GUID.Generate()`, while `SwarmSpawner` already uses `IEntityIdAllocator`. | This breaks the determinism guarantee from closure Phase 2 the moment the map is wired in. |
| R5 | **The geography has three sources of truth.** Coordinates are hard-coded separately in `SwarmSpawner` (`OutpostStallPosition`, node positions, agent spawns), `FacilityZoneService` (zone rects, ids 11–13) and `OutpostMapBuilder` (walls, doors). The scenarios also hard-code positions. | Every layout change touches all of them, and they can silently disagree. |
| R6 | **Sectors are a UI type, but the simulation depends on them.** `FacilityZone` lives in `Simpiens.UI.Zones`, yet `SimulationManager` (scout fulfilment) and the bounties (`targetSectorId`) depend on it. | Layering is inverted: the authoritative simulation reads a UI service. |
| R7 | **World bounds come from the camera.** `SimulationStarter` sizes the spatial partition from `Camera.main`, and `ScreenBoundsGenerator` adds viewport collider walls. | The simulation's extent depends on the display, so headless runs and a larger map get different worlds. |
| R8 | **Revision 1 had no plan for the pens or the existing scenarios.** It placed the map over the same coordinates as the pens and the Commons (y −14..14) and gave no migration for `OutpostSupplyLoopScenario`, `PriceResponseScenario`, `EconomySoakScenario` or the anomaly scenarios. | Wiring in the map would have broken most of PlayMode with no plan to fix it. |
| R9 | **The Definition of Done was vague.** "Agents navigate doors, claim bounties … without clumping" gives no bounded tick count and no concrete outcomes. | It does not meet ROADMAP §1B. |

### What we keep from Revision 1
- The grid-aligned, rectangular, wide-door geometry style.
- `OutpostMapBuilderValidator`'s connectivity and door checks. They are re-pointed at the data definition.
- The idea of a central hub.

The `OutpostMapBuilder` class itself is replaced. Its geometry is small and easy to re-express as data.

---

## 2. Design (Revision 2)

### 2.1 Principles
1. **One source of truth.** A plain C# `OutpostMapDefinition` describes bounds, walls, doors, sectors, points of interest (POIs), resource nodes and spawn points. The nav grid, sector registry, entity spawning, views and scenarios are all derived from it.
2. **The simulation owns the world; views render it.** Walls are written straight into `StaticNavGrid`. Walls are drawn by a view (tiles or batched sprites), never by one GameObject per cell.
3. **Distance and shroud carry meaning.** Near work sectors start explored. Frontier sectors start shrouded, sit behind the near sectors, and hold richer nodes. This gives Scout bounties a reason to exist and makes Gather bounties for frontier resources pay more.
4. **Every sector earns its place through the loop.** No rooms for features that aren't built (descent, workshops, guild drill). Those epics add their own sectors to the definition when they arrive.
5. **The layout is data.** Adding a sector means adding a definition entry plus its validator checks. No code paths are keyed to sector count or ids.

### 2.2 Starter layout (illustrative; exact geometry is settled in Phase 1)

```
              +--------------+--------------+--------------+
              |  FRONTIER W  |    GARDEN    |  FRONTIER E  |
              |  Deep Seam   |  food plots  |  Overgrowth  |
              |  rich ore    |              |  rich herbs  |
              |  (shrouded)  |              |  (shrouded)  |
              +------D-------+------D-------+-------D------+
              |              |     HUB      |              |
              |  ORE QUARRY  D  Stall+Board D  HERB GROVE  |
              |  iron veins  |  Hearth      |  herb patches|
              +--------------+------D-------+--------------+
                             |  BUNKHOUSE   |
                             |  rest bunks  |
                             +------D-------+
                             |  ROAD GATE   |
                             |  arrivals    |
                             +--------------+
```

| Sector | Starts | Loop role | Contents |
| :--- | :--- | :--- | :--- |
| Hub | explored | Commerce, contracts, social life | Trade stall + bounty board, common hearth |
| Bunkhouse | explored | Energy recovery | Rest bunks (rest anchor) |
| Garden | explored | Baseline food | Ration plots (regrowing) |
| Ore Quarry | explored | Near salvage | Iron veins (regrowing) |
| Herb Grove | explored | Near salvage | Herb patches (regrowing) |
| Frontier W (Deep Seam) | **shrouded** | Scout target, then rich ore | Higher-yield iron veins |
| Frontier E (Overgrowth) | **shrouded** | Scout target, then rich herbs | Higher-yield herb patches |
| Road Gate | explored | Spawn and entry point; future drifter arrivals | Spawn points |

Doorways are at least 3 cells wide (the agent body diameter plus clearance on both sides). Hub doors are 4 cells wide because hub traffic is heaviest. The world is about 48 × 36 units, which fits the current camera with modest pan and zoom.

### 2.3 The visible lifecycle (what the producer should see)

Each crawler cycles through the following by need and opportunity. All of these behaviours already exist; this epic gives them places.

**work** (harvest in a quarry or grove, or scout the frontier for a bounty) → **sell** at the stall → **buy and eat** (stall rations or garden) → **socialize** at the hearth (gossip, bonding) → **sleep** in the bunkhouse → repeat.

The one new mechanic is a **rest anchor**. Today `RestAction` recovers energy wherever the agent stands, which is invisible. A bunk anchor gives a recovery bonus, so tired agents walk to the bunkhouse and the loop shows on screen. This is a small GOAP change: `TravelToAnchor(Bunk) → Rest`, priced by `Considerations`. Resting anywhere stays possible as the collapse fallback.

### 2.4 Pens become a test-only canary session
- `ISessionDefinition` gains two implementations:
  - `OutpostSession` (production, `Core/Session`);
  - `CanaryPenSession` (the `Simpiens.Testing` assembly, `UNITY_INCLUDE_TESTS` only).
- `SwarmSpawner` becomes a generic "spawn this session" service. The pen layout, the 24 scripted agents and the seeded relationships and groups move into `CanaryPenSession`.
- **The live game boots into the main map, never the pens.** A **"Dev Map" toggle button** in the dashboard tears the session down and respawns it as the pen arena; pressing it again returns to the main map. Switching resets the simulation (same seed), so nothing carries across.
- The button and the pen session are installed by the `Simpiens.Testing` harness through `BootstrapExtensions`. They are present in Editor play and test builds, and absent from shipping player builds. If the producer later wants the Dev Map in development player builds, the harness gets a `DEVELOPMENT_BUILD` install path; that is a small follow-up, not a redesign.
- A new **`PenCanaryScenario`** asserts each pen's intended outcome within a bounded tick count (fear yield, respect deferral, contested-bush rivalry, altruistic sharing, hearth emergence, succession, schism). Today the pens are only visual demos with no PlayMode assertions; this is what turns them into real canaries.

---

## 3. Work Phases

Each phase ends in a commit with an honest PlayMode status, per the AGENTS.md commit discipline.

### Phase 1: The world as data
- **`OutpostMapDefinition`** (`Simulation/World`, plain C#, no `UnityEngine.Object`): bounds, wall rects, door rects, `SectorDefinition`s (id, name, rect, starts explored), POIs (stall, hearth, bunks), resource nodes (item, yield, regrow ticks) and spawn points. Floor 1 is authored in code as `Floor1Layout`. A ScriptableObject can come later and is not needed now.
- **`WorldBuilder`**: definition → `StaticNavGrid` walls (written once and versioned, not re-rasterized from wall nodes) and the spatial partition sized from the definition's bounds. All ids come from `IEntityIdAllocator`.
- **`ISectorRegistry`** (`Simulation/World`): the authoritative sector lookup, point-to-sector mapping and explored state. Ownership moves out of `FacilityZoneService`, which becomes a read-only UI adapter over the registry, or is folded into it.
- **Camera decoupling**: `SimulationStarter` stops sizing the world from the camera. `ScreenBoundsGenerator` viewport walls are removed; the map's perimeter is the world edge.
- **`MapDefinitionValidator`** (it replaces `OutpostMapBuilderValidator`) checks:
  - every sector is reachable from the Hub through doors wide enough for an agent body;
  - every POI, node and spawn point sits on a walkable cell in the Hub's region;
  - no two sectors overlap;
  - every walkable cell belongs to exactly one sector;
  - the same seed gives the same ids.

### Phase 2: Session split; pens out of the live game
- `ISessionDefinition`, `OutpostSession` and `CanaryPenSession`, with `SwarmSpawner` made generic (§2.4).
- **Dev Map toggle:** a dashboard button that respawns the running game as the pen arena and back (§2.4). This needs re-initialisable world bounds, which Phase 1 provides.
- Live boot is `OutpostSession` only. The default test harness session is also `OutpostSession`. Scenarios opt into the pens explicitly.
- Add `PenCanaryScenario`. `DynamicChaosScenario` keeps running on the pens only if it truly needs them; otherwise it is retargeted to outpost coordinates taken from the definition.
- Exit criteria:
  - nothing in production code references pen coordinates or the pen agent indices (`ScriptedPenAgentCount`);
  - `PenCanaryScenario` passes.

### Phase 3: Populate the outpost and its physical lifecycle
- `OutpostSession` spawns everything from `Floor1Layout`: stall and board, hearth, bunks, garden, near and frontier nodes, and crawlers at spawn points.
- **Population: 12 crawlers to start** (6 today), with seeded personalities. Measure ticks/sec at 12, 16 and 24. Scaling work (§3A #8, the locked `GoapPlanner`) is *not* in scope; the measurement tells us when it becomes urgent.
- Rest anchor and bunk preference (§2.3).
- Seeded bounties: Scout a frontier sector, and Gather frontier ore once it is scouted.
- **Scenario migration**: `OutpostSupplyLoopScenario`, `PriceResponseScenario`, `EconomySoakScenario` and the three anomaly scenarios read positions, sector ids and stall ids from the definition. No hard-coded coordinates remain in `Tests/`.
- Re-run the economy observation soak on the map. Travel distances change the economy's pace, and **these numbers are the input the producer needs for the §3.3 economy decision**.

### Phase 4: The player's view
- Wall and floor rendering from the definition (tilemap or batched sprites). Sector labels.
- **Fog-of-war shroud overlay** on unexplored sectors, lifted when a Scout bounty is fulfilled or a sector is explored.
- Camera that frames the floor, with pan, zoom and a "follow agent" option on click.
- The Facility Dashboard sector list and the Bounty modal's sector picker read from `ISectorRegistry`, so the Scout bounty dropdown lists real, still-shrouded sectors.

### Phase 5: Definition of Done
- **`OutpostLifecycleScenario`**: real `OutpostSession`, seeded, **no injection**, within **9,000 ticks (15 sim-minutes)**. The run passes when:
  - every crawler has completed at least one *harvest → sell* cycle;
  - at least 3 crawlers have rested at a bunk;
  - at least 3 have eaten (bought or harvested food);
  - at least one hearth gathering has happened (2 or more agents within the anchor radius together);
  - the seeded Scout bounty is fulfilled and its frontier sector is unshrouded;
  - the seeded Gather bounty is fulfilled and paid from escrow;
  - player trust has risen for at least one crawler (**reported, not asserted:** the producer moved it to the economy model, §3.3, on 2026-10-13; see [Epic 9 §8.3](epic-9.md));
  - the visual anomaly detector reports zero anomalies;
  - the ledger is conserved;
  - no crawler starves.
- **Determinism check**: two runs with the same seed produce an identical world-state hash at tick 3,000. This holds only as far as the known cognition-arrival deviation (§1A.2 #6) allows; if it doesn't, document the tolerance. (Result: identical at every 100-tick sample to t3,000, so no tolerance was needed.)
- Docs: ROADMAP, this epic's checklist, `curentfocus.txt` and `architecture_history.md` (the map-as-data decision and the session split).

---

## 4. Acceptance checklist (delivered work only; filled in as phases land)

- [x] Phase 1: world as data (done 2026-10-11; PlayMode 42/42 in 3 consecutive runs; `OutpostSupplyLoopScenario` passes with 0 anomalies):
  - `MapDefinition` + builder, `WallRasterizer`, `StaticNavGrid` base layer and rect bounds;
  - `ISectorRegistry`, `IWorldMap.Load`, reconfigurable partition with stale-snapshot guard;
  - `DevMapLayout` (live; cell-for-cell parity with the legacy pens) and `Floor1Layout` (validated, not live);
  - walls are data, drawn by `MapView`; bounds independent of the camera;
  - geometric wall-penetration check;
  - `OutpostMapBuilder` retired.
- [x] Phase 2: session split, pens test-only (done 2026-10-11; PlayMode 43/43; `OutpostSupplyLoopScenario` passes on the pen-free live map with 0 anomalies):
  - `ISessionDefinition`, `SessionBuilder`, `ISessionSwitcher`; `SwarmSpawner` builds any session, and the default is chosen by id;
  - live boot is `OutpostSession` on `OutpostCommonsLayout` (the Commons yard; Floor 1 follows in Phase 3);
  - `CanaryPenSession` + pens-only `DevMapLayout` in `Simpiens.Testing`; production code holds no pen coordinates, keys, ids or agent indices;
  - respawn rewinds the clock and clears the chronicle, so seeded expiries are session-relative and results no longer depend on uptime or test order;
  - Dev Map toggle: a session switch on the dashboard (both views), shown only when the harness registers the Dev Map; `SessionSwitchScenario` presses it and proves a clean round trip;
  - `PenCanaryScenario` asserts all ten pen outcomes within 600 ticks;
  - fix found by the canary: hearths and stalls block only their centre cell (Pen 4's altruism was sealed off since Grid A*);
  - every scenario names its session (`ISimulationScenario.SessionId`); `DevMapParityValidator` retired.
- [x] Phase 3: the outpost floor goes live (done 2026-10-12; PlayMode 47/47 in 3 consecutive runs; producer visual check passed 2026-10-12):
  - `OutpostSession` on `Floor1Layout`: 12 crawlers walk in from the Road Gate; the hub stall; seeded Scout and Gather bounties both on the Deep Seam; crawlers arrive fed (hunger 20);
  - Floor 1 made ready for live use: the hearth moved off the door lanes (doors re-placed), 8 bunks, 12 spawn points, named doors; a validator keeps the hearth ring clear of every door, lane and the stall;
  - the Commons yard retired (`CommonsYardFixture`, test-only);
  - **fog of war** (D1): landmarks in uncharted sectors are not perceived; the scout's survey is private until its successful report, which charts the sector and publishes the survey to every crawler; nobody explores into, or claims a Gather bounty in, an uncharted sector;
  - **the rest anchor**: bunks (one sleeper each, claimed on the main thread), `TravelToBunk -> SleepAtBunk`, priced in `Considerations`; resting in place stays the fallback;
  - map-aware searching: exploration and starvation panic head for the charted sector visited least recently; agents also perceive while they walk;
  - `BunkRestScenario` (live, no injection); `OutpostSupplyLoopScenario` asserts no fog leak and scout-before-gather; navigation and dynamic-chaos scenarios run on Floor 1;
  - `PopulationScalingProbe`: 24 crawlers run at 54x real time unthrottled (no escalation);
  - economy soak re-run on Floor 1 ([Epic 9 §8.2](epic-9.md));
  - fixes found on the way: claimants no longer sell ore owed to a bounty; an affordable stall ration counts as known food (no panic at the stall); the scout surveys from the middle of the room; path smoothing keeps clear of hearths and stalls; leadership is chronicled only when it changes.
- [x] Phase 4: the player's view (done 2026-10-12; PlayMode 52/52 in 3 consecutive runs; producer visual check passed 2026-10-12, including the mouse and keyboard controls):
  - the map on a tilemap (floor per room, rock, walls) aligned with each map's wall cells; `MapViewValidator` checks it cell for cell against the definition and the nav grid; 14 GameObjects for Floor 1 instead of about 270; one `RenderOrder` table;
  - **fog of war on the map** (`ShroudView`, D1 = A): opaque over an uncharted room's interior with "UNCHARTED" under its name, props below it, crawlers above; it fades over 1.5 s when the room is charted; `ShroudViewScenario` (live);
  - camera (`CameraRig` + `CameraController`): pan (drag, WASD), zoom toward the cursor, Home to reframe, clamped to the map; `CameraRigValidator` (headless);
  - selection (`WorldSelection`, `WorldPicker`): click a crawler to follow it (ring and name), click a room to select it; mirrored in the dashboard;
  - the header and the Bounty and Merchant windows in a HUD layer over the map (D2); `MapHudScenario` (live);
  - the bounty board's sector picker lists rooms by name (Scout: uncharted only); `BountyRules` refuses impossible targets in the simulation before escrow; `PlayerScoutScenario` (live): the player posts Scout Overgrowth from the map and the fog lifts;
  - fixes found on the way: bunks were shoved around by crawlers (dynamic physics body); the Bounties and Merchant buttons needed two presses the first time;
  - crawlers lie down in their bunk while they sleep (producer request after the Phase 3 check).
- [x] Phase 5: Definition of Done (done 2026-10-13; PlayMode 54/54 in 3 consecutive runs; producer visual check at 1× passed 2026-10-15):
  - `AgentActivity`: a per-crawler tally of harvests, sales, Gather deliveries, ration purchases, meals, bunk sleeps, hearth gatherings and harvest → sell cycles, recorded where each act is applied; diagnostic only;
  - the jitter check measures sim time (producer decision D1 = A): 3 reversals of more than 120° within 5 ticks, evaluated once per tick from logical positions; thresholds unchanged; jitter fell to 0 in every scenario;
  - **`OutpostLifecycleScenario`** (live, no injection, 9,000 ticks): every crawler closes a harvest → sell cycle, 3+ sleep in a bunk, 3+ eat, the hearth gathers, both seeded bounties are fulfilled (scout first, no fog leak, Gather paid from escrow), 0 anomalies of every type, the ledger balances, nobody starves; a per-crawler report; trust toward the player reported only (§3.3);
  - **`DeterminismScenario`** with `WorldStateHash` (producer decision D2 = A): same-seed runs identical at every 100-tick sample to t3,000;
  - `BunkRestScenario` reports starvation instead of asserting it (producer decision 2026-10-13: food pacing belongs to §3.3; §6 #2);
  - fixes found on the way: a hungry crawler eats the ration it carries (`EatCarriedFoodIntent`; the starvation reflex counts a carried ration as food); an intent decided before a respawn is dropped instead of moving the new agent with the same id (`AgentIntent.SessionEpoch`).

### Deferred (owned elsewhere)
- **Drifter arrival and departure** through the Road Gate (population churn brings gold in and out): this is an economy decision, owned by §3.3 / Epic 9.
- ~~**Trust toward the player as a pass criterion** of `OutpostLifecycleScenario`: it depends on prices and the gouging rule, so it waits on §3.3 / Epic 9 ([Epic 9 §8.4](epic-9.md) #5).~~ **Delivered by §3.3 (2026-10-15):** asserted again (trust rises for ⅔ of crawlers).
- ~~**"No crawler starves" as a pass criterion of `BunkRestScenario`**: a broke crawler can starve while the Garden regrows; food pacing waits on §3.3 / Epic 9 ([Epic 9 §8.4](epic-9.md) #6). `OutpostLifecycleScenario` keeps the assertion.~~ **Delivered by §3.3 (2026-10-15):** asserted again.
- **Descent gate sector and hazards**: Epic 10.
- **Workshop sectors**: Milestone 3.
- ~~**Map serialization and save/load**~~: delivered 2026-10-15 with Milestone 1's whole-session save. The map itself isn't saved: a save names it by fingerprint, and a load rebuilds it from the session's definition.
- **Cognition scaling for larger populations**: §3A #8.

---

## 5. Risks

| Risk | Mitigation |
| :--- | :--- |
| Longer travel starves agents between the frontier and the stall | Garden in the hub's neighbourhood; the soak and lifecycle scenario assert "no crawler starves"; tune with regrow and yield data in the definition, not with code |
| Crowding at hub doors (pawn stacking) | 4-cell hub doors; anomaly scenarios now run on the real map; the existing body-clearance navigation |
| Scenario migration breaks green PlayMode | Phase 2 keeps the old session reachable until Phase 3 migrates each scenario; one scenario per commit |
| A data-driven map invites layout bikeshedding | The layout changes only to fix a failing lifecycle assertion or validator, or when a new epic needs a sector |
| Pens rot once out of sight | `PenCanaryScenario` in the regular PlayMode suite is the guard |
| 12+ agents expose the locked `GoapPlanner` | Measured in Phase 3; escalated as §3A #8 with numbers, not guessed |

---

## 6. Open findings

Found by the live scenarios during Phases 2–5. None fails a test. The epic is complete, so each open item names the owner it was handed to.

1. ~~**Jitter at the stall queue and the bunks** (Phase 3).~~ **Resolved in Phase 5:** measured in sim time (D1 = A), jitter is 0 in every scenario. At the old 10× window (50 ticks) the remaining events were small single steps (about 0.3 units) at the stall and the bunks, plus a few 3–5 unit walk-and-return turns near the bunks and the Garden door: fidgeting and indecision, not vibration. Left for the producer's eye at 1×; **the producer's visual check at 1× passed 2026-10-15.**
2. **The starvation margin is thin** (Phase 3). **Partly fixed in Phase 5:** one cause was a crawler panicking with a ration in its pocket; it now eats it. `OutpostLifecycleScenario`: 0 starving over 9,000 ticks in 3 of 3 runs, worst stretch 233 ticks at 95+ hunger. **Still open:** in the close-out runs `BunkRestScenario` starved one crawler in 3 of 6 close-out runs (t3117, 445 ticks at 95+): with 10g it can't buy a 15g ration, every Garden plot it knows is empty and blacklisted, so starvation panic sends it exploring away from the Garden while the plots regrow. The producer handed it to §3.3 as food pacing (2026-10-13); `BunkRestScenario` now reports starvation instead of asserting it. Owner: §3.3 / Epic 9 ([Epic 9 §8.4](epic-9.md) #6). **Resolved 2026-10-15 by §3.3 (D6):**
   - crawlers arrive with 15g;
   - the Garden regrows faster and yields more;
   - caravan restocks are partial;
   - a starving crawler that has seen the Garden waits at its plots instead of exploring away.
   
   `BunkRestScenario` asserts no starvation again (3/3).
3. **About 1 in 3 bunk claims miss** because the bunk is taken by arrival (Phase 3). The crawler picks another bunk. Churn only. Owner: none scheduled (fix if it becomes visible).
4. **Cognition cost grows faster than linearly** with population: 2× crawlers, 2.7× slower (Phase 3, `PopulationScalingProbe`). Owner: ROADMAP §3A #8.
5. **A succession re-confirmation still applies the peaceful-succession cohesion bonus** and memory events for the leader (Phase 3; only the chronicle was fixed). Behaviour, left for a decision. Owner: Epic 8.
6. **Gather fulfilment does not check provenance** (H6, accepted): ore bought or found counts the same as ore mined.
7. **Pen 7 hands leadership to the defiant follower at t50** (Phase 2, Dev Map only). Its only follower has negative respect for the leader, so the succession check judges the leader discredited and transfers leadership peacefully to the defiant agent. The defiance outcome happens first (t24), so the canary passes, but afterwards the pen shows a leader change it doesn't intend. Fix options: raise the follower count in the pen, or exempt single-follower groups from the discredit rule.
8. **Trust toward the player falls on the default prices** (Phase 5): 10 of 12 crawlers end at −80 to −100. Owner: §3.3 / Epic 9 ([Epic 9 §8.3](epic-9.md)). **Resolved 2026-10-15 by §3.3 (D3):** each crawler judges a trade by its own valuation; trust rises for 10 of 12 at the default prices.
9. ~~**The dossier's *Feed ration* button writes a crawler's hunger directly from the UI**~~ **Resolved 2026-10-15:** the button is removed (producer decision D6 a, Milestone 1 completion plan).
10. **The dossier's *Calm Frustration* button writes a crawler's frustration directly from the UI** (`FacilityDashboardPresenter.HandleCalmTantrum`), the same breach of §1A.1 #7 as *Feed ration* (found 2026-10-15). No scenario presses it. It should become a command or be removed (a producer decision). Owner: Track B.

---

## History
- **2026-10-08: Revision 1 paused.** It was partially started (`OutpostMapBuilder` with 10 doors and `OutpostMapBuilderValidator`), then paused for the architecture review. It was never wired into the live session. Its 9-sector versus 12-sector target is superseded: the producer considers sector count unimportant (2026-10-11).
- **2026-10-11: Revision 2 proposed** (this document). It reframes the epic around the live loop, map-as-data and the pens as a test-only canary session.
- **2026-10-11: Revision 2 approved; epic `ACTIVE`.** Producer added the Dev Map toggle and deferred drifter churn.
- **2026-10-11: Phase 1 done** (world as data).
- **2026-10-11: Phase 2 done** (session split). The live game no longer shows the pens. Open findings from the new pen canary are in §6.
- **2026-10-11: Phase 3 plan approved** with decision D1: shroud is fog of war.
- **2026-10-12: Phase 3 done** (the outpost floor goes live). The live game runs on Floor 1 with fog of war and bunks. Open findings are in §6; the Floor 1 economy soak is in [Epic 9 §8.2](epic-9.md).
- **2026-10-12: Phase 3 visual check passed; Phase 4 plan approved** with D1 = A (crawlers visible in the fog) and D2 = yes (HUD over the map).
- **2026-10-12: Phase 4 done** (the player's view). The player plays from the map. Its deviations are recorded in the Phase 4 plan (git history, `f679a0c`).
- **2026-10-12: Phase 4 visual check passed** (including the mouse and keyboard controls); **Phase 5 plan approved** with D1 = A (jitter measured in sim time) and D2 = A (assert the strongest determinism that holds, record the tolerance). Completed phase plans removed from `.agents/`; open findings moved to §6.
- **2026-10-13: Phase 5 done; epic `COMPLETED`.** The live outpost runs its whole day with nothing injected and zero anomalies, and two same-seed runs tell the same story. The producer folded every economic decision into §3.3, including trust toward the player, which the lifecycle scenario now reports instead of asserting, and the bunk scenario's intermittent starvation (food pacing), which it now reports instead of asserting.
- **2026-10-15: Phase 5 visual check passed** at 1×, after the §3.3 economy model was merged.

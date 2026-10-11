# Epic MAP: The Outpost Floor & Living Simulation Loop

- **Status**: ACTIVE (Revision 2, approved by the producer 2026-10-11). Status is owned by [ROADMAP.md](../ROADMAP.md). Phase 1 done 2026-10-11 ([implementation plan](../../.agents/epic_map_phase1_plan.md)); next: Phase 2.
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
  - player trust has risen for at least one crawler;
  - the visual anomaly detector reports zero anomalies;
  - the ledger is conserved;
  - no crawler starves.
- **Determinism check**: two runs with the same seed produce an identical world-state hash at tick 3,000. This holds only as far as the known cognition-arrival deviation (§1A.2 #6) allows; if it doesn't, document the tolerance.
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
- [ ] Phase 2: session split, pens test-only, Dev Map toggle, `PenCanaryScenario`
- [ ] Phase 3: outpost populated, scenarios migrated, soak re-run
- [ ] Phase 4: map rendering, shroud, camera, sector-aware UI
- [ ] Phase 5: `OutpostLifecycleScenario` passes; docs reconciled

### Deferred (owned elsewhere)
- **Drifter arrival and departure** through the Road Gate (population churn brings gold in and out): this is an economy decision, owned by §3.3 / Epic 9.
- **Descent gate sector and hazards**: Epic 10.
- **Workshop sectors**: Milestone 3.
- **Map serialization and save/load**: the MemoryPack slice. The definition is plain data, so it is ready for that.
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

## History
- **2026-10-08: Revision 1 paused.** It was partially started (`OutpostMapBuilder` with 10 doors and `OutpostMapBuilderValidator`), then paused for the architecture review. It was never wired into the live session. Its 9-sector versus 12-sector target is superseded: the producer considers sector count unimportant (2026-10-11).
- **2026-10-11: Revision 2 proposed** (this document). It reframes the epic around the live loop, map-as-data and the pens as a test-only canary session.
- **2026-10-11: Revision 2 approved; epic `ACTIVE`.** Producer added the Dev Map toggle and deferred drifter churn.

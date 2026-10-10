# Epic MAP: The Utilitarian Outpost Map & Agent Lifecycle Topology

- **Status**: PAUSED (Independent Foundation). Status is owned by [ROADMAP.md](../ROADMAP.md).
- **Track**: Independent (Cross-Cutting World Architecture)
- **Preceding Epic**: None (Replaces isolated test playpens from Epics 7 & 8)
- **Downstream Beneficiaries**: Track A (Epic 9 Outpost Economy, Epic 10 Descent), Track B (Epic UI-1 Facility Dashboard, Epic UI-2 Merchant Terminal), Track C (PlayMode Scenarios)
- **Architectural Layer**: World Architecture (`Simpiens.Environment`), Spatial Partitioning (`Simpiens.Simulation.Spatial`), Sector Topology (`Simpiens.UI.Zones`)

---

> [!NOTE]
> **Pause Record (2026-10-08).** This epic was partially started, then paused to prioritise an architecture review and documentation reconciliation.
>
> **Exists (Phase 1, partial):** `OutpostMapBuilder` / `IOutpostMapBuilder` (perimeter and interior walls, 10 canonical doorway positions) and `OutpostMapBuilderValidator`, registered in `AppBootstrapper`.
>
> **Not done:** `BuildMap()` is not called by the live session (`SwarmSpawner` still builds the isolated play-pen arena); `SimulationStarter` still initialises the 44×36 fallback bounds instead of 40×32; `FacilityZoneService` still defines legacy play-pen zones (Phase 2); entity/resource population (Phase 3) and lifecycle scenarios (Phase 4) have not started.
>
> **Outpost Sector Scope Note (2026-10-08):** The game design specifies 12 interconnected sectors for the complete outpost. This epic's 9-sector topology represents incomplete work-in-progress (WIP) that established the initial core foundation before being paused. It does not supersede the 12-sector target, but represents the current partial state of map construction.
>
> **Dependency note:** Phases 3–4 depend on real navigation (Grid A*), which is deferred from Epic 9; straight-line pathing cannot traverse doorways between sectors.

---

## 1. Executive Summary & Problem Statement

During earlier milestones (Epics 7 & 8), our simulation environment relied on **10 isolated, walled playpens** (two rows of five $4 \times 7$ pens). While effective for verifying isolated behavioral dyads in headless tests (e.g. fear yielding, altruistic food sharing), this playpen topology fundamentally fractures the continuous agent lifecycle:

1. **Physical Isolation**: Solid perimeter and divider walls prevent agents from navigating between sectors.
2. **Artificial Economic Placement**: The Merchant Trade Stall from Epic 9 was artificially placed at `(0, 0)` inside Pen 3 (the Rivalry Contest pen). Agents in other pens have no physical path to reach the stall.
3. **Broken Lifecycle Feedback Loops**: Agents cannot take a bounty from the Central Hub, travel through corridors to an extraction sector (Quarry or Wilderness), harvest nodes, return to the Trade Stall, sell salvage, buy food rations, and rest by the campfire.
4. **Disconnected UI Presentation**: The UI Facility Dashboard and Bounty Board track sectors that have no real physical connection in the world.

### The Epic MAP Solution
Replace the isolated 10-pen test arena with a **unified, utilitarian 9-sector outpost map** featuring:
- **Central Outpost Hub**: Houses the Merchant Trade Stall, public Bounty Board, and Common Hearth Campfire ($r=3.5\text{m}$).
- **Surrounding Functional Zones**: Dedicated sectors for sleeping/energy recovery (Bunkhouse), food foraging (Berry Orchard), group directives and succession (Directive Yard), iron ore mining (Deep Quarry), medicinal herbs and scouting (Herbal Conservatory), and dungeon descent staging (Forward Descent Gate).
- **Dual-Ring Doorway Topology**: 4 wide cardinal doors ($2.5\text{m}$ to $3.0\text{m}$) connecting the Central Hub to cardinal wings, plus 6 outer perimeter bypass corridors allowing agents to transit between work and sleep sectors without causing congestion at the market stall.
- **Pure Utilitarian Geometry**: Strictly rectangular rooms and orthogonal corridors aligned to $1.0\text{m}$ grid units, ensuring zero raycast snags, deterministic line-of-sight checks (`HasLineOfSight`), and zero-allocation Grid A* pathfinding.

---

## 2. Visual & ASCII Outpost Topology (36 x 28 World Units)

```
========================================================================================================
                                      SIMPIENS OUTPOST MAP TOPOLOGY
                                  World Bounds: [-18..+18] x [-14..+14]
========================================================================================================

 [-18, 14]                                      [0, 14]                                       [18, 14]
  #==================================#============================#==================================#
  #  SECTOR 2: BUNKHOUSE             #  SECTOR 3: FORAGING        #  SECTOR 4: DIRECTIVE YARD        #
  #  (Quiet Quarters / Sleep)        #  ORCHARD (Hydroponics)     #  (Guild Drill / Succession)      #
  #  Bounds: [-18..-7] x [5..14]     #  Bounds: [-6..+6] x [5..14]#  Bounds: [+7..+18] x [5..14]     #
  #                                  #                            #                                  #
  #       [===] Rest Bunk            #    (*) Berry Bush 1        #          [|] Rally Standard      #
  #       Pos: (-12.5, 9.5)          #    Pos: (-3.0, 9.5)        #          Pos: (12.5, 9.5)        #
  #                                  #                            #                                  #
  #       [===] Rest Bunk            #    (*) Berry Bush 2        #          (Drill Square /         #
  #       Pos: (-12.5, 7.0)          #    Pos: (0.0, 11.5)        #           Succession Arena)      #
  #                                  #                            #                                  #
  #                                  #    (*) Berry Bush 3        #                                  #
  #                                  #    Pos: (3.0, 9.5)         #                                  #
  #===#      #=======================#====#                  #====#=======================#      #===#
      #  D   #                            #   DOOR NORTH     #                            #  D   #
      #  O   #                            #   [-1.5 .. +1.5] #                            #  O   #
      #  O   #                            #   Pos: (0, 4.5)  #                            #  O   #
      #  R   #                            #                  #                            #  R   #
  #===#      #=======================#====#                  #====#=======================#      #===#
  #  W1: (-12, 4.5)                  #                            #                  E1: (12, 4.5)   #
  #                                  #  SECTOR 1: CENTRAL HUB     #                                  #
  #  SECTOR 5: DEEP QUARRY           #  (Marketplace & Hearth)    #  SECTOR 6: HERBAL CONSERVATORY   #
  #  (Iron Extraction & Salvage)     #  Bounds: [-6..+6] x [-4..4]#  (Medicinal Herbs / Salvage)     #
  #  Bounds: [-18..-7] x [-4..4]     #                            #  Bounds: [+7..+18] x [-4..4]     #
  #                                  #     [$$$] TRADE STALL      #                                  #
  #     (O) Iron Vein 1              #     Pos: (0.0, 2.0)        #     (&) Herb Patch 1             #
  #     Pos: (-14.0, 1.5)       D    #     Merchant & Bounties    # D   Pos: (14.0, 1.5)             #
  #                             O    #                            # O                                #
  #     (O) Iron Vein 2         O W  #                            # O E                              #
  #     Pos: (-10.5, -1.5)      R    #      (o) COMMON HEARTH     # R   (&) Herb Patch 2             #
  #                             :    #      Pos: (0.0, -2.0)      # :   Pos: (10.5, -1.5)            #
  #     (O) Iron Vein 3         (-6,0)      Campfire Anchor       # (6,0)                            #
  #     Pos: (-15.0, -2.5)           #      Social Consensus      #     (&) Herb Patch 3             #
  #                                  #      Relaxation (r=3.5m)   #     Pos: (15.0, -2.5)            #
  #===#      #=======================#====#                  #====#=======================#      #===#
  #  W2: (-12, -4.5)                 #    #   DOOR SOUTH     #    #                  E2: (12, -4.5)  #
      #  D   #                            #   [-1.5 .. +1.5] #                            #  D   #
      #  O   #                            #   Pos: (0, -4.5) #                            #  O   #
      #  O   #                            #                  #                            #  O   #
      #  R   #                            #                  #                            #  R   #
  #===#      #=======================#====#                  #====#=======================#      #===#
  #                                  #                            #                                  #
  #  SECTOR 8: WEST PERIMETER        #  SECTOR 7: DESCENT GATE    #  SECTOR 9: EAST PERIMETER        #
  #  (Transit & Security Post)       #  (Dungeon Airlock/Staging) #  (Workshop Reserve & Transit)    #
  #  Bounds: [-18..-7] x [-14..-5]   #  Bounds: [-6..+6] x[-14..-5#  Bounds: [+7..+18] x [-14..-5]   #
  #                                  #                            #                                  #
  #       [>] Outer West Gate        #     [===] DESCENT GATE     #       [>] Outer East Gate        #
  #       Pos: (-12.5, -9.5)         #     Pos: (0.0, -11.0)      #       Pos: (12.5, -9.5)          #
  #                                  #     Boss Barrier Threshold #                                  #
  #                                  #     Forward Expedition     #       (Future Artisan            #
  #             DOOR S-W             #                            #        Workshops - Phase 3)      #
  #             [-6.5, -9.5]         #                            #        DOOR S-E                  #
  #========#                    #====#                            #====#                    #========#
           #                    #                                      #                    #
 [-18, -14]#====================#======================================#====================# [18, -14]
```

---

## 3. Sector Topology & Functional Roles

| Sector ID | Sector Name | Bounds $[X_{\min}, Y_{\min}, W, H]$ | Anchor / Primary Entities | Primary Functional Role in Simulation |
| :---: | :--- | :--- | :--- | :--- |
| **Zone 1** | **Central Outpost Hub** | `[-6, -4, 12, 8]` | Trade Stall `(0, 2)`, Campfire `(0, -2)` | Commerce, contracts, communal hearth gossip, and social bonding. High-traffic crossroads. |
| **Zone 2** | **Bunkhouse / Quarters** | `[-18, 5, 11, 9]` | Rest Bunks `(-12.5, 8.5)` | Secluded sleeping wing for energy recovery ($\text{Energy} \le 30$) without foot-traffic disruption. |
| **Zone 3** | **Foraging Orchard** | `[-6, 5, 12, 9]` | 3x Berry Bushes `(-3, 9.5)`, `(0, 11.5)`, `(3, 9.5)` | Hydroponic food foraging, altruistic feeding, and baseline food harvesting. |
| **Zone 4** | **Directive Yard** | `[7, 5, 11, 9]` | Rally Standard `(12.5, 9.5)` | Open drill square for group directives, military compliance, and succession dominance trials. |
| **Zone 5** | **Deep Quarry** | `[-18, -4, 11, 8]` | 3x Iron Ore Veins `(-14, 1.5)`, `(-10.5, -1.5)`, `(-15, -2.5)` | Heavy salvage extraction. Target for `GatherResource` (Iron Ore) bounties. Resource rivalry. |
| **Zone 6** | **Herbal Conservatory** | `[7, -4, 11, 8]` | 3x Medicinal Herb Nodes `(14, 1.5)`, `(10.5, -1.5)`, `(15, -2.5)` | Botanical gathering. Target for `GatherResource` (Medicinal Herbs) and scouting contracts. |
| **Zone 7** | **Forward Descent Gate** | `[-6, -14, 12, 9]` | Descent Gate `(0, -11)` | Fortified airlock threshold leading to deeper megadungeon floors. Staging for party expeditions. |
| **Zone 8** | **West Transit Perimeter** | `[-18, -14, 11, 9]`| Patrol Post `(-12.5, -9.5)` | Auxiliary transit corridor connecting Quarry and Gate; outer defense perimeter. |
| **Zone 9** | **East Transit Perimeter** | `[7, -14, 11, 9]`| Workshop Reserve `(12.5, -9.5)` | Auxiliary corridor; staging area for Milestone 3 Artisan Workshops. |

---

## 4. Work Phasing & Incremental Slices

To deliver this world architecture safely without breaking existing test harnesses or regression testing, the work is divided into **4 discrete phases**:

### Phase 1: Physical Environment & Procedural Map Construction
- **Goal**: Implement `OutpostMapBuilder.cs` to construct the static utilitarian wall grid, 2.5m open doorways, camera view bounds, and spatial partition dimensions centered at `(0, 0)`.
- **Deliverables**:
  - `OutpostMapBuilder.cs` (Programmatic spawning of walls, door gaps, and entity anchors).
  - `ScreenBoundsGenerator.cs` updated to center orthographic camera at `(0, 0)` with size $14.5\text{f}$.
  - `SimulationStarter.cs` updated to initialize `ISpatialPartition` with $40 \times 32$ world bounds.
  - Verification of static physics colliders and unobstructed raycasts across doorways.

### Phase 2: Spatial Sector Mapping & UI Shroud Synchronization
- **Goal**: Refactor `FacilityZoneService.cs` to represent the 9 interconnected outpost sectors, replacing the legacy playpen zone IDs.
- **Deliverables**:
  - `FacilityZoneService.cs` updated with exact `Rect Bounds`, anchor positions, and default exploration flags.
  - `FacilityZoneServiceValidator.cs` updated to mathematically assert containment across all 9 new sectors.
  - UI Toolkit `FacilityDashboardPresenter` and `BountyTerminalPresenter` dynamically reflecting the new sector layout.

### Phase 3: Entity Spawning, Resource Nodes & Dynamic Daily Routines
- **Goal**: Connect `SwarmSpawner.cs` to populate the new outpost with renewable berry bushes, iron ore veins, medicinal herb patches, rest bunks, and trade anchors.
- **Deliverables**:
  - `SwarmSpawner.cs` refactored to spawn agents with varied initial routines (foraging, mining, resting, trading).
  - Relational dyads and group structures mapped to appropriate functional rooms (Directive Yard, Quarry, Hearth).
  - Outpost Trade Stall anchored at `(0, 2)` and Common Hearth at `(0, -2)`.

### Phase 4: Full Lifecycle Integration & PlayMode Scenario Verification
- **Goal**: Run automated scenarios ensuring agents navigate doors, claim bounties, mine ore, trade at the stall, and rest without path stalls or clumping.
- **Deliverables**:
  - `SwarmPlayModeTests` executing clean across the new outpost layout.
  - `VisualAnomalyDetector` asserting 0 pawn stacking, 0 watchdog stalls, and 0 state thrashing through door portals.

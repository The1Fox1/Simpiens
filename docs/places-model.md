# The Places Model: Zones, Buildings, Furniture and Stations

The rules and measurements of the outpost's places ([Epic PLACES](epics/epic-places.md)), kept up to date as each milestone lands, as [`life-model.md`](life-model.md) is for the life core and [`skills-model.md`](skills-model.md) for skills.

## 1. The map's walkable ground (measured 2026-11-25, Epic PLACES plan 1, P.1)

The baseline for every step that changes walls or furniture: `MapMeasureTests.MeasureTheMap` (explicit, on `MapMeasure` in `Simpiens.Testing.Core/World`), on Floor 1 at `main`'s `4134c53`. The walk is from the hub's target point to each zone's, along the static grid as A* walks it: eight ways, never cutting a corner, a diagonal √2.

**Floor 1:** 1,790 walkable cells in 15 zones (and the fallback). Its cells are one unit; a wall is written inflated by a pawn's radius (0.25), so a solid one unit wide blocks the cells either side of it as well.

| Zone | Walkable cells | Walk from the hub |
| :--- | ---: | ---: |
| Outpost Hub | 156 | 0.0 |
| Bunkhouse | 64 | 11.2 |
| The Tavern | 148 | 12.0 |
| Ore Quarry | 152 | 16.8 |
| Herb Grove | 152 | 16.8 |
| Road Gate | 64 | 17.2 |
| Workshop Row | 63 | 23.2 |
| Overgrowth | 144 | 24.5 |
| Deep Seam | 148 | 25.1 |
| Glow Caves | 144 | 25.3 |
| Garden | 144 | 25.7 |
| The Sump | 148 | 25.9 |
| The Old Stair | 144 | 30.7 |
| Guildhall | 59 | 33.5 |
| The Drowned Gallery | 60 | 34.9 |

**Doors** (walkable cell pairs across the shared edge): 4 each, the hub to the Bunkhouse, the Tavern, the Ore Quarry and the Herb Grove; the Bunkhouse to the Road Gate; the Garden to the Tavern; the Ore Quarry to the Deep Seam and the Sump; the Herb Grove to the Overgrowth and the Glow Caves; the Deep Seam to the Old Stair; the Road Gate to the Workshop Row; the Sump to the Drowned Gallery. The Workshop Row to the Guildhall is 3: the guildhall is six units deep, so its one door is narrower than the others.

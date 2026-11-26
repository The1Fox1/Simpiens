# The Places Model: Zones, Buildings, Furniture and Stations

The rules and measurements of the outpost's places ([Epic PLACES](epics/epic-places.md)), kept up to date as each milestone lands, as [`life-model.md`](life-model.md) is for the life core and [`skills-model.md`](skills-model.md) for skills.

## 1. The vocabulary (Epic PLACES plan 1, G.1)

What each word means in the code and in the player's words. "Zone" is the player's word for a sector (the epic's Q9); code keeps `Sector`.

| Word | Is | Is not | In the code |
| :--- | :--- | :--- | :--- |
| **Zone** | A walled region of the floor with a name, fog of war, maybe a hazard; where bounties point and the dashboard's floor list groups crawlers | A building; furniture | `SectorDefinition` |
| **Building** | A walled structure inside one zone: a footprint, doors, a name and sign, a floor and wall style, an interior. It hosts stations, furniture, decor and venues. Built by the player on a lot (plan 2), or standing from the start (the bunkhouse) | A zone (no fog of its own, no hazard, no bounties); a station; a business | `BuildingDefinition` (plan 1, B.1) |
| **Lot** | A building's place in a zone before it's built: its footprint, doors and size, drawn as foundations | A building | A `BuildingDefinition` of kind None (plan 1, B.2) |
| **Station** | A fixture where a crawler works: crafts (forge, still, cooking station, brew barrel), serves (a bar), or practises (a training dummy). It has a kind, a place and a reach, a build state, and a worker's slot and shifts | A building (no walls, no door, no name on the map); a venue (it offers no free time by itself); a business; furniture; a store of goods (it works from the stall's stock) | `StationDefinition`, `StationTable` (revised in plan 2: §2) |
| **Furniture** | An object crawlers use without working at it: tables, benches, chairs and stools (seats), bunks and beds, shelves | A station (no work, no worker); decor (it has a use) | `FurnitureTable`, placements (plan 1, F.1) |
| **Decor** | An object that only looks: lanterns and torches, banners, rugs, plants, trophies, a sign. It has a beauty | Furniture; a station | `DecorTable` (plan 3) |
| **Venue** | What a place offers for free time: its activities, seats, hours and who may come. It may belong to a building (the tavern hall's evening), to a zone (the Garden's walk, the hearth's fireside) or to a yard | A building (the Garden's walk needs no walls); a station | `VenueDefinition` |
| **Business** | A building whose venue and stations a staff keeps: the tavern is its hall, its bar, kitchen and barrel, its venue, its keeper and cook, its hours and menu | One station | Today `StationKind.Tavern` with its venue and keeper; a building kind from plan 2 |

## 2. What a station is today, and who reads its kind (G.1, the epic's F1)

**The table** (`StationTable`, `Simulation/Items/`): a row per `StationKind` (never reused: stations are saved by kind), with its word, title, description, whether an artisan works it, whether it has recipes, its cost line and its art key. Four kinds: **Forge** (1) and **Still** (2) are stations in the epic's sense, an artisan making to order. **DrillYard** (3) is a yard: no artisan, no recipes, practice for whoever drills there. **Tavern** (4) is a business: its row stands for the counter, the keeper's shift and the menu all at once. That conflation is the epic's F1; plan 2 (PL3) revises the table so a kind says what work it does (craft, serve, practise), who works it and which building kinds host it.

Every production reader of a station's kind or key, at plan 1's start, and what plan 2 does to it:

| Reader | What it reads | Plan 2 |
| :--- | :--- | :--- |
| `StationTable`, `StationKind` | The rows | Columns for the work (craft, serve, practise), who works it (an artisan's skill, a keeper, a member at drill) and the building kinds that host it; `Tavern` (4) becomes the bar, its id kept; the drill yard's row becomes the training dummy's, or retires for it (plan 2 decides) |
| `RecipeBook` | Recipes by station kind (the forge's, the still's) | The cooking station's and the brew barrel's recipes (stew, ale), the epic's Q4 |
| `Floor1Layout` | The four sites (forge, still, drill yard, tavern counter) | The bar, cooking station and brew barrel in the tavern hall; the dummies in the yard |
| `MapDefinitionBuilder` | Refuses a station of kind None; a venue's station key must exist | A station stands in a building or open-air in a zone, of a kind the building hosts |
| `VenueDefinition.Station`, `VenueRules.OpenMask`, `Wanting` | A venue opens while its station stands and, if an artisan works it, is kept | A building's venue opens while the building stands and its service station is staffed: the venue names its building |
| `WorkshopRules` | `TakesArtisan` (hiring), `StationName` | Who works it, from the new column |
| `WorkshopService` | Work while ordered or with no recipes; `IndexOf(kind)` | Serving is a station's work with no order; finding a station by kind becomes finding it by building and kind |
| `WorkStationIntentHandler` | Makes when the row has recipes | The work column: craft, serve or practise |
| `Drill` (cognition) | The drill yard by kind, built | Practise stations by the row's work |
| `SupplyService`, `SupplyTuning` (`CaravanTerms.ForStation`), `ISupplyHost.StationStands` | The tavern's menu supplied while a tavern stands | The bar's menu; wholesale as the fallback while the kitchen and barrel aren't staffed (Q4) |
| `WorkshopTuning.CostOf` | The row's cost line | New rows bring their cost lines |
| `SkillTable`, `SkillWork.Make`, `ItemCatalog`'s primers | Smithing at the forge, Alchemy at the still | Unchanged; cooking and brewing as skills are plan 2's question (a new skill changes every seed's draw) |
| `SharedWorldSnapshot.IsStationBuilt`, `SimulationManager` | Which stations stand | Unchanged |
| `TavernPlayer`, `GuildPlayer`, `EconomySoak` (Hosting) | Build the tavern, the drill yard, a forge or still by kind; find the tavern's index | The tavern player builds the hall on its lot and hires its keeper and cook |
| `StationModel` (the station window), `ArtKeys` (`prop/` and the row's art key) | The row's words and art key | New rows bring their words and art |

## 3. The map's walkable ground (measured 2026-11-25, Epic PLACES plan 1, P.1)

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

## 4. Buildings and lots (Epic PLACES plan 1, B.1)

- **A building is map data** (`BuildingDefinition`, `MapDefinitionBuilder.Building`, `.Lot`): its key, its kind, the zone it stands in wholly, its footprint (its walls' centre lines, on whole units, at least 4 × 4), its doors (centres on its edges, each a gap of `BuildingDefinition.DoorSpan` = 6 between the wall cells either side, as the map's doors are; the first its main door) and its sign (on the main door's edge, a cell past its post, toward the edge's middle). At most 32 a map.
- **Its walls are written once,** in `Build()` after every other wall: a unit cell at a time along its four edges, less its doors' gaps and any cell a wall already covers, merged into runs. A building standing on a zone's walls and doors adds none and leaves the nav grid as it was; against one wall of its zone, it adds the other three sides. `WallCellsAdded` says how many it added.
- **The builder refuses** a building in no zone or across two, overlapping another (sharing a wall is allowed), with no door, a door off its edges, on or crowding a corner, or overlapping another door, a kind too big for its lot, a footprint off whole units, or a kind with no row.
- **Where am I?** `MapDefinition.BuildingAt(position)`: the index of the building whose inside (within its walls' faces, not on them nor in a doorway) holds the point, or -1.
- **The building table** (`Simulation/Places/BuildingTable`): a row per `BuildingKind` (never reused; None is the empty lot) with its words, the lot it needs, its walls and floor (`WallMaterial`, `FloorMaterial`) and its art key. Lots by their footprint's area: small under 64, medium under 128, large from 128 (`BuildingTable.SizeOf`). Code asks the row (`BuildingNamesTests`).

## 5. Furniture (Epic PLACES plan 1, F.1)

- **The furniture table** (`Simulation/Places/FurnitureTable`): a row per `FurnitureKind` (never reused) with its words, its footprint in world units (width along x, depth along y), whether it blocks movement (only a table, in plan 1), its seats and beds as offsets from its centre, whom its beds are for (`BedFor`: residents or the guild), its beauty and comfort (-10 to 10, read from plan 3) and its art key. Plan 1's rows: table (1 × 1, blocks), bench (1.8 × 0.6, two seats), chair (0.7, one seat), stool (0.6, one seat), bunk and guild bunk (1 × 1, a bed each).
- **A placement is map data** (`FurnitureDefinition`, `MapDefinitionBuilder.Furniture`): its key, kind, centre and whether it is turned a quarter (its footprint's sides and its seats' and beds' offsets swapped), with the zone and the building it stands in. Its seats and beds in the world: `SeatAt(i)`, `BedAt(i)`.
- **The builder refuses** a piece in a wall (a zone's or a building's), in no zone, over another piece, of a kind with no row, or with a key taken. Code asks the row (`FurnitureNamesTests`).
- **Beds** (F.2, D13): `MapDefinition.Beds` lists every bed in the order its bunk is spawned, the bunk POIs first (a map not moved over), then the furniture's (a piece with one bed gives it the piece's key). The session host spawns the furniture's after its POIs; cognition's refuge reads residents' beds from it. Floor 1's twelve bunks and the barracks' eight are furniture, with the keys and places they had as POIs, so every entity keeps its id.

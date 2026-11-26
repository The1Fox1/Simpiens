# Epic PLACES plan 1: Groundwork, buildings and furniture (PL0–PL2)

This is a working document (AGENTS.md, Documentation Protocol 6), the first of Epic PLACES' three plans, written 2026-11-25 at the producer's "Write the first implementation plan for PLACES".
- **The design** is the epic, [`docs/epics/epic-places.md`](../docs/epics/epic-places.md): its positions P1–P2, its vocabulary (§2.1), its rules A1–A7 and the findings F1–F3.
- **The epic's questions Q1–Q9** stand at their defaults: the producer answers by exception, and none was raised when they asked for this plan. In short: fixed lots in the map; a lot takes any building that fits; no roofs; the tavern cooks and brews (plan 2); a building comes furnished and decor is the player's (plan 3); looks are felt (plan 3); the tavern first, the bunkhouse and barracks as buildings; before LIFE-2; "zone" in the player's words.
- **This plan's decisions** (D1–D15) are defaults too; each moves to the epic's §0 when its step lands.
- **Complexity:** Very Complex as a whole: a new level in the map model, furniture that changes walkable ground, art in two view modes and a map design for the producer. It is split below so that every step is Basic or Possibly Complex; none needs supervision.
- **Scope:**
  - **PL0, groundwork:** the vocabulary in code and docs, every reader of a station kind listed (F1), and Epic LIFE-1's two rough edges (the map card's day off, the day log's waking time).
  - **PL1, buildings:** buildings and lots as map data, their walls from a footprint, `BuildingAt`, validators, the look (walls and floors by style, doors, signs, foundations), and the bunkhouse as Floor 1's first building.
  - **PL2, furniture:** the furniture table, placements, bunks as furniture, seats from furniture (the tavern's tables and benches), furniture that blocks, and its look.
  - **A sketch for plan 2:** the outpost square, where the tavern hall, the bunkhouse and the barracks will stand (a producer's stop).
- **Branch:** `feat/epic-places` (from `main` at `4134c53`; it holds the epic's draft). One PR into `main` at the end, a commit per step.
- **Estimate:** 8–12 agent days: groundwork 1, buildings 3–4, furniture 3–4, the sketch, the DoD and the gate 2–3.
- **The producer's stops:**
  - the outpost square's sketch (S.1), before plan 2's map pass; it doesn't hold up plan 1's other steps;
  - the look at a building and at furniture, "the bunkhouse and the tavern's tables" (after F.5);
  - the merge.
- **The checks:**
  - golden unchanged through every step that only adds data or mechanism (G.1, G.2, B.1, B.2, F.1–F.3); a step that changes the map or the life record's bytes re-records it in its own commit, with the gate seeds' day held and scenario seeds re-picked where a moment moves;
  - the art pins and renders re-recorded after a look, in their own commits, the renders in the pinned image;
  - `AnEveningAtTheTavern` live on the real outpost with crawlers seated on drawn benches; the gate seeds' day with the bunkhouse a building and the tavern's tables blocking.

## 0. What `main` hands over (checked at P.1)
- **The map** (`MapDefinition`, built by `MapDefinitionBuilder`):
  - **Sectors** (zones): Floor 1 has 15 and the fallback, each a 16 × 12 room (the bottom row 16 × 6), with a name, a label, fog, maybe a hazard, and a facility flag (the tavern, the workshop row, the guildhall).
  - **Walls** are `WallRect`s (`Wall`, `WallRun` of unit cells on whole-unit centre lines), rasterized into the static nav grid (`WallRasterizer`). Doors are gaps of six units ("every doorway leaves 4 walkable cells after body inflation"); the guildhall's one door leaves 3, its room six units deep.
  - **POIs:** the stall, the hearth, 12 bunks, 8 guild bunks, spawn points, the descent gate. The session host spawns bunk entities from the bunk POIs (`SessionHost`, in POI order, so entity ids follow it).
  - **Stations:** four sites (forge, still, drill yard, tavern counter), each a point and a reach; `StationTable` rows.
  - **Venues:** five (the hearth, the stall, the Garden, the guild's yard, the tavern), their seats bare points. The tavern's eight are two tables of four in the room's west half, centred at (-4.5, 9.5) and (-4.5, 14.5), a seat 1.2 units out on each side; nothing is drawn there.
  - **Labels:** a room's name at a point.
- **The look:** the tile layer (`MapTileLayer`: floor, rock, wall, one style each), props (the hearth, bunks, the stall, stations built or not), the isometric cutaway of walls in front of rooms (`WallStyle`), labels laid out in screen space (`MapLabelLayout`).
- **Props and paths:** props (the stall, the hearth's ring) are kept clear by paths' smoothing (`IsSegmentPropClear`, `PropClearance` 0.75); nothing but walls is in the static nav grid. Its cells are one unit, and a wall is written inflated by a pawn's radius (0.25, `WallRasterizer.Inflation`), so a solid one unit wide blocks the cells either side of it as well.
- **The life record:** 222 spare bytes from 290 (the intention's day sits at 288).
- **The save:** format 15; the workshops' block holds each station's state.
- **The seated pose:** a crawler at an ale, a supper or dice is drawn `Seated` where its seat is, with an icon over it (Epic LIFE-1's V.3, V.4).

## 1. Decisions proposed (defaults; the producer answers by exception)
- **D1. What plan 1 delivers, and what waits.** Plan 1 delivers buildings as map data, standing from the start (Floor 1's bunkhouse) or as lots (proved on the Dev Map), and furniture. The player's build command for a lot, a building's state in the save, the revised station table and the tavern's move wait for plan 2.
  - **So the save format doesn't change in plan 1.** The epic's A7 put the raise at PL1; it moves to plan 2, with the first saved building state.
- **D2. A building's data** (`BuildingDefinition`): its key; its kind (a `BuildingKind`, None for an empty lot); the zone it stands in; its footprint (a `RectF` on whole-unit wall centre lines); its doors (each a gap in one of its edges, six units wide as the map's doors are); its lot size (small, medium, large); its sign's place. The builder refuses a building outside every zone, across two, or overlapping another.
- **D3. Walls from a footprint.** `MapDefinitionBuilder.Building(...)` adds wall runs along the footprint's four edges, less the doors. An edge that lies on a wall already in the map (a zone's) isn't added twice, so a building can stand against a zone's wall. The walls go into the static nav grid with the rest, from the start (Q1): nothing walkable changes at runtime.
- **D4. Building kinds are a table** (`BuildingTable`): the kind's word, what it's for, the lot size it needs, its wall and floor style, its sign, and what furniture it comes with (a list of kinds and offsets; used when plan 2's build command furnishes a lot). Plan 1's rows: the bunkhouse and the barracks. The tavern hall's row comes with plan 2, with its stations.
- **D5. A lot is a building of kind None.** It has a footprint, doors and a size; its walls stand in the nav grid from the start, and it is drawn as foundations: its walls as a low course of stone, its floor bare. Floor 1 gets none in plan 1. The Dev Map gets one, and one small standing building, for the validators and the renders.
- **D6. Floor 1's first building is the bunkhouse, filling its zone.** The Bunkhouse zone's room becomes the bunkhouse building on its existing walls and doors: no wall moves, so paths and golden's moments don't. It takes the bunkhouse's look (timber walls, a plank floor, its sign by the hub door).
  - **The barracks waits for plan 2's map pass.** The guildhall is six units deep: a building in its east half would be a door between two wall stubs. Plan 2's map pass can deepen the bottom row (the producer: the map may grow past the window).
- **D7. The outpost square, sketched now and built in plan 2.** Buildings inside zones need larger zones than Floor 1's rooms. The proposal for plan 2's map pass: the hub, the tavern's room and the bunkhouse's become one zone, an open square with the hearth and the stall in it, the tavern hall and the bunkhouse standing on its sides as buildings, and the Garden beside it again. S.1 draws it for the producer, with the walks it changes. It is a proposal: the producer may want another shape.
- **D8. `BuildingAt`.** `MapDefinition.BuildingAt(position)`: the building whose footprint holds a point (its interior, walls excluded), or none; at most 32 buildings a map, as venues. The snapshot reaches it through its map. Plan 1's readers are the views and the validators; cognition reads it from plan 2.
- **D9. Wall and floor styles in the tile layer.** `MapTileLayer` keeps its three kinds and gains a style per cell, from the building whose walls or floor the cell is: stone (a zone's, today's), timber (a bunkhouse's, a tavern's), foundations (a lot's); and flagstone (a zone's floor, today's) or planks. A style is a row in both view modes' art tables (`tile/wall/timber`, `tile/floor/planks`, ...).
- **D10. A door is drawn and a sign hangs by it.** A door's gap gets a frame (a pair of posts on the wall's line) so it reads as a doorway, not a hole; a building's sign is a small plaque on the wall beside its main door. The building's name is drawn by the map's label layout at the sign, smaller than a zone's name, the zone's name staying where it is.
- **D11. Furniture is a table** (`FurnitureTable`): the kind's word; its footprint in cells; whether it blocks movement; its seats (offsets from its centre) and its beds (a bunk's sleeping place); its beauty and comfort (columns now, read from plan 3); its art key. Plan 1's rows: table, bench, chair, stool, bunk, the guild's bunk. Ids are never reused; code asks the row.
- **D12. Furniture placements are map data.** `MapDefinitionBuilder.Furniture(kind, position)` places a piece in a building or a zone. The validators refuse a piece in a wall, a blocking piece across a door's way in, or a seat or bed on a blocked cell.
- **D13. Bunks become furniture.** The 12 bunk POIs and the 8 guild bunks become bunk and guild-bunk furniture at the same places, and the session host spawns their entities from them in the same order, so every entity's id, every claim and golden stay as they are. The bunk POI kinds stay (never reused) for the Dev Map until it's moved over.
- **D14. A venue's seats can come from furniture.** A venue lists the furniture it seats (by key), and its seats are their seat points in that order; a venue without furniture keeps its standing spots (the Garden's walk, the stall's browsing, the yard). The tavern's eight seats become two tables, each with four stools round it, at the same eight points, so the leisure score and every choice are as before.
- **D15. A table blocks movement; seats don't.** A blocking piece goes into the static nav grid as a wall does, inflated the same, so a one-unit table blocks the 3 × 3 cells round it; a bench, a chair or a stool doesn't block (a crawler steps onto it to sit). Only the tavern's two tables block in plan 1, and their stools move out with them, from 1.2 to 1.8 units from the table's centre, onto the first open cells. A seated crawler doesn't turn to face its table: the bodies are drawn from the front in both modes (a facing waits for a pose that needs it).

## 2. The milestones, phased

### P. The hand-over (half a day)
**Complexity:** Basic.
- [x] **P.1 The hand-over checked, and the map measured** [Basic: docs and a measurement]
  - Check §0 against `main` at `4134c53`; record any difference as a finding (the epic's §7, F4 onward).
  - A baseline for the map's changes: the walkable cells, the rooms each door joins, and the paths between the hub and every room (lengths), so a step that changes walls shows what moved.
  - **Done:** §0 held but for two slips of this plan's (15 zones, not 16; the guildhall's door leaves 3 cells, not 4), corrected above; no finding. The baseline (`MapMeasure`, `MapMeasureTests.MeasureTheMap`) is in `docs/places-model.md` §1: 1,790 walkable cells, the walks from the hub 11.2 (the Bunkhouse) to 34.9 (the Drowned Gallery); a regular test checks every zone is walked to from the hub.

### G. Groundwork: PL0 (1 day)
**Complexity:** Possibly Complex at most (a field in the life record).
- [x] **G.1 The vocabulary, and every reader of a station kind** [Basic: docs]
  - `docs/places-model.md` opens with the epic's vocabulary table and grows with each table's numbers.
  - F1's list: every place that reads a `StationKind` or a station's key (`Drill`, `TavernPlayer`, `GuildPlayer`, `EconomySoak`, the venue's needed station, `WorkshopRules`, the station window, the art keys), each with what plan 2's revision does to it.
  - **Done:** `docs/places-model.md` §1 (the vocabulary) and §2 (the station table today and 16 readers, each with plan 2's change).
- [x] **G.2 The map card says a day off** (Epic LIFE-1's F23) [Basic: a model line and the inspector card; presentation tests]
  - `InspectorModel` gains the intention's line ("Today: a day off."), the card shows it after the mood as the dossier does.
  - **Done:** `InspectorModel.IntentionLine`, the card's mood line joined with it as the dossier's is; `InspectorTests.TheCardSaysADayOff`.
- [x] **G.3 The day log's waking time** (Epic LIFE-1's F23) [Possibly Complex: a field in the life record's spare bytes; golden re-recorded for its coverage]
  - The tick an intention is drawn (when the crawler is first awake outside its night: when it got up) is kept with it, a `uint` at 292 (290–291 padding), so the day log's first line says when the crawler woke, not its waking hour. A record before it reads 0, and the day log falls back to the waking hour.
  - **The checks:** `IntentionValidator` (the tick kept, a record before it), `LifeModelsTests` (the line's time), the round trip.
  - **Done:** `CrawlerLife.WokeAt` / `WokeOn(day)` at 292 (spare 216 from 296), kept by `IntentionRules.Begin` from the draw's tick, left by a call-in; the day log reads it, the waking hour for a record without it. Golden unchanged: its 3,000 ticks end before any intention is drawn (the first routine day draws none).

### S. The outpost square, a sketch for plan 2 (half a day)
**Complexity:** Possibly Complex: a map design for the producer.
- [x] **S.1 The outpost square, sketched** (D7) [Possibly Complex: a design; the producer's stop]
  - A drawing of Floor 1 with the hub, the tavern's room and the bunkhouse's as one zone: an open square with the hearth and the stall, the tavern hall (a large lot), the bunkhouse and a lot for later on its sides, the Garden beside it, the barracks in a deepened guildhall. Each building's footprint, doors and sign; the walks from the Road Gate and the work rooms, against P.1's baseline.
  - It goes to the producer as a page to look at; their answer sets plan 2's map pass. Nothing in plan 1 waits for it.
  - **Done:** with the look at the bunkhouse and the tavern's tables, one page: https://claude.ai/artifact/3KCKNGXkco7DoL2ZCAkw9M. Proposed: A, the square at today's width (the hub, the tavern's room and the bunkhouse's one zone, 16 × 30; the tavern hall across its north end, 16 × 10; the bunkhouse across its south end; the hearth and stall between), over B, a column 24 wide (every side room moving out four units). Questions S1 (A or B) and S2 (the guildhall's row deepened to 12 for the barracks), by exception.

### B. Buildings: PL1 (3–4 days)
**Complexity:** Possibly Complex: a new map element, art in two view modes; each step below is at most Possibly Complex.
- [x] **B.1 Buildings as map data** (D2, D3, D4, D8) [Possibly Complex: a new map element and its validators; golden unchanged, no building on Floor 1 yet]
  - `BuildingDefinition`, `BuildingKind` and `BuildingTable` (the bunkhouse and the barracks); `MapDefinitionBuilder.Building(...)` writing the walls from the footprint less its doors, not twice over a zone's wall; `MapDefinition.Buildings` and `BuildingAt`.
  - **The checks:** `BuildingValidator` (the table; a building in one zone, never two, never overlapping; doors on its edges and six units wide; walls written once; `BuildingAt` inside, on a wall and outside); the builder's refusals; `BuildingNamesTests` (code names a building kind only in the table).
  - **Done:** as planned, the tables in a new `Simulation/Places/` namespace; the furniture a kind comes with waits for plan 2's build command (no reader in plan 1). `docs/places-model.md` §4.
- [x] **B.2 Lots, on the Dev Map** (D5) [Possibly Complex: the test map; golden unchanged]
  - A building of kind None is a lot. The Dev Map gains an empty lot and a small standing building, each with a door into a pen, so a validator walks a crawler in through the door to a point inside, and finds no way in through a wall.
  - **The checks:** `BuildingValidator` gains the walk; `MapDefinitionValidator`'s Dev Map checks; the Dev Map's renders.
  - **Done:** the Dev Map's pens are 4 × 7 on half-unit walls, too small for a building, so a closed building yard was added below them (zone 11, y -26.5..-14.5, on the pens' wall grid: half units in y; footprints and doors lie on the map's wall grid, `MapDefinition.WallAlignment`, which the tile layer reads too) with a barracks and an empty lot side by side, each with its door on its north edge; the camera still frames the pens (`DevMapLayout.PenFrame`), but the isometric view shows the yard's walls, so the Dev Map's two renders were re-recorded in the pinned image.
- [x] **B.3 The building's look: walls and floors by style, doors, foundations** (D9, D10) [Possibly Complex: the tile layer and art in two view modes; art pins and renders after a look]
  - `MapTileLayer` gains a style per cell from the buildings; generators for timber walls, plank floors, a door's frame and a lot's foundations (a low course of stone, a bare floor) in both modes; rows in both art tables; the isometric cutaway treats a building's front walls as a room's.
  - **The checks:** `ArtCompletenessTests` (every style has its rows, every row used), `AtlasLayoutTests`; the art review sheet looked at; the art pins and the Dev Map's renders re-recorded in their own commits.
  - **Done:** `MapTile.Material` from the buildings (a wall on a building's edge takes its walls, a floor inside it or across a doorway its floor, untinted); `BuildingSprites` (timber standing and low, foundations, planks, bare earth, both modes); the door reads as a gap with the building's floor as its threshold, no separate frame sprite (the cutaway lowers front walls, so posts wouldn't show).
- [x] **B.4 Signs, and a building's name on the map** (D10) [Possibly Complex: art and the label layout]
  - A sign plaque by the main door (a decor sprite, its art row); the building's name in the map's label layout at the sign, smaller than a zone's, kept clear of it.
  - **The checks:** `MapLabelLayout`'s tests (no two labels overlap at any offered zoom); art pins; renders.
  - **Done:** the sign is a post standing outside the main door's wall, a cell out (a plaque on the wall would float over a cut-away wall); `LabelKind.Building` a size under its zone's name, hidden with it and where it would overlap; a building whose zone already bears its name isn't named twice.
- [x] **B.5 The bunkhouse, Floor 1's first building** (D6) [Possibly Complex: map data; renders]
  - The bunkhouse building on the Bunkhouse zone's walls and doors, with its look and its sign by the hub door. No wall moves.
  - **The checks:** golden (the map's hash may move for the building's record; the crawlers' parts must not: the step's commit says which); the gate seeds' day; the renders that show the bunkhouse, re-recorded in the pinned image.
  - **Done:** `Floor1Layout.BunkhouseBuilding` on the room's walls and doors (main door the hub's, its sign in the hub at (0, -5)); no wall added, so the nav grid, the save's map fingerprint and golden are unchanged; the zone's label already reads "Bunkhouse", so the building isn't named twice.

### F. Furniture: PL2 (3–4 days)
**Complexity:** Possibly Complex: a new table, seats cognition reads, walkable ground that changes, and art; each step below is at most Possibly Complex.
- [x] **F.1 The furniture table and placements** (D11, D12) [Possibly Complex: a new table and its validators; golden unchanged]
  - `FurnitureKind`, `FurnitureTable` (table, bench, chair, stool, bunk, guild bunk; beauty and comfort as columns), `MapDefinitionBuilder.Furniture(...)`, `MapDefinition.Furniture`.
  - **The checks:** `FurnitureValidator` (the table; no piece in a wall; no blocking piece across a door's way in; seats and beds on open cells); `FurnitureNamesTests`.
  - **Done:** footprints in world units, not cells (a stool is 0.6 across); a piece may be turned a quarter; seats and beds as offsets; `BedFor` says whose beds. The door's way in and open cells are checked against the built grid once a piece blocks (F.4), where they can be.
- [x] **F.2 Bunks as furniture** (D13) [Possibly Complex: entity spawn order; golden unchanged]
  - Floor 1's 12 bunks and 8 guild bunks become furniture at the same places; the session host spawns their entities from the furniture in the same order.
  - **The checks:** golden unchanged (ids and claims as before); `RestAnchorValidator`; `TiredCrawlersSleepInTheBunkhouse`; the barracks' guild tests.
  - **Done:** `MapDefinition.Beds` (POI bunks in POI order, then the furniture's beds) is the one list of beds: the session host spawns furniture beds after its POI loop (Floor 1's hearth came before its bunks, so every id holds), and cognition's refuge (`Vitality.TryGetRefugeFrom`) reads it. Golden unchanged; the full core suite 422/422 and the client's 579/579 locally.
- [x] **F.3 Seats from furniture: the tavern's tables and stools** (D14) [Possibly Complex: cognition reads seats; golden unchanged]
  - Venues may list the furniture they seat; the tavern's venue lists its eight stools, whose seat points are the eight it had. The tables are placed but don't block yet.
  - **The checks:** `VenueValidator` (a venue's seats on its furniture, in its sector, clear of walls); `LeisureTests` (the score as before); `AnEveningAtTheTavern` on its seed.
  - **Done:** `MapDefinitionBuilder.Venue(..., seatedOn:)` takes the pieces' seats in order (`VenueDefinition.SeatedOn` keeps their keys); the tavern's two tables and eight stools, the stools written out as the old seat points (a table's centre plus 1.2 rounds one of them an ulp away in float). Golden unchanged; `AnEveningAtTheTavern` and `AWantComesTrue` pass.
- [x] **F.4 A table blocks** (D15) [Possibly Complex: static nav changes; golden re-recorded if its 3,000 ticks change, scenario seeds re-picked where a moment moves]
  - A blocking piece goes into the static nav grid, inflated as a wall is (`WallRasterizer`); the tavern's two tables block, and their stools move out to 1.8 units (D15). `FurnitureValidator` gains the check that every seat and bed is on an open cell of the built grid.
  - **The checks:** the gate seeds' day; P.1's paths re-measured (only the tavern's change); `AnEveningAtTheTavern` and the tavern soak; the anomaly detector's days (crowding round the tables).
  - **Done:** the rasterizer writes a blocking piece as a wall; the tavern's stools stand 1.8 out and its venue's radius is 4.75 (company counts within it). Re-measured: 1,772 walkable cells (two tables, nine cells each), only the tavern's 148 to 130, every walk from the hub as it was. The anomaly detector counts a table as an obstacle; the save's map fingerprint mixes the beds and the blocking furniture (it already moved at F.2, when the bunks left the POIs: saves from before then are refused by it, as any map change's are). Golden unchanged; the full core suite 422/422 (the gate seeds' day, `AnEveningAtTheTavern` on its seed: no re-pick); the client's 579/579 locally.
- [ ] **F.5 Furniture's look** [Possibly Complex: art in two view modes and the scene; art pins and renders after a look; the producer's look after it]
  - Generators for a table, a bench, a chair and a stool (bunks keep their sprites, read through their furniture rows); rows in both art tables; the scene draws furniture under the crawlers at it (a seated crawler over its bench).
  - **The checks:** `ArtCompletenessTests` (every furniture kind has its art), art pins, renders; the producer's look at the bunkhouse building and the tavern's tables.

- [x] **F.6 A seat is one crawler's** (the epic's F4, found at F.5's look) [Possibly Complex: main-thread claims, a behaviour change at venues; golden unchanged]
  - A sitting spell at a venue seated on furniture claims its seat as it arrives (`Seating`, beside `Lodging`; `IIntentExecutionContext.TryClaimSeat`); a seat held by another fails the intent, and the crawler chooses again; its own, it takes the last steps onto the seat's point (`RecreateIntentHandler.SettleDistance`), so others see it taken. A claim lasts only while its holder is at a spell on that seat, goes with a crawler leaving the outpost, and isn't saved (work in progress, as bunk claims). A venue's standing spots (the stall's browsing, the yard's sparring) stay as they were.
  - **The checks:** `SeatingValidator`; `AnEveningAtTheTavern` (D.1) fails without the claim (every seated tick off its stool) and passes with it.
  - **Done:** first written for every sitting pose, it took in the stall's browsing (an idle pose) and moved golden and 14 seeded scenarios; narrowed to seats on furniture, golden is unchanged and the full core suite 423/423.

### D. The Definition of Done (1 day)
**Complexity:** Possibly Complex: a live scenario with nothing injected.
- [x] **D.1 `AnEveningAtTheTavern`, seated at its tables** [Possibly Complex: a live scenario]
  - On its seed with the tavern player, nothing injected: every crawler drawn `Seated` stands on one of the tavern's bench seats, the tables are walked round and never through, and the tavern serves on two days as before.
  - **Done:** on seed 5, 7,018 crawler-ticks seated at the tables over three days, none off its stool (within 0.06), none sharing one, no crawler ever in a table; it serves on days 2 and 3 as before. Without F.6's claim every seated tick is off its stool and the test fails.
- [x] **D.2 The gate seeds' day with buildings and furniture** [Basic: an existing scenario re-read]
  - `TheOutpostsDayHoldsOnEverySeed` with the bunkhouse a building and the tavern's tables blocking; a sleeper's bunk read through its furniture.
  - **Done:** it held on all 23 gate seeds in every full core run from B.5 on (the bunkhouse a building, its bunks furniture since F.2, the tables blocking since F.4, seats claimed since F.6), with nobody starving and the sleep check passing on beds spawned from furniture.

### C. The gate and close-out (1–2 days)
**Complexity:** Possibly Complex: measuring.
- [ ] **C.1 The gate** [Possibly Complex: measuring]
  - **The tests:** every test, the golden check, the gate seeds' day, the client's tests in the pinned image.
  - **The soak:** every profile's band on the final build.
  - **Measurements:** P.1's map baseline re-run (the walks that changed and why); the day (`MeasureACrawlersDay`), against Epic LIFE-1's gate.
  - **Cost:** `PopulationScalingProbe` at 16, 24 and 48 crawlers beside `main` at the branch's start, in a quiet sitting.
- [ ] **C.2 Close-out** [Basic: written from lists this plan holds]
  - **The docs, in order:** the roadmap first (Epic PLACES ACTIVE, plan 1 done); the epic's status, §0 (D1–D15), §4 and §7; `docs/places-model.md`; `AGENTS.md` (buildings and furniture in the map's notes; recipes: a building kind, a furniture kind); the focus file and `.agents/architecture_history.md`.
  - **Delete this plan.**
  - **Open the PR** into `main` at the producer's word.

## 3. Risks (this plan's; the epic's §6 holds the rest)
- **A building filling its zone looks redundant** (D6). The bunkhouse is a building on its room's walls only until plan 2's square gives it a zone to stand in; the code and art don't change when it moves, only its data.
- **The sketch may send plan 2 elsewhere** (S.1). The producer may want another shape than the square; plan 1's mechanism serves any shape of zone and building.
- **Tables crowd the tavern** (F.4). Eight seats round two tables in a 16 × 12 room is roomy, but the counter's reach and the door's way in must stay clear: `FurnitureValidator` refuses a blocking piece across a door's way in, and the anomaly detector reads the days.
- **A blocking piece is bigger than it looks** (F.4). Inflated as a wall is, a one-unit table takes 3 × 3 cells of walkable ground, so its seats must stand 1.5 units or more from its centre. A table drawn one unit wide with stools 1.8 out may read as sparse: the art (F.5) draws the stools pulled in toward it, the seat points staying where the grid allows.
- **Bunks' ids** (F.2). The session host must spawn bunks from furniture in the POIs' order, or every entity id after them moves and golden with it: the step proves golden unchanged before it lands.
- **Two styles of the same wall** (B.3). A wall cell shared by a zone and a building against it takes the building's style on its inside face only if the art can show two faces; otherwise the building's style wins, a decision the art review sheet settles.
- **Cost:** furniture adds a few static cells, and `BuildingAt` a short loop for the views: C.1 measures.

## 4. What this plan leaves
- **Plan 2** (PL3–PL4): the station table revised (work, service, practice; who works it; which building kinds host it; F1's readers moved), the tavern hall with its bar, cooking station and brew barrel (Q4), the build command for a lot and buildings' state in the save (the format's raise), the map pass (S.1's square or the producer's shape, the barracks in a deeper guildhall), the tavern moved, scenario seeds re-picked.
- **Plan 3** (PL5, PL6, PLG): decor and a place's beauty (Q6), the player's decor (Q5), a night tint with lamps (F3), "At the Tavern" in cards and the dossier, the floor list's buildings, the building window, and the epic's gate.

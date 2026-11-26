# Epic PLACES: Buildings, Furnishings and the Outpost's Look

- **Status**: ACTIVE (2026-11-25, at the producer's "Proceed" on plan 1; its questions Q1–Q9 at their defaults). Drafted the same day at the producer's request: "Please write up an Epic to introduce venues / buildings, furniture / decor / environmental ascetics, and clarify what a 'station' really is and is not." **Plan 1**, Groundwork, buildings and furniture (PL0–PL2), drafted 2026-11-25 at the producer's "Write the first implementation plan for PLACES" and under way the same day: [`.agents/places_buildings_plan.md`](../../.agents/places_buildings_plan.md).
- **Why now:** Epic LIFE-1's close-out found that the tavern doesn't read as a tavern (its F23). The sim has no buildings: a sector is the only walled enclosure, so the tavern took a whole sector, and inside it only the counter is drawn. Its eight seats are invisible points, and crawlers drinking together look like a crowd on bare floor beside the hub.
- **Track**: Track A (Core Simulation & Cognition Engine), with its client work done here, as in Epic 11 and Epic LIFE-1.
- **Milestone**: Production Milestone 6, A Living Outpost (proposed: between Epic LIFE-1 and Epic LIFE-2; the producer's call, §0 Q8).
- **Preceding**: [Epic LIFE-1](epic-life-1.md) (venues as map data, the tavern as a station, Recreate, the seated pose). **Following** (proposed): Epic LIFE-2 (talk, gifts and bonds happen in places), then LIFE-3 and Epic 12.
- **Builds on:**
  - **The map:** `MapDefinition` (sectors, walls rasterized into the static nav grid, POIs, stations, venues, lairs, labels), `Floor1Layout`, the map validators; the isometric view's cutaway of walls in front of rooms (`WallStyle`).
  - **The workshops:** `StationTable` and `StationKind` (forge, still, drill yard, tavern), `WorkshopService` (built, ordered, the current make), artisans on shifts, `RecipeBook`, the stall's stock.
  - **The life core:** venues (`VenueDefinition`: seats, activities, hours, access, the station they need), Recreate and the leisure score, thoughts and mood, the seated pose and activity icons.
  - **The art:** sprite generators, both view modes' art tables, the art pins and the renders in the pinned image.
- **Complexity** ([rubric](../../.agents/rules/complexity-grading.md)): **Very Complex** as an epic. It adds a level to the map model (buildings inside zones), moves the tavern, refactors what a station is, and draws a new family of art in two view modes. Each milestone is stamped in §2.4; the plans split the Very Complex ones.

---

## 0. Producer intent, decisions and questions

**The producer's notes (2026-11-25):**
> I see the issue. The tavern goers are at what was previously the hearth. A tavern is a building, as a user they would have no idea that these crawlers were in a "tavern" when they are simply congregating and drinking together.
>
> Do we not have a concept for "buildings" in our sim? It might benefit us to make a distinction between a "zone" and a "building" both of which should have walls but many buildings can exist in a zone.

> I agree that stations should not be conflated with buildings. A building/venue should be able to host different stations such as a tavern hosting a central bar, cooking station, brew barrel, etc.

**Decided by the producer:**
- **P1. Zones and buildings are different things.** A zone (today's sector) is a walled region of the floor; a building is a walled structure inside a zone, and a zone may hold many.
- **P2. A station is not a building.** A building hosts stations, several of different kinds: a tavern's central bar, cooking station and brew barrel.

### The questions (defaults proposed; the producer answers by exception)
- **Q1. Where buildings stand: fixed lots or the player's choice?** *Default: fixed lots in the map.* A building's footprint, walls and doors are map data from the start, drawn as an empty lot (foundations) until it's built. The player chooses whether and what to build on a lot, not where. Free placement means changing walkable ground at runtime, re-planning paths and saving walls; it waits for the dev map designer, and the lot data is what it would place.
- **Q2. Does a lot take any building, or one kind?** *Default: a lot has a size and takes any building kind that fits it* (a tavern hall needs a large lot; a smithy, a medium one). A lot fixed to one kind is simpler but turns every building into a map edit.
- **Q3. Roofs?** *Default: none.* The outpost is underground on Floor 1: a building is a walled hall with a door and a sign, and its front walls cut away in the isometric view as rooms' do today.
- **Q4. Does the tavern make what it serves?** *Default: yes, in steps.* Its bar serves (a keeper's work); its cooking station cooks supper from rations and herbs (a recipe, a cook's work); its brew barrel brews ale over time from herbs (a slow recipe). The caravan's wholesale ale and stew stay as the fallback while nobody cooks or brews. This deepens the economy, so the soak's bands are re-measured (§3).
- **Q5. Who furnishes and decorates?** *Default: a building kind comes with what its work needs* (a tavern hall with its bar, two tables and their benches) *and decor is the player's to add*, bought from the workshop window within the building's room for it. A full furniture editor waits for the map designer.
- **Q6. Do looks matter to crawlers?** *Default: yes, a little.* Each furniture and decor kind has a beauty and a comfort; a place's beauty (its decor and furniture over its floor) is felt as a thought after a spell there ("A pleasant room") and weighs the leisure score. RimWorld's room impressiveness is the model, at a smaller scale.
- **Q7. Which of today's places become buildings?** *Default:* the tavern first (it moves from its own sector into a hall in a zone, and the Garden can move back beside the hub); the bunkhouse and the guildhall's barracks become buildings in their zones in the same map pass; the forge and the still stay open stations on Workshop Row, with a smithy building as a later option.
- **Q8. When?** *Default: before Epic LIFE-2.* LIFE-2's talk, gifts and bonds need places with character (the tavern, later homes), and LIFE-3's artisans build on clarified stations. It is Milestone 6 work: the outpost living.
- **Q9. "Zone" in the player's words?** *Default: yes.* The dashboard's "THE FLOOR" lists zones, and a building is named inside one ("The Tavern, in the Outpost Hub"). Code keeps `Sector` (renaming it would touch every system for no gain); the docs say "zone (a sector in code)".

---

## 1. Goal
A player can tell where a crawler is and what it's doing by looking. A tavern is a building with walls, a door, a sign, tables and a bar; crawlers walk in through its door, sit at its tables and are served at its bar. Each word for a place means one thing (zone, building, venue, station, furniture, decor), and code, data and the player's words agree.

---

## 2. Design

### 2.1 The vocabulary: what each thing is, and is not

| Word | Is | Is not | Today |
| :--- | :--- | :--- | :--- |
| **Zone** | A walled region of the floor with a name, fog of war, maybe a hazard; where bounties point and the dashboard's floor list groups crawlers | A building; furniture | `SectorDefinition` (sector in code) |
| **Building** | A walled structure inside one zone: a footprint, doors, a name and sign, a floor and wall style, an interior. It hosts stations, furniture, decor and venues. Built by the player on a lot, or standing from the start (the bunkhouse) | A zone (it has no fog of its own, no hazard, no bounties); a station; a business | Missing (the tavern is a whole sector) |
| **Lot** | A building's place in a zone before it's built: its footprint, doors and size, drawn as foundations | A building | Missing (stations have sites) |
| **Station** | A fixture where a crawler works: crafts (forge, still, cooking station, brew barrel), serves (a bar), or practises (a training dummy). It has a kind (its work, recipes or service, who may work it, its cost, its art), a place and a reach, a build state, and a worker's slot and shifts | A building (no walls, no door, no name on the map); a venue (it offers no free time by itself); a business; furniture; a store of goods (it works from the stall's stock) | `StationDefinition`, `StationTable`; but `Tavern` and `DrillYard` are station kinds standing for a business and a yard (F1) |
| **Furniture** | An object crawlers use without working at it: tables, benches, chairs and stools (seats), bunks and beds, shelves | A station (no work, no worker); decor (it has a use) | Bunks are POIs; seats are invisible points in a venue |
| **Decor** | An object that only looks: lanterns and torches, banners, rugs, plants, trophies, a sign. It has a beauty | Furniture; a station | Missing (the hearth's ring and the hazards' animations are the nearest) |
| **Venue** | What a place offers for free time: its activities, seats, hours and who may come. It may belong to a building (the tavern hall's evening), to a zone (the Garden's walk, the hearth's fireside) or to a yard | A building (the Garden's walk needs no walls); a station | `VenueDefinition`, with seats of its own and the one station it needs |
| **Business** | A building whose venue and stations a staff keeps: the tavern is its hall, its bar, kitchen and barrel, its venue, its keeper and cook, its hours and menu | One station | `StationKind.Tavern` plus its venue and keeper |

**My verdict on the producer's "building/venue":** keep the two words apart. A building is the structure; a venue is the free time a place offers. Most venues worth visiting will live in buildings, but the Garden's walk and the hearth's fireside need no walls, and a building may hold work and no leisure at all (a smithy).

### 2.2 Positions (they bind this epic)
- **Buildings are map data** (Q1): a lot's footprint, doors and size are in the map; walls are rasterized into the static nav grid with the zone's walls, so nothing walkable changes at runtime. An unbuilt lot is drawn as foundations and walked through as an empty room.
- **One zone each:** a building lies wholly inside one zone; a zone holds any number; buildings never overlap; every building has a door onto its zone.
- **Stations belong to a place:** a station stands in a building or, open-air, in a zone. A building kind lists the stations it hosts.
- **Seats are furniture:** a venue's seats come from the chairs, benches and stools at its tables, so what a crawler sits on is drawn. The seated pose sits on them.
- **Venues belong to a place:** a building's venue opens while its building stands and its service station is staffed (today's "kept" tavern), within its hours.
- **Every new system is a table:** building kinds, furniture kinds, decor kinds, and the station table revised; ids never reused; names guarded by a names test, as `DeedNamesTests` does. Code asks the row, never the kind.
- **Looks are measured, not hand-tuned per place:** beauty and comfort are columns on the furniture and decor rows, and a place's beauty is computed from what stands in it.

### 2.3 Architecture and rules
- **A1. The map gains buildings.** `BuildingDefinition` (key, zone, footprint, doors, lot size, wall and floor style, sign position, the stations, furniture and decor placed in it) beside `SectorDefinition`; the builder writes its walls with the zone's. A building's state (built, its kind, its staff) lives with the workshops' state and is saved in its block.
- **A2. Where am I?** `MapDefinition.BuildingAt(position)` and the snapshot's copy answer which building a point is in, for the views ("At the Tavern"), venues and validators. Fog, hazards, charting and bounties keep reading zones.
- **A3. The station table, clarified.** A station kind's row says what work it does (craft, serve, practise), its recipes or service, who works it (an artisan's skill, a keeper, a guild member at drill) and what building kinds may host it. `Tavern` becomes the bar (its id kept: stations are saved by kind); the drill yard becomes a yard (a venue) with training dummies (stations of the practise kind).
- **A4. Furniture and decor are tables and map data.** `FurnitureTable` (its word, footprint, whether it blocks, its seats and their facing, beauty, comfort, art key) and `DecorTable` (its word, footprint, beauty, art key, a light if it glows); placements in buildings and zones. Furniture that blocks is a static obstacle in the nav grid; small decor doesn't block.
- **A5. The art follows the tables.** A row per kind in both view modes' tables; generators for walls by style (timber, stone), floors (planks, flagstones), doors, signs, furniture and decor; the art pins and renders re-recorded after a look.
- **A6. Looks reach crawlers through the life core** (Q6): a place's beauty is a thought after a spell there and a factor on the leisure score, through the motive model and the thought table; nothing reads a building's kind to decide.
- **A7. Save:** buildings' state and placed decor are saved; the format is raised once, at the milestone that adds them (PL1), and the tavern's move is a map change (a respawn rebuilds it; an old save is refused by its map fingerprint, as today).

### 2.4 Milestones

| Milestone | What the player sees | Core (engine-free simulation) | Client (KNI) | Complexity | Live scenario |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **PL0. Groundwork** | Nothing new; LIFE-1's two rough edges mended (the day log's waking line, the map card's day off) | The vocabulary in code and docs; every reader of a station kind found and listed (F1); `BuildingAt` with no buildings yet; the validators' shape | The map card's day-off line | Possibly Complex | Golden unchanged |
| **PL1. Buildings and lots** | Buildings with walls, doors and signs inside zones; empty lots as foundations | `BuildingDefinition`, the building kinds' table, lots, walls into the nav grid, doors, validators (one zone, no overlap, doors reachable, every room connected); the bunkhouse and the barracks as buildings; save format raised | Walls by style, floors, doors, signs, foundations; labels for buildings; cutaway of front walls | Very Complex (a map model, nav and the save: split in its plan) | The gate seeds' day holds with the bunkhouse and barracks as buildings; crawlers sleep in them through their doors |
| **PL2. Furniture** | Tables, benches, chairs, bunks and shelves drawn; crawlers sit on chairs | The furniture table; placements; seats from furniture (venues read them); blocking furniture in the nav grid; bunks as furniture | Furniture art in both modes; the seated pose on a seat | Possibly Complex | `AnEveningAtTheTavern`'s crawlers sit on drawn seats |
| **PL3. Stations, clarified** | The tavern hall with its bar, cooking station and brew barrel; the drill yard as a yard with dummies | The station table revised (work, service, practice; who works it; where it may stand); a building hosts several stations; the bar serves, the kitchen cooks supper, the barrel brews ale (Q4); the keeper at the bar, a cook at the kitchen | Station art for each kind; the station window lists a building's stations and their staff | Very Complex (the workshops, the economy and the tavern's move: split in its plan) | `ATavernMakesItsSupper`: on the real outpost with the tavern player, the cook cooks stew from the stall's goods, the bar serves it in the evening, and the ledger balances |
| **PL4. The tavern moves** | The tavern is a hall in a zone, with its door, sign, tables and bar; crawlers walk in through the door | The map pass (Q7): the tavern hall on a lot in a zone, the Garden beside the hub again if the producer wants it back; scenario seeds re-picked | Renders re-recorded | Possibly Complex | `AnEveningAtTheTavern` by its door: residents enter the hall, sit, are served, and leave |
| **PL5. Decor and the look of a place** | Lanterns, banners, rugs and plants; a building's look; at night, lamps glow | The decor table; placements; beauty and comfort; a place's beauty computed; its thought and its weight on leisure (Q6); the player buys decor for a building (Q5) | Decor art; a night tint over the floor with lamps' light (the outpost has a night since LIFE-1); the building window's beauty | Possibly Complex | `APleasantRoom`: a decorated tavern hall draws more evenings than a bare one on the same seed, and its guests think "A pleasant room" |
| **PL6. What the player reads** | "At the Tavern" on cards and in the dossier; the floor list shows buildings in their zones; a building window | The models: a crawler's place in words; buildings in the zone panel | Gum: cards, the dossier, the floor list, the building window | Possibly Complex | Presentation tests and renders |
| **PLG. The gate** | PLACES played through | Every scenario together with a save mid-evening; the gate seeds; the soak's bands with the tavern making its own; cost at 12, 24 and 48 crawlers | The producer's play session | Possibly Complex | `PlacesTogether`: the tavern built on a lot, staffed, decorated, cooking and serving, and its guests' thoughts, on one seed, saved and played on |

**Order:** PL0, then buildings (PL1) and furniture (PL2), so stations (PL3) have a building to stand in; the tavern moves (PL4) once its hall and stations exist; decor (PL5) and the player's words (PL6) follow, then the gate.

**The producer's looks:** PL1 (a building and a lot), PL3 and PL4 (the tavern hall), PL5 (a decorated room at night); then the play session at PLG.

**Plans (proposed, one PR each):** 1. Groundwork, buildings and furniture (PL0–PL2); 2. Stations and the tavern (PL3–PL4); 3. The look and the gate (PL5, PL6, PLG). About 18–28 agent days in all.

**Plan 1's changes to this section** (its decisions, defaults by exception; they move to §0 as their steps land):
- **The save's raise moves to plan 2** (its D1): plan 1's buildings and lots are map data only, and the first saved building state is plan 2's build command for a lot. A7 and PL1's row read "plan 2" for the raise.
- **The barracks moves to plan 2's map pass** (its D6): the guildhall is six units deep, too shallow for a building in its half. Plan 1 makes the bunkhouse a building on its room's walls, so no wall moves, and sketches an outpost square for plan 2 (its D7, S.1): the hub, the tavern's room and the bunkhouse's one zone, with the tavern hall and the bunkhouse standing in it.

---

## 3. Definition of Done
Done means the places emerge in the live outpost with nothing injected (ROADMAP §1B); a scripted player acts only through typed commands.
- **Mechanism:** validators for each table (building kinds, furniture, decor, the revised stations), the map's buildings (one zone, no overlap, doors reachable, rooms connected), seats from furniture, a building's beauty, the save's round trip.
- **Live, headless:** the scenarios in §2.4's last column, and every one together at the gate (`PlacesTogether`).
- **Golden:** unchanged through PL0; every map or behaviour change re-records golden in its own commit, with the gate seeds' day held and scenario seeds re-picked where a moment moves.
- **The economy:** the ledger balances with the tavern's own cooking and brewing; the soak's `tavern` profile re-measured, its bands set by the producer.
- **The look:** each milestone's art looked at on the art review sheet before the pins are recorded; renders recorded in the pinned image.
- **Docs:** the roadmap first, then this file's status, then the focus file; `architecture_history.md`; a `docs/places-model.md` for the tables' numbers; `AGENTS.md` (recipes: a building kind, a furniture kind, a decor kind, a station kind as revised).

## 4. Acceptance checklist (delivered work only)
None yet.

## 5. Deferred and out of scope
| Item | Where it goes |
| :--- | :--- |
| Free placement of buildings and furniture by the player, a build mode | The dev map designer (Milestone 7's tools); lots are what it would place |
| Homes: a crawler's own room, its keepsakes on a shelf | Epic LIFE-2 (keepsakes) or LIFE-3 (aspirations), on PL1's buildings |
| A smithy and an apothecary as buildings for the forge and the still | After PL3, the producer's call (Q7) |
| Cleanliness, wear and repairs of furniture | Not planned |
| Roofs | Not planned (Q3: underground) |

## 6. Risks
| Risk | Mitigation |
| :--- | :--- |
| Walls inside zones change paths everywhere, and every scenario's moments with them | Map changes land in their own steps with golden and one re-pick round each; the validators prove every room is connected before anything moves |
| The floor runs out of room for lots | The producer's word of 2026-11-24: the map may grow past the window; the camera pans |
| In-house cooking and brewing unbalance the tavern's and the stall's economy | The caravan's wholesale stays as the fallback; the soak re-measured; the producer sets the bands (Q4) |
| Furniture that blocks crowds the tavern's doorway | Blocking is a column per furniture kind; doors validated reachable with every piece placed; the anomaly detector's day checks |
| Decor's beauty becomes a number nobody notices | It is felt as a thought with a reason the dossier shows, and measured: `APleasantRoom` must show more evenings in a decorated hall |
| Art for two view modes doubles the work | Generators and rows, as items and creatures are; isometric reuses top-down's patterns where it can |

## 7. Findings
- **F1. Station kinds stand for different things** (read while drafting, 2026-11-25). `StationKind` holds a workstation (the forge, the still), a yard (the drill yard: "no recipes and no artisan; guild members drill there") and a business (the tavern: "kept by a keeper on its shifts; no recipes; a venue for the evening"). The venue reads its station's built and kept state, and the scripted players find stations by kind (`TavernPlayer`, `GuildPlayer`, `EconomySoak`). Owner: PL0 lists every reader, PL3 revises the table [Very Complex, split].
- **F2. Seats are invisible** (Epic LIFE-1's F23). A venue's seats are points with no furniture under them; the seated pose sits on bare floor. Owner: PL2 [Possibly Complex].
- **F3. The outpost has a night but no darkness.** Since Epic LIFE-1 the outpost sleeps at night, and the header's glyph turns to a moon, but the floor is drawn as at noon. Owner: PL5, with lamps' light [Possibly Complex].
- **Carried from Epic LIFE-1's F23:** the day log's waking line uses the waking hour, not when the crawler left its bed; the map's crawler card doesn't say it's a day off. Owner: PL0 [Basic]. Resolved in plan 1's G.2 and G.3.
- **F4. Seats were shared** (found by plan 1's F.5 look, 2026-11-26). A crawler took the free seat nearest it as judged by who sat there, nobody claimed one, and its walk ended within half a unit of it, so crawlers choosing at once picked the same seat and sat bunched near it while other seats stood empty. Drawn stools made it plain (under Epic LIFE-1 it read as a group sitting close). Resolved in plan 1's F.6: a sitting spell claims its seat on arrival (one crawler a seat, as a bunk holds one sleeper) and settles on its point; `AnEveningAtTheTavern` checks every seated crawler is on its own stool.

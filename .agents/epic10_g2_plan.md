# Epic 10 G2: health and hazards (working plan)

Working document (AGENTS.md, Documentation Protocol 6): deleted when G2 closes. Epic: `docs/epics/epic-10.md` §2.2, §3;
producer decisions Q2 (no combat: the dangers are the environment's), Q3 (no death: downed at 0 health), Q9 (one rare
salvage, found only in hazard sectors, bought by the caravan at a premium). Branch `feat/epic10-g2`, stacked on
`feat/epic10-g1` (PR #8, itself on #7). **The producer looks at G2** (danger on screen) before G3.

## Goal
The floor has danger. Two sectors beyond the near work rooms hurt whoever stands in them, and hold a rare salvage worth
the risk. Bold crawlers go in on their own, get hurt, come out or are downed, and heal in the bunks, faster with a herb.
Cautious crawlers stay out.

## Design (defaults; numbers are tuning)
- **The map** (`Floor1Layout`): the two blocks of solid rock either side of the lower wing become sectors, so the floor's
  size and every existing room stay as they are.
  - **The Sump** (id 9, x -24..-8, y -18..-6), entered from the Ore Quarry by a door at (-16, -6): foul air.
  - **The Glow Caves** (id 10, x 8..24, y -18..-6), entered from the Herb Grove by a door at (16, -6): falling rock.
  - Both start charted (the danger is known; Q: fog later), each with three rare-salvage nodes. Labels as the others.
- **Hazards are map data:** `SectorDefinition.Hazard` (`SectorHazard`: a kind and damage per second). The simulation hurts
  every crawler standing in a hazard sector each tick; G3's gear will reduce it (a mitigation term, 0 for now).
- **Health:** `AgentState.Health` (0–100, 100 on spawn), saved. At 0 a crawler is **downed**: it stops what it was doing and
  limps to the nearest bunk at reduced speed (a reflex, like exhaustion collapse), where it sleeps and heals. Health
  recovers slowly anywhere, faster asleep in a bunk; eating a medicinal herb heals a chunk (herbs gain a use).
- **The rare salvage:** `ItemType.Glowstone` (placeholder name). The stall buys it at its own price (player-settable, in
  the price command and the merchant terminal) up to a capacity; the caravan buys the stall's whole stock each visit at
  a premium market price (the `CaravanSalvageSales` faucet). `TradeStallData` gains its price and stock (economy block,
  save format 3; `EconomySerializationValidator` sizes updated).
- **Who goes in:** salvage targeting counts hazard nodes only for a crawler whose risk tolerance (personality) is over a
  threshold and whose health is high; the score weighs the price. **Retreat:** a crawler in a hazard sector under a health
  threshold abandons its plan and walks out (a reflex). Pathfinding needs nothing: the hazard rooms are dead ends.
- **Chronicle:** hurt badly, downed, healed. **Client:** health in the dossier and the resident cards; hazard sectors
  tinted on the map; a Glowstone row in the merchant terminal. **Unity:** its views draw the new sectors from the map
  data and a fallback sprite for the new node kind; nothing else.

## Steps
- [x] 1. Map data: `SectorHazard`, the two sectors, walls, doors, nodes, labels; map validators (Floor 1 checks, walkability,
      doors 4 cells wide).
- [x] 2. Health, damage, downed and limping, healing (bunks, herbs); save fields; validators; chronicle.
- [x] 3. Glowstone in the economy: item kind, stall price and stock, the caravan's premium, the price command; the soak's
      profiles keep their bands (re-measure).
- [x] 4. Cognition: hazard nodes for the bold, retreat reflex, herb use; the 23-seed gate and the soak hold.
- [x] 5. Live scenario (G2's DoD) headless; client: health, tint, merchant row; tests.
- [x] 6. Golden hashes (Unity), references (pinned image), Unity PlayMode and soak; docs; PR; **stop for the producer's look**.

## Definition of Done (epic §3, G2)
Headless, on the real outpost, nothing injected: a bold crawler enters a hazard sector on its own, is hurt, retreats or is
downed, and recovers in the bunks, sooner with a herb; a cautious crawler stays out. The residents' day still holds on the
23 gate seeds; the soak keeps its bands.

# Simpiens: Game Design Proposal

> Product vision and player-facing design (rank 4 in [ROADMAP §0](ROADMAP.md)). Status, scope numbers and sequencing are the roadmap's.

## 1. Executive Summary & Core Game Loop

**Simpiens** is an indirect-control simulation where players operate the support infrastructure—merchant stalls, artisan workshops, and guildhalls—fueling an autonomous adventurer ecosystem. Rather than directly piloting heroes or micromanaging survival pawns, the player functions as a logistical architect within a living megadungeon settlement, exerting influence through contract bounties, economic leverage, and interpersonal goodwill.

```
[Post Bounties / Stock Gear] 
           │
           ▼
[Autonomous Agents Clear Sectors & Harvest Salvage]
           │
           ▼
[Agents Trade Loot at Player Stall ➔ Mutual Trust Grows]
           │
           ▼
[Recruit Trusted Agents to Guild ➔ Outfit Dedicated Parties]
           │
           ▼
[Unlock Floor Descent ➔ Relocate Guild Deeper]

```

* **The Staged Progression:** The player begins with a modest roadside trading post on Floor 1, selling basic provisions to drifters. By fulfilling supply contracts and discovering map sectors, the business expands into an integrated Guildhall that hires non-combat artisans, crafts specialized armaments, and contracts persistent adventurer parties.
* **Platforms and presentation (producer, 2026-10-18):** the MVP is a Windows and Linux desktop game; Android and iOS phones (portrait) follow it, consoles only if mobile goes well, no macOS or web. The world is shown in a fixed isometric view, with cutscenes in screen space (hard cutscenes pause the game; dynamic ones play over it). Art starts as generated placeholders and grows to at most 64×64 px sprites; the target is 30 fps. The client is built on KNI ([Epic CLIENT](epics/epic-client.md)).
* **Session Design:** The core game runs as a single-player offline experience with full simulation clock controls (pause, 1x, 5x, 10x), built on deterministic logical ticks. **Multiplayer is a required post-MVP feature**: the MVP ships single-player, but the architecture must allow an authoritative client-server runtime to be added later (binding constraints: `docs/ROADMAP.md` §1A.1).

---

## 2. Systems: Indirect Agency, Socials, & Narrative

Player agency operates strictly through environmental and economic incentives that interface with the agents' existing utility evaluators.

* **Bounty Contracts as Utility Injections:** Posting a bounty (e.g., "Scout Sector B-4" or "Harvest Iron Ore") injects a high-scoring directive into the public evaluation pool. Unaffiliated agents claim or ignore tasks based on their personal traits, risk tolerance, and reward sizing.
* **The Social Conversion Pipeline:** Agents begin as unaligned independents. By offering trade discounts, stocking preferred items, and posting fair bounties, the player accumulates dyadic **Trust** and **Warmth**. Once loyalty thresholds are met, the player formally recruits the agent into their **Guild Group**, gaining the authority to organize dedicated 4-man parties and dictate target expeditions.
* **Relationships can sour (producer, 2026-11-04):** the pipeline runs both ways. Guild members and artisans have needs and feelings; overworking or not paying them costs trust and affection. Past a threshold they quit: today on low trust or unpaid wages at a caravan visit; in time on affection too, at any moment, with thresholds the design sets per role.
* **Sector Observation & LLM Chronicle:** Sectors remain shrouded under Fog of War until an agent completes a scouting contract. Cleared sectors permit direct camera observation of simulated micro-encounters (campfires, node disputes, combat). A background Model Context Protocol (MCP) narrator parses deterministic `MemoryLedger` events into dynamic tavern rumors and newspaper chronicles.

---

## 3. Floor Progression: The Megadungeon Descent

To avoid punishing offline absences while preserving tense roguelike stakes, progression follows a **Stage-Based Relocation Loop** rather than an arbitrary real-time floor collapse.

| Dimension | Upper Floors (Early Game) | Deeper Floors (Late Game) |
| --- | --- | --- |
| **Dungeon Ecosystem** | Abundant baseline food, minor vermin, low-value salvage. | Lethal mob packs, scarce provisions, high-tier crafting ores. |
| **Agent Demographics** | Novice wanderers, high risk aversion, weak social cohesion. | Specialized veterans, tight party bonds, lethal rivalries. |
| **Player Basecamp** | Fragile market stall; reliant on wandering customer traffic. | Multi-room fortified guildhall housing personal artisans and barracks. |

Advancing to the next floor requires outfitting a party capable of defeating a sector boss guarding the descent gate. Once breached, the player packs up their enterprise and establishes a new forward operating base on the lower floor—preserving accumulated meta-renown, player crafting recipes, and veteran roster contracts, but resetting physical room layouts against escalated environmental pressure.

---

## 4. MVP Scope & Production Roadmap

The initial release must strictly isolate the transaction-and-bounty loop on a single floor before expanding into complex multi-room base building.

* **Phase 1: The Outpost Loop (MVP):** A single connected outpost floor (Floor 1). The sector count is data, not a target (producer, 2026-10-11; see `docs/ROADMAP.md`). The player manages a single trade stall: setting buy/sell prices for harvested materials and posting two bounty types (Scout Sector, Gather Resource).
* **Phase 2: Roster & Party Formation:** Adventurers spend earnings at the player stall, generating personal affinity. Players spend accumulated gold and influence to recruit up to four persistent crawlers, dispatching them into high-hazard sectors as a cohesive `Group`.
* **Phase 3: Base Expansion & Staffing:** Upgrading the outpost into a multi-slot facility (*Fallout Shelter* style), hiring non-combat artisans to process raw materials into weapons, armor, and potions. In the long run the facility is a building the player places and grows (§5).
* **Phase 4: The Gatekeeper Descent:** Procedural floor boss integration, descent gate mechanics, and the persistent campaign migration loop.

---

## 5. Long-Term Direction: Where the Player Builds (producer, 2026-11-04)

This is a vision, not current scope. The roadmap schedules none of it yet. New work should aim toward it and avoid choices that would block it, as [ROADMAP §1A.1](ROADMAP.md) does for multiplayer. It refines §1's staged progression: the shop and the guildhall are two buildings, each placed and grown by the player.

* **Three kinds of zone.** Every part of a floor is one of:
  * **danger zones**, which hurt whoever stays (today's hazard sectors);
  * **neutral zones**, where crawlers live, work and pass through (today's other sectors);
  * **safe zones**, still to come, where the player may build.
* **The player picks the site.** The shop goes in any safe zone the player chooses, and so does the guildhall, each where the player wants it rather than where the map puts it.
* **Each grows as one building.**
  * The shop and its craft stations (the forge, the still, and later ones) grow together as a single building.
  * The guildhall grows as a separate building, with its barracks and muster yard.
  * Growth replaces Phase 3's fixed slots.
* **Rivals as a sink.** The player's shop and guild will compete with other shops and guilds for the crawlers' trade, trust and service. That competition is a future sink and keeps pressure on prices and wages. Rivals could be simulated businesses or, once multiplayer exists, other players' shops and guilds.
* **The descent fits it.** The relocation loop (§3) already resets room layouts on a new floor. Setting up on a lower floor is the same act as placing the first building: pick a safe zone there and build.

**What the work in between keeps in mind:**
* **Part 2's rooms are stand-ins.** [Epic 10 Part 2](epics/epic-10-part-2.md) puts the workshop row and the guildhall on Floor 1 as rooms fixed by the map (its D4). They hold the place of the placed buildings to come.
* **Building state belongs to the building, not the map.** What crafting, hiring and the guild need from a building stays in the building's own state: built or not, its stations, its crew, its orders. When placement arrives, a station's place moves from map data into that saved state, and nothing else should have to change.
* **Facilities and safe zones are different things.** Part 2's facility sectors (its D7: nobody explores them, no bounty targets them) are what a placed building's footprint becomes. A safe zone is where a building may stand.

**Open questions, for when this is designed:**
* What makes a zone safe: the map alone, or something the player earns (charted, cleared, held)? Can a safe zone turn unsafe?
* How does a building grow: by footprint cells or by rooms as modules? What limits it: gold, space, the zone's size?
* Who are the rivals: simulated businesses on the same floor, other players, or both? What do they compete for first: prices, recruits, or the caravan?

---

## 6. Open Design Questions

* ~~**Economic constraint & Faucets/Sinks:** What specific economic constraint should prevent the player from immediately outfitting every crawler with top-tier gear in the MVP: strict local material scarcity, or the adventurer's own willingness to spend personal gold?~~ **Answered 2026-10-15** by the economy model ([docs/economy-model.md](economy-model.md) §3): the scarcity lever is the crawlers' own willingness to trade, judged by each crawler's valuation; faucets and sinks are named in the ledger (§2). Gear-gated scarcity comes with Epic 10 G3.
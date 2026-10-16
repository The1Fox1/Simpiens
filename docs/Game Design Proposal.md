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
* **Session Design:** The core game runs as a single-player offline experience with full simulation clock controls (pause, 1x, 5x, 10x), built on deterministic logical ticks. **Multiplayer is a required post-MVP feature**: the MVP ships single-player, but the architecture must allow an authoritative client-server runtime to be added later (binding constraints: `docs/ROADMAP.md` §1A.1).

---

## 2. Systems: Indirect Agency, Socials, & Narrative

Player agency operates strictly through environmental and economic incentives that interface with the agents' existing utility evaluators.

* **Bounty Contracts as Utility Injections:** Posting a bounty (e.g., "Scout Sector B-4" or "Harvest Iron Ore") injects a high-scoring directive into the public evaluation pool. Unaffiliated agents claim or ignore tasks based on their personal traits, risk tolerance, and reward sizing.
* **The Social Conversion Pipeline:** Agents begin as unaligned independents. By offering trade discounts, stocking preferred items, and posting fair bounties, the player accumulates dyadic **Trust** and **Warmth**. Once loyalty thresholds are met, the player formally recruits the agent into their **Guild Group**, gaining the authority to organize dedicated 4-man parties and dictate target expeditions.
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
* **Phase 3: Base Expansion & Staffing:** Upgrading the outpost into a multi-slot facility (*Fallout Shelter* style), hiring non-combat artisans to process raw materials into weapons, armor, and potions.
* **Phase 4: The Gatekeeper Descent:** Procedural floor boss integration, descent gate mechanics, and the persistent campaign migration loop.

---

## 5. Open Design Questions

* **Economic constraint & Faucets/Sinks:** What specific economic constraint should prevent the player from immediately outfitting every crawler with top-tier gear in the MVP: strict local material scarcity, or the adventurer's own willingness to spend personal gold? *(Note: Faucets and sinks fall under Section 3.3 and will be addressed when that section is tackled.)*
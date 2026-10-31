# Epic 10 Part 3: the descent (draft plan)

Working document (AGENTS.md, Documentation Protocol 6), one of Epic 10's plans ([index](epic10_plan.md)). Written 2026-10-29 at the producer's request.

**Part 3 is QUEUED:** ROADMAP Milestone 4, Gatekeeper Descent & Floor Relocation, with Epic UI-5. Epic 11 shares the milestone as an epic of its own. As with Part 2, this plan is both the draft and the phased plan; once the producer has answered §3, §1–§4 move into the epic.

- **Sources:**
  - **The roadmap's Milestone 4:** "Boss-guarded descent gate breach, roguelike forward operating base relocation loop (via MemoryPack state snapshotting)".
  - **The game design, §3:** "Advancing to the next floor requires outfitting a party capable of defeating a sector boss guarding the descent gate". Then the player moves the outpost down, "preserving accumulated meta-renown, player crafting recipes, and veteran roster contracts, but resetting physical room layouts against escalated environmental pressure".
    - Deeper floors bring "lethal mob packs, scarce provisions, high-tier crafting ores".
    - Phase 4: "procedural floor boss integration, descent gate mechanics, and the persistent campaign migration loop".
  - **Epic 10 §5:** the descent gate, its boss and moving the outpost; creatures and combat; a weapon.
  - **Part 1's deferrals** (the G6 plan, §4): carrying the downed; expeditions into uncharted sectors; loot on hauls.
  - **Epic 8's intent:** "Directives will drive squad-level combat flanking, focus-fire, and tactical retreat."
  - **The refactor plan:** A*'s 16,384-cell cap (R1 refuses a map past it); a `WorldState` wider than 64 facts, left to "the epic that plans the 61st fact".
- **Starts:** after Part 2's close-out.
- **Estimate:** 16–23 agent days: L0 2–3, L1 3–4, L2 3–4, L3 2–3, L4 3–4, L5 2–3, L6 1–2. As rough as Part 2's; L0 measures first.
- **The producer's stops:**
  - answering §3, which is the go;
  - a look at L2, the first fights;
  - a play session at L6, which closes Epic 10.

## 0. What Parts 1 and 2 hand over (checked at L0)
- **Groups:** many-to-many. The guild holds up to eight, with two parties out at once; expeditions are directives with a retreat rule.
- **The workshops:** the forge and the still, with grades and repair; gear for each hazard; draughts.
- **The map:** Floor 1 with 13 sectors: the gallery, the workshop row and the guildhall added. Two of them, the Deep Seam and the Overgrowth, are still charted only by a scout's report.
- **The item system:** the `Attack` and `Defense` stats and the weapon slot, unused; loot tables, not in play.
- **The planner:** about 48 of 64 facts used.

## 1. Goal
**The player promise:** find the gate below, break what guards it, and move the whole outpost down a floor, keeping the guild you built.

What Parts 1 and 2 leave undone:
- **The floor ends.** Nothing lies below, and nothing threatens a party but the air and the rock.
- **The guild has nothing to fight,** so weapons have no use.
- **The uncharted sectors** are only a scouting bounty.
- **There is no campaign:** one floor, played for ever.

## 2. The loop
1. **Scout** the floor's uncharted sectors with an expedition: an order to scout.
2. **Clear** what lives there with armed parties. Creatures drop loot.
3. **Find** the descent gate and the gatekeeper that holds it.
4. **Arm and kit** the guild at the forge and the still, and **breach** the gate.
5. **Descend:** the outpost moves to Floor 2 with the guild, the treasury and what its workshops know. Its rooms are built again.
6. **Floor 2 presses harder:** scarcer food, richer ore, packs.

**The new verbs:** order a scout, order a clearing, breach, descend.

## 3. Questions for the producer (defaults; answers by exception)
| # | Question | Default | Alternative |
| :-: | :--- | :--- | :--- |
| Q1 | **Name and home** | "Epic 10, Part 3: The Descent", in `docs/epics/epic-10-part-3.md`, with Epic UI-5 folded in. Epic 11 stays an epic of its own. | — |
| Q2 | **Combat** | Deterministic, on ticks, with no aiming.<br>• **A hit:** a fighter in reach strikes on a cooldown, for its `Attack` less the target's `Defense`. Health works as G2's does.<br>• **A party** fights through its directive: one target at a time (focus fire), with Part 1's retreat rule.<br>• **Order:** hits are resolved on the main thread, in id order. | Encounters resolved by a seeded roll when a party meets a nest |
| Q3 | **Death** | Still none (Part 1's Q3): a crawler at 0 is downed.<br>Deeper, being downed costs more: its worn gear is lost, and it heals for a sim-day before it works again. | Death on Floor 2 and below: the roguelike's stakes |
| Q4 | **Creatures** | **A new kind of entity, not a crawler.** A creature keeps a sector and has health, `Attack`, `Defense` and a short rule: patrol, attack a crawler in reach, flee when hurt.<br>**Where:** vermin nests in Floor 1's uncharted sectors, spawned from the session's seed. A cleared nest comes back after a while. | Creatures as moving hazards: damage by presence, no fight |
| Q5 | **Weapons and armour** | **Weapons** (`Attack`, in the weapon slot) are made at the forge from ore and glowstone; the caravan sells a basic one.<br>**Armour:** protection gear gains `Defense` through the forge's grades. | Weapons from the caravan only |
| Q6 | **Lone crawlers** | A crawler avoids a known nest as it avoids a hazard over its limit. Attacked, it flees, unless it is bold and armed: then it fights back. | Every crawler flees |
| Q7 | **The gatekeeper** | **One boss creature** at the descent gate, in a new sector at the floor's edge, reached through the uncharted sectors.<br>• Much stronger than vermin; it heals between attempts.<br>• Beaten once, it stays beaten. | Procedural bosses (the game design's Phase 4): after Epic 10 |
| Q8 | **Floor 2** | **Authored as map data** (`Floor2Layout`), as Floor 1 is: larger, within A*'s cap, with its own residents and dangers. | Generated floors |
| Q9 | **What moves down** | The game design's list: "meta-renown, player crafting recipes, and veteran roster contracts".<br>**Moves:**<br>• the guild: members, contracts, allegiance and worn gear;<br>• the treasury;<br>• the workshops' recipes and the artisans' practice. The stations are built again at half price;<br>• renown: Floor 2's residents start with some trust toward the player, by how many of Floor 1's residents trusted it.<br>**Stays:** Floor 1's other residents and their settlements, and the stall's stock, sold to the caravan before leaving. | Everything moves, Floor 1's residents included |
| Q10 | **Floors at once** | One. The session holds the floor it is on, and the save records which. | Floors kept running in the background |
| Q11 | **Loot** | Creatures drop items from loot tables (the item refactor's foundation): salvage, and now and then a rare piece. | No drops |
| Q12 | **Shipping and done** | As in Part 1 (its Q8 and Q10) | — |

## 4. Milestones
| Milestone | What the player sees | Core (engine-free simulation) | Client (KNI, Epic UI-5) |
| :--- | :--- | :--- | :--- |
| **L0. Groundwork** | Nothing new on Floor 1 | Measurements; floors as data (in the session, the save and its fingerprint); Floor 2's map authored and validated, not yet reachable; the creature entity; combat's rule, engine-free; the planner widened past 64 facts if Part 3 needs it | Floor 2's art keys (placeholders) |
| **L1. Creatures** | Vermin in the uncharted sectors; a lone crawler that strays in is hurt and flees | Creatures, nests and their rule; crawlers' flight and avoidance (Q6); spawns on the seed | Creatures drawn; hits and flight readable on the map |
| **L2. Armed parties** | The guild, armed, clears a nest together and carries its downed home | Weapons (Q5); the party's fight under its directive (focus fire, retreat); carrying the downed; loot | Weapons in the merchant terminal and the party builder; the fight in the tracker |
| **L3. Scouting and the gate** | A party scouts, finds the gate and beats the gatekeeper | Scouting orders; the gate's sector; the gatekeeper; the breach | The scouting order in the party builder; the gate on the map; a hard cutscene for the breach |
| **L4. The move** | Descend: the outpost moves to Floor 2 with its guild | The Descend command after the breach; the session rebuilt on Floor 2 with what moves (Q9); save and load across the move | The relocation screen (what moves, what stays); the descent's cutscene |
| **L5. Floor 2's pressure** | A harder floor with its own day | Floor 2's economy and dangers tuned; its gate seeds and soak bands | Floor 2's look |
| **L6. The gate** | The campaign, Floor 1 to Floor 2, played | Every scenario; the whole campaign headless | The producer's play session; Epic 10 closes |

**Order:** L0 to L6, in order. The producer looks at L2 and plays L6.

## 5. The milestones, phased
Each step is a commit. After each: a build with 0 warnings and `dotnet test`. Every milestone passes the index's gates and ends in its PR into `main`.

### L0. Groundwork (2–3 days)
- [ ] **L0.1 Docs:**
  - the answered draft into `docs/epics/epic-10-part-3.md` (Q1);
  - the roadmap: Epic 10 Part 3 ACTIVE, Epic UI-5 folded in;
  - the focus file.
- [ ] **L0.2 Measure:**
  - planning cost and the planner's facts in use;
  - the frame time;
  - where Part 2's economy stands.
- [ ] **L0.3 Floors as data:**
  - `FloorDefinition`: a floor's layout and its tuning;
  - the session's floor, which the save's fingerprint gains (a format bump).
- [ ] **L0.4 Floor 2's map:**
  - `Floor2Layout`, authored, with the map validators;
  - R1's A* guard holds;
  - a test session on Floor 2, with residents only, plays its day.
- [ ] **L0.5 The creature and combat's rule:**
  - the creature entity, as plain data;
  - `CombatRules`: reach, cooldown, `Attack` less `Defense`;
  - `CombatValidator`.
- [ ] **L0.6 The planner's room:** if Part 3's facts would pass 60, widen `WorldState`, in a commit of its own, with the golden hashes unchanged.
- **DoD:**
  - Floor 1's 23 gate seeds and the soak are unchanged;
  - Floor 2's day holds on its own gate seeds, with residents only.

### L1. Creatures (3–4 days)
- [ ] **L1.1 Nests:**
  - map data in the uncharted sectors;
  - spawns on the seed, and their comeback time.
- [ ] **L1.2 The creature's rule:**
  - patrol its sector, attack a crawler in reach, flee when hurt;
  - on the main thread, with no planner.
- [ ] **L1.3 Crawlers:**
  - flight, a reflex beside G2's retreat;
  - avoiding known nests, as `WouldVenture` avoids hazards;
  - a scout's report charts a nest.
- [ ] **L1.4 The client:** creatures' art rows (placeholders), with hits readable.
- **DoD `VerminDriveTheLoneAway`:** on the default seed's day:
  - a lone crawler that strays into a nest's sector is hurt and flees;
  - nobody else enters a known nest;
  - the day's checks hold.

### L2. Armed parties (3–4 days), then the producer's look
- [ ] **L2.1 Weapons (Q5):**
  - the items, the forge's recipes and the caravan's basic weapon;
  - `Attack` in combat;
  - `Defense` in the forge's grades.
- [ ] **L2.2 The party's fight:**
  - the expedition's work stage becomes clearing: focus fire on one creature at a time, the directive's target;
  - the retreat rule.
- [ ] **L2.3 Carrying the downed** (Part 1's deferral): a member carries a downed member home, at half pace.
- [ ] **L2.4 Loot** (Q11): creature drops from loot tables.
- [ ] **L2.5 The client:** weapons in the merchant terminal and the party builder; the fight in the tracker.
- **DoD `AnArmedPartyClearsANest`:**
  - the scripted player arms four and orders a clearing;
  - they fight together, clear the nest, carry anyone downed home and bring back loot;
  - the ledger balances.
- **Stop for the producer's look** before L3.

### L3. Scouting and the gate (2–3 days)
- [ ] **L3.1 Scouting orders** (Part 1's deferral): an expedition into an uncharted sector charts it with a scout's report.
- [ ] **L3.2 The gate:**
  - its sector, as map data at the floor's edge;
  - the gatekeeper (Q7): its health, healing between attempts, beaten once.
- [ ] **L3.3 The breach:**
  - beating the gatekeeper opens the gate, a world flag that is saved;
  - the chronicle tells it, and a hard cutscene plays.
- **DoD `ThePartyFindsAndBreachesTheGate`:** on the default seed, the scripted player scouts, finds the gate, arms and kits the guild, and breaches the gate. More than one attempt is allowed; the bound is set at L3's first measurement.

### L4. The move (3–4 days)
- [ ] **L4.1 The Descend command,** after the breach:
  - what moves (Q9) is captured as a carried record, saved with MemoryPack, as the roadmap says;
  - the session is rebuilt on Floor 2 as a respawn is, with the carried record in place of part of `Populate`, in a new session epoch.
- [ ] **L4.2 Arrival:**
  - Floor 2's residents spawn, with renown;
  - the guild arrives with its contracts and gear, and the treasury with it;
  - stations can be built again at half price.
- [ ] **L4.3 Save and load across the move:** a save on Floor 2 loads on Floor 2, and a save from before the move still loads on Floor 1.
- [ ] **L4.4 The client:** the relocation screen and the descent's cutscene.
- **DoD `TheOutpostMovesDown`:** after the breach, the scripted player descends. On Floor 2:
  - the guild's members, contracts and gear are there;
  - the treasury has moved, and the artisans' practice is kept;
  - Floor 2's day passes its checks.

### L5. Floor 2's pressure (2–3 days)
- [ ] **L5.1 Floor 2's tuning:** scarcer food (fewer plots), richer ore, a pack of creatures, its drifters.
- [ ] **L5.2 Floor 2's gate seeds and soak bands,** set with the producer.
- **DoD `TheSecondFloorsDayHolds`:** on Floor 2's gate seeds:
  - nobody starves, and the ledger balances;
  - crawlers complete their cycles;
  - the guild works.

### L6. The gate (1–2 days)
- [ ] **L6.1 `TheCampaignPlaysThrough`:** from Floor 1's start, through the breach and the move, to Floor 2's first day, headless.
- [ ] **L6.2 Saves across the move,** byte for byte.
- [ ] **L6.3 Seeds and soaks on both floors.**
- [ ] **L6.4 The producer's play session.**
- [ ] **L6.5 Epic 10's close-out:**
  - **the roadmap:** Epic 10 COMPLETED; Milestone 4's Epic 10 share and Epic UI-5 delivered;
  - **the epic docs, the focus file, `architecture_history.md`, the economy model and AGENTS.md;**
  - every Epic 10 plan is deleted, the index too.

## 6. Risks
| Risk | What we do |
| :--- | :--- |
| Creatures and fights add entities and decisions, costing planning and frame time. | Creatures follow a short rule on the main thread, not the planner. Measure at L1 and L2; the refactor's R6 (planners per worker) if needed. |
| Fights must be deterministic. | Resolved on the main thread, in id order, on ticks. |
| The move rebuilds the world, and entity ids repeat per seed. | A new session epoch, as a respawn has. The save's fingerprint gains the floor. |
| Floor 2 comes near A*'s cap. | R1 refuses a map past it. Size the searcher's buffers from the map if Floor 2 needs more. |
| The planner's 64 facts run out. | L0.6 widens `WorldState` first, if needed. |
| Death, if the producer picks Q3's alternative, changes trust, the guild and the save. | It becomes a milestone of its own, between L4 and L5. |
| Epic 11 shares the milestone. | Separate branches and PRs. They share only the chronicle. |

## 7. Deferred (proposed)
| Left out | Owner |
| :--- | :--- |
| Floors past the second; procedural floors and bosses | After Epic 10. Generated maps want the map designer (Epic CLIENT). |
| Multiplayer | After the MVP; this part keeps it possible (ROADMAP §1A.1) |
| Tavern rumours written by a language model | Epic 11 |
| Art beyond the generated placeholders | The producer's art direction |

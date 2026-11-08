# Epic 10 Part 3: creatures, combat and the gatekeeper (draft plan)

Working document (AGENTS.md, Documentation Protocol 6), one of Epic 10's plans ([index](epic10_plan.md)). First written 2026-10-29 as "the descent". **Re-scoped 2026-11-07 by the producer:** "I want to re evaluate what we are tackling in epic10_part3, The focus should be combat system, equipment improvements, death, loot, creatures, bosses / gate keepers. We should not focus on floor 2 at all yet". This draft replaces the earlier one, which git keeps.

**Part 3 is QUEUED.** As with Part 2, this plan is both the draft and the phased plan. Once the producer has answered §4, §1–§4 move into the epic doc.

- **Sources:**
  - **The producer's re-scope** (above): combat, equipment, death, loot, creatures, bosses and gatekeepers. Floor 2 is out.
  - **The game design:**
    - §3: the upper floors have "minor vermin, low-value salvage", and the deeper floors "lethal mob packs". "Advancing to the next floor requires outfitting a party capable of defeating a sector boss guarding the descent gate."
    - §2: "Cleared sectors permit direct camera observation of simulated micro-encounters (campfires, node disputes, combat)."
  - **Epic 8's intent:** "Directives will drive squad-level combat flanking, focus-fire, and tactical retreat."
  - **What Parts 1 and 2 left to Part 3:** a weapon (`Attack` and `Defense` are unused); carrying a downed member; loot tables in play; rarity in the client (Part 1 §5); armour against creatures; combat and death (Part 2 §5).
  - **The guiding lights:**
    - **Majesty:** heroes fight because a reward draws them. The player flags a lair, and the bold answer. They buy their own arms and potions, flee when hurt, and grow into veterans.
    - **RimWorld:** fights are short, readable and dangerous. The downed bleed out unless someone carries them home. Animals drop leather that becomes armour, and a death is felt by those who knew the dead.
- **What left Part 3:** Floor 2, floors as data, the move and what it carries, and Floor 2's pressure (the old L0.3–L0.4, L4 and L5). They go to a later part, the descent, which is not drafted (Q1). Scouting as a guild order (the old L3.1) goes with them; scout bounties already chart sectors.
- **Starts:** after Part 2's close-out (its play session, W6.5).
- **Estimate:** 22–31 agent days: K0 2–3, K1 3–4, K2 3–4, K3 2–3, K4 3–4, K5 2–3, K6 3–4, K7 3–4, K8 1–2. This is rough, as Part 2's first estimate was; K0 measures first.
- **Complexity** ([rubric](rules/complexity-grading.md)): **Very Complex** as a whole. It is epic-sized, its systems have no precedent (combat, creatures, death) and §4 is unanswered.
  - K1, K4, K6 and K7 are Very Complex: each is split below, and the developer supervises its steps stamped Very Complex.
  - K0 holds one supervised step (K0.3).
  - K2, K3, K5 and K8 are Possibly Complex.
- **The producer's stops:**
  - answering §4, which is the go;
  - a look at K2, the first fights;
  - a look at K4, death;
  - a play session at K8.

## 0. What Parts 1 and 2 hand over (checked at K0)
- **Health:** a crawler has 100. A hazard sector takes health by kind, through worn gear. At 0 a crawler is downed and limps to a bunk at 40% of its speed, getting up at 60. Nobody dies (Part 1's Q3).
- **Being hurt shows:** `AgentState.HurtThisTick` and the blinking red coat (S3, D71) work for any damage. Combat feeds them as it is.
- **Gear:** three equipment slots: protection, weapon and tool.
  - Protection holds one of hazard gear, a respirator or a hard hat.
  - The weapon slot and the `Attack` and `Defense` stats are unused.
  - `WearRule.PerUse` is reserved for "a weapon per hit".
- **The workshops:** the forge and the still. They have grades (Standard, Fine, Masterwork) from practice and repair worn pieces at half their inputs. The stall's goods table holds 10 goods; 9 are used.
- **Loot:** loot tables and `LootRoller`, deterministic on `SimRandom` (item system I7), are not wired into play. `ItemRarity` has five tiers, and the sprite kit already draws icon plates with rarity rims.
- **Expeditions:** muster, march, work, return, deliver, with a retreat rule. Two parties of up to four can be out at once, never in the same sector. Draughts heal 80 at once.
- **Leaving the world:** a drifter is removed at the end of the tick (`SessionHost`, `CrawlerDeparted`), and `DepartedCrawler` records who left.
- **The map:** Floor 1 has 13 sectors. Two of them, the Deep Seam and the Overgrowth, are charted only by a scout's report.
- **The planner:** 41 of 64 facts used.

## 1. Goal
**The player promise:** the deep sectors bite back. Arm the outpost, put a price on what lurks there, send the guild against the brood and break the gatekeeper. Any crawler you send can die.

What Parts 1 and 2 leave undone:
- **Nothing threatens a crawler but the air and the rock.** Hazards hurt but never kill, so risk is a cost in time, never a loss.
- **The forge has nothing to arm.** Weapons have no use, and armour only means hazard gear.
- **Loot is a foundation with no floor on it:** the haul is always glowstone.
- **The floor has no climax:** no boss, no gate, nothing to work toward past a full guild.

## 2. The loop
1. **Creatures hold sectors.** Vermin nest in the Deep Seam and the Overgrowth. A lone crawler who strays in is hurt and flees, and the cautious stop going. The salvage there dries up: pressure on the stall.
2. **The player answers indirectly:**
   - stock weapons and armour at the stall, so that the bold arm themselves;
   - post a **Hunt bounty** on a lair (Majesty's flag), which the bold and armed take;
   - send an armed guild party with a **Clear** order.
3. **Kills drop loot:** hides and chitin, sometimes salvage, now and then a rare piece. The stall buys them, and the forge turns them into better armour.
4. **Fighters grow:** members who survive fights become seasoned, then veterans.
5. **The brood's Matriarch** teaches the guild that a boss needs a party, draughts and armour.
6. **The gatekeeper** holds the sealed gate at the floor's edge. Breaking it is Part 3's climax; the way down stays dark until the descent part.
7. **Death is the price throughout.** A fallen crawler who isn't carried out of danger bleeds out. Friends grieve, the guild loses a contract, and the body lies where it fell with everything it carried.

**The new verbs:** stock arms, post a hunt, order a clearing, carry the fallen (the party does it), and breach.

## 3. Positions this draft takes (argued, for the producer to overturn)
- **The player never commands a fight.** Fights come from bounties, orders and stock, as everything else does. A direct "attack this" button would make Simpiens a tactics game, and the game design's whole premise is indirect agency.
- **Combat is watched, so it runs in physical time.**
  - Swings, lunges and flight are motion: ticks and seconds, as walking is ([time-model.md](../docs/time-model.md)).
  - What lasts across the day stays in the day's time: bleeding out, lairs refilling, a boss healing between attempts, and a fighter's growth.
  - An encounter resolved by a hidden roll was rejected: the design promises "direct camera observation" of combat.
- **Creatures are not crawlers.** A creature has a short rule on the main thread, not a planner, needs, memory or a purse. RimWorld's animals are simple too, and fifteen to twenty-five creatures must cost the tick almost nothing.
- **Deaths come from choices.** A lone crawler avoids lairs it knows of, as it avoids a hazard past its limit. So deaths come from the player's orders, from bounties the bold chose to take, and from the fog (a lair nobody has charted). A careful player can lose no one, and a reckless one loses people. Without that, death is noise, not stakes.
- **Loot feeds the workshops, not only the purse.** Hides and chitin become armour at the forge. Each kill pays twice: in what the stall sells, and in what the forge can make.

## 4. Questions for the producer (defaults; answers by exception)
| # | Question | Default | Alternative |
| :-: | :--- | :--- | :--- |
| Q1 | **Name, home and the milestone** | "Epic 10, Part 3: Creatures, Combat and the Gatekeeper", in `docs/epics/epic-10-part-3.md`, with Epic UI-5 folded in. **Milestone 4 splits:**<br>• **Milestone 4**, "Creatures, Combat & the Gatekeeper": Part 3, UI-5 and Epic 11 (deaths and battles give the chronicle its best material).<br>• **Milestone 5**, "Floor Relocation": Part 4, the descent (Floor 2 and the move), QUEUED and not drafted.<br>Epic 10 closes after Part 4. | Keep Milestone 4 whole, with the relocation as its second half |
| Q2 | **Combat** | On ticks, in physical time, melee only, with no aiming:<br>• **A swing:** a fighter in reach swings on its weapon's interval (about 1.5 s). A seeded roll (`SimRandom`, its own salt) decides hit or miss, about 70% to begin with.<br>• **A hit** does the weapon's `Attack`, cut by the target's `Defense` on a curve that never reaches zero (`Defense / (Defense + 10)` to begin with).<br>• **Order:** hits are resolved on the main thread, in one pass after movement, in entity-id order.<br>• **Unarmed**, a crawler hits for 2. | (a) A crossbow at the forge as well: ranged combat needs line of sight and kiting, about +2 days. (b) Encounters resolved by a seeded roll, unseen |
| Q3 | **Death** | **Yes, and permanent.**<br>• At 0 a crawler is downed (as today).<br>• **Downed in danger,** within a hostile creature's reach or in a hazard sector, it bleeds out over 2 game hours.<br>• **The clock stops** when it leaves danger (it limps, or a party member carries it), drinks a draught or is given one.<br>• **When the clock runs out, it dies.**<br>• Creatures turn from a downed crawler to one still standing. | (a) Only creatures kill; a hazard only downs. (b) Majesty's resurrection: the dead brought back for gold at a shrine (deferred by default) |
| Q4 | **What a death costs** | • **The body:** its pack, worn gear and purse lie where it fell, as a loot pile that anyone may recover.<br>• **The guild:** the dead member's contract and kept bunk end, and its party is one short.<br>• **The living:** friends (warmth 30 or more) grieve, through a memory event and a mood. If a member dies under the guild's orders, the other members' trust toward the patron falls.<br>• **Memory:** the chronicle tells it, and the departed list keeps the dead with their cause.<br>• **A newcomer** settles at the Road Gate 2 days after a resident dies, with a new look and name, so the outpost doesn't bleed out over a campaign. | No newcomers: the population only shrinks |
| Q5 | **Creatures** | • **A new kind of entity,** plain data, saved. Its rule: rest at its lair, roam its sector, chase what it sees within a leash, strike in reach, flee when hurt if its kind flees, go home and heal over the day.<br>• **A catalogue of kinds,** like the item catalogue, as data: health, `Attack`, `Defense`, speed, reach, swing interval, sight, leash, flight, loot table and art.<br>• **Three kinds** (placeholder names):<br>&nbsp;&nbsp;– **burrow rats**, weak, in packs of three or four, and they flee;<br>&nbsp;&nbsp;– **stone beetles**, armoured and slow, where a weapon matters;<br>&nbsp;&nbsp;– **cave spinners**, on the gate's approach (K7).<br>• **Lairs are map data:** rats in the Overgrowth, beetles in the Deep Seam. They are spawned from the session's seed, and a cleared lair refills after 2 game days.<br>• **No raids:** creatures keep to their sectors. | (a) Creatures as moving hazards, hurting by presence with no fight. (b) Raids on the outpost at night: later |
| Q6 | **Lone crawlers** | • A crawler judges a **known** lair as it judges a hazard: it avoids one past what it would brave alone.<br>• **Attacked,** it flees (a reflex beside retreat), unless it is bold and armed: then it fights back.<br>• **The bold buy their own weapons** at the stall when their purse spares it, as residents who mine buy pickaxes (D39).<br>• **A scout's report** charts the lairs of the sector it charts. | Every crawler flees; only the guild fights |
| Q7 | **Hunt bounties** (Majesty's flag) | • **A third bounty kind** beside Scout and Gather: "Hunt", on a charted lair, paid from escrow when the lair is empty.<br>• **Up to three claimants,** the bold and armed. The reward is shared by the damage each one dealt.<br>• If the lair refills before it is cleared, the bounty runs on until it expires. | No hunt bounties: only the guild fights |
| Q8 | **The guild's Clear order** | • **A new order** for an expedition: a lair as the target. Its stages: muster, march, **fight**, return, deliver. The haul is what the party picked up.<br>• **Focus fire:** the directive names one target at a time, the party leader's choice (the nearest, then the weakest).<br>• **Retreat:** the party turns back when half its members are downed, or its health is spent past a line; it carries its downed home at half pace (Part 1's deferral).<br>• Flanking is left for later. | No carrying: the downed limp home alone |
| Q9 | **Veterans** | Fighting is practice, as making is for an artisan (W4):<br>• **The grades:** Green, Seasoned, Veteran, from fights survived and blows landed.<br>• **What a grade gives:** a better hit chance and a little more damage.<br>• **Saved;** shown in the dossier and on the roster.<br>This is the design's "veteran roster contracts". | No growth: gear alone makes a fighter |
| Q10 | **Weapons** | Three, in the weapon slot (placeholder names):<br>• **a cudgel**, which the caravan supplies cheaply;<br>• **an iron blade**, made at the forge from ore;<br>• **a glowstone edge**, made from ore and glowstone: the best.<br>They wear per hit (`WearRule.PerUse`), and the forge's grades and repair apply. | Weapons from the caravan only |
| Q11 | **Armour and slots** | • **Protection splits** into **head** and **body**:<br>&nbsp;&nbsp;– the head takes the hard hat, the respirator (worn as a mask) or an iron helm;<br>&nbsp;&nbsp;– the body takes hazard gear or armour: a ring coat from ore (K2), a leather jerkin from hides and chitin plate (K3).<br>• **A real choice:** a party bound for a lair in a hazard sector picks armour or a hazard suit.<br>• **Rarity:** rare pieces are fixed items with a rarity, dropped by bosses (for example the Matriarch's fang dagger), never random affixes. The client shows rarity's colours (Part 1's deferral). | Keep one protection slot: armour or hazard gear |
| Q12 | **Loot** | • **Piles:** a kill drops a pile at its place, rolled from its kind's loot table: its material (hide, chitin), sometimes salvage, now and then a rare piece. A boss drops a sure trophy and a rare piece.<br>• **Who takes it:** the victors pick it up, a party into its haul and a hunter into its pack. Anyone may claim a pile left behind. Piles fade after a game day.<br>• **The economy:** the stall buys the materials, the caravan exports them, and the forge makes armour from them. | Drops go straight into the killer's pack, with no piles |
| Q13 | **Bosses** | **Two:**<br>• **The Matriarch** heads the rats' warren. She is stronger and calls her brood to her at half health. Slain, she returns after a game week.<br>• **The Gatekeeper** holds the sealed descent gate:<br>&nbsp;&nbsp;– **its lair:** a new sector at the floor's edge, the Old Stair, reached through the Deep Seam and uncharted at the start;<br>&nbsp;&nbsp;– **its fight:** a slam that strikes everyone in reach, and fury at a quarter of its health;<br>&nbsp;&nbsp;– **between attempts:** it heals fully once its lair has been empty for a game hour;<br>&nbsp;&nbsp;– **beaten once,** it stays beaten.<br>• **The breach** is a saved world flag, told by the chronicle and a hard cutscene. Beyond the gate the stairs go down into the dark, for the descent part. | The Gatekeeper only |
| Q14 | **Shipping and done** | As in Parts 1 and 2:<br>• one PR, with a commit per step;<br>• each milestone's DoD is a live scenario on the real session, with nothing injected;<br>• a scripted player acts only through commands. | — |

## 5. Architecture first (AGENTS.md, "Refactor and reevaluate")
Part 3 adds systems rather than rewriting old ones, so no large refactor is needed first. Six seams, laid before the systems that use them, make each later step smaller:
- **A1. One combat rule over two kinds of fighter.**
  - `CombatRules` reads a fighter through a small read model (`Combatant`: health, `Attack`, `Defense`, reach, interval and grade).
  - A crawler's comes from `AgentState` and `CrawlerStats`, and a creature's from its own record.
  - So there is one rule, one validator and one place to tune: no crawler path and creature path that drift apart (K0.3).
- **A2. A crawler leaves the world one way.**
  - A death reuses the drifter's departure: removal at the end of the tick and `CrawlerDeparted`.
  - `DepartedCrawler` gains a cause (left or died).
  - The groups, bunks, contracts and views that already handle a drifter leaving handle the dead the same way. That keeps K4.2's risk in the things that are new: succession and the body (K4.2).
- **A3. Slots by body part before new gear.**
  - Head and body replace protection, and the stall's goods table grows to 16, in one format bump (K0.5).
  - Every weapon and armour after that is a table row, by AGENTS.md's "Adding an item".
- **A4. Creatures tick in a phase of their own,** after the crawlers' runtime and in id order, and every swing is resolved in one pass after movement. Determinism holds, and combat can be tested apart from cognition (K1.2).
- **A5. Items on the ground are one entity:** the loot pile. A kill's drop (K3) and a body (K4) are both piles, so death needs no second mechanism.
- **A6. Kinds as data:** creatures in a catalogue, like items, so the Matriarch and the Gatekeeper are rows with boss traits plus the mechanics no row can hold (K7).

## 6. Milestones
| Milestone | What the player sees | Core (engine-free simulation) | Client (KNI, Epic UI-5) | Complexity |
| :--- | :--- | :--- | :--- | :--- |
| **K0. Groundwork** | Nothing new | Measurements; the combat rule and its read model; the creature catalogue; head and body slots; the goods table at 16 | The new slots in the dossier | Very Complex (K0.3 supervised; the rest Possibly Complex) |
| **K1. Creatures** | Vermin in the Deep Seam and the Overgrowth; a stray is hurt and flees; the cautious keep out | Lairs, spawns, the creature's rule; flight and avoidance; the save | Creatures drawn, striking, coated red when hit, hidden by the fog | Very Complex (split; K1.2 and K1.3 supervised) |
| **K2. Arms and armour** | The bold buy blades and fight back; the forge makes weapons and a ring coat | Weapons, armour, wear per hit, fighting back, outfitting for creatures | Weapons in hand, the merchant terminal, the party builder | Possibly Complex |
| **K3. Loot** | Kills leave piles; hides and chitin sell, and become armour | Piles, drops, picking up, materials and their recipes, rare pieces | Piles on the map; rarity's colours | Possibly Complex |
| **K4. Death** | The fallen bleed out unless carried or dosed; the dead are mourned; a newcomer arrives | Bleeding out; a death; grief and trust; the newcomer; the save | The body and a cairn; the chronicle; the dossier of the dead | Very Complex (split; K4.2 supervised) |
| **K5. Hunting** | The player flags a lair; the bold clear it and are paid | Hunt bounties; the hunters' goals | The bounty board's Hunt | Possibly Complex |
| **K6. The guild goes to war** | An armed party clears a nest, carries its downed home and brings back loot | The Clear order; the party's fight; carrying; veterans | The Clear order and the fight in the tracker | Very Complex (split; K6.2 and K6.3 supervised) |
| **K7. Bosses and the gate** | The Matriarch; the Old Stair; the Gatekeeper broken; the gate breached | The new sector; boss traits and mechanics; the breach | Boss art and health bars; the gate; the breach's cutscene | Very Complex (split; K7.3 supervised) |
| **K8. The gate** | Part 3 played through | Every scenario; seeds; soak bands with combat and deaths | The producer's play session | Possibly Complex |

**Order:** K0 to K8, in order. Loot (K3) comes before death (K4), because a body is a pile.

## 7. The milestones, phased
Each step is a commit. After each: a build with 0 warnings and `dotnet test`. The whole part is one PR into `main` (the index's rules).

### K0. Groundwork (2–3 days)
**Complexity:** Very Complex, for K0.3. The rest is Possibly Complex.
- [ ] **K0.1 Docs:** [Basic]
  - the answered draft into `docs/epics/epic-10-part-3.md` (Q1);
  - the roadmap: Part 3 ACTIVE; Milestone 4's split if Q1 stands;
  - the focus file.
- [ ] **K0.2 Measure:** [Possibly Complex: probes and reading them]
  - planning cost and the planner's facts in use;
  - the frame time;
  - the economy after Part 2's F14 decision;
  - how many residents would venture, and with what purses (weapon prices are set against them, as the pickaxe's were, F9).
- [ ] **K0.3 The combat rule:** [Very Complex, supervised by the developer: the first fight's rule sets the pattern K1–K7 follow]
  - `Combatant`, the read model (A1);
  - `CombatRules`: reach, interval, the hit roll on a new `SimRandomSalt`, mitigation by `Defense`, wear per hit;
  - `CombatValidator`;
  - the golden hashes unchanged: nothing fights yet.
- [ ] **K0.4 The creature catalogue:** [Possibly Complex: a new catalogue with a precedent, `ItemCatalog`]
  - kinds as data (A6), with a validator;
  - no spawns yet.
- [ ] **K0.5 Head and body:** [Possibly Complex: the save format, and outfitting may now give a crawler two pieces]
  - the head and body slots in place of protection (A3);
  - the goods table at 16;
  - one format bump.
  - The golden hashes are re-recorded in their own commit only if outfitting changes behaviour.
- **DoD:** Floor 1's 23 gate seeds and the soak are unchanged, or re-recorded with K0.5's cause named.

### K1. Creatures (3–4 days)
**Complexity:** Very Complex: a new system. Split as below. The developer supervises K1.2 and K1.3.
- [ ] **K1.1 Lairs:** [Possibly Complex: a map change, with golden and renders re-recorded]
  - lairs as map data in the Overgrowth and the Deep Seam;
  - spawns on the seed;
  - refilling on the day's time.
- [ ] **K1.2 The creature's rule:** [Very Complex, supervised by the developer: the creature's tick, its own phase in id order (A4)]
  - rest, roam, chase within the leash, strike, flee, go home, heal;
  - its paths through the existing pathfinder;
  - swings resolved in one pass after movement.
- [ ] **K1.3 Crawlers meet creatures:** [Very Complex, supervised by the developer: a new reflex in the runtime's pre-emption, and cognition]
  - flight, a reflex beside retreat and panic;
  - known lairs judged in `LoneDanger`, as hazards are;
  - a scout's report charts lairs;
  - nobody yet fights back, and nobody dies: a downed crawler limps out, as today.
- [ ] **K1.4 Saves:** [Possibly Complex: a save block] creatures and lairs in the world's block, with the round-trip validators.
- [ ] **K1.5 The client:** [Possibly Complex: new art and renders to look at]
  - `CreatureSprites` (generated placeholders);
  - art rows per view mode, and `EntityVisuals`;
  - a strike pose;
  - the red coat on creatures too;
  - the fog hides them;
  - the inspector shows a creature.
- **DoD `VerminDriveTheLoneAway`:** on the 23 gate seeds' day:
  - a crawler who strays into a lair's reach is hurt and flees;
  - nobody enters a known lair alone;
  - the day's checks hold.

### K2. Arms and armour (3–4 days), then the producer's look
**Complexity:** Possibly Complex: items with a precedent, and one new reflex.
- [ ] **K2.1 Weapons (Q10):** [Possibly Complex: a stat read in combat]
  - the cudgel, the iron blade and the glowstone edge;
  - their recipes, and the caravan's terms;
  - wear per hit; grades and repair.
- [ ] **K2.2 Armour from ore (Q11):** [Basic: item rows and recipes, by AGENTS.md's "Adding an item"] the iron helm and the ring coat.
- [ ] **K2.3 Fighting back:** [Possibly Complex: cognition, a fact and a reflex]
  - the bold and armed fight back;
  - the bold buy a weapon (Q6);
  - the party builder outfits members for creatures as it does for hazards (W3).
- [ ] **K2.4 The client:** [Possibly Complex: Gum UI and worn sprites]
  - weapons in hand (`WornSprites`);
  - the new goods in the merchant terminal and the party builder.
- **DoD `AnArmedCrawlerFightsBack`:** on a gate seed, the scripted player stocks blades; a bold resident buys one, meets vermin, fights and kills one; the day holds.
- **Stop for the producer's look** at the first fights.

### K3. Loot (2–3 days)
**Complexity:** Possibly Complex: rolls already exist; piles are a new entity with saved state.
- [ ] **K3.1 Piles:** [Possibly Complex: a new entity, saved]
  - items on the ground (A5);
  - they fade after a game day.
- [ ] **K3.2 Drops:** [Possibly Complex: deterministic rolls] each kind's loot table, rolled at the kill by `LootRoller`.
- [ ] **K3.3 Picking up:** [Possibly Complex: cognition, a goal and a fact]
  - the victors take their pile;
  - anyone may claim one left behind.
- [ ] **K3.4 Materials:** [Possibly Complex: the economy, and the soak may move]
  - hides and chitin at the stall and the caravan;
  - the leather jerkin and chitin plate at the forge;
  - the rare pieces as items.
- [ ] **K3.5 The client:** [Possibly Complex: art and renders]
  - piles on the map;
  - rarity's colours in the merchant terminal and the dossier.
- **DoD `KillsFeedTheForge`:** a hunter's kill drops hide; the hide is sold at the stall; the forge makes a jerkin from it; a resident wears it.

### K4. Death (3–4 days), then the producer's look
**Complexity:** Very Complex: a crawler leaving the world mid-session touches every system that holds one. Split as below. The developer supervises K4.2.
- [ ] **K4.1 Bleeding out (Q3):** [Possibly Complex: a rule in `Vitality`, saved]
  - the clock while downed in danger;
  - what stops it.
- [ ] **K4.2 A death:** [Very Complex, supervised by the developer: groups and succession, contracts, bunks, parties and expeditions, memory, the ledger]
  - a death goes the drifter's way (A2);
  - the body is a pile with the purse in it, held in a ledger account for gold on the ground;
  - a dead leader is succeeded.
- [ ] **K4.3 The living remember (Q4):** [Possibly Complex: memory events, affinity and mood]
  - grief;
  - trust toward the patron after a death under orders;
  - the chronicle;
  - the departed list's cause.
- [ ] **K4.4 A newcomer:** [Possibly Complex: a population rule on the seed] 2 days after a resident's death.
- [ ] **K4.5 Saves:** [Possibly Complex: format] the dead, the bleeding clock and gold on the ground, with the round-trip validators.
- [ ] **K4.6 The client:** [Possibly Complex: art, renders, a dynamic scene]
  - the body and a cairn;
  - the chronicle's lines;
  - the dossier of the dead (portrait greyed);
  - a dynamic scene when a friend sees a death.
- **DoD:**
  - **`DeathHasItsCost`:** the scripted player sends an under-armed party against beetles. A member falls and isn't carried, and it dies. Its friends grieve, the guild loses its contract, the body's pile holds its gear and purse, and a newcomer arrives. The ledger balances.
  - **`TheCarefulLoseNoOne`:** on the 23 gate seeds, with no player action, deaths stay within a band set with the producer (zero is the target).
- **Stop for the producer's look** at death.

### K5. Hunting (2–3 days)
**Complexity:** Possibly Complex: a new bounty kind with two precedents, Scout and Gather.
- [ ] **K5.1 Hunt bounties (Q7):** [Possibly Complex: a command, escrow, saved]
  - posted on a charted lair;
  - claimed by up to three;
  - paid by damage dealt when the lair is empty.
- [ ] **K5.2 The hunters:** [Possibly Complex: cognition, a goal, an action and facts] claim, go, fight in the lair, and are paid.
- [ ] **K5.3 The client:** [Possibly Complex: Gum UI] the bounty board's Hunt, with charted lairs as targets.
- **DoD `AHuntBountyClearsALair`:** the scripted player posts a hunt on the rats; bold, armed residents claim it, clear the lair and are paid. The ledger balances.

### K6. The guild goes to war (3–4 days)
**Complexity:** Very Complex: a party's fight and carrying the downed are both new. Split as below. The developer supervises K6.2 and K6.3.
- [ ] **K6.1 The Clear order (Q8):** [Possibly Complex: a new expedition kind on G5's stages]
  - a lair as the target;
  - the fight stage;
  - the haul as what was picked up.
- [ ] **K6.2 The party's fight:** [Very Complex, supervised by the developer: focus fire through the directive, and the retreat rule]
- [ ] **K6.3 Carrying the downed:** [Very Complex, supervised by the developer: two crawlers' movement coupled, and saved] Part 1's deferral, at half pace.
- [ ] **K6.4 Veterans (Q9):** [Possibly Complex: saved state, with W4's practice as the precedent]
- [ ] **K6.5 The client:** [Possibly Complex: Gum UI]
  - the Clear order in the party builder;
  - the fight in the tracker: the party's health and its foe's;
  - grades on the roster.
- **DoD `AnArmedPartyClearsANest`:** the scripted player arms four and orders a clearing. They fight together, clear the nest, carry anyone downed home and bring back loot. The ledger balances.

### K7. Bosses and the gate (3–4 days)
**Complexity:** Very Complex: boss mechanics are new. Split as below. The developer supervises K7.3.
- [ ] **K7.1 The Old Stair:** [Possibly Complex: a map change, with golden and renders re-recorded]
  - the new sector at the floor's edge, uncharted at the start;
  - the sealed gate;
  - the spinners' lair on its approach.
- [ ] **K7.2 The Matriarch (Q13):** [Possibly Complex: a kind with boss traits]
  - she calls her brood to her;
  - she returns after a week.
- [ ] **K7.3 The Gatekeeper:** [Very Complex, supervised by the developer: the slam, the fury, healing between attempts, beaten once, saved]
- [ ] **K7.4 The breach:** [Possibly Complex]
  - a saved world flag;
  - the chronicle;
  - the stairs beyond, dark.
- [ ] **K7.5 The client:** [Possibly Complex: art, renders, a hard cutscene]
  - boss sprites (up to 64×64) and health bars;
  - the gate on the map;
  - the breach's cutscene.
- **DoD `TheGuildBreaksTheGatekeeper`:** on the default seed the scripted player scouts the Old Stair, arms and kits the guild, and breaks the Gatekeeper. More than one attempt is allowed; the bound is set at K7's first measurement.

### K8. The gate (1–2 days)
**Complexity:** Possibly Complex: the whole part's scenarios and bands; the close-out is Basic.
- [ ] **K8.1 Every scenario together,** [Possibly Complex] with a save mid-fight and with a bleeding crawler, loaded byte for byte.
- [ ] **K8.2 The soak's `hunting` profile:** [Possibly Complex: bands set with the producer] treasuries, kills and deaths per day.
- [ ] **K8.3 Seeds 1–120,** [Basic to run; any failure it finds is graded on its own] with and without the guild.
- [ ] **K8.4 The producer's play session.** [Possibly Complex: grade each fix it asks for]
- [ ] **K8.5 Part 3's close-out:** [Basic]
  - the roadmap: Part 3 COMPLETED, and Milestone 4 as Q1 decides;
  - the epic doc, the focus file, `architecture_history.md`, the economy model and AGENTS.md;
  - this plan deleted.

## 8. Risks
| Risk | What we do |
| :--- | :--- |
| Creatures and fights cost planning and frame time. | Creatures follow a short rule, not the planner (§3). Measure at K1 and K6. |
| Fights must be deterministic. | The main thread, in id order, on ticks, with one seeded roll per swing (A4). |
| Death touches every system that holds a crawler: groups, contracts, bunks, parties, memory, the ledger and the views. | The drifter's departure is the precedent (A2), and K4.2 is supervised. The round-trip validators gain the dead. |
| Deaths change the economy: a smaller population buys and sells less. | The newcomer rule (Q4). The soak's bands are set with deaths in them (K8.2). |
| Hazards that can kill make Part 1's and 2's dangers sharper. | Q3's alternative (a): only creatures kill. `TheCarefulLoseNoOne` watches every gate seed. |
| The bold die on the default seeds with no player involved. | Avoidance of known lairs (Q6) and the band in `TheCarefulLoseNoOne`. If deaths pass the band, the lone limit is tuned. |
| The planner's facts grow: armed, a known lair, a hunt, a pile. | About 46 of 64. R1's guard. |
| The map grows: the Old Stair and two lairs. | Golden and renders re-recorded in their own commits. Well within A*'s cap. |
| Combat reads poorly on screen. | A strike pose, the red coat on both sides, boss health bars, and the producer's look at K2. |
| The save format moves at K0, K1, K3, K4, K5, K6 and K7. | The index's rule: raised once per PR. Old saves are refused with a reason. |

## 9. Deferred (proposed)
| Left out | Owner |
| :--- | :--- |
| Floor 2, floors as data, the move and what moves, the next floor's pressure | Part 4, the descent: QUEUED, not drafted (Q1) |
| Scouting as a guild order | Part 4, or later; scout bounties chart sectors today |
| Ranged weapons | Later, unless the producer takes Q2's alternative (a) |
| Random affixes on gear | Later; rare pieces are fixed items (Q11) |
| Fights between crawlers (the design's "lethal rivalries") | Later, deeper floors |
| Raids on the outpost | Later (Q5's alternative (b)) |
| Resurrection | Later (Q3's alternative (b)) |
| Flanking (Epic 8's intent) | Later; focus fire and retreat come in K6 |
| Loot tables on nodes and the haul | Later; Part 3 rolls loot only for kills |
| Procedural bosses (the game design's Phase 4) | After Epic 10 |
| Art beyond the generated placeholders | The producer's art direction (Epic CLIENT M7) |
| Tavern rumours written by a language model | Epic 11 |

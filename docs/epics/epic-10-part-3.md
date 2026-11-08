# Epic 10, Part 3: Creatures, Combat and the Gatekeeper

- **Status**: ACTIVE (2026-11-08). The producer gave the go: "The revised epic plan looks good. Please proceed with K0". K0, groundwork, is done the same day (§4): the measurements (finding F1), the combat rule (D1–D8) and the creature catalogue. The producer approved it the same day: "Approved. Proceed with developing K1". K1, the equipment system, is under way: K1.1 (slots as a table) and K1.2 (storage for 16 slots, save format 11) are done, and the save design waits for the developer's review before K1.3. Status is owned by [ROADMAP.md](../ROADMAP.md). Decisions and deviations: `.agents/architecture_history.md`. The working plan, with every step stamped: `.agents/epic10_part3_plan.md`.
- **Track**: Track A: Core Simulation & Cognition Engine. Epic UI-5 is folded in, as UI-3 and UI-4 were into Parts 1 and 2, because its UI is the KNI client's.
- **Milestone**: Production Milestone 4, Combat & the Gatekeeper (ROADMAP §3). Milestone 4 ends with this part (Q1).
- **Source**: the draft plan `.agents/epic10_part3_plan.md`. It was first written 2026-10-29 as "the descent", re-scoped by the producer on 2026-11-07 and answered in part the same day. Its design (§1–§4) moved here with the go.
- **Preceding**: [Epic 10 Part 2](epic-10-part-2.md), workshops and the guildhall; its play session and close-out are still to come. **Builds on**: Epic 8 (groups and directives), Epic 9 (the economy), the item system (I1–I8), Part 1's health and hazards (G2) and gear (G3), Part 2's workshops, grades and 24-hour day, and Epic UI-Beta (the client's UI kit and the red coat).
- **Following**: Epic 10 Part 4, the descent (Floor 2 and moving the outpost; Milestone 5), not drafted.
- **Complexity** ([rubric](../../.agents/rules/complexity-grading.md)): **Very Complex** as an epic, for its size and for systems with no precedent (combat, creatures, death, ranged, skills). Each milestone is stamped in §2.4. K1, K2, K5, K8, K9 and K10 are split in the plan, which names the steps the developer supervises.

---

## 0. Producer intent and decisions

**The re-scope (2026-11-07),** right after Part 2 merged as PR #23:
> I want to re evaluate what we are tackling in epic10_part3, The focus should be combat system, equipment improvements, death, loot, creatures, bosses / gate keepers. We should not focus on floor 2 at all yet

**The answers (2026-11-07):**
> Q3 hazards should also kill if the downed crawler is not rescued.
> Q4 looks good.
> Q2 melee first, then range. Seeded hit rolls.
> Q11 Add more equipment slots, for now we should have 2 slots but I want us to build the foundation of a more complex appendable equipment system.
> Q1 the milestone 4 should end here with functional combat system.
> Add K9 - skill system

**The go (2026-11-08):**
> The revised epic plan looks good. Please proceed with K0

The producer answers by exception. So the three readings flagged with the answers stand: Q1 (Epic 11 to Milestone 5), Q3 (the downed collapse in danger; Q3a, Q3b) and Q11 (head and body). So do the defaults Q5–Q8, Q10 and Q12–Q21.

What the answers changed:
- **Ranged combat** joins Part 3 as K8, after the whole melee loop works.
- **An appendable equipment system** is built first, as K1. Its first two slots are head and body, beside the weapon and the tool.
- **Rescue is part of death:**
  - a crawler downed in danger can no longer limp out of it;
  - carrying the fallen moves from the guild's milestone into K5, so that anyone can do it;
  - friends rescue each other.
- **A skill system is K9.** It replaces Q9's veterans and takes over W4's practice.
- **Milestone 4 ends with Part 3:** a functional combat system.

**What left Part 3:** Floor 2, floors as data, the move and what it carries, and Floor 2's pressure. They go to Part 4, the descent, in Milestone 5, which is not drafted. Scouting as a guild order goes with them; scout bounties already chart sectors.

**Sources:**
- **The game design:**
  - §3: the upper floors have "minor vermin, low-value salvage", and the deeper floors "lethal mob packs". "Advancing to the next floor requires outfitting a party capable of defeating a sector boss guarding the descent gate."
  - §2: "Cleared sectors permit direct camera observation of simulated micro-encounters (campfires, node disputes, combat)."
- **Epic 8's intent:** "Directives will drive squad-level combat flanking, focus-fire, and tactical retreat."
- **What Parts 1 and 2 left to Part 3:** a weapon (`Attack` and `Defense` are unused); carrying a downed member; loot tables in play; rarity in the client (Part 1 §5); armour against creatures; combat and death (Part 2 §5).
- **The guiding lights:**
  - **Majesty:** heroes fight because a reward draws them. The player flags a lair, and the bold answer. They buy their own arms and potions, flee when hurt, and grow into veterans.
  - **RimWorld:** fights are short, readable and dangerous. The downed bleed out unless someone carries them to safety. Animals drop leather that becomes armour, skills grow with practice, and a death is felt by those who knew the dead.

### The questions and their answers
Answered rows say so. Every other row is a default that stands by exception.

| # | Question | The producer's answer, or the default | Alternative |
| :-: | :--- | :--- | :--- |
| Q1 | **Name, home and the milestone** | **Answered: Milestone 4 ends with Part 3,** a functional combat system.<br>• **Milestone 4** becomes "Combat & the Gatekeeper": Part 3, with Epic UI-5 folded in, in this file.<br>• **Milestone 5,** "Floor Relocation": Part 4, the descent.<br>• **The reading:** for Milestone 4 to end with Part 3, Epic 11 (the MCP chronicles) moves to Milestone 5.<br>Epic 10 closes after Part 4. | — |
| Q2 | **Combat** | **Answered: melee first, then ranged; seeded hit rolls.**<br>• **A swing:** a fighter in reach swings on its weapon's interval (about 1.5 s). A seeded roll (`SimRandom`, its own salt) decides hit or miss: about 70% until skills (K9) set it.<br>• **A hit** does the weapon's `Attack`, cut by the target's `Defense` on a curve that never reaches zero (`Defense / (Defense + 10)` to begin with).<br>• **Order:** hits are resolved on the main thread, in one pass after movement, in entity-id order.<br>• **Unarmed,** a crawler hits for 2.<br>• **Ranged** is K8, on the same rule (Q15, Q16). | — |
| Q3 | **Death** | **Answered: hazards kill too, if the downed crawler is not rescued.** Death is permanent.<br>• At 0 a crawler is downed.<br>• **Downed in danger,** within a hostile creature's reach or in a hazard sector, it collapses. It can't limp out, and it bleeds out over 2 game hours.<br>• **Downed in safety,** it limps to a bunk as today.<br>• **Rescue stops the clock:** a rescuer carries it out of danger, or gives it a draught (at 80 health it stands and walks out).<br>• **When the clock runs out, it dies.**<br>• Creatures turn from a downed crawler to one still standing.<br>• **The reading:** to honour "rescued", the downed can no longer limp out of danger as Part 1's crawlers do. Two sub-defaults follow:<br>&nbsp;&nbsp;– **Q3a:** a downed crawler may drink a draught it carries. Preparing counts as rescue, and draughts are the player's stock at work.<br>&nbsp;&nbsp;– **Q3b:** friends rescue friends, not only party members. Otherwise a lone crawler downed in a hazard always dies. | Q3a: only another crawler can give the draught. Q3b: only the guild rescues |
| Q4 | **What a death costs** | **Answered: as proposed.**<br>• **The body:** its pack, worn gear and purse lie where it fell, as a loot pile that anyone may recover.<br>• **The guild:** the dead member's contract and kept bunk end, and its party is one short.<br>• **The living:** friends (warmth 30 or more) grieve, through a memory event and a mood. If a member dies under the guild's orders, the other members' trust toward the patron falls.<br>• **Memory:** the chronicle tells it, and the departed list keeps the dead with their cause.<br>• **A newcomer** settles at the Road Gate 2 days after a resident dies, with a new look and name. | — |
| Q5 | **Creatures** | • **A new kind of entity,** plain data, saved. Its rule: rest at its lair, roam its sector, chase what it sees within a leash, strike in reach, flee when hurt if its kind flees, go home and heal over the day.<br>• **A catalogue of kinds,** like the item catalogue, as data: health, `Attack`, `Defense`, speed, reach, swing interval, sight, leash, flight, loot table and art.<br>• **Three kinds** (placeholder names):<br>&nbsp;&nbsp;– **burrow rats**, weak, in packs of three or four, and they flee;<br>&nbsp;&nbsp;– **stone beetles**, armoured and slow, where a weapon matters;<br>&nbsp;&nbsp;– **cave spinners**, which spit from range, on the gate's approach (K8, K10).<br>• **Lairs are map data:** rats in the Overgrowth, beetles in the Deep Seam. They are spawned from the session's seed, and a cleared lair refills after 2 game days.<br>• **No raids:** creatures keep to their sectors. | (a) Creatures as moving hazards, hurting by presence with no fight. (b) Raids on the outpost at night: later |
| Q6 | **Lone crawlers** | • A crawler judges a **known** lair as it judges a hazard: it avoids one past what it would brave alone.<br>• **Attacked,** it flees (a reflex beside retreat), unless it is bold and armed: then it fights back.<br>• **The bold buy their own weapons** at the stall when their purse spares it, as residents who mine buy pickaxes (D39).<br>• **A scout's report** charts the lairs of the sector it charts. | Every crawler flees; only the guild fights |
| Q7 | **Hunt bounties** (Majesty's flag) | • **A third bounty kind** beside Scout and Gather: "Hunt", on a charted lair, paid from escrow when the lair is empty.<br>• **Up to three claimants,** the bold and armed. The reward is shared by the damage each one dealt.<br>• If the lair refills before it is cleared, the bounty runs on until it expires. | No hunt bounties: only the guild fights |
| Q8 | **The guild's Clear order** | • **A new order** for an expedition: a lair as the target. Its stages: muster, march, **fight**, return, deliver. The haul is what the party picked up.<br>• **Focus fire:** the directive names one target at a time, the party leader's choice (the nearest, then the weakest).<br>• **Retreat:** the party turns back when half its members are downed, or its health is spent past a line.<br>• **Rescue under orders:** members carry their fallen out (K5's carrying).<br>• Flanking is left for later. | — |
| Q9 | **Veterans** | **Folded into K9, the skill system** (the producer's "Add K9"): veterans are crawlers with high combat skills. | — |
| Q10 | **Melee weapons** | Three, in the weapon slot (placeholder names):<br>• **a cudgel**, which the caravan supplies cheaply;<br>• **an iron blade**, made at the forge from ore;<br>• **a glowstone edge**, made from ore and glowstone: the best.<br>They wear per hit (`WearRule.PerUse`), and the forge's grades and repair apply. | Weapons from the caravan only |
| Q11 | **Equipment** | **Answered: more slots, two for now, on the foundation of an appendable equipment system (K1).**<br>• **The reading:** the two slots are **head** and **body**, which replace protection. The weapon and the tool slots stay.<br>&nbsp;&nbsp;– the head takes the hard hat, the respirator (worn as a mask) or an iron helm;<br>&nbsp;&nbsp;– the body takes hazard gear or armour: a ring coat from ore (K3), and a leather jerkin from hides or chitin plate (K4).<br>• **A real choice:** a party bound for a lair in a hazard sector picks armour or a hazard suit.<br>• **The foundation:**<br>&nbsp;&nbsp;– slots are a table, not an enum;<br>&nbsp;&nbsp;– an item fits a slot kind, and a kind may later have several slots (two rings, two hands);<br>&nbsp;&nbsp;– storage keeps room for 16 slots, so a new slot needs no save format change;<br>&nbsp;&nbsp;– stats, wear, outfitting, the dossier and worn sprites all read the table;<br>&nbsp;&nbsp;– a new slot is a row, by a new recipe in AGENTS.md, "Adding an equipment slot".<br>• **Rarity:** rare pieces are fixed items with a rarity, dropped by bosses (for example the Matriarch's fang dagger), never random affixes. The client shows rarity's colours (Part 1's deferral). | — |
| Q12 | **Loot** | • **Piles:** a kill drops a pile at its place, rolled from its kind's loot table: its material (hide, chitin), sometimes salvage, now and then a rare piece. A boss drops a sure trophy and a rare piece.<br>• **Who takes it:** the victors pick it up, a party into its haul and a hunter into its pack. Anyone may claim a pile left behind. Piles fade after a game day.<br>• **The economy:** the stall buys the materials, the caravan exports them, and the forge makes armour from them. | Drops go straight into the killer's pack, with no piles |
| Q13 | **Bosses** | **Two:**<br>• **The Matriarch** heads the rats' warren. She is stronger and calls her brood to her at half health. Slain, she returns after a game week.<br>• **The Gatekeeper** holds the sealed descent gate:<br>&nbsp;&nbsp;– **its lair:** a new sector at the floor's edge, the Old Stair, reached through the Deep Seam and uncharted at the start;<br>&nbsp;&nbsp;– **its fight:** a slam that strikes everyone in reach, and fury at a quarter of its health;<br>&nbsp;&nbsp;– **between attempts:** it heals fully once its lair has been empty for a game hour;<br>&nbsp;&nbsp;– **beaten once,** it stays beaten.<br>• **The breach** is a saved world flag, told by the chronicle and a hard cutscene. Beyond the gate the stairs go down into the dark, for Part 4. | The Gatekeeper only |
| Q14 | **Shipping and done** | As in Parts 1 and 2:<br>• one PR, with a commit per step;<br>• each milestone's DoD is a live scenario on the real session, with nothing injected;<br>• a scripted player acts only through commands. | — |
| Q15 | **Ranged weapons** (K8) | • **Two,** in the weapon slot: **a sling** (caravan) and **a crossbow** (forge, from ore and hide).<br>• **A shot** needs a clear line (a ray over the nav grid's walls) and a range. Its hit roll falls off with distance.<br>• **A ranged fighter keeps its distance:** it steps back when a foe closes. In a party, ranged members stand behind the melee.<br>• **One weapon slot,** melee or ranged; a ranged fighter hits unarmed up close. | A sidearm slot for a second weapon: one row in K1's table |
| Q16 | **Ammunition** (K8) | **None:** a ranged weapon wears per shot, as a blade wears per hit. No ammunition to carry, provision or plan for. | Bolts as stacks made at the forge: a sink and a reason to restock, with one more planner fact |
| Q17 | **Which skills** (K9) | **Seven, as a table** (appendable, like the slots): melee, ranged, mining, foraging, smithing, alchemy and medicine. | More now: scouting, social or hauling |
| Q18 | **Levels and growth** (K9) | • **Levels 0–10,** named in bands: Novice (0–2), Apprentice (3–5), Journeyman (6–8), Master (9–10).<br>• **Practice:** experience from each swing landed, shot, unit mined or foraged, make and rescue. Each level costs more than the last.<br>• **No decay** at first. | Levels 0–20 with decay at the top, as RimWorld has |
| Q19 | **Starting skills** (K9) | • **Drawn at spawn** from the seed, on its own salt. Most skills start low, and one or two start higher.<br>• **Personality leans the draw:** the bold lean toward melee.<br>• **Not from a look:** no decision reads a look until lineage traits are designed (`CognitionReadsNoLookTests`).<br>• Newcomers and drifters get skills too. | All start at 0 |
| Q20 | **What skills do** (K9) | • **Melee and ranged:** the hit chance and a little damage.<br>• **Mining and foraging:** harvest speed, multiplied with the pickaxe.<br>• **Smithing and alchemy:** a make's grade and speed. **W4's practice becomes these skills;** an artisan's grade is now its skill's band.<br>• **Medicine:** carrying a fallen crawler faster, and a draught given heals more. | Skills only in combat; W4's practice stays apart |
| Q21 | **Do skills steer choices?** (K9) | **Lightly, through what exists.**<br>• A crawler weighs work by what it pays an hour (W1.4), and skill raises that, so specialists emerge with no new rule.<br>• The roster and the recruit list show skills, so the player hires for them. | Skills as an explicit preference in every work choice |

### K0's decisions (2026-11-08)
Defaults, taken while doing K0; the producer answers by exception. D1–D8 are K0.3's, the combat rule, which the developer reviews (the step is supervised).
- **D1, the read model:** `Combatant` is a value read when a swing is due and then dropped: id, position, body radius, health, whether it stands, attack, defense, reach, swing interval, range and proficiency. A crawler's comes from its state and `CrawlerStats`; a creature's from its kind and its record (K0.4, K2). The rule reads nothing else.
- **D2, a weapon is modifiers on the bare hand:**
  - Reach, the time between swings and range are stats beside `Attack` and `Defense` (`StatKind.Reach`, `AttackInterval`, `Range`).
  - The bare hand is the base: attack 2 (Q2), reach 0.5 across the gap between bodies (neighbours on adjacent tiles), a swing every 1.5 s, no range.
  - So a "+4 attack" cudgel hits for 6, and a blade at "×0.8 interval" swings every 1.2 s.
  - Grades strengthen them as they strengthen every effect (Part 2's D56), and armour adds `Defense`.
- **D3, the roll:**
  - **One draw per swing,** from (seed, attacker, tick, salt 10). The target doesn't enter it.
  - **Several foes in one swing:** a swing that strikes several (the Gatekeeper's slam, K10) draws once a foe, in id order.
  - **Why it holds:** a fighter swings at most once a tick, so every swing has its own stream.
- **D4, no damage variance:** a hit does exactly the mitigated attack, as Q2 words it; the roll decides only hit or miss. A second draw for variance can come later without reshaping the rule.
- **D5, the chance:** 70% plus proficiency (0 until K9), kept within 5–95%.
- **D6, simultaneous resolution** (for K2.2's pass):
  - every swing of a tick is decided from the tick's start, and then all the harm is applied;
  - so two fighters can fell each other in the same tick, and the order of resolution changes nothing;
  - Q2's id order remains the pass's order, for its logs and bookkeeping, but no outcome depends on an entity's id.
- **D7, wear per landed hit:** only the weapon slot's piece wears, and only on a hit; a miss, a bare hand, the tool and armour wear nothing from a swing. How armour wears from blows taken is K1.3's and K3's.
- **D8, the swing's timer belongs to its owner:**
  - A fighter's next swing is a tick (`NextSwingAt`), kept by whoever owns the fighter: the creature's record (K2), or the crawler's state (K3).
  - Whether it is saved is K2.4's call. K11.1 asks a save mid-fight to load byte for byte, which argues for saving it.

## 1. Goal
**The player promise:** the deep sectors bite back. Arm the outpost, put a price on what lurks there, send the guild against the brood and break the gatekeeper. Any crawler you send can die, unless someone brings it home.

What Parts 1 and 2 leave undone:
- **Nothing threatens a crawler but the air and the rock.** Hazards hurt but never kill, so risk is a cost in time, never a loss.
- **The forge has nothing to arm.** Weapons have no use, and armour only means hazard gear.
- **Crawlers never get better at anything,** except an artisan at its station.
- **Loot is a foundation with no floor on it:** the haul is always glowstone.
- **The floor has no climax:** no boss, no gate, nothing to work toward past a full guild.

## 2. Design

### 2.1 The loop
1. **Creatures hold sectors.** Vermin nest in the Deep Seam and the Overgrowth. A lone crawler who strays in is hurt and flees, and the cautious stop going. The salvage there dries up: pressure on the stall.
2. **The player answers indirectly:**
   - stock weapons and armour at the stall, so that the bold arm themselves;
   - post a **Hunt bounty** on a lair (Majesty's flag), which the bold and armed take;
   - send an armed guild party with a **Clear** order.
3. **Kills drop loot:** hides and chitin, sometimes salvage, now and then a rare piece. The stall buys them, and the forge turns them into better armour.
4. **The fallen need rescue.** A crawler downed in danger collapses where it is, and only a draught or a rescuer saves it: a party member, or a friend who dares the danger. Otherwise it bleeds out and dies. Friends grieve, the guild loses a contract, and the body lies where it fell with everything it carried.
5. **Fighters and workers grow:** every swing, shot, unit mined and make is practice (K9). Survivors become veterans.
6. **Bows join blades:** ranged fighters hold back and shoot (K8).
7. **The brood's Matriarch** teaches the guild that a boss needs a party, draughts and armour.
8. **The gatekeeper** holds the sealed gate at the floor's edge. Breaking it is Part 3's climax and Milestone 4's end; the way down stays dark until Part 4.

**The new verbs:** stock arms, post a hunt, order a clearing, rescue the fallen (crawlers do it), and breach.

### 2.2 Positions (argued in the draft; the producer may overturn any)
- **The player never commands a fight.** Fights come from bounties, orders and stock, as everything else does. A direct "attack this" button would make Simpiens a tactics game, and the game design's whole premise is indirect agency.
- **Combat is watched, so it runs in physical time.**
  - Swings, shots, lunges and flight are motion: ticks and seconds, as walking is ([time-model.md](../time-model.md)).
  - What lasts across the day stays in the day's time: bleeding out, lairs refilling, a boss healing between attempts, and practice.
  - An encounter resolved by a hidden roll was rejected: the design promises "direct camera observation" of combat.
- **Creatures are not crawlers.** A creature has a short rule on the main thread, not a planner, needs, memory or a purse. RimWorld's animals are simple too, and fifteen to twenty-five creatures must cost the tick almost nothing.
- **Deaths come from choices.** A lone crawler avoids lairs it knows of, as it avoids a hazard past its limit. So deaths come from:
  - the player's orders;
  - bounties the bold chose to take;
  - the fog: a lair nobody has charted;
  - a venture into a hazard that went wrong with nobody there to help.

  A careful player who stocks draughts loses few, and a reckless one loses many. Without that, death is noise, not stakes.
- **Loot feeds the workshops, not only the purse.** Hides and chitin become armour at the forge. Each kill pays twice: in what the stall sells, and in what the forge can make.
- **Systems that will grow are tables from the start.** Equipment slots (K1), creature kinds (K0.4) and skills (K9) are catalogues, as items are. The next slot, kind or skill is a row, not a refactor: the producer's "appendable" (Q11), applied wherever Part 3 lays a system down.

### 2.3 Architecture and rules
Part 3 adds systems rather than rewriting old ones, with one exception: equipment. Today's slots are an enum, with protection meaning "the one protective item" in the stats, the outfitting rules and the client. The producer's "appendable" makes this the one refactor worth doing first (K1): weapons, armour and ranged weapons all land on it.

Seven seams, laid before the systems that use them:
- **A1. One combat rule over two kinds of fighter.**
  - `CombatRules` reads a fighter through a small read model (`Combatant`: health, `Attack`, `Defense`, reach, interval, range and proficiency).
  - A crawler's comes from `AgentState` and `CrawlerStats`, and a creature's from its own record.
  - Proficiency is flat until K9 fills it from skills, and range is melee only until K8.
  - So there is one rule, one validator and one place to tune (K0.3).
- **A2. A crawler leaves the world one way.**
  - A death reuses the drifter's departure: removal at the end of the tick and `CrawlerDeparted`.
  - `DepartedCrawler` gains a cause (left or died).
  - The groups, bunks, contracts and views that already handle a drifter leaving handle the dead the same way (K5.4).
- **A3. Equipment as a table** (K1): slots, slot kinds, storage with room for 16, and generic stats, wear and outfitting. The goods table grows to 16 in the same format bump.
- **A4. Creatures tick in a phase of their own,** after the crawlers' runtime and in id order, and every swing and shot is resolved in one pass after movement. Determinism holds, and combat can be tested apart from cognition (K2.2).
- **A5. Items on the ground are one entity:** the loot pile. A kill's drop (K4) and a body (K5) are both piles.
- **A6. Kinds as data:** creatures in a catalogue, like items, so the Matriarch and the Gatekeeper are rows with boss traits plus the mechanics no row can hold (K10).
- **A7. Carrying is one capability:** two crawlers' movement coupled, built once (K5.2) and used by friends (K5.3) and parties under orders (K7).

The rest follows Parts 1 and 2: economy numbers live in `EconomyTuning`; new authoritative state goes into its owner's save block, and the format rises once per PR; the planner's facts grow from 41 toward about 47 of 64, under R1's guard.

### 2.4 Milestones

| Milestone | What the player sees | Core (engine-free simulation) | Client (KNI, Epic UI-5) | Complexity |
| :--- | :--- | :--- | :--- | :--- |
| **K0. Groundwork** | Nothing new | Measurements; the combat rule and its read model; the creature catalogue | — | Very Complex (K0.3 supervised; the rest Possibly Complex) |
| **K1. The equipment system** | A crawler wears a hard hat and hazard gear at once | Slots as a table, storage for 16, generic stats, wear and outfitting; the goods table at 16 | The dossier and the party builder drawn from the slot table | Very Complex (split; bundling) |
| **K2. Creatures** | Vermin in the Deep Seam and the Overgrowth; a stray is hurt and flees; the cautious keep out | Lairs, spawns, the creature's rule; flight and avoidance; the save | Creatures drawn, striking, coated red when hit, hidden by the fog | Very Complex (split; K2.2 and K2.3 supervised) |
| **K3. Melee arms and armour** | The bold buy blades and fight back; the forge makes weapons, a helm and a ring coat | Melee weapons, armour from ore, wear per hit, fighting back, outfitting for creatures | Weapons in hand; the merchant terminal; the party builder | Possibly Complex |
| **K4. Loot** | Kills leave piles; hides and chitin sell, and become armour | Piles, drops, picking up, materials and their recipes, rare pieces | Piles on the map; rarity's colours | Possibly Complex |
| **K5. Death and rescue** | The fallen collapse in danger; a friend or a draught saves them, or they die; the dead are mourned; a newcomer arrives | Collapse and bleeding out; carrying; rescue; a death; grief and trust; the newcomer; the save | A crawler carried; the body and a cairn; the chronicle; the dossier of the dead | Very Complex (split; K5.2 and K5.4 supervised) |
| **K6. Hunting** | The player flags a lair; the bold clear it and are paid | Hunt bounties; the hunters' goals | The bounty board's Hunt | Possibly Complex |
| **K7. The guild goes to war** | An armed party clears a nest, carries its fallen out and brings back loot | The Clear order; the party's fight: focus fire, retreat, rescue under orders | The Clear order and the fight in the tracker | Possibly Complex (K7.2 Very Complex, supervised) |
| **K8. Ranged combat** | Slings and crossbows; archers hold back; spinners spit | Ranged weapons, line and range, keeping distance, ranged creatures, ranged members in a party | Shots drawn between ticks; ranged goods | Very Complex (split; K8.2 supervised) |
| **K9. The skill system** | Crawlers get better at what they do; veterans, master smiths and fast miners | Skills as a table, on each crawler, saved; practice; what skills do (W4's practice folded in) | Skills in the dossier and the roster; the chronicle's milestones | Very Complex (split; K9.3 supervised) |
| **K10. Bosses and the gate** | The Matriarch; the Old Stair; the Gatekeeper broken; the gate breached | The new sector; boss traits and mechanics; the breach | Boss art and health bars; the gate; the breach's cutscene | Very Complex (split; K10.3 supervised) |
| **K11. The gate** | Part 3 played through: Milestone 4's functional combat system | Every scenario; seeds; soak bands with combat and deaths | The producer's play session | Possibly Complex |

**Order:** K0 to K11, in order.
- The equipment system (K1) comes before any new gear.
- Loot (K4) comes before death (K5), because a body is a pile.
- The whole melee loop (K2–K7) comes before ranged (K8), as the producer asked.
- Skills (K9) come after both kinds of combat, so they tune what exists, and before the bosses, which are tuned with skills in play.

## 3. Definition of Done

Done means fights, deaths and the gate emerge in the live outpost with nothing injected (ROADMAP §1B). A scripted player acts only through typed commands.
- **Mechanism:** validators for the combat rule, the creature catalogue and rule, equipment slots, loot piles, carrying, death, skills, the bosses, and each save block's round trip.
- **Live, headless:** one scenario per milestone on the real outpost:
  - `GearByTheSlot` (K1);
  - `VerminDriveTheLoneAway` (K2);
  - `AnArmedCrawlerFightsBack` (K3);
  - `KillsFeedTheForge` (K4);
  - `AFriendCarriesTheFallenOut`, `DeathHasItsCost` and `TheCarefulLoseFew` (K5);
  - `AHuntBountyClearsALair` (K6);
  - `AnArmedPartyClearsANest` (K7);
  - `ArchersHoldTheLine` (K8);
  - `PracticeMakesAVeteran` (K9);
  - `TheGuildBreaksTheGatekeeper` (K10);
  - every scenario together, with a save mid-fight, a bleeding crawler and a crawler being carried (K11).
- **The economy:** the ledger balances with deaths, bodies and gold on the ground. The soak gains a `hunting` profile, its bands set with the producer (K11).
- **The producer:** looks at K3, K5 and K8, and a play session at K11.
- **Docs:** the roadmap first, then this file's status, then the focus file; `architecture_history.md`; the economy model; `AGENTS.md` (with "Adding an equipment slot" and the creature catalogue).

## 4. Acceptance checklist (delivered work only)

### K0. Groundwork (done 2026-11-08, `feat/epic10-part3`; the combat rule waits for the developer's review)
- [x] **Docs:** this file, the roadmap (Part 3 ACTIVE), the focus file; the plan keeps the steps.
- [x] **Measurements** (K0.2, the 23 gate seeds for three game days, with no player, the guild player and the crafting players): in the plan, with finding F1 below. 41 of 64 planner facts; about 20,000 ticks/s; frames of 2–4 ms; the soak 15/15; the bold carry 60–100g on most days; nobody is downed by a hazard.
- [x] **The combat rule** (K0.3, D1–D8; the developer's review pending): `Combatant`, `CombatRules` and `SwingOutcome` in `Simulation/Combat/`; `SimRandomSalt.Combat`; `StatKind.Reach`, `AttackInterval` and `Range`. `CombatValidator` (9 tests). Nothing calls it yet, so the golden hashes don't move.
- [x] **The creature catalogue** (K0.4, seam A6): `CreatureKind`, `CreatureDefinition` and `CreatureCatalog` in `Simulation/Creatures/`, with burrow rats, stone beetles and cave spinners as placeholder rows; `Combatant.ForCreature`. `CreatureCatalogValidator` (5 tests) holds the kinds to Q5's shape through the combat rule's expectations. Nothing spawns yet.
- [x] **The gate:** Core 186/186 and client 485/485, though one allocation test failed once with the core's tests running alongside (F2); golden 3/3, unchanged; the 23 gate seeds and the soak hold; seeds 1–120 120/120 with and without the guild; the pinned image 485/485 exact; the MonoGame and Android compiles with 0 warnings. K0 has nothing a player does yet, so it has no live scenario of its own.

## 5. Deferred and out of scope

| Left out | Owner |
| :--- | :--- |
| Floor 2, floors as data, the move and what moves, the next floor's pressure | Part 4, the descent, in Milestone 5: QUEUED, not drafted |
| Scouting as a guild order | Part 4, or later; scout bounties chart sectors today |
| More equipment slots: hands, feet, rings, a sidearm, two-handed weapons that fill two slots | Rows on K1's table, when the design wants them |
| Ammunition | Later, unless the producer takes Q16's alternative |
| Random affixes on gear | Later; rare pieces are fixed items (Q11) |
| Skill decay, passions, and skills beyond the seven | Later (Q17, Q18) |
| Fights between crawlers (the design's "lethal rivalries") | Later, deeper floors |
| Raids on the outpost | Later (Q5's alternative (b)) |
| Resurrection | Later |
| Flanking (Epic 8's intent) | Later; focus fire and retreat come in K7 |
| Loot tables on nodes and the haul | Later; Part 3 rolls loot only for kills |
| Procedural bosses (the game design's Phase 4) | After Epic 10 |
| Art beyond the generated placeholders | The producer's art direction (Epic CLIENT M7) |
| Tavern rumours written by a language model | Epic 11, in Milestone 5 (Q1's reading) |

## 6. Risks

| Risk | What we do |
| :--- | :--- |
| Creatures, fights and shots cost planning and frame time. | Creatures follow a short rule, not the planner (§2.2). Measure at K2, K7 and K8. Line of sight is a ray over the nav grid, checked only when a shot is due. |
| Fights must be deterministic. | The main thread, in id order, on ticks, with one seeded roll per swing or shot (A4). |
| Q3 makes Part 1's and Part 2's hazards lethal: a crawler downed in a hazard no longer limps out. | Measured at K0.2: nobody is downed by a hazard in three game days on the gate seeds (F1). Friends' rescue (Q3b) and a carried draught (Q3a) are the answers in the game. The lone limit and the retreat line are tuned against `TheCarefulLoseFew`'s band. The golden hashes are re-recorded in K5.1's own commit. |
| Death touches every system that holds a crawler: groups, contracts, bunks, parties, memory, the ledger and the views. | The drifter's departure is the precedent (A2), and K5.4 is supervised. The round-trip validators gain the dead. |
| Deaths change the economy: a smaller population buys and sells less. | The newcomer rule (Q4). The soak's bands are set with deaths in them (K11.2). |
| The equipment refactor touches hazards, harvest, outfitting, saves and the client at once. | Split into six steps, with a proof test that adds a slot (K1.6). Behaviour is held, or re-recorded with its cause named. |
| Skills change every seed's numbers: harvest speed, crafting, hits. | They come late (K9), after the systems they tune. The golden hashes are re-recorded in K9.1's own commit, and the bands are re-checked at K11. |
| The planner's facts grow: armed, a known lair, a hunt, a pile, a fallen friend, ranged. | About 47 of 64. R1's guard. |
| The map grows: the Old Stair and two lairs. | Golden and renders re-recorded in their own commits. Well within A*'s cap. |
| Combat reads poorly on screen. | A strike pose, shots drawn, the red coat on both sides, boss health bars, and the producer's looks at K3 and K8. |
| The save format moves at most milestones. | The index's rule: raised once per PR. Old saves are refused with a reason. K1's storage for 16 slots and K9's for 16 skills avoid later bumps for new rows. |

## 7. Findings

- **F1 (2026-11-08, K0.2): hazards alone down nobody.** On the 23 gate seeds over three game days, with no player, with the guild player and with the crafting players, no crawler is downed. The lowest health anyone reaches in a hazard sector is 46 for a resident and 42 for a member on an expedition, above the retreat line (40). So Q3's lethal hazards change no outcome on the gate seeds by themselves, and the deaths `TheCarefulLoseFew` bounds will come from combat, from orders that push past the retreat line, and from bad luck. K5.1 re-records the golden hashes only if a golden seed downs someone in danger. [Basic: a measurement that K5 reads; no work of its own]
- **F2 (2026-11-08, K0's gate): an allocation test failed once.** `WorldViewFramesTests.ASteadyCaptureAllocatesNothing` failed in one `dotnet test` of the whole solution, with the core's tests running alongside. It then passed 3 times alone and in two full reruns of the client's tests (485/485). K0 doesn't touch the client, and the same test passed in K0.3's run. Watched: if it fails again, look at what the runtime allocates on the test's thread during the measured loop (tiered compilation is the first suspect) before looking at the capture. [Basic: watch]

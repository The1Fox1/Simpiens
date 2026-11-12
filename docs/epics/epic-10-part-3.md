# Epic 10, Part 3: Creatures, Combat and the Gatekeeper

- **Status**: ACTIVE (2026-11-08). The producer gave the go: "The revised epic plan looks good. Please proceed with K0". K0, groundwork, is done the same day (§4): the measurements (finding F1), the combat rule (D1–D8) and the creature catalogue. The producer approved it the same day: "Approved. Proceed with developing K1". K1, the equipment system, is done the same day: slots as a table, storage for 16 (save format 11, its design approved: "continue with k1.3-k1.6"), stats and wear over every slot, outfitting by slot and by stats (the live scenario `GearByTheSlot`: a venturer wears a hard hat over hazard gear on 17 of the 23 gate seeds), the client from the slot table (hazard gear is now a suit worn on the body), and a slot added as data. The producer's look passed ("K1 looks good") with the go for K2, creatures: K2.1, lairs, done the same day; K2.2, the creature's rule, done the same day and waiting for the developer's review (its decisions D9–D15 below) before K2.3. At the producer's word (2026-11-09: "Before we move to K3 i want to get some sprites/rendering going for creatures. They can be simplistic for now"), the creatures are drawn on the map ahead of K2.3 (K2.5a): simple side-view placeholders in both view modes, mirrored by the way they go, hidden by the fog; the strike pose, the red coat and the inspector come with the steps that make them visible. The developer's "Proceed" (2026-11-10) began K2.3: its decisions D16–D20 below. On F3 the developer chose option A the same day ("Chose A. Proceed"): hunting went live, K2.4 (the creatures saved) and the strike pose followed, and K2's DoD `VerminDriveTheLoneAway` passes on the 23 gate seeds. At the producer's "Continue" the same day the inspector came to show a creature, so K2 is delivered but for the red coat on creatures, which comes with K3, when something can hurt them (§5). K3, melee arms and armour, began the same day and is done on 2026-11-11 (decision D21 below; its DoD `AnArmedCrawlerFightsBack` passes). The producer's look at the first fights passed the same day ("Looks good"), with the AFK directive for K4–K5 ("I will be afk you will be responsible for finishing K4-K5"). K4, loot, is done the same day (decisions D22–D26 below, for the producer's return; its DoD `KillsFeedTheForge` passes). K5, death and rescue, is done the same day under the same directive (decisions D27–D34 below; `TheCarefulLoseFew` passes live; finding F6 recommends moving `AFriendCarriesTheFallenOut` and `DeathHasItsCost` to K7, since nothing can down a crawler in danger in live play before the Clear order). It stops for the producer's look at death and rescue. Status is owned by [ROADMAP.md](../ROADMAP.md). Decisions and deviations: `.agents/architecture_history.md`. The working plan, with every step stamped: `.agents/epic10_part3_plan.md`.
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
| Q10 | **Melee weapons** | Three, in the weapon slot (placeholder names):<br>• **a cudgel**, which the caravan supplies cheaply;<br>• **an iron blade**, made at the forge from ore;<br>• **a glowstone edge**, made from ore and glowstone: the best.<br>They wear per hit (`WearRule.PerHit`, since K1.3), and the forge's grades and repair apply. | Weapons from the caravan only |
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
- **D7, wear per landed hit:** only the weapon slot's piece wears, and only on a hit; a miss, a bare hand, the tool and armour wear nothing from a swing. How armour wears from blows taken is K1.3's and K3's. K1.3 made it a rule of the piece, not of the slot: what wears by `PerHit` wears on a blow landed, what wears by `PerBlowTaken` on a blow taken (`CombatRules.WearOnBlowTaken`); K3 gives armour its rule.
- **D8, the swing's timer belongs to its owner:**
  - A fighter's next swing is a tick (`NextSwingAt`), kept by whoever owns the fighter: the creature's record (K2), or the crawler's state (K3).
  - Whether it is saved is K2.4's call. K11.1 asks a save mid-fight to load byte for byte, which argues for saving it.

### K2's decisions (2026-11-08), for the developer's review
K2.1's choices and K2.2's rule, the creature's tick; the step is supervised, so the developer reviews these before K2.3.
- **D9, the creature's modes** (`CreatureMode`, a short state machine on the main thread, not a planner):
  - **Rest:** at its place in the lair; it heals there, whole again over its kind's `HealHours`; rested, it roams.
  - **Roam:** it walks to a point near its lair, in its sector, then rests again.
  - **Chase:** after a crawler it noticed; it walks into striking reach and stands there to strike.
  - **Flee:** a kind that flees, hurt under its line (rats under 40%), runs for its place and heeds nothing; it then rests, noticing nothing and not roaming until it has healed back over the line.
  - **Home:** its quarry lost (downed, gone, or past the leash), it walks back to its place and rests.
- **D10, what it sees:**
  - a crawler on its feet, within its kind's `Sight`, with no wall between (the snapshot's line of sight), the nearest first, the lower id on a tie;
  - not other creatures, and not the fog: creatures know their own ground;
  - a downed crawler is nobody's quarry: until K5 it limps out, as from a hazard, so nobody dies.
- **D11, the leash and the path:**
  - the leash is measured from the lair's heart to the **quarry**: a creature chases a crawler while it stands within its kind's `Leash` of the heart, and lets it go past that;
  - paths come through the existing A* (a `GridAStarSearcher` of the creatures' own, on the main thread, against the live snapshot), planned again when the goal moves more than a unit from where the path ends;
  - the creature moves at its kind's speed a second (physical time), through its entity, so a view draws it between ticks.
- **D12, the phase** (seam A4; the combat rule's D6): after the crawlers' runtime, every creature in id order decides and moves; then every due swing is decided from where all stand after moving; then all the harm lands. A creature swings at most once a tick, so two rats' blows on one tick both land.
- **D13, a blow on a crawler:** the crawler notes who swung and when, hit or miss (`AgentState.StruckAtTick`, `StruckBy`: K2.3's flight reads them; unsaved, as `HurtThisTick` is). A hit takes the mitigated attack from its health, shows as the red coat, wears what it wears against blows (D7's `PerBlowTaken`), and at none downs it, as a hazard does (`TimesDowned`, the chronicle's `CrawlerDowned`).
- **D14, hunting comes with flight:** in the live session creatures rest and roam from K2.2, but notice crawlers only from K2.3 (`CreatureService.Hunting`, off), when crawlers can flee and keep away from known lairs. A step where crawlers are struck with no way to answer would fail the day's checks for nothing. The rule's tests switch it on; K2.3 removes the switch.
- **D15, the numbers** (placeholders, tuned by measurement in K2.3 and K3): a rest lasts 20–40 s, drawn from (seed, creature, tick); a roam reaches up to half the kind's leash from the heart; "there" is within a quarter unit.
- **K2.1's choices, for the same review:**
  - **Creatures are entities** (`EntityType.Creature`, their `CreatureState` on the entity, as a crawler's `AgentState` is), so the spatial grid, snapshots, picking and the frames carry them.
  - **Their ids come from a seeded sequence of their own** (`SimRandomSalt.CreatureId`), not the entities' allocator. Creatures coming and going never change who the next crawler or drifter is, and their personalities and looks are drawn from their ids. The counter goes into K2.4's save.
- **To weigh before K2.3:** rats run at 2.2 and crawlers walk at 2, so a crawler fleeing a rat's chase takes blows until it is out of the leash. From overgrowth.3 that is about 6 units, roughly 3 s and 25 health against a full warren. A full warren downs a crawler who stands its ground in about 12 s (34 blows, measured).

### K2.3's decisions (2026-11-10), for the developer's review
The go: "Proceed" (2026-11-10); K2.2's rule (D9–D15) stands by exception. K2.3 is built as K2.3a–c (`b1758b1`, `ee9f9cb`, `e9bb1a0`) and K2.3d's prep (`ef77809`), all with hunting still off live, so golden hasn't moved. Turning hunting on is ready as a patch, and waits for this review because of F3.
- **D16, a lair's reach:** a disc of its kind's leash around its heart (`LairReach`). A creature chases only quarry within its leash of the heart (D11), so outside the disc nobody is chased. Pure map and catalogue data, read off the main thread.
- **D17, who knows of a lair:** a lair is **charted**, known to everyone at once (as a charted sector is), when a crawler on its feet sees one of its creatures in a charted sector (within its vision of 6, nothing walling it off), or is struck by one, even in the fog. The creatures' phase decides it, after the swings; the snapshot carries it to cognition (`ChartedLairMask`).
  - **Deviation from the plan:** the plan had "a scout's report charts lairs" and "remove the perception skip". A scout charts a lair as anyone does, by seeing one of its creatures; charting the sector alone doesn't, or nobody would ever stray. Creatures stay out of spatial memory: they move, so their records would go stale and spread through gossip and surveys. The sighting is the simulation's, from where crawlers and creatures stand.
  - Saved with the creatures in K2.4; until then a load forgets it, as it refills the lairs.
- **D18, a known lair's danger:** a pack's expected harm a second, in a hazard's unit, so a known lair is judged against `Vitality.LoneHazardLimit` as a hazard is (Q6). That is its kind's smallest pack, each swinging at the combat rule's hit chance, its attack cut by the crawler's defense, every swing interval: rats 6.3, beetles 2.45. Both are past the lone limit of 1, so before K3's arms nobody braves a known lair alone. Armour lowers the figure (K3).
  - Read where a lone crawler is sent: the salvage it chooses (a Gather bounty's and a hurt crawler's herb go through it), an explorer's waypoint (in a reach, the sector's own point instead, or none), an idle wander (`Vitality.WouldEnterAt`).
  - Not read where outfitting judges what gear opens: gear against creatures is K3's (K3.3, from K1.4's rule).
- **D19, flight:** struck by a creature (hit or miss) within 2 s, on its feet, a crawler drops what it does and flees.
  - The runtime pre-empts after exhaustion and before starvation, and the evaluator flees first among its danger reflexes.
  - It runs toward the floor plan's nearest bunk (as a retreat does), shown panicking (`FleeIntent`). It stops once it stands a unit clear of every lair's reach and nothing has struck it for 2 s.
  - A downed crawler limps out as from a hazard, and nobody strikes it. Flight is motion, in ticks and seconds.
- **D20, the rats' leash 8 → 6:** so the warren holds overgrowth.3 and leaves overgrowth.2 (7.8 from its heart) and the Overgrowth's survey point (6.4) clear, as the layout means (K2.1 claimed it; the leash of 8 didn't). A fleeing crawler also runs 2 units less. Held by flight: held by the warren until the first blow, a stray fled two ticks later and cleared the reach in 3.2 s for 24 health (with the leash at 8).
- **Measured with hunting on** (the patch applied), each a game day:
  - **The 23 gate seeds:** 23/23 pass.
  - **Seeds 1–120:** 119/120 with and without the guild. Seed 82 fails on one movement jitter by a Grove patch at t4240, where no creature was involved: the change of timing surfaced a crowding jitter of F5's kind.
  - **Across the 120:** the hollow is charted on all of them (a crawler working seam.2 sights a beetle), the warren on 2 (nobody charts the Overgrowth without the player). A stray is struck on 53 seeds (63 blows, 46 hits, 57 crawlers): most at seam.1, already on their way when the hollow was charted (34 of the 53 first flights began within 1.5 units of it, nearly all the rest within 4), one at the warren on a seed where the Overgrowth was charted. Every one fled; none was downed; no lone crawler went into a known lair.
  - **So K2's story emerges unaided:** a stray is hurt and flees, and the cautious stop going.
  - **The soak:** 12/15. The crafting profile fails on its three seeds (F3).
- **F3 settled by option A** (the developer, 2026-11-10: "Chose A. Proceed"), as applied in `e73b586`:
  - **A quit for want of pay is the failure, not a single unpaid visit:** members and artisans quit at their second in a row. `EconomySoak` counts `UnpaidQuits`; the guild band and the workshop loop fail on a quit.
  - **The crafting profile's makes and pay are judged across seeds 1–20** (`TheCraftingLoopHoldsOnMostSeeds`): goods on at least 80% (18/20 measured, off and on), a quit for pay on at most 15%.
    - The pay part goes one step past the option as written, "an unpaid run under the quitting line" per seed. On the soak's seed 2 an artisan really did quit (its stall broke on day 3), but pay quits are as rare with hunting as without (1 of 21 seeds off, 2 on). So it is judged across seeds, as makes are.
  - **The mid-craft save searches 12 days**, since both parties out with a make in hand first comes on day 9.2.
  - **After it:** the soak 15/15, seeds 1–120 119/120 both ways (F4).

### K3's decisions (2026-11-10 to 2026-11-11), for the producer's look at the first fights
K3 isn't supervised; these are the calls made in it, flagged for the look.
- **The arms and armour** (Q10, Q11), placeholder numbers on the bare hand's 2 attack every 1.5 s:
  - **Weapons:** the cudgel (+3 attack, +0.1 reach; 12g, from the caravan); the iron blade (+8, a swing a tenth quicker; three ore; 40g); the glowstone edge (+12, the same; two ore and a glowstone; 85g).
  - **Armour:** the iron helm (head, defense +3; two ore; 30g) and the ring coat (body, defense +6; four ore; 55g).
  - **Wear:** weapons wear by the blow landed, armour by the blow taken. Grades and repair follow.
- **D21, the bold and armed brave a lair they can clear** (Q6: a known lair is avoided "past what it would brave alone"). A bold, armed crawler on its feet treats a known lair as no danger when it expects to slay that lair's smallest pack, one by one, before its health falls to the retreat line (`Vitality.HarmToClear`).
  - **Why:** without it, lairs are charted on day one and armed residents never meet a creature, so nobody would ever fight back.
  - **What it gives:** a blade-wielder at full health works seam.1 by the beetle hollow; a cudgel isn't enough.
- **The blades tuned for it:** the iron blade went from +6 to +8 and the edge from +10 to +12, so a blade clears a lone beetle (about 51 health, from full) or three rats (about 49).
- **Upgrades:** the bold buy the best weapon they can afford and a better one later; the old one is traded in, as protection is. A cudgel bought from the caravan would otherwise have held every bold resident at a cudgel.
- **Fights resolve with the creatures' swings** (the combat rule's D6): a crawler's fight asks the creatures' pass for its swing, so every blow of a tick is decided before any harm lands. A slain creature's place waits its 48 game hours (Q5).
- **Residents buy weapons only.** Armour is the guild's to give with the Clear order (K7); a party's arms come with it too.
- **The caravan's cudgels came with their buyers:** stocked earlier, two cudgels at 6g with nobody buying tipped Part 2's fragile crafting loop.
- **Older scenarios given room** (as F3's option A did): the guild's sign-on within two days (it had signed at t9405 of t10080), the right gear's trip all in respirators within nine days (day 7.8).
- **Measured** (the 23 gate seeds, four days, the workshop player ordering blades): blades worn on 20, fights on 12, a kill on 7. The soak 15/15; seeds 1–120 120/120 with and without the guild.

### K4's decisions (2026-11-11 onward), for the producer's return
K4 was done while the producer was away (the AFK directive of 2026-11-11: "Looks good. I will be afk you will be responsible for finishing K4-K5"); these are the calls made in it.
- **D22, creatures' materials are salvage of their own origin** (`ItemCategory.Creature`): hide and chitin are resources, so the stall buys them and the caravan exports them as it does ore (K4.4). No node yields them, so no Gather bounty asks for them and no pickaxe works them.
  - **The header** counts the staples only: rations, ore and herbs. The materials are listed in the merchant window, as glowstone is. Five counts didn't fit at 1280×720.
  - **The armour made from them** (the jerkin, the chitin plate) and the rare pieces (the fang knife, the stoneheart coat) are of the same origin.
- **D23, what a kill drops** (Q12; placeholder weights, `CreatureLoot`): rolled at the kill on the seed, the creature and the tick, into a pile where it fell, claimed for its slayer for a game hour (K4.1).
  - **A rat:** a hide (70 in 100), a herb (18), its fang knife (2), or nothing (10).
  - **A beetle:** one or two rolls of chitin, one or two (72), an ore (22), its stoneheart coat (3), or nothing (3).
  - **A spinner** (for K10): chitin (60), a herb (30), or nothing (10).
  - **The rare pieces** (Q11, fixed items with a rarity): the fang knife (weapon, attack +9, a swing a fifth quicker; Epic) and the stoneheart coat (body, defense +12; Epic), above the glowstone edge and the ring coat.
- **D24, what a crawler picks up** (A5: "the victors take their pile; anyone may claim one left behind"):
  - **What it takes:** materials, food and medicine while its pack has room, and the purse. A piece of equipment it takes only if wearing it outfits it better for fighting and against every hazard, by stats (K1.4). It wears that piece at once and leaves what it replaces in the pile in its place. Nobody carries gear in its pack, since the stall doesn't buy it. So a fang knife goes to whoever it betters, and the iron blade it replaces waits for someone else.
  - **When:** a pile it sees (its vision of 6, in line of sight, charted), its own or past its victor's hour, where it would go alone (a known lair it doesn't brave keeps it out), holding something it would take. Not while hungry or worn out. The goal ranks above outfitting for danger (42 against 40): the pile is within sight, five game minutes' work.
  - **Who:** anyone, drifters too: a drifter who picks a pile over leaves with it.
  - **Its own drop at hand** (K4.4): a victor within 3 units of its own drop, its claim running, takes it even in a known lair or an uncharted sector. Hurt by its fight, a victor no longer braves the lair, and it used to walk off from its pile a step away; a stray that slew a rat in the uncharted Overgrowth didn't see its own drop. For the same reason, a hurt victor's reflex to fetch a herb waits until its drop at hand is picked up.
- **D25, the materials' trade** (K4.4; placeholder numbers):
  - **The stall** buys hide (5g) and chitin (8g), as ore (5g) and herbs (6g); their base values (3g, 5g) sit under its prices as ore's and herbs' do, so even the greediest seller (asking half again over base) takes them (at 6g and 9g nobody sold). It sells the leather jerkin (35g, defense +4) and the chitin plate (70g, defense +9), made at the forge from two hides, and from two chitin and an ore.
  - **The plate takes two chitin, not three:** a beetle drops about 1.6 chitin, and at three the forge made a plate on 1 of the 23 gate seeds in ten days; at two, on 6.
  - **The caravan** exports hide (6g) and chitin (10g), 8 a visit, past six of each the stall keeps for the forge (`CaravanTerms.Keep`, new). Taken every six hours, the forge would seldom hold two hides at once; never taken, a full shelf would stop the stall buying.
  - **The stall's goods table holds 24** (16 held today's 14 goods and not the four new ones). It grows the stall's saved record from 196 to 276 bytes inside format 11, which is unreleased, as format 10 grew twice in Part 2 and K2.4 and K4.1 added blocks to format 11. A quick-save made on this branch before K4.4 won't load.
  - **The materials don't count toward the stall's room for salvage:** no node yields them, so an empty hide shelf would keep a full stall looking open to the miners (golden 424242 moved at t2000 until this was fixed).
- **D26, armour for a bare slot** (widening K3's "residents buy weapons only"): a bold resident who is armed buys the piece that most raises its fighting outfit, a better weapon or armour for a head or body slot it leaves bare, never one in place of what it wears (hazard gear stays). Without it, nobody would wear the armour made from a kill's materials.

### K5's decisions (2026-11-11 onward), for the producer's return
K5 is being done under the same AFK directive; its two supervised steps (carrying, death) are split so that no part is Very Complex.
- **D27, a death the drifter's way** (A2): a death and a departure let go of the same things (claims, bunks, every group, the guild's and the workshops' contracts, a kept bunk, a party one short), through one shared release; the host removes the dead at the end of the tick.
  - **The departed list keeps the cause:** hazard or creature, with the hazard's or creature's kind, and the tick.
  - **The body** is a pile anyone may take from: its pack, then what it wore, and its purse. It lies until it fades (a game day), even picked clean, so the cairn stands where it fell (K5.8b).
  - **Gold on the ground** is held by the ledger's own account, as escrow is; conservation counts it, a fade sinks it (`GoldFadedOnTheGround`), and a load derives it from the piles.
  - **Inside format 11:** a pile's saved record grows from 56 to 72 bytes (whose body it is) and the departed list's records gain the cause, as K2.4, K4.1 and K4.4 grew format 11 unreleased.
- **D28, collapse and bleeding out** (Q3):
  - **Danger** is a hazard sector, or the reach of a lair holding a living creature, charted or not: danger is the world's, not what the crawlers know.
  - **Downed in danger,** a crawler collapses where it lies. It doesn't move (its walk is refused), and it lies still, which counts as tending to itself, so nothing pre-empts it. Its clock runs 2 game hours.
  - **Downed in safety,** it limps to the bunks as before.
  - **Any medicine it carries** it takes where it lies: a draught (80) stands it up at once, and two herbs (30 each) do too. Q3a named the draught; herbs were already medicine, so a forager who carries two lives.
  - **The clock stops** when it is out of danger (carried, K5.2) or back on its feet. When the clock runs out, it dies of what downed it: the hazard's or the creature's kind, and the creature for the chronicle.
  - **Saved** in the crawler's record: the clock, and what downed it (inside format 11).
  - **K2.3's test** of a stray downed by the warren limping out became "it collapses, lies still and is left alone".
- **D29, carrying** (A7, one capability): the rescuer walks to the fallen, lifts it (a game minute), and carries it at half pace toward the bunk nearest where it lay, the fallen riding at its place. It sets the fallen down as soon as they are out of danger; downed in safety there, the fallen limps on itself.
  - **A carry holds** only while the rescuer's carry runs for this fallen and both are as they were: struck, downed, gone or turned to something else, the two part where they are, and a fallen still in danger lies collapsed with its clock running.
  - **Not saved:** work in progress, as intents are. After a load the fallen lies where it was and is rescued afresh.
  - **The anomaly watch** doesn't count the fallen or the carried as stacked pawns.
- **D30, who comes** (Q3b): a crawler collapsed in danger cries out. A crawler on its feet within 15 units, through walls, comes if the fallen is a friend (its warmth toward it 30 or more, the same line as grief) or a party mate, and if it would itself go where the fallen lies.
  - **Already there:** a rescuer standing in the fallen's hazard sector (a party mate working beside it) needn't pass the lone crawler's judgement to cross the room it is in.
  - **What it does:** with a medicine that alone stands the fallen up (a draught: 80 against the recovery line of 60), it hands that over (two game minutes); otherwise it carries the fallen out.
  - **Priority 90,** above every goal but a drifter's leaving: a life is on a clock.
  - **Both remember:** aid given and received, the rescued warming to its rescuer and trusting it; the chronicle tells it (`CrawlerRescued`).
  - **Why earshot and not sight:** a crawler down in a hazard is usually alone, and sight is 6 units. Earshot lets a friend working nearby come. The 15 is unmeasured; the DoDs' measurement will say whether it is right.
- **D31, the living remember** (Q4):
  - **Grief:** each crawler whose warmth toward the dead is 30 or more (the friend line) remembers a grief and its frustration rises by 25. The portrait's frustrated face shows it; a grieving face of its own is deferred (§5).
  - **Under orders:** if the dead was a guild member on an expedition, every other member's trust toward the patron falls by 15 (and warmth by 5).
  - **The chronicle** tells the death (downed by a creature's kind, or by a sector's danger), the rescue (carried out, or a draught given) and the newcomer. A death's extra packs the cause and its kind.
- **D32, a newcomer** (Q4): 2 game days after a resident's death (not a drifter's), a resident settles at a spawn point clear of crawlers. It comes with a new look and name, a seeded personality, the session's starting purse (a faucet, `NewcomerPurses`) and a resident's arrival hunger. The ones still due are saved in the population block, inside format 11.
- **D34, the client's dead** (K5.8):
  - **On the map:** the fallen lie on their side where they fell; the carried lie across their rescuer, at the catalogue's ride placement, with no shadow of their own; a body is a cairn while its pile lies.
  - **The dossier of the dead:** the map's selection still drops a crawler that leaves the world (a drifter walking out), but the dossier keeps one that died until something else is selected. It shows the name, its death in place of the description ("Downed by a stone beetle; bled out, Day 2 · 14:20"), a DEAD badge and its portrait greyed. The dashboard reads the departed list through a small interface the host implements (`IDepartedRoll`).
  - **A scene:** a rescue starts the demonstrations' rescue scene (development builds, as the other demonstration scenes are; a game hour apart at the most often).
  - **Deferred:** a scene when a friend sees a death. The director's scenes need both actors on the map, and the dead are gone; a one-actor interaction is the director's next piece (§5).
- **D33, `TheCarefulLoseFew`'s band** (the producer's to set; this is a default): at most one death across the 23 gate seeds' day, with no player action. Measured at K5: none.

### K6's decisions (2026-11-11), at the producer's "Proceed with K6-K8"
- **D35, a hunt's party beside its contract** (Q7): the bounty contract keeps its 64 bytes and stays open with no claimant, so the single-claim chain (claim, gather or scout, deliver) never takes a hunt. A `HuntRecord` holds the lair and up to three claimants with the damage each has dealt. A claim at the board joins the party while it has room.
  - **Paid when the lair is empty,** checked after the creatures' phase: the escrow by damage dealt, floors first, the rest to the most damage; an equal split if none was dealt. A hunt nobody joins waits for a claimant, or its expiry's refund.
  - **Posted only on a charted lair with a creature in it;** its sector and heart are the lair's.
- **D36, who hunts:** a resident on its feet over the recovery line, not hungry or worn out, bold and armed, who braves the lair (D21). It joins the open hunt that pays most, walks to the lair and fights its nearest creature, a game hour at a time, until the lair is empty or the retreat line sends it off. Struck while hunting, it hunts on. A hurt hunter stays in the party and heals first. Weighted as a bounty: 35 plus the reward, by greed and diligence, less hunger.
  - **Two hunters on one creature keep clear of each other:** the hunter's step respects other crawlers and waits where one is in the way (a fight's step doesn't, as before).
- **D37, the watch's lone entries:** an entry into a known lair is now a step into its reach unbraved. A brave crawler whose health falls while it is there, or a victor stepping in for its own drop at hand (D24), isn't one.
- **The scripted hunt player** posts on the first charted lair with a creature (or a kind it is given) once a bold resident would brave it at full health, keeping 150g in the treasury besides the reward. Posting while nobody could take it on left 60g in escrow for two days and starved the stall's purchases (seed 4).
- **Measured** (the 23 gate seeds, eight days, with the workshop player making blades): a hunt paid on 21; two or three hunters on most; every day held.

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

### K1. The equipment system (done 2026-11-08, `feat/epic10-part3`; the producer's look passed: "K1 looks good")
- [x] **Slots as a table** (K1.1): `EquipmentSlots`, `SlotKind`, `BodyRegion`; head, body, weapon and tool.
- [x] **Storage for 16 slots** (K1.2): `WornEquipment`, saved whole; save format 11.
- [x] **Stats and wear over every slot** (K1.3): `StatRules`; every piece wears by its own `WearRule`.
- [x] **Outfitting by slot, by stats** (K1.4): `Outfitting`; the live scenario `GearByTheSlot` (a hard hat over hazard gear on 17 of the 23 gate seeds).
- [x] **The client from the slot table** (K1.5): `WornPieces`, `WornLine`, `WornLook`; hazard gear is a suit worn on the body.
- [x] **A slot added as data** (K1.6): `EquipmentSlotProofTests` in both test projects; AGENTS.md's "Adding an equipment slot".

### K2. Creatures (2026-11-08 to 2026-11-10, `feat/epic10-part3`; K2.2 and K2.3 reviewed by the developer)
- [x] **Lairs** (K2.1): map data, filled on the seed; creatures are entities with ids of their own.
- [x] **The creature's rule** (K2.2, D9–D15): rest, roam, chase within the leash, strike, flee, go home and heal, in a phase of its own.
- [x] **Crawlers meet creatures** (K2.3, D16–D20): a lair's reach and danger; charting by sight or by a blow; lone crawlers keep out of known lairs; flight; hunting live; the rats' leash 6.
- [x] **The save** (K2.4): `CreatureSave` in the world's block; the world hash holds the creatures.
- [x] **The client** (K2.5): generated side-view sprites in both view modes, mirrored by the way they go, hidden by the fog; the strike pose; a creature selected and shown in the inspector, its kind's card in the portrait's place.
- [x] **The DoD** `VerminDriveTheLoneAway`: on the 23 gate seeds' day the hollow is charted on every seed; a stray is hurt and flees on 8 (at least 5 asserted) and gets away; nobody goes alone into a known lair; the days hold.
- [x] **The gate** (2026-11-10): core 214/214, client 497/497 in the pinned image, golden 3/3 (re-recorded twice: hunting live, then the creatures in the hash), the soak 15/15, seeds 1–120 119/120 with and without the guild (F4), the MonoGame and Android compiles with 0 warnings.

### K3. Melee arms and armour (2026-11-10 to 2026-11-11, `feat/epic10-part3`; waits for the producer's look at the first fights)
- [x] **Melee weapons** (K3.1, Q10): the cudgel, the iron blade, the glowstone edge; their recipes and the stall's terms; the caravan's cudgel; wear per hit, grades and repair.
- [x] **Armour from ore** (K3.2, Q11): the iron helm and the ring coat.
- [x] **Fighting back** (K3.3): the bold and armed fight the creature that struck them, their swings in the creatures' pass; the bold buy a weapon and a better one; they brave a lair they can clear (D21); kills, the chronicle's `CreatureSlain`.
- [x] **The client** (K3.4): weapons in hand on bodies and figures; the merchant terminal's and the station window's lists scroll.
- [x] **The DoD** `AnArmedCrawlerFightsBack`: on gate seed 18 a bold resident buys an iron blade, braves the beetle hollow, fights and slays a beetle at t14382; the days hold.
- [x] **The gate** (2026-11-11): core 219/219, client 500/500 in the pinned image, golden 3/3 (re-recorded for the purchases), the soak 15/15, seeds 1–120 120/120 with and without the guild, the MonoGame and Android compiles with 0 warnings.

### K4. Loot (2026-11-11, `feat/epic10-part3`; under the producer's AFK directive, its decisions D22–D26 for the producer's return)
- [x] **Piles** (K4.1, A5): items and a purse on the ground, claimed for their victor for a game hour, fading after a game day; saved in the world's block; in the world hash.
- [x] **Drops** (K4.2, Q11, Q12): each kind's loot table rolled at the kill (D23); hide and chitin, creatures' materials of their own origin (D22); the fang knife and the stoneheart coat, rare pieces; a rolled piece comes whole.
- [x] **Picking up** (K4.3, D24): the victor takes its drop, anyone a pile past its claim, by one loot rule (a better piece worn, the replaced one left; materials while they fit); a victor's own drop at hand through fog and lair.
- [x] **The materials** (K4.4, D25, D26): the stall buys hide and chitin and sells the leather jerkin and the chitin plate, made at the forge; the caravan takes the surplus past the forge's reserve; armed residents buy armour for a bare slot.
- [x] **The client** (K4.5): piles drawn as their rarest item; item names in rarity's colours in the merchant terminal and the gear lines.
- [x] **The DoD** `KillsFeedTheForge`: on gate seed 18 a beetle's chitin is picked up, sold, made into a plate, and a resident wears it at t33877; armour from a kill is worn on 5 of the 23 gate seeds in ten days.
- [x] **The gate** (2026-11-11): core 232/232, client 502/502 in the pinned image, golden 3/3 unchanged (its runs end at t3000, before any kill), the soak 15/15, seeds 1–120 120/120 with and without the guild, the MonoGame and Android compiles with 0 warnings.

### K5. Death and rescue (2026-11-11, `feat/epic10-part3`; under the producer's AFK directive, its decisions D27–D34 for the producer's look)
- [x] **A death, the drifter's way** (K5.4, A2, D27): the dead let go of what a departure does and leave at the tick's end; the departed list keeps the cause; the body is a pile anyone may take from, a cairn until it fades; gold on the ground is the ledger's own account; a dead leader is succeeded.
- [x] **Collapse and bleeding out** (K5.1, Q3, D28): downed in danger, a crawler collapses where it lies and bleeds out in 2 game hours; downed in safety it limps; a medicine it carries stands it up.
- [x] **Carrying** (K5.2, A7, D29): lifted and carried at half pace out of danger, the coupling ended however the carry ends.
- [x] **Rescue** (K5.3, Q3b, D30): a friend or a party mate within earshot (15) who would go there, or already stands in the room, comes: a draught handed over, or a carry out.
- [x] **The living remember** (K5.5, Q4, D31): grief, the patron's trust lost under orders, the chronicle's lines.
- [x] **A newcomer** (K5.6, D32) two game days after a resident's death.
- [x] **The saves** (K5.7): the bleeding clock and what downed it, the departed's causes, bodies, the newcomers due; gold on the ground derived on a load.
- [x] **The client** (K5.8, D34): the fallen lie, the carried ride, the dead are cairns; the dossier of the dead, its portrait greyed; a rescue's scene.
- [x] **The DoD** `TheCarefulLoseFew`: no death on the 23 gate seeds' day (band default D33).
- [x] **The gate** (2026-11-11): core 248/248, client 506/506 in the pinned image, golden 3/3 unchanged (nothing dies in a golden run), the soak 15/15, seeds 1–120 120/120 with and without the guild (no death on any), the MonoGame and Android compiles with 0 warnings.
- **Not yet live (F6):** `AFriendCarriesTheFallenOut` and `DeathHasItsCost`. Their rules are proven on the real outpost by hand (`RescueTests`, `BleedingTests`, `DeathTests`); nothing in today's game downs a crawler in danger, so the recommendation is that they move to K7, beside `AnArmedPartyClearsANest`.

## 5. Deferred and out of scope

| Left out | Owner |
| :--- | :--- |
| The red coat on creatures (K2.5): now that crawlers hurt them (K3.3) | Epic 10 Part 3, after the look at the first fights |
| Armour and arms given to a party | Epic 10 Part 3, K7 (the Clear order) |
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
| `AFriendCarriesTheFallenOut` and `DeathHasItsCost` live (F6: no live downing before the Clear order) | Epic 10 Part 3, K7 (F6's option A, the producer's to confirm) |
| A grieving face of its own in the portrait (D31: grief shows as the frustrated face) | Epic 10 Part 3, K5.8 or later |
| A dynamic scene when a friend sees a death (D34: the director needs a one-actor interaction) | Epic 10 Part 3, later; the narrative work |

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
- **F3 (2026-11-10, K2.3d): live hunting presses Part 2's crafting evidence.** With hunting on:
  - **What fails:** the crafting soak fails on its seeds 20261009, 1 and 2, and `WorkshopLoopScenarioTests` fails on gate seed 1:
    - a guild member goes unpaid for one caravan visit (members quit at two);
    - on 20261009 the forge makes nothing within the three-day horizon;
    - the save scenario never finds both parties out with a make in hand.
  - **How much is the creatures':** over seeds 1–20 the crafting profile passes 17/20 with hunting off and 14/20 with it on. The day-3 treasury averages 160g off and 147g on (−8%), and the seeds with an unpaid visit go from 1 to 6, each a single visit.
  - **Two causes:**
    - Some of it is the epic's own outcome #1: seam.1, a rich vein, is lost once the hollow is charted.
    - Most of it is fragility. The profile already fails 3/20 with hunting off. The forge's respirators need glowstone, which arrives with expeditions around hour 68, at the horizon's edge, and any change of timing moves it across.
  - **The options, for the developer:**
    - (A) Judge Part 2's bands by what the game's rules make matter: an unpaid run under the quitting line (`WorstUnpaid < MaxUnpaidVisits`), and makes over the horizon or across more seeds. Revisit with K4.4's materials and the Part 2 play session (its F14).
    - (B) Retune Part 2's economy for creatures now: F14 is the producer's, at the play session.
    - (C) Soften the creatures until K3, with the hollow off seam.1. No vein is lost, but strays then come only from passers-by.
  - **Recommended: (A).** The pressure is the epic's design, and K3's arms lift it. The bands' zero tolerance is stricter than the rule they guard, and retuning before F14's play session and K3's arms would be tuned again twice.
  - [Possibly Complex: test bands of Part 2's evidence; the developer decides]
  - **Settled 2026-11-10 by option A** (the developer's choice; how it was applied is under K2.3's decisions above).
- **F4 (2026-11-10, K2.3d): one jitter on seed 82.** With hunting live, seeds 1–120 pass 119/120 with and without the guild. Seed 82 fails on one movement jitter (3 reversals in 5 ticks) by a herb patch in the Grove at t4240, where no creature took part (no blows on that seed). The day's changed timing surfaced a crowding oscillation of Part 1's F5 kind. Seed 82 isn't a gate seed. [Basic: watch; look at the crowding step if another seed shows one]
  - **2026-11-11:** with K3 in, seeds 1–120 pass 120/120 again. A four-day probe on the gate seeds showed one Ore Quarry jitter on seed 8 on day 2, again with no creature or fight in it.
  - **Again at K2.1:** another allocation test, `FacilityZoneServiceTests.QueriesWithoutARegistryAreEmptyAndQueriesWithOneAllocateNothing`, failed once in a full run of the client's tests; it then passed 3 times alone and in a full rerun (487/487). K2.1 touches neither the facility zones nor their queries. Two different allocation tests failing once each points at the measuring (allocations by the runtime on the test's thread), not at the code measured. [Possibly Complex: measuring, if a third shows]
- **F5 (2026-11-11, K4.4): thrashing at the stall on seed 8 with the workshop player.** Over six days with the workshop player making blades, gate seed 8 shows a crawler thrashing (Thinking and Walking four times in 0.4 s) by the stall at t8962, and a jitter. The same run at K3's gate (`27b4eb7`) shows the same anomaly at the same tick, so it is older than K4; seed 15 over ten days shows one too, at t740, the same at K4.3 (`5bf26a7`). The gate's one-day scenario holds on both (no workshop player). Neither is a gate check today: K4's DoD runs seed 18. [Possibly Complex: a planning loop at the stall; look when the guild's or workshop's soak shows one]
- **F6 (2026-11-11, K5's DoDs): nobody is downed in danger in live play, so K5's rules never fire live.**
  - **Measured, with nothing injected:**
    - the 23 gate seeds' day with no player: no crawler downed by a creature, no death (seeds 1–120 alike, with and without the guild);
    - ten days of the forge loop with fights, on the 23 gate seeds: kills on 14 seeds, but nobody downed by a creature and no death (fighters flee at the retreat line and get away);
    - eight days of the guild player sending four ungeared, under the Bold rule, into the Drowned Gallery: about ten trips a seed, the lowest health 25–32 (15 once), nobody downed. The expedition's retreat brings everyone out, as F1 found for gear.
  - **So:** `TheCarefulLoseFew` is live and passes. `AFriendCarriesTheFallenOut` and `DeathHasItsCost` need a downing in danger, and no command the player has today can bring one about. `DeathHasItsCost`'s own wording sends a party against beetles, which only K7's Clear order can do.
  - **The options, for the producer:**
    - (A) Move the two DoDs to K7, beside `AnArmedPartyClearsANest` (which already carries the fallen out), and take K5 as delivered with its mechanism tests on the real outpost (`RescueTests`, `BleedingTests`, `DeathTests`) and `TheCarefulLoseFew` live.
    - (B) Make danger bite now: a lower retreat line, faster rats, or crawlers who don't break off a fight. That retunes K2's and K3's calibrations before anything needs it.
    - (C) Pull a minimal Clear order into K5. That is half of K7 done twice.
  - **Recommended: (A).** The rules are proven on the real session by hand. The DoD rule wants an outcome that emerges, and nothing in the game can make a crawler fall in danger until the player can send people into a lair. Tuning danger up now would be undone by K7's own tuning.
  - [Possibly Complex: where two DoDs live; the producer decides]

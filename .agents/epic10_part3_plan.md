# Epic 10 Part 3: creatures, combat and the gatekeeper (draft plan)

Working document (AGENTS.md, Documentation Protocol 6), one of Epic 10's plans ([index](epic10_plan.md)). First written 2026-10-29 as "the descent". **Re-scoped 2026-11-07 by the producer:** "I want to re evaluate what we are tackling in epic10_part3, The focus should be combat system, equipment improvements, death, loot, creatures, bosses / gate keepers. We should not focus on floor 2 at all yet". This draft replaces the earlier one, which git keeps.

**Answered in part, 2026-11-07.** The producer answered:
> Q3 hazards should also kill if the downed crawler is not rescued.
> Q4 looks good.
> Q2 melee first, then range. Seeded hit rolls.
> Q11 Add more equipment slots, for now we should have 2 slots but I want us to build the foundation of a more complex appendable equipment system.
> Q1 the milestone 4 should end here with functional combat system.
> Add K9 - skill system

What the answers changed in this plan:
- **Ranged combat** joins Part 3 as K8, after the whole melee loop works.
- **An appendable equipment system** is built first, as K1. Its first two slots are head and body, beside the weapon and the tool.
- **Rescue is part of death:**
  - a crawler downed in danger can no longer limp out of it;
  - carrying the fallen moves from the guild's milestone into K5, so that anyone can do it;
  - friends rescue each other.
- **A skill system is K9.** It replaces Q9's veterans and takes over W4's practice.
- **Milestone 4 ends with Part 3:** a functional combat system.

The milestones are renumbered K0–K11, which puts the skill system at K9. Three answers needed a reading, flagged in §4: Q1's Epic 11, Q3's rescue and Q11's two slots.

**Part 3 is QUEUED.** The questions still unanswered (Q5–Q8, Q10, Q12–Q21) stand as defaults; the producer answers only where they disagree. The go is the producer's word, after Part 2's close-out. At K0.1, §1–§4 move into the epic doc.

- **Sources:**
  - **The producer's re-scope and answers** (above).
  - **The game design:**
    - §3: the upper floors have "minor vermin, low-value salvage", and the deeper floors "lethal mob packs". "Advancing to the next floor requires outfitting a party capable of defeating a sector boss guarding the descent gate."
    - §2: "Cleared sectors permit direct camera observation of simulated micro-encounters (campfires, node disputes, combat)."
  - **Epic 8's intent:** "Directives will drive squad-level combat flanking, focus-fire, and tactical retreat."
  - **What Parts 1 and 2 left to Part 3:** a weapon (`Attack` and `Defense` are unused); carrying a downed member; loot tables in play; rarity in the client (Part 1 §5); armour against creatures; combat and death (Part 2 §5).
  - **The guiding lights:**
    - **Majesty:** heroes fight because a reward draws them. The player flags a lair, and the bold answer. They buy their own arms and potions, flee when hurt, and grow into veterans.
    - **RimWorld:** fights are short, readable and dangerous. The downed bleed out unless someone carries them to safety. Animals drop leather that becomes armour, skills grow with practice, and a death is felt by those who knew the dead.
- **What left Part 3:** Floor 2, floors as data, the move and what it carries, and Floor 2's pressure (the old L0.3–L0.4, L4 and L5). They go to Part 4, the descent, in Milestone 5, which is not drafted. Scouting as a guild order (the old L3.1) goes with them; scout bounties already chart sectors.
- **Starts:** after Part 2's close-out (its play session, W6.5), on the producer's go.
- **Estimate:** 31–44 agent days:

  | K0 | K1 | K2 | K3 | K4 | K5 | K6 | K7 | K8 | K9 | K10 | K11 |
  | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: |
  | 2–3 | 3–4 | 3–4 | 2–3 | 2–3 | 4–5 | 2–3 | 2–3 | 3–4 | 3–4 | 3–4 | 2–3 |

  This is rough, as Part 2's first estimate was; K0 measures first.
- **Complexity** ([rubric](rules/complexity-grading.md)): **Very Complex** as a whole. It is epic-sized, and its systems have no precedent (combat, creatures, death, ranged, skills).
  - K1, K2, K5, K8, K9 and K10 are Very Complex: each is split below, and the developer supervises its steps stamped Very Complex.
  - K0 holds one supervised step (K0.3).
  - K3, K4, K6, K7 and K11 are Possibly Complex.
- **The producer's stops:**
  - the go;
  - looks at K3 (the first fights), K5 (death and rescue) and K8 (ranged);
  - a play session at K11.

## 0. What Parts 1 and 2 hand over (checked at K0)
- **Health:** a crawler has 100. A hazard sector takes health by kind, through worn gear. At 0 a crawler is downed and limps to a bunk at 40% of its speed, getting up at 60. Nobody dies (Part 1's Q3).
- **Being hurt shows:** `AgentState.HurtThisTick` and the blinking red coat (S3, D71) work for any damage. Combat feeds them as it is.
- **Gear:** three equipment slots, fixed by an enum: protection, weapon and tool.
  - Protection holds one of hazard gear, a respirator or a hard hat.
  - The weapon slot and the `Attack` and `Defense` stats are unused.
  - `WearRule.PerUse` is reserved for "a weapon per hit".
- **Practice:** artisans gain grades (Standard, Fine, Masterwork) by making things (W4). It is the only practice in the game.
- **The workshops:** the forge and the still, with grades and repair. The stall's goods table holds 10 goods; 9 are used.
- **Loot:** loot tables and `LootRoller`, deterministic on `SimRandom` (item system I7), are not wired into play. `ItemRarity` has five tiers, and the sprite kit already draws icon plates with rarity rims.
- **Expeditions:** muster, march, work, return, deliver, with a retreat rule. Two parties of up to four can be out at once, never in the same sector. Draughts heal 80 at once.
- **Kindness:** the altruism evaluator, which shares resources with friends, is the precedent for rescue.
- **Leaving the world:** a drifter is removed at the end of the tick (`SessionHost`, `CrawlerDeparted`), and `DepartedCrawler` records who left.
- **The map:** Floor 1 has 13 sectors. Two of them, the Deep Seam and the Overgrowth, are charted only by a scout's report.
- **The planner:** 41 of 64 facts used.

## 1. Goal
**The player promise:** the deep sectors bite back. Arm the outpost, put a price on what lurks there, send the guild against the brood and break the gatekeeper. Any crawler you send can die, unless someone brings it home.

What Parts 1 and 2 leave undone:
- **Nothing threatens a crawler but the air and the rock.** Hazards hurt but never kill, so risk is a cost in time, never a loss.
- **The forge has nothing to arm.** Weapons have no use, and armour only means hazard gear.
- **Crawlers never get better at anything,** except an artisan at its station.
- **Loot is a foundation with no floor on it:** the haul is always glowstone.
- **The floor has no climax:** no boss, no gate, nothing to work toward past a full guild.

## 2. The loop
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

## 3. Positions this draft takes (argued, for the producer to overturn)
- **The player never commands a fight.** Fights come from bounties, orders and stock, as everything else does. A direct "attack this" button would make Simpiens a tactics game, and the game design's whole premise is indirect agency.
- **Combat is watched, so it runs in physical time.**
  - Swings, shots, lunges and flight are motion: ticks and seconds, as walking is ([time-model.md](../docs/time-model.md)).
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

## 4. Questions for the producer (defaults; answers by exception)
Answered rows say so. Every other default stands unless the producer says otherwise.

| # | Question | Default, or the producer's answer | Alternative |
| :-: | :--- | :--- | :--- |
| Q1 | **Name, home and the milestone** | **Answered: Milestone 4 ends with Part 3,** a functional combat system.<br>• **Milestone 4** becomes "Combat & the Gatekeeper": Part 3, with Epic UI-5 folded in, in `docs/epics/epic-10-part-3.md`, named "Epic 10, Part 3: Creatures, Combat and the Gatekeeper".<br>• **Milestone 5,** "Floor Relocation": Part 4, the descent.<br>• **The reading:** for Milestone 4 to end with Part 3, Epic 11 (the MCP chronicles) moves to Milestone 5. Say so if it belongs elsewhere.<br>Epic 10 closes after Part 4. | — |
| Q2 | **Combat** | **Answered: melee first, then ranged; seeded hit rolls.**<br>• **A swing:** a fighter in reach swings on its weapon's interval (about 1.5 s). A seeded roll (`SimRandom`, its own salt) decides hit or miss: about 70% until skills (K9) set it.<br>• **A hit** does the weapon's `Attack`, cut by the target's `Defense` on a curve that never reaches zero (`Defense / (Defense + 10)` to begin with).<br>• **Order:** hits are resolved on the main thread, in one pass after movement, in entity-id order.<br>• **Unarmed,** a crawler hits for 2.<br>• **Ranged** is K8, on the same rule (Q15, Q16). | — |
| Q3 | **Death** | **Answered: hazards kill too, if the downed crawler is not rescued.** Death is permanent.<br>• At 0 a crawler is downed.<br>• **Downed in danger,** within a hostile creature's reach or in a hazard sector, it collapses. It can't limp out, and it bleeds out over 2 game hours.<br>• **Downed in safety,** it limps to a bunk as today.<br>• **Rescue stops the clock:** a rescuer carries it out of danger, or gives it a draught (at 80 health it stands and walks out).<br>• **When the clock runs out, it dies.**<br>• Creatures turn from a downed crawler to one still standing.<br>• **The reading:** to honour "rescued", the downed can no longer limp out of danger as Part 1's crawlers do. Two sub-defaults follow:<br>&nbsp;&nbsp;– **Q3a:** a downed crawler may drink a draught it carries. Preparing counts as rescue, and draughts are the player's stock at work.<br>&nbsp;&nbsp;– **Q3b:** friends rescue friends, not only party members. Otherwise a lone crawler downed in a hazard always dies. | Q3a: only another crawler can give the draught. Q3b: only the guild rescues |
| Q4 | **What a death costs** | **Answered: as proposed.**<br>• **The body:** its pack, worn gear and purse lie where it fell, as a loot pile that anyone may recover.<br>• **The guild:** the dead member's contract and kept bunk end, and its party is one short.<br>• **The living:** friends (warmth 30 or more) grieve, through a memory event and a mood. If a member dies under the guild's orders, the other members' trust toward the patron falls.<br>• **Memory:** the chronicle tells it, and the departed list keeps the dead with their cause.<br>• **A newcomer** settles at the Road Gate 2 days after a resident dies, with a new look and name. | — |
| Q5 | **Creatures** | • **A new kind of entity,** plain data, saved. Its rule: rest at its lair, roam its sector, chase what it sees within a leash, strike in reach, flee when hurt if its kind flees, go home and heal over the day.<br>• **A catalogue of kinds,** like the item catalogue, as data: health, `Attack`, `Defense`, speed, reach, swing interval, sight, leash, flight, loot table and art.<br>• **Three kinds** (placeholder names):<br>&nbsp;&nbsp;– **burrow rats**, weak, in packs of three or four, and they flee;<br>&nbsp;&nbsp;– **stone beetles**, armoured and slow, where a weapon matters;<br>&nbsp;&nbsp;– **cave spinners**, which spit from range, on the gate's approach (K8, K10).<br>• **Lairs are map data:** rats in the Overgrowth, beetles in the Deep Seam. They are spawned from the session's seed, and a cleared lair refills after 2 game days.<br>• **No raids:** creatures keep to their sectors. | (a) Creatures as moving hazards, hurting by presence with no fight. (b) Raids on the outpost at night: later |
| Q6 | **Lone crawlers** | • A crawler judges a **known** lair as it judges a hazard: it avoids one past what it would brave alone.<br>• **Attacked,** it flees (a reflex beside retreat), unless it is bold and armed: then it fights back.<br>• **The bold buy their own weapons** at the stall when their purse spares it, as residents who mine buy pickaxes (D39).<br>• **A scout's report** charts the lairs of the sector it charts. | Every crawler flees; only the guild fights |
| Q7 | **Hunt bounties** (Majesty's flag) | • **A third bounty kind** beside Scout and Gather: "Hunt", on a charted lair, paid from escrow when the lair is empty.<br>• **Up to three claimants,** the bold and armed. The reward is shared by the damage each one dealt.<br>• If the lair refills before it is cleared, the bounty runs on until it expires. | No hunt bounties: only the guild fights |
| Q8 | **The guild's Clear order** | • **A new order** for an expedition: a lair as the target. Its stages: muster, march, **fight**, return, deliver. The haul is what the party picked up.<br>• **Focus fire:** the directive names one target at a time, the party leader's choice (the nearest, then the weakest).<br>• **Retreat:** the party turns back when half its members are downed, or its health is spent past a line.<br>• **Rescue under orders:** members carry their fallen out (K5's carrying).<br>• Flanking is left for later. | — |
| Q9 | **Veterans** | **Folded into K9, the skill system** (the producer's "Add K9"): veterans are crawlers with high combat skills. | — |
| Q10 | **Melee weapons** | Three, in the weapon slot (placeholder names):<br>• **a cudgel**, which the caravan supplies cheaply;<br>• **an iron blade**, made at the forge from ore;<br>• **a glowstone edge**, made from ore and glowstone: the best.<br>They wear per hit (`WearRule.PerUse`), and the forge's grades and repair apply. | Weapons from the caravan only |
| Q11 | **Equipment** | **Answered: more slots, two for now, on the foundation of an appendable equipment system (K1).**<br>• **The reading:** the two slots are **head** and **body**, which replace protection. The weapon and the tool slots stay.<br>&nbsp;&nbsp;– the head takes the hard hat, the respirator (worn as a mask) or an iron helm;<br>&nbsp;&nbsp;– the body takes hazard gear or armour: a ring coat from ore (K3), and a leather jerkin from hides or chitin plate (K4).<br>• **A real choice:** a party bound for a lair in a hazard sector picks armour or a hazard suit.<br>• **The foundation:**<br>&nbsp;&nbsp;– slots are a table, not an enum;<br>&nbsp;&nbsp;– an item fits a slot kind, and a kind may later have several slots (two rings, two hands);<br>&nbsp;&nbsp;– storage keeps room for 16 slots, so a new slot needs no save format change;<br>&nbsp;&nbsp;– stats, wear, outfitting, the dossier and worn sprites all read the table.<br>&nbsp;&nbsp;– A new slot is a row, by a new recipe in AGENTS.md, "Adding an equipment slot".<br>• **Rarity:** rare pieces are fixed items with a rarity, dropped by bosses (for example the Matriarch's fang dagger), never random affixes. The client shows rarity's colours (Part 1's deferral). | — |
| Q12 | **Loot** | • **Piles:** a kill drops a pile at its place, rolled from its kind's loot table: its material (hide, chitin), sometimes salvage, now and then a rare piece. A boss drops a sure trophy and a rare piece.<br>• **Who takes it:** the victors pick it up, a party into its haul and a hunter into its pack. Anyone may claim a pile left behind. Piles fade after a game day.<br>• **The economy:** the stall buys the materials, the caravan exports them, and the forge makes armour from them. | Drops go straight into the killer's pack, with no piles |
| Q13 | **Bosses** | **Two:**<br>• **The Matriarch** heads the rats' warren. She is stronger and calls her brood to her at half health. Slain, she returns after a game week.<br>• **The Gatekeeper** holds the sealed descent gate:<br>&nbsp;&nbsp;– **its lair:** a new sector at the floor's edge, the Old Stair, reached through the Deep Seam and uncharted at the start;<br>&nbsp;&nbsp;– **its fight:** a slam that strikes everyone in reach, and fury at a quarter of its health;<br>&nbsp;&nbsp;– **between attempts:** it heals fully once its lair has been empty for a game hour;<br>&nbsp;&nbsp;– **beaten once,** it stays beaten.<br>• **The breach** is a saved world flag, told by the chronicle and a hard cutscene. Beyond the gate the stairs go down into the dark, for Part 4. | The Gatekeeper only |
| Q14 | **Shipping and done** | As in Parts 1 and 2:<br>• one PR, with a commit per step;<br>• each milestone's DoD is a live scenario on the real session, with nothing injected;<br>• a scripted player acts only through commands. | — |
| Q15 | **Ranged weapons** (K8) | • **Two,** in the weapon slot: **a sling** (caravan) and **a crossbow** (forge, from ore and hide).<br>• **A shot** needs a clear line (a ray over the nav grid's walls) and a range. Its hit roll falls off with distance.<br>• **A ranged fighter keeps its distance:** it steps back when a foe closes. In a party, ranged members stand behind the melee.<br>• **One weapon slot,** melee or ranged; a ranged fighter hits unarmed up close. | A sidearm slot for a second weapon: one row in K1's table |
| Q16 | **Ammunition** (K8) | **None:** a ranged weapon wears per shot, as a blade wears per hit. No ammunition to carry, provision or plan for. | Bolts as stacks made at the forge: a sink and a reason to restock, with one more planner fact |
| Q17 | **Which skills** (K9) | **Seven, as a table** (appendable, like the slots):<br>• melee;<br>• ranged;<br>• mining;<br>• foraging;<br>• smithing;<br>• alchemy;<br>• medicine. | More now: scouting, social or hauling |
| Q18 | **Levels and growth** (K9) | • **Levels 0–10,** named in bands: Novice (0–2), Apprentice (3–5), Journeyman (6–8), Master (9–10).<br>• **Practice:** experience from each swing landed, shot, unit mined or foraged, make and rescue. Each level costs more than the last.<br>• **No decay** at first. | Levels 0–20 with decay at the top, as RimWorld has |
| Q19 | **Starting skills** (K9) | • **Drawn at spawn** from the seed, on its own salt. Most skills start low, and one or two start higher.<br>• **Personality leans the draw:** the bold lean toward melee.<br>• **Not from a look:** no decision reads a look until lineage traits are designed (`CognitionReadsNoLookTests`).<br>• Newcomers and drifters get skills too. | All start at 0 |
| Q20 | **What skills do** (K9) | • **Melee and ranged:** the hit chance and a little damage.<br>• **Mining and foraging:** harvest speed, multiplied with the pickaxe.<br>• **Smithing and alchemy:** a make's grade and speed. **W4's practice becomes these skills;** an artisan's grade is now its skill's band.<br>• **Medicine:** carrying a fallen crawler faster, and a draught given heals more. | Skills only in combat; W4's practice stays apart |
| Q21 | **Do skills steer choices?** (K9) | **Lightly, through what exists.**<br>• A crawler weighs work by what it pays an hour (W1.4), and skill raises that, so specialists emerge with no new rule.<br>• The roster and the recruit list show skills, so the player hires for them. | Skills as an explicit preference in every work choice |

## 5. Architecture first (AGENTS.md, "Refactor and reevaluate")
Part 3 adds systems rather than rewriting old ones, with one exception: equipment. Today's slots are an enum, with protection meaning "the one protective item" in the stats, the outfitting rules and the client. The producer's "appendable" makes this the one refactor worth doing first (K1): weapons, armour and ranged weapons all land on it.

Seven seams, laid before the systems that use them:
- **A1. One combat rule over two kinds of fighter.**
  - `CombatRules` reads a fighter through a small read model (`Combatant`: health, `Attack`, `Defense`, reach, interval, range and proficiency).
  - A crawler's comes from `AgentState` and `CrawlerStats`, and a creature's from its own record.
  - Proficiency is flat until K9 fills it from skills, and range is 1 until K8.
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

## 6. Milestones
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

## 7. The milestones, phased
Each step is a commit. After each: a build with 0 warnings and `dotnet test`. The whole part is one PR into `main` (the index's rules).

### K0. Groundwork (2–3 days)
**Complexity:** Very Complex, for K0.3. The rest is Possibly Complex.
- [ ] **K0.1 Docs:** [Basic]
  - the answered draft into `docs/epics/epic-10-part-3.md`;
  - the roadmap: Part 3 ACTIVE;
  - the focus file.
- [ ] **K0.2 Measure:** [Possibly Complex: probes and reading them]
  - planning cost and the planner's facts in use;
  - the frame time;
  - the economy after Part 2's F14 decision;
  - how many residents would venture, and with what purses (weapon prices are set against them, as the pickaxe's were, F9);
  - how often a lone venturer is downed in a hazard today: Q3 turns each of those into a death unless it is rescued.
- [ ] **K0.3 The combat rule:** [Very Complex, supervised by the developer: the first fight's rule sets the pattern K2–K10 follow]
  - `Combatant`, the read model (A1), with range and proficiency in it from the start;
  - `CombatRules`: reach, interval, the hit roll on a new `SimRandomSalt`, mitigation by `Defense`, wear per hit;
  - `CombatValidator`;
  - the golden hashes unchanged: nothing fights yet.
- [ ] **K0.4 The creature catalogue:** [Possibly Complex: a new catalogue with a precedent, `ItemCatalog`]
  - kinds as data (A6), with a validator;
  - no spawns yet.
- **DoD:** the golden hashes, the 23 gate seeds and the soak are unchanged.

### K1. The equipment system (3–4 days)
**Complexity:** Very Complex: it generalises the inventory, which hazards, harvest, outfitting, saves and the client rely on (bundling). Split as below; each step is Possibly Complex. The developer reviews K1.2's save design before K1.3 starts.
- [ ] **K1.1 Slots as a table:** [Possibly Complex: a new catalogue with a precedent]
  - `EquipmentSlots`: each slot's id, name, kind, body region, draw layer, place on the paper doll and order;
  - the first rows: head, body, weapon and tool, with protection retired;
  - items fit a slot kind (`EquipmentBlock`);
  - a validator.
- [ ] **K1.2 Storage that grows:** [Possibly Complex: the save format, once]
  - the inventory's equipment as room for 16 slot records, indexed by slot id;
  - the save copies all 16, so a new slot loads old saves with it empty;
  - the goods table at 16;
  - one format bump.
- [ ] **K1.3 Stats and wear over every slot:** [Possibly Complex: behaviour; the golden hashes may move]
  - `CrawlerStats` sums every worn slot, with each stat's stacking rule (add, multiply, a cap);
  - each piece wears by its own rule.
  - The golden hashes are re-recorded in their own commit if the hard hat and hazard gear, now worn together, change behaviour.
- [ ] **K1.4 Outfitting by slot:** [Possibly Complex: cognition and the party builder]
  - residents' purchases and the party builder pick the best piece for each slot for the target's dangers, scored by stats, not by named items or groups;
  - W3's outfitting by hazard kind becomes a case of it.
- [ ] **K1.5 The client from the table:** [Possibly Complex: Gum UI and worn sprites]
  - the dossier's and the party builder's equipment panels, laid out from the slot table;
  - worn sprites layered by each slot's draw layer.
- [ ] **K1.6 The proof and the recipe:** [Basic: a test like `ItemSystemProofTests`, and a section of AGENTS.md]
  - a test adds a feet slot and boots to a test catalogue, and shows that stats, outfitting, the save's round trip and the dossier follow;
  - AGENTS.md gains "Adding an equipment slot".
- **DoD `GearByTheSlot`:** on the gate seeds, a venturer wears a hard hat and hazard gear together. The guild's and the workshops' scenarios hold.

### K2. Creatures (3–4 days)
**Complexity:** Very Complex: a new system. Split as below. The developer supervises K2.2 and K2.3.
- [ ] **K2.1 Lairs:** [Possibly Complex: a map change, with golden and renders re-recorded]
  - lairs as map data in the Overgrowth and the Deep Seam;
  - spawns on the seed;
  - refilling on the day's time.
- [ ] **K2.2 The creature's rule:** [Very Complex, supervised by the developer: the creature's tick, its own phase in id order (A4)]
  - rest, roam, chase within the leash, strike, flee, go home, heal;
  - its paths through the existing pathfinder;
  - swings resolved in one pass after movement.
- [ ] **K2.3 Crawlers meet creatures:** [Very Complex, supervised by the developer: a new reflex in the runtime's pre-emption, and cognition]
  - flight, a reflex beside retreat and panic;
  - known lairs judged in `LoneDanger`, as hazards are;
  - a scout's report charts lairs;
  - nobody yet fights back, and nobody dies: until K5 a downed crawler limps out, as today.
- [ ] **K2.4 Saves:** [Possibly Complex: a save block] creatures and lairs in the world's block, with the round-trip validators.
- [ ] **K2.5 The client:** [Possibly Complex: new art and renders to look at]
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

### K3. Melee arms and armour (2–3 days), then the producer's look
**Complexity:** Possibly Complex: items on K1's table, and one new reflex.
- [ ] **K3.1 Melee weapons (Q10):** [Possibly Complex: a stat read in combat]
  - the cudgel, the iron blade and the glowstone edge;
  - their recipes, and the caravan's terms;
  - wear per hit; grades and repair.
- [ ] **K3.2 Armour from ore (Q11):** [Basic: item rows and recipes, by AGENTS.md's "Adding an item"] the iron helm and the ring coat.
- [ ] **K3.3 Fighting back:** [Possibly Complex: cognition, a fact and a reflex]
  - the bold and armed fight back;
  - the bold buy a weapon (Q6);
  - outfitting for creatures comes from K1.4's rule.
- [ ] **K3.4 The client:** [Possibly Complex: worn sprites and Gum UI]
  - weapons in hand;
  - the new goods in the merchant terminal and the party builder.
- **DoD `AnArmedCrawlerFightsBack`:** on a gate seed, the scripted player stocks blades; a bold resident buys one, meets vermin, fights and kills one; the day holds.
- **Stop for the producer's look** at the first fights.

### K4. Loot (2–3 days)
**Complexity:** Possibly Complex: rolls already exist; piles are a new entity with saved state.
- [ ] **K4.1 Piles:** [Possibly Complex: a new entity, saved]
  - items on the ground (A5);
  - they fade after a game day.
- [ ] **K4.2 Drops:** [Possibly Complex: deterministic rolls] each kind's loot table, rolled at the kill by `LootRoller`.
- [ ] **K4.3 Picking up:** [Possibly Complex: cognition, a goal and a fact]
  - the victors take their pile;
  - anyone may claim one left behind.
- [ ] **K4.4 Materials:** [Possibly Complex: the economy, and the soak may move]
  - hides and chitin at the stall and the caravan;
  - the leather jerkin and chitin plate at the forge;
  - the rare pieces as items.
- [ ] **K4.5 The client:** [Possibly Complex: art and renders]
  - piles on the map;
  - rarity's colours in the merchant terminal and the dossier.
- **DoD `KillsFeedTheForge`:** a hunter's kill drops hide; the hide is sold at the stall; the forge makes a jerkin from it; a resident wears it.

### K5. Death and rescue (4–5 days), then the producer's look
**Complexity:** Very Complex: a crawler leaving the world mid-session touches every system that holds one, and carrying couples two crawlers. Split as below. The developer supervises K5.2 and K5.4.
- [ ] **K5.1 Collapse and bleeding out (Q3):** [Possibly Complex: a rule in `Vitality`, saved; Part 1's downed crawlers change]
  - downed in danger, a crawler collapses where it is, and the clock runs;
  - downed in safety, it limps as today;
  - a draught it carries (Q3a) or is given stands it up.
  - The golden hashes are re-recorded in their own commit, since crawlers downed in hazards no longer limp out.
- [ ] **K5.2 Carrying:** [Very Complex, supervised by the developer: two crawlers' movement coupled, and saved] one capability (A7): a rescuer lifts a fallen crawler and carries it at half pace to the nearest safe ground.
- [ ] **K5.3 Rescue (Q3b):** [Possibly Complex: cognition, a goal and a fact, with the altruism evaluator as precedent]
  - a crawler who sees a fallen friend in danger it would itself enter carries it out, or gives it a draught it carries;
  - parties rescue under orders in K7.
- [ ] **K5.4 A death:** [Very Complex, supervised by the developer: groups and succession, contracts, bunks, parties and expeditions, memory, the ledger]
  - a death goes the drifter's way (A2);
  - the body is a pile with the purse in it, held in a ledger account for gold on the ground;
  - a dead leader is succeeded.
- [ ] **K5.5 The living remember (Q4):** [Possibly Complex: memory events, affinity and mood]
  - grief;
  - trust toward the patron after a death under orders;
  - the chronicle;
  - the departed list's cause.
- [ ] **K5.6 A newcomer:** [Possibly Complex: a population rule on the seed] 2 days after a resident's death.
- [ ] **K5.7 Saves:** [Possibly Complex: format] the dead, the bleeding clock, a crawler being carried and gold on the ground, with the round-trip validators.
- [ ] **K5.8 The client:** [Possibly Complex: art, renders, a dynamic scene]
  - a crawler carried on a rescuer's back;
  - the body and a cairn;
  - the chronicle's lines;
  - the dossier of the dead (portrait greyed);
  - a dynamic scene when a friend sees a death or makes a rescue.
- **DoD:**
  - **`AFriendCarriesTheFallenOut`:** a resident downed in a hazard is carried out by a friend and lives. Where nobody comes, one dies.
  - **`DeathHasItsCost`:** the scripted player sends an under-armed party against beetles. A member falls and isn't rescued, and it dies. Its friends grieve, the guild loses its contract, the body's pile holds its gear and purse, and a newcomer arrives. The ledger balances.
  - **`TheCarefulLoseFew`:** on the 23 gate seeds, with no player action, deaths stay within a band set with the producer.
- **Stop for the producer's look** at death and rescue.

### K6. Hunting (2–3 days)
**Complexity:** Possibly Complex: a new bounty kind with two precedents, Scout and Gather.
- [ ] **K6.1 Hunt bounties (Q7):** [Possibly Complex: a command, escrow, saved]
  - posted on a charted lair;
  - claimed by up to three;
  - paid by damage dealt when the lair is empty.
- [ ] **K6.2 The hunters:** [Possibly Complex: cognition, a goal, an action and facts] claim, go, fight in the lair, and are paid.
- [ ] **K6.3 The client:** [Possibly Complex: Gum UI] the bounty board's Hunt, with charted lairs as targets.
- **DoD `AHuntBountyClearsALair`:** the scripted player posts a hunt on the rats; bold, armed residents claim it, clear the lair and are paid. The ledger balances.

### K7. The guild goes to war (2–3 days)
**Complexity:** Possibly Complex overall: G5's stages and K5's carrying are the precedents. K7.2 is Very Complex, supervised by the developer.
- [ ] **K7.1 The Clear order (Q8):** [Possibly Complex: a new expedition kind on G5's stages]
  - a lair as the target;
  - the fight stage;
  - the haul as what was picked up.
- [ ] **K7.2 The party's fight:** [Very Complex, supervised by the developer]
  - focus fire through the directive;
  - the retreat rule;
  - rescue under orders through K5's carrying.
- [ ] **K7.3 The client:** [Possibly Complex: Gum UI]
  - the Clear order in the party builder;
  - the fight in the tracker: the party's health and its foe's.
- **DoD `AnArmedPartyClearsANest`:** the scripted player arms four and orders a clearing. They fight together, clear the nest, carry anyone fallen out and bring back loot. The ledger balances.

### K8. Ranged combat (3–4 days), then the producer's look
**Complexity:** Very Complex: a second mode of combat with line of sight and positioning. Split as below. The developer supervises K8.2.
- [ ] **K8.1 Ranged weapons (Q15, Q16):** [Possibly Complex: items on K1's table, with range in the combat rule]
  - the sling and the crossbow, their recipes and terms;
  - wear per shot;
  - the hit roll's fall with distance.
- [ ] **K8.2 Line and distance:** [Very Complex, supervised by the developer: a ray over the nav grid in the hot path, and a new movement rule]
  - a clear line before a shot;
  - a ranged fighter keeps its distance;
  - in a party, ranged members stand behind the melee.
- [ ] **K8.3 Ranged creatures:** [Possibly Complex: a kind on K0.4's catalogue] the cave spinners spit, for K10's approach.
- [ ] **K8.4 The client:** [Possibly Complex: art and renders]
  - shots drawn between ticks from the frames (presentation only);
  - the ranged goods in the merchant terminal and the party builder.
- **DoD `ArchersHoldTheLine`:** the scripted player arms a party of two blades and two crossbows and orders a clearing of the beetles. The crossbows shoot from behind and never stand in reach. The lair is cleared, and the ledger balances.
- **Stop for the producer's look** at ranged fights.

### K9. The skill system (3–4 days)
**Complexity:** Very Complex: a new system read by combat, harvest, crafting and rescue (bundling). Split as below. The developer supervises K9.3.
- [ ] **K9.1 Skills as a table, on each crawler (Q17, Q19):** [Possibly Complex: a catalogue and saved state]
  - `Skills`, appendable like the slots, with room for 16 on each crawler;
  - starting skills drawn at spawn on their own salt;
  - saved, with the round-trip validators.
  - The golden hashes are re-recorded in their own commit, since every seed draws skills.
- [ ] **K9.2 Practice (Q18):** [Possibly Complex: behaviour] experience from swings, shots, units mined and foraged, makes and rescues.
- [ ] **K9.3 What skills do (Q20):** [Very Complex, supervised by the developer: combat, harvest, crafting and rescue each read skills; W4's practice is folded in]
  - **K9.3a combat:** proficiency in the combat rule (A1), from melee or ranged;
  - **K9.3b work:** harvest speed from mining and foraging;
  - **K9.3c making:** a make's grade and speed from smithing and alchemy, with W4's practice migrated into them;
  - **K9.3d medicine:** carrying faster, and draughts given heal more.
- [ ] **K9.4 Choices (Q21):** [Possibly Complex: cognition] skill raises what work pays an hour; recruits and the roster show skills.
- [ ] **K9.5 The client:** [Possibly Complex: Gum UI]
  - skills in the dossier and the roster;
  - a band reached told in the chronicle.
- **DoD `PracticeMakesAVeteran`:** over three game days on the gate seeds:
  - a guild member's melee rises through fights;
  - a miner gets faster;
  - an artisan's smithing raises its grade;
  - the day's checks hold.

### K10. Bosses and the gate (3–4 days)
**Complexity:** Very Complex: boss mechanics are new. Split as below. The developer supervises K10.3.
- [ ] **K10.1 The Old Stair:** [Possibly Complex: a map change, with golden and renders re-recorded]
  - the new sector at the floor's edge, uncharted at the start;
  - the sealed gate;
  - the spinners' lair on its approach.
- [ ] **K10.2 The Matriarch (Q13):** [Possibly Complex: a kind with boss traits]
  - she calls her brood to her;
  - she returns after a week.
- [ ] **K10.3 The Gatekeeper:** [Very Complex, supervised by the developer: the slam, the fury, healing between attempts, beaten once, saved]
- [ ] **K10.4 The breach:** [Possibly Complex]
  - a saved world flag;
  - the chronicle;
  - the stairs beyond, dark.
- [ ] **K10.5 The client:** [Possibly Complex: art, renders, a hard cutscene]
  - boss sprites (up to 64×64) and health bars;
  - the gate on the map;
  - the breach's cutscene.
- **DoD `TheGuildBreaksTheGatekeeper`:** on the default seed the scripted player scouts the Old Stair, arms and kits the guild, and breaks the Gatekeeper. More than one attempt is allowed; the bound is set at K10's first measurement.

### K11. The gate (2–3 days)
**Complexity:** Possibly Complex: the whole part's scenarios and bands; the close-out is Basic.
- [ ] **K11.1 Every scenario together,** [Possibly Complex] with a save mid-fight, a bleeding crawler and a crawler being carried, each loaded byte for byte.
- [ ] **K11.2 The soak's `hunting` profile:** [Possibly Complex: bands set with the producer] treasuries, kills, rescues and deaths per day.
- [ ] **K11.3 Seeds 1–120,** [Basic to run; any failure it finds is graded on its own] with and without the guild.
- [ ] **K11.4 The producer's play session.** [Possibly Complex: grade each fix it asks for]
- [ ] **K11.5 Part 3's close-out:** [Basic]
  - the roadmap: Part 3 COMPLETED, and Milestone 4 COMPLETED;
  - the epic doc, the focus file, `architecture_history.md`, the economy model and AGENTS.md;
  - this plan deleted.

## 8. Risks
| Risk | What we do |
| :--- | :--- |
| Creatures, fights and shots cost planning and frame time. | Creatures follow a short rule, not the planner (§3). Measure at K2, K7 and K8. Line of sight is a ray over the nav grid, checked only when a shot is due. |
| Fights must be deterministic. | The main thread, in id order, on ticks, with one seeded roll per swing or shot (A4). |
| Q3 makes Part 1's and Part 2's hazards lethal: a crawler downed in a hazard no longer limps out. | K0.2 measures how often it happens today. Friends' rescue (Q3b) and a carried draught (Q3a) are the answers in the game. The lone limit and the retreat line are tuned against `TheCarefulLoseFew`'s band. The golden hashes are re-recorded in K5.1's own commit. |
| Death touches every system that holds a crawler: groups, contracts, bunks, parties, memory, the ledger and the views. | The drifter's departure is the precedent (A2), and K5.4 is supervised. The round-trip validators gain the dead. |
| Deaths change the economy: a smaller population buys and sells less. | The newcomer rule (Q4). The soak's bands are set with deaths in them (K11.2). |
| The equipment refactor touches hazards, harvest, outfitting, saves and the client at once. | Split into six steps, with a proof test that adds a slot (K1.6). Behaviour is held, or re-recorded with its cause named. |
| Skills change every seed's numbers: harvest speed, crafting, hits. | They come late (K9), after the systems they tune. The golden hashes are re-recorded in K9.1's own commit, and the bands are re-checked at K11. |
| The planner's facts grow: armed, a known lair, a hunt, a pile, a fallen friend, ranged. | About 47 of 64. R1's guard. |
| The map grows: the Old Stair and two lairs. | Golden and renders re-recorded in their own commits. Well within A*'s cap. |
| Combat reads poorly on screen. | A strike pose, shots drawn, the red coat on both sides, boss health bars, and the producer's looks at K3 and K8. |
| The save format moves at most milestones. | The index's rule: raised once per PR. Old saves are refused with a reason. K1's storage for 16 slots and K9's for 16 skills avoid later bumps for new rows. |

## 9. Deferred (proposed)
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

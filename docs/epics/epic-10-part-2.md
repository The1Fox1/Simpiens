# Epic 10, Part 2: Workshops and the Guildhall

- **Status**: ACTIVE (2026-11-04). W0, groundwork, is under way on `feat/epic10-part2`. Status is owned by [ROADMAP.md](../ROADMAP.md). Decisions and deviations: `.agents/architecture_history.md`. The working plan, with every step stamped: `.agents/epic10_part2_plan.md`.
- **Track**: Track A: Core Simulation & Cognition Engine. Epic UI-4 (the workshops' UI) is folded in, as Epic UI-3 was into Part 1, because its UI is the KNI client's.
- **Milestone**: Production Milestone 3, Facility Workshops & Crafting (ROADMAP §3).
- **Source**: the draft plan `.agents/epic10_part2_plan.md` (2026-10-29), whose design (§1–§4) moved here when the producer said "Begin Epic 10 part 2 work" (2026-11-04).
- **Preceding**: [Epic 10 Part 1](epic-10.md), the guild and its expeditions. **Builds on**: Epic 8 (groups and directives), Epic 9 (the economy), the item system (I1–I8), the core refactor (R1–R6), Epic UI-Beta (the client's UI kit).
- **Following**: Epic 10 Part 3, the descent (Milestone 4).
- **Complexity** ([rubric](../../.agents/rules/complexity-grading.md)): **Very Complex** as an epic, for its size. Each milestone is stamped in §2.2; W1 and W5 are split in the plan before they start.

---

## 0. Producer intent and decisions

On 2026-10-29 the producer asked for Epic 10 to be written up "into several phased implementation plans". Part 2 had no approved draft, so its plan was both the draft and the plan. On 2026-11-04, after Epic UI-Beta's B2–B5 merged (PR #22), the producer said "Begin Epic 10 part 2 work". The producer answers by exception, so the draft's twelve questions stand as proposed:

- **Q1, name and home:** "Epic 10, Part 2: Workshops and the Guildhall", in this file, with Epic UI-4 folded in. Part 1's file stays Part 1's.
- **Q2, the stations:** two, fixed by the map:
  - **the Forge:** ore and glowstone into tools and gear;
  - **the Alchemy Still:** herbs into draughts.
  - The player builds each once, for gold: a new `Construction` sink, 300g each by default.
- **Q3, who crafts:** hired artisans. An artisan is a resident at trust ≥ 50 and warmth ≥ 30 toward the player who would not go into danger alone (risk tolerance below 30%). Artisans are paid a wage, as the guild is. One artisan per station to begin with.
- **Q4, an artisan among the groups** (Part 1's D1):
  - **A new group kind, `Workshop`:** a station's crew, led by the patron and anchored at the station. Its orders rank after a party's, with the guild's.
  - **One role at a time:** groups would allow a workshop and the guild at once (one of each kind), but an artisan isn't also a guild member.
- **Q5, what is made:** four recipes (the names are placeholders):
  - **a pickaxe:** `HarvestSpeed`. The item exists today, with no source.
  - **a healing draught:** two herbs, healing 80 at once, carried in the pack.
  - **a respirator** (foul air) and **a hard hat** (falling rock): each cuts its own hazard by 75%, where hazard gear cuts any hazard by 50%.
  - And **repair** at the forge.
- **Q6, weapons:** not in Part 2. Nothing fights before Part 3, so the forge learns weapons there.
- **Q7, where made things go:** into the stall's stock. From there:
  - the player gives them to members, free, as Part 1's D12 does;
  - or prices them for anyone. Crawlers buying them hand back the wages they hold: the crawler sink the economy model deferred.
  - The caravan doesn't buy made goods.
- **Q8, quality:**
  - **An artisan's practice:** every 10 items made raise its grade (Common, Uncommon, Rare). Each grade lengthens an item's life and strengthens its effect by a step.
  - **Repair** at the forge: half the ore of a new piece, and durability back to full.
- **Q9, the guildhall:**
  - up to 8 members;
  - barracks with 4 bunks kept for members, who sleep there for a lodging fee taken from their wages;
  - two parties out at once;
  - FormParty, so a party outlives one expedition.
- **Q10, the map:** the floor grows by two rooms, as map data: a workshop row (the two stations) and the guildhall (barracks and a muster yard).
- **Q11, combat and death:** none, as in Part 1 (its Q2 and Q3).
- **Q12, shipping and done:**
  - Live scenarios run headless with nothing injected, and the scripted player acts only through commands, as in Part 1 (its Q8 and Q10).
  - **One PR for all of Part 2**, with a commit per step. This follows the producer's rule of 2026-11-02 ("one PR for the entire epic but frequent commits"), which replaced the draft's one PR per milestone.

## 1. Goal

**The player promise:** turn the haul into gear nobody sells. Staff a forge and a still with the outpost's careful hands, and send your guild deeper, better kitted.

What Part 1 leaves undone:
- **Raw salvage has one buyer.** Everything goes to the caravan; nothing is made at the outpost.
- **The cautious majority has no part in the guild's story.** The 5–10 residents who won't go into danger alone (Part 1's M5) only trade.
- **Gear is one item:** the caravan's hazard gear, one grade, the same against every danger.
- **Wages buy nothing new.** G4 measured members' purses staying within what residents carry, so no sink was needed. Made goods give those purses something worth buying, and W0.2 measures the purses again.
- **The guild is four,** out as one party at a time.

## 2. Design

### 2.1 The loop
1. **Earn trust and build the guild** (Part 1).
2. **Build** a station, the Forge or the Alchemy Still, in the outpost's new workshop row.
3. **Hire an artisan:** a trusted resident who won't go into danger.
4. **Order** what it makes from the stall's salvage (ore, herbs, glowstone): tools, gear and draughts.
5. **Outfit** the guild from what is made, or **sell** it at the stall.
6. **Go deeper:** gear made for a hazard opens the worst of them.

**The new verbs:** build, hire and dismiss an artisan, set an artisan's wage, order a recipe, price a made item, form a party. Each is a typed player command (ROADMAP §1A.1).

### 2.2 Milestones

| Milestone | What the player sees | Core (engine-free simulation) | Client (KNI, Epic UI-4) | Complexity |
| :--- | :--- | :--- | :--- | :--- |
| **W0. Groundwork** | The two new rooms, empty | Measurements; recipes as item data; stations as map data; the crafting rule, engine-free; the `Workshop` group kind; save blocks | The rooms drawn and labelled | Possibly Complex |
| **W1. The forge and its artisan** | Build the forge, hire an artisan, order pickaxes. Bold crawlers buy them and harvest faster. | Construction; hiring (Q3, Q4); shifts; crafting from the stall's stock; the pickaxe's `HarvestSpeed` in the harvest | The station window (build, hire, order, progress); a pickaxe row in the merchant terminal | Very Complex (the plan splits it; the developer supervises shifts) |
| **W2. The still and draughts** | Draughts from herbs; members carry one and keep going | The still; the draught, carried and drunk under orders below the retreat line | The still's window; draughts in the merchant terminal and the party builder | Possibly Complex |
| **W3. Gear for each hazard** | Respirators and hard hats send the guild into the worst of the gallery | Gear with a hazard kind; recipes with glowstone; "outfit from stock" picks the target's gear; a lone crawler judges a hazard by what its gear leaves | Gear by hazard in the party builder | Possibly Complex |
| **W4. Practice and repair** | Practised artisans make better grades; worn gear is mended | Grades (`ItemInstance.Quality`); practice on the artisan's contract; repair | Grades and rarity rims in the merchant terminal and the roster | Possibly Complex |
| **W5. The guildhall** | A guild of eight, two parties out, members sleeping in barracks | The guild's limit from 4 to 8; barracks; lodging fees; FormParty; two expeditions at once | The roster at eight; two trackers | Very Complex (the plan splits it; the developer supervises two expeditions at once) |
| **W6. The gate** | The whole loop, balanced | Every scenario; the soak with crafting; save and load mid-craft | The producer's play session | Possibly Complex |

**Order:** W0 first, then W1 → W2 → W3 → W4 in order. W5 could follow W1, but one agent works them in order. The producer looks at W3 and plays W6.

### 2.3 Architecture and rules
- **Stations are map data:** a `StationDefinition` in the layout gives each station's kind, place and stock sizes. Whether a station is built, who staffs it and what it holds is authoritative state in its owner's save block.
- **Recipes are item data,** beside the item catalog: station, inputs, output, ticks. A made item is added the way `AGENTS.md`'s "Adding an item" adds any item.
- **Crafting is a main-thread rule in the tick.** An artisan's work moves a station's progress. Inputs come from the stall's stock, and the output goes back into it.
- **An artisan's work is a standing directive,** issued through Epic 8's pipeline as the guild's expedition orders are (Part 1's G5). Needs come first, as they do for members (Part 1's Q6).
- **Economy numbers live in `EconomyTuning`:** construction costs, artisans' wages and made goods' terms. Recipes hold only what an item is made of and how long it takes.
- **The planner's facts:** about 43 of 64 are used. R1's guard refuses a fact past 63, and the budget grows to about 48.
- **Saves:** the format rises when a saved record changes shape. Old saves are refused with a reason.

### 2.4 The economy
- **A new sink:** `Construction`, each station once (600g in all by default).
- **New transfers:**
  - artisans' wages;
  - made goods bought by crawlers;
  - lodging fees (W5).
- **What changes:**
  - **Exports:** salvage used at a station isn't exported, so the caravan's quota (2 ore, 2 herbs and 2 glowstone a visit) may go unfilled. Exporting or making is the player's choice.
  - **Wholesale gear:** made gear replaces `GearWholesale` purchases.
- **The bands:** W0 measures. Every milestone re-runs the soak, and W6 sets a `crafting` band with the producer.

## 3. Definition of Done

Done means the workshop loop emerges in the live outpost with nothing injected (ROADMAP §1B). The scripted player acts only through typed commands.
- **Mechanism:** validators for recipes, stations, crafting, the `Workshop` group kind, construction, hiring, grades and repair, and each save block's round trip.
- **Live, headless:** one scenario per milestone on the real outpost:
  - `AForgeTurnsOreIntoPickaxes` (W1);
  - `ADraughtKeepsAPartyWorking` (W2);
  - `TheRightGearForTheRightHazard` (W3);
  - `PracticeMakesBetterGear` and `TheForgeMendsWornGear` (W4);
  - `TwoPartiesOutAtOnce` (W5);
  - `TheWorkshopLoopPlaysThrough` and a save mid-craft and mid-expedition (W6).
- **The economy:** the ledger balances with the new sink and transfers. The soak's bands hold, and W6 sets a `crafting` band.
- **The producer:** a look at W3, and a play session at W6.
- **Docs:** the roadmap first, then this file's status, then the focus file; `architecture_history.md`; the economy model; `AGENTS.md`.

## 4. Acceptance checklist (delivered work only)

Nothing is delivered yet.

## 5. Deferred and out of scope

| Left out | Owner |
| :--- | :--- |
| Weapons, and armour against creatures | Part 3 (L2) |
| More stations; station upgrades | After Epic 10 |
| Artisans who also go out with the guild | Q4's alternative, if the producer wants it |
| The caravan buying made goods | Q7's alternative |
| Loot in play | Part 3 (L2) |
| Combat and death | Part 3 (Q11) |

## 6. Risks

| Risk | What we do about it |
| :--- | :--- |
| Making things starves the caravan's quota, and the fair band falls. | Measure at every milestone. Exporting or making is the player's choice; the band is set at W6. |
| Artisans are cautious crawlers with their own needs, so output is slow. | Shifts come after needs (Part 1's Q6). W1 measures output per minute and tunes recipe times. |
| The planner's facts grow: artisans' work, carrying a draught. | R1's guard. The budget goes from 43 to about 48 of 64. |
| The map grows again (two rooms after G5's one). | Golden hashes and renders re-recorded in their own commits. Well within A*'s cap. |
| Room on screen for stations and two trackers. | Windows, like the merchant and the bounty board (Part 1's rule). |
| The save format changes at most milestones. | Old saves are refused with a reason. |

## 7. Findings

Handed on from Part 1 ([epic-10.md](epic-10.md) §7), each re-measured at W0.2:
- **F3:** on some seeds trust rises for fewer than ⅔ of crawlers. At G6 only seed 43 of 1–120 was short. [Possibly Complex: balance research]
- **F6:** Steady turns back 3 of 12 geared trips on the gate seeds. Draughts (W2) and gear for each hazard (W3) are the levers. [Possibly Complex: tuning]
- **F7:** within 30 minutes at fair prices the guild fills on only 9 of the 23 gate seeds. The thresholds are the producer's (Q3 sets the artisans'). [Possibly Complex: a tuning choice]

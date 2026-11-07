# Epic 10, Part 2: Workshops and the Guildhall

- **Status**: ACTIVE (2026-11-04). W0, groundwork, is done (2026-11-04, `feat/epic10-part2`, §4). W1, the forge and its artisan, is split in the plan; W1.1 (building a station) and W1.2 (hiring an artisan) are done (2026-11-04, §4). The producer widened the scope the same day with milestone T, a 24-hour day (§0, §2.2): done 2026-11-05 (T0–T7), and the day is the game. On 2026-11-05 the producer gave a directive to finish Part 2 in their absence. W1, the forge and its artisan, is done the same day (§4): shifts, the pickaxe, the scripted player, the station window, and its live scenario `AForgeTurnsOreIntoPickaxes`. So is W2, the still and draughts, with `ADraughtKeepsAPartyWorking`, and W3, gear for each hazard, with `TheRightGearForTheRightHazard`; the producer's look at W3 is prepared and pending (§4). W4, practice and repair, is done too, with `PracticeMakesBetterGear` and `TheForgeMendsWornGear`. So is W5, the guildhall (2026-11-06), with `TwoPartiesOutAtOnce`. W6, the gate, is done but for the producer's play session (2026-11-06, §4), which is Part 2's last stop, with F14 (the whole loop's economy) to decide there. Status is owned by [ROADMAP.md](../ROADMAP.md). Decisions and deviations: `.agents/architecture_history.md`. The working plan, with every step stamped: `.agents/epic10_part2_plan.md`.
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

### W0's decisions (2026-11-04)

Defaults, taken while doing W0; the producer answers by exception.
- **D1, a recipe is keyed by what it makes:** an item has at most one recipe, so an order, a save and a command name the item. `RecipeBook` is static and immutable beside the item catalog.
- **D2, the pickaxe's recipe:** 3 iron ore at the forge, 600 ticks (a minute) of work. Placeholder numbers, tuned at W1.
- **D3, a station holds no stock of its own** (the draft had input and output stock sizes): a make takes its inputs from the stall's stock as it begins and puts its output into the stall's stock, holding it only while the stall has no room.
- **D4, the two rooms go where the solid rock was,** under the Road Gate and the Glow Caves, so the floor's bounds don't change: the workshop row, its door in line with the lane, and the guildhall beside it, reached only through the row.
- **D5, the owner:** a `WorkshopService` owns the stations' state, as the guild's service owns the guild's, in its own save block (save format 10, raised once for this PR).
- **D6, precedence is not the group kind's value:** kinds are saved, so `Workshop` takes the next value and a crawler's memberships are ordered by a precedence: party, then guild or workshop, then settlement.
- **D7, the rooms are facilities** (found while doing W0.4): nobody explores them and no bounty targets them. Without it, exploration sent everyone down the two new dead ends, far from food, and 8 of 16 seeds starved someone. Artisans and members will go there under orders.

### W1.3's direction: shifts (producer, 2026-11-04)

Shifts make labour scarce: an artisan can't work around the clock. It has needs and must eventually rest and eat. The player assigns a hired artisan to 4-hour blocks across the day. Stacking three blocks in a row depletes its needs: it panics, breaks out of its shift to look after them, and loses affection (warmth) and trust toward the player. The proposed design (D12–D16: blocks, the standing order, eating on the job, energy as what stacked shifts spend, overwork costing trust) is in the plan's W1.3, waiting for the producer's answers before any code.

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

### Milestone T's direction: a 24-hour day (producer, 2026-11-04)

Shifts exposed that time doesn't hold together: the dashboard's day is 9,000 ticks, but hunger fills in 67 seconds (about 1.8 game hours) and a bunk visit lasts 3 seconds (5 game minutes). The producer widened Part 2's scope to fix it first, following RimWorld's clock as a baseline (1,000 seconds a day at 1×):
- time is a full 24-hour day;
- a crawler needs 8 hours of sleep a day;
- hunger goes from fed to starving over 24 hours.

The design (D17–D22: the day's length, two kinds of time, hunger, sleep, beds, the pace of work) and its phases, T0–T7, are in the plan's milestone T. **The answers (2026-11-04):**
- **D22, the pace of work:** the producer chose to stretch work RimWorld-style, so a crawler does tens of tasks a day rather than hundreds (a unit of ore takes half a game hour to harvest).
- **D17, D19, D20, D21** are accepted as proposed:
  - D17, the day: 10,080 ticks, so a game minute is 7 ticks;
  - D19, hunger: over 24 hours;
  - D20, sleep: need-driven, about 8 hours;
  - D21, beds: one bunk per resident.
- **D18 follows D22:** only motion and what is shown stay in physical ticks. Everything a crawler does or undergoes runs on the day's time, hazards included, because a half-hour harvest at today's damage per second would down anyone.

The go came the same day ("Proceed with the time rework"). The model, with today's day measured and every duration's re-pacing, is [`docs/time-model.md`](../time-model.md). W1.3's shifts are re-answered after it: D14 (eating on the job) falls away with two meals a day, and D15 changes, since 12 hours of work fit inside a 16-hour waking day unless work tires faster than idling.

**The economy per day (T4, supervised by the developer):** D23–D28 were approved on 2026-11-04: prices stay, the caravan visits every 6 game hours, crawlers earn toward a bigger purse, trust fades over days, and the soak plays three days. Measuring changed three of them:
- **D26, the Garden:** kept as it is. On the new day crawlers buy nine meals in ten at the stall, so the Garden's regrowth made no difference.
- **D27, trust's fade:** 5 points every 12 game hours, not 6. At 6, one gate seed failed the day's trust check.
- **D23 and D25, the cost of living:** at today's prices a day's meals took about an hour of work. The developer chose to make salvage cheaper, with bigger caravan lots (D29, 2026-11-05: "go with option c"). Salvage pays a quarter of today's price on the day: ore 5g, herbs 6g, with lots of 16 and 12. Residents now earn for about 5.7 hours a day, and four reach the guild's standing by about day 2. The guild's thresholds stay.

The measurements are in [`docs/time-model.md`](../time-model.md) §2.

**The new verbs:** build, hire and dismiss an artisan, set an artisan's wage, order a recipe, price a made item, form a party. Each is a typed player command (ROADMAP §1A.1).

### 2.2 Milestones

| Milestone | What the player sees | Core (engine-free simulation) | Client (KNI, Epic UI-4) | Complexity |
| :--- | :--- | :--- | :--- | :--- |
| **W0. Groundwork** | The two new rooms, empty | Measurements; recipes as item data; stations as map data; the crafting rule, engine-free; the `Workshop` group kind; save blocks | The rooms drawn and labelled | Possibly Complex |
| **T. A 24-hour day** (added 2026-11-04, before W1.3) | A clock that means something: crawlers eat two or three times a day and sleep about 8 hours, mostly at night | The day's clock in the core (10,080 ticks, RimWorld's 1,000 s rounded to whole minutes); hunger over 24 hours; sleep until rested; beds for everyone; the day's schedules (caravan, regrowth, drifters, cooldowns, expeditions) in game hours; actions stretched to game minutes (D22); hazards, healing and wear per game hour; the economy re-paced per day | The clock from the core; a worked pose while an action runs | Very Complex (the plan splits it; the developer supervises sleep and the economy's re-pacing) |
| **W1. The forge and its artisan** | Build the forge, hire an artisan, order pickaxes. Bold crawlers buy them and harvest faster. | Construction; hiring (Q3, Q4); shifts; crafting from the stall's stock; the pickaxe's `HarvestSpeed` in the harvest | The station window (build, hire, order, progress); a pickaxe row in the merchant terminal | Very Complex (the plan splits it; the developer supervises shifts) |
| **W2. The still and draughts** | Draughts from herbs; members carry one and keep going | The still; the draught, carried and drunk under orders below the retreat line | The still's window; draughts in the merchant terminal and the party builder | Possibly Complex |
| **W3. Gear for each hazard** | Respirators and hard hats send the guild into the worst of the gallery | Gear with a hazard kind; recipes with glowstone; "outfit from stock" picks the target's gear; a lone crawler judges a hazard by what its gear leaves | Gear by hazard in the party builder | Possibly Complex |
| **W4. Practice and repair** | Practised artisans make better grades; worn gear is mended | Grades (`ItemInstance.Quality`); practice on the artisan's contract; repair | Grades and rarity rims in the merchant terminal and the roster | Possibly Complex |
| **W5. The guildhall** | A guild of eight, two parties out, members sleeping in barracks | The guild's limit from 4 to 8; barracks; lodging fees; FormParty; two expeditions at once | The roster at eight; two trackers | Very Complex (the plan splits it; the developer supervises two expeditions at once) |
| **W6. The gate** | The whole loop, balanced | Every scenario; the soak with crafting; save and load mid-craft | The producer's play session | Possibly Complex |

**Order:** W0 first, then W1.1–W1.2, then T, then the rest of W1 → W2 → W3 → W4 in order. W5 could follow W1, but one agent works them in order. The producer looks at W3 and plays W6.

### 2.3 Architecture and rules
- **Stations are map data:** a `StationDefinition` in the layout gives each station's kind, place and stock sizes. Whether a station is built, who staffs it and what it holds is authoritative state in its owner's save block.
- **Recipes are item data,** beside the item catalog: station, inputs, output, ticks. A made item is added the way `AGENTS.md`'s "Adding an item" adds any item.
- **Crafting is a main-thread rule in the tick.** An artisan's work moves a station's progress. Inputs come from the stall's stock, and the output goes back into it.
- **An artisan's work is a standing directive,** issued through Epic 8's pipeline as the guild's expedition orders are (Part 1's G5). Needs come first, as they do for members (Part 1's Q6).
- **Economy numbers live in `EconomyTuning`:** construction costs, artisans' wages and made goods' terms. Recipes hold only what an item is made of and how long it takes.
- **The planner's facts:** about 43 of 64 are used. R1's guard refuses a fact past 63, and the budget grows to about 48.
- **Saves:** the format rises when a saved record changes shape. Old saves are refused with a reason.
- **Toward placed buildings** (the producer's long-term direction, 2026-11-04, [Game Design Proposal §5](../Game%20Design%20Proposal.md)): later, the player places the shop (with its stations) and the guildhall in safe zones of their choosing and grows each as one building. Part 2's fixed rooms (D4) stand in for those buildings. So a building's state (built or not, its stations, crew and orders) stays its own, apart from the map's layout: when placement comes, a station's place moves from map data into that saved state, and nothing else should need to change. Facility sectors (D7) are what a placed building's footprint becomes; a safe zone is where one may stand.

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

### W0. Groundwork (done 2026-11-04, `feat/epic10-part2`)
- [x] **Docs:** this file, the roadmap, the focus file; the plan keeps the steps.
- [x] **Measurements** (W0.2, seeds 1–15 with the guild player, and 1–120 without): in the plan, with findings F8 and F9 below.
- [x] **Recipes as item data** (D1, D2): `Recipe`, `RecipeBook`, `StationKind`; the pickaxe's at the forge. `RecipeValidator`.
- [x] **Stations as map data and the two rooms** (D3, D4, D7): `StationDefinition`; the workshop row and the guildhall on Floor 1; facilities that nobody explores and no bounty targets. The map, exploration and charting validators check them.
- [x] **The crafting rule and its owner** (D5): `Crafting`, `WorkshopService` (`SimulationManager.Workshops`), `WorkshopSave`, save format 10. `CraftingValidator`; the restore validator round-trips a make in progress and a held make; `ItemSystemProofTests` makes a test-only item, added as data, from the stall's stock.
- [x] **The `Workshop` group kind** (D6): ranked with the guild; the guild refuses an artisan. The directive, guild and restore validators.
- [x] **Re-recorded once:** the golden hashes (only the map part moved: two more charted sectors), 18 renders and 5 picture pins (looked at first).
- [x] **The gate:** core 146/146 and client 438/438; golden 3/3; the 23 gate seeds and the soak hold on the grown floor with nothing built; seeds 1–120 119/120 (seed 43's trust, F3, as before); the pinned image 438/438 exact; the MonoGame and Android compiles with 0 warnings. W0 has nothing a player does yet, so it has no live scenario of its own.

### W1. The forge and its artisan (done 2026-11-05, `feat/epic10-part2`)
- [x] **W1.1, building a station** (D10): `BuildStationCommand`; `WorkshopRules.CheckBuild` with its words; 300g from the stall into the `Construction` sink; the chronicle. `WorkshopValidator`.
- [x] **W1.2, hiring an artisan** (D8, D9, D11): `HireArtisanCommand`; Q3's resident; the station's crew, a patron-led `Workshop` group; contracts, wages after the guild's (`StallPayment.ArtisanWage`), quitting, dismissal and cooldowns, saved; Dismiss and SetWage by role. `WorkshopValidator`, the restore validator. Core 147/147, client 438/438, golden 3/3: nobody builds or hires in a live run until the scripted player (W1.5a).
- [x] **W1.3, shifts** (D12, D13, D15, D16, D32–D35): `OrderStationCommand` and `SetShiftsCommand`; six blocks of four hours (`ShiftBlocks`); the crew's `WorkStation` directive on shift, followed through Epic 8's compliance (`StationOrder`, `AtDirectiveWork`, `WorkStationAction`); work tires at 6 energy an hour; an artisan breaks at the break line or after 10 hours on shift in a row (trust −10, warmth −5, off until rested). For everyone: fixtures remembered three game days (D33) and eating before bed (D34). Measured: the day shift breaks 0–1 times in three days, three blocks in a row 1–3 times. `WorkshopValidator`, `WorkshopShiftTests`; golden unchanged (it samples to t3000, before anyone sleeps).
- [x] **W1.4, the pickaxe** (D36–D40): mining a mineral 1.5 times as fast (`CrawlerStats.MiningSpeed`, the first reader of `HarvestSpeed`), salvage chosen by what it pays an hour so an owner goes for ore; 40 units of wear, then it breaks (told in the chronicle); residents who mine buy one at 30g when their purse spares it with a ration's price (`EquipForWorkGoal`, `BuyToolAction`; `HasTool` re-used, 40 of 64 planner facts); the stall's pickaxes are the forge's alone. `ToolValidator`, `PickaxeBuyingTests`. Measured on 6 seeds over three days: 2–5 made, 1–3 bought from day 2, by the least greedy.
- [x] **W1.5, the scripted player and the live scenario**: `WorkshopPlayer` (commands only: builds the forge when the stall can pay, hires the first resident who qualifies, the day shift, pickaxes two at a time). **`AForgeTurnsOreIntoPickaxes`**, nothing injected: built on day 1, the artisan hired, two pickaxes made from the stall's ore, one bought on day 3 and mined with; the construction sink named, the ledger balanced every tick, the days' checks held.
- [x] **W1.6, the client:** the stations on the map in both view modes (the forge, the still, a staked plot before either stands; picked by a view-derived id, `StationPick`); the station window (a tab per station; Build with its cost or the rule's reason not; the artisan with energy, wage, the six blocks and a warning when they tire it, or up to three willing residents with Hire; the order, its progress and why it waits), opened from the header's new Workshop button or a click on a station; the pickaxe's merchant row with each good's stock in its row; the dossier's worn tool. One window at a time through `IDashboardWindow`; below 1440 wide the header's identity drops its words so five window buttons fit (a test keeps the clusters clear at every offered size). Art pins and renders re-recorded after looks, in the pinned image (466/466).

### W2. The still and draughts (done 2026-11-05, `feat/epic10-part2`)
- [x] **W2.1, the draught** (D41, D42, D46): `HealingDraught`, added as content (catalog, the still's recipe of two herbs and an hour, a stall row sold at 20g, a flask in the sprite kit, words); the still needs no code of its own. The herb reflex drinks it under the treat line, herbs first; draughts drunk are counted apart. `DraughtTests`.
- [x] **W2.2, draughts under orders** (D43, D44): the dispatch order names draughts each (0–2), the muster gives them from stock with the rations, and the retreat rule doesn't count a member still carrying medicine: it drinks instead. Saved in the expedition's spare byte. `ExpeditionTests`, the restore validator. Measured live with draughts stocked by hand: bare parties turn back on 15 of 15 trips without, 0 of 13 with two each.
- [x] **W2.3, the bold buy their own** (D45): `CarryMedicineGoal`, `BuyDraughtAction`, the planner's `HasMedicine` (41 of 64 facts); the trade handler judges a draught by its own value. `DraughtTests`.
- [x] **W2.4, the client:** the party builder's draughts each, with the stock on the dispatch line; the still's recipe and the draught's merchant row came from W1's pieces. Renders re-recorded in the pinned image (466/466).
- [x] **W2.5, the live scenario `ADraughtKeepsAPartyWorking`**, nothing injected: the scripted players build and staff the still, and the guild waits for eight draughts, then sends four bare members to the Drowned Gallery under Steady, twice on the default seed, the same until the dispatch. Without draughts the party works 281 ticks and turns back; with two each it works 302, drinks 4 and brings the whole seam home; nobody downed; the ledger and the days' checks hold. *Deviation:* the haul is asserted as at least as big, not bigger, since the Gallery's seam yields 4 and a party turning back has usually taken it.

### W3. Gear for each hazard (done 2026-11-05, `feat/epic10-part2`; the producer's look pending)
- [x] **W3.1, protection by kind** (D47–D49): the respirator (the air hazards, foul air and the Gallery's flooding, cut by three quarters) and the hard hat (falling rock), made at the forge from a glowstone and herbs or ore, sold at 60g; hazard gear still cuts any hazard by half. Kinds are item data: `AirResistance`, `RockResistance`, `CrawlerStats.HazardResistance(inventory, kind)` read by the damage. The stall holds ten goods (inside format 10); the merchant terminal's nine rows fit at 720.
- [x] **W3.2, outfitting by the target's hazard** (D50): the best protection in stock for the target (`GuildRules.BestProtectionInStock`), given for a hazard so it may replace worse gear, the sound piece going back to stock; the party builder and the guild player use it.
- [x] **W3.3, lone crawlers judge a hazard by gear made for it** (D51, the producer's review: "D51 is fine"): hazard gear softens the harm, not the judgment, so the Gallery stays the guild's unless the player sells respirators; the bold buy the piece that opens what they can't reach.
- [x] **W3.4, the client** (D52): the respirator worn as a mask (`Worn.Face`) on today's figures, the bodies and the portraits; the hard hat a steel helm; the outfit button names its gear.
- [x] **W3.5a, the bold upgrade and know the charted** (D53, D54): found by W3.5's probe; a bold crawler trades its gear in for a piece that opens salvage it can't reach, and knows a charted hazard sector's salvage from the map's plan.
- [x] **W3.5, the live scenario `TheRightGearForTheRightHazard`**, nothing injected: with the guild, the forge's respirators replace hazard gear trip by trip; members in respirators take half the harm (0.0375 a tick against 0.075) and bring home as much; without the guild, a bold resident trades its gear for a respirator and works the Gallery alone on day 4.
- [ ] **The producer's look** (pending): the art review sheet (`render-output/art-sheet.png`: the mask and the steel helm, today's figures wearing them), the people sheet (`render-output/people-sheet.png`: "Frames by standing, the helm and the tabard" and the bodies' row), and the references `render-outpost-merchant-t300.png` (nine goods), `render-iso-station-window-at-work.png` (the forge's three recipes) and `render-outpost-guild.png` (the outfit button), in `dotnet/Simpiens.Client.Tests/Baselines/`.

### W4. Practice and repair (done 2026-11-05, `feat/epic10-part2`)
- [x] **W4.1, practice and grades** (D55–D57): an artisan's makes count on its contract, its grade Standard, Fine from 10, Masterwork from 20; each grade above Standard lengthens a worn piece's life a quarter and strengthens its effect a tenth (item data on the equipment); the stall's shelf keeps each good's Fine and Masterwork pieces, the forge stocks a make at its artisan's grade and a sale or a gift hands out the best first. `ItemInstance.Quality` is in use. The stall is 136 bytes, inside format 10.
- [x] **W4.2, worn pieces and repair** (D58, D59): pieces handed back wait on the shelf's worn count when the forge can mend them (hazard gear, the caravan's, as before); a repair order takes a worn piece, half of each input rounded down (no glowstone for a respirator) and half the work, and stocks the piece full at Standard, with no practice.
- [x] **W4.3, the client:** rims a step rarer a grade (six graded icons) in the merchant terminal, the roster and the dossier; the stock line's best grade and worn pieces; the station window's artisan grade and practice, and Mend.
- [x] **W4.4, the live scenarios**, nothing injected: `PracticeMakesBetterGear` (the first pickaxe taken up Standard with 40 units, the smith Fine on day 6, a Masterwork pickaxe taken up on day 9 with 60) and `TheForgeMendsWornGear` (the guild's worn respirator on the shelf on day 5, mended without glowstone). *Deviation:* draughts have no grade (used once, no life to lengthen).

### W5. The guildhall (done 2026-11-06, `feat/epic10-part2`)
- [x] **W5.0, expeditions per party** (D60): the guild holds its parties in slots (`GuildParty`), and G5's stage machine, provisioning, haul and shares run per party; the save holds the parties (`GuildSave.Parties`, inside format 10). A refactor first, with no change in behaviour: the eight guild scenarios printed the same lines.
- [x] **W5.1, eight members** (D61): the guild's limit is 8, a party still four. The roster window is 960 wide in two tabs, Members (eight cards of two lines fit 1280×720) and Expeditions. The soak's guild profile and the console's `--guild` sweep keep Part 1's guild of four until W6.
- [x] **W5.2, the barracks** (D62–D64): eight bunks in the guildhall (`PoiKind.GuildBunk`), one kept for each member from signing until it leaves; a member sleeps in its own wherever it is, and nobody else remembers them as bunks. Each caravan visit a member with a bunk is paid its wage less the 1g lodging fee. The muster stays at the stall. On 8 gate seeds members slept only in their own barracks bunks. *Deviation:* 8 bunks where Q9 said 4 (D21: a sleeper holds its bunk about 8 hours of the day).
- [x] **W5.3a, FormParty** (D65): the player sets a party's members (`FormPartyCommand`); a party's group is made when it is formed and disbanded when emptied, and outlives its trips; a member who stayed behind rejoins it after. Dispatch and Recall name the party. The roster's card button puts a member in a party; the Expeditions tab chooses which party to send.
- [x] **W5.3b, two at once** (D66): each party has its own expedition, never two in the same sector (`SectorTaken`); a tracker per party out on the map. The scripted guild player sends party 2 to its own target, and with `together` both on the same tick. *Supervision:* the plan had the developer supervise this step; the AFK directive delegated it, and the architecture step (W5.0) brought it to Possibly Complex.
- [x] **W5.4, the live scenario** `TwoPartiesOutAtOnce`, nothing injected: eight members on day 2.9; both parties out at once on day 3.3, to the Drowned Gallery and the Glow Caves, each working its own sector; hauls of 4 and 3 home; every wage paid; all eight slept in their own barracks bunks.
- **Found on the way:** each party alone, the caravan's hazard gear outfits one party a visit and they take turns; sent together, both are out at once on every seed that filled the guild. F14 (a guild of eight with both workshops runs the treasury dry) and F15 (a respirator bought for the Gallery may not find its seam) are in §7.

### W6. The gate (2026-11-06, `feat/epic10-part2`; the producer's play session pending)
- [x] **W6.0, the day's lone-venture check judges by D51** (what a crawler's gear leaves of the hazard); the scripted players take an optional cash reserve.
- [x] **W6.1, `TheWorkshopLoopPlaysThrough`**, nothing injected: both stations built and staffed, respirators and draughts made, the guild of eight in two parties sent together, the Gallery's party in the forge's respirators, both out at once and home with hauls, residents buying the stations' goods, no wage unpaid. *Deviation (D67):* on gate seed 1, not the default seed. On the default seed the stall affords the forge and a guild of eight but never the still as well (F14).
- [x] **W6.2, a save mid-craft and mid-expedition**: with both parties out and the still mid-make it loads byte for byte with the same world hash, and plays on.
- [x] **W6.3, the soak's `crafting` profile**, its band proposed by exception (D68; economy model §7): the loop's integrity, since its treasuries end day 3 at 1–390g. The other four bands hold on their seeds.
- [x] **W6.4, the seeds:** 1–120 pass the outpost's day without and with the guild player; the 23 gate seeds hold two days with the workshop players and the crafting guild.
- [ ] **W6.5, the producer's play session** (pending). What to play and look at:
  - **The guild window:** its two tabs; eight cards, each with its bunk and party; the party buttons; the Expeditions tab's party selector; two parties out with two trackers on the map (`render-outpost-guild-eight.png`, `render-outpost-two-trackers.png` in the client tests' `Baselines/`).
  - **The guildhall's barracks** on the map, and members walking there to sleep.
  - **The workshops:** building, hiring, shifts, orders, Mend; made goods on the shelf by grade.
  - **W3's look**, still pending (§4, W3).
  - **To decide, F14:** the whole loop leaves the stall near broke (treasuries of 1–390g at day 3 where the fair player ends at 771–1,277g). The levers, measured in the plan's W6 design: the stations' 300g each, upkeep (320g a day), members' wages (5g a visit, about 130g a day for eight after lodging), glowstone's export price, or the made goods' prices. The `crafting` band follows the choice.
- [ ] **W6.6, the close-out** (after the play session): the roadmap's COMPLETED, Milestone 3, Epic UI-4; the plan deleted.

### The stretch (2026-11-06, `feat/epic10-part2`, the AFK directive's stretch goal)
- [x] **S1, the hazards move** (D69): in the view only, from the scene's time. Sickly puffs rise and thin in the Sump, ripples spread in the Drowned Gallery, and pebbles fall with their dust in the Glow Caves, at spots laid out clear of walls and nodes, hidden by the fog. Art generated on the CPU (`HazardSprites`); no simulation change.
- [x] **S2, the caravan walks in and out** (D70): a porter hauling a covered handcart up the lane from the Road Gate to the stall for each supply visit, trading a game hour and walking back out. Its place is a pure function of the clock (`Caravan.At`), so the visit's timing, the save and the golden hashes are untouched; the route is map data (`MapDefinition.CaravanRoute`). *Left for later:* a caravan that is a pawn (delayed by crowds, met by crawlers, traded with) belongs with interactive trade, after Part 2.
- [x] **S3, a hurt crawler wears a red coat** (D71; added 2026-11-07, the producer's last stretch): while a crawler takes damage, today in a hazard sector even through its gear, a flat red silhouette of its own sprite blinks over it, twice a second, never at full red, hidden by the fog with it. The simulation only records what each crawler lost in its last tick (`AgentState.HurtThisTick`, not saved, read by no decision), so behaviour, saves and the golden hashes are untouched. The renderer draws the silhouette through the stock `BasicEffect` with its fog at 100%, with no shader of our own, in KNI and MonoGame alike.
- **For the play session:** watch the Sump, the Gallery and the Glow Caves (crawlers there blink red), and the lane a little before each six-hourly visit (`render-iso-caravan-t2400.png`).

## 5. Deferred and out of scope

| Left out | Owner |
| :--- | :--- |
| Weapons, and armour against creatures | Part 3 (L2) |
| More stations; station upgrades | After Epic 10 |
| Artisans who also go out with the guild | Q4's alternative, if the producer wants it |
| The caravan buying made goods | Q7's alternative |
| Loot in play | Part 3 (L2) |
| Combat and death | Part 3 (Q11) |
| Safe zones; placing the shop and the guildhall where the player chooses, each grown as one building; rival shops and guilds | Unscheduled: the producer's long-term direction ([Game Design Proposal §5](../Game%20Design%20Proposal.md)) |
| Quitting on soured feelings (the producer, 2026-11-04). Today a guild member (Part 1's D4) and an artisan (W1.2) quit at a caravan visit when trust toward the player is under 30, or after 2 unpaid visits in a row. Later: quitting on warmth (affection) as well as trust, thresholds per role as tuning, quitting at any time rather than only at a visit, and perhaps going to a rival (Game Design Proposal §5) | Unscheduled; any epic that reworks contracts |

## 6. Risks

| Risk | What we do about it |
| :--- | :--- |
| Making things starves the caravan's quota, and the fair band falls. | Measure at every milestone. Exporting or making is the player's choice; the band is set at W6. |
| Artisans are cautious crawlers with their own needs, so output is slow. | Shifts come after needs (Part 1's Q6). W1 measures output per minute and tunes recipe times. |
| The planner's facts grow: artisans' work, carrying a draught. | R1's guard. The budget goes from 43 to about 48 of 64. |
| The map grows again (two rooms after G5's one). | Golden hashes and renders re-recorded in their own commits. Well within A*'s cap. |
| The 24-hour day (T) changes every seed's day: food demand falls about tenfold, sleep holds a bunk for hours, and the expedition's muster and rations were tuned to hunger at 1.5 a second (a trip of about 30 s costs about 45 hunger today, about 3 after T). | T0 measures first and tables every duration; the economy's re-pacing and sleep are supervised by the developer; the soak's bands are re-set with the producer; the expedition's muster rules are re-timed in T3. Stretched work (D22) puts hazards on the day's time too: damage, healing and gear's wear are re-tuned so a unit of glowstone costs what it does today, and the guild's scenarios re-measure who ventures (T3.2). |
| Room on screen for stations and two trackers. | Windows, like the merchant and the bounty board (Part 1's rule). |
| The save format changes at most milestones. | Old saves are refused with a reason. |

## 7. Findings

Handed on from Part 1 ([epic-10.md](epic-10.md) §7), each re-measured at W0.2 (2026-11-04, seeds 1–15 with the guild player for 30 minutes, and seeds 1–120 without it):
- **F3:** on some seeds trust rises for fewer than ⅔ of crawlers. **At W0.2:** unchanged from G6, only seed 43 of 1–120 is short (7 of 12). [Possibly Complex: balance research] **On the 24-hour day (T5):** seeds 1–120 all pass, seed 43 included.
- **F6:** Steady turns back 3 of 12 geared trips on the gate seeds. **At W0.2:** 2 of 8 trips that ended turned back on seeds 1–15. Draughts (W2) and gear for each hazard (W3) are the levers. [Possibly Complex: tuning] **On the 24-hour day (W2.2, 8 seeds, three days):** geared parties turned back on 0 of 13 trips, filling the seam's 4 in 300–460 ticks; bare parties turn back unless they carry draughts.
- **F7:** within 30 minutes at fair prices the guild fills on only 9 of the 23 gate seeds. **At W0.2:** 7 of 15. The thresholds are the producer's (Q3 sets the artisans'). [Possibly Complex: a tuning choice] **On the 24-hour day (T4.2b, T4.4):** trust follows the day's trades, and the guild soak's guild fills on every one of 16 seeds, between days 1.6 and 2.9.

Found in Part 2:
- **F8 (2026-11-04): ore and herbs sit at the stall's cap.** Before nearly every caravan visit the stall holds 20 ore and 20 herbs (`StallSalvageCapacity`), and the caravan takes 2 of each, so crawlers' later salvage is turned away. A station drawing on them costs no export and opens room for more sales. Glowstone is the scarce input (under 1 in stock on average; 6–25 exported in 30 minutes, at 70g each), so W3's glowstone recipes compete with the caravan. [Possibly Complex: recipe inputs and W3's economy] **On the day (T4.2b):** the caravan takes 16 ore and 12 herbs a visit, and the stall is full of ore before 53% of visits (herbs 15%), so a forge drawing on ore still frees room for sales.
- **F10 (2026-11-04): idle crawlers step over the workshop row's threshold.** Nobody explores the row (D7 holds), but an idle wander's short step near the Road Gate lands just inside its door (y ≈ −18.1), for 6–20 crawler-seconds a day on 8 of 12 seeds probed. That shifts the late day slightly on 33 of seeds 1–120 (meals, cycles and anomaly counts); the first scout, gather and sale are the same on every seed, and the day's checks hold as before. No action: W1's artisans will cross it on purpose.
- **F9 (2026-11-04): purses are small.** At minute 30 members carry 10–48g on average and other residents 4–19g (the most anyone carries is about 70g). A pickaxe at its 30g base value is beyond most residents most of the time, so W1.4's price and buy decision must fit the purses that exist, or no crawler buys and Q7's sink never works. [Possibly Complex: W1.4's price] **On the day (T4.2b):** residents carry about 55g at the end of each day, below their 80g target, and a unit of salvage pays 5–6g, so a 30g pickaxe is about six units of work. W1.4 prices it against that. **Answered at W1.4 (D39):** the price stays 30g; residents who mine buy one from day 2 when their purse spares it, the least greedy first, and the player can price it lower in the merchant terminal to reach the greedy.
- **F11 (2026-11-05): on the 24-hour day drifters work.** They sell salvage, so they leave with more gold than they brought (about 460g against 330g over the day's three-day soak at keen prices): Part 1's check that drifters leave gold behind (G1) turns over on the day. They still make the stall richer: the keen player's treasury ends higher with them than closed on all 16 seeds measured (17,254g against 11,985g), which the day's soak now asserts. At the cut-over (T5), G1's purse check gives way to it. [Basic: a test's assertion]
- **F13 (2026-11-05): a just-qualified artisan works about half its shift.** On 6 seeds the scripted player's smith was away from the forge 10–61% of its shift. Hired at trust 50 and warmth 30, its compliance (Epic 8) falls under 20 once it is hungry, and it stops working until it has eaten; a smith hired at trust 60 worked 17–22 of 22–24 hours. Paid wages raise trust over days. [Basic: watch at W6]
- **F12 (2026-11-05): herbs aren't needed on the 24-hour day.** A venture costs about 40 health and a night in a bunk heals 80, so a crawler rarely falls under the herb line (60): 3 herbs eaten in 23 seed-days. G2's scenario no longer asserts herbs; the herb rule stays as a reflex for the badly hurt. W2's draughts are where healing matters next. [Basic: watch at W2]
- **F14 (2026-11-05): a guild of eight with both workshops runs the treasury dry.** A probe with the guild's limit at 8, the scripted guild recruiting to 8, and the forge's and the still's players (4 game days, 12 gate seeds) reached eight members on only 3 seeds. The treasury hit 0 on every seed (the two stations' 600g, the artisans' wages, eight members' wages), and members quit unpaid on 4 seeds. Without the workshops, eight sign on by day 3–5 on 8 of 12 seeds and no wage goes unpaid. This is W6's loop, whose band is set with the producer. [Possibly Complex: measure at W6]
- **F15 (2026-11-06): a crawler who buys a respirator for the Gallery may not find its seam.** D54 lets the charted plan justify the purchase, but salvage choice needs a remembered node, and which sector a crawler searches next is luck. On the default seed after W5.2, the one bold resident bought a respirator and spent the day in the Herb Grove and the Deep Seam. W3's live scenario still sees a resident work the Gallery alone in one; the mechanism test now shows it the seam by hand. A fix would apply D54 to targeting as well (the plan's nodes in a sector the worn gear opens count as known), which needs cognition to find a plan node's entity. [Possibly Complex: a targeting rule, unscheduled]

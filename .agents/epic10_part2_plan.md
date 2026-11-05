# Epic 10 Part 2: workshops and the guildhall (working plan)

Working document (AGENTS.md, Documentation Protocol 6), one of Epic 10's plans ([index](epic10_plan.md)). Written 2026-10-29 as Part 2's draft. On 2026-11-04 the producer said "Begin Epic 10 part 2 work", Q1–Q12 stood as proposed, and the design moved into [the epic](../docs/epics/epic-10-part-2.md): the goal, the loop, the answered questions, the milestones, the economy, the risks and the deferrals. This plan keeps the steps.

- **Branch:** `feat/epic10-part2`, cut from `main` at `e74db29`. One PR for all of Part 2 (Q12), with a commit per step. Pushed and opened only on the producer's word.
- **Estimate:** 14–19 agent days: W0 2, W1 3–4, W2 2, W3 2–3, W4 2, W5 2–3, W6 1–2. The roughest of the plans: nothing is measured yet, and W0 measures first.
- **Complexity** ([rubric](rules/complexity-grading.md)): **Very Complex** as a whole, for its size. W0, W2, W3, W4 and W6 are Possibly Complex. W1 and W5 are Very Complex: split each before it starts (see their stamps).
- **The producer's stops:** a look at W3, the first chain from the forge to an expedition; a play session at W6. (Answering the draft was the first; it was the go on 2026-11-04.)

## 0. What Part 1 hands over (checked at W0)
- **Groups:** many-to-many, one per kind.
  - The guild, four at most, is a `FactionGuild` group with the player as patron.
  - Parties are `TemporaryParty` groups inside it, and the expedition is a directive.
- **The economy:** wages and fees as transfers; bounty fairness; the guild's soak band.
- **Danger and its rewards:** glowstone hauls from the Drowned Gallery; hazard gear from the caravan.
- **The item system:**
  - equipment slots: protection, weapon, tool;
  - stats: `HazardResistance`, `HarvestSpeed`, `Attack`, `Defense`;
  - rarity, and `ItemInstance.Quality`, which is unused so far;
  - loot tables, not yet in play.
- **The planner:** 40 of 64 facts used (W0.2; the draft said about 43). R1 guards the limit.

## 1. The milestones, phased
Each step is a commit. After each: a build with 0 warnings and `dotnet test`. Every milestone passes the index's gates; Part 2 ends in one PR into `main` (Q12).

### W0. Groundwork (2 days)
**Complexity:** Possibly Complex: new data shapes that later milestones build on, a grown map (golden hashes and renders re-recorded), a save block and a group kind.
- [x] **W0.1 Docs:** [Basic]
  - the answered draft into `docs/epics/epic-10-part-2.md` (Q1);
  - the roadmap: Epic 10 Part 2 ACTIVE, Epic UI-4 folded in;
  - the focus file.
- [x] **W0.2 Measure** [Possibly Complex] on seeds 1–15, with G6's guild player:
  - the treasury and the members' purses;
  - the stall's salvage stock at each caravan visit, which is the stations' input;
  - planning cost, and the planner's facts in use.
  - **Done 2026-11-04** (a scratch probe, 30 sim-minutes a seed, as the soak's guild profile plays; `main` at `e74db29`):

    | Measure | Seeds 1–15 |
    | :--- | :--- |
    | Treasury at minute 30 | 1,146–1,991g |
    | Members' purses at minute 30 | 10–48g on average, at most 71g |
    | Other residents' purses | 4–19g on average, at most 70g |
    | Ore and herbs in the stall before a visit | 18.3–20.0 on average: at the cap (`StallSalvageCapacity`, 20) nearly always; the caravan takes 2 of each a visit (60 in 30 minutes) |
    | Glowstone in the stall before a visit | 0.2–1.1 on average, at most 4; 6–25 exported in 30 minutes |
    | Cautious residents (risk tolerance under 30%) | 6–11 of 12 |
    | First artisan candidate (Q3: cautious, trust ≥ 50, warmth ≥ 30, not a member) | t899–t5639 (1.5–9.4 minutes) on every seed; 1–3 at minute 30 |
    | Guild full within 30 minutes (F7) | 7 of 15 |
    | Expeditions | 6 home with the haul, 2 turned back, 1 still out at minute 30 |
    | Throughput (Release, one run a seed, this machine) | 20,000–45,000 ticks/s, the first seed's 8,000 aside (start-up) |
    | Planner facts | 40 of 64 (`StateFact` 0–39), not the draft's 43 |
    | Seeds 1–120, no guild (F3) | 119/120: seed 43's trust (7 of 12), as at G6 |

  - Findings F8 (salvage at the cap) and F9 (small purses) are in the epic's §7.
- [x] **W0.3 Recipes as item data:** [Possibly Complex: a new data shape that W1–W4 rest on] a `Recipe` (station, inputs, output, ticks) beside the item catalog.
  - **D1, a recipe is keyed by what it makes:** an item has at most one recipe, so an order, a save and a command name the item. `RecipeBook` is static and immutable beside `ItemCatalog`, with the same test hook. `StationKind` (Forge, Still) lives with it.
  - **D2, the pickaxe's recipe:** 3 iron ore at the forge, 600 ticks (a minute) of an artisan's work. Placeholder numbers: W1 measures output and tunes them. Ore is plentiful (F8).
  - `RecipeValidator` (every item defined, inputs and counts, one station, work time), `ItemNamesTests`' list, and `AGENTS.md`'s "Adding an item".
  - *Deviation:* `ItemSystemProofTests`' made item moves to W0.5, where something can make it.
- [x] **W0.4 Stations as map data:** [Possibly Complex: the map grows; the gate seeds may move]
  - `StationDefinition`: its key, kind, place and radius;
  - the two rooms (Q10) in `Floor1Layout`, with nothing built;
  - the map validators.
  - **D3, a station holds no stock of its own** (the draft had input and output stock sizes): when work on an item begins, its inputs are taken from the stall's stock, and the finished item goes into the stall's stock (Q7). A station holds a finished item only while the stall has no room for it.
  - **D4, the rooms go into the solid rock** under the Road Gate and the Glow Caves, so the floor's bounds don't change (it still fits a 1280×720 window top-down):
    - the workshop row under the Road Gate (x −8..8, y −24..−18), with a door from the Road Gate;
    - the guildhall beside it under the Glow Caves (x 8..24), reached only through the workshop row, never through a hazard.
    - Both are safe and start charted. The stations stand unbuilt; the guildhall stays empty until W5 brings its barracks and yard.
  - **D7, the rooms are facilities** (found while doing it): with the rooms open, exploration (which heads for the sector a crawler stood in least recently) sent everyone down the two new dead ends, far from food, and 8 of 16 seeds starved someone. A sector can now be a facility (map data, `SectorDefinition.Facility`): nobody explores it, and no bounty targets it (`BountyRules.TargetCheck.Facility`; the bounty board leaves it out). Artisans (W1) and members (W5) will go there under orders. With it, the golden seeds' crawlers, economy, bounties and groups hash the same to t3000; only the map part (two more charted sectors) moved.
  - Re-record the golden hashes and the renders for the map, each in a commit of its own.
- [x] **W0.5 The crafting rule, engine-free:** [Possibly Complex: a save block]
  - a station's inputs, its progress per tick of an artisan's work, its output;
  - its save block;
  - `CraftingValidator`.
  - **D5, the owner:** a `WorkshopService` owns the stations' state, as `GuildService` owns the guild's, and saves it in a new block (`WorkshopSave`; save format 10). A station's state is plain data: built or not, its order (an item and how many), the current item's progress in ticks of work, whether its inputs are taken, and a finished item waiting for room. Inputs are taken from the stall when an item's work begins and are spent from then on.
  - `ItemSystemProofTests`: a made item added as data (a test item, its recipe) is made by the rule from the stall's stock, with no other code knowing it.
- [ ] **W0.6 The `Workshop` group kind (Q4):** [Possibly Complex: the membership order and the directive precedence (G4's D1)] the per-kind rule, the one-role rule, its place in the directive precedence; validators.
  - **D6, precedence is not the kind's value:** group kinds are saved, so `Workshop` takes the next value (3), and a crawler's memberships are ordered by a precedence instead: party, then guild or workshop, then settlement. One role at a time: the guild's rule refuses a workshop member now; W1.2's hiring rule refuses a guild member.
- **DoD:** the 23 gate seeds and the soak hold on the grown floor with nothing built. Validators cover recipes, stations and crafting.

### W1. The forge and its artisan (3–4 days)
**Complexity:** Very Complex: it bundles construction, hiring, shifts, a new buy decision and a station window. Split it before starting: the forge (W1.1–W1.3, with its share of W1.5 and W1.6), then the pickaxe (W1.4) as a stretch of its own. W1.3 stays Very Complex on its own, so the developer supervises it.
- [ ] **W1.1 Construction:** [Possibly Complex: a built station is saved state] `BuildStationCommand` and the `Construction` sink, counted in the ledger.
- [ ] **W1.2 Hiring (Q3, Q4):** [Possibly Complex]
  - **The commands:** Recruit gains a role (guild or a station); Dismiss and Set Wage work for artisans too.
  - **The artisan's contract:** a wage like a member's (G4's D3, D4), and its practice.
- [ ] **W1.3 Shifts.** [Very Complex, supervised by the developer: standing work through Epic 8's directives, after needs, which has to emerge live] The workshop's standing directive is "work the station", through Epic 8's pipeline as the guild's orders go (G5).
  - An artisan eats, sleeps and socializes first, as a member does (Part 1's Q6).
  - Crafting draws the station's inputs from the stall's stock and puts the output in it.
- [ ] **W1.4 The pickaxe:** [Possibly Complex: `HarvestSpeed` in the handler is Basic; the buy decision is a new goal]
  - `HarvestSpeed` in `HarvestResourceIntentHandler`;
  - a buy decision for crawlers who harvest, as `OutfitForDangerGoal` is for gear;
  - its stall row and its price.
- [ ] **W1.5 The scripted player's workshop moves** [Possibly Complex] (build, hire, order), and the live scenario.
- [ ] **W1.6 The client:** [Possibly Complex] the station window and the merchant terminal's row; tests and renders.
- **DoD `AForgeTurnsOreIntoPickaxes`:** on the default seed, within 30 minutes:
  - the scripted player builds the forge, hires an artisan when one qualifies and orders pickaxes;
  - at least two are made from the stall's ore;
  - a crawler buys one, and its harvests take less time;
  - the construction sink shows in the ledger, which balances, and the day's checks hold.

### W2. The still and draughts (2 days)
**Complexity:** Possibly Complex: the forge is the precedent for the still; the draught changes how an expedition retreats.
- [ ] **W2.1 The still,** [Basic, once W1 is done: it copies the forge] built and staffed as the forge is.
- [ ] **W2.2 The draught:** [Possibly Complex: the expedition's retreat rule] the recipe; carried in the pack.
  - **Under orders,** a member below the retreat line drinks one instead of turning back, once per stage of work.
  - **Elsewhere,** a hurt crawler may buy one, as it eats a herb today.
- [ ] **W2.3 The client:** [Possibly Complex] the still's window; draughts in the merchant terminal and the party builder.
- **DoD `ADraughtKeepsAPartyWorking`:** two expeditions to the Drowned Gallery on the same seed, one carrying draughts. The one with draughts works longer and hauls more, with nobody downed.

### W3. Gear for each hazard (2–3 days), then the producer's look
**Complexity:** Possibly Complex: hazard kinds on equipment, and lone crawlers' venture rule, with the soak's cost measured.
- [ ] **W3.1 Hazard kinds on gear:** [Possibly Complex] item data, the reduction per kind, and the respirator and hard hat recipes, which use glowstone.
- [ ] **W3.2 Outfitting:** [Possibly Complex] "outfit from stock" picks gear for the target's hazard.
- [ ] **W3.3 Lone crawlers judge a hazard by what their gear leaves** [Possibly Complex: it changes who ventures; measure the soak] (Part 1's deferral). Through a respirator, the gallery's 6 a second is 1.5, under G5's limit of 4.
  - So selling respirators opens the gallery to the bold, and giving them only to members keeps it the guild's: a choice for the player.
  - The soak shows its cost.
- [ ] **W3.4 The client:** [Possibly Complex] gear by hazard in the party builder.
- **DoD `TheRightGearForTheRightHazard`:**
  - a party in respirators takes half the damage of a party in hazard gear in the gallery, and brings back more;
  - a bold crawler that bought a respirator works the gallery alone.
- **Stop for the producer's look** before W4.

### W4. Practice and repair (2 days)
**Complexity:** Possibly Complex: `ItemInstance.Quality` comes into use, and practice is saved on the contract.
- [ ] **W4.1 Grades:** [Possibly Complex]
  - `ItemInstance.Quality` from the artisan's practice;
  - each grade's step of life and effect, as item data;
  - rarity rims in the merchant terminal and the roster.
- [ ] **W4.2 Repair:** [Possibly Complex] an order at the forge.
- **DoD:**
  - **`PracticeMakesBetterGear`:** over 45 minutes an artisan's grade rises, and its later gear lasts longer;
  - **`TheForgeMendsWornGear`:** a worn piece comes back to full for half the ore of a new one.

### W5. The guildhall (2–3 days)
**Complexity:** Very Complex: G5's one-expedition stage machine becomes several, and parties live on between expeditions. Split it: W5.1–W5.2, then FormParty, then two expeditions at once. The developer supervises the last of these.
- [ ] **W5.1 The guild's limit,** [Basic] from 4 to 8, in `GuildTuning`.
- [ ] **W5.2 Barracks:** [Possibly Complex]
  - bunks kept for members, in the guildhall;
  - a lodging fee taken from wages, as a transfer (economy model §8, if G4.7 or W0.2 measured the need).
- [ ] **W5.3 Parties:** [Very Complex: split FormParty from two expeditions at once; the developer supervises the second]
  - FormParty: a party group lives on between expeditions;
  - two expeditions at once;
  - two trackers.
- **DoD `TwoPartiesOutAtOnce`:**
  - the scripted player keeps eight members, forms two parties and sends them to two sectors;
  - both come back with a haul;
  - every wage is paid, and members sleep in the barracks.

### W6. The gate (1–2 days)
**Complexity:** Possibly Complex: the scenarios and the soak band (set with the producer); the close-out is Basic.
- [ ] **W6.1 `TheWorkshopLoopPlaysThrough`,** [Possibly Complex] 45 minutes on the default seed: build, hire, make, outfit, two parties, hauls, sales.
- [ ] **W6.2 A save mid-craft and mid-expedition** [Possibly Complex] loads byte for byte and plays on.
- [ ] **W6.3 The soak:** [Possibly Complex]
  - a `crafting` profile's band, set with the producer;
  - the other bands re-checked.
- [ ] **W6.4 Seeds 1–120,** [Basic to run; any failure it finds is graded on its own] and the gate seeds with and without the guild and workshop players.
- [ ] **W6.5 The producer's play session.** [Possibly Complex: grade each fix it asks for]
- [ ] **W6.6 Close-out:** [Basic]
  - **the roadmap:** Part 2 COMPLETED, Milestone 3, Epic UI-4 delivered;
  - **the epic, the focus file, `architecture_history.md`, the economy model and AGENTS.md;**
  - this plan is deleted.

## Log
- 2026-11-04: the go ("Begin Epic 10 part 2 work"); Q1–Q12 stand as proposed. W0.1 done: the epic doc, the roadmap, the focus file, this plan.
- 2026-11-04: W0.2 done (above): ore and herbs are plentiful, glowstone scarce, purses small; an artisan qualifies early on every seed.
- 2026-11-04: W0.3 done (`7fd1e45`). W0.4 done (`41f7c4f`, golden `87765c7`, renders `e006175`), with D7 found on the way. W0.5 done: `Crafting`, `WorkshopService` (`SimulationManager.Workshops`), `WorkshopSave`, save format 10; the made item's proof. Core 146/146, client 438/438, golden 3/3.

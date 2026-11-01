# Epic 10 Part 2: workshops and the guildhall (draft plan)

Working document (AGENTS.md, Documentation Protocol 6), one of Epic 10's plans ([index](epic10_plan.md)). Written 2026-10-29 at the producer's request.

**Part 2 is QUEUED:** ROADMAP Milestone 3, Facility Workshops & Crafting, with Epic UI-4. It has no approved draft, so this plan is both the draft and the phased plan. Once the producer has answered §3, §1–§4 move into the epic as Part 2's design, as Part 1's approved Claude Doc did, and this plan keeps the steps.

- **Sources:**
  - **The roadmap's Milestone 3:** "Fixed outpost expansion stations (Forge, Alchemy Still). Hired non-combat artisans refine raw salvage into weapons, armor, and potions."
  - **The game design, Phase 3:** "Upgrading the outpost into a multi-slot facility (*Fallout Shelter* style), hiring non-combat artisans to process raw materials into weapons, armor, and potions". Deeper in the game the player has "a multi-room fortified guildhall housing personal artisans and barracks".
  - **Epic 10 §5's rows owned by Part 2 or Milestone 3:** the pickaxe; gear for each hazard kind, better grades, repair; a guild larger than four, barracks, a guildhall.
  - **Part 1's deferrals** (the G6 plan, §4).
  - **The item refactor's deferrals** (`core_refactor_plan.md`, R7): tool stats in use, quality grades, crafting recipes, repair.
  - **The economy model §8:** crawler sinks.
- **Starts:** after Part 1's close-out and the core refactor's R1–R6 (index, Order).
- **Estimate:** 14–19 agent days: W0 2, W1 3–4, W2 2, W3 2–3, W4 2, W5 2–3, W6 1–2. The roughest of the plans: nothing is measured yet, and W0 measures first.
- **Complexity** ([rubric](rules/complexity-grading.md)): **Very Complex** as a whole: it is epic-sized, and §3 is unanswered. W0, W2, W3, W4 and W6 are Possibly Complex. W1 and W5 are Very Complex: split each before it starts (see their stamps).
- **The producer's stops:**
  - answering §3, which is the go;
  - a look at W3, the first chain from the forge to an expedition;
  - a play session at W6.

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
- **The planner:** about 43 of 64 facts used. R1 guards the limit.

## 1. Goal
**The player promise:** turn the haul into gear nobody sells. Staff a forge and a still with the outpost's careful hands, and send your guild deeper, better kitted.

What Part 1 leaves undone:
- **Raw salvage has one buyer.** Everything goes to the caravan; nothing is made at the outpost.
- **The cautious majority has no part in the guild's story.** The 5–10 residents who won't go into danger alone (M5) only trade.
- **Gear is one item:** the caravan's hazard gear, one grade, the same against every danger.
- **Wages may pile up in purses** (G4.7 measures it), with nothing worth buying.
- **The guild is four,** out as one party at a time.

## 2. The loop
1. **Earn trust and build the guild** (Part 1).
2. **Build** a station, the Forge or the Alchemy Still, in the outpost's new workshop row.
3. **Hire an artisan:** a trusted resident who won't go into danger.
4. **Order** what it makes from the stall's salvage (ore, herbs, glowstone): tools, gear and draughts.
5. **Outfit** the guild from what is made, or **sell** it at the stall.
6. **Go deeper:** gear made for a hazard opens the worst of them.

**The new verbs:** build, hire and dismiss an artisan, set an artisan's wage, order a recipe, price a made item, form a party.

## 3. Questions for the producer (defaults; answers by exception)
| # | Question | Default | Alternative |
| :-: | :--- | :--- | :--- |
| Q1 | **Name and home** | "Epic 10, Part 2: Workshops and the Guildhall", in `docs/epics/epic-10-part-2.md`, with Epic UI-4 folded in. Part 1's file stays Part 1's. | One file for all of Epic 10 |
| Q2 | **The stations** | Two, fixed by the map:<br>• **the Forge:** ore and glowstone into tools and gear;<br>• **the Alchemy Still:** herbs into draughts.<br>The player builds each once, for gold: a new `Construction` sink, 300g each by default. | One generic workbench |
| Q3 | **Who crafts** | Hired artisans: residents at trust ≥ 50 and warmth ≥ 30 toward the player who would not go into danger alone (risk tolerance below 30%). They are paid a wage, as the guild is. One artisan per station to begin with. | Any resident, the bold included |
| Q4 | **An artisan among the groups** (G4's D1) | **A new group kind, `Workshop`:** a station's crew, led by the patron and anchored at the station. Its orders rank after a party's, with the guild's.<br>**One role at a time:** groups would allow a workshop and the guild at once (one of each kind), but by default an artisan isn't also a guild member. | Artisans as guild members with a craft role |
| Q5 | **What is made** | Four recipes (placeholder names):<br>• **a pickaxe:** `HarvestSpeed`; the item exists today, with no source;<br>• **a healing draught:** two herbs, healing 80 at once, carried in the pack;<br>• **a respirator** (foul air) and **a hard hat** (falling rock): each cuts its own hazard by 75%, where hazard gear cuts any hazard by 50%.<br>And **repair** at the forge. | Grades of hazard gear only |
| Q6 | **Weapons** | Not in Part 2: nothing fights before Part 3, so the forge learns weapons there. | Weapons now, unsold until Part 3 |
| Q7 | **Where made things go** | Into the stall's stock. From there:<br>• the player gives them to members, free, as G5's D12 does;<br>• or prices them for anyone. Selling to crawlers takes back the wages they hold: the crawler sink the economy model deferred.<br>The caravan doesn't buy made goods. | The caravan buys made goods at a premium too |
| Q8 | **Quality** | **An artisan's practice:** every 10 items made raise its grade (Common, Uncommon, Rare). Each grade lengthens an item's life and strengthens its effect by a step.<br>**Repair** at the forge: half the ore of a new piece, and durability back to full. | No grades: one quality |
| Q9 | **The guildhall** | • up to 8 members;<br>• barracks with 4 bunks kept for members, who sleep there for a lodging fee taken from their wages;<br>• two parties out at once;<br>• FormParty, so a party outlives one expedition. | The guild stays at four; Milestone 3 only adds stations |
| Q10 | **The map** | The floor grows by two rooms, as map data: a workshop row (the two stations) and the guildhall (barracks and a muster yard). | The stations in the Hub |
| Q11 | **Combat and death** | None, as in Part 1 (its Q2 and Q3) | — |
| Q12 | **Shipping and done** | As in Part 1 (its Q8 and Q10): one PR per milestone; live scenarios headless, with nothing injected; the scripted player acts through commands. | — |

## 4. Milestones
| Milestone | What the player sees | Core (engine-free simulation) | Client (KNI, Epic UI-4) | Complexity |
| :--- | :--- | :--- | :--- | :--- |
| **W0. Groundwork** | The two new rooms, empty | Measurements; recipes as item data; stations as map data; the crafting rule, engine-free; the `Workshop` group kind; save blocks | The rooms drawn and labelled | Possibly Complex |
| **W1. The forge and its artisan** | Build the forge, hire an artisan, order pickaxes. Bold crawlers buy them and harvest faster. | Construction; hiring (Q3, Q4); shifts; crafting from the stall's stock; the pickaxe's `HarvestSpeed` in the harvest | The station window (build, hire, order, progress); a pickaxe row in the merchant terminal | Very Complex (split it) |
| **W2. The still and draughts** | Draughts from herbs; members carry one and keep going | The still; the draught: carried, and drunk under orders below the retreat line | The still's window; draughts in the merchant terminal and the party builder | Possibly Complex |
| **W3. Gear for each hazard** | Respirators and hard hats send the guild into the worst of the gallery | Gear with a hazard kind; recipes with glowstone; "outfit from stock" picks the target's gear; a lone crawler judges a hazard by what its gear leaves | Gear by hazard in the party builder | Possibly Complex |
| **W4. Practice and repair** | Practised artisans make better grades; worn gear is mended | Grades (`ItemInstance.Quality`); practice on the artisan's contract; repair | Grades and rarity rims in the merchant terminal and the roster | Possibly Complex |
| **W5. The guildhall** | A guild of eight, two parties out, members sleeping in barracks | The guild's limit from 4 to 8; barracks; lodging fees; FormParty; two expeditions at once | The roster at eight; two trackers | Very Complex (split it) |
| **W6. The gate** | The whole loop, balanced | Every scenario; the soak with crafting; save and load mid-craft | The producer's play session | Possibly Complex |

**Order:** W0 first. W1 → W2 → W3 → W4 in order; W5 can follow W1, but one agent works them in order. The producer looks at W3 and plays W6.

## 5. The milestones, phased
Each step is a commit. After each: a build with 0 warnings and `dotnet test`. Every milestone passes the index's gates and ends in its PR into `main`.

### W0. Groundwork (2 days)
**Complexity:** Possibly Complex: new data shapes that later milestones build on, a grown map (golden hashes and renders re-recorded), a save block and a group kind.
- [ ] **W0.1 Docs:** [Basic]
  - the answered draft into `docs/epics/epic-10-part-2.md` (Q1);
  - the roadmap: Epic 10 Part 2 ACTIVE, Epic UI-4 folded in;
  - the focus file.
- [ ] **W0.2 Measure** [Possibly Complex] on seeds 1–15, with G6's guild player:
  - the treasury and the members' purses;
  - the stall's salvage stock at each caravan visit, which is the stations' input;
  - planning cost, and the planner's facts in use.
- [ ] **W0.3 Recipes as item data:** [Possibly Complex: a new data shape that W1–W4 rest on] a `Recipe` (station, inputs, output, ticks) beside the item catalog. `ItemSystemProofTests` adds a made item as data.
- [ ] **W0.4 Stations as map data:** [Possibly Complex: the map grows; the gate seeds may move]
  - `StationDefinition`: its kind, its slot, and the sizes of its input and output stock;
  - the two rooms (Q10) in `Floor1Layout`, with nothing built;
  - the map validators.
  - Re-record the golden hashes and the renders for the map, each in a commit of its own.
- [ ] **W0.5 The crafting rule, engine-free:** [Possibly Complex: a save block]
  - a station's inputs, its progress per tick of an artisan's work, its output;
  - its save block;
  - `CraftingValidator`.
- [ ] **W0.6 The `Workshop` group kind (Q4):** [Possibly Complex: the membership order and the directive precedence (G4's D1)] the per-kind rule, the one-role rule, its place in the directive precedence; validators.
- **DoD:** the 23 gate seeds and the soak hold on the grown floor with nothing built. Validators cover recipes, stations and crafting.

### W1. The forge and its artisan (3–4 days)
**Complexity:** Very Complex: it bundles construction, hiring, shifts, a new buy decision and a station window. Split it before starting: the forge (W1.1–W1.3, with its share of W1.5 and W1.6), then the pickaxe (W1.4) as a PR of its own. W1.3 stays Very Complex on its own, so the developer supervises it.
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

## 6. The economy (measured at W0, set at W6)
- **New sink:** `Construction`, each station once (600g in all by default).
- **New transfers:**
  - artisans' wages and fees;
  - made goods bought by crawlers;
  - lodging fees (W5).
- **What changes:**
  - **Exports:** salvage used at a station isn't exported, so the caravan's quota (2 ore, 2 herbs and 2 glowstone a visit) may go unfilled. Exporting or making is the player's choice.
  - **Wholesale gear:** made gear replaces `GearWholesale` purchases.
- **The bands:** every milestone re-runs the soak, and W6 sets a `crafting` band with the producer.

## 7. Risks
| Risk | What we do |
| :--- | :--- |
| Making things starves the caravan's quota, and the fair band falls. | Measure at every milestone. Exporting or making is the player's choice; the band is set at W6. |
| Artisans are cautious crawlers with their own needs, so output is slow. | Shifts come after needs (Part 1's Q6). W1 measures output per minute and tunes recipe times. |
| The planner's facts grow: artisans' work, carrying a draught. | R1's guard. The budget goes from 43 to about 48 of 64. |
| The map grows again (two rooms after G5's one). | Golden hashes and renders re-recorded in their own commits. Well within A*'s cap. |
| Room on screen for stations and two trackers. | Windows, like the merchant and the bounty board (Part 1's rule). |
| The save format changes at most milestones. | Old saves are refused with a reason. |

## 8. Deferred (proposed)
| Left out | Owner |
| :--- | :--- |
| Weapons, and armour against creatures | Part 3 (L2) |
| More stations; station upgrades | After Epic 10 |
| Artisans who also go out with the guild | Q4's alternative, if the producer wants it |
| The caravan buying made goods | Q7's alternative |
| Loot in play | Part 3 (L2) |

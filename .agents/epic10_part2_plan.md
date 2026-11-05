# Epic 10 Part 2: workshops and the guildhall (working plan)

Working document (AGENTS.md, Documentation Protocol 6), one of Epic 10's plans ([index](epic10_plan.md)). Written 2026-10-29 as Part 2's draft. On 2026-11-04 the producer said "Begin Epic 10 part 2 work", Q1–Q12 stood as proposed, and the design moved into [the epic](../docs/epics/epic-10-part-2.md): the goal, the loop, the answered questions, the milestones, the economy, the risks and the deferrals. This plan keeps the steps.

- **Branch:** `feat/epic10-part2`, cut from `main` at `e74db29`. One PR for all of Part 2 (Q12), with a commit per step. Pushed and opened only on the producer's word.
- **Estimate:** 18–25 agent days: W0 2, W1 3–4, T 4–6 (added 2026-11-04), W2 2, W3 2–3, W4 2, W5 2–3, W6 1–2. The roughest of the plans: nothing is measured yet, and W0 measures first.
- **Complexity** ([rubric](rules/complexity-grading.md)): **Very Complex** as a whole, for its size. W0, W2, W3, W4 and W6 are Possibly Complex. W1, T and W5 are Very Complex: split each before it starts (see their stamps).
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
- [x] **W0.6 The `Workshop` group kind (Q4):** [Possibly Complex: the membership order and the directive precedence (G4's D1)] the per-kind rule, the one-role rule, its place in the directive precedence; validators.
  - **D6, precedence is not the kind's value:** group kinds are saved, so `Workshop` takes the next value (3), and a crawler's memberships are ordered by a precedence instead: party, then guild or workshop, then settlement. One role at a time: the guild's rule refuses a workshop member now; W1.2's hiring rule refuses a guild member.
- **DoD:** the 23 gate seeds and the soak hold on the grown floor with nothing built. Validators cover recipes, stations and crafting.
- **Done 2026-11-04:** every step above. Core 146/146, client 438/438, golden 3/3 (re-recorded once, the map part only), seeds 1–120 119/120 as before; the pinned image 438/438 exact; MonoGame and Android compile. Finding F10 (the epic's §7): idle crawlers step over the workshop row's threshold, which moves the late day slightly on 33 of 120 seeds.

### T. A 24-hour day (added 2026-11-04, before W1.3; 4–6 days)
**Complexity:** Very Complex: it re-paces every seed's day, the economy's cadence and the tests' horizons. Split below; the developer supervises T2.2 (sleep) and T4 (the economy per day). Proposed, waiting for the producer's answers before any code.

- **The producer's direction (2026-11-04):** time should be a full 24-hour day, with RimWorld's clock as the baseline (1,000 seconds a day at 1×). A crawler needs 8 hours of sleep a day, and hunger goes from fed to starving over 24 hours.
- **What the numbers show today:**
  - **The day** is the dashboard's 9,000 ticks (900 s), presentation only. RimWorld's 1,000 s is 11% longer: the day itself barely changes.
  - **Hunger** rises 1.5 a second: fed to starving in 67 s, about 1.8 game hours. The target is 15 times slower.
  - **Sleep** is a 3-second bunk visit (5 game minutes) restoring 130 energy. Energy falls 0.2 a second standing and 1.0 walking, so crawlers nap every 2 to 8 game hours. The target, 8 hours asleep, is about 100 times longer.
  - **The economy** is paced by the minute: a caravan visit every 60 s, about 22 meals a minute for 12 crawlers (about 330 a day), the Garden regrowing in 30 s. At two or three meals a day, food demand falls to about 30 a day.
  - **The gate** (23 seeds × 9,000 ticks) runs in 8 s; three days of 10,080 ticks would take about 27 s.
- **Proposed D17, the day:** 10,080 ticks, 1,008 s at 1×: RimWorld's 1,000 s rounded so a game minute is 7 ticks and an hour 420, and every duration in game time is a whole number of ticks. Tick 0 is 06:00 on day 1, as now. A core `DayClock` owns the arithmetic; the dashboard's `OutpostClock` reads it. *Alternative:* exactly 10,000 ticks, with hours of 416⅔ ticks and schedules rounded.
- **Proposed D18, two kinds of time:**
  - **Physical time** stays in ticks as today: moving, poses and their timers, perception, cognition's latency, the actions' lengths (harvest, trade, share), hazard exposure.
  - **The day's time** is set in game hours through the `DayClock`: needs, sleep, the caravan, wages, regrowth, cooldowns, drifters' arrivals and stays, the expedition's muster, bounty expiries, recipes, shifts.
  - T0's table sorts every number into one or the other.
- **Proposed D19, hunger:** rises 100 over 24 hours (100 / 10,080 a tick). The thresholds stay (hungry at 50, the reflex at 60, starving from 85), so a crawler eats two or three times a day, as RimWorld's colonists do. The core keeps counting up; the client may show the meter as "fed", falling from 100 to 0.
- **Proposed D20, sleep is need-driven:** a crawler tires over about 16 waking hours and sleeps in a bunk until rested, about 8 hours. Walking still tires more than standing; the rates are set so the average day on the gate seeds is 16 hours awake and 8 asleep (asserted within an hour). Everyone wakes at 06:00 on day 1, so sleep settles at night, and a night-shift artisan sleeps by day. *Alternative:* scheduled nights (RimWorld's default schedule), which fights night shifts.
- **Proposed D21, beds:** a sleeper holds its bunk a third of the day, so the bunkhouse grows to one bunk per resident (8 → 12). Drifters sleep rough when the bunks are full (resting in place, slower). *Alternative:* keep 8 and make bed scarcity the reason for the guildhall's barracks (W5).
- **Proposed D22, the pace of work:** actions keep their physical lengths (a harvest is still 1.5 s a unit, about 2 game minutes); the economy's numbers are re-derived per day from T0's measurement. *Alternative:* stretch work into game time, RimWorld-like (a harvest of half an hour), so a crawler does tens of tasks a day rather than hundreds.

1. **T0 Measure, and the re-pacing table** [Possibly Complex: a probe and a design note, no game code]: on the gate seeds, per crawler and per day: meals (Garden, stall, pack), sleeps and time asleep, sales, bounty trips, idle time; the stall's flows per day. Then a table of every duration and rate in the core (needs, `Vitality`, `Considerations`, the tunings, the layout's regrowth, memory decay, cooldowns), old → new, physical or day time. The producer reviews the table with T4's numbers.
2. **T1 The day's clock in the core** [Basic: arithmetic and a move, no behaviour change]: `DayClock` (ticks per day, hour and minute; the start at 06:00; day, minute of day, hour, night; hours to ticks). `OutpostClock` reads it. The header's time changes, so renders that show it are re-recorded in the pinned image; golden unchanged.
3. **T2 Needs on the day** [Very Complex as a whole: split into]
   1. **T2.1 Hunger over 24 hours** [Possibly Complex: one rate, but every seed's day changes; golden re-recorded in its own commit], with what was tuned to it: the watch's starving stretch, drifters' arrival hunger, the expedition's muster hunger and rations.
   2. **T2.2 Sleep** [Very Complex, supervised by the developer: a 3-second nap becomes an 8-hour state that must hold live: waking, interruption (starvation, a hazard), bunk claims held for hours, collapse, healing in a bunk]: the waking drain and the bunk's recovery, sleep until rested, the rates measured and asserted.
   3. **T2.3 Beds** [Possibly Complex: a map change; golden and renders re-recorded]: 12 bunks; sleeping rough when they are full.
4. **T3 The day's schedules** [Possibly Complex: mechanical, each number from T0's table]: the caravan's cadence, regrowth, drifters' arrivals and stays, cooldowns, bounty expiries, the expedition's muster, recipes' times, in game hours.
5. **T4 The economy per day** [Very Complex, supervised by the developer: the food trade shrinks about tenfold; a re-derivation, not a tweak]: the ration's price and value, the Garden's yield, the caravan's quota, upkeep and wages, `EarnIncomeGoal.ComfortableGold`; the soak's horizon in days and its bands, proposed to the producer; `docs/economy-model.md` rewritten.
6. **T5 The tests' horizons** [Possibly Complex]: the outpost's day becomes 10,080 ticks; the food and sleep checks run three days; seeds 1–120; golden.
7. **T6 The client** [Basic]: the clock from the core; the needs as they are; renders.
8. **T7 Docs** [Basic]: `AGENTS.md` (the two kinds of time), the roadmap's §1A.1 (the day's durations through the `DayClock`), the economy model, the Game Design Proposal.
- **What T changes in W1.3:** D12's blocks stay (1,680 ticks each). D14, eating on the job, falls away: a block holds no meal at two or three a day, and an artisan eats before or after. D15 changes: 12 hours of work fit inside a 16-hour waking day at idle rates, so working must tire faster than idling, as walking does today (about 1.5 times an idle hour, so 12 hours from rested reach the break line and 8 leave room for the evening). D13 and D16 stand. Re-answered once T is done.

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
- **W1's split (2026-11-04, after W0):** two stretches in this one PR, each ending in its live scenario.
  - **W1a, the forge** [Very Complex as a whole: W1.3; every other step Possibly Complex]
    1. **[x] W1.1 Construction** (done 2026-11-04) [Possibly Complex: a new command, a sink, saved state]: `BuildStationCommand` (the station's index on the map); `WorkshopRules.CheckBuild` (a station, unbuilt, a stall that can pay); `WorkshopTuning` inside `EconomyTuning` (the construction cost, 300g, Q2); the `Construction` sink in the ledger; the refusals in words for the window. Validators: conservation, refusals, the command on its tick.
    2. **[x] W1.2 Hiring** (done 2026-11-04) [Possibly Complex: contracts in the save block, wages at the caravan's visit]: `HireArtisanCommand` (crawler, station, wage), rather than a role on `RecruitCommand`: a station is a different target with different rules, and the guild's command stays as G4 left it. `WorkshopRules.CheckHire` (Q3: a resident, trust ≥ 50, warmth ≥ 30, risk tolerance under 30%; not a guild member, D6; the station built and unstaffed; a wage in range). Hiring makes the station's `Workshop` group (led by the patron, anchored at the station) and an artisan's contract (wage, unpaid visits; W4 adds practice) in `WorkshopSave`. Wages are paid at the caravan's visit beside the guild's; unpaid artisans quit as members do; Dismiss and SetWage for artisans. Validators.
    3. **W1.3 Shifts: the producer's direction and a proposed design (2026-11-04), for the producer's review before any code** [Very Complex, supervised by the developer: a standing order through Epic 8's pipeline that must emerge live after needs; open design until D14 and D15 are answered; after milestone T, which changes both (see T's last note)]
       - **The direction:** shifts make labour scarce: an artisan can't work around the clock. The player assigns a hired artisan to 4-hour blocks across the day. Three blocks in a row deplete its needs; it breaks out of its shift, panicking, to look after them, and loses affection and trust toward the player.
       - **What the numbers say:** a day is 9,000 ticks from 06:00 (the dashboard's clock), so an hour is 375 ticks (37.5 s at 1×) and a block 1,500. Hunger rises 1.5 a second: a crawler is hungry again about every 1.2 game hours. Energy falls 0.2 a second standing (1.0 walking); collapse is at 5.
       - **Proposed D12, blocks:** six a day (06–10, 10–14, 14–18, 18–22, 22–02, 02–06). The player gives each artisan any set of them (`SetShiftsCommand`, a mask on its contract). The day's arithmetic moves from the dashboard into the core, since shifts are simulation now, and the dashboard's clock reads it. A new hire works no block until assigned; the station window offers a day shift (10–18).
       - **Proposed D13, on shift:** the crew's standing order (`DirectiveType.WorkStation`) is active in its blocks and followed through `ComplyDirectiveGoal`, above earning and the hearth; the handler works the station each tick the artisan stands at it. Off shift it lives as any resident.
       - **Proposed D14, it eats on the job:** hunger cycles far faster than shifts, so at the start of each block the artisan takes rations from the stall's stock, as an expedition is provisioned (Part 1's G5), and eats them at the station. With none in stock, it leaves to eat and comes back. *Alternative:* no provisions; it leaves for every meal (more scarcity, much less output).
       - **Proposed D15, stacked shifts spend energy:** working drains energy at the standing rate it already has. A rested artisan has about 40 left after two blocks and reaches the break line (15) in the third, around the 11th hour. Then it breaks: a mental break shown as panicking, as a tantrum is; it walks off its shift to sleep, and doesn't return until rested (energy 60). The rest of that block is lost. Two on and one off is sustainable; three in a row isn't. *Alternative:* a separate overwork meter, slower than energy, if overwork should be apart from tiredness.
       - **Proposed D16, overwork costs trust:** a break costs trust and warmth toward the player (−10 and −5 by default, as an unpaid visit does). Repeated breaks bring an artisan under the quit line (trust 30), and it quits at the next caravan visit (W1.2's rule).
       - **Planner:** one fact at most (40 → 41 of 64). **Measured:** output per block, and breaks a day with two and three blocks in a row.
       - **Before:** the workshop group's standing directive, a new `DirectiveType.WorkStation`, issued at hiring and kept while the artisan is employed. Followed through `ComplyDirectiveGoal` as the guild's expedition is, at a priority under hunger, sleep and the hearth (Part 1's Q6), by an action and intent that walk to the station and work it: the handler calls `Workshops.Work` each tick the artisan stands within its radius. At most one planner fact (40 → 41 of 64). Compliance as Epic 8's: an unwilling artisan may refuse. Measured: output per minute on the default seed.
    4. **W1.5a The scripted player's workshop moves** [Possibly Complex]: `WorkshopPlayer` in `Simpiens.Hosting`, acting only through commands, as `GuildPlayer` does: builds the forge once the treasury can pay, hires the first resident who qualifies, orders pickaxes.
    5. **W1.6a The client** [Possibly Complex: a Gum window and new art; renders and pins re-recorded after a look]: the station window (build, hire from those who qualify, the order and its progress), opened by clicking the station; the forge drawn on the map (built, and its empty plot), as a table row and a generated sprite.
    - **DoD `AForgeIsBuiltAndWorked`:** on the default seed, within 30 minutes, the scripted player builds the forge, hires an artisan when one qualifies and orders pickaxes; at least two are made from the stall's ore and stocked; the construction sink shows in the ledger, which balances, and the day's checks hold.
  - **W1.1–W1.2's design (2026-11-04, before their code):**
    - **D8, hiring is its own command:** `HireArtisanCommand` with its own rules (`WorkshopRules`). Dismiss and SetWage serve both roles, routed by who the crawler is: one role at a time (D6), so it is unambiguous.
    - **D9, an artisan's terms mirror a member's** (Part 1's D2–D4), in `WorkshopTuning` inside `EconomyTuning`:
      - Q3's thresholds: trust ≥ 50, warmth ≥ 30, risk tolerance under 30%;
      - a reservation wage by greed (5g); 5g offered, up to 50g;
      - wages paid at the caravan's visit, after the guild's members, in hire order, in full or not at all, as a conserved transfer counted on its own (`StallPayment.ArtisanWage`);
      - paid, trust and warmth rise; unpaid, they fall; it quits after 2 unpaid visits in a row or below trust 30; a dismissal costs 5 trust; a crawler who quit or was let go can't be hired again for 5 minutes.
      - **No signing fee:** Q3 names only a wage, and the station's construction is the up-front cost.
    - **D10, construction:** 300g from the stall's treasury (Q2), a sink named `Construction`, refused with its reason for an unknown or built station, without a stall, or when the treasury can't pay.
    - **D11, the crew:** a station's first hire makes its `Workshop` group, led by the patron and anchored at the station. The group stays while the station stands: patron-led groups are never succeeded. One artisan staffs a station (Q3). The group carries no order until W1.3's standing directive.
    - **The chronicle** tells of a station built and an artisan hired, unpaid, quitting or let go.
    - **Saves:** the contracts (hire order) and cooldowns (by id) go into `WorkshopSave`, and the artisans' wages into the ledger's totals. The format stays 10 (W0's D5).
  - **W1b, the pickaxe** [Possibly Complex]
    1. **W1.4 The pickaxe** [Possibly Complex: a stat in a handler, a new buy goal, a price that must fit purses (F9)]: `HarvestSpeed` in `HarvestResourceIntentHandler`; the stall's pickaxe row at a price that fits what crawlers carry; a buy decision for crawlers who harvest, as `OutfitForDangerGoal` is for gear; the tool slot's wear.
    2. **W1.5b The live scenario** `AForgeTurnsOreIntoPickaxes` [Possibly Complex]: the draft's DoD below.
    3. **W1.6b The client** [Basic: a row and its words]: the pickaxe in the merchant terminal.
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
- 2026-11-04: W0 closed out. W1 split (above) into W1a, the forge, and W1b, the pickaxe, each ending in a live scenario; W1.3, shifts, waits for the developer to supervise it.
- 2026-11-04: W1.1 (`342ffc6`) and W1.2 (`c94294f`) done. W1.3's direction from the producer and D12–D16 proposed (`855b765`).
- 2026-11-04: the producer widened the scope with a 24-hour day before shifts: milestone T, D17–D22 proposed, waiting for answers.

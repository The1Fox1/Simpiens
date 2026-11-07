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
**Complexity:** Very Complex: it re-paces every seed's day, the economy's cadence and the tests' horizons. Split below; the developer supervises T2.2 (sleep) and T4 (the economy per day). Answered 2026-11-04 (below); waiting for the producer's go before any code.

- **The answers (2026-11-04):** D22, work is stretched RimWorld-style (the producer chose the alternative). D17, D19, D20 and D21 are accepted as proposed (by exception). D18 is revised to follow D22. Accepting is not a go.

- **The producer's direction (2026-11-04):** time should be a full 24-hour day, with RimWorld's clock as the baseline (1,000 seconds a day at 1×). A crawler needs 8 hours of sleep a day, and hunger goes from fed to starving over 24 hours.
- **What the numbers show today:**
  - **The day** is the dashboard's 9,000 ticks (900 s), presentation only. RimWorld's 1,000 s is 11% longer: the day itself barely changes.
  - **Hunger** rises 1.5 a second: fed to starving in 67 s, about 1.8 game hours. The target is 15 times slower.
  - **Sleep** is a 3-second bunk visit (5 game minutes) restoring 130 energy. Energy falls 0.2 a second standing and 1.0 walking, so crawlers nap every 2 to 8 game hours. The target, 8 hours asleep, is about 100 times longer.
  - **The economy** is paced by the minute: a caravan visit every 60 s, about 22 meals a minute for 12 crawlers (about 330 a day), the Garden regrowing in 30 s. At two or three meals a day, food demand falls to about 30 a day.
  - **The gate** (23 seeds × 9,000 ticks) runs in 8 s; three days of 10,080 ticks would take about 27 s.
- **Proposed D17, the day:** 10,080 ticks, 1,008 s at 1×: RimWorld's 1,000 s rounded so a game minute is 7 ticks and an hour 420, and every duration in game time is a whole number of ticks. Tick 0 is 06:00 on day 1, as now. A core `DayClock` owns the arithmetic; the dashboard's `OutpostClock` reads it. *Alternative:* exactly 10,000 ticks, with hours of 416⅔ ticks and schedules rounded.
- **D18, two kinds of time** (revised for D22's answer):
  - **Physical time** stays in ticks as today, and covers only motion and what is shown: walking (2 units a second) and path steps, poses and their timers, panic's sprint, perception's cadence, cognition's latency, the motor watchdog, retries.
  - **The day's time** covers everything a crawler does or undergoes, set in game minutes and hours through the `DayClock`: actions (D22), needs, sleep, hazard damage, healing, gear's wear, the caravan, wages, regrowth, cooldowns, drifters' arrivals and stays, the expedition's muster and stages, bounty expiries, recipes, shifts.
  - T0's table sorts every number into one or the other.
- **Proposed D19, hunger:** rises 100 over 24 hours (100 / 10,080 a tick). The thresholds stay (hungry at 50, the reflex at 60, starving from 85), so a crawler eats two or three times a day, as RimWorld's colonists do. The core keeps counting up; the client may show the meter as "fed", falling from 100 to 0.
- **Proposed D20, sleep is need-driven:** a crawler tires over about 16 waking hours and sleeps in a bunk until rested, about 8 hours. Walking still tires more than standing; the rates are set so the average day on the gate seeds is 16 hours awake and 8 asleep (asserted within an hour). Everyone wakes at 06:00 on day 1, so sleep settles at night, and a night-shift artisan sleeps by day. *Alternative:* scheduled nights (RimWorld's default schedule), which fights night shifts.
- **Proposed D21, beds:** a sleeper holds its bunk a third of the day, so the bunkhouse grows to one bunk per resident (8 → 12). Drifters sleep rough when the bunks are full (resting in place, slower). *Alternative:* keep 8 and make bed scarcity the reason for the guildhall's barracks (W5).
- **D22, work is stretched RimWorld-style (the producer's answer):** a crawler does tens of tasks a day, not hundreds. Today most actions finish on arrival or within 1.5 s. Proposed lengths, tuned in T0's table:
  - **harvest, per unit:** ore and glowstone 30 game minutes (210 ticks, 21 s at 1×), herbs 20 (140), a Garden ration 15 (105);
  - **eat a ration:** 15 minutes;
  - **trade at the stall, claim or deliver at the board, share food:** 10 minutes each;
  - **a scout's survey, treating wounds with a herb:** 30 minutes each;
  - **sleep:** until rested (D20); **the pickaxe** (D2's placeholder): 2 hours at the forge.
  - **Walking stays physical:** crossing the floor (about 50 units) takes about 25 s, some 36 game minutes, roughly RimWorld's pace across a colony.
  - **What it gives:** a working crawler's 16 waking hours, less meals and the hearth, hold some 10–20 units of salvage (a trip of a few units, with the walks, takes about 3 hours). Supply falls along with food demand, so the economy's shape per day may survive with the caravan re-paced; T0 measures both before T4.
  - **What it forces:** hazard damage can't stay per second. A 21-second harvest in the Drowned Gallery (6 a second) costs 126 health, more than anyone has. Damage, healing and gear's wear move to the day's time (D18), re-tuned so a unit of glowstone costs about what it does today (T3.2).

- **How T is built (2026-11-04, while doing T1):** behind the session's tuning, then one cut-over. A crawler's pace becomes data, a `DayTuning` in `EconomyTuning`, as the guild's and the workshops' numbers are. `DayTuning.Legacy` holds today's numbers exactly, and the default stays on it until the cut-over (T5).
  - **Why:** slowing hunger alone would leave the economy's tests red until T4. Building each step behind the tuning keeps every commit green, lets each step prove golden unchanged, and lets the probes measure the new day on a tuned session before it is the default.
  - **The cut-over (T5):** makes the 24-hour day the default and deletes `Legacy`, with golden and the tests re-timed in their own commits.
  - Structural rules take values that reproduce today: a sleep that wakes when rested has a wake energy never reached, and an action of length 0 finishes within its tick.
1. **[x] T0 Measure, and the re-pacing table** (done 2026-11-04: [`docs/time-model.md`](../docs/time-model.md), §2 the measurement and §3 the table) [Possibly Complex: a probe and a design note, no game code]: on the gate seeds, per crawler and per day: meals (Garden, stall, pack), sleeps and time asleep, sales, bounty trips, idle time; the stall's flows per day. Then a table of every duration and rate in the core (needs, `Vitality`, `Considerations`, the tunings, the layout's regrowth, memory decay, cooldowns), old → new, physical or day time. The producer reviews the table with T4's numbers.
2. **[x] T1 The day's clock in the core** (done 2026-11-04, `a846853`, renders `4df916f`) [Basic: arithmetic and a move, no behaviour change]: `DayClock` (ticks per day, hour and minute; the start at 06:00; day, minute of day, hour, night; hours to ticks). `OutpostClock` reads it. The header's time changes, so renders that show it are re-recorded in the pinned image; golden unchanged.
3. **T2 Needs on the day** [Very Complex as a whole: split into]
   0. **[x] T2.0 The pace as data** (done 2026-11-04) [Possibly Complex: cognition and consequences read the session's tuning; golden must hold]: `DayTuning` in `EconomyTuning` (`WithDay`): hunger, energy standing and walking, social, recovery in a bunk and in place, sleep's longest length, wake energy and bonus, rest's length and bonus. `AgentState.Tick` takes the session's pace from the runtime; the sleep and rest actions, the consequence service (`SetPace`, set with the economy) and the bunk's wake rule read it. `DayTuning.Legacy` is the default. Golden 3/3, and T0's probe gives the same whole day on all 23 gate seeds.
   1. **[x] T2.1 Hunger over 24 hours** (done 2026-11-04) [Possibly Complex: one rate behind the tuning; the measurement]: `DayTuning.FullDay`, the 24-hour day's preset, with hunger over 24 hours and social over 16; `DayPaceValidator`. Measured on the gate seeds for three days: 2.4 meals a day, nobody starved (`docs/time-model.md` §2). Drifters' arrival hunger is kept; the expedition's muster moves to T3.2b (stretched work sets the trip's cost); the watch's starving stretch is T5's.
   2. **[x] T2.2 Sleep** (done 2026-11-04, waiting for the developer's review) [Very Complex, supervised by the developer: a 3-second nap becomes an 8-hour state that must hold live: waking, interruption (starvation, a hazard), bunk claims held for hours, collapse, healing in a bunk]: the waking drain and the bunk's recovery, sleep until rested, the rates measured and asserted.
      - **Done, behind `DayTuning.FullDay`:**
        - Energy falls 2.75 an hour standing and 8.25 walking.
        - A bunk restores 8 an hour; sleeping rough (a rest where it stands, or a collapse) restores half that.
        - A sleeper is up when rested (100) or starving (85), after 12 hours at most, with no bonus at the end.
        - The hearth restores company, not energy (`RelaxEnergyBonus`).
        - `Legacy` never wakes a sleeper early: golden 3/3, and T0's whole day is identical.
        - `DayPaceValidator` checks the drain, a bunk's night, a collapse and the wake lines.
      - **Measured** (three days on the gate seeds; `docs/time-model.md` §2): one sleep a day of 8.3 hours, mostly begun 17:00–04:00, 8.6 hours asleep per calendar day; a quarter rough for want of bunks (T2.3); 61 of about 810 sleeps ended by hunger; nobody starved or collapsed.
   3. **[x] T2.3 Beds** (done 2026-11-04, staged for the cut-over) [Possibly Complex: a map change; golden and renders re-recorded at the cut-over]: 12 bunks; sleeping rough when they are full.
      - **Staged, not live:** made live, the four new bunks re-rolled every seed's day at today's pace. Their ids come before the crawlers', so spawns shuffle. Golden failed at t0, along with 6 tests pinned to particular seeds' days: two soak guild bands, two hazard scenarios, a muster and the map validator. Those tests are re-timed at the cut-over anyway, so fixing them for a day about to be replaced would be wasted.
      - **How it's staged:** `Floor1Layout` builds a floor for a number of bunks. `Definition` keeps today's 8; `FullDayDefinition` has 12 in three rows (y −7.5, −9.25, −11, the bunkhouse's walkable depth). `OutpostSession` takes a floor for the day's probes. The map validator checks both.
      - **Measured** on the 12-bunk floor: rough sleep 0.4 hours a day (from 2.0); 7.8 hours a sleep.
   4. **[x] T2.4 Work stretched (D22)** (done 2026-11-04) [Possibly Complex: every handler's length from T0's table; nodes, plots and the stall held for minutes, not ticks (contention); the anomaly detector's checks see long stands; golden]: the actions' lengths in game minutes; a worked pose while an action runs.
      - **Done, behind the tuning:** `ActionTuning` in `DayTuning`, with `Legacy` and `FullDay`.
        - The harvest's, meal's and hand-over's lengths come from the pace unless the intent sets one (validators do).
        - Trades, the board, the survey and treating wounds work for their length before their effect. A shared `StillWorking` helper runs it, and their checks run again each tick.
        - The hearth and the starving crawler's wait at a plot take the pace's length.
        - Harvesting tires at the working rate.
        - `Legacy` reproduces today: golden 3/3, and T0's whole day is identical.
      - **Measured** (`docs/time-model.md` §2): 64 tasks a day, 2.3 meals, one sleep; no stacking from long stands.
        - **A quiet day:** crawlers idle 53% of it, and trust toward the player rose for nobody on 22 of 23 seed runs (1 of 12 on the other).
        - **Why:** crawlers stop earning at 40g and meals are cheap and rare. That is T4's.
      - **No worked pose yet:** a crawler at the stall or the board stands idle, and a harvester keeps its harvesting pose. A new pose is art (T6).
4. **T3 The day's schedules and its dangers** [Very Complex as a whole: split into]
   1. **[x] T3.1 Schedules** (done 2026-11-04) [Possibly Complex: mechanical, each number from T0's table]: the caravan's cadence, regrowth, drifters' arrivals and stays, cooldowns, bounty expiries, the expedition's muster and stages, recipes' times, in game hours.
      - **Most are kept:** the day grew only 11%, so they already read as game hours: drifters every ~5 h staying 4–7 h, the cooldowns ~7 h, a seeded bounty ~14 h.
      - **Changed:** the pickaxe's recipe is now 2 game hours (840 ticks, was 600). It's live: nothing crafts in a live run before W1.5a, so golden holds.
      - **Owned elsewhere:** the caravan and regrowth are T4's; the board's bounty lives (in hours) are T6's; the expedition's stages are T3.2b's.
   2. **[x] T3.2a Damage, healing and wear per game hour** (done 2026-11-04) [Possibly Complex: Part 1's danger numbers (G2, G3) re-expressed; golden]: hazard damage, healing awake and in a bunk, a downed crawler's recovery, gear's wear per hour of exposure, set so a unit of glowstone costs about the health and wear it does today.
      - **Done, behind the tuning:** in `DayTuning`, a hazard's damage scale (0.3), the wear interval (one unit every 3 ticks) and healing awake and in a bunk (10 an hour). The map's hazards keep their figures, so the lone crawler's rule is unchanged. `Legacy` reproduces today (scale 1, every tick, 0.05 and 4 a second): golden 3/3, and T0's whole day is identical.
      - **At the cut-over:** fold the scale into the map's hazards as damage per game hour, and drop it.
      - **Measured:** 32.5 health per glowstone on the new day, against 35.7 today; a venture costs 18 health and nobody is downed.
   3. **[x] T3.2b Who ventures, and the expedition** (done 2026-11-04) [Possibly Complex: measuring; the guild's live scenarios must hold]: the lone crawler's hazard limit (Part 1's G5) and the retreat floors re-measured on the guild player's seeds.
      - **Measured** on seeds 1–15 with the guild player for three days, every resident starting at the recruiting standing (trust from trade is T4's; `docs/time-model.md` §2).
      - **The damage share:** at T3.2a's 0.3, a glowstone cost a party what it does today, but 30% of trips turned back (9% today). At **0.25**, 14% turn back, and a glowstone costs a party 22 health (26 today) and a lone crawler 28 (36).
      - **Kept:** the stages' limits (a trip is about 2 game hours) and the muster (a 4-hour muster gained nothing).
      - **Who ventures alone** is unchanged: the rule reads the sector's own figure.
      - **The probes found, for T4:** on the quiet day nobody reaches the recruiting standing, and members who join quit unpaid (the treasury can't pay wages each visit).
      - **For T5:** some musters end with nobody leaving (6 of 21), because the scripted player sends a party whatever its members are doing.
      - **Probe tool:** `DayTuning.WithHazard` overrides the share and wear for measuring.
5. **T4 The economy per day** [Very Complex, supervised by the developer: the food trade shrinks about tenfold; a re-derivation, not a tweak]: the ration's price and value, the Garden's yield, the caravan's quota, upkeep and wages, `EarnIncomeGoal.ComfortableGold`; the soak's horizon in days and its bands, proposed to the producer; `docs/economy-model.md` rewritten.
   - **What the new day shows** (T2.4–T3.2b, `docs/time-model.md` §2): per resident a day, 64 tasks, 2.3 meals, idle 53%, 2 trades.
     - **The stall** sells 19 times a day and buys 10, and pays 575g of the 1,200g of upkeep due.
     - **Trust** rises for nobody, so nobody can be recruited, and members who join quit unpaid.
   - **The causes:**
     - **The caravan is paced by the real minute:** 15 visits a day, so 15 upkeeps and 15 rounds of wages.
     - **A crawler stops earning at 40g** (`EarnIncomeGoal.ComfortableGold`), and a day's meals cost at most 35g, mostly eaten free: the Garden's 4 plots regrow every 0.7 game hours, about 550 rations a day for a demand of about 30.
     - **Trust fades** 5 points every 1,000 ticks (2.4 game hours) since a crawler's last dealing with the player (`AffinityRecord.RegressTowardNeutral`). That is about 25 lost between two trades half a day apart.
   - **Proposed, for the developer's review before any code:**
     - **D23, prices stay; the volume per day is re-set.** A ration stays 15g and ore 20g, and the trust rules keep their calm values. What changes is how much is traded and owed in a day. *Alternative:* raise prices about tenfold, so a meal is a big purchase.
     - **D24, the caravan visits every 6 game hours,** 4 a day at 06:00, 12:00, 18:00 and 00:00, keeping its per-visit terms: upkeep 80, the quota, restocks, a member's or an artisan's wage. A day's upkeep falls from 1,200g to 320g and wages from 15 to 4 a day. *Alternatives:* once a day (a market day), or 6 a day.
     - **D25, crawlers work for more than a meal.** The purse a crawler stops earning at rises from 40g to about a day's needs and a margin (about 80g), measured so residents work 6–8 hours a day. Made goods (W1.4, the pickaxe; W2, draughts) are what that purse later buys.
     - **D26, the Garden is the baseline, not the whole food supply.** Its plots regrow in game hours, set so the Garden covers about half the day's meals and the stall sells the rest (measured; the economy model's food rule).
     - **D27, trust fades over days, not hours:** 5 points every 6 game hours since the last dealing (from every 2.4). Memory's other decays (where resources and pawns were seen) are measured with T4's busier day.
     - **D28, the soak plays three days** (30,240 ticks), sampled at each visit. The pace bands are re-measured and proposed to the producer, who sets them.
   - **Approved by the developer (2026-11-04): D23–D28.** Split: T4.1 the caravan's cadence (D24); T4.2 motivation and food (D25, D26); T4.3 trust's pace (D27); T4.4 the soak and its bands (D28). Each is measured on the gate seeds at `EconomyTuning.FullDay` before the next.
     1. **[x] T4.1 The caravan every 6 game hours** (done 2026-11-04) [Possibly Complex: a tuning and its measurement]:
        - `EconomyTuning.FullDay`, the day's economy, staged for the cut-over: the day's pace and the caravan every 6 game hours (`SupplyTuning.FullDay` since T4.2b).
        - **Measured:** upkeep is paid in full (320g a day), and the treasury grows from day 2 (481g at the end of day 3).
        - **Trust doesn't build:** the mean stays near 2 (T4.3), and residents idle 42%.
     2. **[x] T4.2 Motivation and food** (done 2026-11-04) [Possibly Complex: tuning and its measurement; a layout fix]:
        - **The purse:** `EconomyTuning.ComfortablePurse` (was `EarnIncomeGoal.ComfortableGold`), 40g today and 80g on the day. Idle falls from 42% to 38%, but harvesting stays about an hour a day.
        - **The Garden is kept** (D26 answered by measuring): crawlers buy nine meals in ten at the stall, and regrowth made no difference.
        - **A bug from T2.3, found and fixed:** the three-row bunkhouse walled a crawler in until it starved. It is now two rows of six, leaving the wall strip free, and a validator rule holds it.
        - **For the developer:** D25's 6–8 hours of work don't follow from D23's prices. A resident spends about 40g a day, and earns that in about an hour of harvesting. Answered by T4.2b (D29, which revises D23's ore price on the day).
     3. **[x] T4.3 Trust's pace** (done 2026-11-04) [Possibly Complex: cognition reads it; measuring]:
        - **How:** `DayTuning.TrustFadeTicks` reaches each crawler's `AgentMemory` (set by its runtime each tick; not saved) and its snapshot. `Legacy` keeps 1,000 ticks.
        - **Taken: 12 game hours, not D27's 6.** At 6, trust rose for fewer than 8 of 12 residents on one gate seed; at 12, it rose on all of them, to a mean of 32 by day 3.
        - **Check:** `DayPaceValidator` test 5.
        - **For the developer:** the guild recruits at trust 60, and on the day that takes several days (F7 at the day's scale). Answered by T4.2b: with more trades, trust builds faster.
     4. **[x] T4.2b Salvage at the day's price** (done 2026-11-05) [Possibly Complex: tuning and its measurement]:
        - **The developer's answer (2026-11-05): option (c), D29.** Salvage pays less, and the caravan takes bigger lots, so a day's meals take hours of work. Food prices, purses and drifters stay as they are. The guild's thresholds stay (answered by exception).
        - **Taken:** ore 5g (worth 2g to a neutral seller; the caravan pays 6g, up to 16 a visit) and herbs 6g (3g; 7g, up to 12): `SupplyTuning.FullDay` and `EconomyTuning.FullDay`. Glowstone keeps its price, as the reward for danger.
        - **A quarter of today's prices, not the third proposed** (`docs/time-model.md` §2): at a third, the stall was full of ore at most visits and residents idled 16%. A quarter, with ore's lot raised to 16, came closest to D25.
        - **Measured,** per resident-day: harvest trips 4.6 h and selling 1.1 h (from 1.4 and 0.4), idle 13% (from 38%). The treasury ends day 3 at 1,075g, and mean trust reaches 72 on day 3.
        - **The guild fills in two days:** four residents reach its standing at a median of day 1.8 (1.6–2.3) on every seed, from 4.5 days and never on 14 seeds.
        - **D25 is near, not met:** about 5.7 hours of earning a day, against 6–8. Made goods to buy (W1.4, W2) bring the rest.
        - **Check:** `EconomyTuningValidator` test 6: the day's prices, every seller still sells, the fairness rule's verdicts as today, and food, purses and glowstone unchanged.
     5. **[x] T4.4 The soak over three days** (done 2026-11-05; bands proposed to the producer) [Possibly Complex: the soak's pace as data; measuring]:
        - **How:** `EconomySoak.Pace`, today's and the day's: the session (economy and floor), the horizon, the sample, the starving rule and the bands. The day's plays three game days, sampled at each caravan visit, starving at 95 for 2 game hours (D28). `simpiens soak --day`.
        - **The profiles' prices are shares of the fair ones** (greedy: food and gear ×2, salvage ×½), so a profile means the same on any economy. On today's economy the shares give exactly the old prices: today's soak is identical, 12/12.
        - **Proposed bands** (`docs/time-model.md` §2, measured over seeds 1–15 and 20261009): fair 600–1,600g at the end of day 3 (measured 771–1,277g); guild 400–1,200g (578–998g); greedy raises trust for 3 at most (0–3); the rest as today, a guild full 8 game hours before the end bringing a haul home.
        - **Drifters work on the day** (Epic 10 Part 2 F11): they leave with more than they brought, but the keen player's stall ends richer with them on all 16 seeds. The day's test asserts that; G1's purse check stays with today's soak until the cut-over.
        - **Tests:** `EconomySoakTests`, the day's soak on the soak's seeds (12 runs), the day's drifters, and each profile's prices on both economies.

6. **[x] T5 The tests' horizons, and the cut-over** (done 2026-11-05) [Very Complex as a whole: every seed's day changes at once, across core, client and the checks; split below]: `DayTuning.FullDay` becomes the default and `Legacy` goes; `FullDayDefinition` becomes Floor 1 (golden, renders); the outpost's day becomes 10,080 ticks; the food and sleep checks run three days; seeds 1–120; golden.
   - **The experiment (2026-11-05, under the AFK directive):** flipping the defaults to the day in the working tree failed 30 of 165 core tests and 42 of 440 client tests. The outpost's day on the 23 gate seeds held. The failures:
     - **expected:** golden, renders, picture pins;
     - **today's numbers held as literals:** the validators that pin the default economy and Floor 1's 8 bunks, the soak's today bands, G1's drifter check;
     - **instant actions assumed:** validators that expect a trade, a claim or a delivery to finish within one call, or a harvest within 200 ticks (on the day they take 10–30 game minutes);
     - **the day's horizons:** scenarios timed to the 9,000-tick day (bunks within 4,000 ticks, the supply loop, the bold and the hazards, expeditions, gear, the proof item; the client's demonstrations within 1,000 ticks).
   - **The decision (architecture first):** make the checks pace-agnostic while today is still the default, so the flip changes only what it must. Validators run an action to completion (a helper) and read rates and prices from the session's tuning rather than literals. Rejected: a fast test pace kept beside the day. It would keep two paces alive and test a game nobody plays.
   1. **[x] T5.1 Pace-agnostic checks** (done 2026-11-05, `94b39f8`) [Possibly Complex: many validators, no behaviour change; golden unchanged]: a `RunToCompletion` helper in `Simpiens.Testing.Core`; validators read the session's pace and prices. Proof: core and client green under today's pace, and the same validators green with the flip patch applied (`scratchpad/t5/flipcheck.sh`).
      - **T5.1b, found by the experiment:** `EarnIncomeGoal` scored the gold a crawler lacked at 0.5 a gold, as a bounty scores its reward, so the day's 80g purse let earning outrank any bounty under about 50g. The need is now a share of the purse, 20 when empty: identical before the day. On the day the work is the same and bounties are taken sooner (the seeded Gather at a median of 3.0 game hours, not 4.6). Measured against capping the need at a 40g gap, which gave the same; the share is scale-free.
   2. **[x] T5.2 The cut-over in the core** (done 2026-11-05, `1e115e0`) [Possibly Complex: every seed's day changes; the scenarios' horizons]:
      - The day's numbers become the defaults: `DayTuning`, `ActionTuning`, `SupplyTuning`, `EconomyTuning`, Floor 1's 12 bunks. `Legacy`, the `FullDay` names and the probe helpers go.
      - The hazard scale is folded into the map's per-sector figures and the lone limit. A quarter is a power of two, so the floats come out the same.
      - Scenario horizons in game days: the outpost's day is 10,080 ticks, and starving is hunger 95 for 2 game hours.
      - The soak's day pace becomes the soak. G1's purse check gives way to the day's drifter check (F11).
      - Core tests green.
      - **What the cut-over found, and decided under the AFK directive:**
        - **D30, earning's need is a share of the purse** (T5.1b, `94b39f8`): measured against capping it; the share is scale-free.
        - **D31, a muster call rouses a sleeper past the tired line** (`AgentState.IsCalledFromSleep`). On the day a party sent at night or at dawn found three of its four asleep, sleeping on to energy 100, and left without them. RimWorld's drafted colonists wake; a member under the tired line (40) sleeps on, needs first (Part 1's Q6). Probed on 8 seeds: every party leaves whole. *Rejected:* teaching the scripted player to wait for dawn. It would hide the rule from the real player.
        - **F12, herbs aren't needed on the day:** a venture costs about 40 health, and a night in a bunk heals 80, so a crawler rarely falls under the herb line (60); 3 herbs eaten in 23 seed-days. G2's scenario no longer asserts herbs. The herb rule stays as a reflex for the badly hurt; W2's draughts bring healing under orders.
        - **The day holds on all 23 gate seeds** in one day, every event well inside its deadline (`docs/time-model.md` §3.5).
   3. **[x] T5.3 Golden re-recorded** (done, `c5e082c`) [Basic: a deliberate behaviour change, recorded in its own commit].
   4. **[x] T5.4 The client on the day** (done: the day-bound tests in `1e115e0`, renders and pins `ef30c1c`) [Possibly Complex: renders and pins need a look]: the demonstration scenes' horizons and the client's other day-bound tests; renders and pins re-recorded in the pinned image after a look at the sheet, in their own commit.
      - **Seen for T6:** the salvage sliders sit at the far left of a range sized for Part 1's prices; the expedition tracker counts its time out in real minutes.
   5. **[x] T5.5 Seeds 1–120 and the gate seeds with the guild player** (done 2026-11-05) [Basic to run; any failure found is graded on its own]: findings into the epic. Seeds 1–120: 120/120 for a day (Part 1's seed 43 and F5's jitter pass on the day); the gate seeds with the guild player over three days: 23/23.
7. **[x] T6 The client** (done 2026-11-05, `e5b8f19`, renders `3f1195e`) [Basic]: the clock from the core; the needs as they are; renders. And what T5.4's renders showed: a bounty's life in game hours (4–24, 12 offered) and its expiry in game time; the merchant's sliders each reaching four times the good's price; spans in game time (`OutpostClock.Span`) for the expedition tracker and a drifter's stay. *Not done:* a worked pose while an action runs: the body kit has none, and a crawler standing at its work reads well enough. Deferred to the art of the stations (W1.6).
8. **[x] T7 Docs** (done 2026-11-05) [Basic]: `AGENTS.md` (the two kinds of time), the roadmap's §1A.1 (the day's durations through the `DayClock`), the economy model (rewritten for the day), the Game Design Proposal.
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
       - **Re-answered on the 24-hour day (2026-11-05, under the producer's AFK directive; the developer's supervision delegated by it)** [W1.3 as a whole stays Very Complex; split below into three Possibly Complex steps]:
         - **D12 stands:** six blocks a day of 4 game hours (1,680 ticks), from 06:00. The contract holds a mask of them; `SetShiftsCommand` sets it. A new hire works no block until assigned; the station window offers the day shift (10:00–18:00).
         - **D13 stands, built on the expedition's pieces:** while its artisan is on shift and the station is built and has an order, the crew's standing directive is `DirectiveType.WorkStation` at the station (the service keeps it each tick, as the guild keeps the expedition's). The member's part is copied to it each tick (`StationOrder`, as `ExpeditionOrder` is). The artisan follows it through `ComplyDirectiveGoal`, judged by Epic 8's compliance every time, so an artisan whose trust has soured stops working. On shift the order binds above the hearth and earning (the expedition's priority); only hunger from 70 comes first.
         - **The planner fact is generalised, not added:** `AtExpeditionWork` becomes `AtDirectiveWork` (same bit, golden unchanged): the directive gives this member work at a site, an expedition's node or a station on shift. The work actions filter by kind (`IsValid`). The planner stays at 40 of 64 facts.
         - **D14 falls away** (T's note): at two or three meals a day a block holds no meal; hunger from 70 takes the artisan to eat, then it comes back.
         - **D15, re-answered:** at the station the artisan works and tires as harvesting does (the day's `EnergyWorkingPerSecond`, 8.25 an hour). Two blocks from rested leave about 34; the third reaches the break line (15) after about 10 hours of work. Tiredness alone doesn't end a shift: on shift the order binds, as an expedition's does; the break line ends it. Two on, one off is sustainable; three in a row is not.
         - **D16 stands:** the break is a panic (the tantrum's pose); it costs trust −10 and warmth −5 toward the player (`WorkshopTuning`), and the artisan is off its shifts until rested (energy 60). Repeated breaks bring it under trust 30, and it quits at the next caravan visit (W1.2's rule).
         - **D32, the order is a command:** `OrderStationCommand` (station, item, how many) reaches `WorkshopService.SetOrder`, with its refusals in words (`WorkshopRules.CheckOrder`). W1.5a's player orders pickaxes with it.
       1. **[x] W1.3a Orders and shifts as commands** (done 2026-11-05, `46327dc`) [Possibly Complex: two commands, saved state in the contract; no live change]: `OrderStationCommand`, `SetShiftsCommand`; `ShiftBlocks` (the block at a tick, the day shift's mask); the contract's shifts and break flag (save format stays 10, D5); `WorkshopRules` checks and words; validators.
       2. **[x] W1.3b Working the station** (done 2026-11-05, `a167054`; golden unchanged) [Possibly Complex: cognition reads a new order; a planner fact generalised; golden must hold]: the crew's `WorkStation` directive kept by the service from the shift; `StationOrder` on the state and the context; `AtDirectiveWork`; `WorkStationAction` and `WorkStationIntent` (the walk there, then each tick at the station one tick of `Crafting`, posed at work so it tires as harvesting does; ending after a game hour or off shift); the compliance priority on shift. Validators: on shift with an order the artisan walks to the forge and the make progresses; off shift it doesn't; golden 3/3.
       3. **[x] W1.3c Breaks** (done 2026-11-05) [Possibly Complex: a rule with a cost; measuring]:
          - **Measured** (a smith hired by hand, 30 pickaxes ordered, three days, 6 seeds): at harvesting's drain (8.25 an hour) the day shift took a smith from 79 to 15 and broke it 0–2 times in three days, about as often as three blocks did. **D15 tuned:** a station's work drains 6 an hour (`DayTuning.EnergyCraftingPerSecond`), between standing and harvesting. The handler marks the tick (`AgentState.WorkedStation`) so the pose stays harvesting's: a new visual state would grow every body's 24 baked frames.
          - **What the trace found next:** a smith off its shift slept rough at the stall, because the 8 bunks it remembered were taken and it had forgotten the other 4 (fixtures fade after 5,000 ticks, 12 game hours). Half a night's rest led to the next day's break. That is D33's, below.
          - **D33, fixtures remembered for three game days** (`AgentMemory.AnchorDecayTicks`): bunks and hearths don't move.
          - **D34, eat before bed** (`DayTuning.BedtimeHunger`, 50): a tired crawler this hungry eats first unless too tired to walk (15), so the hunger line (85) doesn't cut its sleep short. RimWorld's pawns eat before bed. Measured for everyone (23 gate seeds, three days): in a bunk 9.1 h a day (8.9 before), rough 0.22 h (0.49), sleeps cut short by hunger 1 (30); harvesting and meals unchanged, nobody starved.
          - **D35, three blocks in a row break an artisan** (`WorkshopTuning.BreakAfterTicks`, 10 game hours on shift without a gap; `ArtisanContract.ShiftRunTicks`). Energy alone couldn't separate two blocks from three: the difference is about 24 energy, while how well an artisan slept varies more (shifts started anywhere from 57 to 100). Measured with D33–D35 (a smith hired by hand, three days, 6 seeds): the day shift breaks 0–1 times (a badly rested day), three blocks in a row 1–3 times (about daily, trust 87–100); 6–11 pickaxes either way. an artisan on shift at the break line breaks (panic, the costs, the chronicle), off its shifts until rested. Validators; measured with a scripted hire: output per block, and breaks with two and three blocks in a row.
    4. **[x] W1.5a The scripted player's workshop moves** (done 2026-11-05) [Possibly Complex]: `WorkshopPlayer` in `Simpiens.Hosting`, acting only through commands, as `GuildPlayer` does: builds the forge once the treasury can pay, hires the first resident who qualifies, gives it the day shift, orders pickaxes.
       - **Brought forward from W1.4: the pickaxe's stall row** (the stall sells it; opening stock 0; the price W1.4's, provisionally its 30g base value). Without it the forge holds every finished make, and the DoD asks for pickaxes stocked. Nobody buys one until W1.4's buy decision.
       - **The live scenario `AForgeIsBuiltAndWorked`** (the DoD below), in `Simpiens.Core.Tests`, nothing injected. Its horizon in game days, measured.
       - **Done:** on the default seed the player builds the forge at t3470 (14:16 on day 1), hires the first resident who qualifies at t7431, gives it the day shift and orders pickaxes two at a time; two are made and stocked by day 3. The merchant terminal shows each good's stock in its row (six goods outgrew the single stock line).
       - **Finding F13, a just-qualified artisan works about half its shift:** on 6 seeds the scripted player's smith was away from the forge 10–61% of its shift (waiting for ore under an hour). Hired at trust 50 and warmth 30, its compliance (Epic 8) falls under 20 once it is hungry, and it stops working until it has eaten; a smith hired at trust 60 worked 17–22 of 22–24 hours. Paid wages raise trust over days. [Basic: watch at W1.5b and W6]
    5. **[x] W1.6a The client** (done 2026-11-05) [Possibly Complex: a Gum window and new art; renders and pins re-recorded after a look]: the station window (build, hire from those who qualify, the order and its progress), opened by clicking the station; the forge drawn on the map (built, and its empty plot), as a table row and a generated sprite.
       1. **W1.6a.1 Stations on the map** [Possibly Complex: new art; the frame carries the stations]: the per-tick frame copies each station (kind, place, built, at work); the scene draws the forge and the still built, or their empty plots, in both view modes: art rows (`prop/forge`, `prop/forge-plot`, `prop/still`, `prop/still-plot`) and generated sprites. Stations aren't simulation entities (adding entities would shift every id and re-roll every seed), so picking uses an id the presentation derives from the station's index (`StationPick`); a click on one opens its window. Art pins and renders after a look at the sheet.
          - **Done 2026-11-05** (`3b7a036`, pins `4028e81`, renders `57ab978`). Deviations: one plot for every kind (`prop/station-plot`, `ArtKeys.Station(kind, built)`) rather than a plot per kind; a new `DrawRole.FloorMark`, so the flat plot lies under whoever stands on it in isometric instead of sorting by depth; top-down's stations are one cell at the art's own 16 pixels a unit (the first cut drew 2 and 3 cells, chunkier than the walls and larger than isometric's). The isometric still was redrawn after a look (a copper pot on a firebox, not a copper block). `StationView` carries index, kind, place, built, order and lit (a make under way). The click that opens the window is W1.6a.2's.
       2. **W1.6a.2 The station window** [Possibly Complex: a Gum window over a new model]: `StationModel` in `Dashboard/` (the station's name and state; the construction cost and whether it can be built, in the rules' words; residents who would take the work, and their ask; the artisan: wage, shifts as six blocks, on a break, energy; the order: the make, how many, its progress, waiting for inputs) whose actions leave as the typed commands (Build, Hire, SetShifts, Order, Dismiss). `StationWindow` in Gum, opened from the header and by a click on the station; the header's cluster gains a Workshop button. Model tests, a window test, renders after a look.
          - **Done 2026-11-05** (`6ffa1cc`, the anvil's pin `e18152e`, renders in the next commit). Besides the plan: a hire sends the day shift with it (D12: the window offers it); the window lists up to three willing residents, most trusting first, or says why nobody would; the order's line says why it waits (no artisan, a break, no shifts, off shift until when, inputs lacking in the stall, a finished make held). **Architecture:** one window at a time through `IDashboardWindow` (the dashboard closes the others in a loop; each `Open` used to name every other window). **The header didn't fit** a fifth window button at 1280 (the Workshop button ran 43 px under the clock): below 1440 wide the identity keeps its lantern and drops its words, and a test keeps the clusters clear at every offered size with room for a two-digit day. Block labels use a hyphen: the baked fonts hold no en dash.
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
    1. **[x] W1.4 The pickaxe** (done 2026-11-05: `468593a`, `a5a7962`, `5008162`) [Possibly Complex: a stat in a handler, a new buy goal, a price that must fit purses (F9)]: `HarvestSpeed` in `HarvestResourceIntentHandler`; the stall's pickaxe row at a price that fits what crawlers carry; a buy decision for crawlers who harvest, as `OutfitForDangerGoal` is for gear; the tool slot's wear.
       - **The design (2026-11-05, under the AFK directive; RimWorld's mining speed and its tools, Majesty's heroes buying at the market as the guide):**
         - **D36, what a pickaxe does:** worn in the tool slot, it speeds mining: a unit of a mineral (iron ore, glowstone) takes the harvest's time divided by the wearer's `HarvestSpeed` (`CrawlerStats`, 1.5 with a pickaxe). Herbs and food are no faster: a pickaxe doesn't pick herbs (RimWorld keeps mining and plant work apart).
         - **D37, it wears by the unit:** `WearRule.PerUse`, one of its 100 for each mineral unit mined with it; at 0 it breaks and is gone, told in the chronicle. How many game days that is, measured.
         - **D38, who buys one:** a resident (drifters pass through) who wears no tool, knows a mineral node it may mine, isn't hungry or worn out, and carries the price with a ration's price to spare, at or under its reservation price (greed, as gear's). A new planner fact `HasTool` (40 → 41 of 64), a goal `EquipForWorkGoal` (priority 35: above selling salvage, 30, under outfitting for danger, 40) reached through TravelToStall → `BuyToolAction`, a `TradeIntent` to buy, which the trade handler already equips. Glowstone counts only for a crawler who would venture.
         - **D39, its price stays 30g** (its base value): F9 measured residents ending a day with about 55g and a unit of salvage paying 5–6g, so a pickaxe is about six units of work, and at 1.5 times the speed it repays itself within its first 20 of its 100 units. Measured: how many residents buy one in three days, and their purses after.
         - **D40, the stall's pickaxes are the forge's alone:** opening stock 0 (W1.5a's row), so every pickaxe bought was made from the stall's ore. The ore spent in the forge and the pickaxe worn out are Q7's item sinks.
       - **Steps** [Possibly Complex as a whole]:
         1. **W1.4a Mining speed and wear** [Basic: a stat in the handler, wear on use; golden holds, since nobody holds a pickaxe]: the handler divides a mineral unit's time by `HarvestSpeed`; each unit mined wears the tool, and a broken one is gone (chronicle). Validators with a pickaxe given by hand: faster ore, unchanged herbs, wear to breaking.
         2. **W1.4b The buy decision** [Possibly Complex: a planner fact, a goal, an action; golden holds if no pickaxe is ever in stock in the golden run]: `HasTool`, `EquipForWorkGoal`, `BuyToolAction`; `ResourceTargeting.CanBuyTool`. Validators: a resident with the gold buys the stall's pickaxe and wears it; one short of it, or wearing one, doesn't.
         3. **W1.4c Measured** [Basic]: with the scripted workshop player on 6 seeds over three days: pickaxes made, bought, worn out; purses; ore mined per hour with and without. The price is tuned only if nobody can afford one.
       - **Done.** Measured (6 seeds, three days, the scripted workshop player): 2–5 made, 1–3 bought from day 2, always by residents of low greed; day-end purses 24–75g on average; owners mined only 3–16 units. Two changes followed: **D36b**, salvage is chosen by price times mining speed over distance, so an owner goes for ore (4–22 units after; without a tool the score is exactly the old one, golden 3/3); **D37 tuned**, 40 units of wear, not 100: the stall's ore shelf limits an owner to about 5 units a day, so 100 lasted some 20 game days. `HasTool` (fact 7) existed with no production reader and was re-used: no new planner fact. The price stays 30g (D39).
    2. **[x] W1.5b The live scenario** `AForgeTurnsOreIntoPickaxes` (done 2026-11-05, `66a3d74`) [Possibly Complex]: the draft's DoD below. On the default seed: built t3470, hired t7431, two made from the stall's ore, the first bought t26435 (day 3) and mined with at t26691; horizon four game days. W1.5a's `AForgeIsBuiltAndWorked` folded in (same seed and days; its stop, two in stock, no longer held once residents buy).
    3. **[x] W1.6b The client** (done 2026-11-05, `4f41663`, render `fc57084`) [Basic: a row and its words]: the pickaxe's merchant row came with W1.5a; the dossier shows a worn tool and its wear on the gear's line (a line of its own pushed the dossier's buttons off at 720 rows).
- **DoD `AForgeTurnsOreIntoPickaxes`:** on the default seed, within 30 minutes:
  - the scripted player builds the forge, hires an artisan when one qualifies and orders pickaxes;
  - at least two are made from the stall's ore;
  - a crawler buys one, and its harvests take less time;
  - the construction sink shows in the ledger, which balances, and the day's checks hold.

### W2. The still and draughts (2 days) — done 2026-11-05
**Complexity:** Possibly Complex: the forge is the precedent for the still; the draught changes how an expedition retreats.
- [ ] **W2.1 The still,** [Basic, once W1 is done: it copies the forge] built and staffed as the forge is.
- [ ] **W2.2 The draught:** [Possibly Complex: the expedition's retreat rule] the recipe; carried in the pack.
  - **Under orders,** a member below the retreat line drinks one instead of turning back, once per stage of work.
  - **Elsewhere,** a hurt crawler may buy one, as it eats a herb today.
- [ ] **W2.3 The client:** [Possibly Complex] the still's window; draughts in the merchant terminal and the party builder.
- **DoD `ADraughtKeepsAPartyWorking`:** two expeditions to the Drowned Gallery on the same seed, one carrying draughts. The one with draughts works longer and hauls more, with nobody downed.
- **The design (2026-11-05, under the AFK directive; Majesty's heroes buying potions at the market before they go out, RimWorld's medicine as the guide)** [Possibly Complex as a whole]:
  - **What exists already:** the herb reflex (tier 1, before any order) makes any crawler under 60 health eat what it carries of the `Medicine` group, and it fires on an expedition too. So a draught in that group is drunk by the reflex, anywhere, with no new decision. The still is a station like the forge: building, hiring, shifts, orders, the window's tab and the scripted player serve it unchanged.
  - **D41, the still copies the forge:** no new core code for the station; `WorkshopPlayer` takes `StationKind.Still`.
  - **D42, the draught is content:** `ItemType.HealingDraught = 7`; groups `Consumable | Medicine` (not salvage: the stall doesn't buy it and the caravan doesn't take made goods, Q7); heals 80 at once (Q5); a stack of 5. Its recipe at the still: two herbs and an hour's work. Its stall row: sold, opening stock 0, 20g (two herbs at 6g and the work). A new 12×12 flask shape in `ItemShapes`, a line in `ItemArt`, words in `ItemTexts`. Herbs come first when a crawler carries both (id order): the draught is kept for worse.
  - **D43, under orders a member drinks instead of turning back:** the retreat rule (D10 of Part 1) no longer counts a member, not downed, who carries medicine: it will drink at the treat line (60) before the party's floor matters. The carried count is the limit. *Deviation from the draft's "once per stage of work":* the count carried does that job with no new saved state, and the player chooses it.
  - **D44, a party carries draughts:** the dispatch order names how many each member carries (0–2; `DispatchExpeditionCommand`), and the guild gives them from the stall's stock at the muster, with the rations (`Provision`). The expedition remembers the number (its save block, format 10, unreleased).
  - **D45, the bold buy their own:** a resident who would venture (bold and healthy) and knows hazardous salvage, carrying no medicine, buys a draught when its purse spares the price and a ration's, as it buys hazard gear. A new planner fact `HasMedicine` (41 of 64), `CarryMedicineGoal` (priority 38: under gear's 40, so gear comes first), `BuyDraughtAction`; `ResourceTargeting.CanBuyMedicine` (the equipment rule's twin for a pack item). Elsewhere, as the draft said, the hurt drink what they carry; a hurt crawler with none fetches a herb as today (F12: hurt crawlers are rare on the day).
  - **D46, the price:** 20g; measured with D45's buyers (how many carry one, how many drink it).
- **Steps:**
  1. **[x] W2.1 The draught as content** (done 2026-11-05: `9440392`, pins `876e7de`, the merchant's render after) [Basic: an item added the way AGENTS.md says; art pins after a look]: the item, its recipe, its stall row, its art and words; `ItemSystemProofTests`-style checks hold; a validator: the still makes draughts from the stall's herbs; drinking one heals 80. Golden holds (nobody makes one).
  2. **[x] W2.2 Drinking under orders and the party's draughts** (done 2026-11-05, `f5f9eef`) [Possibly Complex: the expedition's rule and its order; saved state]: D43, D44; the reflex counts draughts drunk apart from herbs eaten (`AgentActivity`); `GuildPlayer` can send draughts. Validators: a member under the floor with a draught keeps the party working, without one it turns back; the muster gives the order's draughts from stock; a save mid-expedition keeps the number.
     - **Measured live** (8 seeds, three days, the guild player sending the Drowned Gallery its seam's 4 under Steady, 16 draughts stocked by hand): geared parties fill the goal in 300–460 ticks and never drink, with or without draughts. Bare parties without draughts turn back on 15 of 15 trips (mean haul 3.1, work 279 ticks); with two each, 0 of 13 (haul 4.0, work 460 ticks; 49 drunk, the reflex drinking mid-expedition), nobody downed either way. So W2.5's comparison is between bare parties: draughts stand in for gear, at a cost per trip where gear lasts.
  3. **[x] W2.3 The bold buy their own** (done 2026-11-05, `2c61e53`) [Possibly Complex: a planner fact, a goal, an action]: D45; tests as the pickaxe's. The trade handler judged any non-gear purchase as a ration (its calm value and its counter); it now judges food as a ration and anything else by its own value.
  4. **[x] W2.4 The client** (done 2026-11-05, `0e0fc02`, render `c5777fa`) [Possibly Complex: the party builder's control; renders after a look]: the still's tab shows its recipe (W1's window); the merchant row; the party builder's "Draughts each" stepper with the stock; the dossier's carried draughts.
  5. **[x] W2.5 The live scenario `ADraughtKeepsAPartyWorking`** (done 2026-11-05, `c976e4b`) [Possibly Complex: two runs compared; measured first]: on one seed, the scripted players build and staff the still and send parties to the Drowned Gallery, once carrying draughts and once not; the party with draughts works longer and hauls more, with nobody downed. Measured on several seeds before the assertion is fixed.
     - **Done.** Measured first (8 seeds): the still had made 0–7 draughts by the first dispatch and a member without one turned the party back, so `GuildPlayer` gained a stockpile (it waits for eight, as it waits for gear). Then both runs of a seed send the same tick: without draughts 7 of 8 turned back, with two each none did; work 301–668 against 281–301 ticks; the bold bought 0–6 draughts per seed (D46's demand). *Deviation:* the haul is "at least as big, the seam spent", not "more": the seam yields 4, and a party turning back has usually taken it (one seed hauled 3 against 4). On the default seed: 281 ticks and back, against 302, four drunk, the whole seam.

- **A risk for W3, found at W2.1:** seven goods fill the merchant terminal at 720 rows, and the stall holds 8 (`TradeStallData.GoodsCapacity`). W3's respirator and hard hat make nine: the terminal needs to scroll or compact its rows, and the goods capacity rises (a save format change, unreleased within format 10). [Possibly Complex: plan it in W3]

### W3. Gear for each hazard (2–3 days), then the producer's look — done 2026-11-05, the look pending
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
- **The design (2026-11-05, under the AFK directive; Majesty's heroes taking on what their kit allows, RimWorld's gear by damage type as the guide)** [Possibly Complex as a whole]:
  - **The numbers on the day:** the Sump's foul air 0.75, the Glow Caves' falling rock 1, the Drowned Gallery's flooding 1.5; a lone crawler goes where the rate is at most 1 (`Vitality.LoneHazardLimit`). Hazard gear cuts any hazard by 50%, so by the draft's rule (judge by what gear leaves) it would leave the Gallery at 0.75 and open it to every bold crawler who buys gear today, stripping the seam Part 1 gave to expeditions (D11). The draft's intent is that *respirators* are the player's lever. Hence D51.
  - **D47, protection by kind:** two new pieces, worn in the protection slot (one at a time, G3's D1): the **respirator** cuts the air hazards (foul air, the Gallery's flooding) by 75%, the **hard hat** falling rock by 75% (Q5); hazard gear still cuts any hazard by 50%. As item data: two stats, `AirResistance` and `RockResistance`, beside `HazardResistance`; a hazard's resistance is the general one plus its kind's, at most 0.9 (`CrawlerStats.HazardResistance(inventory, kind)`), read by the damage.
  - **D48, made at the forge from glowstone** (the draft): the respirator from a glowstone and two herbs, the hard hat from a glowstone and two ore, two hours each. Sold at 60g against a neutral crawler's 80g, none to start; they last 1,500 ticks of exposure, as hazard gear.
  - **D49, the stall holds ten goods** (`TradeStallData.GoodsCapacity` 8 → 10; the stall's layout changes inside format 10, unreleased; the serialization validator's pinned size follows). The merchant terminal sizes its rows to fit nine at 720 rows (W3.4).
  - **D50, outfitting by the target's hazard** (W3.2): giving gear, the party builder's outfit and the guild player choose for each member the protection in stock that cuts the target's hazard most, and replace worn gear that cuts it less (as well as none, or half worn).
  - **D51, a lone crawler judges a hazard by what gear made for it leaves** (W3.3, Part 1's deferral): wearing gear against the kind, it judges rate × (1 − that kind's resistance) against the lone limit; general hazard gear softens the harm but doesn't change the judgment. A respirator leaves the Gallery 0.375, so selling respirators opens it to the bold and keeping them for members keeps it the guild's: the player's choice, as the draft wanted. The bold buy the protection that opens salvage they know but can't reach, else hazard gear as today.
  - **D52, how they look:** the hard hat a steel helm (worn on the head, as hazard gear's yellow one); the respirator a mask with its filters, a new way to wear (`Worn.Face`), drawn over the lower face on bodies, portraits and today's figures. Looked at on the art and people sheets.
- **Steps:**
  1. **[x] W3.1 Protection by kind** (done 2026-11-05: `8cae51e`, test `24510d7`, pins `f956928`, renders `6924f6a`) [Possibly Complex: stats in the damage, two items as content, the stall's capacity]: D47–D49's core; validators for the damage per kind and the forge making both. Golden holds (neither is in stock at the start).
  2. **[x] W3.2 Outfitting by hazard** (done 2026-11-05, `ad6ecc1`) [Possibly Complex: the give-gear rule and two callers]: D50. The give-gear command names the hazard; a sound piece replaced goes back to the stall's stock.
  3. **[x] W3.3 Lone crawlers** (done 2026-11-05) [Possibly Complex: it changes who ventures; the soak measured]: D51, with the bold's purchase. The soak runs without a forge, so no respirator exists in it and nothing moves (golden too); the cost of selling respirators shows in W3.5's measurement instead.
  4. **[x] W3.4 The client** (done 2026-11-05: `3b508a2`, pins `68f4f3f`, renders `88ca26d`) [Possibly Complex: a new way to wear across three generators; nine merchant rows; art pins and renders after a look]: D52; the party builder's outfit names its gear.
  - **What W3.5's probe found (6 seeds, six days):** with the guild outfitting for the Gallery, the forge's respirators (2–9) all went to members, trips moving from hazard gear to mixed to respirators, every one hauling the seam's 4; no lone crawler. Sold to the public with no guild, nobody bought one: the default seed's two bold residents wear hazard gear from day 1 (the gear goal wants none worn), and nobody knows the Gallery's one node (seen only from inside, and a bold crawler won't explore the Gallery without a respirator: it can't learn that one would open it).
  - **D53, the bold upgrade** (Majesty's heroes buying better kit at the smith): a crawler wearing protection still wants the piece in stock that would open salvage it can't reach as it is; its world state says it lacks the gear it needs (`HasHazardGear` false then). Buying it, the old piece goes back to the stall as a trade-in, no gold back, as the guild's swap (D50).
  - **D54, what is charted is known:** for that choice, a crawler knows the salvage of charted hazard sectors from the map's plan (its nodes, as the bunks' plan is common knowledge), not only what it has seen. Once it wears the respirator it explores the Gallery and finds the node.
  5. **[x] W3.5a The bold upgrade and know the charted** (done 2026-11-05, `a67038e`) [Possibly Complex: a planner fact read differently, the trade handler's trade-in]: D53, D54; golden holds (no respirator in stock in the golden runs). Measured with respirators sold and no guild: bold residents buy one on 5 of 6 seeds (day 1.3 to 2.7) and work the Gallery alone. The trade-in never takes the same piece again (G3's refusal stands).
  6. **[x] W3.5 The live scenario `TheRightGearForTheRightHazard`** (done 2026-11-05, `72dd630`) [Possibly Complex: measured first]: a party in respirators takes about half the harm of one in hazard gear in the Gallery and brings home at least as much; a bold crawler that bought a respirator works the Gallery alone.
  7. **The producer's look** (prepared 2026-11-05, pending; the epic's §4 lists what to look at) [Basic: prepared, not held]: the art and people sheets and the renders, left for the producer; W4 goes on under the directive.

### W4. Practice and repair (2 days) — done 2026-11-05
**Complexity:** Possibly Complex: `ItemInstance.Quality` comes into use, and practice is saved on the contract.
- [ ] **W4.1 Grades:** [Possibly Complex]
  - `ItemInstance.Quality` from the artisan's practice;
  - each grade's step of life and effect, as item data;
  - rarity rims in the merchant terminal and the roster.
- [ ] **W4.2 Repair:** [Possibly Complex] an order at the forge.
- **DoD:**
  - **`PracticeMakesBetterGear`:** over 45 minutes an artisan's grade rises, and its later gear lasts longer;
  - **`TheForgeMendsWornGear`:** a worn piece comes back to full for half the ore of a new one.
- **The design (2026-11-05, under the AFK directive; RimWorld's crafting skill and item quality, Majesty's smithy upgrades as the guide)** [Possibly Complex as a whole]:
  - **What exists:** `ItemInstance.Quality` (0 Standard, 1 Fine, 2 Masterwork) stamped 0 everywhere; the stall's stock is a count per good, so the grade of what the forge makes has nowhere to live once stocked. Worn pieces replaced by the guild (D50) or traded in (D53) go back into stock *as new* today, which repair should end.
  - **D55, practice:** the artisan's contract counts its makes (`ArtisanContract.Practice`, saved; inside format 10). Its grade is Standard until 10 made, Fine from 10, Masterwork from 20 (Q8: every 10 raise it, to three grades).
  - **D56, the grade on the item:** for what is worn (tools and protection), each grade above Standard lengthens its life by a quarter and strengthens its effect by a tenth (item data on `EquipmentBlock`: `LifePerGrade`, `EffectPerGrade`): a Fine pickaxe lasts 50 units and mines 1.55×, a Masterwork respirator stops 0.9 (the cap). `CrawlerStats` reads the worn instance's grade. *Left out:* draughts have no grade (used once, no life to lengthen); their makes count toward the still's artisan's practice all the same, for when they do.
  - **D57, the shelf by grade:** a stall's good counts its Fine and Masterwork pieces within its stock (`StallGood.Fine`, `.Masterwork`); the forge stocks a make at its artisan's grade (a held make keeps its grade, `StationState.HeldGrade`); a sale or a gift hands out the best grade first, at the good's one price (the player prices the good; a grade is the buyer's luck). The caravan's hazard gear is Standard.
  - **D58, worn pieces wait on the shelf** (`StallGood.Worn`, not for sale): a sound piece the guild replaces (D50), one traded in (D53) and one a member hands in at half its life (D12) go there, no longer back into stock as new.
  - **D59, repair is an order at the forge** (Q8): `OrderStationCommand` gains a repair flag; a repair takes one worn piece and half of each of its recipe's inputs (rounded up) and half its work, and stocks the piece full, at Standard. The station window offers it for goods with worn pieces waiting.
- **Steps:**
  1. **[x] W4.1 Practice and grades** (done 2026-11-05, `2297af8`) [Possibly Complex: a contract field, the shelf's layout, stats by instance]: D55–D57; validators (grade from practice, the shelf by grade, best first, a Fine piece's life and effect). Golden holds (nobody crafts in the golden runs).
  2. **[x] W4.2 Worn pieces and repair** (done 2026-11-05, `abcf11c`, rounding `2499069`) [Possibly Complex: a command flag, a make that consumes a worn piece]: D58, D59; validators. *Changed in the doing:* one rule for a piece handed back (`Crafting.HandBack`): the shelf's worn count for what the forge can mend, back into stock while sound for hazard gear as before; a repair's inputs are halved rounded down, so a respirator is mended without glowstone.
  3. **[x] W4.3 The client** (done 2026-11-05: `cb648be`, pins `040bfbe`, renders `9c9d78c`) [Possibly Complex: rims by grade; the window's repair; renders after a look]: rarity rims by grade in the merchant terminal and the roster; the stock line by grade; the station window's artisan grade and a repair row.
  4. **[x] W4.4 The live scenarios** (done 2026-11-05, `39eee16`) [Possibly Complex: measured first]: `PracticeMakesBetterGear` (within some game days the artisan's grade rises and a later piece lasts longer) and `TheForgeMendsWornGear` (a worn piece comes back full for half the inputs).

### W5. The guildhall (2–3 days) — done 2026-11-06
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
- **The design (2026-11-05, under the AFK directive; Majesty's guilds, whose heroes live in their hall and go out in parties, and RimWorld's owned beds and standing caravans, as the guide)** [Very Complex as a whole; split below, and re-graded after the architecture step]:
  - **What exists:** G5's guild holds one `ExpeditionRecord` and one party list (`GuildService.Expeditions`), read by 20 files: the roster window and its model, the scripted player, cognition's refusal hook, the soak, the world hash and the save (`GuildSave.Expedition`, `.Party`). A party is a `TemporaryParty` group made at dispatch and disbanded when the trip ends. Every bunk is common (`BunkTargeting`: the nearest free one a crawler remembers), and the guildhall stands empty (W0.4).
  - **The architecture first (the directive's pivot):** two expeditions at once is a loop over parties once the stage machine runs per party. It is a refactor of G5's code with no change in behaviour, so it comes first, on its own, proven by the golden hashes and the guild scenarios' unchanged numbers. FormParty and two at once then become small changes on top.
  - **D60, expeditions per party:** the guild holds parties (`GuildParty`: its slot, its group, its roster of `ExpeditionMember`s, its `ExpeditionRecord`), and the stage machine, the provisioning, the haul and the shares run per party. The save holds the parties (`GuildSave.Parties`, inside format 10), the world hash mixes each, and the tracker reads one. `ExpeditionRecord.IsActive` becomes "a stage is set", since a party's group outlives its trips (D65).
  - **D61, eight members, parties of four:** `GuildTuning.MaxMembers` 8 (Q9); a party still has at most four (`DispatchExpeditionCommand.MaxMembers`), and the guild has two (`GuildTuning.MaxParties`).
  - **D62, the barracks: a bunk kept for every member,** 8 where Q9 said 4. On the 24-hour day a sleeper holds its bunk about 8 hours (D21), so four bunks would sleep four of the eight.
    - **The map:** two rows of four in the guildhall's east half, the bunkhouse's spacing and its walkway along the north wall, as `PoiKind.GuildBunk`, spawned as `EntityType.GuildBunk`. No one remembers them as common bunks, and they are drawn with the bunk's art.
    - **Kept per member:** the guild keeps one for each member, the first free one at signing, in the contract's spare byte. A member sleeps in its own bunk wherever it is (`BunkTargeting`: its own bunk first, as a colonist goes to its own bed). Without one, it uses the common bunks.
  - **D63, the lodging fee:** 1g a caravan visit (`GuildTuning.LodgingFee`), taken from the wage of each member with a kept bunk. The stall pays the wage less the fee, never below 0; the fee stays in the treasury and the ledger records the net wage. So the economy model's "transfer" is a deduction: no gold leaves a member's purse.
  - **D64, the muster stays at the stall:** the stall is where the guild provisions, outfits and takes in the haul, and a yard past the workshop row would add the row and the gate to every trip. Q10's muster yard is the guildhall's floor west of the bunks, with no rule of its own. When the player places the guildhall (the long-term direction), the muster may move there.
  - **D65, parties outlive expeditions (FormParty, Q9):**
    - **The command:** `FormPartyCommand(slot, members)` sets a party's roster: up to four guild members. Members in another party move over, and no members empties it. It is refused while the party, or a member's party, is out.
    - **The group:** a `TemporaryParty` group, made when the party is formed and disbanded when it is emptied. Dispatch names the slot and sends its members, none downed (G5's rule).
    - **After a trip:** the party stays formed, its directive cleared. A member who stayed behind (late at the muster, or it refused the order) leaves the party's group for the trip, so the directive doesn't draw it along, and rejoins at the end. One who deserted or left the guild leaves the party, and so does any member who leaves the guild.
  - **D66, two at once:** each party has one expedition, so two can be out. Two parties never work the same sector (a new refusal, `SectorTaken`), and node claims stay within a party. Recall names the slot.
  - **The window** (measured on the roster's render: a card and its gap take 72 rows, and the window around one card takes 330, so eight cards and the expedition section would need about 860 rows against the 650 a 720-row window leaves):
    - **Two tabs,** Members and Expeditions, in a row under the window's header that also holds the status line ("8 of 8 · 40g in wages a visit").
    - **Members:** eight cards of 48 rows. Each has two lines: the name and what it is doing; health, standing and gear, with unpaid visits in warning colour. The wage slider and the buttons stay on the right. Each card's party button cycles "No party", "Party 1", "Party 2" and sends FormParty for the parties it changes (W5.3a; until then "Goes"/"Stays").
    - **Expeditions:** G5's builder, Dispatch and Recall, and the tracker line. From W5.3b, a panel for each party.
- **Steps:**
  1. **[x] W5.0 Expeditions per party** (done 2026-11-05, `c1f7f29`; the eight guild scenarios printed the same 80 lines) [Possibly Complex: a refactor of G5's stage machine across 20 files, with no change in behaviour]: D60. Dispatch still names members, and forms and disbands the party, one at a time. Done when the golden hashes hold and the guild scenarios (`ExpeditionScenarioTests`, `DraughtScenarioTests`, `RightGearScenarioTests`, `PracticeAndRepairScenarioTests`) print the same numbers as before.
  2. **[x] W5.1 Eight members** (done 2026-11-05: `0dcaa09`, renders `b349d46`; the soak's guild profile and the `--guild` sweep keep Part 1's guild of four until W6) [Possibly Complex: the limit is a number; the work is the roster's layout for eight]: D61. The tuning; the window's tabs and compact cards, with a test that eight cards and the Expeditions tab fit at 1280×720; the renders after a look.
     - **Measured first** (2026-11-05, a probe with the limit at 8 and the scripted player recruiting up to 8, 4 game days on 12 gate seeds):
       - **Without the workshops:** eight by day 3–5 on 8 of 12 seeds, the default seed (20261009) by day 3.4. The others reach 4–6: a few residents' trust grows slowly. Treasuries end at 518–1,356g; the lowest dips to 0–120g, and no wage went unpaid.
       - **With the forge's and the still's players as well:** eight on 3 of 12 seeds. The treasury hits 0 on every seed (two stations' 600g, the artisans' wages, eight members' wages), and on four seeds members quit unpaid. That is W6's loop: F14, below.
  3. **[x] W5.2 The barracks** (done 2026-11-06: `bd1c2d7`, golden `d9f5989`, renders and pins `ae33c2b`) [Possibly Complex: map data, so the golden hashes and renders are re-recorded in their own commits; a bunk kept per member; the fee]: D62–D64. A validator for keeping, freeing and restoring a bunk and for the fee. Members sleeping in the barracks is measured on the gate seeds.
     - **Measured:** on 8 gate seeds over four days, members slept only in their own barracks bunks (61–178 bunk-hours a seed), none in the common bunks; no one else took a barracks bunk; nobody starved; no day failed.
     - **Found on the way:** eight more map entities move every later id, so play moved on every seed and six tests that leaned on where crawlers happened to be broke. Each was made robust rather than re-tuned: demand by price is now a whole day at each price; a load is followed for half a day; and so on (the commit lists them). F15 is the one that matters for play.
  4. **[x] W5.3a FormParty** (done 2026-11-06: `4deeb00`, renders `eeb07e1`) [Possibly Complex: a command, and parties that persist]: D65. Dispatch and recall name a slot; the roster's party buttons; the scripted player forms its party once. `ExpeditionTests` and the guild scenarios follow, re-measured: the party's group now exists before dispatch.
  5. **[x] W5.3b Two at once** (done 2026-11-06: `6b554e6`, `c2982d8`; two trackers `ee2958a`, `fd130be`) [Possibly Complex after W5.0; it was Very Complex, with the developer to supervise, which the AFK directive delegates]: D66. The rules, cognition's refusal hook by party, the scripted player with two parties and two targets, the two trackers. The risk lies in play (two musters at the stall, the stock split two ways), which W5.4 measures.
  6. **[x] W5.4 `TwoPartiesOutAtOnce`** (done 2026-11-06, `746a9d7`; measured first: each party alone, the caravan's gear outfits one a visit and they take turns, so the scripted player sends them `together`) [Possibly Complex: measured first]: the DoD above, on the default seed, with nothing injected.

### W6. The gate (1–2 days)
**Complexity:** Possibly Complex: the scenarios and the soak band (set with the producer); the close-out is Basic.
- [x] **W6.1 `TheWorkshopLoopPlaysThrough`,** [Possibly Complex] 45 minutes on the default seed: build, hire, make, outfit, two parties, hauls, sales.
- [x] **W6.2 A save mid-craft and mid-expedition** [Possibly Complex] loads byte for byte and plays on.
- [x] **W6.3 The soak:** (the band proposed by exception, D68) [Possibly Complex]
  - a `crafting` profile's band, set with the producer;
  - the other bands re-checked.
- [x] **W6.4 Seeds 1–120,** [Basic to run; any failure it finds is graded on its own] and the gate seeds with and without the guild and workshop players.
- [ ] **W6.5 The producer's play session.** [Possibly Complex: grade each fix it asks for]
- [ ] **W6.6 Close-out:** [Basic]
  - **the roadmap:** Part 2 COMPLETED, Milestone 3, Epic UI-4 delivered;
  - **the epic, the focus file, `architecture_history.md`, the economy model and AGENTS.md;**
  - this plan is deleted.
- **The design (2026-11-06, under the AFK directive)** [Possibly Complex as a whole]:
  - **Measured first: the whole loop as it stands** (the forge making respirators, the still draughts, the guild of eight in two parties sent together; 6 game days, 5 seeds):
    - **The day's check was wrong:** four seeds failed "crawlers went alone where only an expedition goes", every one a resident in a respirator the forge made, which D51 allows. The observer now judges what a crawler's gear leaves of the hazard (W6.0, `c7e21a5`).
    - **The scripted players spend like nobody minds:** the stations go up the moment the stall can pay, and the guild re-signs members who quit unpaid, 20g a time (seed 1 signed 21 and ended with none). Both now take an optional reserve (W6.0).
    - **F14 is the economy's balance:** with a 200g reserve for the guild no wage went unpaid, but treasuries hover at 0–300g. On the default seed the stall affords the forge and a guild of eight, never the still as well (its 300g never comes back). Where the money goes: upkeep 320g a day, eight members' wages about 130g a day after lodging, two artisans' about 40g, the stations 600g once; the guild's glowstone feeds the forge's respirators instead of the caravan's exports.
  - **D67, the gate's player is a cautious one:** the full-loop scenario and the `crafting` soak profile use the scripted players with reserves, builds before recruiting, as a player who wants both stations would. That's input, not a rule.
  - **Done:** W6.0 `c7e21a5`; W6.1 and W6.2 `bed8565` (on gate seed 1: the deviation in D67's commit). W6.3, the `crafting` profile, its band proposed by exception (economy model §7: treasuries of 1–390g at day 3; the band asks for integrity). W6.4: seeds 1–120 pass the outpost's day without and with the guild player (120/120 each, 2026-11-06); the 23 gate seeds hold two days with the workshop players and the crafting guild (23/23).
  - **D68, no retuning of the economy under the directive:** the producer sets the `crafting` band (W6.3). The soak measures the loop as it stands and proposes a band by exception; F14's numbers and the levers (station cost, upkeep, glowstone's export price, members' wages) go to the play session.

## Log
- 2026-11-04: the go ("Begin Epic 10 part 2 work"); Q1–Q12 stand as proposed. W0.1 done: the epic doc, the roadmap, the focus file, this plan.
- 2026-11-04: W0.2 done (above): ore and herbs are plentiful, glowstone scarce, purses small; an artisan qualifies early on every seed.
- 2026-11-04: W0.3 done (`7fd1e45`). W0.4 done (`41f7c4f`, golden `87765c7`, renders `e006175`), with D7 found on the way. W0.5 done: `Crafting`, `WorkshopService` (`SimulationManager.Workshops`), `WorkshopSave`, save format 10; the made item's proof. Core 146/146, client 438/438, golden 3/3.
- 2026-11-04: W0 closed out. W1 split (above) into W1a, the forge, and W1b, the pickaxe, each ending in a live scenario; W1.3, shifts, waits for the developer to supervise it.
- 2026-11-04: W1.1 (`342ffc6`) and W1.2 (`c94294f`) done. W1.3's direction from the producer and D12–D16 proposed (`855b765`).
- 2026-11-04: the producer widened the scope with a 24-hour day before shifts: milestone T, D17–D22 proposed, waiting for answers.
- 2026-11-04: T answered (D22: work stretched; the rest as proposed), and the go ("Proceed with the time rework"). T0 done: today's day measured on the gate seeds (about 190 tasks, 31 meals and 6 s in a bunk a resident-day) and the re-pacing table written, in `docs/time-model.md`.

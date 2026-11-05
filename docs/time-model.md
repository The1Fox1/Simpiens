# The Outpost's Day: the Time Model

- **Owner:** [Epic 10 Part 2](epics/epic-10-part-2.md), milestone T (a 24-hour day, the producer's direction of 2026-11-04). Status is owned by the [roadmap](ROADMAP.md).
- **The steps:** `.agents/epic10_part2_plan.md`, milestone T. Each step marks its rows here as done.
- **Decisions:** D17–D22 in the epic's §0 and the plan's T. RimWorld's clock is the baseline (1,000 s a day at 1×).

## 1. The rules
- **The clock:** the simulation ticks 10 times a second at 1× (`SimulationClock`), as before. A day is **10,080 ticks** (1,008 s at 1×): a game minute is 7 ticks, an hour 420. Tick 0 is 06:00 on day 1. `DayClock` in the core owns the arithmetic, and the dashboard's clock reads it (D17).
- **Two kinds of time (D18):**
  - **Physical time** covers motion and what is shown, and stays in ticks and seconds:
    - walking at 2 units a second, and path steps;
    - poses and their timers;
    - panic's sprint;
    - perception's cadence and cognition's latency;
    - the motor watchdog, retries and backoffs;
    - ambient idling and wandering, which are motion.
  - **The day's time** covers everything a crawler does or undergoes, written in game minutes and hours and turned into ticks by `DayClock`:
    - actions;
    - needs, sleep, hazard damage, healing and gear's wear;
    - schedules: the caravan, regrowth, drifters, cooldowns, bounty expiries, the expedition's stages, recipes and shifts.
- **Work is stretched RimWorld-style (D22):** actions take game minutes, so a crawler does tens of tasks a day, not hundreds. Walking stays physical: crossing the floor (about 50 units) takes about 25 s, some 36 game minutes.

## 2. Today's day, measured (T0, 2026-11-04)
The 23 gate seeds, residents only, one day of 9,000 ticks (the dashboard's day before T), `feat/epic10-part2` at `1552c36`. Per resident per day:

| Activity | Time | Starts | Mean length |
| :--- | ---: | ---: | ---: |
| Wandering | 344 s (38%) | 60 | 5.7 s |
| Idling | 253 s (28%) | 85 | 3.0 s |
| No intent (between decisions) | 149 s (17%) | | |
| Harvesting (walk included) | 125 s (14%) | 23 | 5.5 s |
| Eating from the pack | 18 s | 20 | 0.9 s |
| Asleep in a bunk | 6 s | 2.2 | 2.9 s |
| Sharing food | 3 s | 1.5 | 1.9 s |
| Scouting, panic, retreat | 2 s | 0.2 | |

- **Tasks:** about 190 started a day. Trades, bounty claims and deliveries finish within the tick they start, so they don't show as time.
- **Walking:** 409 s, 45% of the day.
- **Meals:** 31 a day (hunger falling 10 or more in a tick), at a mean hunger of 41.
- **Sleep:** 2.2 bunk naps a day of 3 s each, taken at a mean energy of 30. 0.03 collapses a day; the lowest energy seen is 5.
- **The stall, per day:**
  - it sells to crawlers 137 times (2,073g, nearly all rations at 15g) and buys from them 80 times (1,949g of salvage);
  - the caravan: 271g of wholesale rations, 1,200g of upkeep, 2,052g paid for exports;
  - 24g of wholesale gear.

**What the targets change:**
- **Meals:** at two or three a day, food demand falls about twelvefold.
- **Sleep:** goes from 6 seconds a day in a bunk to 8 hours.
- **Tasks:** stretched work takes them from about 190 a day to tens.

### Measured along the way
- **T2.1, hunger over 24 hours** (2026-11-04): the gate seeds for three days each (69 seed-days) at `DayTuning.FullDay`, with everything else still at today's pace.
  - **Meals:** 2.4 a resident-day; nobody starved.
  - **With little to do,** crawlers idle 83% of the day and harvest for 10 s.
  - **The stall** sells 27 times a day (from 137) and can't pay its upkeep in full: 704g a day of the 1,200g due.
  - Expected: actions (T2.4) and the economy per day (T4) answer it.
- **T2.2, sleep** (2026-11-04): the same 69 seed-days at `DayTuning.FullDay`, with hunger, social and sleep on the day and actions still at today's lengths.
  - **Sleep:** about one sleep a day, 8.3 hours long, begun at energy 37. On whole calendar days (days 2 and 3), 8.6 hours asleep.
  - **When:** crawlers fall asleep from 17:00 to 04:00, most at 23:00; they wake from 00:00 to 12:00, most around 07:00 and noon.
  - **Where:** 5.3 hours a day in a bunk and 2.0 sleeping rough. With 8 bunks for 12 residents and the drifters, a quarter of sleep is on the floor, which T2.3's beds answer.
  - **Hunger:** 61 of about 810 sleeps ended at the starvation line (85), and the sleeper went to eat. Nobody starved or collapsed.
  - **Walking:** 16% of waking time. Stretched work (T2.4) will change the mix, so the rates are re-measured there.
- **T2.3, a bunk per resident** (2026-11-04): the same, on Floor 1 with 12 bunks (`Floor1Layout.FullDayDefinition`).
  - **Sleep:** 7.8 hours a sleep, 7.7 per calendar day.
  - **Where:** rough for 0.4 hours a day, down from 2.0.
  - **When:** most crawlers wake at 07:00.
  - **Hunger:** 34 sleeps ended by hunger, down from 61. Nobody starved or collapsed.
- **T2.4, work stretched** (2026-11-04): the same 69 seed-days on the 12-bunk floor, at `DayTuning.FullDay` with `ActionTuning.FullDay`. Per resident per day:
  - **Tasks:** 64 started, down from about 190: tens a day, as D22 asks.
  - **Needs:** 2.3 meals and one sleep, begun at energy 37.
  - **Time:** idle 531 s (53%), asleep 280 s, wandering 118 s, harvesting 32 s (walks included), eating 17 s, trading 14 s (2 trades). Walking is 11% of the day.
  - **The stall, per seed-day:** 19 sales to crawlers, 10 purchases of salvage, 575g of upkeep paid.
  - **The watch:**
    - Nobody starved, collapsed or broke the invariants.
    - On 22 of 23 seed runs, trust toward the player rose for nobody (it needs 8 of 12), and for 1 of 12 on the 23rd. Trust comes from trades, and crawlers trade twice a day.
    - Long stands at nodes and the stall raised no stacking; one seed showed a jitter.
  - **Why the day is quiet:** a crawler earns until it carries 40g (`EarnIncomeGoal.ComfortableGold`), and a day's 2.3 meals cost at most 35g, much of it eaten free at the Garden. Its reasons to work are the economy's, and T4 sets them per day.
- **T3.2a, danger on the day** (2026-11-04): the gate seeds, residents and drifters.
  - **Today's pace, one day:** 8.8 ventures into hazard sectors a day, each 8.9 s long, costing 26 health; 36 health per glowstone; nobody downed.
  - **The new day, at per-second damage** (three days each): a venture lasts 15 s and costs 47 health. Crawlers retreat (under 40) before a 21-second harvest ends, so glowstone falls to 0.1 a day, at 667 health each.
  - **With a 0.3 share of the damage and gear worn every 3 ticks** (three days each): a venture lasts 22 s and costs 18 health (median 12); 32.5 health per glowstone; nobody downed and no retreats.
  - Ventures are fewer (2.1 a day against 8.8) and glowstone with them (1.0 a day): the quiet day's, T4's.
- **T3.2b, the expedition on the day** (2026-11-04): the guild player on seeds 1–15 sending parties to the Drowned Gallery. Every resident starts at the recruiting standing toward the player (trust 60, warmth 40), because trust from trade is T4's: on the quiet day nobody qualifies for the guild.
  - **Today's pace, 30 minutes:** 45 trips, 4 turned back (9%); 26.1 health per glowstone to the members.
  - **The new day, three days, by the share of hazard damage taken:**

| Share | Parties turned back | Party health per glowstone | Lone health per glowstone |
| :--- | ---: | ---: | ---: |
| 0.3 | 6 of 20 (30%) | 26.9 | 32.5 |
| 0.25 | 3 of 21 (14%) | 22.4 | 28.1 |
| 0.2 | 0 of 25 | 17.6 | 21.2 |
| 0.15 | 0 of 25 | 12.9 | 15.3 |

  - **Taken: 0.25.** At 0.3 a glowstone costs a party what it does today, but turn-backs triple: a party's damage comes in concentrated stretches of 21-second harvests, and members heal more slowly between trips.
  - **A trip:** about 2 game hours. Its stages stay far inside their limits (longest march 182 of 1,800 ticks, work 457 of 1,800, return 274 of 2,400), so the limits are kept.
  - **The muster:** some musters end with nobody leaving (6 of 21). A 4-hour muster limit left 4, the same 15 parties marching and the same haul, so the 2.1-hour limit is kept. A member eats on the way at about 1 hunger a trip, and the stall still gives the trip's rations at the muster.
  - **Who ventures alone is unchanged:** the rule reads the sector's own damage figure.

- **T4, the economy per day** (2026-11-04): the gate seeds at `EconomyTuning.FullDay` on the 12-bunk floor, three days each, against today's economy over two 9,000-tick days.

| Per day | Today | T4.1: the caravan every 6 h | T4.2: and an 80g purse |
| :--- | ---: | ---: | ---: |
| Tasks a resident | 194 | 57 | 56 |
| Idle | 29% | 42% | 38% |
| Meals a resident | 31.6 | 2.6 | 2.5 |
| Trades a resident | 15.0 | 3.6 | 3.8 |
| The stall sells / buys | 121 / 73 | 30 / 23 | 30 / 24 |
| Caravan's payment for exports | 1,996g | 600g | 645g |
| Upkeep paid / due | 1,200g / 1,200g | 320g / 320g | 320g / 320g |
| Treasury at the end of days 1, 2, 3 | 890g, 1,429g | 106g, 276g, 481g | 64g, 294g, 515g |
| Mean trust toward the player at the end of day 1 | 38.8 | 2.0 | 3.8 |
| Residents whose trust rose, at the end | 10.7 of 12 | 4.4 of 12 | 6.0 of 12 |

- **T4.2's findings:**
  - **The Garden isn't the day's food.** On the new day crawlers buy about 24 rations a seed-day at the stall and harvest about 2.4 from the Garden. A plot regrowing in 1 or 12 game hours made no difference, so the Garden keeps today's numbers as the fallback (D26 was answered by measuring).
  - **An 80g purse adds a little work:** idle falls from 42% to 38%, and harvesting stays about an hour of the day. Crawlers earn what they spend, and on the new day a resident spends about 40g a day: 2.5 meals at 15g. D25's 6–8 hours of work need either more to spend on (W1.4's pickaxe, W2's draughts) or a dearer day, with more salvage the caravan takes. That decision is the developer's.
  - **Bug found and fixed:** on the 12-bunk floor of T2.3, the top row of bunks lay in the walkable strip along the bunkhouse's north wall. That strip is the way from the bunks to the hub door. Paths don't see pawns, and a sleeper lies there for hours, so a crawler re-planned the same blocked path from 21:00 until it starved. The bunks are now two rows of six, 1.5 apart (y −8.5 and −10.5), with the strip and an aisle free. The map validator now fails a full-day bunk set against the wall. Sleep on the new floor: 8.0 hours a sleep, rough sleep 0.8 hours a day.
- **T4.3, trust's pace** (D27): the same runs, with trust and the other affinities fading 5 points every N game hours since a crawler's last dealing.

| Fades 5 every | Mean trust toward the player, end of days 1 / 2 / 3 | Residents whose trust rose, day 3 | Seeds failing the day's trust check |
| :--- | :--- | ---: | ---: |
| 2.4 h (today's 1,000 ticks) | 3.8 / 3.3 / 3.3 | 6.0 of 12 | 18 of 23 |
| 6 h | 8.6 / 14.3 / 16.3 | 9.9 of 12 | 1 |
| **12 h (taken)** | 12.9 / 24.5 / 32.2 | 10.8 of 12 | 0 |
| 24 h | 15.6 / 28.7 / 37.9 | 11.1 of 12 | 0 |

  - **For comparison:** today's pace reaches a mean of 38.8 in one 15-minute day.
  - **Trust builds over days now:** the guild recruits at trust 60 (warmth 40), which takes several game days at fair prices (Part 1's F7, now on the day's scale).
- **T4.2b, salvage at the day's price** (D29, the developer's option (c), 2026-11-05): the same runs with an 80g purse, 12-hour trust fade and cheaper salvage. A sale's price is the stall's; the caravan's price and its lot are per visit. Glowstone stays at 45g (70g, 2 a visit) throughout.

| Salvage: the stall pays (the caravan pays; its lot) | Idle | Harvest trips + selling, h a day | Stall full of ore at a visit | The caravan pays a day | Treasury, end of day 3 | Mean trust, day 3 | Mean purse, day 3 |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Today's: ore 20g, herbs 25g (24g, 30g; 2 each) | 38% | 1.4 + 0.4 | 76% | 645g | 516g | 32.2 | 52g |
| A third: ore 7g, herbs 8g (8g, 10g; 6 each) | 23% | 2.9 + 0.8 | 94% | 711g | 755g | 50.3 | 46g |
| A third; 10 each | 16% | 3.8 + 1.0 | 79% | 961g | 1,093g | 65.1 | 67g |
| A third; 14 ore, 10 herbs | 18% | 4.2 + 1.0 | 53% | 1,044g | 1,213g | 68.7 | 72g |
| A quarter: ore 5g, herbs 6g (6g, 7g; 12 each) | 13% | 4.2 + 1.1 | 73% | 836g | 976g | 68.5 | 53g |
| **A quarter; 16 ore, 12 herbs (taken)** | 13% | 4.6 + 1.1 | 53% | 891g | 1,075g | 72.4 | 55g |
| A fifth: ore 4g, herbs 5g (5g, 6g; 14 each) | 12% | 4.5 + 1.1 | 64% | 798g | 954g | 62.7 | 48g |

  - **What sets the hours:** residents earn what they spend, about 35g a day (2.3 meals at 15g), so a cheaper unit means more units sold. Two things cap it: the stall, which stops buying a salvage type at 20 units until the caravan takes its lot (ore, harvested more, filled it), and the 80g purse.
  - **Taken: a quarter, not the third proposed.** At a third, the stall was full of ore at most visits and residents idled 16%. A fifth gained nothing more, because ore still filled the stall. Raising ore's lot to 16 gave the most work.
  - **D25 is near, not met:** about 5.7 hours of earning a day, against 6–8. Made goods to buy (W1.4, W2) bring the rest.
  - **On every run:** upkeep was paid in full and nobody starved. One run (a third, lots of 6) had one jitter anomaly (F5); the taken one had none.
  - **Trust follows the trades:** about 6 sales a resident-day instead of 1.7. Four residents reach the guild's standing (trust 60, warmth 40) at a median of day 1.8 (1.6–2.3), on every seed; before, the median was day 4.5, and 14 of 23 seeds never got there within 5 days. An artisan qualifies at a median of day 0.8.
  - **Drifters leave richer** (156g a day leaves against 110g brought): they sell salvage too.
- **T4.4, the soak over three days** (D28): `EconomySoak.Pace.Day` plays each profile for three game days (30,240 ticks) on `EconomyTuning.FullDay` and its floor, sampled at each caravan visit; starving is hunger 95 for 2 game hours. Greedy, generous and keen price as shares of the fair prices, which on today's economy are exactly their old prices (today's soak is identical). Seeds 1–15 and 20261009:

| Profile | Treasury at the end of day 3 | Trust rose for (of 12) | Also | Today's band | **Proposed band on the day** |
| :--- | ---: | ---: | :--- | :--- | :--- |
| Fair | 771–1,277g | 10–12 | nobody starved | 1,000–2,000g; trust up for 8 or more; nobody starving | **600–1,600g**; trust up for 8 or more; nobody starving |
| Greedy (food and gear ×2, salvage ×½) | 512–729g | 0–3 | trust mean −29 to −71 | trust up for 2 at most; the mean below 0 at its lowest | **trust up for 3 at most**; the mean below 0 at its lowest |
| Generous (food ×⅓, gear ×⅔, salvage ×1.75 and ×1.6) | broke by day 1–2 on every seed | 12 at its peak | | goes broke; trust up for 8 or more at its peak | the same |
| Guild (fair prices, four recruited and sent out) | 578–998g | 10–12 | full on day 1.6–2.9 of every seed; every wage paid; 1–2 hauls home whenever full 8 game hours before the end | 1,000–2,000g; trust up for 8 or more; nobody starving; no wage unpaid; a haul home when full early | **400–1,200g**; the rest the same, "early" being 8 game hours |
| Keen (food ×4⁄3, gear ×7⁄6, salvage ×0.9 and ×0.88) | 632–1,478g | 4–10 | | none | none |

  - **The treasuries are lower than today's 30 minutes:** a day pays 320g of upkeep, and salvage earns the stall about 1g a unit instead of 4–5g. The guild's wages, fees and outfitting take about 260g more over three days than the fair player's.
  - **Drifters work on the day:** they sell salvage, so they take away more than they bring (about 460g against 330g over three days at keen prices), and G1's check that they leave gold behind turns over. But the keen player's stall ends richer with them than closed on all 16 seeds (17,254g against 11,985g), a comparison G1's day left in the noise (Epic 10 F4). The day's test asserts that (`OnTheDayDriftersLeaveAKeenPlayersStallRicher`).

## 3. The re-pacing table
**Kind:** P is physical time (kept in ticks and seconds); D is the day's time.

**Proposed** is T0's starting point. The step that owns a row measures it and may move it; the table records where it lands. A ✓ marks a row in `DayTuning.FullDay` (or its step's home), which the cut-over (T5) makes the default.

### 3.1 The clock
| What | Where | Today | Kind | Proposed | Step |
| :--- | :--- | :--- | :---: | :--- | :--- |
| Ticks a second | `SimulationClock` | 10 | P | 10 (kept) | |
| A day | `OutpostClock` (presentation only) | 9,000 ticks (900 s); an hour 375 | D | 10,080 ticks in the core's `DayClock`; an hour 420, a minute 7 | T1 |

### 3.2 Needs and health
| What | Where | Today | Kind | Proposed | Step |
| :--- | :--- | :--- | :---: | :--- | :--- |
| Hunger | `DayTuning` | +1.5 a second: fed to starving in 67 s | D | +100 over 24 h (4.17 an hour): 2.4 meals a day measured | T2.1 ✓ |
| Starting and arrival hunger | `OutpostSession.ArrivalHunger`; `DrifterTuning` | 20; drifters 40–70 | D | kept: they come off the road wanting a meal | T2.1 |
| The expedition's muster | `ExpeditionTuning.MusterHunger`, `Rations`, `MusterTicks` | below 20, or carrying 2 rations; 900 ticks | D | kept: the stall still gives the trip's rations at the muster, and a 4-hour muster gained nothing | T3.2b ✓ |
| Energy, standing | `DayTuning` | −0.2 a second | D | −2.75 an hour: 100 to the tired line (40) in about 16 hours of standing (re-measured at T2.4) | T2.2 ✓ |
| Energy, walking | `DayTuning` | −1.0 a second | D | −8.25 an hour, three times standing (today five) | T2.2 ✓ |
| Energy, working (new) | `DayTuning.EnergyWorkingPerSecond` (the harvesting pose; a station's work at W1.3) | as standing | D | the walking rate, 8.25 an hour: work tires as walking does (W1.3's D15) | T2.4 ✓ |
| Sleep in a bunk | `DayTuning` (was `SleepAtBunkAction.SleepSeconds`, `Considerations.BunkRecoveryPerSecond`, `BunkSleepBonus`) | 3 s at +30 a second, then +40 | D | +8 an hour, no bonus, until rested (100) or starving (85), for at most 12 hours: 8.3 hours measured | T2.2 ✓ |
| Resting in place, and a collapse | `DayTuning` (was `RestAction`, the consequence service) | +15 a second; a rest is 2 s, then +30 | D | sleeping rough: +4 an hour, no bonus, up at the same lines as a bunk, for at most 12 hours; a collapse is up at 30 after 7.5 hours | T2.2 ✓ |
| The lines | `Considerations.TiredEnergy`, `TooTiredToWalkEnergy`; `MentalBreakEvaluator.ExhaustionThreshold` | tired 40; too tired to walk 15; collapse 5 | D | kept: sleep begins at energy 37 on average | T2.2 ✓ |
| Social | `DayTuning` | −0.5 a second (empty in 200 s) | D | −100 over 16 h: company once or twice a day | T2.1 ✓ |
| Bunks | `Floor1Layout` | 8, two rows | — | 12, two rows of six 1.5 apart, leaving the strip along the north wall free (`FullDayBunkCount`, `FullDayDefinition`; three rows walled a crawler in, T4.2); drifters sleep rough when they are full | T2.3, T4.2 ✓ |
| A spell at the hearth | the consequence service | +20 social, +10 energy | D | +20 social, no energy: at +10 a visit the hearth outran the waking day's drain, and nobody would sleep | T2.2 ✓ |
| Frustration | `AgentState.Tick` | −1 a second | P | kept: it answers blocked moves, which are physical | |
| Healing, awake | `DayTuning` (was `Vitality.RestingHealPerSecond`) | 0.05 a second (about 50 a day already) | D | 0.05 a second, about 50 a day (kept) | T3.2a ✓ |
| Healing in a bunk | `DayTuning` (was `Vitality.BunkHealPerSecond`) | 4 a second (12 per 3-second nap) | D | +10 an hour: a night's sleep heals 80 | T3.2a ✓ |
| Hazard damage | `Floor1Layout`'s hazards, times `DayTuning.HazardDamageScale` | Sump 3, Glow Caves 4, Drowned Gallery 6 a second | D | a quarter of it (32, 42 and 63 an hour): a glowstone costs a lone crawler 28 health (today 36) and a party 22 (today 26); parties turn back 14% of the time (today 9%) | T3.2a, T3.2b ✓ |
| The lone crawler's hazard limit | `Vitality.LoneHazardLimit` | 4 a second | D | kept: judged on the sector's own figure, so who ventures alone doesn't change | T3.2a ✓ |
| Hazard gear's life | `ItemCatalog` (protection, `PerHazardTick`), one unit every `DayTuning.HazardWearEveryTicks` | 500 ticks of exposure (50 s) | D | a unit every 3 ticks: 1,500 ticks of exposure (about 3.6 game hours), so gear lasts about as many ventures as today's | T3.2a ✓ |

### 3.3 Actions (D22)
| What | Where | Today | Kind | Proposed | Step |
| :--- | :--- | :--- | :---: | :--- | :--- |
| Harvest, a unit of ore or glowstone | `ActionTuning` (an intent's `GatherDuration` overrides it) | 1.5 s | D | 30 min (210 ticks) | T2.4 ✓ |
| Harvest, a unit of herbs | the same | 1.5 s | D | 20 min (140) | T2.4 ✓ |
| Harvest, a ration from a Garden plot | the same | 1.5 s | D | 15 min (105) | T2.4 ✓ |
| Eat a ration from the pack | `ActionTuning` (was `EatCarriedFoodIntent.DefaultDuration`) | 1 s | D | 15 min (105) | T2.4 ✓ |
| Wait at a Garden plot (starving) | `ActionTuning` | 2 s | D | 15 min (105) | T2.4 ✓ |
| Trade at the stall | `ActionTuning`, the handler's work before the trade | within the tick | D | 10 min (70); prices and stock judged at the end | T2.4 ✓ |
| Claim or deliver at the board | `ActionTuning` | within the tick | D | 10 min (70); another may claim first | T2.4 ✓ |
| Share food | `ActionTuning` (an intent's `TransferDuration` overrides it) | 1 s | D | 10 min (70) | T2.4 ✓ |
| A scout's survey | `ActionTuning` | on arrival | D | 30 min (210) at the target | T2.4 ✓ |
| Treat wounds with a herb | `ActionTuning` | within the tick | D | 30 min (210); the herb eaten at the end | T2.4 ✓ |
| A crawler's purse target | `EconomyTuning.ComfortablePurse` (was `EarnIncomeGoal.ComfortableGold`) | 40g | — | 80g, about two days' meals (D25) | T4.2 ✓ |
| The pickaxe at the forge | `RecipeBook` (D2) | 600 ticks | D | 2 h (840); live now, since nothing crafts in a live run before W1.5a | T3.1 ✓ |
| A spell at the hearth | `ActionTuning` (was `RelaxAtAnchorAction`) | 3 s | D | 30 min (210) | T2.4 ✓ |
| Idle pauses | `AmbientWanderEvaluator`, `AgentCognitionService` | 1.5–2 s | P | kept: ambient motion | |
| Panic's sprint; the downed crawler's wait | `PanicIntentHandler.MaxSeconds`; `MentalBreakEvaluator.DownedSleepSeconds` | 6 s; 5 s | P | kept | |
| Poses: tantrum, gossip, yield, share, rally, defiance | `AgentState.Trigger…Visual` | 1.2–2.5 s | P | kept | |

### 3.4 Schedules
| What | Where | Today | Kind | Proposed | Step |
| :--- | :--- | :--- | :---: | :--- | :--- |
| The caravan's visit | `SupplyTuning.RestockIntervalTicks` | 600 ticks (60 s, 15 a day) | D | every 6 game hours (2,520 ticks: 12:00, 18:00, 00:00, 06:00), on the same terms a visit: a day's upkeep 320g, wages 4 rounds (D24) | T4.1 ✓ |
| Salvage's prices and the caravan's lots | `EconomyTuning` (item terms), `SupplyTuning.FullDay` | ore worth 8g, the stall pays 20g, the caravan 24g for 2 a visit; herbs 12g, 25g, 30g for 2 | — | ore 2g, 5g, 6g for 16; herbs 3g, 6g, 7g for 12: a quarter of today's, so a day's meals take about 5 hours of work; glowstone kept (D29) | T4.2b ✓ |
| Garden regrowth | `Floor1Layout.FoodRegrowTicks` | 300 | D | kept: crawlers buy nine meals in ten at the stall, and 1 or 12 game hours made no difference; the Garden is the quick fallback | T4.2 ✓ |
| Herb, ore and glowstone regrowth | `Floor1Layout` | 600, 900, 6,000 | D | kept: the stall's buying (its cap and the caravan's quota), not the nodes, limits salvage | T4.2 ✓ |
| Drifters: first, every, jitter, stay | `DrifterTuning` | 1,200; 2,100 ± 300; 1,800–3,000 | D | kept: already the first at 2.9 h, about every 5 h ± 0.7, staying 4.3–7.1 h, long enough for a meal at two or three a day; T4 looks at their purses | T3.1 ✓ |
| The guild's and the stations' cooldowns | `GuildTuning.RejoinCooldownTicks`, `WorkshopTuning.RehireCooldownTicks` | 3,000 (about 7 game hours) | D | kept: 7.1 game hours | T3.1 ✓ |
| A seeded bounty's life | `OutpostSession.SeedBountyDurationTicks` | 6,000 | D | kept: 14.3 game hours | T3.1 ✓ |
| A posted bounty's life | `BountyBoardModel` | 500–3,000 ticks, 1,000 offered, shown in ticks | D | 4–24 h, 12 offered, shown in hours: the board's model and its words | T6 |
| The expedition's stages (limits) | `ExpeditionTuning` | muster 900, march 1,800, work 1,800, return 2,400 | D | kept: a trip is about 2 game hours, its longest work stage 457 ticks | T3.2b ✓ |
| Memory's decay | `AgentMemory` | resources 1,500, pawns 400, anchors 5,000, other 1,000 | D | kept: the quiet day comes from purses, not forgetting | T4.2 ✓ |
| Trust's fade | `AffinityRecord.RegressTowardNeutral`, the period from `DayTuning.TrustFadeTicks` through `AgentMemory` | 5 every 1,000 ticks (2.4 game hours) | D | 5 every 12 game hours (D27; 6 failed the day's trust check on one seed) | T4.3 ✓ |
| Exploration's choice, gossip's cooldown | `ResourceTargeting.SectorChoiceTicks`; `SocialDynamics` | 200; 100 | P | kept | |
| Retries | `SleepAtBunkIntentHandler`, `SessionHost.Drifters` | 30, 100, 50 | P | kept | |

### 3.5 The checks
| What | Where | Today | Kind | Proposed | Step |
| :--- | :--- | :--- | :---: | :--- | :--- |
| The outpost's day | `OutpostScenarioTests`, `GuildScenarioTests`, `CrawlerIdentityTests` | 9,000 ticks | D | 10,080; food and sleep checks over three days | T5 |
| Starving | `OutpostWatch`, `EconomySoak` | hunger 95 or more for 300 ticks | D | 95 or more for 2 game hours | T5 |
| The soak's horizon and sample | `EconomySoak` (`Pace.Today`, `Pace.Day`) | 30 min (18,000), sampled each visit | D | three game days (30,240), sampled each visit (13 samples), starving at 95 for 2 game hours; bands proposed (D28); the day's soak becomes the soak at the cut-over | T4.4 ✓, T5 |
| The anomaly detector | `AnomalyDetector` | stacking 1 s, thrashing 0.5 s, jitter 5 ticks | P | kept: stretched work's long stands raised no stacking (one jitter in 69 seed-days) | T2.4 ✓ |

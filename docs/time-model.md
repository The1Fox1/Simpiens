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
| The expedition's muster | `ExpeditionTuning.MusterHunger`, `Rations` | below 20, or carrying 2 rations | D | re-set to the new trip's cost, which stretched work sets (a 30 s trip costs about 3 hunger; the work's harvests, hours) | T3.2b |
| Energy, standing | `DayTuning` | −0.2 a second | D | −2.75 an hour: 100 to the tired line (40) in about 16 hours of standing (re-measured at T2.4) | T2.2 ✓ |
| Energy, walking | `DayTuning` | −1.0 a second | D | −8.25 an hour, three times standing (today five) | T2.2 ✓ |
| Energy, working (new) | the actions of D22 | as standing | D | the walking rate: work tires as walking does (W1.3's D15) | T2.4 |
| Sleep in a bunk | `DayTuning` (was `SleepAtBunkAction.SleepSeconds`, `Considerations.BunkRecoveryPerSecond`, `BunkSleepBonus`) | 3 s at +30 a second, then +40 | D | +8 an hour, no bonus, until rested (100) or starving (85), for at most 12 hours: 8.3 hours measured | T2.2 ✓ |
| Resting in place, and a collapse | `DayTuning` (was `RestAction`, the consequence service) | +15 a second; a rest is 2 s, then +30 | D | sleeping rough: +4 an hour, no bonus, up at the same lines as a bunk, for at most 12 hours; a collapse is up at 30 after 7.5 hours | T2.2 ✓ |
| The lines | `Considerations.TiredEnergy`, `TooTiredToWalkEnergy`; `MentalBreakEvaluator.ExhaustionThreshold` | tired 40; too tired to walk 15; collapse 5 | D | kept: sleep begins at energy 37 on average | T2.2 ✓ |
| Social | `DayTuning` | −0.5 a second (empty in 200 s) | D | −100 over 16 h: company once or twice a day | T2.1 ✓ |
| Bunks | `Floor1Layout` | 8, two rows | — | 12, three rows filling the bunkhouse's walkable depth (`FullDayBunkCount`, `FullDayDefinition`); drifters sleep rough when they are full | T2.3 ✓ |
| A spell at the hearth | the consequence service | +20 social, +10 energy | D | +20 social, no energy: at +10 a visit the hearth outran the waking day's drain, and nobody would sleep | T2.2 ✓ |
| Frustration | `AgentState.Tick` | −1 a second | P | kept: it answers blocked moves, which are physical | |
| Healing, awake | `Vitality.RestingHealPerSecond` | 0.05 a second (about 50 a day already) | D | about 50 a day (kept in day terms) | T3.2a |
| Healing in a bunk | `Vitality.BunkHealPerSecond` | 4 a second (12 per 3-second nap) | D | about +10 an hour: a night's sleep heals 80 | T3.2a |
| Hazard damage | `Floor1Layout`'s hazards | Sump 3, Glow Caves 4, Drowned Gallery 6 a second | D | per game hour, set so a unit of glowstone costs about the health it does today | T3.2a |
| The lone crawler's hazard limit | `Vitality.LoneHazardLimit` | 4 a second | D | in the same units as damage, re-measured | T3.2b |
| Hazard gear's life | `ItemCatalog` (protection, `PerHazardTick`) | 500 ticks of exposure (50 s) | D | hours of exposure, set so gear lasts about as many trips as today | T3.2a |

### 3.3 Actions (D22)
| What | Where | Today | Kind | Proposed | Step |
| :--- | :--- | :--- | :---: | :--- | :--- |
| Harvest, a unit of ore or glowstone | `HarvestResourceIntent.GatherDuration` | 1.5 s | D | 30 min (210 ticks) | T2.4 |
| Harvest, a unit of herbs | the same | 1.5 s | D | 20 min (140) | T2.4 |
| Harvest, a ration from a Garden plot | the same | 1.5 s | D | 15 min (105) | T2.4 |
| Eat a ration from the pack | `EatCarriedFoodIntent.DefaultDuration` | 1 s | D | 15 min (105) | T2.4 |
| Wait at a Garden plot (starving) | `MentalBreakEvaluator.WaitAtPlotSeconds` | 2 s | D | 15 min (105) | T2.4 |
| Trade at the stall | `TradeIntentHandler` | within the tick | D | 10 min (70) | T2.4 |
| Claim or deliver at the board | `ClaimBountyIntentHandler`, `DeliverBountyIntentHandler` | within the tick | D | 10 min (70) | T2.4 |
| Share food | `ShareResourceIntent` | 1 s | D | 10 min (70) | T2.4 |
| A scout's survey | `ScoutIntentHandler` | on arrival | D | 30 min (210) at the target | T2.4 |
| Treat wounds with a herb | `TreatWoundsIntentHandler` | within the tick | D | 30 min (210) | T2.4 |
| The pickaxe at the forge | `RecipeBook` (D2) | 600 ticks | D | 2 h (840) | T3.1 |
| Idle pauses | `AmbientWanderEvaluator`, `AgentCognitionService` | 1.5–2 s | P | kept: ambient motion | |
| Panic's sprint; the downed crawler's wait | `PanicIntentHandler.MaxSeconds`; `MentalBreakEvaluator.DownedSleepSeconds` | 6 s; 5 s | P | kept | |
| Poses: tantrum, gossip, yield, share, rally, defiance | `AgentState.Trigger…Visual` | 1.2–2.5 s | P | kept | |

### 3.4 Schedules
| What | Where | Today | Kind | Proposed | Step |
| :--- | :--- | :--- | :---: | :--- | :--- |
| The caravan's visit | `SupplyTuning.RestockIntervalTicks` | 600 ticks (60 s, 15 a day) | D | set with the economy per day: a few visits a day, or one | T4 |
| Garden regrowth | `Floor1Layout.FoodRegrowTicks` | 300 | D | with the food balance per day (stall and Garden) | T4 |
| Herb, ore and glowstone regrowth | `Floor1Layout` | 600, 900, 6,000 | D | with the supply per day | T4 |
| Drifters: first, every, jitter, stay | `DrifterTuning` | 1,200; 2,100 ± 300; 1,800–3,000 | D | first at 3 h, about every 6 h ± 1 h, staying 6–12 h (a meal and perhaps a night) | T3.1 |
| The guild's and the stations' cooldowns | `GuildTuning.RejoinCooldownTicks`, `WorkshopTuning.RehireCooldownTicks` | 3,000 (about 7 game hours) | D | 7 h (2,940) | T3.1 |
| A seeded bounty's life | `OutpostSession.SeedBountyDurationTicks` | 6,000 | D | 14 h (5,880) | T3.1 |
| A posted bounty's life | `BountyBoardModel` | 500–3,000 ticks, 1,000 offered, shown in ticks | D | 4–24 h, 12 offered, shown in hours | T3.1, T6 |
| The expedition's stages (limits) | `ExpeditionTuning` | muster 900, march 1,800, work 1,800, return 2,400 | D | in hours, with room for stretched harvests in the work | T3.2b |
| Memory's decay | `AgentMemory` | resources 1,500, pawns 400, anchors 5,000, other 1,000 | D | kept unless crawlers forget what they use under stretched work (measured) | T2.4 |
| Exploration's choice, gossip's cooldown | `ResourceTargeting.SectorChoiceTicks`; `SocialDynamics` | 200; 100 | P | kept | |
| Retries | `SleepAtBunkIntentHandler`, `SessionHost.Drifters` | 30, 100, 50 | P | kept | |

### 3.5 The checks
| What | Where | Today | Kind | Proposed | Step |
| :--- | :--- | :--- | :---: | :--- | :--- |
| The outpost's day | `OutpostScenarioTests`, `GuildScenarioTests`, `CrawlerIdentityTests` | 9,000 ticks | D | 10,080; food and sleep checks over three days | T5 |
| Starving | `OutpostWatch`, `EconomySoak` | hunger 95 or more for 300 ticks | D | 95 or more for 2 game hours | T5 |
| The soak's horizon and sample | `EconomySoak` | 30 min (18,000), sampled each visit | D | in days, sampled each visit | T4, T5 |
| The anomaly detector | `AnomalyDetector` | stacking 1 s, thrashing 0.5 s, jitter 5 ticks | P | kept; stretched work's long stands at nodes are checked against it | T2.4 |

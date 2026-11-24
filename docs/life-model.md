# The Life Model: Motives, Thoughts and the Day

The rules and measurements of the life core (Milestone 6: [Epic LIFE-1](epics/epic-life-1.md) to LIFE-3), kept up to date as each milestone lands, as [`skills-model.md`](skills-model.md) is for skills and [`time-model.md`](time-model.md) for the day. The design source is the [Living Crawlers proposal](https://claude.ai/artifact/GqJoB2w3A7SM1atjktCUhq) (2026-11-15).

## 1. What stands before the life core (checked 2026-11-23, Epic LIFE-1, L0.1)

### 1.1 How a crawler chooses
`AgentCognitionService.EvaluateAsync`, off the main thread, its decision applied at the next tick:
1. **Collapse:** an exhausted crawler idles.
2. **Tier 1, the reflexes:** `SurvivalUtilityEvaluator` (the hunger reflex), `MentalBreakEvaluator` (starvation at 85, exhaustion at 5, danger) and the rest of the evaluators, in order. A reflex cuts the active plan short.
3. **Tier 2, GOAP:**
   - an active plan is stepped while its step is valid, and goals are not re-scored while it runs;
   - with none, the best valid, unsatisfied goal is planned once (the top three are kept in `DecisionSummary`).
4. **Tier 3:** if no plan was found, `AmbientWanderEvaluator`, then `IdleIntent`.

### 1.2 The goals and their numbers
21 goals in `OutpostGoapDomain.BuildGoals`, in this order. The order breaks ties, because the first goal with the highest priority wins. "× greed" and the like are `Considerations.TraitFactor(trait, strength)`, exactly 1 for a neutral trait.

| # | Goal | Priority today | Valid when (in short) |
| ---: | :--- | :--- | :--- |
| 1 | `SatiateHunger` | hunger | hunger over 20 |
| 2 | `Rest` | 100 − energy | energy under 40, and fed enough for bed unless too tired to walk (15) |
| 3 | `Socialize` | (100 − social, +10 with a warm or trusted friend) × sociability (±40%) | social under 50 |
| 4 | `AidCompanion` | the altruism utility | a sated helper and a companion in distress in view |
| 5 | `GatherAtAnchor` | (max(0, 75 − social) + 15, +5 with a friend) × sociability; 0 in a crisis | no crisis (hunger 60, energy 20), a hearth remembered |
| 6 | `ComplyDirective` | 120 on an expedition or a shift; otherwise (50 + compliance) × loyalty (±40%); low under starvation | an active directive it would comply with (compliance at least 20) |
| 7 | `PurchaseProvisions` | hunger × 1.1 | hungry (50), no food held, a ration it can afford at its price |
| 8 | `OutfitForDanger` | 40 | it would venture, fed and rested, and protection it would buy |
| 9 | `EquipForWork` | 35 | a resident without work gear, gear for sale, a mineral known |
| 10 | `ArmForFight` | 33 | a bold resident, fed and rested, arms it would buy |
| 11 | `CollectLoot` | 42; 125 under a clear's orders | a pile it would pick over |
| 12 | `Rescue` | 90; 130 under orders | a fallen friend it would go to |
| 13 | `Hunt` | (35 + the reward's share × greed) × diligence − hunger × 0.3, at least 5 | a hunter with a hunt it braves |
| 14 | `CarryMedicine` | 38 | it would venture, holds none, can buy some, knows hazardous salvage |
| 15 | `Study` | 30 | a manual it would buy (passions only) |
| 16 | `ReadManual` | 27 | holds a manual, a hearth remembered |
| 17 | `Drill` | 28 | a member free to drill at a built yard |
| 18 | `LiquidateSalvage` | (30 + 15 a sellable held) × greed (±30%) | sellable salvage the stall can pay for |
| 19 | `FulfillBounty` | (35 + the reward's share × greed) × diligence (±30%) − hunger × 0.3, at least 5 | a claimed or reachable bounty, hunger under 75 |
| 20 | `EarnIncome` | max(5, 25 + the purse's need − hunger × 0.2) × greed × diligence; the need is 20 at an empty purse, 0 at the comfortable purse (80g) | under the comfortable purse, fed and rested, a stall that buys salvage, no claimed bounty |
| 21 | `LeaveOutpost` | 95 | a drifter whose stay is over |

`GoapCognitiveEvaluator` repeats the same choice; only `PlanExecutionValidator` uses it.

### 1.3 Needs, frustration and personality
- **Needs** (`AgentNeeds`, 12 bytes in the crawler's record): hunger rises and energy and social fall on the day's pace (`DayTuning`). Gossip refills social.
- **Frustration:**
  - **What raises it:** +25 when a rival takes a contested node (with a tantrum, the only crawler tantrum besides an artisan's breakdown), and +25 for each friend who dies (`SimulationManager.GriefFrustration`, beside a `Grieved` memory event).
  - **How it falls:** 1 every real second, about 42 a game hour.
  - **What reads it:** compliance, through `ComplianceUtility` (−0.2 a point); the `IsThreatened` fact over 60 (read by no action); and the face (frustrated at 60).
- **Personality:** five traits (greed, risk, sociability, loyalty, diligence), each in [−1, 1] on a triangular draw. Passions (Epic 11) weigh a salvage node's score, not a goal's.

### 1.4 Events a thought can ride on (sites, checked)
- **Deeds,** through one entry (`AgentState.AddDeed`): a creature slain, a boss slain, a rescue, a make, a downing survived, a hunt paid, a clear won, a crawler taught, a named creature slain, a masterpiece made.
- **Practice:** a band reached (practice's entry), a technique chosen, an epithet earned (`AddDeed`'s judgement).
- **Money:** wages paid or unpaid (`GuildService`, `WorkshopService`), a bounty delivered.
- **The body:** meals (`ItemUse.Consume`), a bunk slept in (`SleepAtBunkIntentHandler`), an exhaustion collapse (`AgentState`, `AgentRuntime`).
- **Others:** food shared (`ShareResourceIntentHandler`), gossip (`SocialDynamics`), a contested node lost, a friend's death (`SimulationManager.Lodging`).

### 1.5 The chronicle
61 event types (0–60), one ring of 1,024 events outpost-wide and one of 100 per sector, each event with two actors, a sector, a tick, an intensity and one number of extra data.

## 2. The motive model (L0)

### 2.1 Motives and families (L0.2, D1)
Every goal declares what it is for (`Motive`) and the part of the day it belongs to (`ActivityFamily`), on `GoapGoal`'s constructor. `MotiveValidator` pins this table.

| Goal | Motive | Family |
| :--- | :--- | :--- |
| `SatiateHunger`, `PurchaseProvisions` | Hunger | Unweighted |
| `Rest` | Rest | Rest |
| `Socialize`, `GatherAtAnchor` | Company | Social |
| `AidCompanion`, `Rescue` | Aid | Unweighted |
| `ComplyDirective` | Duty | Unweighted |
| `OutfitForDanger`, `EquipForWork`, `ArmForFight`, `CarryMedicine` | Gear | Work |
| `CollectLoot` | Income | Work; Unweighted under a clear's orders (`FamilyFor`) |
| `Hunt`, `LiquidateSalvage`, `FulfillBounty`, `EarnIncome` | Income | Work |
| `Study`, `ReadManual`, `Drill` | Growth | Leisure (free hours, as Epic 11's D10 put the drill) |
| `LeaveOutpost` | Leave | Unweighted |

- **Unweighted** goals are never scaled by the routine or the day's intention (the epic's A8), so hunger, orders, aid and leaving keep their precedence.
- **A new goal** declares both; the validator's table gains its row.

### 2.2 The rule (L0.5, D2)
**Priority = the goal's base × its weight** (+ commitment from L0.7, when it isn't 0).
- **The base** is the goal's own number (`GoapGoal.BasePriority`), §1.2's formulas unchanged.
- **The weight** is the product of the factors every goal shares, each exactly 1 until its milestone sets it:

  | Factor | Keyed by | From |
  | :--- | :--- | :--- |
  | The session's weight | motive | L0.5 (`LifeTuning.MotiveWeight`; 1 for every motive by default) |
  | Mood | motive and mood's band; under orders too, from plan 2's O.1 (D25) | L1.4: low, income 0.9; breaking, income 0.75; company held at 1 until plan 2 (F12). O.1: duty low 0.85, breaking 0.6 |
  | The want | the want's motive | plan 2 |
  | The routine | family and hour | plan 3 |
  | Satiation | work | plan 3 |
  | The day's intention | family | plan 3 |

- **Exact while neutral:** the factors are multiplied together first, then the base by their product; a float times exactly 1 is itself, so golden reproduced byte for byte through L0.4 and L0.5.
- **The one door:** `GoalSelector` reaches a priority only through `MotiveModel`; `MotiveModelTests` fails production code that reads a base elsewhere.
- **Urgency** (`Urgency.Of`) is a motive's shared need: hunger, 100 − energy, 100 − social; 0 for a motive with no need behind it. New goals build their base from it.
- **The "why":** `DecisionSummary` keeps the best goal's base and weight beside the top three.

### 2.3 Commitment (L0.7, D5)
The goal of the last plan adopted (`AgentState.LastGoal`, kept through the plan's end or a reflex, unsaved) gains `LifeTuning.Commitment` at the next choice, if it is still valid and unsatisfied (`MotiveModel.Score`). It is 0: L0.3 measured near ties at 3.6% of choices and 0.8 flips a resident-day, too few to tune against. Plan 2 sets it when leisure options compete.

### 2.4 The fallback (L0.6, D4)
When the best goal can't be planned, the next are planned in order, up to `LifeTuning.FallbackDepth` more (2 in the live game), before Tier 3's wandering (`GoalSelector.PlanFirst`; a decision's source reads "GOAP fallback"). Today it never runs: no plan goes unfound (§3.1), golden didn't move, and the day's measurement re-ran identical. It is there for plan 2's goals, whose venue or friend may be out of reach.

## 3. Measurements

### 3.1 A crawler's day before the life core (L0.3, 2026-11-23)
`ACrawlersDayTests.MeasureACrawlersDay`, explicit: the 23 gate seeds for three game days (69 seed-days, 828 resident-days), residents only, standing, on `feat/life1-mind` after L0.2 (behaviour as `main` at `d0d2d47`). Two runs: no player, and the guild player sending expeditions to the Drowned Gallery. About 15 s in Release.

**Hours a resident-day** (the active plan's goal by family; otherwise what the crawler does):

| | No player | The guild player |
| :--- | ---: | ---: |
| Rest (`Rest`: asleep in a bunk, walks included) | 9.8 h | 9.5 h |
| Work (`EarnIncome` 8.1 h; walks, salvage and sales included) | 8.4 h | 7.5 h |
| Social (`GatherAtAnchor`, at the hearth) | 3.1 h | 3.6 h |
| Between decisions (the decision lands a tick later) | 1.4 h | 1.3 h |
| Unweighted (meals, orders) | 1.0 h | 1.4 h |
| Leisure (`ReadManual`, `Study`) | 0.2 h | 0.3 h |
| Wandering and idling | 0.1 h | 0.2 h |

- **Idle has gone.** The proposal's 13–38% idle came from the time model's T-steps; since then, crawlers below the comfortable purse (80g) earn almost all day, and spend their free hours at the hearth. Work per seed ranges from 6.6 to 9.0 hours (walks included). Leisure will compete with `EarnIncome` itself, not with idling (F11).
- **Choices:** about 600 decisions a resident-day, 594 of them a new plan: plans are short (the harvest's step ends every few game minutes), so goals are re-scored about every 17 ticks (F10).
- **Plans not found: none** in either run. Tier 3 runs about twice a resident-day, each time because no goal was valid (F9).
- **Near ties** (the best goal within 5% of the second): 3.6% of choices, 21.6 a resident-day, nearly all `EarnIncome` against `GatherAtAnchor` or `Socialize`. **Flips** (A, B, A again within a game hour): 0.8 a resident-day. Commitment (D5) stays 0 at L0.7.
- **Together:** every pair of the 12 residents comes within the gossip radius (2.5) every day (65.8 of 66 pairs), for about 300 pair-hours a day, most of them at the hearth.
- **The chronicle:** 592 lines a day outpost-wide (747 with the guild player), from 391 to 1,141 a seed-day, so the busiest seeds overflow the ring of 1,024 within a day (F5). 44 lines a resident-day name a resident. By type: gossip 253, trades 120, companionship 87, claims 60, regrowth 29, tantrums (contested nodes) 22.5 a day.

### 3.2 Moods before mood acts (L1.3, 2026-11-23)
`MoodFollowsTheDayTests.MeasureMoodOnTheGateSeeds`, explicit: the 23 gate seeds; three game days with no player and with the guild's clears (`TheOutpostRemembers`' players: four armed, Steady), and ten with `DeathHasItsCost`'s (two unarmed against the beetles, Bold), which bring deaths. Thoughts thought, mood read each tick; nothing reads mood yet. About 30 s in Release.

| | No player | The clears | Death's players (10 days) |
| :--- | ---: | ---: | ---: |
| Mood, mean | 8.2 | 9.1 | 9.4 |
| Lowest, highest | -21, 35 | -44, 48 | -82, 59 |
| Breaking | 0.0% | 0.0% | 1.4% |
| Low | 0.9% | 1.2% | 3.7% |
| Steady | 53.1% | 48.4% | 35.4% |
| Good | 45.9% | 49.1% | 56.6% |
| Happy | 0.0% | 1.3% | 2.9% |

- **Average is the default:** an ordinary day sits Steady to Good (99%), from comfort (+4), a bunk's sleep and meals (+3 each) and the hearth's talk (+2, stacking to 3.5), less the odd node lost to a rival (-3).
- **Thoughts a resident-day** (no player): a good talk 25.5 (the hearth's gossip refreshes it all evening), a good meal 2.4, beaten to it 1.9, slept in a bunk 1.0, ate at the Garden 0.2; paydays, bands and the rest rarer. With the clears, paid in full 0.7 and hurt 0.5 join them.
- **A friend's death** (7 deaths, 40 griefs in the death run): at -20 (D7's first value), mood fell from 8.5 to -15.3 an hour after, and 28 of 40 griefs reached Low. **Set to -25**: from 8.5 to -21.2, and 37 of 40 reach Low, so grief reads as Low unless a crawler was already very happy. No other value or band moved.

### 3.3 What mood changes (L1.4, 2026-11-23)
- **Frustration on the day's pace (F6):** it eased 1 every real second (about 42 a game hour); now 10 a game hour (`DayTuning.FrustrationEasePerSecond`). Mean frustration 0.6 to 2.4 with no player, at the face's 60 for 0.14% of the time (from none). Golden and moods unchanged.
- **Mood's frustration** (`LifeTuning.MoodFrustrationPerHour`, applied in the crawler's drives): breaking +15 a game hour, low +5, good and happy -5. It weighs against compliance, so an unhappy crawler defies orders sooner (F8); none did on the gate seeds, where moods are seldom low.
- **Mood's weights** (`LifeTuning.MoodWeight`, in the motive model): low, income 0.9; breaking, income 0.75; nothing for a crawler under orders (an expedition or a shift: the epic's A8). **Since plan 2's O.1** (the producer, 2026-11-24: "Crawlers can absolutely disobey orders"), mood weighs crawlers under orders too, and duty gains its weights (§5.2).
  - **Company's first weights (low 1.25, breaking 1.5) were dropped (F12):** a crawler hurt in a fight read low, went to the hearth instead of its bunk, healed slower there and stayed low. Four live DoDs failed (`ADraughtKeepsAPartyWorking`, `EveryScenarioPlaysTogether`, `PassionBurnsBright`, `ACallingEmerges`) though their gate-seed measurements barely moved (a Warden on 1 seed to 3, draughts 15 of 23 to 13, a passion burning bright 11 to 9). With income only, all four pass on their seeds.
- **Readings after L1.4** (`MeasureMoodOnTheGateSeeds`, `MeasureACrawlersDay`): ordinary days unchanged (rest 9.8 h, work 8.4 h, the hearth 3.1 h; moods Steady to Good 99%); with death's players, frustration at 60 for 0.16% of the time, no orders defied, and a friend's death still takes a mourner to Low in 37 of 40 griefs.

### 3.4 Plan 1's gate (C.1, 2026-11-23)
- **The soak:** every profile within its band, 18 of 18; `guild`, `crafting` and `hunting` on seeds 1–20, 20 of 20 each. Leisure isn't yet a thing to spend on, so the economy's numbers stand.
- **Cost** (`PopulationScalingProbe`, Release, twice each, against `main` at `d0d2d47`): 16 crawlers 19.5–20.4k ticks a second (20.1–20.6k on `main`); 24 crawlers 16.4–17.4k (17.8–18.3k). The 12-crawler case ran first and read 8–17k on both, too noisy to compare. The thoughts, mood and the selector cost a few percent at 24.
- **Behaviour** has not moved since L1.4's readings (§3.3): L1.5–L1.8 are the dossier's, the faces' and the DoD's.

## 4. What the player reads (L1.5)
- **Mind** (`MindModel`): the mood and its band ("Low (-15)"), the need terms, then the live thoughts, each by size with its value and the time it has left ("Bram died -25 · 4 days left"); ties by their words. The reason is the line that weighs most in the mood's direction (the most negative when low, the most positive when good), never comfort alone.
- **Today** (`DayLogModel`): since midnight, newest first, the thoughts begun (a thought restacked shows once, at its last time, with its stacks) and the crawler's dramatic and crisis chronicle lines; a thought begun on a line's tick tells it instead. It stores nothing.
- **The chronicle's ring** holds 2,048 events (1,024 before L1.5): the busiest day measured brought 1,141 lines, so a day's dramatic lines are still there at night.


## 5. Fun and the evening (plan 2)

### 5.1 The day by the clock, before fun (P.1, 2026-11-24)
`ACrawlersDayTests.MeasureACrawlersDay` with its clock-hour columns: the 23 gate seeds, three game days, residents only, on `feat/life1-tavern` at its start (behaviour as `main` at `ebc1ef2`). The day's totals are §3.1's to the tenth of an hour (rest 9.8 h, work 8.4 h, the hearth 3.1 h, leisure 0.2 h, wandering 0.1 h; no plan unfound; 0.8 flips a resident-day; 65.8 of 66 pairs meet a day).

**What residents do in each game hour** (no player; a share of residents' time):

| Hour | Work | Social (hearth) | Rest (asleep) | Leisure | Unweighted | Other |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: |
| 06:00 | 65% | 10% | 20% | 0% | 1% | 3% |
| 09:00 | 35% | 15% | 29% | 1% | 7% | 14% |
| 11:00 | 11% | 27% | 38% | 0% | 2% | 21% |
| 13:00 | 32% | 11% | 40% | 1% | 13% | 1% |
| 16:00 | 22% | 10% | 49% | 1% | 4% | 15% |
| 18:00 | 29% | 9% | 56% | 1% | 4% | 1% |
| 20:00 | 33% | 9% | 53% | 0% | 3% | 2% |
| 22:00 | 31% | 10% | 52% | 0% | 2% | 4% |
| 00:00 | 36% | 11% | 45% | 1% | 5% | 3% |
| 02:00 | 43% | 14% | 34% | 1% | 4% | 5% |
| 04:00 | 37% | 14% | 37% | 1% | 7% | 5% |

"Other" is mostly the tick between a decision and its plan, and the reflexes (a meal from the pack, a harvest's last step). With the guild player the shape is the same, a little more asleep in the evening (61% at 18:00) and less at work.

- **The outpost has no night (F16).** Sleep starts when energy falls below the tired line and ends at the wake lines (`DayTuning.Wakes`), never by the clock. Every resident spawns at 06:00 rested, so the outpost sleeps together from mid-afternoon (40% at 13:00) to before midnight (56% at 18:00) and works through the small hours (43% at 02:00). The time model's "06:00 start" lines the day up once; nothing keeps it lined up.
- **The hearth is busiest late morning** (27% at 11:00) and flat otherwise (9–15%).
- **For plan 2:** an evening tavern (18:00–02:00) would open while half the outpost sleeps. Plan 2 stays clock-agnostic (D26): the tavern's hours are its keeper's shifts, set where residents are free; plan 3's routine (the epic's bedtime pull) brings sleep to the night.

### 5.2 Mood reaches crawlers under orders (O.1, 2026-11-24)
`MotiveModel.UnderOrders` is gone: mood weighs every crawler's goals, and duty gains mood's weights (low 0.85, breaking 0.6; D25). Orders still stand at 120 when complied with, so a low member's duty (102) still outranks everything but the reflexes, rescue and hunger from 70; a breaking one's (72) can lose to the hearth or a bed.

`MeasureMoodOnTheGateSeeds`, before and after, on the same build (the measurement gained a count of time under orders spent on a plan other than duty, aid or hunger; it counts gear bought at the muster and loot under a clear's orders as well as refusals, so only its change reads):

| | The clears, before | after | Death's players (10 days), before | after |
| :--- | ---: | ---: | ---: | ---: |
| Under orders, h a resident-day | 0.64 | 0.64 | 0.80 | 0.80 |
| On another plan meanwhile | 25.73% | 25.73% | 12.87% | 12.90% |
| Deaths | 0 | 0 | 9 | 11 |
| Mood mean; breaking, low | 9.1; 0.0%, 1.2% | the same | 9.3; 1.5%, 3.5% | 9.3; 1.4%, 3.5% |

- **Almost nothing moves:** members under orders are seldom low (a low member on a trip is mostly one just hurt, who is already turning back). Golden is unchanged (nothing in its 3,000 ticks reads low), and every live scenario passes on its seed (core 350/350).
- **What it opens:** from L2.6 a bored, unhappy member's Recreate competes with its duty through the same weights.

### 5.3 An expedition's orders judged at every stage (O.2, 2026-11-24)
The guild issues the party's directive afresh at each stage (muster, march, work or fight, return, deliver), with the stage's tick, so each member judges it again by compliance (trust, respect, allegiance to the party and warmth, less hunger and frustration), as an artisan judges each block of its shift. `ComplyDirectiveGoal` judges a member on a trip by compliance at every choice. A member who no longer complies stays behind or deserts, by the path dispatch used; the party goes on without it (`AMemberWhoSoursOnTheWayStaysBehindAtTheNextStage`).

- **On the gate seeds nothing moves:** members who stayed behind or deserted a trip, before and after, the same 2 in the clears' 69 seed-days and 1 in death's players' 230, all of them members not at the muster in time. A member willing at dispatch is still willing at the next stage unless something sours it. Golden is unchanged (no expedition in its 3,000 ticks); core 351/351.
- **What it opens:** a member whose trust falls on a trip (a friend dying under orders costs the patron trust, Epic 10 Part 3's K5) or who grows frustrated or hungry can now walk off it.

### 5.4 Fun (L2.1, 2026-11-24)
- **Held as boredom** in the life record (a float at byte 264, 0 to 100); fun is 100 less it, so a record written before fun reads entertained (D11). The wants keep their 72 bytes at 192, and 244 bytes stay spare from 268.
- **Its pace** (`DayTuning`): 3 a game hour awake (`BoredomPerSecond`), held asleep (a bunk, resting, an exhaustion collapse), eased 6 an hour while relaxing at the hearth (`FiresideFunPerSecond`, D12's fireside).
- **Its urgency:** `Urgency.Of(Motive.Fun)` = 100 − fun. `Motive.Fun` is 10.
- **Nothing reads it yet:** the day's measurement re-ran identical, so golden moved for its coverage only (the world hash mixes every record, now none is empty). The Bored mood term lands with Recreate (L2.6), so a penalty never comes before its remedy.

### 5.5 Activities (L2.2, 2026-11-24)
`ActivityTable` (D12), each with its thought in `ThoughtTable` (ids 22 to 27):

| Activity | Fun / h | Also | Lasts | Cost | Group | Company | Leans | Thought |
| :--- | ---: | :--- | ---: | :--- | :--- | :--- | :--- | :--- |
| Fireside | 6 | (`GatherAtAnchor`'s +20 social) | — | — | Fireside | — | sociable | — |
| Walk | 15 | | 45 min | — | Outdoors | — | solitary | A walk in the Garden +2, 6 h |
| Browse | 10 | | 30 min | — | Market | — | greedy | Browsed the market +1, 6 h |
| Spar | 20 | social 5 / h | 45 min | — | Games | one other | bold | A good bout +3, 8 h |
| Ale | 20 | social 5 / h | 45 min | an ale (T.3) | Drink | — | sociable | An ale at the tavern +3, 8 h |
| Supper | 10 | hunger −40 | 45 min | a stew (T.3) | Meal | — | — | A supper at the tavern +4, 10 h |
| Dice | 30 | social 10 / h | 60 min | — | Games | one to three others | bold | A game of dice +4, 8 h |

The hearth's relax reads the fireside's row (6 an hour), so its rate has one home; golden unchanged.

### 5.6 Venues (L2.3, 2026-11-24)
`VenueDefinition` in the map (D13), with `VenueRules.OpenMask` computed on the main thread each tick and carried by the snapshot (`OpenVenueMask`). The outpost's four, before the tavern:

| Venue | Sector | Offers | Seats | Open |
| :--- | :--- | :--- | ---: | :--- |
| The hearth | the hub | the fireside (`GatherAtAnchor`'s) | — | always |
| The stall | the hub | browsing | 4 | always |
| The Garden | the Garden | a walk | 6 | always (D26) |
| The guildhall yard | the guildhall | sparring, members only | 2 | while the drill yard stands |

None stands in a hazard sector or a lair's reach (`VenueValidator`). Nothing reads them yet: golden unchanged.

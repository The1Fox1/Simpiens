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

### 5.7 The leisure score (L2.4, 2026-11-24)
`Leisure.Score(context, venue, activity)`, and `Leisure.TryChoose` over every venue's recreations (D15), pure and allocation-free:
- **Gates (0):** the venue closed, the guild's yard for a non-member, its sector uncharted, the activity not a recreation it offers, its cost beyond the purse or out of the stall's stock, too few others there (sparring needs one, dice one to three), no seat free.
- **Score:** the share it fills (fun an hour over 30, dice's; a supper adds the hunger it eases, over 100; at most 1) × its lean (a trait of ±1 gives ×1.5 or ×0.5) × the walk (1 / (1 + distance / 30)) × 1.25 with a warm or trusted friend there. Variety and curiosity join at L2.7.
- **At the start** of the outpost a resident's best is a walk in the Garden or browsing the stall, about 0.3–0.5 for a neutral nature in the hub. Recreate's base will be fun's urgency times it.

### 5.8 Recreate (L2.6, 2026-11-24)
`RecreateGoal` (Fun, Leisure; the fact `Recreated`, 49 of 64; after the training goals, before earning): base = fun's urgency × the best leisure score; never while hungry (60), tired (under 40 energy) or hurt (under 60 health), valid under orders (D25). The Bored term (−5, fun 25 or less) lands with it.

**Boredom's pace, set by where it settles** (`MeasureACrawlersDay`, six days, the 23 gate seeds, residents, no player):

| Boredom a game hour awake | Boredom at each day's end (mean) | Recreate | Work | The hearth |
| ---: | :--- | ---: | ---: | ---: |
| 3 (D11's first value) | 30, 48, 48, 47, 47, 45 | 1.5 h | 7.2 h | 3.4 h |
| **2 (set)** | 19, 33, 38, 39, 40, 38 | 0.7 h | 7.6 h | 3.6 h |

At 2 boredom settles near 40 (fun 60), so the Bored term seldom holds and Recreate takes about 0.7 h a resident-day from the outpost's venues alone (the Garden and the stall); the tavern adds its own at T.4–T.6. Over three days, against §3.1's: work 8.4 → 8.0 h, the hearth 3.1 → 2.7 h, leisure 0.2 → 1.0 h; no plan unfound.

**What it costs the fighting** (each scenario's measurement, before and after, the gate seeds): clears 21 and 21 of 23 seeds, hunts paid 21 and 21, a Warden 1 and 1, a passion burning bright 11 and 11, armour from a kill 3 and 5, melee, mining and a smith's grade all rising 8 and 9; fewer: a kill within four days 9 to 5, a masterpiece made 11 to 8, harder foes teaching more 4 to 0, boss downings in the clears' run 18 (15 on one seed) to 0. On 60 seeds a kill within four days comes on 20 against 21, within eight on 32 against 36, and 48 kills against 56: about 14% less fighting, the time leisure takes. The 23-seed drop was mostly chance.

**Seeds re-picked:** the live scenarios whose moment no longer came on their seed were re-picked (each scenario's summary names its new seed and the reason), with `ScenarioSeed.Or` and `SIMPIENS_SCENARIO_SEED` for the search. Day failures across the measurements stay where they were (25 before, 27 after: starvations on 3 seed-runs before and 6 after, each a member downed or carried and recovering while its hunger ran out, Epic 10's open finding).

### 5.9 Mood's pull on leisure, commitment, the tavern's trade (L2.8, 2026-11-24)
- **Fun's mood weights** (D21): low 1.25, breaking 1.5, under orders too (D25). Safe since Recreate and FulfilWant are never chosen hungry, tired or hurt (F12).
- **The leisure score's full share** is 20 fun an hour (30 before): free time won too seldom against work to fill the tavern (F18).
- **Readings** (`MeasureACrawlersDay`, three days; the soak's `tavern` profile, seeds 1–20): Recreate 0.5 h a resident-day with no player, 0.7 with the guild's; boredom settles near 33 (fun fills faster); work 8.3 h; the tavern serves about 5.5 a seed in three days (from 2), 0–9, every stall solvent.
- **Commitment** (D22, F3): flips stay at 0.8 a resident-day, under D22's 2, so commitment stays 0; F3 closes on the measurement.
- **What stays open:** the tavern opens on the day shift while half the outpost sleeps through the afternoon (F16), and work still outbids free time most hours (F11). Plan 3's routine (sleep at night) and satiation (work's pull falling with a full purse) are the levers for its evenings.

### 5.10 The Definition of Done (D.1, D.2, 2026-11-24)
`AnEveningAtTheTavernTests.MeasureTheTavernOnTheGateSeeds`, explicit: the 23 gate seeds, three days with the tavern player. The tavern stands by t3,400–7,900 and is first kept around t11,800–24,700 (when a resident trusts the patron enough), serving 0–10 (5.8 on average) on day 2 or 3; 4 to 9 of 12 residents spend an hour or more at a venue other than the hearth, 0.32–0.89 h a resident-day (0.55 on average); 4 to 13 wants arise a seed and 0 to 6 come true, about twice as many as run out; every seed's days hold but one (seed 17's anomalies, Epic 10's existing flicker).
- **`AnEveningAtTheTavern`** (seed 5): built t3,912, kept t12,115, 9 served (7 ales, 2 suppers) for 48g on days 2 and 3; 8 of 12 residents an hour at a venue; wholesale 78g and the artisans' wages in the ledger, which balances; a save on day 3 loads with the tavern standing and its keeper's contract.
- **`AWantComesTrue`** (seed 5): 12 wants arise, 3 come true at their venues, none missed.
- **D24's reading, amended by measurement:** "leisure at venues reaches an hour a resident-day" read 0.32–0.89 h, so the DoD asks for half the residents to spend an hour at a venue other than the hearth over the three days, and for the tavern to serve on two days. Plan 3's evenings (sleep at night, F16; work's satiation, F11) are where an hour a resident-day is expected.

### 5.11 Plan 2's gate (C.1, 2026-11-24)
- **The tests:** core 370/370, client 563/563 and the same in the pinned image (renders byte for byte); golden 3/3.
- **The soak:** 21 of 21 on the default seeds (seven profiles); `guild`, `crafting`, `hunting` and `tavern` 20 of 20 each on seeds 1–20.
- **The day** (`MeasureACrawlersDay`, the 23 gate seeds, three days, no player): rest 10.0 h, work 8.3 h, the hearth 2.6 h, leisure 0.7 h (Recreate 0.5), unweighted 1.0 h; no plan unfound; 0.8 flips a resident-day; 65.6 of 66 pairs meet a day; boredom 18, 31, 33 at the days' ends. With the guild player: work 7.5 h, the hearth 3.1 h, leisure 1.0 h. The clock's table is §5.1's shape: the outpost still has no night (F16, plan 3's).
- **Moods** (`MeasureMoodOnTheGateSeeds`): no player, mean 8.3, Steady 51%, Good 47%, Low 1.4%; with the guild's clears, mean 9.2, Happy 1.6%; no member stayed behind or deserted on the gate seeds.
- **F17** (`MeasurePassionsOnTheGateSeeds`): a passion burned bright on 7 of 23 seeds; harder foes taught Apprentices more on none (no blow by an Apprentice on a rat). Still the producer's call.
- **Cost** (`PopulationScalingProbe`, Release, alternating twice with `main` at `ebc1ef2`, load about 4): 16 crawlers 16.9–20.4k ticks a second against 17.6–18.7k; 24 crawlers 18.4–18.8k against 16.9–17.5k. The 12-crawler case ran first and read 6–9k on both. Free time costs nothing measurable at this noise: leisure's scoring is offset by fewer plans (552 decisions a resident-day, from about 600 at L0.3, §3.1).

## 6. Rhythm (plan 3)

### 6.1 Sleep by the clock, before rhythm (P.1, 2026-11-24)
`MeasureACrawlersDay` gained sleep's columns: the share asleep by clock hour (in a bunk, resting where it stands or collapsed), each sleep's length (a spell asleep two game hours or more), the hours awake between sleeps, and hands-on work (harvesting or a station's work). The 23 gate seeds, three days, residents, on `feat/life1-rhythm` at its start (behaviour as plan 2's close-out, `d266b86`; plan 2 not yet merged, so the hand-over was checked against that branch). The day's totals are §5.11's.

| | No player | The guild player |
| :--- | ---: | ---: |
| Sleeps a resident-day | 1.04 | 1.03 |
| A sleep's length, mean (p10–p90) | 8.0 h (7.7–8.5) | 8.0 h (7.7–8.7) |
| Hours awake between sleeps, mean (p10, p50, p90) | 10.7 h (9.1, 10.0, 14.0) | 11.1 h (9.1, 10.3, 15.2) |
| Work, all of the family's plans | 8.3 h | 7.5 h |
| Hands-on work (harvesting, a station) | 3.1 h | 2.9 h |
| Asleep at 02:00 / 06:00 / 12:00 / 18:00 | 35% / 20% / 40% / 53% | 29% / 16% / 41% / 58% |

- **A cycle of about 18.7 game hours:** 10.7 awake and 8 asleep, so a resident's sleep drifts about five hours earlier each day and the outpost's sleep is smeared over the clock (F16). Its peak (53% at 17:00–21:00) is the first day's: every resident spawns rested at 06:00 and tires together.
- **Work is mostly walking to it:** of 8.3 hours on the Work family's plans, 3.1 are hands-on; the rest is the walk, the trade and the board.
- **For R.2:** full to the tired line (60 energy) in 10.7 hours is a drain of about 5.6 an hour awake, a mix of standing, walking and work. D27's factor of 0.6 makes it about 18 hours, and the bedtime pull (R.3) sends a crawler to bed before then.

### 6.2 One waking and one night (R.2, D27, 2026-11-24)
The awake drains are scaled by `DayTuning.WakingDayFactor`, 0.6 (standing 1.65 an hour, walking and working 4.95, a station's work 3.6); a bunk still restores 8 an hour. `MeasureACrawlersDay`, before the bedtime pull:

| | Before (§6.1) | At 0.6 |
| :--- | ---: | ---: |
| Hours awake between sleeps, mean (p10, p50, p90) | 10.7 (9.1, 10.0, 14.0) | 18.2 (15.2, 16.7, 24.5) |
| A sleep's length, mean | 8.0 h | 7.8 h |
| Work (the family's plans), hands-on | 8.3 h, 3.1 h | 9.6 h, 3.5 h |
| Rest over three days, a resident-day | 10.0 h | 6.5 h (the first sleep now comes near midnight) |

- **A cycle of about 26 hours** on its own: the waking day is now longer than the clock leaves room for, so without the bedtime pull a resident's sleep would drift later each day.
- **The day's checks:** a night's sleep now runs past the day's 06:00 end, so `OutpostWatch`'s "slept in a bunk" and `TiredCrawlersSleepInTheBunkhouse` count crawlers seen asleep in a bunk rather than sleeps finished. The gate seeds' day holds on all 23.

### 6.3 The bedtime pull (R.3, D31, 2026-11-24)
In its routine's night a crawler's `RestGoal` is valid below the bedtime line (energy 70, `Considerations.BedtimeEnergy`) and weighs 1.5 (`LifeTuning`'s routine weight for the Rest family in the Night block, the first of D30's table); the hearth costs like sleeping rough at bedtime, so a crawler goes to a bunk. A sleeper in its night dozes on at full energy until its waking hour (`AgentState.WakesAt`); hunger's line still wakes it, and the longest sleep (12 hours) still ends it.

**The day by the clock** (the 23 gate seeds, three days, no player; asleep, a share of residents' time):

| Hour | 06:00 | 08:00 | 10:00 | 12:00 | 16:00 | 19:00 | 21:00 | 22:00 | 23:00 | 00:00 | 02:00 | 04:00 |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Before (§6.1) | 20% | 24% | 34% | 40% | 47% | 53% | 52% | 50% | 48% | 44% | 35% | 36% |
| With the bedtime pull | 49% | 12% | 4% | 1% | 0% | 0% | 13% | 29% | 56% | 82% | 93% | 98% |

- **F16 resolved:** the outpost sleeps at night, 82–99% asleep from midnight to 05:00 and nobody from noon to 19:00. Hours awake between sleeps: mean 16.1 (p10 15.0, p50 16.2, p90 17.0); a sleep 7.9 h (p10 7.0, p90 9.1).
- **The rest of the day:** work 9.2 h (hands-on 3.3), the hearth 3.2 h, leisure 0.9 h, rest 7.8 h; flips 0.9 a resident-day; no plan unfound. With the guild player: work 8.2 h, leisure 1.2 h.
- **Golden unchanged:** its 3,000 ticks end at 13:08 on day 1, before anyone's bedtime.

### 6.4 Weights by family and hour (R.4, D30 amended, 2026-11-24)
D30's table in `LifeTuning`: the working block weighs work 1.2 and leisure 0.8; the leisure block work 0.8 and leisure 1.2; the night work 0.6, leisure 0.7, company 0.8 and rest 1.5. An artisan's working block is its shifts.

- **Company weighs 1 by day (D30 amended by measurement).** The first table weighed it 0.9 working and 1.15 at leisure: the hearth, already the strongest pull on free time, took 5.2 h a resident-day (from 3.2), and over `TheGuildBreaksTheGatekeeper`'s 16 days residents harvested and sold about half as much, trust toward the patron faded to 0, the guild lost its members and never reached the gate. With company at 1 by day, the gate breached at t76,230 (t98,528 at plan 2's end).
- **The day** (three days, no player): work 9.2 → 8.5 h (hands-on 3.3 → 3.1, the baseline's), the hearth 3.2 → 4.1 h, leisure 0.9 → 1.0 h; awake 16.2 h.
- **Chronotypes by the clock:** larks asleep 98% at 23:00 and 27% at 05:00; owls 12% at midnight and 56% at 09:00; the average 48% at 23:00 and 18% at 07:00.

### 6.5 Weariness and the windfall (R.5, D33, 2026-11-24)
Weariness (a float at 268) rises 1 a game hour at a harvest, a station's work or a fight; a windfall (a single payment of at least half the comfortable purse: a bounty, a hunt's share, an expedition's share) records its tick at 272 and thinks "Earned a rest" (+4 for 24 hours). Mean weariness over three days: 5.2; over six, 8.4–9.5.

- **Windfalls are rare in today's economy:** the seeded bounties pay 25g and 15g, an expedition's share about 21g. None came in six days with no player or with the guild player; a hunt bounty paid to a lone hunter is where one comes from (D.1).

### 6.6 Satiation on work (R.6, D32, 2026-11-24)
`Rhythm` weighs the Work family's goals by satiation (1 up to the purse target, the comfortable purse times 1 + greed × 0.5 plus what held wants ask for, falling to 0.6 at twice it) and weariness (1 − 0.3 × weariness / 100). Golden unchanged.

- **It barely bites:** over three days residents are at or under the comfortable purse 92% of the time and never at work above it, as before (`EarnIncome` stops at the comfortable purse). F11 stays open: earning outbids free time because the hearth and work are strong, not because crawlers are rich.
- **F18 re-read:** the tavern serves 10–12 a seed in three days on the gate seeds (5.8 at plan 2's end): the evenings by the clock help, not satiation.

### 6.7 The day's intention (R.7, D34 amended, 2026-11-24)
Drawn once a routine day, at the first tick awake outside the night, on `SimRandomSalt.Intention` (23), saved at 276 with its day at 288. A rest day halves weariness and a second in a row clears it.

**Six days, the 23 gate seeds** (before: R.6 measured over the same six days):

| | Before, no player | After | Before, the guild player | After |
| :--- | ---: | ---: | ---: | ---: |
| Work (hands-on) | 7.9 h (2.9) | 7.4 h (2.8) | 6.1 h (2.5) | 5.7 h (2.4) |
| The hearth | 4.9 h | 5.6 h | 6.1 h | 6.7 h |
| Leisure | 1.2 h | 1.1 h | 1.5 h | 1.4 h |
| Intentions a resident-day: work, rest, training, a want's | | 79%, 8%, 12%, 1% | | 77%, 11%, 12%, 1% |

- **A rest day by the fire:** a rest day's hours are 57% at the hearth, 29% asleep, 3–6% at venues and 3–4% at work. D34's company weight on a rest day (1.2) is amended to 1, as D30's was, though with it at 1 the hearth still takes the freed hours: its base is high whenever company runs low, and leisure's is small while the hearth keeps boredom near 15 (F11).
- **Training days** come with a passion (most residents have one), 12% of days.

### 6.8 A rest day keeps to safe zones (R.8, D35, 2026-11-24)
On its day off a crawler's lone danger limit is 0 (`Vitality.Resting`): it ventures nowhere (`WouldVenture`), enters no hazard sector and walks no charted lair's reach, not even one it would clear (`WouldEnterAt`, `LairDangerFor`, `WouldWalk`), and doesn't hunt (`Hunting.IsHunter`). Rescue judges a day off as a working day (`WouldEnterAt(..., restDay: false)`): a friend still goes for the fallen. `OutpostWatch` fails a day on any resident-tick on a rest day in a hazard or a charted lair's reach but to rescue or under orders, and a day off asks no harvest-and-sell cycle of a resident.

- **Two findings of the watch's first runs:**
  - a resident awake at its waking hour (every resident on the first morning, waking at 06:00 at the session's start) drew a day off with a working day's plan in hand and walked it into the Sump: a day off begun with a Work-family plan now drops it (orders are kept);
  - a scout that took up the seeded scout bounty that morning drew a day off at its waking hour and left it unfinished (gate seed 15): the outpost's first routine day has no night's sleep before it, so it is a work day for everyone (D34's "on waking from its night's sleep").
- **The gate seeds' day holds on 22 of 23,** the 23rd (seed 1) on one early jitter at t883 near the Garden (Epic 10's F5 kind), which R.4's change of choices moved onto it.

### 6.9 Duty and a day off; Q11's switch (R.9, D36, D37, 2026-11-24)
The routine never weighs duty (an artisan's working block is its shifts, a muster the player's timing). A rest day weighs duty 0.8 (`RhythmTuning.RestDayDuty`), so compliance decides whether a member answers on its day off; one keeping to its orders (its plan duty's, under an expedition's stage or on shift) is called in, its day a work day again (`AgentState.CallInIfKeepingOrders`, `IntentionRules.CallIn`), so the day off's safe zones never fight its orders. Q11's switch (`RhythmTuning.OutpostRestDay`, off) makes every seventh game day a rest day for every resident.

`DaysOffAndOrdersTests.MeasureDaysOffAndOrders` (the 23 gate seeds, eight days, the guild player sending expeditions to the Drowned Gallery): 7–11 trips a seed; members drew 0–9 days off a seed; every member on a day off at a muster but two was called in and went (on 22 of 23 seeds); none stayed behind on a day off, since orders at 120 × 0.8 still outweigh anything a day off offers; none went into danger on one. `AMemberCalledInFromItsDayOffGoes` (seed 5) asserts it live. Golden unchanged.

### 6.10 The economy with rhythm (R.10, 2026-11-24)
The soak's numbers are in [`docs/economy-model.md`](economy-model.md) ("Rhythm"): every profile within its band (21 of 21 on the defaults; guild, crafting, hunting and tavern 40 of 40 each on seeds 1–40); the tavern serves about 12 a seed in three days; the crafting loop's bands set by exception (goods on 37 of 40, so 0.8 again; a quit for pay on 13 of 40, so 0.35: F20). D25's work hours: 7.4 a resident-day with no player over six days, 5.7 with the guild's expeditions.

- **The anomaly detector, refined:** a move after standing still longer than the jitter window starts a fresh heading. Gate seed 1 failed its day on one jitter at t883: four residents jammed at the Deep Seam's door, one stood a second, the motor watchdog nudged it aside, and the nudge counted as a reversal of its heading from before the stall. Over seeds 1–60 and the gate seeds, jitter alerts 2 → 1 (seed 25's, with a thrash, remains); behaviour unchanged.

### 6.11 The Definition of Done: a day off after a windfall (D.1, 2026-11-25)
`ADayOffAfterAWindfallTests`, live on the real outpost with the workshop player arming residents and the hunt player posting hunts at the default reward (60g), nothing injected.

- **`MeasureADayOffAfterAWindfall`** (the 23 gate seeds, ten days): 83 windfalls (hunts' shares of 40g or more) on 21 seeds; 49 of the 74 intentions drawn within a day of one were rest days (two in three: 5% + 50% + weariness's share); the whole moment (a day off after a windfall with an hour or more at the hearth or a venue, and work the day after) on 18 seeds, the days held on 17; work 6.8 hours a resident-day (D25's 6–8).
- **`ADayOffAfterAWindfall`** (gate seed 20261010): a resident paid a hunt's share at t15,054 on day 2 rests on day 3 (16.1 hours at the hearth or a venue, none in danger) and works on day 4; a save on the day off loads byte for byte with its intention, weariness and windfall; work 6.7 hours a resident-day.
- **A day off by the fire:** the hours at "the hearth or a venue" are mostly the hearth's (§6.7), so the DoD counts the hearth, a venue in the map's own table, where plan 2's counted venues other than it.

### 6.12 Epic LIFE-1 together (G.1, 2026-11-25)
`LifeOneTogetherTests`: every life-core DoD on one seed, live with the tavern (evening), guild (expeditions), workshop (blades) and hunt players, nothing injected. `MeasureLifeOneTogether` (the gate seeds, eight days): moods rose with every meal and bunk's sleep on 17 of 23; wants came true on 22; the tavern served in the evening on two days or more on 22 (one seed's trust never rose, and its tavern was never kept); a day off after a windfall on 9; every beat with the days held on 6. `LifeOneTogether` (gate seed 1): 12 residents ate and slept well, 150 moods rose and none fell; 11 wants arose and 6 came true; the tavern took 38g in the evenings of days 4 and 5; a day off on day 4 after a windfall, 15.6 hours at the hearth or a venue, work the day after; saved at t45,722, loaded byte for byte to the same world hash, and played on for a game day with its players.

### 6.13 Plan 3's gate (G.2, 2026-11-25)
- **The tests:** core 378/378, client 566/566 (and the same in the pinned image, renders byte for byte); golden 3/3.
- **The soak:** 21 of 21 on the default seeds; `guild`, `crafting`, `hunting` and `tavern` 20 of 20 each on seeds 1–20 (40 of 40 on seeds 1–40 at R.10 and D.2).
- **The day** (`MeasureACrawlersDay`, the 23 gate seeds, three days, no player): work 8.2 h (hands-on 3.0), the hearth 4.7 h, leisure 0.9 h, rest 7.6 h; awake between sleeps 16.3 h (p10 15.3, p90 17.0), a sleep 7.8 h; asleep 80% at 00:00 and 99% at 03:00, none from 12:00 to 18:00; flips 0.9 a resident-day; no plan unfound. Chronotypes: larks asleep 99% at 23:00 and 29% at 05:00; owls 17% at midnight and 53% at 09:00. With the guild player: work 7.4 h, the hearth 5.0 h, leisure 1.2 h.
- **Days off** (the same run, no intention on day 1): 0.73 intentions a resident-day, 80% work, 7% rest, 12% training, 1% a want's; a rest day is 57% at the hearth, 28% asleep, 5% at work, 4% at leisure.
- **Moods** (`MeasureMoodOnTheGateSeeds`): no player, mean 8.8, Steady 50%, Good 49%, Low 0.8%; with the guild's clears, mean 9.7, Happy 1.5%, Low 1.1%; no member stayed behind or deserted a trip.
- **F17** (`MeasurePassionsOnTheGateSeeds`, fourteen days): a passion burned bright on 8 of 23 seeds; harder foes taught Apprentices more on none (no Apprentice's blow on a rat). Still the producer's call.
- **Cost** (`PopulationScalingProbe`, Release, alternating twice with plan 2's end at `d266b86`, load about 2–2.5): 16 crawlers 14.4–16.6k ticks a second against 18.2–19.1k; 24 crawlers 16.2–16.4k against 19.0–19.1k; 48 crawlers 6.0k against 6.8–6.85k (12–15% fewer). The 12-crawler case runs first and reads 5.6–7.0k on both. **Why:** residents sleep about 7.6 hours a day instead of 10 and decide while awake (about 680 decisions a resident-day at R.6, from 551 at P.1's baseline); checking the intention once a routine day and the purse target's fast path saved nothing measurable (golden unchanged).

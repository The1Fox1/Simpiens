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

### 2.2 The rule
*Filled at L0.5.*

## 3. Measurements
*L0.3: a crawler's day on the gate seeds.*

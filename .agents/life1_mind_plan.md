# Epic LIFE-1 plan 1: Groundwork and the mind (L0, and L1's thoughts and mood)

This is a working document (AGENTS.md, Documentation Protocol 6), the first of Epic LIFE-1's three plans. It was written 2026-11-23 at the producer's "write up the first implementation plan to cover the first set of work (whether it's all of L1 or only part of L1 is up to your analysis)".
- **The design** is the epic, [`docs/epics/epic-life-1.md`](../docs/epics/epic-life-1.md). Its rules A1–A12 bind every step, and its questions Q1–Q11 stand by exception.
- **The decisions** below (D1–D10) are this plan's defaults. Each moves to the epic's §0 when its step lands.

- **Complexity:** Very Complex as a whole: a refactor of goal choice that every decision passes through, saved state (format 15), a new rule cognition reads, and the client's faces and tabs. Split below so that every step is Basic or Possibly Complex. L0 and L1 are Very Complex as phases only by bundling. No step needs supervision: the golden hashes are a complete check of the two steps that touch every decision (L0.4, L0.5), and they land only if golden reproduces byte for byte.
- **Scope:** all of L0, and L1 except its wants. Wants go to plan 2 with L2's venues, so they have something to want (the epic's header gives the reasoning).
- **Branch:** `feat/life1-mind`, cut from `feat/epic11-masterworks` at `1fca6ba`, because PR #27 (Epic 11's plans 3 and 4) is still open and `main` lacks them (the epic's F7).
  - One PR into `main` at the end, with a commit per step.
  - It opens only after #27 merges, so it is never stacked. Once #27 merges, the PR shows only this plan's commits.
- **Estimate:** 11–15 agent days (L0 5–7, L1 5–7, the gate 1).
- **The producer's stops:**
  - the go, before L0;
  - the look at L1 (faces, the mood line, the dossier's tabs), after L1.7;
  - L1.4's soak bands, by exception;
  - the merge, after #27's.
- **The checks:**
  - golden unchanged through L0.4 and L0.5;
  - the gate seeds' day holds after L0.6 and L1.4;
  - `MoodFollowsTheDay` (L1.8), live on the real outpost with nothing injected.

## 0. What Epic 11 hands over (checked at L0.1)
- **The goals:** 21 in `OutpostGoapDomain.BuildGoals`, in this order. The order breaks ties, because the first goal with the highest priority wins (`>`).
  - **Formulas:**
    - `SatiateHunger` = hunger;
    - `PurchaseProvisions` = hunger × 1.1;
    - `Rest` = 100 − energy;
    - `Socialize` = (100 − social, +10 with a warm or trusted friend) × the sociability factor;
    - `GatherAtAnchor` = (max(0, 75 − social) + 15, +5 with a friend) × sociability, 0 in a crisis;
    - `EarnIncome` = max(5, 25 + the purse's need − hunger × 0.2) × greed × diligence;
    - `FulfillBounty` and `Hunt` = (35 + the reward's share × greed) × diligence − hunger × 0.3, at least 5;
    - `LiquidateSalvage` = (30 + 15 a sellable held) × greed;
    - `AidCompanion` = the altruism utility.
  - **Constants:**
    - `ArmForFight` 33, `CarryMedicine` 38, `OutfitForDanger` 40, `EquipForWork` 35;
    - `CollectLoot` 42, or 125 under a clear's orders;
    - `Rescue` 90, or 130 under orders;
    - `Study` 30, `ReadManual` 27, `Drill` 28, `LeaveOutpost` 95;
    - `ComplyDirective` 120 on an expedition or a shift, otherwise (50 + compliance) × loyalty.
- **The choice** (`AgentCognitionService.EvaluateAsync`):
  1. the reflexes (Tier 1);
  2. the active plan, stepped until a step is invalid (goals are not re-scored while it runs);
  3. otherwise the argmax over valid, unsatisfied goals, with the top three kept in `DecisionSummary` and the best planned once;
  4. with no plan, Tier 3's `AmbientWanderEvaluator`, then `IdleIntent`.
  - `GoapCognitiveEvaluator` repeats the argmax; only `PlanExecutionValidator` uses it (the epic's F1).
- **Personality:** every trait enters as `Considerations.TraitFactor(trait, strength)`, which is exactly 1 for a neutral trait. Passions weigh a node's score in `ResourceTargeting`, not a goal.
- **Needs and frustration:**
  - **Needs:** `AgentNeeds` (hunger, energy, social; 12 bytes) in `CrawlerRecord`, paced by `DayTuning`.
  - **Frustration:** +25 when a harvest fails, and +25 (`SimulationManager.GriefFrustration`) for each friend who dies, beside a `Grieved` memory event. It falls by 1 every real second (F6), and tantrums come through `MentalBreakEvaluator`.
- **Events a thought can ride on:**
  - deeds, counted in one entry (`AgentState.AddDeed`): a creature slain, a rescue, a make, a downing survived, a hunt paid, a clear won, a crawler taught;
  - a band reached (practice's entry), an epithet, a technique chosen, a masterpiece made;
  - wages paid or unpaid (`GuildService`, `WorkshopService`);
  - a bounty delivered, meals (`Cognition/ItemUse`; the Garden plot's rule), a bunk slept in, an exhaustion collapse;
  - food shared, gossip (`SocialDynamics`), a failed harvest.
- **The chronicle:** 61 event types (0–60), one ring of 1,024 events outpost-wide and a ring of 100 per sector, each event with an intensity. `TemplateNarrativeFormatter` words them.
- **The client:**
  - **Faces:** `MoodRule` has seven moods (asleep, hurt, frustrated, scared, hungry, tired, content), held by `MoodTracker` for 30 ticks. `PortraitKit` draws each mood's features, and `PortraitKey` includes the mood.
  - **The dossier:** a header (portrait, name, description, badge, mood line), then needs, gear, skills, optional lines, groups, what they're doing, and feelings. It has no height to spare at 720 rows (Epic 11's F16).
- **Saves:** format 14. `CrawlerRecord` ends with `Traits` (`CrawlerTraits`, 256 bytes, pinned by `EconomySerializationValidator`).
- **The planner:** 48 of 64 facts in use.
- **The economy:** a comfortable purse of 80g, and about 40g a day spent, so crawlers earn what they spend (D25's 6–8 hours unmet; [`docs/time-model.md`](../docs/time-model.md)). The soak's bands are in `docs/economy-model.md` §7. The producer's word during Epic 11 stands: "Economy flukes are tolerable until they completely ruin our testing scenarios."

## 1. Decisions proposed (defaults; the producer answers by exception)
- **D1, motives and families** (A1). New enums in `Cognition/Planning/`:
  - **`Motive`:** None, Hunger, Rest, Company, Income, Gear, Growth, Duty, Aid, Leave. Plan 2 adds Fun and Want; LIFE-3 adds Aspiration.
  - **`ActivityFamily`:** Unweighted, Work, Leisure, Social, Rest.
  - **Declared on `GoapGoal`'s constructor.** A virtual `FamilyFor(context)` returns the declared family unless a goal overrides it.
  - **Growth is leisure:** Epic 11's training happens in free hours, as its D10 put the drill.
  - **Unweighted goals** are never scaled by the routine or the day's intention (A8).

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
  | `Study`, `ReadManual`, `Drill` | Growth | Leisure |
  | `LeaveOutpost` | Leave | Unweighted |

- **D2, the motive model** (A1; Q3):
  - **The rule:** `MotiveModel.Priority(goal, context)` = the goal's base × `MotiveModel.Weight(goal, context)` + commitment.
  - **The base:** `GoapGoal.CalculatePriority` is renamed `BasePriority`. It keeps its body, so every goal's number is today's.
  - **The weight** is a product of factors, each a lookup in `LifeTuning`: mood by motive (L1.4), the want (plan 2), the routine by family and hour, satiation on work, and the intention (plan 3). Each factor is exactly 1f until its milestone. The factors are multiplied together first, then the base is multiplied by the result. A float times exactly 1 is itself, so golden can't move.
  - **Commitment** is added only when it isn't 0 (D5).
  - **The one door:** the selector calls only `MotiveModel.Priority`. A names test checks that no production code outside the model calls `BasePriority`. Validators that pin a goal's own number call the base.
  - **`LifeTuning`** is a record inside the session's `EconomyTuning`, as `DayTuning` is. It holds the factors' tables, commitment and the fallback's depth.
  - **New goals:** `Urgency.Of(motive, context)` gives the shared urgency of a motive (hunger, 100 − energy, 100 − social; fun from plan 2). A new goal's base is its urgency × its opportunity, with no constants of its own (AGENTS.md gains "Adding a goal").
  - **Not in this plan:** rewriting the 21 bases as urgency × opportunity. A base is rewritten when its milestone needs it, as its own behaviour change.
- **D3, one goal selector** (A2; F1):
  - **`GoalSelector.Rank`** fills a fixed three-slot ranking of the valid, unsatisfied goals by `MotiveModel.Priority`. Ties go to the earlier goal, as today.
  - **Its callers:** `AgentCognitionService` and `GoapCognitiveEvaluator` both call it. Their own loops and trace lines move into it, with the same log text.
  - **`DecisionSummary`** keeps the top three, and gains the chosen goal's base and weight, for the "why" the dossier can show at L1.5.
  - It keeps no state between calls and allocates nothing.
- **D4, the fallback** (Q4; F2):
  - **The rule:** when the best goal can't be planned, the selector's next goals are planned in order, up to `LifeTuning.FallbackDepth` more (2), before Tier 3.
  - **The record:** the source reads "GOAP fallback", and each plan not found is traced.
  - **Its own behaviour change:** golden is re-recorded in its own commit, and the gate seeds' day and the soak's bands are checked.
  - **The cost:** at most two more plans per evaluation, measured at the gate.
- **D5, commitment** (F3):
  - **The rule:** the goal whose plan just ended, if it is still valid and unsatisfied, gets `LifeTuning.Commitment` at the next choice. A plan cut short by a reflex counts as ended.
  - **Not saved:** which goal that was is work in progress, so after a load there is none.
  - **Its value:** 0 at L0, unless L0.3 measures switching between near-equal goals at plans' ends often enough to matter (the threshold is set with the measurement). Plan 2 sets it when leisure options compete.
- **D6, the life record** (A3; Q5):
  - **The record:** `CrawlerLife` in `Simulation/Life/`, 512 bytes, its size pinned. It sits in `CrawlerRecord` after `Traits` and is copied to cognition as `AgentContext.Life`.
  - **The format:** 15, the series' one raise (`SessionSaveHeader`). A format 14 save is refused with its reason.
  - **The layout:**

    | Offset | Size | What | Filled by |
    | ---: | ---: | :--- | :--- |
    | 0 | 128 | 16 thought slots of 8 bytes: the thought (2), stacks (1), subject (1: 0 for none, 1–4 a slot below), begun at (4, a tick) | L1.2 |
    | 128 | 64 | 4 subjects, an `EntityId` each: who the thoughts name. A slot frees when no thought names it | L1.2 |
    | 192 | 72 | 3 wants of 24 bytes: the want (2), flags (1), spare (1), expires at (4), its target (16) | plan 2 |
    | 264 | 16 | fun (4), weariness (4), the last windfall's tick (4), the day's intention (1), curiosity (1), spare (2) | plans 2 and 3 |
    | 280 | 8 | variety: recent activity groups' tolerance | plan 2 |
    | 288 | 224 | spare, for LIFE-2 (taste 4; keepsakes 4 × 24) and LIFE-3 (an aspiration and its progress, 8), with about 100 bytes left | later |

  - **Every field reads 0 as "none",** so a field filled later loads from an earlier format 15 save unchanged.
  - **The world hash** mixes the record only when it isn't empty, so golden doesn't move at L0.8.
- **D7, thoughts** (A4, A5):
  - **The table:** `ThoughtTable` holds one row per `Thought` (a `ushort`, never reused): the player's words, a value, a duration in game hours, a stack limit, a stack factor (each further stack adds the value times the factor to the power of the stacks before it), whether it names a subject, and a kind (need, event, social).
  - **The entry:** `AgentState.Think(thought, subject, tick)`, on the main thread only.
    - **Restacking:** the same thought with the same subject adds a stack up to its limit and starts its duration again.
    - **A full record** replaces the slot whose |value| × time left is least.
    - **Expiry:** expired slots are cleared when the record is read or written, with no per-tick sweep.
  - **One hook for deeds:** a deed's row may name the thought it brings (`DeedTable`), so `AddDeed` thinks it, and the deeds' sites gain no new calls.
  - **The starting rows** (values to set at L1.3; "d" is a game day):

    | Thought | Value | Lasts | Stacks | Names | Thought where |
    | :--- | ---: | :--- | ---: | :---: | :--- |
    | A good meal | +3 | 6 h | 2 | | a meal that takes 30 or more off hunger (`ItemUse`) |
    | Ate at the Garden | +4 | 8 h | 1 | | the Garden plot's meal (its node rule) |
    | Slept in a bunk | +3 | 14 h | 1 | | waking from a bunk's sleep |
    | Slept on the floor | −5 | 14 h | 1 | | an exhaustion collapse |
    | A good talk | +2 | 6 h | 3 | | a gossip exchanged (`SocialDynamics`) |
    | Fed by {0} | +4 | 1 d | 1 | ✓ | food shared, for the one fed |
    | Shared my food | +2 | 1 d | 2 | | food shared, for the giver |
    | Paid in full | +3 | 1 d | 1 | | a wage paid in full |
    | Wage unpaid | −8 | 2 d | 2 | | a wage unpaid |
    | Raise unpaid | −4 | 2 d | 1 | | paid below the asking wage (Epic 11, D8) |
    | A good payday | +4 | 1 d | 3 | | a bounty paid, a hunt's share, an expedition's share |
    | A wasted trip | −2 | 4 h | 3 | | a failed harvest (beside today's frustration) |
    | Hurt | −4 | 12 h | 3 | | struck by a creature |
    | Downed | −12 | 2 d | 1 | | a downing survived (the deed) |
    | Rescued by {0} | +10 | 3 d | 1 | ✓ | carried out or given medicine |
    | Saved {0} | +8 | 2 d | 1 | ✓ | the rescue (the deed) |
    | {0} died | −20 | 5 d | 3 | ✓ | `Grieved`, beside today's frustration |
    | A kill | +4 | 1 d | 3 | | a creature slain (the deed) |
    | Grew in my craft | +8 | 2 d | 1 | | a band reached |
    | Earned a name | +12 | 3 d | 1 | | an epithet |
    | Made a masterpiece | +15 | 5 d | 1 | | a masterpiece made (the deed) |

- **D8, mood** (A5):
  - **The rule:** `MoodRules.Of(needs, health, downed, life, tick)` = the sum of the need terms (`MoodTerms`, a table) and the live thoughts' values, held within −100 to 100. It is pure and never saved. The main thread and cognition read the same function.
  - **The need terms** (starting values):

    | Term | When | Value |
    | :--- | :--- | ---: |
    | Hungry | hunger at least 60 | −6 |
    | Starving | hunger at least 80 | −15 |
    | Tired | energy at most 30 | −5 |
    | Exhausted | energy at most 15 | −12 |
    | Lonely | social at most 25 | −6 |
    | Hurt | health under 50 | −8 |
    | Downed | downed | −20 |
    | Comfortable | none of the above | +4 |

  - **The bands** (`MoodBand`): Breaking up to −35, Low to −10, Steady to +10, Good to +30, Happy above. L1.3 sets them, so that an ordinary day sits mostly Steady to Good ("average is the default") and a friend's death reaches Low.
  - **The name:** the core's word is `MoodBand`, so it never collides with the client's face enum, `Mood`.
- **D9, what mood changes** (Q6; F6). The starting values are set at L1.4:
  - **Frustration:** Low adds 5 a game hour, Breaking 15, and Good and Happy ease 5. This feeds today's tantrums through `MentalBreakEvaluator`.
  - **F6 first:** frustration's fall moves onto the day's pace (`DayTuning.FrustrationEasePerHour`), because at 42 a game hour it drowns any hourly term. It is its own commit inside L1.4's behaviour change, measured on the gate seeds before and after.
  - **Weights by motive** (`LifeTuning`):

    | Band | Company | Income |
    | :--- | ---: | ---: |
    | Low | ×1.25 | ×0.9 |
    | Breaking | ×1.5 | ×0.75 |
    | Steady and above | ×1 | ×1 |

    Plan 2 adds Fun's column.
  - **How well work is done** doesn't change (Q6).
- **D10, what the player sees** (A10; Q7; F4, F5):
  - **The day log:** `DayLogModel` in `Presentation/Dashboard/`.
    - **Its lines:** the chronicle's events since 06:00 where the crawler is either actor, in the formatter's words, merged by tick with the thoughts begun since 06:00, in the table's words.
    - **Stored:** nothing.
    - **If a day overflows the chronicle's ring** (L0.3 counts lines a day), a per-crawler window of the day's event ids is decided at L1.5 and saved in the chronicle's block.
  - **The mind:** `MindModel`, the mood's value and band, its need terms, and its thoughts with their value and time left ("Rescued by Bram +10 · 2 days left").
  - **Faces:**
    - **New moods:** `Mood` gains Happy (7), Low (8) and Grieving (9), appended.
    - **`MoodRule`'s order:** asleep, hurt, frustrated (a tantrum), scared, grieving (holding a "{0} died" thought), frustrated, hungry, tired, low, happy, content.
    - **What it reads:** `MoodSigns` gains the band and grieving.
    - **The drawing:** `PortraitKit` draws each new mood's features.
  - **The mood line** names its reason, the largest live thought or need term: "Happy: rescued by Bram", "Low: Ama died".
  - **The dossier's tabs:**
    - **The tab row:** three stone buttons under the header: Crawler, Mind, Today. `StoneSkin.SetActive` lights the open one, as it lights a window's button.
    - **What each holds:** Crawler holds today's sections, Mind the mind, and Today the day log, newest first, scrolling.
    - **What stays:** the header stays on every tab, and the dossier keeps its height at 720 rows.

## 2. The milestones, phased

### L0. Groundwork (5–7 days)
**Complexity:** Very Complex as a phase by bundling (a measurement, the hot path of every decision, a behaviour change, saved state). Split into eight steps, each Basic or Possibly Complex. Golden stays unchanged through L0.2–L0.5, L0.7 (at 0) and L0.8. Only L0.6 re-records it.
- [ ] **L0.1 The hand-over checked, and the life model** [Basic: docs from the code as it stands]
  - Check §0 against the code, and record any difference as a finding (the epic's §7, F8 onward).
  - Create `docs/life-model.md` with what stands today:
    - the 21 goals' bases and their ranges;
    - the choice's tiers;
    - the needs and frustration;
    - where events happen that thoughts will ride on.
  - Link it from the epic and from AGENTS.md's cognition paragraph.
- [ ] **L0.2 Motives and families declared** (D1) [Basic: metadata only; golden unchanged]
  - `Motive`, `ActivityFamily`, `GoapGoal`'s constructor and `FamilyFor`, and the 21 goals' declarations.
  - Test goals in the validators keep compiling through defaults (None, Unweighted).
  - **The check:** `MotiveValidator`: every production goal declares a motive other than None, and its family is one of D1's. `docs/life-model.md` carries D1's table, written from the code.
- [ ] **L0.3 The day measured** (F2, F3, F5) [Possibly Complex: measuring]
  - **The observers:** `SimulationManager` gains observed events for a goal chosen and a plan not found, raised when the decision is applied on the main thread. No rule reads them, as Epic 11's `Practised`.
  - **The test:** `MeasureACrawlersDay`, explicit, over the 23 gate seeds and three game days. Two runs: no player, and the guild player.
  - **Per resident-day:**
    - time by family and by goal, with hours of work (D25) and ambient wandering's share;
    - goals chosen, and plans not found with what followed;
    - switches at plans' ends between goals within 5% of each other (D5's question);
    - distinct pairs of residents that came within the gossip radius;
    - chronicle lines a day, outpost-wide and per crawler (F5's question).
  - **The results** go to `docs/life-model.md`. A surprise becomes a finding.
  - **Re-run** after L0.6 and at the gate, beside this baseline.
- [ ] **L0.4 One goal selector** (D3; F1) [Possibly Complex: the hot path of every decision; golden unchanged]
  - `GoalSelector.Rank`, with both evaluators calling it, and the trace lines moved with it.
  - `PlanExecutionValidator` and every test pass unchanged. Golden reproduces byte for byte, or the step doesn't land.
  - **Zero allocations:** the existing allocation checks, or a validator in the style of the planner's, prove none in the hot path.
- [ ] **L0.5 The motive model** (D2) [Possibly Complex: every priority passes through it; golden unchanged]
  - **The code:** `MotiveModel`, `LifeTuning` in `EconomyTuning`, `Urgency`, and `BasePriority` (renamed). The base-and-weight in `DecisionSummary`.
  - **The names test:** nothing outside the model calls `BasePriority`, except the validators that pin a goal's number.
  - **Mechanism validators:** a weight of 1 leaves every choice as it was; a weight set in a test tuning changes a choice; an unweighted goal ignores family weights.
  - **AGENTS.md:** the motive model in the cognition paragraph, and a recipe, "Adding a goal".
  - Golden reproduces byte for byte.
- [ ] **L0.6 The fallback** (D4; F2) [Possibly Complex: a behaviour change; golden re-recorded in its own commit]
  - **Its commits:** the fallback, then golden re-recorded in its own commit.
  - **The checks:** the gate seeds' day holds; every DoD keeps its seed or is re-picked in its own commit, with the reason; the soak's bands are measured and any move goes to the producer by exception.
  - **Re-run** L0.3's measurement, and record plans not found and wandering's share before and after.
- [ ] **L0.7 Commitment** (D5; F3) [Possibly Complex: cognition; golden unchanged at 0]
  - **The rule:** the goal whose plan just ended reaches the next evaluation through the cognition job, unsaved.
  - **Its value:** if L0.3 measured switching that matters, its value is set here, with golden re-recorded in its own commit. Otherwise it stays 0 and plan 2 sets it.
  - **The check:** a validator that a commitment set in a test tuning keeps a crawler on its goal against a near-equal rival.
- [ ] **L0.8 The life record, format 15** (D6) [Possibly Complex: saved state]
  - **The record:** `CrawlerLife`, as `AgentState.Life`, written and read in `CaptureRecord` and `RestoreRecord`, and copied to `AgentContext.Life`.
  - **The save:** format 15. The record's size is pinned beside the traits record's, with the round trip in `SessionCaptureValidator` and `SessionRestoreValidator`.
  - **The world hash** mixes it only when it isn't empty, so golden is unchanged.
  - **AGENTS.md:** "Who a crawler is living: the life record", beside the traits record's section.

### L1. The mind (5–7 days; L1's wants are plan 2's)
**Complexity:** Very Complex as a phase by bundling (call sites across the core, a rule cognition reads, a behaviour change, art and Gum). Split into eight steps, each Basic or Possibly Complex.
- [ ] **L1.1 The thought table** (D7) [Basic: a table with its validator and names test, as `DeedTable`; nothing reads it yet]
  - `Thought`, `ThoughtDefinition`, and `ThoughtTable` with D7's rows.
  - `ThoughtsValidator`: words, values, durations and stack limits within bounds.
  - `ThoughtNamesTests`: code names a thought only in the table and at the sites listed there, and every row is thought somewhere.
  - AGENTS.md: a recipe, "Adding a thought".
- [ ] **L1.2 Thinking** (D7) [Possibly Complex: call sites across the core, and saved state; golden re-recorded for the hash's coverage only, in its own commit]
  - **The entry:** `AgentState.Think`, with stacking, replacement, lazy expiry and the subjects' table.
  - **The sites:** D7's, including the deed hook in `AddDeed`, all on the main thread.
  - **Observed:** `SimulationManager.Thought` (the crawler, the thought), and `OutpostWatch` tallies thoughts by row.
  - **Golden** moves only because the hash now mixes written records (nothing reads thoughts yet). Re-recorded in its own commit, with the reason.
  - **The round trip** carries live thoughts and their subjects across a save mid-day.
- [ ] **L1.3 Mood** (D8) [Possibly Complex: a rule cognition reads; measuring]
  - **The code:** `MoodRules`, the `MoodTerms` table and `MoodBand`.
  - **The validator:** terms within bounds, the bands ordered, and the rule pure (the same inputs at the same tick give the same mood).
  - **The measurement:** `MeasureMoodOnTheGateSeeds`, explicit, three days, two runs as L0.3's. It reads mood's spread by band per resident-day, how long each thought lives and stacks, and mood around a death on the guild player's run.
  - **Tuning:** D7's values and D8's bands are set from it. Results go to `docs/life-model.md`.
  - **Golden unchanged:** nothing reads mood yet.
- [ ] **L1.4 What mood changes** (D9; F6) [Possibly Complex: a behaviour change and the soak's bands]
  - **The commits, in order:**
    1. frustration onto the day's pace (F6), with golden re-recorded in its own commit;
    2. mood's frustration terms and weights by motive, with golden re-recorded again in its own commit.
  - **The checks:** the gate seeds' day holds; every DoD keeps its seed or is re-picked with the reason; tantrums a day are measured before and after.
  - **The soak's bands:** measured, and any move goes to the producer by exception.
- [ ] **L1.5 The day log and the mind, as models** (D10; F5) [Possibly Complex: presentation models and the chronicle's words about one crawler]
  - **The models:** `DayLogModel`, `MindModel` and `LifeTexts` (thoughts' and bands' words, Latin-1 only), read through `DashboardContext`.
  - **F5's decision** from L0.3's count: the ring as it is, or a per-crawler window saved in the chronicle's block (a saved-state change inside format 15).
  - **The check:** presentation tests for each model, in `Simpiens.Client.Tests`.
- [ ] **L1.6 Faces: happy, low and grieving** (D10) [Possibly Complex: art; the people sheet looked at, and the art pins re-recorded in their own commit]
  - **The code:** `Mood`'s three values, `MoodSigns`, `MoodRule`'s order, `PortraitKit`'s features, and the mood line's reason.
  - **The checks:** `MoodRule`'s tests gain the new moods' thresholds and their way back. Look at the people sheet, then re-record the art pins.
- [ ] **L1.7 The dossier's tabs** (D10; Q7; F4) [Possibly Complex: Gum UI; renders re-recorded in the pinned image after a look]
  - **The code:** the tab row, and Crawler, Mind and Today in `DashboardPanels`.
  - **The checks:** the window-size test confirms the dossier fits at every offered size from 720 rows. Look at the renders, then re-record them in the pinned image (`scripts/test-client-in-container.sh --record`), committed on their own.
  - **The producer's look** at L1 follows this step.
- [ ] **L1.8 The DoD: `MoodFollowsTheDay`** [Possibly Complex: a live scenario]
  - **The gate seeds' day, nothing injected:**
    - every resident thinks "a good meal" and "slept in a bunk", and its mood is higher after each than before;
    - every resident's day log holds its meals and its sleep.
  - **A death:** on the seed and guild player that L1.3's measurement shows bring one (the clears' scripted player, as `TheOutpostRemembers` ran):
    - the dead crawler's friends think "{0} died";
    - their moods fall to Low or below, and their faces show grieving;
    - their day logs say why.
  - **A save mid-day** loads with every live thought, and the moods match.

### Plan 1's gate and close-out (1 day)
**Complexity:** Possibly Complex: measuring.
- [ ] **C.1 The gate** [Possibly Complex: measuring]
  - **The tests:** every test, the golden check, the gate seeds' day, and the client's tests in the pinned image.
  - **The soak:** its bands on the final build.
  - **Measurements:** L0.3's and L1.3's, re-run.
  - **Cost:** `PopulationScalingProbe` at 12, 24 and 48 crawlers, beside the same probe on the branch's starting point, measured in a quiet sitting (the fallback's plans, thoughts and mood).
- [ ] **C.2 Close-out** [Basic: written from lists this plan holds]
  - **The docs, in order:**
    1. the roadmap first, then the epic's status and §4;
    2. decisions to the epic's §0, findings to its §7, and numbers to `docs/life-model.md`;
    3. the focus file, and a summary in `.agents/architecture_history.md`.
  - **Delete this plan.**
  - **Open the PR** into `main` once #27 has merged.

## 3. What plans 2 and 3 take
Drafted when the plan before merges, from the epic's §2.4 and §2.5.
- **Plan 2, Wants, fun and the tavern:**
  - **L1's wants:** the table, three slots in the life record, `FulfilWant`, and the DoD `AWantComesTrue`.
  - **L2:** fun, activities, venues as map data, Recreate, variety, curiosity (Q8), commitment's value, the tavern room, the tavern as the player's building (Q10), thought icons, and the DoD `AnEveningAtTheTavern`.
  - **The producer's look:** an evening at the tavern.
- **Plan 3, Rhythm and the gate:**
  - **L3:** routines and chronotypes, the weights by family and hour, satiation, weariness, the day's intention, rest days in safe zones, Q11's switch, and the DoD `ADayOffAfterAWindfall`.
  - **LG1:** `LifeOneTogether`, the soak's bands, scaling, and the producer's play session.

## 4. Risks (this plan's; the epic's §6 holds the rest)
- **The refactor moves golden by a float's last bit.** D2's order of operations is exact. If golden moves anyway, the step stops and the difference is traced with `WorldStateHash`'s per-part descriptions. It never lands as a re-record.
- **The fallback changes many choices at once.** L0.3's baseline and its re-run show how many. If the gate seeds' day breaks, the fallback's depth drops to 1 before anything is retuned.
- **Thoughts tuned too strong** make moods swing and tantrums spread. L1.3 tunes them before L1.4 lets mood act, and L1.4 measures tantrums a day.
- **The dossier's tabs crowd the header.** The tab row takes one plaque's height. If that doesn't fit at 720 rows, the mood line moves into the Mind tab.

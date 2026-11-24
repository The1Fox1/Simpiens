# Epic LIFE-1: The Mind and the Day

- **Status**: ACTIVE (2026-11-23), at the producer's go: "Proceed with development of Life-1". QUEUED from the same day, drafted at the producer's word with its first plan.
- **Series**: the life core, Epic LIFE-1 to LIFE-3, all complete before Epic 12 (the producer, 2026-11-23; ROADMAP Track Rules, rule 1). This epic is the first: the proposal's L0 to L3. LIFE-2 takes L4–L6 (talk, gifts and bonds) and LIFE-3 takes L7–L8 and the series' gate (L9). Milestones keep the proposal's names, so an L-number means the same thing in every epic of the series.
- **Plans**: three, one PR each (2026-11-23):
  1. **Groundwork and the mind**: L0, and L1's thoughts, mood, day log and the dossier's Mind and Today. Under way: [`.agents/life1_mind_plan.md`](../../.agents/life1_mind_plan.md), on `feat/life1-mind`.
  2. **Wants, fun and the tavern**: L1's wants, and L2. Drafted when plan 1 merges.
  3. **Rhythm and the gate**: L3, and LG1. Drafted when plan 2 merges.
  - **Why L1 is split:** wants need something to want. At L1 alone, the only wants the code could fulfil are today's needs under a new name ("eat", "buy a pickaxe"), and "a want arises and is fulfilled" would pass without meaning anything. With L2's venues they can be a tavern supper, a walk in the Garden or an evening with a friend. Both new goals of the epic (the want's and Recreate) then enter the motive model together, in plan 2. Thoughts and mood stand alone: they read events that already happen, so they fit plan 1 beside the refactor.
- **Track**: Track A: Core Simulation & Cognition Engine. Its UI is the KNI client's and is built here, as in Epic 11.
- **Milestone**: Production Milestone 6, A Living Outpost (ROADMAP §3).
- **Source**: the proposal [Living Crawlers: a proposal for a simulation that feels alive](https://claude.ai/artifact/GqJoB2w3A7SM1atjktCUhq) (Claude Doc, 2026-11-15): its L0–L3, its pillars, and the questions Q1–Q6 that bear on them. Its staging put L0 inside Epic 11's groundwork; Epic 11 closed without it, so all of L0 is here.
- **The numbers:** `docs/life-model.md`, created at L0.1: the life core's rules and measurements, kept up to date as each milestone lands, as `docs/skills-model.md` was for Epic 11.
- **Preceding**: [Epic 11](epic-11.md). **Builds on**:
  - **Cognition:** the needs (`AgentNeeds`: hunger, energy, social), personality (`AgentPersonality`, five traits, read as `Considerations.TraitFactor`) and the GOAP domain (`OutpostGoapDomain`: 21 goals, 48 of 64 planner facts);
  - **Frustration:** frustration, which weighs against compliance and shows on the face (no rule starts a tantrum from it: F8), and the memory ledger with its `Grieved` event (Epic 10 Part 3, K5.5);
  - **Epic 11:** the traits record (A3) and its pattern for a fixed record with spare bytes; passions, which weigh a node's score;
  - **The day:** `DayClock`, `DayTuning`, [`docs/time-model.md`](../time-model.md) and its finding that crawlers earn only what they spend (D25 unmet);
  - **The workshops:** the build-and-hire pattern (`BuildStationCommand`, `HireArtisanCommand`), which the tavern follows;
  - **What the player sees:** the chronicle (61 event types, a ring of 1,024 events), portraits and `MoodRule` (Epic UI-Beta B3), the dossier.
- **Following**: Epic LIFE-2, then Epic LIFE-3, then Epic 12, the descent (Milestone 7).
- **Complexity** ([rubric](../../.agents/rules/complexity-grading.md)): **Very Complex** as an epic. It is large, and its first refactor touches goal choice, which every behaviour passes through. It adds saved state, a map room and a new building across core and client. Each milestone is stamped in §2.4, and the plans split the Very Complex ones.

---

## 0. Producer intent and decisions

**The request (2026-11-23):**
> Look at [the proposal] and write up our first Epic in a series of sim focused epics. This epic should cover anything that is missing for L0, and go up to L3. You will then write up the first implementation plan to cover the first set of work (whether it's all of L1 or only part of L1 is up to your analysis). This epic and all epics for the life core will need to be completed before Epic 12. Make sure the roadmap is updated.

**The brief behind the proposal (2026-11-15):**
> As we begin to flesh out our simulation beyond the basics we need to bring our agents to life, or at least convince the player they are alive. [...] A crawler's life should involve more than just going to mine, selling, sleeping, eating and repeat. They should have loose schedules, downtime where they prioritize socializing with friends or going to a tavern for fun, some crawlers may not want to be crawlers [...]

The brief's scene follows Crawler A's day off. He finishes a quest and rests the next day. He shops at the market and chats with Crawler C. He spends the afternoon at the tavern. At dusk he gives Crawler B a gift, and they become lovers. "Crawler A didn't do any fighting today, but this day was more meaningful than any other day for him."

**What of the scene this epic makes happen:**
- the windfall that earns a rest, and the rest day itself (L3);
- the morning at the market and the evening at the tavern (L2);
- A's face and dossier telling the player how the day went, and why (L1).

The chat that teaches him something (L4), the gift (L5) and the yes (L6) are LIFE-2's.

### The questions
Every row is a default that stands by exception. The proposal's questions keep their numbers where they reappear ("proposal Q6"). Its Q1 is answered by the request above. Q7–Q12 are LIFE-2's and LIFE-3's (§5).

| # | Question | Default | Alternative |
| :-: | :--- | :--- | :--- |
| Q1 | **The series** | Three epics before Epic 12: LIFE-1 (L0–L3), LIFE-2 (L4–L6: talk, gifts and bonds, the scene), LIFE-3 (L7–L8, and L9 as the series' gate) | Two epics; or L8 (creatures, fauna, NPCs), which is independent of the rest, after the descent |
| Q2 | **Names and milestone** | `Epic LIFE-1` to `LIFE-3` in Track A, between Epic 11 and Epic 12, which keeps its number. Milestone 6 becomes "A Living Outpost", and Floor Relocation becomes Milestone 7 | Renumber: the life core becomes Epics 12–14 and the descent Epic 15 |
| Q3 | **The motive model first** (proposal Q2) | Yes: L0, behaviour-neutral, proven by the golden hashes. Each goal keeps its own number as its *base*. The model owns what every goal shares: the weights (mood, routine, satiation, wants, the day's intention) and commitment | Rewrite all 21 formulas as urgency × opportunity now: the golden hashes are at risk, and the player sees nothing for it until a goal needs retuning |
| Q4 | **When the best goal can't be planned** | Try the next two before falling through to wandering (L0, its own behaviour change) | Keep today's fall-through |
| Q5 | **The life record** | One fixed record of 512 bytes per crawler beside the traits record: save format 15, the one raise for the whole series. LIFE-2 and LIFE-3 fill its spare bytes | A raise in each epic |
| Q6 | **What mood changes first** | Frustration (a low mood feeds it, a high one eases it) and goal weights by motive (company and fun pull harder when low; income a little weaker). How well work is done doesn't change | Also work pace, generosity and openness to others (LIFE-2 reads mood for romance either way) |
| Q7 | **Where Today and Mind are shown** | Tabs in the dossier under its header: Crawler (today's sections), Mind, Today. The dossier has no height to spare at 720 rows (Epic 11's F16) | A window opened from the mood line, as the technique tree opens from the techniques' line |
| Q8 | **Curiosity** (proposal Q3) | A sixth trait at L2, held in the life record and drawn on its own salt, so no earlier draw moves | None: read curiosity from the five |
| Q9 | **Comfort** (proposal Q4) | Not a need. Bunks and venues give thoughts | A fifth need |
| Q10 | **The tavern** (proposal Q6) | The player's third building. The player builds it at a site on the map and hires a keeper on an artisan's contract. Its menu comes from the caravan, and the player sets the prices | The outpost's: drinks are a sink, and the player has no lever |
| Q11 | **An outpost rest day** (proposal Q5) | Not by default; a tuning switch at L3 | Every seventh day |

---

### Plan 1's decisions (Groundwork and the mind)
Proposed in [plan 1](../../.agents/life1_mind_plan.md) as D1–D10. Each moves here when its step lands. The producer answers by exception.

---

## 1. Goal
**The player promise:** watch a crawler live a day, not just work one. It wakes late after a windfall and browses the market. It spends the evening at the tavern with its regulars. Its face and dossier say how the day went, and why.

What Epic 11 leaves undone (the proposal's "why life still loops"):
- **Nothing pulls once work is done.** `EarnIncomeGoal` stops at the comfortable purse (80g). Past it, a crawler idles or wanders until a need bites: 13–38% of the day, depending on the economy's tuning ([time model](../time-model.md)).
- **Gold has two uses: survival and gear.** A windfall changes nothing about the next day, and crawlers earn only what they spend, about 40g a day, so D25's 6–8 hours of work stay unmet.
- **A crawler has no memory of how life has been going.** Frustration falls by 1 every real second, about 42 a game hour, so grief's 25 points are gone within the hour. A friend's death leaves nothing on the face by evening.
- **Every goal sets its own priority.** Balance lives in 21 formulas. Anything new that should weigh every goal (an hour of the day, a mood, a day off) would have to be threaded through each of them.

## 2. Design

### 2.1 The loop
After LIFE-1, a crawler's day runs like this:
1. **It wakes and picks what today is for:** a work day, a rest day, a training day or a want's day. It rolls once, weighted by how full its purse is, how weary it is and what it wants (L3).
2. **Its routine weights the hours.** Work pulls hardest in its working block, leisure after it, and sleep toward its bedtime. A lark mines at dawn and an owl sits by the hearth at midnight (L3).
3. **Needs and wants pick the goal.** One rule scores every goal: the goal's own number times the weights all goals share (L0).
4. **Free time has somewhere to go.** Venues advertise what they offer, and the crawler picks by need, taste, variety and friends: the tavern, the hearth, the market, the Garden (L2).
5. **What happens leaves a thought:** a good meal, a night in a bunk, a friend rescued, a friend lost, a bounty paid, an evening of dice. Its mood is its needs plus its thoughts (L1).
6. **Mood bends the day a little.** An unhappy crawler seeks company and fun and gets frustrated sooner. The face, the mood line and the dossier say why (L1).
7. **A want is a goal with a reason:** a tavern supper, a walk, a friend's company. Fulfilled, it leaves a good thought; expired, a small bad one (L1, built in plan 2).

**The new verbs:** build the tavern, hire its keeper and price its menu. Crawlers do the rest.

### 2.2 Positions (the proposal's pillars, as they bind this epic)
- **The illusion is the product.** Every milestone ships its player-facing half: faces and the dossier's tabs with L1, the tavern's evenings with L2, "Today: a day off" with L3.
- **Wants, not chores.** A crawler goes to the tavern because it wants fun or company, not because it is 18:00.
- **Rhythm, not a timetable.** A routine multiplies; it never commands. Two crawlers with the same routine still have different days.
- **Average is the default.** Most crawlers are moderate in everything. A workaholic or a hermit is rare enough to notice.
- **The simulation's rules hold:** determinism on `SimRandom`, a single writer, typed intents and commands, saves in their owners' blocks, and the golden hashes (ROADMAP §1A).
- **Cheap enough for every crawler.** Thoughts expire lazily, the intention is drawn once a day, and nothing scans every pair every tick. Nothing allocates in the tick.

### 2.3 Architecture and rules
- **A1. One priority rule: the motive model.**
  - Every goal declares the **motive** it serves (hunger, rest, company, income, gear, growth, duty, aid, leave; plan 2 adds fun and a want) and its activity **family** (work, leisure, social, rest, or unweighted). A goal may name its family by context: loot gathered under a clear's orders is unweighted, as orders are, not work.
  - Priority = the goal's **base** × its **weight** + **commitment**.
    - The base is the goal's number today (its old `CalculatePriority`).
    - The weight multiplies the shared factors, each exactly 1 until its milestone sets it: mood by motive (L1), the want (plan 2), the routine by family and hour, satiation on work, and the day's intention (L3).
  - A new goal computes its base from its motive's shared urgency (`Urgency.Of`) and its own opportunity. It never brings new constants of its own.
  - **Unweighted** goals (hunger, orders, rescue, leaving) are never scaled by the routine or the intention, so survival and orders keep their precedence.
- **A2. One goal selector.** `GoalSelector` replaces the two argmax loops written today (`AgentCognitionService` and `GoapCognitiveEvaluator`).
  - It ranks the valid, unsatisfied goals, keeps the top three for the "why" (`DecisionSummary`) and breaks ties to the earlier goal, as today.
  - When the best can't be planned, it tries the next two before Tier 3's wandering.
  - It keeps no state between calls and allocates nothing.
- **A3. One life record.** `CrawlerLife` sits in `Simulation/Life/` (cognition reads it, and may read nothing of `People/`). It is 512 bytes in the crawler's record after the traits record, and saved whole.
  - **Format:** 15, the series' one raise, at L0. Its size is pinned.
  - **Holds:** thoughts and the subjects they name; from plan 2, wants, fun, variety and curiosity; from L3, the day's intention, weariness and the last windfall. Spare bytes are left for LIFE-2 (taste, keepsakes) and LIFE-3 (an aspiration).
  - **Copied** for cognition as `AgentContext.Life`.
  - **Subjects:** an `EntityId` is 16 bytes, so thoughts name their subjects through a table of four in the record.
  - A field added later reads 0 as "none", so a save made before it loads unchanged.
- **A4. Every new system is a table:** thoughts, mood terms, wants, activities, venues, chronotypes and intentions.
  - Ids are never reused, and a names test guards each table, as `DeedNamesTests` does.
  - `AGENTS.md` gains a recipe for each kind of row ("Adding a thought", "Adding a venue", "Adding a goal").
- **A5. Thoughts are thought where things happen,** on the main thread, through one entry (`AgentState.Think`), as deeds are counted (Epic 11's A7). They are never rebuilt from the chronicle.
  - Mood is a pure function of needs, health and thoughts at a tick, so the main thread and cognition agree. It is derived and never saved.
- **A6. Venues are map data,** as stations are. What a place offers lives in the place (The Sims' smart objects).
  - One goal, Recreate, scores the activities a crawler knows of.
  - Which venue or friend it picks is chosen by utility, as `ResourceTargeting` chooses a node, never by a planner fact per venue.
- **A7. Routines are computed, not saved:** a pure function of the seed, the crawler's id, its personality and the day. The day's intention is drawn once and saved.
- **A8. Precedence holds,** in today's order:
  1. reflexes and rescue;
  2. orders (Epic 8's compliance; an artisan's shifts are its working block);
  3. strong wants;
  4. the routine.
  - A rest day never sends a crawler into a hazard or a lair's reach unless it goes to rescue someone.
- **A9. Determinism and cost.** Each roll has its own salt on `SimRandom` (curiosity's draw, the day's intention).
  - Thoughts expire when read, with no per-tick sweep.
  - The motive model's weights are pure functions of the context.
  - `PopulationScalingProbe` measures the cost at each plan's gate.
- **A10. Views decide nothing.** The day log is the chronicle's lines and the day's thoughts about one crawler, told about that crawler. It stores nothing the chronicle and the life record don't already hold. The dossier reads models, and frames carry what the map draws.
- **A11. Numbers in their places:**
  - a thought's value and duration in its table row;
  - paces (fun's fall, weariness) in `DayTuning`;
  - prices and the tavern's terms in `EconomyTuning`;
  - the motive model's weights in one `LifeTuning` inside the session's economy.
  - The rules and their measurements are in `docs/life-model.md`.
- **A12. Planner facts are scarce:** 48 of 64 are in use. The epic adds at most three (Recreate's done, a want met, a venue's seat), each with a reason, under the core refactor's R1 guard.

### 2.4 Milestones

| Milestone | What the player sees | Core (engine-free simulation) | Client (KNI) | Complexity | Live scenario |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **L0. Groundwork** | Nothing new | Goals declare a motive and a family; the day measured per crawler (time by family, plans not found, switches at plan ends, who meets whom, chronicle lines a day); one goal selector; the motive model, behaviour-neutral; the fallback, its own behaviour change; commitment (built at 0 unless the measurement shows switching; set at L2); the life record, format 15 | — | Very Complex (bundling; split into eight steps in plan 1, none supervised) | Golden unchanged through the selector and the model; the gate seeds' day holds after the fallback |
| **L1. The mind** | Faces for happy, low and grieving, with a reason; the dossier's Mind and Today; from plan 2, wants | The thought table and the entry that thinks them; mood and its terms; what mood changes; the day log; wants (3 slots, a table, a goal: plan 2) | New faces; the mood line's reason; the dossier's tabs | Very Complex (bundling; split across plans 1 and 2) | `MoodFollowsTheDay` (plan 1): moods rise with a meal and a night in a bunk, and fall for a friend's death, with the reasons in the day log. `AWantComesTrue` (plan 2): a want arises, is pursued at a venue and fulfilled, and leaves its thought |
| **L2. Fun and the tavern** | Evenings at the tavern; dice; walks in the Garden; browsing the market | Fun; the activity table; venues as map data; the Recreate goal; variety; curiosity; the tavern room on the map; the tavern as the player's building, with a keeper and a menu; commitment's value | The tavern's art and the building in the dashboard; poses at venues; thought icons over crawlers | Very Complex (bundling, a map room and a new building; split in plan 2; the tavern's building supervised unless it splits below that) | `AnEveningAtTheTavern`: on the gate seeds' day, most residents spend leisure at a venue, ambient wandering falls by half, nobody starves, and the tavern's takings and upkeep are in the ledger |
| **L3. Rhythm** | Dawn miners, owls at midnight; days off; "Today: a day off" | Routines and chronotypes; the weights by family and hour; satiation (the purse against its target, a recent big win, weariness); the day's intention; a rest day keeps to safe zones | The intention in the dossier and the day log's first line; the outpost clock's pulse | Very Complex (bundling; split in plan 3) | `ADayOffAfterAWindfall`: a resident paid a large bounty rests the next day, staying in safe zones; three-day work hours stay in the soak's band, and D25's hours are measured |
| **LG1. The gate** | LIFE-1 played through | Every scenario together with a save mid-day; the gate seeds; the soak's bands with days off and the tavern's takings; scaling at 12, 24 and 48 crawlers | The producer's play session | Possibly Complex | `LifeOneTogether`: every scenario above on one seed, saved mid-day and played on |

**Order:** L0 to L3, then the gate.
- **The motive model first:** every later milestone weighs goals through it.
- **The mind before fun:** every later milestone writes thoughts, and fun's evenings need a face to show on.
- **Venues before rhythm:** a day off needs somewhere to go (the proposal's order).
- **Wants with venues** (plan 2), for the reason in the header.

**The producer's looks:** L1 (faces, the mood line, the dossier's tabs), L2 (an evening at the tavern), L3 (the outpost's pulse and a day off); then the play session at LG1.

### 2.5 What the later milestones hold
Plan 1 holds L0 and L1's mind in detail. This section is enough to draft plans 2 and 3 from. Their decisions are proposed in those plans, and the numbers are starting points for tuning.
- **Wants** (L1, plan 2):
  - **The table:** a want row names its trigger (spare gold, low fun, a friend not seen for a day, a venue not yet tried), its goal's target kind (a venue's activity, a friend, a good) and its lifetime in game days.
  - **Arising and ending:** up to three are held in the life record. They arise on waking and at their events, are pursued by one `FulfilWant` goal whose motive is the want, and leave a thought when fulfilled or a small bad one when they expire.
- **Fun and venues** (L2, plan 2):
  - **Fun:** falls about 3 points a game hour while awake, held in the life record.
  - **Venues** are `VenueDefinition`s in `MapDefinition`: a key, a sector, seats, opening hours and the activities offered. The hearth, the stall, the Garden and the guildhall yard are the first; the tavern is a new room off the hub.
  - **Activities** are table rows: what they fill per game hour, how long they last, their cost, their variety group, whether they need company (dice needs two to four), the trait that leans toward them and the pose the client draws.
  - **Recreate** scores need × fill × personality × variety × distance × friends present.
  - **No leisure in danger:** no venue stands in a hazard sector or a lair's reach.
- **The tavern** (L2, plan 2): built through the station pattern at a tavern site in the hub. A keeper is hired on an artisan's contract, and the menu (ale, stew) is bought wholesale from the caravan. Prices are set by the existing price command's pattern. Its takings are the treasury's, through the ledger.
- **Rhythm** (L3, plan 3):
  - **The routine:** a chronotype (lark, average or owl; most are average) and a working block of about 5–9 hours by diligence, starting by chronotype. Then a leisure block, meal anchors and a bedtime pull. All of it is computed and weights the families by the hour.
  - **Satiation** on work: the purse against a personal target (the comfortable purse by greed, plus what wants are saving for), an "earned a rest" thought after a big payout, and weariness built by hours worked and cleared only by a day off.
  - **The day's intention:** drawn on waking on its own salt.
  - **Orders still come first:** an artisan's shifts are its working block, and an expedition's muster outranks the routine.

## 3. Definition of Done

Done means crawlers live a day, in the live outpost, with nothing injected (ROADMAP §1B). A scripted player acts only through typed commands.
- **Mechanism:** validators for each table (thoughts, mood terms, wants, activities, venues, chronotypes, intentions), the motive model (a weight changes a choice; weights of 1 change nothing), the selector's fallback, the life record's round trip, thoughts' stacking and expiry, mood's rule, Recreate's scoring, the tavern's commands and its ledger.
- **Live, headless:** the scenarios in §2.4's last column, on the real outpost: `MoodFollowsTheDay`, `AWantComesTrue`, `AnEveningAtTheTavern`, `ADayOffAfterAWindfall`, and every scenario together at the gate (`LifeOneTogether`).
- **Golden:** unchanged through L0's refactor (the selector and the model). Every behaviour change re-records golden in its own commit.
- **The economy:** the ledger balances with the tavern's takings and its keeper's wage. The soak's bands are re-measured at L1 (mood's weights), L2 (the tavern) and L3 (days off), beside D25's work hours; the producer sets the bands. Leisure spending should add work hours: L3 measures whether it does.
- **The producer:** looks at L1, L2 and L3, and a play session at LG1.
- **Docs:** the roadmap first, then this file's status, then the focus file; `architecture_history.md`; `docs/life-model.md`; the economy model (the tavern); `AGENTS.md` (the motive model's place in cognition, and each table's recipe).

## 4. Acceptance checklist (delivered work only)
Nothing yet.

## 5. Deferred and out of scope
| Item | Where it goes |
| :--- | :--- |
| Conversations with a topic, social facts, pair chemistry (L4) | Epic LIFE-2 |
| Personal goods, taste, keepsakes, gifts (L5) | Epic LIFE-2. Taste and keepsakes take the life record's spare bytes |
| Relationship stages, romance and the romance check, what a bond changes (L6); the proposal's Q7, Q10, Q11 and Q12 | Epic LIFE-2 |
| Aspirations, the crawler who would rather be an artisan, petitions (L7) | Epic LIFE-3 |
| Creatures with a day, feeding, harmless fauna, the caravan's merchant, drifter archetypes (L8); the proposal's Q8 and Q9 | Epic LIFE-3 |
| The series' gate (L9): how often the scene emerges across seeds | Epic LIFE-3 |
| The morning card ("Yesterday at the outpost"), bond lines, the follow camera | Epic LIFE-2, with bonds (the proposal's §11) |
| A comfort need; an outpost week (Q9, Q11) | Not planned; options |
| Families and children; fights between crawlers; festivals and seasons | Later (the proposal's "left out") |
| Rumours written by a language model | Epic N, which the day log and the chronicle feed |

## 6. Risks
| Risk | What we do |
| :--- | :--- |
| The refactor moves golden by a float's last bit | The base is computed first and multiplied by the weights left to right. A float times exactly 1 is itself, and commitment is added only when it isn't 0. L0.4 and L0.5 must reproduce golden byte for byte, or they don't land |
| The fallback costs planner time on every failed plan | At most two more plans per evaluation; L0.3 counts plans not found first, and the plan's gate measures `PopulationScalingProbe` before and after |
| Mood and days off move the economy: work hours, upkeep, the soak's bands | Numbers in `LifeTuning` and `EconomyTuning`. Re-measured at L1, L2 and L3; the producer sets the bands. The producer's word for Epic 11 stands: "Economy flukes are tolerable until they completely ruin our testing scenarios" |
| Many behaviour changes churn golden | Each re-records golden in its own commit; L0's refactor is neutral |
| Crawlers flicker between leisure options | Commitment in the motive model; `ThrashingTests` in the client; L0.3's switch count, re-run at L2 |
| The day log loses the morning by evening: the chronicle keeps 1,024 events outpost-wide | L0.3 counts chronicle lines a day; if a day overflows the ring, the day log keeps its own per-crawler window, decided at L1.5 (F5) |
| The tavern is a new building and a new room on the map | Built by the station pattern (precedent); the room follows `Floor1Layout`'s recipe; renders and the art review sheet looked at; split in plan 2 |
| Scope creeps toward The Sims' depth | The first pillar: what the player can't see is cut. Everything is a table; options stay options |
| Planner facts run out | Targets by utility; at most three new facts in the epic |

## 7. Findings
Read from the code while drafting (2026-11-23), each with its owner.
- **F1. Goal choice is written twice.** `AgentCognitionService.EvaluateAsync` picks the best goal for the live game. `GoapCognitiveEvaluator.EvaluateAsync` repeats the same argmax, and only `PlanExecutionValidator` uses it. Owner: L0.4 [Possibly Complex], one selector for both.
- **F2. A goal that can't be planned falls through to ambient wandering.** Nobody has measured how often. Owner: L0.3 measures it [Possibly Complex], and L0.6 tries the next goals [Possibly Complex].
- **F3. Commitment already exists within a plan.** Goals are not re-scored while a plan runs (only reflexes cut it short), so the proposal's flicker can only happen at a plan's end. Owner: L0.7 builds the rule at 0 unless L0.3 measures switching [Possibly Complex]; L2 sets its value when leisure options compete.
- **F4. The dossier has no height to spare at 720 rows** (Epic 11's F16). Mind and Today won't fit as sections. Owner: L1.7, tabs (Q7) [Possibly Complex].
- **F5. The chronicle is one ring of 1,024 events for the whole outpost.** A day log read from it holds only what the ring still holds. Owner: L0.3 counts lines a day [Possibly Complex]; L1.5 decides [Possibly Complex].
- **F6. Frustration runs in physical time:** −1 every second (`AgentState`'s drives), about 42 a game hour. Every other need is paced in game time through `DayTuning`. A low mood that "feeds frustration" in game hours would be drowned by it. Owner: L1.4 judges whether frustration moves onto the day's pace, as its own behaviour change [Possibly Complex].
- **F8. Frustration starts no tantrum.** The proposal's "a very unhappy one breaks, through today's frustration path" has no path to ride: tantrums come only from a contested node lost to a rival and an artisan's breakdown, and `MentalBreakEvaluator` reads hunger, energy and danger. Frustration weighs against compliance (`ComplianceUtility`) and sets `IsThreatened`, a fact no action reads. Owner: L1.4 [Possibly Complex]: mood feeds frustration, so unhappy crawlers defy orders sooner; a break of its own is left to plan 2's wants or LIFE-2, by exception.
- **F7, resolved 2026-11-23. PR #27 (Epic 11's plans 3 and 4) was open** while the roadmap and the history called it merged, so plan 1's branch was first cut from `feat/epic11-masterworks`. #27 merged the same day (`d0d2d47`), and the branch was rebased onto `main` at the go.

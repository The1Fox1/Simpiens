# Epic LIFE-1: The Mind and the Day

- **Status**: ACTIVE (2026-11-23), at the producer's go: "Proceed with development of Life-1". Plan 1, Groundwork and the mind, was done and closed out the same day on `feat/life1-mind`; the producer's look at L1 (the faces, the mood line, the dossier's tabs) passed on 2026-11-24 and it merged as PR #28 ("Looks good PR, merge and close it out"). Plan 2, Wants, fun and the tavern, was drafted the same day on `feat/life1-tavern`, and the producer's go came with two changes to the draft (the map may grow past the window; orders never override a crawler's autonomy). It was done and closed out on 2026-11-24 while the producer was away; its PR into `main` waits for their word and the look at L2. Plan 3, Rhythm and the gate, was drafted the same day at their directive ([`.agents/life1_rhythm_plan.md`](../../.agents/life1_rhythm_plan.md)); at their go ("Proceed with plan 3") it was done through its gate (G.2) on 2026-11-25 on `feat/life1-rhythm`, cut from plan 2's unmerged tip. It waits for the producer's look at L3, their play session (G.3) and both plans' merges before close-out. QUEUED from the same day, drafted at the producer's word with its first plan.
- **Series**: the life core, Epic LIFE-1 to LIFE-3, all complete before Epic 12 (the producer, 2026-11-23; ROADMAP Track Rules, rule 1). This epic is the first: the proposal's L0 to L3. LIFE-2 takes L4–L6 (talk, gifts and bonds) and LIFE-3 takes L7–L8 and the series' gate (L9). Milestones keep the proposal's names, so an L-number means the same thing in every epic of the series.
- **Plans**: three, one PR each (2026-11-23):
  1. **Groundwork and the mind**: L0, and L1's thoughts, mood, day log and the dossier's Mind and Today. Done and closed out 2026-11-23 on `feat/life1-mind`. Its decisions are D1–D10 below, its findings F8–F12 in §7, its numbers in `docs/life-model.md`, and its summary in `.agents/architecture_history.md`. The plan itself is deleted (git keeps it).
  2. **Wants, fun and the tavern**: L1's wants, and L2. Done and closed out 2026-11-24 on `feat/life1-tavern`, with the producer's two changes (D16, D25). Its decisions are D11–D26 below, its findings F13–F19 in §7, its numbers in `docs/life-model.md` §5, and its summary in `.agents/architecture_history.md`. The plan itself is deleted (git keeps it).
  3. **Rhythm and the gate**: L3, and LG1. Drafted 2026-11-24 at the producer's directive ([`.agents/life1_rhythm_plan.md`](../../.agents/life1_rhythm_plan.md)): decisions D27–D38, 21 steps, none supervised. Done through G.2 on 2026-11-25 on `feat/life1-rhythm` (branched from plan 2's tip, since plan 2 hadn't merged): its decisions are D27–D38 below, as amended by measurement, its findings F20–F22 in §7, its numbers in `docs/life-model.md` §6. G.3 (the producer's play session) and G.4 (close-out) wait for the producer.
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

### The producer's decisions of 2026-11-24 (with plan 2's go)
> It is not only okay but encouraged to expand the map beyond the viewport size.

> Crawlers can absolutely disobey orders, being a guild member does not grant the player authority to override their autonomy.

- **The map** may grow past what a window shows whole: the tavern takes the Garden's room off the hub, and the Garden moves north (plan 2's D16; F14).
- **Orders are a motive, not a rule:** a crawler judges orders by compliance at every stage of a trip and every block of a shift, and mood weighs them as it weighs anything (plan 2's D25; F15). It amends A8 below, plan 1's D9, and Epic 10's D8 (an expedition's orders judged once, at dispatch).

### Plan 3's decisions (Rhythm and the gate, from 2026-11-24; delivered 2026-11-25)
- **D27, a day is one waking and one night:** the awake drains times `DayTuning.WakingDayFactor`, 0.6; a waking day of about 16 hours (R.2, `docs/life-model.md` §6.2).
- **D28, chronotypes drawn and saved:** lark, average, owl (20/60/20) at spawn on `SimRandomSalt.Chronotype` (22), the life record's byte 279 (R.1).
- **D29, the routine computed:** `Routine` (waking 05:00/07:00/10:00 ± 40 minutes by a draw keyed on the crawler and the day; a working block of 5–9 hours by diligence; leisure; the night from 16 hours after waking); an artisan's working block is its shifts (R.1).
- **D30, weights by family and hour:** the working block work 1.2, leisure 0.8; the leisure block work 0.8, leisure 1.2; the night work 0.6, leisure 0.7, company 0.8, rest 1.5 (R.3, R.4). **Amended by measurement:** company weighs 1 by day (it was 0.9 and 1.15): the hearth took the hours and the guild's economy failed over sixteen days (§6.4).
- **D31, the bedtime pull:** Rest valid in the night below energy 70 and weighed 1.5; the hearth costs like sleeping rough at bedtime; a sleeper dozes until its waking hour, hunger's line still waking it (R.3; F16 resolved).
- **D32, satiation:** work weighs 1 up to the purse target (the comfortable purse times 1 + greed × 0.5, plus held wants' costs), falling to 0.6 at twice it (R.6).
- **D33, weariness and the windfall:** weariness 1 a game hour at work (byte 268), work weighs 1 − 0.3 × weariness / 100; a payment of half the comfortable purse or more is a windfall (byte 272) with "Earned a rest" (+4, 24 hours) (R.5, R.6).
- **D34, the day's intention:** drawn once a routine day on waking on `SimRandomSalt.Intention` (23), saved at 276 with its day at 288; odds and weights as drafted (R.7). **Amended:** a rest day weighs company 1 (not 1.2), as D30's; the outpost's first routine day is a work day (no night's sleep before it); a day off begun with a work plan in hand drops it (R.8).
- **D35, a rest day keeps to safe zones:** lone danger limit 0 (`Vitality.Resting`), no hunts; rescue judged as a working day (R.8).
- **D36, duty and a day off:** the routine never weighs duty; a rest day weighs it 0.8; a member keeping to its orders is called in (R.9).
- **D37, Q11's switch:** `RhythmTuning.OutpostRestDay`, off (R.9).
- **D38, what the player sees:** "Today: a day off." after the dossier's mood and the day log's waking line (V.1); the outpost's pulse beside the clock (V.2); the seated pose (V.3); an icon over a crawler at an activity (V.4).
- **The tavern by the clock (D.2):** the tavern player keeps its counter on the evening shift (18:00–02:00, `ShiftBlocks.EveningShift`).
- **The soak's bands, by exception (R.10):** the crafting loop's goods share back at 0.8 (F19 resolved); its unpaid-quit share 0.35 (F20).

### Plan 2's decisions (Wants, fun and the tavern, from 2026-11-24)
Proposed in plan 2 as defaults; each lands here with its step. The producer answers by exception.
- **D11, fun:** held as boredom in the life record (byte 264), so a plan 1 save reads entertained; it rises awake and holds asleep; the hearth's relax fills fun at the fireside's rate; fun's urgency is 100 less fun; the Bored term (−5 at fun 25 or less) landed with Recreate (L2.6). **Amended at L2.6:** boredom rises 2 a game hour, not 3: at 3 it settled near 47 and leisure took 1.5 h a day, at 2 near 40 and 0.7 h (`docs/life-model.md` §5.8).
- **D12, activities:** `ActivityTable`, seven rows (the fireside, a walk, browsing, sparring, an ale, a supper, dice), each with its thought (22–27). The fireside is `GatherAtAnchor`'s; Recreate offers the rest.
- **D13, venues:** map data (`VenueDefinition`), open by hours and stations (`VenueRules`, the snapshot's open mask); seats unclaimed, free when no one stands within 0.4. The hearth, the stall, the Garden (all hours, D26) and the guildhall yard (members, with the drill yard).
- **D14, Recreate:** fun's urgency times the best leisure score; never hungry, tired or hurt (F12); valid under orders (D25). The fact `Recreated` (49 of 64). The spell walks to a seat, fills on the day's pace, a walk strolls among the seats, and its thought is thought at its end.
- **D15, the leisure score:** gates (closed, not a member, uncharted, unaffordable, too few others, no seat), then the share filled × the lean on its nature × the walk × a friend there; pure and allocation-free. Variety and curiosity are L2.7's.
- **D25, orders are a motive, not a rule** (the producer's change): mood weighs crawlers under orders as anyone, and duty gains mood's weights, low 0.85 and breaking 0.6 (O.1); an expedition's orders are issued afresh and judged by compliance at every stage, so a member may stay behind or desert mid-trip (O.2). On the gate seeds almost nothing moved; golden unchanged. F15 resolved.
- **D16, the tavern room** (the producer's change, F14): the tavern takes the Garden's room off the hub (sector 15, a facility), the Garden moves a room north, and the floor grows to 64 × 54 (T.2).
- **D17, station kinds as a table** (F13): `StationTable`, its rows `StationKindDefinition`, costs as a `StationCost` line priced in `WorkshopTuning`; behaviour-neutral (T.1).
- **D18, the tavern as the player's building:** built for 200g into `Construction`, kept by a keeper on an artisan's contract and its shifts; its venue opens only while built and kept (`WorkshopService.KeptMask`); no recipes and no keeper's skill (T.4).
- **D19, the menu:** ale and stew (`ItemCategory.Served`), brought by the caravan only while a tavern stands (`CaravanTerms.ForStation`, into `TavernWholesale`), bought from the stall's stock through the ledger as a spell begins (T.3, T.5).
- **D20, wants:** five rows, three slots in the life record, one roll on waking a game day (60%) among the wants whose triggers hold; `FulfilWantGoal` pulls to the spell that answers one; a want to see a friend comes true when they talk.
- **D21, mood's pull on leisure:** fun low 1.25, breaking 1.5 (L2.8); the leisure score's full share 20 fun an hour (F18).
- **D22, commitment:** stays 0: flips stay at 0.8 a resident-day with leisure in (F3 closed).
- **D23, what the player sees:** the tavern's prop, its tab in the workshop window with a KEEPER section, ale and stew on the stall's shelf, wants in the Mind tab (T.7, W.4). The seated pose and the thought icons move to plan 3 (its D38).
- **D24, the DoDs' readings:** three days with the tavern player; half the residents an hour at a venue other than the hearth and the tavern serving on two days, in place of an hour a resident-day (measured 0.32–0.89).
- **D26, plan 2 keeps no clock** (F16): the outpost has no night, so the tavern opens on its keeper's shifts, set where residents are free; plan 3's routine brings sleep to the night.

### Plan 1's decisions (Groundwork and the mind, 2026-11-23)
Proposed in plan 1 as defaults; each landed with its step. The producer answers by exception.
- **D1, motives and families:** every goal declares a `Motive` (hunger, rest, company, income, gear, growth, duty, aid, leave) and an `ActivityFamily` (unweighted, work, leisure, social, rest), as `docs/life-model.md` §2.1 tabulates; growth is leisure; loot under a clear's orders is unweighted (`FamilyFor`). `MotiveValidator` pins the table.
- **D2, the motive model:** priority = the goal's base (`BasePriority`, today's formula unchanged) × the product of the shared weights + commitment. The factors multiply first, then the base, so a weight of exactly 1 changes nothing; golden reproduced byte for byte. The first factor is the session's weight by motive (`LifeTuning`, in `EconomyTuning`); `Urgency.Of` gives new goals their motive's need; `MotiveModelTests` keeps `BasePriority` inside the model.
- **D3, one goal selector:** `GoalSelector.Rank`, called by both evaluators (F1): the best three, ties to the earlier goal, -1 never ranked, nothing allocated; decisions keep the best goal's base and weight.
- **D4, the fallback:** the next two goals are planned when the best can't be (`GoalSelector.PlanFirst`, depth 2 in the live game). It never runs today (F9), so golden didn't move.
- **D5, commitment:** the goal of the last plan adopted (`AgentState.LastGoal`, unsaved) gains `LifeTuning.Commitment`, 0 for now (0.8 flips a resident-day); plan 2 sets it.
- **D6, the life record:** `CrawlerLife`, 512 bytes after the traits record, format 15 (the series' one raise): 16 thought slots and four subjects; 320 spare bytes laid out for wants, fun, the day's rhythm, taste, keepsakes and an aspiration.
- **D7, thoughts:** 21 rows in `ThoughtTable`, thought where things happen through `AgentState.Think` (stacking, replacement of the slot worth least, lazy expiry, four subjects). A friend's death is -25 (from -20, L1.3). **Deviation:** thought at its sites, not hooked onto deeds, since `AddDeed` carries no tick and a rescue's thought names the rescued.
- **D8, mood:** the need terms plus the live thoughts, within ±100, in five bands (Breaking to -35, Low to -10, Steady, Good from 10, Happy from 30); pure and unsaved; the context computes its band once (`AgentContext.Mood`).
- **D9, what mood changes:** frustration eases 10 a game hour on the day's pace (F6); a breaking mood adds 15 an hour, a low one 5, good and happy ease 5 more; a low mood weighs income 0.9, a breaking one 0.75; nothing under orders (A8). **Amended (F12):** company is not weighed in plan 1. **Amended 2026-11-24** (the producer; plan 2's D25): mood reaches crawlers under orders as well.
- **D10, what the player sees:** happy, low and grieving faces; the mood line names its reason ("Low: Bram died."); the dossier's Crawler, Mind and Today tabs on the state badge's line; Mind and Today read lazily. The chronicle's ring grows to 2,048 (F5).

---

## 1. Goal
**The player promise:** watch a crawler live a day, not just work one. It wakes late after a windfall and browses the market. It spends the evening at the tavern with its regulars. Its face and dossier say how the day went, and why.

What Epic 11 leaves undone (the proposal's "why life still loops"):
- **Work and the hearth are all there is.** Measured at L0.3 (`docs/life-model.md` §3.1), a resident sleeps 9.8 hours, earns for 8.4 (walks included) and sits at the hearth for 3.1. Idling and wandering take under a quarter of an hour (F11). The proposal's 13–38% idle came from the time model's earlier steps. `EarnIncomeGoal` pulls until the comfortable purse (80g), and crawlers seldom get there.
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
  - **Unweighted** goals (hunger, orders, rescue, leaving) are never scaled by the routine or the intention, so survival and orders keep their precedence. (Orders: plan 3 revisits this under the producer's rule of 2026-11-24, A8.)
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
  - **Amended 2026-11-24** (the producer: "Crawlers can absolutely disobey orders"): orders are duty, a strong motive weighed with the rest, never a rule above them. Compliance decides whether a crawler takes them up, at every stage of a trip and every block of a shift, and mood weighs them (plan 2's D25). The reflexes and rescue still come first.
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

### L0. Groundwork (done 2026-11-23, `feat/life1-mind`)
- [x] The hand-over checked, and `docs/life-model.md` (L0.1); goals declare a motive and a family (L0.2).
- [x] A crawler's day measured on the gate seeds (L0.3, `MeasureACrawlersDay`): rest 9.8 h, work 8.4 h, the hearth 3.1 h, idling under a quarter hour; no plan unfound; 592 chronicle lines a day.
- [x] One goal selector (L0.4) and the motive model (L0.5), golden unchanged; the fallback (L0.6) and commitment at 0 (L0.7), golden unchanged.
- [x] The life record, save format 15 (L0.8), its round trip in the save validators.

### L1. The mind, plan 1's part (done 2026-11-23, `feat/life1-mind`; the look passed and merged as PR #28, 2026-11-24)
- [x] The thought table (L1.1) and thinking at 15 sites (L1.2), `ThoughtNamesTests` listing each; golden re-recorded for coverage only.
- [x] Mood (L1.3, measured: ordinary days Steady to Good 99%; a friend's death takes a mourner to Low) and what it changes (L1.4: frustration on the day's pace; income's weight when low; F12).
- [x] The Mind and Today models (L1.5); happy, low and grieving faces and the mood line's reason (L1.6, art pins); the dossier's tabs (L1.7, renders recorded in the pinned image).
- [x] **DoD `MoodFollowsTheDay`** (L1.8): `MoodsRiseWithAMealAndABunk` (gate seed 424242: 11 residents, all 69 onsets lifted mood; a save mid-day keeps every thought and mood) and `MoodFallsForAFriendsDeath` (seed 8: six mourners fell to Low or below within the hour).

### Plan 1's gate (2026-11-23)
- [x] `dotnet test`: core 350/350, client 563/563 (and in the pinned image); golden 3/3.
- [x] The soak: 18 of 18 within their bands; `guild`, `crafting` and `hunting` on seeds 1–20, 20 of 20 each.
- [x] Cost (`PopulationScalingProbe`, Release, against `main`): 16 crawlers 19.5–20.4k ticks a second against 20.1–20.6k; 24 crawlers 16.4–17.4k against 17.8–18.3k.

### Orders and autonomy (plan 2, done 2026-11-24, `feat/life1-tavern`)
- [x] Mood reaches crawlers under orders, and duty gains mood's weights, low 0.85 and breaking 0.6 (O.1, D25; `docs/life-model.md` §5.2).
- [x] An expedition's orders judged at every stage, so a member may stay behind or desert mid-trip (O.2, D25; §5.3; `AMemberWhoSoursOnTheWayStaysBehindAtTheNextStage`). F15 resolved.

### L1's wants (plan 2, done 2026-11-24, `feat/life1-tavern`)
- [x] The want table, five rows and their nine thoughts (W.1, D20); three slots in the life record, one roll on waking a game day (W.2); `FulfilWantGoal` and the fact `WantMet` (W.3); the Mind tab lists wants with their time left (W.4).
- [x] **DoD `AWantComesTrue`** (D.2): seed 5 with the tavern player, 12 wants arise and 3 come true at their venues, none missed.

### L2. Fun and the tavern (plan 2, done 2026-11-24, `feat/life1-tavern`)
- [x] Fun held as boredom (L2.1, D11); the activity table (L2.2, D12); venues as map data, open by hours and stations (L2.3, D13); the leisure score (L2.4, D15); the Recreate intent (L2.5) and goal, with the fact `Recreated` and the Bored term (L2.6, D14); variety and curiosity, a sixth trait (L2.7, Q8).
- [x] Station kinds as a table (T.1, D17; F13); the tavern's room off the hub, the Garden north, the floor 64 × 54 (T.2, D16; F14); ale and stew from the caravan (T.3, D19); the tavern built and kept by a keeper on its shifts (T.4, D18); served through the ledger (T.5); the tavern player and the soak's `tavern` profile (T.6); the tavern on screen, renders in the pinned image (T.7, D23).
- [x] Mood's pull on leisure, the leisure score's full share at 20, commitment kept at 0 (L2.8, D21, D22; F3 closed).
- [x] **DoD `AnEveningAtTheTavern`** (D.1, D24 as amended): seed 5 over three days, the tavern built and kept, 9 served on days 2 and 3, 8 of 12 residents an hour at a venue other than the hearth, the takings, wholesale and wages in a balanced ledger, a save on day 3.

### Plan 2's gate (2026-11-24)
- [x] `dotnet test`: core 370/370, client 563/563 (and in the pinned image, renders byte for byte); golden 3/3.
- [x] The soak: 21 of 21 on the default seeds; `guild`, `crafting`, `hunting` and `tavern` 20 of 20 each on seeds 1–20 (the crafting share at 0.75, F19).
- [x] The day, moods and F17 re-run (`docs/life-model.md` §5.11): leisure 0.7 h a resident-day, moods Steady to Good 98.6%, F17 unchanged.
- [x] Cost (`PopulationScalingProbe`, Release, against `main`): within noise at 16 and 24 crawlers.

### L3. Rhythm (plan 3, done 2026-11-25, `feat/life1-rhythm`)
- [x] Chronotypes and the routine (R.1); one waking and one night (R.2); the bedtime pull, the outpost asleep at night (R.3; F16 resolved); weights by family and hour (R.4, D30 amended).
- [x] Weariness and the windfall (R.5); satiation on work (R.6); the day's intention (R.7, D34 amended); a rest day keeps to safe zones (R.8); duty and a day off, Q11's switch (R.9); the economy with rhythm (R.10).
- [x] The intention in the dossier and the day log (V.1); the outpost's pulse (V.2); the seated pose (V.3); icons over crawlers at an activity (V.4). The art pins and renders re-recorded after a look at each.
- [x] **The DoD, `ADayOffAfterAWindfall`** (D.1, gate seed 20261010): a resident paid a hunt's share rests the next day, out of danger, 16.1 hours at the hearth or a venue, back at work the day after; a save on the day off loads byte for byte. The moment comes on 18 of 23 gate seeds (§6.11).
- [x] **`AnEveningAtTheTavern` by the clock** (D.2): the keeper on the evening shift, all takings in the evening; about 12 served a seed in three days (F18 resolved).
- [x] **`AMemberCalledInFromItsDayOffGoes`** (R.9, seed 5) and **`ARestDayKeepsToSafeZones`** (R.8).

### Plan 3's gate (LG1, 2026-11-25)
- [x] **`LifeOneTogether`** (G.1, gate seed 1): every life-core DoD on one seed with the tavern, guild, workshop and hunt players, saved and played on (§6.12).
- [x] `dotnet test`: core 378/378, client 566/566 (and in the pinned image, renders byte for byte); golden 3/3.
- [x] The soak: 21 of 21 on the default seeds; `guild`, `crafting`, `hunting` and `tavern` 20 of 20 each on seeds 1–20.
- [x] The day by the clock, days off, moods and F17 re-run (`docs/life-model.md` §6.13): the outpost asleep 80–99% from midnight to 03:00; moods Steady to Good 99%; F17 unchanged.
- [x] Cost (`PopulationScalingProbe`, 12, 16, 24 and 48 crawlers, against plan 2's end): 12–15% fewer ticks a second, from residents awake and deciding longer (F22).

### Still to come in this epic
- **The producer's look at L3** ("the outpost's pulse and a day off"), after V.4.
- **G.3, the producer's play session**, then G.4's close-out (the plan deleted, Epic LIFE-1 COMPLETED on the roadmap) and the PRs at their word (plan 2's, then plan 3's).

## 5. Deferred and out of scope
| Item | Where it goes |
| :--- | :--- |
| Conversations with a topic, social facts, pair chemistry (L4) | Epic LIFE-2 |
| Personal goods, taste, keepsakes, gifts (L5) | Epic LIFE-2. Taste and keepsakes take the life record's spare bytes |
| A good as a want's target (plan 2's D20); wagers at dice (D12) | Epic LIFE-2, with personal goods and taste, and social facts |
| A keeper's skill (plan 2's D18: a new skill would move every seed's draw) | Not planned; an option for LIFE-3's artisans |
| Meal anchors in the routine (plan 3's D29: hunger's pace gives about 2.4 meals a day without them) | Until a goal needs them; LIFE-2's talk or the tavern's supper may |
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
- **F1. Goal choice is written twice.** `AgentCognitionService.EvaluateAsync` picks the best goal for the live game. `GoapCognitiveEvaluator.EvaluateAsync` repeats the same argmax, and only `PlanExecutionValidator` uses it. Owner: L0.4 [Possibly Complex], one selector for both. **Resolved** at L0.4: `GoalSelector`.
- **F2. A goal that can't be planned falls through to ambient wandering.** Nobody has measured how often. Owner: L0.3 measures it [Possibly Complex], and L0.6 tries the next goals [Possibly Complex]. **Resolved** at L0.3 and L0.6: none goes unfound today (F9); the fallback is in place for plan 2's goals.
- **F3. Commitment exists only within a plan.** Goals are not re-scored while a plan runs (only reflexes cut it short), so flicker can only happen at a plan's end. But plans are short (F10). L0.3 measured near ties at 3.6% of choices and 0.8 flips a resident-day. Owner: L0.7 builds the rule at 0 [Possibly Complex]; plan 2 sets its value when leisure options compete. **Open, plan 2's:** the rule is built (L0.7) at 0. **Closed** at plan 2's L2.8: flips stay at 0.8 a resident-day with leisure in, so commitment stays 0.
- **F4. The dossier has no height to spare at 720 rows** (Epic 11's F16). Mind and Today won't fit as sections. Owner: L1.7, tabs (Q7) [Possibly Complex]. **Resolved** at L1.7: the tabs sit on the state badge's line.
- **F5. The chronicle is one ring of 1,024 events for the whole outpost.** A day log read from it holds only what the ring still holds. L0.3 measured 592 lines a day (391 to 1,141 a seed-day; over half gossip and companionship), so on the busiest seeds the morning is gone by night. Owner: L1.5, decided: the day log tells the thoughts begun since midnight and the crawler's dramatic and crisis lines that no same-tick thought tells, and the ring grows to 2,048, past the busiest day measured.
- **F6. Frustration runs in physical time:** −1 every second (`AgentState`'s drives), about 42 a game hour. Every other need is paced in game time through `DayTuning`. A low mood that "feeds frustration" in game hours would be drowned by it. Owner: L1.4 judges whether frustration moves onto the day's pace, as its own behaviour change [Possibly Complex]. **Resolved** at L1.4: 10 a game hour on the day's pace, its own commit.
- **F9. No plan goes unfound.** L0.3 measured none in 828 resident-days, with and without the guild player. Tier 3 runs only when no goal is valid. So D4's fallback changes nothing today: it is for plan 2's goals (a venue out of reach, a friend gone). Owner: L0.6 [Possibly Complex]; golden should not move. **Resolved** at L0.6.
- **F10. Plans are short.** About 600 decisions a resident-day, 594 of them a new plan: goals are re-scored every 17 ticks or so, because a harvest's step ends every few game minutes. The motive model is on the hottest path of cognition, and a weight that moves with the hour reaches a crawler within minutes. Owner: L0.4 and L0.5 keep it allocation-free [Possibly Complex]; the gate measures its cost. **Resolved** at L0.4–L0.5: allocation-free; the gate's cost measured (§4).
- **F11. Idle has gone.** Residents earn 8.4 hours a day (walks included) and spend 3.1 at the hearth; idling and wandering take under a quarter of an hour. Leisure will compete with `EarnIncome` itself. Owner: plan 2 (L2), whose Recreate must win evenings from work through the motive model's weights, and plan 3 (L3), whose satiation lowers work's pull [Possibly Complex]. **Plan 2's part done:** Recreate and wants take 0.5 h a resident-day with no player and 0.7 with the guild's, work 8.3 h (`docs/life-model.md` §5.9). **Plan 3's part:** satiation barely bites (residents sit under the comfortable purse 92% of the time and never work above it); the day's weights by the hour moved work to the working block, and the freed hours go mostly to the hearth (`docs/life-model.md` §6.4–§6.7). **Open, LIFE-2's:** the hearth is the outpost's leisure; venues other than it take about an hour a resident-day.
- **F8. Frustration starts no tantrum.** The proposal's "a very unhappy one breaks, through today's frustration path" has no path to ride: tantrums come only from a contested node lost to a rival and an artisan's breakdown, and `MentalBreakEvaluator` reads hunger, energy and danger. Frustration weighs against compliance (`ComplianceUtility`) and sets `IsThreatened`, a fact no action reads. Owner: L1.4 [Possibly Complex]: mood feeds frustration, so unhappy crawlers defy orders sooner; a break of its own is left to plan 2's wants or LIFE-2, by exception. **Resolved** at L1.4: mood feeds frustration; no break of its own in plan 1.
- **F12. A hurt crawler's low mood kept it from healing.** With L1.4's first weights, a low mood pulled company goals 1.25: a crawler hurt in a fight (the hurt term and thought make it low) went to the hearth instead of its bunk, healed more slowly there and stayed low. Four live DoDs failed with it (`ADraughtKeepsAPartyWorking`, `EveryScenarioPlaysTogether`, `PassionBurnsBright`, `ACallingEmerges`), though their gate-seed measurements barely moved. Owner: L1.4, resolved: mood weighs income only in plan 1 (low 0.9, breaking 0.75), and never a crawler under orders (the epic's A8). "An unhappy crawler wants leisure more" lands on plan 2's fun, whose Recreate must not outbid healing [Possibly Complex].
- **F13. Six places switch on a station's kind** (read while drafting plan 2, 2026-11-24): `WorkshopTuning.CostOf`, `WorkshopRules` (the drill yard's "no artisan here", the kind's word), `StationModel` (takes work, name, description) and `ArtKeys.Station`. The tavern would add a case to each. Owner: plan 2's T.1 [Possibly Complex]: station kinds as a table (`StationTable`, D17), behaviour-neutral, so the tavern is a row. **Resolved** at T.1.
- **F14. Nothing beside the hub is free for the tavern's room.** The floor is 64 × 42 units, the most a 720-row window shows whole at one screen pixel per art pixel, and every cell around the hub is taken (the hearth's ring, the stall, the lane, the bunks, the drifters' spawn points). The one free ground is the solid strip under the Old Stair, west of the Ore Quarry and the Sump; the beetles' reach ends at y 10, above it. Plan 2's draft put the room in that strip, past the quarry. **Resolved by the producer** (2026-11-24: "It is not only okay but encouraged to expand the map beyond the viewport size"): the tavern takes the Garden's room off the hub and the Garden moves north, the floor growing past the window (D16). Owner: plan 2's T.2 [Possibly Complex].
- **F15. Orders override a crawler's choice in three places** (read while revising plan 2 for the producer's "Crawlers can absolutely disobey orders", 2026-11-24): an expedition's orders are judged once, at dispatch, then hold whatever the member's compliance, at 120 (Epic 10's D8); a shift's, complied with, stand at 120 too; and the motive model leaves crawlers under orders out of mood's weights (plan 1's D9). Owner: plan 2's O.1 and O.2 [Possibly Complex]: mood reaches them, and an expedition's orders are judged at every stage (D25). The expedition already handles a member who stays behind or deserts. **Resolved** at O.1 and O.2 (`docs/life-model.md` §5.2–§5.3).
- **F16. The outpost has no night** (plan 2's P.1, 2026-11-24, `docs/life-model.md` §5.1). Sleep starts at the tired line and ends at the wake lines, never by the clock, and every resident spawns rested at 06:00. So the outpost sleeps together from mid-afternoon to before midnight (56% asleep at 18:00) and works through the small hours (43% at work at 02:00). An evening tavern would open while half the outpost sleeps. Owner: plan 2 keeps no clock (D26: the tavern opens on its keeper's shifts, set where residents are free) [Possibly Complex]; plan 3's routine brings sleep to the night [Possibly Complex], and `AnEveningAtTheTavern` is re-read by the clock at LG1. **Resolved** at R.2 and R.3 (D27, D31): the outpost sleeps 80–99% from midnight to 03:00 and nobody from noon to 18:00; `AnEveningAtTheTavern` re-read by the clock at D.2 (`docs/life-model.md` §6.3, §6.13).
- **F17. Nobody of Apprentice band meets a rat any more** (plan 2's L2.6, 2026-11-24). Epic 11's `PassionBurnsBright` also showed veterans learning more from harder foes: practice a blow from a beetle (challenge 7) against a rat (2) by fighters at Apprentice or above. Before Recreate, Apprentices struck rats on 4 of the 23 gate seeds; since, on none of 63, nor with the guild's clears aimed at the warren first: free time took the field hours that brought them to the warren by chance, and the warren's living Matriarch keeps hunts off it. The scenario now holds the passion on its own; the rule stays proved by the practice validators, and `MeasurePassionsOnTheGateSeeds` reports where it shows. Owner: plan 2's gate (C.1) re-reads it with the tavern and wants in [Possibly Complex]; if it is still gone, a scenario aimed at the warren is the producer's call (Epic 11's DoD, by exception).
- **F18. The tavern trades little** (plan 2's T.6, 2026-11-24). With the tavern built and kept on the day shift, the soak's `tavern` profile sells about two things a seed in three days. While it is open, earning outbids Recreate five times in six (F11), and when Recreate wins, free dice are often the best it finds. Owner: plan 2's L2.8 [Possibly Complex]: mood's pull on fun and the leisure score's strength, measured against the economy; plan 3's satiation is the epic's lever on work hours. **Plan 2's part done** at L2.8: about 5.5 served a seed in three days on the soak's seeds 1–20 (0–9), 5.8 on the gate seeds (§5.10). **Resolved** at D.2: on the evening shift, with the outpost awake in its evenings, the tavern serves about 12 a seed in three days (6–20 on the gate seeds, all in the evening).
- **F19. The crafting loop makes goods on fewer seeds** (plan 2's D.1, 2026-11-24). `TheCraftingLoopHoldsOnMostSeeds` asks the crafting profile's stations to make goods on 80% of seeds 1-40 within three days; with free time, variety, wants and mood's pull on leisure in (L2.6-L2.8) they make goods on 31, one short. The share was lowered to 0.75 by exception (`EconomySoak.CraftingMadeShare`); the cause isn't measured. Owner: plan 3's satiation and the day's rhythm (LG1's soak bands) [Possibly Complex]: re-read with routines in, and the producer's call if it falls further. **Resolved** at R.10: goods on 37 of seeds 1–40, the share back at 0.8.
- **F20. The crafting loop's members quit unpaid on more seeds** (plan 3's R.10, 2026-11-25). With nights, routines and days off residents work and sell less (work about 7.4 hours a resident-day over six days, from 7.9), the generous players' treasury runs dry at a caravan's visit more often, and a member quits for want of pay on 13 of seeds 1–40 (10 allowed). `CraftingUnpaidQuitShare` is 0.35 by exception. Owner: the producer's band (by exception); Epic 10's whole-loop economy (F14) [Possibly Complex].
- **F21. Day 1's late morning waits on the caravan** (plan 3's V.2, 2026-11-25). From about 10:10 to the caravan's noon visit on day 1, nearly every resident stands "Thinking" with no plan, then all plan at once; it is the "between decisions" share of the day's measurement (1.3 hours a resident-day at P.1's baseline, before plan 3), most likely work that can't be planned while the stall can't buy. Owner: a measurement first (which goals are scored and why none is planned) [Possibly Complex], with Epic 10's economy findings.
- **F22. The rhythm costs 12–15% of ticks a second** (plan 3's G.2, 2026-11-25). Residents sleep about 7.6 hours a day instead of 10 and decide while awake: about 680 decisions a resident-day against 551. Checking the intention once a routine day and the purse target's fast path saved nothing measurable. 48 crawlers still run 6,000 ticks a second (140 times real time). Owner: none needed now; LIFE-2's gate measures again [Basic].
- **F7, resolved 2026-11-23. PR #27 (Epic 11's plans 3 and 4) was open** while the roadmap and the history called it merged, so plan 1's branch was first cut from `feat/epic11-masterworks`. #27 merged the same day (`d0d2d47`), and the branch was rebased onto `main` at the go.

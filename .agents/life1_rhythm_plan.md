# Epic LIFE-1 plan 3: Rhythm and the gate (L3, and LG1)

This is a working document (AGENTS.md, Documentation Protocol 6), the last of Epic LIFE-1's three plans. It was drafted 2026-11-24 at the producer's directive while away ("Once you finish all your current work for this implementation draft up the next implementation plan for the Life series of work"), after plan 2's work and close-out on `feat/life1-tavern`. The epic said plan 3 is drafted when plan 2 merges, so P.1 checks this plan's hand-over against `main` after that merge.
- **The design** is the epic, [`docs/epics/epic-life-1.md`](../docs/epics/epic-life-1.md): its rules A1–A12 (A8 as amended 2026-11-24), its questions (Q11, an outpost rest day, is this plan's), §2.5's sketch of rhythm, and the findings plan 2 left open (F11, F16, F17, F18, F19).
- **The decisions** below (D27–D38) are this plan's defaults; the producer answers by exception. Each moves to the epic's §0 when its step lands.
- **Complexity:** Very Complex as a whole. The day's pace and sleep move every crawler's day. Routines, satiation, weariness and the day's intention all weigh every goal through the motive model. A rest day changes where a crawler may go. The client gains a pose, icons over crawlers and the outpost's pulse, and the epic's gate closes it out. It is split below so that every step is Basic or Possibly Complex; none needs supervision.
- **Scope:**
  - **L3:** sleep by night (F16), chronotypes and the routine, the weights by family and hour, satiation, weariness and the windfall, the day's intention, rest days in safe zones, duty under the routine (D25), Q11's switch, and the DoD `ADayOffAfterAWindfall`.
  - **Moved from plan 2:** plan 2's look at venues (T.7's deviation): the seated pose and the thought icons over crawlers.
  - **LG1:** `LifeOneTogether`, the soak's bands with days off and the tavern, scaling at 12, 24 and 48 crawlers, and the producer's play session.
- **Branch:** `feat/life1-rhythm`, from `main` after plan 2's PR merges. One PR into `main` at the end, with a commit per step.
- **Estimate:** 14–20 agent days: sleep and the routine 4–6, satiation and the intention 4–5, the look 3–4, the DoD and the gate 3–5.
- **The producer's stops:**
  - the day's new pace (R.2) and the soak's bands with days off (R.10), both by exception;
  - the look at L3, "the outpost's pulse and a day off", after V.4;
  - the play session (G.3);
  - the merge.
- **The checks:**
  - golden unchanged through every step that only adds data or mechanism (P.1, R.1, R.5's record fields, V.1);
  - each behaviour change re-records golden in its own commit, with the gate seeds' day held and the scenario seeds re-picked by `ScenarioSeed` where a moment moves;
  - `ADayOffAfterAWindfall` and `LifeOneTogether`, live on the real outpost with nothing injected (scripted players act only through commands).

## 0. What plan 2 hands over (checked at P.1)
- **The motive model:** priority = base × weights + commitment. Motives 0–11 (Fun 10, Want 11), room for 16. The weights are the session's weight by motive and mood by band: income (low 0.9, breaking 0.75), duty (low 0.85, breaking 0.6) and fun (low 1.25, breaking 1.5). Mood reaches crawlers under orders too (D25). Commitment stays 0 (D22). 23 goals, 50 of 64 planner facts.
- **The life record** (`CrawlerLife`, 512 bytes, format 15):
  - **In use:** thoughts at 0, subjects at 128, three want slots at 192, boredom at 264, curiosity at 277, the tried venues at 278, variety at 280.
  - **A limit to know:** the tried venues are a byte, a bit for each of a map's first eight venues; a venue past the eighth counts as tried, so it never pulls the curious (Floor 1 has five).
  - **Laid out for this plan:** the rhythm room at 268 (8 bytes: weariness and the last windfall) and the intention at 276. Byte 279 is padding.
  - **Spare:** 224 bytes from 288, for LIFE-2 and LIFE-3.
- **Sleep:** `RestGoal` is valid below the tired line (energy 40), and sleep ends at full energy or hunger 85 (`DayTuning.Wakes`).
  - **The pace:** energy falls 2.75 a game hour standing, 8.25 walking or working and 6 at a station's work; a bunk restores 8 an hour.
  - **The result:** a cycle of about 17–18 game hours, so the outpost has no night (F16). It sleeps together from mid-afternoon to before midnight and works through the small hours (`docs/life-model.md` §5.1).
- **Free time:**
  - **Pace and fills:** boredom rises 2 a game hour awake; the leisure score's full share is 20 fun an hour; variety and curiosity shape the choice.
  - **Measured:** Recreate takes 0.32–0.89 h a resident-day at venues other than the hearth (§5.10). Earning outbids it most of the day (F11), and the tavern serves about 6 a seed in three days (F18).
- **Wants:** five rows, a roll on waking (60%) on `SimRandomSalt.Wants` (21). `FulfilWantGoal` pulls to the spell that answers a want.
- **The tavern:** a station kind with a keeper on shifts; its venue is open only while it is built and kept. Ale and stew are bought wholesale from the caravan, and the soak's `tavern` profile is integrity-only.
- **Orders** are judged at every stage of a trip and every block of a shift (D25). Plan 2 left this plan to decide whether a day off weighs against them.
- **What the frames carry:** a crawler's visual state, worn look, mood and the shots. Nothing yet says which activity a crawler is at (T.7's deviation).
- **The client:** 24 pose frames a body (`BodyKit`, `HeadAnchors`) and no seated pose; the header's clock (`OutpostClock`) shows the time of day.
- **The cost** at plan 1's gate: 16 crawlers 19.5–20.4k ticks a second, 24 crawlers 16.4–17.4k. Plan 2's is in `docs/life-model.md` §5.11.

## 1. Decisions proposed (defaults; the producer answers by exception)
- **D27, a day is one waking and one night** (F16): the energy drains awake are scaled so a waking day lasts about 16 game hours and a night's sleep about 8.
  - **Why:** from full, at today's pace, a crawler reaches the tired line in about 10 hours. No weight by the hour can keep it up until bedtime, because `RestGoal` outbids almost everything once energy is under 40.
  - **How:** one factor on the standing, walking, working and crafting drains, in `DayTuning` (0.6 to start, measured at R.2). Bunk recovery stays 8 an hour, so 40 to 100 still takes 7.5 hours.
  - **What it moves:** every crawler's day and the hours of work energy allows; hunger's pace (100 a day) is unchanged. Golden re-recorded; the scenario seeds re-picked; the soak re-read.
  - **Alternative:** two sleeps a day, the second a nap. The outpost keeps a cycle, but not a night.
- **D28, chronotypes, drawn and saved** (A7, amended):
  - **The draw:** lark, average or owl (20%, 60%, 20%), at spawn on its own salt (`SimRandomSalt.Chronotype`, 22), into the life record's byte 279, where 0 reads as average. No earlier draw moves.
  - **Saved, not computed:** cognition reads it from its copy of the life record without the seed, as it reads curiosity (plan 2's L2.7). A7's "routines are computed, not saved" holds for the hours below.
  - **Alternative:** compute it from the seed and the id. The context would then need the seed, and it would change whenever ids are dealt differently.
- **D29, the routine, computed** (§2.5): a pure function of the chronotype, the personality and the day, with no field of its own.
  - **Waking hour:** 05:00 for a lark, 07:00 average, 10:00 for an owl, each shifted up to 40 minutes either way by a day's draw keyed on the crawler and the day.
  - **The blocks:** a working block of 5 to 9 hours by diligence, starting an hour after waking; a leisure block until bedtime; the night, from 16 hours after waking until waking.
  - **An artisan's working block is its shifts** (§2.5): a keeper or a smith works when the player has set it to.
  - **Meal anchors** are left out: hunger's pace already gives about 2.4 meals a day. They wait until a goal needs them (a Deferred line in the epic).
- **D30, weights by family and hour** (A1): the motive model gains the routine's factor by the goal's family (`FamilyFor`), in `LifeTuning`:

  | Block | Work | Leisure | Social | Rest |
  | :--- | ---: | ---: | ---: | ---: |
  | Working | 1.2 | 0.8 | 0.9 | 1 |
  | Leisure | 0.8 | 1.2 | 1.15 | 1 |
  | Night | 0.6 | 0.7 | 0.8 | the bedtime pull (D31) |

  - Unweighted goals (hunger, orders, rescue, leaving) are never scaled, as A1 says.
  - **Where it is computed:** the routine's block is computed once when the context is built, as mood's band is (`AgentContext.Block`), from its tick and the life record. Nothing allocates.
- **D31, the bedtime pull** (F16):
  - **Rest's validity:** in its night, `RestGoal` is valid from the bedtime line (energy 70) up, not only under the tired line, and its routine weight is 1.5. A crawler goes to bed at bedtime rather than when it drops.
  - **Waking:** a sleeper in its night dozes on at full energy until its waking hour. Hunger's line still wakes it, as does the longest sleep (12 hours).
  - **Outside the night,** sleep keeps today's lines.
  - **The reading:** `MeasureACrawlersDay` by the clock, with most residents asleep between midnight and 05:00 and most awake at 18:00 (§5.1's table, re-run).
- **D32, satiation on work** (§2.5):
  - **The target:** a crawler's purse target is the comfortable purse (80g) times its greed's factor, plus the cost of what its held wants ask for (a supper, a round of ale).
  - **The weight:** work weighs 1 up to the target and falls linearly to 0.6 at twice the target. It lands on the Work family's goals, not on income alone: a rich crawler takes fewer bounties and hunts too. `EarnIncome`'s own limit (valid below the comfortable purse) is unchanged.
  - **What it answers:** F11's "earning outbids free time" for crawlers who have earned enough. F18's trade is re-read after it.
- **D33, weariness and the windfall** (§2.5; the life record's rhythm room):
  - **Weariness** (a float at 268, 0 to 100): it rises 1 a game hour at work (a harvest, a station's work, a fight, an expedition's work), and only a day off clears it. A day off halves it, and a second in a row clears it.
  - **Its weight:** work weighs 1 − 0.3 × weariness / 100, so 0.7 at its height.
  - **The windfall** (a tick at 272): a single payment of at least half the comfortable purse (a bounty, a hunt's share, an expedition's share) records its tick.
  - **"Earned a rest"** (+4 for 24 hours): a new thought row, thought at the payment, through `AgentState.Think`.
- **D34, the day's intention** (§2.5; the life record's byte 276):
  - **The draw:** once a day, on waking from its night's sleep, on its own salt (`SimRandomSalt.Intention`, 23). The day drawn is kept beside it, a `ushort` at 288, the first of the spare bytes. The intention is a work day, a rest day, a training day or a want's day.
  - **The odds:** a work day by default. A rest day's odds are 5%, plus 40% × weariness / 100, plus 50% after a windfall in the last game day, plus 15% with the purse above its target. A training day's are 15% with a passion. A want's day's are 25% with a want held.
  - **The weights** (by motive or family, in `LifeTuning`):
    - a rest day: work 0.4, leisure 1.3, social 1.2;
    - a training day: growth 1.5, work 0.8;
    - a want's day: the want 1.5.
  - **Saved** (byte 276 and the day at 288), since it is drawn once. A save made before plan 3 reads a work day.
- **D35, a rest day keeps to safe zones** (A8):
  - **The rule:** on a rest day, a crawler's lone danger limit is 0, so it neither braves a hazard nor walks a lair's reach (`Vitality.LoneDanger`, `WouldEnterAt` and `WouldWalk` read the intention). Rescue is unweighted and unchanged: a friend still goes for the fallen.
  - **The reading:** in `OutpostWatch`, no resident on a rest day stands in a hazard sector or a lair's reach except to rescue someone or under orders it kept (D36).
- **D36, duty under the routine and the intention** (D25; A1, amended):
  - **The routine never weighs duty.** An artisan's working block is its shifts, and an expedition's muster is the player's timing.
  - **A rest day weighs duty 0.8,** so compliance decides whether a member answers a muster on its day off. A member who keeps to its orders is called in: its intention becomes a work day, so the rest day's safe zones never fight its orders.
  - **Alternative:** nothing but mood and compliance weighs duty, and a rest day yields to any order.
- **D37, Q11's switch:** `LifeTuning.OutpostRestDay` (off by default). When it is on, every seventh game day is a rest day for every resident, and artisans' shifts that day are the player's to keep or clear.
- **D38, what the player sees** (A10):
  - **The intention:** "Today: a day off" on the dossier's Crawler tab under the mood line, and as the first line of Today ("Woke at 07:10, meaning to rest").
  - **The outpost's pulse:** beside the header's clock, a small three-part bar (asleep, at work, at leisure) from the frames, so the outpost's day can be seen turning.
  - **Moved from plan 2 (T.7):**
    - **The seated pose:** a new visual state, `Seated`, with its body frames in every look, worn at a seat during an activity.
    - **The thought icons:** a mug, a bowl, dice, a leaf or a purse over a crawler at an activity, carried by the frames (`EntityView.Activity`) and drawn by the scene.

## 2. The milestones, phased

### P. The hand-over (half a day)
**Complexity:** Basic.
- [ ] **P.1 The hand-over checked, and the day's pace measured** [Basic: docs and a measurement's extra columns; golden unchanged]
  - Check §0 against `main` after plan 2's merge, and record any difference as a finding (the epic's §7, F20 onward).
  - `MeasureACrawlersDay` gains the hours awake between sleeps, the share asleep by clock hour, and work hours a resident-day: the baseline for R.2 and the DoD.
  - `docs/life-model.md` gains §6, "Rhythm", with the baseline.

### R1. The night and the routine (4–6 days)
**Complexity:** Very Complex as a phase by bundling: the day's pace, a saved draw and weights on every goal. Split into four steps, each Basic or Possibly Complex. R.1 leaves golden unchanged; R.2, R.3 and R.4 each re-record it in their own commits.
- [ ] **R.1 Chronotypes and the routine as data** (D28, D29) [Possibly Complex: a saved draw on its own salt and a pure function that cognition reads; golden re-recorded for coverage only if the hash mixes the byte]
  - The chronotype's draw at spawn into byte 279, `Routine.Of` (waking hour, working block, leisure block, night) and `AgentContext.Block`, with every routine weight 1.
  - **The checks:** `RoutineValidator` (the draw's odds, blocks covering the day, an artisan's block from its shifts, the same inputs giving the same routine); the round trip; a seed's earlier draws unchanged.
- [ ] **R.2 One waking and one night** (D27) [Possibly Complex: a pace change that moves every crawler's day; golden re-recorded and seeds re-picked in their own commits; the producer by exception]
  - Scale the awake drains in `DayTuning`, measured so that the waking day is about 16 hours and the night about 8, before the bedtime pull lines them up with the clock.
  - **The checks:** the gate seeds' day holds (nobody starves, nobody collapses); `MeasureACrawlersDay`'s hours of work, which go to the producer by exception with the soak's bands; every live scenario on its seed or re-picked with the reason.
- [ ] **R.3 The bedtime pull** (D31; F16) [Possibly Complex: sleep's validity and waking on the hot path; golden re-recorded in its own commit]
  - Rest's validity and weight in the night; dozing on until the waking hour; the night's block read from the routine.
  - **The checks:** `MeasureACrawlersDay` by the clock (most asleep between midnight and 05:00, most awake at 18:00); a lark up at dawn and an owl awake at midnight on the gate seeds; F16 resolved.
- [ ] **R.4 Weights by family and hour** (D30) [Possibly Complex: a new factor in the motive model; golden re-recorded in its own commit]
  - The routine's factor in `MotiveModel.Weight`, from `LifeTuning`'s table; `MotiveModelValidator` gains a working and a leisure block's weights, and unweighted goals untouched.
  - **The checks:** work hours fall in the leisure block and rise in the working block (`MeasureACrawlersDay`); the gate seeds' day holds; leisure at venues re-read against §5.10.

### R2. Satiation, weariness and the day's intention (4–5 days)
**Complexity:** Very Complex as a phase by bundling: saved fields, a daily draw, the motive model, and where a crawler may go. Split into six steps, each Basic or Possibly Complex.
- [ ] **R.5 Weariness and the windfall** (D33) [Possibly Complex: two saved fields written where work and pay happen, and a thought; golden re-recorded for coverage only if a choice moves]
  - Weariness written each game hour at work (in `AgentRuntime`, on the day's pace); the windfall's tick at a payment of half the comfortable purse or more; "Earned a rest" in `ThoughtTable`, thought at the payment.
  - **The checks:** the round trip; `ThoughtsValidator` and `ThoughtNamesTests` (the new place); weariness after a measured day's work.
- [ ] **R.6 Satiation on work** (D32; F11) [Possibly Complex: a factor in the motive model; golden re-recorded in its own commit]
  - The purse target (the comfortable purse by greed, plus what held wants ask for) and the Work family's weight; `MotiveModelValidator` gains a rich crawler whose work weighs less.
  - **The checks:** work hours by purse on the gate seeds; F11 and F18 re-read; the gate seeds' day holds.
- [ ] **R.7 The day's intention** (D34) [Possibly Complex: a daily draw on its own salt, a saved field and weights on motives and families; golden re-recorded in its own commit]
  - The draw on waking from the night's sleep, its odds from weariness, the windfall, the purse, passions and wants; its weights in the motive model; the day's clearing of weariness.
  - **The checks:** `IntentionValidator` (the odds by input, one draw a day, a work day by default, a save before plan 3 reading a work day); rest days measured on the gate seeds (how often, and after what).
- [ ] **R.8 A rest day keeps to safe zones** (D35) [Possibly Complex: danger's judgement in `Vitality`, read by every work goal's targeting]
  - The lone danger limit at 0 on a rest day, through `LoneDanger`, `WouldEnterAt` and `WouldWalk`; `OutpostWatch` gains the reading.
  - **The checks:** a validator that a resting crawler skips a seam in a hazard and a lair's reach, and still rescues; the gate seeds' day holds with no breach.
- [ ] **R.9 Duty under the routine and the intention** (D36), **and Q11's switch** (D37) [Possibly Complex: orders and a day off, in cognition and the guild]
  - A rest day's weight on duty; the member who keeps to its orders is called in (its intention set to a work day on the main thread); `LifeTuning.OutpostRestDay`.
  - **The checks:** a live test with the guild player that a member on its day off may stay behind at the muster, and one called in goes; Epic 10's and 11's guild scenarios on their seeds; the switch's rest day for everyone in a validator.
- [ ] **R.10 The economy with rhythm** [Possibly Complex: measuring and setting soak bands, by exception]
  - Every soak profile with nights, routines, satiation and days off; D25's work hours (6–8 a resident-day) measured beside them; F19's crafting share re-read.
  - The bands go to the producer by exception, with the numbers in `docs/life-model.md` §6 and `docs/economy-model.md`.

### V. What the player sees (3–4 days)
**Complexity:** Possibly Complex: a dossier line, Gum, a new pose's frames in every body, and icons drawn by the scene. Each look's art pins and renders are re-recorded after a look, in their own commits.
- [ ] **V.1 The intention in the dossier and the day log** (D38) [Basic: a model's line and the day log's first line; presentation tests]
  - `DashboardModel`'s Crawler tab gains "Today: a day off" under the mood line; `DayLogModel` opens with the waking and the intention.
  - **The checks:** `LifeModelsTests`; the dossier's renders in the pinned image.
- [ ] **V.2 The outpost's pulse** (D38) [Possibly Complex: Gum in the header at every offered size; a model from the frames]
  - A pulse model (asleep, at work, at leisure, from the frames' visual states and activities), and its bar beside the clock in `HeaderBar`.
  - **The checks:** the header's test at every offered size; renders in the pinned image.
- [ ] **V.3 The seated pose** (D38, from plan 2's T.7) [Possibly Complex: a new visual state with body frames in every look; art pins re-recorded]
  - `AgentVisualState.Seated`, its frames in `BodyKit` and `HeadAnchors`, the body slots, `CrawlerAppearance`; the Recreate handler sits a crawler at a seat.
  - **The checks:** the people sheet looked at; art pins; `ThrashingTests`; renders.
- [ ] **V.4 Thought icons over crawlers** (D38, from plan 2's T.7) [Possibly Complex: a field the frames carry, art and the scene]
  - `EntityView.Activity` captured from the crawler's intent; glyphs (a mug, a bowl, dice, a leaf, a purse) in the UI kit or a sprite set; `WorldScene` draws one over a crawler at an activity.
  - **The checks:** `ArtCompletenessTests` (every activity has its icon); art pins; renders; the producer's look at L3 after this step.

### D. The Definition of Done (1–2 days)
**Complexity:** Possibly Complex: live scenarios with nothing injected.
- [ ] **D.1 The DoD: `ADayOffAfterAWindfall`** [Possibly Complex: a live scenario]
  - On a seed with the hunt or guild player, nothing injected: a resident paid a windfall takes a rest day on its next waking. It stays out of every hazard and lair's reach all day, spends an hour or more at venues, and returns to work the day after. Three-day work hours stay in the soak's band, and D25's hours are measured.
  - A save on the rest day loads with the intention, weariness and the windfall kept.
- [ ] **D.2 `AnEveningAtTheTavern` by the clock** (F16's re-read) [Basic: a re-run with the evening's hours]
  - With the tavern's keeper on the evening shift (18:00–02:00), most of its takings come in the evening, and the tavern player picks the evening blocks.

### G. The gate: LG1 (2–3 days)
**Complexity:** Possibly Complex: a scenario of every scenario, measuring, and the producer's session.
- [ ] **G.1 `LifeOneTogether`** [Possibly Complex: every DoD on one seed, with a save mid-day]
  - `MoodFollowsTheDay`'s beats, `AWantComesTrue`, `AnEveningAtTheTavern` and `ADayOffAfterAWindfall` on one seed with the tavern and guild players; saved mid-day and played on, with the same outcomes.
- [ ] **G.2 The gate** [Possibly Complex: measuring]
  - **The tests:** every test, the golden check, the gate seeds' day, and the client's tests in the pinned image.
  - **The soak:** every profile's band on the final build (R.10's, set by the producer).
  - **Measurements:** the day by the clock, moods, rest days, leisure at venues, F17's harder foes.
  - **Cost:** `PopulationScalingProbe` gains 48 crawlers, measured at 12, 24 and 48 beside `main` at the branch's start, in a quiet sitting.
- [ ] **G.3 The producer's play session** [Basic: the producer's]
  - A build and a seed for the session; findings go to the epic's §7.
- [ ] **G.4 Close-out** [Basic: written from lists this plan holds]
  - **The docs, in order:**
    1. the roadmap first: Epic LIFE-1 COMPLETED after the play session, and Milestone 6's next epic (LIFE-2) drafted at the producer's word;
    2. the epic's status, §0, §4 and §7;
    3. `docs/life-model.md` and `docs/economy-model.md`;
    4. AGENTS.md: the routine's place in cognition, and recipes for a chronotype and an intention;
    5. the focus file and `.agents/architecture_history.md`.
  - **Delete this plan.**
  - **Open the PR** into `main` at the producer's word.

## 3. Risks (this plan's; the epic's §6 holds the rest)
- **The new pace moves everything** (D27): every scenario's moment, the soak's bands and the hours of work. It lands on its own (R.2), measured before the bedtime pull, with a re-pick round in its own commit; the work hours go to the producer.
- **Days off and satiation cut the economy's work.** Crawlers who work less earn less, buy less and may starve if a rest day eats a meal. Hunger is unweighted, so a resting crawler still eats. R.10 measures purses, work hours and starvation on every profile, and the bands go to the producer.
- **A rest day strands a party.** A member resting at the muster is D36's case: compliance decides, the muster's time limit holds, and a member who comes is called in.
- **Everyone sleeps at once,** so the stall, the tavern and the board are empty at night. Chronotypes spread bedtimes over five hours, and the soak reads the stall's sales by hour.
- **Owls and the tavern's hours:** the evening shift suits the average and the owl, not the lark. D.2 reads the evening's takings, and the tavern player picks its blocks from where residents are.
- **Flicker at the blocks' edges:** a weight that jumps at a block's edge could flip choices. `ThrashingTests`, the switch count in `MeasureACrawlersDay`, and commitment (still 0, D22) if it is needed.
- **Cost:** the routine is computed once per context and read by every goal's weight; the intention is drawn once a day. G.2 measures it beside `main`.
- **Planner facts:** none added (50 of 64). Rest days and routines are weights, not facts.

## 4. What this plan leaves
- **Epic LIFE-2** (L4–L6: talk, gifts and bonds, the scene), drafted at the producer's word after the play session.
- **Meal anchors** (D29), until a goal needs them.
- Any finding of the play session that isn't a defect, to the epic it belongs to.

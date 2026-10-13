# Epic MAP Phase 5: Definition of Done (Implementation Plan)

- **Epic:** [Epic MAP Revision 2](../docs/epics/epic-map.md), Phase 5. Status is owned by `docs/ROADMAP.md`.
- **Branch:** `feat/epic-map-revamp` (continues from Phase 4, `ae58859`). The Phase 1–4 PR is open; at the producer's request Phase 5 lands on the same branch so that the PR completes the epic.
- **Written:** 2026-10-12, after the producer's Phase 4 visual check passed (the mouse and keyboard controls). **Approved** by the producer 2026-10-12 with D1 = A (jitter measured in sim time) and D2 = A (assert the strongest determinism that holds in 3 of 3 runs; record the tolerance).
- **Goal:** prove the epic's promise in one live run. On the real outpost, with nothing injected, every crawler works, trades, eats, socialises and sleeps; both seeded bounties are fulfilled; and nothing looks wrong on screen. Two runs with the same seed tell the same story. Then Epic MAP is `COMPLETED`.
- **What the producer sees after Phase 5:**
  - **No visible change in normal play,** apart from the jitter fixes: crawlers no longer twitch back and forth at the stall or beside the bunks.
  - A new test, `OutpostLifecycleScenario`, that runs 15 sim-minutes of the real game and checks the whole lifecycle (§2.2). It prints a per-crawler report: harvests, sales, meals, sleeps, hearth visits, trust.
  - A determinism test: the same seed gives the same world (§2.5, D2).
  - Epic MAP marked `COMPLETED` in the roadmap.

---

## 1. Findings from the code (why each step exists)

| # | Finding | Where | Consequence for Phase 5 |
| :-: | :--- | :--- | :--- |
| L1 | **Nothing records what each crawler did.** The simulation counts harvests per item type (not per crawler) and bunk sleeps per crawler. Sales and purchases go through the ledger, but only as totals. Meals are applied in `AgentConsequenceService` (a harvested ration −50 hunger, eating carried food −40) with no record at all. The chronicle can't stand in: it is a 1,024-event ring buffer and doesn't record harvests or meals. | `SimulationManager.cs:222, 257-266`; `AgentConsequenceService.cs:66-74, 150-154` | A per-crawler activity tally, kept by the simulation where each act is applied (§2.1). |
| L2 | **Hearth gatherings are already detected by the simulation.** Every 50 ticks `EvaluateHearthProximity` finds crawlers together at the hearth and records `CampfireGossip` / `CampfireCompanionship`. | `SimulationManager.cs:929-1130` | The tally counts gatherings at that point; no new detection. |
| L3 | **Trust toward the player rises on every fair trade** (+10) and on a Gather delivery. | `TradeIntentHandler.cs:107` | The scenario compares each crawler's trust toward the player at the start and the end. |
| L4 | **No live Floor 1 scenario asserts every anomaly type.** The supply loop checks stacking and thrashing; the bunk scenario checks stacking only; nothing on Floor 1 checks jitter, wall penetration or boundary breaches over a long run. `DynamicChaosScenario` allows up to 2 jitter events. | `OutpostSupplyLoopScenario.cs:219-220`, `BunkRestScenario.cs:169`, `DynamicChaosScenario.cs:117` | The lifecycle scenario asserts all five at zero (`TotalAnomalies == 0`) over 9,000 ticks. |
| L5 | **The jitter check runs on real time, the thrashing check on sim time.** Jitter is "3 reversals of more than 120° within 0.5 s of `Time.time`". The thrashing check divides its window by the clock multiplier; the jitter check doesn't. Tests run at 10×, so the jitter check really looks at 5 sim-seconds (50 ticks), ten times more sensitive than at 1×. The player plays at 1×: the game has no speed control, only pause and resume. The detector also samples once per rendered frame, so in the Editor at 10× it skips ticks, and in batch mode it sees each one. **We don't know how many of Phase 3's 6–8 jitter events the player would ever see.** | `VisualAnomalyDetector.cs:201-245, 262-268`; `SimulationManager.cs:388-396` | The jitter check is measured in ticks (D1), then the jitter that remains is diagnosed and fixed (§2.3). |
| L6 | **The starvation margin is thin, but only in one scenario.** `BunkRestScenario`'s worst case is 275 ticks at 95+ hunger, against its 300-tick limit. The Floor 1 soak (export on, the live setting) had 0 starving episodes in 18,000 ticks on each of 3 seeds. | [Epic §6](../docs/epics/epic-map.md) #2; [Epic 9 §8.2](../docs/epics/epic-9.md) | Assert "no crawler starves" with the soak's definition (95+ for 300 ticks in a row); if it fails, tune food data in the layout, not code (§2.4). |
| L7 | **No whole-world determinism check exists.** `DeterminismValidator` covers seeded randomness, ids and same-tick intent order: 8 pawns, 60 ticks. Cognition and path results still arrive on a tick that depends on thread timing (ROADMAP §1A.2 #6), so two live runs may drift apart. How early is unknown. One hint: the Phase 3 and 4 full runs produced identical milestone ticks three times in a row. | `DeterminismValidator.cs`; ROADMAP §1A.2 #6 | Hash the world and compare two runs (§2.5); what to assert is D2. |
| L8 | **A respawn rewinds the clock and resets the session** (Phase 2), and `SessionSwitchScenario` already respawns inside a test. | `SwarmSpawner` | Two same-seed runs fit in one test. |

---

## 2. Design

### 2.1 The activity tally (L1, L2)
- `AgentActivity` (plain struct) per crawler, kept by `SimulationManager` on the main thread:
  - salvage harvests;
  - salvage sales;
  - Gather deliveries;
  - ration purchases;
  - meals;
  - bunk sleeps (the existing `BunkSleeps` moves in here);
  - hearth gatherings;
  - the tick of each first, so the report can say when the cycle first closed.
- Each count is recorded at the one place the act is applied (the harvest, trade and bounty handlers, the consequence service, `EvaluateHearthProximity`), never inferred from state changes. It is cleared on respawn like the existing counters.
- **Read-only and diagnostic:** nothing in cognition reads it. One dictionary entry per crawler is created when it spawns, so recording allocates nothing.

### 2.2 `OutpostLifecycleScenario` (the epic's DoD)
- Real `OutpostSession`, default seed, 10× (like the other live scenarios), **no injection**. It observes for the full **9,000 ticks** (15 sim-minutes), because "no anomalies" and "no starvation" only mean something over the whole window. Wall-clock timeout 400 s (the run needs about 90 s).
- **Passes when** (the epic's list, made exact):
  1. **Every crawler closes a harvest → sell cycle:** it harvests salvage, then later sells salvage or delivers it to a Gather bounty (D3).
  2. **At least 3 crawlers sleep in a bunk.**
  3. **At least 3 crawlers eat:** a meal recorded in the tally, from food bought or harvested.
  4. **At least one hearth gathering.**
  5. **The seeded Scout bounty is fulfilled** and the Deep Seam is charted; no crawler knows a Deep Seam landmark before the report (the supply loop's fog check).
  6. **The seeded Gather bounty is fulfilled** and paid from escrow.
  7. **Trust toward the player rose for at least one crawler.**
  8. **Zero anomalies of every type** (`TotalAnomalies == 0`).
  9. **The ledger is conserved** at every observation.
  10. **No crawler starves:** no 300 ticks in a row at 95+ hunger.
- The scenario logs a per-crawler table (the tally, peak hunger, longest hungry stretch, trust start → end) and, on any anomaly, the agent, tick, position, room, intent and its last 10 positions, so a failure explains itself.
- The supply loop and the bunk scenario stay as they are: they pin the milestone times; this one pins the whole day.

### 2.3 Zero anomalies: measure what the player sees, then fix it (L4, L5)
1. **The jitter check moves to sim time (D1).** It evaluates once per simulation tick from logical positions, with its window in ticks (5 ticks = 0.5 s at play speed). Its thresholds stay as they are (3 reversals of more than 120°), so a crawler that twitches at 1× is still caught at any test speed. `VisualAnomalyDetectorValidator` gains two checks:
   - a pawn reversing every tick is flagged identically at 1× and 10×;
   - a pawn that turns back once (arrive, then leave) is not flagged.
2. **Measure:** run the lifecycle scenario and take every remaining event with its diagnostic dump (§2.2).
3. **Fix each real cause** in navigation or the motor, one commit per cause, each with the scenario that showed it. Likely candidates, unconfirmed: the separation push between crawlers waiting at the stall, the last step onto a bunk (`StepOntoBunk`) against a neighbour's push, and stop-and-go around the arrival tolerance.
- **Rules:** no threshold is loosened beyond the time-scale correction, and no anomaly is ignored because of the crawler's state. If a fix turns out to need a navigation redesign, I stop and bring the producer the evidence and an estimate before starting it.
- `DynamicChaosScenario`'s allowance of 2 jitter events is left as it is: it injects walls on purpose.

### 2.4 Starvation (L6)
- Measured, not pre-tuned. If criterion 10 fails, the fix is food **data** in `Floor1Layout` (garden yield or regrowth), as the epic's risk table says, never cognition code and never the deferred §3.3 prices. Any such change is reported with before-and-after numbers.

### 2.5 Determinism check (L7, L8, D2)
- **`WorldStateHash`** (`Testing/Environment`): FNV-1a over the authoritative state, in id order:
  - the tick;
  - each agent's id, position (quantised to 1/1000 unit), needs (to 0.01), gold, inventory, current intent type and goal;
  - stall stocks and treasuries;
  - bounties (id, status, claimant);
  - the explored mask;
  - ledger totals.

  Plain data only: no view state.
- **`DeterminismScenario`** (live): respawn the outpost with the default seed and record the hash every 100 ticks up to t3,000. Respawn again with the same seed and compare. It reports the first tick where the hashes differ, and what differs (agent and field).
- **What it asserts is D2.** The recommendation is to assert the strongest property that holds in 3 out of 3 full runs, and to record the measured tolerance in ROADMAP §1A.2 #6:
  - **if the hashes match at t3,000:** exact equality;
  - **otherwise:** an identical world up to the first divergence, and the four milestones (first sale, scout, gather, third sleeper) within ±10% between the runs.

### 2.6 Completion
- Once §2.2 and §2.5 pass three full runs in a row: the epic's §6 open findings are updated (fixed ones struck, the rest handed to their owners), then Epic MAP → `COMPLETED` (ROADMAP first, then the epic header and checklist, then `curentfocus.txt`); `architecture_history.md` gets the Phase 5 entry (the activity tally, jitter in sim time, the determinism result).

---

## 3. Steps and commits

Each step must compile and keep PlayMode green (`scripts/run-playmode-tests.sh`, Editor closed), except where a step's gate says a known failure is expected; then the commit body names it. Each commit body states the real test status.

| Step | Work | Gate | Commit |
| :-: | :--- | :--- | :--- |
| 0 | **This plan;** the producer's Phase 3 and Phase 4 visual checks recorded. | Producer approves §7 | `docs(map): Epic MAP Phase 5 plan (Definition of Done)` |
| 1 | **Activity tally** (§2.1). | A new tally check in `SupplyLoopValidator` (headless): each act increments the right crawler's count once; a respawn clears it. Full suite: milestones unchanged. | `feat(simulation): per-crawler activity tally` |
| 2 | **Jitter measured in sim time** (§2.3 item 1, D1). | `VisualAnomalyDetectorValidator` (two new checks). Full suite; jitter counts before and after reported in the commit body. | `fix(testing): the jitter check measures sim time, not frame time` |
| 3 | **`OutpostLifecycleScenario`** (§2.2), all ten criteria asserted from the start. | Runs to t9,000. If anomalies or other criteria fail, the commit body lists them exactly: they are step 4's input. | `test(map): the outpost lifecycle scenario (Epic MAP DoD)` |
| 4 | **Fix what step 3 found** (§2.3 item 3, §2.4), one commit per cause. | The lifecycle scenario passes; the full suite stays green. | `fix(navigation): …` / `fix(map): …` per cause |
| 5 | **`WorldStateHash` and `DeterminismScenario`** (§2.5), asserting per D2. | Holds in 3 of 3 full runs. | `test(simulation): same seed, same world` |
| 6 | **Full suite ×3,** identical milestones; the lifecycle report goes into §6A. | 54/54 three times | (no code; numbers go into step 7) |
| 7 | **Docs and the producer's visual check.** Epic §6 findings updated; this plan deleted (its deviations summarised in `architecture_history.md`). ROADMAP (Epic MAP `COMPLETED`, §1A.2 #6 tolerance), the epic header, checklist and History, `curentfocus.txt`, `architecture_history.md`, AGENTS.md (the new scenarios). **Producer check:** a few minutes at 1× in the Editor, watching the stall and the bunks for twitching. | Docs agree with each other; producer confirms | `docs: Epic MAP complete` |

---

## 4. Tests added or changed

- **New:**
  - `OutpostLifecycleScenario` (live, no injection, 9,000 ticks);
  - `DeterminismScenario` (live, two same-seed runs to t3,000).
- **Changed:**
  - `VisualAnomalyDetectorValidator` (jitter in sim time, two new checks);
  - `SupplyLoopValidator` (activity tally).
- **Re-checked:** every scenario that counts anomalies, after step 2 (the jitter counts change meaning); the supply-loop and bunk milestones after each step 4 fix.
- **Expected count:** 52 + 2 = **54** PlayMode tests, plus the two explicit runs (soak, probe). The suite gets about 2.5 minutes longer.

---

## 5. Risks

| Risk | Mitigation |
| :--- | :--- |
| The jitter has a deep cause (separation against path following) and no small fix | Diagnose first with the dump; if a fix needs a navigation redesign, stop and bring the producer evidence and an estimate (§2.3) |
| The time-scale correction looks like moving the goalposts | It matches what the thrashing check already does and what the player sees at play speed; both counts go in the commit body; no threshold changes |
| A crawler never closes a harvest → sell cycle (it lives on garden rations, or a bounty keeps it busy) | That is a real finding about the loop. It is reported with the tally; the criterion is not weakened without the producer |
| Starvation over the full 9,000 ticks | Tune food data only (§2.4); before-and-after numbers |
| The determinism assertion passes here and flakes on a slower or busier machine (thread timing, #6) | Assert only what holds in 3 of 3 runs; the docs state that it rests on cognition timing; option B in D2 removes the dependency |
| Step 4's fixes move the milestone ticks of other scenarios | Full suite after every fix; bounds re-measured (slowest of 3 × 1.5) only where a fix explains the change |
| The PR grows while it is under review | Phase 5 is separate commits on top; the reviewer can read it as its own range (`ae58859..`) |

---

## 6. Out of scope
- **Fixing §1A.2 #6** (fixed-latency cognition): only if the producer picks D2 = B.
- **The economy (§3.3)**, the provisional export, drifters: deferred by the producer.
- **Carried findings** that the Definition of Done doesn't need:
  - bunk-claim churn (epic §6 #3);
  - cognition scaling (epic §6 #4, ROADMAP §3A #8);
  - the succession cohesion bonus (epic §6 #5);
  - Gather provenance (epic §6 #6);
  - Pen 7 (epic §6 #7).
- **Noticed while reading, not fixed:** the dossier's *Feed ration* button writes a crawler's hunger directly from the UI (`FacilityDashboardPresenter.cs:1092-1098`, since 2026-10-09). That bypasses typed commands (§1A.1). The lifecycle scenario never presses it. It should become a command or be removed; that's a separate decision.

## 6A. Deviations from this plan during implementation
(Filled in during implementation.)

## 7. Decisions

**D1 (decided by the producer 2026-10-12: A): how jitter is measured.** This decides what "zero anomalies" means.

| Option | Meaning | Trade-off |
| :--- | :--- | :--- |
| **A (recommended)** | The check runs per sim tick with its window in ticks: twitching the player would see at play speed (1×), caught identically at any test speed. Same thresholds. | Matches the thrashing check and the game's actual speed. Some of today's 10× events may disappear; the rest are real and get fixed. |
| B | Keep the real-time window and fix everything it flags in the 10× tests. | Stricter than anything the player can see; we may spend the phase chasing back-and-forth over 5 sim-seconds that looks normal at 1×. |

**D2 (decided by the producer 2026-10-12: A): what the determinism check asserts** (§2.5). The epic already allows recording a tolerance if exact equality doesn't hold.

| Option | Work | Trade-off |
| :--- | :--- | :--- |
| **A (recommended)** | Measure, then assert the strongest property that holds in 3 of 3 runs (exact at t3,000, or identical to the first divergence plus milestones within ±10%), and record it in §1A.2 #6. | Small. Honest about #6 without fixing it in this epic. |
| B | Fix #6 now: cognition and path results apply exactly K ticks after dispatch, and the tick waits for them if they're late. Then assert exact equality. | The real multiplayer-readiness fix, about a day of work. It touches the core tick loop and shifts every milestone by K ticks, so every bound gets re-measured. It belongs with the multiplayer work, which is why it is not recommended here. |

**Minor decisions (producer may override):**
3. **A Gather delivery counts as the "sell" half of the cycle:** delivering salvage to the player's bounty is a sale to the player.
4. **10×, default seed, 9,000 ticks,** as the epic states; the same speed as the other live scenarios.
5. **The activity tally lives in the simulation, not the test,** because only the simulation knows when a meal or a sale happened. It's diagnostic: nothing decides on it. It is also what a later in-game crawler card (UI-3) would read.
6. **Same branch as the open PR** (the producer's request); Phase 5 is separate commits on top of `ae58859`.

# Epic 10, Part 1: The Guild and Its Expeditions

- **Status**: ACTIVE (2026-10-23). G0 (a day that holds on every seed), G1 (drifters) and G2 (health and hazards) are done (2026-10-23); the producer looks at G2 (danger on screen) before G3 (gear) starts. Status is owned by [ROADMAP.md](../ROADMAP.md). Decisions and deviations: `.agents/architecture_history.md`.
- **Track**: Track A: Core Simulation & Cognition Engine. Epic UI-3 (Guild Roster & Expedition Dispatch) is folded in, because its UI is now the KNI client's.
- **Milestone**: Production Milestone 2, Guild Roster & Expeditions (ROADMAP §3).
- **Source**: the Claude Doc "Epic 10, Part 1: The Guild and Its Expeditions" (2026-10-23), approved by the producer with every default the same day.
- **Preceding**: Epic 9 (the outpost economy). **Builds on**: Epic 8 (groups and directives), Epic CORE (the engine-free core, headless tests), Epic CLIENT (the KNI client).
- **Following**: Epic 10 Part 2 (workshops and crafting, Milestone 3) and Part 3 (the descent, Milestone 4).

---

## 0. Producer intent and decisions

On 2026-10-23, after the KNI client merged into `main` (PR #6), the producer set Android and the other engine work aside and asked for the next core-gameplay epic. The draft's ten questions were approved with their defaults:

- **Q1, name and home:** "Epic 10, Part 1: The Guild and Its Expeditions", with Epic UI-3 folded in, in this file.
- **Q2, combat:** none. Milestone 2's dangers come from the environment; creatures and combat come with the descent.
- **Q3, death:** none. A crawler at 0 health is downed: its party brings it back, or it limps home. It misses work while it heals, and its trust in whoever sent it falls.
- **Q4, guild size:** up to four (the game design's Phase 2), so the whole guild can be one party. Larger guilds come with the guildhall (Milestone 3).
- **Q5, recruitment:** trust ≥ 60 and warmth ≥ 40 toward the player (Epic 9's design), plus a signing fee and a wage. The numbers are tuned at G4.
- **Q6, a member's own life:** it eats, sleeps and socializes as before, and works for itself when it has no orders. Guild orders come first, obeyed in proportion to its trust.
- **Q7, drifters:** the 12 residents stay. A few drifters at a time arrive with their own gold and leave after a stay; the rate and the cap are tuned at G1.
- **Q8, where "done" is proven:** headless, in `dotnet test`, with live scenarios on the real session and nothing injected. ROADMAP §1B says so since 2026-10-23. Unity's PlayMode suite stays a regression check on the core while Unity exists.
- **Q9, the haul:** one new rare salvage, found only in hazard sectors and bought only by the caravan, at a premium. It has a placeholder name until art and naming are chosen.
- **Q10, shipping:** one pull request into `main` per milestone, from a `feat/` branch, with its measured tests. The agent never merges. Each milestone has a working plan in `.agents/`, deleted when the milestone closes.

## 1. Goal

**The player promise:** earn their trust at your stall, then send your best four where nobody else dares to go.

The outpost's economy works (Epics 9 and MAP), but the player has nothing to grow into:
- **progress has nowhere to go:** trust and gold build up, and nothing spends them;
- **there is no risk:** only hunger hurts a crawler, and a charted frontier is as safe as the hub;
- **the economy is closed:** with no outside buyers, greedy prices never pay ([economy model](../economy-model.md) §7), and the population never changes;
- **the player has few verbs:** three prices and two bounty types;
- **the day is fragile:** it fails on 7 of 23 seeds (Epic CORE finding F2).

## 2. Design

### 2.1 The core loop

The player never commands a crawler's hands. Each step is a decision the player makes; what happens inside it is the crawlers' own choice.

1. **Trade fairly** (the game today): fair prices and bounties build trust and warmth.
2. **Recruit:** trusted crawlers sign on for a fee and a wage.
3. **Outfit:** gear from the caravan cuts hazard damage.
4. **Dispatch a party:** up to four, with a target, a goal and a retreat rule.
5. **Brave the hazard:** health wears down; the party retreats or pushes on.
6. **Bring back the haul:** rare salvage sells to the caravan at a premium, which pays the wages.

**The stakes:** members who are downed or unpaid lose trust and quit.

The new verbs: recruit, dismiss, set a wage, give gear, set a gear price, form a party, dispatch and recall.

### 2.2 Milestones

| Milestone | What the player sees | Core (engine-free simulation) | Client (KNI) |
| :--- | :--- | :--- | :--- |
| **G0. A day that holds on every seed** | Nobody starves or stalls, whatever the seed | Fix finding F2; the seed sweep becomes a test gate | None |
| **G1. Drifters** | Strangers arrive at the Road Gate, trade and move on | Drifters with their own gold, a named source of money in the ledger; arrival and departure on a seeded schedule; residents stay at 12 | Arrivals and departures in the chronicle; the dossier tells a resident from a drifter |
| **G2. Health and hazards** | One or two dangerous sectors beyond the frontier, with a rare salvage | A health need, injury, downed at 0, recovery in the bunks and faster with herbs; sector hazards and rare salvage nodes as map data; the risk-tolerance trait decides who goes in | Health in the dossier and the cards; hazard sectors tinted on the map |
| **G3. Gear** | The stall sells gear; geared crawlers get hurt less | The caravan supplies the basic weapon and the pickaxe (item types today, with no source); a gear price; gear reduces hazard damage and wears out | A gear row in the merchant terminal |
| **G4. The guild** | Recruit trusted crawlers; pay their wages; they may quit | Recruitment thresholds and a signing fee; the guild as an Epic 8 group with the player as patron; wages each caravan visit; quitting when trust falls; bounty fairness as another path to trust | The guild roster (Epic UI-3); Recruit on the dossier |
| **G5. Expeditions** | Send a party of up to four into a hazard sector; watch it go and come back | Parties; an expedition directive (target, goal, retreat rule); gather, travel together, work, retreat or return; the haul to the stall, a premium from the caravan, a share to the members | Party builder, dispatch and recall; an expedition tracker (stage, health, haul) |
| **G6. The gate** | The whole loop, played and balanced | Every scenario of §3; the soak's bands re-set for the guild; save and load mid-expedition | The producer's play session |

Order: G0 first; G1 (the world), G2 → G3 (danger) and G4 (the guild) are separate tracks that meet at G5; G6 closes. One agent works them in order G0 to G6, so the economy's bands settle before the guild arrives. The producer looks at G2, G5 and G6. Estimate: 13 to 20 agent days (G0 2–3, G1 2–3, G2 2–3, G3 1–2, G4 2–3, G5 3–4, G6 1–2).

### 2.3 Architecture and rules

The roadmap's invariants (§1A) bind this epic.

- **The rules live in the engine-free core** (`Assets/Simpiens.Core`, C# 10, netstandard2.1), tested with `dotnet test`. The client in `dotnet/` shows them and sends commands; Unity's views stay frozen.
- **The player's actions are typed commands** applied in a fixed order on the tick: recruit, dismiss, set a wage, give gear, form a party, dispatch and recall an expedition. Gear prices extend the existing price command.
- **The guild and its parties reuse Epic 8's groups:** the guild is a `FactionGuild` group with the player as patron, a party a `TemporaryParty` inside it, an expedition a new kind of directive. Epic 8's directives come from a crawler leader, so the patron needs its own path of authority, with compliance weighted by trust in the player.
- **Numbers live in the session's tuning** (`EconomyTuning` or a tuning beside it), never as literals.
- **Hazards and rare salvage are map data** on sectors and nodes in `MapDefinition`; Floor 1 grows by one or two sectors beyond the frontier.
- **Durations are in ticks;** arrivals, hazard checks and injuries use the session's seeded random numbers.
- **The planner has room:** 36 of its 64 facts are used.
- **New state is saved by its owner** (health, gear wear, drifters, the guild, wages, parties, expeditions in progress), checked by the round-trip validators, with the save format's version raised when a saved record changes shape.
- **The golden hashes change once per behaviour change, on purpose,** re-recorded in their own commit in .NET and in Unity.
- **Unity stays a check, not a view:** while Unity exists, a core change still passes its PlayMode suite and golden check.
- **Epic 10's old "`Unity.Collections` + Burst" deliverable is dropped:** the core is engine-free. G1 and G5 measure the cost of a larger population; anything faster is built only on a measured need.

## 3. Definition of Done

Each milestone is done when its outcome emerges in the real outpost, with nothing injected, within a bounded number of ticks set from the first measurement. The scenarios run headless in `dotnet test` (Q8). A **scripted player** acts only through the typed commands a real player sends, as the economy soak's profiles do; those commands are the player's input, not an injected precondition.

| Milestone | Live scenario: what must happen on its own |
| :--- | :--- |
| G0 | The 15-minute day passes its checks on all 23 seeds: nobody starves, every crawler completes a harvest-and-sell cycle, both bounties are delivered, the ledger balances. |
| G1 | Drifters arrive, trade and leave; their money shows in the ledger under its own name. The fair player's soak stays in its band, and a slightly greedy player does better than in a closed economy. |
| G2 | A bold crawler enters a hazard sector on its own, is hurt, retreats or is downed, and recovers in the bunks, sooner with a herb. A cautious crawler stays out. |
| G3 | A crawler buys gear at the stall and, in the same sector, takes less damage than one without. |
| G4 | Playing fair prices only, the scripted player sees a crawler reach the threshold within one day and recruits it. Wages are paid each caravan visit; a member who goes unpaid loses trust and quits. |
| G5 | The scripted player dispatches a party of four to a hazard sector. It gathers, travels together, works and returns with rare salvage; the caravan pays the premium and the members their share; a geared party returns with nobody downed. |
| G6 | One scenario plays the whole loop. A save made mid-expedition loads byte for byte and plays on. The soak's bands hold with the guild. The producer plays it. |

Each milestone also needs its validators, the golden check and the client's tests.

## 4. Acceptance checklist (delivered work only)

- [x] **G0. A day that holds on every seed** (2026-10-23, `feat/epic10-g0`): finding F2 resolved (§7).
    - The navigation and planning fixes `88265d8`: an occupied destination no longer freezes a crawler; a sale is planned only for what the stall will buy; the last step onto a bunk waits for it to clear; a panicking crawler sidesteps and a panic lasts at most 6 s; the food search waits at the nearest known plot.
    - The gate: `OutpostScenarioTests.TheOutpostsDayHoldsOnEverySeed` plays the day on the 23 gate seeds (`OutpostWatch.GateSeeds`) in `dotnet test`, with pawn stacking checked as Unity's anomaly detector does.
    - The greedy soak band is judged at its lowest trust mean (economy model §7).
    - Golden hashes re-recorded in Unity `91066cb`; the client's references and pins re-recorded in the pinned image `0e8fb1f`.
    - Measured: the gate seeds 23/23 (16/23 before), no stacking; seeds 21–120: 96/100; `dotnet test` 347/347 (core 86, client 261); the client's 261/261 exactly in the pinned image; golden 3/3 in both runtimes; Unity PlayMode 69/69 (2 explicit skipped); the economy soak 9/9 headless and in Unity (fair 701–785g in Unity).

- [x] **G1. Drifters** (2026-10-23, `feat/epic10-g1`, stacked on G0): the outpost's economy is open (Q7).
    - Drifters `245da04`: strangers on a seeded schedule (`DrifterTuning`: one about every 3.5 minutes from minute 2, at most 3 at once, a 3–5 minute stay, 20–26g, arriving hungry) come in at a free Road Gate spawn point, live as crawlers, and leave by deciding to (`LeaveOutpostGoal`). Their purses are the `DrifterPurses` faucet, what they take away the `DrifterDepartures` sink; a claimed bounty goes back on the board and a bunk is freed. `AgentState.DepartsAtTick` and the host's schedule are saved (format 2). The chronicle tells arrivals and departures; the dossier names a drifter's stay.
    - Found with drifters about, and fixed in the same commit: walkers keep right around a crawler in the way (head-on jams in doorways); salvage the stall won't take no longer stops a crawler from gathering what it still buys; food past the 30-unit search is still food; the walk to food stops at 0.7, inside the 0.8 harvest reach.
    - Cross-runtime: `Vec2`'s scalar chains round to float after every step, so Unity's Mono matches CoreCLR on the borderline distance tests that had parted two golden seeds (Epic CORE F1, narrowed).
    - Live scenarios: `DriftersComeBuyAndGo` (in the 15-minute day 4–6 drifters leave, having bought; both ledger flows show; the gold balances), `ASaveWithDriftersLoadsAndTheirScheduleResumes`, `AKeenPlayerDoesBetterWithDrifters` (2,316g against 2,154g closed over the soak seeds; drifters leave 35–139g behind per seed), and in the client `TheGameDrawsDriftersComingAndGoing`.
    - Golden hashes re-recorded in Unity `d80944b`; the client's references and pins `6a275a0`.
    - Measured: the gate seeds 23/23 with drifters, no stacking; seeds 21–120: 98/100 (F3); `dotnet test` 352/352 (core 89, client 263); the client's 263/263 exactly in the pinned image; golden 3/3 in both runtimes; Unity PlayMode 69/69 (2 explicit skipped); the soak 9/9 headless (fair 768–831g) and in Unity (fair 805–831g).

- [x] **G2. Health and hazards** (2026-10-23, `feat/epic10-g2`, stacked on G1): the floor has danger (Q2 no combat, Q3 no death, Q9 one rare salvage).
    - Hazards and health `a6470ea`: the rock either side of the lower wing became the Sump (foul air, 3 health a second; door from the Ore Quarry) and the Glow Caves (falling rock, 4 a second; door from the Herb Grove), both charted, each with three glowstone nodes (one unit, regrowing in 10 minutes). Hazards are map data (`SectorDefinition.Hazard`). `AgentState.Health` is saved (format 3): a crawler at 0 is downed and limps to a bunk, where it heals; a herb heals 30. Reflexes (`MentalBreakEvaluator`): treat wounds below 60, fetch a herb when hurt and not hungry, retreat from a hazard below 40, rest by the bunks when hurt. Only a crawler with risk tolerance of 30% or more and health of 80 or more targets hazard salvage, explores a hazard sector or wanders into one (`Vitality.WouldVenture`). The chronicle tells who was downed and who got up.
    - Glowstone: the stall buys it at 45g (player-set; a fourth row in the merchant terminal and the price command); the caravan buys up to 2 a visit at 70g (`CaravanSalvageSales`). `TradeStallData` gains its price and stock.
    - Client: health in the dossier, DOWNED on the dossier and the resident cards; hazard floors lean red and their danger ("FOUL AIR", "FALLING ROCK") is written under the room's name; a glowstone sprite.
    - Cross-runtime: the golden seeds parted because A* broke ties differently in Unity's Mono (`PathNode.FCost` and the octile heuristic kept at double precision); the searcher, the snapshot's line checks and `IsPositionBlocked` now round each float step (Epic CORE F1, narrowed again).
    - Live scenario: `TheBoldBraveTheHazardsAndHealAndTheCautiousStayOut` (the 15-minute day on the default seed, nothing injected: a bold crawler goes in on its own, is hurt below 70 and recovers to 80 or more; herbs are eaten to heal; no cautious crawler sets foot in a hazard; the caravan buys glowstone at its premium; the day's checks still hold).
    - Golden hashes re-recorded in Unity `c6055f3`; the client's references and pins `2fcc2a0`.
    - Measured: the gate seeds 23/23, no stacking; seeds 1–120 with the golden seeds 120/123 (F3: 31 and 43; seed 73 one crawler's hunger peak); `dotnet test` 355/355 (core 90, client 265); the client's 265/265 exactly in the pinned image; golden 3/3 in both runtimes; Unity PlayMode 69/69 (2 explicit skipped); the soak 9/9 headless and in Unity with identical numbers (fair 1,545–1,594g in the re-set band).

## 5. Deferred and out of scope

| Left out | Owner |
| :--- | :--- |
| The descent gate, its boss and moving the outpost to the next floor | Epic 10 Part 3, Milestone 4 |
| Workshops, hired artisans and crafting | Epic 10 Part 2 with Epic UI-4, Milestone 3 |
| Creatures and combat | With the descent, Milestone 4 |
| A guild larger than four, barracks and a guildhall | Milestone 3 |
| Tavern rumours written by a language model | Epic 11 |
| Android, screen density and a frame cap | Epic CLIENT M4 (paused) |
| Removing Unity after the side-by-side look | Epic CLIENT M2.8 and M2.9 (paused) |
| A planner per thread and a cognition budget (ROADMAP §3A #8) | Only if G1 or G5 measures a need |
| Art beyond the generated placeholders | Until the producer picks an art direction |
| Multiplayer | After the MVP; this epic keeps it possible (ROADMAP §1A.1) |

## 6. Risks

| Risk | What we do about it |
| :--- | :--- |
| New money (drifters, rare salvage) and new costs (wages, gear wear) push the economy out of the soak's bands | Re-measure the bands at every milestone; update the economy model with each change |
| Finding F2 has a deeper cause, in pathing or in the food supply | G0 is time-boxed at 3 days; a seed that still fails is recorded, with its cause, as its own finding |
| Epic 8's directives assume a crawler leader, not the player | G4 opens with a short design note on the player as patron, before any code |
| More crawlers cost more planning time (one locked planner, ROADMAP §1A.2 #8) | Measure at G1 and G5, headless and against the frame-time gate; build per-thread planning only on a measured need |
| Every core change costs minutes in Unity while Unity remains | Batch core changes per milestone for the Unity check |
| The dashboard is laid out for 1280×720; the roster and the tracker need room | Make them windows, like the merchant and the bounty board |
| The save format changes at most milestones | Old saves are refused with a reason, as today; no migration before release |

## 7. Findings

- **F2 (from Epic CORE): resolved by G0 (2026-10-23).** The day failed on 7 of 23 seeds (re-measured on `main`: seeds 4, 10, 11, 12 and 18 had a starving crawler, seeds 8 and 16 a crawler with no harvest-to-sell cycle). No food shortage: every failure was a crawler that could not move or retried something that could not work.
    1. **An occupied destination froze a crawler across the map.** The motor waited while its next waypoint was occupied, and a smoothed path's next waypoint is often the destination: one crawler idling at the stall stopped another in the quarry. The check now applies only near the waypoint.
    2. **A refused sale, retried every tick.** The planner asked whether the stall had room for either salvage; it now asks about what the crawler holds, as the liquidate goal did.
    3. **Lying down on a crawler still in its bunk** (once walkers reached occupied bunks): the last step onto a bunk now waits for the bunk to clear.
    4. **Panic in place.** A panicking crawler blocked by another skipped every waypoint and panicked again on the spot; it now sidesteps, and a panic lasts at most 6 s.
    5. **Searching away from food.** A hungry crawler among empty plots explored the Road Gate and starved on the walk back; the food search now waits at the nearest plot it has seen, as the starvation reflex does.

    Measured: 23 of 23 gate seeds pass, with no pawn stacking; seeds 21–120, 96 of 100.
- **F3 (2026-10-23): on some seeds trust rises for fewer than ⅔ of crawlers.** Seeds 31, 43, 61 and 69 (of 21–120) end with trust up for 6 or 7 of 12 (the day asks for 8). Those crawlers trade two or three times early, spend the day at the hearth, and their trust drifts back to neutral. A balance question, not a stuck crawler: G1 (drifters) and G4 (bounty fairness) add paths to trust; re-measure there. **Re-measured after G1:** seeds 61 and 64 (7 of 12); drifters, who pass through, do not count toward it. **After G2:** seeds 31 and 43 (7 of 12); seed 73 also fails, on one crawler's hunger peak (100) rather than trust, a borderline worth watching in G3.
- **The fair soak band is re-set to 1,000–2,200g (G2, 2026-10-23; to confirm at the producer's look).** Glowstone is new money by design (the caravan's premium, Q9): with it the fair player ends at 1,545–1,594g, against the old band of 375–875g. The epic's G2 Definition of Done asked that the soak keep its bands; tuning glowstone down to fit would have made the hazards not worth the risk. Wages (G4) are the cost meant to meet it ([economy model](../economy-model.md) §7).
- **The greedy soak band is judged at its lowest trust mean (2026-10-23).** With the G0 fixes, hungry crawlers eat at the Garden instead of buying gouged rations late in the run; at greedy prices nobody trades once the stall is empty, and trust drifts back to neutral by minute 30 (seed 2: mean 0.0). The band now asks that greed pushed the mean below 0 at some point, as the generous band is judged at its peak ([economy model](../economy-model.md) §7).

## History

- 2026-10-23: drafted as a Claude Doc and approved by the producer with every default; G0 started on `feat/epic10-g0`.
- 2026-10-23: G0 done: finding F2 resolved, the day gated on 23 seeds; findings F3 (trust on some seeds) and the greedy band recorded. Its working plan was deleted at close-out.
- 2026-10-23: G1 done: drifters open the economy; four navigation and planning faults found and fixed with them; Vec2 rounds alike in both runtimes. Its working plan was deleted at close-out.
- 2026-10-23: G2 done: health, the Sump and the Glow Caves, glowstone; the fair band re-set; A* rounds alike in both runtimes. Its working plan was deleted at close-out.

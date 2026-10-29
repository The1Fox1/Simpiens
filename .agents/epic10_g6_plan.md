# Epic 10 G6: the gate, and Part 1's close-out (working plan)

Working document (AGENTS.md, Documentation Protocol 6), one of Epic 10's plans ([index](epic10_plan.md)). Written 2026-10-29. It starts after G5's PR merges and the producer's look at G5 passes. It is deleted in Part 1's close-out (G6.7).

- **Goal** (epic §2.2, the G6 row):
  - the whole loop plays;
  - a game saved mid-expedition loads and plays on;
  - the economy holds with a guild;
  - the producer plays it.
  - Then Part 1 closes.
- **Branch:** `feat/epic10-g6`, cut from `main` after G5's PR merges. One PR into `main`, carrying the close-out.
- **Estimate:** 1–2 agent days.
- **The producer's stop:** the play session (G6.5).

## 1. Measured (2026-10-29)
| # | What | Measured | Decides |
| :-: | :--- | :--- | :--- |
| M6 | The fair treasury at 30 minutes | 881–1,797g over 15 seeds. On the soak's three seeds, 1,499–1,564g, inside its 1,000–2,000g band. | The fair band stays for a player without a guild. G6 adds a guild profile (D14). |

## 2. Decision (a default; the producer answers only if they disagree)
| # | Decision | Default | Alternative |
| :-: | :--- | :--- | :--- |
| D14 | **The soak with a guild** | A `guild` profile: fair prices plus the scripted guild player.<br>Its band is set from G6.3's measurement with the producer's word, as G2 re-set the fair band.<br>The fair, greedy and generous bands stay as they are, since none of those profiles recruits. | Fold the guild into the fair profile and re-set the fair band. |

## 3. Steps
Each step is a commit. After each: a build with 0 warnings and `dotnet test`.
- [ ] **G6.1 `TheGuildLoopPlaysThrough`:** one scenario, 30 minutes on the default seed.
  - **The scripted player plays the whole loop:**
    - fair prices;
    - recruiting as crawlers qualify, with wages every visit;
    - outfitting, then sending the party once four have signed and the seam is ready;
    - the haul sold, and the loop again.
  - **Asserted:**
    - four signed, every wage paid, and nobody quit;
    - at least two expeditions back with glowstone;
    - trust up for 8 or more of the 12;
    - recruiting, dispatching and disbanding a party never take a member out of its settlement (G4's D1);
    - nobody starving, the ledger balanced, and the day's checks.
- [ ] **G6.2 `ASaveMidExpeditionLoadsAndPlaysOn`:**
  - The game is saved while the party works the seam, and loaded into a fresh session.
  - The capture is byte for byte, and the world hash is equal, groups and the guild included.
  - Then 3,000 more ticks: the party comes back with its haul, wages are paid, the ledger balances, and no intent faults.
- [ ] **G6.3 The soak with a guild (D14):**
  - measure the guild profile on the soak's seeds and set its band: treasury range, no unpaid visit, expeditions back, trust, nobody starving;
  - re-check the other three bands;
  - re-measure F4 with wages, then close it or keep it (epic §7).
- [ ] **G6.4 Every milestone's scenario together** (epic §3, G6):
  - the full suite;
  - the 23 gate seeds, with and without the guild player;
  - seeds 1–120 for F3 and F5.
- [ ] **G6.5 The producer's play session:**
  - **The build:** `dotnet run --project dotnet/Simpiens.Desktop -- --seed 424242`, where four crawlers qualify by t8,520 (M1).
  - **A short script:** earn trust, recruit, set a wage, outfit, send the party to the Drowned Gallery, recall it once, save mid-expedition and load.
  - **Stop** for the verdict. Each fix it asks for is a commit of its own; then the session again.
- [ ] **G6.6 The G6 PR into `main`,** carrying the close-out.
- [ ] **G6.7 Close-out,** in the docs protocol's order:
  1. **The roadmap:**
     - Epic 10's row: Part 1 COMPLETED, with the date; Parts 2 and 3 QUEUED, each with its draft plan;
     - Milestone 2's status, written as Milestone 1's is;
     - Epic UI-3, delivered within Part 1;
     - the operating rhythm and the chart.
  2. **The epic:**
     - the Status header;
     - §4, with G4–G6 as delivered;
     - §5's Deferred, with owners (§4 below);
     - §7's F3, F4 and F5, each resolved or handed on with an owner;
     - History.
  3. **The rest:**
     - the focus file;
     - `architecture_history.md`: D14 and Part 1 in summary (D1–D6 went in at G4's close, D7–D13 at G5's);
     - the economy model: the guild's flows and its band;
     - AGENTS.md.
  4. This plan is deleted in the close-out commit, and the index's Part 1 rows become one line. Nothing links to the deleted plans.
  5. **Next:** the core refactor's R1–R6, then answering Part 2's draft (index, Order).

## 4. Part 1's Deferred (into the epic's §5 at G6.7, if the defaults stand)
| Left out | Owner |
| :--- | :--- |
| A guild larger than four; several parties at once; a FormParty command | Part 2, the guildhall (W5) |
| Gear making lone crawlers bolder (epic §5 put it in G5; G5's D10 settles it for parties) | Part 2, gear for each hazard (W3) |
| The pickaxe; gear for each hazard kind, better grades, repair | Part 2 (W1, W3, W4) |
| Lodging fees or other crawler sinks | G4.7's measurement; otherwise Part 2 (W5) |
| Carrying a downed member | Part 3, with combat (L2) |
| Expeditions into uncharted sectors (scouting as an order) | Part 3 (L3) |
| Creatures, combat, a weapon; the descent gate, its boss, moving the outpost | Part 3 (L1–L4) |
| Drifters signing on | Later: they pass through (Q7) |
| More than one group of a kind per crawler | The milestone that needs it (G4's D1) |
| Settlement leaders issuing directives in play | Not planned; Epic 8's mechanism stays tested by validators and the Dev Map |
| A planner per thread and a cognition budget (ROADMAP §3A #8) | The core refactor's R6, or a measured need |
| Art beyond the generated placeholders; Android; multiplayer; tavern rumours | As epic §5 says today |

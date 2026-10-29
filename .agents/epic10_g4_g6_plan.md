# Epic 10 Part 1, G4–G6: the guild, expeditions and the gate (working plan)

Working document (AGENTS.md, Documentation Protocol 6). It is one plan for the rest of Part 1, written on 2026-10-29 at the producer's request ("an implementation plan to finish and close out Epic 10"). It replaces the one-plan-per-milestone rule of Q10: each milestone's section below is that milestone's working plan. When a milestone closes, its section shrinks to one line and its open findings move to the epic. The file is deleted in Part 1's close-out (G6.7).

- **Scope.** "Epic 10" here means Part 1, The Guild and Its Expeditions (Milestone 2), the only part that is ACTIVE. Parts 2 (workshops, Milestone 3) and 3 (the descent, Milestone 4) are QUEUED and not drafted. Closing Part 1 leaves them QUEUED.
- **Epic sections used** (`docs/epics/epic-10.md`):
  - §2.2: the G4, G5 and G6 rows;
  - §2.3: architecture;
  - §3: the Definition of Done;
  - §6: the risk row "G4 opens with a short design note on the player as patron, before any code". D1–D3 below are that note;
  - §7: F3, F4 and F5, which are re-measured here.
- **Branches:** one PR per milestone into `main`, never stacked (Q10, and the G1/G2 incident).
  - This plan's commit is on `feat/epic10-g4`, cut from `origin/main` (`852902b`) before PR #16 merged. It is rebased onto `main` before any G4 code (step 0.2).
  - `feat/epic10-g5` is cut from `main` after the G4 PR merges, and `feat/epic10-g6` after G5's.
- **Estimate:** 7–10 agent days in all.
  - G4: 2–3 days.
  - G5: 4–5 days. The epic said 3–4; the chamber (D11) and the tracker add about a day.
  - G6: 1–2 days.
- **Stops:**
  - The producer's go on this plan. Silence on a decision keeps its default, but silence is not a go.
  - Their look at G5 (the epic: "the producer looks at G5 and G6").
  - Their play session at G6.

## 0. Before G4
1. PR #16 (graphics V0–V6) merges. Then, as its ABOUT.md says:
   - zip `.agents/local/graphics-v3-v5/` into `.agents/local/archive/graphics-v3-v5_<date>.zip`;
   - check the zip;
   - delete the folder.
2. `feat/epic10-g4` is rebased onto `main`. It holds one unpublished docs commit.
3. Nothing else blocks G4. G3's defaults, the fair band and the drifter tuning stand (the focus file).

## 1. Measured before planning (2026-10-29)
- **The probe:** a scratch program, not committed, on `feat/graphics-catalogue`. That branch's only core change, an item's art key, changes no rule.
- **The run:** the outpost at its default prices (the fair player's) for 30 sim-minutes, sampled every 10 ticks.
- **Seeds:** 15 in all, the golden three and 1–12. Glowstone was measured on the first 10.

| # | What | Measured | What it decides |
| :-: | :--- | :--- | :--- |
| M1 | Q5's threshold: trust ≥ 60 and warmth ≥ 40 toward the player | First reached at t1,170–6,110 on 15 of 15 seeds (median t1,820), inside the 15-minute day. A fourth crawler gets there within 30 minutes on 14 of 15 seeds (t3,340–16,390); seed 2 has only two. On the default seed, 20261009, the first qualifies at t1,250 and the fourth at t12,830. | G4's DoD holds as written, with Q5's numbers. A guild of four is a 15–30 minute goal. |
| M2 | Epic 8's groups in play | On every seed, 7–12 of the 12 residents were in a hearth Settlement at some point, and 2–4 settlements remain at the end.<br>• A crawler is in one group at most: `GroupRegistry.AddMember` takes it out of its last one.<br>• Succession counts a leader that isn't a world entity as incapacitated (`SuccessionEngine.IsLeaderIncapacitatedOrDiscredited`), so it would hand a player-led group to a crawler.<br>• Nothing in the live outpost issues a directive: only validators and the Dev Map's pens call `SetDirective`. | D1 |
| M3 | Glowstone | Bold crawlers going alone take 9–13 of the roughly 18 the six nodes yield in 30 minutes, on 9 of 10 seeds (2 on seed 6). On average 2.0–2.7 of the 6 nodes are ready (3.5 and 5.3 on two quiet seeds). | D11: a party sent into the Sump or the Glow Caves would find 1–1.5 ready nodes, already worked by the bold. |
| M4 | Residents' purses | 70–245g between the 12 at 30 minutes; the richest holds 10–55g. Crawlers spend what they earn. | Wages are a transfer: what members don't spend at the stall stays in their purses. Measured again at G4.4. |
| M5 | Who would go into danger alone (risk tolerance ≥ 30%) | 2–7 of the 12 residents on each seed. | D10: a guild chosen by trust is mostly cautious, so orders have to bring cautious members along. |
| M6 | The fair treasury at 30 minutes | 881–1,797g over the 15 seeds. On the soak's three seeds, 1,499–1,564g, inside its 1,000–2,000g band. | The fair band stays for a player without a guild. G6 adds a guild profile (D14). |

## 2. Decisions to confirm (defaults; the producer answers only where they disagree)
D1–D3 are the design note on the player as patron that the epic's risk row asks for. They go into the epic at G4.1.

| # | Decision | Default | Alternative |
| :-: | :--- | :--- | :--- |
| D1 | **What the guild is** | **A contract with the player, alongside Epic 8's groups.**<br>• A member keeps its settlement, its hearth and its own life (Q6).<br>• The guild is plain data in `Simulation/Guild/`: the patron, and the members in join order.<br>• Membership is a second record on the crawler (`AgentState.Guild`). The guild is saved in a block of its own; the membership is saved with the crawler.<br>• Reused from Epic 8: the shape of the compliance formula (D8) and of the directive (G5's order). Epic 8's code is untouched.<br>**This deviates from §2.3**, which made the guild a `FactionGuild` group and a party a `TemporaryParty` inside it. | §2.3 as written. The costs:<br>• recruiting takes a crawler out of its settlement, and most residents are in one (M2);<br>• succession has to skip a player-led group;<br>• a party still can't be a second group, since a crawler has one group, so it ends up a list on the guild anyway. |
| D2 | **Recruiting** | The player's Recruit command names a crawler and a wage.<br>**Who can be recruited:** a resident (not a drifter), not downed, at trust ≥ 60 and warmth ≥ 40 (Q5). The guild must have room (four at most, Q4), and the crawler must not have quit or been dismissed in the last 5 minutes.<br>**The terms:** the stall pays a signing fee, 20g by default. The crawler signs if the wage is at least its reservation wage: 5g per caravan visit × its greed factor (as its calm values are). | No fee: the wage is the only cost. |
| D3 | **Wages** | **When:** on each caravan visit. The visit's order becomes exports, upkeep, rations, **wages**, gear: food, then people, then stock.<br>**How:** in join order, each wage paid in full or not at all (no debt, as with upkeep).<br>**A paid member:** +2 trust and +1 warmth. The payment counts as an interaction, so a paid member's trust doesn't drift back to neutral.<br>**An unpaid member:** −10 trust, −5 warmth.<br>**In the ledger:** fees and wages are transfers between the stall and crawlers, conserved and counted for observation (`EconomyFlowTotals`). No new faucet or sink. | Wages on a clock of their own (every sim-minute). |
| D4 | **Quitting** | A member quits when its trust toward the player falls below 30, or after 2 unpaid visits in a row. The player can dismiss a member (−5 trust). After either, it can't sign again for 5 minutes. | Quit at the recruitment threshold (no hysteresis). |
| D5 | **Bounty fairness** (§2.2's G4 row, F3) | A delivery is judged as a sale is: the reward against the crawler's own minimum for the work.<br>• **The minimum:** for a Gather, its items' minimum sale prices; for a Scout, a scouting value of 5g × the crawler's greed factor.<br>• **Generous (≥ 3× the minimum):** today's flat +15 trust, +10 warmth, +10 respect.<br>• **Fair (≥ 1.5×):** +8 trust, +5 warmth, +5 respect.<br>• **Stingy:** −5 trust, −3 warmth, no change in respect.<br>The seeded bounties (25g for an ore, 15g for a scout) stay generous to a neutral crawler. | Bounties keep their flat reward, and bounty fairness goes to Deferred. |
| D6 | **The guild on screen** | Recruit on the dossier, showing progress toward the threshold, the fee and the wage; a refusal says why. The roster is a window, like the merchant and the bounty board (epic §6), opened by GUILD in the header beside an "n/4" count. | The roster as a fourth panel (there is no room at 1280×720). |
| D7 | **Sending a party** | One command, Dispatch, carries the party (one to four members), the target sector, the goal (up to N glowstone) and the retreat rule. The party is put together in the roster window, so "form a party" belongs to the UI, not the simulation. Recall brings the party home. One expedition at a time, since the guild is one party. | A FormParty command as well, holding a party that does nothing until it is sent. |
| D8 | **Obeying the patron** (Q6) | Epic 8's compliance formula, with the player's trust in place of fear:<br>U = trust × 0.35 + respect × 0.25 + allegiance × 0.25 + warmth × 0.15 − hunger × 0.30 − frustration × 0.20.<br>A member complies at 20 or more, as in Epic 8. Allegiance starts at 70 (Epic 8's starting value). It rises with paid visits and returned expeditions, and falls with unpaid visits and being downed. A member that doesn't comply stays behind, and the tracker says so. | Epic 8's formula unchanged (fear and respect toward the player). |
| D9 | **An expedition** | Stages held by the simulation, and saved:<br>1. **Muster** at the hub, until every member is there, fed (hunger below 50) and at health 80 or more, or 90 s have passed. Anyone not there by then stays behind.<br>2. **March** together to the sector's door, in file behind the first member, at the slowest member's pace.<br>3. **Work:** each member takes a ready node, claimed in id order. The party holds the sector until the goal is met, the nodes are spent or the retreat rule fires.<br>4. **Return** to the stall.<br>5. **Deliver.**<br>6. **Done.**<br>Members follow the stage from the snapshot: one goal, one action per stage, and at most 4 new planner facts (39 → 43 of 64). | GOAP alone, with no stage machine: each member plans the trip itself, and nothing holds the party together. |
| D10 | **Danger under orders** | **Who goes:** a complying member goes into danger whatever its boldness, from health 80 as today.<br>**When to turn back:** the player's retreat rule: Cautious 70, Steady 55 or Bold 40. 40 stays everyone's floor (G2's retreat reflex).<br>**When a member is downed:** it limps home, and the party retreats at its pace (Q3: "its party brings it back"). Its trust in the player falls by 10.<br>**Going alone** is unchanged: gear doesn't make lone crawlers bolder. | Boldness still decides who goes: the party is whichever members would go alone (2–7 residents per seed are bold, M5). |
| D11 | **Somewhere nobody else dares** (M3; the epic's §1 promise) | **A new hazard chamber off the Sump:** placeholder name "the Deep Sump", taking 6 health a second (3 with gear), with one rich glowstone seam (yield 4, regrowing in 10 minutes). It is charted from the start.<br>**A severity limit on going alone:** a crawler goes alone only where a hazard takes 4 a second or less.<br>So only an expedition works the seam, and the lone crawlers' economy (and with it the fair band) is unchanged. | No new map: expeditions go to the Sump and the Glow Caves, where a trip finds 1–1.5 ready nodes (M3). |
| D12 | **Giving gear** | The player gives a member hazard gear from the stall's stock. No gold changes hands: the stock is the player's, and the caravan restocks it as today. The party builder offers "outfit from stock". | Members buy their own gear before setting out. |
| D13 | **The haul** | The haul belongs to the guild.<br>• Members hand their glowstone to the stall without a sale.<br>• They split 30% of its caravan value (21g a glowstone), paid on delivery, a transfer like wages.<br>• The caravan buys glowstone as today: 70g, two a visit.<br>• A guild delivery closes a member's harvest-to-sell cycle. | Members sell their haul at the stall's price, as anyone does, leaving the guild no margin. |
| D14 | **The soak with a guild** | A `guild` profile: fair prices plus the scripted guild player. Its band is set from G6's measurement, as G2 re-set the fair band. The fair, greedy and generous bands stay as they are, since none of those profiles recruits. | Fold the guild into the fair profile and re-set the fair band. |

## G4. The guild
**Goal:**
- trusted crawlers sign on for a fee and a wage;
- paid members stay, and unpaid ones quit;
- a bounty's reward is judged as fairly as a trade.

**Design.** Numbers live in `GuildTuning`, beside `DrifterTuning` and `GearTuning` in `EconomyTuning`, never as literals.
- **State:**
  - `AgentState.Guild`: a `GuildMembership` holding the join tick, the wage, allegiance and unpaid visits in a row; `None` for a non-member.
  - The roster, `Guild`: the patron's id, the members in join order, and cooldowns by crawler.
  - The patron is always the player for now. Keeping its id in the data costs nothing and leaves room for a guild per player after the MVP (ROADMAP §1A.1).
- **Commands:** typed, in `InfluenceEvent`, applied in queue order on the tick.
  - `RecruitCommand` (a crawler and a wage), `DismissCommand` and `SetWageCommand`.
  - One rule decides each and explains a refusal to the dossier: `GuildRules`, as `BountyRules` does for bounties.
- **The caravan's visit** pays wages after the rations (D3), through `ISupplyHost`. The ledger's conservation check covers fees and wages as it does purchases.
- **Bounty fairness (D5):** `TradeFairness.ClassifyBounty`, with its deltas in `TradeTrustTuning`. `DeliverBountyIntentHandler` uses them in place of the flat +15.
- **Save format 7:** the guild's block and the member's record. The round-trip validators check both, and the world hash covers both.
- **The scripted player:** `Simpiens.Hosting.GuildPlayer`.
  - It acts only through commands: it sets its prices, then recruits the first resident who qualifies, at the default wage, up to a target size.
  - It is shared by the DoD scenarios, G6's soak profile and the console (`simpiens run --player guild`, `soak --profile guild`).
- **Client:**
  - **Dossier:** "Guild: member, 5g a visit", "Will join (fee 20g)" or "Not yet: trust 45 of 60, warmth 22 of 40", and Recruit.
  - **Roster window:** a card per member, with Dismiss. Each card shows:
    - name, health and DOWNED;
    - gear and its wear;
    - trust and warmth toward the player;
    - the wage, with a slider that sends Set Wage;
    - unpaid visits;
    - what the member is doing.
  - **Header:** GUILD, with "n/4".
  - **Chronicle:** joined, couldn't be paid, quit, dismissed.

**Steps.** Each step is a commit. After each: a build with 0 warnings and `dotnet test`.
- [ ] G4.1 Docs:
  - D1–D6 into the epic: §2.3, and a note on the patron, with D1 recorded as a deviation;
  - the roadmap's Epic 10 line: G4 under way, with this plan;
  - the focus file.
- [ ] G4.2 Bounty fairness (D5):
  - the rule, the handler and the tuning;
  - `BountyFulfillmentValidator`: generous, fair and stingy, by greed.
  - Re-measure F3 on seeds 1–120. Re-record the golden hashes in a commit of their own.
- [ ] G4.3 The guild's state and commands (D1, D2):
  - the tuning, the membership, the roster;
  - the three commands, their rule and the fee;
  - save format 7.
  - Validators: `GuildValidator` (the rule and each refusal, the fee, four at most, the cooldown, dismissal) and the round-trip validators.
- [ ] G4.4 Wages and quitting (D3, D4):
  - the visit's wages; trust and allegiance; unpaid visits; quitting; the chronicle;
  - `GuildWageValidator`: paid, unpaid, quitting after two, the trust floor, the ledger.
  - **Measure** a guild of four at 5g, played by the scripted player on the soak's seeds: the treasury, the members' purses (M4) and trust.
  - If purses climb, carry it to G6. The economy model's deferred crawler sink comes back only on that measurement.
- [ ] G4.5 The scripted player and the live scenarios, headless.
- [ ] G4.6 The client: the dossier, the roster window, the header; `DashboardModelTests` for the guild's model; render tests.
- [ ] G4.7 Finish:
  - the golden hashes, in a commit of their own;
  - the client's references and pins, re-recorded in the pinned image, in a commit of their own;
  - the economy model: §1, §2 and §4 (fees, wages, bounty fairness);
  - the epic: §4's G4 entry and §7's F3;
  - the roadmap, the focus file and `architecture_history.md`;
  - AGENTS.md: `Simulation/Guild/`, the commands, `GuildPlayer`;
  - this section shrinks to a line;
  - the PR into `main`.

**Definition of Done** (epic §3, G4). Headless, on the real outpost, with nothing injected; the scripted player acts only through commands.
- **`TheTrustedSignOnAndThePaidStay`:**
  - Playing fair prices only, the scripted player recruits a resident within the day (9,000 ticks; M1: by t6,110 on every probed seed).
  - The fee goes from the treasury to the recruit, and its wage is paid on every visit after that.
  - Its trust stays at 30 or more to the end, and the day's checks hold.
- **`TheUnpaidQuit`:**
  - The scripted player recruits at fair prices, then switches to the soak's generous prices with a price command. Under those prices the stall goes broke (by minute 10 when they are set from the start, economy model §7).
  - Recruiting first matters: under generous prices from the start, the stall may be broke before anyone qualifies, and the fee would be refused.
  - When the treasury can't pay, the member goes unpaid, loses trust and quits within two visits. The chronicle tells it.
  - The ledger balances throughout.
- **Regression:** the 23 gate seeds still hold (a player who doesn't recruit sees only bounty fairness change), and the soak is 9/9.

## G5. Expeditions
**Goal:** the player outfits up to four members and sends them where nobody else dares. They go together, work, come back with glowstone, and are paid their share.

**Design:**
- **The order (D7):** `DispatchExpeditionCommand` (members, target sector, goal, retreat rule), `RecallExpeditionCommand` and `GiveGearCommand` (D12).
  - The expedition lives in `Simulation/Guild/`. It holds the members, the order, the stage and when the stage began, the node claims and the haul.
  - It moves from stage to stage on the main thread, judged from what the members' states show (D9).
- **Cognition:**
  - The snapshot carries the expedition.
  - One goal, `FollowOrdersGoal`, ranked by compliance (D8). It ranks below hunger 70 and the danger reflexes, as Epic 8's directive goal does.
  - One action per stage: muster, march, work a node, return, deliver.
  - `ResourceTargeting` lets a complying member target the order's sector (D10).
- **The march:** each member paths behind the one before it, at the slowest member's pace. F5 (early jitter at the Hub–Bunkhouse door) is re-measured with four walking together.
- **The chamber (D11):**
  - a sector with a door from the Sump, its hazard, the rich seam, the floor's tint and its label;
  - the severity limit on going alone, in `Vitality` and wherever `WouldVenture` is asked (`ResourceTargeting`, `AmbientWanderEvaluator`);
  - map data in `Floor1Layout`, and the map validators.
- **The haul (D13):** a delivery intent puts the glowstone into the stall's stock without a sale and pays the shares. A guild delivery closes a cycle, which the day's check counts.
- **Trust (D8, D10):** downed under orders, −10 trust. Back with a haul, +3 trust and +5 allegiance for each member.
- **Save format 8:** the expedition (members, order, stage, claims, haul). As today, intents and plans aren't saved: after a load, members decide afresh from the stage.
- **The scripted player:** it outfits from stock and dispatches once the guild is full and the seam is ready (as the player sees it on the map), with the retreat rule set to Steady.
- **Client:**
  - **The party builder,** in the roster window: the members, the target among the charted hazard sectors, the goal, the retreat rule and "outfit from stock"; Dispatch, then Recall.
  - **The expedition tracker:** a compact panel while a party is out, showing the stage, each member's health and gear, the haul and the time out.
  - **The chronicle:** sent, turned back, came back with N glowstone, downed on an expedition.
- **Measured at G5.5:**
  - planning cost with an expedition out: `PopulationScalingProbe` with the guild player, on a quiet machine, with `main` measured alongside;
  - the frame-time gate;
  - jitter on seeds 1–120 (F5).
  - Planners per worker (§1A.2 #8) are built only on a measured need (epic §5).

**Steps:**
- [ ] G5.1 Docs: D7–D13 into the epic, the roadmap line, the focus file.
- [ ] G5.2 The chamber (D11):
  - the map data and the severity limit on going alone;
  - the map validators.
  - G2's danger scenario and the gate seeds still pass, with no lone crawler in the chamber.
  - The map changed, so re-record the golden hashes and the renders, each in a commit of its own.
- [ ] G5.3 Giving gear (D12), and the expedition's state, commands and stages (D7, D9):
  - save format 8;
  - `ExpeditionValidator`: the stages in order, recall, the retreat rule, a downed member, the haul and the shares, the ledger, and a round trip mid-stage.
- [ ] G5.4 Cognition (D8, D10): the goal, the actions, the facts, compliance, the march. Mechanism tests on the real floor with members put in place, as `HazardGearTests` does.
- [ ] G5.5 The live scenario, headless, and the measurements.
- [ ] G5.6 The client: the party builder, the tracker, the chronicle; tests and renders.
- [ ] G5.7 Finish, as G4.7. The economy model gains the shares and the chamber's glowstone. Then the PR into `main`.
  - **Stop for the producer's look at G5** before G6 starts.

**Definition of Done** (epic §3, G5):
- **`AGearedPartyBringsBackGlowstone`:** on the default seed, within 30 minutes (its fourth crawler qualifies at t12,830, M1):
  - the scripted player recruits four, outfits them from stock and sends them to the chamber;
  - they muster, march together, work the seam and come back with glowstone;
  - the caravan buys it at its premium (`CaravanSalvageSales`), and each member is paid its share;
  - nobody is downed, the ledger balances, and the day's checks hold.
- **The contrast:** a party sent to the chamber without gear has a member downed and turns back. A mechanism test will do, or the same scenario without outfitting.
- **Nobody alone in the chamber:** no lone crawler sets foot in it on the 23 gate seeds.

## G6. The gate
**Goal:**
- the whole loop plays;
- a game saved mid-expedition loads and plays on;
- the economy holds with a guild;
- the producer plays it.

**Steps:**
- [ ] G6.1 `TheGuildLoopPlaysThrough`: one scenario, 30 minutes on the default seed. The scripted player plays the whole loop:
  - fair prices;
  - recruiting as crawlers qualify, and wages every visit;
  - outfitting, then sending the party once four have signed and the seam is ready;
  - the haul sold, and the loop again.

  Asserted:
  - four signed, every wage paid, and nobody quit;
  - at least two expeditions back with glowstone;
  - trust up for 8 or more of the 12;
  - nobody starving, the ledger balanced, and the day's checks.
- [ ] G6.2 `ASaveMidExpeditionLoadsAndPlaysOn`:
  - The game is saved while the party works the seam and loaded into a fresh session. The capture is byte for byte, and the world hash is equal.
  - Then 3,000 more ticks: the party comes back with its haul, wages are paid, the ledger balances, and no intent faults.
- [ ] G6.3 The soak with a guild (D14):
  - measure the guild profile on the soak's seeds and set its band: treasury range, no unpaid visit, expeditions back, trust, nobody starving;
  - re-check the other three bands;
  - re-measure F4 with wages, then close it or keep it (epic §7).
- [ ] G6.4 Every milestone's scenario together (epic §3, G6):
  - the full suite;
  - the 23 gate seeds, with and without the guild player;
  - seeds 1–120 for F3 and F5.
- [ ] G6.5 The producer's play session:
  - the build: `dotnet run --project dotnet/Simpiens.Desktop -- --seed 424242`, where four crawlers qualify by t8,520 (M1);
  - a short script: earn trust, recruit, set a wage, outfit, send the party to the Deep Sump, recall it once, save mid-expedition and load.
  - **Stop** for the verdict. Each fix it asks for is a commit of its own; then the session again.
- [ ] G6.6 The G6 PR into `main`, carrying the close-out.
- [ ] G6.7 Close-out, in the docs protocol's order:
  1. The roadmap:
     - Epic 10's row: Part 1 COMPLETED, with the date; Parts 2 and 3 QUEUED;
     - Milestone 2's status, written as Milestone 1's is;
     - Epic UI-3, delivered within Part 1;
     - the operating rhythm and the chart.
  2. The epic:
     - the Status header;
     - §4, with G4–G6 as delivered;
     - §5's Deferred, with owners (the table below);
     - §7's F3, F4 and F5, each resolved or handed on with an owner;
     - History.
  3. The focus file; `architecture_history.md` (D1–D14 and the deviations); the economy model (the guild's flows and its band); AGENTS.md.
  4. This plan is deleted in the close-out commit, and nothing links to it.
  5. Next, as the producer chooses: the core refactor's R1–R6 (`.agents/core_refactor_plan.md`, due after Part 1), or drafting Part 2.

## Gates (every milestone)
- `dotnet test dotnet/Simpiens.slnx`. Commit messages give only the counts that actually ran.
- `golden check` 3/3. The hashes are re-recorded only for a deliberate behaviour change, in a commit of their own.
- `scripts/test-client-in-container.sh`, with renders matching byte for byte. Changed renders are looked at first, then re-recorded there, in a commit of their own.
- `dotnet build dotnet/Simpiens.Desktop -p:XnaFramework=MonoGame` and `scripts/build-android.sh` both compile, with 0 warnings.
- The 23 gate seeds; the soak 9/9, with fair within 1,000–2,000g; seeds 1–120 at each milestone's end (F3, F5).
- Check the branch before every commit. Push only on the producer's word, with an explicit refspec.

## Risks
| Risk | What we do |
| :--- | :--- |
| Wages are transfers. If members save them (M4), the treasury doesn't fall as the economy model expects. | Measure at G4.4. Add a crawler sink only on that measurement (economy model §8). |
| Hunger and the reflexes pull members off an order (Epic 8's starvation pre-emption). | The muster waits for fed, healthy members. The order ranks below the reflexes, as Epic 8's directive does. |
| Four walking together jam doors (F5; G0's head-on jams). | They march in file behind the first member; F5 is re-measured on seeds 1–120. |
| Cognition reads guild state off the main thread. | The snapshot carries copies; only the main thread writes (§1A.1 #6). |
| The save format changes at G4 (7) and G5 (8). | Old saves are refused with a reason, as today (epic §6). |
| Four members planning on orders every tick. | Measured at G5.5. Planners per worker only on a measured need. |
| The chamber moves the golden hashes and the renders. | Re-recorded in their own commits at G5.2. |

## Deferred (if the defaults stand)
| Left out | Owner |
| :--- | :--- |
| A guild larger than four; several parties at once | Milestone 3, the guildhall (as epic §5) |
| Drifters signing on | Later: they pass through (Q7) |
| Gear making lone crawlers bolder. Epic §5 put it in G5; D10 settles it for parties. | Part 2, crafting (gear per hazard kind) |
| Carrying a downed member | The descent (Part 3), with combat |
| Expeditions into uncharted sectors (scouting as an order) | Part 3 |
| A FormParty command | When parties outlive one expedition (Milestone 3) |
| Lodging fees or other crawler sinks | Only on G4.4's measurement |

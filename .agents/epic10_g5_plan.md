# Epic 10 G5: expeditions (working plan)

Working document (AGENTS.md, Documentation Protocol 6), one of Epic 10's plans ([index](epic10_plan.md)). Written 2026-10-29. It starts after G4's PR merges and is deleted at G5's close-out (G5.7).

- **Goal** (epic §2.2, the G5 row): the player outfits up to four members and sends them where nobody else dares. They go together, work, come back with glowstone, and are paid their share.
- **Builds on G4's D1:**
  - the party is a `TemporaryParty` group inside the guild;
  - the expedition is a new kind of directive (epic §2.3 as written).
- **Branch:** `feat/epic10-g5`, cut from `main` after G4's PR merges.
- **Estimate:** 4–5 agent days.
  - The epic said 3–4. The chamber (D11) and the tracker add about a day.
  - Making the party a group gives back about half a day.
- **The producer's stop:** a look at G5 before G6 starts (the epic: "the producer looks at G2, G5 and G6").

## 1. Measured (2026-10-29)
- **How:** the scratch probe of the G4 plan, with fair prices for 30 sim-minutes.
- **Seeds:** glowstone on the golden three and 1–7; boldness on the golden three and 1–12.

| # | What | Measured | Decides |
| :-: | :--- | :--- | :--- |
| M3 | Glowstone | Bold crawlers going alone take 9–13 of the roughly 18 the six nodes yield in 30 minutes, on 9 of 10 seeds (2 on seed 6). On average 2.0–2.7 of the 6 nodes are ready (3.5 and 5.3 on two quiet seeds). | D11: a party sent into the Sump or the Glow Caves would find 1–1.5 ready nodes, already worked by the bold. |
| M5 | Who would go into danger alone (risk tolerance ≥ 30%) | 2–7 of the 12 residents on each seed | D10: a guild chosen by trust is mostly cautious, so orders have to bring cautious members along. |

**Epic 8's directives in play:** none are issued (G4's M2). G5 is the first live use of the directive pipeline (`ComplyDirectiveGoal` → `TravelToDirectiveAction` → `ExecuteDirectiveAction`). See §6.

## 2. Decisions (defaults; the producer answers only where they disagree)
| # | Decision | Default | Alternative |
| :-: | :--- | :--- | :--- |
| D7 | **Sending a party** | One command, **Dispatch**, carries the members (one to four), the target sector, the goal (up to N glowstone) and the retreat rule.<br>**It makes the party:** a `TemporaryParty` group led by the patron, inside the guild (`ParentGroupId`). The party is disbanded when the expedition ends, and its members stay in the guild.<br>**Forming it** happens in the roster window, so "form a party" belongs to the UI, not the simulation.<br>**Recall** brings the party home.<br>**One expedition at a time,** since the guild is one party. | A FormParty command as well, holding a party that does nothing until it is sent. |
| D8 | **Obeying the patron** (Q6) | Epic 8's compliance formula, with the patron's weights: trust in place of fear.<br>U = trust × 0.35 + respect × 0.25 + allegiance × 0.25 + warmth × 0.15 − hunger × 0.30 − frustration × 0.20.<br>Allegiance is the guild membership's.<br>**Epic 8's three outcomes:**<br>• 20 or more: the member complies;<br>• from 0 to 20: it stays behind (−5 allegiance), and the tracker says so;<br>• below 0: it deserts, quitting the guild and the party but keeping its settlement.<br>**Allegiance** starts at 70 (Epic 8). It rises with paid visits and returned expeditions, and falls with unpaid visits and being downed. | Epic 8's formula unchanged (fear and respect toward the player). |
| D9 | **An expedition** | Stages held by the simulation and saved, in the expedition's record (kept by the party's id):<br>1. **Muster** at the hub, until every member is there, fed (hunger below 50) and at health 80 or more, or 90 s have passed. Anyone not there by then stays behind.<br>2. **March** together to the sector's door, in file behind the first member, at the slowest member's pace.<br>3. **Work:** each member takes a ready node, claimed in id order. The party holds the sector until the goal is met, the nodes are spent or the retreat rule fires.<br>4. **Return** to the stall.<br>5. **Deliver.**<br>6. **Done.**<br>**The directive** (`DirectiveType.Expedition`) is issued once, at dispatch, and members judge it then (D8). Each stage's target comes from the record in the snapshot.<br>**Members follow it through Epic 8's pipeline, extended:** one goal and one action per stage, using the existing facts `HasActiveDirective` and `AtDirectiveLocation`, plus at most 4 new facts (39 → 43 of 64). | GOAP alone, with no stage machine: each member plans the trip itself, and nothing holds the party together. |
| D10 | **Danger under orders** | **Who goes:** a complying member goes into danger whatever its boldness, from health 80 as today.<br>**When to turn back:** the player's retreat rule: Cautious 70, Steady 55 or Bold 40. 40 stays everyone's floor (G2's retreat reflex).<br>**When a member is downed:** it limps home, and the party retreats at its pace (Q3: "its party brings it back"). Its trust in the player falls by 10.<br>**Going alone** is unchanged: gear doesn't make lone crawlers bolder. | Boldness still decides who goes: the party is whichever members would go alone (2–7 residents per seed are bold, M5). |
| D11 | **Somewhere nobody else dares** (M3; the epic's §1 promise) | **A new hazard chamber below the Sump.** Placeholder name "the Drowned Gallery": renamed from the single plan's "Deep Sump", because Floor 1 already has a "Deep Seam".<br>• **The map:** the floor is a full three-by-three of rooms, so it grows by one room.<br>• **The danger:** 6 health a second (3 with gear).<br>• **The seam:** one rich glowstone seam (yield 4, regrowing in 10 minutes), charted from the start.<br>**A severity limit on going alone:** a crawler goes alone only where a hazard takes 4 a second or less.<br>So only an expedition works the seam, and the lone crawlers' economy, and with it the fair band, is unchanged. | No new map: expeditions go to the Sump and the Glow Caves, where a trip finds 1–1.5 ready nodes (M3). |
| D12 | **Giving gear** | The player gives a member hazard gear from the stall's stock. No gold changes hands: the stock is the player's, and the caravan restocks it as today. The party builder offers "outfit from stock". | Members buy their own gear before setting out. |
| D13 | **The haul** | The haul belongs to the guild.<br>• Members hand their glowstone to the stall without a sale.<br>• They split 30% of its caravan value (21g a glowstone), paid on delivery: a transfer, like wages.<br>• The caravan buys glowstone as today: 70g, two a visit.<br>• A guild delivery closes a member's harvest-to-sell cycle. | Members sell their haul at the stall's price, as anyone does, leaving the guild no margin. |

## 3. Design
- **The order (D7):**
  - `DispatchExpeditionCommand` (members, target sector, goal, retreat rule);
  - `RecallExpeditionCommand`;
  - `GiveGearCommand` (D12).
- **The party and the expedition:**
  - Dispatch makes the party group, inside the guild, and issues its directive.
  - The expedition's record lives in `Simulation/Guild/`. It holds the order, the stage and when the stage began, the node claims and the haul.
  - It moves from stage to stage on the main thread, judged from what the members' states show (D9).
  - When it ends, the directive is cleared and the party disbanded. Its members stay in the guild.
- **Groups:** `GroupData` and the group's save record gain `ParentGroupId` (a party's guild).
- **Cognition:**
  - The snapshot carries the expedition's record.
  - `ComplyDirectiveGoal` handles the expedition directive, ranked by compliance (D8). It ranks below hunger 70 and the danger reflexes, as Epic 8's directive goal does.
  - One action per stage: muster, march, work a node, return, deliver.
  - `ResourceTargeting` lets a complying member target the order's sector (D10).
- **The march:** each member paths behind the one before it, at the slowest member's pace. F5 (early jitter at the Hub–Bunkhouse door) is re-measured with four walking together.
- **The chamber (D11):**
  - a sector with a door from the Sump, its hazard, the rich seam, the floor's tint and its label;
  - the severity limit on going alone, in `Vitality` and wherever `WouldVenture` is asked (`ResourceTargeting`, `AmbientWanderEvaluator`);
  - the map data in `Floor1Layout`, and the map validators.
- **The haul (D13):** a delivery intent puts the glowstone into the stall's stock without a sale and pays the shares. A guild delivery closes a cycle, which the day's check counts.
- **Trust (D8, D10):**
  - downed under orders: −10 trust;
  - back with a haul: +3 trust and +5 allegiance for each member.
- **Save format 8:** the group record's parent and the expedition's record. As today, intents and plans aren't saved: after a load, members decide afresh from the stage.
- **The scripted player:** it outfits from stock and dispatches once the guild is full and the seam is ready (as the player sees it on the map), with the retreat rule set to Steady.
- **Client:**
  - **The party builder,** in the roster window: the members; the target, among the charted hazard sectors; the goal; the retreat rule; "outfit from stock". Then Dispatch, and Recall.
  - **The expedition tracker:** a compact panel while a party is out, showing the stage, each member's health and gear, the haul and the time out.
  - **The chronicle:** sent, turned back, came back with N glowstone, downed on an expedition.
- **Measured at G5.5:**
  - planning cost with an expedition out: `PopulationScalingProbe` with the guild player, on a quiet machine, with `main` measured alongside;
  - the frame-time gate;
  - jitter on seeds 1–120 (F5).
  - Planners per worker (§1A.2 #8; the refactor's R6) are built only on a measured need (epic §5).

## 4. Steps
Each step is a commit. After each: a build with 0 warnings and `dotnet test`.
- [ ] **G5.1 Docs:** D7–D13 into the epic, the roadmap line, the focus file.
- [ ] **G5.2 The chamber (D11):**
  - the map data and the severity limit on going alone;
  - the map validators.
  - G2's danger scenario and the gate seeds still pass, with no lone crawler in the chamber.
  - The map changed, so re-record the golden hashes and the renders, each in a commit of its own.
- [ ] **G5.3 Giving gear (D12); the party, its directive and the expedition's stages (D7, D9):**
  - save format 8;
  - `ExpeditionValidator`:
    - the stages in order, recall, the retreat rule, a downed member;
    - the haul and the shares, the ledger;
    - a round trip mid-stage;
    - the party disbanded, its members still in the guild and in their settlements.
- [ ] **G5.4 Cognition (D8, D10):** the goal, the actions, the facts, compliance, the march.
  - Mechanism tests on the real floor with members put in place, as `HazardGearTests` does.
  - Each stage of the directive pipeline's first live use gets a validator case.
- [ ] **G5.5 The live scenario, headless, and the measurements.**
- [ ] **G5.6 The client:** the party builder, the tracker, the chronicle; tests and renders.
- [ ] **G5.7 Finish, as G4.10.** The economy model gains the shares and the chamber's glowstone. Then the PR into `main`.
  - **Stop for the producer's look at G5** before G6 starts.

## 5. Definition of Done (epic §3, G5)
- **`AGearedPartyBringsBackGlowstone`:** on the default seed, within 30 minutes (its fourth crawler qualifies at t12,830, M1):
  - the scripted player recruits four, outfits them from stock and sends them to the chamber;
  - they muster, march together, work the seam and come back with glowstone;
  - the caravan buys it at its premium (`CaravanSalvageSales`), and each member is paid its share;
  - nobody is downed, the ledger balances, and the day's checks hold.
- **The contrast:** a party sent to the chamber without gear has a member downed and turns back. A mechanism test will do, or the same scenario without outfitting.
- **Nobody alone in the chamber:** no lone crawler sets foot in it on the 23 gate seeds.

## 6. Risks
| Risk | What we do |
| :--- | :--- |
| Epic 8's directive pipeline has never run in play (M2). | A validator case per stage (G5.4). The live scenario is its first live proof, and a faulting intent fails the day (refactor R0). |
| Hunger and the reflexes pull members off an order (Epic 8's starvation pre-emption). | The muster waits for fed, healthy members. The order ranks below the reflexes, as Epic 8's directive does. |
| Four walking together jam doors (F5; G0's head-on jams). | They march in file behind the first member; F5 is re-measured on seeds 1–120. |
| Cognition reads guild state off the main thread. | The snapshot carries copies; only the main thread writes (§1A.1 #6). |
| Four members planning on orders every tick. | Measured at G5.5. Planners per worker only on a measured need. |
| The chamber moves the golden hashes and the renders. | Re-recorded in their own commits at G5.2. |
| The save format changes (8). | Old saves are refused with a reason. |

## 7. Deferred (G5)
| Left out | Owner |
| :--- | :--- |
| Several parties at once; a FormParty command (parties that outlive one expedition) | Part 2, the guildhall (W5) |
| Gear making lone crawlers bolder. Epic §5 put it in G5; D10 settles it for parties. | Part 2, gear for each hazard (W3) |
| Carrying a downed member | Part 3, with combat (L2) |
| Expeditions into uncharted sectors (scouting as an order) | Part 3 (L3) |
| Loot tables on the haul (the item refactor's foundation) | Part 3, creature drops (L2). The haul stays glowstone. |

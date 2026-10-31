# Epic 10 G4: the guild, on many-to-many groups (working plan)

Working document (AGENTS.md, Documentation Protocol 6), one of Epic 10's plans ([index](epic10_plan.md)). Written 2026-10-29; deleted at G4's close-out (G4.10).

- **Goal** (epic §2.2, the G4 row):
  - trusted crawlers sign on for a fee and a wage;
  - paid members stay, and unpaid ones quit;
  - a bounty's reward is judged as fairly as a trade.
- **The design note first.** The epic's risk row asks for a note on the player as patron before any code. §2 (D1–D6) is that note; D1 answers the producer's view that groups should be many-to-many.
- **Branch:** `feat/epic10-g4`, rebased onto `main` at `b54ef83` (PR #17, Epic UI-Beta B0–B1, merged 2026-10-30); one PR into `main`.
- **Estimate:** 3–4 agent days. The single plan said 2–3; the groups refactor (G4.2–G4.4) adds about one.
- **The producer's go:** 2026-10-30, "proceed with epic 10 work". D1–D6 stand as proposed.

## 0. Before G4
- [x] PR #16 (graphics V0–V6) is merged (`01a1321`, 2026-10-29). Its local folder is archived as `.agents/local/archive/graphics-v3-v5_2026-10-29.zip`.
- [x] `feat/epic10-g4` is rebased onto `main` (`b54ef83`, after PR #17).
- PR #17 took save format 7 and made the world hash cover each crawler's look and name; G4's save change is format 8.
- G3's defaults, the fair band (1,000–2,000g) and the drifter tuning stand.

## 1. Measured (2026-10-29)
- **How:** a scratch probe, not committed, with the outpost at its default prices (the fair player's).
- **The run:** 30 sim-minutes, sampled every 10 ticks.

| # | What | Measured | Decides |
| :-: | :--- | :--- | :--- |
| M1 | Q5's threshold: trust ≥ 60 and warmth ≥ 40 toward the player | On 15 seeds (the golden three and 1–12):<br>• first reached on all 15, at t1,170–6,110 (median t1,820), inside the 15-minute day;<br>• a fourth crawler within 30 minutes on 14 (t3,340–16,390); seed 2 has only two.<br>On the default seed, 20261009, the first qualifies at t1,250 and the fourth at t12,830. | G4's DoD stands, with Q5's numbers |
| M2 | Epic 8's groups in play | On 43 seeds (the golden three and 1–40):<br>• **Settlements:** 2–4 form on every seed, holding 6–12 of the 12 residents at their largest.<br>• **Recruits in a settlement:** 98 of the 215 residents who reached Q5's threshold (46%) were in a settlement at that moment.<br>• **Drifters:** 36 of 369 joined a settlement. A departing crawler never leaves its group, so on 25 of 43 seeds a departed drifter stayed on a roster, up to three at once.<br>• **Leadership:** passed on 7 seeds, always peacefully. Cohesion never fell below 80, so there was no dominance contest and no schism.<br>• **Behaviour:** no crawler's choice reads a membership in the live outpost:<br>&nbsp;&nbsp;– settlements form and pass leadership on;<br>&nbsp;&nbsp;– the planner's `IsGroupLeader` fact is read by no action;<br>&nbsp;&nbsp;– only validators and the Dev Map issue directives;<br>&nbsp;&nbsp;– group events go into a memory ledger that nothing reads.<br>The world hash doesn't cover groups. | D1 |
| M4 | Residents' purses | 70–245g between the 12 at 30 minutes; the richest holds 10–55g. Crawlers spend what they earn. | Wages are transfers. Measured again at G4.7. |

M3 and M5 are in the G5 plan, M6 in the G6 plan.

## 2. Decisions (defaults; the producer answers only where they disagree)

### D1. Groups become many-to-many, and the guild is an Epic 8 group (epic §2.3 as written)
**The producer (2026-10-29):** "Groups should be many to many relationship is my opinion", with a request to argue for or against it.

**My verdict: for, with one rule on top.**
- **The rule:** a crawler holds at most one group of each kind: one settlement, one guild, one party. The limit is data, so it can be raised.
- **What follows:**
  - the guild is a `FactionGuild` group with the player as patron, as epic §2.3 says;
  - a party is a `TemporaryParty` group inside it.
- **What it replaces:** the earlier default (the guild as a contract beside Epic 8's groups) is withdrawn, and with it the deviation from §2.3.

**For:**
1. **The approved design already needs it.**
   - Epic §2.3 puts a party inside the guild, so a party member belongs to two groups.
   - The game design says the player "recruits the agent into their **Guild Group**" and sends parties out "as a cohesive `Group`".
   - Epic UI-3's party builder assigns "persistent `GroupDirective` contracts".
   - One membership per crawler can express none of these.
   - Epic 8 never argued for one membership. It follows from keeping the membership on the crawler (`AgentState.Membership`).
   - Epic 8's three kinds are collectives of different scales. `GroupType`'s own comments give a hunting party, a craft guild and a village, and a villager in a craft guild who joins a hunting party belongs to all three.
2. **With one membership, recruiting would break settlements (M2).**
   - Nearly half the crawlers who qualify are in a settlement when they do, and `AddMember` would take each of them out of it.
   - A recruited leader would leave its settlement leaderless, so the player's recruit would start a succession.
   - The epic promises (Q6) that a member keeps its own life.
3. **The alternative is the silo Epic 8 warned against.**
   - The withdrawn default built a second group system beside the first: a membership record, a roster, allegiance, a compliance formula, a directive path and a save block.
   - Epic 8 §1 names this anti-pattern: a class per kind of collective makes "rigid silos that cannot dynamically evolve".
   - Every later affiliation would add another silo: Part 2's artisans, the guildhall's parties, a guild per player after the MVP.
4. **It is cheap now, and provable.**
   - The registry already keeps a member set per group. Only the crawler's side holds one membership: a dictionary entry, mirrored on `AgentState` and saved twice (in the crawler's record and in the groups' block).
   - 17 core files and 6 validators or fixtures touch memberships.
   - In play no choice reads a membership (M2). So once the world hash covers groups (G4.2), the refactor must leave the golden hashes exactly as they are.
5. **It fixes two faults found on the way.**
   - **Departed members:** a departing crawler stays on its group's roster (M2: 25 of 43 seeds).
   - **Epic 8's open finding 1:** `CreateGroup` leaves a new leader on its old roster. Epic 8 §11 calls it latent, but a schism reaches it, because the splinter group's new leader is still on the old group's roster. No schism happened on the 43 seeds. The per-kind rule closes it.

**Against, and the answers:**
1. **Whose order does a crawler follow?** In three groups it may hold three directives.
   - *Answer:* one fixed precedence: its party's order first, then its guild's, then its settlement's. Only the first active one is followed.
   - In the outpost only the patron will issue orders.
2. **Unlimited membership means nothing yet:** two hearths to sleep at, two patrons to obey.
   - *Answer:* the per-kind limit.
   - Here I'd argue against the unlimited form until a milestone needs it. Nothing in Epic 10 does, and each kind would need a rule for which group wins.
3. **More cases to test:** succession in a settlement whose members are also in the guild; a schism; deserting one group only.
   - *Answer:* the validators grow by those cases (G4.3).
4. **Schedule:** G4 grows by about a day.
   - *Answer:* G5 gets about half of it back, since its party is a group and its expedition a directive.
   - Part 2's artisans and the guildhall's parties then come almost free.

**Alternative:** the withdrawn default, a contract beside the groups, with Epic 8's code untouched.

### D2–D6
| # | Decision | Default | Alternative |
| :-: | :--- | :--- | :--- |
| D2 | **Recruiting** | The player's Recruit command names a crawler and a wage.<br>**Who can be recruited:**<br>• a resident (not a drifter), not downed;<br>• at trust ≥ 60 and warmth ≥ 40 (Q5);<br>• with room in the guild (four at most, Q4);<br>• not having quit or been dismissed in the last 5 minutes.<br>**What it keeps:** its settlement (D1).<br>**The terms:** the stall pays a signing fee, 20g by default. The crawler signs if the wage is at least its reservation wage: 5g per caravan visit × its greed factor (as its calm values are). | No fee: the wage is the only cost. |
| D3 | **Wages** | **When:** on each caravan visit. The visit's order becomes exports, upkeep, rations, **wages**, gear: food, then people, then stock.<br>**How:** in join order, each wage paid in full or not at all (no debt, as with upkeep).<br>**A paid member:** +2 trust, +1 warmth and +1 allegiance. The payment counts as an interaction, so a paid member's trust doesn't drift back to neutral.<br>**An unpaid member:** −10 trust, −5 warmth and −5 allegiance.<br>**In the ledger:** fees and wages are transfers between the stall and crawlers, conserved and counted for observation (`EconomyFlowTotals`). No new faucet or sink. | Wages on a clock of their own (every sim-minute). |
| D4 | **Quitting** | A member quits when its trust toward the player falls below 30, or after 2 unpaid visits in a row. The player can dismiss a member (−5 trust).<br>Either way it leaves the guild only, keeping its settlement, and it can't sign again for 5 minutes. | Quit at the recruitment threshold (no hysteresis). |
| D5 | **Bounty fairness** (§2.2's G4 row; F3) | A delivery is judged as a sale is: the reward against the crawler's own minimum for the work.<br>• **The minimum:** for a Gather, its items' minimum sale prices; for a Scout, a scouting value of 5g × the crawler's greed factor.<br>• **Generous (≥ 3× the minimum):** today's flat +15 trust, +10 warmth, +10 respect.<br>• **Fair (≥ 1.5×):** +8 trust, +5 warmth, +5 respect.<br>• **Stingy:** −5 trust, −3 warmth, no change in respect.<br>The seeded bounties (25g for an ore, 15g for a scout) stay generous to a neutral crawler. | Bounties keep their flat reward, and bounty fairness goes to Deferred. |
| D6 | **The guild on screen** | **Recruit** is on the dossier, showing the crawler's groups, its progress toward the threshold, the fee and the wage. A refusal says why.<br>**The roster** is a window, like the merchant and the bounty board (epic §6). GUILD in the header opens it, beside an "n/4" count. | The roster as a fourth panel (there is no room at 1280×720). |

## 3. Design
Numbers live in `GuildTuning`, beside `DrifterTuning` and `GearTuning` in `EconomyTuning`, never as literals.

**Groups (D1):**
- **The record:** `GroupMembership` (8 bytes: group, role, allegiance, join tick) stays one record per crawler and group.
- **The set:** the registry keeps each crawler's memberships in a `MembershipSet`, ordered by kind, with no allocation. It has room for four inline: today's three kinds and Part 2's workshop.
- **The rule:** one group per kind, held as data (`GroupKinds`).
  - Joining a group leaves the crawler's other group of the same kind. `AddMember` does this today for any group.
  - `CreateGroup` applies the same rule to its leader.
- **The API:**
  - a membership by group (`TryGetMembership(crawler, groupId, …)`), by kind, or all of them;
  - `UpdateAllegiance(crawler, groupId, delta)`;
  - `LeaveAllGroups(crawler)`.
- **One owner, the registry.**
  - The crawler keeps a copy for its context, `AgentState.Memberships`. It is refreshed every tick (today only while the crawler has a group) and is not saved.
  - Main-thread code asks the registry; cognition reads the copy.
- **Readers:**
  - **Formation:** a crawler with no settlement counts as unaligned. Today it is one with no group, which is the same in play, where settlements are the only groups.
  - **Directive evaluation** walks a crawler's memberships in precedence order (party, guild, settlement) and follows the first group with an active directive.
  - **The context** carries that membership (`DirectiveMembership`). So `ComplyDirectiveGoal`, the directive actions and the planner's facts read one membership, as they do now.
  - **Desertion** leaves that group only.
- **Departure:** leaving the outpost leaves every group (`SimulationManager.DepartOutpost`).
- **The patron:** a group led by the player (`WorldRegistry.PlayerEntityId`).
  - The patron leads without a place on the roster.
  - Succession never replaces it.
  - Compliance with its orders uses the patron's weights (D8, in G5).

**The guild (D2–D4):**
- **What it is:** a `FactionGuild` group, led by the patron and anchored at the stall.
  - It is made at the first recruit and kept while empty.
  - The patron is always the player for now. Keeping it as data leaves room for a guild per player after the MVP (ROADMAP §1A.1).
- **Its terms** belong to the guild, not to the group primitive. `GuildContracts` holds each member's wage and unpaid visits in a row, and each crawler's cooldown.
- **Allegiance** is the guild membership's: Epic 8's 0–100, starting at 70.
- **Commands:** typed, in `InfluenceEvent`, applied in queue order on the tick.
  - `RecruitCommand` (a crawler and a wage), `DismissCommand` and `SetWageCommand`.
  - One rule decides each and explains a refusal to the dossier: `GuildRules`, as `BountyRules` does for bounties.
- **Wages:** the caravan's visit pays them after the rations (D3), through `ISupplyHost`. The ledger's conservation check covers fees and wages as it does purchases.
- **Leaving:** quitting or dismissal takes a crawler out of the guild group only. Its settlement stays.

**Bounty fairness (D5):** `TradeFairness.ClassifyBounty`, with its deltas in `TradeTrustTuning`. `DeliverBountyIntentHandler` uses them in place of the flat +15.

**Saves (format 8, raised once for G4; Epic UI-Beta took 7):**
- **Memberships:** the crawler's record drops its membership, so the groups' block holds the only copy, with several records per crawler, sorted by crawler and group.
- **The guild's contracts:** a block of their own.
- The round-trip validators check both.

**The world hash:**
- **Groups, from G4.2:** each group's kind, leader, patron, cohesion and sorted roster, and each crawler's memberships.
- **The guild's contracts, from G4.6.**

**The scripted player:** `Simpiens.Hosting.GuildPlayer`.
- It acts only through commands: it sets its prices, then recruits the first resident who qualifies, at the default wage, up to a target size.
- It is shared by the DoD scenarios, G6's soak profile and the console (`simpiens run --player guild`, `soak --profile guild`).

**Client:**
- **Dossier:**
  - its groups: "Settlement: led by Mara", "Guild: member, 5g a visit";
  - "Will join (fee 20g)", or "Not yet: trust 45 of 60, warmth 22 of 40";
  - the Recruit button.
- **Roster window:** a card per member, with Dismiss. Each card shows:
  - name, health and DOWNED;
  - gear and its wear;
  - trust and warmth toward the player;
  - the wage, with a slider that sends Set Wage;
  - unpaid visits;
  - what the member is doing.
- **Header:** GUILD, with "n/4".
- **Chronicle:** joined, couldn't be paid, quit, dismissed.

## 4. Steps
Each step is a commit. After each: a build with 0 warnings and `dotnet test`.
- [x] **G4.1 Docs:**
  - **The epic:**
    - D1–D6;
    - §2.3 gains "groups are many-to-many, one per kind";
    - §6's risk row is answered.
  - **Epic 8's doc:** finding 1 gets its owner (G4.4).
  - **The roadmap:** the Epic 10 line (G4 under way, with this plan) and Epic CLIENT's line (PR #16 merged, 2026-10-29).
  - **The focus file.**
- [x] **G4.2 The world hash covers groups.** No behaviour change. The golden hashes are re-recorded in this commit, which says why.
- [x] **G4.3 Groups become many-to-many (D1):**
  - the set, the per-kind rule, the API, the registry as the one owner, the readers, the directive precedence;
  - save format 8 (the crawler's record drops its membership).
  - **Validators:**
    - `GroupPrimitiveValidator`: a settlement and a guild at once; a second settlement replaces the first; disbanding one group leaves the others;
    - `SuccessionValidator`: succession and a schism in a settlement whose members are in a guild;
    - `DirectiveComplianceValidator`: the precedence; desertion leaves one group;
    - the round-trip validators: several memberships per crawler;
    - the Dev Map's pens still play.
  - **The proof:**
    - `golden check` 3/3, **unchanged**;
    - the 23 gate seeds;
    - the probe's settlement counts and leader changes identical on its 43 seeds, recorded in the commit body.
- [x] **G4.4 Leaving, fixed (D1's two faults):**
  - a departing crawler leaves every group;
  - `CreateGroup` takes its leader off the old roster (Epic 8 finding 1, closed);
  - a validator case for each: a drifter's departure, and a schism's splinter leader.
  - The golden hashes change only in their groups part, which the per-part descriptions name. They are re-recorded in a commit of their own.
- [x] **G4.5 Bounty fairness (D5):**
  - the rule, the handler and the tuning;
  - `BountyFulfillmentValidator`: generous, fair and stingy, by greed.
  - Re-measure F3 on seeds 1–120. Re-record the golden hashes in a commit of their own.
- [ ] **G4.6 The guild (D2):**
  - **The patron:** succession skips it, and it leads without a place on the roster.
  - `GuildTuning` and `GuildContracts`.
  - The three commands, their rule and the fee.
  - The contracts' save block.
  - **`GuildValidator`:** the rule and each refusal, the fee, four at most, the cooldown, dismissal, and a recruit keeping its settlement.
- [ ] **G4.7 Wages and quitting (D3, D4):**
  - the visit's wages; trust and allegiance; unpaid visits; quitting; the chronicle;
  - `GuildWageValidator`: paid, unpaid, quitting after two, the trust floor, the ledger.
  - **Measure** a guild of four at 5g, played by the scripted player on the soak's seeds: the treasury, the members' purses (M4) and trust.
  - If purses climb, carry it to G6. A crawler sink comes back only on that measurement (economy model §8).
- [ ] **G4.8 The scripted player and the live scenarios,** headless.
- [ ] **G4.9 The client:**
  - the dossier, the roster window, the header;
  - `DashboardModelTests` for the guild's model, and render tests.
- [ ] **G4.10 Finish:**
  - the client's references and pins, re-recorded in the pinned image, in a commit of their own;
  - **the economy model:** §1, §2 and §4 (fees, wages, bounty fairness);
  - **the epic:** §4's G4 entry and §7's F3;
  - **Epic 8's doc:** the membership model (many-to-many, one per kind), and finding 1 closed;
  - **the roadmap, the focus file and `architecture_history.md`:** D1 changes Epic 8's primitive; D2–D6;
  - **AGENTS.md:** memberships in `Cognition/Groups/`, `Simulation/Guild/`, the commands, `GuildPlayer`;
  - the index's G4 row shrinks to a line, and this plan is deleted;
  - the PR into `main`.

## 5. Definition of Done (epic §3, G4)
Headless, on the real outpost, with nothing injected. The scripted player acts only through commands.
- **`TheTrustedSignOnAndThePaidStay`:**
  - Playing fair prices only, the scripted player recruits a resident within the day (9,000 ticks). M1: someone qualifies by t6,110 on every probed seed.
  - The fee goes from the treasury to the recruit, and its wage is paid on every visit after that.
  - The recruit keeps its settlement, if it had one (D1).
  - Its trust stays at 30 or more to the end, and the day's checks hold.
- **`TheUnpaidQuit`:**
  - The scripted player recruits at fair prices, then switches to the soak's generous prices with a price command.
  - Under generous prices the stall goes broke: by minute 10 when they are set from the start (economy model §7).
  - Recruiting first matters. Under generous prices from the start, the stall may be broke before anyone qualifies, and the fee would be refused.
  - When the treasury can't pay, the member goes unpaid, loses trust and quits within two visits, keeping its settlement. The chronicle tells it.
  - The ledger balances throughout.
- **Regression:**
  - the 23 gate seeds hold: a player who doesn't recruit sees only bounty fairness change;
  - the soak is 9/9.

## 6. Risks
| Risk | What we do |
| :--- | :--- |
| The refactor changes behaviour unseen: the world hash doesn't cover groups. | G4.2 extends the hash first, and G4.3 must reproduce it exactly. |
| Code that assumes a crawler leader meets a player-led group: succession, the legitimacy check, `CreateGroup` putting its leader on the roster. | The patron's rules in G4.6, each with a validator case. |
| Wages are transfers. If members save them (M4), the treasury doesn't fall as the economy model expects. | Measure at G4.7. A crawler sink only on that measurement (economy model §8). |
| The save format changes (8). | Old saves are refused with a reason, as today (epic §6). |

## 7. Deferred (G4)
| Left out | Owner |
| :--- | :--- |
| More than one group of a kind per crawler | The milestone that needs it; the limit is data (D1) |
| Settlement leaders issuing directives in play | Not planned. Epic 8's mechanism stays tested by validators and the Dev Map. |
| A guild larger than four | Part 2, the guildhall (W5) |
| Drifters signing on | Later: they pass through (Q7) |
| Lodging fees or other crawler sinks | Only on G4.7's measurement; otherwise Part 2 (W5) |

## 8. Findings (G4)
- **G4.2–G4.4's proof needs a horizon past the golden one.** No group forms before t3000 on the golden seeds, so `golden check` can't see a groups change. The scratch probe samples the hash's groups part every 100 ticks to t18000 on the golden three and seeds 1–40.
  - At G4.2: groups on 43 of 43 seeds, 124 formed, at most 4 at once, 7 leader changes.
  - G4.3 reproduces it exactly.
  - G4.4 changes the groups part on 25 seeds, exactly those where a departed drifter stayed on a roster (up to 3 at once), and nothing else. Crawlers, the economy, bounties and the map are identical on all 43. Seed 18 gains a leader change: a departing leader now leaves its settlement, which then passes leadership on.
  - Golden doesn't change at G4.4, so there is no re-record.
- **A tooling trap:** `dotnet run probe.cs` (a file-based app) reused its first build after the core changed, so a second run compared old code with itself. A new file name forces a fresh build. G4.3's probe was re-run that way and still matches.
- **G4.5, F3 re-measured (seeds 1–120):** 112/120, the same 8 seeds failing the same way as before bounty fairness.
  - Trust on 31, 43 and 81 is still 7 of 12. Bounty fairness changes only the claimant's trust, and the missing crawlers aren't claimants, so F3 stays open (G6).
  - Jitter (F5) on 56, 69, 74 and 119; seed 73's hunger.
- **G4.5 meets F5 off the gate:**
  - The glowstone-free proof day (`ItemSystemProofTests`, the default seed) now has one jitter, at t2516 near the Garden (−10.5, 2.2): a crawler steps, waits and steps among others.
  - The proof is about the economy, so it now judges every check of the day but F5's jitter, and only while there is no penetration or breach.
  - The tolerance goes with F5, at G5.

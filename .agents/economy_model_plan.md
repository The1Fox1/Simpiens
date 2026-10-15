# §3.3 The Economy Model (Implementation Plan)

- **Owner:** review §3.3 / ROADMAP §3A #10a. Promotes [Epic 9](../docs/epics/epic-9.md) and closes Epic Test-2's remaining scope. Status is owned by `docs/ROADMAP.md`.
- **Written:** 2026-10-13, after Epic MAP's Definition of Done.
- **Approved 2026-10-14 under provisional authority:** the producer, going AFK, delegated §3.3 to the agent. D1–D7 were taken as recommended (A; D5 bands set from the step 2 baseline). The producer reviews on return.
- **Progress:** steps 1–4 done.
- **Branch:** `feat/economy-model`, from `main` once the Epic MAP close-out (`feat/epic-map-closeout`) is merged.
- **Input:** every economic decision was folded into §3.3 by the producer on 2026-10-13. The evidence and the list are in [Epic 9 §8](../docs/epics/epic-9.md) (§8.4 is the list).
- **Goal:** one economy that holds on its own, where the player's price sliders are a real choice between profit and trust:
  - gold enters and leaves through named, bounded faucets and sinks;
  - a fair player's treasury grows slowly and crawlers come to trust them;
  - a greedy player earns faster but loses the crawlers' trust;
  - a careless or over-generous player can go broke;
  - nobody starves at sensible prices.
  
  Then the soak asserts it, `OutpostLifecycleScenario` asserts trust again, and Epic 9 is `COMPLETED`.
- **What the producer sees after this plan:**
  - The game looks the same. What changes is how it plays out over 15–30 minutes: the treasury readout, trust on the crawler dossier and how crawlers react to the price sliders.
  - Price sliders that matter. Over-charging for rations costs trust; paying poorly for salvage costs trust and supply; generosity drains the treasury.
  - The treasury pays a running cost: upkeep (if D2 = A), so neglect shows.
  - A one-page `docs/economy-model.md`: faucets, sinks, the scarcity lever and the pace targets.
  - A soak that plays three kinds of player (fair, greedy, generous) and asserts each one's outcome.

---

## 1. Findings from the code (why each step exists)

| # | Finding | Where | Consequence |
| :-: | :--- | :--- | :--- |
| E1 | **Gold has one way in and one way out.** In: the session's starting gold (`InitialSeed`) and the provisional export (ore 24g, herb 30g per unit above a reserve of 10, each caravan visit). Out: the caravan's wholesale rations (2g each, topped up to 50 every 600 ticks). Everything else (sales, purchases, bounty escrow) moves gold between the stall and crawlers. | `SupplyService.cs:180-196`, `SupplyTuning.cs:50` | Without the export the stall goes broke in about 3 minutes; with it the treasury grows about 250g a minute with no limit (§8.2). The faucet needs a bound and the treasury needs a sink (D1, D2). |
| E2 | **The trust rule and the crawler's own judgement disagree.** A neutral crawler at hunger 50 will pay up to 20g for a ration (`RationReservationPrice`, 5–45g across personalities and hunger), but `TradeIntentHandler` calls anything above 7.5g gouging (−15 trust, −10 warmth). That threshold is a local constant (5g × 1.5), not the stall's price or the crawler's. Selling salvage always gives +10 trust, whatever the stall pays. | `TradeIntentHandler.cs:83-104, 164`; `Considerations.cs:17, 38-43, 86-90` | Trust falls for 10 of 12 crawlers on the default prices (§8.3). A fair price for a crawler should be judged by that crawler (D3). |
| E3 | **Economy numbers are scattered** across code and data: base values (`ItemTypeExtensions`), starting gold 10g (`AgentState`, `AgentInventory`), stall terms and bounty rewards (`OutpostSession`), wholesale and export prices (`SupplyTuning`), the gouging base (`TradeIntentHandler`), the reference ration price (`Considerations`), Garden yield and regrowth (`Floor1Layout`), hunger rate 1.5/s (`AgentState`). | as listed | Tuning means editing code in six places. One `EconomyTuning` per session first (step 1), so the soak can tune without code changes. |
| E4 | **The Garden can't feed everyone.** 4 plots × 3 rations, regrowing 450 ticks after they empty: at most about 16 rations a minute. Demand: hunger rises 1.5/s, a harvested ration takes off 50, so each crawler needs about 1.8 meals a minute, about 22 a minute for 12. At least a quarter of the meals must come from the stall at 15g. A crawler starts with 10g. | `Floor1Layout.cs:39, 140-143`; `AgentState.cs:160, 198` | A broke crawler meeting an empty Garden starves. That is what `BunkRestScenario` caught again in the close-out run (§1A below). Food pacing is D6. |
| E5 | **Crawler wealth doesn't move:** 20–35g each after 30 minutes, with or without the export. Gold flows stall → crawler → stall. | Epic 9 §8.2 #3 | No crawler-side hoarding problem today, so no crawler sink is needed for Milestone 1 (D2). |
| E6 | **The player's levers already exist and are typed commands:** ration, ore and herb prices (1–50g sliders, `UpdateStallPricesCommand`) and bounty rewards (10–100g). | `FacilityDashboard.uxml:144-239`; `InfluenceEvent.cs` | The soak can play a kind of player by sending the same commands at t0. That is the player's input, not an injected outcome. |
| E7 | **The soak already records the right flows** (`EconomyFlowTotals`, named faucets and sinks, CSV per seed) but asserts only integrity. | `EconomySoakScenario.cs`; `EconomyFlowTotals.cs` | Add trust per crawler and the player profiles; then turn the agreed bands into assertions (step 7). |
| E8 | **Trust is what Milestone 2 spends:** the design proposal recruits crawlers into the Guild once trust is high enough, earned by "trade discounts, stocking preferred items and posting fair bounties". | `Game Design Proposal.md` §2 | The fairness rule decides whether Milestone 2's recruitment works at all. It has to reward fair trade at prices the stall can survive on. |

### 1A. Close-out finding (2026-10-13, input for D6)
The Epic MAP close-out suite run 1 failed `BunkRestScenario` on starvation (53/54): at t3117 `SwarmAgent_4` was panicking at the Ore Quarry door with hunger 100, 10g, 4 known Garden plots and no plan, 445 ticks at 95+ hunger. `8ea87e0` fixed the case where a crawler panics with a ration in its pocket. This one is different: the crawler has no ration and no money, and the Garden is empty. Starvation panic then sends it to explore the charted room it has visited least recently, away from the Garden, rather than waiting for the plots to regrow. Runs 2–3: run 2 starved the same crawler at the same tick; run 3 took a different trajectory (first sleep t839 instead of t885) and starved nobody (worst stretch 233 ticks). After the assertion became a report, three more runs: starved in 1 of 3 (the t885 trajectory again). In total 3 of 6 runs, with exactly two trajectories, chosen by cognition timing (§1A.2 #6). **Producer decision 2026-10-13:** handed to §3.3; `BunkRestScenario` reports starvation instead of asserting it until step 6 lands, and step 6 restores the assertion.

---

## 2. The model (recommended; each choice is a decision in §7)

### 2.1 Where gold comes from (D1 = A recommended)
**The caravan becomes a real market with a limited appetite.** On each visit it buys up to a quota of salvage per type (for example 8 ore and 8 herbs) at a market price. It still sells wholesale rations. The `ProvisionalCaravanExport` faucet is renamed `CaravanSalvageSales`, and the `Provisional*` names and the export switch are removed. The faucet's ceiling is quota × price per visit, so the treasury can't grow without limit.

### 2.2 Where gold goes (D2 = A recommended)
**Outpost upkeep:** the treasury pays a fixed sum every in-game period, for example 40g every 600 ticks, as an `OutpostUpkeep` sink. Upkeep is what makes "a careless player goes broke" possible. If the treasury can't pay, the stall stops buying salvage until it can, and the HUD shows that.
- **No crawler sink for Milestone 1** (E5). Lodging fees and gear wear wait for Epic 10, where gear exists.

### 2.3 What counts as fair (D3 = A recommended)
**Each crawler judges a price by its own standards,** using the numbers it already decides with:
- **Buying a ration:** the price as a share of its reservation price (`Considerations.RationReservationPrice`). At most 75% is generous (+10 trust), up to 100% is fair (+3), above 100% is gouging (−15). Above 100% it already refuses to buy, so gouging only happens to a desperate buyer: hunger raises the reservation price, and a starving crawler pays what a fed one wouldn't, and resents it.
- **Selling salvage:** the stall's buy price against the crawler's minimum sale price (`Considerations.MinimumSalePrice`). At least 2× is generous (+10), at least 1× is fair (+3), below it is a lowball (it doesn't sell, no trust change).
- All thresholds live in `EconomyTuning`, so the soak can tune them.

### 2.4 What limits the player (D4 = A recommended)
**For Milestone 1, the crawlers' willingness to trade is the lever**, with material scarcity as a backstop: the player's prices against each crawler's reservation and minimum prices, which already vary with personality. Gear-gated material scarcity is Epic 10's question; the design proposal's open question about top-tier gear has no gear to apply to yet.

### 2.5 Pace targets (D5)
The proposal, measured over 30 sim-minutes on 3 seeds, each profile set by price commands at t0:

| Profile | Prices (ration / ore / herb) | Treasury after 30 min (start 250g) | Trust toward the player | Starving |
| :--- | :--- | :--- | :--- | :--- |
| **Fair** (defaults) | 15 / 20 / 25 | grows, 1.5–3× | rises for at least 8 of 12 | 0 |
| **Greedy** | 30 / 10 / 12 | grows faster in the first 10 min, then sales fall off | falls for at least 8 of 12 | not asserted |
| **Generous** | 5 / 35 / 40 | reaches 0 before 30 min | rises for all 12 | 0 |

The numbers are placeholders to make the shape concrete: the producer sets them (D5). Once set they become the soak's assertions.

### 2.6 Food (D6 = A recommended)
- **Data, not code:** the Garden scales with the population, a crawler's starting gold covers at least one ration, and rations stay below one salvage sale (ration price ≤ ore buy price, today 15 ≤ 20).
- If 1A's failure persists, a starving crawler waits by known regrowing plots instead of exploring away from them. That is a behaviour fix, made only with the soak's evidence.

### 2.7 Drifters (D7 = A recommended)
**Deferred to Milestone 2.** Residents keep the trust they've built; churn would wipe it and make Milestone 1's trust measurement meaningless. The bounded caravan market does the faucet's job.

---

## 3. Steps and commits

Each step must compile and keep PlayMode green (`scripts/run-playmode-tests.sh`, Editor closed); each commit body states the real test status.

| Step | Work | Gate | Commit |
| :-: | :--- | :--- | :--- |
| 0 | **This plan;** producer decisions D1–D7 | Producer approves §7 | `docs(economy): §3.3 economy model plan` |
| 1 | **`EconomyTuning`** (session data): base values, stall terms, starting gold, wholesale, caravan, fairness thresholds, upkeep, Garden data. Every current number moves in unchanged (E3). | A validator checks the defaults equal today's constants; full suite green; milestones unchanged | `refactor(economy): one economy tuning per session` |
| 2 | **Soak profiles and baseline:** `EconomySoakScenario` plays fair, greedy and generous through `UpdateStallPricesCommand` at t0 and records trust per crawler. Measured on today's model. | 3 seeds × 3 profiles, CSVs; numbers into Epic 9 §8.5 | `test(economy): the soak plays three kinds of player` |
| 3 | **Fairness rule (D3).** Trust from trade judged by the crawler's own prices. `OutpostLifecycleScenario` asserts trust again (criterion 7). | `TradeTransactionValidator` (generous, fair, gouging and lowball cases); lifecycle passes with trust | `feat(economy): crawlers judge a fair price by their own standards` |
| 4 | **Faucet (D1):** the caravan market with quotas; the provisional export and its switch removed. | `SupplyLoopValidator` (quota respected; ledger names the faucet); soak re-measured | `feat(economy): the caravan buys salvage up to its quota` |
| 5 | **Sink (D2):** outpost upkeep; the stall stops buying when it can't pay, and the HUD shows it. | `EconomyCountersValidator` (upkeep paid on schedule, ledger conserved, a broke stall refuses sales); soak re-measured | `feat(economy): the outpost pays upkeep` |
| 6 | **Food pacing (D6):** data first, then the panic-near-food behaviour if the evidence calls for it; `BunkRestScenario` asserts "no crawler starves" again. | No starving in the fair and generous profiles; `BunkRestScenario` asserts it and passes 3 of 3 | `fix(map): …` / `fix(drives): …` |
| 7 | **Tune to the bands** (D5) through `EconomyTuning` only; write `docs/economy-model.md`; **producer play session** at 1× with the sliders. Then the soak asserts its bands. | Bands hold on 3 seeds; the producer agrees the game plays right | `feat(economy): the economy model's numbers`, `test(economy): the soak asserts the pace` |
| 8 | **Docs and promotion:** Epic 9 → `COMPLETED`, Epic Test-2 → `COMPLETED` (if its soak scope is then delivered), ROADMAP Milestone 1 status (MemoryPack remains), epic docs, `curentfocus.txt`, `architecture_history.md`; this plan deleted. | Full suite ×3 green | `docs: the economy model; Epic 9 complete` |

---

## 4. Tests added or changed
- **New:** fairness cases in `TradeTransactionValidator`; caravan-quota and upkeep checks; an `EconomyTuning` defaults check.
- **Changed:**
  - `EconomySoakScenario`: player profiles and trust; asserted bands after step 7. It stays `[Explicit]`, since it is long; one short fair-profile run may join the regular suite if it is fast enough.
  - `OutpostLifecycleScenario`: trust asserted again.
  - `PriceResponseScenario`: re-measured, since it reads reservation prices.
- **Re-checked after every step:** supply-loop, bunk and lifecycle milestones.

---

## 5. Risks

| Risk | Mitigation |
| :--- | :--- |
| Tuning becomes endless | Bands are agreed before tuning (D5); tuning happens only through `EconomyTuning` and the soak; one producer play session decides |
| Upkeep plus a bounded faucet starves the stall, so crawlers starve | The fair profile asserts 0 starving; the stall stops buying salvage before it stops selling food |
| The fairness rule makes trust trivially easy or impossible | The thresholds are data; the greedy and generous profiles bracket them |
| Changing the trust rule moves Pen 1–3 canaries on the Dev Map | Pens use neutral personalities; re-run `PenCanaryScenario` at step 3 |
| The close-out starvation (§1A) is a behaviour bug, not pacing | Step 6 decides on evidence; a behaviour fix gets its own commit and validator |
| Determinism: new periodic events (upkeep, caravan quota) | Ticks only, fixed order, ledger-named; `DeterminismScenario` stays green |

## 6. Out of scope
- `MemoryPack` serialization (the rest of Milestone 1).
- Gear, durability and gear-gated scarcity (Epic 10).
- Drifters (D7 = A), Guild recruitment thresholds (Milestone 2), the "why?" inspector (§3A #10b).
- The dossier's *Feed ration* button (Epic MAP §6 #9, Track B).

## 6A. Deviations from this plan during implementation
- **Step 1:** fairness thresholds, upkeep and the caravan quota are added to `EconomyTuning` in steps 3–5, where they get a reader, not in step 1.
- **Step 1:** Garden yields and regrowth stay map data in `Floor1Layout` (`MapDefinition` is the single source of truth for nodes).
- **Step 1:** the hunger rate stays in `AgentState` (a drive, not an economy number).
- **Step 1:** personality trait strengths stay in `Considerations`.
- **Step 3:** a purchase is judged against the crawler's *calm* valuation (its reservation price at hunger 50), not the hunger-raised one. The hunger-raised price already decides whether it buys, so judging against it would never find gouging.
- **Step 3:** a sale gets a third band, *stingy*: at or above the minimum but below 1.5× (−5 trust). Without it, low salvage prices still earned trust.
- **Step 3:** the generous bands are buy ≤ 0.6× and sale ≥ 3× (§2.3 said 0.75× and 2×). With the plan's ratios, the default prices read as generous and trust saturated just as it does for the generous player.
- **Step 3:** the lifecycle asserts trust rising for at least ⅔ of crawlers.
- **Step 4:** the caravan buys with no reserve. The stall also gets a salvage capacity (20 per type): when full, it stops buying, and crawlers' targeting sees that. Without the capacity, the stall would keep buying salvage the caravan won't take.
- **Step 4:** the export switch (`SimulationSettings.ProvisionalExportEnabled`, `SIMPIENS_PROVISIONAL_EXPORT`, the runner's `--export`) is removed, not renamed.

## 7. Decisions

**D1: where new gold comes from.**

| Option | Meaning | Trade-off |
| :--- | :--- | :--- |
| **A (recommended)** | The caravan buys salvage up to a quota per visit at a market price | Smallest change: it is today's export with a ceiling. Deterministic and easy to tune |
| B | Drifters arrive through the Road Gate with gold and leave | Matches the design proposal's "selling provisions to drifters", but churn wipes the trust Milestone 1 measures; much more work |
| C | Keep the export unbounded and add only sinks | Two forces to balance instead of one ceiling; the treasury's growth is still open-ended |

**D2: where gold leaves.**

| Option | Meaning | Trade-off |
| :--- | :--- | :--- |
| **A (recommended)** | Outpost upkeep from the treasury on a schedule | Gives neglect a cost; simple; no new crawler behaviour |
| B | Upkeep plus a lodging fee per bunk sleep | Crawler-side pressure, but crawlers are already poor (E5) and food is tight (E4) |
| C | Garden scarcity only (less free food) | Moves pressure onto crawlers' food, the part that already starves |

**D3: what counts as a fair price.**

| Option | Meaning | Trade-off |
| :--- | :--- | :--- |
| **A (recommended)** | Each crawler's own reservation and minimum prices (§2.3) | Consistent with how crawlers already decide; personality and hunger matter; gouging means profiting from desperation |
| B | The stall's going rate (a moving average of its own prices) | Any price becomes fair once it has held long enough; the slider stops meaning anything |
| C | Today's rule with the base retuned (e.g. 20g instead of 5g) | One-line fix, but still blind to the crawler and its hunger; salvage prices still don't matter |

**D4: what limits the player** (the design proposal's open question).

| Option | Meaning | Trade-off |
| :--- | :--- | :--- |
| **A (recommended)** | Crawlers' willingness to trade, for Milestone 1; materials as a backstop | Uses the price sliders and personalities that exist; the gear question waits for gear (Epic 10) |
| B | Material scarcity first (fewer nodes, slower regrowth) | Strong pressure, but on food and supply, which are already tight |

**D5: the pace targets.** Approve, change or replace the three profiles and their bands in §2.5. They become assertions.

**D6: food pacing.**

| Option | Meaning | Trade-off |
| :--- | :--- | :--- |
| **A (recommended)** | Data first (Garden scaled to population, starting gold ≥ one ration); then a panic-near-food fix only if evidence calls for it | No behaviour change unless needed |
| B | Lower the hunger rate (e.g. 1.0/s) | Crawlers spend less of the day eating and the stall sells less food; shifts every milestone |
| C | Leave it; accept rare starvation as part of the game | `BunkRestScenario` and the lifecycle's "nobody starves" stay flaky |

**D7: drifters.** **A (recommended):** defer to Milestone 2. B: build them now as the faucet (D1 = B).

**Minor decisions (producer may override):**
1. Upkeep and the caravan quota run on the caravan's 600-tick schedule, so the economy has one rhythm.
2. A crawler that refuses a lowball price doesn't lose trust; only a completed trade changes trust.
3. Bounty rewards keep today's trust effects; bounty fairness is Milestone 2's question.

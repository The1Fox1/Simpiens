# The Outpost Economy Model

- **Owner:** review §3.3 / [ROADMAP §3A #10a](ROADMAP.md#3a-architecture-review-remediation-tracker). Status is owned by the roadmap.
- **Evidence:** [Epic 9 §8](epics/epic-9.md) (Milestone 1); [Epic 10 §4 and §7](epics/epic-10.md) (drifters, glowstone, the bands since).
- **Numbers:** one `EconomyTuning` per session ([`EconomyTuning.cs`](../dotnet/Simpiens.Core/Simulation/Economy/EconomyTuning.cs), with `SupplyTuning` and `DrifterTuning`). Since the item system refactor (2026-10-26):
  - each item's terms are a table: its base value and the stall's role, price and stock, plus the caravan's exports and supplies (`ItemTerms`, `CaravanTerms`);
  - what an item does (hazard gear's protection and life, a ration's or a herb's effect) is its definition in the item catalog. The Garden and the hazard sectors are map data (`Floor1Layout`).
- **Decided 2026-10-14** by the agent under the producer's provisional authority. Reviewed, merged (PR #3) and play-tested in the Unity editor by the producer on 2026-10-15. Extended by Epic 10: drifters (G1), glowstone (G2) and hazard gear (G3).

## 1. The loop in one paragraph
Crawlers harvest ore and herbs and sell them to the player's stall. They spend that gold on stall rations or eat free from the Garden. The caravan visits every minute:
1. it buys a fixed quota of the stall's salvage at a market price (the main way new gold enters), glowstone from the hazard sectors at a premium since Epic 10 G2;
2. the stall pays its upkeep (the main way gold leaves);
3. the caravan sells the stall wholesale rations;
4. then hazard gear, since Epic 10 G3: food comes first.

The player sets five prices (rations, ore, herbs, glowstone since Epic 10 G2, hazard gear since G3) and posts bounties. Each crawler judges every trade by its own valuation, and trust toward the player follows those judgements.

**Hazard gear (Epic 10 G3, 2026-10-25)** is the crawlers' own sink, deferred by Epic 9 D2.
- **What it does:** a bold crawler buys gear at the stall before going into a hazard sector. Worn, it halves the damage and wears out after 50 s in danger, so the crawler buys again.
- **Where the gold goes:** the crawler's gold goes to the stall, and the stall pays the caravan wholesale for the next piece.

**Drifters (Epic 10 G1, 2026-10-23)** open the economy: strangers come in by the Road Gate with a small purse, buy a meal or two, sometimes work, and move on after three to five minutes. Their purses are the second way new gold enters; what they still carry leaves with them. The 12 residents stay.

## 2. Faucets and sinks (all named in the ledger)
| Flow | Name | Size | Bound |
| :--- | :--- | :--- | :--- |
| Faucet | `InitialSeed` | stall treasury 250g + 15g per crawler | once, at tick 0 |
| Faucet | `CaravanSalvageSales` | 2 ore × 24g + 2 herbs × 30g per visit | 108g a visit for ore and herbs; with glowstone's row below, `SupplyTuning.MaxSalvagePaymentPerVisit` is 248g |
| Sink | `OutpostUpkeep` | 80g per visit | fixed; a stall that can't pay goes into arrears |
| Sink | `SupplyCaravan` | 2g per wholesale ration, topping stock up to 50 | as much as the stall can pay for (partial restocks) |
| Faucet | `CaravanSalvageSales` (glowstone) | up to 2 glowstone × 70g per visit (Epic 10 G2); the stall pays crawlers 45g | six nodes in the hazard sectors, one unit each, regrowing in 10 minutes |
| Sink | `GearWholesale` | 20g per hazard gear, topping stock up to 3 after the rations (Epic 10 G3) | as much as the stall can pay for; the stall sells it to crawlers at the player's price (default 30g) |
| Faucet | `DrifterPurses` | 20–26g per drifter, about one every 3.5 minutes from minute 2 | at most 3 drifters at once (`DrifterTuning`) |
| Sink | `DrifterDepartures` | what a drifter still carries when it leaves | its purse plus what it earned |
| Transfer | purchases, sales, bounty escrow | between the stall and crawlers | conserved |

**What follows:** the stall's net per visit ≈ salvage faucet − upkeep − wholesale − growth in crawler wallets.
- Crawlers spend almost all they earn, so their gold passes through.
- The treasury grows only when crawlers sell enough salvage to fill the caravan's quota, which happens when the stall's prices are fair.

## 3. The scarcity lever: crawlers' willingness to trade (D4)
Every crawler values things with its personality and needs:
- **A ration:** calm value = 20g × greed factor (neutral 20g, greedy 10g, generous 30g). Hunger raises what it will pay, up to 1.5× when starving.
- **Salvage:** minimum sale price = base value × greed factor (neutral ore 8g, herb 12g).
- **Hazard gear (Epic 10 G3):** calm value = 40g × greed factor (neutral 40g, greedy 20g, generous 60g). No need raises it, so it is also the most a crawler pays.
    - **Who buys:** only a crawler bold and healthy enough to go into danger, wearing no gear, that can still pay for a ration after.
    - **Why 30g:** the default is 0.75 of the neutral value, as the ration's 15g is of 20g. At 40g, the base value, nobody with any greed bought.
    - **Gear's scarcity:** the caravan's three a visit and the crawlers' purses. Those are the "gear-gated scarcity" Epic 9 deferred.

A crawler refuses a price outside its valuation. Over-pricing doesn't only cost trust: it cuts trade volume, and volume is what fills the caravan's quota.

## 4. Fairness and trust (D3)
Only completed trades change trust; a refused trade changes nothing.

| Trade | Generous (+10 trust) | Fair (+3) | Unfair |
| :--- | :--- | :--- | :--- |
| Crawler buys a ration | price ≤ 0.6 × calm value | ≤ 1 × calm value | **Gouged** (−15): above its calm value, so hunger made it pay |
| Crawler buys hazard gear (Epic 10 G3) | price ≤ 0.6 × calm value | ≤ 1 × calm value | none: it never pays above its calm value |
| Crawler sells salvage | price ≥ 3 × its minimum | ≥ 1.5 × minimum | **Stingy** (−5): at its minimum or just above |

The default prices (15 / 20 / 25) are fair to a neutral crawler. Trust regresses toward neutral over time, so a player keeps trust only by keeping trade fair.

## 5. The stall's limits
- **Salvage capacity:** 20 of each type. A full stall stops buying until the caravan takes its quota, and crawlers stop harvesting salvage for it.
- **Arrears:** when upkeep can't be paid in full, the stall pays what it has (no debt) and buys no salvage until a visit pays in full. It keeps selling rations, which is how it recovers. The merchant view shows the arrears and the chronicle records it.
- **Bankruptcy is felt as hunger.** With no gold the caravan delivers no rations, and the outpost falls back on the Garden.

## 6. Food (D6)
- **Hunger** rises 1.5/s; a ration takes off 50, so a crawler needs about 1.8 meals a minute (12 crawlers: about 22).
- **The Garden:** 4 plots × 4 rations, regrowing 300 ticks after they empty, about 32 a minute at most (`Floor1Layout.FoodPlotYield`, `FoodRegrowTicks`). It's free, but the plots are shared and walking takes time; crawlers harvest about 9 a minute in practice.
- **A crawler arrives with 15g,** one ration at the default price.
- **Invariant:** a ration costs no more than one ore sale (15 ≤ 20), so one sale always buys a meal.
- **Behaviour:** a starving crawler that knows no reachable food goes back to the Garden plots it has seen and waits for them to regrow. It doesn't explore away.

## 7. Pace targets (D5), asserted by the soak
`simpiens soak` and `dotnet test` (`EconomySoakTests`) run 30 sim-minutes for each of 3 seeds; each profile is set by a price command at tick 0, and every run is checked against its profile's band (`EconomySoak`). (Unity's `scripts/run-economy-soak.sh` did the same until Unity's removal on 2026-10-24.)

| Profile | Prices (ration / ore / herb; gear since Epic 10 G3) | Band (per run, at 30 min) | Measured 2026-10-25 with gear, headless (3 seeds) |
| :--- | :--- | :--- | :--- |
| Fair | 15 / 20 / 25; gear 30 | treasury 1,000–2,000g (the producer, 2026-10-25; was 375–875g before Epic 10 G2's glowstone); trust up for ≥ 8 of 12; 0 starving episodes | 1,499–1,564g; trust up 11–12; 0 starving (before gear 1,545–1,594g; 2026-10-15, before Epic 10: 546–771g) |
| Greedy | 30 / 10 / 12; gear 60 | trust up for ≤ 2 of 12; trust mean below 0 at its lowest (since 2026-10-23, Epic 10 G0; it was at 30 min) (treasury not asserted) | trust up 0, mean −2.5 to −0.8 at 30 min; treasury 0g at 30 min on every seed |
| Generous | 5 / 35 / 40; gear 20 | the stall goes broke (treasury 0 or in arrears); trust up for ≥ 8 of 12 at its peak | broke by minute 10 on every seed; trust up 11–12; 0 starving |

- **Why greed paid nothing before drifters:** the economy was closed. Without an outside buyer, greedy prices only shrank the trade that fills the caravan's quota. Since Epic 10 G1 drifters bring outside gold: drifters leave gold behind on every soak seed (their purses exceed what they take away), and a slightly greedy player (rations 20, ore 18, herbs 22: the soak's `keen` profile) ends the 30 minutes richer than the same player in a closed outpost over the soak's seeds (`dotnet test` asserted it in `AKeenPlayerDoesBetterWithDrifters` until Epic 10 G3; `simpiens soak --profile keen [--closed]`). Measured 2026-10-23, headless: 2,316g against 2,154g summed (886 against 796, 652 against 652, 778 against 706); drifters left 139g, 35g and 86g behind. Per seed the comparison is noisy: a run with drifters is a different run of a chaotic day.
    - **Since glowstone the comparison is within that noise** (Epic 10 F4). Measured 2026-10-25 with gear over seeds 1–15: 19,355g against 19,249g, ahead on 8 of 15.
    - The test (`DriftersLeaveGoldBehindForAKeenPlayer`) now asserts what is causal: drifters leave gold behind on every soak seed. It reports the treasury comparison.
- **The fair band since glowstone (Epic 10 G2, 2026-10-23):** the hazard sectors' glowstone is new money by design (the caravan's premium); the fair player ends at 1,545–1,594g, the same in Unity and headless, so the band was re-set to 1,000–2,200g, and the producer set it to 1,000–2,000g on 2026-10-25 (re-measured: 1,545–1,594g). Gear wear (G3) and wages (G4) are the costs meant to meet it. Measured before G2, with drifters: 768–831g.
- **The fair band with drifters:** it holds (768–831g headless, 2026-10-23). Larger purses broke it: 40–80g every two minutes nearly doubled the fair player's treasury (1,189–1,273g), so the tuning was cut until the band held.
- **Why greed is judged at its lowest trust (2026-10-23, Epic 10 G0):** greed costs trust early, through stingy sales. Once the stall is empty nobody trades, and trust drifts back to neutral by minute 30. Before G0's navigation fixes, starving crawlers stuck near the stall bought gouged rations late in the run, and that kept the mean below 0 at the end; now they eat at the Garden. The generous band is judged at its peak for the same reason.
- **Why generous starvation isn't asserted:** a bankrupt outpost can't pay for food. Partial restocks soften that, and the soak measured 0 anyway.
- **The regular suite** asserts the fair case over 15 minutes: the outpost's day (`OutpostScenarioTests`, on 23 gate seeds) checks that trust rises for ⅔ of crawlers and nobody starves.
- **Headless (.NET 10, since 2026-10-17; the only runtime since Unity's removal on 2026-10-24):** `simpiens soak` and `dotnet test` assert the bands, all 9 runs in about 4 s. Until Epic 10 G2 the numbers differed from Unity's, because Mono rounded some float expressions differently (epic CORE §6 F1); since G2's rounding fixes the 9 runs end on identical numbers in both runtimes.
- **Across seeds:** the 15-minute day (`simpiens sweep`, the lifecycle checks) passes on all 23 gate seeds since Epic 10 G0, and `dotnet test` asserts it (`OutpostWatch.GateSeeds`). On 2026-10-17 it passed on 16 of 23 (epic CORE §6 F2, resolved by G0: crawlers that couldn't move, not a food shortage). Wider sweeps leave a few trust shortfalls (Epic 10 F3).

## 8. Deferred
- ~~**Drifters** (outside buyers and churn): Milestone 2.~~ Delivered by Epic 10 G1 (2026-10-23): §1, §2, §7.
- ~~**Gear, wear and gear-gated scarcity:** Epic 10 G3.~~ Delivered by Epic 10 G3 (2026-10-25): §1, §2, §3, §4, §7. Crafted gear, grades and repair: Epic 10 Part 2.
- **Wages, bounty fairness and Guild recruitment thresholds:** Epic 10 G4 (Milestone 2).
- **Lodging fees and other crawler sinks:** not needed while crawler wallets stay flat.

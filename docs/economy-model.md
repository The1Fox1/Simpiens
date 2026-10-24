# The Outpost Economy Model (Milestone 1)

- **Owner:** review §3.3 / [ROADMAP §3A #10a](ROADMAP.md#3a-architecture-review-remediation-tracker). Status is owned by the roadmap.
- **Evidence:** [Epic 9 §8](epics/epic-9.md).
- **Numbers:** one `EconomyTuning` per session (`Assets/Scripts/Simulation/Economy/EconomyTuning.cs`, with `SupplyTuning` nested). The Garden is map data (`Floor1Layout`).
- **Decided 2026-10-14** by the agent under the producer's provisional authority. Reviewed, merged (PR #3) and play-tested in the Unity editor by the producer on 2026-10-15.

## 1. The loop in one paragraph
Crawlers harvest ore and herbs and sell them to the player's stall. They spend that gold on stall rations or eat free from the Garden. The caravan visits every minute:
1. it buys a fixed quota of the stall's salvage at a market price (the only way new gold enters);
2. the stall pays its upkeep (the main way gold leaves);
3. the caravan sells the stall wholesale rations.

The player sets three prices (rations, ore, herbs) and posts bounties. Each crawler judges every trade by its own valuation, and trust toward the player follows those judgements.

## 2. Faucets and sinks (all named in the ledger)
| Flow | Name | Size | Bound |
| :--- | :--- | :--- | :--- |
| Faucet | `InitialSeed` | stall treasury 250g + 15g per crawler | once, at tick 0 |
| Faucet | `CaravanSalvageSales` | 2 ore × 24g + 2 herbs × 30g per visit | at most 108g a minute (`SupplyTuning.MaxSalvagePaymentPerVisit`) |
| Sink | `OutpostUpkeep` | 80g per visit | fixed; a stall that can't pay goes into arrears |
| Sink | `SupplyCaravan` | 2g per wholesale ration, topping stock up to 50 | as much as the stall can pay for (partial restocks) |
| Transfer | purchases, sales, bounty escrow | between the stall and crawlers | conserved |

**What follows:** the stall's net per visit ≈ salvage faucet − upkeep − wholesale − growth in crawler wallets.
- Crawlers spend almost all they earn, so their gold passes through.
- The treasury grows only when crawlers sell enough salvage to fill the caravan's quota, which happens when the stall's prices are fair.

## 3. The scarcity lever: crawlers' willingness to trade (D4)
Every crawler values things with its personality and needs:
- **A ration:** calm value = 20g × greed factor (neutral 20g, greedy 10g, generous 30g). Hunger raises what it will pay, up to 1.5× when starving.
- **Salvage:** minimum sale price = base value × greed factor (neutral ore 8g, herb 12g).

A crawler refuses a price outside its valuation. Over-pricing doesn't only cost trust: it cuts trade volume, and volume is what fills the caravan's quota.

## 4. Fairness and trust (D3)
Only completed trades change trust; a refused trade changes nothing.

| Trade | Generous (+10 trust) | Fair (+3) | Unfair |
| :--- | :--- | :--- | :--- |
| Crawler buys a ration | price ≤ 0.6 × calm value | ≤ 1 × calm value | **Gouged** (−15): above its calm value, so hunger made it pay |
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
`scripts/run-economy-soak.sh` runs 30 sim-minutes for each of 3 seeds; each profile is set by a price command at tick 0. `EconomySoakScenario.AssertPace` checks every run against its profile's band.

| Profile | Prices (ration / ore / herb) | Band (per run, at 30 min) | Measured 2026-10-15 (3 seeds) |
| :--- | :--- | :--- | :--- |
| Fair | 15 / 20 / 25 | treasury 375–875g (1.5–3.5× the 250g start); trust up for ≥ 8 of 12; 0 starving episodes | 546–771g; trust up 9–10; 0 starving |
| Greedy | 30 / 10 / 12 | trust up for ≤ 2 of 12; trust mean below 0 at its lowest (since 2026-10-23, Epic 10 G0; it was at 30 min) (treasury not asserted) | trust up 0, mean −3 to −17; treasury 0–216g, below fair on every seed |
| Generous | 5 / 35 / 40 | the stall goes broke (treasury 0 or in arrears); trust up for ≥ 8 of 12 at its peak | broke by minute 10; peak trust up 12; 0 starving |

- **Why greed doesn't pay here:** the economy is closed. Without an outside buyer (drifters, Milestone 2), greedy prices only shrink the trade that fills the caravan's quota.
- **Why greed is judged at its lowest trust (2026-10-23, Epic 10 G0):** greed costs trust early, through stingy sales. Once the stall is empty nobody trades, and trust drifts back to neutral by minute 30. Before G0's navigation fixes, starving crawlers stuck near the stall bought gouged rations late in the run, and that kept the mean below 0 at the end; now they eat at the Garden. The generous band is judged at its peak for the same reason.
- **Why generous starvation isn't asserted:** a bankrupt outpost can't pay for food. Partial restocks soften that, and the soak measured 0 anyway.
- **The regular suite** asserts the fair case over 15 minutes: `OutpostLifecycleScenario` checks that trust rises for ⅔ of crawlers and nobody starves.
- **Headless (.NET 10, 2026-10-17):** `simpiens soak` and `dotnet test` assert the same bands without Unity, all 9 runs in about 4 s. Measured: fair 664–768g, trust up 8–10, 0 starving; greedy trust up 0, mean −4.6 to −12.6, treasury 0–342g, below fair on every seed; generous broke by minute 10, peak trust up 12. The numbers differ from Unity's because Mono rounds some float expressions differently (epic CORE §6 F1); both runtimes are inside every band.
- **Across seeds (headless sweep, 2026-10-17):** the 15-minute day (`simpiens sweep`, the lifecycle checks) passes on 16 of 23 seeds: 5 seeds have a crawler starving (hunger ≥ 95 for 300+ ticks) and 2 a crawler that never closes a cycle. Unity agrees on seed 11 (two crawlers starve there too). Only the default seed is asserted. Open: epic CORE §6 F2.

## 8. Deferred
- **Drifters** (outside buyers and churn): Milestone 2.
- **Gear, wear and gear-gated scarcity:** Epic 10.
- **Bounty fairness and Guild recruitment thresholds:** Milestone 2.
- **Lodging fees and other crawler sinks:** not needed while crawler wallets stay flat.

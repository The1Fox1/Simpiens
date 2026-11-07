# The Outpost Economy Model

- **Owner:** review §3.3 / [ROADMAP §3A #10a](ROADMAP.md#3a-architecture-review-remediation-tracker). Status is owned by the roadmap.
- **Evidence:** [Epic 9 §8](epics/epic-9.md) (Milestone 1); [Epic 10 §4 and §7](epics/epic-10.md) (drifters, glowstone, the guild); [Epic 10 Part 2](epics/epic-10-part-2.md) and the [time model](time-model.md) §2 (the economy per day, T4–T5).
- **Numbers:** one `EconomyTuning` per session ([`EconomyTuning.cs`](../dotnet/Simpiens.Core/Simulation/Economy/EconomyTuning.cs), with `SupplyTuning`, `DrifterTuning`, `GuildTuning`, `WorkshopTuning` and the day's pace, `DayTuning`). Since the item system refactor (2026-10-26):
  - each item's terms are a table: its base value and the stall's role, price and stock, plus the caravan's exports and supplies (`ItemTerms`, `CaravanTerms`);
  - what an item does (hazard gear's protection and life, a ration's or a herb's effect) is its definition in the item catalog. The Garden and the hazard sectors are map data (`Floor1Layout`).
- **History:**
  - **Decided 2026-10-14** by the agent under the producer's provisional authority; reviewed, merged (PR #3) and play-tested by the producer on 2026-10-15.
  - **Extended by Epic 10:** drifters (G1), glowstone (G2), hazard gear (G3), the guild (G4) and expeditions (G5).
  - **Re-paced for the 24-hour day** by Epic 10 Part 2, milestone T, 2026-11-05 (D23–D29, approved by the developer; T5 made it the game). Before the day, the caravan came every real minute, salvage paid four times as much, and the soak played 30 minutes: see git history for the model as it was.

## 1. The loop in one paragraph
Crawlers harvest ore and herbs and sell them to the player's stall. They spend that gold on stall rations, and sometimes eat free from the Garden. A crawler eats two or three times a day, sleeps about 8 hours, and works toward a purse of 80g (D25). The caravan visits every 6 game hours, at 12:00, 18:00, 00:00 and 06:00 (D24). On each visit:
1. it buys the stall's salvage up to its lots at a market price (the main way new gold enters), and glowstone from the hazard sectors at a premium;
2. the stall pays its upkeep (the main way gold leaves);
3. the caravan sells the stall wholesale rations;
4. the stall pays the guild's wages, then the artisans' (Epic 10 Part 2);
5. then hazard gear: food, then people, then stock.

The player sets five prices (rations, ore, herbs, glowstone, hazard gear) and posts bounties. Each crawler judges every trade by its own valuation, and trust toward the player follows those judgements.

**Work on the day (D29).** Salvage pays a quarter of Part 1's price, so a day's meals (about 35–40g) take a resident some 5–6 hours of harvesting and selling, RimWorld-style: harvest trips 4.6 game hours a day and selling 1.1, idle 13% (measured on the 23 gate seeds over three days). The caravan's bigger lots let the stall keep buying what a day's work brings in. D25's 6–8 hours are near, not met; made goods to buy (the pickaxe, draughts) are meant to bring the rest.

**Hazard gear (Epic 10 G3)** is the crawlers' own sink.
- **What it does:** a bold crawler buys gear at the stall before going into a hazard sector. Worn, it halves the damage and wears out after 1,500 ticks in danger (about 3.6 game hours), so the crawler buys again.
- **Where the gold goes:** the crawler's gold goes to the stall, and the stall pays the caravan wholesale for the next piece.

**The guild (Epic 10 G4)** is the player's cost of keeping people.
- **Joining:** a resident at trust 60 and warmth 40 signs on for a 20g fee from the stall. On the day four residents qualify by about day 2 (T4.2b): trust follows six sales a day.
- **Wages:** each visit pays every member its wage (5g by default, 20g a day) in join order, each in full or not at all.
- **Where the gold goes:** both are transfers from the stall to members, who spend them.

**Expeditions (Epic 10 G5)** bring back what nobody gathers alone.
- **The chamber:** the Drowned Gallery's one glowstone seam (yield 4, regrowing in about 14 game hours) is worked only by a party: a crawler alone never braves more than 1 health a second, and the Gallery takes 1.5.
- **The muster** rouses a member who has slept past the tired line (D31), as RimWorld's drafted colonists wake.
- **The haul:** it goes into the stall's stock without a sale, and the caravan buys it at its premium as any glowstone. The members split 30% of its caravan value (21g a glowstone), paid from the stall on delivery as transfers, each in full or not at all.
- **What the stall gives from stock, no gold:** hazard gear for members who need it, and two rations each at the muster. The caravan restocks both at wholesale, so they are the expedition's cost.
- **One trip:** about 280g of glowstone for the caravan, 84g in shares, gear and rations from stock. The soak's `guild` profile plays them (§7).

**Drifters (Epic 10 G1)** open the economy: strangers come in by the Road Gate with 20–26g, about one every 5 game hours from the third, and stay 4–7 game hours. On the day they work too (F11): they sell salvage, so they take away more gold than they bring, but a stall ends richer with them than in a closed outpost (§7).

## 2. Faucets and sinks (all named in the ledger)
A day is 4 caravan visits. Measured per day on the gate seeds (T4.2b): the caravan pays about 890g, upkeep takes 320g, wholesale rations about 56g, gear about 21g; the stall sells 29 rations (455g) and buys 92 units of salvage (660g).

| Flow | Name | Size | Bound |
| :--- | :--- | :--- | :--- |
| Faucet | `InitialSeed` | stall treasury 250g + 15g per crawler | once, at tick 0 |
| Faucet | `CaravanSalvageSales` | up to 16 ore × 6g + 12 herbs × 7g a visit (the stall pays 5g and 6g) | 180g a visit for ore and herbs; with glowstone's row below, `SupplyTuning.MaxSalvagePaymentPerVisit` is 320g |
| Sink | `OutpostUpkeep` | 80g a visit, 320g a day | fixed; a stall that can't pay goes into arrears |
| Sink | `SupplyCaravan` | 2g per wholesale ration, topping stock up to 50 | as much as the stall can pay for (partial restocks) |
| Faucet | `CaravanSalvageSales` (glowstone) | up to 2 glowstone × 70g a visit; the stall pays crawlers 45g | six nodes in the hazard sectors, one unit each, regrowing in about 14 game hours |
| Sink | `GearWholesale` | 20g per hazard gear, topping stock up to 3 after the rations | as much as the stall can pay for; the stall sells it at the player's price (default 30g) |
| Faucet | `DrifterPurses` | 20–26g per drifter, about one every 5 game hours | at most 3 drifters at once (`DrifterTuning`) |
| Sink | `DrifterDepartures` | what a drifter still carries when it leaves | its purse plus what it earned |
| Transfer | purchases, sales, bounty escrow | between the stall and crawlers | conserved |
| Transfer | guild fees and wages (Epic 10 G4) | 20g a recruit; each member's wage a visit (5g by default) | conserved; `Fees` and `Wages` in the ledger's flows; a wage the treasury can't cover isn't paid |
| Faucet | `CaravanSalvageSales` (the Gallery's glowstone, Epic 10 G5) | an expedition's haul, up to 4 glowstone, sold 2 a visit at 70g | one seam; only a party works it |
| Transfer | expedition shares (Epic 10 G5) | 30% of the haul's caravan value, split among the party | conserved; `Shares` in the ledger's flows |
| Sink | `Construction` (Epic 10 Part 2) | 300g per station, the forge and the still (`WorkshopTuning`) | once per station; refused when the treasury can't pay |
| Transfer | artisans' wages (Epic 10 Part 2) | each artisan's wage a visit (5g by default), after the guild's members | conserved; `ArtisanWages` in the ledger's flows; no signing fee |

**What follows:** the stall's net a day ≈ the caravan's payment − upkeep − wholesale − what crawlers keep.
- Crawlers spend what they earn, so their gold passes through; purses stay near 40–55g, under the 80g they earn toward.
- The stall earns about 1g on each unit of salvage it passes on, and about 13g on each ration it sells. The treasury grows about 250g a day at fair prices.

## 3. The scarcity lever: crawlers' willingness to trade (D4)
Every crawler values things with its personality and needs:
- **A ration:** calm value = 20g × greed factor (neutral 20g, greedy 10g, generous 30g). Hunger raises what it will pay, up to 1.5× when starving.
- **Salvage:** minimum sale price = base value × greed factor (neutral ore 2g, herb 3g). Every seller, greedy to generous, sells at the stall's default price.
- **Hazard gear:** calm value = 40g × greed factor (neutral 40g, greedy 20g, generous 60g). No need raises it, so it is also the most a crawler pays.
    - **Who buys:** only a crawler bold and healthy enough to go into danger, wearing no gear, that can still pay for a ration after.
    - **Why 30g:** the default is 0.75 of the neutral value, as the ration's 15g is of 20g.
    - **Gear's scarcity:** the caravan's three a visit and the crawlers' purses.

**Earning (D30).** Below its 80g purse a crawler works for gold; how urgently is the share of the purse it lacks (20 when empty), not the gold, so a bigger purse doesn't make every bounty look small beside earning.

A crawler refuses a price outside its valuation. Over-pricing doesn't only cost trust: it cuts trade volume, and volume is what the caravan pays for.

## 4. Fairness and trust (D3)
Only completed trades change trust; a refused trade changes nothing.

| Trade | Generous (+10 trust) | Fair (+3) | Unfair |
| :--- | :--- | :--- | :--- |
| Crawler buys a ration | price ≤ 0.6 × calm value | ≤ 1 × calm value | **Gouged** (−15): above its calm value, so hunger made it pay |
| Crawler buys hazard gear | price ≤ 0.6 × calm value | ≤ 1 × calm value | none: it never pays above its calm value |
| Crawler sells salvage | price ≥ 3 × its minimum | ≥ 1.5 × minimum | **Stingy** (−5): at its minimum or just above |
| Crawler delivers a bounty (Epic 10 G4) | reward ≥ 3 × its minimum for the work (+15 trust, +10 warmth, +10 respect) | ≥ 1.5 × (+8, +5, +5) | **Stingy** (−5 trust, −3 warmth) |

**A bounty's minimum for the work:** a Gather's items at the crawler's minimum sale prices, and a Scout's report at 5g × its greed factor. The seeded bounties (25g for an ore, 15g for a scout) are generous to every crawler on the day.

**A guild member's trust:** each paid visit is +2 trust and +1 warmth; an unpaid one is −10 and −5. A member quits below 30 trust, or after two unpaid visits in a row.

The default prices (15 / 5 / 6) are fair to a neutral crawler. A remembered affinity fades 5 points toward neutral every 12 game hours since the last dealing (D27), so a player keeps trust only by keeping trade fair.

## 5. The stall's limits
- **Salvage capacity:** 20 of each type. A full stall stops buying until the caravan takes its lot, and crawlers stop harvesting salvage for it. On the day ore fills it before about half the visits.
- **Arrears:** when upkeep can't be paid in full, the stall pays what it has (no debt) and buys no salvage until a visit pays in full. It keeps selling rations, which is how it recovers. The merchant view shows the arrears and the chronicle records it.
- **Bankruptcy is felt as hunger.** With no gold the caravan delivers no rations, and the outpost falls back on the Garden.

## 6. Food (D6, D19)
- **Hunger** fills from fed to starving over a game day; a ration from the pack takes off 40, eaten at a Garden plot 50. A crawler eats two or three times a day (2.3 measured).
- **The Garden:** 4 plots × 4 rations, regrowing 300 ticks after they empty (`Floor1Layout.FoodPlotYield`, `FoodRegrowTicks`). It's free, but on the day crawlers buy nine meals in ten at the stall; the Garden is the quick fallback (D26, measured).
- **A crawler arrives with 15g,** one ration at the default price.
- **A meal is three units of ore** (15g against 5g): about an hour and a half of harvesting with the walks. Before the day one ore sale bought a meal.
- **Behaviour:** a starving crawler that knows no reachable food goes back to the Garden plots it has seen and waits for them to regrow. A sleeper wakes when hunger reaches 85.

## 7. Pace targets (D5, D28), asserted by the soak
`simpiens soak` and `dotnet test` (`EconomySoakTests`) play three game days (30,240 ticks) for each of 3 seeds, sampled at each caravan visit; each profile's prices are set by a command at tick 0 as shares of the fair prices, and every run is checked against its profile's band (`EconomySoak`). Starving is hunger 95 or more for 2 game hours.

| Profile | Prices (ration / ore / herb; gear) | Band (per run, at the end of day 3) | Measured 2026-11-05 (T4.4, seeds 1–15 and 20261009) |
| :--- | :--- | :--- | :--- |
| Fair | 15 / 5 / 6; gear 30 | treasury 600–1,600g; trust up for ≥ 8 of 12; 0 starving episodes | 771–1,277g; trust up 10–12; 0 starving |
| Greedy | 30 / 2 / 3; gear 60 (food and gear ×2, salvage ×½) | trust up for ≤ 3 of 12; trust mean below 0 at its lowest (treasury not asserted) | trust up 0–3; mean −29 to −71 at the end; treasury 512–729g |
| Generous | 5 / 9 / 10; gear 20 | the stall goes broke (treasury 0 or in arrears); trust up for ≥ 8 of 12 at its peak | broke by day 1–2 on every seed; trust up 12 |
| Guild (Epic 10 G6, D14) | fair, plus the scripted guild player: four recruited as they qualify, outfitted from stock and sent to the Drowned Gallery whenever its seam is back | treasury 400–1,200g; trust up for ≥ 8 of 12; 0 starving; no wage unpaid; a guild full 8 game hours before the end brings an expedition home | 578–998g; full on day 1.6–2.9; every wage paid; 1–2 hauls home whenever full in time |

- **The bands are proposed** from T4.4's measurement and stand by the producer's rule of answering by exception. Before the day: fair and guild 1,000–2,000g at 30 minutes (the producer, 2026-10-25), greedy trust up for 2 at most.
- **Lower treasuries than Part 1's:** a day pays 320g of upkeep, and salvage earns the stall about 1g a unit instead of 4–5g. The guild's wages, fees and outfitting take about 260g more over three days than the fair player's.
- **Drifters (F11):** the keen profile (rations 20, ore 4, herbs 5, gear 35: slightly greedy) ends richer with drifters than in a closed outpost on every soak seed. Measured over 16 seeds: 17,254g against 11,985g, ahead on all 16, though drifters take away more than they bring (about 460g against 330g over three days). `DriftersLeaveAKeenPlayersStallRicher` asserts it; on Part 1's day the comparison was within the noise (Epic 10 F4).
- **Why greed is judged at its lowest trust:** greed costs trust early, through stingy sales. Once the stall is empty nobody trades, and trust drifts back to neutral. The generous band is judged at its peak for the same reason.
- **Why generous starvation isn't asserted:** a bankrupt outpost can't pay for food. Partial restocks soften that, and the soak measured 0 anyway.
- **The regular suite** asserts the fair case over one game day: the outpost's day (`OutpostScenarioTests`, on the 23 gate seeds) checks that trust rises for ⅔ of crawlers and nobody starves. Seeds 1–120 all pass (`simpiens sweep`, 2026-11-05).

## 8. Deferred
- ~~**Drifters**, **gear**, **wages and recruitment**, **expeditions**:~~ delivered by Epic 10 G1–G5 (§1, §2).
- **Made goods** (the pickaxe, draughts, gear for each hazard): Epic 10 Part 2, W1–W4. They are what the 80g purse is for.
- ~~**Lodging fees:**~~ delivered by Epic 10 Part 2, W5 (2026-11-06, D63): a guild member with a bunk in the barracks is paid its wage less 1g each caravan visit (`GuildTuning.LodgingFee`), never below nothing. It is a deduction, not a transfer: the fee stays in the treasury and the ledger records the wage paid.
- **Other crawler sinks:** not needed while crawler wallets stay flat.
- **Rival shops and guilds** (the producer's long-term direction, 2026-11-04): other shops and guilds competing for the crawlers' trade, trust and service, as a sink and a pressure on prices and wages. Unscheduled; the vision is in the [Game Design Proposal §5](Game%20Design%20Proposal.md).

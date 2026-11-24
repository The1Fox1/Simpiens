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
| Sink | `Construction` (Epic 10 Part 2) | 300g per station, the forge and the still; 200g for the guildhall's drill yard (Epic 11, S3.4, `WorkshopTuning.CostOf`) | once per station; refused when the treasury can't pay |
| Sink | `ManualWholesale` (Epic 11, S3.2) | 2g per primer, one of each of seven kept on the shelf, after the gear | as much as the stall can pay for; the first visit pays 14g for the set, later visits only for what sold. 8g at first: the first visit's 56g, when the stall is at its poorest, left the crafting profile's stations unbuilt on 6 of 20 seeds (S3.6) |
| Transfer | primers (Epic 11, S3.3) | sold at 25g to a resident who loves the primer's skill, from a spare purse (the price, a ration's and 15g besides) | conserved, as a trade; the stall keeps 23g a primer over its wholesale |
| Transfer | artisans' wages (Epic 10 Part 2) | each artisan's wage a visit (5g by default), after the guild's members | conserved; `ArtisanWages` in the ledger's flows; no signing fee |
| Faucet | `CaravanSalvageSales` (creatures' materials, Epic 10 Part 3, K4.4) | hides and chitin past six of each kept for the forge, 6g and 10g (the stall pays 5g and 8g) | 8 of each a visit |
| Faucet | `CaravanSalvageSales` (bosses' trophies, Epic 10 Part 3, K10.1a) | the Matriarch's pelt at 44g (the stall pays 36g), the Keeper's stone at 170g (140g) | a pelt a game week at most (her return); the stone once a session (the Gatekeeper never returns) |
| Transfer | hunt bounties (Epic 10 Part 3, K6) | escrow posted by the player, paid out by damage dealt among up to three hunters | conserved; released in shares |
| Transfer | raises (Epic 11, S2.2, D8) | a member's or artisan's asking wage is its reservation wage ×1.25 at Journeyman, ×1.5 at Master (`GuildTuning`'s raise rows, read by the artisans too); a raise paid is part of its wage | conserved, inside `Wages` and `ArtisanWages`; a cost, not a new sink. Paid below its asking wage, a hand takes the unpaid wage's trust and warmth, no unpaid visit counted |
| Transfer | sponsorship (Epic 11, S4.6, D16) | the patron pays one of its people (a member or an artisan) to take up a band's other technique: 20g at Apprentice, 40g at Journeyman, 80g at Master (`GuildTuning`'s sponsor rows); refused under trust 50 or against the crawler's nature | conserved: from the stall's treasury into the crawler's purse, counted inside `Wages`; no new sink, and no scripted player sponsors |

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
| Crafting (Epic 10 Part 2, W6.3, D67–D68; measured 2026-11-06, same seeds) | fair, plus the forge making respirators, the still draughts, and the guildhall's guild of eight (two parties sent together), keeping a 200g reserve | proposed by exception: treasury 0–1,200g; trust up for ≥ 8 of 12; 0 starving; no member unpaid; a station standing and something made | 1–390g; trust up 10–12; 0 starving; stations 1–2 standing, 0–8 made; a member unpaid once on 2 seeds; nothing made on 2 (no glowstone or herbs to work); eight members by day 3 only on the default seed. 12 of 16 within the band, the soak's 3 seeds all |
| Hunting (Epic 10 Part 3, K11.2, D73; measured 2026-11-15, seeds 1–20) | fair, plus the forge making blades and ring coats, the still draughts, the scripted hunt player, and a guild of four clearing the first charted lair with two draughts each, every player keeping a 150g reserve | proposed by exception: treasury 0–1,200g; trust up for ≥ 8 of 12; 0 starving; no quit for pay; at most one death (kills counted, not banded: the fighting starts at the horizon's edge) | 66–397g; trust up 11–12; 0 starving; no quit; no death; 0–2 kills by day 3. Over eight days: 0–8 kills a seed, 0–3 hunts paid, no rescue, no death, a quit for pay after day 3 on 4 of the 20 seeds (finding F14 of Part 3) |

- **The bands are proposed** from T4.4's measurement and stand by the producer's rule of answering by exception. Before the day: fair and guild 1,000–2,000g at 30 minutes (the producer, 2026-10-25), greedy trust up for 2 at most.
- **Lower treasuries than Part 1's:** a day pays 320g of upkeep, and salvage earns the stall about 1g a unit instead of 4–5g. The guild's wages, fees and outfitting take about 260g more over three days than the fair player's.
- **Drifters (F11):** the keen profile (rations 20, ore 4, herbs 5, gear 35: slightly greedy) ends richer with drifters than in a closed outpost on every soak seed. Measured over 16 seeds: 17,254g against 11,985g, ahead on all 16, though drifters take away more than they bring (about 460g against 330g over three days). `DriftersLeaveAKeenPlayersStallRicher` asserts it; on Part 1's day the comparison was within the noise (Epic 10 F4).
- **Why greed is judged at its lowest trust:** greed costs trust early, through stingy sales. Once the stall is empty nobody trades, and trust drifts back to neutral. The generous band is judged at its peak for the same reason.
- **Why generous starvation isn't asserted:** a bankrupt outpost can't pay for food. Partial restocks soften that, and the soak measured 0 anyway.
- **The cost of mastery (Epic 11, S2.4, measured 2026-11-17):** the scripted players pay each raise when it is asked (S2.3). The `guild`, `crafting` and `hunting` profiles hold their bands with raises paid on 20 of 20 seeds each (seeds 1–20). No member or artisan reaches Journeyman by day 3, so no raise falls due and upkeep costs nothing at the soak's horizon. Over 14 days (seeds 1–10), one hand under contract stands past Apprentice at the end (a member on `crafting` seed 2, 1g of raise a visit). The bands stay as they are, by exception.
- **The teachers (Epic 11, S3.6):** the primers' shelf is a new early cost. At 8g wholesale it left the crafting profile's stations unbuilt on 6 of seeds 1–20; at 2g the share holds at its edge (goods made on 16 of 20, the band 16; a quit for pay on 2, the band 5). Epic 11's F7.
- **Plan 1's final build (Epic 11, C.1, 2026-11-17):** the soak agrees with S3.6. Every profile holds its band: the regular soak 18 of 18, and the `guild`, `crafting` and `hunting` profiles on seeds 1–20 20 of 20 each. Crafting makes goods on 16 of 20 and loses a member to pay on 2. No member or artisan stands past Apprentice by day 3, so no raise is owed. The bands stay as they are.
- **Plan 2's final build (Epic 11, C.1, 2026-11-19):** with techniques, Leadership and the callings' roles, every profile holds its band: the regular soak 18 of 18, and the `guild`, `crafting` and `hunting` profiles 20 of 20 each on seeds 1–20. Sponsorship is a transfer no scripted player makes. The crafting band's live test judges seeds 1–40 since S4.7 (F9: a quit for pay on 12 of seeds 1–60 against 5 before plan 2, the treasury at day 3 unchanged on average). The bands stay as they are.
- **Epic 11's final build (S9.2, 2026-11-22):** with passions, upkeep, teachers, techniques, callings, legend and masterpieces, every profile holds its band: the regular soak 18 of 18, and the `guild`, `crafting` and `hunting` profiles 20 of 20 each on seeds 1–20. At day 3: the guild's treasury 594–1,090g (mean 798g), the crafting player's 0–335g (mean 144g; goods made on 20 of 20, a quit for pay on 3), the hunting players' 45–615g (mean 283g); trust up for 10–12 of 12 on every run; no specialist under contract by day 3, so upkeep costs nothing at the soak's horizon. Masterpieces add no gold: a masterpiece is sold at its good's price. The bands stay as they are, by the producer's standing word (D47).
- **The regular suite** asserts the fair case over one game day: the outpost's day (`OutpostScenarioTests`, on the 23 gate seeds) checks that trust rises for ⅔ of crawlers and nobody starves. Seeds 1–120 all pass (`simpiens sweep`, 2026-11-05).

## 8. Deferred
- ~~**Drifters**, **gear**, **wages and recruitment**, **expeditions**:~~ delivered by Epic 10 G1–G5 (§1, §2).
- ~~**Made goods** (the pickaxe, draughts, gear for each hazard):~~ delivered by Epic 10 Part 2, W1–W4. They are what the 80g purse is for.
- **The whole loop's balance** (Epic 10 Part 2's F14, joined by Part 3's F14, F8 and F10). This is open and the producer's decision, and unscheduled: the next epic that re-tunes the economy re-sets the `crafting` and `hunting` bands (§7) with it.
  - **The producer's word (2026-11-16):** "Economy flukes are tolerable until they completely ruin our testing scenarios." So the economy is left as it is until a scenario breaks on it. A test's band that drifts with the economy is judged across seeds, as F3's option A did, rather than tuned around.
  - **What happens:**
    - With both stations and a guild of eight, the stall ends day 3 at 1–390g, where the fair player ends at 771–1,277g.
    - On the default seed it affords the forge and a guild of eight, but never the still as well.
    - With every player at once (the `hunting` profile), the guild or an artisan quits for want of pay after day 3 on 4 of seeds 1–20.
  - **Where the money goes** (Part 2's W6 measurement, 6 game days, 5 seeds):
    - upkeep, 320g a day;
    - eight members' wages, about 130g a day after lodging;
    - two artisans' wages, about 40g a day;
    - the stations, 600g once.
    - The guild's glowstone feeds the forge's respirators instead of the caravan's exports.
  - **Upkeep beside it (Epic 11, S2.4):** the raises add nothing measurable yet. Over 14 days on seeds 1–10, the `crafting` profile's guild falls apart for want of pay on 8 seeds (0–5 members left, up to 10 quits) and the `hunting` profile's on 7 (up to 18 quits). Yet a hand under contract stands past Apprentice at the end on one seed only, for 1g a visit. The strain is the open loop's, not mastery's. The `guild` profile alone holds four members, pays every wage and quits for nothing on all 10. Measured by the soak's `Specialists`, `Upkeep` and `RaisesOwed`; the scripted players keep paying raises as they come.
  - **What holds it today:** the scripted players keep a reserve (150–200g, D67 and D71), as a careful player would. That is input, not a rule, and it holds every DoD's run.
  - **The levers:**
    - the stations' cost (300g each);
    - upkeep;
    - members' wages (5g a visit);
    - glowstone's export price;
    - the made goods' prices.
- ~~**Lodging fees:**~~ delivered by Epic 10 Part 2, W5 (2026-11-06, D63): a guild member with a bunk in the barracks is paid its wage less 1g each caravan visit (`GuildTuning.LodgingFee`), never below nothing. It is a deduction, not a transfer: the fee stays in the treasury and the ledger records the wage paid.
- **Other crawler sinks:** not needed while crawler wallets stay flat.
- **Rival shops and guilds** (the producer's long-term direction, 2026-11-04): other shops and guilds competing for the crawlers' trade, trust and service, as a sink and a pressure on prices and wages. Unscheduled; the vision is in the [Game Design Proposal §5](Game%20Design%20Proposal.md).

## The tavern (Epic LIFE-1 plan 2, T.3–T.6, 2026-11-24)
- **Its flows:** building it, 200g into `Construction`; its keeper's wage, as an artisan's; the caravan's ale (12 at 2g) and stew (8 at 4g) into `TavernWholesale`, only while it stands; its takings, an ale at 4g and a stew supper at 10g, from the crawler's purse to the treasury through the ledger's stall purchase, as any sale. Dice are free.
- **The `tavern` soak profile** (the fair player's prices and `TavernPlayer`, keeping a 150g reserve): on seeds 1–20 the tavern stands by day 1, is first kept around t12,000 (when a resident trusts the patron enough to keep it), opens 2–15 hours over the three days on the day shift, and serves 0–10 a seed (about 2 a seed, 13g). Treasuries end at 440–1,311g, every stall solvent, nobody starving. Its band asks for the tavern built and kept, the stall solvent, trust risen for 8 and no starving; its takings are read, not banded (finding F18: work outbids free time, and dice are what free time picks most).

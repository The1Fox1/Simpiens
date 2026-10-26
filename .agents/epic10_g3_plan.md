# Epic 10 G3: gear (working plan)

Working document (AGENTS.md, Documentation Protocol 6): deleted when G3 closes.
- **Epic:** `docs/epics/epic-10.md` §2.2 (G3: the caravan supplies gear, a gear price, gear reduces hazard damage and wears out; a gear row in the merchant terminal) and §3 (G3's DoD).
- **Earlier designs it follows:**
  - Epic 9 defined the item types: `ToolPickaxe` "enhances harvest yield", `BasicWeapon` "equipped for hazard defense".
  - Epic 9 D2 deferred gear wear to Epic 10 as the crawlers' own gold sink.
  - The economy model §8 lists "gear, wear and gear-gated scarcity" for G3.
- **Branch:** `feat/epic10-g3` from `main`; one PR into `main` (Q10).
- **Producer decisions, 2026-10-25, before G3:** the hazards UI is fine for now (G2's look passed), and the fair soak band is 1,000–2,000g.

## Goal
The stall sells gear, and a geared crawler loses health more slowly in a hazard sector.

Gear wears out, so crawlers buy it again. That makes it:
- the crawlers' own gold sink (Epic 9 D2);
- the counterweight to glowstone's new money until wages arrive (G4).

## Decisions to confirm (defaults; the producer answers only where they disagree)
- **D1. What gear is in G3: one protective item.**
  - **Why not the pickaxe.** A harvest takes 1.5 s a unit (`HarvestResourceIntent.GatherDuration`), against 15–30 s of exposure per hazard trip. A faster pickaxe would cut a trip's damage by 5% at most. A yield bonus is an economy change, better tuned with the workshops.
  - **Default:** the pickaxe stays without a source and moves to Part 2 (harvest tools, Milestone 3), recorded under Deferred.
  - **Alternative:** both items, as the epic's G3 row says: the pickaxe adds +1 ore per harvest and gets a second terminal row. About half a day more, and the soak has to be re-measured.
- **D2. The item is named for what it does.**
  - **The problem:** Epic 9's `BasicWeapon` is the hazard-defence item, but Part 1 has no combat, so "weapon" misleads the player and the code.
  - **Default renames,** keeping the value 5:
    - `ItemType.BasicWeapon` becomes `HazardGear` (placeholder name "hazard gear", like glowstone's);
    - `StateFact.HasWeapon` becomes `HasHazardGear`;
    - `EconomyTuning.WeaponBaseValue` becomes `HazardGearBaseValue`.
  - **The weapon itself** comes with combat, in the descent.
  - **Alternative:** keep "Basic Weapon".
- **D3. Gear is worn, not carried.**
  - **Default:** one gear slot beside the 4-slot pack, holding the item with its durability. Gear then never crowds salvage out of the pack; a full pack refusing a harvest is the kind of retry loop G0 removed.
  - **Alternative:** carry gear in the pack. That needs no new save field but costs a slot.

## Design (defaults; numbers are tuning, in `EconomyTuning`, never literals)
- **Protection and wear (`GearTuning`, beside `SupplyTuning` and `DrifterTuning`):**
  - While gear is worn, hazard damage is cut by half (`DamageReduction` 0.5), applied where G2 left the term (`AgentRuntime`).
  - Gear starts at durability 100 and loses 2 a second in a hazard sector, about 50 s of exposure, or two or three glowstone trips.
  - At 0 it breaks and is gone (chronicle: Notable).
- **Who buys:**
  - **The rule:** a crawler that would venture (G2's rule, `Vitality.WouldVenture`, unchanged), knows a hazard salvage node and wears no gear. It must be able to pay and still keep a ration's price, and the price must be within its own valuation: calm gear value = base value × greed factor, as for a ration.
  - **The planner:** a new goal, `OutfitForDangerGoal` (`HasHazardGear`), and a new action, `BuyHazardGearAction` (`AtTradeStall` → `HasHazardGear`), through the existing `TradeIntent` with `BuyItem`. The goal ranks above gathering salvage while it applies.
  - **The fact budget:** no new facts; 39 of 64 stay.
- **The trade:**
  - `TradeIntentHandler`'s buy path is generalized from rations to sold goods (a ration, hazard gear), with stock and price per item. Bought gear goes into the gear slot; a buy is refused while the slot is taken.
  - Trust works as for rations: generous at ≤ 0.6 × calm value (+10), fair at ≤ 1 × (+3). Gouging can't happen, because no need pushes a crawler above its valuation for gear.
- **The stall and the caravan:**
  - `TradeStallData` gains `HazardGearSellPrice` (default 40g, the base value, fair to a neutral crawler) and `HazardGearStock`.
  - On each visit, after rations (food first), the caravan tops gear up to a cap of 3 at 20g wholesale, paid from the treasury under a new named sink, `GearWholesale`. Restocks can be partial, as for rations.
  - Gear's scarcity is the caravan's few per visit and the crawlers' purses. That is the "gear-gated scarcity" of the economy model §8, written into §2 and §3 there.
- **The player:**
  - `UpdateStallPricesCommand` gains the gear price (0 keeps the current price).
  - The soak profiles get gear prices: fair 40, greedy 60, generous 25.
- **Save format 4:**
  - the worn gear, in the inventory block;
  - the stall's gear price and stock, in the economy block (`EconomySerializationValidator` sizes);
  - the new sink, by name, in the ledger.
  - The round-trip validators cover all three.
- **Client:**
  - **Merchant terminal:** a gear row (a sell-price slider), with gear added to the stock line.
  - **Dossier:** the worn gear and its wear.
  - **Tests and pins:** updated in the pinned image.

**To measure early (step 4):** whether a bold crawler can afford the gear. A glowstone sale pays 45g, and the gear plus a ration's reserve costs 55g at the default prices. If nobody buys within a day on most gate seeds, tune the price and the base value, and record the numbers.

## Steps
- [x] 1. Docs:
      - the producer's decisions (G2's look passed; the fair band 1,000–2,000g, `EconomySoak.FairTreasuryMax` 2,000; measured fair 1,545–1,594g);
      - this plan.
- [ ] 2. D2's renames, pure (golden unchanged), in a commit of their own.
- [ ] 3. The gear slot (D3):
      - inventory and snapshot;
      - the world state's `HasHazardGear`;
      - save format 4;
      - inventory and round-trip validators.
- [ ] 4. Gear in the economy:
      - the tuning;
      - the stall's price and stock;
      - the caravan's restock and its sink;
      - the buy path and its trust verdict;
      - the price command.
      - Validators. Measure what the crawlers can afford, then re-measure the soak: the bands hold, fair within 1,000–2,000g.
- [ ] 5. Protection and wear:
      - the damage cut in `AgentRuntime`;
      - wear and breaking;
      - the chronicle;
      - validators.
- [ ] 6. Cognition: the outfit goal and action. The 23-seed gate and G2's danger scenario still hold.
- [ ] 7. The live scenario (G3's DoD) headless; the client (merchant row, dossier); tests.
- [ ] 8. Finish:
      - golden hashes re-recorded, in a commit of their own;
      - references and pins in the pinned image, in a commit of their own;
      - the economy model, the epic and the roadmap;
      - the PR into `main`.

## Definition of Done (epic §3, G3)
Headless, on the real outpost, with nothing injected:
- a crawler buys hazard gear at the stall on its own;
- in the same hazard sector, it loses health more slowly per second than a crawler without gear;
- its gear wears;
- the caravan restocks the stall's gear under its own sink, and the ledger balances;
- the residents' day still holds on the 23 gate seeds, and G2's danger scenario still passes;
- the soak keeps its bands, with fair within 1,000–2,000g.

## Deferred (if D1's default stands)
| Left out | Owner |
| :--- | :--- |
| The pickaxe (harvest tools, a yield bonus) | Epic 10 Part 2, the workshops (Milestone 3) |
| A weapon | Combat, with the descent (Part 3, Milestone 4) |
| Gear per hazard kind (a respirator for foul air, a helmet for falling rock), better grades, repair | Part 2, crafting |
| Gear making more crawlers willing to venture | G5, where a party's outfit matters |

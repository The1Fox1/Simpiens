# Item system refactor (working plan)

Working document (AGENTS.md, Documentation Protocol 6): deleted when the work closes.
- **Status:** in progress. The producer said "proceed with refactor plan" on 2026-10-26, after merging Epic 10 G3 (PR #14), so it runs now, before G4, and before refactor R1–R4 (§6's order, changed). **I1 done** (`feat/items-i1-catalog`). It replaces refactor R7 in `.agents/core_refactor_plan.md`, and widens it with grouping, rarity, consumable effects, equipment stats and loot tables.
- **Timing:** after Epic 10 Part 1, before Part 2 (crafting multiplies the items), with the refactor's R1–R4 first (§6).
- **The ask (producer, 2026-10-26):**
  - adding and removing items is easy;
  - items are grouped (resources, consumables, …);
  - every item has a rarity score, as the foundation for random drops and loot tables;
  - weapons give stats, and consumables restore stats.

  "We do not need all these systems right now but need a solid foundation."

---

## 1. Goals and non-goals
**Goals**
- **One place per item.** An item is one definition in a catalog. Adding one means a definition, a sprite and, if it's found in the world, the map nodes that yield it. Removing one means deleting its definition.
- **Code asks about groups, not items.** The economy, cognition and the client ask "every Salvage item" or "what restores Health", never "ore, herbs and glowstone". A test enforces it.
- **A foundation for:**
  - rarity, with a score per item;
  - deterministic loot tables, built and tested but not yet wired into play;
  - equipment that changes a crawler's stats (weapons, protection, tools);
  - consumables whose effects are data.
- **No behaviour change.** Every phase reproduces the golden hashes, the pinned renders and the soak's numbers exactly. This is a refactor; game changes come after it, on purpose.

**Non-goals (later, on this foundation)**
- combat, and the weapon stats it uses (the descent, Part 3);
- loot drops in play, conditions on loot tables, and crafting recipes (Part 2);
- item data files (JSON) and content tools (the map designer, Epic CLIENT);
- quality grades (Part 2).

## 2. Today (the review of 2026-10-26)
- **Generic already:**
  - the item record (`ItemInstance`, 16 bytes: type, quantity, base value, durability, quality, id);
  - the pack (`AgentInventory`, stacking by type);
  - resource nodes (map data with an item, a yield and a regrowth time).
- **Not generic.** Each item is named in about 20 files:
  - **The stall:** `TradeStallData` has two named fields per item, threaded through 8 copy methods, and it is raw-saved, so every item bumps the save format.
  - **The caravan, the player's prices and the opening terms:** a field per item in `SupplyTuning`, `UpdateStallPricesCommand`, `StallTerms` and `EconomyTuning`.
  - **Groups:** hand-written checks (`IsFoodYield`, `IsSalvage`, `IsWearable`), plus lists of ore, herbs and glowstone in cognition (`SellSalvageAction`, `LiquidateSalvageGoal`, `AffordableHeldSalvage`, `WorldStateBuilder` ×2).
  - **The client:** a sprite `switch`, plus merchant rows and a stock line written by hand.
- **Item effects are code, not data:**
  - A carried ration takes 40 off hunger (`AgentConsequenceService`); food eaten at a Garden plot takes 50.
  - A herb heals 30 (`Vitality.HerbHeal`).
  - Hazard gear halves hazard damage (`GearTuning`).
- **Unused:** `ItemInstance.Quality` is never read, and `ItemId` is stamped 1 everywhere.

## 3. Design

### 3.1 Identity: `ItemType` stays the id
- **The id:** `enum ItemType : byte` keeps identifying an item in saves, the ledger, the world hash and memory. One enum line per item keeps code and saves readable.
- **Ids are never reused.** A removed item's value stays reserved with a comment (`// 7: retired, was X`).
- **A load refuses** a save holding an item the session's catalog doesn't define, and says which.
- **Room:** 255 ids. If that's ever short, `ItemInstance`'s 2 bytes of padding let `ItemType` widen to `ushort` (a save-format change, recorded then).

### 3.2 The catalog
**Changed in I1 (2026-10-26): what an item *is* and how a session *trades* it are split.**
- **Why:** the group checks are called where no per-session catalog can reach without new plumbing (`AgentState`, `AgentInventory`, the consequence service). Besides, what an item is (its groups, rarity, names, stack size, and later its use and equipment effects) doesn't vary by session.
- **The split:**
  - `ItemCatalog` is **static and immutable**, readable anywhere on any thread, like `Floor1Layout.Definition`.
  - How a session trades an item stays in **`EconomyTuning`**, which already reaches every caller: today's base-value `switch` and per-item fields, and from I5 a per-item terms table holding the base value and the stall's and caravan's terms.
  - Sessions still differ in which items they trade and place, through their economy and map.
  - §3.3's `BaseValue`, `Stall` and `Caravan` rows therefore live in the economy's table from I5, not in the definition.

The original design follows, as planned:
- **Where the types live:** `ItemCatalog` (in `Simulation/Items/`) is an immutable table of `ItemDefinition`s indexed by `ItemType`.
- **Per session:** `ISessionDefinition.Items`, beside `Economy`. Sessions can differ: the Dev Map can leave items out.
- **Published on the world snapshot,** so off-thread cognition reads it without locks, as it reads `EconomyTuning`.
- **Content is code,** like the layouts: `Session/Items/OutpostItems.cs` defines the outpost's items.
- **Validated** (`ItemCatalogValidator`) when a session is built:
  - ids are unique;
  - categories fit the item's blocks: Equipment needs an equipment block, Consumable an effect;
  - a stall that both buys and sells an item is refused;
  - loot tables refer to items that exist.

### 3.3 `ItemDefinition`
| Field | What it is | Example |
| :--- | :--- | :--- |
| `Id`, `Name`, `DisplayName` | The id; a code name for logs; the player's name | `Glowstone`, "glowstone", "Glowstone" |
| `Categories` | Groups, as flags (§3.4) | `Resource \| Mineral \| Hazardous` |
| `Rarity` | A tier and its score (§3.5) | `Rare` |
| `BaseValue` | A neutral crawler's valuation (today's `EconomyTuning.BaseValue`) | 30 |
| `MaxStack` | Units per pack slot (today 99 for all) | 99 |
| `Consume` | What using it does (§3.6), or none | herb: Health +30 |
| `Equip` | Slot, stat modifiers, durability and wear rule (§3.7), or none | hazard gear: Protection, HazardResistance 0.5, 500 ticks, per hazard tick |
| `Stall` | The stall buys it (price, capacity) or sells it (price, opening stock), or neither | glowstone: buys at 45, holds 20 |
| `Caravan` | The caravan exports it (quota, market price) or supplies it (cap, wholesale cost), or neither | glowstone: exports 2 at 70 |
| `SpriteKey` | What the client draws | `"Resource_Glowstone"` |

### 3.4 Grouping: categories as flags
- **The type:** `[Flags] enum ItemCategory : uint`. An item can be in several groups; a herb is a resource, a medicine and a consumable.
- **Why flags, not text tags:** a flag test costs nothing on the tick path, allocates nothing and is deterministic.
- **The groups:**

| Group | Meaning |
| :--- | :--- |
| `Resource` | A raw material, harvested from a node |
| `Consumable` | Used up for an effect (§3.6) |
| `Food` | A consumable that eases hunger |
| `Medicine` | A consumable that heals |
| `Equipment` | Worn or wielded (§3.7) |
| `Weapon`, `Protection`, `Tool` | The equipment kinds; each maps to an equipment slot |
| `Mineral`, `Botanical` | Origin, for future rules (a pickaxe for minerals) |
| `Hazardous` | Found only in hazard sectors |

- **The rule:** "salvage", what crawlers gather to sell, is not a category. It's a fact the catalog computes: a `Resource` the stall buys. So the caravan and cognition ask the catalog, and an item becomes salvage just by having a stall-buy block.
- **Queries:** `catalog.InCategory(ItemCategory.Food)` returns a precomputed list in id order, built once when the catalog is built, so it never allocates.

### 3.5 Rarity
- **The type:** `ItemRarity` tiers, each with a default score: the item's weight in a loot table.

| Tier | Score |
| :--- | :--- |
| `Common` | 1000 |
| `Uncommon` | 250 |
| `Rare` | 50 |
| `Epic` | 10 |
| `Legendary` | 2 |

  A lower score is rarer. A loot entry may override the score.
- **What reads it now:** loot tables (§3.8) and, later, the client's colouring. Nothing in play reads it yet.

### 3.6 Consumables and stats
- **The stats:** `StatKind` names a crawler's stats.
  - **Today's:** Health, Hunger, Energy, Social, Frustration.
  - **Reserved for equipment:** HazardResistance, HarvestSpeed, Attack, Defense, MoveSpeed. Only HazardResistance is used.
- **Effects:**
  - A consumable has a short fixed list of `ConsumeEffect { StatKind Stat; float Amount }`: ration: Hunger −40; herb: Health +30.
  - One main-thread path (`ItemUse.Apply`) applies them, clamped as today. It replaces the ration's −40 in `AgentConsequenceService` and the herb's +30 in `TreatWoundsIntentHandler`.
- **Kept as it is, so nothing changes:**
  - Food eaten at a Garden plot takes 50 off hunger. That's the node's rule (eaten on the spot), not the ration's, so the plot keeps it.
  - The `HasFoodInInventory` flag stays.
  - Both are recorded in §5 for a deliberate cleanup later.

### 3.7 Equipment and stats
- **Slots:** `EquipmentSlot` has Protection, Weapon and Tool. `AgentInventory`'s single gear slot becomes a fixed array of three, saved (format change).
- **Stats:** an equipment block lists `StatModifier { StatKind Stat; ModifierOp Op; float Value }` (Add or Multiply) and a durability with a wear rule:
  - **`PerHazardTick`:** today's hazard gear;
  - **`PerUse`:** a tool per harvest, a weapon per hit. Reserved.
  - **`None`.**
- **Reading them:**
  - `CrawlerStats` computes a crawler's stats from its base values and its equipment, in slot order (deterministic): adds first, then multiplies.
  - Code asks `CrawlerStats` ("HazardResistance") instead of a tuning: `AgentRuntime`'s hazard damage reads it in place of `GearTuning`, which goes away.
  - **Exact floats:** the formula stays `damage × (1 − resistance)` with today's casts, so the golden hashes don't move.
- **Weapons:** a weapon is a definition with the Weapon category, the Weapon slot and an Attack modifier. Nothing reads Attack until combat. The foundation is the slot, the modifier and the stat.

### 3.8 Loot tables (foundation, not wired into play)
- **The types:**
  - `LootTable { Id; byte MinRolls, MaxRolls; LootEntry[] Entries }`, in the catalog.
  - `LootEntry` is one of:
    - an item, with a quantity range and a weight (default: the item's rarity score);
    - another table, nested at most 4 deep;
    - "nothing", with a weight.
- **The roll:** `LootRoller.Roll(table, seed, entity, tick)`.
  - **Its randomness:** a `SimRandom` stream keyed by (seed, entity, tick, `SimRandomSalt.Loot`), so it's deterministic on any thread, in any order.
  - **The weighted pick is integer arithmetic,** not floats, so it's the same on every machine.
  - **Its output** is item stacks in a caller's buffer, with no allocation.
- **Validation:** no cycles, weights above 0, quantities in range, items in the catalog.
- **Proof:**
  - the same inputs give the same drops;
  - over 100,000 rolls each entry's share is within tolerance of its weight;
  - nesting and "nothing" behave.
- **Not wired:** nodes keep their fixed yield. `ResourceNodeDefinition` gains an optional loot table, unset everywhere, so G5's haul or Part 2's drops can use it later.

### 3.9 The stall, the caravan and the player's prices
- **The stall:** `TradeStallData`'s per-item fields become a fixed table of goods, `StallGoods` (a C# 12 inline array of 16 `{ ItemType, Price, Stock }`), in catalog order.
  - The record stays unmanaged and raw-saved: one format change now, and none per item after.
  - Capacity applies per item, from the catalog.
  - The bounty fields and the treasury stay as they are.
- **The caravan:** its per-item fields leave `SupplyTuning` for the catalog's caravan blocks. `SupplyTuning` keeps the visit interval and the upkeep.
  - **The visit, unchanged in order:** export what the stall holds up to each quota, in id order; pay upkeep; restock supplied goods, food first, then the rest in id order.
- **The player's prices:** `UpdateStallPricesCommand`'s per-item fields become a typed `SetStallPriceCommand { StallId, ItemType, Price }`. Apply queues one per changed price, applied in queue order (deterministic).
- **Opening terms:** `StallTerms` and `EconomyTuning`'s per-item opening terms come from the catalog.

### 3.10 Code asks groups, not items
- **The rule:** outside the catalog's content, the map layouts and tests, production code doesn't name an item (`ItemType.X`).
- **The guard:** a test reads the sources and lists any offenders.
- **The exceptions,** few and named in the test: `ItemType.None` (an empty slot), and the session's opening Ration if it can't be expressed as a category.

## 4. Today's items in the catalog
| Item | Categories | Rarity | Consume | Equip | Stall | Caravan |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| Ration | Consumable, Food | Common | Hunger −40 | — | sells 15, opens with 50 | supplies to 50 at 2g |
| Iron Ore | Resource, Mineral | Common | — | — | buys 20, holds 20, opens with 10 | exports 2 at 24g |
| Medicinal Herb | Resource, Botanical, Consumable, Medicine | Common | Health +30 | — | buys 25, holds 20, opens with 10 | exports 2 at 30g |
| Glowstone | Resource, Mineral, Hazardous | Rare | — | — | buys 45, holds 20 | exports 2 at 70g |
| Hazard gear | Equipment, Protection | Uncommon | — | Protection: HazardResistance ×0.5 off damage, 500 ticks, per hazard tick | sells 30, opens with 3 | supplies to 3 at 20g |
| Pickaxe | Equipment, Tool, Mineral | Uncommon | — | Tool: HarvestSpeed (reserved, unused) | none (no source until Part 2) | none |

## 5. Quirks kept exactly, for a deliberate change later
1. **Two hunger values for one ration:** eaten from the pack it takes 40; eaten at a Garden plot, 50.
2. **A duplicate food flag:** `AgentState.HasFoodInInventory` is kept beside the pack's rations.
3. **Untyped yields:** a node yielding `ItemType.None` counts as food (old records).
4. **`ItemInstance.ItemId`** is stamped 1 everywhere, and `Quality` is never read.

Each change is a behaviour change, with the golden hashes re-recorded in a commit of its own.

## 6. Phases
Each phase is one PR into `main`. It must pass:
- `dotnet test` and 0 warnings;
- golden 3/3, **unchanged**;
- the pinned image, byte for byte;
- the soak's 9 runs with **identical numbers**;
- the MonoGame and Android builds.

A phase that moves a golden hash is not a refactor: stop and report.

| Phase | What | Days |
| :-: | :--- | :-: |
| **I1** | The catalog: `ItemDefinition`, `ItemCategory`, `ItemRarity`, `ItemCatalog`, `OutpostItems`, `ItemCatalogValidator`, on the session and the snapshot. `IsFoodYield`, `IsSalvage`, `IsWearable` and the `BaseValue` switch read the catalog. A load refuses unknown items. | 0.5–1 |
| **I2** | Cognition asks groups. The hand lists in `SellSalvageAction`, `LiquidateSalvageGoal`, `AffordableHeldSalvage` and `WorldStateBuilder` loop over the catalog in today's stated orders: the sale choice tries glowstone, herbs, ore and takes a strictly higher price; `SellSalvageAction` checks ore, herbs, glowstone. Plus the guard test (§3.10). | 0.5 |
| **I3** | Consumables: `StatKind`, `ConsumeEffect`, `ItemUse.Apply`. The ration's and the herb's effects come from their definitions; the quirks of §5 are kept. | 0.5 |
| **I4** | Equipment: `EquipmentSlot`, `StatModifier`, `CrawlerStats`; three slots replace the gear slot (save format 5). Hazard gear's resistance and life come from its definition, and `GearTuning` goes away. | 1 |
| **I5** | The stall, the caravan and prices: `StallGoods`, the caravan's blocks, `SetStallPriceCommand`, the opening terms from the catalog (save format 6). Best after refactor R4, which moves the stall into `Market`. | 1–1.5 |
| **I6** | The client from the catalog: merchant rows, the stock line and sprites by key. Renders unchanged. | 0.5 |
| **I7** | Loot tables: `LootTable`, `LootEntry`, `LootRoller`, `SimRandomSalt.Loot`, validation, determinism and distribution tests; the optional table on nodes (unset). | 0.5–1 |
| **I8** | Proof and docs, below. | 0.5 |

**I8's proof that the system is extensible:**
- **Adding:** a validator builds a catalog with a test-only item, a new salvage. With no other code change, the stall buys it, the caravan exports it, crawlers sell it and the merchant model lists it.
- **Removing:** a catalog without glowstone runs a day: nothing trades it, and nothing breaks.
- **Docs:** `AGENTS.md` gets "How to add or remove an item"; the economy model and the roadmap are updated.

**Total:** about 5–6.5 agent days. R7 was estimated at 2–3; the rest is the foundation asked for (rarity, consumable effects, equipment stats, loot tables).

**Order after Epic 10 Part 1:** refactor R1–R4, then I1–I8, then R5–R6. Part 2 starts on the catalog.

## 7. Deferred
| Item | Owner |
| :--- | :--- |
| Loot in play: node drops, expedition hauls, conditions (sector, danger, depth, a crawler's stats) | The milestone that first needs random drops (G5's haul if the producer wants it, otherwise Part 2) |
| Weapon stats in use (Attack, Defense), combat | The descent, Part 3 |
| Tool stats in use (HarvestSpeed), quality grades, crafting recipes, repair | Part 2 |
| Rarity in the client (colours, the merchant) | With the first rare drop the player sees |
| Item data files (JSON) and content tools | The map designer (Epic CLIENT) |
| §5's quirks | One behaviour-change commit each, when convenient |

## 8. Decisions for the producer (defaults apply unless you disagree)
1. **When:** after Epic 10 Part 1, before Part 2, with refactor R1–R4 first. The alternative is now, before G4 (G4–G6 add no items).
2. **Rarity:** five tiers, Common to Legendary, with scores 1000 / 250 / 50 / 10 / 2 as default loot weights.
3. **Equipment:** three slots now (Protection, Weapon, Tool), one item each.
4. **Ids:** `ItemType` stays a byte enum with one line per item. The alternative is ids defined by data, worth it only with item data files.
5. **Groups:** the flags of §3.4. Salvage is computed (a resource the stall buys), not a category.

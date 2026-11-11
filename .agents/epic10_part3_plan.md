# Epic 10 Part 3: creatures, combat and the gatekeeper (working plan)

Working document (AGENTS.md, Documentation Protocol 6), one of Epic 10's plans ([index](epic10_plan.md)). First written 2026-10-29 as "the descent", re-scoped by the producer on 2026-11-07 and answered in part the same day. On 2026-11-08 the producer gave the go: "The revised epic plan looks good. Please proceed with K0". The design moved into [the epic](../docs/epics/epic-10-part-3.md): the producer's words, the answered questions (Q1–Q21), the goal, the loop, the positions, the seams (A1–A7), the milestones, the Definition of Done, the deferrals and the risks. This plan keeps the steps.

- **Branch:** `feat/epic10-part3`, cut from `main` at `3f7fc36` (PR #23's merge). One PR for all of Part 3 (Q14), with a commit per step. Pushed and opened only on the producer's word.
- **Estimate:** 31–44 agent days:

  | K0 | K1 | K2 | K3 | K4 | K5 | K6 | K7 | K8 | K9 | K10 | K11 |
  | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: |
  | 2–3 | 3–4 | 3–4 | 2–3 | 2–3 | 4–5 | 2–3 | 2–3 | 3–4 | 3–4 | 3–4 | 2–3 |

  This is rough, as Part 2's first estimate was; K0 measures first.
- **Complexity** ([rubric](rules/complexity-grading.md)): **Very Complex** as a whole. It is epic-sized, and its systems have no precedent (combat, creatures, death, ranged, skills).
  - K1, K2, K5, K8, K9 and K10 are Very Complex: each is split below, and the developer supervises its steps stamped Very Complex.
  - K0 holds one supervised step (K0.3).
  - K3, K4, K6, K7 and K11 are Possibly Complex.
- **The producer's stops:**
  - the go, given for K0 on 2026-11-08;
  - looks at K3 (the first fights), K5 (death and rescue) and K8 (ranged);
  - a play session at K11.
- **Part 2's close-out** (its play session, W6.5, with F14 to decide, and W6.6) is still the producer's to hold. Part 3 runs beside it.

## 0. What Parts 1 and 2 hand over (checked at K0)
- **Health:** a crawler has 100. A hazard sector takes health by kind, through worn gear. At 0 a crawler is downed and limps to a bunk at 40% of its speed, getting up at 60. Nobody dies (Part 1's Q3).
- **Being hurt shows:** `AgentState.HurtThisTick` and the blinking red coat (S3, D71) work for any damage. Combat feeds them as it is.
- **Gear:** three equipment slots, fixed by an enum: protection, weapon and tool.
  - Protection holds one of hazard gear, a respirator or a hard hat.
  - The weapon slot and the `Attack` and `Defense` stats are unused.
  - `WearRule.PerUse` is reserved for "a weapon per hit".
- **Practice:** artisans gain grades (Standard, Fine, Masterwork) by making things (W4). It is the only practice in the game.
- **The workshops:** the forge and the still, with grades and repair. The stall's goods table holds 10 goods; 9 are used.
- **Loot:** loot tables and `LootRoller`, deterministic on `SimRandom` (item system I7), are not wired into play. `ItemRarity` has five tiers, and the sprite kit already draws icon plates with rarity rims.
- **Expeditions:** muster, march, work, return, deliver, with a retreat rule. Two parties of up to four can be out at once, never in the same sector. Draughts heal 80 at once.
- **Kindness:** the altruism evaluator, which shares resources with friends, is the precedent for rescue.
- **Leaving the world:** a drifter is removed at the end of the tick (`SessionHost`, `CrawlerDeparted`), and `DepartedCrawler` records who left.
- **The map:** Floor 1 has 13 sectors. Two of them, the Deep Seam and the Overgrowth, are charted only by a scout's report.
- **The planner:** 41 of 64 facts used.

## 1. The milestones, phased
Each step is a commit. After each: a build with 0 warnings and `dotnet test`. The whole part is one PR into `main` (the index's rules).

### K0. Groundwork (2–3 days)
**Complexity:** Very Complex, for K0.3. The rest is Possibly Complex.
- [x] **K0.1 Docs:** [Basic] (done 2026-11-08)
  - the answered draft into `docs/epics/epic-10-part-3.md`;
  - the roadmap: Part 3 ACTIVE;
  - the focus file.
- [x] **K0.2 Measure:** [Possibly Complex: probes and reading them]
  - planning cost and the planner's facts in use;
  - the frame time;
  - the economy after Part 2's F14 decision;
  - how many residents would venture, and with what purses (weapon prices are set against them, as the pickaxe's were, F9);
  - how often a lone venturer is downed in a hazard today: Q3 turns each of those into a death unless it is rescued.
  - **Done 2026-11-08** (scratch probes over the 23 gate seeds for three game days, with no player, G6's guild player and the soak's crafting players; `feat/epic10-part3` at `30aa984`, the same code as `main`). F14 is still undecided, so the economy is measured as it stands:

    | Measure | Result |
    | :--- | :--- |
    | Planner facts | 41 of 64 (`StateFact` 0–40, the last `HasMedicine`) |
    | Throughput (Release, `PopulationScalingProbe`, a quiet run) | 20,500 ticks/s with 12 crawlers, 23,300 with 16, 19,900 with 24: some 2,000 times 1× |
    | Frame time (Release, `FrameTimeProbe`, llvmpipe) | isometric 1.73 ms at 800×600, top-down 3.11 ms; the dashboard with a dossier 3.99 ms at 1280×720, the bounty board 2.06 ms. 30 fps allows 33 ms |
    | The economy (the soak, every profile, its default seeds) | 15/15 within their bands. Treasury at day 3: fair 890–1,056g, greedy 652–685g, guild 813–835g, generous 0g. Crafting 123–154g: F14 as W6 reported, with the still built on 2 of 3 seeds and no party home |
    | The bold (risk tolerance 30% or more), of 12 residents | 1–6 per seed, 2 on most; ready to venture (bold, at 80 health or more) in 7–47% of resident-hours |
    | The bold's purses at each day's end | 24–116g on average on day 1 and 52–116g on day 3, most 60–100g; the richest 64–120g. With the guild player, up to 185g |
    | Downed in three game days | **Nobody**, on any seed, with no player, with the guild player or with the crafting players |
    | The lowest health in a hazard sector | Residents 46–84 (most 65–74), drifters 57–100, members on an expedition 42–73. Nobody reaches the retreat line (40), so nobody retreats either |

    What it means:
    - **Weapons' prices (K3):** the bold carry 60–100g on most days, and residents who mine buy the 30g pickaxe from day 2 (D39). So a cudgel at about 15g and an iron blade at about 30g are within a bold resident's purse; the glowstone edge's price follows glowstone's.
    - **Q3's hazard deaths change nothing on the gate seeds by themselves** (finding F1 in the epic): nobody is downed by a hazard today. Deaths will come from combat, from orders that push past the retreat line, and from bad luck. So `TheCarefulLoseFew`'s band is set by combat, and K5.1 re-records the golden hashes only if a golden seed downs someone in danger.
    - **Room to spend:** planning and frame time leave wide margins for 15–25 creatures (§8's first risk); K2, K7 and K8 measure again.
- [x] **K0.3 The combat rule:** [Very Complex, supervised by the developer: the first fight's rule sets the pattern K2–K10 follow] (done 2026-11-08, waiting for the developer's review)
  - `Combatant`, the read model (A1), with range and proficiency in it from the start;
  - `CombatRules`: reach, interval, the hit roll on a new `SimRandomSalt`, mitigation by `Defense`, wear per hit;
  - `CombatValidator`;
  - the golden hashes unchanged: nothing fights yet.
  - **What was built** (`Simulation/Combat/`):
    - `Combatant`: id, position, body radius, health, standing, attack, defense, reach, swing interval in ticks, range and proficiency. `Combatant.ForCrawler` reads a crawler from its state and `CrawlerStats`; a creature's comes with K0.4.
    - `CombatRules`, pure and allocation-free: `InReach`, `Gap`, `CanSwing`/`NextSwingAt`, `CanStrike`, `HitChance`, `Mitigated`, `RandomFor`, `Swing` and `WearOnHit`, with `SwingOutcome`.
    - `SimRandomSalt.Combat` (10); `StatKind.Reach`, `AttackInterval` and `Range` (23–25).
    - `CombatValidator`, 9 tests: the roll seeded and the same on any thread, hit rates at their chance over 100,000 swings, the chance's bounds, the defense curve, a crawler's read with a test weapon and armour, reach and timing, wear per hit, a crawler against a hand-written creature (the same on a replay and in either order of resolution; over 40 seeds the crawler wins 24, the creature 15, both fall once), and no allocation.
  - **For the review** (K0's decisions D1–D8 in the epic's §0): the bare hand's numbers (D2), the target outside the roll (D3), no damage variance (D4), and above all D6, simultaneous resolution: K2.2's pass decides every swing of a tick from the tick's start and applies the harm after, so outcomes never depend on entity ids. D8 leaves to K2.4 whether a fighter's next swing is saved.
- [x] **K0.4 The creature catalogue:** [Possibly Complex: a new catalogue with a precedent, `ItemCatalog`] (done 2026-11-08)
  - kinds as data (A6), with a validator;
  - no spawns yet.
  - **What was built** (`Simulation/Creatures/`):
    - `CreatureKind` (ids never reused, as items' aren't);
    - `CreatureDefinition`: health, attack, defense, reach, swing interval, range, speed, body, sight, leash, the flight line, pack size, healing and return in game hours, and a loot table id for K4. Its fields are init-only properties, so a row reads by name;
    - `CreatureCatalog`: static and immutable, with `ExtendForTest`;
    - `Combatant.ForCreature`, so a creature fights through the one rule (A1).
  - **The three kinds** (placeholder numbers, which K2 and K3 tune by measurement):
    - burrow rats: 20 health, 3 attack, quick, packs of 3–4, fleeing under 40%;
    - stone beetles: 60 health, 7 attack, defense 8, slower than a crawler, packs of 1–2;
    - cave spinners: 30 health, a 5-unit spit for K8, fleeing under 30%.
    - All heal over 12 game hours and return after 2 game days (Q5).
  - `CreatureCatalogValidator`, 5 tests, checks the shape Q5 describes through the rule's expectations:
    - a bare hand needs 116 s against a beetle that downs it in 41 s, and the placeholder cudgel 31 s;
    - one rat loses to a bare hand, and a pack of three downs it in 16 s.
- **DoD:** the golden hashes, the 23 gate seeds and the soak are unchanged.
- **Done 2026-11-08:** every step above; K0.3 waits for the developer's review. Core 186/186 and client 485/485, though one allocation test failed once with the core's tests running alongside (F2); golden 3/3, unchanged; the 23 gate seeds and the soak hold; seeds 1–120 120/120 with and without the guild; the pinned image 485/485 exact; the MonoGame and Android compiles with 0 warnings.

### K1. The equipment system (3–4 days)
**Complexity:** Very Complex: it generalises the inventory, which hazards, harvest, outfitting, saves and the client rely on (bundling). Split as below; each step is Possibly Complex. The developer reviews K1.2's save design before K1.3 starts.
- [x] **K1.1 Slots as a table:** [Possibly Complex: a new catalogue with a precedent]
  - `EquipmentSlots`: each slot's id, name, kind, body region, draw layer, place on the paper doll and order;
  - the first rows: head, body, weapon and tool, with protection retired;
  - items fit a slot kind (`EquipmentBlock`);
  - a validator.
  - **What was built** (`Simulation/Items/EquipmentSlots.cs`, `WornGear.cs`):
    - `EquipmentSlot` stays an id enum, as `ItemType` is, and the id is the slot's index in storage. Protection's id 0 is retired, never reused. The weapon (1) and the tool (2) keep their ids, so the world hash's old encoding still holds for them. Head is 3 and body 4.
    - `SlotKind` is what fits a slot: an item's `EquipmentBlock.Kind`. A kind may have several slots, and a piece goes in the first free one in id order. `BodyRegion` is where a piece is worn, for portraits and worn sprites.
    - The catalogue is static and immutable, with `ExtendForTest`. `All` is in id order, the order stats are summed in; `InDisplayOrder` is for lists.
    - The inventory and its snapshot keep a record for each of 16 slot ids, indexed by the id (as an array here; K1.2 makes it the save's fixed record). `TryEquip` fills the first free slot of a piece's kind, or a named slot.
    - Stats, hazard wear and the "is it wearing" questions loop over the table's rows.
    - **One temporary rule:** `WornGear.TryReplacing` is what a piece takes the place of. The stall's sales, the guild's gifts and the scripted guild player share it. Until K1.3 a piece of protection takes the place of the protection worn, as G3's single slot did. So K1.1 and K1.2 change the structure only, and K1.3 changes this one rule.
    - **Deviation:** the save format is raised to 11 here, not at K1.2, because this step changes what the equipment's indices mean. It is the PR's one raise, and K1.2's changes ride on it, as W4's did on W0.5's.
  - `EquipmentSlotsValidator`, 6 tests: the table is consistent; Q11's first rows; an item fits a kind (two test ring slots fill in turn); a slot is a row (stats and the snapshot follow); the shared rule; no allocation.
  - **Done 2026-11-08:** core 187/187, client 485/485, golden 3/3 unchanged, seeds 1–120 120/120 with and without the guild.
- [x] **K1.2 Storage that grows:** [Possibly Complex: the save format, once]
  - the inventory's equipment as room for 16 slot records, indexed by slot id;
  - the save copies all 16, so a new slot loads old saves with it empty;
  - the goods table at 16;
  - one format bump.
  - **What was built** (the design for the developer's review, before K1.3):
    - `WornEquipment` (`Simulation/Items/`), an unmanaged inline array of 16 `ItemInstance` records indexed by slot id, 256 bytes and pinned. The inventory holds one, the snapshot copies it whole, and `InventorySave.Equipped` is one, saved as raw memory.
    - **The save copies all 16 records,** filled or not, so a bare crawler saves as many bytes as a geared one. A slot added to the table later finds its record empty in an older save, and loads with nothing in it, with no change of format. A seventeenth slot changes the format (the pin says so).
    - **Refused before anything loads** (`SaveItems.FirstMisworn`, read after the unknown-item check): a piece in a slot this game doesn't define (a retired one, as protection's 0, or one a newer game added), and a piece in a slot of another kind. The player reads, for example, "The save holds equipment this game can't wear (a HardHat in SwarmAgent_3's Body slot)".
    - **The goods table at 16** (`TradeStallData` 136 to 196 bytes, pinned), room for Part 3's arms and armour, the forge's and the caravan's.
    - **The format:** 11, raised once in K1.1 for the whole PR.
    - No migration: a format-10 save is refused with a reason, as every older format is.
  - Tests: `EquipmentSlotsValidator` gains 3 (every record round-trips; a new slot loads an older save empty; misworn pieces are refused); `LiveSaveTests` refuses a live save with a piece in retired slot 0 and one with a hard hat on the body; the size pins.
  - **Done 2026-11-08:** core 187/187, client 485/485, golden 3/3 unchanged, seeds 1–120 120/120 with and without the guild, the soak 15/15 (bands as at K0), the pinned image 485/485 exact, the MonoGame and Android compiles with 0 warnings. **Waits for the developer's review of the save design before K1.3.**
- [x] **K1.3 Stats and wear over every slot:** [Possibly Complex: behaviour; the golden hashes may move]
  - `CrawlerStats` sums every worn slot, with each stat's stacking rule (add, multiply, a cap);
  - each piece wears by its own rule.
  - The golden hashes are re-recorded in their own commit if the hard hat and hazard gear, now worn together, change behaviour.
  - **What was built:**
    - **Stacking** (`StatRules`, `Simulation/Items/`): a stat is its base, plus every worn piece's adds, times every piece's multiplies, in the slot table's id order, then held within the stat's bounds. The bounds that were scattered are rows now: every resistance at most 0.9 (`StatRules.MaxResistance`; `CrawlerStats.MaxHazardResistance` is it), harvest and move speed at least a tenth (W1.4's floor on mining), no negative attack, defense, reach or range, and a swing of at least 0.1 s. A stat with no row is unbounded. No live value was out of bounds, so no number moved.
    - **Wear by rule** (`AgentInventory.WearBy`): `WearRule.PerUse` became `PerHarvest` (a pickaxe) and `PerHit` (a weapon), and `PerBlowTaken` (armour, D7's open question; `CombatRules.WearOnBlowTaken`) is new. A tick in danger, a mineral unit mined, a blow landed or taken wears every piece with that rule, whichever slot it is in, and names the pieces that broke into a stack buffer: no allocation. The runtime's hazard wear, the harvest's tool wear and combat's weapon wear all go through it; none names a slot.
    - **`WornGear`'s temporary rule is gone:** a piece takes the place only of the piece in its own slot. A hard hat and hazard gear are worn together; a respirator takes a hard hat's place. `WornGear.Protection` is the first protective piece in id order (the head's), for what still shows one piece until K1.5.
    - **Deviation, the scripted guild player** (`GuildPlayer.Outfitted`): it compared the best gear in stock with the member's one protective piece. Compared slot by slot, it put hazard gear on every member already in a respirator (an empty body slot reads as worn out), drained the stall's gear, and the first trip of `ASaveMidCraftAndMidExpeditionLoadsAndPlaysOn` slipped from day 4.3 to day 5.75. It now gives a piece to a member with no sound protection, in place of a worn-down piece in its slot, or when it cuts the hazard more than everything the member wears (`GuildRules.WearsSoundProtection`, `CrawlerStats.HazardResistance`): G3's and W3's rule, over every slot. A protected member's bare slot waits for K1.4. The first trip is now on day 5.25: residents who buy a respirator keep their hazard gear rather than trading it back into stock (D53's trade-in now happens only within a slot).
  - **Expectations that flipped, on purpose:**
    - `ExpeditionTests.GearIsGivenForTheTargetsHazardSlotBySlot` (was `…AndSoundGearGoesBackToStock`): the respirator goes on over the gear, nothing goes back, a hard hat is refused against flooding (no better than the respirator in its slot) and allowed against rock.
    - `LoneVentureTests.ABoldCrawlerTradesItsHardHatForARespiratorKeepsItsGearAndWorksTheGalleryAlone` (was `…TradesItsGearForARespirator…`): D53's trade-in, within the head slot; the hat goes to the shelf of worn pieces.
    - `RightGearScenarioTests`: members keep their gear under the forge's respirators, so the live trips show hazard gear alone and a respirator over gear: a fifth of the harm (0.015 against 0.075 a tick; 0.5 and 0.75 held at 0.9), not W3's half. A respirator alone still halves it (`HazardGearTests`, a mechanism test).
  - `EquipmentSlotsValidator`, 11 tests (test 5 rewritten for the per-slot rule; 10, stats stack within their bounds: a hard hat over hazard gear stops 0.9 of falling rock, two lead rings can't stop mining or take defense below none; 11, each piece wears by its own rule: exposure wears the hat and the gear, the harvest the pickaxe, a blow landed a blade, a blow taken mail, a ring on a new slot by its rule).
  - **Done 2026-11-08:** core 187/187, client 485/485, golden 3/3 unchanged (no forge in the golden runs, so nobody wears two pieces), seeds 1–120 120/120 with and without the guild, the soak 15/15 (crafting 123–154g, as at K0), the MonoGame compile with 0 warnings.
- [x] **K1.4 Outfitting by slot:** [Possibly Complex: cognition and the party builder]
  - residents' purchases and the party builder pick the best piece for each slot for the target's dangers, scored by stats, not by named items or groups;
  - W3's outfitting by hazard kind becomes a case of it.
  - **What was built:**
    - **The score** (`Outfitting`, `Simulation/Items/`): an `OutfitNeed` is the hazards a wearer faces and whether it mines (K3 adds creatures). A piece is worth what it changes in the wearer's stats for the need, stacked and bounded, with it worn in its slot: `CrawlerStats` takes a `WornSwap` (a piece in a slot in place of what is there; nothing moves). Hazard gear under a hard hat against falling rock is worth 0.15, not its 0.5. `BestInStock` is the stall's best piece for a slot, over what the slot holds or over the slot bare (a worn-down piece is refreshed, D12); the first in id order on a tie, as D50. No allocation.
    - **Residents** (`ResourceTargeting`): `TryChooseProtection` reads every equipment good, not the protection group. First a piece that opens hazardous salvage it can't brave alone (D51, D53, D54; judged by its resistance with the piece worn), trading in what its slot holds if it must; else the piece that most cuts the harm where it works hazardous salvage, for a slot it has bare. So a venturer in hazard gear buys a hard hat for the Glow Caves. `WouldUpgradeProtection` became `WantsMoreProtection` (opens, or fills a bare slot); the planner's facts ask `Protects` and `EquippedForWork`, by stats. The pickaxe's purchase is `TryChooseWorkGear`: the piece that most speeds mining, for a bare slot. `CanBuyGear`, `CanBuyTool`, `ProtectionForSale` and `ToolForSale` are gone.
    - **The guild** (`GuildRules`): `CheckGiveGear` compares by the gain over what the slot holds (by the stats of all the member wears), not by the two pieces' definitions. `GiftFor` is the best piece for a member's slot; `PlanOutfit` plans a party's outfit, the party builder's and the scripted player's alike, in two rounds: each member with no sound protection gets its one best piece, then every slot of every member gets its best piece, as stock allows. `BestProtectionInStock` is gone; `BestForBare` names what outfitting gives per slot ("Outfit: respirator, hazard gear").
    - **Deviation, two rounds:** a single pass slot by slot gave the first unprotected members two pieces each and left the rest bare until the caravan, which slipped `ASaveMidCraftAndMidExpeditionLoadsAndPlaysOn`'s first trip again. The scripted player runs the unprotected's round for both parties before either party's second round.
    - Golden, the sweeps and the soak are unchanged: with only hazard gear on the shelf, residents choose as before.
  - Tests: `OutfittingValidator`, 5 (a piece is worth what it changes in the stats; scored by its stats, not its name, and a tie goes to the lower id; asking moves nothing; the best in stock for a slot; no allocation); `ExpeditionTests.TheUnprotectedAreOutfittedFirstThenEverySlot`; `PlayerTextTests`' outfit button names a piece per slot.
  - **DoD `GearByTheSlot`** (`GearByTheSlotScenarioTests`): on the 23 gate seeds, live, the scripted workshop player builds a forge and orders hard hats; a bold resident in hazard gear buys a hard hat and wears the two together on 17 of 23 seeds within six days (asserted: at least 15). On the other six no venturer remembered the caves' glowstone (spent nodes regrow out of sight) when it had the gold. Every wearer is a venturer who bought both; the ledger balances and no handler throws on any seed.
  - **Done 2026-11-08:** core 190/190, client 485/485, golden 3/3 unchanged, seeds 1–120 120/120 with and without the guild, the soak 15/15 (every treasury as at K1.3), the MonoGame compile with 0 warnings.
- [x] **K1.5 The client from the table:** [Possibly Complex: Gum UI and worn sprites]
  - the dossier's and the party builder's equipment panels, laid out from the slot table;
  - worn sprites layered by each slot's draw layer.
  - **What was built:**
    - **The panels:** `WornPieces` (`Presentation/Dashboard/`) is a row per slot in the table's display order: each piece's words ("Hard Hat, 64% left"), short ("Hard Hat 64%"), what's left ("64%") and icon, rebuilt only when they change. The dossier, the guild's cards and the expedition's rows read it; `DossierModel.Gear`, `Tool` and their icons are gone. The client's `WornLine` lays it out on one line, an entry per slot made the first time the table shows it: "Gear: [icon] Hard Hat 64% [icon] Hazard Gear 80% [icon] Pickaxe 80%" in the dossier (the full words would overflow its 350 pixels), the icons and "64%" on the guild's cards, before the bunk; "Gear: None" with nothing worn. The paper doll's cells (`DollColumn`, `DollRow`) are unused: the dossier has no height for a grid at 720 rows.
    - **Worn looks:** a frame carries a `WornLook` (`Presentation/Art/`), each slot's piece that shows worn, compared and hashed whole. Bodies (`BodyKey`, `BodyKit`) and portraits (`PortraitKey`, `PortraitKit`) bake from it up the slots' draw layers: the head's helm or mask, a coat over the shirt (the portrait's shoulders), the tabard over a coat. A crawler with no look shows its topmost piece (`WornLook.Topmost`).
    - **Deviation, hazard gear's art:** K1.1 put hazard gear on the body, but its art was still a yellow helmet, so worn with a hard hat it couldn't show. It is now a suit: `Worn.Torso` (new), a new `ItemShapes.Vest` glyph (sleeveless, open at the neck, two reflective bands), silver bands in place of the helmet's lamp. Its icon, map sprite, carried marker and the 24 figures wearing it changed; the tests and pins that used it as the helm example use the hard hat.
    - **For the producer's look:** the guild's tabard device is gold, the suit's own yellow, so it doesn't show over the suit; the blue tabard does.
  - Tests: `BodyKitTests.TheCoatColoursTheShirtAndShowsWithTheHelm`; `PortraitKitTests.TheHelmTheSuitAndTheTabardShow`; the dossier's rows in display order and their words (`PlayerTextTests`); the dossier line (`DashboardPanelsTests.TheDossierShowsEachWornPiece`); the frames carry each slot; the people sheet gains a hazard suit, a hard hat over it and a guild member in both; the pins gain coats by build, a hat and coat, a suit portrait and a hat-and-suit portrait (151 bodies).
  - **Renders and pins:** the art pins re-recorded in their own commit (`2ca0f06`) after a look at the art review sheet and the people sheet. 17 renders re-recorded in the pinned image in their own commit (`0576e37`) after a look at each, every change local: the dossier's gear line, the guild's cards, Ilse's portrait in a suit, residents in suits, and hazard gear's vest on the stall's counter (every isometric view) and in the merchant terminal. Four of them differ past the local tolerance; the rest by a few dozen pixels.
  - **Done 2026-11-08** (`59cc433`, `2ca0f06`, `0576e37`): core 190/190, client 486/486, the pinned image 486/486 byte for byte, the MonoGame compile with 0 warnings. Golden, the sweeps and the soak don't read the client.
- [x] **K1.6 The proof and the recipe:** [Basic: a test like `ItemSystemProofTests`, and a section of AGENTS.md]
  - a test adds a feet slot and boots to a test catalogue, and shows that stats, outfitting, the save's round trip and the dossier follow;
  - AGENTS.md gains "Adding an equipment slot".
  - **What was built:**
    - `TestCatalogs` (`Simpiens.Testing.Core`, test only): the catalogues' test hooks for any test project, and the proof's feet slot (5, a new kind, on the feet, under every other piece) and boots (any hazard's harm cut by a fifth, worn out by exposure).
    - `EquipmentSlotProofTests` (core), on the real outpost with boots on the stall's shelf: the boots go on the feet and stack with hazard gear (0.7), and a tick in danger wears both; **a bold resident in hazard gear buys a pair for its bare feet on its own** (t1622 on the default seed); the guild's outfit gives boots for a member's feet beside hazard gear; a live save holds them and loads them back at format 11.
    - `EquipmentSlotProofTests` (client): the dossier's model has a row for the feet in display order, and its line shows the boots.
    - `SlotNamesTests`, as `ItemNamesTests`: production code names a slot or a kind only in the table and the catalogue. The last one outside was the stall's tally of tool purchases, now "it speeds mining", by stats.
    - AGENTS.md: "Adding an equipment slot".
  - **Done 2026-11-08:** see the K1 close below.
- **DoD `GearByTheSlot`:** on the gate seeds, a venturer wears a hard hat and hazard gear together. The guild's and the workshops' scenarios hold. **Met at K1.4** (`GearByTheSlotScenarioTests`: 17 of 23 seeds, at least 15 asserted).
- **K1 done 2026-11-08** (`2729757`..K1.6's commit): core 195/195, client 487/487, golden 3/3 unchanged through all of K1, seeds 1–120 120/120 with and without the guild, the soak 15/15 (crafting 123–154g, as at K0), the pinned image 487/487 byte for byte, the MonoGame and Android compiles with 0 warnings. **For the producer's look:** hazard gear's suit (the art review sheet, the people sheet, the 17 renders), the gear lines in the dossier and the guild's cards, and the tabard's gold device lost on the suit's yellow.

### K2. Creatures (3–4 days)
**Complexity:** Very Complex: a new system. Split as below. The developer supervises K2.2 and K2.3.
- [x] **K2.1 Lairs:** [Possibly Complex: a map change, with golden and renders re-recorded]
  - lairs as map data in the Overgrowth and the Deep Seam;
  - spawns on the seed;
  - refilling on the day's time.
  - **What was built:**
    - **Map data:** `LairDefinition` (key, kind, heart) in `MapDefinition.Lairs`, `MapDefinitionBuilder.Lair`. Floor1: the rats' warren at (21, 16) in the Overgrowth and the beetles' hollow at (−21, 16) in the Deep Seam, each in its far corner: one node in its kind's sight (overgrowth.3, seam.1), one past its leash, and the sector's door past it.
    - **Creatures as entities:** `EntityType.Creature`, and `CreatureState` (`Simulation/Creatures/`: kind, lair and place, home, position, health) on the entity, as a crawler's `AgentState` is: the entity's position is the creature's.
    - **`CreatureService`** (`SimulationManager.Creatures`, following the map as the workshops do): every lair filled at the session's start to a pack drawn from the seed within its kind's range (`SimRandomSalt.Lair`), each member at its place around the heart; a slain member (`Slay`, for K3 and the tests) leaves the world and returns to its place after its kind's `RespawnHours` of the day (Q5: 2 game days). `Tick` is the creatures' phase, run by the host after the crawlers' runtime (seam A4); K2.2 adds each creature's rule to it. The creatures are kept in id order.
    - **Ids of their own:** a creature's id comes from a seeded sequence of its own (`SimRandomSalt.CreatureId`), not the entities' allocator, so creatures coming and going never change who the next crawler or drifter is (their personalities and looks are drawn from their ids). The counter is the service's, for K2.4's save.
    - **Held apart until their steps:** crawlers don't perceive creatures until K2.3 (`AgentMemory` skips them), and the frames don't carry them until K2.5 draws them. A load fills the lairs afresh until K2.4 saves them.
    - **Deviation:** the plan expected golden and renders re-recorded here. Neither moved: the creatures' own ids, perception and the frames hold K2.1 to structure, and behaviour changes with K2.2's rule.
  - Tests: `LairValidator`, 5 (the lairs fill on the seed; the same seed fills them the same, with ids never from the entities' sequence; every pack size turns up across 60 seeds; a slain rat returns two game days on, not a tick before; the phase allocates nothing); `MapDefinitionValidator`'s (l), Floor1's lairs; `LairTests.TheOutpostOpensWithItsLairsFull` (a respawn and a load fill them the same).
  - **Done 2026-11-08:** core 197/197, client 487/487 (one allocation test failed once in a full run and passed after: F2, again), golden 3/3 unchanged, seeds 1–120 120/120 with and without the guild, the soak 15/15 (crafting 123–154g), the MonoGame compile with 0 warnings.
- [x] **K2.2 The creature's rule:** [Very Complex, supervised by the developer: the creature's tick, its own phase in id order (A4)]
  - rest, roam, chase within the leash, strike, flee, go home, heal;
  - its paths through the existing pathfinder;
  - swings resolved in one pass after movement.
  - **What was built** (`Simulation/Creatures/CreatureService.Rule.cs`; the decisions D9–D15 are in the epic, for the developer's review):
    - `CreatureMode` (rest, roam, chase, flee, home) on `CreatureState`, with its quarry, its swing's timer (D8), its rest's end and its goal; its path is work in progress.
    - The phase: every creature in id order decides and moves, then every due swing is decided from where all stand, then all the harm lands (`ICreatureWorld.Strike`, the simulation's: harm, the red coat, armour's wear, a downing; the crawler notes who swung, `StruckAtTick`, `StruckBy`).
    - Paths through a `GridAStarSearcher` of the creatures' own, on the main thread, planned again when the goal moves past a unit.
    - **`Hunting` is off in the live session (D14):** creatures rest and roam there, and notice crawlers from K2.3. So golden, the sweeps and the soak are unchanged.
  - Tests, `CreatureRuleTests` (7, on the real outpost, hunting on, the stray put and held by hand): every creature rests and roams within half its leash and in its sector; a rat swings 0.7 s after a stray appears in its sight, and the warren downs it in 12 s (34 blows), then lets it be and goes home; the leash ends a chase and nobody follows past it; a hurt rat flees home heeding nothing and heals an hour's twelfth in an hour; the same seed plays the same, and blows on one tick all land; the rule allocates nothing once warm; and without hunting no crawler is touched in a day.
  - **Done 2026-11-08:** core 204/204, client 487/487, golden 3/3 unchanged, seeds 1–120 120/120 with and without the guild, the soak 15/15 (crafting 123–154g), the MonoGame compile with 0 warnings. **Waits for the developer's review before K2.3.**
- [x] **K2.3 Crawlers meet creatures:** [Very Complex, supervised by the developer: a new reflex in the runtime's pre-emption, and cognition]
  - flight, a reflex beside retreat and panic;
  - known lairs judged in `LoneDanger`, as hazards are;
  - a scout's report charts lairs;
  - nobody yet fights back, and nobody dies: until K5 a downed crawler limps out, as today.
  - **The go:** "Proceed" (the producer, 2026-11-10, after the K2.5a report closed with "Say the word and I'll start K2.3"); K2.2's rule (D9–D15) stands by exception. Split into four sub-steps; the decisions D16–D20 go into the epic for the developer's review.
  - [x] **K2.3a Lairs' reach, danger and charting:** [Possibly Complex: map data and a pure rule, plus one mask in the snapshot]
    - A lair's **reach** is a disc of its kind's leash around its heart: the ground its pack defends (D16).
    - Its **danger** is a pack's expected harm a second (its kind's smallest pack, the combat rule's hit chance, the crawler's defense through what it wears), compared with `Vitality.LoneHazardLimit` as a hazard's is (D18).
    - A lair is **charted** (known to everyone) when a crawler on its feet sees one of its creatures in a charted sector, within its vision and line of sight, or is struck by one (D17). The creatures' phase decides it, after the swings; `SharedWorldSnapshot.ChartedLairMask` carries it to cognition from the next tick. Saved with the creatures in K2.4; until then a load forgets it, as it refills the lairs.
    - **Built:** `LairReach` (reach, the lair at a point, danger), `CreatureService.ChartedMask` (charted on a strike or a sighting, behind `Hunting` until K2.3d), the snapshot's `ChartedLairMask`, `Vitality.LairDangerAt` and `WouldEnterAt`. Tests `LairKnowledgeTests` (6). Core 210/210, golden 3/3 unchanged.
  - [x] **K2.3b Lone crawlers keep out of known lairs:** [Possibly Complex: every place-judgment that sends a crawler somewhere alone]
    - `Vitality.WouldEnterAt(context, position)`: the sector's hazard as today, and no known lair's reach a crawler would not brave alone.
    - Read where salvage is chosen (with a Gather bounty's), where a hurt crawler fetches a herb, where an explorer and an idle wanderer go.
    - **Built:** `ResourceTargeting.KnowsSalvage` and `TrySelectSalvage` (a Gather bounty's and a herb's go through it), `TryPickSectorWaypoint` (a waypoint in a reach falls back to the sector's own point, or none) and `AmbientWanderEvaluator` read `WouldEnterAt`. The outfitting judgments (what gear opens) stay on hazards: gear against creatures is K3's. Test `ALoneCrawlerLooksPastAChartedLairsReach`. Core 211/211, golden 3/3 unchanged.
  - [x] **K2.3c Flight:** [Very Complex, supervised by the developer: a reflex in the runtime's pre-emption and in cognition]
    - Struck by a creature in the last 2 s, on its feet, a crawler drops what it does (the runtime's pre-emption, after exhaustion and before starvation) and flees: a `FleeIntent` toward the bunks, shown panicking, that ends once it stands out of every lair's reach and has not been struck for 2 s (D19).
    - `AgentContext.StruckAtTick`; `FleeIntentHandler`; the reflex in `MentalBreakEvaluator` beside retreat. A downed crawler limps out as today.
    - **Built:** `FleeIntent` and `FleeIntentHandler` (registered; its path returned to the pool), `Vitality.FlightSeconds`, `StruckRecently`, `ClearOfLairs`; `AgentContext.StruckAtTick`; the runtime pre-empts a struck crawler on its feet after exhaustion and before starvation; `MentalBreakEvaluator` flees first among the danger reflexes; `IIntentExecutionContext.Map`. No new activity counter: `AgentActivity` is saved, and the live checks watch for `FleeIntent`.
    - Tests `FlightTests` (2): held by the warren until the first blow, a stray flees two ticks later, clears the reach in 3.2 s for 24 health (the rats' leash still 8), stops 1.2 s after, and keeps out of the charted warren for 150 s; a downed one limps for the bunks and nobody strikes it. Core 213/213, golden 3/3 unchanged.
  - [x] **K2.3d Hunting goes live:** [Possibly Complex: a behaviour change on every seed]
    - `CreatureService.Hunting` removed; the rats' leash tuned to 6 (D20); golden re-recorded in its own commit.
    - Measured on the 23 gate seeds' day: who strays and is struck, who flees, what is charted when; the sweeps and the soak.
    - The DoD `VerminDriveTheLoneAway` if the day shows it unaided; otherwise with the scripted player's scout bounty as its input.
    - **Prep done 2026-11-10** (`ef77809`, hunting still off): `OutpostWatch` watches the creatures (blows, hits, the struck, flights, downings, each lair's charting, lone entries into a known lair, which fail the day) and the sweep prints it; the rats' leash is 6.
    - **The word, 2026-11-10:** "Chose A. Proceed". Hunting on (`e73b586`), F3 settled by option A in the same commit (a quit for want of pay is the failure, not a single unpaid visit; the crafting loop's pay and makes judged across seeds 1–20; the mid-craft save searched over 12 days), golden re-recorded (`7298461`: only 20261010 moves, from t1900), the DoD `VerminDriveTheLoneAway` (a stray hurt and fleeing on 8 of the 23 gate seeds, none downed, the hollow charted on all, no lone entries, the days hold).
    - **Was waiting for the developer** (F3, in the epic): it is ready as `.agents/local/k2.3d-hunting-flip.patch` (the switch removed; three test files). With it applied: gate seeds 23/23, seeds 1–120 119/120 both ways (seed 82, one Grove jitter), the story unaided on 53 of 120 seeds; but the crafting soak fails on its three seeds and the workshop loop on gate seed 1 (F3's options A/B/C; A recommended). After the word: apply, settle F3, re-record golden in its own commit, the renders in the pinned image, and the DoD `VerminDriveTheLoneAway` (the day shows it unaided).
- [x] **K2.4 Saves:** [Possibly Complex: a save block] creatures and lairs in the world's block, with the round-trip validators.
  - **What was built:** `CreatureSave` in the world's block (`WorldSave.Creatures`), written and read by `CreatureService` (`Capture`, `Restore`, `Check`): the id counter, the charted lairs, each lair's pack and places (a slain member's return), every creature by id with its position, health, mode, quarry, goal, rest and swing's timer (D8: saved, for K11.1's mid-fight load). Not saved: its path, and when it last swung. A save that doesn't fit the map is refused before anything changes; a save without the block fills the lairs afresh. The world hash mixes the creatures into its map part. The save format stays at 11 (raised once in this PR).
  - Tests: `SessionRestoreValidator.ValidateCreaturesRoundTrip` (the bytes, every field, four refusals, the records' sizes); `LairTests.ALoadPutsTheCreaturesBackAsSaved` (a slain rat's waiting place and a hurt rat, byte for byte and the same hash; the rat returns on time after the load); `LiveSaveTests` checks the block.
- [ ] **K2.5 The client:** [Possibly Complex: new art and renders to look at]
  - [x] **K2.5a The creatures drawn, ahead of K2.3** (the producer, 2026-11-09: "Before we move to K3 i want to get some sprites/rendering going for creatures. They can be simplistic for now"): [Possibly Complex: new art, the frames and the scene, renders to look at]
    - `CreatureSprites` (generated placeholders): each kind from the side, facing right, two frames of a scuttle, on a crawler's 16-pixel canvas with its feet on row 14; in the world sheet before the body slots.
    - Art rows per view mode, `creature/<kind's art key>` (`TopDownArtTable.Creatures`: each kind's frame rate, shared with isometric; a crawler's size in top-down, standing on its feet in isometric), and `EntityVisuals.Creature(kind)` (the missing art for a kind with no row).
    - The frames carry creatures (`EntityView.Creature`, `CreatureMode`, `IsCreature`): the frame skip of K2.1 is gone.
    - The scene draws each among the crawlers by depth, on the shadow in isometric: scuttling while it moved over the last tick, its first frame while it stands, mirrored when it last went left on screen (`CreatureAppearance`, the view's own: which way is left is the projection's). The fog hides them.
    - Tests: `CreatureViewTests` (the frames carry every creature as its kind; the fog hides them, and charted each is drawn as its kind where it stands, in both modes; a rat scuttles while it roams and stands still resting; facing by the projection, by hand; renders `render-iso-warren-t300` and `render-floor1-hollow-t300`, the lairs charted by hand); `ArtCompletenessTests` (every kind has its own art in both modes, every row is used); the sheet's order.
    - **Done 2026-11-09:** golden untouched (no core change). Five existing renders moved, each by the stone beetle alone, in the Deep Seam once a scout charts it (t1500, the fog lifting, the caravan at t2400, the expedition); they passed locally under the render tolerance, and the pinned image re-recorded them. Commits `f3d04b4` (code), `38953e6` (art pins), `003e672` (renders).
  - [x] **K2.5b The strike pose, with live hunting** (2026-11-10, `570ada4`): [Basic: a frame flag and a view timing] a creature notes when it last swung (`SwungAtTick`, unsaved); the frames carry its quarry and the swing; the view lunges it 0.2 units at its quarry over 0.25 s of view time, facing it. Test `ARatLungesAtWhatItStrikes`.
  - [x] **K2.5c A creature in the inspector** (2026-11-10, "Continue"): [Possibly Complex: selection, the inspector's model and card, an atlas region]
    - A creature is pickable; the selection holds a crawler or a creature, never both (`WorldSelection.SelectedCreature`, `Selected`, `TryGetSelectedPosition`, `SelectedName`); the ring, its name and the camera's follow take either.
    - `InspectorModel` shows a creature (its kind's name, its badge by mode, its lair's room, its health against its kind's, what it does and after whom); `InspectorCard` hides the needs and the dossier for one; `PortraitAtlas.ShowCreature` bakes its kind's card (`CreatureCards`: its sprite three to one, centred on a cave's floor, in a frame of its own) once per scale.
    - Tests `AClickOnACreatureSelectsItWithItsRingAndName`, `TheInspectorShowsACreature` (render `render-iso-inspector-beetle-t300`), `ACreaturesCardIsItsSpriteThreeToOneInItsFrame`.
  - Still K2.5's, with the step that makes it visible:
    - the red coat on creatures too, with K3 (nothing hurts a creature before).
- **DoD `VerminDriveTheLoneAway`:** on the 23 gate seeds' day:
  - a crawler who strays into a lair's reach is hurt and flees;
  - nobody enters a known lair alone;
  - the day's checks hold.

### K3. Melee arms and armour (2–3 days), then the producer's look
**Complexity:** Possibly Complex: items on K1's table, and one new reflex.
- **The go:** "Continue" (2026-11-10), after K2's report: K2.5's inspector, then K3 up to the stop for the producer's look.
- [x] **K3.1 Melee weapons (Q10):** [Possibly Complex: a stat read in combat]
  - the cudgel, the iron blade and the glowstone edge;
  - their recipes, and the caravan's terms;
  - wear per hit; grades and repair.
  - **Built** (with K3.2): `ItemType` 10–12, rows in `ItemCatalog` (the weapon slot; attack +3 and reach +0.1 for the cudgel, +6 and a swing a tenth quicker for the blade, +10 and the same for the edge; durability 80, 120, 160 blows landed), the stall's terms (sold at 12g, 40g, 85g, none to start), the forge's recipes (the blade three ore in 2 h, the edge two ore and a glowstone in 3 h); looks in `ItemArt` on two new shapes (`Club`, `Blade`); words in `ItemTexts`. Grades and repair follow, since each forged piece has a recipe.
  - **Deviation:** the caravan's cudgel supply moves to K3.3. Stocked before anyone buys a weapon, its 12g once tipped the crafting loop's quits for pay from 2 to 6 of seeds 1–20, so it comes with its buyers and is measured with them.
- [x] **K3.2 Armour from ore (Q11):** [Basic: item rows and recipes, by AGENTS.md's "Adding an item"] the iron helm and the ring coat.
  - **Built:** `ItemType` 13–14: the iron helm (head, defense +3, two ore in 2 h, 30g) and the ring coat (body, defense +6, four ore in 3 h, 55g), each wearing by the blow taken (80 and 120); the helm on the helmet's shape in dark iron, the coat on a new `Mail` shape, worn as a coat. The worn figures, bodies and portraits follow.
- [x] **K3.3 Fighting back:** [Possibly Complex: cognition, a fact and a reflex]
  - the bold and armed fight back;
  - the bold buy a weapon (Q6);
  - outfitting for creatures comes from K1.4's rule.
  - [x] **K3.3a The fight:** [Possibly Complex: a reflex, an intent, and crawlers' swings in the creatures' pass]
    - Struck, a crawler who is bold (as one who would venture), armed (it strikes harder than a bare hand) and above the retreat line fights back (`Vitality.FightsBack`); the rest flee as before. The runtime pre-empts for either; `AgentContext.StruckBy`.
    - `FightIntent`: out of reach it steps toward the creature, in reach it asks the creatures' pass for a swing (`SwingAtCreature`, `CreatureService.AskSwing`); done when the creature is slain, gone, or runs past 3 units; failed under the retreat line (it then flees) or downed. Shown defiant.
    - The pass decides the crawlers' asked swings with the creatures' own, from where all stand, before any harm lands (D6), on each attacker's stream; the crawler's timer is its own (`AgentState.NextSwingAt`, unsaved: a fight is work in progress). A blow wears its weapon (D7) and turns a resting or roaming creature on its attacker; at no health the creature is slain (`Slay`: its place waits 48 game hours), the chronicle tells it (`CreatureSlain`), and `CreatureService.Kills` counts it.
    - Tests `FightTests` (4): with the glowstone edge and a ring coat a bold crawler slays the beetle that struck it in 9 blows, keeping 66 health; the timid with a blade and the bold bare-handed flee; a fighter under the retreat line flees.
  - [x] **K3.3b The bold arm themselves, and brave what they can clear:** [Possibly Complex: a goal, an action, a fact, a judgment]
    - `ArmForFightGoal` and `BuyWeaponAction` (`StateFact.HasWeapon`: armed with the best it would buy): a bold resident buys the weapon that most raises its attack among those the stall has in stock at or under its price, with a ration's to spare (`ResourceTargeting.TryChooseWeapon`, by `OutfitNeed.ForFighting`), and a better one later. The old one is traded in, as protection is (the trade handler).
    - The caravan supplies the cudgel (two at 6g), moved here from K3.1 to come with its buyers.
    - **D21, the bold and armed brave a lair they can clear:** a known lair is no danger to a bold, armed crawler on its feet that expects to slay its smallest pack, one by one, before its health falls to the retreat line (`Vitality.HarmToClear`, `BravesLair`, read through `WouldEnterAt`). So a blade-wielder at full health works seam.1 by the beetle hollow. The day's lone-entry check exempts it.
    - **Tuned:** the iron blade +8 attack (was +6), the glowstone edge +12 (was +10), so a blade clears a lone beetle (51 health expected, from full) or three rats (49).
    - **Measured** (the 23 gate seeds, four days, the workshop player ordering blades): blades worn on 20; fights on 12; a bold resident slew a creature on 7, the first at t14382 (seed 18). One Ore Quarry jitter on seed 8 on day 2, with no fight in it (F4's kind).
    - **Older scenarios given room:** the guild's sign-on within two days (it came at t9405 on the default seed, 93% of the day); the right gear's trip all in respirators within nine days (day 7.8).
- [x] **K3.4 The client:** [Possibly Complex: worn sprites and Gum UI]
  - weapons in hand;
  - the new goods in the merchant terminal and the party builder.
  - **Done with K3.1** (the new goods overflowed both windows at 720 rows): the merchant terminal's goods scroll past nine (the window 24 px wider for the scrollbar), the station window's orders past four.
  - **Weapons in hand:** a weapon is worn in the hand (`Worn.Hand`): bodies (`BodyKit`) and today's figures (`WornSprites`) draw its hilt beside the right hand (the rightmost skin under the head) and its blade rising up and out, three pixels, in its colours. Portraits show none.
  - **The party builder:** the guild's outfit (K1.4's rule, by hazards) gives no weapons yet: weapons for a party come with the Clear order (K7). Nothing to show there.
- **DoD `AnArmedCrawlerFightsBack`:** on a gate seed, the scripted player stocks blades; a bold resident buys one, meets vermin, fights and kills one; the day holds.
  - **Done 2026-11-11** (`04a8d1e`): gate seed 18, the slayer SwarmAgent_11 at t14382 with an iron blade it bought, the days holding.
- **Stop for the producer's look** at the first fights. **Here, 2026-11-11.**

### K4. Loot (2–3 days)
**Complexity:** Possibly Complex: rolls already exist; piles are a new entity with saved state.
- **The go:** the producer's AFK directive (2026-11-11): "Looks good. I will be afk you will be responsible for finishing K4-K5" (K3's look passed). Decisions D22 onward go into the epic for the producer's return; a local log in `.agents/local/epic10-part3_afk-log.md`.
- [x] **K4.1 Piles:** [Possibly Complex: a new entity, saved]
  - items on the ground (A5);
  - they fade after a game day.
  - **The plan:** `EntityType.Pile` and a `PileService` owned by the simulation (as the creatures' is): up to 16 stacks and a purse (for K5's bodies), where it lies, when it fades (a game day on), and who may claim it first until when (its victor, a game hour). Ids from a seeded sequence of their own (as creatures', so drops never shift who comes next). Out of crawlers' spatial memory (as creatures are): cognition finds piles in the snapshot. Saved in the world's block (`PileSave`), checked before a load; the world hash holds them.
  - **Built:** `LootPile` and `PileService` (`Simulation/Loot/`, `SimulationManager.Piles`, ticked after the creatures: fading), `IPileWorld`, `EntityType.Pile`, `SimRandomSalt.PileId`, `PileSave`/`PileRecord` (56 bytes) in the world's block; `SaveItems` vets a pile's stacks, `CheckRestorable` its counts; out of spatial memory. Gold that fades is logged until K5's account for gold on the ground.
  - Tests: `SessionRestoreValidator.ValidatePilesRoundTrip`; `PileTests` (3). Core 222/222, client 500/500, golden 3/3 unchanged (no pile is dropped in a live run yet).
- [x] **K4.2 Drops:** [Possibly Complex: deterministic rolls] each kind's loot table, rolled at the kill by `LootRoller`.
  - **The plan:** the materials as items (hide, chitin) and each kind's table in `CreatureLoot` (`CreatureDefinition.LootTable`): a rat drops a hide, sometimes a herb, now and then a rare piece; a beetle chitin, sometimes ore, now and then a rare piece. Rolled at the kill on (seed, creature, tick) into a pile at its place, claimed for the slayer.
  - **Built:** items 15–20 (hide, chitin, the leather jerkin, the chitin plate, the fang knife, the stoneheart coat: their catalogue rows, base values, looks and words; the armour's recipes and stall terms wait for K4.4); `ItemCategory.Creature` (D22); `CreatureLoot` (D23); `CreatureService` rolls at the kill and `ICreatureWorld.CreatureSlain` drops the pile.
  - **What leaked:** loops over every resource met the new materials. The stall's room check now counts only kinds the stall buys (a material it didn't trade had room, so a full stall still looked open: golden 424242 moved at t2000 until this was fixed). The header's counts and the bounty board's Gather list leave out a creature's materials.
  - Tests: `CreatureLootTests` (3); `FightTests` checks the beetle's pile; the catalogue validators pin the materials and the tables. Core 226/226, client 500/500 in the pinned image (no render moved), golden 3/3 unchanged (no kill in a golden run); the art pins re-recorded for the 6 items (114 sprites) after a look at the sheet.
- [x] **K4.3 Picking up:** [Possibly Complex: cognition, a goal and a fact]
  - the victors take their pile;
  - anyone may claim one left behind.
  - **The plan:** `CollectLootGoal` and `PickUpLootAction` (`StateFact.LootCollected`), a `PickUpIntent`: a pile in sight (the snapshot, within a crawler's vision of 6 and charted), its own or past its claim, where it would go alone (`WouldEnterAt`), with room in its pack; it walks there and takes what fits, the purse too. Then the stall buys what it sells.
  - [x] **K4.3a The roller's pieces come whole:** [Basic] a rolled piece of equipment is a new Standard piece with its full life (`Outfitting.NewPiece`), not a stack of durability 0, which would break at its first wear.
  - [x] **K4.3b Piles in the snapshot:** [Possibly Complex: a new buffer in every snapshot, main thread to cognition] `PileView` (id, place, claim, purse, a 256-bit mask of the items held), at most 64 a snapshot in id order, published as stalls and bounties are (`ISpatialPartition.SetPiles`).
  - [x] **K4.3c What a crawler takes (D24):** [Possibly Complex: a rule the pile service and cognition share] materials and consumables that fit its pack, the purse, and a piece of equipment only when wearing it outfits the taker better for fighting and against every hazard (`OutfitNeed.ForLoot`): it is worn at once, and what it replaces is left in the pile in its place. One rule (`LootRules`), so cognition never walks to a pile it would take nothing from.
  - [x] **K4.3d Cognition:** [Possibly Complex: a goal, an action, a fact, an intent and its handler, as the harvest's] `LootTargeting.TryChoosePile` (in sight and line of sight, charted, its own or past the claim, where it would go alone, holding something it would take; the nearest, then the lowest id); `StateFact.LootCollected = 42`; `CollectLootGoal` (priority 42); `PickUpLootAction`; `PickUpIntent` and its handler (the walk, five game minutes' stoop, `ActionTuning.PickUpSeconds`, the take through `IIntentExecutionContext.TakeFromPile`); the plan's words in the client (`PlanText`); a counter (`Activity.LootTaken`).
  - [x] **K4.3e Tests:** [Basic] mechanism tests on the real outpost: a slayer takes its pile; a stranger waits out the claim; a better piece is worn and a worse one left; a full pack leaves the materials. The live evidence is K4's DoD, once the stall buys the materials (K4.4).
  - **Built:** as planned. Activity counters stay out of the crawler's saved record: the pile service counts units taken and pieces worn (unsaved). `PileTests`' fade test ticks the piles by hand now, since crawlers pick its pile over first.
  - Tests: `PickUpTests` (5): the rule; a kill's piece whole; a full pack; a pile by the stall picked over at t467 (three set out for it); a claimed pile taken by its victor at t507, inside its hour. Core 231/231, client 500/500, golden 3/3 unchanged (no pile in a golden run).
- [x] **K4.4 Materials:** [Possibly Complex: the economy, and the soak may move]
  - hides and chitin at the stall and the caravan;
  - the leather jerkin and chitin plate at the forge;
  - the rare pieces as items.
  - **The plan:** the stall buys hide and chitin, the caravan exports them; the forge makes the leather jerkin (two hides) and the chitin plate (three chitin and an ore); two rare pieces (a rat's fang knife, a beetle's stoneheart coat). The bold who are armed buy armour for a bare slot too (D: the K3 call "residents buy weapons only" widened so a jerkin finds a wearer).
  - [x] **K4.4a The stall's goods (D25):** [Possibly Complex: the stall's record grows, inside format 11] room for 24 goods (16 hold today's 14 and not the 4 new ones), as K2.4 and K4.1 grew format 11 without a raise; the stall buys hide (5g) and chitin (8g) and sells the jerkin (35g) and the plate (70g), all from no stock. The size pins follow.
  - [x] **K4.4b The forge's recipes:** [Basic] the jerkin (two hides, 2 game hours), the plate (three chitin and an ore, 3 hours).
  - [x] **K4.4c The caravan takes the surplus (D25):** [Possibly Complex: a new term in the caravan's market] hide (6g) and chitin (10g), 8 a visit, past a reserve the stall keeps for the forge (`CaravanTerms.Keep`, 6): taken every six hours, the forge would seldom hold two hides at once; never taken, a full shelf would stop the stall buying.
  - [x] **K4.4d Armour for a bare slot (D26):** [Possibly Complex: cognition, and the soak may move] the bold and armed buy the piece that most raises their fighting outfit: a better weapon (K3.3) or armour for an empty head or body slot, never one in place of what they wear (hazard gear stays). `ResourceTargeting.TryChooseArms` replaces `TryChooseWeapon`; the fact and the goal keep their names.
  - [x] **K4.4e The DoD `KillsFeedTheForge`:** [Possibly Complex: a live scenario on a gate seed] the scripted workshop player orders blades, then jerkins; a kill drops hide, a crawler picks it up and sells it to the stall, the forge makes a jerkin from it, and a resident wears it; the days hold, the ledger balances.
  - Then the golden hashes (re-recorded in their own commit: the stall's new goods are in the world hash), the soak and the seed sweeps.
  - **Built:** as planned, with what the measurement found (the gate seeds over ten days, `KillsFeedTheForgeTests`' explicit measurement):
    - nobody sold the materials at base values of 6g and 9g, over the stall's 5g and 8g: the greediest seller asks half again over base. Bases 3g and 5g (D25);
    - victors walked off from their drop a step away, hurt and no longer braving the lair, or blind to it in uncharted ground; and a hurt victor's herb reflex came first. A victor's own drop at hand is taken fog or lair, before the herb (D24);
    - the plate at three chitin was made on 1 seed in ten days; at two chitin, on 6 (D25);
    - the materials' empty shelves kept a full stall looking open to the miners (golden 424242 moved at t2000): they don't count toward its room for salvage (D25).
    - The workshop player takes extra makes (`WorkshopPlayer.Also`): one of each when the stall holds its inputs and none is on the shelf.
  - **The DoD** `KillsFeedTheForge` (`AKillsMaterialsBecomeArmourAResidentWears`): gate seed 18, a chitin plate worn at t33877 after three kills; armour from a kill worn on 5 of the 23 gate seeds in ten days (made on 6).
  - **Found:** F5, older than K4: a thrash by the stall on seeds 8 and 15 over long runs with the workshop player (the same at K3's gate and at K4.3).
  - Tests: core 232/232, golden 3/3 unchanged (its runs end at t3000, before any kill), the soak 15/15, seeds 1–120 120/120 with and without the guild; client 500/500 in the pinned image after re-recording the merchant terminal and the station window (their lists grew), and the art pins (the jerkin's and the plate's graded icons) after a look.
- [ ] **K4.5 The client:** [Possibly Complex: art and renders]
  - piles on the map;
  - rarity's colours in the merchant terminal and the dossier.
- **DoD `KillsFeedTheForge`:** a hunter's kill drops hide; the hide is sold at the stall; the forge makes a jerkin from it; a resident wears it.

### K5. Death and rescue (4–5 days), then the producer's look
**Complexity:** Very Complex: a crawler leaving the world mid-session touches every system that holds one, and carrying couples two crawlers. Split as below. The developer supervises K5.2 and K5.4.
- [ ] **K5.1 Collapse and bleeding out (Q3):** [Possibly Complex: a rule in `Vitality`, saved; Part 1's downed crawlers change]
  - downed in danger, a crawler collapses where it is, and the clock runs;
  - downed in safety, it limps as today;
  - a draught it carries (Q3a) or is given stands it up.
  - The golden hashes are re-recorded in their own commit, since crawlers downed in hazards no longer limp out.
- [ ] **K5.2 Carrying:** [Very Complex, supervised by the developer: two crawlers' movement coupled, and saved] one capability (A7): a rescuer lifts a fallen crawler and carries it at half pace to the nearest safe ground.
- [ ] **K5.3 Rescue (Q3b):** [Possibly Complex: cognition, a goal and a fact, with the altruism evaluator as precedent]
  - a crawler who sees a fallen friend in danger it would itself enter carries it out, or gives it a draught it carries;
  - parties rescue under orders in K7.
- [ ] **K5.4 A death:** [Very Complex, supervised by the developer: groups and succession, contracts, bunks, parties and expeditions, memory, the ledger]
  - a death goes the drifter's way (A2);
  - the body is a pile with the purse in it, held in a ledger account for gold on the ground;
  - a dead leader is succeeded.
- [ ] **K5.5 The living remember (Q4):** [Possibly Complex: memory events, affinity and mood]
  - grief;
  - trust toward the patron after a death under orders;
  - the chronicle;
  - the departed list's cause.
- [ ] **K5.6 A newcomer:** [Possibly Complex: a population rule on the seed] 2 days after a resident's death.
- [ ] **K5.7 Saves:** [Possibly Complex: format] the dead, the bleeding clock, a crawler being carried and gold on the ground, with the round-trip validators.
- [ ] **K5.8 The client:** [Possibly Complex: art, renders, a dynamic scene]
  - a crawler carried on a rescuer's back;
  - the body and a cairn;
  - the chronicle's lines;
  - the dossier of the dead (portrait greyed);
  - a dynamic scene when a friend sees a death or makes a rescue.
- **DoD:**
  - **`AFriendCarriesTheFallenOut`:** a resident downed in a hazard is carried out by a friend and lives. Where nobody comes, one dies.
  - **`DeathHasItsCost`:** the scripted player sends an under-armed party against beetles. A member falls and isn't rescued, and it dies. Its friends grieve, the guild loses its contract, the body's pile holds its gear and purse, and a newcomer arrives. The ledger balances.
  - **`TheCarefulLoseFew`:** on the 23 gate seeds, with no player action, deaths stay within a band set with the producer.
- **Stop for the producer's look** at death and rescue.

### K6. Hunting (2–3 days)
**Complexity:** Possibly Complex: a new bounty kind with two precedents, Scout and Gather.
- [ ] **K6.1 Hunt bounties (Q7):** [Possibly Complex: a command, escrow, saved]
  - posted on a charted lair;
  - claimed by up to three;
  - paid by damage dealt when the lair is empty.
- [ ] **K6.2 The hunters:** [Possibly Complex: cognition, a goal, an action and facts] claim, go, fight in the lair, and are paid.
- [ ] **K6.3 The client:** [Possibly Complex: Gum UI] the bounty board's Hunt, with charted lairs as targets.
- **DoD `AHuntBountyClearsALair`:** the scripted player posts a hunt on the rats; bold, armed residents claim it, clear the lair and are paid. The ledger balances.

### K7. The guild goes to war (2–3 days)
**Complexity:** Possibly Complex overall: G5's stages and K5's carrying are the precedents. K7.2 is Very Complex, supervised by the developer.
- [ ] **K7.1 The Clear order (Q8):** [Possibly Complex: a new expedition kind on G5's stages]
  - a lair as the target;
  - the fight stage;
  - the haul as what was picked up.
- [ ] **K7.2 The party's fight:** [Very Complex, supervised by the developer]
  - focus fire through the directive;
  - the retreat rule;
  - rescue under orders through K5's carrying.
- [ ] **K7.3 The client:** [Possibly Complex: Gum UI]
  - the Clear order in the party builder;
  - the fight in the tracker: the party's health and its foe's.
- **DoD `AnArmedPartyClearsANest`:** the scripted player arms four and orders a clearing. They fight together, clear the nest, carry anyone fallen out and bring back loot. The ledger balances.

### K8. Ranged combat (3–4 days), then the producer's look
**Complexity:** Very Complex: a second mode of combat with line of sight and positioning. Split as below. The developer supervises K8.2.
- [ ] **K8.1 Ranged weapons (Q15, Q16):** [Possibly Complex: items on K1's table, with range in the combat rule]
  - the sling and the crossbow, their recipes and terms;
  - wear per shot;
  - the hit roll's fall with distance.
- [ ] **K8.2 Line and distance:** [Very Complex, supervised by the developer: a ray over the nav grid in the hot path, and a new movement rule]
  - a clear line before a shot;
  - a ranged fighter keeps its distance;
  - in a party, ranged members stand behind the melee.
- [ ] **K8.3 Ranged creatures:** [Possibly Complex: a kind on K0.4's catalogue] the cave spinners spit, for K10's approach.
- [ ] **K8.4 The client:** [Possibly Complex: art and renders]
  - shots drawn between ticks from the frames (presentation only);
  - the ranged goods in the merchant terminal and the party builder.
- **DoD `ArchersHoldTheLine`:** the scripted player arms a party of two blades and two crossbows and orders a clearing of the beetles. The crossbows shoot from behind and never stand in reach. The lair is cleared, and the ledger balances.
- **Stop for the producer's look** at ranged fights.

### K9. The skill system (3–4 days)
**Complexity:** Very Complex: a new system read by combat, harvest, crafting and rescue (bundling). Split as below. The developer supervises K9.3.
- [ ] **K9.1 Skills as a table, on each crawler (Q17, Q19):** [Possibly Complex: a catalogue and saved state]
  - `Skills`, appendable like the slots, with room for 16 on each crawler;
  - starting skills drawn at spawn on their own salt;
  - saved, with the round-trip validators.
  - The golden hashes are re-recorded in their own commit, since every seed draws skills.
- [ ] **K9.2 Practice (Q18):** [Possibly Complex: behaviour] experience from swings, shots, units mined and foraged, makes and rescues.
- [ ] **K9.3 What skills do (Q20):** [Very Complex, supervised by the developer: combat, harvest, crafting and rescue each read skills; W4's practice is folded in]
  - **K9.3a combat:** proficiency in the combat rule (A1), from melee or ranged;
  - **K9.3b work:** harvest speed from mining and foraging;
  - **K9.3c making:** a make's grade and speed from smithing and alchemy, with W4's practice migrated into them;
  - **K9.3d medicine:** carrying faster, and draughts given heal more.
- [ ] **K9.4 Choices (Q21):** [Possibly Complex: cognition] skill raises what work pays an hour; recruits and the roster show skills.
- [ ] **K9.5 The client:** [Possibly Complex: Gum UI]
  - skills in the dossier and the roster;
  - a band reached told in the chronicle.
- **DoD `PracticeMakesAVeteran`:** over three game days on the gate seeds:
  - a guild member's melee rises through fights;
  - a miner gets faster;
  - an artisan's smithing raises its grade;
  - the day's checks hold.

### K10. Bosses and the gate (3–4 days)
**Complexity:** Very Complex: boss mechanics are new. Split as below. The developer supervises K10.3.
- [ ] **K10.1 The Old Stair:** [Possibly Complex: a map change, with golden and renders re-recorded]
  - the new sector at the floor's edge, uncharted at the start;
  - the sealed gate;
  - the spinners' lair on its approach.
- [ ] **K10.2 The Matriarch (Q13):** [Possibly Complex: a kind with boss traits]
  - she calls her brood to her;
  - she returns after a week.
- [ ] **K10.3 The Gatekeeper:** [Very Complex, supervised by the developer: the slam, the fury, healing between attempts, beaten once, saved]
- [ ] **K10.4 The breach:** [Possibly Complex]
  - a saved world flag;
  - the chronicle;
  - the stairs beyond, dark.
- [ ] **K10.5 The client:** [Possibly Complex: art, renders, a hard cutscene]
  - boss sprites (up to 64×64) and health bars;
  - the gate on the map;
  - the breach's cutscene.
- **DoD `TheGuildBreaksTheGatekeeper`:** on the default seed the scripted player scouts the Old Stair, arms and kits the guild, and breaks the Gatekeeper. More than one attempt is allowed; the bound is set at K10's first measurement.

### K11. The gate (2–3 days)
**Complexity:** Possibly Complex: the whole part's scenarios and bands; the close-out is Basic.
- [ ] **K11.1 Every scenario together,** [Possibly Complex] with a save mid-fight, a bleeding crawler and a crawler being carried, each loaded byte for byte.
- [ ] **K11.2 The soak's `hunting` profile:** [Possibly Complex: bands set with the producer] treasuries, kills, rescues and deaths per day.
- [ ] **K11.3 Seeds 1–120,** [Basic to run; any failure it finds is graded on its own] with and without the guild.
- [ ] **K11.4 The producer's play session.** [Possibly Complex: grade each fix it asks for]
- [ ] **K11.5 Part 3's close-out:** [Basic]
  - the roadmap: Part 3 COMPLETED, and Milestone 4 COMPLETED;
  - the epic doc, the focus file, `architecture_history.md`, the economy model and AGENTS.md;
  - this plan deleted.

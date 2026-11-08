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
- [ ] **K1.3 Stats and wear over every slot:** [Possibly Complex: behaviour; the golden hashes may move]
  - `CrawlerStats` sums every worn slot, with each stat's stacking rule (add, multiply, a cap);
  - each piece wears by its own rule.
  - The golden hashes are re-recorded in their own commit if the hard hat and hazard gear, now worn together, change behaviour.
- [ ] **K1.4 Outfitting by slot:** [Possibly Complex: cognition and the party builder]
  - residents' purchases and the party builder pick the best piece for each slot for the target's dangers, scored by stats, not by named items or groups;
  - W3's outfitting by hazard kind becomes a case of it.
- [ ] **K1.5 The client from the table:** [Possibly Complex: Gum UI and worn sprites]
  - the dossier's and the party builder's equipment panels, laid out from the slot table;
  - worn sprites layered by each slot's draw layer.
- [ ] **K1.6 The proof and the recipe:** [Basic: a test like `ItemSystemProofTests`, and a section of AGENTS.md]
  - a test adds a feet slot and boots to a test catalogue, and shows that stats, outfitting, the save's round trip and the dossier follow;
  - AGENTS.md gains "Adding an equipment slot".
- **DoD `GearByTheSlot`:** on the gate seeds, a venturer wears a hard hat and hazard gear together. The guild's and the workshops' scenarios hold.

### K2. Creatures (3–4 days)
**Complexity:** Very Complex: a new system. Split as below. The developer supervises K2.2 and K2.3.
- [ ] **K2.1 Lairs:** [Possibly Complex: a map change, with golden and renders re-recorded]
  - lairs as map data in the Overgrowth and the Deep Seam;
  - spawns on the seed;
  - refilling on the day's time.
- [ ] **K2.2 The creature's rule:** [Very Complex, supervised by the developer: the creature's tick, its own phase in id order (A4)]
  - rest, roam, chase within the leash, strike, flee, go home, heal;
  - its paths through the existing pathfinder;
  - swings resolved in one pass after movement.
- [ ] **K2.3 Crawlers meet creatures:** [Very Complex, supervised by the developer: a new reflex in the runtime's pre-emption, and cognition]
  - flight, a reflex beside retreat and panic;
  - known lairs judged in `LoneDanger`, as hazards are;
  - a scout's report charts lairs;
  - nobody yet fights back, and nobody dies: until K5 a downed crawler limps out, as today.
- [ ] **K2.4 Saves:** [Possibly Complex: a save block] creatures and lairs in the world's block, with the round-trip validators.
- [ ] **K2.5 The client:** [Possibly Complex: new art and renders to look at]
  - `CreatureSprites` (generated placeholders);
  - art rows per view mode, and `EntityVisuals`;
  - a strike pose;
  - the red coat on creatures too;
  - the fog hides them;
  - the inspector shows a creature.
- **DoD `VerminDriveTheLoneAway`:** on the 23 gate seeds' day:
  - a crawler who strays into a lair's reach is hurt and flees;
  - nobody enters a known lair alone;
  - the day's checks hold.

### K3. Melee arms and armour (2–3 days), then the producer's look
**Complexity:** Possibly Complex: items on K1's table, and one new reflex.
- [ ] **K3.1 Melee weapons (Q10):** [Possibly Complex: a stat read in combat]
  - the cudgel, the iron blade and the glowstone edge;
  - their recipes, and the caravan's terms;
  - wear per hit; grades and repair.
- [ ] **K3.2 Armour from ore (Q11):** [Basic: item rows and recipes, by AGENTS.md's "Adding an item"] the iron helm and the ring coat.
- [ ] **K3.3 Fighting back:** [Possibly Complex: cognition, a fact and a reflex]
  - the bold and armed fight back;
  - the bold buy a weapon (Q6);
  - outfitting for creatures comes from K1.4's rule.
- [ ] **K3.4 The client:** [Possibly Complex: worn sprites and Gum UI]
  - weapons in hand;
  - the new goods in the merchant terminal and the party builder.
- **DoD `AnArmedCrawlerFightsBack`:** on a gate seed, the scripted player stocks blades; a bold resident buys one, meets vermin, fights and kills one; the day holds.
- **Stop for the producer's look** at the first fights.

### K4. Loot (2–3 days)
**Complexity:** Possibly Complex: rolls already exist; piles are a new entity with saved state.
- [ ] **K4.1 Piles:** [Possibly Complex: a new entity, saved]
  - items on the ground (A5);
  - they fade after a game day.
- [ ] **K4.2 Drops:** [Possibly Complex: deterministic rolls] each kind's loot table, rolled at the kill by `LootRoller`.
- [ ] **K4.3 Picking up:** [Possibly Complex: cognition, a goal and a fact]
  - the victors take their pile;
  - anyone may claim one left behind.
- [ ] **K4.4 Materials:** [Possibly Complex: the economy, and the soak may move]
  - hides and chitin at the stall and the caravan;
  - the leather jerkin and chitin plate at the forge;
  - the rare pieces as items.
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

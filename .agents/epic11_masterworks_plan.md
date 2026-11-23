# Epic 11 plan 4: Masterworks and the gate (S8–S9)

Working document (AGENTS.md, Documentation Protocol 6), the last of Epic 11's plans ([index](epic11_plan.md)). Written 2026-11-22 at the producer's "Write up the implementation plan for Masterworks and proceed on work once it's ready", which is also **the go**. The design is the epic, [`docs/epics/epic-11.md`](../docs/epics/epic-11.md) (§2.3's rules A1–A10 bind every step); its questions stand by exception, Q14 (masterworks) most of all. The decisions below (D38–D48) are this plan's defaults; each moves to the epic's §0 when its step lands. Plan 3's numbers are tuned on, from `docs/skills-model.md` §5.13.

- **Complexity:** Very Complex as a whole: S8 carries unique item instances through the item system, the stall, the piles and the save. Split below so every step is Basic or Possibly Complex. The epic stamps S8 "supervised": its two steps inside the item flow and the save (S8.1, S8.3) are flagged for the developer, and, done after the go while the producer is away, listed for their review as S7.1–S7.3 were.
- **Branch:** `feat/epic11-masterworks`, cut from `feat/epic11-legend` at `7f6be32`. **A deviation from the index:** plans are drafted when the one before merges, and plan 3 hasn't (its push was refused by the permission check), but the producer asked for this one now. Its PR goes into `main` only after plan 3's merges, so no PR is stacked. If plan 3 changes in review, its new commits are merged into this branch, never rebased.
- **Estimate:** 5–8 agent days (S8 3–5, S9 1–2, close-out 1).
- **The producer's stops:**
  - **the go:** given 2026-11-22 ("proceed on work once it's ready");
  - **the play session at S9:** Epic 11 played through; S8's look folds into it (the epic lists no look at S8);
  - **the merges:** plan 3's PR first, then this one's. The epic is COMPLETED only on the play session's pass.
- **The DoDs:** `AMasterpieceHasAMaker` (S8) and `EpicElevenTogether` (S9), live on the real outpost with nothing injected.
- **Save format:** 14 once, at S8.1: the item instance's id field becomes a masterpiece's mark, and the workshops' block gains the masterpieces' records. The PR's one raise.

## 0. What plan 3 hands over (checked at S8.0)
- **An item instance** (`ItemInstance`, 16 bytes, saved raw in every pack, worn slot and pile): `ItemId` (a `ushort` stamped 1, or the item type's number at the harvest, the guild's provisions and a trade's roll-back: AGENTS.md's quirk 4), the type, `Quality` (the grade), quantity, base value, durability, and six bytes of padding. The world hash doesn't mix `ItemId`; the instance's equality does. 178 constructions pass an id, 11 of them in production code.
- **Grades** (Epic 10 Part 2, W4; K9): Standard, Fine and Masterwork, by the artisan's band through the seam (`WorkshopService.GradeOf`, a Journeyman's make Masterwork, techniques' grade counted: Fine Temper, Masterstroke). A grade is a tenth more effect and a quarter more life (`EquipmentBlock.EffectPerGrade`, `LifePerGrade`); its rim a step above the item's rarity per grade, to Legendary (`Grades.RimOf`). Only worn things take a grade: masterworks come from the forge's worn makes.
- **The stall's shelf** (`StallGood`): counts per good: stock, Fine, Masterwork and worn pieces. A sale (`TradeIntentHandler`) and the guild's gift and outfit (`GuildService.Expeditions`) take the best grade first (`TakeBest`); a piece handed back (the guild's swap, a trade-in) goes onto the worn shelf if the forge can mend it (`Crafting.HandBack`), and a repair comes back Standard. Cognition appraises a stall piece at the stall's best grade (`Outfitting.NewPiece`).
- **A make** (`WorkshopService.Work`): `Crafting.Work` at the artisan's grade and pace; at a finished make, practice by the recipe's challenge and the `Make` deed; the output held at the station (`HeldOutput`, `HeldGrade`) while the stall has no room.
- **Piles** carry instances whole: a body (`SimulationManager.Die`: its pack and gear), a kill's drop; `PileService.Take` wears what the taker would wear (`TryWear`), leaving the old piece in the pile; a pile's fade takes what is left.
- **Wear:** `AgentInventory.WearBy`: a piece worn down breaks and is gone.
- **Kills** are credited in `SimulationManager` (`CreatureSlain`, `BossSlain`, `NamedCreatureSlain`), with the killer in hand.
- **Counts:** deeds 9 of 16; chronicle events to 59; salts to 18; planner facts 48 of 64; save format 13.
- **Names:** the identity service names crawlers and named creatures; the departed list keeps the dead's names, causes and ticks.
- **Growth** (`docs/skills-model.md` §5.6, §5.13): smithing's Journeyman bands come mostly from the forge's hands. **Measured at S8.0** (§5.14): with the hunting players the forge makes Masterwork pieces on 14 of 23 gate seeds in two weeks (193 in all, by 18 smiths) and on 21 in four; the crafting player orders few worn pieces, so it makes them on 4 seeds in two weeks. The rest of this section matched the code.

## 1. Decisions proposed (defaults; the producer answers by exception)
- **D38, masterpieces: a named piece, a grade above Masterwork** (Q14):
  - **The words:** "masterwork" stays the grade a Journeyman makes, as today. The unique piece with a name and a maker is a **masterpiece**, so code and screen keep the two apart.
  - **Its grade:** `Grades.Masterpiece` (3): a tenth more effect and a quarter more life again, by the equipment's existing rule. Its rim is always Legendary's (a step a grade from a common item's would stop at Epic). Nothing else about its numbers is new.
- **D39, the instance's mark; the quirk retired** (Q14: "retiring the kept `ItemId` quirk"):
  - **The field:** `ItemInstance.ItemId` becomes `Masterpiece`, a `ushort`: 0 on every ordinary piece, a masterpiece's id on one. The constructor loses its first argument (all 178 sites; mechanical, checked by the compiler); a piece is marked with `WithMasterpiece(id)`.
  - **What stays:** the instance's size (16 bytes) and its stacking (a worn piece stacks to one).
  - **The hash** mixes a worn piece's mark only when it is set, so golden holds until a masterpiece exists.
  - **The save:** format 14. A save holding a marked piece with no record is refused (`SaveItems`).
- **D40, its record: provenance** (Q14):
  - **The record:** `MasterpieceRecord`, unmanaged, its size pinned:
    - its id and its item;
    - its name's parts (form, adjective, noun) and whom it is named for;
    - its maker, and the station and tick it was made at;
    - its kills, and the named creatures slain with it (a count and the last one);
    - its owners (the first and the last three, with a count).
  - **The owner:** `MasterpieceService`, the workshops' (`WorkshopService.Masterpieces`), saved in their block sorted by id. Main thread only; views read it through the dashboard's context.
  - **Where it is now is found, not tracked:** on the shelf (the service's own list), worn or carried by a crawler, or in a pile; found nowhere, it is lost (broken, gone with a drifter, taken by the floor). Nothing can go stale.
- **D41, inspiration** (§2.1's "now and then a Journeyman makes a masterwork with a name and a maker"):
  - **When:** a finished make of a worn piece at Masterwork grade (a Journeyman or better at it, techniques counted). A repair never.
  - **The roll:** on its own salt (`SimRandomSalt.Inspiration`, 19), keyed by the maker and the tick: **8% a Masterwork make**, set at S8.0 (§5.14) for a masterpiece on most seeds where a Journeyman smith works for two weeks (about 9 of 14 with the hunting players), about one a smith.
  - **What raises the chance:** a keen passion for the work (×1.5), a burning one (×2: the passions' practice multipliers), and grief (×2): a friend's death within two game days in the maker's own memory (the `Grieved` event Epic 10's K5.5 records for each friend at the death line, saved with the crawler's memory), so no new state is needed.
  - **The limit:** one masterpiece a game day per maker, read from its newest record.
  - **What follows:** the piece is stocked at grade 3 with its mark, onto the service's shelf; the maker counts a deed (`Deed.MasterpieceMade`, 10); the chronicle tells it (`MasterpieceMade`, 60).
- **D42, its name:**
  - **The table:** `MasterpieceNames`: adjectives and nouns by the piece's kind of slot and reach (a blade's, a bow's, a tool's, a coat's, a helm's), drawn on the inspiration's stream.
  - **The forms:** "the {adjective} {noun}" ("the Quiet Fang"). Grieving, the piece takes the dead friend's name: "{friend}'s {noun}" ("Ama's Oath"), the smith "whose blade carries a dead friend's name" (§1).
  - **The rules:** Latin-1 only, sized to fit the dossier's gear line. Words are rows, never reordered: the record holds their indices.
- **D43, it passes through hands:**
  - **The shelf:** a stocked masterpiece counts among its good's Masterwork pieces, and the service keeps it on its shelf. A sale or a gift that takes a Masterwork piece hands out a masterpiece first, at grade 3 with its mark (`MasterpieceService.TakeFromShelf`, at the two sites that call `TakeBest`).
  - **Handed back** (a swap, a trade-in), a masterpiece goes back onto the shelf whole, never onto the worn shelf: a repair would make it Standard.
  - **On the ground:** a body or a drop keeps its mark, and a pickup that wears it makes a new owner (`PileService.Take`).
  - **Owners** are recorded at every acquisition: a sale, a gift, a pickup.
  - **Cognition** appraises a stall piece at the stall's best grade, so a masterpiece is valued as a masterwork until it is worn. No new fact (A10).
- **D44, its deeds:** a kill by a crawler wearing a masterpiece counts on the piece (every masterpiece it wears: a slot's kind is named only in the slot table), and a named creature slain with it is kept by whom it was named for and its rank. Its story reads them. No epithet: the Maker (40 makes) stays.
- **D45, the client:**
  - **Its story** in the tooltip wherever a worn piece shows (the dossier's gear line, the guild's card): "The Quiet Fang, a masterpiece. Made by Tamsin at the forge on day 6, for Ama. Worn by Bram, its second owner. 4 kills, Ama's Bane among them."
  - **On the shelf:** the stall's row for its good names the masterpiece in its stock words ("masterpiece the Quiet Fang by Tamsin"; the rows' words carry no tip).
  - **Its colour:** its name in Legendary's colour; its icon is the Masterwork's (a masterpiece icon would be new art, for a later look).
  - **The guild's card** shows it by its name.
  - **The maker:** the dossier's legend line names what it made ("made the Quiet Fang").
  - **The chronicle and a moment:** the line, and the inspiration scene for the patron's people (`SkillScenes`).
  - **F16 fixed** with it: the dossier's 6-pixel overrun at 720 rows, since S8 touches the dossier.
- **D46, every scenario together** (S9, the epic's §3): one run on a gate seed with every player (the guild's clears, the workshop, the hunt, the teachers) asserts each layer's outcome: a passion's band, a technique picked, a calling, an epithet, a scar, a named creature, a masterpiece. A save mid-scene, with a named creature alive and a masterpiece worn, loads byte for byte and plays on.
- **D47, the soak's bands re-measured** with passions, upkeep, techniques, legend and masterpieces, 20 seeds a profile. A band moves only by the producer's word; until then the run is judged against today's.
- **D48, the play session:** a brief in the epic's §4 that walks through every layer, on a seed and with steps that show each one.

## 2. The milestones, phased

### S8. Masterworks (3–5 days)
**Complexity:** Very Complex as the epic stamps it (the item system and the save). Split so the mark and its save, the rule, the flow through hands and the client land and are checked alone. S8.1 and S8.3 are supervised (for review).
- [x] **S8.0 The hand-over checked, and the masterwork makes measured** (done 2026-11-22: §0 matched the code but for the growth line, corrected; `docs/skills-model.md` §5.14; D41's chance 8%) [Possibly Complex: measuring]
  - Check §0 against the code; a difference becomes a finding (F18 onward).
  - **The measurement:** finished makes by grade, by station and by the artisan's band, over 14 and 28 days on the gate seeds, with the hunting players and with the crafting profile, from a copied Release bin. Written to `docs/skills-model.md` §5.14.
  - **Set from it:** D41's chance.
- [x] **S8.1 The mark and the record; format 14** (done 2026-11-22: `3c4d178` the quirk retired, `d63799c` the record, the service and its block; golden unchanged) [Possibly Complex: the save format; supervised, for review]
  - **Two commits, one raise.** First the quirk retired: the field renamed, the constructor's first argument gone, `WithMasterpiece`, the tests' id tags moved to durability or value, and format 14. Then the record, the service and its block.
  - **Grades:** `Grades.Masterpiece`, its name and its rim.
  - **The checks:**
    - `EconomySerializationValidator` pins the instance (16) and the record;
    - `SessionRestoreValidator`: a round trip with a masterpiece worn, one on the shelf and one in a pile;
    - `SaveItems` refuses an unknown mark;
    - `MasterpieceValidator`: the record, the service's capture order.
  - Golden unchanged (no masterpiece yet; the mark not hashed while 0).
- [x] **S8.2 Inspiration and its name** (done 2026-11-22; golden unchanged: no forge works in its 3,000 ticks) [Possibly Complex: a behaviour change; a new salt]
  - **The rule:** `InspirationRules` (pure: grade, passion, grief, the day's limit), called at `WorkshopService.Work`'s finished make. **The name:** `MasterpieceNames`.
  - **What follows:** the record, the marked piece stocked (or held), the deed by its recipe, the chronicle's line.
  - **The checks:**
    - `InspirationValidator`: the odds by passion and grief, determinism on the salt, no inspiration below Masterwork or on a repair, the day's limit, a grieving name;
    - `MasterpieceNamesValidator`: Latin-1, the lengths, every kind of slot covered;
    - `DeedsValidator` and `DeedNamesTests` list the new deed.
  - Golden re-recorded in its own commit only if a masterpiece is made within its 3,000 ticks.
- [x] **S8.3 Through every hand** (done 2026-11-22; golden unchanged) [Possibly Complex: the item flow across the stall, the guild and the piles; supervised, for review]
  - **The paths:** the shelf at the sale and the gift; handed back whole; the pickup's new owner; the kill counted on the piece; `Find`.
  - **The check:** `MasterpieceFlowTests` (on the live outpost, so in the core tests) follows one piece: made, shelved, sold, worn, handed back, given, its wearer's death, its body picked over, worn again. It checks the owners in order, a kill counted, broken and lost, the shelf never above its good's Masterwork count, and a load keeping every record and mark.
- [x] **S8.4 Re-measured** (done 2026-11-22: `docs/skills-model.md` §5.14; no DoD moved) [Possibly Complex: measuring]
  - The 23 gate seeds' day, seeds 1–120 both ways, the soak, and the S8.0 measurement again with masterpieces (§5.14).
  - A moved DoD is re-picked in its own commit.
- [x] **S8.5 The client** (done 2026-11-22; F16 fixed: the four 720-row dossier renders looked at and re-recorded) [Possibly Complex: Gum UI and renders, a look]
  - **What is built:** D45: the story's tooltip, the shelf's, the rim, the legend line, the chronicle, the moment, and F16's fix.
  - **The checks:** `MasterpiecesOnScreenTests`; the art sheet looked at (the rim is the kit's), the renders looked at and re-recorded in the pinned image, in their own commit.
- [x] **S8.6 DoD `AMasterpieceHasAMaker`** (done 2026-11-22: gate seed 424242; made on 11 of 23, worn on 8) [Possibly Complex: a live scenario]
  - **The scenario:** on a gate seed picked from S8.0's measurement, the workshop player's forge with its best smith, within a bounded run:
    - an inspired Journeyman makes a masterpiece;
    - a resident wears it, bought or given;
    - a save and a load mid-run keep its record, its mark on its wearer, its owners and its maker's name.
  - **The explicit measure:** how many gate seeds make one.
- [x] **S8.7 S8's gate** (done 2026-11-22: core 339, client 553 pinned, golden 3/3, compiles clean) [Basic]: AGENTS.md's gates (§4 below).

### S9. The gate (1–2 days), then the play session
**Complexity:** Possibly Complex, as the epic stamps it.
- [x] **S9.1 DoD `EpicElevenTogether`, with a save mid-scene** (done 2026-11-22: gate seed 5, every layer by day 7, the only one of 23; F19) [Possibly Complex: a live scenario across every system]: D46, as `PartThreeTogetherTests` does for Part 3.
- [x] **S9.2 The soak's bands and the seeds** (done 2026-11-22: S8.4's run on the final simulation code, every profile within its band; §5.15, economy-model §7) [Possibly Complex: measuring; a band moves only by the producer's word]: D47, written to `docs/skills-model.md` §5.15 and `docs/economy-model.md` §7. Seeds 1–120 both ways.
- [ ] **S9.3 The epic's gate and the play session's brief** [Basic]: the gates on the final build; D48's brief in the epic's §4.

### C. The close-out
- [ ] **C.1 Epic 11's numbers at the close** [Basic: a measurement already written]: 28 days on the gate seeds with every layer (§5.16).
- [ ] **C.2 The close-out** [Basic: a close-out from the lists here]
  - **The docs:** the roadmap, the epic, the focus file, `architecture_history.md`, AGENTS.md (masterpieces in the items' notes; a recipe, "Adding a masterpiece word"; quirk 4 struck off), the skills model.
  - **Findings** go to the epic's §7.
  - **Plans:** this plan and the index are deleted.
  - **The PR** into `main` after plan 3's merge, on the producer's word.
  - **The epic's status:** COMPLETED only at the play session's pass; until then, ACTIVE with every plan done.

## 3. Risks
| Risk | What we do |
| :--- | :--- |
| Masterpieces are rare: Journeyman smiths on about a third of the gate seeds in two weeks. | S8.0 measures before the chance is set; the DoD's seed and length come from it; the explicit measure reports the rest. |
| The shelf's list and the stall's counts drift apart. | One door (`TakeFromShelf`) at both take sites; the validator holds the list within the counts; a take that finds no Masterwork count hands out a plain piece. |
| Renaming the instance's id touches 178 sites in three projects. | One mechanical commit, the compiler checking every site, behaviour unchanged (the hash doesn't read it). |
| A masterpiece worn down vanishes, and its story with it. | The record outlives the piece: found nowhere, it is "lost", and its story still reads. |
| The dossier is already 6 pixels over at 720 rows (F16). | The story goes in a tooltip, not a line, and F16 is fixed in S8.5. |

## 4. Gates (every step that changes code)
- `dotnet build dotnet/Simpiens.slnx`, then `dotnet test dotnet/Simpiens.slnx`; commit messages give only the counts that ran.
- `golden check` 3/3; a re-record only for a deliberate change, in its own commit naming the cause.
- `scripts/test-client-in-container.sh`, renders byte for byte; changed renders looked at, then re-recorded there, in their own commit.
- The MonoGame and Android compiles with 0 warnings.
- The 23 gate seeds' day and the soak; seeds 1–120 with and without the guild at each milestone's end.
- No source edited while a source-scanning test runs; measurements from a copied Release bin.

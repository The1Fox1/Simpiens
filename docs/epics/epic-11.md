# Epic 11: Skills, the Heart and Engine of a Crawler

- **Status**: ACTIVE (2026-11-16). The producer gave the go for plan 1: "Economy flukes are tolerable until they completely ruin our testing scenarios. For now proceed with epic 11 Craft". Plan 1, Craft (S0–S3), is under way on `feat/epic11-craft`: S0–S3 done on 2026-11-17, at the producer's "Complete S2-S3 I will review afterwards"; the review (with S1's look), then the close-out (C). QUEUED from 2026-11-15, when the producer asked for it: "create a new Epic focused on the Skill system heart and engine". It follows Epic 10, which ends with Part 3. Its questions (§0) are defaults that stand by exception.
- **Plans** (2026-11-16, at the producer's "Prepare to work on Epic 11. Decide how many implementation plans you will need ... and have the first one drafted and ready to start"):
  - **Four,** one PR each, along the epic's layers: Craft (S0–S3), Technique and Calling (S4–S5), Legend (S6–S7), Masterworks and the gate (S8–S9). Why four, the order and the shared rules: `.agents/epic11_plan.md`.
  - **The first, Craft,** is drafted and ready to start: `.agents/epic11_craft_plan.md` (decisions D1–D12 as defaults, every step stamped). Under way since the producer's go (2026-11-16).
  - **Each later plan** is drafted when the one before it merges, from what that plan measured.
- **Track**: Track A: Core Simulation & Cognition Engine. Its UI is the KNI client's and is built here, as Epic 10 folded in UI-3 to UI-5.
- **Milestone**: Production Milestone 5, Skills and Careers (ROADMAP §3; Q2).
- **Source**: the research paper [Simpiens Skill System: Research and Long-Term Vision](https://claude.ai/artifact/5mqWWaE3F87kz45EQXuYbR) (Claude Doc, 2026-11-15). This draft starts from its five layers and its questions SQ1–SQ12, and replaces its staging (S1–S4) with the milestones in §2.4.
- **The numbers:** [docs/skills-model.md](../skills-model.md), the skills' rules and measurements, kept up to date as each milestone lands.
- **Preceding**: [Epic 10 Part 3](epic-10-part-3.md), whose K9 laid the skill foundation and K9.8 its seam. **Builds on**:
  - K9: seven skills as a table, practice, levels 0–10 in four bands, what a level does;
  - K9.8: `SkillWork`, the effective level (`CrawlerSkills.Level`) and `PracticeSource`;
  - the equipment table (K1), creatures and lairs (K2), death and rescue (K5), hunts (K6), the guild's Clear order (K7);
  - Epic UI-Beta's portraits and dossier, and the chronicle.
- **Following**: Epic 12, the descent (Floor 2 and moving the outpost; Milestone 6), not drafted.
- **Complexity** ([rubric](../../.agents/rules/complexity-grading.md)): **Very Complex** as an epic: its size, systems with no precedent (techniques, callings, named creatures, unique items) and saved state across core and client. Each milestone is stamped in §2.4. The plan splits the Very Complex ones and names the steps the developer supervises.

---

## 0. Producer intent and decisions

**The request (2026-11-15):**
> I want you to create a new Epic focused on the Skill system heart and engine. [...] After Epic 10 we will move onto Epic 11 which will be re written as the skills epic.

**The research brief behind it (2026-11-15):**
> As an RPG systems skills are often the cornerstone of gameplay [...] most importantly when done right they are just plain fun. [...] Assume that the game will lean towards deep rather than simple. You should be thinking beyond raw mechanics and truly think about what would be exciting for a player to see and experience.

**The Nemesis system (2026-11-15):** the producer moved it from "avoid" to "emulate": "its a great system we can emulate while still adhering to the patent." Every claim of US10926179B2 needs an avatar the player operates; Simpiens has none (Q13).

**What "heart and engine" means here.** K9 gave every crawler numbers. This epic gives it:
- a nature: passions beside its talents;
- choices: a technique at each band;
- an identity: a calling;
- a history: its legend, and the creatures that remember it.

The engine is what carries them: tables, one saved record of who a crawler has become, practice sources and K9.8's seam.

**The research paper's layers, and where each lands:**

| Layer | Whose hand | In this epic | Later |
| :--- | :--- | :--- | :--- |
| **Craft** | The crawler's, by doing | Passions, practice by challenge, the mastery budget, upkeep, teachers | Rust, only if specialists fail to emerge (Q5) |
| **Technique** | The crawler's, by nature; the patron sponsors | All of it: a choice at each band, sponsorship | — |
| **Calling** | Emerges from bands and techniques | Titles, party roles, capstones, Leadership | Ambitions; retirement into staff |
| **Doctrine** | The patron's | — | The guild's web, after rival guilds and the descent |
| **Legend** | The world's | Deeds, epithets, scars, earned traits, named creatures, masterworks | The hall's wall of the dead and the retired |

### The questions
Every row is a default that stands by exception.

| # | Question | Default | Alternative |
| :-: | :--- | :--- | :--- |
| Q1 | **Scope** | Craft, Technique, Calling and Legend, as the table above. Doctrine and careers wait for rival guilds and the descent. | Doctrine now too: it needs renown and rivals, which don't exist yet |
| Q2 | **Home and milestone** | Milestone 5, "Skills and Careers", after Part 3 closes Milestone 4. The descent (Epic 10 Part 4 until 2026-11-15) becomes Epic 12, in Milestone 6. | Epic 11 between milestones, as UI-Beta was, with the descent staying Milestone 5 |
| Q3 | **Who picks a technique** | The crawler, by its nature, on its own seed. The patron may sponsor another for gold, and trust and nature decide whether it takes it. | The patron picks for guild members; residents pick for themselves |
| Q4 | **Levels** | Keep 0–10 and the four bands. A technique choice at each band above Novice: three per skill. | 0–20, RimWorld's range |
| Q5 | **Rust** | None. A band once reached is never lost. | Decay above Journeyman, RimWorld's way |
| Q6 | **Mastery budget** | At most two skills past Apprentice and one at Master. A skill outside the budget stops at the top of its band (5, or 8) until the crawler gives one up (never, today). | No cap |
| Q7 | **Passions** | None, keen or burning per skill: practice ×1, ×1.5, ×2. Drawn at spawn on their own salt, leaned by personality: at most two keen and one burning. Work at a passion relieves frustration. | Talents only, as today |
| Q8 | **Practice and challenge** | Practice scales with the foe's or the work's difficulty against the crawler's level. A rat teaches a Novice fully and a Journeyman a quarter; a beetle or a boss teaches everyone. | Flat practice, as today |
| Q9 | **Upkeep** | An expected wage that rises with a crawler's best band: a quarter more for a Journeyman, half more for a Master (`EconomyTuning`). A raise unpaid costs trust, as an unpaid wage does. | No upkeep |
| Q10 | **Teachers** | Three practice sources with ceilings: a mentor (a better hand at the same work nearby, to its level less two), the drill yard (a guildhall station, to 5) and manuals (items from the caravan, to 3 or 5). Lineage is recorded. | Mentors only |
| Q11 | **Callings** | Titles, party roles and capstones at Master. Leadership is an eighth skill, practised by leading a party. Ambitions and retirement come later. | Titles only |
| Q12 | **Legend** | A deeds ledger, epithets by rule, scars after a downing, three earned-trait slots. The chronicle speaks in a crawler's strongest skill. | Epithets only |
| Q13 | **Named creatures** | In, after the Nemesis system: a creature that kills or downs a crawler earns a name, grows and may lead its lair's pack. Grudges run both ways, and a hunt can name it. No player avatar. | Mark the killer, nothing more |
| Q14 | **Masterworks** | In, last before the gate, supervised: unique item instances (retiring the kept `ItemId` quirk), inspiration, provenance. It can split into its own plan without touching the rest. | Out, to a later epic |
| Q15 | **New skills** | Leadership here (S5). Scouting with the descent (Epic 12). Trade when rival shops exist. | Keep the seven |
| Q16 | **Order** | Craft, then techniques, callings, legend, named creatures, masterworks (§2.4) | Legend first, for the earliest fun |

---

### Plan 1's decisions (Craft, from 2026-11-16)
Proposed in plan 1 as defaults; each lands here with its step. The producer answers by exception.
- **D1, the traits record** (S0.4; A3):
  - **The record:** `CrawlerTraits`, a fixed record of 256 bytes in the crawler's record after its skills, saved whole (format 12, plan 1's one raise) and copied to cognition. It lives in `Simulation/Skills/`, because cognition reads it and may read nothing of `People/`.
  - **Its sections:** a passion for each skill id; 48 technique slots (three bands for each of 16 skills); the first teachers of four skills, 20 bytes each; 16 deed counters; a calling, an epithet, four earned traits and four scars; 68 spare bytes.
  - **Why four teachers:** an `EntityId` is 16 bytes, so a teacher for every skill would take 320 bytes, and a crawler is taught in a few skills at most.
  - **The world hash** mixes the record only once something is written, so a record nothing has touched moves no hash.
- **D2, deeds are a table** (S0.5; A2, A7):
  - **The table:** seven rows. A creature slain, a boss slain, a rescue, a make, a downing survived, a hunt paid, a clear won.
  - **Counting:** on the main thread where each happens, never rebuilt from the chronicle. First blood is a count of one, derived.
  - **Why now:** counted from the start, the deeds give the measurements their data, and S6's epithets read a real history.
- **D3, passions are a table and a draw** (S1.1; A2, Q7):
  - **The table:** `PassionTable` has three steps (none, keen, burning). Each row gives the practice multiplier (×1, ×1.5, ×2), the frustration each act eases (0, 2, 4), its weight in the work's choice (D6) and its flame in the UI kit.
  - **The draw:** at spawn, after the skills' draw, on its own salt (`SimRandomSalt.Passions`, 17), so no earlier draw moves.
  - **The odds:** a burning passion with a quarter's chance, then none (40%), one (40%) or two (20%) keen ones. Each is picked at odds of the skill's lean, as talents are, and a starting talent counts double. One passion a skill at most.
- **D4, a passion speeds every source** (S1.2): practice's one entry multiplies by the passion before the source's ceiling, for the work and every teacher alike. The passionless keep K9's rate.
- **D5, practice by challenge** (S1.3; Q8):
  - **The rule:** `PracticeSource` carries the level an act demands. A crawler above it takes 75, 55, 40, 25, 20, 15 hundredths a level above, and a tenth from seven above (`SkillRules.ChallengePercent`).
  - **The numbers:**
    - creatures: rats 2, spinners 5, beetles 7, the Matriarch 9, the Gatekeeper 10;
    - harvested items: herbs 3, ore 4, glowstone 7;
    - recipes: from 3 (a pickaxe) to 7 (a glowstone edge, a chitin plate). The draught is 5 as the still's only recipe, so alchemy doesn't stall at Apprentice.
    - A rescue is always full.
  - **What the player sees:** veterans learn from beetles, the Gallery's glowstone and the forge's harder recipes, not from rats and pickaxes.
- **D6, passions weigh the work** (S1.4; A10): the salvage choice multiplies a node's score by the passion's weight (keen ×1.15, burning ×1.3), through `Considerations.PassionWeight`. No planner fact. The fight's choices and the guild's orders don't read passions yet.
- **D7, the mastery budget** (S2.1; Q6): at most two skills past Apprentice and one at Master. A third skill stops at 5 while two others stand at 6 or more; a second stops at 8 while another stands at 9 or more (`SkillRules.BudgetCeiling`, in practice's one entry beside the source's ceiling). First come, first served; nobody gives a skill up, and a skill past its budget keeps what it has. A starting talent is at most 5, so no draw breaks it.
- **D8, the asking wage** (S2.2; Q9):
  - **How much:** a member's or artisan's reservation wage times its best band's raise, ×1.25 for a Journeyman and ×1.5 for a Master (`GuildTuning`'s two raise rows, which the artisans read too). The best band is read through the seam.
  - **Signing:** a recruit or a hire signs for no less, and the refusal names the ask.
  - **A raise unpaid:** at a visit, a hand paid in full but below its asking wage takes the unpaid wage's trust and warmth. No allegiance moves and no unpaid visit is counted, so it quits only when its trust falls under the line.
  - **Told once:** the chronicle tells the ask when a band reached lifts the asking wage above the contract's (`RaiseAsked`).
  - **The ledger:** raises are part of wages, transfers, so it balances. The scripted players pay each raise when it is asked, within their reserves (S2.3).
- **D9, mentors** (S3.1; Q10):
  - **Who teaches:** when a crawler practises its work, a hand at least three levels better at the same work (its effective level), who did it within the game hour, within 6 units and on its feet, teaches it at source Mentor; the best of them, the lowest id on a tie.
  - **How much:** half the act, to the mentor's level less two, times the learner's passion; the budget holds it as any practice.
  - **Lineage:** the first lesson in a skill records the mentor in the learner's traits record and counts the mentor's "taught" deed (the eighth deed); the chronicle tells it.
  - **Not saved:** each crawler's last practice is work in progress; after a load nobody teaches until it works again.
- **D10, the drill yard** (S3.4; Q10):
  - **The station:** a station of its own kind in the guildhall, built through the BuildStation command for 200g into the Construction sink. No recipes, no artisan.
  - **Who drills:** a guild member with no orders (no expedition, no shift), fed (hunger under 50) and rested (energy over 50), its drill skill below 5, at priority 28: a game hour at the fight work of what it wields, 30 experience at source Yard in six lots, to 5.
  - **Paid for by the building:** nothing is charged by the hour. The station window says who drilled today.
- **D11, manuals** (S3.2, S3.3; Q10):
  - **The items:** a primer for each skill, an item row with a manual block (the work it teaches, 300 experience, to 3), at 25g. A treatise to 5 is the same block with another ceiling.
  - **Bought and read:** the caravan brings one of each (2g, the `ManualWholesale` sink; 8g until S3.6 found the first visit's 56g too dear for a stall at its poorest), and the stall's goods table grew from 24 to 32 inside format 12. A resident buys a primer for a skill it loves, from a spare purse, and reads it at the hearth for two game hours; the primer is used up and its experience practised at the end. Interrupted, the reader keeps the book and gains nothing.
  - **Narrowed, for the producer:** D11 said "its passions first"; delivered as **passions only** (keen or burning, the burning first). The passionless don't buy books, so the shelf doesn't draw every purse in an economy already strained (F6). Opening it to everyone is one condition in `ResourceTargeting.TryChooseManual`.
  - **Two planner facts,** `HasManual` (the book in the pack) and `Drilled` (a spell at the yard done, never true in a built state): 48 of 64.
- **D12, practice is observed** (S0.2): `ISimulationManager.Practised` (the crawler, the skill, the experience granted after every ceiling, the source's kind) is raised from practice's one entry for observers (measurements, live scenarios, `OutpostWatch`). No rule reads it.

## 1. Goal
**The player promise:** watch a crawler become someone. The miner who burns for the deep seam. The Warden who holds the line. The smith whose blade carries a dead friend's name. The patron can't make a hero, but can pay for the training, sponsor the technique and post the hunt that makes one.

What K9 leaves undone:
- **Skills are numbers without a nature.** Every crawler learns every skill at the same rate.
- **A band changes a percentage, never what a crawler can do.**
- **Seven bars tell no story:** no titles, no roles, no deeds.
- **Growth costs nothing.** A Master asks no more than a Novice, and nothing keeps a crawler to a few crafts.
- **Practice is flat:** a rat teaches a veteran as much as a beetle, so farming the weak is the best way to grow.
- **Creatures forget.** The beetle that killed a crawler is one beetle among many.

## 2. Design

### 2.1 The loop
1. **A crawler is born with a nature:** its talents (K9) and its passions, leaned by personality.
2. **It practises what it does,** faster at what it loves and more against what is hard.
3. **A band reached is a moment.** The crawler picks one of two or three techniques, in a dynamic scene. The patron may sponsor another, at a price, and trust decides.
4. **Bands and techniques make a calling:** Warden, Marksman, Prospector, Master Smith, Field Surgeon. A title on its card, a badge on its body and a role in a party.
5. **Growth costs.** A Journeyman expects a higher wage. A crawler masters two crafts, not seven. Teachers speed the young: a Master beside them, the drill yard, a manual from the stall.
6. **Deeds become legend:** first blood, epithets, scars, earned traits. The chronicle speaks in the crawler's strongest skill.
7. **The world remembers back.** A creature that kills or downs a crawler earns a name, grows and comes to lead its pack. The friends of the fallen want it dead, and a hunt names it. Whoever slays it carries the deed.
8. **Inspiration:** now and then a Journeyman makes a masterwork with a name and a maker, and it outlives them.

**The new verbs:** sponsor a technique, pay for teaching (the drill yard; manuals at the stall), name a hunt's quarry. Crawlers do the rest.

### 2.2 Positions (argued in the research paper; the producer may overturn any)
- **The patron proposes, the crawler disposes.** The player never spends a crawler's points. Point-spending for every pawn is a chore at outpost scale, and it would make the crawlers puppets in a game about indirect agency.
- **Keep 0–10 and four bands.** The three bands above Novice are the three choice points per skill; 0–20 adds granularity nobody reads.
- **A band once reached is never lost,** so a technique never vanishes and the moments stay earned.
- **Passions speed practice; they never replace it.** Today's rate is "no passion", so K9's measured DoDs hold.
- **Few choices that change rules, each with a cost.** A technique has an upside and a downside, never a +3% node.
- **Legend is earned, never rolled.** Traits and epithets are authored rows with names, not random affixes.
- **Read at a glance, deep on demand.** A roster card shows the calling and the best band; the dossier holds the rest.
- **No crawler-wide level.** Growth stays per skill (Oblivion's trap).

### 2.3 Architecture and rules
- **A1. K9.8's seam is the only door.**
  - How good a crawler is enters through `CrawlerSkills.Level`: gear, techniques and callings that raise a level.
  - What teaches enters through `Practise` and a `PracticeSource`: the work itself, a mentor, the drill yard, a manual, each with a ceiling.
  - Which skill a work trains is `SkillTable.SkillFor`, including Leadership's work.
  - No effect reads a learned level outside the seam (`SkillReadsTests`).
- **A2. Every new system is a table:** passions, techniques, callings, epithets, earned traits and creature titles.
  - Ids are never reused.
  - A names test guards each, as `SkillNamesTests` does.
  - `AGENTS.md` gains a recipe for each kind of row ("Adding a technique", "Adding a calling").
- **A3. One record of who a crawler has become.**
  - A fixed-capacity traits record beside its `SkillSet`: passions per skill, techniques per skill and band, a calling, an epithet, earned traits, scars and deed counters.
  - Saved in the crawler's record, with room to grow, and copied for cognition as its skills are.
  - The save format rises once in the PR, at S0, as K1 reserved 16 slots; later fields stay inside it while it is unreleased.
- **A4. Three kinds of technique effect, in rising cost:**
  1. a number, through the seam or a `StatModifier`;
  2. a rule flag that the fight, the harvest or the make reads;
  3. a new act, an intent of its own (a field splint, a rallying shout).

  Most are the first two.
- **A5. Bounded stacking.** Every modifier passes through `StatRules`' bounds, so no combination runs away.
- **A6. Choices are rules with reasons.**
  - Sponsorship is a typed command decided by a rule that explains a refusal, as `GuildRules` decides a recruit.
  - A crawler's own pick is a pure function of its nature and its seed, drawn on its own salt.
- **A7. Deeds are counted where they happen,** on the main thread: a kill, a rescue, a make, a downing survived. They are never rebuilt from the chronicle's text. The chronicle tells; it decides nothing.
- **A8. Named creatures extend the creature's record and its save block** (`CreatureSave`), and never get a planner (Part 3's position).
  - They grow from crawlers' encounters.
  - The patent line: no character the player pilots.
- **A9. Numbers live where they belong:** practice, passions and the budget in `SkillRules`; wages and prices in `EconomyTuning`.
- **A10. Cognition reads copies, and facts are scarce.**
  - Passions and techniques may weigh choices through `Considerations`.
  - The planner holds 46 of 64 facts, so a new fact needs a reason, under R1's guard.

### 2.4 Milestones

| Milestone | What the player sees | Core (engine-free simulation) | Client (KNI) | Complexity |
| :--- | :--- | :--- | :--- | :--- |
| **S0. Groundwork** | Nothing new | Measurements: skill growth on the gate seeds over 14 days, who specialises, practice by source; the traits record and its save (format 12); deed counters | — | Possibly Complex: saved state |
| **S1. Passions and challenge** | Some crawlers burn for their work and learn it fast; veterans want harder foes | Passions drawn at spawn; practice ×1.5 and ×2; passion work relieves frustration; practice scaled by challenge; passions weigh the work choices | Flames on the roster and in the dossier | Very Complex (bundling; split) |
| **S2. The cost of mastery** | Specialists: a crawler masters two crafts; a Journeyman wants a raise | The mastery budget; the expected wage by band, for members and artisans; a raise unpaid costs trust | The budget and the asking wage in the dossier and on the roster | Possibly Complex: behaviour and the soak's bands |
| **S3. Teachers** | A Master's apprentice learns fast; the guild's drill yard; manuals on the shelf | Mentors, the drill yard and manuals as practice sources with ceilings; lineage recorded | The yard; the manuals; "taught by" in the dossier | Possibly Complex |
| **S4. Techniques** | A band reached is a scene: Bram picks Shield Wall over Reckless Fury, and the patron can sponsor the other | The technique table (seven skills × three bands × two or three); the pick by nature; effects as numbers, flags and a few acts; the sponsorship command | The choice scene; techniques in the dossier; the sponsor button | Very Complex (split; the effects step supervised) |
| **S5. Callings** | One word on every card: Warden, Marksman, Prospector, Master Smith, Field Surgeon | The calling table; a calling earned from bands and techniques; party roles the guild's orders read; capstones; Leadership, an eighth skill | Titles, a badge on the body and the portrait, roles in the party builder | Very Complex (bundling: a skill, a table and orders; split) |
| **S6. First legends** | Corrin Beetlebane; a scar across a face; the chronicle in a skill's voice | The deeds ledger; epithets by rule; earned traits (help, hurt, act on their own); scars after a downing | Epithets in names; scars on the body and the portrait; the first-blood scene; skills speak | Very Complex (bundling; split) |
| **S7. Named creatures** | The beetle that killed Ama is Ama's Bane; it leads its lair; whoever slays it is Ama's Avenger | A creature's name, rank and growth; the pack's leader; grudges both ways; a hunt naming its quarry; saved | The named creature in the inspector and over the map; the hunt board | Very Complex (a new mechanism in the creatures' rule and save; supervised) |
| **S8. Masterworks** | Tamsin's inspiration: a named blade with a maker, owners and kills | Unique item instances (the `ItemId` quirk retired); inspiration; provenance | A masterwork's name and story in its tooltip and the dossier | Very Complex (the item system and the save; supervised) |
| **S9. The gate** | Epic 11 played through | Every scenario; seeds; soak bands with passions and upkeep | The producer's play session | Possibly Complex |

**Order:** S0 to S9, in order.
- **Craft first (S1–S3):** it changes the numbers everything after is tuned on, so the DoDs are re-measured once, early.
- **Techniques before callings:** a calling is built from them.
- **Legend after callings:** epithets and traits read the deeds and the callings.
- **Named creatures after the deeds ledger:** a creature's name comes from a deed.
- **Masterworks last:** the riskiest change, and the one that can split out.

## 3. Definition of Done

Done means crawlers grow into someone in the live outpost with nothing injected (ROADMAP §1B). A scripted player acts only through typed commands.
- **Mechanism:** validators for each table (passions, techniques, callings, epithets, traits, creature titles), the traits record's round trip, practice by challenge and by source, the budget, sponsorship's rule, deeds, named creatures and item instances.
- **Live, headless:** one scenario per milestone on the real outpost:
  - `PassionBurnsBright` (S1): a burning passion reaches a band before its passionless peers, and a Journeyman learns more from a beetle than from a rat;
  - `SpecialistsEmerge` (S2): by day 14 nobody holds more than two skills past Apprentice, and a member past Apprentice asks for a raise;
  - `AMasterTeachesAnApprentice` (S3): a new hand beside a Master is taught, and reaches Apprentice;
  - `ATechniqueDecidesAClear` (S4): a member picks a technique on its own, and the party's clear uses it;
  - `ACallingEmerges` (S5): a Warden emerges and holds a party's front;
  - `TheOutpostRemembers` (S6): an epithet is earned, and a downed crawler carries a scar;
  - `TheBeetleThatRemembers` (S7): a creature that downs or kills a crawler earns a name, is hunted and slain, and its slayer carries the deed;
  - `AMasterpieceHasAMaker` (S8): an inspired artisan makes a named piece, a resident wears it, and its provenance survives a save;
  - every scenario together, with a save mid-scene (S9).
- **The economy:** the ledger balances with upkeep, sponsorship and manuals. The soak's bands are re-measured with them (S2, S9).
- **The producer:** looks at S1, S4, S6 and S7, and a play session at S9.
- **Docs:** the roadmap first, then this file's status, then the focus file; `architecture_history.md`; the economy model (upkeep); `AGENTS.md` (each table's recipe).

## 4. Acceptance checklist (delivered work only)

### S0. Groundwork (done 2026-11-16, `feat/epic11-craft`)
- [x] **The hand-over checked, and the skills model:** plan 1's §0 matches the code. The skills' rules and numbers are in `docs/skills-model.md`.
- [x] **Practice observed** (D12): the `Practised` event, and `OutpostWatch`'s tallies by skill and source.
- [x] **The measurement** (`SkillGrowthMeasurementTests.MeasureGrowthOnTheGateSeeds`): 14 days on the 23 gate seeds, with no player and with the `hunting` players (`docs/skills-model.md` §5.1). Findings F1 (the budget wouldn't bind) and F2 (ranged and medicine never grow).
- [x] **The traits record** (D1): `CrawlerTraits`, saved in the crawler's record. Save format 12; golden unchanged.
- [x] **Deeds** (D2): seven deeds counted where they happen. `DeedsValidator`, `DeedNamesTests`, and each deed asserted in the live scenario that brings it about. Golden unchanged.

### S1. Passions and challenge (done 2026-11-16, `feat/epic11-craft`; the producer's look next)
- [x] **Passions drawn at spawn** (D3): `PassionTable`, drawn on their own salt. `PassionValidator` checks the odds over 10,000 crawlers, the caps, and the leans by personality and talent. Golden re-recorded for the hash alone.
- [x] **A passion speeds practice and eases frustration** (D4).
- [x] **Practice by challenge** (D5): a challenge on every creature, harvested item and recipe; `SkillsValidator` repeats Q8's examples.
- [x] **Passions weigh the work** (D6): the salvage choice, through `Considerations`. No planner fact.
- [x] **Re-measured** (`docs/skills-model.md` §5.2): F1 settled. Four DoDs re-picked across seeds: the hunt (8), the forge's kills (424242), the workshop loop (1; F3) and the death (62, off the gate seeds; F4).
- [x] **The client:** a flame in the dossier and on the guild's cards beside each skill a crawler loves, amber for keen, ember for burning. The dossier lists a loved skill even at 0. Two UI kit glyphs; renders recorded in the pinned image.
- [x] **DoD `PassionBurnsBright`** (gate seed 17): a resident burning for foraging reaches Apprentice ahead of every passionless peer who started where it did, and fighters above a rat's challenge draw 10 a blow from beetles against 4 from rats. Drafted as "a Journeyman learns more from a beetle than from a rat"; the scenario reads the rule over every fighter above a rat's challenge, since melee Journeymen are rare (S0.3). F5 found on the way.
- [ ] **The producer's look,** folded into the review after S3 (the producer, 2026-11-17: "Complete S2-S3 I will review afterwards"). What to look at:
  - **Start:** the desktop game on the default seed (`dotnet run --project dotnet/Simpiens.Desktop -- --seed 20261009`), at 5× or 10×.
  - **The flames:** click a resident and read its dossier's skills: an amber flame for a keen passion, two ember flames for a burning one. A skill it loves shows even at 0. Open the guild and read the cards' two best skills.
  - **A passion at work:** follow a crawler burning for mining or foraging. It goes for the salvage it loves, and its skill climbs faster than its neighbours'.
  - **Veterans and harder work:** an Apprentice miner learns less from ore, a forge's hand less from pickaxes than from ring coats, and a fighter more from a beetle than from a rat (not visible directly; the dossier's levels show it over days).
  - **To decide, by exception:** the passions' odds and numbers (D3, D4), the challenge numbers (D5), and the flames' look.

### S2. The cost of mastery (done 2026-11-17, `feat/epic11-craft`)
- [x] **The mastery budget** (D7): `SkillRules.BudgetCeiling` in practice's one entry. `SkillsValidator` test 7: the third skill past Apprentice stops at 5, the second Master at 8, first come, first served, and a skill past its budget keeps what it has. `OutpostWatch` fails the day of any live scenario on a breach at any tick. Golden unchanged.
- [x] **The asking wage** (D8): `GuildRules.AskingWage` and `WorkshopRules.AskingWage`, read by signing, the visit's pay, the chronicle and the dashboard. `UpkeepValidator` (5 tests): a raise paid keeps trust; a raise unpaid costs the unpaid wage's trust and warmth with no unpaid visit counted, the ledger balanced; the ask is told once; artisans ask by the same rows.
- [x] **The scripted players pay raises:** `GuildPlayer` and `WorkshopPlayer` send `SetWageCommand` up to the asking wage within their reserves; `LeaveRaisesUnpaid` keeps the signed wage (`ScriptedRaisesTests`).
- [x] **The soak with upkeep** (economy model §2, §7, §8): the `guild`, `crafting` and `hunting` profiles hold their bands on seeds 1–20 with raises paid, since no raise falls due by day 3. Bands unchanged, by exception. Over 14 days upkeep adds nothing measurable to the loop's strain (F6).
- [x] **The client:** "Foraging held at 5: two crafts past Apprentice" in the dossier; "asks 6g" in amber in the dossier, on the guild's card and on the station window's artisan line (`UpkeepOnScreenTests`). Looked at with an ask and a hold set by hand; no recorded render changes.
- [x] **DoD `SpecialistsEmerge`** (gate seed 5): the still's artisan reaches Alchemy 6, asks 6g and is paid it on the next tick; nobody breaks the budget at any tick; the days hold. Drafted with "a skill held by the budget" on the run as well; growth brings nobody to a third skill past Apprentice in two weeks on any gate seed (F1), so the validator proves the hold (`docs/skills-model.md` §5.3).

### S3. Teachers (done 2026-11-17, `feat/epic11-craft`)
- [x] **Mentors** (D9): a better hand at the same work nearby teaches at source Mentor, half the act, to its level less two; the first lesson records the teacher and counts the "taught" deed (the eighth deed). `MentorValidator` (5 tests). Golden re-recorded.
- [x] **Manuals as items** (D11): a primer for every skill (`ItemType` 25–31, a `ManualBlock`, `ItemCategory.Manual`), sold at 25g, the caravan bringing one of each at 2g into `ManualWholesale`; the goods table at 32 inside format 12. `ManualValidator` (4 tests). A book shape and seven covers; art pins re-recorded.
- [x] **Reading a manual** (D11): `StudyGoal` buys a primer for a skill the crawler loves, `ReadManualGoal` reads it at the hearth for two game hours, and it is used up. One planner fact, `HasManual`. `ReadingValidator` (3 tests); `ManualReadingTests` live on the default seed.
- [x] **The drill yard** (D10): a station kind in the guildhall, built for 200g; a guild member with no orders, fed and rested, drills there an hour at a time, to 5. One planner fact, `Drilled` (48 of 64). `DrillValidator` (3 tests). Its art in both view modes; art and picture pins re-recorded.
- [x] **The client:** the station window's yard tab (who drilled today), the primers in the merchant terminal, "Mining taught by Hugo" in the dossier (`TeachersOnScreenTests`). Looked at; renders recorded in the pinned image.
- [x] **DoD `AMasterTeachesAnApprentice`** (gate seed 13): Avery, taught at the herbs by Vesna, reaches Foraging 3 with Vesna recorded; Jory reads the ranged primer; Zara drills at the yard the player built; the days hold (`docs/skills-model.md` §5.4). Drafted with a Master teaching; every teacher found in two weeks is an Apprentice (nobody reaches Master yet, F1), so the run says so and keeps the epic's name.
- [x] **Moved DoDs re-picked across seeds** on the final build (the 2g wholesale, F7): PassionBurnsBright (18), SpecialistsEmerge (10), the nest's clear (20261009), the death's cost (16, back on the gate seeds), the workshop loop (18) and the archers (13); the gate, the carry, the armed crawler and the save mid-carry held their seeds, and the crafting soak's share holds (16 of 20).

## 5. Deferred and out of scope

| Left out | Owner |
| :--- | :--- |
| Doctrine: the guild's web, bought with renown and kept through the descent | After Epic 12 and rival guilds |
| Ambitions, and retirement into staff (mentor, quartermaster) | Later, with the guildhall's growth (Game Design Proposal §5) |
| Rust | Only if a measurement shows specialists fail to emerge (Q5) |
| Skill families, and a curve per skill | When a design needs them; K9.8's seam is where they enter |
| Scouting as a skill | Epic 12, the descent |
| Trade as a skill | When rival shops exist |
| Fights between crawlers (the design's "lethal rivalries") | Later, deeper floors |
| The hall's wall of the dead and the retired | Later, with ambitions |
| Tavern rumours written by a language model | Epic N, the agentic narrative engine |

## 6. Risks

| Risk | What we do |
| :--- | :--- |
| Passions and challenge change every seed's growth, and K9's DoDs were tuned on flat practice. | "No passion" is today's rate. Re-measure as K9.7 did, move a DoD's seed in its own commit, re-record golden in its own commit. |
| Upkeep moves the soak's bands and what the stall can afford (Part 2's F14). | Its numbers live in `EconomyTuning`. The soak's profiles are re-measured at S2, and the producer sets the bands. |
| Techniques multiply: 42 to 63 rows, each a balance risk. | Bounded stacking (A5); about six per specialist; a validator per kind of effect; the effects step supervised. |
| Ten milestones, three of them new systems. | S8 can split into its own plan with no loss to the rest; Doctrine stays out (Q1). |
| Named creatures need downings and deaths, which careful play rarely gives (Part 3's F6). | A downing counts, not only a kill. The scenario uses the hunt or the Clear order. |
| The Nemesis patent. | Every claim needs an avatar the player operates. Crawlers stay autonomous; any character the player pilots goes to counsel first (Q13). |
| Cards get crowded. | The roster shows a calling and a best band; the dossier holds the rest and still fits 720 rows. |
| Planner facts run out (46 of 64). | Choices weigh through `Considerations` first; a new fact needs a reason (A10). |
| The traits record grows past its room. | Fixed capacity, its size pinned as the economy records' are; a bump only by plan. |

## 7. Findings
- **F1 (2026-11-16, S0.3): the mastery budget wouldn't bind in 14 days.** On the 23 gate seeds over 14 game days, no resident holds two skills past Apprentice, with no player or with the `hunting` players (`docs/skills-model.md` §5.1). Nobody reaches Master, and the highest level is 8. As drafted, `SpecialistsEmerge` (S2) asks for a skill held by the budget by day 14. Passions and teachers speed growth, so S1.5 and S3 measure again. If the budget still never binds, S2's DoD asserts the rule at every tick and the asking wage live, and the budget's own outcome is proven where growth allows (a longer run, or a validator). [Possibly Complex: a DoD's shape; decided at S1.5]
  - **Settled at S1.5 (2026-11-16):** with passions and practice by challenge, still nobody holds two skills past Apprentice in 14 days (`docs/skills-model.md` §5.2). `SpecialistsEmerge` asserts the rule at every tick and the asking wage live, and the budget's own outcome is proven by a validator.
- **F2 (2026-11-16, S0.3): ranged and medicine never grow.** No resident practises either in 14 days on any gate seed. The hunting players arm blades, and no rescue happened. Teachers (manuals and the yard, S3) are these skills' only road until the guild arms bows or someone falls in danger. [Basic: a measurement that S3 reads]
- **F3 (2026-11-16, S1.5): the whole workshop loop plays through on 1 of the 23 gate seeds.** `MeasureTheWorkshopLoopOnTheGateSeeds` (both stations, the guildhall's guild of eight in two parties, seven days) plays through only on seed 1 (3 of 23 with skills at K9). The rest fall short on members quitting for want of pay, the forge's respirators or a party's trips. That is Epic 10's open F14, the economy under every player, made a little worse by faster growth. `TheWorkshopLoopPlaysThrough` moves to seed 1. The producer tolerates such flukes until they ruin a scenario; this one still has a seed. **Since the teachers (S3.6):** it plays through on 3 of the 23 (7, 18 and 20), and the DoD is on seed 18. [Possibly Complex: the producer's economy decision, unscheduled]
- **F4 (2026-11-16, S1.5): under-armed pairs rarely die now.** `DeathHasItsCost` sends two unarmed members against the beetles under the Bold rule. Over ten days a member dies on 1 of the 23 gate seeds (2 of 23 at Epic 10's K10.2a), and on 12 of seeds 21 to 100: rescuers carry most of the fallen out alive. Faster growth moves who stands where. Not traced: the medicine skill gets no practice in the growth measurement, so it isn't the rescuers' skill. The DoD moves to seed 62, off the gate seeds. **Since the teachers (S3.6):** a death on 7 of the 23 gate seeds over ten days, and the DoD is back on gate seed 16; deaths swing with timing, not a trend to trace. [Basic: watch; trace if deaths vanish on wider seeds too]
- **F5 (2026-11-16, S1.7): thrashing over fourteen days with the hunting players.** S1.7's measurement plays the `hunting` players for fourteen days on the gate seeds. Gate seeds 20261009 and 18 fail their days on visual-state thrashing (10 and 6 thrashes, with a jitter each). The days' checks hadn't run this long with every player before, so it isn't known whether Epic 11 brought it on. It is the crowding and planning kind of Epic 10 Part 3's F4, F5 and F11. [Basic: watch; trace one if a gate check shows it]
- **F6 (2026-11-17, S2.4): over two weeks the crafting and hunting guilds fall apart for want of pay.** Over 14 days on seeds 1–10, the `crafting` profile's guild loses members to unpaid wages on 8 seeds (up to 10 quits) and the `hunting` profile's on 7 (up to 18). Only one hand under contract stands past Apprentice at the end, for 1g a visit, so upkeep isn't the cause. It is Epic 10's open F14 seen over a longer run; the `guild` profile alone holds on all 10. The producer tolerates the economy until it ruins a scenario, and no scenario runs those profiles that long. [Possibly Complex: the producer's economy decision, unscheduled]
- **F7 (2026-11-17, S3.6): the primers' wholesale at 8g strained the crafting loop.** At 8g the caravan's first visit took 56g, when the stall is at its poorest, and the crafting profile's stations made nothing by day 3 on 6 of 20 seeds (the band asks 16); without primers 19, with the mentors off still 14, at 2g 16. The armed clears agreed (21 of 23 cleared without primers and at 2g, 18 at 8g). Set at 2g. It is the same strain as F6 and Epic 10's F14: the stall can't take a new cost early. [Basic: fixed; the band sits at its edge, 16 of 16]
- **F8 (2026-11-17, S3.6): teachers are Apprentices, so far.** Over two weeks every learner taught to Apprentice was taught by an Apprentice 3 to 5; nobody reaches Master (F1), so the epic's "a Master teaches an Apprentice" waits on growth past Journeyman. Mentors teach mostly at the seams (16 of 22 in mining). [Basic: watch; plan 2's measurements read it]


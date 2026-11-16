# Epic 11: Skills, the Heart and Engine of a Crawler

- **Status**: ACTIVE (2026-11-16). The producer gave the go for plan 1: "Economy flukes are tolerable until they completely ruin our testing scenarios. For now proceed with epic 11 Craft". Plan 1, Craft (S0–S3), is under way on `feat/epic11-craft`. QUEUED from 2026-11-15, when the producer asked for it: "create a new Epic focused on the Skill system heart and engine". It follows Epic 10, which ends with Part 3. Its questions (§0) are defaults that stand by exception.
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
Nothing yet.

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
None yet.

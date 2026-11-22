# Epic 11 plan 3: Legend (S6–S7)

Working document (AGENTS.md, Documentation Protocol 6), one of Epic 11's plans ([index](epic11_plan.md)). Written 2026-11-20, when plan 2 merged (PR #26, `d387b05`), at the producer's "This is great. PR plan 2 into main and then draft up plan3". **The go,** the same day: "Proceed with legend plan". The design is the epic, [`docs/epics/epic-11.md`](../docs/epics/epic-11.md) (§2.3's rules A1–A10 bind every step). Its questions Q1–Q16 stand by exception, Q12 (legend) and Q13 (named creatures) most of all. The decisions below (D26–D37) are this plan's defaults; each moves to the epic's §0 when its step lands. Plan 2's numbers are tuned on, from `docs/skills-model.md` §5.8.

- **Complexity:** Very Complex as a whole: S6 bundles four kinds of legend (epithets, scars, earned traits, the chronicle's voice) across the core and the client's art, and S7 is a new mechanism in the creatures' rule and their save. Split below so that every step is Basic or Possibly Complex. The epic stamps S7 "supervised": its three steps inside the creatures' rule and save (S7.1–S7.3) are flagged for the developer to supervise, or, if the producer says go and steps away again, listed for their review.
- **Branch:** `feat/epic11-legend`, cut from `main` at `d387b05`. One PR at the end, with a commit per step.
- **Estimate:** 8–11 agent days (S6 4–5, S7 4–5, close-out 1).
- **The producer's stops:**
  - **the go:** given 2026-11-20 ("Proceed with legend plan");
  - **the look at S6** (epithets in names, scars on bodies and portraits, the legend in the dossier, the first-blood scene);
  - **the look at S7** (a named creature over the map and in the inspector, the hunt that names it, the avenger);
  - **the merge.** The PR is opened at the close-out and not merged without the producer's word.
- **The DoDs:** `TheOutpostRemembers` (S6) and `TheBeetleThatRemembers` (S7), both live on the real outpost with nothing injected.
- **Save format:** S6 fits in the traits record (its epithet, earned-trait and scar bytes are reserved, and 68 bytes are spare). S7 raises the format to 13 once, at S7.1, for the creature record (a name, a rank, a foe) and the hunt record (its quarry): the PR's one raise.

## 0. What plan 2 hands over (checked at S6.0)
- **The traits record** (D1): `CrawlerTraits`, 256 bytes. Passions, techniques, teachers, deeds and the calling are filled. Reserved and empty: the epithet (a `ushort` at byte 178), four earned-trait bytes (180), four scar bytes (184) and one byte (177). Spare: 68 bytes (188).
- **The deeds** (D2): 8 of 16 counters: creatures slain, bosses slain, rescues, makes, downings survived, hunts paid, clears won, crawlers taught, each counted where it happens on the main thread (`AgentState.AddDeed`: `SimulationManager` for kills, downings survived and lessons; `CarryIntentHandler`, `WorkshopService`, `Market`, `GuildService.Expeditions`). `DeedNamesTests` lists the places. **No run has measured them yet:** S6.0 does.
- **The downing:** `AgentRuntime` at health 0 sets `IsDowned`, `DownedBy` (the cause: a creature or a hazard) and keeps `StruckBy` (the last striker's id), and tells `CrawlerDowned`. A downing survived counts its deed when the bleed clock stops with the crawler alive (`SimulationManager`). A death is `SimulationManager.Die`: `CrawlerDied` with ActorB the creature, and the departed list keeps the given name and the cause.
- **The techniques' door** (D14, D23): `CrawlerTechniques.Get(traits, skills, work, stat)` sums the techniques held in the work's skill and the calling's capstone at Master, within each stat's bounds. Cognition reads the same through its copy. Earned traits enter here (A1).
- **Moments** (D17): `SceneDirector`'s moment trigger plays a one-actor scene written from a chronicle event (`SkillScenes`), for the patron's people, an hour apart at the most often, armed by `ClientOptions.SkillScenes`.
- **Names on cards** (D24): the dossier's header "Bram the Warden" (`CallingTexts.Title`), the residents' cards ("Warden · Going to bed"), the guild's ("Warden (front)"), the technique tree's header (D25).
- **Bodies and portraits** (Epic UI-Beta): `PortraitKey` (look, mood, standing, worn) keys every baked portrait and face; `BodyKit` bakes 24 frames from a look and a `WornLook`. A scar is new to both keys.
- **Creatures** (Epic 10 Part 3, K2): `CreatureState` (kind, lair, place, health, mode, quarry, the keeper's last visit), saved raw as `CreatureRecord` in `CreatureSave` (format 12). A slain member returns to its place after its kind's hours, as a new creature. A creature notices the nearest crawler in sight within its leash, a Front member 3 units nearer at a clear's fight (D22). Its fighter is `Combatant.ForCreature`, from its kind and health. A lair's danger is `LairReach`; the snapshot carries which lairs are charted and which hold a living boss (`BossLairMask`).
- **Hunts** (K6): a hunt bounty names a charted lair (`PostBountyCommand`); `HuntRecord` keeps the lair and up to three hunters with the damage each dealt, paid from escrow when the lair is empty. `HuntPlayer` posts one once a bold resident would brave the lair.
- **The chronicle:** event ids to 52 (`CallingEarned`); `TemplateNarrativeFormatter` writes every line.
- **Growth** (`docs/skills-model.md` §5.8): with the hunting players, Journeymen on 22 of 23 seeds in 28 days; nobody reaches Master; callings move with the work.
- **Planner facts:** 48 of 64. This plan adds none (A10): a grudge weighs through the lair judgement cognition already makes.
- **The economy:** the producer's word stands ("Economy flukes are tolerable until they completely ruin our testing scenarios"). A named hunt is a bounty like any other; nothing here adds a sink or a faucet.

## 1. Decisions proposed (defaults; the producer answers by exception)
- **D26, the deeds measured, and one more** (A7):
  - **Measured first** (S6.0): each deed per resident over 14 and 28 days on the gate seeds, with no player and with the hunting players, and each downing by its cause. The epithets' and earned traits' thresholds (D27, D29) are set from it, so that a legend is earned on most seeds in two weeks and by few crawlers on each ("legend is earned, never rolled").
  - **One new deed** (S7.5): `NamedCreatureSlain` (9), the blow that slays a named creature. First blood stays a count of one, not a deed.
- **D27, epithets by rule** (A2, Q12):
  - **The table:** `EpithetTable` in `Simulation/Skills/`, a row per `Epithet` id (a `ushort`, never reused): its words, the deed it reads and the count that earns it, its rank, and its form. The forms: plain ("the Steadfast"); a creature kind's word, from a new column on each kind (`CreatureDefinition.SlayerEpithet`: "Ratcatcher", "Beetlebane", "Spinnerbane", the bosses "Broodbane" and "Gatebreaker"); or a crawler's name ("Ama's Avenger", S7).
  - **One held:** the highest rank earned; a later one of the same rank doesn't replace it. Judged in one rule (`EpithetRules.Judge`) wherever a deed is counted, never rebuilt. The kind or the crawler it names is kept in the traits record's spare bytes (a kind byte and an `EntityId`).
  - **Told:** the chronicle (`EpithetEarned`), and a moment for the patron's people.
  - **Shown:** after the given name, on every card and in the dossier: "Corrin Beetlebane", "Mara the Steadfast"; with a calling, "Corrin Beetlebane the Warden".
  - **The first rows** (the lines set at S6.0 from `docs/skills-model.md` §5.9; the Steadfast and the Unbroken are earned only where crawlers fall, F13):

    | Epithet | Earned by | Rank |
    | :--- | :--- | :-: |
    | the kind's word ("Beetlebane") | the 3rd creature slain; the kind of that kill | 2 |
    | the boss's word ("Broodbane", "Gatebreaker") | a boss slain | 5 |
    | the Steadfast | 3 rescues | 3 |
    | the Unbroken | 2 downings survived | 3 |
    | Lairbreaker | 5 clears won | 3 |
    | the Huntsman | 3 hunts paid | 2 |
    | the Teacher | 10 crawlers taught | 2 |
    | the Maker | 40 makes | 1 |
    | the victim's name ("Ama's Avenger") | a named creature slain (S7.5) | 6 |
- **D28, scars after a downing** (Q12):
  - **When:** at a downing survived (where its deed is counted), the crawler takes a scar, while it has a free scar byte (four).
  - **Which:** a row of `ScarTable` by what downed it: a creature kind's mark (a rat's bite, a beetle's claw, a spinner's acid), or a hazard's (a rockfall's brow, foul air's pale burn, the flood's none). Its place on the face follows the scars already held (a cheek, then the brow, the other cheek, the chin), so no draw is needed.
  - **What it does:** nothing to the numbers. It is told (`ScarTaken`), shown on the body and the portrait, and listed in the dossier ("a beetle's claw across the cheek").
- **D29, earned traits** (Q12):
  - **The table:** `EarnedTraitTable`, a row per `EarnedTrait` id (a byte, never reused): its words, its kind (help, hurt, or an act of its own), the condition that earns it, and its effects as `TechniqueEffect`s scoped to a kind of work.
  - **Three held** (three of the record's four bytes; the fourth stays spare). Earned once, never lost.
  - **Read through the techniques' door** (A1): `CrawlerTechniques.Get` sums the earned traits that apply to the work beside the techniques and the capstone, within the same bounds. A hurt read by cognition goes through the copy.
  - **The first rows** (the lines set at S6.0; Monster Hunter a month's story, the downing's and the rescue's where crawlers fall, F13):

    | Trait | Kind | Earned by | Effect |
    | :--- | :--- | :--- | :--- |
    | Battle-Hardened | help | 2 downings survived, if bold | +1 defense in a fight |
    | Shaken | hurt | a downing survived, if cautious | 10% less damage in a fight; a lair judged 25% more dangerous |
    | Monster Hunter | help | 10 creatures slain | 10% more damage in a fight |
    | Lifesaver | help | 3 rescues | carries 15% faster; medicine heals 10% more |
    | Patient Teacher | help | 10 crawlers taught | its lessons teach 10 points more |
    | Vengeful | its own act | a friend downed or slain by a named creature (S7.3) | a grudge (D33) |
- **D30, first blood and the skills' voices** (Q12: "the chronicle speaks in a crawler's strongest skill"):
  - **First blood:** the first creature a crawler slays is a moment for the patron's people (a scene from the `CreatureSlain` event when its count becomes one).
  - **The voices:** each skill's row gains a few authored lines (`SkillDefinition.Voice`) for the events that are a crawler's own: a band reached, an epithet earned, first blood. The chronicle speaks them in the crawler's best skill ("Bram's pick rings truer: Mining 6"; "Ilse's blade remembers its first kill"). A skill with no voice speaks plainly, as today.
- **D31, a creature earns a name** (A8, Q13):
  - **When:** its blow downs a crawler (the crawler's `StruckBy` at its downing), or a crawler it downed bleeds out. A boss doesn't take a name: it has one.
  - **The name:** "{the victim's given name}'s Bane", rank 1. Each further crawler it downs or kills raises its rank, to 3. The victim's id names it while the victim lives, and the departed list's name after.
  - **Saved** (format 13): the creature record gains its victim, its rank, its count of crawlers downed and its foe (D33).
  - **It doesn't return:** slain, its place refills with a plain creature in its kind's hours.
  - **Told:** `CreatureNamed`, `CreatureRankRose`.
  - **The patent line:** crawlers stay autonomous; no character the player pilots (Q13).
- **D32, it grows and leads** (A8, Q13):
  - **The titles' table:** `CreatureTitleTable`, a row per rank: its word (Bane, Scourge, Terror), its health and attack in percent (+20%/+10% a rank, first guesses), within bounds.
  - **Read:** where its fighter is made (`Combatant.ForCreature` reads the record's rank) and in its lair's danger (`LairReach`), so a lone crawler keeps away from a lair a named creature leads.
  - **It leads its pack:** while it chases, the pack mates in its lair's reach take the same quarry. The rule reads the rank, never a name.
- **D33, grudges both ways** (Q13):
  - **The creature's:** it remembers the last crawler that struck it and lived (its foe, saved). Noticing quarry, it sees its foe 3 units nearer, as it sees a Front member.
  - **The crawlers':** a friend of the crawler it downed or slew (warmth 40 or more, first guess) earns Vengeful (D29) with the creature as its grudge (an `EntityId` in the traits record's spare bytes). A Vengeful crawler, bold and armed, braves the grudge's lair with 25% less to spare (`Vitality.BravesLair`, through the copy and a snapshot mask of the lairs a named creature leads). The dossier says whom it hates.
- **D34, a hunt names its quarry** (Q13: "a hunt can name it"):
  - **The command:** a hunt bounty may name a named creature in a charted lair (`PostBountyCommand`'s quarry). It is paid when that creature dies, by the damage each hunter dealt it, not when the lair is empty. `HuntRecord` gains the quarry (format 13, the same raise).
  - **The players:** `HuntPlayer` names a quarry when one is charted; the guild's clear of its lair focuses it first (`ChooseFocus`).
- **D35, the avenger** (Q13: "whoever slays it carries the deed"): the blow that slays a named creature counts `NamedCreatureSlain` and earns "{victim}'s Avenger", the highest rank (D27). Told (`NamedCreatureSlain`) and a moment.
- **D36, the client for S6:**
  - **Names:** the epithet in every name the dashboard shows (the dossier, the cards, the guild's roster, the technique tree, the inspector, the chronicle). Where a card can't fit name, epithet and title at 720 rows, the card drops the title and the dossier keeps all three (decided at S6.6 from the longest).
  - **The dossier's legend line:** the epithet and why ("Beetlebane: 5 creatures slain"), the earned traits, the scars; each in its tip.
  - **Scars on the body and the portrait:** marks drawn by `BodyKit` and `PortraitKit` at the scar's place, both keys holding the scars. Look at the people sheet, then re-record the art pins.
  - **The first-blood scene** (D30).
- **D37, the client for S7:**
  - **On the map:** a named creature's name over it, in amber, and a mark (a world-sheet badge, one per view mode, hung as the role badges are).
  - **The inspector:** its name, rank, whom it downed, its foe.
  - **The hunt board:** a named creature offered as a hunt's quarry beside the lairs.
  - **The naming scene:** for the patron's people, the fallen's friend's vow when a creature earns its name.

## 2. The milestones, phased

### S6. First legends (4–5 days), then the producer's look
**Complexity:** Very Complex as the epic stamps it (bundling: epithets, scars, traits and voices). Split here so each table, its rule and its client land and are checked alone; each step is Possibly Complex but two.
- [x] **S6.0 The hand-over checked, and the deeds measured** (done 2026-11-20: §0 matches the code; `docs/skills-model.md` §5.9; the lines below set from it; F13, nobody falls in ordinary play) [Possibly Complex: measuring]
  - Check §0 against the code; a difference becomes a finding (F13 onward).
  - **The measurement:** the growth measurement (`SkillGrowthMeasurementTests`) also reports each deed per resident and the downings by cause, at 14 and 28 days, no player and hunting players, from a copied Release bin. Written to `docs/skills-model.md` §5.9.
  - **Set from it:** D27's and D29's thresholds, so that a legend is earned on most seeds in two weeks and by few crawlers on each.
- [x] **S6.1 The epithet table and its rule** (done 2026-11-20; golden unchanged: no epithet is earned in its 3,000 ticks) [Possibly Complex: a new table whose ids are saved]
  - **The table:** D27's `Epithet`, `EpithetTable`, the kinds' `SlayerEpithet` column; the traits record's epithet and its subject (spare bytes, so the format holds).
  - **The rule:** `EpithetRules.Judge`, called where each deed is counted; `EpithetEarned` in the chronicle.
  - **The checks:** `EpithetValidator` (the table, the ranks, a row per form, the rule at each threshold, one held, the round trip); `EpithetNamesTests`. Golden re-recorded in its own commit if an epithet is earned within its 3,000 ticks.
- [x] **S6.2 Scars** (done 2026-11-20; golden unchanged) [Possibly Complex: saved in the traits record]
  - **The table:** D28's `Scar`, `ScarTable` (by a creature's kind or a hazard's, its place by the scars held).
  - **The rule:** at a downing survived; `ScarTaken` in the chronicle.
  - **The checks:** `ScarValidator` (the table, a scar for each cause, the places in order, four at most, the round trip).
- [x] **S6.3 Earned traits through the door** (done 2026-11-20; golden unchanged; one new stat, Dread, for Shaken's lair judgement) [Possibly Complex: a behaviour change; cognition reads a hurt]
  - **The table:** D29's `EarnedTrait`, `EarnedTraitTable`, `EarnedTraitRules` (judged where deeds are counted and at a downing survived, by nature for the bold and the cautious).
  - **The door:** `CrawlerTechniques.Get` sums the earned traits that apply to the work; Shaken's lair judgement is read in `Vitality.HarmToClear` through the copy.
  - **The checks:** `EarnedTraitValidator` (a test per kind of trait); the names and reads guards list the table. Golden re-recorded in its own commit if it moves.
- [x] **S6.4 First blood and the skills' voices** (done 2026-11-20; golden unchanged; first blood is its own event, `FirstBlood`, so the kill's keeps its meaning) [Possibly Complex: the chronicle and a moment]
  - **The voices:** `SkillDefinition.Voice` lines for each skill; `TemplateNarrativeFormatter` speaks a band reached, an epithet earned and first blood in the crawler's best skill.
  - **First blood:** the moment, from `CreatureSlain` at a count of one (`SkillScenes`).
  - **The checks:** the formatter's tests (a line per skill, the plain fallback); `SkillSceneTests` gains first blood.
- [x] **S6.5 Re-measured** (done 2026-11-20: growth as before; epithets on 20 of 23 seeds with the players; seeds 1–120 both ways, the soak 18/18 and 20/20 ×3; no DoD moved) [Possibly Complex: measuring]
  - The 14-day growth and deeds on the gate seeds (`docs/skills-model.md` §5.10); the gate seeds' day, the soak, seeds 1–120 with and without the guild.
  - **Moved DoDs** are re-picked once, each from a measurement across the gate seeds, each in its own commit.
- [x] **S6.6 The client: epithets and the legend line** (done 2026-11-20; the guild's card shows the epithet before its title, its name line holding two skills; the expedition tracker and the chronicle's actor names stay plain) [Possibly Complex: the client]
  - **Names:** the epithet in every name shown (D36), with the longest name, epithet and title fitted on each card at 1280×720.
  - **The dossier:** the legend line and its tips; the dossier still fits 720 rows.
  - **The checks:** `LegendOnScreenTests` (names, the line, its tips, the fit).
- [x] **S6.7 Scars on the body and the portrait** (done 2026-11-20; a fleck of scar red at each place on the body; each shape at its place on the portrait; the frames carry the scars) [Possibly Complex: art across two keys]
  - **The art:** `BodyKit` and `PortraitKit` draw each scar at its place; `PortraitKey` and the bodies' key hold the scars.
  - **The checks:** look at the people sheet; re-record the art pins in their own commit; changed renders recorded in the pinned image in their own commit.
- [x] **S6.8 DoD `TheOutpostRemembers`** (done 2026-11-20: gate seed 3, on the guild's ordinary clears; an epithet on 19 of 23 gate seeds and a scar on 12 in ten days; F13 corrected: the soak's parties carry draughts, the guild's clears without them down members) [Possibly Complex: a live DoD]
  - On the real outpost with the guild's player ordering clears (the soak's hunting players never down anyone, F13), within ten days: a crawler earns an epithet on its own, and a crawler downed and brought back carries a scar, both in the chronicle; on screen in `LegendOnScreenTests`.
  - Its seed from a measurement across the gate seeds (`MeasureTheOutpostRemembersOnTheGateSeeds`).
- [x] **S6.9 S6's gate** (done 2026-11-20: core 325/325, client 542/542 pinned, golden 3/3, compiles clean; seeds and soak at S6.5) [Basic: the gates (§4)]
- [ ] **The producer's look at S6:** the brief is in the epic's §4.

### S7. Named creatures (4–5 days), then the producer's look
**Complexity:** Very Complex as the epic stamps it (a new mechanism in the creatures' rule and save; supervised). Split so the name and the save, the growth, the grudges and the hunt land alone. **S7.1–S7.3 are supervised by the developer** (the creatures' rule and save); if the producer is away, they are listed for review on return and nothing in them goes in unflagged.
- [x] **S7.0 Downings measured** [Possibly Complex: measuring] (2026-11-22: `docs/skills-model.md` §5.11. The guild's clears down a member on 12 of 23 seeds within ten days and would name a creature on 10, which the party slays in the same fight; no player and the hunting players down nobody. The DoD runs the guild's clears.)
  - How often a creature downs or kills a crawler over 14 days on the gate seeds, by lair and scenario: no player, the hunting players, the guild's clears, `DeathHasItsCost`'s pairs under the Bold rule. Written to `docs/skills-model.md` §5.11.
  - **Sets the DoD's shape:** downings are rare (Part 3's F6, plan 1's F4). If no scenario downs a crawler on most seeds within ten days, the DoD runs the Bold rule's pairs.
- [x] **S7.1 A creature earns a name; the save's one raise** [Possibly Complex: saved state, format 13; supervised] (2026-11-22, for the developer's review. **Deviation:** `HuntRecord`'s quarry is added here, not at S7.4, so format 13 has one layout from its first commit and a save made mid-branch keeps loading. The name is registered with the identity service by the session host, so the chronicle and the dashboard resolve it as a crawler's.)
  - **The record:** `CreatureState`'s victim, rank, count and foe; `CreatureRecord` grows; format 13; `EconomySerializationValidator` pins the new size.
  - **The rule:** at a downing by a creature and at a death it caused (D31); named creatures don't return; `CreatureNamed` and `CreatureRankRose` in the chronicle.
  - **The checks:** `NamedCreatureValidator` (named at a downing and at a death, the rank rising to 3, a boss never named, a named creature not returning, the round trip in `SessionCaptureValidator` and `SessionRestoreValidator`).
- [x] **S7.2 It grows and leads** [Possibly Complex: the creatures' rule; supervised] (2026-11-22, for the developer's review. The lead reuses the Matriarch's call (`Rally`), limited to mates in the lair's reach. Golden unchanged. `DeathHasItsCost` moved: gate seed 16's two fallen are now carried out alive; re-picked to seed 11 in its own commit.)
  - **The table:** D32's `CreatureTitleTable`, its bounds.
  - **Read:** `Combatant.ForCreature` and `LairReach` read the rank; the pack takes its leader's quarry.
  - **The checks:** `NamedCreatureValidator` gains the growth, the lair's danger and the pack's quarry. Golden re-recorded in its own commit if it moves.
- [x] **S7.3 Grudges both ways** [Possibly Complex: cognition reads a grudge; supervised] (2026-11-22, for the developer's review. **Readings:** only a named creature keeps a foe, so plain fights and golden don't move; Vengeful is an earned trait's first "act" row (`TraitAct`), so code names the act, never the trait. The grudge lives in the traits record's spare bytes, aligned. `DeathHasItsCost` moved again; re-picked to seed 8 in its own commit, ahead of S7.6, so each commit stays green.)
  - **The creature's:** its foe seen 3 units nearer (D33).
  - **The crawlers':** Vengeful earned by a fallen's friend, the grudge in the traits record, the grudge's lair braved with less to spare (the snapshot's mask of lairs a named creature leads).
  - **The checks:** `GrudgeValidator` (the foe's draw, Vengeful earned by warmth, the eased bar only for the grudge's lair and only for the bold and armed).
- [x] **S7.4 A hunt names its quarry** [Possibly Complex: a command's shape and the hunt record, in the same raise] (2026-11-22. The record's field landed at S7.1. The hunters' handler takes the quarry first and is done at its death, read through the context's `TryGetHunt`. Golden unchanged; no DoD moved.)
  - **The command and the record:** `PostBountyCommand`'s quarry, `HuntRecord`'s; `Market` pays on the quarry's death by damage dealt to it.
  - **The players:** `HuntPlayer` names a charted quarry; `ChooseFocus` takes the named first.
  - **The checks:** the hunt's validators gain a named hunt (refused for a creature not named or not charted; paid on its death by damage, not on the lair emptied); the round trip.
- [x] **S7.5 The avenger** [Basic: a deed row and an epithet row, by their recipes] (2026-11-22. The moment, a scene for the patron's people, comes with S7.7's naming scene.)
  - `Deed.NamedCreatureSlain` (9) counted at the killing blow; the Avenger epithet (D35); `NamedCreatureSlain` in the chronicle. `DeedNamesTests` lists the place.
- [x] **S7.6 Re-measured** [Possibly Complex: measuring] (2026-11-22: §5.12. Seeds 1–120 both ways and the soak hold; `DeathHasItsCost` re-picked at S7.2 and S7.3 rather than once here, each in its own commit, so every step's commit stayed green.)
  - As S6.5, with named creatures (`docs/skills-model.md` §5.12). Moved DoDs re-picked once, each in its own commit.
- [ ] **S7.7 The client: the named creature** [Possibly Complex: art, the inspector, the board, a scene]
  - The name over it and its mark on the map (a badge per view mode, art pins); the inspector's lines; the hunt board's quarry picker; the naming scene.
  - **The checks:** `NamedCreaturesOnScreenTests`; the art sheet looked at, the pins and renders re-recorded in their own commits.
- [ ] **S7.8 DoD `TheBeetleThatRemembers`** [Possibly Complex: a live DoD on a rare trigger]
  - On the real outpost, within a bounded run: a creature downs or kills a crawler and earns a name; the guild's player posts a hunt naming it (or orders a clear that focuses it); it is slain; its slayer holds the Avenger epithet. The scripted players act only through commands.
  - Its seed from a measurement across the gate seeds.
- [ ] **S7.9 S7's gate** [Basic: the gates (§4)]
- [ ] **The producer's look at S7:** a brief in the epic's §4, written at S7.9.

### C. Close-out (1 day)
- [ ] **C.1 The Legend gate** [Basic: the gates on the final build]
  - Every gate in §4 on the final build, and growth at legend complete over 28 days (`docs/skills-model.md` §5.13: deeds, epithets, scars, traits, named creatures).
- [ ] **C.2 The close-out** [Basic: a close-out from the lists this plan holds]
  - The roadmap, the epic, the focus file, `architecture_history.md`, `AGENTS.md` (recipes: an epithet, a scar, an earned trait, a creature title) and the skills model reconciled; findings to the epic's §7; the plan deleted. The PR into `main`, merged on the producer's word.

## 3. For the producer
- **Decided by exception:** D26–D37, above all the epithets' and traits' rows (first guesses, their thresholds set at S6.0), scars as marks with no effect, a named creature's growth (+20% health and +10% attack a rank, to 3), the grudge's warmth line (40) and its eased bar (25%), and a boss never taking a name.
- **Two looks** (S6, S7) and the merge. S7.1–S7.3 are supervised steps.
- **Carried from plan 2, unanswered and standing as delivered:** F9 (the crafting band, judged on seeds 1–40), F11 (a skilled loner would brave the Matriarch's warren). A named creature leading a lair makes F11's loner less likely there; F11 itself stays the producer's.
- **Not in this plan:** pre-sponsoring a band not reached (D25's open offer), Leadership's techniques and primer (D19), techniques' acts of their own (D14).

## 4. Gates (every milestone)
As the index's: the whole solution built, then `dotnet test`; `golden check` 3/3 (re-recorded only for a deliberate change, in its own commit naming the cause); the client's tests in the pinned image, renders byte for byte (changed renders looked at, then re-recorded there, in their own commit); the MonoGame and Android compiles with 0 warnings; the 23 gate seeds and the soak; seeds 1–120 with and without the guild at each milestone's end.

## 5. Risks
| Risk | What we do |
| :--- | :--- |
| Downings are rare in careful play (Part 3's F6, plan 1's F4), so names and scars may not happen in a DoD's run. | S7.0 measures first; the DoD may run the Bold rule's under-armed pairs, as `DeathHasItsCost` does. A downing counts, not only a death. |
| Epithets too common read as noise; too rare, never seen. | Thresholds set from S6.0's measurement, re-checked at S6.5. |
| Names grow long: "Corrin Beetlebane the Master Warden" on a card at 720 rows. | Measured at S6.6 against the longest given name (10 letters), epithet and title; a card drops the title before the dossier drops anything. |
| Scars enter the portrait's and the body's keys: every baked face can move. | Only crawlers with scars change; the people sheet and the pins show which. |
| The creature record's raise breaks old saves. | Refused with a reason, as each raise has been; one raise in the PR. |
| A named creature that grows makes a lair a wall. | Three ranks, bounded; its lair's danger keeps the lone away (D32), and the hunt and the clear are the answer the design wants. |
| Planner facts run out. | None added: the grudge weighs through the lair judgement. |

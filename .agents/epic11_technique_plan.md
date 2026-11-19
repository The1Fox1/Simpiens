# Epic 11 plan 2: Technique and Calling (S4–S5)

Working document (AGENTS.md, Documentation Protocol 6), one of Epic 11's plans ([index](epic11_plan.md)). Written 2026-11-19, when plan 1 merged (PR #25, `aceb183`), at the producer's "I will be afk. You will merge in Plan 1 then draft up plan 2. After drafting plan 2 you will begin immediate execution and will attempt to complete it". The design is the epic, [`docs/epics/epic-11.md`](../docs/epics/epic-11.md) (§2.3's rules A1–A10 bind every step). Its questions Q1–Q16 stand by exception. The decisions below (D13–D24) are this plan's defaults; each moves to the epic's §0 when its step lands. Plan 1's numbers are tuned on, from `docs/skills-model.md` §5.5.

- **Complexity:** Very Complex as a whole: two milestones with no precedent in the game (choices that change rules, identities that change orders), across the core, cognition, the guild's orders, the creatures' rule and the client. Split below so that every step is Basic or Possibly Complex. The epic stamps S4's effects step "supervised": here it is split three ways (the seam with the fight's numbers; the work's numbers; the one rule flag), each Possibly Complex. The producer is away, so the steps the epic would have them supervise are listed for their review on return (§3), and none goes in unflagged.
- **Branch:** `feat/epic11-technique`, cut from `main` at `aceb183`. One PR at the end, with a commit per step.
- **Estimate:** 8–11 agent days (S4 4–6, S5 3–4, close-out 1).
- **The producer's stops:**
  - the look at S4 (the choice scene, techniques in the dossier, sponsorship). The producer is away, so it folds into the review on their return, as S1's did into plan 1's;
  - the merge. The PR is opened at the close-out and **not merged** without the producer's word.
- **The DoDs:** `ATechniqueDecidesAClear` (S4) and `ACallingEmerges` (S5), both live on the real outpost with nothing injected.
- **No save format change.** Techniques and the calling fill the traits record's reserved bytes (D1), and Leadership is the eighth of sixteen skill places. Format 12 holds.

## 0. What plan 1 hands over (checked at S4.0)
- **The traits record** (D1): `CrawlerTraits`, 256 bytes. Its 48 technique bytes (skill id × 3 + band − 1) and its calling byte are reserved and empty; 68 spare bytes.
- **The seam** (K9.8): `CrawlerSkills.Level(skills, worn, work)` is how good a crawler is; it takes no traits yet. Its callers: the fight (`Combatant.ForCrawler`, both overloads: the main thread's from a state, cognition's from skills and what is worn, for `Vitality.HarmToClear`), the harvest (`HarvestResourceIntentHandler.SkillSpeed`, read by the salvage choice too), the stations (`WorkshopService`, a make's pace and grade), the rescue (`Vitality` carry pace, `CarryIntentHandler` healing), mentors (`SimulationManager.FindMentor`, `Teach`), the drill yard (`Drill`), manuals (`ResourceTargeting.TryChooseManual`), and the dashboard.
- **Practice's one entry:** `SimulationManager.Practise`, then `PractiseOwn`: passion × challenge, the source's ceiling and the budget, the `Practised` event, and a band reached told in the chronicle (`SkillBandReached`) with the ask (`TellTheAsk`).
- **The draw at spawn:** `SessionHost.GiveIdentity`: look, skills (`SimRandomSalt.Skills`, 16), passions (`Passions`, 17).
- **The guild's orders** (`GuildService.Expeditions`): a clear's focus is the creature nearest the party's first member (`ChooseFocus`); its carrier the mate with the most health (`ChooseCarrier`); its arms `GuildRules.PlanArms`, the last N members armed to shoot; the retreat rules `ShouldTurnBack` and `ShouldTurnBackFromFight` read `ExpeditionTuning.RetreatHealth`. Each member's order is copied to cognition each tick (`ExpeditionOrder`).
- **The creatures' rule** (`CreatureService.Rule`): a creature notices the nearest crawler in sight within its leash (`Notice`), turns on whoever strikes it while resting, roaming or going home (`Provoke`), and keeps its quarry while chasing.
- **The client:** dynamic scenes start from a zone entered or a two-crawler chronicle interaction (`SceneDirector`), each a fixed script; the dossier lists skills with flames, teachers and the budget's hold; the guild's cards show two best skills.
- **Growth at craft complete** (`docs/skills-model.md` §5.5, hunting players, 14 days): every resident holds an Apprentice band from spawn; 11–12 more Apprentice bands a seed are reached; about one Journeyman a seed (mostly the artisans); never a Master. Melee: 27 to Apprentice across the 23 seeds, none past it.
- **Planner facts:** 48 of 64. This plan adds none (A10): techniques and callings weigh through the numbers cognition already reads.
- **The economy:** the producer's word stands ("Economy flukes are tolerable until they completely ruin our testing scenarios"). Sponsorship is a transfer, like a wage; nothing here retunes the loop.

## 1. Decisions proposed (defaults; the producer answers by exception)
- **D13, the technique table** (A2, A4, Q4):
  - **The table:** `TechniqueTable` in `Simulation/Skills/`, one row per `Technique` id (a byte, 1–255, never reused). Each row: its skill, its band (Apprentice, Journeyman, Master), the personality that leans its pick, up to three effects, the player's words for them, and its vow (the crawler's line in the choice scene).
  - **Two at each band** of each of the seven skills: 42 rows. Each has an upside and a downside, never a free bonus ("few choices that change rules, each with a cost").
  - **The pattern:** in a fight, a stance (Shield Wall against Reckless Fury), then a rhythm, then a last resort; at work, producing against learning (pace against practice) at Apprentice and Journeyman, and at Master teaching against producing. Smithing's Apprentice and Master choices are grades. Medicine's are healing against carrying.
  - **Leadership has no techniques in this plan** (D19). A band reached in a skill with no rows picks nothing.
- **D14, effects are numbers through one door** (A1, A4, A5):
  - **The stats** (`TechniqueStat`): attack (%), defense (flat), hit (hundredths), pace (%: a swing's interval, a harvest, a make or a carry, whichever the skill's work is), range (%), last stand (% attack below half health), practice (%), grade (levels), heal (%), teach (points on a mentor's 50%), and one rule flag, unyielding.
  - **The door:** `CrawlerTechniques.Get(traits, skills, work, stat)`, beside `CrawlerSkills`. It sums the effects of the techniques the crawler holds in the work's skill (and, from S5, its calling's capstone), held within each stat's bounds (`TechniqueRules`, A5). A pure function of what cognition's copy also holds, so the main thread and cognition agree.
  - **Scoped to the skill's work:** Shield Wall's defense counts while its holder fights in reach, not while it shoots or mines.
  - **No acts of their own in this plan** (A4's third kind: a rallying shout, a field splint). Deferred until a technique needs one, by default.
- **D15, the pick by nature** (A6, Q3):
  - **When:** at a band reached in a skill whose band has rows, if its slot is empty: in practice's one entry, and at spawn for the starting talents (an Apprentice's).
  - **How:** one of the band's rows, each weighted 1 + 2 × its lean in the crawler's personality (as talents are), drawn on its own salt (`SimRandomSalt.Techniques`, 18), keyed by the crawler, the skill and the band. The same crawler picks the same whenever it gets there.
  - **Told:** the chronicle tells the pick (`TechniqueChosen`), not at spawn.
  - **The seed:** the simulation keeps the session's seed (`SimulationManager.Seed`), set where the lairs are filled and on a load.
- **D16, sponsorship** (A6, Q3):
  - **The command:** `SponsorTechniqueCommand` (the crawler, the technique). The patron offers gold for a crawler to take a band's other technique in place of the one it holds.
  - **The rule** (`TechniqueRules.CheckSponsor`, with a reason for each refusal): one of the patron's people (a guild member or an artisan); the band reached and holding the other row; the stall able to pay; trust at least 50 (warmth 0 or more); and not against its nature (a lean of −0.5 or below toward the row: "Bram is too cautious for Reckless Fury").
  - **The price:** by band, 20g, 40g, 80g (`GuildTuning`), paid from the stall's treasury into the crawler's purse: a transfer, so the ledger balances with no new sink.
  - **Told:** the chronicle (`TechniqueSponsored`). A band may be sponsored again; each costs.
- **D17, the choice scene** (the client):
  - **A moment:** a dynamic scene with one actor, started by a chronicle event naming one crawler and built from it: the crawler's vow, then the road not taken ("Reckless Fury? Not for me."). A new trigger kind beside the two that exist.
  - **Whose:** the patron's people only (members and artisans), at most one a game hour, so the outpost's many Apprentice bands don't drown the screen. The chronicle tells everyone's.
  - **Armed** by `ClientOptions.SkillScenes` (the desktop exe's), as the story's scenes are; tests turn it on where they look at it.
- **D18, the dossier and the sponsor button:** a line for each technique held ("Shield Wall (Melee): +3 defense, 15% less damage"), and for one of the patron's people, a button to sponsor the band's other row ("Sponsor Reckless Fury, 20g"), greyed with the rule's reason.
- **D19, Leadership, an eighth skill** (Q11, Q15):
  - **The row:** `Skill.Leadership` (8), a new kind of work (`SkillUse.Lead`, `SkillWork.Lead()`), leaned by loyalty, its practitioner "captain".
  - **The captain:** the member of a party out with the best effective Leadership, the lowest id on a tie (a Captain by calling first, from S5.4). Derived each time it is asked, so nothing new is saved.
  - **Practice:** at a trip's delivery, the captain practises 40 for each other member brought home, at the trip's challenge (a gathering 3; a clear, its lair's kind's).
  - **What a level does:** the captain steadies its party. Both retreat rules' health line falls by 3% a level (30% at Master).
  - **No techniques yet** (D13). Its calling is Captain.
  - **Every seed's draw moves** (talents are picked among all skills), so golden is re-recorded and the moved DoDs re-picked once, at S5.6.
- **D20, the calling table** (A2, Q11): `CallingTable`, one row per `Calling` id (a byte, never reused). Each row gives the title, its skill, the Apprentice technique that earns it (none for Captain), its party role, its Master title and its capstone. 15 rows:

  | Skill | Technique | Calling | Role | Master title |
  | :--- | :--- | :--- | :--- | :--- |
  | Melee | Shield Wall | Warden | Front | Master Warden |
  | Melee | Reckless Fury | Berserker | Front | Warlord |
  | Ranged | Steady Aim | Marksman | Back | Master Marksman |
  | Ranged | Quick Draw | Skirmisher | Back | Master Skirmisher |
  | Mining | Seam Student | Prospector | — | Master Prospector |
  | Mining | Hard Driver | Delver | — | Deep Delver |
  | Foraging | Gentle Hands | Herbalist | — | Master Herbalist |
  | Foraging | Quick Hands | Gleaner | — | Master Gleaner |
  | Smithing | Fine Temper | Smith | — | Master Smith |
  | Smithing | Fast Forge | Forgehand | — | Forgemaster |
  | Alchemy | Careful Measure | Apothecary | — | Master Apothecary |
  | Alchemy | Quick Brew | Brewer | — | Master Brewer |
  | Medicine | Steady Hands | Field Surgeon | Carrier | Master Surgeon |
  | Medicine | Strong Back | Bearer | Carrier | Master Bearer |
  | Leadership | — | Captain | Leader | Commander |
- **D21, a calling earned** (Q11):
  - **The rule** (`CallingRules.Earned`): the crawler's best skill (the highest level, then the most experience, then the table's display order), at Apprentice or above, with the Apprentice technique that earns its row. Nobody is without one for long: every resident starts with an Apprentice talent, so "one word on every card".
  - **Kept:** the calling is saved in the traits record and changes only when another skill becomes the best, or a sponsorship changes the technique that earned it. Judged at spawn, at a band reached and at a technique changed.
  - **Told:** a calling changed is told in the chronicle (`CallingEarned`), not at spawn.
- **D22, party roles the guild's orders read** (Q11):
  - **The order carries the role:** the member's order copy (`ExpeditionOrder.Role`) holds its calling's role while it is with a party out.
  - **Front holds the front:** at a clear's fight, a creature that notices quarry takes a Front member in its sight as 3 units nearer than it is, and a creature chasing a party mate turns on a Front member of that party within 3 units of it (the Warden steps in). The creatures' rule reads the order, never a calling's name.
  - **Back shoots:** the clear's arms (`GuildRules.PlanArms`) arm the Back members to shoot first, then the last in the list as today.
  - **Carrier carries:** the guild sends a Carrier to carry the fallen before the healthiest mate.
  - **Leader leads:** a Captain is the party's captain (D19). The captain is who the focus is chosen nearest (today the party's first member).
- **D23, capstones** (Q11): at Master in its skill, a calling takes its Master title and its capstone, an effect through the same door (D14). Mostly +15% pace; the Warden +3 defense, the Berserker +20% attack, the Marksman +5 hit, the Field Surgeon +20% healing. Nobody reaches Master in two weeks (F1, F8), so the capstones are proven by a validator.
- **D24, the badge and the title** (the client): the calling's title on the guild's cards, in the dossier's header and on the roster; the role in the party builder. A role badge from the UI kit (four glyphs: a shield, a bow, a cross, a star) at the corner of a guild member's portrait, and over its body on the map. A calling without a role shows its title alone.

### The technique table (D13)
The words are the effects; numbers are hundredths of a percent where marked "%".

| # | Skill | Band | Technique | Lean | Effects |
| :-: | :--- | :--- | :--- | :--- | :--- |
| 1 | Melee | Apprentice | Shield Wall | cautious | defense +3; attack −15% |
| 2 | Melee | Apprentice | Reckless Fury | bold | attack +25%; defense −2 |
| 3 | Melee | Journeyman | Quick Blade | diligent | pace +20%; attack −10% |
| 4 | Melee | Journeyman | Heavy Blows | bold | attack +20%; pace −15% |
| 5 | Melee | Master | Unyielding | loyal | never flees a blow; defense +2; pace −10% |
| 6 | Melee | Master | Last Stand | bold | attack +40% below half health; defense −2 |
| 7 | Ranged | Apprentice | Steady Aim | cautious | hit +8; pace −15% |
| 8 | Ranged | Apprentice | Quick Draw | bold | pace +20%; hit −5 |
| 9 | Ranged | Journeyman | Long Shot | diligent | range +25%; attack −10% |
| 10 | Ranged | Journeyman | Point Blank | bold | attack +20%; range −20% |
| 11 | Ranged | Master | Deadeye | cautious | hit +10; attack +10%; pace −20% |
| 12 | Ranged | Master | Rapid Fire | bold | pace +30%; hit −8 |
| 13 | Mining | Apprentice | Seam Student | diligent | practice +25%; pace −10% |
| 14 | Mining | Apprentice | Hard Driver | greedy | pace +15%; practice −20% |
| 15 | Mining | Journeyman | Patient Pick | cautious | practice +30%; pace −5% |
| 16 | Mining | Journeyman | Rich Veins | greedy | pace +20%; practice −30% |
| 17 | Mining | Master | Old Hand's Lessons | sociable | teach +25; pace −10% |
| 18 | Mining | Master | Mother Lode | greedy | pace +30%; practice −40% |
| 19 | Foraging | Apprentice | Gentle Hands | cautious | practice +25%; pace −10% |
| 20 | Foraging | Apprentice | Quick Hands | bold | pace +15%; practice −20% |
| 21 | Foraging | Journeyman | Herb Lore | diligent | practice +30%; pace −5% |
| 22 | Foraging | Journeyman | Wide Ranger | greedy | pace +20%; practice −30% |
| 23 | Foraging | Master | Green Teacher | sociable | teach +25; pace −10% |
| 24 | Foraging | Master | Bountiful Harvest | greedy | pace +30%; practice −40% |
| 25 | Smithing | Apprentice | Fine Temper | diligent | grade +2; pace −20% |
| 26 | Smithing | Apprentice | Fast Forge | greedy | pace +20%; grade −2 |
| 27 | Smithing | Journeyman | Studied Hammer | cautious | practice +30%; pace −10% |
| 28 | Smithing | Journeyman | Hot Iron | bold | pace +25%; practice −30% |
| 29 | Smithing | Master | Forge Lessons | sociable | teach +25; pace −10% |
| 30 | Smithing | Master | Masterstroke | diligent | grade +3; pace −25% |
| 31 | Alchemy | Apprentice | Careful Measure | cautious | practice +25%; pace −10% |
| 32 | Alchemy | Apprentice | Quick Brew | bold | pace +15%; practice −20% |
| 33 | Alchemy | Journeyman | Patient Still | diligent | practice +30%; pace −5% |
| 34 | Alchemy | Journeyman | Big Batches | greedy | pace +25%; practice −30% |
| 35 | Alchemy | Master | Still Lessons | sociable | teach +25; pace −10% |
| 36 | Alchemy | Master | Alchemist's Haste | bold | pace +30%; practice −40% |
| 37 | Medicine | Apprentice | Steady Hands | sociable | heal +40%; pace −15% |
| 38 | Medicine | Apprentice | Strong Back | bold | pace +25%; heal −25% |
| 39 | Medicine | Journeyman | Bedside Study | diligent | practice +40%; heal −10% |
| 40 | Medicine | Journeyman | Triage | cautious | heal +30%; practice −30% |
| 41 | Medicine | Master | Surgeon's Lessons | sociable | teach +25; pace −10% |
| 42 | Medicine | Master | Miracle Worker | loyal | heal +60%; pace −20% |

Grades only change what equipment does today, so alchemy's choices are pace and practice, not grade. A grade technique counts levels toward the band that sets a make's grade (Standard, Fine, Masterwork), within 0 to 10.

## 2. The milestones, phased

### S4. Techniques (4–6 days), then the producer's look (folded into their review)
**Complexity:** Very Complex as the epic stamps it (a new system; the effects step supervised). Split here so the table, the pick, each kind of effect, the command and the client land and are checked alone; each step is Possibly Complex.
- [x] **S4.0 The hand-over checked** (done 2026-11-19: §0 matches the code; no finding) [Basic: §0 against the code]
  - Check §0 against the code; a difference becomes a finding (F9 onward).
- [x] **S4.1 The technique table** (done 2026-11-19; golden unchanged) [Possibly Complex: a new table whose ids are saved; no behaviour change]
  - **The table:** D13's `Technique`, `TechniqueTable` (42 rows), `TechniqueStat` and `TechniqueRules` (each stat's bounds).
  - **The record:** the traits record's technique slots read and written by skill and band (`TechniqueIn`, `SetTechnique`).
  - **The checks:** `TechniqueValidator` (two rows at each band of the seven skills; ids unique; each row an upside and a downside; every stat within its bounds; the words name every effect); `TechniqueNamesTests` (production code names a technique only in the table).
  - **AGENTS.md:** "Adding a technique".
  - **Golden:** unchanged.
- [x] **S4.2 The pick by nature** (done 2026-11-19; golden re-recorded for the hash alone) [Possibly Complex: a draw on a new salt, saved; golden moves for the hash]
  - **The pick:** D15, at spawn for the starting talents and at a band reached in practice's one entry; `TechniqueChosen` in the chronicle; `SimulationManager.Seed`.
  - **The checks:** `TechniquePickValidator`: over 10,000 crawlers the bold pick Reckless Fury more often and the cautious Shield Wall; the same crawler picks the same whenever it reaches the band; a band with no rows picks nothing; a filled slot is never picked over; no earlier draw moves (looks, names, skills, passions).
  - **Golden:** the records change, the effects not yet live: re-recorded in its own commit, named as the hash's.
- [x] **S4.3 The door, and the fight's numbers** (done 2026-11-19; golden unchanged; three fight DoDs moved) [Possibly Complex: a behaviour change in combat, read by cognition too]
  - **The door:** D14's `CrawlerTechniques.Get`.
  - **The fight:** `Combatant.ForCrawler` takes the traits (both overloads; cognition's `Vitality.HarmToClear` passes its copy): attack, defense (never below none), hit, pace on the swing's interval, range, last stand.
  - **The checks:** `TechniqueEffectsValidator`, a test per fight stat (Shield Wall's holder takes less from a blow and deals less; Reckless Fury the other way; a quick blade swings sooner; a long shot reaches farther; last stand only below half health; the bounds hold however techniques stack); `TechniqueReadsTests` (effects are read only through the door).
  - **Golden:** re-recorded in its own commit.
- [x] **S4.4 The work's numbers** (done 2026-11-19; golden re-recorded) [Possibly Complex: behaviour in the harvest, the stations, the rescue, practice and mentors]
  - **Pace:** the harvest's (`SkillSpeed`, which the salvage choice reads through cognition's copy), the make's, the carry's.
  - **The rest:** grade (levels toward the make's grade), heal (a medicine given), practice (in `PractiseOwn`, beside the passion), teach (a mentor's lesson share).
  - **The checks:** `TechniqueEffectsValidator`, a test per stat.
  - **Golden:** re-recorded in its own commit.
- [x] **S4.5 The rule flag: unyielding** (done 2026-11-19; golden unchanged) [Possibly Complex: a rule cognition reads]
  - **The rule:** a crawler holding it fights back when struck, bold or not (`Vitality.FightsBack`), if armed.
  - **The check:** a validator: the cautious and armed flee a blow without it and stand with it.
  - **Golden:** unchanged (nobody reaches Master), or re-recorded in its own commit.
- [x] **S4.6 Sponsorship** (done 2026-11-19: the rule is `SponsorRules`, beside `GuildRules`, since the guild owns the patron's people; golden unchanged) [Possibly Complex: a new command with a rule and a transfer]
  - **The command and rule:** D16's `SponsorTechniqueCommand`, `TechniqueRules.CheckSponsor` and `Describe` (each refusal's words), applied on the main thread in command order.
  - **The checks:** `SponsorValidator`: each refusal; a sponsorship paid from the stall into the purse, the ledger balanced; the slot changed and told; a band sponsored back costs again.
  - **The economy model:** §2 gains sponsorship, a transfer.
  - **Golden:** unchanged (no player sponsors).
- [x] **S4.7 Re-measured** (done 2026-11-19: `docs/skills-model.md` §5.6; two DoDs re-picked, F9, F10; core 312/312; soak 18/18 and 20/20 x3) [Possibly Complex: measuring, moved DoDs]
  - **Again:** S0.3's measurement (a fourth time), with the techniques picked and their effects; which techniques are held at day 14, by band.
  - **Every scenario:** a DoD whose seed moves is re-picked from a measurement across the gate seeds, never one seed, in its own commit. The soak's 18 runs and the three player profiles.
  - **Findings** for anything that surprises.
- [x] **S4.8 The client: the choice scene** (done 2026-11-19: `SkillScenes`, a moment trigger; `SkillSceneTests`) [Possibly Complex: a new kind of scene trigger, built from an event]
  - **The trigger:** D17's moment, its script built from the chronicle event (the vow, the road not taken); the patron's people only, a game hour apart; armed by `ClientOptions.SkillScenes`.
  - **The checks:** a presentation test (the trigger fires for a member's pick, not a resident's; its lines name both techniques); a client test that it plays in the window.
- [x] **S4.9 The client: techniques in the dossier, and the sponsor button** (done 2026-11-19: the sway's second button sponsors one of the patron's people; renders at S4.11) [Possibly Complex: Gum, renders]
  - **The dossier:** D18's lines, and the sponsor button with the price and the rule's reason.
  - **The checks:** `TechniquesOnScreenTests`. Renders recorded in the pinned image after a look, in their own commit.
- [x] **S4.10 DoD `ATechniqueDecidesAClear`** (done 2026-11-19: gate seed 3; 17 of 23) [Possibly Complex: a live scenario]
  - **The run:** the guild player builds the drill yard and its members drill; on a seed S4.7's measurement picks, a member reaches a band during the run and picks a technique on its own; then the player's clear.
  - **What emerges:** the member holds the technique it picked (told in the chronicle, not sponsored); the party's clear empties the lair; the member fought in it, its technique in every blow it landed or took (its fighter with the technique differs from its fighter without).
  - **The days hold.**
- [x] **S4.11 S4's gate and docs; the look's brief** (done 2026-11-19: core 313/313 (explicit not run), client 531/531 in the pinned image, golden 3/3, seeds 1–120 120/120 both ways, MonoGame and Android 0 warnings; the look's brief in the epic's §4) [Basic]
  - **The gate:** the index's gates.
  - **Docs:** the epic's §0 (D13–D18) and §4 (S4); `skills-model.md` (techniques); AGENTS.md ("Adding a technique"); the look's brief in the epic's §4, for the producer's review on return.

### S5. Callings (3–4 days)
**Complexity:** Very Complex as the epic stamps it (bundling: a skill, a table and orders). Split here so the skill, the table, the rule, the roles and the capstones each land alone; each step is Possibly Complex.
- [x] **S5.1 Leadership, an eighth skill** (done 2026-11-19: no primer for it either; golden re-recorded) [Possibly Complex: a skill row by AGENTS.md's recipe, but a new kind of work, its practice in the guild's expeditions and every seed's draw]
  - **The row and the work:** D19's `Skill.Leadership`, `SkillUse.Lead`, `SkillWork.Lead()`.
  - **The captain** of a party out; practice at a trip's delivery; the retreat rules' line falling by its level.
  - **The checks:** `SkillsValidator` (the row); `LeadershipValidator` (the captain chosen; practice at delivery by the trip's challenge; the line steadied by level); `SkillProofTests` still adds Mycology as a row.
  - **Golden:** re-recorded in its own commit (every seed's draw moves). The DoDs it moves are re-picked at S5.6, with S5.4's.
- [x] **S5.2 The calling table** (done 2026-11-19: "Delver" is Rockbreaker, Delver being a look lineage) [Possibly Complex: a new table whose ids are saved]
  - **The table:** D20's `Calling`, `CallingTable` (15 rows), `PartyRole`.
  - **The checks:** `CallingValidator` (every Apprentice technique earns one calling, every row's skill and technique agree, Captain needs none); `CallingNamesTests`.
  - **AGENTS.md:** "Adding a calling".
- [x] **S5.3 A calling earned** (done 2026-11-19: a calling keeps its skill while that skill ties the top; golden re-recorded for the hash) [Possibly Complex: a rule over bands and techniques, saved]
  - **The rule:** D21's `CallingRules.Earned`, judged at spawn, at a band reached, at a pick and at a sponsorship; saved in the record; `CallingEarned` in the chronicle.
  - **The checks:** a validator: the best skill decides; ties by experience, then display order; an Apprentice technique sponsored away changes the calling; a skill below Apprentice earns none.
  - **Golden:** the hash moves: re-recorded in its own commit.
- [x] **S5.4 Party roles the guild's orders read** (done 2026-11-19; golden unchanged) [Possibly Complex: the guild's orders and the creatures' rule]
  - **The orders:** D22's role in the order copy; `PlanArms` by role; `ChooseCarrier` by role; the captain (a Captain first) for the focus.
  - **The front:** the creatures' rule, as D22.
  - **The checks:** `PartyRoleValidator`: a Front member draws a creature's notice and turns a creature off its mate; a Back member is armed to shoot; a Carrier is sent first; nothing changes outside a clear's fight.
  - **Golden:** re-recorded in its own commit if it moves.
- [x] **S5.5 Capstones and the Master title** (done 2026-11-19; golden unchanged) [Possibly Complex: rows read through the door]
  - **The rule:** D23, a calling's capstone through `CrawlerTechniques.Get` at Master in its skill.
  - **The check:** a validator (a Master Warden's defense; the title; nothing below Master).
- [x] **S5.6 Re-measured** (done 2026-11-19: `docs/skills-model.md` §5.7; five DoDs re-picked, F11; two fixtures read the rule's own numbers) [Possibly Complex: measuring, moved DoDs]
  - **The measurement:** who holds which calling at day 14 on the gate seeds; roles in clears (how often a Front member takes the blows); Leadership's growth.
  - **Every scenario:** moved DoDs re-picked across the gate seeds, each in its own commit, S4's DoD among them.
- [ ] **S5.7 The client: titles and roles** [Possibly Complex: Gum, renders]
  - **Where:** the calling's title on the guild's cards, in the dossier's header and on the roster; the role in the party builder.
  - **The checks:** `CallingsOnScreenTests`; renders after a look.
- [ ] **S5.8 The client: the badge** [Possibly Complex: UI kit pieces, art pins, a draw item, renders]
  - **The glyphs:** four role glyphs in the UI kit; at the corner of a member's portrait (dossier, cards) and over its body on the map.
  - **Pins and renders:** the art review sheet looked at, the art pins re-recorded; renders in the pinned image after a look, in their own commits.
- [ ] **S5.9 DoD `ACallingEmerges`** [Possibly Complex: a live scenario]
  - **The run:** the guild player with the drill yard and a clear, on a seed S5.6's measurement picks.
  - **What emerges:** a member becomes a Warden during the run, on its own (melee its best skill, Shield Wall picked); at the clear's fight it holds the front: it takes more of the creatures' blows than any mate, and the clear empties the lair.
  - **The days hold.**
- [ ] **S5.10 S5's gate and docs** [Basic]
  - **Docs:** the epic's §0 (D19–D24) and §4 (S5); `skills-model.md` (Leadership, callings, roles); AGENTS.md ("Adding a calling", Leadership in the skills paragraph).

### C. The plan's close-out and PR (1 day)
**Complexity:** Possibly Complex: the whole plan re-measured once. The PR itself is Basic.
- [ ] **C.1 The Technique and Calling gate** [Possibly Complex: measuring]
  - **The runs:** every scenario; seeds 1–120, with and without the guild; the soak.
  - **Growth:** S0.3's measurement a fifth time, with techniques and callings; and once over 28 days on the gate seeds, to see whether anyone reaches Journeyman twice or Master (F1, F8) before plan 3 is drafted.
- [ ] **C.2 The close-out** [Basic: a list this plan already holds]
  - **The roadmap first,** then the epic's status and §4, the focus file, `architecture_history.md`, AGENTS.md, the skills and economy models.
  - **This plan:** its findings move to the epic, then it is deleted (the index stays).
  - **The PR:** `git push origin feat/epic11-technique` and a PR into `main`. **Not merged** without the producer's word.

## 3. For the producer's review on return
The producer is away for the whole plan. What they would have looked at or supervised is gathered here, and the PR description repeats it:
- **The look at S4** (the epic's §4 holds the brief): the choice scene, techniques in the dossier, the sponsor button.
- **The steps the epic marks supervised** (S4's effects): S4.3–S4.5, each with its validator. The technique table's numbers (D13) are first guesses, to tune.
- **Every default D13–D24,** by exception.

## Log
- 2026-11-19: plan 1 merged (PR #25, `aceb183`) on the producer's word; this plan drafted on `feat/epic11-technique` and started at once ("After drafting plan 2 you will begin immediate execution and will attempt to complete it").
- 2026-11-19: S4 done; its look waits for the producer's return (the brief in the epic's §4). S5 begins.

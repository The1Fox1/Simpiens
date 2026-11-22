# The Skills Model

- **Owner:** [Epic 11](epics/epic-11.md), the skill system (Milestone 5). It builds on Epic 10 Part 3's K9, the skill foundation, and K9.8, its seam. Status is owned by the [roadmap](ROADMAP.md).
- **What this file is:** the skills' rules and numbers as the game has them, and the measurements behind them, as [docs/economy-model.md](economy-model.md) is for the economy and [docs/time-model.md](time-model.md) for the day. Each Epic 11 milestone updates it as its rules land.
- **The code:** `Simpiens.Core/Simulation/Skills/` (`SkillTable`, `SkillRules`, `SkillSet`, `SkillWork`, `CrawlerSkills`, `ISkillPractice`).

## 1. Skills, levels and bands
- **Skills are a table** (`SkillTable`, capacity 16, ids never reused). Each row holds the player's word, the work that practises it, the personality that leans its draw, its practitioner's word for the chronicle, and its place in lists.

| Skill | Practised by | Lean | Practitioner |
| :--- | :--- | :--- | :--- |
| Melee | a fight in reach | bold | fighter |
| Ranged | a fight from range | cautious | marksman |
| Mining | harvesting minerals | diligent | miner |
| Foraging | harvesting botanicals | — | forager |
| Smithing | making at the forge | diligent | smith |
| Alchemy | making at the still | — | alchemist |
| Medicine | tending the fallen | sociable | medic |

- **Levels** run from 0 to 10. A level costs 100 times its number: 100 experience for the first, 1,000 for the tenth, and 5,500 for all ten.

| Band | Levels | Experience to reach it |
| :--- | :--- | :--- |
| Novice | 0–2 | 0 |
| Apprentice | 3–5 | 600 |
| Journeyman | 6–8 | 2,100 |
| Master | 9–10 | 4,500 |

- **A crawler's skills** are its `SkillSet`: 16 experiences, 32 bytes, saved whole in its record. A skill added later loads at 0 from an older save.

## 2. The draw at spawn
On its own salt (`SimRandomSalt.Skills`), for residents and drifters alike:
- **Every skill starts as a Novice's:** 0 (half of them), 1 or 2.
- **One talent, or two** (a coin flip), starts at an Apprentice's 3, 4 or 5. Each is picked among the skills at odds of 1 + 2 × the personality's lean toward the row (the bold lean toward melee), and a skill is a talent once at most.
- **No look enters the draw.**

## 3. Practice
- **One entry:** `ISkillPractice.Practise(agent, work, experience, source)`, implemented by `SimulationManager.Practise` on the main thread.
  - **Which skill:** the work names it (`SkillWork`), and the table answers (`SkillTable.SkillFor`). Code never names a skill (`SkillNamesTests`).
  - **What happens:** the experience is added up to the source's ceiling. A level gained is logged, and a band reached is told in the chronicle.
- **What each act is worth today:**

| Act | Experience | Where it is recorded |
| :--- | :-: | :--- |
| A blow landed in reach | 10 | the creatures' pass (`CreatureService.Rule.Land`) |
| A shot loosed, hit or miss | 8 | the creatures' pass |
| A unit of salvage harvested | 10 | `HarvestResourceIntentHandler` |
| A make finished at a station | 60 | `WorkshopService` |
| A fallen crawler carried out, or stood up with a medicine | 100 | `CarryIntentHandler` |

- **Sources:** `PracticeSource` is a kind and a ceiling. Today's only kind is the work itself, which teaches to 10.

### 3.1 Passions (Epic 11, S1)
- **Three steps** (`PassionTable`): none, keen and burning. A passion multiplies every act of practice at its skill (×1.5 keen, ×2 burning), eases frustration with each act (2, 4), and weighs the salvage choice toward its work (×1.15, ×1.3).
- **Drawn at spawn** on their own salt, after the skills: a burning passion with a quarter's chance, then none (40%), one (40%) or two (20%) keen ones. Each is picked at odds of the skill's lean (as talents are), doubled for a starting talent; one passion a skill at most.

### 3.2 Practice by challenge (Epic 11, S1)
Each act names the level it demands (`PracticeSource.Challenge`). A crawler at or below it takes the whole act; above it, less:

| Levels above the challenge | 0 or less | 1 | 2 | 3 | 4 | 5 | 6 | 7+ |
| :--- | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: |
| Share of the act | 100% | 75% | 55% | 40% | 25% | 20% | 15% | 10% |

| What is practised on | Challenge |
| :--- | :-: |
| Rats | 2 |
| Spinners | 5 |
| Beetles | 7 |
| The Matriarch | 9 |
| The Gatekeeper | 10 |
| Herbs | 3 |
| Ore | 4 |
| Glowstone | 7 |
| A pickaxe | 3 |
| An iron helm, a leather jerkin | 4 |
| An iron blade, a hard hat, a healing draught (the still's only recipe) | 5 |
| A ring coat, a respirator, a crossbow | 6 |
| A glowstone edge, a chitin plate | 7 |
| A rescue | always the whole act |

The order in practice's one entry: the act's experience, times the passion, times the challenge's share (rounded to the nearest), then held to the source's ceiling and the mastery budget (§3.3), whichever is lower.

### 3.3 The mastery budget (Epic 11, S2)
- **The rule** (`SkillRules.BudgetCeiling`, D7): at most two skills past Apprentice (6 or more) and one at Master (9 or more). A third skill stops at 5 while two others stand past Apprentice; a second stops at 8 while another stands at Master.
- **First come, first served:** the skills that got there first hold the slots, in the order practice arrives. Nobody gives a skill up, and a skill already past its budget keeps what it has.
- **Starting skills never break it:** a starting talent is at most 5.
- **Checked:** `SkillsValidator` test 7 proves the hold. `OutpostWatch` fails any live scenario's day if a crawler holds more than the budget at any tick, and counts the crawler-ticks a skill stands held.
- **The dossier** says so: "Foraging held at 5: two crafts past Apprentice", or "held at 8: a Master already".

### 3.4 The asking wage (Epic 11, S2)
- **The rule** (`GuildRules.AskingWage`, `WorkshopRules.AskingWage`, D8): a member's or an artisan's reservation wage times its best band's raise: ×1 to Apprentice, ×1.25 for a Journeyman, ×1.5 for a Master. With the neutral 5g reservation: 6g and 8g. The best band is read through the seam (`CrawlerSkills.BestBand`).
- **Signing:** a recruit or a hire signs for no less.
- **At the caravan's visit:** a hand paid in full but below what it asks takes the unpaid wage's trust and warmth (−10, −5), with no unpaid visit counted. It quits only when its trust falls under the line, after about three such visits.
- **Told once:** when a band reached lifts the asking wage above the contract's, the chronicle tells the ask ("asks for a raise, to 6g a visit").
- **The scripted players** pay each raise when it is asked, within their reserves; `LeaveRaisesUnpaid` keeps the signed wage.
- **On screen:** "asks 6g" in amber on the guild's card, in the dossier and on the station window's artisan line.

### 3.5 Teachers (Epic 11, S3)
Every source goes through practice's one entry, so a passion multiplies it and the mastery budget holds it, as the work's own.

| Source | Who, when | How much | To |
| :--- | :--- | :--- | :-: |
| The work (`Work`) | doing it | the act's worth (§3), times the challenge's share | 10 |
| A mentor (`Mentor`, D9) | practising a work beside a hand at least 3 levels better at the same work, who did it within the game hour, within 6 units | half the act | the mentor's level less 2 |
| A manual (`Manual`, D11) | a resident who loves the manual's skill buys it (25g, from a spare purse) and reads it at the hearth for 2 game hours | 300 at the end of the reading; nothing if interrupted | 3 (a primer) |
| The drill yard (`Yard`, D10) | a guild member with no orders, fed and rested, in a yard the player built (200g) | 30 a game hour, in six lots, at the fight work of what it wields | 5 |

- **Lineage:** the first lesson in a skill records the mentor in the learner's traits record (four slots) and counts the mentor's "taught" deed; the chronicle tells it ("learned foraging at Vesna's side"), and the dossier says "Foraging taught by Vesna".
- **Mentors aren't saved:** each crawler's last practice is work in progress, so after a load nobody teaches until it works again.

### 3.6 Techniques (Epic 11, S4)
- **A choice at each band** (`TechniqueTable`, D13): two techniques at each band above Novice of each of the seven skills, 42 in all, each with an upside and a cost. A crawler holds one per band of each skill, in its traits record.
- **The pick** (D15): at a band reached, and at spawn for the talents, the crawler picks one of the band's two by its nature: each weighted 1 + 2 × its lean in the crawler's personality, on its own salt, the same whenever it gets there. With a lean of 0.8 toward one and none toward the other, about 72% pick the first. A slot held is never picked over.
- **Sponsorship** (D16): the patron may pay one of its people to take up the band's other: 20g, 40g, 80g by band, into the crawler's purse. It refuses under trust 50 or warmth 0, or a technique whose lean in its personality is −0.5 or less.
- **The effects** (D14) are numbers in the technique's own skill's work, read through one door (`CrawlerTechniques.Get`), summed over the techniques held and held within bounds:

| Stat | In | Bounds |
| :--- | :--- | :-: |
| Attack (%) | a fight | −50 to +100 |
| Defense (flat, never below none) | a fight | −5 to +10 |
| Hit (hundredths) | a fight | −20 to +20 |
| Pace (%): a swing's interval, a harvest, a make, a carry | any work | −50 to +100 |
| Range (%) | a fight from range | −50 to +50 |
| Last stand (% attack below half health) | a fight | 0 to +100 |
| Unyielding (a flag: struck, it fights back if armed, bold or not) | a fight in reach | 0 or 1 |
| Practice (%), beside the passion | any work | −50 to +100 |
| Grade (levels toward a make's grade, within 0 and 10) | making | −3 to +3 |
| Heal (% of a medicine given) | tending | −50 to +100 |
| Teach (points on a mentor's 50%) | any work | 0 to +50 |

The techniques (first guesses, to tune):

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

### 3.7 Leadership (Epic 11, S5.1)
- **An eighth skill**, practised by leading a party out and home. Leaned by loyalty in the draw, as the others by theirs.
- **The captain** of a party out is the member on its feet with the best Leadership, the lowest id on a tie (a Captain by calling has the best Leadership by definition, being its best skill).
- **Practice:** at a trip's delivery the captain takes 40 for each mate brought home, at the trip's challenge (a gathering 3, a clear its lair's kind's: rats 2, beetles 7).
- **What a level does:** the captain steadies its party: the retreat rules' health line, 3% lower a level (Steady's 55 is 46.75 under a Leadership 5 captain). The party's focus is chosen nearest the captain.

### 3.8 Callings and party roles (Epic 11, S5)
- **A calling is earned** from the crawler's best skill (the highest learned level, then the most experience, then the display order) at Apprentice or above and the Apprentice technique it took there. It keeps its skill while that skill ties the top. Every resident holds one from spawn.
- **At Master** in its skill a calling takes its Master title and its capstone, an effect through the techniques' door.

| Skill | Technique | Calling | Role | Master title | Capstone |
| :--- | :--- | :--- | :--- | :--- | :--- |
| Melee | Shield Wall | Warden | front | Master Warden | +3 defense |
| Melee | Reckless Fury | Berserker | front | Warlord | +20% attack |
| Ranged | Steady Aim | Marksman | back | Master Marksman | +5 to hit |
| Ranged | Quick Draw | Skirmisher | back | Master Skirmisher | 15% faster |
| Mining | Seam Student | Prospector | — | Master Prospector | 15% faster |
| Mining | Hard Driver | Rockbreaker | — | Master Rockbreaker | 15% faster |
| Foraging | Gentle Hands | Herbalist | — | Master Herbalist | 15% faster |
| Foraging | Quick Hands | Gleaner | — | Master Gleaner | 15% faster |
| Smithing | Fine Temper | Smith | — | Master Smith | 15% faster |
| Smithing | Fast Forge | Forgehand | — | Forgemaster | 15% faster |
| Alchemy | Careful Measure | Apothecary | — | Master Apothecary | 15% faster |
| Alchemy | Quick Brew | Brewer | — | Master Brewer | 15% faster |
| Medicine | Steady Hands | Field Surgeon | carrier | Master Surgeon | +20% healing |
| Medicine | Strong Back | Bearer | carrier | Master Bearer | 20% faster carrying |
| Leadership | (any) | Captain | leader | Commander | none yet |

- **The roles the guild's orders read** (a member's order carries its calling's role):
  - **Front:** at a clear's fight a creature choosing its quarry sees a Front member 3 units nearer than it is, and a creature chasing a mate of the same fight turns on a Front member within 3 units of it.
  - **Back:** the clear's arms give the Back a weapon with a range first, as many as the order asks shooters.
  - **Carrier:** the guild sends a Carrier to a fallen mate before the healthiest.
  - **Leader:** the party's captain (§3.7).

## 4. What a level does
How good a crawler is at a work is its **effective level**, `CrawlerSkills.Level(skills, worn, work)`, the one number every effect and choice reads. Today it equals the learned level. Gear, techniques or callings that change how good a crawler is enter there (`SkillReadsTests` guards it). A technique doesn't raise a level: it changes the work's numbers below through its own door (§3.6).

| Work | Per level | At 0 | At 10 |
| :--- | :--- | :--- | :--- |
| Fighting (either way) | hit chance +2 points; damage +2% | 70% hit chance, base damage | 90% (capped at 95%), +20% damage |
| Harvesting | pace +4 points from 92%, multiplied by a pickaxe's | 92% | 132% |
| Making | pace +5%; the grade is the band's | Standard | 150% pace, Masterwork from Journeyman |
| Tending | carrying pace +3 points from half; medicine heals +5% | half pace | 80% pace, +50% healing |

The harvest alone is centred on a Novice 2 (D51 of Epic 10 Part 3): the outpost's starting hands together bring in what they did before skills.

## 5. Measurements

### 5.1 Before Epic 11 (S0.3, 2026-11-16)
`SkillGrowthMeasurementTests.MeasureGrowthOnTheGateSeeds`: the 23 gate seeds over 14 game days, 12 residents each, drifters left out. One run with no player, and one with the soak's `hunting` players (the forge making blades and ring coats, the still, the hunt player, and a guild of four clearing with draughts, each keeping a 150g reserve).

| | No player | Hunting players |
| :--- | :--- | :--- |
| Seeds where someone reaches Journeyman | 4 of 23 (from day 11.7) | 14 of 23 (from day 5.2) |
| Residents holding a Journeyman's skill at day 14 | 0.26 a seed (most 3) | 0.91 a seed (most 3) |
| Seeds where someone reaches Master | 0 | 0 |
| Residents with two skills past Apprentice | 0 | 0 |
| Highest level reached | 6 (mining) | 8 (alchemy) |

Levels gained a seed, by skill, with the bands reached across all 23 seeds:

| Skill | No player | Hunting players |
| :--- | :--- | :--- |
| Melee | 0.0 | 2.0 (4 to Apprentice, 1 to Journeyman) |
| Ranged | 0.0 | 0.0 |
| Mining | 17.5 (113 to Apprentice, 6 to Journeyman) | 13.0 (56 to Apprentice, 3 to Journeyman) |
| Foraging | 12.0 (31 to Apprentice) | 9.2 (17 to Apprentice) |
| Smithing | 0.0 | 3.8 (20 to Apprentice, 6 to Journeyman) |
| Alchemy | 0.0 | 3.7 (17 to Apprentice, 11 to Journeyman) |
| Medicine | 0.0 | 0.0 |

Practice a seed with the hunting players: mining about 6,150 experience, foraging 3,930, alchemy 1,900, smithing 1,710 and melee 690. Ranged and medicine get none.

**What it means for Epic 11's Craft:**
- **Growth is slow and narrow.** The harvest carries the outpost's growth. Artisans are the only ones who reach Journeyman early, because a make is worth 60. Fighters gain a level or two in two weeks.
- **The mastery budget wouldn't bind** (finding F1): nobody holds even two skills past Apprentice in 14 days. Passions (×1.5, ×2) and teachers speed growth, so S1.5 measures again. Unless something changes, `SpecialistsEmerge` can't ask for a skill held by the budget within 14 days.
- **The asking wage would come early for artisans:** the forge's and the still's hands reach Journeyman on most seeds where they are hired, from day 5.
- **Ranged and medicine never grow** (finding F2): the hunting players arm blades, and no rescue happened in these runs. Teachers (manuals, the yard) are their only road until the guild arms bows.

### 5.2 With passions and practice by challenge (S1.5, 2026-11-16)
The same measurement after S1.1–S1.4: passions drawn at spawn, practice ×1.5 or ×2 at a passion, practice scaled by challenge, and passions weighing the salvage choice.

| | No player | Hunting players |
| :--- | :--- | :--- |
| Seeds where someone reaches Journeyman | 4 of 23 (from day 9.8) | 15 of 23 (from day 4.5) |
| Residents holding a Journeyman's skill at day 14 | 0.17 a seed (most 1) | 0.96 a seed (most 3) |
| Seeds where someone reaches Master | 0 | 0 |
| Residents with two skills past Apprentice | 0 | 0 |
| Highest level reached | 6 (mining) | 7 (smithing and alchemy) |

Levels gained a seed with the hunting players: melee 2.0, mining 14.0, foraging 9.3, smithing 4.5 (9 reached Journeyman across the seeds), alchemy 4.1 (10). Ranged and medicine still get no practice.

**What it means:**
- **Passions widen growth, while challenge caps it.** More crawlers reach Apprentice (mining 67 against 56, foraging 21 against 17), but past Apprentice the common seams and the pickaxe teach less, so the top of the curve barely moves.
- **The budget still wouldn't bind** (F1, settled here): two skills past Apprentice would need a crawler to reach Journeyman twice, and nobody does it once on most seeds. `SpecialistsEmerge` (S2) asserts the rule at every tick and the asking wage live. The budget's own outcome is proven by a validator.

### 5.3 Specialists and their wages (S2.6, 2026-11-17)
`SpecialistsEmergeTests.MeasureSpecialistsOnTheGateSeeds`: 14 game days on the 23 gate seeds, with the guild profile's player (four members sent to the Drowned Gallery) and the still's player (draughts, a 150g reserve), both paying raises.

| | Gate seeds |
| :--- | :--- |
| Someone reaches Journeyman | 23 of 23 (from day 4.3); the still's alchemist first on all but one (a miner on seed 9) |
| A member or an artisan asks for a raise | 8 of 23, each paid the next tick |
| A skill held by the budget | 0 |
| The days hold | 23 of 23 |

**What it means:**
- **The still makes specialists.** Its artisan is hired as the best alchemist at hand, practises the draught at its own challenge (5), and every make teaches it fully until 5. The guild's members don't reach Journeyman in two weeks.
- **A thrifty hand asks no more.** On the other 15 seeds the first Journeyman's raise rounds to the 5g default wage or under: its reservation wage is below 5g.
- **The 3-day soak** sees no raise due on any profile (economy model §7), and over 14 days the `crafting` and `hunting` guilds fall apart for want of pay on most seeds with no specialist under contract: the open loop's strain, not upkeep's (economy model §8).

### 5.4 Teachers (S3.6, 2026-11-17)
`AMasterTeachesAnApprenticeTests.MeasureTeachersOnTheGateSeeds`: 14 game days on the 23 gate seeds, with the guild profile's player building the drill yard (a 150g reserve besides) and the primers on the shelf.

| | Gate seeds |
| :--- | :--- |
| A learner taught to Apprentice, its teacher recorded | 22 of 23 (from day 1.9; 9 to 82 lessons a seed) |
| Its teacher's band | Apprentice on all 22 (3 to 5), or since gone; never a Master |
| A primer bought and read | 23 of 23 (from t3473, at the caravan's first visit) |
| The yard built, and a member drilling there | 23 of 23 (built from day 0.75, drilled within the game day after) |
| All three, the days held | 22 of 23 |

**What it means:**
- **Mentors teach at the seams.** The lessons fall where miners and foragers work side by side: 16 of the 22 learners reached Mining 3, 6 Foraging 3. Nobody reaches Master in two weeks (F1), so the epic's "a Master teaches an Apprentice" is, today, an Apprentice 5 teaching a Novice 2.
- **Primers sell early.** Someone who loves a craft buys and reads its primer within the hour after the caravan's first visit on 8 seeds, and within days on the rest. Ranged and medicine (F2) are bought and read too: the first road into either skill.
- **The yard is drilled in melee:** members carry no ranged weapon in the guild profile, so drill raises melee.


### 5.5 Craft complete (C.1, 2026-11-17)
S0.3's measurement a third time, on plan 1's final build: passions, practice by challenge, the mastery budget, the asking wage, mentors and primers on the shelf. The `hunting` profile doesn't build the drill yard, so neither run drills. These are the curves plan 2's techniques are tuned on.

| | No player | Hunting players |
| :--- | :--- | :--- |
| Seeds where someone reaches Journeyman | 5 of 23 (from day 12.0) | 19 of 23 (from day 4.4) |
| Residents holding a Journeyman's skill at day 14 | 0.22 a seed (most 1) | 1.13 a seed (most 3) |
| Seeds where someone reaches Master | 0 | 0 |
| Residents with two skills past Apprentice | 0 | 0 |
| Highest level reached | 6 (mining) | 7 (smithing and alchemy) |
| Deaths | 0 | 0 |

Levels gained a seed, by skill, with the bands reached across all 23 seeds:

| Skill | No player | Hunting players |
| :--- | :--- | :--- |
| Melee | 2.7 (27 to Apprentice) | 4.6 (27 to Apprentice) |
| Ranged | 2.7 (27 to Apprentice) | 2.9 (30 to Apprentice) |
| Mining | 20.0 (137 to Apprentice, 5 to Journeyman) | 14.3 (72 to Apprentice, 2 to Journeyman) |
| Foraging | 14.2 (54 to Apprentice) | 10.0 (35 to Apprentice) |
| Smithing | 1.6 (15 to Apprentice) | 5.6 (32 to Apprentice, 7 to Journeyman) |
| Alchemy | 1.6 (15 to Apprentice) | 6.0 (35 to Apprentice, 17 to Journeyman) |
| Medicine | 1.6 (13 to Apprentice) | 1.6 (15 to Apprentice) |

Practice a seed with the hunting players: mining about 6,740 experience, foraging 4,350, alchemy 2,680, smithing 2,220, melee 1,480, ranged 650 and medicine 360.

**Across plan 1** (hunting players):

| | S0.3, before | S1.5, passions and challenge | C.1, craft complete |
| :--- | :-: | :-: | :-: |
| Seeds with a Journeyman | 14 of 23 | 15 of 23 | 19 of 23 |
| Journeymen held at day 14, a seed | 0.91 | 0.96 | 1.13 |
| Skills that grow at all | 5 of 7 | 5 of 7 | 7 of 7 |
| Highest level | 8 | 7 | 7 |

**What it means for plan 2:**
- **Teachers widen the base, not the top.** Every skill now grows, with or without a player. With no player, smithing, alchemy and medicine each take about 360 experience a seed, about one primer read: primers are the road into those skills and into ranged (F2 answered for the start). With no player, mining's Apprentices rose from 113 (S0.3) to 137, with passions and the mentors at the seams.
- **The top stays where it was.** Nobody reaches Master in two weeks and nobody holds two skills past Apprentice, so the budget still doesn't bind (F1) and every teacher is an Apprentice (F8). The artisans are the outpost's specialists: of the 26 Journeyman bands reached in the hunting run, 17 are the still's alchemy and 7 the forge's smithing.
- **For techniques (S4):**
  - *At Apprentice:* every resident starts with one or two talents there, and 11 to 12 more Apprentice bands a seed are reached in two weeks (246 in the hunting run, 288 with no player). A technique at Apprentice is chosen by everyone, mostly at spawn or in the first days.
  - *At Journeyman:* about one resident a seed in two weeks with the players, mostly an artisan, and almost never a fighter; a fifth of one with no player.
  - *At Master:* never in two weeks.
  - Plan 2 tunes on these numbers, or measures over a longer horizon.

### 5.6 With techniques (S4.7, 2026-11-19)
S0.3's measurement a fourth time, on plan 2's S4 build: a technique picked at each band reached (and at spawn for the talents), its effects on the fight and the work (§6). Melee and ranged are scored the same way as before; the counts of techniques come from the traits records at day 14 against day 0.

| | No player | Hunting players |
| :--- | :--- | :--- |
| Seeds where someone reaches Journeyman | 3 of 23 (from day 8.0) | 20 of 23 (from day 4.4) |
| Residents holding a Journeyman's skill at day 14 | 0.13 a seed (most 1) | 1.22 a seed (most 3) |
| Seeds where someone reaches Master | 0 | 0 |
| Residents with two skills past Apprentice | 0 | 0 |
| Highest level reached | 6 (mining) | 8 (alchemy) |
| Deaths | 0 | 0 |
| Techniques picked in the run, a seed | 12.8 (292 at Apprentice, 3 at Journeyman) | 12.1 (250 at Apprentice, 28 at Journeyman) |

With the hunting players, levels gained a seed: melee 4.7 (2 reached Journeyman, the first melee Journeymen in any measurement), ranged 2.7, mining 14.6 (2), foraging 10.4, smithing 5.1 (8), alchemy 6.3 (16), medicine 2.0. The first Journeyman of a seed is the still's alchemist on 13 of the 20 where one is made, a smith on 4, a fighter on 2 and a miner on 1.

**Which techniques are held** (hunting players, at day 14, with how many were picked during the run): each Apprentice pair splits by nature, close to even across the outpost: Shield Wall 41 (11) and Reckless Fury 49 (18); Steady Aim 57 (18) and Quick Draw 49 (9); Seam Student 70 (36) and Hard Driver 65 (38); Gentle Hands 41 (20) and Quick Hands 37 (18); Fine Temper 55 (18) and Fast Forge 42 (13); Careful Measure 41 (18) and Quick Brew 41 (15); Steady Hands 41 (12) and Strong Back 36 (6). At Journeyman, 28 picks in all, mostly the artisans': Patient Still 9, Big Batches 7, Hot Iron 5, Studied Hammer 3, Heavy Blows 2. No Master technique.

**What it means:**
- **Techniques change who grows, not how far.** The students' +25–30% practice and the drivers' −20–30% roughly cancel across the outpost. The top moves a step (a Journeyman on 20 seeds against 19; the highest level 8 against 7), and still nobody reaches Master or a second skill past Apprentice (F1, F8).
- **Apprentice techniques are everyone's,** from spawn: every talent holds one, and 11–13 more are picked a seed in two weeks. They are what the player sees and sponsors.
- **Journeyman techniques are the artisans'.** Master techniques and Unyielding are proven by validators only.

### 5.7 With Leadership and callings (S5.6, 2026-11-19)
The same measurement on S5's build: Leadership an eighth skill (in every draw, practised by a party's captain), callings earned, party roles in the guild's orders.

| | No player | Hunting players |
| :--- | :--- | :--- |
| Seeds where someone reaches Journeyman | 4 of 23 (from day 12.3) | 18 of 23 (from day 4.0) |
| Residents holding a Journeyman's skill at day 14 | 0.17 a seed (most 1) | 1.43 a seed (most 3) |
| Seeds where someone reaches Master | 0 | 0 |
| Residents with two skills past Apprentice | 0 | 0 |
| Highest level reached | 6 (mining) | 8 (alchemy) |
| Techniques picked in the run, a seed | 11.7 | 12.1 (32 at Journeyman in all) |
| Callings changed in the run | 1 | 24 |

- **Leadership** grows only where parties go out: with the hunting players 0.7 levels a seed, 6 captains to Apprentice and one to Journeyman (Leadership 6) across the 23 seeds, 416 experience a seed; none with no player.
- **Every resident holds a calling** from spawn (none without one), spread across all fifteen (hunting players, at day 14): Smith 29, Marksman 25, Apothecary 24, Rockbreaker 23, Captain 23, Skirmisher 20, Warden 16, Berserker 16, Forgehand 16, Field Surgeon 16, Brewer 16, Bearer 15, Prospector 14, Herbalist 14, Gleaner 9.
- **Callings change with the work:** 24 residents' changed in two weeks with the players, one with none. A Warden earned during a run is rare (finding F12): over ten days of the DoD's scenario a guild member became one on 2 of 23 gate seeds, since a member's melee, from the yard to 5, seldom passes the talent it started with.
- **The front holds where it stands:** on both seeds the Warden took the most of the creatures' blows at the next clear (26 against a mate's 4 at the most; 7 against 2).

### 5.8 Technique and Calling complete (C.1, 2026-11-19)
Plan 2's final build. Its fourteen days are §5.7's: no code that runs in the simulation changed after S5.6. The same measurement over 28 days on the gate seeds asks what plan 3 will find past the two weeks every DoD runs.

| | No player, 28 days | Hunting players, 28 days | Hunting players, 14 days (§5.7) |
| :--- | :-: | :-: | :-: |
| Seeds where someone reaches Journeyman | 19 of 23 (from day 12.3) | 22 of 23 (from day 4.0) | 18 of 23 |
| Residents holding a Journeyman's skill at the end | 1.83 a seed (most 4) | 3.57 a seed (most 7) | 1.43 |
| Residents with two skills past Apprentice | 0 | 4, on 4 seeds | 0 |
| Seeds where someone reaches Master | 0 | 0 | 0 |
| Highest level reached | 7 (mining) | 8 (melee, smithing, alchemy) | 8 |
| Techniques picked at Journeyman in the run | 42 | 82 | 32 |
| Leadership | none | 1.7 levels a seed; 11 to Apprentice, 4 to Journeyman | 0.7 |
| Callings changed in the run | 40 | 59 | 24 |

**What it means for plan 3 (Legend):**
- **The budget starts to bind after about four weeks** (F1): four residents hold two skills past Apprentice by day 28, so a third skill held at 5 is a month's story, not a fortnight's.
- **Master stays out of reach** (F8): nobody reaches it in 28 days. Master techniques, capstones and Master titles are the validators' until growth past Journeyman quickens or runs grow longer.
- **Journeymen and their techniques grow with time:** 82 Journeyman picks in 28 days against 32 in 14, mostly the artisans' and the miners'. Legend's deeds will meet Journeymen on most seeds in a month.
- **Callings move with the work:** 59 changed in four weeks with the players; a calling is a living label, not a birth one.


### 5.9 Deeds (S6.0, 2026-11-20)
Plan 2's final build, on the 23 gate seeds (`MeasureGrowthOnTheGateSeeds`, which since S6.0 also counts each deed per resident and each downing by its cause). Of 276 residents, how many gained at least 1 / 2 / 3 / 5 / 10 / 20 / 40 of each deed in the run, and on how many seeds someone reached 3 and 5:

| Deed | No player, 14 days | Hunting players, 14 days | Hunting players, 28 days |
| :--- | :--- | :--- | :--- |
| Creatures slain | none | 79 / 46 / 27 / 9 / 0 / 0 / 0; most 8; seeds 18, 8 | 120 / 87 / 69 / 38 / 11 / 0 / 0; most 16; seeds 23, 19 |
| Bosses slain | none | 5 / 1; most 2 | 23 / 2; most 2 |
| Rescues | none | none | none |
| Makes | none (no stations) | 85 / 82 / 76 / 66 / 57 / 34 / 8; most 59 | 115 / 113 / 106 / 102 / 93 / 74 / 26; most 94 |
| Downings survived | none | none | none |
| Hunts paid | none | 39 / 25 / 16 / 1 / 0; most 5; seeds 9, 1 | 40 / 33 / 20 / 3 / 0; most 5; seeds 13, 3 |
| Clears won | none | 97 / 68 / 45 / 17 / 0; most 6; seeds 12, 7 | 134 / 118 / 100 / 64 / 21 / 0; most 15; seeds 20, 16 |
| Crawlers taught | 140 / 96 / 77 / 55 / 12 / 1 / 0; most 21 | 123 / 85 / 58 / 34 / 8 / 0 / 0; most 16 | 186 / 144 / 108 / 65 / 18 / 3 / 0; most 27 |

**Downings: none.** Over 14 and 28 days, with no player or with the hunting players, no crawler on any gate seed is downed, by a creature or a hazard, and so nobody is rescued and nobody dies. Careful crawlers retreat before anything takes them to nothing (finding F13).

**The lines set from it** (D27, D29), so that a legend is earned on most seeds in two weeks and by few on each:
- **Epithets:** a creature kind's word at the 3rd kill (18 of 23 seeds in two weeks, 27 residents); a boss's word at the first boss slain; Lairbreaker at 5 clears won (7 seeds, 17 residents; 16 seeds by day 28); the Huntsman at 3 hunts paid (9 seeds, 16 residents); the Teacher at 10 crawlers taught (8 with the players, 12 without, in two weeks); the Maker at 40 makes (8 residents in two weeks). The Steadfast (2 rescues) and the Unbroken (2 downings survived) are earned only where crawlers fall (F13).
- **Earned traits:** Monster Hunter at 10 kills (none in two weeks, 11 residents by day 28: a month's story); Patient Teacher at 10 taught; Lifesaver at 3 rescues, Battle-Hardened and Shaken at a downing survived: where crawlers fall.

### 5.10 With epithets, scars and earned traits (S6.5, 2026-11-20)
S6.1–S6.4's build, the 23 gate seeds over 14 days. Growth is §5.7's to the digit (a Journeyman on 18 of 23 seeds with the hunting players, 1.43 a seed; 4 of 23 with no player), and so are the deeds (§5.9): epithets change no number, scars come only with downings, and of the earned traits only Patient Teacher is earned in two weeks.

| Held at day 14 | No player | Hunting players |
| :--- | :--- | :--- |
| Residents with an epithet | 12, on 10 seeds | 61, on 20 seeds |
| By epithet | the Teacher 12 | a kind's word 14 (27 earned it; a higher rank replaced the rest), a boss's word 5, Lairbreaker 15, the Huntsman 11, the Teacher 8, the Maker 8 |
| Earned traits | Patient Teacher 12 | Patient Teacher 8 |
| Scars | none | none (no downings, F13) |

Seeds 1–120 pass the outpost's day with and without the guild; the soak holds, 18 of 18 and 20 of 20 on each player profile. No DoD moved. A party's members win their clears together, so Lairbreaker comes to a party at once; the lines are first guesses for the producer.

### 5.11 Downings, the trigger of a named creature (S7.0, 2026-11-22)
S6's final build, the 23 gate seeds over 14 days (`MeasureDowningsOnTheGateSeeds`). Each downing is credited to the creature whose blow took the crawler to nothing (its `StruckBy`); a creature that downs a crawler and isn't a boss is one that would earn a name (D31), and each further crawler it downs raises its rank.

| Play | Seeds with a downing by a creature | Downings (by bosses) | Deaths | Would be named | Rank 2 / 3 | Slain after |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| No player | 0 | 0 | 0 | none | | |
| Hunting players | 0 | 0 | 0 | none | | |
| The guild's clears (four, armed from the stall, Steady) | 12, all within ten days (the first on day 2–9) | 47 (18) | 0 | 29 on 10 seeds | 0 / 0 | 29 |
| `DeathHasItsCost`'s pairs (two unarmed, the beetles, Bold) | 20 (19 within ten days) | 137 (0) | 7 | 75 on 20 seeds | 30 / 20 | 71 |

- **Where:** the beetles' hollow, almost alone (the clears: 28 of 29 named; the pairs: all). The Matriarch's warren downs members too, but by its boss, who has a name already.
- **Nobody falls in careful play** (F13 stands): with no player or the hunting players no creature downs anyone in two weeks, so no creature is named there.
- **A clear's named creature dies in the same fight:** the party that was downed in finishes the lair, so the beetle named for a fallen member is slain by its mates the same hour (29 of 29), and its slayer is the avenger (D35). A named creature outlives its naming only where the attackers break off: the pairs under the Bold rule, where the lair keeps its named beetles: of 75, 30 reach rank 2 and 20 rank 3.
- **The DoD's shape** (S7.8): the guild's clears name a creature on 10 of 23 seeds within ten days, so `TheBeetleThatRemembers` runs them, as `TheOutpostRemembers` does; the Bold rule's pairs aren't needed. A hunt naming its quarry (D34) gets its proof in the validators and the client, since in live play the clear that names a creature also kills it.

### 5.12 With named creatures (S7.6, 2026-11-22)
S7.1–S7.5's build (a name, growth and the lead, grudges, a hunt's quarry, the avenger), the 23 gate seeds over 14 days.

- **Growth and legends as before:** §5.10 to the digit. A Journeyman's skill at day 14 held by 1.43 residents a seed with the hunting players and 0.17 with none; epithets held by 61 residents on 20 seeds with the hunting players and 12 on 10 with none, the same rows. Nobody falls in careful play (F13), so no creature is named there.
- **Downings** (`MeasureDowningsOnTheGateSeeds`):

| Play | Seeds with a downing | Downings (by bosses) | Deaths | Named | Rank 2 / 3 | Slain after |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| No player, hunting players | 0 | 0 | 0 | none | | |
| The guild's clears | 12, all within ten days | 50 (18) | 0 | 32 on 10 seeds | 0 / 0 | 32 |
| `DeathHasItsCost`'s pairs | 20 (19 within ten days) | 198 (0) | 13 | 49 on 20 seeds | 23 / 18 | 34 |

- **The named creature bites:** on the bold pairs a beetle that grows and leads downs more of them (198 downings against 137 at §5.11, deaths 13 against 7, fewer creatures named because each named one downs more), and fewer are slain after (34 of 49 against 71 of 75): the lair a named creature leads is the wall D32 warns of, which the hunt and the clear answer. On the guild's clears every named creature still dies in the fight that named it.
- **Moved DoDs:** `DeathHasItsCost`, re-picked at S7.2 (to gate seed 11) and S7.3 (to 8), each in its own commit; no other. Seeds 1–120 pass the outpost's day, 120/120 with and without the guild; the soak holds, 18 of 18, and the `guild`, `crafting` and `hunting` profiles 20 of 20 each on seeds 1–20.

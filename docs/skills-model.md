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

The order in practice's one entry: the act's experience, times the passion, times the challenge's share (rounded to the nearest), then held to the source's ceiling.

## 4. What a level does
How good a crawler is at a work is its **effective level**, `CrawlerSkills.Level(skills, worn, work)`, the one number every effect and choice reads. Today it equals the learned level. Gear, techniques or callings that change how good a crawler is enter there (`SkillReadsTests` guards it).

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


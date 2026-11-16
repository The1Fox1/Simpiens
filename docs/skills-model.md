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
To come with S0.3: skill growth on the 23 gate seeds over 14 game days, who specialises, and practice by source.

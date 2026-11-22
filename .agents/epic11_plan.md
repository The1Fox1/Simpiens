# Epic 11: the implementation plans (index)

Working document (AGENTS.md, Documentation Protocol 6). The producer asked for it on 2026-11-16, after Epic 10's close-out: "Prepare to work on Epic 11. Decide how many implementation plans you will need to complete the work and have the first one drafted and ready to start". The design is the epic, [`docs/epics/epic-11.md`](../docs/epics/epic-11.md); its questions Q1–Q16 stand by exception.

**Complexity:** Very Complex as an epic ([rubric](rules/complexity-grading.md)): its size, and four systems with no precedent (techniques, callings, named creatures, unique items). Each plan breaks its share below that.

## The decision: four plans, one PR each

| Plan | Milestones | What the player gets | Estimate (agent days) | Complexity | The producer's stops |
| :--- | :--- | :--- | :-: | :--- | :--- |
| Craft (**done**, closed out 2026-11-18; the plan deleted) | S0–S3, **Craft** | Passions, practice by challenge, the mastery budget and upkeep, teachers (mentors, manuals, the drill yard) | 11–15 | Very Complex as a whole; every step Basic or Possibly Complex | The go; the look at S1; S2's soak bands (by exception); the merge |
| Technique and Calling (**done**, merged as PR #26 on 2026-11-20; the plan deleted) | S4–S5 | A technique chosen at each band, sponsored by the patron; callings, party roles, capstones, Leadership | 8–11 | Very Complex; S4's effects step supervised | The look at S4; the merge |
| Legend (**done**, closed out 2026-11-22; the plan deleted) | S6–S7 | Epithets, scars and earned traits from deeds; named creatures after the Nemesis system | 8–11 | Very Complex; S7 supervised | The looks at S6 and S7; the merge |
| Masterworks and the gate (drafted when plan 3 merges) | S8–S9 | Masterworks with a maker and a story; every scenario together; the play session; the close-out | 5–8 | Very Complex; S8 supervised | The play session; the merge |

In all, 32–45 agent days. Epic 10 Part 3's single plan (K0–K11) was estimated at 31–44 and took eight calendar days.

**Why four:**
- **Not one.** Part 3 ran as one plan and one branch: 267 commits, and the producer found Epic 10 "already too long as is". Ten milestones on one branch would stay off `main` for weeks and end in one unreviewable PR.
- **Not ten.** Each PR costs the producer a review and a merge, and three pairs of milestones share their foundations:
  - Craft's three milestones change the numbers everything after them is tuned on. The epic's order measures them and re-picks the DoDs' seeds once, early.
  - A calling is built from techniques, so S4 and S5 share one table and one round of balance.
  - A named creature's name comes from the deeds ledger, which S6 builds into legend.
- **The layers are the seams.** The plans follow the epic's own layers (Craft, Technique and Calling, Legend, Masterworks). Each ends at one of the producer's looks.
- **Each plan leaves `main` whole.** Each ends with its DoDs live and the game playable. Craft alone makes a richer outpost, and no earlier plan needs a later one to make sense.
- **S8 can leave.** If the producer moves masterworks out (Q14's alternative), plan 4 shrinks to the gate.
- **Later plans are drafted late on purpose.** Each is drafted when its predecessor merges, from what that plan measured: S0's growth curves set S4's numbers, and S6's deeds set S7's names. A plan drafted today would be written twice.

## Order
1. **Plan 1, Craft:** branch `feat/epic11-craft`, cut from `main` at `30c931b` (after PR #24). Done: S0–S3 from the producer's go (2026-11-16) to the review (2026-11-17, "This looks good. Let's wrap it up with C.1 and C.2"), closed out on 2026-11-18. It took two calendar days against the 11–15 agent days estimated. Merged as PR #25 on 2026-11-19. Its decisions (D1–D12) and findings (F1–F8) are in the epic, its numbers in `docs/skills-model.md` §5, its summary in `architecture_history.md`.
2. **Plan 2, Technique and Calling:** drafted on 2026-11-19 when plan 1 merged, from its measurements (`docs/skills-model.md` §5.5), on `feat/epic11-technique`, cut from `main` at `aceb183`. Executed at once, the producer away ("After drafting plan 2 you will begin immediate execution and will attempt to complete it"): S4 and S5 done and closed out the same day, against the 8–11 agent days estimated. At the look at S4 the producer asked for a skill tree to sponsor from; the technique tree followed (D25), the look passed, and it merged as PR #26 on 2026-11-20 ("This is great. PR plan 2 into main and then draft up plan3"). Its decisions (D13–D25) and findings (F9–F12) are in the epic, its numbers in `docs/skills-model.md` §3.6–§3.8 and §5.6–§5.8, its summary in `architecture_history.md`.
3. **Plan 3, Legend (S6–S7):** drafted on 2026-11-20 when plan 2 merged, from `docs/skills-model.md` §5.8 (the 28-day curves), on `feat/epic11-legend`, cut from `main` at `d387b05`. Done and closed out on 2026-11-22 ("Let's finish up the legend plan"); its decisions D26–D37 are in the epic's §0, its findings F13–F17 in §7, its summary in `architecture_history.md`; the plan is deleted. Its PR waits for S7's look and the merge.
4. **Plan 4** in turn, the same way.
5. **The epic's close-out** comes with plan 4. Every plan here is then deleted, and this index with them.

## What every plan shares
- **Branches and PRs:** one PR per plan into `main`, from one branch cut from `main` after the previous merge, with a commit per step. Never stacked.
  - Check `git branch --show-current` before every commit.
  - Push only on the producer's word, with an explicit refspec: `git push origin <branch>`. Merge only on the producer's word.
- **Decisions are defaults.** The producer answers only where they disagree. Silence keeps a default, but silence is not a go.
- **Numbering:** the epic's decisions run D1 onward across all four plans, and its findings F1 onward. Each moves to the epic's §0 or §7 as its step lands. The questions stay Q1–Q16.
- **One record of who a crawler has become** (A3): plan 1 lays out `CrawlerTraits` with room for every later plan's fields (techniques, a calling, an epithet, earned traits, scars). Plans 2 and 3 then fill reserved bytes without changing its shape.
- **Saves:**
  - Format 12 comes with plan 1, for the traits record. Plan 3 raises it again if the creature record grows (named creatures), and plan 4 if items become instances.
  - At most one raise per PR. Old saves are refused with a reason.
- **The numbers' home:** `docs/skills-model.md`, created at S0. It holds the skills' rules and measurements, as the economy and time models hold theirs, and outlives the plans.
- **Cards and the dossier still fit 720 rows** (the epic's risk row). The roster shows a calling and a best band; the dossier holds the rest.
- **Each plan shrinks as it closes:**
  - its open findings go to the epic, and data still needed for a decision goes to `docs/`;
  - its decisions go to `architecture_history.md`;
  - then the plan is deleted (Documentation Protocol 6).

## Gates (every milestone of every plan)
- `dotnet build dotnet/Simpiens.slnx`, then `dotnet test dotnet/Simpiens.slnx`. Commit messages give only the counts that ran. (Build the whole solution first: a one-project build once left the host stale for the golden check.)
- `golden check` 3/3. The hashes are re-recorded only for a deliberate change, in a commit of their own that names the cause.
- `scripts/test-client-in-container.sh`, with renders matching byte for byte. Changed renders are looked at first, then re-recorded there, in a commit of their own.
- `dotnet build dotnet/Simpiens.Desktop -p:XnaFramework=MonoGame` and `scripts/build-android.sh` compile, with 0 warnings.
- The 23 gate seeds and the soak. Seeds 1–120, with and without the guild, at each milestone's end.

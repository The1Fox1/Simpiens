# Epic 10: the implementation plans (index)

Working document (AGENTS.md, Documentation Protocol 6). On 2026-10-29 the producer asked to "write up Epic 10 into several phased implementation plans", and gave a view on decision D1: "Groups should be many to many relationship is my opinion", asking for an argument for or against (the answer: G4's D1, now in the epic's §0).

This file maps those plans and holds what they share. It replaces `.agents/epic10_g4_g6_plan.md`, a single plan for G4–G6 written earlier the same day that never left this branch.

## The plans
| Plan | Covers | Status | Estimate (agent days) | Complexity | The producer's stops |
| :--- | :--- | :--- | :-: | :--- | :--- |
| Part 1 (G4–G6) | The guild, expeditions and the gate | **COMPLETED 2026-11-01** (epic §4); its plans are deleted | — | — | Play session passed 2026-11-01 |
| [`core_refactor_plan.md`](core_refactor_plan.md) (not Epic 10) | R1–R6: guards, `SimulationManager`'s split, planners per worker | Next: Part 1 is done (producer, 2026-10-25: after Part 1) | 3–3.5 | **Very Complex** as a whole: R5 bundles three extractions (split it), and R6 changes threading (the developer supervises it). R1–R4 are Basic or Possibly Complex | As that plan says |
| [`epic10_part2_plan.md`](epic10_part2_plan.md) | Part 2, Milestone 3: workshops and the guildhall, W0–W6, with Epic UI-4 folded in | A draft: its questions need the producer's answers | 14–19 | **Very Complex** (epic-sized, open design). W1 and W5 need splitting | Answering the draft; a look at W3; a play session at W6 |
| [`epic10_part3_plan.md`](epic10_part3_plan.md) | Part 3, Milestone 4: the descent, L0–L6, with Epic UI-5 folded in | A draft, as Part 2 | 16–23 | **Very Complex** (epic-sized; new systems). L0, L1, L2 and L4 need splitting or the developer's supervision | Answering the draft; a look at L2; a play session at L6 |

**Left:** about 33–45.5 agent days (the refactor 3–3.5, Part 2 14–19, Part 3 16–23).
- **Complexity** ([rubric](rules/complexity-grading.md)): every plan, phase and step is stamped. Grep for `[Very Complex` to find the work that needs splitting or supervision.
- Part 1's numbers rest on measurements (M1–M6).
- Parts 2 and 3 have no measurements yet, so their estimates are the roughest. Each opens by measuring (W0, L0).

## Order
1. ~~**Part 1:** G4, G5, G6.~~ Done 2026-11-01: Part 1 COMPLETED; Parts 2 and 3 stay QUEUED.
2. **The core refactor's R1–R6** (next), before either draft is started:
   - R5 moves group formation and succession into `SocialDynamics`, after G4 has changed them.
   - R1's guards come before Parts 2 and 3 add planner facts and a larger floor: the planner's facts must stay below 64, and a map too large for A* must be refused.
3. **Part 2:**
   - the producer answers the draft's questions;
   - the design part of the draft (§1–§4) moves into an epic doc, as Part 1's approved draft did;
   - then W0–W6.
4. **Part 3:** the same, then L0–L6. L6's close-out marks Epic 10 COMPLETED, and every plan here is deleted.

Epic 11 (the MCP narrative) shares Milestone 4 with Part 3 but is its own epic. Nothing here waits for it.

## What every plan shares
- **Branches:** one PR per milestone into `main`, from `feat/epic10-<milestone>` cut from `main` after the previous PR merges. Never stacked (epic Q10, and G1 and G2's stacked PRs).
- **The agent never merges.**
  - Check `git branch --show-current` before every commit.
  - Push only on the producer's word, with an explicit refspec: `git push origin <branch>`.
- **Decisions are defaults.** The producer answers only where they disagree. Silence keeps a default, but silence is not a go.
- **Numbering:**
  - Part 1's decisions were D1–D14 (G4: D1–D6; G5: D7–D13; G6: D14), now in the epic's §0, and its measurements M1–M6.
  - Parts 2 and 3 each number their draft questions Q1–Q12, as Part 1's draft did. Cite them with the part, e.g. "Part 2 Q3".
- **Each plan shrinks as it closes:**
  - a closed milestone's row here becomes one line;
  - its open findings go to the epic, and data still needed for a decision goes to `docs/`;
  - its decisions go to `architecture_history.md`;
  - then the plan is deleted (Documentation Protocol 6).
  - This index goes with Part 3's close-out.

## Gates (every milestone of every plan)
- `dotnet test dotnet/Simpiens.slnx`. Commit messages give only the counts that ran.
- `golden check` 3/3. The hashes are re-recorded only for a deliberate change, in a commit of their own that names the cause.
- `scripts/test-client-in-container.sh`, with renders matching byte for byte. Changed renders are looked at first, then re-recorded there, in a commit of their own.
- `dotnet build dotnet/Simpiens.Desktop -p:XnaFramework=MonoGame` and `scripts/build-android.sh` compile, with 0 warnings.
- The 23 gate seeds and the soak 9/9. The fair band stays 1,000–2,000g until a plan re-sets a band with the producer. Seeds 1–120 at each milestone's end.
- **Saves:** the format is raised once per PR when a saved record changes shape. Old saves are refused with a reason.

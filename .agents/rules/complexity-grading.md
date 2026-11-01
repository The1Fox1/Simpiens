---
trigger: always_on
---

# Complexity grading (binding)

Every piece of planned work is stamped with how hard it is for an agent to do well, so the developer can pick the model, the reasoning effort and how closely to supervise. The producer set this rule on 2026-11-01.

## The scale
| Grade | Means | Who runs it |
| :--- | :--- | :--- |
| **Basic** | Little risk that a small model breaks something or brings in a bad pattern. | Any model. The checks the step names catch a mistake. |
| **Possibly Complex** | The work needs judgement. | The developer picks the model size and reasoning effort for it. |
| **Very Complex** | Only a frontier model, with the developer supervising. | A frontier model. The developer reviews each step. |

## The rubric
Grade a unit by its hardest part, not by its average.

**Basic:** every one of these holds.
- **A precedent:** a recipe or an earlier example exists and the step copies it. Examples: `AGENTS.md`'s "Adding an item / art / a look", an existing command, a validator, a table row.
- **Confined:** the step either preserves behaviour (the golden hashes don't change) or changes data only.
- **Already checked:** the existing checks prove the step: the tests, `golden check`, the renders, or a validator the step names.
- **Nothing delicate:** no change to the save format, threading, determinism or a hot path, and no new planner fact.
- **Small:** a few files in one project.
- **No calls to make:** no producer decision and no question of taste inside the step.
- Examples: R2's moves into partial files; a guard with its validator; an art or item row; a close-out written from a list the plan already holds.

**Possibly Complex:** any one of these makes a unit Possibly Complex.
- **A new mechanism with a nearby precedent:** a new command, group kind, sink, stat used in a handler, or Gum window.
- **A behaviour change:** the golden hashes are re-recorded, or the gate seeds or soak bands may move.
- **Saved state:** new authoritative state that must go into its owner's save block, or a bump of the save format.
- **Cognition:** a GOAP goal, action or fact, or anything else cognition reads.
- **The client:** Gum UI or renders that change and need a look.
- **Measuring:** reading probes, setting a soak band, tuning numbers.
- **Breadth:** one change across core and client.

**Very Complex:** any one of these makes a unit Very Complex.
- **Threading:** the single writer, the cognition scheduler, planners per worker, the simulation on its own thread, or determinism under concurrency.
- **A new system with no precedent:** combat, creatures, floors, moving the outpost, an external integration (MCP), a new platform.
- **Generalising a core state machine or invariant that other systems rely on:** one expedition becoming several, a session rebuilt mid-play.
- **Bundling:** several Possibly Complex parts in one unit.
- **Open design:** the producer hasn't yet answered the questions the work depends on.
- **Size:** several agent days spanning core, client and saves at once.

## The rules
1. **Every implementation plan is stamped.**
   - Each `.agents/*_plan.md` has a `**Complexity:**` line in its header, and so does every phase or milestone.
   - Every step gets a stamp right after its bold title: `[Basic]`, `[Possibly Complex]` or `[Very Complex]`.
   - Give a short reason wherever the grade isn't obvious, inside the brackets: `[Possibly Complex: a save block]`.
   - A plan without stamps is unfinished. Stamp it before any work on it starts.
2. **A Very Complex step or phase in a plan is a signal.** Do one of two things:
   - split it until its parts are Basic or Possibly Complex;
   - or write, next to the stamp, that the developer supervises it.

   An epic may be Very Complex, because of its size. A plan's job is to break the epic down below that.
3. **The remaining milestones of an epic carry stamps too,** in the epic doc or in the plan that holds them. An open finding gets a stamp once it has an owner.
4. **Complexity is not status.** A stamp sits next to its work item. It never goes into the roadmap's status column or the status vocabulary (ROADMAP §0).
5. **Re-grade when you learn more.**
   - If the work turns out harder than its stamp, stop. Re-stamp it, and tell the developer before carrying on.
   - If you are not a frontier model, don't start Very Complex work unless the developer says to.
6. **Find work by grade:** `grep -rn "\[Very Complex" .agents docs`.

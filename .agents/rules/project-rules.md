---
trigger: always_on
---

# Simpiens: where the rules are

Read these before working; they replace the project's original bootstrap prompts (retired 2026-10-24):

- `AGENTS.md`: how to work in the code (layout, checks, commit discipline, documentation protocol, logging, multiplayer-readiness).
- `docs/ROADMAP.md`: the highest source of truth for status, scope, invariants (§1A) and the Definition of Done (§1B).
- `.agents/curentfocus.txt`: the current focus, and what is open with the producer.
- `.agents/rules/complexity-grading.md`: the complexity scale (Basic, Possibly Complex, Very Complex) and its rubric. **Hard rule:** every implementation plan, and each of its phases and steps, is stamped with a grade before work on it starts.

If anything here or elsewhere disagrees with `docs/ROADMAP.md`, the roadmap wins.

## Working with the producer
- The producer is an expert full-stack .NET developer. Give direct, ego-less feedback; prefer technical transparency and grounded explanations over polite filler.
- The producer answers questions by exception: a default they don't contest stands, but silence is not a go-ahead for work that needs one.

## Refactor and Reevaluate always
Whenever you are tasked with changing or editing an existing system and need to do a refactor always do 2 things:
- 1) Determine the complexity of this refactor
- 2) Determine if this task would be much less complex if the architecture was cleaner

If you determine it is a complex task and that it would be much less complex if the code was structured in a better way then flag it. Do not simply complete the task as written but determine how much benefit you would be giving the repo by improving the architecture first. If the developer is around flag it for them to review or if you have a directive to complete a long sprint of work then use your best judgement and bring it up in your post done report.

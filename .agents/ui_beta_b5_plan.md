# Epic UI-Beta B5: the interface pass (plan)

Working document (AGENTS.md, Documentation Protocol 6). Written 2026-11-03 under the producer's directive of 2026-11-02 ("continue working through B3-B5 ... an implementation plan for each work chunk before executing"). The design is the epic's Q11 and story 10 ([`docs/epics/epic-ui-beta.md`](../docs/epics/epic-ui-beta.md)): nine items, "each strikeable".

- **Branch:** `feat/ui-beta-b5`, cut from `feat/ui-beta-b4` at `969f25f`, since nothing can merge overnight. One PR into `main` after B4's. Nothing is pushed.
- **Estimate:** 2–3 agent days, as the epic holds. Each item is about a quarter of a day; tooltips and the dropdown's icons about half.
- **Complexity** ([rubric](rules/complexity-grading.md)): **Possibly Complex** as a whole, as the epic stamps B5. Each item is stamped below. None touches the core, the save or threading.
- **Gates (every commit):**
  - a build with 0 warnings;
  - `dotnet test`;
  - the pinned image exact, with a commit that changes the picture followed by its re-records;
  - MonoGame and Android at the end.
  - B5's own gate is "tests and renders" (the epic's §4).
- **Order:** the Basic items first, then the Possibly Complex ones. Each item is a commit, with its renders re-recorded in the next.

## Decisions and steps (defaults; the producer answers by exception)

1. **Debug ids only in Debug builds** [Basic: an option, with the renders re-recorded]
   - Names lose their "[SwarmAgent_0]" unless `ClientOptions.ShowDebugIds` is set. The desktop exe sets it in Debug builds.
   - Tests and the pinned image run without it, so their renders and the debug-free names agree in every configuration.
   - The residents' cards and the dossier's name lose the id.
2. **The time of day in the header** [Basic: a presentation clock, a header text]
   - The outpost's day is the 15-minute day the live scenarios run: 9,000 ticks.
   - The clock starts at 06:00 on day 1 and shows "Day 1 · 06:30" beside the tick.
   - `OutpostClock`, in the presentation: time of day as words, nothing the simulation reads.
3. **A key help overlay** [Basic: a Gum panel from the bindings' table]
   - F1 (`InputAction.Help`) shows and hides a panel listing every binding, read from `KeyBindings`, so it can't drift from them.
4. **The stall opens the merchant terminal** [Basic: a click rule in the world view]
   - A click on the map that picks no crawler but lands on the stall's counter opens the merchant terminal.
5. **A grouped chronicle** [Possibly Complex: a model change and a render]
   - In a sector's recent events, consecutive events with the same words are one row, "×N" after them, at the newest one's time.
6. **Events that find their crawler** (Q11's "alerts that find their crawler") [Basic: B4's click to find]
   - A click on a recent event's row finds its first actor on the map (`FindOnMap`). An event with no crawler does nothing.
7. **Tooltips** [Possibly Complex: Gum has none; a hover timer and a panel that follows the cursor]
   - After half a second over an element registered with a tip, a small panel with the tip shows beside the cursor.
   - Registered: the header's buttons and counters, the dossier's drive bars, the inspector's buttons.
8. **Icons in the bounty board's dropdown** [Possibly Complex: Gum's ComboBox items]
   - The item dropdown's entries show the item's icon at 1× before its name, from the icon atlas.
9. **State marks that don't rely on colour** [Basic: a decision]
   - **Default: strike it.** Every state badge already carries its state as a word (WALKING), and on the map state shows by pose and the marks over the head (B2, D7), not by colour.
   - Adding glyphs would need new font glyphs (the baked Inter is Latin-1).
   - The producer can ask for it back.

## The close-out [Basic]
- The producer's look, in the morning, with B3's and B4's: the header, the tooltips, the key help and the chronicle.
- The epic's §10 and its §4 row; the roadmap, then the epic, then the focus file; `architecture_history.md`; AGENTS.md if a recipe changed.
- This plan is deleted.

## Risks
| Risk | What we do about it |
| :--- | :--- |
| Tooltips steal clicks or flicker | The tip panel takes no events; it shows only after the hover time and hides on a move away |
| The ComboBox's items resist templating | If Gum's ComboBox can't show icons cheaply, the item stays words and the dropdown shows the chosen item's icon beside it |
| Removing debug ids breaks tests that read names | Tests read friendly names through the model; the few that compare whole names are updated |

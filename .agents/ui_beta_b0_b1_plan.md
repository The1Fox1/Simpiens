# Plan: Epic UI-Beta B0 and B1

Working document (AGENTS.md, Documentation Protocol 6). The plan was written on 2026-10-30 as the Claude Doc "Plan: Epic UI-Beta B0 and B1" (https://claude.ai/artifact/6iUhjBH4ppvhcTRuPwNHrN). The producer answered the same day: "start B0 and B1 on one branch one PR". This copy carries that answer. The epic is `docs/epics/epic-ui-beta.md`.

## What changed with one branch
- **Branch and PR:** `feat/ui-beta` from `main` at `01a1321`, with one pull request for B0 and B1. The plan had two.
- **Order:** B1's look and catalogue land first, wired to nothing, so B0's spike draws real looks. There is no throwaway spike look.
- **The look at B0 doesn't block B1:** B1 is data, not art. The people sheet goes to the producer with the pull request. A change of options or odds is a catalogue row.
- **Before Epic 10 G4 and R1** (Q1 as answered):
  - R1's `Crawler` record doesn't exist yet. The name and the look live on `AgentState`, so `SessionHost` gains no fifth parallel list.
  - G4 and R1 rebase onto this branch's save format (6 → 7) and hash.

## Decisions (defaults stand; the producer's go accepted them)
| # | Decision | Default |
| :--- | :--- | :--- |
| D1 | Where B0's spike lives | `Simpiens.Client.Tests/People/`, beside the art review sheet, never shipped. What passes the look moves into `Simpiens.Presentation/Art/People/` at B2 and B3. |
| D2 | Where the design note lives | §9 of the epic file. |
| D3 | A look nobody drew | 0 means unset in every gene; real options start at 1. The live check fails on an unset gene. |
| D4 | Departed crawlers' names | A register of everyone the session has seen (id, name, look) outlives a drifter's departure and is saved. The chronicle formats names when it shows a line. |
| D5 | Who a name must not repeat | Everyone in the register while the lists last, then only the living. That is about 16 crawlers a day against about 150 names. |
| D6 | How the identity service learns names | The host registers them at spawn and load, and clears them at respawn. The hashed name stays the fallback for ids the host never spawned. |
| D7 | Names' letters and length | Latin-1 only (the baked Inter atlases), at most 10 characters. |
| D8 | Proving B1 changes no behaviour | Two commits. Commit A: the look and names land unhashed, with golden 3/3 and the soak unchanged. Commit B: the hash takes them, and the re-recorded baseline differs from the old one only in its hex fields. |

## Steps
1. [x] Docs: the roadmap (Track B, §3), the epic file, the focus file, this plan.
2. [ ] B1.2–B1.3, the look as data, wired to nothing:
   - `Simpiens.Core/Simulation/People/`: `CrawlerLook` (16 bytes, size pinned), `LookCatalog`, `CrawlerLook.Generate` on `SimRandomSalt.Look`;
   - validators: the catalogue, generation and odds.
3. [ ] B0, the spike (test-only, D1) in `Simpiens.Client.Tests/People/`:
   - bodies recoloured from today's 24 pose frames by key colour, with three hair styles, a broad build and the delver transform;
   - a 48×48 Hearthlight bust in tones written in integer maths;
   - `PeopleSheetTests` writes `render-output/people-sheet.png`.
4. [ ] B0's design note: §9 of the epic, from the spike's counts and timings.
5. [ ] B1.4–B1.6, names, the crawler, the host and the save. This is commit A:
   - `NameLists` and the name draw on `SimRandomSalt.Name`;
   - `AgentState.Look` and `GivenName`;
   - the register (`SessionHost.People`, departed drifters in `PopulationSave`);
   - the identity service registers names;
   - the save format goes to 7;
   - round trips in the capture and restore validators;
   - the guard: no `Cognition/` file reads `Look`.
   - Gate: golden 3/3 and the soak unchanged.
6. [ ] Commit B: `WorldStateHash` takes the look and name, golden re-recorded, and a script checks the diff is in hex fields only.
7. [ ] Commit C: the renders that show names are looked at, then re-recorded in the pinned image.
8. [ ] The live scenario `CrawlerIdentityTests`: the 23 gate seeds, and `OutpostWatch` checks names every tick.
9. [ ] Docs close-out:
   - the roadmap, the epic and the focus file;
   - `architecture_history.md`;
   - "Adding a look" (its core half) in `AGENTS.md`.
10. [ ] The standing gate, then one pull request into `main` (B0 and B1).

## Findings
(none yet)

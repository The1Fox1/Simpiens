# UI polish: Lantern & Stone (working plan)

- **Complexity:** Very Complex as a whole, for its breadth across every UI screen ([rubric](rules/complexity-grading.md)). No phase is Very Complex; each is graded below.
- **Report:** the Claude Doc "Simpiens UI Polish Proposal" (2026-11-03): https://claude.ai/artifact/1j4DQU1gwKmEWPMAr6XXvk. It audits the header and menus, surveys four flavors (A Guildhall Ledger, B Lantern & Stone, C Facility Terminal, D Clear Glass) with mockups and sources, and recommends B.
- **Authority:** the producer, 2026-11-03: "write up a proposal… commit to a new branch on top of the current one feat/ui-beta-b5 and begin working on your preferred design flavor… you have permission to run in my absence so long as you work on feature branch". The flavor is the agent's pick until the producer's look; the open questions (the doc's last section) run on their defaults.
- **Branch:** `feat/ui-polish`, cut from `feat/ui-beta-b5` (321d9f7). One pull request for the plan, opened after B2–B5's; nothing pushed until the producer says.
- **Scope:** `Simpiens.Presentation` and `Simpiens.Client` only. The core, the saves and the golden hashes don't change.

## What Lantern & Stone is
The UI is cut from the outpost's stone and lit by the portraits' lantern: a stone lintel for the header, recessed plaques with chamfered pixel corners, raised stone buttons that press in, stone panels in riveted iron frames, one accent (lantern amber). Light from the top left, as in Hearthlight. Frame metals reuse the portraits' standing metals (iron, bronze, blued steel). Inter stays, 1:1. Palette, rules and mockups: the doc's "Flavor B".

## Phases

### U0. The report, the plan, the branch — [Basic]
1. **The report** [Basic]: the Claude Doc above. Done.
2. **This plan and the branch** [Basic]. Done.

### U1. The palette — [Possibly Complex: renders change and need a look]
1. **`UiStyle`'s tokens to Lantern & Stone** [Basic: data in one class]: surfaces, text, the one accent, the meaning colours, the drive bars.
2. **Muted state badges** [Basic]: every state a dark plate with light words; panic keeps the danger red.
3. **A lighter, warmer veil** [Basic]: about 45% so the map shows under a window.
4. **Renders re-recorded** [Possibly Complex: the look]: in the pinned image, in their own commit.

### U2. The UI kit — [Possibly Complex: new generated art and pins]
1. **`UiKit`** [Possibly Complex: a new generator, with `SpriteKit` as precedent]: in `Simpiens.Presentation/Art/`, named pixel sprites generated on the CPU, reading no table or sheet: the plaque, the raised button (idle, hover, pressed, disabled, active), the primary button, the iron frame in three metals, the slider's groove and thumb, the lintel's stone, and small glyphs (coin, notice, shield, stall, pause, play, menu, sun, close, lantern).
2. **Into the icon sheet** [Basic]: appended to `SpriteSheets.Icons`, so `IconAtlas` packs them and `ArtPinTests` pins them; `IconAtlas` finds a kit piece by name at 1x.
3. **Pins re-recorded** [Basic]: after a look at the art review sheet, in their own commit.

### U3. Stone buttons — [Possibly Complex: Gum Forms visuals]
1. **Buttons from the kit** [Possibly Complex]: `UiParts.Button` points the V3 button's background nine-slice at the kit and sets each state's source (enabled, highlighted, pushed, disabled); text colours per state.
2. **An active state** [Basic]: a window's button stays lit while its window is open.
3. **Tests** [Basic]: a button's source per state.

### U4. The header in five clusters — [Possibly Complex: a new popup; tests read the header's words]
1. **Clusters** [Possibly Complex]: identity (lantern, SIMPIENS, the floor); the outpost (coin and gold, the stock, the notice and bounties, in one plaque); the windows (Stall, Bounties, Guild, with glyphs); time (sun and clock, pause, speed pips 1× 2× 5× 10×); Map | Ledger and the menu.
2. **The header model's figures** [Basic]: gold and bounties as figures; the plural bug goes.
3. **The menu** [Possibly Complex: a new popup]: Save, Load (asks first), Key help.
4. **Tooltips on the counters** [Basic]: B5's `Tooltips` on the outpost plaque's entries.
5. **Tests** [Basic]: `HudTests`, `GuildWindowTests` and new header tests.

### U5. One window chrome — [Possibly Complex: renders]
1. **`Window`** [Possibly Complex]: the iron frame from the kit with the window's metal (bronze for the stall, iron for the bounties, blued steel for the guild), its glyph in the title band, a drawn close glyph.
2. **Tests** [Basic].

### U6. Copy in the world's voice — [Basic: strings and the tests that read them]
1. **The rewrite table** [Basic]: the doc's "Shared foundations" table, in the models and windows.

### U7. Motion — [Possibly Complex: real-time animation must not reach the renders or the frame gate]
1. **A tween clock on real time** [Possibly Complex]: press 80 ms, hover 100 ms, window in 160 ms and out 120 ms, numbers rolling over 250 ms; settled at once when tests capture.
2. **Tests and the gate** [Possibly Complex: measuring]: `FrameTimeGateTests`, `UiDrawProbe`.

### U8. The dashboard and the inspector — [Possibly Complex: the largest render change]
1. **Panels, cards and badges from the kit** [Possibly Complex].
2. **The dossier and the inspector** [Possibly Complex].

### U10. The window's size (the producer's look, 2026-11-03) — [Basic: a client option, a menu row, a settings file]
1. **1920 by 1080 by default** [Basic]: `ClientOptions`' size from `WindowSizes`.
2. **The menu picks the size** [Basic]: 1280 × 720, 1920 × 1080, 2560 × 1440 as pips, the window's own lit; the game changes the window at the start of its next update, never inside the click; Gum's canvas follows the window.
3. **The choice is kept** [Basic]: `ClientSettings` (`settings.txt` beside the saves' folder), written only when the host passes a file (the desktop exe); the command line's size wins.
4. **Tests** [Basic]: `WindowSizeTests`.

### U11. Full screen and portraits that grow (the producer, 2026-11-03) — [Possibly Complex: the portrait atlas re-bakes; full screen differs by platform]
1. **Full screen** [Possibly Complex: KNI's SDL window]: a choice under the size pips, borderless at the screen's own size (no mode switch); the menu's sizes leave it; the settings keep it. On the producer's screen (3440 × 1440 at 1.05 under Wayland: 1372 logical rows), a 2560 × 1440 window can't fit with its title bar, and SDL can't tell beforehand.
2. **Portraits in whole steps** [Possibly Complex: the atlas's cells and every view that sizes around a portrait]: `PortraitAtlas.ScalesFor(height)`: the dossier's and the inspector's 2x at 720 rows, 3x at 1080, 4x at 1440; the speaker's 1x, then 2x. Cells hold the largest step; a new step re-bakes the seated portraits. The dossier's column, the inspector card and the scene's bubble and box grow by as much as their portrait.
3. **Tests** [Basic]: `WindowSizeTests` (full screen), `DossierPortraitTests` (the steps, pixel for pixel, and the widths).

### U9. Close-out — [Basic]
1. **Docs** [Basic]: `AGENTS.md`'s client notes (the kit), `.agents/architecture_history.md`; the roadmap once the producer says which epic owns this.
2. **The producer's look** [Basic].

## Log
- 2026-11-03: U0 done.
- 2026-11-03: U1–U6 done and their renders re-recorded in the pinned image (430/430 exact; core 143/143; golden 3/3; MonoGame and Android compile). Commits in `.agents/architecture_history.md`. Next: U7 (motion), U8 (the dashboard and the inspector), then U9 and the producer's look.
- Open from U1–U6, for U8: the combo boxes, list scroll bars and text boxes still wear Gum's flat look in the palette's colours; the dashboard's panels and cards are flat fills; the merchant's status bar is a flat inset.
- 2026-11-03: U8 done (the ledger's panels, the inspector, the tracker, key help and the cutscene box in frames; insets as plaques; drive bars in grooves) and its renders re-recorded; U7 done (windows arrive, the gold rolls, behind `ClientOptions.UiMotion`, on in the desktop game). Client 433/433, exact in the pinned image. `UiDrawProbe` (Release, one run right after a build, so the time is indicative only): UiDraw 2.84 ms, texture switches 337, sprite-batch begins 204, against B4's 3.12 ms, 380 and 234.
- 2026-11-03: the producer's look passed ("This is looking really good") and asked for U10: a 1920 by 1080 window by default, and a size picker in the menu. Done: Gum's canvas never followed the window before (a dragged window kept the UI at its first size); `EnableExpandToWindow` fixes both. The menu's rows now stack from their top: the load question's second line was hidden under its buttons since U4. Core 143/143, client 436/436 on the host, golden 3/3, MonoGame compiles.
- 2026-11-03: the producer found 2560 × 1440 only widened the window (their screen has 1372 logical rows) and wanted portraits to grow in larger windows; asked, they chose a Full screen choice and portraits only (text and list faces stay 1:1). U11 done. Client 438/438 on the host.
- Still open, for the producer's look or a later pass: the combo boxes, scroll bars and list cards keep Gum's flat look in the palette; no exit motion (a window closes at once); the lintel has no coursed mortar (an atlas region can't tile in Gum). Next: U9, the close-out, after the producer's look.

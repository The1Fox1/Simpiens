# Epic CLIENT M3, isometric and cutscenes: implementation plan

Working document (AGENTS.md, documentation protocol). It is deleted when M3 closes, after its findings move to `docs/epics/epic-client.md` and its decisions to `.agents/architecture_history.md`.

- **Goal:** isometric becomes the game's main view, and a cutscene mode exists (the proposal's M3).
  - **The isometric view:** the outpost drawn isometrically from generated placeholders. Walls, props and crawlers interleave by depth, so a crawler walks behind a wall.
  - **What carries over:** the fog, labels, picking, the camera and the dashboard all work in it.
  - **Top-down** stays for developers.
  - **Cutscenes come in two kinds** (producer, P3):
    - **Hard cutscenes** pause the game: still backdrops with characters and text boxes (Fire Emblem style), in screen space.
    - **Dynamic cutscenes** are part of play: they run over the live world, with the real crawlers as actors.
- **The proposal's gate:** depth-order tests pass, and walls and crawlers interleave in reviewed screenshots.
- **Where it starts:** the client seams (review A1–A3) are done. They delivered:
  - `IViewProjection` with `TopDownProjection` and `IsometricProjection` (the maths tested in four orientations, not wired in);
  - draw roles and the isometric World layer;
  - the fog rule D1 = C as a scene rule;
  - per-tick view frames with interpolation;
  - clicks that pick what is drawn;
  - screen-space map text from baked Inter.
- **What the seams deferred into M3:**
  - wiring isometric into the game, its art, sprites standing on their feet, the fog's cloud in isometric, and cutscene mode;
  - before or with M3: one world epoch (review A5), the scene built in `Update` (A6), and the frame path's allocations and culling (R1–R3);
  - with M3: splitting `SimpiensGame` (A4), because cutscenes need a screen to live in.
- **Branch:** `feat/client-m3` from `main-kni` (`f5f09bc`). Pull requests go into `main-kni` and are created by the producer, or by the agent on request. Proposed: one PR per phase group (P6 below).
- **Baseline, measured 2026-10-21 on `f5f09bc`:**
  - `dotnet build` 0 errors;
  - `dotnet test` 199 of 199 (client 136, core 63);
  - golden check 3 of 3.
- **Estimate:** about 7–10 agent days. That is the proposal's 3–5 for M3, plus 2–3.5 for the review items deferred into it, plus about a day for the second kind of cutscene.
- **No core change is planned.** If one becomes necessary, it is checked in Unity (PlayMode and golden), as the seams did.

---

## Design

- **M3-D1: a world view owns how the world is drawn; the game is a shell with a screen stack** (review A4).
  `SimpiensGame` splits four ways:
  - **`ClientSession`:** the simulation, its driver, the frames, the selection, the dashboard model and the save slot. It has no XNA types, so tests that need no GPU build it alone.
  - **`WorldView`:** the view mode (projection and art), the camera rig and controls, the scene, the label layout and picking.
  - **Screens:** `PlayScreen` (the world view, the dashboard and their input) and `CutsceneScreen` (Phase 3), on a small stack. The map designer becomes a screen in its own milestone.
  - **`SimpiensGame`:** the XNA lifecycle, device, content, platform, the Gum layer and the stack.

  A screen says whether it covers the screens below it and whether the world advances under it.
- **M3-D2: view state advances once per update; drawing is pure** (review A6). `WorldView.Advance(seconds)` runs in `Update`, after the camera: fog fades, the crawlers' display dwell, pruning a vanished selection. `Present(width, height)` then builds the draw list and labels from that state and changes nothing. A capture, a second view or two draws in one frame give the same picture.
- **M3-D3: one world epoch** (review A5). Every view cache (scene, fog, appearance, selection, camera framing) compares `WorldViewFrames.Epoch` with the epoch it was built for. The frames already start a new epoch on every respawn, switch or load. This removes the checks for "the tick went backwards" and "a new map object" from five places. The camera reframes only when the epoch brings a different map.
- **M3-D4: art sets per view mode.** A `WorldArt` gives each thing drawn its sprite, size in art pixels, pivot and height: tiles by kind, props by kind, crawler poses, the ring, a shadow, the cloud.
  - `TopDownArt` is today's numbers, so its picture and pins don't change.
  - `IsometricArt` uses the placeholders, with standing things on their feet (pivot 0.5, 1) and floor tiles centred.
  - The scene asks the art set and never tests which projection it has.
  - A view mode is a projection plus an art set plus its camera limits.
  - This is the seed of M7's sprite catalogue (review R5), with no packer or data files yet.
- **M3-D5: generated isometric placeholders** (`IsometricSprites`, Presentation), built from code and pinned by a hash test like `ProceduralSprites`. The tile size is a parameter (P1; default 32×16, 2:1, height 16 art pixels per unit).
  - **Floor:** a flagstone diamond, tinted per sector as top-down tints it.
  - **Rock:** a dark flat diamond.
  - **Walls:** a block per wall cell, full height and low, with lit top, left and right faces.
  - **On the floor:** a 2:1 selection ring and a soft crawler shadow.
  - **Bunk:** an isometric bunk, because the top-down bed doesn't stand.
  - **Reused standing:** the crawlers' 16 px sprites (front-facing figures), the hearth and the resource nodes, drawn 1:1 at the default tile size.
  - They join the one sprite atlas, so walls and crawlers interleaving in one layer stay one batch.
- **M3-D6: things that lie on the floor are drawn in floor space under the view's transform.**
  - In isometric a sector's rectangle is a parallelogram on screen, which a sprite can't be. The view's projection and camera are affine, so the renderer draws floor quads (the fog's cloud) in a batch begun with that transform (`SpriteBatch.Begin` with a matrix, shared XNA API). The rectangle then becomes the parallelogram the projection makes of it.
  - This gives one continuous cloud per sector instead of a cloud per floor cell.
  - Top-down's cloud takes the same path. If its pixels move beyond the tolerance, its references are re-recorded after a look.
  - Pixel art (floor diamonds, the ring) stays pre-drawn, because point-sampled art under a shear is jagged.
- **M3-D7: walls in the way** (P2). The wall style is a scene rule computed per map from the projection's depth, not art.
  - Full: every wall full height.
  - Low: every wall low.
  - Cutaway: a wall stays full height only when no floor lies behind it, so the outer far walls stand and every wall nearer the viewer is a low stub (The Sims' cutaway).
  - Default: cutaway. A key cycles the styles for the producer's look.
  - Picking still finds a crawler behind a wall: the picker takes the topmost *pickable* quad, and walls aren't pickable.
- **M3-D8: switching views.**
  - `--view iso|top` picks the view at start, and a key (V) toggles it. Switching keeps the floor point at the centre and about the same scale.
  - Each mode has its camera limits: the closest zoom shows about a room in both. Whole-pixel zoom steps come from the art's pixels per unit.
  - **One fixed view, no rotation** (P5). The isometric view has one orientation (quarter turn 0). Which corner faces the viewer is a single constant, so it can change at the look.
- **M3-D9: two kinds of cutscene on one script model** (P3).
  - **Hard cutscenes stop the game.**
    - They are still backdrops with characters and text boxes (Fire Emblem style), on a `CutsceneScreen` that covers the world.
    - The world doesn't advance: the screen stops the simulation driver, so nothing is written to the clock.
    - They draw in screen space through a `ScreenProjection`: virtual units of 640×360, scaled to the window at whole pixels, letterboxed, with draw order by layer only. Backdrops and characters go through the same draw list and renderer as the map.
  - **Dynamic cutscenes are part of play.**
    - The world keeps running, and they play over the world view with the real crawlers as actors.
    - **Speech boxes:** a line appears in a box over the crawler who speaks. The box sits above its head, follows it as it moves (at its drawn position, between ticks), and falls back to a text box at the bottom of the screen while the speaker is hidden by the fog or off screen.
    - **The camera** can be sent to a floor point or a crawler, and the player's own pan takes it back.
    - **The dashboard and clicks keep working.**
    - They present the simulation and never direct it (P7).
  - **One model.** `CutsceneScript` is built in code (`CutsceneBuilder`, as sessions are).
    - **Shared steps:** lines (speaker and text), waits, camera moves.
    - **Hard cutscenes only:** the backdrop, and characters entering, moving, facing, posing and leaving slots.
    - **Dynamic cutscenes only:** actors, which are crawlers named by id.
  - **One player.** `CutscenePlayer` steps a script on view time (real time, so text reads at the same pace at 10×) and on the player's advance (click, Space, Enter).
    - Text appears letter by letter: an advance finishes the line, then moves on, and Esc skips the scene.
    - In a dynamic cutscene, lines also move on by themselves after a reading time, because the game doesn't wait.
    - It is engine-free and tested without a GPU.
  - **Text** is drawn by Gum in baked Inter, with Regular 16 (and any size the look settles on) added to `FontSet` in its own re-bake. Speech boxes are placed on screen from the view, as the map's labels are.
  - **Placeholders:** generated backdrops, and crawler sprites at a whole scale as hard-cutscene characters.
  - **Not in M3:** what starts either kind in play, and the story (the narrative work).

---

## Phases and chunks (one commit each, only when the gate commands pass)

### Phase 0 · Groundwork (review A4, A5, A6, R1–R3): nothing on screen changes
**Gate:** the picture pins and render references unchanged; golden 3/3.

1. **0.0 The plan** (this file) and the roadmap's pointer.
2. **0.1 One world epoch (D3).**
   - Every view cache keys on `Frames.Epoch`; the tick and map-reference checks are removed.
   - Tests:
     - a load from a later tick rebuilds every cache (the case the tick check misses);
     - a respawn at the same tick on the same map rebuilds them too.
3. **0.2 Advance in `Update`, pure `Present` (D2).**
   - `WorldScene` splits into the state step and the pure build. Captures and `Draw` only present and render.
   - Tests:
     - two draws with no update between give identical pixels;
     - a capture starts no fade and raises no `Selection.Changed`.
4. **0.3 The split (D1).** `ClientSession`, `WorldView`, `PlayScreen`, the screen stack, and `SimpiensGame` as the shell.
   - Tests move mechanically: `game.Simulation` becomes `game.Session.Simulation`, and so on.
   - GPU-free tests build a `ClientSession` or a `WorldView` alone.
5. **0.4 The frame path allocates nothing; off-screen items are culled** (R1's cheap half, R2, R3).
   - **Allocations:**
     - the label layout's per-frame `HashSet` (`MapLabels.cs:139`) becomes a reused field;
     - each glyph carries its atlas page's index, resolved at layout, instead of a lookup keyed by a string-hashed face.
   - **Culling:** the scene drops items whose screen quad misses the viewport before sorting.
     - The pins first count only what lands on screen, in their own commit, so culling leaves them unchanged.
   - **Test:** after a warm-up, 100 frames of advance, present and the world's draw allocate 0 bytes on the thread. If KNI's `SpriteBatch` allocates, our code is asserted at 0 and KNI's bytes are recorded as a finding.

### Phase 1 · Isometric drawing
1. **1.1 Art sets (D4).** `WorldArt`, with `TopDownArt` carrying today's sizes and pivots. The scene reads only the art set.
   - **Gate:** the top-down pins and references unchanged.
2. **1.2 The placeholders (D5).**
   - `IsometricSprites` at the P1 tile size, pinned by hash; `IsometricArt`; the atlas grows.
   - `IsometricProjection` is built at the chosen tile size.
   - Tests:
     - every sprite fits its tile's footprint;
     - standing things sit on their feet;
     - every `AgentVisualState` has frames in both art sets (R5's check).
3. **1.3 Floor quads and the fog in isometric (D6).** `View` gives its floor-to-screen affine (engine-free), and the renderer draws floor quads under it.
   - Tests:
     - a cloud's four corners land where the projection puts the sector's inset corners;
     - D1 = C still hides and fades in isometric;
     - the top-down render references hold, or are re-recorded after a look with the reason.
4. **1.4 Walls in the way (D7).** The wall style rule, cached per map, with a key to cycle it.
   - Test: on Floor 1, cutaway keeps exactly the walls with no floor behind them.
5. **1.5 Isometric in the game (D8).** `--view`, the V toggle, camera limits per mode, and reframing at whole pixels. The dashboard, the follow and the labels are unchanged.
   - Tests:
     - toggling keeps the centre floor point;
     - the zoom limits hold in both modes;
     - a resize keeps whole pixels.
   - The default view at the end of the chunk is isometric (P4).

### Phase 2 · The gate for isometric
1. **2.1 Depth order and picking on the real outpost.**
   - **Depth order** (Floor 1 at t300 and t1500, in the fixed view): every standing item that overlaps a wall on screen is drawn before it when behind it and after it when in front.
   - **Crossing:** a crawler walking past a wall changes order on the tick its depth crosses.
   - **Picking:**
     - a click on a crawler's head picks it;
     - a click on empty floor selects the room under it;
     - a crawler hidden behind a full wall can still be clicked.
2. **2.2 Labels in isometric.**
   - No overlaps on Floor 1 or the Dev Map at seven zooms.
   - "UNCHARTED" sits under its room.
   - The selected name sits above its standing crawler: offset by the sprite's height, not a floor offset.
3. **2.3 Pins and references in isometric.**
   - The set: t0, t300, t1500, zoomed, selected, a fog fading, the Dev Map, and the header and dashboard over the map.
   - Each one is looked at, magnified where it matters.
   - A wall-style comparison set (full, low, cutaway) for P2.
4. **2.4 Frame time in isometric**, on llvmpipe and the GPU, beside top-down's. Recorded as a finding.

### Phase 3 · Cutscenes, two kinds (D9)
1. **3.1 The script and its player** (Presentation, engine-free, shared by both kinds): `CutsceneBuilder`, `CutsceneScript`, `CutscenePlayer`.
   - Tests:
     - the steps run in order on view time;
     - the letter-by-letter text, and an advance finishing a line;
     - skip;
     - a dynamic script's lines move on by themselves;
     - deterministic replays.
2. **3.2 `ScreenProjection`:** virtual 640×360, whole-pixel scale, letterbox.
   - Tests: the scale steps with the window size; the letterbox is centred; order is by layer only.
3. **3.3 Hard cutscenes: the cutscene screen.**
   - It is pushed over Play. The world is paused under it and resumed after.
   - Backdrop and characters go through the renderer; characters slide into their slots.
   - The text box and name plate are Gum.

   The new font size comes first, in its own re-bake commit; the font golden moves on purpose.
4. **3.4 Dynamic cutscenes over the world.** A cutscene layer on `PlayScreen`:
   - speech boxes over their speakers, following their drawn positions, with the bottom box as the fallback when a speaker is hidden by the fog or off screen;
   - the directed camera, released by the player's pan;
   - the world ticking throughout.

   Tests:
   - the tick advances while it plays;
   - a box follows its moving speaker;
   - a speaker hidden by the fog falls back to the bottom box;
   - a pan releases the camera;
   - a click still selects;
   - the golden hashes from inside the client are the same with a dynamic cutscene playing as without: it writes nothing.
5. **3.5 Demonstrations** in Debug builds, started by keys or from the Dev Map's bar:
   - a hard cutscene;
   - a dynamic one between the selected crawler and its nearest neighbour.

   Tests drive both through the scripted input: advance, the letter-by-letter text, skip, and for the hard one the world's tick unchanged while it plays. References: two hard-cutscene frames and one dynamic frame in isometric, each looked at.

### Phase 4 · Close-out
- Frame time measured again.
- The Android and MonoGame builds.
- Docs, roadmap first:
  - the epic's checklist, findings and deferred items;
  - AGENTS.md: view modes, art sets, screens, floor quads, cutscenes;
  - `curentfocus`;
  - an `architecture_history` entry.
- The producer's look (person-gated): the isometric screenshots, the wall style, the toggle and camera feel, both kinds of cutscene.
- This plan deleted.

---

## Gate commands (before every commit)
```
dotnet build dotnet/Simpiens.slnx -c Release                               # 0 errors
dotnet test  dotnet/Simpiens.slnx -c Release                               # all pass (199 at the start, plus new tests)
dotnet run --project dotnet/Simpiens.Host -c Release -- golden check       # 3/3 seeds
dotnet build dotnet/Simpiens.Desktop -c Release -p:XnaFramework=MonoGame   # 0 errors
dotnet build dotnet/Simpiens.Desktop -c Debug                              # 0 errors
scripts/build-android.sh                                                   # 0 errors
git diff --stat main-kni -- Assets/ ProjectSettings/ Packages/             # empty: the core and Unity are not touched
```
Render references, pins, pinned sprites and baked fonts change only on purpose, in the chunk that changes them, after a look. The pins tell a real change from rounding (F9).

---

## Decided by the producer (2026-10-21)
- **P1, tile size:** 32×16 placeholders, as proposed. It is one constant and a regeneration, so M7 can change it with the real art.
- **P2, walls in the way:** cutaway, as proposed. Phase 2 renders full, low and cutaway for the look.
- **P3, cutscenes:** two kinds (D9):
  - hard, traditional cutscenes that pause the game;
  - dynamic cutscenes that are part of the gameplay itself.

  Scripts are written in code. M3 ships both modes with a demonstration each; what starts them in play, and the story, are the narrative work.
- **P4, default view:** isometric, with V switching to top-down in every build, as proposed.
- **P5, rotation:** out of scope. One fixed view.
- **P6, pull requests:** three into `main-kni`: Phase 0, Phases 1–2, Phase 3.

## Still open for the producer (defaults hold until you say otherwise)
- **P7, dynamic cutscenes directing crawlers.**
  - **Default:** they present the live world (the real crawlers, doing what they decide) and never direct it.
  - **The alternative needs the core:** making crawlers walk to marks or act out a scene means a typed command that overrides their decisions for the scene. That belongs with the narrative work and the multiplayer rules, not in M3.
- **P8, control during a dynamic cutscene.**
  - **Default:** the player keeps it. The dashboard and clicks work, and a pan takes the camera back.
  - **Alternative:** lock input to advance and skip while one plays.

---

## Risks
| Risk | Plan |
| :--- | :--- |
| Isometric order breaks on tall or wide objects | Everything standing is one cell wide (a test exists), walls are blocks per cell, and equal depths tie-break by role. 2.1 checks every overlap on the real outpost |
| Walls hide crawlers and rooms | The cutaway style (P2); the picker finds hidden crawlers; the producer picks the style from screenshots |
| A floor quad under the view's transform samples differently from a sprite | The transform is shared XNA API, compiled for MonoGame and Android. Top-down's cloud is compared with its references, and re-recorded only after a look |
| Labels crowd in isometric (rows aren't floor rows any more) | The layout already groups by screen rows; 2.2 checks both maps at seven zooms |
| The split (0.3) churns many tests | It is mechanical and lands in its own commit, with no behaviour change; the pins and references prove it |
| KNI's `SpriteBatch` allocates per frame | Our code is gated at zero allocations; KNI's share is recorded and raised upstream if it matters on phones (M4) |
| Pixel art blurs at in-between zooms | Whole-pixel zoom steps come from the art's pixels per unit; the camera snaps the view plane to pixels in both modes |
| Cutscene text needs larger faces | A re-bake adds them through `FontSet`; the font golden changes in that commit only |
| Scope grows (7–10 days against the proposal's 3–5) | The review items are separate chunks in Phase 0. Phase 3 (both kinds of cutscene) can become its own milestone if you prefer |
| A dynamic cutscene changes the world | It only reads frames and steers the camera. A test compares the golden hashes from inside the client with one playing and without |

---

## Not in M3
- **For M7 (art pipeline):**
  - real art;
  - the sprite catalogue as data with a packer (review R5);
  - premultiplied alpha (R4);
  - textures loaded through `Texture2D.FromStream` (R7).
- **For M4:**
  - the static floor pass in vertex buffers with chunk culling (review R1's second half), measured first on a phone;
  - touch and the portrait phone layout (M4 and M6);
  - one input pipeline (U1);
  - a frame cap (R6).
- **The map designer:** its own milestone; it becomes a screen.
- **Cutscene triggers, story and writing tools:** the narrative work, by producer decision.
- **The simulation on its own thread and the dashboard on a snapshot** (review D1): after M3.
- **Speed, pause, save, load and switch as host commands** (review D3, with D2's tick cap per frame): still open, for the producer to schedule. The cutscene's pause doesn't add to it, because it stops the driver rather than writing the clock.
- **Gameplay height:** height stays visual only.
- **Rotation:** out of scope (producer, P5): one fixed view. The projection's quarter turns stay in its maths and tests, unused.
- **Dynamic cutscenes that move crawlers** (P7): typed commands in the core, with the narrative work.
- **Unity's removal (M2.8, M2.9):** independent; it waits for the side-by-side sign-off.

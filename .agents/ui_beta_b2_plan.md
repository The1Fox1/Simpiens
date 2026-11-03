# Epic UI-Beta B2: bodies on the map (plan)

Working document (AGENTS.md, Documentation Protocol 6). Written 2026-11-02 on the producer's word, "Begin B2 work". The design is the epic's §3.2 and B0's design note (§9 of [`docs/epics/epic-ui-beta.md`](../docs/epics/epic-ui-beta.md)).

- **Branch:** `feat/ui-beta-b2`, cut from `main` at `cee524b`; one PR into `main`, with a commit per step (the producer, 2026-11-02: one PR per plan).
- **Estimate:** 3–4 agent days, as the epic's §4 and §9 hold: P1 1.5–2, P2 1–1.5, P3 0.5.
- **Complexity** ([rubric](rules/complexity-grading.md)): **Possibly Complex**, as the epic stamps B2: generated pixel art with a head-anchor table; renders change and need a look. No step touches the core's behaviour, the save or threading. The core change (P2.1) only copies the look into the view's frame.
- **The producer's stop:** the look at the people sheet and the map (P3.1). The art pins of the bodies and the changed renders are recorded after it, each in a commit of its own.
- **Gates (every commit):**
  - a build with 0 warnings;
  - `dotnet test`;
  - `golden check` 3/3, unchanged (B2 changes no behaviour);
  - the client's tests exact in the pinned image;
  - the MonoGame and Android compiles.
  - Until P3.2, the client draws today's figures, so the renders hold. A commit that changes the picture on purpose (P3.2) is followed by the re-records.

## 0. What exists
- Every crawler has a saved `CrawlerLook` (B1): 13 genes, option ids from `LookCatalog`.
- Today's 24 pose frames (12 states, 2 frames each) are ASCII patterns in `ProceduralSprites`, in key letters: `#` outline, `K` skin, `H` hair, `B` eyes, `W` white, `C` shirt, `S` trousers, `T` tool, and the marks `!`, `?` and `Z`. Clothes colour shows the state today.
- Both view modes draw the same 16x16 crawler sprites from the world sheet: top-down at 0.7 of a cell, isometric standing on its feet. `WornSprites` recolours the hair into a helmet. `EntityVisuals` picks a crawler's art by state and worn item, and only `WorldScene.AddCrawler` draws crawlers.
- The view's frames (`EntityView`) carry no look.
- B0's spike (`Simpiens.Client.Tests/People/`) reads frames back into keys by colour, finds the head from the hair, and draws five hair styles by rule. `SpikePalettes` holds a colour per option. The people sheet's crowd is 64 looks from seed 20261030.

## 1. Decisions (defaults; the producer answers by exception)
| # | Decision | Default |
| :-: | :--- | :--- |
| D1 | Where bodies live | **A region of the world sheet:** 64 body slots of 24 frames, as blank 16x16 sprites (`Body_<slot>_<frame>`). The client writes a bake into its slot's cells (`Texture2D.SetData` with a rectangle, which KNI and MonoGame share). The world stays one batch and a crawler one quad. |
| D2 | Who gets a slot | **A slot per body, not per crawler:** keyed by the look and the head gear's art key, so crawlers that look alike share one. A slot is freed when no crawler in the current frame uses it. Past 64 bodies, the rest draw today's figure, and the overflow is counted. A bake happens at spawn, arrival or a gear change, never per frame. |
| D3 | A crawler with no look | Validators' hand-built crawlers, cutscene actors and a scene with no body table draw **today's figure**, so their tests are unchanged. Cutscenes get faces in B4. |
| D4 | The helmet (Q8) | **A miner's helm over the hair:** the rows above the eyes become the helm in its item's two tones, with a lamp pixel in its secondary. Hair below the brow (long, braids, ponytail) still shows. Today's helmet variants stay for today's figures. |
| D5 | Asleep | The blanket keeps the bunk's colours; the head shows the crawler's skin and hair. No build or height in the lying pose (§9). |
| D6 | Builds and lineages (§9) | Slight is one column narrower, broad one wider, heavy one wider on each side. A delver is one trouser row shorter, with the head full size. |
| D7 | Clothes and state (Q8) | The shirt is the primary colour, the trousers the secondary's shade. State shows by pose and the marks over the head. **No new marks in B2** unless the producer's look asks for them (§9: Idle and Walking differ only by the legs). |
| D8 | The guild's tabard (Q8) | Members wear a two-column tabard in the guild's colours down the shirt's front, over every pose but sleep. The frame gains a crawler's standing (resident, drifter, guild member), which B3's frames reuse. |
| D9 | Colours | B0's spike palettes, as passed at B0's look, move into `Simpiens.Presentation/Art/People/LookPalettes`. |
| D10 | Pins | The bodies of every option once, and of the crowd's 64 looks, are pinned by name in `art-pins.txt`, after the look. Renders and picture pins are re-recorded in the pinned image, in their own commit. |

## 2. Phases

### P1. The body kit (1.5–2 days) [Possibly Complex: generated pixel art, a question of taste]
Engine-free, in `Simpiens.Presentation/Art/People/`. Generators never read a table or a sheet (`ArtInitialisationTests`).
1. **Key frames from the patterns** [Basic]: `ProceduralSprites` exposes each crawler frame's pattern as key letters (`CrawlerKeyFrames`), so the kit reads keys, never colours. Today's sprites are unchanged; the art pins hold.
2. **The head anchors** [Basic: data with a check]: 24 rows (state, frame) giving the hair's top row, the head's columns, the eyes' row and the face's last row. A test checks each row against its pattern: hair on the top row, skin on the face rows, outline at the columns.
3. **`LookPalettes`** [Basic]: the spike's colours, moved. A test covers every colour option in the catalogue.
4. **`BodyKit.Bake(look, worn, standing)`** [Possibly Complex: five new hair overlays, taste]: 24 `PixelSprite`s from the keys, applying in order:
   - the hair style (10: five by rule, five as small overlays at the anchor);
   - facial hair (a beard below the chin, a two-pixel moustache);
   - the build, then the delvers' height;
   - the helm (D4), then the tabard (D8);
   - finally the look's palette.

   Pure and deterministic. Same look, same pixels; every option draws. No allocation beyond its output, with one scratch buffer.
5. **The people sheet on the kit** [Basic]: the sheet's bodies come from `BodyKit` and `BodySpike` is deleted. A row per option and every pose, with the helm and the tabard. The portrait spike keeps its place for B3 and reads `LookPalettes`.

### P2. The region and the frames (1–1.5 days) [Possibly Complex: a new mechanism across core, presentation and client]
1. **Looks in the frames** [Basic: one copy per crawler]: `EntityView.Look` and `EntityView.Standing`, captured at the end of each tick. It is the only core-side change, and it reads the crawler without writing it (`ViewsWriteNothingTests`). Golden is unchanged.
2. **The body slots in the world sheet** [Basic]: 64 × 24 blank sprites after the worn sprites. Measure the sheet's size: `AtlasLayoutTests` keeps it within 2048. Re-record the art pins for the blank cells in their own commit; nothing visible changes, and the renders hold in the pinned image.
3. **`CrawlerBodies`** [Possibly Complex: allocation-free per frame, slot reuse]:
   - the slot table (D2), walked in the scene's `Advance` once per update, with bakes queued for the client;
   - a slot's sprite index per (state, frame);
   - tests: a slot is shared and freed, overflow falls back, no allocation per frame once baked (`FrameAllocationTests`), and a bake happens only on a change.
4. **The scene draws bodies; the client writes them** [Possibly Complex: the texture write must compile on MonoGame and Android]:
   - `WorldScene.Bodies` is null by default, which draws today's figures;
   - given a table, a crawler with a look is drawn from its slot;
   - `SpriteAtlas.Write(index, pixels)` writes a bake into its cells after `Advance`.

   The client isn't wired yet: a review test writes the map with bodies, top-down and isometric, to `render-output/` for the look. The renders hold.

### P3. The look and the proof (0.5 day) [Possibly Complex: renders change and need the producer's look]
1. **The producer's look** [stop]:
   - the people sheet (`render-output/people-sheet.png`);
   - the review maps;
   - D1–D10 as they turned out, in particular D7 (are the poses enough without coloured clothes?).
2. **The client draws bodies** [Possibly Complex: a deliberate change of the picture]: `ClientGraphics` owns the table and `WorldView` gives it to its scene. Then, in commits of their own:
   - the art pins of the bodies;
   - the renders and picture pins, re-recorded in the pinned image after a look at `container-output/render-output/`.
3. **The live proof** [Possibly Complex: a client live test, nothing injected]:
   - on the live outpost, every crawler drawn comes from its own look's slot;
   - the day's first drifter arrives with a body of its own;
   - a guild member wears the tabard;
   - on the 23 gate seeds, no two living residents bake to the same pixels (measured first; if they do, the catalogue grows, per the epic's §7).
4. **Close-out** [Basic]:
   - §10 of the epic and its §4 row;
   - the roadmap, then the epic's status, then the focus file;
   - `architecture_history.md`;
   - "Adding a look" in AGENTS.md gains a picture row (a hair style is an overlay);
   - this plan is deleted.

## 3. Risks
| Risk | What we do about it |
| :--- | :--- |
| A hair overlay spoils a pose (the raised arms of Panic, the lying pose) | The anchor table holds each frame; the people sheet shows every pose with every style; the look decides |
| The world sheet passes 2048 | 64 slots × 24 frames at 18×18 texels with their gutter is about 0.5 M texels; P2.2 measures. If it passes, the slots drop to 48 |
| A larger atlas moves a render | The picture doesn't depend on the layout (epic F12); P2.2 proves it in the pinned image before any body is drawn |
| Residents look alike once state no longer colours clothes | P3.3 measures; D7's marks are the fallback, on the producer's word |
| `SetData` with a rectangle differs between KNI and MonoGame | The MonoGame and Android compiles run at P2.4; a difference goes through `XnaPlatform` |

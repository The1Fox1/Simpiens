# Epic UI-Beta B3: portraits and the dossier (plan)

Working document (AGENTS.md, Documentation Protocol 6). Written 2026-11-03 under the producer's directive of 2026-11-02: "continue working through B3-B5. You must write an implementation plan for each work chunk before executing." The design is the epic's §3.3, Q9, Q10, Q12, and B0's design note (§9 of [`docs/epics/epic-ui-beta.md`](../docs/epics/epic-ui-beta.md)).

- **Branch:** `feat/ui-beta-b3`, cut from `feat/ui-beta-b2` at `d971b60`, because B2's PR can't merge overnight. One PR into `main` after B2's merges (epic Q12: one PR per milestone), rebased onto `main` then. Nothing is pushed without the producer's word.
- **Estimate:** 2–3 agent days, as the epic holds: P1 1–1.5, P2 0.25, P3 0.75, P4 0.25, P5 0.25.
- **Complexity** ([rubric](rules/complexity-grading.md)): **Possibly Complex**, as the epic stamps B3. The portrait kit's hair is the main risk, and the gate is a question of taste. No step touches the core, the save or threading.
- **The producer's stop:** a look at the portraits and the dossier (the epic's B3 gate). Under the directive it happens in the morning, after the work. Renders and pins are recorded so every commit stays green, and the look can still send any of it back.
- **Gates (every commit):**
  - a build with 0 warnings;
  - `dotnet test`;
  - the client's tests exact in the pinned image;
  - the MonoGame and Android compiles at the phases that touch the client.
  - The core is untouched, so golden can't move.

## 0. What exists
- B0's spike (`Simpiens.Client.Tests/People/PortraitSpike.cs`), test-only. It draws a 48×48 Hearthlight bust in tones and materials, coloured from the look's ramps (`LookPalettes`, `SpriteKit.RampOf`), with a warm rim and an outline. It has two moods (content, tired), the tunic for every outfit, no accessories, and an iron frame over a stonework backdrop.
- The dossier (`DossierModel`, `DashboardPanels`) reads the selected crawler live. It shows a name row with the state badge, then drives, gear, groups, intent, affinities and actions, and no picture.
- The menus' icons come from `IconAtlas`: 1× and 2× on the CPU, drawn 1:1 through Gum's `SpriteRuntime`.
- B2's frames carry each crawler's look and standing (`CrawlerStanding`).
- Dynamic scenes (`DynamicSceneView`) show a speaking crawler's name in a bubble or a box. Hard cutscenes' characters have no look.

## 1. Decisions (defaults; the producer answers by exception, in the morning)
| # | Decision | Default |
| :-: | :--- | :--- |
| D1 | **The mood rule** (§3.3, the epic's §7: hysteresis) | Seven moods, by priority: asleep (sleeping) > hurt (downed, or health under 40; back at 50) > frustrated by a tantrum or defiance > scared (panicking) > frustrated (60 or more; back under 50) > hungry (hunger 70 or more; back under 60) > tired (energy 25 or less; back over 35) > content. A mood shown holds at least 30 ticks (3 s), unless the next is asleep or hurt. *As built: a tantrum shows as panicking on the map, so it is checked before scared.* |
| D2 | **Faces by mood** | content: a soft smile. tired: half-lidded eyes, a flat mouth. hungry: brows up, a small open mouth. frustrated: brows knit down, a frown. scared: wide eyes, brows high, an open mouth. hurt: one eye squeezed, a frown, a bandage across the brow. asleep: eyes closed, a slack mouth. |
| D3 | **Outfits** (8 cuts of the collar and shoulders) | tunic (round collar, placket); jerkin (a laced V); cloak (over the shoulders, a clasp); hood (down, behind the neck); robe (a crossed wrap); furs (a shaggy collar); pauldron (a steel plate on the near shoulder); scarf (wrapped at the neck). Primary colour for the body of the cloth, secondary for the trim. |
| D4 | **Accessories** | earring (a gold pixel at the ear); circlet (a gold band across the brow); feather (behind the ear, rising); scar (a pale line across the cheek); warpaint (two ochre stripes under the near eye). |
| D5 | **Gear and standing on the bust** | The helm: the hazard gear's dome and brim over the hair, its lamp lit, with long hair showing below. The guild's tabard: B2's deep blue with the gold device, down the chest. |
| D6 | **The frame** (Q9) | Two pixels, by standing: iron for residents, bronze for drifters, blued steel for guild members. B0's stonework sits behind the bust; both are outside the bake. A framed portrait is 52×52, magnified on the CPU to 104×104, each drawn 1:1. |
| D7 | **The portrait atlas** | 48 slots on one texture, 1× and 2×, keyed by look, mood, standing and helm; the least recently shown slot is reused. A bake happens at a portrait's first showing, and again when its mood, gear or standing changes. |
| D8 | **The dossier's header** (§9's mock) | The portrait at 2× on the left. Beside it: the name, then "Human, average build. Resident.", the state badge, then "Content. Health 100%.". The rest of the dossier is unchanged below it. Debug ids stay in names until B5. |
| D9 | **Scene speakers** (§4's B3 row) | A dynamic scene's bubble and box show the speaking crawler's portrait at 1× beside its name. Hard cutscenes' characters have no look, so they stay faceless until they are given one (not in this epic). |
| D10 | **Pins** | The portraits are pinned by name in `art-pins.txt`: each mood, outfit, accessory, frame, hair style, lineage and face shape on a base look, and the crowd of 64 content. The renders that change are recorded in the pinned image, in their own commit. |

## 2. Phases

### P1. The portrait kit (1–1.5 days) [Possibly Complex: generated art, a question of taste]
In `Simpiens.Presentation/Art/People/`. It reads no table or sheet.
1. **`PortraitKit` from the spike** [Basic: a move]: the spike moves into the presentation, its content and tired faces unchanged. `PortraitSpike` is deleted, and the people sheet reads the kit.
2. **Moods** [Possibly Complex: taste]: the seven faces of D2, as a table of features (eyes, brows, mouth, marks), each drawn in tones.
3. **Outfits and accessories** [Possibly Complex: taste]: D3's eight cuts and D4's five accessories.
4. **Hair, front and back** [Possibly Complex: the epic's main risk]: the ten styles revisited at 48×48. The cap stays, and the styles that read poorly on the sheet get their own silhouettes (braids, ponytail, bun, coils, mohawk).
5. **Gear, standing, frames** [Basic: layers over a known shape]: D5's helm and tabard; D6's frames and the backdrop (`PortraitKit.Framed`).
6. **The people sheet and checks** [Basic]:
   - the sheet: every mood, outfit, accessory, frame and style;
   - `PortraitKitTests`: same input, same pixels; every option the portrait shows changes it; every mood differs; the helm and tabard show; a bake into a buffer allocates nothing.

### P2. The mood rule (0.25 day) [Possibly Complex: hysteresis]
1. **`Mood` and `MoodRule`** [Possibly Complex]: D1 as a table, plus a `MoodTracker` per crawler on ticks (the shown mood, and since when). Tests:
   - each mood from its state;
   - no flicker at a threshold;
   - the hold;
   - asleep and hurt don't wait;
   - nothing allocates per update.

### P3. The atlas and the dossier (0.75 day) [Possibly Complex: Gum UI; a render changes]
1. **`CrawlerPortraits`** [Possibly Complex: slot reuse, as `CrawlerBodies`]: engine-free. D7's slots, LRU, keyed by `PortraitKey`, with a bake queue.
2. **`PortraitAtlas`** [Basic: as `IconAtlas`, written at run time as B2's region is]: the client's texture holds each slot at 1× and 2×, written with `SetData` and a rectangle, and a source rectangle per slot and scale.
3. **The dossier model** [Basic]:
   - `PortraitKey`: the selected crawler's look, mood, standing and helm;
   - `Description`: "Human, average build. Resident.";
   - `MoodLine`: "Content. Health 100%.".
   - The tracker moves on per refresh, by the tick.
4. **The header in Gum** [Possibly Complex: a render changes]: D8's layout, drawn 1:1 at whole pixels. `render-outpost-dashboard-t300` is re-recorded in the pinned image.
5. **The selected crawler's portrait is its own** [Basic: Q12's client test]:
   - selecting crawlers shows each one's look and mood in the dossier's slot;
   - the atlas holds that bake;
   - after a refresh, a change of mood re-bakes.

### P4. Scene speakers (0.25 day) [Possibly Complex: Gum UI]
1. **Faces in dynamic scenes** [Possibly Complex]: D9. `render-iso-scene-gossip` is re-recorded.

### P5. The look and close-out (0.25 day) [Basic]
1. **The producer's look** [stop, in the morning]:
   - the people sheet's portraits;
   - the dossier render;
   - the gossip scene;
   - D1–D10 as they turned out.
2. **Close-out** [Basic]:
   - the epic's §10 and its §4 row;
   - the roadmap, then the epic's status, then the focus file;
   - `architecture_history.md`;
   - AGENTS.md: the portrait kit under Art/People, and "Adding a look";
   - this plan is deleted.

## 3. Risks
| Risk | What we do about it |
| :--- | :--- |
| The hair reads as programmer art | The sheet shows every style at 1× and 2×; the morning look decides; every layer stays replaceable (M7) |
| A mood flickers | D1's hysteresis and hold, with a test at each threshold |
| The dossier header crowds its 380 px column | The portrait's 104 px plus the text column; the render shows it; the name wraps under a width |
| Gum draws a portrait blurred | Drawn 1:1 at whole pixels from a point-sampled texture, as the icons are |
| Texture writes differ on MonoGame or Android | The same `SetData` with a rectangle as B2; both compile |

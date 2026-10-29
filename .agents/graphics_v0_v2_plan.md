# Graphics V0–V2: the art as data, with no picture change

Working plan, 2026-10-28. This is the implementation of phases V0–V2 of `.agents/graphics_plan.md`, whose §1 has the
findings and §2 the design. Status is not tracked here: when the producer says go, the roadmap gets its entry first
(Epic CLIENT, pulled forward from M7's review R5 and S10).

**The promise of V0–V2: nothing on screen changes.**
- Every render matches its reference byte for byte in the pinned image (`scripts/test-client-in-container.sh`).
- The picture pins are re-pinned once, in V0.2, when they switch from sprite indices to sprite names. They hold from
  then on.
- No simulation code changes: golden 3/3 and the soak can't move.

**What V0–V2 buy:**
- A thing drawn is named by an **art key** and resolved through a per-mode **`ArtCatalog`**. Sizes, pivots, frames,
  frame rates and pulses are data in two tables (top-down and isometric).
- The scene asks one question per entity ("which visual?"). `WorldArt`'s per-kind members are gone.
- Sprite indices exist only inside sheets.
- Every sprite is pinned by name, and one PNG shows them all for review.
- V3 (item art) then adds sprites and catalogue rows, not code paths.

---

## Changes to the graphics plan

Two things move from V1 to V3, because they change the atlas's layout or add a sprite, and either can move a sampled
texel:
- **P2, the atlas packer.**
- **The magenta "missing" placeholder.**

V3 re-records renders anyway. Until then:
- The atlas stays the grid it is.
- A missing key resolves to today's fallbacks: the berry bush for a node, nothing for anything else.
- The completeness tests make sure no shipped content reaches a fallback, except the recorded ration quirk (V2.4).

`graphics_plan.md` §3 is updated to match.

---

## Invariants for every step

1. **Indices are the sheet's business.** After V1.2, only a `SpriteSheet` and the catalogue's build hold sprite
   indices. A test asserts nothing about an index's value.
2. **The catalogue reproduces today's float expressions, in today's order.** A piece's size is
   `(Width × motion) × UnitScale`.
   - Top-down's tables keep sizes in world units with `UnitScale = 16`, because today's code is
     `HearthSize * pulse * Art` and `CrawlerSize * pose.ScaleX * Art`.
   - Isometric's tables keep sizes in art pixels with `UnitScale = 1` (`Standing * pose.ScaleX`).
   - Frames: `(int)(t * fps) % count`, which equals today's `& 1` for two frames and t ≥ 0.
   - The pulse: `1f + MathF.Sin(t * rate) * amplitude`.
   - A difference in the last bit of a size can move a pixel edge. The renders would catch it, but it is cheaper
     not to cause it.
3. **The frame path allocates nothing and touches no strings.** Keys resolve to `ArtId`s when a catalogue is built
   or a mode is set. `FrameAllocationTests` stays green.
4. **Views decide nothing; presentation stays XNA-free.** All new types live in `Simpiens.Presentation/Art/`.
   `Simpiens.Client` only builds textures from `SpriteSheet`s.
5. **No item names in presentation code.** Node art goes through `ItemDefinition.SpriteKey`, as today, so
   `ItemNamesTests` stays green. The rename to `ArtKey` is V3's.

## Gate for every commit

- `dotnet build dotnet/Simpiens.slnx`: 0 warnings.
- `dotnet test dotnet/Simpiens.slnx`: all green.
- `scripts/test-client-in-container.sh`: green, with exact renders, **never `--record`** in V0–V2.
- `dotnet build dotnet/Simpiens.Desktop -p:XnaFramework=MonoGame`: compiles.
- At the end of V2: `FrameTimeProbe`, compared with the numbers in `graphics_plan.md` §1.4, within ±10%.

The commit body states the counts. Golden and soak need no run (no core change); say so in the body rather than
claim them.

**If a render differs:**
1. Look at `container-output/render-output/*.diff.png`.
2. A difference is never re-recorded in V0–V2. Either the step has a bug (fix it), or the step changes the texture's
   layout and is moved to V3 (see V1.1's fallback).

---

## V0: the safety net (no production behaviour changes)

### V0.1 One table of what each sheet holds
- **New `Simpiens.Presentation/Art/SpriteSheets.cs`:**
  - `public static IReadOnlyList<PixelSprite> Contents(DrawSheet sheet)` lists today's four lists:
    - `Sprites` → `ProceduralSprites.All`
    - `Fog` → `{ FogCloud.Sprite }`
    - `Isometric` → `WorldSprites.All`
    - `Backdrop` → `CutsceneArt.Backdrops`
  - It is the one place that says this.
- **`ClientGraphics`** builds its atlases from `SpriteSheets.Contents` (same lists, same order, same atlas
  arguments: fog smooth with no gutter).
- **Test:** sprite names are unique within each sheet, and across sheets except where one sprite sits in two sheets
  (the 16×16 art in `Sprites` and `Isometric`). There, the entries must be the same object.
- Commit: `refactor(presentation): one table of what each sprite sheet holds (graphics V0)`.

### V0.2 Picture pins by sprite name, not by index
- In `PicturePinTests.ItemsHash`, replace `Fnv.Add(item.Sheet)` and `Fnv.Add(item.SpriteIndex)` with a hash of
  `SpriteSheets.Contents(item.Sheet)[item.SpriteIndex].Name`, using FNV over its UTF-16 chars.
  - The sheet adds no information beyond the name: the fog's sampling is the fog's sprite.
  - Everything else stays as it is: layer, quantized quad and rotation, tint.
- **Re-pin all five pins in this commit.**
  - The evidence that nothing drawn changed: the same commit's renders pass exact in the container, and the old pins
    passed on its parent.
  - Say both in the body, plus the pin history comment (the existing comment block records every re-pin).
- Commit: `test(client): picture pins name their sprites instead of indexing them (graphics V0)`.

### V0.3 Every sprite pinned by name
- **New `Simpiens.Client.Tests/ArtPinTests.cs` and `Simpiens.Client.Tests/Baselines/art-pins.txt`:**
  - One line per sprite of every sheet (deduplicated by object): `name width height fnv64-of-pixels`, sorted by
    name.
  - `EverySpriteIsAsPinned` reports each sprite that changed, appeared or disappeared, by name.
  - `SIMPIENS_RECORD_ART=1` rewrites the file. Pixels are generated on the CPU, so the pins are exact on every
    machine: no container is needed to record them.
- **Retire the aggregate pixel pins it supersedes:**
  - `ProceduralSpriteTests.TheGeneratedArtIsUnchanged`'s `PinnedArtHash`;
  - `IsometricArtTests`'s `PinnedPlaceholders`;
  - `DemoSceneTests.TheBackdropsAreUnchanged`.
  - **Keep `PortedArtHash`.** It is history: the first 33 sprites are Unity's, pixel for pixel. It stays a separate
    test over `ProceduralSprites.All.Take(33)`, whose order doesn't change.
- **Update the art-change rule in `AGENTS.md`'s checks table:**
  - a deliberate art change re-records `art-pins.txt` in its own commit, after a look at the review sheet;
  - renders are re-recorded in the container in that same commit.
- Commit: `test(client): every sprite pinned by name in Baselines/art-pins.txt (graphics V0)`.

### V0.4 The art review sheet
- **New `ArtReviewSheetTests.WriteTheArtReviewSheet`**, a plain `[Fact]` (cheap, so every container run produces it).
  It writes `render-output/art-sheet.png`, which the container script already copies to
  `container-output/render-output/`.
- **Layout:**
  - one section per sheet;
  - each sprite magnified by the largest whole factor that keeps it at most 96 px tall (16×16 at 6×), on a two-grey
    checker that shows transparency;
  - its name under it, from the baked Inter 10 pages (`FontLibrary` + `Png.Decode`: the pages are 8-bit RGBA, which
    `Png` reads).
- **Asserts:** every sprite appears exactly once; the labels don't overlap.
- **Retire `IsometricArtTests.WriteTheContactSheet`** (explicit, placeholders only).
- **Document it in `AGENTS.md`:** to look at the art, run the client tests and open `art-sheet.png`.
- Commit: `test(client): an art review sheet of every sprite, named (graphics V0)`.

**V0 done when:** pins by name and sprites by name are in place, the sheet is written, and the renders are
untouched.

---

## V1: sheets and the catalogue

### V1.1 One world sheet for both view modes
- **`DrawSheet` becomes `World = 0, Fog = 1, Backdrop = 2`.** `Sprites` is removed; `Isometric` is renamed `World`.
  The client's `_sheets` array follows the new values.
- **Callers:**
  - `TopDownArt` and `CutsceneStage` draw from `World`;
  - the first 35 world indices are the same sprites as top-down's sheet, so no sprite index changes.
- **Tests that filter on `DrawSheet.Sprites`** move to `World`: `ArtSetTests`, `FogTests`, `CutsceneScreenTests`,
  `IsometricDrawOrderTests`, `IsometricArtTests`.
- `SpriteSheets.Contents` loses its `Sprites` row.
- **Risk:** top-down now samples a 294×252 texture (42-pixel cells, sized to the 32×40 isometric wall) where it
  sampled 108×108 (18-pixel cells), and its sprites sit in other cells.
  - With point sampling, a sample can land on another texel only if the float UV sits on a texel boundary.
    That is unlikely, but this is the step where it would show.
  - **Fallback if a top-down or cutscene render differs:** revert this step and keep the separate top-down sheet
    until V3. Nothing later in V1–V2 depends on one sheet.
- Commit: `refactor(client): one world sprite sheet for both view modes (graphics V1)`.

### V1.2 Sprites without indices
- **`PixelSprite` loses `Index`.** It is a name, a size and pixels.
- **New `SpriteSheet`** (Presentation/Art): an immutable ordered list with:
  - `Count`, `this[int]`;
  - `IndexOf(string name)`, which throws for an unknown name;
  - `TryIndexOf`.
- **`SpriteSheets.World`, `.Fog` and `.Backdrop`** are built once, lazily, in today's order: `ProceduralSprites.All`,
  then `IsometricSprites.All`. So every index stays what it is.
  - `IsometricSprites` stops offsetting by `ProceduralSprites.All.Count`.
  - `WorldSprites` is removed.
  - `SpriteSheets.Contents(DrawSheet)` returns the sheet.
- **Callers of `.Index`:**
  - `TopDownArt`, `IsometricArt`, `FogOfWar`, `CutsceneStage`, `CutsceneArt` and the tests;
  - they resolve through the sheet, with the art sets resolving once into `static readonly` ids.
  - This is mechanical, and the compiler finds every one. V1.3 and V2 replace most of them.
- **Static initialization:** the generators never read a sheet, and a sheet reads the generators' lists once. So no
  index can be read before it exists. A test builds `SpriteSheets.World` and checks
  `World[World.IndexOf(n)].Name == n` for every name.
- **The client's `SpriteAtlas`** takes a `SpriteSheet`. It still lays the grid out in sheet order (the packer is
  V3's).
- Commit: `refactor(presentation): sprite indices belong to their sheets (graphics V1)`.

### V1.3 The art catalogue, behind today's art sets
New files in `Simpiens.Presentation/Art/`:

```csharp
public readonly struct ArtId { public readonly int Value; public bool IsNone => Value < 0; public static ArtId None => new(-1); }

public readonly struct ArtDefinition
{
    public readonly string Key;
    public readonly DrawSheet Sheet;
    public readonly int FirstFrame, FrameCount;   // into the catalogue's frame table (sprite indices)
    public readonly float FramesPerSecond;        // 0: the caller picks the frame (a crawler's pose)
    public readonly float Width, Height;          // in UnitScale units
    public readonly float UnitScale;              // top-down 16 (sizes in world units), isometric 1 (art pixels)
    public readonly float PivotX, PivotY;
    public readonly float PulseRate, PulseAmplitude; // size × (1 + sin(t × rate) × amplitude); 0: none
}

public sealed class ArtCatalog
{
    public sealed class Builder { /* Add(key, sheet, spriteNames[], width, height, unitScale, pivot, fps, pulse); Build() */ }
    public ArtId Id(string key);                 // at setup only; throws for an unknown key
    public bool TryId(string key, out ArtId id);
    public ref readonly ArtDefinition this[ArtId id] { get; }
    public ArtPiece Piece(ArtId id, float viewSeconds);              // frame by fps, pulse applied
    public ArtPiece Piece(ArtId id, int frame, float scaleX, float scaleY); // a crawler's pose
    public Vec2 NameOffset { get; }  public float NameHeight { get; }   // where the selected crawler's name hangs
}
```

- **`Build()` refuses** a duplicate key, a sprite name its sheet doesn't have, no frames, or a negative size. It
  resolves names to indices into one `int[]` frame table, so lookups are array reads.
- **`ArtKeys`** holds the keys code names, as constants:
  - `tile/floor`, `tile/rock`, `tile/wall`, `tile/wall-low`;
  - `prop/hearth`, `prop/bunk`;
  - `fx/selection-ring`, `fx/crawler-shadow`;
  - `crawler/<state>` from a precomputed `string[]` by `AgentVisualState`;
  - `node/<sprite key>` and `node/default`.
- **The tables (the art as data):**
  - **`TopDownArtTable`:** every number `TopDownArt` has today. Crawler 0.7, node 0.8, hearth, bunk and ring 0.9, the
    wall tile 1, all at `UnitScale` 16. The hearth: two frames at 6 fps, pulse rate 4, amplitude 0.03. Name offset
    (0, 0.65), name height 0. Its public size constants keep their names, because tests use them.
  - **`IsometricArtTable`:**
    - floor and rock 32×16;
    - walls at `BlockPivotY`;
    - the bunk's block;
    - 16×16 standing pieces at `FeetPivot`;
    - shadow and ring at their sprites' sizes;
    - name height 1.125;
    - `FeetPivot` moves here (`CutsceneStage` uses it).
  - **Nodes:** one row per sprite that today's lookup can reach. The keys are `node/Resource_OreVein`,
    `node/Resource_HerbPatch`, `node/Resource_Glowstone`, `node/Resource_BerryBush`, plus `node/default` = the berry
    bush. The lookup is by catalog `SpriteKey`, so no item is named.
- **`TopDownArt` and `IsometricArt` keep their `WorldArt` overrides,** each now one line reading its catalogue:
  - `Tile(kind) => catalog.Piece(_tile[kind], 0f)`;
  - `Hearth(t) => catalog.Piece(_hearth, t)`;
  - `Crawler(pose) => catalog.Piece(_crawler[pose.State], frame, pose.ScaleX, pose.ScaleY)`.
  - So the scene is untouched in V1, and `ArtSetTests` passes as it is.
  - `ViewMode` gains a `Catalog` property.
- **Tests (new `ArtCatalogTests`):**
  - the builder's refusals;
  - every key in `ArtKeys` resolves in both modes;
  - each catalogue piece equals the art set's piece of the parent commit, field for field (sprite, size, pivot,
    sheet), at t = 0, 0.1 and 1.7 s, for every tile kind, node item, crawler state × frame, hearth, bunk, ring and
    shadow. These expected values are taken from the old code before it is replaced; a scratch test on the parent
    commit prints them.
- Commit: `feat(presentation): the art catalogue, sizes and frames as data (graphics V1)`.

**V1 done when:** both art sets read a catalogue, sprite indices live only in sheets, and renders and pins are
unchanged.

---

## V2: visuals as data

### V2.1 A crawler's pose without a sprite
- **`CrawlerPose` carries `Frame` (0 or 1)** in place of `PixelSprite Sprite`.
  - `CrawlerAppearance` no longer touches art: it decides state, frame, tilt and squash, and the catalogue decides
    pixels.
  - `AnimationFrame` and `MicroAnimation` stay where they are. They are Unity's per-state behaviour and are used by
    `CutsceneStage` and the tests. Crawler definitions have `FramesPerSecond = 0`.
- **`CutsceneStage`** draws characters through the top-down catalogue's `crawler/<state>` frames, at
  `sprite size × CharacterScale` as today. The sprite's size comes from the sheet.
- **Tests:** `IsometricArtTests`, `WorldEpochTests` and `AppearanceTests` compare frames and keys instead of
  `PixelSprite` objects.
- Commit: `refactor(presentation): a crawler's pose is a state and a frame, not a sprite (graphics V2)`.

### V2.2 One question per entity
- **New `EntityVisuals`** (Presentation/Art), built per catalogue:
  - an `ArtId[]` by `EntityType`: `Anchor` → `prop/hearth`, `Bunk` → `prop/bunk`; `Wall`, `Stall` and `Walkable` →
    none;
  - an `ArtId[256]` by `ItemType` for nodes: `node/<SpriteKey>` when the catalogue has it, else `node/default`.
    Today's fallback, kept.
  - `bool TryProp(in EntityView e, out ArtId id)` is two array reads.
  - Crawlers keep their own path (the pose), through `CrawlerArt(state)`.
- **`WorldScene.AddProp`'s switch** becomes `if (Visuals.TryProp(entity, out var id)) Add(DrawRole.Prop, depth,
  Catalog.Piece(id, viewSeconds), position, tint);`. The role stays `Prop` for all three.
- Commit: `refactor(presentation): the scene asks each entity for its visual (graphics V2)`.

### V2.3 `WorldArt` retired
- **`ViewMode(name, projection, catalog)`.**
  - `WorldScene.Art` becomes `WorldScene.Catalog`.
  - Setting the mode resolves the scene's cached ids: the four tiles, ring, shadow and the visuals.
- **`WorldArt`, `TopDownArt` and `IsometricArt` are deleted.**
  - `ArtPiece` stays as the scene's per-item value.
  - `DynamicSceneView` reads `Mode.Catalog.NameHeight`.
- **`ArtSetTests.MarkedArt`** becomes a marked catalogue built with the same `Builder`:
  - real sprite names, but sizes 100 + k that identify each piece;
  - the test checks that every drawn item's size and pivot come from its definition.
  - That keeps the test's point: the scene draws what the art says, not what it knows.
- **`TopDownsArtIsUnitysSizesCentredOnTheirPoints`** reads `TopDownArtTable`.
- Commit: `refactor(presentation): a view mode is a projection and an art catalogue (graphics V2)`.

### V2.4 Completeness tests
- **`EverythingTheFramesCarryHasArt`:** for both modes, on Floor 1 and the Dev Map at tick 0, every frame entity the
  scene draws (crawlers, resources, anchors, bunks) resolves to a non-none visual.
- **`EveryNodeHasItsOwnArt`:** every item yielded by a node on a shipped map resolves to a node key other than
  `node/default`, with one **recorded exception: rations.**
  - Ration's `SpriteKey` is `Item_Ration`, which no sprite has, so garden plots draw the berry bush by fallback. That
    is today's picture.
  - V3 gives rations real node art and removes the exception.
  - Record the exception as a quirk in `core_refactor_plan.md` R7's quirk list.
- **`EveryCatalogueRowIsUsed`:** each row is reachable from `ArtKeys` or a node, so the tables don't collect dead art.
- Commit: `test(presentation): everything drawn has art; every node has its own (graphics V2)`.

### V2.5 A check, not a change: sub-pixel shimmer (P7)
- **Explicit test `ShimmerProbe`:**
  - draw one walking crawler across 16 sub-pixel offsets, at zoom 1 and 2, in both modes;
  - count how many silhouette pixels change per 1/16 px step.
- Write the finding into `docs/epics/epic-client.md`. If it shimmers, snapping anchors becomes a V3 item, because it
  changes renders; if not, P7 is dropped.
- No commit if there is nothing to keep beyond the finding (the finding goes in with V2.6's docs).

### V2.6 Docs
- **`AGENTS.md`'s client section:**
  - view modes are "a projection with its art catalogue (`ArtCatalog`, keys in `ArtKeys`, tables `TopDownArtTable`,
    `IsometricArtTable`)";
  - sprite sheets (`SpriteSheets`);
  - the art pins and the review sheet in the checks table.
- **`docs/epics/epic-client.md`:**
  - R5's catalogue half and S10 done, pulled forward; the packer and imports stay in M7;
  - the P7 finding.
- **`docs/ROADMAP.md`'s entry.**
- **`.agents/architecture_history.md`:** one entry with the decisions (keys over indices, tables over classes, units
  × scale order, the ration fallback) and the deviations.
- `graphics_plan.md`'s §3 marks V0–V2 done. This file is deleted, after its findings move to the epic.
- Commit: `docs: the art catalogue and visuals as data (graphics V0-V2)`.

**V2 done when:** `WorldArt` is gone, the scene's per-kind code is one visuals table, the completeness tests pass,
renders are byte-identical, and frame time is within ±10%.

---

## Size and order

| Step | Size | Touches |
| :--- | :--- | :--- |
| V0.1–V0.4 | ~1 day | `SpriteSheets`, `ClientGraphics`, tests, `art-pins.txt` |
| V1.1 | ~0.25 day | `DrawSheet`, `ClientGraphics`, ~6 tests |
| V1.2 | ~0.5 day | `PixelSprite`, generators, `SpriteAtlas`, consumers |
| V1.3 | ~1 day | `Art/` (catalogue, keys, tables), art sets as thin wrappers |
| V2.1–V2.4 | ~1–1.5 days | `CrawlerAppearance`, `WorldScene`, `ViewMode`, cutscenes, ~15 tests |
| V2.5–V2.6 | ~0.5 day | Probe, docs |

That is about 4–5 agent days.
- The work goes on one branch (`feat/graphics-catalogue`, from `main` after this plan lands) with one commit per
  step. It ships as one PR into main.
- Each commit passes the gate on its own, so a bisect lands on a step.

## Risks

- **V1.1's texture change could move a sampled texel** (see V1.1). The fallback is ready, and nothing later depends
  on that step.
- **Float order:** invariant 2. `ArtCatalogTests` compares pieces field for field with the old code, so a last-bit
  difference shows up as a test failure, not as a mystery render diff.
- **Test churn:** about 15 tests name sprite fields or `DrawSheet.Sprites`. A small helper,
  `ArtTest.Sprite(ViewMode, key, frame)` → sheet index, keeps each change a one-line diff.
- **Gum and the dashboard aren't touched** (V4). The cutscene stage is, in V2.1, and its renders (`render-cutscene-*`)
  are part of the gate.

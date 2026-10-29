# Graphics V3–V5: item art, icons in the menus, items in the world

Working plan, 2026-10-29. This is the implementation of phases V3–V5 of `.agents/graphics_plan.md`, built on V0–V2.
V0–V2 are done on `feat/graphics-catalogue` and recorded in the epic's §3:
- the art catalogue;
- sprite indices owned by sheets;
- `EntityVisuals`;
- every sprite pinned by name;
- the review sheet.

Status is not tracked here. When the producer says go, the roadmap gets its entry first: Epic CLIENT, ACTIVE again.
The work starts on a new branch from `main` once the catalogue's pull request has merged (no stacked branches).

**What V3–V5 deliver:**
- **V3:** every catalog item has a menu icon and a map sprite, generated from a shape and its colours. Rarity shows on
  icons. The world's sprites sample the same way whatever the atlas looks like (closing F12), so the atlas can be
  packed and the two world sheets merged.
- **V4:** the merchant terminal, the dossier, the bounty board and the header show item icons.
- **V5:** items are visible in the world:
  - the stall is drawn as furniture, with its goods on display;
  - crawlers wear their hazard gear;
  - a crawler carrying salvage shows it.

**Pictures change in this work, on purpose.** Every picture change is its own step:
1. the producer looks at the art review sheet, or at `container-output/render-output/`;
2. then the renders are re-recorded in the pinned image (`scripts/test-client-in-container.sh --record`) and the art pins
   with `SIMPIENS_RECORD_ART=1`;
3. the re-recording is committed on its own, after its code commit.

---

## What V0–V2 left that shapes this plan

### F12 and F19, measured again on 2026-10-29

**The camera zooms continuously.** `CameraInput.ZoomStep` is 0.85 per wheel notch, and `CameraRig.ZoomAt` doesn't
snap. So in play, the view is almost always at an in-between scale. A scratch run of `ShimmerProbe` at in-between zooms
(not committed) showed:

| Zoom | Top-down crawler (shapes / pixels) | Isometric crawler (shapes / pixels) |
| ---: | :--- | :--- |
| 1 | 9 / 47–55 | **1** / 105 |
| 1.25 | 8 / 75–87 | 4 / 160–172 |
| 1.37 | 10 / 85–105 | **14** / 190–202 |
| 1.5 | 12 / 105–128 | 2 / 232–241 |
| 2 | 5 / 201–210 | **1** / 420 |
| 2.5 | 4 / 315–325 | 2 / 649–664 |

The epic's F19 ("isometric's crawlers don't shimmer") holds only at whole zooms. **At the zooms players use, both
views shimmer,** and both sample differently when the atlas changes (F12). The cause is one thing:
- with point sampling at an in-between scale, a screen pixel's centre can land exactly on a texel's edge;
- which texel wins then depends on float rounding, including the texture's width (F12);
- and it depends on the sprite's sub-pixel position (F19's shimmer).

### The fix, as a lead developer would do it

Two rules, both in the presentation/renderer seam, with no change to how zoom feels:

1. **Standing pieces land on whole pixels.**
   - Props, crawlers, their shadows and the selection ring snap their screen anchor to a whole pixel.
   - Tiles and walls don't: they tile, and snapping each would open hairline gaps at in-between zooms.
   - A sprite's left edge then sits at the same fraction of a pixel wherever it moves at a given zoom. Its shape is
     constant (no motion shimmer, in both views), and a crawler moves in whole-pixel steps, as pixel art should.
   - The snap goes in `ScreenQuad.Of`, so picking hits what is drawn.
2. **A fixed sampling bias.**
   - The renderer shifts every point-sampled quad by a small constant (≈0.13 px; deliberately not a binary fraction).
   - At a whole scale it changes nothing: a pixel's centre is half a texel from any edge.
   - At an in-between scale it moves pixel centres off texel edges. Which texel wins is then decided by geometry, not
     by rounding, and so not by the atlas's layout.
   - It's large enough to survive a GPU's 8-bit sub-pixel rasterizer at any zoom the rig allows.

**Optional, a product call (decision D6):** whole-step zoom (1×, 2×, 3×… above 1:1, eased between steps). It is the
crisp choice: every texel is the same size. But it changes the zoom feel the producer approved in M3. The default keeps
the smooth zoom; the two rules above make it stable.

### Other facts that shape V3–V5

- **Items carry `SpriteKey`.** Four of the six set it, to their node's sprite name (rations' is the berry bush); the
  pickaxe and hazard gear default to `Item_<name>`, which nothing draws.
- **`ItemNamesTests` matches only `ItemType.X` in code.** Art keys are strings (`iron-ore`), so presentation can key
  art by them without naming an item.
- **The stall isn't a world entity.** It is a `TradeStallData` (plain, unmanaged: its goods table holds item, role,
  price and stock) in `ISimulationManager.ActiveStalls`. A frame can copy it whole. `WorldViewFrames.Follow` doesn't
  take the simulation manager yet.
- **Every crawler pose draws its hair band with palette letter `H`**, standing, panicking or asleep. A helmet is the
  same pattern with `H` recoloured: aligned in every pose, with no anchor detection.
- **Gum:**
  - `SpriteRuntime` takes `Texture` and a `SourceRectangle` (XNA's `Rectangle`, the same in KNI and MonoGame; set
    `TextureAddress` to custom);
  - `Renderer.GetDrawStateSummary()` counts a frame's sprite-batch begins, texture switches and Apos.Shapes
    (`ShapeBatchBeginCount`) begins;
  - the panels' fills and borders are `RectangleRuntime`s interleaved with text. Whether they draw through Apos.Shapes
    (an optional Gum package) or Gum's sprite batch, and how many batch begins they cost, is what V4.0 measures; it is
    the likeliest cause of UiDraw's 3.1 ms;
  - Gum's texture filter is global; fonts are drawn 1:1 from baked atlases (F8).
- **Where items appear in the menus today:**
  - the merchant's rows (labels from `ItemTexts`);
  - the dossier's "Gear:" line;
  - the bounty board's item picker (a `ComboBox` of words);
  - the header's stock words ("12 RATIONS", "3 ORE | 2 HERB").

---

## Invariants for every step

1. **Views decide nothing.** Models carry keys (an icon's art key); the client resolves pixels. Presentation stays
   XNA-free.
2. **The frame path allocates nothing.** Art keys resolve to ids when a catalogue or mode is set; per-item lookups are
   at most one dictionary probe on an art key (so items a test adds still resolve). `FrameAllocationTests` stays green.
3. **No item names in presentation code.** Art is keyed by the catalog's art key. `ItemArt` (colours and shapes) is
   keyed by art-key strings, which the guard doesn't match.
4. **Pixel art is drawn at whole scales where it is authored for it.** Icons are pre-magnified and drawn 1:1, like the
   fonts; world sprites follow the two sampling rules.
5. **Generated art is deterministic:** integer arithmetic and plain float ops (no `Sin`), pinned by name in
   `art-pins.txt`.
6. **No simulation change.** The art key is presentation data on a definition: no save format, no world hash, no
   behaviour. Golden 3/3 and the soak are run once in V3.4, the only step touching `Simpiens.Core`, to prove it.

## Gate for every commit

- `dotnet build dotnet/Simpiens.slnx`: 0 warnings.
- `dotnet test dotnet/Simpiens.slnx`: green.
- `scripts/test-client-in-container.sh`: green and exact. A step that changes pictures runs it with `--record` in its
  own re-record commit, after the look.
- MonoGame compiles.
- At the end of each phase:
  - Android compiles;
  - `FrameTimeProbe` is within ±10% of the epic's F20 (the world) and of V4.0's measurement (the UI).

---

## V3: sampling, the atlas, and item art

### V3.1 The sampling rules (pictures change)
- **`DrawItem.SnapToPixel`** (a bool), set by `WorldScene.Add` for `Prop`, `Crawler`, `Shadow` and `Selection`.
  `ScreenQuad.Of` rounds the anchor (`SimMath.Round`, half to even) when it is set. Picking goes through `ScreenQuad`,
  so it follows.
- **`WorldRenderer.SampleBias`:** a constant, added to every non-floor, point-sampled quad's screen position. The fog's
  smooth floor quads and the glyphs (1:1) are untouched.
- **`ShimmerProbe` gains:**
  - the in-between zooms of the table above;
  - the snap, which it must show leaves one shape per zoom in both views;
  - a sweep of every zoom the rig reaches in 40 wheel notches either side of the fit, asserting that no pixel centre
    lies within 1/64 texel of an edge once biased.
  - It stays an explicit measurement, plus that last check as a **regular test** (CPU only, fast):
    `NoPixelCentreSitsOnATexelEdge`.
- **Commits:**
  1. `fix(presentation): standing pieces land on whole pixels; point sampling biased off texel edges (graphics V3, F12, F19)`;
  2. then the re-recorded renders and picture pins, on their own (after a look at the diffs: crawlers and props move by
     at most half a pixel; zoomed views change texels at former ties).

### V3.2 The packer and one world sheet (no picture change: the proof)
- **New `Simpiens.Presentation/Art/AtlasLayout.cs`** (pure, testable): shelf packing.
  - Sprites are sorted by height, then by sheet index (stable).
  - Gutter 2 px.
  - The width is the smallest multiple of 64 that keeps the atlas near square.
  - It refuses a layout over `MaxTextureSize = 2048` (the safe floor for M4's phones).
- The client's `SpriteAtlas` lays a sheet out with it.
- **`DrawSheet.Sprites` is retired, and `Isometric` is renamed `World`.** Both catalogues draw from one world sheet.
  Top-down's rows and the cutscene stage follow; `SpriteSheets` has `World`, `Fog`, `Backdrop`.
- **Tests:**
  - `AtlasLayoutTests`: no overlap, gutters kept, deterministic, every sheet within budget.
  - **`ThePictureDoesNotDependOnTheAtlasLayout`:** the outpost at t300 top-down and isometric, and both zoomed views,
    rendered with the world atlas packed normally and packed in reverse order, must match byte for byte. It needs a
    test-only hook in `ClientOptions` (`AtlasOrder`).
- **Gate:** every render byte-identical to V3.1's. **If they differ, the sampling rule isn't closed, and that is a bug in
  V3.1, not a re-record.**
- Commit: `refactor(client): one world sheet, packed by shelves within a texture budget (graphics V3)`.

### V3.3 The missing-art placeholder (no picture change)
- **`fx/missing`:** a 16×16 magenta-and-black checker, in the world sheet and in every catalogue.
  - `ArtCatalog.Node` and the item lookups (V3.6) fall back to it, not to the berry bush.
  - `node/default` goes. It is also the fallback in the icon catalogue (V3.6).
- **Tests:** `ArtCompletenessTests` fail when shipped content reaches `fx/missing`, naming the item and the picture.
- Renders: identical (nothing shipped is missing).
- Commit: `feat(presentation): missing art is loud, and fails a test (graphics V3)`.

### V3.4 The art key (core; no behaviour change)
- **`ItemDefinition.SpriteKey` becomes `ArtKey`:** a short, stable, kebab-case id, defaulting to the name in kebab case
  (`ration`, `iron-ore`, `medicinal-herb`, `pickaxe`, `hazard-gear`, `glowstone`). The catalog sets none explicitly.
- **`ItemCatalogValidator`:** art keys are non-empty, unique, and kebab-case (`[a-z0-9-]+`).
- **Presentation:**
  - node rows are keyed `node/<art key>`, mapping to today's sprites (`node/ration` → the berry bush,
    `node/iron-ore` → the ore vein, …);
  - `EntityVisuals`, the test accessors and `ArtCompletenessTests` follow.
- **Gate:** renders identical; golden 3/3 and the soak identical to `main` (run once here, the only core touch).
- Commit: `refactor(core): an item's art key replaces its sprite key (graphics V3)`.

### V3.5 The sprite kit (no picture change)
**New `Simpiens.Presentation/Art/SpriteKit.cs`.** Deterministic image operations on `Rgba32[]`, each pinned by a unit
test on small inputs:
- **`Ramp(Rgba32 base)` → 4 tones:**
  - highlight: lighter, less saturated, hue nudged toward yellow;
  - base;
  - shade: darker, hue nudged toward blue;
  - deep.
  - It works in integer HSV, with rounding stated, so the same base gives the same bytes on any machine.
- **`Paint(string[] shape, primary ramp, secondary ramp)`:**
  - a silhouette's letters `1`–`4` take the primary ramp, `a`–`d` the secondary;
  - `.` is clear;
  - any other letter is refused.
- **`Outline(pixels, colour)`:** a 1-px outline (4-neighbour) around opaque pixels, in the crawlers' outline colour
  (20, 24, 33).
- **`Icon(glyph, rarity)`:**
  - a 16×16 plate (the dashboard's inset slate, corners clear) with a 1-px rim in the rarity's colour;
  - the outlined 12×12 glyph is centred on it;
  - rarity colours come from a `RarityColours` table (common slate grey, uncommon green, rare blue, epic purple,
    legendary amber, the dashboard's palette), decision D3.
- **`Recolour(pattern, palette, letter, colour)`:** for worn variants (V5.2).
- Commit: `feat(presentation): a sprite kit: ramps, painting, outlines, icon plates (graphics V3)`.

### V3.6 Item art (art changes; renders don't)
- **`Simpiens.Presentation/Art/ItemShapes.cs`:** hand-drawn 12×12 silhouettes, **one per shape, not per item**:
  - `chunk` (ore, glowstone);
  - `sprig` (herb);
  - `pack` (ration);
  - `helmet` (hazard gear: falling rock and foul air);
  - `pick` (pickaxe).
  - Plus `bundle`, 8×8, the carried marker (V5).
  - A shape no item uses doesn't exist (`EveryShapeIsUsed`).
- **`Simpiens.Presentation/Art/ItemArt.cs`:** one line per art key: shape, primary base colour, optional secondary, and
  how it is worn (`Worn.Head` for protection, `None` otherwise). Adding an item's art is one line.
- **Sprites, generated at start-up from those two tables through the kit:**
  - **`Item_<key>_Icon`** (16×16) go in a new **icon sheet** (`DrawSheet.Icons`, point-sampled, never drawn by the
    world renderer);
  - **`Item_<key>_Map`** (16×16 canvas: the outlined glyph, bottom-centred) go in the world sheet;
  - **`Item_<key>_Carried`** (8×8 bundle tinted with the item's primary colour) go in the world sheet.
- **Catalogue rows:**
  - `item/<key>/map` and `item/<key>/carried` in both mode tables (top-down at 0.6 and 0.35 of a cell, isometric
    standing at 16 and 8 art pixels, feet pivot);
  - `item/<key>/icon` in a new **`IconArtTable.Catalog`** (icons sheet, 16×16, unit scale 1).
  - Lookups: `ArtCatalog.Item(string artKey, ItemPicture picture)` → `ArtId`, `fx/missing` when absent.
- **Tests:**
  - `ArtCompletenessTests`: every catalog item has an icon, a map sprite and a carried marker that aren't missing;
  - `EveryCatalogueRowIsUsed` counts item rows as used through the catalog's art keys;
  - `ArtPinTests` re-recorded (new sprites only: every existing pin unchanged).
- **The look:** the review sheet gains an "Items" section (icons at 2× beside their map sprites). **The producer looks at
  `art-sheet.png` before the pins are recorded.**
- **Commits:**
  1. `feat(presentation): every item has a menu icon and a map sprite, from its shape and colours (graphics V3)`;
  2. then `test(client): pin the item art` (the art pins) on their own.

**V3 done when:**
- the sampling rules hold (the layout test and the edge sweep);
- there is one packed world sheet;
- missing art fails a test;
- every item has its three pictures;
- `ItemArt` is the only place an item's look is described.

Golden and soak are unchanged.

---

## V4: icons in the menus

### V4.0 Measure first (no change)
- An explicit probe, `UiDrawProbe`: the dossier-panels frame of `FrameTimeProbe`, printing Gum's
  `GetDrawStateSummary()`:
  - sprite-batch begins, and why (clip, state, texture);
  - Apos.Shapes begins, if the fills go through it;
  - the text runs.
- **Then one experiment, if the begins are what costs:** Gum's batch-grouped sibling ordering (`BatchKeyGroupedOrderer`),
  which exists to cut them. Keep it only if the dashboard renders are byte-identical and the draw time falls. Otherwise record
  why not.
- The numbers become the epic's F21. Icons then must not raise the UI draw by more than 10% over them.
- **The filter spike:** a test window with an icon drawn 1:1 at whole pixels, rendered under Gum's point and linear
  filters, must match byte for byte. That proves icons pre-magnified and drawn 1:1 are filter-independent, as the fonts
  are (F8).
- Commit: `test(client): what the dashboard's draw costs, by cause (graphics V4)`, plus the ordering change if it
  passed.

### V4.1 The icon atlas (client)
- **`IconAtlas`:** the icon sheet magnified ×2 on the CPU at load (`IconScale = 2`, decision D2; 32×32 per icon), one
  texture, and `Source(string iconKey)` → `Rectangle`. Built in `ClientGraphics`.
- **`UiParts.Icon(IconAtlas, string key)`:**
  - a 32×32 `SpriteRuntime` with `TextureAddress` custom and its source rectangle;
  - `UiParts.SetIcon` re-points it;
  - a null key hides it.
- **Whole pixels:** icons are placed at integer offsets in fixed-size rows. A test reads each icon's absolute position
  from Gum and asserts whole pixels.
- Commit: `feat(client): an icon atlas and an icon element (graphics V4)`.

### V4.2 Icon keys in the models (presentation)
- `MerchantRow.Icon`.
- `DossierModel.GearIcon` (null with no gear).
- `BountyBoardModel.ItemIcon` (the chosen gather item).
- `HeaderModel.Stock`: a list of (icon, count) pairs, the food first, then the resources in id order, in place of the
  stock words (decision D9).
- Keys come from `ItemCatalog.Get(item).ArtKey` through `ArtKeys.ItemIcon`. The words stay in `ItemTexts`, for tooltips
  and screen readers later.
- Tests: the models' icon keys for today's stall, gear and bounty choices; each resolves in `IconArtTable.Catalog` to a
  non-missing row.
- Commit: `feat(presentation): the dashboard's models name their items' icons (graphics V4)`.

### V4.3 The windows (pictures change)
- **The merchant:** an icon at the left of each row, with the labels moving right by 40 px. The window keeps its width if
  `TheTreasuryAndTheStockLineDoNotOverlapAtTheirLongest` still passes; otherwise it widens, and the test says why.
- **The dossier:** the gear's icon before "Gear:" text.
- **The bounty board:** the chosen item's icon beside the item picker. The `ComboBox` keeps its words; icons inside Gum's
  list items are deferred.
- **The header:** icon plus count per good, in place of the stock words (D9).
- **Tests:**
  - layout tests for the rows;
  - `UiTextTests` and the header's (no clipping at 1280×720);
  - the frame-time check against V4.0.
- **Commits:**
  1. `feat(client): item icons in the merchant terminal, the dossier, the bounty board and the header (graphics V4)`;
  2. then the re-recorded dashboard renders (`render-outpost-*`, `render-iso-header-t300`, `render-devmap-banner-t300`,
     `render-iso-devmap-t300`) on their own, after the producer's look.

**V4 done when:**
- the four places show icons;
- the UI's draw cost is measured and within 10%;
- icons are proven filter-independent.

---

## V5: items in the world

### V5.1 What the frames carry (presentation; no picture change)
- **`EntityView` gains:**
  - `Worn` (the protection slot's item; `None` when empty);
  - `Carried` (the first salvage the crawler holds: `InventoryGroups.FirstHeld(ItemCategory.Resource)`; `None`
    otherwise).
  - Both are plain data a network client could replicate.
- **`WorldViewFrame` gains `Stalls`:** a reused array of `TradeStallData` copies, from `ISimulationManager.ActiveStalls`.
  `WorldViewFrames.Follow` takes the simulation manager (as an `IStallSource` seam, so tests can feed stalls).
- **Tests:**
  - `ViewFrameTests`: worn, carried and stalls are captured, and cleared on a new epoch;
  - `FrameAllocationTests`: still zero;
  - `ViewsWriteNothingTests`: still green.
- Commit: `feat(presentation): frames carry worn gear, carried salvage and the stalls (graphics V5)`.

### V5.2 The art (art changes; renders don't)
- **The stall:**
  - a top-down 16×16 pattern (a counter under an awning) and an isometric placeholder block (`IsometricSprites`, like
    the bunk);
  - rows `prop/stall` in both tables;
  - display slots as data: up to 4 slots in the tables (floor offsets and heights from the stall's anchor), like
    `NameOffset`.
- **Worn variants:**
  - for each item whose `ItemArt` says `Worn.Head`, every crawler state and frame is regenerated with `H` recoloured to
    the item's primary ramp (`SpriteKit.Recolour` over the existing patterns): 24 sprites per wearable;
  - rows `crawler/<state>/<art key>`.
  - Only hazard gear today.
- **The carry offset** (`CarryOffset`, `CarryHeight`) in both tables: above the head, as the selected crawler's name
  hangs.
- **Tests:**
  - every worn variant differs from its base sprite only where the base has `H`;
  - completeness covers the stall and the worn variants.
- The producer looks at the review sheet; the art pins are re-recorded on their own.
- Commit: `feat(presentation): the stall's art, worn hazard gear, and where carried goods hang (graphics V5)`.

### V5.3 The scene (pictures change)
- **The stall:**
  - `WorldScene.AddStalls` draws each stall from the frame at its position, as a `Prop`, faded with the fog;
  - its goods in stock go on its display slots, in table order: the stall's `item/<key>/map` sprites, drawn after the
    stall at the same depth;
  - not pickable yet (clicking it to open the merchant terminal is deferred).
- **Worn gear:** `EntityVisuals.Crawler(state, worn)` picks `crawler/<state>/<art key>` when the catalogue has it, and
  the plain crawler otherwise.
- **Carried salvage:** `item/<key>/carried` at the carry offset above the crawler, drawn just after it. It is pickable
  as the crawler (its pick slot), so a click on the bundle selects its carrier.
- **Tests:**
  - scene tests on built frames (a stall with stock shows its goods and none without stock; a geared crawler draws its
    variant; a carrier draws its bundle);
  - isometric draw order (the goods over the counter, the bundle over the head);
  - picking.
- **Commits:**
  1. `feat(presentation): the stall stands in the hub with its goods; crawlers wear their gear and show what they carry (graphics V5)`;
  2. then the re-recorded map renders and picture pins, after the producer's look.

### V5.4 It happens in the live game (the Definition of Done's spirit, for views)
`LiveItemsInTheWorldTests` (client tests, headless) runs the real outpost session with nothing injected, through the
frames and the scene, and asserts, within a bounded number of ticks on the gate seed:
- the stall is drawn with at least one good on display from tick 0;
- a crawler that bought hazard gear (G3's behaviour) is drawn wearing it;
- a crawler walking salvage to the stall is drawn carrying it.

The tick bounds come from G3's live scenario and the supply loop's; the test reports the first tick of each.

Commit: `test(client): the stall's goods, worn gear and carried salvage show in the live outpost (graphics V5)`.

**V5 done when:**
- the stall, its goods, worn gear and carried salvage are drawn;
- they appear in the live game without injection;
- renders and pins are re-recorded after a look.

---

## V6: close-out (from `graphics_plan.md`)

- **`AGENTS.md`:** "Adding an item" step 4 becomes "its art: a line in `ItemArt` (shape and colours; a new shape in
  `ItemShapes` if none fits)". A short "Adding art" note covers the catalogue tables, the review sheet and the pins.
- **The epic:**
  - §3 lists the steps;
  - §6 records F12 closed, F19 corrected (in-between zooms), F21 (the UI's draw cost) and the frame times;
  - §4 keeps M7's imports, 64×64 art and premultiplied alpha.
- **The roadmap.**
- **`architecture_history`.**
- **Delete this plan and `graphics_plan.md`**, after moving their open items (decision D6 if declined; the deferrals
  below) to the epic.

---

## Size and order

| Step | Size | Pictures |
| :--- | :--- | :--- |
| V3.1 sampling rules | 0.5–1 day | Change (re-record) |
| V3.2 packer and one sheet | 0.5 day | Identical (the proof) |
| V3.3 missing placeholder | 0.25 day | Identical |
| V3.4 art key | 0.25 day | Identical; golden and soak run |
| V3.5 sprite kit | 0.5–1 day | None |
| V3.6 item art | 1 day | Art pins only, after the look |
| V4.0 measure and spike | 0.5 day | Identical (or the ordering, if byte-identical) |
| V4.1–V4.3 icons | 1–1.5 days | Dashboard re-recorded, after the look |
| V5.1 frames | 0.5 day | None |
| V5.2 art | 0.5–1 day | Art pins only, after the look |
| V5.3 scene | 1 day | Map re-recorded, after the look |
| V5.4 live check | 0.5 day | None |
| V6 docs | 0.25 day | None |

That is about 7–9 agent days.

**The producer looks at four things:**
1. V3.1's sampling diffs;
2. V3.6's item art on the review sheet;
3. V4.3's dashboard;
4. V5.2–V5.3's art and map.

Phases can ship as separate pull requests (V3, then V4, then V5). Each leaves `main` whole, so the producer can stop
after any of them.

## Decisions for the producer (defaults in bold, standing unless contested)

Decisions D1–D5 of `graphics_plan.md` stand as accepted:
- items at 16×16 with icons at 2×;
- rarity rims;
- the stall's display, worn gear and carried salvage now;
- top-down on the same catalogue.

D1, the timing, needs a go for V3–V5.

- **D6 Zoom:** **keep the smooth zoom, made stable by the two sampling rules.** The alternative is whole-step zoom (1×,
  2×, 3×… eased between steps): crisper, with every texel the same size, but a change to the zoom feel you approved in
  M3, and to the camera tests (14 of them, with 16 zoom calls).
- **D7 Nodes:** **keep today's node sprites** (Unity's, and G2's glowstone), keyed by art key. The alternative
  regenerates them through the sprite kit for one style; that changes the map and retires the pin of the art ported
  from Unity.
- **D8 Worn gear:** **a helmet: the hair band recoloured in the item's colours, in every pose.** The alternative is an
  overlay layer per slot, which scales to many slots but needs head anchors per pose.
- **D9 The header:** **stock as icon plus count** (food, then resources). The alternative keeps the words and adds
  nothing there.
- **D10 Carried salvage:** **a small bundle in the salvage's colour above any crawler holding salvage.** The alternatives
  are only while walking to the stall, or not at all.
- **D11 The stall's display:** **up to four goods in stock, in the stall's table order.**

## Deferred (owners)

- Clicking the stall to open the merchant terminal: with the stall as an entity, or with map POIs becoming pickable
  (Epic CLIENT, after V5).
- Icons inside the bounty board's dropdown (Gum list item templates): after V4, if wanted.
- Dropped items as world entities, and loot in play: the epic that brings loot into play.
- Tools shown in hand (the harvest pose's `T` pixels recoloured for an equipped tool): when tools have a source (Epic 10
  Part 2).
- Imported art (PNG and Aseprite), 64×64 isometric art, premultiplied alpha, `Texture2D.FromStream`: Epic CLIENT M7.
  Imports override a key's generated sprite.

## Risks

- **V3.1 moves many pixels at once** (every zoomed render and every crawler's sub-pixel position). That's why V3.2 and
  V3.3 must then be byte-identical. That identity is the evidence the rules work, and the layout test keeps it true.
- **A GPU that rasterizes with fewer sub-pixel bits** than llvmpipe could still see a tie at extreme zooms. The bias is
  sized for 8 bits at the rig's largest zoom, and the CPU edge sweep checks the margin.
- **Gum's sibling ordering may reorder overlapping elements.** Kept only if the renders are byte-identical.
- **Worn variants multiply sprites** (24 per wearable). That's fine at 16×16 and a handful of items; an overlay (D8's
  alternative) is the path if wearables grow.
- **The live check's tick bounds depend on the economy.** It reports first ticks, so a slower economy changes a number,
  not the test's meaning.

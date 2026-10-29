# Graphics plan: items you can see, and the art as data (V0–V6)

Working plan, 2026-10-28. Written after the item system (PR #15). Status is not tracked here: this is Epic CLIENT
work that pulls part of M7 forward. Its status goes into `docs/ROADMAP.md` first, once the producer says go.

The producer asked three things:
- Do walls, furniture, crawlers and items share an interface for rendering?
- How should we generate item sprites for the map and icons for the menus?
- Where can the rendering pipeline improve?

This plan answers as the lead developer. It ends with phases, gates and decisions.

---

## 1. What we have (read 2026-10-28, main at `852902b`)

### 1.1 The pipeline

```
SessionHost.TickCompleted
  └─ WorldViewFrames.Capture        plain data per entity: id, type, ResourceKind, position, visual state, flags
WorldView.Update
  └─ WorldScene.Advance             fog fades, crawler poses (3-tick hold)
WorldView.Draw
  └─ WorldScene.Present             tiles, room names, fog, props, crawlers, selection → DrawList
       ├─ WorldArt (TopDownArt | IsometricArt)    "what it looks like": ArtPiece (sheet, sprite index, size, pivot)
       ├─ IViewProjection                         "where and in what order": layer and depth per DrawRole
       ├─ DrawList.Cull → Sort                    64-bit key: layer, depth, role, sequence (stable, no allocation)
  └─ WorldRenderer.Draw             SpriteBatch Deferred, a batch per sheet change, point sampling for pixel art
  └─ DrawGlyphs                     map text from the baked Inter atlases, 1:1
UiLayer (Gum)                       dashboard windows, text only
```

This layering is sound, and it should not be rewritten:
- Views read frames and never the simulation.
- The scene is engine-free (Presentation has no XNA).
- The sort is stable and deterministic, culling happens before sorting, and a steady frame allocates nothing
  (`FrameAllocationTests`).
- Picking hit-tests what was drawn, through the same `ScreenQuad`.
- Pixels are pinned by byte-exact renders in the pinned image, plus picture pins.

The problems are in the layer above the pipeline: how art is named, chosen and authored.

### 1.2 Do walls, furniture, crawlers and items share a rendering interface?

**Half of one.** Everything drawn ends up as the same thing, a `DrawItem` in one `DrawList`, built from an `ArtPiece`.
That common currency is good and stays. Above it, nothing is shared: each kind of thing has its own path.

| Thing | How the scene finds its picture | Where its sprite comes from |
| :--- | :--- | :--- |
| Floor, rock, walls | `MapTileLayer` → `Art.Tile(TileKind)` / `Art.LowWall` | static fields `ProceduralSprites.FloorSprite`, `IsometricSprites.Wall`, … |
| Resource node | `EntityType.Resource` in `AddProp`'s switch → `Art.Resource(ItemType)` | `ProceduralSprites.GetResourceSprite`, via the item's `SpriteKey` (the only lookup by name) |
| Hearth | `EntityType.Anchor` → `Art.Hearth(seconds)` | two static campfire frames; animation math written into each art set |
| Bunk | `EntityType.Bunk` → `Art.Bunk` | static field per art set |
| **Stall** | **not drawn.** It isn't a world entity: it is a `TradeStallData` at a POI | none |
| Crawler | `AddCrawler` → `CrawlerAppearance.Pose` → `Art.Crawler(pose)` | `ProceduralSprites.GetAgentSprite(state, frame)` |
| **Items** (rations, gear, ore in a pack) | **not drawn anywhere**, on the map or in the menus | none (the merchant's rows are text) |

The consequences:
- **`WorldArt` is an abstract class with one method per kind of thing.** To add a kind (a stall, a dropped item, a
  door), you edit the scene's switch, the abstract class and both art sets. The art set is code, not data.
- **Sprites are named by C# fields, and their indices are positions.** `PixelSprite.Index` is its place in creation
  order. The isometric indices start after `ProceduralSprites.All.Count`, so adding a top-down sprite moves every
  isometric index. That is harmless today, because nothing stores an index, but it is the wrong foundation for a
  catalogue.
- **An item's `SpriteKey` names its node's sprite** (`Resource_OreVein`), not a picture of the item. "The thing on
  the map that yields ore" and "ore" are different pictures, and so are "ore in a menu" and "ore on the floor".
- **The same 16×16 art sits in two textures.** `DrawSheet.Sprites` holds the top-down art; `DrawSheet.Isometric`
  holds all of it again plus the placeholders. The two exist only because the isometric sheet was added beside
  top-down's in M3.

### 1.3 Authoring

`ProceduralSprites.cs` is 966 lines of 16×16 ASCII patterns, palettes and lookups. `IsometricSprites` draws
diamonds and blocks in code. `CutsceneArt` paints backdrops.

The approach is right for this project:
- it is text, so it diffs;
- an agent can write it;
- it is deterministic and pinned by tests;
- there is no binary asset pipeline to maintain.

Three things are wrong with it:
- data and code live in one class;
- a variant is a whole copy: the three resource nodes are the berry bush's pattern with a new palette, and each agent
  state is a pattern plus a hand-made palette;
- there are no shared operations (outline, shade, frame, shadow), so every new sprite is hand-drawn from scratch.

### 1.4 Measurements (llvmpipe, `FrameTimeProbe`, 2026-10-28)

| Scene | Frame (CPU) | ScreenDraw | UiDraw | Draw items |
| :--- | ---: | ---: | ---: | ---: |
| Top-down, 800×600 | 2.71 ms | 1.55 ms | 0.80 ms | 1,850 |
| Isometric, 800×600 | 1.43 ms | 0.98 ms | 0.31 ms | 1,580 |
| Panels with a dossier, 1280×720 | 4.49 ms | 1.01 ms | **3.15 ms** | – |
| Bounty board over the map, 1280×720 | 1.68 ms | 0.86 ms | 0.74 ms | – |

The world is cheap: about 1 ms for about 1,600 sprites, most of them static tiles rebuilt each frame. **The most
expensive thing on screen is Gum drawing the dashboard panels** (70% of the frame). Icons will add to that cost, so
it has to be measured before and after (V4).

---

## 2. Recommendations

### 2.1 One way to name a picture: art keys, resolved once to ids

- Replace "which C# field" with **art keys**: stable strings such as `item/ore/icon`, `item/ore/map`, `node/ore`,
  `prop/hearth`, `prop/stall`, `tile/wall`, `crawler/walking`.
- An **`ArtCatalog`** per view mode maps each key to an **`ArtDefinition`**:
  - its frames (sprite ids);
  - frames per second;
  - size in art pixels;
  - pivot;
  - sheet.
- The catalogue is built once, when the views start.
- Keys become dense `ArtId` ints at build time. The frame path only ever handles ids, so it never touches strings or
  allocates.
- **Sprite indices are given out by the atlas builder,** not by creation order. Nothing outside the client's atlas
  knows them.
- **A missing key resolves to a loud placeholder** (magenta and black). A test fails when content (a catalog item,
  an entity kind the scene draws) has no art of its own, the same way `ItemNamesTests` guards item names.

This is the "sprite catalogue as data" of Epic CLIENT M7 (review R5) and "sizes as data" (S10), done now at the
scale we need. M7's later work, PNG and Aseprite import and 64×64 art, becomes a new source of pixels for the same
keys. No code changes for it.

### 2.2 One question per thing drawn: what is its visual?

The scene's per-kind code collapses into one function in Presentation:
`Visual(in EntityView entity) → (ArtId, DrawRole, animation phase)`.
- A crawler maps its pose to `crawler/<state>`.
- A node maps to `node/<item art key>`.
- The hearth maps to `prop/hearth`, a bunk to `prop/bunk`, and so on.
- `WorldArt`'s per-kind abstract members go away. A view mode becomes a projection plus an `ArtCatalog`.
- Pose effects that are code stay as code: squash, tilt and the hearth's pulse are a small `ArtMotion` the definition
  names, so the motion is not data hidden in strings.

**This is not an interface on simulation entities.** Entities are plain data, views decide nothing, and a
multiplayer client fills the same frames from replicated state. "A thing that can be rendered" means **a thing whose
frame row maps to an art key**. That mapping is presentation's.

### 2.3 Items: one art key in the core, three pictures in presentation

- Rename `ItemDefinition.SpriteKey` to **`ArtKey`**: a short stable id (`ore`, `herb`, `ration`, `glowstone`,
  `hazard-gear`), defaulting to the item's name in kebab case.
  - The core only carries an id, never pixels or sizes. That is already true of `SpriteKey` and is multiplayer-safe.
  - The save format is unchanged: definitions aren't saved.
- Presentation derives three pictures from it:
  - **`item/<key>/icon`**: menus, 16×16 art drawn at an integer scale (2× = 32 px in the dashboard).
  - **`item/<key>/map`**: the item itself in the world (on a stall's display, carried, dropped later), smaller than a
    cell and standing on its feet in isometric.
  - **`node/<key>`**: the node that yields it, when it is gathered (ore vein, herb patch). This is today's
    `Resource_*`.
- Not every item has a node. Gear and rations don't; the missing-art test knows that from the catalog (an item that
  no map node yields needs no node art).

### 2.4 Generating the art: a small sprite kit, a silhouette per shape, colour per item

There are three ways to make 16×16 item art. Use the first two now, and leave room for the third.

1. **Hand-drawn ASCII silhouettes**, as today, **one per shape, not per item**:
   - shapes: chunk (ore, glowstone), sprig (herb), pack (ration), vest/mask (protective gear), blade (weapon), vial
     (consumable);
   - six to eight shapes cover every item in sight;
   - each is 16×16, with one letter per tone: `o` outline, `1`–`4` light to dark, `h` highlight, `.` clear.
2. **A `SpriteKit` of deterministic operations** that turns a silhouette plus an item's colours into finished
   sprites:
   - a 4-tone **palette ramp** from one base colour (hue-shifted, darker tones cooler: the standard pixel-art ramp);
   - an **automatic outline** from the shape;
   - an **icon frame**: a backplate and a 1-px rim in the item's **rarity colour** (common grey, uncommon green, rare
     blue, epic purple, legendary amber). This is the conventional reading, and it puts rarity, which the catalog
     already has, on screen for free;
   - **map treatment**: scaled into the 10–12 px a dropped item takes, with a soft floor shadow (isometric) or a
     drop shadow (top-down);
   - **node treatment**: the item's colours on a node silhouette (vein in rock, patch of leaves).

   Colours are per item, in a presentation table keyed by art key (`ItemArt`, beside `ItemTexts`), with a fallback
   from the item's category. Adding an item gives it art with **one table line** (shape plus base colour), or none
   (category fallback plus the test's warning). That matches "Adding an item" in `AGENTS.md`.
3. **Imported pixels later (M7):**
   - `Texture2D.FromStream` (review R7) for PNG or Aseprite files under `Content/Art/<key>.png`, loaded into the same
     catalogue;
   - an imported file **overrides** the generated sprite for its key;
   - generated art stays the fallback, and the review sheet shows which is which.

**Why not generate everything procedurally with noise?** Noise sprites read as noise at 16×16. A silhouette drawn
by hand reads at that size; colour and finish are what scale. This hybrid is how small teams get consistent placeholder
art.

**What it is not:** an AI image pipeline. Nothing here needs network access, a model or non-deterministic pixels.
The render tests stay byte-exact.

### 2.5 Items that are physically in the world, now, with no core change

The producer's point is that items are physical objects. Three ways to show them need no new simulation entity:
- **The stall becomes furniture.** It is drawn at its POI from the map (static, like tiles: `prop/stall`). Its
  **goods are on display**: the `item/<key>/map` sprite of each good in stock, from a stall row added to
  `WorldViewFrame` (item, stock > 0). It is the most visible building in the outpost, and it is invisible today.
- **Worn gear on the crawler.** The crawler is drawn with a layer for the equipped protection's `item/<key>/worn`
  sprite (a mask). The frame row gains the equipped item's type. That makes Epic 10 G3's gear visible on the map
  without opening a dossier.
- **What is carried.** A crawler walking to the stall with salvage shows a small `item/<key>/map` over its pack. The
  frame row gains the first carried salvage item.

**Dropped items as world entities** (`EntityType.Item` holding an `ItemInstance`) are what loot tables will need. That
is simulation work: saves, perception and pickup intents. It belongs to the epic that wires loot into play. This plan
only makes sure the visual mapping has a slot for them (`item/<key>/map`, `DrawRole.Prop`).

### 2.6 Icons in the menus: views over keys

- Dashboard models gain an **`IconKey` per row** (merchant goods, the dossier's gear and pack, the bounty board's
  gather items). The model decides which item; it never decides pixels.
- The client resolves keys through an **`IconAtlas`**: one texture, point-sampled.
- A helper, `UiParts.Icon(key, scale)`, makes a Gum `SpriteRuntime` with `Texture` and `SourceRectangle` set from
  the atlas.
- Icons draw at **whole-number scales only** (1×, 2×), so pixel art keeps square pixels.
- Gum's texture filter is global (`Renderer.TextureFilter`). Today's fonts are drawn 1:1, so point sampling them is
  identical. V4 opens with a spike that confirms it: render a font-heavy window both ways, byte-compare.

### 2.7 Pipeline improvements, by value

| # | Improvement | Why | When |
| :--- | :--- | :--- | :--- |
| P1 | **One world atlas for both view modes.** Top-down draws from the world sheet, so `DrawSheet.Sprites` goes. | One texture, one index space, one fewer batch switch. Same pixels and sampling, so renders should stay byte-identical. | V1 |
| P2 | **The atlas builder gives out indices and packs by size** (shelf packing, sorted by height), within a texture budget (2048² is the safe floor for M4 phones) checked by a test. | Today's square grid sizes every cell to the largest sprite, which wastes space once 16×16 icons sit beside 32×40 walls and M7's 64×64 art. | V1 |
| P3 | **An art review sheet.** A test writes every catalogued sprite, named, on one PNG (`container-output/art-sheet.png`), and pins each sprite's pixels by key. | The producer reviews art in one picture. A changed sprite fails by name, not as "render t300 differs". | V0 |
| P4 | **Measure Gum before adding icons.** Time the dossier panel's draw; look at Gum's batch count and text re-layout per refresh. | UiDraw is 70% of the most expensive frame, and icons add sprites to it. | V4 opens with it |
| P5 | **Static tiles cached** (a chunked vertex buffer or render target per map, review R1's second half). | ~1,500 of ~1,600 items a frame are static tiles. But the world is 1 ms on a software renderer: **don't do this until a phone measures it** (M4, as the epic already says). | Deferred (M4) |
| P6 | **Premultiplied alpha** (review R4). | Linear sampling with non-premultiplied colours darkens edges (the fog's cloud). Pixel art is point-sampled, so it doesn't matter there. Changes renders. | With M7's imported art |
| P7 | **Snap sprite anchors to whole screen pixels.** The view already snaps to pixels, but crawlers between ticks land on fractions. | With point sampling, a sprite on a fraction can shimmer by a pixel as it moves. Check first: if the picture pins show no shimmer at 1×/2×, drop it. | Check in V2 |

Things to keep as they are, deliberately:
- the 64-bit sort key;
- culling before sorting;
- frames as the views' only input;
- `XnaPlatform` as the only place for platform-divergent APIs;
- byte-exact renders in the pinned image;
- re-recording renders only in the container, in their own commit.

---

## 3. Phases

Rules for every phase:
- The build has no warnings.
- `dotnet test` passes; golden 3/3; the soak is unchanged (none of this touches the simulation until V5's frame rows,
  which are presentation-side).
- The client's renders are checked in the pinned image.
- **A phase marked "no picture change" must reproduce every render byte for byte.** A phase that changes pictures
  re-records them in their own commit, after the producer has looked at the review sheet.

| Phase | What | Picture change | Gate |
| :--- | :--- | :--- | :--- |
| **V0** Review sheet | The art review sheet and per-sprite pins (P3), over today's sprites by today's names. | No | Sheet written; pins pass; renders byte-identical |
| **V1** Catalogue and atlas | `ArtKey` → `ArtId` → `ArtDefinition`. The atlas builder gives out indices and packs (P2). One world sheet (P1). `ProceduralSprites` and `IsometricSprites` register by key; their static fields remain as thin aliases until V2. The missing-art placeholder. | No | Renders and pins byte-identical; texture budget test; `FrameAllocationTests` |
| **V2** Visuals as data | `Visual(entity)` replaces `AddProp`'s switch and `WorldArt`'s per-kind members. A view mode is a projection plus a catalogue. `ArtMotion` covers the hearth's pulse and the crawler's squash and tilt. Check P7. | No | Renders byte-identical; `ArtSetTests` rewritten over the catalogue; a test that every `EntityType` the frames carry has a visual |
| **V3** Sprite kit and item art | `SpriteKit` (ramp, outline, frame, map and node treatments); shape silhouettes; `ItemArt` table. `ItemDefinition.SpriteKey` → `ArtKey`. An icon and a map sprite for every catalog item; nodes regenerated through the kit. The missing-art test over the catalog. | **Yes**: nodes may change look (kept as close as the kit allows) | Producer looks at the review sheet; renders re-recorded in their own commit |
| **V4** Icons in the menus | Measure Gum first (P4) and spike the texture filter. `IconKey` on the merchant, dossier and bounty board models; `IconAtlas`; `UiParts.Icon`. | **Yes**: the dashboard renders | Model tests; the UI frame time no worse than +10% on the dossier probe; renders re-recorded; producer look |
| **V5** Items in the world | The stall as furniture with its goods on display. Worn gear on crawlers. Carried salvage. The frame gains the stall rows, the worn item and the carried item. | **Yes**: the map renders | `ViewsWriteNothingTests`; zero-allocation frame; renders re-recorded; producer look |
| **V6** Close-out | `AGENTS.md`: "Adding an item" gains its art line, and a short "Adding art" section. The epic doc records what moved from M7. `architecture_history`. Delete this plan. | No | Docs reconciled |

The size is about 5–8 agent days. V0–V2 are behaviour-preserving and can land together. V3–V5 change pictures and
each needs a look.

---

## 4. Decisions for the producer (defaults in bold, standing unless contested)

- **D1 Scope and timing:** **V0–V5 now, as Epic CLIENT work pulled forward from M7, before G4.** The roadmap entry
  is written first, when you say go. The alternative: V0–V2 now (the foundation, no picture change), and V3–V5
  after Epic 10 Part 1.
- **D2 Art resolution:** **16×16 item art now** (icons at 2× in menus). The catalogue stores sizes, so 32×32 or M7's
  64×64 is a data change later.
- **D3 Rarity on icons:** **a rarity-coloured rim** (grey, green, blue, purple, amber).
- **D4 Items in the world:** **the stall's display, worn gear and carried salvage now.** Dropped items as entities
  wait for loot in play.
- **D5 Top-down:** **kept as the developers' view, on the same catalogue** (no separate effort for its look).

## 5. Deferred (owners)

- Dropped items as world entities, and loot wired into play: the epic that brings loot into play.
- PNG and Aseprite import, 64×64 isometric art, premultiplied alpha (R4), `Texture2D.FromStream` (R7): Epic CLIENT M7.
- Static tiles in vertex buffers with chunk culling (R1, second half), and UI scaling by density (U2): Epic CLIENT M4.
- Animation beyond two-frame loops (clips, events): when art needs it (M7).

# Epic CLIENT, text cleanup (before M3): implementation plan

Working document (AGENTS.md, documentation protocol): deleted when the cleanup closes, after its findings move to `docs/epics/epic-client.md` and its decisions to `.agents/architecture_history.md`.

- **Goal:** text that is crisp and evenly spaced everywhere in the client:
  - the dashboard and its windows;
  - the map's room names, "UNCHARTED", and the selected crawler's name;
  - every zoom level and both maps.
- **Why now:** the producer found the text rough (blurry, letters not lining up) and wants it cleaned up before M3. Isometric (M3), cutscene text boxes and the map designer will all draw text, so they should start on a sound pipeline.
- **Branch:** `feat/client-text` from `main-kni`, which is the acting main until M4 proves Android (producer, 2026-10-18). The pull request goes into `main-kni`.
- **Baseline:** `main-kni` at `681d3b5` (M2 parity). There:
  - `dotnet test` passes 147 of 147;
  - the golden check passes 3 of 3;
  - the MonoGame, Debug desktop and Android builds compile.

## What is wrong (measured on the committed render references, magnified 4× without smoothing)
1. **Dashboard text is blurry and unevenly spaced.**
   - The UI draws Gum's one built-in font, an 18 px Arial bitmap, scaled to 0.64–0.85 (`UiStyle.TextScale`, `UiParts.*Scale`).
   - With any scale other than 1, Gum stops snapping glyphs to pixels: `BitmapFont.DrawTextLines` switches to "free-floating" positions, so each glyph lands on a fractional pixel.
   - The gaps between letters therefore drift: "S warmAgent", "Ro sa", "compl eted", "FACILI TY". The shrunken bitmap greys the edges, and thin strokes fade ("GatherSalvage").
2. **Map text breaks off the whole-pixel scale.**
   - The code-generated 5×7 `PixelFont` is drawn in world units. It is crisp only at the whole-pixel framing, where one font pixel is two screen pixels.
   - Zoomed in, a font pixel becomes 2 or 3 screen pixels, so strokes are uneven.
   - On the Dev Map, M2.7's fitted sizes fall below one screen pixel per font pixel, and the letters break apart ("Fea^ Y1e:d", "Cefer").
   - The font has no descenders: "p" reads as "F" ("Outfost Hub").
3. **Gum warns about fonts it can't find.**
   - Setting `Font`, `FontSize` and `IsBold` one by one makes Gum look up each in-between combination ("No usable font could be resolved for FontCache/Font18Inter.fnt").
   - Today it falls back to Arial silently.

## Research (2026-10-18; a scratch spike outside the repo, renders compared magnified)
- **Inter drawn 1:1 fixes the UI's defects.** Inter rasterized at its exact pixel sizes by KernSmith (Gum's own font generator: MIT, FreeType hinting, grayscale anti-aliasing) and drawn 1:1 has even spacing, crisp strokes, real descenders, and stays legible at 11 px.
  - The managed StbTrueType rasterizer (no hinting, no natives) is close behind, a little softer.
- **Fonts can be baked ahead of time.**
  - Fonts written to disk in BMFont format, under Gum's cache names (`FontCache/Font{size}{family}[_o{outline}][_noSmooth][_Italic][_Bold].fnt` + PNG pages), are found by Gum with no generator at run time.
  - They render pixel-identical to generating in memory.
  - Gum resolves `Content/` from the executable's folder, not the working directory.
  - An ASCII atlas is 15–24 KB at 11–16 px.
  - `BmFontResult.ToFile` adds `.fnt` itself, so it takes the path without it.
- **Considered and not chosen:**
  - **Generating in memory at run time** (`KernSmithFontCreator`): FreeType natives on every platform (FreeTypeSharp has no linux-arm64), work at start-up, and output only as stable as each platform's native library. Baked files are plain data and identical everywhere.
  - **FontStashSharp** (`FontStashSharp.KNI`): good dynamic text, but Gum can't draw with it, so we would keep two text systems.
  - **SDF/MSDF text:** smooth at any zoom, but it needs a shader through KNI's effect pipeline, and Gum has no SDF text. Not needed if map labels move to screen space (below).
  - **Keeping the code-made pixel font:** its design (no descenders, one size) is the problem, and drawing a pixel font crisply needs whole screen-pixel scales anyway.
- **Licensing:** Inter is SIL OFL 1.1, which allows embedding and bitmap derivatives. Its licence goes with the source TTFs in the repo and into the credits.

## Design
- **T-D1: one text pipeline.** TrueType sources are baked offline into BMFont atlases at the exact pixel sizes used, and drawn 1:1 at whole pixels. No `FontScale` anywhere: a new size is a new baked atlas.
- **T-D2: Inter as the default typeface** for the UI and the map: Regular, SemiBold and Bold. It is designed for screens at these sizes.
  - The font set is data, so a designer's typeface later is a re-bake.
  - The same pipeline can bake a pixel font for the map instead, without anti-aliasing and at whole multiples of its grid, if the producer prefers that look. Candidates: Monogram (CC0) and Pixelify Sans (OFL).
- **T-D3: the baker.** `dotnet/Simpiens.FontBaker` is a developer tool in the solution that never ships. It uses KernSmith with FreeType, pinned, through Gum's own `BmfcSave` and `GumFontGenerator`, so the names and metrics are exactly Gum's.
  - **The font set:** `FontSet` in `Simpiens.Presentation` lists family, sizes, weights and outline. The baker, the UI and the map text share it.
  - **Output:** atlases go to `dotnet/Simpiens.Client/Content/FontCache/` and are committed. They are copied next to the game, and into Android assets.
  - **A font golden:** a test re-bakes into a temporary folder and compares bytes with the committed atlases. A deliberate change re-bakes in its own commit, as with the golden hashes.
- **T-D4: the UI on baked fonts.**
  - Gum's three text styles (Normal, Strong, Emphasis) get `Font`, `FontSize` and `IsBold`.
  - `UiParts.Text`, `Button` and `Slider` take a text style instead of a scale.
  - Sizes follow Unity's stylesheet (titles 15–16 bold, body 12–13, small 11, badges 10), chosen by eye because Gum's `FontSize` is the tallest glyph's height.
  - Font properties are set together, in an order whose in-between combinations exist.
  - Gum's default font becomes Inter, so no control falls back to Arial.
- **T-D5: map text moves to screen space.**
  - Room names, "UNCHARTED" and the selected crawler's name stay anchored to world points. The world renderer draws them in a screen-space pass, from the same atlases, snapped to whole pixels.
  - A label's size is chosen from the baked set by zoom, so it grows and shrinks with the map as Unity's world-sized labels did, in steps. Below the smallest size it is hidden.
  - Labels on a row never overlap: this screen-space rule replaces M2.7's world-space `FitLabels`.
  - A 1 px dark shadow keeps labels readable on any floor colour.
  - `PixelFont` and `WorldText` are removed.
  - The layout is engine-free in `Simpiens.Presentation`: a BMFont `.fnt` reader and the label placement. It is tested without a GPU.

## Chunks (one commit each, only when the gate commands pass)
1. **T0 The plan** (this file) and the roadmap's pointer.
2. **T1 Fonts and the baker.**
   - Inter's static TTFs and OFL licence go under `assets/fonts/inter/`.
   - `FontSet` in Presentation.
   - The `Simpiens.FontBaker` project, in `Simpiens.slnx`.
   - The baked atlases, committed.
   - `Content/` copied to the game's output, and included as Android assets.
   - Tests:
     - the font golden (re-bake equals the committed bytes);
     - every `FontSet` entry has its atlas;
     - the `.fnt` files parse.
   - `AGENTS.md`: how to re-bake.
3. **T2 The UI on baked fonts.**
   - `UiStyle` and `UiParts` by text style, no `FontScale`, and Gum's default font set to Inter.
   - Tests:
     - no Gum "No usable font" message from start-up through every panel and window (standard output captured);
     - every text in the dashboard's tree draws at scale 1 in a baked font.
   - The UI render references are re-recorded, each looked at whole and magnified.
4. **T3 The map's text in screen space.**
   - The BMFont reader, label placement and the renderer's screen-space pass. `PixelFont`, `WorldText` and `FitLabels` are removed.
   - Tests:
     - glyphs land on whole pixels;
     - advances and kerning come from the metrics;
     - the size steps with zoom;
     - no two labels overlap on Floor 1 or the Dev Map, at the full view, zoomed, and at the closest zoom;
     - the selected name sits above its crawler.
   - The map's references are re-recorded, including a zoomed one and the Dev Map, each looked at magnified.
5. **T4 Close-out.**
   - Frame time measured again.
   - The Android build with its font assets.
   - Docs, roadmap first.
   - The producer's look.
   - This plan deleted.

## Gate commands (before every commit)
```
dotnet build dotnet/Simpiens.slnx -c Release                               # 0 errors
dotnet test  dotnet/Simpiens.slnx -c Release                               # all pass (147 at the start, plus new tests)
dotnet run --project dotnet/Simpiens.Host -c Release -- golden check       # 3/3 seeds
dotnet build dotnet/Simpiens.Desktop -c Release -p:XnaFramework=MonoGame   # 0 errors
dotnet build dotnet/Simpiens.Desktop -c Debug                              # 0 errors
scripts/build-android.sh                                                   # 0 errors
git diff --stat main-kni -- Assets/ ProjectSettings/ Packages/             # empty: the core and Unity are not touched
```
Render references and baked fonts change only on purpose, in the chunk that changes them. Each is looked at before it is committed.

## Risks
| Risk | Plan |
| :--- | :--- |
| FreeType bakes differently on another OS (a Windows developer) | The font golden runs where the atlases were baked (Linux, and CI). A different bake is re-committed deliberately, or the baker switches to StbTrueType, whose managed output is identical everywhere |
| Gum looks up in-between font combinations | The UI sets font properties together, in a safe order. A test fails on any "No usable font" message |
| Android finds no fonts | Atlases are packed as Android assets; Gum falls back to `TitleContainer`, which reads assets on Android. The compile-only build checks the packaging; running it is M4 |
| Text sizes don't match Unity's look | Sizes are data (`FontSet`); the producer's look at T4 decides, and a change is a re-bake |
| High-DPI and phone screens need larger text | M6 bakes 1.5× and 2× sets from the same `FontSet` |
| Kerning in the map's own text layout differs from Gum's | Both read the same `.fnt` kerning pairs. The map's layout has unit tests against the metrics |

## Not in this cleanup
- the UI's reskin (the designer is still to be decided);
- languages beyond Latin (atlases are ASCII plus Latin-1 for names);
- cutscene text boxes (later, on this pipeline);
- SDF text;
- M2.8 and M2.9, Unity's removal, which are independent of this.

## Decided by the producer (2026-10-18)
"Go ahead, Inter and smooth labels are fine": Inter is the typeface, and the map's text is smooth labels in screen space (T-D2, T-D5).

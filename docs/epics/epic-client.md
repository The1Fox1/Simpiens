# Epic CLIENT: KNI Client and Engine Rework

- **Status**: ACTIVE (2026-10-18) on branch `feat/kni-client`; M0 and M1 complete, M2 waits for the producer. Status is owned by [ROADMAP.md](../ROADMAP.md). Decisions and deviations: `.agents/architecture_history.md`.
- **Track**: Independent (cross-cutting architecture)
- **Source**: the engine proposal, [`docs/engine-proposal-kni.md`](../engine-proposal-kni.md). That is a repo copy of the Claude Docs proposal, including the producer's answers of 2026-10-18. The engine spike that led to it compared Godot, BRUTAL, MonoGame, FNA and staying on Unity.
- **Builds on**: [Epic CORE](epic-core.md). The simulation is engine-free (`Assets/Simpiens.Core`), builds for .NET 10 (`dotnet/`) and runs headless through `Simpiens.Hosting`.
- **Replaces**: Unity's views and dashboard (`Assets/Scripts/`). They are frozen from M0, and Unity is retired at M2.

---

## 0. Producer intent and decisions

The producer chose KNI on 2026-10-18 for its responsive maintainer and its modern .NET 10 support on mobile. They asked the agent to see M0 and M1 through under provisional authority, with an implementation plan before each, committing regularly and only in a working state.

**Decisions (2026-10-18):**

- **Framework:** KNI (`nkast.Xna.Framework.*`, 4.3.9001). MonoGame stays compiling in CI as its fallback. Godot is dropped.
- **Views:**
  - Isometric will most likely replace top-down as the main view.
  - Height is visual only.
  - Top-down is for design only: the developers' map designer, and debugging.
  - Cutscenes are still backdrops with dynamic characters and text boxes, in the style of Fire Emblem.
- **Platforms:**
  - The MVP is Windows and Linux desktop.
  - Android and iOS follow the MVP: phones in portrait only, tablets not a priority. An Apple developer account and a Mac exist.
  - Consoles come last, only if mobile goes well.
  - No macOS, no web.
- **M2 (2026-10-18):** two pull requests, the first at parity and the second removing Unity. The M2 plan's defaults are approved (Gum's built-in visuals in Unity's palette; Unity's layout first; the Definition of Done in `dotnet/` once Unity is gone).
- **Art and performance:**
  - Art starts basic and grows to at most 64×64 px sprites.
  - The UI style and the art designer are still to be decided.
  - Target 30 fps.
- **Unity** retires at M2.
- **Map designer:** for developers only.
- **Working rules:** the same as Epic CORE.
  - A feature branch, never merged or pushed by the agent.
  - One commit per chunk, with its measured test status.
  - Roadmap first.

---

## 1. Goal

A KNI game client draws the simulation for players, replacing Unity, and the simulation doesn't change.

- **Read-only views:** the client reads the core's state through `Simpiens.Hosting` and never writes it.
- **Typed commands:** player input enters the core only as typed commands (ROADMAP §1A.1).
- **Testable without a person:** every frame the client draws can be checked by an agent, through render tests against committed images and the golden world hashes from inside the client.

**Milestones** (estimates in agent days; proposal §Roadmap):

| Milestone | Scope | Gate |
| :--- | :--- | :--- |
| M0 First pixels (1–2) | A desktop window; the host ticks the outpost; Floor 1 and its crawlers drawn top-down | Golden hashes 3 of 3 from inside the client; a render test in Linux CI; the MonoGame build compiles |
| M1 Top-down parity (3–5) | Fog, props, labels, animation, camera, mouse picking; a compile-only Android build in CI | Render tests pass; screenshots reviewed beside Unity's |
| M2 Dashboard (5–8) | Every panel in Gum (desktop layout); quick save and load; session switch; then Unity retired in its own chunk | The 7 view and UI scenarios pass |
| M3 Isometric (3–5) | Projection, depth sorting, generated isometric placeholders, picking, cutscene mode | Depth-order tests; reviewed screenshots |
| M4 Android, M5 iOS (3–5 each, after the MVP) | Touch, lifecycle, density, safe areas, the portrait phone layout; signing | Runs on devices (a person) |
| M6 Screen sizes (1–3) | Desktop windows of any size, portrait phones | Render tests at five sizes |
| M7 Art pipeline (2–3) | Sprite catalogue as data, 64×64 atlases, PNG and Aseprite import | New art is a data-only change |

The developers' map designer follows M2 as its own milestone.

**The agent's current authority covers M0 and M1.** M2 and later wait for the producer.

---

## 2. Architecture

The full design is in the proposal. In outline:

```mermaid
flowchart TB
    Desktop["Simpiens.Desktop (exe)<br/>SDL2 + OpenGL"] --> Client
    Client["Simpiens.Client<br/>KNI Game, renderer, input"] --> Presentation
    Client --> Hosting
    Presentation["Simpiens.Presentation<br/>engine-free: tiles, sprites, draw list, projections"] --> Core
    Hosting["Simpiens.Hosting (existing)"] --> Core["Simpiens.Core (existing)"]
    Tests["Simpiens.Client.Tests<br/>xUnit v3: presentation, render, golden"] --> Client
    Android["Simpiens.Android (apk)<br/>OpenGL ES, compile-only until M4"] --> Client
```

**Rules:**

- **`Simpiens.Presentation` has no XNA reference.** It turns the core's state into data:
  - tile layers;
  - generated sprite pixels;
  - draw items with sort keys;
  - projections.

  It is tested without a GPU. (A refinement of the proposal, which had a single `Simpiens.Client`.)
- **`Simpiens.Client` holds the only code that touches XNA types.** KNI-specific code sits in one class. The MSBuild property `XnaFramework` picks the framework: `Kni` (the default) or `MonoGame`. CI compiles both.
- **Views read state; they never write it.** Input goes down as typed commands.
- **Draw order:** layers from today's `RenderOrder` table, then depth, then insertion order, packed into one sort key and stably sorted by us, not by `SpriteBatch`.

---

## 3. Acceptance checklist (delivered work only)

- [x] C0: roadmap entry, this epic doc, the proposal copy (`docs/engine-proposal-kni.md`), the M0 plan (2026-10-18).
- [x] M0.1: the client's projects (`Simpiens.Presentation`, `Simpiens.Client`, `Simpiens.Desktop`, `Simpiens.Client.Tests`), the `XnaFramework` switch (KNI, or MonoGame compile-only), and the client booting offscreen in tests (2026-10-18).
- [x] M0.2: the engine-free presentation: the generated art ported pixel for pixel from Unity's `AgentSpriteLibrary` (`ProceduralSprites`, 33 sprites, pinned), the map as tiles (`MapTileLayer`, a port of `MapView`'s cell logic; Floor 1 is 1,175 floor, 384 rock and 254 wall tiles), the draw list with its stable 64-bit sort key, the top-down camera, the frame's scene (`WorldScene`) and PNG files (2026-10-18).
- [x] M0.3: the client ticks the simulation (`SimulationDriver`: real time, or N ticks a frame). **Gate met:** the game, run frame by frame offscreen, reproduces the golden world hashes for all 3 baseline seeds, and uneven frame pacing (hitches, zero-length frames) gives the same world (2026-10-18).
- [x] M0.4: first pixels. The client draws Floor 1 top-down (sector-tinted floor, walls, rock) and its crawlers in their current states, from one sprite atlas, through a camera snapped to whole pixels. Render tests compare the outpost at t0 and t300 (800×600, Mesa llvmpipe) with reference images in `dotnet/Simpiens.Client.Tests/Baselines/`. The desktop exe runs the live outpost (2026-10-18).
- [x] M0.5: CI runs the client's tests (Xvfb, Mesa llvmpipe, the system SDL2) and the MonoGame compile; `AGENTS.md` describes the client's projects and checks (2026-10-18). Unverified in CI until the branch is pushed.
- [x] **M0 complete (2026-10-18):** golden hashes 3 of 3 from inside the client; render tests pass on llvmpipe; the MonoGame build compiles.
- [x] M1.1: props and crawler appearance, ported from Unity's `AgentView`, `CampfireView` and `SwarmSpawner`.
    - Resource nodes by kind, the hearth (two frames at 6 Hz, a ±3% pulse) and bunks, at Unity's sizes on the Props layer.
    - Crawler state overrides, the 3-tick display dwell and every per-state micro-animation (tilt and squash).
    - A view clock: real time in the game, the tick's time in tests, so renders stay deterministic.
    - Render references re-recorded after review (2026-10-18).
- [x] M1.2: text and room names. `PixelFont` is a code-generated 5×7 font (ASCII 32–126) in its own sheet. `WorldText` lays it out in world units, snapped to its pixel grid. Floor 1's eight room names are drawn in their label colours on the Labels layer, as Unity's `MapView` placed them. The renderer starts a batch per sheet (2026-10-18).
- [x] M1.3: fog of war, ported from Unity's `ShroudView`.
    - Each uncharted sector's interior is covered by the value-noise cloud (alpha 0.97, linear sampling, its own sheet), over the props and under the crawlers.
    - "UNCHARTED" sits under the room's name. The line is 1.125 units down instead of Unity's 0.9, because the pixel font is taller.
    - A scout's report fades the sector's fog over 1.5 s of view time, then removes it. A respawn brings the fog back (2026-10-18).
- [x] M1.4: the camera, ported from Unity's `CameraRig` and `CameraController`.
    - Margin 2, closest zoom 3, zoom about the cursor (0.85 a notch), clamping, frame-rate-independent follow, reframe.
    - Right or middle drag to pan, WASD or arrows (0.6 view heights a second), Home, Esc; a left click is one that moves no more than 6 px.
    - Plain input snapshots, so the controls are tested without devices. The window is resizable.
    - Addition: given the window's pixel height, reframing (and the zoom-out limit) shows the whole map at whole pixels per art pixel. `CameraRigValidator`'s checks are ported (2026-10-18).
- [x] M1.5: picking and selection, ported from Unity's `WorldPicker` and `WorldSelection`.
    - A left click picks the nearest crawler within 0.6, ties going to the lower id. It selects and follows that crawler and its room, or selects just the room under an empty spot.
    - The selected crawler gets Unity's generated gold ring on the Selection layer, and its name from the identity service above it.
    - The camera follows the selection; panning or Esc stops it.
    - A respawn or a new map clears the selection (2026-10-18).
- [x] M1.6: a compile-only Android build (2026-10-18).
    - `dotnet/Simpiens.Android` runs KNI's game activity over the shared client, portrait only (`net10.0-android`, `nkast.Kni.Platform.Android.GL`). It is outside `Simpiens.slnx`, because it needs the Android workload.
    - Built locally with a user-local toolchain (`scripts/setup-android-toolchain.sh`: a .NET 10 SDK with the android workload, a portable JDK 21 and the Android SDK, under `~/.cache/simpiens-toolchains`, with no sudo) and `scripts/build-android.sh`: 0 errors, and a debug-signed APK and AAB produced.
    - CI has an `android` job that does the same on Ubuntu.
- [x] M1.7: the gate and close-out (2026-10-18).
    - The review set: the outpost at t0, t300 and t1500 (fog lifting as the scout charts rooms), a zoomed and panned view, and a selected crawler. Each is a render reference, and the agent looked at each against the ported Unity logic.
    - Frame time measured (§6 F2); the desktop exe logs through `SimLog` (the console sink moved into `Simpiens.Hosting`, shared with `simpiens`); a smoke test reads KNI's mouse and keyboard on a running game.
- [x] **M1 complete (2026-10-18):** render tests pass for fog, props, labels, animation, the camera and selection; the Android client compiles. Measured: `dotnet test` 111/111 (client 49, core 62); golden check 3/3; the MonoGame and Android builds compile.
    - The gate's second half, the producer's look at the client beside Unity, is person-gated and still open (§5).
- [x] M2.0: Gum in the client (2026-10-18).
    - Gum 2026.10.1.1 (Gum.KNI, or Gum.MonoGame for the fallback), with its built-in visuals in the palette of Unity's `FacilityDashboard.uss` and its font at 0.75 scale (`UiStyle`).
    - The UI layer is drawn over the map and updated before it, so the map ignores the pointer over a control and the camera ignores the keys while a control has them.
    - Gum keeps global state, so the game that started it last owns it (`UiLayer`).
    - Tests drive Gum's own cursor from a script, with the offscreen game marked active.
    - The client suite ran clean 20 times in a row, and the frame time is unchanged.

---

## 4. Deferred

- M2 to M7 and the map designer: they wait for the producer (the agent's authority covered M0 and M1).
- Interpolating crawlers between ticks: Unity didn't, so parity doesn't need it; in fixed-tick mode it would draw the previous tick. Unscheduled within this epic.
- Sound: not in the proposal's milestones; unscheduled.
- Touch controls: M4 (Android).
- The removal of Unity: M2, by producer decision.

---

## 5. Risks

- **Open, person-gated: the producer's side-by-side look** at the client beside Unity, M1's gate (2026-10-18). Run the client with `dotnet run --project dotnet/Simpiens.Desktop -c Release`. The agent compared every render with the ported Unity logic, but never with Unity's own screen.
- **The application id is a placeholder (M1.6):** the Android project uses `dev.simpiens.game` until the producer picks the store identity (M4).
- **CI is unverified (M0.5, M1.6):** neither the `test` job's client steps nor the `android` job has run yet. The workflow runs the client's tests under Xvfb with Ubuntu's SDL 2.30 and Mesa; locally they run SDL's offscreen driver on Fedora's sdl2-compat. The first push shows whether the reference images match there as well (the render tolerance allows 0.1% of pixels to differ). If they don't, the renders from CI are kept as a workflow artifact.


See the proposal's risk table. These matter for M0 and M1:

| Risk | Mitigation |
| :--- | :--- |
| Render tests need a GPU, and CI has none | SDL's offscreen video driver with Mesa's software OpenGL (llvmpipe), proven locally on 2026-10-18; CI may need a virtual display instead |
| KNI-specific APIs leak into shared code | The MonoGame build in CI fails on any KNI-only API outside the one platform class |
| The client changes the simulation | The golden world hashes are checked from inside the client, through its own tick path |

---

## 6. Findings

- **F1: headless rendering needs the system SDL2** (2026-10-18, scratch prototype).
  - KNI's bundled `libSDL2-2.0.so.0` can't create an OpenGL context under SDL's offscreen video driver.
  - The system's SDL2 (sdl2-compat 2.32 over SDL3 on Fedora) can, with Mesa: llvmpipe with `LIBGL_ALWAYS_SOFTWARE=1`, radeonsi without.
  - In offscreen mode, `Game.Dispose()` segfaults after `Run()`. The process exits cleanly if the game isn't disposed.
  - `Game.RunOneFrame()` works, and two games run in one process.
  - SDL and Mesa read their settings (`SDL_VIDEODRIVER`, `LIBGL_ALWAYS_SOFTWARE`) with `getenv`. On Unix, .NET's `Environment.SetEnvironmentVariable` doesn't reach it, so the test set-up calls libc's `setenv`. Without that, the first test run hung trying to open a real window (M0.1).
- **F2: frame time is far inside the budget** (2026-10-18, M1.7, `FrameTimeProbe`, an explicit test).
    - The probe ticks once a frame and builds and draws the whole outpost at 800×600: 1,925 draw items at tick 600.
    - Per frame: 1.18 ms on Mesa's llvmpipe and 0.67 ms on an AMD RX 7900 XTX. The 30 fps budget is 33 ms.
    - Rerun with `dotnet dotnet/Simpiens.Client.Tests/bin/Release/net10.0/Simpiens.Client.Tests.dll -method "*FrameTime*" -explicit only -showLiveOutput`; `LIBGL_ALWAYS_SOFTWARE=0` measures the GPU.
    - Phones aren't measured yet (M4).
- **F3: an offscreen game never has focus** (M1.7). XNA then naps `InactiveSleepTime` (20 ms) every frame, and doesn't read input (the game reads devices only while active). The tests set the nap to zero, which halved the client suite's time. The devices are exercised by a direct `ClientInput` test instead.
- **F4: KNI registers its platform factories by reflection** (M1.7). The desktop exe prints "GameFactory not found ... Registering ... through reflection" to standard output at start. Registering them explicitly in each platform's entry point would quiet it and avoid reflection, which matters for trimming and AOT on mobile (M4).

---

## History

- 2026-10-18: epic opened from the KNI engine proposal; C0 (setup) done.
- 2026-10-18: M0 (first pixels) done in five chunks: M0.1 `ba1f17c`, M0.2 `27c48be`, M0.3 `79fa223`, M0.4 `cf88e89`, M0.5 the close-out commit. M1's plan follows.
- 2026-10-18: M1 (top-down parity) done in seven chunks: the plan `f83a948`, M1.1 `0dbfaf9`, M1.2 `981e5aa`, M1.3 `fa789fc`, M1.4 `510d31a`, M1.5 `989f2cd`, M1.6 `adcde54`, M1.7 the close-out commit. The plan was deleted at close-out. The agent's authority ends here: M2 waits for the producer.

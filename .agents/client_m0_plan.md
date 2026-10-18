# Epic CLIENT, M0 (first pixels): implementation plan

Working document (AGENTS.md, documentation protocol): it is deleted when M0 closes, after its findings move to `docs/epics/epic-client.md` and its decisions to `.agents/architecture_history.md`.

- **Goal:** a desktop window in which our .NET host ticks the outpost, and Floor 1 and its crawlers are drawn top-down from the generated sprites.
- **Gate (proposal §Roadmap):**
  1. The golden world hashes, 3 of 3 seeds, come out right from inside the client, through the client's own tick path.
  2. A render test passes in Linux CI.
  3. The client compiles against MonoGame as well as KNI.
- **Branch and baseline:** `feat/kni-client`, cut from `main` at `adf76e5`. There, `dotnet test dotnet/Simpiens.slnx` passes 62 of 62, and `simpiens golden check` passes 3 of 3.
- **Out of M0, and owned by M1:** fog of war, props (nodes, bunks, hearths), labels, sprite animation, camera control, picking, logging set-up in the client, the Android build.

## Design

### Projects (`dotnet/`)
| Project | Kind | References | Holds in M0 |
| :--- | :--- | :--- | :--- |
| `Simpiens.Presentation` | library, `net10.0` | `Simpiens.Core` only. **No XNA.** | Generated sprite pixels (port of `AgentSpriteLibrary`), the map's tile layer (port of `MapView`'s cell logic), draw items and sort keys, the top-down projection, the frame's scene builder, a PNG writer and reader |
| `Simpiens.Client` | library, `net10.0` | Presentation, Hosting; XNA from the `XnaFramework` switch | `SimpiensGame : Game`, the simulation driver, the sprite atlas upload, the world renderer, frame capture, the one KNI-specific class |
| `Simpiens.Desktop` | exe, `net10.0` | Client; the platform package from the switch | `Program.Main`: options, `game.Run()` |
| `Simpiens.Client.Tests` | xUnit v3 exe | Client, `Simpiens.Testing.Core`; KNI's SDL2.GL platform with its bundled natives excluded | Presentation tests (no GPU), driver tests (no GPU), golden-in-client and render tests (offscreen GPU) |

### The framework switch
- The MSBuild property is `XnaFramework`: `Kni` (the default) or `MonoGame`. It sets `DefineConstants` `KNI` or `MONOGAME`, and picks the package references:
  - **KNI:** `nkast.Xna.Framework*` 4.3.9001, plus `nkast.Kni.Platform.SDL2.GL` in executables.
  - **MonoGame:** `MonoGame.Framework.DesktopGL` 3.8.6.
- A MonoGame build gets its own `obj/` and `bin/` folders (`BaseIntermediateOutputPath` in `Directory.Build.props`), so the two package sets never share an assets file.
- **The MonoGame check:** `dotnet build dotnet/Simpiens.Desktop -c Release -p:XnaFramework=MonoGame`. It is compile-only: nothing runs MonoGame.

### The client
- **Tick path:** `SimpiensGame.Update` calls `SimulationDriver.Advance(gameTime)`.
  - **Live mode:** `clock.Advance(elapsed seconds)`, as Unity's `SimulationClockDriver` does.
  - **Fixed mode (tests, fast-forward):** `N` ticks per update.
  - Either way, the host steps whole ticks, so the world never depends on frame pacing.
- **Draw:**
  - The scene builder fills a pooled draw list from the host's state: the map's tiles (cached per map) and crawlers (position, `AgentState.VisualState`, frame).
  - The list is sorted stably by key: layer (`RenderOrder`), then depth, then insertion.
  - The renderer draws it with `SpriteBatch` in `Deferred` mode, `PointClamp` sampling, `NonPremultiplied` blending and the camera's matrix.
- **Camera (M0):** fixed. It frames `map.CameraFrame` at a whole number of pixels per world unit, 16 by default.
- **Capture:** draw into a `RenderTarget2D`, then `GetData` into RGBA bytes, then the Presentation PNG writer (zlib from `System.IO.Compression`; no image library).

### Tests
- **Headless set-up:** a module initializer sets `SDL_VIDEODRIVER=offscreen` and `LIBGL_ALWAYS_SOFTWARE=1` unless they're already set. The test project excludes KNI's bundled SDL2, so the system's SDL2 loads (finding F1).
  - CI runs Ubuntu: installing `libsdl2-2.0-0` and Mesa is enough if SDL 2.30's offscreen driver gives a GL context.
  - If it doesn't, CI uses `xvfb-run` with `SDL_VIDEODRIVER=x11` instead. This is decided at the CI chunk.
- **One process, no dispose:** games run in-process with `Game.RunOneFrame()` and are never disposed, because disposing segfaults offscreen (finding F1). Parallelization is off.
- **Golden inside the client:** for each baseline seed, a `SimpiensGame` in fixed mode runs frames to t3000. A hook samples `WorldStateHash` on every 100th tick, and `GoldenWorldHash.Compare` must pass.
- **Driver test, no GPU:** irregular frame times (16, 33 and 250 ms, and a pause) end in the same samples as the baseline.
- **Render test:**
  - The outpost at t0 (seed 20261009) is drawn into a render target at 16 px per unit and compared with `dotnet/Simpiens.Client.Tests/Baselines/render-floor1-t0.png`.
  - Tolerance: at most 0.1% of pixels may differ by more than 2 in any channel.
  - On a mismatch, the actual image and a diff go to the test output folder. `SIMPIENS_RECORD_RENDERS=1` records the reference.
  - Every recorded image is looked at by the agent before it is committed.
- **Presentation tests, no GPU:**
  - sort-key order and stability;
  - the tile layer's invariants for Floor 1 (every wall rect's cells are wall or rock; floor only inside the playable bounds; floor tints come from sector labels);
  - sprite fidelity (dimensions, and a pinned checksum of every generated sprite);
  - the projection's round trip.

## Chunks (one commit each, only when the gate commands below pass)
1. **M0.1 Scaffolding and the framework switch.**
   - The four projects, the solution entries, the switch in `Directory.Build.props` and `.targets`.
   - A `SimpiensGame` that clears the screen, and the desktop exe.
   - Tests: the client boots offscreen for 3 frames, and the back buffer reads back the clear colour.
   - The MonoGame build compiles.
2. **M0.2 Presentation.**
   - The sprite port, the tile layer, draw items and sort keys, the top-down projection, the scene builder, PNG read and write.
   - Their tests.
3. **M0.3 The tick path and golden hashes inside the client.**
   - `SimulationDriver` and the client's simulation (respawning the outpost with a seed).
   - The driver test, and the golden test: 3 of 3 seeds from inside `SimpiensGame`.
4. **M0.4 First pixels.**
   - The atlas upload, the world renderer, the fixed camera, capture.
   - The render test with its committed reference image, reviewed by eye.
   - The desktop exe draws the live outpost.
5. **M0.5 CI and close-out.**
   - The workflow: client tests on Ubuntu with the headless set-up; the MonoGame compile step.
   - `AGENTS.md` (the `dotnet/` row, the check table).
   - ROADMAP progress and the M0 close, the epic checklist, `curentfocus`, `architecture_history`.
   - Delete this plan, then write the M1 plan.

## Gate commands (before every commit)
```
dotnet build dotnet/Simpiens.slnx -c Release                       # 0 errors
dotnet test  dotnet/Simpiens.slnx -c Release                       # all pass (62 before M0, plus the new tests)
dotnet run --project dotnet/Simpiens.Host -c Release -- golden check   # 3/3 seeds
dotnet build dotnet/Simpiens.Desktop -c Release -p:XnaFramework=MonoGame   # from M0.1: 0 errors
git diff --stat main -- Assets/ ProjectSettings/ Packages/        # empty: Unity untouched
```

## Risks
| Risk | Plan |
| :--- | :--- |
| An API differs between KNI and MonoGame | The MonoGame build fails; move the call behind the KNI-specific class, or use the shared XNA API |
| CI has no GL context offscreen | Switch the CI step to `xvfb-run` (decided in M0.5; the workflow can't run before a push, so the commit says it is unverified) |
| The software and GPU renderers differ by a pixel | Point sampling at integer positions avoids filtering differences. The tolerance covers the rest, and the tests pin llvmpipe. |
| The client changes behaviour | The golden test inside the client fails. The core isn't edited in M0. |

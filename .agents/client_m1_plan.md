# Epic CLIENT, M1 (top-down parity on desktop): implementation plan

Working document (AGENTS.md, documentation protocol): deleted when M1 closes, after its findings move to `docs/epics/epic-client.md` and its decisions to `.agents/architecture_history.md`.

- **Goal:** the KNI client shows everything Unity's 2D view shows, and plays the same way with mouse and keyboard:
  - fog of war;
  - props (resource nodes, the hearth, bunks);
  - map labels;
  - crawler animation and state cues;
  - camera control;
  - click selection with a ring and a name.

  Also a compile-only Android build, so desktop-only assumptions are caught early.
- **Gate (proposal §Roadmap):**
  1. Render tests pass for each of the above.
  2. Screenshots reviewed beside Unity's. The agent reviews every render against the ported Unity logic. The producer's side-by-side look is a separate, person-gated step, listed at close-out.
- **Baseline:** `feat/kni-client` at `1f119fe` (M0 complete). There, `dotnet test` passes 83 of 83, the golden check passes 3 of 3, and the MonoGame build compiles.
- **Source of truth for behaviour:** Unity's views, frozen at M0:
  - `ShroudView` (fog);
  - `SwarmSpawner.HandlePropSpawned`, `CampfireView` (props);
  - `MapView` labels;
  - `AgentView` (state overrides, display dwell, micro-animation);
  - `CameraRig` and `CameraController` (camera and input);
  - `WorldSelection` and `WorldPicker` (picking);
  - the validator to port, `CameraRigValidator`.
- **Not in M1:**
  - the dashboard and every UI panel (M2);
  - isometric (M3);
  - touch (M4);
  - position interpolation between ticks: Unity didn't interpolate crawlers, so parity doesn't need it, and in fixed-tick mode it would draw the previous tick;
  - sound;
  - the debug grid gizmo (editor-only in Unity).

## Design
- **Engine-free first.** Every ported behaviour goes into `Simpiens.Presentation` as plain C#, tested without a GPU:
  - `CameraRig`, `WorldPicker` and `WorldSelection`;
  - crawler appearance (overrides, dwell, micro-animation);
  - fog overlays and their fade;
  - the pixel font and text layout.

  `Simpiens.Client` only reads XNA input into a plain snapshot and draws.
- **A view clock.** Micro-animations, the campfire and fog fades use real seconds in Unity. The client keeps a view time:
  - **live:** unscaled elapsed time, so fades read the same at any simulation speed, as in Unity;
  - **fixed (tests):** derived from the tick (tick × 0.1 s), so a render at a given tick is always the same.
- **Draw items grow:**
  - rotation and scale, for micro-animation;
  - a texture id: the atlas, point-sampled, or the fog cloud, linearly sampled.

  The renderer breaks the batch when the texture or sampler changes. The order is still the sort key.
- **Text.** A code-generated pixel font (5×7 glyphs, ASCII 32–126) as sprites in the atlas, laid out in world units. It is used for map labels, "UNCHARTED" and the selected crawler's name. It needs no content pipeline and no font files. The real UI fonts come with Gum in M2.
- **Input.** `ClientInput` turns `Mouse.GetState()` and `Keyboard.GetState()` into a plain snapshot: pointer, buttons, wheel delta, keys pressed and held. `CameraInput` (Presentation) applies Unity's controls to the rig and the selection:
  - scroll zooms toward the cursor by 0.85 per notch;
  - right or middle drag pans;
  - WASD or the arrows pan at 0.6 view heights a second;
  - Home reframes;
  - Esc stops following;
  - a left click (moving no more than 6 px) picks.

  Tests feed snapshots, not devices.
- **The window is resizable.** The rig keeps its aspect, as `CameraRig.SetAspect` did.
- **Logging.** The desktop exe installs a console sink for `SimLog`, as `simpiens` does (Warning and up, unless `SIMPIENS_LOG` is set).

## Chunks (one commit each, only when the gate commands pass)
1. **M1.1 Props and crawler appearance.**
   - Resource nodes, by kind, at 0.8 scale on the Props layer.
   - The hearth: two frames at 6 Hz and a ±3% pulse, at 0.9 scale.
   - Bunks at 0.9 scale, under sleepers.
   - Crawler state overrides: tantrum or starvation → panicking; sleeping kept; then yielding, sharing, gossiping.
   - The 3-tick display dwell, and the micro-animations (tilt and scale per state), as `AgentView` did.
   - The view clock; rotation and scale in draw items.
   - Tests: appearance rules from scripted states. The render references are re-recorded on purpose and reviewed.
2. **M1.2 Text and map labels.**
   - `PixelFont`, and text layout (centred, in world units).
   - Floor 1's eight labels on the Labels layer, in their colours.
   - Tests: glyph coverage and widths; label placement; a render reference.
3. **M1.3 Fog of war.**
   - The cloud texture (`ShroudView`'s value noise, 32×32, linear sampling) over each uncharted sector's interior, inset by the wall's half-thickness, on the Fog layer at alpha 0.97.
   - "UNCHARTED" under the room's name.
   - A 1.5 s fade (view time) once a scout charts the sector.
   - Renderer batch breaks.
   - Tests: overlays at t0 for the uncharted sectors; charting starts a fade that removes the overlay; a render reference.
4. **M1.4 Camera.**
   - `CameraRig` ported: margin 2, minimum size 3, zoom at a point, clamp, follow with sharpness 6, reframe, aspect.
   - The camera drawn from the rig.
   - `CameraInput` with Unity's controls; a resizable window.
   - Tests: `CameraRigValidator`'s checks ported; input snapshots (scroll at the cursor, drags, keys, Home).
5. **M1.5 Picking and selection.**
   - `WorldPicker` (nearest within 0.6, ties to the lower id) and `WorldSelection` ported.
   - A left click selects and follows a crawler and its sector, or selects the room.
   - The gold ring (Unity's generated 16×16 ring) on the Selection layer.
   - The name above it, from `IAgentIdentityService`.
   - The camera follows.
   - Tests: picking and ties; click flows on the live outpost; following; a render reference with a selection.
6. **M1.6 Compile-only Android build.**
   - `dotnet/Simpiens.Android` (`net10.0-android`, `nkast.Kni.Platform.Android.GL` 4.3.9001, KNI's template activity), outside `Simpiens.slnx` so ordinary builds don't need the Android workload.
   - CI job: install the workload and build it.
   - Local check: a user-local toolchain under `~/.cache/simpiens-toolchains/` (a user-local .NET SDK with the android workload, a portable JDK 21, the Android SDK from `InstallAndroidDependencies`), with no sudo and nothing on the system path. If that can't be done, the project stays CI-only and every commit says the Android build is unverified locally.
7. **M1.7 Gate and close-out.**
   - The review set: renders of the outpost at t0, t300 and t1500 (fog, props, labels, animation), a zoomed and panned view, and a selection. Each looked at by the agent and written to `render-output/`.
   - Frame time of a full outpost draw, measured against the 33 ms budget.
   - Docs, roadmap first: ROADMAP, epic, `AGENTS.md`, `curentfocus`, `architecture_history`.
   - Delete this plan.
   - The producer's side-by-side with Unity stays open, as a person-gated check.

## Gate commands (before every commit)
```
dotnet build dotnet/Simpiens.slnx -c Release                       # 0 errors
dotnet test  dotnet/Simpiens.slnx -c Release                       # all pass (83 at M1 start, plus new tests)
dotnet run --project dotnet/Simpiens.Host -c Release -- golden check   # 3/3 seeds
dotnet build dotnet/Simpiens.Desktop -c Release -p:XnaFramework=MonoGame   # 0 errors
git diff --stat main -- Assets/ ProjectSettings/ Packages/        # empty: Unity untouched
```
From M1.6, the Android build also runs, if the local toolchain works.

Render references change only on purpose, in the chunk that changes the drawing. Each re-recorded image is looked at before it's committed.

## Risks
| Risk | Plan |
| :--- | :--- |
| Micro-animations or fades make renders nondeterministic | The view time comes from the tick in fixed mode; the tests use fixed mode only |
| The pixel font reads poorly next to Unity's smooth `TextMesh` | Accepted for M1: legible pixel text. Gum's fonts arrive in M2. |
| The fog's linear sampling differs between drivers | It is covered by the render tolerance. If needed, the fog's own pixels are compared with a looser tolerance. |
| The Android toolchain can't be installed without root | CI-only, stated in each commit, as above |
| An input or API call differs between KNI and MonoGame | `ClientInput` sits in `Simpiens.Client`; the MonoGame compile catches it |

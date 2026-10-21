# Plan: the client's seams before M3 (Epic CLIENT, review findings A1–A3)

Working document; delete it at close-out (AGENTS.md, plan protocol). The full plan, with its reasoning, tables and
diagrams, is the Claude Doc "Plan: Projection, Draw Order and Frame Snapshot (A1–A3)"
(https://claude.ai/artifact/PL1GQHLou3pLPBRY7EuhJ2), written from the "KNI Client Architecture Review"
(https://claude.ai/code/artifact/612e6a7a-0d7e-4485-80d0-7f7a78ee9f13). Branch `feat/client-seams` from `main-kni`; one PR
per phase into `main-kni`.

## Why

- **A1:** top-down is hard-wired (`TopDownCamera` is a concrete struct in six places). M3's isometric view would have to
  change every one of them.
- **A2:** `RenderLayer` draws walls, props, fog and crawlers as separate layers. Isometric needs walls, props and crawlers
  interleaved by depth, and then the fog (decision D1 = A: over props, under crawlers) has no slot.
- **A3:** the views read live simulation objects in `Draw`. There is no per-tick snapshot, so crawlers move in 10 Hz steps,
  the simulation can't leave the render thread, and a network client would rewrite every view.

## Constraints on every step

- The 10 render references pass (tolerance 2 per channel); a re-record goes in the same commit as its cause, and the
  commit body names each changed reference.
- Golden hashes 3/3 inside the client and in `golden check`; the view never writes the simulation.
- New types in `Simpiens.Presentation` (no XNA); the MonoGame build compiles.
- A steady frame allocates nothing.
- The one core change (3.1) is checked in both runtimes.
- One commit per step with the measured test status.

## Phase 1 · Projection seam (A1)

1. **1.1** Pin today's picture: a test hashes the draw list at tick 300 (sheet, sprite, layer, sequence, position, size)
   and the placed labels.
2. **1.2** `IViewProjection`, `ViewCamera`, `View`, `TopDownProjection`; `TopDownCamera` deleted; the renderer, label
   layout, input and `SimpiensGame` take `View`. Label rows group by screen y and sort by screen x.
3. **1.3** `DrawItem`: anchor, `Z`, pivot, size in art pixels, pick index; `ScreenQuad.Of` shared by renderer and
   picker; the scene asks the projection for depth.
4. **1.4** The rig holds a floor centre and a zoom; pan, zoom-at-cursor, follow, clamp and snap through the projection.
   `CameraTests` keep their numbers.
5. **1.5** Screen-space picking over the last drawn list, topmost first, with today's reach (0.6 world units).
6. **1.6** `IsometricProjection` maths, tested, not wired in.

Gate: references and 1.1's hash unchanged; golden 3/3; MonoGame compiles; no `TopDownCamera`.

## Phase 2 · Draw order (A2)

1. **2.1** Producer decision: fog option A (hide props in covered sectors; recommended), B (keep the fog layer) or C
   (cloud over everything). Also: option A in top-down too, or isometric only.
2. **2.2** `DrawRole`, `LayerOf(role)` on both projections; the sort key becomes layer 8 | depth 32 | role 4 |
   sequence 20; `RenderLayer` is top-down's table.
3. **2.3** The fog rule from 2.1 (blocked until then).
4. **2.4** Isometric depth-order tests on hand-built scenes.

Gate: 1.1's hash unchanged by 2.2; references changed only by 2.3; isometric order tests pass.

## Phase 3 · View frame (A3)

1. **3.1** `SessionHost.TickCompleted`, raised at the end of `TickCrawlers` (core; .NET tests, golden check, Unity suite).
2. **3.2** `WorldViewFrame`, `EntityView`, the double-buffered `WorldViewFrames` writer; an epoch on `SessionChanged`.
3. **3.3** The scene, appearance, fog and selection read only the frames; the selection's getter is pure.
4. **3.4** Interpolation: alpha from the clock live, 1 under `ViewClockFromTicks`; snap over 2 world units; follow on
   the drawn position.

Gate: references unchanged; golden 3/3; frame time at or below today's; the producer watches crawlers walk at 1× and 10×.

## Deferred (owners)

- Wiring isometric, its art, per-cell fog, cutscene mode: M3.
- Static floor pass and culling: review R1 (step 8).
- One world epoch for every cache, scene built in `Update`: review A5 and A6 (step 3).
- The simulation on its own thread and the dashboard on a snapshot: review D1.
- The pinned Mesa/SDL container for render tests: review T2.

# Plan: the client's open architecture findings (Epic CLIENT)

Working document. It is deleted at close-out, after its decisions move to `architecture_history.md` and its open items to the epic.

- **Request (producer, 2026-10-22):** M4 is deferred until later. The work now is the architecture improvements in the Claude Doc "KNI Client Architecture Review" (https://claude.ai/artifact/D12DmvAzrLuK1LhwxnhEwL), on a new branch.
- **Branch:** `feat/client-architecture`, cut from `main-kni` at `9ff5be3`. It is not pushed or merged by the agent.
- **Rules:**
  - Each step is one commit, made only when it works: `dotnet test` passes, the golden check passes, and the MonoGame build compiles. The commit body states the measured status.
  - The picture pins and render references stay unchanged unless a step says why they change. A step that changes them re-records them in that step, after a look.
  - The core (`Assets/Simpiens.Core`) is not expected to change. If it must, the Unity suite and golden check run before that commit.

## What is already done

Done in the client seams (A1–A3) and M3 Phase 0:

| Finding | Done in |
| --- | --- |
| A1 projection seam | Seams |
| A2 draw roles | Seams |
| A3 view frames | Seams |
| A4 game split into shell, session, world view and screens | M3 Phase 0 |
| A5 one world epoch | M3 Phase 0 |
| A6 drawing is pure | M3 Phase 0 |
| R1, first half: culling before sorting | M3 Phase 0 |
| R2 zero-allocation frame, with a test | M3 Phase 0 |
| R3 glyph pages by index | M3 Phase 0 |
| U5, third point: the selection's getter is pure | Seams |
| T1 CI on `main-kni` | `0022a66` (green on `9ff5be3`) |
| T4, first point: games are reused, not leaked | M3, epic F15 |

## Steps

### Phase 1: measure and guard (D1, T3, T4)

1. **1.1 Frame timings by phase (D1).**
   - The game times each frame's tick, world update, world draw, UI update and UI draw with `Stopwatch.GetTimestamp`, allocating nothing.
   - `FrameTimings` keeps a rolling window and reports the mean and 99th percentile of each phase.
   - The probe prints the phases. In Debug builds, F3 shows them in a corner.
2. **1.2 A frame-time gate (T3).** A test, not `[Explicit]`, runs the isometric outpost at 800×600 for 300 frames after warm-up. It fails if the mean frame exceeds 8 ms (it is 1.6 ms today on llvmpipe), or if the 99th percentile exceeds the 33 ms budget. The zero-allocation test already exists (M3 0.4).
3. **1.3 Views never write the simulation (T4).** With the clock paused, frames of update, draw and UI (panels open, a crawler selected, the camera moved) leave `WorldStateHash` unchanged.

### Phase 2: host commands (D3, D2)

1. **2.1 A host command queue (D3).**
   - Typed host commands: set speed, pause, resume, quick save, quick load and switch session.
   - The dashboard queues them instead of calling the services.
   - `ClientSession` applies them at the start of its update, before the ticks. A load or a switch no longer rebuilds the world inside a Gum click.
   - The session button gets one handler, not two.
2. **2.2 Speed and pause controls.** The header gets pause and speed controls that go through the queue, plus keys for them. The header's references are re-recorded once, deliberately.
3. **2.3 A tick cap per frame (D2).** The driver clamps a frame's real time to 0.4 s, at most 4 ticks per frame times the speed. The dropped time is logged once per stall. The core doesn't change.

### Phase 3: one input frame (U1)

1. **3.1 Input as one frame with actions.**
   - `ClientInput` reads one frame per update: the pointer and the keys.
   - A bindings table (data) turns keys into actions: pan, reframe, close, panels, wall style, view, pause, speed, advance, demo cutscene.
   - `CameraInput` and the screens read actions, not keys.
2. **3.2 Gum fed from the same frame.**
   - Gum's cursor takes the game's input frame, through Gum's public API where it allows.
   - The tests' scripted cursor stops reflecting into Gum.
   - The scripted input runs without KNI's private active flag being set by reflection.

### Phase 4: the dashboard's loose ends (U3, U4, U5, S3, S7, S8, S9)

1. **4.1 The palette into UiStyle (U3).** Every colour becomes a named token, with no visual change.
2. **4.2 Required services (U4, S3).**
   - `DashboardContext.From` requires what the game always has.
   - The client reads identity and the chronicle through typed accessors instead of a service locator.
   - The nullable constructor stays for unit tests.
3. **4.3 Player text and rules (U5, S7, S8, S9).**
   - Goals and actions get display names, not type names.
   - Occupancy counts each crawler once, by the map's sector lookup.
   - Affinities are sorted, and "Unknown" stands for absent peers.
   - The stall's name and sector come from the session.
   - The bounty type is set through `SetType`.

   The UI references re-record wherever this changes text.

### Phase 5: smaller items, then the pinned renderer (S1, S2, S4, S6, T2)

1. **5.1 Smells.**
   - S1: the appearance forgets crawlers that leave the frame.
   - S2: "UNCHARTED" finds its room by sector id, not float equality.
   - S6: delete the stray summary if it is still there.
2. **5.2 S4:** rename `HeadlessSimulation` to `LocalSimulation`. This touches `dotnet/` and the docs only.
3. **5.3 T2, the client tests in a pinned container.**
   - A Containerfile on Ubuntu, with apt pinned to a snapshot date (`snapshot.ubuntu.com`), so it needs no registry.
   - A script runs the client tests in it with podman.
   - CI runs the same image, and the references are recorded in it once.
   - If the snapshot route fails, this step stops, with a note for the producer: it then needs a registry for a pushed image.

## Deferred (with owner)

| Finding | Owner |
| --- | --- |
| U2 UI scaling by density | M4, with the producer's density decision |
| R1, second half: the static floor in vertex buffers | M4, measured on a phone first |
| R6 a frame cap per platform | M4, for battery |
| R4 premultiplied alpha | M7 |
| R5 sprite catalogue | M7 |
| R7 textures through `FromStream` | M7 |
| S10 sizes as data | M7 |
| S5 "Unity" in comments | Unity's removal (M2.9) |
| D1, second half: the simulation on its own thread | After this plan |
| T4, second point: the font baker referenced by the tests | Left as it is (Linux and Windows only) |

## Progress

- 2026-10-22: plan written; branch cut (`9b8c456`).
- Phase 1 done:
  - 1.1 `7d96570`;
  - 1.2 `2eb6b62`;
  - 1.3 `9ea1f1b`.

  First numbers: the tick is about 0.1 ms of a 1.4 ms isometric frame (CPU); drawing is most of it.
- Phase 2 done:
  - 2.1 `cebc9e3`;
  - 2.2 `ce56fc2`, with eight header references re-recorded;
  - 2.3 `9c3105f`.
- Phase 3 done:
  - 3.1 `2195f35`;
  - 3.2 `01ced93`.

  The tests' reflection into Gum's cursor is gone; KNI's focus flag stays, because Gum gates its whole update on it.
- Phase 4 done:
  - 4.1 `974352d`;
  - 4.2 `89732e1`;
  - 4.3 `fb7d3dc`.

  4.3 was committed twice. A power cut emptied the first commit's objects; `.git` was repaired from the last sound commit, and the same tree was committed again after a full re-check.
- 5.1: S1 and S2 fixed; S6 was already fixed. This step's commit.

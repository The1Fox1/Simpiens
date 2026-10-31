# Epic UI-Beta: Crawler Identity and the Interface Pass

- **Status**: ACTIVE (2026-10-30): B0 and B1 on one branch and one pull request. Status is owned by [ROADMAP.md](../ROADMAP.md). Decisions and deviations: `.agents/architecture_history.md`; the working plan: `.agents/ui_beta_b0_b1_plan.md`.
- **Track**: Track B: UI / UX & Player Interface. An interstitial, as UI-Alpha was: Epic UI-4 and UI-5 keep their places with Epic 10's Parts 2 and 3.
- **Source**: the Claude Doc "Epic UI-Beta: Crawler Identity and the Interface Pass (draft)" (2026-10-29, Hearthlight added 2026-10-30), with the producer's answers below.
- **Builds on**: Epic CLIENT (the KNI client; the graphics work's sprite kit, art catalogues and icon atlas), Epic 10 Part 1 (drifters, health, gear).
- **Milestones**: B0 to B6 (Part 3's draft uses L0 to L6).

---

## 0. Producer intent and decisions

On 2026-10-29 the producer asked for a UI epic to follow Epic 10 Part 1. The core story: select a crawler in the menus and see its portrait in a window. It should also lay the foundation for randomly generated bodies: skin tone, female and male, hair colour, "racial body types". On 2026-10-30 the producer chose the Hearthlight look for portraits, from four styles drawn on the draft's eight crawlers, and then said: "start B0 and B1 on one branch one PR".

| # | Decision | Answer |
| :--- | :--- | :--- |
| Q1 | Name, place, timing | "Epic UI-Beta: Crawler Identity and the Interface Pass", Track B. **B0 and B1 started 2026-10-30, before Epic 10's G4** (producer: "start B0 and B1"), not after Part 1 and R1–R6 as the draft proposed. G4 and R1 rebase onto B1's save format and hash. |
| Q2 | Where a look lives | In the core: plain data on the crawler, saved with it, drawn at spawn from the seed on its own random stream. The golden hashes are re-recorded once, for the new data. |
| Q3 | What a look holds | Lineage, body frame (female or male), build, skin tone, hair style, hair colour, facial hair, eye colour, face shape, two clothing colours, an outfit and an accessory. Age, scars from hazards and tattoos come later. |
| Q4 | Lineages | A table of any length; this epic ships two: humans and one short, broad people, working name "delvers". The world's peoples, names and lore are the producer's to write. |
| Q5 | Do looks matter to play? | **Lineage traits, as a design of their own, later** (the draft's default and alternative were swapped in the draft). This epic changes no stat, price, need or decision: the save holds a lineage id, so a later traits design adds catalogue fields, not save fields. |
| Q6 | How looks are drawn | Skin tones span the full human range with even odds; hair texture, colour and style vary independently of skin; female and male about half each; lineages are fantasy peoples, never real-world ones. |
| Q7 | Names | A name fits the body frame (feminine, masculine and neutral lists), is unique among the living, and is saved. Titles stay. Each seed's names change once. |
| Q8 | What clothes say | Clothes become the crawler's own colours and cut. State shows by pose, by the marks over the head and by the badge in the menus. Hazard gear is a miner's helm with a glowstone lamp, over the hair; guild members wear the guild's tabard. |
| Q9 | Portraits | 48×48 pixel-art busts in the Hearthlight look, at 2× in the dossier and the inspector and 1× on cards; the expression follows the state. Three-quarter, lit from the top left with a warm rim, the hub's stonework behind, a frame whose metal says standing. |
| Q10 | The map inspector | A card at the bottom left while a crawler is selected on the map: portrait, name, state, health and needs, action, Open dossier, Follow. |
| Q11 | The interface pass (B5) | Tooltips; the time of day in the header; a grouped chronicle; alerts that find their crawler; the stall opens the merchant terminal; icons in the bounty board's dropdown; debug ids in Debug builds only; a key help overlay; state marks that don't rely on colour. |
| Q12 | Done, and pull requests | B1's live scenario on the real outpost; a client test that the selected crawler's portrait is its own look; the producer's looks at B0, B2 and B3; a play session at B6. **B0 and B1 share one branch and one pull request** (producer, 2026-10-30); later milestones one pull request each. |

The working plan's decisions D1–D8 (2026-10-30) were accepted by the producer's go; where one branch changed them, the plan says so.

---

## 1. What the player sees today

Every crawler is the same 16×16 figure: one skin tone, one brown haircut, and an outfit whose colour shows its state. The dashboard knows each crawler but never shows one:
- The dossier has no picture: a name, a state badge, five bars, the goal and the action, and the affinities as text.
- Names carry debug ids ("Rosa the Guardian [SwarmAgent_0]"). A name is a hash of the entity id into 48 fixed names, so two residents can share one, and a name never follows the body.
- Lists show words only; the map has no inspector; cutscenes have no faces.

## 2. User stories

| # | As a player, I want… | So that… | Milestone |
| :-: | :--- | :--- | :-: |
| 1 | a portrait of the crawler I select, in the dossier | I know who I am looking at (the core story) | B3 |
| 2 | every crawler to look different on the map | I can follow one without selecting it | B2 |
| 3 | crawlers born with varied bodies: female and male, builds, skin tones, hair, lineages | the outpost feels like people, not copies | B1, B2 |
| 4 | the portrait to show how the crawler feels | I read its state at a glance | B3 |
| 5 | a card with the portrait when I click a crawler on the map | I learn who it is without leaving the map | B4 |
| 6 | faces beside names in every list | I recognise crawlers everywhere | B4 |
| 7 | the speaker's face in cutscenes and scene bubbles | I know who is talking | B4 |
| 8 | to click a face anywhere and have the camera find that crawler | a list leads me to the map | B4 |
| 9 | names that fit the crawler and are never shared | I can tell crawlers apart in the chronicle | B1 |
| 10 | tooltips, the time of day, a calmer chronicle and alerts | the interface explains itself | B5 |

The developers' story: a new hair style, skin palette or lineage is a row in a table, checked on a review sheet and pinned by tests (B1 to B3).

---

## 3. Design

### 3.1 The look as data (B1)
A crawler's look is a 16-byte record, `CrawlerLook`, drawn once at spawn and saved with the crawler. Each byte is an option's id in a catalogue; the core holds ids and odds, never colours or pixels (the item system's split: `ItemCatalog` holds an art key, the client's `ItemArt` the picture).

| Gene | Options | On the map (16×16) | In the portrait (48×48) |
| :--- | :--- | :--- | :--- |
| Lineage | human, delver | height and proportions | head and shoulders |
| Body frame | female, male | hair-style odds | face shape, brows, facial-hair odds |
| Build | slight, average, broad, heavy (a lineage offers some) | torso width | neck and shoulders |
| Skin tone | 10 human tones, light to deep; delvers add ruddy and ash | skin | skin ramp |
| Hair style | shaved, crop, side part, long, ponytail, bun, braids, curls, coils, mohawk | the head's outline | back and front hair |
| Hair colour | 9 natural (black to white), 3 rare dyes | hair | hair ramp, brows |
| Facial hair | none, stubble, moustache, beard | beard only | jaw |
| Eye colour | brown, hazel, green, blue, grey, amber | too small | eyes |
| Face shape | round, oval, square, long | too small | head |
| Clothes | two of 12 colours | shirt and trousers | collar and shoulders |
| Outfit | tunic, jerkin, cloak, hood, robe, furs, pauldron, scarf | not drawn | the cut of the collar and shoulders |
| Accessory | none, earring, circlet, feather, scar, warpaint | too small | ears, brow or face |

- `LookCatalog`, static and immutable like `ItemCatalog`, lists each lineage with the options it offers and their odds. Option ids are never reused. 0 means unset in every gene.
- `CrawlerLook.Generate(seed, id)` draws on its own salt of `SimRandom`, as `AgentPersonality.Generate` does, so no decision, path or price can change. Genes draw in a fixed order; a gene added later draws last.
- Every crawler gets one: residents, drifters, the Dev Map's pens.
- Names come from feminine, masculine and neutral lists. At spawn the host draws from the frame's list and the neutral one, skipping names already used. The name is saved; the identity service reads it.
- Saved and hashed: the look joins the crawler's save record (the save format rises once) and each crawler's line in `WorldStateHash`.

### 3.2 Bodies on the map (B2)
A body is baked once per look, on the CPU: today's 24 pose frames are drawn in key colours (skin, hair, shirt), so a look is a palette plus overlays (hair styles and facial hair at each frame's head anchor; builds and lineages as transforms of the torso and legs, with a hand-drawn override where a transform spoils a pose; worn gear and the guild's tabard on top). The bakes go into a region of the world texture kept for crawlers; drawing stays one quad per crawler. State shows by pose, marks over the head and the menus' badges (Q8). Tinted layers and a palette shader were rejected (no hue-shifted shadows; triple the draw items); a shader waits for a measured need on a phone.

### 3.3 Portraits (B3)
A 48×48 bust baked in layers from the same look, back to front: the plate; hair behind the head; shoulders and collar in the outfit's cut; neck; ears and head; eyes, brows, nose and mouth; facial hair; hair in front; the accessory, the helmet and the tabard; the outline. Expressions follow the state through one small table in the presentation (content, tired, hungry, frustrated, scared, hurt, asleep). Portraits are baked into a portrait atlas, like the icon atlas, at 1× and 2×, drawn 1:1.

**The look: Hearthlight.** The key light comes from the top left, banded into the four tones of `SpriteKit.RampOf`'s ramps; edges take the map's outline colour in shadow. Busts are three-quarter, with a one-pixel warm rim on the far edge. The hub's stonework sits behind the bust and a frame by standing (iron for residents, bronze for drifters, blued steel for guild members) around it, both outside the bake. Each layer writes tones, not colours, in integer maths, and the look's ramps colour them, as `ItemShapes` does, so the pins stay exact.

### 3.4 The interface pass (B4, B5)
B4 puts faces everywhere and adds the map inspector (Q10), affinities as rows, and click-to-find. B5 is Q11's list, each item strikeable.

---

## 4. Milestones

| Milestone | Delivers | Estimate (agent days) | Gate |
| :--- | :--- | :-: | :--- |
| B0 | Spike: rough body and Hearthlight portrait kits (test-only), the people sheet, the design note (§9) | 1–1.5 | The producer's look at the sheet |
| B1 | The look as data: `CrawlerLook`, `LookCatalog`, its own salt; names saved and unique; a register of everyone seen; save format raised; golden re-recorded once; a live scenario | 2–3 | The soak and the behaviour hashes hold |
| B2 | Bodies on the map: body kit, crawler region, art pins, map renders | 3–4 | The producer's look: the sheet and the map |
| B3 | Portraits and the dossier: portrait kit and moods, atlas, header, cutscene speakers | 2–3 | The producer's look: portraits and the dossier |
| B4 | Faces everywhere: inspector, faces in lists, click to find | 2 | The frame-time gate |
| B5 | The interface pass | 2–3 | Tests and renders |
| B6 | The gate: a client live test on the real outpost; docs | 1 | The producer's play session |

Every milestone passes the standing gate: a build with no warnings, `dotnet test`, the client's tests exact in the pinned image, and golden and the soak wherever the core changed.

## 5. Definition of Done

Done means looks and faces emerge in the live outpost with nothing injected, survive a save, and agree between the map and the menus.
- **Mechanism:** the look's validators (same seed, same look; each option at its odds; the catalogue; the save's round trip), the kits' art and portrait pins, the dashboard models' tests.
- **Live, headless (B1):** the real outpost on the 23 gate seeds, nothing injected: every crawler that spawns or arrives has a look; no two living crawlers share a name; across the seeds every lineage, frame, build, skin tone and hair style turns up; a save and load keeps every look and name; the golden hashes' behaviour and the soak match the run before the change.
- **Live, in the client (B6):** the day's first drifter arrives with a look of its own; selecting a crawler shows its portrait, baked from its own look and showing its mood; its body and portrait come from the same look; a click on the map opens the inspector.
- **The producer's looks and play:** the people sheet at B0, the sheet and the map at B2, portraits and the dossier at B3, a play session at B6.
- **Docs:** the roadmap first, then this file's status, then the focus file; `architecture_history`; "Adding a look" in `AGENTS.md`.

## 6. Architecture and rules

Only the core writes a look; everything after it reads. The roadmap's invariants (§1A) bind this epic.
- **Where code lives:** the core's `Simulation/People/` (the look, its catalogue, its generator, the name lists); the presentation's `Art/People/` (palettes and shapes, the body and portrait kits, the mood rule); the client's crawler region and portrait atlas.
- **Determinism and multiplayer:** a look is authoritative plain data drawn from the seed on its own salt, never from the shared stream or the clock. Changing one later is a typed player command.
- **Golden hashes and saves:** re-recorded once at B1, in their own commit; the save format rises once, at B1.
- **Nothing allocates per frame:** bakes happen at spawn, arrival, a gear change and a portrait's first showing.
- **Platforms:** texture writes use the API KNI and MonoGame share, or go through `XnaPlatform`; Android compiles at every milestone.
- **Generators never read a table or a sheet** (`ArtInitialisationTests`).

## 7. Risks

| Risk | What we do about it |
| :--- | :--- |
| Generated portraits look like programmer art | B0's spike draws Hearthlight heads for the producer's look; B3 is re-estimated from it; every layer stays replaceable by imported art in the key palette (M7) |
| The art grows combinatorially | Poses drawn once; builds and lineages are transforms; hair is an overlay at a head anchor; pins catch regressions |
| The core change disturbs the simulation | The look draws only from its own salt; the golden hashes' behaviour and the soak must match before the re-record, which is its own commit |
| Looks read as stereotypes | Q6's principles; options drawn independently; the people sheet shows the spread; lineages are fantasy peoples |
| Runtime texture writes differ on Android or MonoGame | Divergent calls go through `XnaPlatform`; both builds compile in CI |
| Faces in every list raise the UI's draw cost (F21) | The UI draw probe at B4; the frame-time gate holds |
| A mood flickers at a threshold | Hysteresis, and a mood holds a few seconds |
| Two residents look alike on the map | Measured on the gate seeds; the catalogue grows if they do |
| Every seed's names change at B1 | Render references and tests that name crawlers are updated once; older saves are already refused |

## 8. Deferred and out of scope

| Left out | Owner |
| :--- | :--- |
| Changing a look in play: an editor, renaming, dyes | Later, as a typed player command |
| Clothes and accessories as items | Epic 10 Part 2 (crafting) |
| Ageing, scars from hazards, tattoos | When the game has a calendar |
| Lineage traits that change play | A design of its own, later (Q5) |
| Pronouns in prose (the chronicle keeps names and "their") | Epic 11 |
| Real art: imported layers, 64×64, an art direction | Epic CLIENT M7 |
| UI scaling by display density; phones' portrait layout | Epic CLIENT M4 and M6 |
| Animation beyond two-frame loops | M7 |
| Shared render states for the UI (F21) | Only on B4's measurement |
| Sound in the interface | Unscheduled |

## 9. B0 design note (2026-10-30)

The spike is test-only (`Simpiens.Client.Tests/People/`). It draws every look in the catalogue on today's poses and as a Hearthlight bust. It writes the people sheet, `render-output/people-sheet.png`, for the producer's look, and its counts to `people-sheet.txt`.

**Genes and odds.** These are as `LookCatalog` holds them (§3.1's table, ids by place from 1). Humans are 80 in 100 and delvers 20. Over 10,000 draws every option turns up at its odds (`LookValidator`).

| Gene | Odds |
| :--- | :--- |
| Frame | half each |
| Skin tone | even: humans across their 10; delvers across all 12, two of them their own |
| Build | humans: slight 20, average 45, broad 20, heavy 15. Delvers: average 30, broad 50, heavy 20 |
| Hair style | by frame. Women are more often long, ponytail, bun or braids; men more often shaved, crop or side part. Delvers are more often braided |
| Facial hair | women none. Human men: none 40, stubble 20, moustache 15, beard 25. Delver men: beard 70 |
| Hair colour | 9 natural colours by weight, 3 dyes at 1 in 100 each |
| Eyes | brown 30, blue 18, hazel 15, grey 15, green 12, amber 10 |
| Face shape | even |
| Clothes | even; the second colour never equals the first |
| Outfit | even |
| Accessory | none 50 |

**Bodies on the map (for B2):**
- **Colours are free.** Skin, hair and clothes are key colours in today's poses, so a palette option costs no art.
- **The head is found from the hair's pixels in 42 of 48 frames.** It fails on Idle 1 and both Panicking frames, whose eyes are drawn differently. B2 should hold a head anchor per frame as data (24 rows) rather than infer it.
- **Hair styles:**
  - five are drawn by rule as overlays at the anchor: shaved, crop, long, bun and mohawk;
  - the other five need their own small overlays: side part, ponytail, braids, curls and coils;
  - the lying pose needs its own head row.
- **Facial hair:** a face on the map is three rows. Hair across a whole row reads as a mask, so a beard hangs below the chin and a moustache is two pixels.
- **Builds:** broad and heavy work as a column shift of the torso. Slight needs a narrowing transform. The sleeping pose takes no build.
- **Delvers:** a trouser row removed reads as short and broad, with the head kept full size.
- **State:** once clothes stop showing state, the marks over the head (!, ?, the bubble, Zzz), the tool and the arms still tell most poses apart. Idle and Walking differ only by the legs. That is for the producer's look; B2 adds the marks Q8 lists if they're needed.
- **Budget:** a look's 24 frames are 6,144 pixels, or 12,288 with the helmet (48 KB). A 512×512 region of the world texture holds 21 looks with helmets: 12 residents, 3 drifters and room to spare.

**Portraits (for B3):**
- **The layers work in integer maths.** Each shape is a dome lit from the top left and banded into four tones; skin is softer. The look's `SpriteKit` ramps colour the tones. A warm rim lights the far edge, and the outline is the map's colour in shadow and the material's deepest tone where lit.
- **What drives the cost:**
  - the hair's silhouettes: 10 styles, each front and back;
  - the features per mood, of which the spike draws content and tired;
  - the outfits' cuts, of which the spike draws the tunic for all eight.
- **The study's look isn't reached yet:** its faces are more modelled and its hair more textured. B3's 2–3 days stand, with the hair as the main risk.
- **Budget:** a portrait at 1× and 2× is 11,520 pixels (46 KB). A 1024×1024 atlas holds 90.

**The dossier's header and the inspector** (B3, B4; at 1:1):

```
+-------------------------------------------+   +--------------------------------+
| AGENT DOSSIER                             |   | [48x48] Maya       [HARVESTING] |
| +--------+  Maya the Forager              |   |         health ####------      |
| | 96x96  |  Human, average build. Resident|   |         needs  ## ## ## ##     |
| | portrait| [HARVESTING]                  |   |         Mining ore in the Seam |
| | iron   |  Content. Health 100%.         |   | [Open dossier]  [Follow]       |
| +--------+                                |   +--------------------------------+
| drives, gear, intent below                |      the map inspector, bottom left
+-------------------------------------------+
```

**Names (B1; plan D4–D7):**
- Three lists (feminine, masculine and neutral) of at least 48 names each, Latin-1 only and at most 10 characters.
- A crawler draws from its frame's list and the neutral one, on its own salt.
- A name is never shared with anyone the session has seen while the lists last, then only with the living.
- The register of everyone seen outlives a drifter's departure, so the chronicle still names it, and is saved.

**Re-estimates:** B2 stays 3–4 days (the anchor table and five hair overlays are new work); B3 stays 2–3 days.

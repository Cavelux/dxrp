# Changelog: Field Guide Placement Kit

Display versions are `0.x.y` while the kit API settles. Bumped on every publish (minor per content
release, patch for hotfix, manual bumps only).

## v0.1.1 - 2026-07-31

Both demo panels get the Field Kit look.

### Changed
- The tweak panel is restyled to the Field Kit UI system: near-black card, 16px radius, hairline
  border, and the kit's green accent on the active tab. The three step segments become a pill group,
  the sliders get pill tracks with a flat accent fill, and Copy is promoted to the primary button.
- The demo hint card matches the panel it sits opposite: same card fill, radius and hairline, a 16px
  title, a 13px lede, and 110px key chips in pure white bold 700 on a neutral plate. The card stays
  accent free on purpose, so the green stays the tweak panel's colour.
- A slider at the bottom of its range keeps a 14px minimum fill, so it reads as a pill rather than an
  empty track, and the track picks up a hover tint.

### Fixed
- A hint row with a two line explanation no longer leaves its key chip floating half way down. Chips
  are top aligned to their row.

### Notes
- Visual pass only. No behaviour, convar, key binding or panel boot-state logic changed.
- Both stylesheets carry their own copy of the design tokens, with a drift note, because kits cannot
  import from each other. The mockup's CSS was translated rather than copied, to stay inside what the
  engine's parser accepts: border-width plus border-color instead of the border shorthand, no
  box-shadow, no letter-spacing, expanded offsets instead of the inset shorthand, explicit px line
  heights, and font sizes only from 12/13/14/16.

## v0.1.0 - 2026-07-30

First release. Extracted and generalized from World Builder's in-game placement workflow, then
rebuilt around its hero case: fitting custom models to a character and baking the offsets into code.

Demo polish (2026-07-30, after the owner's live pass):

- The demo opens with the tweak panel already up and the three accessories already fitted, at offsets
  measured on the shipped citizen. The scene's point is the panel, so it no longer waits behind a
  keypress, and the first thing on screen is a finished result rather than three primitives in a heap.
- `TweakPanel.OpenOnStart` decides the panel's boot state, and nothing else does. It is off by default,
  so a `placement_panel 1` left persisted from an earlier session still cannot pre-open the panel in a
  consumer game, and the panel logs when it forces a stale value closed. Applied on the first update
  rather than in `OnStart`, so a panel built in code cannot race its creator's configuration.
- Accessories are now a bare root plus child primitives: the root carries the offset and a uniform scale,
  the children carry the proportions. The tweak panel's Scale row writes a uniform scale on the root, so
  proportions kept there would be flattened the first time anyone dragged it.
- Silhouettes read as what they represent, from stock dev primitives only: the hand tool is a handle with
  a heavier head, the hat is a flat brim disk under a dome crown, the pack is taller up the spine than it
  is deep off the back. All built along local X, which is the axis the bone chain runs down on this rig.
- Child parts are sized in engine units and divided by the model's own bounds, so the part table reads as
  real dimensions instead of model-relative multipliers. Root scale now defaults to 1, meaning "the size
  it was authored at", and that is the number the export carries.
- Accessories whose mount resolves to an unexpected frame fall back to the previous character-frame seed
  and say so in the console. Bone-local numbers are only valid on the rig they were measured on.
- The head accessory is now "Hat" rather than "Head Visor", so it bakes as `hat`.

Added in the pre-publish pass (2026-07-30):

- `CharacterAttachPoint`: mount a GameObject on a skinned character by attachment name (`hold_R`,
  `hat`) or bone name (`hand_R`, `spine_2`), whichever resolves first. Resolution is deferred and
  retried, because a model created this frame has no attachment objects yet and an unposed skeleton
  reads the bind pose; after a timeout it falls back to the character root and says so, so a renamed
  attachment degrades instead of vanishing. A bone mount is re-pinned each frame in `OnPreRender`, so a
  worn item rides the animation rather than sliding with the root.
- `AttachedTweakTarget`: a tweak target that reads its frame label from a live `CharacterAttachPoint`,
  so the export and the bake comment name the mount the accessory actually landed on.
- `PlacedOffset`: the small record a baked line constructs, with `ApplyTo( GameObject )` writing it back
  onto a local transform. This is what the Copy button hands you and what your game code holds.
- `TweakRanges`: per-target slider bounds and steps, defaulting to accessory scale (position ±48 units
  at 0.25, so xfine reaches 0.025). `TweakRanges.Scene` restores the old ±1024 at 1 for scene props.
- Optional `ITweakTarget` members, all default-implemented so existing implementations keep compiling:
  `FrameName` (what the offset is relative to), `BakeSymbol` (the id the bake line names), `Ranges`, and
  `FormatBakeLine` (shape the line into your project's own data type).
- `PlacementExport.BakeLine` / `DefaultBakeLine`: the paste-ready two-line form, a comment naming the id
  and frame plus a `PlacedOffset` constructor.
- Tweak panel: a Copy button that puts the active target's bake line on the clipboard game-side via
  `Sandbox.UI.Clipboard.SetText` and flips to "Copied!"; a three-state step toggle (xfine ÷10, fine,
  coarse ×10); a frame sub-line naming what the numbers are measured against; a 42px close control.
- Demo: a dressed stock citizen with three accessories on real mounts (hand attachment, head attachment,
  spine bone), plus an on-screen key card up from the first frame.
- `OrbitCamera.FocusStartM`: where the camera starts looking, so a scene can frame a standing character
  instead of its feet.

Changed:

- `PlacementExport.Collect` now exports BOTH populations: tweak-session targets as LOCAL offsets tagged
  with their frame, and `PlacedInstance` objects in world space. It previously read world transforms off
  `PlacedInstance` only, which meant exporting from the tweak panel produced nothing at all.
- `PlacedItem` carries a `Frame` string ("world" for a placement, "citizen/hold_R" and the like for an
  offset). JSON entries gained a `frame` field; the C# snippet splits into an offsets block and a
  placements block.
- Tweak panel Reset restores the transform captured when the target was registered, not identity.
  Zeroing a hand-mounted accessory collapses it into the wrist, which is never what you wanted back.
- `TweakSession` records each target's authored transform at registration, and exposes `CaptureSeed` to
  re-baseline one after code has positioned it.
- Docs: the README is rebuilt around the accessory-fitting loop, with scene authoring second. The claim
  that game code cannot reach the clipboard is removed; it is false. The Input.config section now states
  that `attack1` and `attack2` are required actions and what to do in a project that dropped them.

Added in the first extraction pass:

- `OrbitCamera`: freed-cursor orbit / pan / zoom tool camera, ported near drop-in. The two former
  project-global tuning constants (units-per-meter and the keyboard orbit rate) are now `[Property]`
  fields with the same defaults.
- `TweakPanel` + `ITweakTarget` + `TweakSession`: a tabbed live transform tuner (position, rotation as
  pitch/yaw/roll, uniform scale) with drag-scrub sliders and step nudges, editing any caller-supplied
  targets. Toggle with `P` or the `placement_panel` convar.
- `GhostPlacer` + `PlacementCatalog` + `PlaceableEntry` + `PlacedInstance`: aim-follow ghost placement
  with a validity-tinted preview, catalog cycling, rotate, place and delete. Single-player authoring
  only (no networking). Toggle with `B` or the `placement_place` convar.
- `PlacementExport` + `PlacedItem`: export placed transforms as JSON and as a paste-ready C# snippet,
  written into `FileSystem.Data`.
- `PlacementExportTool` (Editor): menu actions that copy an export file to the system clipboard.
- `placement_demo.scene`: a runnable demo wired by `DemoBootstrap`.

# Changelog

## v0.5.0 (2026-07-31)

The Tips Studio, and live reload. Everything here is additive; the v0.1 through v0.4 API keeps compiling and behaves identically for existing consumers.

Added
- Tips Studio: an authoring panel that runs inside your game. Add a `TipsStudioPanel` to a `ScreenPanel` object, press play, press `T`. It lists every tip in the merged catalog with the source it resolved from, opens any of them in an editor for wording, order and prerequisites, and gives each trigger a kind picker that shows only the fields that kind reads. The `InputAction` picker is fed from the project's real input actions, not a list the kit invented. Off by default behind `fg_tips_studio`, and it forces itself shut outside the editor unless you say otherwise.
- Live card preview: the draft is drawn by `TipsDisplay` itself, lower left, with the same markup and the same stylesheet the player sees, so what you are looking at IS the shipped card. It follows the wording as you type. Buttons pin the card to keyboard or pad (`TipsCoach.PreviewDevice`), so the pad wording and the pad chips are readable without owning a controller.
- Test fire: put the draft on screen under its own id and complete it for real, so the prerequisite chain advances in front of you rather than in your head.
- Bake out, two ways. Copy puts the `.tip` file's exact contents on the clipboard from in game. Stage writes it into the game's data folder, and the new editor menu actions (Field Guide / Write staged tips to Assets/tips) turn every staged tip into a real asset in the current project's `Assets/tips`, skipping anything that already exists unless you pick the overwrite action. What the Studio writes is byte for byte what the editor writes, asserted against the five shipped `.tip` assets in the harness.
- Authoring notes: the Studio flags triggers that can never fire, a `Timer` with no seconds, and runs of text long enough to hit the engine's grey-block quirk (a single run longer than one card line rasterizes as a solid rectangle).
- `TipsCatalog.RegisterScoped( tips )`, which hands the previous code catalog back when you dispose it, plus `ClearRuntime`, `RuntimeIds` and `TipsCatalog.View`. `TipsCoach` gains `IsCompleted`, `Uncomplete` and `DropActive`.
- Console: `fg_tips_rebuild` rescans `.tip` assets and prints the merged result; `fg_tips_studio` opens the panel.
- The demo scene walks you into the Studio. Its last tip now reads "That is the loop. Press T for the Tips Studio and edit these very tips. R replays.", the demo bootstrap builds the Studio's host at boot so that key works with nothing to set up, and opening the Studio is what retires the tip: you finish the walkthrough looking at the five tips you just walked, ready to edit them. The wiring is two small pieces and worth copying if you want a tip to retire on one of your own screens. The bootstrap raises a string signal while the Studio is open and `demo_wrap.tip` carries a `Signal` completion waiting on that name, so the kit itself knows nothing about the Studio's open state. `R` closes the Studio as it replays, which puts the last beat back.

Fixed
- An edited `.tip` never reached a running session. `TipsCatalog` cached its merged list and nothing invalidated it on an asset change, so authoring a tip meant restarting the editor. `TipResource` now tells the catalog from PostLoad and PostReload, the engine's own load and recompile hooks, so a created or edited tip is live immediately. `fg_tips_rebuild` covers what no hook announces, a deleted file or a code hotload.
- A code catalog registered from a scene component outlived its scene, and the leftover tips (highest priority, nothing present to retire them) pinned an unretireable card in the next scene. `RegisterScoped` is the fix, and the README says so where the registering happens.

Changed
- Both surfaces are rebuilt on the Field Kits UI system (docs/design/ui-system). The Studio is a centred 1360px modal over a dim scrim in three columns: the catalog on the left, the tip in the middle, and on the right the same card drawn twice, once as a keyboard player reads it and once as a controller player does, over the bake buttons. The toast is the shared notification card: a 3px accent stripe the card's own rounded corner clips, a mono kicker, short prose runs with atomic key chips that never split across a wrapped line, and a 42px close target. Both surfaces carry the system's type scale, 16 / 14 / 13 / 12 with exact px line heights. An earlier build of this release stripped every font size out on the strength of MCP screenshots that draw sized text as grey blocks; that artifact belongs to the capture path, the panels were always sharp on a real screen, and the sizes are back.
- The merged catalog is one value holding the tip list and the source labels together, rebuilt from a pure `TipCatalogMerge` and swapped in whole, instead of three parallel statics that could disagree. `TipsCatalog.Example` becomes a computed property, so an edit to it is not stranded behind a hotload.
- `TipsDisplay` folds the active tip's wording into its BuildHash alongside its id, so the card follows a tip that is rewritten under the same id. A shipped game is unaffected: `TipDefinition` is a record with init-only fields, so its wording cannot change without the tip being replaced.

## v0.4.0 (2026-07-21)

Device-aware tips. Everything here is additive; the v0.1 through v0.3 API keeps compiling and behaves identically for existing consumers.

Added
- Demo scene: `Assets/scenes/tips_demo.scene`, a runnable five-tip sequence (move, jump, walk to the marker, switch it on, wrap up) coaching a dressed stock citizen you drive with the stick or W A S D. It covers the analog kind, an input action with pad wording, both `TipTriggerObject` world modes and a timed beat. The tips are `.tip` assets (`Assets/demo/`); the wiring is delete-me code in `Code/Demo`. Press R to replay. The camera is fixed and framed to hold the whole play area, so the citizen and the marker stay in view wherever you walk.
- Demo pawn look: the pawn is a dressed citizen rather than a blue box. `TipsDemoCitizen` builds it in code at boot from the engine's own citizen model and `citizen_clothes` resources, switches the scene's authored box renderer off, turns the citizen to face its travel and drives the stock citizen animgraph (`move_*` from the ground actually covered, `wish_*` from the stick, `b_grounded`, `b_jump` on the hop). The kit still ships no art, and the scene file still holds the plain block, so the editor viewport shows the simple authored object when nothing is playing.
- Active device: `TipsCoach.ActiveDevice` (KeyboardMouse / Gamepad, backed by `Input.UsingController`). The display folds it into its BuildHash, so plugging or unplugging a controller re-renders the active tip at once.
- Per-device text: an optional `TipDefinition.TextPad` (and a matching `TextPad` field on the `.tip` asset), shown instead of `Text` while the player is on a controller. Null falls back to `Text`, so a tip with no pad wording reads the same on both devices and the "either" idiom (one line naming both a keycap and a pad chip) still renders both.
- Pad label map: `TipsCoach.PadLabelFor`, a whole-catalog keycap remap applied at render time on a pad. Return a controller label to swap a chip, the label unchanged to pass it through, or null/empty to skip a chip with no pad equivalent. It is the alternative to authoring `TextPad` on every tip.
- Analog trigger: `TipTrigger.Analog(source, magnitude)` and the `AnalogAxis` kind (on the `.tip` asset too), completing while a move or look stick is pushed at or past a magnitude. Retires a stick-throttle "accelerate" on a pad, which has no digital press to read.
- Hide seams: `TipsCoach.HideKeyLabel` / `HidePadLabel` / `HideAll`, for an optional device-aware "hide tips" chip beside the close affordance. Unset by default, so the close affordance keeps dismissing the current tip as before; set `HideAll` to route both at a game-owned "tips off" action instead.
- Console: `fg_tips_device` prints the active device and how the on-screen tip resolved (Text vs TextPad, the parsed chip segments, the hide label). `fg_tips_list` now leads with the active device and adds a per-tip live relevance verdict.

Changed
- Shipped `.tip` assets, `Assets/starter.tip` included, now gate their `Relevance` on the `fg_tips_demo` context flag that only the demo bootstrap sets. They still merge into the catalog, but a game that vendors the kit never sees a demo tip on screen. Delete `Assets/demo`, `Assets/starter.tip` and `Code/Demo` in your own project.

## v0.3.0 (2026-07-21)

Trigger system. Everything here is additive; the v0.1/v0.2 API keeps compiling and behaves identically for existing consumers.

Fixed
- Toast text rendered as solid filled blocks: the engine corrupts glyphs on panels that declare font-size or letter-spacing (verified on 26.07.15a; even one shared font-size corrupted). The stylesheet now declares neither; text scale inherits the engine default and the kicker uses weight plus uppercase for hierarchy.

Added
- Declarative triggers: `TipTrigger` (record) + `TipTriggerKind` with factories (`Action`, `KeyPress`, `Named`, `After`, `Any`, `All`), and two optional `TipDefinition` fields, `Completion` and `Relevance`. A tip retires when any completion path fires (predicate, declarative trigger, timeout, or `TipsCoach.Complete(id)`); the readable-window floor still applies to all of them.
- Self-driving coach: the coach evaluates triggers from its own `OnUpdate` and reads `Input` directly, so input, key, signal and timer tips need no `Tick` call and no bridge code. A game that calls `Tick` every frame stays fully consumer-driven and behaves identically.
- `TipsCoach.Complete(id)` to retire a tip by id from anywhere.
- `.tip` GameResource (`TipResource` + `TipTriggerSpec`) authored in the inspector, with a native input-action dropdown; discovered via `ResourceLibrary.GetAll`. The catalog merges code, assets and runtime drafts by id (precedence code > asset > draft), with `RegisterRuntime` / `UnregisterRuntime` / `Rebuild`. One starter `.tip` ships in `Assets/`.
- `TipTriggerObject` drop-on-object component for world completion (Interacted, PlayerEntered, LookedAt, Signal), with fail-inert `TipsWorld` seams (`LocalPlayerPosition`, `AimRay`, `HasLocalPlayer`). Proximity and look-at are inert until their seam is set.
- Console: `fg_tips_list`, `fg_tips_show <id>`, `fg_tips_complete <id>`.

## v0.2.0 (2026-07-20)

Release-readiness pass. Everything here is additive; the v0.1.0 API keeps compiling.

Added
- Gamepad button chips: `` `backticks` `` render a rounded controller chip next to the existing `*asterisk*` keycaps, so one tip line can prompt keyboard and gamepad at once.
- Genre-neutral drive path for games that are not RPGs: `TipContext.SetFlag` / `SetNumber` (live custom conditions), `TipsCoach.Signal(string)` with `TipContext.Ever(string)` (latched custom behaviours).
- `TipsCoach.SaveFileName` to namespace the progress file, and `TipsCoach.MigrateProgressFrom(legacyFile)` to carry old progress forward once (generalized from the first consumer's private migration helper).
- Theming: the toast SCSS reads a `THEME TOKENS` variable block a consumer edits in one place to reskin the panel, including separate keycap and gamepad-chip tokens.

Changed
- `TipSegment` now carries a `TipSegmentKind` (Plain / Key / GamepadButton); `IsKey` stays as a convenience property.

## v0.1.0 (2026-07-18)

First release. Extracted from RPG Builder (commit 2066894).

Added
- Contextual tip coach: priority-ordered, retires tips by observed player behaviour, hides while an overlay or modal is open.
- Neutral TipContext + signal methods so any game wires its own events and vitals in.
- TipsCatalog registration types and a bright keycap-markup TipsDisplay HUD panel.

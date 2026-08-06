# Provenance: Field Guide Placement Kit

All code here is our own, extracted from the World Builder game project and relicensed MIT for this
library. No third-party code, no fabricated attribution.

## Source

- Repo: `world-builder` (private game project)
- Commit pinned at extraction: `e3c799c87e625fcb3edc77624b594bf064e4e283`
- Extraction date: 2026-07-18

## Ported vs pattern-rebuilt

- `Code/OrbitCamera.cs`: PORTED near drop-in from `world_builder/Code/Game/OrbitCamera.cs`. Changes:
  namespaced `FieldGuide.Placement`; the two `Tuning` reads (`Tuning.M`, `Tuning.OrbitKeyRotateDegS`)
  lifted into the `UnitsPerMeter` and `OrbitKeyRotateDegS` `[Property]` fields with the same defaults;
  the two `RotateCCW` / `RotateCW` input actions replaced with raw `Q` / `E` keyboard reads so the kit
  needs no custom Input.config action; `FocusStartM` added so a scene can frame a standing subject;
  comments genericized; category retagged.
- `Code/Tweak/TweakPanel.razor` (+ `.scss`), `TweakSession.cs`, `ITweakTarget.cs`, `TweakRanges.cs`:
  PATTERN-REBUILT from `world_builder/Code/Items/WbMountTweak.cs` + `Code/UI/MountTweakPanel.razor`
  (+ `.scss`). The interaction pattern (tabs, drag-scrub sliders, +/- step nudges, the three-state
  xfine / fine / coarse step toggle, the frame sub-line, the Copy button that flips to "Copied!") is
  preserved; the WB-specific item/mount internals are replaced with the generic `ITweakTarget` seam
  editing a target GameObject's local transform, and the target list moves to a `TweakSession`. The
  colours are the kit's own: WB's sheet predates the contrast rules this kit is written to.
- `Code/Attach/CharacterAttachPoint.cs` (+ `AttachedTweakTarget.cs`): PATTERN-REBUILT from the
  attachment-then-bone mount resolution in `world_builder/Code/Player/WbCharacter.cs` and the
  hand-mount recipe in `fieldguide.rpg/Code/Items/Equipment.cs`. Generalized to a named-candidate list
  with deferred retry and a character-root fallback.
- `Code/Placement/GhostPlacer.cs` (+ `PlacementCatalog.cs`, `PlaceableEntry.cs`, `PlacedInstance.cs`):
  PATTERN-REBUILT from `world_builder/Code/Game/WbPropPlacer.cs`. The ghost-follows-aim, validity-tint,
  rotate/place/delete flow is preserved; all networking, host RPCs, rate limiting and the MCP bridge are
  dropped (single-player authoring per spec); the fixed WB model set becomes a caller-supplied catalog
  with a `Func<Vector3, Rotation, bool>` validity seam.
- `Code/Export/` (`PlacementExport.cs`, `PlacedItem.cs`, `PlacedOffset.cs`, `BakeFormat.cs`): NEW,
  inspired by WB's "bake back into constants" dump and its `BakeLine` / clipboard Copy. Produces JSON
  and a C# snippet instead of WB-specific constant lines, and covers both local offsets and world-space
  placements.
- `Editor/PlacementExportTool.cs`: NEW editor-assembly tool using `EditorUtility.Clipboard.Copy`, for
  the bulk export files. Single-line copies happen game-side via `Sandbox.UI.Clipboard.SetText`.
- `Code/Demo/DemoBootstrap.cs`, `DemoHintCard.razor` (+ `.scss`), `Assets/scenes/placement_demo.scene`:
  NEW demo wiring. The dressed stock citizen follows the driver recipe in
  `fieldguide.vehiclephysics/Code/VehicleFactory.cs`.

## Backport policy

The game repo stays canonical for behavior. Backports are deliberate: when an upstream OrbitCamera or
placement change is worth shipping, port it, bump the kit version, and update the pinned commit above.

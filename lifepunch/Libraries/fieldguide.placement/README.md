# Field Guide Placement Kit

Version 0.1.1 · MIT · namespace `FieldGuide.Placement`

Fit things to a character in-game, then paste the numbers into your code.

You have a hat, a backpack, a weapon, a lantern. It hangs off a bone or an attachment on your character
model, and it needs an offset: a position, a rotation, a scale, relative to that mount. Guessing those
numbers in a text editor costs a recompile per nudge. This kit gives you sliders in the running game and
a Copy button that hands you the finished line.

It also does scene authoring, because it came out of a game that needed both: ghost-place props with the
mouse, then export every world transform. That is the second half of the README.

Extracted and generalized from World Builder's in-game placement workflow. Self-contained: it compiles
alone, references no other library and no game code, and reaches back into your project only through a
few small seams. MIT licensed. Source-distributed: read it, edit it, delete the modules you do not need.

## The accessory-fitting loop

1. Parent your model to a mount on the character. `CharacterAttachPoint` resolves that mount by name,
   trying model attachments first (`hold_R`, `hat`) and then bones (`hand_R`, `spine_2`), and falling
   back to the character root if nothing resolves so a renamed attachment never leaves you with nothing
   on screen.
2. Register it with the scene's `TweakSession`, giving it the frame it hangs off.
3. Press `P` and drag. Position, pitch / yaw / roll, and uniform scale, all applied to the object's
   LOCAL transform, so what moves is the offset.
4. Press Copy. The line lands on your clipboard:

```csharp
// hand_tool: offset from citizen/hold_R
new PlacedOffset( "hand_tool", new Vector3( 5.25f, 0f, -3f ), new Angles( 0f, 90f, 0f ), 0.18f ),
```

5. Paste it into your project and apply it:

```csharp
static readonly PlacedOffset HandTool =
    new PlacedOffset( "hand_tool", new Vector3( 5.25f, 0f, -3f ), new Angles( 0f, 90f, 0f ), 0.18f );

tool.SetParent( handMount, false );
HandTool.ApplyTo( tool );          // writes LocalPosition / LocalRotation / LocalScale
```

Wiring the whole thing takes about a dozen lines:

```csharp
var mount = characterChild.Components.Create<CharacterAttachPoint>();
mount.CharacterName = "citizen";
mount.AttachmentNames = new List<string> { "hold_R", "hold_r" };
mount.BoneNames = new List<string> { "hand_R", "hand_r" };

var tool = Scene.CreateObject();
tool.SetParent( mount.GameObject, false );
tool.Components.Create<ModelRenderer>().Model = Model.Load( "models/my/tool.vmdl" );

TweakSession.Instance.Add( new AttachedTweakTarget( tool, mount, "Hand Tool" ) );
```

`AttachedTweakTarget` reads the frame from the mount LIVE, which matters: a skinned model has no
attachment objects on the frame it is created, so the mount resolves a beat late. Read the frame once at
registration and your bake comment says `citizen/root` forever.

## The demo

`Libraries/fieldguide.placement/Assets/scenes/placement_demo.scene` (asset path
`scenes/placement_demo.scene`) opens on a dressed stock citizen with three accessories already
fitted and a key card on screen telling you what to press:

- a tool in the right hand, on the `hand_R` bone,
- a hat on the `head` bone,
- a pack on the `spine_2` bone.

All three are bone mounts, re-pinned every frame, so they ride the idle animation instead of sliding
with the root. Each mount asks for an attachment first (`hold_R`, `hat`) and falls back to the bone; on
the shipped citizen the bones are what resolve.

The tweak panel is already open on the right, because fitting the accessories is what the scene is for.
`P` and the header `×` close it.

The accessories open at offsets already dialled in on this rig, so the first thing you see is a finished
result rather than three primitives in a heap. Drag to change your mind, Reset to come back.
Anything the demo cannot recognise, a renamed bone or the character-root fallback, starts from a generic
character-frame seed instead and says so in the console, because a bone-local number is only valid on
the rig it was measured on.

Each accessory is a bare root holding one or more child primitives. The root carries the offset and a
uniform scale; the children carry the proportions. That split matters: the panel's Scale row writes a
uniform scale on the root, so non-uniform proportions kept there would be flattened the first time
anyone touched the slider. The row starts at 1, meaning "the size it was authored at".

Everything the demo uses ships with the engine: the citizen model, its clothing, and the dev box and
sphere standing in for your art.

Press `B` for the second beat: ghost placement. Boxes follow your aim, green where the demo's validity
seam allows them, and land as world-space placements that export alongside the accessory offsets.

`Code/Demo/` is the delete-me folder. `DemoBootstrap` and `DemoHintCard` are demo scaffolding, not kit
surface.

## Modules

- `Code/Attach/` : `CharacterAttachPoint` (mount resolution by attachment or bone name, deferred and
  retried, with a root fallback) and `AttachedTweakTarget` (a tweak target whose frame label comes from
  its mount).
- `Code/Tweak/` : the tabbed live tuner (`TweakPanel`), its seam (`ITweakTarget`), the target holder
  (`TweakSession`), and slider bounds (`TweakRanges`).
- `Code/Export/` : `PlacementExport` (bake lines, JSON, C# snippet), the `PlacedItem` export record, the
  `PlacedOffset` paste target, and `BakeFormat` (shared float and id formatting).
- `Code/Placement/` : ghost placement. `PlacementCatalog` (+ `PlaceableEntry`), the `GhostPlacer` driver,
  and the `PlacedInstance` tag stamped on placed objects.
- `Code/OrbitCamera.cs` : orbit / pan / zoom tool camera. Cursor stays visible so the UI is clickable.
- `Code/Demo/` : `DemoBootstrap` and `DemoHintCard`, which wire the demo scene in code. Delete this
  folder in your project.
- `Editor/PlacementExportTool.cs` : editor menu actions that copy a bulk export file to the system
  clipboard.
- `Assets/scenes/placement_demo.scene` : the runnable demo (installs under `Libraries/fieldguide.placement/`,
  see the Quickstart for how to find it).

## Quickstart

1. Install the kit through the editor's Library Manager (View menu), or drop this folder into your
   project's `Libraries/`.
2. Open the demo scene. The kit installs under your project's `Libraries/` folder, not your own
   `Assets/`, so it is not where a new scene search starts. Two routes that work: in the Asset
   Browser, select **Everything** and search `placement_demo`, or walk the sidebar to
   **Libraries > fieldguide.placement > Assets > scenes**. Double-click `placement_demo.scene` and
   press Play. Or wire your own scene:
   - Add a `TweakSession` anywhere in the scene. Register targets with `TweakSession.Add(...)`, or drop
     GameObjects into its `Objects` list in the inspector for the no-code path.
   - Add a `TweakPanel` on its own `ScreenPanel` GameObject.
   - For character work, add a `CharacterAttachPoint` on an empty child of the character and parent your
     accessory under it.
   - For scene authoring, add `OrbitCamera` next to a `CameraComponent` (set `UnitsPerMeter = 1` to
     author in engine units), plus a `PlacementCatalog` filled with `PlaceableEntry` items and a
     `GhostPlacer`.
3. Press `P` to tweak, `B` to place. Copy or export when the numbers look right.

## Key bindings

Camera (always active):

- Right mouse (hold) : orbit
- `W` `A` `S` `D` : pan the focus point
- `Q` / `E` : orbit left / right from the keyboard. `Q` and the `G` delete key below are raw keyboard
  reads that overlap the stock `Menu` and `Drop` actions; if your game reads those, change the keys per
  instance on `OrbitCamera` / `GhostPlacer`.
- Mouse wheel : zoom

Tweak panel:

- `P` : toggle the panel (console fallback: `placement_panel 1` / `0`)
- The panel starts closed unless its `OpenOnStart` property is set. That property is the only thing that
  decides the boot state, so a `placement_panel 1` left persisted in a previous session cannot pre-open
  it in your game.
- Drag a row's slider to scrub, `+` / `-` for precise steps
- The step toggle sets what those steps are worth: `xfine` divides the row's step by ten, `fine` is the
  row's step, `coarse` multiplies it by ten. At the accessory default that is 0.025 / 0.25 / 2.5 units.
- Reset returns the target to the transform it had when it was registered, not to zero
- Copy puts the active target's bake line on the clipboard
- The `×` in the header closes the panel

Placement mode:

- `B` : toggle placement mode (console fallback: `placement_place 1` / `0`)
- `[` / `]` : cycle the catalog
- `R` : rotate the ghost
- Left mouse : place on a valid (green) spot
- `G` : delete the placed object under the cursor

Demo only:

- `H` : hide or show the key card (console fallback: `placement_hint 1` / `0`)

### Required Input.config actions

The kit reads two stock engine actions: `attack1` (place) and `attack2` (orbit). Everything else is a raw
keyboard read, so no letter or bracket key above needs registering.

Those two are REQUIRED, not optional. A fresh s&box template project ships them and you have nothing to
do. A project whose `Input.config` was rewritten or trimmed may have dropped them, and then placing and
orbiting silently do nothing: `Input.Pressed` / `Input.Down` on an action the project does not define
never returns true, and the editor console names the missing action when the read happens. If clicking
does not place and right-drag does not orbit, open `Input.config` and re-add `attack1` (mouse1) and
`attack2` (mouse2). Stop and start play mode after adding them; if the actions still do not register,
restart the editor.

`OrbitCamera` also reads `Input.AnalogMove` for WASD panning, which comes from the stock movement group
(`Forward` / `Backward` / `Left` / `Right`). Same rule: stock projects have it, trimmed ones may not.

## Seams (how the kit reaches back into your project)

- `ITweakTarget` : `DisplayName` and `GameObject Target` are required. `FrameName`, `BakeSymbol`,
  `Ranges` and `FormatBakeLine` are default-implemented, so an existing implementation keeps compiling.
  - `FrameName` : what the offset is measured against, e.g. `"citizen/hold_R"`. Shows on the panel
    sub-line, goes into the export and the bake comment.
  - `BakeSymbol` : the id the bake line names. Defaults to a code-safe form of `DisplayName`, so
    "Back Pack" bakes as `back_pack`.
  - `Ranges` : a `TweakRanges` giving the sliders their bounds and steps. Defaults to
    `TweakRanges.Accessory` (position ±48 units at 0.25). `TweakRanges.Scene` (±1024 at 1) is the right
    one for a placed prop.
  - `FormatBakeLine( PlacedItem )` : return your own text to bake straight into your project's real
    shape, e.g. `ItemMounts.HatOffset = new Vector3( ... );`. Return null for the kit's default line.
- Built-in implementations: `TweakTarget` (wraps a GameObject, all four settable) and
  `AttachedTweakTarget` (reads its frame from a `CharacterAttachPoint`).
- `CharacterAttachPoint.AttachmentNames` / `BoneNames` / `CharacterName` : the mount candidates and the
  label that prefixes the frame string.
- `PlacementCatalog.ValidityCheck` : `Func<Vector3, Rotation, bool>`; return whether a spot is valid.
  Null means always valid. The ghost tints green / red from this, and a click only places on a valid spot.
- `PlaceableEntry.ModelPath` / `PlaceableEntry.Prefab` : what the catalog spawns (a model path builds a
  GameObject + ModelRenderer; a template GameObject is cloned).
- `OrbitCamera` `[Property]` fields (`UnitsPerMeter`, `FocusStartM`, `OrbitKeyRotateDegS`, distances,
  speeds) : all tuning is per-instance config; the camera reads no project-global constant.

## Export

Three routes out, smallest first.

**One line, from the game.** The tweak panel's Copy button puts the active target's bake line on the
system clipboard with `Sandbox.UI.Clipboard.SetText`, in play, no editor round trip. This is the fast
path and the one the accessory loop uses.

**Everything, to a file.** "Export all to Data" (or `PlacementExport.WriteAll(Scene)`) writes two files
into `FileSystem.Data` and logs their full paths. On Windows that resolves to
`<s&box install>\data\<your org>\<your ident>#local\`, for example
`...\steamapps\common\sbox\data\local\my_game#local\`. Note this is outside your project folder, so it
is not under your source control:

- `placement_export.json` : an array of `{ id, frame, position {x,y,z}, rotation {pitch,yaw,roll}, scale }`.
- `placement_export.cs` : up to two paste-ready blocks, a `PlacedOffset[]` of character-relative offsets
  with the frame named above each line, and a `PlacedItem[]` of world-space placements.

Both collect BOTH populations: every `ITweakTarget` in the scene's `TweakSession` as a local offset
tagged with its frame, and every object carrying a `PlacedInstance` in world space.

**The file to your clipboard.** `Editor → Field Guide → Copy Placement Export (JSON)` or `(C# snippet)`
reads the latest exported file and copies it whole, using `EditorUtility.Clipboard` from the editor
assembly. Use this when you want the bulk export rather than one line.

## License

MIT. See `LICENSE`.

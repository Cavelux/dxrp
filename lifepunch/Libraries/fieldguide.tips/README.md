# fieldguide.tips

Version 0.5.0 · MIT · namespace `FieldGuide.Tips`

A self-contained s&box contextual tip coach: a calm HUD panel that shows one onboarding tip at a
time, orders them by priority, and retires each tip the moment the player *does the thing* it teaches
instead of on a timer.

This is a library of SOURCE you vendor into your game's `Libraries/` folder. It references no game
code and no other library. The kit owns the coaching engine, the catalog types and the display; your
game owns the words (your tip content) and the wiring (feeding the coach context and signals). Nothing
flows back to your game except through the seams below.

## The model

A single `TipsCoach` component decides which one tip is relevant right now and publishes it for the
`TipsDisplay` panel to render. It never fights for attention: it hides while a modal, a conversation,
or a full-screen dev/menu overlay is open, and every tip auto-retires by *observed behaviour* so the
coach walks with the player instead of lecturing.

Because a library may not read game state, the coach is DRIVEN by your game two ways:

1. Every frame you build a neutral `TipContext` and call `coach.Tick(ctx)`.
2. When you observe a one-off behaviour (moved, cast a spell, aggroed an enemy) you call a signal
   method. The coach latches it for the session, and the matching `Ever*` field on the context reads
   true from then on.

Progress (completed tip ids + a "dismissed for good" flag) persists to `FileSystem.Data`
(`fieldguide_tips.json`) so returning players are not re-taught. `FileSystem.Data` is sandboxed per
game, so that file never collides with another game that also vendors the kit. See
[Save files and migration](#save-files-and-migration) if you want to rename it or carry old progress
forward.

## Install

To see the coach run before you wire anything, open `Assets/scenes/tips_demo.scene` and press play (see
[Demo scene](#demo-scene)). For your own game:

1. Copy `fieldguide.tips/` into your project's `Libraries/` folder.
2. At client bootstrap, call `TipsCoach.Ensure( scene )` once. It creates a `ScreenPanel` GameObject
   with the coach and the `TipsDisplay` panel on it.
3. Write your tips. Two ways, and the first is the one to start with:
   - **The [Tips Studio](#tips-studio)**: add the panel, press play, press `T`. You get a live card
     preview, a trigger picker fed from your own input actions, a test-fire button, and a Bake that writes
     a real `.tip` asset. No code, no restarts.
   - **[In code](#authoring-your-catalog)**: `TipsCatalog.Register( myTips )`, for tips whose logic only
     your game knows. The two mix freely; they merge into one catalog by id.

   Skip both and you get the tiny generic `Example` catalog, which is meant to be replaced.
4. For tips that complete on input, a signal, a timer or a world trigger, that is the whole wiring: the
   coach reads input itself and evaluates triggers on its own frame. Only tips whose relevance or
   completion reads live game state (vitals, modal and overlay flags, custom flags and numbers) need the
   per-frame `coach.Tick(ctx)` bridge below. Calling `Tick` every frame is the v0.2 pattern and still works.

See [Triggers](#triggers-retire-tips-with-little-or-no-code) for the near-zero-code path; the sections
after it cover the `Tick`-driven context and signal path for tips that need live game state.

## Tips Studio

New in v0.5. The Studio is an authoring panel that runs inside your game: it lists your tips, edits one,
shows you the real card as you type, fires the tip for real, and writes the `.tip` file at the end. It is a
dev tool, off by default, and it never ships with your game unless you ask it to.

### Turn it on

Add a `TipsStudioPanel` component to a `ScreenPanel` GameObject in your dev scene (the same kind of object
`TipsCoach.Ensure` makes). Then, in play:

- Press `T` to open it, or set `fg_tips_studio 1` in the console.
- Close it with the × in its header, or `fg_tips_studio 0`. The key only OPENS, so pressing `T` inside a
  text box types a `T` and nothing else.

Three properties on the component: `OpenKey` (default `T`, a plain letter because the editor eats F1 to
F12 in play-in-editor), `OpenOnStart` (off, and it is the only thing that decides the boot state, so a
convar someone left set weeks ago cannot open a panel over your game), and `EditorOnly` (on, so the Studio
forces itself shut anywhere but the editor; turn it off if you want it in your own standalone dev build).

### Author a tip

The Studio is one panel in three columns: the catalog on the left, the tip you are writing in the middle,
and what it will look like on the right.

The **left column** lists every tip in the merged catalog, highest priority first, with the source it came
from: `code`, `asset`, `draft` or `example`. Click one to open it, or press New tip. A source you did not
expect is worth a second look; see [The catalog outlives your scene](#the-catalog-outlives-your-scene).

The **middle column** is the editor:

- **Wording**: id, text, pad text and icon. Same markup as everywhere else, `*asterisks*` for a keycap and
  `` `backticks` `` for a pad chip.
- **Order**: priority, a max-seconds safety valve, and prerequisites picked from the ids already in your
  catalog.
- **Completion** and **Relevance**: a kind picker covering every trigger kind, showing only the fields
  that kind reads. Pick `InputAction` and the action list is your project's own input actions, read from
  the engine, not a list the kit made up.
- **Notes**: things worth knowing before you ship the tip. A trigger that can never fire, a `Timer` with
  no seconds, and runs of text long enough to hit the engine's grey-block quirk (a single run longer than
  one card line rasterizes as a solid rectangle; break the sentence or put a chip in it).

The **right column** is the preview. Your tip is drawn twice, once as a keyboard player reads it and once
as a controller player does, so the pad wording and the pad chips are in front of you without owning a
controller. Press "show it" and the card lower left becomes your draft as well: that one is `TipsDisplay`,
the same panel and the same stylesheet your players see, and the keyboard / pad / live chips pin it to one
device.

The cards follow you as you type. The notes refresh when you press Enter in a box or click something, not
on every keystroke: rebuilding the panel would take the cursor out of the box you are typing in.

### Test fire it

"Test fire" makes the draft the live tip under its own id and puts it on screen. From there its completion
is the real thing: do the action in the game and the tip retires, or press "Complete it" to fire the same
path a world trigger uses. Whatever waits on that tip becomes eligible, so a prerequisite chain advances in
front of you.

### Bake it out

The bottom of the right column has two ways to land the tip, because game code cannot write into your
project's `Assets` folder:

- **Copy .tip JSON** puts the file's exact contents on your clipboard, from in game. Paste it into a new
  file under `Assets/` and the editor reads it as a tip.
- **Write to project** stages it in the game's data folder instead. Then, in the editor, run
  **Field Guide / Write staged tips to Assets/tips** and every staged tip becomes a real asset in
  `<your project>/Assets/tips/`. Staging goes through a file, so the play session can be over by the time
  you land the assets. The plain action never overwrites: it skips a tip whose file already exists and
  tells you, and there is a separate **(overwrite existing)** action for when you mean it.

Files land in the CURRENT project's `Assets/tips`. In a game that vendors this kit that is your game's
Assets folder, which is where tips belong. Open the kit's own host project and they land in the host, not
in `Libraries/fieldguide.tips/Assets`.

### Live reload

An edited `.tip` reaches a running session on its own: the resource type tells the catalog when a tip is
loaded or recompiled from disk, and the catalog rebuilds. Edit a tip in the inspector, or in a text editor,
and the change is live. `fg_tips_rebuild` in the console forces a rescan by hand, which is what you want
after deleting a `.tip` file or after a big code hotload.

## Triggers: retire tips with little or no code

New in v0.3. A tip can retire itself from a declarative trigger the coach evaluates for you, so the common
cases need no bridge code. The v0.2 predicate path still works and mixes freely with triggers: a tip
completes as soon as any of its completion paths fires.

### Input completion, no bridge code

Point a tip's `Completion` at an input action and the coach reads the input itself, every frame, from its
own update. You do not call `Tick` for input, key, signal or timer tips.

```csharp
using System.Collections.Generic;
using FieldGuide.Tips;

TipsCatalog.Register( new List<TipDefinition>
{
    new()
    {
        Id = "jump", Icon = "🕹️", Priority = 100,
        Text = "Press *Space* or `A` to jump.",
        Completion = TipTrigger.Action( "Jump" ),   // completes on the Jump input action
    },
    new()
    {
        Id = "sprint", Icon = "🏃", Priority = 90, PrerequisiteTipIds = new[] { "jump" },
        Text = "Hold *Shift* to sprint.",
        Completion = TipTrigger.KeyPress( "shift" ), // a raw key or mouse button by name
    },
} );

TipsCoach.Ensure( scene );   // that is the whole wiring for input tips
```

`TipTrigger.Action( ... )` takes one or more `Input.config` action names and fires when any is pressed, so
one field covers keyboard, mouse and gamepad in whatever the player has bound. `TipTrigger.KeyPress( ... )`
takes raw key or mouse-button names (`"space"`, `"w"`, `"mouse1"`). A gamepad button has no raw read
outside a named action, so bind an action and use `TipTrigger.Action` for pad prompts.

The other kinds: `TipTrigger.Named( "signal" )` (a latched string signal), `TipTrigger.After( seconds )`
(a visible-time timer), and the context kinds that read the `TipContext` you push (`Flag`, `AtLeast`,
`Ever`). Compose them with `TipTrigger.Any( ... )` and `TipTrigger.All( ... )`:

```csharp
Completion = TipTrigger.Any( TipTrigger.Action( "Jump" ), TipTrigger.KeyPress( "space" ) ),
```

Every tip keeps a short readable-window floor: it stays up a beat before it can retire, even when the
player fires the trigger the instant it appears, so a fast press is never lost.

`Relevance` uses the same trigger shape for "may this tip show yet". It narrows the existing `Trigger`
predicate: a tip is relevant when both agree.

### World triggers: "talk to this NPC"

World-anchored completion (walk up to a thing, look at a thing, interact with a thing) rides on a component
you drop on the object, not on the coach's input pass. Add a `TipTriggerObject` to the NPC, door or pickup,
set the tip id and the mode, and it retires the tip when the player does that thing:

```
TipTriggerObject on your NPC
  Tip Id       talk_elder
  Complete On  Interacted
```

The modes are `Interacted` (your code calls `Interacted()` on the component, or the static
`TipTriggerObject.NotifyInteracted( target )`, when the object is used), `PlayerEntered` (the local player
comes within `Radius`), `LookedAt` (the aim ray hits the object within `Radius`), and `Signal` (raise a
named signal instead of completing directly).

`PlayerEntered` and `LookedAt` need to know where the player is and where they aim. A library cannot read
your player, so you set two seams once at bootstrap. They are fail-inert: a mode whose seam is unset never
fires and never throws, so a game that skips them still runs.

```csharp
using Sandbox;
using FieldGuide.Tips;

TipsWorld.LocalPlayerPosition = () => MyLocalPlayer.WorldPosition;
TipsWorld.AimRay = () => new Ray( MyCamera.WorldPosition, MyCamera.WorldRotation.Forward );
```

Interaction is the one case that needs an event, because only your game knows when an interaction
happened. Call the component from wherever you already handle "player used this object":

```csharp
using FieldGuide.Tips;

TipTriggerObject.NotifyInteracted( usedObject );
```

If you also vendor `fieldguide.interaction`, its `PlayerInteraction.InteractionPerformed` event carries the
used object, so the bridge is one line in your own game bootstrap. Game code may reference both kits; the
kits never reference each other.

```csharp
using FieldGuide.Interaction;
using FieldGuide.Tips;

PlayerInteraction.InteractionPerformed += e => TipTriggerObject.NotifyInteracted( e.Target );
```

### Authoring tips as `.tip` assets

A tip can be a `.tip` asset instead of code. Create one in the editor (right-click, Field Guide Tips, Tip)
and fill Id, Text, Icon, Priority and Prerequisites, then pick a Completion kind. The Completion and
Relevance blocks are the same trigger shape as the code path, and the action field is a dropdown of your
game's input actions. `.tip` assets cover the input, key, signal, timer and context kinds; world-anchored
proximity and look-at stay on the `TipTriggerObject` component, which needs a scene object.

Assets and code merge into one catalog by id. When an id collides, the code catalog wins, then the asset,
then a runtime draft, so a game's own `Register` call stays the source of truth and assets add to it. The
kit ships the [demo scene](#demo-scene)'s five tips as assets (`Assets/demo/` and `Assets/starter.tip`),
so the authoring workflow is readable on a fresh clone; they are gated to the demo and safe to delete.
Discovery is automatic through `ResourceLibrary.GetAll`, and so is reload: a `.tip` that is created or
recompiled tells the catalog, which rebuilds, so an edit reaches a running session with no restart.
`fg_tips_rebuild` forces a rescan by hand for the cases nothing announces (a deleted file, a code
hotload); `TipsCatalog.Rebuild()` is the same call from code.

The friendly way to write one of these is the [Tips Studio](#tips-studio), which edits the same fields with
a live card preview and writes the file for you.

### The code escape hatch

The two `Func<TipContext,bool>` predicates (`Trigger`, `CompleteWhen`) cannot serialize, so they stay
code-only, and they stay first-class. When a tip needs game logic no declarative kind covers, register it
in code with a predicate. It sits in the same catalog as your `.tip` assets and completes by the same rule:
whichever path fires first retires the tip. Assets cover the declarative triggers; drop to a code
`TipDefinition` when you need to read something only your game knows.

## Keyboard and controller

New in v0.4. The coach tracks which device the player last used and lets a tip read differently, or complete
differently, on a controller. The device is `Input.UsingController`, which the engine flaps to the last-used
device; the kit reads it directly (a library may read `Input`, the coach already does for its input
triggers), so none of this needs a bridge.

`TipsCoach.ActiveDevice` is the live value (`KeyboardMouse` or `Gamepad`). The display folds it into its
BuildHash, so plugging in a controller or reaching back for the mouse re-renders the active tip at once, with
the right wording and chips.

### Per-device wording

Give a tip a `TextPad` and it shows that instead of `Text` on a controller:

```csharp
new()
{
    Id = "interact", Icon = "💬",
    Text    = "Walk up to someone and press *E* to interact.",
    TextPad = "Walk up to someone and press `X` to interact.",
    CompleteWhen = c => c.EverTalked,
}
```

`TextPad` is optional. Left null (the default) the tip uses `Text` on both devices, so a single line that
names both inputs keeps rendering both chips: `"Attack with *LMB* / \`RT\`."` shows the keycap and the pad
chip side by side on either device, with no auto-stripping. Reach for `TextPad` only when the pad needs
genuinely different words. On the `.tip` asset it is the `TextPad` field next to `Text`.

### Remapping keycaps for a pad without authoring TextPad everywhere

If your only pad difference is the keycaps (the same sentence, `RMB` becomes `LT`), set `TipsCoach.PadLabelFor`
once instead of writing `TextPad` on every tip. It is a per-label map the display applies to keycap chips
while the player is on a pad:

```csharp
TipsCoach.PadLabelFor = label => label switch
{
    "RMB"  => "LT",     // swap the chip to the controller label
    "Tab"  => null,     // no pad equivalent: drop the chip
    _      => label,    // anything else: leave it as it is
};
```

Return a controller label to swap the chip, return the label unchanged to pass it through, or return null or
an empty string to skip a chip that has no pad equivalent. Unset (the default) leaves every keycap as it is.
A remapped label renders as a pad chip; a passed-through one stays a keycap.

### Completing on a stick

A pad action with no digital press (a stick-throttle "accelerate", a look-to-aim beat) completes on
`AnalogAxis`. It fires while the chosen stick is pushed at or past a magnitude from 0 to 1:

```csharp
new()
{
    Id = "drive", Icon = "🏎️",
    Text    = "Hold *W* to accelerate.",
    TextPad = "Push the `Left Stick` forward to accelerate.",
    Completion = TipTrigger.Analog( TipTriggerAnalogSource.AnalogMove, 0.5f ),
}
```

`AnalogMove` reads the movement stick, `AnalogLook` the look stick. It composes under `Any` / `All` like any
other kind, so one tip can retire on either a key or a stick. On the `.tip` asset it is the `AnalogAxis` kind
with an `AnalogSource` and a `Magnitude`.

Raw gamepad buttons still have no public read outside a named action, so for a plain button prompt bind an
input action and complete on `TipTrigger.Action`, which already covers keyboard, mouse and pad in whatever
the player has bound.

### A device-aware "hide tips" chip

The close affordance dismisses the current tip. If your game has a "tips off" preference and wants the card
to advertise the button that toggles it, set the labels and the action. The chip then shows the label that
matches the active device, and clicking it (or the close affordance) runs your action instead of dismissing
one tip:

```csharp
TipsCoach.HideKeyLabel = "Y";                  // shown on keyboard
TipsCoach.HidePadLabel = "B";                  // shown on a pad
TipsCoach.HideAll      = () => MyPrefs.TipsHidden = true;
```

All three are optional and unset by default. What "hide tips" means is yours to define: a library cannot
reference your controls or your saved preferences, so `HideAll` is the seam that carries the intent across.
Leave them unset and the card behaves exactly as before.

## Building the context each frame

`TipContext` is a plain struct of neutral values. You set the live fields; you leave the `Ever*`
fields alone (the coach fills them from latched signals before it evaluates any tip).

Live fields you set every frame:

| Field | Meaning |
| --- | --- |
| `HasPlayer` | A local player exists to coach. False shows nothing. |
| `HasInteractionTarget` | Aiming at something usable right now. |
| `ModalOpen` | A full-screen modal (sheet, inventory) is up. Hides tips. |
| `OverlayOpen` | Any full-screen dev/menu overlay is open. Hides tips, and latches `EverOpenedOverlay`. |
| `DialogueActive` | A conversation is on screen. Hides tips, and latches `EverTalked`. |
| `InCombat` | The player recently dealt or took damage. |
| `EnemiesNearby` | At least one living hostile is close. With `InCombat`, latches `EverAggroed`. |
| `KnownSpellCount` | How many spells the player knows. |
| `ActiveQuestCount` | Active quests. `> 0` latches `EverAcceptedQuest`. |
| `HasActionBarSpell` | At least one action-bar slot holds a spell. |
| `HasUnspentPoint` | An unspent attribute point is available. |
| `HoldingPotion` | Carrying a usable consumable. |
| `HealthFraction`, `ManaFraction`, `StaminaFraction` | Current pools as 0..1 fractions. These replace the game's old direct `StatSheet` reference: compute them off your own stat sheet and push the numbers in. |

`OverlayOpen` is the generalized replacement for the game's old dev-overlay flag: set it true while any
full-screen dev or menu overlay is open, whatever your game calls it.

```csharp
using FieldGuide.Tips;

// A tiny bridge component that stays in your game and reads your local player.
public sealed class TipsBridge : Component
{
    private TipsCoach _coach;

    protected override void OnStart() => _coach = TipsCoach.Ensure( Scene );

    protected override void OnUpdate()
    {
        var sheet = /* your local player's stat sheet */;
        var ctx = new TipContext
        {
            HasPlayer        = sheet is not null,
            ModalOpen        = MyModalRouter.AnyOpen,
            OverlayOpen      = MyDevMenu.IsOpen,
            DialogueActive   = MyDialogue.IsActive,
            InCombat         = sheet?.InCombat ?? false,
            EnemiesNearby    = MyWorld.AnyHostileNear( sheet ),
            ActiveQuestCount = MyQuestLog.ActiveCount,
            HasUnspentPoint  = MyProgression.UnspentPoints > 0,
            HoldingPotion    = MyInventory.HasConsumable,
            HealthFraction   = sheet is null ? 0f : sheet.Health / sheet.MaxHealth,
            // ... the rest of the live fields ...
        };

        _coach.Tick( ctx );
    }
}
```

## Signals: wire your events to the coach

When your game observes a one-off behaviour, call the matching signal. The four named wrappers map to
the events the coach historically subscribed to; the generic `Signal(TipSignal)` covers the rest.

| Your game event | Call |
| --- | --- |
| NPC aggro on the local player | `coach.SignalNpcAggro()` |
| Quest started / accepted | `coach.SignalQuestStarted()` |
| Spell cast | `coach.SignalSpellCast()` |
| Interaction / talked to a character | `coach.SignalInteraction()` |
| Player moved | `coach.Signal( TipSignal.Moved )` |
| Jumped / sprinted | `coach.Signal( TipSignal.Jumped )` / `Signal( TipSignal.Sprinted )` |
| Light / heavy attack | `coach.Signal( TipSignal.LightAttacked )` / `Signal( TipSignal.HeavyAttacked )` |
| Blocked / dodged | `coach.Signal( TipSignal.Blocked )` / `Signal( TipSignal.Dodged )` |
| Opened the character sheet | `coach.Signal( TipSignal.OpenedSheet )` |
| Spent an attribute point | `coach.Signal( TipSignal.SpentPoint )` |
| Used a potion | `coach.Signal( TipSignal.UsedPotion )` |

Signals are idempotent, once latched a behaviour stays latched for the session. `Talked`,
`AcceptedQuest`, `Aggroed`, `OpenedOverlay` and `ClosedPanel` are ALSO auto-latched by the coach from
the neutral context each tick, so if your game only pushes context (no explicit signal) those still
advance. The rest (movement, attacks, spell cast, sheet, point, potion) are signal-only in the kit:
the original game latched them by reading input and game components directly, which a neutral library
cannot do, so you raise the signal when you see the action.

Input-driven behaviours read game-specific input action names and letter keys in the original. In the
kit those live entirely on your side of the bridge: you decide what "moved" or "light attack" means and
raise the signal.

## Authoring your catalog

A tip is a `TipDefinition` record: an id, a one-line `Text`, an optional icon, a `Priority`,
`PrerequisiteTipIds`, a `Trigger` predicate (relevant now?), a `CompleteWhen` predicate (done the
thing?), and an optional `MaxShowSeconds` timeout for "just glance" beats with no behavioural signal.
Predicates read a `TipContext`.

```csharp
using FieldGuide.Tips;

var tips = new List<TipDefinition>
{
    new()
    {
        Id = "move", Icon = "🧭", Priority = 100,
        Text = "Move with *W* *A* *S* *D*. Hold *Shift* to sprint, tap *Space* to jump.",
        CompleteWhen = c => c.EverMoved,
    },
    new()
    {
        Id = "cast", Icon = "✨", Priority = 50, PrerequisiteTipIds = new[] { "move" },
        Trigger = c => c.KnownSpellCount > 0 && c.HasActionBarSpell,
        Text = "Cast a spell from your action bar.",
        CompleteWhen = c => c.EverCastSpell,
    },
};

TipsCatalog.Register( tips );
```

Input markup, two kinds. Wrap a keyboard or mouse name in `*asterisks*` for a square keycap chip, and
a gamepad button in `` `backticks` `` for a rounded controller chip. The two are independent, so one
line can prompt both schemes: `"Attack with *LMB* / \`RT\`."` renders a keycap and a pad chip side by
side. Everything outside the markers is plain text. `Priority` breaks ties when several tips are
eligible; `PrerequisiteTipIds` sequences the spine; a contextual interrupt (a live fight) just uses a
higher `Priority` with a `Trigger`.

The kit ships only `TipsCatalog.Example`, a four-tip generic illustration (a completed-by-behaviour
spine beat, a prerequisite chain, a timed glance beat, and a combat interrupt). It exists so the panel
does something before you wire content in. Register your own catalog to replace it. Your game keeps its
own tip content, including anything specific to your keybinds, menus or dev tools.

### The catalog outlives your scene

`TipsCatalog` is static, so a catalog registered from a scene component survives that scene, that play
session, and the scene you load next. It bites like this: a bootstrap registers three high-priority tips,
you load a different scene, and the coach still picks the highest-priority tip it can see. Nothing over
there can retire it, so an unretireable card sits on screen and the real tips never get a turn.

Register once from a component that lives for the whole game and this never comes up. Register from
anything that can be destroyed, and use `RegisterScoped`, which hands the previous catalog back:

```csharp
public sealed class MyTipsBootstrap : Component
{
    private IDisposable _tips;

    protected override void OnStart()
    {
        _tips = TipsCatalog.RegisterScoped( MyTips.All );
        TipsCoach.Ensure( Scene );
    }

    protected override void OnDestroy()
    {
        _tips?.Dispose();
        _tips = null;
    }
}
```

Disposing twice does nothing, and disposing after someone else has registered leaves their catalog alone.
The same rule covers runtime drafts: whoever calls `RegisterRuntime` calls `UnregisterRuntime`, or
`ClearRuntime` for all of them at once (the Tips Studio does this when it shuts down). `TipsCatalog.Reset`
clears code tips AND drafts, so it is the blunt version, not the tidy one.

If a tip you do not recognise is on screen, `fg_tips_list` prints every tip with the source it resolved
from, and the Tips Studio's tip list shows the same labels. A tip sourced from `code` in a scene that
registers no code tips is the tell.

## Driving a game that isn't an RPG

The named `TipContext` fields (spells, quests, potions, attribute points) come from the kit's first
consumer, an action RPG. A game that has none of those concepts just leaves them at their defaults and
drives the coach through the universal fields (`HasPlayer`, `ModalOpen`, `OverlayOpen`,
`DialogueActive`) plus a genre-neutral escape hatch so you never have to bend your game into RPG
vocabulary:

- Live conditions: `ctx.SetFlag("NearWorkbench", true)` / `ctx.SetNumber("BlocksPlaced", n)`, read in a
  predicate with `c.Flag("NearWorkbench")` / `c.Number("BlocksPlaced")`.
- One-off behaviours: `coach.Signal("PlacedBlock")` latches for the session and reads back with
  `c.Ever("PlacedBlock")`, the string twin of the built-in `Ever*` fields.

```csharp
var tips = new List<TipDefinition>
{
    new()
    {
        Id = "place", Icon = "🧱", Priority = 100,
        Text = "Aim at the ground and press *LMB* or `RT` to place a block.",
        CompleteWhen = c => c.Ever("PlacedBlock"),
    },
    new()
    {
        Id = "stack", Icon = "🏗️", Priority = 90, PrerequisiteTipIds = new[] { "place" },
        Trigger = c => c.Number("BlocksPlaced") >= 1,
        Text = "Nice. Stack a few more to build up.",
        CompleteWhen = c => c.Number("BlocksPlaced") >= 5,
    },
};
TipsCatalog.Register( tips );

// In your per-frame bridge:
ctx.SetNumber( "BlocksPlaced", myWorld.PlacedByLocalPlayer );
_coach.Tick( ctx );
// When the place action fires:
_coach.Signal( "PlacedBlock" );
```

Keys are matched verbatim, so pick stable names once and reuse them across your catalog and bridge.

## Theming

The toast reads its whole palette from a block of SCSS variables at the top of
`TipsDisplay.razor.scss`, marked `THEME TOKENS`. To reskin it, edit that block and nothing else: card
background and border, the left accent stripe, body / kicker / glyph text, and both chip styles
(`$fg-tips-key-*` for keycaps, `$fg-tips-pad-*` for gamepad chips) each have a variable. The rules
below the block reference the variables, so one edit repaints the panel.

The tokens live in this one file on purpose. s&box compiles SCSS per file at runtime, so an `@import`
of a game-side theme file is avoided to keep the kit rendering correctly wherever it is vendored. Keep
tip text bright; dim gray fails over a busy HUD. To move the card, change `left` / `bottom` on `.tip`.

## The display

`TipsDisplay` is a `PanelComponent` mounted on the coach's `ScreenPanel` (via `Ensure`). It is a pure
view: it reads the coach's `Visible`, `ActiveTip`, `ActiveSegments` and `ActivePadSegments` statics,
picks the segment list off `ActiveDevice`, and renders nothing when there is no active tip, so it is
inert until a coach drives it. Tip text is kept bright (never dim gray). The card sits lower-left by
default; move it in `TipsDisplay.razor.scss`.

The panel's namespace is `FieldGuide.Tips`. If you reference the type from C# by bare name, add
`using FieldGuide.Tips;` (the razor markup already declares `@namespace FieldGuide.Tips`).

## Console

- `fg_tips 0` / `fg_tips 1`, turn the coach off / on.
- `fg_tips_reset`, forget all progress and start the walkthrough over.
- `fg_tips_list`, print the active device, then every tip in the merged catalog: id, source (code / asset /
  draft), completed state, priority, prerequisites, and a live relevance verdict (done, active,
  prereqs-pending, trigger-false, relevance-false, or relevant) so a withheld tip is distinguishable from a
  broken bridge.
- `fg_tips_device`, print the active device and how the on-screen tip resolved for it: which text field
  drives the wording (Text or TextPad), the parsed chip segments, the hide-chip label, and whether a pad
  label map or hide action is wired.
- `fg_tips_show <id>`, force a tip on screen for preview.
- `fg_tips_complete <id>`, fire a tip's completion, the same path a world trigger uses.
- `fg_tips_rebuild`, rescan `.tip` assets and rebuild the merged catalog, then print what came back.
  Reload is automatic for an edited or newly created tip; this is the fallback for what nothing announces,
  a deleted file or a code hotload.
- `fg_tips_studio 1` / `fg_tips_studio 0`, open or close the [Tips Studio](#tips-studio) authoring panel.
  Off by default, and it needs a `TipsStudioPanel` in the scene to open onto.

## Save files and migration

Progress persists to `FileSystem.Data/fieldguide_tips.json`. `FileSystem.Data` is already scoped per
game, so two games on one machine that both vendor the kit write to separate folders and never collide.
You rarely need to touch the name.

Two knobs on `TipsCoach` cover the cases where you do:

- `TipsCoach.SaveFileName` sets the file name. Assign it before the first `Ensure` / signal call (the
  first load reads it). Use this only if one game hosts several distinct tip tracks that each need their
  own progress.
- `TipsCoach.MigrateProgressFrom("old_name.json")` copies an old progress file forward once, if the
  current file does not exist yet. Call it at bootstrap before `Ensure` so returning players keep their
  completed tips after you adopt the kit or rename the save file. It leaves the old file untouched and
  is safe to call every launch.

```csharp
// Player used your pre-kit tutorial (saved as my_game_tips.json)? Carry it forward once.
TipsCoach.MigrateProgressFrom( "my_game_tips.json" );
TipsCoach.Ensure( scene );
```

## Demo scene

`Assets/scenes/tips_demo.scene` runs the kit end to end with no wiring of your own. Open it, press play,
and the coach walks a five-tip sequence. A stock citizen in a blue jumpsuit stands in for a player: it
slides along the ground on the movement stick or `W` `A` `S` `D`, turns to face where it is going, and
hops on the `Jump` action. An amber marker sits across the yard waiting to be switched on.

| # | The tip says | What retires it |
| --- | --- | --- |
| 1 | Move the citizen with W A S D or the left stick. | the move stick past 0.4, or a raw W/A/S/D press (an `AnyOf` over one `AnalogAxis` and four `Key` triggers) |
| 2 | Press Space or A to jump. | the `Jump` input action, so either device retires it |
| 3 | Walk over to the amber marker. | a `TipTriggerObject` in `PlayerEntered` mode, reading the `TipsWorld.LocalPlayerPosition` seam the demo sets |
| 4 | Stand in the ring and press Space to switch the marker on. | a `TipTriggerObject` in `Interacted` mode; the marker's own code calls `NotifyInteracted` when it is used |
| 5 | That is the loop. Press T for the Tips Studio and edit these very tips. | opening the Studio: the bootstrap latches a `Signal` while it is open, and this tip waits on it |

The walkthrough ends inside the authoring tool. The last tip coaches `T`, the bootstrap builds the Tips
Studio's host at boot so that key works with nothing to set up, and opening the Studio is what retires the
tip: the player finishes the loop looking at the five tips they just walked, ready to edit them. The wiring
is worth copying if your own game wants a tip to retire on one of its own screens, because it is two small
pieces: the bootstrap calls `coach.Signal( "fg_tips_studio_opened" )` while the Studio is open, and
`demo_wrap.tip` carries a `Signal` completion waiting on that name. The kit itself knows nothing about the
Studio's open state. One consequence worth knowing: the Studio forces itself shut outside the editor, so
that last tip is the one beat of the walkthrough that wants an editor session. Everything before it plays
anywhere.

The tips are `.tip` assets (`Assets/demo/` plus `Assets/starter.tip` for the jump beat), so the scene also
shows the no-code authoring path. The wiring lives under `Code/Demo`: `TipsDemoBootstrap` (seams,
`Ensure`, the Studio host, one `Tick` a frame and the signal above), `TipsDemoPawn` (the citizen's
movement), `TipsDemoMarker` (the marker's own state, and the one line that tells the kit it was used) and
`TipsDemoCitizen` (the look). Press `R` to reset progress and replay from the first tip. It is ignored
while the Studio is open, since the key is read raw and an `r` typed into a text box should not restart
the walkthrough under the panel.

The scene authors the pawn as a plain box. `TipsDemoPawn` switches that renderer off at boot and builds
the citizen in code instead, dressing it from the shipped `citizen_clothes` resources and driving the
stock citizen animgraph from the distance it covers each frame. Nothing here ships with the kit: the
model, the clothing and the animgraph all come with the engine, and the scene file on disk still holds
the simple block, which is what the editor viewport shows when nothing is playing.

The demo needs one input action, `Jump`, which the s&box default `Input.config` binds to Space and the
pad A button. Movement and the replay key are read raw, so nothing else has to be bound.

### Keyboard and controller in the demo

Every beat works on both devices. Movement reads `Input.AnalogMove`, which carries the left stick and,
in a project that binds the standard movement actions, the keyboard too; raw W/A/S/D drives the citizen
either way. The jump and switch beats ride the `Jump` action, so whatever it is bound to retires them. Tips 2 and 4 carry a `TextPad`, so picking up a controller mid-sequence re-renders
the card with the pad wording (`A` instead of Space) and reaching back for the keyboard swaps it back;
tip 5 changes its whole line on a pad. Run `fg_tips_device` while a card is up to see which device and
which text field it resolved.

### Deleting the demo

`Code/Demo` and `Assets/demo` are demo-only, and so is `Assets/starter.tip`. Delete all three when you
drop the kit into your own project. Until you do they are inert: every shipped `.tip` gates its
`Relevance` on a context flag (`fg_tips_demo`) that only `TipsDemoBootstrap` sets, so the demo tips merge
into the catalog and never show in your game. The demo also keeps progress in its own file
(`fieldguide_tips_demo.json`), so replaying it never overwrites what your game saves, and it hands the
`TipsWorld` seams and the save-file name back when the scene ends. Completed ids are static for the
session, though, so run `fg_tips_reset` after a demo run before you test your own tips in the same
editor session.

## Notes

- SCSS is compiled by the editor/runtime, not by `dotnet build`, so style errors are invisible to a
  headless build. The panel's tokens are inlined (no `@import` of a game theme file) and there is no
  `border-style` (a parse error that would collapse the panel).
- Everything is client-local. The coach never touches networking or gameplay state; it only reads the
  context you push and the signals you raise.

## Appendix: the `.tip` file format

A `.tip` asset is JSON. The editor writes it from the inspector and the [Tips Studio](#tips-studio) writes
the identical bytes, so you rarely edit it by hand, but the on-disk shape is documented here field for
field (the five demo tips under `Assets/` are live examples). The top-level object mirrors `TipResource`:

| Field | Type | Meaning |
| --- | --- | --- |
| `Id` | string | Stable id, persisted once complete and used as a prerequisite key. Blank falls back to the file name. |
| `Text` | string | The tip line. `*asterisks*` mark a keycap chip, `` `backticks` `` a gamepad chip. |
| `TextPad` | string | Optional gamepad wording, shown instead of `Text` on a controller. Blank uses `Text` on both devices. |
| `Icon` | string | A small emoji glyph beside the text. |
| `Priority` | int | Higher wins when several tips are eligible at once. |
| `PrerequisiteTipIds` | string[] | Ids that must be complete before this tip may show. |
| `Completion` | trigger | When the tip retires (see the trigger object below). |
| `MaxShowSeconds` | float | Safety valve: auto-complete after this many visible seconds. `0` never times out. |
| `Relevance` | trigger | Narrows when the tip may show. `Always` adds no extra gate. |
| `__references`, `__version` | editor | Editor bookkeeping. Leave them as written. |

`Completion` and `Relevance` are each a trigger object with the same shape (the serialized twin of the code
`TipTrigger`). One `Kind` selects which condition it watches, and only the fields that match that `Kind`
are read; the rest stay at their defaults and are ignored:

| Field | Type | Read when `Kind` is |
| --- | --- | --- |
| `Kind` | enum | always. One of `Always`, `InputAction`, `Key`, `Signal`, `Ever`, `Flag`, `AtLeast`, `Timer`, `AnalogAxis`, `AnyOf`, `AllOf`. |
| `Action` | string | `InputAction`. An input-action name (the inspector shows a dropdown of your game's actions). Fires when it is pressed. |
| `Key` | string | `Key`. A raw key or mouse-button name, e.g. `"space"`, `"mouse1"`. |
| `Name` | string | `Signal`, `Ever`, `Flag`, `AtLeast`. The named condition this trigger reads. |
| `Threshold` | float | `AtLeast`. The `Number(Name)` value to reach. |
| `Seconds` | float | `Timer`. Visible seconds before it fires. |
| `AnalogSource` | enum | `AnalogAxis`. `AnalogMove` or `AnalogLook`. |
| `Magnitude` | float | `AnalogAxis`. The stick magnitude (0 to 1) to reach. |
| `Children` | trigger[] | `AnyOf`, `AllOf`. The composed triggers. |

Quirks worth knowing:

- `AnyOf` fires when any child fires; `AllOf` when every child does. An `AllOf` with no children never fires
  (it does not vacuously complete), so an empty composite is a safe "never".
- A `Timer` of `0` fires the instant the readable-window floor passes, so a tip authored with a `Timer` kind
  and no seconds set will retire almost immediately. Set `Seconds`, or use a different kind, unless that is
  what you want.
- The `.tip` asset covers the input, key, signal, timer, context and analog kinds. World-anchored completion
  (proximity, look-at) is not a `.tip` field; it lives on the `TipTriggerObject` component, which needs a
  scene object to anchor to.
- The two arbitrary `Func` predicates (`Trigger`, `CompleteWhen`) cannot serialize, so they are code-only and
  have no `.tip` field.

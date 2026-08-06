# fieldguide.daynight

Version 0.1.0 · MIT · namespace `FieldGuide.DayNight`

A self-contained s&box day/night cycle: a host-authoritative clock, pure sun and sky grading math, and a
deterministic per-day weather roll.

This is a library of SOURCE you vendor into your game's `Libraries/` folder. It references no game code and
no other library. The kit owns the clock and the math; your game owns the scene, the camera, and (by design)
the sky. Almost all of it is pure and testable; the one networked piece is a small, well-marked component.

## What you get

- `DayNightClock`, a host-authoritative clock component. The host advances game-time and publishes it; clients
  observe and extrapolate. Handles pause correctly across the network (see [The pause pin](#the-pause-pin)).
- `SkyGrade`, pure math that turns a game-hour plus weather into a sun rotation and four colour grades (sun
  key, ambient fill, sky tint, envmap tint). It never touches exposure, so night renders genuinely dark under
  a locked-exposure camera instead of fighting it.
- `WeatherRoll`, a pure deterministic per-day weather roll (Clear / Cloudy / Rain) that a host and every client
  compute identically from the shared seed, so weather needs no per-day networking.
- `SkyWeights`, the sky seam: four normalized crossfade weights for any hour. The kit ships no sky shader and
  no sky art on purpose (see [Why no sky shader](#why-no-sky-shader)); you drive your own sky from these.
- `TimeMath`, pure helpers for setting the clock from a UI (day-preserving time set, minute-quantized slider).
- `DayNightPanel` (optional), a dev tuning surface: scrub the clock, change the pace, jump to dawn or dusk,
  pin the weather, hold time, and copy the tuned config as a paste-ready C# block. Toggle with `N`. Delete
  `Code/Ui/` if you would rather drive the clock from your own UI.
- `DayNightDriver` (optional), a convenience component that applies the grade to your DirectionalLight each
  frame. Delete it if you would rather call `SkyGrade.ApplyGradeTo` yourself.
- `RainStreaks` (optional), a cosmetic rain shower that makes the Rain weather visible. Delete the `Weather/`
  folder if you do not want it.
- `DayNightSelfTest`, a pure self-test battery (console `fg_daynight_selftest`) that proves the math.
- `Assets/scenes/daynight_demo.scene`, a runnable demo of all of it (see [The demo](#the-demo)).

## Quickstart

1. Install the kit through the editor's Library Manager (View menu), or drop this folder into your project's
   `Libraries/`.
2. Open the demo scene and press Play. The kit installs under your project's `Libraries/` folder, not your own
   `Assets/`, so it is not where a new scene search starts. Two routes that work: in the Asset Browser, select
   **Everything** and search `daynight_demo`, or walk the sidebar to
   **Libraries > fieldguide.daynight > Assets > scenes**. Double-click `daynight_demo.scene`.
3. Wire your own scene:

```csharp
using FieldGuide.DayNight;

// 1. Add a clock to your session / game-manager GameObject and give it your world seed.
var clock = gameManager.Components.Create<DayNightClock>();
clock.WorldSeed = myWorldSeed;              // so weather rolls the same on every peer
// clock.Config = DayNightConfig.Default;   // tweak the config if you want a different look/pace

// 2. Either add the driver to your DirectionalLight's GameObject...
var driver = sunGameObject.Components.Create<DayNightDriver>();

// 3. ...or drive the look yourself from the clock each frame:
float now = clock.GetTimeHours();
var weather = clock.EffectiveWeather( clock.CurrentDay );
SkyGrade.ApplyGradeTo( sun, sky, env, now, weather, clock.Config );

// 4. Drive your own sky (any way you like) from the weights:
Vector4 skyBlend = SkyWeights.WeightsFor( now, clock.Config );  // x morning, y noon, z evening, w night
```

Single-player just works: with networking inactive the clock is a host-of-one and reads its own field.

### Required Input.config actions

None. The kit binds no input actions at all. The two keys it reads (`N` for the panel, `H` for the demo's hint
card) are RAW keyboard keys through `Input.Keyboard.Pressed`, so they work in a project whose `Input.config`
you have never opened, and they cannot collide with your own action names. Both are letters on purpose: the
s&box editor swallows F1-F12 in play-in-editor, so an F-key toggle looks broken in exactly the place you would
test it.

If you want different keys, they are one line each in `Code/Ui/DayNightPanel.razor` and
`Code/Demo/DayNightHintCard.razor`. Console fallbacks exist for both (see [Console](#console)).

## The demo

`Libraries/fieldguide.daynight/Assets/scenes/daynight_demo.scene` (asset path `scenes/daynight_demo.scene`)
opens on a lit shape cluster with the sun already climbing.

An in-game day takes four real minutes there, so you see a whole cycle without waiting: the sun sweeps, the
shadows swing a quarter turn across the ground, the grade warms into dusk and drops into a genuinely dark
night, and it comes back up. The shapes are chosen for their shadows rather than their looks, because a bare
plane shows almost nothing.

Two cards are up from the first frame. The hint card on the left says what to press and prints the live sky
weights, which is the only way to SEE the sky seam in a kit that ships no sky art: the four numbers hand off
from one slot to the next as the clock runs. The time panel on the right is the kit's own dev surface, opened
here because driving the cycle is what the scene is for. `N` and the header `×` close it, `H` hides the card.

Everything in the scene ships with the engine: the dev primitives, the default material, the stock skybox.
The kit adds no art of its own.

Demo content is inert by construction, not by instruction. The kit ships no scanned GameResource, so there is
nothing that can load itself into your game, and the demo's UI is gated on a flag only
`DayNightDemoBootstrap` sets. Deleting `Code/Demo/` and `Assets/scenes/` is still the tidy thing to do; it is
just not a safety requirement.

## The time panel

`DayNightPanel` is a `PanelComponent`. Put it on a GameObject with a `ScreenPanel` and press `N`.

It starts CLOSED unless you set `OpenOnStart`, and that property is the only thing that decides the boot
state. The `daynight_panel` convar is a fallback toggle, and s&box persists convars across sessions, so a
value someone left set weeks ago must never be able to open a tuning panel over a shipped game. The panel
logs when it forces a stale value closed.

What it writes, and how:

- Time of day and the four jump chips go through `DayNightClock.SetTimeOfDay`, which preserves the day index,
  so scrubbing inside a day never re-rolls that day's weather. The slider tops out at 23:59 rather than 24:00,
  because hour 24 IS the next day's midnight.
- Day length writes `DayNightConfig.DayLengthMinutes` onto the clock AND every `DayNightDriver` in the scene,
  because the docs tell you to keep those two configs identical and a panel that quietly desynchronised them
  would be the exact bug.
- Weather pins through `SetWeatherOverride`. The fourth segment, `auto`, writes -1 and hands the day back to
  the deterministic roll.
- Copy config puts a paste-ready block on your clipboard: `DayNightConfig.Default` plus the fields you moved.

On a CLIENT every clock setter is a quiet no-op (the host owns the clock), so the panel says so in a note
instead of letting your drag fail silently. The two config writes are guarded in the panel itself, because
config is authoring data and is not replicated: a client that changed its own day length would extrapolate at
a different pace than the host.

## The clock

`DayNightClock` is the only networked type in the kit. The host owns the clock; it accumulates game-time each
fixed tick and publishes three `[Sync(SyncFlags.FromHost)]` fields (`NetTimeOfDay`, `NetTimePaused`,
`NetWeatherOverride`) plus the replicated `WorldSeed`. Clients never write those; they observe and extrapolate
locally so the sun advances smoothly between network snapshots.

Read the clock with `GetTimeHours()` (total game-hours since world start; day index is `floor / 24`,
hour-of-day is the remainder). Write it, host-side, with:

- `SetTimeOfDay(hourOfDay)`, jump to an hour while keeping the current day (so weather does not re-roll).
- `NudgeTime(deltaHours)`, offset the clock.
- `SetPaused(bool)` / `TimePaused`, hold or resume the clock.
- `SetWeatherOverride(int)`, force a `WeatherKind` (or pass -1 to return to the deterministic roll).

Every setter is authority-guarded: a client call is a quiet no-op, because the host's `FromHost` snapshot
would overwrite a client write anyway. Route your UI (a Tab-menu time slider, a debug console) through these.

### The pause pin

This is the one piece of hard-won networking worth understanding, and the reason there are TWO time-related
fields on the wire instead of one.

A paused host STOPS writing `NetTimeOfDay`. A client only corrects its extrapolated clock toward the host
snapshot when that value CHANGES. So with only a time field replicated, a paused host would leave every client
free-running forever: the host sits frozen at noon while each client cycles through a whole day. That was a
real defect.

The fix is `NetTimePaused`, published on the same `FromHost` surface. While the host reports paused, a client
pins its local clock to the snapshot and skips the advance. The instant the host unpauses, extrapolation
resumes and re-locks onto the moving snapshot. Keep both fields together; this is the hardening, do not drop
one when you trim the component.

## The grade math

`SkyGrade.ComputeGrade(total, weather, config, out sunRot, out sunColor, out skyColor, out skyTint, out envTint)`
is pure: same inputs, same outputs, no engine state. `ApplyGradeTo(...)` is the only method that writes engine
state (it calls `ComputeGrade` then assigns the sun/sky/envmap). Pass null for a sky or envmap you do not have.

Two properties make the cycle feel right and stay verifiable:

- Anchor-exact. At `config.AnchorHours` under Clear weather, the computed grade equals the reference colours in
  the config exactly, so pinning that hour reproduces an authored look byte-for-byte. The whole arc is derived
  from `config.SunDirection`, so nudging that one vector moves sunrise, noon, and sunset together.
- Twilight continuity. Dusk and dawn smoothstep-blend from the horizon-edge look to the night grade over
  `config.TwilightHours`, so the sun reads as continuing its arc below the horizon instead of snapping.

`SkyGrade.ClockRateScale(hourOfDay, config)` is the non-uniform pace: the daylight arc runs slower
(`config.DayRateScale`) while the night runs at rate 1, so the night keeps its real-time length exactly and the
day lasts longer. The default scale is solved so the effective day:night real-time ratio is 3.0. If you change
the daylight window or the twilight width, re-solve the scale against your target (the self-test's
`rate_ratio_is_3x` case is how you check it).

## Deterministic weather

`WeatherRoll.For(seed, dayIndex)` hashes (seed, day) with FNV-1a and buckets it into Clear (about 60%),
Cloudy (about 25%), or Rain (about 15%). It uses no `DateTime` and no `System.Random`, so a host and every
client roll the same weather for a day from the shared seed alone, with no per-day network traffic. The roll
never flaps mid-day because it is a pure function of the day index.

`DayNightClock.EffectiveWeather(day)` returns the host override when one is set, otherwise this roll.

## Why no sky shader

World Builder renders its sky with a custom four-slot equirectangular blend shader that keeps four skies
resident and crossfades them by material weights. That shader and its four sky textures are flagship game
material, and the shader path carries a first-touch compile gotcha (the compiled `.shader_c` has to be committed
or the first load races). Shipping it would drag heavy art and a fragile asset path into a library that is
supposed to compile clean in an empty project.

So the kit stops at the seam. `SkyWeights.WeightsFor(hour, config)` gives you four normalized weights (morning,
noon, evening, night) that sum to 1 with only the adjacent pair non-zero. What you do with them is yours:

- Feed them to your own four-sky blend shader (push the `Vector4` into a material parameter).
- Lerp four flat sky colours for a stylized look.
- Swap `SkyBox2D` materials at the dominant slot.
- Ignore them and read `GetTimeHours()` for anything else time-driven (ambience, spawns, NPC schedules).

Feed `WeightsFor` the SAME hour you feed the grade (`GetTimeHours()`) and your sky can never disagree with your
sun. Because the weights are a stateless pure function of the hour, an explicit time jump lands the exact target
weights the same frame (an instant snap), while natural clock advance crossfades smoothly. Both fall out of
purity; do not add smoothing on top.

## Config over constants

Every tuning value lives in `DayNightConfig`, a struct you pass into the math. Grab `DayNightConfig.Default`
(the reference grade: afternoon anchor, symmetric 12-hour day, warm HDR daylight, deep-blue night) and change
the fields you care about. The library never reads a project-global static for tuning, so two games that vendor
the kit can run completely different looks. Set the same config on the clock and the driver, and set it
identically on every networked peer (it is authoring data, not replicated session state).

Exposure is deliberately not in the config. The whole diurnal look comes from sun and sky colours, never from
tone-mapping, so if your game locks camera exposure the kit will not fight it.

## Seams (how it talks back to your game)

A library cannot read game state, so everything the kit needs from you is a small, explicit seam:

| Seam | Type | Purpose |
| --- | --- | --- |
| `DayNightClock.WorldSeed` | `int` | Your per-world seed, so weather rolls the same on every peer. |
| `DayNightClock.Config` / `DayNightDriver.Config` | `DayNightConfig` | All tuning, passed in, not read from a static. |
| `DayNightDriver.ShowCycle` | `Func<bool>` | Return false to hold a stable anchor look (for example while the local player is in a menu or god-camera mode); the clock keeps ticking underneath. Null means always show the cycle. |
| `DayNightDriver.AuthoringSkyTint` | `Color?` | The sky tint to force while `ShowCycle` is false. |
| `RainStreaks.Center` | `Func<Vector3>` | Where the rain shower centres (usually your local player or camera). |
| `SkyWeights.WeightsFor` output | `Vector4` | The sky seam: you drive your own sky from these weights. |

## What is in the folder

- `Code/` : the clock, the pure math (`SkyGrade`, `SkyWeights`, `TimeMath`, `WeatherRoll`) and the config.
- `Code/Ui/` : `DayNightPanel`, the optional dev tuning surface. Delete the folder if you do not want it.
- `Code/Weather/` : `RainStreaks`, the optional cosmetic shower. Delete the folder if you do not want it.
- `Code/SelfTest/` : the pure battery behind `fg_daynight_selftest`.
- `Code/Demo/` : `DayNightDemoBootstrap` and `DayNightHintCard`, which wire the demo scene in code. Delete
  this folder in your project.
- `Assets/scenes/daynight_demo.scene` : the runnable demo (installs under `Libraries/fieldguide.daynight/`,
  see the Quickstart for how to find it).

## Console

- `fg_daynight_selftest`, run the pure self-test battery and print one line per case plus a summary.
- `daynight_panel 1` / `0`, open or close the time panel (same as `N`).
- `daynight_hint 1` / `0`, show or hide the demo's key card (same as `H`).

## Determinism

The weather roll and all the grade/rate/weight math are pure: no `DateTime.Now`, no `System.Random`, no engine
state. The optional `RainStreaks` FX uses an xorshift for cosmetic jitter only (never `System.Random`), so even
the disposable visual noise stays off the determinism path. This is what lets a host and every client agree on
time, weather, and lighting from the replicated clock and seed alone.

## Notes

- The clock is the only networked type. Everything else is pure and unit-testable, which is why the self-test
  is meaningful without the editor.
- The kit never writes exposure, shadows, or fog. It owns sun rotation and colour, sky tint, ambient fill, and
  envmap tint, and nothing else in your lighting setup.

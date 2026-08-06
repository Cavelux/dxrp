using SboxDirector;

namespace AdaptiveDirectorExamples;

/// <summary>A local example. A network game sends MusicDecisionBatch to the owning client before applying it to MusicRuntime.</summary>
public sealed class MusicDirectorExample
{
    readonly MusicDirectorCore _authority;
    readonly MusicRuntime _runtime;
    readonly IMusicAudioSink _audio;

    public MusicDirectorExample(string participantId, IMusicAudioSink audio)
    {
        _audio = audio;
        var cues = new MusicCueConfiguration
        {
            CombatIntroEvents = new[] { "mygame.combat.intro" },
            CombatSecondaryEvent = "mygame.combat.secondary",
            CombatCloseEvent = "mygame.combat.close",
            CombatTrackIds = new[] { "combat", "combat_secondary", "combat_close" },
            BossApproachingEvent = "mygame.boss.approach",
            BossTrackId = "boss",
            SafeAtmosphereEvent = "mygame.atmosphere.safe",
            DangerAtmosphereEvent = "mygame.atmosphere.danger",
            PositionalStingerEvent = ""
        };
        _authority = new MusicDirectorCore(RecoveredMusicDirectorDefaults.CreateConfiguration(cues, seed: 123));
        _runtime = new MusicRuntime(participantId, new MusicEventCatalog(new[]
        {
            Event("mygame.combat.intro", "combat", MusicPriority.Critical),
            Event("mygame.combat.secondary", "combat_secondary", MusicPriority.Critical),
            Event("mygame.combat.close", "combat_close", MusicPriority.Critical),
            Event("mygame.boss.approach", "boss", MusicPriority.Critical, blocks: new[] { "combat", "combat_secondary", "combat_close" }),
            Event("mygame.atmosphere.safe", "ambient", MusicPriority.Low),
            Event("mygame.atmosphere.danger", "ambient", MusicPriority.Medium)
        }));
    }

    public MusicHostRunResult Tick(MusicWorldSnapshot world, IReadOnlyList<MusicGameplayEvent>? events = null)
    {
        var decisions = _authority.Tick(world, events); // Replicate this batch here when authority and listener are separate.
        var runtime = _runtime.Apply(decisions); foreach (var action in runtime.Actions) _audio.Apply(action); return new(decisions, runtime);
    }

    static MusicEventDefinition Event(string eventId, string trackId, MusicPriority priority, IReadOnlyList<string>? blocks = null) => new() { EventId = eventId, TrackId = trackId, Priority = priority, BlockTrackList = blocks ?? Array.Empty<string>() };
}

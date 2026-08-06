using SboxDirector;

namespace AdaptiveDirectorExamples;

/// <summary>Example IDs only. Replace them with events implemented by your own audio adapter.</summary>
public static class ExampleMusicSetup
{
    public static MusicCueConfiguration CreateCues() => new()
    {
        CombatIntroEvents = new[] { "game.combat.intro" },
        CombatSecondaryEvent = "game.combat.secondary",
        CombatCloseEvent = "game.combat.close",
        CombatTrackIds = new[] { "combat", "combat_secondary", "combat_close" },
        MobbedEvent = "game.mobbed",
        BossApproachingEvent = "game.boss",
        BossTrackId = "boss",
        HazardBurningEvent = "game.hazard.burning",
        HazardAttackEvent = "game.hazard.attack",
        HazardRageEvent = "game.hazard.rage",
        SafeAtmosphereEvent = "game.ambient.safe",
        DangerAtmosphereEvent = "game.ambient.danger",
        PositionalStingerEvent = "game.positional.stinger",
        WanderingHazardEventsLowToHigh = new[] { "game.hazard.anger4", "game.hazard.anger3", "game.hazard.anger2", "game.hazard.anger1" },
        WanderingHazardTrackId = "hazard_wandering",
        AmbientMobEvent = "game.mob.ambient",
        MobSpawnEvent = "game.mob.spawn",
        MobSpawnBehindEvent = "game.mob.spawn_behind",
        LargeAreaRevealEvent = "game.area.reveal"
    };

    public static IReadOnlyList<SpecialMusicAlertConfiguration> CreateSpecialAlerts() => new[]
    {
        new SpecialMusicAlertConfiguration { ArchetypeId = "ambusher", CloseEvent = "game.alert.ambusher.close", MiddleEvent = "game.alert.ambusher", FarEvent = "game.alert.ambusher.far" },
        new SpecialMusicAlertConfiguration { ArchetypeId = "disruptor", CloseEvent = "game.alert.disruptor.close", MiddleEvent = "game.alert.disruptor", FarEvent = "game.alert.disruptor.far" }
    };

    public static MusicEventCatalog CreateCatalog()
    {
        var events = new List<MusicEventDefinition>
        {
            Event("game.combat.intro", "combat", MusicPriority.Critical, autoQueue: "game.combat.loop"),
            Event("game.combat.loop", "combat", MusicPriority.Critical, flags: MusicMasterFlags.PlayToEnd),
            Event("game.combat.secondary", "combat_secondary", MusicPriority.Critical),
            Event("game.combat.close", "combat_close", MusicPriority.Critical),
            Event("game.mobbed", "mob_rules", MusicPriority.High, ducks: new[] { "all" }),
            Event("game.boss", "boss", MusicPriority.Critical, blocks: new[] { "combat", "combat_secondary", "combat_close" }, stops: new[] { "combat", "combat_secondary", "combat_close" }),
            Event("game.hazard.burning", "hazard", MusicPriority.Critical),
            Event("game.hazard.attack", "hazard", MusicPriority.Critical),
            Event("game.hazard.rage", "hazard_rage", MusicPriority.Critical),
            Event("game.ambient.safe", "ambient", MusicPriority.Low),
            Event("game.ambient.danger", "ambient", MusicPriority.Medium),
            Event("game.positional.stinger", "stinger", MusicPriority.Low),
            Event("game.mob.ambient", "mob_signal", MusicPriority.Medium),
            Event("game.mob.spawn", "mob_signal", MusicPriority.High),
            Event("game.mob.spawn_behind", "mob_signal", MusicPriority.High),
            Event("game.area.reveal", "stinger", MusicPriority.Medium),
            Event("game.player.death", "death", MusicPriority.Critical, flags: MusicMasterFlags.AllowAfterDeath),
            new MusicEventDefinition { EventId = "game.rhythm.master", TrackId = "rhythm", Priority = MusicPriority.High, TimingTags = new[] { new MusicTimingTag(0, 0), new MusicTimingTag(1, 2), new MusicTimingTag(2, 4) }, LoopSeconds = 8 },
            new MusicEventDefinition { EventId = "game.rhythm.accent", TrackId = "accent", Priority = MusicPriority.High, SyncTrackId = "rhythm", SyncTagIndex = 2, SyncTagDelaySeconds = .25, SyncTagDelayMultiplier = 1 }
        };
        foreach (var id in new[] { "game.hazard.anger4", "game.hazard.anger3", "game.hazard.anger2", "game.hazard.anger1" }) events.Add(Event(id, "hazard_wandering", MusicPriority.High, flags: MusicMasterFlags.PlaySplit));
        foreach (var archetype in new[] { "ambusher", "disruptor" }) foreach (var suffix in new[] { ".close", "", ".far" }) events.Add(Event($"game.alert.{archetype}{suffix}", $"alert_{archetype}", MusicPriority.Critical));
        return new MusicEventCatalog(events);
    }

    static MusicEventDefinition Event(string eventId, string trackId, MusicPriority priority, IReadOnlyList<string>? blocks = null, IReadOnlyList<string>? ducks = null, IReadOnlyList<string>? stops = null, string? autoQueue = null, MusicMasterFlags flags = MusicMasterFlags.None) => new()
    {
        EventId = eventId,
        TrackId = trackId,
        Priority = priority,
        BlockTrackList = blocks ?? Array.Empty<string>(),
        DuckTrackList = ducks ?? Array.Empty<string>(),
        StopTrackList = stops ?? Array.Empty<string>(),
        AutoQueueEventId = autoQueue,
        Flags = flags,
        DefaultFadeOutSeconds = 1
    };
}

#nullable enable annotations
namespace SboxDirector;

public enum MusicDecisionKind { SetMixerLayer, Play, StopEvent, StopTrack, StopAll, General }
public enum MusicPriority { Low = 1, Medium = 2, High = 3, Critical = 4 }
public enum MusicTriggerKind { Play, StopEvent, StopTrack, StopAll, General, Reset, ScenarioEnded, ParticipantRestored, AmbientMob, MobSpawn, MobSpawnBehind, LargeAreaRevealed, LandmarkRevealed }
public enum MusicRuntimeActionKind { Play, StopEvent, StopTrack, StopAll, SetTrackVolume, SetMixerLayer, General }
public enum MusicDecisionResultKind { Accepted, Deferred, RejectedUnknownEvent, RejectedBlocked, RejectedLifecycle, RejectedPriority, FailedAdapter }

[Flags]
public enum MusicMasterFlags
{
    None = 0,
    PlayToEnd = 1,
    PlaySplit = 2,
    DontDisengage = 4,
    DontEngage = 8,
    AllowAfterDeath = 16,
    AllowAfterScenarioEnd = 32
}

public static class MusicMixerLayers
{
    public const string MobBeating = "mob_beating";
    public const string MobRules = "mob_rules";
    public const string HazardRage = "hazard_rage";
    public const string Combat = "combat";
    public const string CombatSecondary = "combat_secondary";
    public const string CombatClose = "combat_close";
    public const string Adrenaline = "adrenaline";
    public const string Checkpoint = "checkpoint";
    public const string Ambient = "ambient";
}

public sealed record SpecialMusicThreat(string ArchetypeId, float Distance, string EmitterId = "", DirectorVector? Position = null);
public sealed record PositionalMusicCandidate(string Id, DirectorVector Position, bool Eligible = true);
public sealed record WanderingMusicHazard(string Id, float Anger, DirectorVector Position);

public sealed class MusicParticipantSnapshot
{
    public string Id { get; init; } = "";
    public bool IsEligible { get; init; } = true;
    public bool IsAlive { get; init; } = true;
    public bool IsIncapacitated { get; init; }
    public bool DynamicMusicAllowed { get; init; } = true;
    public float CumulativeDamage { get; init; }
    public float WeaponActivity { get; init; }
    public bool InflictedDamage { get; init; }
    public int VisibleCommonThreats { get; init; }
    public int ActiveCommonThreats { get; init; }
    public int ScannedCommonThreats { get; init; }
    public float AttackingCommonFactor { get; init; }
    public float CloseCommonAction { get; init; }
    public float VeryCloseCommonFactor { get; init; }
    public bool MobPressureActive { get; init; }
    public bool GlobalCombatActive { get; init; }
    public bool BossPresent { get; init; }
    public string BossArchetypeId { get; init; } = "";
    public bool HazardPresent { get; init; }
    public bool HazardAttacking { get; init; }
    public bool HazardBurning { get; init; }
    public float HazardRage { get; init; }
    public string HazardEmitterId { get; init; } = "";
    public DirectorVector? HazardPosition { get; init; }
    public float Adrenaline { get; init; }
    public bool InCheckpoint { get; init; }
    public IReadOnlyList<SpecialMusicThreat> SpecialThreats { get; init; } = Array.Empty<SpecialMusicThreat>();
    public IReadOnlyList<PositionalMusicCandidate> PositionalCandidates { get; init; } = Array.Empty<PositionalMusicCandidate>();
    public IReadOnlyList<WanderingMusicHazard> WanderingHazards { get; init; } = Array.Empty<WanderingMusicHazard>();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id)) throw new ArgumentException("A music participant id is required.");
        var values = new[] { CumulativeDamage, WeaponActivity, AttackingCommonFactor, CloseCommonAction, VeryCloseCommonFactor, HazardRage, Adrenaline };
        if (values.Any(x => !float.IsFinite(x)) || CumulativeDamage < 0 || VisibleCommonThreats < 0 || ActiveCommonThreats < 0 || ScannedCommonThreats < 0) throw new ArgumentException("Music participant measurements must be finite and non-negative.");
        if (WeaponActivity is < 0 or > 1 || AttackingCommonFactor is < 0 or > 1 || CloseCommonAction is < 0 or > 1 || VeryCloseCommonFactor is < 0 or > 1 || HazardRage is < 0 or > 1 || Adrenaline is < 0 or > 1) throw new ArgumentException("Normalized music inputs must be in [0,1].");
        if (SpecialThreats.Any(x => string.IsNullOrWhiteSpace(x.ArchetypeId) || !float.IsFinite(x.Distance) || x.Distance < 0)) throw new ArgumentException("Special music threats require an archetype and finite non-negative distance.");
        if (PositionalCandidates.Any(x => string.IsNullOrWhiteSpace(x.Id))) throw new ArgumentException("Positional music candidates require stable ids.");
        if (WanderingHazards.Any(x => string.IsNullOrWhiteSpace(x.Id) || !float.IsFinite(x.Anger) || x.Anger is < 0 or > 1)) throw new ArgumentException("Wandering music hazards require stable ids and anger in [0,1].");
    }
}

public sealed class MusicWorldSnapshot
{
    public double Time { get; init; }
    public IReadOnlyList<MusicParticipantSnapshot> Participants { get; init; } = Array.Empty<MusicParticipantSnapshot>();
    public bool ScenarioEnded { get; init; }

    public void Validate()
    {
        if (!double.IsFinite(Time)) throw new ArgumentException("Music world time must be finite.");
        foreach (var participant in Participants) participant.Validate();
        if (Participants.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != Participants.Count) throw new ArgumentException("Music participant ids must be unique.");
    }
}

public sealed record MusicGameplayEvent(long Sequence, MusicTriggerKind Kind, string ParticipantId = "", string TargetId = "", float FadeSeconds = 0, string EmitterId = "", DirectorVector? Position = null)
{
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
}

public sealed record MusicDecision(long DecisionId, MusicDecisionKind Kind, string ParticipantId, string TargetId, string Reason)
{
    public string LayerId { get; init; } = "";
    public float Value { get; init; }
    public float FadeSeconds { get; init; }
    public float StartOffsetSeconds { get; init; }
    public string EmitterId { get; init; } = "";
    public DirectorVector? Position { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
}

public sealed class MusicDecisionBatch
{
    public double Time { get; init; }
    public List<MusicDecision> Decisions { get; } = new();
}

public sealed record MusicTimingTag(int Index, double OffsetSeconds);

public sealed class MusicEventDefinition
{
    public string EventId { get; init; } = "";
    public string TrackId { get; init; } = "main";
    public MusicPriority Priority { get; init; } = MusicPriority.Low;
    public MusicMasterFlags Flags { get; init; }
    public float DefaultFadeOutSeconds { get; init; }
    public double DelaySeconds { get; init; }
    public string? AutoQueueEventId { get; init; }
    public IReadOnlyList<string> BlockTrackList { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DuckTrackList { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> StopTrackList { get; init; } = Array.Empty<string>();
    public IReadOnlyList<MusicTimingTag> TimingTags { get; init; } = Array.Empty<MusicTimingTag>();
    public double LoopSeconds { get; init; }
    public string? SyncTrackId { get; init; }
    public int? SyncTagIndex { get; init; }
    public double SyncTagDelaySeconds { get; init; }
    public double SyncTagDelayMultiplier { get; init; } = 1;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EventId) || string.IsNullOrWhiteSpace(TrackId)) throw new ArgumentException("Music events require event and track ids.");
        if (!float.IsFinite(DefaultFadeOutSeconds) || DefaultFadeOutSeconds < 0 || !double.IsFinite(DelaySeconds) || DelaySeconds < 0 || !double.IsFinite(LoopSeconds) || LoopSeconds < 0 || !double.IsFinite(SyncTagDelaySeconds) || !double.IsFinite(SyncTagDelayMultiplier) || SyncTagDelayMultiplier < 0) throw new ArgumentException("Music timing values must be finite and non-negative where applicable.");
        if (TimingTags.Any(x => x.Index < 0 || !double.IsFinite(x.OffsetSeconds) || x.OffsetSeconds < 0) || TimingTags.Select(x => x.Index).Distinct().Count() != TimingTags.Count) throw new ArgumentException("Music timing tags require unique non-negative indices and offsets.");
        if (BlockTrackList.Concat(DuckTrackList).Concat(StopTrackList).Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Music track lists cannot contain empty ids.");
    }
}

public interface IMusicEventCatalog
{
    bool TryGet(string eventId, out MusicEventDefinition definition);
}

public sealed class MusicEventCatalog : IMusicEventCatalog
{
    readonly Dictionary<string, MusicEventDefinition> _events;
    public MusicEventCatalog(IEnumerable<MusicEventDefinition> events)
    {
        _events = new(StringComparer.Ordinal);
        foreach (var definition in events) { definition.Validate(); if (!_events.TryAdd(definition.EventId, definition)) throw new ArgumentException($"Duplicate music event '{definition.EventId}'."); }
        foreach (var definition in _events.Values) if (definition.AutoQueueEventId is { Length: > 0 } next && !_events.ContainsKey(next)) throw new ArgumentException($"Music event '{definition.EventId}' queues unknown event '{next}'.");
    }
    public bool TryGet(string eventId, out MusicEventDefinition definition) => _events.TryGetValue(eventId, out definition!);
}

public sealed record MusicRuntimeAction(long ActionId, MusicRuntimeActionKind Kind, string TargetId)
{
    public string EventId { get; init; } = "";
    public string TrackId { get; init; } = "";
    public string LayerId { get; init; } = "";
    public float Value { get; init; }
    public float FadeSeconds { get; init; }
    public float StartOffsetSeconds { get; init; }
    public string EmitterId { get; init; } = "";
    public DirectorVector? Position { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
}

public sealed record MusicDecisionResult(long DecisionId, MusicDecisionResultKind Result, string Detail = "", long InstanceId = 0);

public sealed class MusicRuntimeBatch
{
    public double Time { get; init; }
    public List<MusicRuntimeAction> Actions { get; } = new();
    public List<MusicDecisionResult> Results { get; } = new();
}

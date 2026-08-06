#nullable enable annotations
namespace SboxDirector;

public sealed class MusicRuntimeConfiguration
{
    public string Version { get; init; } = "music-runtime-1.0";
    public float DuckedTrackVolume { get; init; } = .5f;
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Version) || !float.IsFinite(DuckedTrackVolume) || DuckedTrackVolume is < 0 or > 1) throw new ArgumentException("Invalid music runtime configuration.");
    }
}

public sealed record ActiveMusicEventState(long InstanceId, long DecisionId, string EventId, string TrackId, double StartedAt, float StartOffsetSeconds, string EmitterId, DirectorVector? Position);
public sealed record QueuedMusicEventState(long DecisionId, string EventId, double DueAt, float StartOffsetSeconds, string EmitterId, DirectorVector? Position);
public sealed record MusicRuntimeState(string ConfigurationVersion, string ParticipantId, bool HasTicked, double LastTime, long LastDecisionId, long NextActionId, long NextInstanceId, bool ParticipantDead, bool ScenarioEnded, IReadOnlyList<ActiveMusicEventState> Active, IReadOnlyList<QueuedMusicEventState> Queued, IReadOnlyDictionary<string, float> MixerLayers, IReadOnlyDictionary<string, float> TrackVolumes);

public sealed class MusicRuntime
{
    readonly string _participantId;
    readonly IMusicEventCatalog _catalog;
    readonly MusicRuntimeConfiguration _configuration;
    readonly List<ActiveMusicEventState> _active = new();
    readonly List<QueuedMusicEventState> _queued = new();
    readonly Dictionary<string, float> _mixer = new(StringComparer.Ordinal);
    readonly Dictionary<string, float> _trackVolumes = new(StringComparer.Ordinal);
    long _nextActionId = 1, _nextInstanceId = 1;
    long _lastDecisionId;
    double _lastTime;
    bool _hasTicked;
    bool _participantDead, _scenarioEnded;

    public MusicRuntime(string participantId, IMusicEventCatalog catalog, MusicRuntimeConfiguration? configuration = null)
    {
        if (string.IsNullOrWhiteSpace(participantId)) throw new ArgumentException("A runtime participant id is required.");
        _participantId = participantId; _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); _configuration = configuration ?? new(); _configuration.Validate();
    }

    public MusicRuntime(string participantId, IMusicEventCatalog catalog, MusicRuntimeState state, MusicRuntimeConfiguration? configuration = null) : this(participantId, catalog, configuration)
    {
        if (!string.Equals(state.ParticipantId, participantId, StringComparison.Ordinal) || !string.Equals(state.ConfigurationVersion, _configuration.Version, StringComparison.Ordinal)) throw new InvalidOperationException("Music runtime state identity/version does not match this runtime.");
        if (state.NextActionId <= 0 || state.NextInstanceId <= 0 || state.Active is null || state.Queued is null || state.MixerLayers is null || state.TrackVolumes is null) throw new InvalidOperationException("Music runtime state is incomplete.");
        foreach (var item in state.Active) { if (!_catalog.TryGet(item.EventId, out var definition) || !string.Equals(item.TrackId, definition.TrackId, StringComparison.Ordinal)) throw new InvalidOperationException($"Saved active music event '{item.EventId}' is missing or changed in the catalog."); _active.Add(item); }
        foreach (var item in state.Queued) { if (!_catalog.TryGet(item.EventId, out _)) throw new InvalidOperationException($"Saved queued music event '{item.EventId}' is missing from the catalog."); _queued.Add(item); }
        foreach (var pair in state.MixerLayers) _mixer[pair.Key] = pair.Value; foreach (var pair in state.TrackVolumes) _trackVolumes[pair.Key] = pair.Value;
        _nextActionId = state.NextActionId; _nextInstanceId = state.NextInstanceId; _lastDecisionId = state.LastDecisionId; _hasTicked = state.HasTicked; _lastTime = state.LastTime; _participantDead = state.ParticipantDead; _scenarioEnded = state.ScenarioEnded;
    }

    public void SetLifecycle(bool participantDead, bool scenarioEnded) { _participantDead = participantDead; _scenarioEnded = scenarioEnded; }

    public MusicRuntimeBatch Apply(MusicDecisionBatch decisions)
    {
        ValidateTime(decisions.Time);
        var output = new MusicRuntimeBatch { Time = decisions.Time };
        ProcessDue(decisions.Time, output);
        var relevant = decisions.Decisions.Where(x => string.Equals(x.ParticipantId, _participantId, StringComparison.Ordinal)).OrderBy(x => x.DecisionId).ToArray();
        if (relevant.Select(x => x.DecisionId).Distinct().Count() != relevant.Length || relevant.Any(x => x.DecisionId <= 0)) throw new ArgumentException("Music decisions require unique positive ids within a batch.");
        foreach (var decision in relevant) { if (decision.DecisionId <= _lastDecisionId) continue; ApplyDecision(decision, decisions.Time, output); _lastDecisionId = decision.DecisionId; }
        ProcessDue(decisions.Time, output);
        return output;
    }

    public MusicRuntimeBatch Tick(double time)
    {
        ValidateTime(time);
        var output = new MusicRuntimeBatch { Time = time }; ProcessDue(time, output); return output;
    }

    public MusicRuntimeBatch ReportCompleted(long instanceId, double time)
    {
        ValidateTime(time);
        var output = new MusicRuntimeBatch { Time = time };
        var index = _active.FindIndex(x => x.InstanceId == instanceId); if (index < 0) return output;
        var completed = _active[index]; _active.RemoveAt(index);
        if (_catalog.TryGet(completed.EventId, out var definition) && definition.AutoQueueEventId is { Length: > 0 } next) QueueResolved(0, next, time, 0, "", null, output, reportDeferred: false);
        RecomputeDucking(output); ProcessDue(time, output); return output;
    }

    public MusicRuntimeBatch ReportAdapterFailure(long actionId, string detail, double time)
    {
        ValidateTime(time);
        var output = new MusicRuntimeBatch { Time = time }; output.Results.Add(new(actionId, MusicDecisionResultKind.FailedAdapter, detail)); return output;
    }

    void ApplyDecision(MusicDecision decision, double time, MusicRuntimeBatch output)
    {
        switch (decision.Kind)
        {
            case MusicDecisionKind.SetMixerLayer:
                _mixer[decision.LayerId] = Math.Clamp(decision.Value, 0, 1);
                output.Actions.Add(NewAction(MusicRuntimeActionKind.SetMixerLayer, decision.LayerId) with { LayerId = decision.LayerId, Value = Math.Clamp(decision.Value, 0, 1) });
                output.Results.Add(new(decision.DecisionId, MusicDecisionResultKind.Accepted));
                break;
            case MusicDecisionKind.Play:
                QueueResolved(decision.DecisionId, decision.TargetId, time, decision.StartOffsetSeconds, decision.EmitterId, decision.Position, output, reportDeferred: true);
                break;
            case MusicDecisionKind.StopEvent:
                StopMatching(x => string.Equals(x.EventId, decision.TargetId, StringComparison.Ordinal), MusicRuntimeActionKind.StopEvent, decision.TargetId, decision.FadeSeconds, output);
                output.Results.Add(new(decision.DecisionId, MusicDecisionResultKind.Accepted));
                break;
            case MusicDecisionKind.StopTrack:
                StopMatching(x => string.Equals(x.TrackId, decision.TargetId, StringComparison.Ordinal), MusicRuntimeActionKind.StopTrack, decision.TargetId, decision.FadeSeconds, output);
                output.Results.Add(new(decision.DecisionId, MusicDecisionResultKind.Accepted));
                break;
            case MusicDecisionKind.StopAll:
                StopAll(decision.FadeSeconds, output); output.Results.Add(new(decision.DecisionId, MusicDecisionResultKind.Accepted));
                break;
            case MusicDecisionKind.General:
                ApplyGeneral(decision, output);
                break;
        }
    }

    void QueueResolved(long decisionId, string eventId, double time, float startOffset, string emitterId, DirectorVector? position, MusicRuntimeBatch output, bool reportDeferred)
    {
        if (!_catalog.TryGet(eventId, out var definition)) { if (decisionId != 0) output.Results.Add(new(decisionId, MusicDecisionResultKind.RejectedUnknownEvent, eventId)); return; }
        if (_participantDead && !definition.Flags.HasFlag(MusicMasterFlags.AllowAfterDeath) || _scenarioEnded && !definition.Flags.HasFlag(MusicMasterFlags.AllowAfterScenarioEnd)) { if (decisionId != 0) output.Results.Add(new(decisionId, MusicDecisionResultKind.RejectedLifecycle)); return; }
        var due = time + definition.DelaySeconds;
        due = Math.Max(due, ResolveTagTime(definition, time));
        _queued.Add(new(decisionId, eventId, due, startOffset, emitterId, position));
        _queued.Sort((a, b) => { var timeOrder = a.DueAt.CompareTo(b.DueAt); return timeOrder != 0 ? timeOrder : a.DecisionId.CompareTo(b.DecisionId); });
        if (reportDeferred && due > time && decisionId != 0) output.Results.Add(new(decisionId, MusicDecisionResultKind.Deferred, $"due at {due:0.###}"));
    }

    double ResolveTagTime(MusicEventDefinition incoming, double time)
    {
        if (incoming.SyncTrackId is not { Length: > 0 } track || incoming.SyncTagIndex is not { } tagIndex) return time;
        var source = _active.Where(x => string.Equals(x.TrackId, track, StringComparison.Ordinal)).OrderBy(x => x.StartedAt).FirstOrDefault(); if (source is null || !_catalog.TryGet(source.EventId, out var sourceDefinition)) return time;
        var tag = sourceDefinition.TimingTags.FirstOrDefault(x => x.Index == tagIndex); if (tag is null) return time;
        var target = source.StartedAt + tag.OffsetSeconds + incoming.SyncTagDelaySeconds * incoming.SyncTagDelayMultiplier;
        if (target < time && sourceDefinition.LoopSeconds > 0) target += Math.Ceiling((time - target) / sourceDefinition.LoopSeconds) * sourceDefinition.LoopSeconds;
        return Math.Max(time, target);
    }

    void ProcessDue(double time, MusicRuntimeBatch output)
    {
        while (_queued.Count > 0 && _queued[0].DueAt <= time)
        {
            var item = _queued[0]; _queued.RemoveAt(0);
            if (!_catalog.TryGet(item.EventId, out var definition)) { if (item.DecisionId != 0) output.Results.Add(new(item.DecisionId, MusicDecisionResultKind.RejectedUnknownEvent)); continue; }
            Engage(item, definition, time, output);
        }
    }

    void Engage(QueuedMusicEventState queued, MusicEventDefinition incoming, double time, MusicRuntimeBatch output)
    {
        var blocker = _active.FirstOrDefault(x => _catalog.TryGet(x.EventId, out var activeDefinition) && Matches(activeDefinition.BlockTrackList, incoming.TrackId));
        if (blocker is not null) { if (queued.DecisionId != 0) output.Results.Add(new(queued.DecisionId, MusicDecisionResultKind.RejectedBlocked, blocker.EventId)); return; }
        var sameTrack = _active.Where(x => string.Equals(x.TrackId, incoming.TrackId, StringComparison.Ordinal)).ToArray();
        foreach (var active in sameTrack)
        {
            _catalog.TryGet(active.EventId, out var current);
            if (current.Flags.HasFlag(MusicMasterFlags.DontDisengage) || current.Flags.HasFlag(MusicMasterFlags.PlayToEnd) && incoming.Priority <= current.Priority || incoming.Priority < current.Priority)
            {
                if (queued.DecisionId != 0) output.Results.Add(new(queued.DecisionId, MusicDecisionResultKind.RejectedPriority, active.EventId)); return;
            }
        }
        foreach (var pattern in incoming.StopTrackList.Distinct(StringComparer.Ordinal)) StopMatching(x => TrackMatches(pattern, x.TrackId), MusicRuntimeActionKind.StopTrack, pattern, incoming.DefaultFadeOutSeconds, output);
        if (!incoming.Flags.HasFlag(MusicMasterFlags.DontEngage) && !incoming.Flags.HasFlag(MusicMasterFlags.PlaySplit)) foreach (var active in sameTrack) StopMatching(x => x.InstanceId == active.InstanceId, MusicRuntimeActionKind.StopEvent, active.EventId, incoming.DefaultFadeOutSeconds, output);
        var instance = _nextInstanceId++;
        _active.Add(new(instance, queued.DecisionId, incoming.EventId, incoming.TrackId, time, queued.StartOffsetSeconds, queued.EmitterId, queued.Position));
        output.Actions.Add(NewAction(MusicRuntimeActionKind.Play, incoming.EventId) with { EventId = incoming.EventId, TrackId = incoming.TrackId, StartOffsetSeconds = queued.StartOffsetSeconds, EmitterId = queued.EmitterId, Position = queued.Position });
        if (queued.DecisionId != 0) output.Results.Add(new(queued.DecisionId, MusicDecisionResultKind.Accepted, InstanceId: instance));
        RecomputeDucking(output);
    }

    void ApplyGeneral(MusicDecision decision, MusicRuntimeBatch output)
    {
        switch (decision.TargetId)
        {
            case "reset":
                StopAll(0, output); _participantDead = false; _scenarioEnded = false; _mixer.Clear(); output.Results.Add(new(decision.DecisionId, MusicDecisionResultKind.Accepted)); break;
            case "scenario_ended":
                _scenarioEnded = true; output.Results.Add(new(decision.DecisionId, MusicDecisionResultKind.Accepted)); break;
            case "participant_restored":
                StopAll(0, output); _participantDead = false; _scenarioEnded = false; output.Results.Add(new(decision.DecisionId, MusicDecisionResultKind.Accepted)); break;
            default:
                output.Actions.Add(NewAction(MusicRuntimeActionKind.General, decision.TargetId) with { Arguments = decision.Arguments }); output.Results.Add(new(decision.DecisionId, MusicDecisionResultKind.Accepted, decision.TargetId)); break;
        }
    }

    void StopAll(float fade, MusicRuntimeBatch output)
    {
        if (_active.Count > 0 || _queued.Count > 0) output.Actions.Add(NewAction(MusicRuntimeActionKind.StopAll, "all") with { FadeSeconds = fade });
        _active.Clear(); _queued.Clear(); RecomputeDucking(output);
    }

    void StopMatching(Func<ActiveMusicEventState, bool> predicate, MusicRuntimeActionKind actionKind, string target, float fade, MusicRuntimeBatch output)
    {
        var removed = _active.Where(predicate).ToArray();
        foreach (var active in removed) { _active.Remove(active); output.Actions.Add(NewAction(actionKind, target) with { EventId = active.EventId, TrackId = active.TrackId, FadeSeconds = fade }); }
        _queued.RemoveAll(x => _catalog.TryGet(x.EventId, out var definition) && (actionKind == MusicRuntimeActionKind.StopEvent ? string.Equals(x.EventId, target, StringComparison.Ordinal) : TrackMatches(target, definition.TrackId)));
        if (removed.Length > 0) RecomputeDucking(output);
    }

    void RecomputeDucking(MusicRuntimeBatch output)
    {
        var tracks = _active.Select(x => x.TrackId).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        foreach (var track in tracks)
        {
            var ducked = _active.Any(x => !string.Equals(x.TrackId, track, StringComparison.Ordinal) && _catalog.TryGet(x.EventId, out var definition) && Matches(definition.DuckTrackList, track));
            var volume = ducked ? _configuration.DuckedTrackVolume : 1f;
            if (_trackVolumes.TryGetValue(track, out var previous) && MathF.Abs(previous - volume) <= .000001f) continue;
            _trackVolumes[track] = volume; output.Actions.Add(NewAction(MusicRuntimeActionKind.SetTrackVolume, track) with { TrackId = track, Value = volume });
        }
        foreach (var stale in _trackVolumes.Keys.Except(tracks, StringComparer.Ordinal).ToArray()) _trackVolumes.Remove(stale);
    }

    static bool Matches(IReadOnlyList<string> patterns, string track) => patterns.Any(x => TrackMatches(x, track));
    static bool TrackMatches(string pattern, string track) => string.Equals(pattern, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(pattern, track, StringComparison.Ordinal);
    MusicRuntimeAction NewAction(MusicRuntimeActionKind kind, string target) => new(_nextActionId++, kind, target);

    void ValidateTime(double time)
    {
        if (!double.IsFinite(time) || _hasTicked && time < _lastTime) throw new InvalidOperationException("Music runtime time must be finite and monotonic.");
        _hasTicked = true; _lastTime = time;
    }

    public MusicRuntimeState CaptureState() => new(_configuration.Version, _participantId, _hasTicked, _lastTime, _lastDecisionId, _nextActionId, _nextInstanceId, _participantDead, _scenarioEnded, _active.OrderBy(x => x.InstanceId).ToArray(), _queued.OrderBy(x => x.DueAt).ThenBy(x => x.DecisionId).ToArray(), new Dictionary<string, float>(_mixer), new Dictionary<string, float>(_trackVolumes));
    public IReadOnlyList<ActiveMusicEventState> Active => _active.OrderBy(x => x.InstanceId).ToArray();
    public IReadOnlyList<QueuedMusicEventState> Queued => _queued.OrderBy(x => x.DueAt).ThenBy(x => x.DecisionId).ToArray();
}

public interface IMusicAudioSink { void Apply(MusicRuntimeAction action); }

public sealed class DelegateMusicAudioSink : IMusicAudioSink
{
    readonly Action<MusicRuntimeAction> _apply;
    public DelegateMusicAudioSink(Action<MusicRuntimeAction> apply) { _apply = apply ?? throw new ArgumentNullException(nameof(apply)); }
    public void Apply(MusicRuntimeAction action) => _apply(action);
}

public sealed record MusicHostRunResult(MusicDecisionBatch Decisions, MusicRuntimeBatch Runtime);

public sealed class MusicDirectorHostRunner
{
    readonly MusicDirectorCore _director;
    readonly MusicRuntime _runtime;
    readonly IMusicAudioSink _sink;
    public MusicDirectorHostRunner(MusicDirectorCore director, MusicRuntime runtime, IMusicAudioSink sink) { _director = director; _runtime = runtime; _sink = sink; }
    public MusicHostRunResult Update(MusicWorldSnapshot world, IReadOnlyList<MusicGameplayEvent>? events = null)
    {
        var decisions = _director.Tick(world, events); var runtime = _runtime.Apply(decisions); foreach (var action in runtime.Actions) { try { _sink.Apply(action); } catch (Exception error) { runtime.Results.Add(new(0, MusicDecisionResultKind.FailedAdapter, $"action {action.ActionId}: {error.Message}")); } } return new(decisions, runtime);
    }
}

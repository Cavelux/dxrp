#nullable enable annotations
namespace SboxDirector;

public readonly record struct ExtensionState(string PersistenceId, IReadOnlyDictionary<string, string> Values)
{
    public static ExtensionState Empty => new("", new Dictionary<string, string>());
    public bool HasState => !string.IsNullOrWhiteSpace(PersistenceId);
}

public readonly record struct DirectorState(
    string ConfigurationVersion,
    bool HasTicked,
    double LastTickTime,
    TempoState Tempo,
    RandomState Random,
    long NextRequestId,
    IReadOnlyDictionary<string, PressureState> Pressure,
    PopulationLedgerState Population,
    IReadOnlyDictionary<string, string> Schedule,
    ExtensionState Policy,
    ExtensionState PlacementPolicy,
    ExtensionState Composer,
    IReadOnlyList<IssuedRequestState> IssuedRequests,
    IReadOnlyList<RetryRequestState> RetryQueue,
    ResourcePopulationState? Resources,
    DirectorOrchestrationState? Orchestration);

public sealed class DirectorCore
{
    readonly DirectorConfiguration _configuration;
    readonly DeterministicRandom _random;
    readonly TeamPressure _pressure;
    readonly TempoController _tempo;
    readonly PopulationLedger _population;
    readonly PlacementSelector _placement = new();
    readonly IPlacementPolicy _placementPolicy;
    readonly IDirectorPolicy _policy;
    readonly IEncounterSchedule _schedule;
    readonly IEncounterComposer _composer;
    readonly ResourcePopulationController? _resources;
    readonly DirectorOrchestration? _orchestration;
    readonly Dictionary<long, IssuedRequestState> _issuedRequests = new();
    readonly List<RetryRequestState> _retryQueue = new();
    long _nextRequestId = 1;
    double _lastTickTime = double.NegativeInfinity;
    bool _hasTicked;
    readonly bool _alignTempoOnFirstTick;
    public event Action<DirectorDiagnostic>? Diagnostic;

    public DirectorCore(DirectorConfiguration configuration, IDirectorPolicy? policy = null, IEncounterSchedule? schedule = null, IPlacementPolicy? placementPolicy = null, IEncounterComposer? composer = null, ResourcePopulationController? resources = null, DirectorOrchestration? orchestration = null, double? startTime = null)
    {
        _configuration = configuration; configuration.Validate(); _random = new DeterministicRandom(configuration.Seed);
        _pressure = new TeamPressure(configuration.Pressure); _tempo = new TempoController(configuration.Tempo, startTime ?? 0); _alignTempoOnFirstTick = startTime is null;
        _population = new PopulationLedger(configuration.Population); _policy = policy ?? new DefaultDirectorPolicy(); _schedule = schedule ?? new BasicEncounterSchedule(); _placementPolicy = placementPolicy ?? new DefaultPlacementPolicy(); _composer = composer ?? new DefaultEncounterComposer(); _resources = resources; _orchestration = orchestration;
    }
    public DirectorCore(DirectorConfiguration configuration, DirectorState state, IDirectorPolicy? policy = null, IEncounterSchedule? schedule = null, IPlacementPolicy? placementPolicy = null, IEncounterComposer? composer = null, ResourcePopulationController? resources = null, DirectorOrchestration? orchestration = null)
    {
        _configuration = configuration; configuration.Validate();
        if (state.ConfigurationVersion != configuration.Version) throw new InvalidOperationException($"State version '{state.ConfigurationVersion}' does not match configuration version '{configuration.Version}'.");
        _random = new DeterministicRandom(state.Random); _pressure = new TeamPressure(configuration.Pressure); _pressure.Restore(state.Pressure);
        _tempo = new TempoController(configuration.Tempo, state.Tempo); _population = new PopulationLedger(configuration.Population); _population.Restore(state.Population);
        _policy = policy ?? new DefaultDirectorPolicy(); _schedule = schedule ?? new BasicEncounterSchedule(); _placementPolicy = placementPolicy ?? new DefaultPlacementPolicy(); _composer = composer ?? new DefaultEncounterComposer(); _resources = resources; _orchestration = orchestration; _nextRequestId = state.NextRequestId;
        _hasTicked = state.HasTicked; _lastTickTime = state.HasTicked ? state.LastTickTime : double.NegativeInfinity;
        _alignTempoOnFirstTick = !state.HasTicked;
        if (_schedule is IPersistentEncounterSchedule persistent) persistent.RestoreScheduleState(state.Schedule);
        RestorePolicyState(state.Policy);
        RestorePlacementPolicyState(state.PlacementPolicy);
        RestoreComposerState(state.Composer);
        foreach (var request in state.IssuedRequests) _issuedRequests[request.RequestId] = request;
        _retryQueue.AddRange(state.RetryQueue);
        if (state.Resources is { } resourceState)
        {
            if (_resources is null) throw new InvalidOperationException("Saved resource-population state requires a ResourcePopulationController.");
            _resources.RestoreState(resourceState);
        }
        if (state.Orchestration is { } orchestrationState)
        {
            if (_orchestration is null) throw new InvalidOperationException("Saved orchestration state requires a DirectorOrchestration instance constructed from that state.");
            if (_orchestration.CaptureState() != orchestrationState) throw new InvalidOperationException("The supplied DirectorOrchestration was not restored from the saved orchestration state.");
        }
    }

    public void ApplyPressure(PressureEvent input, double time) { if (_hasTicked && time < _lastTickTime) throw new InvalidOperationException("Pressure event time cannot precede the last Director tick."); _pressure.Apply(input, time); EmitDiagnostic(time, "pressure", $"{input.ParticipantId}: {input.Severity} ({input.Reason})"); }
    public void ApplyResult(SpawnRequestResult result)
    {
        if (result.SpawnedCount < 0) throw new ArgumentOutOfRangeException(nameof(result), "Spawned count cannot be negative.");
        if (_issuedRequests.TryGetValue(result.RequestId, out var known))
        {
            if (result.SpawnedCount > known.RequestedCount) throw new ArgumentOutOfRangeException(nameof(result), "Spawned count cannot exceed the request.");
            if (result.Result is RequestResultKind.Rejected or RequestResultKind.Failed && result.SpawnedCount != 0) throw new ArgumentException("Rejected and failed requests cannot report spawned members.", nameof(result));
            if (result.Result == RequestResultKind.Succeeded && result.SpawnedCount < known.RequestedCount) result = result with { Result = RequestResultKind.PartiallySucceeded, Detail = "Host reported success with an incomplete count. " + result.Detail };
        }
        if (result.Result == RequestResultKind.Deferred) { _population.Resolve(result); return; }
        _population.Resolve(result);
        if (_issuedRequests.Remove(result.RequestId, out var issued))
        {
            var retryQueued = QueueRetry(issued, result, _hasTicked ? _lastTickTime : 0);
            _orchestration?.ApplyResult(issued.OrchestrationSource, result, retryQueued);
        }
        EmitDiagnostic(_hasTicked ? _lastTickTime : 0, "result", $"request {result.RequestId}: {result.Result}, spawned={result.SpawnedCount}");
    }
    public void ApplyResourceResult(ResourceSpawnResult result) => _resources?.ApplyResult(result);
    public void StartScenario(double time) { if (_orchestration is null) throw new InvalidOperationException("No Director orchestration was configured."); _orchestration.StartScenario(time); }
    public void FailScenario() => _orchestration?.FailScenario();
    public void StartWaveSequence(double time, int? count = null) { if (_orchestration is null) throw new InvalidOperationException("No Director orchestration was configured."); _orchestration.StartWaves(time, count); }
    public void CancelWaveSequence() => _orchestration?.CancelWaves();
    public bool RemoveParticipant(string participantId) => _pressure.Remove(participantId);
    public IReadOnlyList<long> CancelPendingRequests()
    {
        var cancelled = _population.CancelAll(); _issuedRequests.Clear(); _retryQueue.Clear(); _resources?.CancelPending(); _orchestration?.CancelOutstandingRequests(); foreach (var id in cancelled) EmitDiagnostic(_hasTicked ? _lastTickTime : 0, "request", $"request {id} cancelled"); return cancelled;
    }

    public DirectorDecisionBatch Tick(WorldSnapshot world)
    {
        if (_configuration.ValidateWorldSnapshots) world.Validate();
        if (_hasTicked && world.Time < _lastTickTime) throw new InvalidOperationException("World time cannot move backwards.");
        if (!_hasTicked && _alignTempoOnFirstTick) _tempo.AlignStart(world.Time, world.TeamProgress);
        _lastTickTime = world.Time; _hasTicked = true;
        foreach (var expired in _population.Expire(world.Time))
        {
            if (_issuedRequests.Remove(expired, out var issued))
            {
                var timeout = new SpawnRequestResult(expired, RequestResultKind.Failed, 0, "request timeout");
                var retryQueued = QueueRetry(issued, timeout, world.Time); _orchestration?.ApplyResult(issued.OrchestrationSource, timeout, retryQueued);
            }
            EmitDiagnostic(world.Time, "request", $"request {expired} expired");
        }
        var maximum = _pressure.UpdateAndGetMaximum(world); var transition = _tempo.Update(world, maximum);
        var musicIntensity = CalculateMusicIntensity(maximum); if (_policy is IAdaptiveDirectorPolicy adaptiveMusic) musicIntensity = Math.Clamp(adaptiveMusic.AdjustMusicIntensity(musicIntensity, _tempo.Phase, maximum, world), 0, 1);
        var output = new DirectorDecisionBatch { Time = world.Time, Tempo = _tempo.Phase, TeamPressure = maximum, MusicIntensity = musicIntensity };
        if (transition is { } changed) output.Events.Add(new DirectorEvent("tempo_changed", $"{changed.Previous} -> {changed.Current}: {changed.Reason}", world.Time));
        if (_resources is not null) output.ResourceRequests.AddRange(_resources.Tick(world.Resources, world));
        var orchestration = _orchestration?.Update(world); if (orchestration is not null) output.Events.AddRange(orchestration.Events);
        var requestsUsed = 0;
        if (orchestration?.Intent is { } intent && requestsUsed < _configuration.MaximumRequestsPerTick)
        {
            var created = TryCreateRequest(intent.Kind, world, output, intent.Count, reason: intent.Reason, orchestrationSource: intent.SourceId); _orchestration!.IntentFinalized(intent.SourceId, created); if (created) requestsUsed++;
        }
        while (requestsUsed < _configuration.MaximumRequestsPerTick)
        {
            var retryIndex = _retryQueue.FindIndex(x => x.NotBefore <= world.Time);
            if (retryIndex < 0) break;
            var retry = _retryQueue[retryIndex]; _retryQueue.RemoveAt(retryIndex);
            var retried = TryCreateRequest(retry.Kind, world, output, retry.RequestedCount, retry.Attempt, retry.Composition, $"retry: {retry.Reason}", retry.OrchestrationSource);
            if (!retried) _retryQueue.Add(retry with { NotBefore = world.Time + _configuration.Retry.DelaySeconds });
            if (!retried) break;
            requestsUsed++;
        }
        for (; requestsUsed < _configuration.MaximumRequestsPerTick && orchestration?.SuppressOrdinaryScheduling != true; requestsUsed++)
        {
            var kind = _schedule.SelectEncounter(world, _tempo.Phase, maximum, _population, _random);
            if (kind is null) break;
            var created = TryCreateRequest(kind.Value, world, output);
            if (_schedule is IEncounterScheduleFeedback feedback) feedback.SelectionFinalized(created);
            if (!created) break;
        }
        return output;
    }

    bool TryCreateRequest(EncounterKind kind, WorldSnapshot world, DirectorDecisionBatch output, int? requestedCount = null, int attempt = 1, IReadOnlyList<EncounterMember>? composition = null, string? reason = null, string? orchestrationSource = null)
    {
        if (!_policy.AllowEncounter(kind, world) || !_policy.CanCoexist(kind, world.Population)) return false;
        var proposedCount = requestedCount ?? _schedule.RequestedCount(kind, world); if (_policy is IAdaptiveDirectorPolicy adaptive) proposedCount = adaptive.AdjustEncounterCount(kind, proposedCount, _tempo.Phase, output.TeamPressure, world);
        var limit = _population.ConfiguredLimit(kind); if (_policy is IAdaptivePopulationPolicy adaptivePopulation) limit = Math.Max(0, adaptivePopulation.AdjustPopulationLimit(kind, limit, world));
        var count = Math.Min(proposedCount, _population.Available(kind, world.Population, limit));
        if (count <= 0 || !_configuration.Placement.TryGetValue(kind, out var profile)) return false;
        var placement = _placement.Select(world.SpawnCandidates, profile, _random, world, _placementPolicy); if (placement is null) return false;
        var id = _nextRequestId++; if (!_population.TryReserveWithLimit(id, kind, count, world.Population, limit, world.Time + _configuration.RequestTimeoutSeconds)) return false;
        var members = composition is { Count: > 0 } && composition.Sum(x => x.Count) == count ? composition : _composer.Compose(kind, count, world, _policy, _random);
        if (members.Count == 0 || members.Any(x => string.IsNullOrWhiteSpace(x.Archetype) || x.Count <= 0) || members.Sum(x => x.Count) != count) { _population.Resolve(new(id, RequestResultKind.Failed, 0, "invalid composition")); throw new InvalidOperationException("Encounter composition must contain exactly the requested positive count."); }
        var requestReason = $"tempo={_tempo.Phase}; score={placement.Score:0.###}; fallback={placement.IsFallback}" + (string.IsNullOrWhiteSpace(reason) ? "" : $"; {reason}");
        var request = new SpawnRequest(id, kind, count, placement.Candidate.Id, members[0].Archetype, requestReason) { Composition = members, Attempt = attempt };
        _issuedRequests[id] = new(id, kind, count, attempt, members, orchestrationSource); output.SpawnRequests.Add(request);
        if (kind == EncounterKind.Boss && _policy is IAdaptiveDirectorPolicy musicPolicy && musicPolicy.ShouldPlayBossMusic(kind, world)) output.Events.Add(new("boss_music", members[0].Archetype, world.Time));
        EmitDiagnostic(world.Time, "request", $"request {id}: {kind} x{count} at {placement.Candidate.Id}"); return true;
    }

    bool QueueRetry(IssuedRequestState issued, SpawnRequestResult result, double time)
    {
        var remaining = result.Result == RequestResultKind.PartiallySucceeded ? Math.Max(0, issued.RequestedCount - result.SpawnedCount) : issued.RequestedCount;
        var enabled = result.Result switch { RequestResultKind.PartiallySucceeded => _configuration.Retry.RetryPartialRemainder, RequestResultKind.Rejected => _configuration.Retry.RetryRejected, RequestResultKind.Failed => _configuration.Retry.RetryFailed, _ => false };
        if (!enabled || remaining == 0 || issued.Attempt >= _configuration.Retry.MaximumAttempts) return false;
        var composition = remaining == issued.RequestedCount ? issued.Composition : Array.Empty<EncounterMember>();
        _retryQueue.Add(new(issued.Kind, remaining, issued.Attempt + 1, time + _configuration.Retry.DelaySeconds, composition, result.Detail ?? result.Result.ToString(), issued.OrchestrationSource)); return true;
    }

    public DirectorState CaptureState() => new(
        _configuration.Version,
        _hasTicked,
        _hasTicked ? _lastTickTime : 0,
        _tempo.State,
        _random.State,
        _nextRequestId,
        _pressure.Capture(),
        _population.Capture(),
        (_schedule as IPersistentEncounterSchedule)?.CaptureScheduleState() ?? new Dictionary<string, string>(),
        CapturePolicyState(),
        CapturePlacementPolicyState(),
        CaptureComposerState(),
        _issuedRequests.Values.OrderBy(x => x.RequestId).ToArray(),
        _retryQueue.OrderBy(x => x.NotBefore).ThenBy(x => x.Kind).ToArray(),
        _resources?.CaptureState(),
        _orchestration?.CaptureState());
    public IReadOnlyDictionary<EncounterKind, int> Reservations => _population.Reservations;
    float CalculateMusicIntensity(float pressure) => Math.Clamp(_tempo.Phase switch { TempoPhase.Relax => pressure * .35f, TempoPhase.BuildUp => .25f + pressure * .5f, TempoPhase.SustainPeak => .75f + pressure * .25f, TempoPhase.PeakFade => .25f + pressure * .6f, _ => pressure }, 0f, 1f);
    void EmitDiagnostic(double time, string category, string message) => Diagnostic?.Invoke(new(time, category, message));

    ExtensionState CapturePolicyState() => _policy is IPersistentDirectorPolicy persistent
        ? new(RequirePersistenceId(persistent.PersistenceId, nameof(IPersistentDirectorPolicy)), persistent.CapturePolicyState() ?? new Dictionary<string, string>())
        : ExtensionState.Empty;

    ExtensionState CapturePlacementPolicyState() => _placementPolicy is IPersistentPlacementPolicy persistent
        ? new(RequirePersistenceId(persistent.PersistenceId, nameof(IPersistentPlacementPolicy)), persistent.CapturePlacementState() ?? new Dictionary<string, string>())
        : ExtensionState.Empty;

    ExtensionState CaptureComposerState() => _composer is IPersistentEncounterComposer persistent
        ? new(RequirePersistenceId(persistent.PersistenceId, nameof(IPersistentEncounterComposer)), persistent.CaptureComposerState() ?? new Dictionary<string, string>())
        : ExtensionState.Empty;

    void RestorePolicyState(ExtensionState state)
    {
        if (!state.HasState) return;
        if (_policy is not IPersistentDirectorPolicy persistent) throw new InvalidOperationException($"Saved policy state '{state.PersistenceId}' requires an {nameof(IPersistentDirectorPolicy)} implementation.");
        EnsurePersistenceIdMatches(state.PersistenceId, persistent.PersistenceId, "Director policy");
        persistent.RestorePolicyState(state.Values);
    }

    void RestorePlacementPolicyState(ExtensionState state)
    {
        if (!state.HasState) return;
        if (_placementPolicy is not IPersistentPlacementPolicy persistent) throw new InvalidOperationException($"Saved placement-policy state '{state.PersistenceId}' requires an {nameof(IPersistentPlacementPolicy)} implementation.");
        EnsurePersistenceIdMatches(state.PersistenceId, persistent.PersistenceId, "Placement policy");
        persistent.RestorePlacementState(state.Values);
    }

    void RestoreComposerState(ExtensionState state)
    {
        if (!state.HasState) return;
        if (_composer is not IPersistentEncounterComposer persistent) throw new InvalidOperationException($"Saved composer state '{state.PersistenceId}' requires an {nameof(IPersistentEncounterComposer)} implementation.");
        EnsurePersistenceIdMatches(state.PersistenceId, persistent.PersistenceId, "Encounter composer"); persistent.RestoreComposerState(state.Values);
    }

    static string RequirePersistenceId(string id, string contract) => string.IsNullOrWhiteSpace(id) ? throw new InvalidOperationException($"{contract} requires a non-empty PersistenceId.") : id;
    static void EnsurePersistenceIdMatches(string saved, string current, string label)
    {
        if (!string.Equals(saved, RequirePersistenceId(current, label), StringComparison.Ordinal)) throw new InvalidOperationException($"{label} state '{saved}' cannot be restored into '{current}'.");
    }
}

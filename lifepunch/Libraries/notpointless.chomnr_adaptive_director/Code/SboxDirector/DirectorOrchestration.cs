#nullable enable annotations
namespace SboxDirector;

public sealed class DirectorOrchestrationConfiguration
{
    public string PersistenceId { get; init; } = "orchestration-v1";
    public IReadOnlyList<ScenarioStageDefinition> ScenarioStages { get; init; } = Array.Empty<ScenarioStageDefinition>();
    public WaveSequenceConfiguration Waves { get; init; } = new();
    public EncounterKind WaveEncounterKind { get; init; } = EncounterKind.CommonWave;
    public int WaveEncounterCount { get; init; } = 10;
    public bool SuppressOrdinarySchedulingDuringWaves { get; init; } = true;
    public int Seed { get; init; } = 1;
    public void Validate()
    {
        Waves.Validate(); if (string.IsNullOrWhiteSpace(PersistenceId)) throw new ArgumentException("Orchestration persistence id is required.", nameof(PersistenceId)); if (WaveEncounterCount <= 0) throw new ArgumentOutOfRangeException(nameof(WaveEncounterCount));
        if (ScenarioStages.Any(x => string.IsNullOrWhiteSpace(x.Id) || !double.IsFinite(x.MinimumDuration) || x.MinimumDuration < 0 || x.EncounterCount <= 0) || ScenarioStages.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != ScenarioStages.Count) throw new ArgumentException("Scenario stages require unique ids, finite non-negative durations, and positive encounter counts.");
    }
}

public readonly record struct OrchestrationIntent(EncounterKind Kind, int Count, string Reason, string SourceId);
public sealed record OrchestrationUpdate(OrchestrationIntent? Intent, IReadOnlyList<DirectorEvent> Events, bool SuppressOrdinaryScheduling);
public readonly record struct DirectorOrchestrationState(string ConfigurationId, RandomState Random, ScenarioState Scenario, WaveSequenceState Waves, int IssuedScenarioStage, int NotifiedScenarioStage, bool PendingWaveRequest, bool WaveRequestOutstanding, int OutstandingScenarioStage, string? TerminalFailure);

/// <summary>Composes the recovered panic-wave and finale-style stage responsibilities around the host-neutral core.</summary>
public sealed class DirectorOrchestration
{
    readonly DirectorOrchestrationConfiguration _configuration;
    readonly DeterministicRandom _random;
    readonly ScenarioController _scenario;
    readonly WaveSequenceController _waves;
    int _issuedScenarioStage = -1;
    int _notifiedScenarioStage = -1;
    bool _pendingWaveRequest;
    bool _waveRequestOutstanding;
    int _outstandingScenarioStage = -1;
    string? _terminalFailure;

    public DirectorOrchestration(DirectorOrchestrationConfiguration configuration)
    {
        configuration.Validate(); _configuration = configuration; _random = new(configuration.Seed); _scenario = new(configuration.ScenarioStages); _waves = new(configuration.Waves, _random);
    }

    public DirectorOrchestration(DirectorOrchestrationConfiguration configuration, DirectorOrchestrationState state)
    {
        configuration.Validate(); if (state.ConfigurationId != ConfigurationIdentity(configuration)) throw new InvalidOperationException("Orchestration state does not match the configured definition."); _configuration = configuration; _random = new(state.Random); _scenario = new(configuration.ScenarioStages, state.Scenario); _waves = new(configuration.Waves, _random, state.Waves); _issuedScenarioStage = state.IssuedScenarioStage; _notifiedScenarioStage = state.NotifiedScenarioStage; _pendingWaveRequest = state.PendingWaveRequest; _waveRequestOutstanding = state.WaveRequestOutstanding; _outstandingScenarioStage = state.OutstandingScenarioStage; _terminalFailure = state.TerminalFailure;
    }

    public bool WaveActive => _waves.State is not WaveState.Inactive and not WaveState.Done;
    public ScenarioStatus ScenarioStatus => _scenario.Status;
    public void StartScenario(double time) { RequireFiniteTime(time); _scenario.Start(time); _issuedScenarioStage = -1; _notifiedScenarioStage = -1; _outstandingScenarioStage = -1; _terminalFailure = null; }
    public void FailScenario() => _scenario.Fail();
    public void StartWaves(double time, int? count = null) { RequireFiniteTime(time); _waves.Start(time, count); _pendingWaveRequest = false; _waveRequestOutstanding = false; _terminalFailure = null; }
    public void CancelWaves() { _waves.Cancel(); _pendingWaveRequest = false; _waveRequestOutstanding = false; }

    public OrchestrationUpdate Update(WorldSnapshot world)
    {
        var events = new List<DirectorEvent>(); OrchestrationIntent? intent = null;
        if (_terminalFailure is { } failure) { events.Add(new("orchestration_failed", failure, world.Time)); _terminalFailure = null; }
        if (_pendingWaveRequest) intent = new(_configuration.WaveEncounterKind, _configuration.WaveEncounterCount, "panic wave", "wave");
        else if (WaveActive && !_waveRequestOutstanding)
        {
            var action = _waves.Update(world.Time, world.CombatActive);
            if (action.IssueWave) { _pendingWaveRequest = true; intent = new(_configuration.WaveEncounterKind, _configuration.WaveEncounterCount, action.Reason, "wave"); events.Add(new("wave_issued", action.Reason, world.Time)); }
            if (action.Completed) events.Add(new("wave_sequence_complete", action.Reason, world.Time));
        }

        if (_scenario.Status == ScenarioStatus.Running && _scenario.Current is { } stage)
        {
            if (_notifiedScenarioStage != _scenario.StageIndex)
            {
                events.Add(new("scenario_stage_entered", stage.Id, world.Time)); if (!string.IsNullOrWhiteSpace(stage.MusicCue)) events.Add(new("music_cue", stage.MusicCue!, world.Time)); _notifiedScenarioStage = _scenario.StageIndex;
            }
            if (intent is null && stage.Encounter is { } kind && _issuedScenarioStage != _scenario.StageIndex && _outstandingScenarioStage != _scenario.StageIndex) intent = new(kind, stage.EncounterCount, $"scenario stage {stage.Id}", $"scenario:{_scenario.StageIndex}");
            var encounterReady = stage.Encounter is null || _issuedScenarioStage == _scenario.StageIndex;
            if (encounterReady && _scenario.TryAdvance(world.Time, world.CombatActive))
            {
                events.Add(new("scenario_advanced", _scenario.Status == ScenarioStatus.Complete ? "scenario complete" : $"entered {_scenario.Current?.Id}", world.Time));
                if (_scenario.Status == ScenarioStatus.Running && _scenario.Current?.Encounter is null) _issuedScenarioStage = _scenario.StageIndex;
            }
        }
        return new(intent, events, WaveActive && _configuration.SuppressOrdinarySchedulingDuringWaves);
    }

    public void IntentFinalized(string sourceId, bool created)
    {
        if (!created) return;
        if (sourceId == "wave") { _pendingWaveRequest = false; _waveRequestOutstanding = true; }
        else if (TryScenarioStage(sourceId, out var stage)) _outstandingScenarioStage = stage;
    }

    public void ApplyResult(string? sourceId, SpawnRequestResult result, bool retryQueued)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || result.Result == RequestResultKind.Deferred) return;
        if (result.Result == RequestResultKind.Succeeded)
        {
            if (sourceId == "wave") _waveRequestOutstanding = false;
            else if (TryScenarioStage(sourceId, out var stage)) { _issuedScenarioStage = stage; _outstandingScenarioStage = -1; }
            return;
        }
        if (retryQueued) return;
        if (sourceId == "wave") { _waveRequestOutstanding = false; _waves.Cancel(); }
        else if (TryScenarioStage(sourceId, out _)) { _outstandingScenarioStage = -1; _scenario.Fail(); }
        _terminalFailure = $"{sourceId}: {result.Detail ?? result.Result.ToString()}";
    }

    public void CancelOutstandingRequests()
    {
        if (_waveRequestOutstanding) _pendingWaveRequest = true;
        _waveRequestOutstanding = false; _outstandingScenarioStage = -1;
    }

    public DirectorOrchestrationState CaptureState() => new(ConfigurationIdentity(_configuration), _random.State, _scenario.Snapshot, _waves.Snapshot, _issuedScenarioStage, _notifiedScenarioStage, _pendingWaveRequest, _waveRequestOutstanding, _outstandingScenarioStage, _terminalFailure);
    static bool TryScenarioStage(string sourceId, out int stage)
    {
        stage = -1;
        return sourceId.StartsWith("scenario:", StringComparison.Ordinal) && int.TryParse(sourceId[9..], NumberStyles.Integer, CultureInfo.InvariantCulture, out stage);
    }
    static void RequireFiniteTime(double time) { if (!double.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time)); }
    static string ConfigurationIdentity(DirectorOrchestrationConfiguration configuration)
    {
        var stages = string.Join("|", configuration.ScenarioStages.Select(x => FormattableString.Invariant($"{Encode(x.Id)},{x.MinimumDuration:R},{x.RequiresCombatClear},{x.Encounter},{x.EncounterCount},{Encode(x.MusicCue ?? "")}")));
        var waves = configuration.Waves;
        return FormattableString.Invariant($"{Encode(configuration.PersistenceId)};{configuration.Seed};{configuration.WaveEncounterKind};{configuration.WaveEncounterCount};{configuration.SuppressOrdinarySchedulingDuringWaves};{waves.WaveCount};{waves.InitialDelayMinimum:R};{waves.InitialDelayMaximum:R};{waves.CombatTimeout:R};{waves.PauseMinimum:R};{waves.PauseMaximum:R};{stages}");
    }
    static string Encode(string value) => $"{value.Length}:{value}";
}

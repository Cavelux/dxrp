namespace SboxDirector;

public interface IDirectorPolicy
{
    bool AllowEncounter(EncounterKind kind, WorldSnapshot world);
    string SelectArchetype(EncounterKind kind, WorldSnapshot world, DeterministicRandom random);
    bool CanCoexist(EncounterKind requested, PopulationSnapshot population);
}

/// <summary>Implement this alongside IDirectorPolicy when a custom policy owns mutable deterministic state.</summary>
public interface IPersistentDirectorPolicy
{
    string PersistenceId { get; }
    IReadOnlyDictionary<string, string> CapturePolicyState();
    void RestorePolicyState(IReadOnlyDictionary<string, string> state);
}

/// <summary>Optional mode/difficulty override surface corresponding to recovered scripted Director policy.</summary>
public interface IAdaptiveDirectorPolicy
{
    int AdjustEncounterCount(EncounterKind kind, int proposedCount, TempoPhase tempo, float teamPressure, WorldSnapshot world);
    float AdjustMusicIntensity(float proposedIntensity, TempoPhase tempo, float teamPressure, WorldSnapshot world);
    bool ShouldPlayBossMusic(EncounterKind kind, WorldSnapshot world);
}

public interface IAdaptivePopulationPolicy
{
    int AdjustPopulationLimit(EncounterKind kind, int configuredLimit, WorldSnapshot world);
}

public sealed class DefaultDirectorPolicy : IDirectorPolicy
{
    public bool AllowEncounter(EncounterKind kind, WorldSnapshot world) => true;
    public bool CanCoexist(EncounterKind requested, PopulationSnapshot population) => requested != EncounterKind.Boss || population[EncounterKind.Boss] == 0;
    public string SelectArchetype(EncounterKind kind, WorldSnapshot world, DeterministicRandom random) => kind.ToString().ToLowerInvariant();
}

public interface IEncounterSchedule
{
    EncounterKind? SelectEncounter(WorldSnapshot world, TempoPhase tempo, float teamPressure, PopulationLedger population, DeterministicRandom random);
    int RequestedCount(EncounterKind kind, WorldSnapshot world);
}

public interface IPersistentEncounterSchedule
{
    IReadOnlyDictionary<string, string> CaptureScheduleState();
    void RestoreScheduleState(IReadOnlyDictionary<string, string> state);
}

public interface IEncounterScheduleFeedback
{
    void SelectionFinalized(bool requestCreated);
}

public sealed class BasicEncounterSchedule : IEncounterSchedule, IPersistentEncounterSchedule, IEncounterScheduleFeedback
{
    readonly double _ambientInterval;
    readonly double _activeInterval;
    double _nextEncounterTime;
    double _previousEncounterTime;
    bool _selectionPending;

    public BasicEncounterSchedule(double ambientInterval = 3, double activeInterval = 8)
    {
        if (ambientInterval < 0 || activeInterval < 0) throw new ArgumentOutOfRangeException("Encounter intervals must be non-negative.");
        _ambientInterval = ambientInterval; _activeInterval = activeInterval;
    }

    public EncounterKind? SelectEncounter(WorldSnapshot world, TempoPhase tempo, float teamPressure, PopulationLedger population, DeterministicRandom random)
    {
        if (world.Time < _nextEncounterTime) return null;
        if (tempo == TempoPhase.Relax && population.Available(EncounterKind.Ambient, world.Population) > 0)
        {
            _previousEncounterTime = _nextEncounterTime; _selectionPending = true; _nextEncounterTime = world.Time + _ambientInterval;
            return EncounterKind.Ambient;
        }
        if (tempo is TempoPhase.BuildUp or TempoPhase.SustainPeak)
        {
            if (population.Available(EncounterKind.Special, world.Population) > 0 && random.NextFloat() < 0.25f) { _previousEncounterTime = _nextEncounterTime; _selectionPending = true; _nextEncounterTime = world.Time + _activeInterval; return EncounterKind.Special; }
            if (population.Available(EncounterKind.CommonWave, world.Population) > 0) { _previousEncounterTime = _nextEncounterTime; _selectionPending = true; _nextEncounterTime = world.Time + _activeInterval; return EncounterKind.CommonWave; }
        }
        return null;
    }
    public int RequestedCount(EncounterKind kind, WorldSnapshot world) => kind switch { EncounterKind.Ambient => 1, EncounterKind.Special => 1, EncounterKind.CommonWave => 10, _ => 1 };
    public IReadOnlyDictionary<string, string> CaptureScheduleState() => new Dictionary<string, string> { ["nextEncounterTime"] = _nextEncounterTime.ToString("R", CultureInfo.InvariantCulture) };
    public void RestoreScheduleState(IReadOnlyDictionary<string, string> state)
    {
        if (state.TryGetValue("nextEncounterTime", out var value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) _nextEncounterTime = parsed;
    }
    public void SelectionFinalized(bool requestCreated) { if (_selectionPending && !requestCreated) _nextEncounterTime = _previousEncounterTime; _selectionPending = false; }
}

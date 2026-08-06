#nullable enable annotations
namespace SboxDirector;

public sealed record ScenarioStageDefinition(
    string Id,
    double MinimumDuration = 0,
    bool RequiresCombatClear = false,
    EncounterKind? Encounter = null,
    int EncounterCount = 1,
    string? MusicCue = null);
public readonly record struct ScenarioState(ScenarioStatus Status, int StageIndex, double StageStarted);

public sealed class ScenarioController
{
    readonly IReadOnlyList<ScenarioStageDefinition> _stages;
    public ScenarioStatus Status { get; private set; } = ScenarioStatus.Inactive;
    public int StageIndex { get; private set; } = -1;
    public double StageStarted { get; private set; }
    public ScenarioStageDefinition? Current => StageIndex >= 0 && StageIndex < _stages.Count ? _stages[StageIndex] : null;
    public ScenarioController(IReadOnlyList<ScenarioStageDefinition> stages)
    {
        _stages = stages ?? throw new ArgumentNullException(nameof(stages));
        if (_stages.Any(x => string.IsNullOrWhiteSpace(x.Id) || !double.IsFinite(x.MinimumDuration) || x.MinimumDuration < 0 || x.EncounterCount <= 0) || _stages.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != _stages.Count) throw new ArgumentException("Scenario stages are invalid.", nameof(stages));
    }
    public ScenarioController(IReadOnlyList<ScenarioStageDefinition> stages, ScenarioState state) : this(stages) { Status = state.Status; StageIndex = state.StageIndex; StageStarted = state.StageStarted; if (!double.IsFinite(StageStarted) || StageIndex < -1 || StageIndex > _stages.Count || (Status == ScenarioStatus.Running && Current is null) || (Status == ScenarioStatus.Complete && StageIndex != _stages.Count)) throw new ArgumentException("Scenario state does not match the definition."); }
    public void Start(double time) { RequireTime(time); if (_stages.Count == 0) { Status = ScenarioStatus.Complete; StageIndex = 0; StageStarted = time; return; } Status = ScenarioStatus.Running; StageIndex = 0; StageStarted = time; }
    public bool TryAdvance(double time, bool combatActive)
    {
        RequireTime(time); if (time < StageStarted) throw new InvalidOperationException("Scenario time cannot move backwards.");
        if (Status != ScenarioStatus.Running || Current is null) return false;
        if (time - StageStarted < Current.MinimumDuration || (Current.RequiresCombatClear && combatActive)) return false;
        StageIndex++; StageStarted = time; if (StageIndex >= _stages.Count) Status = ScenarioStatus.Complete; return true;
    }
    public void Fail() { if (Status == ScenarioStatus.Running) Status = ScenarioStatus.Failed; }
    public void Reset() { Status = ScenarioStatus.Inactive; StageIndex = -1; StageStarted = 0; }
    public ScenarioState Snapshot => new(Status, StageIndex, StageStarted);
    static void RequireTime(double time) { if (!double.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time)); }
}

public interface ISessionFlow
{
    string ModeId { get; }
    string? CurrentRoundId { get; }
    bool IsSessionActive { get; }
}

public sealed class ContinuousSessionFlow : ISessionFlow
{
    public string ModeId { get; init; } = "continuous";
    public string? CurrentRoundId => null;
    public bool IsSessionActive { get; set; } = true;
}

public sealed class RoundSessionFlow : ISessionFlow
{
    public string ModeId { get; init; } = "rounds";
    public string? CurrentRoundId { get; private set; }
    public bool IsSessionActive => CurrentRoundId is not null;
    public void BeginRound(string id) { CurrentRoundId = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Round id is required.") : id; }
    public void EndRound() => CurrentRoundId = null;
}

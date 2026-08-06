namespace SboxDirector;

public sealed class WaveSequenceConfiguration
{
    public int WaveCount { get; init; } = 1;
    public double InitialDelayMinimum { get; init; } = 1;
    public double InitialDelayMaximum { get; init; } = 2;
    public double CombatTimeout { get; init; } = 10;
    public double PauseMinimum { get; init; } = 5;
    public double PauseMaximum { get; init; } = 10;
    public void Validate()
    {
        var durations = new[] { InitialDelayMinimum, InitialDelayMaximum, CombatTimeout, PauseMinimum, PauseMaximum };
        if (WaveCount <= 0 || durations.Any(x => !double.IsFinite(x)) || InitialDelayMinimum < 0 || InitialDelayMaximum < InitialDelayMinimum || CombatTimeout < 0 || PauseMinimum < 0 || PauseMaximum < PauseMinimum) throw new ArgumentException("Invalid wave sequence configuration.");
    }
}

public readonly record struct WaveSequenceState(WaveState State, int IssuedWaves, int TargetWaves, double Deadline);
public readonly record struct WaveAction(bool IssueWave, bool Completed, string Reason);

public sealed class WaveSequenceController
{
    readonly WaveSequenceConfiguration _configuration; readonly DeterministicRandom _random;
    public WaveState State { get; private set; } = WaveState.Inactive;
    public int IssuedWaves { get; private set; }
    public int TargetWaves { get; private set; }
    public double Deadline { get; private set; }
    public WaveSequenceController(WaveSequenceConfiguration configuration, DeterministicRandom random) { configuration.Validate(); _configuration = configuration; _random = random; }
    public WaveSequenceController(WaveSequenceConfiguration configuration, DeterministicRandom random, WaveSequenceState state) : this(configuration, random) { State = state.State; IssuedWaves = state.IssuedWaves; TargetWaves = state.TargetWaves; Deadline = state.Deadline; if (!double.IsFinite(Deadline) || IssuedWaves < 0 || TargetWaves < 0 || IssuedWaves > TargetWaves) throw new ArgumentException("Wave state is invalid.", nameof(state)); }
    public WaveSequenceState Snapshot => new(State, IssuedWaves, TargetWaves, Deadline);
    public void Start(double time, int? waveCount = null) { RequireTime(time); TargetWaves = waveCount ?? _configuration.WaveCount; if (TargetWaves <= 0) throw new ArgumentOutOfRangeException(nameof(waveCount)); IssuedWaves = 0; State = WaveState.InitialDelay; Deadline = time + Range(_configuration.InitialDelayMinimum, _configuration.InitialDelayMaximum); }
    public void Cancel() { State = WaveState.Inactive; IssuedWaves = 0; TargetWaves = 0; Deadline = 0; }
    public WaveAction Update(double time, bool relevantCombatantsRemain)
    {
        RequireTime(time);
        if (State is WaveState.Inactive or WaveState.Done) return new(false, State == WaveState.Done, "inactive or complete");
        if (time < Deadline) return new(false, false, "waiting for timer");
        if (State == WaveState.InitialDelay) { State = WaveState.IssueWave; return new(false, false, "initial delay expired"); }
        if (State == WaveState.IssueWave) { IssuedWaves++; State = WaveState.WaitForClear; Deadline = time + _configuration.CombatTimeout; return new(true, false, "wave issued"); }
        if (State == WaveState.WaitForClear)
        {
            if (relevantCombatantsRemain) { Deadline = time + _configuration.CombatTimeout; return new(false, false, "combat remains"); }
            if (IssuedWaves >= TargetWaves) { State = WaveState.Done; return new(false, true, "target waves issued and combat clear"); }
            State = WaveState.Pause; Deadline = time + Range(_configuration.PauseMinimum, _configuration.PauseMaximum); return new(false, false, "pausing before next wave");
        }
        if (State == WaveState.Pause) { State = WaveState.IssueWave; return new(false, false, "pause expired"); }
        return new(false, false, "no action");
    }
    double Range(double minimum, double maximum) => minimum + (maximum - minimum) * _random.NextFloat();
    static void RequireTime(double time) { if (!double.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time)); }
}

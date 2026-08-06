namespace SboxDirector;

public readonly record struct TempoState(TempoPhase Phase, double PhaseStarted, float RelaxStartProgress);
public readonly record struct TempoTransition(TempoPhase Previous, TempoPhase Current, string Reason);

public sealed class TempoController
{
    readonly TempoConfiguration _configuration;
    public TempoPhase Phase { get; private set; } = TempoPhase.Relax;
    public double PhaseStarted { get; private set; }
    public float RelaxStartProgress { get; private set; }

    public TempoController(TempoConfiguration configuration, double startTime = 0, float startProgress = 0)
    { _configuration = configuration; configuration.Validate(); PhaseStarted = startTime; RelaxStartProgress = startProgress; }
    public TempoController(TempoConfiguration configuration, TempoState state) : this(configuration, state.PhaseStarted, state.RelaxStartProgress) { Phase = state.Phase; }
    public TempoState State => new(Phase, PhaseStarted, RelaxStartProgress);
    public void AlignStart(double time, float progress)
    {
        PhaseStarted = time; if (Phase == TempoPhase.Relax) RelaxStartProgress = progress;
    }

    public TempoTransition? Update(WorldSnapshot world, float maximumInstantaneousPressure)
    {
        var elapsed = world.Time - PhaseStarted;
        return Phase switch
        {
            TempoPhase.BuildUp when elapsed >= _configuration.BuildUpMinimumSeconds && maximumInstantaneousPressure > _configuration.PeakThreshold => Transition(TempoPhase.SustainPeak, world, "buildup timer ready and team pressure crossed peak threshold"),
            TempoPhase.SustainPeak when elapsed >= _configuration.SustainPeakSeconds => Transition(TempoPhase.PeakFade, world, "sustain timer expired"),
            TempoPhase.PeakFade when !world.CombatActive && maximumInstantaneousPressure < _configuration.RelaxThreshold => Transition(TempoPhase.Relax, world, "combat cleared and team pressure fell below relax threshold"),
            TempoPhase.Relax when elapsed >= _configuration.RelaxMaximumSeconds => Transition(TempoPhase.BuildUp, world, "maximum relax time expired"),
            TempoPhase.Relax when elapsed >= _configuration.RelaxMinimumSeconds && world.TeamProgress - RelaxStartProgress >= _configuration.RelaxMaximumProgressTravel => Transition(TempoPhase.BuildUp, world, "team traveled the configured relax distance"),
            _ => null
        };
    }

    TempoTransition Transition(TempoPhase next, WorldSnapshot world, string reason)
    {
        var previous = Phase; Phase = next; PhaseStarted = world.Time;
        if (next == TempoPhase.Relax) RelaxStartProgress = world.TeamProgress;
        return new(previous, next, reason);
    }
}

using SboxDirector;

namespace AdaptiveDirectorExamples;

/// <summary>A thin integration owned by an existing round manager.</summary>
public sealed class RoundBasedExample
{
    readonly RoundSessionFlow _rounds = new();
    readonly DirectorCore _director;
    readonly DirectorHostRunner _runner;

    public RoundBasedExample(Func<WorldSnapshot> captureWorld, Func<SpawnRequest, SpawnRequestResult> executeSpawn)
    {
        var schedule = new CompositeEncounterSchedule(
            new MilestoneEncounterSchedule("round-boss", EncounterKind.Boss, .65f, .85f),
            new RuleBasedEncounterSchedule(new[]
            {
                new EncounterRule { Id = "wave", Kind = EncounterKind.CommonWave, Count = 12, Priority = 10, CooldownSeconds = 12 },
                new EncounterRule { Id = "special", Kind = EncounterKind.Special, Count = 1, Priority = 20, CooldownSeconds = 18, Probability = .4f }
            }));
        _director = new DirectorCore(new DirectorConfiguration { Seed = 123 }, schedule: schedule);
        _runner = new DirectorHostRunner(_director, _rounds, new DelegateWorldSource(captureWorld), new DelegateSpawnExecutor(executeSpawn));
    }

    public void BeginRound(string roundId) => _rounds.BeginRound(roundId);
    public void EndRound() { _runner.Stop(); _rounds.EndRound(); }
    public DirectorRunResult FixedUpdate() => _runner.Update();
    public void ReportDamage(string playerId, double time) => _runner.ReportPressure(new(playerId, PressureSeverity.Moderate, "damage"), time);
}

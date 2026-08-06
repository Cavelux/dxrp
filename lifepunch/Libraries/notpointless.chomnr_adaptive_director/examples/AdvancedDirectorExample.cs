using SboxDirector;

namespace AdaptiveDirectorExamples;

public static class AdvancedDirectorExample
{
    public static DirectorCore Create()
    {
        var schedule = new ConcurrentEncounterSchedule(new[]
        {
            new ScheduleLane { Id = "waves", Priority = 20, Schedule = new RuleBasedEncounterSchedule(new[] { new EncounterRule { Id = "wave", Kind = EncounterKind.CommonWave, Count = 10, CooldownSeconds = 12 } }) },
            new ScheduleLane { Id = "specials", Priority = 10, Schedule = new RuleBasedEncounterSchedule(new[] { new EncounterRule { Id = "special", Kind = EncounterKind.Special, Count = 1, CooldownSeconds = 18, Probability = .4f } }) }
        });
        var composer = new WeightedEncounterComposer(new Dictionary<EncounterKind, IReadOnlyList<WeightedArchetype>>
        {
            [EncounterKind.CommonWave] = new[] { new WeightedArchetype("grunt", 4), new WeightedArchetype("brute", 1) }
        });
        var resources = new ResourcePopulationController(new[] { new ResourceRule { Category = "healing", TargetCount = 3 } });
        var orchestration = new DirectorOrchestration(new DirectorOrchestrationConfiguration
        {
            PersistenceId = "my-game-scenario-v1",
            ScenarioStages = new[] { new ScenarioStageDefinition("horde", Encounter: EncounterKind.CommonWave), new ScenarioStageDefinition("boss", RequiresCombatClear: true, Encounter: EncounterKind.Boss, MusicCue: "boss_intro"), new ScenarioStageDefinition("escape") }
        });
        return new DirectorCore(new DirectorConfiguration { Version = "my-game-v1", MaximumRequestsPerTick = 3 }, schedule: schedule, composer: composer, resources: resources, orchestration: orchestration);
    }
}

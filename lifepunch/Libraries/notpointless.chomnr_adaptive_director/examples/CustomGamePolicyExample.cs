using SboxDirector;

namespace AdaptiveDirectorExamples;

/// <summary>Shows how game-specific enemies and rules stay outside the reusable Director core.</summary>
public static class CustomGamePolicyExample
{
    public static DirectorCore Create(int seed = 77)
    {
        var schedule = new RuleBasedEncounterSchedule(new[]
        {
            new EncounterRule { Id = "ambient", Kind = EncounterKind.Ambient, Count = 2, Priority = 5, CooldownSeconds = 4, Phases = new HashSet<TempoPhase> { TempoPhase.Relax } },
            new EncounterRule { Id = "special", Kind = EncounterKind.Special, Count = 1, Priority = 20, CooldownSeconds = 15 },
            new EncounterRule { Id = "hazard", Kind = EncounterKind.DormantHazard, Count = 1, Priority = 15, CooldownSeconds = 45, AdditionalCondition = world => world.TeamProgress is > .25f and < .8f },
            new EncounterRule { Id = "boss", Kind = EncounterKind.Boss, Count = 1, Priority = 30, CooldownSeconds = 90, MinimumPressure = .4f }
        });
        return new DirectorCore(new DirectorConfiguration { Version = "custom-policy-example-v1", Seed = seed }, policy: new ExamplePolicy(), schedule: schedule);
    }

    sealed class ExamplePolicy : IDirectorPolicy, IAdaptiveDirectorPolicy, IAdaptivePopulationPolicy
    {
        public bool AllowEncounter(EncounterKind kind, WorldSnapshot world) => world.ModeId != "tutorial" || kind is EncounterKind.Ambient or EncounterKind.Objective;

        public bool CanCoexist(EncounterKind requested, PopulationSnapshot population)
        {
            if (requested == EncounterKind.Boss) return population[EncounterKind.Boss] == 0 && population[EncounterKind.DormantHazard] == 0;
            if (requested == EncounterKind.DormantHazard) return population[EncounterKind.Boss] == 0;
            return true;
        }

        public string SelectArchetype(EncounterKind kind, WorldSnapshot world, DeterministicRandom random) => kind switch
        {
            EncounterKind.Ambient => "crawler",
            EncounterKind.Special => new[] { "disruptor", "ambusher", "support" }[random.NextInt(3)],
            EncounterKind.Boss => world.ModeId == "hard" ? "siege_colossus" : "brute",
            EncounterKind.DormantHazard => "sleeping_guardian",
            _ => kind.ToString().ToLowerInvariant()
        };

        public int AdjustEncounterCount(EncounterKind kind, int proposedCount, TempoPhase tempo, float teamPressure, WorldSnapshot world) => world.ModeId == "hard" && kind == EncounterKind.CommonWave ? (int)MathF.Ceiling(proposedCount * 1.35f) : proposedCount;
        public float AdjustMusicIntensity(float proposedIntensity, TempoPhase tempo, float teamPressure, WorldSnapshot world) => proposedIntensity;
        public bool ShouldPlayBossMusic(EncounterKind kind, WorldSnapshot world) => kind == EncounterKind.Boss;
        public int AdjustPopulationLimit(EncounterKind kind, int configuredLimit, WorldSnapshot world) => world.ModeId == "solo" && kind == EncounterKind.Special ? Math.Min(configuredLimit, 2) : configuredLimit;
    }
}

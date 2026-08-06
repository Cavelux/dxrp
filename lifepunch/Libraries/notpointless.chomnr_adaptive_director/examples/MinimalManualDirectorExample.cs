using SboxDirector;

namespace AdaptiveDirectorExamples;

/// <summary>The smallest integration when your game manager already owns the update loop.</summary>
public sealed class MinimalManualDirectorExample
{
    readonly DirectorCore _director = new(new DirectorConfiguration
    {
        Version = "minimal-example-v1",
        Seed = 1234
    });

    public DirectorDecisionBatch Tick(WorldSnapshot world, Func<SpawnRequest, int> spawn)
    {
        var decisions = _director.Tick(world);
        foreach (var request in decisions.SpawnRequests)
        {
            var spawned = Math.Clamp(spawn(request), 0, request.RequestedCount);
            var result = spawned == request.RequestedCount
                ? RequestResultKind.Succeeded
                : spawned > 0 ? RequestResultKind.PartiallySucceeded : RequestResultKind.Failed;
            _director.ApplyResult(new SpawnRequestResult(request.RequestId, result, spawned));
        }
        return decisions;
    }

    public void ReportDamage(string playerId, float fractionOfHealth, double time)
    {
        var severity = fractionOfHealth switch
        {
            < .20f => PressureSeverity.Moderate,
            < .50f => PressureSeverity.Major,
            _ => PressureSeverity.Critical
        };
        _director.ApplyPressure(new PressureEvent(playerId, severity, "damage"), time);
    }
}

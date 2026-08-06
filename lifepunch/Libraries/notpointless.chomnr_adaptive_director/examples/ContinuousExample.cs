using SboxDirector;

namespace AdaptiveDirectorExamples;

/// <summary>An always-active campaign/extraction integration with save and restore.</summary>
public sealed class ContinuousExample
{
    readonly ContinuousSessionFlow _session = new();
    DirectorCore _director;
    DirectorHostRunner _runner;
    readonly Func<WorldSnapshot> _capture;
    readonly Func<SpawnRequest, SpawnRequestResult> _spawn;

    public ContinuousExample(Func<WorldSnapshot> capture, Func<SpawnRequest, SpawnRequestResult> spawn)
    {
        _capture = capture; _spawn = spawn; _director = new DirectorCore(Configuration()); _runner = BuildRunner();
    }

    public DirectorRunResult FixedUpdate() => _runner.Update();
    public string Save() => DirectorStateCodec.ToJson(_director.CaptureState());
    public void Load(string json) { _director = new DirectorCore(Configuration(), DirectorStateCodec.FromJson(json)); _runner = BuildRunner(); }

    DirectorHostRunner BuildRunner() => new(_director, _session, new DelegateWorldSource(_capture), new DelegateSpawnExecutor(_spawn));
    static DirectorConfiguration Configuration() => new() { Version = "my-game-director-v1", Seed = 9001 };
}

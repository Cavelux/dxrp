#nullable enable annotations
namespace SboxDirector;

public interface IDirectorAuthority
{
    bool IsAuthoritative { get; }
}

public sealed class AlwaysAuthoritative : IDirectorAuthority
{
    public bool IsAuthoritative => true;
}

public interface IDirectorWorldSource
{
    WorldSnapshot CaptureWorld();
}

public interface IDirectorSpawnExecutor
{
    SpawnRequestResult Execute(SpawnRequest request);
}

public interface IDirectorResourceExecutor
{
    ResourceSpawnResult Execute(ResourceSpawnRequest request);
}

public interface IDirectorNotificationSink
{
    void OnDecision(DirectorDecisionBatch decisions);
    void OnEvent(DirectorEvent directorEvent);
}

public enum DirectorRunStatus { Updated, SessionInactive, NotAuthoritative }
public sealed record DirectorRunResult(DirectorRunStatus Status, DirectorDecisionBatch? Decisions, IReadOnlyList<SpawnRequestResult> Results)
{
    public IReadOnlyList<ResourceSpawnResult> ResourceResults { get; init; } = Array.Empty<ResourceSpawnResult>();
}

public sealed class DirectorHostRunner
{
    readonly DirectorCore _director;
    readonly ISessionFlow _session;
    readonly IDirectorAuthority _authority;
    readonly IDirectorWorldSource _world;
    readonly IDirectorSpawnExecutor _spawner;
    readonly IDirectorResourceExecutor? _resourceSpawner;
    readonly IDirectorNotificationSink? _notifications;

    public DirectorHostRunner(DirectorCore director, ISessionFlow session, IDirectorWorldSource world, IDirectorSpawnExecutor spawner, IDirectorAuthority? authority = null, IDirectorResourceExecutor? resourceSpawner = null, IDirectorNotificationSink? notifications = null)
    {
        _director = director; _session = session; _world = world; _spawner = spawner; _authority = authority ?? new AlwaysAuthoritative(); _resourceSpawner = resourceSpawner; _notifications = notifications;
    }

    public DirectorRunResult Update()
    {
        if (!_authority.IsAuthoritative) return new(DirectorRunStatus.NotAuthoritative, null, Array.Empty<SpawnRequestResult>());
        if (!_session.IsSessionActive) return new(DirectorRunStatus.SessionInactive, null, Array.Empty<SpawnRequestResult>());
        var snapshot = _world.CaptureWorld();
        var decisions = _director.Tick(snapshot);
        _notifications?.OnDecision(decisions); foreach (var directorEvent in decisions.Events) _notifications?.OnEvent(directorEvent);
        var results = new List<SpawnRequestResult>();
        foreach (var request in decisions.SpawnRequests)
        {
            SpawnRequestResult result;
            try { result = _spawner.Execute(request); }
            catch (Exception exception) { result = new(request.RequestId, RequestResultKind.Failed, 0, exception.Message); }
            result = NormalizeResult(request, result);
            _director.ApplyResult(result); results.Add(result);
        }
        var resourceResults = new List<ResourceSpawnResult>();
        foreach (var request in decisions.ResourceRequests)
        {
            ResourceSpawnResult result;
            try { result = _resourceSpawner?.Execute(request) ?? new(request.RequestId, RequestResultKind.Rejected, "No resource executor was configured."); }
            catch (Exception exception) { result = new(request.RequestId, RequestResultKind.Failed, exception.Message); }
            if (result.RequestId != request.RequestId) result = result with { RequestId = request.RequestId, Detail = "Host returned the wrong request id. " + result.Detail };
            _director.ApplyResourceResult(result); resourceResults.Add(result);
        }
        return new(DirectorRunStatus.Updated, decisions, results) { ResourceResults = resourceResults };
    }

    public void ReportPressure(PressureEvent input, double time)
    {
        if (_authority.IsAuthoritative && _session.IsSessionActive) _director.ApplyPressure(input, time);
    }
    public IReadOnlyList<long> Stop() => _director.CancelPendingRequests();

    static SpawnRequestResult NormalizeResult(SpawnRequest request, SpawnRequestResult result)
    {
        if (result.RequestId != request.RequestId) result = result with { RequestId = request.RequestId, Detail = "Host returned the wrong request id. " + result.Detail };
        if (result.SpawnedCount < 0 || result.SpawnedCount > request.RequestedCount) return new(request.RequestId, RequestResultKind.Failed, 0, "Host returned an invalid spawned count. " + result.Detail);
        if (result.Result is RequestResultKind.Rejected or RequestResultKind.Failed && result.SpawnedCount != 0) return new(request.RequestId, RequestResultKind.Failed, 0, "Host returned spawned members for a failed request. " + result.Detail);
        if (result.Result == RequestResultKind.Succeeded && result.SpawnedCount < request.RequestedCount) return result with { Result = RequestResultKind.PartiallySucceeded, Detail = "Host reported success with an incomplete count. " + result.Detail };
        return result;
    }
}

public sealed class DelegateWorldSource : IDirectorWorldSource
{
    readonly Func<WorldSnapshot> _capture;
    public DelegateWorldSource(Func<WorldSnapshot> capture) { _capture = capture ?? throw new ArgumentNullException(nameof(capture)); }
    public WorldSnapshot CaptureWorld() => _capture();
}

public sealed class DelegateSpawnExecutor : IDirectorSpawnExecutor
{
    readonly Func<SpawnRequest, SpawnRequestResult> _execute;
    public DelegateSpawnExecutor(Func<SpawnRequest, SpawnRequestResult> execute) { _execute = execute ?? throw new ArgumentNullException(nameof(execute)); }
    public SpawnRequestResult Execute(SpawnRequest request) => _execute(request);
}

public sealed class DelegateResourceExecutor : IDirectorResourceExecutor
{
    readonly Func<ResourceSpawnRequest, ResourceSpawnResult> _execute;
    public DelegateResourceExecutor(Func<ResourceSpawnRequest, ResourceSpawnResult> execute) { _execute = execute ?? throw new ArgumentNullException(nameof(execute)); }
    public ResourceSpawnResult Execute(ResourceSpawnRequest request) => _execute(request);
}

public sealed class DelegateNotificationSink : IDirectorNotificationSink
{
    readonly Action<DirectorDecisionBatch>? _decision;
    readonly Action<DirectorEvent>? _event;
    public DelegateNotificationSink(Action<DirectorDecisionBatch>? decision = null, Action<DirectorEvent>? directorEvent = null) { _decision = decision; _event = directorEvent; }
    public void OnDecision(DirectorDecisionBatch decisions) => _decision?.Invoke(decisions);
    public void OnEvent(DirectorEvent directorEvent) => _event?.Invoke(directorEvent);
}

#nullable enable annotations
using Sandbox;

namespace SboxDirector;

/// <summary>
/// Optional s&amp;box component bridge. Derive from this class and translate your
/// game's entities/navigation into a WorldSnapshot and SpawnRequestResult.
/// </summary>
[Category("Gameplay")]
[Title("Adaptive Director")]
[Icon("neurology")]
public abstract class AdaptiveDirectorComponent : Component
{
    [Property] public bool DirectorEnabled { get; set; } = true;
    [Property] public int Seed { get; set; } = 1;
    [Property] public bool RoundBased { get; set; }
    [Property] public bool ResetDirectorOnRoundBegin { get; set; } = true;

    public DirectorCore? Director { get; private set; }
    public ISessionFlow? Session { get; private set; }
    public DirectorDecisionBatch? LastDecisions { get; private set; }
    public DirectorRunStatus LastRunStatus { get; private set; } = DirectorRunStatus.SessionInactive;

    DirectorHostRunner? _runner;

    protected override void OnStart() => InitializeDirector();

    protected override void OnFixedUpdate()
    {
        if (!DirectorEnabled || _runner is null) return;
        var result = _runner.Update(); LastRunStatus = result.Status; LastDecisions = result.Decisions;
        if (result.Decisions is not null) OnDirectorDecisions(result.Decisions, result.Results);
    }

    public void InitializeDirector()
    {
        Director = CreateDirector();
        Session = RoundBased ? new RoundSessionFlow() : new ContinuousSessionFlow();
        BindRunner();
        OnDirectorInitialized(Director);
    }

    public void BeginRound(string roundId)
    {
        if (Session is not RoundSessionFlow rounds) throw new InvalidOperationException("This component is not configured as round-based.");
        _runner?.Stop();
        if (ResetDirectorOnRoundBegin) { Director = CreateDirector(); BindRunner(); OnDirectorInitialized(Director); }
        rounds.BeginRound(roundId);
    }

    public void EndRound()
    {
        if (Session is not RoundSessionFlow rounds) return;
        _runner?.Stop(); rounds.EndRound();
    }

    public void ReportPressure(PressureEvent input, double time) => _runner?.ReportPressure(input, time);
    public string SaveDirectorState() => Director is null ? throw new InvalidOperationException("Director has not initialized.") : DirectorStateCodec.ToJson(Director.CaptureState());
    public void LoadDirectorState(string json)
    {
        _runner?.Stop(); Director = RestoreDirector(DirectorStateCodec.FromJson(json)); BindRunner(); OnDirectorInitialized(Director);
    }

    protected virtual DirectorConfiguration CreateConfiguration() => new() { Seed = Seed };
    protected virtual IDirectorPolicy CreatePolicy() => new DefaultDirectorPolicy();
    protected virtual IEncounterSchedule CreateSchedule() => new BasicEncounterSchedule();
    protected virtual IPlacementPolicy CreatePlacementPolicy() => new DefaultPlacementPolicy();
    protected virtual IEncounterComposer CreateComposer() => new DefaultEncounterComposer();
    protected virtual ResourcePopulationController? CreateResourceController() => null;
    protected virtual DirectorOrchestration? CreateOrchestration() => null;
    protected virtual DirectorOrchestration? RestoreOrchestration(DirectorOrchestrationState? state) => state is null ? CreateOrchestration() : throw new InvalidOperationException("Override RestoreOrchestration when orchestration persistence is enabled.");
    protected virtual DirectorCore CreateDirector() => new(CreateConfiguration(), CreatePolicy(), CreateSchedule(), CreatePlacementPolicy(), CreateComposer(), CreateResourceController(), CreateOrchestration());
    protected virtual DirectorCore RestoreDirector(DirectorState state) => new(CreateConfiguration(), state, CreatePolicy(), CreateSchedule(), CreatePlacementPolicy(), CreateComposer(), CreateResourceController(), RestoreOrchestration(state.Orchestration));
    protected virtual bool HasDirectorAuthority => !IsProxy;
    protected virtual void OnDirectorInitialized(DirectorCore director) { }
    protected virtual void OnDirectorDecisions(DirectorDecisionBatch decisions, IReadOnlyList<SpawnRequestResult> results) { }
    protected abstract WorldSnapshot CaptureDirectorWorld();
    protected abstract SpawnRequestResult ExecuteDirectorSpawn(SpawnRequest request);
    protected virtual ResourceSpawnResult ExecuteDirectorResourceSpawn(ResourceSpawnRequest request) => new(request.RequestId, RequestResultKind.Rejected, "Override ExecuteDirectorResourceSpawn to enable resource population.");
    void BindRunner()
    {
        if (Director is null || Session is null) throw new InvalidOperationException("Director component is incomplete.");
        _runner = new DirectorHostRunner(Director, Session, new DelegateWorldSource(CaptureDirectorWorld), new DelegateSpawnExecutor(ExecuteDirectorSpawn), new ComponentAuthority(this), new DelegateResourceExecutor(ExecuteDirectorResourceSpawn));
    }

    sealed class ComponentAuthority : IDirectorAuthority
    {
        readonly AdaptiveDirectorComponent _component;
        public ComponentAuthority(AdaptiveDirectorComponent component) { _component = component; }
        public bool IsAuthoritative => _component.HasDirectorAuthority;
    }
}

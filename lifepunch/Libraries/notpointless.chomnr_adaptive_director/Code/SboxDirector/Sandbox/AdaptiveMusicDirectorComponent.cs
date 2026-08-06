#nullable enable annotations
using Sandbox;

namespace SboxDirector;

/// <summary>Optional local/authoritative s&amp;box bridge for adaptive music. Networked games may replicate MusicDecisionBatch and run MusicRuntime on each client instead.</summary>
[Category("Gameplay")]
[Title("Adaptive Music Director")]
[Icon("graphic_eq")]
public abstract class AdaptiveMusicDirectorComponent : Component
{
    [Property] public bool MusicEnabled { get; set; } = true;
    [Property] public int Seed { get; set; } = 1;
    [Property] public string RuntimeParticipantId { get; set; } = "local";

    public MusicDirectorCore? MusicDirector { get; private set; }
    public MusicRuntime? MusicRuntime { get; private set; }
    public MusicHostRunResult? LastMusicResult { get; private set; }

    readonly List<MusicGameplayEvent> _pendingEvents = new();
    MusicDirectorHostRunner? _runner;
    long _nextEventSequence = 1;

    protected override void OnStart() => InitializeMusicDirector();
    protected override void OnFixedUpdate()
    {
        if (!MusicEnabled || _runner is null || !HasMusicAuthority) return;
        LastMusicResult = _runner.Update(CaptureMusicWorld(), _pendingEvents.ToArray()); _pendingEvents.Clear();
    }

    public void InitializeMusicDirector()
    {
        var catalog = CreateMusicCatalog(); MusicDirector = new MusicDirectorCore(CreateMusicConfiguration()); MusicRuntime = new MusicRuntime(RuntimeParticipantId, catalog, configuration: CreateMusicRuntimeConfiguration());
        _runner = new(MusicDirector, MusicRuntime, new ComponentAudioSink(this)); OnMusicDirectorInitialized(MusicDirector, MusicRuntime);
    }

    public long QueueMusicEvent(MusicTriggerKind kind, string participantId = "", string targetId = "", float fadeSeconds = 0, string emitterId = "", DirectorVector? position = null, IReadOnlyList<string>? arguments = null)
    {
        var sequence = _nextEventSequence++; _pendingEvents.Add(new(sequence, kind, participantId, targetId, fadeSeconds, emitterId, position) { Arguments = arguments ?? Array.Empty<string>() }); return sequence;
    }

    public string SaveMusicDirectorState() => MusicDirector is null ? throw new InvalidOperationException("Music Director has not initialized.") : MusicStateCodec.DirectorToJson(MusicDirector.CaptureState());
    public string SaveMusicRuntimeState() => MusicRuntime is null ? throw new InvalidOperationException("Music runtime has not initialized.") : MusicStateCodec.RuntimeToJson(MusicRuntime.CaptureState());
    public void LoadMusicState(string directorJson, string runtimeJson)
    {
        var directorState = MusicStateCodec.DirectorFromJson(directorJson); var runtimeState = MusicStateCodec.RuntimeFromJson(runtimeJson); var catalog = CreateMusicCatalog();
        var director = new MusicDirectorCore(CreateMusicConfiguration(), directorState); var runtime = new MusicRuntime(RuntimeParticipantId, catalog, runtimeState, CreateMusicRuntimeConfiguration());
        MusicDirector = director; MusicRuntime = runtime; _runner = new(director, runtime, new ComponentAudioSink(this)); _nextEventSequence = Math.Max(1, directorState.LastGameplayEventSequence + 1); _pendingEvents.Clear(); OnMusicDirectorInitialized(director, runtime);
    }

    protected virtual MusicDirectorConfiguration CreateMusicConfiguration() => RecoveredMusicDirectorDefaults.CreateConfiguration(seed: Seed);
    protected virtual MusicRuntimeConfiguration CreateMusicRuntimeConfiguration() => new();
    protected virtual bool HasMusicAuthority => !IsProxy;
    protected virtual void OnMusicDirectorInitialized(MusicDirectorCore director, MusicRuntime runtime) { }
    protected abstract IMusicEventCatalog CreateMusicCatalog();
    protected abstract MusicWorldSnapshot CaptureMusicWorld();
    protected abstract void ExecuteMusicAction(MusicRuntimeAction action);

    sealed class ComponentAudioSink : IMusicAudioSink
    {
        readonly AdaptiveMusicDirectorComponent _component;
        public ComponentAudioSink(AdaptiveMusicDirectorComponent component) { _component = component; }
        public void Apply(MusicRuntimeAction action) => _component.ExecuteMusicAction(action);
    }
}

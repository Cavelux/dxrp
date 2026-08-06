#nullable enable annotations
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Editor;
using Sandbox;

namespace SboxDirector.Editor;

public static class DirectorCompileGate
{
    static bool _started;
    [EditorEvent.Frame]
    public static void Tick()
    {
        if (_started) return;
        _started = true;
        var path = Environment.GetEnvironmentVariable("ADAPTIVE_DIRECTOR_GATE_RESULT");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path + ".arm")) return;
        try { File.Delete(path + ".arm"); } catch { return; }
        _ = Run(path);
    }

    static async Task Run(string path)
    {
        var result = new GateResult();
        try
        {
            result.ProjectPath = Project.Current?.GetRootPath();
            if ((result.ProjectPath ?? "").IndexOf("adaptive-director-editor-rig", StringComparison.OrdinalIgnoreCase) < 0) return;
            var configuration = new DirectorConfiguration { MaximumRequestsPerTick = 2, Placement = new Dictionary<EncounterKind, PlacementProfile> { [EncounterKind.Boss] = RecoveredDirectorDefaults.CreateThreatAreaProfile() } };
            var orchestrationConfiguration = new DirectorOrchestrationConfiguration { PersistenceId = "editor-gate-v1", ScenarioStages = new[] { new ScenarioStageDefinition("boss", Encounter: EncounterKind.Boss) } };
            var resourceRules = new[] { new ResourceRule { Category = "supply", TargetCount = 1 } };
            var composer = new UniformEncounterComposer(new Dictionary<EncounterKind, IReadOnlyList<string>> { [EncounterKind.Boss] = new[] { "gate_boss" } });
            var core = new DirectorCore(configuration, composer: composer, resources: new ResourcePopulationController(resourceRules), orchestration: new DirectorOrchestration(orchestrationConfiguration)); core.StartScenario(0);
            core.ApplyPressure(new PressureEvent("gate", PressureSeverity.Major, "editor gate"), 0);
            var world = new WorldSnapshot { Time = 0, Participants = new[] { new ParticipantSnapshot { Id = "gate" } }, SpawnCandidates = new[] { new SpawnCandidate { Id = "spawn", Reachable = true, Spawnable = true, NearestParticipantDistance = 1200, ThreatSeparation = 3000, PathDistance = 1000, IngressDistance = 500, ClearRadius = 500, AreaWidth = 24, AreaHeight = 24 } }, Resources = new ResourceWorldSnapshot { Candidates = new[] { new ResourceCandidate { Id = "resource", Category = "supply" } } } };
            var batch = core.Tick(world);
            var json = DirectorStateCodec.ToJson(core.CaptureState()); var state = DirectorStateCodec.FromJson(json);
            var restoredResources = new ResourcePopulationController(resourceRules); var restoredOrchestration = new DirectorOrchestration(orchestrationConfiguration, state.Orchestration!.Value); var restored = new DirectorCore(configuration, state, composer: composer, resources: restoredResources, orchestration: restoredOrchestration);
            var restoredBatch = restored.Tick(new WorldSnapshot { Time = 1, Participants = new[] { new ParticipantSnapshot { Id = "gate" } } });
            var samples = HullVisibilitySamples.CreateFivePointPattern(new DirectorVector(10, 0, 0), new DirectorVector(0, 0, 0), RecoveredDirectorDefaults.BossHullWidth, RecoveredDirectorDefaults.BossHullHeight);
            var rotation = new SpecialClassRotation(new[] { "gate_special" }); rotation.RestoreState(new Dictionary<string, double> { ["gate_special"] = 0 });
            result.CoreSmokePassed = batch.Time == 0 && batch.TeamPressure > 0 && batch.SpawnRequests.Count >= 1 && batch.SpawnRequests[0].Kind == EncounterKind.Boss && batch.SpawnRequests[0].Composition[0].Archetype == "gate_boss" && batch.ResourceRequests.Count == 1 && restoredBatch.TeamPressure == batch.TeamPressure && samples.Count == 5 && rotation.SelectDue(0, _ => true, new DeterministicRandom(1)) == "gate_special";

            var musicCues = new MusicCueConfiguration { CombatIntroEvents = new[] { "gate.combat" }, CombatSecondaryEvent = "", CombatCloseEvent = "", MobbedEvent = "", BossApproachingEvent = "", SafeAtmosphereEvent = "", DangerAtmosphereEvent = "", PositionalStingerEvent = "" };
            var musicConfig = RecoveredMusicDirectorDefaults.CreateConfiguration(musicCues, seed: 7); var music = new MusicDirectorCore(musicConfig);
            music.Tick(new MusicWorldSnapshot { Time = 0, Participants = new[] { new MusicParticipantSnapshot { Id = "gate" } } });
            var musicDecisions = music.Tick(new MusicWorldSnapshot { Time = 1, Participants = new[] { new MusicParticipantSnapshot { Id = "gate", VisibleCommonThreats = 2, VeryCloseCommonFactor = 1, CumulativeDamage = 5 } } });
            var catalog = new MusicEventCatalog(new[] { new MusicEventDefinition { EventId = "gate.combat", TrackId = "combat", Priority = MusicPriority.Critical } }); var musicRuntime = new MusicRuntime("gate", catalog); var musicActions = musicRuntime.Apply(musicDecisions);
            var musicState = MusicStateCodec.DirectorFromJson(MusicStateCodec.DirectorToJson(music.CaptureState())); var runtimeState = MusicStateCodec.RuntimeFromJson(MusicStateCodec.RuntimeToJson(musicRuntime.CaptureState()));
            var restoredMusic = new MusicDirectorCore(musicConfig, musicState); var restoredRuntime = new MusicRuntime("gate", catalog, runtimeState);
            result.MusicSmokePassed = musicDecisions.Decisions.Exists(x => x.Kind == MusicDecisionKind.Play && x.TargetId == "gate.combat") && musicActions.Actions.Exists(x => x.Kind == MusicRuntimeActionKind.Play && x.EventId == "gate.combat") && restoredMusic.CaptureState().NextDecisionId == musicState.NextDecisionId && restoredRuntime.Active.Count == 1;
        }
        catch (Exception e) { result.Error = e.ToString(); }
        result.Completed = true; result.Passed = result.CoreSmokePassed && result.MusicSmokePassed;
        File.WriteAllText(path, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        await Task.Delay(500); EditorUtility.Quit(true);
        await Task.Delay(10000); Environment.Exit(result.Passed ? 0 : 1);
    }

    sealed class GateResult
    {
        public bool Completed { get; set; }
        public bool Passed { get; set; }
        public bool CoreSmokePassed { get; set; }
        public bool MusicSmokePassed { get; set; }
        public string? ProjectPath { get; set; }
        public string? Error { get; set; }
    }
}

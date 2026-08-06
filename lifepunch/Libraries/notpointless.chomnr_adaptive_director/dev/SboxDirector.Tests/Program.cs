using SboxDirector;

static class Tests
{
    static int _count;
    static void Check(bool value, string name) { _count++; if (!value) throw new Exception($"FAILED: {name}"); }
    static void Near(float actual, float expected, float epsilon, string name) => Check(MathF.Abs(actual - expected) <= epsilon, $"{name}: expected {expected}, got {actual}");

    public static void Main()
    {
        Pressure(); TeamMaximum(); Tempo(); Placement(); AdvancedPlacement(); RecoveredCompatibility(); Population(); Wave(); Scenarios(); Core(); Persistence(); PersistentPolicies(); RuleSchedules(); ConcurrentSchedules(); CompositionAndRetry(); Resources(); Orchestration(); AdaptiveCallbacks(); HostRunner(); OwnershipAndResources(); MusicAuthority(); MusicRuntimeArbitration(); MusicPersistenceAndHost(); Validation(); LongDeterminism(); AdvancedDeterminism();
        Console.WriteLine($"PASS: {_count} adaptive-director assertions");
    }

    static void Pressure()
    {
        var p = new PressureTracker(new PressureConfiguration(), 0);
        p.Add(PressureSeverity.Major, 0); Near(p.Instantaneous, .125f, .00001f, "major coefficient");
        p.Update(5); Near(p.Instantaneous, .125f, .00001f, "hold");
        p.Update(8); Near(p.Instantaneous, .025f, .0001f, "linear decay");
        p.Add(PressureSeverity.Maximum, 8); Near(p.Instantaneous, 1f, .00001f, "maximum saturates");
        var q = new PressureTracker(new PressureConfiguration(), 0); q.Add(PressureSeverity.Critical, 0); q.Update(1f / 15f);
        Near(q.Averaged, MathF.Pow(.25f, (1f / 15f) / 20f), .0001f, "recovered averaged formula");
    }

    static void TeamMaximum()
    {
        var team = new TeamPressure(new PressureConfiguration()); team.Apply(new("a", PressureSeverity.Critical, "test"), 0); team.Apply(new("b", PressureSeverity.Minor, "test"), 0);
        var world = new WorldSnapshot { Time = 0, Participants = new[] { new ParticipantSnapshot { Id = "a" }, new ParticipantSnapshot { Id = "b" } } };
        Near(team.UpdateAndGetMaximum(world), .25f, .0001f, "team uses max");
    }

    static void Tempo()
    {
        var c = new TempoController(new TempoConfiguration { RelaxMinimumSeconds = 1, RelaxMaximumSeconds = 2, RelaxMaximumProgressTravel = .1f });
        c.Update(new WorldSnapshot { Time = 1, TeamProgress = .2f }, 0); Check(c.Phase == TempoPhase.BuildUp, "relax to buildup");
        c.Update(new WorldSnapshot { Time = 20 }, 1); Check(c.Phase == TempoPhase.SustainPeak, "buildup to peak");
        var absoluteClock = new DirectorCore(new DirectorConfiguration { Tempo = new TempoConfiguration { RelaxMinimumSeconds = 20, RelaxMaximumSeconds = 45 } }); Check(absoluteClock.Tick(World(10000)).Tempo == TempoPhase.Relax, "first tick aligns absolute clock");
    }

    static void Placement()
    {
        var candidates = new[] { new SpawnCandidate { Id = "visible", Reachable = true, Spawnable = true, Visible = true, NearestParticipantDistance = 1000, ThreatSeparation = 1000 }, new SpawnCandidate { Id = "ok", Reachable = true, Spawnable = true, NearestParticipantDistance = 1200, ThreatSeparation = 1000 } };
        var selected = new PlacementSelector().Select(candidates, new PlacementProfile(), new DeterministicRandom(1)); Check(selected?.Candidate.Id == "ok", "placement filters visibility");
        var vetoed = new PlacementSelector().Select(candidates, new PlacementProfile(), new DeterministicRandom(1), new WorldSnapshot(), new RejectAllPlacement()); Check(vetoed is null, "custom placement veto");
        var tagged = new SpawnCandidate { Id = "tagged", Reachable = true, Spawnable = true, NearestParticipantDistance = 1200, ThreatSeparation = 1000, Tags = new HashSet<string> { "outdoor" } }; Check(new PlacementSelector().Select(new[] { tagged }, new PlacementProfile { RequiredTags = new HashSet<string> { "indoor" } }, new DeterministicRandom(1)) is null, "required placement tags");
    }

    static void AdvancedPlacement()
    {
        var strict = new SpawnCandidate { Id = "strict", Reachable = true, Spawnable = true, NearestParticipantDistance = 1200, ThreatSeparation = 2000, PathDistance = 800, IngressDistance = 300, ClearRadius = 500, AdvanceRelation = AdvanceRelation.Ahead };
        var fallback = new SpawnCandidate { Id = "fallback", Reachable = true, Spawnable = true, NearestParticipantDistance = 1200, ThreatSeparation = 2000, PathDistance = 20, IngressDistance = 10, ClearRadius = 20, AdvanceRelation = AdvanceRelation.Behind };
        var profile = new PlacementProfile { MinimumPathDistance = 500, MinimumIngressDistance = 200, MinimumClearRadius = 300, AllowedRelations = new HashSet<AdvanceRelation> { AdvanceRelation.Ahead }, FallbackMode = PlacementFallbackMode.IgnoreRouteConstraints };
        var selected = new PlacementSelector().Select(new[] { strict, fallback }, profile, new DeterministicRandom(1)); Check(selected?.Candidate.Id == "strict" && !selected.IsFallback, "route-constrained placement");
        selected = new PlacementSelector().Select(new[] { fallback }, profile, new DeterministicRandom(1)); Check(selected?.Candidate.Id == "fallback" && selected.IsFallback, "route fallback placement");
    }

    static void RecoveredCompatibility()
    {
        var route = new[]
        {
            new SpawnCandidate { Id = "first-tie", Reachable = true, Spawnable = true, Progress = 50 },
            new SpawnCandidate { Id = "lower", Reachable = true, Spawnable = true, Progress = 40 },
            new SpawnCandidate { Id = "second-tie", Reachable = true, Spawnable = true, Progress = 50 }
        };
        var routeChoice = new PlacementSelector().Select(route, RecoveredDirectorDefaults.CreateRouteStepProfile(), new DeterministicRandom(1));
        Check(routeChoice?.Candidate.Id == "first-tie", "recovered route uses strict greater-than and first tie");

        var threat = new[]
        {
            new SpawnCandidate { Id = "too-small", Reachable = true, Spawnable = true, AreaWidth = 23, AreaHeight = 24 },
            new SpawnCandidate { Id = "a", Reachable = true, Spawnable = true, AreaWidth = 24, AreaHeight = 24 },
            new SpawnCandidate { Id = "b", Reachable = true, Spawnable = true, AreaWidth = 30, AreaHeight = 30 }
        };
        var expectedOrder = threat.ToArray(); var expectedRandom = new DeterministicRandom(17);
        for (var index = 0; index < expectedOrder.Length; index++) { var other = expectedRandom.NextInt(expectedOrder.Length); (expectedOrder[index], expectedOrder[other]) = (expectedOrder[other], expectedOrder[index]); }
        var expected = expectedOrder.First(x => x.AreaWidth >= 24 && x.AreaHeight >= 24).Id;
        var threatChoice = new PlacementSelector().Select(threat, RecoveredDirectorDefaults.CreateThreatAreaProfile(), new DeterministicRandom(17));
        Check(threatChoice?.Candidate.Id == expected, "recovered threat shuffle uses full-range random transpositions then first valid");

        var samples = HullVisibilitySamples.CreateFivePointPattern(new DirectorVector(10, 0, 2), new DirectorVector(0, 0, 2), 32, 72);
        Check(samples.Count == 5, "recovered visibility sample count");
        Near(samples[0].Y, 16, .0001f, "lower left hull offset"); Near(samples[1].Y, -16, .0001f, "lower right hull offset");
        Near(samples[2].Z, 38, .0001f, "upper center half-height"); Near(samples[3].Z, 74, .0001f, "upper corner full-height");

        var uniform = new UniformEncounterComposer(new Dictionary<EncounterKind, IReadOnlyList<string>> { [EncounterKind.Special] = new[] { "a", "b", "c" } });
        var composition = uniform.Compose(EncounterKind.Special, 20, new WorldSnapshot(), new DefaultDirectorPolicy(), new DeterministicRandom(3));
        Check(composition.Sum(x => x.Count) == 20 && composition.All(x => x.Archetype is "a" or "b" or "c"), "uniform recovered composition");

        var rotation = new SpecialClassRotation(new[] { "a", "b", "c" });
        rotation.RestoreState(new Dictionary<string, double> { ["a"] = 0, ["b"] = 0, ["c"] = 0 });
        var selected = rotation.SelectDue(10, x => x != "a", new DeterministicRandom(4));
        Check(selected is "b" or "c", "special rotation selects uniformly from due eligible classes");
        Near((float)rotation.CaptureState()["a"], 30, .0001f, "ineligible class retry delay");
        rotation.ReportSpawnFailure(selected!, 10); Near((float)rotation.CaptureState()[selected!], 15, .0001f, "failed class retry delay");
        rotation.ReportSpawnSuccess(selected!, 20); Near((float)rotation.CaptureState()[selected!], 1019, .0001f, "successful class hold");
        rotation.ReleaseAfterLifecycle(selected!, 30); Near((float)rotation.CaptureState()[selected!], 75, .0001f, "special respawn interval");
    }

    static void Population()
    {
        var ledger = new PopulationLedger(new PopulationConfiguration()); var empty = new PopulationSnapshot(); Check(ledger.TryReserve(1, EncounterKind.Boss, 1, empty), "reserve"); Check(ledger.Available(EncounterKind.Boss, empty) == 0, "reservation counts"); ledger.Resolve(new(1, RequestResultKind.Failed, 0)); Check(ledger.Available(EncounterKind.Boss, empty) == 1, "failure releases");
        Check(ledger.TryReserve(2, EncounterKind.Boss, 1, empty, 10), "reserve deferred"); ledger.Resolve(new(2, RequestResultKind.Deferred, 0)); Check(ledger.Available(EncounterKind.Boss, empty) == 0, "deferred remains reserved"); Check(ledger.Expire(10).SequenceEqual(new long[] { 2 }), "reservation expires"); Check(ledger.Available(EncounterKind.Boss, empty) == 1, "expiry releases");
    }

    static void Wave()
    {
        var wave = new WaveSequenceController(new WaveSequenceConfiguration { InitialDelayMinimum = 0, InitialDelayMaximum = 0, CombatTimeout = 0, PauseMinimum = 0, PauseMaximum = 0 }, new DeterministicRandom(2)); wave.Start(0); wave.Update(0, false); Check(wave.Update(0, false).IssueWave, "wave issued"); Check(wave.Update(0, false).Completed, "wave completed");
        var restored = new WaveSequenceController(new WaveSequenceConfiguration(), new DeterministicRandom(2), wave.Snapshot); Check(restored.State == WaveState.Done, "wave restore"); restored.Cancel(); Check(restored.State == WaveState.Inactive, "wave cancel");
    }

    static void Scenarios()
    {
        var scenario = new ScenarioController(new[] { new ScenarioStageDefinition("one") }); scenario.Start(0); Check(scenario.TryAdvance(0, false) && scenario.Status == ScenarioStatus.Complete, "scenario complete"); var rounds = new RoundSessionFlow(); rounds.BeginRound("1"); Check(rounds.IsSessionActive, "round adapter"); rounds.EndRound(); Check(!rounds.IsSessionActive, "round end"); Check(new ContinuousSessionFlow().IsSessionActive, "continuous adapter");
        var restored = new ScenarioController(new[] { new ScenarioStageDefinition("one") }, scenario.Snapshot); Check(restored.Status == ScenarioStatus.Complete, "scenario restore"); restored.Reset(); Check(restored.Status == ScenarioStatus.Inactive, "scenario reset");
    }

    static void Core()
    {
        var core = new DirectorCore(new DirectorConfiguration { Tempo = new TempoConfiguration { RelaxMinimumSeconds = 0, RelaxMaximumSeconds = 0 } });
        var output = core.Tick(new WorldSnapshot { Time = 0, Participants = new[] { new ParticipantSnapshot { Id = "p" } }, SpawnCandidates = new[] { new SpawnCandidate { Id = "s", Reachable = true, Spawnable = true, NearestParticipantDistance = 800, ThreatSeparation = 2000 } } });
        Check(output.Events.Count == 1, "core emits tempo transition");
        var multi = new DirectorCore(new DirectorConfiguration { MaximumRequestsPerTick = 2 }, schedule: new RepeatingSchedule()); var requests = multi.Tick(World(0)); Check(requests.SpawnRequests.Count == 2, "multiple requests per tick");
    }

    static WorldSnapshot World(double time, string? round = null) => new() { Time = time, RoundId = round, Participants = new[] { new ParticipantSnapshot { Id = "p" } }, SpawnCandidates = new[] { new SpawnCandidate { Id = "s", Reachable = true, Spawnable = true, NearestParticipantDistance = 1000, ThreatSeparation = 3000 } } };

    static void Persistence()
    {
        var config = new DirectorConfiguration { Seed = 42 }; Check(DirectorStateCodec.ToJson(new DirectorCore(config).CaptureState()).Contains("HasTicked"), "pre-tick state serializes"); var first = new DirectorCore(config); first.ApplyPressure(new("p", PressureSeverity.Major, "test"), 0); var initial = first.Tick(World(0)); Check(initial.SpawnRequests.Count == 1, "state setup request");
        var json = DirectorStateCodec.ToJson(first.CaptureState()); Check(json.Contains("ConfigurationVersion"), "state JSON");
        var restored = new DirectorCore(config, DirectorStateCodec.FromJson(json));
        var a = first.Tick(World(4)); var b = restored.Tick(World(4)); Check(a.Tempo == b.Tempo && a.TeamPressure == b.TeamPressure && a.SpawnRequests.SequenceEqual(b.SpawnRequests), "restored core remains deterministic");
        var diagnostics = new DirectorTelemetryBuffer(restored, 2); restored.ApplyPressure(new("p", PressureSeverity.Minor, "one"), 4); restored.ApplyPressure(new("p", PressureSeverity.Minor, "two"), 4); restored.ApplyPressure(new("p", PressureSeverity.Minor, "three"), 4); Check(diagnostics.Snapshot().Count == 2, "telemetry bounded"); diagnostics.Clear(); Check(diagnostics.Snapshot().Count == 0, "telemetry clear");
        Check(restored.CancelPendingRequests().Count > 0, "cancel pending");
    }

    static void RuleSchedules()
    {
        var population = new PopulationLedger(new PopulationConfiguration()); var world = World(0); var random = new DeterministicRandom(4);
        var rule = new RuleBasedEncounterSchedule(new[] { new EncounterRule { Id = "boss", Kind = EncounterKind.Boss, Count = 1, Priority = 10, CooldownSeconds = 30, Phases = new HashSet<TempoPhase> { TempoPhase.Relax } } });
        Check(rule.SelectEncounter(world, TempoPhase.Relax, 0, population, random) == EncounterKind.Boss, "rule selection"); rule.SelectionFinalized(false); Check(rule.SelectEncounter(world, TempoPhase.Relax, 0, population, random) == EncounterKind.Boss, "failed rule rolls cooldown back"); rule.SelectionFinalized(true); Check(rule.SelectEncounter(world, TempoPhase.Relax, 0, population, random) is null, "rule cooldown committed");
        var milestone = new MilestoneEncounterSchedule("boss-half", EncounterKind.Boss, .5f); var halfway = new WorldSnapshot { Time = 0, TeamProgress = .6f, ModeId = "campaign", RoundId = "r1" };
        Check(milestone.SelectEncounter(halfway, TempoPhase.Relax, 0, population, random) == EncounterKind.Boss, "milestone selected"); milestone.SelectionFinalized(false); Check(milestone.SelectEncounter(halfway, TempoPhase.Relax, 0, population, random) == EncounterKind.Boss, "milestone retry"); milestone.SelectionFinalized(true); Check(milestone.SelectEncounter(halfway, TempoPhase.Relax, 0, population, random) is null, "milestone one-shot per round");
        var state = milestone.CaptureScheduleState(); var copy = new MilestoneEncounterSchedule("boss-half", EncounterKind.Boss, .5f); copy.RestoreScheduleState(state); Check(copy.SelectEncounter(halfway, TempoPhase.Relax, 0, population, random) is null, "milestone persistence");
    }

    static void PersistentPolicies()
    {
        var configuration = new DirectorConfiguration();
        var policy = new StatefulPolicy("tests.policy");
        var placement = new StatefulPlacementPolicy("tests.placement");
        var composer = new StatefulComposer("tests.composer");
        var original = new DirectorCore(configuration, policy: policy, placementPolicy: placement, composer: composer);
        original.Tick(World(0));
        Check(policy.CallCount > 0 && placement.CallCount > 0, "custom policy state setup");

        var json = DirectorStateCodec.ToJson(original.CaptureState());
        var restoredPolicy = new StatefulPolicy("tests.policy");
        var restoredPlacement = new StatefulPlacementPolicy("tests.placement");
        var restoredComposer = new StatefulComposer("tests.composer");
        _ = new DirectorCore(configuration, DirectorStateCodec.FromJson(json), policy: restoredPolicy, placementPolicy: restoredPlacement, composer: restoredComposer);
        Check(restoredPolicy.CallCount == policy.CallCount, "custom Director policy restored");
        Check(restoredPlacement.CallCount == placement.CallCount, "custom placement policy restored");
        Check(restoredComposer.CallCount == composer.CallCount, "custom composer restored");

        var threw = false;
        try { _ = new DirectorCore(configuration, DirectorStateCodec.FromJson(json), policy: new StatefulPolicy("wrong.policy"), placementPolicy: restoredPlacement, composer: restoredComposer); }
        catch (InvalidOperationException) { threw = true; }
        Check(threw, "custom policy identity mismatch rejected");

        threw = false;
        try { _ = new DirectorCore(configuration, DirectorStateCodec.FromJson(json)); }
        catch (InvalidOperationException) { threw = true; }
        Check(threw, "custom policy state cannot be silently discarded");
    }

    static void ConcurrentSchedules()
    {
        var schedule = new ConcurrentEncounterSchedule(new[]
        {
            new ScheduleLane { Id = "ambient", Priority = 10, Schedule = new BasicEncounterSchedule(100, 100) },
            new ScheduleLane { Id = "secondary", Priority = 5, Schedule = new BasicEncounterSchedule(100, 100) }
        });
        var core = new DirectorCore(new DirectorConfiguration { MaximumRequestsPerTick = 2 }, schedule: schedule);
        var output = core.Tick(World(0)); Check(output.SpawnRequests.Count == 2, "independent schedule lanes share a tick");
        var state = schedule.CaptureScheduleState(); var copy = new ConcurrentEncounterSchedule(new[] { new ScheduleLane { Id = "ambient", Priority = 10, Schedule = new BasicEncounterSchedule(100, 100) }, new ScheduleLane { Id = "secondary", Priority = 5, Schedule = new BasicEncounterSchedule(100, 100) } }); copy.RestoreScheduleState(state); Check(copy.CaptureScheduleState()["coordinator.served"].Contains("ambient"), "concurrent schedule persistence");
    }

    static void CompositionAndRetry()
    {
        var composer = new WeightedEncounterComposer(new Dictionary<EncounterKind, IReadOnlyList<WeightedArchetype>> { [EncounterKind.Ambient] = new[] { new WeightedArchetype("grunt", 1) } });
        var configuration = new DirectorConfiguration { Retry = new RetryConfiguration { MaximumAttempts = 3, DelaySeconds = 1 } };
        var core = new DirectorCore(configuration, composer: composer); var first = core.Tick(World(0)).SpawnRequests.Single(); Check(first.Composition.Single() == new EncounterMember("grunt", 1) && first.Attempt == 1, "weighted composition");
        core.ApplyResult(new(first.RequestId, RequestResultKind.Failed, 0, "blocked")); var saved = DirectorStateCodec.FromJson(DirectorStateCodec.ToJson(core.CaptureState())); Check(saved.RetryQueue.Count == 1, "retry persisted");
        var restored = new DirectorCore(configuration, saved, composer: composer); Check(restored.Tick(World(.5)).SpawnRequests.Count == 0, "retry delay"); var retry = restored.Tick(World(1)).SpawnRequests.Single(); Check(retry.Attempt == 2 && retry.Composition.SequenceEqual(first.Composition), "retry preserves attempt and composition");
        restored.ApplyResult(new(retry.RequestId, RequestResultKind.PartiallySucceeded, 0, "partial")); Check(restored.CaptureState().RetryQueue.Single().RequestedCount == 1, "partial remainder queued");
    }

    static void Resources()
    {
        var rules = new[] { new ResourceRule { Category = "healing", TargetCount = 2, MaximumRequestsPerTick = 2 } }; var resources = new ResourcePopulationController(rules, 4);
        var core = new DirectorCore(new DirectorConfiguration(), resources: resources); var baseWorld = World(0); var world = new WorldSnapshot { Time = baseWorld.Time, Participants = baseWorld.Participants, SpawnCandidates = baseWorld.SpawnCandidates, Resources = new ResourceWorldSnapshot { Candidates = new[] { new ResourceCandidate { Id = "r1", Category = "healing", AreaCapacity = 2 }, new ResourceCandidate { Id = "r2", Category = "healing", AreaCapacity = 1 } } } };
        var output = core.Tick(world); Check(output.ResourceRequests.Count == 2, "resource density requests"); foreach (var request in output.ResourceRequests) core.ApplyResourceResult(new(request.RequestId, RequestResultKind.Succeeded));
        var state = core.CaptureState(); Check(state.Resources?.Consumed.Count == 2, "resource revisit state captured");
        var restoredResources = new ResourcePopulationController(rules, 9); _ = new DirectorCore(new DirectorConfiguration(), state, resources: restoredResources); Check(restoredResources.CaptureState().Consumed.Count == 2, "resource state restored");
        var threw = false; try { _ = new ResourcePopulationController(new[] { new ResourceRule { Category = "healing", TargetCount = 3 } }, state.Resources!.Value); } catch (InvalidOperationException) { threw = true; }
        Check(threw, "resource definition mismatch rejected");
        var clusterRules = new[] { new ResourceRule { Category = "ammo", TargetCount = 2, MaximumRequestsPerTick = 2, MaximumPerCluster = 1 } }; var clustered = new ResourcePopulationController(clusterRules); var clusteredWorld = new WorldSnapshot { Resources = new ResourceWorldSnapshot { Candidates = new[] { new ResourceCandidate { Id = "a", Category = "ammo", ClusterId = "room" }, new ResourceCandidate { Id = "b", Category = "ammo", ClusterId = "room" } } } }; var clusterRequest = clustered.Tick(clusteredWorld.Resources, clusteredWorld).Single(); Check(clusterRequest.CandidateId is "a" or "b", "resource cluster limit"); clustered.ApplyResult(new(clusterRequest.RequestId, RequestResultKind.Succeeded));
        var clusterCopy = new ResourcePopulationController(clusterRules, clustered.CaptureState()); var changedSnapshot = new ResourceWorldSnapshot { Candidates = new[] { new ResourceCandidate { Id = "replacement", Category = "ammo", ClusterId = "room" } } }; Check(clusterCopy.Tick(changedSnapshot, new WorldSnapshot { Resources = changedSnapshot }).Count == 0, "resource cluster history survives absent candidates");
        threw = false; try { _ = new ResourcePopulationController(new[] { new ResourceRule { Category = "healing", TargetCount = 2, MaximumRequestsPerTick = 2, RequiredTags = new HashSet<string> { "locked" } } }, state.Resources!.Value); } catch (InvalidOperationException) { threw = true; }
        Check(threw, "resource required-tag mismatch rejected");
    }

    static void Orchestration()
    {
        var orchestrationConfiguration = new DirectorOrchestrationConfiguration { ScenarioStages = new[] { new ScenarioStageDefinition("boss", Encounter: EncounterKind.Boss, MusicCue: "boss_intro"), new ScenarioStageDefinition("escape") }, Waves = new WaveSequenceConfiguration { InitialDelayMinimum = 0, InitialDelayMaximum = 0, CombatTimeout = 0, PauseMinimum = 0, PauseMaximum = 0 }, WaveEncounterCount = 3 };
        var orchestration = new DirectorOrchestration(orchestrationConfiguration); var core = new DirectorCore(new DirectorConfiguration(), orchestration: orchestration); core.StartScenario(0); var stage = core.Tick(World(0)); Check(stage.SpawnRequests.Single().Kind == EncounterKind.Boss && stage.Events.Any(x => x.Name == "music_cue"), "scenario encounter and music cue"); core.ApplyResult(new(stage.SpawnRequests[0].RequestId, RequestResultKind.Succeeded, 1)); core.Tick(World(1)); Check(orchestration.ScenarioStatus == ScenarioStatus.Running, "scenario advances stages");
        var saved = core.CaptureState(); var restoredOrchestration = new DirectorOrchestration(orchestrationConfiguration, saved.Orchestration!.Value); _ = new DirectorCore(new DirectorConfiguration(), saved, orchestration: restoredOrchestration); Check(restoredOrchestration.ScenarioStatus == orchestration.ScenarioStatus, "orchestration persistence");
        var threw = false; try { _ = new DirectorOrchestration(new DirectorOrchestrationConfiguration { PersistenceId = "wrong" }, saved.Orchestration.Value); } catch (InvalidOperationException) { threw = true; }
        Check(threw, "orchestration definition mismatch rejected");
        threw = false; try { _ = new DirectorOrchestration(new DirectorOrchestrationConfiguration { ScenarioStages = new[] { new ScenarioStageDefinition("changed") } }, saved.Orchestration.Value); } catch (InvalidOperationException) { threw = true; }
        Check(threw, "orchestration content mismatch rejected");
        threw = false; try { _ = new DirectorCore(new DirectorConfiguration(), saved, orchestration: new DirectorOrchestration(orchestrationConfiguration)); } catch (InvalidOperationException) { threw = true; }
        Check(threw, "fresh orchestration cannot replace restored state");
        var waves = new DirectorOrchestration(orchestrationConfiguration); var waveCore = new DirectorCore(new DirectorConfiguration(), orchestration: waves); waveCore.StartWaveSequence(0); waveCore.Tick(World(0)); var issued = waveCore.Tick(World(0)); Check(issued.SpawnRequests.Single().Kind == EncounterKind.CommonWave && issued.SpawnRequests[0].RequestedCount == 3, "integrated panic wave");

        var retryConfiguration = new DirectorConfiguration { Retry = new RetryConfiguration { MaximumAttempts = 2, DelaySeconds = 0 } };
        var retryScenario = new DirectorOrchestration(new DirectorOrchestrationConfiguration { ScenarioStages = new[] { new ScenarioStageDefinition("required", Encounter: EncounterKind.Boss) } }); var retryScenarioCore = new DirectorCore(retryConfiguration, orchestration: retryScenario); retryScenarioCore.StartScenario(0); var failedStage = retryScenarioCore.Tick(World(0)).SpawnRequests.Single(); retryScenarioCore.ApplyResult(new(failedStage.RequestId, RequestResultKind.Failed, 0)); var stageRetry = retryScenarioCore.Tick(World(0)).SpawnRequests.Single(); Check(stageRetry.Attempt == 2 && retryScenario.ScenarioStatus == ScenarioStatus.Running, "scenario waits for retried host result"); retryScenarioCore.ApplyResult(new(stageRetry.RequestId, RequestResultKind.Succeeded, 1)); Check(retryScenarioCore.Tick(World(0)).Events.Any(x => x.Name == "scenario_advanced") && retryScenario.ScenarioStatus == ScenarioStatus.Complete, "scenario advances after retry success");
        var retryWaves = new DirectorOrchestration(new DirectorOrchestrationConfiguration { Waves = new WaveSequenceConfiguration { InitialDelayMinimum = 0, InitialDelayMaximum = 0, CombatTimeout = 0, PauseMinimum = 0, PauseMaximum = 0 } }); var retryWaveCore = new DirectorCore(retryConfiguration, orchestration: retryWaves); retryWaveCore.StartWaveSequence(0); retryWaveCore.Tick(World(0)); var failedWave = retryWaveCore.Tick(World(0)).SpawnRequests.Single(); retryWaveCore.ApplyResult(new(failedWave.RequestId, RequestResultKind.Failed, 0)); var waveRetry = retryWaveCore.Tick(World(0)).SpawnRequests.Single(); Check(waveRetry.Attempt == 2 && retryWaves.WaveActive, "wave retry bypasses ordinary-schedule suppression"); retryWaveCore.ApplyResult(new(waveRetry.RequestId, RequestResultKind.Succeeded, waveRetry.RequestedCount)); Check(retryWaveCore.Tick(World(0)).Events.Any(x => x.Name == "wave_sequence_complete"), "wave waits for retry success before completion");
        var terminalOrchestration = new DirectorOrchestration(new DirectorOrchestrationConfiguration { ScenarioStages = new[] { new ScenarioStageDefinition("required", Encounter: EncounterKind.Boss) } }); var terminalCore = new DirectorCore(new DirectorConfiguration { Retry = new RetryConfiguration { MaximumAttempts = 1 } }, orchestration: terminalOrchestration); terminalCore.StartScenario(0); var terminalRequest = terminalCore.Tick(World(0)).SpawnRequests.Single(); terminalCore.ApplyResult(new(terminalRequest.RequestId, RequestResultKind.Rejected, 0, "veto")); Check(terminalCore.Tick(World(0)).Events.Any(x => x.Name == "orchestration_failed") && terminalOrchestration.ScenarioStatus == ScenarioStatus.Failed, "terminal orchestration failure is explicit");
    }

    static void AdaptiveCallbacks()
    {
        var policy = new AdaptivePolicy(); var core = new DirectorCore(new DirectorConfiguration(), policy: policy, schedule: new SingleKindSchedule(EncounterKind.Boss)); var output = core.Tick(World(0)); Check(output.SpawnRequests.Single().RequestedCount == 1 && output.MusicIntensity == .5f && output.Events.Any(x => x.Name == "boss_music"), "adaptive count music and boss callback");
        var populationCore = new DirectorCore(new DirectorConfiguration(), policy: policy, schedule: new SingleKindSchedule(EncounterKind.Ambient)); Check(populationCore.Tick(World(0)).SpawnRequests.Single().RequestedCount == 1, "adaptive population limit");
        var raisedLimit = new DirectorCore(new DirectorConfiguration { Population = new PopulationConfiguration { Limits = new Dictionary<EncounterKind, int> { [EncounterKind.Ambient] = 0 } } }, policy: policy, schedule: new SingleKindSchedule(EncounterKind.Ambient)); Check(raisedLimit.Tick(World(0)).SpawnRequests.Single().RequestedCount == 1, "adaptive population policy can raise configured limit");
        var timing = new RuleBasedEncounterSchedule(new[] { new EncounterRule { Id = "timed", Kind = EncounterKind.Ambient, CooldownSeconds = 10, Phases = new HashSet<TempoPhase> { TempoPhase.Relax } } }, new ModeEncounterTimingPolicy(new Dictionary<string, double> { ["survival"] = .5 })); var ledger = new PopulationLedger(new PopulationConfiguration()); var timedWorld = new WorldSnapshot { Time = 0, ModeId = "survival" }; Check(timing.SelectEncounter(timedWorld, TempoPhase.Relax, 0, ledger, new DeterministicRandom(1)) is not null, "adaptive timing initial"); timing.SelectionFinalized(true); Check(timing.SelectEncounter(new WorldSnapshot { Time = 4.9, ModeId = "survival" }, TempoPhase.Relax, 0, ledger, new DeterministicRandom(1)) is null && timing.SelectEncounter(new WorldSnapshot { Time = 5, ModeId = "survival" }, TempoPhase.Relax, 0, ledger, new DeterministicRandom(1)) is not null, "mode cooldown multiplier");
        var decisions = 0; var runner = new DirectorHostRunner(new DirectorCore(new DirectorConfiguration()), new ContinuousSessionFlow(), new DelegateWorldSource(() => World(0)), new DelegateSpawnExecutor(x => new(x.RequestId, RequestResultKind.Succeeded, x.RequestedCount)), notifications: new DelegateNotificationSink(_ => decisions++)); runner.Update(); Check(decisions == 1, "decision notification callback");
    }

    static void HostRunner()
    {
        var core = new DirectorCore(new DirectorConfiguration()); var flow = new ContinuousSessionFlow(); var executed = 0;
        var runner = new DirectorHostRunner(core, flow, new DelegateWorldSource(() => World(0)), new DelegateSpawnExecutor(request => { executed++; return new(request.RequestId, RequestResultKind.Succeeded, request.RequestedCount); }));
        var result = runner.Update(); Check(result.Status == DirectorRunStatus.Updated && executed == 1 && result.Results.Count == 1, "host executes decisions");
        var denied = new DirectorHostRunner(new DirectorCore(new DirectorConfiguration()), flow, new DelegateWorldSource(() => throw new Exception("must not capture")), new DelegateSpawnExecutor(_ => throw new Exception()), new TestAuthority(false)); Check(denied.Update().Status == DirectorRunStatus.NotAuthoritative, "authority gate");
        flow.IsSessionActive = false; Check(runner.Update().Status == DirectorRunStatus.SessionInactive, "session gate");
        flow.IsSessionActive = true; var failureRunner = new DirectorHostRunner(new DirectorCore(new DirectorConfiguration()), flow, new DelegateWorldSource(() => World(0)), new DelegateSpawnExecutor(_ => throw new InvalidOperationException("host failure"))); Check(failureRunner.Update().Results[0].Result == RequestResultKind.Failed, "host exception becomes result");
        var malformedRunner = new DirectorHostRunner(new DirectorCore(new DirectorConfiguration()), flow, new DelegateWorldSource(() => World(0)), new DelegateSpawnExecutor(request => new(request.RequestId + 99, RequestResultKind.Succeeded, request.RequestedCount + 1))); var malformed = malformedRunner.Update().Results.Single(); Check(malformed.RequestId == 1 && malformed.Result == RequestResultKind.Failed && malformed.SpawnedCount == 0, "host runner normalizes malformed result");
    }

    static void OwnershipAndResources()
    {
        var allocator = new EncounterOwnershipAllocator(); var first = allocator.Select(new[] { new OwnershipCandidate("a", 1, false), new OwnershipCandidate("b", 1) }, new DeterministicRandom(1)); Check(first == "b", "ownership eligibility");
        var r1 = new DeterministicRandom(9); var r2 = new DeterministicRandom(9); var candidates = new[] { new OwnershipCandidate("a", 1), new OwnershipCandidate("b", 3) }; Check(allocator.Select(candidates, r1) == allocator.Select(candidates, r2), "ownership deterministic");
        var budget = new ResourceBudget(new Dictionary<string, float> { ["health"] = 2 }); Check(budget.TrySpend("health", 1.5f) && !budget.TrySpend("health", 1), "resource budget"); Near(budget.Capture()["health"], .5f, .0001f, "resource capture");
    }

    static MusicEventCatalog TestMusicCatalog() => new(new[]
    {
        new MusicEventDefinition { EventId = "ambient.safe", TrackId = "ambient", Priority = MusicPriority.Low },
        new MusicEventDefinition { EventId = "mobbed", TrackId = "mob", Priority = MusicPriority.High, DuckTrackList = new[] { "all" } },
        new MusicEventDefinition { EventId = "boss", TrackId = "boss", Priority = MusicPriority.Critical, BlockTrackList = new[] { "combat" } },
        new MusicEventDefinition { EventId = "combat.low", TrackId = "combat", Priority = MusicPriority.Low },
        new MusicEventDefinition { EventId = "combat.high", TrackId = "combat", Priority = MusicPriority.High },
        new MusicEventDefinition { EventId = "delayed", TrackId = "stinger", Priority = MusicPriority.Medium, DelaySeconds = 2 },
        new MusicEventDefinition { EventId = "intro", TrackId = "intro", Priority = MusicPriority.High, AutoQueueEventId = "loop" },
        new MusicEventDefinition { EventId = "loop", TrackId = "loop", Priority = MusicPriority.High },
        new MusicEventDefinition { EventId = "death", TrackId = "death", Priority = MusicPriority.Critical, Flags = MusicMasterFlags.AllowAfterDeath },
        new MusicEventDefinition { EventId = "tag.master", TrackId = "master", Priority = MusicPriority.High, TimingTags = new[] { new MusicTimingTag(2, 4) }, LoopSeconds = 8 },
        new MusicEventDefinition { EventId = "tag.child", TrackId = "child", Priority = MusicPriority.High, SyncTrackId = "master", SyncTagIndex = 2 },
        new MusicEventDefinition { EventId = "split.a", TrackId = "split", Priority = MusicPriority.High },
        new MusicEventDefinition { EventId = "split.b", TrackId = "split", Priority = MusicPriority.High, Flags = MusicMasterFlags.PlaySplit }
    });

    static MusicDecisionBatch MusicBatch(double time, params MusicDecision[] decisions)
    {
        var batch = new MusicDecisionBatch { Time = time }; batch.Decisions.AddRange(decisions); return batch;
    }

    static void MusicAuthority()
    {
        var special = new SpecialMusicAlertConfiguration { ArchetypeId = "ambusher", CloseEvent = "alert.close", MiddleEvent = "alert.middle", FarEvent = "alert.far" };
        var cues = new MusicCueConfiguration { CombatIntroEvents = new[] { "combat.intro" }, CombatSecondaryEvent = "combat.secondary", CombatCloseEvent = "combat.close", MobbedEvent = "mobbed", BossApproachingEvent = "boss", BossTrackId = "boss", HazardBurningEvent = "hazard.burn", HazardAttackEvent = "hazard.attack", HazardRageEvent = "hazard.rage", SafeAtmosphereEvent = "ambient.safe", DangerAtmosphereEvent = "ambient.danger", PositionalStingerEvent = "choir" };
        var configuration = RecoveredMusicDirectorDefaults.CreateConfiguration(cues, new[] { special }, 41);
        var core = new MusicDirectorCore(configuration);
        var idle = new MusicWorldSnapshot { Time = 0, Participants = new[] { new MusicParticipantSnapshot { Id = "p" } } };
        var initial = core.Tick(idle); Check(initial.Decisions.Count(x => x.Kind == MusicDecisionKind.SetMixerLayer) == 9, "music authority emits initial mixer snapshot");
        var combatInput = new MusicParticipantSnapshot { Id = "p", CumulativeDamage = 10, VisibleCommonThreats = 3, ActiveCommonThreats = 20, ScannedCommonThreats = 5, VeryCloseCommonFactor = 1, AttackingCommonFactor = .4f, CloseCommonAction = .42f, InflictedDamage = true, SpecialThreats = new[] { new SpecialMusicThreat("ambusher", 1000, "s1") } };
        var combat = core.Tick(new MusicWorldSnapshot { Time = 1, Participants = new[] { combatInput } });
        Check(combat.Decisions.Any(x => x.Kind == MusicDecisionKind.Play && x.TargetId == "combat.intro"), "music authority starts combat group");
        Check(combat.Decisions.Any(x => x.TargetId == "alert.close"), "music authority selects close special alert");
        Near(combat.Decisions.Single(x => x.LayerId == MusicMixerLayers.MobBeating).Value, .37f, .0001f, "recovered damage duck equation");
        Near(combat.Decisions.Single(x => x.LayerId == MusicMixerLayers.MobRules).Value, 0f, .0001f, "recovered mob pressure equation");
        Near(combat.Decisions.Single(x => x.LayerId == MusicMixerLayers.CombatClose).Value, 0f, .0001f, "recovered close action equation");

        var boss = core.Tick(new MusicWorldSnapshot { Time = 2, Participants = new[] { new MusicParticipantSnapshot { Id = "p", BossPresent = true } } });
        Check(boss.Decisions.Any(x => x.Kind == MusicDecisionKind.Play && x.TargetId == "boss"), "music boss approach transition");
        var defeated = core.Tick(new MusicWorldSnapshot { Time = 3, Participants = new[] { new MusicParticipantSnapshot { Id = "p" } } });
        Check(defeated.Decisions.Any(x => x.Kind == MusicDecisionKind.StopTrack && x.TargetId == "boss" && x.FadeSeconds == 2), "music boss defeat track stop");
        var hazard = core.Tick(new MusicWorldSnapshot { Time = 4, Participants = new[] { new MusicParticipantSnapshot { Id = "p", HazardPresent = true, HazardBurning = true, HazardAttacking = true, HazardRage = .75f, HazardEmitterId = "h", HazardPosition = new DirectorVector(1, 2, 3), WanderingHazards = new[] { new WanderingMusicHazard("w", .2f, new DirectorVector(4, 5, 6)) } } } });
        Check(new[] { "hazard.burn", "hazard.attack", "hazard.rage" }.All(id => hazard.Decisions.Any(x => x.Kind == MusicDecisionKind.Play && x.TargetId == id)), "hazard transition cues");
        Near(hazard.Decisions.Single(x => x.LayerId == MusicMixerLayers.HazardRage).Value, .25f, .0001f, "hazard rage inverse layer");
        Check(hazard.Decisions.Any(x => x.TargetId == "hazard.wandering.tier4" && x.Position is not null), "wandering hazard low-anger tier and position");

        var generic = core.Tick(new MusicWorldSnapshot { Time = 5, Participants = new[] { new MusicParticipantSnapshot { Id = "p" } } }, new[] { new MusicGameplayEvent(1, MusicTriggerKind.Play, "p", "custom.event") });
        Check(generic.Decisions.Any(x => x.TargetId == "custom.event"), "generic gameplay music event");
        var duplicate = core.Tick(new MusicWorldSnapshot { Time = 6, Participants = new[] { new MusicParticipantSnapshot { Id = "p" } } }, new[] { new MusicGameplayEvent(1, MusicTriggerKind.Play, "p", "should.not.repeat") });
        Check(!duplicate.Decisions.Any(x => x.TargetId == "should.not.repeat"), "music events apply exactly once");
        var reveal = core.Tick(new MusicWorldSnapshot { Time = 7, Participants = new[] { new MusicParticipantSnapshot { Id = "p" } } }, new[] { new MusicGameplayEvent(2, MusicTriggerKind.LargeAreaRevealed, "p") });
        var revealLimited = core.Tick(new MusicWorldSnapshot { Time = 8, Participants = new[] { new MusicParticipantSnapshot { Id = "p" } } }, new[] { new MusicGameplayEvent(3, MusicTriggerKind.LargeAreaRevealed, "p") });
        Check(reveal.Decisions.Any(x => x.TargetId == "world.large_area_reveal") && !revealLimited.Decisions.Any(x => x.TargetId == "world.large_area_reveal"), "large-area reveal cooldown");
    }

    static void MusicRuntimeArbitration()
    {
        var catalog = TestMusicCatalog(); var runtime = new MusicRuntime("p", catalog);
        var ambient = runtime.Apply(MusicBatch(0, new MusicDecision(1, MusicDecisionKind.Play, "p", "ambient.safe", "test")));
        Check(ambient.Actions.Any(x => x.Kind == MusicRuntimeActionKind.Play && x.EventId == "ambient.safe"), "runtime plays resolved event");
        Check(runtime.Apply(MusicBatch(0, new MusicDecision(1, MusicDecisionKind.Play, "p", "ambient.safe", "duplicate"))).Actions.Count == 0, "runtime decisions apply exactly once");
        var mob = runtime.Apply(MusicBatch(1, new MusicDecision(2, MusicDecisionKind.Play, "p", "mobbed", "test")));
        Check(mob.Actions.Any(x => x.Kind == MusicRuntimeActionKind.SetTrackVolume && x.TrackId == "ambient" && x.Value == .5f), "runtime duck list");
        Check(!mob.Actions.Any(x => x.Kind == MusicRuntimeActionKind.SetTrackVolume && x.TrackId == "mob" && x.Value == .5f), "duck source does not duck itself");
        runtime.Apply(MusicBatch(2, new MusicDecision(3, MusicDecisionKind.Play, "p", "boss", "test")));
        var blocked = runtime.Apply(MusicBatch(3, new MusicDecision(4, MusicDecisionKind.Play, "p", "combat.high", "test")));
        Check(blocked.Results.Single(x => x.DecisionId == 4).Result == MusicDecisionResultKind.RejectedBlocked, "runtime active block list");

        var priority = new MusicRuntime("p", catalog); priority.Apply(MusicBatch(0, new MusicDecision(10, MusicDecisionKind.Play, "p", "combat.low", "test")));
        var high = priority.Apply(MusicBatch(1, new MusicDecision(11, MusicDecisionKind.Play, "p", "combat.high", "test")));
        Check(high.Actions.Any(x => x.Kind == MusicRuntimeActionKind.StopEvent && x.EventId == "combat.low") && high.Actions.Any(x => x.Kind == MusicRuntimeActionKind.Play && x.EventId == "combat.high"), "higher priority replaces same track");
        var rejectedLow = priority.Apply(MusicBatch(2, new MusicDecision(12, MusicDecisionKind.Play, "p", "combat.low", "test")));
        Check(rejectedLow.Results.Single(x => x.DecisionId == 12).Result == MusicDecisionResultKind.RejectedPriority, "lower priority cannot replace active track");

        var delayed = new MusicRuntime("p", catalog); var deferred = delayed.Apply(MusicBatch(0, new MusicDecision(20, MusicDecisionKind.Play, "p", "delayed", "test")));
        Check(deferred.Results.Single().Result == MusicDecisionResultKind.Deferred && delayed.Tick(1).Actions.Count == 0, "runtime delay queue");
        Check(delayed.Tick(2).Actions.Any(x => x.EventId == "delayed"), "runtime delayed engagement");

        var tagged = new MusicRuntime("p", catalog); tagged.Apply(MusicBatch(0, new MusicDecision(30, MusicDecisionKind.Play, "p", "tag.master", "test")));
        var tagDeferred = tagged.Apply(MusicBatch(1, new MusicDecision(31, MusicDecisionKind.Play, "p", "tag.child", "test")));
        Check(tagDeferred.Results.Single().Result == MusicDecisionResultKind.Deferred && tagged.Tick(3.9).Actions.Count == 0 && tagged.Tick(4).Actions.Any(x => x.EventId == "tag.child"), "runtime timing-tag alignment");

        var split = new MusicRuntime("p", catalog); split.Apply(MusicBatch(0, new MusicDecision(35, MusicDecisionKind.Play, "p", "split.a", "test"))); split.Apply(MusicBatch(1, new MusicDecision(36, MusicDecisionKind.Play, "p", "split.b", "test")));
        Check(split.Active.Count == 2, "runtime split flag permits concurrent same-track events");

        var auto = new MusicRuntime("p", catalog); var intro = auto.Apply(MusicBatch(0, new MusicDecision(40, MusicDecisionKind.Play, "p", "intro", "test"))); var instance = intro.Results.Single().InstanceId;
        Check(auto.ReportCompleted(instance, 1).Actions.Any(x => x.EventId == "loop"), "runtime autoqueue on completion");
        auto.SetLifecycle(true, false); var deadReject = auto.Apply(MusicBatch(2, new MusicDecision(41, MusicDecisionKind.Play, "p", "ambient.safe", "test"), new MusicDecision(42, MusicDecisionKind.Play, "p", "death", "test")));
        Check(deadReject.Results.Single(x => x.DecisionId == 41).Result == MusicDecisionResultKind.RejectedLifecycle && deadReject.Results.Single(x => x.DecisionId == 42).Result == MusicDecisionResultKind.Accepted, "runtime after-death gate");
        var general = auto.Apply(MusicBatch(3, new MusicDecision(43, MusicDecisionKind.General, "p", "custom.command", "test") { Arguments = new[] { "a", "b" } }));
        Check(general.Actions.Single(x => x.Kind == MusicRuntimeActionKind.General).Arguments.SequenceEqual(new[] { "a", "b" }), "runtime forwards generalized commands");
    }

    static void MusicPersistenceAndHost()
    {
        var config = RecoveredMusicDirectorDefaults.CreateConfiguration(seed: 99); var first = new MusicDirectorCore(config); first.Tick(new MusicWorldSnapshot { Time = 0, Participants = new[] { new MusicParticipantSnapshot { Id = "p" } } });
        first.Tick(new MusicWorldSnapshot { Time = 1, Participants = new[] { new MusicParticipantSnapshot { Id = "p", VisibleCommonThreats = 2, WeaponActivity = .5f } } });
        var restored = new MusicDirectorCore(config, MusicStateCodec.DirectorFromJson(MusicStateCodec.DirectorToJson(first.CaptureState())));
        var nextWorld = new MusicWorldSnapshot { Time = 2, Participants = new[] { new MusicParticipantSnapshot { Id = "p", VisibleCommonThreats = 1 } } };
        Check(first.Tick(nextWorld).Decisions.SequenceEqual(restored.Tick(nextWorld).Decisions), "music authority save restore determinism");

        var catalog = TestMusicCatalog(); var runtime = new MusicRuntime("p", catalog); runtime.Apply(MusicBatch(0, new MusicDecision(1, MusicDecisionKind.Play, "p", "ambient.safe", "test"), new MusicDecision(2, MusicDecisionKind.Play, "p", "delayed", "test")));
        var restoredRuntime = new MusicRuntime("p", catalog, MusicStateCodec.RuntimeFromJson(MusicStateCodec.RuntimeToJson(runtime.CaptureState())));
        Check(runtime.Tick(2).Actions.SequenceEqual(restoredRuntime.Tick(2).Actions), "music runtime save restore determinism");

        var actions = new List<MusicRuntimeAction>(); var hostCore = new MusicDirectorCore(RecoveredMusicDirectorDefaults.CreateConfiguration(new MusicCueConfiguration { CombatIntroEvents = new[] { "ambient.safe" }, CombatSecondaryEvent = "", CombatCloseEvent = "", SafeAtmosphereEvent = "", DangerAtmosphereEvent = "", PositionalStingerEvent = "" })); var hostRuntime = new MusicRuntime("p", catalog); var runner = new MusicDirectorHostRunner(hostCore, hostRuntime, new DelegateMusicAudioSink(actions.Add));
        runner.Update(new MusicWorldSnapshot { Time = 0, Participants = new[] { new MusicParticipantSnapshot { Id = "p" } } }); runner.Update(new MusicWorldSnapshot { Time = 1, Participants = new[] { new MusicParticipantSnapshot { Id = "p", VisibleCommonThreats = 1 } } });
        Check(actions.Any(x => x.Kind == MusicRuntimeActionKind.Play && x.EventId == "ambient.safe"), "music host runner bridges decisions to audio sink");
    }

    static void Validation()
    {
        var backwards = new DirectorCore(new DirectorConfiguration()); backwards.Tick(World(2)); var threw = false; try { backwards.Tick(World(1)); } catch (InvalidOperationException) { threw = true; }
        Check(threw, "reject backwards world time");
        threw = false; try { _ = new DirectorConfiguration { MaximumRequestsPerTick = 0 }; new DirectorConfiguration { MaximumRequestsPerTick = 0 }.Validate(); } catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw, "configuration validation");
        threw = false; try { _ = DirectorStateCodec.FromJson("{}"); } catch (InvalidOperationException) { threw = true; }
        Check(threw, "state validation");
        threw = false; try { new WorldSnapshot { Participants = new[] { new ParticipantSnapshot { Id = "same" }, new ParticipantSnapshot { Id = "same" } } }.Validate(); } catch (ArgumentException) { threw = true; }
        Check(threw, "duplicate participant validation");
        var incomplete = new DirectorCore(new DirectorConfiguration()); var incompleteRequest = incomplete.Tick(World(0)).SpawnRequests.Single(); incomplete.ApplyResult(new(incompleteRequest.RequestId, RequestResultKind.Succeeded, 0)); Check(incomplete.CaptureState().RetryQueue.Count == 1, "incomplete success becomes partial retry");
        var excessive = new DirectorCore(new DirectorConfiguration()); var excessiveRequest = excessive.Tick(World(0)).SpawnRequests.Single(); threw = false; try { excessive.ApplyResult(new(excessiveRequest.RequestId, RequestResultKind.Succeeded, excessiveRequest.RequestedCount + 1)); } catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw, "excessive host result rejected");
    }

    static void LongDeterminism()
    {
        var configuration = new DirectorConfiguration { Seed = 7823, RequestTimeoutSeconds = 2 }; DirectorCore first = new(configuration); DirectorCore second = new(configuration);
        for (var index = 0; index < 1000; index++)
        {
            var time = index * .1;
            if (index % 73 == 0) { var pressure = new PressureEvent("p", (PressureSeverity)(index / 73 % 4 + 1), "simulation"); first.ApplyPressure(pressure, time); second.ApplyPressure(pressure, time); }
            var world = World(time); var a = first.Tick(world); var b = second.Tick(world);
            Check(a.Tempo == b.Tempo && a.TeamPressure == b.TeamPressure && a.MusicIntensity == b.MusicIntensity && a.SpawnRequests.SequenceEqual(b.SpawnRequests) && a.Events.SequenceEqual(b.Events), $"deterministic tick {index}");
            foreach (var request in a.SpawnRequests) { var result = new SpawnRequestResult(request.RequestId, RequestResultKind.Succeeded, request.RequestedCount); first.ApplyResult(result); second.ApplyResult(result); }
            if (index == 500) second = new DirectorCore(configuration, DirectorStateCodec.FromJson(DirectorStateCodec.ToJson(second.CaptureState())));
        }
    }

    sealed class RejectAllPlacement : IPlacementPolicy { public bool IsAllowed(SpawnCandidate candidate, PlacementProfile profile, WorldSnapshot world) => false; public float ScoreAdjustment(SpawnCandidate candidate, PlacementProfile profile, WorldSnapshot world) => 0; }
    sealed class TestAuthority : IDirectorAuthority { public TestAuthority(bool value) { IsAuthoritative = value; } public bool IsAuthoritative { get; } }
    sealed class RepeatingSchedule : IEncounterSchedule { int _index; public EncounterKind? SelectEncounter(WorldSnapshot world, TempoPhase tempo, float teamPressure, PopulationLedger population, DeterministicRandom random) => _index++ % 2 == 0 ? EncounterKind.Ambient : EncounterKind.Special; public int RequestedCount(EncounterKind kind, WorldSnapshot world) => 1; }
    sealed class SingleKindSchedule : IEncounterSchedule { readonly EncounterKind _kind; public SingleKindSchedule(EncounterKind kind) { _kind = kind; } public EncounterKind? SelectEncounter(WorldSnapshot world, TempoPhase tempo, float teamPressure, PopulationLedger population, DeterministicRandom random) => _kind; public int RequestedCount(EncounterKind kind, WorldSnapshot world) => 2; }
    sealed class AdaptivePolicy : IDirectorPolicy, IAdaptiveDirectorPolicy, IAdaptivePopulationPolicy
    {
        public bool AllowEncounter(EncounterKind kind, WorldSnapshot world) => true;
        public bool CanCoexist(EncounterKind requested, PopulationSnapshot population) => true;
        public string SelectArchetype(EncounterKind kind, WorldSnapshot world, DeterministicRandom random) => "boss";
        public int AdjustEncounterCount(EncounterKind kind, int proposedCount, TempoPhase tempo, float teamPressure, WorldSnapshot world) => 1;
        public float AdjustMusicIntensity(float proposedIntensity, TempoPhase tempo, float teamPressure, WorldSnapshot world) => .5f;
        public bool ShouldPlayBossMusic(EncounterKind kind, WorldSnapshot world) => true;
        public int AdjustPopulationLimit(EncounterKind kind, int configuredLimit, WorldSnapshot world) => 1;
    }

    static void AdvancedDeterminism()
    {
        var configuration = new DirectorConfiguration { Seed = 99, MaximumRequestsPerTick = 3, Retry = new RetryConfiguration { MaximumAttempts = 3, DelaySeconds = .5 } };
        DirectorCore first = BuildAdvancedCore(configuration); DirectorCore second = BuildAdvancedCore(configuration); first.StartScenario(0); second.StartScenario(0); first.StartWaveSequence(0, 2); second.StartWaveSequence(0, 2);
        for (var index = 0; index < 500; index++)
        {
            var baseWorld = World(index * .25); var world = new WorldSnapshot { Time = baseWorld.Time, Participants = baseWorld.Participants, SpawnCandidates = baseWorld.SpawnCandidates, Resources = new ResourceWorldSnapshot { Candidates = new[] { new ResourceCandidate { Id = "supply-a", Category = "supply", AreaCapacity = 2 }, new ResourceCandidate { Id = "supply-b", Category = "supply", AreaCapacity = 1 } } } };
            var a = first.Tick(world); var b = second.Tick(world); Check(a.SpawnRequests.SequenceEqual(b.SpawnRequests) && a.ResourceRequests.SequenceEqual(b.ResourceRequests) && a.Events.SequenceEqual(b.Events) && a.MusicIntensity == b.MusicIntensity, $"advanced deterministic tick {index}");
            foreach (var request in a.SpawnRequests) { var result = new SpawnRequestResult(request.RequestId, request.RequestId % 7 == 0 ? RequestResultKind.Failed : RequestResultKind.Succeeded, request.RequestId % 7 == 0 ? 0 : request.RequestedCount); first.ApplyResult(result); second.ApplyResult(result); }
            foreach (var request in a.ResourceRequests) { var result = new ResourceSpawnResult(request.RequestId, RequestResultKind.Succeeded); first.ApplyResourceResult(result); second.ApplyResourceResult(result); }
            if (index == 250) second = BuildAdvancedCore(configuration, DirectorStateCodec.FromJson(DirectorStateCodec.ToJson(second.CaptureState())));
        }
    }

    static DirectorCore BuildAdvancedCore(DirectorConfiguration configuration, DirectorState? state = null)
    {
        IEncounterSchedule Schedule() => new ConcurrentEncounterSchedule(new[] { new ScheduleLane { Id = "ambient", Priority = 10, Schedule = new BasicEncounterSchedule(2, 4) }, new ScheduleLane { Id = "special", Priority = 5, Schedule = new RuleBasedEncounterSchedule(new[] { new EncounterRule { Id = "special", Kind = EncounterKind.Special, Count = 1, CooldownSeconds = 5, Probability = .5f, Phases = new HashSet<TempoPhase> { TempoPhase.Relax, TempoPhase.BuildUp, TempoPhase.SustainPeak } } }) } });
        var composer = new WeightedEncounterComposer(new Dictionary<EncounterKind, IReadOnlyList<WeightedArchetype>> { [EncounterKind.CommonWave] = new[] { new WeightedArchetype("grunt", 3), new WeightedArchetype("brute", 1) } });
        var resources = new ResourcePopulationController(new[] { new ResourceRule { Category = "supply", TargetCount = 2, MaximumRequestsPerTick = 2 } }, 77);
        var orchestrationConfiguration = new DirectorOrchestrationConfiguration { PersistenceId = "advanced-test-v1", ScenarioStages = new[] { new ScenarioStageDefinition("opening", .5), new ScenarioStageDefinition("boss", Encounter: EncounterKind.Boss), new ScenarioStageDefinition("done") }, Waves = new WaveSequenceConfiguration { WaveCount = 2, InitialDelayMinimum = 0, InitialDelayMaximum = 0, CombatTimeout = 0, PauseMinimum = .25, PauseMaximum = .25 } };
        if (state is null) return new(configuration, schedule: Schedule(), composer: composer, resources: resources, orchestration: new(orchestrationConfiguration));
        var orchestration = state.Value.Orchestration is { } orchestrationState ? new DirectorOrchestration(orchestrationConfiguration, orchestrationState) : null;
        return new(configuration, state.Value, schedule: Schedule(), composer: composer, resources: resources, orchestration: orchestration);
    }
    sealed class StatefulPolicy : IDirectorPolicy, IPersistentDirectorPolicy
    {
        public StatefulPolicy(string id) { PersistenceId = id; }
        public string PersistenceId { get; }
        public int CallCount { get; private set; }
        public bool AllowEncounter(EncounterKind kind, WorldSnapshot world) { CallCount++; return true; }
        public bool CanCoexist(EncounterKind requested, PopulationSnapshot population) => true;
        public string SelectArchetype(EncounterKind kind, WorldSnapshot world, DeterministicRandom random) => kind.ToString();
        public IReadOnlyDictionary<string, string> CapturePolicyState() => new Dictionary<string, string> { ["calls"] = CallCount.ToString(CultureInfo.InvariantCulture) };
        public void RestorePolicyState(IReadOnlyDictionary<string, string> state) => CallCount = int.Parse(state["calls"], CultureInfo.InvariantCulture);
    }
    sealed class StatefulPlacementPolicy : IPlacementPolicy, IPersistentPlacementPolicy
    {
        public StatefulPlacementPolicy(string id) { PersistenceId = id; }
        public string PersistenceId { get; }
        public int CallCount { get; private set; }
        public bool IsAllowed(SpawnCandidate candidate, PlacementProfile profile, WorldSnapshot world) { CallCount++; return true; }
        public float ScoreAdjustment(SpawnCandidate candidate, PlacementProfile profile, WorldSnapshot world) => 0;
        public IReadOnlyDictionary<string, string> CapturePlacementState() => new Dictionary<string, string> { ["calls"] = CallCount.ToString(CultureInfo.InvariantCulture) };
        public void RestorePlacementState(IReadOnlyDictionary<string, string> state) => CallCount = int.Parse(state["calls"], CultureInfo.InvariantCulture);
    }
    sealed class StatefulComposer : IEncounterComposer, IPersistentEncounterComposer
    {
        public StatefulComposer(string id) { PersistenceId = id; }
        public string PersistenceId { get; }
        public int CallCount { get; private set; }
        public IReadOnlyList<EncounterMember> Compose(EncounterKind kind, int requestedCount, WorldSnapshot world, IDirectorPolicy policy, DeterministicRandom random) { CallCount++; return new[] { new EncounterMember("stateful", requestedCount) }; }
        public IReadOnlyDictionary<string, string> CaptureComposerState() => new Dictionary<string, string> { ["calls"] = CallCount.ToString(CultureInfo.InvariantCulture) };
        public void RestoreComposerState(IReadOnlyDictionary<string, string> state) => CallCount = int.Parse(state["calls"], CultureInfo.InvariantCulture);
    }
}

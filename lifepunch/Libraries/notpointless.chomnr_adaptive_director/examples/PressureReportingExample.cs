using SboxDirector;

namespace AdaptiveDirectorExamples;

/// <summary>
/// Centralizes the conversion from game events to the Director's five pressure
/// severities. Call this service only from authoritative gameplay code.
/// </summary>
public sealed class PressureReportingExample
{
    readonly Action<PressureEvent, double> _report;

    public PressureReportingExample(Action<PressureEvent, double> report)
    {
        _report = report ?? throw new ArgumentNullException(nameof(report));
    }

    public static PressureReportingExample ForDirector(DirectorCore director) => new(director.ApplyPressure);
    public static PressureReportingExample ForHostRunner(DirectorHostRunner runner) => new(runner.ReportPressure);

    /// <summary>normalizedDamage is damage divided by the participant's full health.</summary>
    public void OnDamage(string participantId, float normalizedDamage, double gameTime)
    {
        if (!float.IsFinite(normalizedDamage) || normalizedDamage < 0) throw new ArgumentOutOfRangeException(nameof(normalizedDamage));
        var severity = normalizedDamage switch
        {
            < .20f => PressureSeverity.Moderate,
            < .50f => PressureSeverity.Major,
            _ => PressureSeverity.Critical
        };
        Report(participantId, severity, "damage", gameTime);
    }

    public void OnLedgeDanger(string participantId, double gameTime) => Report(participantId, PressureSeverity.Major, "ledge_danger", gameTime);

    public void OnIncapacitated(string participantId, int previousReviveCount, double gameTime)
    {
        if (previousReviveCount < 0) throw new ArgumentOutOfRangeException(nameof(previousReviveCount));
        Report(participantId, previousReviveCount == 0 ? PressureSeverity.Maximum : PressureSeverity.Critical, "incapacitated", gameTime);
    }

    /// <summary>Compatibility behavior: only cooperative Easy/Normal revival reaches maximum pressure.</summary>
    public void OnDefibrillatorRevived(string participantId, string baseMode, string difficulty, double gameTime)
    {
        var compatible = string.Equals(baseMode, "coop", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(difficulty, "easy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(difficulty, "normal", StringComparison.OrdinalIgnoreCase));
        if (compatible) Report(participantId, PressureSeverity.Maximum, "defibrillator_revival", gameTime);
    }

    /// <summary>Maps the recovered 150/500-unit proximity bands. Set typedThreat for special/nonzero threat types.</summary>
    public void OnThreatKilledNearby(string participantId, float distance, bool bossOrDormantHazard, bool typedThreat, float ordinaryThreatSpeed, double gameTime)
    {
        if (!float.IsFinite(distance) || distance < 0 || !float.IsFinite(ordinaryThreatSpeed) || ordinaryThreatSpeed < 0) throw new ArgumentOutOfRangeException(nameof(distance));
        if (distance > 500) return;

        PressureSeverity severity;
        if (bossOrDormantHazard) severity = distance <= 150 ? PressureSeverity.Critical : PressureSeverity.Major;
        else if (typedThreat) severity = distance <= 150 ? PressureSeverity.Moderate : PressureSeverity.Minor;
        else severity = distance <= 150 && ordinaryThreatSpeed > 150 ? PressureSeverity.Moderate : PressureSeverity.Minor;
        Report(participantId, severity, "nearby_threat_death", gameTime);
    }

    /// <summary>Use custom mappings for game-specific events while keeping them in one auditable place.</summary>
    public void OnCustomDanger(string participantId, PressureSeverity severity, string reason, double gameTime) => Report(participantId, severity, reason, gameTime);

    void Report(string participantId, PressureSeverity severity, string reason, double gameTime)
    {
        if (string.IsNullOrWhiteSpace(participantId)) throw new ArgumentException("A participant id is required.", nameof(participantId));
        if (!double.IsFinite(gameTime)) throw new ArgumentOutOfRangeException(nameof(gameTime));
        _report(new PressureEvent(participantId, severity, reason), gameTime);
    }
}

/// <summary>Example diagnostic wiring for a developer HUD or server log.</summary>
public sealed class DirectorDiagnosticsExample
{
    readonly DirectorTelemetryBuffer _telemetry;
    public DirectorDiagnosticsExample(DirectorCore director) { _telemetry = new DirectorTelemetryBuffer(director, capacity: 200); }
    public IReadOnlyList<DirectorDiagnostic> ReadForDebugHud() => _telemetry.Snapshot();
    public void Clear() => _telemetry.Clear();
}

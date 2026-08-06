#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AssetDoctor;

/// <summary>Provides the dockable UI for heuristic current-project asset diagnostics.</summary>
[Dock("Editor", "Asset Doctor", "local_hospital")]
public sealed class AssetDoctorWindow : Widget
{
    /// <summary>Limits total scan findings retained in memory and exported.</summary>
    private const int MaxScanFindings = 50_000;
    /// <summary>Limits rendered occurrences under one rule group.</summary>
    private const int MaxRenderedFindingsPerGroup = 250;
    /// <summary>Limits rendered rule groups to protect editor responsiveness.</summary>
    private const int MaxRenderedGroups = 200;
    /// <summary>Bounds memory used by dependency-graph edges.</summary>
    private const int MaxDependencyGraphEdges = 100_000;
    /// <summary>References the scrollable findings host.</summary>
    private ScrollArea? _scrollArea;
    /// <summary>References the current findings canvas.</summary>
    private Widget? _resultsContainer;
    /// <summary>Displays scan and export status.</summary>
    private Label? _statusLabel;
    /// <summary>References the report export button.</summary>
    private Button? _exportButton;
    /// <summary>Stores the cancellation source for the active scan.</summary>
    private CancellationTokenSource? _scanCts;
    /// <summary>Stores findings from the most recently completed scan attempt.</summary>
    private List<Finding> _lastFindings = new();
    /// <summary>Invalidates stale asynchronous scan completions.</summary>
    private int _scanGeneration;

    /// <summary>Initializes the dock content.</summary>
    public AssetDoctorWindow(Widget parent) : base(parent, false) => BuildUI();

    /// <summary>Builds fresh editor controls after creation or hotload.</summary>
    [EditorEvent.Hotload]
    private void BuildUI()
    {
        CancelCurrentScan();
        _scanGeneration++;
        if(Layout == null)
        {
            Layout = Layout.Column();
            Layout.Margin = 4;
            Layout.Spacing = 4;
        }
        else Layout.Clear(true);
        var scanButton = new Button("Run / Restart Scan", "search", this);
        scanButton.Clicked += OnScanClicked;
        scanButton.MinimumSize = new Vector2(220, 38);
        Layout.Add(scanButton);
        _exportButton = new Button("Export Reports", "download", this);
        _exportButton.Clicked += ExportReports;
        _exportButton.MinimumSize = new Vector2(220, 38);
        _exportButton.Hidden = _lastFindings.Count == 0;
        Layout.Add(_exportButton);
        _statusLabel = new Label(string.Empty, this) { Hidden = true };
        Layout.Add(_statusLabel);
        _scrollArea = new ScrollArea(this) { Hidden = true };
        Layout.Add(_scrollArea);
        RebuildResultsContainer();
        if(_lastFindings.Count > 0) RenderFindings(_lastFindings);
    }

    /// <summary>Starts a new scan and cancels any earlier scan.</summary>
    private async void OnScanClicked()
    {
        CancelCurrentScan();
        var cts = new CancellationTokenSource();
        _scanCts = cts;
        var generation = ++_scanGeneration;
        _lastFindings = new();
        if(_exportButton != null) _exportButton.Hidden = true;
        if(_scrollArea != null) _scrollArea.Hidden = true;
        if(_statusLabel != null) _statusLabel.Hidden = false;
        if(_resultsContainer?.Layout != null) _resultsContainer.Layout.Clear(true);
        SetStatus("Scanning current-project source assets...", "#ffffff");
        try
        {
            var snapshot = CreateSnapshot();
            var findings = await Task.Run(() => ScanFiles(snapshot, cts.Token), cts.Token);
            if(!IsValid || cts.IsCancellationRequested || generation != _scanGeneration) return;
            RenderFindings(findings);
        }
        catch(OperationCanceledException)
        {
            if(generation == _scanGeneration) SetStatus("Scan cancelled.", "#ffe19a");
        }
        catch(Exception exception)
        {
            Log.Error($"Asset Doctor scan failed: {exception}");
            if(generation == _scanGeneration) SetStatus($"Scan failed: {exception.GetType().Name}", "#ffb8c0");
        }
        finally
        {
            if(ReferenceEquals(_scanCts, cts)) _scanCts = null;
            cts.Dispose();
        }
    }

    /// <summary>Creates a UI-thread snapshot of project files and all editor-known resolvable logical paths.</summary>
    private static ScanSnapshot CreateSnapshot()
    {
        var scope = ProjectAssetScope.TryCreate();
        if(scope == null) throw new InvalidOperationException("No active project Assets directory is available.");
        var allEditorAssets = AssetSystem.All.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Path)).ToArray();
        var resolvablePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var asset in allEditorAssets)
        {
            resolvablePaths.Add(AssetPathRules.NormalizeSeparators(asset.Path));
            if(!string.IsNullOrWhiteSpace(asset.RelativePath))
                resolvablePaths.Add(AssetPathRules.NormalizeSeparators(asset.RelativePath));
        }
        var projectAssets = allEditorAssets
            .Where(scope.Contains)
            .Where(x => !string.IsNullOrWhiteSpace(x.AbsolutePath))
            .Select(x => new SourceAsset(AssetPathRules.NormalizeSeparators(x.Path), x.AbsolutePath))
            .ToArray();
        return new ScanSnapshot(projectAssets, resolvablePaths);
    }

    /// <summary>Scans physical source files on a worker thread without s&box interop calls.</summary>
    private static List<Finding> ScanFiles(ScanSnapshot snapshot, CancellationToken token)
    {
        var collector = new FindingCollector(MaxScanFindings);
        var projectPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach(var asset in snapshot.Assets)
        {
            token.ThrowIfCancellationRequested();
            if(projectPaths.TryGetValue(asset.Path, out var previous))
            {
                if(!collector.TryAdd(new Finding("AD110", FindingSeverity.Error, "Asset paths collide when compared without letter case.", asset.Path, previous)))
                    return collector.Findings;
            }
            else projectPaths.Add(asset.Path, asset.Path);
        }

        var graph = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var graphEdgeCount = 0;
        var graphLimitReported = false;
        foreach(var asset in snapshot.Assets)
        {
            token.ThrowIfCancellationRequested();
            if(!AssetPathRules.HasAnyExtension(asset.Path, AssetPathRules.TextAssetExtensions)) continue;
            try
            {
                var info = new FileInfo(asset.AbsolutePath);
                if(!info.Exists)
                {
                    if(!collector.TryAdd(new Finding("AD105", FindingSeverity.Error, "Could not inspect text asset because its source file no longer exists.", asset.Path)))
                        return collector.Findings;
                    continue;
                }
                if(info.Length == 0) continue;
                if(info.Length > AssetPathRules.MaxTextFileBytes)
                {
                    if(!collector.TryAdd(new Finding("AD112", FindingSeverity.Warning, "Text asset was skipped because it exceeds the physical file-size scan limit.", asset.Path)))
                        return collector.Findings;
                    continue;
                }

                var text = File.ReadAllText(asset.AbsolutePath);
                if(text.Length > AssetPathRules.MaxTextCharacters)
                {
                    if(!collector.TryAdd(new Finding("AD112", FindingSeverity.Warning, "Text asset was skipped because it exceeds the decoded character scan limit.", asset.Path)))
                        return collector.Findings;
                    continue;
                }

                var extraction = ReferenceExtractor.Extract(text, AssetPathRules.UsesJsonEscapes(asset.Path), token);
                if(extraction.IsTruncated && !collector.TryAdd(new Finding("AD113", FindingSeverity.Warning, "Reference extraction stopped after reaching the per-file reference limit.", asset.Path)))
                    return collector.Findings;

                foreach(var reference in extraction.References)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var invalid = AssetPathValidator.Validate(asset.Path, reference.Path, reference.Line);
                        if(invalid != null)
                        {
                            if(!collector.TryAdd(invalid)) return collector.Findings;
                            continue;
                        }

                        if(projectPaths.TryGetValue(reference.Path, out var actualProjectPath))
                        {
                            if(!string.Equals(reference.Path, actualProjectPath, StringComparison.Ordinal) &&
                                !collector.TryAdd(new Finding("AD103", FindingSeverity.Error, "Asset reference uses different letter case than the actual asset path.", asset.Path, reference.Path, reference.Line, actualProjectPath)))
                                return collector.Findings;

                            if(!graph.TryGetValue(asset.Path, out var dependencies))
                            {
                                dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                graph.Add(asset.Path, dependencies);
                            }

                            if(!dependencies.Contains(actualProjectPath))
                            {
                                if(graphEdgeCount < MaxDependencyGraphEdges)
                                {
                                    dependencies.Add(actualProjectPath);
                                    graphEdgeCount++;
                                }
                                else if(!graphLimitReported)
                                {
                                    if(!collector.TryAdd(new Finding(
                                        "AD115",
                                        FindingSeverity.Warning,
                                        "Dependency graph limit was reached. Circular-reference results may be incomplete.",
                                        asset.Path)))
                                    {
                                        return collector.Findings;
                                    }

                                    graphLimitReported = true;
                                }
                            }
                        }
                        else if(!snapshot.ResolvablePaths.Contains(reference.Path))
                        {
                            if(!collector.TryAdd(new Finding("AD100", FindingSeverity.Error, "Missing asset reference.", asset.Path, reference.Path, reference.Line)))
                                return collector.Findings;
                        }
                    }
                    catch(Exception exception)
                    {
                        if(!collector.TryAdd(new Finding("AD111", FindingSeverity.Error, $"Could not validate asset reference ({exception.GetType().Name}).", asset.Path, reference.Path, reference.Line)))
                            return collector.Findings;
                    }
                }
            }
            catch(Exception exception)
            {
                if(!collector.TryAdd(new Finding("AD105", FindingSeverity.Error, $"Could not read text asset ({exception.GetType().Name}).", asset.Path)))
                    return collector.Findings;
            }
        }

        foreach(var cycleFinding in AssetReferenceCycleDetector.Find(graph, token))
        {
            if(!collector.TryAdd(cycleFinding)) break;
        }
        return collector.Findings;
    }

    /// <summary>Renders bounded groups from completed findings.</summary>
    private void RenderFindings(List<Finding> findings)
    {
        if(_resultsContainer?.Layout == null) return;
        _lastFindings = findings;
        _resultsContainer.Layout.Clear(true);
        if(_scrollArea != null)
        {
            _scrollArea.Hidden = false;
            _scrollArea.MinimumSize = new Vector2(0, 280);
        }
        if(_exportButton != null) _exportButton.Hidden = findings.Count == 0;
        var errors = findings.Count(x => x.Severity == FindingSeverity.Error);
        var warnings = findings.Count(x => x.Severity == FindingSeverity.Warning);
        SetStatus($"Found {findings.Count} issues · {errors} errors · {warnings} warnings · heuristic quoted-path scan", errors > 0 ? "#ffb8c0" : "#9ee6a5");
        var groups = findings.GroupBy(x => new { x.RuleId, x.Severity, x.Message }).OrderByDescending(x => Rank(x.Key.Severity)).ThenBy(x => x.Key.RuleId).Take(MaxRenderedGroups).ToArray();
        foreach(var group in groups) AddGroup(group.Key.RuleId, group.Key.Severity, group.Key.Message, group);
        if(findings.Count > 0 && groups.Length == MaxRenderedGroups) _resultsContainer.Layout.Add(new Label("More rule groups were omitted to protect Editor performance. Export reports for the full list.", _resultsContainer));
        _resultsContainer.Layout.AddStretchCell();
    }

    /// <summary>Adds one collapsible rule group to the results canvas.</summary>
    private void AddGroup(string ruleId, FindingSeverity severity, string message, IEnumerable<Finding> findings)
    {
        if(_resultsContainer?.Layout == null) return;
        var all = findings.OrderBy(x => x.SourcePath, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.ReferencedPath, StringComparer.OrdinalIgnoreCase).ToArray();
        var rendered = all.Take(MaxRenderedFindingsPerGroup).ToArray();
        var isError = severity == FindingSeverity.Error;
        var background = isError ? "#4a1f25" : "#4a3a12";
        var border = isError ? "#d94b58" : "#e0ae32";
        var text = isError ? "#ffb8c0" : "#ffe19a";
        var container = new Widget(_resultsContainer) { Layout = Layout.Column() };
        container.Layout.Margin = 0;
        container.Layout.Spacing = 1;
        _resultsContainer.Layout.Add(container);
        var closed = $"▶ {ruleId} · {message} · {all.Length} issue(s)";
        var open = $"▼ {ruleId} · {message} · {all.Length} issue(s)";
        var details = new Widget(container) { Layout = Layout.Column(), Hidden = true };
        details.Layout.Margin = 0;
        details.Layout.Spacing = 1;
        var header = new FindingRow(container, closed, background, border, text);
        header.Clicked += () =>
        {
            if(!details.IsValid || !header.IsValid) return;
            details.Hidden = !details.Hidden;
            header.Text = details.Hidden ? closed : open;
        };
        container.Layout.Add(header);
        foreach(var finding in rendered)
        {
            var source = finding.SourcePath;
            var holder = new Widget(details) { Layout = Layout.Row() };
            holder.Layout.Margin = 0;
            holder.Layout.Spacing = 0;
            holder.Layout.Add(new Widget(holder) { FixedWidth = 18 });
            var detail = finding.Details == null ? string.Empty : $" · {finding.Details}";
            var line = finding.Line.HasValue ? $":{finding.Line.Value}" : string.Empty;
            var row = new FindingRow(holder, $"{finding.RuleId} · {finding.Message} · {finding.SourcePath}{line} → {finding.ReferencedPath}{detail}", background, border, text);
            row.Clicked += () => AssetBrowserNavigator.FocusAsset(source);
            holder.Layout.Add(row, 1);
            details.Layout.Add(holder);
        }
        if(all.Length > rendered.Length) details.Layout.Add(new Label($"… {all.Length - rendered.Length} more issue(s); export reports for the full list.", details));
        container.Layout.Add(details);
    }

    /// <summary>Exports reports through a user-selected directory.</summary>
    private void ExportReports()
    {
        if(_lastFindings.Count == 0) return;
        var dialog = new FileDialog(this) { Title = "Select Report Folder" };
        dialog.SetFindDirectory();
        if(!dialog.Execute() || string.IsNullOrWhiteSpace(dialog.Directory)) return;
        try
        {
            var name = Path.GetFileName(Project.Current?.GetAssetsPath()?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "project";
            AssetDoctorReportExporter.Export(dialog.Directory, name, _lastFindings);
            SetStatus($"Reports exported to {dialog.Directory}", "#9ee6a5");
        }
        catch(Exception exception)
        {
            Log.Error($"Asset Doctor export failed: {exception}");
            SetStatus($"Export failed: {exception.GetType().Name}", "#ffb8c0");
        }
    }

    /// <summary>Creates the scroll canvas used for results.</summary>
    private void RebuildResultsContainer()
    {
        if(_scrollArea == null) return;
        _resultsContainer = new Widget(null) { Layout = Layout.Column() };
        _resultsContainer.Layout.Spacing = 2;
        _scrollArea.Canvas = _resultsContainer;
    }

    /// <summary>Cancels and detaches the active scan; its owner disposes the source after completion.</summary>
    private void CancelCurrentScan()
    {
        var current = Interlocked.Exchange(ref _scanCts, null);
        current?.Cancel();
    }

    /// <summary>Updates status text and its severity color.</summary>
    private void SetStatus(string text, string color)
    {
        if(_statusLabel == null) return;
        _statusLabel.Hidden = false;
        _statusLabel.Text = text;
        _statusLabel.SetStyles($"padding: 4px 2px; color: {color};");
    }

    /// <summary>Returns sort precedence for severities.</summary>
    private static int Rank(FindingSeverity severity) => severity == FindingSeverity.Error ? 2 : severity == FindingSeverity.Warning ? 1 : 0;
    /// <summary>Bounds retained findings and adds one explicit truncation diagnostic when the scan limit is reached.</summary>
    private sealed class FindingCollector
    {
        /// <summary>Stores retained findings.</summary>
        private readonly List<Finding> _findings = new();
        /// <summary>Stores the maximum retained finding count.</summary>
        private readonly int _limit;
        /// <summary>Tracks whether the terminal limit finding was added.</summary>
        private bool _truncated;

        /// <summary>Initializes a bounded finding collector.</summary>
        public FindingCollector(int limit) => _limit = limit;
        /// <summary>Gets retained findings.</summary>
        public List<Finding> Findings => _findings;
        /// <summary>Adds a finding or a final truncation diagnostic, returning false when scanning must stop.</summary>
        public bool TryAdd(Finding finding)
        {
            if(_truncated) return false;
            if(_findings.Count < _limit - 1)
            {
                _findings.Add(finding);
                return true;
            }

            _findings.Add(new Finding("AD114", FindingSeverity.Warning, "Scan stopped after reaching the global finding limit.", finding.SourcePath, finding.ReferencedPath, finding.Line));
            _truncated = true;
            return false;
        }
    }

    /// <summary>Represents a physical source file captured on the UI thread.</summary>
    private sealed record SourceAsset(string Path, string AbsolutePath);
    /// <summary>Represents a scan-input snapshot that worker code treats as read-only.</summary>
    private sealed record ScanSnapshot(SourceAsset[] Assets, HashSet<string> ResolvablePaths);
}

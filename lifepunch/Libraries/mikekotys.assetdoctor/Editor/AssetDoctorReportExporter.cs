#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AssetDoctor;

/// <summary>Writes timestamped Markdown, text, and JSON reports for completed heuristic scans.</summary>
public static class AssetDoctorReportExporter
{
    /// <summary>Exports all report formats with one timestamp and attempts to remove partial final files on failure.</summary>
    public static IReadOnlyList<string> Export(string directory, string projectName, IReadOnlyCollection<Finding> findings)
    {
        if(string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A report directory is required.", nameof(directory));
        if(findings == null) throw new ArgumentNullException(nameof(findings));
        Directory.CreateDirectory(directory);
        var generatedAt = DateTimeOffset.UtcNow;
        var stamp = generatedAt.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var safeProject = SanitizeFileName(string.IsNullOrWhiteSpace(projectName) ? "project" : projectName);
        var unique = Guid.NewGuid().ToString("N")[..8];
        var prefix = $"asset_doctor_{safeProject}_{stamp}_{unique}";
        var ordered = findings.OrderByDescending(x => Rank(x.Severity)).ThenBy(x => x.RuleId, StringComparer.Ordinal).ThenBy(x => x.SourcePath, StringComparer.Ordinal).ToArray();
        var finals = new[] { Path.Combine(directory, prefix + ".md"), Path.Combine(directory, prefix + ".txt"), Path.Combine(directory, prefix + ".json") };
        var temps = finals.Select(path => path + ".tmp-" + Guid.NewGuid().ToString("N")).ToArray();
        var moved = new List<string>();
        try
        {
            WriteMarkdown(temps[0], ordered, generatedAt);
            WriteText(temps[1], ordered, generatedAt);
            WriteJson(temps[2], ordered, generatedAt);
            for(var index = 0; index < finals.Length; index++)
            {
                File.Move(temps[index], finals[index], false);
                moved.Add(finals[index]);
            }
            return finals;
        }
        catch
        {
            foreach(var path in moved) TryDelete(path);
            throw;
        }
        finally
        {
            foreach(var path in temps) TryDelete(path);
        }
    }

    /// <summary>Writes a Markdown report without building an additional full report string in memory.</summary>
    private static void WriteMarkdown(string path, IReadOnlyList<Finding> findings, DateTimeOffset generatedAt)
    {
        using var writer = CreateWriter(path);
        writer.WriteLine("# Asset Doctor Report");
        writer.WriteLine();
        writer.WriteLine($"Generated: {generatedAt:O}");
        writer.WriteLine("Detection mode: heuristic quoted-path scan; results are not a complete dependency graph.");
        writer.WriteLine($"Issues: {findings.Count}");
        writer.WriteLine();
        foreach(var finding in findings)
        {
            writer.WriteLine($"## {EscapeMarkdown(finding.RuleId)} · {finding.Severity}");
            writer.WriteLine();
            writer.WriteLine(EscapeMarkdown(finding.Message));
            writer.WriteLine($"- Source: {Code(finding.SourcePath)}");
            if(!string.IsNullOrEmpty(finding.ReferencedPath)) writer.WriteLine($"- Reference: {Code(finding.ReferencedPath)}");
            if(finding.Line.HasValue) writer.WriteLine($"- Line: {finding.Line.Value}");
            if(!string.IsNullOrEmpty(finding.Details)) writer.WriteLine($"- Details: {Code(finding.Details)}");
            writer.WriteLine();
        }
    }

    /// <summary>Writes a plain-text report without building an additional full report string in memory.</summary>
    private static void WriteText(string path, IReadOnlyList<Finding> findings, DateTimeOffset generatedAt)
    {
        using var writer = CreateWriter(path);
        writer.WriteLine("ASSET DOCTOR REPORT");
        writer.WriteLine($"Generated: {generatedAt:O}");
        writer.WriteLine("Detection mode: heuristic quoted-path scan; results are not a complete dependency graph.");
        writer.WriteLine($"Issues: {findings.Count}");
        writer.WriteLine();
        foreach(var finding in findings)
        {
            writer.WriteLine($"{finding.RuleId} · {finding.Severity} · {Plain(finding.Message)}");
            writer.WriteLine($"Source: {Plain(finding.SourcePath)}");
            if(!string.IsNullOrEmpty(finding.ReferencedPath)) writer.WriteLine($"Reference: {Plain(finding.ReferencedPath)}");
            if(finding.Line.HasValue) writer.WriteLine($"Line: {finding.Line.Value}");
            if(!string.IsNullOrEmpty(finding.Details)) writer.WriteLine($"Details: {Plain(finding.Details)}");
            writer.WriteLine();
        }
    }

    /// <summary>Writes a machine-readable JSON report without requiring external serializer packages.</summary>
    private static void WriteJson(string path, IReadOnlyList<Finding> findings, DateTimeOffset generatedAt)
    {
        using var writer = CreateWriter(path);
        writer.WriteLine("{");
        writer.WriteLine($"  \"generatedAt\": {Json(generatedAt.ToString("O", CultureInfo.InvariantCulture))},");
        writer.WriteLine("  \"detectionMode\": \"heuristic quoted-path scan; not a complete dependency graph\",");
        writer.WriteLine("  \"findings\": [");
        for(var index = 0; index < findings.Count; index++)
        {
            var finding = findings[index];
            writer.Write("    { \"ruleId\": "); writer.Write(Json(finding.RuleId));
            writer.Write(", \"severity\": "); writer.Write(Json(finding.Severity.ToString()));
            writer.Write(", \"message\": "); writer.Write(Json(finding.Message));
            writer.Write(", \"sourcePath\": "); writer.Write(Json(finding.SourcePath));
            writer.Write(", \"referencedPath\": "); writer.Write(Json(finding.ReferencedPath));
            writer.Write(", \"line\": "); writer.Write(finding.Line?.ToString(CultureInfo.InvariantCulture) ?? "null");
            writer.Write(", \"details\": "); writer.Write(Json(finding.Details));
            writer.Write(" }");
            if(index + 1 < findings.Count) writer.Write(',');
            writer.WriteLine();
        }
        writer.WriteLine("  ]");
        writer.WriteLine("}");
    }

    /// <summary>Creates a UTF-8 writer for one temporary report.</summary>
    private static StreamWriter CreateWriter(string path) => new(path, false, new UTF8Encoding(false));

    /// <summary>Escapes a JSON string including control characters.</summary>
    private static string Json(string? value)
    {
        if(value == null) return "null";
        var builder = new StringBuilder(value.Length + 2).Append('"');
        foreach(var character in value)
        {
            switch(character)
            {
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if(character < ' ') builder.Append($"\\u{(int)character:X4}");
                    else builder.Append(character);
                    break;
            }
        }
        return builder.Append('"').ToString();
    }

    /// <summary>Escapes Markdown characters that could alter report structure.</summary>
    private static string EscapeMarkdown(string? value) => Plain(value).Replace("\\", "\\\\").Replace("`", "\\`").Replace("*", "\\*").Replace("_", "\\_").Replace("[", "\\[").Replace("]", "\\]").Replace("#", "\\#").Replace("|", "\\|").Replace("!", "\\!").Replace("~", "\\~").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>Uses a variable-length inline-code delimiter so embedded backticks remain literal.</summary>
    private static string Code(string? value)
    {
        var text = Plain(value);
        var fence = "`";
        while(text.Contains(fence, StringComparison.Ordinal)) fence += "`";
        return fence + text + fence;
    }

    /// <summary>Renders selected control and directional characters visibly to reduce report spoofing.</summary>
    private static string Plain(string? value)
    {
        if(string.IsNullOrEmpty(value)) return string.Empty;
        var builder = new StringBuilder(value.Length);
        foreach(var character in value) builder.Append(char.IsControl(character) || character == '\u202E' ? $"\\u{(int)character:X4}" : character);
        return builder.ToString();
    }

    /// <summary>Replaces filename characters that are invalid on the current platform.</summary>
    private static string SanitizeFileName(string value) => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));

    /// <summary>Deletes temporary or rolled-back files without masking the primary export error.</summary>
    private static void TryDelete(string path)
    {
        try { if(File.Exists(path)) File.Delete(path); }
        catch { }
    }

    /// <summary>Returns a deterministic severity sort rank.</summary>
    private static int Rank(FindingSeverity severity) => severity == FindingSeverity.Error ? 2 : severity == FindingSeverity.Warning ? 1 : 0;
}

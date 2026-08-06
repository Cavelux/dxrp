#nullable enable
using System;
using System.Text;

namespace AssetDoctor;

/// <summary>Validates that extracted references are safe portable asset paths.</summary>
public static class AssetPathValidator
{
    /// <summary>Returns the highest-priority finding for a reference, or null when no issue is detected.</summary>
    public static Finding? Validate(string sourcePath, string? referencedPath, int? line = null)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(referencedPath)) return null;
            if(!string.Equals(referencedPath, referencedPath.Trim(), StringComparison.Ordinal)) return New("AD109", FindingSeverity.Warning, "Asset path has leading or trailing whitespace.", sourcePath, referencedPath, line);
            var path = AssetPathRules.NormalizeSeparators(referencedPath);
            foreach(var character in path) if(char.IsControl(character) || character == '\u202E') return New("AD101", FindingSeverity.Error, "Asset path contains unsafe control or directional characters.", sourcePath, referencedPath, line);
            if(path.StartsWith("mount://", StringComparison.OrdinalIgnoreCase)) return New("AD102", FindingSeverity.Error, "Mounted assets cannot be included in a published package.", sourcePath, referencedPath, line);
            if(path.Contains("://", StringComparison.Ordinal)) return New("AD106", FindingSeverity.Error, "External URI used where a project-relative asset path is expected.", sourcePath, referencedPath, line);
            var drivePath = path.Length >= 3 && ((path[0] is >= 'A' and <= 'Z') || (path[0] is >= 'a' and <= 'z')) && path[1] == ':' && path[2] == '/';
            if(path.StartsWith("/", StringComparison.Ordinal) || drivePath || path.IndexOf(':') >= 0) return New("AD107", FindingSeverity.Error, "Absolute or drive-relative asset paths are not portable.", sourcePath, referencedPath, line);
            var decoded = TryPercentDecode(path);
            foreach(var segment in decoded.Split('/')) if(segment == "..") return New("AD108", FindingSeverity.Error, "Parent-directory traversal is not allowed in asset paths.", sourcePath, referencedPath, line);
            if(path.StartsWith("./", StringComparison.Ordinal) || path.Contains("//", StringComparison.Ordinal) || path.Contains("/./", StringComparison.Ordinal) || !path.IsNormalized(NormalizationForm.FormC)) return New("AD109", FindingSeverity.Warning, "Asset path is not canonical.", sourcePath, referencedPath, line);
            return null;
        }
        catch(Exception exception) when(exception is ArgumentException || exception is UriFormatException)
        {
            return New("AD101", FindingSeverity.Error, "Asset path contains invalid Unicode or encoding.", sourcePath, referencedPath ?? string.Empty, line);
        }
    }

    /// <summary>Attempts to decode percent escapes for traversal detection without changing the reported original path.</summary>
    private static string TryPercentDecode(string path)
    {
        try { return Uri.UnescapeDataString(path); }
        catch(UriFormatException) { return path; }
    }

    /// <summary>Creates a finding with consistent source-location metadata.</summary>
    private static Finding New(string ruleId, FindingSeverity severity, string message, string source, string reference, int? line) => new(ruleId, severity, message, source, reference, line);
}

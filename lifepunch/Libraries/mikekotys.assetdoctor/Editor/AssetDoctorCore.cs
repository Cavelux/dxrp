#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace AssetDoctor;

/// <summary>Describes the severity assigned to a validation result.</summary>
public enum FindingSeverity { Info, Warning, Error }

/// <summary>Represents one diagnostic produced by a scan.</summary>
public sealed record Finding(string RuleId, FindingSeverity Severity, string Message, string SourcePath, string? ReferencedPath = null, int? Line = null, string? Details = null);

/// <summary>Stores shared asset-path constants and normalization helpers.</summary>
public static class AssetPathRules
{
    /// <summary>Limits an individual quoted candidate to prevent pathological scans.</summary>
    public const int MaxReferenceLength = 4096;
    /// <summary>Limits extracted references from one source file to protect scan memory.</summary>
    public const int MaxReferencesPerSourceFile = 10_000;
    /// <summary>Limits physical source-file bytes before allocating text in memory.</summary>
    public const long MaxTextFileBytes = 4_000_000;
    /// <summary>Limits decoded source characters after a successful read.</summary>
    public const int MaxTextCharacters = 4_000_000;
    /// <summary>Lists source formats whose quoted values are currently scanned heuristically.</summary>
    public static readonly string[] TextAssetExtensions = { ".scene", ".prefab", ".vmdl", ".vmat", ".vtex", ".sound", ".surface", ".clothing", ".decal", ".vmap", ".vfx", ".vanmgrph", ".vpost", ".shader", ".shdrgrph", ".vpcf", ".json" };
    /// <summary>Lists referenced asset extensions recognized inside quoted source values.</summary>
    public static readonly string[] ReferenceExtensions = { ".scene", ".prefab", ".vmdl", ".vmat", ".vtex", ".sound", ".surface", ".clothing", ".decal", ".vmap", ".vfx", ".vanmgrph", ".vpost", ".shader", ".shdrgrph", ".vpcf", ".png", ".jpg", ".jpeg", ".tga", ".fbx", ".json" };
    /// <summary>Returns whether a path ends in one of the supplied extensions.</summary>
    public static bool HasAnyExtension(string path, IReadOnlyList<string> extensions)
    {
        if(string.IsNullOrEmpty(path)) return false;
        for(var index = 0; index < extensions.Count; index++) if(path.EndsWith(extensions[index], StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    /// <summary>Converts one or more Windows separators without removing unsafe whitespace.</summary>
    public static string NormalizeSeparators(string path) => (path ?? string.Empty).Replace('\\', '/');
    /// <summary>Returns whether references in this source format should use JSON-style escape decoding.</summary>
    public static bool UsesJsonEscapes(string path) => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".scene", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Represents one extracted asset path and its one-based source line.</summary>
public sealed record AssetReference(string Path, int Line);

/// <summary>Contains bounded reference extraction output and indicates whether the per-file limit was reached.</summary>
public sealed record ReferenceExtractionResult(IReadOnlyList<AssetReference> References, bool IsTruncated);

/// <summary>Extracts quoted asset paths with bounded linear-time scanning.</summary>
public static class ReferenceExtractor
{
    /// <summary>Extracts recognized references from text; output is heuristic because only quoted values are examined.</summary>
    public static ReferenceExtractionResult Extract(string? text, bool decodeJsonEscapes, CancellationToken token = default)
    {
        var results = new List<AssetReference>();
        if(string.IsNullOrEmpty(text)) return new ReferenceExtractionResult(results, false);
        var line = 1;
        for(var index = 0; index < text.Length; index++)
        {
            if((index & 0xFFF) == 0) token.ThrowIfCancellationRequested();
            if(text[index] == '\n') { line++; continue; }
            var quote = text[index];
            if(quote != '\'' && quote != '"') continue;
            var startLine = line;
            var start = ++index;
            var escaped = false;
            var tooLong = false;
            var closed = false;
            while(index < text.Length)
            {
                if((index & 0xFFF) == 0) token.ThrowIfCancellationRequested();
                var character = text[index];
                if(character == '\n') line++;
                if(character == quote && !escaped) { closed = true; break; }
                if(index - start >= AssetPathRules.MaxReferenceLength) tooLong = true;
                escaped = character == '\\' ? !escaped : false;
                index++;
            }
            if(!closed || tooLong) continue;
            var raw = text.Substring(start, index - start);
            if(!TryDecodeEscapes(raw, decodeJsonEscapes, out var decoded)) continue;
            var normalized = AssetPathRules.NormalizeSeparators(decoded);
            if(normalized.Length == 0 || !AssetPathRules.HasAnyExtension(normalized, AssetPathRules.ReferenceExtensions)) continue;
            results.Add(new AssetReference(normalized, startLine));
            if(results.Count >= AssetPathRules.MaxReferencesPerSourceFile)
                return new ReferenceExtractionResult(results, true);
        }
        return new ReferenceExtractionResult(results, false);
    }

    /// <summary>Decodes JSON escapes only for JSON-like source formats and preserves ordinary backslash paths otherwise.</summary>
    private static bool TryDecodeEscapes(string raw, bool decodeJsonEscapes, out string value)
    {
        if(!decodeJsonEscapes) { value = raw; return true; }
        var builder = new StringBuilder(raw.Length);
        for(var index = 0; index < raw.Length; index++)
        {
            var character = raw[index];
            if(character != '\\') { builder.Append(character); continue; }
            if(++index >= raw.Length) { value = string.Empty; return false; }
            switch(raw[index])
            {
                case '"': builder.Append('"'); break;
                case '\\': builder.Append('\\'); break;
                case '/': builder.Append('/'); break;
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case 'u' when index + 4 < raw.Length:
                    var hex = raw.Substring(index + 1, 4);
                    if(!ushort.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var code)) { value = string.Empty; return false; }
                    builder.Append((char)code); index += 4; break;
                default: value = string.Empty; return false;
            }
        }
        value = builder.ToString();
        return true;
    }
}

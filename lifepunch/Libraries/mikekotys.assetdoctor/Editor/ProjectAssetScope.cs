#nullable enable
using System;
using System.IO;

namespace AssetDoctor;

/// <summary>Limits scans to physical source files beneath the current project's Assets directory.</summary>
public sealed class ProjectAssetScope
{
    /// <summary>Stores the normalized project Assets directory.</summary>
    private readonly string _assetsRoot;

    /// <summary>Stores the platform-selected path comparison mode.</summary>
    private readonly StringComparison _pathComparison;

    /// <summary>Creates a scope for the active project, or returns null when its Assets path is unavailable or invalid.</summary>
    public static ProjectAssetScope? TryCreate()
    {
        var assetsPath = Project.Current?.GetAssetsPath();
        if(string.IsNullOrWhiteSpace(assetsPath)) return null;
        try { return new ProjectAssetScope(assetsPath); }
        catch(Exception exception) { Log.Warning($"Asset Doctor could not resolve project Assets path: {exception.GetType().Name}"); return null; }
    }

    /// <summary>Initializes a normalized project asset scope.</summary>
    private ProjectAssetScope(string assetsPath)
    {
        _assetsRoot = NormalizeDirectory(assetsPath);
        _pathComparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    /// <summary>Returns whether an editor asset belongs to the current project's source Assets directory.</summary>
    public bool Contains(Editor.Asset? asset)
    {
        if(asset == null || !asset.HasSourceFile || string.IsNullOrWhiteSpace(asset.AbsolutePath)) return false;
        try { return Path.GetFullPath(asset.AbsolutePath).StartsWith(_assetsRoot, _pathComparison); }
        catch(Exception exception) { Log.Warning($"Asset Doctor could not scope '{asset.Path}': {exception.GetType().Name}"); return false; }
    }

    /// <summary>Normalizes a directory with one trailing separator for safe prefix matching.</summary>
    private static string NormalizeDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath + Path.DirectorySeparatorChar;
    }
}

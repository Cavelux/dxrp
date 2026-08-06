#nullable enable
using System;

namespace AssetDoctor;

/// <summary>Focuses an editor-known asset in its Asset Browser view.</summary>
public static class AssetBrowserNavigator
{
    /// <summary>Attempts to focus an asset by logical path and falls back to highlighting the path.</summary>
    public static void FocusAsset(string path)
    {
        if(string.IsNullOrWhiteSpace(path)) return;
        var asset = AssetSystem.All.FirstOrDefault(item =>
            item != null && string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
        if(asset == null)
        {
            EditorEvent.Run("assetsystem.highlight", path);
            Log.Warning($"Asset Doctor could not select '{path}'; sent an Asset Browser highlight instead.");
            return;
        }

        var browser = AssetBrowser.Get();
        var assetBrowser = browser?.GetBrowser(asset);
        if(assetBrowser == null)
        {
            Log.Warning($"Asset Doctor could not locate an Asset Browser view for '{path}'.");
            return;
        }

        assetBrowser.FocusOnAsset(asset, true);
    }
}

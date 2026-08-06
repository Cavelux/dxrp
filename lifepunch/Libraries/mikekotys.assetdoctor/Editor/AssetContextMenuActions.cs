#nullable enable
namespace AssetDoctor;

/// <summary>Adds Asset Doctor direct-link actions to the Asset Browser context menu.</summary>
public static class AssetContextMenuActions
{
    /// <summary>Stores the nested menu path for reverse dependency lookup.</summary>
    private static readonly string[] FindDependantsMenuPath = { "Asset Doctor", "Find Assets Using This" };
    /// <summary>Stores the nested menu path for forward dependency lookup.</summary>
    private static readonly string[] FindReferencesMenuPath = { "Asset Doctor", "Find Assets Used By This" };

    /// <summary>Registers direct-link actions when exactly one valid Asset Browser entry is selected.</summary>
    [Event("asset.contextmenu")]
    private static void OnAssetContextMenu(AssetContextMenu context)
    {
        if(context.SelectedList == null || context.SelectedList.Count != 1) return;
        var asset = context.SelectedList[0].Asset;
        if(asset == null || string.IsNullOrWhiteSpace(asset.Path)) return;
        var added = false;

        context.Menu.AboutToShow += () =>
        {
            if(added) return;
            added = true;

            context.Menu.AddSeparator();
            context.Menu.AddOption(
                FindDependantsMenuPath,
                "manage_search",
                () => new AssetLinksWindow(asset, true),
                "Show assets that directly use this asset");

            context.Menu.AddOption(
                FindReferencesMenuPath,
                "account_tree",
                () => new AssetLinksWindow(asset, false),
                "Show assets directly used by this asset");
        };
    }
}

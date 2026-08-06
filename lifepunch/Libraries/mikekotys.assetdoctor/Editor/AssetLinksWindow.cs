#nullable enable
using System;
using System.Linq;

namespace AssetDoctor;

/// <summary>Shows direct native s&box references or dependants for one selected asset.</summary>
public sealed class AssetLinksWindow : Widget
{
    /// <summary>Limits synchronous row creation while retaining the true lookup count.</summary>
    private const int MaxRenderedAssets = 500;

    /// <summary>Creates and displays a compact direct-link inspector.</summary>
    public AssetLinksWindow(Asset asset, bool showDependants) : base(null)
    {
        if(asset == null) throw new ArgumentNullException(nameof(asset));
        WindowTitle = showDependants ? "Find Assets Using This" : "Find Assets Used By This";
        MinimumSize = new Vector2(420, 260);
        Size = new Vector2(520, 340);
        Layout = Layout.Column();
        Layout.Margin = 6;
        Layout.Spacing = 4;
        Layout.Add(new Label(showDependants ? $"Assets directly using: {asset.Path}" : $"Assets directly used by: {asset.Path}", this));
        Asset[] allLinks;
        try
        {
            allLinks = (showDependants ? asset.GetDependants(false) : asset.GetReferences(false))?
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Path))
                .GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<Asset>();
        }
        catch(Exception exception)
        {
            Log.Error($"Asset Doctor link lookup failed: {exception}");
            Layout.Add(new Label($"Lookup failed: {exception.GetType().Name}", this));
            Show();
            return;
        }
        var renderedLinks = allLinks.Take(MaxRenderedAssets).ToArray();
        Layout.Add(new Label($"Found {allLinks.Length} direct asset(s)", this));
        var scroll = new ScrollArea(this);
        Layout.Add(scroll);
        var content = new Widget(null) { Layout = Layout.Column() };
        content.Layout.Spacing = 2;
        scroll.Canvas = content;
        foreach(var link in renderedLinks)
        {
            var target = link;
            var row = new FindingRow(content, target.Path, "#1d2c3a", "#3a6d8f", "#c7e8ff");
            row.Clicked += () => AssetBrowserNavigator.FocusAsset(target.Path);
            content.Layout.Add(row);
        }
        if(allLinks.Length == 0) content.Layout.Add(new Label("No direct asset links found.", content));
        if(allLinks.Length > renderedLinks.Length) content.Layout.Add(new Label($"… {allLinks.Length - renderedLinks.Length} more asset(s) were not rendered to protect Editor responsiveness.", content));
        content.Layout.AddStretchCell();
        Show();
    }
}

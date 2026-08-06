using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.UI;

namespace Goo;

/// <summary>
/// Factories for skin (border-image) styling on a <see cref="Container"/>.
/// </summary>
public static class Skins
{
    /// <summary>Dev-diagnostic sink for the zero-width-border nine-slice case; null by default so non-logging contexts (tests) can use the helper.</summary>
    public static Action<string>? OnZeroBorder;

    /// <summary>A nine-slice skinned <see cref="Container"/> that sets source, slice insets, and rendered border together so the slice cannot be set without a width. <paramref name="sliceSrcPx"/> is the corner-art size in SOURCE-image pixels (measure the PNG); <paramref name="renderedBorder"/> is the on-screen corner size, typically 16-48 regardless of source resolution - do not set it equal to a hi-res slice.</summary>
    public static Container NineSlice(
        Texture? source,
        float sliceSrcPx,
        float renderedBorder,
        BorderImageRepeat repeat = BorderImageRepeat.Stretch,
        BorderImageFill fill = BorderImageFill.Filled,
        string? key = null)
    {
        if (sliceSrcPx <= 0f)
            throw new ArgumentException("sliceSrcPx must be > 0 (the corner-art inset in source pixels)", nameof(sliceSrcPx));

        if (ShouldWarnZeroBorder(source != null, renderedBorder))
            OnZeroBorder?.Invoke($"Goo.Skins.NineSlice: a BorderImageSource is set but renderedBorder is {renderedBorder}; the skin paints into a zero-width border and renders nothing. Pass renderedBorder > 0.");

        return new Container
        {
            Key                    = key,
            Width                  = Length.Percent(100),
            Height                 = Length.Percent(100),
            BorderImageSource      = source,
            BorderImageWidthLeft   = sliceSrcPx,
            BorderImageWidthTop    = sliceSrcPx,
            BorderImageWidthRight  = sliceSrcPx,
            BorderImageWidthBottom = sliceSrcPx,
            BorderWidth            = renderedBorder,
            BorderImageRepeat      = repeat,
            BorderImageFill        = fill,
        };
    }

    // The silent-blank footgun: a source paints into a zero-thickness border and shows nothing.
    internal static bool ShouldWarnZeroBorder(bool hasSource, float renderedBorder)
        => hasSource && renderedBorder <= 0f;

    static readonly Dictionary<string, Texture> s_pathCache = new();

    /// <summary>Path-loading overload. Lazy-loads via <c>FileSystem.Mounted</c> and caches per path; a failed load is NOT cached, so it retries once assets mount instead of pinning null forever.</summary>
    public static Container NineSlice(
        string path,
        float sliceSrcPx,
        float renderedBorder,
        BorderImageRepeat repeat = BorderImageRepeat.Stretch,
        BorderImageFill fill = BorderImageFill.Filled,
        string? key = null)
    {
        if (!s_pathCache.TryGetValue(path, out var tex))
        {
            tex = Texture.LoadFromFileSystem(path, FileSystem.Mounted);
            if (tex is not null) s_pathCache[path] = tex;
        }
        return NineSlice(tex, sliceSrcPx, renderedBorder, repeat, fill, key);
    }
}

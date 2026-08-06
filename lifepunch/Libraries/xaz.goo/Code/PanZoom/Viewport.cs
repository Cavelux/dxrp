using System;
using Sandbox;

namespace Goo;

/// <summary>
/// Pure pan/zoom transform state for building canvases. No Panel dependency, fully unit-tested.
/// Model: <c>screen = world * Zoom + Pan</c>, origin top-left. A consumer drives <see cref="PanBy"/>
/// from drag deltas and <see cref="ZoomAt"/> from the mouse wheel, then feeds <see cref="Zoom"/>/<see cref="Pan"/>
/// into a <c>Transform = Scale(Zoom).Translate(Pan)</c> on the world container (with its transform-origin
/// pinned to top-left so the scale pivots at the local origin, matching this math).
/// </summary>
public sealed class Viewport
{
    /// <summary>Translation in screen pixels.</summary>
    public Vector2 Pan { get; private set; }

    /// <summary>Uniform scale factor; 1 = no zoom. Always within [<see cref="MinZoom"/>, <see cref="MaxZoom"/>].</summary>
    public float Zoom { get; private set; } = 1f;

    /// <summary>Lower zoom clamp.</summary>
    public float MinZoom { get; init; } = 0.1f;

    /// <summary>Upper zoom clamp.</summary>
    public float MaxZoom { get; init; } = 10f;

    /// <summary>Maps a world-space point to its screen-space position.</summary>
    public Vector2 WorldToScreen(Vector2 world) => world * Zoom + Pan;

    /// <summary>Maps a screen-space point back to world space.</summary>
    public Vector2 ScreenToWorld(Vector2 screen) => (screen - Pan) / Zoom;

    /// <summary>Pans by a screen-space delta (e.g. a drag movement).</summary>
    public void PanBy(Vector2 screenDelta) => Pan += screenDelta;

    /// <summary>Pans (zoom unchanged) so <paramref name="world"/> lands under <paramref name="screenPoint"/> - e.g. centre a node by passing the viewport centre.</summary>
    public void CenterOn(Vector2 world, Vector2 screenPoint) => Pan = screenPoint - world * Zoom;

    /// <summary>
    /// Multiplies zoom by <paramref name="factor"/> (clamped) while keeping the world point currently
    /// under <paramref name="screenAnchor"/> fixed under it - i.e. zoom-to-cursor.
    /// </summary>
    public void ZoomAt(Vector2 screenAnchor, float factor)
    {
        var newZoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        var worldAtAnchor = ScreenToWorld(screenAnchor);
        Zoom = newZoom;
        Pan = screenAnchor - worldAtAnchor * newZoom;
    }
}

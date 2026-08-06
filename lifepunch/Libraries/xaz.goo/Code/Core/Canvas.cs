using Sandbox;
using Sandbox.UI;

namespace Goo;

/// <summary>
/// Immediate-mode custom-draw callback for a <see cref="Container"/>. Invoked once per paint with a
/// <see cref="Canvas"/> scoped to the panel's box.
/// </summary>
public delegate void DrawCallback(Canvas canvas);

/// <summary>
/// Drawing surface handed to a <see cref="Container.Draw"/> callback. Wraps the engine's batched
/// <c>Panel.Draw.*</c> primitives. Only valid for the duration of the callback: it is a ref struct and
/// cannot be stored, boxed, or captured.
/// </summary>
public readonly ref struct Canvas
{
    readonly Rect _rect;

    internal Canvas(Rect rect) => _rect = rect;

    // Panel.Draw.* renders in SCREEN-space px (the shader channel draws its quad at the panel's
    // screen Box.Rect too), but callbacks author in panel-LOCAL coords (origin top-left, see
    // LocalRect). Translate every primitive by the panel's screen position so local drawing lands
    // inside the panel wherever it sits. Full-screen panels have Position ~ (0,0), so this is a
    // no-op for them; offset panels (e.g. a draggable window) now track their box.
    Rect    Map(Rect r)    => new Rect(r.Position + _rect.Position, r.Size);
    Vector2 Map(Vector2 p) => p + _rect.Position;

    /// <summary>Panel width in pixels.</summary>
    public float Width => _rect.Width;

    /// <summary>Panel height in pixels.</summary>
    public float Height => _rect.Height;

    /// <summary>Panel size in pixels.</summary>
    public Vector2 Size => _rect.Size;

    /// <summary>Panel-local rect, origin at top-left.</summary>
    public Rect LocalRect => new Rect(0f, 0f, _rect.Width, _rect.Height);

    /// <summary>Filled (optionally uniformly-rounded) rectangle.</summary>
    public void Rect(Rect rect, Color color, float cornerRadius = 0f)
        => Panel.Draw.Rect(Map(rect), color, cornerRadius);

    /// <summary>Filled rectangle with per-corner radius (bottom-right, top-right, bottom-left, top-left).</summary>
    public void Rect(Rect rect, Color color, Vector4 cornerRadius)
        => Panel.Draw.Rect(Map(rect), color, cornerRadius);

    /// <summary>Filled circle.</summary>
    public void Circle(Vector2 center, float radius, Color color)
        => Panel.Draw.Circle(Map(center), radius, color);

    /// <summary>Texture drawn into a rect.</summary>
    public void Texture(Texture texture, Rect rect, Color? tint = null)
        => Panel.Draw.Texture(texture, Map(rect), tint);

    /// <summary>Text laid out within a rect.</summary>
    public void Text(string text, Rect rect, float size, Color color, TextFlag flags = TextFlag.LeftTop)
        => Panel.Draw.Text(text, Map(rect), size, color, flags);

    /// <summary>Box shadow (drop or inset). offset stays a delta, not a position.</summary>
    public void Shadow(Rect rect, Color color, float blur = 0f, float spread = 0f, Vector2 offset = default, float cornerRadius = 0f, bool inset = false)
        => Panel.Draw.Shadow(Map(rect), color, blur, spread, offset, cornerRadius, inset);

    /// <summary>Outline (stroke) around a rect.</summary>
    public void Outline(Rect rect, Color color, float width, float cornerRadius = 0f, float offset = 0f)
        => Panel.Draw.Outline(Map(rect), color, width, cornerRadius, offset);
}

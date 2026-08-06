using System;
using System.Collections.Immutable;
using Sandbox;

namespace Goo;

// Geometry payload for Sector / Arc Blobs.
// Field meaning by BlobKind:
//   Sector: A=StartAngle (deg, 0=up, cw+), B=EndAngle, C=InnerRadius (0..1), D=OuterRadius (0..1), E=CornerRadius (0..1, fraction of outer radius)
//   Arc:    A=StartAngle,                  B=EndAngle,  C=Radius (0..1, centerline), D=StrokeWidth (0..1), E=unused
public struct ShapeParams : System.IEquatable<ShapeParams>
{
    public float A;
    public float B;
    public float C;
    public float D;
    public float E;   // Sector: CornerRadius (0..1 fraction of outer radius). Arc: unused (0).
    public float F;   // RectHitTest flag (0/1) for all shape kinds; rides the params so diffs propagate.

    public bool Equals(ShapeParams other) => A == other.A && B == other.B && C == other.C && D == other.D && E == other.E && F == other.F;
    public override bool Equals(object? obj) => obj is ShapeParams p && Equals(p);
    public override int GetHashCode() => System.HashCode.Combine(A, B, C, D, E, F);
    public static bool operator ==(ShapeParams a, ShapeParams b) => a.Equals(b);
    public static bool operator !=(ShapeParams a, ShapeParams b) => !a.Equals(b);

    // Stable 64-bit hash of (kind, A, B, C, D) -- excludes E; used as a debug-name suffix only.
    public long PackKey(BlobKind kind)
    {
        long qAngle(float deg)
        {
            float wrapped = deg - 360f * System.MathF.Floor(deg / 360f);
            int n = (int)System.MathF.Round(wrapped * (16384f / 360f));
            if (n == 16384) n = 0;
            return (long)n;
        }
        long qUnit(float u)
        {
            int n = (int)System.MathF.Round(u * 16384f);
            if (n < 0) n = 0;
            if (n > 16383) n = 16383;
            return (long)n;
        }
        return ((long)(byte)kind)
             | (qAngle(A) << 8)
             | (qAngle(B) << 22)
             | (qUnit(C) << 36)
             | (qUnit(D) << 50);
    }
}

internal struct Frame
{
    public BlobKind Kind;
    public string? Key;
    public string Content;     // Text-only; empty for non-Text
    public bool IsRich;        // Text-only; false for plain text
    public StyleList Style;    // Container-only; null until StyleList added
    public Children? Children; // Container-only; null otherwise
    public BlobEvents Events;  // Container/Text/Image/ScenePanel; default = all null
    public Texture? Texture;   // Image-only; null for non-Image
    public string? Path;       // Image (texture path), ScenePanel (.scene path), SvgPanel (svg path), or WebPanel (URL); null otherwise
    public Scene? Scene;       // ScenePanel-only; null for non-ScenePanel
    public bool RenderOnce;    // ScenePanel-only; false for non-ScenePanel
    // Embed-only carry-field; null for non-Embed kinds.
    internal EmbedConfig? Embed;
    // Virtual-only carry-field; null for non-Virtual kinds.
    internal VirtualConfig? Virtual;
    public bool Paused;        // WebPanel-only; false for non-WebPanel
    public string? Color;     // SvgPanel-only; null for non-SvgPanel
    // TextEntry-only carry-fields. Path slot is reused for the text value via
    // the Op.Text alias; these are the additional control-surface fields.
    public string? Placeholder;
    public int?    MaxLength;
    public bool    Disabled;
    public bool    Numeric;
    public float?  MinValue;
    public float?  MaxValue;
    public string? NumberFormat;
    public bool    Multiline;
    public int?    MinLength;
    public string? CharacterRegex;
    public string? StringRegex;
    public Func<char, bool>?   CanEnterChar;
    public Func<string, bool>? Validate;
    public Action<bool>?       OnValidationChanged;
    public Action<string>? OnChange;
    public Action<string>? OnSubmit;
    public Action?         OnFocus;
    public Action<string>? OnBlur;
    public Action?         OnCancel;
    public bool    IsControlled;
    public bool    ValueAndDefaultTextBothSet;
    public ShapeParams Shape;       // Sector / Arc only; default for non-shape kinds
    public Vector2[]? Points;       // Polygon-only; null for non-Polygon kinds
    // Custom-shader channel (Container only): a self-applying ShaderEffect, drawn via IPanelDraw.
    public ShaderEffect? Effect;
    // Global effect-addressing tag (Container only): resolves an effect from the host manifest.
    public string? Tag;
    // Custom-draw callback (Container only). Invoked from StatefulDrawPanel.OnDraw via the Canvas facade.
    public DrawCallback? Draw;
    // Declared layout-move transition (Container only); null = snap.
    public LayoutTransition? LayoutTransition;
    // Cell-only carry-fields; null/default for non-Cell kinds.
    public Func<Cell>?    CellFactory;   // first-mount path (new TCell())
    public Action<Cell>?  Seed;          // applied once at first mount only, in the fresh-create branch of DiffCell
    public Action<Cell>?  Configure;     // refresh props on the persistent instance each rebuild
    public Type?          CellType;      // typeof(TCell): reuse-vs-fresh match check
}

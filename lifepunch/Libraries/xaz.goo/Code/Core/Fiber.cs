using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Sandbox;

namespace Goo;

internal sealed class Fiber
{
    public BlobKind Kind;
    public string? Key;
    public string Content = "";
    public bool IsRich;
    public StyleList Style = StyleList.Empty;
    public List<Fiber>? Children;
    public Texture? Texture;
    public string? Path;          // Image (texture path), ScenePanel (.scene path), SvgPanel (svg path), or WebPanel (URL)
    public Scene? Scene;
    public bool RenderOnce;
    public bool Paused;             // WebPanel only
    public string? Color;
    // TextEntry-only carry-fields. Path slot is reused for the text value.
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
    public BlobEvents PrevEvents;
    public ShapeParams Shape;       // Sector / Arc geometry; Polygon uses only the F (RectHitTest) flag
    public Vector2[]? Points;       // Polygon only
    // Custom-shader channel (Container only). Previous-render snapshot for delta comparison.
    public ShaderEffect? Effect;
    // Custom-draw callback (Container only). Previous-render snapshot for delta comparison.
    public DrawCallback? Draw;
    // Declared layout-move transition (Container only). Previous-render snapshot for delta comparison.
    public LayoutTransition? LayoutTransition;
    public VirtualConfig? Virtual;  // Virtual only: last-applied items/row config
    public object? Instance;        // Cell only: the persistent self-owning state instance
}

using System;
using System.Collections.Immutable;
using Sandbox;

namespace Goo;

public enum OpKind : byte
{
    CreateText,
    UpdateText,
    RemoveAt,
    CreateContainer,
    CreateImage,
    UpdateImage,
    CreateScenePanel,
    UpdateScenePanel,
    CreateSvgPanel,
    UpdateSvgPanel,
    CreateSector,
    UpdateSector,
    CreateArc,
    UpdateArc,
    CreatePolygon,
    UpdatePolygon,
    CreateWebPanel,
    UpdateWebPanel,
    CreateTextEntry,
    UpdateTextEntry,
    CreateEmbed,
    UpdateEmbed,
    CreateVirtual,
    UpdateVirtual,
    MoveAt,
    SetStyle,
    SetEvents,
    SetDraw,
    SetEffect,
    SetLayoutTransition,
}

// Discriminated value-type union: one struct with name-overlapped fields carries all op variants, so List<Op> is a poolable value buffer. BlobEvents (six delegate refs) is the heaviest variant (~64B/slot).
public struct Op : IEquatable<Op>
{
    public OpKind Kind;
    public int[] HostPath;

    // Index meaning by kind: Create*/Update*/Remove* target index; MoveAt source index.
    public int Index;
    public int ToIndex;

    // Text Content for CreateText/UpdateText; image Path for CreateImage/UpdateImage.
    public string? StringPayload;
    public bool IsRich;
    public Texture? Texture;

    internal StyleList? Style;
    internal BlobEvents Events;

    // ScenePanel-only carry-fields. Scene is public to match Texture's posture
    // (engine refs callers may inspect); RenderOnce is value-typed flag.
    public Scene? Scene;
    public bool RenderOnce;

    // WebPanel-only carry-field; maps to engine WebSurface.InBackgroundMode.
    public bool Paused;

    // SvgPanel-only carry-field.
    public string? Color;

    // TextEntry carry-fields. StringPayload (already declared) carries the text value.
    public string? Placeholder;
    public int?    MaxLength;
    public bool    Disabled;
    public bool    Numeric;
    public float?  MinValue;
    public float?  MaxValue;
    public string? NumberFormat;
    public bool    Multiline;
    // Validation carry-fields (string-pattern surface mirrors engine TextEntry.Validation).
    public int?    MinLength;
    public string? CharacterRegex;
    public string? StringRegex;
    // Validation delegates. Rebound every Update; delta-detected by ReferenceEquals.
    public Func<char, bool>?   CanEnterChar;
    public Func<string, bool>? Validate;
    public Action<bool>?       OnValidationChanged;
    public Action<string>? OnChange;
    public Action<string>? OnSubmit;
    // Interaction callbacks. OnBlur carries the final text; all rebound every Update.
    public Action?         OnFocus;
    public Action<string>? OnBlur;
    public Action?         OnCancel;
    // True in controlled mode (consumer set Value); false when only DefaultText was set.
    public bool IsControlled;

    // Sector / Arc carry-field.
    public ShapeParams Shape;

    // Polygon carry-field. Points are unit-coord [0..1] vertices; treat as immutable.
    public Vector2[]? Points;

    // SetDraw carry-field (Container only).
    public DrawCallback? DrawCallback;

    // SetEffect carry-field (Container only).
    public ShaderEffect? Effect;

    // SetLayoutTransition carry-field (Container only).
    public LayoutTransition? LayoutTransition;

    // Read-only semantic aliases so call sites can use the natural name for the variant.
    // Embed carry-field: create/update delegates, reference-compared.
    internal EmbedConfig? Embed;

    // Virtual carry-field: items/row/layout config, reference-compared.
    internal VirtualConfig? Virtual;

    public string? Content => StringPayload;
    public string? Path => StringPayload;
    public string? ScenePath => StringPayload;
    public string? Url => StringPayload;
    public string? Text => StringPayload;
    public int FromIndex => Index;

    public static Op CreateText(int index, string content, int[]? hostPath = null, bool isRich = false) =>
        new() { Kind = OpKind.CreateText, Index = index, StringPayload = content, IsRich = isRich, HostPath = hostPath ?? Array.Empty<int>() };

    public static Op UpdateText(int index, string content, int[]? hostPath = null, bool isRich = false) =>
        new() { Kind = OpKind.UpdateText, Index = index, StringPayload = content, IsRich = isRich, HostPath = hostPath ?? Array.Empty<int>() };

    public static Op RemoveAt(int index, int[]? hostPath = null) =>
        new() { Kind = OpKind.RemoveAt, Index = index, HostPath = hostPath ?? Array.Empty<int>() };

    public static Op CreateContainer(int index, int[]? hostPath = null) =>
        new() { Kind = OpKind.CreateContainer, Index = index, HostPath = hostPath ?? Array.Empty<int>() };

    public static Op CreateImage(int index, Texture? texture, string? path, int[]? hostPath = null) =>
        new() { Kind = OpKind.CreateImage, Index = index, Texture = texture, StringPayload = path, HostPath = hostPath ?? Array.Empty<int>() };

    public static Op UpdateImage(int index, Texture? texture, string? path, int[]? hostPath = null) =>
        new() { Kind = OpKind.UpdateImage, Index = index, Texture = texture, StringPayload = path, HostPath = hostPath ?? Array.Empty<int>() };

    public static Op CreateScenePanel(int index, Scene? scene, string? scenePath, bool renderOnce, int[]? hostPath = null) =>
        new() {
            Kind = OpKind.CreateScenePanel, Index = index,
            Scene = scene, StringPayload = scenePath, RenderOnce = renderOnce,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    public static Op UpdateScenePanel(int index, Scene? scene, string? scenePath, bool renderOnce, int[]? hostPath = null) =>
        new() {
            Kind = OpKind.UpdateScenePanel, Index = index,
            Scene = scene, StringPayload = scenePath, RenderOnce = renderOnce,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    public static Op CreateSvgPanel(int index, string? path, string? color, int[]? hostPath = null) =>
        new() {
            Kind = OpKind.CreateSvgPanel, Index = index,
            StringPayload = path, Color = color,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    public static Op UpdateSvgPanel(int index, string? path, string? color, int[]? hostPath = null) =>
        new() {
            Kind = OpKind.UpdateSvgPanel, Index = index,
            StringPayload = path, Color = color,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    public static Op CreateSector(int index, in ShapeParams shape, int[]? hostPath = null) =>
        new() {
            Kind = OpKind.CreateSector, Index = index,
            Shape = shape,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    public static Op UpdateSector(int index, in ShapeParams shape, int[]? hostPath = null) =>
        new() {
            Kind = OpKind.UpdateSector, Index = index,
            Shape = shape,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    public static Op CreateArc(int index, in ShapeParams shape, int[]? hostPath = null) =>
        new() {
            Kind = OpKind.CreateArc, Index = index,
            Shape = shape,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    public static Op UpdateArc(int index, in ShapeParams shape, int[]? hostPath = null) =>
        new() {
            Kind = OpKind.UpdateArc, Index = index,
            Shape = shape,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    public static Op CreatePolygon(int index, Vector2[]? points, in ShapeParams shape, int[]? hostPath = null) =>
        new() {
            Kind = OpKind.CreatePolygon, Index = index,
            Points = points, Shape = shape,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    public static Op UpdatePolygon(int index, Vector2[]? points, in ShapeParams shape, int[]? hostPath = null) =>
        new() {
            Kind = OpKind.UpdatePolygon, Index = index,
            Points = points, Shape = shape,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    public static Op CreateWebPanel(int index, string? url, bool paused = false, int[]? hostPath = null) =>
        new() {
            Kind = OpKind.CreateWebPanel, Index = index,
            StringPayload = url, Paused = paused,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    public static Op UpdateWebPanel(int index, string? url, bool paused = false, int[]? hostPath = null) =>
        new() {
            Kind = OpKind.UpdateWebPanel, Index = index,
            StringPayload = url, Paused = paused,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    public static Op CreateTextEntry(
        int index,
        string? text,
        bool isControlled = false,
        string? placeholder = null,
        int? maxLength = null,
        bool disabled = false,
        bool numeric = false,
        float? minValue = null,
        float? maxValue = null,
        string? numberFormat = null,
        bool multiline = false,
        Action<string>? onChange = null,
        Action<string>? onSubmit = null,
        Action? onFocus = null,
        Action<string>? onBlur = null,
        Action? onCancel = null,
        int? minLength = null,
        string? characterRegex = null,
        string? stringRegex = null,
        Func<char, bool>? canEnterChar = null,
        Func<string, bool>? validate = null,
        Action<bool>? onValidationChanged = null,
        int[]? hostPath = null) =>
        new() {
            Kind = OpKind.CreateTextEntry, Index = index,
            StringPayload = text,
            IsControlled = isControlled,
            Placeholder = placeholder,
            MaxLength = maxLength,
            Disabled = disabled,
            Numeric = numeric,
            MinValue = minValue,
            MaxValue = maxValue,
            NumberFormat = numberFormat,
            Multiline = multiline,
            MinLength = minLength,
            CharacterRegex = characterRegex,
            StringRegex = stringRegex,
            CanEnterChar = canEnterChar,
            Validate = validate,
            OnValidationChanged = onValidationChanged,
            OnChange = onChange,
            OnSubmit = onSubmit,
            OnFocus = onFocus,
            OnBlur = onBlur,
            OnCancel = onCancel,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    public static Op UpdateTextEntry(
        int index,
        string? text,
        bool isControlled = false,
        string? placeholder = null,
        int? maxLength = null,
        bool disabled = false,
        bool numeric = false,
        float? minValue = null,
        float? maxValue = null,
        string? numberFormat = null,
        bool multiline = false,
        Action<string>? onChange = null,
        Action<string>? onSubmit = null,
        Action? onFocus = null,
        Action<string>? onBlur = null,
        Action? onCancel = null,
        int? minLength = null,
        string? characterRegex = null,
        string? stringRegex = null,
        Func<char, bool>? canEnterChar = null,
        Func<string, bool>? validate = null,
        Action<bool>? onValidationChanged = null,
        int[]? hostPath = null) =>
        new() {
            Kind = OpKind.UpdateTextEntry, Index = index,
            StringPayload = text,
            IsControlled = isControlled,
            Placeholder = placeholder,
            MaxLength = maxLength,
            Disabled = disabled,
            Numeric = numeric,
            MinValue = minValue,
            MaxValue = maxValue,
            NumberFormat = numberFormat,
            Multiline = multiline,
            MinLength = minLength,
            CharacterRegex = characterRegex,
            StringRegex = stringRegex,
            CanEnterChar = canEnterChar,
            Validate = validate,
            OnValidationChanged = onValidationChanged,
            OnChange = onChange,
            OnSubmit = onSubmit,
            OnFocus = onFocus,
            OnBlur = onBlur,
            OnCancel = onCancel,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    public static Op MoveAt(int fromIndex, int toIndex, int[]? hostPath = null) =>
        new() { Kind = OpKind.MoveAt, Index = fromIndex, ToIndex = toIndex, HostPath = hostPath ?? Array.Empty<int>() };

    internal static Op SetStyle(StyleList style, int[]? hostPath = null) =>
        new() { Kind = OpKind.SetStyle, Style = style, HostPath = hostPath ?? Array.Empty<int>() };

    internal static Op SetEvents(BlobEvents events, int[]? hostPath = null) =>
        new() { Kind = OpKind.SetEvents, Events = events, HostPath = hostPath ?? Array.Empty<int>() };

    internal static Op SetDraw(DrawCallback? drawCallback, int[]? hostPath = null) =>
        new() {
            Kind = OpKind.SetDraw,
            DrawCallback = drawCallback,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    internal static Op SetEffect(ShaderEffect? effect, int[]? hostPath = null) =>
        new() {
            Kind = OpKind.SetEffect,
            Effect = effect,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    internal static Op SetLayoutTransition(LayoutTransition? transition, int[]? hostPath = null) =>
        new() {
            Kind = OpKind.SetLayoutTransition,
            LayoutTransition = transition,
            HostPath = hostPath ?? Array.Empty<int>(),
        };

    // Record-default equality: value-equal for primitives/strings, reference-equal for arrays/managed refs. HostPath defaults to the empty-array singleton so no-path factory calls compare equal.
    internal static Op CreateEmbed(int index, EmbedConfig? config, int[]? hostPath = null) =>
        new() { Kind = OpKind.CreateEmbed, Index = index, Embed = config, HostPath = hostPath ?? Array.Empty<int>() };

    internal static Op UpdateEmbed(int index, EmbedConfig? config, int[]? hostPath = null) =>
        new() { Kind = OpKind.UpdateEmbed, Index = index, Embed = config, HostPath = hostPath ?? Array.Empty<int>() };

    internal static Op CreateVirtual(int index, VirtualConfig? config, int[]? hostPath = null) =>
        new() { Kind = OpKind.CreateVirtual, Index = index, Virtual = config, HostPath = hostPath ?? Array.Empty<int>() };

    internal static Op UpdateVirtual(int index, VirtualConfig? config, int[]? hostPath = null) =>
        new() { Kind = OpKind.UpdateVirtual, Index = index, Virtual = config, HostPath = hostPath ?? Array.Empty<int>() };

    public bool Equals(Op other)
    {
        if (Kind != other.Kind) return false;
        if (!ReferenceEquals(HostPath, other.HostPath)) return false;
        if (Index != other.Index) return false;
        if (ToIndex != other.ToIndex) return false;
        if (!string.Equals(StringPayload, other.StringPayload)) return false;
        if (IsRich != other.IsRich) return false;
        if (!ReferenceEquals(Texture, other.Texture)) return false;
        if (!ReferenceEquals(Scene, other.Scene)) return false;
        if (RenderOnce != other.RenderOnce) return false;
        if (Paused != other.Paused) return false;
        if (!string.Equals(Color, other.Color)) return false;
        if (Shape != other.Shape) return false;
        if (!ReferenceEquals(Points, other.Points)) return false;
        if (!ReferenceEquals(Embed, other.Embed)) return false;
        if (!ReferenceEquals(Virtual, other.Virtual)) return false;
        if (!ReferenceEquals(Style, other.Style)) return false;
        if (!BlobEvents.ContentsEqual(in Events, in other.Events)) return false;
        if (!ReferenceEquals(DrawCallback, other.DrawCallback)) return false;
        if (!Equals(Effect, other.Effect)) return false;
        if (!Nullable.Equals(LayoutTransition, other.LayoutTransition)) return false;
        if (!string.Equals(Placeholder, other.Placeholder)) return false;
        if (MaxLength != other.MaxLength) return false;
        if (Disabled != other.Disabled) return false;
        if (Numeric != other.Numeric) return false;
        if (MinValue != other.MinValue) return false;
        if (MaxValue != other.MaxValue) return false;
        if (!string.Equals(NumberFormat, other.NumberFormat)) return false;
        if (Multiline != other.Multiline) return false;
        if (MinLength != other.MinLength) return false;
        if (!string.Equals(CharacterRegex, other.CharacterRegex)) return false;
        if (!string.Equals(StringRegex, other.StringRegex)) return false;
        if (!ReferenceEquals(CanEnterChar, other.CanEnterChar)) return false;
        if (!ReferenceEquals(Validate, other.Validate)) return false;
        if (!ReferenceEquals(OnValidationChanged, other.OnValidationChanged)) return false;
        if (!ReferenceEquals(OnChange, other.OnChange)) return false;
        if (!ReferenceEquals(OnSubmit, other.OnSubmit)) return false;
        if (!ReferenceEquals(OnFocus, other.OnFocus)) return false;
        if (!ReferenceEquals(OnBlur, other.OnBlur)) return false;
        if (!ReferenceEquals(OnCancel, other.OnCancel)) return false;
        if (IsControlled != other.IsControlled) return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is Op o && Equals(o);
    public override int GetHashCode() => HashCode.Combine(HashCode.Combine((byte)Kind, Index, ToIndex, StringPayload, Style, Scene, RenderOnce, Color), HashCode.Combine(Shape, Points, IsRich));
    public static bool operator ==(Op a, Op b) => a.Equals(b);
    public static bool operator !=(Op a, Op b) => !a.Equals(b);
}

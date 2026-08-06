using System;
using Sandbox;
using Sandbox.Rendering;
using Sandbox.UI;

namespace Goo;

internal enum StyleField : ushort
{
    None = 0,
    FlexDirection, JustifyContent, AlignItems, Display,
    Width, Height,
    Padding, PaddingLeft, PaddingTop, PaddingRight, PaddingBottom,
    Margin, MarginLeft, MarginTop, MarginRight, MarginBottom,
    Gap, RowGap, ColumnGap,
    BackgroundColor,
    BorderRadius, BorderTopLeftRadius, BorderTopRightRadius,
    BorderBottomRightRadius, BorderBottomLeftRadius,
    // Extended to cover remaining settable PanelStyle properties.
    AlignContent, AlignSelf,
    AspectRatio,
    BackdropFilterBlur, BackdropFilterBrightness, BackdropFilterContrast,
    BackdropFilterHueRotate, BackdropFilterInvert, BackdropFilterSaturate, BackdropFilterSepia,
    BackgroundAngle, BackgroundBlendMode, BackgroundImage, BackgroundPlaybackPaused,
    BackgroundPositionX, BackgroundPositionY, BackgroundRepeat,
    BackgroundSizeX, BackgroundSizeY, BackgroundTint,
    BorderBottomColor, BorderBottomWidth,
    BorderColor,
    BorderImageFill, BorderImageRepeat, BorderImageSource, BorderImageTint,
    BorderImageWidthBottom, BorderImageWidthLeft, BorderImageWidthRight, BorderImageWidthTop,
    BorderLeftColor, BorderLeftWidth,
    BorderRightColor, BorderRightWidth,
    BorderTopColor, BorderTopWidth,
    BorderWidth,
    Bottom,
    BoxShadow,
    CaretColor,
    Cursor,
    FilterBlur, FilterBorderColor, FilterBorderWidth,
    FilterBrightness, FilterContrast, FilterHueRotate, FilterInvert, FilterSaturate,
    FilterSepia, FilterTint,
    FlexBasis, FlexGrow, FlexShrink, FlexWrap,
    FontColor, FontFamily, FontSize,
    FontSmooth, FontStyle, FontVariantNumeric, FontWeight,
    ImageRendering,
    Left, LetterSpacing, LineHeight,
    MaskAngle, MaskImage, MaskMode, MaskPositionX, MaskPositionY,
    MaskRepeat, MaskScope, MaskSizeX, MaskSizeY,
    MaxHeight, MaxWidth, MinHeight, MinWidth,
    MixBlendMode,
    ObjectFit,
    Opacity,
    Order,
    OutlineColor, OutlineOffset, OutlineWidth,
    Overflow, OverflowX, OverflowY,
    PerspectiveOriginX, PerspectiveOriginY,
    PointerEvents,
    Position,
    Right,
    SoundIn, SoundOut,
    TextAlign,
    TextBackgroundAngle,
    TextDecorationColor, TextDecorationLine, TextDecorationSkipInk,
    TextDecorationStyle, TextDecorationThickness,
    TextFilter,
    TextLineThroughOffset,
    TextOverflow, TextOverlineOffset,
    TextShadow,
    TextStrokeColor, TextStrokeWidth,
    TextTransform,
    TextUnderlineOffset,
    Top,
    Transform,
    TransformOriginX, TransformOriginY,
    WhiteSpace,
    WordBreak, WordSpacing,
    ZIndex,
    HoverBackgroundColor, ActiveBackgroundColor, FocusBackgroundColor,
    HoverFontColor,       ActiveFontColor,       FocusFontColor,
    TransitionMs,
    Disabled,
}

internal enum StyleValueKind : byte
{
    None = 0,
    Length,
    Color,
    FlexDirection,
    Justify,
    Align,
    DisplayMode,
    String,
    Single,
    Boolean,
    Texture,
    Int32,
    Wrap,
    BackgroundRepeat,
    BorderImageFill,
    BorderImageRepeat,
    FontSmooth,
    FontStyle,
    FontVariantNumeric,
    ImageRendering,
    MaskMode,
    MaskScope,
    ObjectFit,
    OverflowMode,
    PointerEvents,
    PositionMode,
    TextAlign,
    TextDecoration,
    TextDecorationStyle,
    TextSkipInk,
    FilterMode,
    TextOverflow,
    TextTransform,
    WhiteSpace,
    WordBreak,
    Shadows,
    PanelTransform,
}

internal struct StyleValue : IEquatable<StyleValue>
{
    public StyleValueKind Kind;
    public Length LengthVal;     // valid when Kind == Length
    public Color ColorVal;       // valid when Kind == Color
    public int EnumVal;          // packed enum value for any enum-typed kind
    public int RefSlot;          // packed value for Single (float bits), Boolean (0/1), Int32 (raw)
    public object? RefVal;       // String or Texture reference

    public static StyleValue FromLength(Length v) => new() { Kind = StyleValueKind.Length, LengthVal = v };
    public static StyleValue FromColor(Color v)   => new() { Kind = StyleValueKind.Color, ColorVal = v };
    public static StyleValue FromFlexDirection(FlexDirection v) => new() { Kind = StyleValueKind.FlexDirection, EnumVal = (int)v };
    public static StyleValue FromJustify(Justify v) => new() { Kind = StyleValueKind.Justify, EnumVal = (int)v };
    public static StyleValue FromAlign(Align v)     => new() { Kind = StyleValueKind.Align, EnumVal = (int)v };
    public static StyleValue FromDisplay(DisplayMode v) => new() { Kind = StyleValueKind.DisplayMode, EnumVal = (int)v };

    public static StyleValue FromString(string v) => new() { Kind = StyleValueKind.String, RefVal = v };
    public static StyleValue FromSingle(float v)  => new() { Kind = StyleValueKind.Single, RefSlot = BitConverter.SingleToInt32Bits(v) };
    public static StyleValue FromBoolean(bool v)  => new() { Kind = StyleValueKind.Boolean, RefSlot = v ? 1 : 0 };
    public static StyleValue FromInt32(int v)     => new() { Kind = StyleValueKind.Int32, RefSlot = v };
    public static StyleValue FromTexture(Texture v) => new() { Kind = StyleValueKind.Texture, RefVal = v };
    public static StyleValue FromWrap(Wrap v) => new() { Kind = StyleValueKind.Wrap, EnumVal = (int)v };
    public static StyleValue FromBackgroundRepeat(BackgroundRepeat v) => new() { Kind = StyleValueKind.BackgroundRepeat, EnumVal = (int)v };
    public static StyleValue FromBorderImageFill(BorderImageFill v) => new() { Kind = StyleValueKind.BorderImageFill, EnumVal = (int)v };
    public static StyleValue FromBorderImageRepeat(BorderImageRepeat v) => new() { Kind = StyleValueKind.BorderImageRepeat, EnumVal = (int)v };
    public static StyleValue FromFontSmooth(FontSmooth v) => new() { Kind = StyleValueKind.FontSmooth, EnumVal = (int)v };
    public static StyleValue FromFontStyle(FontStyle v) => new() { Kind = StyleValueKind.FontStyle, EnumVal = (int)v };
    public static StyleValue FromFontVariantNumeric(FontVariantNumeric v) => new() { Kind = StyleValueKind.FontVariantNumeric, EnumVal = (int)v };
    public static StyleValue FromImageRendering(ImageRendering v) => new() { Kind = StyleValueKind.ImageRendering, EnumVal = (int)v };
    public static StyleValue FromMaskMode(MaskMode v) => new() { Kind = StyleValueKind.MaskMode, EnumVal = (int)v };
    public static StyleValue FromMaskScope(MaskScope v) => new() { Kind = StyleValueKind.MaskScope, EnumVal = (int)v };
    public static StyleValue FromObjectFit(ObjectFit v) => new() { Kind = StyleValueKind.ObjectFit, EnumVal = (int)v };
    public static StyleValue FromOverflowMode(OverflowMode v) => new() { Kind = StyleValueKind.OverflowMode, EnumVal = (int)v };
    public static StyleValue FromPointerEvents(PointerEvents v) => new() { Kind = StyleValueKind.PointerEvents, EnumVal = (int)v };
    public static StyleValue FromPositionMode(PositionMode v) => new() { Kind = StyleValueKind.PositionMode, EnumVal = (int)v };
    public static StyleValue FromTextAlign(TextAlign v) => new() { Kind = StyleValueKind.TextAlign, EnumVal = (int)v };
    public static StyleValue FromTextDecoration(TextDecoration v) => new() { Kind = StyleValueKind.TextDecoration, EnumVal = (int)v };
    public static StyleValue FromTextDecorationStyle(TextDecorationStyle v) => new() { Kind = StyleValueKind.TextDecorationStyle, EnumVal = (int)v };
    public static StyleValue FromTextSkipInk(TextSkipInk v) => new() { Kind = StyleValueKind.TextSkipInk, EnumVal = (int)v };
    public static StyleValue FromFilterMode(FilterMode v) => new() { Kind = StyleValueKind.FilterMode, EnumVal = (int)v };
    public static StyleValue FromTextOverflow(TextOverflow v) => new() { Kind = StyleValueKind.TextOverflow, EnumVal = (int)v };
    public static StyleValue FromTextTransform(TextTransform v) => new() { Kind = StyleValueKind.TextTransform, EnumVal = (int)v };
    public static StyleValue FromWhiteSpace(WhiteSpace v) => new() { Kind = StyleValueKind.WhiteSpace, EnumVal = (int)v };
    public static StyleValue FromWordBreak(WordBreak v) => new() { Kind = StyleValueKind.WordBreak, EnumVal = (int)v };

    public static StyleValue FromShadows(Shadows v)
        => new() { Kind = StyleValueKind.Shadows, RefVal = v };

    public static StyleValue FromPanelTransform(Goo.PanelTransform v)
        => new() { Kind = StyleValueKind.PanelTransform, RefVal = v._entries };

    public bool Equals(StyleValue other)
    {
        if (Kind != other.Kind) return false;
        return Kind switch
        {
            StyleValueKind.Length         => LengthVal.Equals(other.LengthVal),
            StyleValueKind.Color          => ColorVal.Equals(other.ColorVal),
            StyleValueKind.String         => ReferenceEquals(RefVal, other.RefVal),
            StyleValueKind.Texture        => ReferenceEquals(RefVal, other.RefVal),
            StyleValueKind.Single         => RefSlot == other.RefSlot,
            StyleValueKind.Boolean        => RefSlot == other.RefSlot,
            StyleValueKind.Int32          => RefSlot == other.RefSlot,
            // Shadows boxes a struct in RefVal; compare structurally, not by box identity.
            StyleValueKind.Shadows        => ((Shadows)RefVal!).Equals((Shadows)other.RefVal!),
            StyleValueKind.PanelTransform => Goo.PanelTransform.EntriesEqual(
                RefVal as System.Collections.Immutable.ImmutableList<Sandbox.UI.PanelTransform.Entry>,
                other.RefVal as System.Collections.Immutable.ImmutableList<Sandbox.UI.PanelTransform.Entry>),
            _                             => EnumVal == other.EnumVal,
        };
    }

    public override bool Equals(object? obj) => obj is StyleValue v && Equals(v);
    public override int GetHashCode() => Kind switch
    {
        // PanelTransform stores an ImmutableList in RefVal; Equals is structural via
        // EntriesEqual, so the hash must be structural too, hashing RefVal as object
        // would give reference identity and break the Equals/GetHashCode contract.
        StyleValueKind.PanelTransform => HashCode.Combine(
            (byte)Kind,
            Goo.PanelTransform.ComputeEntriesHash(
                RefVal as System.Collections.Immutable.ImmutableList<Sandbox.UI.PanelTransform.Entry>)),
        StyleValueKind.Shadows => HashCode.Combine((byte)Kind, (Shadows)RefVal!),
        _ => HashCode.Combine((byte)Kind, LengthVal.GetHashCode(), ColorVal.GetHashCode(), EnumVal, RefSlot, RefVal),
    };
}

internal struct StyleEntry
{
    public StyleField Field;
    public StyleValue Value;
}

internal sealed class StyleList
{
    internal static readonly StyleList Empty = new(readOnly: true);

    StyleEntry[] _items;
    int _count;
    readonly bool _readonly;

    public StyleList() : this(readOnly: false) { }
    StyleList(bool readOnly) { _items = new StyleEntry[4]; _count = 0; _readonly = readOnly; }

    public int Count => _count;
    public ref StyleEntry this[int i] => ref _items[i];

    public void Add(StyleField field, StyleValue value)
    {
        if (_readonly) throw new InvalidOperationException("Cannot mutate StyleList.Empty sentinel.");
        if (_count == _items.Length) Array.Resize(ref _items, _items.Length * 2);
        _items[_count++] = new StyleEntry { Field = field, Value = value };
    }

    public bool TryGet(StyleField field, out StyleValue value)
    {
        // Last-declared wins: scan from the end so a later declaration (or a `with` override) takes precedence.
        for (int i = _count - 1; i >= 0; i--)
            if (_items[i].Field == field) { value = _items[i].Value; return true; }
        value = default;
        return false;
    }

    internal void Reset()
    {
        if (_readonly) return;
        Array.Clear(_items, 0, _count);
        _count = 0;
    }

    internal void CopyFrom(StyleList src)
    {
        if (_readonly) throw new InvalidOperationException("Cannot mutate StyleList.Empty sentinel.");
        if (_items.Length < src._count) _items = new StyleEntry[src._items.Length];
        Array.Copy(src._items, _items, src._count);
        _count = src._count;
    }

    public static bool ContentsEqual(StyleList a, StyleList b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a._count != b._count) return false;
        for (int i = 0; i < a._count; i++)
            if (a._items[i].Field != b._items[i].Field || !a._items[i].Value.Equals(b._items[i].Value))
                return false;
        return true;
    }
}

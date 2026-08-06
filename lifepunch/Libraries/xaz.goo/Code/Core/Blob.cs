using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Sandbox;
using Sandbox.UI;

namespace Goo;

/// <summary>Renders plain text, Markdown, or engine-supported inline HTML in one label. Style it with the text and common style properties.</summary>
public readonly partial record struct Text : IBlob
{
    public static BlobKind Kind => BlobKind.Text;
    /// <summary>The inline text to display. Leave empty when using Path.</summary>
    public string Content { get; init; }
    /// <summary>A mounted text-file path used instead of Content. The file extension has no special meaning.</summary>
    public string? Path { get; init; }
    /// <summary>Construction-time mode that parses Markdown and preserves inline HTML before rendering through the engine's rich-text label. Keep stable for this blob identity.</summary>
    public bool IsRich { get; init; }
    /// <summary>Stable identity across rebuilds. Set it when this blob can change position among its siblings, so the reconciler matches it to the same panel.</summary>
    public string? Key { get; init; }
    internal readonly StyleList _style;

    internal readonly Action<MousePanelEvent>? _onClick;
    internal readonly Action<MousePanelEvent>? _onRightClick;
    internal readonly Action<MousePanelEvent>? _onMiddleClick;
    internal readonly Action<MousePanelEvent>? _onMouseEnter;
    internal readonly Action<MousePanelEvent>? _onMouseLeave;
    internal readonly Action<MousePanelEvent>? _onMouseDown;
    internal readonly Action<MousePanelEvent>? _onMouseUp;
    internal readonly Action<MousePanelEvent>? _onMouseMove;

    /// <summary>Called when clicked with the left mouse button.</summary>
    public Action<MousePanelEvent>? OnClick      { init => _onClick      = value; }
    /// <summary>Called when clicked with the right mouse button.</summary>
    public Action<MousePanelEvent>? OnRightClick { init => _onRightClick = value; }
    /// <summary>Called when clicked with the middle mouse button.</summary>
    public Action<MousePanelEvent>? OnMiddleClick { init => _onMiddleClick = value; }
    /// <summary>Called when the pointer enters.</summary>
    public Action<MousePanelEvent>? OnMouseEnter { init => _onMouseEnter = value; }
    /// <summary>Called when the pointer leaves.</summary>
    public Action<MousePanelEvent>? OnMouseLeave { init => _onMouseLeave = value; }
    /// <summary>Called when a mouse button is pressed.</summary>
    public Action<MousePanelEvent>? OnMouseDown  { init => _onMouseDown  = value; }
    /// <summary>Called when a mouse button is released.</summary>
    public Action<MousePanelEvent>? OnMouseUp    { init => _onMouseUp    = value; }
    /// <summary>Called when the pointer moves over it.</summary>
    public Action<MousePanelEvent>? OnMouseMove  { init => _onMouseMove  = value; }

    public Text()
    {
        Content = string.Empty;
        Path = null;
        Key = null;
        _style = StyleList.Empty;
    }

    public Text(string content) : this(content, false)
    {
    }

    public Text(string content, bool isRich)
    {
        Content = content ?? string.Empty;
        Path = null;
        IsRich = isRich;
        Key = null;
        _style = StyleList.Empty;
    }

    public bool Equals(Text other)
        => Content == other.Content && Path == other.Path && IsRich == other.IsRich && Key == other.Key && StyleList.ContentsEqual(_style, other._style);

    public override int GetHashCode()
        => HashCode.Combine(Content, Path, IsRich, Key);
    // _style and event handlers intentionally excluded; mirrors Container per StructIntentEqualityTests.

    void IBlob.WriteTo(ref Frame frame)
    {
        var source = string.IsNullOrWhiteSpace(Path)
            ? Content ?? string.Empty
            : Internal.TextSourceResolver.Read(Content, Path, FileSystem.Mounted);

        frame.Kind = BlobKind.Text;
        frame.Key = Key;
        // Keep the source unparsed through reconciliation so an unchanged rich Text
        // does not pay the Markdown conversion cost on every rebuild.
        frame.Content = source;
        frame.IsRich = IsRich;
        frame.Style = _style;
        frame.Children = null;
        frame.Texture = null;
        frame.Path = null;
        frame.Scene = null;
        frame.RenderOnce = false;
        frame.Paused = false;
        frame.Color = null;
        frame.Shape = default;
        frame.Points = null;
        frame.Effect = null;
        frame.Tag = null;
        frame.Draw = null;
        frame.LayoutTransition = null;
        frame.Placeholder   = null;
        frame.MaxLength     = null;
        frame.Disabled      = false;
        frame.Numeric       = false;
        frame.MinValue      = null;
        frame.MaxValue      = null;
        frame.NumberFormat  = null;
        frame.Multiline     = false;
        frame.OnChange      = null;
        frame.OnSubmit      = null;
        frame.OnFocus       = null;
        frame.OnBlur        = null;
        frame.OnCancel      = null;
        frame.MinLength     = null;
        frame.CharacterRegex = null;
        frame.StringRegex   = null;
        frame.CanEnterChar  = null;
        frame.Validate      = null;
        frame.OnValidationChanged = null;
        frame.IsControlled  = false;
        frame.ValueAndDefaultTextBothSet = false;
        frame.Events.OnClick      = _onClick;
        frame.Events.OnRightClick = _onRightClick;
        frame.Events.OnMiddleClick = _onMiddleClick;
        frame.Events.OnMouseEnter = _onMouseEnter;
        frame.Events.OnMouseLeave = _onMouseLeave;
        frame.Events.OnMouseDown  = _onMouseDown;
        frame.Events.OnMouseUp    = _onMouseUp;
        frame.Events.OnMouseMove  = _onMouseMove;
    }
}

/// <summary>
/// The general-purpose layout blob. Holds children and exposes the full style surface:
/// layout, flex, spacing, border, background, filters, transforms, and state variants.
/// </summary>
public readonly partial record struct Container : IBlob, IEnumerable
{
    public static BlobKind Kind => BlobKind.Container;
    /// <summary>Stable identity across rebuilds. Set it when this blob can change position among its siblings, so the reconciler matches it to the same panel.</summary>
    public string? Key { get; init; }
    internal readonly StyleList _style;
    internal readonly Children _children;

    internal readonly Action<MousePanelEvent>? _onClick;
    internal readonly Action<MousePanelEvent>? _onRightClick;
    internal readonly Action<MousePanelEvent>? _onMiddleClick;
    internal readonly Action<MousePanelEvent>? _onMouseEnter;
    internal readonly Action<MousePanelEvent>? _onMouseLeave;
    internal readonly Action<MousePanelEvent>? _onMouseDown;
    internal readonly Action<MousePanelEvent>? _onMouseUp;
    internal readonly Action<MousePanelEvent>? _onMouseMove;
    internal readonly Action<Vector2>?         _onMouseWheel;

    /// <summary>Called when clicked with the left mouse button.</summary>
    public Action<MousePanelEvent>? OnClick      { init => _onClick      = value; }
    /// <summary>Called when clicked with the right mouse button.</summary>
    public Action<MousePanelEvent>? OnRightClick { init => _onRightClick = value; }
    /// <summary>Called when clicked with the middle mouse button.</summary>
    public Action<MousePanelEvent>? OnMiddleClick { init => _onMiddleClick = value; }
    /// <summary>Called when the pointer enters.</summary>
    public Action<MousePanelEvent>? OnMouseEnter { init => _onMouseEnter = value; }
    /// <summary>Called when the pointer leaves.</summary>
    public Action<MousePanelEvent>? OnMouseLeave { init => _onMouseLeave = value; }
    /// <summary>Called when a mouse button is pressed.</summary>
    public Action<MousePanelEvent>? OnMouseDown  { init => _onMouseDown  = value; }
    /// <summary>Called when a mouse button is released.</summary>
    public Action<MousePanelEvent>? OnMouseUp    { init => _onMouseUp    = value; }
    /// <summary>Called when the pointer moves over it.</summary>
    public Action<MousePanelEvent>? OnMouseMove  { init => _onMouseMove  = value; }
    /// <summary>Called when the mouse wheel scrolls over it. The argument is the wheel delta (y positive = scroll down). Setting this consumes the wheel so it does not bubble to an ancestor scroll container.</summary>
    public Action<Vector2>? OnMouseWheel { init => _onMouseWheel = value; }

    internal readonly Action<DragEvent>?  _onDragStart;
    internal readonly Action<DragEvent>?  _onDrag;
    internal readonly Action<DragEvent>?  _onDragEnd;
    internal readonly Action<PanelEvent>? _onDragEnter;
    internal readonly Action<PanelEvent>? _onDragLeave;
    internal readonly Action<PanelEvent>? _onDrop;

    /// <summary>Called when a drag begins on this blob.</summary>
    public Action<DragEvent>?  OnDragStart { init => _onDragStart = value; }
    /// <summary>Called on each frame of an in-flight drag.</summary>
    public Action<DragEvent>?  OnDrag      { init => _onDrag      = value; }
    /// <summary>Called when a drag started on this blob ends.</summary>
    public Action<DragEvent>?  OnDragEnd   { init => _onDragEnd   = value; }
    /// <summary>Called when a dragged item enters this blob as a drop target.</summary>
    public Action<PanelEvent>? OnDragEnter { init => _onDragEnter = value; }
    /// <summary>Called when a dragged item leaves this blob as a drop target.</summary>
    public Action<PanelEvent>? OnDragLeave { init => _onDragLeave = value; }
    /// <summary>Called when a dragged item is dropped on this blob.</summary>
    public Action<PanelEvent>? OnDrop      { init => _onDrop      = value; }

    internal readonly ShaderEffect? _effect;
    internal readonly DrawCallback? _draw;
    internal readonly LayoutTransition? _layoutTransition;

    /// <summary>A custom shader effect to render this blob with (FrostedGlass, Spotlight, Particles, or a custom ShaderEffect). Drawn via IPanelDraw; coexists with Draw.</summary>
    public ShaderEffect? Effect { get => _effect; init => _effect = value; }
    /// <summary>A custom immediate-mode draw callback. Receives a <see cref="Canvas"/> for issuing batched primitive draws (rects, text, shadows, ...).</summary>
    public DrawCallback? Draw { get => _draw; init => _draw = value; }
    /// <summary>Declared transition for layout position moves. When this blob's flex slot changes (pop, insert, reorder, reflow), it glides to the new position instead of snapping. Declare per child; survivors must be keyed.</summary>
    public LayoutTransition? LayoutTransition { get => _layoutTransition; init => _layoutTransition = value; }
    /// <summary>Global effect-addressing tag. When set and no inline <see cref="Effect"/> is given, the effect resolves from the host's effect manifest. May repeat across the tree; distinct from <see cref="Key"/> (sibling-local reconciliation identity).</summary>
    public string? Tag { get; init; }

    /// <summary>When true, a left click stops propagating before reaching a parent handler. Use on the content panel inside a scrim or popover so clicks inside do not dismiss the overlay.</summary>
    public bool SwallowClick { get; init; }

    static readonly Action<MousePanelEvent> _swallowDelegate = e => e.StopPropagation();

    public Container()
    {
        if (BuildContext._current == null)
            throw new InvalidOperationException(
                "Container constructed outside Build(). Containers are short-lived: " +
                "build them inside your Build() method, or in a helper function called from Build(). " +
                "Static Container fields and instances held across rebuilds are not supported.");
        _children = BuildContext._current.RentChildren();
        _style = StyleList.Empty;
        Key = null;
        Tag = null;
        _effect = null;
        _draw = null;
        _layoutTransition = null;
    }

    /// <summary>The child blobs added to this container during Build().</summary>
    public Children Children => _children;

    /// <summary>Adds a child blob. Called by collection-initializer syntax inside Build().</summary>
    public void Add<T>(in T child) where T : struct, IBlob => _children.Add(in child);

    // IEnumerable is required by C# collection-initializer syntax; iteration is not a
    // use case for Container, so this returns an empty enumerator.
    IEnumerator IEnumerable.GetEnumerator() => Array.Empty<object>().GetEnumerator();


    public bool Equals(Container other)
        => Key == other.Key
        && Tag == other.Tag
        && StyleList.ContentsEqual(_style, other._style)
        && ReferenceEquals(_draw, other._draw)
        && Equals(_effect, other._effect)
        && Nullable.Equals(_layoutTransition, other._layoutTransition);

    public override int GetHashCode()
        => Key?.GetHashCode() ?? 0;
    // _style, _children, event handlers, _draw, _effect, and _layoutTransition intentionally excluded
    // from GetHashCode; Equals governs correctness -- see StructIntentEqualityTests.

    void IBlob.WriteTo(ref Frame frame)
    {
        frame.Kind = BlobKind.Container;
        frame.Key = Key;
        frame.Content = "";
        frame.Style = _style;
        frame.Children = _children;
        frame.Texture = null;
        frame.Path = null;
        frame.Scene = null;
        frame.RenderOnce = false;
        frame.Paused = false;
        frame.Color = null;
        frame.Shape = default;
        frame.Points = null;
        frame.Effect = _effect;
        frame.Tag = Tag;
        frame.Draw = _draw;
        frame.LayoutTransition = _layoutTransition;
        frame.Placeholder   = null;
        frame.MaxLength     = null;
        frame.Disabled      = false;
        frame.Numeric       = false;
        frame.MinValue      = null;
        frame.MaxValue      = null;
        frame.NumberFormat  = null;
        frame.Multiline     = false;
        frame.OnChange      = null;
        frame.OnSubmit      = null;
        frame.OnFocus       = null;
        frame.OnBlur        = null;
        frame.OnCancel      = null;
        frame.MinLength     = null;
        frame.CharacterRegex = null;
        frame.StringRegex   = null;
        frame.CanEnterChar  = null;
        frame.Validate      = null;
        frame.OnValidationChanged = null;
        frame.IsControlled  = false;
        frame.ValueAndDefaultTextBothSet = false;
        if (SwallowClick)
        {
            var onClick = _onClick;
            frame.Events.OnClick = onClick is null
                ? _swallowDelegate
                : e => { e.StopPropagation(); onClick(e); };
        }
        else
        {
            frame.Events.OnClick = _onClick;
        }
        frame.Events.OnRightClick = _onRightClick;
        frame.Events.OnMiddleClick = _onMiddleClick;
        frame.Events.OnMouseEnter = _onMouseEnter;
        frame.Events.OnMouseLeave = _onMouseLeave;
        frame.Events.OnMouseDown  = _onMouseDown;
        frame.Events.OnMouseUp    = _onMouseUp;
        frame.Events.OnMouseMove  = _onMouseMove;
        frame.Events.OnMouseWheel = _onMouseWheel;
        frame.Events.OnDragStart  = _onDragStart;
        frame.Events.OnDrag       = _onDrag;
        frame.Events.OnDragEnd    = _onDragEnd;
        frame.Events.OnDragEnter  = _onDragEnter;
        frame.Events.OnDragLeave  = _onDragLeave;
        frame.Events.OnDrop       = _onDrop;
    }
}

/// <summary>
/// Displays an image from a live <see cref="T:Sandbox.Texture"/> or an asset path. Set one source, never both.
/// Wrap in a <see cref="T:Goo.Container"/> for layout, background, border, or other styles it does not expose.
/// </summary>
public readonly partial record struct Image : IBlob
{
    public static BlobKind Kind => BlobKind.Image;
    /// <summary>A live texture to display. Set this or <see cref="Path"/>, never both. Setting both renders nothing and warns.</summary>
    public Texture? Texture { get; init; }
    /// <summary>Path to an image asset, for example "ui/logo.png". Set this or <see cref="Texture"/>, never both. Setting both renders nothing and warns.</summary>
    public string?  Path    { get; init; }
    /// <summary>Stable identity across rebuilds. Set it when this blob can change position among its siblings, so the reconciler matches it to the same panel.</summary>
    public string?  Key     { get; init; }
    internal readonly StyleList _style;

    internal readonly Action<MousePanelEvent>? _onClick;
    internal readonly Action<MousePanelEvent>? _onRightClick;
    internal readonly Action<MousePanelEvent>? _onMiddleClick;
    internal readonly Action<MousePanelEvent>? _onMouseEnter;
    internal readonly Action<MousePanelEvent>? _onMouseLeave;
    internal readonly Action<MousePanelEvent>? _onMouseDown;
    internal readonly Action<MousePanelEvent>? _onMouseUp;
    internal readonly Action<MousePanelEvent>? _onMouseMove;

    /// <summary>Called when clicked with the left mouse button.</summary>
    public Action<MousePanelEvent>? OnClick      { init => _onClick      = value; }
    /// <summary>Called when clicked with the right mouse button.</summary>
    public Action<MousePanelEvent>? OnRightClick { init => _onRightClick = value; }
    /// <summary>Called when clicked with the middle mouse button.</summary>
    public Action<MousePanelEvent>? OnMiddleClick { init => _onMiddleClick = value; }
    /// <summary>Called when the pointer enters.</summary>
    public Action<MousePanelEvent>? OnMouseEnter { init => _onMouseEnter = value; }
    /// <summary>Called when the pointer leaves.</summary>
    public Action<MousePanelEvent>? OnMouseLeave { init => _onMouseLeave = value; }
    /// <summary>Called when a mouse button is pressed.</summary>
    public Action<MousePanelEvent>? OnMouseDown  { init => _onMouseDown  = value; }
    /// <summary>Called when a mouse button is released.</summary>
    public Action<MousePanelEvent>? OnMouseUp    { init => _onMouseUp    = value; }
    /// <summary>Called when the pointer moves over it.</summary>
    public Action<MousePanelEvent>? OnMouseMove  { init => _onMouseMove  = value; }

    public Image()
    {
        Texture = null;
        Path = null;
        Key = null;
        _style = StyleList.Empty;
    }

    public bool Equals(Image other)
        => ReferenceEquals(Texture, other.Texture)
        && Path == other.Path
        && Key == other.Key
        && StyleList.ContentsEqual(_style, other._style);

    public override int GetHashCode()
        => HashCode.Combine(Texture, Path, Key);
    // _style and event handlers intentionally excluded; mirrors Text/Container per StructIntentEqualityTests.

    void IBlob.WriteTo(ref Frame frame)
    {
        frame.Kind = BlobKind.Image;
        frame.Key = Key;
        frame.Content = "";
        frame.Style = _style;
        frame.Children = null;
        frame.Texture = Texture;
        frame.Path = Path;
        frame.Scene = null;
        frame.RenderOnce = false;
        frame.Paused = false;
        frame.Color = null;
        frame.Shape = default;
        frame.Points = null;
        frame.Effect = null;
        frame.Tag = null;
        frame.Draw = null;
        frame.LayoutTransition = null;
        frame.Placeholder   = null;
        frame.MaxLength     = null;
        frame.Disabled      = false;
        frame.Numeric       = false;
        frame.MinValue      = null;
        frame.MaxValue      = null;
        frame.NumberFormat  = null;
        frame.Multiline     = false;
        frame.OnChange      = null;
        frame.OnSubmit      = null;
        frame.OnFocus       = null;
        frame.OnBlur        = null;
        frame.OnCancel      = null;
        frame.MinLength     = null;
        frame.CharacterRegex = null;
        frame.StringRegex   = null;
        frame.CanEnterChar  = null;
        frame.Validate      = null;
        frame.OnValidationChanged = null;
        frame.IsControlled  = false;
        frame.ValueAndDefaultTextBothSet = false;
        frame.Events.OnClick      = _onClick;
        frame.Events.OnRightClick = _onRightClick;
        frame.Events.OnMiddleClick = _onMiddleClick;
        frame.Events.OnMouseEnter = _onMouseEnter;
        frame.Events.OnMouseLeave = _onMouseLeave;
        frame.Events.OnMouseDown  = _onMouseDown;
        frame.Events.OnMouseUp    = _onMouseUp;
        frame.Events.OnMouseMove  = _onMouseMove;
    }
}

/// <summary>Renders a 3D <see cref="T:Sandbox.Scene"/> into the UI. Provide a live scene or a scene path.</summary>
public readonly partial record struct ScenePanel : IBlob
{
    public static BlobKind Kind => BlobKind.ScenePanel;
    /// <summary>A live scene to render. Set this or <see cref="ScenePath"/>.</summary>
    public Scene?  Scene      { get; init; }
    /// <summary>Path to a scene asset to render. Set this or <see cref="Scene"/>.</summary>
    public string? ScenePath  { get; init; }
    /// <summary>When true, renders the scene a single frame instead of every frame. Use for static previews.</summary>
    public bool    RenderOnce { get; init; }
    /// <summary>Stable identity across rebuilds. Set it when this blob can change position among its siblings, so the reconciler matches it to the same panel.</summary>
    public string? Key        { get; init; }
    internal readonly StyleList _style;

    internal readonly Action<MousePanelEvent>? _onClick;
    internal readonly Action<MousePanelEvent>? _onRightClick;
    internal readonly Action<MousePanelEvent>? _onMiddleClick;
    internal readonly Action<MousePanelEvent>? _onMouseEnter;
    internal readonly Action<MousePanelEvent>? _onMouseLeave;
    internal readonly Action<MousePanelEvent>? _onMouseDown;
    internal readonly Action<MousePanelEvent>? _onMouseUp;
    internal readonly Action<MousePanelEvent>? _onMouseMove;

    /// <summary>Called when clicked with the left mouse button.</summary>
    public Action<MousePanelEvent>? OnClick      { init => _onClick      = value; }
    /// <summary>Called when clicked with the right mouse button.</summary>
    public Action<MousePanelEvent>? OnRightClick { init => _onRightClick = value; }
    /// <summary>Called when clicked with the middle mouse button.</summary>
    public Action<MousePanelEvent>? OnMiddleClick { init => _onMiddleClick = value; }
    /// <summary>Called when the pointer enters.</summary>
    public Action<MousePanelEvent>? OnMouseEnter { init => _onMouseEnter = value; }
    /// <summary>Called when the pointer leaves.</summary>
    public Action<MousePanelEvent>? OnMouseLeave { init => _onMouseLeave = value; }
    /// <summary>Called when a mouse button is pressed.</summary>
    public Action<MousePanelEvent>? OnMouseDown  { init => _onMouseDown  = value; }
    /// <summary>Called when a mouse button is released.</summary>
    public Action<MousePanelEvent>? OnMouseUp    { init => _onMouseUp    = value; }
    /// <summary>Called when the pointer moves over it.</summary>
    public Action<MousePanelEvent>? OnMouseMove  { init => _onMouseMove  = value; }

    public ScenePanel()
    {
        Scene = null;
        ScenePath = null;
        RenderOnce = false;
        Key = null;
        _style = StyleList.Empty;
    }

    public bool Equals(ScenePanel other)
        => ReferenceEquals(Scene, other.Scene)
        && ScenePath == other.ScenePath
        && RenderOnce == other.RenderOnce
        && Key == other.Key
        && StyleList.ContentsEqual(_style, other._style);

    public override int GetHashCode()
        => HashCode.Combine(Scene, ScenePath, RenderOnce, Key);
    // _style and event handlers intentionally excluded; mirrors Image per StructIntentEqualityTests.

    void IBlob.WriteTo(ref Frame frame)
    {
        frame.Kind = BlobKind.ScenePanel;
        frame.Key = Key;
        frame.Content = "";
        frame.Style = _style;
        frame.Children = null;
        frame.Texture = null;
        frame.Scene = Scene;
        frame.Path = ScenePath;
        frame.RenderOnce = RenderOnce;
        frame.Paused = false;
        frame.Color = null;
        frame.Shape = default;
        frame.Points = null;
        frame.Effect = null;
        frame.Tag = null;
        frame.Draw = null;
        frame.LayoutTransition = null;
        frame.Placeholder   = null;
        frame.MaxLength     = null;
        frame.Disabled      = false;
        frame.Numeric       = false;
        frame.MinValue      = null;
        frame.MaxValue      = null;
        frame.NumberFormat  = null;
        frame.Multiline     = false;
        frame.OnChange      = null;
        frame.OnSubmit      = null;
        frame.OnFocus       = null;
        frame.OnBlur        = null;
        frame.OnCancel      = null;
        frame.MinLength     = null;
        frame.CharacterRegex = null;
        frame.StringRegex   = null;
        frame.CanEnterChar  = null;
        frame.Validate      = null;
        frame.OnValidationChanged = null;
        frame.IsControlled  = false;
        frame.ValueAndDefaultTextBothSet = false;
        frame.Events.OnClick      = _onClick;
        frame.Events.OnRightClick = _onRightClick;
        frame.Events.OnMiddleClick = _onMiddleClick;
        frame.Events.OnMouseEnter = _onMouseEnter;
        frame.Events.OnMouseLeave = _onMouseLeave;
        frame.Events.OnMouseDown  = _onMouseDown;
        frame.Events.OnMouseUp    = _onMouseUp;
        frame.Events.OnMouseMove  = _onMouseMove;
    }
}

/// <summary>Renders an SVG asset, with an optional color override.</summary>
public readonly partial record struct SvgPanel : IBlob
{
    public static BlobKind Kind => BlobKind.SvgPanel;
    /// <summary>Path to the SVG asset to render, for example "ui/icon.svg".</summary>
    public string? Path  { get; init; }
    /// <summary>Optional color to tint the SVG, as a CSS color string. Leave null to use the SVG's own colors.</summary>
    public string? Color { get; init; }
    /// <summary>Stable identity across rebuilds. Set it when this blob can change position among its siblings, so the reconciler matches it to the same panel.</summary>
    public string? Key   { get; init; }
    internal readonly StyleList _style;

    internal readonly Action<MousePanelEvent>? _onClick;
    internal readonly Action<MousePanelEvent>? _onRightClick;
    internal readonly Action<MousePanelEvent>? _onMiddleClick;
    internal readonly Action<MousePanelEvent>? _onMouseEnter;
    internal readonly Action<MousePanelEvent>? _onMouseLeave;
    internal readonly Action<MousePanelEvent>? _onMouseDown;
    internal readonly Action<MousePanelEvent>? _onMouseUp;
    internal readonly Action<MousePanelEvent>? _onMouseMove;

    /// <summary>Called when clicked with the left mouse button.</summary>
    public Action<MousePanelEvent>? OnClick      { init => _onClick      = value; }
    /// <summary>Called when clicked with the right mouse button.</summary>
    public Action<MousePanelEvent>? OnRightClick { init => _onRightClick = value; }
    /// <summary>Called when clicked with the middle mouse button.</summary>
    public Action<MousePanelEvent>? OnMiddleClick { init => _onMiddleClick = value; }
    /// <summary>Called when the pointer enters.</summary>
    public Action<MousePanelEvent>? OnMouseEnter { init => _onMouseEnter = value; }
    /// <summary>Called when the pointer leaves.</summary>
    public Action<MousePanelEvent>? OnMouseLeave { init => _onMouseLeave = value; }
    /// <summary>Called when a mouse button is pressed.</summary>
    public Action<MousePanelEvent>? OnMouseDown  { init => _onMouseDown  = value; }
    /// <summary>Called when a mouse button is released.</summary>
    public Action<MousePanelEvent>? OnMouseUp    { init => _onMouseUp    = value; }
    /// <summary>Called when the pointer moves over it.</summary>
    public Action<MousePanelEvent>? OnMouseMove  { init => _onMouseMove  = value; }

    public SvgPanel()
    {
        Path = null;
        Color = null;
        Key = null;
        _style = StyleList.Empty;
    }

    public bool Equals(SvgPanel other)
        => Path == other.Path
        && Color == other.Color
        && Key == other.Key
        && StyleList.ContentsEqual(_style, other._style);

    public override int GetHashCode()
        => HashCode.Combine(Path, Color, Key);

    void IBlob.WriteTo(ref Frame frame)
    {
        frame.Kind = BlobKind.SvgPanel;
        frame.Key = Key;
        frame.Content = "";
        frame.Style = _style;
        frame.Children = null;
        frame.Texture = null;
        frame.Path = Path;
        frame.Color = Color;
        frame.Scene = null;
        frame.RenderOnce = false;
        frame.Paused = false;
        frame.Shape = default;
        frame.Points = null;
        frame.Effect = null;
        frame.Tag = null;
        frame.Draw = null;
        frame.LayoutTransition = null;
        frame.Placeholder   = null;
        frame.MaxLength     = null;
        frame.Disabled      = false;
        frame.Numeric       = false;
        frame.MinValue      = null;
        frame.MaxValue      = null;
        frame.NumberFormat  = null;
        frame.Multiline     = false;
        frame.OnChange      = null;
        frame.OnSubmit      = null;
        frame.OnFocus       = null;
        frame.OnBlur        = null;
        frame.OnCancel      = null;
        frame.MinLength     = null;
        frame.CharacterRegex = null;
        frame.StringRegex   = null;
        frame.CanEnterChar  = null;
        frame.Validate      = null;
        frame.OnValidationChanged = null;
        frame.IsControlled  = false;
        frame.ValueAndDefaultTextBothSet = false;
        frame.Events.OnClick      = _onClick;
        frame.Events.OnRightClick = _onRightClick;
        frame.Events.OnMiddleClick = _onMiddleClick;
        frame.Events.OnMouseEnter = _onMouseEnter;
        frame.Events.OnMouseLeave = _onMouseLeave;
        frame.Events.OnMouseDown  = _onMouseDown;
        frame.Events.OnMouseUp    = _onMouseUp;
        frame.Events.OnMouseMove  = _onMouseMove;
    }
}

/// <summary>A pie wedge or ring segment. Spans from <see cref="StartAngle"/> to <see cref="EndAngle"/>, between <see cref="InnerRadius"/> and <see cref="OuterRadius"/>.</summary>
public readonly partial record struct Sector : IBlob
{
    public static BlobKind Kind => BlobKind.Sector;
    /// <summary>Angle where the wedge begins, in degrees.</summary>
    public float   StartAngle  { get; init; }
    /// <summary>Angle where the wedge ends, in degrees.</summary>
    public float   EndAngle    { get; init; }
    /// <summary>Inner edge of the ring, as a fraction (0 to 1) of the frame box. 0 gives a solid wedge.</summary>
    public float   InnerRadius { get; init; }
    /// <summary>Outer edge of the ring, as a fraction (0 to 1) of the frame box.</summary>
    public float   OuterRadius { get; init; }
    /// <summary>Corner rounding, as a fraction (0 to 1) of the outer radius. 0 gives sharp corners.</summary>
    public float   CornerRadius { get; init; }
    /// <summary>When true, mouse events hit the full bounding box instead of the drawn outline. For editor-style selection and dragging; visuals are unchanged.</summary>
    public bool    RectHitTest { get; init; }
    /// <summary>Stable identity across rebuilds. Set it when this blob can change position among its siblings, so the reconciler matches it to the same panel.</summary>
    public string? Key         { get; init; }
    internal readonly StyleList _style;

    internal readonly Action<MousePanelEvent>? _onClick;
    internal readonly Action<MousePanelEvent>? _onRightClick;
    internal readonly Action<MousePanelEvent>? _onMiddleClick;
    internal readonly Action<MousePanelEvent>? _onMouseEnter;
    internal readonly Action<MousePanelEvent>? _onMouseLeave;
    internal readonly Action<MousePanelEvent>? _onMouseDown;
    internal readonly Action<MousePanelEvent>? _onMouseUp;
    internal readonly Action<MousePanelEvent>? _onMouseMove;

    /// <summary>Called when clicked with the left mouse button.</summary>
    public Action<MousePanelEvent>? OnClick      { init => _onClick      = value; }
    /// <summary>Called when clicked with the right mouse button.</summary>
    public Action<MousePanelEvent>? OnRightClick { init => _onRightClick = value; }
    /// <summary>Called when clicked with the middle mouse button.</summary>
    public Action<MousePanelEvent>? OnMiddleClick { init => _onMiddleClick = value; }
    /// <summary>Called when the pointer enters.</summary>
    public Action<MousePanelEvent>? OnMouseEnter { init => _onMouseEnter = value; }
    /// <summary>Called when the pointer leaves.</summary>
    public Action<MousePanelEvent>? OnMouseLeave { init => _onMouseLeave = value; }
    /// <summary>Called when a mouse button is pressed.</summary>
    public Action<MousePanelEvent>? OnMouseDown  { init => _onMouseDown  = value; }
    /// <summary>Called when a mouse button is released.</summary>
    public Action<MousePanelEvent>? OnMouseUp    { init => _onMouseUp    = value; }
    /// <summary>Called when the pointer moves over it.</summary>
    public Action<MousePanelEvent>? OnMouseMove  { init => _onMouseMove  = value; }

    public Sector()
    {
        StartAngle = 0f;
        EndAngle = 0f;
        InnerRadius = 0f;
        OuterRadius = 0f;
        CornerRadius = 0f;
        RectHitTest = false;
        Key = null;
        _style = StyleList.Empty;
    }

    public bool Equals(Sector other)
        => StartAngle == other.StartAngle
        && EndAngle == other.EndAngle
        && InnerRadius == other.InnerRadius
        && OuterRadius == other.OuterRadius
        && CornerRadius == other.CornerRadius
        && RectHitTest == other.RectHitTest
        && Key == other.Key
        && StyleList.ContentsEqual(_style, other._style);

    public override int GetHashCode()
        => HashCode.Combine(StartAngle, EndAngle, InnerRadius, OuterRadius, CornerRadius, RectHitTest, Key);

    void IBlob.WriteTo(ref Frame frame)
    {
        frame.Kind = BlobKind.Sector;
        frame.Key = Key;
        frame.Content = "";
        frame.Style = _style;
        frame.Children = null;
        frame.Texture = null;
        frame.Path = null;
        frame.Scene = null;
        frame.RenderOnce = false;
        frame.Paused = false;
        frame.Color = null;
        frame.Shape = new ShapeParams { A = StartAngle, B = EndAngle, C = InnerRadius, D = OuterRadius, E = CornerRadius, F = RectHitTest ? 1f : 0f };
        frame.Points = null;
        frame.Effect = null;
        frame.Tag = null;
        frame.Draw = null;
        frame.LayoutTransition = null;
        frame.Placeholder   = null;
        frame.MaxLength     = null;
        frame.Disabled      = false;
        frame.Numeric       = false;
        frame.MinValue      = null;
        frame.MaxValue      = null;
        frame.NumberFormat  = null;
        frame.Multiline     = false;
        frame.OnChange      = null;
        frame.OnSubmit      = null;
        frame.OnFocus       = null;
        frame.OnBlur        = null;
        frame.OnCancel      = null;
        frame.MinLength     = null;
        frame.CharacterRegex = null;
        frame.StringRegex   = null;
        frame.CanEnterChar  = null;
        frame.Validate      = null;
        frame.OnValidationChanged = null;
        frame.IsControlled  = false;
        frame.ValueAndDefaultTextBothSet = false;
        frame.Events.OnClick      = _onClick;
        frame.Events.OnRightClick = _onRightClick;
        frame.Events.OnMiddleClick = _onMiddleClick;
        frame.Events.OnMouseEnter = _onMouseEnter;
        frame.Events.OnMouseLeave = _onMouseLeave;
        frame.Events.OnMouseDown  = _onMouseDown;
        frame.Events.OnMouseUp    = _onMouseUp;
        frame.Events.OnMouseMove  = _onMouseMove;
    }
}

/// <summary>A stroked arc line. Sweeps from <see cref="StartAngle"/> to <see cref="EndAngle"/> at a fixed <see cref="Radius"/>.</summary>
public readonly partial record struct Arc : IBlob
{
    public static BlobKind Kind => BlobKind.Arc;
    /// <summary>Angle where the arc begins, in degrees.</summary>
    public float   StartAngle  { get; init; }
    /// <summary>Angle where the arc ends, in degrees.</summary>
    public float   EndAngle    { get; init; }
    /// <summary>Radius of the arc, as a fraction (0 to 1) of the frame box.</summary>
    public float   Radius      { get; init; }
    /// <summary>Thickness of the arc stroke.</summary>
    public float   StrokeWidth { get; init; }
    /// <summary>When true, mouse events hit the full bounding box instead of the drawn outline. For editor-style selection and dragging; visuals are unchanged.</summary>
    public bool    RectHitTest { get; init; }
    /// <summary>Stable identity across rebuilds. Set it when this blob can change position among its siblings, so the reconciler matches it to the same panel.</summary>
    public string? Key         { get; init; }
    internal readonly StyleList _style;

    internal readonly Action<MousePanelEvent>? _onClick;
    internal readonly Action<MousePanelEvent>? _onRightClick;
    internal readonly Action<MousePanelEvent>? _onMiddleClick;
    internal readonly Action<MousePanelEvent>? _onMouseEnter;
    internal readonly Action<MousePanelEvent>? _onMouseLeave;
    internal readonly Action<MousePanelEvent>? _onMouseDown;
    internal readonly Action<MousePanelEvent>? _onMouseUp;
    internal readonly Action<MousePanelEvent>? _onMouseMove;

    /// <summary>Called when clicked with the left mouse button.</summary>
    public Action<MousePanelEvent>? OnClick      { init => _onClick      = value; }
    /// <summary>Called when clicked with the right mouse button.</summary>
    public Action<MousePanelEvent>? OnRightClick { init => _onRightClick = value; }
    /// <summary>Called when clicked with the middle mouse button.</summary>
    public Action<MousePanelEvent>? OnMiddleClick { init => _onMiddleClick = value; }
    /// <summary>Called when the pointer enters.</summary>
    public Action<MousePanelEvent>? OnMouseEnter { init => _onMouseEnter = value; }
    /// <summary>Called when the pointer leaves.</summary>
    public Action<MousePanelEvent>? OnMouseLeave { init => _onMouseLeave = value; }
    /// <summary>Called when a mouse button is pressed.</summary>
    public Action<MousePanelEvent>? OnMouseDown  { init => _onMouseDown  = value; }
    /// <summary>Called when a mouse button is released.</summary>
    public Action<MousePanelEvent>? OnMouseUp    { init => _onMouseUp    = value; }
    /// <summary>Called when the pointer moves over it.</summary>
    public Action<MousePanelEvent>? OnMouseMove  { init => _onMouseMove  = value; }

    public Arc()
    {
        StartAngle = 0f;
        EndAngle = 0f;
        Radius = 0f;
        StrokeWidth = 0f;
        RectHitTest = false;
        Key = null;
        _style = StyleList.Empty;
    }

    public bool Equals(Arc other)
        => StartAngle == other.StartAngle
        && EndAngle == other.EndAngle
        && Radius == other.Radius
        && StrokeWidth == other.StrokeWidth
        && RectHitTest == other.RectHitTest
        && Key == other.Key
        && StyleList.ContentsEqual(_style, other._style);

    public override int GetHashCode()
        => HashCode.Combine(StartAngle, EndAngle, Radius, StrokeWidth, RectHitTest, Key);

    void IBlob.WriteTo(ref Frame frame)
    {
        frame.Kind = BlobKind.Arc;
        frame.Key = Key;
        frame.Content = "";
        frame.Style = _style;
        frame.Children = null;
        frame.Texture = null;
        frame.Path = null;
        frame.Scene = null;
        frame.RenderOnce = false;
        frame.Paused = false;
        frame.Color = null;
        frame.Shape = new ShapeParams { A = StartAngle, B = EndAngle, C = Radius, D = StrokeWidth, F = RectHitTest ? 1f : 0f };
        frame.Points = null;
        frame.Effect = null;
        frame.Tag = null;
        frame.Draw = null;
        frame.LayoutTransition = null;
        frame.Placeholder   = null;
        frame.MaxLength     = null;
        frame.Disabled      = false;
        frame.Numeric       = false;
        frame.MinValue      = null;
        frame.MaxValue      = null;
        frame.NumberFormat  = null;
        frame.Multiline     = false;
        frame.OnChange      = null;
        frame.OnSubmit      = null;
        frame.OnFocus       = null;
        frame.OnBlur        = null;
        frame.OnCancel      = null;
        frame.MinLength     = null;
        frame.CharacterRegex = null;
        frame.StringRegex   = null;
        frame.CanEnterChar  = null;
        frame.Validate      = null;
        frame.OnValidationChanged = null;
        frame.IsControlled  = false;
        frame.ValueAndDefaultTextBothSet = false;
        frame.Events.OnClick      = _onClick;
        frame.Events.OnRightClick = _onRightClick;
        frame.Events.OnMiddleClick = _onMiddleClick;
        frame.Events.OnMouseEnter = _onMouseEnter;
        frame.Events.OnMouseLeave = _onMouseLeave;
        frame.Events.OnMouseDown  = _onMouseDown;
        frame.Events.OnMouseUp    = _onMouseUp;
        frame.Events.OnMouseMove  = _onMouseMove;
    }
}

// Polygon: arbitrary-vertex filled shape baked into a 512x512 alpha mask. Unit coords in [0,1], treated as immutable (the cache key holds the reference). Click hit-testing uses the bounding rect (engine limitation).
/// <summary>A filled shape defined by arbitrary vertices in unit coordinates.</summary>
public readonly partial record struct Polygon : IBlob
{
    public static BlobKind Kind => BlobKind.Polygon;
    /// <summary>The vertices, in unit coordinates from 0 to 1 (origin top-left, x right, y down). Treated as immutable: do not mutate the array after assigning it.</summary>
    public Vector2[]? Points { get; init; }
    /// <summary>When true, mouse events hit the full bounding box instead of the drawn outline. For editor-style selection and dragging; visuals are unchanged.</summary>
    public bool       RectHitTest { get; init; }
    /// <summary>Stable identity across rebuilds. Set it when this blob can change position among its siblings, so the reconciler matches it to the same panel.</summary>
    public string?    Key    { get; init; }
    internal readonly StyleList _style;

    internal readonly Action<MousePanelEvent>? _onClick;
    internal readonly Action<MousePanelEvent>? _onRightClick;
    internal readonly Action<MousePanelEvent>? _onMiddleClick;
    internal readonly Action<MousePanelEvent>? _onMouseEnter;
    internal readonly Action<MousePanelEvent>? _onMouseLeave;
    internal readonly Action<MousePanelEvent>? _onMouseDown;
    internal readonly Action<MousePanelEvent>? _onMouseUp;
    internal readonly Action<MousePanelEvent>? _onMouseMove;

    /// <summary>Called when clicked with the left mouse button.</summary>
    public Action<MousePanelEvent>? OnClick      { init => _onClick      = value; }
    /// <summary>Called when clicked with the right mouse button.</summary>
    public Action<MousePanelEvent>? OnRightClick { init => _onRightClick = value; }
    /// <summary>Called when clicked with the middle mouse button.</summary>
    public Action<MousePanelEvent>? OnMiddleClick { init => _onMiddleClick = value; }
    /// <summary>Called when the pointer enters.</summary>
    public Action<MousePanelEvent>? OnMouseEnter { init => _onMouseEnter = value; }
    /// <summary>Called when the pointer leaves.</summary>
    public Action<MousePanelEvent>? OnMouseLeave { init => _onMouseLeave = value; }
    /// <summary>Called when a mouse button is pressed.</summary>
    public Action<MousePanelEvent>? OnMouseDown  { init => _onMouseDown  = value; }
    /// <summary>Called when a mouse button is released.</summary>
    public Action<MousePanelEvent>? OnMouseUp    { init => _onMouseUp    = value; }
    /// <summary>Called when the pointer moves over it.</summary>
    public Action<MousePanelEvent>? OnMouseMove  { init => _onMouseMove  = value; }

    public Polygon()
    {
        Points = null;
        RectHitTest = false;
        Key = null;
        _style = StyleList.Empty;
    }

    public bool Equals(Polygon other)
        => ReferenceEquals(Points, other.Points)
        && RectHitTest == other.RectHitTest
        && Key == other.Key
        && StyleList.ContentsEqual(_style, other._style);

    public override int GetHashCode()
        => HashCode.Combine(Points, RectHitTest, Key);

    void IBlob.WriteTo(ref Frame frame)
    {
        frame.Kind = BlobKind.Polygon;
        frame.Key = Key;
        frame.Content = "";
        frame.Style = _style;
        frame.Children = null;
        frame.Texture = null;
        frame.Path = null;
        frame.Scene = null;
        frame.RenderOnce = false;
        frame.Paused = false;
        frame.Color = null;
        frame.Shape = new ShapeParams { F = RectHitTest ? 1f : 0f };
        frame.Points = Points;
        frame.Effect = null;
        frame.Tag = null;
        frame.Draw = null;
        frame.LayoutTransition = null;
        frame.Placeholder   = null;
        frame.MaxLength     = null;
        frame.Disabled      = false;
        frame.Numeric       = false;
        frame.MinValue      = null;
        frame.MaxValue      = null;
        frame.NumberFormat  = null;
        frame.Multiline     = false;
        frame.OnChange      = null;
        frame.OnSubmit      = null;
        frame.OnFocus       = null;
        frame.OnBlur        = null;
        frame.OnCancel      = null;
        frame.MinLength     = null;
        frame.CharacterRegex = null;
        frame.StringRegex   = null;
        frame.CanEnterChar  = null;
        frame.Validate      = null;
        frame.OnValidationChanged = null;
        frame.IsControlled  = false;
        frame.ValueAndDefaultTextBothSet = false;
        frame.Events.OnClick      = _onClick;
        frame.Events.OnRightClick = _onRightClick;
        frame.Events.OnMiddleClick = _onMiddleClick;
        frame.Events.OnMouseEnter = _onMouseEnter;
        frame.Events.OnMouseLeave = _onMouseLeave;
        frame.Events.OnMouseDown  = _onMouseDown;
        frame.Events.OnMouseUp    = _onMouseUp;
        frame.Events.OnMouseMove  = _onMouseMove;
    }
}

// WebPanel: embeds an engine Chromium webview. The Url is the only content surface;
// background-image and cursor are owned by the engine WebPanel itself per frame, so
// the common-only facade keeps both out by construction.
/// <summary>Embeds a web view via the engine's Chromium webview. <see cref="Url"/> is its only content surface.</summary>
public readonly partial record struct WebPanel : IBlob
{
    public static BlobKind Kind => BlobKind.WebPanel;
    /// <summary>The URL to load in the web view.</summary>
    public string? Url { get; init; }
    // Maps to engine WebSurface.InBackgroundMode.
    /// <summary>When true, throttles the view's script and repaint and pauses its media. Use for offscreen or hidden views.</summary>
    public bool Paused { get; init; }
    /// <summary>Stable identity across rebuilds. Set it when this blob can change position among its siblings, so the reconciler matches it to the same panel.</summary>
    public string? Key { get; init; }
    internal readonly StyleList _style;

    internal readonly Action<MousePanelEvent>? _onClick;
    internal readonly Action<MousePanelEvent>? _onRightClick;
    internal readonly Action<MousePanelEvent>? _onMiddleClick;
    internal readonly Action<MousePanelEvent>? _onMouseEnter;
    internal readonly Action<MousePanelEvent>? _onMouseLeave;
    internal readonly Action<MousePanelEvent>? _onMouseDown;
    internal readonly Action<MousePanelEvent>? _onMouseUp;
    internal readonly Action<MousePanelEvent>? _onMouseMove;

    /// <summary>Called when clicked with the left mouse button.</summary>
    public Action<MousePanelEvent>? OnClick      { init => _onClick      = value; }
    /// <summary>Called when clicked with the right mouse button.</summary>
    public Action<MousePanelEvent>? OnRightClick { init => _onRightClick = value; }
    /// <summary>Called when clicked with the middle mouse button.</summary>
    public Action<MousePanelEvent>? OnMiddleClick { init => _onMiddleClick = value; }
    /// <summary>Called when the pointer enters.</summary>
    public Action<MousePanelEvent>? OnMouseEnter { init => _onMouseEnter = value; }
    /// <summary>Called when the pointer leaves.</summary>
    public Action<MousePanelEvent>? OnMouseLeave { init => _onMouseLeave = value; }
    /// <summary>Called when a mouse button is pressed.</summary>
    public Action<MousePanelEvent>? OnMouseDown  { init => _onMouseDown  = value; }
    /// <summary>Called when a mouse button is released.</summary>
    public Action<MousePanelEvent>? OnMouseUp    { init => _onMouseUp    = value; }
    /// <summary>Called when the pointer moves over it.</summary>
    public Action<MousePanelEvent>? OnMouseMove  { init => _onMouseMove  = value; }

    public WebPanel()
    {
        Url = null;
        Paused = false;
        Key = null;
        _style = StyleList.Empty;
    }

    public bool Equals(WebPanel other)
        => Url == other.Url
        && Paused == other.Paused
        && Key == other.Key
        && StyleList.ContentsEqual(_style, other._style);

    public override int GetHashCode()
        => HashCode.Combine(Url, Paused, Key);

    void IBlob.WriteTo(ref Frame frame)
    {
        frame.Kind = BlobKind.WebPanel;
        frame.Key = Key;
        frame.Content = "";
        frame.Style = _style;
        frame.Children = null;
        frame.Texture = null;
        frame.Path = Url;
        frame.Scene = null;
        frame.RenderOnce = false;
        frame.Paused = Paused;
        frame.Color = null;
        frame.Shape = default;
        frame.Points = null;
        frame.Effect = null;
        frame.Tag = null;
        frame.Draw = null;
        frame.LayoutTransition = null;
        frame.Placeholder   = null;
        frame.MaxLength     = null;
        frame.Disabled      = false;
        frame.Numeric       = false;
        frame.MinValue      = null;
        frame.MaxValue      = null;
        frame.NumberFormat  = null;
        frame.Multiline     = false;
        frame.OnChange      = null;
        frame.OnSubmit      = null;
        frame.OnFocus       = null;
        frame.OnBlur        = null;
        frame.OnCancel      = null;
        frame.MinLength     = null;
        frame.CharacterRegex = null;
        frame.StringRegex   = null;
        frame.CanEnterChar  = null;
        frame.Validate      = null;
        frame.OnValidationChanged = null;
        frame.IsControlled  = false;
        frame.ValueAndDefaultTextBothSet = false;
        frame.Events.OnClick      = _onClick;
        frame.Events.OnRightClick = _onRightClick;
        frame.Events.OnMiddleClick = _onMiddleClick;
        frame.Events.OnMouseEnter = _onMouseEnter;
        frame.Events.OnMouseLeave = _onMouseLeave;
        frame.Events.OnMouseDown  = _onMouseDown;
        frame.Events.OnMouseUp    = _onMouseUp;
        frame.Events.OnMouseMove  = _onMouseMove;
    }
}

/// <summary>A text input wrapping the engine's TextEntry. Set <see cref="Value"/> for a controlled field driven by your state, or <see cref="DefaultText"/> for an uncontrolled one; never both.</summary>
public readonly partial record struct TextEntry : IBlob
{
    public static BlobKind Kind => BlobKind.TextEntry;

    /// <summary>The field text in controlled mode, re-emitted each render to track your state. Set this or <see cref="DefaultText"/>, never both.</summary>
    public string? Value        { get; init; }
    /// <summary>The starting text in uncontrolled mode, set once when the field is created. Set this or <see cref="Value"/>, never both.</summary>
    public string? DefaultText  { get; init; }
    /// <summary>Hint text shown when the field is empty.</summary>
    public string? Placeholder  { get; init; }
    /// <summary>Maximum number of characters allowed. Null for no limit.</summary>
    public int?    MaxLength    { get; init; }
    /// <summary>When true, the field is read-only and does not accept input.</summary>
    public bool    Disabled     { get; init; }
    /// <summary>When true, the field accepts and wraps multiple lines.</summary>
    public bool    Multiline    { get; init; }
    /// <summary>When true, the field accepts numeric input only.</summary>
    public bool    Numeric      { get; init; }
    /// <summary>Minimum number of characters; shorter input is flagged invalid. Null for no minimum.</summary>
    public int?    MinLength    { get; init; }
    /// <summary>Regex each typed character must match; non-matching keystrokes are blocked. Null to allow any character.</summary>
    public string? CharacterRegex { get; init; }
    /// <summary>Regex the whole text must match to be valid. Null for no whole-text check.</summary>
    public string? StringRegex  { get; init; }
    /// <summary>Smallest accepted value when <see cref="Numeric"/> is true.</summary>
    public float?  MinValue     { get; init; }
    /// <summary>Largest accepted value when <see cref="Numeric"/> is true.</summary>
    public float?  MaxValue     { get; init; }
    /// <summary>Format string applied to the value when <see cref="Numeric"/> is true.</summary>
    public string? NumberFormat { get; init; }
    /// <summary>Stable identity across rebuilds. Set it when this blob can change position among its siblings, so the reconciler matches it to the same panel.</summary>
    public string? Key          { get; init; }

    internal readonly StyleList _style;

    internal readonly Action<string>? _onChange;
    internal readonly Action<string>? _onSubmit;
    internal readonly Action? _onFocus;
    internal readonly Action<string>? _onBlur;
    internal readonly Action? _onCancel;
    internal readonly Func<char, bool>?   _canEnterChar;
    internal readonly Func<string, bool>? _validate;
    internal readonly Action<bool>?       _onValidationChanged;
    internal readonly Action<MousePanelEvent>? _onClick;
    internal readonly Action<MousePanelEvent>? _onRightClick;
    internal readonly Action<MousePanelEvent>? _onMiddleClick;
    internal readonly Action<MousePanelEvent>? _onMouseEnter;
    internal readonly Action<MousePanelEvent>? _onMouseLeave;
    internal readonly Action<MousePanelEvent>? _onMouseDown;
    internal readonly Action<MousePanelEvent>? _onMouseUp;
    internal readonly Action<MousePanelEvent>? _onMouseMove;

    /// <summary>Called on each edit, with the new text.</summary>
    public Action<string>?           OnChange     { init => _onChange     = value; }
    /// <summary>Called when the user submits the field, for example by pressing Enter, with the text.</summary>
    public Action<string>?           OnSubmit     { init => _onSubmit     = value; }
    /// <summary>Called when the field gains focus.</summary>
    public Action?                   OnFocus      { init => _onFocus      = value; }
    /// <summary>Called when the field loses focus, with its final text.</summary>
    public Action<string>?           OnBlur       { init => _onBlur       = value; }
    /// <summary>Called when the user cancels editing, for example by pressing Escape.</summary>
    public Action?                   OnCancel     { init => _onCancel     = value; }
    /// <summary>Predicate gating each typed character; return false to block it. Composed with <see cref="CharacterRegex"/> and the engine's numeric/multiline rules.</summary>
    public Func<char, bool>?   CanEnterChar { init => _canEnterChar = value; }
    /// <summary>Predicate validating the whole text; return false to mark the field invalid. Can only tighten validity, never loosen engine rules.</summary>
    public Func<string, bool>? Validate     { init => _validate     = value; }
    /// <summary>Called when the field's validity flips, with the current error state (true = invalid).</summary>
    public Action<bool>?       OnValidationChanged { init => _onValidationChanged = value; }
    /// <summary>Called when clicked with the left mouse button.</summary>
    public Action<MousePanelEvent>?  OnClick      { init => _onClick      = value; }
    /// <summary>Called when clicked with the right mouse button.</summary>
    public Action<MousePanelEvent>?  OnRightClick { init => _onRightClick = value; }
    /// <summary>Called when clicked with the middle mouse button.</summary>
    public Action<MousePanelEvent>?  OnMiddleClick { init => _onMiddleClick = value; }
    /// <summary>Called when the pointer enters.</summary>
    public Action<MousePanelEvent>?  OnMouseEnter { init => _onMouseEnter = value; }
    /// <summary>Called when the pointer leaves.</summary>
    public Action<MousePanelEvent>?  OnMouseLeave { init => _onMouseLeave = value; }
    /// <summary>Called when a mouse button is pressed.</summary>
    public Action<MousePanelEvent>?  OnMouseDown  { init => _onMouseDown  = value; }
    /// <summary>Called when a mouse button is released.</summary>
    public Action<MousePanelEvent>?  OnMouseUp    { init => _onMouseUp    = value; }
    /// <summary>Called when the pointer moves over it.</summary>
    public Action<MousePanelEvent>?  OnMouseMove  { init => _onMouseMove  = value; }

    public TextEntry()
    {
        Value = null;
        DefaultText = null;
        Placeholder = null;
        MaxLength = null;
        Disabled = false;
        Multiline = false;
        Numeric = false;
        MinLength = null;
        CharacterRegex = null;
        StringRegex = null;
        MinValue = null;
        MaxValue = null;
        NumberFormat = null;
        Key = null;
        _style = StyleList.Empty;
    }

    public bool Equals(TextEntry other)
        => Value == other.Value
        && DefaultText == other.DefaultText
        && Placeholder == other.Placeholder
        && MaxLength == other.MaxLength
        && Disabled == other.Disabled
        && Multiline == other.Multiline
        && Numeric == other.Numeric
        && MinLength == other.MinLength
        && CharacterRegex == other.CharacterRegex
        && StringRegex == other.StringRegex
        && MinValue == other.MinValue
        && MaxValue == other.MaxValue
        && NumberFormat == other.NumberFormat
        && Key == other.Key
        && StyleList.ContentsEqual(_style, other._style);

    public override int GetHashCode()
        => HashCode.Combine(Value, DefaultText, Placeholder, MaxLength, Disabled, Numeric, Multiline, Key);

    void IBlob.WriteTo(ref Frame frame)
    {
        frame.Kind = BlobKind.TextEntry;
        frame.Key = Key;
        frame.Content = "";
        frame.Style = _style;
        frame.Children = null;
        frame.Texture = null;
        // Path slot carries the text value (controlled-or-initial). Reconciler reads
        // IsControlled to decide whether to re-emit on Update.
        frame.Path = Value ?? DefaultText;
        frame.IsControlled = Value is not null;
        frame.ValueAndDefaultTextBothSet = Value is not null && DefaultText is not null;
        frame.Scene = null;
        frame.RenderOnce = false;
        frame.Paused = false;
        frame.Color = null;
        frame.Shape = default;
        frame.Points = null;
        frame.Effect = null;
        frame.Tag = null;
        frame.Draw = null;
        frame.LayoutTransition = null;
        frame.Placeholder = Placeholder;
        frame.MaxLength = MaxLength;
        frame.Disabled = Disabled;
        frame.Numeric = Numeric;
        frame.MinLength = MinLength;
        frame.CharacterRegex = CharacterRegex;
        frame.StringRegex = StringRegex;
        frame.MinValue = MinValue;
        frame.MaxValue = MaxValue;
        frame.NumberFormat = NumberFormat;
        frame.Multiline = Multiline;
        frame.OnChange = _onChange;
        frame.OnSubmit = _onSubmit;
        frame.OnFocus = _onFocus;
        frame.OnBlur = _onBlur;
        frame.OnCancel = _onCancel;
        frame.CanEnterChar = _canEnterChar;
        frame.Validate = _validate;
        frame.OnValidationChanged = _onValidationChanged;
        frame.Events.OnClick      = _onClick;
        frame.Events.OnRightClick = _onRightClick;
        frame.Events.OnMiddleClick = _onMiddleClick;
        frame.Events.OnMouseEnter = _onMouseEnter;
        frame.Events.OnMouseLeave = _onMouseLeave;
        frame.Events.OnMouseDown  = _onMouseDown;
        frame.Events.OnMouseUp    = _onMouseUp;
        frame.Events.OnMouseMove  = _onMouseMove;
    }
}

// Immutable carry-object so Frame/Fiber/Op grow by one reference instead of two delegate fields.
internal sealed class EmbedConfig
{
    public readonly Func<Panel> Create;
    public readonly Action<Panel>? Update;

    public EmbedConfig(Func<Panel> create, Action<Panel>? update)
    {
        Create = create;
        Update = update;
    }
}

/// <summary>
/// Escape hatch: mounts an arbitrary engine <see cref="Panel"/> (a Razor panel, a library
/// control, any imperative panel) inside a Goo tree. <see cref="Create"/> runs once at mount;
/// <see cref="Update"/> runs right after creation and again on every owner rebuild - push
/// current data into the panel there. The panel owns its own internal state; Goo only
/// positions and styles the slot and never diffs inside it. Hosted panels default to
/// PointerEvents.All so they receive input under Goo's pointer-transparent containers;
/// declare PointerEvents on the blob (or in the panel's own constructor) to override.
/// </summary>
public readonly partial record struct Embed : IBlob
{
    public static BlobKind Kind => BlobKind.Embed;
    /// <summary>Builds the hosted panel. Runs exactly once, at mount. Swapping this delegate on a later rebuild does NOT recreate the panel; change <see cref="Key"/> to force a new instance.</summary>
    public Func<Panel>? Create { get; init; }
    /// <summary>Data-push hook: runs after creation and on every rebuild of the owning GooPanel, unconditionally (delegates cannot be diffed). Keep it a cheap property push and let the panel self-diff.</summary>
    public Action<Panel>? Update { get; init; }
    /// <summary>Stable identity across rebuilds. Set it when this blob can change position among its siblings, so the reconciler matches it to the same panel.</summary>
    public string? Key { get; init; }
    internal readonly StyleList _style;

    public Embed()
    {
        Create = null;
        Update = null;
        Key = null;
        _style = StyleList.Empty;
    }

    public bool Equals(Embed other)
        => Key == other.Key && StyleList.ContentsEqual(_style, other._style);
    // Delegates intentionally excluded from equality, mirroring the other blobs.

    public override int GetHashCode() => HashCode.Combine(Key);

    void IBlob.WriteTo(ref Frame frame)
    {
        frame = default;
        frame.Kind = BlobKind.Embed;
        frame.Key = Key;
        frame.Content = "";
        frame.Style = _style;
        frame.Embed = Create is null ? null : new EmbedConfig(Create, Update);
    }
}

/// <summary>Builds the row Blob for one item of a <see cref="Virtual"/> viewport. Named delegate because IBlob's static-abstract Kind bars it from generic type arguments (CS8920), same as BlobBuilder.</summary>
public delegate IBlob RowBuilder(object item);

/// <summary>Typed variant of <see cref="RowBuilder"/> for the Virtual.List/Grid factories.</summary>
public delegate IBlob RowBuilder<in T>(T item) where T : class;

// Immutable carry-object so Frame/Fiber/Op grow by one reference instead of six fields.
internal sealed class VirtualConfig
{
    public readonly IReadOnlyList<object>? Items;
    public readonly int Count;               // captured at build time so same-list mutations are caught on rebuild
    public readonly RowBuilder? Row;
    public readonly Func<BaseVirtualPanel>? Panel;
    public readonly float ItemHeight;
    public readonly Vector2 ItemSize;
    public readonly Action? OnLastRow;

    public VirtualConfig(IReadOnlyList<object>? items, RowBuilder? row, Func<BaseVirtualPanel>? panel,
        float itemHeight, Vector2 itemSize, Action? onLastRow)
    {
        Items = items;
        Count = items?.Count ?? 0;
        Row = row;
        Panel = panel;
        ItemHeight = itemHeight;
        ItemSize = itemSize;
        OnLastRow = onLastRow;
    }
}

/// <summary>
/// A virtualized scrollable viewport over the engine's BaseVirtualPanel: only visible rows exist
/// as panels. Rows are built as Goo blobs via <see cref="Row"/>; stateful nodes inside a row
/// (e.g. TextEntry editors) should carry a per-item Key so a recycled panel never inherits
/// another item's private engine state. Give it a bounded size on the scroll axis.
/// </summary>
public readonly partial record struct Virtual : IBlob
{
    public static BlobKind Kind => BlobKind.Virtual;
    /// <summary>The backing items. Pass the same list reference across rebuilds; a new reference or a count change triggers a refresh (content changes in a new list are diffed per row engine-side).</summary>
    public IReadOnlyList<object>? Items { get; init; }
    /// <summary>Builds the Blob for one item. Runs when the row scrolls into view or its item changes.</summary>
    public RowBuilder? Row { get; init; }
    /// <summary>Fixed row height for list mode. Ignored when <see cref="ItemSize"/> or <see cref="Panel"/> is set.</summary>
    public float ItemHeight { get; init; }
    /// <summary>Fixed cell size for grid mode (y &gt; 0 selects the fiber-backed grid over the list panel).</summary>
    public Vector2 ItemSize { get; init; }
    /// <summary>Escape hatch: supply any BaseVirtualPanel subclass for a custom layout strategy. Create-only.</summary>
    public Func<BaseVirtualPanel>? Panel { get; init; }
    /// <summary>Called when the last item materializes - the infinite-scroll load-more hook.</summary>
    public Action? OnLastRow { get; init; }
    /// <summary>Stable identity across rebuilds. Set it when this blob can change position among its siblings, so the reconciler matches it to the same panel.</summary>
    public string? Key { get; init; }
    internal readonly StyleList _style;

    public Virtual()
    {
        Items = null;
        Row = null;
        ItemHeight = 0f;
        ItemSize = default;
        Panel = null;
        OnLastRow = null;
        Key = null;
        _style = StyleList.Empty;
    }

    /// <summary>A virtualized vertical list: fixed-height rows, one row blob per visible item.</summary>
    public static Virtual List<T>(IReadOnlyList<T> items, float itemHeight, RowBuilder<T> row, string? key = null) where T : class
        => new() { Items = items, ItemHeight = itemHeight, Row = o => row((T)o), Key = key };

    /// <summary>A virtualized grid: fixed-size cells flowing into as many columns as fit.</summary>
    public static Virtual Grid<T>(IReadOnlyList<T> items, Vector2 itemSize, RowBuilder<T> row, string? key = null) where T : class
        => new() { Items = items, ItemSize = itemSize, Row = o => row((T)o), Key = key };

    /// <summary>A virtualized viewport with a custom BaseVirtualPanel layout strategy.</summary>
    public static Virtual Custom<T>(IReadOnlyList<T> items, Func<BaseVirtualPanel> panel, RowBuilder<T> row, string? key = null) where T : class
        => new() { Items = items, Panel = panel, Row = o => row((T)o), Key = key };

    public bool Equals(Virtual other)
        => ReferenceEquals(Items, other.Items)
        && ItemHeight == other.ItemHeight
        && ItemSize == other.ItemSize
        && Key == other.Key
        && StyleList.ContentsEqual(_style, other._style);

    public override int GetHashCode()
        => HashCode.Combine(Items, ItemHeight, ItemSize, Key);
    // _style and delegates intentionally excluded; mirrors the other blobs.

    void IBlob.WriteTo(ref Frame frame)
    {
        frame = default;
        frame.Kind = BlobKind.Virtual;
        frame.Key = Key;
        frame.Content = "";
        frame.Style = _style;
        frame.Virtual = new VirtualConfig(Items, Row, Panel, ItemHeight, ItemSize, OnLastRow);
    }
}

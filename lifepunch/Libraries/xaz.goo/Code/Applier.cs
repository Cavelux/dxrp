using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Sandbox;
using Sandbox.Rendering;
using Sandbox.UI;
using Goo.Internal;
using EnginePanelTransform = Sandbox.UI.PanelTransform;

namespace Goo;

internal static class Applier
{
    // Fast path takes the concrete List<Op> via CollectionsMarshal.AsSpan so the JIT
    // emits an indexed-span loop, avoiding the boxed IEnumerator<Op> and per-element
    // 64B copy that an interface-typed foreach over a value-type Op would incur.
    public static void Apply(Panel root, IReadOnlyList<Op> ops)
    {
        if (ops is List<Op> list)
        {
            var span = CollectionsMarshal.AsSpan(list);
            for (int i = 0; i < span.Length; i++)
                ApplyTimed(root, in span[i]);
            return;
        }

        foreach (var op in ops)
            ApplyTimed(root, in op);
    }

    static void ApplyTimed(Panel root, in Op op)
    {
        if (!Perf.Enabled) { ApplyOne(root, in op); return; }
        long t = System.Diagnostics.Stopwatch.GetTimestamp();
        ApplyOne(root, in op);
        Perf.RecordOp(op.Kind, System.Diagnostics.Stopwatch.GetTimestamp() - t);
    }

    static void ApplyOne(Panel root, in Op op)
    {
        var host = WalkPath(root, op.HostPath);
        switch (op.Kind)
        {
            case OpKind.CreateText:
                {
                    var label = host.AddChild(new StatefulLabel
                    {
                        IsRich = op.IsRich,
                        Text = op.StringPayload!,
                    });
                    host.SetChildIndex(label, op.Index);
                    break;
                }
            case OpKind.UpdateText:
                {
                    if (host.GetChild(op.Index, false) is Label label)
                    {
                        label.IsRich = op.IsRich;
                        label.Text = op.StringPayload!;
                    }
                    break;
                }
            case OpKind.CreateContainer:
                {
                    var panel = host.AddChild(new StatefulDrawPanel());
                    // Default FlexDirection; SetStyle overrides if the StyleList carries one.
                    panel.Style.FlexDirection = FlexDirection.Row;
                    host.SetChildIndex(panel, op.Index);
                    break;
                }
            case OpKind.CreateImage:
                {
                    var img = host.AddChild(new StatefulImage());
                    if (op.StringPayload is not null)
                        img.SetTexture(op.StringPayload);
                    else if (op.Texture is not null)
                        img.Texture = op.Texture;
                    host.SetChildIndex(img, op.Index);
                    break;
                }
            case OpKind.UpdateImage:
                {
                    if (host.GetChild(op.Index, false) is Sandbox.UI.Image img)
                    {
                        if (op.StringPayload is not null)
                            img.SetTexture(op.StringPayload);
                        else if (op.Texture is not null)
                            img.Texture = op.Texture;
                    }
                    break;
                }
            case OpKind.CreateScenePanel:
                {
                    // ScenePath ctor path triggers engine's RenderScene.Load(SceneLoadOptions) and engine owns the scene.
                    // Scene-ref path uses the parameterless ctor and assigns RenderScene, which flips _ownsScene = false.
                    var scenePanel = op.StringPayload is not null
                        ? new StatefulScenePanel(op.StringPayload)
                        : new StatefulScenePanel();
                    if (op.StringPayload is null && op.Scene is not null)
                        scenePanel.RenderScene = op.Scene;
                    scenePanel.RenderOnce = op.RenderOnce;
                    host.AddChild(scenePanel);
                    host.SetChildIndex(scenePanel, op.Index);
                    break;
                }
            case OpKind.UpdateScenePanel:
                {
                    if (host.GetChild(op.Index, false) is Sandbox.UI.ScenePanel scenePanel)
                    {
                        // ScenePath hot-swap: re-load via SceneLoadOptions on the existing RenderScene.
                        // Scene-ref hot-swap: assign RenderScene (disposes prior owned scene if any).
                        if (op.StringPayload is not null)
                        {
                            var options = new SceneLoadOptions { ShowLoadingScreen = false };
                            if (options.SetScene(op.StringPayload))
                                scenePanel.RenderScene.Load(options);
                        }
                        else if (op.Scene is not null)
                        {
                            scenePanel.RenderScene = op.Scene;
                        }
                        scenePanel.RenderOnce = op.RenderOnce;
                    }
                    break;
                }
            case OpKind.CreateSvgPanel:
                {
                    var svg = new StatefulSvgPanel();
                    if (op.StringPayload is not null)
                        svg.Src = op.StringPayload;
                    if (op.Color is not null)
                        svg.Color = op.Color;
                    host.AddChild(svg);
                    host.SetChildIndex(svg, op.Index);
                    break;
                }
            case OpKind.UpdateSvgPanel:
                {
                    if (host.GetChild(op.Index, false) is Sandbox.UI.SvgPanel svg)
                    {
                        svg.Src = op.StringPayload;
                        svg.Color = op.Color;
                    }
                    break;
                }
            case OpKind.CreateSector:
                {
                    var p = new StatefulShapePanel();
                    // Shapes with no explicit size collapse to 0 under the engine's
                    // default Yoga flex layout. Fill the parent unless the user
                    // declared a Width/Height (SetStyle path preserves this default).
                    p.Style.Width  = Length.Percent(100);
                    p.Style.Height = Length.Percent(100);
                    host.AddChild(p);
                    p.RectHitTest = op.Shape.F > 0f;
                    p.ApplyShape(BlobKind.Sector, in op.Shape);
                    host.SetChildIndex(p, op.Index);
                    break;
                }
            case OpKind.UpdateSector:
                {
                    if (host.GetChild(op.Index, false) is StatefulShapePanel p)
                    {
                        p.RectHitTest = op.Shape.F > 0f;
                        p.ApplyShape(BlobKind.Sector, in op.Shape);
                    }
                    break;
                }
            case OpKind.CreateArc:
                {
                    var p = new StatefulShapePanel();
                    p.Style.Width  = Length.Percent(100);
                    p.Style.Height = Length.Percent(100);
                    host.AddChild(p);
                    p.RectHitTest = op.Shape.F > 0f;
                    p.ApplyShape(BlobKind.Arc, in op.Shape);
                    host.SetChildIndex(p, op.Index);
                    break;
                }
            case OpKind.UpdateArc:
                {
                    if (host.GetChild(op.Index, false) is StatefulShapePanel p)
                    {
                        p.RectHitTest = op.Shape.F > 0f;
                        p.ApplyShape(BlobKind.Arc, in op.Shape);
                    }
                    break;
                }
            case OpKind.CreatePolygon:
                {
                    var p = new StatefulShapePanel();
                    p.Style.Width  = Length.Percent(100);
                    p.Style.Height = Length.Percent(100);
                    host.AddChild(p);
                    p.RectHitTest = op.Shape.F > 0f;
                    if (op.Points is not null && op.Points.Length >= 3)
                        p.ApplyPolygon(op.Points);
                    host.SetChildIndex(p, op.Index);
                    break;
                }
            case OpKind.UpdatePolygon:
                {
                    if (host.GetChild(op.Index, false) is StatefulShapePanel p)
                    {
                        p.RectHitTest = op.Shape.F > 0f;
                        if (op.Points is not null && op.Points.Length >= 3)
                            p.ApplyPolygon(op.Points);
                        else
                            p.Style.BackgroundImage = null;
                    }
                    break;
                }
            case OpKind.CreateWebPanel:
                {
                    var wp = new StatefulWebPanel();
                    // WebPanel needs PointerEvents to receive mouse/wheel/key events; default to All so a bare one is interactive (preserved unless the user overrides).
                    wp.Style.PointerEvents = PointerEvents.All;
                    // Engine writes the webview texture to BackgroundImage without a size; force 100% so it fills the panel instead of centering with black bars.
                    wp.Style.BackgroundSizeX = Length.Percent(100);
                    wp.Style.BackgroundSizeY = Length.Percent(100);
                    if (op.StringPayload is not null)
                        wp.Url = op.StringPayload;
                    if (op.Paused)
                        wp.Surface.InBackgroundMode = true;
                    host.AddChild(wp);
                    host.SetChildIndex(wp, op.Index);
                    break;
                }
            case OpKind.UpdateWebPanel:
                {
                    if (host.GetChild(op.Index, false) is Sandbox.UI.WebPanel wp)
                    {
                        wp.Url = op.StringPayload;
                        wp.Surface.InBackgroundMode = op.Paused;
                    }
                    break;
                }
            case OpKind.CreateTextEntry:
                {
                    var te = new StatefulTextEntry();
                    // TextEntry needs PointerEvents to receive focus on click; default to All so a bare one is interactive (preserved unless the user overrides).
                    te.Style.PointerEvents = PointerEvents.All;
                    // Create-time uses the Text setter (safe: not focused yet, so the Value-setter HasFocus guard is irrelevant; the Update arm uses Value for controlled mode).
                    if (op.StringPayload is not null)
                        te.Text = op.StringPayload;
                    if (op.Placeholder is not null)
                        te.Placeholder = op.Placeholder;
                    if (op.MaxLength.HasValue)
                        te.MaxLength = op.MaxLength.Value;
                    te.Disabled = op.Disabled;
                    te.Numeric = op.Numeric;
                    te.MinLength = op.MinLength;
                    te.CharacterRegex = op.CharacterRegex;
                    te.StringRegex = op.StringRegex;
                    if (op.MinValue.HasValue)
                        te.MinValue = op.MinValue.Value;
                    if (op.MaxValue.HasValue)
                        te.MaxValue = op.MaxValue.Value;
                    if (op.NumberFormat is not null)
                        te.NumberFormat = op.NumberFormat;
                    te.Multiline = op.Multiline;
                    te._onChange = op.OnChange;
                    te._onSubmit = op.OnSubmit;
                    te._onFocus = op.OnFocus;
                    te._onBlur = op.OnBlur;
                    te._onCancel = op.OnCancel;
                    te._canEnterChar = op.CanEnterChar;
                    te._validate = op.Validate;
                    te._onValidationChanged = op.OnValidationChanged;
                    // OnChange/OnSubmit live outside BlobEvents, so a text-only entry (no mouse
                    // handlers) never gets a SetEvents op. Wire RequestRebuild here too so typing
                    // auto-rebuilds. Same policy the SetEvents arm reads from BuildContext.
                    var teCtx = BuildContext._current;
                    te.RequestRebuild = (teCtx != null && teCtx.AutoRebuildOnEvents) ? teCtx.RootRebuild : null;
                    // Compute initial validity now so the 'invalid' class / callback reflect the starting text.
                    te.RecomputeValidation();
                    host.AddChild(te);
                    host.SetChildIndex(te, op.Index);
                    break;
                }
            case OpKind.UpdateTextEntry:
                {
                    if (host.GetChild(op.Index, false) is Sandbox.UI.TextEntry te)
                    {
                        var ste = te as Goo.Internal.StatefulTextEntry;

                        te.Placeholder = op.Placeholder;
                        te.MaxLength = op.MaxLength;
                        te.Disabled = op.Disabled;
                        te.Numeric = op.Numeric;
                        te.MinLength = op.MinLength;
                        te.CharacterRegex = op.CharacterRegex;
                        te.StringRegex = op.StringRegex;
                        te.MinValue = op.MinValue;
                        te.MaxValue = op.MaxValue;
                        te.NumberFormat = op.NumberFormat;
                        te.Multiline = op.Multiline;

                        if (ste != null)
                        {
                            ste._onChange = op.OnChange;
                            ste._onSubmit = op.OnSubmit;
                            ste._onFocus = op.OnFocus;
                            ste._onBlur = op.OnBlur;
                            ste._onCancel = op.OnCancel;
                            ste._canEnterChar = op.CanEnterChar;
                            ste._validate = op.Validate;
                            ste._onValidationChanged = op.OnValidationChanged;
                            // See CreateTextEntry: keep RequestRebuild wired in case this entry
                            // never receives a SetEvents op (no mouse handlers in BlobEvents).
                            var steCtx = BuildContext._current;
                            ste.RequestRebuild = (steCtx != null && steCtx.AutoRebuildOnEvents) ? steCtx.RootRebuild : null;
                        }

                        // Controlled mode re-emits Value every render (the engine's HasFocus guard
                        // no-ops mid-typing). Done AFTER all validation config is applied so the
                        // OnValueChanged it triggers validates against the current rules and predicate;
                        // uncontrolled mode never touches text (engine owns it after DefaultText).
                        if (op.IsControlled)
                            te.Value = op.StringPayload ?? "";

                        // Recompute for the config-only-change case where no controlled Value set
                        // fired OnValueChanged; flip-gated, so a redundant call is a harmless no-op.
                        ste?.RecomputeValidation();
                    }
                    break;
                }
            case OpKind.CreateEmbed:
                {
                    var cfg = op.Embed!;
                    var hosted = cfg.Create();
                    // Embed-hosted panels are intrinsically interactive (WebPanel treatment): the marker
                    // class routes the style pass to All, and ??= keeps a bare embed clickable under
                    // Goo's None-stamped ancestors. An explicit declaration (blob or ctor) still wins.
                    hosted.AddClass("goo-embed");
                    hosted.Style.PointerEvents ??= PointerEvents.All;
                    host.AddChild(hosted);
                    host.SetChildIndex(hosted, op.Index);
                    cfg.Update?.Invoke(hosted);
                    break;
                }
            case OpKind.UpdateEmbed:
                {
                    if (host.GetChild(op.Index, false) is { } hosted)
                        op.Embed!.Update?.Invoke(hosted);
                    break;
                }
            case OpKind.CreateVirtual:
                {
                    var cfg = op.Virtual!;
                    // Both modes default to the fiber-backed Tier-2 panels (recycle + rediff);
                    // cfg.Panel is the escape hatch back to a stock engine panel.
                    var vp = cfg.Panel?.Invoke()
                        ?? (cfg.ItemSize.y > 0
                            ? new GooVirtualGrid { ItemSize = cfg.ItemSize }
                            : cfg.ItemHeight > 0
                                ? (BaseVirtualPanel)new GooVirtualList { ItemHeight = cfg.ItemHeight }
                                : new GooVirtualList());
                    // Scroll viewport must be hittable or the wheel never arrives; SetStyle re-derives this.
                    vp.Style.PointerEvents = PointerEvents.All;
                    WireVirtual(vp, cfg);
                    host.AddChild(vp);
                    host.SetChildIndex(vp, op.Index);
                    if (cfg.Items is not null)
                        vp.SetItems(cfg.Items);
                    break;
                }
            case OpKind.UpdateVirtual:
                {
                    if (host.GetChild(op.Index, false) is BaseVirtualPanel vp)
                    {
                        var cfg = op.Virtual!;
                        WireVirtual(vp, cfg);
                        if (cfg.Items is not null)
                            vp.SetItems(cfg.Items);
                        else
                            vp.Clear();
                    }
                    break;
                }
            case OpKind.RemoveAt:
                {
                    host.GetChild(op.Index, false)?.Delete(true);
                    break;
                }
            case OpKind.MoveAt:
                {
                    var child = host.GetChild(op.Index, false);
                    if (child is not null)
                        host.SetChildIndex(child, op.ToIndex);
                    break;
                }
            case OpKind.SetStyle:
                {
                    ApplyAllDeclaredFields(host, host.Style, op.Style!);
                    break;
                }
            case OpKind.SetEvents:
                {
                    // HostPath for SetEvents resolves directly to the target panel
                    // (not its parent), so `host` here is the target.
                    if (host is IStatefulEventHost evHost)
                    {
                        evHost.ApplyEvents(op.Events);
                        // Auto-rebuild-after-dispatch: wire the panel's rebuild request from the
                        // owning GooPanel, unless it opted out. BuildContext._current is the
                        // owning panel's context for the whole Apply pass.
                        var ctx = BuildContext._current;
                        evHost.RequestRebuild = (ctx != null && ctx.AutoRebuildOnEvents) ? ctx.RootRebuild : null;
                        // Apply-or-clear: a one-way All write would leave a panel that lost its
                        // handlers hittable forever, so the engine never hands the mouse back to the
                        // game (Panel.WantsMouseInput scans for All). Clear to None when handlers go.
                        if (!evHost.UserSetPointerEvents)
                        {
                            if (op.Events.AnyNonNull)
                                host.Style.PointerEvents = PointerEvents.All;
                            else if (host is not Sandbox.UI.WebPanel and not Sandbox.UI.TextEntry
                                && (host is not IStatefulHost sh || !sh.HasActiveStateVariants))
                                host.Style.PointerEvents = PointerEvents.None;
                        }
                    }
                    break;
                }
            case OpKind.SetDraw:
                {
                    if (host is StatefulDrawPanel p)
                        p._draw = op.DrawCallback;
                    break;
                }
            case OpKind.SetEffect:
                {
                    if (host is StatefulDrawPanel p)
                    {
                        p._effect = op.Effect;
                        // Create the Material here on the main thread; Draw (render thread) cannot.
                        op.Effect?.Warm();
                    }
                    break;
                }
            case OpKind.SetLayoutTransition:
                {
                    if (host is StatefulDrawPanel p)
                        p.SetLayoutTransition(op.LayoutTransition);
                    break;
                }
        }
    }

    // Rows are one-shot blob mounts; the rebuild hook is the OWNING panel's, captured at apply
    // time, so a click in a row rebuilds the outer tree (which refreshes rows via Items).
    static void WireVirtual(BaseVirtualPanel vp, VirtualConfig cfg)
    {
        vp.OnLastCell = cfg.OnLastRow;
        var row = cfg.Row;
        var ctx = BuildContext._current;
        var rebuild = (ctx != null && ctx.AutoRebuildOnEvents) ? ctx.RootRebuild : null;
        // Fiber-backed panels own their own row diffing; OnCreateCell never fires on them.
        if (vp is GooVirtualPanel gvp)
        {
            gvp.Row = row;
            gvp.RootRebuild = rebuild;
            return;
        }
        if (row is null) { vp.OnCreateCell = null; return; }
        vp.OnCreateCell = (panel, data) => RowMounter.MountInto(panel, row, data, rebuild);
    }

    // One-pass resolution scratch for ApplyAllDeclaredFields: forward fill keeps last-declared-wins,
    // and every Try* probe below becomes an O(1) read instead of a reverse scan of the whole
    // declared list per field (~130 probes per SetStyle). Apply runs on the main thread only.
    const int StyleFieldCount = (int)StyleField.Disabled + 1;
    static readonly StyleValue[] _resolvedValues = new StyleValue[StyleFieldCount];
    static readonly bool[] _resolvedDeclared = new bool[StyleFieldCount];

    static void ResolveDeclaredFields(StyleList style)
    {
        Array.Clear(_resolvedValues);
        Array.Clear(_resolvedDeclared);
        for (int i = 0; i < style.Count; i++)
        {
            ref var e = ref style[i];
            _resolvedDeclared[(int)e.Field] = true;
            _resolvedValues[(int)e.Field] = e.Value;
        }
    }

    static bool TryResolved(StyleField f, out StyleValue value)
    {
        value = _resolvedValues[(int)f];
        return _resolvedDeclared[(int)f];
    }

    static void ApplyAllDeclaredFields(Panel host, PanelStyle target, StyleList style)
    {
        ResolveDeclaredFields(style);

        // Track whether the user declared PointerEvents in their style; the SetEvents handler
        // uses this flag to decide whether to auto-gate to PointerEvents.All when any handler is set.
        if (host is IStatefulEventHost evHost)
            evHost.UserSetPointerEvents = _resolvedDeclared[(int)StyleField.PointerEvents];


        Length? padding       = TryLen(StyleField.Padding);
        Length? paddingLeft   = TryLen(StyleField.PaddingLeft)   ?? padding;
        Length? paddingTop    = TryLen(StyleField.PaddingTop)    ?? padding;
        Length? paddingRight  = TryLen(StyleField.PaddingRight)  ?? padding;
        Length? paddingBottom = TryLen(StyleField.PaddingBottom) ?? padding;

        Length? margin       = TryLen(StyleField.Margin);
        Length? marginLeft   = TryLen(StyleField.MarginLeft)   ?? margin;
        Length? marginTop    = TryLen(StyleField.MarginTop)    ?? margin;
        Length? marginRight  = TryLen(StyleField.MarginRight)  ?? margin;
        Length? marginBottom = TryLen(StyleField.MarginBottom) ?? margin;

        Length? gap       = TryLen(StyleField.Gap);
        Length? rowGap    = TryLen(StyleField.RowGap)    ?? gap;
        Length? columnGap = TryLen(StyleField.ColumnGap) ?? gap;

        Length? borderRadius            = TryLen(StyleField.BorderRadius);
        Length? borderTopLeftRadius     = TryLen(StyleField.BorderTopLeftRadius)     ?? borderRadius;
        Length? borderTopRightRadius    = TryLen(StyleField.BorderTopRightRadius)    ?? borderRadius;
        Length? borderBottomRightRadius = TryLen(StyleField.BorderBottomRightRadius) ?? borderRadius;
        Length? borderBottomLeftRadius  = TryLen(StyleField.BorderBottomLeftRadius)  ?? borderRadius;

        // BorderColor fan-out: shorthand default applied to per-edge reads.
        Color? borderColor       = TryColor(StyleField.BorderColor);
        Color? borderLeftColor   = TryColor(StyleField.BorderLeftColor)   ?? borderColor;
        Color? borderTopColor    = TryColor(StyleField.BorderTopColor)    ?? borderColor;
        Color? borderRightColor  = TryColor(StyleField.BorderRightColor)  ?? borderColor;
        Color? borderBottomColor = TryColor(StyleField.BorderBottomColor) ?? borderColor;

        // BorderWidth fan-out: shorthand default applied to per-edge reads.
        Length? borderWidth       = TryLen(StyleField.BorderWidth);
        Length? borderLeftWidth   = TryLen(StyleField.BorderLeftWidth)   ?? borderWidth;
        Length? borderTopWidth    = TryLen(StyleField.BorderTopWidth)    ?? borderWidth;
        Length? borderRightWidth  = TryLen(StyleField.BorderRightWidth)  ?? borderWidth;
        Length? borderBottomWidth = TryLen(StyleField.BorderBottomWidth) ?? borderWidth;

        // FlexDirection is non-nullable on PanelStyle; default to Row when undeclared.
        target.FlexDirection   = TryFlex(StyleField.FlexDirection) ?? FlexDirection.Row;
        target.JustifyContent  = TryJustify(StyleField.JustifyContent);
        target.AlignItems      = TryAlign(StyleField.AlignItems);
        target.Display         = TryDisplay(StyleField.Display);
        // Shape panels need to fill their parent by default so they don't collapse
        // to 0 under flex layout; explicit user Width/Height still wins.
        target.Width           = TryLen(StyleField.Width)
                              ?? (host is Goo.Internal.StatefulShapePanel ? Length.Percent(100) : (Length?)null);
        target.Height          = TryLen(StyleField.Height)
                              ?? (host is Goo.Internal.StatefulShapePanel ? Length.Percent(100) : (Length?)null);
        // Shapes draw via the IPanelDraw effect quad: BackgroundColor rides the ShapeColor uniform (set below), not the engine background path.
        bool _isShape     = host is Goo.Internal.StatefulShapePanel;
        bool _isWebPanel  = host is Goo.Internal.StatefulWebPanel;
        bool _isTextEntry = host is Goo.Internal.StatefulTextEntry;
        bool _isImage     = host is Goo.Internal.StatefulImage;
        bool _isVirtual   = host is BaseVirtualPanel;

        // State variants (hover/active/focus). Read up here because the inline base
        // colors depend on them: the engine cascade merges inline styles after all
        // stylesheet rules, so a channel with a variant must move its declared base
        // into the variant sheet and leave the inline slot null, or the variant
        // never visibly flips. The pointer-events policy below also needs them.
        Color? hoverBg  = TryColor(StyleField.HoverBackgroundColor);
        Color? activeBg = TryColor(StyleField.ActiveBackgroundColor);
        Color? focusBg  = TryColor(StyleField.FocusBackgroundColor);
        Color? hoverFg  = TryColor(StyleField.HoverFontColor);
        Color? activeFg = TryColor(StyleField.ActiveFontColor);
        Color? focusFg  = TryColor(StyleField.FocusFontColor);

        bool hasBgVariant = hoverBg.HasValue || activeBg.HasValue || focusBg.HasValue;
        bool hasFgVariant = hoverFg.HasValue || activeFg.HasValue || focusFg.HasValue;
        bool hasAnyVariant = hasBgVariant || hasFgVariant;

        StateController.SplitBaseColors(
            TryColor(StyleField.BackgroundColor),
            TryColor(StyleField.FontColor),
            hasBgVariant, hasFgVariant, _isShape,
            out var inlineBg, out var inlineFg,
            out var sheetBg, out var sheetFg);

        target.BackgroundColor = inlineBg;

        target.Padding = padding;
        target.PaddingLeft  = paddingLeft;     target.PaddingTop     = paddingTop;
        target.PaddingRight = paddingRight;    target.PaddingBottom  = paddingBottom;

        target.Margin = margin;
        target.MarginLeft  = marginLeft;       target.MarginTop      = marginTop;
        target.MarginRight = marginRight;      target.MarginBottom   = marginBottom;

        target.RowGap = rowGap;                target.ColumnGap      = columnGap;

        target.BorderTopLeftRadius     = borderTopLeftRadius;
        target.BorderTopRightRadius    = borderTopRightRadius;
        target.BorderBottomRightRadius = borderBottomRightRadius;
        target.BorderBottomLeftRadius  = borderBottomLeftRadius;

        // Align properties.
        target.AlignContent = TryAlign(StyleField.AlignContent);
        target.AlignSelf    = TryAlign(StyleField.AlignSelf);

        // Aspect ratio.
        target.AspectRatio = TrySingle(StyleField.AspectRatio);

        // Backdrop filter properties.
        target.BackdropFilterBlur       = TryLen(StyleField.BackdropFilterBlur);
        target.BackdropFilterBrightness = TryLen(StyleField.BackdropFilterBrightness);
        target.BackdropFilterContrast   = TryLen(StyleField.BackdropFilterContrast);
        target.BackdropFilterHueRotate  = TryLen(StyleField.BackdropFilterHueRotate);
        target.BackdropFilterInvert     = TryLen(StyleField.BackdropFilterInvert);
        target.BackdropFilterSaturate   = TryLen(StyleField.BackdropFilterSaturate);
        target.BackdropFilterSepia      = TryLen(StyleField.BackdropFilterSepia);

        // Background properties.
        target.BackgroundAngle          = TryLen(StyleField.BackgroundAngle);
        target.BackgroundBlendMode      = TryString(StyleField.BackgroundBlendMode);
        // Shapes draw via the effect quad, not the engine background path; leave their BackgroundImage/Size untouched. Other panels pass through (undeclared clears).
        if (!_isShape)
            target.BackgroundImage      = TryTexture(StyleField.BackgroundImage);
        target.BackgroundPlaybackPaused = TryBool(StyleField.BackgroundPlaybackPaused);
        target.BackgroundPositionX      = TryLen(StyleField.BackgroundPositionX);
        target.BackgroundPositionY      = TryLen(StyleField.BackgroundPositionY);
        // Image defaults to NoRepeat so ObjectFit.Contain's aspect-preserved empty bars
        // don't get tiled by the engine's Repeat default (engine-fact-objectfit-contain-tiles:
        // Panel.Draw.cs DrawBackgroundTexture defaults BackgroundRepeat to Repeat when null).
        // Explicit user value still wins. Mirrors the WebPanel BackgroundSize default below.
        target.BackgroundRepeat         = TryBackgroundRepeat(StyleField.BackgroundRepeat)
                                       ?? (_isImage ? BackgroundRepeat.NoRepeat : (BackgroundRepeat?)null);
        if (!_isShape)
        {
            // WebPanel needs BackgroundSize=100% so the engine-managed webview texture
            // fills the panel instead of being centered at native size with black bars
            // around it. Explicit user value still wins.
            target.BackgroundSizeX      = TryLen(StyleField.BackgroundSizeX)
                                       ?? (_isWebPanel ? Length.Percent(100) : (Length?)null);
            target.BackgroundSizeY      = TryLen(StyleField.BackgroundSizeY)
                                       ?? (_isWebPanel ? Length.Percent(100) : (Length?)null);
        }
        // Shapes route colour through the ShapeColor uniform (all kinds), so BackgroundTint never applies to them; push the uniform + hover knobs here.
        if (_isShape)
        {
            var shapePanel = (Goo.Internal.StatefulShapePanel)host;
            shapePanel.SetShapeColor(TryColor(StyleField.BackgroundColor) ?? Color.White);
            shapePanel.SetHover(TryColor(StyleField.HoverBackgroundColor), TryInt32(StyleField.TransitionMs) ?? 0);
        }
        target.BackgroundTint           = TryColor(StyleField.BackgroundTint);

        // Border color: shorthand first, per-edge override.
        target.BorderColor       = borderColor;
        target.BorderLeftColor   = borderLeftColor;   target.BorderTopColor    = borderTopColor;
        target.BorderRightColor  = borderRightColor;  target.BorderBottomColor = borderBottomColor;

        // Border image properties.
        target.BorderImageFill        = TryBorderImageFill(StyleField.BorderImageFill);
        target.BorderImageRepeat      = TryBorderImageRepeat(StyleField.BorderImageRepeat);
        target.BorderImageSource      = TryTexture(StyleField.BorderImageSource);
        target.BorderImageTint        = TryColor(StyleField.BorderImageTint);
        target.BorderImageWidthBottom = TryLen(StyleField.BorderImageWidthBottom);
        target.BorderImageWidthLeft   = TryLen(StyleField.BorderImageWidthLeft);
        target.BorderImageWidthRight  = TryLen(StyleField.BorderImageWidthRight);
        target.BorderImageWidthTop    = TryLen(StyleField.BorderImageWidthTop);

        // Border width: shorthand first, per-edge override.
        target.BorderWidth       = borderWidth;
        target.BorderLeftWidth   = borderLeftWidth;   target.BorderTopWidth    = borderTopWidth;
        target.BorderRightWidth  = borderRightWidth;  target.BorderBottomWidth = borderBottomWidth;

        // Position edges.
        target.Bottom = TryLen(StyleField.Bottom);
        target.Left   = TryLen(StyleField.Left);
        target.Right  = TryLen(StyleField.Right);
        target.Top    = TryLen(StyleField.Top);

        // Caret.
        target.CaretColor = TryColor(StyleField.CaretColor);

        // Cursor. TextEntry defaults to "text" so consumers don't have to opt in
        // to the I-beam; explicit user value wins.
        target.Cursor = TryString(StyleField.Cursor)
                     ?? (_isTextEntry ? "text" : null);

        // Filter properties.
        target.FilterBlur        = TryLen(StyleField.FilterBlur);
        target.FilterBorderColor = TryColor(StyleField.FilterBorderColor);
        target.FilterBorderWidth = TryLen(StyleField.FilterBorderWidth);
        target.FilterBrightness  = TryLen(StyleField.FilterBrightness);
        target.FilterContrast    = TryLen(StyleField.FilterContrast);
        target.FilterHueRotate   = TryLen(StyleField.FilterHueRotate);
        target.FilterInvert      = TryLen(StyleField.FilterInvert);
        target.FilterSaturate    = TryLen(StyleField.FilterSaturate);
        target.FilterSepia       = TryLen(StyleField.FilterSepia);
        target.FilterTint        = TryColor(StyleField.FilterTint);

        // Flex properties.
        target.FlexBasis  = TryLen(StyleField.FlexBasis);
        target.FlexGrow   = TrySingle(StyleField.FlexGrow);
        target.FlexShrink = TrySingle(StyleField.FlexShrink);
        target.FlexWrap   = TryWrap(StyleField.FlexWrap);

        // Font properties.
        target.FontColor          = inlineFg;
        target.FontFamily         = TryString(StyleField.FontFamily);
        target.FontSize           = TryLen(StyleField.FontSize);
        target.FontSmooth         = TryFontSmooth(StyleField.FontSmooth);
        target.FontStyle          = TryFontStyle(StyleField.FontStyle);
        target.FontVariantNumeric = TryFontVariantNumeric(StyleField.FontVariantNumeric);
        target.FontWeight         = TryInt32(StyleField.FontWeight);

        // Image rendering.
        target.ImageRendering = TryImageRendering(StyleField.ImageRendering);

        // Letter and line spacing.
        target.LetterSpacing = TryLen(StyleField.LetterSpacing);
        target.LineHeight    = TryLen(StyleField.LineHeight);

        // Mask properties.
        target.MaskAngle     = TryLen(StyleField.MaskAngle);
        target.MaskImage     = TryTexture(StyleField.MaskImage);
        target.MaskMode      = TryMaskMode(StyleField.MaskMode);
        target.MaskPositionX = TryLen(StyleField.MaskPositionX);
        target.MaskPositionY = TryLen(StyleField.MaskPositionY);
        target.MaskRepeat    = TryBackgroundRepeat(StyleField.MaskRepeat);
        target.MaskScope     = TryMaskScope(StyleField.MaskScope);
        target.MaskSizeX     = TryLen(StyleField.MaskSizeX);
        target.MaskSizeY     = TryLen(StyleField.MaskSizeY);

        // Min/max size.
        target.MaxHeight = TryLen(StyleField.MaxHeight);
        target.MaxWidth  = TryLen(StyleField.MaxWidth);
        target.MinHeight = TryLen(StyleField.MinHeight);
        target.MinWidth  = TryLen(StyleField.MinWidth);

        // Mix blend mode.
        target.MixBlendMode = TryString(StyleField.MixBlendMode);

        // Object fit.
        target.ObjectFit = TryObjectFit(StyleField.ObjectFit);

        // Opacity.
        target.Opacity = TrySingle(StyleField.Opacity);

        // Order.
        target.Order = TryInt32(StyleField.Order);

        // Outline properties.
        target.OutlineColor  = TryColor(StyleField.OutlineColor);
        target.OutlineOffset = TryLen(StyleField.OutlineOffset);
        target.OutlineWidth  = TryLen(StyleField.OutlineWidth);

        // Engine Overflow is a facade writing both axis fields; coalesce or the per-axis nulls wipe it.
        // Virtual panels default to Scroll (their ctor value) so a user style write cannot kill scrolling.
        var overflow = TryOverflowMode(StyleField.Overflow)
                    ?? (_isVirtual ? OverflowMode.Scroll : (OverflowMode?)null);
        target.OverflowX = TryOverflowMode(StyleField.OverflowX) ?? overflow;
        target.OverflowY = TryOverflowMode(StyleField.OverflowY) ?? overflow;

        // Perspective origin.
        target.PerspectiveOriginX = TryLen(StyleField.PerspectiveOriginX);
        target.PerspectiveOriginY = TryLen(StyleField.PerspectiveOriginY);

        // Pointer events: WebPanel and TextEntry default to All (intrinsically interactive); also gate to All when handlers exist so a style-only rebuild does not clobber the events-driven gate. A handler-less panel with a state variant stays capturing (unset); a fully inert panel resolves to None so it is pointer-transparent. Explicit value wins.
        bool hasHandlers = host is IStatefulEventHost peHost && peHost.HasEventHandlers;
        target.PointerEvents = TryPointerEvents(StyleField.PointerEvents)
            ?? (_isVirtual ? PointerEvents.All
                : PointerEventsPolicy.Resolve(null, _isWebPanel, hasHandlers, hasStateVariant: hasAnyVariant, isTextEntry: _isTextEntry,
                    isEmbed: host.HasClass("goo-embed")));
        target.Position      = TryPositionMode(StyleField.Position)
            ?? (_isVirtual ? PositionMode.Relative : (PositionMode?)null);

        // Shadow lists: PanelStyle's ShadowList fields are mutable lists, not self-diffing
        // setters, so diff here and mark dirty manually on change.
        ApplyShadows(target, target.BoxShadow, TryShadows(StyleField.BoxShadow), allowInset: false);
        ApplyShadows(target, target.TextShadow, TryShadows(StyleField.TextShadow), cancelInheritance: true);

        // Sound.
        target.SoundIn  = TryString(StyleField.SoundIn);
        target.SoundOut = TryString(StyleField.SoundOut);

        // Text properties.
        target.TextAlign               = TryTextAlign(StyleField.TextAlign);
        target.TextBackgroundAngle     = TryLen(StyleField.TextBackgroundAngle);
        target.TextDecorationColor     = TryColor(StyleField.TextDecorationColor);
        target.TextDecorationLine      = TryTextDecoration(StyleField.TextDecorationLine);
        target.TextDecorationSkipInk   = TryTextSkipInk(StyleField.TextDecorationSkipInk);
        target.TextDecorationStyle     = TryTextDecorationStyle(StyleField.TextDecorationStyle);
        target.TextDecorationThickness = TryLen(StyleField.TextDecorationThickness);
        target.TextFilter              = TryFilterMode(StyleField.TextFilter);
        target.TextLineThroughOffset   = TryLen(StyleField.TextLineThroughOffset);
        target.TextOverflow            = TryTextOverflow(StyleField.TextOverflow);
        target.TextOverlineOffset      = TryLen(StyleField.TextOverlineOffset);
        target.TextStrokeColor         = TryColor(StyleField.TextStrokeColor);
        target.TextStrokeWidth         = TryLen(StyleField.TextStrokeWidth);
        target.TextTransform           = TryTextTransform(StyleField.TextTransform);
        target.TextUnderlineOffset     = TryLen(StyleField.TextUnderlineOffset);

        // Transform.
        target.Transform = TryPanelTransform(StyleField.Transform);

        // Transform origin.
        target.TransformOriginX = TryLen(StyleField.TransformOriginX);
        target.TransformOriginY = TryLen(StyleField.TransformOriginY);

        // White space and word properties.
        target.WhiteSpace  = TryWhiteSpace(StyleField.WhiteSpace);
        target.WordBreak   = TryWordBreak(StyleField.WordBreak);
        target.WordSpacing = TryLen(StyleField.WordSpacing);

        // Z-index, scaled so a declared value dominates sibling document order.
        target.ZIndex = ZIndexScaling.Scale(TryInt32(StyleField.ZIndex));

        int? transMs = TryInt32(StyleField.TransitionMs);

        if (host is IStatefulHost stateful)
        {
            if (hasAnyVariant)
                stateful.ApplyStateVariants(
                    baseBg: sheetBg,
                    baseFg: sheetFg,
                    hoverBg: hoverBg, activeBg: activeBg, focusBg: focusBg,
                    hoverFg: hoverFg, activeFg: activeFg, focusFg: focusFg,
                    transitionMs: transMs);
            else
                // Reused panels can carry a prior occupant's variant sheet; shed it
                // so its stale base/:hover background does not bleed through.
                stateful.ClearStateVariants();
        }

        // Disabled is the final word: state variants force PointerEvents.All for hover delivery, so this must run after them.
        bool disabled = TryBool(StyleField.Disabled) == true;
        if (disabled)
        {
            target.PointerEvents = PointerEvents.None;
            target.Opacity = Container.ResolveDisabledOpacity(target.Opacity);
        }
    }

    // Each Try* ends with (T?)null not null: a bare null lets the ternary resolve through engine types' implicit string operator and NPE. See engine-fact memories.

    static Length? TryLen(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.Length ? v.LengthVal : (Length?)null;

    static Color? TryColor(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.Color ? v.ColorVal : (Color?)null;

    static FlexDirection? TryFlex(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.FlexDirection ? ((FlexDirection)v.EnumVal) : (FlexDirection?)null;

    static Justify? TryJustify(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.Justify ? ((Justify)v.EnumVal) : (Justify?)null;

    static Align? TryAlign(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.Align ? ((Align)v.EnumVal) : (Align?)null;

    static DisplayMode? TryDisplay(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.DisplayMode ? ((DisplayMode)v.EnumVal) : (DisplayMode?)null;

    static string? TryString(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.String ? (string?)v.RefVal : null;

    static Shadows? TryShadows(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.Shadows ? (Shadows)v.RefVal! : (Shadows?)null;

    static readonly Shadow EmptyTextShadow = new() { Color = Color.Transparent };
    static bool _warnedUnsupportedInsetShadow;

    internal static void ApplyShadows(
        PanelStyle target,
        ShadowList list,
        Shadows? value,
        bool allowInset = true,
        bool cancelInheritance = false)
    {
        var shadows = value ?? default;
        int sourceCount = shadows.Count;
        int count = 0;
        for (int i = 0; i < sourceCount; i++)
            if (allowInset || !shadows[i].Inset)
                count++;

        bool none = value.HasValue && sourceCount == 0;
        bool useEmptySentinel = none && cancelInheritance;
        int appliedCount = count + (useEmptySentinel ? 1 : 0);
        bool isNone = none && !useEmptySentinel;
        if (!allowInset && count != sourceCount && !_warnedUnsupportedInsetShadow)
        {
            _warnedUnsupportedInsetShadow = true;
            Log.Warning("Goo: BoxShadow inset is unsupported and was ignored.");
        }

        if (list.Count == appliedCount && list.IsNone == isNone)
        {
            bool same = !useEmptySentinel || Shadows.Same(list[0], EmptyTextShadow);
            int applied = 0;
            for (int i = 0; same && i < sourceCount; i++)
            {
                var shadow = shadows[i];
                if (!allowInset && shadow.Inset) continue;
                if (Shadows.Same(list[applied++], shadow)) continue;
                same = false;
                break;
            }
            if (same) return;
        }

        list.Clear();
        list.IsNone = isNone;
        if (useEmptySentinel)
        {
            // s&box ignores IsNone while cascading, so a transparent entry blocks inheritance.
            list.Add(EmptyTextShadow);
        }
        else
        {
            for (int i = 0; i < sourceCount; i++)
            {
                var shadow = shadows[i];
                if (allowInset || !shadow.Inset)
                    list.Add(shadow);
            }
        }
        target.Dirty();
    }

    static float? TrySingle(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.Single ? System.BitConverter.Int32BitsToSingle(v.RefSlot) : (float?)null;

    static bool? TryBool(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.Boolean ? v.RefSlot != 0 : (bool?)null;

    static int? TryInt32(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.Int32 ? v.RefSlot : (int?)null;

    static Texture? TryTexture(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.Texture ? (Texture?)v.RefVal : null;

    static EnginePanelTransform? TryPanelTransform(StyleField f)
    {
        if (!TryResolved(f, out var v)) return null;
        if (v.Kind != StyleValueKind.PanelTransform) return null;
        if (v.RefVal is not ImmutableList<EnginePanelTransform.Entry> list) return null;
        return new EnginePanelTransform { List = list };
    }

    static Wrap? TryWrap(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.Wrap ? ((Wrap)v.EnumVal) : (Wrap?)null;

    static BackgroundRepeat? TryBackgroundRepeat(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.BackgroundRepeat ? ((BackgroundRepeat)v.EnumVal) : (BackgroundRepeat?)null;

    static BorderImageFill? TryBorderImageFill(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.BorderImageFill ? ((BorderImageFill)v.EnumVal) : (BorderImageFill?)null;

    static BorderImageRepeat? TryBorderImageRepeat(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.BorderImageRepeat ? ((BorderImageRepeat)v.EnumVal) : (BorderImageRepeat?)null;

    static FontSmooth? TryFontSmooth(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.FontSmooth ? ((FontSmooth)v.EnumVal) : (FontSmooth?)null;

    static FontStyle? TryFontStyle(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.FontStyle ? ((FontStyle)v.EnumVal) : (FontStyle?)null;

    static FontVariantNumeric? TryFontVariantNumeric(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.FontVariantNumeric ? ((FontVariantNumeric)v.EnumVal) : (FontVariantNumeric?)null;

    static ImageRendering? TryImageRendering(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.ImageRendering ? ((ImageRendering)v.EnumVal) : (ImageRendering?)null;

    static MaskMode? TryMaskMode(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.MaskMode ? ((MaskMode)v.EnumVal) : (MaskMode?)null;

    static MaskScope? TryMaskScope(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.MaskScope ? ((MaskScope)v.EnumVal) : (MaskScope?)null;

    static ObjectFit? TryObjectFit(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.ObjectFit ? ((ObjectFit)v.EnumVal) : (ObjectFit?)null;

    static OverflowMode? TryOverflowMode(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.OverflowMode ? ((OverflowMode)v.EnumVal) : (OverflowMode?)null;

    static PointerEvents? TryPointerEvents(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.PointerEvents ? ((PointerEvents)v.EnumVal) : (PointerEvents?)null;

    static PositionMode? TryPositionMode(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.PositionMode ? ((PositionMode)v.EnumVal) : (PositionMode?)null;

    static TextAlign? TryTextAlign(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.TextAlign ? ((TextAlign)v.EnumVal) : (TextAlign?)null;

    static TextDecoration? TryTextDecoration(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.TextDecoration ? ((TextDecoration)v.EnumVal) : (TextDecoration?)null;

    static TextDecorationStyle? TryTextDecorationStyle(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.TextDecorationStyle ? ((TextDecorationStyle)v.EnumVal) : (TextDecorationStyle?)null;

    static TextSkipInk? TryTextSkipInk(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.TextSkipInk ? ((TextSkipInk)v.EnumVal) : (TextSkipInk?)null;

    static FilterMode? TryFilterMode(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.FilterMode ? ((FilterMode)v.EnumVal) : (FilterMode?)null;

    static TextOverflow? TryTextOverflow(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.TextOverflow ? ((TextOverflow)v.EnumVal) : (TextOverflow?)null;

    static TextTransform? TryTextTransform(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.TextTransform ? ((TextTransform)v.EnumVal) : (TextTransform?)null;

    static WhiteSpace? TryWhiteSpace(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.WhiteSpace ? ((WhiteSpace)v.EnumVal) : (WhiteSpace?)null;

    static WordBreak? TryWordBreak(StyleField f)
        => TryResolved(f, out var v) && v.Kind == StyleValueKind.WordBreak ? ((WordBreak)v.EnumVal) : (WordBreak?)null;

    static Panel WalkPath(Panel root, int[] path)
    {
        var cur = root;
        foreach (var idx in path)
        {
            cur = cur.GetChild(idx, false);
            if (cur is null)
                throw new InvalidOperationException(
                    $"Applier.WalkPath: child index {idx} is null while traversing HostPath=[{string.Join(",", path)}].");
        }
        return cur;
    }
}

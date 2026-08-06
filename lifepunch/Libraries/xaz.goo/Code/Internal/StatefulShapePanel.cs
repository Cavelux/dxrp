using System;
using Sandbox;
using Sandbox.Rendering;
using Sandbox.UI;

namespace Goo.Internal;

internal sealed class StatefulShapePanel : Panel, IPanelDraw, IStatefulEventHost
{
    internal Action<MousePanelEvent>? _onClick;
    internal Action<MousePanelEvent>? _onRightClick;
    internal Action<MousePanelEvent>? _onMiddleClick;
    internal Action<MousePanelEvent>? _onMouseEnter;
    internal Action<MousePanelEvent>? _onMouseLeave;
    bool _hoverInside;   // gates true enter/leave over the engine's bubbling over/out
    internal Action<MousePanelEvent>? _onMouseDown;
    internal Action<MousePanelEvent>? _onMouseUp;
    internal Action<MousePanelEvent>? _onMouseMove;
    internal bool    _userSetPointerEvents;
    internal Action? _requestRebuild;
    public Action? RequestRebuild { set => _requestRebuild = value; }

    // When true, mouse events hit the full bounding box instead of the drawn SDF outline (RectHitTest blob flag).
    public bool RectHitTest;

    // Every shape renders via ui_shape.shader (GPU SDF): no CPU bake, crisp at any size. Polygon rides
    // its vertices on a points-texture uniform (kind 4); Sector/Arc use the parametric uniforms (kind 0).
    ShaderEffect? _shapeEffect;
    Color _shapeColor = Color.White;
    Color? _hoverColor;
    int    _transitionMs;

    // Retained shape identity so the actionable handlers can hit-test against the drawn outline.
    BlobKind _shapeKind = BlobKind.Sector;
    ShapeParams _shapeParams;
    float _shapeInner, _shapeOuter, _shapeCorner; // post-expansion (Arc -> inner/outer), fed to the effect + Contains
    Vector2[]? _polygonPoints;
    Texture? _pointsTex;

    public void ApplyEvents(in BlobEvents events)
    {
        _onClick      = events.OnClick;
        _onRightClick = events.OnRightClick;
        _onMiddleClick = events.OnMiddleClick;
        _onMouseEnter = events.OnMouseEnter;
        _onMouseLeave = events.OnMouseLeave;
        _onMouseDown  = events.OnMouseDown;
        _onMouseUp    = events.OnMouseUp;
        _onMouseMove  = events.OnMouseMove;
    }

    public bool HasEventHandlers =>
        _onClick != null || _onRightClick != null || _onMiddleClick != null || _onMouseEnter != null || _onMouseLeave != null ||
        _onMouseDown != null || _onMouseUp != null || _onMouseMove != null;

    public bool UserSetPointerEvents
    {
        get => _userSetPointerEvents;
        set => _userSetPointerEvents = value;
    }

    // Sector/Arc: drive the SDF shader's uniforms. Color arrives separately via SetShapeColor (it
    // comes from the style's BackgroundColor, not the shape params). DrawQuad passes Color.White, so
    // the colour has to be a uniform rather than a background tint.
    public void ApplyShape(BlobKind kind, in ShapeParams shape)
    {
        float inner, outer, corner;
        if (kind == BlobKind.Arc)
        {
            // Arc params are (StartAngle, EndAngle, centre Radius, Stroke); expand to inner/outer.
            float halfStroke = shape.D * 0.5f;
            inner  = MathF.Max(0f, shape.C - halfStroke);
            outer  = MathF.Min(1f, shape.C + halfStroke);
            corner = 0f;
        }
        else
        {
            inner  = shape.C;
            outer  = shape.D;
            corner = shape.E;
        }

        // If this panel had been a Polygon, drop its baked mask so it does not draw behind the SDF.
        Style.BackgroundImage = null;
        _shapeKind = kind;
        _shapeParams = shape;
        _shapeInner = inner;
        _shapeOuter = outer;
        _shapeCorner = corner;
        _polygonPoints = null;
        RebuildShapeEffect();
        MarkRenderDirty();
    }

    // Base fill color for any shape. Rebuilds the effect so the next draw picks it up.
    public void SetShapeColor(Color color)
    {
        if (_shapeColor == color) return;
        _shapeColor = color;
        if (_shapeEffect is not null) RebuildShapeEffect();
    }

    // Hover recolor knobs (HoverBackgroundColor + TransitionMs from the style). Null hover keeps the
    // plain effect. Rebuilds the effect so the next draw reflects the change.
    public void SetHover(Color? hoverColor, int transitionMs)
    {
        if (_hoverColor == hoverColor && _transitionMs == transitionMs) return;
        _hoverColor = hoverColor;
        _transitionMs = transitionMs;
        RebuildShapeEffect();
    }

    // Builds the per-frame effect from retained geometry + colors: a ShapeEffect when a hover color is
    // set, else a plain ShaderEffect. Called whenever geometry, base color, or hover knobs change.
    void RebuildShapeEffect()
    {
        if (_shapeKind == BlobKind.Polygon)
        {
            if (_polygonPoints is null || _pointsTex is null) { _shapeEffect = null; return; }
            if (_hoverColor is { } hcp)
                _shapeEffect = new ShapeEffect(_polygonPoints, _pointsTex, _shapeColor, hcp, _transitionMs);
            else
                _shapeEffect = new ShaderEffect("shaders/ui_shape.shader")
                {
                    ["ShapeKind"]  = 4f,
                    ["PointsTex"]  = _pointsTex,
                    ["PointCount"] = (float)_polygonPoints.Length,
                    ["ShapeColor"] = _shapeColor,
                };
            _shapeEffect.Warm();
            return;
        }
        float kind = 0f; // Sector and Arc both render as shader kind 0 (Arc pre-expanded to inner/outer)
        if (_hoverColor is { } hc)
            _shapeEffect = new ShapeEffect(kind, _shapeInner, _shapeOuter,
                start: _shapeParams.A, end: _shapeParams.B, corner: _shapeCorner,
                baseColor: _shapeColor, hoverColor: hc, transitionMs: _transitionMs);
        else
            _shapeEffect = new ShaderEffect("shaders/ui_shape.shader")
            {
                ["ShapeStart"]  = _shapeParams.A,
                ["ShapeEnd"]    = _shapeParams.B,
                ["ShapeInner"]  = _shapeInner,
                ["ShapeOuter"]  = _shapeOuter,
                ["ShapeCorner"] = _shapeCorner,
                ["ShapeColor"]  = _shapeColor,
            };
        _shapeEffect.Warm();
    }

    public void ApplyPolygon(Vector2[] points)
    {
        _shapeKind = BlobKind.Polygon;
        _polygonPoints = points;
        _pointsTex = ShapeTextureCache.GetOrBakePointsTexture(points);
        Style.BackgroundImage = null;
        RebuildShapeEffect();
        MarkRenderDirty();
    }

    // Re-record the one SDF quad each frame so animated geometry (changed shape params push new
    // uniforms) and panel moves/resizes stay in sync. One quad per shape per frame is trivial.
    public override void Tick()
    {
        base.Tick();
        if (_shapeEffect is not null)
            MarkRenderDirty();
    }

    void IPanelDraw.Draw(CommandList cl)
    {
        if (_shapeEffect is null) return;
        _shapeEffect.Apply(cl, Box.Rect);
        cl.DrawQuad(Box.Rect, _shapeEffect.Material, Color.White);
    }

    // True if the event position falls inside the drawn SDF outline (not just the bounding rect).
    // LocalPosition and Box.Rect are both engine-rendered px, so the uv ratio is UI-scale-invariant.
    bool HitInsideShape(MousePanelEvent e)
    {
        if (RectHitTest) return true;
        var size = Box.Rect.Size;
        if (size.x <= 0f || size.y <= 0f) return true; // not laid out yet: don't suppress
        var uv = new Vector2(e.LocalPosition.x / size.x, e.LocalPosition.y / size.y);
        if (_shapeKind == BlobKind.Polygon)
            return _polygonPoints is null || ShapeMath.Polygon(_polygonPoints, uv);
        float margin = 1f / MathF.Max(size.y, 1f); // ~1px feather in uv terms
        return ShapeMath.Contains(_shapeKind, in _shapeParams, uv, margin);
    }

    protected override void OnClick(MousePanelEvent e)       { base.OnClick(e);       if (HitInsideShape(e)) EventDispatch.Fire(_onClick, e, _requestRebuild); }
    protected override void OnRightClick(MousePanelEvent e)  { base.OnRightClick(e);  if (HitInsideShape(e)) EventDispatch.Fire(_onRightClick, e, _requestRebuild); }
    protected override void OnMiddleClick(MousePanelEvent e) { base.OnMiddleClick(e); if (HitInsideShape(e)) EventDispatch.Fire(_onMiddleClick, e, _requestRebuild); }
    protected override void OnMouseOver(MousePanelEvent e)
    {
        base.OnMouseOver(e);
        EventDispatch.FireCrossing(ref _hoverInside, HitInsideShape(e), _onMouseEnter, _onMouseLeave, e, _requestRebuild);
    }

    protected override void OnMouseOut(MousePanelEvent e)
    {
        base.OnMouseOut(e);
        EventDispatch.FireCrossing(ref _hoverInside, false, _onMouseEnter, _onMouseLeave, e, _requestRebuild);
    }

    protected override void OnMouseDown(MousePanelEvent e)   { base.OnMouseDown(e);   if (HitInsideShape(e)) EventDispatch.Fire(_onMouseDown, e, _requestRebuild); }
    protected override void OnMouseUp(MousePanelEvent e)     { base.OnMouseUp(e);     if (HitInsideShape(e)) EventDispatch.Fire(_onMouseUp, e, _requestRebuild); }

    protected override void OnMouseMove(MousePanelEvent e)
    {
        base.OnMouseMove(e);
        EventDispatch.Fire(_onMouseMove, e, _requestRebuild);
        EventDispatch.FireCrossing(ref _hoverInside, HitInsideShape(e), _onMouseEnter, _onMouseLeave, e, _requestRebuild);
    }
}

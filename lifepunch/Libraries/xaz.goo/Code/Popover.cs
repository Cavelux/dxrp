using System;
using Sandbox.UI;

namespace Goo;

// Where a Popover's floating content sits relative to its trigger.
public enum PopoverAnchor { Top, Bottom, Left, Right, BelowStart }

public static partial class Controls
{
    public static Container Popover(
        bool open,
        Container trigger,
        Container content,
        PopoverAnchor anchor = PopoverAnchor.Bottom,
        Action onDismiss = null,
        float gap = 4f,
        string key = null )
    {
        var wrapper = new Container
        {
            Key       = key,
            Position  = PositionMode.Relative,
            AlignSelf = Align.FlexStart,
        };
        wrapper.Children.Add( trigger );

        if ( open )
        {
            wrapper.Children.Add( new Container
            {
                Key           = "popover-catch",
                Position      = PositionMode.Absolute,
                Left          = Px.Of( -5000 ),
                Top           = Px.Of( -5000 ),
                Width         = Px.Of( 10000 ),
                Height        = Px.Of( 10000 ),
                ZIndex        = 199,
                PointerEvents = PointerEvents.All,
                OnClick       = onDismiss is null ? null : _ => onDismiss(),
            } );

            wrapper.Children.Add( AnchorBox( anchor, gap, content ) );
        }

        return wrapper;
    }

    static Container AnchorBox( PopoverAnchor anchor, float gap, Container content )
    {
        Length? top = null, bottom = null, left = null, right = null;
        float mt = 0, mb = 0, ml = 0, mr = 0;

        switch ( anchor )
        {
            case PopoverAnchor.Top:        bottom = Length.Percent( 100 ); left = Length.Percent( 50 ); mb = gap; break;
            case PopoverAnchor.Bottom:     top    = Length.Percent( 100 ); left = Length.Percent( 50 ); mt = gap; break;
            case PopoverAnchor.Left:       right  = Length.Percent( 100 ); top  = Length.Percent( 50 ); mr = gap; break;
            case PopoverAnchor.Right:      left   = Length.Percent( 100 ); top  = Length.Percent( 50 ); ml = gap; break;
            case PopoverAnchor.BelowStart: top    = Length.Percent( 100 ); left = Px.Of( 0 );           mt = gap; break;
        }

        return new Container
        {
            Key          = "popover-content",
            Position     = PositionMode.Absolute,
            Top          = top,
            Bottom       = bottom,
            Left         = left,
            Right        = right,
            MarginTop    = Px.Of( mt ),
            MarginBottom = Px.Of( mb ),
            MarginLeft   = Px.Of( ml ),
            MarginRight  = Px.Of( mr ),
            ZIndex       = 200,
            SwallowClick = true,
            Children     = { content },
        };
    }
}

using System.Collections.Generic;
using Editor;
using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

[CustomEditor( typeof( GooLength ), NamedEditor = "goo-side-lengths" )]
public sealed class GooSideLengthsControlWidget : ControlWidget
{
	public GooSideLengthsControlWidget( SerializedProperty property ) : base( property )
	{
		Layout = global::Editor.Layout.Column();
		Layout.Spacing = 2;
		MinimumHeight = Theme.RowHeight * 2 + 2;

		if ( !GooSideControlLayout.TryGetProperties( property, out var properties ) )
			return;

		AddPair( properties, 0, 1 );
		AddPair( properties, 2, 3 );
	}

	void AddPair( SerializedProperty[] properties, int first, int second )
	{
		var row = Layout.AddRow();
		row.Spacing = 2;
		AddSide( row, properties, first );
		AddSide( row, properties, second );
	}

	static void AddSide( global::Editor.Layout row, SerializedProperty[] properties, int index )
	{
		var side = GooSideControlLayout.Sides[index];
		var control = row.Add( new LengthControlWidget( properties[index] )
		{
			HighlightColor = side.Color,
			Icon = side.Icon,
			Label = null,
			ToolTip = side.Label
		} );
		control.MinimumWidth = 60;
		control.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
	}

	protected override void OnPaint() { }
}

[CustomEditor( typeof( Color ), NamedEditor = "goo-side-colors" )]
public sealed class GooSideColorsControlWidget : ControlWidget
{
	public GooSideColorsControlWidget( SerializedProperty property ) : base( property )
	{
		Layout = global::Editor.Layout.Column();
		Layout.Spacing = 2;
		MinimumHeight = Theme.RowHeight * 2 + 2;

		if ( !GooSideControlLayout.TryGetProperties( property, out var properties ) )
			return;

		AddPair( properties, 0, 1 );
		AddPair( properties, 2, 3 );
	}

	void AddPair( SerializedProperty[] properties, int first, int second )
	{
		var row = Layout.AddRow();
		row.Spacing = 2;
		AddSide( row, properties, first );
		AddSide( row, properties, second );
	}

	static void AddSide( global::Editor.Layout row, SerializedProperty[] properties, int index )
	{
		var side = GooSideControlLayout.Sides[index];
		var control = row.Add( new GooSideColorControlWidget( properties[index], side.Icon, side.Color, side.Label ) );
		control.MinimumWidth = Theme.RowHeight * 2;
		control.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
	}

	protected override void OnPaint() { }
}

static class GooSideControlLayout
{
	static readonly Dictionary<string, string[]> Families = new()
	{
		["PaddingLeft"] = ["PaddingLeft", "PaddingTop", "PaddingRight", "PaddingBottom"],
		["MarginLeft"] = ["MarginLeft", "MarginTop", "MarginRight", "MarginBottom"],
		["BorderLeftWidth"] = ["BorderLeftWidth", "BorderTopWidth", "BorderRightWidth", "BorderBottomWidth"],
		["BorderImageWidthLeft"] = ["BorderImageWidthLeft", "BorderImageWidthTop", "BorderImageWidthRight", "BorderImageWidthBottom"],
		["Left"] = ["Left", "Top", "Right", "Bottom"],
		["BorderLeftColor"] = ["BorderLeftColor", "BorderTopColor", "BorderRightColor", "BorderBottomColor"]
	};

	public static readonly (string Icon, Color Color, string Label)[] Sides =
	{
		("border_left", Theme.Red, "Left"),
		("border_top", Theme.Blue, "Top"),
		("border_right", Theme.Green, "Right"),
		("border_bottom", Theme.Yellow, "Bottom")
	};

	public static bool TryGetProperties( SerializedProperty anchor, out SerializedProperty[] properties )
	{
		properties = null!;
		if ( !Families.TryGetValue( anchor.Name, out var names ) )
			return false;

		properties = new SerializedProperty[names.Length];
		for ( var i = 0; i < names.Length; i++ )
		{
			properties[i] = anchor.Parent.GetProperty( names[i] );
			if ( properties[i] is null )
				return false;
		}

		return true;
	}
}

sealed class GooSideColorControlWidget : ControlWidget
{
	readonly string _icon;
	readonly Color _color;

	public GooSideColorControlWidget( SerializedProperty property, string icon, Color color, string label ) : base( property )
	{
		_icon = icon;
		_color = color;
		FixedHeight = Theme.RowHeight;
		ToolTip = $"{label}. Click the icon to unset.";

		Layout = global::Editor.Layout.Row();
		Layout.Margin = new Margin( Theme.RowHeight, 0, 0, 0 );
		Layout.Add( new GooNullableColorSwatchWidget( property ) );
		GooTokenBindingButton.Attach( this );
	}

	protected override void OnPaint()
	{
		var hovered = IsUnderMouse && Enabled;
		Paint.ClearPen();
		Paint.SetBrush( _color.Darken( hovered ? 0.7f : 0.8f ).Desaturate( 0.8f ).WithAlphaMultiplied( IsControlDisabled ? 0.5f : 1f ) );
		Paint.DrawRect( new Rect( 0, 0, Height, Height ).Shrink( 2 ), Theme.ControlRadius - 1f );
		Paint.SetPen( _color.Darken( hovered ? 0f : 0.1f ).Desaturate( hovered ? 0f : 0.2f ).WithAlphaMultiplied( IsControlDisabled ? 0.5f : 1f ) );
		Paint.DrawIcon( new Rect( 0, Height ), _icon, Height - 6, TextFlag.Center );
	}

	protected override void OnMouseReleased( MouseEvent e )
	{
		base.OnMouseReleased( e );
		if ( !e.LeftMouseButton || e.LocalPosition.x >= Height || ReadOnly || SerializedProperty.IsNull )
			return;

		PropertyStartEdit();
		SerializedProperty.SetNullState( true );
		SignalValuesChanged();
		PropertyFinishEdit();
		Update();
	}
}

sealed class GooNullableColorSwatchWidget : ColorSwatchWidget
{
	readonly SerializedProperty _property;

	public GooNullableColorSwatchWidget( SerializedProperty property ) : base( property )
	{
		_property = property;
		HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
	}

	protected override void OnPaint()
	{
		if ( !_property.IsNull )
		{
			base.OnPaint();
			return;
		}

		PaintUnder();
		ColorPalette.PaintSwatch( Color.Transparent, LocalRect.Shrink( 3 ), false, radius: 2, disabled: IsControlDisabled );
		Paint.SetPen( Theme.TextControl.WithAlpha( 0.45f ) );
		Paint.DrawIcon( LocalRect, "remove", 14, TextFlag.Center );
	}
}

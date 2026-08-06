using System.Collections.Generic;
using Editor;
using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

[CustomEditor( typeof( GooLength ), NamedEditor = "goo-axis-lengths" )]
public sealed class GooAxisLengthsControlWidget : ControlWidget
{
	public GooAxisLengthsControlWidget( SerializedProperty property ) : base( property )
	{
		Layout = global::Editor.Layout.Row();
		Layout.Spacing = 2;

		if ( !TryGetProperties( property, out var properties ) )
			return;

		AddAxis( properties[0], Theme.Red, "X" );
		AddAxis( properties[1], Theme.Green, "Y" );
	}

	void AddAxis( SerializedProperty property, Color color, string label )
	{
		var control = Layout.Add( new LengthControlWidget( property )
		{
			HighlightColor = color,
			Label = label,
			ToolTip = property.DisplayName
		} );
		control.MinimumWidth = 60;
		control.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
	}

	static bool TryGetProperties( SerializedProperty anchor, out SerializedProperty[] properties )
	{
		properties = null!;
		var names = anchor.Name switch
		{
			"Width" => new[] { "Width", "Height" },
			"MinWidth" => new[] { "MinWidth", "MinHeight" },
			"MaxWidth" => new[] { "MaxWidth", "MaxHeight" },
			"ColumnGap" => new[] { "ColumnGap", "RowGap" },
			"BackgroundPositionX" => new[] { "BackgroundPositionX", "BackgroundPositionY" },
			"BackgroundSizeX" => new[] { "BackgroundSizeX", "BackgroundSizeY" },
			"MaskPositionX" => new[] { "MaskPositionX", "MaskPositionY" },
			"MaskSizeX" => new[] { "MaskSizeX", "MaskSizeY" },
			"TransformOriginX" => new[] { "TransformOriginX", "TransformOriginY" },
			"PerspectiveOriginX" => new[] { "PerspectiveOriginX", "PerspectiveOriginY" },
			"TransformTranslateX" => new[] { "TransformTranslateX", "TransformTranslateY" },
			_ => null
		};
		if ( names is null )
			return false;

		properties = [anchor, anchor.Parent.GetProperty( names[1] )];
		return properties[0] is not null && properties[1] is not null;
	}

	protected override void OnPaint() { }
}

[CustomEditor( typeof( GooLength ), NamedEditor = "goo-corner-lengths" )]
public sealed class GooCornerLengthsControlWidget : ControlWidget
{
	static readonly (string Name, float Rotation, Color Color, string Label)[] Corners =
	{
		("BorderTopLeftRadius", 270f, Theme.Red, "Top Left"),
		("BorderTopRightRadius", 0f, Theme.Green, "Top Right"),
		("BorderBottomLeftRadius", 180f, Theme.Blue, "Bottom Left"),
		("BorderBottomRightRadius", 90f, Theme.Yellow, "Bottom Right")
	};

	public GooCornerLengthsControlWidget( SerializedProperty property ) : base( property )
	{
		Layout = global::Editor.Layout.Column();
		Layout.Spacing = 2;
		MinimumHeight = Theme.RowHeight * 2 + 2;

		AddPair( property, 0, 1 );
		AddPair( property, 2, 3 );
	}

	void AddPair( SerializedProperty anchor, int first, int second )
	{
		var row = Layout.AddRow();
		row.Spacing = 2;
		AddCorner( row, anchor, first );
		AddCorner( row, anchor, second );
	}

	static void AddCorner( global::Editor.Layout row, SerializedProperty anchor, int index )
	{
		var corner = Corners[index];
		var property = anchor.Parent.GetProperty( corner.Name );
		if ( property is null )
			return;

		var control = row.Add( new LengthControlWidget( property )
		{
			HighlightColor = corner.Color,
			Icon = "rounded_corner",
			IconRotation = corner.Rotation,
			ToolTip = corner.Label
		} );
		control.MinimumWidth = 60;
		control.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
	}

	protected override void OnPaint() { }
}

[CustomEditor( typeof( float ), NamedEditor = "goo-axis-floats" )]
public sealed class GooAxisFloatsControlWidget : ControlWidget
{
	public GooAxisFloatsControlWidget( SerializedProperty property ) : base( property )
	{
		Layout = global::Editor.Layout.Row();
		Layout.Spacing = 2;

		var secondName = property.Name switch
		{
			"TransformScaleX" => "TransformScaleY",
			"TransformSkewX" => "TransformSkewY",
			_ => null
		};
		if ( secondName is null )
			return;

		AddAxis( property, Theme.Red, "X" );
		AddAxis( property.Parent.GetProperty( secondName ), Theme.Green, "Y" );
	}

	void AddAxis( SerializedProperty property, Color color, string label )
	{
		if ( property is null )
			return;

		var control = Layout.Add( new GooFloatControlWidget( property )
		{
			HighlightColor = color,
			Label = label,
			ToolTip = property.DisplayName
		} );
		control.MinimumWidth = 60;
		control.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
	}

	protected override void OnPaint() { }
}

[CustomEditor( typeof( OverflowMode ), NamedEditor = "goo-axis-enums" )]
public sealed class GooAxisEnumsControlWidget : ControlWidget
{
	public GooAxisEnumsControlWidget( SerializedProperty property ) : base( property )
	{
		Layout = global::Editor.Layout.Row();
		Layout.Spacing = 2;

		AddAxis( property.Parent.GetProperty( "OverflowX" ), Theme.Red, "X" );
		AddAxis( property.Parent.GetProperty( "OverflowY" ), Theme.Green, "Y" );
	}

	void AddAxis( SerializedProperty property, Color color, string label )
	{
		if ( property is null )
			return;

		var control = Layout.Add( new GooAxisEnumField( property, color, label ) );
		control.MinimumWidth = 60;
		control.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
	}

	protected override void OnPaint() { }
}

[CustomEditor( typeof( float ), NamedEditor = "goo-layout-transition" )]
public sealed class GooLayoutTransitionControlWidget : ControlWidget
{
	public GooLayoutTransitionControlWidget( SerializedProperty property ) : base( property )
	{
		Layout = global::Editor.Layout.Row();
		Layout.Spacing = 2;

		var duration = Layout.Add( new GooFloatControlWidget( property )
		{
			ToolTip = "Duration in milliseconds. Set to 0 to disable."
		} );
		duration.MinimumWidth = 70;
		duration.MaximumWidth = 100;
		duration.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;

		var easingProperty = property.Parent.GetProperty( "LayoutTransitionEasing" );
		if ( easingProperty is null )
			return;

		var easing = Layout.Add( new GooLayoutEasingControlWidget( easingProperty )
		{
			ToolTip = "Easing used while the container glides to its new layout position."
		} );
		easing.MinimumWidth = 90;
		easing.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
	}

	protected override void OnPaint() { }
}

sealed class GooLayoutEasingControlWidget : DropdownControlWidget<GooLayoutEasing>
{
	public GooLayoutEasingControlWidget( SerializedProperty property ) : base( property ) { }

	protected override IEnumerable<object> GetDropdownValues()
	{
		foreach ( var value in System.Enum.GetValues<GooLayoutEasing>() )
			yield return new Entry { Value = value, Label = DisplayName( value ) };
	}

	protected override string GetDisplayText() =>
		DisplayName( SerializedProperty.GetValue<GooLayoutEasing>() );

	static string DisplayName( GooLayoutEasing easing ) => easing switch
	{
		GooLayoutEasing.EaseIn => "Ease In",
		GooLayoutEasing.EaseOut => "Ease Out",
		GooLayoutEasing.EaseInOut => "Ease In Out",
		GooLayoutEasing.ExpoIn => "Expo In",
		GooLayoutEasing.ExpoOut => "Expo Out",
		GooLayoutEasing.ExpoInOut => "Expo In Out",
		GooLayoutEasing.BounceIn => "Bounce In",
		GooLayoutEasing.BounceOut => "Bounce Out",
		GooLayoutEasing.BounceInOut => "Bounce In Out",
		GooLayoutEasing.SineIn => "Sine In",
		GooLayoutEasing.SineOut => "Sine Out",
		GooLayoutEasing.SineInOut => "Sine In Out",
		GooLayoutEasing.StepStart => "Step Start",
		GooLayoutEasing.StepEnd => "Step End",
		_ => easing.ToString(),
	};
}

sealed class GooAxisEnumField : ControlWidget
{
	readonly Color _color;
	readonly string _label;
	readonly GooNullableEnumControlWidget _dropdown;

	public GooAxisEnumField( SerializedProperty property, Color color, string label ) : base( property )
	{
		_color = color;
		_label = label;
		ToolTip = property.DisplayName;

		Layout = global::Editor.Layout.Row();
		Layout.Margin = new Margin( Theme.RowHeight, 0, 0, 0 );
		_dropdown = Layout.Add( new GooNullableEnumControlWidget( property ) );
		_dropdown.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
	}

	public override void StartEditing() => _dropdown.StartEditing();

	protected override void OnPaint()
	{
		var hovered = IsUnderMouse && Enabled;
		Paint.ClearPen();
		Paint.SetBrush( _color.Darken( hovered ? 0.7f : 0.8f ).Desaturate( 0.8f ).WithAlphaMultiplied( IsControlDisabled ? 0.5f : 1f ) );
		Paint.DrawRect( new Rect( 0, 0, Height, Height ).Shrink( 2 ), Theme.ControlRadius - 1f );
		Paint.SetPen( _color.Darken( hovered ? 0f : 0.1f ).Desaturate( hovered ? 0f : 0.2f ).WithAlphaMultiplied( IsControlDisabled ? 0.5f : 1f ) );
		Paint.SetHeadingFont( 9, 500 );
		Paint.DrawText( new Rect( 1, Height - 1 ), _label, TextFlag.Center );
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		if ( e.LeftMouseButton && e.LocalPosition.x < Height )
			_dropdown.StartEditing();
	}
}

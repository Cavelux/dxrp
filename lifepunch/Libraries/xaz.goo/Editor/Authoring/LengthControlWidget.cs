using System.Globalization;
using Editor;
using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

/// <summary>GooLength editor with numeric scrubbing, units, keywords, and nullable inheritance.</summary>
[CustomEditor( typeof( GooLength ) )]
public class LengthControlWidget : StringControlWidget
{
	public Color HighlightColor { get; set; } = Theme.Green;
	public string? Icon { get; set; }
	public float IconRotation { get; set; }
	public string? Label { get; set; }

	static readonly (string Label, LengthUnit Unit)[] NumericUnits =
	{
		("px", LengthUnit.Pixels), ("%", LengthUnit.Percentage),
		("em", LengthUnit.Em), ("rem", LengthUnit.RootEm),
		("vw", LengthUnit.ViewWidth), ("vh", LengthUnit.ViewHeight),
		("vmin", LengthUnit.ViewMin), ("vmax", LengthUnit.ViewMax),
	};

	static readonly (string Label, LengthUnit Unit)[] KeywordUnits =
	{
		("auto", LengthUnit.Auto), ("start", LengthUnit.Start), ("center", LengthUnit.Center),
		("end", LengthUnit.End), ("cover", LengthUnit.Cover), ("contain", LengthUnit.Contain),
	};

	bool IsCompact => Icon is not null || Label is not null;

	const float UnitWidth = 48;

	bool IsNullable => System.Nullable.GetUnderlyingType( SerializedProperty.PropertyType ) is not null;

	GooLength? Current => SerializedProperty.GetValue<GooLength?>( null );

	float _dragValue;
	LengthUnit _dragUnit;
	bool _dragging;
	bool _dragPressed;
	bool _hasRange;
	bool _rangeClamped;
	float _rangeMinimum;
	float _rangeMaximum;
	float _rangeStep;

	public LengthControlWidget( SerializedProperty property ) : base( property )
	{
		MinimumWidth = 60;
		ReadRangeMetadata();
		if ( string.IsNullOrEmpty( LineEdit.PlaceholderText ) )
			LineEdit.PlaceholderText = IsNullable ? "unset" : "auto";
		GooTokenBindingButton.Attach( this, UnitWidth + 2f );
	}

	void ReadRangeMetadata()
	{
		foreach ( var attribute in SerializedProperty.GetAttributes() )
		{
			if ( attribute is StepAttribute step )
				_rangeStep = step.Step;
			else if ( attribute is RangeAttribute range )
			{
				_hasRange = true;
				_rangeMinimum = range.Min;
				_rangeMaximum = range.Max;
				_rangeClamped = range.Clamped;
			}
		}
	}

	static bool IsNumeric( LengthUnit unit ) =>
		unit is LengthUnit.Pixels or LengthUnit.Percentage or LengthUnit.Em or LengthUnit.RootEm
			or LengthUnit.ViewWidth or LengthUnit.ViewHeight or LengthUnit.ViewMin or LengthUnit.ViewMax;

	static string UnitLabel( LengthUnit unit )
	{
		foreach ( var (label, u) in NumericUnits ) if ( u == unit ) return label;
		foreach ( var (label, u) in KeywordUnits ) if ( u == unit ) return label;
		return unit.ToString().ToLowerInvariant();
	}

	protected override void DoLayout()
	{
		base.DoLayout();
		Cursor = IsCompact ? CursorShape.SizeH : CursorShape.Arrow;
		var iconWidth = Icon is not null || Label is not null ? Theme.RowHeight : 0;
		LineEdit.Position = new Vector2( iconWidth, 0 );
		LineEdit.Size = new Vector2( Width - iconWidth - UnitWidth, Height );
	}

	protected override void PaintControl()
	{
		var h = Height;
		if ( h <= 6f || Width <= 0f ) return;

		var hovered = IsUnderMouse && Enabled;

		if ( IsCompact )
		{
			Paint.ClearPen();
			Paint.SetBrush( HighlightColor.Darken( hovered ? 0.7f : 0.8f ).Desaturate( 0.8f ).WithAlphaMultiplied( IsControlDisabled ? 0.5f : 1f ) );
			Paint.DrawRect( new Rect( 0, 0, h, h ).Shrink( 2 ), Theme.ControlRadius - 1f );

			Paint.SetPen( HighlightColor.Darken( hovered ? 0f : 0.1f ).Desaturate( hovered ? 0f : 0.2f ).WithAlphaMultiplied( IsControlDisabled ? 0.5f : 1f ) );
			if ( string.IsNullOrEmpty( Label ) )
			{
				var center = new Vector2( h * 0.5f, h * 0.5f );
				Paint.Rotate( IconRotation, center );
				Paint.DrawIcon( new Rect( 0, h ), Icon, h - 6, TextFlag.Center );
				Paint.Rotate( -IconRotation, center );
			}
			else
			{
				Paint.SetHeadingFont( 9, 500 );
				Paint.DrawText( new Rect( 1, h - 1 ), Label, TextFlag.Center );
			}
		}

		var rect = new Rect( Width - UnitWidth, 0, UnitWidth, Height ).Shrink( 2 );
		var unitHovered = hovered && global::Editor.Application.CursorPosition.x - ScreenPosition.x > Width - UnitWidth;

		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground.Lighten( unitHovered ? 0.6f : 0.3f ) );
		Paint.DrawRect( rect, Theme.ControlRadius - 1.0f );

		var label = Current is { } value ? UnitLabel( value.Unit ) : (IsNullable ? "–" : "auto");

		Paint.SetPen( unitHovered ? Theme.Blue : Theme.TextControl );
		Paint.SetDefaultFont();
		Paint.DrawText( rect.Shrink( 4, 0 ), label, TextFlag.Center );
	}

	protected override void OnMousePress( MouseEvent e )
	{
		if ( e.LeftMouseButton && e.LocalPosition.x > Width - UnitWidth && !ReadOnly )
		{
			OpenUnitMenu();
			e.Accepted = true;
			return;
		}

		if ( !e.LeftMouseButton || ReadOnly || !SerializedProperty.IsEditable || !IsCompact || e.LocalPosition.x >= Height )
		{
			base.OnMousePress( e );
			return;
		}

		LineEdit.Blur();
		var current = Current;
		_dragUnit = current is { } value && IsNumeric( value.Unit ) ? value.Unit : LengthUnit.Pixels;
		_dragValue = current is { } numeric && IsNumeric( numeric.Unit ) ? numeric.Value : 0f;
		_dragPressed = true;
		PropertyStartEdit();
		e.Accepted = true;
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		if ( !_dragPressed )
		{
			base.OnMouseMove( e );
			Cursor = IsCompact && e.LocalPosition.x < Height ? CursorShape.SizeH : CursorShape.Arrow;
			return;
		}

		if ( !e.ButtonState.Contains( MouseButtons.Left ) )
			return;

		_dragging = true;
		var step = ScrubStep( _dragUnit );
		var delta = global::Editor.Application.CursorDelta.x * step;
		if ( e.ButtonState.Contains( MouseButtons.Right ) )
			delta *= 0.1f;

		_dragValue = ApplyRange( _dragValue + delta );
		var value = _dragValue.SnapToGrid( step );
		SerializedProperty.SetValue( new GooLength { Value = value, Unit = _dragUnit } );
		LineEdit.Text = ValueToString();
		SignalValuesChanged();

		global::Editor.Application.CursorPosition = ScreenPosition + Theme.RowHeight * 0.5f;
		Cursor = CursorShape.Blank;
		e.Accepted = true;
	}

	protected override void OnMouseReleased( MouseEvent e )
	{
		if ( !e.LeftMouseButton || !_dragPressed )
		{
			base.OnMouseReleased( e );
			return;
		}

		if ( _dragging )
			LineEdit.Focus();

		_dragPressed = false;
		_dragging = false;
		Cursor = IsCompact ? CursorShape.SizeH : CursorShape.Arrow;
		PropertyFinishEdit();
		e.Accepted = true;
	}

	float ScrubStep( LengthUnit unit ) => _rangeStep > 0f ? _rangeStep : unit switch
	{
		LengthUnit.Em or LengthUnit.RootEm => 0.1f,
		_ => 1f
	};

	float ApplyRange( float value ) => _hasRange && _rangeClamped
		? value.Clamp( _rangeMinimum, _rangeMaximum )
		: value;

	void OpenUnitMenu()
	{
		var menu = new ContextMenu( this );

		foreach ( var (label, unit) in NumericUnits )
			menu.AddOption( label, action: () => SetUnit( unit ) );

		menu.AddSeparator();

		foreach ( var (label, unit) in KeywordUnits )
			menu.AddOption( label, action: () => SetKeyword( unit ) );

		if ( IsNullable )
		{
			menu.AddSeparator();
			menu.AddOption( "unset", action: () => SetTo( null ) );
		}

		menu.OpenAtCursor( false );
	}

	void SetUnit( LengthUnit unit )
	{
		// Preserve the number between numeric units and start at zero from keywords or unset values.
		var number = Current is { } v && IsNumeric( v.Unit ) ? ApplyRange( v.Value ) : ApplyRange( 0f );
		SetTo( new GooLength { Value = number, Unit = unit } );
	}

	void SetKeyword( LengthUnit unit ) => SetTo( new GooLength { Unit = unit } );

	void SetTo( GooLength? value )
	{
		PropertyStartEdit();
		SerializedProperty.SetValue( value );
		LineEdit.Text = ValueToString();
		SignalValuesChanged();
		PropertyFinishEdit();
	}

	protected override string ValueToString()
	{
		var value = Current;
		if ( value is null || !IsNumeric( value.Value.Unit ) )
			return "";

		return value.Value.Value.ToString( "0.###", CultureInfo.InvariantCulture );
	}

	protected override object StringToValue( string text )
	{
		if ( string.IsNullOrWhiteSpace( text ) )
			return IsNullable ? null! : (object)default( GooLength );

		// Preserve the current value while an incomplete numeric string is being typed.
		if ( !float.TryParse( text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number ) )
			return SerializedProperty.GetValue<object>( null! );

		var unit = Current is { } v && IsNumeric( v.Unit ) ? v.Unit : LengthUnit.Pixels;
		return new GooLength { Value = ApplyRange( number ), Unit = unit };
	}
}

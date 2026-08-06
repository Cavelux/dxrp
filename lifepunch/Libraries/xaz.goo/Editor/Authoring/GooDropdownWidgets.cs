using System.Collections.Generic;
using Editor;
using Sandbox;

namespace Goo.Authoring;

/// <summary>Nullable enum dropdown with an "(inherit)" entry; flags enums keep the stock widget.</summary>
[CustomEditor( typeof( System.Enum ), NamedEditor = "goo-nullable-enum" )]
public class GooNullableEnumControlWidget : DropdownControlWidget<object>
{
	public GooNullableEnumControlWidget( SerializedProperty property ) : base( property ) { }

	protected override IEnumerable<object> GetDropdownValues()
	{
		yield return new Entry { Value = null!, Label = "(inherit)" };

		var enumType = System.Nullable.GetUnderlyingType( SerializedProperty.PropertyType ) ?? SerializedProperty.PropertyType;
		foreach ( var value in System.Enum.GetValues( enumType ) )
			yield return value;
	}

	protected override string GetDisplayText() =>
		SerializedProperty.GetValue<object>()?.ToString() ?? "(inherit)";
}

/// <summary>Dropdown of the cursor names the engine actually resolves (InputRouter.CursorLookup).</summary>
[CustomEditor( typeof( string ), NamedEditor = "goo-cursor" )]
public class GooCursorControlWidget : DropdownControlWidget<string>
{
	static readonly string[] Cursors =
	{
		"none", "arrow", "pointer", "text", "crosshair", "move", "progress", "wait",
		"not-allowed", "ew-resize", "ns-resize", "nesw-resize", "nwse-resize",
	};

	public GooCursorControlWidget( SerializedProperty property ) : base( property )
	{
		GooTokenBindingButton.Attach( this, 20f );
	}

	protected override IEnumerable<object> GetDropdownValues()
	{
		yield return new Entry { Value = null!, Label = "(inherit)" };
		foreach ( var cursor in Cursors )
			yield return cursor;
	}

	protected override string GetDisplayText() =>
		SerializedProperty.GetValue<string>() is { Length: > 0 } s ? s : "(inherit)";
}

/// <summary>Dropdown of the blend modes the panel renderer supports (everything else falls back to normal).</summary>
[CustomEditor( typeof( string ), NamedEditor = "goo-blend-mode" )]
public class GooBlendModeControlWidget : DropdownControlWidget<string>
{
	public GooBlendModeControlWidget( SerializedProperty property ) : base( property )
	{
		GooTokenBindingButton.Attach( this, 20f );
	}

	protected override IEnumerable<object> GetDropdownValues()
	{
		yield return new Entry { Value = null!, Label = "(inherit)" };
		yield return "normal";
		yield return "lighten";
		yield return "multiply";
	}

	protected override string GetDisplayText() =>
		SerializedProperty.GetValue<string>() is { Length: > 0 } s ? s : "(inherit)";
}

/// <summary>Dropdown of the named CSS font weights.</summary>
[CustomEditor( typeof( int ), NamedEditor = "goo-font-weight" )]
public class GooFontWeightControlWidget : DropdownControlWidget<object>
{
	static readonly (int Weight, string Name)[] Weights =
	{
		(100, "Thin"), (200, "Extra Light"), (300, "Light"), (400, "Regular"), (500, "Medium"),
		(600, "Semi Bold"), (700, "Bold"), (800, "Extra Bold"), (900, "Black"),
	};

	public GooFontWeightControlWidget( SerializedProperty property ) : base( property )
	{
		GooTokenBindingButton.Attach( this, 20f );
	}

	protected override IEnumerable<object> GetDropdownValues()
	{
		yield return new Entry { Value = null!, Label = "(inherit)" };
		foreach ( var (weight, name) in Weights )
			yield return new Entry { Value = weight, Label = $"{weight} {name}" };
	}

	protected override string GetDisplayText()
	{
		var value = SerializedProperty.GetValue<int?>();
		if ( value is null ) return "(inherit)";
		foreach ( var (weight, name) in Weights )
			if ( weight == value ) return $"{value} {name}";
		return value.ToString() ?? "(inherit)";
	}
}

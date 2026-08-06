namespace Grains.RazorDesigner.Document;

public enum ControlType
{
	// Layout
	Panel,
	SplitContainer,

	// Display
	Label,
	Image,
	IconPanel,

	// Input
	Button,
	ButtonGroup,
	Checkbox,
	TextEntry,
	DropDown,

	// Form
	Form,
	Field,
	FieldControl,
}

public enum ControlCategory
{
	Layout,
	Display,
	Input,
	Form,
}

public sealed record ControlDefaults(
	Length DefaultWidth,
	Length DefaultHeight,
	string DefaultContent,
	string DefaultIcon,
	string ExtraStyle,
	float DefaultFlexGrow,
	FlexDirection DefaultDirection,
	FlexWrap DefaultWrap,
	ControlCategory Category )
{
	public static ControlDefaults For( ControlType type ) => type switch
	{
		// Layout
		ControlType.Panel          => new( Length.Auto,         Length.Auto,         "",          "",     "", 0f, FlexDirection.Row,    FlexWrap.NoWrap, ControlCategory.Layout ),
		ControlType.SplitContainer => new( Length.Percent(100), Length.Percent(100), "",          "",     "", 0f, FlexDirection.Row,    FlexWrap.NoWrap, ControlCategory.Layout ),

		// Display
		ControlType.Label          => new( Length.Auto,         Length.Auto,         "Label",     "",     "", 0f, FlexDirection.Row,    FlexWrap.NoWrap, ControlCategory.Display ),
		ControlType.Image          => new( Length.Auto,         Length.Auto,         "",          "",     "", 0f, FlexDirection.Row,    FlexWrap.NoWrap, ControlCategory.Display ),
		// IconPanel glyph lives in DefaultIcon (engine icon picker via [IconName]); DefaultContent stays empty.
		ControlType.IconPanel      => new( Length.Auto,         Length.Auto,         "",          "help", "", 0f, FlexDirection.Row,    FlexWrap.NoWrap, ControlCategory.Display ),

		// Input
		ControlType.Button         => new( Length.Auto,         Length.Auto,         "Button",    "",     "", 0f, FlexDirection.Row,    FlexWrap.NoWrap, ControlCategory.Input ),
		ControlType.ButtonGroup    => new( Length.Auto,         Length.Px(32),       "",          "",     "", 0f, FlexDirection.Row,    FlexWrap.NoWrap, ControlCategory.Input ),
		ControlType.Checkbox       => new( Length.Auto,         Length.Auto,         "Checkbox",  "",     "", 0f, FlexDirection.Row,    FlexWrap.NoWrap, ControlCategory.Input ),
		ControlType.TextEntry      => new( Length.Px(200),      Length.Px(30),       "Enter text","",     "", 0f, FlexDirection.Row,    FlexWrap.NoWrap, ControlCategory.Input ),
		ControlType.DropDown       => new( Length.Auto,         Length.Auto,         "",          "",     "", 0f, FlexDirection.Row,    FlexWrap.NoWrap, ControlCategory.Input ),

		// Form
		ControlType.Form           => new( Length.Auto,         Length.Auto,         "",          "",     "", 0f, FlexDirection.Column, FlexWrap.NoWrap, ControlCategory.Form ),
		ControlType.Field          => new( Length.Auto,         Length.Auto,         "",          "",     "", 0f, FlexDirection.Row,    FlexWrap.NoWrap, ControlCategory.Form ),
		ControlType.FieldControl   => new( Length.Auto,         Length.Auto,         "",          "",     "", 1f, FlexDirection.Row,    FlexWrap.NoWrap, ControlCategory.Form ),

		_ => throw new System.ArgumentOutOfRangeException( nameof(type), type, null ),
	};

	public static string ClassNamePrefix( ControlType type ) =>
		type.ToString().ToLowerInvariant();

	public static string CategoryDisplayName( ControlCategory cat ) => cat switch
	{
		ControlCategory.Layout   => "Layout",
		ControlCategory.Display  => "Display",
		ControlCategory.Input    => "Input",
		ControlCategory.Form     => "Form",
		_ => cat.ToString(),
	};
}

public enum FlexDirection
{
	Row,
	Column,
}

public enum JustifyContent
{
	Start,
	Center,
	End,
	SpaceBetween,
	SpaceAround,
}

public enum AlignItems
{
	Start,
	Center,
	End,
	Stretch,
}

public enum AlignSelfKind
{
	Auto,
	Start,
	Center,
	End,
	Stretch,
	Baseline,
}

public enum PositionKind
{
	Relative,
	Absolute,
}

public enum FlexWrap
{
	NoWrap,
	Wrap,
	WrapReverse,
}

public enum CursorKind
{
	Auto,
	Default,
	Pointer,
	Text,
	Grab,
	Grabbing,
	Wait,
	Crosshair,
	Move,
	NotAllowed,
	None,
}

public enum OverflowKind
{
	Visible,
	Hidden,
	Scroll,
	Clip,
	ClipWhole,
}

public enum TextAlignment
{
	Left,
	Center,
	Right,
}

// CSS text-transform. None = leave text as authored (engine default). grd-7t2z.
public enum TextTransformKind
{
	None,
	Uppercase,
	Lowercase,
	Capitalize,
}

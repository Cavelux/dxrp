using System;
using Grains.RazorDesigner.Document;
using Sandbox;

namespace Grains.RazorDesigner.Inspector;

public sealed class StateRuleAppearanceProxy
{
	private readonly ControlRecord _record;
	private readonly Func<int> _ruleIndex;  // re-resolved each access; rules list can shift.

	public event Action Changed;

	public StateRuleAppearanceProxy( ControlRecord record, Func<int> ruleIndex )
	{
		_record = record ?? throw new ArgumentNullException( nameof(record) );
		_ruleIndex = ruleIndex ?? throw new ArgumentNullException( nameof(ruleIndex) );
	}

	private Appearance Current => _record.StateRules[_ruleIndex()].Delta;

	private void Mutate( Func<Appearance, Appearance> f )
	{
		var i = _ruleIndex();
		var rule = _record.StateRules[i];
		_record.StateRules[i] = rule with { Delta = f( rule.Delta ) };
		Changed?.Invoke();
	}


	[Group( "Typography" )] [Title( "Override" )]
	[Description( "Emit per-control typography rules. Off = inherit from theme." )]
	public bool OverrideTypography { get => Current.OverrideTypography; set => Mutate( a => a with { OverrideTypography = value } ); }

	[Group( "Typography" )] [Title( "Font" )]
	[Description( "Font family (e.g. Poppins, Roboto Mono). Empty = inherit family from theme." )]
	public string FontFamily { get => Current.FontFamily; set => Mutate( a => a with { FontFamily = value } ); }

	[Group( "Typography" )] [Title( "Size" )]
	[Description( "Font size." )]
	public Length FontSize { get => Current.FontSize; set => Mutate( a => a with { FontSize = value } ); }

	[Group( "Typography" )] [Title( "Weight" )]
	[Description( "CSS font-weight: 100-900 (common: 400 normal, 600 semibold, 700 bold)." )]
	public int FontWeight { get => Current.FontWeight; set => Mutate( a => a with { FontWeight = value } ); }

	[Group( "Typography" )] [Title( "Color" )]
	[Description( "Color of the control's content text. On TextEntry this is the entered text only — placeholder hint stays muted via the theme's .textentry .placeholder rule." )]
	public Color Color { get => Current.Color; set => Mutate( a => a with { Color = value } ); }

	[Group( "Typography" )] [Title( "Align" )]
	[Description( "Horizontal text alignment within the label." )]
	public TextAlignment TextAlign { get => Current.TextAlign; set => Mutate( a => a with { TextAlign = value } ); }

	[Group( "Typography" )] [Title( "Italic" )]
	[Description( "Render the text in italic (font-style: italic)." )]
	public bool FontStyleItalic { get => Current.FontStyleItalic; set => Mutate( a => a with { FontStyleItalic = value } ); }

	[Group( "Typography" )] [Title( "Transform" )]
	[Description( "CSS text-transform — uppercase, lowercase, capitalize, or none." )]
	public TextTransformKind TextTransform { get => Current.TextTransform; set => Mutate( a => a with { TextTransform = value } ); }

	[Group( "Typography" )] [Title( "Letter Spacing" )]
	[Description( "Extra space between characters. Auto = inherit." )]
	public Length LetterSpacing { get => Current.LetterSpacing; set => Mutate( a => a with { LetterSpacing = value } ); }

	[Group( "Typography" )] [Title( "Line Height" )]
	[Description( "Line box height. Auto = inherit. Use em or % — the engine treats a bare number as px." )]
	public Length LineHeight { get => Current.LineHeight; set => Mutate( a => a with { LineHeight = value } ); }


	[Group( "Background" )] [Title( "Override" )]
	[Description( "Emit per-control background rules. Off = inherit from theme." )]
	public bool OverrideBackground { get => Current.OverrideBackground; set => Mutate( a => a with { OverrideBackground = value } ); }

	[Group( "Background" )] [Title( "Color" )]
	[Description( "Background color." )]
	public Color BackgroundColor { get => Current.BackgroundColor; set => Mutate( a => a with { BackgroundColor = value } ); }

	[Group( "Background" )] [Title( "Image" )]
	[Description( "Background image: url('asset.png'), linear-gradient(to bottom, #fff, #000), or empty. Use the button to pick an image asset." )]
	[BackgroundImagePicker]
	public string BackgroundImage { get => Current.BackgroundImage; set => Mutate( a => a with { BackgroundImage = value } ); }

	[Group( "Background" )] [Title( "Size" )]
	[Description( "CSS background-size: 'cover', 'contain', or 1–2 lengths (e.g. '100% 100%', '200px 100px'). Empty = the texture's native pixel size (which visibly rescales as you zoom the canvas)." )]
	public string BackgroundSize { get => Current.BackgroundSize; set => Mutate( a => a with { BackgroundSize = value } ); }

	[Group( "Background" )] [Title( "Position" )]
	[Description( "CSS background-position: X then optional Y. 'center' = 50%. e.g. 'center', '50% 100%', '10px 20px'. Empty = top-left." )]
	public string BackgroundPosition { get => Current.BackgroundPosition; set => Mutate( a => a with { BackgroundPosition = value } ); }

	[Group( "Background" )] [Title( "Repeat" )]
	[Description( "CSS background-repeat: 'no-repeat', 'repeat', 'repeat-x', 'repeat-y'. Empty = 'repeat' (a bare url() tiles by default — usually you want 'no-repeat')." )]
	public string BackgroundRepeat { get => Current.BackgroundRepeat; set => Mutate( a => a with { BackgroundRepeat = value } ); }


	[Group( "Border" )] [Title( "Override" )]
	[Description( "Emit per-control border rules. Off = inherit from theme." )]
	public bool OverrideBorder { get => Current.OverrideBorder; set => Mutate( a => a with { OverrideBorder = value } ); }

	[Group( "Border" )] [Title( "Radius" )]
	[Description( "Corner radius (applies to all four corners)." )]
	public Length BorderRadius { get => Current.BorderRadius; set => Mutate( a => a with { BorderRadius = value } ); }

	[Group( "Border" )] [Title( "Color" )]
	[Description( "Border color." )]
	public Color BorderColor { get => Current.BorderColor; set => Mutate( a => a with { BorderColor = value } ); }

	[Group( "Border" )] [Title( "Width" )]
	[Description( "Border thickness." )]
	public Length BorderWidth { get => Current.BorderWidth; set => Mutate( a => a with { BorderWidth = value } ); }


	[Group( "Effects" )] [Title( "Override" )]
	[Description( "Emit per-control box-shadow. Off = inherit from theme." )]
	public bool OverrideEffects { get => Current.OverrideEffects; set => Mutate( a => a with { OverrideEffects = value } ); }

	[Group( "Effects" )] [Title( "Shadow X" )]
	[Description( "Horizontal offset of the box-shadow." )]
	public Length BoxShadowX { get => Current.BoxShadowX; set => Mutate( a => a with { BoxShadowX = value } ); }

	[Group( "Effects" )] [Title( "Shadow Y" )]
	[Description( "Vertical offset of the box-shadow." )]
	public Length BoxShadowY { get => Current.BoxShadowY; set => Mutate( a => a with { BoxShadowY = value } ); }

	[Group( "Effects" )] [Title( "Shadow Blur" )]
	[Description( "Blur radius of the box-shadow." )]
	public Length BoxShadowBlur { get => Current.BoxShadowBlur; set => Mutate( a => a with { BoxShadowBlur = value } ); }

	[Group( "Effects" )] [Title( "Shadow Color" )]
	[Description( "Box-shadow color." )]
	public Color BoxShadowColor { get => Current.BoxShadowColor; set => Mutate( a => a with { BoxShadowColor = value } ); }

	[Group( "Effects" )] [Title( "Shadow Inset" )]
	[Description( "Render the shadow inside the element instead of outside." )]
	public bool BoxShadowInset { get => Current.BoxShadowInset; set => Mutate( a => a with { BoxShadowInset = value } ); }

	[Group( "Effects" )] [Title( "Opacity" )]
	[Description( "0 = transparent, 1 = fully opaque." )] [Range( 0, 1 )] [Step( 0.01f )]
	public float Opacity { get => Current.Opacity; set => Mutate( a => a with { Opacity = value } ); }


	[Group( "Constraints" )] [Title( "Override" )]
	[Description( "Emit per-control margin / size constraint rules." )]
	public bool OverrideConstraints { get => Current.OverrideConstraints; set => Mutate( a => a with { OverrideConstraints = value } ); }

	[Group( "Constraints" )] [Title( "Margin" )]
	[Description( "Space between this control's edge and its siblings (per-side: top, right, bottom, left)." )]
	public Edges Margin { get => Current.Margin; set => Mutate( a => a with { Margin = value } ); }

	[Group( "Constraints" )] [Title( "Min Width" )]
	[Description( "Floor on this control's width." )]
	public Length MinWidth { get => Current.MinWidth; set => Mutate( a => a with { MinWidth = value } ); }

	[Group( "Constraints" )] [Title( "Max Width" )]
	[Description( "Ceiling on this control's width." )]
	public Length MaxWidth { get => Current.MaxWidth; set => Mutate( a => a with { MaxWidth = value } ); }

	[Group( "Constraints" )] [Title( "Min Height" )]
	[Description( "Floor on this control's height." )]
	public Length MinHeight { get => Current.MinHeight; set => Mutate( a => a with { MinHeight = value } ); }

	[Group( "Constraints" )] [Title( "Max Height" )]
	[Description( "Ceiling on this control's height." )]
	public Length MaxHeight { get => Current.MaxHeight; set => Mutate( a => a with { MaxHeight = value } ); }


	[Group( "Interaction" )] [Title( "Override" )]
	[Description( "Emit per-control cursor / overflow rules." )]
	public bool OverrideInteraction { get => Current.OverrideInteraction; set => Mutate( a => a with { OverrideInteraction = value } ); }

	[Group( "Interaction" )] [Title( "Cursor" )]
	[Description( "Mouse cursor when hovering this control." )]
	public CursorKind Cursor { get => Current.Cursor; set => Mutate( a => a with { Cursor = value } ); }

	[Group( "Interaction" )] [Title( "Overflow" )]
	[Description( "How to handle children that overflow this control's box." )]
	public OverflowKind Overflow { get => Current.Overflow; set => Mutate( a => a with { Overflow = value } ); }

	[Group( "Interaction" )] [Title( "Z-Index" )]
	[Description( "Stacking order among siblings. 0 = default (no explicit stacking)." )]
	public int ZIndex { get => Current.ZIndex; set => Mutate( a => a with { ZIndex = value } ); }

	[Group( "Interaction" )] [Title( "Pointer Events" )]
	[Description( "When off, this control is transparent to the mouse (pointer-events: none)." )]
	public bool PointerEvents { get => Current.PointerEvents; set => Mutate( a => a with { PointerEvents = value } ); }
}

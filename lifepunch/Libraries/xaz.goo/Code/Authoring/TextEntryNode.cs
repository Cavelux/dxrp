using System;
using Goo;
using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

/// <summary>A Goo TextEntry authored in the editor.</summary>
[Title( "Goo Text Entry" )]
[Category( "Goo" )]
[Icon( "edit" )]
public sealed partial class TextEntryNode : BlobNode
{
	/// <summary>Use a controlled value instead of one-time initial text.</summary>
	[Property, Group( "Content" ), Order( 5 )] public bool Controlled { get; set; }

	/// <summary>Controlled text value. Updated before OnChange and OnSubmit run.</summary>
	[Property, Group( "Content" ), Order( 5 ), ShowIf( nameof( Controlled ), true )] public string Value { get; set; }

	/// <summary>Initial uncontrolled text. This is the normal hierarchy-authored mode.</summary>
	[Property, Group( "Content" ), Order( 5 ), ShowIf( nameof( Controlled ), false ), Title( "Initial Text" )] public string DefaultText { get; set; }

	/// <summary>Hint text shown while the field is empty.</summary>
	[Property, Group( "Content" ), Order( 5 )] public string Placeholder { get; set; }

	/// <summary>Maximum number of characters, or null for no limit.</summary>
	[Property, Group( "Validation", StartFolded = true ), Order( 6 ), Editor( "goo-integer" ), Range( 0, int.MaxValue, true, false ), Step( 1 )] public int? MaxLength { get; set; }

	/// <summary>Minimum number of characters, or null for no minimum.</summary>
	[Property, Group( "Validation", StartFolded = true ), Order( 6 ), Editor( "goo-integer" ), Range( 0, int.MaxValue, true, false ), Step( 1 )] public int? MinLength { get; set; }

	/// <summary>Regex that each entered character must match.</summary>
	[Property, Group( "Validation", StartFolded = true ), Order( 6 )] public string CharacterRegex { get; set; }

	/// <summary>Regex that the complete text must match.</summary>
	[Property, Group( "Validation", StartFolded = true ), Order( 6 )] public string StringRegex { get; set; }

	/// <summary>When true, the field is read-only.</summary>
	[Property, Group( "Behavior" ), Order( 7 )] public bool Disabled { get; set; }

	/// <summary>When true, the field accepts multiple lines.</summary>
	[Property, Group( "Behavior" ), Order( 7 )] public bool Multiline { get; set; }

	/// <summary>When true, the field accepts numeric input.</summary>
	[Property, Group( "Numeric", StartFolded = true ), Order( 8 )] public bool Numeric { get; set; }

	/// <summary>Smallest accepted numeric value.</summary>
	[Property, Group( "Numeric", StartFolded = true ), Order( 8 ), Editor( "goo-float" ), Step( 0.1f ), ShowIf( nameof( Numeric ), true )] public float? MinValue { get; set; }

	/// <summary>Largest accepted numeric value.</summary>
	[Property, Group( "Numeric", StartFolded = true ), Order( 8 ), Editor( "goo-float" ), Step( 0.1f ), ShowIf( nameof( Numeric ), true )] public float? MaxValue { get; set; }

	/// <summary>Format string applied in numeric mode.</summary>
	[Property, Group( "Numeric", StartFolded = true ), Order( 8 ), ShowIf( nameof( Numeric ), true )] public string NumberFormat { get; set; }

	/// <summary>Runs whenever the text changes.</summary>
	[Property, SingleAction, Group( "Events", StartFolded = true ), Order( 130 )] public GooTextValueAction OnChange { get; set; }

	/// <summary>Runs when the text is submitted.</summary>
	[Property, SingleAction, Group( "Events", StartFolded = true ), Order( 130 )] public GooTextValueAction OnSubmit { get; set; }

	/// <summary>Runs when the field gains focus.</summary>
	[Property, SingleAction, Group( "Events", StartFolded = true ), Order( 130 )] public GooTextAction OnFocus { get; set; }

	/// <summary>Runs when the field loses focus.</summary>
	[Property, SingleAction, Group( "Events", StartFolded = true ), Order( 130 )] public GooTextAction OnBlur { get; set; }

	/// <summary>Runs when editing is cancelled.</summary>
	[Property, SingleAction, Group( "Events", StartFolded = true ), Order( 130 )] public GooTextAction OnCancel { get; set; }

	/// <summary>Runs when the complete value changes between valid and invalid.</summary>
	[Property, SingleAction, Group( "Events", StartFolded = true ), Order( 130 )] public GooTextValidationAction OnValidationChanged { get; set; }

	internal override void AddTo( Children children ) =>
		children.Add( WithStyles( new Goo.TextEntry
		{
			Key = BlobKey,
			Value = Controlled ? Value ?? "" : null,
			DefaultText = !Controlled && !string.IsNullOrEmpty( DefaultText ) ? DefaultText : null,
			Placeholder = string.IsNullOrEmpty( Placeholder ) ? null : Placeholder,
			MaxLength = MaxLength,
			MinLength = MinLength,
			CharacterRegex = string.IsNullOrEmpty( CharacterRegex ) ? null : CharacterRegex,
			StringRegex = string.IsNullOrEmpty( StringRegex ) ? null : StringRegex,
			Disabled = Disabled,
			Multiline = Multiline,
			Numeric = Numeric,
			MinValue = MinValue,
			MaxValue = MaxValue,
			NumberFormat = string.IsNullOrEmpty( NumberFormat ) ? null : NumberFormat,
			OnChange = !Controlled && OnChange is null ? null : value =>
			{
				if ( Controlled ) Value = value;
				OnChange?.Invoke( this, value );
			},
			OnSubmit = OnSubmit is null ? null : value =>
			{
				if ( Controlled ) Value = value;
				OnSubmit( this, value );
			},
			OnFocus = OnFocus is null ? null : () => OnFocus( this ),
			OnBlur = OnBlur is null ? null : _ => OnBlur( this ),
			OnCancel = OnCancel is null ? null : () => OnCancel( this ),
			OnValidationChanged = OnValidationChanged is null ? null : isValid => OnValidationChanged( this, isValid ),
			OnClick = BlobOnClick,
			OnRightClick = BlobOnRightClick,
			OnMiddleClick = BlobOnMiddleClick,
			OnMouseEnter = BlobOnMouseEnter,
			OnMouseLeave = BlobOnMouseLeave,
			OnMouseDown = BlobOnMouseDown,
			OnMouseUp = BlobOnMouseUp,
			OnMouseMove = BlobOnMouseMove,
		} ) );
}

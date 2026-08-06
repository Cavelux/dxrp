using System;
using System.Collections.Generic;
using Sandbox;

namespace FieldGuide.Tips;

/// <summary>
/// The editor-authored twin of the runtime <see cref="TipTrigger"/> record. An author fills the fields
/// that match <see cref="Kind"/>; <see cref="ToTrigger"/> maps them to the runtime record. It carries no
/// delegates, so it serializes cleanly onto a <c>.tip</c> asset. <c>[InputAction]</c> renders the native
/// dropdown of the game's Input.config actions; <c>[ShowIf]</c> hides fields that do not apply to the
/// chosen kind (an editor nicety only, never load-bearing).
/// </summary>
public sealed class TipTriggerSpec
{
	[Property] public TipTriggerKind Kind { get; set; } = TipTriggerKind.Timer;

	/// <summary>InputAction: native dropdown of the game's Input.config actions.</summary>
	[Property, InputAction, ShowIf( nameof( Kind ), TipTriggerKind.InputAction )]
	public string Action { get; set; } = "";

	/// <summary>Key: a raw key / mouse-button name, e.g. "space", "mouse1".</summary>
	[Property, ShowIf( nameof( Kind ), TipTriggerKind.Key )]
	public string Key { get; set; } = "";

	/// <summary>Signal / Ever / Flag / AtLeast key (the named condition this trigger reads).</summary>
	[Property] public string Name { get; set; } = "";

	[Property, ShowIf( nameof( Kind ), TipTriggerKind.AtLeast )] public float Threshold { get; set; }
	[Property, ShowIf( nameof( Kind ), TipTriggerKind.Timer )] public float Seconds { get; set; }

	/// <summary>AnalogAxis: which stick to watch (its magnitude is compared against <see cref="Magnitude"/>).</summary>
	[Property, ShowIf( nameof( Kind ), TipTriggerKind.AnalogAxis )]
	public TipTriggerAnalogSource AnalogSource { get; set; } = TipTriggerAnalogSource.AnalogMove;

	/// <summary>AnalogAxis: the stick magnitude (0..1) at or past which the trigger fires.</summary>
	[Property, ShowIf( nameof( Kind ), TipTriggerKind.AnalogAxis )]
	public float Magnitude { get; set; }

	/// <summary>Children for AnyOf / AllOf.</summary>
	[Property] public List<TipTriggerSpec> Children { get; set; } = new();

	/// <summary>Map this authored spec to the runtime <see cref="TipTrigger"/> record, recursing children.
	/// The field-to-kind rule itself lives in the pure <see cref="TipTriggerBuild.From"/>, shared with the
	/// Tips Studio's picker so the asset and the panel build byte-identical triggers.</summary>
	public TipTrigger ToTrigger()
		=> TipTriggerBuild.From( Kind, Action, Key, Name, Threshold, Seconds, AnalogSource, Magnitude, ChildrenToTriggers() );

	private List<TipTrigger> ChildrenToTriggers()
	{
		if ( Children is null || Children.Count == 0 )
			return null;

		var list = new List<TipTrigger>( Children.Count );
		foreach ( var child in Children )
			list.Add( child?.ToTrigger() ?? new TipTrigger { Kind = TipTriggerKind.Always } );
		return list;
	}
}

/// <summary>
/// A tip authored as a <c>.tip</c> asset: the free authoring UI. Every field is an inspector control, so
/// creating a tip is creating an asset with no code. It mirrors <see cref="TipDefinition"/> minus the two
/// arbitrary <c>Func</c> predicates (which stay code-only, section 6.3 of the spec). Modelled directly on
/// the RPG kit's <c>ItemDefinition</c> / <c>QuestDefinition</c>.
///
/// The <c>.tip</c> asset covers the input / signal / timer / context trigger family. World-anchored
/// completion (proximity, look-at) lives on the <see cref="TipTriggerObject"/> component, which needs a
/// scene anchor, so it is authored by dropping the component on the object rather than in this asset.
/// </summary>
[AssetType( Name = "Tip", Extension = "tip", Category = "Field Guide Tips" )]
public sealed class TipResource : GameResource
{
	[Property, Group( "Identity" )] public string Id { get; set; } = "";
	[Property, Group( "Identity" ), TextArea] public string Text { get; set; } = "";

	/// <summary>Optional gamepad wording, shown instead of <see cref="Text"/> on a controller. Leave blank to
	/// use <see cref="Text"/> on both devices. Mark gamepad buttons in `backticks`, same as <see cref="Text"/>.</summary>
	[Property, Group( "Identity" ), TextArea] public string TextPad { get; set; } = "";

	[Property, Group( "Identity" )] public string Icon { get; set; } = "";

	[Property, Group( "Order" )] public int Priority { get; set; }
	[Property, Group( "Order" )] public List<string> PrerequisiteTipIds { get; set; } = new();

	[Property, Group( "Completion" )] public TipTriggerSpec Completion { get; set; } = new();

	/// <summary>Safety valve: auto-complete after this many visible seconds (0 = never on this path).</summary>
	[Property, Group( "Completion" )] public float MaxShowSeconds { get; set; }

	[Property, Group( "Relevance" )] public TipTriggerSpec Relevance { get; set; } = new();

	/// <summary>Called when the asset is first loaded from disk (a fresh session, or a <c>.tip</c> created
	/// while the editor is running). Fills the id fallback, then tells the catalog its asset source moved so
	/// the new tip is in <see cref="TipsCatalog.Active"/> without a restart.</summary>
	protected override void PostLoad()
	{
		AdoptFileNameId();
		TipsCatalog.NoteAssetsChanged();
	}

	/// <summary>
	/// Called when the asset is recompiled / reloaded from disk: the engine's own live-reload hook, and the
	/// one that makes editing a <c>.tip</c> in the inspector (or in a text editor) reach a running session.
	/// The catalog caches its merged view, so without this an edited tip is invisible until something else
	/// invalidates it. <c>ResourceLibrary.IEventListener</c> would be the general form of this hook but it is
	/// internal to the engine, so a kit hooks the resource type it owns instead.
	/// </summary>
	protected override void PostReload()
	{
		AdoptFileNameId();
		TipsCatalog.NoteAssetsChanged();
	}

	// A tip authored without an explicit id falls back to its file name, same as ItemDefinition, so the
	// merged catalog always has a non-empty key.
	private void AdoptFileNameId()
	{
		if ( string.IsNullOrWhiteSpace( Id ) )
			Id = ResourceName;
	}

	/// <summary>Map this authored asset to the runtime <see cref="TipDefinition"/> the coach reads.</summary>
	public TipDefinition ToDefinition()
	{
		return new TipDefinition
		{
			Id = Id ?? "",
			Text = Text ?? "",
			TextPad = string.IsNullOrEmpty( TextPad ) ? null : TextPad,
			Icon = Icon ?? "",
			Priority = Priority,
			PrerequisiteTipIds = ( PrerequisiteTipIds is null || PrerequisiteTipIds.Count == 0 )
				? Array.Empty<string>()
				: PrerequisiteTipIds.ToArray(),
			MaxShowSeconds = MaxShowSeconds,
			Completion = Completion?.ToTrigger(),
			Relevance = Relevance?.ToTrigger(),
		};
	}
}

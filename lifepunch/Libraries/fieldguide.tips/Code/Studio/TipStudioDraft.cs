using System;
using System.Collections.Generic;

namespace FieldGuide.Tips;

/// <summary>
/// A tip being AUTHORED. It carries exactly the fields a <c>.tip</c> asset carries, in the same order, so it
/// is both what the Tips Studio edits and what gets written out as JSON; and it maps to a runtime
/// <see cref="TipDefinition"/> so a draft can be live in the catalog while you are still typing it.
///
/// Why this exists next to <see cref="TipResource"/> rather than reusing it: <see cref="TipResource"/> is a
/// <c>GameResource</c>, which means the engine owns its lifetime and it only exists once a file does. A
/// draft has no file yet. This type is plain data with no <c>Sandbox</c> reference at all, which also lets the
/// headless harness round-trip the exact serialization the editor writes.
/// </summary>
public sealed class TipStudioDraft
{
	/// <summary>Stable id. Doubles as the file name when the draft is baked out (<c>&lt;id&gt;.tip</c>).</summary>
	public string Id { get; set; } = "";

	/// <summary>The tip line. <c>*asterisks*</c> mark a keycap chip, <c>`backticks`</c> a gamepad chip.</summary>
	public string Text { get; set; } = "";

	/// <summary>Optional gamepad wording. Blank uses <see cref="Text"/> on both devices.</summary>
	public string TextPad { get; set; } = "";

	/// <summary>A small emoji glyph shown beside the text.</summary>
	public string Icon { get; set; } = "";

	/// <summary>Higher wins when several tips are eligible at once.</summary>
	public int Priority { get; set; }

	/// <summary>Tip ids that must be complete before this one may show.</summary>
	public List<string> PrerequisiteTipIds { get; set; } = new();

	/// <summary>When the tip retires.</summary>
	public TipStudioTrigger Completion { get; set; } = new();

	/// <summary>Safety valve: auto-complete after this many visible seconds. 0 never times out.</summary>
	public float MaxShowSeconds { get; set; }

	/// <summary>Narrows when the tip may show.</summary>
	public TipStudioTrigger Relevance { get; set; } = new();

	/// <summary>The runtime tip this draft describes, ready for
	/// <see cref="TipsCatalog.RegisterRuntime(TipDefinition)"/>. A blank id yields null: the catalog keys on
	/// id and an unnamed draft would be unreachable.</summary>
	public TipDefinition ToDefinition()
	{
		if ( string.IsNullOrWhiteSpace( Id ) )
			return null;

		return new TipDefinition
		{
			Id = Id,
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

	/// <summary>Open an existing catalog tip in the editor. The two code-only predicates
	/// (<c>Trigger</c> / <c>CompleteWhen</c>) have no authored form and are dropped, so a code tip opened here
	/// and baked out keeps its declarative triggers and loses its predicates. The Studio says so on screen.</summary>
	public static TipStudioDraft FromDefinition( TipDefinition def )
	{
		if ( def is null )
			return new TipStudioDraft();

		return new TipStudioDraft
		{
			Id = def.Id ?? "",
			Text = def.Text ?? "",
			TextPad = def.TextPad ?? "",
			Icon = def.Icon ?? "",
			Priority = def.Priority,
			PrerequisiteTipIds = def.PrerequisiteTipIds is null
				? new List<string>()
				: new List<string>( def.PrerequisiteTipIds ),
			MaxShowSeconds = def.MaxShowSeconds,
			Completion = TipStudioTrigger.FromTrigger( def.Completion ),
			Relevance = TipStudioTrigger.FromTrigger( def.Relevance ),
		};
	}

	/// <summary>A deep copy, so editing a draft never writes through to the one it was copied from.</summary>
	public TipStudioDraft Clone() => new()
	{
		Id = Id,
		Text = Text,
		TextPad = TextPad,
		Icon = Icon,
		Priority = Priority,
		PrerequisiteTipIds = new List<string>( PrerequisiteTipIds ?? new List<string>() ),
		Completion = Completion?.Clone() ?? new TipStudioTrigger(),
		MaxShowSeconds = MaxShowSeconds,
		Relevance = Relevance?.Clone() ?? new TipStudioTrigger(),
	};
}

/// <summary>
/// The authored form of one trigger: a kind plus the boxes that kind reads. It mirrors
/// <see cref="TipTriggerSpec"/> field for field (that is the shape the <c>.tip</c> file carries) but holds no
/// inspector attributes and no <c>Sandbox</c> reference, so the Studio and the harness share it. Both it and
/// the asset build their runtime trigger through the same <see cref="TipTriggerBuild.From"/>.
/// </summary>
public sealed class TipStudioTrigger
{
	/// <summary>Which observable condition this trigger watches.</summary>
	public TipTriggerKind Kind { get; set; } = TipTriggerKind.Always;

	/// <summary>InputAction: an action name from the project's input settings.</summary>
	public string Action { get; set; } = "";

	/// <summary>Key: a raw key / mouse-button name, e.g. "space", "mouse1".</summary>
	public string Key { get; set; } = "";

	/// <summary>Signal / Ever / Flag / AtLeast: the named condition this trigger reads.</summary>
	public string Name { get; set; } = "";

	/// <summary>AtLeast: the Number(Name) value to reach.</summary>
	public float Threshold { get; set; }

	/// <summary>Timer: visible seconds before it fires.</summary>
	public float Seconds { get; set; }

	/// <summary>AnalogAxis: which stick to watch.</summary>
	public TipTriggerAnalogSource AnalogSource { get; set; } = TipTriggerAnalogSource.AnalogMove;

	/// <summary>AnalogAxis: the stick magnitude (0 to 1) to reach.</summary>
	public float Magnitude { get; set; }

	/// <summary>AnyOf / AllOf: the composed triggers.</summary>
	public List<TipStudioTrigger> Children { get; set; } = new();

	/// <summary>Map this authored trigger to the runtime record, recursing children.</summary>
	public TipTrigger ToTrigger()
		=> TipTriggerBuild.From( Kind, Action, Key, Name, Threshold, Seconds, AnalogSource, Magnitude, ChildTriggers() );

	private List<TipTrigger> ChildTriggers()
	{
		if ( Children is null || Children.Count == 0 )
			return null;

		var list = new List<TipTrigger>( Children.Count );
		foreach ( var child in Children )
			list.Add( child?.ToTrigger() ?? new TipTrigger { Kind = TipTriggerKind.Always } );
		return list;
	}

	/// <summary>Read a runtime trigger back into authored boxes, so an existing tip opens in the picker with
	/// its fields filled. Only the fields the kind reads are carried over, which is what keeps a
	/// build-then-read round trip stable.</summary>
	public static TipStudioTrigger FromTrigger( TipTrigger trigger )
	{
		if ( trigger is null )
			return new TipStudioTrigger();

		var spec = new TipStudioTrigger { Kind = trigger.Kind };

		switch ( trigger.Kind )
		{
			case TipTriggerKind.InputAction:
				spec.Action = FirstOr( trigger.Actions );
				break;

			case TipTriggerKind.Key:
				spec.Key = FirstOr( trigger.Keys );
				break;

			case TipTriggerKind.Signal:
			case TipTriggerKind.Ever:
			case TipTriggerKind.Flag:
				spec.Name = trigger.Key ?? "";
				break;

			case TipTriggerKind.AtLeast:
				spec.Name = trigger.Key ?? "";
				spec.Threshold = trigger.Threshold;
				break;

			case TipTriggerKind.Timer:
				spec.Seconds = trigger.Seconds;
				break;

			case TipTriggerKind.AnalogAxis:
				spec.AnalogSource = trigger.AnalogSource;
				spec.Magnitude = trigger.Threshold;
				break;

			case TipTriggerKind.AnyOf:
			case TipTriggerKind.AllOf:
				if ( trigger.Children is not null )
					foreach ( var child in trigger.Children )
						spec.Children.Add( FromTrigger( child ) );
				break;
		}

		return spec;
	}

	/// <summary>A deep copy.</summary>
	public TipStudioTrigger Clone()
	{
		var copy = new TipStudioTrigger
		{
			Kind = Kind,
			Action = Action,
			Key = Key,
			Name = Name,
			Threshold = Threshold,
			Seconds = Seconds,
			AnalogSource = AnalogSource,
			Magnitude = Magnitude,
		};

		if ( Children is not null )
			foreach ( var child in Children )
				copy.Children.Add( child?.Clone() ?? new TipStudioTrigger() );

		return copy;
	}

	private static string FirstOr( string[] values )
		=> values is null || values.Length == 0 ? "" : ( values[0] ?? "" );

	// ------------------------------------------------------------------
	// Kind names. Spelled out rather than reflected off the enum: these strings are the ON-DISK values in a
	// .tip file, so they are a format, not a debug rendering, and a rename of an enum member must be a
	// deliberate edit here rather than a silent file-format break. It also keeps the Studio's dropdown, the
	// writer and the reader reading one list.
	// ------------------------------------------------------------------

	/// <summary>Every trigger kind, in the order the Studio's dropdown offers them: the two composites last,
	/// because they are the only ones that nest.</summary>
	public static IReadOnlyList<TipTriggerKind> AllKinds { get; } = new[]
	{
		TipTriggerKind.Always,
		TipTriggerKind.InputAction,
		TipTriggerKind.Key,
		TipTriggerKind.Signal,
		TipTriggerKind.Ever,
		TipTriggerKind.Flag,
		TipTriggerKind.AtLeast,
		TipTriggerKind.Timer,
		TipTriggerKind.AnalogAxis,
		TipTriggerKind.AnyOf,
		TipTriggerKind.AllOf,
	};

	/// <summary>Both analog sources, for the AnalogAxis picker.</summary>
	public static IReadOnlyList<TipTriggerAnalogSource> AllAnalogSources { get; } = new[]
	{
		TipTriggerAnalogSource.AnalogMove,
		TipTriggerAnalogSource.AnalogLook,
	};

	/// <summary>The on-disk name of a trigger kind.</summary>
	public static string KindName( TipTriggerKind kind ) => kind switch
	{
		TipTriggerKind.InputAction => "InputAction",
		TipTriggerKind.Key => "Key",
		TipTriggerKind.Signal => "Signal",
		TipTriggerKind.Ever => "Ever",
		TipTriggerKind.Flag => "Flag",
		TipTriggerKind.AtLeast => "AtLeast",
		TipTriggerKind.Timer => "Timer",
		TipTriggerKind.AnalogAxis => "AnalogAxis",
		TipTriggerKind.AnyOf => "AnyOf",
		TipTriggerKind.AllOf => "AllOf",
		_ => "Always",
	};

	/// <summary>Read an on-disk kind name. Anything unrecognised reads as <see cref="TipTriggerKind.Always"/>,
	/// which adds no gate, so a file from a newer kit degrades to "no extra condition" instead of throwing.</summary>
	public static TipTriggerKind ParseKind( string name ) => name switch
	{
		"InputAction" => TipTriggerKind.InputAction,
		"Key" => TipTriggerKind.Key,
		"Signal" => TipTriggerKind.Signal,
		"Ever" => TipTriggerKind.Ever,
		"Flag" => TipTriggerKind.Flag,
		"AtLeast" => TipTriggerKind.AtLeast,
		"Timer" => TipTriggerKind.Timer,
		"AnalogAxis" => TipTriggerKind.AnalogAxis,
		"AnyOf" => TipTriggerKind.AnyOf,
		"AllOf" => TipTriggerKind.AllOf,
		_ => TipTriggerKind.Always,
	};

	/// <summary>The on-disk name of an analog source.</summary>
	public static string SourceName( TipTriggerAnalogSource source )
		=> source == TipTriggerAnalogSource.AnalogLook ? "AnalogLook" : "AnalogMove";

	/// <summary>Read an on-disk analog source name; anything else is the movement stick.</summary>
	public static TipTriggerAnalogSource ParseSource( string name )
		=> name == "AnalogLook" ? TipTriggerAnalogSource.AnalogLook : TipTriggerAnalogSource.AnalogMove;

	/// <summary>Which authored box a kind actually reads, for the picker: it shows one field, not nine.
	/// "none" means the kind takes no parameter (Always), "children" means it composes others.</summary>
	public static string FieldFor( TipTriggerKind kind ) => kind switch
	{
		TipTriggerKind.InputAction => "action",
		TipTriggerKind.Key => "key",
		TipTriggerKind.Signal or TipTriggerKind.Ever or TipTriggerKind.Flag => "name",
		TipTriggerKind.AtLeast => "name+threshold",
		TipTriggerKind.Timer => "seconds",
		TipTriggerKind.AnalogAxis => "stick+magnitude",
		TipTriggerKind.AnyOf or TipTriggerKind.AllOf => "children",
		_ => "none",
	};
}

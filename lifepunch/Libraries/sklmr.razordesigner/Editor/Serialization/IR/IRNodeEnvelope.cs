using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grains.RazorDesigner.Document;

namespace Grains.RazorDesigner.Serialization.IR;

public sealed record IRNodeEnvelope
{
	// Shared empty collections — avoids allocations for the common case.
	private static readonly IReadOnlyList<Grains.RazorDesigner.Wiring.Binding> _emptyBindings = System.Array.Empty<Grains.RazorDesigner.Wiring.Binding>();
	private static readonly IReadOnlyDictionary<string, object>                _emptyMetadata = new Dictionary<string, object>();
	private static readonly IReadOnlyList<IRNodeEnvelope>                     _emptyChildren = System.Array.Empty<IRNodeEnvelope>();
	private static readonly IReadOnlyDictionary<string, IRNodeEnvelope>       _emptySlots    = new Dictionary<string, IRNodeEnvelope>();

	// Document identity GUID — preserved verbatim across save/load. Never reminted on read.
	public Guid Id { get; init; }

	// Control type as a PascalCase string (JsonStringEnumConverter handles this).
	public ControlType Kind { get; init; }

	// CSS class name — user-editable, used as the SCSS selector.
	public string ClassName { get; init; }

	// All layout/visual appearance fields. Serialises the M4 Appearance struct directly.
	public Appearance Appearance { get; init; } = Appearance.Default;

	// Per-kind payload with "$type" discriminator (from [JsonPolymorphic] on abstract Payload).
	public Payload Payload { get; init; }

	public IReadOnlyDictionary<string, IRNodeEnvelope> Slots { get; init; } = _emptySlots;

	// Non-slot children in document order.
	public IReadOnlyList<IRNodeEnvelope> Children { get; init; } = _emptyChildren;

	[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public IReadOnlyList<IRStateEnvelope> States { get; init; }

	[JsonIgnore( Condition = JsonIgnoreCondition.Never )]
	public IReadOnlyList<Grains.RazorDesigner.Wiring.Binding> Bindings { get; init; } = _emptyBindings;

	// Reserved metadata dict — always {} in v1.
	[JsonIgnore( Condition = JsonIgnoreCondition.Never )]
	public IReadOnlyDictionary<string, object> Metadata { get; init; } = _emptyMetadata;

	// Forward-compat: unknown per-node keys from future-version files ride here.
	[JsonExtensionData]
	public IDictionary<string, JsonElement> Extra { get; init; }

	public Dictionary<string, string> CustomStyles { get; init; }
}

public sealed record IRStateEnvelope
{
	public Grains.RazorDesigner.Document.PseudoKind State { get; init; }

	[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public Grains.RazorDesigner.Document.NthChildMode NthChildMode { get; init; } = Grains.RazorDesigner.Document.NthChildMode.Literal;

	[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public int NthChildArg { get; init; } = 1;

	public Grains.RazorDesigner.Document.Appearance Delta { get; init; } = Grains.RazorDesigner.Document.Appearance.Default;
}

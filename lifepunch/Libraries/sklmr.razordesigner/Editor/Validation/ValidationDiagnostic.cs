using System;

namespace Grains.RazorDesigner.Validation;

public enum DiagnosticSeverity
{
	Warn,
	Error,
}

public readonly record struct ValidationDiagnostic(
	Guid? NodeId,
	DiagnosticSeverity Severity,
	string Code,
	string Message );

public static class DiagnosticCodes
{
	/// <summary>A non-container node has children (e.g. Button with a Label child).</summary>
	public const string LeafHasChildren = "leaf.has-children";

	/// <summary>A SplitContainer is missing its "left" or "right" slot.</summary>
	public const string SlotMissing = "slot.missing";

	/// <summary>Two or more nodes share the same ClassName, causing SCSS rule collisions.</summary>
	public const string ClassNameDuplicate = "classname.duplicate";

	public const string SchemaUnsupported = "schema.unsupported";

	public const string ReservedKind = "reserved.kind";

	public const string BindingsNonempty = "bindings.nonempty";

	public const string UnsupportedPseudoClass = "pseudo.unsupported";
}

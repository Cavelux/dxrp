using System;
using System.Collections.Generic;
using Grains.RazorDesigner.Contracts;
using Grains.RazorDesigner.Document;
using Grains.RazorDesigner.Projection;

namespace Grains.RazorDesigner.Validation;

public sealed class Validator : IValidator
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	public IReadOnlyList<ValidationDiagnostic> Validate( IReadOnlyNode root )
	{
		var diagnostics = new List<ValidationDiagnostic>();

		// --- Dormant stubs (M5) -----------------------------------------------

		CheckSchemaUnsupported( diagnostics );

		CheckReservedKind( diagnostics );

		CheckBindingsNonempty( diagnostics );

		// --- Live rules (M3) --------------------------------------------------

		var seenClassNames = new Dictionary<string, Guid>( StringComparer.Ordinal );

		// Iterative DFS walk (avoids stack overflow on pathological deep trees).
		var stack = new Stack<IReadOnlyNode>();
		if ( root is not null )
			stack.Push( root );

		while ( stack.Count > 0 )
		{
			IReadOnlyNode node;
			try
			{
				node = stack.Pop();
			}
			catch ( Exception ex )
			{
				Log.Warning( $"{LogPrefix} Validator: exception popping node — {ex.Message}; skipping" );
				continue;
			}

			try
			{
				// Rule 1: leaf.has-children
				CheckLeafHasChildren( node, diagnostics );

				// Rule 2: slot.missing (only for SplitContainer)
				CheckSlotMissing( node, diagnostics );

				// Rule 3: classname.duplicate — accumulate into seenClassNames
				CheckClassNameDuplicate( node, seenClassNames, diagnostics );

				// Rule 7: pseudo.unsupported — reject state rules with out-of-set PseudoKind or bad :nth-child
				CheckStateRules( node, diagnostics );

				// Enqueue non-slot children (slot children are already in node.Slots).
				foreach ( var child in node.Children )
				{
					if ( child is not null )
						stack.Push( child );
				}

				foreach ( var slotList in node.Slots.Values )
				{
					foreach ( var slotChild in slotList )
					{
						if ( slotChild is not null )
							stack.Push( slotChild );
					}
				}
			}
			catch ( Exception ex )
			{
				// Malformed node (e.g. unrecognized Kind string) → diagnostic, never throw.
				Log.Warning( $"{LogPrefix} Validator: exception processing node {node?.Id} — {ex.Message}" );
				diagnostics.Add( new ValidationDiagnostic(
					node?.Id,
					DiagnosticSeverity.Error,
					DiagnosticCodes.SchemaUnsupported,
					$"Validator error on node {node?.Id}: {ex.Message}" ) );
			}
		}

		var count = diagnostics.Count;
		Log.Info( $"{LogPrefix} Validator: {count} diagnostic(s)" );
		return diagnostics;
	}

	// ─── Rule 1: leaf.has-children ───────────────────────────────────────────

	private static void CheckLeafHasChildren( IReadOnlyNode node, List<ValidationDiagnostic> out_ )
	{
		if ( node is null ) return;

		ControlType kind;
		if ( !Enum.TryParse( node.Kind, out kind ) )
			return; // unknown kind — let the malformed-node catch handle it if needed

		if ( ContractScanner.Table.Get( kind ).IsContainer ) return;  // containers are allowed children

		if ( node.Children.Count > 0 )
		{
			out_.Add( new ValidationDiagnostic(
				node.Id,
				DiagnosticSeverity.Error,
				DiagnosticCodes.LeafHasChildren,
				$"{kind} (class \"{node.ClassName}\") cannot have children but has {node.Children.Count}." ) );
		}
	}

	// ─── Rule 2: slot.missing ────────────────────────────────────────────────

	private static void CheckSlotMissing( IReadOnlyNode node, List<ValidationDiagnostic> out_ )
	{
		if ( node is null ) return;

		ControlType kind;
		if ( !Enum.TryParse( node.Kind, out kind ) ) return;
		if ( kind != ControlType.SplitContainer ) return;

		foreach ( var slotName in new[] { "left", "right" } )
		{
			IReadOnlyList<IReadOnlyNode> slotList;
			var hasSlot = node.Slots.TryGetValue( slotName, out slotList );
			if ( !hasSlot || slotList is null || slotList.Count == 0 )
			{
				out_.Add( new ValidationDiagnostic(
					node.Id,
					DiagnosticSeverity.Error,
					DiagnosticCodes.SlotMissing,
					$"SplitContainer (class \"{node.ClassName}\") is missing the \"{slotName}\" slot." ) );
			}
		}
	}

	// ─── Rule 3: classname.duplicate ─────────────────────────────────────────

	private static void CheckClassNameDuplicate(
		IReadOnlyNode node,
		Dictionary<string, Guid> seen,
		List<ValidationDiagnostic> out_ )
	{
		if ( node is null ) return;

		var name = node.ClassName;
		// Blank class names are valid (the node just has no class) — skip the duplicate check.
		if ( string.IsNullOrEmpty( name ) ) return;

		Guid firstId;
		if ( seen.TryGetValue( name, out firstId ) )
		{
			out_.Add( new ValidationDiagnostic(
				node.Id,
				DiagnosticSeverity.Error,
				DiagnosticCodes.ClassNameDuplicate,
				$"ClassName \"{name}\" is used by more than one node (first: {firstId}). SCSS rules will merge silently." ) );
		}
		else
		{
			seen[name] = node.Id;
		}
	}

	private static void CheckStateRules( IReadOnlyNode node, List<ValidationDiagnostic> out_ )
	{
		if ( node is null ) return;
		foreach ( var sr in node.StateRules )
		{
			if ( sr is null ) continue;

			if ( !Enum.IsDefined( typeof( PseudoKind ), sr.State ) )
			{
				out_.Add( new ValidationDiagnostic(
					node.Id,
					DiagnosticSeverity.Error,
					DiagnosticCodes.UnsupportedPseudoClass,
					$"Node \"{node.ClassName}\" has a state rule with unsupported pseudo-class value {(int)sr.State}." ) );
				continue;
			}

			if ( sr.State == PseudoKind.NthChild
				 && sr.NthChildMode == NthChildMode.Literal
				 && sr.NthChildArg < 1 )
			{
				out_.Add( new ValidationDiagnostic(
					node.Id,
					DiagnosticSeverity.Error,
					DiagnosticCodes.UnsupportedPseudoClass,
					$"Node \"{node.ClassName}\" has :nth-child({sr.NthChildArg}) — must be >= 1 (or use odd/even)." ) );
			}
		}
	}

	// ─── Dormant stubs (activate in M5) ──────────────────────────────────────

	private static void CheckSchemaUnsupported( List<ValidationDiagnostic> out_ )
	{
	}

	// activates in M5 when the IR envelope (themeRef / localization) exists.
	private static void CheckReservedKind( List<ValidationDiagnostic> out_ )
	{
	}

	// activates in M5 when the bindings field exists on documents / nodes.
	private static void CheckBindingsNonempty( List<ValidationDiagnostic> out_ )
	{
	}
}

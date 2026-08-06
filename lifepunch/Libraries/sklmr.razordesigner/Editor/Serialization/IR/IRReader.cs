using System;
using System.IO;
using System.Text.Json;
using Grains.RazorDesigner.Document;

namespace Grains.RazorDesigner.Serialization.IR;

public static class IRReader
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	public static DesignerDocument ReadDocument( string json )
	{
		if ( string.IsNullOrEmpty( json ) )
			throw new ArgumentException( $"{LogPrefix} IRReader.ReadDocument: json is null or empty", nameof( json ) );

		Log.Info( $"{LogPrefix} IRReader.ReadDocument: deserialising ({json.Length} chars)" );

		var envelope = JsonSerializer.Deserialize<IRDocumentEnvelope>( json, DesignerIRJson.Options );
		if ( envelope is null )
			throw new InvalidDataException( $"{LogPrefix} IRReader.ReadDocument: deserialised envelope is null" );

		// Schema migration — throws on future version; no-ops if schemaVersion == Current.
		SchemaMigrations.Migrate( ref envelope );

		// Schema-level validation — rejects missing root node; typed wiring is self-validating.
		ValidateSchema( envelope );

		var doc = FromEnvelope( envelope );
		Log.Info( $"{LogPrefix} IRReader.ReadDocument: built document (root children: {doc.RootRecord.Children.Count})" );
		return doc;
	}


	private static void ValidateSchema( IRDocumentEnvelope env )
	{
		if ( env.Root is null )
			throw new InvalidDataException( $"{LogPrefix} IRReader: 'root' node is missing from the envelope." );
	}


	private static DesignerDocument FromEnvelope( IRDocumentEnvelope env )
	{
		var doc = new DesignerDocument();

		CopyEnvelopeAppearanceAndPayloadOnto( env.Root, doc.RootRecord );

		// Recurse regular children (non-slot).
		foreach ( var child in env.Root.Children )
			doc.RootRecord.Children.Add( BuildRecord( child ) );

		// Slot children of the root node (unlikely for a Panel root, but handled generically).
		foreach ( var kvp in env.Root.Slots )
		{
			var built = BuildRecord( kvp.Value );
			built.IsSlot  = true;
			built.SlotName = kvp.Key;
			doc.RootRecord.Children.Add( built );
		}

		doc.SeedCounters();

		doc.Wiring = env.Wiring ?? Grains.RazorDesigner.Wiring.WiringEnvelope.Empty;

		return doc;
	}

	private static ControlRecord BuildRecord( IRNodeEnvelope node )
	{
		// Id is init-only — set it in the object initialiser so the persisted GUID is preserved.
		var record = new ControlRecord
		{
			Type       = node.Kind,
			Id         = node.Id,
			ClassName  = node.ClassName,
			// Appearance is init-only — set it here in the object initialiser.
			Appearance = node.Appearance,
		};

		// Unpack payload fields from the deserialised Payload leaf back into the raw auto-props.
		UnpackPayload( node.Payload, record );

		// Recurse regular (non-slot) children.
		foreach ( var child in node.Children )
			record.Children.Add( BuildRecord( child ) );

		// Rebuild slot children from the Slots dict (e.g. SplitContainer "left"/"right").
		foreach ( var kvp in node.Slots )
		{
			var slot = BuildRecord( kvp.Value );
			slot.IsSlot  = true;
			slot.SlotName = kvp.Key;

			// The slot record's own children were already handled by the recursive BuildRecord above.
			record.Children.Add( slot );
		}

		// Populate per-state style overrides from the States array (omitted in JSON when empty).
		if ( node.States is { Count: > 0 } )
		{
			foreach ( var s in node.States )
			{
				record.StateRules.Add( new StateRule
				{
					State        = s.State,
					NthChildMode = s.NthChildMode,
					NthChildArg  = s.NthChildArg,
					Delta        = s.Delta,
				} );
			}
		}

		if ( node.Bindings is { Count: > 0 } )
			record.Bindings.AddRange( node.Bindings );

		if ( node.CustomStyles is { Count: > 0 } )
		{
			foreach ( var kvp in node.CustomStyles )
			{
				record.CustomStyles[kvp.Key] = kvp.Value;
			}
		}
		
		return record;
	}

	private static void CopyEnvelopeAppearanceAndPayloadOnto( IRNodeEnvelope node, ControlRecord target )
	{
		var scratch = new ControlRecord
		{
			Type       = target.Type,
			Id         = target.Id,
			ClassName  = target.ClassName,
			Appearance = node.Appearance,
		};

		// Load payload fields into the scratch record first.
		UnpackPayload( node.Payload, scratch );

		if ( node.Bindings is { Count: > 0 } )
			scratch.Bindings.AddRange( node.Bindings );

		if ( node.CustomStyles is { Count: > 0 } )
		{
			foreach ( var kvp in node.CustomStyles )
			{
				scratch.CustomStyles[kvp.Key] = kvp.Value;
			}
		}
		
		scratch.CopyFieldsTo( target );

		Log.Info( $"{LogPrefix} IRReader: applied envelope root appearance to RootRecord (ClassName={target.ClassName})" );
	}

	private static void UnpackPayload( Payload payload, ControlRecord record )
	{
		if ( payload is null ) return;

		record.Content      = payload.Content;
		record.Placeholder  = payload.Placeholder;
		record.Source       = payload.Source;
		record.IconName     = payload.IconName;
		record.CheckboxSize = payload.CheckboxSize;
	}
}

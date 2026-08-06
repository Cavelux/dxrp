using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Grains.RazorDesigner.Document;

namespace Grains.RazorDesigner.Serialization.IR;

public static class IRWriter
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	// Empty collections reused for nodes that have no slots/children/metadata.
	private static readonly IReadOnlyDictionary<string, object> _emptyMetadata = new Dictionary<string, object>();
	private static readonly IReadOnlyList<IRNodeEnvelope>      _emptyChildren = System.Array.Empty<IRNodeEnvelope>();
	private static readonly IReadOnlyDictionary<string, IRNodeEnvelope> _emptySlots = new Dictionary<string, IRNodeEnvelope>();

	public static string WriteDocument( DesignerDocument doc )
	{
		if ( doc is null )
			throw new ArgumentNullException( nameof( doc ) );

		Log.Info( $"{LogPrefix} IRWriter.WriteDocument: serialising document (root children: {doc.RootRecord.Children.Count})" );

		var envelope = new IRDocumentEnvelope
		{
			Root   = ToNode( doc.RootRecord ),
			Wiring = doc.Wiring ?? Grains.RazorDesigner.Wiring.WiringEnvelope.Empty,
		};

		var json = JsonSerializer.Serialize( envelope, DesignerIRJson.Options );

		// Normalise CRLF → LF (canonical form; .gitattributes also pins LF as a backstop).
		if ( json.Contains( '\r' ) )
			json = json.Replace( "\r\n", "\n" ).Replace( "\r", "\n" );

		Log.Info( $"{LogPrefix} IRWriter.WriteDocument: OK ({json.Length} chars)" );
		return json;
	}

	public static string CanonicalHash( string json )
	{
		if ( json is null )
			throw new ArgumentNullException( nameof( json ) );

		var bytes = Encoding.UTF8.GetBytes( json );
		var hash  = SHA256.HashData( bytes );
		return Convert.ToHexString( hash ).ToLowerInvariant();
	}

	// Recursively converts a ControlRecord to its IRNodeEnvelope representation.
	private static IRNodeEnvelope ToNode( ControlRecord r )
	{
		Dictionary<string, IRNodeEnvelope> slotDict  = null;
		List<IRNodeEnvelope>               childList = null;

		foreach ( var child in r.Children )
		{
			if ( child.IsSlot )
			{
				slotDict ??= new Dictionary<string, IRNodeEnvelope>();
				slotDict[child.SlotName] = ToNode( child );
			}
			else
			{
				childList ??= new List<IRNodeEnvelope>();
				childList.Add( ToNode( child ) );
			}
		}

		return new IRNodeEnvelope
		{
			Id         = r.Id,
			Kind       = r.Type,
			ClassName  = r.ClassName,
			Appearance = r.Appearance,
			Payload    = r.Payload,
			Slots      = slotDict  is not null
				? (IReadOnlyDictionary<string, IRNodeEnvelope>)slotDict
				: _emptySlots,
			Children   = childList is not null
				? (IReadOnlyList<IRNodeEnvelope>)childList
				: _emptyChildren,
			States = r.StateRules.Count == 0
				? null
				: r.StateRules
					.OrderBy( rule => rule, Comparer<StateRule>.Create( StateRule.CompareCanonical ) )
					.Select( rule => new IRStateEnvelope
					{
						State       = rule.State,
						NthChildMode = rule.NthChildMode,
						NthChildArg  = rule.NthChildArg,
						Delta        = rule.Delta,
					} )
					.ToList(),
			Bindings   = r.Bindings.Count == 0
				? System.Array.Empty<Grains.RazorDesigner.Wiring.Binding>()
				: r.Bindings.ToArray(),

				CustomStyles = r.CustomStyles.Count == 0 
				? null 
				: new Dictionary<string, string>( r.CustomStyles ),
		};
	}
}

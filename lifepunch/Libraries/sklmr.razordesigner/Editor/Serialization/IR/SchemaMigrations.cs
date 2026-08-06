using System.IO;

namespace Grains.RazorDesigner.Serialization.IR;

public static class SchemaMigrations
{
	public const int Current = 4;

	public static void Migrate( ref IRDocumentEnvelope env )
	{
		if ( env.SchemaVersion > Current )
			throw new InvalidDataException(
				$"[Grains.RazorDesigner] IRReader: file written by a newer Razor Designer " +
				$"(schema={env.SchemaVersion} > current={Current}). Please update the Designer addon." );

	}
}

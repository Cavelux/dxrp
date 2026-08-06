namespace Grains.RazorDesigner;

// Production-vs-development gate.
//
// To ship: remove RAZORDESIGNER_DEBUG from .sbproj DefineConstants and
// Editor/grains_razordesigner.editor.csproj DefineConstants. With the
// symbol undefined:
//   - Dev-only Editor menu entries (goldens, contract dump, round-trip
//     fixtures) drop out via #if RAZORDESIGNER_DEBUG on their [Menu]
//     attributes — they truly disappear from the Editor menu, not just
//     no-op.
//   - Log.Info(...) calls compile to nothing (see the shadow `Log` below).
//   - Log.Warning / Log.Error always fire — real diagnostics, never gated.
//   - `if ( RazorDesignerDebug.Enabled ) { … }` blocks fold away — the
//     const lets the compiler dead-strip the body.
//
// User-facing menu entries (Razor Designer dock, "Regenerate bundled
// templates") are NOT gated — they stay visible always.
public static class RazorDesignerDebug
{
#if RAZORDESIGNER_DEBUG
	public const bool Enabled = true;
#else
	public const bool Enabled = false;
#endif
}

// Shadow over the engine's Log static aggregator
// (Sandbox.Internal.GlobalGameNamespace.Log, brought in via the csproj's
// <Using Static="true" />). Code in Grains.RazorDesigner.* resolves `Log`
// to this class first via enclosing-namespace lookup, which takes
// precedence over `using static` imports. That means existing `Log.Info`
// callsites in every file under Editor/ and Code/ route through here
// without any rewrite.
//
// Info is gated; Warning/Error pass through identically so real
// diagnostics still surface in production.
internal static class Log
{
	public static void Info( string message )
	{
#if RAZORDESIGNER_DEBUG
		Sandbox.Internal.GlobalGameNamespace.Log.Info( message );
#endif
	}

	public static void Warning( string message )
		=> Sandbox.Internal.GlobalGameNamespace.Log.Warning( message );

	public static void Warning( System.Exception ex, string message )
		=> Sandbox.Internal.GlobalGameNamespace.Log.Warning( ex, message );

	public static void Error( string message )
		=> Sandbox.Internal.GlobalGameNamespace.Log.Error( message );

	public static void Error( System.Exception ex, string message )
		=> Sandbox.Internal.GlobalGameNamespace.Log.Error( ex, message );
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Editor;
using Grains.RazorDesigner.Contracts;
using Grains.RazorDesigner.Projection.Tests;

namespace Grains.RazorDesigner;

public static class ProjectionGoldensMenu
{
    private const string LogPrefix = "[Grains.RazorDesigner]";

    // Dev-only Editor menu entries — hidden in production via RAZORDESIGNER_DEBUG.
    // See Editor/Common/RazorDesignerDebug.cs.
#if RAZORDESIGNER_DEBUG

    [Menu( "Editor", "Razor Designer/Run projection goldens", "fact_check" )]
    public static void RunProjectionGoldens()
    {
        List<GoldenRunner.GoldenResult> results;
        try
        {
            results = GoldenRunner.RunAll();
        }
        catch ( Exception ex )
        {
            Log.Error( $"{LogPrefix} GoldenRunner.RunAll threw: {ex.GetType().Name}: {ex.Message}" );
            EditorUtility.DisplayDialog(
                "Projection Goldens — Error",
                $"RunAll failed with {ex.GetType().Name}:\n{ex.Message}",
                "Okay", "⚠️" );
            return;
        }

        // Tally results
        int pass  = 0;
        int total = results.Count;
        var diffs = new List<string>();

        foreach ( var r in results )
        {
            if ( r.Pass )
            {
                pass++;
                Log.Info( $"{LogPrefix} Goldens: PASS [{r.Kind}]" );
            }
            else
            {
                Log.Warning( $"{LogPrefix} Goldens: FAIL [{r.Kind}]: {r.Message}" );
                if ( diffs.Count < 5 )
                    diffs.Add( $"[{r.Kind}] {r.Message}" );
            }
        }

        Log.Info( $"{LogPrefix} Goldens: {pass}/{total} passed" );

        // Build modal text
        var sb = new StringBuilder();
        sb.AppendLine( $"{pass}/{total} passed" );

        if ( diffs.Count > 0 )
        {
            sb.AppendLine();
            sb.AppendLine( "First failing diff(s):" );
            foreach ( var d in diffs )
            {
                sb.AppendLine( $"  {d}" );
            }
        }

        var icon  = pass == total ? "✅" : "❌";
        var title = pass == total
            ? $"Projection Goldens — {pass}/{total} PASSED"
            : $"Projection Goldens — {pass}/{total} FAILED";

        EditorUtility.DisplayDialog( title, sb.ToString().TrimEnd(), "Okay", icon );
    }


    [Menu( "Editor", "Razor Designer/Dump Contract Table", "table_view" )]
    public static void DumpContractTable()
    {
        Log.Info( $"{LogPrefix} === Dump Contract Table ===" );
        try
        {
            var table = ContractScanner.Table;
            int count = 0;

            foreach ( var c in table.All )
            {
                count++;
                var slotNames   = string.Join( ",", c.Slots.Select( s => s.Name ) );
                var fieldNames  = string.Join( ", ", c.PayloadFields.Select( f =>
                    $"{f.Name}:{f.ClrType.Name}" +
                    ( string.IsNullOrEmpty( f.Group ) ? "" : $"@{f.Group}" ) +
                    ( f.IsOverrideGate ? "(gate)" : "" )
                ) );

                Log.Info( $"{LogPrefix} contract {c.Kind}: " +
                    $"payload={c.PayloadType.Name} " +
                    $"tag='{c.LibraryTag}' " +
                    $"container={c.IsContainer} " +
                    $"icon='{c.InspectorIcon}' " +
                    $"slots=[{slotNames}] " +
                    $"preview={c.PreviewStrategy} " +
                    $"fields={c.PayloadFields.Count}: {fieldNames}" );
            }

            Log.Info( $"{LogPrefix} === Contract Table dump complete: {count} contracts ===" );
            EditorUtility.DisplayDialog(
                "Contract Table Dump",
                $"{count} contracts dumped to the editor console.\n\n" +
                "Expected: 13 contracts, one per ControlType.\n" +
                "Check console for per-kind payload-field counts and names.",
                "Okay", "table_view" );
        }
        catch ( Exception ex )
        {
            Log.Error( $"{LogPrefix} DumpContractTable threw: {ex.GetType().Name}: {ex.Message}" );
            EditorUtility.DisplayDialog(
                "Contract Table Dump — Error",
                $"ContractScanner.Table threw {ex.GetType().Name}:\n{ex.Message}",
                "Okay", "⚠️" );
        }
    }


    [Menu( "Editor", "Razor Designer/Update projection goldens (autoupdate)", "autorenew" )]
    public static void UpdateProjectionGoldens()
    {
        // Confirm before overwriting — this is an intentional-change workflow.
        EditorUtility.DisplayDialog(
            "Update Projection Goldens",
            "This will overwrite all golden 'expected' blocks with current projector output.\n\nProceed?",
            "Cancel",
            "Update All",
            () =>
            {
                try
                {
                    GoldenRunner.WriteAllGoldens( overwrite: true );
                    Log.Info( $"{LogPrefix} Goldens: all expected blocks updated." );
                    EditorUtility.DisplayDialog(
                        "Projection Goldens Updated",
                        "All golden expected blocks have been rewritten from current projector output.\n\nRun 'Run projection goldens' to verify.",
                        "Okay", "✅" );
                }
                catch ( Exception ex )
                {
                    Log.Error( $"{LogPrefix} GoldenRunner.WriteAllGoldens threw: {ex.GetType().Name}: {ex.Message}" );
                    EditorUtility.DisplayDialog(
                        "Projection Goldens — Update Error",
                        $"WriteAllGoldens failed:\n{ex.Message}",
                        "Okay", "⚠️" );
                }
            },
            "❓" );
    }


    [Menu( "Editor", "Razor Designer/Run CSharp goldens", "code" )]
    public static void RunCSharpGoldens()
    {
        List<CSharpGoldenRunner.CSharpGoldenResult> results;
        try
        {
            results = CSharpGoldenRunner.RunAll();
        }
        catch ( Exception ex )
        {
            Log.Error( $"{LogPrefix} CSharpGoldenRunner.RunAll threw: {ex.GetType().Name}: {ex.Message}" );
            EditorUtility.DisplayDialog(
                "CSharp Goldens — Error",
                $"RunAll failed with {ex.GetType().Name}:\n{ex.Message}",
                "Okay", "⚠️" );
            return;
        }

        int pass  = 0;
        int total = results.Count;
        var diffs = new List<string>();

        foreach ( var r in results )
        {
            if ( r.Pass )
            {
                pass++;
                Log.Info( $"{LogPrefix} CSharpGoldens: PASS [{r.Name}]" );
            }
            else
            {
                Log.Warning( $"{LogPrefix} CSharpGoldens: FAIL [{r.Name}]: {r.Message}" );
                if ( diffs.Count < 5 )
                    diffs.Add( $"[{r.Name}] {r.Message}" );
            }
        }

        Log.Info( $"{LogPrefix} CSharpGoldens: {pass}/{total} passed" );

        var sb = new StringBuilder();
        sb.AppendLine( $"{pass}/{total} passed" );
        if ( diffs.Count > 0 )
        {
            sb.AppendLine();
            sb.AppendLine( "First failing diff(s):" );
            foreach ( var d in diffs ) sb.AppendLine( $"  {d}" );
        }

        var icon  = pass == total ? "✅" : "❌";
        var title = pass == total
            ? $"CSharp Goldens — {pass}/{total} PASSED"
            : $"CSharp Goldens — {pass}/{total} FAILED";

        EditorUtility.DisplayDialog( title, sb.ToString().TrimEnd(), "Okay", icon );
    }

    [Menu( "Editor", "Razor Designer/Update CSharp goldens (autoupdate)", "autorenew" )]
    public static void UpdateCSharpGoldens()
    {
        EditorUtility.DisplayDialog(
            "Update CSharp Goldens",
            "This will overwrite all CSharp golden 'expected' blocks with current projector output.\n\nProceed?",
            "Cancel",
            "Update All",
            () =>
            {
                try
                {
                    CSharpGoldenRunner.WriteAllGoldens( overwrite: true );
                    Log.Info( $"{LogPrefix} CSharpGoldens: all expected blocks updated." );
                    EditorUtility.DisplayDialog(
                        "CSharp Goldens Updated",
                        "All expected blocks rewritten from current projector output.\n\nRun 'Run CSharp goldens' to verify.",
                        "Okay", "✅" );
                }
                catch ( Exception ex )
                {
                    Log.Error( $"{LogPrefix} CSharpGoldenRunner.WriteAllGoldens threw: {ex.GetType().Name}: {ex.Message}" );
                    EditorUtility.DisplayDialog(
                        "CSharp Goldens — Update Error",
                        $"WriteAllGoldens failed:\n{ex.Message}",
                        "Okay", "⚠️" );
                }
            },
            "❓" );
    }


    [Menu( "Editor", "Razor Designer/Run round-trip tests", "verified" )]
    public static void RunRoundTripTests()
    {
        var results = RoundTripRunner.RunAll();
        var passed  = results.FindAll( r => r.Pass ).Count;
        var failed  = results.Count - passed;
        Log.Info( $"{LogPrefix} Round-trip: {passed} pass, {failed} fail (of {results.Count})" );
    }

    [Menu( "Editor", "Razor Designer/Re-canonicalize round-trip fixtures", "brush" )]
    public static void RecanonicalizeRoundTripFixtures()
    {
        var root = Sandbox.Project.Current?.RootDirectory?.FullName;
        if ( string.IsNullOrEmpty( root ) )
        {
            Log.Warning( $"{LogPrefix} Re-canonicalize: Project.Current is null." );
            return;
        }
        var dir = System.IO.Path.Combine( root, "Editor", "Projection", "Tests", "Goldens" );
        foreach ( var path in System.IO.Directory.GetFiles( dir, "*.roundtrip.json" ) )
        {
            try
            {
                var original = System.IO.File.ReadAllText( path );
                var env      = System.Text.Json.JsonSerializer.Deserialize<Grains.RazorDesigner.Serialization.IR.IRDocumentEnvelope>(
                    original, Grains.RazorDesigner.Serialization.IR.DesignerIRJson.Options );
                var canonical = System.Text.Json.JsonSerializer.Serialize(
                    env, Grains.RazorDesigner.Serialization.IR.DesignerIRJson.Options );
                if ( canonical.Contains( '\r' ) )
                    canonical = canonical.Replace( "\r\n", "\n" ).Replace( "\r", "\n" );
                System.IO.File.WriteAllText( path, canonical );
                Log.Info( $"{LogPrefix} Re-canonicalized {System.IO.Path.GetFileName( path )}" );
            }
            catch ( System.Exception ex )
            {
                Log.Warning( $"{LogPrefix} Re-canonicalize failed for {path}: {ex.Message}" );
            }
        }
    }

#endif // RAZORDESIGNER_DEBUG
}

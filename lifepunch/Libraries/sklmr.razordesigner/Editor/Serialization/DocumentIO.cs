using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Grains.RazorDesigner.Document;
using Grains.RazorDesigner.Projection;
using Grains.RazorDesigner.Serialization.IR;
using Grains.RazorDesigner.Validation;
using Sandbox;

namespace Grains.RazorDesigner.Serialization;

public readonly record struct SaveResult(
    string JsonPath,
    string RazorPath,
    string ScssPath,
    bool RazorExported,
    IReadOnlyList<ValidationDiagnostic> Diagnostics,
    string CSharpPath = null,
    bool CSharpExported = false );

public readonly record struct LoadResult(
    DesignerDocument Document,
    string ResolvedPath,
    IReadOnlyList<string> Warnings,
    bool Success,
    bool DriftDetected = false,
    bool StampMismatch = false,
    bool MigratedFromLegacy = false,
    string LegacyRazorPath = null );

public static class DocumentIO
{
    private const string LogPrefix = "[Grains.RazorDesigner]";

    public static SaveResult Save( DesignerDocument document, string razorPath, PreviewTheme theme )
    {
        // Perf probe (grd-pewf): covers validate + IR write + .razor/.scss regen + disk I/O.
        var probeSw = System.Diagnostics.Stopwatch.StartNew();
        var className = System.IO.Path.GetFileNameWithoutExtension( razorPath );

        RewriteOutOfAssetsImages( document );

        IReadOnlyList<ValidationDiagnostic> diags;
        bool hasError;
        try
        {
            diags = new Validator().Validate( new RecordNode( document.RootRecord ) );
            hasError = diags.Any( d => d.Severity == DiagnosticSeverity.Error );
            Log.Info( $"{LogPrefix} Save: validator returned {diags.Count} diagnostic(s), hasError={hasError}" );
            foreach ( var d in diags )
                Log.Warning( $"{LogPrefix} Save: {d.Severity} [{d.Code}] node={d.NodeId?.ToString() ?? "<document>"} — {d.Message}" );
        }
        catch ( Exception ex )
        {
            // Validator must not throw (spec: "validator is total"), but be defensive.
            Log.Warning( $"{LogPrefix} Save: validator threw unexpectedly: {ex.Message}; proceeding as if no errors" );
            diags = Array.Empty<ValidationDiagnostic>();
            hasError = false;
        }

        var probeDiskMs = 0.0;
        var probeLap = System.Diagnostics.Stopwatch.StartNew();

        var json     = IRWriter.WriteDocument( document );
        var probeJsonMs = probeLap.Elapsed.TotalMilliseconds; probeLap.Restart();
        var hash     = IRWriter.CanonicalHash( json );
        var probeHashMs = probeLap.Elapsed.TotalMilliseconds; probeLap.Restart();
        var jsonPath = razorPath + ".json";
        var tmp      = jsonPath + ".tmp";

        System.IO.File.WriteAllText( tmp, json );
        System.IO.File.Move( tmp, jsonPath, overwrite: true );
        probeDiskMs += probeLap.Elapsed.TotalMilliseconds; probeLap.Restart();
        Log.Info( $"{LogPrefix} Saved -> {jsonPath}" );

        // Step 4: Conditional .razor + .razor.scss regeneration.
        var scssPath = razorPath + ".scss";
        var csPath = razorPath + ".cs";
        bool razorExported = false;
        bool csharpExported = false;
        var probeRazorMs = 0.0;
        var probeScssMs = 0.0;
        var probeCsharpMs = 0.0;

        if ( !hasError )
        {
            try
            {
                probeLap.Restart();
                var razor = DocumentSerializer.GenerateRazorMarkup( document, hash );
                probeRazorMs = probeLap.Elapsed.TotalMilliseconds; probeLap.Restart();
                var scss  = DocumentSerializer.GenerateSavedScss( document, className, theme );
                probeScssMs = probeLap.Elapsed.TotalMilliseconds; probeLap.Restart();
                System.IO.File.WriteAllText( razorPath, razor );
                System.IO.File.WriteAllText( scssPath, scss );
                probeDiskMs += probeLap.Elapsed.TotalMilliseconds;
                razorExported = true;
                Log.Info( $"{LogPrefix} Saved -> {razorPath}" );
                Log.Info( $"{LogPrefix} Saved -> {scssPath}" );
            }
            catch ( Exception ex )
            {
                Log.Error( $"{LogPrefix} Razor export failed (JSON saved OK): {ex.Message}" );
            }

            try
            {
                probeLap.Restart();
                var nsFallback = Project.Current?.Package?.Ident ?? "Grains";
                var cs = DocumentSerializer.GenerateCSharp( document, className, nsFallback );
                probeCsharpMs = probeLap.Elapsed.TotalMilliseconds; probeLap.Restart();
                if ( cs is not null )
                {
                    System.IO.File.WriteAllText( csPath, cs );
                    probeDiskMs += probeLap.Elapsed.TotalMilliseconds;
                    csharpExported = true;
                    Log.Info( $"{LogPrefix} Saved -> {csPath}" );
                }
                else
                {
                    Log.Info( $"{LogPrefix} CSharp emit elided (empty wiring); existing {csPath} untouched if present." );
                }
            }
            catch ( Exception ex )
            {
                Log.Error( $"{LogPrefix} CSharp export failed (JSON saved OK): {ex.Message}" );
            }
        }
        else
        {
            var errorCount = diags.Count( d => d.Severity == DiagnosticSeverity.Error );
            Log.Warning(
                $"{LogPrefix} Razor export skipped — {errorCount} validation error(s); " +
                $".razor.json saved. Fix the errors and re-save to regenerate .razor." );
        }

        var probeTotal = probeSw.Elapsed.TotalMilliseconds;
        Log.Info( $"{LogPrefix} probe: Save took {probeTotal:F3}ms = IRWrite {probeJsonMs:F3} + hash {probeHashMs:F3} + razorGen {probeRazorMs:F3} + scssGen {probeScssMs:F3} + csharpGen {probeCsharpMs:F3} + disk {probeDiskMs:F3} + validate/other {probeTotal - probeJsonMs - probeHashMs - probeRazorMs - probeScssMs - probeCsharpMs - probeDiskMs:F3} (razorExported={razorExported}, csharpExported={csharpExported})" );

        return new SaveResult( jsonPath, razorPath, scssPath, razorExported, diags, csPath, csharpExported );
    }

    private static readonly Regex IrHashStampRegex = new(
        @"@\*\s*generated-from-ir-hash:\s*(?<h>[0-9a-f]+)\s*\*@",
        RegexOptions.Compiled );

    private static string NormalizeNewlines( string s ) =>
        s is null ? null : s.Replace( "\r\n", "\n" ).Replace( "\r", "\n" );

    public static LoadResult Load( string path )
    {
        if ( string.IsNullOrEmpty( path ) )
        {
            Log.Warning( $"{LogPrefix} Load: path is null or empty" );
            return new LoadResult( null, path, new[] { "path is null or empty" }, Success: false );
        }

        var stem     = path.EndsWith( ".razor.json", StringComparison.OrdinalIgnoreCase )
            ? path[..^5]   // strip ".json" → yields "…/Foo.razor"
            : path;
        var jsonPath = stem + ".json";  // "…/Foo.razor.json"

        Log.Info( $"{LogPrefix} Load: stem={stem}  jsonPath={jsonPath}" );

        if ( File.Exists( jsonPath ) )
        {
            DesignerDocument doc;
            try
            {
                var jsonText = File.ReadAllText( jsonPath );
                doc = IRReader.ReadDocument( jsonText );
            }
            catch ( Exception ex )
            {
                var msg = $"Failed to read .razor.json: {ex.Message}";
                Log.Error( $"{LogPrefix} Load: {msg}  ({jsonPath})" );
                return new LoadResult( null, jsonPath, new[] { msg }, Success: false,
                    LegacyRazorPath: stem );
            }

            // Check for drift / stamp-mismatch if .razor also exists.
            if ( File.Exists( stem ) )
            {
                var razorText        = NormalizeNewlines( File.ReadAllText( stem ) );
                var jsonForHash      = File.ReadAllText( jsonPath );
                var expectedHash     = IRWriter.CanonicalHash( jsonForHash );
                var regeneratedRazor = NormalizeNewlines( DocumentSerializer.GenerateRazorMarkup( doc, expectedHash ) );

                if ( string.Equals( razorText, regeneratedRazor, StringComparison.Ordinal ) )
                {
                    // .razor matches the IR byte-for-byte (modulo line endings) — silent canonical load.
                    Log.Info( $"{LogPrefix} Load: loaded from .razor.json (canonical, .razor matches IR)" );
                    return new LoadResult( doc, jsonPath, Array.Empty<string>(), Success: true,
                        LegacyRazorPath: stem );
                }

                var stampMatch = IrHashStampRegex.Match( razorText );
                var stampHash  = stampMatch.Success ? stampMatch.Groups["h"].Value : null;

                if ( stampHash == expectedHash )
                {
                    Log.Info( $"{LogPrefix} Load: drift detected — .razor body hand-edited (stamp intact: {stampHash})" );
                    return new LoadResult( doc, jsonPath, Array.Empty<string>(), Success: true,
                        DriftDetected: true, LegacyRazorPath: stem );
                }

                Log.Info( $"{LogPrefix} Load: stamp mismatch — .razor generated from a different IR " +
                          $"(stamp={stampHash ?? "<absent>"}  expected={expectedHash})" );
                return new LoadResult( doc, jsonPath, Array.Empty<string>(), Success: true,
                    StampMismatch: true, LegacyRazorPath: stem );
            }

            // .razor.json present, .razor absent — clean canonical load (no drift check possible).
            Log.Info( $"{LogPrefix} Load: loaded from .razor.json (canonical, no .razor sibling)" );
            return new LoadResult( doc, jsonPath, Array.Empty<string>(), Success: true,
                LegacyRazorPath: stem );
        }

        if ( File.Exists( stem ) )
        {
            Log.Info( $"{LogPrefix} Load: no .razor.json found — cold-migrating from legacy .razor: {stem}" );

            var (doc, diags) = LegacyRazorImporter.Import( stem );
            var warnings = diags.Select( d => $"{d.Severity}: {d.Message}" ).ToList();

            if ( doc == null )
            {
                var reason = warnings.Count > 0 ? warnings[warnings.Count - 1] : "unknown";
                Log.Warning( $"{LogPrefix} Load: cold migration aborted — {reason}" );
                return new LoadResult( null, stem, warnings, Success: false,
                    LegacyRazorPath: stem );
            }

            // Write the .razor.json sibling so subsequent opens go through the canonical path.
            WriteJson( doc, jsonPath );
            Log.Info( $"{LogPrefix} Load: cold-migrated from legacy .razor → wrote {jsonPath}" );

            var migDiag = new ValidationDiagnostic(
                NodeId:   null,
                Severity: DiagnosticSeverity.Warn,
                Code:     "legacy-migrated",
                Message:  $"Migrated from legacy .razor — created {jsonPath}" );
            warnings.Add( $"{migDiag.Severity}: {migDiag.Message}" );

            return new LoadResult( doc, stem, warnings, Success: true,
                MigratedFromLegacy: true, LegacyRazorPath: stem );
        }

        Log.Warning( $"{LogPrefix} Load: file not found: {path}" );
        return new LoadResult( null, path, new[] { $"file not found: {path}" }, Success: false );
    }

    internal static void WriteJson( DesignerDocument doc, string jsonPath )
    {
        var json = IRWriter.WriteDocument( doc );
        var tmp  = jsonPath + ".tmp";
        File.WriteAllText( tmp, json );
        File.Move( tmp, jsonPath, overwrite: true );
        Log.Info( $"{LogPrefix} WriteJson: wrote {jsonPath}" );
    }

    public static void RewriteOutOfAssetsImages( DesignerDocument document )
    {
        var assetsRoot = Project.Current?.GetAssetsPath();
        if ( string.IsNullOrEmpty( assetsRoot ) )
        {
            Log.Warning( $"{LogPrefix} Auto-import skipped: Project.Current has no assets path" );
            return;
        }

        var importsDir = System.IO.Path.Combine( assetsRoot, "ImageImports" );

        foreach ( var r in document.WalkAll() )
        {
            if ( r.Type != ControlType.Image ) continue;
            if ( string.IsNullOrEmpty( r.Source ) ) continue;

            // Inside-Assets paths don't escape; nothing to do.
            var rel = r.Source.Replace( '\\', '/' );
            if ( !rel.StartsWith( "../" ) ) continue;

            var sourceAbs = System.IO.Path.GetFullPath( r.Source, assetsRoot );
            if ( !System.IO.File.Exists( sourceAbs ) )
            {
                Log.Warning( $"{LogPrefix} Auto-import skipped (file missing): {sourceAbs}" );
                continue;
            }

            System.IO.Directory.CreateDirectory( importsDir );

            var fileName = System.IO.Path.GetFileName( sourceAbs );
            var stem = System.IO.Path.GetFileNameWithoutExtension( fileName );
            var ext = System.IO.Path.GetExtension( fileName );

            var dest = System.IO.Path.Combine( importsDir, fileName );
            var n = 1;
            while ( System.IO.File.Exists( dest ) && !FilesEqual( sourceAbs, dest ) )
            {
                dest = System.IO.Path.Combine( importsDir, $"{stem}_{n}{ext}" );
                n++;
            }

            if ( !System.IO.File.Exists( dest ) )
            {
                System.IO.File.Copy( sourceAbs, dest );
                Log.Info( $"{LogPrefix} Auto-imported image: {sourceAbs} -> {dest}" );
            }
            else
            {
                Log.Info( $"{LogPrefix} Auto-import: existing identical file reused at {dest}" );
            }

            r.Source = $"ImageImports/{System.IO.Path.GetFileName( dest )}";
        }
    }

    private static bool FilesEqual( string a, string b )
    {
        try
        {
            var ia = new System.IO.FileInfo( a );
            var ib = new System.IO.FileInfo( b );
            if ( ia.Length != ib.Length ) return false;

            using var sa = ia.OpenRead();
            using var sb = ib.OpenRead();
            var bufA = new byte[8192];
            var bufB = new byte[8192];
            int read;
            while ( ( read = sa.Read( bufA, 0, bufA.Length ) ) > 0 )
            {
                var got = sb.Read( bufB, 0, read );
                if ( got != read ) return false;
                for ( int i = 0; i < read; i++ )
                    if ( bufA[i] != bufB[i] ) return false;
            }
            return true;
        }
        catch ( System.Exception e )
        {
            Log.Warning( $"{LogPrefix} FilesEqual({a}, {b}) failed: {e.Message}" );
            return false;
        }
    }
}

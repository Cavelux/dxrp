using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Grains.RazorDesigner.Serialization;
using Sandbox;

namespace Grains.RazorDesigner.Projection.Tests;

public static class GoldenRunner
{
    private const string LogPrefix = "[Grains.RazorDesigner]";
    private const int SupportedFormatVersion = 1;


    private static string GoldensDir()
    {
        var root = Project.Current?.RootDirectory?.FullName
            ?? throw new InvalidOperationException( "GoldenRunner: Project.Current is null — cannot locate Goldens directory." );
        return Path.Combine( root, "Editor", "Projection", "Tests", "Goldens" );
    }


    public readonly struct GoldenResult
    {
        public string Kind    { get; }
        public bool   Pass    { get; }
        public string Message { get; }

        public GoldenResult( string kind, bool pass, string message )
        {
            Kind    = kind;
            Pass    = pass;
            Message = message;
        }
    }

    // Run all *.golden.json fixtures + PanelOpExhaustivenessTest.
    public static List<GoldenResult> RunAll()
    {
        var results = new List<GoldenResult>();
        var dir = GoldensDir();

        if ( !Directory.Exists( dir ) )
        {
            Log.Warning( $"{LogPrefix} GoldenRunner.RunAll: Goldens directory not found at '{dir}'." );
            results.Add( new GoldenResult( "<dir-missing>", false, $"Goldens directory missing: {dir}" ) );
            return results;
        }

        foreach ( var path in Directory.GetFiles( dir, "*.golden.json" ) )
        {
            var result = RunFile( path );
            results.Add( result );
            if ( result.Pass )
                Log.Info( $"{LogPrefix} PASS [{result.Kind}]" );
            else
                Log.Warning( $"{LogPrefix} FAIL [{result.Kind}]: {result.Message}" );
        }

        // --- Exhaustiveness test ---
        var (exPass, exMsg) = PanelOpExhaustivenessTest.Run();
        results.Add( new GoldenResult( "PanelOpExhaustivenessTest", exPass, exMsg ) );
        if ( exPass )
            Log.Info( $"{LogPrefix} PASS [PanelOpExhaustivenessTest]: {exMsg}" );
        else
            Log.Warning( $"{LogPrefix} FAIL [PanelOpExhaustivenessTest]: {exMsg}" );

        return results;
    }

    // Run a single kind by name (matches filename stem <kind>.golden.json).
    public static GoldenResult Run( string kind )
    {
        var path = Path.Combine( GoldensDir(), $"{kind.ToLowerInvariant()}.golden.json" );
        if ( !File.Exists( path ) )
            return new GoldenResult( kind, false, $"No golden file at '{path}'" );
        return RunFile( path );
    }

    public static void WriteGolden( string kind, bool overwrite = false )
    {
        var dir  = GoldensDir();
        var path = Path.Combine( dir, $"{kind.ToLowerInvariant()}.golden.json" );

        if ( !File.Exists( path ) )
            throw new FileNotFoundException( $"GoldenRunner.WriteGolden: fixture file not found at '{path}'. Create the fixture node first." );

        var json    = File.ReadAllText( path );
        var fixture = JsonSerializer.Deserialize<GoldenFixture>( json, GoldenJson.Options )
            ?? throw new InvalidOperationException( $"GoldenRunner.WriteGolden: failed to deserialize '{path}'." );

        if ( fixture.Expected != null && !overwrite )
            throw new InvalidOperationException(
                $"GoldenRunner.WriteGolden: fixture '{kind}' already has an expected block. Pass overwrite:true to replace it." );

        var result = RunProjection( fixture );
        fixture.Expected = ResultToExpected( result );

        var updated = JsonSerializer.Serialize( fixture, GoldenJson.Options );
        File.WriteAllText( path, updated );
        Log.Info( $"{LogPrefix} GoldenRunner.WriteGolden: wrote expected block to '{path}'." );
    }

    // Write (or overwrite) expected blocks for ALL fixtures.
    public static void WriteAllGoldens( bool overwrite = false )
    {
        var dir = GoldensDir();
        if ( !Directory.Exists( dir ) )
        {
            Log.Warning( $"{LogPrefix} GoldenRunner.WriteAllGoldens: directory missing at '{dir}'." );
            return;
        }

        foreach ( var path in Directory.GetFiles( dir, "*.golden.json" ) )
        {
            var json    = File.ReadAllText( path );
            var fixture = JsonSerializer.Deserialize<GoldenFixture>( json, GoldenJson.Options );
            if ( fixture == null )
            {
                Log.Warning( $"{LogPrefix} GoldenRunner.WriteAllGoldens: failed to parse '{path}', skipping." );
                continue;
            }

            if ( fixture.Expected != null && !overwrite )
            {
                Log.Info( $"{LogPrefix} GoldenRunner.WriteAllGoldens: skipping '{fixture.Kind}' (expected present, overwrite=false)." );
                continue;
            }

            var result   = RunProjection( fixture );
            fixture.Expected = ResultToExpected( result );
            var updated  = JsonSerializer.Serialize( fixture, GoldenJson.Options );
            File.WriteAllText( path, updated );
            Log.Info( $"{LogPrefix} GoldenRunner.WriteAllGoldens: wrote '{fixture.Kind}'." );
        }
    }


    private static GoldenResult RunFile( string path )
    {
        var kind = "<unknown>";
        try
        {
            var json    = File.ReadAllText( path );
            var fixture = JsonSerializer.Deserialize<GoldenFixture>( json, GoldenJson.Options );
            if ( fixture == null )
                return new GoldenResult( kind, false, $"Deserialization returned null for '{path}'" );

            kind = fixture.Kind ?? "<unknown>";

            // Version gate
            if ( fixture.GoldenFormatVersion != SupportedFormatVersion )
                return new GoldenResult( kind, false,
                    $"goldenFormatVersion={fixture.GoldenFormatVersion}, expected {SupportedFormatVersion}" );

            if ( fixture.Expected == null )
                return new GoldenResult( kind, false, "expected block is null — run WriteGolden to author it first" );

            var actual = RunProjection( fixture );

            // --- Compare PanelOps (ordered structural equality) ---
            var expectedOps = ToOps( fixture.Expected.PanelOps );
            if ( expectedOps.Count != actual.PanelOps.Count )
                return new GoldenResult( kind, false,
                    $"PanelOps count: expected {expectedOps.Count}, got {actual.PanelOps.Count}" );

            for ( int i = 0; i < expectedOps.Count; i++ )
            {
                if ( !Equals( expectedOps[i], actual.PanelOps[i] ) )
                    return new GoldenResult( kind, false,
                        $"PanelOps[{i}]: expected {expectedOps[i]}, got {actual.PanelOps[i]}" );
            }

            // --- Compare ScssLines (normalize each line, keep order) ---
            IReadOnlyList<string> expScss;
            IReadOnlyList<string> actScss;
            try
            {
                expScss = ScssNormalizer.Normalize( fixture.Expected.ScssLines ?? new List<string>() );
                actScss = ScssNormalizer.Normalize( actual.ScssLines );
            }
            catch ( Exception ex )
            {
                return new GoldenResult( kind, false, $"ScssNormalizer: {ex.Message}" );
            }

            if ( expScss.Count != actScss.Count )
                return new GoldenResult( kind, false,
                    $"ScssLines count: expected {expScss.Count}, got {actScss.Count}" );

            for ( int i = 0; i < expScss.Count; i++ )
            {
                if ( !string.Equals( expScss[i], actScss[i], StringComparison.Ordinal ) )
                    return new GoldenResult( kind, false,
                        $"ScssLines[{i}]: expected \"{expScss[i]}\", got \"{actScss[i]}\"" );
            }

            // --- Compare RazorAttributes (ordered exact) ---
            var expAttrs = fixture.Expected.RazorAttributes ?? new List<string>();
            var actAttrs = actual.RazorAttributes;
            if ( expAttrs.Count != actAttrs.Count )
                return new GoldenResult( kind, false,
                    $"RazorAttributes count: expected {expAttrs.Count}, got {actAttrs.Count}" );

            for ( int i = 0; i < expAttrs.Count; i++ )
            {
                if ( !string.Equals( expAttrs[i], actAttrs[i], StringComparison.Ordinal ) )
                    return new GoldenResult( kind, false,
                        $"RazorAttributes[{i}]: expected \"{expAttrs[i]}\", got \"{actAttrs[i]}\"" );
            }

            // --- Compare RazorInnerText (exact) ---
            var expText = fixture.Expected.RazorInnerText;
            var actText = actual.RazorInnerText;
            if ( !string.Equals( expText, actText, StringComparison.Ordinal ) )
                return new GoldenResult( kind, false,
                    $"RazorInnerText: expected \"{expText}\", got \"{actText}\"" );

            return new GoldenResult( kind, true, $"PASS" );
        }
        catch ( Exception ex )
        {
            return new GoldenResult( kind, false, $"Exception: {ex.GetType().Name}: {ex.Message}" );
        }
    }

    private static ProjectionResult RunProjection( GoldenFixture fixture )
    {
        if ( !ProjectorRegistry.Has( fixture.Kind ) )
            throw new KeyNotFoundException( $"GoldenRunner: no projector registered for kind '{fixture.Kind}'" );

        var node = new FixtureNode( fixture.Node );
        var ctx  = new ProjectionContext(
            Theme:      Grains.RazorDesigner.Serialization.PreviewTheme.Default,
            ForPreview: fixture.Context?.ForPreview ?? false );

        // Base projection (PanelOps / RazorAttributes / RazorInnerText come from the projector).
        var proj = ProjectorRegistry.For( fixture.Kind ).Project( node, node.Appearance, node.Payload, ctx );

        // ScssLines comes from the Applier (projector lines + state blocks).
        var scssLines = Applier.GetNodeScssLines( node, ctx );

        return new ProjectionResult(
            PanelOps:        proj.PanelOps,
            ScssLines:       scssLines,
            RazorAttributes: proj.RazorAttributes,
            RazorInnerText:  proj.RazorInnerText );
    }

    private static List<PanelOp> ToOps( List<OpDto> dtos )
    {
        var result = new List<PanelOp>( dtos.Count );
        foreach ( var dto in dtos )
            result.Add( OpMapping.ToOp( dto ) );
        return result;
    }

    private static ExpectedDto ResultToExpected( ProjectionResult r )
    {
        var opDtos = new List<OpDto>( r.PanelOps.Count );
        foreach ( var op in r.PanelOps )
            opDtos.Add( OpMapping.FromOp( op ) );

        return new ExpectedDto
        {
            PanelOps         = opDtos,
            ScssLines        = new List<string>( r.ScssLines ),
            RazorAttributes  = new List<string>( r.RazorAttributes ),
            RazorInnerText   = r.RazorInnerText,
        };
    }
}

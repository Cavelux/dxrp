using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Grains.RazorDesigner.Projection.CSharp;
using Grains.RazorDesigner.Serialization.IR;
using Sandbox;

namespace Grains.RazorDesigner.Projection.Tests;

public static class CSharpGoldenRunner
{
    private const string LogPrefix = "[Grains.RazorDesigner]";
    private const int SupportedFormatVersion = 1;

    public readonly struct CSharpGoldenResult
    {
        public string Name    { get; }
        public bool   Pass    { get; }
        public string Message { get; }

        public CSharpGoldenResult( string name, bool pass, string message )
        {
            Name    = name;
            Pass    = pass;
            Message = message;
        }
    }

    private static string GoldensDir()
    {
        var root = Project.Current?.RootDirectory?.FullName
            ?? throw new InvalidOperationException( "CSharpGoldenRunner: Project.Current is null — cannot locate Goldens directory." );
        return Path.Combine( root, "Editor", "Projection", "Tests", "Goldens" );
    }

    public static List<CSharpGoldenResult> RunAll()
    {
        var results = new List<CSharpGoldenResult>();
        var dir = GoldensDir();

        if ( !Directory.Exists( dir ) )
        {
            Log.Warning( $"{LogPrefix} CSharpGoldenRunner.RunAll: Goldens directory not found at '{dir}'." );
            results.Add( new CSharpGoldenResult( "<dir-missing>", false, $"Goldens directory missing: {dir}" ) );
            return results;
        }

        foreach ( var path in Directory.GetFiles( dir, "*.csgolden.json" ) )
        {
            var result = RunFile( path );
            results.Add( result );
            if ( result.Pass )
                Log.Info( $"{LogPrefix} PASS [csgolden:{result.Name}]" );
            else
                Log.Warning( $"{LogPrefix} FAIL [csgolden:{result.Name}]: {result.Message}" );
        }

        var (exPass, exMsg) = CSharpOpExhaustivenessTest.Run();
        results.Add( new CSharpGoldenResult( "CSharpOpExhaustivenessTest", exPass, exMsg ) );
        if ( exPass )
            Log.Info( $"{LogPrefix} PASS [CSharpOpExhaustivenessTest]: {exMsg}" );
        else
            Log.Warning( $"{LogPrefix} FAIL [CSharpOpExhaustivenessTest]: {exMsg}" );

        return results;
    }

    public static void WriteAllGoldens( bool overwrite = false )
    {
        var dir = GoldensDir();
        if ( !Directory.Exists( dir ) )
        {
            Log.Warning( $"{LogPrefix} CSharpGoldenRunner.WriteAllGoldens: directory missing at '{dir}'." );
            return;
        }

        foreach ( var path in Directory.GetFiles( dir, "*.csgolden.json" ) )
        {
            try
            {
                var json    = File.ReadAllText( path );
                var fixture = JsonSerializer.Deserialize<CSharpFixture>( json, DesignerIRJson.Options );
                if ( fixture is null )
                {
                    Log.Warning( $"{LogPrefix} CSharpGoldenRunner.WriteAllGoldens: failed to parse '{path}', skipping." );
                    continue;
                }

                if ( fixture.Expected != null && !overwrite )
                {
                    Log.Info( $"{LogPrefix} CSharpGoldenRunner.WriteAllGoldens: skipping '{fixture.Name}' (expected present, overwrite=false)." );
                    continue;
                }

                var actualOps = RunProjection( fixture );
                fixture.Expected = new CSharpExpectedDto { Ops = ToDtos( actualOps ) };

                // Re-serialize with the canonical options and normalise to LF (mirrors IRWriter).
                var updated = JsonSerializer.Serialize( fixture, DesignerIRJson.Options );
                if ( updated.Contains( '\r' ) )
                    updated = updated.Replace( "\r\n", "\n" ).Replace( "\r", "\n" );

                File.WriteAllText( path, updated );
                Log.Info( $"{LogPrefix} CSharpGoldenRunner.WriteAllGoldens: wrote '{fixture.Name}'." );
            }
            catch ( Exception ex )
            {
                Log.Error( $"{LogPrefix} CSharpGoldenRunner.WriteAllGoldens: '{path}' failed — {ex.GetType().Name}: {ex.Message}" );
            }
        }
    }

    // --- Private helpers ---

    private static CSharpGoldenResult RunFile( string path )
    {
        var name = "<unknown>";
        try
        {
            var json    = File.ReadAllText( path );
            var fixture = JsonSerializer.Deserialize<CSharpFixture>( json, DesignerIRJson.Options );
            if ( fixture is null )
                return new CSharpGoldenResult( name, false, $"Deserialization returned null for '{path}'" );

            name = fixture.Name ?? Path.GetFileNameWithoutExtension( path );

            if ( fixture.GoldenFormatVersion != SupportedFormatVersion )
                return new CSharpGoldenResult( name, false,
                    $"goldenFormatVersion={fixture.GoldenFormatVersion}, expected {SupportedFormatVersion}" );

            if ( fixture.Expected is null )
                return new CSharpGoldenResult( name, false, "expected block is null — run 'Update CSharp goldens (autoupdate)' to author it first" );

            var actualOps  = RunProjection( fixture );
            var expectedOps = ToOps( fixture.Expected.Ops ?? new List<CSharpOpDto>() );

            if ( expectedOps.Count != actualOps.Count )
                return new CSharpGoldenResult( name, false,
                    $"ops count: expected {expectedOps.Count}, got {actualOps.Count}" );

            for ( int i = 0; i < expectedOps.Count; i++ )
            {
                if ( !Equals( expectedOps[i], actualOps[i] ) )
                    return new CSharpGoldenResult( name, false,
                        $"ops[{i}]: expected {expectedOps[i]}, got {actualOps[i]}" );
            }

            return new CSharpGoldenResult( name, true, "PASS" );
        }
        catch ( Exception ex )
        {
            return new CSharpGoldenResult( name, false, $"Exception: {ex.GetType().Name}: {ex.Message}" );
        }
    }

    private static IReadOnlyList<CSharpOp> RunProjection( CSharpFixture fixture )
    {
        var view = new WiringEnvelopeView(
            fixture.Wiring ?? Wiring.WiringEnvelope.Empty,
            fixture.NamespaceFallback ?? "Grains",
            fixture.ClassNameFallback ?? "Test" );

        var result = CSharpProjector.Project( view, fixture.DocumentHasAnyBindings );
        return result.Ops;
    }

    private static List<CSharpOpDto> ToDtos( IReadOnlyList<CSharpOp> ops )
    {
        var list = new List<CSharpOpDto>( ops.Count );
        foreach ( var op in ops )
            list.Add( CSharpOpMapping.FromOp( op ) );
        return list;
    }

    private static List<CSharpOp> ToOps( List<CSharpOpDto> dtos )
    {
        var list = new List<CSharpOp>( dtos.Count );
        foreach ( var dto in dtos )
            list.Add( CSharpOpMapping.ToOp( dto ) );
        return list;
    }
}

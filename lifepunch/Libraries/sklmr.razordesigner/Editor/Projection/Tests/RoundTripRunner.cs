using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Grains.RazorDesigner.Serialization.IR;
using Sandbox;

namespace Grains.RazorDesigner.Projection.Tests;

public static class RoundTripRunner
{
    private const string LogPrefix = "[Grains.RazorDesigner]";

    public readonly struct RoundTripResult
    {
        public string Name    { get; }
        public bool   Pass    { get; }
        public string Message { get; }

        public RoundTripResult( string name, bool pass, string message )
        {
            Name    = name;
            Pass    = pass;
            Message = message;
        }
    }

    private static string GoldensDir()
    {
        var root = Project.Current?.RootDirectory?.FullName
            ?? throw new InvalidOperationException( "RoundTripRunner: Project.Current is null — cannot locate Goldens directory." );
        return Path.Combine( root, "Editor", "Projection", "Tests", "Goldens" );
    }

    public static List<RoundTripResult> RunAll()
    {
        var results = new List<RoundTripResult>();
        var dir = GoldensDir();

        if ( !Directory.Exists( dir ) )
        {
            Log.Warning( $"{LogPrefix} RoundTripRunner.RunAll: Goldens directory not found at '{dir}'." );
            results.Add( new RoundTripResult( "<dir-missing>", false, $"Goldens directory missing: {dir}" ) );
            return results;
        }

        foreach ( var path in Directory.GetFiles( dir, "*.roundtrip.json" ) )
        {
            var result = RunFile( path );
            results.Add( result );
            if ( result.Pass )
                Log.Info( $"{LogPrefix} PASS [round-trip:{result.Name}]" );
            else
                Log.Warning( $"{LogPrefix} FAIL [round-trip:{result.Name}]: {result.Message}" );
        }

        return results;
    }

    private static RoundTripResult RunFile( string path )
    {
        var name = Path.GetFileNameWithoutExtension( path );
        try
        {
            // 1. Read on-disk text. .gitattributes pins LF; if a Windows checkout has CRLF, normalize.
            var original = File.ReadAllText( path );
            if ( original.Contains( '\r' ) )
                original = original.Replace( "\r\n", "\n" ).Replace( "\r", "\n" );

            // 2. Deserialize through the canonical Options.
            var envelope = JsonSerializer.Deserialize<IRDocumentEnvelope>( original, DesignerIRJson.Options );
            if ( envelope is null )
                return new RoundTripResult( name, false, "Deserialize returned null." );

            // 3. Re-serialize and normalize line endings the same way IRWriter does.
            var reserialized = JsonSerializer.Serialize( envelope, DesignerIRJson.Options );
            if ( reserialized.Contains( '\r' ) )
                reserialized = reserialized.Replace( "\r\n", "\n" ).Replace( "\r", "\n" );

            // 4. Byte-compare.
            if ( string.Equals( original, reserialized, StringComparison.Ordinal ) )
                return new RoundTripResult( name, true, "byte-identical" );

            // On mismatch, return the first divergent position for easier diagnosis.
            int max = Math.Min( original.Length, reserialized.Length );
            int divergeAt = -1;
            for ( int i = 0; i < max; i++ )
            {
                if ( original[i] != reserialized[i] )
                {
                    divergeAt = i;
                    break;
                }
            }
            if ( divergeAt < 0 )
                divergeAt = max;

            int contextStart = Math.Max( 0, divergeAt - 40 );
            var origCtx      = original.Substring( contextStart, Math.Min( 80, original.Length - contextStart ) );
            var rsCtx        = reserialized.Substring( contextStart, Math.Min( 80, reserialized.Length - contextStart ) );

            return new RoundTripResult( name, false,
                $"diverges at offset {divergeAt}. original: \"{origCtx}\" vs reserialized: \"{rsCtx}\"" );
        }
        catch ( Exception ex )
        {
            return new RoundTripResult( name, false, $"Exception: {ex.GetType().Name}: {ex.Message}" );
        }
    }
}

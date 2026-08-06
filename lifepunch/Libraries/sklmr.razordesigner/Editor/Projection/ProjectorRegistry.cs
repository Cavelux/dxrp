using System;
using System.Collections.Generic;
using System.Reflection;

namespace Grains.RazorDesigner.Projection;

public static class ProjectorRegistry
{
    private static readonly string LogPrefix = "[Grains.RazorDesigner]";

    private static readonly Dictionary<string, IControlProjector> _projectors;

    static ProjectorRegistry()
    {
        _projectors = new Dictionary<string, IControlProjector>( StringComparer.Ordinal );

        var asm = Assembly.GetExecutingAssembly();
        foreach ( var type in asm.GetTypes() )
        {
            if ( type.IsAbstract || type.IsInterface ) continue;
            if ( !typeof( IControlProjector ).IsAssignableFrom( type ) ) continue;

            var attr = type.GetCustomAttribute<ProjectorAttribute>();
            if ( attr == null ) continue;

            try
            {
                var instance = (IControlProjector)Activator.CreateInstance( type )!;
                _projectors[attr.Kind] = instance;
                Log.Info( $"{LogPrefix} ProjectorRegistry: registered {type.Name} -> \"{attr.Kind}\"" );
            }
            catch ( Exception ex )
            {
                Log.Warning( $"{LogPrefix} ProjectorRegistry: failed to instantiate {type.FullName}: {ex.Message}" );
            }
        }

        Log.Info( $"{LogPrefix} ProjectorRegistry: {_projectors.Count} projector(s) registered." );
    }

    /// <summary>Returns the projector for the given kind. Throws <see cref="KeyNotFoundException"/> if unknown.</summary>
    public static IControlProjector For( string kind ) => _projectors[kind];

    public static bool Has( string kind ) => _projectors.ContainsKey( kind );

    public static IEnumerable<KeyValuePair<string, IControlProjector>> All => _projectors;
}

using System.Collections.Generic;
using Grains.RazorDesigner.Wiring;

namespace Grains.RazorDesigner.Projection.CSharp.Projectors;

public static class SymbolProjector
{
    public static void EmitAll( IReadOnlyWiring wiring, List<CSharpOp> ops, CSharpProjectorContext ctx )
    {
        // Collect each kind into typed buckets.
        var parameters = new List<ParameterSymbol>();
        var state      = new List<StateSymbol>();
        var nodeRefs   = new List<NodeRefSymbol>();
        var methods    = new List<MethodSymbol>();
        var lifecycles = new List<LifecycleSymbol>();

        foreach ( var sym in wiring.Symbols )
        {
            switch ( sym )
            {
                case ParameterSymbol p: parameters.Add( p ); break;
                case StateSymbol s:     state.Add( s );      break;
                case NodeRefSymbol nr:  nodeRefs.Add( nr );  break;
                case MethodSymbol m:    methods.Add( m );    break;
                case LifecycleSymbol l: lifecycles.Add( l ); break;
            }
        }

        // Sort within each group.
        parameters.Sort( ( a, b ) => System.StringComparer.Ordinal.Compare( a.Name, b.Name ) );
        state.Sort     ( ( a, b ) => System.StringComparer.Ordinal.Compare( a.Name, b.Name ) );
        nodeRefs.Sort  ( ( a, b ) => System.StringComparer.Ordinal.Compare( a.Name, b.Name ) );
        methods.Sort   ( ( a, b ) => System.StringComparer.Ordinal.Compare( a.Name, b.Name ) );
        lifecycles.Sort( ( a, b ) => ((int)a.Hook).CompareTo( (int)b.Hook ) );

        // Emit each group in canonical order.
        foreach ( var p  in parameters ) ParameterSymbolProjector.Emit( p,  ops, ctx );
        foreach ( var s  in state )      StateSymbolProjector    .Emit( s,  ops, ctx );
        foreach ( var nr in nodeRefs )   NodeRefSymbolProjector  .Emit( nr, ops, ctx );
        foreach ( var m  in methods )    MethodSymbolProjector   .Emit( m,  ops, ctx );
        foreach ( var l  in lifecycles ) LifecycleSymbolProjector.Emit( l,  ops, ctx, nodeRefs );
    }
}

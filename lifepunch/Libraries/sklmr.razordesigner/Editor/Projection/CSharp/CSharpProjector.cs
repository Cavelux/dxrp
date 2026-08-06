using System.Collections.Generic;
using Grains.RazorDesigner.Projection.CSharp.Projectors;

namespace Grains.RazorDesigner.Projection.CSharp;

public static class CSharpProjector
{
    public static CSharpResult Project(
        IReadOnlyWiring wiring,
        bool documentHasAnyBindings )
    {
        if ( wiring.Symbols.Count == 0 && !documentHasAnyBindings )
            return new CSharpResult( System.Array.Empty<CSharpOp>(), null );

        var ctx = new CSharpProjectorContext( wiring );
        var ops = new List<CSharpOp>( 64 );

        ops.Add( new HeaderBanner( wiring.ClassName, wiring.Namespace ) );
        ops.Add( new UsingDirective( "Sandbox" ) );
        ops.Add( new UsingDirective( "Sandbox.UI" ) );
        foreach ( var u in wiring.Usings )
            ops.Add( new UsingDirective( u ) );
        ops.Add( new NamespaceOpen( wiring.Namespace ) );
        ops.Add( new ClassOpen( wiring.ClassName, wiring.BaseClass ) );

        // Step 3: body. SymbolProjector handles grouping + sorting + per-kind dispatch.
        SymbolProjector.EmitAll( wiring, ops, ctx );

        ops.Add( new ClassClose() );

        var source = CSharpApplier.Apply( ops ).Replace( "\r\n", "\n" );

        return new CSharpResult( ops, source );
    }
}

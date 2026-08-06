using System.Collections.Generic;
using Grains.RazorDesigner.Wiring;

namespace Grains.RazorDesigner.Projection.CSharp.Projectors;

public static class ParameterSymbolProjector
{
    public static void Emit( ParameterSymbol s, List<CSharpOp> ops, CSharpProjectorContext ctx )
    {
        var initial = s.Initial is null ? "default" : ExpressionEmitter.Emit( s.Initial, ctx );
        ops.Add( new FieldDecl(
            Visibility: "public", Type: s.Type, Name: s.Name,
            InitialExpr: initial, IsParameter: true ) );
    }
}

using System.Collections.Generic;
using Grains.RazorDesigner.Wiring;

namespace Grains.RazorDesigner.Projection.CSharp.Projectors;

public static class NodeRefSymbolProjector
{
    public static void Emit( NodeRefSymbol s, List<CSharpOp> ops, CSharpProjectorContext ctx )
    {
        ops.Add( new FieldDecl(
            Visibility: "private", Type: s.Type, Name: s.Name,
            InitialExpr: "default", IsParameter: false ) );
    }
}

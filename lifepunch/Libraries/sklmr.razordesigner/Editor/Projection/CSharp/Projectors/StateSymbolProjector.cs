using System.Collections.Generic;
using Grains.RazorDesigner.Wiring;

namespace Grains.RazorDesigner.Projection.CSharp.Projectors;

public static class StateSymbolProjector
{
    public static void Emit( StateSymbol s, List<CSharpOp> ops, CSharpProjectorContext ctx )
    {
        var visibility = VisibilityToString( s.Visibility );
        var initial = s.Initial is null ? "default" : ExpressionEmitter.Emit( s.Initial, ctx );
        ops.Add( new FieldDecl(
            Visibility: visibility, Type: s.Type, Name: s.Name,
            InitialExpr: initial,
            IsParameter: false,
            IsProperty: s.Visibility == SymbolVisibility.Public ) );
    }

    private static string VisibilityToString( SymbolVisibility v )
    {
        switch ( v )
        {
            case SymbolVisibility.Private:   return "private";
            case SymbolVisibility.Public:    return "public";
            case SymbolVisibility.Internal:  return "internal";
            case SymbolVisibility.Protected: return "protected";
            default:                         return "private";
        }
    }
}

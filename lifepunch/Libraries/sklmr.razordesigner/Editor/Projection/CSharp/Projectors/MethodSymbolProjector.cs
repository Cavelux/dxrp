using System.Collections.Generic;
using System.Linq;
using Grains.RazorDesigner.Wiring;

namespace Grains.RazorDesigner.Projection.CSharp.Projectors;

public static class MethodSymbolProjector
{
    public static void Emit( MethodSymbol s, List<CSharpOp> ops, CSharpProjectorContext ctx )
    {
        ctx.PushMethodScope();
        var bodyOps = new List<CSharpOp>();
        foreach ( var stmt in s.Body )
            ActionEmitter.Emit( stmt, bodyOps, ctx );
        var isAsync = ctx.PopMethodScope();

        var visibility  = VisibilityToString( s.Visibility );
        var retType     = ResolveAsyncReturnType( s.ReturnType, isAsync );
        var paramList   = string.Join( ", ", s.Parameters.Select( p => $"{p.Type} {p.Name}" ) );

        ops.Add( new MethodOpen(
            Visibility: visibility, IsOverride: false, IsAsync: isAsync,
            ReturnType: retType, Name: s.Name, ParameterList: paramList ) );
        ops.AddRange( bodyOps );
        ops.Add( new MethodClose() );
    }

    private static string ResolveAsyncReturnType( string declaredType, bool isAsync )
    {
        if ( !isAsync ) return declaredType;
        if ( declaredType == "void" ) return "void";
        if ( declaredType == "Task" ) return "System.Threading.Tasks.Task";
        return declaredType;
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

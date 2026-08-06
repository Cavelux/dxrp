using System.Linq;
using System.Text.Json;
using Grains.RazorDesigner.Wiring;

namespace Grains.RazorDesigner.Projection.CSharp.Projectors;

public static class ExpressionEmitter
{
    public static string Emit( Expression expr, CSharpProjectorContext ctx )
    {
        switch ( expr )
        {
            case LiteralExpression lit:        return EmitLiteral( lit );
            case SymbolRefExpression sref:     return EmitSymbolRef( sref, ctx );
            case BinaryOpExpression bop:       return EmitBinaryOp( bop, ctx );
            case UnaryOpExpression uop:        return EmitUnaryOp( uop, ctx );
            case ConditionalExpression cond:   return EmitConditional( cond, ctx );
            case MethodCallExpression mc:      return EmitMethodCall( mc, ctx );
            case MemberAccessExpression ma:    return EmitMemberAccess( ma, ctx );
            case InlineExpression inl:         return inl.Code ?? "";
            default:
                throw new System.Diagnostics.UnreachableException(
                    $"ExpressionEmitter: unhandled Expression variant '{expr?.GetType().Name}'. " +
                    "Add an arm here AND add the leaf to Expression.cs's JsonDerivedType list in the same commit." );
        }
    }

    // Public so ActionEmitter (Task 4) can call it for CallAction's callee without duplication.
    public static string EmitCallee( CalleeRef callee, CSharpProjectorContext ctx )
    {
        switch ( callee )
        {
            case MethodCallee mc:
            {
                if ( ctx.Symbols.TryGetValue( mc.SymbolId, out var symbol ) )
                {
                    if ( symbol is MethodSymbol )
                        return symbol.Name;
                    return symbol.Name + " /* not-a-method */";
                }
                return $"/* missing callee {mc.SymbolId} */";
            }
            case InlineCallee ic:
                return ic.Code ?? "";
            case null:
                return "/* null callee */";
            default:
                throw new System.Diagnostics.UnreachableException(
                    $"ExpressionEmitter.EmitCallee: unhandled CalleeRef variant '{callee?.GetType().Name}'." );
        }
    }


    private static string EmitLiteral( LiteralExpression lit )
    {
        // TypeId lives on the base Expression record, read from lit directly (inherited property).
        switch ( lit.TypeId )
        {
            case "string":
            {
                if ( lit.Value.ValueKind == JsonValueKind.Null )
                    return "null";
                var raw = lit.Value.ValueKind == JsonValueKind.Undefined ? null : lit.Value.GetString();
                return "\"" + EscapeStringLiteral( raw ) + "\"";
            }
            case "int":
                return lit.Value.GetInt32().ToString( System.Globalization.CultureInfo.InvariantCulture );
            case "float":
                return lit.Value.GetDouble().ToString( "R", System.Globalization.CultureInfo.InvariantCulture ) + "f";
            case "bool":
                return lit.Value.GetBoolean() ? "true" : "false";
            default:
                return lit.Value.ValueKind == JsonValueKind.Undefined ? "" : lit.Value.ToString();
        }
    }

    private static string EmitSymbolRef( SymbolRefExpression sref, CSharpProjectorContext ctx )
    {
        if ( ctx.Symbols.TryGetValue( sref.SymbolId, out var symbol ) )
            return symbol.Name;

        return $"/* missing symbol {sref.SymbolId} */";
    }

    private static string EmitBinaryOp( BinaryOpExpression bop, CSharpProjectorContext ctx )
    {
        return "(" + Emit( bop.Left, ctx ) + " " + BinaryOpToken( bop.Op ) + " " + Emit( bop.Right, ctx ) + ")";
    }

    private static string EmitUnaryOp( UnaryOpExpression uop, CSharpProjectorContext ctx )
    {
        return "(" + UnaryOpToken( uop.Op ) + Emit( uop.Operand, ctx ) + ")";
    }

    private static string EmitConditional( ConditionalExpression cond, CSharpProjectorContext ctx )
    {
        return "(" + Emit( cond.Condition, ctx ) + " ? " + Emit( cond.Then, ctx ) + " : " + Emit( cond.Else, ctx ) + ")";
    }

    private static string EmitMethodCall( MethodCallExpression mc, CSharpProjectorContext ctx )
    {
        string prefix = "";
        if ( mc.Await )
        {
            ctx.RecordAwait();
            prefix = "await ";
        }

        var arglist = string.Join( ", ", mc.Args.Select( a => Emit( a, ctx ) ) );
        return prefix + EmitCallee( mc.Callee, ctx ) + "(" + arglist + ")";
    }

    private static string EmitMemberAccess( MemberAccessExpression ma, CSharpProjectorContext ctx )
    {
        // Trust the member name — the validator's surface; we don't need to escape it.
        return Emit( ma.Receiver, ctx ) + "." + ma.Member;
    }


    private static string EscapeStringLiteral( string s )
    {
        if ( s is null || s.Length == 0 ) return "";
        return s
            .Replace( "\\", "\\\\" )
            .Replace( "\"", "\\\"" )
            .Replace( "\n", "\\n" )
            .Replace( "\r", "\\r" )
            .Replace( "\t", "\\t" );
    }

    private static string BinaryOpToken( BinaryOp op )
    {
        switch ( op )
        {
            case BinaryOp.Eq:    return "==";
            case BinaryOp.NotEq: return "!=";
            case BinaryOp.Lt:    return "<";
            case BinaryOp.LtEq:  return "<=";
            case BinaryOp.Gt:    return ">";
            case BinaryOp.GtEq:  return ">=";
            case BinaryOp.And:   return "&&";
            case BinaryOp.Or:    return "||";
            case BinaryOp.Add:   return "+";
            case BinaryOp.Sub:   return "-";
            case BinaryOp.Mul:   return "*";
            case BinaryOp.Div:   return "/";
            case BinaryOp.Mod:   return "%";
            default:
                throw new System.Diagnostics.UnreachableException(
                    $"ExpressionEmitter.BinaryOpToken: unhandled BinaryOp variant '{op}'. " +
                    "Add a token arm here whenever a new BinaryOp value is added." );
        }
    }

    private static string UnaryOpToken( UnaryOp op )
    {
        switch ( op )
        {
            case UnaryOp.Not:    return "!";
            case UnaryOp.Negate: return "-";
            default:
                throw new System.Diagnostics.UnreachableException(
                    $"ExpressionEmitter.UnaryOpToken: unhandled UnaryOp variant '{op}'. " +
                    "Add a token arm here whenever a new UnaryOp value is added." );
        }
    }
}

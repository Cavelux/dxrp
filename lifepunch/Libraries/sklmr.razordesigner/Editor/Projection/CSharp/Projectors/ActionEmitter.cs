using System.Collections.Generic;
using System.Linq;
using Grains.RazorDesigner.Wiring;

namespace Grains.RazorDesigner.Projection.CSharp.Projectors;

public static class ActionEmitter
{
    public static void Emit( Grains.RazorDesigner.Wiring.Action action, List<CSharpOp> ops, CSharpProjectorContext ctx )
    {
        switch ( action )
        {
            case SetAction set:
                ops.Add( new Statement(
                    EmitTarget( set.Target, ctx ) + " = " + ExpressionEmitter.Emit( set.Value, ctx ) ) );
                break;

            case CallAction call:
                var prefix = call.Await ? "await " : "";
                if ( call.Await ) ctx.RecordAwait();
                var callee = ExpressionEmitter.EmitCallee( call.Callee, ctx );
                var args   = string.Join( ", ", call.Args.Select( a => ExpressionEmitter.Emit( a, ctx ) ) );
                ops.Add( new Statement( prefix + callee + "(" + args + ")" ) );
                break;

            case IfAction iff:
                ops.Add( new BlockOpen( "if ( " + ExpressionEmitter.Emit( iff.Condition, ctx ) + " )" ) );
                foreach ( var inner in iff.Then ) Emit( inner, ops, ctx );
                ops.Add( new BlockClose() );
                if ( iff.Else != null && iff.Else.Count > 0 )
                {
                    ops.Add( new BlockOpen( "else" ) );
                    foreach ( var inner in iff.Else ) Emit( inner, ops, ctx );
                    ops.Add( new BlockClose() );
                }
                break;

            case StateHasChangedAction:
                ops.Add( new Statement( "StateHasChanged()" ) );
                break;

            case LogAction log:
                var method = log.Level switch
                {
                    LogLevel.Info    => "Log.Info",
                    LogLevel.Warning => "Log.Warning",
                    LogLevel.Error   => "Log.Error",
                    _                => "Log.Info",
                };
                ops.Add( new Statement( method + "( " + ExpressionEmitter.Emit( log.Message, ctx ) + " )" ) );
                break;

            case ReturnAction ret:
                if ( ret.Value is null )
                    ops.Add( new Statement( "return" ) );
                else
                    ops.Add( new Statement( "return " + ExpressionEmitter.Emit( ret.Value, ctx ) ) );
                break;

            case InlineAction inl:
                foreach ( var rawLine in (inl.Code ?? "").Split( '\n' ) )
                {
                    var afterCarriageReturn = rawLine.TrimEnd( '\r' );
                    var withoutSemicolon = afterCarriageReturn.EndsWith( ";" )
                        ? afterCarriageReturn[..^1]
                        : afterCarriageReturn;
                    var cleaned = withoutSemicolon.TrimEnd();
                    if ( string.IsNullOrWhiteSpace( cleaned ) ) continue;
                    ops.Add( new Statement( cleaned ) );
                }
                break;

            default:
                throw new System.Diagnostics.UnreachableException(
                    $"ActionEmitter: unhandled Action variant '{action?.GetType().Name}'. " +
                    "Add an arm here AND add the leaf to Action.cs's JsonDerivedType list in the same commit." );
        }
    }


    private static string EmitTarget( TargetRef target, CSharpProjectorContext ctx )
    {
        switch ( target )
        {
            case SymbolTarget sym:
                if ( ctx.Symbols.TryGetValue( sym.SymbolId, out var symbol ) )
                    return symbol.Name;
                return $"/* missing symbol {sym.SymbolId} */";

            case null:
                return "/* null target */";

            default:
                throw new System.Diagnostics.UnreachableException(
                    $"ActionEmitter.EmitTarget: unhandled TargetRef variant '{target.GetType().Name}'. " +
                    "Add an arm here AND add the leaf to TargetRef.cs's JsonDerivedType list in the same commit." );
        }
    }
}

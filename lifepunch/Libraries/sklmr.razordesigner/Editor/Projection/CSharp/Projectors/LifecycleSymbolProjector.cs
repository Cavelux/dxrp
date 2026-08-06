using System.Collections.Generic;
using Grains.RazorDesigner.Wiring;

namespace Grains.RazorDesigner.Projection.CSharp.Projectors;

public static class LifecycleSymbolProjector
{
    public static void Emit(
        LifecycleSymbol s,
        List<CSharpOp> ops,
        CSharpProjectorContext ctx,
        IReadOnlyList<NodeRefSymbol> nodeRefs )
    {
        var (declaredReturn, paramList, hookName, defaultsToAsync) = HookSignature( s.Hook );

        ctx.PushMethodScope();
        var bodyOps = new List<CSharpOp>();

        if ( s.Hook == LifecycleHook.OnAfterTreeRender )
        {
            foreach ( var nr in nodeRefs )
            {
                bodyOps.Add( new Statement(
                    $"{nr.Name} = Find<{nr.Type}>( \"[data-grd-node-id='{nr.TargetNodeId}']\" )" ) );
            }
        }

        // User body
        foreach ( var stmt in s.Body )
            ActionEmitter.Emit( stmt, bodyOps, ctx );

        var bodyDeducedAsync = ctx.PopMethodScope();

        var isAsync = bodyDeducedAsync || defaultsToAsync;
        var retType = ResolveAsyncReturnType( declaredReturn, isAsync );

        ops.Add( new MethodOpen(
            Visibility: "protected",
            IsOverride: true,
            IsAsync: isAsync,
            ReturnType: retType,
            Name: hookName,
            ParameterList: paramList ) );
        ops.AddRange( bodyOps );
        ops.Add( new MethodClose() );
    }

    private static (string ReturnType, string ParamList, string HookName, bool DefaultsToAsync)
        HookSignature( LifecycleHook hook )
    {
        switch ( hook )
        {
            case LifecycleHook.OnParametersSet:
                return ("void", "", "OnParametersSet", false);
            case LifecycleHook.OnParametersSetAsync:
                return ("System.Threading.Tasks.Task", "", "OnParametersSetAsync", true);
            case LifecycleHook.OnAfterTreeRender:
                return ("void", "bool firstTime", "OnAfterTreeRender", false);
            case LifecycleHook.OnUpdate:
                return ("void", "", "OnUpdate", false);
            case LifecycleHook.BuildHash:
                return ("int", "", "BuildHash", false);
            default:
                throw new System.Diagnostics.UnreachableException(
                    $"LifecycleSymbolProjector.HookSignature: unhandled LifecycleHook variant '{hook}'. " +
                    "Add an arm here whenever a new LifecycleHook value is added." );
        }
    }

    private static string ResolveAsyncReturnType( string declaredType, bool isAsync )
    {
        if ( !isAsync ) return declaredType;
        if ( declaredType == "void" ) return "void";
        if ( declaredType == "Task" ) return "System.Threading.Tasks.Task";
        return declaredType;
    }
}

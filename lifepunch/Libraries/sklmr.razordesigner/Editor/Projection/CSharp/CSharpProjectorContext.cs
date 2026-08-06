using System.Collections.Generic;
using Grains.RazorDesigner.Wiring;
using Sandbox;

namespace Grains.RazorDesigner.Projection.CSharp;

public sealed class CSharpProjectorContext
{
    public IReadOnlyDictionary<System.Guid, Symbol> Symbols { get; }
    public string ClassName { get; }

    private readonly Stack<bool> _asyncScopeStack = new();

    public CSharpProjectorContext( IReadOnlyWiring wiring )
    {
        ClassName = wiring.ClassName;

        var dict = new Dictionary<System.Guid, Symbol>();
        foreach ( var symbol in wiring.Symbols )
        {
            if ( !dict.ContainsKey( symbol.Id ) )
                dict[symbol.Id] = symbol;
        }
        Symbols = dict;
    }

    // Begins a new method scope. The scope starts with "no awaits encountered".
    public void PushMethodScope() => _asyncScopeStack.Push( false );

    public void RecordAwait()
    {
        if ( _asyncScopeStack.Count == 0 )
        {
            Log.Warning( "[Grains.RazorDesigner] CSharpProjectorContext.RecordAwait called with no open method scope — caller bug; silently ignoring." );
            return;
        }

        _asyncScopeStack.Pop();
        _asyncScopeStack.Push( true );
    }

    public bool PopMethodScope()
    {
        if ( _asyncScopeStack.Count == 0 )
        {
            Log.Warning( "[Grains.RazorDesigner] CSharpProjectorContext.PopMethodScope called with no open method scope — caller bug; returning false." );
            return false;
        }

        return _asyncScopeStack.Pop();
    }
}

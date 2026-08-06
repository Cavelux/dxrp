using System.Collections.Generic;
using System.Linq;
using Grains.RazorDesigner.Wiring;

namespace Grains.RazorDesigner.CodeDialog;

public sealed class CodeDialogEditBuffer
{
    public List<StateSymbol> States { get; }
    public List<ParameterSymbol> Parameters { get; }
    public IReadOnlyList<Symbol> OtherSymbols { get; }

    public string Namespace { get; set; }
    public string ClassName { get; set; }
    public string BaseClass { get; set; }
    public IReadOnlyList<string> Usings { get; set; }

    public CodeDialogEditBuffer( WiringEnvelope source )
    {
        States = new List<StateSymbol>(
            source.Symbols.OfType<StateSymbol>() );
        Parameters = new List<ParameterSymbol>(
            source.Symbols.OfType<ParameterSymbol>() );
        OtherSymbols = source.Symbols
            .Where( s => s is not StateSymbol and not ParameterSymbol )
            .ToArray();
        Namespace = source.Namespace;
        ClassName = source.ClassName;
        BaseClass = source.BaseClass;
        Usings = source.Usings;
    }

    public WiringEnvelope ToEnvelope()
    {
        var symbols = new List<Symbol>( Parameters.Count + States.Count + OtherSymbols.Count );
        symbols.AddRange( Parameters );
        symbols.AddRange( States );
        symbols.AddRange( OtherSymbols );

        return new WiringEnvelope
        {
            Namespace = Namespace ?? "",
            ClassName = ClassName ?? "",
            BaseClass = string.IsNullOrEmpty( BaseClass ) ? "PanelComponent" : BaseClass,
            Symbols = symbols,
            Usings = Usings ?? System.Array.Empty<string>(),
        };
    }
}

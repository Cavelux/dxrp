using System.Collections.Generic;
using Grains.RazorDesigner.Wiring;

namespace Grains.RazorDesigner.Projection.CSharp;

public interface IReadOnlyWiring
{
    string Namespace { get; }
    string ClassName { get; }
    string BaseClass { get; }
    IReadOnlyList<Symbol> Symbols { get; }
    IReadOnlyList<string> Usings { get; }
}

public sealed class WiringEnvelopeView : IReadOnlyWiring
{
    public WiringEnvelopeView( WiringEnvelope env, string namespaceFallback, string classNameFallback )
    {
        Namespace = string.IsNullOrEmpty( env.Namespace ) ? namespaceFallback : env.Namespace;
        ClassName = string.IsNullOrEmpty( env.ClassName ) ? classNameFallback : env.ClassName;
        BaseClass = env.BaseClass;
        Symbols   = env.Symbols;
        Usings    = env.Usings;
    }

    public string Namespace { get; }
    public string ClassName { get; }
    public string BaseClass { get; }
    public IReadOnlyList<Symbol> Symbols { get; }
    public IReadOnlyList<string> Usings { get; }
}

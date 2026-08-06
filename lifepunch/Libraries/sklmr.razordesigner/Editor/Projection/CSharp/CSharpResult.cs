using System.Collections.Generic;

namespace Grains.RazorDesigner.Projection.CSharp;

public readonly record struct CSharpResult( IReadOnlyList<CSharpOp> Ops, string Source );

using System;
using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Wiring;

[JsonPolymorphic( TypeDiscriminatorPropertyName = "$type" )]
[JsonDerivedType( typeof( LiteralExpression ),       "Literal" )]
[JsonDerivedType( typeof( SymbolRefExpression ),     "SymbolRef" )]
[JsonDerivedType( typeof( BinaryOpExpression ),      "BinaryOp" )]
[JsonDerivedType( typeof( UnaryOpExpression ),       "UnaryOp" )]
[JsonDerivedType( typeof( ConditionalExpression ),   "Conditional" )]
[JsonDerivedType( typeof( MethodCallExpression ),    "MethodCall" )]
[JsonDerivedType( typeof( MemberAccessExpression ),  "MemberAccess" )]
[JsonDerivedType( typeof( InlineExpression ),        "Inline" )]
public abstract record Expression
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string TypeId { get; init; }
}

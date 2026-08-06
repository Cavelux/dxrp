namespace Grains.RazorDesigner.Projection.CSharp;

public abstract record CSharpOp;

// File-level scaffold
public sealed record HeaderBanner( string ClassName, string Namespace ) : CSharpOp;
public sealed record UsingDirective( string Namespace ) : CSharpOp;
public sealed record NamespaceOpen( string Namespace ) : CSharpOp;
public sealed record ClassOpen( string ClassName, string BaseClass ) : CSharpOp;
public sealed record ClassClose() : CSharpOp;

public sealed record FieldDecl(
    string Visibility, string Type, string Name, string InitialExpr,
    bool IsParameter, bool IsProperty = false ) : CSharpOp;

public sealed record MethodOpen(
    string Visibility, bool IsOverride, bool IsAsync,
    string ReturnType, string Name, string ParameterList ) : CSharpOp;

public sealed record MethodClose() : CSharpOp;

// Body-level
public sealed record Statement( string Code ) : CSharpOp;          // single `;`-terminated line
public sealed record BlockOpen( string Header ) : CSharpOp;        // e.g. `if ( <cond> )` — applier writes "<header> {\n" and indents
public sealed record BlockClose() : CSharpOp;
public sealed record BlankLine() : CSharpOp;
public sealed record Comment( string Text ) : CSharpOp;            // `// <text>`

using System;
using System.Text.Json.Serialization;
using Grains.RazorDesigner.Projection.CSharp;

namespace Grains.RazorDesigner.Projection.Tests;

public sealed class CSharpOpDto
{
    [JsonPropertyName( "$type" )]
    public string Type { get; set; } = "";

    // HeaderBanner / ClassOpen
    [JsonPropertyName( "className" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
    public string ClassName { get; set; }

    // HeaderBanner / NamespaceOpen / UsingDirective
    [JsonPropertyName( "namespace" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
    public string Namespace { get; set; }

    // ClassOpen
    [JsonPropertyName( "baseClass" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
    public string BaseClass { get; set; }

    // FieldDecl
    [JsonPropertyName( "visibility" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
    public string Visibility { get; set; }

    // FieldDecl / MethodOpen
    [JsonPropertyName( "returnType" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
    public string ReturnType { get; set; }

    // FieldDecl
    [JsonPropertyName( "fieldType" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
    public string FieldType { get; set; }

    // FieldDecl / MethodOpen
    [JsonPropertyName( "name" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
    public string Name { get; set; }

    // FieldDecl
    [JsonPropertyName( "initialExpr" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
    public string InitialExpr { get; set; }

    [JsonPropertyName( "isParameter" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
    public bool IsParameter { get; set; }

    [JsonPropertyName( "isProperty" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
    public bool IsProperty { get; set; }

    // MethodOpen
    [JsonPropertyName( "isOverride" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
    public bool IsOverride { get; set; }

    // MethodOpen
    [JsonPropertyName( "isAsync" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
    public bool IsAsync { get; set; }

    // MethodOpen
    [JsonPropertyName( "parameterList" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
    public string ParameterList { get; set; }

    [JsonPropertyName( "code" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
    public string Code { get; set; }

    // Comment
    [JsonPropertyName( "text" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
    public string Text { get; set; }
}

public static class CSharpOpMapping
{
    public static CSharpOpDto FromOp( CSharpOp op )
    {
        switch ( op )
        {
            case HeaderBanner h:
                return new CSharpOpDto { Type = "HeaderBanner", ClassName = h.ClassName, Namespace = h.Namespace };
            case UsingDirective u:
                return new CSharpOpDto { Type = "UsingDirective", Namespace = u.Namespace };
            case NamespaceOpen n:
                return new CSharpOpDto { Type = "NamespaceOpen", Namespace = n.Namespace };
            case ClassOpen c:
                return new CSharpOpDto { Type = "ClassOpen", ClassName = c.ClassName, BaseClass = c.BaseClass };
            case ClassClose:
                return new CSharpOpDto { Type = "ClassClose" };
            case FieldDecl f:
                return new CSharpOpDto
                {
                    Type        = "FieldDecl",
                    Visibility  = f.Visibility,
                    FieldType   = f.Type,
                    Name        = f.Name,
                    InitialExpr = f.InitialExpr,
                    IsParameter = f.IsParameter,
                    IsProperty  = f.IsProperty,
                };
            case MethodOpen m:
                return new CSharpOpDto
                {
                    Type          = "MethodOpen",
                    Visibility    = m.Visibility,
                    IsOverride    = m.IsOverride,
                    IsAsync       = m.IsAsync,
                    ReturnType    = m.ReturnType,
                    Name          = m.Name,
                    ParameterList = m.ParameterList,
                };
            case MethodClose:
                return new CSharpOpDto { Type = "MethodClose" };
            case Statement s:
                return new CSharpOpDto { Type = "Statement", Code = s.Code };
            case BlockOpen b:
                return new CSharpOpDto { Type = "BlockOpen", Code = b.Header };
            case BlockClose:
                return new CSharpOpDto { Type = "BlockClose" };
            case BlankLine:
                return new CSharpOpDto { Type = "BlankLine" };
            case Comment cm:
                return new CSharpOpDto { Type = "Comment", Text = cm.Text };
            default:
                throw new InvalidOperationException(
                    $"CSharpOpMapping.FromOp: unknown CSharpOp variant '{op?.GetType().Name}'. " +
                    "Add an arm here AND update CSharpApplier's switch in the same commit." );
        }
    }

    public static CSharpOp ToOp( CSharpOpDto dto )
    {
        switch ( dto.Type )
        {
            case "HeaderBanner":   return new HeaderBanner( dto.ClassName ?? "", dto.Namespace ?? "" );
            case "UsingDirective": return new UsingDirective( dto.Namespace ?? "" );
            case "NamespaceOpen":  return new NamespaceOpen( dto.Namespace ?? "" );
            case "ClassOpen":      return new ClassOpen( dto.ClassName ?? "", dto.BaseClass ?? "" );
            case "ClassClose":     return new ClassClose();
            case "FieldDecl":      return new FieldDecl(
                dto.Visibility ?? "", dto.FieldType ?? "", dto.Name ?? "",
                dto.InitialExpr ?? "", dto.IsParameter, dto.IsProperty );
            case "MethodOpen":     return new MethodOpen(
                dto.Visibility ?? "", dto.IsOverride, dto.IsAsync,
                dto.ReturnType ?? "", dto.Name ?? "", dto.ParameterList ?? "" );
            case "MethodClose":    return new MethodClose();
            case "Statement":      return new Statement( dto.Code ?? "" );
            case "BlockOpen":      return new BlockOpen( dto.Code ?? "" );
            case "BlockClose":     return new BlockClose();
            case "BlankLine":      return new BlankLine();
            case "Comment":        return new Comment( dto.Text ?? "" );
            default:
                throw new InvalidOperationException(
                    $"CSharpOpMapping.ToOp: unknown $type '{dto.Type}'. " +
                    "Author the fixture against an existing CSharpOp leaf, or add the new leaf to both CSharpOp.cs and this mapper." );
        }
    }
}

using System;

namespace Grains.RazorDesigner.Projection;

[AttributeUsage( AttributeTargets.Class )]
public sealed class ProjectorAttribute : Attribute
{
    public string Kind { get; }
    public ProjectorAttribute( string kind ) { Kind = kind; }
}

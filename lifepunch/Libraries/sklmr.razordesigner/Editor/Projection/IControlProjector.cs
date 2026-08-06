namespace Grains.RazorDesigner.Projection;

public interface IControlProjector
{
    string Kind { get; }   // matches ControlType.ToString()
    ProjectionResult Project( IReadOnlyNode node, IAppearance appearance, IPayload payload, ProjectionContext ctx );
}

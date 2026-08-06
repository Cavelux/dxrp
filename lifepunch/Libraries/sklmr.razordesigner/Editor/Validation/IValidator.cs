using System.Collections.Generic;
using Grains.RazorDesigner.Projection;

namespace Grains.RazorDesigner.Validation;

public interface IValidator
{
	System.Collections.Generic.IReadOnlyList<ValidationDiagnostic> Validate( IReadOnlyNode root );
}

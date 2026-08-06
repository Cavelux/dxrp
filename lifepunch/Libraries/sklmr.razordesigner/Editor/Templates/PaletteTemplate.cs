using System.Collections.Generic;
using Grains.RazorDesigner.Document;

namespace Grains.RazorDesigner.Templates;

public sealed record PaletteTemplate(
	string Name,
	string IconName,
	bool WrappedInContainer,
	IReadOnlyList<ControlRecord> Roots,
	string FilePath );

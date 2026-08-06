using Grains.RazorDesigner.Serialization; // PreviewTheme

namespace Grains.RazorDesigner.Projection;

public readonly record struct ProjectionContext(
    PreviewTheme Theme,   // swappable preview theme; PreviewTheme.Default for save
    bool ForPreview );    // preview emits .preview-* chrome classes; save does not

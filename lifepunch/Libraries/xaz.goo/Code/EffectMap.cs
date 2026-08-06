using System.Collections.Generic;

namespace Goo;

/// <summary>A host-level <c>{ tag -> ShaderEffect }</c> manifest. A Blob with a matching
/// <see cref="Container.Tag"/> resolves its effect from here unless it sets <c>Effect</c> inline.</summary>
public sealed class EffectMap : Dictionary<string, ShaderEffect>
{
    /// <summary>The shared empty manifest. Hosts that target no tags return this.</summary>
    public static readonly EffectMap Empty = new();
}

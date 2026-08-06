namespace Goo;

/// <summary>How a <see cref="ShaderEffect"/> grabs the framebuffer behind its panel before drawing.</summary>
public enum GrabMode
{
    /// <summary>No grab. For procedural or self-contained shaders that do not read the backdrop.</summary>
    None,
    /// <summary>Grab the raw framebuffer (FrameBufferCopyTexture). For shaders that sample the backdrop sharp.</summary>
    Sharp,
    /// <summary>Grab a gaussian-blurred framebuffer mip chain. For backdrop-blur effects (frosted glass, aero).</summary>
    Blurred,
}

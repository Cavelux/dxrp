using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

/// <summary>ActionGraph signature for hierarchy-authored Goo pointer events.</summary>
public delegate void GooPointerAction( BlobNode source, Vector2 localPosition, MouseButtons button );

/// <summary>ActionGraph signature for mouse-wheel events. Delta y is positive when scrolling down.</summary>
public delegate void GooWheelAction( BlobNode source, Vector2 delta );

/// <summary>ActionGraph signature for TextEntry events that provide the current text.</summary>
public delegate void GooTextValueAction( TextEntryNode source, string value );

/// <summary>ActionGraph signature for TextEntry events without an additional value.</summary>
public delegate void GooTextAction( TextEntryNode source );

/// <summary>ActionGraph signature for TextEntry validation changes.</summary>
public delegate void GooTextValidationAction( TextEntryNode source, bool isValid );

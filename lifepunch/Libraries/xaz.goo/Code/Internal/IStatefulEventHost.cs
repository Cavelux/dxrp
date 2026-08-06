using System;

namespace Goo.Internal;

internal interface IStatefulEventHost
{
    void ApplyEvents(in BlobEvents events);
    bool UserSetPointerEvents { get; set; }
    bool HasEventHandlers { get; }

    /// <summary>Set by the Applier: invoked after any user handler runs so the panel
    /// auto-rebuilds. Null when the owning GooPanel opts out (AutoRebuildOnEvents == false).</summary>
    Action? RequestRebuild { set; }
}

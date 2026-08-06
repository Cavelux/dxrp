using System.Collections.Generic;
using Grains.RazorDesigner.Document;

namespace Grains.RazorDesigner.Contracts;

public interface IContractTable
{
    ControlContract Get( ControlType kind );

    // Enumerate all contracts.
    IEnumerable<ControlContract> All { get; }
}

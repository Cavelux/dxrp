namespace Goo.Internal;

// The engine paints siblings per parent in ascending SiblingIndex + ZIndex order,
// so an unscaled declared z is out-bid by any sibling declared far enough later.
// Scaling by more than any realistic child count makes a declared z dominate
// document order outright: distinct values never tie, equal values resolve by
// sibling index, negatives sort behind. Undeclared (null) passes through.
internal static class ZIndexScaling
{
    internal const int Factor = 4096;

    internal static int? Scale(int? declared) => declared * Factor;
}

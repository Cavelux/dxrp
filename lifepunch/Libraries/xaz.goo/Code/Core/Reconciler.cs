using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Goo;

public readonly record struct DiffResult(IReadOnlyList<Op> Ops, IReadOnlyList<string> Warnings);

// Lazy-rented diff output buffers. Identical-tree iterations never add an Op or warning,
// so we don't even touch the pool on the hot path. The pooled lists outlive Reconciler.Diff
// and are read by the caller; BuildContext.ReturnAll returns them after Applier.Apply runs.
internal struct DiffBuffers
{
    public List<Op>? Ops;
    public List<string>? Warnings;

    public void AddOp(Op op) => (Ops ??= BuildContext.Current.RentOpList()).Add(op);
    public void AddWarning(string warning) => (Warnings ??= BuildContext.Current.RentWarningList()).Add(warning);
}

public static class Reconciler
{
    internal static DiffResult Diff<TRoot>(ref Fiber? rootFiber, in TRoot intentRoot)
        where TRoot : struct, IBlob
    {
        Frame rootFrame = default;
        intentRoot.WriteTo(ref rootFrame);  // direct call, monomorphized - no cast, no boxing
        return DiffFromFrame(ref rootFiber, ref rootFrame);
    }

    // Non-generic entry for callers holding a boxed root (e.g. GooView's Func<IBlob>).
    // Costs one interface dispatch + the caller's single root box; children stay unboxed in DiffNode.
    internal static DiffResult Diff(ref Fiber? rootFiber, IBlob intentRoot)
    {
        Frame rootFrame = default;
        intentRoot.WriteTo(ref rootFrame);  // interface dispatch on the boxed root
        return DiffFromFrame(ref rootFiber, ref rootFrame);
    }

    // Shared tail. WriteTo runs before this (it only populates the root Frame from the struct's
    // already-built Children; it touches neither the host-path pool nor the op buffers), so moving
    // it ahead of BeginDiff/RentPath is order-safe.
    static DiffResult DiffFromFrame(ref Fiber? rootFiber, ref Frame rootFrame)
    {
        DiffBuffers bufs = default;
        var ctx = BuildContext.Current;
        ctx.BeginDiff();
        var path = ctx.RentPath();

        try
        {
            DiffNode(ref rootFiber, ref rootFrame, 0, path, ref bufs);
            return new DiffResult(
                bufs.Ops ?? (IReadOnlyList<Op>)Array.Empty<Op>(),
                bufs.Warnings ?? (IReadOnlyList<string>)Array.Empty<string>());
        }
        finally
        {
            ctx.ReturnPath(path);
        }
    }

    static void DiffNode(ref Fiber? fiber, ref Frame frame, int index, List<int> path, ref DiffBuffers bufs)
    {
        if (frame.Kind == BlobKind.Cell)
        {
            DiffCell(ref fiber, ref frame, index, path, ref bufs);
            return;
        }

        if (fiber is null)
        {
            fiber = AllocateFiberFor(in frame);
            EmitCreate(in frame, fiber, index, path, ref bufs);
            return;
        }

        if (fiber.Kind != frame.Kind)
        {
            var snap = Snapshot(path);
            bufs.AddOp(Op.RemoveAt(index, snap));
            TeardownTree(fiber);
            fiber = AllocateFiberFor(in frame);
            EmitCreate(in frame, fiber, index, path, ref bufs);
            return;
        }

        switch (frame.Kind)
        {
            case BlobKind.Text:
                if (fiber.Content != frame.Content || fiber.IsRich != frame.IsRich)
                {
                    bufs.AddOp(Op.UpdateText(index, RenderText(frame.Content, frame.IsRich), Snapshot(path), frame.IsRich));
                    fiber.Content = frame.Content;
                    fiber.IsRich = frame.IsRich;
                }
                if (!StyleList.ContentsEqual(fiber.Style, frame.Style))
                {
                    path.Add(index);
                    bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                    UpdateFiberStyle(fiber, frame.Style);
                    path.RemoveAt(path.Count - 1);
                }
                path.Add(index);
                EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                path.RemoveAt(path.Count - 1);
                fiber.Key = frame.Key;
                break;

            case BlobKind.Container:
                path.Add(index);
                DiffChildren(fiber, frame.Children!, path, ref bufs);
                if (!StyleList.ContentsEqual(fiber.Style, frame.Style))
                {
                    WarnNineSliceInvariants(in frame, path, ref bufs);
                    bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                    UpdateFiberStyle(fiber, frame.Style);
                }
                EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                {
                    if (!ReferenceEquals(fiber.Draw, frame.Draw))
                    {
                        bufs.AddOp(Op.SetDraw(frame.Draw, Snapshot(path)));
                        fiber.Draw = frame.Draw;
                    }
                }
                {
                    var eff = frame.Effect ?? BuildContext.Current.ResolveTagEffect(frame.Tag);
                    if (!Equals(fiber.Effect, eff))
                    {
                        bufs.AddOp(Op.SetEffect(eff, Snapshot(path)));
                        fiber.Effect = eff;
                    }
                }
                {
                    if (!Nullable.Equals(fiber.LayoutTransition, frame.LayoutTransition))
                    {
                        bufs.AddOp(Op.SetLayoutTransition(frame.LayoutTransition, Snapshot(path)));
                        fiber.LayoutTransition = frame.LayoutTransition;
                    }
                }
                fiber.Key = frame.Key;
                path.RemoveAt(path.Count - 1);
                break;

            case BlobKind.Image:
                if (!ReferenceEquals(fiber.Texture, frame.Texture) || fiber.Path != frame.Path)
                {
                    if ((frame.Texture is not null) == (frame.Path is not null))
                    {
                        bufs.AddWarning($"Image at HostPath=[{string.Join(",", path)},{index}] must have exactly one of Texture/Path set; update emitted with both null.");
                        bufs.AddOp(Op.UpdateImage(index, null, null, Snapshot(path)));
                    }
                    else
                    {
                        bufs.AddOp(Op.UpdateImage(index, frame.Texture, frame.Path, Snapshot(path)));
                    }
                    fiber.Texture = frame.Texture;
                    fiber.Path = frame.Path;
                }
                if (!StyleList.ContentsEqual(fiber.Style, frame.Style))
                {
                    path.Add(index);
                    bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                    UpdateFiberStyle(fiber, frame.Style);
                    path.RemoveAt(path.Count - 1);
                }
                path.Add(index);
                EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                path.RemoveAt(path.Count - 1);
                fiber.Key = frame.Key;
                break;

            case BlobKind.ScenePanel:
                bool sceneChanged = !ReferenceEquals(fiber.Scene, frame.Scene) || fiber.Path != frame.Path;
                bool renderOnceChanged = fiber.RenderOnce != frame.RenderOnce;
                if (sceneChanged || renderOnceChanged)
                {
                    if ((frame.Scene is not null) == (frame.Path is not null))
                    {
                        bufs.AddWarning($"ScenePanel at HostPath=[{string.Join(",", path)},{index}] must have exactly one of Scene/ScenePath set; update emitted with both null.");
                        bufs.AddOp(Op.UpdateScenePanel(index, null, null, frame.RenderOnce, Snapshot(path)));
                    }
                    else
                    {
                        bufs.AddOp(Op.UpdateScenePanel(index, frame.Scene, frame.Path, frame.RenderOnce, Snapshot(path)));
                    }
                    fiber.Scene = frame.Scene;
                    fiber.Path = frame.Path;
                    fiber.RenderOnce = frame.RenderOnce;
                }
                if (!StyleList.ContentsEqual(fiber.Style, frame.Style))
                {
                    path.Add(index);
                    bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                    UpdateFiberStyle(fiber, frame.Style);
                    path.RemoveAt(path.Count - 1);
                }
                path.Add(index);
                EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                path.RemoveAt(path.Count - 1);
                fiber.Key = frame.Key;
                break;

            case BlobKind.SvgPanel:
                bool pathChanged  = fiber.Path  != frame.Path;
                bool colorChanged = fiber.Color != frame.Color;
                if (pathChanged || colorChanged)
                {
                    bufs.AddOp(Op.UpdateSvgPanel(index, frame.Path, frame.Color, Snapshot(path)));
                    fiber.Path  = frame.Path;
                    fiber.Color = frame.Color;
                }
                if (!StyleList.ContentsEqual(fiber.Style, frame.Style))
                {
                    path.Add(index);
                    bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                    UpdateFiberStyle(fiber, frame.Style);
                    path.RemoveAt(path.Count - 1);
                }
                path.Add(index);
                EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                path.RemoveAt(path.Count - 1);
                fiber.Key = frame.Key;
                break;

            case BlobKind.Sector:
                if (fiber.Shape != frame.Shape)
                {
                    bufs.AddOp(Op.UpdateSector(index, in frame.Shape, Snapshot(path)));
                    fiber.Shape = frame.Shape;
                }
                if (!StyleList.ContentsEqual(fiber.Style, frame.Style))
                {
                    path.Add(index);
                    bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                    UpdateFiberStyle(fiber, frame.Style);
                    path.RemoveAt(path.Count - 1);
                }
                path.Add(index);
                EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                path.RemoveAt(path.Count - 1);
                fiber.Key = frame.Key;
                break;

            case BlobKind.Arc:
                if (fiber.Shape != frame.Shape)
                {
                    bufs.AddOp(Op.UpdateArc(index, in frame.Shape, Snapshot(path)));
                    fiber.Shape = frame.Shape;
                }
                if (!StyleList.ContentsEqual(fiber.Style, frame.Style))
                {
                    path.Add(index);
                    bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                    UpdateFiberStyle(fiber, frame.Style);
                    path.RemoveAt(path.Count - 1);
                }
                path.Add(index);
                EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                path.RemoveAt(path.Count - 1);
                fiber.Key = frame.Key;
                break;

            case BlobKind.Polygon:
                if (!ReferenceEquals(fiber.Points, frame.Points) || fiber.Shape != frame.Shape)
                {
                    bufs.AddOp(Op.UpdatePolygon(index, frame.Points, in frame.Shape, Snapshot(path)));
                    fiber.Points = frame.Points;
                    fiber.Shape = frame.Shape;
                }
                if (!StyleList.ContentsEqual(fiber.Style, frame.Style))
                {
                    path.Add(index);
                    bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                    UpdateFiberStyle(fiber, frame.Style);
                    path.RemoveAt(path.Count - 1);
                }
                path.Add(index);
                EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                path.RemoveAt(path.Count - 1);
                fiber.Key = frame.Key;
                break;

            case BlobKind.WebPanel:
                if (fiber.Path != frame.Path || fiber.Paused != frame.Paused)
                {
                    bufs.AddOp(Op.UpdateWebPanel(index, frame.Path, frame.Paused, Snapshot(path)));
                    fiber.Path = frame.Path;
                    fiber.Paused = frame.Paused;
                }
                if (!StyleList.ContentsEqual(fiber.Style, frame.Style))
                {
                    path.Add(index);
                    bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                    UpdateFiberStyle(fiber, frame.Style);
                    path.RemoveAt(path.Count - 1);
                }
                path.Add(index);
                EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                path.RemoveAt(path.Count - 1);
                fiber.Key = frame.Key;
                break;

            case BlobKind.Embed:
                {
                    // Update runs on every owner rebuild by contract (the panel decides internally
                    // whether pushed data actually changed) - delegates can't be diffed.
                    var nh = frame.Embed;
                    if (nh?.Update is not null)
                        bufs.AddOp(Op.UpdateEmbed(index, nh, Snapshot(path)));
                    if (!StyleList.ContentsEqual(fiber.Style, frame.Style))
                    {
                        path.Add(index);
                        bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                        UpdateFiberStyle(fiber, frame.Style);
                        path.RemoveAt(path.Count - 1);
                    }
                    fiber.Key = frame.Key;
                }
                break;

            case BlobKind.Virtual:
                {
                    // Items ref or count change triggers a refresh; the engine setter dedups
                    // content-equal lists, so delegate-only churn (fresh lambdas) emits nothing.
                    var pv = fiber.Virtual;
                    var nv = frame.Virtual!;
                    if (pv is null || !ReferenceEquals(pv.Items, nv.Items) || pv.Count != nv.Count)
                    {
                        bufs.AddOp(Op.UpdateVirtual(index, nv, Snapshot(path)));
                        fiber.Virtual = nv;
                    }
                    if (!StyleList.ContentsEqual(fiber.Style, frame.Style))
                    {
                        path.Add(index);
                        bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                        UpdateFiberStyle(fiber, frame.Style);
                        path.RemoveAt(path.Count - 1);
                    }
                    fiber.Key = frame.Key;
                }
                break;

            case BlobKind.TextEntry:
                {
                    WarnTextEntryInvariants(in frame, index, path, ref bufs);
                    bool placeholderChanged = fiber.Placeholder != frame.Placeholder;
                    bool maxLengthChanged = fiber.MaxLength != frame.MaxLength;
                    bool disabledChanged = fiber.Disabled != frame.Disabled;
                    bool numericChanged = fiber.Numeric != frame.Numeric;
                    bool minValueChanged = fiber.MinValue != frame.MinValue;
                    bool maxValueChanged = fiber.MaxValue != frame.MaxValue;
                    bool numberFormatChanged = fiber.NumberFormat != frame.NumberFormat;
                    bool multilineChanged = fiber.Multiline != frame.Multiline;
                    bool minLengthChanged = fiber.MinLength != frame.MinLength;
                    bool characterRegexChanged = fiber.CharacterRegex != frame.CharacterRegex;
                    bool stringRegexChanged = fiber.StringRegex != frame.StringRegex;
                    bool canEnterCharChanged = !ReferenceEquals(fiber.CanEnterChar, frame.CanEnterChar);
                    bool validateChanged = !ReferenceEquals(fiber.Validate, frame.Validate);
                    bool onValidationChangedChanged = !ReferenceEquals(fiber.OnValidationChanged, frame.OnValidationChanged);
                    bool onChangeChanged = !ReferenceEquals(fiber.OnChange, frame.OnChange);
                    bool onSubmitChanged = !ReferenceEquals(fiber.OnSubmit, frame.OnSubmit);
                    bool onFocusChanged = !ReferenceEquals(fiber.OnFocus, frame.OnFocus);
                    bool onBlurChanged = !ReferenceEquals(fiber.OnBlur, frame.OnBlur);
                    bool onCancelChanged = !ReferenceEquals(fiber.OnCancel, frame.OnCancel);
                    bool isControlledChanged = fiber.IsControlled != frame.IsControlled;

                    // Controlled mode re-emits Value every render so the engine's HasFocus
                    // guard can no-op in-flight typing. Uncontrolled mode never re-emits
                    // the text on Update (DefaultText is one-shot at Create time).
                    bool emit = frame.IsControlled
                              || placeholderChanged || maxLengthChanged || disabledChanged
                              || numericChanged || minValueChanged || maxValueChanged
                              || numberFormatChanged || multilineChanged
                              || minLengthChanged || characterRegexChanged || stringRegexChanged
                              || canEnterCharChanged || validateChanged || onValidationChangedChanged
                              || onChangeChanged || onSubmitChanged
                              || onFocusChanged || onBlurChanged || onCancelChanged
                              || isControlledChanged;

                    if (emit)
                    {
                        // Uncontrolled mode carries the fiber's last-known text so the Applier has valid state either way.
                        string? textPayload = frame.IsControlled ? frame.Path : fiber.Path;

                        bufs.AddOp(Op.UpdateTextEntry(
                            index,
                            text:                textPayload,
                            isControlled:        frame.IsControlled,
                            placeholder:         frame.Placeholder,
                            maxLength:           frame.MaxLength,
                            disabled:            frame.Disabled,
                            numeric:             frame.Numeric,
                            minValue:            frame.MinValue,
                            maxValue:            frame.MaxValue,
                            numberFormat:        frame.NumberFormat,
                            multiline:           frame.Multiline,
                            minLength:           frame.MinLength,
                            characterRegex:      frame.CharacterRegex,
                            stringRegex:         frame.StringRegex,
                            canEnterChar:        frame.CanEnterChar,
                            validate:            frame.Validate,
                            onValidationChanged: frame.OnValidationChanged,
                            onChange:            frame.OnChange,
                            onSubmit:            frame.OnSubmit,
                            onFocus:             frame.OnFocus,
                            onBlur:              frame.OnBlur,
                            onCancel:            frame.OnCancel,
                            hostPath:            Snapshot(path)));

                        // Always sync fiber state to frame after emit (covers callback
                        // ref rotation, controlled-value updates, all other fields).
                        fiber.Path = textPayload;
                        fiber.Placeholder = frame.Placeholder;
                        fiber.MaxLength = frame.MaxLength;
                        fiber.Disabled = frame.Disabled;
                        fiber.Numeric = frame.Numeric;
                        fiber.MinLength = frame.MinLength;
                        fiber.CharacterRegex = frame.CharacterRegex;
                        fiber.StringRegex = frame.StringRegex;
                        fiber.MinValue = frame.MinValue;
                        fiber.MaxValue = frame.MaxValue;
                        fiber.NumberFormat = frame.NumberFormat;
                        fiber.Multiline = frame.Multiline;
                        fiber.OnChange = frame.OnChange;
                        fiber.OnSubmit = frame.OnSubmit;
                        fiber.OnFocus = frame.OnFocus;
                        fiber.OnBlur = frame.OnBlur;
                        fiber.OnCancel = frame.OnCancel;
                        fiber.CanEnterChar = frame.CanEnterChar;
                        fiber.Validate = frame.Validate;
                        fiber.OnValidationChanged = frame.OnValidationChanged;
                        fiber.IsControlled = frame.IsControlled;
                    }
                    if (!StyleList.ContentsEqual(fiber.Style, frame.Style))
                    {
                        path.Add(index);
                        bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                        UpdateFiberStyle(fiber, frame.Style);
                        path.RemoveAt(path.Count - 1);
                    }
                    path.Add(index);
                    EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                    path.RemoveAt(path.Count - 1);
                    fiber.Key = frame.Key;
                }
                break;

        }
    }

    // Cell: expand a self-owning stateful instance in place at this slot; its state lives on the fiber.
    static void DiffCell(ref Fiber? fiber, ref Frame frame, int index, List<int> path, ref DiffBuffers bufs)
    {
        // 1. Reuse the fiber's instance when its runtime type matches frame.CellType, else dispose old and create fresh.
        Cell instance;
        if (fiber?.Instance is Cell prior && prior.GetType() == frame.CellType)
            instance = prior;
        else
        {
            if (fiber?.Instance is IDisposable replaced) replaced.Dispose();
            instance = frame.CellFactory!();
            // Seed fires once here in the create branch; configure (below, unconditional) fires right after on first mount.
            frame.Seed?.Invoke(instance);
        }

        // 2. Refresh props on the persistent instance.
        frame.Configure?.Invoke(instance);

        // 3. Wire the rebuild hook (root marks itself dirty).
        instance.SetRebuildHook(BuildContext.Current.RootRebuild);

        // 4. Detach before recursing so an inner-kind-change teardown cannot dispose the instance we re-attach.
        if (fiber is not null) fiber.Instance = null;

        // 5. Expand to the inner Blob and recurse at the same slot; the normal arms handle it.
        Frame inner = default;
        instance.ExpandInto(ref inner);
        DiffNode(ref fiber, ref inner, index, path, ref bufs);

        // 6. Re-attach the instance and stamp the cell's key onto the slot fiber (keyed reorder needs it).
        // fiber is non-null here: every DiffNode arm assigns it before returning.
        fiber!.Instance = instance;
        fiber.Key = frame.Key;
    }

    // Skip when both sides are all-null. Otherwise emit only on a structural handler change (Delegate.Equals per slot): method-groups and instance-field closures compare equal and skip; local-capture closures differ each Build and correctly rewire.
    static void EmitSetEventsIfDelta(in Frame frame, Fiber fiber, List<int> targetPath, ref DiffBuffers bufs)
    {
        if (!frame.Events.AnyNonNull && !fiber.PrevEvents.AnyNonNull) return;
        if (BlobEvents.ContentsEqual(in fiber.PrevEvents, in frame.Events))
        {
            fiber.PrevEvents = frame.Events;
            return;
        }
        bufs.AddOp(Op.SetEvents(frame.Events, Snapshot(targetPath)));
        fiber.PrevEvents = frame.Events;
    }

    static void DiffChildren(Fiber parent, Children intentChildren, List<int> path, ref DiffBuffers bufs)
    {
        parent.Children ??= new List<Fiber>();
        var prev = parent.Children;
        int nextCount = intentChildren.Count;

        bool prevAllUnkeyed = AllUnkeyed(prev);
        bool nextAllUnkeyed = AllUnkeyed(in intentChildren);
        bool allUnkeyed = prevAllUnkeyed && nextAllUnkeyed;

        bool prevAllKeyed = AllKeyed(prev);
        bool nextAllKeyed = AllKeyed(in intentChildren);
        bool allKeyed = prevAllKeyed && nextAllKeyed;

        if (allKeyed)
        {
            DiffKeyed(parent, in intentChildren, path, ref bufs);
            return;
        }

        if (!allUnkeyed)
        {
            bufs.AddWarning($"Mixed keyed/unkeyed children at HostPath=[{string.Join(",", path)}]; use all-keyed or all-unkeyed children per list. {DescribeKeyMix(in intentChildren)}");
            DiffPositional(parent, in intentChildren, path, ref bufs, warnLengthChange: false);
            return;
        }

        DiffPositional(parent, in intentChildren, path, ref bufs, warnLengthChange: true);
    }

    static void DiffPositional(Fiber parent, in Children intentChildren, List<int> path, ref DiffBuffers bufs, bool warnLengthChange)
    {
        var prev = parent.Children!;
        int nextCount = intentChildren.Count;

        if (warnLengthChange && prev.Count != nextCount)
            bufs.AddWarning($"Keyless child list at HostPath=[{string.Join(",", path)}] changed length {prev.Count}->{nextCount}.");

        int common = Math.Min(prev.Count, nextCount);

        for (int i = 0; i < common; i++)
        {
            Fiber? f = prev[i];
            DiffNode(ref f, ref intentChildren[i], i, path, ref bufs);
            prev[i] = f!;
        }

        for (int i = common; i < nextCount; i++)
        {
            Fiber? f = null;
            DiffNode(ref f, ref intentChildren[i], i, path, ref bufs);
            prev.Add(f!);
        }

        for (int i = prev.Count - 1; i >= common && prev.Count > nextCount; i--)
        {
            bufs.AddOp(Op.RemoveAt(i, Snapshot(path)));
            TeardownTree(prev[i]);
            prev.RemoveAt(i);
        }
    }

    // Dispose every IDisposable Cell instance in a fiber tree; entry point for panel teardown on disable/remount.
    internal static void TeardownFiberTree(Fiber? fiber)
    {
        if (fiber is not null) TeardownTree(fiber);
    }

    // Dispose any IDisposable Cell instance in a fiber subtree that is leaving the tree for good.
    static void TeardownTree(Fiber fiber)
    {
        if (fiber.Instance is IDisposable d) d.Dispose();
        if (fiber.Children is { } kids)
            for (int i = 0; i < kids.Count; i++) TeardownTree(kids[i]);
    }

    static void DiffKeyed(Fiber parent, in Children intentChildren, List<int> path, ref DiffBuffers bufs)
    {
        var prev = parent.Children!;
        int nextCount = intentChildren.Count;
        var ctx = BuildContext.Current;

        // prevByKey: identity lookup of the surviving Fiber for a key (stays valid through prev mutation).
        // nextKeys:  membership probe for the removal pass.
        // The redundant "current" key mirror that the old code carried is gone; IndexOfKey scans prev directly.
        var prevByKey = ctx.RentKeyedDict();
        var nextKeys  = ctx.RentKeySet();

        try
        {
            // Duplicate keys on either side break the move/insert bookkeeping; degrade like the mixed-keys path.
            string? dupKey = null;
            for (int i = 0; i < nextCount && dupKey is null; i++)
                if (!nextKeys.Add(intentChildren[i].Key!)) dupKey = intentChildren[i].Key;
            for (int i = 0; i < prev.Count && dupKey is null; i++)
                if (!prevByKey.TryAdd(prev[i].Key!, prev[i])) dupKey = prev[i].Key;
            if (dupKey is not null)
            {
                bufs.AddWarning($"Duplicate key \"{dupKey}\" at HostPath=[{string.Join(",", path)}]; keys must be unique per list, falling back to positional diff.");
                DiffPositional(parent, in intentChildren, path, ref bufs, warnLengthChange: false);
                return;
            }

            for (int i = prev.Count - 1; i >= 0; i--)
            {
                if (!nextKeys.Contains(prev[i].Key!))
                {
                    bufs.AddOp(Op.RemoveAt(i, Snapshot(path)));
                    TeardownTree(prev[i]);
                    prev.RemoveAt(i);
                }
            }

            for (int newIdx = 0; newIdx < nextCount; newIdx++)
            {
                var nKey = intentChildren[newIdx].Key!;

                if (!prevByKey.ContainsKey(nKey))
                {
                    Fiber? f = null;
                    DiffNode(ref f, ref intentChildren[newIdx], newIdx, path, ref bufs);
                    prev.Insert(newIdx, f!);
                    prevByKey[nKey] = f!;
                    continue;
                }

                int oldIdx = IndexOfKey(prev, nKey);
                if (oldIdx != newIdx)
                {
                    bufs.AddOp(Op.MoveAt(oldIdx, newIdx, Snapshot(path)));
                    var moved = prev[oldIdx];
                    prev.RemoveAt(oldIdx);
                    prev.Insert(newIdx, moved);
                }

                Fiber existing = prevByKey[nKey];
                Fiber? slot = existing;
                DiffNode(ref slot, ref intentChildren[newIdx], newIdx, path, ref bufs);
                prev[newIdx] = slot!;
            }
        }
        finally
        {
            ctx.ReturnKeyedDict(prevByKey);
            ctx.ReturnKeySet(nextKeys);
        }
    }

    static int IndexOfKey(List<Fiber> prev, string key)
    {
        for (int i = 0; i < prev.Count; i++) if (prev[i].Key == key) return i;
        return -1;
    }

    static void EmitCreate(in Frame frame, Fiber fiber, int index, List<int> path, ref DiffBuffers bufs)
    {
        switch (frame.Kind)
        {
            case BlobKind.Text:
                bufs.AddOp(Op.CreateText(index, RenderText(frame.Content, frame.IsRich), Snapshot(path), frame.IsRich));
                if (frame.Style != StyleList.Empty && frame.Style.Count > 0)
                {
                    path.Add(index);
                    bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                    UpdateFiberStyle(fiber, frame.Style);
                    path.RemoveAt(path.Count - 1);
                }
                path.Add(index);
                EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                path.RemoveAt(path.Count - 1);
                break;

            case BlobKind.Container:
                {
                    bufs.AddOp(Op.CreateContainer(index, Snapshot(path)));
                    path.Add(index);
                    fiber.Children = new List<Fiber>();
                    var kids = frame.Children!;
                    for (int i = 0; i < kids.Count; i++)
                    {
                        Fiber? child = null;
                        DiffNode(ref child, ref kids[i], i, path, ref bufs);
                        fiber.Children.Add(child!);
                    }
                    if (frame.Style != StyleList.Empty && frame.Style.Count > 0)
                    {
                        WarnNineSliceInvariants(in frame, path, ref bufs);
                        bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                        UpdateFiberStyle(fiber, frame.Style);
                    }
                    EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                    if (frame.Draw is not null)
                    {
                        bufs.AddOp(Op.SetDraw(frame.Draw, Snapshot(path)));
                    }
                    var eff = frame.Effect ?? BuildContext.Current.ResolveTagEffect(frame.Tag);
                    if (eff is not null)
                    {
                        bufs.AddOp(Op.SetEffect(eff, Snapshot(path)));
                    }
                    fiber.Effect = eff;
                    if (frame.LayoutTransition is not null)
                        bufs.AddOp(Op.SetLayoutTransition(frame.LayoutTransition, Snapshot(path)));
                    fiber.LayoutTransition = frame.LayoutTransition;
                    path.RemoveAt(path.Count - 1);
                }
                break;

            case BlobKind.Image:
                {
                    if ((frame.Texture is not null) == (frame.Path is not null))
                    {
                        bufs.AddWarning($"Image at HostPath=[{string.Join(",", path)}] must have exactly one of Texture/Path set; emitted with both null.");
                        bufs.AddOp(Op.CreateImage(index, null, null, Snapshot(path)));
                    }
                    else
                    {
                        bufs.AddOp(Op.CreateImage(index, frame.Texture, frame.Path, Snapshot(path)));
                    }
                    if (frame.Style != StyleList.Empty && frame.Style.Count > 0)
                    {
                        path.Add(index);
                        bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                        UpdateFiberStyle(fiber, frame.Style);
                        path.RemoveAt(path.Count - 1);
                    }
                    fiber.Texture = frame.Texture;
                    fiber.Path = frame.Path;
                    path.Add(index);
                    EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                    path.RemoveAt(path.Count - 1);
                }
                break;

            case BlobKind.ScenePanel:
                {
                    if ((frame.Scene is not null) == (frame.Path is not null))
                    {
                        bufs.AddWarning($"ScenePanel at HostPath=[{string.Join(",", path)}] must have exactly one of Scene/ScenePath set; emitted with both null.");
                        bufs.AddOp(Op.CreateScenePanel(index, null, null, frame.RenderOnce, Snapshot(path)));
                    }
                    else
                    {
                        bufs.AddOp(Op.CreateScenePanel(index, frame.Scene, frame.Path, frame.RenderOnce, Snapshot(path)));
                    }
                    if (frame.Style != StyleList.Empty && frame.Style.Count > 0)
                    {
                        path.Add(index);
                        bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                        UpdateFiberStyle(fiber, frame.Style);
                        path.RemoveAt(path.Count - 1);
                    }
                    fiber.Scene = frame.Scene;
                    fiber.Path = frame.Path;
                    fiber.RenderOnce = frame.RenderOnce;
                    path.Add(index);
                    EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                    path.RemoveAt(path.Count - 1);
                }
                break;

            case BlobKind.SvgPanel:
                {
                    bufs.AddOp(Op.CreateSvgPanel(index, frame.Path, frame.Color, Snapshot(path)));
                    if (frame.Style != StyleList.Empty && frame.Style.Count > 0)
                    {
                        path.Add(index);
                        bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                        UpdateFiberStyle(fiber, frame.Style);
                        path.RemoveAt(path.Count - 1);
                    }
                    fiber.Path = frame.Path;
                    fiber.Color = frame.Color;
                    path.Add(index);
                    EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                    path.RemoveAt(path.Count - 1);
                }
                break;

            case BlobKind.Sector:
                {
                    bufs.AddOp(Op.CreateSector(index, in frame.Shape, Snapshot(path)));
                    if (frame.Style != StyleList.Empty && frame.Style.Count > 0)
                    {
                        path.Add(index);
                        bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                        UpdateFiberStyle(fiber, frame.Style);
                        path.RemoveAt(path.Count - 1);
                    }
                    fiber.Shape = frame.Shape;
                    path.Add(index);
                    EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                    path.RemoveAt(path.Count - 1);
                }
                break;

            case BlobKind.Arc:
                {
                    bufs.AddOp(Op.CreateArc(index, in frame.Shape, Snapshot(path)));
                    if (frame.Style != StyleList.Empty && frame.Style.Count > 0)
                    {
                        path.Add(index);
                        bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                        UpdateFiberStyle(fiber, frame.Style);
                        path.RemoveAt(path.Count - 1);
                    }
                    fiber.Shape = frame.Shape;
                    path.Add(index);
                    EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                    path.RemoveAt(path.Count - 1);
                }
                break;

            case BlobKind.Polygon:
                {
                    bufs.AddOp(Op.CreatePolygon(index, frame.Points, in frame.Shape, Snapshot(path)));
                    if (frame.Style != StyleList.Empty && frame.Style.Count > 0)
                    {
                        path.Add(index);
                        bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                        UpdateFiberStyle(fiber, frame.Style);
                        path.RemoveAt(path.Count - 1);
                    }
                    fiber.Points = frame.Points;
                    fiber.Shape = frame.Shape;
                    path.Add(index);
                    EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                    path.RemoveAt(path.Count - 1);
                }
                break;

            case BlobKind.WebPanel:
                {
                    bufs.AddOp(Op.CreateWebPanel(index, frame.Path, frame.Paused, Snapshot(path)));
                    if (frame.Style != StyleList.Empty && frame.Style.Count > 0)
                    {
                        path.Add(index);
                        bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                        UpdateFiberStyle(fiber, frame.Style);
                        path.RemoveAt(path.Count - 1);
                    }
                    fiber.Path = frame.Path;
                    fiber.Paused = frame.Paused;
                    path.Add(index);
                    EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                    path.RemoveAt(path.Count - 1);
                }
                break;

            case BlobKind.TextEntry:
                {
                    WarnTextEntryInvariants(in frame, index, path, ref bufs);
                    bufs.AddOp(Op.CreateTextEntry(
                        index,
                        text:                frame.Path,
                        isControlled:        frame.IsControlled,
                        placeholder:         frame.Placeholder,
                        maxLength:           frame.MaxLength,
                        disabled:            frame.Disabled,
                        numeric:             frame.Numeric,
                        minValue:            frame.MinValue,
                        maxValue:            frame.MaxValue,
                        numberFormat:        frame.NumberFormat,
                        multiline:           frame.Multiline,
                        minLength:           frame.MinLength,
                        characterRegex:      frame.CharacterRegex,
                        stringRegex:         frame.StringRegex,
                        canEnterChar:        frame.CanEnterChar,
                        validate:            frame.Validate,
                        onValidationChanged: frame.OnValidationChanged,
                        onChange:            frame.OnChange,
                        onSubmit:            frame.OnSubmit,
                        onFocus:             frame.OnFocus,
                        onBlur:              frame.OnBlur,
                        onCancel:            frame.OnCancel,
                        hostPath:            Snapshot(path)));
                    if (frame.Style != StyleList.Empty && frame.Style.Count > 0)
                    {
                        path.Add(index);
                        bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                        UpdateFiberStyle(fiber, frame.Style);
                        path.RemoveAt(path.Count - 1);
                    }
                    fiber.Path = frame.Path;
                    fiber.Placeholder = frame.Placeholder;
                    fiber.MaxLength = frame.MaxLength;
                    fiber.Disabled = frame.Disabled;
                    fiber.Numeric = frame.Numeric;
                    fiber.MinLength = frame.MinLength;
                    fiber.CharacterRegex = frame.CharacterRegex;
                    fiber.StringRegex = frame.StringRegex;
                    fiber.MinValue = frame.MinValue;
                    fiber.MaxValue = frame.MaxValue;
                    fiber.NumberFormat = frame.NumberFormat;
                    fiber.Multiline = frame.Multiline;
                    fiber.OnChange = frame.OnChange;
                    fiber.OnSubmit = frame.OnSubmit;
                    fiber.OnFocus = frame.OnFocus;
                    fiber.OnBlur = frame.OnBlur;
                    fiber.OnCancel = frame.OnCancel;
                    fiber.CanEnterChar = frame.CanEnterChar;
                    fiber.Validate = frame.Validate;
                    fiber.OnValidationChanged = frame.OnValidationChanged;
                    fiber.IsControlled = frame.IsControlled;
                    path.Add(index);
                    EmitSetEventsIfDelta(in frame, fiber, path, ref bufs);
                    path.RemoveAt(path.Count - 1);
                }
                break;

            case BlobKind.Embed:
                {
                    bufs.AddOp(Op.CreateEmbed(index, frame.Embed, Snapshot(path)));
                    if (frame.Style != StyleList.Empty && frame.Style.Count > 0)
                    {
                        path.Add(index);
                        bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                        UpdateFiberStyle(fiber, frame.Style);
                        path.RemoveAt(path.Count - 1);
                    }
                }
                break;

            case BlobKind.Virtual:
                {
                    bufs.AddOp(Op.CreateVirtual(index, frame.Virtual, Snapshot(path)));
                    if (frame.Style != StyleList.Empty && frame.Style.Count > 0)
                    {
                        path.Add(index);
                        bufs.AddOp(Op.SetStyle(frame.Style, Snapshot(path)));
                        UpdateFiberStyle(fiber, frame.Style);
                        path.RemoveAt(path.Count - 1);
                    }
                    fiber.Virtual = frame.Virtual;
                }
                break;

            default:
                throw new InvalidOperationException($"Unknown BlobKind {frame.Kind}.");
        }
    }

    // Nine-slice author-mistake warnings shared by the Container Create and style-change Update
    // arms. All three misuses render blank or wrong with zero engine errors (HasBackground culling,
    // zero-width border paint, shader dropping the middle at >= 50% slice). path already ends with
    // this container's index when called.
    static void WarnNineSliceInvariants(in Frame frame, List<int> path, ref DiffBuffers bufs)
    {
        var s = frame.Style;
        if (s.Count == 0) return;

        bool hasL = s.TryGet(StyleField.BorderImageWidthLeft, out var sl);
        bool hasT = s.TryGet(StyleField.BorderImageWidthTop, out var st);
        bool hasR = s.TryGet(StyleField.BorderImageWidthRight, out var sr);
        bool hasB = s.TryGet(StyleField.BorderImageWidthBottom, out var sb);
        bool hasSlice = hasL || hasT || hasR || hasB;
        bool hasSource = s.TryGet(StyleField.BorderImageSource, out var src);
        if (!hasSlice && !hasSource) return;

        string where = $"Container at HostPath=[{string.Join(",", path)}]";

        if (!hasSource)
        {
            bufs.AddWarning(
                $"{where} sets BorderImageWidth* but BorderImageSource is null/absent " +
                "(failed texture load?); the panel is background-culled and renders NOTHING.");
            return;
        }

        float border = 0f;
        if (s.TryGet(StyleField.BorderWidth, out var bw)) border = MathF.Max(border, bw.LengthVal.Value);
        if (s.TryGet(StyleField.BorderLeftWidth, out var b1)) border = MathF.Max(border, b1.LengthVal.Value);
        if (s.TryGet(StyleField.BorderTopWidth, out var b2)) border = MathF.Max(border, b2.LengthVal.Value);
        if (s.TryGet(StyleField.BorderRightWidth, out var b3)) border = MathF.Max(border, b3.LengthVal.Value);
        if (s.TryGet(StyleField.BorderBottomWidth, out var b4)) border = MathF.Max(border, b4.LengthVal.Value);
        if (border <= 0f)
            bufs.AddWarning(
                $"{where} sets BorderImageSource but no positive BorderWidth; the image paints into " +
                "a zero-width border and renders NOTHING. Set BorderWidth (Skins.NineSlice renderedBorder).");

        if (src.RefVal is Sandbox.Texture tex && tex.Width > 0 && tex.Height > 0)
        {
            float l = hasL ? sl.LengthVal.Value : 0f, r = hasR ? sr.LengthVal.Value : 0f;
            float t = hasT ? st.LengthVal.Value : 0f, b = hasB ? sb.LengthVal.Value : 0f;
            if (NineSliceConsumesSource(l, r, t, b, tex.Width, tex.Height))
                bufs.AddWarning(
                    $"{where} slice widths (L{l} T{t} R{r} B{b}) consume the whole {tex.Width}x{tex.Height} " +
                    "source; the engine shader drops the middle at >= 50% per axis. Slice must be the " +
                    "corner-art size in SOURCE-image pixels, not on-screen pixels.");
        }
    }

    // Engine ui_cssbox.shader skips the middle patch when the slice reaches half a source dimension.
    internal static bool NineSliceConsumesSource(float l, float r, float t, float b, float texW, float texH)
        => l + r >= texW || t + b >= texH;

    // Author-mistake warnings shared by the Create and Update TextEntry paths, so the
    // two arms can't drift. Value+DefaultText is a controlled/uncontrolled mixup; Multiline
    // +OnSubmit is a no-op handler (Enter inserts a newline in multiline mode).
    static void WarnTextEntryInvariants(in Frame frame, int index, List<int> path, ref DiffBuffers bufs)
    {
        if (frame.ValueAndDefaultTextBothSet)
            bufs.AddWarning(
                $"TextEntry at HostPath=[{string.Join(",", path)},{index}] " +
                "has both Value and DefaultText set; using Value (controlled mode). " +
                "Set only one: Value for controlled, DefaultText for uncontrolled.");
        if (frame.Multiline && frame.OnSubmit is not null)
            bufs.AddWarning(
                $"TextEntry at HostPath=[{string.Join(",", path)},{index}] " +
                "has Multiline=true with an OnSubmit handler; OnSubmit will not fire " +
                "(Enter inserts newline in multiline mode). Use OnChange to observe edits.");
    }

    static Fiber AllocateFiberFor(in Frame frame)
    {
        return new Fiber
        {
            Kind = frame.Kind,
            Key = frame.Key,
            Content = frame.Content,
            IsRich = frame.IsRich,
            Style = StyleList.Empty,
            Children = frame.Kind == BlobKind.Container ? new List<Fiber>() : null,
            Texture = frame.Texture,
            Path = frame.Path,
            Scene = frame.Scene,
            RenderOnce = frame.RenderOnce,
            Paused = frame.Paused,
            Color = frame.Color,
            Shape = frame.Shape,
            Points = frame.Points,
            Effect = frame.Effect,
            // Kind-gated: pooled Frame slots can carry a stale config from a prior occupant,
            // and copying it would pin that build's items list on an unrelated fiber.
            Virtual = frame.Kind == BlobKind.Virtual ? frame.Virtual : null,
            Draw = frame.Draw,
            LayoutTransition = frame.LayoutTransition,
            Placeholder = frame.Placeholder,
            MaxLength = frame.MaxLength,
            Disabled = frame.Disabled,
            Numeric = frame.Numeric,
            MinValue = frame.MinValue,
            MaxValue = frame.MaxValue,
            NumberFormat = frame.NumberFormat,
            Multiline = frame.Multiline,
            MinLength = frame.MinLength,
            CharacterRegex = frame.CharacterRegex,
            StringRegex = frame.StringRegex,
            OnChange = frame.OnChange,
            OnSubmit = frame.OnSubmit,
            OnFocus = frame.OnFocus,
            OnBlur = frame.OnBlur,
            OnCancel = frame.OnCancel,
            CanEnterChar = frame.CanEnterChar,
            Validate = frame.Validate,
            OnValidationChanged = frame.OnValidationChanged,
            IsControlled = frame.IsControlled,
        };
    }

    static void UpdateFiberStyle(Fiber fiber, StyleList incoming)
    {
        if (incoming == StyleList.Empty || incoming.Count == 0)
        {
            fiber.Style = StyleList.Empty;
            return;
        }
        if (fiber.Style == StyleList.Empty)
            fiber.Style = new StyleList();
        fiber.Style.CopyFrom(incoming);
    }

    static bool AllUnkeyed(List<Fiber> fibers)
    {
        foreach (var f in fibers) if (f.Key is not null) return false;
        return true;
    }

    static bool AllUnkeyed(in Children children)
    {
        for (int i = 0; i < children.Count; i++) if (children[i].Key is not null) return false;
        return true;
    }

    static bool AllKeyed(List<Fiber> fibers)
    {
        foreach (var f in fibers) if (f.Key is null) return false;
        return true;
    }

    static bool AllKeyed(in Children children)
    {
        for (int i = 0; i < children.Count; i++) if (children[i].Key is null) return false;
        return true;
    }

    static int[] Snapshot(List<int> path) => BuildContext.Current.RentHostPath(path);

    static string RenderText(string source, bool isRich)
        => isRich ? Internal.RichTextParser.Parse(source) : source;

    // Slow-path diagnostic for the mixed-keyed warning: lists which sibling indices are keyed vs unkeyed (truncated per side) so the developer can spot the offender.
    static string DescribeKeyMix(in Children intentChildren)
    {
        const int MaxListed = 8;
        var unkeyed = new System.Text.StringBuilder();
        var keyed = new System.Text.StringBuilder();
        int unkeyedCount = 0, keyedCount = 0;
        for (int i = 0; i < intentChildren.Count; i++)
        {
            var key = intentChildren[i].Key;
            if (key is null)
            {
                if (unkeyedCount < MaxListed)
                {
                    if (unkeyedCount > 0) unkeyed.Append(',');
                    unkeyed.Append(i);
                }
                unkeyedCount++;
            }
            else
            {
                if (keyedCount < MaxListed)
                {
                    if (keyedCount > 0) keyed.Append(',');
                    keyed.Append(i).Append(":\"").Append(key).Append('"');
                }
                keyedCount++;
            }
        }
        if (unkeyedCount > MaxListed) unkeyed.Append(",...");
        if (keyedCount > MaxListed) keyed.Append(",...");
        return $"Intent ({intentChildren.Count} children): unkeyed at [{unkeyed}]; keyed at [{keyed}].";
    }
}

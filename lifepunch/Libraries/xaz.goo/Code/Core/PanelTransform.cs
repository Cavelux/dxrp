using System;
using System.Collections.Immutable;
using Sandbox;
using Sandbox.UI;
using EngineEntry = Sandbox.UI.PanelTransform.Entry;
using EngineEntryType = Sandbox.UI.PanelTransform.EntryType;

namespace Goo;

/// <summary>Typed wrapper around Sandbox.UI.PanelTransform adding fluent chaining and structural equality (the engine struct's Add* methods do not chain and its equality is reference-based, which would emit redundant per-Build ops).</summary>
public readonly struct PanelTransform : IEquatable<PanelTransform>
{
    internal readonly ImmutableList<EngineEntry>? _entries;

    private PanelTransform(ImmutableList<EngineEntry> entries) => _entries = entries;

    public bool IsEmpty => _entries is null || _entries.Count == 0;

    public bool Equals(PanelTransform other) => EntriesEqual(_entries, other._entries);

    public override bool Equals(object? obj) => obj is PanelTransform o && Equals(o);

    public override int GetHashCode() => ComputeEntriesHash(_entries);

    internal static int ComputeEntriesHash(ImmutableList<EngineEntry>? entries)
    {
        if (entries is null || entries.Count == 0) return 0;
        var h = new HashCode();
        h.Add(entries.Count);
        foreach (var e in entries) h.Add(EntryHash(e));
        return h.ToHashCode();
    }

    public static bool operator ==(PanelTransform a, PanelTransform b) => a.Equals(b);
    public static bool operator !=(PanelTransform a, PanelTransform b) => !a.Equals(b);

    internal static bool EntriesEqual(ImmutableList<EngineEntry>? a, ImmutableList<EngineEntry>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        var aCount = a?.Count ?? 0;
        var bCount = b?.Count ?? 0;
        if (aCount != bCount) return false;
        if (aCount == 0) return true;
        for (int i = 0; i < aCount; i++)
            if (!EntryEqual(a![i], b![i])) return false;
        return true;
    }

    static bool EntryEqual(in EngineEntry a, in EngineEntry b)
        => a.Type == b.Type
            && a.Data == b.Data
            && a.Matrix == b.Matrix
            && a.X.Equals(b.X)
            && a.Y.Equals(b.Y)
            && a.Z.Equals(b.Z);

    static int EntryHash(in EngineEntry e)
        => HashCode.Combine(e.Type, e.Data, e.Matrix, e.X, e.Y, e.Z);

    // ---- Static factories ----

    public static PanelTransform Translate(Length x, Length y)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Translate, X = x, Y = y }));

    public static PanelTransform Translate(Length x, Length y, Length z)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Translate, X = x, Y = y, Z = z }));

    public static PanelTransform TranslateX(Length x)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Translate, X = x }));

    public static PanelTransform TranslateY(Length y)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Translate, Y = y }));

    public static PanelTransform TranslateZ(Length z)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Translate, Z = z }));

    public static PanelTransform Rotate(float zDegrees)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Rotation, Data = new Vector3(0f, 0f, zDegrees) }));

    public static PanelTransform Rotate(float xDeg, float yDeg, float zDeg)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Rotation, Data = new Vector3(xDeg, yDeg, zDeg) }));

    public static PanelTransform RotateX(float deg)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Rotation, Data = new Vector3(deg, 0f, 0f) }));

    public static PanelTransform RotateY(float deg)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Rotation, Data = new Vector3(0f, deg, 0f) }));

    public static PanelTransform RotateZ(float deg)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Rotation, Data = new Vector3(0f, 0f, deg) }));

    public static PanelTransform Scale(float uniform)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Scale, Data = new Vector3(uniform, uniform, uniform) }));

    public static PanelTransform Scale(Vector3 scale)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Scale, Data = scale }));

    public static PanelTransform ScaleX(float s)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Scale, Data = new Vector3(s, 1f, 1f) }));

    public static PanelTransform ScaleY(float s)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Scale, Data = new Vector3(1f, s, 1f) }));

    public static PanelTransform ScaleZ(float s)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Scale, Data = new Vector3(1f, 1f, s) }));

    public static PanelTransform Skew(float xDeg, float yDeg)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Skew, Data = new Vector3(xDeg, yDeg, 0f) }));

    public static PanelTransform SkewX(float deg)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Skew, Data = new Vector3(deg, 0f, 0f) }));

    public static PanelTransform SkewY(float deg)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Skew, Data = new Vector3(0f, deg, 0f) }));

    public static PanelTransform Perspective(Length d)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Perspective, X = d }));

    public static PanelTransform Matrix3D(Matrix m)
        => new(ImmutableList.Create(new EngineEntry { Type = EngineEntryType.Matrix, Matrix = m }));

    // ---- Internal append helper used by extension fluent methods ----

    internal PanelTransform Append(in EngineEntry entry)
        => new((_entries ?? ImmutableList<EngineEntry>.Empty).Add(entry));
}

using Sandbox;
using Sandbox.UI;
using EngineEntry = Sandbox.UI.PanelTransform.Entry;
using EngineEntryType = Sandbox.UI.PanelTransform.EntryType;

namespace Goo;

/// <summary>Instance fluent chain methods for <see cref="PanelTransform"/>, each returning a new value with the entry appended (extensions because C# forbids same-name static and instance methods on one type).</summary>
public static class PanelTransformExtensions
{
    public static PanelTransform Translate(this PanelTransform t, Length x, Length y)
        => t.Append(new EngineEntry { Type = EngineEntryType.Translate, X = x, Y = y });

    public static PanelTransform Translate(this PanelTransform t, Length x, Length y, Length z)
        => t.Append(new EngineEntry { Type = EngineEntryType.Translate, X = x, Y = y, Z = z });

    public static PanelTransform TranslateX(this PanelTransform t, Length x)
        => t.Append(new EngineEntry { Type = EngineEntryType.Translate, X = x });

    public static PanelTransform TranslateY(this PanelTransform t, Length y)
        => t.Append(new EngineEntry { Type = EngineEntryType.Translate, Y = y });

    public static PanelTransform TranslateZ(this PanelTransform t, Length z)
        => t.Append(new EngineEntry { Type = EngineEntryType.Translate, Z = z });

    public static PanelTransform Rotate(this PanelTransform t, float zDegrees)
        => t.Append(new EngineEntry { Type = EngineEntryType.Rotation, Data = new Vector3(0f, 0f, zDegrees) });

    public static PanelTransform Rotate(this PanelTransform t, float xDeg, float yDeg, float zDeg)
        => t.Append(new EngineEntry { Type = EngineEntryType.Rotation, Data = new Vector3(xDeg, yDeg, zDeg) });

    public static PanelTransform RotateX(this PanelTransform t, float deg)
        => t.Append(new EngineEntry { Type = EngineEntryType.Rotation, Data = new Vector3(deg, 0f, 0f) });

    public static PanelTransform RotateY(this PanelTransform t, float deg)
        => t.Append(new EngineEntry { Type = EngineEntryType.Rotation, Data = new Vector3(0f, deg, 0f) });

    public static PanelTransform RotateZ(this PanelTransform t, float deg)
        => t.Append(new EngineEntry { Type = EngineEntryType.Rotation, Data = new Vector3(0f, 0f, deg) });

    public static PanelTransform Scale(this PanelTransform t, float uniform)
        => t.Append(new EngineEntry { Type = EngineEntryType.Scale, Data = new Vector3(uniform, uniform, uniform) });

    public static PanelTransform Scale(this PanelTransform t, Vector3 scale)
        => t.Append(new EngineEntry { Type = EngineEntryType.Scale, Data = scale });

    public static PanelTransform ScaleX(this PanelTransform t, float s)
        => t.Append(new EngineEntry { Type = EngineEntryType.Scale, Data = new Vector3(s, 1f, 1f) });

    public static PanelTransform ScaleY(this PanelTransform t, float s)
        => t.Append(new EngineEntry { Type = EngineEntryType.Scale, Data = new Vector3(1f, s, 1f) });

    public static PanelTransform ScaleZ(this PanelTransform t, float s)
        => t.Append(new EngineEntry { Type = EngineEntryType.Scale, Data = new Vector3(1f, 1f, s) });

    public static PanelTransform Skew(this PanelTransform t, float xDeg, float yDeg)
        => t.Append(new EngineEntry { Type = EngineEntryType.Skew, Data = new Vector3(xDeg, yDeg, 0f) });

    public static PanelTransform SkewX(this PanelTransform t, float deg)
        => t.Append(new EngineEntry { Type = EngineEntryType.Skew, Data = new Vector3(deg, 0f, 0f) });

    public static PanelTransform SkewY(this PanelTransform t, float deg)
        => t.Append(new EngineEntry { Type = EngineEntryType.Skew, Data = new Vector3(0f, deg, 0f) });

    public static PanelTransform Perspective(this PanelTransform t, Length d)
        => t.Append(new EngineEntry { Type = EngineEntryType.Perspective, X = d });

    public static PanelTransform Matrix3D(this PanelTransform t, Matrix m)
        => t.Append(new EngineEntry { Type = EngineEntryType.Matrix, Matrix = m });
}

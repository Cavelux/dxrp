using Sandbox;
using Sandbox.Rendering;
using Sandbox.UI;

namespace Goo;

// Style helpers hoisted so generated Blob facades share them. Keep the early-return form: an engine-type ternary that resolves bare null via an implicit string operator silently produces magenta (Color.Parse fallback) instead of the intended absent-property. See engine-fact memories.

internal static class StyleAccumulator
{
    static StyleList Rent(StyleList current)
        => ReferenceEquals(current, StyleList.Empty)
            ? BuildContext.Current.RentStyleList()
            : current;

    public static StyleList Add(StyleList current, StyleField field, Length? value)
    {
        if (!value.HasValue) return current;
        var list = Rent(current);
        list.Add(field, StyleValue.FromLength(value.Value));
        return list;
    }

    public static StyleList Add<TEnum>(StyleList current, StyleField field, TEnum? value, System.Func<TEnum, StyleValue> wrap) where TEnum : struct
    {
        if (!value.HasValue) return current;
        var list = Rent(current);
        list.Add(field, wrap(value.Value));
        return list;
    }

    public static StyleList Add(StyleList current, StyleField field, Color? value, System.Func<Color, StyleValue> wrap)
    {
        if (!value.HasValue) return current;
        var list = Rent(current);
        list.Add(field, wrap(value.Value));
        return list;
    }

    public static StyleList Add(StyleList current, StyleField field, string? value)
    {
        if (value is null) return current;
        var list = Rent(current);
        list.Add(field, StyleValue.FromString(value));
        return list;
    }

    public static StyleList Add(StyleList current, StyleField field, float? value)
    {
        if (!value.HasValue) return current;
        var list = Rent(current);
        list.Add(field, StyleValue.FromSingle(value.Value));
        return list;
    }

    public static StyleList Add(StyleList current, StyleField field, bool? value)
    {
        if (!value.HasValue) return current;
        var list = Rent(current);
        list.Add(field, StyleValue.FromBoolean(value.Value));
        return list;
    }

    public static StyleList Add(StyleList current, StyleField field, int? value)
    {
        if (!value.HasValue) return current;
        var list = Rent(current);
        list.Add(field, StyleValue.FromInt32(value.Value));
        return list;
    }

    public static StyleList Add(StyleList current, StyleField field, Texture? value)
    {
        if (value is null) return current;
        var list = Rent(current);
        list.Add(field, StyleValue.FromTexture(value));
        return list;
    }
}

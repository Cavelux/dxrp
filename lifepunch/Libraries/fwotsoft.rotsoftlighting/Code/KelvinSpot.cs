using Sandbox;
using System;

namespace RotSoft;

/// <summary>
/// A Spotlight with a kelvin temperature gradient bar.
/// </summary>
public class KelvinTemperatureAttribute : System.Attribute { }

public sealed class KelvinSpot : SpotLight
{
    public new Color LightColor
    {
        get => base.LightColor;
        set => base.LightColor = value;
    }

    float _temperature = 5500f;

    [Property, Range(1000f, 20000f), Step(10f), KelvinTemperature]
    public float Temperature
    {
        get => _temperature;
        set
        {
            if (_temperature == value) return;
            _temperature = value;
            ApplyColor();
        }
    }

    float _brightness = 1f;

    [Property, Range(0.1f, 100f), Step(0.1f), Title("Brightness")]
    public float Range
    {
        get => _brightness;
        set
        {
            if (_brightness == value) return;
            _brightness = value;
            ApplyColor();
        }
    }

    void ApplyColor()
    {
        LightColor = KelvinToColor(_temperature) * _brightness;
    }

    /// <summary>
    /// Approximate kelvin → sRGB. Based on Tanner Helland's algorithm.
    /// </summary>
    public static Color KelvinToColor(float kelvin)
    {
        float temp = kelvin / 100f;
        float r, g, b;

        if (temp <= 66f)
            r = 1f;
        else
            r = Math.Clamp(1.29293618606f * MathF.Pow(temp - 60f, -0.1332047592f), 0f, 1f);

        if (temp <= 66f)
            g = Math.Clamp(0.390081578769f * MathF.Log(temp) - 0.63184144378f, 0f, 1f);
        else
            g = Math.Clamp(1.12989086369f * MathF.Pow(temp - 60f, -0.0755148492f), 0f, 1f);

        if (temp >= 66f)
            b = 1f;
        else if (temp <= 19f)
            b = 0f;
        else
            b = Math.Clamp(0.54320678911f * MathF.Log(temp - 10f) - 1.19625408914f, 0f, 1f);

        return new Color(r, g, b);
    }
}

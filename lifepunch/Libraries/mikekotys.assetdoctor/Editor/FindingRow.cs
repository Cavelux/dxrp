#nullable enable
using System;

namespace AssetDoctor;

/// <summary>Provides a styled clickable row without native button rendering.</summary>
public sealed class FindingRow : Widget
{
    /// <summary>Displays the row text.</summary>
    private readonly Label _label;
    /// <summary>Raised when the row receives a mouse click.</summary>
    public event Action? Clicked;
    /// <summary>Gets or sets the visible row text.</summary>
    public string Text { get => _label.Text; set => _label.Text = value; }
    /// <summary>Creates a styled row using caller-supplied colors.</summary>
    public FindingRow(Widget parent, string text, string background, string border, string color) : base(parent)
    {
        MinimumSize = new Vector2(0, 24); Layout = Layout.Row(); Layout.Margin = 0; Layout.Spacing = 0;
        SetStyles($"background-color: {background}; border: 1px solid {border};");
        _label = new Label(text, this) { TransparentForMouseEvents = true };
        _label.SetStyles($"color: {color}; padding: 0px 6px; background-color: transparent;"); Layout.Add(_label);
        MouseClick += OnMouseClick;
    }
    /// <summary>Raises the row click event.</summary>
    private void OnMouseClick() => Clicked?.Invoke();
}

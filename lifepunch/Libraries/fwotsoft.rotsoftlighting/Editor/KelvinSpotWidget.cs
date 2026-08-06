using Editor;
using Sandbox;
using Sandbox.UI;
using System;
namespace RotSoft;

[CustomEditor(typeof(float), WithAllAttributes = new[] { typeof(KelvinTemperatureAttribute) })]
public class KelvinTemperatureWidget : ControlWidget
{
    public override bool SupportsMultiEdit => false;
    public override bool IncludeLabel => false;

    public KelvinTemperatureWidget(SerializedProperty property) : base(property)
    {
        Layout = Layout.Column();
        Layout.Spacing = 2;
        Layout.Margin = new Margin(0, 0, 0, 20);

        var bar = Layout.Add(new ClickableKelvinBar(this, property));
        bar.FixedHeight = 44;
    }

    protected override void OnPaint() { }

    internal class ClickableKelvinBar : Widget
    {
        SerializedProperty _prop;
        bool _grabbed;

        public ClickableKelvinBar(Widget parent, SerializedProperty prop) : base(parent)
        {
            _prop = prop;
            Cursor = CursorShape.Finger;
            HorizontalSizeMode = SizeMode.CanGrow;
            FocusMode = FocusMode.Click;
        }

        protected override void OnPaint()
        {
            var rect = LocalRect.Shrink(0);
            var gradientRect = new Rect(rect.Left, rect.Top, rect.Width, rect.Height - 16);
            int steps = Math.Max((int)gradientRect.Width, 1);

            Paint.ClearPen();

            for (int i = 0; i < steps; i++)
            {
                float frac = (float)i / steps;
                float kelvin = 1000f + frac * 19000f;
                Paint.SetBrush(KelvinSpot.KelvinToColor(kelvin));
                Paint.DrawRect(new Rect(gradientRect.Left + i, gradientRect.Top, 1f, gradientRect.Height));
            }

            if (_prop is null) return;

            float t = _prop.GetValue<float>();
            float handleFrac = (t - 1000f) / 19000f;
            float hx = gradientRect.Left + handleFrac * gradientRect.Width;

            Paint.ClearPen();
            Paint.SetBrush(Color.Black.WithAlpha(0.35f));
            Paint.DrawRect(new Rect(hx - 4, gradientRect.Top, 8, gradientRect.Height), 2f);
            Paint.SetBrush(Color.White);
            Paint.DrawRect(new Rect(hx - 3, gradientRect.Top + 1, 6, gradientRect.Height - 2), 2f);

            Paint.SetPen(Theme.Text);
            Paint.SetDefaultFont();
            Paint.DrawText(new Rect(rect.Left, gradientRect.Bottom, rect.Width, 16), $"{t:0}K", TextFlag.Center | TextFlag.SingleLine);
        }

        protected override void OnMousePress(MouseEvent e)
        {
            base.OnMousePress(e);
            _grabbed = true;
            SetFromPosition(e.LocalPosition.x);
            e.Accepted = true;
        }

        protected override void OnMouseReleased(MouseEvent e)
        {
            base.OnMouseReleased(e);
            _grabbed = false;
        }

        protected override void OnMouseMove(MouseEvent e)
        {
            base.OnMouseMove(e);
            if (!_grabbed) return;
            SetFromPosition(e.LocalPosition.x);
        }

        void SetFromPosition(float localX)
        {
            if (_prop is null) return;
            float frac = Math.Clamp(localX / Width, 0f, 1f);
            _prop.SetValue(1000f + frac * 19000f);
        }
    }
}
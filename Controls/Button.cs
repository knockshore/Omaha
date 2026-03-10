using Omaha.Events;
using SkiaSharp;

namespace Omaha.Controls;

/// <summary>
/// A clickable button control with text label.
/// Supports normal, hover, pressed, and disabled visual states.
/// </summary>
public class Button : Control
{
    private string _text = string.Empty;
    private bool _isPressed;

    public string Text
    {
        get => _text;
        set { _text = value; Invalidate(); }
    }

    public bool IsPrimary { get; set; }
    public float FontSize { get; set; }

    /// <summary>
    /// Fired when the button is clicked.
    /// </summary>
    public event Action? Clicked;

    public Button()
    {
        PreferredWidth = 120;
        PreferredHeight = 36;
        MinWidth = 60;
        MinHeight = 28;
    }

    public Button(string text) : this()
    {
        _text = text;
    }

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;

        canvas.Save();
        canvas.Translate(X, Y);

        // Determine background color based on state
        SKColor bgColor;
        SKColor textColor;

        if (!IsEnabled)
        {
            bgColor = Theme.ControlBackground;
            textColor = Theme.TextDisabled;
        }
        else if (_isPressed)
        {
            bgColor = IsPrimary ? Theme.PrimaryPressed : Theme.ControlBackgroundPressed;
            textColor = IsPrimary ? Theme.PrimaryText : Theme.TextPrimary;
        }
        else if (IsHovered)
        {
            bgColor = IsPrimary ? Theme.PrimaryHover : Theme.ControlBackgroundHover;
            textColor = IsPrimary ? Theme.PrimaryText : Theme.TextPrimary;
        }
        else
        {
            bgColor = IsPrimary ? Theme.Primary : Theme.ControlBackground;
            textColor = IsPrimary ? Theme.PrimaryText : Theme.TextPrimary;
        }

        // Draw background with rounded corners
        var rect = new SKRoundRect(LocalBounds, Theme.BorderRadius);
        using var bgPaint = new SKPaint { Color = bgColor, IsAntialias = true };
        canvas.DrawRoundRect(rect, bgPaint);

        // Draw border (non-primary only)
        if (!IsPrimary)
        {
            using var borderPaint = new SKPaint
            {
                Color = IsFocused ? Theme.BorderFocused : Theme.Border,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Theme.BorderWidth,
                IsAntialias = true
            };
            canvas.DrawRoundRect(rect, borderPaint);
        }

        // Draw text
        using var textPaint = Theme.CreateTextPaint(
            FontSize > 0 ? FontSize : Theme.FontSizeNormal,
            textColor,
            IsPrimary);
        float textWidth = textPaint.MeasureText(Text);
        float textX = (Width - textWidth) / 2;
        float textY = Height / 2 + textPaint.TextSize / 3;
        canvas.DrawText(Text, textX, textY, textPaint);

        canvas.Restore();
    }

    public override bool HandleEvent(OmahaEvent evt)
    {
        if (!IsVisible || !IsEnabled) return false;

        switch (evt)
        {
            case MouseMoveEvent move:
                bool wasHovered = IsHovered;
                IsHovered = HitTest(move.X, move.Y);
                if (!IsHovered) _isPressed = false;
                if (wasHovered != IsHovered) Invalidate();
                return false;

            case MouseButtonEvent click:
                if (HitTest(click.X, click.Y))
                {
                    _isPressed = click.IsPressed;
                    if (!click.IsPressed && IsHovered)
                    {
                        Clicked?.Invoke();
                    }
                    Invalidate();
                    return true;
                }
                _isPressed = false;
                return false;
        }

        return false;
    }
}

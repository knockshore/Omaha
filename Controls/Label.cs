using SkiaSharp;

namespace Omaha.Controls;

/// <summary>
/// A text label control. Renders a single or multi-line text string.
/// </summary>
public class Label : Control
{
    private string _text = string.Empty;

    public string Text
    {
        get => _text;
        set { _text = value; Invalidate(); }
    }

    public float FontSize { get; set; }
    public SKColor? TextColor { get; set; }
    public bool Bold { get; set; }
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;

    public Label()
    {
        PreferredHeight = 24;
    }

    public Label(string text) : this()
    {
        _text = text;
    }

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible || string.IsNullOrEmpty(Text)) return;

        canvas.Save();
        canvas.Translate(X, Y);

        using var paint = Theme.CreateTextPaint(
            FontSize > 0 ? FontSize : Theme.FontSizeNormal,
            TextColor ?? Theme.TextPrimary,
            Bold);

        float textY = Height / 2 + paint.TextSize / 3; // Approximate vertical center
        float textX = Alignment switch
        {
            TextAlignment.Center => Width / 2 - paint.MeasureText(Text) / 2,
            TextAlignment.Right => Width - paint.MeasureText(Text) - Theme.Padding,
            _ => Theme.Padding
        };

        canvas.DrawText(Text, textX, textY, paint);
        canvas.Restore();
    }

    public override SKSize Measure(float availableWidth, float availableHeight)
    {
        using var paint = Theme.CreateTextPaint(
            FontSize > 0 ? FontSize : Theme.FontSizeNormal, bold: Bold);
        float textWidth = paint.MeasureText(Text) + Theme.Padding * 2;
        float textHeight = paint.TextSize + Theme.Padding;

        float effMaxW = Math.Max(MinWidth, Math.Min(MaxWidth, availableWidth));
        float effMaxH = Math.Max(MinHeight, Math.Min(MaxHeight, availableHeight));
        return new SKSize(
            Math.Clamp(textWidth, MinWidth, effMaxW),
            Math.Clamp(textHeight, MinHeight, effMaxH));
    }
}

public enum TextAlignment { Left, Center, Right }

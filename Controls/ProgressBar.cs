using SkiaSharp;

namespace Omaha.Controls;

/// <summary>
/// A progress bar control. Supports determinate and indeterminate modes.
/// </summary>
public class ProgressBar : Control
{
    private float _value;
    private bool _isIndeterminate;
    private float _animationOffset;

    public float Value
    {
        get => _value;
        set { _value = Math.Clamp(value, 0, 100); Invalidate(); }
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set { _isIndeterminate = value; Invalidate(); }
    }

    public string StatusText { get; set; } = string.Empty;

    public ProgressBar()
    {
        PreferredHeight = 24;
        MinHeight = 6;
    }

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;

        canvas.Save();
        canvas.Translate(X, Y);

        // Track background
        var trackRect = new SKRoundRect(
            new SKRect(0, Height - Theme.ProgressHeight, Width, Height),
            Theme.ProgressHeight / 2);
        using var trackPaint = new SKPaint { Color = Theme.ProgressBackground, IsAntialias = true };
        canvas.DrawRoundRect(trackRect, trackPaint);

        // Fill
        if (IsIndeterminate)
        {
            float barWidth = Width * 0.3f;
            float barX = (_animationOffset % (Width + barWidth)) - barWidth;
            var fillRect = new SKRoundRect(
                new SKRect(Math.Max(0, barX), Height - Theme.ProgressHeight,
                    Math.Min(Width, barX + barWidth), Height),
                Theme.ProgressHeight / 2);
            using var fillPaint = new SKPaint { Color = Theme.ProgressForeground, IsAntialias = true };
            canvas.DrawRoundRect(fillRect, fillPaint);
            _animationOffset += 3;
        }
        else
        {
            float fillWidth = Width * (_value / 100f);
            if (fillWidth > 0)
            {
                var fillRect = new SKRoundRect(
                    new SKRect(0, Height - Theme.ProgressHeight, fillWidth, Height),
                    Theme.ProgressHeight / 2);
                using var fillPaint = new SKPaint { Color = Theme.ProgressForeground, IsAntialias = true };
                canvas.DrawRoundRect(fillRect, fillPaint);
            }
        }

        // Status text above bar
        if (!string.IsNullOrEmpty(StatusText))
        {
            using var textPaint = Theme.CreateTextPaint(Theme.FontSizeSmall, Theme.TextSecondary);
            float textY = Height - Theme.ProgressHeight - 4;
            canvas.DrawText(StatusText, 0, textY, textPaint);
        }

        canvas.Restore();
    }
}

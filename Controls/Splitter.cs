using SkiaSharp;

namespace Omaha.Controls;

/// <summary>
/// A resizable splitter that divides space between two children.
/// Equivalent to QSplitter in the original PyQt5 app.
/// </summary>
public class Splitter : Container
{
    private float _splitRatio = 0.3f;
    private bool _isDragging;
    private readonly float _handleWidth = 6f;

    public SplitterOrientation Orientation { get; set; } = SplitterOrientation.Horizontal;

    public float SplitRatio
    {
        get => _splitRatio;
        set { _splitRatio = Math.Clamp(value, 0.1f, 0.9f); Invalidate(); }
    }

    /// <summary>
    /// Set the two panels (left/right or top/bottom).
    /// </summary>
    public void SetPanels(Control first, Control second)
    {
        Clear();
        Add(first);
        Add(second);
    }

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible || Children.Count < 2) return;

        // Arrange children based on split
        ArrangeChildren();

        canvas.Save();
        canvas.Translate(X, Y);

        // Render first panel
        Children[0].Render(canvas);

        // Draw handle
        using var handlePaint = new SKPaint
        {
            Color = _isDragging ? Theme.Primary : Theme.Border,
            IsAntialias = true
        };

        if (Orientation == SplitterOrientation.Horizontal)
        {
            float handleX = Width * _splitRatio - _handleWidth / 2;
            canvas.DrawRect(handleX, 0, _handleWidth, Height, handlePaint);

            // Handle grip dots
            using var dotPaint = new SKPaint { Color = Theme.TextSecondary, IsAntialias = true };
            float cx = handleX + _handleWidth / 2;
            for (float dy = Height / 2 - 15; dy <= Height / 2 + 15; dy += 10)
            {
                canvas.DrawCircle(cx, dy, 1.5f, dotPaint);
            }
        }
        else
        {
            float handleY = Height * _splitRatio - _handleWidth / 2;
            canvas.DrawRect(0, handleY, Width, _handleWidth, handlePaint);
        }

        // Render second panel
        Children[1].Render(canvas);

        canvas.Restore();
    }

    private void ArrangeChildren()
    {
        if (Children.Count < 2) return;

        if (Orientation == SplitterOrientation.Horizontal)
        {
            float splitX = Width * _splitRatio;
            Children[0].Arrange(0, 0, splitX - _handleWidth / 2, Height);
            Children[1].Arrange(splitX + _handleWidth / 2, 0,
                Width - splitX - _handleWidth / 2, Height);
        }
        else
        {
            float splitY = Height * _splitRatio;
            Children[0].Arrange(0, 0, Width, splitY - _handleWidth / 2);
            Children[1].Arrange(0, splitY + _handleWidth / 2,
                Width, Height - splitY - _handleWidth / 2);
        }
    }

    public override bool HandleEvent(Events.OmahaEvent evt)
    {
        if (evt is Events.MouseButtonEvent click)
        {
            if (click.IsPressed && IsOnHandle(click.X, click.Y))
            {
                _isDragging = true;
                Invalidate();
                return true;
            }
            if (!click.IsPressed)
            {
                _isDragging = false;
                Invalidate();
            }
        }

        if (evt is Events.MouseMoveEvent move && _isDragging)
        {
            if (Orientation == SplitterOrientation.Horizontal)
            {
                SplitRatio = (move.X - AbsoluteX) / Width;
            }
            else
            {
                SplitRatio = (move.Y - AbsoluteY) / Height;
            }
            return true;
        }

        return base.HandleEvent(evt);
    }

    private bool IsOnHandle(float absX, float absY)
    {
        float localX = absX - AbsoluteX;
        float localY = absY - AbsoluteY;

        if (Orientation == SplitterOrientation.Horizontal)
        {
            float handleX = Width * _splitRatio;
            return Math.Abs(localX - handleX) < _handleWidth * 2 &&
                   localY >= 0 && localY <= Height;
        }
        else
        {
            float handleY = Height * _splitRatio;
            return Math.Abs(localY - handleY) < _handleWidth * 2 &&
                   localX >= 0 && localX <= Width;
        }
    }
}

public enum SplitterOrientation { Horizontal, Vertical }

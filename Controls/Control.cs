using Omaha.Events;
using Omaha.Theming;
using SkiaSharp;

namespace Omaha.Controls;

/// <summary>
/// Base class for all Omaha UI controls.
/// Provides layout, rendering, and event handling infrastructure.
/// </summary>
public abstract class Control
{
    // Layout properties
    public float X { get; set; }
    public float Y { get; set; }
            public float Width { get; set; }
    public float Height { get; set; }

    // Constraints (used by layout system)
    public float MinWidth { get; set; }
    public float MinHeight { get; set; }
    public float MaxWidth { get; set; } = float.MaxValue;
    public float MaxHeight { get; set; } = float.MaxValue;
    public float PreferredWidth { get; set; }
    public float PreferredHeight { get; set; }

    // Flex layout
    public int Flex { get; set; }
    public Margin Margin { get; set; } = Margin.Zero;

    // State
    public bool IsVisible { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public bool IsFocused { get; set; }
    public bool IsHovered { get; set; }

    // Identification
    public string Name { get; set; } = string.Empty;
    public string ToolTip { get; set; } = string.Empty;

    // Theme
    public OmahaTheme Theme { get; set; } = OmahaTheme.Light;

    // Parent reference
    public Control? Parent { get; internal set; }

    /// <summary>
    /// Absolute X position (accounting for parent offsets).
    /// </summary>
    public float AbsoluteX => X + (Parent?.AbsoluteX ?? 0);

    /// <summary>
    /// Absolute Y position (accounting for parent offsets).
    /// </summary>
    public float AbsoluteY => Y + (Parent?.AbsoluteY ?? 0);

    /// <summary>
    /// Bounding rectangle in absolute coordinates.
    /// </summary>
    public SKRect AbsoluteBounds => new(AbsoluteX, AbsoluteY, AbsoluteX + Width, AbsoluteY + Height);

    /// <summary>
    /// Local bounding rectangle.
    /// </summary>
    public SKRect LocalBounds => new(0, 0, Width, Height);

    /// <summary>
    /// Check if a point (in absolute coordinates) is inside this control.
    /// </summary>
    public virtual bool HitTest(float x, float y)
    {
        return IsVisible && AbsoluteBounds.Contains(x, y);
    }

    /// <summary>
    /// Render this control using the provided SkiaSharp canvas.
    /// </summary>
    public abstract void Render(SKCanvas canvas);

    /// <summary>
    /// Handle a UI event. Return true if the event was consumed.
    /// </summary>
    public virtual bool HandleEvent(OmahaEvent evt)
    {
        if (!IsVisible || !IsEnabled) return false;

        switch (evt)
        {
            case MouseMoveEvent move:
                bool wasHovered = IsHovered;
                IsHovered = HitTest(move.X, move.Y);
                if (wasHovered != IsHovered) Invalidate();
                break;

            case MouseButtonEvent click when click.IsPressed:
                if (HitTest(click.X, click.Y))
                {
                    OnClick(click);
                    return true;
                }
                break;
        }

        return false;
    }

    /// <summary>
    /// Called when the control is clicked.
    /// </summary>
    protected virtual void OnClick(MouseButtonEvent evt) { }

    /// <summary>
    /// Request a visual redraw.
    /// </summary>
    public void Invalidate()
    {
        // Walk up to root window to trigger repaint
        InvalidateRequested?.Invoke();
    }

    /// <summary>
    /// Event fired when the control needs repainting.
    /// </summary>
    public event Action? InvalidateRequested;

    /// <summary>
    /// Measure the desired size of this control.
    /// </summary>
    public virtual SKSize Measure(float availableWidth, float availableHeight)
    {
        // Guard: the effective max must never fall below the min (can happen when
        // availableWidth/Height is smaller than MinWidth/MinHeight).
        float effectiveMaxW = Math.Max(MinWidth, Math.Min(MaxWidth, availableWidth));
        float effectiveMaxH = Math.Max(MinHeight, Math.Min(MaxHeight, availableHeight));
        return new SKSize(
            Math.Clamp(PreferredWidth, MinWidth, effectiveMaxW),
            Math.Clamp(PreferredHeight, MinHeight, effectiveMaxH));
    }

    /// <summary>
    /// Arrange this control in the given rectangle.
    /// </summary>
    public virtual void Arrange(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = Math.Clamp(width, MinWidth, Math.Max(MinWidth, MaxWidth));
        Height = Math.Clamp(height, MinHeight, Math.Max(MinHeight, MaxHeight));
    }
}

/// <summary>
/// Margin/padding values for layout.
/// </summary>
public record struct Margin(float Left, float Top, float Right, float Bottom)
{
    public static Margin Zero => new(0, 0, 0, 0);
    public static Margin All(float value) => new(value, value, value, value);
    public static Margin Symmetric(float horizontal, float vertical) =>
        new(horizontal, vertical, horizontal, vertical);

    public float HorizontalTotal => Left + Right;
    public float VerticalTotal => Top + Bottom;
}

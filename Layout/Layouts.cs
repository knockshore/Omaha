using Omaha.Controls;
using SkiaSharp;

namespace Omaha.Layout;

/// <summary>
/// Vertical box layout - arranges children top to bottom.
/// Supports flex-based sizing for children.
/// </summary>
public class VBox : Container
{
    public float Spacing { get; set; } = 6f;
    public float Padding { get; set; } = 8f;

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;

        LayoutChildren();
        base.Render(canvas);
    }

    private void LayoutChildren()
    {
        float availableHeight = Math.Max(0, Height - Padding * 2);
        float availableWidth  = Math.Max(0, Width  - Padding * 2);
        float y = Padding;

        // First pass: measure fixed-size children
        float totalFixed = 0;
        int totalFlex = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            if (child.Flex > 0)
            {
                totalFlex += child.Flex;
            }
            else
            {
                var size = child.Measure(availableWidth, availableHeight);
                totalFixed += size.Height + child.Margin.VerticalTotal;
            }
        }

        float totalSpacing = Spacing * Math.Max(0, Children.Count(c => c.IsVisible) - 1);
        float flexSpace = Math.Max(0, availableHeight - totalFixed - totalSpacing);

        // Second pass: arrange children
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            y += child.Margin.Top;

            float childWidth = Math.Max(0, availableWidth - child.Margin.HorizontalTotal);
            float childHeight;

            if (child.Flex > 0)
            {
                childHeight = flexSpace * child.Flex / totalFlex;
            }
            else
            {
                var size = child.Measure(childWidth, availableHeight);
                childHeight = size.Height;
            }

            child.Arrange(Padding + child.Margin.Left, y, childWidth, childHeight);
            y += childHeight + child.Margin.Bottom + Spacing;
        }
    }

    public override SKSize Measure(float availableWidth, float availableHeight)
    {
        float totalHeight = Padding * 2;
        float maxWidth = 0;
        float childAvailW = Math.Max(0, availableWidth - Padding * 2);

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            var size = child.Measure(childAvailW, Math.Max(0, availableHeight));
            totalHeight += size.Height + child.Margin.VerticalTotal + Spacing;
            maxWidth = Math.Max(maxWidth, size.Width + child.Margin.HorizontalTotal);
        }

        return new SKSize(
            Math.Clamp(maxWidth + Padding * 2, MinWidth, Math.Max(MinWidth, MaxWidth)),
            Math.Clamp(totalHeight, MinHeight, Math.Max(MinHeight, MaxHeight)));
    }
}

/// <summary>
/// Horizontal box layout - arranges children left to right.
/// Supports flex-based sizing for children.
/// </summary>
public class HBox : Container
{
    public float Spacing { get; set; } = 6f;
    public float Padding { get; set; } = 8f;

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;

        LayoutChildren();
        base.Render(canvas);
    }

    private void LayoutChildren()
    {
        float availableWidth  = Math.Max(0, Width  - Padding * 2);
        float availableHeight = Math.Max(0, Height - Padding * 2);
        float x = Padding;

        // First pass: measure fixed-size children
        float totalFixed = 0;
        int totalFlex = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            if (child.Flex > 0)
            {
                totalFlex += child.Flex;
            }
            else
            {
                var size = child.Measure(availableWidth, availableHeight);
                totalFixed += size.Width + child.Margin.HorizontalTotal;
            }
        }

        float totalSpacing = Spacing * Math.Max(0, Children.Count(c => c.IsVisible) - 1);
        float flexSpace = Math.Max(0, availableWidth - totalFixed - totalSpacing);

        // Second pass: arrange children
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            x += child.Margin.Left;

            float childHeight = Math.Max(0, availableHeight - child.Margin.VerticalTotal);
            float childWidth;

            if (child.Flex > 0)
            {
                childWidth = flexSpace * child.Flex / totalFlex;
            }
            else
            {
                var size = child.Measure(availableWidth, childHeight);
                childWidth = size.Width;
            }

            child.Arrange(x, Padding + child.Margin.Top, childWidth, childHeight);
            x += childWidth + child.Margin.Right + Spacing;
        }
    }

    public override SKSize Measure(float availableWidth, float availableHeight)
    {
        float totalWidth = Padding * 2;
        float maxHeight = 0;
        float childAvailH = Math.Max(0, availableHeight - Padding * 2);

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            var size = child.Measure(Math.Max(0, availableWidth), childAvailH);
            totalWidth += size.Width + child.Margin.HorizontalTotal + Spacing;
            maxHeight = Math.Max(maxHeight, size.Height + child.Margin.VerticalTotal);
        }

        return new SKSize(
            Math.Clamp(totalWidth, MinWidth, Math.Max(MinWidth, MaxWidth)),
            Math.Clamp(maxHeight + Padding * 2, MinHeight, Math.Max(MinHeight, MaxHeight)));
    }
}

/// <summary>
/// Simple panel with optional background and border.
/// </summary>
public class Panel : Container
{
    public SKColor? BackgroundColor { get; set; }
    public bool ShowBorder { get; set; }
    public bool ShowShadow { get; set; }

    /// <summary>
    /// Panels always fill the single child to their own bounds so that
    /// wrapping a VBox/HBox in a Panel for background/border purposes
    /// does not break layout.  Layout only happens in Arrange (not Render)
    /// to avoid triggering redraws every frame.
    /// </summary>
    public override void Arrange(float x, float y, float width, float height)
    {
        base.Arrange(x, y, width, height);
        if (Children.Count == 1)
            Children[0].Arrange(0, 0, width, height);
    }

    public override SKSize Measure(float availableWidth, float availableHeight)
    {
        if (Children.Count == 1)
        {
            var s = Children[0].Measure(availableWidth, availableHeight);
            // Honour our own PreferredHeight if set explicitly
            float h = PreferredHeight > 0 ? PreferredHeight : s.Height;
            float w = PreferredWidth  > 0 ? PreferredWidth  : s.Width;
            return new SKSize(
                Math.Clamp(w, MinWidth,  Math.Max(MinWidth,  MaxWidth)),
                Math.Clamp(h, MinHeight, Math.Max(MinHeight, MaxHeight)));
        }
        return base.Measure(availableWidth, availableHeight);
    }

    protected override void RenderBackground(SKCanvas canvas)
    {
        var bg = BackgroundColor ?? Theme.PanelBackground;
        var rect = new SKRoundRect(LocalBounds, Theme.BorderRadius);

        if (ShowShadow)
        {
            using var shadowPaint = new SKPaint
            {
                Color = Theme.ShadowColor,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, Theme.ShadowBlur),
                IsAntialias = true
            };
            canvas.DrawRoundRect(rect, shadowPaint);
        }

        using var bgPaint = new SKPaint { Color = bg, IsAntialias = true };
        canvas.DrawRoundRect(rect, bgPaint);

        if (ShowBorder)
        {
            using var borderPaint = new SKPaint
            {
                Color = Theme.Border,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Theme.BorderWidth,
                IsAntialias = true
            };
            canvas.DrawRoundRect(rect, borderPaint);
        }
    }
}

using Omaha.Controls;
using SkiaSharp;

namespace Omaha.Layout;

/// <summary>
/// Wrapping flow layout — arranges children left-to-right, wrapping to the
/// next row when a row is full. Useful for icon grids like a dashboard.
/// </summary>
public class WrapLayout : Container
{
    public float ItemSpacingH { get; set; } = 16f;
    public float ItemSpacingV { get; set; } = 16f;
    public float Padding { get; set; } = 16f;

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;

        LayoutChildren();
        base.Render(canvas);
    }

    private void LayoutChildren()
    {
        float available = Width - Padding * 2;
        float x = Padding;
        float y = Padding;
        float rowHeight = 0;

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            var size = child.Measure(child.PreferredWidth > 0 ? child.PreferredWidth : available, Height);
            float childW = size.Width;
            float childH = size.Height;

            // Wrap if the child won't fit on the current row
            if (x + childW > Width - Padding && x > Padding)
            {
                x = Padding;
                y += rowHeight + ItemSpacingV;
                rowHeight = 0;
            }

            child.Arrange(x, y, childW, childH);
            x += childW + ItemSpacingH;
            rowHeight = Math.Max(rowHeight, childH);
        }
    }

    public override SKSize Measure(float availableWidth, float availableHeight)
    {
        float available = availableWidth - Padding * 2;
        float x = 0;
        float y = Padding;
        float rowHeight = 0;

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            var size = child.Measure(child.PreferredWidth > 0 ? child.PreferredWidth : available, availableHeight);

            if (x + size.Width > available && x > 0)
            {
                x = 0;
                y += rowHeight + ItemSpacingV;
                rowHeight = 0;
            }

            x += size.Width + ItemSpacingH;
            rowHeight = Math.Max(rowHeight, size.Height);
        }

        float totalHeight = y + rowHeight + Padding;
        return new SKSize(
            Math.Clamp(availableWidth, MinWidth, Math.Max(MinWidth, MaxWidth)),
            Math.Clamp(totalHeight, MinHeight, Math.Max(MinHeight, MaxHeight)));
    }
}

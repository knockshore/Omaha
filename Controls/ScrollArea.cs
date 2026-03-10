using Omaha.Events;
using SkiaSharp;

namespace Omaha.Controls;

/// <summary>
/// A scrollable area that can contain a single child that is larger than the viewport.
/// Equivalent to QScrollArea in the original PyQt5 app.
/// </summary>
    public class ScrollArea : Container
{
    private float _scrollX;
    private float _scrollY;
    private float _contentWidth;
    private float _contentHeight;

    public float ScrollX
    {
        get => _scrollX;
        set { _scrollX = Math.Max(0, value); Invalidate(); }
    }

    public float ScrollY
    {
        get => _scrollY;
        set { _scrollY = Math.Max(0, value); Invalidate(); }
    }

    /// <summary>
    /// Set the virtual content size (may be larger than the scroll area).
    /// </summary>
    public void SetContentSize(float width, float height)
    {
        _contentWidth = width;
        _contentHeight = height;
    }

    /// <summary>
    /// On arrange, measure and stack children vertically at our viewport width,
    /// then set the virtual content size.  This runs only when the layout
    /// actually changes (resize, child added), not every frame.
    /// </summary>
    public override void Arrange(float x, float y, float width, float height)
    {
        base.Arrange(x, y, width, height);
        RelayoutChildren();
    }

    private void RelayoutChildren()
    {
        float contentH = 0;
        float contentW = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            var size = child.Measure(Width, float.MaxValue);
            child.Arrange(0, contentH, Width, size.Height);
            contentH += size.Height;
            contentW = Math.Max(contentW, size.Width);
        }
        _contentWidth  = Math.Max(contentW, Width);
        _contentHeight = Math.Max(contentH, Height);
    }

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;

        canvas.Save();
        canvas.Translate(X, Y);
        canvas.ClipRect(LocalBounds);

        // Background
        using var bgPaint = new SKPaint { Color = Theme.PanelBackground };
        canvas.DrawRect(LocalBounds, bgPaint);

        // Content with scroll offset
        canvas.Save();
        canvas.Translate(-_scrollX, -_scrollY);

        foreach (var child in Children)
        {
            if (child.IsVisible)
                child.Render(canvas);
        }

        canvas.Restore();

        // Vertical scrollbar
        if (_contentHeight > Height)
        {
            float ratio = Height / _contentHeight;
            float thumbH = Math.Max(20, Height * ratio);
            float thumbY = (_scrollY / _contentHeight) * Height;

            using var trackPaint = new SKPaint { Color = Theme.ScrollbarTrack };
            canvas.DrawRect(Width - Theme.ScrollbarWidth, 0, Theme.ScrollbarWidth, Height, trackPaint);

            var thumbRect = new SKRect(Width - Theme.ScrollbarWidth, thumbY,
                Width, thumbY + thumbH);
            using var thumbPaint = new SKPaint { Color = Theme.ScrollbarThumb, IsAntialias = true };
            canvas.DrawRoundRect(new SKRoundRect(thumbRect, 4), thumbPaint);
        }

        // Horizontal scrollbar
        if (_contentWidth > Width)
        {
            float ratio = Width / _contentWidth;
            float thumbW = Math.Max(20, Width * ratio);
            float thumbX = (_scrollX / _contentWidth) * Width;

            using var trackPaint = new SKPaint { Color = Theme.ScrollbarTrack };
            canvas.DrawRect(0, Height - Theme.ScrollbarWidth, Width, Theme.ScrollbarWidth, trackPaint);

            var thumbRect = new SKRect(thumbX, Height - Theme.ScrollbarWidth,
                thumbX + thumbW, Height);
            using var thumbPaint = new SKPaint { Color = Theme.ScrollbarThumb, IsAntialias = true };
            canvas.DrawRoundRect(new SKRoundRect(thumbRect, 4), thumbPaint);
        }

        canvas.Restore();
    }

    public override bool HandleEvent(OmahaEvent evt)
    {
        if (!IsVisible || !IsEnabled) return false;

        if (evt is MouseScrollEvent scroll && HitTest(scroll.X, scroll.Y))
        {
            float maxScrollY = Math.Max(0, _contentHeight - Height);
            float maxScrollX = Math.Max(0, _contentWidth - Width);

            _scrollY = Math.Clamp(_scrollY - scroll.DeltaY * 40, 0, maxScrollY);
            _scrollX = Math.Clamp(_scrollX - scroll.DeltaX * 40, 0, maxScrollX);
            Invalidate();
            return true;
        }

        return base.HandleEvent(evt);
    }
}

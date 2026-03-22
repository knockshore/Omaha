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

    /// <summary>
    /// Fired whenever ScrollX or ScrollY changes due to user input.
    /// Use this to synchronise two ScrollAreas (e.g. left/right page panels).
    /// The argument is (scrollX, scrollY).
    /// </summary>
    public event Action<float, float>? ScrollChanged;

    /// <summary>
    /// Fired at the start of every layout pass with the current viewport width.
    /// Subscribe to set an ImageView's zoom so the image fits the viewport, e.g.:
    ///   <c>scroll.ViewportWidthChanged += vw => { if (!view.IsUserZoomed) view.FitToWidth(vw); };</c>
    /// </summary>
    public event Action<float>? ViewportWidthChanged;

    /// <summary>
    /// Children are rendered offset by -_scrollX/-_scrollY, so their screen X/Y
    /// starts at this scroll area's screen position minus the scroll offset.
    /// </summary>
    public override float ChildAbsoluteX => AbsoluteX - _scrollX;
    public override float ChildAbsoluteY => AbsoluteY - _scrollY;

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
        // Notify subscribers (e.g. ImageView fit-to-width) before measuring children
        // so the updated zoom is used during Measure.
        ViewportWidthChanged?.Invoke(Width);

        float contentH = 0;
        float contentW = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            // Measure at unconstrained width/height so content larger than the
            // viewport is reported correctly and the horizontal scrollbar appears.
            var size = child.Measure(float.MaxValue, float.MaxValue);
            child.Arrange(0, contentH, Math.Max(size.Width, Width), size.Height);
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

    // ── Scrollbar thumb drag state ────────────────────────────────────────
    private enum DragAxis { None, Vertical, Horizontal }
    private DragAxis _dragAxis = DragAxis.None;
    private float _dragStartMouse;   // mouse coord when drag began
    private float _dragStartScroll;  // scroll offset when drag began

    // ── Middle-mouse content drag (pan) state ─────────────────────────────
    private bool  _panning;
    private float _panStartMouseX, _panStartMouseY;
    private float _panStartScrollX, _panStartScrollY;

    public override bool HandleEvent(OmahaEvent evt)
    {
        if (!IsVisible || !IsEnabled) return false;

        // ── Active content pan (middle-mouse drag) ─────────────────────────
        if (_panning)
        {
            if (evt is MouseMoveEvent panMove)
            {
                float maxScrollX = Math.Max(0, _contentWidth  - Width);
                float maxScrollY = Math.Max(0, _contentHeight - Height);
                _scrollX = Math.Clamp(_panStartScrollX + (_panStartMouseX - panMove.X), 0, maxScrollX);
                _scrollY = Math.Clamp(_panStartScrollY + (_panStartMouseY - panMove.Y), 0, maxScrollY);
                Invalidate();
                ScrollChanged?.Invoke(_scrollX, _scrollY);
                return true;
            }
            if (evt is MouseButtonEvent panUp && !panUp.IsPressed && panUp.Button == MouseButton.Middle)
            {
                _panning = false;
                return true;
            }
        }

        // ── Mouse scroll wheel ─────────────────────────────────────────────
        if (evt is MouseScrollEvent scroll && HitTest(scroll.X, scroll.Y))
        {
            // Children (nested scroll areas) get priority.
            for (int i = Children.Count - 1; i >= 0; i--)
                if (Children[i].HandleEvent(scroll)) return true;

            if (scroll.Ctrl)
            {
                // Ctrl + scroll → zoom all ImageView children toward the cursor.
                float factor = scroll.DeltaY != 0
                    ? (float)Math.Pow(1.1, scroll.DeltaY)
                    : 1.0f;

                bool zoomed = false;
                foreach (var child in Children)
                {
                    if (child is not ImageView imgView) continue;

                    float oldZoom = imgView.Zoom;
                    float newZoom = Math.Clamp(oldZoom * factor, 0.05f, 10f);
                    if (Math.Abs(newZoom - oldZoom) < 0.0005f) continue;

                    // Compute image-space point under the cursor so we can
                    // keep it stationary after the zoom.
                    float absX = AbsoluteX, absY = AbsoluteY;
                    float imgX = (scroll.X - absX + _scrollX) / oldZoom;
                    float imgY = (scroll.Y - absY + _scrollY) / oldZoom;

                    imgView.IsUserZoomed = true;
                    imgView.Zoom = newZoom;   // fires ZoomChanged → syncs partner panel

                    // Pan so the same image point stays under the cursor.
                    float maxScrollX = Math.Max(0, imgView.ImageWidth  - Width);
                    float maxScrollY = Math.Max(0, imgView.ImageHeight - Height);
                    _scrollX = Math.Clamp(imgX * newZoom - (scroll.X - absX), 0, maxScrollX);
                    _scrollY = Math.Clamp(imgY * newZoom - (scroll.Y - absY), 0, maxScrollY);
                    zoomed = true;
                }

                if (zoomed) ScrollChanged?.Invoke(_scrollX, _scrollY);
                Invalidate();
                return true;
            }

            // Normal scroll: only consume if this area has content to scroll in
            // the requested direction.  If not, let the event bubble to the parent
            // (e.g. the outer page-list scroll area) so it can navigate between pages.
            float maxScrollY2 = Math.Max(0, _contentHeight - Height);
            float maxScrollX2 = Math.Max(0, _contentWidth  - Width);
            bool wouldScroll = (MathF.Abs(scroll.DeltaY) > 0.01f && maxScrollY2 > 0)
                            || (MathF.Abs(scroll.DeltaX) > 0.01f && maxScrollX2 > 0);
            if (!wouldScroll) return false;  // bubble up

            _scrollY = Math.Clamp(_scrollY - scroll.DeltaY * 40, 0, maxScrollY2);
            _scrollX = Math.Clamp(_scrollX - scroll.DeltaX * 40, 0, maxScrollX2);
            Invalidate();
            ScrollChanged?.Invoke(_scrollX, _scrollY);
            return true;
        }

        // ── Scrollbar thumb drag ───────────────────────────────────────────
        float ax = AbsoluteX, ay = AbsoluteY;
        float sw = Theme.ScrollbarWidth;

        if (evt is MouseButtonEvent btn)
        {
            if (btn.IsPressed)
            {
                // ── Middle-mouse: start content pan ───────────────────────
                if (btn.Button == MouseButton.Middle && HitTest(btn.X, btn.Y))
                {
                    _panning        = true;
                    _panStartMouseX = btn.X;
                    _panStartMouseY = btn.Y;
                    _panStartScrollX = _scrollX;
                    _panStartScrollY = _scrollY;
                    return true;
                }

                // Hit-test vertical thumb
                if (_contentHeight > Height)
                {
                    float ratio  = Height / _contentHeight;
                    float thumbH = Math.Max(20, Height * ratio);
                    float thumbY = (_scrollY / _contentHeight) * Height;
                    var vTrack = new SKRect(ax + Width - sw, ay, ax + Width, ay + Height);
                    var vThumb = new SKRect(ax + Width - sw, ay + thumbY, ax + Width, ay + thumbY + thumbH);
                    if (vTrack.Contains(btn.X, btn.Y))
                    {
                        if (vThumb.Contains(btn.X, btn.Y))
                        {
                            _dragAxis        = DragAxis.Vertical;
                            _dragStartMouse  = btn.Y;
                            _dragStartScroll = _scrollY;
                        }
                        else
                        {
                            // Click on track: jump to position
                            float clickRatio = (btn.Y - ay) / Height;
                            _scrollY = Math.Clamp(clickRatio * _contentHeight - Height / 2,
                                0, Math.Max(0, _contentHeight - Height));
                            Invalidate();
                            ScrollChanged?.Invoke(_scrollX, _scrollY);
                        }
                        return true;
                    }
                }

                // Hit-test horizontal thumb
                if (_contentWidth > Width)
                {
                    float ratio  = Width / _contentWidth;
                    float thumbW = Math.Max(20, Width * ratio);
                    float thumbX = (_scrollX / _contentWidth) * Width;
                    var hTrack = new SKRect(ax, ay + Height - sw, ax + Width, ay + Height);
                    var hThumb = new SKRect(ax + thumbX, ay + Height - sw, ax + thumbX + thumbW, ay + Height);
                    if (hTrack.Contains(btn.X, btn.Y))
                    {
                        if (hThumb.Contains(btn.X, btn.Y))
                        {
                            _dragAxis        = DragAxis.Horizontal;
                            _dragStartMouse  = btn.X;
                            _dragStartScroll = _scrollX;
                        }
                        else
                        {
                            float clickRatio = (btn.X - ax) / Width;
                            _scrollX = Math.Clamp(clickRatio * _contentWidth - Width / 2,
                                0, Math.Max(0, _contentWidth - Width));
                            Invalidate();
                            ScrollChanged?.Invoke(_scrollX, _scrollY);
                        }
                        return true;
                    }
                }
            }
            else // mouse up
            {
                if (_dragAxis != DragAxis.None)
                {
                    _dragAxis = DragAxis.None;
                    return true;
                }
            }
        }

        if (evt is MouseMoveEvent move && _dragAxis != DragAxis.None)
        {
            if (_dragAxis == DragAxis.Vertical)
            {
                float delta      = move.Y - _dragStartMouse;
                float scrollable = Math.Max(1, _contentHeight - Height);
                float trackLen   = Height;
                _scrollY = Math.Clamp(_dragStartScroll + delta * (_contentHeight / trackLen),
                    0, scrollable);
            }
            else
            {
                float delta      = move.X - _dragStartMouse;
                float scrollable = Math.Max(1, _contentWidth - Width);
                float trackLen   = Width;
                _scrollX = Math.Clamp(_dragStartScroll + delta * (_contentWidth / trackLen),
                    0, scrollable);
            }
            Invalidate();
            ScrollChanged?.Invoke(_scrollX, _scrollY);
            return true;
        }

        return base.HandleEvent(evt);
    }
}

using SkiaSharp;

namespace Omaha.Controls;

/// <summary>
/// Displays a bitmap image (from SkiaSharp or PNG data) with zoom support.
/// Used for rendering PDF pages and diff visualizations.
/// </summary>
public class ImageView : Control
{
    private SKBitmap? _bitmap;
    private float _zoom = 1.0f;

    /// <summary>
    /// Fired whenever Zoom changes (user-driven or via code).
    /// The argument is the new zoom factor.
    /// </summary>
    public event Action<float>? ZoomChanged;

    /// <summary>
    /// True once the user has manually zoomed, suppressing automatic fit-to-width.
    /// </summary>
    public bool IsUserZoomed { get; set; }

    public float Zoom
    {
        get => _zoom;
        set
        {
            var clamped = Math.Clamp(value, 0.1f, 10f);
            if (Math.Abs(clamped - _zoom) > 0.0005f)
            {
                _zoom = clamped;
                Invalidate();
                ZoomChanged?.Invoke(_zoom);
            }
        }
    }

    /// <summary>
    /// Set zoom so the image width fills <paramref name="viewportWidth"/> exactly.
    /// Does NOT set IsUserZoomed and does NOT fire ZoomChanged — this is
    /// called automatically by the layout system on every frame.
    /// </summary>
    public void FitToWidth(float viewportWidth)
    {
        if (_bitmap == null || viewportWidth <= 0) return;
        float newZoom = Math.Clamp(viewportWidth / _bitmap.Width, 0.05f, 10f);
        if (Math.Abs(newZoom - _zoom) > 0.0005f)
            _zoom = newZoom;
        // No Invalidate() needed — we are called from inside a layout pass;
        // the render that follows will pick up the updated zoom.
    }

    /// <summary>
    /// Set the image from PNG byte data.
    /// </summary>
    public void SetImage(byte[] pngData)
    {
        _bitmap?.Dispose();
        
        if (pngData == null || pngData.Length == 0)
        {
            throw new ArgumentException("Error: failed to create a bitmap object - No image data provided");
        }
        
        try
        {
            _bitmap = SKBitmap.Decode(pngData);
            
            if (_bitmap == null)
            {
                throw new InvalidOperationException($"Error: failed to create a bitmap object - SKBitmap.Decode returned null (data length: {pngData.Length} bytes)");
            }
            
            if (_bitmap.Width <= 0 || _bitmap.Height <= 0)
            {
                var width = _bitmap.Width;
                var height = _bitmap.Height;
                _bitmap.Dispose();
                _bitmap = null;
                throw new InvalidOperationException($"Error: failed to create a bitmap object - Invalid decoded dimensions: width={width}, height={height}");
            }
        }
        catch (Exception ex) when (!(ex is ArgumentException || ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"Error: failed to create a bitmap object - SKBitmap.Decode failed with {pngData.Length} bytes: {ex.Message}", ex);
        }
        
        Invalidate();
    }

    /// <summary>
    /// Set the image from an existing SKBitmap.
    /// </summary>
    public void SetImage(SKBitmap bitmap)
    {
        // Validate bitmap dimensions
        if (bitmap != null && (bitmap.Width <= 0 || bitmap.Height <= 0))
        {
            throw new ArgumentException(
                $"Error: Invalid bitmap dimensions - width={bitmap.Width}, height={bitmap.Height}");
        }

        _bitmap?.Dispose();
        _bitmap = bitmap;
        Invalidate();
    }

    /// <summary>
    /// Clear the displayed image.
    /// </summary>
    public void ClearImage()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        Invalidate();
    }

    /// <summary>
    /// The actual rendered width of the image at current zoom.
    /// </summary>
    public float ImageWidth => (_bitmap?.Width ?? 0) * _zoom;

    /// <summary>
    /// The actual rendered height of the image at current zoom.
    /// </summary>
    public float ImageHeight => (_bitmap?.Height ?? 0) * _zoom;

    /// <summary>
    /// Override Measure to report zoom-aware dimensions so the parent ScrollArea
    /// knows the real content size and shows scrollbars when the image is larger
    /// than the viewport.
    /// </summary>
    public override SKSize Measure(float availableWidth, float availableHeight)
    {
        if (_bitmap == null) return base.Measure(availableWidth, availableHeight);
        return new SKSize(
            Math.Max(MinWidth, _bitmap.Width  * _zoom),
            Math.Max(MinHeight, _bitmap.Height * _zoom));
    }

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;

        canvas.Save();
        // Snap to whole pixels to avoid sub-pixel blur on fine PDF text.
        canvas.Translate(MathF.Round(X), MathF.Round(Y));
        canvas.ClipRect(LocalBounds);

        // Background
        using var bgPaint = new SKPaint { Color = Theme.PanelBackground };
        canvas.DrawRect(LocalBounds, bgPaint);

        if (_bitmap != null)
        {
            // Draw image with zoom
            var destRect = new SKRect(
                0,
                0,
                MathF.Round(ImageWidth),
                MathF.Round(ImageHeight));

            bool isNearNativeScale = MathF.Abs(_zoom - 1.0f) < 0.01f;
            using var imgPaint = new SKPaint
            {
                FilterQuality = isNearNativeScale ? SKFilterQuality.None : SKFilterQuality.High,
                IsAntialias = false
            };
            canvas.DrawBitmap(_bitmap, destRect, imgPaint);
        }
        else
        {
            // Placeholder text
            using var textPaint = Theme.CreateTextPaint(Theme.FontSizeMedium, Theme.TextPlaceholder);
            float textX = Width / 2 - textPaint.MeasureText("No image") / 2;
            float textY = Height / 2;
            canvas.DrawText("No image", textX, textY, textPaint);
        }

        canvas.Restore();
    }
}

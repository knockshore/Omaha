using SkiaSharp;

namespace Omaha.Theming;

/// <summary>
/// Defines the visual theme for all Omaha controls.
/// Modeled after a modern flat UI (Fusion-style from PyQt5).
/// </summary>
public class OmahaTheme
{
    // Background colors
    public SKColor WindowBackground { get; set; } = new(0xF5, 0xF5, 0xF5);
    public SKColor PanelBackground { get; set; } = SKColors.White;
    public SKColor ControlBackground { get; set; } = new(0xE8, 0xE8, 0xE8);
    public SKColor ControlBackgroundHover { get; set; } = new(0xD0, 0xD0, 0xD0);
    public SKColor ControlBackgroundPressed { get; set; } = new(0xC0, 0xC0, 0xC0);

    // Primary accent (blue header from original app)
    public SKColor Primary { get; set; } = new(0x44, 0x72, 0xC4);
    public SKColor PrimaryHover { get; set; } = new(0x3A, 0x65, 0xB5);
    public SKColor PrimaryPressed { get; set; } = new(0x30, 0x58, 0xA6);
    public SKColor PrimaryText { get; set; } = SKColors.White;

    // Text colors
    public SKColor TextPrimary { get; set; } = new(0x33, 0x33, 0x33);
    public SKColor TextSecondary { get; set; } = new(0x66, 0x66, 0x66);
    public SKColor TextDisabled { get; set; } = new(0xAA, 0xAA, 0xAA);
    public SKColor TextPlaceholder { get; set; } = new(0x99, 0x99, 0x99);

    // Status colors (match the diff HTML)
    public SKColor MatchGreen { get; set; } = new(0x28, 0xA7, 0x45);
    public SKColor MismatchRed { get; set; } = new(0xDC, 0x35, 0x45);
    public SKColor PatternBlue { get; set; } = new(0x00, 0x7B, 0xFF);
    public SKColor WarningYellow { get; set; } = new(0xFF, 0xC1, 0x07);

    // Borders
    public SKColor Border { get; set; } = new(0xDD, 0xDD, 0xDD);
    public SKColor BorderFocused { get; set; } = new(0x44, 0x72, 0xC4);
    public float BorderWidth { get; set; } = 1f;
    public float BorderRadius { get; set; } = 4f;

    // Typography
    public string FontFamily { get; set; } = "DejaVu Sans";
    public float FontSizeSmall { get; set; } = 11f;
    public float FontSizeNormal { get; set; } = 13f;
    public float FontSizeMedium { get; set; } = 15f;
    public float FontSizeLarge { get; set; } = 18f;
    public float FontSizeTitle { get; set; } = 22f;

    // Spacing
    public float Padding { get; set; } = 8f;
    public float PaddingLarge { get; set; } = 15f;
    public float Spacing { get; set; } = 6f;
    public float SpacingLarge { get; set; } = 12f;

    // Scrollbar
    public float ScrollbarWidth { get; set; } = 10f;
    public SKColor ScrollbarTrack { get; set; } = new(0xF0, 0xF0, 0xF0);
    public SKColor ScrollbarThumb { get; set; } = new(0xC0, 0xC0, 0xC0);
    public SKColor ScrollbarThumbHover { get; set; } = new(0xA0, 0xA0, 0xA0);

    // Tab control
    public SKColor TabBackground { get; set; } = new(0xE8, 0xE8, 0xE8);
    public SKColor TabActiveBackground { get; set; } = SKColors.White;
    public SKColor TabIndicator { get; set; } = new(0x44, 0x72, 0xC4);
    public float TabHeight { get; set; } = 36f;

    // Progress bar
    public SKColor ProgressBackground { get; set; } = new(0xE0, 0xE0, 0xE0);
    public SKColor ProgressForeground { get; set; } = new(0x44, 0x72, 0xC4);
    public float ProgressHeight { get; set; } = 6f;

    // Shadow
    public float ShadowBlur { get; set; } = 4f;
    public SKColor ShadowColor { get; set; } = new(0, 0, 0, 26); // ~10% opacity

    /// <summary>
    /// Create a default light theme.
    /// </summary>
    public static OmahaTheme Light => new();

    /// <summary>
    /// Create an SKPaint for text rendering.
    /// </summary>
    public SKPaint CreateTextPaint(float fontSize = 0, SKColor? color = null, bool bold = false)
    {
        return new SKPaint
        {
            Color = color ?? TextPrimary,
            TextSize = fontSize > 0 ? fontSize : FontSizeNormal,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(
                FontFamily,
                bold ? SKFontStyle.Bold : SKFontStyle.Normal),
            SubpixelText = true
        };
    }
}

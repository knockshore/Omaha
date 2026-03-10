using Omaha.Events;
using SkiaSharp;

namespace Omaha.Controls;

/// <summary>
/// A single-line text input field.
/// Supports keyboard entry, placeholder text, and read-only mode.
/// </summary>
public class TextInput : Control
{
    private string _text = string.Empty;
    private int _cursorPos;
    private bool _cursorVisible = true;
    private double _blinkAccum;

    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? string.Empty;
            _cursorPos = Math.Clamp(_cursorPos, 0, _text.Length);
            TextChanged?.Invoke(_text);
            Invalidate();
        }
    }

    public string Placeholder { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; }

    public float FontSize { get; set; }

    public event Action<string>? TextChanged;

    public TextInput()
    {
        PreferredHeight = 34;
        MinHeight = 28;
    }

    public TextInput(string placeholder) : this()
    {
        Placeholder = placeholder;
    }

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;

        canvas.Save();
        canvas.Translate(X, Y);

        // Background
        using var bgPaint = new SKPaint
        {
            Color = IsEnabled ? Theme.PanelBackground : Theme.ControlBackground,
            IsAntialias = true
        };
        var rect = new SKRoundRect(LocalBounds, Theme.BorderRadius);
        canvas.DrawRoundRect(rect, bgPaint);

        // Border
        using var borderPaint = new SKPaint
        {
            Color = IsFocused ? Theme.BorderFocused : Theme.Border,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = IsFocused ? 2f : Theme.BorderWidth,
            IsAntialias = true
        };
        canvas.DrawRoundRect(rect, borderPaint);

        float paddingH = Theme.Padding + 2;
        float fontSize = FontSize > 0 ? FontSize : Theme.FontSizeNormal;

        canvas.ClipRect(new SKRect(paddingH, 0, Width - paddingH, Height));

        // Text or placeholder
        if (string.IsNullOrEmpty(_text) && !IsFocused && !string.IsNullOrEmpty(Placeholder))
        {
            using var phPaint = Theme.CreateTextPaint(fontSize, Theme.TextPlaceholder);
            float textY = Height / 2f + phPaint.TextSize / 3f;
            canvas.DrawText(Placeholder, paddingH, textY, phPaint);
        }
        else
        {
            using var textPaint = Theme.CreateTextPaint(fontSize,
                IsEnabled ? Theme.TextPrimary : Theme.TextDisabled);
            float textY = Height / 2f + textPaint.TextSize / 3f;
            canvas.DrawText(_text, paddingH, textY, textPaint);

            // Cursor
            if (IsFocused && !IsReadOnly && _cursorVisible)
            {
                float cursorX = paddingH + textPaint.MeasureText(_text.AsSpan(0, _cursorPos));
                using var cursorPaint = new SKPaint
                {
                    Color = Theme.TextPrimary,
                    StrokeWidth = 1.5f,
                    IsAntialias = true
                };
                canvas.DrawLine(cursorX, 6, cursorX, Height - 6, cursorPaint);
            }
        }

        canvas.Restore();
    }

    public override bool HandleEvent(OmahaEvent evt)
    {
        if (!IsVisible || !IsEnabled) return false;

        switch (evt)
        {
            case MouseMoveEvent move:
                bool wasHovered = IsHovered;
                IsHovered = HitTest(move.X, move.Y);
                if (wasHovered != IsHovered) Invalidate();
                return false;

            case MouseButtonEvent click when click.IsPressed && HitTest(click.X, click.Y):
                IsFocused = true;
                _cursorPos = _text.Length; // Move cursor to end on click
                Invalidate();
                return true;

            case MouseButtonEvent click when click.IsPressed && !HitTest(click.X, click.Y):
                IsFocused = false;
                Invalidate();
                return false;

            case KeyEvent key when key.IsPressed && IsFocused && !IsReadOnly:
                return HandleKey(key.KeyCode, key.Ctrl, key.Shift);

            case TextInputEvent textInput when IsFocused && !IsReadOnly:
                if (!char.IsControl(textInput.Character))
                {
                    _text = _text.Insert(_cursorPos, textInput.Character.ToString());
                    _cursorPos++;
                    TextChanged?.Invoke(_text);
                    Invalidate();
                    return true;
                }
                return false;
        }

        return false;
    }

    private bool HandleKey(int keyCode, bool ctrl, bool shift)
    {
        // Silk.NET key codes
        const int KeyBackspace = 259;
        const int KeyDelete = 261;
        const int KeyLeft = 263;
        const int KeyRight = 262;
        const int KeyHome = 268;
        const int KeyEnd = 269;
        const int KeyA = 65;

        switch (keyCode)
        {
            case KeyBackspace when _cursorPos > 0:
                _text = _text.Remove(_cursorPos - 1, 1);
                _cursorPos--;
                TextChanged?.Invoke(_text);
                Invalidate();
                return true;

            case KeyDelete when _cursorPos < _text.Length:
                _text = _text.Remove(_cursorPos, 1);
                TextChanged?.Invoke(_text);
                Invalidate();
                return true;

            case KeyLeft when _cursorPos > 0:
                _cursorPos--;
                Invalidate();
                return true;

            case KeyRight when _cursorPos < _text.Length:
                _cursorPos++;
                Invalidate();
                return true;

            case KeyHome:
                _cursorPos = 0;
                Invalidate();
                return true;

            case KeyEnd:
                _cursorPos = _text.Length;
                Invalidate();
                return true;

            case KeyA when ctrl:
                _cursorPos = _text.Length;
                Invalidate();
                return true;
        }

        return false;
    }

    /// <summary>
    /// Call this from an update loop to animate the cursor blink.
    /// </summary>
    public void UpdateBlink(double deltaSeconds)
    {
        if (!IsFocused) return;
        _blinkAccum += deltaSeconds;
        if (_blinkAccum >= 0.5)
        {
            _blinkAccum = 0;
            _cursorVisible = !_cursorVisible;
            Invalidate();
        }
    }
}

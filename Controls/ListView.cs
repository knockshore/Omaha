using Omaha.Events;
using SkiaSharp;

namespace Omaha.Controls;

/// <summary>
/// A scrollable list of selectable items.
/// Equivalent to QListWidget in the original PyQt5 app.
/// </summary>
public class ListView : Control
{
    private readonly List<ListViewItem> _items = [];
    private int _selectedIndex = -1;
    private float _scrollOffset;
    private float _itemHeight = 32f;

    public IReadOnlyList<ListViewItem> Items => _items.AsReadOnly();
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex != value)
            {
                _selectedIndex = value;
                SelectionChanged?.Invoke(_selectedIndex);
                Invalidate();
            }
        }
    }

    public ListViewItem? SelectedItem =>
        _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    public float ItemHeight
    {
        get => _itemHeight;
        set { _itemHeight = value; Invalidate(); }
    }

    /// <summary>
    /// Fired when the selected item changes.
    /// </summary>
    public event Action<int>? SelectionChanged;

    /// <summary>
    /// Fired when an item is double-clicked.
    /// </summary>
    public event Action<int>? ItemDoubleClicked;

    public ListView()
    {
        MinWidth = 100;
        MinHeight = 50;
    }

    public void AddItem(string text, string? icon = null, object? tag = null)
    {
        _items.Add(new ListViewItem { Text = text, Icon = icon, Tag = tag });
        Invalidate();
    }

    public void ClearItems()
    {
        _items.Clear();
        _selectedIndex = -1;
        _scrollOffset = 0;
        Invalidate();
    }

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;

        canvas.Save();
        canvas.Translate(X, Y);
        canvas.ClipRect(LocalBounds);

        // Background
        using var bgPaint = new SKPaint { Color = Theme.PanelBackground };
        canvas.DrawRoundRect(new SKRoundRect(LocalBounds, Theme.BorderRadius), bgPaint);

        // Border
        using var borderPaint = new SKPaint
        {
            Color = Theme.Border,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Theme.BorderWidth,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(LocalBounds, Theme.BorderRadius), borderPaint);

        // Items
        canvas.Save();
        canvas.ClipRect(new SKRect(1, 1, Width - 1, Height - 1));
        canvas.Translate(0, -_scrollOffset);

        for (int i = 0; i < _items.Count; i++)
        {
            float itemY = i * _itemHeight;
            if (itemY + _itemHeight < _scrollOffset) continue;
            if (itemY > _scrollOffset + Height) break;

            var itemRect = new SKRect(1, itemY, Width - 1, itemY + _itemHeight);

            // Selection highlight
            if (i == _selectedIndex)
            {
                using var selPaint = new SKPaint { Color = Theme.Primary.WithAlpha(40) };
                canvas.DrawRect(itemRect, selPaint);
            }
            else if (IsHovered)
            {
                // Simple hover could be implemented with per-item tracking
            }

            // Item text
            using var textPaint = Theme.CreateTextPaint(
                Theme.FontSizeNormal,
                i == _selectedIndex ? Theme.Primary : Theme.TextPrimary);
            float textY = itemY + _itemHeight / 2 + textPaint.TextSize / 3;
            canvas.DrawText(_items[i].Text, Theme.Padding * 2, textY, textPaint);

            // Separator line
            if (i < _items.Count - 1)
            {
                using var sepPaint = new SKPaint { Color = Theme.Border.WithAlpha(128) };
                canvas.DrawLine(Theme.Padding, itemY + _itemHeight,
                    Width - Theme.Padding, itemY + _itemHeight, sepPaint);
            }
        }

        canvas.Restore();

        // Scrollbar
        float totalHeight = _items.Count * _itemHeight;
        if (totalHeight > Height)
        {
            float thumbRatio = Height / totalHeight;
            float thumbHeight = Math.Max(20, Height * thumbRatio);
            float thumbY = (_scrollOffset / totalHeight) * Height;

            var trackRect = new SKRect(Width - Theme.ScrollbarWidth, 0, Width, Height);
            using var trackPaint = new SKPaint { Color = Theme.ScrollbarTrack };
            canvas.DrawRect(trackRect, trackPaint);

            var thumbRect = new SKRect(Width - Theme.ScrollbarWidth, thumbY,
                Width, thumbY + thumbHeight);
            using var thumbPaint = new SKPaint { Color = Theme.ScrollbarThumb, IsAntialias = true };
            canvas.DrawRoundRect(new SKRoundRect(thumbRect, 4), thumbPaint);
        }

        canvas.Restore();
    }

    public override bool HandleEvent(OmahaEvent evt)
    {
        if (!IsVisible || !IsEnabled) return false;

        switch (evt)
        {
            case MouseButtonEvent click when click.IsPressed && HitTest(click.X, click.Y):
                float localY = click.Y - AbsoluteY + _scrollOffset;
                int clickedIndex = (int)(localY / _itemHeight);
                if (clickedIndex >= 0 && clickedIndex < _items.Count)
                {
                    SelectedIndex = clickedIndex;
                }
                return true;

            case MouseScrollEvent scroll when HitTest(scroll.X, scroll.Y):
                float totalHeight = _items.Count * _itemHeight;
                _scrollOffset = Math.Clamp(_scrollOffset - scroll.DeltaY * 30, 0,
                    Math.Max(0, totalHeight - Height));
                Invalidate();
                return true;
        }

        return base.HandleEvent(evt);
    }
}

public class ListViewItem
{
    public required string Text { get; init; }
    public string? Icon { get; init; }
    public object? Tag { get; init; }
}

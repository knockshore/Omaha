using Omaha.Events;
using SkiaSharp;

namespace Omaha.Controls;

/// <summary>
/// A tabbed container that shows one child panel at a time.
/// Equivalent to QTabWidget in the original PyQt5 app.
/// </summary>
public class TabControl : Control
{
    private readonly List<TabPage> _pages = [];
    private int _activeIndex;

    public IReadOnlyList<TabPage> Pages => _pages.AsReadOnly();

    public int ActiveIndex
    {
        get => _activeIndex;
        set
        {
            if (value >= 0 && value < _pages.Count && value != _activeIndex)
            {
                _activeIndex = value;
                ActiveTabChanged?.Invoke(value);
                Invalidate();
            }
        }
    }

    public TabPage? ActivePage =>
        _activeIndex >= 0 && _activeIndex < _pages.Count ? _pages[_activeIndex] : null;

    public event Action<int>? ActiveTabChanged;

    public void AddPage(string title, Container content)
    {
        _pages.Add(new TabPage { Title = title, Content = content });
        content.Parent = this;
        content.Theme = Theme;
        Invalidate();
    }

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;

        canvas.Save();
        canvas.Translate(X, Y);

        // Tab bar background
        var tabBarRect = new SKRect(0, 0, Width, Theme.TabHeight);
        using var tabBarPaint = new SKPaint { Color = Theme.TabBackground };
        canvas.DrawRect(tabBarRect, tabBarPaint);

        // Draw tabs
        float tabWidth = _pages.Count > 0 ? Width / _pages.Count : Width;
        for (int i = 0; i < _pages.Count; i++)
        {
            float tabX = i * tabWidth;
            var tabRect = new SKRect(tabX, 0, tabX + tabWidth, Theme.TabHeight);

            // Active tab background
            if (i == _activeIndex)
            {
                using var activePaint = new SKPaint { Color = Theme.TabActiveBackground };
                canvas.DrawRect(tabRect, activePaint);

                // Active indicator line
                using var indicatorPaint = new SKPaint
                {
                    Color = Theme.TabIndicator,
                    StrokeWidth = 3,
                    IsAntialias = true
                };
                canvas.DrawLine(tabX, Theme.TabHeight - 1.5f,
                    tabX + tabWidth, Theme.TabHeight - 1.5f, indicatorPaint);
            }

            // Tab text
            var textColor = i == _activeIndex ? Theme.Primary : Theme.TextSecondary;
            using var textPaint = Theme.CreateTextPaint(Theme.FontSizeNormal, textColor, i == _activeIndex);
            float textWidth = textPaint.MeasureText(_pages[i].Title);
            float textX = tabX + (tabWidth - textWidth) / 2;
            float textY = Theme.TabHeight / 2 + textPaint.TextSize / 3;
            canvas.DrawText(_pages[i].Title, textX, textY, textPaint);
        }

        // Separator line
        using var sepPaint = new SKPaint { Color = Theme.Border };
        canvas.DrawLine(0, Theme.TabHeight, Width, Theme.TabHeight, sepPaint);

        // Render active page content
        if (ActivePage != null)
        {
            canvas.Save();
            canvas.Translate(0, Theme.TabHeight);
            canvas.ClipRect(new SKRect(0, 0, Width, Height - Theme.TabHeight));
            ActivePage.Content.Arrange(0, 0, Width, Height - Theme.TabHeight);
            ActivePage.Content.Render(canvas);
            canvas.Restore();
        }

        canvas.Restore();
    }

    public override bool HandleEvent(OmahaEvent evt)
    {
        if (!IsVisible || !IsEnabled) return false;

        // Check tab clicks
        if (evt is MouseButtonEvent click && click.IsPressed && HitTest(click.X, click.Y))
        {
            float localY = click.Y - AbsoluteY;
            if (localY <= Theme.TabHeight && _pages.Count > 0)
            {
                float tabWidth = Width / _pages.Count;
                float localX = click.X - AbsoluteX;
                int tabIndex = (int)(localX / tabWidth);
                if (tabIndex >= 0 && tabIndex < _pages.Count)
                {
                    ActiveIndex = tabIndex;
                    return true;
                }
            }
        }

        // Propagate to active page content
        if (ActivePage?.Content.HandleEvent(evt) == true)
            return true;

        return base.HandleEvent(evt);
    }
}

public class TabPage
{
    public required string Title { get; init; }
    public required Container Content { get; init; }
}

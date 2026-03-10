using Omaha.Events;
using SkiaSharp;

namespace Omaha.Controls;

/// <summary>
/// A container that holds child controls and manages their layout.
/// Base class for layout containers (VBox, HBox, etc.)
/// </summary>
public abstract class Container : Control
{
    protected List<Control> Children { get; } = [];

    public IReadOnlyList<Control> ChildControls => Children.AsReadOnly();

    public void Add(Control child)
    {
        child.Parent = this;
        child.Theme = Theme;
        Children.Add(child);
        Invalidate();
    }

    public void Remove(Control child)
    {
        child.Parent = null;
        Children.Remove(child);
        Invalidate();
    }

    public void Clear()
    {
        foreach (var child in Children) child.Parent = null;
        Children.Clear();
        Invalidate();
    }

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;

        canvas.Save();
        canvas.Translate(X, Y);
        canvas.ClipRect(LocalBounds);

        RenderBackground(canvas);

        foreach (var child in Children)
        {
            if (child.IsVisible)
                child.Render(canvas);
        }

        canvas.Restore();
    }

    protected virtual void RenderBackground(SKCanvas canvas) { }

    public override bool HandleEvent(OmahaEvent evt)
    {
        if (!IsVisible || !IsEnabled) return false;

        // Propagate to children in reverse order (topmost first)
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i].HandleEvent(evt))
                return true;
        }

        return base.HandleEvent(evt);
    }
}

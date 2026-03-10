using SkiaSharp;

namespace Omaha.Events;

/// <summary>
/// Base class for all Omaha UI events.
/// </summary>
public abstract class OmahaEvent
{
    public bool Handled { get; set; }
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Mouse button states.
/// </summary>
public enum MouseButton { Left, Right, Middle }

public class MouseMoveEvent : OmahaEvent
{
    public float X { get; init; }
    public float Y { get; init; }
}

public class MouseButtonEvent : OmahaEvent
{
    public float X { get; init; }
    public float Y { get; init; }
    public MouseButton Button { get; init; }
    public bool IsPressed { get; init; }
}

public class MouseScrollEvent : OmahaEvent
{
    public float X { get; init; }
    public float Y { get; init; }
    public float DeltaX { get; init; }
    public float DeltaY { get; init; }
}

public class KeyEvent : OmahaEvent
{
    public int KeyCode { get; init; }
    public bool IsPressed { get; init; }
    public bool Shift { get; init; }
    public bool Ctrl { get; init; }
    public bool Alt { get; init; }
}

public class TextInputEvent : OmahaEvent
{
    public char Character { get; init; }
}

public class ResizeEvent : OmahaEvent
{
    public int Width { get; init; }
    public int Height { get; init; }
}

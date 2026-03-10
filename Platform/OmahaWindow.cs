using Omaha.Controls;
using Omaha.Events;
using Omaha.Theming;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SkiaSharp;

namespace Omaha.Platform;

/// <summary>
/// Omaha Application Host — creates a native window using Silk.NET,
/// manages the SkiaSharp rendering surface, and dispatches input events
/// to the Omaha control tree.
/// 
/// This is the main entry point for running an Omaha UI application.
/// </summary>
public class OmahaWindow : IDisposable
{
    private readonly IWindow _window;
    private GL? _gl;
    private GRContext? _grContext;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;
    private Container? _rootControl;
    private bool _needsRedraw = true;
    private bool _disposed;

    public string Title
    {
        get => _window.Title;
        set => _window.Title = value;
    }

    public int Width => _window.Size.X;
    public int Height => _window.Size.Y;

    public OmahaTheme Theme { get; set; } = OmahaTheme.Light;

    /// <summary>
    /// Set the root control that fills the entire window.
    /// </summary>
    public Container? RootControl
    {
        get => _rootControl;
        set
        {
            _rootControl = value;
            if (_rootControl != null)
                _rootControl.Theme = Theme;
            _needsRedraw = true;
        }
    }

    /// <summary>
    /// Called once during window initialization. Set up your UI here.
    /// </summary>
    public event Action? OnSetup;

    /// <summary>
    /// Called each frame for custom logic (animations, async updates).
    /// </summary>
    public event Action<double>? OnUpdate;

    public OmahaWindow(int width = 1400, int height = 900, string title = "Omaha Application")
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(width, height);
        options.Title = title;
        options.VSync = true;
        options.PreferredStencilBufferBits = 8;
        options.PreferredDepthBufferBits = 24;

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Resize += OnResize;
        _window.Closing += OnClosing;
    }

    /// <summary>
    /// Start the application main loop (blocking).
    /// </summary>
    public void Run()
    {
        _window.Run();
    }

    private void OnLoad()
    {
        _gl = _window.CreateOpenGL();

        // Create SkiaSharp GL context
        var grInterface = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(grInterface);

        CreateSurface();

        // Setup input handling
        var input = _window.CreateInput();
        foreach (var mouse in input.Mice)
        {
            mouse.MouseMove += (_, pos) =>
            {
                _rootControl?.HandleEvent(new MouseMoveEvent { X = pos.X, Y = pos.Y });
                _needsRedraw = true;
            };
            mouse.MouseDown += (_, btn) =>
            {
                var pos = mouse.Position;
                _rootControl?.HandleEvent(new MouseButtonEvent
                {
                    X = pos.X, Y = pos.Y,
                    Button = MapMouseButton(btn),
                    IsPressed = true
                });
                _needsRedraw = true;
            };
            mouse.MouseUp += (_, btn) =>
            {
                var pos = mouse.Position;
                _rootControl?.HandleEvent(new MouseButtonEvent
                {
                    X = pos.X, Y = pos.Y,
                    Button = MapMouseButton(btn),
                    IsPressed = false
                });
                _needsRedraw = true;
            };
            mouse.Scroll += (_, wheel) =>
            {
                var pos = mouse.Position;
                _rootControl?.HandleEvent(new MouseScrollEvent
                {
                    X = pos.X, Y = pos.Y,
                    DeltaX = wheel.X,
                    DeltaY = wheel.Y
                });
                _needsRedraw = true;
            };
        }

        foreach (var keyboard in input.Keyboards)
        {
            keyboard.KeyDown += (_, key, _) =>
            {
                _rootControl?.HandleEvent(new KeyEvent { KeyCode = (int)key, IsPressed = true });
                _needsRedraw = true;
            };
            keyboard.KeyUp += (_, key, _) =>
            {
                _rootControl?.HandleEvent(new KeyEvent { KeyCode = (int)key, IsPressed = false });
                _needsRedraw = true;
            };
        }

        OnSetup?.Invoke();
    }

    private void CreateSurface()
    {
        _renderTarget?.Dispose();
        _surface?.Dispose();

        _gl!.GetInteger(GLEnum.FramebufferBinding, out int framebuffer);
        _gl.GetInteger(GLEnum.Stencil, out int stencil);
        _gl.GetInteger(GLEnum.Samples, out int samples);

        var info = new GRGlFramebufferInfo((uint)framebuffer, SKColorType.Rgba8888.ToGlSizedFormat());
        _renderTarget = new GRBackendRenderTarget(
            _window.Size.X, _window.Size.Y,
            Math.Max(0, samples), Math.Max(0, stencil), info);

        _surface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
    }

    private void OnRender(double deltaTime)
    {
        OnUpdate?.Invoke(deltaTime);

        // ContinuousRendering forces a redraw every frame (e.g. during async
        // operations that update state from background threads).
        if (ContinuousRendering) _needsRedraw = true;

        if (!_needsRedraw && _surface != null) return;
        _needsRedraw = false;

        if (_surface == null) return;

        var canvas = _surface.Canvas;
        canvas.Clear(Theme.WindowBackground);

        if (_rootControl != null)
        {
            _rootControl.Arrange(0, 0, _window.Size.X, _window.Size.Y);
            _rootControl.Render(canvas);
        }

        canvas.Flush();
        _grContext?.Flush();
    }

    private void OnResize(Vector2D<int> size)
    {
        _gl?.Viewport(0, 0, (uint)size.X, (uint)size.Y);
        CreateSurface();
        _needsRedraw = true;

        _rootControl?.HandleEvent(new ResizeEvent { Width = size.X, Height = size.Y });
    }

    private void OnClosing()
    {
        Dispose();
    }

    private static Events.MouseButton MapMouseButton(Silk.NET.Input.MouseButton btn) => btn switch
    {
        Silk.NET.Input.MouseButton.Left => Events.MouseButton.Left,
        Silk.NET.Input.MouseButton.Right => Events.MouseButton.Right,
        Silk.NET.Input.MouseButton.Middle => Events.MouseButton.Middle,
        _ => Events.MouseButton.Left
    };

    /// <summary>
    /// When true, the window redraws every frame regardless of input events.
    /// Use this during async operations (loading, comparisons) that update
    /// UI state from background threads.  Reset to false when idle to save CPU.
    /// </summary>
    public bool ContinuousRendering { get; set; }

    /// <summary>
    /// Request a redraw on the next frame.
    /// </summary>
    public void Invalidate() => _needsRedraw = true;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _surface?.Dispose();
        _renderTarget?.Dispose();
        _grContext?.Dispose();
        _gl?.Dispose();

        GC.SuppressFinalize(this);
    }
}

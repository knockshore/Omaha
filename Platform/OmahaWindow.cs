using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
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
/// Supports cross-platform windowing with proper error handling and diagnostics.
/// </summary>
public class OmahaWindow : IDisposable
{
    private readonly IWindow _window;
    private readonly ConcurrentQueue<Action> _postedUiActions = new();
    private GL? _gl;
    private GRContext? _grContext;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;
    private Container? _rootControl;
    private bool _needsRedraw = true;
    private bool _disposed;
    private bool _shiftHeld;
    private bool _ctrlHeld;
    private int _uiThreadId;
    private IKeyboard? _keyboard;  // first keyboard; used to query live modifier state

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
            // Unwire the old root so it no longer drives redraws
            if (_rootControl != null)
                _rootControl.InvalidateRequested -= Invalidate;

            _rootControl = value;

            if (_rootControl != null)
            {
                _rootControl.Theme = Theme;
                _rootControl.InvalidateRequested += Invalidate;
            }

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
        // Disable automatic SwapBuffers — we call it ourselves only after
        // a real paint so that skipped frames never expose the stale back-buffer
        // (which is what causes the visible flicker between previous/current draws).
        options.ShouldSwapAutomatically = false;

        try
        {
            _window = Window.Create(options);
        }
        catch (PlatformNotSupportedException pex)
        {
            // Provide detailed diagnostics for windowing backend issues
            Console.Error.WriteLine("[OmahaWindow] Windowing platform not supported on this system.");
            Console.Error.WriteLine($"[OmahaWindow] Error: {pex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("[OmahaWindow] Platform diagnostics:");
            Console.Error.WriteLine($"  OS: {GetOperatingSystem()}");
            Console.Error.WriteLine($"  Architecture: {RuntimeInformation.ProcessArchitecture}");
            Console.Error.WriteLine($"  Runtime: {RuntimeInformation.FrameworkDescription}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("[OmahaWindow] Troubleshooting:");
            Console.Error.WriteLine("  Windows: Ensure GPU drivers are installed and OpenGL support is available.");
            Console.Error.WriteLine("  Linux: Install libglfw3 or libsdl2: sudo apt-get install libglfw3 libsdl2-2.0-0");
            Console.Error.WriteLine("  macOS: Ensure Xcode command line tools are installed.");
            throw new InvalidOperationException(
                $"Cannot initialize GUI. No suitable windowing platform available. "
                + $"See diagnostics above. Inner error: {pex.Message}", pex);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[OmahaWindow] Unexpected error during window creation: {ex.GetType().Name}");
            Console.Error.WriteLine($"[OmahaWindow] Message: {ex.Message}");
            throw;
        }

        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Resize += OnResize;
        _window.Closing += OnClosing;
    }

    private static string GetOperatingSystem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "Linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macOS";
        return "Unknown";
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
        _uiThreadId = Environment.CurrentManagedThreadId;
        SynchronizationContext.SetSynchronizationContext(new OmahaWindowSynchronizationContext(this));

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
                // Do NOT set _needsRedraw here unconditionally — controls call
                // Invalidate() themselves only when their hover state changes,
                // preventing spurious redraws on every mouse-move event.
                _rootControl?.HandleEvent(new MouseMoveEvent { X = pos.X, Y = pos.Y });
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
                // Query live keyboard state so Shift/Ctrl+scroll is always reliable
                // even if the KeyDown event was missed (focus change, etc.).
                bool shift = _keyboard?.IsKeyPressed(Key.ShiftLeft) == true
                          || _keyboard?.IsKeyPressed(Key.ShiftRight) == true
                          || _shiftHeld;
                bool ctrl  = _keyboard?.IsKeyPressed(Key.ControlLeft)  == true
                          || _keyboard?.IsKeyPressed(Key.ControlRight) == true
                          || _ctrlHeld;
                float dx = shift ? wheel.Y : wheel.X;
                float dy = shift ? 0f      : wheel.Y;
                _rootControl?.HandleEvent(new MouseScrollEvent
                {
                    X = pos.X, Y = pos.Y,
                    DeltaX = dx,
                    DeltaY = dy,
                    Ctrl   = ctrl,
                });
                _needsRedraw = true;
            };
        }

        foreach (var keyboard in input.Keyboards)
        {
            _keyboard ??= keyboard;   // capture first keyboard for live state queries
            keyboard.KeyDown += (_, key, _) =>
            {
                if (key is Key.ShiftLeft   or Key.ShiftRight)   _shiftHeld = true;
                if (key is Key.ControlLeft or Key.ControlRight) _ctrlHeld  = true;
                _rootControl?.HandleEvent(new KeyEvent { KeyCode = (int)key, IsPressed = true,  Shift = _shiftHeld, Ctrl = _ctrlHeld });
                _needsRedraw = true;
            };
            keyboard.KeyUp += (_, key, _) =>
            {
                if (key is Key.ShiftLeft   or Key.ShiftRight)   _shiftHeld = false;
                if (key is Key.ControlLeft or Key.ControlRight) _ctrlHeld  = false;
                _rootControl?.HandleEvent(new KeyEvent { KeyCode = (int)key, IsPressed = false, Shift = _shiftHeld, Ctrl = _ctrlHeld });
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
        DrainPostedUiActions();
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

        // Swap only after a real paint — prevents the stale back-buffer from
        // being shown on frames where we returned early (no _needsRedraw).
        _window.SwapBuffers();
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

    /// <summary>
    /// Queue work onto the window UI thread.
    /// </summary>
    public void Post(Action action)
    {
        if (_disposed) return;

        if (Environment.CurrentManagedThreadId == _uiThreadId)
        {
            action();
            Invalidate();
            return;
        }

        _postedUiActions.Enqueue(action);
        Invalidate();
    }

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

    private void DrainPostedUiActions()
    {
        while (_postedUiActions.TryDequeue(out var action))
            action();
    }

    private sealed class OmahaWindowSynchronizationContext : SynchronizationContext
    {
        private readonly OmahaWindow _window;

        public OmahaWindowSynchronizationContext(OmahaWindow window)
        {
            _window = window;
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            _window.Post(() => d(state));
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            if (Environment.CurrentManagedThreadId == _window._uiThreadId)
            {
                d(state);
                return;
            }

            Exception? dispatchException = null;
            using var signal = new ManualResetEventSlim();

            _window.Post(() =>
            {
                try
                {
                    d(state);
                }
                catch (Exception ex)
                {
                    dispatchException = ex;
                }
                finally
                {
                    signal.Set();
                }
            });

            signal.Wait();

            if (dispatchException is not null)
                ExceptionDispatchInfo.Capture(dispatchException).Throw();
        }
    }
}
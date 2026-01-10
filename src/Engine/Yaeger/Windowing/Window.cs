using System.Numerics;

using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

using Yaeger.Audio;
using Yaeger.Input;

namespace Yaeger.Windowing;

public sealed class Window : IDisposable
{
    private readonly IWindow _innerWindow;
    internal GL Gl { get; }

    /// <summary>
    /// Gets the audio context for this window.
    /// </summary>
    public AudioContext AudioContext { get; }

    private Window(IWindow window)
    {
        _innerWindow = window;

        // Forward Silk.NET events to public events
        _innerWindow.Resize += size => Resize?.Invoke(new Vector2(size.X, size.Y));
        _innerWindow.Update += delta => Update?.Invoke(delta);
        _innerWindow.Closing += Closing;
        _innerWindow.Render += delta => Render?.Invoke(delta);

        _innerWindow.Initialize();

        Gl = _innerWindow.CreateOpenGL();
        Gl.Viewport(0, 0, (uint)_innerWindow.Size.X, (uint)_innerWindow.Size.Y);
        _innerWindow.Resize += size => Gl.Viewport(0, 0, (uint)size.X, (uint)size.Y);

        // Initialize the keyboard
        var inputContext = _innerWindow.CreateInput();
        Keyboard.Initialize(inputContext);
        // Note: inputContext lifecycle is managed by _innerWindow, which will dispose it

        // Initialize audio - if this fails, we need to clean up resources
        try
        {
            AudioContext = Audio.AudioContext.Create();
        }
        catch (Exception ex)
        {
            // Clean up already-initialized resources if audio initialization fails
            Gl.Dispose();
            _innerWindow.Dispose();
            throw new InvalidOperationException("Failed to initialize audio system. Ensure audio device is available.", ex);
        }
    }

    public static Window Create()
        => new(Silk.NET.Windowing.Window.Create(WindowOptions.Default));

    public Vector2 Size => new(_innerWindow.Size.X, _innerWindow.Size.Y);

    #region "Events"
    // Backing fields for public events
    private event Action? Load;
    private event Action<Vector2>? Resize;
    private event Action<double>? Update;
    private event Action? Closing;
    private event Action<double>? Render;

    public event Action? OnLoad
    {
        add => Load += value;
        remove => Load -= value;
    }
    public event Action<Vector2>? OnResize
    {
        add => Resize += value;
        remove => Resize -= value;
    }
    public event Action<double>? OnUpdate
    {
        add => Update += value;
        remove => Update -= value;
    }
    public event Action? OnClosing
    {
        add => Closing += value;
        remove => Closing -= value;
    }
    public event Action<double>? OnRender
    {
        add => Render += value;
        remove => Render -= value;
    }
    #endregion

    public void Run()
    {
        // Invoke the Load event
        Load?.Invoke();

        _innerWindow.Run();
    }
    public void Close() => _innerWindow.Close();

    public void Dispose()
    {
        // Dispose the AudioContext before the window. The audio system is independent of the
        // IWindow lifecycle, so this order is safe and makes ownership of audio resources explicit.
        AudioContext.Dispose();
        _innerWindow.Dispose();
    }
}
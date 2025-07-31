using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

public class Buffer<T> : IDisposable where T : unmanaged
{
    private readonly GL _gl;
    private readonly uint _handle;
    private readonly BufferTargetARB _target;

    public unsafe Buffer(GL gl, ReadOnlySpan<T> data, BufferTargetARB target)
    {
        _gl = gl;
        _target = target;
        _handle = _gl.GenBuffer();
        Bind();
        fixed (void* d = data)
        {
            _gl.BufferData(_target, (nuint) (data.Length * sizeof(T)), d, BufferUsageARB.StaticDraw);
        }
    }

    public void Bind() => _gl.BindBuffer(_target, _handle);

    public void Dispose() => _gl.DeleteBuffer(_handle);
}


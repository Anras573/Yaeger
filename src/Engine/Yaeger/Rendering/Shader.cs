using System.Numerics;
using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

public class Shader : IDisposable
{
    private readonly GL _gl;
    private readonly uint _program;
    private Dictionary<string, int> UniformLocations { get; } = new();

    public Shader(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;
        var vertexShader = CompileShader(GLEnum.VertexShader, vertexSource);
        var fragmentShader = CompileShader(GLEnum.FragmentShader, fragmentSource);
        _program = _gl.CreateProgram();
        _gl.AttachShader(_program, vertexShader);
        _gl.AttachShader(_program, fragmentShader);
        _gl.LinkProgram(_program);
        CheckLinkStatus();
        _gl.DetachShader(_program, vertexShader);
        _gl.DetachShader(_program, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);
    }

    private uint CompileShader(GLEnum type, string source)
    {
        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        var status = _gl.GetShaderInfoLog(shader);
        if (string.IsNullOrEmpty(status))
            return shader;

        throw new Exception($"Shader compilation failed: {status}");
    }

    private void CheckLinkStatus()
    {
        var status = _gl.GetProgramInfoLog(_program);
        if (string.IsNullOrEmpty(status))
            return;

        throw new Exception($"Shader program linking failed: {status}");
    }

    public void Bind() => _gl.UseProgram(_program);

    public void Unbind() => _gl.UseProgram(0);

    private int GetUniformLocation(string name)
    {
        if (UniformLocations.TryGetValue(name, out var location))
            return location;

        location = _gl.GetUniformLocation(_program, name);
        if (location == -1)
            throw new Exception($"Uniform '{name}' not found in shader program.");

        UniformLocations[name] = location;
        return location;
    }

    public unsafe void SetUniformMatrix4(string name, Matrix4x4 matrix)
    {
        var location = GetUniformLocation(name);
        _gl.UniformMatrix4(location, 1, false, (float*)&matrix);
    }

    public void SetUniformVec4(string name, Vector4 value)
    {
        var location = GetUniformLocation(name);
        _gl.Uniform4(location, value.X, value.Y, value.Z, value.W);
    }

    public void SetUniformVec3(string name, Vector3 value)
    {
        var location = GetUniformLocation(name);
        _gl.Uniform3(location, value.X, value.Y, value.Z);
    }

    public void SetUniformFloat(string name, float value)
    {
        var location = GetUniformLocation(name);
        _gl.Uniform1(location, value);
    }

    public unsafe void SetUniformMatrix3(string name, Matrix4x4 m)
    {
        var location = GetUniformLocation(name);
        float* mat3 = stackalloc float[]
        {
            m.M11,
            m.M12,
            m.M13,
            m.M21,
            m.M22,
            m.M23,
            m.M31,
            m.M32,
            m.M33,
        };
        _gl.UniformMatrix3(location, 1, false, mat3);
    }

    public void Dispose() => _gl.DeleteProgram(_program);
}

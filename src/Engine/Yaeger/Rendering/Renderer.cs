using System.Numerics;
using System.Runtime.CompilerServices;

using Silk.NET.OpenGL;
using Yaeger.Windowing;

namespace Yaeger.Rendering;

public class Renderer
{
    private readonly GL _gl;
    private readonly VertexArray _vao;
    
    private const string VertexShaderSource = """
                                              #version 330 core
                                              layout(location = 0) in vec2 aPosition;
                                              layout(location = 1) in vec2 aTexCoord;
                                              
                                              uniform mat4 uTransform;
                                              
                                              out vec2 vTexCoord;
                                              
                                              void main()
                                              {
                                                  gl_Position = uTransform * vec4(aPosition, 0.0, 1.0);
                                                  vTexCoord = aTexCoord;
                                              }
                                              """;
    private const string FragmentShaderSource = """
                                                #version 330 core
                                                in vec2 vTexCoord;
                                                out vec4 FragColor;
                                                
                                                uniform sampler2D uTexture;
                                                
                                                void main()
                                                {
                                                    FragColor = texture(uTexture, vTexCoord);
                                                }
                                                """;
    
    private readonly TextureManager _textureManager;
    private readonly Shader _textureShader;

    private static readonly float[] Vertices =
    [
        //X     Y     Z     U   V
        0.5f,  0.5f, 0.0f, 1f, 1f,
        0.5f, -0.5f, 0.0f, 1f, 0f,
        -0.5f, -0.5f, 0.0f, 0f, 0f,
        -0.5f,  0.5f, 0.0f, 0f, 1f
    ];

    private static readonly uint[] Indices =
    [
        0, 1, 3,
        1, 2, 3
    ];
    
    public Renderer(Window window)
    {
        _gl = window.Gl;
        
        _textureManager = new TextureManager(_gl);
        _textureShader = new Shader(_gl, VertexShaderSource, FragmentShaderSource);

        var vbo = new Buffer<float>(_gl, Vertices, BufferTargetARB.ArrayBuffer);
        var ebo = new Buffer<uint>(_gl, Indices, BufferTargetARB.ElementArrayBuffer);
        _vao = new VertexArray(_gl, vbo, ebo);
        
        CheckGlError();
        
        Console.WriteLine($"GL initialized: {_gl.GetStringS(GLEnum.Version)}, {_gl.GetStringS(GLEnum.Renderer)}");
    }

    private void CheckGlError([CallerMemberName]string context = "")
    {
        var error = _gl.GetError();
        if (error != GLEnum.NoError)
        {
            Console.WriteLine($"OpenGL error after {context}: {error}");
        }
    }

    public void BeginFrame()
    {
        // Query the current framebuffer size
        var viewport = new int[4];
        _gl.GetInteger(GLEnum.Viewport, viewport);
        var width = viewport[2];
        var height = viewport[3];

        // Always set the viewport to the current framebuffer size
        _gl.Viewport(0, 0, (uint)width, (uint)height);
        
        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear((uint)ClearBufferMask.ColorBufferBit);
        
        CheckGlError();
    }

    public void EndFrame() { /* No-op for now */ }

    public unsafe void DrawQuad(Matrix4x4 model, string texturePath)
    {
        var texture = _textureManager.Get(texturePath);
        
        _textureShader.Bind();
        _textureShader.SetUniformMatrix4("uTransform", model);
        
        texture.Bind();
        _vao.Bind();
        
        _gl.DrawElements(PrimitiveType.Triangles, (uint) Indices.Length, DrawElementsType.UnsignedInt, null);
        
        _vao.Unbind();
        texture.Unbind();
        _textureShader.Unbind();
    }
}

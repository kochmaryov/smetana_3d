using Silk.NET.OpenGL;

namespace SmetaNA.Rendering.Annotations;

/// <summary>
/// GL-отрисовка батча размеров: линии размеров, выноски, стрелки и штрихи
/// текста одним draw-вызовом GL_LINES (тот же line-шейдер, что у рёбер сцены,
/// со смещением глубины). Ресурсы создаются лениво на первом кадре.
/// </summary>
public sealed class DimensionGlRenderer : IDisposable
{
    /// <summary>Цвет линий размеров и подписей.</summary>
    private static readonly float[] LineColor = [0.10f, 0.12f, 0.17f, 1f];

    private const float DepthBias = 0.0006f;

    private GL? _gl;
    private uint _program;
    private int _mvpLocation;
    private int _colorLocation;
    private int _biasLocation;
    private uint _vbo;
    private uint _vao;
    private bool _useVao;
    private int _vertexCount;
    private DimensionBatch? _uploaded;

    /// <summary>
    /// Отрисовать батч (или очистить путь при <c>null</c>).
    /// Вызывается на GL-потоке с готовым контекстом после кадра сцены.
    /// </summary>
    public void Draw(GL gl, DimensionBatch? batch, float[] mvp, float[]? color = null)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentNullException.ThrowIfNull(mvp);

        EnsureInitialized(gl);
        if (batch is not { IsEmpty: false })
        {
            return;
        }

        if (!ReferenceEquals(_uploaded, batch))
        {
            Upload(batch.Vertices);
            _uploaded = batch;
        }

        gl.UseProgram(_program);
        SetMatrix(_mvpLocation, mvp);
        SetColor(_colorLocation, color ?? LineColor);
        gl.Uniform1(_biasLocation, DepthBias);

        BindLayout();
        gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_vertexCount);
    }

    public void Dispose()
    {
        if (_gl is not { } gl)
        {
            return;
        }

        if (_vbo != 0)
        {
            gl.DeleteBuffer(_vbo);
            _vbo = 0;
        }

        if (_vao != 0)
        {
            gl.DeleteVertexArray(_vao);
            _vao = 0;
        }

        if (_program != 0)
        {
            gl.DeleteProgram(_program);
            _program = 0;
        }

        _gl = null;
        _uploaded = null;
        _vertexCount = 0;
    }

    private void EnsureInitialized(GL gl)
    {
        if (_gl is not null)
        {
            return;
        }

        (bool isEs, int major, int minor) = GlSceneRenderer.ParseVersion(
            GlSceneRenderer.GetStringSafe(gl, StringName.Version));
        _useVao = !isEs || major >= 3;

        (string vs, string fs) = ShaderSources.SelectLine(isEs, major, minor);
        try
        {
            _program = BuildProgram(gl, vs, fs);
        }
        catch (InvalidOperationException)
        {
            (vs, fs) = ShaderSources.SelectLine(false, 1, 1);
            _program = BuildProgram(gl, vs, fs);
        }

        _mvpLocation = gl.GetUniformLocation(_program, "uMvp");
        _colorLocation = gl.GetUniformLocation(_program, "uColor");
        _biasLocation = gl.GetUniformLocation(_program, "uDepthBias");
        _vbo = gl.GenBuffer();
        if (_useVao)
        {
            _vao = gl.GenVertexArray();
        }

        _gl = gl;
    }

    private void Upload(float[] vertices)
    {
        GL gl = _gl!;
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        unsafe
        {
            fixed (float* p = vertices)
            {
                gl.BufferData(
                    BufferTargetARB.ArrayBuffer,
                    (nuint)(vertices.Length * sizeof(float)),
                    p,
                    BufferUsageARB.StaticDraw);
            }
        }

        _vertexCount = vertices.Length / 3;
        if (_useVao)
        {
            gl.BindVertexArray(_vao);
            BindLayout();
            gl.BindVertexArray(0);
        }
    }

    private void BindLayout()
    {
        GL gl = _gl!;
        if (_useVao)
        {
            gl.BindVertexArray(_vao);
            return;
        }

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        unsafe
        {
            const uint stride = 3 * sizeof(float);
            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        }

        gl.EnableVertexAttribArray(0);
    }

    private static uint BuildProgram(GL gl, string vsSource, string fsSource)
    {
        uint vs = CompileShader(gl, ShaderType.VertexShader, vsSource);
        uint fs = CompileShader(gl, ShaderType.FragmentShader, fsSource);
        uint program = gl.CreateProgram();
        gl.AttachShader(program, vs);
        gl.AttachShader(program, fs);

        // Для legacy-шейдеров без layout(location=...) — фиксируем атрибут до линковки.
        gl.BindAttribLocation(program, 0, "aPos");
        gl.LinkProgram(program);
        gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = gl.GetProgramInfoLog(program);
            gl.DeleteShader(vs);
            gl.DeleteShader(fs);
            gl.DeleteProgram(program);
            throw new InvalidOperationException("линковка программы размеров: " + log);
        }

        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        return program;
    }

    private static uint CompileShader(GL gl, ShaderType type, string source)
    {
        uint shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compiled);
        if (compiled == 0)
        {
            string log = gl.GetShaderInfoLog(shader);
            gl.DeleteShader(shader);
            throw new InvalidOperationException($"компиляция {type}: {log}");
        }

        return shader;
    }

    private void SetMatrix(int location, float[] matrix)
    {
        GL gl = _gl!;
        unsafe
        {
            fixed (float* p = matrix)
            {
                gl.UniformMatrix4(location, 1, false, p);
            }
        }
    }

    private void SetColor(int location, float[] color) =>
        _gl!.Uniform4(location, color[0], color[1], color[2], color[3]);
}

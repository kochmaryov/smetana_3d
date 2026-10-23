using Silk.NET.OpenGL;

namespace SmetaNA.Rendering.PrintVolume;

/// <summary>
/// GL-отрисовка оверлея зоны печати (задача 4.5): каркас зоны и сетка пола
/// (line-шейдер со смещением глубины — тот же путь, что у размеров), подсветка
/// «за пределами зоны» — полупрозрачные треугольники модели с контуром.
/// Ресурсы создаются лениво на первом кадре, как у
/// <see cref="Annotations.DimensionGlRenderer"/>.
/// </summary>
public sealed class PrintVolumeGlRenderer : IDisposable
{
    private static readonly float[] FrameColor = [0.35f, 0.78f, 1.0f, 0.9f];
    private static readonly float[] FloorGridColor = [0.30f, 0.55f, 0.72f, 0.55f];
    private static readonly float[] OutOfVolumeFillColor = [1.0f, 0.30f, 0.22f, 0.35f];
    private static readonly float[] OutOfVolumeEdgeColor = [1.0f, 0.35f, 0.25f, 0.9f];

    private const float LineDepthBias = 0.0006f;
    private const float FillPolygonOffsetUnits = 2f;

    private GL? _gl;
    private bool _useVao;

    private uint _lineProgram;
    private uint _meshProgram;
    private int _lineMvpLocation;
    private int _lineColorLocation;
    private int _lineBiasLocation;
    private int _meshMvpLocation;
    private int _meshMvLocation;
    private int _meshColorLocation;
    private int _meshLightLocation;

    private uint _lineVbo;
    private uint _lineVao;
    private uint _triVbo;
    private uint _triVao;

    private int _floorVertexCount;
    private int _frameVertexCount;
    private int _outlineVertexCount;
    private int _fillVertexCount;
    private PrintVolumeOverlay? _uploaded;

    /// <summary>
    /// Отрисовать оверлей (или очистить путь при <c>null</c>).
    /// Вызывается на GL-потоке с готовым контекстом после кадра сцены.
    /// </summary>
    public void Draw(GL gl, PrintVolumeOverlay? overlay, float[] mvp, float[] mv)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentNullException.ThrowIfNull(mvp);
        ArgumentNullException.ThrowIfNull(mv);

        EnsureInitialized(gl);
        if (overlay is null)
        {
            return;
        }

        if (!ReferenceEquals(_uploaded, overlay))
        {
            Upload(overlay);
        }

        // Сетка пола и каркас зоны — линии со смещением глубины.
        if (_floorVertexCount > 0)
        {
            DrawLines(0, _floorVertexCount, mvp, FloorGridColor);
        }

        if (_frameVertexCount > 0)
        {
            DrawLines(_floorVertexCount, _frameVertexCount, mvp, FrameColor);
        }

        // «За пределами зоны»: полупрозрачная заливка + контур.
        if (_fillVertexCount > 0)
        {
            gl.Enable(EnableCap.Blend);
            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            gl.DepthMask(false);
            gl.Enable(EnableCap.PolygonOffsetFill);
            gl.PolygonOffset(-1f, FillPolygonOffsetUnits);

            gl.UseProgram(_meshProgram);
            SetMatrix(_meshMvpLocation, mvp);
            SetMatrix(_meshMvLocation, mv);
            gl.Uniform1(_meshLightLocation, 0f);
            SetColor(_meshColorLocation, OutOfVolumeFillColor);
            BindLayout(_triVao, _triVbo);
            gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_fillVertexCount);

            gl.Disable(EnableCap.PolygonOffsetFill);
            gl.DepthMask(true);
            gl.Disable(EnableCap.Blend);
        }

        if (_outlineVertexCount > 0)
        {
            DrawLines(_floorVertexCount + _frameVertexCount, _outlineVertexCount, mvp, OutOfVolumeEdgeColor);
        }
    }

    public void Dispose()
    {
        if (_gl is not { } gl)
        {
            return;
        }

        if (_lineVbo != 0)
        {
            gl.DeleteBuffer(_lineVbo);
            _lineVbo = 0;
        }

        if (_triVbo != 0)
        {
            gl.DeleteBuffer(_triVbo);
            _triVbo = 0;
        }

        if (_lineVao != 0)
        {
            gl.DeleteVertexArray(_lineVao);
            _lineVao = 0;
        }

        if (_triVao != 0)
        {
            gl.DeleteVertexArray(_triVao);
            _triVao = 0;
        }

        if (_lineProgram != 0)
        {
            gl.DeleteProgram(_lineProgram);
            _lineProgram = 0;
        }

        if (_meshProgram != 0)
        {
            gl.DeleteProgram(_meshProgram);
            _meshProgram = 0;
        }

        _gl = null;
        _uploaded = null;
        _floorVertexCount = 0;
        _frameVertexCount = 0;
        _outlineVertexCount = 0;
        _fillVertexCount = 0;
    }

    /// <summary>Сброс состояния без GL-вызовов (контекст уже потерян).</summary>
    private void ForgetResources()
    {
        _gl = null;
        _lineProgram = 0;
        _meshProgram = 0;
        _lineVbo = 0;
        _triVbo = 0;
        _lineVao = 0;
        _triVao = 0;
        _uploaded = null;
        _floorVertexCount = 0;
        _frameVertexCount = 0;
        _outlineVertexCount = 0;
        _fillVertexCount = 0;
    }

    private void EnsureInitialized(GL gl)
    {
        if (_gl is not null && ReferenceEquals(_gl, gl))
        {
            return;
        }

        if (_gl is not null)
        {
            // Контекст сменился (GL lost): ресурсы старого контекста недействительны
            // и освобождаются вместе с ним — только сбрасываем состояние.
            ForgetResources();
        }

        (bool isEs, int major, int minor) = GlSceneRenderer.ParseVersion(
            GlSceneRenderer.GetStringSafe(gl, StringName.Version));
        _useVao = !isEs || major >= 3;

        (string lineVs, string lineFs) = ShaderSources.SelectLine(isEs, major, minor);
        (string meshVs, string meshFs) = ShaderSources.SelectMesh(isEs, major, minor);
        try
        {
            _lineProgram = BuildProgram(gl, lineVs, lineFs);
            _meshProgram = BuildProgram(gl, meshVs, meshFs);
        }
        catch (InvalidOperationException)
        {
            (lineVs, lineFs) = ShaderSources.SelectLine(false, 1, 1);
            (meshVs, meshFs) = ShaderSources.SelectMesh(false, 1, 1);
            _lineProgram = BuildProgram(gl, lineVs, lineFs);
            _meshProgram = BuildProgram(gl, meshVs, meshFs);
        }

        _lineMvpLocation = gl.GetUniformLocation(_lineProgram, "uMvp");
        _lineColorLocation = gl.GetUniformLocation(_lineProgram, "uColor");
        _lineBiasLocation = gl.GetUniformLocation(_lineProgram, "uDepthBias");
        _meshMvpLocation = gl.GetUniformLocation(_meshProgram, "uMvp");
        _meshMvLocation = gl.GetUniformLocation(_meshProgram, "uMv");
        _meshColorLocation = gl.GetUniformLocation(_meshProgram, "uColor");
        _meshLightLocation = gl.GetUniformLocation(_meshProgram, "uLight");

        _lineVbo = gl.GenBuffer();
        _triVbo = gl.GenBuffer();
        if (_useVao)
        {
            _lineVao = gl.GenVertexArray();
            _triVao = gl.GenVertexArray();
        }

        _gl = gl;
    }

    private void Upload(PrintVolumeOverlay overlay)
    {
        GL gl = _gl!;

        // Один line-буфер: сетка пола, каркас, контур «за пределами зоны».
        float[] floor = overlay.FloorGridLines;
        float[] frame = overlay.FrameLines;
        float[] outline = ExpandTrianglesToLines(overlay.OutOfVolumeTriangles);
        var lineData = new float[floor.Length + frame.Length + outline.Length];
        Array.Copy(floor, 0, lineData, 0, floor.Length);
        Array.Copy(frame, 0, lineData, floor.Length, frame.Length);
        Array.Copy(outline, 0, lineData, floor.Length + frame.Length, outline.Length);
        UploadVertices(gl, _lineVbo, lineData);
        _floorVertexCount = floor.Length / 3;
        _frameVertexCount = frame.Length / 3;
        _outlineVertexCount = outline.Length / 3;
        if (_useVao)
        {
            SetupVao(gl, _lineVao, _lineVbo);
        }

        UploadVertices(gl, _triVbo, overlay.OutOfVolumeTriangles);
        _fillVertexCount = overlay.OutOfVolumeTriangles.Length / 3;
        if (_useVao)
        {
            SetupVao(gl, _triVao, _triVbo);
        }

        _uploaded = overlay;
    }

    /// <summary>Треугольники → рёбра контура (6 вершин на треугольник).</summary>
    private static float[] ExpandTrianglesToLines(float[] triangles)
    {
        int triangleCount = triangles.Length / 9;
        var lines = new float[triangleCount * 18];
        for (int t = 0; t < triangleCount; t++)
        {
            int src = t * 9;
            int dst = t * 18;
            for (int edge = 0; edge < 3; edge++)
            {
                int from = src + (edge * 3);
                int to = src + (((edge + 1) % 3) * 3);
                lines[dst + (edge * 6) + 0] = triangles[from + 0];
                lines[dst + (edge * 6) + 1] = triangles[from + 1];
                lines[dst + (edge * 6) + 2] = triangles[from + 2];
                lines[dst + (edge * 6) + 3] = triangles[to + 0];
                lines[dst + (edge * 6) + 4] = triangles[to + 1];
                lines[dst + (edge * 6) + 5] = triangles[to + 2];
            }
        }

        return lines;
    }

    private void DrawLines(int firstVertex, int vertexCount, float[] mvp, float[] color)
    {
        GL gl = _gl!;
        BindLayout(_lineVao, _lineVbo);
        gl.UseProgram(_lineProgram);
        SetMatrix(_lineMvpLocation, mvp);
        gl.Uniform1(_lineBiasLocation, LineDepthBias);
        SetColor(_lineColorLocation, color);
        gl.DrawArrays(PrimitiveType.Lines, firstVertex, (uint)vertexCount);
    }

    private unsafe void BindLayout(uint vao, uint vbo)
    {
        GL gl = _gl!;
        if (_useVao)
        {
            gl.BindVertexArray(vao);
            return;
        }

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        const uint stride = 3 * sizeof(float);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(0);
    }

    private unsafe void SetupVao(GL gl, uint vao, uint vbo)
    {
        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        const uint stride = 3 * sizeof(float);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(0);
        gl.BindVertexArray(0);
    }

    private static unsafe void UploadVertices(GL gl, uint vbo, float[] data)
    {
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* p = data)
        {
            gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(data.Length * sizeof(float)),
                p,
                BufferUsageARB.StaticDraw);
        }
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

    private static uint BuildProgram(GL gl, string vsSource, string fsSource)
    {
        uint vs = CompileShader(gl, ShaderType.VertexShader, vsSource);
        uint fs = CompileShader(gl, ShaderType.FragmentShader, fsSource);
        uint program = gl.CreateProgram();
        gl.AttachShader(program, vs);
        gl.AttachShader(program, fs);
        gl.BindAttribLocation(program, 0, "aPos");
        gl.LinkProgram(program);
        gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = gl.GetProgramInfoLog(program);
            gl.DeleteShader(vs);
            gl.DeleteShader(fs);
            gl.DeleteProgram(program);
            throw new InvalidOperationException("линковка программы зоны печати: " + log);
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
}

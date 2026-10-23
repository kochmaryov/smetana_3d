using System.Runtime.InteropServices;
using SmetaNA.Core.Model;
using Silk.NET.OpenGL;

namespace SmetaNA.Rendering;

/// <summary>
/// GL-ресурсы и проходы рендера сцены. Все вызовы — через Silk.NET поверх
/// proc-address провайдера Avalonia (путь спика); контекст и FBO принадлежат Avalonia.
/// </summary>
internal sealed class GlSceneRenderer : IDisposable
{
    private static readonly float[] BackgroundColor = [0.14f, 0.16f, 0.19f];
    private static readonly float[] FaceColor = [0.80f, 0.81f, 0.84f, 1f];
    private static readonly float[] XRayFaceColor = [0.80f, 0.81f, 0.84f, 0.32f];
    private static readonly float[] EdgeColor = [0.15f, 0.17f, 0.20f, 1f];
    private static readonly float[] XRayEdgeColor = [0.15f, 0.17f, 0.20f, 0.85f];
    private static readonly float[] GridColor = [0.32f, 0.34f, 0.38f, 1f];
    private static readonly float[] AxisXColor = [0.86f, 0.28f, 0.28f, 1f];
    private static readonly float[] AxisYColor = [0.28f, 0.72f, 0.32f, 1f];
    private static readonly float[] AxisZColor = [0.28f, 0.48f, 0.88f, 1f];
    private static readonly float[] HoverColor = [0.30f, 0.65f, 1.0f, 0.45f];
    private static readonly float[] SelectedColor = [1.0f, 0.62f, 0.18f, 0.45f];
    private static readonly float[] HoverEdgeColor = [0.30f, 0.65f, 1.0f, 1f];
    private static readonly float[] SelectedEdgeColor = [1.0f, 0.62f, 0.18f, 1f];

    private const float EdgeDepthBias = 0.0008f;

    private readonly GL _gl;
    private readonly bool _useVao;

    private uint _meshProgram;
    private uint _lineProgram;
    private int _meshMvpLocation;
    private int _meshMvLocation;
    private int _meshColorLocation;
    private int _meshLightLocation;
    private int _lineMvpLocation;
    private int _lineColorLocation;
    private int _lineBiasLocation;

    private uint _meshVao;
    private uint _edgeVao;
    private uint _gridVao;
    private uint _axisVao;
    private uint _vertexVbo;
    private uint _triangleIbo;
    private uint _edgeIbo;
    private uint _gridVbo;
    private uint _axisVbo;

    private int _edgeIndexCount;
    private int _gridVertexCount;
    private int _axisVertexCount;

    public GlSceneRenderer(GL gl, bool isEs, int major, int minor, out bool usedFallback)
    {
        _gl = gl;
        _useVao = !isEs || major >= 3;

        (string meshVs, string meshFs) = ShaderSources.SelectMesh(isEs, major, minor);
        (string lineVs, string lineFs) = ShaderSources.SelectLine(isEs, major, minor);
        usedFallback = false;
        try
        {
            _meshProgram = BuildProgram(meshVs, meshFs);
            _lineProgram = BuildProgram(lineVs, lineFs);
        }
        catch (InvalidOperationException)
        {
            usedFallback = true;
            (meshVs, meshFs) = ShaderSources.SelectMesh(false, 1, 1);
            (lineVs, lineFs) = ShaderSources.SelectLine(false, 1, 1);
            _meshProgram = BuildProgram(meshVs, meshFs);
            _lineProgram = BuildProgram(lineVs, lineFs);
        }

        _meshMvpLocation = _gl.GetUniformLocation(_meshProgram, "uMvp");
        _meshMvLocation = _gl.GetUniformLocation(_meshProgram, "uMv");
        _meshColorLocation = _gl.GetUniformLocation(_meshProgram, "uColor");
        _meshLightLocation = _gl.GetUniformLocation(_meshProgram, "uLight");
        _lineMvpLocation = _gl.GetUniformLocation(_lineProgram, "uMvp");
        _lineColorLocation = _gl.GetUniformLocation(_lineProgram, "uColor");
        _lineBiasLocation = _gl.GetUniformLocation(_lineProgram, "uDepthBias");

        _vertexVbo = _gl.GenBuffer();
        _triangleIbo = _gl.GenBuffer();
        _edgeIbo = _gl.GenBuffer();
        _gridVbo = _gl.GenBuffer();
        _axisVbo = _gl.GenBuffer();
        if (_useVao)
        {
            _meshVao = _gl.GenVertexArray();
            _edgeVao = _gl.GenVertexArray();
            _gridVao = _gl.GenVertexArray();
            _axisVao = _gl.GenVertexArray();
        }

        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Lequal);
    }

    /// <summary>Загрузить батч сцены (GL-поток, готовый контекст).</summary>
    public void UploadMesh(RenderBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        UploadVertices(_vertexVbo, batch.Vertices);
        UploadIndices(_triangleIbo, batch.TriangleIndices);
        UploadIndices(_edgeIbo, batch.EdgeIndices);
        _edgeIndexCount = batch.EdgeIndices.Length;
        if (_useVao)
        {
            SetupVao(_meshVao, _vertexVbo, _triangleIbo);
            SetupVao(_edgeVao, _vertexVbo, _edgeIbo);
        }
    }

    /// <summary>Загрузить геометрию сетки (пары вершин под GL_LINES).</summary>
    public void UploadGrid(float[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        UploadVertices(_gridVbo, lines);
        _gridVertexCount = lines.Length / 3;
        if (_useVao)
        {
            SetupVao(_gridVao, _gridVbo, 0);
        }
    }

    /// <summary>Загрузить три сегмента осей (X, Y, Z по две вершины).</summary>
    public void UploadAxes(float[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        UploadVertices(_axisVbo, lines);
        _axisVertexCount = lines.Length / 3;
        if (_useVao)
        {
            SetupVao(_axisVao, _axisVbo, 0);
        }
    }

    /// <summary>Один кадр: фон, сетка/оси, грани и рёбра сцены, подсветка выделения.</summary>
    public void Draw(
        int width,
        int height,
        float[] mvp,
        float[] mv,
        RenderMode mode,
        RenderBatch? batch,
        SceneNode? hovered,
        SceneNode? selected,
        bool showGrid,
        bool showAxes)
    {
        _gl.Viewport(0, 0, (uint)Math.Max(width, 1), (uint)Math.Max(height, 1));
        _gl.ClearColor(BackgroundColor[0], BackgroundColor[1], BackgroundColor[2], 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Lequal);
        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(true);

        if (showGrid && _gridVertexCount > 0)
        {
            DrawLines(_gridVao, _gridVbo, 0, _gridVertexCount, mvp, GridColor, 0f);
        }

        if (showAxes && _axisVertexCount >= 6)
        {
            DrawLines(_axisVao, _axisVbo, 0, 2, mvp, AxisXColor, 0f);
            DrawLines(_axisVao, _axisVbo, 2, 2, mvp, AxisYColor, 0f);
            DrawLines(_axisVao, _axisVbo, 4, 2, mvp, AxisZColor, 0f);
        }

        if (batch is not null && batch.Ranges.Count > 0)
        {
            DrawScene(batch, mvp, mv, mode, hovered, selected);
        }
    }

    public void Dispose()
    {
        if (_vertexVbo != 0)
        {
            _gl.DeleteBuffer(_vertexVbo);
        }

        if (_triangleIbo != 0)
        {
            _gl.DeleteBuffer(_triangleIbo);
        }

        if (_edgeIbo != 0)
        {
            _gl.DeleteBuffer(_edgeIbo);
        }

        if (_gridVbo != 0)
        {
            _gl.DeleteBuffer(_gridVbo);
        }

        if (_axisVbo != 0)
        {
            _gl.DeleteBuffer(_axisVbo);
        }

        if (_meshVao != 0)
        {
            _gl.DeleteVertexArray(_meshVao);
        }

        if (_edgeVao != 0)
        {
            _gl.DeleteVertexArray(_edgeVao);
        }

        if (_gridVao != 0)
        {
            _gl.DeleteVertexArray(_gridVao);
        }

        if (_axisVao != 0)
        {
            _gl.DeleteVertexArray(_axisVao);
        }

        if (_meshProgram != 0)
        {
            _gl.DeleteProgram(_meshProgram);
        }

        if (_lineProgram != 0)
        {
            _gl.DeleteProgram(_lineProgram);
        }
    }

    /// <summary>Чтение GL-строк (версия/рендерер) — диагностика для HUD и смоука.</summary>
    public static string GetStringSafe(GL gl, StringName name)
    {
        unsafe
        {
            byte* p = gl.GetString(name);
            return p == null ? "<null>" : Marshal.PtrToStringAnsi((IntPtr)p) ?? "<null>";
        }
    }

    public static (bool IsEs, int Major, int Minor) ParseVersion(string version)
    {
        bool isEs = version.Contains("ES", StringComparison.OrdinalIgnoreCase);
        return (isEs, ParseNumber(version, 0), ParseNumber(version, 1));
    }

    private void DrawScene(RenderBatch batch, float[] mvp, float[] mv, RenderMode mode, SceneNode? hovered, SceneNode? selected)
    {
        bool xray = mode == RenderMode.XRay;
        if (mode is RenderMode.Shaded or RenderMode.XRay)
        {
            if (xray)
            {
                _gl.Enable(EnableCap.Blend);
                _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                _gl.DepthMask(false);
            }

            _gl.UseProgram(_meshProgram);
            SetMatrix(_meshMvpLocation, mvp);
            SetMatrix(_meshMvLocation, mv);
            _gl.Uniform1(_meshLightLocation, 1f);
            SetColor(_meshColorLocation, xray ? XRayFaceColor : FaceColor);
            foreach (ObjectRange range in batch.Ranges)
            {
                DrawTriangles(range);
            }

            if (xray)
            {
                _gl.DepthMask(true);
                _gl.Disable(EnableCap.Blend);
            }
        }

        // Рёбра поверх граней: смещение глубины вместо GL_POLYGON_OFFSET_LINE (нет в GLES).
        _gl.UseProgram(_lineProgram);
        SetMatrix(_lineMvpLocation, mvp);
        _gl.Uniform1(_lineBiasLocation, EdgeDepthBias);
        SetColor(_lineColorLocation, xray ? XRayEdgeColor : EdgeColor);
        DrawEdges(0, _edgeIndexCount);

        if (hovered is not null)
        {
            DrawHighlights(batch, hovered, HoverColor, HoverEdgeColor, mvp, mv);
        }

        if (selected is not null && !ReferenceEquals(selected, hovered))
        {
            DrawHighlights(batch, selected, SelectedColor, SelectedEdgeColor, mvp, mv);
        }
    }

    /// <summary>Подсветка всех диапазонов, принадлежащих узлу (объект может быть из нескольких мешей).</summary>
    private void DrawHighlights(RenderBatch batch, SceneNode node, float[] faceTint, float[] edgeTint, float[] mvp, float[] mv)
    {
        foreach (ObjectRange range in batch.Ranges)
        {
            if (ReferenceEquals(range.Object.SelectionNode, node))
            {
                DrawHighlight(range, faceTint, edgeTint, mvp, mv);
            }
        }
    }

    private void DrawHighlight(ObjectRange range, float[] faceTint, float[] edgeTint, float[] mvp, float[] mv)
    {
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.DepthMask(false);

        _gl.UseProgram(_meshProgram);
        SetMatrix(_meshMvpLocation, mvp);
        SetMatrix(_meshMvLocation, mv);
        _gl.Uniform1(_meshLightLocation, 0f);
        SetColor(_meshColorLocation, faceTint);
        DrawTriangles(range);

        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);

        _gl.UseProgram(_lineProgram);
        SetMatrix(_lineMvpLocation, mvp);
        _gl.Uniform1(_lineBiasLocation, EdgeDepthBias);
        SetColor(_lineColorLocation, edgeTint);
        DrawEdges(range.EdgeFirst, range.EdgeCount);
    }

    private void DrawLines(uint vao, uint vbo, int firstVertex, int vertexCount, float[] mvp, float[] color, float depthBias)
    {
        BindLayout(vao, vbo, 0);
        _gl.UseProgram(_lineProgram);
        SetMatrix(_lineMvpLocation, mvp);
        _gl.Uniform1(_lineBiasLocation, depthBias);
        SetColor(_lineColorLocation, color);
        _gl.DrawArrays(PrimitiveType.Lines, firstVertex, (uint)vertexCount);
    }

    private unsafe void DrawTriangles(ObjectRange range)
    {
        BindLayout(_meshVao, _vertexVbo, _triangleIbo);
        _gl.DrawElements(
            PrimitiveType.Triangles,
            (uint)range.TriangleCount,
            DrawElementsType.UnsignedInt,
            (void*)(range.TriangleFirst * sizeof(uint)));
    }

    private unsafe void DrawEdges(int firstIndex, int indexCount)
    {
        BindLayout(_edgeVao, _vertexVbo, _edgeIbo);
        _gl.DrawElements(
            PrimitiveType.Lines,
            (uint)indexCount,
            DrawElementsType.UnsignedInt,
            (void*)(firstIndex * sizeof(uint)));
    }

    /// <summary>Привязка VAO (или ручная раскладка атрибутов без VAO в legacy-режиме).</summary>
    private unsafe void BindLayout(uint vao, uint vbo, uint ibo)
    {
        if (_useVao)
        {
            _gl.BindVertexArray(vao);
            return;
        }

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        const uint stride = 3 * sizeof(float);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(0);
        if (ibo != 0)
        {
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ibo);
        }
    }

    private unsafe void SetupVao(uint vao, uint vbo, uint ibo)
    {
        _gl.BindVertexArray(vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        const uint stride = 3 * sizeof(float);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(0);
        if (ibo != 0)
        {
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ibo);
        }

        _gl.BindVertexArray(0);
    }

    private void UploadVertices(uint vbo, float[] data)
    {
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        unsafe
        {
            fixed (float* p = data)
            {
                _gl.BufferData(
                    BufferTargetARB.ArrayBuffer,
                    (nuint)(data.Length * sizeof(float)),
                    p,
                    BufferUsageARB.StaticDraw);
            }
        }
    }

    private void UploadIndices(uint ibo, uint[] data)
    {
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ibo);
        unsafe
        {
            fixed (uint* p = data)
            {
                _gl.BufferData(
                    BufferTargetARB.ElementArrayBuffer,
                    (nuint)(data.Length * sizeof(uint)),
                    p,
                    BufferUsageARB.StaticDraw);
            }
        }
    }

    private void SetMatrix(int location, float[] matrix)
    {
        unsafe
        {
            fixed (float* p = matrix)
            {
                _gl.UniformMatrix4(location, 1, false, p);
            }
        }
    }

    private void SetColor(int location, float[] color) =>
        _gl.Uniform4(location, color[0], color[1], color[2], color[3]);

    private uint BuildProgram(string vsSource, string fsSource)
    {
        uint vs = CompileShader(ShaderType.VertexShader, vsSource);
        uint fs = CompileShader(ShaderType.FragmentShader, fsSource);
        uint program = _gl.CreateProgram();
        _gl.AttachShader(program, vs);
        _gl.AttachShader(program, fs);

        // Для legacy-шейдеров без layout(location=...) — фиксируем атрибут до линковки.
        _gl.BindAttribLocation(program, 0, "aPos");
        _gl.LinkProgram(program);
        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = _gl.GetProgramInfoLog(program);
            _gl.DeleteShader(vs);
            _gl.DeleteShader(fs);
            _gl.DeleteProgram(program);
            throw new InvalidOperationException("линковка программы: " + log);
        }

        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);
        return program;
    }

    private uint CompileShader(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compiled);
        if (compiled == 0)
        {
            string log = _gl.GetShaderInfoLog(shader);
            _gl.DeleteShader(shader);
            throw new InvalidOperationException($"компиляция {type}: {log}");
        }

        return shader;
    }

    private static int ParseNumber(string version, int index)
    {
        var parts = new List<string>();
        int i = 0;
        while (i < version.Length && parts.Count <= index)
        {
            while (i < version.Length && !char.IsDigit(version[i]))
            {
                i++;
            }

            int start = i;
            while (i < version.Length && (char.IsDigit(version[i]) || version[i] == '.'))
            {
                i++;
            }

            if (i > start)
            {
                foreach (string piece in version[start..i].Split('.'))
                {
                    parts.Add(piece);
                }
            }
        }

        return parts.Count > index && int.TryParse(parts[index], out int value) ? value : 0;
    }
}

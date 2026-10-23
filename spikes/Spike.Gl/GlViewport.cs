using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.VisualTree;
using Silk.NET.OpenGL;

namespace Spike.Gl;

/// <summary>
/// OpenGL-вьюпорт: контекст и FBO даёт Avalonia (OpenGlControlBase),
/// все GL-вызовы идут через биндинги Silk.NET поверх GlInterface.GetProcAddress.
/// </summary>
public sealed class GlViewport : OpenGlControlBase
{
    private GL? _gl;
    private uint _program;
    private uint _vbo;
    private uint _vao;
    private int _mvpLocation;
    private bool _useVao;
    private bool _glEverReady;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _prevFrameTime;
    private double _firstFrameTime;
    private double _lastFrameTime;
    private double _rotationAngle;

    private long _framesTotal;
    private double _fps;
    private double _frameMs;
    private double _minBucketFps = double.MaxValue;
    private double _bucketStart;
    private int _bucketFrames;
    private bool _warmupDone;

    private string _glInfo = "GL: инициализация…";

    public double Fps => _fps;

    public double FrameMs => _frameMs;

    public long FramesTotal => _framesTotal;

    public double MinFps => _warmupDone && _minBucketFps < double.MaxValue ? _minBucketFps : 0;

    public double AverageFps
    {
        get
        {
            double span = _lastFrameTime - _firstFrameTime;
            return span > 0 && _framesTotal > 1 ? (_framesTotal - 1) / span : 0;
        }
    }

    public string GlInfo => _glInfo;

    public bool GlReady => _gl is not null;

    /// <summary>GL-инициализация прошла успешно хотя бы раз (не сбрасывается при закрытии).</summary>
    public bool GlEverReady => _glEverReady;

    protected override void OnOpenGlInit(GlInterface glInterface)
    {
        try
        {
            GL api = GL.GetApi(name => glInterface.GetProcAddress(name));
            _gl = api;

            string version = GetString(api, StringName.Version);
            string renderer = GetString(api, StringName.Renderer);
            string glsl = GetString(api, StringName.ShadingLanguageVersion);
            bool isEs = version.Contains("ES", StringComparison.OrdinalIgnoreCase);
            int major = ParseMajor(version);
            int minor = ParseMinor(version);

            _glInfo = string.Create(CultureInfo.InvariantCulture,
                $"{renderer} | {version} | GLSL {glsl} | mode: {SpikeOptions.ModeLabel}");
            Console.WriteLine($"[Spike.Gl] GL context: {version}");
            Console.WriteLine($"[Spike.Gl] GL renderer: {renderer}");
            Console.WriteLine($"[Spike.Gl] GLSL: {glsl}");
            Console.WriteLine($"[Spike.Gl] profile: {(isEs ? "OpenGLES" : "OpenGl")} {major}.{minor}, mode: {SpikeOptions.ModeLabel}");

            (string vs, string fs) = SelectShaders(isEs, major, minor);
            _program = BuildProgram(api, vs, fs, out bool usedFallback);
            if (usedFallback)
            {
                Console.WriteLine("[Spike.Gl] основной шейдер не собрался — используется legacy-вариант");
            }

            _mvpLocation = api.GetUniformLocation(_program, "uMvp");
            if (_mvpLocation < 0)
            {
                throw new InvalidOperationException("uniform uMvp не найден в шейдере");
            }

            float[] vertices = BuildCubeVertices();
            _vbo = api.GenBuffer();
            api.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            unsafe
            {
                fixed (float* p = vertices)
                {
                    api.BufferData(BufferTargetARB.ArrayBuffer,
                        (nuint)(vertices.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
                }
            }

            // VAO обязателен в core-профилях; в GLES 2.0 его нет — там рисуем без VAO.
            _useVao = !isEs || major >= 3;
            if (_useVao)
            {
                _vao = api.GenVertexArray();
                api.BindVertexArray(_vao);
            }

            api.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            SetupVertexLayout(api);
            api.Enable(EnableCap.DepthTest);
            _glEverReady = true;
        }
        catch (Exception ex)
        {
            _glInfo = "GL ERROR: " + ex.Message;
            Console.WriteLine($"[Spike.Gl] GL init failed: {ex}");
        }
    }

    protected override void OnOpenGlRender(GlInterface glInterface, int fb)
    {
        if (_gl is not { } api)
        {
            return;
        }

        double now = _clock.Elapsed.TotalSeconds;
        double dt = _prevFrameTime <= 0 ? 1.0 / 60.0 : Math.Clamp(now - _prevFrameTime, 0.0, 0.25);
        _prevFrameTime = now;
        _rotationAngle += dt * 1.1;

        UpdateStats(now, dt);

        // Размер FBO = Bounds × RenderScaling — та же формула, что у OpenGlControlBase.
        double scaling = this.GetPresentationSource()?.RenderScaling ?? 1.0;
        int width = Math.Max(1, (int)(Bounds.Width * scaling));
        int height = Math.Max(1, (int)(Bounds.Height * scaling));

        api.Viewport(0, 0, (uint)width, (uint)height);
        api.ClearColor(0.07f, 0.09f, 0.12f, 1f);
        api.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        api.Enable(EnableCap.DepthTest);

        float[] model = Mat4.Multiply(Mat4.RotationX(0.45f), Mat4.RotationY((float)_rotationAngle));
        float[] view = Mat4.Translation(0f, 0f, -2.6f);
        float[] projection = Mat4.Perspective(MathF.PI / 4f, (float)width / height, 0.1f, 100f);
        float[] mvp = Mat4.Multiply(projection, Mat4.Multiply(view, model));

        api.UseProgram(_program);
        unsafe
        {
            fixed (float* p = mvp)
            {
                api.UniformMatrix4(_mvpLocation, 1, false, p);
            }
        }

        if (_useVao)
        {
            api.BindVertexArray(_vao);
        }

        api.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        SetupVertexLayout(api);
        api.DrawArrays(PrimitiveType.Triangles, 0, 36);

        RequestNextFrameRendering();
    }

    protected override void OnOpenGlDeinit(GlInterface glInterface)
    {
        if (_gl is { } api)
        {
            if (_vbo != 0)
            {
                api.DeleteBuffer(_vbo);
            }

            if (_vao != 0)
            {
                api.DeleteVertexArray(_vao);
            }

            if (_program != 0)
            {
                api.DeleteProgram(_program);
            }

            api.Dispose();
        }

        _gl = null;
        _vbo = 0;
        _vao = 0;
        _program = 0;
        base.OnOpenGlDeinit(glInterface);
    }

    protected override void OnOpenGlLost()
    {
        _gl = null;
        _glInfo = "GL: контекст потерян";
        Console.WriteLine("[Spike.Gl] GL context lost");
        base.OnOpenGlLost();
    }

    private void UpdateStats(double now, double dt)
    {
        _framesTotal++;
        if (_firstFrameTime <= 0)
        {
            _firstFrameTime = now;
        }

        _lastFrameTime = now;
        double instantaneous = 1.0 / dt;
        _fps = _fps <= 0 ? instantaneous : (_fps * 0.92) + (instantaneous * 0.08);
        _frameMs = dt * 1000.0;

        if (_bucketStart <= 0)
        {
            _bucketStart = now;
            _bucketFrames = 0;
        }

        _bucketFrames++;
        double bucketDuration = now - _bucketStart;
        if (bucketDuration >= 1.0)
        {
            if (now > 2.0)
            {
                _warmupDone = true;
                double bucketFps = _bucketFrames / bucketDuration;
                if (bucketFps < _minBucketFps)
                {
                    _minBucketFps = bucketFps;
                }
            }

            _bucketStart = now;
            _bucketFrames = 0;
        }
    }

    private static (string Vs, string Fs) SelectShaders(bool isEs, int major, int minor)
    {
        if (isEs)
        {
            return major >= 3 ? (Vs300Es, Fs300Es) : (Vs100, Fs100);
        }

        bool modern = major > 3 || (major == 3 && minor >= 3);
        return modern ? (Vs330, Fs330) : (Vs110, Fs110);
    }

    private static uint BuildProgram(GL api, string vsSource, string fsSource, out bool usedFallback)
    {
        usedFallback = false;
        try
        {
            return BuildProgramCore(api, vsSource, fsSource);
        }
        catch (InvalidOperationException)
        {
            usedFallback = true;
            return BuildProgramCore(api, Vs110, Fs110);
        }
    }

    private static uint BuildProgramCore(GL api, string vsSource, string fsSource)
    {
        uint vs = CompileShader(api, ShaderType.VertexShader, vsSource);
        uint fs = CompileShader(api, ShaderType.FragmentShader, fsSource);
        uint program = api.CreateProgram();
        api.AttachShader(program, vs);
        api.AttachShader(program, fs);

        // Для legacy-шейдеров без layout(location=...): фиксируем атрибуты до линковки.
        api.BindAttribLocation(program, 0, "aPos");
        api.BindAttribLocation(program, 1, "aColor");
        api.LinkProgram(program);
        api.GetProgram(program, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = api.GetProgramInfoLog(program);
            api.DeleteShader(vs);
            api.DeleteShader(fs);
            api.DeleteProgram(program);
            throw new InvalidOperationException("линковка программы: " + log);
        }

        api.DeleteShader(vs);
        api.DeleteShader(fs);
        return program;
    }

    private static uint CompileShader(GL api, ShaderType type, string source)
    {
        uint shader = api.CreateShader(type);
        api.ShaderSource(shader, source);
        api.CompileShader(shader);
        api.GetShader(shader, ShaderParameterName.CompileStatus, out int compiled);
        if (compiled == 0)
        {
            string log = api.GetShaderInfoLog(shader);
            api.DeleteShader(shader);
            throw new InvalidOperationException($"компиляция {type}: {log}");
        }

        return shader;
    }

    private static unsafe void SetupVertexLayout(GL api)
    {
        const uint stride = 6 * sizeof(float);
        api.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        api.EnableVertexAttribArray(0);
        api.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        api.EnableVertexAttribArray(1);
    }

    private static unsafe string GetString(GL api, StringName name)
    {
        byte* p = api.GetString(name);
        return p == null ? "<null>" : Marshal.PtrToStringAnsi((IntPtr)p) ?? "<null>";
    }

    private static int ParseMajor(string version) => ParseNumber(version, 0);

    private static int ParseMinor(string version) => ParseNumber(version, 1);

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

    private static float[] BuildCubeVertices()
    {
        const float h = 0.5f;
        var data = new List<float>(36 * 6);

        // Каждый квад: (a,b,c,d) — CCW снаружи; треугольники (a,b,c) и (a,c,d).
        void Quad(
            float ax, float ay, float az, float bx, float by, float bz,
            float cx, float cy, float cz, float dx, float dy, float dz,
            float r, float g, float b)
        {
            float[] corners =
            {
                ax, ay, az, bx, by, bz, cx, cy, cz,
                ax, ay, az, cx, cy, cz, dx, dy, dz,
            };
            for (int i = 0; i < corners.Length; i += 3)
            {
                data.Add(corners[i]);
                data.Add(corners[i + 1]);
                data.Add(corners[i + 2]);
                data.Add(r);
                data.Add(g);
                data.Add(b);
            }
        }

        Quad(-h, -h, h, h, -h, h, h, h, h, -h, h, h, 0.20f, 0.55f, 0.95f);
        Quad(h, -h, -h, -h, -h, -h, -h, h, -h, h, h, -h, 0.14f, 0.34f, 0.62f);
        Quad(h, -h, h, h, -h, -h, h, h, -h, h, h, h, 0.90f, 0.36f, 0.30f);
        Quad(-h, -h, -h, -h, -h, h, -h, h, h, -h, h, -h, 0.62f, 0.22f, 0.18f);
        Quad(-h, h, h, h, h, h, h, h, -h, -h, h, -h, 0.45f, 0.85f, 0.40f);
        Quad(-h, -h, -h, h, -h, -h, h, -h, h, -h, -h, h, 0.28f, 0.52f, 0.26f);

        return data.ToArray();
    }

    private const string Vs330 = """
        #version 330 core
        layout(location = 0) in vec3 aPos;
        layout(location = 1) in vec3 aColor;
        uniform mat4 uMvp;
        out vec3 vColor;
        void main()
        {
            vColor = aColor;
            gl_Position = uMvp * vec4(aPos, 1.0);
        }
        """;

    private const string Fs330 = """
        #version 330 core
        in vec3 vColor;
        out vec4 FragColor;
        void main()
        {
            FragColor = vec4(vColor, 1.0);
        }
        """;

    private const string Vs300Es = """
        #version 300 es
        precision highp float;
        layout(location = 0) in vec3 aPos;
        layout(location = 1) in vec3 aColor;
        uniform mat4 uMvp;
        out vec3 vColor;
        void main()
        {
            vColor = aColor;
            gl_Position = uMvp * vec4(aPos, 1.0);
        }
        """;

    private const string Fs300Es = """
        #version 300 es
        precision highp float;
        in vec3 vColor;
        out vec4 FragColor;
        void main()
        {
            FragColor = vec4(vColor, 1.0);
        }
        """;

    private const string Vs100 = """
        #version 100
        precision highp float;
        attribute vec3 aPos;
        attribute vec3 aColor;
        uniform mat4 uMvp;
        varying vec3 vColor;
        void main()
        {
            vColor = aColor;
            gl_Position = uMvp * vec4(aPos, 1.0);
        }
        """;

    private const string Fs100 = """
        #version 100
        precision highp float;
        varying vec3 vColor;
        void main()
        {
            gl_FragColor = vec4(vColor, 1.0);
        }
        """;

    private const string Vs110 = """
        #version 110
        attribute vec3 aPos;
        attribute vec3 aColor;
        uniform mat4 uMvp;
        varying vec3 vColor;
        void main()
        {
            vColor = aColor;
            gl_Position = uMvp * vec4(aPos, 1.0);
        }
        """;

    private const string Fs110 = """
        #version 110
        varying vec3 vColor;
        void main()
        {
            gl_FragColor = vec4(vColor, 1.0);
        }
        """;
}

namespace SmetaNA.Rendering;

/// <summary>
/// GLSL-исходники шейдеров в двух основных диалектах (330 core для desktop GL,
/// 300 es для GLES3/ANGLE) и legacy-страховке 110 — путь интеграции спика
/// (spikes/Spike.Gl/README.md, раздел «Как интегрирован GL-контекст»).
/// Свет — плоский, нормаль берётся из производных экрана (dFdx/dFdy),
/// поэтому вершины остаются общими и буфер вдвое меньше.
/// </summary>
internal static class ShaderSources
{
    public static (string Vs, string Fs) SelectMesh(bool isEs, int major, int minor)
    {
        if (isEs)
        {
            return major >= 3 ? (MeshVs300Es, MeshFs300Es) : (MeshVs110, MeshFs110);
        }

        bool modern = major > 3 || (major == 3 && minor >= 3);
        return modern ? (MeshVs330, MeshFs330) : (MeshVs110, MeshFs110);
    }

    public static (string Vs, string Fs) SelectLine(bool isEs, int major, int minor)
    {
        if (isEs)
        {
            return major >= 3 ? (LineVs300Es, LineFs300Es) : (LineVs110, LineFs110);
        }

        bool modern = major > 3 || (major == 3 && minor >= 3);
        return modern ? (LineVs330, LineFs330) : (LineVs110, LineFs110);
    }

    private const string MeshVs330 = """
        #version 330 core
        layout(location = 0) in vec3 aPos;
        uniform mat4 uMvp;
        uniform mat4 uMv;
        out vec3 vViewPos;
        void main()
        {
            vec4 viewPos = uMv * vec4(aPos, 1.0);
            vViewPos = viewPos.xyz;
            gl_Position = uMvp * vec4(aPos, 1.0);
        }
        """;

    private const string MeshFs330 = """
        #version 330 core
        in vec3 vViewPos;
        uniform vec4 uColor;
        uniform float uLight;
        out vec4 FragColor;
        void main()
        {
            vec3 n = normalize(cross(dFdx(vViewPos), dFdy(vViewPos)));
            float diff = abs(dot(n, normalize(vec3(0.35, 0.25, 0.9))));
            float light = mix(1.0, 0.35 + 0.65 * diff, uLight);
            FragColor = vec4(uColor.rgb * light, uColor.a);
        }
        """;

    private const string MeshVs300Es = """
        #version 300 es
        precision highp float;
        layout(location = 0) in vec3 aPos;
        uniform mat4 uMvp;
        uniform mat4 uMv;
        out vec3 vViewPos;
        void main()
        {
            vec4 viewPos = uMv * vec4(aPos, 1.0);
            vViewPos = viewPos.xyz;
            gl_Position = uMvp * vec4(aPos, 1.0);
        }
        """;

    private const string MeshFs300Es = """
        #version 300 es
        precision highp float;
        in vec3 vViewPos;
        uniform vec4 uColor;
        uniform float uLight;
        out vec4 FragColor;
        void main()
        {
            vec3 n = normalize(cross(dFdx(vViewPos), dFdy(vViewPos)));
            float diff = abs(dot(n, normalize(vec3(0.35, 0.25, 0.9))));
            float light = mix(1.0, 0.35 + 0.65 * diff, uLight);
            FragColor = vec4(uColor.rgb * light, uColor.a);
        }
        """;

    private const string MeshVs110 = """
        #version 110
        attribute vec3 aPos;
        uniform mat4 uMvp;
        uniform mat4 uMv;
        varying vec3 vViewPos;
        void main()
        {
            vec4 viewPos = uMv * vec4(aPos, 1.0);
            vViewPos = viewPos.xyz;
            gl_Position = uMvp * vec4(aPos, 1.0);
        }
        """;

    private const string MeshFs110 = """
        #version 110
        varying vec3 vViewPos;
        uniform vec4 uColor;
        uniform float uLight;
        void main()
        {
            // GLSL 1.10 без производных — плоский цвет.
            gl_FragColor = uColor;
        }
        """;

    private const string LineVs330 = """
        #version 330 core
        layout(location = 0) in vec3 aPos;
        uniform mat4 uMvp;
        uniform float uDepthBias;
        void main()
        {
            vec4 p = uMvp * vec4(aPos, 1.0);
            p.z -= p.w * uDepthBias;
            gl_Position = p;
        }
        """;

    private const string LineFs330 = """
        #version 330 core
        uniform vec4 uColor;
        out vec4 FragColor;
        void main()
        {
            FragColor = uColor;
        }
        """;

    private const string LineVs300Es = """
        #version 300 es
        precision highp float;
        layout(location = 0) in vec3 aPos;
        uniform mat4 uMvp;
        uniform float uDepthBias;
        void main()
        {
            vec4 p = uMvp * vec4(aPos, 1.0);
            p.z -= p.w * uDepthBias;
            gl_Position = p;
        }
        """;

    private const string LineFs300Es = """
        #version 300 es
        precision highp float;
        uniform vec4 uColor;
        out vec4 FragColor;
        void main()
        {
            FragColor = uColor;
        }
        """;

    private const string LineVs110 = """
        #version 110
        attribute vec3 aPos;
        uniform mat4 uMvp;
        uniform float uDepthBias;
        void main()
        {
            vec4 p = uMvp * vec4(aPos, 1.0);
            p.z -= p.w * uDepthBias;
            gl_Position = p;
        }
        """;

    private const string LineFs110 = """
        #version 110
        uniform vec4 uColor;
        void main()
        {
            gl_FragColor = uColor;
        }
        """;
}

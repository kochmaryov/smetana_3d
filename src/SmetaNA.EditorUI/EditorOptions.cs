namespace SmetaNA.EditorUI;

/// <summary>
/// Опции запуска редактора: <c>--smoke [сек]</c> (смоук-окно, см. Smoke),
/// <c>--fps N</c> (лимит кадров вьюпорта, 0 — без лимита), <c>--wgl</c>
/// (нативный Windows OpenGL вместо дефолтного ANGLE — для замеров).
/// </summary>
public static class EditorOptions
{
    /// <summary>Целевой FPS вьюпорта (frame pacing); 0 — без лимита.</summary>
    public static double TargetFps { get; private set; } = 60;

    /// <summary>Форс WGL на Windows (игнорируется на других ОС).</summary>
    public static bool ForceWgl { get; private set; }

    public static string ModeLabel => ForceWgl ? "wgl" : "default(AngleEgl)";

    public static void Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        for (int i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--fps" when i + 1 < args.Count &&
                    double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double fps) && fps >= 0:
                    TargetFps = fps;
                    i++;
                    break;
                case "--wgl":
                    ForceWgl = true;
                    break;
            }
        }
    }
}

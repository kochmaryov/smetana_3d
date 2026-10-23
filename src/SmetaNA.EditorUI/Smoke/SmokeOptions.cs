namespace SmetaNA.EditorUI.Smoke;

/// <summary>Параметры смоук-запуска окна (краткий запуск с автозакрытием).</summary>
public readonly record struct SmokeOptions(int Seconds)
{
    /// <summary>Разобрать аргументы: <c>--smoke [секунды]</c>.</summary>
    public static SmokeOptions? Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        for (int i = 0; i < args.Count; i++)
        {
            if (!string.Equals(args[i], "--smoke", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int seconds = 5;
            if (i + 1 < args.Count && int.TryParse(args[i + 1], out int parsed) && parsed > 0)
            {
                seconds = parsed;
            }

            return new SmokeOptions(seconds);
        }

        return null;
    }
}

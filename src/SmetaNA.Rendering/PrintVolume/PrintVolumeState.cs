namespace SmetaNA.Rendering.PrintVolume;

/// <summary>
/// Общее состояние зоны печати (задача 4.5): выбранный профиль принтера, который
/// показывают все GL-вьюпорты. Мост UI → рендер без правок существующих файлов:
/// панель профиля пишет <see cref="Current"/>, вьюпорт читает его каждый кадр.
/// <c>null</c> — зона печати выключена (поведение вьюпорта по умолчанию не меняется).
/// </summary>
public static class PrintVolumeState
{
    private static PrinterProfile? _current;

    /// <summary>Текущий профиль принтера или <c>null</c> (зона печати выключена).</summary>
    public static PrinterProfile? Current
    {
        get => _current;
        set
        {
            if (ReferenceEquals(_current, value))
            {
                return;
            }

            _current = value;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>Текущий профиль изменился (включая переход в <c>null</c>).</summary>
    public static event EventHandler? Changed;
}

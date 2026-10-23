namespace SmetaNA.Core.Tools;

/// <summary>
/// Инструмент редактора: получает модель ввода (указатель/клавиши) и публикует
/// результат — предпросмотр для отрисовки и состояние числового ввода (VCB).
/// Жизненный цикл: <see cref="Activate"/> → события → <see cref="Deactivate"/>;
/// <see cref="Cancel"/> прерывает текущее взаимодействие без изменения сцены.
/// </summary>
public interface ITool
{
    /// <summary>Идентификатор инструмента (совпадает с идентификатором команды).</summary>
    string Id { get; }

    /// <summary>Идёт непрерывное взаимодействие (перетаскивание): UI не должен троттлить ввод.</summary>
    bool IsInteracting { get; }

    /// <summary>Состояние числового ввода (VCB).</summary>
    ToolVcbState Vcb { get; }

    /// <summary>Текущий предпросмотр для отрисовки.</summary>
    ToolPreview Preview { get; }

    /// <summary>Предпросмотр или состояние VCB изменились.</summary>
    event Action? StateChanged;

    void Activate(ToolContext context);

    void Deactivate();

    /// <summary>Отмена текущего действия инструмента (сцена не меняется).</summary>
    void Cancel();

    /// <summary>Событие указателя; <c>true</c> — событие потреблено инструментом.</summary>
    bool OnPointer(PointerInput input);

    /// <summary>Событие клавиши; <c>true</c> — событие потреблено инструментом.</summary>
    bool OnKey(KeyInput input);

    /// <summary>
    /// Числовой ввод из VCB (мм для длин); <c>true</c> — значение применено
    /// инструментом.
    /// </summary>
    bool TryApplyValue(double value);
}

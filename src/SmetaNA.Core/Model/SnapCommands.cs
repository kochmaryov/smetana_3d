using SmetaNA.Core.Geometry;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Model;

/// <summary>
/// Команда смены кадра пользовательской точки сборки (позиция/нормаль/up)
/// с отменой — правка ориентации Snap'а в режиме редактирования.
/// </summary>
public sealed class SetSnapFrameCommand : IEditCommand
{
    private readonly SnapNode _snap;
    private readonly SnapFrame _frame;
    private SnapFrame _previous;

    public SetSnapFrameCommand(SnapNode snap, SnapFrame frame)
    {
        ArgumentNullException.ThrowIfNull(snap);
        _snap = snap;
        _frame = frame.Normalized();
    }

    public string Description => "Ориентация точки сборки";

    public void Execute()
    {
        _previous = _snap.Frame;
        _snap.Frame = _frame;
    }

    public void Undo() => _snap.Frame = _previous;
}

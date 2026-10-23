using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SmetaNA.EditorUI.ViewModels;

/// <summary>База view-моделей: INPC + обновление всех привязок.</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Обновить все привязки (смена языка, темы и т.п.).</summary>
    protected void InvalidateAll() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

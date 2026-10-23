using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Tutorial;

namespace SmetaNA.EditorUI.ViewModels;

/// <summary>
/// Навигация по интерактивному уроку (задача 6.2): шаги, подсветка инструмента
/// текущего шага и статус выполнения. Логика не зависит от Avalonia — тестируется юнитами.
/// </summary>
public sealed class TutorialViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private int _currentStepIndex;
    private bool _isActive;
    private bool _isCompleted;

    public TutorialViewModel(ILocalizationService localization, TutorialLesson lesson)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(lesson);
        if (lesson.Steps.Count == 0)
        {
            throw new ArgumentException("Урок должен содержать хотя бы один шаг.", nameof(lesson));
        }

        _localization = localization;
        Lesson = lesson;

        StartCommand = new RelayCommand(_ => Start(), null);
        NextCommand = new RelayCommand(_ => Next(), _ => CanGoNext);
        BackCommand = new RelayCommand(_ => Back(), _ => CanGoBack);
        RestartCommand = new RelayCommand(_ => Restart(), _ => IsActive);
        CloseCommand = new RelayCommand(_ => Close(), _ => IsActive);

        _localization.CultureChanged += OnCultureChanged;
    }

    public TutorialLesson Lesson { get; }

    /// <summary>Урок запущен и ещё не закрыт.</summary>
    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (SetField(ref _isActive, value))
            {
                RaiseStateChanged();
            }
        }
    }

    /// <summary>Все шаги пройдены.</summary>
    public bool IsCompleted
    {
        get => _isCompleted;
        private set
        {
            if (SetField(ref _isCompleted, value))
            {
                RaiseStateChanged();
            }
        }
    }

    /// <summary>Индекс текущего шага (0-based).</summary>
    public int CurrentStepIndex => _currentStepIndex;

    public int StepCount => Lesson.Steps.Count;

    public bool CanGoNext => IsActive && !IsCompleted;

    public bool CanGoBack => IsActive && _currentStepIndex > 0;

    /// <summary>Команда инструмента текущего шага для подсветки (или <c>null</c>).</summary>
    public string? HighlightedCommandId =>
        IsActive && !IsCompleted ? Lesson.Steps[_currentStepIndex].CommandId : null;

    public string Title => _localization.Get(Lesson.NameKey);

    public string Intro => _localization.Get(Lesson.IntroKey);

    /// <summary>Текст текущего шага; по завершении — поздравление.</summary>
    public string CurrentStepText =>
        IsCompleted
            ? _localization.Get(Lesson.DoneKey)
            : _localization.Get(Lesson.Steps[_currentStepIndex].TextKey);

    /// <summary>Прогресс «Шаг N из M»; по завершении — отметка о завершении.</summary>
    public string ProgressText =>
        IsCompleted
            ? _localization.Get(LocalizationKeys.TutorialCompleted)
            : string.Format(
                _localization.Get(LocalizationKeys.TutorialProgress),
                _currentStepIndex + 1,
                StepCount);

    public RelayCommand StartCommand { get; }
    public RelayCommand NextCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand RestartCommand { get; }
    public RelayCommand CloseCommand { get; }

    /// <summary>Состояние урока изменилось (шаг, активность, завершение).</summary>
    public event Action? StateChanged;

    /// <summary>Начать урок с первого шага.</summary>
    public void Start()
    {
        _currentStepIndex = 0;
        IsCompleted = false;
        IsActive = true;
        RaiseStateChanged();
    }

    /// <summary>Следующий шаг; после последнего — завершение урока.</summary>
    public void Next()
    {
        if (!CanGoNext)
        {
            return;
        }

        if (_currentStepIndex >= StepCount - 1)
        {
            IsCompleted = true;
        }
        else
        {
            _currentStepIndex++;
        }

        RaiseStateChanged();
    }

    /// <summary>Предыдущий шаг; из завершения — возврат к последнему шагу.</summary>
    public void Back()
    {
        if (IsCompleted)
        {
            IsCompleted = false;
            RaiseStateChanged();
            return;
        }

        if (!CanGoBack)
        {
            return;
        }

        _currentStepIndex--;
        RaiseStateChanged();
    }

    /// <summary>Начать урок заново.</summary>
    public void Restart()
    {
        if (!IsActive)
        {
            return;
        }

        Start();
    }

    /// <summary>Закрыть урок (подсветка гасится).</summary>
    public void Close()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        IsCompleted = false;
        _currentStepIndex = 0;
        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        OnPropertyChanged(nameof(CurrentStepIndex));
        OnPropertyChanged(nameof(CurrentStepText));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(HighlightedCommandId));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
        NextCommand.RaiseCanExecuteChanged();
        BackCommand.RaiseCanExecuteChanged();
        RestartCommand.RaiseCanExecuteChanged();
        CloseCommand.RaiseCanExecuteChanged();
        StateChanged?.Invoke();
    }

    private void OnCultureChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Intro));
        OnPropertyChanged(nameof(CurrentStepText));
        OnPropertyChanged(nameof(ProgressText));
    }
}

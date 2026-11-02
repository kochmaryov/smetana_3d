using SmetaNA.Core.Model;
using SmetaNA.EditorUI.Tools;
using ProjectDocument = SmetaNA.Formats.ProjectFile.ProjectDocument;

namespace SmetaNA.EditorUI.Sessions;

/// <summary>
/// Состояние редактора окна: активный документ проекта (сцена, история и
/// инструменты), горячие клавиши и переключение между вкладками проектов.
/// </summary>
public sealed class EditorSession
{
    public EditorSession()
    {
        Shortcuts = new Commands.ShortcutMap();

        // Документ «вне проектов»: стартовый экран до открытия первой вкладки
        // (инструменты и VCB привязаны к нему) либо сцена смоук-режима.
        Document = new EditorDocument();
        Document.History.Changed += OnActiveHistoryChanged;
    }

    /// <summary>Активный документ вкладки.</summary>
    public EditorDocument Document { get; private set; }

    /// <summary>Сцена активного документа.</summary>
    public Scene Scene => Document.Scene;

    /// <summary>История активного документа.</summary>
    public UndoHistoryService History => Document.History;

    /// <summary>Инструменты редактора (маршрутизация ввода вьюпорта).</summary>
    public ToolController Tools => Document.Tools;

    public Commands.ShortcutMap Shortcuts { get; }

    /// <summary>История активного документа изменилась.</summary>
    public event Action? HistoryChanged;

    /// <summary>
    /// Сделать документ активным: инструменты прежнего документа выключаются,
    /// чтобы ввод вьюпорта не попадал в неактивную вкладку.
    /// </summary>
    public void Activate(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (ReferenceEquals(Document, document))
        {
            return;
        }

        Document.Tools.Deactivate();
        Document.History.Changed -= OnActiveHistoryChanged;
        Document = document;
        Document.History.Changed += OnActiveHistoryChanged;
    }

    /// <summary>Новый пустой документ для вкладки проекта.</summary>
    public EditorDocument CreateDocument(LengthUnit unit = LengthUnit.Millimeter) =>
        new(new Scene { Unit = unit });

    private void OnActiveHistoryChanged() => HistoryChanged?.Invoke();

    /// <summary>
    /// Загрузить документ проекта в активный документ: содержимое заменяется,
    /// история сбрасывается (новый файл — новая история), inference-кэш чистится.
    /// </summary>
    public void LoadDocument(ProjectDocument document) => LoadDocument(Document, document);

    /// <summary>Загрузить документ проекта в указанный документ (новая вкладка).</summary>
    public void LoadDocument(EditorDocument target, ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(document);

        Scene scene = target.Scene;
        while (scene.Root.Children.Count > 0)
        {
            scene.Root.RemoveChild(scene.Root.Children[^1]);
        }

        foreach (Tag tag in scene.Tags.Tags.ToList())
        {
            scene.Tags.Remove(tag);
        }

        Scene source = document.Scene;
        foreach (Tag tag in source.Tags.Tags.ToList())
        {
            source.Tags.Remove(tag);
            scene.Tags.Add(tag);
        }

        while (source.Root.Children.Count > 0)
        {
            SceneNode child = source.Root.Children[0];
            source.Root.RemoveChild(child);
            scene.Root.AddChild(child);
        }

        scene.Unit = source.Unit;
        target.Tools.Context.InvalidateInferenceCache();
        target.History.Clear();
    }
}

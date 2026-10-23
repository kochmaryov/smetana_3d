namespace SmetaNA.EditorUI.Smoke;

/// <summary>Итог смоук-проверки главного окна.</summary>
/// <param name="Passed">Все проверки прошли.</param>
/// <param name="Summary">Строка-сводка для вывода.</param>
public sealed record SmokeReport(bool Passed, string Summary);

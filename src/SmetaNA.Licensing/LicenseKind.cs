namespace SmetaNA.Licensing;

/// <summary>Тип лицензии, закодированный в ключе.</summary>
public enum LicenseKind : byte
{
    /// <summary>Бессрочная лицензия (разовая покупка).</summary>
    Perpetual = 1,

    /// <summary>Подписка: действует до даты окончания в ключе.</summary>
    Subscription = 2,
}

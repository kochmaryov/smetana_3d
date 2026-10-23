namespace SmetaNA.Licensing;

/// <summary>Источник идентификатора устройства для node-lock.</summary>
public interface IDeviceFingerprintProvider
{
    /// <summary>Возвращает текущий идентификатор устройства.</summary>
    DeviceFingerprint GetFingerprint();
}

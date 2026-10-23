using System.Security.Cryptography;

namespace SmetaNA.Licensing;

/// <summary>
/// Хранилище локального лицензионного состояния: подписанные файлы активации и триала плюс локальный ключ устройства.
/// Файлы подписаны ECDSA-ключом, который генерируется на устройстве при первой записи; подпись ловит
/// простую подделку (правку дат и полей в текстовом редакторе). Локальный ключ хранится рядом —
/// это защита от «простой» подделки, а не DRM: злоумышленник с полным доступом к машине сможет переподписать файл.
/// </summary>
public sealed class LocalStateStore : IDisposable
{
    /// <summary>Имя файла состояния активации.</summary>
    public const string ActivationStateFile = "activation.state";

    /// <summary>Имя файла состояния триала.</summary>
    public const string TrialStateFile = "trial.state";

    /// <summary>Имя файла локального ключа устройства.</summary>
    public const string LocalKeyFile = "device.key.pem";

    private const string ActivationEnvelopeFormat = "smetana-activation-state/1";
    private const string TrialEnvelopeFormat = "smetana-trial-state/1";

    private readonly object _sync = new();
    private ECDsa? _localKey;

    /// <summary>Создаёт хранилище в указанной папке (создаётся при необходимости).</summary>
    public LocalStateStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory = System.IO.Path.GetFullPath(directory);
        System.IO.Directory.CreateDirectory(Directory);
    }

    /// <summary>Папка хранилища.</summary>
    public string Directory { get; }

    /// <summary>Открытый ключ устройства в PEM (для диагностики и обмена с поддержкой).</summary>
    public string LocalPublicKeyPem
    {
        get
        {
            lock (_sync)
            {
                return LicenseCrypto.ExportPublicPem(GetOrCreateLocalKey());
            }
        }
    }

    /// <summary>Сохраняет состояние активации (переподписывая локальным ключом).</summary>
    public void SaveActivationState(ActivationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        WriteState(ActivationStateFile, ActivationEnvelopeFormat, state.ToPayloadBytes());
    }

    /// <summary>Читает состояние активации; <c>null</c> — файла нет.</summary>
    /// <exception cref="LicenseStateException">Файл повреждён или подделан.</exception>
    public ActivationState? LoadActivationState()
    {
        byte[]? payload = ReadState(ActivationStateFile, ActivationEnvelopeFormat);
        return payload is null ? null : ActivationState.FromPayloadBytes(payload);
    }

    /// <summary>Удаляет состояние активации.</summary>
    public void DeleteActivationState()
    {
        lock (_sync)
        {
            string path = Path.Combine(Directory, ActivationStateFile);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>Сохраняет состояние пробного периода (переподписывая локальным ключом).</summary>
    public void SaveTrialState(TrialState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        WriteState(TrialStateFile, TrialEnvelopeFormat, state.ToPayloadBytes());
    }

    /// <summary>Читает состояние пробного периода; <c>null</c> — файла нет.</summary>
    /// <exception cref="LicenseStateException">Файл повреждён или подделан.</exception>
    public TrialState? LoadTrialState()
    {
        byte[]? payload = ReadState(TrialStateFile, TrialEnvelopeFormat);
        return payload is null ? null : TrialState.FromPayloadBytes(payload);
    }

    /// <summary>Удаляет состояние пробного периода.</summary>
    public void DeleteTrialState()
    {
        lock (_sync)
        {
            string path = Path.Combine(Directory, TrialStateFile);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            _localKey?.Dispose();
            _localKey = null;
        }
    }

    private void WriteState(string fileName, string envelopeFormat, byte[] payload)
    {
        lock (_sync)
        {
            ECDsa key = GetOrCreateLocalKey();
            byte[] signature = LicenseCrypto.Sign(key, envelopeFormat, payload);
            string envelope = SignedEnvelope.Write(envelopeFormat, payload, signature);
            File.WriteAllText(Path.Combine(Directory, fileName), envelope);
        }
    }

    private byte[]? ReadState(string fileName, string envelopeFormat)
    {
        lock (_sync)
        {
            string path = Path.Combine(Directory, fileName);
            if (!File.Exists(path))
            {
                return null;
            }

            string envelope;
            try
            {
                envelope = File.ReadAllText(path);
            }
            catch (IOException ex)
            {
                throw new LicenseStateException("Не удалось прочитать локальное лицензионное состояние.", ex);
            }

            (byte[] payload, byte[] signature) = SignedEnvelope.Read(envelope, envelopeFormat);

            ECDsa? key = LoadLocalKey();
            if (key is null || !LicenseCrypto.Verify(key, envelopeFormat, payload, signature))
            {
                throw new LicenseStateException(
                    "Локальное лицензионное состояние повреждено или подделано: подпись не прошла проверку.");
            }

            return payload;
        }
    }

    private ECDsa GetOrCreateLocalKey()
    {
        ECDsa? key = _localKey ?? LoadLocalKey();
        if (key is not null)
        {
            _localKey = key;
            return key;
        }

        key = LicenseCrypto.CreateSigningKey();
        string keyPath = Path.Combine(Directory, LocalKeyFile);
        File.WriteAllText(keyPath, LicenseCrypto.ExportPrivatePem(key));
        HardenKeyFile(keyPath);
        _localKey = key;
        return key;
    }

    private ECDsa? LoadLocalKey()
    {
        string keyPath = Path.Combine(Directory, LocalKeyFile);
        if (!File.Exists(keyPath))
        {
            return null;
        }

        try
        {
            return LicenseCrypto.ImportPrivatePem(File.ReadAllText(keyPath));
        }
        catch (LicenseFormatException ex)
        {
            throw new LicenseStateException("Локальный ключ устройства повреждён.", ex);
        }
    }

    private static void HardenKeyFile(string keyPath)
    {
        if (OperatingSystem.IsWindows())
        {
            // На Windows права NTFS/DPAPI настраиваются установщиком продукта; File API .NET без внешних
            // зависимостей не редактирует ACL. Файл с приватным ключом создаётся скрытым.
            File.SetAttributes(keyPath, File.GetAttributes(keyPath) | FileAttributes.Hidden);
            return;
        }

        File.SetUnixFileMode(
            keyPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}

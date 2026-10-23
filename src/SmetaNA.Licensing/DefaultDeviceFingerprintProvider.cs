using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace SmetaNA.Licensing;

/// <summary>
/// Стандартный источник идентификатора устройства для Windows и Linux.
/// Стратегия: стабильные системные идентификаторы (MachineGuid / volume serial на Windows,
/// /etc/machine-id и DMI UUID на Linux) плюс имя компьютера как дополнительная примесь.
/// Слабые места стратегии описаны в отчёте по задаче: переустановка ОС меняет идентификатор,
/// клонированные ВМ дают одинаковые значения, все источники читаемы и подменяемы администратором машины.
/// </summary>
public sealed class DefaultDeviceFingerprintProvider : IDeviceFingerprintProvider
{
    private static readonly nint HkeyLocalMachine = unchecked((nint)(-2147483646)); // 0x80000002 со знаковым расширением

    private const int RrfRtRegSz = 0x00000002;
    private const int RrfSubkeyWow6464Key = 0x00010000;
    private const int ErrorSuccess = 0;

    /// <inheritdoc />
    public DeviceFingerprint GetFingerprint()
    {
        var components = new List<DeviceFingerprintComponent>();

        if (OperatingSystem.IsWindows())
        {
            CollectWindows(components);
        }
        else if (OperatingSystem.IsLinux())
        {
            CollectLinux(components);
        }

        if (components.Count == 0)
        {
            CollectFallback(components);
        }

        return DeviceFingerprint.Create(components);
    }

    [SupportedOSPlatform("windows")]
    private static void CollectWindows(List<DeviceFingerprintComponent> components)
    {
        string? machineGuid = ReadWindowsMachineGuid();
        if (!string.IsNullOrEmpty(machineGuid))
        {
            components.Add(new DeviceFingerprintComponent("machine-guid", machineGuid));
        }

        string? systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
        if (!string.IsNullOrEmpty(systemRoot) && TryGetVolumeSerial(systemRoot, out uint serial))
        {
            components.Add(new DeviceFingerprintComponent(
                "volume-serial",
                serial.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
        }

        components.Add(new DeviceFingerprintComponent("computer-name", Environment.MachineName));
        components.Add(new DeviceFingerprintComponent("os", "windows"));
    }

    private static void CollectLinux(List<DeviceFingerprintComponent> components)
    {
        string? machineId = ReadFirstLine("/etc/machine-id") ?? ReadFirstLine("/var/lib/dbus/machine-id");
        if (!string.IsNullOrEmpty(machineId))
        {
            components.Add(new DeviceFingerprintComponent("machine-id", machineId));
        }

        string? productUuid = ReadFirstLine("/sys/class/dmi/id/product_uuid");
        if (!string.IsNullOrEmpty(productUuid))
        {
            components.Add(new DeviceFingerprintComponent("dmi-product-uuid", productUuid));
        }

        string? hostName = ReadFirstLine("/etc/hostname");
        components.Add(new DeviceFingerprintComponent(
            "computer-name",
            string.IsNullOrEmpty(hostName) ? Environment.MachineName : hostName));
        components.Add(new DeviceFingerprintComponent("os", "linux"));
    }

    private static void CollectFallback(List<DeviceFingerprintComponent> components)
    {
        // Крайний случай (контейнеры, необычные ОС): слабый, но непустой идентификатор.
        components.Add(new DeviceFingerprintComponent("computer-name", Environment.MachineName));
        components.Add(new DeviceFingerprintComponent("user-name", Environment.UserName));
        components.Add(new DeviceFingerprintComponent("os", "unknown"));
    }

    private static string? ReadFirstLine(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            foreach (string line in File.ReadLines(path))
            {
                string value = line.Trim();
                return value.Length == 0 ? null : value;
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadWindowsMachineGuid()
    {
        int size = 0;
        int code = RegGetValueW(
            HkeyLocalMachine,
            @"SOFTWARE\Microsoft\Cryptography",
            "MachineGuid",
            RrfRtRegSz | RrfSubkeyWow6464Key,
            out _,
            null,
            ref size);

        if (code != ErrorSuccess || size <= 0)
        {
            return null;
        }

        var buffer = new byte[size];
        code = RegGetValueW(
            HkeyLocalMachine,
            @"SOFTWARE\Microsoft\Cryptography",
            "MachineGuid",
            RrfRtRegSz | RrfSubkeyWow6464Key,
            out _,
            buffer,
            ref size);

        if (code != ErrorSuccess)
        {
            return null;
        }

        string value = Encoding.Unicode.GetString(buffer).TrimEnd('\0').Trim();
        return value.Length == 0 ? null : value;
    }

    [SupportedOSPlatform("windows")]
    private static bool TryGetVolumeSerial(string rootPath, out uint serial)
    {
        serial = 0;
        return GetVolumeInformationW(rootPath, null, 0, out serial, out _, out _, null, 0);
    }

    [DllImport("advapi32.dll", EntryPoint = "RegGetValueW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegGetValueW(
        nint hkey,
        string? subKey,
        string? value,
        int flags,
        out int type,
        byte[]? data,
        ref int dataSize);

    [DllImport("kernel32.dll", EntryPoint = "GetVolumeInformationW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetVolumeInformationW(
        string rootPath,
        StringBuilder? volumeName,
        int volumeNameSize,
        out uint volumeSerial,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder? fileSystemName,
        int fileSystemNameSize);
}

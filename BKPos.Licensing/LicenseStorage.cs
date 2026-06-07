using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BKPos.Licensing.Security;
using Microsoft.Win32;

namespace BKPos.Licensing;

public sealed class LicenseStorage
{
    private readonly HardwareIdProvider _hardwareIdProvider;

    private const string RegKeyPath = @"Software\{6F7B8A3D-9E21-4C57-AF0B-2C9E6D3481A7}";
    private static readonly string RegValueName = Hashing.Sha256Hex("state-value-primary")[..16];

    public LicenseStorage(HardwareIdProvider hardwareIdProvider)
    {
        _hardwareIdProvider = hardwareIdProvider;
    }

    public string? Load()
    {
        return TryReadFile(GetPrimaryPath())
               ?? TryReadFile(GetBackupPath())
               ?? TryReadRegistry();
    }

    public void Save(string protectedValue)
    {
        TryWriteFile(GetPrimaryPath(), protectedValue);
        TryWriteFile(GetBackupPath(), protectedValue);
        TryWriteRegistry(protectedValue);
    }

    public void Clear()
    {
        TryDeleteFile(GetPrimaryPath());
        TryDeleteFile(GetBackupPath());
        TryDeleteRegistry();
    }

    internal string GetPrimaryPath()
    {
        var seed = GetPathSeed();
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder1 = Hashing.Sha256Hex("store-a:" + seed.MachineGuid)[..12];
        var folder2 = Hashing.Sha256Hex("store-b:" + seed.HardwareId)[..12];
        var file = Hashing.Sha256Hex("store-c:" + seed.MachineGuid + ":" + seed.HardwareId)[..16] + ".dat";
        return Path.Combine(root, folder1, folder2, file);
    }

    internal string GetBackupPath()
    {
        var seed = GetPathSeed();
        var root = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var folder = Hashing.Sha256Hex("store-d:" + seed.MachineGuid)[..12];
        var file = Hashing.Sha256Hex("store-e:" + seed.HardwareId)[..16] + ".bin";
        return Path.Combine(root, folder, file);
    }

    private (string MachineGuid, string HardwareId) GetPathSeed()
    {
        var info = _hardwareIdProvider.GetHardwareInfo();
        return (string.IsNullOrWhiteSpace(info.MachineGuid) ? "NA" : info.MachineGuid, info.HardwareId);
    }

    private static byte[] Protect(StoreEnvelope envelope)
    {
        var json = JsonSerializer.Serialize(envelope);
        return ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.LocalMachine);
    }

    private static StoreEnvelope? Unprotect(byte[] bytes)
    {
        try
        {
            var plain = ProtectedData.Unprotect(bytes, null, DataProtectionScope.LocalMachine);
            return JsonSerializer.Deserialize<StoreEnvelope>(Encoding.UTF8.GetString(plain));
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return Unprotect(File.ReadAllBytes(path))?.Value;
        }
        catch
        {
            return null;
        }
    }

    private static void TryWriteFile(string path, string value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, Protect(new StoreEnvelope(value, DateTimeOffset.Now)));
        }
        catch
        {
            // Storage backup is best-effort; license validation remains cryptographic.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    private static string? TryReadRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath);
            return key?.GetValue(RegValueName) is byte[] encrypted
                ? Unprotect(encrypted)?.Value
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static void TryWriteRegistry(string value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
            key.SetValue(RegValueName, Protect(new StoreEnvelope(value, DateTimeOffset.Now)), RegistryValueKind.Binary);
        }
        catch
        {
            // Ignore backup failures.
        }
    }

    private static void TryDeleteRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, writable: true);
            key?.DeleteValue(RegValueName, throwOnMissingValue: false);
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    private sealed record StoreEnvelope(string Value, DateTimeOffset SavedAt);
}

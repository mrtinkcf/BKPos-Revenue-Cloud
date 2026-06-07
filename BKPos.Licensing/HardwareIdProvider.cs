using System.Diagnostics;
using System.Text.Json;
using BKPos.Licensing.Models;
using BKPos.Licensing.Security;
using Microsoft.Win32;

namespace BKPos.Licensing;

public sealed class HardwareIdProvider
{
    private readonly Lazy<HardwareInfo> _hardwareInfo;

    public HardwareIdProvider()
    {
        _hardwareInfo = new Lazy<HardwareInfo>(BuildHardwareInfo, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public HardwareInfo GetHardwareInfo()
        => _hardwareInfo.Value;

    public string GetHardwareId() => GetHardwareInfo().HardwareId;

    private static HardwareInfo BuildHardwareInfo()
    {
        var cim = ReadCimSnapshot();
        var info = new HardwareInfo
        {
            CpuId = cim.CpuId,
            MainboardSerial = cim.MainboardSerial,
            BiosSerial = cim.BiosSerial,
            DiskSerial = cim.DiskSerial,
            MachineGuid = ReadMachineGuid()
        };

        info.RawFingerprint = string.Join('|',
            Normalize(info.CpuId),
            Normalize(info.MainboardSerial),
            Normalize(info.BiosSerial),
            Normalize(info.DiskSerial),
            Normalize(info.MachineGuid));

        info.HardwareId = HardwareIdFormatter.CreateFromRawId(info.RawFingerprint);
        return info;
    }

    private static CimSnapshot ReadCimSnapshot()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = ResolvePowerShellPath(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command " + QuotePowerShellArgument(BuildCimScript())
            });

            if (process is null)
            {
                return new CimSnapshot();
            }

            if (!process.WaitForExit(12000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore cleanup failures.
                }

                return new CimSnapshot();
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return new CimSnapshot();
            }

            var snapshot = JsonSerializer.Deserialize<CimSnapshot>(output) ?? new CimSnapshot();
            snapshot.CpuId = CleanHardwareValue(snapshot.CpuId);
            snapshot.MainboardSerial = CleanHardwareValue(snapshot.MainboardSerial);
            snapshot.BiosSerial = CleanHardwareValue(snapshot.BiosSerial);
            snapshot.DiskSerial = CleanHardwareValue(snapshot.DiskSerial);
            return snapshot;
        }
        catch
        {
            return new CimSnapshot();
        }
    }

    private static string ReadMachineGuid()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return Convert.ToString(key?.GetValue("MachineGuid"))?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsPlaceholder(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized is "TO BE FILLED BY O.E.M." or "DEFAULT STRING" or "SYSTEM SERIAL NUMBER" or "NONE" or "UNKNOWN";
    }

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? "NA" : value.Trim().ToUpperInvariant();

    private static string CleanHardwareValue(string? value)
    {
        var cleaned = value?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(cleaned) || IsPlaceholder(cleaned) ? string.Empty : cleaned;
    }

    private static string ResolvePowerShellPath()
    {
        var systemPowerShell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");

        return File.Exists(systemPowerShell) ? systemPowerShell : "powershell.exe";
    }

    private static string QuotePowerShellArgument(string script)
        => "\"" + script.Replace("\"", "\\\"") + "\"";

    private static string BuildCimScript()
        => """
           function FirstValue($class, $prop) {
             try {
               $value = Get-CimInstance -ClassName $class -ErrorAction Stop | Select-Object -First 1 -ExpandProperty $prop -ErrorAction SilentlyContinue
               if ($null -eq $value) { '' } else { [string]$value }
             } catch { '' }
           }
           $disk = ''
           try {
             $diskObj = Get-CimInstance -ClassName Win32_DiskDrive -ErrorAction Stop |
               Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.SerialNumber) } |
               Sort-Object @{Expression={ if ([string]$_.InterfaceType -eq 'USB') { 1 } else { 0 } }}, Index |
               Select-Object -First 1
             if ($null -ne $diskObj) { $disk = [string]$diskObj.SerialNumber }
           } catch {}
           [pscustomobject]@{
             CpuId = FirstValue 'Win32_Processor' 'ProcessorId'
             MainboardSerial = FirstValue 'Win32_BaseBoard' 'SerialNumber'
             BiosSerial = FirstValue 'Win32_BIOS' 'SerialNumber'
             DiskSerial = $disk
           } | ConvertTo-Json -Compress
           """;

    private sealed class CimSnapshot
    {
        public string CpuId { get; set; } = string.Empty;
        public string MainboardSerial { get; set; } = string.Empty;
        public string BiosSerial { get; set; } = string.Empty;
        public string DiskSerial { get; set; } = string.Empty;
    }
}

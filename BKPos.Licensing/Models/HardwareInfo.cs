namespace BKPos.Licensing.Models;

public sealed class HardwareInfo
{
    public string CpuId { get; set; } = string.Empty;
    public string MainboardSerial { get; set; } = string.Empty;
    public string BiosSerial { get; set; } = string.Empty;
    public string DiskSerial { get; set; } = string.Empty;
    public string MachineGuid { get; set; } = string.Empty;
    public string RawFingerprint { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
}

namespace BKPos.Core.Models;

public sealed class PrinterProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D").ToUpperInvariant();

    public string Name { get; set; } = string.Empty;

    public PrinterConnectionType ConnectionType { get; set; }

    public string PrinterName { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public int Port { get; set; } = 9100;

    public string? SerialPort { get; set; }

    public int BaudRate { get; set; } = 9600;

    public int PaperWidthMm { get; set; } = 80;

    public string? DefaultBillTemplateId { get; set; }

    public string? DefaultCupTemplateId { get; set; }

    public string? DefaultKitchenTemplateId { get; set; }

    public bool IsDefault { get; set; }
}

public enum PrinterConnectionType
{
    WindowsSpooler,
    TcpIp,
    SerialPort
}

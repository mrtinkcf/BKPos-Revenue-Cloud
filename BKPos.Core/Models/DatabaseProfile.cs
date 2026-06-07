namespace BKPos.Core.Models;

public sealed class DatabaseProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D").ToUpper();

    public string Name { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public DateTime LastUsed { get; set; } = DateTime.Now;
}

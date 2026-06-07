namespace BKPos.Core.Models;

public sealed class AppUser
{
    public int Id { get; set; }

    public string ExternalId { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }

    public string GroupExternalId { get; set; } = string.Empty;

    public HashSet<string> Permissions { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsBuiltInAdmin =>
        string.Equals(Username.Trim(), "admin", StringComparison.OrdinalIgnoreCase);

    public bool HasPermission(string permissionKey) =>
        IsAdmin || Permissions.Contains(permissionKey);
}

namespace Ferre.Services.Auth;

public sealed class RememberedLoginAccount
{
    public string Email { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTime LastUsedAtUtc { get; set; } = DateTime.UtcNow;
}

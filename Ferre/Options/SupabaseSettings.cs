namespace Ferre.Options;

public sealed class SupabaseSettings
{
    public string Url { get; init; } = string.Empty;
    public string AnonKey { get; init; } = string.Empty;
    public string ServiceRoleKey { get; init; } = string.Empty;
    public string SignUpRedirectUrl { get; init; } = string.Empty;
    public string PasswordResetRedirectUrl { get; init; } = string.Empty;
}

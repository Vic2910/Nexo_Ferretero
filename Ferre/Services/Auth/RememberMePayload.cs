namespace Ferre.Services.Auth;

public sealed class RememberMePayload
{
    public string UserRole { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}

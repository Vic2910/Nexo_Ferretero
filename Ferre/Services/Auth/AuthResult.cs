namespace Ferre.Services.Auth;

public sealed record AuthResult(
    bool Succeeded,
    string? ErrorMessage = null,
    string? Role = null,
    string? UserId = null,
    string? FirstName = null,
    string? LastName = null)
{
    public static AuthResult Success(
        string? role = null,
        string? userId = null,
        string? firstName = null,
        string? lastName = null)
        => new(true, null, role, userId, firstName, lastName);

    public static AuthResult Failure(string message)
        => new(false, message);
}
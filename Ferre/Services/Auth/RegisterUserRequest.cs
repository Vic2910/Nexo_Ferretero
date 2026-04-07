namespace Ferre.Services.Auth;

public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Phone);

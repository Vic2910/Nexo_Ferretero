using Ferre.Models.Auth;
using Ferre.Services.Common;

namespace Ferre.Services.Auth;

public interface ISupabaseAuthService
{
    Task<AuthResult> SignInAsync(string email, string password);

    Task<AuthResult> SignUpAsync(RegisterUserRequest request);

    Task<AuthResult> SendPasswordResetAsync(string email);

    Task<AuthResult> ResetPasswordAsync(string accessToken, string? refreshToken, string newPassword);

    Task<AuthResult> ResetPasswordWithRecoveryCodeAsync(string email, string recoveryCode, string newPassword);

    Task<AuthResult> UpdateUserRoleAsync(string userId, string role);

    Task<IReadOnlyList<AdminUserViewModel>> GetUsersByRolesAsync(IReadOnlyCollection<string> roles);

    Task<ClientProfileViewModel?> GetClientProfileByEmailAsync(string email);

    Task<OperationResult> UpdateClientProfileAsync(ClientProfileViewModel profile);

    Task<OperationResult> UpdateUserAsync(AdminUserUpdateModel request);

    Task<OperationResult> DeleteUserAsync(string userId);
}

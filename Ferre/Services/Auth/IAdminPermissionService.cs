namespace Ferre.Services.Auth;

public interface IAdminPermissionService
{
    bool IsSuperAdmin(string? email);

    bool HasAccess(string? email, string area);

    IReadOnlyDictionary<string, bool> GetPermissions(string? email);

    IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>> GetPermissionsForAdmins(IEnumerable<string> emails);

    void SavePermissions(string? email, IReadOnlyDictionary<string, bool> permissions);
}

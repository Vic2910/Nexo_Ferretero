using System.Text.Json;
using Ferre.Models.Auth;

namespace Ferre.Services.Auth;

public sealed class AdminPermissionService : IAdminPermissionService
{
    private const string StoreFileName = "admin-area-permissions.json";
    private const string SuperAdminEmail = "gabrielaabigaildiaz8@gmail.com";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly object SyncRoot = new();

    private readonly string _storeFilePath;

    private sealed class PermissionStore
    {
        public Dictionary<string, Dictionary<string, bool>> ItemsByEmail { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    public AdminPermissionService(IWebHostEnvironment webHostEnvironment)
    {
        _storeFilePath = Path.Combine(webHostEnvironment.ContentRootPath, "App_Data", StoreFileName);
    }

    public bool IsSuperAdmin(string? email)
    {
        return string.Equals(NormalizeEmail(email), SuperAdminEmail, StringComparison.OrdinalIgnoreCase);
    }

    public bool HasAccess(string? email, string area)
    {
        if (string.IsNullOrWhiteSpace(area))
        {
            return false;
        }

        if (IsSuperAdmin(email))
        {
            return true;
        }

        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return false;
        }

        lock (SyncRoot)
        {
            var store = ReadStoreUnsafe();
            if (!store.ItemsByEmail.TryGetValue(normalizedEmail, out var permissions) || permissions is null)
            {
                return true;
            }

            return !permissions.TryGetValue(area, out var isEnabled) || isEnabled;
        }
    }

    public IReadOnlyDictionary<string, bool> GetPermissions(string? email)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return CreateDefaultPermissions();
        }

        if (IsSuperAdmin(normalizedEmail))
        {
            return CreateAllEnabledPermissions();
        }

        lock (SyncRoot)
        {
            var store = ReadStoreUnsafe();
            if (!store.ItemsByEmail.TryGetValue(normalizedEmail, out var permissions) || permissions is null)
            {
                return CreateDefaultPermissions();
            }

            return NormalizePermissions(permissions);
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>> GetPermissionsForAdmins(IEnumerable<string> emails)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, bool>>(StringComparer.OrdinalIgnoreCase);
        foreach (var email in emails.Where(e => !string.IsNullOrWhiteSpace(e)).Select(NormalizeEmail).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            result[email] = GetPermissions(email);
        }

        return result;
    }

    public void SavePermissions(string? email, IReadOnlyDictionary<string, bool> permissions)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || IsSuperAdmin(normalizedEmail))
        {
            return;
        }

        var normalizedPermissions = NormalizePermissions(permissions);

        lock (SyncRoot)
        {
            var store = ReadStoreUnsafe();
            store.ItemsByEmail[normalizedEmail] = new Dictionary<string, bool>(normalizedPermissions, StringComparer.OrdinalIgnoreCase);
            WriteStoreUnsafe(store);
        }
    }

    private static Dictionary<string, bool> CreateDefaultPermissions()
    {
        return AdminAreas.All.ToDictionary(area => area, _ => false, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, bool> CreateAllEnabledPermissions()
    {
        return AdminAreas.All.ToDictionary(area => area, _ => true, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, bool> NormalizePermissions(IReadOnlyDictionary<string, bool> source)
    {
        var normalized = CreateAllEnabledPermissions();
        foreach (var area in AdminAreas.All)
        {
            if (source.TryGetValue(area, out var isEnabled))
            {
                normalized[area] = isEnabled;
            }
        }

        return normalized;
    }

    private PermissionStore ReadStoreUnsafe()
    {
        try
        {
            if (!File.Exists(_storeFilePath))
            {
                return new PermissionStore();
            }

            var raw = File.ReadAllText(_storeFilePath);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new PermissionStore();
            }

            return JsonSerializer.Deserialize<PermissionStore>(raw, JsonOptions) ?? new PermissionStore();
        }
        catch
        {
            return new PermissionStore();
        }
    }

    private void WriteStoreUnsafe(PermissionStore store)
    {
        var directory = Path.GetDirectoryName(_storeFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_storeFilePath, JsonSerializer.Serialize(store, JsonOptions));
    }

    private static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}

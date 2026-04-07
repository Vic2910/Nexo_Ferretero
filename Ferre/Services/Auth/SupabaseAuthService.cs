using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ferre.Models.Auth;
using Ferre.Options;
using Ferre.Services.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupabaseClient = Supabase.Client;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;

namespace Ferre.Services.Auth;

public sealed class SupabaseAuthService : ISupabaseAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SupabaseClient _client;
    private readonly SupabaseSettings _settings;
    private readonly ILogger<SupabaseAuthService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Lazy<Task> _initializer;

    public SupabaseAuthService(
        SupabaseClient client,
        IOptions<SupabaseSettings> settings,
        ILogger<SupabaseAuthService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _initializer = new Lazy<Task>(() => _client.InitializeAsync());
    }

    public async Task<AuthResult> ResetPasswordWithRecoveryCodeAsync(string email, string recoveryCode, string newPassword)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();

            var verifyRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_settings.Url.TrimEnd('/')}/auth/v1/verify");

            verifyRequest.Headers.TryAddWithoutValidation("apikey", _settings.AnonKey);
            verifyRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_settings.AnonKey}");

            var verifyPayload = new
            {
                email,
                token = recoveryCode,
                type = "recovery"
            };

            verifyRequest.Content = new StringContent(
                JsonSerializer.Serialize(verifyPayload),
                Encoding.UTF8,
                "application/json");

            var verifyResponse = await client.SendAsync(verifyRequest).ConfigureAwait(false);
            if (!verifyResponse.IsSuccessStatusCode)
            {
                return AuthResult.Failure("El código es inválido o expiró.");
            }

            await using var verifyStream = await verifyResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var verifyResult = await JsonSerializer.DeserializeAsync<VerifyRecoveryResponse>(verifyStream, JsonOptions).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(verifyResult?.AccessToken))
            {
                return AuthResult.Failure("No fue posible validar el código de recuperación.");
            }

            var updateRequest = new HttpRequestMessage(
                HttpMethod.Put,
                $"{_settings.Url.TrimEnd('/')}/auth/v1/user");

            updateRequest.Headers.TryAddWithoutValidation("apikey", _settings.AnonKey);
            updateRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {verifyResult.AccessToken}");
            updateRequest.Content = new StringContent(
                JsonSerializer.Serialize(new { password = newPassword }),
                Encoding.UTF8,
                "application/json");

            var updateResponse = await client.SendAsync(updateRequest).ConfigureAwait(false);
            if (!updateResponse.IsSuccessStatusCode)
            {
                return AuthResult.Failure("No fue posible actualizar la contraseña.");
            }

            return AuthResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al restablecer contraseña con código de recuperación.");
            return AuthResult.Failure("No fue posible actualizar la contraseña.");
        }
    }

    public async Task<IReadOnlyList<AdminUserViewModel>> GetUsersByRolesAsync(IReadOnlyCollection<string> roles)
    {
        if (string.IsNullOrWhiteSpace(_settings.ServiceRoleKey))
        {
            return Array.Empty<AdminUserViewModel>();
        }

        try
        {
            var normalizedRoles = roles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim().ToLowerInvariant())
                .ToHashSet();

            if (normalizedRoles.Count == 0)
            {
                return Array.Empty<AdminUserViewModel>();
            }

            var users = await GetAllUsersAsync().ConfigureAwait(false);
            return users
                .Where(u => normalizedRoles.Contains(u.Role.Trim().ToLowerInvariant()))
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo recuperar la lista de usuarios de Supabase.");
            return Array.Empty<AdminUserViewModel>();
        }
    }

    public async Task<ClientProfileViewModel?> GetClientProfileByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        try
        {
            var users = await GetAllUsersAsync().ConfigureAwait(false);
            var user = users.FirstOrDefault(x => string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                return null;
            }

            return new ClientProfileViewModel
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                Address = user.Address,
                ProfileImageUrl = user.ProfileImageUrl,
                Role = string.IsNullOrWhiteSpace(user.Role) ? "cliente" : user.Role
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo obtener perfil de cliente para {Email}", email);
            return null;
        }
    }

    public async Task<OperationResult> UpdateClientProfileAsync(ClientProfileViewModel profile)
    {
        if (string.IsNullOrWhiteSpace(_settings.ServiceRoleKey))
        {
            return OperationResult.Failure("Falta configurar la Service Role Key de Supabase.");
        }

        if (string.IsNullOrWhiteSpace(profile.UserId) || string.IsNullOrWhiteSpace(profile.Email))
        {
            return OperationResult.Failure("No se pudo identificar el perfil de usuario.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var httpRequest = new HttpRequestMessage(
                HttpMethod.Put,
                $"{_settings.Url.TrimEnd('/')}/auth/v1/admin/users/{profile.UserId}");

            httpRequest.Headers.TryAddWithoutValidation("apikey", _settings.ServiceRoleKey);
            httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_settings.ServiceRoleKey}");

            var payload = new Dictionary<string, object?>
            {
                ["email"] = profile.Email,
                ["phone"] = profile.Phone,
                ["user_metadata"] = new Dictionary<string, object?>
                {
                    ["first_name"] = profile.FirstName,
                    ["last_name"] = profile.LastName,
                    ["phone"] = profile.Phone,
                    ["address"] = profile.Address,
                    ["avatar_url"] = profile.ProfileImageUrl,
                    ["role"] = string.IsNullOrWhiteSpace(profile.Role) ? "cliente" : profile.Role
                }
            };

            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await client.SendAsync(httpRequest).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("No se pudo actualizar el perfil cliente {Email}. Status: {Status}", profile.Email, response.StatusCode);
                return OperationResult.Failure("No fue posible actualizar tu perfil.");
            }

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo actualizar perfil cliente {Email}", profile.Email);
            return OperationResult.Failure("No fue posible actualizar tu perfil.");
        }
    }

    public async Task<OperationResult> UpdateUserAsync(AdminUserUpdateModel request)
    {
        if (string.IsNullOrWhiteSpace(_settings.ServiceRoleKey))
        {
            return OperationResult.Failure("Falta configurar la Service Role Key de Supabase.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var httpRequest = new HttpRequestMessage(
                HttpMethod.Put,
                $"{_settings.Url.TrimEnd('/')}/auth/v1/admin/users/{request.Id}");

            httpRequest.Headers.TryAddWithoutValidation("apikey", _settings.ServiceRoleKey);
            httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_settings.ServiceRoleKey}");

            var payload = new Dictionary<string, object?>
            {
                ["email"] = request.Email,
                ["user_metadata"] = new Dictionary<string, object?>
                {
                    ["first_name"] = request.FirstName,
                    ["last_name"] = request.LastName,
                    ["phone"] = request.Phone,
                    ["role"] = request.Role
                }
            };

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                payload["password"] = request.Password;
            }

            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await client.SendAsync(httpRequest).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("No se pudo actualizar el usuario {UserId}. Status: {Status}", request.Id, response.StatusCode);
                return OperationResult.Failure("No fue posible actualizar el usuario.");
            }

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo actualizar el usuario {UserId} en Supabase.", request.Id);
            return OperationResult.Failure("No fue posible actualizar el usuario.");
        }
    }

    public async Task<OperationResult> DeleteUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(_settings.ServiceRoleKey))
        {
            return OperationResult.Failure("Falta configurar la Service Role Key de Supabase.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var httpRequest = new HttpRequestMessage(
                HttpMethod.Delete,
                $"{_settings.Url.TrimEnd('/')}/auth/v1/admin/users/{userId}");

            httpRequest.Headers.TryAddWithoutValidation("apikey", _settings.ServiceRoleKey);
            httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_settings.ServiceRoleKey}");

            var response = await client.SendAsync(httpRequest).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("No se pudo eliminar el usuario {UserId}. Status: {Status}", userId, response.StatusCode);
                return OperationResult.Failure("No fue posible eliminar el usuario.");
            }

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar el usuario {UserId} en Supabase.", userId);
            return OperationResult.Failure("No fue posible eliminar el usuario.");
        }
    }

    public async Task<AuthResult> SignInAsync(string email, string password)
    {
        await _initializer.Value.ConfigureAwait(false);

        try
        {
            var session = await _client.Auth.SignIn(email, password).ConfigureAwait(false);
            if (session?.User is null)
            {
                return AuthResult.Failure("Credenciales inválidas.");
            }

            var role = ExtractRole(session.User);
            var firstName = ExtractMetadataValue(session.User, "first_name");
            var lastName = ExtractMetadataValue(session.User, "last_name");
            return AuthResult.Success(role, session.User.Id, firstName, lastName);
        }
        catch (GotrueException)
        {
            return AuthResult.Failure("Credenciales inválidas.");
        }
    }

    public async Task<AuthResult> UpdateUserRoleAsync(string userId, string role)
    {
        if (string.IsNullOrWhiteSpace(_settings.ServiceRoleKey))
        {
            return AuthResult.Failure("Falta configurar la Service Role Key de Supabase.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(
                HttpMethod.Put,
                $"{_settings.Url.TrimEnd('/')}/auth/v1/admin/users/{userId}");

            request.Headers.TryAddWithoutValidation("apikey", _settings.ServiceRoleKey);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_settings.ServiceRoleKey}");

            var payload = new { user_metadata = new { role } };
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await client.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("No se pudo actualizar el rol. Status: {Status}", response.StatusCode);
                return AuthResult.Failure("No fue posible actualizar el rol del usuario.");
            }

            return AuthResult.Success(role);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo actualizar el rol del usuario en Supabase.");
            return AuthResult.Failure("No fue posible actualizar el rol del usuario.");
        }
    }

    public async Task<AuthResult> SignUpAsync(RegisterUserRequest request)
    {
        await _initializer.Value.ConfigureAwait(false);

        try
        {
            var metadata = new Dictionary<string, object>
            {
                ["first_name"] = request.FirstName,
                ["last_name"] = request.LastName,
                ["phone"] = request.Phone,
                ["role"] = "cliente",
                ["app_name"] = "NexoFerretero",
                ["support_email"] = "soporte@nexoferretero.com",
                ["welcome_message"] = "Gracias por registrarte en NexoFerretero. Tu cuenta ha sido creada correctamente. Si necesitas ayuda, contáctanos en soporte@nexoferretero.com."
            };

            var options = new SignUpOptions
            {
                Data = metadata,
                RedirectTo = string.IsNullOrWhiteSpace(_settings.SignUpRedirectUrl)
                    ? null
                    : _settings.SignUpRedirectUrl
            };

            var response = await _client.Auth.SignUp(request.Email, request.Password, options).ConfigureAwait(false);
            if (response?.User is null || response.User.Identities is null || response.User.Identities.Count == 0)
            {
                _logger.LogWarning("Supabase respondió sin usuario en el registro.");
                return AuthResult.Failure("No se pudo completar el registro.");
            }

            return AuthResult.Success("cliente", response.User.Id);
        }
        catch (GotrueException ex) when (ex.Message.Contains("already registered", StringComparison.OrdinalIgnoreCase))
        {
            return AuthResult.Failure("No se pudo completar el registro. Verifica tus datos o inicia sesión.");
        }
        catch (GotrueException ex) when (ex.Message.Contains("signup is disabled", StringComparison.OrdinalIgnoreCase))
        {
            return AuthResult.Failure("El registro está deshabilitado temporalmente.");
        }
        catch (GotrueException ex) when (ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            return AuthResult.Failure("Se alcanzó el límite de intentos. Intenta más tarde.");
        }
        catch (GotrueException ex)
        {
            _logger.LogWarning(ex, "No se pudo completar el registro en Supabase.");
            return AuthResult.Failure("No se pudo completar el registro.");
        }
    }

    public async Task<AuthResult> SendPasswordResetAsync(string email)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_settings.Url.TrimEnd('/')}/auth/v1/otp");

            request.Headers.TryAddWithoutValidation("apikey", _settings.AnonKey);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_settings.AnonKey}");

            var payload = new Dictionary<string, object?>
            {
                ["email"] = email,
                ["create_user"] = false,
                ["type"] = "recovery"
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await client.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("No fue posible enviar recuperación. Status: {Status}", response.StatusCode);
                return AuthResult.Failure("No fue posible enviar el correo de recuperación.");
            }

            return AuthResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No fue posible enviar recuperación por OTP.");
            return AuthResult.Failure("No fue posible enviar el correo de recuperación.");
        }
    }

    public async Task<AuthResult> ResetPasswordAsync(string accessToken, string? refreshToken, string newPassword)
    {
        await _initializer.Value.ConfigureAwait(false);

        try
        {
            _client.Auth.SetAuth(accessToken);

            var attributes = new UserAttributes { Password = newPassword };
            await _client.Auth.Update(attributes).ConfigureAwait(false);

            return AuthResult.Success();
        }
        catch (GotrueException)
        {
            return AuthResult.Failure("No fue posible actualizar la contraseña.");
        }
    }

    private static string ExtractRole(User user)
    {
        return ExtractMetadataValue(user, "role") ?? string.Empty;
    }

    private static string? ExtractMetadataValue(User user, string key)
    {
        if (user.UserMetadata is not null
            && user.UserMetadata.TryGetValue(key, out var value)
            && value is not null)
        {
            return value.ToString();
        }

        return null;
    }

    private async Task<List<AdminUserViewModel>> GetAllUsersAsync()
    {
        const int perPage = 200;
        var currentPage = 1;
        var users = new List<AdminUserViewModel>();
        var client = _httpClientFactory.CreateClient();

        while (true)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_settings.Url.TrimEnd('/')}/auth/v1/admin/users?page={currentPage}&per_page={perPage}");

            request.Headers.TryAddWithoutValidation("apikey", _settings.ServiceRoleKey);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_settings.ServiceRoleKey}");

            var response = await client.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                break;
            }

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var payload = await JsonSerializer.DeserializeAsync<SupabaseAdminUsersResponse>(stream, JsonOptions).ConfigureAwait(false);
            if (payload?.Users is null || payload.Users.Count == 0)
            {
                break;
            }

            users.AddRange(payload.Users.Select(MapUser));

            if (payload.Users.Count < perPage)
            {
                break;
            }

            currentPage++;
        }

        return users;
    }

    private static AdminUserViewModel MapUser(SupabaseAdminUser user)
    {
        var firstName = ExtractMetadataValue(user.UserMetadata, "first_name")
            ?? ExtractMetadataValue(user.AppMetadata, "first_name");
        var lastName = ExtractMetadataValue(user.UserMetadata, "last_name")
            ?? ExtractMetadataValue(user.AppMetadata, "last_name");
        var role = ExtractMetadataValue(user.UserMetadata, "role")
            ?? ExtractMetadataValue(user.AppMetadata, "role")
            ?? "cliente";
        var phoneMetadata = ExtractMetadataValue(user.UserMetadata, "phone")
            ?? ExtractMetadataValue(user.AppMetadata, "phone");
        var address = ExtractMetadataValue(user.UserMetadata, "address")
            ?? ExtractMetadataValue(user.AppMetadata, "address");
        var profileImageUrl = ExtractMetadataValue(user.UserMetadata, "avatar_url")
            ?? ExtractMetadataValue(user.AppMetadata, "avatar_url");

        return new AdminUserViewModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            Phone = string.IsNullOrWhiteSpace(user.Phone) ? phoneMetadata : user.Phone,
            FirstName = firstName ?? string.Empty,
            LastName = lastName ?? string.Empty,
            Address = address,
            ProfileImageUrl = profileImageUrl,
            Role = role
        };
    }

    private static string? ExtractMetadataValue(Dictionary<string, JsonElement>? metadata, string key)
    {
        if (metadata is null || !metadata.TryGetValue(key, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return value.ToString();
    }

    private sealed class SupabaseAdminUsersResponse
    {
        [JsonPropertyName("users")]
        public List<SupabaseAdminUser> Users { get; set; } = new();
    }

    private sealed class SupabaseAdminUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("user_metadata")]
        public Dictionary<string, JsonElement>? UserMetadata { get; set; }

        [JsonPropertyName("app_metadata")]
        public Dictionary<string, JsonElement>? AppMetadata { get; set; }
    }

    private sealed class VerifyRecoveryResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }
}

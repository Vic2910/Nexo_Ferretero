using System.Text.Json;
using Ferre.Services.Auth;
using Microsoft.AspNetCore.DataProtection;

namespace Ferre.Middleware;

public sealed class RememberMeSessionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDataProtector _protector;

    public RememberMeSessionMiddleware(RequestDelegate next, IDataProtectionProvider dataProtectionProvider)
    {
        _next = next;
        _protector = dataProtectionProvider.CreateProtector(RememberMeConstants.ProtectorPurpose);
    }

    public async Task Invoke(HttpContext context)
    {
        var isAuthenticated = context.Session.GetString("IsAuthenticated") == "true";
        if (!isAuthenticated && context.Request.Cookies.TryGetValue(RememberMeConstants.CookieName, out var protectedValue))
        {
            try
            {
                var json = _protector.Unprotect(protectedValue);
                var payload = JsonSerializer.Deserialize<RememberMePayload>(json);

                if (payload is not null
                    && !string.IsNullOrWhiteSpace(payload.UserRole)
                    && !string.IsNullOrWhiteSpace(payload.UserEmail))
                {
                    context.Session.SetString("IsAuthenticated", "true");
                    context.Session.SetString("UserRole", payload.UserRole);
                    context.Session.SetString("UserEmail", payload.UserEmail);
                    context.Session.SetString("UserName", string.IsNullOrWhiteSpace(payload.UserName) ? payload.UserEmail : payload.UserName);
                }
            }
            catch
            {
                context.Response.Cookies.Delete(RememberMeConstants.CookieName);
            }
        }

        await _next(context);
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ferre.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequireSessionAttribute : Attribute, IAsyncActionFilter
{
    private readonly string[] _roles;

    public RequireSessionAttribute(params string[] roles)
    {
        _roles = roles;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var isAuthenticated = httpContext.Session.GetString("IsAuthenticated") == "true";

        if (!isAuthenticated)
        {
            if (IsAjaxRequest(httpContext.Request))
            {
                var returnUrl = $"{httpContext.Request.Path}{httpContext.Request.QueryString}";
                var loginUrl = $"/Home/Login?returnUrl={Uri.EscapeDataString(returnUrl)}";

                context.Result = new UnauthorizedObjectResult(new
                {
                    succeeded = false,
                    requiresLogin = true,
                    errorMessage = "Debes iniciar sesión para continuar.",
                    loginUrl
                });
                return;
            }

            context.Result = new RedirectToActionResult("Login", "Home", null);
            return;
        }

        if (_roles.Length > 0)
        {
            var role = httpContext.Session.GetString("UserRole") ?? string.Empty;
            if (!_roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                context.Result = ResolveRoleRedirect(role);
                return;
            }
        }

        await next();
    }

    private static RedirectToActionResult ResolveRoleRedirect(string role)
    {
        return role.ToLowerInvariant() switch
        {
            "administrador" => new RedirectToActionResult("Admin", "Home", null),
            "vendedor" => new RedirectToActionResult("Vendedor", "Home", null),
            "cliente" => new RedirectToActionResult("Portada", "Home", null),
            _ => new RedirectToActionResult("Login", "Home", null)
        };
    }

    private static bool IsAjaxRequest(HttpRequest request)
    {
        return string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }
}

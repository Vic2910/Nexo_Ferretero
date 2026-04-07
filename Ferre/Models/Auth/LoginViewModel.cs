using System.ComponentModel.DataAnnotations;
using Ferre.Services.Auth;

namespace Ferre.Models.Auth;

public sealed class LoginViewModel
{
    public string? ReturnUrl { get; set; }

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public IReadOnlyList<RememberedLoginAccount> RememberedAccounts { get; set; } = Array.Empty<RememberedLoginAccount>();
}

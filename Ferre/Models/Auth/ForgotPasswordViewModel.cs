using System.ComponentModel.DataAnnotations;

namespace Ferre.Models.Auth;

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
    public string Email { get; set; } = string.Empty;
}

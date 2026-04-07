using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Ferre.Models.Auth;

public sealed class ResetPasswordViewModel : IValidatableObject
{
    public string? AccessToken { get; set; }

    public string? RefreshToken { get; set; }

    [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
    public string? Email { get; set; }

    [RegularExpression("^\\d{6}$", ErrorMessage = "El código debe contener 6 dígitos.")]
    public string? RecoveryCode { get; set; }

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [RegularExpression("^(?=.*[A-Z])(?=.*\\d).+$", ErrorMessage = "La contraseña debe incluir al menos una mayúscula y un número.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirma la contraseña.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var isTokenFlow = !string.IsNullOrWhiteSpace(AccessToken);
        if (isTokenFlow)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            yield return new ValidationResult("El correo electrónico es obligatorio.", new[] { nameof(Email) });
        }

        if (string.IsNullOrWhiteSpace(RecoveryCode))
        {
            yield return new ValidationResult("El código es obligatorio.", new[] { nameof(RecoveryCode) });
            yield break;
        }

        if (!Regex.IsMatch(RecoveryCode, "^\\d{6}$"))
        {
            yield return new ValidationResult("El código debe contener 6 dígitos.", new[] { nameof(RecoveryCode) });
        }
    }
}

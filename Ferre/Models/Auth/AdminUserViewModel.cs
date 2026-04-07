using System.ComponentModel.DataAnnotations;

namespace Ferre.Models.Auth;

public sealed class AdminUserViewModel
{
    public string Id { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName => string.Join(" ", new[] { FirstName, LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [RegularExpression("^\\d+$", ErrorMessage = "El teléfono solo debe contener números.")]
    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? ProfileImageUrl { get; set; }

    public string Role { get; set; } = "vendedor";
}

public sealed class AdminUserCreateModel
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [RegularExpression("^(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).{8,}$", ErrorMessage = "La contraseña debe tener al menos 8 caracteres, una mayúscula y un símbolo.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "El rol es obligatorio.")]
    public string Role { get; set; } = "vendedor";
}

public sealed class AdminUserUpdateModel
{
    [Required(ErrorMessage = "El usuario es obligatorio.")]
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [RegularExpression("^\\d+$", ErrorMessage = "El teléfono solo debe contener números.")]
    public string? Phone { get; set; }

    [RegularExpression("^(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).{8,}$", ErrorMessage = "La contraseña debe tener al menos 8 caracteres, una mayúscula y un símbolo.")]
    public string? Password { get; set; }

    [Required(ErrorMessage = "El rol es obligatorio.")]
    public string Role { get; set; } = "vendedor";
}

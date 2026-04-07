using System.ComponentModel.DataAnnotations;
using Ferre.Models.Catalog;

namespace Ferre.Models.Auth;

public sealed class ClientProfileViewModel
{
    public string? UserId { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(80, ErrorMessage = "El nombre no puede superar 80 caracteres.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [MaxLength(80, ErrorMessage = "El apellido no puede superar 80 caracteres.")]
    public string LastName { get; set; } = string.Empty;

    public string FullName => string.Join(" ", new[] { FirstName, LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    public string Email { get; set; } = string.Empty;

    [RegularExpression("^$|^\\d+$", ErrorMessage = "El teléfono solo debe contener números.")]
    public string? Phone { get; set; }

    [MaxLength(250, ErrorMessage = "La dirección no puede superar 250 caracteres.")]
    public string? Address { get; set; }

    public string? ProfileImageUrl { get; set; }

    public string Role { get; set; } = "cliente";

    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? Email : FullName;

    public string Initial => string.IsNullOrWhiteSpace(DisplayName) ? "U" : DisplayName.Substring(0, 1).ToUpperInvariant();

    public IReadOnlyList<Category> Categories { get; set; } = Array.Empty<Category>();

    public IReadOnlyList<Product> Products { get; set; } = Array.Empty<Product>();
}

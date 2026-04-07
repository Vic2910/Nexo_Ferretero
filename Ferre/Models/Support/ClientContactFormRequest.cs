using System.ComponentModel.DataAnnotations;

namespace Ferre.Models.Support;

public sealed class ClientContactFormRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(80, ErrorMessage = "El nombre no puede superar 80 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El asunto es obligatorio.")]
    [MaxLength(120, ErrorMessage = "El asunto no puede superar 120 caracteres.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "El mensaje es obligatorio.")]
    [MaxLength(1500, ErrorMessage = "El mensaje no puede superar 1500 caracteres.")]
    public string Message { get; set; } = string.Empty;
}

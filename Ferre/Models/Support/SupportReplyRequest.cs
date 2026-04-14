using System.ComponentModel.DataAnnotations;

namespace Ferre.Models.Support;

public sealed class SupportReplyRequest
{
    [Required(ErrorMessage = "La conversación es obligatoria.")]
    public Guid ConversationId { get; set; }

    [Required(ErrorMessage = "El mensaje es obligatorio.")]
    [MaxLength(1500, ErrorMessage = "El mensaje no puede superar 1500 caracteres.")]
    public string Message { get; set; } = string.Empty;
}

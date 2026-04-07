using System.ComponentModel.DataAnnotations;

namespace Ferre.Models.Catalog;

public sealed class CategoryFormModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, ErrorMessage = "El nombre no debe superar 80 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "La descripción no debe superar 200 caracteres.")]
    public string? Description { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace Ferre.Models.Catalog;

public sealed class ProductFormModel : IValidatableObject
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no debe superar 120 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999", ErrorMessage = "El precio debe ser mayor a 0.")]
    public decimal Price { get; set; }

    [StringLength(500, ErrorMessage = "La descripción no debe superar 500 caracteres.")]
    public string? Description { get; set; }

    [Range(0, 999999, ErrorMessage = "El stock debe ser mayor o igual a 0.")]
    public int Stock { get; set; }

    [Range(0, 999999, ErrorMessage = "El stock mínimo debe ser mayor o igual a 0.")]
    public int MinStock { get; set; }

    [Required(ErrorMessage = "La categoría es obligatoria.")]
    public Guid? CategoryId { get; set; }

    [StringLength(500, ErrorMessage = "La imagen 1 no debe superar 500 caracteres.")]
    public string? ImageUrl1 { get; set; }

    [StringLength(500, ErrorMessage = "La imagen 2 no debe superar 500 caracteres.")]
    public string? ImageUrl2 { get; set; }

    [StringLength(500, ErrorMessage = "La imagen 3 no debe superar 500 caracteres.")]
    public string? ImageUrl3 { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinStock > Stock)
        {
            yield return new ValidationResult(
                "El stock mínimo no puede ser mayor al stock actual.",
                new[] { nameof(MinStock), nameof(Stock) });
        }

        if (string.IsNullOrWhiteSpace(ImageUrl1)
            && string.IsNullOrWhiteSpace(ImageUrl2)
            && string.IsNullOrWhiteSpace(ImageUrl3))
        {
            yield return new ValidationResult(
                "Debes ingresar al menos una imagen.",
                new[] { nameof(ImageUrl1), nameof(ImageUrl2), nameof(ImageUrl3) });
        }
    }
}

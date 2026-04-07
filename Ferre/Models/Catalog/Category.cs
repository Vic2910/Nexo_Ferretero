using Postgrest.Attributes;
using Postgrest.Models;

namespace Ferre.Models.Catalog;

[Table("categorias")]
public sealed class Category : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("nombre")]
    public string Name { get; set; } = string.Empty;

    [Column("descripcion")]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

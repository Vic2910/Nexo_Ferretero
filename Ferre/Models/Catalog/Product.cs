using Postgrest.Attributes;
using Postgrest.Models;

namespace Ferre.Models.Catalog;

[Table("productos")]
public sealed class Product : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("nombre")]
    public string Name { get; set; } = string.Empty;

    [Column("precio")]
    public decimal Price { get; set; }

    [Column("descripcion")]
    public string? Description { get; set; }

    [Column("categoria_id")]
    public Guid CategoryId { get; set; }

    [Column("imagen_1")]
    public string ImageUrl1 { get; set; } = string.Empty;

    [Column("imagen_2")]
    public string? ImageUrl2 { get; set; }

    [Column("imagen_3")]
    public string? ImageUrl3 { get; set; }

    [Column("stock")]
    public int Stock { get; set; }

    [Column("stock_minimo")]
    public int MinStock { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

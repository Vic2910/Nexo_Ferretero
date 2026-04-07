using Ferre.Models.Catalog;
using Ferre.Services.Common;

namespace Ferre.Services.Catalog;

public interface IProductService
{
    Task<IReadOnlyList<Product>> GetAllAsync();

    Task<Product?> GetByIdAsync(Guid id);

    Task<OperationResult> CreateAsync(ProductFormModel model);

    Task<OperationResult> UpdateAsync(ProductFormModel model);

    Task<OperationResult> DeleteAsync(Guid id);
}
